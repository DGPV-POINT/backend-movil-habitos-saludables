using HealthyMind.Domain.Common;

namespace HealthyMind.Domain.Users;

public enum UserCodePurpose
{
    /// <summary>Confirmar el correo al crear la cuenta.</summary>
    VerifyEmail = 1,

    /// <summary>Poner una contraseña nueva cuando se olvidó la anterior.</summary>
    ResetPassword = 2,
}

/// <summary>
/// Código de 6 dígitos enviado por correo. Solo se guarda su hash; vence pronto,
/// admite pocos intentos y sirve una sola vez.
/// </summary>
public sealed class UserCode : Entity
{
    public const int MaxAttempts = 5;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>Espera mínima para pedir otro código (evita llenar el buzón de alguien).</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private UserCode()
    {
    }

    private UserCode(Guid id) : base(id)
    {
    }

    public Guid UserId { get; private init; }

    public UserCodePurpose Purpose { get; private init; }

    public string CodeHash { get; private init; } = null!;

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public int Attempts { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static UserCode Issue(Guid userId, UserCodePurpose purpose, string codeHash, DateTimeOffset now)
    {
        Guard.NotEmpty(userId, "usuario");
        var created = now.ToUniversalTime();

        return new UserCode(Guid.CreateVersion7())
        {
            UserId = userId,
            Purpose = purpose,
            CodeHash = Guard.Text(codeHash, "código", 128),
            CreatedAt = created,
            ExpiresAt = created.Add(Lifetime),
        };
    }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && Attempts < MaxAttempts && ExpiresAt > now;

    public bool CanResend(DateTimeOffset now) => CreatedAt.Add(ResendCooldown) <= now;

    /// <summary>Compara el hash; un fallo gasta un intento y el acierto lo deja usado.</summary>
    public bool TryUse(string codeHash, DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            return false;
        }

        if (!string.Equals(CodeHash, codeHash, StringComparison.Ordinal))
        {
            Attempts++;
            return false;
        }

        UsedAt = now.ToUniversalTime();
        return true;
    }

    /// <summary>Al emitir uno nuevo, los anteriores dejan de servir.</summary>
    public void Invalidate(DateTimeOffset now) => UsedAt ??= now.ToUniversalTime();
}
