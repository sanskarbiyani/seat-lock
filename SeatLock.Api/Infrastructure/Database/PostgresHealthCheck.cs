using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SeatLock.Api.Infrastructure.Database;

public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly DbConnectionFactory _connectionFactory;

    public PostgresHealthCheck(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();

            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";

            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL is unavailable.",
                ex);
        }
    }
}