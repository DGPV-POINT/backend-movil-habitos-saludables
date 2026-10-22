using FluentValidation;
using HealthyMind.Application.Common;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HealthyMind.Api.Infrastructure;

/// <summary>Traduce las excepciones de aplicación y dominio a respuestas ProblemDetails (RFC 9457).</summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => ValidationProblem(validation),
            DomainException => Problem(StatusCodes.Status422UnprocessableEntity, "Regla de negocio", exception.Message),
            NotFoundException => Problem(StatusCodes.Status404NotFound, "No encontrado", exception.Message),
            ConflictException => Problem(StatusCodes.Status409Conflict, "Conflicto", exception.Message),
            AuthenticationFailedException => Problem(StatusCodes.Status401Unauthorized, "No autenticado", exception.Message),
            ForbiddenException => Problem(StatusCodes.Status403Forbidden, "No permitido", exception.Message),
            AiProviderUnavailableException => Problem(StatusCodes.Status503ServiceUnavailable, "IA no disponible", exception.Message),
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Error no controlado en {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, "Error interno", "Ocurrió un error inesperado.");
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails ValidationProblem(ValidationException exception)
    {
        if (!exception.Errors.Any())
        {
            return Problem(StatusCodes.Status400BadRequest, "Datos inválidos", exception.Message);
        }

        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Datos inválidos",
            Detail = "Revisa los campos marcados.",
        };
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
