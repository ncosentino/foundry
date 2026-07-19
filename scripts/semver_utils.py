"""Minimal Semantic Versioning precedence helpers."""

from __future__ import annotations

from functools import cmp_to_key
import re


_SEMVER_PATTERN = re.compile(
    r'^(\d+)\.(\d+)\.(\d+)'
    r'(?:-([0-9A-Za-z.-]+))?'
    r'(?:\+[0-9A-Za-z.-]+)?$'
)


def compare_semver(left: str, right: str) -> int:
    """Compare two SemVer values using the SemVer 2.0 precedence rules."""
    left_match = _SEMVER_PATTERN.match(left)
    right_match = _SEMVER_PATTERN.match(right)
    if not left_match or not right_match:
        return (left > right) - (left < right)

    left_core = tuple(int(value) for value in left_match.groups()[:3])
    right_core = tuple(int(value) for value in right_match.groups()[:3])
    if left_core != right_core:
        return (left_core > right_core) - (left_core < right_core)

    left_prerelease = left_match.group(4)
    right_prerelease = right_match.group(4)
    if left_prerelease is None:
        return 0 if right_prerelease is None else 1
    if right_prerelease is None:
        return -1

    left_parts = left_prerelease.split('.')
    right_parts = right_prerelease.split('.')
    for left_part, right_part in zip(left_parts, right_parts):
        if left_part == right_part:
            continue

        left_numeric = left_part.isdigit()
        right_numeric = right_part.isdigit()
        if left_numeric and right_numeric:
            return (int(left_part) > int(right_part)) - (
                int(left_part) < int(right_part)
            )
        if left_numeric != right_numeric:
            return -1 if left_numeric else 1
        return (left_part > right_part) - (left_part < right_part)

    return (len(left_parts) > len(right_parts)) - (
        len(left_parts) < len(right_parts)
    )


def sort_semver_descending(versions: list[str]) -> list[str]:
    """Return versions in descending SemVer precedence order."""
    return sorted(
        versions,
        key=cmp_to_key(compare_semver),
        reverse=True,
    )
