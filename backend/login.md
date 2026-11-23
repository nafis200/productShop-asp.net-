# 🔐 Login কীভাবে কাজ করে: appsettings.json থেকে Response পর্যন্ত

এই ডকুমেন্টে দেখানো হয়েছে, ফ্রন্টএন্ড `POST /api/auth/login` পাঠালে ব্যাকএন্ডে **কোন ফাইলে, কোন লাইনে, কী ঘটে**, আর শেষে কীভাবে response ফেরত যায়।

---

## 📖 কীভাবে পড়বেন

- পুরো লেখাটা **৩টা অংশে** ভাগ করা:
  - **অংশ A: অ্যাপ চালু হওয়ার সময় (মাত্র একবার)**: সেটিংস পড়া, সার্ভিস রেজিস্টার করা
  - **অংশ B: Login রিকোয়েস্ট আসার সময় (প্রতিবার)**: আসল login-এর কাজ
  - **অংশ C: Login-এর পরে**: টোকেন দিয়ে অন্য API কল করা
- প্রতিটা **ধাপ** নম্বর দেওয়া (ধাপ ১, ধাপ ২ ...)।
- কোডের ভেতরে `// ①`, `// ②` এরকম **নম্বর** দেওয়া আছে। কোডের নিচে সেই নম্বর ধরে ধরে ব্যাখ্যা দেওয়া হয়েছে।
- প্রতিটা ধাপের শুরুতে একটা চিহ্ন আছে, যেটা বলে কোডটা কার লেখা:

| চিহ্ন | মানে |
|------|------|
| 📘 **Framework** | Microsoft / ASP.NET Core / EF Core নিজে করে দেয়। আমরা শুধু ব্যবহার করি। (অফিশিয়াল ডকুমেন্টেশনে এর বর্ণনা আছে) |
| ✍️ **আমাদের কোড** | এই প্রজেক্টে আমরা নিজেরা লিখেছি। |

---

## 🗺️ পুরো চিত্র এক নজরে

```
                    ┌──────────────── অংশ A: অ্যাপ চালু (একবার) ────────────────┐
appsettings.json ──► Program.cs ──► AddApplication()  ──► IAuthService = AuthService
                         │      └──► AddInfrastructure() ──► DbContext, UserRepository,
                         │                                   JwtSettings, JwtTokenService,
                         │                                   PasswordHasher
                         └──► JWT Authentication, CORS, Middleware সাজানো
                    └──────────────────────────────────────────────────────────────┘

                    ┌──────────────── অংশ B: Login রিকোয়েস্ট (প্রতিবার) ─────────┐
Frontend ── POST /api/auth/login {email, password}
   │
   ▼
① CORS ──► ② Authentication ──► ③ Authorization ──► ④ Routing
   │
   ▼
⑤ AuthController.Login(LoginDto)      ◄── [ApiController] আগে LoginDto যাচাই করে (ভুল হলে 400)
   │
   ▼
⑥ AuthService.LoginAsync()
   ├──► ⑦ NormalizeEmail()            " Admin@Mail.COM " → "admin@mail.com"
   ├──► ⑧ UserRepository.GetByEmailAsync() ──► DbContext ──► PostgreSQL
   ├──► ⑨ PasswordHasher.Verify()     পাসওয়ার্ড ঠিক?  না → null → 401
   └──► ⑩ CreateAuthResponse()
           ├──► ⑪ user.ToDto()          (PasswordHash বাদ দিয়ে)
           └──► ⑫ JwtTokenService.CreateAccessToken()  → JWT টোকেন
   │
   ▼
⑬ return Ok(authResponse) ──► JSON ──► Frontend   (200 OK)
                    └──────────────────────────────────────────────────────────────┘
```

---

# অংশ A: অ্যাপ চালু হওয়ার সময় (মাত্র একবার)

> `dotnet run` দিলে এই অংশের কোড **একবারই** চলে। এখানে কোনো ইউজার login করছে না। আমরা শুধু "যন্ত্রপাতি" সাজিয়ে রাখছি, যাতে পরে login এলে সব রেডি থাকে।

---

## ধাপ ১: `appsettings.json` থেকে সেটিংস 📘 Framework + ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Api/appsettings.json](ProductShop.Api/appsettings.json) এবং [ProductShop.Api/appsettings.Development.json](ProductShop.Api/appsettings.Development.json)

```json
{
  "JwtSettings": {                                   // ①
    "Issuer": "ProductShop",                         // ②
    "Audience": "ProductShop.Api",                   // ③
    "Key": "<কমপক্ষে ৩২ অক্ষরের গোপন চাবি>",           // ④
    "ExpiryTimeInMinutes": 60                        // ⑤
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=...; Database=...; Username=...; Password=..."   // ⑥
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:3000" ]    // ⑦
  },
  "Database": {
    "ApplyMigrationsOnStartup": true                 // ⑧
  }
}
```

**ব্যাখ্যা:**

| নম্বর | কী | ব্যাখ্যা |
|------|----|---------|
| ① | `JwtSettings` | JWT টোকেন বানানো ও যাচাই করার সব সেটিংস এক জায়গায়। পরে এটা `JwtSettings` ক্লাসে বসানো হবে (ধাপ ৫)। |
| ② | `Issuer` | টোকেন **কে বানিয়েছে**। আমাদের API। |
| ③ | `Audience` | টোকেন **কার জন্য** বানানো। |
| ④ | `Key` | **গোপন চাবি**। এটা দিয়ে টোকেনে "সই" করা হয়। কেউ টোকেন বদলালে সই মিলবে না। ⚠️ এটা কখনো কাউকে দেখানো বা git-এ রাখা উচিত না। |
| ⑤ | `ExpiryTimeInMinutes` | টোকেন কত মিনিট কাজ করবে। এখানে ৬০ মিনিট। |
| ⑥ | `DefaultConnection` | PostgreSQL ডাটাবেসের ঠিকানা আর পাসওয়ার্ড। ইউজার খুঁজতে লাগবে। |
| ⑦ | `AllowedOrigins` | কোন ওয়েবসাইট (ফ্রন্টএন্ড) এই API কল করতে পারবে। |
| ⑧ | `ApplyMigrationsOnStartup` | অ্যাপ চালুর সময় ডাটাবেস টেবিল আপডেট করবে কিনা। |

**দুইটা ফাইল কেন?** 📘
ASP.NET প্রথমে `appsettings.json` পড়ে, তারপর `appsettings.Development.json` পড়ে। **দ্বিতীয়টার মান প্রথমটার উপরে বসে যায় (override)**। তাই `appsettings.json`-এ `"Key": ""` খালি থাকলেও Development মোডে আসল Key পাওয়া যায়।

---

## ধাপ ২: `Program.cs`: অ্যাপ বানানো শুরু 📘 Framework

**ফাইল:** [ProductShop.Api/Program.cs](ProductShop.Api/Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);                                       // ①

builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter())); // ②
```

**ব্যাখ্যা:**

- **①** `CreateBuilder` 📘 অনেক কাজ নিজে করে দেয়:
  - `appsettings.json` আর `appsettings.Development.json` পড়ে `builder.Configuration`-এ রাখে।
  - `builder.Services` নামে একটা **DI কন্টেইনার** (সার্ভিসের লিস্ট) তৈরি করে।
- **②** `AddControllers` 📘 কন্ট্রোলার চালু করে।
  `AuthorizeFilter()` ✍️ মানে: **সব API-তে টোকেন লাগবে**। শুধু যেখানে `[AllowAnonymous]` লেখা আছে (যেমন login), সেখানে টোকেন লাগবে না।
  > Login-এ তো ইউজারের কাছে এখনো টোকেন নেই, তাই login-এ `[AllowAnonymous]` দিতেই হবে (ধাপ ১১ দেখুন)।

---

## ধাপ ৩: `AddApplication()`: AuthService রেজিস্টার ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Api/Program.cs:53](ProductShop.Api/Program.cs#L53) → [ProductShop.Application/DependencyInjection.cs](ProductShop.Application/DependencyInjection.cs)

```csharp
// Program.cs
builder.Services.AddApplication();                                   // ①

// Application/DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddScoped<IAuthService, AuthService>();                 // ②
    services.AddScoped<IProductService, ProductService>();
    return services;                                                 // ③
}
```

**ব্যাখ্যা:**

- **①** Program.cs থেকে Application লেয়ারের রেজিস্ট্রেশন মেথড কল হয়।
- **②** DI কন্টেইনারকে বলা হচ্ছে: **"কেউ `IAuthService` চাইলে তাকে একটা `AuthService` দিও।"**
  - `AddScoped` 📘 = প্রতিটা HTTP রিকোয়েস্টের জন্য **একটা নতুন** অবজেক্ট। রিকোয়েস্ট শেষ হলে সেটা ফেলে দেওয়া হয়।
- **③** `services` ফেরত দেয়, যাতে চাইলে পরপর আরও মেথড ডাকা যায়।

---

## ধাপ ৪: `AddInfrastructure()`: ডাটাবেস, Repository, JWT, PasswordHasher ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Api/Program.cs:54](ProductShop.Api/Program.cs#L54) → [ProductShop.Infrastructure/DependencyInjection.cs](ProductShop.Infrastructure/DependencyInjection.cs)

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");        // ①

    if (string.IsNullOrWhiteSpace(connectionString))                                     // ②
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
    }

    services.AddDbContext<ProductShopDbContext>(options =>                               // ③
        options.UseNpgsql(connectionString, npgsql =>                                    // ④
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ProductShopDbContext.Schema)));  // ⑤

    services.AddScoped<IUserRepository, UserRepository>();                               // ⑥
    services.AddScoped<IRoleRepository, RoleRepository>();
    services.AddScoped<IProductRepository, ProductRepository>();

    var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() // ⑦
        ?? throw new InvalidOperationException($"The '{JwtSettings.SectionName}' configuration section is missing.");
    jwtSettings.Validate();                                                              // ⑧
    services.AddSingleton(jwtSettings);                                                  // ⑨

    services.AddScoped<ITokenService, JwtTokenService>();                                // ⑩
    services.AddScoped<IPasswordHasher, PasswordHasher>();                               // ⑪

    // ... (অ্যাডমিন সিড আর DatabaseInitializer — login-এর সাথে সরাসরি সম্পর্ক নেই)
    return services;
}
```

**ব্যাখ্যা:**

| নম্বর | কী হচ্ছে |
|------|---------|
| ① | 📘 `appsettings`-এর `ConnectionStrings:DefaultConnection` পড়ে (ধাপ ১-এর ⑥)। |
| ② | ✍️ খালি হলে অ্যাপ **শুরুতেই** বন্ধ হয়ে যায়, পরিষ্কার error দিয়ে। পরে রহস্যময় error পাওয়ার চেয়ে এটা ভালো। |
| ③ | 📘 `ProductShopDbContext`-কে DI-তে রেজিস্টার করে (ডিফল্টে Scoped)। |
| ④ | 📘 বলে দেয় ডাটাবেস **PostgreSQL** (Npgsql ড্রাইভার)। |
| ⑤ | ✍️ মাইগ্রেশনের হিসাবের টেবিলটাও আমাদের নিজের schema `product_shop`-এ রাখে। |
| ⑥ | ✍️ "কেউ `IUserRepository` চাইলে `UserRepository` দাও।" Login-এ ইউজার খুঁজতে এটা লাগবে। |
| ⑦ | 📘 `appsettings`-এর `"JwtSettings"` অংশ পড়ে **`JwtSettings` ক্লাসের অবজেক্টে** বসিয়ে দেয় (একে বলে **binding**)। JSON-এর `"Issuer"` → `jwtSettings.Issuer`, এভাবে নাম মিলিয়ে। |
| ⑧ | ✍️ সেটিংস ঠিক আছে কিনা চেক করে (ধাপ ৫ দেখুন)। |
| ⑨ | 📘 `AddSingleton` = পুরো অ্যাপে **একটাই** অবজেক্ট। কারণ সেটিংস বদলায় না। টোকেন **বানাতে** (JwtTokenService) আর **যাচাই করতে** (Program.cs) একই অবজেক্ট ব্যবহার হয়। |
| ⑩ | ✍️ "কেউ `ITokenService` চাইলে `JwtTokenService` দাও।" টোকেন বানাবে। |
| ⑪ | ✍️ "কেউ `IPasswordHasher` চাইলে `PasswordHasher` দাও।" পাসওয়ার্ড চেক করবে। |

> 💡 **Interface কেন?** `AuthService` শুধু `IUserRepository`, `ITokenService` এসব interface চেনে, আসল ক্লাস চেনে না। ফলে Application লেয়ার ডাটাবেস বা JWT লাইব্রেরির উপর নির্ভর করে না। এটাকে **Clean Architecture** বলে।

---

## ধাপ ৫: `JwtSettings` ক্লাস: সেটিংস চেক করা ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Infrastructure/Auth/JwtSettings.cs](ProductShop.Infrastructure/Auth/JwtSettings.cs)

```csharp
public class JwtSettings
{
    public const string SectionName = "JwtSettings";                 // ①

    public string Issuer { get; set; } = string.Empty;               // ②
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpiryTimeInMinutes { get; set; } = 60;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))                       // ③
            throw new InvalidOperationException(SectionName + ":Issuer is not configured.");

        if (string.IsNullOrWhiteSpace(Audience))                     // ④
            throw new InvalidOperationException(SectionName + ":Audience is not configured.");

        int keyLengthInBytes = Encoding.UTF8.GetByteCount(Key);      // ⑤
        if (keyLengthInBytes < 32)
            throw new InvalidOperationException(SectionName + ":Key must be at least 32 bytes long.");

        if (ExpiryTimeInMinutes <= 0)                                // ⑥
            throw new InvalidOperationException(SectionName + ":ExpiryTimeInMinutes must be positive.");
    }
}
```

**ব্যাখ্যা:**

- **①** `appsettings.json`-এ যে নামে অংশটা আছে, সেই নাম। ধাপ ৪-এর ⑦-এ এটা ব্যবহার হয়।
- **②** প্রতিটা property-র নাম JSON-এর key-র সাথে **হুবহু মিলতে হবে**, তবেই binding কাজ করে।
- **③ ④** Issuer বা Audience খালি হলে অ্যাপ চালুই হবে না।
- **⑤** Key কমপক্ষে **৩২ byte** হতে হবে, কারণ `HmacSha256` অ্যালগরিদম ২৫৬ bit (= ৩২ byte)-এর কম চাবি নেয় না।
- **⑥** মেয়াদ ০ বা নেগেটিভ হলে চলবে না।

---

## ধাপ ৬: JWT Authentication চালু করা 📘 Framework + ✍️ আমাদের সেটিংস

**ফাইল:** [ProductShop.Api/Program.cs:58-96](ProductShop.Api/Program.cs#L58-L96)

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)           // ①
    .AddJwtBearer();                                                      // ②

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSettings>((options, jwtSettings) =>                    // ③
    {
        options.MapInboundClaims = false;                                 // ④

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,                                        // ⑤
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateIssuerSigningKey = true,                              // ⑥
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

            ValidateLifetime = true,                                      // ⑦
            ClockSkew = TimeSpan.FromSeconds(30),

            NameClaimType = JwtRegisteredClaimNames.Email,                // ⑧
            RoleClaimType = RoleNames.ClaimType                           // ⑨
        };
    });

builder.Services.AddAuthorization();                                      // ⑩
```

> ⚠️ **গুরুত্বপূর্ণ:** Login-এর সময় এই কোড টোকেন **যাচাই করে না**, কারণ login রিকোয়েস্টে টোকেন থাকেই না। এটা কাজে লাগে **login-এর পরে** (অংশ C)। তবে এখানে যে সেটিংস দেওয়া হচ্ছে, login-এ বানানো টোকেনকে ঠিক সেই সেটিংসের সাথেই মিলতে হবে।

**ব্যাখ্যা:**

| নম্বর | কী হচ্ছে |
|------|---------|
| ① | 📘 বলে দেয় ডিফল্ট লগইন-পদ্ধতি হলো **JWT Bearer**। |
| ② | 📘 `Authorization: Bearer <token>` হেডার পড়ার handler যোগ করে। |
| ③ | ✍️ DI থেকে `JwtSettings` (ধাপ ৪-এর ⑨) নিয়ে যাচাইয়ের নিয়ম সাজায়। ফলে **বানানো আর যাচাই একই সেটিংসে** হয়। |
| ④ | ✍️ টোকেনের claim-এর নাম (`sub`, `email`, `role`) যেমন আছে তেমনই রাখে। না দিলে .NET এগুলোকে লম্বা URL-এর মতো নামে বদলে ফেলত। |
| ⑤ | টোকেনের Issuer আর Audience আমাদের সেটিংসের সাথে মিলতে হবে। |
| ⑥ | একই গোপন Key দিয়ে সই যাচাই। শুধু `HmacSha256` গ্রহণযোগ্য। |
| ⑦ | মেয়াদ পার হলে টোকেন বাতিল। দুই সার্ভারের ঘড়িতে সামান্য পার্থক্য থাকতে পারে, তাই ৩০ সেকেন্ড ছাড়। |
| ⑧ | `User.Identity.Name` = টোকেনের `email` claim। |
| ⑨ | `[Authorize(Roles = "Admin")]` রোল খুঁজবে `"role"` claim-এ। |
| ⑩ | 📘 Authorization (অনুমতি যাচাই) সার্ভিস চালু। |

---

## ধাপ ৭: CORS: ফ্রন্টএন্ডকে অনুমতি দেওয়া 📘 Framework + ✍️ আমাদের সেটিংস

**ফাইল:** [ProductShop.Api/Program.cs:101-109](ProductShop.Api/Program.cs#L101-L109)

```csharp
const string FrontendCorsPolicy = "Frontend";                                           // ①
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();        // ②
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)                                                    // ③
        .AllowAnyHeader()                                                               // ④
        .AllowAnyMethod());                                                             // ⑤
});
```

**ব্যাখ্যা:**

- **①** পলিসির একটা নাম। পরে `app.UseCors(...)`-এ এই নাম ব্যবহার হবে।
- **②** `appsettings`-এর `Cors:AllowedOrigins` থেকে `["http://localhost:3000"]` পড়ে।
- **③** শুধু এই ঠিকানা (Next.js ফ্রন্টএন্ড) থেকে আসা কল ব্রাউজার অনুমতি দেবে।
- **④** যেকোনো হেডার চলবে (যেমন `Content-Type`, `Authorization`)।
- **⑤** যেকোনো মেথড চলবে (GET, POST, PUT, DELETE)।

> 💡 CORS না থাকলে `localhost:3000` থেকে `localhost:5xxx`-এ login কল করলে **ব্রাউজার** response আটকে দিত।

---

## ধাপ ৮: Middleware পাইপলাইন সাজানো 📘 Framework

**ফাইল:** [ProductShop.Api/Program.cs:111-147](ProductShop.Api/Program.cs#L111-L147)

```csharp
var app = builder.Build();                 // ①

// ... (DatabaseInitializer: মাইগ্রেশন + অ্যাডমিন ইউজার তৈরি)               // ②

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();                      // ③
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCorsPolicy);           // ④
app.UseAuthentication();                   // ⑤
app.UseAuthorization();                    // ⑥
app.MapControllers();                      // ⑦

app.Run();                                 // ⑧
```

**ব্যাখ্যা:**

- **①** এ পর্যন্ত যা রেজিস্টার করা হয়েছে, সব দিয়ে আসল অ্যাপ তৈরি। এরপর আর নতুন সার্ভিস যোগ করা যায় না।
- **②** অ্যাপ চালুর সময় ডাটাবেস রেডি করে, আর `appsettings`-এর `AdminUser` দিয়ে একজন অ্যাডমিন বানায়। এই ইউজার দিয়ে login টেস্ট করা যায়।
- **③** ডেভেলপমেন্টে `/swagger` পেজ, যেখান থেকে login API টেস্ট করা যায়।
- **④ ⑤ ⑥ ⑦** প্রতিটা রিকোয়েস্ট **এই ক্রমেই** এগুলোর ভেতর দিয়ে যায়। **ক্রম খুব গুরুত্বপূর্ণ!** (অংশ B-এর ধাপ ১০-এ বিস্তারিত)
- **⑧** সার্ভার চালু হয়ে রিকোয়েস্টের জন্য অপেক্ষা করতে থাকে।

✅ **অংশ A শেষ।** সব যন্ত্রপাতি রেডি। এখন ইউজার login করার জন্য অপেক্ষা।

---

# অংশ B: Login রিকোয়েস্ট আসার সময় (প্রতিবার)

## ধাপ ৯: ফ্রন্টএন্ড রিকোয়েস্ট পাঠায় 🌐 Frontend

```http
POST /api/auth/login HTTP/1.1                    ①
Host: localhost:5xxx
Origin: http://localhost:3000                    ②
Content-Type: application/json                   ③

{                                                ④
  "email": "Admin@ProductShop.com",
  "password": "Admin@12345"
}
```

**ব্যাখ্যা:**

- **①** মেথড `POST`, ঠিকানা `/api/auth/login`।
- **②** ব্রাউজার নিজে বলে দেয় রিকোয়েস্ট কোন ওয়েবসাইট থেকে আসছে। CORS এটা দেখে।
- **③** body JSON ফরম্যাটে।
- **④** ইমেইল আর পাসওয়ার্ড। খেয়াল করুন: এখানে কোনো `Authorization` হেডার নেই, কারণ এখনো টোকেন নেই।

> ⚠️ প্রোডাকশনে সবসময় **HTTPS** ব্যবহার করতে হবে। নাহলে পাসওয়ার্ড খোলা অবস্থায় নেটওয়ার্কে যায়।

---

## ধাপ ১০: Middleware-এর ভেতর দিয়ে যাওয়া 📘 Framework

রিকোয়েস্টটা ধাপ ৮-এর ক্রম অনুযায়ী যায়:

| ক্রম | Middleware | Login রিকোয়েস্টে কী হয় |
|-----|-----------|------------------------|
| ① | `UseCors` | `Origin: http://localhost:3000` অনুমোদিত লিস্টে আছে → response-এ CORS হেডার যোগ হবে। |
| ② | `UseAuthentication` | `Authorization` হেডার খোঁজে। **নেই**, তাই ইউজার "অচেনা" (anonymous) থাকে। এটা কোনো error না। |
| ③ | `UseAuthorization` | দেখে endpoint-এ টোকেন লাগবে কিনা। Login-এ `[AllowAnonymous]` আছে → **যেতে দেয়**। |
| ④ | `MapControllers` | URL `/api/auth/login` + POST দেখে `AuthController.Login()` মেথড খুঁজে বের করে। |

> 💡 যদি `[AllowAnonymous]` না থাকত, তাহলে ধাপ ③-এ `AuthorizeFilter`-এর কারণে **401** ফেরত যেত, আর কেউ কখনো login করতে পারত না!

---

## ধাপ ১১: `AuthController` তৈরি হওয়া (Dependency Injection) 📘 Framework + ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Api/Controllers/AuthController.cs](ProductShop.Api/Controllers/AuthController.cs)

```csharp
[Route("api/[controller]")]                                // ①
[ApiController]                                            // ②
public class AuthController : ControllerBase               // ③
{
    private readonly IAuthService _authService;            // ④

    public AuthController(IAuthService authService)        // ⑤
    {
        _authService = authService;
    }
```

**ব্যাখ্যা:**

- **①** `[controller]` = ক্লাসের নাম থেকে "Controller" বাদ দিলে যা থাকে, অর্থাৎ `Auth`। তাই base URL `/api/auth`।
- **②** `[ApiController]` 📘 কয়েকটা জাদু করে:
  - JSON body নিজে থেকে C# অবজেক্টে রূপান্তর করে।
  - **মেথড চালানোর আগেই** DTO যাচাই করে। ভুল হলে নিজে **400 Bad Request** দিয়ে দেয় (ধাপ ১২)।
- **③** `ControllerBase` থেকে `Ok()`, `Unauthorized()` এসব হেল্পার মেথড পাওয়া যায়।
- **④ ⑤** কন্ট্রোলার `IAuthService` চায়। DI কন্টেইনার (ধাপ ৩) একটা `AuthService` বানিয়ে দেয়।

**DI পুরো চেইনটা এভাবে বানায় (প্রতি রিকোয়েস্টে):** 📘

```
AuthController
   └── IAuthService      → AuthService
          ├── IUserRepository → UserRepository
          │       └── ProductShopDbContext  (connection string সহ)
          ├── IRoleRepository → RoleRepository
          ├── IPasswordHasher → PasswordHasher
          └── ITokenService   → JwtTokenService
                  └── JwtSettings  (Singleton, অ্যাপ চালুর সময় একবারই বানানো)
```

আমাদের কোথাও `new AuthService(...)` লিখতে হয়নি। ASP.NET নিজে সব জোড়া লাগিয়ে দেয়।

---

## ধাপ ১২: `LoginDto`: JSON থেকে অবজেক্ট + যাচাই 📘 Framework + ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Application/Dtos/LoginDto.cs](ProductShop.Application/Dtos/LoginDto.cs)

```csharp
public class LoginDto
{
    [Required]                                         // ①
    [EmailAddress]                                     // ②
    public required string Email { get; set; }         // ③

    [Required]                                         // ④
    public required string Password { get; set; }
}
```

**ব্যাখ্যা:**

- **DTO** = Data Transfer Object। শুধু ডাটা আনা-নেওয়ার জন্য একটা সাধারণ ক্লাস।
- JSON-এর `"email"` → `Email`, `"password"` → `Password`। 📘 নামের বড়/ছোট হাতের অক্ষরের পার্থক্য ধরে না।
- **①** Email খালি হতে পারবে না।
- **②** Email-এর ফরম্যাট ঠিক থাকতে হবে (`@` থাকতে হবে)।
- **③** C#-এর `required` কীওয়ার্ড: JSON-এ `email` না থাকলে অবজেক্টই বানানো যাবে না।
- **④** Password খালি হতে পারবে না।

**যাচাই ব্যর্থ হলে** 📘 `[ApiController]` নিজে থেকে এমন response দেয়। আমাদের কোড চলেই না:

```json
// 400 Bad Request
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": [ "The Email field is not a valid e-mail address." ]
  }
}
```

---

## ধাপ ১৩: `Login()` অ্যাকশন মেথড ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Api/Controllers/AuthController.cs:39-55](ProductShop.Api/Controllers/AuthController.cs#L39-L55)

```csharp
[HttpPost("login")]                                                           // ①
[AllowAnonymous]                                                              // ②
[ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]      // ③
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> Login([FromBody] LoginDto loginDto)          // ④
{
    var authResponse = await _authService.LoginAsync(loginDto);               // ⑤

    if (authResponse is null)                                                 // ⑥
    {
        return Unauthorized(new { message = "Invalid email or password." });  // ⑦
    }

    return Ok(authResponse);                                                  // ⑧
}
```

**ব্যাখ্যা:**

| নম্বর | কী হচ্ছে |
|------|---------|
| ① | শুধু `POST` রিকোয়েস্ট, URL `/api/auth` + `/login` = `/api/auth/login`। |
| ② | টোকেন ছাড়াই ঢোকা যাবে (ধাপ ১০ দেখুন)। |
| ③ | শুধু Swagger ডকুমেন্টেশনের জন্য: কোন কোন response আসতে পারে। কোডের আচরণ বদলায় না। |
| ④ | `[FromBody]` = রিকোয়েস্টের JSON body থেকে `LoginDto` বানাও। `async` = ডাটাবেসের জন্য অপেক্ষার সময় থ্রেড আটকে থাকে না। |
| ⑤ | আসল কাজ `AuthService`-কে দিয়ে দেওয়া হলো (ধাপ ১৪)। কন্ট্রোলার নিজে কোনো লজিক করে না, শুধু HTTP সামলায়। |
| ⑥ | `null` মানে ইমেইল বা পাসওয়ার্ড ভুল। |
| ⑦ | **401 Unauthorized** + একটা মেসেজ ফেরত। |
| ⑧ | সফল হলে **200 OK** + টোকেন ও ইউজারের তথ্য। |

---

## ধাপ ১৪: `AuthService.LoginAsync()`: Login-এর মূল লজিক ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Application/Services/AuthService.cs:58-70](ProductShop.Application/Services/AuthService.cs#L58-L70)

```csharp
public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
{
    var user = await _userRepository.GetByEmailAsync(loginDto.Email.NormalizeEmail());   // ①

    if (user is null || !_passwordHasher.Verify(user, user.PasswordHash, loginDto.Password)) // ②
    {
        return null;                                                                       // ③
    }

    return CreateAuthResponse(user);                                                       // ④
}
```

**ব্যাখ্যা:**

- **①** দুটো কাজ একসাথে:
  - প্রথমে `NormalizeEmail()` দিয়ে ইমেইল পরিষ্কার করা (ধাপ ১৫)।
  - তারপর সেই ইমেইল দিয়ে ডাটাবেসে ইউজার খোঁজা (ধাপ ১৬)।
- **②** দুটো চেক, `||` (অথবা) দিয়ে:
  - `user is null` → এই ইমেইলের কোনো ইউজার নেই।
  - `!Verify(...)` → ইউজার আছে, কিন্তু পাসওয়ার্ড ভুল (ধাপ ১৭)।
  - 💡 `||`-এর বাম পাশ `true` হলে ডান পাশ আর চলে না। তাই ইউজার `null` হলে `user.PasswordHash` পড়তে গিয়ে crash হয় না।
- **③** দুই ক্ষেত্রেই **একই** `null` ফেরত দেয়। 🔒 **নিরাপত্তার কারণে**: হ্যাকার যেন বুঝতে না পারে কোন ইমেইল রেজিস্টার্ড আছে আর কোনটা নেই।
- **④** সব ঠিক থাকলে টোকেন সহ response বানানো (ধাপ ১৮)।

---

## ধাপ ১৫: `NormalizeEmail()`: ইমেইল পরিষ্কার করা ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Utilities/Extensions/StringExtensions.cs](ProductShop.Utilities/Extensions/StringExtensions.cs)

```csharp
public static string NormalizeEmail(this string email)    // ①
{
    return email.Trim().ToLowerInvariant();               // ②
}
```

**ব্যাখ্যা:**

- **①** এটা একটা **Extension Method** (`this string`)। তাই যেকোনো string-এর উপর `"...".NormalizeEmail()` লেখা যায়।
- **②** `Trim()` সামনে-পিছনের ফাঁকা জায়গা সরায়, `ToLowerInvariant()` সব ছোট হাতের করে।
  ```
  " Admin@ProductShop.COM "  →  "admin@productshop.com"
  ```
- Register-এর সময়ও এভাবেই সেভ করা হয়। তাই ইউজার বড় হাতের অক্ষরে ইমেইল লিখলেও login হবে।

---

## ধাপ ১৬: `UserRepository.GetByEmailAsync()`: ডাটাবেস থেকে ইউজার আনা ✍️ আমাদের কোড + 📘 EF Core

**ফাইল:** [ProductShop.Infrastructure/Repositories/UserRepository.cs](ProductShop.Infrastructure/Repositories/UserRepository.cs)

```csharp
public Task<User?> GetByEmailAsync(string email)
{
    return UsersWithRoles().SingleOrDefaultAsync(u => u.Email == email);   // ①
}

private IQueryable<User> UsersWithRoles()
{
    return _context.Users                                                 // ②
        .Include(u => u.UserRoles)                                        // ③
        .ThenInclude(ur => ur.Role);                                      // ④
}
```

**ব্যাখ্যা:**

- **②** `_context.Users` = `ProductShopDbContext`-এর `Users` টেবিল (schema `product_shop`)।
- **③** `Include` = ইউজারের সাথে তার `UserRoles` (কোন ইউজারের কোন রোল) সারিগুলোও আনো।
- **④** `ThenInclude` = প্রতিটা `UserRole`-এর সাথে আসল `Role` (নাম: "Admin"/"User") আনো।
  রোল লাগবে, কারণ টোকেনে রোল রাখতে হবে।
- **①** `SingleOrDefaultAsync` 📘:
  - ঠিক ১টা পেলে → সেই ইউজার।
  - ০টা পেলে → `null`।
  - ২টা পেলে → error। তবে `UserConfiguration`-এ ইমেইলে **Unique Index** আছে, তাই এটা কখনো হবে না।

📘 **EF Core এই LINQ থেকে প্রায় এরকম SQL বানিয়ে PostgreSQL-এ পাঠায়:**

```sql
SELECT u.*, ur.*, r.*
FROM product_shop."Users" AS u
LEFT JOIN product_shop."UserRoles" AS ur ON u."Id" = ur."UserId"
LEFT JOIN product_shop."Roles"     AS r  ON ur."RoleId" = r."Id"
WHERE u."Email" = @email;
```

**ডাটাবেস থেকে যে `User` অবজেক্ট আসে:**

```
User
 ├── Id           = 3f2a...-....   (Guid)
 ├── FirstName    = "Admin"
 ├── LastName     = "User"
 ├── Email        = "admin@productshop.com"
 ├── PasswordHash = "AQAAAAIAAYagAAAAE..."   ← আসল পাসওয়ার্ড না, hash
 └── UserRoles    = [ { RoleId = 1, Role = { Name = "Admin" } } ]
```

---

## ধাপ ১৭: `PasswordHasher.Verify()`: পাসওয়ার্ড মিলানো ✍️ আমাদের কোড + 📘 ASP.NET Identity

**ফাইল:** [ProductShop.Infrastructure/Auth/PasswordHasher.cs](ProductShop.Infrastructure/Auth/PasswordHasher.cs)

```csharp
private readonly PasswordHasher<User> _identityHasher;                      // ①

public bool Verify(User user, string passwordHash, string password)
{
    PasswordVerificationResult result =
        _identityHasher.VerifyHashedPassword(user, passwordHash, password); // ②

    if (result == PasswordVerificationResult.Failed)                        // ③
    {
        return false;
    }

    return true;                                                            // ④
}
```

**ব্যাখ্যা:**

- **①** 📘 ASP.NET Identity-র তৈরি hasher। ভেতরে **PBKDF2 + random salt + হাজার হাজার iteration** ব্যবহার করে। নিজে hash লেখার চেয়ে এটা অনেক নিরাপদ।
- **②** কীভাবে মেলায়?
  - ডাটাবেসের hash থেকে **salt** বের করে।
  - ইউজারের টাইপ করা পাসওয়ার্ড + সেই salt দিয়ে আবার hash বানায়।
  - দুটো hash মেলে কিনা দেখে।
  - 💡 Hash থেকে আসল পাসওয়ার্ড **কখনো ফেরত পাওয়া যায় না** (one-way)।
- **③** `Failed` → পাসওয়ার্ড ভুল → `false`।
- **④** `Success` বা `SuccessRehashNeeded` → পাসওয়ার্ড ঠিক → `true`।

**ফলাফল:**
- `false` হলে → ধাপ ১৪-এর ③ → `null` → কন্ট্রোলার **401** দেয়।
- `true` হলে → পরের ধাপে।

---

## ধাপ ১৮: `CreateAuthResponse()`: Response তৈরি ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Application/Services/AuthService.cs:79-90](ProductShop.Application/Services/AuthService.cs#L79-L90)

```csharp
private AuthResponseDto CreateAuthResponse(User user)
{
    var userDto = user.ToDto();                                                  // ①
    var (token, expiresAt) = _tokenService.CreateAccessToken(user, userDto.Roles); // ②

    return new AuthResponseDto                                                   // ③
    {
        AccessToken = token,
        ExpiresAt = expiresAt,
        User = userDto
    };
}
```

**ব্যাখ্যা:**

- **①** `User` entity থেকে `UserDto` বানায় (ধাপ ১৯)।
- **②** টোকেন বানায় (ধাপ ২০)। `(token, expiresAt)` = **tuple**: একসাথে দুটো মান ফেরত পাওয়া।
- **③** সব একসাথে একটা `AuthResponseDto`-তে ভরে ফেরত দেয় (ধাপ ২১)।

> এই মেথড `private` আর Register-এও ব্যবহার হয়। তাই register করার পরেও সাথে সাথে টোকেন পাওয়া যায়।

---

## ধাপ ১৯: `ToDto()`: Entity থেকে DTO ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Application/Mappings/MappingExtensions.cs](ProductShop.Application/Mappings/MappingExtensions.cs)

```csharp
public static UserDto ToDto(this User user)
{
    return new UserDto
    {
        Id = user.Id,                                                  // ①
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),    // ②
        CreatedAt = user.CreatedAt
    };                                                                 // ③
}
```

**ব্যাখ্যা:**

- **①** দরকারি তথ্যগুলো কপি করা হয়।
- **②** `UserRoles` লিস্ট থেকে শুধু রোলের **নাম** বের করে: `["Admin"]`। এজন্যই ধাপ ১৬-এ `ThenInclude(ur => ur.Role)` লাগত, নাহলে `ur.Role` হতো `null`।
- **③** 🔒 খেয়াল করুন: **`PasswordHash` কপি করা হয়নি!** তাই hash কখনো ফ্রন্টএন্ডে যায় না। Entity সরাসরি না পাঠিয়ে DTO পাঠানোর এটাই মূল কারণ।

---

## ধাপ ২০: `JwtTokenService.CreateAccessToken()`: JWT টোকেন বানানো ✍️ আমাদের কোড + 📘 Microsoft.IdentityModel

**ফাইল:** [ProductShop.Infrastructure/Auth/JwtTokenService.cs](ProductShop.Infrastructure/Auth/JwtTokenService.cs)

### ২০.ক: মূল মেথড

```csharp
public (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles)
{
    List<Claim> claims = BuildClaims(user, roles);                                   // ①

    DateTime expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryTimeInMinutes); // ②

    SigningCredentials signingCredentials = BuildSigningCredentials();               // ③

    SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor();         // ④
    tokenDescriptor.Issuer = _jwtSettings.Issuer;
    tokenDescriptor.Audience = _jwtSettings.Audience;
    tokenDescriptor.Subject = new ClaimsIdentity(claims);
    tokenDescriptor.Expires = expiresAt;
    tokenDescriptor.SigningCredentials = signingCredentials;

    string token = _tokenHandler.CreateToken(tokenDescriptor);                       // ⑤

    return (token, expiresAt);                                                       // ⑥
}
```

| নম্বর | কী হচ্ছে |
|------|---------|
| ① | টোকেনের ভেতরে ইউজারের কী কী তথ্য থাকবে (ধাপ ২০.খ)। |
| ② | মেয়াদ = এখন (UTC) + ৬০ মিনিট (ধাপ ১-এর ⑤)। |
| ③ | গোপন Key দিয়ে সই করার যন্ত্র (ধাপ ২০.গ)। |
| ④ | টোকেনের "নকশা": কে বানাল (Issuer), কার জন্য (Audience), কী তথ্য (Subject), মেয়াদ, সই। মানগুলো ধাপ ১-এর `appsettings.json` থেকেই আসছে। |
| ⑤ | 📘 `JsonWebTokenHandler` নকশা থেকে আসল টোকেন string বানায়। |
| ⑥ | টোকেন আর মেয়াদ ফেরত। |

### ২০.খ: `BuildClaims()`: টোকেনের ভেতরের তথ্য

```csharp
private List<Claim> BuildClaims(User user, IEnumerable<string> roles)
{
    List<Claim> claims = new List<Claim>();

    claims.Add(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));          // ①
    claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));                // ②
    claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName));        // ③
    claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName));        // ④
    claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));   // ⑤

    foreach (string role in roles)
    {
        claims.Add(new Claim(RoleNames.ClaimType, role));                            // ⑥
    }

    return claims;
}
```

| নম্বর | Claim নাম | মান | কেন |
|------|----------|-----|-----|
| ① | `sub` | ইউজারের Id | **সবচেয়ে গুরুত্বপূর্ণ।** পরে `/me`-তে এটা দিয়েই ইউজার চেনা হয় (অংশ C)। |
| ② | `email` | ইমেইল | `User.Identity.Name` হিসেবে ব্যবহার হয় (ধাপ ৬-এর ⑧)। |
| ③ | `given_name` | নামের প্রথম অংশ | ফ্রন্টএন্ডে দেখানোর জন্য। |
| ④ | `family_name` | নামের শেষ অংশ | ফ্রন্টএন্ডে দেখানোর জন্য। |
| ⑤ | `jti` | নতুন Guid | প্রতিটা টোকেনের আলাদা ইউনিক Id। |
| ⑥ | `role` | "Admin" / "User" | `[Authorize(Roles = "Admin")]` এটা দেখেই অনুমতি দেয়। |

> ⚠️ টোকেন **এনক্রিপ্টেড না**, শুধু **সই করা**। jwt.io-তে যে কেউ পড়তে পারে। তাই এখানে কখনো পাসওয়ার্ড বা গোপন তথ্য রাখা যাবে না।

### ২০.গ: `BuildSigningCredentials()`: সই করার যন্ত্র

```csharp
private SigningCredentials BuildSigningCredentials()
{
    byte[] keyBytes = Encoding.UTF8.GetBytes(_jwtSettings.Key);                      // ①
    SymmetricSecurityKey securityKey = new SymmetricSecurityKey(keyBytes);           // ②
    SigningCredentials credentials =
        new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);          // ③
    return credentials;
}
```

- **①** `appsettings`-এর Key (string) → byte-এ রূপান্তর।
- **②** **Symmetric** = একই Key দিয়ে সই **এবং** যাচাই দুটোই হয়। (ধাপ ৬-এর ⑥-এও ঠিক এই Key ব্যবহার হয়।)
- **③** `HmacSha256` অ্যালগরিদম দিয়ে সই।

### টোকেন দেখতে কেমন

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9 . eyJzdWIiOiIzZjJhLi4uIiwiZW1haWwiOi... . SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
└────────── ১. Header ──────────┘   └──────────── ২. Payload ────────────┘   └──────────── ৩. Signature ────────────┘
   {"alg":"HS256","typ":"JWT"}         {"sub":"3f2a...","email":"admin@...",       গোপন Key দিয়ে Header+Payload-এর সই
                                         "role":"Admin","exp":...,"iss":...}
```

---

## ধাপ ২১: `AuthResponseDto`: যে অবজেক্ট ফেরত যাবে ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Application/Dtos/AuthResponseDto.cs](ProductShop.Application/Dtos/AuthResponseDto.cs) ও [ProductShop.Application/Dtos/UserDto.cs](ProductShop.Application/Dtos/UserDto.cs)

```csharp
public class AuthResponseDto
{
    public string TokenType => "Bearer";                   // ①
    public required string AccessToken { get; set; }       // ②
    public required DateTime ExpiresAt { get; set; }       // ③
    public required UserDto User { get; set; }             // ④
}
```

- **①** সবসময় `"Bearer"`। ফ্রন্টএন্ডকে বলে দেয় হেডারে কীভাবে পাঠাতে হবে: `Authorization: Bearer <token>`।
- **②** JWT টোকেন (ধাপ ২০)।
- **③** কখন মেয়াদ শেষ। ফ্রন্টএন্ড এটা দেখে আগেভাগে বুঝতে পারে কখন আবার login করাতে হবে।
- **④** ইউজারের তথ্য (ধাপ ১৯)। ফ্রন্টএন্ড আলাদা কল না করেই নাম/রোল দেখাতে পারে।

---

## ধাপ ২২: `Ok(authResponse)` → JSON → Frontend 📘 Framework

ধাপ ১৩-এর ⑧-এ `return Ok(authResponse);` হলে:

1. 📘 ASP.NET `System.Text.Json` দিয়ে অবজেক্টকে JSON বানায়।
2. 📘 property-র নাম **camelCase** হয়ে যায়: `AccessToken` → `accessToken`।
3. 📘 `DateTime` → ISO ফরম্যাট (`"2026-11-23T11:00:00Z"`)।
4. 📘 CORS middleware (ধাপ ১০-এর ①) response-এ `Access-Control-Allow-Origin` হেডার যোগ করে।
5. রিকোয়েস্টের শেষে Scoped অবজেক্টগুলো (`AuthService`, `DbContext` ...) ফেলে দেওয়া হয়।

### ✅ সফল হলে: `200 OK`

```json
{
  "tokenType": "Bearer",
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOi...",
  "expiresAt": "2026-11-23T11:00:00Z",
  "user": {
    "id": "3f2a1c9e-....-....-....-............",
    "firstName": "Admin",
    "lastName": "User",
    "email": "admin@productshop.com",
    "roles": [ "Admin" ],
    "createdAt": "2026-11-01T08:30:00Z"
  }
}
```

### ❌ ইমেইল/পাসওয়ার্ড ভুল হলে: `401 Unauthorized` (ধাপ ১৩-এর ⑦)

```json
{
  "message": "Invalid email or password."
}
```

### ❌ ইনপুট ফরম্যাট ভুল হলে: `400 Bad Request` (ধাপ ১২)

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Email": [ "The Email field is not a valid e-mail address." ] }
}
```

---

# অংশ C: Login-এর পরে টোকেন ব্যবহার

ফ্রন্টএন্ড `accessToken` রেখে দেয়, আর পরের প্রতিটা রিকোয়েস্টে হেডারে পাঠায়:

```http
GET /api/auth/me HTTP/1.1
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOi...
```

## ধাপ ২৩: টোকেন যাচাই 📘 Framework (ধাপ ৬-এর সেটিংস দিয়ে)

`UseAuthentication` middleware এবার টোকেন **পায়**, আর যাচাই করে:

| ক্রম | চেক | ব্যর্থ হলে |
|-----|-----|-----------|
| ① | সই কি আমাদের Key দিয়ে করা? (কেউ টোকেন বদলায়নি তো?) | 401 |
| ② | অ্যালগরিদম কি `HS256`? | 401 |
| ③ | Issuer = `"ProductShop"`? | 401 |
| ④ | Audience = `"ProductShop.Api"`? | 401 |
| ⑤ | মেয়াদ শেষ হয়নি তো? (+৩০ সেকেন্ড ছাড়) | 401 |

সব ঠিক থাকলে টোকেনের claims থেকে কন্ট্রোলারের `User` প্রপার্টি (`ClaimsPrincipal`) তৈরি হয়।

## ধাপ ২৪: `/me`: টোকেন থেকে ইউজার চেনা ✍️ আমাদের কোড

**ফাইল:** [ProductShop.Api/Controllers/AuthController.cs:58-73](ProductShop.Api/Controllers/AuthController.cs#L58-L73) ও [ProductShop.Api/Auth/ClaimsPrincipalExtensions.cs](ProductShop.Api/Auth/ClaimsPrincipalExtensions.cs)

```csharp
// AuthController
[HttpGet("me")]                                                                  // ①
public async Task<IActionResult> Me()
{
    var user = await _authService.GetUserByIdAsync(User.GetUserId());            // ②
    if (user is null) return NotFound();                                         // ③
    return Ok(user);                                                             // ④
}

// ClaimsPrincipalExtensions
public static Guid GetUserId(this ClaimsPrincipal principal)
{
    string? userIdText = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);  // ⑤
    if (!Guid.TryParse(userIdText, out Guid userId))                             // ⑥
        throw new InvalidOperationException("The access token does not contain a valid user id.");
    return userId;
}
```

- **①** এখানে `[AllowAnonymous]` **নেই**, তাই `AuthorizeFilter`-এর কারণে টোকেন বাধ্যতামূলক।
- **⑤** টোকেনের `sub` claim পড়ে। ধাপ ২০.খ-এর ①-এ আমরা এখানেই ইউজারের Id রেখেছিলাম।
- **⑥** string থেকে Guid-এ রূপান্তর।
- **②** সেই Id দিয়ে ডাটাবেস থেকে ইউজার আনা।
- **③** টোকেন ঠিক, কিন্তু এর মধ্যে ইউজার ডিলিট হয়ে গেছে → 404।
- **④** ইউজারের তথ্য ফেরত।

> 💡 খেয়াল করুন: এখানে আর পাসওয়ার্ড লাগছে না। **টোকেনই প্রমাণ** যে ইউজার আগে সঠিক পাসওয়ার্ড দিয়ে login করেছিল।

---

# 📋 সারাংশ

## Login-এ যে ফাইলগুলো জড়িত (ক্রম অনুযায়ী)

| # | ফাইল | লেয়ার | কাজ |
|---|------|-------|-----|
| 1 | [appsettings.json](ProductShop.Api/appsettings.json) / [appsettings.Development.json](ProductShop.Api/appsettings.Development.json) | Api | JWT, ডাটাবেস, CORS সেটিংস |
| 2 | [Program.cs](ProductShop.Api/Program.cs) | Api | সার্ভিস রেজিস্টার, JWT যাচাইয়ের নিয়ম, middleware ক্রম |
| 3 | [Application/DependencyInjection.cs](ProductShop.Application/DependencyInjection.cs) | Application | `IAuthService → AuthService` |
| 4 | [Infrastructure/DependencyInjection.cs](ProductShop.Infrastructure/DependencyInjection.cs) | Infrastructure | DbContext, Repository, JwtSettings, TokenService, PasswordHasher |
| 5 | [JwtSettings.cs](ProductShop.Infrastructure/Auth/JwtSettings.cs) | Infrastructure | JWT সেটিংস রাখা ও চেক করা |
| 6 | [AuthController.cs](ProductShop.Api/Controllers/AuthController.cs) | Api | HTTP রিকোয়েস্ট গ্রহণ, 200/401 ফেরত |
| 7 | [LoginDto.cs](ProductShop.Application/Dtos/LoginDto.cs) | Application | ইনপুট আকার ও যাচাই |
| 8 | [AuthService.cs](ProductShop.Application/Services/AuthService.cs) | Application | Login-এর মূল লজিক |
| 9 | [StringExtensions.cs](ProductShop.Utilities/Extensions/StringExtensions.cs) | Utilities | ইমেইল ছোট হাতের করা |
| 10 | [UserRepository.cs](ProductShop.Infrastructure/Repositories/UserRepository.cs) | Infrastructure | ডাটাবেস থেকে ইউজার + রোল আনা |
| 11 | [ProductShopDbContext.cs](ProductShop.Infrastructure/Persistence/ProductShopDbContext.cs) | Infrastructure | EF Core-এর ডাটাবেস সংযোগ |
| 12 | [User.cs](ProductShop.Domain/Entities/User.cs), [UserRole.cs](ProductShop.Domain/Entities/UserRole.cs), [Role.cs](ProductShop.Domain/Entities/Role.cs) | Domain | ডাটাবেস টেবিলের আকার |
| 13 | [PasswordHasher.cs](ProductShop.Infrastructure/Auth/PasswordHasher.cs) | Infrastructure | পাসওয়ার্ড মেলানো |
| 14 | [MappingExtensions.cs](ProductShop.Application/Mappings/MappingExtensions.cs) | Application | User → UserDto (hash বাদ দিয়ে) |
| 15 | [JwtTokenService.cs](ProductShop.Infrastructure/Auth/JwtTokenService.cs) | Infrastructure | JWT টোকেন বানানো |
| 16 | [AuthResponseDto.cs](ProductShop.Application/Dtos/AuthResponseDto.cs), [UserDto.cs](ProductShop.Application/Dtos/UserDto.cs) | Application | Response-এর আকার |
| 17 | [ClaimsPrincipalExtensions.cs](ProductShop.Api/Auth/ClaimsPrincipalExtensions.cs) | Api | Login-এর পরে টোকেন থেকে Id বের করা |

## 🔒 নিরাপত্তার যে বিষয়গুলো কোডে আছে

1. পাসওয়ার্ড কখনো সরাসরি সেভ হয় না, শুধু **hash** (PBKDF2 + salt)।
2. "ইমেইল নেই" আর "পাসওয়ার্ড ভুল" দুটোতেই **একই 401 মেসেজ**।
3. Response-এ **PasswordHash কখনো যায় না** (DTO ব্যবহারের কারণে)।
4. টোকেন **সই করা**, তাই কেউ বদলালে ধরা পড়ে। **মেয়াদও** আছে।
5. JWT Key ছোট হলে অ্যাপ **চালুই হয় না**।
6. শুধু অনুমোদিত ফ্রন্টএন্ড থেকে কল করা যায় (**CORS**)।

## 📘 অফিশিয়াল ডকুমেন্টেশন (আরও পড়ার জন্য)

| বিষয় | লিংক |
|------|------|
| Configuration (appsettings) | https://learn.microsoft.com/aspnet/core/fundamentals/configuration/ |
| Dependency Injection | https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection |
| Middleware ও তার ক্রম | https://learn.microsoft.com/aspnet/core/fundamentals/middleware/ |
| JWT Bearer Authentication | https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication |
| Model Validation (`[Required]`, `[ApiController]`) | https://learn.microsoft.com/aspnet/core/mvc/models/validation |
| CORS | https://learn.microsoft.com/aspnet/core/security/cors |
| EF Core (`Include`, `ThenInclude`) | https://learn.microsoft.com/ef/core/querying/related-data/eager |
