using FluentValidation;
using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Habits;

namespace HealthyMind.Application.Features.HabitRecords;

internal sealed class HabitRecordUseCases(
    IHabitRecordRepository records,
    OwnedResources owned,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<CreateHabitRecordRequest> createValidator,
    IValidator<HabitProgressRequest> progressValidator) : IHabitRecordUseCases
{
    private const string DuplicateDay = "Ya existe un registro para este hábito en la fecha indicada.";

    public async Task<IReadOnlyList<HabitRecordResponse>> ListByHabitAsync(
        Guid userId,
        Guid habitId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            throw new FluentValidation.ValidationException("La fecha inicial no puede ser posterior a la fecha final.");
        }

        var habit = await owned.HabitAsync(userId, habitId, cancellationToken);
        var result = await records.ListByHabitAsync(habit.Id, from, to, cancellationToken);
        return result.Select(HabitRecordResponse.From).ToList();
    }

    public async Task<HabitRecordResponse> GetAsync(Guid userId, Guid recordId, CancellationToken cancellationToken) =>
        HabitRecordResponse.From(await owned.RecordAsync(userId, recordId, cancellationToken));

    public async Task<HabitRecordResponse> CreateAsync(
        Guid userId,
        Guid habitId,
        CreateHabitRecordRequest request,
        CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var habit = await owned.HabitAsync(userId, habitId, cancellationToken);
        var id = request.Id ?? Guid.CreateVersion7();

        if (await records.GetByIdAsync(id, cancellationToken) is not null)
        {
            throw new ConflictException("Ya existe un registro con ese identificador.");
        }

        if (await records.GetByHabitAndDateAsync(habit.Id, request.RecordDate, cancellationToken) is not null)
        {
            throw new ConflictException(DuplicateDay);
        }

        var record = await NewRecordAsync(id, userId, habit, request.RecordDate, request.ToProgress(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return HabitRecordResponse.From(record);
    }

    public async Task<HabitRecordResponse> UpsertForDateAsync(
        Guid userId,
        Guid habitId,
        DateOnly date,
        HabitProgressRequest request,
        CancellationToken cancellationToken)
    {
        await progressValidator.ValidateAndThrowAsync(request, cancellationToken);
        var habit = await owned.HabitAsync(userId, habitId, cancellationToken);

        var record = await records.GetByHabitAndDateAsync(habit.Id, date, cancellationToken);
        if (record is null)
        {
            record = await NewRecordAsync(Guid.CreateVersion7(), userId, habit, date, request.ToProgress(), cancellationToken);
        }
        else
        {
            record.Update(habit, request.ToProgress(), timeProvider.GetUtcNow());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return HabitRecordResponse.From(record);
    }

    public async Task<HabitRecordResponse> UpdateAsync(
        Guid userId,
        Guid recordId,
        HabitProgressRequest request,
        CancellationToken cancellationToken)
    {
        await progressValidator.ValidateAndThrowAsync(request, cancellationToken);
        var record = await owned.RecordAsync(userId, recordId, cancellationToken);
        var habit = await owned.HabitAsync(userId, record.HabitId, cancellationToken);

        record.Update(habit, request.ToProgress(), timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return HabitRecordResponse.From(record);
    }

    public async Task DeleteAsync(Guid userId, Guid recordId, CancellationToken cancellationToken)
    {
        var record = await owned.RecordAsync(userId, recordId, cancellationToken);
        record.Delete(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<HabitRecord> NewRecordAsync(
        Guid id,
        Guid userId,
        Habit habit,
        DateOnly date,
        HabitProgress progress,
        CancellationToken cancellationToken)
    {
        var user = await owned.UserAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var record = HabitRecord.Create(id, habit, date, progress, user.LocalDate(now), now);
        records.Add(record);
        return record;
    }
}
