"""Give API and overview pages distinct metadata without changing navigation."""

from pathlib import PurePosixPath


def api_title(path: PurePosixPath) -> str | None:
    parts = path.parts
    if not parts or parts[0] != 'api':
        return None

    if len(parts) == 2 and path.stem == 'index':
        return 'API Reference'
    if len(parts) < 3:
        return None

    version = {'dev': 'Development', 'stable': 'Stable'}.get(parts[1], parts[1])
    name = '.'.join(parts[2:-1])
    if path.stem == 'index':
        prefix = f'{name} ' if name else ''
        title = f'{prefix}API Reference ({version})'
    else:
        qualified_name = '.'.join((*parts[2:-1], path.stem))
        title = f'{qualified_name} ({version} API)'

    return title


def documentation_title(path: PurePosixPath) -> str | None:
    overviews = {
        'guides/index.md': 'Guides Overview',
        'analyzers/index.md': 'Analyzers Overview',
    }
    return overviews.get(str(path)) or api_title(path)


def on_page_context(context, page, config, nav):
    context['qualified_documentation_title'] = documentation_title(PurePosixPath(page.file.src_uri))
    context['documentation_title'] = (
        context['qualified_documentation_title'] or page.title
    )
    return context
