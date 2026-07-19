#!/usr/bin/env python3
"""Write a normalized package list into an API documentation index."""

from __future__ import annotations

import argparse
from pathlib import Path
from typing import Sequence


PACKAGES_HEADING = '## Packages'


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument('index_path', type=Path)
    parser.add_argument('--packages-root', required=True, type=Path)
    parser.add_argument('--package-prefix', required=True)
    parser.add_argument('--package', action='append', default=[])
    return parser.parse_args()


def _resolve_package_names(
    packages_root: Path,
    package_prefix: str,
    package_names: Sequence[str],
) -> list[str]:
    if package_names:
        invalid_names = sorted({
            name
            for name in package_names
            if not name.startswith(package_prefix)
            or not (packages_root / name / 'index.md').is_file()
        })
        if invalid_names:
            raise ValueError(
                'Package indexes were not found for: '
                + ', '.join(invalid_names)
            )
        return sorted(set(package_names))

    discovered_names = sorted(
        child.name
        for child in packages_root.iterdir()
        if child.is_dir()
        and child.name.startswith(package_prefix)
        and (child / 'index.md').is_file()
    )
    if not discovered_names:
        raise ValueError(
            f'No package indexes matching {package_prefix!r} were found '
            f'in {packages_root}.'
        )
    return discovered_names


def update_package_index(
    index_path: Path,
    packages_root: Path,
    package_prefix: str,
    package_names: Sequence[str] = (),
) -> None:
    """Replace the package section with a sorted Markdown list."""
    lines = index_path.read_text(encoding='utf-8').splitlines()
    try:
        heading_index = next(
            index
            for index, line in enumerate(lines)
            if line.strip() == PACKAGES_HEADING
        )
    except StopIteration as error:
        raise ValueError(
            f'{index_path} does not contain a {PACKAGES_HEADING!r} heading.'
        ) from error

    section_end = next(
        (
            index
            for index in range(heading_index + 1, len(lines))
            if lines[index].startswith('## ')
        ),
        len(lines),
    )
    resolved_names = _resolve_package_names(
        packages_root,
        package_prefix,
        package_names,
    )
    package_links = [
        f'- [{name}]({name}/index.md)'
        for name in resolved_names
    ]

    updated_lines = lines[:heading_index + 1] + [''] + package_links
    if section_end < len(lines):
        updated_lines += [''] + lines[section_end:]

    index_path.write_text(
        '\n'.join(updated_lines) + '\n',
        encoding='utf-8',
    )


def main() -> None:
    """Update an API package index from command-line arguments."""
    args = _parse_args()
    update_package_index(
        args.index_path,
        args.packages_root,
        args.package_prefix,
        args.package,
    )


if __name__ == '__main__':
    main()
