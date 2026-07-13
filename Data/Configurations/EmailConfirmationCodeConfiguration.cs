using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class EmailConfirmationCodeConfiguration : IEntityTypeConfiguration<EmailConfirmationCode>
{
    public void Configure(EntityTypeBuilder<EmailConfirmationCode> builder)
    {
        builder.ToTable("EmailConfirmationCodes");

        builder.HasKey(c => c.Id);

        builder.Ignore(c => c.IsExpired);   // computed properties, not columns
        builder.Ignore(c => c.IsActive);

        builder.Property(c => c.CodeHash).IsRequired();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(c => c.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
