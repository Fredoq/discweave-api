# Search v1

Search v1 is the collection-scoped catalog search contract for the hosted
private beta. It answers archive questions across catalog entities, ownership
state, media, tags, labels, credits, and collector audit views.

## Endpoint

```http
GET /api/search
```

The endpoint requires the authenticated collection-member cookie. The active
collection is resolved from the current user. Clients must not send
`collectionId`, and normal search responses do not expose collection ids.

At least one search criterion is required.

## Request Parameters

| Parameter | Description |
| --- | --- |
| `query` | Full-text query. |
| `q` | Alias for `query`; `query` wins when both are present. |
| `entityType` | Optional constrained value: `artist`, `release`, `track`, `ownedItem`, `label`, `playlist`. |
| `role` | Optional credit or relation role filter. Open because roles can be dictionary-backed. |
| `media` | Optional media filter. Open because custom media dictionaries can add codes. |
| `status` | Optional constrained ownership status: `owned`, `wanted`, `sold`, `needsDigitization`. |
| `labelId` | Optional label GUID filter. |
| `tag` | Optional tag or genre filter. Open because users define collection tags. |
| `savedView` | Optional collector view. |
| `limit` | Page size using the shared pagination rules. |
| `offset` | Page offset using the shared pagination rules. |

Invalid constrained values return deterministic `400` errors:

- `search.entity_type_invalid`
- `search.status_invalid`
- `search.saved_view_invalid`
- `search.label_id_invalid`
- `search.limit_invalid`
- `search.offset_invalid`
- `search.criteria_required`

## Saved Views

Supported `savedView` values:

- `all`
- `credits`
- `remixes`
- `productions`
- `labels`
- `needsDigitization`
- `physicalWithoutDigital`
- `lossyWithoutLossless`
- `mp3notlossless`
- `wantedNotOwned`

Saved views can be combined with compatible filters such as `entityType`,
`status`, `media`, `role`, `labelId`, and `tag`.

## Response Shape

Responses use the shared list envelope:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "type": "release",
      "title": "Confusion",
      "subtitle": "Factory Records",
      "summary": "Electronic, factory",
      "matchedFields": ["title", "label", "credit.role"],
      "snippets": ["Confusion", "Factory Records", "producer"],
      "facets": {
        "roles": ["producer"],
        "media": ["vinyl"],
        "statuses": ["owned"],
        "tags": ["Electronic", "factory"],
        "labelId": "00000000-0000-0000-0000-000000000000",
        "collectorSignals": ["physicalWithoutDigital"]
      },
      "rank": 10.5
    }
  ],
  "limit": 20,
  "offset": 0,
  "total": 1
}
```

Result `type` values match `entityType`. Facets are per result, not aggregate
facet counts.

## Examples

```http
GET /api/search?q=arthur%20baker&limit=20&offset=0
GET /api/search?entityType=release&role=producer&limit=20&offset=0
GET /api/search?savedView=physicalWithoutDigital&limit=20&offset=0
GET /api/search?savedView=all&entityType=release&status=owned&limit=20&offset=0
GET /api/search?media=vinyl&tag=crate-dig&limit=20&offset=0
```

## Indexing And Smoke Verification

Search documents use PostgreSQL full-text search, trigram matching, and
trigram indexes on filter facet columns. To create a repeatable large
collection and run the local search smoke probes:

```sh
dotnet run --project src/Cratebase.Seeding/Cratebase.Seeding.csproj -- \
  --connection-string "<postgres>" \
  --verify-search \
  --search-budget-ms 250
```

Functional probe failures exit non-zero. Queries over the local budget print
`WARN` so release evidence can be collected without making latency a flaky CI
gate.
