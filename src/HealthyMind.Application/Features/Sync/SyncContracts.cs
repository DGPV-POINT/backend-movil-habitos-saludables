using FluentValidation;
using HealthyMind.Application.Features.HabitRecords;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Domain.Habits;

namespace HealthyMind.Application.Features.Sync;

/// <summary>
/// Cambios que el móvil acumuló en SQLite mientras no tenía internet.
/// Se procesan hábitos primero y luego registros, porque un registro puede apuntar a un hábito creado sin conexión.
/// </summary>
public sealed record SyncPushRequest(
    string DeviceId,
    IReadOnlyList<HabitChange>? Habits,
    IReadOnlyList<HabitRecordChange>? Records);

/// <param name="UpdatedAt">Cuándo se hizo el cambio en el dispositivo; decide quién gana en un conflicto.</param>
public sealed record HabitChange(
    Guid Id,
    string Name,
    HabitCategory Category,
    int Goal,
    string Unit,
    IReadOnlyList<DayOfWeek>? ScheduledDays,
    TrackingMode TrackingMode,
    bool IsActive,
    bool IsDeleted,
    DateTimeOffset UpdatedAt)
{
    internal HabitDefinition ToDefinition() =>
        new(Name, Category, Goal, Unit, ScheduledDays.ToWeekDays(), TrackingMode);
}

/// <inheritdoc cref="HabitChange"/>
public sealed record HabitRecordChange(
    Guid Id,
    Guid HabitId,
    DateOnly RecordDate,
    int CompletedValue,
    string? Notes,
    double? Latitude,
    double? Longitude,
    bool IsDeleted,
    DateTimeOffset UpdatedAt)
{
    internal HabitProgress ToProgress() =>
        new(CompletedValue, Notes, GeoLocation.From(Latitude, Longitude));
}

public enum SyncItemStatus
{
    /// <summary>Se aplicó. El móvil marca el cambio como sincronizado.</summary>
    Applied = 1,

    /// <summary>El servidor tiene una versión más nueva; el móvil debe quedarse con la del servidor (llega en el pull).</summary>
    Stale = 2,

    /// <summary>No se puede aplicar nunca (regla de negocio). El móvil debe descartarlo y avisar al usuario.</summary>
    Rejected = 3,
}

/// <param name="ServerId">
/// Si viene y es distinto de <paramref name="Id"/>, el servidor ya tenía ese dato con otro id
/// (p. ej. el mismo día registrado desde otro teléfono): el móvil debe reemplazar su id local por este.
/// </param>
public sealed record SyncItemResult(Guid Id, SyncItemStatus Status, Guid? ServerId, string? Message);

public sealed record SyncPushResponse(
    DateTimeOffset ServerTime,
    IReadOnlyList<SyncItemResult> Habits,
    IReadOnlyList<SyncItemResult> Records);

/// <param name="ServerTime">Guardarlo y enviarlo como <c>since</c> en el siguiente pull.</param>
/// <param name="IsFullSnapshot">true si no se envió <c>since</c>: el móvil puede reemplazar todo su contenido.</param>
public sealed record SyncPullResponse(
    DateTimeOffset ServerTime,
    bool IsFullSnapshot,
    IReadOnlyList<HabitSyncItem> Habits,
    IReadOnlyList<HabitRecordSyncItem> Records,
    IReadOnlyList<RecommendationResponse> Recommendations);

public sealed record HabitSyncItem(HabitResponse Habit, bool IsDeleted);

public sealed record HabitRecordSyncItem(HabitRecordResponse Record, bool IsDeleted);

public sealed class SyncPushRequestValidator : AbstractValidator<SyncPushRequest>
{
    public const int MaxItemsPerType = 500;

    public SyncPushRequestValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().WithMessage("El identificador del dispositivo es obligatorio.");
        RuleFor(x => x.Habits!.Count).LessThanOrEqualTo(MaxItemsPerType).When(x => x.Habits is not null)
            .WithMessage($"Se pueden enviar como máximo {MaxItemsPerType} hábitos por lote.");
        RuleFor(x => x.Records!.Count).LessThanOrEqualTo(MaxItemsPerType).When(x => x.Records is not null)
            .WithMessage($"Se pueden enviar como máximo {MaxItemsPerType} registros por lote.");
        RuleForEach(x => x.Habits).Must(change => change.Id != Guid.Empty)
            .WithMessage("Todos los hábitos deben traer su identificador.");
        RuleForEach(x => x.Records).Must(change => change.Id != Guid.Empty && change.HabitId != Guid.Empty)
            .WithMessage("Todos los registros deben traer su identificador y el de su hábito.");
    }
}
