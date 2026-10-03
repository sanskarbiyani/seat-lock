namespace SeatLock.Api.Models.Reservations;

public enum ReservationCancellationResult
{
    Succeeded,
    NotFound,
    Forbidden,
    NotCancellable
}
