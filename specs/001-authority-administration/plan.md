# Implementation Plan: Authority Administration

**Branch**: `001-authority-administration` | **Date**: 2026-08-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-authority-administration/spec.md`

## Summary

Governance/reference data for the Provider → Cell → Underwriter delegated-authority
cascade: grant, revise, and revoke authority limits at Cell and Underwriter tier;
reject an over-cascade grant with an explicit escalation path
(`RequestCellAuthorityIncrease`); reassess in-flight submissions whenever the rules
underneath them change. This is the **first feature to go through `/speckit-plan`**
in this repo — no `.NET` solution exists yet (confirmed: no `.sln`/`.csproj` anywhere
in the repo). Part of this plan is therefore establishing the initial solution
scaffold, not just this module — see "Solution scaffold" in `research.md`.

Technical approach: event-sourced `AuthorityLimit` aggregate (one Marten stream per
grant, keyed by `authorityLimitId`), an `Inline` Marten snapshot behind `AuthorityMatrix`
(same-transaction-consistency requirement, Clarified 2026-08-09 / SC-001), and three
new domain events to close the rejection-path gaps the spec's clarifications
surfaced (`CellAuthorityLimitGrantRejected`, an extended `UnderwriterAuthorityLimitRejected`,
and `AuthorityLimitRevisionRejected`) — see `data-model.md`.

## Technical Context

**Language/Version**: C# / .NET 10 — per constitution Technology Constraints;
`WolverineFx`/`WolverineFx.Http`/`WolverineFx.Marten`/`WolverineFx.RabbitMQ` `6.22.0`,
`Marten` `9.19.0` (re-verify these still resolve before trusting the pinned versions —
constitution's own caveat).

**Primary Dependencies**: Wolverine.Http (command handling + HTTP endpoints), Marten
(event store), no RabbitMQ dependency for *this* module's own slices (Authority
Administration has no cross-module command it needs to *send*, only an integration
event it *publishes* — see "Cross-module boundary" below), ASP.NET Core Identity
(authN, per constitution).

**Storage**: PostgreSQL via Marten, event-sourcing exclusively (constitution
Principle I / Architecture Constraints; ADR-002). Schema: `authority` (one Marten
schema per module, ADR-013).

**Testing**: xUnit + Shouldly (Layers 1–2), NSubstitute for `IDocumentSession`
mocking (Layer 2), Testcontainers-backed real Postgres (Layer 3) — constitution
Principle IV. Every clarified rejection path (FR-010–012) and every scenario in
`spec.md`'s Acceptance Scenarios gets at least one executable test.

**Target Platform**: Linux containers on Kubernetes (ADR-011); this module ships as
part of the single `Api.Host` deployable (ADR-001), not its own service.

**Project Type**: web-service — one module (`BrokerConnect.Modules.AuthorityAdministration.*`)
inside the `Api.Host` modular monolith (ADR-001).

**Performance Goals**: No numeric SLA stated by the source board or Requirements
docs for this context (`NEEDS CLARIFICATION` in the generic sense) **except** the one
already resolved via `/speckit-clarify`: `AuthorityMatrix` reads must reflect all
prior writes with zero observed staleness (SC-001) — resolved as "`Inline` Marten
snapshot", not a numeric target, so nothing further to clarify here.

**Constraints**:
- `AuthorityMatrix` must be an `Inline` Marten projection (Clarified 2026-08-09).
- Cell-scoped vs. cross-cell authorization must be enforced at the handler level
  (Solution Arch §8, NFR-006) — a caller's role alone does not imply access to
  every cell's authority data.
- No numeric latency/throughput constraint stated elsewhere — do not invent one;
  flag as open in `research.md` rather than fabricate a number.

**Scale/Scope**: 9 slices (5 `STATE_CHANGE`, 3 `STATE_VIEW`, 1 `AUTOMATION`) — matches
constitution Principle II's "exactly one of state-change/state-view/automation"
exhaustively; no slice in this feature falls outside those three shapes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Constraint | Check | Status |
|---|---|---|
| I. Event-Sourced Everywhere | `AuthorityLimit` is a single self-aggregating stream (`Create`/`Apply`); no document-store branch proposed. `[JsonInclude]`/`[JsonConstructor]` required — flagged for implementation, not yet coded. | **PASS** |
| II. Vertical Slices, Three Shapes Only | All 9 slices classify cleanly as `STATE_CHANGE`/`STATE_VIEW`/`AUTOMATION` (see Scale/Scope above) — no slice implies a downstream effect beyond its own event(s) inlined into a command handler. | **PASS** |
| III. The Spec Is the Source of Truth | Every field, event, and read model in `data-model.md` traces to `spec.md`'s Event Model Detail appendix or an explicit Clarification; the 3 new rejection events are each justified by a specific `FR-0XX` gap the spec already documents, not invented beyond that. | **PASS** |
| IV. Test-First, Three Layers | Test plan in `quickstart.md` covers all 3 layers; deferred to `/speckit-tasks` for actual task breakdown. | **PASS (planned)** |
| V. Discoverable by Convention | All handler classes will be named `*Handler` per convention — flagged for implementation review, not a planning-time violation. | **N/A at plan stage** |
| Architecture Constraints — snapshots opt-in | `AuthorityMatrix` is the **only** slice in this feature that needs an `Inline` snapshot (Clarified 2026-08-09, entity queried by id at decision time). `CellAuthorityRegister`/`UnderwriterAuthorityRegister` are historical registers with no same-request read-after-write requirement — plain async Marten projections, not snapshots. | **PASS** |
| Architecture Constraints — automation decision state | `ReassessInFlightSubmissionsOnRuleChange`'s own decision state (if any) must be computed live, never persisted — applies to whichever module ends up implementing it (see Cross-module boundary below, this module does not implement it). | **N/A to this module** |
| Architecture Constraints — cross-module communication | `InFlightSubmissionReassessed`/`ReassessInFlightSubmissionsOnRuleChange` targets `SubmissionAssessment`, which is Underwriting Decisioning's aggregate (`004-underwriting-decisioning`), not this module's. Per ADR-004 (module boundaries = board's `MODEL_CONTEXT` nodes exactly, cross-module only via integration events), this module's actual scope is **publishing** a versioned integration event on `AuthorityLimitRevised`/`AuthorityLimitRevoked`; the consuming automation belongs to `004`. See `data-model.md` "Cross-module boundary" and `research.md` Decision 3. | **Resolved — scope narrowed, not a violation** |

No unjustified violations — Complexity Tracking table in this plan is intentionally empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-authority-administration/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/            # Phase 1 output
│   └── authority-administration-http.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

No `.NET` solution exists yet anywhere in this repo — this is the first feature to
reach implementation planning. Structure Decision below establishes the scaffold
convention every subsequent feature's plan will reuse; see `research.md` Decision 1
for why `src/` at repo root, matching build-kit-dotnet-es's own
`<path-to-your-.NET-solution>` placeholder convention, and Solution Arch §3's module
shape (`BrokerConnect.Modules.<Context>.{Api,Domain,Contracts}`).

```text
src/
├── BrokerConnect.slnx
├── Api.Host/                                    # ADR-001: single deployable, composes all modules
│   ├── Program.cs                                # AddMarten(...).IntegrateWithWolverine(); per-module IModuleInstaller discovery
│   └── Api.Host.csproj
├── BuildingBlocks/
│   ├── BuildingBlocks.Domain.Money/              # ADR-007 — not needed by this feature (no monetary fields), scaffolded when Bordereaux Settlement/Claims need it
│   └── BuildingBlocks.Governance/                # ADR-006 — referral/escalation engine; not needed by this feature (Authority Administration's escalation, RequestCellAuthorityIncrease, is a simpler one-shot request, not the referral cascade this library targets)
└── Modules/
    └── AuthorityAdministration/
        ├── BrokerConnect.Modules.AuthorityAdministration.Api/
        │   ├── Commands/
        │   │   ├── GrantCellAuthorityLimit/
        │   │   ├── GrantUnderwriterAuthorityLimit/
        │   │   ├── ReviseAuthorityLimit/
        │   │   ├── RevokeAuthorityLimit/
        │   │   └── RequestCellAuthorityIncrease/
        │   ├── ReadModels/
        │   │   ├── AuthorityMatrix/               # Inline snapshot — see Constraints above
        │   │   ├── CellAuthorityRegister/
        │   │   └── UnderwriterAuthorityRegister/
        │   ├── IntegrationEvents/
        │   │   └── Published/
        │   │       └── AuthorityLimitChangedV1.cs # consumed by 004-underwriting-decisioning — see Cross-module boundary
        │   ├── Module.cs
        │   └── AuthorityAdministrationModuleDbConfig.cs
        ├── BrokerConnect.Modules.AuthorityAdministration.Domain/
        │   ├── Aggregates/
        │   │   └── AuthorityLimit.cs
        │   └── Events/
        │       └── (one file per event — see data-model.md)
        └── BrokerConnect.Modules.AuthorityAdministration.Contracts/
            └── (public DTOs other modules may reference)

tests/
└── Modules/
    └── AuthorityAdministration/
        ├── AuthorityAdministration.Domain.Tests/      # Layer 1
        ├── AuthorityAdministration.Api.Tests/          # Layer 2
        └── AuthorityAdministration.IntegrationTests/   # Layer 3 (Testcontainers)
```

No `Infrastructure` project — this context has no external integration
(`IR-00X`) of its own, unlike e.g. Submission Intake's rating-engine client.

**Structure Decision**: New `src/` solution root, one module
(`AuthorityAdministration`) under `src/Modules/`, `Api.Host` as the single
deployable per ADR-001. This is the scaffold every later feature's plan builds on;
flag in `004-underwriting-decisioning`'s own plan that it must consume
`AuthorityLimitChangedV1` (see Cross-module boundary in `data-model.md`).

## Constitution Check — Post-Design Re-evaluation

Re-checked after Phase 1 (`data-model.md`, `contracts/`, `quickstart.md`):

- **Principle I / snapshots-opt-in**: confirmed unchanged — exactly one `Inline`
  snapshot (`AuthorityMatrix`), both registers stay plain async projections.
- **Principle II**: the 3 new/extended rejection events (Decision 3) are all
  appended by their triggering command's own handler, inline — none introduce a
  4th slice shape or a downstream effect requiring a separate automation.
- **Architecture Constraints / cross-module**: `data-model.md`'s
  `AuthorityLimitChangedV1` section confirms the design doesn't reach into
  `SubmissionAssessment` directly anywhere in this module's contracts or data
  model — the boundary from Decision 4 held through detailed design.
- **No new violations introduced** by the detailed field/event design in Phase 1.

Gate: **PASS**, unchanged from pre-design check.

## Complexity Tracking

*No violations to justify — table intentionally left empty.*
