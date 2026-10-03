using SeatLock.Api.DTOs.Reservations;

namespace SeatLock.Api.Models.Reservations;

public sealed record ReservationResult(
    ReservationResponse? Reservation,
    ReservationFailure? Failure,
    bool IsReplay)
{
    public static ReservationResult Succeeded(
        ReservationResponse reservation,
        bool isReplay = false) =>
        new(reservation, null, isReplay);

    public static ReservationResult Failed(ReservationFailure failure) =>
        new(null, failure, false);
}

public enum ReservationFailure
{
    ShowNotFound,
    InvalidSeatSelection,
    SeatsUnavailable,
    SeatLimitExceeded,
    IdempotencyKeyReused
}
