#!/usr/bin/env python3
"""CHALLENGE W2 — Encoding chain. Builds challenge.txt.

Takes the flag and encodes it flag -> ROT13 -> hex -> base64 (decode order: base64, hex,
ROT13). Ships a decoy base64 string too. Stdlib only.

    FLAG_W2_ENCODING=flag{...} python3 build.py
"""
import base64
import codecs
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FLAG = os.environ.get("FLAG_W2_ENCODING", "flag{missing_env_w2}")


def encode_chain(text: str) -> str:
    rot = codecs.encode(text, "rot_13")           # 1. ROT13
    hexed = rot.encode().hex()                     # 2. hex
    b64 = base64.b64encode(hexed.encode()).decode()  # 3. base64
    return b64


def main() -> None:
    real = encode_chain(FLAG)
    decoy = base64.b64encode(b"flag{not_this_layer}").decode()  # single-layer decoy
    out = (
        "Acme intercepted transmission — decode to recover the message.\n\n"
        f"payload:  {real}\n"
        f"decoy:    {decoy}\n\n"
        "Hint: it's been through more than one layer. None of them are encryption.\n"
    )
    with open(os.path.join(HERE, "challenge.txt"), "w", encoding="utf-8") as fh:
        fh.write(out)
    print("wrote challenge.txt")
    print("solve: base64 -d | xxd -r -p | (ROT13)")


if __name__ == "__main__":
    main()
