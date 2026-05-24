# Cratebase Acceptance Checklist

This checklist describes the shared product acceptance path for `cratebase-api`
and `cratebase-web`.

## Local Setup

- Run PostgreSQL and start the API with `ConnectionStrings__Cratebase`.
- Start the web app with the Vite proxy pointed at the API.
- Start the desktop app with `CRATEBASE_API_BASE_URL` pointed at the API.
- Bootstrap the first admin user when the database is empty.
- Sign in and confirm catalog routes use the authenticated cookie.

## Acceptance Path

1. Bootstrap a clean database and create the first admin user.
2. Create a release manually with artist credits, label metadata, tracklist rows, genres, tags and one owned item.
3. Search for the created data by artist, release title, track title, label, media, ownership status, tag and credit role.
4. Open catalog result details and verify server graph sections for credits, relations, media coverage, collector signals and workspace links.
5. Create a manual playlist with ordered release or track references and verify the order remains stable after reload.
6. Create a smart playlist with tag, genre, media, ownership status or year rules and verify results are computed from current catalog data.
7. Confirm playlists appear in search, export data, catalog links and graph backlinks.
8. Use the desktop app to scan a local audio folder and create an import review session.
9. Confirm every supported audio file includes a SHA-256 `contentHash` in the desktop scan request.
10. Re-import the same folder and verify fully duplicate drafts are no-ops against existing catalog data.
11. Rename or move duplicate files and verify same-collection content hash matching still preselects existing tracks.
12. Add a partial duplicate folder and verify existing tracks are preselected while missing catalog data can still be created.
13. Use saved search views for `remixes`, `productions`, `labels`, `physicalWithoutDigital`, `lossyWithoutLossless`, `wantedNotOwned` and `needsDigitization`.
14. Export JSON and CSV and verify core catalog data, import-created data, playlists and playlist entries are present.
15. Restore a JSON export into an empty collection and verify restored search, graph context, playlists and exports.

## Verification Commands

Backend:

```bash
dotnet test Cratebase.slnx
dotnet format Cratebase.slnx --verify-no-changes --verbosity diagnostic
```

Frontend:

```bash
npm run format:check
npm run lint
npm run typecheck
npm test
npm run build
```

## Product Boundaries

- Smart playlists are dynamic rules, not materialized snapshots.
- Browser import review is supported, but local folder scanning is desktop-only.
- Audio files are not uploaded to the API.
- External catalog integrations, streaming, marketplace, social, and recommendation features are outside the product boundary.
