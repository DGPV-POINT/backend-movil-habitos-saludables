using HealthyMind.Application.Features.Sync;

namespace HealthyMind.Application.Ports.In;

/// <summary>Sincronización del móvil sin conexión (RNF05).</summary>
public interface ISyncUseCases
{
    /// <summary>
    /// Aplica lo pendiente del dispositivo. Cada elemento se resuelve por separado:
    /// uno rechazado no impide que los demás se apliquen.
    /// </summary>
    Task<SyncPushResponse> PushAsync(Guid userId, SyncPushRequest request, CancellationToken cancellationToken);

    /// <summary>Lo que cambió en el servidor desde <paramref name="since"/>; sin él, todo lo vigente.</summary>
    Task<SyncPullResponse> PullAsync(Guid userId, DateTimeOffset? since, CancellationToken cancellationToken);
}
