using HealthyMind.Domain.Common;
using HealthyMind.Domain.Users;

namespace HealthyMind.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 15, 0, 0, TimeSpan.Zero);

    private static User NewUser(string? timeZone = null) =>
        User.Register("Martin.G", " Martin@Correo.COM ", "Martín", "Guerrón", "hash", UserRole.User, timeZone, Now);

    [Fact]
    public void Username_and_email_are_normalized()
    {
        var user = NewUser();

        Assert.Equal("Martin.G", user.Username);
        Assert.Equal("martin.g", user.NormalizedUsername);
        Assert.Equal("martin@correo.com", user.Email);
        Assert.Equal(User.DefaultTimeZone, user.TimeZoneId);
    }

    [Fact]
    public void Account_locks_after_too_many_failed_logins_and_unlocks_later()
    {
        var user = NewUser();

        for (var attempt = 0; attempt < User.MaxFailedLoginAttempts; attempt++)
        {
            user.RegisterFailedLogin(Now);
        }

        Assert.True(user.IsLockedOut(Now.AddMinutes(1)));
        Assert.False(user.IsLockedOut(Now.Add(User.LockoutDuration).AddSeconds(1)));
    }

    [Fact]
    public void Suspend_and_reactivate_change_status()
    {
        var user = NewUser();

        user.Suspend(Now);
        Assert.False(user.IsActive);
        Assert.Throws<DomainException>(() => user.Suspend(Now));

        user.Reactivate(Now);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Local_date_uses_the_user_time_zone()
    {
        var user = NewUser("America/Guayaquil"); // UTC-5

        // 02:00 UTC del 23 todavía es 22 en Ecuador.
        Assert.Equal(new DateOnly(2026, 10, 22), user.LocalDate(new DateTimeOffset(2026, 10, 23, 2, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Unknown_time_zone_is_rejected() =>
        Assert.Throws<DomainException>(() => NewUser("Marte/Base"));

    [Fact]
    public void Rotated_refresh_token_is_inactive_and_points_to_the_next_one()
    {
        var userId = Guid.CreateVersion7();
        var current = RefreshToken.Issue(userId, "hash-1", "device-1", "Pixel", Now, TimeSpan.FromDays(60));
        var next = RefreshToken.Issue(userId, "hash-2", "device-1", "Pixel", Now, TimeSpan.FromDays(60));

        current.RotateTo(next, Now.AddMinutes(1));

        Assert.False(current.IsActive(Now.AddMinutes(2)));
        Assert.True(current.WasRotated);
        Assert.Equal(next.Id, current.ReplacedByTokenId);
        Assert.True(next.IsActive(Now.AddDays(59)));
        Assert.False(next.IsActive(Now.AddDays(61)));
    }
}
