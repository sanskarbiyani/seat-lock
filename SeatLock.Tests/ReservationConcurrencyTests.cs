using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using SeatLock.Api.DTOs.Shows;

namespace SeatLock.Tests;

public class ReservationConcurrencyTests
{

    private static readonly string[] UserIds =
    [
        "01a10208-5590-7d22-993d-648edbe669f7",
        "01a102c3-ce46-7b2c-b8f8-c983732cb908",
        "01a102f3-c591-75bb-a878-2c7536513c60",
        "01a102f3-c6e7-7819-9630-5567195caa2d",
        "01a102f3-c7a2-708b-9c42-0f524d44ea1c",
        "01a102f3-c846-7858-9d8a-671c5050ff83",
        "01a102f3-c8e3-7af9-b726-7703fffa2bd3",
        "01a102f3-c973-7e2a-a65c-7c326194a84e",
        "01a102f3-ca07-73e2-9516-1d1c122494b8",
        "01a102f3-caa7-776d-89f8-1f8352ac707d",
        "01a102f3-cb46-7ef0-ade3-68dad2469fc2",
        "01a102f3-cbe0-7398-8d31-92dbff8823be",
        "01a102f3-cc73-73b4-9881-6ddf178e8812",
        "01a102f3-cd0f-7d2d-b326-a6300a70909a",
        "01a102f3-cdab-7f28-bd11-2214c672f752",
        "01a102f3-ce43-75a6-bf3e-fbef61ec25fb",
        "01a102f3-cede-7f83-863e-f4f824d67e67",
        "01a102f3-cf7f-7b1c-adaf-a6301e5fe11d",
        "01a102f3-d012-7eee-acb6-fba234ce923c",
        "01a102f3-d0c5-7bd0-9e3d-9b2a8970d751",
        "01a102f3-d2db-7b32-92b6-3a7014e2db59",
        "01a102f3-d395-77d0-9721-1e97014e02e5",
        "01a102f3-d453-7054-a373-05d154e42628",
        "01a102f3-d4e7-748c-9bb8-58a3932a527b",
        "01a102f3-d585-7000-ba19-d97b24276e99",
        "01a102f3-d60e-7b9d-bed8-07e6c3b51fb2",
        "01a102f3-ec33-7598-a300-24e9807849eb",
        "01a102f3-eceb-705d-bbfc-433660871f43",
        "01a102f3-ed78-79ab-a59e-66611c132b70",
        "01a102f3-ee11-7ebc-ad1f-9a0041032500",
        "01a102f3-eea1-7074-b472-cc4483288992",
        "01a102f3-ef45-77cc-a299-9855679a3641",
        "01a102f3-efd7-7c0e-a75e-eb256348e8b3",
        "01a102f3-f05f-74fd-9bcd-5edd87ea5581",
        "01a102f3-f0f6-79f6-bc65-1ca200129127",
        "01a102f3-f17c-79eb-978b-c2765eaec6b4",
        "01a102f3-f206-7313-973e-ebec1705a87f",
        "01a102f3-f2a5-765c-83c5-245c98abe994",
        "01a102f3-f33c-7dcf-a7ac-c5e102f686d8",
        "01a102f3-f3d2-750d-a509-5484688f71f7",
        "01a102f3-f45c-7767-9776-cca32a53b46d",
        "01a102f3-f4fe-7c8a-99b3-b5314a7f97fe",
        "01a102f3-f596-76cd-88d6-475abbd44c58",
        "01a102f3-fbf1-73db-8e25-508ba5c00887",
        "01a102f4-12cd-71a0-a02e-5b7662084daa",
        "01a102f4-1461-7b7a-9615-d269f3905276",
        "01a102f4-15f5-74e1-a5fc-40b43242d832",
        "01a102f4-1732-7862-8b27-9f92488dc315",
        "01a102f4-185d-793a-9edc-ba3c354610ce",
        "01a102f4-2d66-7c38-a07d-8c20247a2d77"
    ];

    private static readonly String[] SeatIds =
    [
        "A1", "A2", "A3", "A4", "A5",
        "B1", "B2", "B3", "B4", "B5",
        "C1", "C2", "C3", "C4", "C5",
        "D1", "D2", "D3", "D4", "D5",
        "E1", "E2", "E3", "E4", "E5"
    ];

    [Fact]
    public async Task Fifty_users_reserving_same_seat_only_one_succeeds()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5224")
        };

        var showId = await CreateShowAsync(
            client,
            "Concurrency Test Show",
            ["A1"]);

        // Get JWTs for all users.
        var tokenTasks = UserIds.Select(
            userId => GetTokenAsync(client, userId));

        var tokens = await Task.WhenAll(tokenTasks);

        // Send all reservation requests concurrently.
        var reservationTasks = tokens.Select(
            token => ReserveSeatAsync(
                client,
                showId,
                token,
                $"burst-test-{Guid.NewGuid()}",
                "A1"));

        var responses = await Task.WhenAll(reservationTasks);

        PrintStatusCodeSummary(
            nameof(Fifty_users_reserving_same_seat_only_one_succeeds),
            responses);

        var successful = responses.Count(
            response => response.StatusCode == HttpStatusCode.Created);

        var conflicts = responses.Count(
            response => response.StatusCode == HttpStatusCode.Conflict);

        successful.Should().Be(1);
        conflicts.Should().Be(49);
    }

    [Fact]
    public async Task Fifty_users_randomly_reserving_five_seats_only_one_reservation_per_seat_succeeds()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5224"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        string[] seats = ["A1", "A2", "A3", "A4", "A5"];
        var showId = await CreateShowAsync(
            client,
            "Random Seat Burst Concurrency Test Show",
            seats);

        var tokenTasks = UserIds.Select(
            userId => GetTokenAsync(client, userId));
        var tokens = await Task.WhenAll(tokenTasks);

        var random = new Random();
        var attempts = tokens
            .Select(token =>
            {
                var seat = seats[random.Next(seats.Length)];
                return new
                {
                    Seat = seat,
                    ResponseTask = ReserveSeatAsync(
                        client,
                        showId,
                        token,
                        $"random-seat-burst-{Guid.NewGuid():N}",
                        seat)
                };
            })
            .ToArray();

        var responses = await Task.WhenAll(
            attempts.Select(attempt => attempt.ResponseTask));

        PrintStatusCodeSummary(
            nameof(Fifty_users_randomly_reserving_five_seats_only_one_reservation_per_seat_succeeds),
            responses);

        var createdCount = responses.Count(
            response => response.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(
            response => response.StatusCode == HttpStatusCode.Conflict);

        createdCount.Should().BeLessThanOrEqualTo(seats.Length);
        conflictCount.Should().Be(UserIds.Length - createdCount);
        responses.Should().OnlyContain(response =>
            response.StatusCode == HttpStatusCode.Created
                || response.StatusCode == HttpStatusCode.Conflict);
        responses.Should().NotContain(response =>
            (int)response.StatusCode >= 500);

        foreach (var seat in seats)
        {
            attempts
                .Select((attempt, index) => new
                {
                    attempt.Seat,
                    StatusCode = responses[index].StatusCode
                })
                .Where(result => result.Seat == seat)
                .Count(result => result.StatusCode == HttpStatusCode.Created)
                .Should()
                .BeLessThanOrEqualTo(1, $"seat {seat} must have at most one successful reservation");
        }

        using var showResponse = await client.GetAsync($"api/shows/{showId}");
        showResponse.EnsureSuccessStatusCode();
        var show = await showResponse.Content.ReadFromJsonAsync<GetShowResponse>();

        show.Should().NotBeNull();
        show!.Seats.Should().HaveCount(seats.Length);
        show.Seats.Select(seat => seat.SeatNumber)
            .Should().BeEquivalentTo(seats);

        foreach (var seat in seats)
        {
            var successfulRequestsForSeat = attempts
                .Select((attempt, index) => new
                {
                    attempt.Seat,
                    StatusCode = responses[index].StatusCode
                })
                .Count(result =>
                    result.Seat == seat
                    && result.StatusCode == HttpStatusCode.Created);
            var seatState = show.Seats.Single(item => item.SeatNumber == seat);

            seatState.Status.Should().Be(
                successfulRequestsForSeat == 1 ? "confirmed" : "available");
        }

        foreach (var response in responses)
            response.Dispose();
    }

    [Fact]
    public async Task One_user_reserving_different_seats()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5224")
        };

        var userId = UserIds.First();
        var token = await GetTokenAsync(client, userId);

        var showId = await CreateShowAsync(
            client,
            "Concurrency Test Show",
            ["A1", "A2", "A3", "A4", "A5"]);

        Console.WriteLine($"Created show with ID: {showId}");

        var reservationTasks = SeatIds.Select(
            seat => ReserveSeatAsync(
                client,
                showId,
                token,
                $"burst-test-{Guid.NewGuid()}",
                seat));

        var responses = await Task.WhenAll(reservationTasks);

        PrintStatusCodeSummary(
            nameof(One_user_reserving_different_seats),
            responses);

        var successful = responses.Count(
            response => response.StatusCode == HttpStatusCode.Created);

        successful.Should().Be(4);
    }

    [Fact]
    public async Task One_user_with_different_seats_same_idempotency_key()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5224")
        };

        var userId = UserIds.First();
        var token = await GetTokenAsync(client, userId);

        var PartialSeatIds = new [] {"A1", "A2", "A3", "A4", "A5"};

        var showId = await CreateShowAsync(
            client,
            "Concurrency Test Show",
            PartialSeatIds);

        Console.WriteLine($"Created show with ID: {showId}");

        var reservationTasks = PartialSeatIds.Select(
            seat => ReserveSeatAsync(
                client,
                showId,
                token,
                $"burst-test-1",
                seat));

        var responses = await Task.WhenAll(reservationTasks);

        PrintStatusCodeSummary(
            nameof(One_user_with_different_seats_same_idempotency_key),
            responses);

        var successful = responses.Count(
            response => response.StatusCode == HttpStatusCode.Created);

        // Only one reservation should succeed, as the same idempotency key is used for all requests.
        successful.Should().Be(1);
    }

    [Fact]
    public async Task Fifty_concurrent_requests_with_same_idempotency_key_and_different_seats()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5224")
        };

        var seatIds = Enumerable.Range(0, 50)
            .Select(_ => $"SEAT-{Guid.NewGuid():N}")
            .ToArray();

        var showId = await CreateShowAsync(
            client,
            "Same Idempotency Key Concurrency Test Show",
            seatIds);

        Console.WriteLine($"Created show with ID: {showId}");

        var token = await GetTokenAsync(client, UserIds.First());
        const string idempotencyKey = "same-key-different-seats";

        var reservationTasks = seatIds.Select(
            seat => ReserveSeatAsync(
                client,
                showId,
                token,
                idempotencyKey,
                seat));

        var responses = await Task.WhenAll(reservationTasks);

        PrintStatusCodeSummary(
            nameof(Fifty_concurrent_requests_with_same_idempotency_key_and_different_seats),
            responses);

        responses.Count(response =>
            response.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(response =>
            response.StatusCode == HttpStatusCode.Conflict).Should().Be(49);
    }

    private static void PrintStatusCodeSummary(
        string testName,
        IEnumerable<HttpResponseMessage> responses)
    {
        var statusCounts = responses
            .GroupBy(response => response.StatusCode)
            .Select(group => new
            {
                StatusCode = group.Key,
                Count = group.Count()
            })
            .OrderBy(item => item.StatusCode);

        Console.WriteLine($"Test: {testName}");

        foreach (var item in statusCounts)
        {
            Console.WriteLine(
                $"StatusCode: {(int)item.StatusCode} {item.StatusCode}, Count: {item.Count}");
        }

        Console.WriteLine();
        Console.WriteLine();
    }

    private static async Task<string> GetTokenAsync(
        HttpClient client,
        string userId)
    {
        var response = await client.PostAsJsonAsync(
            "api/auth/token",
            new
            {
                user_id = Guid.Parse(userId)
            });

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<TokenResponse>();
        return result!.AccessToken;
    }

    private static async Task<HttpResponseMessage> ReserveSeatAsync(
        HttpClient client,
        Guid showId,
        string token,
        string idempotencyKey,
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
            idempotent_key = idempotencyKey
        });

        return await client.SendAsync(request);
    }

    private static async Task<Guid> CreateShowAsync(
        HttpClient client,
        string name,
        IEnumerable<string> seatIds)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/shows");

        request.Content = JsonContent.Create(new
        {
            name,
            seats = seatIds,
            price_paise = 1000,
        });

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<CreateShowResponse>();
        return result!.show_id;
    }

    private sealed record TokenResponse(string AccessToken);

    private sealed record CreateShowResponse(Guid show_id);
}