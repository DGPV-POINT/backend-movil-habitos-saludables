using System.Globalization;
using FluentValidation;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Notifications;
using HealthyMind.Domain.Users;
using Microsoft.Extensions.Logging;

namespace HealthyMind.Application.Features.Notifications;

/// <param name="DeviceId">El mismo que se usa al iniciar sesión.</param>
/// <param name="Token">Token de Firebase Messaging del teléfono.</param>
public sealed record PushTokenRequest(string DeviceId, string Token);

internal sealed class PushTokenRequestValidator : AbstractValidator<PushTokenRequest>
{
    public PushTokenRequestValidator()
    {
        RuleFor(request => request.DeviceId).NotEmpty().MaximumLength(RefreshToken.DeviceIdMaxLength);
        RuleFor(request => request.Token).NotEmpty().MaximumLength(PushDevice.TokenMaxLength);
    }
}

internal sealed class PushUseCases(
    IPushDeviceRepository devices,
    IPushSender sender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<PushTokenRequest> validator,
    ILogger<PushUseCases> logger) : IPushUseCases, IRecommendationPublisher
{
    /// <summary>Tipo de mensaje que la app reconoce.</summary>
    public const string RecommendationType = "recommendation";

    public async Task RegisterAsync(Guid userId, PushTokenRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var deviceId = request.DeviceId.Trim();
        var token = request.Token.Trim();

        // Un token es de un solo teléfono: si antes era de otro usuario, ese usuario deja de recibir avisos ahí.
        foreach (var other in await devices.ListByTokenAsync(token, cancellationToken))
        {
            if (other.UserId != userId || other.DeviceId != deviceId)
            {
                devices.Remove(other);
            }
        }

        var device = await devices.GetAsync(userId, deviceId, cancellationToken);
        if (device is null)
        {
            devices.Add(PushDevice.Register(userId, deviceId, token, now));
        }
        else
        {
            device.UpdateToken(token, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterAsync(Guid userId, string deviceId, CancellationToken cancellationToken)
    {
        var device = await devices.GetAsync(userId, deviceId.Trim(), cancellationToken);
        if (device is not null)
        {
            devices.Remove(device);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task PublishAsync(Guid userId, RecommendationResponse recommendation, CancellationToken cancellationToken)
    {
        if (!sender.IsEnabled)
        {
            return;
        }

        try
        {
            var targets = await devices.ListReachableAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
            if (targets.Count == 0)
            {
                return;
            }

            var message = new PushMessage(new Dictionary<string, string>
            {
                ["type"] = RecommendationType,
                ["id"] = recommendation.Id.ToString(),
                ["title"] = recommendation.Title,
                ["summary"] = recommendation.Summary,
                ["createdAt"] = recommendation.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            });

            var invalid = await sender.SendAsync(targets.Select(device => device.Token).ToList(), message, cancellationToken);
            if (invalid.Count > 0)
            {
                foreach (var device in targets.Where(device => invalid.Contains(device.Token)))
                {
                    devices.Remove(device);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // El aviso es un extra: la recomendación ya quedó guardada y la app la trae al sincronizar.
            unitOfWork.DiscardChanges();
            logger.LogWarning(exception, "No se pudo enviar el aviso push al usuario {UserId}.", userId);
        }
    }
}
