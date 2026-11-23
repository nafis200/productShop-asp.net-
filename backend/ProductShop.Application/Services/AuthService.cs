using ProductShop.Application.Dtos;
using ProductShop.Application.Interfaces;
using ProductShop.Application.Mappings;
using ProductShop.Domain.Constants;
using ProductShop.Domain.Entities;
using ProductShop.Utilities.Extensions;

namespace ProductShop.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;

        // Dependency Injection: ASP.NET creates these objects and passes them in.
        // AuthService only knows the interfaces, never the database classes.
        public AuthService(
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IPasswordHasher passwordHasher,
            ITokenService tokenService)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto)
        {
            var email = registerDto.Email.NormalizeEmail();

            if (await _userRepository.EmailExistsAsync(email))
            {
                return null;
            }

            // Everyone who registers is a normal "User". Admins are created by the seeder.
            var userRole = await _roleRepository.GetByNameAsync(RoleNames.User)
                ?? throw new InvalidOperationException($"Role '{RoleNames.User}' is missing. Did you apply the migrations?");

            var user = new User
            {
                FirstName = registerDto.FirstName.Trim(),
                LastName = registerDto.LastName.Trim(),
                Email = email
            };
            user.PasswordHash = _passwordHasher.Hash(user, registerDto.Password);
            user.UserRoles.Add(new UserRole { RoleId = userRole.Id, Role = userRole });

            await _userRepository.AddAsync(user);

            return CreateAuthResponse(user);
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
        {
            var user = await _userRepository.GetByEmailAsync(loginDto.Email.NormalizeEmail());

            // Same answer for "no such email" and "wrong password",
            // so an attacker cannot find out which emails are registered.
            if (user is null || !_passwordHasher.Verify(user, user.PasswordHash, loginDto.Password))
            {
                return null;
            }

            return CreateAuthResponse(user);
        }

        public async Task<UserDto?> GetUserByIdAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            return user?.ToDto();
        }

        private AuthResponseDto CreateAuthResponse(User user)
        {
            var userDto = user.ToDto();
            var (token, expiresAt) = _tokenService.CreateAccessToken(user, userDto.Roles);

            return new AuthResponseDto
            {
                AccessToken = token,
                ExpiresAt = expiresAt,
                User = userDto
            };
        }
    }
}
