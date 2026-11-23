using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name).IsRequired().HasMaxLength(Product.MaxNameLength);

            // numeric(18,2): up to 16 digits before the dot, exactly 2 after. Right type for money.
            builder.Property(p => p.Price).HasPrecision(18, 2);
        }
    }
}
