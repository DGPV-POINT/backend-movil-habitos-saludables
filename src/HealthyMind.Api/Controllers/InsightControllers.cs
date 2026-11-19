using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Features.Statistics;
using HealthyMind.Application.Features.Sync;
using HealthyMind.Application.Ports.In;
using Microsoft.AspNetCore.Mvc;

namespace HealthyMind.Api.Controllers;

/// <summary>Estadísticas (RF04). Fechas en formato yyyy-MM-dd, en la zona horaria del usuario.</summary>
[Route("api/statistics")]
public sealed class StatisticsController(IStatisticsUseCases statistics) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardResponse> Dashboard(CancellationToken cancellationToken) =>
        statistics.GetDashboardAsync(CurrentUserId, cancellationToken);

    [HttpGet("daily")]
    public Task<PeriodStatisticsResponse> Daily([FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        statistics.GetDailyAsync(CurrentUserId, date, cancellationToken);

    [HttpGet("weekly")]
    public Task<PeriodStatisticsResponse> Weekly([FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        statistics.GetWeeklyAsync(CurrentUserId, date, cancellationToken);

    [HttpGet("monthly")]
    public Task<PeriodStatisticsResponse> Monthly(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken) =>
        statistics.GetMonthlyAsync(CurrentUserId, year, month, cancellationToken);

    [HttpGet("range")]
    public Task<PeriodStatisticsResponse> Range(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        statistics.GetRangeAsync(CurrentUserId, from, to, cancellationToken);
}

/// <summary>Recomendaciones de la IA (RF05). Se generan automáticamente; aquí se consultan.</summary>
[Route("api/recommendations")]
public sealed class RecommendationsController(
    IRecommendationUseCases recommendations,
    IRecommendationGenerationUseCases generation) : ApiControllerBase
{
    /// <summary>
    /// Pide una al momento (modo presentación de la app). Solo si el servidor lo permite
    /// (<c>Recommendations:AllowOnDemand</c>); 204 si el usuario todavía no tiene hábitos.
    /// </summary>
    [HttpPost("now")]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RecommendationResponse>> Now(CancellationToken cancellationToken)
    {
        var recommendation = await generation.GenerateOnDemandAsync(CurrentUserId, cancellationToken);
        return recommendation is null ? NoContent() : Ok(recommendation);
    }

    /// <summary>La más reciente; 204 si todavía no hay ninguna.</summary>
    [HttpGet("latest")]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<RecommendationResponse>> Latest(CancellationToken cancellationToken)
    {
        var latest = await recommendations.GetLatestAsync(CurrentUserId, cancellationToken);
        return latest is null ? NoContent() : Ok(latest);
    }

    [HttpGet]
    public Task<PagedResult<RecommendationResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        recommendations.ListAsync(CurrentUserId, new PageRequest(page, pageSize), cancellationToken);

    /// <summary>"Me sirvió" (true) o "No me sirvió" (false). Se puede cambiar.</summary>
    [HttpPut("{recommendationId:guid}/feedback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Feedback(
        Guid recommendationId,
        RecommendationFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        await recommendations.RateAsync(CurrentUserId, recommendationId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>El usuario abrió la pestaña IA: apaga el aviso de "sin leer" en sus otros teléfonos.</summary>
    [HttpPut("seen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Seen(RecommendationsSeenRequest request, CancellationToken cancellationToken)
    {
        await recommendations.MarkSeenAsync(CurrentUserId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{recommendationId:guid}")]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RecommendationResponse> Get(Guid recommendationId, CancellationToken cancellationToken) =>
        recommendations.GetAsync(CurrentUserId, recommendationId, cancellationToken);
}

/// <summary>
/// Sincronización con el SQLite del móvil (RNF05). Orden al recuperar conexión: primero push, luego pull.
/// </summary>
[Route("api/sync")]
public sealed class SyncController(ISyncUseCases sync) : ApiControllerBase
{
    [HttpPost("push")]
    public Task<SyncPushResponse> Push(SyncPushRequest request, CancellationToken cancellationToken) =>
        sync.PushAsync(CurrentUserId, request, cancellationToken);

    /// <param name="since">El <c>serverTime</c> del pull anterior. Sin él se descarga todo.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet("pull")]
    public Task<SyncPullResponse> Pull([FromQuery] DateTimeOffset? since, CancellationToken cancellationToken) =>
        sync.PullAsync(CurrentUserId, since, cancellationToken);
}
