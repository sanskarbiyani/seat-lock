using SeatLock.Api.DTOs.Shows;
using SeatLock.Api.Models.Shows;

namespace SeatLock.Api.Interfaces;

public interface IShowRepository
{
    public Task<List<SeatResponse>> CreateShowAsync(Guid eventId, string name, long pricePaise, int perUserSeatLimit, string[] seats);
    public Task<List<EventDetails>?> GetShowAsync(Guid showId);
}
