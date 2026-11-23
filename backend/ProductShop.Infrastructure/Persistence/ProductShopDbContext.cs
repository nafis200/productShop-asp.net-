using Microsoft.EntityFrameworkCore;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Persistence
{
    // The DbContext is EF Core's "connection + unit of work".
    // Each DbSet<T> property = one table we can query with LINQ.
    public class ProductShopDbContext : DbContext
    {
        // All our tables live in this PostgreSQL schema (a "folder" inside the database),
        // so they never clash with tables of other projects using the same database.
        public const string Schema = "product_shop";

        public ProductShopDbContext(DbContextOptions<ProductShopDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema(Schema);

            // Finds every IEntityTypeConfiguration<T> class in this project (the Configurations folder)
            // and applies it. So adding a new XxxConfiguration.cs file is enough — no need to touch this.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductShopDbContext).Assembly);
        }
    }
}
