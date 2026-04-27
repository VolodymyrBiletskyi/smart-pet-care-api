using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.ToTable("Pets", t =>
        {
            t.HasCheckConstraint(
                "CK_Pets_WeightKg_Positive",
                "\"WeightKg\" IS NULL OR \"WeightKg\" > 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.Species).IsRequired();
        builder.Property(p => p.BirthDate).HasColumnType("date");
        builder.Property(p => p.WeightKg).HasColumnType("numeric");
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(p => p.UserId);

        // The fix: wire the existing User.Pets collection
        builder.HasOne<User>()
            .WithMany(u => u.Pets)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}