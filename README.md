# Seat Reservation at Scale

A concurrent seat reservation service built with **ASP.NET Core, PostgreSQL, Dapper, Docker, Prometheus, and Grafana**.

The primary correctness invariant is:

> A seat can be confirmed for at most one reservation, even when many
> users attempt to reserve it concurrently.

## Live Deployment

**API:** https://seat-lock.onrender.com/

-   `GET /health/live` --- liveness
-   `GET /health/ready` --- readiness including PostgreSQL
-   `GET /metrics` --- Prometheus metrics

## Tech Stack

  Component          Technology
  ------------------ -----------------------------
  API                ASP.NET Core / .NET 10
  Database           PostgreSQL
  Data access        Dapper + Npgsql
  Authentication     JWT Bearer
  Password hashing   ASP.NET Core PasswordHasher
  Containerization   Docker / Docker Compose
  Metrics            Prometheus / prometheus-net
  Dashboard          Grafana
  Testing            xUnit
  IDs                UUIDv7

## Features

-   JWT authentication and role-based authorization
-   Admin-only show creation
-   Concurrent multi-seat reservations
-   Per-user seat limits
-   Idempotent reservation requests
-   Explicit reservation cancellation
-   PostgreSQL transactional concurrency control
-   Prometheus metrics and Grafana dashboard
-   Structured logging and correlation IDs
-   Liveness/readiness health checks
-   Dockerized local development
-   Reproducible burst testing with final reconciliation

## API

### Register

``` http
POST /api/auth/register
```

``` json
{"email":"user@example.com","password":"password123"}
```

### Login

``` http
POST /api/auth/login
```

``` json
{"email":"user@example.com","password":"password123"}
```

Both return a JWT on success.

### Create Show

``` http
POST /api/shows
Authorization: Bearer <admin-token>
```

``` json
{
  "name": "friday-night",
  "seats": ["A1", "A2", "A3", "A4"],
  "price_paise": 25000,
  "per_user_seat_limit": 4
}
```

Requires the `admin` role.

### Get Show

``` http
GET /api/shows/{showId}
```

### Reserve Seats

``` http
POST /api/shows/{showId}/reserve
Authorization: Bearer <user-token>
```

``` json
{
  "seats": ["A1", "A2"],
  "idempotent_key": "unique-client-generated-key"
}
```

Reservations are all-or-nothing.

### Cancel Reservation

``` http
POST /api/reservations/{reservationId}/cancel
Authorization: Bearer <user-token>
```

The implementation uses explicit cancellation rather than time-boxed holds.

## Local Development

### Prerequisites

-   .NET 10 SDK
-   Docker Desktop
-   Docker Compose

### Start

From the repository root:

``` bash
docker compose up -d --build
```

Services:

``` text
API         http://localhost:8080
PostgreSQL  localhost:5432
Prometheus  http://localhost:9090
Grafana     http://localhost:3000
```

Stop:

``` bash
docker compose down
```

Remove the database volume too:

``` bash
docker compose down -v
```

## Configuration

The API requires:

``` text
ConnectionStrings__Postgres
Jwt__SigningKey
Jwt__Issuer - Present in appsettings file
Jwt__Audience - Present in appsettings file
```

Use environment variables, deployment configuration, or .NET User Secrets. Do not commit credentials or JWT signing keys.

## Tests

``` bash
dotnet test
```

## Burst Test

The load-test project reproduces an on-sale stampede. It reports confirmed reservations, declined reservations by reason, other 4xx responses, 5xx responses, transport errors, p50/p95/p99/max latency, and performs final seat-state reconciliation.

``` bash
dotnet run --project SeatLock.LoadTest -- http://localhost:8080
```

### Local 1,000-user normal burst

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
Reconciliation: PASS
```

### Local 1,000-user hot-seat storm

All 1,000 concurrent requests target `A1`:

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
Reconciliation: PASS
```

These are local correctness/load-test results, not a claim of sustained 1,000 requests/second production capacity.

## Observability

Application metrics include:

``` text
reservation_attempts_total
successful_reservations_total
reservation_conflicts_total
reservation_rejections_total
reservation_replays_total
reservation_cancellations_total
reservation_cancellation_successes_total
reservation_cancellation_failures_total
unhandled_exceptions_total
```

HTTP metrics are also exposed. Prometheus labels are kept low-cardinality; user IDs, reservation IDs, and correlation IDs are not used as metric labels.

Requests accept `X-Correlation-ID`; when absent, the API generates a UUIDv7 correlation ID and returns it in the response.

## Project Structure

``` text
SeatLock/
├── SeatLock.Api/
├── SeatLock.Tests/
├── SeatLock.LoadTest/
├── db/migrations/
├── observability/prometheus/
├── docker-compose.yml
└── SeatLock.slnx
```

## Design Summary

The reservation decision is made inside one PostgreSQL transaction:

1.  Load show rules.
2.  Serialize requests for the same user/show.
3.  Claim/check the idempotency key.
4.  Enforce the per-user limit.
5.  Lock requested seats with `SELECT ... FOR UPDATE`.
6.  Verify availability.
7.  Create the reservation and seat mappings.
8.  Mark seats confirmed.
9.  Associate the idempotency key with the reservation.
10. Commit atomically.

PostgreSQL is deliberately the consistency authority. Redis, Kafka, and distributed locking are not part of the critical reservation path for this exercise.

For detailed reasoning, trade-offs, load-test analysis, and limitations, see `WRITEUP.md`.
