using HealthyMind.Application.Features.Notifications;
using HealthyMind.Application.Ports.In;
using Microsoft.AspNetCore.Mvc;

namespace HealthyMind.Api.Controllers;

/// <summary>Avisos push: el teléfono registra su token de Firebase después de iniciar sesión.</summary>
[Route("api/push/devices")]
public sealed class PushController(IPushUseCases push) : ApiControllerBase
{
    /// <summary>Registra o actualiza el token de este dispositivo.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(PushTokenRequest request, CancellationToken cancellationToken)
    {
        await push.RegisterAsync(CurrentUserId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Deja de enviar avisos a ese dispositivo (al cerrar sesión).</summary>
    [HttpDelete("{deviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unregister(string deviceId, CancellationToken cancellationToken)
    {
        await push.UnregisterAsync(CurrentUserId, deviceId, cancellationToken);
        return NoContent();
    }
}
