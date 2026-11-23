# Step 4 — Infrastructure Layer: Database (EF Core + PostgreSQL) ও Repositories

> **এই ধাপের লক্ষ্য:** PostgreSQL এর সাথে connect করা, আমাদের table গুলো describe করা, আর Step 3 এর
> Interface গুলো implement করে আসল Repository class গুলো লেখা।
>
> ⬅️ [Step 3 — Application layer](03-application-layer.md) · পরের ধাপ ➡️ [Step 5 — JWT authentication](05-jwt-authentication.md)

---

## 4.1 যে শব্দগুলো জানা দরকার

| শব্দ | সহজ মানে |
|---|---|
| **EF Core** (Entity Framework Core) | একটা **ORM**: আপনি C# লিখবেন (`_context.Products.Add(p)`), আর সে আপনার হয়ে SQL (`INSERT INTO ...`) লিখে দেবে। |
| **Npgsql** | .NET এর জন্য PostgreSQL driver। `Npgsql.EntityFrameworkCore.PostgreSQL` = EF Core + ওই driver। |
| **DbContext** | যে class টা আপনার database session কে represent করে। প্রতিটা `DbSet<T>` property = একটা table। |
| **Configuration** (`IEntityTypeConfiguration<T>`) | একটা table এর নিয়মকানুন: max length, required, unique index, relation। |
| **Repository** | এমন একটা class যেটা EF Core কে একটা সহজ Interface (`GetByIdAsync`, `AddAsync`, ...) এর পেছনে লুকিয়ে রাখে। |
| **Seed data** | যে row গুলো automatically insert হয়ে যায় (আমাদের `Admin` / `User` role)। |
| **Schema** | PostgreSQL database এর ভেতরে একটা "folder"। আলাদা schema তে থাকা table গুলোর মধ্যে কখনো নামের সংঘর্ষ হয় না। |

```bash
mkdir -p ProductShop.Infrastructure/Persistence/Configurations \
         ProductShop.Infrastructure/Persistence/SeedData \
         ProductShop.Infrastructure/Repositories \
         ProductShop.Infrastructure/Auth
```

---

## 4.2 Connection string (credentials)

Connection string টা থাকে **`ProductShop.Api/appsettings.Development.json`** এ — কারণ Api project টাই
আসলে run হয়, তাই settings ও তার কাছেই থাকে। (পুরো file টা Step 6 এ আছে।)

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=ep-late-dream-b5clkohq-pooler.c-7.us-east-2.aws.neon.tech; Database=neondb; Username=neondb_owner; Password=npg_bTlCN6g5ZuUn; SSL Mode=VerifyFull; Channel Binding=Require;"
}
```

| অংশ | মানে |
|---|---|
| `Host` | server এর address (Neon cloud Postgres) |
| `Database` | database এর নাম |
| `Username` / `Password` | database এ login করার তথ্য |
| `SSL Mode=VerifyFull` | encrypted connection, certificate ও check করা হয় |

> এটা **TwitterClone এর সেই একই Neon database**। TwitterClone এর একটা `Users` table আগে থেকেই ওখানে আছে,
> তাই ProductShop তার সব table নিজের আলাদা schema **`product_shop`** এ রাখে → `product_shop."Users"`,
> `product_shop."Products"`, ... কোনো conflict নেই।

> 🔐 আসল project এ: password কখনো commit করবেন না। `dotnet user-secrets` বা environment variable ব্যবহার করুন।
> (শেখার project এর জন্য এভাবে ঠিক আছে।)

### এটা code পর্যন্ত কীভাবে পৌঁছায়

```
appsettings.Development.json
        │  ASP.NET loads it automatically at startup into "builder.Configuration"
        ▼
Program.cs:  builder.Services.AddInfrastructure(builder.Configuration);
        ▼
DependencyInjection.cs:  configuration.GetConnectionString("DefaultConnection")
        ▼
services.AddDbContext<ProductShopDbContext>(o => o.UseNpgsql(connectionString))
        ▼
Any class asking for ProductShopDbContext in its constructor gets a connected one.
```

---

## 4.3 `ProductShopDbContext`

- `HasDefaultSchema("product_shop")` → সব table ওই schema তে যাবে।
- `ApplyConfigurationsFromAssembly` → এই project এর সব `XxxConfiguration` class automatically apply করে দেয়।

📄 **`ProductShop.Infrastructure/Persistence/ProductShopDbContext.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Persistence
{
    // The DbContext is EF Core's "connection + unit of work".
    // Each DbSet<T> property = one table we can query with LINQ.
    public class ProductShopDbContext : DbContext
    {
        // All our tables live in this PostgreSQL schema (a "folder" inside the database),
        // so they never clash with tables of other projects using the same database.
        public const string Schema = "product_shop";

        public ProductShopDbContext(DbContextOptions<ProductShopDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema(Schema);

            // Finds every IEntityTypeConfiguration<T> class in this project (the Configurations folder)
            // and applies it. So adding a new XxxConfiguration.cs file is enough — no need to touch this.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductShopDbContext).Assembly);
        }
    }
}
```

---

## 4.4 Table configuration (প্রতিটা Entity র জন্য একটা file)

Domain Entity গুলোর উপর attribute বসানোর বদলে (তাহলে Domain কে EF Core এর উপর depend করতে হতো),
table এর নিয়মগুলো এখানে Infrastructure এ রাখা হয়।

| C# rule | PostgreSQL এ যা হয়ে যায় |
|---|---|
| `HasKey(u => u.Id)` | `PRIMARY KEY ("Id")` |
| `IsRequired()` | `NOT NULL` |
| `HasMaxLength(50)` | `character varying(50)` |
| `HasIndex(u => u.Email).IsUnique()` | `CREATE UNIQUE INDEX` |
| `HasPrecision(18, 2)` | `numeric(18,2)` |
| `HasOne(...).WithMany(...).HasForeignKey(...)` | `FOREIGN KEY` |
| `HasData(...)` | Migration এর ভেতরে `INSERT` |

📄 **`ProductShop.Infrastructure/Persistence/Configurations/UserConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Persistence.Configurations
{
    // Table rules for "Users": column sizes, required columns, indexes.
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            builder.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
            builder.Property(u => u.LastName).IsRequired().HasMaxLength(50);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
            builder.Property(u => u.PasswordHash).IsRequired();

            // The database itself refuses two users with the same email.
            builder.HasIndex(u => u.Email).IsUnique();
        }
    }
}
```

📄 **`ProductShop.Infrastructure/Persistence/Configurations/RoleConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductShop.Domain.Entities;
using ProductShop.Infrastructure.Persistence.Seed;

namespace ProductShop.Infrastructure.Persistence.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.HasKey(r => r.Id);

            // We choose the ids ourselves (1 = Admin, 2 = User), the database must not generate them.
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Name).IsRequired().HasMaxLength(50);
            builder.HasIndex(r => r.Name).IsUnique();

            // Seed data: these rows are inserted by the migration itself.
            builder.HasData(SeedData.Roles);
        }
    }
}
```

📄 **`ProductShop.Infrastructure/Persistence/Configurations/UserRoleConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Persistence.Configurations
{
    public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
    {
        public void Configure(EntityTypeBuilder<UserRole> builder)
        {
            // Composite primary key: a user can have a role only once.
            builder.HasKey(ur => new { ur.UserId, ur.RoleId });

            // Delete a user -> their UserRole rows are deleted too.
            builder.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // A role that is still assigned to someone cannot be deleted.
            builder.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

📄 **`ProductShop.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name).IsRequired().HasMaxLength(Product.MaxNameLength);

            // numeric(18,2): up to 16 digits before the dot, exactly 2 after. Right type for money.
            builder.Property(p => p.Price).HasPrecision(18, 2);
        }
    }
}
```

---

## 4.5 Seed data

### Role গুলো (Migration insert করে)

📄 **`ProductShop.Infrastructure/Persistence/SeedData/SeedData.cs`**

```csharp
using ProductShop.Domain.Constants;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Persistence.Seed
{
    // Fixed rows inserted by the migration (HasData). Ids must NEVER change after a migration was applied.
    public static class SeedData
    {
        public static class RoleIds
        {
            public const int Admin = 1;
            public const int User = 2;
        }

        public static readonly Role[] Roles =
        {
            new Role { Id = RoleIds.Admin, Name = RoleNames.Admin },
            new Role { Id = RoleIds.User, Name = RoleNames.User }
        };
    }
}
```

### প্রথম Admin account (startup এ insert হয়)

Register সবসময় সাধারণ **User** account বানায় (না হলে যে কেউ নিজেকে admin বানিয়ে ফেলতে পারত)।
তাহলে প্রথম admin কে? আমরা সেটা `appsettings.Development.json` থেকে পড়ি:

```json
"AdminUser": {
  "Email": "admin@productshop.com",
  "Password": "Admin@12345",
  "FirstName": "Admin",
  "LastName": "User"
}
```

…এটাকে এই class এর সাথে bind করি…

📄 **`ProductShop.Infrastructure/Persistence/SeedData/AdminUserSettings.cs`**

```csharp
namespace ProductShop.Infrastructure.Persistence.Seed
{
    // Bound from the "AdminUser" section of appsettings.
    // On startup, DatabaseInitializer creates this admin account if it does not exist yet.
    public class AdminUserSettings
    {
        public const string SectionName = "AdminUser";

        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = "Admin";
        public string LastName { get; set; } = "User";
    }
}
```

…আর API start হওয়ার সময় `DatabaseInitializer` account টা বানিয়ে দেয় (শুধু যদি আগে থেকে না থাকে)।
`Database:ApplyMigrationsOnStartup` যদি `true` থাকে, তাহলে pending Migration গুলোও সে apply করে দেয়।

📄 **`ProductShop.Infrastructure/Persistence/DatabaseInitializer.cs`**

```csharp
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
```

---

## 4.6 Repositories — আসল implementation

প্রতিটা Repository:
1. তার constructor এ `ProductShopDbContext` পায় (Dependency Injection),
2. `ProductShop.Application/Interfaces` থেকে একটা Interface implement করে।

**5 লাইনে EF Core:**

```csharp
_context.Products.Add(product);          // prepare INSERT
_context.Products.Update(product);       // prepare UPDATE
_context.Products.Remove(product);       // prepare DELETE
await _context.SaveChangesAsync();       // send everything prepared to the database
await _context.Products.Where(p => p.Price > 10).ToListAsync();   // SELECT ... WHERE
```

📄 **`ProductShop.Infrastructure/Repositories/UserRepository.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Entities;
using ProductShop.Infrastructure.Persistence;

namespace ProductShop.Infrastructure.Repositories
{
    // The real implementation of IUserRepository, using EF Core.
    public class UserRepository : IUserRepository
    {
        private readonly ProductShopDbContext _context;

        public UserRepository(ProductShopDbContext context)
        {
            _context = context;
        }

        public Task<User?> GetByIdAsync(Guid id)
        {
            return UsersWithRoles().SingleOrDefaultAsync(u => u.Id == id);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            return UsersWithRoles().SingleOrDefaultAsync(u => u.Email == email);
        }

        public Task<bool> EmailExistsAsync(string email)
        {
            return _context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // Include + ThenInclude = SQL JOIN Users -> UserRoles -> Roles,
        // so user.UserRoles[i].Role.Name is filled in.
        private IQueryable<User> UsersWithRoles()
        {
            return _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role);
        }
    }
}
```

📄 **`ProductShop.Infrastructure/Repositories/RoleRepository.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Entities;
using ProductShop.Infrastructure.Persistence;

namespace ProductShop.Infrastructure.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly ProductShopDbContext _context;

        public RoleRepository(ProductShopDbContext context)
        {
            _context = context;
        }

        public Task<Role?> GetByNameAsync(string name)
        {
            return _context.Roles.SingleOrDefaultAsync(r => r.Name == name);
        }
    }
}
```

📄 **`ProductShop.Infrastructure/Repositories/ProductRepository.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Entities;
using ProductShop.Infrastructure.Persistence;

namespace ProductShop.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ProductShopDbContext _context;

        public ProductRepository(ProductShopDbContext context)
        {
            _context = context;
        }

        public Task<List<Product>> GetAllAsync()
        {
            // AsNoTracking: read-only list, EF does not need to watch these objects for changes (faster).
            return _context.Products
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(Guid id)
        {
            // FindAsync looks up by primary key.
            return await _context.Products.FindAsync(id);
        }

        public async Task AddAsync(Product product)
        {
            _context.Products.Add(product);       // INSERT is prepared
            await _context.SaveChangesAsync();    // ...and sent to PostgreSQL here
        }

        public async Task UpdateAsync(Product product)
        {
            _context.Products.Update(product);    // UPDATE
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Product product)
        {
            _context.Products.Remove(product);    // DELETE
            await _context.SaveChangesAsync();
        }
    }
}
```

---

## 4.7 `DependencyInjection.cs` — এই layer register করা

এটাই হলো Application এর Interface আর Infrastructure এর class এর মধ্যে **সংযোগ**:

```
services.AddScoped<IProductRepository, ProductRepository>();
                   ▲ যেটা চাওয়া হয়        ▲ যেটা পাওয়া যায়
```

এটা config থেকে `JwtSettings` (Step 5) আর `AdminUser` ও পড়ে।

📄 **`ProductShop.Infrastructure/DependencyInjection.cs`**

```csharp
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
```

---

## 4.8 Build

```bash
dotnet build ProductShop.Infrastructure
```

`Auth/` এর ভেতরের JWT / password class গুলো পরের ধাপে explain করা হবে।
**Migration** (আসল table গুলো বানানো) হবে Step 7 এ, `Program.cs` তৈরি হওয়ার পর।

✅ **Step 4 শেষ।** পরের ধাপ ➡️ [Step 5 — JWT authentication](05-jwt-authentication.md)
