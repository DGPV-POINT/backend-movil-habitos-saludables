using HealthyMind.Domain.Common;
using HealthyMind.Domain.Habits;

namespace HealthyMind.UnitTests.Domain;

public sealed class HabitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 22);

    internal static Habit NewHabit(int goal = 8, WeekDays days = WeekDays.Everyday, DateTimeOffset? createdAt = null) =>
        Habit.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new HabitDefinition("Tomar agua", HabitCategory.Hidratacion, goal, "vasos", days, TrackingMode.Manual),
            createdAt ?? Now);

    [Fact]
    public void Record_is_completed_only_when_the_goal_is_reached()
    {
        var habit = NewHabit(goal: 8);

        var short_ = HabitRecord.Create(Guid.CreateVersion7(), habit, Today.AddDays(-1), new HabitProgress(7, null, null), Today, Now);
        var met = HabitRecord.Create(Guid.CreateVersion7(), habit, Today, new HabitProgress(8, null, null), Today, Now);

        Assert.False(short_.Completed);
        Assert.True(met.Completed);
    }

    [Fact]
    public void Future_dates_are_rejected()
    {
        var habit = NewHabit();

        var error = Assert.Throws<DomainException>(() =>
            HabitRecord.Create(Guid.CreateVersion7(), habit, Today.AddDays(1), new HabitProgress(1, null, null), Today, Now));

        Assert.Equal("No se puede registrar una fecha futura.", error.Message);
    }

    [Fact]
    public void Inactive_habit_rejects_new_records_but_allows_corrections()
    {
        var habit = NewHabit(goal: 2);
        var record = HabitRecord.Create(Guid.CreateVersion7(), habit, Today, new HabitProgress(1, null, null), Today, Now);
        habit.SetActive(false, Now.AddMinutes(1));

        Assert.Throws<DomainException>(() =>
            HabitRecord.Create(Guid.CreateVersion7(), habit, Today.AddDays(-1), new HabitProgress(1, null, null), Today, Now));

        record.Update(habit, new HabitProgress(2, "corregido", null), Now.AddMinutes(2));
        Assert.True(record.Completed);
    }

    [Fact]
    public void Deleted_habit_cannot_be_edited()
    {
        var habit = NewHabit();
        habit.Delete(Now);

        Assert.True(habit.IsDeleted);
        Assert.Throws<DomainException>(() => habit.SetActive(false, Now.AddMinutes(1)));
    }

    [Fact]
    public void Last_write_wins_compares_change_timestamps()
    {
        var habit = NewHabit(createdAt: Now);

        Assert.True(habit.IsOlderThan(Now.AddSeconds(1)));
        Assert.False(habit.IsOlderThan(Now));
        Assert.False(habit.IsOlderThan(Now.AddMinutes(-5)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Goal_must_be_positive(int goal) =>
        Assert.Throws<DomainException>(() => NewHabit(goal: goal));

    [Fact]
    public void Scheduled_days_round_trip_and_empty_means_everyday()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Friday }.ToWeekDays();

        Assert.Equal(WeekDays.Monday | WeekDays.Friday, days);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], days.ToDaysOfWeek());
        Assert.Equal(WeekDays.Everyday, Array.Empty<DayOfWeek>().ToWeekDays());
        Assert.Equal(WeekDays.Everyday, ((IEnumerable<DayOfWeek>?)null).ToWeekDays());
    }

    [Theory]
    [InlineData(10.0, null)]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    public void Invalid_locations_are_rejected(double? latitude, double? longitude) =>
        Assert.Throws<DomainException>(() => GeoLocation.From(latitude, longitude));

    [Fact]
    public void Missing_location_is_allowed()
    {
        Assert.Null(GeoLocation.From(null, null));
        Assert.Equal(-0.18, GeoLocation.From(-0.18, -78.47)!.Latitude);
    }
}
