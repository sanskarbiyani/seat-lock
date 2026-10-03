using SeatLock.Api.Models.Reservations;

namespace SeatLock.Api.Interfaces.Repositories;

public interface IReservationRepository
{
    Task<IReservationSession> BeginAsync(CancellationToken cancellationToken);
}

public interface IReservationSession : IAsyncDisposable
{
    Task<int> CancelReservationAsync(
        Guid userId,
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<int> ReleaseSeatsAsync(
        Guid[] seatIds,
        CancellationToken cancellationToken);

    Task<ReservationRules?> GetRulesAsync(
        Guid showId,
        CancellationToken cancellationToken);

    Task LockUserForShowAsync(
        Guid showId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> TryAddIdempotencyKeyAsync(
        Guid showId,
        Guid userId,
        string idempotentKey,
        string requestHash,
        CancellationToken cancellationToken);

    Task<ReservationRecord?> GetReservationForIdempotencyKeyAsync(
        Guid showId,
        Guid userId,
        string idempotentKey,
        CancellationToken cancellationToken);

    Task<long> CountConfirmedSeatsAsync(
        Guid showId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReservationSeat>> GetSeatsForUpdateAsync(
        Guid showId,
        string[] seatNumbers,
        CancellationToken cancellationToken);

    Task InsertReservationAsync(
        Guid reservationId,
        Guid showId,
        Guid userId,
        long amountPaise,
        CancellationToken cancellationToken);

    Task InsertReservationSeatsAsync(
        Guid reservationId,
        Guid[] seatIds,
        CancellationToken cancellationToken);

    Task ConfirmSeatsAsync(
        Guid[] seatIds,
        CancellationToken cancellationToken);

    Task SetIdempotencyReservationAsync(
        Guid showId,
        Guid userId,
        string idempotentKey,
        Guid reservationId,
        CancellationToken cancellationToken);

    public Task<ReservationDetails?> GetReservationDetails(
            Guid guid, CancellationToken
            cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);
}
