# StudyBuddy — Production Deployment Guide

This guide deploys StudyBuddy in two pieces:

| Piece   | Where    | Tech                               |
| ------- | -------- | ---------------------------------- |
| Backend | Render   | ASP.NET Core 8 + PostgreSQL (free) |
| Frontend| Vercel   | Create React App (static)          |

Everything is already wired up (one monorepo). You only need to push it and set
a couple of secrets.

The repo layout:

```
repo root
├── render.yaml                    # Render Blueprint (rootDir: Study_Buddy_Backend/Study_Buddy)
├── Study_Buddy_Backend/Study_Buddy   # ASP.NET Core backend (Render / PostgreSQL)
├── Study_Buddy_Frontend/study-buddy  # React frontend (Vercel, Root Directory)
└── DEPLOYMENT.md
```

---

## What changed in the code (already applied)

- **`Program.cs`** — reads the Postgres connection from the `DATABASE_URL` env
  var when present; otherwise falls back to the SQL Server LocalDB connection
  string for local dev. Binds the `PORT` env var (Render). Uses a CORS policy
  driven by `CORS_ORIGIN` (falls back to `FrontendUrl` = `http://localhost:3000`).
  Swagger is enabled unless `Production` (or force-on with `SWAGGER=true`).
  HTTPS auto-redirect is only used outside `Production`.
- **`Services/StartupSeeder.cs`** — on startup with PostgreSQL it runs
  `EnsureCreated()` (creates the schema on a brand-new database) and seeds the
  built-in exam syllabi from `SeedData/*.json` unless the exam already has
  subjects. Set `SEED_RESET=true` once to force delete-and-reinsert.
- **`Data/StudyBuddyContext.cs`** — timestamp column defaults are now provider
  aware (`GETDATE()` for SQL Server, `CURRENT_TIMESTAMP` for PostgreSQL).
- **`Study_Buddy.csproj`** — added `Npgsql.EntityFrameworkCore.PostgreSQL`.
- **`src/services/api.js`** — `API_BASE` now reads `process.env.REACT_APP_API_BASE`
  and defaults to `http://localhost:5193/api` for local dev.
- **`vercel.json`** — SPA rewrite so React Router deep links work on Vercel.
- **`render.yaml`** — Render Blueprint (web service + free Postgres).
- **`.gitignore`** — added for backend (`bin/`, `obj/`, `out/`, ...); SeedData
  JSON files stay committed (they are deployed and seed the database).

---

## Environment variables

### Backend (set in Render)

| Variable          | Required | Notes                                                    |
| ----------------- | -------- | -------------------------------------------------------- |
| `DATABASE_URL`    | auto     | Provided automatically by the Render Blueprint Postgres. |
| `Mistral__ApiKey` | yes      | Your Mistral API key — set manually, never committed. Also used for local dev (see "Secrets" below). |
| `CORS_ORIGIN`     | optional | Your Vercel URL minus trailing slash (e.g. `https://studybuddy-abc123.vercel.app`). Until set, all origins are allowed. Recommended for hardening. |
| `ASPNETCORE_ENVIRONMENT` | no | Set to `Production` (already in `render.yaml`). |
| `PORT`           | auto     | Injected by Render; the app binds it.                    |
| `SWAGGER`         | optional | `true` to expose `/swagger` on Render.                   |
| `SEED_RESET`      | optional | `true` on one deploy to force re-seeding syllabi.        |
| `PGSSL_MODE`      | optional | SSL mode for Postgres (default `Require`, required by Render). Set `Prefer`/`Disable` only to run against a local Postgres without SSL. |

> **Secrets**: the Mistral key is **not** in `appsettings.json` anymore. Locally it is
> read from the user-level env var `Mistral__ApiKey` (already set on this machine).
> On Render it must be entered during the Blueprint setup.

### Frontend (set in Vercel)

| Variable             | Notes                                                       |
| -------------------- | ----------------------------------------------------------- |
| `REACT_APP_API_BASE` | `https://<backend>.onrender.com/api` (note the `/api` suffix). |

---

## Step 1 — Push the repository

1. The code is already committed locally on branch `main` (git repo at
   `C:\StudyBuddy`).
2. Add the remote and push:

   ```bash
   cd C:\StudyBuddy
   git remote add origin https://github.com/Meiroopashree/Study_Buddy.git
   git push -u origin main
   ```

   Important: `SeedData/*.json` is committed (copied to the build output at
   deploy time, it seeds the database). No secrets are in the repo —
   `Mistral__ApiKey` was removed from `appsettings.json`.

## Step 2 — Backend on Render (Blueprint)

The repo root contains `render.yaml` (service `rootDir: Study_Buddy_Backend/Study_Buddy`).

1. Go to [render.com](https://render.com) → **New** → **Blueprint**.
2. Select the `Study_Buddy` repo.
3. Render reads `render.yaml` and creates:
   - A free Web Service (`studybuddy-api`) running `dotnet publish -c Release`.
   - A free PostgreSQL database (`studybuddy-db`) with `DATABASE_URL` wired in.
4. When prompted, fill in the secret:
   - `Mistral__ApiKey` = your Mistral API key.
   > `CORS_ORIGIN` is optional at first — until set, the API allows all origins.
   > To harden it, add `CORS_ORIGIN` in `Dashboard → Env` after you have your
   > Vercel URL (Step 3) and redeploy.
5. Click **Apply**. The first deploy takes a few minutes.
6. Verify: open `https://studybuddy-api.onrender.com/api/learning/exams`
   (Render health checks use this path). It should return a JSON array.

> The free web service sleeps after inactivity; the first request after sleep
> takes ~30–60s to wake up.

## Step 3 — Frontend on Vercel

The frontend lives at `Study_Buddy_Frontend/study-buddy` inside the **same** repo.

1. Go to [vercel.com](https://vercel.com) → **Add New…** → **Project**.
2. Import the `Study_Buddy` repo.
3. Set **Root Directory** = `Study_Buddy_Frontend/study-buddy`. Vercel auto-
   detects **Create React App** (build `npm run build`, output `build`). The
   `vercel.json` SPA rewrite is already in place for React Router.
4. Under **Settings → Environment Variables** add:
   - `REACT_APP_API_BASE` = `https://studybuddy-api.onrender.com/api`
5. Deploy. Visit `https://<project>.vercel.app`.

## Step 4 — Final wiring

- Optional hardening: set `CORS_ORIGIN` on Render to your deployed Vercel URL
  (`<name>.vercel.app`, no trailing slash) and redeploy the web service.
- Test the full flow in the browser: sign up, generate a quiz, ask AI a
  question (needs a working Mistral key).

---

## Manual fallbacks (no Blueprint)

### Render without Blueprint
1. **New → Web Service** from the `Study_Buddy` repo:
   - Root Directory: `Study_Buddy_Backend/Study_Buddy`
   - Language: **Docker** or **.NET**; Build: `dotnet publish -c Release -o out`
   - Start: `./out/Study_Buddy`
2. **New → PostgreSQL** instance, copy its internal connection string to the
   web service's `DATABASE_URL`.
3. Add `Mistral__ApiKey`, `CORS_ORIGIN`, `ASPNETCORE_ENVIRONMENT=Production`.

### Vercel without Git
```bash
cd C:\StudyBuddy\Study_Buddy_Frontend\study-buddy
npx vercel
# during deploy, add the env var: REACT_APP_API_BASE=https://studybuddy-api.onrender.com/api
```

---

## Troubleshooting

- **401/403 from the frontend** — CORS. Check `CORS_ORIGIN` equals the exact
  deployed origin (no trailing slash).
- **API slow first call** — Render free tier cold start; deploy-wake or upgrade.
- **Subjects missing for an exam** — the backend only seeds exams with zero
  subjects. Set `SEED_RESET=true` once on the Render service to force a full
  reseed from `SeedData`.
- **Swagger** — set `SWAGGER=true` to browse `/swagger` on Render.
- **`dotnet` build lock error on Windows** — stop the running local server
  (`Get-Process dotnet | Stop-Process`) before rebuilding.