using HealthyMind.Application.Features.HabitRecords;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Ports.Out;

namespace HealthyMind.Application.Ports.In;

/// <summary>Gestión de hábitos (RF02).</summary>
public interface IHabitUseCases
{
    Task<IReadOnlyList<HabitResponse>> ListAsync(Guid userId, HabitFilter filter, CancellationToken cancellationToken);

    Task<HabitResponse> GetAsync(Guid userId, Guid habitId, CancellationToken cancellationToken);

    Task<HabitResponse> CreateAsync(Guid userId, HabitRequest request, CancellationToken cancellationToken);

    Task<HabitResponse> UpdateAsync(Guid userId, Guid habitId, HabitRequest request, CancellationToken cancellationToken);

    Task<HabitResponse> SetActiveAsync(
        Guid userId,
        Guid habitId,
        HabitStatusRequest request,
        CancellationToken cancellationToken);

    /// <summary>Baja lógica del hábito y de sus registros.</summary>
    Task DeleteAsync(Guid userId, Guid habitId, CancellationToken cancellationToken);
}

/// <summary>Registro diario de cumplimiento (RF03).</summary>
public interface IHabitRecordUseCases
{
    Task<IReadOnlyList<HabitRecordResponse>> ListByHabitAsync(
        Guid userId,
        Guid habitId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);

    Task<HabitRecordResponse> GetAsync(Guid userId, Guid recordId, CancellationToken cancellationToken);

    Task<HabitRecordResponse> CreateAsync(
        Guid userId,
        Guid habitId,
        CreateHabitRecordRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Crea o actualiza el registro del día. Pensado para el contador de pasos, que reporta varias veces al día.
    /// </summary>
    Task<HabitRecordResponse> UpsertForDateAsync(
        Guid userId,
        Guid habitId,
        DateOnly date,
        HabitProgressRequest request,
        CancellationToken cancellationToken);

    Task<HabitRecordResponse> UpdateAsync(
        Guid userId,
        Guid recordId,
        HabitProgressRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid recordId, CancellationToken cancellationToken);
}
