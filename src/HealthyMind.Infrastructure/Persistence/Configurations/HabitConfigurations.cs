using HealthyMind.Domain.Habits;
using HealthyMind.Domain.Recommendations;
using HealthyMind.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthyMind.Infrastructure.Persistence.Configurations;

internal sealed class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.ToTable("habits");
        builder.HasKey(habit => habit.Id);
        builder.Property(habit => habit.Id).ValueGeneratedNever();

        builder.Property(habit => habit.Name).HasMaxLength(Habit.NameMaxLength).IsRequired();
        builder.Property(habit => habit.Unit).HasMaxLength(Habit.UnitMaxLength).IsRequired();
        builder.Property(habit => habit.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(habit => habit.TrackingMode).HasConversion<string>().HasMaxLength(30);
        builder.Property(habit => habit.ScheduledDays).HasConversion<short>();

        builder.HasIndex(habit => new { habit.UserId, habit.ServerModifiedAt });
        // La unicidad del nombre (sin mayúsculas, solo vigentes) va como índice por expresión en la migración inicial.

        builder.HasOne<User>().WithMany().HasForeignKey(habit => habit.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(habit => habit.IsDeleted);
    }
}

internal sealed class HabitRecordConfiguration : IEntityTypeConfiguration<HabitRecord>
{
    public void Configure(EntityTypeBuilder<HabitRecord> builder)
    {
        builder.ToTable("habit_records");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();

        builder.Property(record => record.Notes).HasMaxLength(HabitRecord.NotesMaxLength);
        builder.ComplexProperty(record => record.Location, location =>
        {
            location.Property(point => point.Latitude).HasColumnName("latitude");
            location.Property(point => point.Longitude).HasColumnName("longitude");
        });

        // Un registro vigente por hábito y día; los eliminados no cuentan.
        builder.HasIndex(record => new { record.HabitId, record.RecordDate })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");
        builder.HasIndex(record => new { record.UserId, record.RecordDate });
        builder.HasIndex(record => new { record.UserId, record.ServerModifiedAt });

        builder.HasOne<Habit>().WithMany().HasForeignKey(record => record.HabitId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(record => record.UserId).OnDelete(DeleteBehavior.NoAction);

        builder.Ignore(record => record.IsDeleted);
    }
}

internal sealed class AiRecommendationConfiguration : IEntityTypeConfiguration<AiRecommendation>
{
    public void Configure(EntityTypeBuilder<AiRecommendation> builder)
    {
        builder.ToTable("ai_recommendations");
        builder.HasKey(recommendation => recommendation.Id);
        builder.Property(recommendation => recommendation.Id).ValueGeneratedNever();

        builder.Property(recommendation => recommendation.Title).HasMaxLength(AiRecommendation.TitleMaxLength).IsRequired();
        builder.Property(recommendation => recommendation.Summary).HasMaxLength(AiRecommendation.TextMaxLength).IsRequired();
        builder.Property(recommendation => recommendation.FocusHabit).HasMaxLength(AiRecommendation.FocusHabitMaxLength);
        builder.Property(recommendation => recommendation.Recommendation).HasMaxLength(AiRecommendation.TextMaxLength).IsRequired();
        builder.Property(recommendation => recommendation.ModelName).HasMaxLength(AiRecommendation.ModelNameMaxLength).IsRequired();

        builder.HasIndex(recommendation => new { recommendation.UserId, recommendation.CreatedAt });

        builder.HasOne<User>().WithMany().HasForeignKey(recommendation => recommendation.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
