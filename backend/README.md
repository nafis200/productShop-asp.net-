# ProductShop — ASP.NET Core 8 Backend (Clean Architecture + PostgreSQL + JWT)

ছোট কিন্তু পুরোপুরি কাজ করা একটা backend, বানানো হয়েছে **ধাপে ধাপে ASP.NET শেখার জন্য**:

- JWT token দিয়ে **Register / Login**
- দুইটা role: **Admin** আর **User**
- **Products** (name + price):
  - **Admin**: create, update, delete, view — সব পারে
  - **User**: শুধু view করতে পারে
- **Entity Framework Core** দিয়ে PostgreSQL (Neon), migration সহ
- TwitterClone project এর মতোই একই folder/code structure

Frontend (Next.js) আছে `../frontend` এ, আর সেটা এই API এর সাথে কথা বলে।

---

## 📚 Guide — সিরিয়াল মেনে পড়ো

প্রতিটা ধাপে বলা আছে **কী** করছি, **কেন** করছি, কোন **terminal command** চালাতে হবে, আর ওই ধাপের **সব code** দেখানো আছে।

| Step | Page | যা শিখবে |
|---|---|---|
| 1 | [Project setup](docs/01-project-setup.md) | solution, layer, project reference, NuGet package |
| 2 | [Domain layer](docs/02-domain-layer.md) | Entity (`User`, `Role`, `Product`), constant |
| 3 | [Application layer](docs/03-application-layer.md) | DTO, validation, Interface, Service, Dependency Injection |
| 4 | [Infrastructure — database](docs/04-infrastructure-database.md) | EF Core, DbContext, table configuration, seed data, Repository, connection string |
| 5 | [JWT authentication](docs/05-jwt-authentication.md) | login token কীভাবে কাজ করে, password hashing, role, 401 vs 403 |
| 6 | [API layer & Program.cs](docs/06-api-layer.md) | Controller, একটা request কীভাবে প্রতিটা file এর ভেতর দিয়ে যায়, `Program.cs`, appsettings, CORS |
| 7 | [Migrations](docs/07-migrations.md) | PostgreSQL এ table বানানো |
| 8 | [Run & test](docs/08-run-and-test.md) | Swagger, `.http` file, curl, কী result আসার কথা |
| 9 | [Cheat sheet](docs/09-cheatsheet.md) | একদম শূন্য থেকে নতুন project বানানো, নতুন feature যোগ করা |

---

## 🏗️ এক ছবিতে Architecture

```
                         HTTP (JSON)  ◄──►  Next.js frontend (localhost:3000)
                              │
 ┌────────────────────────────▼─────────────────────────────┐
 │ ProductShop.Api            Program.cs, Controllers         │  receives requests, JWT check, returns JSON
 └──────────────┬──────────────────────────────┬─────────────┘
                ▼                              ▼
 ┌──────────────────────────────┐  ┌──────────────────────────────────┐
 │ ProductShop.Application      │◄─│ ProductShop.Infrastructure        │
 │ Services, DTOs, Interfaces   │  │ EF Core DbContext, Repositories,  │
 │ (WHAT the app does)          │  │ JWT, PasswordHasher (HOW)         │──► PostgreSQL (Neon)
 └──────────────┬───────────────┘  └───────────────┬──────────────────┘     schema "product_shop"
                └──────────────┬───────────────────┘
                               ▼
                 ┌──────────────────────────────┐
                 │ ProductShop.Domain  Entities │  (knows nobody)
                 └──────────────────────────────┘
```

## 🔗 File গুলো একটার সাথে আরেকটা কীভাবে যুক্ত (উদাহরণ: Admin একটা product যোগ করছে)

```
POST /api/products  + "Authorization: Bearer <token>"
  │
  ├─ Program.cs                  UseCors → UseAuthentication → UseAuthorization (Roles = Admin ✔)
  ├─ ProductsController.Create   [Api]             receives CreateProductDto (auto-validated)
  ├─ IProductService             ── DI ──►  ProductService.CreateAsync      [Application]
  ├─ IProductRepository          ── DI ──►  ProductRepository.AddAsync      [Infrastructure]
  ├─ ProductShopDbContext        SaveChangesAsync → INSERT INTO product_shop."Products"
  └─ back: Product → ToDto() → ProductDto → 201 Created (JSON)

"DI" registrations live in:
  ProductShop.Application/DependencyInjection.cs     → AddApplication()
  ProductShop.Infrastructure/DependencyInjection.cs  → AddInfrastructure(configuration)
  both called from ProductShop.Api/Program.cs
```

---

## 🌐 Endpoints

Base URL: `http://localhost:5000`

| Method | URL | কে পারবে | Body |
|---|---|---|---|
| POST | `/api/auth/register` | যে কেউ | `{ firstName, lastName, email, password }` |
| POST | `/api/auth/login` | যে কেউ | `{ email, password }` |
| GET | `/api/auth/me` | login করা থাকলে | – |
| GET | `/api/products` | login করা থাকলে | – |
| GET | `/api/products/{id}` | login করা থাকলে | – |
| POST | `/api/products` | **Admin** | `{ name, price }` |
| PUT | `/api/products/{id}` | **Admin** | `{ name, price }` |
| DELETE | `/api/products/{id}` | **Admin** | – |

---

## 🚀 Quick start

```bash
# requirements: .NET 8 SDK, dotnet-ef (dotnet tool install --global dotnet-ef --version 8.0.2)
cd backend
dotnet build
dotnet run --project ProductShop.Api --launch-profile http
# Development এ startup এর সময় migration নিজে থেকেই apply হয়ে যায়
# open http://localhost:5000/swagger
```

Admin login: **`admin@productshop.com` / `Admin@12345`** (আসছে `appsettings.Development.json` → `AdminUser` থেকে)।

এরপর আরেকটা terminal এ frontend চালাও:

```bash
cd ../frontend
npm install
npm run dev          # http://localhost:3000
```

> ⚠️ **Folder path এ `#` থাকলে Next.js run হয় না** (যেমন `PracticeC#`): Next.js এর দুইটা
> bundler ই `#` কে URL fragment ধরে নেয়, ফলে build fail করে। ASP.NET backend এর এতে কোনো সমস্যা নেই।
> Frontend চালানোর আগে parent folder এর নাম বদলাও (যেমন `PracticeC#` → `PracticeCSharp`) অথবা `ProductShop/` কে
> এমন কোথাও সরিয়ে নাও যেখানে path এ `#` নেই।

---

## 📁 Folder structure

```
backend/
├── ProductShop.sln
├── README.md                       ← তুমি এখন এখানে
├── docs/                           ← ধাপে ধাপে guide
├── ProductShop.Domain/             Entities/, Constants/
├── ProductShop.Utilities/          Extensions/
├── ProductShop.Application/        Dtos/, Interfaces/, Services/, Mappings/, DependencyInjection.cs
├── ProductShop.Infrastructure/     Auth/, Persistence/ (DbContext, Configurations, Migrations, SeedData), Repositories/, DependencyInjection.cs
└── ProductShop.Api/                Controllers/, Auth/, Program.cs, appsettings*.json, ProductShop.Api.http
```
