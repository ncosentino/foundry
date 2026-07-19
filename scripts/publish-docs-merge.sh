#!/bin/bash
set -euo pipefail

SITE_DIR_ARG="${1:?Usage: $0 <site-dir> <main|release> [version]}"
MODE="${2:?Usage: $0 <site-dir> <main|release> [version]}"
VERSION="${3:-}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

if [[ "$SITE_DIR_ARG" = /* ]]; then
    SITE_DIR="$SITE_DIR_ARG"
else
    SITE_DIR="$ROOT_DIR/$SITE_DIR_ARG"
fi

if [ ! -d "$SITE_DIR" ]; then
    echo "Site directory does not exist: $SITE_DIR" >&2
    exit 1
fi

if [ -z "${GITHUB_TOKEN:-}" ]; then
    echo "GITHUB_TOKEN is required to publish gh-pages." >&2
    exit 1
fi

if [ -n "${GITHUB_REPOSITORY:-}" ]; then
    REPO_SLUG="$GITHUB_REPOSITORY"
else
    REMOTE_URL="$(git -C "$ROOT_DIR" remote get-url origin)"
    REPO_SLUG="$(echo "$REMOTE_URL" | sed -E 's#.*github\.com[:/]([^/]+/[^/.]+)(\.git)?$#\1#')"
fi

PUBLIC_REPO_URL="https://github.com/${REPO_SLUG}.git"
MERGE_DIR="$ROOT_DIR/gh-pages-merge"

if [ "$MODE" = "main" ] && [ -n "${GITHUB_SHA:-}" ]; then
    CURRENT_MAIN_SHA="$(
        git ls-remote "$PUBLIC_REPO_URL" refs/heads/main |
            cut -f1
    )"
    if [ -n "$CURRENT_MAIN_SHA" ] && [ "$GITHUB_SHA" != "$CURRENT_MAIN_SHA" ]; then
        echo \
            "Skipping stale documentation artifact for $GITHUB_SHA; " \
            "current main is $CURRENT_MAIN_SHA."
        exit 0
    fi
fi

if [ "$MODE" = "release" ]; then
    if [ -z "$VERSION" ]; then
        echo "A version is required for release mode." >&2
        exit 1
    fi
fi

if [ "$MODE" = "release" ]; then
    COMMIT_MESSAGE="docs: publish API documentation v$VERSION"
else
    COMMIT_MESSAGE="docs: publish development documentation"
fi

AUTH_HEADER="$(
    printf 'x-access-token:%s' "$GITHUB_TOKEN" |
        base64 |
        tr -d '\r\n'
)"

for ATTEMPT in 1 2 3; do
    rm -rf "$MERGE_DIR"

    if git ls-remote --exit-code --heads "$PUBLIC_REPO_URL" gh-pages > /dev/null 2>&1; then
        git clone --depth 1 --branch gh-pages "$PUBLIC_REPO_URL" "$MERGE_DIR"
    else
        mkdir -p "$MERGE_DIR"
        git -C "$MERGE_DIR" init
        git -C "$MERGE_DIR" checkout --orphan gh-pages
        git -C "$MERGE_DIR" remote add origin "$PUBLIC_REPO_URL"
    fi

    MERGE_ARGS=("$SITE_DIR" "$MERGE_DIR" --mode "$MODE")
    if [ "$MODE" = "release" ]; then
        MERGE_ARGS+=(--version "$VERSION")
    fi

    python "$SCRIPT_DIR/merge-docs-site.py" "${MERGE_ARGS[@]}"

    git -C "$MERGE_DIR" config user.name "github-actions[bot]"
    git -C "$MERGE_DIR" config user.email "41898282+github-actions[bot]@users.noreply.github.com"
    git -C "$MERGE_DIR" add -A

    if git -C "$MERGE_DIR" diff --cached --quiet; then
        echo "No gh-pages changes to publish."
        exit 0
    fi

    git -C "$MERGE_DIR" commit -m "$COMMIT_MESSAGE"
    if git -C "$MERGE_DIR" \
        -c "http.extraheader=AUTHORIZATION: basic $AUTH_HEADER" \
        push "$PUBLIC_REPO_URL" gh-pages; then
        echo "Published merged documentation to gh-pages."
        exit 0
    fi

    echo "gh-pages changed concurrently; retrying merge ($ATTEMPT/3)." >&2
    sleep $((ATTEMPT * 2))
done

echo "Failed to publish gh-pages after three merge attempts." >&2
exit 1
