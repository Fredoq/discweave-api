# Desktop Import API Boundary

Roadmap 14 defines the hosted API boundary for local folder scans produced by
the macOS desktop app. The browser web app may review import sessions, but it
must not browse arbitrary local folders. The desktop app performs native folder
selection and local filesystem walking. In full-scan mode it also extracts audio
metadata, reads cover candidates, and calculates SHA-256 hashes before
submitting metadata to the authenticated hosted API. In names-only mode it
submits paths, file names, file sizes, and timestamps without opening audio or
cover files, so cloud-only folders can be reviewed without downloading file
contents.

The backend remains the owner of collection scope, import pattern parsing,
release grouping, review sessions, duplicate matching, confirmation, and final
catalog persistence.

## Endpoint

```http
POST /api/imports/desktop-folder-scans
```

The endpoint requires the authenticated collection-member cookie. The active
collection is resolved from the signed-in user's default collection. Clients
must not send `collectionId`, and normal import responses must not expose it.

Successful submissions return `201 Created` with the persisted
`ReleaseImportSession` detail response. The `Location` header points to
`/api/imports/{sessionId}`.

## Request Contract

```json
{
  "sourceRoot": "/Users/example/Music",
  "ignoredFileCount": 0,
  "files": [
    {
      "filePath": "/Users/example/Music/Release/01 Track.flac",
      "relativePath": "Release/01 Track.flac",
      "format": "flac",
      "sizeBytes": 12345678,
      "lastModifiedAt": "2026-05-16T12:00:00Z",
      "contentHash": "70bc8f4b72a86921468bf8e8441dce51d8c6cb7d792fa7bbcb0d4d9eba328b75",
      "audioMetadata": {
        "title": "Track",
        "artists": ["Artist"],
        "albumTitle": "Release",
        "albumArtists": ["Artist"],
        "catalogNumber": "CAT-001",
        "releaseDate": "2026-05-16",
        "year": 2026,
        "durationSeconds": 321,
        "trackNumber": 1
      },
      "coverArtifact": null
    }
  ]
}
```

`sourceRoot` is the selected local folder root. `filePath` is the absolute local
path used for inventory and fallback duplicate matching. `relativePath` is the
path relative to `sourceRoot` and is used for grouping releases and tracks.

Supported audio formats are `flac`, `mp3`, `wav`, `ogg`, and `m4a`. Full-scan
desktop clients should send a SHA-256 `contentHash` for every supported audio
file. Names-only scans send `contentHash: null` and `audioMetadata: null`. The
backend accepts a missing hash, records a warning issue with code
`release_import.content_hash_missing`, and falls back to duplicate matching by
path, file size, and last modified time.

Cover images may be submitted only as import review artifacts, not as audio
files. Supported cover extensions are `.jpg`, `.jpeg`, `.png`, and `.webp`.
The backend accepts a selected cover artifact up to 10 MiB and stores it on the
import draft for review and later confirmation. Oversized cover candidates are
kept as paths with a warning instead of attached content. Names-only cover
candidates are kept as paths without attached content.

## Response Contract

The response uses the existing import session detail shape:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "sourceRoot": "/Users/example/Music",
  "status": "readyForReview",
  "draftCount": 1,
  "trackCount": 1,
  "ignoredFileCount": 0,
  "createdAt": "2026-05-16T12:00:00Z",
  "updatedAt": "2026-05-16T12:00:00Z",
  "drafts": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "sourcePath": "/Users/example/Music/Release",
      "relativePath": "Release",
      "status": "ready",
      "title": "Release",
      "type": "unknown",
      "catalogNumber": "CAT-001",
      "labelName": null,
      "releaseDate": "2026-05-16",
      "year": 2026,
      "isVariousArtists": false,
      "notOnLabel": false,
      "artistNames": ["Artist"],
      "artistCredits": [
        {
          "artistId": null,
          "name": "Artist",
          "role": "mainArtist"
        }
      ],
      "selectedArtistIds": [],
      "artistSuggestions": [],
      "labels": [],
      "genres": [],
      "tags": [],
      "coverPath": null,
      "issues": [],
      "tracks": [
        {
          "id": "00000000-0000-0000-0000-000000000002",
          "filePath": "/Users/example/Music/Release/01 Track.flac",
          "relativePath": "01 Track.flac",
          "format": "flac",
          "sizeBytes": 12345678,
          "lastModifiedAt": "2026-05-16T12:00:00Z",
          "durationSeconds": 321,
          "position": 1,
          "title": "Track",
          "artistNames": ["Artist"],
          "artistCredits": [
            {
              "artistId": null,
              "name": "Artist",
              "role": "mainArtist"
            }
          ],
          "artistSuggestions": [],
          "trackSuggestions": [],
          "isSkipped": false,
          "selectedTrackId": null,
          "selectedArtistIds": [],
          "issues": []
        }
      ]
    }
  ]
}
```

Import issue severities are `info`, `warning`, or `error`. Error severity keeps
the affected draft in `needsReview`; warning severity preserves a reviewable
draft while surfacing data quality or duplicate-detection concerns.

## Deduplication

Duplicate matching is scoped to the authenticated user's active collection.
The backend never matches files globally across users or collections.

Matching order:

1. Normalized SHA-256 `contentHash`.
2. Fallback fingerprint: `filePath + sizeBytes + lastModifiedAt`.

When a scan matches an existing owned digital track, the draft track receives
`selectedTrackId` and a `release_import.duplicate_file` warning. Confirming a
fully duplicate draft is a no-op for existing release, track, and owned item
rows. Confirming a partial duplicate reuses a same-title release only when the
existing release tracklist is safely represented by selected duplicate tracks in
the reviewed draft; the backend then appends only missing tracks and owned
digital items. Repeating the same scan must not create duplicate catalog data.

## No Audio Uploads

The v1 API stores metadata, local paths for inventory, hashes, file size,
timestamps, review issues, and optional cover artifacts. It must not receive,
store, proxy, stream, or serve third-party audio file bytes.

Roadmap 15 should keep folder selection and scanning behind the Electron
main/preload boundary. Roadmap 16 can build on this contract for deeper import
review, grouping, confirmation, and deduplication behavior without reopening
the desktop/browser filesystem boundary.
