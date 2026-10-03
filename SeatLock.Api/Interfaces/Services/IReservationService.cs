using SeatLock.Api.DTOs.Reservations;
using SeatLock.Api.Models.Reservations;

namespace SeatLock.Api.Interfaces.Services;

public interface IReservationService
{
    Task<ReservationResult> ReserveAsync(
        Guid showId,
        Guid userId,
        ReserveRequest request,
        CancellationToken cancellationToken);

    Task<ReservationCancellationResult> ReleaseAsync(
        Guid userId,
        Guid reservationId,
        CancellationToken cancellationToken);
}
