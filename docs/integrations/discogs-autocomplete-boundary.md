# Discogs Autocomplete Boundary

Roadmap 24 defines the product, data, and compliance boundary for
Discogs-assisted autocomplete in DiscWeave v1.1. This document is a decision
record for later API and web implementation issues. It does not introduce
runtime endpoints, database schema, or Discogs API calls by itself.

DiscWeave remains the user's private music archive. Discogs can help reduce
manual entry effort, but DiscWeave catalog records stay editable local data and
remain the source of truth for the user's collection.

## V1.1 Scope

The v1.1 Discogs integration is limited to autocomplete and review workflows:

- release autocomplete for manual release creation and update review;
- artist autocomplete for manual artist creation and update review;
- release-backed track autocomplete where Discogs track data is shown with its
  release context.

Discogs data must be presented as candidate metadata. The user must review the
candidate before DiscWeave applies any field to an artist, release, track,
medium, credit, relation, label, or owned item.

## Explicit Exclusions

The autocomplete slice must not include:

- importing, storing, proxying, or displaying Discogs images as DiscWeave cover
  art or artist media;
- marketplace data, inventory, pricing suggestions, orders, sales history, or
  seller workflows;
- Discogs user collection data;
- Discogs wantlist data;
- user Discogs OAuth, linked Discogs accounts, or user-authorized Discogs
  collection import;
- public profiles, sharing, social behavior, recommendation behavior, or any
  workflow that turns DiscWeave into a Discogs replacement;
- automatic catalog writes, silent overwrites, or automatic merge decisions
  based only on Discogs identifiers.

Future Discogs OAuth or collection import work needs a separate product
decision before implementation. That work must stay separate from the
server-held provider credentials used for autocomplete.

## Source Of Truth

Accepted Discogs-assisted data becomes ordinary editable DiscWeave catalog
data. DiscWeave must not require a Discogs identifier for artist, release,
track, label, credit, relation, medium, or owned-item identity.

Discogs identifiers and source URLs may be persisted only as optional external
source provenance. Provenance is useful for traceability, but it is not a
canonical identity system and must not replace DiscWeave's collection-scoped
domain identifiers.

Update flows must use review/apply semantics:

- candidate search returns selectable summaries and source links;
- candidate detail maps provider data into DiscWeave-friendly draft fields;
- the UI shows attribution and differences before applying fields;
- applying a candidate updates only fields the user explicitly accepts;
- local DiscWeave edits remain valid even when provider data later changes.

## API And Web Boundary

All Discogs calls must go through `discweave-api`. `discweave-web` must not
call Discogs directly, embed Discogs credentials, or depend on Discogs response
shapes.

Backend integration work must keep these rules:

- Discogs credentials, API base URL, user agent, timeout, and feature enablement
  are backend configuration only;
- external metadata endpoints require the authenticated DiscWeave cookie and
  current collection access;
- clients must not pass `collectionId` for autocomplete operations;
- normal user-facing responses must not expose `collectionId`;
- provider failures must map to deterministic structured errors suitable for
  UI handling;
- automated tests must use fake HTTP or provider implementations and must not
  require Internet access;
- optional manual smoke checks against the real Discogs API must stay outside
  normal CI.

Provider response contracts must include enough information for visible
attribution and source links wherever Discogs candidate data is displayed.

## Compliance Notes

Reviewed on 2026-05-30 against the official Discogs policy pages:

- [API Terms of Use](https://support.discogs.com/hc/en-us/articles/360009334593-API-Terms-of-Use)
- [Application Name and Description Policy](https://support.discogs.com/hc/en-us/articles/360009207054-Application-Name-and-Description-Policy)
- [Account settings / Developers token reference](https://support.discogs.com/hc/en-us/articles/360007423833-How-Do-I-Change-My-Account-Settings)
- [Discogs API root](https://api.discogs.com/)

Discogs API terms distinguish broadly reusable catalog metadata from restricted
data such as user data, marketplace data, and images. The v1.1 autocomplete
scope intentionally uses release, artist, label, tracklist, credit, barcode,
catalog number, version, and source-link metadata while excluding restricted
data categories that are not needed for autocomplete.

Every public-facing use of Discogs API content must show the required
application notice in product or usage documentation:

> This application uses Discogs' API but is not affiliated with, sponsored or endorsed by Discogs. 'Discogs' is a trademark of Zink Media, LLC.

Every visible Discogs candidate must also include attribution next to the data:

> Data provided by Discogs.

The attribution must link to the relevant `discogs.com` source page for the
candidate data. Candidate source links must be ordinary links and must not be
hidden behind tracking, blocking, or link-credit suppression behavior.

DiscWeave must not name any app, feature, or integration in a way that implies
Discogs partnership, sponsorship, endorsement, or ownership. Use phrasing such
as "Discogs-assisted autocomplete", "Search Discogs candidates", or "Update via
Discogs" only as descriptive integration copy.

Discogs may apply rate limits and may change API availability or data fields.
DiscWeave must treat Discogs as an optional external provider: when Discogs is
disabled, unavailable, timed out, or rate-limited, ordinary catalog create,
edit, search, import, export, and restore workflows must continue to work.

## Implementation Consequences

Roadmap 25 can define hosted and local credential setup without exposing
secrets to the browser.

Roadmap 26 can add a backend provider abstraction and Discogs client behind
configuration, timeout, cancellation, and deterministic error handling.

Roadmap 27 can add optional external source provenance without making Discogs
identifiers mandatory.

Roadmap 28 through Roadmap 33 can add release, artist, and release-backed track
candidate APIs and UI review flows while preserving explicit user review before
data is applied.

Roadmap 34 can harden provider limits, logging, redaction, and offline tests.

Roadmap 35 remains a future decision for user Discogs OAuth and collection
import. It must not be implemented as part of autocomplete.
