using Prometheus;

namespace SeatLock.Api.Observability;

public sealed class ApplicationMetrics
{
    public Counter UnhandledExceptions { get; } = Metrics.CreateCounter(
        "unhandled_exceptions_total",
        "Total number of unhandled exceptions."
    );
}