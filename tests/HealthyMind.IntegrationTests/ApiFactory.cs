using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthyMind.Application.Features.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace HealthyMind.IntegrationTests;

/// <summary>Levanta la API real contra un PostgreSQL desechable en Docker.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:HealthyMind", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Jwt:SigningKey", "clave-de-pruebas-de-integracion-0123456789");
        builder.UseSetting("RecommendationJob:Enabled", "false");
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", "1000");
        builder.UseSetting("BootstrapAdmin:Username", "admin");
        builder.UseSetting("BootstrapAdmin:Email", "admin@test.local");
        builder.UseSetting("BootstrapAdmin:Password", "Admin12345");
    }

    public async Task<(HttpClient Client, AuthResponse Auth)> RegisterAsync(string deviceId = "device-1")
    {
        var username = "u" + Guid.NewGuid().ToString("N")[..10];
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(username, $"{username}@test.com", "Ana", "Pérez", "Clave1234", deviceId, "Pixel", null),
            Json);
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
