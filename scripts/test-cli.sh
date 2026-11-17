#!/usr/bin/env bash
set -euo pipefail

# End-to-end CLI test using the sample input and reference date from the challenge.
# Requires .NET SDK 8+ installed and available as `dotnet`.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

EXPECTED_FILE="samples/sample-expected.csv"
OUT_FILE="${TMPDIR:-/tmp}/reimbursement-cli-out.csv"

cat samples/sample.csv | dotnet run --project src/Reimbursement.Cli -- --reference-date 2024-05-01 > "$OUT_FILE"

if diff -u "$EXPECTED_FILE" "$OUT_FILE"; then
  echo "CLI end-to-end test passed."
  rm -f "$OUT_FILE"
  exit 0
else
  echo "CLI end-to-end test failed: output differs from expected." >&2
  echo "Output kept at: $OUT_FILE" >&2
  exit 1
fi
