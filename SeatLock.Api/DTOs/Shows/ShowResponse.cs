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

public record GetShowResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("show_id")]
    Guid ShowId,
    IReadOnlyList<SeatResponse> Seats,
    string Name,
    [property: System.Text.Json.Serialization.JsonPropertyName("price_paise")]
    long PricePaise,
    [property: System.Text.Json.Serialization.JsonPropertyName("per_user_seat_limit")]
    int PerUserSeatLimit
);