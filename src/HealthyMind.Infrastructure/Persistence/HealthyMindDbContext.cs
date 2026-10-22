using HealthyMind.Domain.Habits;
using HealthyMind.Domain.Recommendations;
using HealthyMind.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HealthyMind.Infrastructure.Persistence;

public sealed class HealthyMindDbContext(DbContextOptions<HealthyMindDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Habit> Habits => Set<Habit>();

    public DbSet<HabitRecord> HabitRecords => Set<HabitRecord>();

    public DbSet<AiRecommendation> AiRecommendations => Set<AiRecommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HealthyMindDbContext).Assembly);
}
