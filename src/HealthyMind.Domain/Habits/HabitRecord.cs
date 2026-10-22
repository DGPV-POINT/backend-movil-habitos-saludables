using HealthyMind.Domain.Common;

namespace HealthyMind.Domain.Habits;

/// <summary>Cumplimiento de un hábito en un día. Hay uno solo por hábito y fecha.</summary>
public sealed class HabitRecord : SyncableEntity
{
    public const int NotesMaxLength = 300;
    public const int CompletedValueMaxValue = 10_000_000;

    private HabitRecord()
    {
    }

    private HabitRecord(Guid id, DateTimeOffset createdAt) : base(id, createdAt)
    {
    }

    public Guid HabitId { get; private init; }

    public Guid UserId { get; private init; }

    public DateOnly RecordDate { get; private init; }

    public int CompletedValue { get; private set; }

    /// <summary>
    /// Se calcula contra la meta vigente al registrar; si después cambia la meta, el histórico no se reescribe.
    /// </summary>
    public bool Completed { get; private set; }

    public string? Notes { get; private set; }

    public GeoLocation? Location { get; private set; }

    protected override string DeletedMessage => "El registro fue eliminado.";

    /// <param name="id">Lo genera el móvil para poder registrar sin conexión.</param>
    /// <param name="today">Fecha local del usuario; no se aceptan fechas futuras.</param>
    public static HabitRecord Create(
        Guid id,
        Habit habit,
        DateOnly recordDate,
        HabitProgress progress,
        DateOnly today,
        DateTimeOffset createdAt)
    {
        habit.EnsureAcceptsRecords();

        if (recordDate > today)
        {
            throw new DomainException("No se puede registrar una fecha futura.");
        }

        var record = new HabitRecord(id, createdAt)
        {
            HabitId = habit.Id,
            UserId = habit.UserId,
            RecordDate = recordDate,
        };

        record.Apply(habit, progress);
        return record;
    }

    public void Update(Habit habit, HabitProgress progress, DateTimeOffset updatedAt)
    {
        if (habit.Id != HabitId)
        {
            throw new DomainException("El registro no pertenece a ese hábito.");
        }

        // Corregir un día ya registrado se permite aunque el hábito esté inactivo; crear uno nuevo no.
        habit.EnsureNotDeleted();
        Touch(updatedAt);
        Apply(habit, progress);
    }

    public void Delete(DateTimeOffset deletedAt) => MarkDeleted(deletedAt);

    public bool IsOwnedBy(Guid userId) => UserId == userId;

    private void Apply(Habit habit, HabitProgress progress)
    {
        if (progress.CompletedValue is < 0 or > CompletedValueMaxValue)
        {
            throw new DomainException("El valor realizado no es válido.");
        }

        CompletedValue = progress.CompletedValue;
        Completed = habit.IsGoalMet(progress.CompletedValue);
        Notes = Guard.OptionalText(progress.Notes, "notas", NotesMaxLength);
        Location = progress.Location;
    }
}

/// <summary>Lo que el usuario reporta de un día.</summary>
public sealed record HabitProgress(int CompletedValue, string? Notes, GeoLocation? Location);
