using HealthyMind.Domain.Notifications;
using HealthyMind.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthyMind.Infrastructure.Persistence.Configurations;

internal sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> builder)
    {
        builder.ToTable("push_devices");
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).ValueGeneratedNever();

        builder.Property(device => device.DeviceId).HasMaxLength(RefreshToken.DeviceIdMaxLength).IsRequired();
        builder.Property(device => device.Token).HasMaxLength(PushDevice.TokenMaxLength).IsRequired();

        builder.HasIndex(device => new { device.UserId, device.DeviceId }).IsUnique();
        builder.HasIndex(device => device.Token);

        builder.HasOne<User>().WithMany().HasForeignKey(device => device.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
