using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class NutritionGoalConfiguration : IEntityTypeConfiguration<NutritionGoal>
{
    public void Configure(EntityTypeBuilder<NutritionGoal> builder)
    {
        builder.ToTable("NutritionGoals", t =>
        {
            t.HasCheckConstraint(
                "CK_NutritionGoals_NonNegative",
                "(\"DailyCalorieTarget\" IS NULL OR \"DailyCalorieTarget\" >= 0) " +
                "AND (\"DailyPortionTarget\" IS NULL OR \"DailyPortionTarget\" >= 0) " +
                "AND (\"MealsPerDay\" IS NULL OR \"MealsPerDay\" >= 0)");
        });

        builder.HasKey(g => g.Id);

        builder.Property(g => g.CreatedAt).HasDefaultValueSql("now()");

        // One nutrition goal per pet.
        builder.HasIndex(g => g.PetId).IsUnique();

        builder.HasOne<Pet>()
            .WithMany()
            .HasForeignKey(g => g.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
