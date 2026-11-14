"""Rendered and published title-only documentation regression checks."""

from html import escape
from html.parser import HTMLParser
import importlib.util
import os
from pathlib import Path, PurePosixPath
import tempfile
import unittest

from mkdocs.commands.build import build
from mkdocs.config import load_config
import yaml


ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location(
    'refresh', ROOT / 'scripts' / 'refresh-docs-titles.py',
)
refresh = importlib.util.module_from_spec(spec)
spec.loader.exec_module(refresh)


class Metadata(HTMLParser):
    def __init__(self):
        super().__init__()
        self.title = ''
        self.heading = ''
        self.metas = {}
        self.tag = None

    def handle_starttag(self, tag, attrs):
        if tag in ('title', 'h1'):
            self.tag = tag
        if tag == 'meta':
            attrs = dict(attrs)
            self.metas[attrs.get('property') or attrs.get('name')] = attrs.get('content')

    def handle_endtag(self, tag):
        if tag == self.tag:
            self.tag = None

    def handle_data(self, data):
        if self.tag == 'title':
            self.title += data
        elif self.tag == 'h1':
            self.heading += data


class RenderedTitlesTests(unittest.TestCase):
    def test_rendered_metadata_preserves_headings_and_navigation(self):
        pages = {
            'index.md': ('Foundry', 'Foundry'),
            'getting-started.md': ('Getting Started', 'Getting Started'),
            'guides/index.md': ('Guides', 'Guides Overview'),
            'analyzers/index.md': ('Foundry analyzers', 'Analyzers Overview'),
            'api/index.md': ('API Reference', 'API Reference'),
            'api/dev/index.md': ('API Reference', 'API Reference (Development)'),
            'api/dev/NexusLabs.Foundry/index.md': (
                'Index', 'NexusLabs.Foundry API Reference (Development)',
            ),
            'api/stable/NexusLabs.Foundry/index.md': (
                'Index', 'NexusLabs.Foundry API Reference (Stable)',
            ),
            'api/v0.1.0-alpha.1/NexusLabs.Foundry/index.md': (
                'Index', 'NexusLabs.Foundry API Reference (v0.1.0-alpha.1)',
            ),
            'api/stable/NexusLabs.Foundry/Plugin.md': (
                'Plugin', 'NexusLabs.Foundry.Plugin (Stable API)',
            ),
            'api/dev/NexusLabs.Foundry/Plugin.md': (
                'Plugin', 'NexusLabs.Foundry.Plugin (Development API)',
            ),
            'api/dev/NexusLabs.Foundry.Other/Plugin.md': (
                'Plugin', 'NexusLabs.Foundry.Other.Plugin (Development API)',
            ),
            'api/v0.1.0-alpha.1/NexusLabs.Foundry/Factory_T_.Nested.md': (
                'Factory<T>.Nested',
                'NexusLabs.Foundry.Factory_T_.Nested (v0.1.0-alpha.1 API)',
            ),
        }
        with tempfile.TemporaryDirectory(dir=ROOT) as directory:
            workspace = Path(directory)
            docs = workspace / 'docs'
            for source, (heading, _) in pages.items():
                path = docs / source
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(f'# {escape(heading)}\n', encoding='utf-8')
            overrides = {}
            if os.environ.get('DOCS_TITLES_BASELINE') == '1':
                baseline = workspace / 'overrides'
                baseline.mkdir()
                (baseline / 'main.html').write_text(
                    '{% extends "base.html" %}{% block extrahead %}'
                    '<meta property="og:title" content="{{ page.title }} – {{ config.site_name }}" />'
                    '<meta name="twitter:title" content="{{ page.title }} – {{ config.site_name }}" />'
                    '{% endblock %}', encoding='utf-8',
                )
                overrides = {
                    'hooks': [],
                    'theme': {'name': 'material', 'custom_dir': str(baseline)},
                }
            config = load_config(
                config_file=str(ROOT / 'mkdocs.yml'),
                docs_dir=str(docs), site_dir=str(workspace / 'site'),
                site_url='https://example.test/foundry/',
                nav=[
                    {'Unchanged navigation label': source}
                    if source in ('api/index.md', 'guides/index.md', 'analyzers/index.md')
                    else source
                    for source in pages
                ],
                extra_javascript=[], **overrides,
            )
            build(config)
            for source, (heading, expected) in pages.items():
                with self.subTest(source=source):
                    url = PurePosixPath(source).with_suffix('')
                    if url.name == 'index':
                        url = url.parent
                    html = (workspace / 'site' / str(url) / 'index.html').read_text(encoding='utf-8')
                    parsed = Metadata()
                    parsed.feed(html)
                    self.assertEqual(
                        parsed.title.strip(),
                        'Foundry' if source == 'index.md' else f'{expected} - Foundry',
                    )
                    self.assertEqual(parsed.metas['og:title'], f'{expected} – Foundry')
                    self.assertEqual(parsed.metas['twitter:title'], parsed.metas['og:title'])
                    self.assertEqual(parsed.heading.rstrip('¶'), heading)
                    self.assertIn('Unchanged navigation label', html)


class RefreshTitlesTests(unittest.TestCase):
    HTML = (
        '<html><head>\r\n<title>Index - Foundry</title>'
        '<meta property="og:title" content="Index – Foundry">'
        '<meta name="twitter:title" content="Index – Foundry">'
        '<link rel="canonical" href="/unchanged/">'
        '<meta name="description" content="Unchanged">'
        '</head><body><h1>Index</h1><nav>Original</nav>'
        '<svg><title>Diagram</title></svg>λ</body></html>\r\n'
    )

    def test_only_three_values_change_and_repeat_is_identical(self):
        source = PurePosixPath('api/stable/NexusLabs.Foundry/Factory_T_.Nested.md')
        title = 'NexusLabs.Foundry.Factory_T_.Nested (Stable API)'
        expected = self.HTML.replace('Index - Foundry', f'{title} - Foundry')
        expected = expected.replace('Index – Foundry', f'{title} – Foundry')
        actual = refresh.refresh_html(self.HTML, source)
        self.assertEqual(actual.encode(), expected.encode())
        self.assertEqual(refresh.refresh_html(actual, source), actual)

    def test_preview_and_apply_validate_every_file_before_writing(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as directory:
            site = Path(directory)
            paths = [
                site / 'api/stable/NexusLabs.Foundry/index.html',
                site / 'api/v0.1.0-alpha.1/NexusLabs.Foundry/Factory.Nested/index.html',
            ]
            untouched = site / 'api/dev/NexusLabs.Foundry/index.html'
            for path in [*paths, untouched]:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(self.HTML.encode())
            preview = refresh.refresh_site(site)
            self.assertEqual(preview['scanned'], 2)
            self.assertEqual(preview['changed'], 2)
            self.assertTrue(all(path.read_bytes() == self.HTML.encode() for path in paths))
            paths[1].write_bytes(b'<html><head></head></html>')
            with self.assertRaises(ValueError):
                refresh.refresh_site(site, apply=True)
            self.assertEqual(paths[0].read_bytes(), self.HTML.encode())
            paths[1].write_bytes(self.HTML.encode())
            self.assertEqual(refresh.refresh_site(site, apply=True), preview)
            self.assertEqual(refresh.refresh_site(site)['changed'], 0)
            self.assertEqual(untouched.read_bytes(), self.HTML.encode())

    def test_missing_and_duplicate_metadata_are_rejected(self):
        for html in (
            self.HTML.replace('<title>Index - Foundry</title>', ''),
            self.HTML.replace('</head>', '<meta property="og:title" content="duplicate"></head>'),
            self.HTML.replace('content="Index – Foundry"', 'content=Index'),
        ):
            with self.subTest(html=html), self.assertRaises(ValueError):
                refresh.refresh_html(html, PurePosixPath('api/stable/index.md'))

    def test_escaped_values_preserve_quotes_and_nested_attribute_text(self):
        original = self.HTML.replace(
            'property="og:title"',
            'data-note=\'content="not a title"\' property="og:title"',
        ).replace('content="Index – Foundry"', "content='Index – Foundry'")
        actual = refresh.refresh_html(
            original, PurePosixPath('api/stable/NexusLabs.Foundry/Factory&T.md'),
        )
        expected = original.replace(
            'Index - Foundry', 'NexusLabs.Foundry.Factory&amp;T (Stable API) - Foundry',
        ).replace(
            'Index – Foundry', 'NexusLabs.Foundry.Factory&amp;T (Stable API) – Foundry',
        )
        self.assertEqual(actual, expected)

    def test_published_overview_titles_are_refreshed_without_body_changes(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as directory:
            site = Path(directory)
            paths = {
                'api/stable/index.html': 'API Reference (Stable)',
                'api/index.html': 'API Reference',
                'guides/index.html': 'Guides Overview',
                'analyzers/index.html': 'Analyzers Overview',
            }
            for source in paths:
                path = site / source
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(self.HTML.encode())
            result = refresh.refresh_site(site, apply=True)
            self.assertEqual(result['changed'], 4)
            for source, title in paths.items():
                expected = self.HTML.replace('Index - Foundry', f'{title} - Foundry')
                expected = expected.replace('Index – Foundry', f'{title} – Foundry')
                self.assertEqual((site / source).read_bytes(), expected.encode())
            self.assertEqual(refresh.refresh_site(site)['changed'], 0)

    def test_symlinks_cannot_redirect_writes(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as directory:
            site = Path(directory)
            stable = site / 'api/stable'
            stable.mkdir(parents=True)
            outside = site / 'outside.html'
            outside.write_bytes(self.HTML.encode())
            (stable / 'index.html').symlink_to(outside)
            with self.assertRaises(ValueError):
                refresh.refresh_site(site, apply=True)
            self.assertEqual(outside.read_bytes(), self.HTML.encode())


class PublisherContractTests(unittest.TestCase):
    def test_publishers_share_non_cancelling_lock(self):
        owners = (
            ('docs.yml', 'deploy'), ('release.yml', 'documentation'),
            ('docs-titles.yml', 'refresh-published-titles'),
        )
        for workflow, job in owners:
            data = yaml.safe_load((ROOT / '.github/workflows' / workflow).read_text())
            self.assertEqual(data['jobs'][job]['concurrency'], {
                'group': 'foundry-documentation-publication', 'cancel-in-progress': False,
            })
        data = yaml.safe_load((ROOT / '.github/workflows/docs-cloudflare.yml').read_text())
        self.assertEqual(data['concurrency']['group'], 'foundry-documentation-publication')
        self.assertFalse(data['concurrency']['cancel-in-progress'])

    def test_receipts_are_repeat_safe(self):
        data = yaml.safe_load((ROOT / '.github/workflows/docs-titles.yml').read_text())
        for job in data['jobs'].values():
            for step in job['steps']:
                if step.get('uses', '').startswith('actions/upload-artifact@'):
                    self.assertIn('${{ github.run_attempt }}', step['with']['name'])


if __name__ == '__main__':
    unittest.main()
