#!/usr/bin/env python3
"""Reviewer CLI for patch / explain-your-solve submissions.

    python3 review.py list                 # pending submissions
    python3 review.py show <id>            # explanation + diff + the challenge's patch rubric
    python3 review.py grade <id> approve|reject [note...]

Decisions are appended to the same queue; graded items drop off `list`. On approve, give the
player that challenge's patch flag (FLAG_C<n>_PATCH from .env). Stdlib only.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
QUEUE = os.environ.get("REVIEW_QUEUE", os.path.join(HERE, "queue.jsonl"))

# Patch rubric per challenge number — what a fix diff must show (from the *.patched.cs.txt refs).
RUBRICS = {
    1: ["Parameterized query (FromSql / @param), no string concat, no FromSqlRaw($\"...\")",
        "' OR 1=1 -- and the UNION payload return nothing unexpected; q=Widget still works",
        "No blocklist hack (didn't just ban 'UNION')"],
    2: ["Ownership check: invoice belongs to the caller (OwnerId == current user)",
        "403/404 for someone else's invoice; own invoices still readable",
        "Authorization per-record, not just 'is logged in'"],
    3: ["ROTATE the leaked credential (deleting the file doesn't un-leak it)",
        "Secret moved to env / user-secrets; file .gitignore'd",
        "(bonus) history purged with filter-repo/BFG"],
    4: ["FromSql / FromSqlInterpolated / LINQ (parameterized), never FromSqlRaw($\"...\")",
        "Injection payloads return nothing unexpected; a real ref still works",
        "Understands FromSqlRaw != FromSql"],
    5: ["Binds to a DTO with only user-editable fields (not the User entity)",
        "IsAdmin can't be set from the body; 'isAdmin':true no longer escalates",
        "Normal display-name updates still work"],
    6: ["Comment content not passed raw to dangerouslySetInnerHTML (text or DOMPurify)",
        "<img onerror> payload no longer executes when viewed; normal comments show",
        "Explains React escapes by default; dangerouslySetInnerHTML is the opt-out"],
    7: ["Path.GetFullPath + confirm inside the reports root",
        "../flag.txt and absolute paths rejected; legit reports still download",
        "Not a naive single '..' strip (....// would survive)"],
    8: ["Real 256-bit random signing key from config (or RS256), not guessable",
        "A token forged with the old weak key no longer validates",
        "Understands the weakness was entropy; alg:none doesn't apply"],
    9: ["ValidateIssuer/Audience/Lifetime all true; ValidIssuer/ValidAudience pinned",
        "The partner/legacy token (wrong issuer/audience) is rejected (401)",
        "Understands the defaults were secure; fix is not disabling them"],
    10: ["Parameterized query (no concatenation)",
         "WAITFOR payload no longer changes response time; legit checks still work",
         "Understands blind injection: a timing oracle still exfiltrates"],
    11: ["Read-check-write made atomic (transaction+isolation, RowVersion, or atomic UPDATE)",
         "Concurrent requests can no longer exceed the limit; single allocation still works",
         "Did NOT reach for NOLOCK"],
    12: ["No privileged key in client assets; privileged calls proxied via backend as the user",
         "Or client key minimally scoped and can't reach admin routes; leaked key rotated",
         "Understands client-side = public; obfuscation is not a fix"],
}


def load():
    subs, decided = {}, set()
    if not os.path.exists(QUEUE):
        return subs, decided
    with open(QUEUE, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            r = json.loads(line)
            if r.get("decision"):
                decided.add(r["id"])
            elif "explanation" in r:
                subs[r["id"]] = r
    return subs, decided


def chal_num(s):
    try:
        return int(str(s).split()[0])
    except Exception:
        return None


def cmd_list():
    subs, decided = load()
    pending = [r for i, r in subs.items() if i not in decided]
    if not pending:
        print("no pending submissions.")
        return
    for r in pending:
        print(f"{r['id']}  {r['ts']}  [{r['kind']:7}]  {r['player']:12}  {r['challenge']}")


def cmd_show(sid):
    subs, _ = load()
    r = subs.get(sid)
    if not r:
        sys.exit(f"no submission {sid}")
    print(f"=== {r['id']}  {r['ts']}  {r['kind']}  player={r['player']} ===")
    print(f"challenge: {r['challenge']}\n")
    print("--- explanation ---\n" + r["explanation"] + "\n")
    if r["diff"]:
        print("--- diff ---\n" + r["diff"] + "\n")
    n = chal_num(r["challenge"])
    if n in RUBRICS:
        print("--- patch rubric (grade against this) ---")
        for item in RUBRICS[n]:
            print(f"  [ ] {item}")
        print(f"\nOn approve, give the player FLAG_C{n}_PATCH from .env.")


def cmd_grade(sid, decision, note):
    if decision not in ("approve", "reject"):
        sys.exit("decision must be approve or reject")
    subs, _ = load()
    if sid not in subs:
        sys.exit(f"no submission {sid}")
    with open(QUEUE, "a", encoding="utf-8") as fh:
        fh.write(json.dumps({"id": sid, "decision": decision, "note": note}) + "\n")
    print(f"{sid} -> {decision}" + (f" ({note})" if note else ""))
    if decision == "approve":
        n = chal_num(subs[sid]["challenge"])
        print(f"Give the player FLAG_C{n}_PATCH." if n else "Give the player the patch flag.")


def main():
    a = sys.argv[1:]
    if not a or a[0] == "list":
        cmd_list()
    elif a[0] == "show" and len(a) == 2:
        cmd_show(a[1])
    elif a[0] == "grade" and len(a) >= 3:
        cmd_grade(a[1], a[2], " ".join(a[3:]))
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
