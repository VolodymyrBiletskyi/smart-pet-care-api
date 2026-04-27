using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class PetConditionConfiguration : IEntityTypeConfiguration<PetCondition>
{
    public void Configure(EntityTypeBuilder<PetCondition> builder)
    {
        builder.ToTable("PetConditions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(c => c.PetId);
        builder.HasIndex(c => new { c.PetId, c.IsActive });

        builder.HasOne<Pet>()
            .WithMany(p => p.PetConditions)
            .HasForeignKey(c => c.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}