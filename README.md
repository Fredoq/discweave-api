# cratebase-api

Backend API for Cratebase, a personal music archive for cataloging releases,
tracks, media, owned items, credits, artist relations, imports, playlists,
search, graph navigation, and exports.

Cratebase is an archive, not a music player. The API is shaped around the
collection domain and the question: what is in the collection, and how is it
connected?

## Product Status

The backend is a product API for authenticated personal music archives. It has
local accounts, one default private collection per user, collection-scoped
catalog APIs, manual CRUD, credits, labels, artist and track relations, rating
criteria, release cover uploads, local folder import review, content-hash import
deduplication, persistent manual and smart playlists, search saved views,
catalog graph context, compact catalog links, JSON/CSV exports, and JSON
restore into empty collections.

## Requirements

- .NET SDK 10.0.100 or newer 10.0 feature band
- PostgreSQL for local API runs
- Docker-compatible runtime for integration tests that use Testcontainers

The repository pins the SDK in `global.json`.

## Solution Layout

- `src/Cratebase.Domain` - domain entities, value objects, enums, and invariants.
- `src/Cratebase.Application` - application services and use-case contracts.
- `src/Cratebase.Infrastructure` - EF Core persistence, identity, search, files, and queries.
- `src/Cratebase.Importing` - folder scan parsing and import grouping primitives.
- `src/Cratebase.Api` - ASP.NET Core composition root and HTTP endpoints.
- `tests/Cratebase.Domain.Tests` - domain behavior tests.
- `tests/Cratebase.Infrastructure.Tests` - persistence and collection-boundary tests.
- `tests/Cratebase.Api.Tests` - API contract and workflow tests.

## Local API Setup

Restore and build:

```bash
dotnet restore Cratebase.slnx
dotnet build Cratebase.slnx
```

Run the API against a local PostgreSQL database:

```bash
ConnectionStrings__Cratebase="Host=localhost;Port=5432;Database=cratebase;Username=<postgres-user>;Password=<postgres-password>" \
  dotnet run --project src/Cratebase.Api/Cratebase.Api.csproj --launch-profile http
```

The HTTP launch profile listens on `http://localhost:5094`.

Health check:

```http
GET /health
```

Expected response:

```json
{
  "service": "cratebase-api",
  "status": "ok"
}
```

Use the web app first-user bootstrap form when the database has no users. After
bootstrap, catalog, search, import, export, playlist, rating, and settings
routes require the authenticated cookie and resolve the active collection from
the user's default collection.

## Hosted Private Beta Baseline

The v1 private beta deployment baseline is vendor-neutral and same-origin:

- the public placeholder origin is `https://cratebase.example.com`;
- `/api/*` and `/health` route to the API container;
- `/web-health` and browser web routes go to the React static web container;
- PostgreSQL is managed service data;
- release covers and the optional macOS installer live in persistent service storage.

The API container listens on internal HTTP port `8080` and expects production
configuration from environment variables:

```sh
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__Cratebase=<managed-postgresql-connection-string>
ReleaseCovers__StorageRoot=/var/lib/cratebase/release-covers
DesktopDownloads__MacOsInstallerPath=/var/lib/cratebase/desktop/Cratebase.dmg
Discogs__Enabled=false
Discogs__BaseUrl=https://api.discogs.com
Discogs__UserAgent="Cratebase/0.1 (+https://cratebase.example.com)"
Discogs__TimeoutSeconds=10
```

`Discogs__AccessToken` is required only when Discogs autocomplete is enabled.
Store it in the hosted secret manager or a local developer secret store; never
commit it or expose it to `cratebase-web`. See
[docs/integrations/discogs-credentials-setup.md](docs/integrations/discogs-credentials-setup.md)
for the credential setup contract.

See [docs/hosting/hosted-deployment-baseline.md](docs/hosting/hosted-deployment-baseline.md)
for topology, migration, secret, TLS, reverse proxy, storage, and compose
example details.
See [docs/hosting/hosted-backup-restore-baseline.md](docs/hosting/hosted-backup-restore-baseline.md)
for managed PostgreSQL, service storage, and local restore drill expectations.
See [docs/security/hosted-security-baseline.md](docs/security/hosted-security-baseline.md)
for forwarded-header, same-origin, rate-limit, security-header, and logging
redaction expectations.
Private beta data handling and release readiness are documented in
[docs/private-beta/data-handling-and-trust.md](docs/private-beta/data-handling-and-trust.md)
and
[docs/private-beta/release-readiness.md](docs/private-beta/release-readiness.md).

## Verification

```bash
dotnet test Cratebase.slnx
dotnet format Cratebase.slnx --verify-no-changes --verbosity diagnostic
bash deploy/hosted-restore-drill.sh
```

## Product Workflows

- Bootstrap the first admin user and default music collection.
- Create and edit artists, labels, releases, tracks, owned items, credits, and relations.
- Search across entities, roles, tags, labels, media, ownership status, and collector saved views.
- Review local folder import sessions produced by the desktop scanner.
- Deduplicate imports by same-collection SHA-256 content hash, then by path, size, and mtime fallback.
- Persist manual playlists with ordered release and track references.
- Persist smart playlists with dynamic tag, genre, media, ownership status, and year rules.
- Navigate graph context with credits, relations, media coverage, collector signals, and playlist backlinks.
- Export portable JSON and CSV data, including playlists.
- Restore a JSON export into an empty active collection.

See [docs/exports/portable-export-v1.md](docs/exports/portable-export-v1.md)
for the hosted JSON/CSV export contract, CSV table headers, collection scoping,
cover metadata boundary, and restore limits.

See [docs/search-v1.md](docs/search-v1.md) for the finalized search request and
response contract, saved views, collection isolation behavior, and large-seed
smoke verification command.

See [docs/imports/desktop-import-api-boundary.md](docs/imports/desktop-import-api-boundary.md)
for the hosted desktop folder scan contract, including request and response
shapes, collection scoping, no-audio-upload boundaries, cover artifact limits,
and duplicate matching rules.

See [docs/quality/large-collection-quality-baseline.md](docs/quality/large-collection-quality-baseline.md)
for the catalog quality report, destructive delete confirmation tokens, and
large-collection performance smoke probes.

## Large Collection Seed

Use `Cratebase.Seeding` to create a synthetic collection for search, graph,
export, and UI load testing. The command creates a separate local account and
default collection, applies migrations, and refuses to add duplicate seed data
when that seed collection already contains catalog records.

Default scale: 1,200 artists, 120 labels, 1,500 releases, 12,000 tracks, owned
items, credits, relations, playlists, and rebuilt search documents.

```bash
dotnet run --project src/Cratebase.Seeding/Cratebase.Seeding.csproj -- \
  --connection-string "Host=localhost;Port=5432;Database=cratebase;Username=postgres;Password=postgres"
```

Sign in with:

- email: `seed@cratebase.local`
- password: `SeedPassword1!`

Custom scale:

```bash
dotnet run --project src/Cratebase.Seeding/Cratebase.Seeding.csproj -- \
  --connection-string "Host=localhost;Port=5432;Database=cratebase;Username=postgres;Password=postgres" \
  --artists 3000 \
  --labels 250 \
  --releases 5000 \
  --tracks-per-release 10
```

Run the search v1 smoke probes after seeding:

```bash
dotnet run --project src/Cratebase.Seeding/Cratebase.Seeding.csproj -- \
  --connection-string "Host=localhost;Port=5432;Database=cratebase;Username=postgres;Password=postgres" \
  --verify-search \
  --search-budget-ms 250
```

Run the large-collection performance smoke probes after seeding:

```bash
dotnet run --project src/Cratebase.Seeding/Cratebase.Seeding.csproj -- \
  --connection-string "Host=localhost;Port=5432;Database=cratebase;Username=postgres;Password=postgres" \
  --verify-performance \
  --performance-budget-ms 250
```

## Product Boundaries

- Smart playlists are dynamic rule queries; they are not materialized snapshots.
- Local audio scanning belongs to the desktop client. The API stores metadata and file identity, not audio files.
- Catalog links are compact lookup results for selectors, not a full replacement for search.
- V1 has no external Discogs, MusicBrainz, streaming, social, marketplace, or recommendation integrations.
- V1.1 Discogs work is limited to bounded autocomplete and review flows; see
  [docs/integrations/discogs-autocomplete-boundary.md](docs/integrations/discogs-autocomplete-boundary.md).

See [docs/acceptance-checklist.md](docs/acceptance-checklist.md) for the shared
acceptance path.

## Build Configuration

Shared build settings live in `Directory.Build.props`. Package versions are
centralized in `Directory.Packages.props`.

Style rules live in `.editorconfig` and are enforced by the `Check` GitHub
Actions workflow through `dotnet format`.

## CI Workflows

- `Check` verifies formatting and style.
- `Build` restores and builds the solution in Release configuration.
- `Test` restores, builds, and runs the xUnit test projects.
- `SonarQube` runs SonarQube Cloud analysis for `Fredoq_cratebase-api`.

## Engineering Notes

- Keep domain behavior independent of persistence and HTTP concerns.
- Keep owned items separate from releases and media formats.
- Model credits and relations explicitly so role-based search remains first-class.
- Do not add Redis, RabbitMQ, background workers, or external catalog integrations until a concrete product scenario requires them.
- Use explicit mapping and local validation code. Do not use AutoMapper, MediatR, Moq, or FluentValidation.
