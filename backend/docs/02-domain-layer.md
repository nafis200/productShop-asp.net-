# Step 2 — Domain Layer (Entities) + Utilities

> **এই ধাপের লক্ষ্য:** আমাদের data কেমন দেখতে, সেটা বোঝানোর জন্য সাধারণ C# class লেখা।
> পরে (Step 4-এ) প্রতিটা **entity** class database-এ একটা করে **table** হয়ে যাবে।
>
> ⬅️ [Step 1 — Project setup](01-project-setup.md) · পরের ধাপ ➡️ [Step 3 — Application layer](03-application-layer.md)

---

## 2.1 Domain layer কী?

- এটা app-এর **কেন্দ্র**। এটা **অন্য কোনো project** বা **কোনো NuGet package** reference করে না।
- এর ভেতরে থাকে **Entities** (data + ছোটখাটো নিয়ম) আর **Constants**।
- HTTP, JSON, database বা JWT — এগুলোর কোনোটা সম্পর্কেই এটা কিছু জানে না।

আমাদের app-এ চারটা জিনিস লাগবে:

| Entity | Table | মানে |
|---|---|---|
| `User` | `Users` | একজন মানুষ, যে register / log in করতে পারে |
| `Role` | `Roles` | `Admin` অথবা `User` |
| `UserRole` | `UserRoles` | কোন user-এর কোন role আছে (একটা "join" table, many-to-many) |
| `Product` | `Products` | name + price, admin এগুলো manage করে |

```
 Users                 UserRoles               Roles
┌────────────┐      ┌─────────────────┐     ┌────────────┐
│ Id (Guid)  │◄─────│ UserId          │     │ Id (int)   │
│ FirstName  │      │ RoleId          │────►│ Name       │
│ LastName   │      │ AssignedAt      │     └────────────┘
│ Email      │      └─────────────────┘       1 = Admin
│ PasswordHash│                               2 = User
│ CreatedAt  │
│ ModifiedAt │      Products
└────────────┘      ┌────────────┐
                    │ Id (Guid)  │
                    │ Name       │
                    │ Price      │
                    │ CreatedAt  │
                    │ ModifiedAt │
                    └────────────┘
```

> **শুধু `bool IsAdmin` না রেখে আলাদা Role table কেন?**
> এটা TwitterClone-এর মতোই design, আর এটা scale করে: কাল যদি একটা `Manager` role যোগ করতে চান,
> `Users` table-এ কোনো পরিবর্তন না করেই সেটা করতে পারবেন।

---

## 2.2 Folder গুলো বানাই

```bash
mkdir -p ProductShop.Domain/Entities ProductShop.Domain/Constants
mkdir -p ProductShop.Utilities/Extensions
```

---

## 2.3 `BaseEntity` — সবার common column

যেসব entity-র id হলো `Guid`, তারা সবাই এখান থেকে `Id`, `CreatedAt`, `ModifiedAt` পায়।

- `abstract` = আপনি `new BaseEntity()` করতে পারবেন না, শুধু এটাকে inherit করতে পারবেন।
- `private set` = value টা শুধু class নিজেই change করতে পারে। বাইরের code এটা পড়তে পারবে, কিন্তু overwrite করতে পারবে না।
- Service যখনই কোনো row change করে, তখন `MarkAsModified()` call করে।

📄 **`ProductShop.Domain/Entities/BaseEntity.cs`**

```csharp
namespace ProductShop.Domain.Entities
{
    // Every table that uses a Guid id inherits from this class,
    // so Id / CreatedAt / ModifiedAt are written only once.
    public abstract class BaseEntity
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? ModifiedAt { get; private set; }

        public void MarkAsModified()
        {
            ModifiedAt = DateTime.UtcNow;
        }
    }
}
```

---

## 2.4 `User`

`: BaseEntity` মানে "User নিজের মধ্যে Id, CreatedAt, ModifiedAt inherit করে নেয়"।
`UserRoles` হলো একটা **navigation property** — আমরা চাইলে (`Include` দিয়ে) EF Core এটার ভেতরে user-এর role গুলো ভরে দেবে।

📄 **`ProductShop.Domain/Entities/User.cs`**

```csharp
namespace ProductShop.Domain.Entities
{
    // One row in the "Users" table = one person who can log in.
    public class User : BaseEntity
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Always stored lower-case (see StringExtensions.NormalizeEmail).
        public string Email { get; set; } = string.Empty;

        // We NEVER store the real password, only a one-way hash of it.
        public string PasswordHash { get; set; } = string.Empty;

        // Navigation property: the roles this user has (Admin, User, ...).
        public List<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
```

---

## 2.5 `Role` আর `UserRole`

`Role` Guid-এর বদলে `int` id (1, 2) ব্যবহার করে, কারণ এর row গুলো fixed, আর আমরা নিজেরাই
seed data দিয়ে এগুলো insert করি (Step 4)।

`required` (C# 11) মানে "`Role` বানানোর সময় `Name` অবশ্যই set করতে হবে"।

📄 **`ProductShop.Domain/Entities/Role.cs`**

```csharp
namespace ProductShop.Domain.Entities
{
    // Lookup table with fixed int ids so the rows can be seeded (see SeedData.cs).
    public class Role
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public List<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
```

📄 **`ProductShop.Domain/Entities/UserRole.cs`**

```csharp
namespace ProductShop.Domain.Entities
{
    // Join table. One row = "this user has this role". Primary key is (UserId, RoleId).
    public class UserRole
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
```

---

## 2.6 `Product`

`MaxNameLength` একটা constant, যাতে database column-এর size (Step 4) আর validation (Step 3)
দুই জায়গাতেই একই number ব্যবহার হয়।

📄 **`ProductShop.Domain/Entities/Product.cs`**

```csharp
namespace ProductShop.Domain.Entities
{
    // One row in the "Products" table.
    public class Product : BaseEntity
    {
        public const int MaxNameLength = 100;

        public string Name { get; set; } = string.Empty;

        // decimal (not double) because money must be exact.
        public decimal Price { get; set; }
    }
}
```

---

## 2.7 `RoleNames` — কোনো magic string না

10 জায়গায় `"Admin"` টাইপ করার বদলে (আর তার কোনো একটায় typo করে ফেলার বদলে), প্রতিটা file
`RoleNames.Admin` ব্যবহার করে। `ClaimType = "role"` হলো JWT-র ভেতরে role field-এর নাম (Step 5)।

📄 **`ProductShop.Domain/Constants/RoleNames.cs`**

```csharp
namespace ProductShop.Domain.Constants
{
    public static class RoleNames
    {
        // The claim name we use for roles inside the JWT.
        public const string ClaimType = "role";

        public const string Admin = "Admin";
        public const string User = "User";
    }
}
```

---

## 2.8 Utilities — `StringExtensions`

একটা **extension method** আগে থেকে থাকা কোনো type-এ নতুন method যোগ করে। `this string email` থাকার কারণে
আপনি `"A@B.com".NormalizeEmail()` লিখতে পারেন, যেন `string`-এর মধ্যে এই method সবসময়ই ছিল।
আমরা এটা ব্যবহার করি যাতে `Nafis@Mail.com` আর `nafis@mail.com` কে একই email হিসেবে ধরা হয়।

📄 **`ProductShop.Utilities/Extensions/StringExtensions.cs`**

```csharp
namespace ProductShop.Utilities.Extensions
{
    public static class StringExtensions
    {
        // " Nafis@Mail.COM " -> "nafis@mail.com"
        // Used everywhere we save or search an email, so login is case-insensitive.
        public static string NormalizeEmail(this string email)
        {
            return email.Trim().ToLowerInvariant();
        }
    }
}
```

---

## 2.9 Build

```bash
dotnet build ProductShop.Domain
dotnet build ProductShop.Utilities
```

✅ **Step 2 শেষ।** পরের ধাপ ➡️ [Step 3 — Application layer](03-application-layer.md)
