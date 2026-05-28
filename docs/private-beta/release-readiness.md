# Hosted Private Beta Release Readiness

Roadmap 23 turns the completed v1 roadmap slice into a repeatable hosted
release path and a private beta evidence loop. It does not launch production by
itself.

## Release Checklist

- API image is built from the intended commit.
- Web image is built from the matching `cratebase-web` commit.
- EF Core migrations are applied as an explicit release step.
- Managed PostgreSQL backup exists before migrations.
- Release cover storage and desktop artifact storage are persistent.
- Hosted backup and restore drill has been run for the release candidate.
- Hosted security baseline is enabled and verified.
- Browser origin routes `/api/*`, `/health`, `/web-health` and web fallback
  through the reverse proxy.
- macOS desktop DMG is built, published to service storage and points at the
  private beta origin or a documented `CRATEBASE_API_BASE_URL` override.
- API, web, import, export, restore, search, playlist and catalog quality
  acceptance checks pass in staging.
- Data-handling and onboarding trust copy is visible before users import real
  collection data.
- Support contact and feedback capture process are ready.

## Private Beta Feedback Checklist

Capture one issue or note per finding. Useful evidence includes:

- import success rate and draft cleanup effort;
- search usefulness for artists, labels, tags, roles and ownership states;
- manual entry speed for incomplete or rare releases;
- export trust and whether JSON/CSV output is understandable;
- onboarding clarity around hosted accounts, desktop import and no audio upload;
- recovery confidence after reading export and hosted backup expectations;
- confusing validation messages, missing copy or unsafe destructive operations.

## Go/No-Go Rule

Do not invite users with valuable real collections until staging proves:

- account access and disabled-user behavior;
- collection isolation;
- desktop import without audio upload;
- export and empty-collection JSON restore;
- hosted backup/restore drill;
- hosted security baseline;
- data-handling trust copy.

Post-v1 roadmap decisions should be based on observed usage from the private
beta, not speculative integrations or player/social features.
