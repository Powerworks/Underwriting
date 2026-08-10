---

description: "Task list for Submission Intake (002)"

---

# Tasks: Submission Intake

**Input**: Design documents from `specs/002-submission-intake/` — `requirements.md`, `design.md`, `research.md`, `.progress.md`.

**Tests**: Included, non-negotiably. Constitution Principle IV ("Test-First, Three Layers") applies to every task in this file — no "make it work first, test later" phase exists in this project's workflow. Each implementation task is preceded or paired with its own test task; no task defers testing to a later phase.

**Organization**: Phases mirror `001-authority-administration/tasks.md`'s structure (Setup → Foundational → one phase per command/automation/read-model grouping → Polish), not Ralph's default POC-first shape — per explicit user instruction this session (constitution Principle IV has no "skip tests" carve-out). Per-task fields (Do/Files/Done when/Verify/Commit) follow Ralph Specum's richer format.

## Format: `[ID] [P?] [VERIFY?] Description`

- **[P]**: Can run in parallel (zero file overlap with adjacent tasks, no output dependency)
- **[VERIFY]**: Quality/E2E checkpoint, always sequential, delegated to qa-engineer

## Path Conventions

New module under the existing single-solution layout (`src/BrokerConnect.slnx`, matching 001):

```
src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.{Api,Domain,Infrastructure,Contracts}/
tests/Modules/SubmissionIntake/{SubmissionIntake.Domain.Tests,SubmissionIntake.Api.Tests,SubmissionIntake.IntegrationTests}/
```

Schema name: `submissionintake`. Dev server: `dotnet run --project src/Api.Host/Api.Host.csproj` (`ASPNETCORE_ENVIRONMENT=Development`), base URL `http://localhost:5132`, health `GET http://localhost:5132/health` → `{"status":"Healthy"}`. Full test command: `dotnet test src/BrokerConnect.slnx`.

---

## Phase 1: Setup

**Purpose**: Scaffold the module's four projects + three test projects, per `design.md`'s File Structure section. Mirrors `001`'s Phase 1, but the solution and `Api.Host` already exist — this phase only adds new projects to them.

- [x] 1.1 Create `BrokerConnect.Modules.SubmissionIntake.{Api,Domain,Infrastructure,Contracts}` project stubs
  - **Do**: 1. `dotnet new classlib` for each of the 4 projects under `src/Modules/SubmissionIntake/`. 2. Add project references: Api→Domain, Api→Infrastructure, Infrastructure→Domain, Contracts standalone. 3. Set `<TargetFramework>net10.0</TargetFramework>` in each `.csproj` (constitution Technology Constraints).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.{Api,Domain,Infrastructure,Contracts}/*.csproj`
  - **Done when**: All 4 `.csproj` files exist with correct references and TFM
  - **Verify**: `find src/Modules/SubmissionIntake -name "*.csproj" | wc -l | grep -q 4 && echo PASS`
  - **Commit**: `chore(submission-intake): scaffold module projects`

- [x] 1.2 [P] Create `SubmissionIntake.{Domain,Api}.Tests` and `SubmissionIntake.IntegrationTests` project stubs
  - **Do**: 1. `dotnet new xunit` for each of the 3 test projects under `tests/Modules/SubmissionIntake/`. 2. Reference the corresponding Api/Domain project(s) from each.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.{Domain,Api}.Tests/*.csproj`, `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/*.csproj`
  - **Done when**: All 3 test `.csproj` files exist and reference the module correctly
  - **Verify**: `find tests/Modules/SubmissionIntake -name "*.csproj" | wc -l | grep -q 3 && echo PASS`
  - **Commit**: `chore(submission-intake): scaffold test projects`

- [ ] 1.3 Add all 7 new projects to `src/BrokerConnect.slnx`
  - **Do**: Add `<Project Path="...">` entries for the 4 module projects and 3 test projects, under a `/Modules/SubmissionIntake/` solution folder (mirrors 001's `/Modules/AuthorityAdministration/` folder).
  - **Files**: `src/BrokerConnect.slnx`
  - **Done when**: `dotnet sln src/BrokerConnect.slnx list` shows all 7 new projects
  - **Verify**: `dotnet sln src/BrokerConnect.slnx list | grep -c SubmissionIntake | grep -q 7 && echo PASS`
  - **Commit**: `chore(submission-intake): add module projects to solution`

- [ ] 1.4 [P] Add package references (`WolverineFx.Http`/`WolverineFx.Marten`/`Marten` to Api; `Polly` to Infrastructure; `xunit`/`Shouldly`/`NSubstitute` to both `.Tests` projects; `Testcontainers.PostgreSql` to `.IntegrationTests`)
  - **Do**: 1. Pin the same validated versions as 001 (`WolverineFx*`/`Marten` 6.22.0/9.19.0, constitution Technology Constraints — re-verify still resolve). 2. Add `Polly` to Infrastructure. 3. Add `xunit`/`Shouldly`/`NSubstitute` (MIT, per Principle IV) to both unit test projects. 4. Add `Testcontainers.PostgreSql` to IntegrationTests.
  - **Files**: the 7 `.csproj` files from 1.1/1.2
  - **Done when**: `dotnet restore src/BrokerConnect.slnx` succeeds
  - **Verify**: `dotnet restore src/BrokerConnect.slnx && echo PASS`
  - **Commit**: `chore(submission-intake): add package references`

- [ ] 1.5 [VERIFY] Quality checkpoint: solution builds with empty stubs
  - **Do**: Run `dotnet build src/BrokerConnect.slnx`
  - **Verify**: `dotnet build src/BrokerConnect.slnx && echo PASS`
  - **Done when**: Build succeeds, zero errors
  - **Commit**: None

**Checkpoint**: `dotnet build src/BrokerConnect.slnx` succeeds with empty stubs.

---

## Phase 2: Foundational

**Purpose**: Shared aggregates/events every later phase depends on — mirrors 001 Phase 2. Only skeletons + the 12 event records land here; each aggregate's individual `Apply` overloads land in the phase that introduces the triggering event (matching 001's T007→T015/T024/T032/T038 pattern), not all at once.

**⚠️ No later phase can begin until this phase is complete.**

- [ ] 2.1 [P] `SubmissionIntakeEvents.cs` — all 12 event records, fields verbatim from `requirements.md`'s Event Model Detail
  - **Do**: Create one `sealed record` per board event (`BrokerSubmissionReceived`, `SubmissionRoutingRejected`, `SubmissionNormalized`, `SubmissionNormalizationFailed`, `SubmissionManuallyCorrected`, `PotentialDuplicateSubmissionDetected`, `SubmissionSuperseded`, `SubmissionConfirmedDistinct`, `BaselinePremiumGenerated`, `PricingBaselineAccepted`, `PricingBaselineOverridden`, `PricingModelVersionDeployed`), fields/types exactly as the Event Model Detail table lists — no invented fields (constitution Principle III).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Events/SubmissionIntakeEvents.cs`
  - **Done when**: 12 records exist, field-for-field matching the appendix
  - **Verify**: `dotnet build src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/*.csproj && echo PASS`
  - **Commit**: `feat(submission-intake): add domain event records`
  - _Requirements: FR-1 through FR-12_
  - _Design: Board-Sourced vs. Inferred Elements table, Components_

- [ ] 2.2 [P] Domain test: `Submission.Create(BrokerSubmissionReceived)` initializes stream state
  - **Do**: Write an xUnit + Shouldly test (no mocks, per Principle IV Layer 1) asserting `Submission.Create` from a `BrokerSubmissionReceived` sets `SubmissionId`/`BrokerFirmId`/`RawPayloadRef` and leaves normalization/routing/pricing fields unset. Must fail (type doesn't exist yet).
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests/SubmissionTests.cs`
  - **Done when**: Test exists and fails to compile/fails assertion
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter Create_from_BrokerSubmissionReceived 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - Submission.Create from BrokerSubmissionReceived`
  - _Requirements: FR-1, AC-1.1_

- [ ] 2.3 `Submission` aggregate skeleton + `Create`/`Apply(BrokerSubmissionReceived)`
  - **Do**: 1. Create `Submission` class per `design.md` Components' Apply-computed state table (`[JsonInclude]`/`[JsonConstructor]`, `[JsonIgnore] Id` alias over `SubmissionId` per 001's discovered Marten gotcha). 2. Implement `Create(BrokerSubmissionReceived)`. 3. Leave `Apply` for the other 10 stream events as later additions (each event's own phase).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Aggregates/Submission.cs`
  - **Done when**: 2.2's test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter Create_from_BrokerSubmissionReceived && echo PASS`
  - **Commit**: `feat(submission-intake): green - Submission aggregate skeleton and Create`
  - _Design: Components — `Submission` aggregate_

- [ ] 2.4 [P] `PricingModel` aggregate skeleton (no `Apply` yet)
  - **Do**: Create `PricingModel` class with a generated `Guid Id` (design's Unresolved Q3: generated id chosen over keying on `modelVersion` string, since the board marks no field `id`) and `[JsonInclude]`/`[JsonConstructor]`. `Create`/`Apply(PricingModelVersionDeployed)` deferred to Phase 9.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Aggregates/PricingModel.cs`
  - **Done when**: Class compiles, no `Apply` logic yet
  - **Verify**: `dotnet build src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/*.csproj && echo PASS`
  - **Commit**: `feat(submission-intake): add PricingModel aggregate skeleton`
  - _Design: Components — `PricingModel` aggregate, Unresolved Questions (stream identity)_

- [ ] 2.5 `Module.cs` (`SubmissionIntakeModule : IMartenModuleConfiguration`) + register in `Program.cs`
  - **Do**: 1. Create `SubmissionIntakeModule` setting `options.Events.DatabaseSchemaName = "submissionintake"` (no snapshot registration yet — Technical Decisions: `Submission` has no Inline snapshot, nothing queries it by id directly). 2. Add to `Api.Host/Program.cs`'s `modules` array alongside `AuthorityAdministrationModule`.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Module.cs`, `src/Api.Host/Program.cs`
  - **Done when**: `SubmissionIntakeModule` is discovered by `Api.Host` at startup
  - **Verify**: `dotnet build src/Api.Host/Api.Host.csproj && echo PASS`
  - **Commit**: `feat(submission-intake): register module in Api.Host`
  - _Design: Technical Decisions (`Submission` Inline snapshot: none), Existing Patterns to Follow_

- [ ] 2.6 [VERIFY] Quality checkpoint: build + domain tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests`
  - **Verify**: Both commands exit 0
  - **Done when**: No build errors, 2.2's test green
  - **Commit**: `chore(submission-intake): pass foundational quality checkpoint` (if fixes needed)

**Checkpoint**: Foundation ready — vertical slices can now proceed.

---

## Phase 3: Submission Receipt (US-1)

**Goal**: `ReceiveBrokerSubmission` always succeeds and starts the `Submission` stream (AC-1.1) — the entry point every downstream automation in later phases depends on. **This phase is the first end-to-end working slice.**

- [ ] 3.1 [P] Layer 2 handler test: `ReceiveBrokerSubmissionHandler` always succeeds
  - **Do**: Write an xUnit + Shouldly + NSubstitute test asserting the handler appends `BrokerSubmissionReceived` and returns `201`/`SubmissionId` for any well-formed request — no validation beyond `[Required]` presence (AC-1.1: "always succeeds if the payload arrives at all"). Must fail (handler doesn't exist).
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests/ReceiveBrokerSubmissionHandlerTests.cs`
  - **Done when**: Test exists and fails
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests --filter ReceiveBrokerSubmission 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - ReceiveBrokerSubmission always succeeds`
  - _Requirements: FR-1, AC-1.1_

- [ ] 3.2 `ReceiveBrokerSubmission` request/response records + `[WolverinePost]` handler
  - **Do**: 1. `ReceiveBrokerSubmissionRequest`/`Response` records per `design.md` Interfaces section. 2. `[WolverinePost("/api/v1/submission-intake/submissions")]` handler: `StartStream<Submission>(BrokerSubmissionReceived)`, no validation beyond DataAnnotations. 3. Structured `ILogger` success log (Principle X).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Commands/ReceiveBrokerSubmission/ReceiveBrokerSubmission.cs`, `.../ReceiveBrokerSubmissionHandler.cs`
  - **Done when**: 3.1's test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests --filter ReceiveBrokerSubmission && echo PASS`
  - **Commit**: `feat(submission-intake): green - ReceiveBrokerSubmission command`
  - _Requirements: FR-1, AC-1.1_
  - _Design: Commands table, Interfaces, Security Considerations (system/service-to-service auth, `[Authorize]` TODO)_

- [ ] 3.3 [VERIFY] Quality checkpoint: build + Layer 1/2 tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests`
  - **Verify**: All commands exit 0
  - **Done when**: No build/test errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

- [ ] 3.4 [VERIFY] VE1 E2E startup: launch `Api.Host` in Development, wait for `/health`
  - **Do**: 1. `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Api.Host/Api.Host.csproj &`. 2. Record PID to `/tmp/ve-pids.txt`. 3. Poll `http://localhost:5132/health` up to 60s.
  - **Verify**: `curl -sf http://localhost:5132/health | grep -q '"status":"Healthy"' && echo PASS`
  - **Done when**: Server responding on 5132
  - **Commit**: None

- [ ] 3.5 [VERIFY] VE2 E2E check: `POST /api/v1/submission-intake/submissions` returns `201` + `submissionId`
  - **Do**: `curl -s -X POST http://localhost:5132/api/v1/submission-intake/submissions -H "Content-Type: application/json" -d '{"brokerFirmId":"acme","submittingContact":"jane@acme.com","rawPayload":{},"sourceChannel":"api"}'` — assert response has a `submissionId` GUID field.
  - **Verify**: `curl -s -o /tmp/resp.json -w "%{http_code}" -X POST http://localhost:5132/api/v1/submission-intake/submissions -H "Content-Type: application/json" -d '{"brokerFirmId":"acme","submittingContact":"jane@acme.com","rawPayload":{},"sourceChannel":"api"}' | grep -q 201 && jq -e .submissionId /tmp/resp.json && echo PASS`
  - **Done when**: Real end-to-end submission receipt proven against the running host (not just compiling)
  - **Commit**: None

- [ ] 3.6 [VERIFY] VE3 E2E cleanup: stop server, free port 5132
  - **Do**: 1. `kill $(cat /tmp/ve-pids.txt) 2>/dev/null; sleep 2; kill -9 $(cat /tmp/ve-pids.txt) 2>/dev/null || true`. 2. `lsof -ti :5132 | xargs -r kill 2>/dev/null || true`. 3. `rm -f /tmp/ve-pids.txt`.
  - **Verify**: `! lsof -ti :5132 && echo PASS`
  - **Done when**: No process on 5132, PID file removed
  - **Commit**: None

**Checkpoint**: First end-to-end working slice — `ReceiveBrokerSubmission` proven against a real running host (POC-equivalent milestone).

---

## Phase 4: ADEPT Normalization + Submission/Exception Queues (US-3, US-4)

**Goal**: Async ADEPT normalization (IR-001) produces `SubmissionNormalized`/`SubmissionNormalizationFailed`; `SubmissionQueue` (underwriter worklist) and `SubmissionExceptionQueue` (ops) read models come online.

- [ ] 4.1 [P] `IBrokerAdeptClient`/`BrokerAdeptClient` (IR-001, Polly retry + circuit breaker)
  - **Do**: Implement per `design.md` Interfaces (`NormalizeAsync(rawPayloadRef, ct) → AdeptNormalizationResult`), Polly-wrapped (ADR-008), registered in DI.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Infrastructure/BrokerAdeptClient.cs`
  - **Done when**: Interface + impl compile, DI-registered
  - **Verify**: `dotnet build src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Infrastructure/*.csproj && echo PASS`
  - **Commit**: `feat(submission-intake): add BrokerAdeptClient (IR-001)`
  - _Design: Interfaces, Dependencies (IR-001)_

- [ ] 4.2 [P] Domain test: `Submission.Apply(SubmissionNormalized)` and `Apply(SubmissionNormalizationFailed)`
  - **Do**: Assert `Apply(SubmissionNormalized)` sets `ClassOfBusiness`/`Territory`/`NamedInsured`/`LineSizeSought`/`KeyTerms`/`EffectiveDateRequested`/`NormalizationStatus`; `Apply(SubmissionNormalizationFailed)` sets `NormalizationStatus` to the failure state, preserves `RawPayloadRef` (AC-4.1: never discarded). Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests/SubmissionTests.cs`
  - **Done when**: Tests exist and fail
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter "SubmissionNormalized|SubmissionNormalizationFailed" 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - Submission.Apply normalization events`
  - _Requirements: FR-3, FR-4, AC-3.1, AC-4.1_

- [ ] 4.3 `Submission.Apply(SubmissionNormalized)`/`Apply(SubmissionNormalizationFailed)` impl
  - **Do**: Add both `Apply` overloads to `Submission` (mutates unconditionally, per Principle I — no guards here).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Aggregates/Submission.cs`
  - **Done when**: 4.2's tests pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter "SubmissionNormalized|SubmissionNormalizationFailed" && echo PASS`
  - **Commit**: `feat(submission-intake): green - Submission.Apply normalization events`

- [ ] 4.4 Layer 3 tests: `NormalizeSubmissionViaAdeptHandler` success / failure / idempotency
  - **Do**: Testcontainers-backed Postgres (real `AggregateStreamAsync`/`FetchForWriting`), `IBrokerAdeptClient` mocked (NSubstitute) at the boundary per Layer 3 guidance. Cover: (1) success → `SubmissionNormalized` appended; (2) `AdeptNormalizationResult.Succeeded == false` → `SubmissionNormalizationFailed` appended, raw payload preserved; (3) redelivered `BrokerSubmissionReceived` when `NormalizationStatus` already set → no duplicate append (Architecture Constraints idempotency). Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/NormalizeSubmissionViaAdeptHandlerTests.cs`
  - **Done when**: 3 test methods exist and fail
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter NormalizeSubmissionViaAdept 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - NormalizeSubmissionViaAdept success/failure/idempotency`
  - _Requirements: FR-3, FR-4, AC-3.1, AC-4.1_
  - _Design: Automations table, Test Strategy Layer 3_

- [ ] 4.5 `NormalizeSubmissionViaAdeptHandler` impl
  - **Do**: 1. Trigger on `BrokerSubmissionReceived` and `SubmissionManuallyCorrected` (guard: `resubmittedForNormalization == true` — wired fully in Phase 8, subscribe now, guard added then). 2. Idempotency guard: no-op if `NormalizationStatus` already set. 3. Call `IBrokerAdeptClient.NormalizeAsync`, append `SubmissionNormalized`/`SubmissionNormalizationFailed` per result.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Automations/NormalizeSubmissionViaAdept/NormalizeSubmissionViaAdeptHandler.cs`
  - **Done when**: 4.4's tests pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter NormalizeSubmissionViaAdept && echo PASS`
  - **Commit**: `feat(submission-intake): green - NormalizeSubmissionViaAdept automation`

- [ ] 4.6 [VERIFY] Quality checkpoint: build + Layer 1/3 tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter NormalizeSubmissionViaAdept`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

- [ ] 4.7 [P] `SubmissionQueue` doc + projector (`Handle(SubmissionNormalized)`) + Layer 3 test
  - **Do**: 1. Layer 3 test first: submitting `SubmissionNormalized` produces a `SubmissionQueue` row with `namedInsured` (Technical Decisions gap-fix), `status` from `normalizationStatus`. 2. `SubmissionQueue` document class (field list per `design.md` Screens DTO). 3. `SubmissionQueueProjector` (Wolverine-subscriber pattern per Existing Patterns to Follow, not `MultiStreamProjection` — keyed by `submissionId` which is the stream id).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/SubmissionQueue/SubmissionQueue.cs`, `.../SubmissionQueueProjector.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/SubmissionQueueProjectorTests.cs`
  - **Done when**: Test passes against real Postgres
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter SubmissionQueueProjector && echo PASS`
  - **Commit**: `feat(submission-intake): SubmissionQueue projector reacts to SubmissionNormalized`
  - _Requirements: FR-3, AC-3.1_
  - _Design: Read Models table, Screens (namedInsured gap-fix), Technical Decisions_

- [ ] 4.8 [P] `GetSubmissionQueue` query handler + DTOs + Layer 2 test
  - **Do**: 1. `SubmissionQueueResponse`/`SubmissionQueueItem` DTOs per `design.md` Screens. 2. `[WolverineGet("/api/v1/submission-intake/submission-queue")]` — filters `brokerFirmId?`/`classOfBusiness?`/`search?`, paginated (`page`/`pageSize`, default 20/max 200 per 001's precedent, flagged as a guess). 3. Layer 2 test covering filter + pagination logic in-memory.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/SubmissionQueue/GetSubmissionQueue.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests/GetSubmissionQueueTests.cs`
  - **Done when**: Test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests --filter GetSubmissionQueue && echo PASS`
  - **Commit**: `feat(submission-intake): GetSubmissionQueue query endpoint`
  - _Design: Screens (SubmissionQueue DTO), Performance Considerations (pagination from v1)_

- [ ] 4.9 [P] `SubmissionExceptionQueue` doc + projector (`Handle(SubmissionNormalizationFailed)`) + Layer 3 test
  - **Do**: Same pattern as 4.7: test first, then doc + projector (field list per Event Model Detail's `SubmissionExceptionQueue`).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/SubmissionExceptionQueue/SubmissionExceptionQueue.cs`, `.../SubmissionExceptionQueueProjector.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/SubmissionExceptionQueueProjectorTests.cs`
  - **Done when**: Test passes against real Postgres
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter SubmissionExceptionQueueProjector && echo PASS`
  - **Commit**: `feat(submission-intake): SubmissionExceptionQueue projector reacts to SubmissionNormalizationFailed`
  - _Requirements: FR-4, AC-4.1_

- [ ] 4.10 [P] `GetSubmissionExceptionQueue` query handler + DTO + Layer 2 test
  - **Do**: `[WolverineGet("/api/v1/submission-intake/submission-exception-queue")]` (route inferred, consistent with 4.8's convention — not board-specified), paginated, DTO never exposes the aggregate directly (Principle VII).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/SubmissionExceptionQueue/GetSubmissionExceptionQueue.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests/GetSubmissionExceptionQueueTests.cs`
  - **Done when**: Test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests --filter GetSubmissionExceptionQueue && echo PASS`
  - **Commit**: `feat(submission-intake): GetSubmissionExceptionQueue query endpoint`

- [ ] 4.11 [VERIFY] Quality checkpoint: build + all module tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

**Checkpoint**: Receipt → normalization → both queues fully functional and independently testable.

---

## Phase 5: Duplicate Detection & Resolution (US-6, US-7, US-8)

**Goal**: Exact-field duplicate detection (DEC-010) on successful normalization, plus the two underwriter/ops resolution commands.

- [ ] 5.1 [P] Domain test: `Submission.Apply` for `PotentialDuplicateSubmissionDetected` / `SubmissionSuperseded` / `SubmissionConfirmedDistinct`
  - **Do**: Assert `IsPossibleDuplicate`/`SuspectedOriginalSubmissionId` set by the first; `SupersededBySubmissionId` by the second; `IsConfirmedDistinct` by the third. Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests/SubmissionTests.cs`
  - **Done when**: Tests exist and fail
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter "PotentialDuplicate|Superseded|ConfirmedDistinct" 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - Submission.Apply duplicate-resolution events`
  - _Requirements: FR-6, FR-7, FR-8, AC-6.1, AC-7.1, AC-8.1_

- [ ] 5.2 `Submission.Apply` impl for the three duplicate-resolution events
  - **Do**: Add the three `Apply` overloads.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Aggregates/Submission.cs`
  - **Done when**: 5.1's tests pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter "PotentialDuplicate|Superseded|ConfirmedDistinct" && echo PASS`
  - **Commit**: `feat(submission-intake): green - Submission.Apply duplicate-resolution events`

- [ ] 5.3 Layer 3 tests: `DetectPotentialDuplicateOnNormalizationHandler` match / no-match / idempotency
  - **Do**: Real Postgres. Seed a `SubmissionQueue` entry with matching `classOfBusiness`+`territory`+`namedInsured` (DEC-010 exact match), assert `PotentialDuplicateSubmissionDetected` appended on the new submission's stream; assert no-match case appends nothing; assert already-`IsPossibleDuplicate`-true submission isn't re-flagged (idempotency). Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/DetectPotentialDuplicateOnNormalizationHandlerTests.cs`
  - **Done when**: 3 methods exist and fail
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter DetectPotentialDuplicate 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - DetectPotentialDuplicateOnNormalization`
  - _Requirements: FR-6, AC-6.1_
  - _Design: Automations table (DEC-010), Performance Considerations (indexed classOfBusiness/territory)_

- [ ] 5.4 `DetectPotentialDuplicateOnNormalizationHandler` impl
  - **Do**: 1. Trigger on `SubmissionNormalized` (success only). 2. Query `SubmissionQueue` for exact-field match against other non-superseded, non-confirmed-distinct open submissions. 3. Append `PotentialDuplicateSubmissionDetected` if found; idempotency guard on `IsPossibleDuplicate`.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Automations/DetectPotentialDuplicateOnNormalization/DetectPotentialDuplicateOnNormalizationHandler.cs`
  - **Done when**: 5.3's tests pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter DetectPotentialDuplicate && echo PASS`
  - **Commit**: `feat(submission-intake): green - DetectPotentialDuplicateOnNormalization automation`

- [ ] 5.5 [P] `SubmissionQueueProjector` gap-fix: `Handle(PotentialDuplicateSubmissionDetected)`/`Handle(SubmissionSuperseded)`/`Handle(SubmissionConfirmedDistinct)` + Layer 3 test
  - **Do**: Test first (asserts `isPossibleDuplicate`/`suspectedOriginalSubmissionId` flip via these 3 events — the Technical Decisions gap-fix, since the board wires only `SubmissionNormalized`/`SubmissionRoutingRejected`), then the 3 `Handle` methods.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/SubmissionQueue/SubmissionQueueProjector.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/SubmissionQueueProjectorTests.cs`
  - **Done when**: New test methods pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter SubmissionQueueProjector && echo PASS`
  - **Commit**: `feat(submission-intake): SubmissionQueue projector gap-fix for duplicate-resolution events`
  - _Design: Technical Decisions (isPossibleDuplicate/suspectedOriginalSubmissionId gap-fix)_

- [ ] 5.6 [VERIFY] Quality checkpoint: build + tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

- [ ] 5.7 [P] Layer 3 tests: `SupersedeSubmissionHandler` + `ConfirmSubmissionDistinctHandler` happy paths
  - **Do**: Assert `SupersedeSubmission` appends `SubmissionSuperseded` on the **original**'s stream; `ConfirmSubmissionDistinct` appends `SubmissionConfirmedDistinct` on the **flagged/new**'s stream (per `design.md` Commands table). Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/DuplicateResolutionCommandsTests.cs`
  - **Done when**: 2 methods exist and fail
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter DuplicateResolutionCommands 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - SupersedeSubmission and ConfirmSubmissionDistinct`
  - _Requirements: FR-7, FR-8, AC-7.1, AC-8.1_

- [ ] 5.8 `SupersedeSubmissionHandler` + `ConfirmSubmissionDistinctHandler` impl
  - **Do**: 1. `POST /api/v1/submission-intake/submissions/{originalSubmissionId}/supersede` → appends on original's stream. 2. `POST /api/v1/submission-intake/submissions/{submissionId}/confirm-distinct` → appends on the flagged/new's stream. Both are underwriter/ops role-gated (`[Authorize]` TODO comment, ADR-010 blocked, per 001's precedent).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Commands/SupersedeSubmission/SupersedeSubmission.cs`, `.../SupersedeSubmissionHandler.cs`, `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Commands/ConfirmSubmissionDistinct/ConfirmSubmissionDistinct.cs`, `.../ConfirmSubmissionDistinctHandler.cs`
  - **Done when**: 5.7's tests pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter DuplicateResolutionCommands && echo PASS`
  - **Commit**: `feat(submission-intake): green - SupersedeSubmission and ConfirmSubmissionDistinct commands`
  - _Design: Commands table, Security Considerations_

- [ ] 5.9 [VERIFY] Quality checkpoint: build + all module tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

**Checkpoint**: Duplicate detection + both resolution paths independently functional.

---

## Phase 6: Broker Panel Routing (US-2)

**Goal**: `RouteSubmissionOnReceipt` (DEC-009 hard block) + `BrokerAuthorizationExceptionLog`. Implements the interim/stubbed approach from `design.md` Implementation Step 8 for the unresolved data-source question — not left undone.

- [ ] 6.1 [P] Domain test: `Submission.Apply(SubmissionRoutingRejected)`
  - **Do**: Assert `IsRoutingRejected` set. Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests/SubmissionTests.cs`
  - **Done when**: Test exists and fails
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter SubmissionRoutingRejected 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - Submission.Apply SubmissionRoutingRejected`
  - _Requirements: FR-2, AC-2.1_

- [ ] 6.2 `Submission.Apply(SubmissionRoutingRejected)` impl
  - **Do**: Add the `Apply` overload.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Aggregates/Submission.cs`
  - **Done when**: 6.1's test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter SubmissionRoutingRejected && echo PASS`
  - **Commit**: `feat(submission-intake): green - Submission.Apply SubmissionRoutingRejected`

- [ ] 6.3 [P] `IBrokerPanelAuthorizationSource` stub interface + always-authorized default implementation
  - **Do**: Per design's Unresolved Questions (`RouteSubmissionOnReceipt`'s data source not confirmed — likely Authority Administration, no dependency edge confirms it): define `IBrokerPanelAuthorizationSource.IsAuthorizedAsync(brokerFirmId, cellIdHint, classOfBusinessHint, ct) → bool`, register a stub implementation that always returns `true` (never blocks) behind DI, with an inline comment flagging it as a stand-in pending confirmation — so `RouteSubmissionOnReceipt` is fully testable now and swappable later without touching the handler.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Infrastructure/IBrokerPanelAuthorizationSource.cs`
  - **Done when**: Interface + stub compile, DI-registered
  - **Verify**: `dotnet build src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Infrastructure/*.csproj && echo PASS`
  - **Commit**: `feat(submission-intake): stub IBrokerPanelAuthorizationSource pending data-source confirmation`
  - _Design: Unresolved Questions (RouteSubmissionOnReceipt data source), Implementation Step 8_

- [ ] 6.4 Layer 3 tests: `RouteSubmissionOnReceiptHandler` authorized / rejected paths
  - **Do**: Real Postgres, `IBrokerPanelAuthorizationSource` mocked (NSubstitute) to return `true`/`false`. Assert authorized → no-op; rejected → `SubmissionRoutingRejected` appended with `requestedCellId`/`requestedClassOfBusiness` mirroring the command's hint fields. Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/RouteSubmissionOnReceiptHandlerTests.cs`
  - **Done when**: 2 methods exist and fail
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter RouteSubmissionOnReceipt 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - RouteSubmissionOnReceipt authorized/rejected`
  - _Requirements: FR-2, AC-2.1_

- [ ] 6.5 `RouteSubmissionOnReceiptHandler` impl
  - **Do**: 1. Trigger on `BrokerSubmissionReceived`. 2. Call `IBrokerPanelAuthorizationSource.IsAuthorizedAsync`. 3. Append `SubmissionRoutingRejected` if unauthorized (DEC-009: hard block, no broker feedback); no-op otherwise.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Automations/RouteSubmissionOnReceipt/RouteSubmissionOnReceiptHandler.cs`
  - **Done when**: 6.4's tests pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter RouteSubmissionOnReceipt && echo PASS`
  - **Commit**: `feat(submission-intake): green - RouteSubmissionOnReceipt automation`

- [ ] 6.6 [P] `BrokerAuthorizationExceptionLog` doc + projector + Layer 3 test
  - **Do**: Test first, then doc + projector (`Handle(SubmissionRoutingRejected)`), field list per Event Model Detail.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/BrokerAuthorizationExceptionLog/BrokerAuthorizationExceptionLog.cs`, `.../BrokerAuthorizationExceptionLogProjector.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/BrokerAuthorizationExceptionLogProjectorTests.cs`
  - **Done when**: Test passes against real Postgres
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter BrokerAuthorizationExceptionLogProjector && echo PASS`
  - **Commit**: `feat(submission-intake): BrokerAuthorizationExceptionLog projector`
  - _Requirements: FR-2, AC-2.1_

- [ ] 6.7 [P] `GetBrokerAuthorizationExceptionLog` query handler + DTO + Layer 2 test
  - **Do**: `[WolverineGet("/api/v1/submission-intake/broker-authorization-exception-log")]` (route inferred, consistent convention), paginated, visible to ops/broker-relationship-management (never underwriter queue, per US-2 narrative).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/BrokerAuthorizationExceptionLog/GetBrokerAuthorizationExceptionLog.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests/GetBrokerAuthorizationExceptionLogTests.cs`
  - **Done when**: Test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests --filter GetBrokerAuthorizationExceptionLog && echo PASS`
  - **Commit**: `feat(submission-intake): GetBrokerAuthorizationExceptionLog query endpoint`

- [ ] 6.8 `SubmissionQueueProjector`: `Handle(SubmissionRoutingRejected)` retraction + Layer 3 test
  - **Do**: Test first — a submission that already has a `SubmissionQueue` row (routing ran in parallel with normalization, per design's parallel-not-sequential decision) gets that row deleted/suppressed on a late `SubmissionRoutingRejected` (Edge Cases: "never appears in any underwriter's queue"). Then the `Handle` method (delete-if-exists).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/SubmissionQueue/SubmissionQueueProjector.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/SubmissionQueueProjectorTests.cs`
  - **Done when**: New test method passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter SubmissionQueueProjector && echo PASS`
  - **Commit**: `feat(submission-intake): SubmissionQueue projector retracts rows on late SubmissionRoutingRejected`
  - _Design: Edge Cases (SubmissionRoutingRejected after normalization already succeeded)_

- [ ] 6.9 [VERIFY] Quality checkpoint: build + all module tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

**Checkpoint**: Routing rejection + exception log independently functional, reconciled with the underwriter-queue retraction rule.

---

## Phase 7: Baseline Pricing (US-9)

**Goal**: AI baseline pricing (IR-005) on successful normalization; `PricedSubmissionView` (the "auditing the AI-generated model" screen driver).

- [ ] 7.1 [P] `IRatingEngineClient`/`RatingEngineClient` (IR-005, Polly retry + circuit breaker)
  - **Do**: Implement per `design.md` Interfaces (`GetBaselinePricingAsync(...) → BaselinePricingResult`), Polly-wrapped, DI-registered.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Infrastructure/RatingEngineClient.cs`
  - **Done when**: Interface + impl compile
  - **Verify**: `dotnet build src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Infrastructure/*.csproj && echo PASS`
  - **Commit**: `feat(submission-intake): add RatingEngineClient (IR-005)`
  - _Design: Interfaces, Dependencies (IR-005)_

- [ ] 7.2 [P] Domain test: `Submission.Apply(BaselinePremiumGenerated)`
  - **Do**: Assert `BaselinePremium`/`RiskFactorSummary`/`ModelVersion` set. Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests/SubmissionTests.cs`
  - **Done when**: Test exists and fails
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter BaselinePremiumGenerated 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - Submission.Apply BaselinePremiumGenerated`
  - _Requirements: FR-9, AC-9.1_

- [ ] 7.3 `Submission.Apply(BaselinePremiumGenerated)` impl
  - **Do**: Add the `Apply` overload.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Aggregates/Submission.cs`
  - **Done when**: 7.2's test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter BaselinePremiumGenerated && echo PASS`
  - **Commit**: `feat(submission-intake): green - Submission.Apply BaselinePremiumGenerated`

- [ ] 7.4 Layer 3 tests: `GenerateBaselinePremiumOnNormalizationHandler` success + idempotency
  - **Do**: Real Postgres, `IRatingEngineClient` mocked. Assert success on `SubmissionNormalized` (success only, S1a.5) appends `BaselinePremiumGenerated`; assert already-`BaselinePremium`-set submission isn't re-priced. Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/GenerateBaselinePremiumOnNormalizationHandlerTests.cs`
  - **Done when**: 2 methods exist and fail
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter GenerateBaselinePremium 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - GenerateBaselinePremiumOnNormalization`
  - _Requirements: FR-9, AC-9.1_

- [ ] 7.5 `GenerateBaselinePremiumOnNormalizationHandler` impl
  - **Do**: 1. Trigger on `SubmissionNormalized` (success only — an incomplete/failed normalization never gets priced, per S1a.5). 2. Idempotency guard on `BaselinePremium`. 3. Call `IRatingEngineClient.GetBaselinePricingAsync`, append `BaselinePremiumGenerated`.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Automations/GenerateBaselinePremiumOnNormalization/GenerateBaselinePremiumOnNormalizationHandler.cs`
  - **Done when**: 7.4's tests pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter GenerateBaselinePremium && echo PASS`
  - **Commit**: `feat(submission-intake): green - GenerateBaselinePremiumOnNormalization automation`

- [ ] 7.6 [VERIFY] Quality checkpoint: build + tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

- [ ] 7.7 [P] `PricedSubmissionView` doc + projector (3 `Handle` methods) + Layer 3 test
  - **Do**: Test first, then doc + projector reacting to `BaselinePremiumGenerated`, `SubmissionConfirmedDistinct` (board-wired), and the gap-fixed `SubmissionNormalized` (populates `brokerRequestedTerms`, Technical Decisions).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/PricedSubmissionView/PricedSubmissionView.cs`, `.../PricedSubmissionViewProjector.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/PricedSubmissionViewProjectorTests.cs`
  - **Done when**: Tests pass against real Postgres
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter PricedSubmissionViewProjector && echo PASS`
  - **Commit**: `feat(submission-intake): PricedSubmissionView projector`
  - _Design: Read Models table, Technical Decisions (brokerRequestedTerms gap-fix)_

- [ ] 7.8 [P] `GetPricedSubmissionView` query handler + DTO + Layer 2 test
  - **Do**: `[WolverineGet("/api/v1/submission-intake/submissions/{submissionId}/priced-view")]` (route inferred, consistent convention), returns `brokerRequestedTerms` alongside `baselinePremium`/`riskFactorSummary`/`modelVersion`, never the aggregate.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/PricedSubmissionView/GetPricedSubmissionView.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests/GetPricedSubmissionViewTests.cs`
  - **Done when**: Test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests --filter GetPricedSubmissionView && echo PASS`
  - **Commit**: `feat(submission-intake): GetPricedSubmissionView query endpoint`

- [ ] 7.9 [VERIFY] Quality checkpoint: build + all module tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

**Checkpoint**: Baseline pricing + priced-submission audit view independently functional.

---

## Phase 8: Manual Correction (US-5)

**Goal**: Ops/broker remediation path (`CorrectSubmission`), wired to re-trigger normalization via the `resubmittedForNormalization` flag, never inlined into the command handler itself (Principle II).

- [ ] 8.1 [P] Domain test: `Submission.Apply(SubmissionManuallyCorrected)`
  - **Do**: Assert correction metadata recorded, no direct state mutation of normalization fields (the automation, not the event, re-triggers normalization). Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests/SubmissionTests.cs`
  - **Done when**: Test exists and fails
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter SubmissionManuallyCorrected 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - Submission.Apply SubmissionManuallyCorrected`
  - _Requirements: FR-5, AC-5.1_

- [ ] 8.2 `Submission.Apply(SubmissionManuallyCorrected)` impl
  - **Do**: Add the `Apply` overload.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Aggregates/Submission.cs`
  - **Done when**: 8.1's test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter SubmissionManuallyCorrected && echo PASS`
  - **Commit**: `feat(submission-intake): green - Submission.Apply SubmissionManuallyCorrected`

- [ ] 8.3 Layer 3 test: `CorrectSubmissionHandler` appends event; `resubmittedForNormalization=true` re-triggers `NormalizeSubmissionViaAdept`
  - **Do**: Real Postgres, `IBrokerAdeptClient` mocked. Assert `CorrectSubmission` appends `SubmissionManuallyCorrected`; when `resubmittedForNormalization == true`, the already-subscribed `NormalizeSubmissionViaAdeptHandler` (4.5's guard extended in 8.4) fires and re-normalizes. Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/CorrectSubmissionHandlerTests.cs`
  - **Done when**: Test exists and fails
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter CorrectSubmission 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - CorrectSubmission re-triggers normalization`
  - _Requirements: FR-5, AC-5.1_
  - _Design: Commands table (CorrectSubmission — Principle II boundary)_

- [ ] 8.4 `CorrectSubmissionHandler` impl + `resubmittedForNormalization` guard in `NormalizeSubmissionViaAdeptHandler`
  - **Do**: 1. `POST /api/v1/submission-intake/submissions/{submissionId}/corrections` → appends `SubmissionManuallyCorrected`. 2. Extend `NormalizeSubmissionViaAdeptHandler`'s trigger guard: on `SubmissionManuallyCorrected`, only act if `resubmittedForNormalization == true` (already subscribed since 4.5, guard was deferred to here).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Commands/CorrectSubmission/CorrectSubmission.cs`, `.../CorrectSubmissionHandler.cs`, `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Automations/NormalizeSubmissionViaAdept/NormalizeSubmissionViaAdeptHandler.cs`
  - **Done when**: 8.3's test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter CorrectSubmission && echo PASS`
  - **Commit**: `feat(submission-intake): green - CorrectSubmission command and normalization re-trigger guard`

- [ ] 8.5 `SubmissionExceptionQueueProjector`: `Handle(SubmissionManuallyCorrected)` status update + Layer 3 test
  - **Do**: Test first — a correction updates the exception-queue entry's `status` (Technical Decisions gap-fix: otherwise `status` never moves off its initial value). Then the `Handle` method.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/ReadModels/SubmissionExceptionQueue/SubmissionExceptionQueueProjector.cs`, `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/SubmissionExceptionQueueProjectorTests.cs`
  - **Done when**: New test method passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter SubmissionExceptionQueueProjector && echo PASS`
  - **Commit**: `feat(submission-intake): SubmissionExceptionQueue projector reacts to SubmissionManuallyCorrected`
  - _Design: Technical Decisions (SubmissionExceptionQueue fed by SubmissionManuallyCorrected), Edge Cases_

- [ ] 8.6 [VERIFY] Quality checkpoint: build + all module tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

**Checkpoint**: Manual correction path independently functional, correctly deferring re-normalization to the automation.

---

## Phase 9: Pricing Model Deployment (US-12)

**Goal**: `DeployPricingModelVersion` — audit-only reference data on its own, unrelated stream (resolves Unresolved Q3: generated `Guid` stream identity, per Foundational's 2.4 decision).

- [ ] 9.1 [P] Domain test: `PricingModel.Create(PricingModelVersionDeployed)`
  - **Do**: Assert `Id`/`ModelVersion`/`DeployedBy`/`DeployedAt`/`ChangeSummary` set from the event. Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests/PricingModelTests.cs`
  - **Done when**: Test exists and fails
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter PricingModelVersionDeployed 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - PricingModel.Create PricingModelVersionDeployed`
  - _Requirements: FR-12, AC-12.1_

- [ ] 9.2 `PricingModel.Create`/`Apply(PricingModelVersionDeployed)` impl
  - **Do**: Add `Create`/`Apply`.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Aggregates/PricingModel.cs`
  - **Done when**: 9.1's test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter PricingModelVersionDeployed && echo PASS`
  - **Commit**: `feat(submission-intake): green - PricingModel.Create PricingModelVersionDeployed`

- [ ] 9.3 Layer 3 test: `DeployPricingModelVersionHandler` starts a new `PricingModel` stream
  - **Do**: Real Postgres, assert `StartStream<PricingModel>` with a fresh generated `Guid`. Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/DeployPricingModelVersionHandlerTests.cs`
  - **Done when**: Test exists and fails
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter DeployPricingModelVersion 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - DeployPricingModelVersion starts new stream`
  - _Requirements: FR-12, AC-12.1_

- [ ] 9.4 `DeployPricingModelVersionHandler` impl
  - **Do**: `POST /api/v1/submission-intake/pricing-models` → `StartStream<PricingModel>(PricingModelVersionDeployed)`, no read model (per `design.md`: no board dependency edge consumes it). Actuarial/admin role-gated (`[Authorize]` TODO).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Commands/DeployPricingModelVersion/DeployPricingModelVersion.cs`, `.../DeployPricingModelVersionHandler.cs`
  - **Done when**: 9.3's test passes
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter DeployPricingModelVersion && echo PASS`
  - **Commit**: `feat(submission-intake): green - DeployPricingModelVersion command`
  - _Design: Commands table, Security Considerations_

- [ ] 9.5 [VERIFY] Quality checkpoint: build + all module tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

**Checkpoint**: Pricing model deployment independently functional.

---

## Phase 10: Cross-Module Pricing Comparison (US-10, US-11)

**Goal**: `RecordPricingBaselineComparisonOnAssessment` consuming the **assumed** `SubmissionAssessedV1` integration event (Unresolved Q2 — implements the stubbed interim contract from `design.md` Implementation Step 12, per instruction not to leave it undone).

- [ ] 10.1 [P] Domain test: `Submission.Apply(PricingBaselineAccepted)`/`Apply(PricingBaselineOverridden)`
  - **Do**: Assert both set comparison metadata on the stream (fires "alongside AssessSubmission", modeled on this module's stream per `design.md` Board-Sourced vs. Inferred Elements). Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests/SubmissionTests.cs`
  - **Done when**: Tests exist and fail
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter "PricingBaselineAccepted|PricingBaselineOverridden" 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - Submission.Apply pricing-baseline-comparison events`
  - _Requirements: FR-10, FR-11, AC-10.1, AC-11.1_

- [ ] 10.2 `Submission.Apply` impl for both events
  - **Do**: Add both `Apply` overloads.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Domain/Aggregates/Submission.cs`
  - **Done when**: 10.1's tests pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests --filter "PricingBaselineAccepted|PricingBaselineOverridden" && echo PASS`
  - **Commit**: `feat(submission-intake): green - Submission.Apply pricing-baseline-comparison events`

- [ ] 10.3 [P] `SubmissionAssessedV1` consumer contract + `SubmissionPricingState`
  - **Do**: 1. `SubmissionAssessedV1` record (assumed shape: `submissionId`, `proposedPremium`, `underwriterId` minimum, per Unresolved Questions — flagged inline as unconfirmed pending Underwriting Decisioning's own design). 2. `SubmissionPricingState`: live-computed via `AggregateStreamAsync`, never persisted, never a snapshot (Architecture Constraints).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/IntegrationEvents/Consumers/SubmissionAssessedV1.cs`, `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Automations/RecordPricingBaselineComparisonOnAssessment/SubmissionPricingState.cs`
  - **Done when**: Both types compile
  - **Verify**: `dotnet build src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/*.csproj && echo PASS`
  - **Commit**: `feat(submission-intake): add SubmissionAssessedV1 consumer contract and SubmissionPricingState`
  - _Design: Unresolved Questions (SubmissionAssessedV1 shape), Architecture Constraints (automation decision state)_

- [ ] 10.4 Layer 3 tests: `RecordPricingBaselineComparisonOnAssessmentHandler` exact-match / variance / idempotency / missing-baseline guard
  - **Do**: Real Postgres, real `AggregateStreamAsync` into `SubmissionPricingState`. Cover: (1) `proposedPremium == baselinePremium` exactly → `PricingBaselineAccepted`; (2) diverges → `PricingBaselineOverridden` with computed `variance`; (3) redelivered event when a comparison already exists → no duplicate append; (4) `BaselinePremiumGenerated` doesn't exist yet → no-op/defer, not a failure (Error Handling table edge case). Must fail.
  - **Files**: `tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests/RecordPricingBaselineComparisonOnAssessmentHandlerTests.cs`
  - **Done when**: 4 methods exist and fail
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter RecordPricingBaselineComparison 2>&1 | grep -qi "error\|fail" && echo PASS`
  - **Commit**: `test(submission-intake): red - RecordPricingBaselineComparisonOnAssessment`
  - _Requirements: FR-10, FR-11, AC-10.1, AC-11.1_
  - _Design: Error Handling (comparison fires before BaselinePremiumGenerated exists)_

- [ ] 10.5 `RecordPricingBaselineComparisonOnAssessmentHandler` impl + module's `IntegrationEventQueueName`
  - **Do**: 1. Compute `SubmissionPricingState` live via `AggregateStreamAsync<SubmissionPricingState>`. 2. Compare `proposedPremium` to `baselinePremium`; append `PricingBaselineAccepted` (exact) or `PricingBaselineOverridden` (variance). 3. Set `SubmissionIntakeModule.IntegrationEventQueueName` and wire `ListenToRabbitQueue` for `SubmissionAssessedV1` (Architecture Constraints: versioned integration events over durable per-module queues, never a direct cross-module call — ADR-004).
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Automations/RecordPricingBaselineComparisonOnAssessment/RecordPricingBaselineComparisonOnAssessmentHandler.cs`, `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Module.cs`
  - **Done when**: 10.4's tests pass
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests --filter RecordPricingBaselineComparison && echo PASS`
  - **Commit**: `feat(submission-intake): green - RecordPricingBaselineComparisonOnAssessment automation`
  - _Design: Automations table, Architecture Constraints (cross-module integration events)_

- [ ] 10.6 [VERIFY] Quality checkpoint: build + all module tests pass
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All exit 0
  - **Done when**: No errors
  - **Commit**: `chore(submission-intake): pass quality checkpoint` (if fixes needed)

**Checkpoint**: All 12 board events, 5 commands, 5 automations, 4 read models implemented and independently tested.

---

## Phase 11: Wiring Audit + Full-Flow E2E Verification

**Goal**: Confirm every projector/automation is actually invoked (Principle V's silent-failure risk — a missing `Handler` suffix or unregistered `SubscribeToEvent<T>` fails silently, no exception), then prove the entire receipt→normalization→duplicate/routing/pricing→resolution flow works against a real running host.

- [ ] 11.1 Wiring audit: confirm every `*Handler`/`*Projector` class name ends in `Handler`/registers correctly; confirm `IntegrationEventQueueName` for `SubmissionAssessedV1`
  - **Do**: 1. `grep -rL "Handler$" ` scan of all handler files' class names (Principle V). 2. Confirm `Module.cs` registers all 4 projectors (no snapshot needed per 2.5's decision — Wolverine-subscriber pattern doesn't need explicit `Projections.Add` the way Async-lifecycle ones do; confirm this against 001's actual pattern). 3. Confirm `Program.cs`'s `opts.ListenToRabbitQueue(queueName).UseDurableInbox()` loop picks up `SubmissionIntakeModule.IntegrationEventQueueName` from 10.5.
  - **Files**: `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Module.cs`, `src/Api.Host/Program.cs`
  - **Done when**: No handler class fails the naming convention; integration-event queue confirmed wired
  - **Verify**: `dotnet build src/BrokerConnect.slnx && grep -c "class.*Handler" src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/**/*.cs | grep -qv "^0" && echo PASS`
  - **Commit**: `fix(submission-intake): wiring audit fixes` (only if issues found)
  - _Design: Implementation Step 13_

- [ ] 11.2 [VERIFY] Full module test suite passes
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests && dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests`
  - **Verify**: All commands exit 0
  - **Done when**: Every test across all 3 layers is green
  - **Commit**: None

- [ ] 11.3 [VERIFY] VE1 E2E startup: launch `Api.Host` in Development, wait for `/health`
  - **Do**: 1. `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Api.Host/Api.Host.csproj &`. 2. Record PID to `/tmp/ve-pids.txt`. 3. Poll `http://localhost:5132/health` up to 60s.
  - **Verify**: `curl -sf http://localhost:5132/health | grep -q '"status":"Healthy"' && echo PASS`
  - **Done when**: Server responding on 5132
  - **Commit**: None

- [ ] 11.4 [VERIFY] VE2 E2E check: full submission-intake flow end-to-end
  - **Do**: 1. `POST /api/v1/submission-intake/submissions` → capture `submissionId`. 2. Poll `GET /api/v1/submission-intake/submission-queue?search=<namedInsured>` until the row appears (allow for async normalization). 3. `GET /api/v1/submission-intake/submissions/{submissionId}/priced-view` → assert `baselinePremium` populated. 4. `POST /api/v1/submission-intake/submissions/{submissionId}/confirm-distinct` (or `/supersede` against a second seeded submission) → assert `200`/`204`.
  - **Verify**: `bash -c 'set -e; ID=$(curl -s -X POST http://localhost:5132/api/v1/submission-intake/submissions -H "Content-Type: application/json" -d "{\"brokerFirmId\":\"acme\",\"submittingContact\":\"jane@acme.com\",\"rawPayload\":{},\"sourceChannel\":\"api\"}" | jq -r .submissionId); for i in $(seq 1 30); do curl -sf "http://localhost:5132/api/v1/submission-intake/submission-queue?search=$ID" | jq -e ".items | length >= 0" >/dev/null && break; sleep 1; done; echo PASS'`
  - **Done when**: The full receipt→normalization→pricing flow is proven against real running infrastructure, not just unit-level mocks
  - **Commit**: None

- [ ] 11.5 [VERIFY] VE3 E2E cleanup: stop server, free port 5132
  - **Do**: 1. `kill $(cat /tmp/ve-pids.txt) 2>/dev/null; sleep 2; kill -9 $(cat /tmp/ve-pids.txt) 2>/dev/null || true`. 2. `lsof -ti :5132 | xargs -r kill 2>/dev/null || true`. 3. `rm -f /tmp/ve-pids.txt`.
  - **Verify**: `! lsof -ti :5132 && echo PASS`
  - **Done when**: No process on 5132, PID file removed
  - **Commit**: None

- [ ] 11.6 [VERIFY] AC checklist — every AC-1.1 through AC-12.1 mapped to a passing test
  - **Do**: Read `requirements.md`'s Acceptance Criteria; for each `AC-N.M`, grep the corresponding test method(s) written across Phases 3–10 and confirm it's green.
  - **Verify**: `dotnet test tests/Modules/SubmissionIntake/SubmissionIntake.Domain.Tests tests/Modules/SubmissionIntake/SubmissionIntake.Api.Tests tests/Modules/SubmissionIntake/SubmissionIntake.IntegrationTests && echo PASS`
  - **Done when**: All 12 AC entries confirmed covered — no acceptance criterion without a corresponding executable test (Principle IV)
  - **Commit**: None

**Checkpoint**: Feature complete — every board slice implemented, tested at all 3 layers, and proven end-to-end against a real running host.

---

## Phase 12: Polish & Cross-Cutting Concerns

- [ ] 12.1 [P] Write ADR files for `design.md`'s Technical Decisions table into `docs/adr/`
  - **Do**: Continue this project's own ADR numbering (constitution Quality Gates — currently ADR-001 through ADR-018 per 001's Solution Arch §10) with the next free numbers for the 10 decisions in `design.md`'s Technical Decisions table (SubmissionQueue gap-fixes, raw payload storage, routing-vs-normalization parallelism, cross-module pricing comparison, duplicate-match heuristic, etc.).
  - **Files**: `docs/adr/ADR-0NN-*.md` (one file per decision, or a consolidated set — match 001's actual `docs/adr/` file granularity)
  - **Done when**: ADR files exist for all 10 decisions, numbered continuing the existing sequence
  - **Verify**: `ls docs/adr/ | grep -c "ADR-0" | awk '{exit ($1>=10)?0:1}' && echo PASS`
  - **Commit**: `docs(submission-intake): write ADRs for design decisions`

- [ ] 12.2 [P] `dotnet format --verify-no-changes`, `dotnet list package --vulnerable`, SAST + secret-scan pass
  - **Do**: Run the constitution's pre-checkin deterministic checks over the module (Quality Gates). Fix any High/Critical findings; lower-severity findings are a signal, not a blocker.
  - **Files**: N/A (verification pass)
  - **Done when**: `dotnet format --verify-no-changes` clean; no High/Critical vulnerable packages; SAST/secret-scan clean
  - **Verify**: `dotnet format src/BrokerConnect.slnx --verify-no-changes && dotnet list src/BrokerConnect.slnx package --vulnerable 2>&1 | grep -qv "critical\|high" && echo PASS`
  - **Done when**: All checks pass or findings are logged with a stated reason (Quality Gates: bypass is audited, never silent)
  - **Commit**: `chore(submission-intake): pass pre-checkin quality gates` (if fixes needed)

- [ ] 12.3 Structured logging pass — `ILogger` message templates on every handler
  - **Do**: Confirm every command/automation handler and read endpoint has structured (never interpolated) log calls: Information on success/no-op, Warning on rejection (routing/normalization-failed), per Principle X and 001's `GrantCellAuthorityLimitHandler` precedent. Confirm correlation ID propagation (already zero-code from `Program.cs`'s `ActivityTrackingOptions`, 001-established) surfaces in log output for this module's handlers too.
  - **Files**: All `*Handler.cs`/`*Projector.cs`/`Get*.cs` files under `src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/`
  - **Done when**: No handler is missing a structured log line on its success/rejection paths
  - **Verify**: `grep -rL "ILogger" src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Commands src/Modules/SubmissionIntake/BrokerConnect.Modules.SubmissionIntake.Api/Automations | grep -q . && echo FOUND_GAPS || echo PASS`
  - **Commit**: `feat(submission-intake): add structured logging to handlers` (if gaps found)

- [ ] 12.4 [VERIFY] Full solution quality gate
  - **Do**: `dotnet build src/BrokerConnect.slnx && dotnet format src/BrokerConnect.slnx --verify-no-changes && dotnet test src/BrokerConnect.slnx`
  - **Verify**: All 3 commands exit 0
  - **Done when**: Full solution (both modules) builds, formats clean, all tests pass — no regression to `001-authority-administration`'s own suite
  - **Commit**: `chore(submission-intake): pass full solution quality gate` (if fixes needed)

- [ ] 12.5 Push branch
  - **Do**: 1. Confirm current branch is `002-submission-intake`: `git branch --show-current`. 2. If on a different/default branch, STOP and alert the user (should not happen — branch set at spec start, constitution Quality Gates one-branch-per-spec rule). 3. `git push -u origin 002-submission-intake`.
  - **Files**: N/A
  - **Done when**: Branch pushed to origin
  - **Verify**: `git branch --show-current | grep -q "002-submission-intake" && git push -u origin 002-submission-intake && echo PASS`
  - **Done when**: Remote branch exists and is up to date with local
  - **Commit**: None (push only — no PR creation; this project has no CI configured and no PR-review process, per user instruction. The user decides separately when/how to merge.)

**Checkpoint**: Feature complete, all quality gates passed locally, pushed to its feature branch. No PR Lifecycle phase — matches this project's actual workflow (no CI, manual merge decision).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all later phases.
- **Phase 3 (Receipt)**: Depends on Foundational. Every other phase depends on Phase 3's `BrokerSubmissionReceived` stream existing.
- **Phases 4–10**: Each depends on Phase 3. Phases 4, 6, 7 can be built in parallel once Phase 3 lands (each triggers off `BrokerSubmissionReceived`/`SubmissionNormalized` independently). Phase 5 depends on Phase 4 (duplicate detection triggers on `SubmissionNormalized`, needs `SubmissionQueue` to query against). Phase 8 depends on Phase 4 (extends `NormalizeSubmissionViaAdept`'s trigger set). Phase 9 is fully independent (separate `PricingModel` stream) — can run any time after Foundational. Phase 10 depends on Phase 7 (`BaselinePremiumGenerated` must exist for comparison to be meaningful, though the guard handles absence).
- **Phase 11 (Wiring + Full VE)**: Depends on Phases 3–10 all complete.
- **Phase 12 (Polish)**: Depends on Phase 11.

### Parallel Opportunities

- Within Setup/Foundational: all `[P]` tasks run in parallel.
- Once Phase 3 completes: Phases 4, 6, 7, 9 can start in parallel (independent trigger events / independent streams).
- Within each phase: read-model doc+projector+test tasks (`[P]`) run in parallel with query-handler tasks (`[P]`), broken by each phase's `[VERIFY]` checkpoints.

---

## Notes

- **Test-first, no exceptions**: every implementation task in this file is preceded by its own test task (or, for pure wiring/registration tasks with no new logic, verified by the existing suite) — constitution Principle IV has no "skip tests" phase, unlike Ralph's default POC-first workflow.
- **Board-inferred names used as-is**: `RouteSubmissionOnReceipt`, `NormalizeSubmissionViaAdept`, `DetectPotentialDuplicateOnNormalization`, `CorrectSubmission`, `SupersedeSubmission`, `ConfirmSubmissionDistinct`, `DeployPricingModelVersion`, `RecordPricingBaselineComparisonOnAssessment` are all board-inferred (no literal board node) per `design.md`'s Board-Sourced vs. Inferred Elements table — implemented as designed, not re-litigated here.
- **5 unresolved questions implemented per their interim/stubbed approach**, not left undone: broker-panel auth data source (6.3's `IBrokerPanelAuthorizationSource` stub), `SubmissionAssessedV1` shape (10.3's assumed contract), `PricingModel` stream identity (2.4's generated `Guid` decision), stuck-in-normalization terminal state (not synthesized — Polly retry/circuit-breaker from 4.1 handles the outage path per Error Handling table, no synthetic `SubmissionNormalizationFailed` invented), `SubmissionQueue.status` value set (4.7's projector sets it directly from `normalizationStatus`, no enum invented).
- **Cross-module boundary flagged for `004-underwriting-decisioning`**: when that spec is built, its own `AssessSubmission` command must actually publish `SubmissionAssessedV1` for Phase 10's consumer to receive anything — mirrors 001's own `AuthorityLimitChangedV1` cross-referencing obligation, in the opposite direction.
