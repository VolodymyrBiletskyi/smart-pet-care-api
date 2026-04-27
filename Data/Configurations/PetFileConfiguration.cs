using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class PetFileConfiguration : IEntityTypeConfiguration<PetFile>
{
    public void Configure(EntityTypeBuilder<PetFile> builder)
    {
        builder.ToTable("PetFiles", t =>
        {
            t.HasCheckConstraint(
                "CK_PetFiles_Size_NonNegative",
                "\"Size\" >= 0");
        });

        builder.HasKey(f => f.Id);

        builder.Property(f => f.StorageKey).IsRequired();
        builder.Property(f => f.FileName).IsRequired();
        builder.Property(f => f.ContentType).IsRequired();
        builder.Property(f => f.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(f => f.PetId);
        builder.HasIndex(f => f.UploadedByUserId);
        builder.HasIndex(f => f.StorageKey).IsUnique();

        builder.HasOne<Pet>()
            .WithMany(p => p.PetFiles)
            .HasForeignKey(f => f.PetId)
            .OnDelete(DeleteBehavior.Cascade);

        // User has no PetFiles collection → use empty WithMany
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}