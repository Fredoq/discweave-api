# API Endpoint Structure

Cratebase uses ASP.NET Core Minimal APIs with vertical feature slices. Endpoint code should stay close to request and response contracts, but no resource should be hidden inside a large umbrella route class.

## Options Considered

Endpoint-per-operation classes keep files very small, but add ceremony before the API has enough workflow complexity to justify it.

One grouped class per resource keeps the route table, handlers, validation, and mapping easy to read while preserving Minimal API simplicity.

Large module classes such as `CoreCatalogEndpointRouteBuilderExtensions` make route discovery quick at first, but they mix unrelated resources and become difficult to review, test, and extend.

Mediator-style command and query dispatch was rejected for now because the repository explicitly avoids MediatR and the current CRUD slices do not need an extra in-process pipeline.

## Chosen Rule

Use one resource slice per API resource:

- `Features/Artists`
- `Features/Labels`
- `Features/Tracks`
- `Features/Releases`
- `Features/OwnedItems`
- `Features/Settings`

Each slice owns its Minimal API route builder extension, endpoint handlers, contracts, and resource-specific mapping. Shared HTTP helpers are allowed only for cross-cutting behavior that is already duplicated, such as structured errors, pagination, delete confirmation, and persistence conflict detection.

The root `MapCratebaseEndpoints()` method composes resource slices directly. It must not delegate to broad umbrella route builders that mix unrelated resources.

## Settings Dictionaries

Collection-scoped dictionaries are exposed under `/api/settings/dictionaries`. The settings slice owns these endpoints because dictionary data configures catalog behavior but is not itself a catalog entity.

Supported operations:

- `GET /api/settings/dictionaries` lists all dictionary entries for the authenticated user's default collection, with optional `kind` filtering.
- `POST /api/settings/dictionaries` creates a custom entry with an immutable code.
- `PUT /api/settings/dictionaries/{entryId}` updates mutable fields: name, sort order, active state, and media profile.
- `DELETE /api/settings/dictionaries/{entryId}` hard-deletes only unused, unprotected entries and requires delete confirmation.
- `POST /api/settings/dictionaries/{entryId}/replace` transactionally rewrites usages to another entry of the same kind before deletion.

Catalog endpoints continue to accept and return string codes. They resolve those codes against the active collection's dictionaries during writes and filters. New writes require active entries; inactive entries stay readable so historical catalog data does not disappear.

## Size Rule

Manually maintained C# files must stay at or below 300 lines. Generated files and EF Core migrations are excluded from this limit.

The repository enforces this with an architecture test. Because the project uses one top-level type per file, the file limit is the practical class-size rule.
