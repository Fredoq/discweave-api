# Hosted Security Baseline

DiscWeave private beta uses secure HTTP-only cookie authentication on one
hosted origin. The hosted API must assume public internet traffic and protect
auth, invite, import, export and restore endpoints without turning v1 into a
general public SaaS platform.

## HTTP And Cookie Baseline

- Terminate TLS at the public reverse proxy.
- Forward `Host`, `X-Forwarded-For`, `X-Forwarded-Host` and
  `X-Forwarded-Proto` to the API.
- Keep the API container private to the service network. The API accepts one
  forwarded-header hop from the reverse proxy when its network or IP is listed
  under `HostedSecurity:ForwardedHeaders:KnownNetworks` or
  `HostedSecurity:ForwardedHeaders:KnownProxies`.
- Production cookies are secure, HTTP-only and `SameSite=Lax`.
- Browser API calls remain same-origin relative `/api` calls.
- Do not add broad browser CORS for hosted v1.

Production responses include baseline security headers:

- `Strict-Transport-Security`;
- `X-Content-Type-Options: nosniff`;
- `X-Frame-Options: DENY`;
- `Referrer-Policy: no-referrer`.

Production server errors return structured `server.error` responses without
stack traces or internal exception details.

## CSRF And Origin Posture

Unsafe methods reject an untrusted `Origin` header with
`security.origin_invalid`. Same-origin requests are allowed. Loopback HTTP(S)
origins are allowed so the Electron desktop proxy can forward authenticated
metadata submissions to the hosted API.

Missing `Origin` headers are not rejected in v1 because non-browser operational
checks and desktop flows may omit them. This baseline targets browser
cross-site request abuse while keeping private beta operations practical.

## Rate Limits

The hosted API uses in-process rate limits as a private beta baseline:

| Surface | Partition | Limit |
| --- | --- | --- |
| `POST /api/auth/register`, `POST /api/auth/login` | remote IP | 10/minute |
| invite, admin user and password lifecycle endpoints | authenticated user, then IP | 20/minute |
| `POST /api/imports/desktop-folder-scans` | authenticated user, then IP | 12/hour |
| `/api/exports/*` | authenticated user, then IP | 10/hour |

Rejected requests return `429` with `rate_limit.exceeded`.

These limits protect the private beta from accidental loops and basic abuse.
If public signup or multiple API instances are introduced later, move rate
limiting to a shared store.

## Invite Abuse Controls

Invites remain single-use and server-generated. Invite list responses never
return plaintext codes. Revoked, expired, redeemed and unknown invites cannot
create accounts. Admin endpoints are role-protected.

## Logging Redaction

Application and reverse-proxy logs must not record:

- passwords or temporary passwords;
- plaintext invite codes;
- cookies or session identifiers;
- desktop local file paths from import payloads;
- user collection metadata, search terms or exported collection contents.

Operational logs should prefer request path, status code, timing, coarse
authenticated user id and correlation data. Payload logging is disabled for
private collection data.
