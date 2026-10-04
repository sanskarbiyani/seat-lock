using SeatLock.Api.DTOs.Auth;
using SeatLock.Api.DTOs.Authentication;
using SeatLock.Api.Interfaces;
using SeatLock.Api.Interfaces.Authentication;
using SeatLock.Api.Interfaces.Services;
using SeatLock.Api.Models;

namespace SeatLock.Api.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<TokenResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (existingUser is not null)
        {
            _logger.LogWarning(
                "Registration rejected because email {Email} is already registered.",
                email);

            throw new InvalidOperationException(
                "An account with this email already exists.");
        }

        var user = new User
        {
            UserId = Guid.CreateVersion7(),
            Email = email,
            Role = "user"
        };

        user.PasswordHash = _passwordService.HashPassword(
            user,
            request.Password);

        await _userRepository.CreateAsync(user, cancellationToken);

        _logger.LogInformation(
            "Created user {UserId}.",
            user.UserId);

        return _tokenService.CreateToken(user);
    }

    public async Task<TokenResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null ||
            user.PasswordHash is null ||
            user.Email is null)
        {
            _logger.LogWarning(
                "Login failed for email {Email}.",
                email);

            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        var validPassword = _passwordService.VerifyPassword(
            user,
            request.Password,
            user.PasswordHash);

        if (!validPassword)
        {
            _logger.LogWarning(
                "Login failed for email {Email}.",
                email);

            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        _logger.LogInformation(
            "User {UserId} successfully authenticated.",
            user.UserId);

        return _tokenService.CreateToken(user);
    }
}