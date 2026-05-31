# Portable Export v1

Portable export v1 is the hosted private beta contract for user-owned
collection exports. It exists for portability, spreadsheet work, and personal
backups. Operator-managed database and service recovery remain a separate
hosted backup responsibility.

## Endpoints

```http
GET /api/exports/json
GET /api/exports/csv
POST /api/exports/json/restore
```

All export and restore endpoints require the authenticated collection-member
cookie. The active collection is resolved from the signed-in user's default
collection. Clients must not send `collectionId`, and export responses must
not expose it.

`GET /api/exports/json` returns `200 OK` with a JSON snapshot.
`GET /api/exports/csv` returns `200 OK`, `application/zip`, and
`discweave-export-csv.zip`. `POST /api/exports/json/restore` restores a
`formatVersion` 1 JSON snapshot into the authenticated user's empty active
collection only when the request includes:

```http
X-DiscWeave-Confirm-Restore: restore-empty-collection
```

## JSON Snapshot

The JSON snapshot is frozen as `formatVersion: 1` and uses readable catalog API
shapes rather than persistence models. The top-level sections are:

- `artists`
- `labels`
- `releases`
- `tracks`
- `ownedItems`
- `playlists`
- `credits`
- `artistRelations`
- `trackRelations`
- `dictionaries`
- `importPatterns`
- `ratingCriteria`
- `ratings`

The snapshot intentionally includes convenience read fields that help users
inspect the archive outside DiscWeave: track release appearances, owned item
targets, inventory signals, playlist results, release labels, tracklists,
credit names, tags, genres, external source provenance, and release cover
metadata. These fields are part of JSON export v1 and must remain
restore-compatible for `formatVersion: 1`.

The snapshot must not include user account data, collection ids, internal
database-only fields, import review sessions or drafts, raw cover image bytes,
cover artifact `contentBase64`, or audio file bytes.

## CSV ZIP

The CSV export is a ZIP archive of normalized tables for spreadsheet workflows.
CSV fields use UTF-8 without BOM. Values that could be interpreted as formulas
by spreadsheet tools are prefixed with `'`.

The v1 archive entries and headers are:

| File | Header |
| --- | --- |
| `artists.csv` | `id,type,name` |
| `labels.csv` | `id,name` |
| `releases.csv` | `id,title,type,label_id,year,release_date,is_various_artists,not_on_label,genres,tags,cover_image_url,cover_image_content_type,cover_image_original_file_name,cover_image_size_bytes,cover_image_source_type` |
| `release_labels.csv` | `release_id,label_id,name,catalog_number,has_no_catalog_number` |
| `release_tracklist.csv` | `release_id,track_id,position,title,duration_seconds,version_note` |
| `tracks.csv` | `id,title,duration_seconds,genres,tags` |
| `owned_items.csv` | `id,target_type,target_id,status,medium_type,medium_description,medium_path,medium_format,medium_disc_count,condition,storage_location` |
| `playlists.csv` | `id,name,type,description,rule_tags,rule_genres,rule_media,rule_ownership_statuses,rule_year_from,rule_year_to` |
| `playlist_entries.csv` | `playlist_id,position,kind,id,title` |
| `credits.csv` | `id,contributor_artist_id,contributor_name,target_type,target_id,role` |
| `artist_relations.csv` | `id,source_artist_id,target_artist_id,type,start_year,end_year` |
| `track_relations.csv` | `id,source_track_id,target_track_id,type` |
| `dictionaries.csv` | `id,kind,code,name,sort_order,is_active,is_builtin,is_protected,media_profile` |
| `import_patterns.csv` | `id,kind,template,sort_order,is_active,is_builtin` |
| `rating_criteria.csv` | `id,code,name,target_types,sort_order,is_active,is_builtin,is_protected` |
| `ratings.csv` | `id,criterion_id,target_type,target_id,value` |

Multi-value fields such as `genres`, `tags`, `target_types`, and smart
playlist rule arrays are joined with `|`.

External source provenance is JSON-only in v1. CSV exports intentionally omit
`externalSources`.

## Cover And Import Boundaries

Exports include cover metadata only: API URL, content type, original file name,
size in bytes, and source type. They do not include raw cover bytes. A restored
snapshot preserves stored cover metadata but does not recreate object storage
bytes.

Confirmed desktop imports become ordinary catalog data and are included in
exports as releases, tracks, credits, labels, owned digital items, and media
paths. Export v1 does not include import review sessions, draft issues,
desktop scan DTOs, cover artifact base64 content, audio metadata request
payloads, audio file hashes, or audio file bytes.

## Restore Boundary

JSON restore is a portability and personal backup tool. It requires an empty
active collection, preserves public ids from the snapshot, and rejects
unsupported `formatVersion` values or invalid references with structured
errors. Hosted service backup and disaster recovery remain outside export v1
and are covered by the hosted backup roadmap item.
