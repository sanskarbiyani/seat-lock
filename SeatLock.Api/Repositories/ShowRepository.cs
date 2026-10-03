using Dapper;
using SeatLock.Api.DTOs.Shows;
using SeatLock.Api.Infrastructure.Database;
using SeatLock.Api.Interfaces;

namespace SeatLock.Api.Repositories;

public sealed class ShowRepository : IShowRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public ShowRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<SeatResponse>> CreateShowAsync(
        Guid eventId,
        string name,
        long pricePaise,
        int perUserSeatLimit,
        string[] seats)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        List<SeatResponse> seatDetails;

        try
        {
            const string insertEventSql = """
            INSERT INTO events (
                event_id,
                name,
                price_paise,
                per_user_seat_limit
            )
            VALUES (
                @EventId,
                @Name,
                @PricePaise,
                @PerUserSeatLimit
            );
            """;

            await connection.ExecuteAsync(
                insertEventSql,
                new
                {
                    EventId = eventId,
                    Name = name,
                    PricePaise = pricePaise,
                    PerUserSeatLimit = perUserSeatLimit
                },
                transaction);

            var seatIds = seats.Select(seat => Guid.CreateVersion7()).ToArray();

            const string insertSeatSql = """
            INSERT INTO seats (
                seat_id,
                event_id,
                seat_number,
                status
            )
            SELECT
                seat_id,
                @EventId,
                seat_number,
                'available'
            FROM unnest(@SeatIds, @SeatNumbers) AS t(seat_id, seat_number)
            RETURNING seat_number, status;
            """;

            seatDetails = (await connection.QueryAsync<SeatResponse>(
                insertSeatSql,
                new
                {
                    SeatIds = seatIds,
                    SeatNumbers = seats,
                    EventId = eventId
                },
                transaction
            )).ToList();

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            // Log the exception (ex) as needed
            seatDetails = new List<SeatResponse>();
        }

        return seatDetails;
    }
}