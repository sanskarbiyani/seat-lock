using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SeatLock.Api.DTOs.Auth;

public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;
}