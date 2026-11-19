using HealthyMind.Application.Features.Notifications;
using HealthyMind.Application.Features.Recommendations;

namespace HealthyMind.Application.Ports.In;

/// <summary>Registro del teléfono para recibir avisos push.</summary>
public interface IPushUseCases
{
    Task RegisterAsync(Guid userId, PushTokenRequest request, CancellationToken cancellationToken);

    Task UnregisterAsync(Guid userId, string deviceId, CancellationToken cancellationToken);
}

/// <summary>Avisa al usuario que hay una recomendación nueva. Nunca falla.</summary>
public interface IRecommendationPublisher
{
    Task PublishAsync(Guid userId, RecommendationResponse recommendation, CancellationToken cancellationToken);
}
