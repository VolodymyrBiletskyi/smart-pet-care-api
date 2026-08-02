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
                "CK_NutritionAnalyses_Score",
                "\"Score\" >= 0 AND \"Score\" <= 100");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Summary).HasMaxLength(4000).IsRequired();
        builder.Property(a => a.Disclaimer).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.Advice).IsRequired();
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
