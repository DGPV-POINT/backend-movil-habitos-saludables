using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Users;

public sealed record UserProfileResponse(
    Guid Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    UserRole Role,
    UserStatus Status,
    string TimeZoneId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt)
{
    public static UserProfileResponse From(User user) => new(
        user.Id,
        user.Username,
        user.Email,
        user.FirstName,
        user.LastName,
        user.Role,
        user.Status,
        user.TimeZoneId,
        user.CreatedAt,
        user.LastLoginAt);
}

public sealed record UpdateProfileRequest(string FirstName, string LastName, string Email, string? TimeZoneId);

/// <param name="DeviceId">La sesión de este dispositivo se conserva; las demás se cierran.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string DeviceId);

public sealed record CreateUserRequest(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    UserRole Role,
    string? TimeZoneId);

public sealed record UserSearchRequest(string? Search, UserStatus? Status, int Page = 1, int PageSize = 20);

/// <summary>Administrador inicial, tomado de la configuración al arrancar.</summary>
public sealed record BootstrapAdminRequest(string Username, string Email, string Password);
