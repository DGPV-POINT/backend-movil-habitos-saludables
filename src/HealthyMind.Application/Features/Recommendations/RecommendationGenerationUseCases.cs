using HealthyMind.Application.Features.Statistics;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Common;
using HealthyMind.Domain.Habits;
using HealthyMind.Domain.Recommendations;
using HealthyMind.Domain.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthyMind.Application.Features.Recommendations;

internal sealed class RecommendationGenerationUseCases(
    IUserRepository users,
    IHabitRepository habits,
    IHabitRecordRepository records,
    IRecommendationRepository recommendations,
    IAiRecommendationProvider aiProvider,
    ComplianceReader compliance,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IOptions<RecommendationOptions> options,
    ILogger<RecommendationGenerationUseCases> logger) : IRecommendationGenerationUseCases
{
    public async Task<int> GenerateDueAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var cutoff = timeProvider.GetUtcNow().AddHours(-settings.MinHoursBetween);
        var due = await recommendations.ListUsersDueAsync(cutoff, settings.BatchSize, cancellationToken);
        var generated = 0;

        foreach (var userId in due)
        {
            try
            {
                if (await GenerateForUserAsync(userId, cancellationToken) is not null)
                {
                    generated++;
                }
            }
            catch (Exception exception) when (exception is AiProviderUnavailableException or DomainException)
            {
                // Se reintenta en la próxima corrida: el usuario sigue pendiente.
                logger.LogWarning(exception, "No se pudo generar la recomendación del usuario {UserId}.", userId);
            }
        }

        return generated;
    }

    public async Task<RecommendationResponse?> GenerateForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var userHabits = await habits.ListByUserAsync(user.Id, new HabitFilter(), cancellationToken);
        if (userHabits.Count == 0)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var to = user.LocalDate(now);
        var from = to.AddDays(-(options.Value.LookbackDays - 1));

        var periodRecords = await records.ListByUserAsync(user.Id, from, to, cancellationToken);
        var report = ComplianceCalculator.Calculate(userHabits, periodRecords, from, to, user.GetTimeZone());
        var (current, best) = await compliance.ReadStreaksAsync(user, to, cancellationToken);

        var snapshot = BuildSnapshot(report, userHabits, periodRecords, current, best);
        var content = await aiProvider.GenerateAsync(snapshot, cancellationToken);

        var recommendation = AiRecommendation.Create(user.Id, content, from, to, now);
        recommendations.Add(recommendation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RecommendationResponse.From(recommendation);
    }

    private static HabitActivitySnapshot BuildSnapshot(
        ComplianceReport report,
        IReadOnlyList<Habit> userHabits,
        IReadOnlyList<HabitRecord> periodRecords,
        int currentStreak,
        int bestStreak)
    {
        var habitsById = userHabits.ToDictionary(habit => habit.Id);

        var habitActivity = report.Habits
            .Select(stats =>
            {
                var habit = habitsById[stats.HabitId];
                var values = periodRecords.Where(record => record.HabitId == habit.Id).Select(record => record.CompletedValue).ToList();
                return new HabitActivity(
                    habit.Name,
                    habit.Category.ToString(),
                    habit.Goal,
                    habit.Unit,
                    habit.IsActive,
                    stats.Expected,
                    stats.Completed,
                    stats.Percentage,
                    values.Count == 0 ? null : Math.Round(values.Average(), 2));
            })
            .ToList();

        var weekdays = report.Days
            .GroupBy(day => day.Date.DayOfWeek)
            .OrderBy(group => ((int)group.Key + 6) % 7)
            .Select(group =>
            {
                var expected = group.Sum(day => day.Expected);
                var completed = group.Sum(day => day.Completed);
                return new WeekdayActivity(group.Key, expected, completed, Percent.Of(completed, expected));
            })
            .ToList();

        return new HabitActivitySnapshot(
            report.From,
            report.To,
            report.Percentage,
            currentStreak,
            bestStreak,
            habitActivity,
            weekdays);
    }
}
