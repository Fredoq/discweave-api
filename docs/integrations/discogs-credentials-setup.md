# Discogs Credentials Setup

Roadmap 25 defines the operational setup path for Discogs autocomplete
credentials. This document records the configuration contract that later
provider implementation work must use. It does not add runtime Discogs API
calls, HTTP endpoints, database schema, or application services.

Discogs access is optional and disabled by default. Cratebase must keep
ordinary catalog, search, import, export, and restore workflows working when the
Discogs provider is disabled or when no Discogs credential is configured.

## Operator Setup

Use a Discogs account controlled by the Cratebase operator. In Discogs account
settings, open the `Developers` area to register the application or create an
API token. Discogs documents this entry point in the account settings page as
the place where applications can be registered and API tokens can be created.

Use a durable application identity:

- application name: `Cratebase`;
- user agent: `Cratebase/0.1 (+https://cratebase.example.com)`;
- website URL: the hosted Cratebase origin when it is available.

Do not use a name that implies Discogs partnership, sponsorship, endorsement, or
ownership. The app name and user agent must remain stable product identifiers,
not release-channel names.

## Configuration Keys

Future implementation must read Discogs provider settings from the backend
configuration section named `Discogs`.

```sh
Discogs__Enabled=false
Discogs__BaseUrl=https://api.discogs.com
Discogs__UserAgent="Cratebase/0.1 (+https://cratebase.example.com)"
Discogs__TimeoutSeconds=10
Discogs__AccessToken=<secret-discogs-api-token>
```

`Discogs__Enabled` controls whether external metadata endpoints may call
Discogs. The default is `false`.

`Discogs__BaseUrl` defaults to the official Discogs API root.

`Discogs__UserAgent` must identify Cratebase to Discogs and must not include
secret values.

`Discogs__TimeoutSeconds` is the outbound provider timeout. The default is
`10`.

`Discogs__AccessToken` is a secret. It must be stored only in the hosted
provider secret manager or in a local developer secret store. The token must not
be committed to Git, added to `deploy/.env.example`, exposed to `cratebase-web`,
or sent to the browser. Expected state: token not committed, not in sample
configuration, and not visible to clients.

## Hosted Configuration

Set non-secret defaults through ordinary environment configuration:

```sh
Discogs__Enabled=false
Discogs__BaseUrl=https://api.discogs.com
Discogs__UserAgent="Cratebase/0.1 (+https://cratebase.example.com)"
Discogs__TimeoutSeconds=10
```

When Discogs autocomplete is ready to be enabled, set
`Discogs__AccessToken` in the hosting provider's secret manager and set
`Discogs__Enabled=true` in the target environment.

Do not place the token in:

- repository files;
- Docker Compose example files;
- web build environment variables;
- desktop build environment variables;
- issue comments, PR descriptions, logs, screenshots, or support messages.

The browser web app must call only `cratebase-api` endpoints. It must never call
Discogs directly and must never receive the Discogs token.

## Local Development

Local development should run with Discogs disabled unless a developer is
explicitly working on the provider integration:

```sh
Discogs__Enabled=false
```

For provider work, use local user secrets or an untracked environment file:

```sh
dotnet user-secrets set "Discogs:Enabled" "true" --project src/Cratebase.Api/Cratebase.Api.csproj
dotnet user-secrets set "Discogs:AccessToken" "<developer-discogs-api-token>" --project src/Cratebase.Api/Cratebase.Api.csproj
```

The local token should belong to the developer or to an operator-approved test
account. Do not reuse production credentials locally.

Automated tests must use fake HTTP or fake provider implementations. Normal CI
must not require Internet access or real Discogs credentials.

## Optional Manual Smoke Check

The real-API smoke check is optional and must stay outside normal CI. Use it
only after provider code exists and only from an environment where the token is
available through secrets.

The smoke check should verify:

- provider disabled mode does not call Discogs;
- provider enabled mode sends the configured user agent;
- a simple request to `https://api.discogs.com` succeeds;
- rate-limit and provider-error responses are reported without leaking the
  token;
- unrelated Cratebase catalog workflows still work if Discogs is unavailable.

Do not paste real tokens into shell history, logs, or issue comments.

## Required Copy

Every public-facing use of Discogs API content must include this notice in
product or usage documentation:

> This application uses Discogs' API but is not affiliated with, sponsored or endorsed by Discogs. 'Discogs' is a trademark of Zink Media, LLC.

Every visible Discogs candidate must include attribution next to the data:

> Data provided by Discogs.

The attribution must link to the relevant `discogs.com` source page for the
candidate data.

## References

- [API Terms of Use](https://support.discogs.com/hc/en-us/articles/360009334593-API-Terms-of-Use)
- [Application Name and Description Policy](https://support.discogs.com/hc/en-us/articles/360009207054-Application-Name-and-Description-Policy)
- [Account settings / Developers token reference](https://support.discogs.com/hc/en-us/articles/360007423833-How-Do-I-Change-My-Account-Settings)
- [Discogs API root](https://api.discogs.com/)
