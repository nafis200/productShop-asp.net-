using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Persistence.Configurations
{
    // Table rules for "Users": column sizes, required columns, indexes.
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            builder.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
            builder.Property(u => u.LastName).IsRequired().HasMaxLength(50);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
            builder.Property(u => u.PasswordHash).IsRequired();

            // The database itself refuses two users with the same email.
            builder.HasIndex(u => u.Email).IsUnique();
        }
    }
}
