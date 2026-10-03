namespace SeatLock.Api.Models.Reservations;

public sealed record ReservationRules(long PricePaise, int PerUserSeatLimit);

public sealed record IdempotencyRecord(string RequestHash, Guid? ReservationId);

public sealed record ReservationRecord(
    string RequestHash,
    Guid ReservationId,
    Guid EventId,
    long AmountPaise,
    string Status,
    string[] Seats);

public sealed record ReservationSeat(Guid SeatId, string SeatNumber, string Status);

public sealed class ReservationRecordRow
{
    public string RequestHash { get; init; } = null!;
    public Guid ReservationId { get; init; }
    public Guid EventId { get; init; }
    public long AmountPaise { get; init; }
    public string Status { get; init; } = null!;
    public string[] Seats { get; init; } = [];
}