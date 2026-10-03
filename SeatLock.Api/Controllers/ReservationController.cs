using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatLock.Api.DTOs.Reservations;
using SeatLock.Api.Interfaces.Services;
using SeatLock.Api.Models.Reservations;

namespace SeatLock.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/shows/{showId:guid}")]
public sealed class ReservationController(
    IReservationService reservationService) : ControllerBase
{
    [HttpPost("reserve")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Reserve(
        Guid showId,
        [FromBody] ReserveRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
            return Unauthorized();

        var result = await reservationService.ReserveAsync(
            showId,
            userId,
            request,
            cancellationToken);

        if (result.Failure is { } failure)
        {
            var (statusCode, title, detail) = failure switch
            {
                ReservationFailure.ShowNotFound =>
                    (StatusCodes.Status404NotFound, "Show not found",
                        "The requested show does not exist."),
                ReservationFailure.InvalidSeatSelection =>
                    (StatusCodes.Status400BadRequest, "Invalid seat selection",
                        "Seats must be non-empty and must not contain duplicates."),
                ReservationFailure.SeatsUnavailable =>
                    (StatusCodes.Status409Conflict, "Seats unavailable",
                        "One or more requested seats are unavailable."),
                ReservationFailure.SeatLimitExceeded =>
                    (StatusCodes.Status409Conflict, "Seat limit exceeded",
                        "The reservation exceeds the per-user seat limit."),
                ReservationFailure.IdempotencyKeyReused =>
                    (StatusCodes.Status409Conflict, "Idempotency key conflict",
                        "This idempotent_key was already used with a different seat selection."),
                _ => throw new InvalidOperationException(
                    $"Unknown reservation failure: {failure}.")
            };

            return Problem(statusCode: statusCode, title: title, detail: detail);
        }

        var response = result.Reservation
            ?? throw new InvalidOperationException(
                "A successful reservation result must contain a reservation.");

        return result.IsReplay ? Ok(response) : StatusCode(
            StatusCodes.Status201Created,
            response);
    }

    /// <summary>Releases a confirmed reservation owned by the authenticated user.</summary>
    [HttpPost("~/api/reservations/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Release(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
            return Unauthorized();

        var result = await reservationService.ReleaseAsync(
            userId,
            id,
            cancellationToken);

        return result switch
        {
            ReservationCancellationResult.Succeeded => Ok(),
            ReservationCancellationResult.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Reservation not found",
                detail: "The reservation does not exist."),
            ReservationCancellationResult.Forbidden => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Reservation access denied",
                detail: "You can only cancel your own reservations."),
            ReservationCancellationResult.NotCancellable => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Reservation cannot be cancelled",
                detail: "The reservation is no longer confirmed."),
            _ => throw new InvalidOperationException(
                $"Unknown reservation cancellation result: {result}.")
        };
    }
}
