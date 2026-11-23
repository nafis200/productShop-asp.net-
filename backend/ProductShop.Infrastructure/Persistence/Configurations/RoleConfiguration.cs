using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductShop.Domain.Entities;
using ProductShop.Infrastructure.Persistence.Seed;

namespace ProductShop.Infrastructure.Persistence.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.HasKey(r => r.Id);

            // We choose the ids ourselves (1 = Admin, 2 = User), the database must not generate them.
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Name).IsRequired().HasMaxLength(50);
            builder.HasIndex(r => r.Name).IsUnique();

            // Seed data: these rows are inserted by the migration itself.
            builder.HasData(SeedData.Roles);
        }
    }
}
