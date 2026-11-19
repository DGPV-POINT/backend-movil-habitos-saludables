using System.Text;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using HealthyMind.Application.Ports.Out;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthyMind.Infrastructure.Notifications;

/// <summary>
/// Credenciales de la cuenta de servicio de Firebase. Van en base64 (una sola línea en el .env);
/// vacías = sin avisos push (la app sigue avisando con su revisión periódica).
/// </summary>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string CredentialsBase64 { get; set; } = string.Empty;
}

/// <summary>Envía mensajes de datos con prioridad alta por Firebase Cloud Messaging (API HTTP v1).</summary>
internal sealed class FirebasePushSender : IPushSender
{
    private const int MaxTokensPerCall = 500;

    private readonly FirebaseMessaging? _messaging;
    private readonly ILogger<FirebasePushSender> _logger;

    public FirebasePushSender(IOptions<FirebaseOptions> options, ILogger<FirebasePushSender> logger)
    {
        _logger = logger;
        var encoded = options.Value.CredentialsBase64.Trim();
        if (encoded.Length == 0)
        {
            logger.LogInformation("Firebase sin configurar: los avisos push están apagados.");
            return;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var app = FirebaseApp.GetInstance("healthymind") ?? FirebaseApp.Create(
                new AppOptions { Credential = CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential() },
                "healthymind");
            _messaging = FirebaseMessaging.GetMessaging(app);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException)
        {
            // Credencial mal pegada: la API arranca igual, sin push.
            logger.LogError(exception, "Las credenciales de Firebase no son válidas: los avisos push están apagados.");
        }
    }

    public bool IsEnabled => _messaging is not null;

    public async Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyList<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        if (_messaging is null || tokens.Count == 0)
        {
            return [];
        }

        var invalid = new List<string>();
        foreach (var chunk in tokens.Chunk(MaxTokensPerCall))
        {
            // El SDK sugiere los FID nuevos, pero Firebase Messaging en Android sigue entregando tokens de registro
            // y la API HTTP v1 los acepta: se usan esos.
#pragma warning disable CS0618
            var multicast = new MulticastMessage
            {
                Tokens = chunk,
#pragma warning restore CS0618
                Data = message.Data,
                // Alta prioridad: Android despierta la app aunque esté en reposo para que arme el aviso.
                Android = new AndroidConfig { Priority = Priority.High, TimeToLive = TimeSpan.FromDays(1) },
            };

            var result = await _messaging.SendEachForMulticastAsync(multicast, cancellationToken);
            for (var index = 0; index < result.Responses.Count; index++)
            {
                var response = result.Responses[index];
                if (response.IsSuccess)
                {
                    continue;
                }

                if (response.Exception?.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                {
                    invalid.Add(chunk[index]);
                }
                else
                {
                    _logger.LogWarning(response.Exception, "Firebase no entregó un aviso push.");
                }
            }
        }

        return invalid;
    }

    public async Task SendToTopicAsync(string topic, PushMessage message, CancellationToken cancellationToken)
    {
        if (_messaging is null)
        {
            return;
        }

        await _messaging.SendAsync(
            new Message
            {
                Topic = topic,
                Data = message.Data,
                Android = new AndroidConfig { Priority = Priority.High, TimeToLive = TimeSpan.FromDays(1) },
            },
            cancellationToken);
    }
}
