using HealthyMind.Domain.Common;

namespace HealthyMind.Domain.Users;

/// <summary>
/// Sesión de un dispositivo. Es lo que permite "iniciar sesión una vez y luego entrar con huella":
/// el móvil guarda el token en almacenamiento seguro, la huella lo libera y con él pide un access token nuevo.
/// </summary>
/// <remarks>
/// Solo se guarda el hash del token. Cada uso lo rota: si alguien presenta un token ya rotado,
/// es señal de robo y se cierran todas las sesiones de ese dispositivo.
/// </remarks>
public sealed class RefreshToken : Entity
{
    public const int DeviceIdMaxLength = 100;
    public const int DeviceNameMaxLength = 100;
    public const int ReasonMaxLength = 100;

    private RefreshToken()
    {
    }

    private RefreshToken(Guid id) : base(id)
    {
    }

    public Guid UserId { get; private init; }

    public string TokenHash { get; private init; } = null!;

    public string DeviceId { get; private init; } = null!;

    public string? DeviceName { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevocationReason { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public bool WasRotated => ReplacedByTokenId is not null;

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        string deviceId,
        string? deviceName,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        Guard.NotEmpty(userId, "usuario");

        return new RefreshToken(Guid.CreateVersion7())
        {
            UserId = userId,
            TokenHash = Guard.Text(tokenHash, "token", 128),
            DeviceId = Guard.Text(deviceId, "dispositivo", DeviceIdMaxLength),
            DeviceName = Guard.OptionalText(deviceName, "nombre del dispositivo", DeviceNameMaxLength),
            CreatedAt = now.ToUniversalTime(),
            ExpiresAt = now.ToUniversalTime().Add(lifetime),
        };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public bool BelongsToDevice(string deviceId) =>
        string.Equals(DeviceId, deviceId.Trim(), StringComparison.Ordinal);

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now.ToUniversalTime();
        RevocationReason = reason.Length > ReasonMaxLength ? reason[..ReasonMaxLength] : reason;
    }

    public void RotateTo(RefreshToken next, DateTimeOffset now)
    {
        Revoke(now, RevocationReasons.Rotated);
        ReplacedByTokenId = next.Id;
    }
}

public static class RevocationReasons
{
    public const string Rotated = "Rotado";
    public const string Logout = "Cierre de sesión";
    public const string LogoutAll = "Cierre de todas las sesiones";
    public const string NewLoginOnDevice = "Nuevo inicio de sesión en el dispositivo";
    public const string ReuseDetected = "Reutilización de token detectada";
    public const string PasswordChanged = "Cambio de contraseña";
    public const string AccountSuspended = "Cuenta suspendida";
    public const string RevokedByUser = "Revocada por el usuario";
}
