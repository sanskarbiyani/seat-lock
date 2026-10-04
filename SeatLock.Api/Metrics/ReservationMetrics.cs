using Prometheus;

namespace SeatLock.Api.Observability;

public sealed class ReservationMetrics
{
    public Counter ReservationAttempts { get; } = Metrics.CreateCounter(
        "reservation_attempts_total",
        "Total number of reservation attempts."
    );

    public Counter SuccessfulReservations { get; } = Metrics.CreateCounter(
        "successful_reservations_total",
        "Total number of successful reservations."
    );

    public Counter ReservationConflicts { get; } =
        Metrics.CreateCounter(
            "reservation_conflicts_total",
            "Total number of reservation conflicts.",
            new CounterConfiguration
            {
                LabelNames = ["reason"]
            }
        );
    
    public Counter ReservationRejections { get; } = Metrics.CreateCounter(
        "reservation_rejections_total",
        "Total number of reservation rejections.",
        new CounterConfiguration
        {
            LabelNames = ["reason"]
        }
    );

    public Counter ReservationReplays { get; } = Metrics.CreateCounter(
        "reservation_replays_total",
        "Total number of reservation replays."
    );

    public Counter ReservationCancellations { get; } = Metrics.CreateCounter(
        "reservation_cancellations_total",
        "Total number of reservation cancellations."
    );

    public Counter ReservationCancellationSuccesses { get; } = Metrics.CreateCounter(
        "reservation_cancellation_successes_total",
        "Total number of successful reservation cancellations."
    );

    public Counter ReservationCancellationRejections { get; } = Metrics.CreateCounter(
        "reservation_cancellation_failures_total",
        "Total number of failed reservation cancellations.",
        new CounterConfiguration
        {
            LabelNames = ["reason"]
        }
    );
}