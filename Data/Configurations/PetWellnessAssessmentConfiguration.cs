using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public sealed class PetWellnessAssessmentConfiguration
    : IEntityTypeConfiguration<PetWellnessAssessment>
{
    public void Configure(EntityTypeBuilder<PetWellnessAssessment> builder)
    {
        builder.ToTable("PetWellnessAssessments", table =>
        {
            table.HasCheckConstraint(
                "CK_PetWellnessAssessments_Score",
                "\"WellnessScore\" IS NULL OR (\"WellnessScore\" >= 0 AND \"WellnessScore\" <= 100)");
            table.HasCheckConstraint(
                "CK_PetWellnessAssessments_Coverage",
                "\"DataCoverage\" >= 0 AND \"DataCoverage\" <= 1");
        });

        builder.HasKey(assessment => assessment.Id);
        builder.Property(assessment => assessment.Band).HasMaxLength(32);
        builder.Property(assessment => assessment.ScoreStatus).HasMaxLength(32).IsRequired();
        builder.Property(assessment => assessment.DataCoverage).HasPrecision(5, 4);
        builder.Property(assessment => assessment.CalculationVersion).HasMaxLength(32).IsRequired();
        builder.Property(assessment => assessment.Trend).HasMaxLength(32);
        builder.Property(assessment => assessment.ResponseJson).HasColumnType("jsonb").IsRequired();
        builder.Property(assessment => assessment.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(assessment => new { assessment.PetId, assessment.EvaluatedAt })
            .IsDescending(false, true);

        builder.HasOne(assessment => assessment.Pet)
            .WithMany()
            .HasForeignKey(assessment => assessment.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
