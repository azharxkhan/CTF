#!/usr/bin/env python3
"""Patch / explain-your-solve review service.

A tiny stdlib HTTP service where players submit, for a challenge, a short explanation of how
they solved it AND (for the patch flag) their fix as a diff. Submissions land in a JSON-lines
queue the reviewer reads with review.py. No CTFd plugin, no external deps — runs on the
internal network.

    REVIEW_QUEUE=/data/queue.jsonl PORT=8080 python3 server.py
"""
import json
import os
import time
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
QUEUE = os.environ.get("REVIEW_QUEUE", os.path.join(HERE, "queue.jsonl"))
PORT = int(os.environ.get("PORT", "8080"))

CHALLENGES = [
    "1 Product search (SQLi)", "2 Invoices (IDOR)", "3 Billing service (git secret)",
    "4 Order lookup (FromSqlRaw)", "5 Profile (mass assignment)", "6 Community board (XSS)",
    "7 Report download (path traversal)", "8 Token service (weak JWT)",
    "9 Token service (JWT validation)", "10 Status check (blind SQLi)",
    "11 Allocation (race)", "12 Admin dashboard (bundle key)",
]


def append(record: dict) -> None:
    os.makedirs(os.path.dirname(QUEUE) or ".", exist_ok=True)
    with open(QUEUE, "a", encoding="utf-8") as fh:
        fh.write(json.dumps(record) + "\n")


class Handler(BaseHTTPRequestHandler):
    def _send(self, code, body, ctype="application/json"):
        data = body if isinstance(body, bytes) else body.encode()
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        if self.path in ("/", "/index.html", "/submit.html"):
            with open(os.path.join(HERE, "submit.html"), "rb") as fh:
                return self._send(200, fh.read(), "text/html; charset=utf-8")
        if self.path == "/api/challenges":
            return self._send(200, json.dumps(CHALLENGES))
        return self._send(404, json.dumps({"error": "not found"}))

    def do_POST(self):
        if self.path != "/api/submit":
            return self._send(404, json.dumps({"error": "not found"}))
        try:
            n = int(self.headers.get("Content-Length", "0"))
            body = json.loads(self.rfile.read(n).decode() or "{}")
        except Exception:
            return self._send(400, json.dumps({"error": "bad json"}))
        challenge = (body.get("challenge") or "").strip()
        player = (body.get("player") or "").strip()
        explanation = (body.get("explanation") or "").strip()
        diff = (body.get("diff") or "").strip()
        kind = "patch" if diff else "explain"
        if not challenge or not player or not explanation:
            return self._send(400, json.dumps({"error": "challenge, player and explanation are required"}))
        rec = {
            "id": uuid.uuid4().hex[:8], "ts": time.strftime("%Y-%m-%d %H:%M:%S"),
            "challenge": challenge, "player": player, "kind": kind,
            "explanation": explanation, "diff": diff, "status": "pending",
        }
        append(rec)
        return self._send(200, json.dumps({"ok": True, "id": rec["id"], "kind": kind}))

    def log_message(self, *a):  # quiet
        pass


if __name__ == "__main__":
    print(f"review service on :{PORT}  queue={QUEUE}")
    ThreadingHTTPServer(("0.0.0.0", PORT), Handler).serve_forever()
