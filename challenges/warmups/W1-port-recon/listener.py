#!/usr/bin/env python3
"""CHALLENGE W1 — Port recon.

Opens a few TCP ports, each printing a banner when you connect. One non-obvious high port
carries the flag; the others are decoys. Players scan (`nmap -sV`) and read banners (`nc`).

    FLAG_W1_PORTRECON=flag{...} python3 listener.py

Runs until stopped. Stdlib only.
"""
import os
import socket
import threading

FLAG = os.environ.get("FLAG_W1_PORTRECON", "flag{missing_env_w1}")

# (port, banner). The flag hides on a high, unremarkable port among decoys.
SERVICES = [
    (47001, "220 acme-smtp ESMTP ready\r\n"),                 # decoy: looks like a mail server
    (47002, "nothing here\r\n"),                              # decoy: one -sV disproves it
    (48219, f"Acme Telemetry Agent v1.4\r\nstatus: ok\r\ntoken: {FLAG}\r\n"),  # the flag
    (49500, "SSH-2.0-OpenSSH_8.9\r\n"),                       # decoy: fake ssh banner
]


def serve(port: str, banner: str) -> None:
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(("0.0.0.0", port))
    s.listen(8)
    while True:
        try:
            conn, _ = s.accept()
            with conn:
                conn.sendall(banner.encode())
        except OSError:
            break


def main() -> None:
    threads = []
    for port, banner in SERVICES:
        t = threading.Thread(target=serve, args=(port, banner), daemon=True)
        t.start()
        threads.append(t)
    print(f"W1 listening on: {', '.join(str(p) for p, _ in SERVICES)}")
    print("Flag is on one of them. Scan with: nmap -sV -p 47000-49999 <host>")
    for t in threads:
        t.join()


if __name__ == "__main__":
    main()
