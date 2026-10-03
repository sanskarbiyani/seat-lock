namespace SeatLock.Api.Models.Shows;

public record EventDetails(
    Guid EventId,
    String SeatNumber,
    String Status,
    string Name,
    long PricePaise,
    int PerUserSeatLimit
);