using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Domain.Habits;
using HealthyMind.Domain.Statistics;

namespace HealthyMind.Application.Features.Statistics;

/// <summary>Misma forma para el día, la semana, el mes o un rango: así el móvil usa un solo gráfico.</summary>
public sealed record PeriodStatisticsResponse(
    DateOnly From,
    DateOnly To,
    int Expected,
    int Completed,
    double CompletionPercentage,
    IReadOnlyList<DayStatisticsResponse> Days,
    IReadOnlyList<HabitStatisticsResponse> Habits)
{
    public static PeriodStatisticsResponse FromReport(ComplianceReport report) => new(
        report.From,
        report.To,
        report.Expected,
        report.Completed,
        report.Percentage,
        report.Days
            .Select(day => new DayStatisticsResponse(day.Date, day.Date.DayOfWeek, day.Expected, day.Completed, day.Percentage))
            .ToList(),
        report.Habits
            .Select(habit => new HabitStatisticsResponse(
                habit.HabitId,
                habit.Name,
                habit.Category,
                habit.Expected,
                habit.Completed,
                habit.Percentage))
            .ToList());
}

public sealed record DayStatisticsResponse(
    DateOnly Date,
    DayOfWeek DayOfWeek,
    int Expected,
    int Completed,
    double Percentage);

public sealed record HabitStatisticsResponse(
    Guid HabitId,
    string Name,
    HabitCategory Category,
    int Expected,
    int Completed,
    double Percentage);

/// <summary>Pantalla de inicio: lo de hoy, cómo va la semana y el mes, rachas y la última recomendación.</summary>
public sealed record DashboardResponse(
    DateOnly Today,
    int ExpectedToday,
    int CompletedToday,
    double TodayPercentage,
    double WeekPercentage,
    double MonthPercentage,
    int CurrentStreak,
    int BestStreak,
    RecommendationResponse? LatestRecommendation);
