# ProductShop — Frontend (Next.js)

```bash
npm install
npm run dev     # http://localhost:3000
```

- API URL: `NEXT_PUBLIC_API_URL` in `.env.local` (default `http://localhost:5000`). Start the backend first — see `../backend/README.md`.
- Admin login: `admin@productshop.com` / `Admin@12345`. Newly registered accounts are normal users (view only).
- ⚠️ Next.js fails to build when the folder path contains `#` (e.g. `PracticeC#`). Move or rename the folder first.

| Route | Who | What |
|---|---|---|
| `/login`, `/register` | guests | sign in / create an account |
| `/products` | logged in | product table; admins also get the add form and Edit / Delete |
