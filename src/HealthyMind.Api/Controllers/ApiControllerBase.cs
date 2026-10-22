using HealthyMind.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthyMind.Api.Controllers;

/// <summary>Adaptador de entrada HTTP: solo traduce peticiones a casos de uso, sin lógica de negocio.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId => User.GetUserId();
}
