using HealthyMind.Application.Features.Users;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;
using Microsoft.Extensions.Options;

namespace HealthyMind.Application.Features.Auth;

/// <summary>Abre, rota y cierra sesiones de dispositivo. No graba: eso lo decide quien lo usa.</summary>
internal sealed class SessionIssuer(
    IRefreshTokenRepository refreshTokens,
    ISecureTokenGenerator tokenGenerator,
    IAccessTokenIssuer accessTokenIssuer,
    TimeProvider timeProvider,
    IOptions<AuthOptions> options)
{
    private TimeSpan RefreshLifetime => TimeSpan.FromDays(options.Value.RefreshTokenLifetimeDays);

    /// <summary>Sesión nueva tras ingresar con contraseña. Cierra la anterior del mismo dispositivo.</summary>
    public async Task<AuthResponse> OpenAsync(
        User user,
        string deviceId,
        string? deviceName,
        CancellationToken cancellationToken)
    {
        await RevokeDeviceAsync(user.Id, deviceId, RevocationReasons.NewLoginOnDevice, cancellationToken);
        var (token, plain) = Issue(user.Id, deviceId, deviceName);
        return BuildResponse(user, token, plain);
    }

    /// <summary>Cambia el refresh token usado por uno nuevo (rotación).</summary>
    public AuthResponse Rotate(User user, RefreshToken current)
    {
        var (next, plain) = Issue(user.Id, current.DeviceId, current.DeviceName);
        current.RotateTo(next, timeProvider.GetUtcNow());
        return BuildResponse(user, next, plain);
    }

    public async Task RevokeDeviceAsync(Guid userId, string deviceId, string reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var token in await refreshTokens.ListActiveByDeviceAsync(userId, deviceId.Trim(), now, cancellationToken))
        {
            token.Revoke(now, reason);
        }
    }

    public async Task RevokeAllAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken,
        string? exceptDeviceId = null)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var token in await refreshTokens.ListActiveByUserAsync(userId, now, cancellationToken))
        {
            if (exceptDeviceId is null || !token.BelongsToDevice(exceptDeviceId))
            {
                token.Revoke(now, reason);
            }
        }
    }

    private (RefreshToken Token, string Plain) Issue(Guid userId, string deviceId, string? deviceName)
    {
        var plain = tokenGenerator.Generate();
        var token = RefreshToken.Issue(
            userId,
            tokenGenerator.Hash(plain),
            deviceId,
            deviceName,
            timeProvider.GetUtcNow(),
            RefreshLifetime);

        refreshTokens.Add(token);
        return (token, plain);
    }

    private AuthResponse BuildResponse(User user, RefreshToken token, string plainRefreshToken)
    {
        var access = accessTokenIssuer.Issue(user);
        return new AuthResponse(
            access.Value,
            access.ExpiresAt,
            plainRefreshToken,
            token.ExpiresAt,
            UserProfileResponse.From(user));
    }
}
