#!/usr/bin/env bash
set -euo pipefail

# Creates a delivery zip excluding build artifacts, dependencies, and VCS metadata.
# Output: reimbursement-solution.zip in the repository root (max 50 MB target).

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

ZIP_NAME="reimbursement-solution.zip"
rm -f "$ZIP_NAME"

zip -r "$ZIP_NAME" . \
  -x "*/bin/*" "*/obj/*" "*/build/*" "*/.dart_tool/*" "*/node_modules/*" "*/dist/*" \
  -x "*/.git/*" ".git/*" \
  -x "*.zip" \
  -x "*.iml" "*/*.iml" "*/*/*.iml" \
  -x ".idea/*" "*/.idea/*" ".idea" "*/.idea" \
  -x "*/.vs/*" \
  -x "mobile/android/.gradle/*" "*/.gradle/*" \
  -x "mobile/android/local.properties" "*/local.properties" "local.properties" \
  -x "*/TestResults/*" "TestResults/*" \
  -x "*/Pods/*" "*/.symlinks/*" "*/.plugin_symlinks/*" \
  -x "*/ephemeral/*" "*/Flutter/ephemeral/*" \
  -x "mobile/ios/Flutter/flutter_export_environment.sh" \
  > /dev/null

SIZE=$(wc -c < "$ZIP_NAME")
FORBIDDEN_COUNT=$(unzip -l "$ZIP_NAME" | grep -E "/(bin|obj)/" | wc -l || true)
if [ "$FORBIDDEN_COUNT" -ne 0 ]; then
  echo "ERROR: zip contains forbidden bin/ or obj/ entries:" >&2
  unzip -l "$ZIP_NAME" | grep -E "/(bin|obj)/" >&2 || true
  exit 1
fi

echo "Created $ZIP_NAME ($SIZE bytes)"
if [ "$SIZE" -gt 52428800 ]; then
  echo "ERROR: zip exceeds 50 MB limit." >&2
  exit 1
fi
