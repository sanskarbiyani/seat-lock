namespace SeatLock.Api.Models;

public class User
{
    public Guid UserId { get; set; }

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public string Role { get; set; } = "user";

    public DateTime CreatedAt { get; set; }
}