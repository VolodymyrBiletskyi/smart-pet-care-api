using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");

        builder.HasKey(t => t.Id);

        builder.Ignore(t => t.IsExpired);   // computed properties, not columns
        builder.Ignore(t => t.IsActive);

        builder.Property(t => t.TokenHash).IsRequired();
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");

        // Unique: the hash is what the reset is looked up by, so it has to resolve
        // to one row — a collision would be two accounts behind one link.
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
