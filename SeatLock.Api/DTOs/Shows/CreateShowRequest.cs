using System.Text.Json.Serialization;

namespace SeatLock.Api.DTOs.Shows;

public record CreateShowRequest(
    string Name,
    string[] Seats,
    [property: JsonPropertyName("price_paise")]    
    long PricePaise,
    [property: JsonPropertyName("per_user_seat_limit")]
    int PerUserSeatLimit = 4
);
