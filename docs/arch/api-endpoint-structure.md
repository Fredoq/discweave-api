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

Each slice owns its Minimal API route builder extension, endpoint handlers, contracts, and resource-specific mapping. Shared HTTP helpers are allowed only for cross-cutting behavior that is already duplicated, such as structured errors, pagination, delete confirmation, and persistence conflict detection.

The root `MapCratebaseEndpoints()` method composes resource slices directly. It must not delegate to broad umbrella route builders that mix unrelated resources.

## Size Rule

Manually maintained C# files must stay at or below 300 lines. Generated files and EF Core migrations are excluded from this limit.

The repository enforces this with an architecture test. Because the project uses one top-level type per file, the file limit is the practical class-size rule.
