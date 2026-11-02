# AIVES – AI-powered Viva Exam System

Frontend: React + TypeScript
Backend: ASP.NET Core Web API
AI: Python FastAPI
Database: Neon PostgreSQL

## Architecture
```text
React
  ↓
ASP.NET Core
  ├──→ Neon PostgreSQL
  │
  └──→ Python FastAPI
          ↓
         AI
```

## Docker with a shared PostgreSQL database

Docker Compose reads `DATABASE_CONNECTION` from the root `.env` file. Copy
`.env.example` to `.env`, replace the placeholders with the database connection
details (use the Npgsql `Host=...;Database=...;Username=...;Password=...` format),
then run:

```powershell
docker compose up --build -d
```

The shared database must already have the schema expected by the backend.
Compose does not create or migrate the shared database. Keep `.env` private and
never commit it. Rotate any database password shared in chat before use.
