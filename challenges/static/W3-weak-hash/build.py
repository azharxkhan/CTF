#!/usr/bin/env python3
"""CHALLENGE W3 — Weak hash. Builds hashes.txt.

Writes an unsalted MD5 of a rockyou-present password (the answer players submit) plus a
bcrypt decoy that won't crack on a laptop. Stdlib only.

    FLAG_W3_WEAKHASH=letmein python3 build.py
"""
import hashlib
import os

HERE = os.path.dirname(os.path.abspath(__file__))
PLAINTEXT = os.environ.get("FLAG_W3_WEAKHASH", "letmein")  # the answer to submit

# A real bcrypt hash (of a long random value) — deliberately uncrackable on a laptop.
BCRYPT_DECOY = "$2b$12$C6UzMDM.H6dfI/f/IKcEeO3Jn8Kx6nX1x5oQ0m3s9y8u1nJ4a6bqe"


def main() -> None:
    md5 = hashlib.md5(PLAINTEXT.encode()).hexdigest()
    out = (
        "Two password hashes were recovered from a leak. Crack what you can.\n\n"
        f"hash_1:  {md5}\n"
        f"hash_2:  {BCRYPT_DECOY}\n\n"
        "Submit the plaintext of the one you crack. Not every hash is meant to be cracked.\n"
    )
    with open(os.path.join(HERE, "hashes.txt"), "w", encoding="utf-8") as fh:
        fh.write(out)
    print(f"wrote hashes.txt (hash_1 = MD5 of the answer; hash_2 = bcrypt decoy)")
    print("solve: hashid -> john --format=raw-md5 --wordlist=rockyou.txt hashes.txt")


if __name__ == "__main__":
    main()
