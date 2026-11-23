# Step 3 — Application Layer (DTOs, Interfaces, Services)

> **এই ধাপের লক্ষ্য:** app-এর "brain" লেখা — register, login,
> আর product create/read/update/delete করার সময় কী হয় — তবে এখনো **কোনো** database code না লিখে।
>
> ⬅️ [Step 2 — Domain layer](02-domain-layer.md) · পরের ধাপ ➡️ [Step 4 — Infrastructure & database](04-infrastructure-database.md)

---

## 3.1 এই layer-এর চারটা folder

| Folder | ভেতরে কী থাকে | সহজ ভাষায় মানে |
|---|---|---|
| `Dtos/` | `RegisterDto`, `ProductDto`, ... | **D**ata **T**ransfer **O**bjects: API-তে যে JSON **ঢোকে** আর API থেকে যে JSON **বের হয়**, তার হুবহু shape। |
| `Interfaces/` | `IProductRepository`, `IProductService`, ... | **Contracts**: code ছাড়া শুধু method-এর একটা list। "আমার *এমন কিছু* লাগবে যেটা এই কাজটা করতে পারে।" |
| `Services/` | `AuthService`, `ProductService` | Business rule গুলো। এরা interface গুলো ব্যবহার করে। |
| `Mappings/` | `MappingExtensions` | Entity → DTO তে convert করে। |

```bash
mkdir -p ProductShop.Application/Dtos ProductShop.Application/Interfaces \
         ProductShop.Application/Services ProductShop.Application/Mappings
```

### সরাসরি entity না দিয়ে DTO কেন?

`User` entity-তে `PasswordHash` আছে। Controller যদি entity টাই return করত, তাহলে hash টা browser-এ
চলে যেত। `UserDto`-তে শুধু সেই field গুলো থাকে যেগুলো আমরা দেখাতে **চাই**। তাছাড়া product create করার সময়
client যেন `Id` বা `CreatedAt` পাঠাতে না পারে — `CreateProductDto`-তে এগুলো রাখাই হয়নি।

### Interface কেন?

```
ProductService  ──uses──►  IProductRepository  (interface, in Application)
                                   ▲
                                   │ implements
                           ProductRepository   (class, in Infrastructure, talks to PostgreSQL)
```

`ProductService` কখনো PostgreSQL দেখে না। Step 6-এ (`Program.cs`) আমরা ASP.NET কে বলে দিই:
*"কারো যখন `IProductRepository` লাগবে, তাকে একটা `ProductRepository` দিয়ে দাও।"*
এটাকেই বলে **Dependency Injection (DI)**।

---

## 3.2 DTOs

### Validation attribute

`[Required]`, `[EmailAddress]`, `[StringLength]`, `[Range]` — এই attribute গুলো controller method run হওয়ার আগেই
ASP.NET **নিজে থেকেই check করে** (কারণ controller-এ `[ApiController]` আছে)।
JSON invalid হলে client একটা error list সহ `400 Bad Request` পায় — আপনাকে একটাও `if` লিখতে হয় না।

| Attribute | নিয়ম |
|---|---|
| `[Required]` | থাকতেই হবে, আর empty/whitespace হতে পারবে না |
| `[EmailAddress]` | দেখতে email-এর মতো হতে হবে |
| `[StringLength(100, MinimumLength = 8)]` | length 8 থেকে 100-এর মধ্যে |
| `[Range(0.01, 1_000_000)]` | number 0.01 থেকে 1,000,000-এর মধ্যে |

### Auth DTOs

📄 **`ProductShop.Application/Dtos/RegisterDto.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace ProductShop.Application.Dtos
{
    // JSON body of POST /api/auth/register
    public class RegisterDto
    {
        [Required]
        [StringLength(50)]
        public required string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public required string Password { get; set; }
    }
}
```

📄 **`ProductShop.Application/Dtos/LoginDto.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace ProductShop.Application.Dtos
{
    // JSON body of POST /api/auth/login
    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        public required string Password { get; set; }
    }
}
```

📄 **`ProductShop.Application/Dtos/UserDto.cs`**

```csharp
namespace ProductShop.Application.Dtos
{
    // What the API sends back about a user. Note: NO PasswordHash here.
    public class UserDto
    {
        public required Guid Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required List<string> Roles { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
```

register আর login যা return করে, সেটা হলো `AuthResponseDto`। উদাহরণ JSON:

```json
{
  "tokenType": "Bearer",
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-11-17T12:00:00Z",
  "user": { "id": "…", "firstName": "Nafis", "lastName": "Ahamed",
            "email": "nafis@mail.com", "roles": ["User"], "createdAt": "…" }
}
```

> ASP.NET নিজে থেকেই C#-এর `PascalCase` property name গুলোকে JSON-এর `camelCase`-এ convert করে দেয়।

📄 **`ProductShop.Application/Dtos/AuthResponseDto.cs`**

```csharp
namespace ProductShop.Application.Dtos
{
    // Returned by register and login. The frontend stores AccessToken and sends it
    // back in the header "Authorization: Bearer <AccessToken>" on every request.
    public class AuthResponseDto
    {
        public string TokenType => "Bearer";
        public required string AccessToken { get; set; }
        public required DateTime ExpiresAt { get; set; }
        public required UserDto User { get; set; }
    }
}
```

### Product DTOs

📄 **`ProductShop.Application/Dtos/ProductDto.cs`**

```csharp
namespace ProductShop.Application.Dtos
{
    // What the API sends back for one product.
    public class ProductDto
    {
        public required Guid Id { get; set; }
        public required string Name { get; set; }
        public required decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }
}
```

📄 **`ProductShop.Application/Dtos/CreateProductDto.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Dtos
{
    // JSON body of POST /api/products (admin only)
    public class CreateProductDto
    {
        [Required]
        [StringLength(Product.MaxNameLength)]
        public required string Name { get; set; }

        [Range(0.01, 1_000_000)]
        public required decimal Price { get; set; }
    }
}
```

📄 **`ProductShop.Application/Dtos/UpdateProductDto.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Dtos
{
    // JSON body of PUT /api/products/{id} (admin only)
    public class UpdateProductDto
    {
        [Required]
        [StringLength(Product.MaxNameLength)]
        public required string Name { get; set; }

        [Range(0.01, 1_000_000)]
        public required decimal Price { get; set; }
    }
}
```

---

## 3.3 Interfaces (contracts)

### `async` / `Task` কী?

Database-এর সাথে কথা বলতে সময় লাগে (network)। `async`/`await` থাকলে server একটা thread block করে বসে না থেকে,
অপেক্ষার সময়টায় **অন্য** request গুলো handle করতে পারে।

- `Task<User?>` = "একটা promise, যেটা পরে একটা `User` অথবা `null` দেবে" (JS-এর `Promise<User | null>`-এর মতো)।
- `await` = "এটার জন্য অপেক্ষা করো" (JS-এর `await`-এর মতোই)।
- নিয়ম অনুযায়ী, async method-এর নাম `Async` দিয়ে শেষ হয়।

### Repository interface (data access)

📄 **`ProductShop.Application/Interfaces/IUserRepository.cs`**

```csharp
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Interfaces
{
    // Contract: "something that can load and save users".
    // The real code (EF Core + PostgreSQL) lives in Infrastructure/Repositories/UserRepository.cs
    public interface IUserRepository
    {
        // Both Get methods also load the user's roles.
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email);
        Task AddAsync(User user);
    }
}
```

📄 **`ProductShop.Application/Interfaces/IRoleRepository.cs`**

```csharp
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Interfaces
{
    public interface IRoleRepository
    {
        Task<Role?> GetByNameAsync(string name);
    }
}
```

📄 **`ProductShop.Application/Interfaces/IProductRepository.cs`**

```csharp
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Interfaces
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(Guid id);
        Task AddAsync(Product product);
        Task UpdateAsync(Product product);
        Task DeleteAsync(Product product);
    }
}
```

### Security interface (Step 5-এ implement করা হবে)

📄 **`ProductShop.Application/Interfaces/IPasswordHasher.cs`**

```csharp
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
```

📄 **`ProductShop.Application/Interfaces/ITokenService.cs`**

```csharp
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
```

### Service interface (controller গুলো এগুলো ব্যবহার করে)

📄 **`ProductShop.Application/Interfaces/IAuthService.cs`**

```csharp
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
```

📄 **`ProductShop.Application/Interfaces/IProductService.cs`**

```csharp
using ProductShop.Application.Dtos;

namespace ProductShop.Application.Interfaces
{
    public interface IProductService
    {
        Task<List<ProductDto>> GetAllAsync();
        Task<ProductDto?> GetByIdAsync(Guid id);
        Task<ProductDto> CreateAsync(CreateProductDto createProductDto);

        // Returns null when the product does not exist.
        Task<ProductDto?> UpdateAsync(Guid id, UpdateProductDto updateProductDto);

        // Returns false when the product does not exist.
        Task<bool> DeleteAsync(Guid id);
    }
}
```

---

## 3.4 Mapping (Entity → DTO)

📄 **`ProductShop.Application/Mappings/MappingExtensions.cs`**

```csharp
using ProductShop.Application.Dtos;
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Mappings
{
    // Entity (database shape)  ->  DTO (JSON shape).
    // Keeps the controllers from ever returning an entity (e.g. User with PasswordHash).
    public static class MappingExtensions
    {
        // Requires user.UserRoles[i].Role to be loaded (the repositories do this with Include).
        public static UserDto ToDto(this User user)
        {
            return new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                CreatedAt = user.CreatedAt
            };
        }

        public static ProductDto ToDto(this Product product)
        {
            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                CreatedAt = product.CreatedAt,
                ModifiedAt = product.ModifiedAt
            };
        }
    }
}
```

---

## 3.5 Services (business rules)

### `AuthService`

**Register flow**

```
RegisterDto ─► normalize email ─► email already used? ──yes──► return null  (controller → 409 Conflict)
                                         │ no
                                         ▼
                        load "User" role from DB
                        hash the password
                        create User + UserRole
                        save  ─►  create JWT  ─►  AuthResponseDto
```

**Login flow**

```
LoginDto ─► find user by email ─► not found OR password wrong? ──► return null (controller → 401)
                                         │ ok
                                         ▼
                                  create JWT ─► AuthResponseDto
```

**Constructor** interface গুলো receive করে। আপনি কখনো নিজে `new AuthService(...)` লেখেন না —
`DependencyInjection.cs`-এর registration গুলো দেখে ASP.NET নিজেই এটা করে (Dependency Injection)।

📄 **`ProductShop.Application/Services/AuthService.cs`**

```csharp
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
```

### `ProductService`

সাধারণ CRUD (Create, Read, Update, Delete)। খেয়াল করুন: **এখানে কোনো role check নেই**। "শুধু admin create
করতে পারবে" — এটা একটা HTTP/security rule, আর এটা থাকে controller-এ (Step 6)। Service শুধু এটা নিয়ে চিন্তা করে যে
product টা *কীভাবে* create হবে।

📄 **`ProductShop.Application/Services/ProductService.cs`**

```csharp
using ProductShop.Application.Dtos;
using ProductShop.Application.Interfaces;
using ProductShop.Application.Mappings;
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Services
{
    // Business rules for products. Who is ALLOWED to call these (admin or not)
    // is decided in the controller with [Authorize(Roles = ...)].
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<List<ProductDto>> GetAllAsync()
        {
            var products = await _productRepository.GetAllAsync();

            return products.Select(p => p.ToDto()).ToList();
        }

        public async Task<ProductDto?> GetByIdAsync(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);

            return product?.ToDto();
        }

        public async Task<ProductDto> CreateAsync(CreateProductDto createProductDto)
        {
            var product = new Product
            {
                Name = createProductDto.Name.Trim(),
                Price = Math.Round(createProductDto.Price, 2)
            };

            await _productRepository.AddAsync(product);

            return product.ToDto();
        }

        public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductDto updateProductDto)
        {
            var product = await _productRepository.GetByIdAsync(id);

            if (product is null)
            {
                return null;
            }

            product.Name = updateProductDto.Name.Trim();
            product.Price = Math.Round(updateProductDto.Price, 2);
            product.MarkAsModified();

            await _productRepository.UpdateAsync(product);

            return product.ToDto();
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);

            if (product is null)
            {
                return false;
            }

            await _productRepository.DeleteAsync(product);

            return true;
        }
    }
}
```

---

## 3.6 `DependencyInjection.cs` — এই layer-এর service গুলো register করা

সব registration `Program.cs`-এ না রেখে, প্রতিটা layer-এর নিজের একটা extension method থাকে।
`Program.cs` শুধু `builder.Services.AddApplication();` call করে।

| Lifetime | মানে | কোথায় ব্যবহার করবেন |
|---|---|---|
| `AddSingleton` | পুরো app যতক্ষণ চলে, ততক্ষণ একটাই object | settings, stateless helper |
| `AddScoped` | **প্রতিটা HTTP request-এর জন্য** একটা object | service আর repository (এরা DbContext ব্যবহার করে, যেটা নিজেই scoped) |
| `AddTransient` | যতবার চাওয়া হবে, ততবার নতুন object | ছোট, হালকা জিনিস |

📄 **`ProductShop.Application/DependencyInjection.cs`**

```csharp
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
```

> ⚠️ VS Code যদি "Generate interface IServiceCollection" অপশন দেখায় — **ওটায় click করবেন না**। এটা একটা নকল
> interface বানিয়ে ফেলে আর build ভেঙে দেয়। আসল টা আসে
> `Microsoft.Extensions.DependencyInjection.Abstractions` package + `using Microsoft.Extensions.DependencyInjection;` থেকে।

---

## 3.7 Build

```bash
dotnet build ProductShop.Application
```

✅ **Step 3 শেষ।** পরের ধাপ ➡️ [Step 4 — Infrastructure & database](04-infrastructure-database.md)
