using SeatLock.Api.DTOs.Shows;
using SeatLock.Api.Interfaces;
using SeatLock.Api.Interfaces.Services;

namespace SeatLock.Api.Services;

public sealed class ShowService: IShowService
{
    private readonly IShowRepository _repository;
    private readonly ILogger<ShowService> _logger;

    public ShowService(
        IShowRepository repository,
        ILogger<ShowService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ShowResponse> CreateShowAsync(CreateShowRequest request)
    {
        if (request.PricePaise < 0)
        {
            _logger.LogWarning(
                "Show creation rejected because price {PricePaise} is negative.",
                request.PricePaise);
            throw new ArgumentException("Price cannot be negative.");
        }

        if (request.PerUserSeatLimit <= 0)
        {
            _logger.LogWarning(
                "Show creation rejected because per-user seat limit {SeatLimit} is not positive.",
                request.PerUserSeatLimit);
            throw new ArgumentException(
                "Per-user seat limit must be greater than zero.");
        }

        if (request.Seats.Length <= 0)
        {
            _logger.LogWarning(
                "Show creation rejected because no seats were provided.");
            throw new ArgumentException(
                "Seat count must be greater than zero.");
        }

        var showId = Guid.CreateVersion7();

        var createdSeats = await _repository.CreateShowAsync(
            showId,
            request.Name,
            request.PricePaise,
            request.PerUserSeatLimit,
            request.Seats);

        if (createdSeats.Count == 0)
        {
            _logger.LogError(
                "Show creation for {ShowId} returned no seats.",
                showId);
        }
        else
        {
            _logger.LogInformation(
                "Created show {ShowId} with {SeatCount} seats and per-user limit {SeatLimit}.",
                showId,
                createdSeats.Count,
                request.PerUserSeatLimit);
        }

        return new ShowResponse(
            showId,
            createdSeats
        );
    }

    public async Task<GetShowResponse?> GetShowAsync(Guid showId)
    {
        var showData = await _repository.GetShowAsync(showId);
        if (showData == null)
        {
            _logger.LogInformation(
                "Show {ShowId} was not found.",
                showId);
            return null;
        }

        var seats = showData.Select(row => new SeatResponse(
            row.SeatNumber,
            row.Status
        )).ToList();

        var firstRow = showData.First();
        _logger.LogDebug(
            "Retrieved show {ShowId} with {SeatCount} seats.",
            showId,
            seats.Count);
        return new GetShowResponse(showId, seats, firstRow.Name, firstRow.PricePaise, firstRow.PerUserSeatLimit);
    }
}