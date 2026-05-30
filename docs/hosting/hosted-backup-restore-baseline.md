# Hosted Backup And Restore Baseline

DiscWeave private beta recovery is an operator-managed service responsibility.
User exports through JSON and CSV remain portability tools and personal
backups, but they are not the only hosted recovery path.

## Backup Scope

Hosted backups must include:

- managed PostgreSQL data for accounts, invites, catalog records, imports,
  search documents, playlists, ratings, settings and export/restore state;
- release covers stored under `ReleaseCovers__StorageRoot`;
- desktop artifacts stored under `DesktopDownloads__MacOsInstallerPath` or the
  containing service-storage directory;
- deployment configuration needed to reconnect API, web, database and storage
  in the same environment.

Backups must not include third-party audio files because DiscWeave v1 does not
upload or store audio files.

## Retention

Production should keep daily managed PostgreSQL backups for at least 14 days
and retain the most recent known-good pre-migration backup until the hosted
acceptance path passes after a release. Staging may use shorter retention, but
must keep enough history to rehearse restore drills before production rollout.

Service storage for release covers and desktop artifacts should be snapshotted
on the same cadence as the database or copied into a versioned backup location.
Database and service-storage backups must be treated as one recovery set.

## Environment Separation

Staging and production must not share PostgreSQL databases, service storage,
secrets, invite data, user accounts or backup destinations. A restore drill
must restore into an isolated target environment, never directly over
production.

## Restore Drill

Run the local compose restore drill from the API repository:

```bash
bash deploy/hosted-restore-drill.sh
```

The drill creates a PostgreSQL dump, archives service storage, restores both
into an isolated compose project and verifies the restored database can answer
basic catalog/auth queries. It is release evidence, not a hosted provider
replacement.

For managed production providers, the equivalent drill is:

1. create a point-in-time PostgreSQL restore into a staging target;
2. restore the matching release cover and desktop artifact storage snapshot;
3. deploy the current API and web images against the restored target;
4. run the hosted acceptance checklist, including login, import review, export,
   cover metadata and search;
5. record the drill date, backup source, restore target and any gaps.

## User-Facing Recovery Expectations

Private beta users can rely on JSON/CSV exports for portability and personal
backup workflows. Hosted disaster recovery is operated separately by the
service owner and covers the database plus service-owned persisted artifacts.
The private beta should state that recovery objectives are best-effort until
the production provider and final retention policy are selected.
