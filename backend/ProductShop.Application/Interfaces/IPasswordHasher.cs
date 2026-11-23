using ProductShop.Domain.Entities;

namespace ProductShop.Application.Interfaces
{
    // Implemented in Infrastructure/Auth/PasswordHasher.cs
    public interface IPasswordHasher
    {
        string Hash(User user, string password);
        bool Verify(User user, string passwordHash, string password);
    }
}
