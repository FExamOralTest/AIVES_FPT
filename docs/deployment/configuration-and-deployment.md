# AIVES — Configuration & Deployment Guide (Step-by-Step)

> Last updated: 2026-10-19 · AIVES FPT (SWD392)
> Stack: **Vite + React 19** (frontend) · **.NET 8 ASP.NET Core** (backend) · **FastAPI** (AI service) · **Neon Postgres** (DB) · **Cloudflare R2** (object storage) · **Hangfire** (background jobs) · **AI Service** (STT/Question Gen).

This guide walks you through configuring and deploying **all six building blocks** of the AIVES system, both **locally** (Docker / native) and **in production** (cloud). Every section ends with a **Verify** checklist.

---

## 0. Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| **Git** | 2.40+ | Clone repo |
| **.NET SDK** | 8.0 | Build backend |
| **Node.js** | 22 LTS | Build frontend |
| **npm** | 10+ | Install JS deps |
| **Python** | 3.11+ | Run AI service (native) |
| **Docker Desktop** | 4.x | Run full stack with one command (recommended for dev) |
| **psql** (optional) | 15+ | Inspect DB |

> **Tip for new contributors:** if you only need to run the system end-to-end, use Docker (§1). If you need to debug a specific service, use native mode (§2).

```bash
git clone https://github.com/<org>/AIVES_FPT.git
cd AIVES_FPT
```

---

## 1. Quick Start (Docker — Recommended for Dev)

The repo ships a `docker-compose.yml` that starts: **Postgres + backend (with auto-migration) + AI service + frontend + Hangfire (embedded in backend)**.

### Step 1.1 — Start the stack
```bash
docker compose up -d --build
```
First build takes 5–10 min. Subsequent runs take ~30 s.

### Step 1.2 — Wait for health checks
```bash
docker compose ps
```
All services should show `(healthy)`. Typical startup order: `postgres` → `db-init` → `backend` → `ai-service` → `frontend`.

### Step 1.3 — Open the apps
- **Frontend:** http://localhost:5173
- **Backend Swagger:** http://localhost:5000/swagger
- **Hangfire Dashboard:** http://localhost:5000/hangfire (dev only)
- **DB health:** http://localhost:5000/api/health/db

### Step 1.4 — Stop the stack
```bash
docker compose down            # keep volumes
docker compose down -v         # drop DB + uploads too
```

**Verify ✅**
- [ ] `GET /api/health/db` returns `Status: Healthy`, `DatabaseConnected: true`
- [ ] Frontend loads, you can sign in with the seeded admin (see backend log on first run for credentials)
- [ ] `docker compose logs backend | tail -50` shows no errors

---

## 2. Native Dev Setup (no Docker)

Use this when you need fast iteration on one service.

### 2.1 — Database (Postgres local or Neon)
Choose one:

**Option A — Local Postgres (matches `docker-compose.yml`):**
```bash
docker run -d --name aives-pg -p 5432:5432 \
  -e POSTGRES_DB=aives_dev -e POSTGRES_USER=aives -e POSTGRES_PASSWORD=aives \
  postgres:16-alpine
```

**Option B — Neon (cloud):** see §3 for provisioning, then grab the pooler connection string.

### 2.2 — Backend
```bash
cd backend
dotnet restore
dotnet run --project Aives.Api -c Debug
```
Listens on https://localhost:7223 (or http://localhost:5223) by default.

**Apply EF migrations** (first time only, or after pulling new migrations):
```bash
cd backend
dotnet ef database update --project Aives.Infrastructure --startup-project Aives.Api
```

### 2.3 — AI Service
```bash
cd ai-service
python -m venv .venv && .venv\Scripts\activate   # Windows
# source .venv/bin/activate                        # macOS/Linux
pip install -r requirements.txt
cp .env.example .env                               # tweak AI_PROVIDER/STT/TTS as needed
python main.py
```
Listens on http://localhost:8000.

### 2.4 — Frontend
```bash
cd frontend/aives-web
npm install
cp .env.example .env.local                         # adjust VITE_API_PROXY_TARGET if needed
npm run dev
```
Listens on http://localhost:5173. The Vite proxy forwards `/api` → backend (default `https://localhost:7223`).

**Verify ✅**
- [ ] Backend `/api/health/db` returns 200
- [ ] An evaluation request succeeds with the configured provider and API key
- [ ] Frontend loads, login works
- [ ] Hangfire dashboard shows the `default` and `answers` queues

---

## 3. Neon Postgres (Cloud DB)

### Step 3.1 — Provision
1. Go to https://console.neon.tech → **Create Project**.
2. Region: pick the closest to your backend (e.g. AWS Singapore for FPT).
3. Plan: Free is fine for dev, Launch for prod.
4. After creation, Neon gives you **two** endpoints on the dashboard:
   - **Pooled** (pgbouncer, port 5432) — for app runtime
   - **Direct** (port 5432, no pooler) — for migrations and admin tasks

### Step 3.2 — Capture connection strings
You need **both**:
- `DefaultConnection` → pooled endpoint
- `MigrationConnection` → direct endpoint (used by EF `Database.Migrate`)

Example format (Npgsql):
```
Host=ep-xxxx-pooler.region.aws.neon.tech;
Database=neondb;
Username=neondb_owner;
Password=npg_xxxxx;
SSL Mode=Require;
Trust Server Certificate=true;
```

### Step 3.3 — Configure
Add to `backend/Aives.Api/appsettings.Production.json` (or env vars, see §7):
```json
{
  "ConnectionStrings": {
    "DefaultConnection":      "Host=<POOLED>; ... ;SSL Mode=Require;Trust Server Certificate=true;",
    "MigrationConnection":    "Host=<DIRECT>; ... ;SSL Mode=Require;Trust Server Certificate=true;"
  }
}
```

### Step 3.4 — Apply schema
```bash
cd backend
ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__MigrationConnection="Host=<DIRECT>;..." \
  dotnet ef database update --project Aives.Infrastructure --startup-project Aives.Api
```

**Verify ✅**
- [ ] Neon dashboard **Tables** shows EF tables + `hangfire.*` (after first Hangfire boot)
- [ ] `GET /api/health/db` reports `UserCount > 0` (admin seeded on first boot)

---

## 4. Cloudflare R2 (Audio Storage)

### Step 4.1 — Create the bucket
1. Cloudflare dashboard → **R2 Object Storage** → **Create bucket** → name: `aives-audio` (matches config).
2. Under **Settings → CORS Policy**, add (replace with your frontend origin):
   ```json
   [{
     "AllowedOrigins": ["https://aives.example"],
     "AllowedMethods": ["GET", "PUT"],
     "AllowedHeaders": ["*"],
     "ExposeHeaders": ["ETag"],
     "MaxAgeSeconds": 3600
   }]
   ```

### Step 4.2 — Generate API token
R2 → **Manage R2 API Tokens** → **Create API Token** with:
- Permissions: **Object Read & Write**
- Scope: bucket = `aives-audio`

Save the **Access Key ID** and **Secret Access Key**. Also note the **Account ID** from the R2 overview page.

### Step 4.3 — Public access (for audio playback)
1. R2 → bucket `aives-audio` → **Settings → Public Access** → **Connect Domain**.
2. Either use the auto-generated `pub-xxxxx.r2.dev` (fine for dev) or attach a custom domain via Cloudflare DNS.

### Step 4.4 — Configure backend
```json
{
  "CloudflareR2": {
    "AccountId":           "9e7xxxxxxxxxxxxx",
    "AccessKeyId":         "f90a3axxxxxxxxxxxxx",
    "SecretAccessKey":     "58c2cbxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "BucketName":          "aives-audio",
    "PublicBaseUrl":       "https://pub-xxxxx.r2.dev",
    "UseLocalStorageFallback": false
  }
}
```

> **Local dev fallback:** set `UseLocalStorageFallback: true` to write to `wwwroot/uploads/` instead of R2 (used by `docker-compose.yml`).

**Verify ✅**
- [ ] Upload a test file via API → see the object appear in R2 dashboard
- [ ] `PublicBaseUrl` URL is reachable in a browser
- [ ] E2E test `CloudflareR2_FullLifecycle_ShouldSucceed` passes:
  ```bash
  dotnet test --filter "FullyQualifiedName~CloudflareR2"
  ```

---

## 5. Hangfire (Background Job Queue)

Hangfire runs **in-process** in the backend — no separate server. It stores jobs in the same Postgres DB under schema `hangfire` (configurable).

### Step 5.1 — Schema isolation
Default schema is `hangfire`. To change, set:
```json
"Hangfire": { "SchemaName": "myapp_hangfire" }
```
The schema is created automatically on first boot.

### Step 5.2 — Worker & queue tuning
```json
"Hangfire": {
  "SchemaName":   "hangfire",
  "WorkerCount":  5
}
```
- `WorkerCount` → number of concurrent jobs. Tune to CPU / I/O.
- Queues are hard-coded in `Program.cs`: `default` and `answers`.

### Step 5.3 — Dashboard
- **Dev:** already exposed at `http://localhost:5000/hangfire` (open in browser, no auth — see §9 for production hardening).
- **Prod:** gate with auth or run a private tunnel. See §9.

### Step 5.4 — Retry behavior
Configured in `Hangfire.PostgreSql` storage options:
- `QueuePollInterval`: 2 s
- `InvisibilityTimeout`: 10 min
- Per-job retries are handled by `AnswerProcessingJob` (it throws `RetryableJobException` on transient errors).

**Verify ✅**
- [ ] `http://localhost:5000/hangfire` shows **Recurring jobs** and **Enqueued** counters
- [ ] Submitting an answer creates a job on the `answers` queue
- [ ] On failure, the job shows up under **Retries** with backoff
- [ ] E2E tests `HangfireQueueRetryTests.*` pass

---

## 6. AI Service (FastAPI — STT + Question Generation)

### Step 6.1 — Choose providers
Edit `ai-service/.env` (copy from `.env.example`):

```bash
# Pick a generation provider
AI_PROVIDER=mock             # mock | gemini | openai
GEMINI_API_KEY=...           # if AI_PROVIDER=gemini
GEMINI_MODEL=gemini-3.1-flash-lite
OPENAI_API_KEY=...           # if AI_PROVIDER=openai

# Pick an STT provider
STT_PROVIDER=mock            # mock | gemini | google | faster_whisper | openai_whisper
GEMINI_STT_MODEL=gemini-3.1-flash-lite
GOOGLE_STT_API_KEY=...
FASTER_WHISPER_MODEL=base    # for local Whisper

# Pick a TTS provider (if used)
TTS_PROVIDER=mock            # mock | google | edge_tts | openai_tts

# Auth: must match backend AI:ApiKey
AI_SERVICE_SECRET_KEY=<same value as AI__ApiKey in backend>

BACKEND_URL=http://localhost:5000    # used for callbacks (if any)
```

### Step 6.2 — Run
**Native:**
```bash
cd ai-service
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

**Docker:** already wired in `docker-compose.yml` (mock providers; override via `environment:` for prod).

### Step 6.3 — Wire to backend
In `backend/appsettings.Production.json`:
```json
{
  "AI": {
    "BaseUrl": "https://ai.internal.example.com",
    "ApiKey":  "<same as AI_SERVICE_SECRET_KEY>",
    "TimeoutSeconds": 300
  }
}
```
The backend **fails fast in production** if `AI:ApiKey` is missing (see `Program.cs` validation).

### Step 6.4 — Validate
```bash
# Verify the configured provider through a real POST evaluation request.
# A provider/API-key error is returned in the response detail and AI-service logs.
```

**Verify ✅**
- [ ] Integration tests pass:
  ```bash
  dotnet test --filter "FullyQualifiedName~SttIntegration|FullyQualifiedName~AiPipelineE2E"
  ```
- [ ] End-to-end: upload audio → transcript appears in DB within 1–2 s

---

## 7. Frontend (Vite + React 19)

### Step 7.1 — Local env
Edit `frontend/aives-web/.env.local`:
```bash
# Public API base (used by axios in browser). Leave empty in dev — Vite proxy handles /api.
VITE_API_BASE_URL=

# Where the Vite dev server forwards /api requests.
VITE_API_PROXY_TARGET=https://localhost:7223
```

For a deployed build, set:
```bash
VITE_API_BASE_URL=https://api.aives.example
```
and **leave `VITE_API_PROXY_TARGET` unset** (prod nginx forwards `/api` directly to the backend container — see `nginx.conf`).

### Step 7.2 — Run
```bash
cd frontend/aives-web
npm install
npm run dev          # http://localhost:5173
```

### Step 7.3 — Production build
```bash
npm run build        # outputs to ./dist
```
The included `Dockerfile` does this and serves via `nginx:stable-alpine`, proxying `/api` and `/uploads` to the backend container.

### Step 7.4 — CORS
The backend CORS allow-list lives in `Program.cs`:
```csharp
policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
      .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
```
**For production**, edit `Program.cs` (or refactor to read from config) and add your real frontend origin, e.g. `https://aives.example`.

**Verify ✅**
- [ ] `npm run build` exits 0
- [ ] `dist/index.html` opens and shows the app
- [ ] `https://api.aives.example/api/health/db` is reachable from the browser console (no CORS error)
- [ ] Login → Dashboard round-trip works

---

## 8. Production Deployment

### 8.1 — Architecture (recommended)
```
        ┌──────────────────────────┐
        │   Cloudflare (CDN + WAF) │
        └─────────────┬────────────┘
                      │
            ┌─────────▼─────────┐
            │  Frontend (nginx) │  https://aives.example
            │  Vite static SPA  │
            └─────────┬─────────┘
                      │ /api
            ┌─────────▼─────────┐
            │   Backend API     │  https://api.aives.example
            │  ASP.NET Core 8   │  (also runs Hangfire workers)
            │  + Hangfire       │
            └─┬──────────────┬──┘
              │              │
   ┌──────────▼─┐      ┌─────▼──────────┐
   │  Neon DB   │      │ Cloudflare R2  │
   │  (pooled)  │      │  audio bucket  │
   └────────────┘      └────────────────┘
              │
   ┌──────────▼──────────┐
   │  AI Service         │  internal-only network
   │  FastAPI / uvicorn  │  https://ai.internal
   └─────────────────────┘
```

### 8.2 — Pick a host
Common options that work with the existing `Dockerfile`s:
- **AWS ECS Fargate** (or EC2 + ALB) — straightforward ECS task per service
- **Azure Container Apps** — auto-scale, simple revision management
- **Fly.io** — great for small teams, easy multi-region
- **Render / Railway** — easiest; supports the `docker-compose.yml` pattern

> All Dockerfiles already run as non-root, expose a single port, and include a healthcheck.

### 8.3 — Backend env vars (production)
Set these on your platform (ECS task def, Fly secrets, etc.):

| Env var | Value | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Enables HTTPS redirect, fail-fast on missing AI key |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen on 8080 inside container |
| `ConnectionStrings__DefaultConnection` | `Host=<neon-pooled>; ... SSL Mode=Require;Trust Server Certificate=true;` | |
| `ConnectionStrings__MigrationConnection` | `Host=<neon-direct>; ...` | Used by `Database.MigrateAsync` if you wire it in |
| `Jwt__Key` | `<32+ char random>` | **`dotnet user-secrets` for local, secrets manager for prod** |
| `Jwt__Issuer` | `AIVES` | |
| `Jwt__Audience` | `AIVES.Client` | |
| `AI__BaseUrl` | `https://ai.internal.example.com` | |
| `AI__ApiKey` | `<same as AI_SERVICE_SECRET_KEY>` | **Required in prod** (validation throws otherwise) |
| `AI__TimeoutSeconds` | `300` | |
| `CloudflareR2__AccountId` / `AccessKeyId` / `SecretAccessKey` / `BucketName` / `PublicBaseUrl` | from §4 | |
| `Hangfire__SchemaName` | `hangfire` | |
| `Hangfire__WorkerCount` | `5` | tune to CPU |

> EF migrations: either run `dotnet ef database update` from CI before deploy, or call `Database.MigrateAsync()` on startup (add this call in `Program.cs` for prod, behind a `MigrateOnStartup` flag — currently the app does `EnsureCreatedAsync` only for the local Docker dev DB).

### 8.4 — AI Service env vars (production)
| Env var | Value | Required? | Description |
|---|---|---|---|
| `AI_SERVICE_ENV` | `production` | ✅ | |
| `AI_SERVICE_SECRET_KEY` | same as backend `AI__ApiKey` | ✅ | Sent via the `X-Internal-Token` header to `transcribe-object` |
| `AI_PROVIDER` | `gemini` (or `openai`) | ✅ | |
| `GEMINI_API_KEY` / `OPENAI_API_KEY` | from provider | depends on provider | |
| `STT_PROVIDER` | `gemini` / `faster_whisper` / `google` | ✅ | |
| `STT_MAX_AUDIO_BYTES` | `14680064` (~14 MB) | recommended | Caps the audio size STT accepts |
| `TTS_PROVIDER` | `edge_tts` / `google` / `openai_tts` | recommended | |
| `FASTER_WHISPER_MODEL` | `base` | if using local | `tiny`/`base`/`small`/`medium` |
| `FASTER_WHISPER_DEVICE` | `cpu` | if using local | `cuda` if a GPU is available |
| `FASTER_WHISPER_COMPUTE_TYPE` | `int8` | if using local | |
| **R2 (new — PR #41)** ||||
| `R2_ACCOUNT_ID` | from Cloudflare R2 | ✅ if calling `transcribe-object` | |
| `R2_ENDPOINT_URL` | `https://<account_id>.r2.cloudflarestorage.com` | ✅ | Derived from the Account ID if left blank |
| `R2_ACCESS_KEY_ID` / `R2_SECRET_ACCESS_KEY` | R2 API token | ✅ | **Read** permission on the bucket is enough for STT |
| `R2_BUCKET_NAME` | `aives-exam-audios` | ✅ | Same bucket the backend uses |
| `BACKEND_URL` | internal URL of backend | ✅ if using callback | |

> **Important — PR #41 (`transcribe-object`):** as of this version the AI service **downloads audio from R2 on its own** using its own credentials, so the backend no longer needs to sign a presigned URL. The backend only has to send the `object_key` (stored in `answers.object_key`) in the JSON body along with the `X-Internal-Token` header.

### 8.5 — Frontend
Build the image (or let your platform build from `frontend/aives-web/Dockerfile`) with:
```bash
docker build -t aives-web frontend/aives-web \
  --build-arg VITE_API_BASE_URL=https://api.aives.example
```
The default `Dockerfile` hard-codes `VITE_API_BASE_URL=""`. To inject at build time, edit the Dockerfile:
```dockerfile
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
```
…and rebuild.

### 8.6 — Smoke test after deploy
```bash
# 1. DB
curl https://api.aives.example/api/health/db
# 2. AI service (internal only)
# Verify the provider with a POST evaluation request; there is no AI-service GET health/model endpoint.
# 3. Frontend
curl -I https://aives.example
# 4. End-to-end: log in via UI, submit a question, check Hangfire dashboard
```

**Verify ✅**
- [ ] `/api/health/db` is `Healthy`
- [ ] No 5xx in platform logs for 15 min
- [ ] Hangfire dashboard shows the configured `WorkerCount` workers
- [ ] A test audio upload appears in R2 within 5 s

---

## 9. Security & Hardening Checklist

- [ ] **Secrets** — never commit `appsettings.Development.json` with real creds. Use `dotnet user-secrets` (local) and platform secret manager (prod). Rotate Neon & R2 keys every 90 days.
- [ ] **JWT** — generate `Jwt:Key` with `openssl rand -hex 32`. Store in secret manager only.
- [ ] **CORS** — remove `http://localhost:5173` from the `CorsPolicy` allow-list in `Program.cs` for prod. Add only your real origin(s).
- [ ] **Hangfire dashboard** — gate behind auth in prod (e.g. wrap `app.UseHangfireDashboard` with `[Authorize(Roles="Admin")]` or a separate auth scheme).
- [ ] **HTTPS** — terminate at the load balancer, redirect in the app (`app.UseHttpsRedirection()` is already on for non-dev).
- [ ] **Rate limiting** — `LoginPolicy` is wired (100 / 15 min / IP). Confirm it survives your proxy (preserve `X-Forwarded-For`).
- [ ] **R2 bucket** — private by default; only enable the public host for the `audio/*` prefix (R2 supports custom domains per bucket, not per prefix — use signed URLs for sensitive files).
- [ ] **DB user** — use a least-privilege role for runtime; keep the owner role only for migrations.

---

## 10. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `Jwt:Key configuration is required.` at startup | Missing env var | Set `Jwt__Key` (≥32 chars) |
| `AI:ApiKey is required in production.` | Missing `AI__ApiKey` | Set it in secret manager; in dev it's warn-only |
| `psql: SSL SYSCALL error` | DB connection without `SSL Mode=Require` | Add `SSL Mode=Require;Trust Server Certificate=true;` to the connection string |
| Hangfire jobs stuck in `Enqueued` | No workers running | Check `WorkerCount`, ensure backend is not behind a proxy that drops long-poll |
| Frontend shows `Network Error` on login | CORS or wrong `VITE_API_BASE_URL` | Check `Program.cs` CORS list; in dev, ensure `VITE_API_PROXY_TARGET` points to the running backend |
| R2 upload returns 403 | Wrong AccessKey/Secret, or bucket region mismatch | R2 uses the Account ID as the endpoint host — verify `endpoint = https://<AccountId>.r2.cloudflarestorage.com` |
| Neon pooler times out under load | `MaxPoolSize` not set | Append `Maximum Pool Size=50;` to the pooled connection string |
| `EnsureCreatedAsync` is restricted to local Docker | By design — guards against accidental prod wipe | Use `dotnet ef database update` in CI for prod |
| AI tests fail intermittently | xUnit running tests in parallel against shared state | `Aives.Tests/xunit.runner.json` is set to `parallelizeTestCollections: false` — do not delete it |

---

## 11. Quick Reference — All Environment Variables

### Backend (.NET)
```
ASPNETCORE_ENVIRONMENT
ASPNETCORE_URLS
ConnectionStrings__DefaultConnection
ConnectionStrings__MigrationConnection
Jwt__Key
Jwt__Issuer
Jwt__Audience
AI__BaseUrl
AI__ApiKey
AI__TimeoutSeconds
AI__QuestionGenerationTimeoutSeconds
CloudflareR2__AccountId
CloudflareR2__AccessKeyId
CloudflareR2__SecretAccessKey
CloudflareR2__BucketName
CloudflareR2__PublicBaseUrl
CloudflareR2__UseLocalStorageFallback
Hangfire__SchemaName
Hangfire__WorkerCount
Docker__InitializeDatabase      # local dev only — DO NOT set in prod
```

### AI Service (Python)
```
AI_SERVICE_PORT / HOST / ENV / LOG_LEVEL
AI_SERVICE_SECRET_KEY
AI_PROVIDER
GEMINI_API_KEY / GEMINI_MODEL / GEMINI_STT_MODEL
OPENAI_API_KEY / OPENAI_MODEL
STT_PROVIDER
STT_MAX_AUDIO_BYTES                 # PR #41 — default ~14 MB cap
GOOGLE_STT_API_KEY
FASTER_WHISPER_MODEL
FASTER_WHISPER_DEVICE               # PR #41 — cpu | cuda
FASTER_WHISPER_COMPUTE_TYPE         # PR #41 — int8 | float16 | ...
TTS_PROVIDER
GOOGLE_TTS_API_KEY
BACKEND_URL
# PR #41 — used for the /speech/transcribe-object endpoint
R2_ACCOUNT_ID
R2_ENDPOINT_URL                     # optional: auto-derived from Account ID
R2_ACCESS_KEY_ID
R2_SECRET_ACCESS_KEY
R2_BUCKET_NAME
```

### Frontend (Vite)
```
VITE_API_BASE_URL
VITE_API_PROXY_TARGET        # dev only
```

### Cloudflare R2 (per bucket)
```
AccountId, AccessKeyId, SecretAccessKey, BucketName, PublicBaseUrl
```

### Neon (per project)
```
Pooled connection string  →  DefaultConnection
Direct connection string  →  MigrationConnection
```

---

## 12. Where things live in the repo

| Component | Path |
|---|---|
| Backend source | `backend/Aives.{Api,Application,Domain,Infrastructure}` |
| Backend Dockerfile | `backend/Aives.Api/Dockerfile` |
| Backend settings | `backend/Aives.Api/appsettings*.json` |
| EF migrations | `backend/Aives.Infrastructure/Persistence/Migrations` |
| Hangfire wiring | `backend/Aives.Api/Program.cs` (`AddHangfire`, `AddHangfireServer`) |
| Cloudflare R2 client | `backend/Aives.Infrastructure/Storage/CloudflareR2StorageService.cs` |
| AI Service source | `ai-service/app/` |
| AI Service Dockerfile | `ai-service/Dockerfile` |
| AI Service env | `ai-service/.env.example` |
| Frontend source | `frontend/aives-web/src/` |
| Frontend Dockerfile + nginx | `frontend/aives-web/Dockerfile`, `frontend/aives-web/nginx.conf` |
| Frontend env | `frontend/aives-web/.env.example` |
| Full stack orchestration | `docker-compose.yml` (repo root) |
| Architecture diagrams | `docs/architecture/` |
| API contracts | `docs/api/` |
| ERD | `docs/documentation/erd.drawio.png` |

---

**Done.** If anything in this guide drifts from the code, please update it in the same PR.
