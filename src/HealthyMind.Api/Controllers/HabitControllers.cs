using HealthyMind.Application.Features.HabitRecords;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Habits;
using Microsoft.AspNetCore.Mvc;

namespace HealthyMind.Api.Controllers;

/// <summary>Hábitos del usuario autenticado (RF02).</summary>
[Route("api/habits")]
public sealed class HabitsController(IHabitUseCases habits, IHabitRecordUseCases records) : ApiControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<HabitResponse>> List(
        [FromQuery] HabitCategory? category,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken) =>
        habits.ListAsync(CurrentUserId, new HabitFilter(category, search, isActive), cancellationToken);

    [HttpGet("{habitId:guid}")]
    [ProducesResponseType<HabitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<HabitResponse> Get(Guid habitId, CancellationToken cancellationToken) =>
        habits.GetAsync(CurrentUserId, habitId, cancellationToken);

    [HttpPost]
    [ProducesResponseType<HabitResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HabitResponse>> Create(HabitRequest request, CancellationToken cancellationToken)
    {
        var habit = await habits.CreateAsync(CurrentUserId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { habitId = habit.Id }, habit);
    }

    [HttpPut("{habitId:guid}")]
    [ProducesResponseType<HabitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<HabitResponse> Update(Guid habitId, HabitRequest request, CancellationToken cancellationToken) =>
        habits.UpdateAsync(CurrentUserId, habitId, request, cancellationToken);

    /// <summary>Activa o pausa el hábito. Pausado no admite registros nuevos.</summary>
    [HttpPatch("{habitId:guid}/status")]
    public Task<HabitResponse> SetStatus(Guid habitId, HabitStatusRequest request, CancellationToken cancellationToken) =>
        habits.SetActiveAsync(CurrentUserId, habitId, request, cancellationToken);

    [HttpDelete("{habitId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid habitId, CancellationToken cancellationToken)
    {
        await habits.DeleteAsync(CurrentUserId, habitId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{habitId:guid}/records")]
    public Task<IReadOnlyList<HabitRecordResponse>> ListRecords(
        Guid habitId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        records.ListByHabitAsync(CurrentUserId, habitId, from, to, cancellationToken);

    [HttpPost("{habitId:guid}/records")]
    [ProducesResponseType<HabitRecordResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<HabitRecordResponse>> CreateRecord(
        Guid habitId,
        CreateHabitRecordRequest request,
        CancellationToken cancellationToken)
    {
        var record = await records.CreateAsync(CurrentUserId, habitId, request, cancellationToken);
        return CreatedAtAction(nameof(RecordsController.Get), "Records", new { recordId = record.Id }, record);
    }

    /// <summary>Crea o actualiza el registro de un día (ideal para el contador de pasos). Fecha: yyyy-MM-dd.</summary>
    [HttpPut("{habitId:guid}/records/{date}")]
    [ProducesResponseType<HabitRecordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<HabitRecordResponse> UpsertRecordForDate(
        Guid habitId,
        DateOnly date,
        HabitProgressRequest request,
        CancellationToken cancellationToken) =>
        records.UpsertForDateAsync(CurrentUserId, habitId, date, request, cancellationToken);
}

/// <summary>Registros de cumplimiento por id (RF03).</summary>
[Route("api/records")]
public sealed class RecordsController(IHabitRecordUseCases records) : ApiControllerBase
{
    [HttpGet("{recordId:guid}")]
    [ProducesResponseType<HabitRecordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<HabitRecordResponse> Get(Guid recordId, CancellationToken cancellationToken) =>
        records.GetAsync(CurrentUserId, recordId, cancellationToken);

    [HttpPut("{recordId:guid}")]
    public Task<HabitRecordResponse> Update(Guid recordId, HabitProgressRequest request, CancellationToken cancellationToken) =>
        records.UpdateAsync(CurrentUserId, recordId, request, cancellationToken);

    [HttpDelete("{recordId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid recordId, CancellationToken cancellationToken)
    {
        await records.DeleteAsync(CurrentUserId, recordId, cancellationToken);
        return NoContent();
    }
}
