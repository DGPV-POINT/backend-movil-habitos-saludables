using HealthyMind.Application.Common;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Recommendations;
using HealthyMind.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HealthyMind.Infrastructure.Persistence.Repositories;

internal sealed class RecommendationRepository(HealthyMindDbContext context) : IRecommendationRepository
{
    public Task<AiRecommendation?> GetLatestAsync(Guid userId, CancellationToken cancellationToken) =>
        context.AiRecommendations
            .AsNoTracking()
            .Where(recommendation => recommendation.UserId == userId)
            .OrderByDescending(recommendation => recommendation.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<AiRecommendation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AiRecommendations.AsNoTracking().FirstOrDefaultAsync(recommendation => recommendation.Id == id, cancellationToken);

    public async Task<PagedResult<AiRecommendation>> ListByUserAsync(
        Guid userId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = context.AiRecommendations.AsNoTracking().Where(recommendation => recommendation.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(recommendation => recommendation.CreatedAt)
            .Skip((page.SafePage - 1) * page.SafePageSize)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AiRecommendation>(items, page.SafePage, page.SafePageSize, total);
    }

    public async Task<IReadOnlyList<AiRecommendation>> ListCreatedSinceAsync(
        Guid userId,
        DateTimeOffset? since,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = context.AiRecommendations.AsNoTracking().Where(recommendation => recommendation.UserId == userId);
        if (since is not null)
        {
            query = query.Where(recommendation => recommendation.CreatedAt > since);
        }

        return await query
            .OrderByDescending(recommendation => recommendation.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListUsersDueAsync(
        DateTimeOffset generatedBefore,
        int limit,
        CancellationToken cancellationToken)
    {
        var candidates =
            from user in context.Users
            where user.Status == UserStatus.Active
                && context.Habits.Any(habit => habit.UserId == user.Id && habit.DeletedAt == null)
            let last = context.AiRecommendations
                .Where(recommendation => recommendation.UserId == user.Id)
                .Max(recommendation => (DateTimeOffset?)recommendation.CreatedAt)
            where last == null
                || (last < generatedBefore
                    && (context.HabitRecords.Any(record => record.UserId == user.Id && record.ServerModifiedAt > last)
                        || context.Habits.Any(habit => habit.UserId == user.Id && habit.ServerModifiedAt > last)))
            orderby last
            select user.Id;

        return await candidates.Take(limit).ToListAsync(cancellationToken);
    }

    public void Add(AiRecommendation recommendation) => context.AiRecommendations.Add(recommendation);
}
