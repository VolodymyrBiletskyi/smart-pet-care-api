using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token).IsRequired();
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(t => t.LastSeenAt).HasDefaultValueSql("now()");

        // upsert key: an FCM token identifies a device, not an account, so it is unique on
        // its own. Keyed per user it could sit under several accounts at once, and the row
        // left under an account the device had been signed out of kept receiving that
        // account's pushes until it went stale 60 days later.
        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => t.LastSeenAt);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
