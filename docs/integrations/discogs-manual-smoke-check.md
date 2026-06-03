# Discogs Manual Smoke Check

This checklist is for a local or hosted environment where a real Discogs API
token is available through secrets. It is intentionally outside CI. Automated
tests for Discogs integration must keep using fake providers or fake HTTP
handlers, with no Internet dependency and no real credentials.

## Preconditions

- Roadmap 30 through 34 API code is deployed or running locally.
- The operator has configured `Discogs__AccessToken` through user secrets or a
  secret manager.
- `Discogs__Enabled=true` is set only for the environment being checked.
- The web client calls only `discweave-api`; it must not call Discogs directly.
- A test DiscWeave user can authenticate and access the default collection.

Do not paste real tokens into shell history, issue comments, screenshots, logs,
or committed files.

## Local Secret Setup

Use user secrets for local smoke checks:

```sh
dotnet user-secrets set "Discogs:Enabled" "true" --project src/DiscWeave.Api/DiscWeave.Api.csproj
dotnet user-secrets set "Discogs:AccessToken" "<developer-discogs-api-token>" --project src/DiscWeave.Api/DiscWeave.Api.csproj
```

Keep non-secret defaults in ordinary configuration:

```sh
Discogs__BaseUrl=https://api.discogs.com
Discogs__UserAgent="DiscWeave/0.1 (+https://discweave.example.com)"
Discogs__TimeoutSeconds=10
```

## Checks

1. Start the API and authenticate as a test user.
2. With `Discogs__Enabled=false`, call a Discogs autocomplete endpoint and
   confirm it returns a deterministic safe provider error without making a real
   upstream request.
3. With `Discogs__Enabled=true`, call release search:

```sh
curl -i \
  -b "<auth-cookie-file>" \
  "https://localhost:5001/api/external-metadata/discogs/releases?artist=New%20Order&title=Blue%20Monday&limit=5"
```

4. Call artist search:

```sh
curl -i \
  -b "<auth-cookie-file>" \
  "https://localhost:5001/api/external-metadata/discogs/artists?query=New%20Order&limit=5"
```

5. Call track search:

```sh
curl -i \
  -b "<auth-cookie-file>" \
  "https://localhost:5001/api/external-metadata/discogs/tracks?title=Blue%20Monday&artist=New%20Order&limit=5"
```

6. Pick one candidate from each search and call its detail endpoint.
7. Confirm every candidate and detail response includes Discogs attribution and
   a `discogs.com` source URL.
8. Confirm responses do not include `collectionId`, access tokens, upstream
   response bodies, stack traces, Discogs image URLs, marketplace data, Discogs
   user collection data, or wantlist data.
9. Temporarily set an invalid token and confirm `401` or `403` upstream
   responses map to a safe API error without exposing the token or upstream
   body.
10. If Discogs returns `429`, confirm the API returns `429` and preserves a
    safe `Retry-After` value when present.
11. Stop or block upstream network access and confirm ordinary catalog create,
    edit, import, export, and restore workflows still work without Discogs.

## Expected Boundaries

The smoke check must not create or update catalog data by itself. Discogs
responses are review data only. Persisted DiscWeave records may store optional
external source provenance after the user explicitly saves an artist, release,
or track form, but the API autocomplete endpoints must not persist anything.

The smoke check must not import Discogs images, marketplace data, user
collection data, wantlist data, or any OAuth/user-authorized Discogs data.
