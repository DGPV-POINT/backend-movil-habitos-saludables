using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Habits;
using Microsoft.EntityFrameworkCore;

namespace HealthyMind.Infrastructure.Persistence.Repositories;

internal sealed class HabitRepository(HealthyMindDbContext context) : IHabitRepository
{
    public async Task<Habit?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Habits.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Habit>> ListByUserAsync(
        Guid userId,
        HabitFilter filter,
        CancellationToken cancellationToken)
    {
        var query = context.Habits.Where(habit => habit.UserId == userId && habit.DeletedAt == null);

        if (filter.Category is not null)
        {
            query = query.Where(habit => habit.Category == filter.Category);
        }

        if (filter.IsActive is not null)
        {
            query = query.Where(habit => habit.IsActive == filter.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePattern.Contains(filter.Search.Trim());
            query = query.Where(habit => EF.Functions.ILike(habit.Name, pattern));
        }

        return await query.OrderByDescending(habit => habit.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        Guid userId,
        string name,
        Guid? excludingHabitId,
        CancellationToken cancellationToken)
    {
        var pattern = LikePattern.Exact(name.Trim());
        return context.Habits.AnyAsync(
            habit => habit.UserId == userId
                && habit.DeletedAt == null
                && habit.Id != excludingHabitId
                && EF.Functions.ILike(habit.Name, pattern),
            cancellationToken);
    }

    public async Task<IReadOnlyList<Habit>> ListChangedSinceAsync(
        Guid userId,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        var query = context.Habits.AsNoTracking().Where(habit => habit.UserId == userId);
        query = since is null
            ? query.Where(habit => habit.DeletedAt == null)
            : query.Where(habit => habit.ServerModifiedAt > since);

        return await query.OrderBy(habit => habit.ServerModifiedAt).ToListAsync(cancellationToken);
    }

    public void Add(Habit habit) => context.Habits.Add(habit);
}

internal sealed class HabitRecordRepository(HealthyMindDbContext context) : IHabitRecordRepository
{
    public async Task<HabitRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await context.HabitRecords.FindAsync([id], cancellationToken);

    public Task<HabitRecord?> GetByHabitAndDateAsync(Guid habitId, DateOnly date, CancellationToken cancellationToken) =>
        context.HabitRecords.FirstOrDefaultAsync(
            record => record.HabitId == habitId && record.RecordDate == date && record.DeletedAt == null,
            cancellationToken);

    public async Task<IReadOnlyList<HabitRecord>> ListByHabitAsync(
        Guid habitId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var query = context.HabitRecords.Where(record => record.HabitId == habitId && record.DeletedAt == null);

        if (from is not null)
        {
            query = query.Where(record => record.RecordDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(record => record.RecordDate <= to);
        }

        return await query.OrderByDescending(record => record.RecordDate).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HabitRecord>> ListByUserAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken) =>
        await context.HabitRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId
                && record.DeletedAt == null
                && record.RecordDate >= from
                && record.RecordDate <= to)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DateOnly>> ListCompletedDatesAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.HabitRecords
            .Where(record => record.UserId == userId && record.DeletedAt == null && record.Completed)
            .Select(record => record.RecordDate)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HabitRecord>> ListChangedSinceAsync(
        Guid userId,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        var query = context.HabitRecords.AsNoTracking().Where(record => record.UserId == userId);
        query = since is null
            ? query.Where(record => record.DeletedAt == null)
            : query.Where(record => record.ServerModifiedAt > since);

        return await query.OrderBy(record => record.ServerModifiedAt).ToListAsync(cancellationToken);
    }

    public void Add(HabitRecord record) => context.HabitRecords.Add(record);
}
