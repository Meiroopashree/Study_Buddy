# StudyBuddy — Production Deployment Guide

This guide deploys StudyBuddy in two pieces:

| Piece   | Where  | Tech                              | URL                                      |
| ------- | ------ | --------------------------------- | ---------------------------------------- |
| Backend | Render | ASP.NET Core 8 + PostgreSQL (free) | https://studybuddy-api-dy48.onrender.com |
| Frontend| Vercel | Create React App (static)         | https://study-buddy-meiroopashrees-projects.vercel.app |

The repo layout:

```
repo root
├── Study_Buddy_Backend/Study_Buddy      # ASP.NET Core backend (Render / PostgreSQL)
│   └── Dockerfile                        # multi-stage .NET 8 build (Render runs the app in a container)
├── Study_Buddy_Frontend/study-buddy      # React frontend (Vercel, Root Directory)
└── DEPLOYMENT.md
```

`render.yaml` at the repo root exists for reference, but the current Render
resources (web service + PostgreSQL) were provisioned **via the Render API**
(API-provisioned services ignore the Blueprint file).

---

## Live endpoints

- Backend API: `https://studybuddy-api-dy48.onrender.com/api`
  - Health smoke test: `GET /api/learning/exams` → `200` with a JSON array.
- Frontend app: `https://study-buddy-meiroopashrees-projects.vercel.app`
  (also `https://study-buddy-delta-ten.vercel.app`; both are production aliases of the same deployment).
- Swagger is only enabled with `SWAGGER=true`.

---

## What changed in the code (already applied)

- **`Program.cs`**
  - Reads the Postgres connection from `DATABASE_URL`; falls back to SQL Server
    LocalDB for local dev.
  - `ParsePostgresUrl` defaults the port to **5432** when the connection string
    omits it (`uri.Port <= 0`). Render's *internal* Postgres connection string
    has no port; without this the app crashed at startup with
    `System.ArgumentOutOfRangeException: Invalid port: -1`.
  - Binds the `PORT` env var (Render) as `http://0.0.0.0:<port>`.
  - CORS policy driven by `CORS_ORIGIN`; **until set, all origins are allowed**
    (`Access-Control-Allow-Origin: *`).
  - Swagger enabled unless `Production` (or force-on with `SWAGGER=true`);
    HTTPS auto-redirect only outside `Production`.
- **`Services/StartupSeeder.cs`** — on startup with PostgreSQL runs
  `EnsureCreated()` and seeds the built-in exam syllabi from `SeedData/*.json`
  unless the exam already has subjects. Set `SEED_RESET=true` once to force
  delete-and-reinsert.
- **`Dockerfile`** — multi-stage `.NET 8` build; entrypoint `dotnet Study_Buddy.dll`;
  listens on `http://+:10000` via `ASPNETCORE_URLS`.
- **`src/services/api.js`** — `API_BASE` reads `process.env.REACT_APP_API_BASE`
  and defaults to `http://localhost:5193/api` for local dev.
- **`vercel.json`** — SPA rewrite so React Router deep links work on Vercel.
- **`.gitignore`** — added for backend (`bin/`, `obj/`, `out/`, ...); `SeedData`
  JSON files stay committed (they are copied into the Docker image and seed the DB).

---

## Environment variables

### Backend (set in Render → Service → Environment)

| Variable          | Required | Notes                                                        |
| ----------------- | -------- | ------------------------------------------------------------ |
| `DATABASE_URL`    | auto     | Provided by Render (Postgres created via API). May omit the port — the code defaults to 5432. |
| `Mistral__ApiKey` | yes      | Mistral API key for the AI chat/quiz features. Set manually, never committed. |
| `CORS_ORIGIN`     | optional | The deployed Vercel origin, no trailing slash (e.g. `https://study-buddy-meiroopashrees-projects.vercel.app`). Until set, all origins are allowed. Recommended for hardening. |
| `ASPNETCORE_ENVIRONMENT` | no | `Production` disables Swagger/HTTPS-redirect. |
| `PORT`            | auto     | Injected by Render; the app binds it.                        |
| `SWAGGER`         | optional | `true` to expose `/swagger` on Render.                       |
| `SEED_RESET`      | optional | `true` on one deploy to force re-seeding syllabi.            |
| `PGSSL_MODE`      | optional | SSL mode for Postgres (default `Require`, required by Render). |

### Frontend (set in Vercel → Project → Settings → Environment Variables)

| Variable             | Notes                                                            |
| -------------------- | ---------------------------------------------------------------- |
| `REACT_APP_API_BASE` | `https://studybuddy-api-dy48.onrender.com/api` (note the `/api` suffix). |

> `REACT_APP_*` values are inlined into the client bundle and are public.

---

## How deploys run now

- **Render**: the web service auto-deploys on every push to `main` (Docker build).
  Build command: `docker build`; start command: none (Dockerfile handles it).
- **Vercel**: the GitHub integration auto-deploys on every push to `main`.
  Project settings (Vercel → Dashboard → study-buddy → Settings → General):
  - **Root Directory** = `Study_Buddy_Frontend/study-buddy` *(this must be set —
    without it Vercel treats the repo root as the app and fails with
    `react-scripts: command not found`)*.
  - **Framework Preset** = Create React App, **Node.js Version** = `22.x`
    (older than the default 24.x to keep `react-scripts` 5.0.1 stable).
  - Install/Build commands: left at defaults (`npm install` / `npm run build`).
- No manual backoffice clicks are required after a push.

---

## Local dev

Backend: `Study_Buddy_Backend\Study_Buddy` → `dotnet run` (uses SQL Server
LocalDB + `FrontendUrl=http://localhost:3000`).

Frontend: `Study_Buddy_Frontend\study-buddy` → `npm start`.

---

## Troubleshooting

- **401/403 from the frontend** — CORS. Set `CORS_ORIGIN` on Render to the exact
  deployed origin (no trailing slash) and redeploy. Until then the API allows
  all origins (`*`).
- **Vercel build fails with `react-scripts: command not found`** — the project's
  Root Directory is not set (build ran at the repo root where there is no
  `package.json`). Set Root Directory = `Study_Buddy_Frontend/study-buddy`.
- **Vercel build fails on ESLint warnings** — Vercel builds with `CI=true` for
  Git deployments; warnings become errors. Fix the warnings, don't disable CI.
- **Backend crashes at startup with `Invalid port: -1`** — stale image or a
  `DATABASE_URL` with no port; current code defaults to 5432; redeploy.
- **API slow first call** — Render free tier cold start (~30–60s), then scale to
  zero while idle.
- **Subjects missing for an exam** — the backend only seeds exams with zero
  subjects. Set `SEED_RESET=true` once on the Render service to force a full
  reseed from `SeedData`.
- **`dotnet` build lock error on Windows** — stop the running local server
  (`Get-Process dotnet | Stop-Process`) before rebuilding.

---

## Secrets hygiene

`Mistral__ApiKey`, the Vercel CLI token, and the Render API key were pasted into
CLI sessions during setup. Once the deployment is stable, revoke/rotate those
tokens. Nothing secret is stored in the repo.