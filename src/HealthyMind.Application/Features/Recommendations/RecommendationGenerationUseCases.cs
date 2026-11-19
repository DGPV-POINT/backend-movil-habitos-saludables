using HealthyMind.Application.Common;
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
    IRecommendationPublisher publisher,
    TimeProvider timeProvider,
    IOptions<RecommendationOptions> options,
    ILogger<RecommendationGenerationUseCases> logger) : IRecommendationGenerationUseCases
{
    /// <summary>Recomendaciones anteriores que se le muestran a la IA para que no se repita.</summary>
    private const int PreviousForContext = 3;

    public async Task<int> GenerateDueAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var cutoff = timeProvider.GetUtcNow().AddHours(-settings.MinHoursBetween);
        var due = await recommendations.ListUsersDueAsync(cutoff, settings.BatchSize, cancellationToken);
        var generated = 0;

        var now = timeProvider.GetUtcNow();
        foreach (var userId in due)
        {
            try
            {
                // De noche no: el aviso llegaría de madrugada. Queda pendiente para la primera corrida de la mañana.
                var user = await users.GetByIdAsync(userId, cancellationToken);
                if (user is null || QuietHours.Contains(user.LocalHour(now), settings.QuietHoursStart, settings.QuietHoursEnd))
                {
                    continue;
                }

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

        var previous = await recommendations.ListCreatedSinceAsync(user.Id, null, PreviousForContext, cancellationToken);
        var snapshot = BuildSnapshot(report, userHabits, periodRecords, current, best) with
        {
            Previous = previous.Select(item => new PastAdvice(item.Title, item.FocusHabit, item.WasHelpful)).ToList(),
        };

        RecommendationContent content;
        try
        {
            content = await aiProvider.GenerateAsync(snapshot, cancellationToken);
        }
        catch (AiProviderUnavailableException exception)
        {
            // Sin IA el usuario igual recibe su análisis, armado con reglas sobre las mismas cifras.
            logger.LogWarning(exception, "La IA no respondió: se usa la recomendación por reglas para {UserId}.", user.Id);
            content = RuleBasedRecommendation.Build(snapshot);
        }

        var recommendation = AiRecommendation.Create(user.Id, content, from, to, now);
        recommendations.Add(recommendation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = RecommendationResponse.From(recommendation);
        await publisher.PublishAsync(user.Id, response, cancellationToken);
        return response;
    }

    public async Task<RecommendationResponse?> GenerateOnDemandAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.AllowOnDemand)
        {
            throw new ForbiddenException("Este servidor no genera recomendaciones a pedido.");
        }

        var latest = await recommendations.GetLatestAsync(userId, cancellationToken);
        if (latest is not null && latest.CreatedAt > timeProvider.GetUtcNow().AddMinutes(-settings.OnDemandMinMinutes))
        {
            return RecommendationResponse.From(latest);
        }

        return await GenerateForUserAsync(userId, cancellationToken);
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
