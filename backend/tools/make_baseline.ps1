$ErrorActionPreference = 'Stop'
$raw = "$env:TEMP\baseline_raw.sql"
$out = 'C:\Users\thinh\Documents\GitHub\AIVES_FPT\backend\db\baseline.sql'
New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null

$sql = Get-Content $raw

# Make every object creation idempotent so the baseline can be re-run safely.
$sql = $sql -replace '^CREATE TABLE ', 'CREATE TABLE IF NOT EXISTS '
$sql = $sql -replace '^CREATE (UNIQUE )?INDEX ', 'CREATE $1INDEX IF NOT EXISTS '

# PostgreSQL has no CREATE TYPE IF NOT EXISTS, so guard each enum in a DO block.
$outLines = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $sql.Count; $i++) {
    $line = $sql[$i]
    if ($line -match '^CREATE TYPE ([\w\.]+) AS ENUM \($') {
        $name = $Matches[1]
        $body = New-Object System.Collections.Generic.List[string]
        # Enum values span until the statement-closing ");".
        for ($j = $i + 1; $j -lt $sql.Count; $j++) {
            $body.Add($sql[$j])
            if ($sql[$j] -match '^\);') { $i = $j; break }
        }
        $outLines.Add('DO $enum$ BEGIN')
        $outLines.Add(('    CREATE TYPE {0} AS ENUM (' -f $name))
        $outLines.Add(($body[0..($body.Count - 2)] -join "`n"))
        $outLines.Add('    );')
        $outLines.Add('    EXCEPTION WHEN duplicate_object THEN NULL;')
        $outLines.Add('END $enum$;')
        continue
    }
    $outLines.Add($line)
}
$sql = $outLines

# Drop pg_dump bookkeeping comments; keep type comments (they describe enums).
$sql = $sql | Where-Object { $_ -notmatch '^--$' -and $_ -notmatch '^-- Dumped (from|by)' }

# psql-only meta-commands (\restrict) are not valid SQL and break other clients.
$sql = $sql | Where-Object { $_ -notmatch '^\\(restrict|unrestrict)\b' }

# pg_dump emits "SELECT pg_catalog.set_config('search_path', '', false)". Neon keeps
# that setting on the connection afterwards, so every later unqualified query fails
# with 42P01 "relation users does not exist" - the app dies at startup. Restore the
# default instead of leaving the search path empty.
$sql = $sql -replace [regex]::Escape("SELECT pg_catalog.set_config('search_path', '', false);"), "SELECT pg_catalog.set_config('search_path', 'public', false);"

# "public" always exists on a fresh PostgreSQL database (and on Neon), so creating
# it fails with 42P06. Nothing in this script needs it to be created.
$sql = $sql | Where-Object { $_ -notmatch '^CREATE SCHEMA public;' }

# COMMENT ON SCHEMA public fails on a database that already has the schema.
$sql = $sql | Where-Object { $_ -notmatch '^COMMENT ON SCHEMA ' }

# "ALTER TABLE x ALTER COLUMN y ADD GENERATED ALWAYS AS IDENTITY (...)" implicitly
# creates a sequence, so it fails with 42P07 when the column already has one.
$identityOut = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $sql.Count; $i++) {
    $line = $sql[$i]
    $isIdentity = $line -match '^ALTER TABLE (.+?) ALTER COLUMN (.+?) ADD GENERATED ALWAYS AS IDENTITY \($'
    if ($isIdentity) {
        $table = $Matches[1]
        $column = $Matches[2]
        $opts = New-Object System.Collections.Generic.List[string]
        for ($j = $i + 1; $j -lt $sql.Count; $j++) {
            $opts.Add($sql[$j])
            if ($sql[$j] -match '^\);') { $i = $j; break }
        }
        $identityOut.Add('DO $id$ BEGIN')
        $identityOut.Add(('    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = ''public'' AND table_name = ''{0}'' AND column_name = ''{1}'' AND is_identity = ''YES'') THEN' -f ($table -replace '^public\.', ''), $column))
        $identityOut.Add(('        ALTER TABLE {0} ALTER COLUMN {1} ADD GENERATED ALWAYS AS IDENTITY (' -f $table, $column))
        $identityOut.Add(($opts[0..($opts.Count - 2)] -join "`n"))
        $identityOut.Add('        );')
        $identityOut.Add('    END IF;')
        $identityOut.Add('END $id$;')
        continue
    }
    $identityOut.Add($line)
}
$sql = $identityOut

# Constraints have no IF NOT EXISTS either: pg_dump writes them as two lines
# ("ALTER TABLE ONLY x" / "    ADD CONSTRAINT ..."). Wrap each pair in a DO block
# that checks pg_constraint explicitly - guessing the SQLSTATE is unreliable because
# a duplicate PRIMARY KEY and a duplicate FOREIGN KEY raise different codes.
$constraintOut = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $sql.Count; $i++) {
    $line = $sql[$i]
    $isAlterOnly = $line -match '^ALTER TABLE ONLY (.+)$'
    if (-not $isAlterOnly) { $constraintOut.Add($line); continue }
    $table = $Matches[1]
    $nextExists = ($i + 1) -lt $sql.Count
    if (-not $nextExists) { $constraintOut.Add($line); continue }
    $isAddConstraint = $sql[$i + 1] -match '^\s+ADD CONSTRAINT "?([\w]+)"? '
    if (-not $isAddConstraint) { $constraintOut.Add($line); continue }
    $consName = $Matches[1]
    $stmt = ($sql[$i + 1].Trim() -replace ';+$', '')
    $constraintOut.Add('DO $c$ BEGIN')
    $constraintOut.Add(('    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = ''{0}'') THEN' -f $consName))
    $constraintOut.Add(('        ALTER TABLE ONLY {0}' -f $table))
    $constraintOut.Add(('            {0};' -f $stmt))
    $constraintOut.Add('    END IF;')
    $constraintOut.Add('END $c$;')
    $i++
}
$sql = $constraintOut

$header = @'
-- AIVES baseline schema (PostgreSQL 18)
--
-- WHY THIS FILE EXISTS
-- The EF migrations in Aives.Infrastructure/Migrations are incremental only: every one
-- of them uses AddColumn/CreateIndex against tables that were never created by a
-- committed migration. The first migration (20261008172917) already alters
-- "exam_sessions", so `dotnet ef database update` on an empty database fails with
--   ERROR: relation "exam_sessions" does not exist
-- This file is that missing baseline. It was dumped schema-only from the Neon
-- database that the team already runs, so it matches the real schema exactly.
--
-- HOW TO USE (first-time setup on a NEW database)
--   psql -d <your-database> -v ON_ERROR_STOP=1 -f db/baseline.sql
--   dotnet ef database update --project Aives.Infrastructure --startup-project Aives.Api
--
-- Every statement is IF NOT EXISTS, so it is safe to re-run against a database that
-- already has the schema. Run this ONLY on a new/empty database: on an existing
-- database the migrations already cover it and this file is a no-op.
--
-- Contains schema only - no rows. Generated with:
--   pg_dump --schema-only --no-owner --no-privileges

'@

($header + ($sql -join "`n") + "`n") | Set-Content $out -Encoding utf8
Write-Output "written: $out"
Write-Output "lines: $((Get-Content $out | Measure-Object -Line).Lines)"