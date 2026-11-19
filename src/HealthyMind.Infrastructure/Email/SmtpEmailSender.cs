using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using HealthyMind.Application.Ports.Out;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthyMind.Infrastructure.Email;

/// <summary>
/// Servidor de correo saliente. Las credenciales llegan por variables de entorno
/// (<c>Email__SmtpPassword</c>), nunca en appsettings.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Vacío = no se envía nada; el código queda en el log (solo para desarrollo).</summary>
    public string SmtpHost { get; set; } = "";

    public int SmtpPort { get; set; } = 587;

    /// <summary>STARTTLS en el 587 (Gmail, Outlook, Brevo, etc.).</summary>
    public bool EnableSsl { get; set; } = true;

    public string SmtpUser { get; set; } = "";

    public string SmtpPassword { get; set; } = "";

    /// <summary>Remitente; si se deja vacío se usa <see cref="SmtpUser"/>.</summary>
    public string From { get; set; } = "";

    public string FromName { get; set; } = "HealthyMindIA";

    public int TimeoutSeconds { get; set; } = 20;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            // Sin SMTP (desarrollo): se registra el texto para poder seguir el flujo. En producción hay que configurarlo.
            logger.LogWarning(
                "Email:SmtpHost vacío; no se envió el correo «{Subject}» a {To}. Contenido: {Text}",
                message.Subject,
                message.To,
                message.TextBody);
            return;
        }

        var from = string.IsNullOrWhiteSpace(settings.From) ? settings.SmtpUser : settings.From;
        using var mail = new MailMessage
        {
            From = new MailAddress(from, settings.FromName),
            Subject = message.Subject,
            SubjectEncoding = System.Text.Encoding.UTF8,
            BodyEncoding = System.Text.Encoding.UTF8,
            Body = message.TextBody,
            IsBodyHtml = false,
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, System.Text.Encoding.UTF8, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = settings.TimeoutSeconds * 1000,
        };

        if (!string.IsNullOrWhiteSpace(settings.SmtpUser))
        {
            client.Credentials = new NetworkCredential(settings.SmtpUser, settings.SmtpPassword);
        }

        try
        {
            await client.SendMailAsync(mail, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            logger.LogError(exception, "No se pudo enviar el correo «{Subject}».", message.Subject);
            throw new EmailDeliveryException();
        }
    }
}
