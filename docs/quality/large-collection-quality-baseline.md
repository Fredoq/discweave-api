# Large Collection Quality Baseline

Roadmap 19 defines a read-only quality report, explicit confirmation for
selected destructive operations, and a local performance smoke mode for large
collections. These checks are cleanup guidance and release evidence. They do
not merge, delete, or rewrite catalog data automatically.

## Catalog Quality Report

```http
GET /api/catalog-quality
GET /api/catalog-quality?limit=50
```

The endpoint requires the authenticated collection-member cookie. The active
collection is resolved from the signed-in user's default collection. Clients
must not send `collectionId`, and the response must not expose it.

`limit` is optional, applies independently to every report section, defaults
to `25`, and has a maximum of `100`. Invalid limits return:

```json
{
  "code": "catalog_quality.limit_invalid",
  "message": "Catalog quality limit must be between 1 and 100"
}
```

The report sections are:

- `duplicateCandidates.releases` - same-title release cleanup candidates.
- `duplicateCandidates.tracks` - same-title track cleanup candidates.
- `duplicateCandidates.digitalFileIdentities` - repeated digital file
  identities, using content hash when available and path as the fallback key.
- `missingMetadata.releasesMissingYearOrDate` - releases without year and
  release date.
- `missingMetadata.releasesMissingLabel` - releases without release labels,
  summary label, or explicit not-on-label marker.
- `missingMetadata.tracksMissingDuration` - tracks without duration.
- `missingMetadata.ownedItemsMissingCondition` - owned items without condition.
- `missingMetadata.ownedItemsMissingStorageLocation` - owned items without
  storage location.
- `missingMetadata.ownedItemsMissingDigitalFormat` - digital owned items
  without an audio format.
- `formatGaps.physicalWithoutDigital` - release or track targets with physical
  holdings and no digital holding.
- `formatGaps.lossyWithoutLossless` - digital targets with lossy formats and no
  lossless format.
- `formatGaps.wantedNotOwned` - targets that are wanted and not owned.
- `formatGaps.needsDigitization` - targets with `needsDigitization` ownership
  status.

Duplicate candidates are not merge instructions. They identify rows that a
collector may review manually.

## Destructive Confirmation Tokens

Delete endpoints that remove catalog or settings data require
`X-DiscWeave-Confirm-Delete`. Missing or mismatched tokens return
`delete.confirmation_required`.

Roadmap 19 adds explicit confirmation to:

- `DELETE /api/ratings/{targetType}/{targetId}/{criterionId}` with
  `rating:{targetType}:{targetId}:{criterionId}`.
- `DELETE /api/settings/import-patterns/{patternId}` with
  `import-pattern:{patternId}`.

These confirmations are exact string matches. They are scoped by route values
and do not authorize cross-collection access.

## Performance Smoke Mode

`DiscWeave.Seeding` can run representative large-collection probes after
seeding:

```bash
dotnet run --project src/DiscWeave.Seeding/DiscWeave.Seeding.csproj -- \
  --connection-string "<postgres>" \
  --verify-performance \
  --performance-budget-ms 250
```

The probes cover:

- release list;
- search;
- relations;
- playlists;
- import deduplication lookup;
- export read.

Each probe prints `PASS` when it completes within the warning budget and
`WARN` when it exceeds the budget. Budget warnings do not fail the process.
Functional failures, such as a probe returning no representative data, print
`FAIL` and exit non-zero through an exception.

The existing search-specific mode remains available:

```bash
dotnet run --project src/DiscWeave.Seeding/DiscWeave.Seeding.csproj -- \
  --connection-string "<postgres>" \
  --verify-search \
  --search-budget-ms 250
```
