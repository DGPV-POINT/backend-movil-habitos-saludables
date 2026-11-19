using HealthyMind.Application.Ports.Out;
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
    DateTimeOffset? LastLoginAt,
    bool EmailVerified = true)
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
        user.LastLoginAt,
        user.IsEmailVerified);
}

public sealed record AdminDashboardResponse(
    int TotalUsers,
    int ActiveUsers,
    int SuspendedUsers,
    int Admins,
    int PendingVerification,
    int NewUsersLast30Days,
    int UsersActiveLast7Days,
    int ActiveHabits,
    int RecordsLast7Days,
    int RecommendationsLast7Days,
    double? HelpfulPercentage,
    int PushDevices,
    IReadOnlyList<DailyCount> SignUpsLast14Days)
{
    public static AdminDashboardResponse From(AdminDashboard d) => new(
        d.TotalUsers,
        d.ActiveUsers,
        d.SuspendedUsers,
        d.Admins,
        d.PendingVerification,
        d.NewUsersLast30Days,
        d.UsersActiveLast7Days,
        d.ActiveHabits,
        d.RecordsLast7Days,
        d.RecommendationsLast7Days,
        d.HelpfulPercentage,
        d.PushDevices,
        d.SignUpsLast14Days);
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

public sealed record ChangeRoleRequest(UserRole Role);

/// <summary>Correos que siempre son administradores (sección <c>Admin</c>).</summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>Al arrancar se promueven si ya existen; si se registran después, nacen con rol Admin.</summary>
    public string[] Emails { get; set; } = [];

    public bool IsAdminEmail(string email) =>
        Emails.Any(item => string.Equals(item.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Administrador inicial, tomado de la configuración al arrancar.</summary>
public sealed record BootstrapAdminRequest(string Username, string Email, string Password);
