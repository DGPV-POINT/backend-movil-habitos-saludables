using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Ports.Out;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}

/// <summary>Tokens opacos aleatorios (refresh tokens). Solo se persiste su hash.</summary>
public interface ISecureTokenGenerator
{
    string Generate();

    string Hash(string token);
}
