using HealthyMind.Application.Common;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Auth;

internal sealed class SessionUseCases(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    SessionIssuer sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ISessionUseCases
{
    public async Task<IReadOnlyList<SessionResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = await refreshTokens.ListActiveByUserAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
        return active.OrderByDescending(token => token.CreatedAt).Select(SessionResponse.From).ToList();
    }

    public async Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var token = (await refreshTokens.ListActiveByUserAsync(userId, now, cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == sessionId)
            ?? throw new NotFoundException("Sesión no encontrada.");

        token.Revoke(now, RevocationReasons.RevokedByUser);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        await sessions.RevokeAllAsync(userId, RevocationReasons.LogoutAll, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        users.IsActiveAsync(userId, cancellationToken);
}
