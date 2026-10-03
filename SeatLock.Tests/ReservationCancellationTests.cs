using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SeatLock.Tests;

public sealed class ReservationCancellationTests
{
    private const string UserA = "01a10208-5590-7d22-993d-648edbe669f7";
    private const string UserB = "01a102c3-ce46-7b2c-b8f8-c983732cb908";

    private static readonly Uri ApiBaseAddress = new("http://localhost:5224");

    private static readonly IConfiguration Configuration =
        new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

    private static string ConnectionString =>
        Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException(
            "Connection string 'Postgres' is not configured.");

    [Fact]
    public async Task Cancelling_valid_reservation_returns_200_and_updates_reservation_and_seat()
    {
        using var client = CreateClient();
        var showId = await CreateShowAsync(client, ["A1"]);
        var token = await GetTokenAsync(client, UserA);
        var reservationId = await CreateReservationAsync(
            client, showId, token, "A1");

        using var response = await CancelAsync(client, token, reservationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("cancelled", await GetReservationStatusAsync(reservationId));
        Assert.Equal("available", await GetSeatStatusAsync(showId, "A1"));
    }

    [Fact]
    public async Task Cancelling_same_reservation_twice_returns_409()
    {
        using var client = CreateClient();
        var showId = await CreateShowAsync(client, ["A1"]);
        var token = await GetTokenAsync(client, UserA);
        var reservationId = await CreateReservationAsync(
            client, showId, token, "A1");

        using var firstResponse = await CancelAsync(client, token, reservationId);
        using var secondResponse = await CancelAsync(client, token, reservationId);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("cancelled", await GetReservationStatusAsync(reservationId));
        Assert.Equal("available", await GetSeatStatusAsync(showId, "A1"));
    }

    [Fact]
    public async Task Concurrent_cancellation_of_same_reservation_by_same_user_returns_one_200_and_forty_nine_409()
    {
        using var client = CreateClient();
        var showId = await CreateShowAsync(client, ["A1"]);
        var token = await GetTokenAsync(client, UserA);
        var reservationId = await CreateReservationAsync(
            client, showId, token, "A1");

        var cancellationTasks = Enumerable.Range(0, 50)
            .Select(_ => CancelAsync(client, token, reservationId));
        var responses = await Task.WhenAll(cancellationTasks);

        try
        {
            var statusCounts = responses
                .GroupBy(response => response.StatusCode)
                .OrderBy(group => group.Key)
                .ToArray();

            Console.WriteLine(
                $"Test: {nameof(Concurrent_cancellation_of_same_reservation_by_same_user_returns_one_200_and_forty_nine_409)}");
            foreach (var group in statusCounts)
            {
                Console.WriteLine(
                    $"StatusCode: {(int)group.Key} {group.Key}, Count: {group.Count()}");
            }

            Assert.Equal(1, responses.Count(
                response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(49, responses.Count(
                response => response.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(50, responses.Length);
            Assert.Equal("cancelled", await GetReservationStatusAsync(reservationId));
            Assert.Equal("available", await GetSeatStatusAsync(showId, "A1"));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task Different_user_cannot_cancel_reservation()
    {
        using var client = CreateClient();
        var showId = await CreateShowAsync(client, ["A1"]);
        var userAToken = await GetTokenAsync(client, UserA);
        var userBToken = await GetTokenAsync(client, UserB);
        var reservationId = await CreateReservationAsync(
            client, showId, userAToken, "A1");

        using var response = await CancelAsync(client, userBToken, reservationId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("confirmed", await GetReservationStatusAsync(reservationId));
        Assert.Equal("confirmed", await GetSeatStatusAsync(showId, "A1"));
    }

    [Fact]
    public async Task Cancelling_nonexistent_reservation_returns_404()
    {
        using var client = CreateClient();
        var token = await GetTokenAsync(client, UserA);

        using var response = await CancelAsync(client, token, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_user_can_reserve_seats_after_cancellation()
    {
        using var client = CreateClient();
        var showId = await CreateShowAsync(client, ["A1"]);
        var userAToken = await GetTokenAsync(client, UserA);
        var userBToken = await GetTokenAsync(client, UserB);
        var cancelledReservationId = await CreateReservationAsync(
            client, showId, userAToken, "A1");

        using var cancelResponse = await CancelAsync(
            client, userAToken, cancelledReservationId);
        using var reserveResponse = await ReserveAsync(
            client, showId, userBToken, "A1");

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, reserveResponse.StatusCode);

        var newReservation = await reserveResponse.Content
            .ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(newReservation);
        Assert.Equal(
            "cancelled",
            await GetReservationStatusAsync(cancelledReservationId));
        Assert.Equal(
            "confirmed",
            await GetReservationStatusAsync(newReservation.ReservationId));
        Assert.Equal("confirmed", await GetSeatStatusAsync(showId, "A1"));
        Assert.Equal(1, await CountConfirmedReservationsForSeatAsync(showId, "A1"));
    }

    [Fact]
    public async Task Concurrent_cancel_and_reserve_on_same_seat_leave_a_consistent_state()
    {
        using var client = CreateClient();
        var showId = await CreateShowAsync(client, ["A1"]);
        var userAToken = await GetTokenAsync(client, UserA);
        var userBToken = await GetTokenAsync(client, UserB);
        var reservationId = await CreateReservationAsync(
            client, showId, userAToken, "A1");

        var cancelTask = CancelAsync(client, userAToken, reservationId);
        var reserveTask = ReserveAsync(client, showId, userBToken, "A1");
        using var cancelResponse = await cancelTask;
        using var reserveResponse = await reserveTask;

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.Contains(
            reserveResponse.StatusCode,
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        Assert.NotEqual(HttpStatusCode.InternalServerError, cancelResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, reserveResponse.StatusCode);

        Assert.Equal("cancelled", await GetReservationStatusAsync(reservationId));

        var confirmedReservations = await CountConfirmedReservationsForSeatAsync(
            showId, "A1");
        Assert.InRange(confirmedReservations, 0, 1);

        if (reserveResponse.StatusCode == HttpStatusCode.Created)
        {
            var newReservation = await reserveResponse.Content
                .ReadFromJsonAsync<ReservationResponse>();
            Assert.NotNull(newReservation);
            Assert.Equal(
                "confirmed",
                await GetReservationStatusAsync(newReservation.ReservationId));
            Assert.Equal(1, confirmedReservations);
            Assert.Equal("confirmed", await GetSeatStatusAsync(showId, "A1"));
        }
        else
        {
            Assert.Equal(0, confirmedReservations);
            Assert.Equal("available", await GetSeatStatusAsync(showId, "A1"));
        }
    }

    private static HttpClient CreateClient() => new()
    {
        BaseAddress = ApiBaseAddress,
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static async Task<Guid> CreateShowAsync(
        HttpClient client,
        string[] seats)
    {
        using var response = await client.PostAsJsonAsync(
            "api/shows",
            new
            {
                name = $"Cancellation Test Show {Guid.NewGuid():N}",
                seats,
                price_paise = 1000
            });

        response.EnsureSuccessStatusCode();
        var show = await response.Content.ReadFromJsonAsync<CreateShowResponse>();
        return show?.ShowId
            ?? throw new InvalidOperationException(
                "The create-show response did not include a show_id.");
    }

    private static async Task<string> GetTokenAsync(
        HttpClient client,
        string userId)
    {
        using var response = await client.PostAsJsonAsync(
            "api/auth/token",
            new { user_id = Guid.Parse(userId) });

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return token?.AccessToken
            ?? throw new InvalidOperationException(
                "The token response did not include an access token.");
    }

    private static async Task<Guid> CreateReservationAsync(
        HttpClient client,
        Guid showId,
        string token,
        string seat)
    {
        using var response = await ReserveAsync(client, showId, token, seat);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException(
                $"Expected reservation creation to return 201, got {(int)response.StatusCode}.");
        }

        var reservation = await response.Content
            .ReadFromJsonAsync<ReservationResponse>();
        return reservation?.ReservationId
            ?? throw new InvalidOperationException(
                "The reservation response did not include a reservation_id.");
    }

    private static async Task<HttpResponseMessage> ReserveAsync(
        HttpClient client,
        Guid showId,
        string token,
        string seat)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/shows/{showId}/reserve");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            seats = new[] { seat },
            idempotent_key = $"cancel-test-{Guid.NewGuid():N}"
        });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> CancelAsync(
        HttpClient client,
        string token,
        Guid reservationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/reservations/{reservationId}/cancel");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request);
    }

    private static async Task<string?> GetReservationStatusAsync(
        Guid reservationId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT status FROM reservations WHERE reservation_id = @ReservationId;",
            connection);
        command.Parameters.AddWithValue("ReservationId", reservationId);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task<string?> GetSeatStatusAsync(
        Guid showId,
        string seatNumber)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT status
            FROM seats
            WHERE event_id = @ShowId AND seat_number = @SeatNumber;
            """,
            connection);
        command.Parameters.AddWithValue("ShowId", showId);
        command.Parameters.AddWithValue("SeatNumber", seatNumber);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task<long> CountConfirmedReservationsForSeatAsync(
        Guid showId,
        string seatNumber)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT COUNT(*)
            FROM reservations r
            JOIN reservation_seats rs
                ON rs.reservation_id = r.reservation_id
            JOIN seats s
                ON s.seat_id = rs.seat_id
            WHERE r.event_id = @ShowId
              AND s.seat_number = @SeatNumber
              AND r.status = 'confirmed';
            """,
            connection);
        command.Parameters.AddWithValue("ShowId", showId);
        command.Parameters.AddWithValue("SeatNumber", seatNumber);
        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException(
                "The confirmed reservation count query returned null."));
    }

    private sealed record CreateShowResponse(
        [property: JsonPropertyName("show_id")] Guid ShowId);

    private sealed record TokenResponse(
        [property: JsonPropertyName("accessToken")] string AccessToken);

    private sealed record ReservationResponse(
        [property: JsonPropertyName("reservation_id")] Guid ReservationId);
}
