using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HealthyMind.Infrastructure.Persistence.Repositories;

internal sealed class PushDeviceRepository(HealthyMindDbContext context) : IPushDeviceRepository
{
    public Task<PushDevice?> GetAsync(Guid userId, string deviceId, CancellationToken cancellationToken) =>
        context.PushDevices.FirstOrDefaultAsync(device => device.UserId == userId && device.DeviceId == deviceId, cancellationToken);

    public async Task<IReadOnlyList<PushDevice>> ListByTokenAsync(string token, CancellationToken cancellationToken) =>
        await context.PushDevices.Where(device => device.Token == token).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PushDevice>> ListReachableAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await context.PushDevices
            .Where(device => device.UserId == userId
                && context.RefreshTokens.Any(token => token.UserId == userId
                    && token.DeviceId == device.DeviceId
                    && token.RevokedAt == null
                    && token.ExpiresAt > now))
            .ToListAsync(cancellationToken);

    public void Add(PushDevice device) => context.PushDevices.Add(device);

    public void Remove(PushDevice device) => context.PushDevices.Remove(device);
}
