#!/usr/bin/env bash
# First-time bring-up on a Docker host. Run from the repo root. Requires Docker + a filled .env.
# This does the parts that can be automated; the CTFd admin token (step 3) is created in the UI.
set -euo pipefail

[ -f .env ] || { echo "Create .env from .env.example and fill it in first."; exit 1; }
command -v docker >/dev/null || { echo "Docker not found. Run this on the deploy host."; exit 1; }

HOST="${CTF_HOST:-localhost}"

echo "==> Building and starting the stack (this pulls SQL Server, CTFd, etc. on first run)..."
docker compose up -d --build

echo "==> Waiting for db-init (per-challenge DBs + scoped logins) to finish..."
until [ "$(docker compose ps -a --format '{{.Service}} {{.State}}' | grep '^db-init ' | awk '{print $2}')" = "exited" ]; do sleep 3; done
docker compose logs db-init | tail -3

echo "==> Waiting for CTFd at http://$HOST:8000 ..."
until curl -sf "http://$HOST:8000" >/dev/null 2>&1; do sleep 3; done

cat <<EOF

Stack is up. Finish these (see docs/operator/DEPLOY.md):
  1) Open http://$HOST:8000 and complete CTFd first-run (create the admin account).
  2) CTFd -> Settings -> Access Tokens -> generate an admin token.
  3) Load the flags, then create all challenges:
       set -a; . ./.env; set +a
       CTF_URL=http://$HOST:8000 CTF_HOST=$HOST CTF_ADMIN_TOKEN=<token> python3 ctfd/seed-ctfd.py --dry-run
       CTF_URL=http://$HOST:8000 CTF_HOST=$HOST CTF_ADMIN_TOKEN=<token> python3 ctfd/seed-ctfd.py
  4) Set CTFd global config: dynamic scoring, submission rate limit (~10/min), registration policy.
  5) Work the Verification checklist in docs/operator/DEPLOY.md (incl. the cross-DB isolation check).
  6) Add scripts/reset.sh to cron (hourly). For C11 per-player containers, see ctfd/plugins/README.md.
EOF
