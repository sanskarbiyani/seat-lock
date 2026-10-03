using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SeatLock.Api.DTOs.Reservations;

/// <summary>Specifies the seats to reserve and the idempotency key for the request.</summary>
public sealed record ReserveRequest
{
    /// <summary>The seat numbers to reserve.</summary>
    [Required, MinLength(1)]
    [JsonPropertyName("seats")]
    public required string[] Seats { get; init; }

    /// <summary>A client-generated key used to safely retry this request.</summary>
    [Required, MaxLength(255)]
    [JsonPropertyName("idempotent_key")]
    public required string IdempotentKey { get; init; }
}
