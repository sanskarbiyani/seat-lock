using System.Security.Cryptography;
using System.Text;
using SeatLock.Api.DTOs.Reservations;
using SeatLock.Api.Interfaces.Repositories;
using SeatLock.Api.Interfaces.Services;
using SeatLock.Api.Models.Reservations;

namespace SeatLock.Api.Services;

public sealed class ReservationService(
    IReservationRepository reservationRepository,
    ILogger<ReservationService> logger) : IReservationService
{
    public async Task<ReservationCancellationResult> ReleaseAsync(
        Guid userId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        await using var session =
            await reservationRepository.BeginAsync(cancellationToken);

        var details = await session.GetReservationDetails(
            reservationId,
            cancellationToken);

        if(details is null)
        {
            await session.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Reservation cancellation rejected because reservation {ReservationId} was not found.",
                reservationId);
            return ReservationCancellationResult.NotFound;
        }

        if(details.UserId != userId)
        {
            await session.CommitAsync(cancellationToken);
            logger.LogWarning(
                "User {UserId} attempted to cancel reservation {ReservationId} owned by another user.",
                userId,
                reservationId);
            return ReservationCancellationResult.Forbidden;
        }

        if (!string.Equals(
                details.Status,
                "confirmed",
                StringComparison.Ordinal))
        {
            await session.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Reservation {ReservationId} cannot be cancelled because its status is {ReservationStatus}.",
                reservationId,
                details.Status);
            return ReservationCancellationResult.NotCancellable;
        }

        if (details.SeatIds.Length == 0)
        {
            logger.LogError(
                "Confirmed reservation {ReservationId} has no associated seats.",
                reservationId);
            throw new InvalidOperationException(
                "A confirmed reservation must contain at least one seat.");
        }

        var cancelledReservations = await session.CancelReservationAsync(
            userId,
            reservationId,
            cancellationToken);

        if (cancelledReservations == 0)
        {
            logger.LogInformation(
                "Reservation {ReservationId} was no longer cancellable when the cancellation update ran.",
                reservationId);
            return ReservationCancellationResult.NotCancellable;
        }
        if (cancelledReservations != 1)
        {
            logger.LogError(
                "Cancellation of reservation {ReservationId} updated {AffectedReservations} reservation rows; expected exactly one.",
                reservationId,
                cancelledReservations);
            throw new InvalidOperationException(
                "Cancelling a reservation must update exactly one reservation.");
        }

        var releasedSeats = await session.ReleaseSeatsAsync(
            details.SeatIds,
            cancellationToken);
        if (releasedSeats != details.SeatIds.Length)
        {
            logger.LogError(
                "Cancellation of reservation {ReservationId} released {ReleasedSeats} of {ExpectedSeats} seats.",
                reservationId,
                releasedSeats,
                details.SeatIds.Length);
            throw new InvalidOperationException(
                "Releasing a reservation must update all of its confirmed seats.");
        }

        await session.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Cancelled reservation {ReservationId} for user {UserId} and released {ReleasedSeats} seats.",
            reservationId,
            userId,
            releasedSeats);
        return ReservationCancellationResult.Succeeded;
    }

    public async Task<ReservationResult> ReserveAsync(
        Guid showId,
        Guid userId,
        ReserveRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Seats.Length == 0
            || request.Seats.Any(string.IsNullOrWhiteSpace)
            || request.Seats.Distinct(StringComparer.Ordinal).Count()
                != request.Seats.Length)
        {
            logger.LogWarning(
                "Reservation rejected for show {ShowId} and user {UserId} because the seat selection is invalid.",
                showId,
                userId);
            return ReservationResult.Failed(
                ReservationFailure.InvalidSeatSelection);
        }

        var requestHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join(
                "\n",
                request.Seats.Order(StringComparer.Ordinal)))));

        await using var session =
            await reservationRepository.BeginAsync(cancellationToken);

        var rules = await session.GetRulesAsync(showId, cancellationToken);
        if (rules is null)
        {
            logger.LogInformation(
                "Reservation rejected because show {ShowId} was not found.",
                showId);
            return ReservationResult.Failed(ReservationFailure.ShowNotFound);
        }

        await session.LockUserForShowAsync(
            showId,
            userId,
            cancellationToken);

        var isNewIdempotencyKey = await session.TryAddIdempotencyKeyAsync(
            showId,
            userId,
            request.IdempotentKey,
            requestHash,
            cancellationToken);
        if (!isNewIdempotencyKey)
        {
            var existingReservation =
                await session.GetReservationForIdempotencyKeyAsync(
                    showId,
                    userId,
                    request.IdempotentKey,
                    cancellationToken)
                ?? throw LogMissingIdempotencyReservation(showId, userId);

            if (!string.Equals(
                    existingReservation.RequestHash,
                    requestHash,
                    StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Reservation rejected for show {ShowId} and user {UserId} because an idempotency key was reused with a different seat selection.",
                    showId,
                    userId);
                return ReservationResult.Failed(
                    ReservationFailure.IdempotencyKeyReused);
            }

            await session.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Replayed reservation {ReservationId} for show {ShowId} and user {UserId}.",
                existingReservation.ReservationId,
                showId,
                userId);
            return ReservationResult.Succeeded(
                ToResponse(existingReservation),
                isReplay: true);
        }

        var existingSeatCount = await session.CountConfirmedSeatsAsync(
            showId,
            userId,
            cancellationToken);
        if (existingSeatCount + request.Seats.Length > rules.PerUserSeatLimit)
        {
            logger.LogInformation(
                "Reservation rejected for show {ShowId} and user {UserId}: requested {RequestedSeats} seats with {ExistingSeats} already confirmed, exceeding limit {SeatLimit}.",
                showId,
                userId,
                request.Seats.Length,
                existingSeatCount,
                rules.PerUserSeatLimit);
            return ReservationResult.Failed(
                ReservationFailure.SeatLimitExceeded);
        }

        var seats = await session.GetSeatsForUpdateAsync(
            showId,
            request.Seats,
            cancellationToken);
        if (seats.Count != request.Seats.Length
            || seats.Any(seat => seat.Status != "available"))
        {
            logger.LogInformation(
                "Reservation rejected for show {ShowId} and user {UserId} because one or more of {RequestedSeats} requested seats are unavailable.",
                showId,
                userId,
                request.Seats.Length);
            return ReservationResult.Failed(
                ReservationFailure.SeatsUnavailable);
        }

        var reservationId = Guid.CreateVersion7();
        var amountPaise = checked(rules.PricePaise * request.Seats.Length);

        await session.InsertReservationAsync(
            reservationId,
            showId,
            userId,
            amountPaise,
            cancellationToken);
        var seatIds = seats.Select(seat => seat.SeatId).ToArray();
        await session.InsertReservationSeatsAsync(
            reservationId,
            seatIds,
            cancellationToken);
        await session.ConfirmSeatsAsync(seatIds, cancellationToken);
        await session.SetIdempotencyReservationAsync(
            showId,
            userId,
            request.IdempotentKey,
            reservationId,
            cancellationToken);

        await session.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Created reservation {ReservationId} for show {ShowId} and user {UserId} with {ReservedSeats} seats.",
            reservationId,
            showId,
            userId,
            request.Seats.Length);
        return ReservationResult.Succeeded(new ReservationResponse(
            reservationId,
            showId,
            request.Seats,
            amountPaise,
            "confirmed"));
    }

    private static ReservationResponse ToResponse(
        ReservationRecord reservation) =>
        new(
            reservation.ReservationId,
            reservation.EventId,
            reservation.Seats.ToArray(),
            reservation.AmountPaise,
            reservation.Status);

    private InvalidOperationException LogMissingIdempotencyReservation(
        Guid showId,
        Guid userId)
    {
        logger.LogError(
            "An existing idempotency key for show {ShowId} and user {UserId} does not reference a reservation.",
            showId,
            userId);

        return new InvalidOperationException(
            "The idempotency key does not reference a reservation.");
    }
}
