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

        // upsert key: a given token is unique per user
        builder.HasIndex(t => new { t.UserId, t.Token }).IsUnique();
        builder.HasIndex(t => t.LastSeenAt);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
