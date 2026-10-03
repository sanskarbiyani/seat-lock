using SeatLock.Api.DTOs.Shows;
using SeatLock.Api.Interfaces;
using SeatLock.Api.Interfaces.Services;

namespace SeatLock.Api.Services;

public sealed class ShowService: IShowService
{
    private readonly IShowRepository _repository;

    public ShowService(IShowRepository repository)
    {
        _repository = repository;
    }

    public async Task<ShowResponse> CreateShowAsync(CreateShowRequest request)
    {
        if (request.PricePaise < 0)
            throw new ArgumentException("Price cannot be negative.");

        if (request.PerUserSeatLimit <= 0)
            throw new ArgumentException(
                "Per-user seat limit must be greater than zero.");

        if (request.Seats.Length <= 0)
            throw new ArgumentException(
                "Seat count must be greater than zero.");

        var showId = Guid.CreateVersion7();

        var createdSeats = await _repository.CreateShowAsync(
            showId,
            request.Name,
            request.PricePaise,
            request.PerUserSeatLimit,
            request.Seats);

        return new ShowResponse(
            showId,
            createdSeats
        );
    }

    public async Task<GetShowResponse?> GetShowAsync(Guid showId)
    {
        var showData = await _repository.GetShowAsync(showId);
        if (showData == null)
            return null;

        var seats = showData.Select(row => new SeatResponse(
            row.SeatNumber,
            row.Status
        )).ToList();

        var firstRow = showData.First();
        return new GetShowResponse(showId, seats, firstRow.Name, firstRow.PricePaise, firstRow.PerUserSeatLimit);
    }
}