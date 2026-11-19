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

    [Fact]
    public async Task Account_is_unusable_until_the_email_code_is_entered()
    {
        var client = factory.CreateClient();
        var username = "v" + Guid.NewGuid().ToString("N")[..10];
        var email = $"{username}@test.com";
        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(username, email, "Ana", "Pérez", "Clave1234", "d1", null, null),
            ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Clave1234", "d1", null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains(EmailNotVerifiedException.Code, await login.Content.ReadAsStringAsync());

        var wrong = await client.PostAsJsonAsync("/api/auth/email/verify", new VerifyEmailRequest(email, "000000", "d1", null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var ok = await client.PostAsJsonAsync(
            "/api/auth/email/verify",
            new VerifyEmailRequest(email, factory.Mail.LastCodeFor(email), "d1", null),
            ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Forgotten_password_is_replaced_with_the_emailed_code_and_closes_sessions()
    {
        var (_, auth) = await factory.RegisterAsync("phone-r");
        var client = factory.CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/auth/password/forgot", new EmailRequest("nadie@test.com"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode); // no delata qué correos existen

        await client.PostAsJsonAsync("/api/auth/password/forgot", new EmailRequest(auth.User.Email), ApiFactory.Json);
        var reset = await client.PostAsJsonAsync(
            "/api/auth/password/reset",
            new ResetPasswordRequest(auth.User.Email, factory.Mail.LastCodeFor(auth.User.Email), "NuevaClave99"),
            ApiFactory.Json);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var oldSession = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken, "phone-r"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(auth.User.Email, "NuevaClave99", "phone-r", null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Admin_promotes_a_user_and_sees_the_dashboard()
    {
        var (_, auth) = await factory.RegisterAsync();
        var admin = await LoginAdminAsync();

        var promoted = await admin.PatchAsJsonAsync(
            $"/api/admin/users/{auth.User.Id}/role",
            new ChangeRoleRequest(Domain.Users.UserRole.Admin),
            ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);

        var dashboard = await admin.GetFromJsonAsync<AdminDashboardResponse>("/api/admin/dashboard", ApiFactory.Json);
        Assert.True(dashboard!.Admins >= 2);
        Assert.Equal(14, dashboard.SignUpsLast14Days.Count);
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
