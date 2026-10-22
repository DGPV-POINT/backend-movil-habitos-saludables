using FluentValidation;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Statistics;

internal sealed class StatisticsUseCases(
    OwnedResources owned,
    ComplianceReader compliance,
    IRecommendationRepository recommendations,
    TimeProvider timeProvider) : IStatisticsUseCases
{
    public const int MaxRangeDays = 366;

    public async Task<DashboardResponse> GetDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await owned.UserAsync(userId, cancellationToken);
        var today = Today(user);

        var month = await compliance.ReadAsync(user, new DateOnly(today.Year, today.Month, 1), today, cancellationToken);
        var week = await compliance.ReadAsync(user, StartOfWeek(today), today, cancellationToken);
        var todayStats = week.Days[^1];
        var (current, best) = await compliance.ReadStreaksAsync(user, today, cancellationToken);
        var latest = await recommendations.GetLatestAsync(user.Id, cancellationToken);

        return new DashboardResponse(
            today,
            todayStats.Expected,
            todayStats.Completed,
            todayStats.Percentage,
            week.Percentage,
            month.Percentage,
            current,
            best,
            latest is null ? null : RecommendationResponse.From(latest));
    }

    public async Task<PeriodStatisticsResponse> GetDailyAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken)
    {
        var user = await owned.UserAsync(userId, cancellationToken);
        var day = date ?? Today(user);
        return await ReadAsync(user, day, day, cancellationToken);
    }

    public async Task<PeriodStatisticsResponse> GetWeeklyAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken)
    {
        var user = await owned.UserAsync(userId, cancellationToken);
        var start = StartOfWeek(date ?? Today(user));
        return await ReadAsync(user, start, start.AddDays(6), cancellationToken);
    }

    public async Task<PeriodStatisticsResponse> GetMonthlyAsync(
        Guid userId,
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        var user = await owned.UserAsync(userId, cancellationToken);
        var today = Today(user);

        if (month is < 1 or > 12 || year is < 2000 or > 9999)
        {
            throw new ValidationException("El año o el mes no son válidos.");
        }

        var start = new DateOnly(year ?? today.Year, month ?? today.Month, 1);
        return await ReadAsync(user, start, start.AddMonths(1).AddDays(-1), cancellationToken);
    }

    public async Task<PeriodStatisticsResponse> GetRangeAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            throw new ValidationException("La fecha inicial no puede ser posterior a la fecha final.");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
        {
            throw new ValidationException($"El rango no puede superar los {MaxRangeDays} días.");
        }

        var user = await owned.UserAsync(userId, cancellationToken);
        return await ReadAsync(user, from, to, cancellationToken);
    }

    /// <summary>Los días que aún no llegan no cuentan: el periodo se corta en hoy.</summary>
    private async Task<PeriodStatisticsResponse> ReadAsync(
        User user,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var today = Today(user);
        if (from > today)
        {
            throw new ValidationException("No se pueden consultar fechas futuras.");
        }

        var report = await compliance.ReadAsync(user, from, to > today ? today : to, cancellationToken);
        return PeriodStatisticsResponse.FromReport(report);
    }

    private DateOnly Today(User user) => user.LocalDate(timeProvider.GetUtcNow());

    private static DateOnly StartOfWeek(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
