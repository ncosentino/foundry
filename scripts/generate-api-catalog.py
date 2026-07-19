#!/usr/bin/env python3
"""Generate the API version catalog and runtime switcher data."""

from __future__ import annotations

import argparse
import base64
import json
import os
import re
import subprocess
import urllib.error
import urllib.request
from pathlib import Path

from semver_utils import compare_semver, sort_semver_descending


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        '--current-stable',
        default='',
        help='Version becoming stable during this release.',
    )
    return parser.parse_args()


def repository_root() -> Path:
    return Path(__file__).resolve().parent.parent


def repository_slug(root: Path) -> str:
    github_repository = os.environ.get('GITHUB_REPOSITORY', '')
    if github_repository:
        return github_repository

    remote = subprocess.check_output(
        ['git', '-C', str(root), 'remote', 'get-url', 'origin'],
        text=True,
    ).strip()
    match = re.search(r'github\.com[:/]([^/]+/[^/.]+)(?:\.git)?$', remote)
    if not match:
        raise RuntimeError(f'Cannot derive GitHub repository from {remote!r}.')
    return match.group(1)


def github_json(url: str):
    headers = {'Accept': 'application/vnd.github+json'}
    token = os.environ.get('GITHUB_TOKEN')
    if token:
        headers['Authorization'] = 'Bearer ' + token

    request = urllib.request.Request(url, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.loads(response.read().decode('utf-8'))
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise RuntimeError(
            f'GitHub API request failed with HTTP {error.code}: {url}'
        ) from error
    except urllib.error.URLError as error:
        raise RuntimeError(
            f'GitHub API request failed: {url}'
        ) from error


def existing_api_state(repo_slug: str) -> tuple[set[str], str]:
    api_url = (
        f'https://api.github.com/repos/{repo_slug}/contents/api'
        '?ref=gh-pages'
    )
    directory_data = github_json(api_url)
    directories = {
        item['name']
        for item in directory_data or []
        if item.get('type') == 'dir'
    }

    versions_url = (
        f'https://api.github.com/repos/{repo_slug}/contents/api/versions.json'
        '?ref=gh-pages'
    )
    versions_data = github_json(versions_url)
    current_stable = ''
    if isinstance(versions_data, dict) and versions_data.get('content'):
        decoded = base64.b64decode(versions_data['content']).decode('utf-8')
        current_stable = json.loads(decoded).get('current_stable', '')

    return directories, current_stable


def released_versions(
    root: Path,
    existing_directories: set[str],
    current_stable: str,
) -> list[str]:
    tags = subprocess.check_output(
        [
            'git',
            '-C',
            str(root),
            'tag',
            '--list',
            'v*',
        ],
        text=True,
    ).splitlines()

    versions = []
    for tag in tags:
        version = tag.removeprefix('v')
        directory = f'v{version}'
        if (
            not existing_directories
            or directory in existing_directories
            or version == current_stable
        ):
            versions.append(version)

    versions = sort_semver_descending(versions)

    if current_stable:
        versions = [
            version
            for version in versions
            if version != current_stable
        ]
        versions.insert(0, current_stable)

    return versions


def write_catalog(
    api_directory: Path,
    current_stable: str,
    versions: list[str],
) -> None:
    lines = [
        '# API Reference',
        '',
        'Select an API view:',
        '',
    ]
    if current_stable:
        lines.append(
            '- [Stable](stable/index.md) - the latest published release.'
        )
    lines.append(
        '- [Development](dev/index.md) - the latest `main` branch.'
    )

    if versions:
        lines.extend([
            '',
            'Use the API version selector on generated reference pages to '
            'browse archived releases.',
        ])

    (api_directory / 'index.md').write_text(
        '\n'.join(lines) + '\n',
        encoding='utf-8',
    )

    entries = []
    if current_stable:
        entries.append({
            'label': f'Stable ({current_stable})',
            'path': 'stable',
        })
    entries.append({
        'label': 'Development (main)',
        'path': 'dev',
    })
    if versions:
        entries.append({'separator': True})
        entries.extend({
            'label': f'v{version}',
            'path': f'v{version}',
        } for version in versions)

    payload = {
        'current_stable': current_stable,
        'entries': entries,
    }
    (api_directory / 'versions.json').write_text(
        json.dumps(payload, indent=2) + '\n',
        encoding='utf-8',
    )


def main() -> int:
    args = parse_args()
    root = repository_root()
    api_directory = root / 'docs' / 'api'
    api_directory.mkdir(parents=True, exist_ok=True)

    slug = repository_slug(root)
    directories, remote_stable = existing_api_state(slug)
    current_stable = remote_stable
    if (
        args.current_stable
        and (
            not current_stable
            or compare_semver(args.current_stable, current_stable) >= 0
        )
    ):
        current_stable = args.current_stable
    versions = released_versions(root, directories, current_stable)
    write_catalog(api_directory, current_stable, versions)

    print(
        f'Wrote API catalog with {len(versions)} version archive(s); '
        f'current stable: {current_stable or "none"}.'
    )
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
