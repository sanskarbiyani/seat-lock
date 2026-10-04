using SeatLock.Api.DTOs.Authentication;
using SeatLock.Api.Models;

namespace SeatLock.Api.Interfaces.Authentication;

public interface ITokenService
{
    TokenResponse CreateToken(User user);
}
