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
            logger.LogWarning("Groq:ApiKey no está configurada: no se generarán recomendaciones automáticas.");
            return;
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
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // Un fallo (p. ej. la base caída) no debe matar el proceso: se reintenta en la siguiente vuelta.
            logger.LogError(exception, "Falló la corrida de recomendaciones automáticas.");
        }
    }
}
