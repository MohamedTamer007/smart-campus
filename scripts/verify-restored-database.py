"""Compare all application rows and constraints in two isolated local test databases."""
import argparse
import hashlib
import json
import os
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True)
    parser.add_argument("--restored", required=True)
    args = parser.parse_args()
    host = os.environ.get("PGHOST", "")
    if host not in ("127.0.0.1", "localhost") or not args.source.endswith("_test") or \
       not args.restored.endswith("_restore_test") or args.source == args.restored:
        raise ValueError("Only independent loopback test databases may be compared")
    environment = {key: value for key, value in os.environ.items() if key not in ("PGSERVICE", "PGHOSTADDR")}

    def query(database, sql):
        result = subprocess.run(["psql", "--host", host, "--dbname", database, "--tuples-only", "--no-align",
                                 "--set", "ON_ERROR_STOP=1", "--command", sql], env=environment,
                                capture_output=True, check=True, timeout=60)
        return result.stdout

    table_query = "SELECT tablename FROM pg_tables WHERE schemaname='smart_campus' ORDER BY tablename;"
    tables = query(args.source, table_query)
    if not tables.strip() or tables != query(args.restored, table_query):
        raise ValueError("Application table inventory differs or is empty")
    total = 0
    for table in tables.decode().splitlines():
        identifier = '"' + table.replace('"', '""') + '"'
        sql = "SELECT to_jsonb(t)::text FROM smart_campus." + identifier + " t ORDER BY to_jsonb(t)::text;"
        original = query(args.source, sql)
        recovered = query(args.restored, sql)
        if hashlib.sha256(original).digest() != hashlib.sha256(recovered).digest():
            raise ValueError("Restored application data differs")
        total += len(original.splitlines())
    for sql in (
        "SELECT conrelid::regclass::text,conname,contype,pg_get_constraintdef(oid) FROM pg_constraint WHERE connamespace='smart_campus'::regnamespace ORDER BY conrelid::regclass::text,conname,contype;",
        "SELECT tablename,indexname,indexdef FROM pg_indexes WHERE schemaname='smart_campus' ORDER BY tablename,indexname;",
    ):
        if query(args.source, sql) != query(args.restored, sql):
            raise ValueError("Restored constraints or indexes differ")
    if total == 0:
        raise ValueError("Empty fixtures do not establish recovery of application data")
    print(json.dumps({"result": "restored_application_matches", "tables": len(tables.splitlines()), "rows": total,
                      "verified": ["all application rows", "migration history", "constraints", "indexes"]}))


if __name__ == "__main__":
    try:
        main()
    except ValueError as error:
        # These messages are fixed validation reasons, never database rows or connection strings.
        print(str(error), file=sys.stderr)
        sys.exit(1)
    except (OSError, subprocess.SubprocessError):
        print("Isolated restoration comparison failed; inspect the controlled test databases privately.", file=sys.stderr)
        sys.exit(1)
