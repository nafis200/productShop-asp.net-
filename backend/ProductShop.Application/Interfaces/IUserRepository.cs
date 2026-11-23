using ProductShop.Domain.Entities;

namespace ProductShop.Application.Interfaces
{
    // Contract: "something that can load and save users".
    // The real code (EF Core + PostgreSQL) lives in Infrastructure/Repositories/UserRepository.cs
    public interface IUserRepository
    {
        // Both Get methods also load the user's roles.
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email);
        Task AddAsync(User user);
    }
}
