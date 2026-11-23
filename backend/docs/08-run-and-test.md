# Step 8 — API Run ও Test করা

> ⬅️ [Step 7 — Migrations](07-migrations.md) · পরের ধাপ ➡️ [Step 9 — Cheat sheet](09-cheatsheet.md)

---

## 8.1 Run

```bash
cd backend
dotnet run --project ProductShop.Api --launch-profile http
```

তুমি এরকম দেখতে পাবে:

```
info: ProductShop.Infrastructure.Persistence.DatabaseInitializer[0]
      Seeded admin user admin@productshop.com.        ← শুধু প্রথমবার
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

বন্ধ করতে `Ctrl + C` চাপো। প্রতিবার code change করলে automatically restart চাইলে `dotnet watch --project ProductShop.Api` ব্যবহার করো।

---

## 8.2 Swagger দিয়ে test (সবচেয়ে সহজ)

1. <http://localhost:5000/swagger> খোলো
2. `POST /api/auth/login` → **Try it out** → body:
   ```json
   { "email": "admin@productshop.com", "password": "Admin@12345" }
   ```
   → **Execute** → `accessToken` এর value টা copy করো।
3. **Authorize 🔓** button এ click করো (উপরে ডান দিকে) → token টা paste করো → **Authorize**।
4. এখন Swagger থেকে যাওয়া প্রতিটা request এর সাথে `Authorization: Bearer <token>` যাবে। `POST /api/products` try করে দেখো।

## 8.3 `.http` file দিয়ে test

VS Code এ `ProductShop.Api/ProductShop.Api.http` খোলো (**REST Client** extension install করে নাও), তারপর
প্রতিটা request এর উপরে থাকা **Send Request** এ click করো।

📄 **`ProductShop.Api/ProductShop.Api.http`**

```http
# Open this file in VS Code (REST Client extension) or Rider/Visual Studio and click "Send Request".
@baseUrl = http://localhost:5000

### 1. Register a normal user
POST {{baseUrl}}/api/auth/register
Content-Type: application/json

{
  "firstName": "Test",
  "lastName": "User",
  "email": "test.user@example.com",
  "password": "Password123"
}

### 2. Login as the seeded admin  -> copy "accessToken" from the response into @token below
POST {{baseUrl}}/api/auth/login
Content-Type: application/json

{
  "email": "admin@productshop.com",
  "password": "Admin@12345"
}

###
@token = PASTE_ACCESS_TOKEN_HERE

### 3. Who am I?
GET {{baseUrl}}/api/auth/me
Authorization: Bearer {{token}}

### 4. List products (any logged-in user)
GET {{baseUrl}}/api/products
Authorization: Bearer {{token}}

### 5. Create a product (admin only)
POST {{baseUrl}}/api/products
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "name": "Keyboard",
  "price": 49.99
}

### 6. Update a product (admin only) — replace the id
PUT {{baseUrl}}/api/products/00000000-0000-0000-0000-000000000000
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "name": "Mechanical Keyboard",
  "price": 89.50
}

### 7. Delete a product (admin only) — replace the id
DELETE {{baseUrl}}/api/products/00000000-0000-0000-0000-000000000000
Authorization: Bearer {{token}}
```

## 8.4 curl দিয়ে test

```bash
# admin হিসেবে login করে token টা একটা variable এ রাখো
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@productshop.com","password":"Admin@12345"}' \
  | python3 -c 'import sys,json; print(json.load(sys.stdin)["accessToken"])')

# একটা product বানাও
curl -X POST http://localhost:5000/api/products \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"Keyboard","price":49.99}'

# product এর list দেখো
curl http://localhost:5000/api/products -H "Authorization: Bearer $TOKEN"
```

---

## 8.5 প্রত্যাশিত ফলাফল (সবগুলো এই project এ verify করা)

| Test | Status | Response |
|---|---|---|
| নতুন user register | **201** | `{ tokenType, accessToken, expiresAt, user: { roles: ["User"] } }` |
| একই email দিয়ে আবার register | **409** | `{"message":"A user with email '...' already exists."}` |
| ভুল password দিয়ে login | **401** | `{"message":"Invalid email or password."}` |
| Admin login | **200** | `user.roles = ["Admin"]` |
| token ছাড়া `GET /api/products` | **401** | – |
| User token → `POST /api/products` | **403** | – |
| Admin → `POST /api/products` `{"name":"  Keyboard ","price":49.999}` | **201** | `{"name":"Keyboard","price":50.00,...}` (trim হয়েছে, round হয়েছে) |
| Admin → `POST` `{"name":"  ","price":0}` | **400** | `errors: { Name: [...required], Price: [...between 0.01 and 1000000] }` |
| Admin → `PUT /api/products/{id}` | **200** | update হওয়া product, `modifiedAt` সেট হয়েছে |
| User token → `GET /api/products` | **200** | list |
| User token → `DELETE /api/products/{id}` | **403** | – |
| Admin → `DELETE /api/products/{id}` | **204** | – |
| Admin → একই `DELETE` আবার | **404** | – |
| `GET /api/auth/me` | **200** | logged-in user টা |
| `http://localhost:3000` থেকে CORS preflight | **204** | `Access-Control-Allow-Origin: http://localhost:3000` |

### Frontend কে যে error JSON shape গুলো handle করতে হবে

```jsonc
// our own errors (401 login, 409 register)
{ "message": "Invalid email or password." }

// automatic validation errors from [ApiController] (400)
{ "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "Name": ["The Name field is required."] } }
```

---

## 8.6 Account গুলো

| Email | Password | Role |
|---|---|---|
| `admin@productshop.com` | `Admin@12345` | Admin (appsettings থেকে seed করা) |
| তুমি যেটা register করবে | তোমার পছন্দমতো (কমপক্ষে 8 character) | User |

---

## 8.7 Troubleshooting

| সমস্যা | সমাধান |
|---|---|
| port 5000 এ `address already in use` | অন্য কোনো app এটা ব্যবহার করছে: `lsof -i :5000`, অথবা `launchSettings.json` এ port বদলাও (আর frontend এর `.env.local` এও) |
| Frontend: browser console এ "CORS error" | frontend এর URL টা `Cors:AllowedOrigins` এ থাকতে হবে; appsettings বদলানোর পর API restart করো |
| token দেওয়ার পরেও সবসময় 401 | header হুবহু `Authorization: Bearer <token>` হতে হবে (মাঝের space সহ); token expire হয়ে যেতে পারে (60 min) |
| create করতে গেলে 403 | তুমি সাধারণ User হিসেবে logged in — admin হিসেবে login করো |
| VS Code লাল দাগ দেখাচ্ছে কিন্তু `dotnet build` ঠিকঠাক চলছে | `Ctrl+Shift+P` → **Developer: Reload Window** অথবা **C#: Restart Language Server**; অথবা `rm -rf */bin */obj && dotnet build` |

✅ **Step 8 শেষ।** পরের ধাপ ➡️ [Step 9 — Cheat sheet](09-cheatsheet.md)
