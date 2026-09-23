#!/usr/bin/env bash
# Hourly reset of shared challenge state so one player's mess doesn't block the next.
# Run from the repo root (where docker-compose.yml is). Cron example at the bottom.
#
# Only challenges whose intended solve MUTATES or ACCUMULATES data need resetting:
#   c5  (mass assignment flips IsAdmin), c6 (comments/XSS pile up)  -> reseed the SQL DB
#   c7  (report/flag files), c11 (SQLite race state), c12 (dashboard) -> self-reseed on restart
# Read-only challenges (c1, c2, c4, c8, c9, c10) don't change data, so they're left alone.
# Earned points are unaffected — flags live in CTFd, not challenge state.
set -uo pipefail

log() { echo "[$(date '+%F %T')] $*"; }

# SQL Server challenges: stop (release connections) -> reseed (drop+recreate) -> start.
reseed_sql() {
  local svc="$1"
  log "reseeding $svc ..."
  docker compose stop "$svc" >/dev/null 2>&1
  docker compose run --rm "$svc" --reseed >/dev/null 2>&1 && log "  $svc reseeded" || log "  $svc reseed FAILED"
  docker compose start "$svc" >/dev/null 2>&1
}

for svc in vuln-c5 vuln-c6; do
  reseed_sql "$svc"
done

# Self-contained challenges reseed/regenerate on startup — a restart is enough.
log "restarting self-resetting challenges ..."
docker compose restart vuln-c7 vuln-c11 vuln-c12 >/dev/null 2>&1 && log "  done" || log "  restart FAILED"

log "reset complete."

# --- Cron: run hourly. Add to the deploy host's crontab (crontab -e): ---
#   0 * * * * cd /path/to/internal-ctf && ./scripts/reset.sh >> /var/log/ctf-reset.log 2>&1
