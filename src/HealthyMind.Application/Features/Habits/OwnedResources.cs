using HealthyMind.Application.Common;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Habits;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Habits;

/// <summary>
/// Carga lo que pertenece al usuario. Lo ajeno responde igual que lo inexistente,
/// para no revelar ids de otras cuentas.
/// </summary>
internal sealed class OwnedResources(
    IUserRepository users,
    IHabitRepository habits,
    IHabitRecordRepository records)
{
    public async Task<User> UserAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("Usuario no encontrado.");

    public async Task<Habit> HabitAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
    {
        var habit = await habits.GetByIdAsync(habitId, cancellationToken);
        return habit is not null && habit.IsOwnedBy(userId) && !habit.IsDeleted
            ? habit
            : throw new NotFoundException("Hábito no encontrado.");
    }

    public async Task<HabitRecord> RecordAsync(Guid userId, Guid recordId, CancellationToken cancellationToken)
    {
        var record = await records.GetByIdAsync(recordId, cancellationToken);
        return record is not null && record.IsOwnedBy(userId) && !record.IsDeleted
            ? record
            : throw new NotFoundException("Registro no encontrado.");
    }

    /// <summary>Al borrar un hábito se borran sus registros, para que los demás dispositivos también los quiten.</summary>
    public async Task DeleteHabitAsync(Habit habit, DateTimeOffset deletedAt, CancellationToken cancellationToken)
    {
        habit.Delete(deletedAt);
        foreach (var record in await records.ListByHabitAsync(habit.Id, null, null, cancellationToken))
        {
            record.Delete(deletedAt);
        }
    }
}
