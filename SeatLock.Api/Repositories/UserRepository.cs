using Dapper;
using SeatLock.Api.Infrastructure.Database;
using SeatLock.Api.Interfaces;
using SeatLock.Api.Models;

namespace SeatLock.Api.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public UserRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                user_id,
                email,
                password_hash,
                role,
                created_at
            FROM users
            WHERE LOWER(email) = LOWER(@Email);
            """;

        return await connection.QuerySingleOrDefaultAsync<User>(
            sql,
            new { Email = email });
    }

    public async Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                user_id,
                email,
                password_hash,
                role,
                created_at
            FROM users
            WHERE user_id = @UserId;
            """;

        return await connection.QuerySingleOrDefaultAsync<User>(
            sql,
            new { UserId = userId });
    }

    public async Task CreateAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO users (
                user_id,
                email,
                password_hash,
                role
            )
            VALUES (
                @UserId,
                @Email,
                @PasswordHash,
                @Role
            );
            """;

        await connection.ExecuteAsync(
            sql,
            new
            {
                user.UserId,
                user.Email,
                user.PasswordHash,
                user.Role
            });
    }
}