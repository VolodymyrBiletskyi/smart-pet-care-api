using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smart_pet_care_api.Models;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();

        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(u => u.Email).IsUnique();
    }
}