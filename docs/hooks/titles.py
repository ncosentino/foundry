"""Give generated API pages distinct metadata without changing navigation."""

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


def on_page_context(context, page, config, nav):
    context['api_documentation_title'] = api_title(PurePosixPath(page.file.src_uri))
    context['documentation_title'] = (
        context['api_documentation_title'] or page.title
    )
    return context
