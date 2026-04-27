using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class PetMedicationConfiguration : IEntityTypeConfiguration<PetMedication>
{
    public void Configure(EntityTypeBuilder<PetMedication> builder)
    {
        builder.ToTable("PetMedications", t =>
        {
            t.HasCheckConstraint(
                "CK_PetMedications_DateRange",
                "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
            t.HasCheckConstraint(
                "CK_PetMedications_Interval_Positive",
                "\"Interval\" > 0");
        });

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).IsRequired();
        builder.Property(m => m.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(m => m.PetId);
        builder.HasIndex(m => new { m.PetId, m.StartDate });

        builder.HasOne<Pet>()
            .WithMany(p => p.PetMedications)
            .HasForeignKey(m => m.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}