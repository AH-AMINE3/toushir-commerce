"""Consistent online SQLite snapshot. No application secrets are printed."""
import argparse
import pathlib
import sqlite3

parser = argparse.ArgumentParser()
parser.add_argument("--database", required=True)
parser.add_argument("--destination", required=True)
args = parser.parse_args()
source = pathlib.Path(args.database).resolve(strict=True)
destination = pathlib.Path(args.destination).resolve()
if destination.exists():
    parser.error("Destination already exists; refusing to overwrite a backup.")
if source == destination:
    parser.error("Source and destination must differ.")
destination.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
try:
    with sqlite3.connect(source.as_uri() + "?mode=ro", uri=True) as original:
        with sqlite3.connect(destination) as snapshot:
            original.backup(snapshot)
            result = snapshot.execute("PRAGMA integrity_check").fetchone()[0]
            if result != "ok":
                raise RuntimeError("SQLite snapshot integrity check failed.")
    destination.chmod(0o600)
except Exception:
    destination.unlink(missing_ok=True)
    raise
print("Backup created and integrity verified:", destination)
