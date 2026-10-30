using HealthyMind.Domain.Recommendations;

namespace HealthyMind.Application.Features.Recommendations;

public sealed record RecommendationResponse(
    Guid Id,
    string Title,
    string Summary,
    string? FocusHabit,
    string Recommendation,
    string ModelName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateTimeOffset CreatedAt)
{
    public static RecommendationResponse From(AiRecommendation recommendation) => new(
        recommendation.Id,
        recommendation.Title,
        recommendation.Summary,
        recommendation.FocusHabit,
        recommendation.Recommendation,
        recommendation.ModelName,
        recommendation.PeriodStart,
        recommendation.PeriodEnd,
        recommendation.CreatedAt);
}

public sealed class RecommendationOptions
{
    public const string SectionName = "Recommendations";

    /// <summary>Mínimo de horas entre dos recomendaciones del mismo usuario.</summary>
    public int MinHoursBetween { get; set; } = 24;

    /// <summary>Días de historial que se analizan.</summary>
    public int LookbackDays { get; set; } = 30;

    /// <summary>Usuarios por corrida, para no agotar la cuota del proveedor de IA.</summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// Permite que el usuario pida una recomendación al momento (modo presentación de la app).
    /// Apagado por defecto: en producción gasta cuota de la IA; se enciende solo en desarrollo.
    /// </summary>
    public bool AllowOnDemand { get; set; }

    /// <summary>Mínimo de minutos entre dos pedidos a la IA del mismo usuario, aunque el modo esté encendido.</summary>
    public int OnDemandMinMinutes { get; set; } = 2;
}
