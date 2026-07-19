#!/bin/bash
# Generate versioned API documentation for releases
# Usage: ./scripts/generate-versioned-api-docs.sh <version>
#
# This script:
# 1. Generates API docs to docs/api/v{version}/
# 2. Copies to docs/api/stable/
# 3. Updates docs/api/index.md with version links

set -e

VERSION="${1:?Usage: $0 <version>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Generating API documentation for version $VERSION..."

# Generate docs to versioned directory
"$SCRIPT_DIR/generate-api-docs.sh" "$ROOT_DIR/docs/api/v$VERSION"

# Create version-specific index
cat > "$ROOT_DIR/docs/api/v$VERSION/index.md" << EOF
# API Reference v$VERSION

This documentation is for version **$VERSION**.

## Packages

EOF

python "$SCRIPT_DIR/api_package_index.py" \
    "$ROOT_DIR/docs/api/v$VERSION/index.md" \
    --packages-root "$ROOT_DIR/docs/api/v$VERSION" \
    --package-prefix "NexusLabs.Foundry"

# Copy to stable (latest release)
echo "Copying to stable..."
mkdir -p "$ROOT_DIR/docs/api/stable"
cp -r "$ROOT_DIR/docs/api/v$VERSION/"* "$ROOT_DIR/docs/api/stable/"

# Update stable index
cat > "$ROOT_DIR/docs/api/stable/index.md" << EOF
# API Reference (Stable)

This documentation is for the latest stable release: **v$VERSION**.

For development (unreleased) documentation, see [dev](../dev/index.md).

## Packages

EOF

python "$SCRIPT_DIR/api_package_index.py" \
    "$ROOT_DIR/docs/api/stable/index.md" \
    --packages-root "$ROOT_DIR/docs/api/stable" \
    --package-prefix "NexusLabs.Foundry"

# Update the shared API catalog and version-switcher data. The catalog helper
# preserves released versions already present on gh-pages and always includes
# the version being published by this release.
python "$SCRIPT_DIR/generate-api-catalog.py" --current-stable "$VERSION"
echo "Versioned API documentation generated successfully"
