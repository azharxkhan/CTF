#!/usr/bin/env bash
# CHALLENGE 3 — Secret in git history.
#
# Builds a small, realistic repo whose history contains a production connection string (with
# the flag as its password), then "removes" it in a later commit. HEAD looks clean, but the
# secret is still recoverable from history — which is the whole lesson.
#
#   ./seed-history.sh [output-dir]      # default: ./repo next to this script
#   FLAG_C3_GITSECRET=flag{...} ./seed-history.sh
#
# The generated repo is what players get (read-only) on the player host.
set -euo pipefail

OUT="${1:-$(cd "$(dirname "$0")" && pwd)/repo}"
# NB: don't put the {…} flag in a ${VAR:-default} — the inner } closes the expansion early.
FLAG="${FLAG_C3_GITSECRET:-}"
[ -z "$FLAG" ] && FLAG='flag{rotate_dont_just_delete}'

rm -rf "$OUT"
mkdir -p "$OUT"
cd "$OUT"

git init -q
git config user.name "Priya Nair"
git config user.email "priya@corp.local"
git config commit.gpgsign false

# Deterministic dates so the history reads like a few days of real work.
c() { # c <date> <message>
  GIT_AUTHOR_DATE="$1 12:00:00" GIT_COMMITTER_DATE="$1 12:00:00" \
    git commit -q -m "$2"
}

# --- Commit 1: project skeleton (local dev config, no secret) ---
cat > README.md <<'EOF'
# Acme Billing Service

Internal service that issues and reconciles invoices.

## Configuration
Local development uses `appsettings.json`. See the team wiki for details.
EOF

cat > BillingService.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
EOF

cat > appsettings.json <<'EOF'
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\MSSQLLocalDB;Database=Billing;Trusted_Connection=True"
  }
}
EOF

cat > Program.cs <<'EOF'
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/health", () => "ok");
app.Run();
EOF

git add -A
c 2025-03-03 "Initial commit: billing service skeleton"

# --- Commit 2: THE LEAK — production config with a real connection string ---
cat > appsettings.Production.json <<EOF
{
  "ConnectionStrings": {
    "Default": "Server=sql-prod-01.corp.local;Database=Billing;User Id=svc_billing;Password=${FLAG}"
  }
}
EOF
git add -A
c 2025-03-04 "Add production configuration"

# --- Commit 3: normal work, reads config ---
cat > Program.cs <<'EOF'
var builder = WebApplication.CreateBuilder(args);
var cs = builder.Configuration.GetConnectionString("Default");
var app = builder.Build();
app.MapGet("/health", () => "ok");
app.Run();
EOF
git add -A
c 2025-03-05 "Wire up connection string from configuration"

# --- Commit 4: "remove" the secret (but it's already in history) ---
git rm -q appsettings.Production.json
cat > .gitignore <<'EOF'
# Never commit environment-specific secrets
appsettings.Production.json
appsettings.*.local.json
.env
EOF
cat >> README.md <<'EOF'

## Secrets
Production secrets are supplied via environment variables, never committed. Do not add
`appsettings.Production.json` to the repo.
EOF
git add -A
c 2025-03-06 "Remove prod secrets from repo; supply via environment variables"

echo "Seeded git-secret repo at: $OUT"
echo "HEAD is clean:"; git -C "$OUT" ls-files | sed 's/^/  /'
echo "Intended find: git log -p | grep -i password   (or git log --all, then git show <commit>)"
