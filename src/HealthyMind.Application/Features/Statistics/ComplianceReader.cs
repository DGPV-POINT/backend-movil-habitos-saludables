using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Statistics;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Statistics;

/// <summary>Carga hábitos y registros de un periodo y los pasa por la calculadora del dominio.</summary>
internal sealed class ComplianceReader(IHabitRepository habits, IHabitRecordRepository records)
{
    public async Task<ComplianceReport> ReadAsync(
        User user,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var userHabits = await habits.ListByUserAsync(user.Id, new HabitFilter(), cancellationToken);
        var periodRecords = await records.ListByUserAsync(user.Id, from, to, cancellationToken);
        return ComplianceCalculator.Calculate(userHabits, periodRecords, from, to, user.GetTimeZone());
    }

    public async Task<(int Current, int Best)> ReadStreaksAsync(User user, DateOnly today, CancellationToken cancellationToken)
    {
        var completedDays = await records.ListCompletedDatesAsync(user.Id, cancellationToken);
        return (StreakCalculator.Current(completedDays, today), StreakCalculator.Best(completedDays));
    }
}
