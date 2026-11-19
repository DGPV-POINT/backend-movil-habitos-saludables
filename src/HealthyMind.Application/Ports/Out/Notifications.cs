using HealthyMind.Domain.Notifications;

namespace HealthyMind.Application.Ports.Out;

public interface IPushDeviceRepository
{
    Task<PushDevice?> GetAsync(Guid userId, string deviceId, CancellationToken cancellationToken);

    /// <summary>Registros con el mismo token (el teléfono cambió de usuario).</summary>
    Task<IReadOnlyList<PushDevice>> ListByTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>Solo dispositivos con una sesión activa: cerrar sesión o revocarla corta los avisos.</summary>
    Task<IReadOnlyList<PushDevice>> ListReachableAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(PushDevice device);

    void Remove(PushDevice device);
}

/// <summary>Mensaje de datos: la app arma la notificación (respeta su canal y la privacidad en pantalla bloqueada).</summary>
public sealed record PushMessage(IReadOnlyDictionary<string, string> Data);

/// <summary>Envía avisos push (hoy: Firebase Cloud Messaging).</summary>
public interface IPushSender
{
    /// <summary>false si el servidor no tiene credenciales de Firebase: no se envía nada.</summary>
    bool IsEnabled { get; }

    /// <returns>Los tokens que Firebase ya no reconoce (app desinstalada): hay que borrarlos.</returns>
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken);
}
