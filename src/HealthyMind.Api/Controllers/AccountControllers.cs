using HealthyMind.Api.Infrastructure;
using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Auth;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Features.Users;
using HealthyMind.Application.Ports.In;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthyMind.Api.Controllers;

/// <summary>Dispositivos con sesión abierta del usuario.</summary>
[Route("api/auth/sessions")]
public sealed class SessionsController(ISessionUseCases sessions) : ApiControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SessionResponse>> List(CancellationToken cancellationToken) =>
        sessions.ListAsync(CurrentUserId, cancellationToken);

    [HttpDelete("{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid sessionId, CancellationToken cancellationToken)
    {
        await sessions.RevokeAsync(CurrentUserId, sessionId, cancellationToken);
        return NoContent();
    }

    /// <summary>Cierra la sesión en todos los dispositivos.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeAll(CancellationToken cancellationToken)
    {
        await sessions.RevokeAllAsync(CurrentUserId, cancellationToken);
        return NoContent();
    }
}

[Route("api/profile")]
public sealed class ProfileController(IProfileUseCases profile) : ApiControllerBase
{
    [HttpGet]
    public Task<UserProfileResponse> Get(CancellationToken cancellationToken) =>
        profile.GetAsync(CurrentUserId, cancellationToken);

    [HttpPut]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<UserProfileResponse> Update(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        profile.UpdateAsync(CurrentUserId, request, cancellationToken);

    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await profile.ChangePasswordAsync(CurrentUserId, request, cancellationToken);
        return NoContent();
    }
}

/// <summary>Administración de cuentas (RF01): solo rol Admin.</summary>
[Route("api/admin/users")]
[Authorize(Policy = AuthenticationSetup.AdminPolicy)]
public sealed class AdminUsersController(IUserAdministrationUseCases administration) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserProfileResponse>> Search(
        [FromQuery] UserSearchRequest request,
        CancellationToken cancellationToken) =>
        administration.SearchAsync(request, cancellationToken);

    [HttpGet("{userId:guid}")]
    public Task<UserProfileResponse> Get(Guid userId, CancellationToken cancellationToken) =>
        administration.GetAsync(userId, cancellationToken);

    [HttpPost]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await administration.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { userId = user.Id }, user);
    }

    [HttpPatch("{userId:guid}/suspend")]
    public Task<UserProfileResponse> Suspend(Guid userId, CancellationToken cancellationToken) =>
        administration.SuspendAsync(CurrentUserId, userId, cancellationToken);

    [HttpPatch("{userId:guid}/reactivate")]
    public Task<UserProfileResponse> Reactivate(Guid userId, CancellationToken cancellationToken) =>
        administration.ReactivateAsync(userId, cancellationToken);

    /// <summary>Da o quita el rol de administrador. Cierra las sesiones de esa persona para aplicar el cambio.</summary>
    [HttpPatch("{userId:guid}/role")]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<UserProfileResponse> ChangeRole(Guid userId, ChangeRoleRequest request, CancellationToken cancellationToken) =>
        administration.ChangeRoleAsync(CurrentUserId, userId, request, cancellationToken);
}

/// <summary>Tablero del administrador: cifras generales de uso de la plataforma.</summary>
[Route("api/admin/dashboard")]
[Authorize(Policy = AuthenticationSetup.AdminPolicy)]
public sealed class AdminDashboardController(IUserAdministrationUseCases administration) : ApiControllerBase
{
    [HttpGet]
    public Task<AdminDashboardResponse> Get(CancellationToken cancellationToken) =>
        administration.GetDashboardAsync(CurrentUserId, cancellationToken);
}

/// <summary>Disparo manual de la IA, útil para probar la API key sin esperar al proceso automático.</summary>
[Route("api/admin/recommendations")]
[Authorize(Policy = AuthenticationSetup.AdminPolicy)]
public sealed class AdminRecommendationsController(IRecommendationGenerationUseCases generation) : ApiControllerBase
{
    /// <summary>Corre ahora la generación para todos los usuarios a los que ya les toca.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<object>> RunDue(CancellationToken cancellationToken) =>
        Ok(new { generated = await generation.GenerateDueAsync(cancellationToken) });

    /// <summary>Genera ya una recomendación para un usuario, aunque no le toque.</summary>
    [HttpPost("users/{userId:guid}")]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<RecommendationResponse>> GenerateForUser(Guid userId, CancellationToken cancellationToken)
    {
        var recommendation = await generation.GenerateForUserAsync(userId, cancellationToken);
        return recommendation is null ? NoContent() : Ok(recommendation);
    }
}
