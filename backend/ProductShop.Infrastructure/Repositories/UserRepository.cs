using Microsoft.EntityFrameworkCore;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Entities;
using ProductShop.Infrastructure.Persistence;

namespace ProductShop.Infrastructure.Repositories
{
    // The real implementation of IUserRepository, using EF Core.
    public class UserRepository : IUserRepository
    {
        private readonly ProductShopDbContext _context;

        public UserRepository(ProductShopDbContext context)
        {
            _context = context;
        }

        public Task<User?> GetByIdAsync(Guid id)
        {
            return UsersWithRoles().SingleOrDefaultAsync(u => u.Id == id);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            return UsersWithRoles().SingleOrDefaultAsync(u => u.Email == email);
        }

        public Task<bool> EmailExistsAsync(string email)
        {
            return _context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // Include + ThenInclude = SQL JOIN Users -> UserRoles -> Roles,
        // so user.UserRoles[i].Role.Name is filled in.
        private IQueryable<User> UsersWithRoles()
        {
            return _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role);
        }
    }
}
