namespace SeatLock.Api.Models.Reservations;

public sealed class ReservationDetails
{

    public Guid ReservationId { get; init;}
    public Guid UserId { get; init;}
    public string Status { get; init;} = string.Empty;
    public Guid[] SeatIds { get; init;} = Array.Empty<Guid>();
    public string[] SeatNumbers { get; init;} = Array.Empty<string>();
};