using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class PetWeightLogConfiguration : IEntityTypeConfiguration<PetWeightLog>
{
    public void Configure(EntityTypeBuilder<PetWeightLog> builder)
    {
        builder.ToTable("PetWeightLogs", t =>
        {
            t.HasCheckConstraint(
                "CK_PetWeightLogs_WeightKg_Positive",
                "\"WeightKg\" > 0 AND \"WeightKg\" <= 230");
        });

        builder.HasKey(w => w.Id);

        builder.Property(w => w.WeightKg).HasColumnType("numeric");
        builder.Property(w => w.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(w => new { w.PetId, w.MeasuredAt })
            .IsUnique()
            .IsDescending(false, true);

        builder.HasIndex(w => w.ReminderId);

        builder.HasOne<Pet>()
            .WithMany(p => p.WeightLogs)
            .HasForeignKey(w => w.PetId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a reminder must not delete weight history, so the link is cut instead.
        builder.HasOne<Reminder>()
            .WithMany()
            .HasForeignKey(w => w.ReminderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
