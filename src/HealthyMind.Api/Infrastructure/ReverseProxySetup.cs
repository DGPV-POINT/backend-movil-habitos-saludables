using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace HealthyMind.Api.Infrastructure;

internal static class ReverseProxySetup
{
    /// <summary>
    /// Acepta X-Forwarded-For / X-Forwarded-Proto solo de los proxies listados en <c>ReverseProxy:KnownProxies</c>
    /// (p. ej. el IIS). Sin esto, detrás del proxy todos los clientes comparten la IP del proxy y el límite de
    /// intentos de login sería uno solo para todos.
    /// </summary>
    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        var proxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        return services;
    }
}
