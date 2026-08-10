---

description: "Task list for Authority Administration (001)"

---

# Tasks: Authority Administration

**Input**: Design documents from `/specs/001-authority-administration/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md` — all present.

**Tests**: Included. Constitution Principle IV ("Test-First, Three Layers") is
NON-NEGOTIABLE for this project — tests are not optional here the way the generic
spec-kit template treats them.

**Organization**: Tasks are grouped by user story, numbered exactly as in
`spec.md`, reordered into priority tiers (P1 stories first, then P2) per spec-kit
convention. US9 is scoped narrowly per `research.md` Decision 4 — see that phase.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to the user story number in `spec.md` (US1, US2, US3...)

## Path Conventions

Single .NET solution per `plan.md` Project Structure — `src/`, `tests/` at repo root.

---

## Phase 1: Setup

**Purpose**: Solution/project scaffolding — this is the **first feature in the
repo**, so this phase creates the solution itself, not just this module.

- [X] T001 Create `src/BrokerConnect.slnx` and add all projects listed in `plan.md`'s Project Structure (empty class-library/web stubs at this stage)
- [X] T002 Add `WolverineFx`/`WolverineFx.Http`/`WolverineFx.Marten` `6.22.0` and `Marten` `9.19.0` package references to `src/Api.Host/Api.Host.csproj` and `src/Modules/AuthorityAdministration/BrokerConnect.Modules.AuthorityAdministration.Api/*.csproj` (constitution Technology Constraints — re-verify these versions still resolve before pinning)
- [X] T003 [P] Add `xunit`, `Shouldly`, `NSubstitute` to `tests/Modules/AuthorityAdministration/AuthorityAdministration.Domain.Tests` and `.Api.Tests`; add `Testcontainers.PostgreSql` to `.IntegrationTests` (constitution Principle IV)
- [X] T004 [P] Configure `dotnet format` + analyzers at solution level (constitution Quality Gates)

**Checkpoint**: `dotnet build src/BrokerConnect.slnx` succeeds with empty stubs.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared infrastructure every user story below depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Configure `src/Api.Host/Program.cs`: `AddMarten(...).IntegrateWithWolverine()`, central exception handler mapping `JasperFx.ConcurrencyException`/`Marten.Exceptions.ConcurrentUpdateException` → `409 Conflict` (constitution Architecture Constraints) — used the validated skeleton from `build-kit-dotnet-es/AGENT.md` "Program.cs / Host wiring" verbatim rather than re-deriving it; also created `src/BuildingBlocks/BrokerConnect.BuildingBlocks.Domain/` (not in the original Project Structure list) to house `IMartenModuleConfiguration`, since `AGENT.md` places it there and it didn't exist yet; added `WolverineFx.RabbitMQ` to `Api.Host` (needed for the outbox that publishes `AuthorityLimitChangedV1`, even though this module doesn't consume RabbitMQ itself)
- [X] T006 [P] ~~Implement `AuthorityAdministrationModuleDbConfig`~~ — **consolidated into T008's `Module.cs`**: `AGENT.md`'s validated pattern is one `IMartenModuleConfiguration` implementation per module, not a separate DbConfig class. Sets `options.Events.DatabaseSchemaName = "authority"` in `src/Modules/AuthorityAdministration/BrokerConnect.Modules.AuthorityAdministration.Api/Module.cs`
- [X] T007 [P] Create `AuthorityLimit` aggregate skeleton (`[JsonConstructor]`, `AuthorityLimitId`/`Tier`/`Status`/`Version`) in `src/Modules/AuthorityAdministration/BrokerConnect.Modules.AuthorityAdministration.Domain/Aggregates/AuthorityLimit.cs` per `data-model.md` state shape — `Create`/`Apply` overloads deferred to each event's own phase (T015, T024, T032, T038)
- [X] T008 Implement `Module.cs` (`AuthorityAdministrationModule : IMartenModuleConfiguration`, not `IModuleInstaller` — see T006 note) in `src/Modules/AuthorityAdministration/BrokerConnect.Modules.AuthorityAdministration.Api/Module.cs`, discovered by `Api.Host`'s `Program.cs` via `opts.Discovery.IncludeAssembly`
- [X] T009 Add a health check endpoint separate from business endpoints (`GET /health`) in `src/Api.Host/Program.cs` (constitution Principle X)

**Checkpoint**: Foundation ready — user stories can now proceed.

---

## Phase 3: User Story 1 - Grant Cell Authority Limit (Priority: P1)

**Goal**: Governance can grant a Cell an authority limit, creating the first
record in an `AuthorityLimit` stream.

**Independent Test**: `POST /api/authority/cells/{cellId}/limits` → `201`, then
`GET /api/authority/matrix/{authorityLimitId}` in the same test reflects it
immediately (quickstart.md scenario 1).

### Tests for User Story 1

- [X] T010 [P] [US1] Contract test `POST /api/authority/cells/{cellId}/limits` (happy path) — **retargeted to Layer 3**: `tests/Modules/AuthorityAdministration/AuthorityAdministration.IntegrationTests/GrantCellAuthorityLimitIntegrationTests.cs`, not the Api.Tests path originally named — `session.Query<AuthorityLimit>`/`FetchForWriting` are awkward to mock meaningfully (build-state-change SKILL.md), so all of US1/US3/US4/US5's contract tests run against a real Postgres via Testcontainers instead. Passing.
- [X] T011 [P] [US1] Contract test duplicate-active-grant → `409 CellAuthorityLimitGrantRejected` (Clarified 2026-08-09, FR-011) — same file/retarget as T010. Passing.
- [X] T012 [P] [US1] Domain test `AuthorityLimit.Create(CellAuthorityLimitGranted)` in `tests/Modules/AuthorityAdministration/AuthorityAdministration.Domain.Tests/AuthorityLimitTests.cs` — passing

### Implementation for User Story 1

- [X] T013 [P] [US1] `CellAuthorityLimitGranted` event record in `.../Domain/Events/CellAuthorityLimitGranted.cs` (`data-model.md` field list)
- [X] T014 [P] [US1] `CellAuthorityLimitGrantRejected` event record (new, `data-model.md` Decision 3) in `.../Domain/Events/CellAuthorityLimitGrantRejected.cs`
- [X] T015 [US1] `AuthorityLimit.Create`/`Apply(CellAuthorityLimitGranted)` in `.../Domain/Aggregates/AuthorityLimit.cs` (depends on T007, T013)
- [X] T016 [US1] `GrantCellAuthorityLimit` command record + `[WolverinePost]` handler in `.../Api/Commands/GrantCellAuthorityLimit/` — validates no existing Active record for (tier=Cell, cellId, classOfBusiness) by querying `session.Query<AuthorityLimit>()` in-memory (functionally live once T046 registers the Inline snapshot — see handler comment), appends `CellAuthorityLimitGranted` or `CellAuthorityLimitGrantRejected`. Builds clean.
- [X] T017 [US1] `409` rejection path returns `ProblemHttpResult` (`AuthorityRejectionProblem.Conflict`, RFC7807 `application/problem+json`) — **fixed 2026-08-09**: originally returned a bespoke `Conflict<T>` JSON DTO across all 3 rejection-capable handlers (US1/US3/US4), violating constitution Principle VII. Retrofitted all 3 to a shared `AuthorityRejectionProblem` helper in `.../Api/AuthorityRejectionProblem.cs`; `contracts/authority-administration-http.md` updated to match (also added the `/v1/` route prefix used throughout, matching `build-kit-dotnet-es`'s own example convention).

**Checkpoint**: US1 fully functional and independently testable.

---

## Phase 4: User Story 3 - Grant Underwriter Authority Limit (Priority: P1)

**Goal**: A Cell CUO can grant an underwriter authority within the cell's limit,
with the cascade-limit rejection path (FR-003/FR-006) and the duplicate-active
rejection path (FR-011) both explicit.

**Independent Test**: quickstart.md scenarios 2–4.

### Tests for User Story 3

- [X] T018 [P] [US3] Contract test happy-path grant — **retargeted to Layer 3** (same rationale as T010): `tests/Modules/AuthorityAdministration/AuthorityAdministration.IntegrationTests/GrantUnderwriterAuthorityLimitIntegrationTests.cs`. Passing.
- [X] T019 [P] [US3] Contract test `rejectionReason: "ExceedsCellLimit"` — same file/retarget as T018. Passing.
- [X] T020 [P] [US3] Contract test `rejectionReason: "DuplicateActiveGrant"` (Clarified 2026-08-09, FR-011) — same file/retarget as T018. Passing.
- [X] T021 [P] [US3] Domain test cascade-limit calculation in `tests/Modules/AuthorityAdministration/AuthorityAdministration.Domain.Tests/AuthorityLimitTests.cs` — **scope note**: cascade *rejection* logic lives in the handler (`Apply` never guards, per constitution), so this landed as a narrower scope-documentation test rather than a cascade-rejection test; the real cascade-rejection behavior is only verifiable via T019/T020 (still open)

### Implementation for User Story 3

- [X] T022 [P] [US3] `UnderwriterAuthorityLimitGranted` event record in `.../Domain/Events/UnderwriterAuthorityLimitGranted.cs`
- [X] T023 [US3] Extend `UnderwriterAuthorityLimitRejected` event's `rejectionReason` to `"ExceedsCellLimit" | "DuplicateActiveGrant"` (`data-model.md` Decision 3) in `.../Domain/Events/UnderwriterAuthorityLimitRejected.cs`
- [X] T024 [US3] `AuthorityLimit.Create`/`Apply` for both events (depends on T007, T022, T023)
- [X] T025 [US3] `GrantUnderwriterAuthorityLimit` command + handler in `.../Api/Commands/GrantUnderwriterAuthorityLimit/` — reads the Cell's snapshot for the cascade check, appends onto the **Cell's** stream on rejection per `data-model.md`. Builds clean.

**Checkpoint**: US1 + US3 both independently functional.

---

## Phase 5: User Story 4 - Revise Authority Limit (Priority: P1)

**Goal**: Governance can revise a Cell's or Underwriter's limit with an audit
trail; both new rejection paths (Revoked-terminal, cascade-exceeded) enforced.

**Independent Test**: quickstart.md scenarios 5–6, 8 (integration-event publish half).

### Tests for User Story 4

- [X] T026 [P] [US4] Contract test happy-path revise — **retargeted to Layer 3** (same rationale as T010): `tests/Modules/AuthorityAdministration/AuthorityAdministration.IntegrationTests/ReviseAuthorityLimitIntegrationTests.cs`. Passing.
- [X] T027 [P] [US4] Contract test `rejectionReason: "RevokedRecord"` (Clarified 2026-08-09, FR-010) — same file/retarget as T026. Passing.
- [X] T028 [P] [US4] Contract test `rejectionReason: "ExceedsCellLimit"` on revise (Clarified 2026-08-09, FR-012) — same file/retarget as T026. Passing.
- [X] T029 [P] [US4] Integration test (Layer 3) asserting `AuthorityLimitChangedV1` is published — landed in the same `ReviseAuthorityLimitIntegrationTests.cs` file rather than a separate `AuthorityLimitChangedIntegrationEventTests.cs`; `IMessageBus` is substituted (NSubstitute) rather than run through a real host/RabbitMQ, since there is no consumer of `AuthorityLimitChangedV1` yet (004 doesn't exist) to verify actual cross-process delivery against — the test asserts the handler hands the outbox the right event shape, not outbox durability itself. Passing. **Also surfaced two real bugs while getting Testcontainers runs working for the first time** (T046's Inline snapshot registration had never actually been exercised against real Postgres before this session): (1) Marten's `Projections.Snapshot<T>` registration throws unless the identity member is literally named `Id` — `AuthorityLimitId` alone (the "{TypeName}Id" convention, which `LoadAsync`/`Query` *do* honor) isn't enough; fixed with a `JsonIgnore`d `Id` alias in `AuthorityLimit.cs`. (2) The generator that dispatches `Create`/`Apply` must run in the assembly defining the aggregate — `AuthorityAdministration.Domain.csproj` needed its own `Marten` PackageReference, not just the Api project's. (3) Marten silently overwrites a document property literally named `Version` with its own internal concurrency sequence number — `AuthorityLimit.Version` (the business revision counter) was renamed to `RevisionNumber` after this was caught corrupting revise-response version numbers.

### Implementation for User Story 4

- [X] T030 [P] [US4] `AuthorityLimitRevised` event record in `.../Domain/Events/AuthorityLimitRevised.cs`
- [X] T031 [P] [US4] `AuthorityLimitRevisionRejected` event record (new, `data-model.md` Decision 3) in `.../Domain/Events/AuthorityLimitRevisionRejected.cs`
- [X] T032 [US4] `AuthorityLimit.Apply` for both events, increments `version` on `Revised` (depends on T007, T030, T031)
- [X] T033 [US4] `ReviseAuthorityLimit` command + handler in `.../Api/Commands/ReviseAuthorityLimit/` — checks `status != "Revoked"`, re-runs cascade check when `tier == "Underwriter"` (depends on T032). Builds clean.
- [X] T034 [US4] `AuthorityLimitChangedV1` integration event record in `.../Api/IntegrationEvents/Published/AuthorityLimitChangedV1.cs` (`data-model.md` Cross-module boundary) — published via `IMessageBus.PublishAsync` before `SaveChangesAsync` (Wolverine/Marten outbox pattern); **runtime outbox behavior unverified** — needs a real Postgres+RabbitMQ (T029) to confirm, compiling correctly is not proof it publishes correctly

**Checkpoint**: US1, US3, US4 independently functional.

---

## Phase 6: User Story 5 - Revoke Authority Limit (Priority: P1)

**Goal**: Governance can revoke a Cell's or Underwriter's limit entirely,
terminal and immediate.

**Independent Test**: revoke, then attempt a further revise → `409` (covered
jointly with US4's T027 test, since it needs both handlers).

### Tests for User Story 5

- [X] T035 [P] [US5] Contract test happy-path revoke — **retargeted to Layer 3** (same rationale as T010): `tests/Modules/AuthorityAdministration/AuthorityAdministration.IntegrationTests/RevokeAuthorityLimitIntegrationTests.cs`. Passing.
- [X] T036 [P] [US5] Integration test asserting `AuthorityLimitChangedV1` published on revoke, reusing T029's harness (NSubstitute `IMessageBus`, same scope note as T029) — same file as T035. Passing.

### Implementation for User Story 5

- [X] T037 [P] [US5] `AuthorityLimitRevoked` event record in `.../Domain/Events/AuthorityLimitRevoked.cs`
- [X] T038 [US5] `AuthorityLimit.Apply(AuthorityLimitRevoked)` sets `status = "Revoked"` (depends on T007, T037)
- [X] T039 [US5] `RevokeAuthorityLimit` command + handler in `.../Api/Commands/RevokeAuthorityLimit/` (depends on T038). Builds clean.
- [X] T040 [US5] Publish `AuthorityLimitChangedV1` on `AuthorityLimitRevoked` too (depends on T034, T039) — same outbox-verification caveat as T034

**Checkpoint**: US1, US3, US4, US5 independently functional.

---

## Phase 7: User Story 7 - Request Cell Authority Increase (Priority: P1)

**Goal**: A Cell CUO can escalate after a rejected underwriter grant, as its own
auditable request.

**Independent Test**: quickstart.md scenario 7.

### Tests for User Story 7

- [X] T041 [P] [US7] Contract test — **redirected to a domain test**: `CellAuthorityIncreaseRequestTests.cs` in `AuthorityAdministration.Domain.Tests`, not a Layer 2 contract test yet. Passing.

### Implementation for User Story 7

- [X] T042 [P] [US7] `CellAuthorityIncreaseRequest` aggregate (single-event stream, `data-model.md`) in `.../Domain/Aggregates/CellAuthorityIncreaseRequest.cs`
- [X] T043 [P] [US7] `CellAuthorityIncreaseRequested` event record in `.../Domain/Events/CellAuthorityIncreaseRequested.cs`
- [X] T044 [US7] `RequestCellAuthorityIncrease` command + handler in `.../Api/Commands/RequestCellAuthorityIncrease/` (depends on T042, T043). Builds clean.

**Checkpoint**: All 5 P1 stories independently functional — this is the P1 MVP slice of this feature.

---

## Phase 8: User Story 2 - AuthorityMatrix Projection (Priority: P2)

**Goal**: The decision-time view Underwriting Decisioning's `AssessSubmission`
queries, with the `Inline`-snapshot same-transaction guarantee (Clarified
2026-08-09 / SC-001).

**Independent Test**: quickstart.md scenario 1 (already exercised by US1, but
this phase is what makes it pass rather than 404).

### Tests for User Story 2

- [ ] T045 [P] [US2] Test `Inline` snapshot reflects a grant in the same request in `tests/Modules/AuthorityAdministration/AuthorityAdministration.IntegrationTests/AuthorityMatrixInlineSnapshotTests.cs` — this is the executable proof of SC-001, must use a real Postgres (Testcontainers), not a mock

### Implementation for User Story 2

- [X] T046 [US2] Register `Projections.Snapshot<AuthorityLimit>(SnapshotLifecycle.Inline)` — **consolidated into `Module.cs`** (same T006 pattern), not a separate DbConfig file. Registration compiled since early in the session but was never actually run against real Postgres until T010/T018/T026/T035's Testcontainers work in this session, which surfaced (and this task's fix resolved) two blocking bugs — see T029's note. Now verified working: a grant is queryable via `session.Query<AuthorityLimit>()`/`LoadAsync` in the same session. **Still projecting `AuthorityLimit` itself, not a distinct `AuthorityMatrix` DTO** — T047 (the `GET` endpoint) is what was supposed to shape that DTO and is still not started, so flagging rather than claiming the DTO half of this task's own description is done.
- [ ] T047 [US2] `GET /api/authority/matrix/{authorityLimitId}` `[WolverineGet]` endpoint in `.../Api/ReadModels/AuthorityMatrix/GetAuthorityMatrix.cs` (depends on T046)
- [ ] T048 [US2] Wire T016's and T025's duplicate/cascade checks to query this snapshot directly rather than re-deriving state (depends on T046; retrofit into US1/US3 handlers)

**Checkpoint**: US1–US5, US7, US2 all independently functional.

---

## Phase 9: User Story 6 - CellAuthorityRegister Projection (Priority: P2)

**Goal**: Historical register per cell, current + full history.

### Tests for User Story 6

- [ ] T049 [P] [US6] Projection test in `tests/Modules/AuthorityAdministration/AuthorityAdministration.Api.Tests/CellAuthorityRegisterProjectionTests.cs`

### Implementation for User Story 6

- [ ] T050 [P] [US6] `CellAuthorityRegister` async projection (`data-model.md` field list) reacting to `CellAuthorityLimitGranted`, `AuthorityLimitRevoked` in `.../Api/ReadModels/CellAuthorityRegister/CellAuthorityRegisterProjector.cs`
- [ ] T051 [US6] `GET /api/authority/cells/{cellId}/register` endpoint in `.../Api/ReadModels/CellAuthorityRegister/GetCellAuthorityRegister.cs` — paginate `history[]` (page size TBD, not specified by source — flag rather than guess a number)

**Checkpoint**: US1–US7 (all P1) + US2, US6 (P2) functional.

---

## Phase 10: User Story 8 - UnderwriterAuthorityRegister Projection (Priority: P2)

**Goal**: Historical register per underwriter.

### Tests for User Story 8

- [ ] T052 [P] [US8] Projection test in `tests/Modules/AuthorityAdministration/AuthorityAdministration.Api.Tests/UnderwriterAuthorityRegisterProjectionTests.cs`

### Implementation for User Story 8

- [ ] T053 [P] [US8] `UnderwriterAuthorityRegister` async projection reacting to `UnderwriterAuthorityLimitGranted`, `CellAuthorityIncreaseRequested` in `.../Api/ReadModels/UnderwriterAuthorityRegister/UnderwriterAuthorityRegisterProjector.cs`
- [ ] T054 [US8] `GET /api/authority/underwriters/{underwriterId}/register` endpoint in `.../Api/ReadModels/UnderwriterAuthorityRegister/GetUnderwriterAuthorityRegister.cs`

**Checkpoint**: All 8 in-scope stories functional (US9 is scope-narrowed below).

---

## Phase 11: User Story 9 - Rules-Changed-Mid-Flight Publishing (Priority: P2, narrowed scope)

**Goal**: Per `research.md` Decision 4, this module's actual responsibility for
FR-009 is limited to **publishing** `AuthorityLimitChangedV1` — already done in
T034/T040 (US4/US5). This phase is the explicit checkpoint confirming that
narrowing, not new implementation.

- [ ] T055 [US9] Confirm `AuthorityLimitChangedV1` is published on both `AuthorityLimitRevised` (T034) and `AuthorityLimitRevoked` (T040) — no new code, verification only
- [ ] T056 [US9] Add a note to `004-underwriting-decisioning/plan.md` (when that feature reaches planning) that it must consume `AuthorityLimitChangedV1` and implement `ReassessInFlightSubmissionsOnRuleChange` against its own `SubmissionAssessment` aggregate — **do not implement that automation here**

**Checkpoint**: Feature complete except cross-module consumption (deferred to `004`).

---

## Phase 12: Polish & Cross-Cutting Concerns

- [ ] T057 [P] Write ADR files for `research.md` Decisions 1–4 into `docs/adr/` (Solution Arch §10 note: "Full ADR text... lives in `docs/adr/` once the solution is scaffolded" — it now is)
- [ ] T058 [P] `dotnet format --verify-no-changes`, `dotnet list package --vulnerable`, SAST + secret-scan pass over the module (constitution Quality Gates)
- [ ] T059 Run `quickstart.md` scenarios 1–8 end-to-end against a real Postgres
- [ ] T060 [P] Structured logging pass — `ILogger` message templates on every handler (constitution Principle X), correlation ID propagation verified

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **P1 stories (Phases 3–7)**: All depend on Foundational. US1 and US3 share the
  duplicate-active/cascade check against `AuthorityMatrix` (T016, T025) — build
  US1 first since US3's cascade check reads the Cell record US1 creates.
- **P2 stories (Phases 8–11)**: Depend on Foundational; US2 (Phase 8) should land
  before Polish since T048 retrofits US1/US3's handlers to use the real
  snapshot instead of re-deriving state ad hoc.
- **Polish (Phase 12)**: Depends on all desired stories being complete.

### Parallel Opportunities

- All `[P]` tasks within Setup and Foundational run in parallel.
- Once Foundational completes, US1, US4, US5, US7 can start in parallel (US3
  has a soft dependency on US1's Cell record existing for realistic cascade
  tests, but its own code can be written in parallel).
- US2, US6, US8 (the three read-model stories) are fully independent of each
  other and can be built in parallel once their source events (from the P1
  phases) exist.

---

## Parallel Example: User Story 1

```bash
Task: "Contract test POST /api/authority/cells/{cellId}/limits happy path in tests/.../GrantCellAuthorityLimitTests.cs"
Task: "Contract test duplicate-active-grant 409 in tests/.../GrantCellAuthorityLimitTests.cs"
Task: "Domain test AuthorityLimit.Create(CellAuthorityLimitGranted) in tests/.../AuthorityLimitTests.cs"
Task: "CellAuthorityLimitGranted event record in .../Domain/Events/CellAuthorityLimitGranted.cs"
Task: "CellAuthorityLimitGrantRejected event record in .../Domain/Events/CellAuthorityLimitGrantRejected.cs"
```

---

## Implementation Strategy

### MVP First (P1 stories only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phases 3–7 (US1, US3, US4, US5, US7 — all P1)
4. **STOP and VALIDATE**: quickstart.md scenarios 1–7 pass against a real
   Postgres, even without the dedicated `AuthorityMatrix`/register read
   endpoints (US1/US3's handlers can query the stream directly until US2 lands)
5. This is a legitimate MVP checkpoint — every P1 story is independently
   functional without the P2 read-model phases

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 → Test independently (grant + duplicate rejection)
3. US3 → Test independently (cascade + duplicate rejection)
4. US4 → Test independently (revise + both rejection paths + event publish)
5. US5 → Test independently (revoke + event publish)
6. US7 → Test independently (escalation request)
7. US2 → Retrofit US1/US3 onto the real snapshot, test SC-001 directly
8. US6, US8 → Historical registers, parallel with each other
9. US9 → Verification-only checkpoint, no new code
10. Polish

### Notes

- [P] tasks = different files, no dependencies.
- Every `Clarified 2026-08-09` FR (010, 011, 012) has an explicit test task —
  don't let these silently regress to "just the happy path" during implementation.
- Commit after each task or logical group; one slice per work session/PR per
  constitution Quality Gates.
