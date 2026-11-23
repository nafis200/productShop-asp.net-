using ProductShop.Domain.Entities;

namespace ProductShop.Application.Interfaces
{
    // Implemented in Infrastructure/Auth/JwtTokenService.cs
    public interface ITokenService
    {
        // Creates a signed JWT that contains the user's id, email and roles.
        (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles);
    }
}
