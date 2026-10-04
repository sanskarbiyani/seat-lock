using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SeatLock.Api.DTOs.Authentication;
using SeatLock.Api.Interfaces.Authentication;
using SeatLock.Api.Models;

namespace SeatLock.Api.Services;

public sealed class TokenService : ITokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _signingCredentials;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        string signingKey,
        string issuer,
        string audience,
        ILogger<TokenService> logger)
    {
        _issuer = issuer;
        _audience = audience;
        _logger = logger;

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public TokenResponse CreateToken(User user)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.Add(TokenLifetime);

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                user.UserId.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.CreateVersion7().ToString()),

            new Claim(
                ClaimTypes.Role,
                user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _signingCredentials);

        var response = new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);

        _logger.LogInformation(
            "Issued an access token for user {UserId}, expiring at {ExpiresAt}.",
            user.UserId,
            expiresAt);

        return response;
    }
}