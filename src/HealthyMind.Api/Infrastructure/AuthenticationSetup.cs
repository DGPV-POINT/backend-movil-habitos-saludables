using System.Security.Claims;
using HealthyMind.Application.Ports.In;
using HealthyMind.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HealthyMind.Api.Infrastructure;

internal static class AuthenticationSetup
{
    public const string AdminPolicy = "Admin";

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Se arma desde JwtOptions ya validadas, no leyendo la configuración a mano.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtClaims.Name,
                    RoleClaimType = JwtClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = RejectSuspendedUsersAsync };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy.RequireRole("Admin"));

        return services;
    }

    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? userId
            : throw new InvalidOperationException("El token no trae el identificador del usuario.");

    /// <summary>Un access token sigue siendo válido unos minutos; esto corta al instante a una cuenta suspendida.</summary>
    private static async Task RejectSuspendedUsersAsync(TokenValidatedContext context)
    {
        var sessions = context.HttpContext.RequestServices.GetRequiredService<ISessionUseCases>();
        var isActive = context.Principal is not null
            && Guid.TryParse(context.Principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            && await sessions.IsUserActiveAsync(userId, context.HttpContext.RequestAborted);

        if (!isActive)
        {
            context.Fail("La cuenta no existe o está suspendida.");
        }
    }
}
