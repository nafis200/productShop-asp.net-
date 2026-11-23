# Step 9 — Cheat Sheet: একদম শুরু থেকে নতুন Project বানানো

> এই project (বা এরকম যেকোনো project) নিজে নিজে আবার বানানোর জন্য ছোট একটা checklist। `MyApp` এর জায়গায় তোমার নিজের নাম বসাও।
> প্রতিটা line সেই step এর link দেয় যেখানে বিষয়টা বুঝিয়ে বলা আছে।
>
> ⬅️ [Step 8 — Run & test](08-run-and-test.md) · [README তে ফিরে যাও](../README.md)

---

### A. Setup ([Step 1](01-project-setup.md))

```bash
mkdir MyApp && cd MyApp
dotnet new sln -n MyApp
dotnet new classlib -n MyApp.Domain -f net8.0
dotnet new classlib -n MyApp.Application -f net8.0
dotnet new classlib -n MyApp.Infrastructure -f net8.0
dotnet new classlib -n MyApp.Utilities -f net8.0
dotnet new webapi   -n MyApp.Api -f net8.0 --use-controllers
dotnet sln add MyApp.Domain MyApp.Application MyApp.Infrastructure MyApp.Utilities MyApp.Api

dotnet add MyApp.Application    reference MyApp.Domain MyApp.Utilities
dotnet add MyApp.Infrastructure reference MyApp.Application MyApp.Domain MyApp.Utilities
dotnet add MyApp.Api            reference MyApp.Application MyApp.Infrastructure MyApp.Domain MyApp.Utilities

dotnet add MyApp.Application    package Microsoft.Extensions.DependencyInjection.Abstractions --version 8.0.2
dotnet add MyApp.Infrastructure package Microsoft.Extensions.DependencyInjection.Abstractions --version 8.0.2
dotnet add MyApp.Infrastructure package Microsoft.Extensions.Configuration.Binder           --version 8.0.2
dotnet add MyApp.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL               --version 8.0.2
dotnet add MyApp.Infrastructure package Microsoft.Extensions.Identity.Core                  --version 8.0.32
dotnet add MyApp.Infrastructure package Microsoft.IdentityModel.JsonWebTokens               --version 8.23.0
dotnet add MyApp.Api package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.32
dotnet add MyApp.Api package Microsoft.EntityFrameworkCore.Design          --version 8.0.2
dotnet new gitignore
rm MyApp.*/Class1.cs MyApp.Api/WeatherForecast.cs MyApp.Api/Controllers/WeatherForecastController.cs
```

### B. এই ক্রমে code লেখো

| # | Layer | File | Step |
|---|---|---|---|
| 1 | Domain | `Entities/BaseEntity.cs`, তোমার entity গুলো, `Constants/RoleNames.cs` | [2](02-domain-layer.md) |
| 2 | Utilities | ছোট ছোট helper (`StringExtensions.cs`) | [2](02-domain-layer.md) |
| 3 | Application | `Dtos/*`, `Interfaces/I*Repository.cs`, `Interfaces/I*Service.cs`, `IPasswordHasher`, `ITokenService` | [3](03-application-layer.md) |
| 4 | Application | `Mappings/MappingExtensions.cs`, `Services/*Service.cs`, `DependencyInjection.cs` (`AddApplication`) | [3](03-application-layer.md) |
| 5 | Infrastructure | `Persistence/MyAppDbContext.cs`, `Persistence/Configurations/*Configuration.cs`, `SeedData/*` | [4](04-infrastructure-database.md) |
| 6 | Infrastructure | `Repositories/*Repository.cs` | [4](04-infrastructure-database.md) |
| 7 | Infrastructure | `Auth/JwtSettings.cs`, `Auth/JwtTokenService.cs`, `Auth/PasswordHasher.cs` | [5](05-jwt-authentication.md) |
| 8 | Infrastructure | `DependencyInjection.cs` (`AddInfrastructure`) | [4](04-infrastructure-database.md) |
| 9 | Api | `appsettings*.json` (connection string, JwtSettings, AdminUser, Cors) | [6](06-api-layer.md) |
| 10 | Api | `Auth/*`, `Controllers/*Controller.cs`, `Program.cs` | [6](06-api-layer.md) |
| 11 | DB | `dotnet ef migrations add InitialCreate ...` → এটা পড়ো → `dotnet ef database update ...` | [7](07-migrations.md) |
| 12 | Test | `dotnet run --project MyApp.Api` → `/swagger` | [8](08-run-and-test.md) |

### C. পরে একটা নতুন feature add করা (যেমন "Category")

1. **Domain:** `Entities/Category.cs`
2. **Infrastructure:** DbContext এ `DbSet<Category> Categories` + `CategoryConfiguration.cs`
3. **Application:** `CategoryDto`, `CreateCategoryDto`, `ICategoryRepository`, `ICategoryService`, `CategoryService`, `ToDto()` mapping
4. **Infrastructure:** `CategoryRepository.cs`
5. **DI তে register করো:** `AddScoped<ICategoryService, CategoryService>()` (Application) আর `AddScoped<ICategoryRepository, CategoryRepository>()` (Infrastructure)
6. **Api:** `CategoriesController.cs` (+ write action গুলোতে `[Authorize(Roles = RoleNames.Admin)]`)
7. **Migration:** `dotnet ef migrations add AddCategory ...` → `dotnet ef database update ...`

> Step 5 ভুলে গেলে এই error পাবে:
> `Unable to resolve service for type 'ICategoryService' while attempting to activate 'CategoriesController'`.

### D. Golden rules

- Domain কাউকে reference করে না। Application কখনো Infrastructure কে reference করে না।
- Controller সবসময় **Service** কে call করে, কখনো সরাসরি Repository বা DbContext কে না।
- Controller থেকে কখনো entity return করবে না — সবসময় DTO।
- কখনো plain password store করবে না — সবসময় `IPasswordHasher` ব্যবহার করো।
- Middleware এর ক্রম: `UseCors` → `UseAuthentication` → `UseAuthorization` → `MapControllers`।
- Generate হওয়া migration apply করার আগে সবসময় পড়ে নাও।
