using SeatLock.Api.DTOs.Shows;

namespace SeatLock.Api.Interfaces.Services;

public interface IShowService
{
    public Task<ShowResponse> CreateShowAsync(CreateShowRequest request);

}
