# Step 7 — Database Migrations (আসল table গুলো বানানো)

> **এই ধাপের লক্ষ্য:** আমাদের C# entity + configuration গুলোকে আসল PostgreSQL table এ রূপান্তর করা।
>
> ⬅️ [Step 6 — API layer](06-api-layer.md) · পরের ধাপ ➡️ [Step 8 — Run & test](08-run-and-test.md)

---

## 7.1 Migration কী?

**Migration** হলো EF Core এর generate করা একটা C# file, যেটা বলে *"database কে version A থেকে
version B তে কীভাবে বদলাতে হবে"*। এর মধ্যে থাকে:

- `Up()` — change টা apply করে (table বানানো, column add করা, ...)
- `Down()` — change টা undo করে

EF তোমার বর্তমান C# model কে শেষ snapshot (`ProductShopDbContextModelSnapshot.cs`) এর সাথে তুলনা করে
আর শুধু **পার্থক্যটুকু** লিখে। ব্যাপারটা অনেকটা git commit এর মতো, তবে database এর structure এর জন্য।

---

## 7.2 যা যা লাগবে (আগের ধাপগুলোতে করা হয়ে গেছে)

- `dotnet-ef` tool globally install করা (Step 1)।
- **Api** project এ `Microsoft.EntityFrameworkCore.Design` (Step 1)।
- **Infrastructure** project এ `Npgsql.EntityFrameworkCore.PostgreSQL` (Step 1)।
- `ProductShop.Api/appsettings.Development.json` এ connection string (Step 4/6)।
- Project টা build হয় (`dotnet build`)।

---

## 7.3 প্রথম migration বানানো

`backend/` folder থেকে run করো:

```bash
dotnet ef migrations add InitialCreate \
  --project ProductShop.Infrastructure \
  --startup-project ProductShop.Api \
  --output-dir Persistence/Migrations
```

| Option | কেন |
|---|---|
| `InitialCreate` | migration এর নাম (যেকোনো নাম দিতে পারো: `AddCategory`, `AddStockColumn`, ...) |
| `--project ProductShop.Infrastructure` | যে project এ `DbContext` আছে → migration file গুলো এখানেই যাবে |
| `--startup-project ProductShop.Api` | যে project টা **run** হয়: `appsettings` আর `Program.cs` পড়ার জন্য EF এটাকে start করে |
| `--output-dir Persistence/Migrations` | generate হওয়া file গুলোর folder |

যে file গুলো generate হয়:

```
ProductShop.Infrastructure/Persistence/Migrations/
├── 20261116222518_InitialCreate.cs            ← Up() / Down()   (এটা পড়ো)
├── 20261116222518_InitialCreate.Designer.cs   ← metadata, edit করবে না
└── ProductShopDbContextModelSnapshot.cs       ← "current model" snapshot, edit করবে না
```

> **Apply করার আগে সবসময় generate হওয়া `..._InitialCreate.cs` file টা খুলে পড়ে নাও।**
> এখানে তুমি check করতে পারবে: প্রতিটা table এ `schema: "product_shop"` আছে কিনা, `Roles` এর row গুলো insert হচ্ছে কিনা,
> `Email` এ unique index আছে কিনা। TwitterClone এর কোনো table এ হাত পড়ছে না।

📄 **`ProductShop.Infrastructure/Persistence/Migrations/20261116222518_InitialCreate.cs`**

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ProductShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "product_shop");

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "product_shop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "product_shop",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "product_shop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "product_shop",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "product_shop",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "product_shop",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "product_shop",
                table: "Roles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Admin" },
                    { 2, "User" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "product_shop",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "product_shop",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "product_shop",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products",
                schema: "product_shop");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "product_shop");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "product_shop");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "product_shop");
        }
    }
}
```

---

## 7.4 Database এ migration apply করা

Option A — command দিয়ে:

```bash
dotnet ef database update \
  --project ProductShop.Infrastructure \
  --startup-project ProductShop.Api
```

Output:

```
Build succeeded.
Applying migration '20261116222518_InitialCreate'.
Done.
```

Option B — startup এর সময় automatically: যেহেতু `appsettings.Development.json` এ
`"Database": { "ApplyMigrationsOnStartup": true }` আছে, তাই শুধু API run করলেই pending
migration গুলো apply হয়ে যায় (`DatabaseInitializer` → `Database.MigrateAsync()`)।

EF কোন কোন migration apply হয়েছে সেটা `product_shop."__EFMigrationsHistory"` table এ লিখে রাখে, তাই প্রতিটা
migration শুধু একবারই run হয়।

---

## 7.5 PostgreSQL এ ফলাফল

```
schema product_shop
├── __EFMigrationsHistory
├── Roles        (1 Admin, 2 User)
├── Users        (+ admin@productshop.com, created at first startup)
├── UserRoles
└── Products
```

Data দেখতে চাইলে: Neon dashboard → **Tables** (schema `product_shop` বেছে নাও), অথবা যেকোনো SQL client:

```sql
SELECT * FROM product_shop."Products";
SELECT u."Email", r."Name" FROM product_shop."Users" u
JOIN product_shop."UserRoles" ur ON ur."UserId" = u."Id"
JOIN product_shop."Roles" r ON r."Id" = ur."RoleId";
```

> PostgreSQL এ বড় হাতের অক্ষর (capital letter) আছে এমন নামের চারপাশে double quote দিতে হয় (`"Products"`)।

---

## 7.6 পরে database বদলানো (প্রতিদিনের workflow)

উদাহরণ: তুমি product এ একটা `Stock` column চাও।

1. `Product.cs` এ property টা add করো → `public int Stock { get; set; }`
2. (optional) `ProductConfiguration.cs` এ rule গুলো দাও
3. DTO / mapping এ এটা add করো
4. নতুন একটা migration বানাও:
   ```bash
   dotnet ef migrations add AddProductStock --project ProductShop.Infrastructure --startup-project ProductShop.Api --output-dir Persistence/Migrations
   ```
5. Apply করো: `dotnet ef database update ...` (অথবা শুধু API run করো)।

## 7.7 কাজের command গুলো

| Command | কী করে |
|---|---|
| `dotnet ef migrations list --project ProductShop.Infrastructure --startup-project ProductShop.Api` | migration এর list দেখায়, কোনগুলো apply হয়েছে সেটাও দেখায় |
| `dotnet ef migrations remove --project ProductShop.Infrastructure --startup-project ProductShop.Api` | **সর্বশেষ** migration টা delete করে (শুধু যদি এখনো apply না হয়ে থাকে) |
| `dotnet ef database update <MigrationName> ...` | নির্দিষ্ট একটা migration এ পিছনে/সামনে যায় |
| `dotnet ef migrations script ...` | run না করে SQL টা print করে দেখায় |

## 7.8 সাধারণ error গুলো

| Error | সমাধান |
|---|---|
| `Could not execute because the specified command or file was not found` (dotnet ef) | `dotnet tool install --global dotnet-ef --version 8.0.2` |
| `Your startup project doesn't reference Microsoft.EntityFrameworkCore.Design` | ওই package টা **Api** project এ add করো |
| `ConnectionStrings:DefaultConnection is not configured` | `appsettings.Development.json` check করো; `ASPNETCORE_ENVIRONMENT` অবশ্যই `Development` হতে হবে |
| `JwtSettings:Key must be at least 32 bytes long` | EF `Program.cs` ও run করে — তাই JWT key সেট করা থাকতে হবে |
| `relation "product_shop.Roles" does not exist` | migration apply হয়নি — `dotnet ef database update` run করো |

✅ **Step 7 শেষ।** পরের ধাপ ➡️ [Step 8 — Run & test](08-run-and-test.md)
