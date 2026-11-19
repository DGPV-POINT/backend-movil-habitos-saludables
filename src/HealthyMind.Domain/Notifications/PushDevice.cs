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

    /// <summary>El usuario quiere los avisos de la IA en este teléfono (lo elige en Perfil).</summary>
    public bool RecommendationsEnabled { get; private set; } = true;

    public DateTimeOffset UpdatedAt { get; private set; }

    public static PushDevice Register(Guid userId, string deviceId, string token, bool recommendationsEnabled, DateTimeOffset now)
    {
        Guard.NotEmpty(userId, "usuario");
        var device = new PushDevice(Guid.CreateVersion7())
        {
            UserId = userId,
            DeviceId = Guard.Text(deviceId, "dispositivo", RefreshToken.DeviceIdMaxLength),
        };
        device.Update(token, recommendationsEnabled, now);
        return device;
    }

    /// <summary>Firebase renueva el token de vez en cuando, y el usuario puede cambiar su preferencia.</summary>
    public void Update(string token, bool recommendationsEnabled, DateTimeOffset now)
    {
        Token = Guard.Text(token, "token de notificaciones", TokenMaxLength);
        RecommendationsEnabled = recommendationsEnabled;
        UpdatedAt = now.ToUniversalTime();
    }
}
