namespace HealthyMind.Domain.Habits;

/// <summary>Días de la semana en que aplica un hábito.</summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Everyday = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday,
}

public static class WeekDaysExtensions
{
    public static WeekDays ToWeekDay(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        DayOfWeek.Saturday => WeekDays.Saturday,
        DayOfWeek.Sunday => WeekDays.Sunday,
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, null),
    };

    /// <summary>Lista vacía o nula significa todos los días.</summary>
    public static WeekDays ToWeekDays(this IEnumerable<DayOfWeek>? days)
    {
        var result = WeekDays.None;
        foreach (var day in days ?? [])
        {
            result |= day.ToWeekDay();
        }

        return result == WeekDays.None ? WeekDays.Everyday : result;
    }

    public static IReadOnlyList<DayOfWeek> ToDaysOfWeek(this WeekDays days) =>
        Enum.GetValues<DayOfWeek>().Where(day => days.HasFlag(day.ToWeekDay())).ToList();
}
