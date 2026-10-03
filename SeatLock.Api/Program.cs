using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using SeatLock.Api.Infrastructure.Database;
using SeatLock.Api.Interfaces;
using SeatLock.Api.Interfaces.Authentication;
using SeatLock.Api.Interfaces.Repositories;
using SeatLock.Api.Interfaces.Services;
using SeatLock.Api.Repositories;
using SeatLock.Api.Services;

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("JWT signing key is not configured.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must be at least 32 bytes long.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<ITokenService>(
    new TokenService(jwtKey, jwtIssuer, jwtAudience));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("PostgreSQL", tags: ["ready"]);
builder.Services.AddScoped<IShowRepository, ShowRepository>();
builder.Services.AddScoped<IShowService, ShowService>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IReservationService, ReservationService>();

builder.Services.AddControllers();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy =
        JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live");

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
