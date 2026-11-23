using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductShop.Application.Interfaces;
using ProductShop.Infrastructure.Auth;
using ProductShop.Infrastructure.Persistence;
using ProductShop.Infrastructure.Persistence.Seed;
using ProductShop.Infrastructure.Repositories;

namespace ProductShop.Infrastructure
{
    public static class DependencyInjection
    {
        // Called once from Program.cs:  builder.Services.AddInfrastructure(builder.Configuration);
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // ---------- Database ----------
            // Reads "ConnectionStrings:DefaultConnection" from appsettings.
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
            }

            services.AddDbContext<ProductShopDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                    // Keep EF's migration history table inside our own schema too.
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ProductShopDbContext.Schema)));

            // ---------- Repositories (interface -> class) ----------
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();

            // ---------- JWT + passwords ----------
            var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                ?? throw new InvalidOperationException($"The '{JwtSettings.SectionName}' configuration section is missing.");
            jwtSettings.Validate();
            services.AddSingleton(jwtSettings);   // Program.cs also uses it to VALIDATE tokens

            services.AddScoped<ITokenService, JwtTokenService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();

            // ---------- Startup: migrations + admin seeding ----------
            var adminUserSettings = configuration.GetSection(AdminUserSettings.SectionName).Get<AdminUserSettings>()
                ?? new AdminUserSettings();
            services.AddSingleton(adminUserSettings);
            services.AddScoped<DatabaseInitializer>();

            return services;
        }
    }
}
