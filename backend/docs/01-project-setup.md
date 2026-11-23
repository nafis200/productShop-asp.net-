# Step 1 — Project Setup (Solution, Layer, Reference, Package)

> **এই ধাপের লক্ষ্য:** একটা খালি ASP.NET Core 8 solution বানানো যেটা কয়েকটা layer এ ভাগ করা
> (Clean Architecture), layer গুলোকে একটার সাথে আরেকটা যুক্ত করা, আর project এ যত NuGet package
> লাগবে সব install করে ফেলা।
>
> ⬅️ ফিরে যাও [README](../README.md) · পরের ধাপ ➡️ [Step 2 — Domain layer](02-domain-layer.md)

---

## 1.1 যা যা install করা থাকতে হবে

| Tool | Check command | কেন লাগবে |
|---|---|---|
| .NET 8 SDK | `dotnet --list-sdks` → অবশ্যই `8.0.x` দেখাতে হবে | C# compile আর run করে |
| EF Core CLI | `dotnet ef --version` | database migration বানায় |
| একটা PostgreSQL database | (আমরা Neon cloud Postgres ব্যবহার করছি) | user আর product store করে রাখে |

`dotnet ef` না থাকলে পুরো কম্পিউটারের জন্য একবার install করে নাও:

```bash
dotnet tool install --global dotnet-ef --version 8.0.2
```

---

## 1.2 শুরুতেই যে শব্দগুলো জানা দরকার

| শব্দ | সহজ মানে |
|---|---|
| **Solution (`.sln`)** | একটা "বাক্স" যেটা অনেকগুলো project একসাথে ধরে রাখে। VS Code / Visual Studio solution টা open করলেই সব project একসাথে দেখা যায়। |
| **Project (`.csproj`)** | একটা compiled unit (একটা `.dll`)। প্রতিটা layer আলাদা আলাদা project। |
| **classlib** | এমন project যেটা শুধু code, অন্য project গুলো ব্যবহার করবে (নিজে নিজে run হতে পারে না)। |
| **webapi** | এমন project যেটা একটা web server চালু করে আর HTTP request এর উত্তর দেয়। আমরা এটাকেই run করি। |
| **Reference** | "Project A, Project B এর class গুলো ব্যবহার করতে পারবে।" `dotnet add A reference B` দিয়ে যোগ করা হয়। |
| **NuGet package** | Internet থেকে download করা একটা library (`npm` package এর মতো)। |

---

## 1.3 Layer গুলো আর কে কাকে চেনে

```
                 ┌─────────────────────────┐
                 │     ProductShop.Api     │  Controllers, Program.cs  (the web server)
                 └───────────┬─────────────┘
                 ┌───────────┴─────────────┐
                 ▼                         ▼
     ┌──────────────────────┐   ┌──────────────────────────┐
     │ ProductShop.         │◄──│ ProductShop.             │
     │ Application          │   │ Infrastructure           │
     │ DTOs, Interfaces,    │   │ Database (EF Core),      │
     │ Services (business   │   │ Repositories, JWT,       │
     │ rules)               │   │ Password hashing         │
     └──────────┬───────────┘   └────────────┬─────────────┘
                └──────────────┬─────────────┘
                               ▼
                    ┌─────────────────────┐
                    │ ProductShop.Domain  │  Entities (User, Product, Role)
                    └─────────────────────┘
         ProductShop.Utilities: small helpers, usable by any layer
```

**Dependency rule (খুবই গুরুত্বপূর্ণ):**

| Layer | কাকে চেনে (reference করে) | কাজ |
|---|---|---|
| **Domain** | কাউকেই না | মূল data: `User`, `Product`, `Role`। একদম সাধারণ C# class। |
| **Application** | Domain, Utilities | App *কী* করে: Service, DTO আর **Interface** (contract), যেমন `IProductRepository`। |
| **Infrastructure** | Application, Domain, Utilities | কাজটা *কীভাবে* হয়: আসল database code যেটা ওই Interface গুলো **implement** করে, JWT বানানো, password hashing। |
| **Api** | সবাইকে | HTTP request নেয়, Service কে call করে, JSON ফেরত দেয়। `Program.cs` সবকিছু একসাথে জুড়ে দেয়। |

কেন এভাবে? Application layer বলে *"আমার এমন কিছু লাগবে যেটা একটা product save করতে পারে"* (একটা Interface)।
Infrastructure বলে *"আমি PostgreSQL এ product save করতে পারি"* (একটা class যেটা ওই Interface implement করে)।
কাল যদি SQL Server এ চলে যাও, তাহলে শুধু Infrastructure বদলাবে।

---

## 1.4 Folder আর solution বানাও

```bash
mkdir -p ProductShop/backend
cd ProductShop/backend

dotnet new sln -n ProductShop
```

ফলাফল: `ProductShop.sln` নামে একটা file।

---

## 1.5 প্রতিটা layer এর জন্য একটা করে project বানাও

```bash
dotnet new classlib -n ProductShop.Domain         -f net8.0
dotnet new classlib -n ProductShop.Application    -f net8.0
dotnet new classlib -n ProductShop.Infrastructure -f net8.0
dotnet new classlib -n ProductShop.Utilities      -f net8.0
dotnet new webapi   -n ProductShop.Api            -f net8.0 --use-controllers
```

`--use-controllers` দিলে Web API template টা "minimal APIs" এর বদলে **Controller** (class) ব্যবহার করে।

Template গুলো যে demo file বানায় সেগুলো delete করে দাও — আমাদের লাগবে না:

```bash
rm ProductShop.*/Class1.cs
rm ProductShop.Api/WeatherForecast.cs ProductShop.Api/Controllers/WeatherForecastController.cs
```

---

## 1.6 সব project solution এ যোগ করো

Project বানালেই সেটা solution এ ঢুকে যায় **না**। Solution কে প্রতিটার কথা আলাদা করে বলে দিতে হয়:

```bash
dotnet sln add ProductShop.Domain/ProductShop.Domain.csproj
dotnet sln add ProductShop.Application/ProductShop.Application.csproj
dotnet sln add ProductShop.Infrastructure/ProductShop.Infrastructure.csproj
dotnet sln add ProductShop.Utilities/ProductShop.Utilities.csproj
dotnet sln add ProductShop.Api/ProductShop.Api.csproj
```

---

## 1.7 Layer গুলো যুক্ত করো (project reference)

এই command গুলো উপরের dependency rule table টা হুবহু মেনে চলে:

```bash
# Application  ->  Domain, Utilities
dotnet add ProductShop.Application reference ProductShop.Domain ProductShop.Utilities

# Infrastructure  ->  Application, Domain, Utilities
dotnet add ProductShop.Infrastructure reference ProductShop.Application ProductShop.Domain ProductShop.Utilities

# Api  ->  Application, Infrastructure, Domain, Utilities
dotnet add ProductShop.Api reference ProductShop.Application ProductShop.Infrastructure ProductShop.Domain ProductShop.Utilities
```

প্রতিটা command `.csproj` file এ একটা `<ProjectReference ... />` line লিখে দেয়।
চাইলে `.csproj` open করে দেখতে পারো (এই page এর শেষে দেখানো আছে)।

---

## 1.8 NuGet package install করো

| Project | Package | কেন লাগবে |
|---|---|---|
| Application | `Microsoft.Extensions.DependencyInjection.Abstractions` | এটা দিয়ে একটা classlib `IServiceCollection` ব্যবহার করতে পারে, ফলে নিজের Service গুলো নিজেই register করতে পারে (`AddApplication()`) |
| Infrastructure | `Microsoft.Extensions.DependencyInjection.Abstractions` | একই কাজ, `AddInfrastructure()` এর জন্য |
| Infrastructure | `Microsoft.Extensions.Configuration.Binder` | `appsettings.json` এর section গুলো C# class এ পড়তে দেয় (`.Get<JwtSettings>()`) |
| Infrastructure | `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core + PostgreSQL driver |
| Infrastructure | `Microsoft.Extensions.Identity.Core` | আমাদের `PasswordHasher<T>` দেয় (নিরাপদ password hashing) |
| Infrastructure | `Microsoft.IdentityModel.JsonWebTokens` | JWT token বানায় (sign করে) |
| Api | `Microsoft.AspNetCore.Authentication.JwtBearer` | প্রতিটা request এ JWT পড়ে আর validate করে |
| Api | `Microsoft.EntityFrameworkCore.Design` | `dotnet ef` এর migration বানাতে এটা লাগে |

```bash
dotnet add ProductShop.Application    package Microsoft.Extensions.DependencyInjection.Abstractions --version 8.0.2

dotnet add ProductShop.Infrastructure package Microsoft.Extensions.DependencyInjection.Abstractions --version 8.0.2
dotnet add ProductShop.Infrastructure package Microsoft.Extensions.Configuration.Binder           --version 8.0.2
dotnet add ProductShop.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL               --version 8.0.2
dotnet add ProductShop.Infrastructure package Microsoft.Extensions.Identity.Core                  --version 8.0.32
dotnet add ProductShop.Infrastructure package Microsoft.IdentityModel.JsonWebTokens               --version 8.23.0

dotnet add ProductShop.Api package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.32
dotnet add ProductShop.Api package Microsoft.EntityFrameworkCore.Design          --version 8.0.2
```

> ⚠️ সব `Microsoft.*` / EF package **8.x** version এ রাখো, কারণ project গুলো `net8.0` target করে।
> 9.x বা 10.x package net8.0 তে install হতেই রাজি হবে না।

`Swashbuckle.AspNetCore` (Swagger UI) `webapi` template এ আগে থেকেই দেওয়া থাকে।

---

## 1.9 .gitignore

```bash
dotnet new gitignore
```

এটা `bin/` আর `obj/` (build output) কে git এ ঢুকতে দেয় না।

---

## 1.10 সব ঠিকমতো যুক্ত হয়েছে কিনা build করে দেখো

```bash
dotnet build
```

তোমার `Build succeeded. 0 Error(s)` দেখতে পাওয়ার কথা।

---

## 1.11 Final folder structure (সব ধাপ শেষ হওয়ার পর)

```
backend/
├── ProductShop.sln
├── README.md                      ← এখান থেকে শুরু করো
├── docs/                          ← এই guide
├── ProductShop.Domain/
│   ├── Constants/RoleNames.cs
│   └── Entities/  BaseEntity.cs  User.cs  Role.cs  UserRole.cs  Product.cs
├── ProductShop.Utilities/
│   └── Extensions/StringExtensions.cs
├── ProductShop.Application/
│   ├── DependencyInjection.cs     ← AddApplication()
│   ├── Dtos/          (shapes of JSON in / out)
│   ├── Interfaces/    (contracts)
│   ├── Mappings/MappingExtensions.cs
│   └── Services/      AuthService.cs  ProductService.cs
├── ProductShop.Infrastructure/
│   ├── DependencyInjection.cs     ← AddInfrastructure()
│   ├── Auth/          JwtSettings.cs  JwtTokenService.cs  PasswordHasher.cs
│   ├── Persistence/
│   │   ├── ProductShopDbContext.cs
│   │   ├── DatabaseInitializer.cs
│   │   ├── Configurations/ (table rules, one file per entity)
│   │   ├── Migrations/     (generated by dotnet ef)
│   │   └── SeedData/       SeedData.cs  AdminUserSettings.cs
│   └── Repositories/  UserRepository.cs  RoleRepository.cs  ProductRepository.cs
└── ProductShop.Api/
    ├── Program.cs                 ← সবকিছু একসাথে জুড়ে দেয়
    ├── appsettings.json / appsettings.Development.json
    ├── Auth/          ClaimsPrincipalExtensions.cs  AuthRateLimit.cs
    └── Controllers/   AuthController.cs  ProductsController.cs
```

---

## 1.12 এই ধাপের পর `.csproj` file গুলো

এগুলোতে তুমি যে reference আর package যোগ করেছ সেগুলো দেখা যাবে। তোমার file গুলোর সাথে মিলিয়ে দেখো।

📄 **`ProductShop.Application/ProductShop.Application.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\ProductShop.Domain\ProductShop.Domain.csproj" />
    <ProjectReference Include="..\ProductShop.Utilities\ProductShop.Utilities.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

📄 **`ProductShop.Infrastructure/ProductShop.Infrastructure.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\ProductShop.Application\ProductShop.Application.csproj" />
    <ProjectReference Include="..\ProductShop.Domain\ProductShop.Domain.csproj" />
    <ProjectReference Include="..\ProductShop.Utilities\ProductShop.Utilities.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.2" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageReference Include="Microsoft.Extensions.Identity.Core" Version="8.0.32" />
    <PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" Version="8.23.0" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.2" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

📄 **`ProductShop.Api/ProductShop.Api.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.32" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ProductShop.Application\ProductShop.Application.csproj" />
    <ProjectReference Include="..\ProductShop.Infrastructure\ProductShop.Infrastructure.csproj" />
    <ProjectReference Include="..\ProductShop.Domain\ProductShop.Domain.csproj" />
    <ProjectReference Include="..\ProductShop.Utilities\ProductShop.Utilities.csproj" />
  </ItemGroup>

</Project>
```

---

✅ **Step 1 শেষ।** পরের ধাপ ➡️ [Step 2 — Domain layer](02-domain-layer.md)
