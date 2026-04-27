using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class PetMedicationScheduleTimeConfiguration
    : IEntityTypeConfiguration<PetMedicationScheduleTime>
{
    public void Configure(EntityTypeBuilder<PetMedicationScheduleTime> builder)
    {
        builder.ToTable("PetMedicationScheduleTimes");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TimeOfDay).HasColumnType("time");

        builder.HasIndex(s => s.PetMedicationId);
        builder.HasIndex(s => new { s.PetMedicationId, s.TimeOfDay }).IsUnique();

        builder.HasOne<PetMedication>()
            .WithMany()
            .HasForeignKey(s => s.PetMedicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}