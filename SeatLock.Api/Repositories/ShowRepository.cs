using Dapper;
using SeatLock.Api.DTOs.Shows;
using SeatLock.Api.Infrastructure.Database;
using SeatLock.Api.Interfaces;
using SeatLock.Api.Models.Shows;

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
        catch (Exception)
        {
            await transaction.RollbackAsync();
            // Log the exception (ex) as needed
            seatDetails = new List<SeatResponse>();
        }

        return seatDetails;
    }

    public async Task<List<EventDetails>?> GetShowAsync(Guid showId)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string querySql = """
        SELECT
            e.event_id,
            s.seat_number,
            s.status,
            e.name,
            e.price_paise,
            e.per_user_seat_limit
        FROM events e
        LEFT JOIN seats s ON e.event_id = s.event_id
        WHERE e.event_id = @ShowId;
        """;

        List<EventDetails> showData;
        try
        {
            showData = (await connection.QueryAsync<EventDetails>(querySql, new { ShowId = showId })).ToList();
        }
        catch (Exception ex)
        {
            // Log the exception (ex) as needed
            return null;
        }

        if (!showData.Any())
            return null;
    
        return showData;
    }
}