using Microsoft.EntityFrameworkCore;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Entities;
using ProductShop.Infrastructure.Persistence;

namespace ProductShop.Infrastructure.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly ProductShopDbContext _context;

        public RoleRepository(ProductShopDbContext context)
        {
            _context = context;
        }

        public Task<Role?> GetByNameAsync(string name)
        {
            return _context.Roles.SingleOrDefaultAsync(r => r.Name == name);
        }
    }
}
