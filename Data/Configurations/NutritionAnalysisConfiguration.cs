using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class NutritionAnalysisConfiguration : IEntityTypeConfiguration<NutritionAnalysis>
{
    public void Configure(EntityTypeBuilder<NutritionAnalysis> builder)
    {
        builder.ToTable("NutritionAnalyses", t =>
        {
            t.HasCheckConstraint(
                "CK_NutritionAnalyses_NonNegativeCalories",
                "\"TargetCalories\" >= 0 AND \"ActualCalories\" >= 0");
        });

        builder.HasKey(a => a.Id);

        // Calorie figures come back from the classifier as plain numbers; two
        // decimals is well past what a kcal figure needs.
        builder.Property(a => a.TargetCalories).HasPrecision(10, 2);
        builder.Property(a => a.ActualCalories).HasPrecision(10, 2);

        // Deviation is unbounded above — feeding ten times the target is a
        // legitimate 900%.
        builder.Property(a => a.DeviationPct).HasPrecision(10, 2);

        builder.Property(a => a.Disclaimer).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("now()");

        // Retention trims to the newest two rows per pet, which is exactly the
        // order this index serves.
        builder.HasIndex(a => new { a.PetId, a.CreatedAt });

        builder.HasOne<Pet>()
            .WithMany()
            .HasForeignKey(a => a.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
