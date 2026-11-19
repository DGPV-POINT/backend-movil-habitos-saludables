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
    DateTimeOffset CreatedAt,
    bool? WasHelpful = null)
{
    /// <summary>true si la armaron las reglas porque la IA no respondió.</summary>
    public bool IsRuleBased => ModelName == RuleBasedRecommendation.ModelName;

    public static RecommendationResponse From(AiRecommendation recommendation) => new(
        recommendation.Id,
        recommendation.Title,
        recommendation.Summary,
        recommendation.FocusHabit,
        recommendation.Recommendation,
        recommendation.ModelName,
        recommendation.PeriodStart,
        recommendation.PeriodEnd,
        recommendation.CreatedAt,
        recommendation.WasHelpful);
}

/// <summary>"Me sirvió" / "No me sirvió".</summary>
public sealed record RecommendationFeedbackRequest(bool Helpful);

/// <summary>El usuario abrió la pestaña IA hasta este momento (apaga la campana en sus otros teléfonos).</summary>
public sealed record RecommendationsSeenRequest(DateTimeOffset SeenAt);

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

    /// <summary>Hora local (0–23) desde la que no se generan recomendaciones automáticas…</summary>
    public int QuietHoursStart { get; set; } = 22;

    /// <summary>…y hasta la que dura el silencio. El modo presentación no lo respeta: lo pide el usuario.</summary>
    public int QuietHoursEnd { get; set; } = 7;
}
