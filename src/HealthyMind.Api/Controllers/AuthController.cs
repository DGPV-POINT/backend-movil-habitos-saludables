using HealthyMind.Api.Infrastructure;
using HealthyMind.Application.Features.Auth;
using HealthyMind.Application.Ports.In;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HealthyMind.Api.Controllers;

/// <summary>
/// Flujo del móvil: registro o login con contraseña una sola vez; después, la huella libera el refresh token
/// guardado y se llama a <c>refresh</c>.
/// </summary>
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
public sealed class AuthController(IAuthUseCases auth) : ApiControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await auth.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        auth.LoginAsync(request, cancellationToken);

    /// <summary>Entrega tokens nuevos. El refresh token enviado deja de servir: guarda el que llega.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken cancellationToken) =>
        auth.RefreshAsync(request, cancellationToken);

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
