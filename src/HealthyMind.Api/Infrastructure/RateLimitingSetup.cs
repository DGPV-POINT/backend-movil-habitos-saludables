using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace HealthyMind.Api.Infrastructure;

internal static class RateLimitingSetup
{
    public const string AuthPolicy = "auth";

    /// <summary>Frena la adivinación de contraseñas: pocas llamadas de autenticación por minuto y por IP.</summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitsPerMinute = configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Demasiados intentos",
                        Detail = "Espera un minuto antes de volver a intentarlo.",
                    },
                    cancellationToken);
            };
        });

        return services;
    }
}
