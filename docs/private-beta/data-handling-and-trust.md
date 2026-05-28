# Private Beta Data Handling And Trust

Cratebase private beta is a hosted personal music archive with a macOS desktop
companion. It is invite-only and operated for a small group of collectors while
the v1 workflows are validated with real catalog data.

## What Cratebase Stores

Cratebase stores private collection metadata for the authenticated user's
default collection:

- artists, labels, releases, tracks, credits and relations;
- owned item inventory, media formats, condition, storage location and status;
- import sessions, review drafts and confirmed catalog records;
- playlists, settings, dictionaries, rating criteria and ratings;
- release cover files uploaded by the user or imported as cover artifacts.

Normal user-facing API responses do not expose `collectionId`.

## Desktop Import Boundary

The browser app never browses arbitrary local folders. Local folder selection
belongs to the macOS desktop companion.

Desktop import sends metadata needed for archive review:

- file identity, relative paths and local paths for inventory;
- SHA-256 content hashes for supported audio files;
- size and last-modified timestamps;
- audio tags such as artist, title, album, year, duration and track number;
- cover artifacts used as release cover candidates.

Desktop import does not upload audio files. The API stores metadata and review
state, not third-party audio bytes.

## Exports And Backups

Users can trigger JSON and CSV exports for portability, spreadsheet workflows
and personal backup habits. Export v1 includes confirmed catalog data and
omits account data, raw cover bytes, audio bytes and import review drafts.

Hosted service recovery is separate. The operator is responsible for managed
PostgreSQL backups and service-storage backups for release covers and desktop
artifacts. User exports are valuable, but they are not the only hosted
disaster-recovery mechanism.

## Known V1 Limits

- Signup is invite-only.
- Each user has one default private collection.
- JSON restore is limited to an empty active collection.
- Desktop import is reviewable and idempotent, but users should inspect draft
  releases before confirmation.
- External catalog integrations, streaming, public profiles, sharing,
  marketplace flows and recommendation engines are outside v1.
- Backup retention and support response targets may change after provider and
  private beta operations are finalized.

## Support Expectations

Private beta support is operator-assisted. Users should report import failures,
search gaps, export concerns, onboarding confusion and data recovery concerns
with enough detail to reproduce the issue. Logs and screenshots should remove
passwords, invite codes, cookies and private collection data unless explicitly
requested through a private support channel.
