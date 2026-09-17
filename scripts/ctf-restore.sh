#!/usr/bin/env bash
# ctf-restore — put a player's practice database back to the pristine seed.
#
# Players run this (or click the Restore button) when they've broken their sandbox. It only
# ever resets the CALLER's own database. It never removes earned scoreboard points (those
# live in CTFd, not here).
#
#   ctf-restore                     # reset MY SQLite playground
#   ctf-restore --sqlserver         # reset MY SQL Server Playground_<me>
#   ctf-restore --challenge 5       # reset MY per-player data for a scored challenge
#   ctf-restore --player <name>     # OPERATOR ONLY: reset someone else's (needs privilege)
#
# Layout assumed on the player host:
#   pristine SQLite seed : /opt/ctf/sql-playground/playground.seed.db  (read-only)
#   player working copy  : ~/ctf/playground.db
#   SQL Server seed sql  : /opt/ctf/sql-playground/{schema.sql,seed.sql}
set -euo pipefail

SEED_DIR="${CTF_SEED_DIR:-/opt/ctf/sql-playground}"
PLAYER="${USER:-player}"
ENGINE="sqlite"
CHALLENGE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --sqlserver) ENGINE="sqlserver"; shift ;;
    --challenge) CHALLENGE="${2:?challenge number required}"; shift 2 ;;
    --player)    PLAYER="${2:?player name required}"; shift 2 ;;   # operator override
    -h|--help)   grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done

restore_sqlite() {
  local home_dir seed dst
  home_dir=$(getent passwd "$PLAYER" | cut -d: -f6); home_dir="${home_dir:-$HOME}"
  seed="$SEED_DIR/playground.seed.db"
  dst="$home_dir/ctf/playground.db"
  [[ -f "$seed" ]] || { echo "pristine seed not found: $seed (build it with build_sqlite.py)" >&2; exit 1; }
  mkdir -p "$(dirname "$dst")"
  cp -f "$seed" "$dst"
  echo "Restored SQLite playground for '$PLAYER' -> $dst"
}

restore_sqlserver() {
  : "${SA_PASSWORD:?set SA_PASSWORD to reach SQL Server}"
  local db="Playground_${PLAYER}"
  echo "Restoring SQL Server database $db ..."
  # Re-create the schema then re-seed, scoped to this player's own database only.
  sqlcmd -S "${VULN_DB_HOST:-vuln-db.internal}" -U sa -P "$SA_PASSWORD" -b \
    -Q "IF DB_ID('$db') IS NULL CREATE DATABASE [$db];"
  sqlcmd -S "${VULN_DB_HOST:-vuln-db.internal}" -U sa -P "$SA_PASSWORD" -d "$db" -b -i "$SEED_DIR/schema.sql"
  sqlcmd -S "${VULN_DB_HOST:-vuln-db.internal}" -U sa -P "$SA_PASSWORD" -d "$db" -b -i "$SEED_DIR/seed.sql"
  echo "Restored SQL Server $db"
}

if [[ -n "$CHALLENGE" ]]; then
  # Scored challenges with per-player mutable data (e.g. C5). Each such challenge ships its
  # own reset under $SEED_DIR/challenge-<n>/. Fall back to a clear message if absent.
  cdir="$SEED_DIR/challenge-$CHALLENGE"
  if [[ -d "$cdir" && -x "$cdir/reset.sh" ]]; then
    CTF_PLAYER="$PLAYER" "$cdir/reset.sh"
    echo "Reset challenge $CHALLENGE data for '$PLAYER'"
  else
    echo "Challenge $CHALLENGE has no per-player reset (it may be shared / read-only)." >&2
    echo "Shared challenges are reset by the operator on a schedule." >&2
    exit 1
  fi
  exit 0
fi

case "$ENGINE" in
  sqlite)    restore_sqlite ;;
  sqlserver) restore_sqlserver ;;
esac
echo "Note: earned scoreboard points are unaffected — flags live in CTFd, not this database."
