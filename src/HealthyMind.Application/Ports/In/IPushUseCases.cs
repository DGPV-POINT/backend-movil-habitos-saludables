using HealthyMind.Application.Features.Notifications;
using HealthyMind.Application.Features.Recommendations;

namespace HealthyMind.Application.Ports.In;

/// <summary>Registro del teléfono para recibir avisos push, y anuncios del administrador.</summary>
public interface IPushUseCases
{
    Task RegisterAsync(Guid userId, PushTokenRequest request, CancellationToken cancellationToken);

    Task UnregisterAsync(Guid userId, string deviceId, CancellationToken cancellationToken);

    /// <summary>Aviso a todos los teléfonos (tema "anuncios"). Solo administradores.</summary>
    Task AnnounceAsync(AnnouncementRequest request, CancellationToken cancellationToken);

    /// <summary>Limpieza periódica de tokens abandonados.</summary>
    Task<int> RemoveStaleDevicesAsync(CancellationToken cancellationToken);
}

/// <summary>Avisa al usuario que hay una recomendación nueva. Nunca falla.</summary>
public interface IRecommendationPublisher
{
    Task PublishAsync(Guid userId, RecommendationResponse recommendation, CancellationToken cancellationToken);
}
