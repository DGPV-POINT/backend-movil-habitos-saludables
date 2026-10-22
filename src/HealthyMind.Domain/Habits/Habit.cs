using HealthyMind.Domain.Common;

namespace HealthyMind.Domain.Habits;

public sealed class Habit : SyncableEntity
{
    public const int NameMaxLength = 100;
    public const int UnitMaxLength = 30;
    public const int GoalMaxValue = 1_000_000;

    private Habit()
    {
    }

    private Habit(Guid id, DateTimeOffset createdAt) : base(id, createdAt)
    {
    }

    public Guid UserId { get; private init; }

    public string Name { get; private set; } = null!;

    public HabitCategory Category { get; private set; }

    public int Goal { get; private set; }

    public string Unit { get; private set; } = null!;

    public WeekDays ScheduledDays { get; private set; }

    public TrackingMode TrackingMode { get; private set; }

    public bool IsActive { get; private set; }

    protected override string DeletedMessage => "El hábito fue eliminado.";

    /// <param name="id">Lo genera el móvil para poder crear hábitos sin conexión.</param>
    public static Habit Create(Guid id, Guid userId, HabitDefinition definition, DateTimeOffset createdAt)
    {
        Guard.NotEmpty(userId, "usuario");

        var habit = new Habit(id, createdAt)
        {
            UserId = userId,
            IsActive = true,
        };

        habit.Apply(definition);
        return habit;
    }

    public void Update(HabitDefinition definition, DateTimeOffset updatedAt)
    {
        Touch(updatedAt);
        Apply(definition);
    }

    public void SetActive(bool isActive, DateTimeOffset updatedAt)
    {
        Touch(updatedAt);
        IsActive = isActive;
    }

    public void Delete(DateTimeOffset deletedAt) => MarkDeleted(deletedAt);

    public bool IsOwnedBy(Guid userId) => UserId == userId;

    public bool IsScheduledOn(DateOnly date) => ScheduledDays.HasFlag(date.DayOfWeek.ToWeekDay());

    public bool IsGoalMet(int completedValue) => completedValue >= Goal;

    public void EnsureAcceptsRecords()
    {
        EnsureNotDeleted();

        if (!IsActive)
        {
            throw new DomainException("No se puede registrar progreso en un hábito inactivo.");
        }
    }

    private void Apply(HabitDefinition definition)
    {
        if (!Enum.IsDefined(definition.Category))
        {
            throw new DomainException("La categoría no es válida.");
        }

        if (!Enum.IsDefined(definition.TrackingMode))
        {
            throw new DomainException("El modo de seguimiento no es válido.");
        }

        if (definition.Goal is < 1 or > GoalMaxValue)
        {
            throw new DomainException($"La meta debe estar entre 1 y {GoalMaxValue}.");
        }

        if (definition.ScheduledDays == WeekDays.None || (definition.ScheduledDays & ~WeekDays.Everyday) != 0)
        {
            throw new DomainException("Los días programados no son válidos.");
        }

        Name = Guard.Text(definition.Name, "nombre", NameMaxLength);
        Unit = Guard.Text(definition.Unit, "unidad", UnitMaxLength);
        Category = definition.Category;
        Goal = definition.Goal;
        ScheduledDays = definition.ScheduledDays;
        TrackingMode = definition.TrackingMode;
    }
}

/// <summary>Los datos editables de un hábito.</summary>
public sealed record HabitDefinition(
    string Name,
    HabitCategory Category,
    int Goal,
    string Unit,
    WeekDays ScheduledDays,
    TrackingMode TrackingMode);
