using HealthyMind.Application.Common;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Users;

/// <summary>Alta de usuarios: la comparten el registro público y el alta hecha por un administrador.</summary>
internal sealed class UserRegistration(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<User> RegisterAsync(NewUser newUser, CancellationToken cancellationToken)
    {
        if (await users.UsernameExistsAsync(User.NormalizeUsername(newUser.Username), cancellationToken))
        {
            throw new ConflictException("El nombre de usuario ya está registrado.");
        }

        if (await users.EmailExistsAsync(User.NormalizeEmail(newUser.Email), null, cancellationToken))
        {
            throw new ConflictException("El correo ya está registrado.");
        }

        var user = User.Register(
            newUser.Username,
            newUser.Email,
            newUser.FirstName,
            newUser.LastName,
            passwordHasher.Hash(newUser.Password),
            newUser.Role,
            newUser.TimeZoneId,
            timeProvider.GetUtcNow());

        users.Add(user);
        return user;
    }
}

internal sealed record NewUser(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    UserRole Role,
    string? TimeZoneId);
