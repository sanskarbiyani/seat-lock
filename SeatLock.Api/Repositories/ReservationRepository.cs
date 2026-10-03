using System.ComponentModel;
using Dapper;
using Npgsql;
using SeatLock.Api.Infrastructure.Database;
using SeatLock.Api.Interfaces.Repositories;
using SeatLock.Api.Models.Reservations;

namespace SeatLock.Api.Repositories;

public sealed class ReservationRepository(
    DbConnectionFactory connectionFactory) : IReservationRepository
{
    public async Task<IReservationSession> BeginAsync(
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();
        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction =
                await connection.BeginTransactionAsync(cancellationToken);
            return new ReservationSession(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class ReservationSession(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction) : IReservationSession
    {
        public async Task<ReservationRules?> GetRulesAsync(
            Guid showId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT price_paise, per_user_seat_limit
                FROM events
                WHERE event_id = @ShowId;
                """;

            return await connection.QuerySingleOrDefaultAsync<ReservationRules>(
                new CommandDefinition(
                    sql,
                    new { ShowId = showId },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        public async Task LockUserForShowAsync(
            Guid showId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            const string ensureSql = """
                INSERT INTO event_users (event_id, user_id)
                VALUES (@ShowId, @UserId)
                ON CONFLICT (event_id, user_id) DO NOTHING;
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                ensureSql,
                new { ShowId = showId, UserId = userId },
                transaction,
                cancellationToken: cancellationToken));

            const string lockSql = """
                SELECT user_id
                FROM event_users
                WHERE event_id = @ShowId
                  AND user_id = @UserId
                FOR UPDATE;
                """;
            await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                lockSql,
                new { ShowId = showId, UserId = userId },
                transaction,
                cancellationToken: cancellationToken));
        }

        public async Task<bool> TryAddIdempotencyKeyAsync(
            Guid showId,
            Guid userId,
            string idempotentKey,
            string requestHash,
            CancellationToken cancellationToken)
        {
            const string sql = """
                INSERT INTO idempotency_keys (
                    idempotency_key,
                    event_id,
                    user_id,
                    request_hash
                )
                VALUES (@IdempotentKey, @ShowId, @UserId, @RequestHash)
                ON CONFLICT (event_id, user_id, idempotency_key) DO NOTHING
                RETURNING 1;
                """;

            var inserted = await connection.QuerySingleOrDefaultAsync<int?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        IdempotentKey = idempotentKey,
                        ShowId = showId,
                        UserId = userId,
                        RequestHash = requestHash
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            return inserted.HasValue;
        }

        public async Task<ReservationRecord?> GetReservationForIdempotencyKeyAsync(
            Guid showId,
            Guid userId,
            string idempotentKey,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    ik.request_hash,
                    r.reservation_id,
                    r.event_id,
                    r.amount_paise,
                    r.status,
                    array_agg(s.seat_number ORDER BY s.seat_number)::text[] AS seats
                FROM idempotency_keys ik
                INNER JOIN reservations r
                    ON r.reservation_id = ik.reservation_id
                INNER JOIN reservation_seats rs
                    ON rs.reservation_id = r.reservation_id
                INNER JOIN seats s
                    ON s.seat_id = rs.seat_id
                WHERE ik.event_id = @ShowId
                  AND ik.user_id = @UserId
                  AND ik.idempotency_key = @IdempotentKey
                GROUP BY
                    ik.request_hash,
                    r.reservation_id,
                    r.event_id,
                    r.amount_paise,
                    r.status;
                """;

            var row =  await connection.QuerySingleOrDefaultAsync<ReservationRecordRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        IdempotentKey = idempotentKey,
                        ShowId = showId,
                        UserId = userId
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            
            return row is null
                    ? null
                    : new ReservationRecord(
                        row.RequestHash,
                        row.ReservationId,
                        row.EventId,
                        row.AmountPaise,
                        row.Status,
                        row.Seats);
        }

        public async Task<long> CountConfirmedSeatsAsync(
            Guid showId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT COUNT(*)
                FROM reservations r
                INNER JOIN reservation_seats rs
                    ON rs.reservation_id = r.reservation_id
                WHERE r.event_id = @ShowId
                  AND r.user_id = @UserId
                  AND r.status = 'confirmed';
                """;

            return await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    sql,
                    new { ShowId = showId, UserId = userId },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        public async Task<IReadOnlyList<ReservationSeat>> GetSeatsForUpdateAsync(
            Guid showId,
            string[] seatNumbers,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    seat_id AS "SeatId",
                    seat_number AS "SeatNumber",
                    status AS "Status"
                FROM seats
                WHERE event_id = @ShowId
                  AND seat_number = ANY(@SeatNumbers)
                ORDER BY seat_number
                FOR UPDATE;
                """;

            var seats = await connection.QueryAsync<ReservationSeat>(
                new CommandDefinition(
                    sql,
                    new { ShowId = showId, SeatNumbers = seatNumbers },
                    transaction,
                    cancellationToken: cancellationToken));
            return seats.AsList();
        }

        public async Task InsertReservationAsync(
            Guid reservationId,
            Guid showId,
            Guid userId,
            long amountPaise,
            CancellationToken cancellationToken)
        {
            const string sql = """
                INSERT INTO reservations (
                    reservation_id,
                    event_id,
                    user_id,
                    amount_paise
                )
                VALUES (
                    @ReservationId,
                    @ShowId,
                    @UserId,
                    @AmountPaise
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    ReservationId = reservationId,
                    ShowId = showId,
                    UserId = userId,
                    AmountPaise = amountPaise
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        public async Task InsertReservationSeatsAsync(
            Guid reservationId,
            Guid[] seatIds,
            CancellationToken cancellationToken)
        {
            const string sql = """
                INSERT INTO reservation_seats (reservation_id, seat_id)
                SELECT @ReservationId, seat_id
                FROM unnest(@SeatIds) AS t(seat_id);
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ReservationId = reservationId, SeatIds = seatIds },
                transaction,
                cancellationToken: cancellationToken));
        }

        public async Task ConfirmSeatsAsync(
            Guid[] seatIds,
            CancellationToken cancellationToken)
        {
            const string sql = """
                UPDATE seats
                SET status = 'confirmed'
                WHERE seat_id = ANY(@SeatIds);
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { SeatIds = seatIds },
                transaction,
                cancellationToken: cancellationToken));
        }

        public async Task SetIdempotencyReservationAsync(
            Guid showId,
            Guid userId,
            string idempotentKey,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                UPDATE idempotency_keys
                SET reservation_id = @ReservationId
                WHERE event_id = @ShowId
                  AND user_id = @UserId
                  AND idempotency_key = @IdempotentKey;
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    ReservationId = reservationId,
                    ShowId = showId,
                    UserId = userId,
                    IdempotentKey = idempotentKey
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        public async Task<int> CancelReservationAsync(
            Guid userId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            const string cancelSql = """
                UPDATE reservations
                SET status = 'cancelled',
                    cancelled_at = NOW()
                WHERE reservation_id = @ReservationId
                  AND user_id = @UserId
                  AND status = 'confirmed';
                """;

            return await connection.ExecuteAsync(
                new CommandDefinition(
                    cancelSql,
                    new
                    {
                        ReservationId = reservationId,
                        UserId = userId
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        public async Task<int> ReleaseSeatsAsync(
            Guid[] seatIds,
            CancellationToken cancellationToken)
        {
            const string releaseSeatsSql = """
                UPDATE seats
                SET status = 'available'
                WHERE seat_id = ANY(@SeatIds)
                AND status = 'confirmed';
                """;

            return await connection.ExecuteAsync(new CommandDefinition(
                releaseSeatsSql,
                new
                {
                    SeatIds = seatIds
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        public async Task<ReservationDetails?> GetReservationDetails(
            Guid guid, CancellationToken
            cancellationToken)
        {
            const string sql = """"
            SELECT
                r.reservation_id,
                r.user_id,
                r.status,
                array_agg(s.seat_id ORDER BY s.seat_number)::uuid[] AS seat_ids,
                array_agg(s.seat_number ORDER BY s.seat_number)::text[] AS seat_numbers
            FROM reservations r
            JOIN reservation_seats rs
                ON rs.reservation_id = r.reservation_id
            JOIN seats s
                ON s.seat_id = rs.seat_id
            WHERE r.reservation_id = @ReservationId
            GROUP BY
                r.reservation_id,
                r.user_id,
                r.status;
            """";

            return await connection.QueryFirstOrDefaultAsync<ReservationDetails>(
                new CommandDefinition(
                    sql,
                    new { ReservationId = guid },
                    transaction,
                    cancellationToken: cancellationToken)
            );
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
