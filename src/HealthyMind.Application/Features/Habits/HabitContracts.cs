using FluentValidation;
using HealthyMind.Domain.Habits;

namespace HealthyMind.Application.Features.Habits;

/// <param name="Id">Opcional. El móvil puede enviarlo para usar el mismo id que generó en SQLite.</param>
/// <param name="ScheduledDays">Días en que aplica; vacío o nulo significa todos los días.</param>
public sealed record HabitRequest(
    Guid? Id,
    string Name,
    HabitCategory Category,
    int Goal,
    string Unit,
    IReadOnlyList<DayOfWeek>? ScheduledDays,
    TrackingMode TrackingMode = TrackingMode.Manual)
{
    internal HabitDefinition ToDefinition() =>
        new(Name, Category, Goal, Unit, ScheduledDays.ToWeekDays(), TrackingMode);
}

public sealed record HabitStatusRequest(bool IsActive);

public sealed record HabitResponse(
    Guid Id,
    string Name,
    HabitCategory Category,
    int Goal,
    string Unit,
    IReadOnlyList<DayOfWeek> ScheduledDays,
    TrackingMode TrackingMode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static HabitResponse From(Habit habit) => new(
        habit.Id,
        habit.Name,
        habit.Category,
        habit.Goal,
        habit.Unit,
        habit.ScheduledDays.ToDaysOfWeek(),
        habit.TrackingMode,
        habit.IsActive,
        habit.CreatedAt,
        habit.UpdatedAt);
}

public sealed class HabitRequestValidator : AbstractValidator<HabitRequest>
{
    public HabitRequestValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty).WithMessage("El identificador no puede estar vacío.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del hábito es obligatorio.")
            .MaximumLength(Habit.NameMaxLength).WithMessage($"El nombre no puede superar los {Habit.NameMaxLength} caracteres.");
        RuleFor(x => x.Category).IsInEnum().WithMessage("La categoría no es válida.");
        RuleFor(x => x.Goal)
            .InclusiveBetween(1, Habit.GoalMaxValue).WithMessage($"La meta debe estar entre 1 y {Habit.GoalMaxValue}.");
        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("La unidad es obligatoria.")
            .MaximumLength(Habit.UnitMaxLength).WithMessage($"La unidad no puede superar los {Habit.UnitMaxLength} caracteres.");
        RuleForEach(x => x.ScheduledDays).IsInEnum().WithMessage("Hay un día de la semana no válido.");
        RuleFor(x => x.TrackingMode).IsInEnum().WithMessage("El modo de seguimiento no es válido.");
    }
}
