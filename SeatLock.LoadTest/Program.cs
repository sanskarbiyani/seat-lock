using System.Net.Http.Headers;
using System.Net.Http.Json;

var client = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5224")
};

var showId = Guid.Parse("01a102c6-8ee6-7fa8-9246-05d89e7c00ed");

var userAToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMWExMDIwOC01NTkwLTdkMjItOTkzZC02NDhlZGJlNjY5ZjciLCJqdGkiOiI3NjI2NjljYi0xZWY0LTRiYzgtODYzOS04NDg1ZDcxNDFjNjQiLCJuYmYiOjE3OTEwNDc5NTAsImV4cCI6MTc5MTA1MTU1MCwiaXNzIjoiU2VhdExvY2siLCJhdWQiOiJTZWF0TG9jay5BcGkifQ.8c7HqAGv9XouG2IOC3fi-gwZbI_Gv0qpedewreSWYk0";
var userBToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMWExMDJjMy1jZTQ2LTdiMmMtYjhmOC1jOTgzNzMyY2I5MDgiLCJqdGkiOiJlM2Q5YThhZC05MDlhLTQxNjctODA2Ni00NzZjYzI0NzVjNDUiLCJuYmYiOjE3OTEwNDc5ODMsImV4cCI6MTc5MTA1MTU4MywiaXNzIjoiU2VhdExvY2siLCJhdWQiOiJTZWF0TG9jay5BcGkifQ.1DwemozwZCTFPueIiAOS_wAdIlULDD-2Xs4DGuyYwHk";

var tasks = new[]
{
    ReserveAsync(
        client,
        showId,
        userAToken,
        "user-a-test-001"),

    ReserveAsync(
        client,
        showId,
        userBToken,
        "user-b-test-001")
};

var responses = await Task.WhenAll(tasks);

foreach (var response in responses)
{
    Console.WriteLine(
        $"Status: {(int)response.StatusCode} {response.StatusCode}");

    Console.WriteLine(
        await response.Content.ReadAsStringAsync());

    Console.WriteLine();
}

static async Task<HttpResponseMessage> ReserveAsync(
    HttpClient client,
    Guid showId,
    string token,
    string idempotencyKey)
{
    using var request = new HttpRequestMessage(
        HttpMethod.Post,
        $"api/shows/{showId}/reserve");

    Console.WriteLine($"Request URL: {client.BaseAddress}{request.RequestUri}");

    request.Headers.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    request.Content = JsonContent.Create(new
    {
        seats = new[] { "A2" },
        idempotent_key = idempotencyKey
    });

    return await client.SendAsync(request);
}