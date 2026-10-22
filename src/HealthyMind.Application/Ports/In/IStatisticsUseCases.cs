using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Features.Statistics;

namespace HealthyMind.Application.Ports.In;

/// <summary>Estadísticas de cumplimiento (RF04). Las fechas son locales del usuario.</summary>
public interface IStatisticsUseCases
{
    Task<DashboardResponse> GetDashboardAsync(Guid userId, CancellationToken cancellationToken);

    Task<PeriodStatisticsResponse> GetDailyAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken);

    /// <summary>Semana de lunes a domingo que contiene <paramref name="date"/> (hoy si no se indica).</summary>
    Task<PeriodStatisticsResponse> GetWeeklyAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken);

    Task<PeriodStatisticsResponse> GetMonthlyAsync(Guid userId, int? year, int? month, CancellationToken cancellationToken);

    Task<PeriodStatisticsResponse> GetRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>Consulta de recomendaciones (RF05). Se generan solas; aquí solo se leen.</summary>
public interface IRecommendationUseCases
{
    Task<RecommendationResponse?> GetLatestAsync(Guid userId, CancellationToken cancellationToken);

    Task<PagedResult<RecommendationResponse>> ListAsync(Guid userId, PageRequest page, CancellationToken cancellationToken);

    Task<RecommendationResponse> GetAsync(Guid userId, Guid recommendationId, CancellationToken cancellationToken);
}

/// <summary>Generación automática de recomendaciones; la dispara un proceso en segundo plano.</summary>
public interface IRecommendationGenerationUseCases
{
    /// <summary>Genera para los usuarios a los que ya les toca. Devuelve cuántas se generaron.</summary>
    Task<int> GenerateDueAsync(CancellationToken cancellationToken);

    /// <summary>Genera ya para un usuario. Devuelve null si todavía no tiene hábitos.</summary>
    Task<RecommendationResponse?> GenerateForUserAsync(Guid userId, CancellationToken cancellationToken);
}
