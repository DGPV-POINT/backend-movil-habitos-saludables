using HealthyMind.Domain.Habits;
using HealthyMind.Domain.Statistics;

namespace HealthyMind.UnitTests.Domain;

public sealed class StatisticsTests
{
    private static readonly TimeZoneInfo Ecuador = TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");

    // Lunes 19 a domingo 25 de octubre de 2026.
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private static readonly DateTimeOffset CreatedMonday = new(2026, 10, 19, 13, 0, 0, TimeSpan.Zero);

    private static HabitRecord Record(Habit habit, DateOnly date, int value) =>
        HabitRecord.Create(Guid.CreateVersion7(), habit, date, new HabitProgress(value, null, null), Monday.AddDays(6), CreatedMonday);

    [Fact]
    public void Expected_counts_only_scheduled_days_after_creation()
    {
        var weekdaysOnly = HabitTests.NewHabit(
            goal: 1,
            days: WeekDays.Monday | WeekDays.Tuesday | WeekDays.Wednesday | WeekDays.Thursday | WeekDays.Friday,
            createdAt: CreatedMonday.AddDays(2)); // creado el miércoles

        var report = ComplianceCalculator.Calculate([weekdaysOnly], [], Monday, Monday.AddDays(6), Ecuador);

        // Miércoles, jueves y viernes.
        Assert.Equal(3, report.Expected);
        Assert.Equal(0, report.Completed);
    }

    [Fact]
    public void Percentage_reflects_completed_over_expected()
    {
        var habit = HabitTests.NewHabit(goal: 8, createdAt: CreatedMonday);
        var records = new[]
        {
            Record(habit, Monday, 8),
            Record(habit, Monday.AddDays(1), 3),
            Record(habit, Monday.AddDays(2), 10),
        };

        var report = ComplianceCalculator.Calculate([habit], records, Monday, Monday.AddDays(3), Ecuador);

        Assert.Equal(4, report.Expected);
        Assert.Equal(2, report.Completed);
        Assert.Equal(50, report.Percentage);
        Assert.Equal(100, report.Days[0].Percentage);
        Assert.Equal(0, report.Days[3].Percentage);
        Assert.Equal(50, Assert.Single(report.Habits).Percentage);
    }

    [Fact]
    public void Inactive_habit_only_counts_on_days_it_was_recorded()
    {
        var habit = HabitTests.NewHabit(goal: 1, createdAt: CreatedMonday);
        var record = Record(habit, Monday, 1);
        habit.SetActive(false, CreatedMonday.AddDays(1));

        var report = ComplianceCalculator.Calculate([habit], [record], Monday, Monday.AddDays(6), Ecuador);

        Assert.Equal(1, report.Expected);
        Assert.Equal(1, report.Completed);
    }

    [Fact]
    public void Deleted_records_and_habits_are_ignored()
    {
        var kept = HabitTests.NewHabit(goal: 1, createdAt: CreatedMonday);
        var removed = HabitTests.NewHabit(goal: 1, createdAt: CreatedMonday);
        var deletedRecord = Record(kept, Monday, 1);
        deletedRecord.Delete(CreatedMonday.AddHours(1));
        removed.Delete(CreatedMonday.AddHours(1));

        var report = ComplianceCalculator.Calculate([kept, removed], [deletedRecord], Monday, Monday, Ecuador);

        Assert.Equal(1, report.Expected);
        Assert.Equal(0, report.Completed);
    }

    [Fact]
    public void Current_streak_survives_until_today_ends()
    {
        var today = new DateOnly(2026, 10, 22);
        var days = new[] { today.AddDays(-1), today.AddDays(-2), today.AddDays(-3), today.AddDays(-6) };

        Assert.Equal(3, StreakCalculator.Current(days, today));
        Assert.Equal(4, StreakCalculator.Current([.. days, today], today));
        Assert.Equal(0, StreakCalculator.Current([today.AddDays(-2)], today));
    }

    [Fact]
    public void Best_streak_finds_the_longest_run()
    {
        var start = new DateOnly(2026, 1, 1);
        var days = new[] { start, start.AddDays(1), start.AddDays(5), start.AddDays(6), start.AddDays(7), start.AddDays(7) };

        Assert.Equal(3, StreakCalculator.Best(days));
        Assert.Equal(0, StreakCalculator.Best([]));
    }
}
