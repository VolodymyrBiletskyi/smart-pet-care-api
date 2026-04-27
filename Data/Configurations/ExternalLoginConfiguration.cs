using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("ExternalLogins");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProviderUserId).IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(e => new { e.Provider, e.ProviderUserId }).IsUnique();
        builder.HasIndex(e => e.UserId);

        builder.HasOne<User>()
            .WithMany(u => u.ExternalLogins)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}