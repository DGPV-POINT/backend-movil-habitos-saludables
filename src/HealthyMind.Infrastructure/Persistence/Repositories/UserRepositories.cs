using HealthyMind.Application.Common;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HealthyMind.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(HealthyMindDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByLoginAsync(string usernameOrEmail, CancellationToken cancellationToken)
    {
        var login = usernameOrEmail.Trim().ToLowerInvariant();
        return context.Users.FirstOrDefaultAsync(
            user => user.NormalizedUsername == login || user.Email == login,
            cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, Guid? excludingUserId, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.Email == email && user.Id != excludingUserId, cancellationToken);

    public Task<bool> AnyAdminAsync(CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.Role == UserRole.Admin, cancellationToken);

    public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.Id == id && user.Status == UserStatus.Active, cancellationToken);

    public async Task<PagedResult<User>> SearchAsync(
        string? search,
        UserStatus? status,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            query = query.Where(user =>
                EF.Functions.ILike(user.Username, pattern)
                || EF.Functions.ILike(user.Email, pattern)
                || EF.Functions.ILike(user.FirstName, pattern)
                || EF.Functions.ILike(user.LastName, pattern));
        }

        if (status is not null)
        {
            query = query.Where(user => user.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(user => user.NormalizedUsername)
            .Skip((page.SafePage - 1) * page.SafePageSize)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<User>(items, page.SafePage, page.SafePageSize, total);
    }

    public void Add(User user) => context.Users.Add(user);
}

internal sealed class RefreshTokenRepository(HealthyMindDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > now)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByDeviceAsync(
        Guid userId,
        string deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await context.RefreshTokens
            .Where(token => token.UserId == userId
                && token.DeviceId == deviceId
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => context.RefreshTokens.Add(token);
}
