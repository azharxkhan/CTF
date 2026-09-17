#!/usr/bin/env python3
"""Flag — the little desktop app players use to submit flags.

Pick a challenge, paste the flag, hit Submit. It talks to CTFd's API behind the scenes, so
CTFd stays the single scoreboard (scoring, rate-limiting, and anti-cheat all live there).
No browser needed. Uses only the Python standard library + tkinter.

Deploy notes (see README.md):
  * CTFd base URL comes from $CTF_URL (default http://ctfd.internal:8000).
  * Each player's CTFd API token is read from ~/.config/ctf/token (created at provisioning).
  * The challenge list is fetched from CTFd so it always matches the live board.
"""
import json
import os
import ssl
import urllib.request
import urllib.error

CTF_URL = os.environ.get("CTF_URL", "http://ctfd.internal:8000").rstrip("/")
TOKEN_PATH = os.path.expanduser("~/.config/ctf/token")


def _token() -> str:
    try:
        with open(TOKEN_PATH, "r", encoding="utf-8") as fh:
            return fh.read().strip()
    except OSError:
        return ""


def _api(method: str, path: str, body: dict | None = None) -> dict:
    url = f"{CTF_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("Authorization", f"Token {_token()}")
    ctx = ssl.create_default_context()
    with urllib.request.urlopen(req, context=ctx, timeout=10) as resp:
        return json.loads(resp.read().decode())


def list_challenges() -> list[dict]:
    try:
        return _api("GET", "/api/v1/challenges").get("data", [])
    except Exception:
        return []


def submit(challenge_id: int, flag: str) -> tuple[str, str]:
    """Return (status, message). status in {correct, incorrect, already_solved, error, ...}."""
    try:
        r = _api("POST", "/api/v1/challenges/attempt",
                 {"challenge_id": challenge_id, "submission": flag})
        d = r.get("data", {})
        return d.get("status", "error"), d.get("message", "")
    except urllib.error.HTTPError as e:
        return "error", f"HTTP {e.code} — check you're logged in / token is set"
    except Exception as e:
        return "error", str(e)


# ---------------------------------------------------------------------------
# GUI
# ---------------------------------------------------------------------------
def run_gui() -> None:
    import tkinter as tk
    from tkinter import ttk

    root = tk.Tk()
    root.title("Flag")
    root.geometry("460x230")
    root.resizable(False, False)

    frm = ttk.Frame(root, padding=16)
    frm.pack(fill="both", expand=True)

    ttk.Label(frm, text="Challenge").grid(row=0, column=0, sticky="w")
    chals = list_challenges()
    names = [f'{c.get("id")} · {c.get("name","?")}' for c in chals] or ["(could not load challenges)"]
    combo = ttk.Combobox(frm, values=names, state="readonly", width=44)
    combo.grid(row=1, column=0, columnspan=2, sticky="we", pady=(0, 10))
    if chals:
        combo.current(0)

    ttk.Label(frm, text="Flag").grid(row=2, column=0, sticky="w")
    flag_var = tk.StringVar()
    entry = ttk.Entry(frm, textvariable=flag_var, width=46)
    entry.grid(row=3, column=0, columnspan=2, sticky="we", pady=(0, 10))

    result = ttk.Label(frm, text="", wraplength=420)
    result.grid(row=5, column=0, columnspan=2, sticky="w", pady=(8, 0))

    def do_submit() -> None:
        if not chals:
            result.config(text="Challenges didn't load — is CTFd reachable and your token set?", foreground="#b00")
            return
        cid = chals[combo.current()].get("id")
        flag = flag_var.get().strip()
        if not flag:
            result.config(text="Paste a flag first.", foreground="#b00")
            return
        status, msg = submit(int(cid), flag)
        colors = {"correct": "#0a0", "already_solved": "#088", "incorrect": "#b00", "ratelimited": "#b60"}
        pretty = {"correct": "✅ Correct!", "already_solved": "✔ Already solved",
                  "incorrect": "❌ Incorrect", "ratelimited": "⏳ Slow down — rate limited"}
        result.config(text=f'{pretty.get(status, status)}  {msg}'.strip(),
                      foreground=colors.get(status, "#b00"))

    ttk.Button(frm, text="Submit", command=do_submit).grid(row=4, column=0, sticky="w")
    entry.bind("<Return>", lambda _e: do_submit())
    frm.columnconfigure(0, weight=1)
    root.mainloop()


if __name__ == "__main__":
    run_gui()
