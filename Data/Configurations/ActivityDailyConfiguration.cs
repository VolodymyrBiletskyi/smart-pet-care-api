using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class ActivityDailyConfiguration : IEntityTypeConfiguration<ActivityDaily>
{
    public void Configure(EntityTypeBuilder<ActivityDaily> builder)
    {
        builder.ToTable("ActivityDailies", t =>
        {
            t.HasCheckConstraint(
                "CK_ActivityDailies_NonNegative",
                "(\"Steps\" IS NULL OR \"Steps\" >= 0) " +
                "AND (\"ActiveMinutes\" IS NULL OR \"ActiveMinutes\" >= 0) " +
                "AND (\"SleepHours\" IS NULL OR \"SleepHours\" >= 0)");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.RawPayload).IsRequired();
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(a => new { a.PetId, a.ActivityDate })
            .IsDescending(false, true);
        builder.HasIndex(a => new { a.PetId, a.ActivityDate, a.Source })
            .IsUnique();

        builder.HasOne<Pet>()
            .WithMany(p => p.ActivityDailies)
            .HasForeignKey(a => a.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}