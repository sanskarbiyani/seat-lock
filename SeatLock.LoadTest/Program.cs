using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;


var baseUrl = args.FirstOrDefault()
    ?? throw new ArgumentException(
        "Usage: dotnet run -- <BASE_URL>");

if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
    throw new ArgumentException("BASE_URL must be a valid absolute URL.");

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var adminEmail = configuration["Admin:Email"]
    ?? throw new InvalidOperationException(
        "Admin email is not configured.");

var adminPassword = configuration["Admin:Password"]
    ?? throw new InvalidOperationException(
        "Admin password is not configured.");

using var client = new HttpClient
{
    BaseAddress = baseUri,
    Timeout = TimeSpan.FromSeconds(30)
};

Console.WriteLine("=== SeatLock Burst Test ===");
Console.WriteLine($"Base URL: {client.BaseAddress}");
Console.WriteLine();

var adminToken = await LoginAsync(client, adminEmail, adminPassword);

Console.WriteLine("Admin authentication: OK");
Console.WriteLine();

const int normalRequestCount = 500;
const int normalConcurrency = 200;

var normalUsers = await CreateUsersAsync(
    client,
    normalRequestCount);

var normalSeats = Enumerable
    .Range(1, normalRequestCount)
    .Select(i => $"A{i}")
    .ToArray();

var normalShowId = await CreateShowAsync(
    client,
    adminToken,
    "Burst Test - Normal",
    normalSeats);

Console.WriteLine($"Normal burst show: {normalShowId}");
Console.WriteLine();

Console.WriteLine("=== NORMAL ON-SALE BURST ===");

var normalResult = await RunBurstAsync(
    client,
    normalShowId,
    normalUsers,
    normalSeats,
    normalConcurrency);

PrintResult(normalResult);

Console.WriteLine();

var normalReconciliation = await GetShowAsync(
    client,
    normalShowId);

PrintReconciliation(
    "Normal burst",
    normalReconciliation,
    expectedConfirmed: normalRequestCount);

Console.WriteLine();

const int hotSeatRequestCount = 500;
const int hotSeatConcurrency = 500;

var hotSeatUsers = await CreateUsersAsync(
    client,
    hotSeatRequestCount);

var hotSeatShowId = await CreateShowAsync(
    client,
    adminToken,
    "Burst Test - Hot Seat",
    ["A1"]);

Console.WriteLine($"Hot-seat show: {hotSeatShowId}");
Console.WriteLine();

Console.WriteLine("=== HOT-SEAT STORM ===");
Console.WriteLine($"Seat: A1");
Console.WriteLine($"Requests: {hotSeatRequestCount}");
Console.WriteLine($"Concurrency: {hotSeatConcurrency}");
Console.WriteLine();

var hotSeatResult = await RunBurstAsync(
    client,
    hotSeatShowId,
    hotSeatUsers,
    Enumerable.Repeat("A1", hotSeatRequestCount).ToArray(),
    hotSeatConcurrency);

PrintResult(hotSeatResult);

Console.WriteLine();

var hotSeatReconciliation = await GetShowAsync(
    client,
    hotSeatShowId);

PrintReconciliation(
    "Hot-seat storm",
    hotSeatReconciliation,
    expectedConfirmed: 1);

Console.WriteLine();

var passed =
    normalResult.FiveHundreds == 0
    && normalResult.Confirmed == normalRequestCount
    && normalReconciliation.Confirmed == normalRequestCount
    && normalReconciliation.Available == 0
    && hotSeatResult.FiveHundreds == 0
    && hotSeatResult.Confirmed == 1
    && hotSeatReconciliation.Confirmed == 1
    && hotSeatReconciliation.Available == 0;

Console.WriteLine("=================================");
Console.WriteLine(passed
    ? "RESULT: PASS"
    : "RESULT: FAIL");
Console.WriteLine("=================================");

return passed ? 0 : 1;


static async Task<string> LoginAsync(
    HttpClient client,
    string email,
    string password)
{
    using var response = await client.PostAsJsonAsync(
        "api/auth/login",
        new
        {
            email,
            password
        });

    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Admin login failed: {(int)response.StatusCode} {body}");
    }

    var token = JsonSerializer.Deserialize<TokenResponse>(
        body);

    return token?.AccessToken
        ?? throw new InvalidOperationException(
            "Login response did not contain accessToken.");
}


static async Task<List<TestUser>> CreateUsersAsync(
    HttpClient client,
    int count)
{
    Console.WriteLine($"Creating {count} test users...");

    var tasks = Enumerable
        .Range(1, count)
        .Select(_ => CreateUserAsync(client));

    var users = await Task.WhenAll(tasks);

    Console.WriteLine($"Created {users.Length} test users.");

    return users.ToList();
}


static async Task<TestUser> CreateUserAsync(
    HttpClient client)
{
    var suffix = Guid.NewGuid().ToString("N");

    var email = $"burst-{suffix}@example.com";
    var password = $"Burst-{suffix}!";

    using var response = await client.PostAsJsonAsync(
        "api/auth/register",
        new
        {
            email,
            password
        });

    var body = await response.Content.ReadAsStringAsync();

    if (response.StatusCode != HttpStatusCode.Created)
    {
        throw new InvalidOperationException(
            $"User registration failed: {(int)response.StatusCode} {body}");
    }

    var token = JsonSerializer.Deserialize<TokenResponse>(body);

    return new TestUser(
        token?.AccessToken
            ?? throw new InvalidOperationException(
                "Registration response did not contain accessToken."));
}


static async Task<Guid> CreateShowAsync(
    HttpClient client,
    string adminToken,
    string name,
    string[] seats)
{
    using var request = new HttpRequestMessage(
        HttpMethod.Post,
        "api/shows");

    request.Headers.Authorization =
        new AuthenticationHeaderValue("Bearer", adminToken);

    request.Content = JsonContent.Create(new
    {
        name,
        seats,
        price_paise = 1000
    });

    using var response = await client.SendAsync(request);

    var body = await response.Content.ReadAsStringAsync();

    if (response.StatusCode != HttpStatusCode.Created)
    {
        throw new InvalidOperationException(
            $"Create show failed: {(int)response.StatusCode} {body}");
    }

    var show = JsonSerializer.Deserialize<CreateShowResponse>(body);

    return show?.ShowId
        ?? throw new InvalidOperationException(
            "Create show response did not contain show_id.");
}


static async Task<BurstResult> RunBurstAsync(
    HttpClient client,
    Guid showId,
    IReadOnlyList<TestUser> users,
    IReadOnlyList<string> seats,
    int maxConcurrency)
{
    if (users.Count != seats.Count)
        throw new ArgumentException(
            "Users and seats must contain the same number of entries.");

    using var semaphore = new SemaphoreSlim(maxConcurrency);

    var tasks = Enumerable
        .Range(0, users.Count)
        .Select(async index =>
        {
            await semaphore.WaitAsync();

            try
            {
                return await ReserveAsync(
                    client,
                    showId,
                    users[index].AccessToken,
                    seats[index],
                    index);
            }
            finally
            {
                semaphore.Release();
            }
        });

    var results = await Task.WhenAll(tasks);

    return BurstResult.From(results);
}


static async Task<ReservationAttempt> ReserveAsync(
    HttpClient client,
    Guid showId,
    string token,
    string seat,
    int requestNumber)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/shows/{showId}/reserve");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        request.Content = JsonContent.Create(new
        {
            seats = new[] { seat },
            idempotent_key =
                $"burst-{showId:N}-{requestNumber}-{Guid.NewGuid():N}"
        });

        using var response = await client.SendAsync(request);

        stopwatch.Stop();

        var body = await response.Content.ReadAsStringAsync();

        return new ReservationAttempt(
            response.StatusCode,
            body,
            stopwatch.Elapsed.TotalMilliseconds);
    }
    catch (Exception ex)
    {
        stopwatch.Stop();

        return new ReservationAttempt(
            null,
            ex.Message,
            stopwatch.Elapsed.TotalMilliseconds);
    }
}


static async Task<ShowReconciliation> GetShowAsync(
    HttpClient client,
    Guid showId)
{
    using var response =
        await client.GetAsync($"api/shows/{showId}");

    var body = await response.Content.ReadAsStringAsync();

    response.EnsureSuccessStatusCode();
    // Console.WriteLine($"GET show {showId}: {body}");

    var show = JsonSerializer.Deserialize<ShowResponse>(body)
        ?? throw new InvalidOperationException(
            "Could not deserialize GET show response.");

    var available = show.Seats.Count(
        s => string.Equals(
            s.Status,
            "available",
            StringComparison.OrdinalIgnoreCase));

    var confirmed = show.Seats.Count(
        s => string.Equals(
            s.Status,
            "confirmed",
            StringComparison.OrdinalIgnoreCase));

    return new ShowReconciliation(
        available,
        confirmed);
}


static int? FindInt(
    JsonElement element,
    string propertyName)
{
    if (element.ValueKind != JsonValueKind.Object)
        return null;

    foreach (var property in element.EnumerateObject())
    {
        if (string.Equals(
                property.Name,
                propertyName,
                StringComparison.OrdinalIgnoreCase))
        {
            if (property.Value.TryGetInt32(out var value))
                return value;
        }
    }

    return null;
}


static void PrintResult(BurstResult result)
{
    Console.WriteLine($"Requests:      {result.Total}");
    Console.WriteLine($"Confirmed:     {result.Confirmed}");

    foreach (var conflict in result.Conflicts
                 .OrderBy(x => x.Key))
    {
        Console.WriteLine(
            $"409 {conflict.Key}: {conflict.Value}");
    }

    Console.WriteLine($"Other 4xx:     {result.Other4xx}");
    Console.WriteLine($"5xx:           {result.FiveHundreds}");
    Console.WriteLine($"Transport err: {result.TransportErrors}");

    Console.WriteLine(
        $"p50 latency:   {Percentile(result.Latencies, 0.50):F1} ms");

    Console.WriteLine(
        $"p95 latency:   {Percentile(result.Latencies, 0.95):F1} ms");

    Console.WriteLine(
        $"p99 latency:   {Percentile(result.Latencies, 0.99):F1} ms");

    Console.WriteLine(
        $"Max latency:   {result.Latencies.Max():F1} ms");
}


static void PrintReconciliation(
    string name,
    ShowReconciliation reconciliation,
    int expectedConfirmed)
{
    var pass =
        reconciliation.Confirmed == expectedConfirmed
        && reconciliation.Available +
            reconciliation.Confirmed > 0;

    Console.WriteLine($"=== {name} RECONCILIATION ===");
    Console.WriteLine($"Available:          {reconciliation.Available}");
    Console.WriteLine($"Confirmed:          {reconciliation.Confirmed}");
    Console.WriteLine($"Expected confirmed: {expectedConfirmed}");
    Console.WriteLine($"Result:             {(pass ? "PASS" : "FAIL")}");
}


static double Percentile(
    IReadOnlyList<double> values,
    double percentile)
{
    var sorted = values
        .OrderBy(x => x)
        .ToArray();

    if (sorted.Length == 0)
        return 0;

    var position =
        (sorted.Length - 1) * percentile;

    var lower = (int)Math.Floor(position);
    var upper = (int)Math.Ceiling(position);

    if (lower == upper)
        return sorted[lower];

    var fraction = position - lower;

    return sorted[lower] +
           (sorted[upper] - sorted[lower]) * fraction;
}


sealed record TestUser(string AccessToken);

sealed record TokenResponse(
    [property: JsonPropertyName("accessToken")]
    string AccessToken);

sealed record CreateShowResponse(
    [property: JsonPropertyName("show_id")]
    Guid ShowId);

sealed record ReservationAttempt(
    HttpStatusCode? StatusCode,
    string Body,
    double LatencyMs);

sealed record ShowReconciliation(
    int Available,
    int Confirmed);

record ShowSeat(
    [property: JsonPropertyName("seat_number")] string SeatNumber,
    [property: JsonPropertyName("status")] string Status);

record ShowResponse(
    [property: JsonPropertyName("show_id")] Guid ShowId,
    [property: JsonPropertyName("seats")] ShowSeat[] Seats,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("price_paise")] long PricePaise,
    [property: JsonPropertyName("per_user_seat_limit")] int PerUserSeatLimit);

sealed class BurstResult
{
    public int Total { get; private set; }
    public int Confirmed { get; private set; }
    public int Other4xx { get; private set; }
    public int FiveHundreds { get; private set; }
    public int TransportErrors { get; private set; }

    public Dictionary<string, int> Conflicts { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<double> Latencies { get; } = [];

    public static BurstResult From(
        IEnumerable<ReservationAttempt> attempts)
    {
        var result = new BurstResult();

        foreach (var attempt in attempts)
        {
            result.Total++;
            result.Latencies.Add(attempt.LatencyMs);

            if (attempt.StatusCode is null)
            {
                result.TransportErrors++;
                continue;
            }

            var status = (int)attempt.StatusCode.Value;

            if (status == 201)
            {
                result.Confirmed++;
                continue;
            }

            if (status == 409)
            {
                var reason = ExtractConflictReason(attempt.Body);

                if (!result.Conflicts.TryAdd(reason, 1))
                    result.Conflicts[reason]++;
                
                continue;
            }

            if (status >= 400 && status < 500)
            {
                result.Other4xx++;
                continue;
            }

            if (status >= 500)
            {
                result.FiveHundreds++;
            }
        }

        return result;
    }

    private static string ExtractConflictReason(
        string body)
    {
        try
        {
            using var document =
                JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty(
                    "reason",
                    out var reason))
            {
                return reason.GetString() ?? "unknown";
            }

            if (document.RootElement.TryGetProperty(
                    "title",
                    out var title))
            {
                return title.GetString() ?? "unknown";
            }
        }
        catch
        {
            // Keep burst output useful even if the
            // error response isn't valid JSON.
        }

        return "unknown";
    }
}