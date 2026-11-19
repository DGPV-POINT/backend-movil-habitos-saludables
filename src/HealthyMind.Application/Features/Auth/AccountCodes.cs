using System.Net;
using System.Security.Cryptography;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Auth;

/// <summary>Emite y comprueba los códigos de 6 dígitos que llegan por correo.</summary>
internal sealed class AccountCodes(
    IUserCodeRepository codes,
    ISecureTokenGenerator tokenGenerator,
    IEmailSender email,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Invalida los anteriores y envía uno nuevo. Si se pidió hace menos de un minuto no envía nada
    /// (el anterior sigue sirviendo). Hay que grabar después.
    /// </summary>
    public async Task SendAsync(User user, UserCodePurpose purpose, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var latest = await codes.GetLatestAsync(user.Id, purpose, cancellationToken);
        if (latest is not null && latest.IsUsable(now) && !latest.CanResend(now))
        {
            return;
        }

        foreach (var old in await codes.ListUnusedAsync(user.Id, purpose, cancellationToken))
        {
            old.Invalidate(now);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        codes.Add(UserCode.Issue(user.Id, purpose, HashOf(user, code), now));
        await email.SendAsync(Compose(user, purpose, code), cancellationToken);
    }

    /// <summary>Gasta un intento si el código no coincide. Hay que grabar después, acierte o no.</summary>
    public async Task<bool> TryUseAsync(User user, UserCodePurpose purpose, string code, CancellationToken cancellationToken)
    {
        var latest = await codes.GetLatestAsync(user.Id, purpose, cancellationToken);
        return latest is not null && latest.TryUse(HashOf(user, code.Trim()), timeProvider.GetUtcNow());
    }

    // El id del usuario va en el hash: el mismo código de dos personas no da el mismo hash.
    private string HashOf(User user, string code) => tokenGenerator.Hash($"{user.Id:N}:{code}");

    private static EmailMessage Compose(User user, UserCodePurpose purpose, string code)
    {
        var (subject, intro) = purpose switch
        {
            UserCodePurpose.VerifyEmail => ("Confirma tu correo en HealthyMindIA", "Usa este código para activar tu cuenta:"),
            _ => ("Recupera tu contraseña de HealthyMindIA", "Usa este código para poner una contraseña nueva:"),
        };

        var minutes = (int)UserCode.Lifetime.TotalMinutes;
        var name = WebUtility.HtmlEncode(user.FirstName);
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;color:#1f2937">
              <h2 style="color:#0f766e">HealthyMindIA</h2>
              <p>Hola {name},</p>
              <p>{intro}</p>
              <p style="font-size:32px;font-weight:bold;letter-spacing:8px;background:#f0fdfa;padding:16px;text-align:center;border-radius:8px">{code}</p>
              <p>Vence en {minutes} minutos y sirve una sola vez.</p>
              <p style="color:#6b7280;font-size:13px">Si no fuiste tú, ignora este mensaje: nadie puede entrar a tu cuenta sin este código.</p>
            </div>
            """;
        var text = $"Hola {user.FirstName},\n\n{intro} {code}\n\nVence en {minutes} minutos. Si no fuiste tú, ignora este mensaje.";
        return new EmailMessage(user.Email, subject, html, text);
    }
}
