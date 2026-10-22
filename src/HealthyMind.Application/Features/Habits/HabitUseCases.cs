using FluentValidation;
using HealthyMind.Application.Common;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Habits;

namespace HealthyMind.Application.Features.Habits;

internal sealed class HabitUseCases(
    IHabitRepository habits,
    OwnedResources owned,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<HabitRequest> validator) : IHabitUseCases
{
    private const string DuplicateName = "Ya existe un hábito con ese nombre.";

    public async Task<IReadOnlyList<HabitResponse>> ListAsync(
        Guid userId,
        HabitFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await habits.ListByUserAsync(userId, filter, cancellationToken);
        return result.Select(HabitResponse.From).ToList();
    }

    public async Task<HabitResponse> GetAsync(Guid userId, Guid habitId, CancellationToken cancellationToken) =>
        HabitResponse.From(await owned.HabitAsync(userId, habitId, cancellationToken));

    public async Task<HabitResponse> CreateAsync(Guid userId, HabitRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var id = request.Id ?? Guid.CreateVersion7();

        if (await habits.GetByIdAsync(id, cancellationToken) is not null)
        {
            throw new ConflictException("Ya existe un hábito con ese identificador.");
        }

        if (await habits.NameExistsAsync(userId, request.Name.Trim(), null, cancellationToken))
        {
            throw new ConflictException(DuplicateName);
        }

        var habit = Habit.Create(id, userId, request.ToDefinition(), timeProvider.GetUtcNow());
        habits.Add(habit);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return HabitResponse.From(habit);
    }

    public async Task<HabitResponse> UpdateAsync(
        Guid userId,
        Guid habitId,
        HabitRequest request,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var habit = await owned.HabitAsync(userId, habitId, cancellationToken);

        if (await habits.NameExistsAsync(userId, request.Name.Trim(), habit.Id, cancellationToken))
        {
            throw new ConflictException(DuplicateName);
        }

        habit.Update(request.ToDefinition(), timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return HabitResponse.From(habit);
    }

    public async Task<HabitResponse> SetActiveAsync(
        Guid userId,
        Guid habitId,
        HabitStatusRequest request,
        CancellationToken cancellationToken)
    {
        var habit = await owned.HabitAsync(userId, habitId, cancellationToken);
        habit.SetActive(request.IsActive, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return HabitResponse.From(habit);
    }

    public async Task DeleteAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
    {
        var habit = await owned.HabitAsync(userId, habitId, cancellationToken);
        await owned.DeleteHabitAsync(habit, timeProvider.GetUtcNow(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
