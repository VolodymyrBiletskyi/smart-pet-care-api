using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class ReminderRunConfiguration : IEntityTypeConfiguration<ReminderRun>
{
    public void Configure(EntityTypeBuilder<ReminderRun> builder)
    {
        builder.ToTable("ReminderRun", t =>
        {
            t.HasCheckConstraint(
                "CK_ReminderRun_TimingOrder",
                "(\"SentAt\" IS NULL OR \"SentAt\" >= \"ScheduledFor\") " +
                "AND (\"CompletedAt\" IS NULL OR \"SentAt\" IS NULL OR \"CompletedAt\" >= \"SentAt\")");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.DeliveryMeta)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(r => r.ReminderId);
        builder.HasIndex(r => new { r.Status, r.ScheduledFor });

        builder.HasOne<Reminder>()
            .WithMany(rm => rm.ReminderRuns)
            .HasForeignKey(r => r.ReminderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}