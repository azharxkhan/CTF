#!/usr/bin/env bash
# Create each challenge's database and a LEAST-PRIVILEGE login scoped to only that database.
# This is what stops a SQL-injection in one challenge from cross-DB reading another
# challenge's flag (verified: with sa/sysadmin, `UNION ... FROM Vuln_C4.dbo.Flags` works;
# with a db-scoped login it's denied). Each app connects as its own cN_app login, never sa.
#
# Runs once as the db-init service after vuln-db is healthy. Idempotent.
set -euo pipefail

SQLCMD=(/opt/mssql-tools18/bin/sqlcmd -S vuln-db -U sa -P "$SA_PASSWORD" -C -b)

# SQL-injection challenges (and any DB-backed one). C11 uses SQLite (self-contained), and
# the file-based challenges (C3 git, W*) need no DB, so they're not listed.
for c in c1 c2 c4 c5 c6 c7 c8 c9 c10 c12; do
  DB="Vuln_${c^^}"          # c1 -> Vuln_C1
  LOGIN="${c}_app"
  echo "== $DB / $LOGIN =="
  "${SQLCMD[@]}" -Q "IF DB_ID('$DB') IS NULL CREATE DATABASE [$DB];"
  "${SQLCMD[@]}" -Q "IF SUSER_ID('$LOGIN') IS NULL CREATE LOGIN [$LOGIN] WITH PASSWORD='${DB_APP_PASSWORD}', CHECK_POLICY=OFF;"
  # db_owner on its OWN database only (lets EnsureCreated build the schema); no rights elsewhere.
  "${SQLCMD[@]}" -d "$DB" -Q "IF USER_ID('$LOGIN') IS NULL CREATE USER [$LOGIN] FOR LOGIN [$LOGIN]; ALTER ROLE db_owner ADD MEMBER [$LOGIN];"
done

echo "db-init complete: per-challenge databases + scoped logins ready."
