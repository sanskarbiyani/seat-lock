using SeatLock.Api.Models;

namespace SeatLock.Api.Interfaces.Authentication;

public interface IPasswordService
{
    string HashPassword(User user, string password);

    bool VerifyPassword(
        User user,
        string password,
        string passwordHash);
}