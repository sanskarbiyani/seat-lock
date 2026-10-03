using System.Text.Json.Serialization;

namespace SeatLock.Api.DTOs.Authentication;

/// <summary>Identifies the user for whom an access token is requested.</summary>
public sealed record TokenRequest(
    [property: JsonPropertyName("user_id")]
    Guid UserId
);