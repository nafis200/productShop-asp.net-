using ProductShop.Application.Dtos;

namespace ProductShop.Application.Interfaces
{
    public interface IAuthService
    {
        // Returns null when the email is already registered.
        Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto);

        // Returns null when the email or password is wrong.
        Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);

        // Returns null when the user no longer exists.
        Task<UserDto?> GetUserByIdAsync(Guid userId);
    }
}
