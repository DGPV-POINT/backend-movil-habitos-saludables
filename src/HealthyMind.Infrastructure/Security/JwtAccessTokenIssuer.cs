using System.Security.Claims;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HealthyMind.Infrastructure.Security;

internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(User user)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtClaims.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtClaims.Role, user.Role.ToString()),
            ]),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}

/// <summary>Nombres cortos de claims; la API los usa tal cual (sin el mapeo heredado de .NET).</summary>
public static class JwtClaims
{
    public const string Name = "name";
    public const string Role = "role";
}
