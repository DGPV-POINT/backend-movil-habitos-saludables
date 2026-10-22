using HealthyMind.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthyMind.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Username).HasMaxLength(User.UsernameMaxLength).IsRequired();
        builder.Property(user => user.NormalizedUsername).HasMaxLength(User.UsernameMaxLength).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(user => user.FirstName).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(user => user.LastName).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(user => user.TimeZoneId).HasMaxLength(User.TimeZoneMaxLength).IsRequired();
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(user => user.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(user => user.NormalizedUsername).IsUnique();
        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasIndex(user => user.Role);

        builder.Ignore(user => user.IsActive);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(token => token.DeviceId).HasMaxLength(RefreshToken.DeviceIdMaxLength).IsRequired();
        builder.Property(token => token.DeviceName).HasMaxLength(RefreshToken.DeviceNameMaxLength);
        builder.Property(token => token.RevocationReason).HasMaxLength(RefreshToken.ReasonMaxLength);

        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.UserId, token.DeviceId });

        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(token => token.WasRotated);
    }
}
