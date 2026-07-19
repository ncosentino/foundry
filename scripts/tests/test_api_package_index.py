from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest

from markdown import markdown


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from api_package_index import update_package_index


class ApiPackageIndexTests(unittest.TestCase):
    def test_replaces_placeholder_with_markdown_list(self) -> None:
        with TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            index_path = root / 'index.md'
            index_path.write_text(
                '# API Reference\n\n'
                '## Packages\n\n'
                '*API documentation is generated during the workflow.*\n',
                encoding='utf-8',
            )
            self._create_package(root, 'NexusLabs.Foundry.Zeta')
            self._create_package(root, 'NexusLabs.Foundry.Alpha')

            update_package_index(
                index_path,
                root,
                'NexusLabs.Foundry',
            )

            content = index_path.read_text(encoding='utf-8')
            self.assertEqual(
                '# API Reference\n\n'
                '## Packages\n\n'
                '- [NexusLabs.Foundry.Alpha]'
                '(NexusLabs.Foundry.Alpha/index.md)\n'
                '- [NexusLabs.Foundry.Zeta]'
                '(NexusLabs.Foundry.Zeta/index.md)\n',
                content,
            )
            rendered = markdown(content)
            self.assertIn('<ul>', rendered)
            self.assertNotIn('API documentation is generated', rendered)

    def test_replaces_existing_links_and_preserves_next_section(self) -> None:
        with TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            index_path = root / 'index.md'
            index_path.write_text(
                '# API Reference\n\n'
                '## Packages\n\n'
                '* [NexusLabs.Foundry.Old]'
                '(NexusLabs.Foundry.Old/index.md)\n\n'
                '## More\n\n'
                'Preserved content.\n',
                encoding='utf-8',
            )
            self._create_package(root, 'NexusLabs.Foundry.Current')

            update_package_index(
                index_path,
                root,
                'NexusLabs.Foundry',
                ['NexusLabs.Foundry.Current'],
            )
            update_package_index(
                index_path,
                root,
                'NexusLabs.Foundry',
                ['NexusLabs.Foundry.Current'],
            )

            content = index_path.read_text(encoding='utf-8')
            self.assertEqual(
                1,
                content.count('- [NexusLabs.Foundry.Current]'),
            )
            self.assertNotIn('NexusLabs.Foundry.Old', content)
            self.assertIn(
                '## More\n\nPreserved content.\n',
                content,
            )

    @staticmethod
    def _create_package(root: Path, name: str) -> None:
        package_directory = root / name
        package_directory.mkdir()
        (package_directory / 'index.md').write_text(
            f'# {name}\n',
            encoding='utf-8',
        )


if __name__ == '__main__':
    unittest.main()
