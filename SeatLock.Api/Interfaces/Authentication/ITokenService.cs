using SeatLock.Api.DTOs.Authentication;

namespace SeatLock.Api.Interfaces.Authentication;

public interface ITokenService
{
    TokenResponse CreateToken(Guid userId);
}
