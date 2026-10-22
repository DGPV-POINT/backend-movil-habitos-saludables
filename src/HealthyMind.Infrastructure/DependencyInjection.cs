using HealthyMind.Application.Ports.Out;
using HealthyMind.Infrastructure.Ai;
using HealthyMind.Infrastructure.BackgroundJobs;
using HealthyMind.Infrastructure.Persistence;
using HealthyMind.Infrastructure.Persistence.Repositories;
using HealthyMind.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HealthyMind.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "HealthyMind";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddSecurity(services);
        AddAi(services, configuration);

        services.AddOptions<RecommendationJobOptions>()
            .BindConfiguration(RecommendationJobOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHostedService<RecommendationBackgroundService>();

        return services;
    }

    /// <summary>Aplica las migraciones pendientes. Pensado para desarrollo y para el contenedor.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<HealthyMindDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión '{ConnectionStringName}'.");

        services.AddSingleton<SyncTimestampInterceptor>();
        services.AddDbContext<HealthyMindDbContext>((provider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(provider.GetRequiredService<SyncTimestampInterceptor>()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IHabitRepository, HabitRepository>();
        services.AddScoped<IHabitRecordRepository, HabitRecordRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
    }

    private static void AddSecurity(IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    }

    private static void AddAi(IServiceCollection services, IConfiguration configuration)
    {
        var timeout = TimeSpan.FromSeconds(
            configuration.GetSection(GroqOptions.SectionName).Get<GroqOptions>()?.TimeoutSeconds ?? new GroqOptions().TimeoutSeconds);

        services.AddOptions<GroqOptions>()
            .BindConfiguration(GroqOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IAiRecommendationProvider, GroqRecommendationProvider>((provider, client) =>
            {
                var groq = provider.GetRequiredService<IOptions<GroqOptions>>().Value;
                client.BaseAddress = new Uri(groq.BaseUrl.EndsWith('/') ? groq.BaseUrl : groq.BaseUrl + "/");
                client.DefaultRequestHeaders.Authorization = new("Bearer", groq.ApiKey);
            })
            .AddStandardResilienceHandler(resilience =>
            {
                // Reintenta 429/5xx con espera exponencial; cada intento tiene el tiempo configurado.
                resilience.AttemptTimeout.Timeout = timeout;
                resilience.CircuitBreaker.SamplingDuration = timeout * 2;
                resilience.TotalRequestTimeout.Timeout = timeout * 3;
            });
    }
}
