using System.Globalization;
using FluentValidation;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Notifications;
using HealthyMind.Domain.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthyMind.Application.Features.Notifications;

/// <param name="DeviceId">El mismo que se usa al iniciar sesión.</param>
/// <param name="Token">Token de Firebase Messaging del teléfono.</param>
/// <param name="RecommendationsEnabled">El usuario quiere los avisos de la IA en este teléfono.</param>
public sealed record PushTokenRequest(string DeviceId, string Token, bool RecommendationsEnabled = true);

/// <summary>Anuncio del administrador para todos los teléfonos (p. ej. "Mantenimiento esta noche").</summary>
public sealed record AnnouncementRequest(string Title, string Body);

internal sealed class PushTokenRequestValidator : AbstractValidator<PushTokenRequest>
{
    public PushTokenRequestValidator()
    {
        RuleFor(request => request.DeviceId).NotEmpty().MaximumLength(RefreshToken.DeviceIdMaxLength);
        RuleFor(request => request.Token).NotEmpty().MaximumLength(PushDevice.TokenMaxLength);
    }
}

internal sealed class AnnouncementRequestValidator : AbstractValidator<AnnouncementRequest>
{
    public AnnouncementRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(PushUseCases.AnnouncementTitleMaxLength);
        RuleFor(request => request.Body).NotEmpty().MaximumLength(PushUseCases.AnnouncementBodyMaxLength);
    }
}

/// <summary>Lo que la app reconoce en el campo "type" de cada push, y el tema de los anuncios.</summary>
public static class PushContract
{
    public const string RecommendationType = "recommendation";
    public const string AnnouncementType = "announcement";

    /// <summary>Tema de Firebase al que se suscriben todos los teléfonos.</summary>
    public const string AnnouncementsTopic = "anuncios";
}

public sealed class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>Tokens sin actualizar en estos días se borran (igual que la duración de la sesión).</summary>
    public int StaleAfterDays { get; set; } = 60;
}

internal sealed class PushUseCases(
    IPushDeviceRepository devices,
    IPushSender sender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<PushTokenRequest> validator,
    IValidator<AnnouncementRequest> announcementValidator,
    IOptions<PushOptions> options,
    ILogger<PushUseCases> logger) : IPushUseCases, IRecommendationPublisher
{
    private const string RecommendationType = PushContract.RecommendationType;
    private const string AnnouncementType = PushContract.AnnouncementType;
    private const string AnnouncementsTopic = PushContract.AnnouncementsTopic;

    public const int AnnouncementTitleMaxLength = 80;
    public const int AnnouncementBodyMaxLength = 300;

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
            devices.Add(PushDevice.Register(userId, deviceId, token, request.RecommendationsEnabled, now));
        }
        else
        {
            device.Update(token, request.RecommendationsEnabled, now);
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

    public async Task AnnounceAsync(AnnouncementRequest request, CancellationToken cancellationToken)
    {
        await announcementValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!sender.IsEnabled)
        {
            throw new Common.ConflictException("Este servidor no tiene configurado Firebase: no puede enviar anuncios.");
        }

        await sender.SendToTopicAsync(
            AnnouncementsTopic,
            new PushMessage(new Dictionary<string, string>
            {
                ["type"] = AnnouncementType,
                ["title"] = request.Title.Trim(),
                ["body"] = request.Body.Trim(),
            }),
            cancellationToken);
    }

    public async Task<int> RemoveStaleDevicesAsync(CancellationToken cancellationToken) =>
        await devices.DeleteStaleAsync(timeProvider.GetUtcNow().AddDays(-options.Value.StaleAfterDays), cancellationToken);

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
