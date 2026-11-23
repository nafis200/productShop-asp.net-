using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Constants;
using ProductShop.Domain.Entities;
using ProductShop.Infrastructure.Persistence.Seed;
using ProductShop.Utilities.Extensions;

namespace ProductShop.Infrastructure.Persistence
{
    // Runs once when the API starts (called from Program.cs).
    public class DatabaseInitializer
    {
        private readonly ProductShopDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly AdminUserSettings _adminUserSettings;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(
            ProductShopDbContext context,
            IPasswordHasher passwordHasher,
            AdminUserSettings adminUserSettings,
            ILogger<DatabaseInitializer> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _adminUserSettings = adminUserSettings;
            _logger = logger;
        }

        public async Task InitializeAsync(bool applyMigrations)
        {
            if (applyMigrations)
            {
                // Same as running "dotnet ef database update": applies every migration not applied yet.
                await _context.Database.MigrateAsync();
            }

            await SeedAdminUserAsync();
        }

        // Registration always creates normal users, so the first admin has to come from somewhere:
        // it is created here from the "AdminUser" settings.
        private async Task SeedAdminUserAsync()
        {
            if (string.IsNullOrWhiteSpace(_adminUserSettings.Email) || string.IsNullOrWhiteSpace(_adminUserSettings.Password))
            {
                _logger.LogWarning("No AdminUser configured in appsettings, skipping admin seeding.");
                return;
            }

            var email = _adminUserSettings.Email.NormalizeEmail();

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return;
            }

            var adminRole = await _context.Roles.SingleAsync(r => r.Name == RoleNames.Admin);

            var admin = new User
            {
                FirstName = _adminUserSettings.FirstName,
                LastName = _adminUserSettings.LastName,
                Email = email
            };
            admin.PasswordHash = _passwordHasher.Hash(admin, _adminUserSettings.Password);
            admin.UserRoles.Add(new UserRole { RoleId = adminRole.Id });

            _context.Users.Add(admin);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded admin user {Email}.", email);
        }
    }
}
