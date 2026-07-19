#!/usr/bin/env python3
"""Merge one workflow-owned documentation slice into a gh-pages checkout."""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

from semver_utils import compare_semver, sort_semver_descending


SITE_URL = 'https://www.devleader.ca/projects/foundry'


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('target', type=Path)
    parser.add_argument('--mode', choices=('main', 'release'), required=True)
    parser.add_argument('--version', default='')
    return parser.parse_args()


def remove_path(path: Path) -> None:
    if path.is_dir():
        shutil.rmtree(path)
    elif path.exists():
        path.unlink()


def copy_path(source: Path, target: Path) -> None:
    if source.is_dir():
        shutil.copytree(source, target, dirs_exist_ok=True)
    elif source.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)


def load_search_index(site: Path) -> dict:
    search_index = site / 'search' / 'search_index.json'
    if not search_index.is_file():
        return {'config': {}, 'docs': []}
    return json.loads(search_index.read_text(encoding='utf-8'))


def write_search_index(site: Path, payload: dict) -> None:
    search_index = site / 'search' / 'search_index.json'
    search_index.parent.mkdir(parents=True, exist_ok=True)
    search_index.write_text(
        json.dumps(payload, separators=(',', ':')),
        encoding='utf-8',
    )


def merge_search_docs(
    source: Path,
    target: Path,
    mode: str,
    version: str = '',
    update_stable: bool = True,
    existing_target_index: dict | None = None,
) -> None:
    source_index = load_search_index(source)
    target_index = existing_target_index or load_search_index(target)

    if mode == 'main':
        source_docs = [
            doc
            for doc in source_index.get('docs', [])
            if not (
                doc.get('location', '').startswith('api/stable/')
                or doc.get('location', '').startswith('api/v')
            )
        ]
        preserved_docs = [
            doc
            for doc in target_index.get('docs', [])
            if doc.get('location', '').startswith('api/stable/')
        ]
    else:
        source_docs = [
            doc
            for doc in source_index.get('docs', [])
            if (
                update_stable
                and doc.get('location', '').startswith('api/stable/')
            )
        ]
        preserved_docs = [
            doc
            for doc in target_index.get('docs', [])
            if (
                not doc.get('location', '').startswith('api/v')
                and not (
                    update_stable
                    and doc.get('location', '').startswith('api/stable/')
                )
            )
        ]

    write_search_index(
        target,
        {
            'config': source_index.get('config') or target_index.get('config', {}),
            'docs': source_docs + preserved_docs,
        },
    )


def current_stable(site: Path) -> str:
    versions_file = site / 'api' / 'versions.json'
    if not versions_file.is_file():
        return ''

    return json.loads(
        versions_file.read_text(encoding='utf-8')
    ).get('current_stable', '')


def write_version_metadata(site: Path, stable_version: str) -> None:
    api_directory = site / 'api'
    versions = sort_semver_descending([
        child.name[1:]
        for child in api_directory.iterdir()
        if child.is_dir()
        and child.name.startswith('v')
        and len(child.name) > 1
        and child.name[1].isdigit()
    ])
    if stable_version in versions:
        versions = [
            version
            for version in versions
            if version != stable_version
        ]
        versions.insert(0, stable_version)

    entries = []
    if stable_version and (api_directory / 'stable').is_dir():
        entries.append({
            'label': f'Stable ({stable_version})',
            'path': 'stable',
        })
    if (api_directory / 'dev').is_dir():
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

    (api_directory / 'versions.json').write_text(
        json.dumps(
            {
                'current_stable': stable_version,
                'entries': entries,
            },
            indent=2,
        ) + '\n',
        encoding='utf-8',
    )


def write_sitemap_index(site: Path) -> None:
    sub_sitemaps = [
        relative_path
        for relative_path in (
            'api/dev/sitemap.xml',
            'api/stable/sitemap.xml',
            'sitemap-main.xml',
        )
        if (site / relative_path).is_file()
    ]

    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        '<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
    ]
    for sub_sitemap in sub_sitemaps:
        lines.extend([
            '  <sitemap>',
            f'    <loc>{SITE_URL}/{sub_sitemap}</loc>',
            '  </sitemap>',
        ])
    lines.append('</sitemapindex>')
    (site / 'sitemap.xml').write_text(
        '\n'.join(lines) + '\n',
        encoding='utf-8',
    )


def merge_main(source: Path, target: Path) -> None:
    target_search_index = load_search_index(target)
    source_stable = current_stable(source)
    target_stable = current_stable(target)
    stable_version = source_stable
    if (
        target_stable
        and (
            not stable_version
            or compare_semver(target_stable, stable_version) > 0
        )
    ):
        stable_version = target_stable

    api_target = target / 'api'
    preserved = {
        path.resolve()
        for path in [api_target / 'stable', *api_target.glob('v*')]
        if path.exists()
    }

    for child in target.iterdir():
        if child.name == '.git':
            continue
        if child.name not in ('api', 'assets'):
            remove_path(child)

    api_target.mkdir(parents=True, exist_ok=True)
    for child in api_target.iterdir():
        if child.resolve() not in preserved:
            remove_path(child)

    copy_path(source, target)
    merge_search_docs(
        source,
        target,
        'main',
        existing_target_index=target_search_index,
    )
    write_version_metadata(target, stable_version)
    write_sitemap_index(target)

    if target_stable and target_stable != source_stable:
        print(f'Preserved newer stable metadata {target_stable}.')


def merge_release(
    source: Path,
    target: Path,
    version: str,
) -> None:
    if not version:
        raise ValueError('--version is required for release mode.')

    if not (target / 'index.html').is_file():
        copy_path(source, target)
        search_index = load_search_index(source)
        search_index['docs'] = [
            doc
            for doc in search_index.get('docs', [])
            if not (
                doc.get('location', '').startswith('api/dev/')
                or doc.get('location', '').startswith('api/v')
            )
        ]
        write_search_index(target, search_index)
        write_version_metadata(target, current_stable(source))
        write_sitemap_index(target)
        print('Bootstrapped an empty gh-pages site from the release build.')
        return

    api_source = source / 'api'
    api_target = target / 'api'
    api_target.mkdir(parents=True, exist_ok=True)
    copy_path(source / 'assets', target / 'assets')

    target_stable = current_stable(target)
    update_stable = (
        not target_stable
        or compare_semver(version, target_stable) >= 0
    )

    release_paths = [Path(f'v{version}')]
    if update_stable:
        release_paths.extend((
            Path('stable'),
            Path('index.html'),
            Path('versions.json'),
        ))

    for relative_path in release_paths:
        remove_path(api_target / relative_path)
        copy_path(api_source / relative_path, api_target / relative_path)

    merge_search_docs(
        source,
        target,
        'release',
        version,
        update_stable=update_stable,
    )

    stable_version = version if update_stable else target_stable
    write_version_metadata(target, stable_version)
    write_sitemap_index(target)

    if not update_stable:
        print(
            f'Preserved newer stable documentation {target_stable}; '
            f'published archive v{version} only.'
        )


def main() -> int:
    args = parse_args()
    if not args.source.is_dir():
        raise FileNotFoundError(args.source)
    args.target.mkdir(parents=True, exist_ok=True)

    if args.mode == 'main':
        merge_main(args.source, args.target)
    else:
        merge_release(args.source, args.target, args.version)

    print(f'Merged {args.mode} documentation into {args.target}.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
