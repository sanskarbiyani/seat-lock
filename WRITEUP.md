# Seat Reservation at Scale --- Design & Deployment Write-up

## 1. Overview

This project implements a seat reservation system designed to remain correct under concurrent reservation attempts. The primary requirement is to guarantee that the same seat cannot be confirmed for two different users while also supporting idempotency, per-user limits, explicit cancellation, authentication, and observability.

The implementation uses ASP.NET Core / .NET 10, PostgreSQL, Dapper/Npgsql, JWT authentication, Docker, Prometheus, and Grafana. The critical reservation decision is made transactionally in PostgreSQL.

## 2. Core Invariant

> A seat may have at most one confirmed reservation at any point in
> time.

A multi-seat reservation is all-or-nothing. If a user requests `A1`, `A2`, and `A3`, the system either confirms all three or confirms none.

## 3. Atomic Reservation Decision

The reservation operation is implemented as a single PostgreSQL transaction:

1.  Load show configuration and reservation rules.
2.  Serialize concurrent requests from the same user for the same show.
3.  Claim/check the idempotency key.
4.  Enforce the per-user seat limit.
5.  Lock requested seat rows using `SELECT ... FOR UPDATE`.
6.  Verify that all requested seats exist and are available.
7.  Insert the reservation.
8.  Insert reservation-to-seat mappings.
9.  Change the seats to `confirmed`.
10. Associate the idempotency key with the reservation.
11. Commit.

If any step fails, the transaction is rolled back. This prevents a partially committed reservation decision.

## 4. Concurrency Control

Requested seats are selected with:

``` sql
SELECT seat_id, seat_number, status
FROM seats
WHERE event_id = @ShowId
  AND seat_number = ANY(@SeatNumbers)
ORDER BY seat_number
FOR UPDATE;
```

For two users simultaneously requesting `A1`:

``` text
Transaction A -> locks A1
Transaction B -> waits for A1

Transaction A -> confirms A1 -> COMMIT
Transaction B -> obtains lock -> sees A1 is confirmed -> 409
```

The second transaction therefore cannot confirm the same seat.

### Deterministic locking order

Multiple requested seats are locked in `seat_number` order. This reduces deadlock risk when requests overlap on multiple seats.

## 5. Same-User Serialization

An `event_users` row provides a serialization point for each `(event_id, user_id)` pair and is locked with `FOR UPDATE`.

This matters for the per-user limit. Without serialization, two concurrent requests could both read the same current usage and both decide they are within the limit. With the lock, the second request waits and re-evaluates after the first transaction commits.

Different users do not contend on this serialization row and can proceed independently until they overlap on actual seat rows.

## 6. Idempotency

Each reservation request contains a client-supplied idempotency key scoped to:

``` text
(event_id, user_id, idempotency_key)
```

The requested seat selection is hashed using SHA-256.

For a new key, the key is inserted, the reservation is performed, the reservation ID is associated with the key, and both are committed in the same transaction.

For the same key and same request, the existing reservation is returned as a replay. For the same key with a different request, the request is rejected as an idempotency conflict.

Because the idempotency record and reservation are committed together, a failed reservation does not permanently consume the key.

## 7. Why PostgreSQL Is the Consistency Authority

Redis, Kafka, a distributed lock service, and other external coordination mechanisms were deliberately not introduced into the critical reservation path.

PostgreSQL already owns seat state, reservation state, idempotency state, and user/show limits. It therefore has the information required to make the atomic decision.

Using database transactions and row-level locks avoids introducing another distributed consistency boundary.

The trade-off is that PostgreSQL becomes the critical coordination point. For this exercise, that is preferable to adding architectural complexity without a demonstrated requirement for it.

At larger scale, database capacity, connection limits, lock contention, and partitioning strategy would become explicit scaling concerns.

## 8. Cancellation / Hold Model

The implementation uses explicit cancellation rather than time-boxed holds.

A successful cancellation changes:

``` text
reservation -> cancelled
seat        -> available
```

transactionally.

A timed hold would introduce additional states and expiry behavior. 
Explicit cancellation keeps the state machine smaller for this exercise.

## 9. Authentication and Authorization

The API uses JWT Bearer authentication. Users can register, log in, reserve seats, and cancel their own reservations. Show creation requires the `admin` role.

Passwords are stored using ASP.NET Core's password hashing implementation. JWT signing keys and database credentials are supplied through environment configuration or User Secrets and are not committed to the repository.

## 10. Observability

Prometheus metrics are exposed through `/metrics`.

Application metrics include reservation attempts, successful reservations, reservation conflicts by reason, idempotency replays, cancellation attempts/successes/conflicts and unhandled exceptions.
HTTP metrics additionally provide request counts, status codes, request durations, and requests in progress.

Metric labels are deliberately low-cardinality. User IDs, reservation IDs, and correlation IDs are not Prometheus labels.

### Correlation IDs

The API accepts `X-Correlation-ID`. If absent, the application generates a UUIDv7 identifier. The ID is returned in the response and included in the logging scope.

## 11. Health Model

`/health/ready` includes the PostgreSQL dependency. This separates process liveness from readiness to serve requests requiring the database.

## 12. Load Testing

The load-test runner reproduces an on-sale stampede and reports confirmed reservations, declined reservations by reason, other 4xx responses, 5xx responses, transport failures, p50/p95/p99/max latency, and final seat-state reconciliation.

The final reconciliation is important because HTTP response counts alone are not sufficient evidence of correctness.

## 13. Local 1,000-Request Results

The application was tested locally with **1,000 concurrent users**.

### Normal on-sale burst

Each user attempted to reserve a distinct seat.

``` text
Requests:      1000
Confirmed:     1000
Other 4xx:        0
5xx:               0
Transport err:     0
p50 latency:    101.2 ms
p95 latency:    122.3 ms
p99 latency:    131.0 ms
Max latency:    141.9 ms
```

Final reconciliation:

``` text
Available:             0
Confirmed:          1000
Expected confirmed: 1000
Result: PASS
```

### Hot-seat storm

All 1,000 users simultaneously attempted to reserve `A1`.

``` text
Requests:             1000
Concurrency:          1000
Confirmed:               1
409 Seats unavailable: 999
Other 4xx:               0
5xx:                      0
Transport err:            0
p50 latency:           97.2 ms
p95 latency:          164.9 ms
p99 latency:          173.2 ms
Max latency:          174.6 ms
```

Final reconciliation:

``` text
Available:             0
Confirmed:              1
Expected confirmed:     1
Result: PASS
```

This is the most important concurrency result. Despite 1,000 simultaneous attempts against the same seat, exactly one reservation was confirmed and the remaining 999 requests were rejected with the expected `409 Conflict`. The final database-backed show state independently confirmed the same result.

These measurements are not intended to establish a production capacity limit. They demonstrate **1,000 concurrent requests**, not sustained 1,000 requests per second.

## 14. Live Deployment Validation

The application was deployed publicly on Render with PostgreSQL and validated with a 20-request concurrent burst.

### Normal burst

``` text
Requests:      20
Confirmed:     20
Other 4xx:      0
5xx:             0
Transport err:   0
p50 latency:  2594.6 ms
p95 latency:  2723.5 ms
p99 latency:  2946.2 ms
Max latency:  3001.9 ms
Reconciliation: PASS
```

### Hot-seat storm

``` text
Requests:              20
Confirmed:              1
409 Seats unavailable: 19
Other 4xx:              0
5xx:                     0
Transport err:           0
p50 latency:          1060.6 ms
p95 latency:          1081.7 ms
p99 latency:          1141.2 ms
Max latency:          1156.1 ms
Reconciliation: PASS
```

The deployed instance uses a constrained hosting tier, so these latency measurements should not be interpreted as a production capacity benchmark. The live test demonstrates that the reservation correctness properties hold in the deployed environment.

## 15. Testing Strategy

The test suite covers successful reservation, unavailable seats, per-user limits, idempotent replay, idempotency-key reuse, cancellation, repeated cancellation, cancellation authorization, reservation/cancellation races, concurrent cancellation scenarios, and final database-state validation.

The load test complements functional tests by exercising simultaneous requests against the same database state.

## 16. Failure Handling

The application has a global exception handler that converts unexpected exceptions into structured HTTP 500 responses without exposing internal exception details.

Expected business conflicts are returned as `409 Conflict`, including seats unavailable, seat limit exceeded, idempotency-key reuse with a different request, and invalid reservation state.

## 17. Design Trade-offs

### PostgreSQL row locks vs distributed locking

**Choice:** PostgreSQL row locks.

**Reason:** PostgreSQL already owns the state being protected, so a separate distributed lock service is unnecessary for this design.

### Explicit cancellation vs timed holds

**Choice:** Explicit cancellation.

**Reason:** Smaller state machine and no expiry scheduler.

### UUIDv7 vs UUIDv4

**Choice:** UUIDv7.

**Reason:** UUIDv7 provides globally unique identifiers while providing temporal ordering that is more index-friendly than fully random UUIDv4 identifiers.

### Integer money representation

Prices are stored as integer paise, avoiding floating-point representation issues for monetary values.

### No Redis/Kafka in the critical path

The required correctness properties can be established with PostgreSQL transactions and row-level locks. Adding Redis or Kafka would introduce additional operational and consistency complexity without being necessary for the reservation decision.

## 18. Known Limitations and Next Steps

Potential production improvements include:

1.  Capacity testing on appropriately sized infrastructure.
2.  PostgreSQL connection-pool and database saturation monitoring.
3.  Rate limiting/admission control around extreme bursts.
4.  OpenTelemetry distributed tracing.
5.  A production-grade migration/versioning mechanism.
6.  Stronger relational constraints ensuring reservation-seat mappings cannot cross event boundaries.
7.  An explicit database scaling/partitioning strategy for very large datasets.
8.  A pre-created authenticated user pool for repeated load tests so test setup does not become the bottleneck.

The current Render deployment is intended to demonstrate correctness,
deployment, and observability rather than production-scale capacity.

## 19. AI Usage

AI assistance was used during development for reviewing concurrency and transaction design, identifying race conditions, reviewing API and database structure, debugging PostgreSQL/Dapper behavior, reviewing Docker/deployment configuration, designing Prometheus metrics, analyzing load-test results, and improving test coverage and failure handling.

The implementation, architectural decisions, testing, deployment, and validation were reviewed and executed as part of the project development process.

## 20. Conclusion

The system's core correctness strategy is deliberately simple:

> Let PostgreSQL make the atomic reservation decision.

The reservation transaction serializes requests where necessary, locks the relevant seat rows, validates the complete request, updates reservation state, and commits the decision atomically.

The local 1,000-request tests demonstrated:

``` text
1,000 concurrent normal requests
-> 1,000 confirmed
-> 0 failures
-> reconciliation PASS
```

and:

``` text
1,000 concurrent requests for one seat
-> 1 confirmed
-> 999 declined
-> 0 failures
-> reconciliation PASS
```

The deployed Render environment independently reproduced the same correctness properties at 20 concurrent requests.

The resulting system demonstrates the primary requirement of the exercise: concurrent requests cannot double-sell a seat, and the final database state is reconciled against the expected outcome.
