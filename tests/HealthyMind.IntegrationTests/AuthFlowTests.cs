using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Auth;
using HealthyMind.Application.Features.Users;

namespace HealthyMind.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthFlowTests(ApiFactory factory)
{
    [Fact]
    public async Task Refresh_rotates_the_token_and_a_reused_token_closes_the_device()
    {
        var (client, auth) = await factory.RegisterAsync("phone-a");

        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken, "phone-a"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var next = (await rotated.Content.ReadFromJsonAsync<AuthResponse>(ApiFactory.Json))!;
        Assert.NotEqual(auth.RefreshToken, next.RefreshToken);

        // Alguien reusa el token viejo: se cierran todas las sesiones del dispositivo, incluida la nueva.
        var reused = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken, "phone-a"), ApiFactory.Json);
        var afterTheft = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(next.RefreshToken, "phone-a"), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterTheft.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_only_works_on_the_device_it_was_issued_for()
    {
        var (client, auth) = await factory.RegisterAsync("phone-a");

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken, "otro-telefono"), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Suspended_user_is_cut_off_immediately()
    {
        var (client, auth) = await factory.RegisterAsync();
        var admin = await LoginAdminAsync();

        var suspend = await admin.PatchAsync($"/api/admin/users/{auth.User.Id}/suspend", null);
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        // El access token aún no vence, pero ya no sirve.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/habits")).StatusCode);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(auth.User.Username, "Clave1234", "device-1", null),
            ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Regular_users_cannot_reach_admin_endpoints()
    {
        var (client, _) = await factory.RegisterAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task On_demand_recommendations_are_off_unless_the_server_allows_them()
    {
        var (client, _) = await factory.RegisterAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/recommendations/now", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_search_users()
    {
        var (_, auth) = await factory.RegisterAsync();
        var admin = await LoginAdminAsync();

        var page = await admin.GetFromJsonAsync<PagedResult<UserProfileResponse>>(
            $"/api/admin/users?search={auth.User.Username}",
            ApiFactory.Json);

        Assert.Equal(auth.User.Id, Assert.Single(page!.Items).Id);
    }

    private async Task<HttpClient> LoginAdminAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Admin12345", "pc", null), ApiFactory.Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(ApiFactory.Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
