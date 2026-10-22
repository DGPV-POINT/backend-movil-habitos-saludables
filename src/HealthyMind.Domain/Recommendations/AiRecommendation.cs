using HealthyMind.Domain.Common;

namespace HealthyMind.Domain.Recommendations;

/// <summary>Recomendación generada automáticamente a partir del historial del usuario (RF05).</summary>
public sealed class AiRecommendation : Entity
{
    public const int TitleMaxLength = 150;
    public const int FocusHabitMaxLength = 150;
    public const int ModelNameMaxLength = 100;
    public const int TextMaxLength = 4_000;

    private AiRecommendation()
    {
    }

    private AiRecommendation(Guid id) : base(id)
    {
    }

    public Guid UserId { get; private init; }

    public string Title { get; private init; } = null!;

    /// <summary>Hallazgo principal del análisis.</summary>
    public string Summary { get; private init; } = null!;

    /// <summary>Hábito que requiere más atención, si lo hay.</summary>
    public string? FocusHabit { get; private init; }

    /// <summary>Recomendación práctica para los próximos días.</summary>
    public string Recommendation { get; private init; } = null!;

    public string ModelName { get; private init; } = null!;

    public DateOnly PeriodStart { get; private init; }

    public DateOnly PeriodEnd { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public static AiRecommendation Create(
        Guid userId,
        RecommendationContent content,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateTimeOffset createdAt)
    {
        Guard.NotEmpty(userId, "usuario");

        if (periodStart > periodEnd)
        {
            throw new DomainException("El periodo analizado no es válido.");
        }

        return new AiRecommendation(Guid.CreateVersion7())
        {
            UserId = userId,
            Title = Guard.Text(content.Title, "título", TitleMaxLength),
            Summary = Guard.Text(content.Summary, "análisis", TextMaxLength),
            FocusHabit = Guard.OptionalText(content.FocusHabit, "hábito a reforzar", FocusHabitMaxLength),
            Recommendation = Guard.Text(content.Recommendation, "recomendación", TextMaxLength),
            ModelName = Guard.Text(content.ModelName, "modelo", ModelNameMaxLength),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CreatedAt = createdAt.ToUniversalTime(),
        };
    }
}

public sealed record RecommendationContent(
    string Title,
    string Summary,
    string? FocusHabit,
    string Recommendation,
    string ModelName);
