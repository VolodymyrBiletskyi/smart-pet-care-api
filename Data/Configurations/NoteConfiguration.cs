using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("Notes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Content).IsRequired().HasMaxLength(10000);

        builder.Property(n => n.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(n => new { n.PetId, n.UpdatedAt })
            .IsDescending(false, true);

        builder.HasOne<Pet>()
            .WithMany(p => p.Notes)
            .HasForeignKey(n => n.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
