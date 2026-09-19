#!/usr/bin/env python3
"""CHALLENGE W4 — Hidden in a file. Builds the artifacts players get.

Produces:
  * telemetry.bin  — a binary blob with the flag embedded as a string (find with `strings`).
  * photo.dat      — a real PNG renamed to .dat (find its true type with `file`).

Stdlib only. The generated binaries are git-ignored; ship them to players.

    FLAG_W4_HIDDENFILE=flag{...} python3 build.py
"""
import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
FLAG = os.environ.get("FLAG_W4_HIDDENFILE", "flag{missing_env_w4}")


def make_blob() -> bytes:
    # Deterministic noise with the flag buried in the middle, as an embedded string.
    noise = bytes((i * 37 + 11) % 256 for i in range(2048))
    marker = f"\x00\x00comment={FLAG}\x00\x00".encode()
    return noise[:900] + marker + noise[900:]


def make_png() -> bytes:
    # Minimal valid 1x1 PNG so `file` correctly reports it as a PNG despite the .dat name.
    def chunk(typ: bytes, data: bytes) -> bytes:
        return (struct.pack(">I", len(data)) + typ + data
                + struct.pack(">I", zlib.crc32(typ + data) & 0xffffffff))
    sig = b"\x89PNG\r\n\x1a\n"
    ihdr = chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0))
    raw = b"\x00\xff\x00\x00"                      # one red pixel
    idat = chunk(b"IDAT", zlib.compress(raw))
    iend = chunk(b"IEND", b"")
    return sig + ihdr + idat + iend


def main() -> None:
    with open(os.path.join(HERE, "telemetry.bin"), "wb") as fh:
        fh.write(make_blob())
    with open(os.path.join(HERE, "photo.dat"), "wb") as fh:   # PNG in disguise
        fh.write(make_png())
    print("wrote telemetry.bin (flag as embedded string) and photo.dat (PNG renamed .dat)")
    print("solve: file * ; strings telemetry.bin | grep flag ; (exiftool works too)")


if __name__ == "__main__":
    main()
