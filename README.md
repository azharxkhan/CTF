# Internal CTF

An always-on, internal capture-the-flag platform for teaching secure coding. Colleagues
and juniors break deliberately-vulnerable lookalike apps, then earn bonus points by
submitting a working **patch** — the fix is the point.

Players do all their work from a **locked-down player host** they reach through a browser
(Apache Guacamole). That host has **no outbound internet**, so challenge content cannot be
pasted into an external AI, and everything a player needs — the field manual, white-box
source, and the toolkit — is pre-loaded.

## Layout

| Path | What it is |
|------|-----------|
| [`docker-compose.yml`](docker-compose.yml) | CTFd + every challenge instance + review + proxy, one command up. |
| [`challenges/`](challenges/) | The vulnerable .NET API (all 12 challenges) + React app, git-secret seed, static warm-ups. |
| [`ctfd/`](ctfd/) | CTFd config, the challenge auto-loader (`seed-ctfd.py`), and the per-player-container plugin. |
| [`db/`](db/) | `init.sh` — per-challenge databases + least-privilege logins (blocks cross-DB cheating). |
| [`proxy/`](proxy/) | Caddy config exposing the internal-only challenges to players. |
| [`review/`](review/) | Patch / explain-your-solve service + reviewer CLI. |
| [`writeups/`](writeups/) | Per-challenge write-ups (vuln → exploit → fix → lesson), published on solve. |
| [`player-host/`](player-host/) | The locked-down host players log into: provisioning, network lockdown, the Flag app. |
| [`guacamole/`](guacamole/) | Browser gateway to the player host (clipboard/file-transfer off, session recording on). |
| [`field-manual/`](field-manual/) | The player handbook (served on the player host). |
| `docs/` | Internal docs (SETUP, DEPLOY runbook, admin solve guide, player guides, plan). Kept local, not in this repo. |

## Documentation

- **Write-ups** — [`writeups/`](writeups/): one per challenge, the vuln, the exploit, the
  secure fix, and the lesson. Publish each on solve. Start at [`writeups/README.md`](writeups/README.md).
- **Internal docs** (`docs/`, kept local — not committed): the deploy runbook
  (`docs/operator/DEPLOY.md`), the admin solve guide + patch rubrics (`docs/CHALLENGES.md`),
  the player guides (`docs/player-guide/`), and the living build plan (`docs/plan/`). The full
  original spec is `BUILD.md`.

## Run it

```bash
cp .env.example .env      # fill in secrets + flags
docker compose up -d --build
# then load the challenges into CTFd:
CTF_ADMIN_TOKEN=<token> CTF_HOST=<host> python3 ctfd/seed-ctfd.py
```
Full steps + verification checklist are in the internal `docs/operator/DEPLOY.md`.

## Anti-cheat model (what actually works)

Screenshot- and copy-blocking inside a web page is theater — any player defeats it with a
phone camera or DevTools. Real deterrence comes from three layers, in order of strength:

1. **No AI reachable.** The player host has no outbound internet; challenges live on an
   internal-only Docker network. Nothing to paste a challenge into.
2. **Patch-flag review.** Worth 50% more than the exploit flag, human-reviewed. You cannot
   defend a fix you do not understand.
3. **Per-player flag values + rate-limited submission**, so a flag copied from a neighbour
   (or a public write-up) does not validate for you.

A cosmetic `user-select: none` / copy-blocker is applied to the field manual and web
challenges as a mild speed bump only — it is explicitly **not** relied on for security.

> Get written infrastructure sign-off before standing this up. See BUILD.md §0.
