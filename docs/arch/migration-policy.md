# Migration Policy

DiscWeave treats collection data as durable product data. Database migrations are append-only by default and must preserve the ability to upgrade an existing archive without rewriting history.

## Rules

- Add new EF Core migrations for schema changes.
- Rewrite the baseline migration only with explicit project-owner approval for a deliberate schema reset.
- Keep migrations readable and reviewable.
- Back up collection data before applying migrations that affect catalog, ownership, import, search, export, settings, playlists, ratings, or authentication data.
- Prefer reversible operational procedures for risky changes: export JSON first, apply migration, verify acceptance checks, then keep the backup until the upgraded archive is confirmed.

## Rollback Expectations

Rollback is an operational decision, not an automatic promise. If a migration cannot safely roll back through EF Core alone, document the recovery path in the same change, usually by restoring from a JSON export or database backup.
