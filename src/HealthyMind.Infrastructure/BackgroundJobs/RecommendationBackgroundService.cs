using System.ComponentModel.DataAnnotations;
using HealthyMind.Application.Ports.In;
using HealthyMind.Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthyMind.Infrastructure.BackgroundJobs;

public sealed class RecommendationJobOptions
{
    public const string SectionName = "RecommendationJob";

    public bool Enabled { get; set; } = true;

    /// <summary>Cada cuánto se buscan usuarios a los que ya les toca una recomendación.</summary>
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 60;

    [Range(0, 3600)]
    public int InitialDelaySeconds { get; set; } = 30;
}

/// <summary>
/// Adaptador de entrada disparado por tiempo: genera las recomendaciones solas, sin que el usuario las pida.
/// </summary>
internal sealed class RecommendationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RecommendationJobOptions> jobOptions,
    IOptions<GroqOptions> groqOptions,
    TimeProvider timeProvider,
    ILogger<RecommendationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = jobOptions.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Generación automática de recomendaciones deshabilitada.");
            return;
        }

        if (!groqOptions.Value.IsConfigured)
        {
            // Sigue corriendo: sin IA las recomendaciones se arman con reglas (RuleBasedRecommendation).
            logger.LogWarning("Groq:ApiKey no está configurada: las recomendaciones se generarán con reglas, sin IA.");
        }

        await Task.Delay(TimeSpan.FromSeconds(settings.InitialDelaySeconds), timeProvider, stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.IntervalMinutes), timeProvider);

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var useCases = scope.ServiceProvider.GetRequiredService<IRecommendationGenerationUseCases>();
            var generated = await useCases.GenerateDueAsync(stoppingToken);

            if (generated > 0)
            {
                logger.LogInformation("Se generaron {Count} recomendaciones automáticas.", generated);
            }

            // De paso, borra los tokens de teléfonos que desinstalaron la app sin cerrar sesión.
            var removed = await scope.ServiceProvider.GetRequiredService<IPushUseCases>().RemoveStaleDevicesAsync(stoppingToken);
            if (removed > 0)
            {
                logger.LogInformation("Se borraron {Count} tokens de avisos push abandonados.", removed);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // Un fallo (p. ej. la base caída) no debe matar el proceso: se reintenta en la siguiente vuelta.
            logger.LogError(exception, "Falló la corrida de recomendaciones automáticas.");
        }
    }
}
