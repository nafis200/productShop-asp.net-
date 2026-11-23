# Step 6 — API Layer: Controllers, Program.cs, appsettings

> **এই ধাপের লক্ষ্য:** Service গুলোকে HTTP endpoint হিসেবে বাইরে expose করা, আর `Program.cs` এ
> **সবকিছু** একসাথে জোড়া লাগানো।
>
> ⬅️ [Step 5 — JWT authentication](05-jwt-authentication.md) · পরের ধাপ ➡️ [Step 7 — Migrations](07-migrations.md)

---

## 6.1 API layer আসলে কী?

এটাই একমাত্র project যেটা **run** হয়। এর কাজগুলো হলো:

1. Web server (Kestrel) চালু করা — `Program.cs`।
2. Settings পড়া — `appsettings.json` / `appsettings.Development.json`।
3. HTTP request কে C# method call এ রূপান্তর করা — **Controllers**।
4. C# এর result কে HTTP response এ রূপান্তর করা (status code + JSON)।

এখানে **কোনো business rule থাকে না** — Controller শুধু Service কে call করে।

```bash
mkdir -p ProductShop.Api/Auth ProductShop.Api/Controllers
```

---

## 6.2 একটা request কীভাবে সব file এর মধ্য দিয়ে যায়

উদাহরণ: Admin product form এ "Add" এ click করলো → `POST /api/products {"name":"Keyboard","price":49.99}`

```
Browser (Next.js)
   │  POST http://localhost:5000/api/products
   │  Authorization: Bearer eyJ...
   ▼
Program.cs middleware pipeline
   UseCors ─► UseRateLimiter ─► UseAuthentication (JWT → User) ─► UseAuthorization (Roles=Admin?) ─► MapControllers
   ▼
[Api]            ProductsController.Create(CreateProductDto)      ← [ApiController] already validated the JSON
   │ calls IProductService           (DI gives a ProductService — registered in Application/DependencyInjection.cs)
   ▼
[Application]    ProductService.CreateAsync()                      ← business rules: trim name, round price
   │ calls IProductRepository        (DI gives a ProductRepository — registered in Infrastructure/DependencyInjection.cs)
   ▼
[Infrastructure] ProductRepository.AddAsync()  → _context.Products.Add() → SaveChangesAsync()
   │
   ▼
[Database]       INSERT INTO product_shop."Products" (...)
   │
   ▼  back up the chain: Product entity → ToDto() → ProductDto
Controller returns CreatedAtAction(...)  →  201 Created + JSON
```

**Interface এর সাথে class টা কে connect করে?** `Program.cs` এ `AddApplication()` আর
`AddInfrastructure()` call করা হয়, যেগুলোর ভেতরে `services.AddScoped<IProductRepository, ProductRepository>()` এর মতো line আছে।
ASP.NET যখন `ProductsController` বানায়, তখন দেখে এর constructor এ `IProductService` লাগবে, তাই একটা
`ProductService` বানায়; তারপর দেখে *ওটার* আবার `IProductRepository` লাগবে, তাই একটা `ProductRepository` বানায়, যেটার আবার
`ProductShopDbContext` লাগবে, এভাবে চলতে থাকে। এই automatic chain টাকেই বলে **Dependency Injection container**।

---

## 6.3 Controller এর basics

| জিনিস | মানে |
|---|---|
| `[ApiController]` | DTO নিজে নিজেই validate করে (JSON ভুল হলে 400 return করে), body এর JSON automatically পড়ে |
| `[Route("api/[controller]")]` | URL এর prefix। `[controller]` = class এর নাম থেকে "Controller" বাদ দিলে যা থাকে → `api/products` |
| `[HttpGet]`, `[HttpPost("login")]`, `[HttpPut("{id:guid}")]` | HTTP method + URL এর বাকি অংশ |
| `[FromBody]` | এই parameter টা JSON body থেকে পড়ো |
| `[FromRoute]` | এটা URL থেকে পড়ো (`{id}`) |
| `[ProducesResponseType]` | শুধু Swagger এর documentation এর জন্য |
| `ControllerBase` এর helper গুলো | `Ok(x)` 200 · `Created(...)` 201 · `NoContent()` 204 · `BadRequest()` 400 · `Unauthorized()` 401 · `NotFound()` 404 · `Conflict()` 409 |

### `AuthController`

| Method | URL | Auth | Body | Success |
|---|---|---|---|---|
| POST | `/api/auth/register` | লাগবে না | `RegisterDto` | 201 + `AuthResponseDto` |
| POST | `/api/auth/login` | লাগবে না | `LoginDto` | 200 + `AuthResponseDto` |
| GET | `/api/auth/me` | token | – | 200 + `UserDto` |

📄 **`ProductShop.Api/Controllers/AuthController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductShop.Api.Auth;
using ProductShop.Application.Dtos;
using ProductShop.Application.Interfaces;

namespace ProductShop.Api.Controllers
{
    // [controller] is replaced by the class name without "Controller"  ->  /api/auth
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // POST /api/auth/register
        [HttpPost("register")]
        [AllowAnonymous]                                 // no token needed (you don't have one yet)
        [EnableRateLimiting(AuthRateLimit.PolicyName)]   // max 10 requests / minute / IP
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            var authResponse = await _authService.RegisterAsync(registerDto);

            if (authResponse is null)
            {
                return Conflict(new { message = $"A user with email '{registerDto.Email}' already exists." });
            }

            return Created("/api/auth/me", authResponse);
        }

        // POST /api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var authResponse = await _authService.LoginAsync(loginDto);

            if (authResponse is null)
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            return Ok(authResponse);
        }

        // GET /api/auth/me   (needs a token — the global AuthorizeFilter in Program.cs applies)
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Me()
        {
            var user = await _authService.GetUserByIdAsync(User.GetUserId());

            if (user is null)
            {
                // Token is valid, but the account was deleted after it was issued.
                return NotFound();
            }

            return Ok(user);
        }
    }
}
```

### `ProductsController`

| Method | URL | Auth | Body | Success |
|---|---|---|---|---|
| GET | `/api/products` | যেকোনো logged-in user | – | 200 + `ProductDto[]` |
| GET | `/api/products/{id}` | যেকোনো logged-in user | – | 200 + `ProductDto` |
| POST | `/api/products` | **Admin** | `CreateProductDto` | 201 + `ProductDto` |
| PUT | `/api/products/{id}` | **Admin** | `UpdateProductDto` | 200 + `ProductDto` |
| DELETE | `/api/products/{id}` | **Admin** | – | 204 |

📄 **`ProductShop.Api/Controllers/ProductsController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductShop.Application.Dtos;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Constants;

namespace ProductShop.Api.Controllers
{
    //  /api/products
    //  - Any logged-in user (Admin or User) can READ.
    //  - Only Admin can CREATE, UPDATE, DELETE.
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        // GET /api/products
        [HttpGet]
        [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _productService.GetAllAsync());
        }

        // GET /api/products/{id}
        // ":guid" = this route only matches if {id} is a valid Guid.
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var product = await _productService.GetByIdAsync(id);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        // POST /api/products
        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]   // logged in but not Admin -> 403 Forbidden
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateProductDto createProductDto)
        {
            var product = await _productService.CreateAsync(createProductDto);

            // 201 + "Location: /api/products/{id}" header + the product in the body.
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        // PUT /api/products/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = RoleNames.Admin)]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateProductDto updateProductDto)
        {
            var product = await _productService.UpdateAsync(id, updateProductDto);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        // DELETE /api/products/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = RoleNames.Admin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var isDeleted = await _productService.DeleteAsync(id);

            if (!isDeleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
```

---

## 6.4 `Program.cs` — যে file সবকিছু জোড়া লাগায়

এটাকে 3 ভাগে ভাগ করে পড়ো:

| ভাগ | Code | মানে |
|---|---|---|
| **1. Services** | `builder.Services.Add...` | "এই object গুলো আছে, আর এভাবে এগুলো বানাতে হয়" (DI registration, auth configuration, CORS, Swagger) |
| **2. Startup এর কাজ** | `DatabaseInitializer.InitializeAsync` | migration run করা + admin বানানো, একবারই |
| **3. Middleware pipeline** | `app.Use...` | যে ধাপগুলোর মধ্য দিয়ে **প্রতিটা request** যায়, **ক্রম অনুযায়ী** |

### CORS কেন লাগে?

Next.js চলে `http://localhost:3000` এ, আর API চলে `http://localhost:5000` এ। Port আলাদা মানে
**origin** ও আলাদা, আর browser এরকম call block করে দেয়, যদি না API response এ
`Access-Control-Allow-Origin: http://localhost:3000` পাঠায়। কোন কোন origin allowed সেটা appsettings এ আছে
(`Cors:AllowedOrigins`), তাই production এ শুধু তোমার আসল frontend URL টা add করে দিলেই হবে।

### Initializer এর জন্য `CreateScope()` কেন?

`DatabaseInitializer` আর `DbContext` হলো **Scoped** (সাধারণত প্রতি HTTP request এ একটা করে)। Startup এর সময় তো
কোনো request নেই, তাই আমরা নিজেরাই হাতে একটা scope বানাই, ব্যবহার করি, আর `using` সেটাকে dispose করে দেয়।

📄 **`ProductShop.Api/Program.cs`**

```csharp
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProductShop.Api.Auth;
using ProductShop.Application;
using ProductShop.Domain.Constants;
using ProductShop.Infrastructure;
using ProductShop.Infrastructure.Auth;
using ProductShop.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// =====================================================================
// PART 1 — SERVICES  ("what objects exist in the app")
// =====================================================================

// 1. Controllers. The AuthorizeFilter makes EVERY endpoint require a valid token,
//    except the ones marked [AllowAnonymous] (register, login).
builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter()));

// 2. Swagger (the test web page at /swagger), with an "Authorize" button for the JWT.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Paste the accessToken returned by /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// 3. Our own layers. Each layer registers its own classes (see their DependencyInjection.cs).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 4. JWT authentication: read "Authorization: Bearer <token>" and check the token.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Uses the SAME JwtSettings object that JwtTokenService uses to sign tokens,
// so signing and validating can never get out of sync.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSettings>((options, jwtSettings) =>
    {
        // Keep claim names exactly as written in the token ("sub", "email", "role").
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Email,
            // Tells [Authorize(Roles = "Admin")] which claim holds the roles.
            RoleClaimType = RoleNames.ClaimType
        };
    });

builder.Services.AddAuthorization();

// 5. CORS: the browser blocks calls from http://localhost:3000 (Next.js) to this API
//    unless the API says that origin is allowed.
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// 6. Rate limiting: slows down password guessing on register/login. 10 requests per minute per IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthRateLimit.PolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// =====================================================================
// PART 2 — STARTUP WORK: apply migrations + create the admin user
// =====================================================================
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"));
}

// =====================================================================
// PART 3 — MIDDLEWARE PIPELINE  (every request passes through these, IN THIS ORDER)
// =====================================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCorsPolicy);   // must be before auth so preflight (OPTIONS) requests get CORS headers
app.UseRateLimiter();
app.UseAuthentication();           // "who are you?"  (reads the JWT)
app.UseAuthorization();            // "are you allowed?" ([Authorize], Roles)

app.MapControllers();              // finally, run the controller method

app.Run();
```

---

## 6.5 Settings file গুলো

ASP.NET প্রথমে `appsettings.json` load করে, তারপর তার উপরে `appsettings.{Environment}.json` load করে
(একই নামের value থাকলে সেটা **overwrite** হয়ে যায়)। `ASPNETCORE_ENVIRONMENT=Development` সেট করা আছে
`Properties/launchSettings.json` এ, তাই local এ দুটো file ই ব্যবহার হয়।

| Section | কে পড়ে |
|---|---|
| `ConnectionStrings:DefaultConnection` | `Infrastructure/DependencyInjection.cs` → `UseNpgsql` |
| `JwtSettings` | `Infrastructure/DependencyInjection.cs` → `JwtSettings` (signing) + `Program.cs` (validation) |
| `AdminUser` | `Infrastructure/DependencyInjection.cs` → `AdminUserSettings` → `DatabaseInitializer` |
| `Database:ApplyMigrationsOnStartup` | `Program.cs` |
| `Cors:AllowedOrigins` | `Program.cs` |

📄 **`ProductShop.Api/appsettings.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "JwtSettings": {
    "Issuer": "ProductShop",
    "Audience": "ProductShop.Api",
    "Key": "",
    "ExpiryTimeInMinutes": 60
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:3000" ]
  },
  "Database": {
    "ApplyMigrationsOnStartup": false
  }
}
```

📄 **`ProductShop.Api/appsettings.Development.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "JwtSettings": {
    "Issuer": "ProductShop",
    "Audience": "ProductShop.Api",
    "Key": "fylvmacUJ+KtZ1zyLR6wZVCJZa/jpSMsjyorTWupkm0rBUxnGOP2u/kzIH9vEbw/",
    "ExpiryTimeInMinutes": 60
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=ep-late-dream-b5clkohq-pooler.c-7.us-east-2.aws.neon.tech; Database=neondb; Username=neondb_owner; Password=npg_bTlCN6g5ZuUn; SSL Mode=VerifyFull; Channel Binding=Require;"
  },
  "Database": {
    "ApplyMigrationsOnStartup": true
  },
  "AdminUser": {
    "Email": "admin@productshop.com",
    "Password": "Admin@12345",
    "FirstName": "Admin",
    "LastName": "User"
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:3000" ]
  }
}
```

### Port — `Properties/launchSettings.json`

Generate হওয়া random port গুলো আমরা সহজ port এ বদলে দিয়েছি: **http → 5000**, https → 7000।
Frontend ব্যবহার করে `http://localhost:5000`।

📄 **`ProductShop.Api/Properties/launchSettings.json`**

```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:50773",
      "sslPort": 44371
    }
  },
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "https://localhost:7000;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
      "launchUrl": "swagger",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

---

## 6.6 Build

```bash
dotnet build
```

✅ **Step 6 শেষ।** পরের ধাপ ➡️ [Step 7 — Migrations](07-migrations.md)
