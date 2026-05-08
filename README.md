# cratebase-api

Backend API for Cratebase, a personal music archive for cataloging releases, tracks, media, owned items, credits, artist relations, imports, search, and exports.

Cratebase is an archive, not a music player. The API is shaped around the collection domain and the question: what is in the collection, and how is it connected?

## Requirements

- .NET SDK 10.0.100 or newer 10.0 feature band

The repository pins the SDK in `global.json`.

## Solution Layout

- `src/Cratebase.Domain` - reserved for domain entities, value objects, enums, and invariants after the domain model is designed.
- `src/Cratebase.Application` - application services and use-case orchestration.
- `src/Cratebase.Infrastructure` - persistence, imports, exports, and external infrastructure adapters.
- `src/Cratebase.Api` - ASP.NET Core composition root and HTTP endpoints.
- `tests/Cratebase.Domain.Tests` - domain behavior tests.
- `tests/Cratebase.Api.Tests` - API smoke and contract tests.

## Local Development

Restore dependencies:

```bash
dotnet restore Cratebase.slnx
```

Build the solution:

```bash
dotnet build Cratebase.slnx
```

Run tests:

```bash
dotnet test Cratebase.slnx
```

Verify formatting and analyzer style:

```bash
dotnet format Cratebase.slnx --verify-no-changes --verbosity diagnostic
```

Run the API:

```bash
dotnet run --project src/Cratebase.Api/Cratebase.Api.csproj
```

Health check:

```http
GET /health
```

Expected response:

```json
{
  "service": "cratebase-api",
  "status": "ok"
}
```

## Build Configuration

Shared build settings live in `Directory.Build.props`. Package versions are centralized in `Directory.Packages.props`.

Style rules live in `.editorconfig` and are enforced by the `Check` GitHub Actions workflow through `dotnet format`.

## CI Workflows

- `Check` verifies formatting and style.
- `Build` restores and builds the solution in Release configuration.
- `Test` restores, builds, and runs the xUnit test projects.
- `SonarQube` runs SonarQube Cloud analysis for `Fredoq_cratebase-api`.

## Engineering Notes

- Keep domain behavior independent of persistence and HTTP concerns.
- Keep owned items separate from releases and media formats.
- Model credits and relations explicitly so role-based search remains first-class.
- Do not add Redis, RabbitMQ, background workers, or external catalog integrations until a concrete product scenario requires them.
- Use explicit mapping and local validation code. Do not use AutoMapper, MediatR, Moq, or FluentValidation.
