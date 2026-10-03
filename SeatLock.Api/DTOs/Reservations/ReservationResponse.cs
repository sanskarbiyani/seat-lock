using System.Text.Json.Serialization;

namespace SeatLock.Api.DTOs.Reservations;

/// <summary>Describes a successfully reserved set of seats.</summary>
public sealed record ReservationResponse(
    [property: JsonPropertyName("reservation_id")] Guid ReservationId,
    [property: JsonPropertyName("show_id")] Guid ShowId,
    [property: JsonPropertyName("seats")] IReadOnlyList<string> Seats,
    [property: JsonPropertyName("amount_paise")] long AmountPaise,
    [property: JsonPropertyName("status")] string Status);
