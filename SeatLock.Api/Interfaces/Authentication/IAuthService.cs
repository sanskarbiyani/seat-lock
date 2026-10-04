using SeatLock.Api.DTOs.Auth;
using SeatLock.Api.DTOs.Authentication;

namespace SeatLock.Api.Interfaces.Services;

public interface IAuthService
{
    Task<TokenResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<TokenResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}