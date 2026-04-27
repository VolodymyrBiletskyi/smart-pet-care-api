using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class PetEventConfiguration : IEntityTypeConfiguration<PetEvent>
{
    public void Configure(EntityTypeBuilder<PetEvent> builder)
    {
        builder.ToTable("PetEvents", t =>
        {
            t.HasCheckConstraint(
                "CK_PetEvents_DateRange",
                "\"EndAt\" IS NULL OR \"EndAt\" >= \"ScheduledAt\"");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(e => new { e.PetId, e.ScheduledAt })
            .IsDescending(false, true);
        builder.HasIndex(e => new { e.PetId, e.Status });
        builder.HasIndex(e => new { e.SourceType, e.SourceId });

        builder.HasOne<Pet>()
            .WithMany(p => p.PetEvents)
            .HasForeignKey(e => e.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}