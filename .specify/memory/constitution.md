# Underwriting Constitution
<!-- Sync Impact Report — 1.1.0 → 1.2.0
Version bump: MINOR (materially expanded guidance in two existing principles, one existing
Quality Gates bullet amended with a carve-out, one new Quality Gates bullet added — no
principle renamed, removed, or redefined incompatibly).
Modified principles:
  - III. The Spec Is the Source of Truth — added guidance on verifying a read model's
    dependency-edge gap against spec.md's literal board export before treating it as a bug
    vs. an intentional, board-consistent limitation.
  - XI. AI-Assisted Development Rules — added a bullet: confirm with the user before
    widening an already-shipped feature's contract for a later feature's sake.
Modified sections:
  - Quality Gates & Workflow — added a full-suite-required exception to the existing
    per-commit testing bullet, for changes touching a pre-existing shared event/aggregate;
    added a new bullet on docs/adr/ numbering continuity.
Added sections: none (no new Principle or top-level section — all changes are additions
  within existing principles/sections).
Removed sections: none.
Follow-up TODOs: none.
Source: process learnings from completing 001-authority-administration (all tasks done,
  2026-08-10) — see that feature's tasks.md/commit history for the concrete incidents each
  addition generalizes from.
-->
<!-- Adapted from the sibling "Powergym" project's constitution (2026-08-09) — both projects
share the same build-kit-dotnet-es (this repo's own eventmodelers .NET build kit) as their
architecture/quality source; see build-kit-dotnet-es/README.md, .claude/skills/build-{state-change,
state-view,automation}/SKILL.md, and quality-checks.md for the full source material these
principles are distilled from. These are architecture/quality principles only — build-kit-dotnet-es's
own Ralph autonomous-loop tooling (ralph-claude.js, orchestrate.mjs, lib/backend-prompt.md,
tasks.json/progress.txt) is out of scope for this project: implementation happens through Claude
Code against the specs in specs/, not the Ralph loop. Principles VI–XI and the Technology
Constraints section were folded in from a personal greenfield Spec Kit constitution template
(modeled after barretb/BarretApi) to close gaps this constitution didn't originally cover. Content
is unchanged from Powergym's constitution except for this project's name and the version/date
fields below — nothing here is Powergym-specific; it's the shared build-kit-dotnet-es contract. -->

## Core Principles

### I. Event-Sourced Everywhere (NON-NEGOTIABLE)
Every domain entity is a self-aggregating event stream — a `Create`/`Apply` pair Marten discovers by convention via `FetchForWriting`/`AggregateStreamAsync`. There is no per-module or per-slice choice between a document store and an event stream; this project has exactly one storage strategy. `[JsonInclude]`/`[JsonConstructor]` are required on every entity whose constructor/setters are restricted — without them the entity serializes fine on write and throws `NotSupportedException` on the first real read. Entities never guard their own preconditions (`Apply` mutates unconditionally); precondition checks belong in the handler that calls them.

### II. Vertical Slices, Three Shapes Only
Every feature is exactly one of: a **state-change** slice (`[WolverinePost]`/`[WolverinePut]` request → validate → append event(s) → response), a **state-view** slice (a read model query, plain Marten document), or an **automation** (`EVENT(s) → AUTOMATION → COMMAND/EVENT(s)`, triggered by an event, never by a route). A state-change handler decides only "is this request valid" — never a further consequence. If a slice's own description implies a downstream effect beyond its own event, that effect is a separate automation triggered by the event just emitted, not inlined into the command handler.

### III. The Spec Is the Source of Truth
The board slice / spec definition (fields, events, given/when/then scenarios) is authoritative; code follows it, never the reverse. No invented fields, no guessed names, no business rules or defaults beyond what the spec states. A slice is not "done" because it compiles — it is done when every field, every event, and every specification in the source spec has a corresponding, matching element in code, with nothing extra and nothing missing. When a read model's declared dependency edge looks wrong or incomplete, verify against `spec.md`'s literal Event Model Detail appendix (the verbatim board export) before concluding anything — not just `data-model.md`'s prose summary, which can drift from the board itself. A dependency edge that's *consistently* absent across the board's own field data is the board's actual, intentional scope (implement as specified, flag the limitation in a comment); a dependency edge the board declares but that has no field to route by is a genuine defect worth fixing (as `001-authority-administration` did twice: `AuthorityMatrix`'s missing event edges, and `AuthorityLimitRevoked`/`CellAuthorityIncreaseRequested` missing the identifier a later read model needed). Telling these two cases apart requires reading the actual board export, not inferring from a summary.

### IV. Test-First, Three Layers (NON-NEGOTIABLE)
Tests are written before, or alongside, the handler they describe — never after. **Layer 1** (domain): xUnit + Shouldly, no mocks, calling factory/domain methods directly. **Layer 2** (handler): xUnit + Shouldly + NSubstitute, mocking `IDocumentSession` where that's tractable. **Layer 3** (Testcontainers): a real Postgres-backed Marten store, used whenever Layer 2 mocking of `FetchForWriting`/`AggregateStreamAsync` becomes more trouble than it's worth — which is most event-sourced handlers. Every scenario in a slice's specifications gets at least one executable test; a specification with no equivalent test is a gap, not a nice-to-have. Shouldly and NSubstitute (both MIT) are a deliberate choice, not a default — they avoid Moq's 2024 SponsorLink trust incident and FluentAssertions' 2025 move to a paid license for commercial use from v8 onward; re-check license terms before adding any other test-tooling dependency.

### V. Discoverable by Convention — and Convention Fails Silently
Wolverine discovers handlers, projectors, and automations by naming convention alone: **the class name must end in `Handler`**, or a correctly-written `Handle` method is silently never registered — no exception, no log line, just "nothing happened." This is the single highest-value thing to double check on every new handler. The same silent-failure shape applies elsewhere in this stack (a trigger event never subscribed via `SubscribeToEvent<T>`, a consuming module missing its `IntegrationEventQueueName`) — when something *should* have happened and didn't, check registration/wiring before assuming the logic is wrong.

### VI. Async Correctness (NON-NEGOTIABLE)
`async`/`await` propagates all the way up every call chain touching a Wolverine handler, Marten call, or HTTP endpoint — no `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on an async call from request-handling code; under a captured `SynchronizationContext` these deadlock, and even without one they block a thread-pool thread for nothing. Every public async method accepts and forwards a `CancellationToken` through every downstream `FetchForWriting`/`AggregateStreamAsync`/`SaveChangesAsync`/HTTP call it makes. No fire-and-forget (`_ = SomeAsync()`) inside a handler; work that must run detached from the request goes through a hosted service or a durable Wolverine queue instead, so failures are observed and logged rather than silently dropped. Default to `Task`; `ValueTask` is reserved for a hot path a profiler has actually flagged, never awaited or stored twice.

### VII. API Contract & Surface
Wolverine.Http endpoints return DTOs — the request/response records already required by `[WolverinePost]`/`[WolverinePut]` — never the event-sourced entity itself; returning the aggregate leaks internal shape and couples the wire contract to however `Apply` happens to structure state today. HTTP verbs are used by actual semantics (`GET` safe/idempotent, `POST` create/non-idempotent, `PUT` full idempotent replace, `PATCH` partial, `DELETE` remove), not framework convenience. All error responses are `ProblemDetails`/`ValidationProblemDetails` — already wired for validation failures and for `409` conflicts (Architecture Constraints) — no endpoint invents its own ad-hoc error shape, and no stack trace or exception message reaches a client outside local development. List endpoints are paginated from their first version. Public API versioning uses one consistent convention, decided before the first public endpoint ships — distinct from the `V1`/`V2` integration-event suffixing in Architecture Constraints, which versions events, not the HTTP surface.

### VIII. Security by Default
Secrets (connection strings, RabbitMQ/Postgres credentials, API keys) never appear in source control or `appsettings.json` — user secrets in development, environment variables or a managed secret store in every other environment, configured from the first commit that needs one. `UseAuthentication()` runs before `UseAuthorization()` in the middleware pipeline. Every state-mutating endpoint requires an explicit authorization check; `[AllowAnonymous]` carries a comment justifying the exception, deny-by-default otherwise — in addition to, not a replacement for, the ownership check already required in Architecture Constraints. Exceptions on an authn/authz path are never silently swallowed — log via `ILogger` before re-throwing or redirecting. CI runs SAST and secrets scanning alongside the existing dependency-vulnerability check (Quality Gates & Workflow), gated on High/Critical severity findings, on every PR.

### IX. Performance Discipline
Defaults are the plain option — `Task` over `ValueTask`, ordinary collections over `Span<T>`/`ArrayPool<T>`/object pooling, `Inline` snapshots over `Async` (Architecture Constraints) unless a specific reason says otherwise. Reach for the low-allocation toolkit, an extra snapshot, or a denormalized read model only where profiling (`dotnet-trace`, `dotnet-counters`, or BenchmarkDotNet) has already identified a real, measured hot path — never preemptively on the assumption it might matter. A source generator is adopted only if it eliminates a real runtime cost or enables something otherwise impossible, and only if it implements `IIncrementalGenerator` rather than the legacy non-incremental `ISourceGenerator`.

### X. Observability & Structured Logging
All logging uses structured message templates (`_logger.LogInformation("Slice {SliceId} completed", sliceId)`), never string interpolation, wired in from the first `Program.cs`. A correlation ID is generated once at the HTTP entry point, propagated through every downstream Wolverine/Marten/RabbitMQ call, and included in every log line and audit event for that request. No `catch { }` block with an empty body — a genuinely ignorable exception still carries an inline comment explaining why. Logging exports through OpenTelemetry (`ILogger` + OTel exporters), not a bespoke sink. A health check endpoint, separate from business endpoints, exists before the first deploy to any shared environment.

### XI. AI-Assisted Development Rules
Any AI coding agent working in this repository (Claude Code, via Spec Kit or otherwise) treats this constitution as binding, alongside `build-kit-dotnet-es/AGENT.md` (Governance). Specifically, an agent:
- Does not change Marten's schema-auto-creation/`AutoCreate` configuration, or otherwise alter how schema is managed, without explicit review — Marten auto-manages schema (Architecture Constraints), so there is no migration file to review, which makes an unreviewed change to that behavior riskier, not safer.
- Does not push commits, open pull requests, or merge without explicit confirmation for that specific action.
- Follows the architecture in Principles I–II; a change that needs to violate them requires a documented rationale, not a silent shortcut.
- Writes or updates tests for any new business logic in the same change, per Principle IV — never as a deferred follow-up.
- Flags, rather than silently resolves, any conflict it finds between this constitution, the source spec (Principle III), and existing code.
- Confirms with the user before widening an already-shipped feature's contract (a request/response shape, a domain event's fields) to satisfy a *different*, later feature's needs — e.g. a read model discovering an earlier event lacks a field it needs to route by. The fix is often correct and minimal, but it revises delivered, tested scope for another feature's sake, which is not a unilateral call the way an internal-only, non-breaking addition is.

## Architecture Constraints

- **Stack**: Wolverine.Http + Marten + RabbitMQ, Marten in event-sourcing mode exclusively (see Principle I) — no hybrid document-store branch anywhere in the solution.
- **Snapshots are opt-in, not default.** Add `Projections.Snapshot<T>(SnapshotLifecycle.Inline)` only when something under `ReadModels/**` genuinely queries that entity by id. Prefer `Inline` over `Async` unless there's a specific reason to decouple write latency from projection cost — for a request/response API, `Inline` means a caller's next read always sees their own prior write.
- **An automation's decision state is not an entity snapshot.** When an automation's decision needs more than the trigger event's own fields, compute it live via `AggregateStreamAsync` into a single-purpose state type, every invocation. Never persist it, never register it as a snapshot, never let a second handler reuse it — a second handler needing "similar" state gets its own type. This is a different rule from entity snapshots (Principle I / above): an entity's snapshot is durable and meant to be shared; an automation's decision state is disposable and never shared, full stop.
- **Cross-module communication is versioned integration events over durable per-module queues** (`IntegrationEventQueueName`), never a shared table or direct cross-module query. New integration event types are suffixed `V1`, so a future breaking change adds `V2` rather than editing the original.
- **Automations are idempotent.** Delivery is at-least-once; every automation handler checks target state before acting so a redelivered or repeated trigger event never double-applies its effect.
- **Ownership checks are explicit, not implied by role.** A role check alone lets any caller with that role act on every other caller's resource, not just their own — an action scoped to "your own resource" needs its own `entity.OwnerId != callerId → Forbid()` check in addition to any role gate.
- **Concurrency conflicts map to `409` centrally, once** — both `JasperFx.ConcurrencyException` and `Marten.Exceptions.ConcurrentUpdateException` (two separate hierarchies, event-stream vs. document-level), handled in one place in `Program.cs`, not per-handler.
- **A handler loads its own mutation target live, never as a snapshot.** A `Commands/**`/`Automations/**` handler must fetch the entity it is about to mutate via `FetchForWriting`/`AggregateStreamAsync`, never `LoadAsync`/`Query` a snapshot of that same type — loading your own target as a snapshot risks acting on stale state. A read-only lookup of a *different* entity (an ownership or existence check) is legitimate but must be explicitly allowlisted with a one-line justification rather than passing silently, so the exception is visible and reviewable rather than indistinguishable from the mistake this rule exists to catch.

## Technology Constraints

- **Runtime**: `net10.0` — matches the validated package combo in `build-kit-dotnet-es/AGENT.md` (`WolverineFx`/`WolverineFx.Http`/`WolverineFx.Marten`/`WolverineFx.RabbitMQ` `6.22.0`, `Marten` `9.19.0`); re-verify these still resolve before assuming they're current on a much later date.
- **Database**: Postgres via Marten, event-sourcing mode exclusively (Principle I) — no EF Core, no separate document database.
- **Messaging**: RabbitMQ via `WolverineFx.RabbitMQ`, one durable queue per module needing cross-module integration events (Architecture Constraints).
- **API style**: Wolverine.Http endpoint attributes (`[WolverinePost]`/`[WolverineGet]`/etc.) exclusively — no MVC controllers.
- **Identity**: ASP.NET Core Identity.
- **Logging**: built-in `ILogger` with OpenTelemetry exporters (Principle X).
- **Solution file format**: `.slnx` (the XML solution format, .NET 10's `dotnet new sln` default as of this constitution's ratification), not the legacy `.sln` — decided 2026-08-09 while scaffolding `001-authority-administration`, before anything depended on the older format. `dotnet build`/`dotnet sln add`/IDE tooling all support `.slnx` directly; no conversion step needed.

## Quality Gates & Workflow

- **Every commit must pass `dotnet build` and the slice's own tests** (`dotnet test --filter <SliceName>`) before landing — running only the affected slice's tests is enough; the full suite is not required per commit. **Exception**: a change to a domain event or aggregate that predates the current slice (extending an existing event's fields, fixing a shared aggregate) requires running the full solution suite before landing, not just the new slice's tests — the change is not confined to the slice touching it, and the cheapest place to catch a break is before the commit, not in a later, unrelated slice's session.
- **`docs/adr/` continues this project's own ADR numbering** from Solution Arch §10's summary table (currently ADR-001 through ADR-018) — a feature's own `research.md` decisions get the next free numbers in that same sequence when written up as full ADRs (Quality Gates, `speckit-plan`'s ADR step). Do not reuse or confuse these with ADR numbers appearing in `build-kit-dotnet-es`'s own reference material (e.g. "ADR-019"/"ADR-031" in its `AGENT.md`/`README.md`) — those belong to an unrelated prior project that toolkit was validated against, not this one.
- **Pre-checkin deterministic checks** (see `build-kit-dotnet-es/quality-checks.md` for the source rationale): `dotnet format --verify-no-changes`, `dotnet list package --vulnerable`, a SAST pass, and a secret-scan pass over the staged diff, run before any change lands — regardless of what enforces them (CI, a git hook, or manual discipline); this project does not run the Ralph loop, so nothing here assumes its specific hook mechanism. High/Critical findings from any of these gate the change (Principle VIII); lower-severity findings are a signal, not a blocker.
- **Bypassing the gate requires a stated reason and is always audited** — never a silent skip. If a check must be bypassed, that decision is logged (timestamp, slice, reason), unconditionally.
- **New PII-shaped fields** (name, address, phone, email, or similar) are flagged for an explicit masking/retention decision at the point they're added — not deferred, not assumed safe by default. In an underwriting/insurance domain this applies to insured names, broker contact details, and claimant information at minimum.
- **One slice per work session/PR.** Do not chain multiple slices' implementation together; each slice is built, tested, and committed on its own before moving to the next.
- **Naming**: handler test methods follow `MethodName_Scenario_ExpectedOutcome`. Domain events needing a version bump are suffixed `V1`, `V2`, etc. rather than mutating an existing event's shape in place.

## Governance

This constitution supersedes ad-hoc convention; where a PR or generated slice conflicts with a principle here, the principle wins and the conflict gets fixed, not waived silently. Complexity that deviates from a stated principle (e.g. a snapshot added "just in case," a shared automation-state type, a document-store shortcut) must be justified explicitly in the PR/commit description, not left implicit.

Amendments happen by editing this file directly, with a version bump and a dated entry below explaining what changed and why — this file's own history is the audit trail, there is no separate changelog.

`build-kit-dotnet-es/AGENT.md` (this repo's own accumulated, project-specific learnings log) is the living companion to this document: it records concrete gotchas and patterns discovered while building against these principles, and should be read alongside this constitution during implementation — but it *refines and illustrates* these principles, it does not override them. A learning that contradicts a principle here means either the learning is wrong, or this constitution needs an amendment; it does not silently win by being more recent.

**Version**: 1.2.0 | **Ratified**: 2026-08-09 | **Last Amended**: 2026-08-10
<!-- 1.0.0: initial adoption for Underwriting, adapted verbatim (minus naming) from Powergym's 1.2.0 constitution — both projects share build-kit-dotnet-es as their common architecture source. -->
<!-- 1.1.0: added Technology Constraints solution-file-format entry (.slnx over legacy .sln) while scaffolding 001-authority-administration. -->
<!-- 1.2.0: process learnings from completing 001-authority-administration (all tasks done, 2026-08-10) — Principle III: verify against spec.md's literal board export, not just data-model.md's summary, before treating a dependency-edge gap as a bug vs. an intentional limitation. Principle XI: confirm with the user before widening an already-shipped feature's contract for a later feature's sake. Quality Gates: full-suite requirement when a change touches a pre-existing shared event/aggregate; docs/adr/ numbering continues this project's own ADR-001-018 sequence, distinct from build-kit-dotnet-es's own unrelated reference-project ADR numbers. -->
