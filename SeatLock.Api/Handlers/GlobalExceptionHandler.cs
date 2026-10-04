using Microsoft.AspNetCore.Diagnostics;
using SeatLock.Api.Observability;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    ApplicationMetrics metrics) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception processing {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        metrics.UnhandledExceptions.Inc();

        context.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        await context.Response.WriteAsJsonAsync(
            new
            {
                error = "An unexpected error occurred."
            },
            cancellationToken);

        return true;
    }
}