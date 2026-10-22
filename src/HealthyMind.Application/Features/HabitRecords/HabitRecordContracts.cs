using FluentValidation;
using HealthyMind.Domain.Habits;

namespace HealthyMind.Application.Features.HabitRecords;

/// <param name="Id">Opcional. El móvil puede enviarlo para usar el mismo id que generó en SQLite.</param>
public sealed record CreateHabitRecordRequest(
    Guid? Id,
    DateOnly RecordDate,
    int CompletedValue,
    string? Notes,
    double? Latitude,
    double? Longitude) : IHabitProgressFields
{
    internal HabitProgress ToProgress() =>
        new(CompletedValue, Notes, GeoLocation.From(Latitude, Longitude));
}

public sealed record HabitProgressRequest(int CompletedValue, string? Notes, double? Latitude, double? Longitude)
    : IHabitProgressFields
{
    internal HabitProgress ToProgress() =>
        new(CompletedValue, Notes, GeoLocation.From(Latitude, Longitude));
}

public sealed record HabitRecordResponse(
    Guid Id,
    Guid HabitId,
    DateOnly RecordDate,
    int CompletedValue,
    bool Completed,
    string? Notes,
    double? Latitude,
    double? Longitude,
    DateTimeOffset UpdatedAt)
{
    public static HabitRecordResponse From(HabitRecord record) => new(
        record.Id,
        record.HabitId,
        record.RecordDate,
        record.CompletedValue,
        record.Completed,
        record.Notes,
        record.Location?.Latitude,
        record.Location?.Longitude,
        record.UpdatedAt);
}

/// <summary>Campos de avance comunes a crear y actualizar un registro.</summary>
public interface IHabitProgressFields
{
    int CompletedValue { get; }

    string? Notes { get; }

    double? Latitude { get; }

    double? Longitude { get; }
}

public sealed class HabitProgressFieldsValidator : AbstractValidator<IHabitProgressFields>
{
    public HabitProgressFieldsValidator()
    {
        RuleFor(x => x.CompletedValue)
            .InclusiveBetween(0, HabitRecord.CompletedValueMaxValue)
            .WithMessage("El valor realizado no puede ser negativo.");
        RuleFor(x => x.Notes)
            .MaximumLength(HabitRecord.NotesMaxLength)
            .WithMessage($"Las notas no pueden superar los {HabitRecord.NotesMaxLength} caracteres.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).WithMessage("La latitud no es válida.");
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).WithMessage("La longitud no es válida.");
        RuleFor(x => x.Longitude)
            .NotNull().When(x => x.Latitude.HasValue)
            .WithMessage("La ubicación necesita latitud y longitud.");
        RuleFor(x => x.Latitude)
            .NotNull().When(x => x.Longitude.HasValue)
            .WithMessage("La ubicación necesita latitud y longitud.");
    }
}

public sealed class CreateHabitRecordRequestValidator : AbstractValidator<CreateHabitRecordRequest>
{
    public CreateHabitRecordRequestValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty).WithMessage("El identificador no puede estar vacío.");
        RuleFor(x => x.RecordDate).NotEqual(default(DateOnly)).WithMessage("La fecha es obligatoria.");
        Include(new HabitProgressFieldsValidator());
    }
}

public sealed class HabitProgressRequestValidator : AbstractValidator<HabitProgressRequest>
{
    public HabitProgressRequestValidator()
    {
        Include(new HabitProgressFieldsValidator());
    }
}
