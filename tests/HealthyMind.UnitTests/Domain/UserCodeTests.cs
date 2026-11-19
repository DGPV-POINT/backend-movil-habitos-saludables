using HealthyMind.Domain.Users;

namespace HealthyMind.UnitTests.Domain;

public sealed class UserCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    private static UserCode NewCode() => UserCode.Issue(Guid.NewGuid(), UserCodePurpose.VerifyEmail, "HASH", Now);

    [Fact]
    public void A_code_works_once()
    {
        var code = NewCode();

        Assert.True(code.TryUse("HASH", Now.AddMinutes(1)));
        Assert.False(code.TryUse("HASH", Now.AddMinutes(2)));
    }

    [Fact]
    public void A_code_expires()
    {
        Assert.False(NewCode().TryUse("HASH", Now.Add(UserCode.Lifetime).AddSeconds(1)));
    }

    [Fact]
    public void Too_many_wrong_attempts_burn_the_code()
    {
        var code = NewCode();
        for (var i = 0; i < UserCode.MaxAttempts; i++)
        {
            Assert.False(code.TryUse("OTRO", Now));
        }

        Assert.False(code.TryUse("HASH", Now)); // ni el correcto sirve ya
    }

    [Fact]
    public void Resend_waits_a_minute()
    {
        var code = NewCode();

        Assert.False(code.CanResend(Now.AddSeconds(30)));
        Assert.True(code.CanResend(Now.Add(UserCode.ResendCooldown)));
    }

    [Fact]
    public void Resetting_the_password_unlocks_and_confirms_the_email()
    {
        var user = User.Register("ana.p", "ana@correo.com", "Ana", "Pérez", "hash", UserRole.User, null, Now);
        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        Assert.True(user.IsLockedOut(Now));
        Assert.False(user.IsEmailVerified);

        user.ResetPassword("nuevo-hash", Now);

        Assert.False(user.IsLockedOut(Now));
        Assert.True(user.IsEmailVerified);
        Assert.Equal("nuevo-hash", user.PasswordHash);
    }
}
