namespace SeatLock.Api.DTOs.Authentication;

/// <summary>Contains a JWT access token and its expiration time.</summary>
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
