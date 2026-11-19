using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HealthyMind.Infrastructure.Persistence.Repositories;

/// <summary>Conteos para el tablero del administrador; solo lectura y sin datos personales.</summary>
internal sealed class AdminDashboardReader(HealthyMindDbContext context) : IAdminDashboardReader
{
    private const int SignUpDays = 14;

    public async Task<AdminDashboard> ReadAsync(DateTimeOffset now, TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var users = context.Users.AsNoTracking();
        var last7 = now.AddDays(-7);
        var last30 = now.AddDays(-30);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var firstSignUpDay = today.AddDays(-(SignUpDays - 1));
        var signUpsSince = now.AddDays(-(SignUpDays + 1));

        var total = await users.CountAsync(cancellationToken);
        var active = await users.CountAsync(user => user.Status == UserStatus.Active, cancellationToken);
        var admins = await users.CountAsync(user => user.Role == UserRole.Admin, cancellationToken);
        var pending = await users.CountAsync(user => user.EmailVerifiedAt == null, cancellationToken);
        var newUsers = await users.CountAsync(user => user.CreatedAt >= last30, cancellationToken);
        var recentlyActive = await users.CountAsync(user => user.LastLoginAt >= last7, cancellationToken);

        var habits = await context.Habits.AsNoTracking()
            .CountAsync(habit => habit.IsActive && habit.DeletedAt == null, cancellationToken);
        var records = await context.HabitRecords.AsNoTracking()
            .CountAsync(record => record.DeletedAt == null && record.CreatedAt >= last7, cancellationToken);

        var recommendations = context.AiRecommendations.AsNoTracking();
        var recentRecommendations = await recommendations.CountAsync(item => item.CreatedAt >= last7, cancellationToken);
        var rated = await recommendations.CountAsync(item => item.WasHelpful != null, cancellationToken);
        var helpful = await recommendations.CountAsync(item => item.WasHelpful == true, cancellationToken);

        var devices = await context.PushDevices.AsNoTracking().CountAsync(cancellationToken);

        // Pocos registros (14 días): se agrupan en memoria por la fecha local del administrador.
        var createdAt = await users
            .Where(user => user.CreatedAt >= signUpsSince)
            .Select(user => user.CreatedAt)
            .ToListAsync(cancellationToken);
        var byDay = createdAt
            .Select(instant => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime))
            .Where(day => day >= firstSignUpDay)
            .GroupBy(day => day)
            .ToDictionary(group => group.Key, group => group.Count());
        var signUps = Enumerable.Range(0, SignUpDays)
            .Select(offset => firstSignUpDay.AddDays(offset))
            .Select(day => new DailyCount(day, byDay.GetValueOrDefault(day)))
            .ToList();

        return new AdminDashboard(
            total,
            active,
            total - active,
            admins,
            pending,
            newUsers,
            recentlyActive,
            habits,
            records,
            recentRecommendations,
            rated == 0 ? null : Math.Round(helpful * 100.0 / rated, 1),
            devices,
            signUps);
    }
}
