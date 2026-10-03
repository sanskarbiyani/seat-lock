namespace SeatLock.Api.Models.Reservations;

public sealed class ReservationDetails
{

    public Guid ReservationId { get; init;}
    public Guid UserId { get; init;}
    public string Status { get; init;}
    public Guid[] SeatIds { get; init;}
    public string[] SeatNumbers { get; init;}
};