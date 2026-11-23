using Microsoft.EntityFrameworkCore;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Entities;
using ProductShop.Infrastructure.Persistence;

namespace ProductShop.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ProductShopDbContext _context;

        public ProductRepository(ProductShopDbContext context)
        {
            _context = context;
        }

        public Task<List<Product>> GetAllAsync()
        {
            // AsNoTracking: read-only list, EF does not need to watch these objects for changes (faster).
            return _context.Products
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(Guid id)
        {
            // FindAsync looks up by primary key.
            return await _context.Products.FindAsync(id);
        }

        public async Task AddAsync(Product product)
        {
            _context.Products.Add(product);       // INSERT is prepared
            await _context.SaveChangesAsync();    // ...and sent to PostgreSQL here
        }

        public async Task UpdateAsync(Product product)
        {
            _context.Products.Update(product);    // UPDATE
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Product product)
        {
            _context.Products.Remove(product);    // DELETE
            await _context.SaveChangesAsync();
        }
    }
}
