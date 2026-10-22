using HealthyMind.Domain.Habits;

namespace HealthyMind.Domain.Statistics;

/// <summary>
/// Calcula el cumplimiento de un periodo (RF04). Es la única fuente de verdad para estadísticas
/// diarias, semanales, mensuales y para el resumen que se envía a la IA.
/// </summary>
/// <remarks>
/// Un hábito "se esperaba" un día si ese día tiene registro, o si estaba activo, programado para ese
/// día de la semana y ya existía. No hay historial de activaciones: un hábito inactivo hoy solo cuenta
/// en los días en que sí se registró.
/// </remarks>
public static class ComplianceCalculator
{
    public static ComplianceReport Calculate(
        IEnumerable<Habit> habits,
        IEnumerable<HabitRecord> records,
        DateOnly from,
        DateOnly to,
        TimeZoneInfo timeZone)
    {
        if (from > to)
        {
            throw new ArgumentException("La fecha inicial no puede ser posterior a la final.", nameof(from));
        }

        var liveHabits = habits.Where(habit => !habit.IsDeleted).ToList();
        var recordsByKey = records
            .Where(record => !record.IsDeleted && record.RecordDate >= from && record.RecordDate <= to)
            .ToDictionary(record => (record.HabitId, record.RecordDate));

        var days = new List<DayCompliance>();
        var perHabit = liveHabits.ToDictionary(habit => habit.Id, _ => new Counter());

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var dayCounter = new Counter();

            foreach (var habit in liveHabits)
            {
                recordsByKey.TryGetValue((habit.Id, date), out var record);

                if (!WasExpected(habit, record, date, timeZone))
                {
                    continue;
                }

                var completed = record?.Completed == true;
                dayCounter.Add(completed);
                perHabit[habit.Id].Add(completed);
            }

            days.Add(new DayCompliance(date, dayCounter.Expected, dayCounter.Completed));
        }

        var habitsCompliance = liveHabits
            .Select(habit => new HabitCompliance(
                habit.Id,
                habit.Name,
                habit.Category,
                perHabit[habit.Id].Expected,
                perHabit[habit.Id].Completed))
            .ToList();

        return new ComplianceReport(from, to, days, habitsCompliance);
    }

    private static bool WasExpected(Habit habit, HabitRecord? record, DateOnly date, TimeZoneInfo timeZone)
    {
        if (record is not null)
        {
            return true;
        }

        var createdOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(habit.CreatedAt, timeZone).DateTime);
        return habit.IsActive && habit.IsScheduledOn(date) && date >= createdOn;
    }

    private sealed class Counter
    {
        public int Expected { get; private set; }

        public int Completed { get; private set; }

        public void Add(bool completed)
        {
            Expected++;
            if (completed)
            {
                Completed++;
            }
        }
    }
}

public sealed record ComplianceReport(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<DayCompliance> Days,
    IReadOnlyList<HabitCompliance> Habits)
{
    public int Expected => Days.Sum(day => day.Expected);

    public int Completed => Days.Sum(day => day.Completed);

    public double Percentage => Percent.Of(Completed, Expected);
}

public sealed record DayCompliance(DateOnly Date, int Expected, int Completed)
{
    public double Percentage => Percent.Of(Completed, Expected);
}

public sealed record HabitCompliance(Guid HabitId, string Name, HabitCategory Category, int Expected, int Completed)
{
    public double Percentage => Percent.Of(Completed, Expected);
}

public static class Percent
{
    public static double Of(int part, int total) =>
        total == 0 ? 0 : Math.Round(part * 100.0 / total, 2, MidpointRounding.AwayFromZero);
}
