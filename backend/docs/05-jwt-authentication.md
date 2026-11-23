# Step 5 — JWT Authentication, Password Hashing ও Roles

> **এই ধাপের লক্ষ্য:** User দের login করতে দেওয়া, তাদের একটা token দেওয়া, আর প্রতিটা request এ API যেন
> token টা আর User এর role (Admin / User) চিনতে পারে সেটা নিশ্চিত করা।
>
> ⬅️ [Step 4 — Infrastructure ও database](04-infrastructure-database.md) · পরের ধাপ ➡️ [Step 6 — API layer ও Program.cs](06-api-layer.md)

---

## 5.1 JWT login কীভাবে কাজ করে (বড় ছবিটা)

```
 Next.js (browser)                                   ASP.NET API
 ─────────────────                                   ───────────
 1. POST /api/auth/login  {email, password}  ───────►  AuthService.LoginAsync
                                                         • find user by email
                                                         • PasswordHasher.Verify
                                                         • JwtTokenService.CreateAccessToken
 2. ◄───────────────  { accessToken: "eyJ...", user: {..., roles:["Admin"]} }

 3. save accessToken (localStorage)

 4. GET /api/products
    Header:  Authorization: Bearer eyJ...    ───────►  UseAuthentication()  (JwtBearer)
                                                         • signature correct? (same Key)
                                                         • issuer / audience correct?
                                                         • not expired?
                                                         → builds "User" (ClaimsPrincipal)
                                                       UseAuthorization()
                                                         • [Authorize] → must be logged in
                                                         • [Authorize(Roles="Admin")] → role claim must be Admin
 5. ◄───────────────  200 OK [products]   or 401 (no/bad token)   or 403 (wrong role)
```

### একটা JWT এর ভেতরে কী থাকে?

একটা JWT হলো dot দিয়ে জোড়া লাগানো 3 টা Base64 অংশ: `HEADER.PAYLOAD.SIGNATURE`।
একটা token <https://jwt.io> এ paste করলেই payload টা দেখতে পাবেন, যেমন:

```json
{
  "sub": "c9a20d3f-eb33-43dc-bc81-1fbbe1f5fe61",
  "email": "admin@productshop.com",
  "given_name": "Admin",
  "family_name": "User",
  "role": "Admin",
  "iss": "ProductShop",
  "aud": "ProductShop.Api",
  "exp": 1794871650
}
```

- যে কেউ এটা **পড়তে** পারে (এটা encrypted না) → তাই ভেতরে কখনো কোনো secret রাখবেন না।
- কিন্তু কেউ এটা **বদলাতে** পারে না: SIGNATURE টা আমাদের secret `Key` দিয়ে বানানো। কেউ যদি
  `"role": "User"` কে edit করে `"Admin"` বানায়, তাহলে signature আর মিলবে না আর API **401** return করবে।

### 401 vs 403

| Code | মানে | উদাহরণ |
|---|---|---|
| **401 Unauthorized** | "আমি জানি না তুমি কে" | token নেই, token expired, ভুয়া token |
| **403 Forbidden** | "আমি জানি তুমি কে, কিন্তু তোমার অনুমতি নেই" | একজন সাধারণ User `POST /api/products` call করছে |

---

## 5.2 JWT settings (appsettings)

`ProductShop.Api/appsettings.Development.json` এ (TwitterClone এর মতো একই key):

```json
"JwtSettings": {
  "Issuer": "ProductShop",
  "Audience": "ProductShop.Api",
  "Key": "fylvmacUJ+KtZ1zyLR6wZVCJZa/jpSMsjyorTWupkm0rBUxnGOP2u/kzIH9vEbw/",
  "ExpiryTimeInMinutes": 60
}
```

| Setting | মানে |
|---|---|
| `Issuer` | token টা কে বানিয়েছে |
| `Audience` | token টা কার জন্য। TwitterClone একটা **আলাদা** Issuer/Audience ব্যবহার করে, তাই Key একই হলেও TwitterClone এর token এখানে reject হয়ে যায়। |
| `Key` | sign করার secret। **কমপক্ষে 32 character।** এটা যার কাছে থাকবে সে admin token বানাতে পারবে — তাই গোপন রাখুন। |
| `ExpiryTimeInMinutes` | এই সময় পার হলে token আর কাজ করবে না, User কে আবার login করতে হবে |

> নিজের random key বানাতে: `openssl rand -base64 48`

`appsettings.json` (যেটা সব environment এ ব্যবহার হয়) এ ইচ্ছা করেই `"Key": ""` রাখা হয়েছে: production
server কে নিজের key দিতে হবে (যেমন env var `JwtSettings__Key`)। Key যদি না থাকে বা
খুব ছোট হয়, তাহলে `Validate()` startup এই একটা পরিষ্কার error দিয়ে app থামিয়ে দেয়।

📄 **`ProductShop.Infrastructure/Auth/JwtSettings.cs`**

```csharp
using System.Text;

namespace ProductShop.Infrastructure.Auth
{
    // Bound from the "JwtSettings" section of appsettings.
    public class JwtSettings
    {
        public const string SectionName = "JwtSettings";

        public string Issuer { get; set; } = string.Empty;     // who created the token (this API)
        public string Audience { get; set; } = string.Empty;   // who the token is meant for
        public string Key { get; set; } = string.Empty;        // secret used to sign the token
        public int ExpiryTimeInMinutes { get; set; } = 60;

        // Fail at startup with a clear message instead of failing on the first login.
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Issuer))
                throw new InvalidOperationException($"{SectionName}:Issuer is not configured.");

            if (string.IsNullOrWhiteSpace(Audience))
                throw new InvalidOperationException($"{SectionName}:Audience is not configured.");

            // HMAC-SHA256 needs a key of at least 256 bits (32 bytes).
            if (Encoding.UTF8.GetByteCount(Key) < 32)
                throw new InvalidOperationException($"{SectionName}:Key must be at least 32 bytes long.");

            if (ExpiryTimeInMinutes <= 0)
                throw new InvalidOperationException($"{SectionName}:ExpiryTimeInMinutes must be positive.");
        }
    }
}
```

---

## 5.3 `JwtTokenService` — token বানানো

**Claims** = token এর ভেতরে লেখা তথ্যগুলো। আমরা `sub` (user id), `email`, নামগুলো, আর প্রতিটা role এর জন্য একটা করে
`role` claim যোগ করি।

📄 **`ProductShop.Infrastructure/Auth/JwtTokenService.cs`**

```csharp
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Constants;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Auth
{
    // Creates the JWT access token returned by register / login.
    public class JwtTokenService : ITokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly JsonWebTokenHandler _jwtHandler = new JsonWebTokenHandler();

        public JwtTokenService(JwtSettings jwtSettings)
        {
            _jwtSettings = jwtSettings;
        }

        public (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles)
        {
            // Claims = facts about the user written inside the token.
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),       // "sub": user id
                new Claim(JwtRegisteredClaimNames.Email, user.Email),             // "email"
                new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),     // "given_name"
                new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),     // "family_name"
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // unique token id
            };

            // "role": "Admin"  — [Authorize(Roles = "Admin")] reads this claim.
            claims.AddRange(roles.Select(role => new Claim(RoleNames.ClaimType, role)));

            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryTimeInMinutes);
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
            };

            return (_jwtHandler.CreateToken(descriptor), expiresAt);
        }
    }
}
```

---

## 5.4 `PasswordHasher` — আসল password কখনো store করবেন না

```
Register:  "Password123"  ──Hash──►  "AQAAAAIAAYagAAAAEJ3k...(random salt + hash)"   ← DB তে store হয়
Login:     "Password123"  + stored hash  ──Verify──►  true / false
```

- একই password থেকে প্রতিবার **আলাদা** hash আসে (random salt) — তাই একই password ওয়ালা দুইজন
  User এর hash কখনো এক হয় না।
- এটা ইচ্ছা করেই ধীর (অনেকগুলো iteration), যাতে attacker রা প্রতি সেকেন্ডে লাখ লাখ password guess করতে না পারে।
- আমরা ASP.NET Identity র battle-tested `PasswordHasher<T>` ব্যবহার করি (package `Microsoft.Extensions.Identity.Core`)।

📄 **`ProductShop.Infrastructure/Auth/PasswordHasher.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Auth
{
    // Wraps ASP.NET Identity's PasswordHasher (PBKDF2 + random salt + many iterations).
    // Hashing is ONE-WAY: you can check a password against a hash, but never get the password back.
    public class PasswordHasher : IPasswordHasher
    {
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

        public string Hash(User user, string password)
        {
            return _passwordHasher.HashPassword(user, password);
        }

        public bool Verify(User user, string passwordHash, string password)
        {
            var result = _passwordHasher.VerifyHashedPassword(user, passwordHash, password);

            return result != PasswordVerificationResult.Failed;
        }
    }
}
```

---

## 5.5 Token validate করা — `Program.cs` এ

Token বানানো হয় Infrastructure এ; কিন্তু token **check** করা হয় Api project এ,
কারণ এটা HTTP pipeline এর অংশ। এটা হলো `Program.cs` এর JWT অংশ (পুরো file Step 6 এ):

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSettings>((options, jwtSettings) =>      // ← same JwtSettings object that signs tokens
    {
        options.MapInboundClaims = false;                  // keep "sub", "email", "role" names as-is

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,          ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,  IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateLifetime = true,          ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Email,
            RoleClaimType = RoleNames.ClaimType   // ← "role": makes [Authorize(Roles = "Admin")] work
        };
    });
```

আর Middleware pipeline এ:

```csharp
app.UseAuthentication();   // reads + validates the token, fills "User"
app.UseAuthorization();    // checks [Authorize] / [Authorize(Roles = ...)]
```

> ⚠️ Order গুরুত্বপূর্ণ: `UseAuthentication()` অবশ্যই `UseAuthorization()` এর **আগে**।

---

## 5.6 Endpoint গুলোতে Role

```csharp
// Program.cs — every endpoint requires login by default:
builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter()));

// AuthController — register / login are open to everyone:
[AllowAnonymous]

// ProductsController — read: any logged-in user (default rule above)
[HttpGet]

// ProductsController — write: admins only
[Authorize(Roles = RoleNames.Admin)]
[HttpPost] / [HttpPut] / [HttpDelete]
```

| Endpoint | Token নেই | User token | Admin token |
|---|---|---|---|
| `POST /api/auth/register`, `/login` | ✅ | ✅ | ✅ |
| `GET /api/auth/me` | 401 | ✅ | ✅ |
| `GET /api/products`, `/api/products/{id}` | 401 | ✅ | ✅ |
| `POST/PUT/DELETE /api/products...` | 401 | **403** | ✅ |

### Controller এর ভেতরে logged-in User কে পড়া

`ControllerBase.User` হলো token থেকে বানানো `ClaimsPrincipal`। একটা ছোট extension id টা পড়ে দেয়:

📄 **`ProductShop.Api/Auth/ClaimsPrincipalExtensions.cs`**

```csharp
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ProductShop.Api.Auth
{
    public static class ClaimsPrincipalExtensions
    {
        // Inside a controller, "User" is the logged-in person built from the JWT.
        // This reads their id from the token's "sub" claim.
        public static Guid GetUserId(this ClaimsPrincipal principal)
        {
            var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (!Guid.TryParse(subject, out var userId))
            {
                throw new InvalidOperationException("The access token does not contain a valid user id.");
            }

            return userId;
        }
    }
}
```

---

## 5.7 Login/register এ Rate limiting

Password guess করার গতি কমাতে, register আর login এ **প্রতি IP থেকে প্রতি মিনিটে 10 টা request** allow করা হয়
(এর বেশি হলে: `429 Too Many Requests`)। নিয়মটা `Program.cs` এ define করা, আর
`[EnableRateLimiting(AuthRateLimit.PolicyName)]` দিয়ে লাগানো হয়।

📄 **`ProductShop.Api/Auth/AuthRateLimit.cs`**

```csharp
namespace ProductShop.Api.Auth
{
    // Name of the rate-limit rule used on register / login (defined in Program.cs).
    public static class AuthRateLimit
    {
        public const string PolicyName = "auth";
    }
}
```

---

## 5.8 এই project যা করে না (কিন্তু TwitterClone করে)

TwitterClone এ **refresh token** ও আছে (একটা দীর্ঘমেয়াদী দ্বিতীয় token, যেটা দিয়ে আবার login না করেই নতুন access token
পাওয়া যায়), logout, আর change-password ও আছে। ProductShop জিনিসটা সহজ রেখেছে: access token
expire হলে (60 min) frontend User কে আবার login page এ পাঠিয়ে দেয়।
পরে refresh token যোগ করতে চাইলে, TwitterClone থেকে `RefreshToken.cs`, `RefreshTokenRepository.cs` আর
`Refresh`/`Logout` method গুলো copy করে নিন।

✅ **Step 5 শেষ।** পরের ধাপ ➡️ [Step 6 — API layer ও Program.cs](06-api-layer.md)
