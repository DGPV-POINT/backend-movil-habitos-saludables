using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Notifications;
using HealthyMind.Domain.Recommendations;
using HealthyMind.Domain.Users;

namespace HealthyMind.UnitTests.Domain;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(23, true)]
    [InlineData(3, true)]
    [InlineData(7, false)]
    [InlineData(15, false)]
    [InlineData(22, true)]
    public void Quiet_hours_cross_midnight(int hour, bool quiet) =>
        Assert.Equal(quiet, QuietHours.Contains(hour, start: 22, end: 7));

    [Fact]
    public void User_local_hour_uses_the_user_time_zone()
    {
        var user = User.Register("ana.perez", "ana@correo.com", "Ana", "Pérez", "hash", UserRole.User, "America/Guayaquil", Now);

        Assert.Equal(10, user.LocalHour(Now)); // 15:00 UTC = 10:00 en Ecuador
    }

    [Fact]
    public void Seen_mark_only_moves_forward_and_never_into_the_future()
    {
        var user = User.Register("ana.perez", "ana@correo.com", "Ana", "Pérez", "hash", UserRole.User, null, Now);

        user.MarkRecommendationsSeen(Now.AddHours(-1), Now);
        user.MarkRecommendationsSeen(Now.AddHours(-5), Now); // un teléfono atrasado no retrocede la marca
        Assert.Equal(Now.AddHours(-1), user.RecommendationsSeenAt);

        user.MarkRecommendationsSeen(Now.AddDays(3), Now); // ni la adelanta más allá de ahora
        Assert.Equal(Now, user.RecommendationsSeenAt);
    }

    [Fact]
    public void Feedback_can_change_its_mind()
    {
        var recommendation = AiRecommendation.Create(
            Guid.NewGuid(),
            new RecommendationContent("Título", "Hallazgo", null, "• Acción", "modelo"),
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 19),
            Now);

        recommendation.RateHelpful(false, Now);
        recommendation.RateHelpful(true, Now.AddMinutes(1));

        Assert.True(recommendation.WasHelpful);
        Assert.Equal(Now.AddMinutes(1), recommendation.FeedbackAt);
    }

    [Fact]
    public void Rule_based_fallback_points_to_the_weakest_habit_and_day()
    {
        var snapshot = new HabitActivitySnapshot(
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 19),
            55,
            3,
            7,
            [
                new HabitActivity("Tomar agua", "Salud", 8, "vasos", true, 19, 17, 89.5, 7),
                new HabitActivity("Leer", "Mente", 1, "vez", true, 19, 4, 21.1, 1),
            ],
            [
                new WeekdayActivity(DayOfWeek.Monday, 6, 1, 16.7),
                new WeekdayActivity(DayOfWeek.Friday, 6, 6, 100),
            ]);

        var content = RuleBasedRecommendation.Build(snapshot);

        Assert.Equal(RuleBasedRecommendation.ModelName, content.ModelName);
        Assert.Equal("Leer", content.FocusHabit);
        Assert.Contains("lunes", content.Recommendation, StringComparison.Ordinal);
        Assert.Equal(3, content.Recommendation.Split('\n').Length);
        Assert.True(AiRecommendation.Create(Guid.NewGuid(), content, snapshot.From, snapshot.To, Now) is not null);
    }

    [Fact]
    public void Push_device_keeps_the_user_preference()
    {
        var device = PushDevice.Register(Guid.NewGuid(), "device-1", "token-1", recommendationsEnabled: true, Now);

        device.Update("token-2", recommendationsEnabled: false, Now.AddDays(1));

        Assert.Equal("token-2", device.Token);
        Assert.False(device.RecommendationsEnabled);
        Assert.Equal(Now.AddDays(1), device.UpdatedAt);
    }
}
