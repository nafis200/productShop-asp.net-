using ProductShop.Domain.Entities;

namespace ProductShop.Application.Interfaces
{
    public interface IRoleRepository
    {
        Task<Role?> GetByNameAsync(string name);
    }
}
