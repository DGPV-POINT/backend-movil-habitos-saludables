using HealthyMind.Domain.Common;
using HealthyMind.Domain.Users;

namespace HealthyMind.Domain.Notifications;

/// <summary>
/// Token de Firebase (FCM) de un teléfono, para avisarle aunque la app esté cerrada.
/// Uno por usuario y dispositivo; solo se usa mientras ese dispositivo tenga una sesión activa.
/// </summary>
public sealed class PushDevice : Entity
{
    public const int TokenMaxLength = 4096;

    private PushDevice()
    {
    }

    private PushDevice(Guid id) : base(id)
    {
    }

    public Guid UserId { get; private init; }

    public string DeviceId { get; private init; } = null!;

    public string Token { get; private set; } = null!;

    public DateTimeOffset UpdatedAt { get; private set; }

    public static PushDevice Register(Guid userId, string deviceId, string token, DateTimeOffset now)
    {
        Guard.NotEmpty(userId, "usuario");
        return new PushDevice(Guid.CreateVersion7())
        {
            UserId = userId,
            DeviceId = Guard.Text(deviceId, "dispositivo", RefreshToken.DeviceIdMaxLength),
            Token = Guard.Text(token, "token de notificaciones", TokenMaxLength),
            UpdatedAt = now.ToUniversalTime(),
        };
    }

    /// <summary>Firebase renueva el token de vez en cuando: se reemplaza.</summary>
    public void UpdateToken(string token, DateTimeOffset now)
    {
        Token = Guard.Text(token, "token de notificaciones", TokenMaxLength);
        UpdatedAt = now.ToUniversalTime();
    }
}
