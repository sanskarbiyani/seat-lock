namespace SeatLock.Api.DTOs.Shows;

public record ShowResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("show_id")]
    Guid ShowId,
    IReadOnlyList<SeatResponse> Seats
);

public record SeatResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("seat_number")]
    string SeatNumber,
    string Status
);