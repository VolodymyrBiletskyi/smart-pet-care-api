using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(4000);

        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(e => new { e.PetId, e.ObservedAt })
            .IsDescending(false, true);
        builder.HasIndex(e => new { e.PetId, e.Type });

        builder.HasOne<Pet>()
            .WithMany(p => p.JournalEntries)
            .HasForeignKey(e => e.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
