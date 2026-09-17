#!/usr/bin/env python3
"""Build the pristine SQLite playground seed database from schema.sql + seed.sql.

Produces `playground.seed.db` (the read-only pristine copy). Restore copies this over each
player's working `playground.db`. Uses only the Python standard library.

Usage:
    python3 build_sqlite.py                # writes ./playground.seed.db
    python3 build_sqlite.py --out /path/playground.seed.db
    python3 build_sqlite.py --verify       # build, then run a couple of sanity queries
"""
import argparse
import os
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def build(out_path: str) -> None:
    for f in ("schema.sql", "seed.sql"):
        if not os.path.exists(os.path.join(HERE, f)):
            sys.exit(f"missing {f} next to this script")
    if os.path.exists(out_path):
        os.remove(out_path)
    con = sqlite3.connect(out_path)
    try:
        for f in ("schema.sql", "seed.sql"):
            with open(os.path.join(HERE, f), "r", encoding="utf-8") as fh:
                con.executescript(fh.read())
        con.commit()
    finally:
        con.close()
    print(f"built {out_path}")


def verify(out_path: str) -> None:
    con = sqlite3.connect(out_path)
    try:
        cur = con.cursor()
        for tbl in ("Users", "Products", "Invoices", "Orders", "Comments", "Flags"):
            n = cur.execute(f"SELECT COUNT(*) FROM {tbl}").fetchone()[0]
            print(f"  {tbl}: {n} rows")
        # The core SQLi shape returns everything:
        all_products = cur.execute(
            "SELECT COUNT(*) FROM Products WHERE Name = '' OR 1=1"
        ).fetchone()[0]
        print(f"  ' OR 1=1 returns {all_products} products (expected: all of them)")
        # UNION extraction reaches the Flags table:
        row = cur.execute(
            "SELECT Id, Name FROM Products WHERE Name='nope' "
            "UNION SELECT Id, Secret FROM Flags"
        ).fetchall()
        print(f"  UNION SELECT surfaces {len(row)} practice-flag rows")
    finally:
        con.close()


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(HERE, "playground.seed.db"))
    ap.add_argument("--verify", action="store_true")
    args = ap.parse_args()
    build(args.out)
    if args.verify:
        verify(args.out)


if __name__ == "__main__":
    main()
