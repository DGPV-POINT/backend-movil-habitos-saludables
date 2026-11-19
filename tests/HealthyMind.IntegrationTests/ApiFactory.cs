using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthyMind.Application.Features.Auth;
using HealthyMind.Application.Ports.Out;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // Firebase falso: guarda lo que se habría enviado.
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPushSender>();
            services.AddSingleton<IPushSender>(Push);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Mail);
        });
    }

    public RecordingPushSender Push { get; } = new();

    public RecordingEmailSender Mail { get; } = new();

    public async Task<(HttpClient Client, AuthResponse Auth)> RegisterAsync(string deviceId = "device-1")
    {
        var username = "u" + Guid.NewGuid().ToString("N")[..10];
        var email = $"{username}@test.com";
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(username, email, "Ana", "Pérez", "Clave1234", deviceId, "Pixel", null),
            Json);
        response.EnsureSuccessStatusCode();

        // La sesión se abre al confirmar el correo con el código que "llegó".
        var verified = await client.PostAsJsonAsync(
            "/api/auth/email/verify",
            new VerifyEmailRequest(email, Mail.LastCodeFor(email), deviceId, "Pixel"),
            Json);
        verified.EnsureSuccessStatusCode();

        var auth = (await verified.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }
}

/// <summary>Correo de mentira: guarda los mensajes para leer el código enviado.</summary>
public sealed partial class RecordingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public string LastCodeFor(string email) =>
        SixDigits().Match(Sent.Last(message => message.To == email).TextBody).Value;

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
