# Hosted Deployment Baseline

Cratebase v1 private beta uses a hosted service plus the macOS desktop companion. This document defines the repo-level deployment contract without choosing a hosting provider.

The placeholder public origin is `https://cratebase.example.com` until the real private beta domain is chosen.

## Topology

The baseline hosted topology is:

- one public HTTPS origin;
- a reverse proxy that terminates TLS and routes requests;
- a `cratebase-api` container running `Cratebase.Api` on internal HTTP port `8080`;
- a `cratebase-web` container serving the React build on internal HTTP port `8080`;
- managed PostgreSQL for durable catalog, auth, import, search, export, playlist, and settings data;
- persistent service storage for release covers and the optional macOS installer artifact.

Routing is same-origin:

- `/api/*` routes to the API container;
- `/health` routes to the API container;
- `/web-health` routes to the web container;
- every other path routes to the web container and falls back to `index.html`.

The browser app uses relative `/api` requests. Do not configure browser CORS for normal hosted use. Packaged desktop builds from `cratebase-web` default to the placeholder origin until the real private beta domain is chosen, and runtime deployments can override the API target with `CRATEBASE_API_BASE_URL`.

## Environments

Use separate staging and production environments. They must not share:

- PostgreSQL databases;
- release cover storage roots;
- desktop installer artifacts;
- application secrets;
- invite data;
- user accounts.

Staging exists to validate migrations, reverse proxy routing, cookie behavior, desktop API connectivity, imports, exports, and restore drills before production rollout.

## Required API Configuration

Set these API environment variables in hosted environments:

```sh
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__Cratebase=<managed-postgresql-connection-string>
ReleaseCovers__StorageRoot=/var/lib/cratebase/release-covers
DesktopDownloads__MacOsInstallerPath=/var/lib/cratebase/desktop/Cratebase.dmg
```

`ConnectionStrings__Cratebase` is a secret. Store it in the provider secret manager, not in Git.

`ReleaseCovers__StorageRoot` must point at persistent service storage. The API stores release cover files, not audio files.

`DesktopDownloads__MacOsInstallerPath` is optional until the desktop installer is published. If configured, it should point at the hosted DMG artifact that `/api/imports/desktop-downloads/macos` can serve.

## TLS, Cookies, And Proxy Rules

The public origin must be HTTPS. Production cookies are secure HTTP-only cookies and rely on same-origin requests.

The reverse proxy must forward:

- `Host`;
- `X-Forwarded-For`;
- `X-Forwarded-Host`;
- `X-Forwarded-Proto`.

Keep the API container private to the service network. Only the reverse proxy should be public.
Configure `HostedSecurity:ForwardedHeaders:KnownNetworks` or
`HostedSecurity:ForwardedHeaders:KnownProxies` for the reverse proxy network or
IP address so forwarded origin and client IP values are accepted only from
trusted peers.

## Migrations

Run EF Core migrations as an explicit release step before starting the new API version against production traffic.

Recommended source-tree command:

```sh
CRATEBASE_DESIGN_TIME_CONNECTION_STRING="<managed-postgresql-connection-string>" \
  dotnet ef database update \
  --project src/Cratebase.Infrastructure/Cratebase.Infrastructure.csproj \
  --startup-project src/Cratebase.Api/Cratebase.Api.csproj
```

Before migrations that affect catalog, ownership, import, search, export, settings, playlists, ratings, or authentication data, take a managed database backup. Keep the backup until the hosted acceptance path passes.

Do not run automatic destructive migrations from API startup.

## Example Compose Stack

The example stack in `deploy/compose.yaml` is a local integration reference for the hosted topology. It expects this workspace shape:

```text
cratebase/
  cratebase-api/
  cratebase-web/
```

Run it from `cratebase-api`:

```sh
cp deploy/.env.example deploy/.env
docker compose --env-file deploy/.env -f deploy/compose.yaml up --build
```

The example includes an `api-migrations` one-shot service. It runs EF Core migrations before the API container starts. Hosted production releases should still treat migrations as an explicit release step.

Then check:

```sh
curl http://localhost:8080/health
curl http://localhost:8080/web-health
```

The example publishes HTTP on `localhost:8080` only for local validation. Real hosted environments must terminate TLS before the reverse proxy.

If local port `8080` is already in use, override `CRATEBASE_HOST_PORT` in `deploy/.env`.

## Release Responsibilities

API release:

1. Build and publish the API image.
2. Back up the target database.
3. Apply EF Core migrations.
4. Deploy the API container with production environment variables.
5. Verify `/health` through the public origin.

Web release:

1. Build and publish the web static image.
2. Deploy it behind the same reverse proxy.
3. Verify browser login and authenticated `/api` calls through the public origin.

Desktop release:

1. Build the macOS DMG from `cratebase-web`.
2. Publish it into the configured persistent installer path.
3. Confirm the packaged desktop API target is the private beta origin, or provide `CRATEBASE_API_BASE_URL` as a runtime override when targeting another hosted origin.

Database and storage recovery are operator responsibilities and are covered by the later hosted backup and restore roadmap item.
