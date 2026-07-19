# Documentation Publishing

Foundry documentation is built with MkDocs and published to a dedicated
Cloudflare Pages project. The public URL is routed through the existing
Dev Leader project-documentation Worker:

```text
https://www.devleader.ca/projects/foundry/
        -> Cloudflare Worker
        -> https://nexuslabs-foundry.pages.dev/
```

No Cloudflare custom domain is required for the Pages project. The Worker owns
the public path on `www.devleader.ca`.

## Versioned API documentation

Foundry preserves the same API-documentation model as Needlr:

- pushes to `main` regenerate `/api/dev/`;
- releases generate immutable `/api/v<version>/` archives and refresh
  `/api/stable/`;
- `gh-pages` acts as the merge store so development and release workflows own
  separate URL slices without overwriting each other;
- the API version switcher reads `/api/versions.json`;
- Cloudflare receives a trimmed mirror containing main, development, stable,
  and the newest version archives.

Historical archives remain in `gh-pages` history even after older versions are
trimmed from the Cloudflare deployment.

The Documentation and Release workflows only merge their owned slices into
`gh-pages`. A separate `Documentation Deployment` workflow checks out the
latest merged branch, trims old archives, and deploys that complete snapshot to
Cloudflare. This latest-snapshot deployment prevents concurrent main and
release builds from publishing partial or stale trees.

## Safe-by-default deployment

The `Documentation` workflow always builds and uploads the generated site as a GitHub
Actions artifact. Deployment remains disabled until the repository variable
`DOCS_DEPLOY_ENABLED` is set to `true`.

This allows documentation changes to merge before Cloudflare credentials or
routing are configured without breaking CI or creating public infrastructure.

## One-time Cloudflare setup

These steps require explicit approval because they create public
infrastructure:

1. Create the Pages project:

   ```powershell
   npx wrangler pages project create nexuslabs-foundry --production-branch main
   ```

2. Create a protected GitHub environment named `documentation` and add these
   environment secrets:

   - `CLOUDFLARE_API_TOKEN`
   - `CLOUDFLARE_ACCOUNT_ID`

3. Set the repository variable:

   ```text
   DOCS_DEPLOY_ENABLED=true
   ```

4. Run the `Documentation` workflow from `main` with `deploy=true`, or merge a
   subsequent documentation change to `main`.

## Dev Leader routing

The public path requires changes in `ncosentino/devleader-blog`:

1. Add `"foundry": "nexuslabs-foundry.pages.dev"` to the project router's
   `PROJECT_MAP`.
2. Deploy the `devleader-router` Worker.
3. Add Foundry to the `/projects` catalog and FAQ.
4. Add `https://www.devleader.ca/projects/foundry/sitemap.xml` to
   `robots.txt`.
5. Extend the project-page, robots, and live documentation tests.

Adding Foundry to `ncosentino/homebase` is optional. It only controls whether
Foundry appears on `links.devleader.ca`; it is not involved in documentation
routing.

## Verification

Verify each layer in order:

1. `python -m mkdocs build --strict`
2. `https://nexuslabs-foundry.pages.dev/`
3. `https://www.devleader.ca/projects/foundry/`
4. `https://www.devleader.ca/projects/foundry/sitemap.xml`
5. `https://www.devleader.ca/projects/foundry/llms.txt`

The public route should preserve canonical URLs, social metadata, sitemap
locations, and internal links under `/projects/foundry`.
