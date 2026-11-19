using HealthyMind.Domain.Recommendations;

namespace HealthyMind.Application.Ports.Out;

/// <summary>Modelo de lenguaje que redacta la recomendación (hoy: el modelo configurado en Groq).</summary>
public interface IAiRecommendationProvider
{
    /// <exception cref="AiProviderUnavailableException">El proveedor falló o respondió algo inutilizable.</exception>
    Task<RecommendationContent> GenerateAsync(HabitActivitySnapshot snapshot, CancellationToken cancellationToken);
}

/// <summary>
/// Lo único que sale hacia el proveedor de IA: cifras y nombres de hábitos, nunca datos personales.
/// </summary>
public sealed record HabitActivitySnapshot(
    DateOnly From,
    DateOnly To,
    double OverallPercentage,
    int CurrentStreak,
    int BestStreak,
    IReadOnlyList<HabitActivity> Habits,
    IReadOnlyList<WeekdayActivity> Weekdays,
    IReadOnlyList<PastAdvice>? Previous = null);

/// <summary>Una recomendación anterior: para no repetirla y para saber qué le sirvió al usuario.</summary>
public sealed record PastAdvice(string Title, string? FocusHabit, bool? WasHelpful);

public sealed record HabitActivity(
    string Name,
    string Category,
    int Goal,
    string Unit,
    bool IsActive,
    int Expected,
    int Completed,
    double Percentage,
    double? AverageValue);

public sealed record WeekdayActivity(DayOfWeek Day, int Expected, int Completed, double Percentage);

public sealed class AiProviderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
