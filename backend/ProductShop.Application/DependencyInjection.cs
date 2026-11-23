using Microsoft.Extensions.DependencyInjection;
using ProductShop.Application.Interfaces;
using ProductShop.Application.Services;

namespace ProductShop.Application
{
    public static class DependencyInjection
    {
        // Called once from Program.cs:  builder.Services.AddApplication();
        // "When someone asks for IAuthService, give them an AuthService."
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // Scoped = one new object per HTTP request.
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IProductService, ProductService>();

            return services;
        }
    }
}
