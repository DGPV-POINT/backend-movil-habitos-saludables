using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HealthyMind.Application.Features.Auth;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Features.Notifications;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Habits;

namespace HealthyMind.IntegrationTests;

/// <summary>Firebase de mentira: guarda cada envío y puede marcar tokens como inválidos.</summary>
public sealed class RecordingPushSender : IPushSender
{
    public ConcurrentQueue<(string Token, PushMessage Message)> Sent { get; } = new();

    public ConcurrentQueue<(string Topic, PushMessage Message)> Topics { get; } = new();

    public ConcurrentDictionary<string, bool> Invalid { get; } = new();

    public bool IsEnabled => true;

    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken)
    {
        foreach (var token in tokens)
        {
            Sent.Enqueue((token, message));
        }

        return Task.FromResult<IReadOnlyList<string>>(tokens.Where(Invalid.ContainsKey).ToList());
    }

    public Task SendToTopicAsync(string topic, PushMessage message, CancellationToken cancellationToken)
    {
        Topics.Enqueue((topic, message));
        return Task.CompletedTask;
    }

    public int SentTo(string token) => Sent.Count(item => item.Token == token);
}

[Collection(ApiCollection.Name)]
public sealed class PushFlowTests(ApiFactory factory)
{
    [Fact]
    public async Task New_recommendation_reaches_the_phone_while_the_session_is_active()
    {
        var token = "tok-" + Guid.NewGuid().ToString("N");
        var (client, auth) = await factory.RegisterAsync("phone-1");
        await CreateHabitAsync(client);
        await RegisterTokenAsync(client, "phone-1", token);
        var admin = await AdminAsync();

        // Sin clave de Groq en las pruebas: sale el plan B por reglas, y el aviso igual se envía.
        var generated = await GenerateAsync(admin, auth.User.Id);
        Assert.Equal(RuleBasedRecommendation.ModelName, generated.ModelName);
        var sent = Assert.Single(factory.Push.Sent, item => item.Token == token);
        Assert.Equal(PushContract.RecommendationType, sent.Message.Data["type"]);
        Assert.Equal(generated.Title, sent.Message.Data["title"]);

        // Cerrar sesión corta los avisos a ese teléfono.
        (await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(auth.RefreshToken), ApiFactory.Json)).EnsureSuccessStatusCode();
        await GenerateAsync(admin, auth.User.Id);
        Assert.Equal(1, factory.Push.SentTo(token));
    }

    [Fact]
    public async Task Turning_off_ai_notifications_and_invalid_tokens_stop_the_pushes()
    {
        var token = "tok-" + Guid.NewGuid().ToString("N");
        var (client, auth) = await factory.RegisterAsync("phone-2");
        await CreateHabitAsync(client);
        var admin = await AdminAsync();

        await RegisterTokenAsync(client, "phone-2", token, recommendationsEnabled: false);
        await GenerateAsync(admin, auth.User.Id);
        Assert.Equal(0, factory.Push.SentTo(token));

        // Firebase dice que el token ya no existe (desinstalaron la app): se borra y no se vuelve a usar.
        await RegisterTokenAsync(client, "phone-2", token);
        factory.Push.Invalid[token] = true;
        await GenerateAsync(admin, auth.User.Id);
        await GenerateAsync(admin, auth.User.Id);
        Assert.Equal(1, factory.Push.SentTo(token));
    }

    [Fact]
    public async Task Feedback_and_seen_mark_are_saved()
    {
        var (client, auth) = await factory.RegisterAsync("phone-3");
        await CreateHabitAsync(client);
        var generated = await GenerateAsync(await AdminAsync(), auth.User.Id);

        var feedback = await client.PutAsJsonAsync(
            $"/api/recommendations/{generated.Id}/feedback", new RecommendationFeedbackRequest(true), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.NoContent, feedback.StatusCode);
        var latest = await client.GetFromJsonAsync<RecommendationResponse>("/api/recommendations/latest", ApiFactory.Json);
        Assert.True(latest!.WasHelpful);

        var seenAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        (await client.PutAsJsonAsync("/api/recommendations/seen", new RecommendationsSeenRequest(seenAt), ApiFactory.Json)).EnsureSuccessStatusCode();
        var pull = await client.GetFromJsonAsync<Application.Features.Sync.SyncPullResponse>("/api/sync/pull", ApiFactory.Json);
        Assert.NotNull(pull!.RecommendationsSeenAt);
    }

    [Fact]
    public async Task Only_admins_can_announce()
    {
        var (client, _) = await factory.RegisterAsync("phone-4");
        var request = new AnnouncementRequest("Mantenimiento", "Esta noche a las 23:00.");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/announcements", request, ApiFactory.Json)).StatusCode);

        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsJsonAsync("/api/admin/announcements", request, ApiFactory.Json)).StatusCode);
        Assert.Contains(factory.Push.Topics, item => item.Topic == PushContract.AnnouncementsTopic && item.Message.Data["title"] == "Mantenimiento");
    }

    private static async Task CreateHabitAsync(HttpClient client) =>
        (await client.PostAsJsonAsync(
            "/api/habits",
            new HabitRequest(null, "Leer " + Guid.NewGuid().ToString("N")[..4], HabitCategory.Lectura, 1, "vez", null),
            ApiFactory.Json)).EnsureSuccessStatusCode();

    private static async Task RegisterTokenAsync(HttpClient client, string deviceId, string token, bool recommendationsEnabled = true) =>
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync("/api/push/devices", new PushTokenRequest(deviceId, token, recommendationsEnabled), ApiFactory.Json)).StatusCode);

    private static async Task<RecommendationResponse> GenerateAsync(HttpClient admin, Guid userId)
    {
        var response = await admin.PostAsync($"/api/admin/recommendations/users/{userId}", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecommendationResponse>(ApiFactory.Json))!;
    }

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("admin", "Admin12345", "admin-tests-" + Guid.NewGuid().ToString("N")[..6], "Pruebas"), ApiFactory.Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(ApiFactory.Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
