# Database setup for Neon

## The problem this solves

`dotnet ef database update` **never worked** on an empty database in this repo, and
still does not. The nine migrations in `Aives.Infrastructure/Migrations` are
incremental only — every one of them calls `AddColumn` / `CreateIndex` against
tables that no committed migration ever created:

| Migration | CreateTable | AddColumn |
|---|---|---|
| `20261008172917_AddSessionCodeAndMonitoringFields` | 0 | 6 |
| `20261012090000_AddQuestionBankTopic` | 0 | 1 |
| `20261013063254_AddQuestionRubrics` | 1 | 1 |
| `20261013142810_AddAiFeedbackAnalysis` | 0 | 4 |
| `20261013182240_AddQuestionBankLevel` | 0 | 1 |
| `20261016100236_RestructureRolesAndAddAuditFields` | 0 | 2 |
| `20261016161324_AddQuestionApprovalAndExpectedKeywords` | 0 | 5 |
| `20261018211259_AddAuditLogsAndEnhanceAnswerEntity` | 1 | 6 |
| `20261023104325_AddExamIsActive` | 0 | 1 |

The initial migration was never committed. The first one (`bc7676a`, 2026-10-09)
already alters `exam_sessions`, and its snapshot already describes all 14 tables and
5 enums — so the base schema was created by hand outside of Git, and incremental
migrations were tracked from that point on.

On a fresh database you get:

```
42P01: relation "exam_sessions" does not exist
```

## Setup on a NEW Neon database

**Step 1 — create the schema.** Run `db/baseline.sql` in the Neon console
(*SQL Editor → Paste → Run*), or from a terminal:

```powershell
psql "postgresql://<user>:<password>@<host>/<db>?sslmode=require" -v ON_ERROR_STOP=1 -f db/baseline.sql
```

`baseline.sql` was dumped schema-only from the Neon database the team already runs, so
it matches the real schema exactly: 14 tables, 5 enums, 24 foreign keys, 36 indexes.
It contains **no rows**. Every statement is guarded, so re-running it is safe.

**Step 2 — record the existing migrations as applied.** Required once per new
database, because the baseline already contains every column those migrations add:

```powershell
dotnet ef database update 0   # nothing to do
```

If you see `42701: column "session_code" ... already exists`, that is expected — the
baseline is ahead of the migration history. Record the nine migrations without
replaying them:

```powershell
dotnet ef migrations script 20261008172917_AddSessionCodeAndMonitoringFields 20261023104325_AddExamIsActive --idempotent --output $env:TEMP\seed.sql
```

then run `seed.sql`, **edited** so that each guarded block inserts into
`__EFMigrationsHistory` instead of running the `ALTER TABLE`. Example:

```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008172917_AddSessionCodeAndMonitoringFields', '8.0.11')
ON CONFLICT DO NOTHING;
```

Do this for all nine ids. Afterwards `dotnet ef database update` is a clean no-op and
future migrations apply normally.

## Verifying

```powershell
dotnet ef migrations list --project Aives.Infrastructure --startup-project Aives.Api
dotnet ef migrations has-pending-model-changes --project Aives.Infrastructure --startup-project Aives.Api
```

`has-pending-model-changes` must print *"No changes have been made to the model since
the last migration"* — otherwise the model has drifted from the snapshot and a new
migration is required.

## Regenerating baseline.sql

```powershell
# PGPASSWORD=... pg_dump --schema-only --no-owner --no-privileges -n public -f baseline_raw.sql
.\tools\make_baseline.ps1
```

The script re-applies the idempotency guards (`CREATE TABLE IF NOT EXISTS`, enum
`DO` blocks, `pg_constraint` checks for constraints, `information_schema` checks for
identity columns) and drops the psql-only `\restrict` meta-commands, which are not
valid SQL in the Neon console.