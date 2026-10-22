namespace HealthyMind.Domain.Statistics;

/// <summary>Rachas: días seguidos con al menos un hábito cumplido.</summary>
public static class StreakCalculator
{
    /// <summary>
    /// La racha sigue viva si hoy o ayer hubo cumplimiento: hoy todavía no termina.
    /// </summary>
    public static int Current(IEnumerable<DateOnly> completedDays, DateOnly today)
    {
        var days = completedDays.ToHashSet();

        var cursor = days.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;

        while (days.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    public static int Best(IEnumerable<DateOnly> completedDays)
    {
        var best = 0;
        var current = 0;
        DateOnly? previous = null;

        foreach (var day in completedDays.Distinct().Order())
        {
            current = previous is not null && previous.Value.AddDays(1) == day ? current + 1 : 1;
            best = Math.Max(best, current);
            previous = day;
        }

        return best;
    }
}
