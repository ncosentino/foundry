#!/bin/bash
# Generate API documentation from XML doc comments
# Usage: ./scripts/generate-api-docs.sh <output-dir> [--update-index]
#
# Examples:
#   ./scripts/generate-api-docs.sh docs/api/dev --update-index
#   ./scripts/generate-api-docs.sh docs/api/v0.0.2

set -e

OUTPUT_DIR_ARG="${1:?Usage: $0 <output-dir> [--update-index]}"
UPDATE_INDEX="${2:-}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

# Convert output dir to absolute path
if [[ "$OUTPUT_DIR_ARG" = /* ]]; then
    OUTPUT_DIR="$OUTPUT_DIR_ARG"
else
    OUTPUT_DIR="$ROOT_DIR/$OUTPUT_DIR_ARG"
fi

echo "Generating API documentation to $OUTPUT_DIR..."

# Ensure output directory exists
mkdir -p "$OUTPUT_DIR"

# Find all XML documentation files for publishable projects
# Look directly in the project's own bin folder (not in Test project folders that reference them)
# Exclude Tests, Benchmarks, Examples, and IntegrationTests.
declare -A SEEN_PACKAGES
XML_FILES_UNIQUE=""

while IFS= read -r xml_file; do
    pkg_name=$(basename "$xml_file" .xml)
    project_folder=$(dirname "$xml_file" | sed 's|/bin/Release/.*||' | xargs basename)

    # Only include if the XML is in a folder matching its package name
    # This excludes DLLs that are copied to other projects as references
    if [ "$pkg_name" = "$project_folder" ] && [ -z "${SEEN_PACKAGES[$pkg_name]:-}" ]; then
        SEEN_PACKAGES[$pkg_name]=1
        XML_FILES_UNIQUE="$XML_FILES_UNIQUE $xml_file"
    fi
done < <(find "$ROOT_DIR/src" -path "*/bin/Release/*/NexusLabs.Foundry*.xml" \
    ! -path "*/Examples/*" \
    ! -name "*Tests.xml" \
    ! -name "*Benchmarks.xml" \
    ! -name "*IntegrationTests.xml" \
    2>/dev/null | sort)

XML_FILES="$XML_FILES_UNIQUE"

if [ -z "$XML_FILES" ]; then
    echo "No XML documentation files found. Ensure the project was built with Release configuration."
    exit 1
fi

echo "Found XML documentation files:"
for f in $XML_FILES; do echo "  - $(basename "$f")"; done

# Collect all package names for link fixing later
ALL_PACKAGES=""

# Keep track of generated packages
GENERATED_PACKAGES=""
FAILED_PACKAGES=""

# Generate documentation for each XML file
for XML_FILE in $XML_FILES; do
    PROJECT_NAME=$(basename "$XML_FILE" .xml)
    ALL_PACKAGES="$ALL_PACKAGES $PROJECT_NAME"

    # Get the corresponding DLL
    DLL_FILE="${XML_FILE%.xml}.dll"

    if [ ! -f "$DLL_FILE" ]; then
        echo "  Error: DLL not found for $PROJECT_NAME"
        FAILED_PACKAGES="$FAILED_PACKAGES $PROJECT_NAME"
        continue
    fi

    # Create project-specific output directory
    PROJECT_OUTPUT="$OUTPUT_DIR/$PROJECT_NAME"
    mkdir -p "$PROJECT_OUTPUT"

    echo "Generating docs for $PROJECT_NAME..."

    # Run DefaultDocumentation
    if dotnet defaultdocumentation \
        --AssemblyFilePath "$DLL_FILE" \
        --DocumentationFilePath "$XML_FILE" \
        --OutputDirectoryPath "$PROJECT_OUTPUT" \
        --ConfigurationFilePath "$ROOT_DIR/defaultdocumentation.json" 2>&1; then

        MD_COUNT=$(find "$PROJECT_OUTPUT" -name "*.md" -type f 2>/dev/null | wc -l)

        # If no index.md was generated but we have markdown files, create one
        # DefaultDocumentation doesn't always create index.md for single-namespace packages
        if [ ! -f "$PROJECT_OUTPUT/index.md" ] && [ "$MD_COUNT" -gt 0 ]; then
            # Look for a namespace file matching the project name
            NAMESPACE_FILE="$PROJECT_OUTPUT/$PROJECT_NAME.md"
            if [ -f "$NAMESPACE_FILE" ]; then
                # Copy namespace file to index.md
                cp "$NAMESPACE_FILE" "$PROJECT_OUTPUT/index.md"
                echo "  Created index.md from namespace file"
            else
                # Create a simple index.md listing all files
                echo "# $PROJECT_NAME" > "$PROJECT_OUTPUT/index.md"
                echo "" >> "$PROJECT_OUTPUT/index.md"
                for md in "$PROJECT_OUTPUT"/*.md; do
                    if [ "$(basename "$md")" != "index.md" ]; then
                        name=$(basename "$md" .md)
                        echo "- [$name]($name.md)" >> "$PROJECT_OUTPUT/index.md"
                    fi
                done
                echo "  Created index.md with file listing"
            fi
        fi

        # Now check if we have a valid index.md
        if [ -f "$PROJECT_OUTPUT/index.md" ]; then
            GENERATED_PACKAGES="$GENERATED_PACKAGES $PROJECT_NAME"

            # Replace escaped angle brackets in headings. The original grep/sed
            # loop treated \< and \> as regex word-boundary operators on some
            # platforms and could loop forever without changing the file.
            python - "$PROJECT_OUTPUT" <<'PY'
from pathlib import Path
import sys

for markdown_file in Path(sys.argv[1]).rglob('*.md'):
    lines = markdown_file.read_text(encoding='utf-8').splitlines(keepends=True)
    updated = [
        line.replace(r'\<', '&lt;').replace(r'\>', '&gt;')
        if line.startswith('##')
        else line
        for line in lines
    ]
    markdown_file.write_text(''.join(updated), encoding='utf-8')
PY
        else
            echo "  Note: $PROJECT_NAME has no public types, skipping..."
            rm -rf "$PROJECT_OUTPUT"
        fi
    else
        echo "  Error: Failed to generate docs for $PROJECT_NAME"
        FAILED_PACKAGES="$FAILED_PACKAGES $PROJECT_NAME"
    fi
done

# Post-process all packages to fix internal links
# Convert Microsoft Learn URLs for Foundry types to relative links
# IMPORTANT: Process packages from longest to shortest to avoid partial matches
echo ""
echo "Post-processing: Fixing internal links..."

python - "$OUTPUT_DIR" "$ALL_PACKAGES" "$GENERATED_PACKAGES" <<'PY'
from pathlib import Path
import re
import sys

output_dir = Path(sys.argv[1])
all_packages = sorted(sys.argv[2].split(), key=len, reverse=True)
generated_packages = sys.argv[3].split()

for project_name in generated_packages:
    for markdown_file in (output_dir / project_name).rglob('*.md'):
        content = markdown_file.read_text(encoding='utf-8')
        for package_name in all_packages:
            pattern = re.compile(
                r"\(https://learn\.microsoft\.com/[^ ]*dotnet/api/"
                + re.escape(package_name.lower())
                + r"[^ ]* '[^']*'\)",
                re.IGNORECASE,
            )
            content = pattern.sub(
                f'(../{package_name}/index.md)',
                content,
            )
        markdown_file.write_text(content, encoding='utf-8')
PY

# Update index if requested
if [ "$UPDATE_INDEX" = "--update-index" ] && [ -f "$OUTPUT_DIR/index.md" ]; then
    echo "Updating index page..."

    # Remove old package links and placeholder text
    sed -i '/^- \[NexusLabs/d' "$OUTPUT_DIR/index.md"
    sed -i '/^\* \[NexusLabs/d' "$OUTPUT_DIR/index.md"
    sed -i '/No API documentation generated/d' "$OUTPUT_DIR/index.md"
    sed -i '/API documentation will be generated/d' "$OUTPUT_DIR/index.md"

    # Sort and add links to each generated package (only if it has index.md)
    for PROJECT_NAME in $(echo $GENERATED_PACKAGES | tr ' ' '\n' | sort); do
        if [ -f "$OUTPUT_DIR/$PROJECT_NAME/index.md" ]; then
            echo "* [$PROJECT_NAME]($PROJECT_NAME/index.md)" >> "$OUTPUT_DIR/index.md"
        fi
    done
fi

if [ -n "$FAILED_PACKAGES" ]; then
    echo "API documentation failed for:$FAILED_PACKAGES" >&2
    exit 1
fi

if [ -z "$GENERATED_PACKAGES" ]; then
    echo "No package API documentation was generated." >&2
    exit 1
fi

echo ""
echo "API documentation generated successfully at $OUTPUT_DIR"
echo "Generated packages: $(echo $GENERATED_PACKAGES | wc -w)"
