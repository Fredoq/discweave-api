# AGENTS.md

## Scope

This file applies to the `cratebase-api` repository.

The root Cratebase rules still apply. In particular, everything committed to the repository must be written in English.

## Project Context

`cratebase-api` is the backend API for Cratebase, a personal music archive for cataloging albums, tracks, media, owned items, credits, artist relations, imports, search, and exports.

The API must model the collection domain directly. Do not shape the backend around current UI screens or a future music player.

Primary product question:

> What do I have in my collection, and how is it connected?

## Language and Tone

- Use English for code, identifiers, comments, XML doc comments, tests, fixtures, migrations, logs, errors, README files, documentation, issue templates, and any committed repository artifact.
- Conversation with the project owner may happen in Russian.
- Keep technical explanations concise and practical.
- Use .NET terminology precisely.

## Platform

- Target modern .NET and ASP.NET Core.
- Prefer .NET 10 and C# 14 unless the repository explicitly targets another version.
- Use idiomatic C# features when they reduce complexity.
- Prefer file-scoped namespaces.
- Prefer nullable reference types enabled.
- Prefer `required` members where they improve construction clarity.
- Prefer collection expressions where they improve readability.

## Approved Stack

Use these technologies when the feature needs them:

- ASP.NET Core for HTTP APIs.
- Entity Framework Core for relational persistence.
- xUnit for tests.
- Testcontainers for integration tests that need real infrastructure.
- Redis later, only when caching is needed.
- RabbitMQ later, only when asynchronous messaging is needed.

Do not add Redis, RabbitMQ, background workers, or message queues before a concrete MVP scenario requires them.

## Prohibited Libraries and Patterns

Do not use:

- AutoMapper or any convention-based object mapping library;
- MediatR or in-process mediator abstractions;
- MassTransit;
- Moq;
- FluentValidation.

Guidance:

- Use explicit mapping code between domain models, persistence models, and contracts.
- Call application services or vertical slice handlers directly instead of routing through a mediator package.
- Use the RabbitMQ client directly or a small local abstraction when messaging becomes necessary.
- Prefer hand-written fakes, stubs, and test doubles over mocking frameworks.
- Implement validation with explicit code, value objects, endpoint filters, or local validation components.

## Architecture

Use Clean Architecture, DDD, and Vertical Slice principles pragmatically.

The default direction of dependencies:

- Domain depends on nothing application-specific or infrastructure-specific.
- Application depends on Domain.
- Infrastructure depends on Application and Domain.
- API composition root depends on all layers and wires them together.

Vertical slices should group request handling, contracts, validation, and application behavior for a feature. Keep shared abstractions small and real.

DDD rules:

- Domain entities own business invariants.
- Value objects should be immutable.
- Aggregates should expose behavior, not public mutable state.
- Domain services are acceptable only when behavior does not naturally belong to an entity or value object.
- Persistence concerns must not leak into domain behavior.
- Do not make EF Core entities the only domain model unless the design remains clean, explicit, and testable.

## Domain Priorities

Keep these concepts explicit:

- Artist;
- Release;
- Track;
- Medium;
- Owned Item;
- Credit;
- Relation;
- Import;
- Export;
- Search.

Owned Item must stay separate from Release and Medium. A release can exist in multiple copies, formats, ownership statuses, and storage locations.

Credits and relations must support role-based search without adding one-off columns for each role.

Statuses such as owned, wanted, sold, and needs digitization must be explicit data, not inferred from file presence.

## API Design

- Keep endpoints resource-oriented and predictable.
- Use clear route names and stable request/response contracts.
- Keep API contracts separate from persistence models.
- Return structured errors.
- Do not expose stack traces or internal exception details through HTTP responses.
- Use cancellation tokens on async request paths and infrastructure calls.
- Keep validation errors deterministic and machine-readable.
- Prefer pagination for collection endpoints.
- Prefer explicit filters over broad ambiguous query parameters.

## Persistence

- Use EF Core intentionally; do not hide query composition or persistence behavior behind broad abstractions.
- Treat `DbContext` as the concrete unit of work and `DbSet` as the concrete repository implementation.
- EF Core `DbContext` types may remain unsealed when the repository/unit-of-work implementation needs cast-based generic interface dispatch.
- Command-side repository interfaces may exist only as thin EF-aware contracts: `TryFindAsync`, `Add`, and `Delete`.
- Do not create standalone generic repository implementation classes. The EF Core `DbContext` must implement supported repository interfaces directly, usually through explicit members in partial files grouped by aggregate root.
- Repository lookup must use public domain identifiers such as `ArtistId`, `ReleaseId`, and `TrackId`. It must not use infrastructure-only shadow surrogate keys.
- Use `IUnitOfWork.SaveChangesAsync` to commit command changes. Do not add a custom unit-of-work implementation separate from EF Core.
- Use named query interfaces for reusable read models and reports. Define query contracts in Application and implement them in Infrastructure with EF Core LINQ projections.
- Do not introduce a generic specification pipeline. Reusable queries should be methods with descriptive names.
- Prefer specific query/application services over broad generic query abstractions.
- Keep migrations readable.
- Do not add incremental migrations before the initial schema is stable; update the baseline migration during early schema design.
- Model constraints in the database when they represent real invariants.
- Avoid lazy loading by default.
- Avoid N+1 queries; use projections and explicit includes where appropriate.
- Use optimistic concurrency where user-owned mutable data needs conflict protection.

## Mapping

- Mapping must be explicit and readable.
- Avoid reflection-based or convention-based mapping.
- Keep mapping close to the feature or boundary where it is used.
- Do not introduce shared mapping layers before duplication is real and harmful.

## Validation

- Do not use FluentValidation.
- Prefer validation where the invariant belongs:
  - value objects for domain invariants;
  - endpoint/request validation for input shape;
  - application services for workflow rules;
  - database constraints for persisted uniqueness and referential integrity.
- Validation messages committed to the repository must be in English.

## Messaging and Caching

Redis and RabbitMQ are not part of the first implementation unless a task explicitly requires them.

When Redis is introduced:

- cache only data with clear invalidation rules;
- use short, explicit key formats;
- avoid caching domain objects directly if contracts are more stable;
- tests must cover cache miss and cache invalidation behavior.

When RabbitMQ is introduced:

- define message contracts explicitly;
- design idempotent consumers;
- include retry and dead-letter behavior intentionally;
- do not use MassTransit.

## Testing

- Use xUnit.
- Use Testcontainers for integration tests that need a real database, Redis, RabbitMQ, or other infrastructure.
- Prefer behavior-focused tests over implementation-detail tests.
- Every test must assert at least once.
- Test names must be full English sentences describing the expected behavior.
- Tests must be deterministic and independent.
- Avoid hidden shared state between test cases.
- Prefer explicit setup in the test body unless a fixture makes the test clearer.
- Prefer fakes and stubs over mocks.
- Do not use Moq.
- Tests must not depend on Internet access unless the test explicitly targets network integration.
- Tests that wait for async work must use bounded timeouts.
- Tests should create temporary files in temporary directories, not inside the repository.
- Integration tests must cleanly dispose containers and external resources.
- Do not assert on full human-readable error messages when a stronger contract exists, such as status code, error code, or structured payload.

## C# Design Rules

- Classes and records should be `sealed` by default.
- Put exactly one top-level type in each `.cs` file. This applies to classes, records, structs, interfaces, and enums. Name the file after that type.
- Prefer immutability for value objects and contracts.
- Private instance fields must use `_camelCase`, including EF Core backing fields. Do not use bare camelCase private fields.
- Use `Guid.CreateVersion7()` for newly generated GUID values, including typed IDs. Do not use `Guid.NewGuid()` for domain identifiers.
- Use `class` for objects with identity, lifecycle, or behavior-heavy invariants.
- Use `record` or `record struct` only when value semantics are intentional.
- Do not use primary constructors for classes or records unless the repository explicitly adopts them later.
- Keep public APIs small.
- Avoid public mutable setters outside persistence models and serialization contracts.
- Domain models must not expose nullable public properties, nullable parameters, optional parameters with `null` defaults, or `null` sentinel values.
- Represent optional domain data with explicit value objects such as `OptionalValue<T>`, and represent alternatives with distinct subtypes instead of paired nullable identifiers.
- C# `enum` is allowed for simple closed domain choices with no variant-specific state or behavior.
- Use object models for domain choices when variants need behavior, state, invariants, or richer identity.
- Domain choices must not be represented by public string codes or descriptions, and must not expose open factories such as `FromCode` or `FromDescription`.
- Avoid static methods for business logic.
- Prefer composition over inheritance.
- Avoid reflection for domain behavior.
- Avoid type introspection and casts in domain logic.
- Methods should not return `null`; use exceptions, empty collections, `OptionalValue<T>`, or explicit result objects.
- Do not pass `null` as a valid argument.
- Error and log messages should be single English sentences and should not end with a period.

## Comments and Documentation

- Add comments only when they explain non-obvious intent, trade-offs, or constraints.
- Do not narrate obvious code.
- Public XML documentation is acceptable for public APIs when it clarifies purpose.
- XML documentation must be brief and in English.
- Keep README short and focused on repository purpose, setup, test commands, and contribution basics.
- Store architecture notes and diagrams under `docs/arch` when they become useful.
- Use Mermaid for diagrams where possible.
- Keep the domain model diagram at `docs/arch/domain-model.md`.
- When changing domain entities, value objects, typed IDs, capability interfaces, or domain relationships, update `docs/arch/domain-model.md` in the same pull request.

## Open Source Process

- Default license: MIT unless stated otherwise.
- Versioning: SemVer.
- Changelog format: Keep a Changelog.
- Commit style: Conventional Commits.
- Default branch: `main`.
- Working branches should use `feat/*`, `fix/*`, `docs/*`, `test/*`, or `chore/*`.
- `main` should be protected.
- Prefer pull requests even for solo development.
- Do not create commits or push branches unless the user explicitly asks.
- By default, only the user performs commits and pushes.

## Agent Development Workflow

All development work must happen on a branch other than `main`.

Default agent workflow:

1. Create or switch to a working branch before changing repository files.
2. Use branch prefixes that match the work type: `feat/*`, `fix/*`, `docs/*`, `test/*`, or `chore/*`.
3. Implement the planned change on that branch.
4. Open a GitHub pull request for the branch when the work is ready for remote review.
5. Inspect and resolve all actionable CodeRabbit comments.
6. Inspect and resolve all actionable SonarQube or SonarCloud findings.
7. Re-run or wait for all required GitHub checks.
8. Hand the task back to the project owner only after the pull request is green and all required checks have completed successfully.

If GitHub access, push permissions, or PR creation is unavailable, state the blocker clearly and keep the local branch ready for the project owner.

## Before Implementation

Before making code changes:

1. Read the relevant existing code and local conventions.
2. Check the current branch and repository status.
3. Make the smallest change that satisfies the task.
4. Keep unrelated refactoring out of scope.
5. Add or update tests when behavior changes.

Before claiming work is complete:

1. Run the relevant build/test/format checks.
2. Read the command output.
3. Report what passed and what was not run.

## Handling Incomplete Input

Ask at most one or two clarifying questions, and only when proceeding without the answer would be risky.

If a safe and reasonable assumption can be made, state it and continue.
