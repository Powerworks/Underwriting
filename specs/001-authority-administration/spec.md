# Feature Specification: Authority Administration

**Feature Branch**: `001-authority-administration`

**Created**: 2026-08-10

**Status**: Draft

**Input**: Derived from the "BrokerConnect" eventmodelers.ai board (`80f53178-c291-43a0-8aa5-bc723990c5db`), chapter **Broker Connect**, context **Authority Administration** — pulled live via `GET .../slicedata?contextName=...` on 2026-08-10 and cached at `event-model/slices/*.json` / `event-model/import-config.json` (Phase A / MVP scope only — see `Project Plan/01-project-plan.md` §4; the 4 exploratory contexts are not yet pulled). Supersedes an earlier partial export now kept at `event-model/archive/` for provenance — see `event-model-to-speckit-guide.md`.

## Clarifications

### Session 2026-08-09

- Q: What read-consistency guarantee must `AuthorityMatrix` provide for `AssessSubmission`'s decision-time query? → A: Zero staleness — a caller's next read must reflect their own prior write in the same request (resolved as an `Inline` Marten snapshot; see SC-001).
- Q: What values can `AuthorityLimit.status` take? → A: `Active` or `Revoked` only.
- Q: What happens when `ReviseAuthorityLimit` targets a `Revoked` record? → A: Reject — append `AuthorityLimitRevisionRejected` (`rejectionReason: "RevokedRecord"`); no change to `status`/`version` (see FR-010).
- Q: What happens when a grant would duplicate an existing `Active` grant for the same scope? → A: Reject at either tier — append `CellAuthorityLimitGrantRejected` (Cell tier, new event) or extend `UnderwriterAuthorityLimitRejected` with `rejectionReason: "DuplicateActiveGrant"` (Underwriter tier) (see FR-011).
- Q: What happens when `ReviseAuthorityLimit` on an Underwriter-tier record would push it above the Cell's current limit? → A: Reject — append `AuthorityLimitRevisionRejected` (`rejectionReason: "ExceedsCellLimit"`), the same cascade check `GrantUnderwriterAuthorityLimit` already runs (see FR-012).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - CellAuthorityLimitGranted (Priority: P1)

As **GrantCellAuthorityLimit**, I want to cellAuthorityLimitGranted so that **CellAuthorityLimitGranted** is recorded.

**Narrative** (verbatim from the board export): S0.1: actor is Underwriting Governance or senior executive - reference/configuration data, not transactional flow. OPEN QUESTION: is this purely internal (TFP governance), or does it require the capacity provider's own sign-off captured as part of the event?

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **GrantCellAuthorityLimit** under the right preconditions and asserting that **CellAuthorityLimitGranted** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S0.1: becomes the ceiling that all underwriter-level grants (S0.2) within that cell must fit inside., **When** GrantCellAuthorityLimit, **Then** CellAuthorityLimitGranted

---

### User Story 2 - UnderwriterAuthorityLimitGranted (Priority: P2)

The system maintains **AuthorityMatrix**, projected from **UnderwriterAuthorityLimitGranted**, **CellAuthorityLimitGranted**, **AuthorityLimitRevised**, and **AuthorityLimitRevoked**.

**Narrative** (verbatim from the board export): The view Context 2's AssessSubmission actually queries at decision time. Renamed from an earlier generic 'AuthorityLimit' readmodel to match the terminology used once Context 0 was detailed - same underlying projection.

**Correction (2026-08-10)**: the story title/FR-002 originally credited only `UnderwriterAuthorityLimitGranted` — a board-authoring gap (this readmodel node was missing 3 of its 4 real inbound edges) fixed today; see the `AuthorityMatrix` entry's `Dependencies` line in the Event Model Detail appendix, now showing all 4. Without the other 3, a Cell-tier grant, a revision, or — most importantly — a **revocation** would never reach `AuthorityMatrix`, meaning `AssessSubmission` (004) could keep validating against a revoked authority limit. `plan.md`/`data-model.md`'s technical design (`Inline` snapshot of the whole `AuthorityLimit` aggregate, not a hand-rolled per-event async projection) already sidesteps this risk regardless of what this FR says — but the FR text itself needed correcting so it doesn't mislead a future reader.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **UnderwriterAuthorityLimitGranted** and asserting that **AuthorityMatrix** reflects the update.

**Acceptance Scenarios**:

1. **Given** S0.2: this is the record AssessSubmission (Context 2) checks against, via AuthorityMatrix., **When** UnderwriterAuthorityLimitGranted is appended, **Then** **AuthorityMatrix** reflects it
2. **Given** a Cell-tier grant, a revision, or a revocation is appended to any `AuthorityLimit` stream, **When** the event is appended, **Then** **AuthorityMatrix** reflects it (see FR-002, corrected 2026-08-10)

---

### User Story 3 - UnderwriterAuthorityLimitRejected (Priority: P1)

As **GrantUnderwriterAuthorityLimit**, I want to underwriterAuthorityLimitRejected so that **UnderwriterAuthorityLimitRejected** is recorded.

**Narrative** (verbatim from the board export): S0.2/S0.3: Cell CUO granting authority to an individual underwriter, validated against the cell's own limit (S0.1). OPEN QUESTION: does validation need to account for other underwriters' existing grants - an aggregate pool per cell (sum of all underwriters can't exceed the cell's limit) vs. each underwriter's limit being independent (a per-transaction ceiling, not a shared pool)? Real domain question for governance stakeholders, not a technicality.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **GrantUnderwriterAuthorityLimit** under the right preconditions and asserting that **UnderwriterAuthorityLimitRejected** is the resulting domain event.

**Correction (2026-08-10)**: this story's title and FR-003 describe only the rejection branch. `GrantUnderwriterAuthorityLimit` is a branching command — see the Event Model Detail appendix's `UnderwriterAuthorityLimitRejected` slice, whose `Dependencies` line now shows two outbound `UnderwriterAuthorityLimitGranted` edges alongside the rejected one (board edges fixed 2026-08-10; `gen_specs_from_slices.py` renders FRs per-slice and can't express this cross-slice fan-out on its own, so it's noted here by hand). Happy path: **UnderwriterAuthorityLimitGranted**. Rejection: **UnderwriterAuthorityLimitRejected** (`rejectionReason: "ExceedsCellLimit" | "DuplicateActiveGrant"`, see FR-011).

**Acceptance Scenarios**:

1. **Given** S0.3: requested delegation would exceed the cell's own limit. OPEN QUESTION: does rejection trigger escalation (a request to increase the cell's own overall limit, see CellAuthorityIncreaseRequested) or dead-end requiring the Cell CUO to reallocate existing underwriters' authority instead? Modeled the escalation path explicitly rather than leaving a pure dead end - see CellAuthorityIncreaseRequested., **When** GrantUnderwriterAuthorityLimit, **Then** UnderwriterAuthorityLimitRejected
2. **Given** the requested delegation is within the cell's own limit and does not duplicate an existing Active grant for the same scope, **When** GrantUnderwriterAuthorityLimit, **Then** UnderwriterAuthorityLimitGranted

---

### User Story 4 - AuthorityLimitRevised (Priority: P1)

As **ReviseAuthorityLimit**, I want to authorityLimitRevised so that **AuthorityLimitRevised** is recorded.

**Narrative** (verbatim from the board export): Either tier's authority record is revised - e.g. an annual treaty renewal resets the Cell's limit, or a Cell Head Underwriter adjusts an individual underwriter's delegation.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **ReviseAuthorityLimit** under the right preconditions and asserting that **AuthorityLimitRevised** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S0.4: sharpest open question in this context - a submission already in-flight (post-1a, pre-bind), assessed against the old limit, when the revision drops below what it needs. Grandfather (evaluate against authority in effect when it entered decisioning) vs re-evaluate (immediately re-check, force referral if it now breaches) are both defensible; leaning toward re-evaluation as the safer default for a governance-critical system, but this is a business decision, not an architecture default. Either way, always triggers ReassessInFlightSubmissionsOnRuleChange producing InFlightSubmissionReassessed, so the decision and its trigger are visible in the audit trail regardless of which policy stance is chosen. Same underlying 'rules changed mid-flight' problem as S1e.2's freeze-during-decisioning - solved with the same mechanism, not two bespoke ones., **When** ReviseAuthorityLimit, **Then** AuthorityLimitRevised

---

### User Story 5 - AuthorityLimitRevoked (Priority: P1)

As **RevokeAuthorityLimit**, I want to authorityLimitRevoked so that **AuthorityLimitRevoked** is recorded.

**Narrative** (verbatim from the board export): Either tier's authority record is revoked outright - e.g. an underwriter leaves the cell, or a Provider ends a Cell relationship for a class of business.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **RevokeAuthorityLimit** under the right preconditions and asserting that **AuthorityLimitRevoked** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S0.5: an underwriter with zero authority cannot have any submission proceed under their name, referral cascade or not - same in-flight question as S0.4 with sharper urgency, triggers ReassessInFlightSubmissionsOnRuleChange immediately. OPEN QUESTION: does revocation trigger a review flag on their recently bound business, similar to a thematic file review trigger? Not modeled as its own event yet - flagged for confirmation., **When** RevokeAuthorityLimit, **Then** AuthorityLimitRevoked

---

### User Story 6 - CellAuthorityLimitGranted (Priority: P2)

The system maintains **CellAuthorityRegister**, projected from **CellAuthorityLimitGranted**.

**Narrative** (verbatim from the board export): S0.1: current and historical authority limits per cell, queryable at any point in time.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **CellAuthorityLimitGranted** and asserting that **CellAuthorityRegister** reflects the update.

**Acceptance Scenarios**:

1. **Given** Second instance of the same event type, paired here with the register projection it feeds., **When** CellAuthorityLimitGranted is appended, **Then** **CellAuthorityRegister** reflects it

---

### User Story 7 - CellAuthorityIncreaseRequested (Priority: P1)

As **RequestCellAuthorityIncrease**, I want to cellAuthorityIncreaseRequested so that **CellAuthorityIncreaseRequested** is recorded.

**Narrative** (verbatim from the board export): Escalation path from S0.3's rejection - modeled explicitly rather than a pure dead end, since in practice someone needs to know 'how do we actually get this underwriter the authority they need.'

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **RequestCellAuthorityIncrease** under the right preconditions and asserting that **CellAuthorityIncreaseRequested** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S0.3 follow-on: loops back into a variant of S0.1 (GrantCellAuthorityLimit / AuthorityLimitRevised) if approved by the capacity provider/governance - the request itself is the auditable fact, distinct from whatever decision follows., **When** RequestCellAuthorityIncrease, **Then** CellAuthorityIncreaseRequested

---

### User Story 8 - UnderwriterAuthorityLimitGranted (Priority: P2)

The system maintains **UnderwriterAuthorityRegister**, projected from **UnderwriterAuthorityLimitGranted**.

**Narrative** (verbatim from the board export): S0.2: current authority per underwriter, with full history - distinct from AuthorityMatrix (the decision-time query view).

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **UnderwriterAuthorityLimitGranted** and asserting that **UnderwriterAuthorityRegister** reflects the update.

**Acceptance Scenarios**:

1. **Given** Second instance of the same event type, paired here with the register projection it feeds (distinct from AuthorityMatrix, which is what Context 2 queries at decision time - this is the underwriter-facing historical register)., **When** UnderwriterAuthorityLimitGranted is appended, **Then** **UnderwriterAuthorityRegister** reflects it

---

### User Story 9 - InFlightSubmissionReassessed (Priority: P2)

As a background policy in **Authority Administration**, the system reacts by executing **ReassessInFlightSubmissionsOnRuleChange**, producing **InFlightSubmissionReassessed**.

**Narrative** (verbatim from the board export): Triggered by AuthorityLimitRevised, AuthorityLimitRevoked (Context 0), and TerritoryUnderwritingFrozen (Context 1e) - one shared policy for 'rules changed mid-flight', not three separate reactive processes solving the same problem differently.

**Why this priority**: System-driven policy that keeps derived state (bordereaux, projections, notifications) consistent after the primary action(s) that trigger it.

**Independent Test**: Can be tested by invoking **ReassessInFlightSubmissionsOnRuleChange** under the right preconditions and asserting that **InFlightSubmissionReassessed** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S0.4/S0.5's sharpest open question, generalized: what happens to a submission already in-flight when the rules it's being evaluated against change underneath it. Leaning toward re-evaluation as the safer default for a governance-critical system, but this is a business decision (policyApplied), not an architecture default. Revocation (S0.5) pushes for the stricter, immediate interpretation given it correlates with higher-risk situations; routine revisions (S0.4) may be more relaxed (checked at next decision point)., **When** ReassessInFlightSubmissionsOnRuleChange, **Then** InFlightSubmissionReassessed

---

### Edge Cases

- What happens when GrantUnderwriterAuthorityLimit cannot proceed on the happy path? System records **UnderwriterAuthorityLimitRejected** instead — S0.3: requested delegation would exceed the cell's own limit. OPEN QUESTION: does rejection trigger escalation (a request to increase the cell's own overall limit, see CellAuthorityIncreaseRequested) or dead-end requiring the Cell CUO to reallocate existing underwriters' authority instead? Modeled the escalation path explicitly rather than leaving a pure dead end - see CellAuthorityIncreaseRequested..
- What happens when `ReviseAuthorityLimit` targets a record whose `status` is already `Revoked`? System rejects it — appends **AuthorityLimitRevisionRejected** (`rejectionReason: "RevokedRecord"`) to the target's own stream instead of `AuthorityLimitRevised`; `status`/`version` are unchanged (Clarified 2026-08-09, see FR-010).
- What happens when a grant would duplicate an existing `Active` grant for the same (tier, cell/underwriter, class of business) scope? System rejects it — **CellAuthorityLimitGrantRejected** at Cell tier, or **UnderwriterAuthorityLimitRejected** with `rejectionReason: "DuplicateActiveGrant"` at Underwriter tier (Clarified 2026-08-09, see FR-011).
- What happens when `ReviseAuthorityLimit` on an Underwriter-tier record would push it above the Cell's current limit? System rejects it — appends **AuthorityLimitRevisionRejected** (`rejectionReason: "ExceedsCellLimit"`), re-running the same cascade check as the initial grant (Clarified 2026-08-09, see FR-012).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support **GrantCellAuthorityLimit**, producing the **CellAuthorityLimitGranted** domain event(s).
- **FR-002**: System MUST project **AuthorityMatrix** from **CellAuthorityLimitGranted**, **UnderwriterAuthorityLimitGranted**, **AuthorityLimitRevised**, and **AuthorityLimitRevoked** (corrected 2026-08-10 — the generated text previously credited only `UnderwriterAuthorityLimitGranted`; see User Story 2's Correction note and `data-model.md`).
- **FR-003**: System MUST support **GrantUnderwriterAuthorityLimit**, producing **UnderwriterAuthorityLimitGranted** (happy path) or **UnderwriterAuthorityLimitRejected** (`rejectionReason: "ExceedsCellLimit" | "DuplicateActiveGrant"`) (corrected 2026-08-10 — see User Story 3's Correction note).
- **FR-004**: System MUST support **ReviseAuthorityLimit**, producing the **AuthorityLimitRevised** domain event(s).
- **FR-005**: System MUST support **RevokeAuthorityLimit**, producing the **AuthorityLimitRevoked** domain event(s).
- **FR-006**: System MUST project **CellAuthorityRegister** from the **CellAuthorityLimitGranted** domain event(s).
- **FR-007**: System MUST support **RequestCellAuthorityIncrease**, producing the **CellAuthorityIncreaseRequested** domain event(s).
- **FR-008**: System MUST project **UnderwriterAuthorityRegister** from the **UnderwriterAuthorityLimitGranted** domain event(s).
- **FR-009**: System MUST support **ReassessInFlightSubmissionsOnRuleChange**, producing the **InFlightSubmissionReassessed** domain event(s).
- **FR-010**: System MUST reject **ReviseAuthorityLimit** when the target record's `status` is `Revoked`, producing **AuthorityLimitRevisionRejected** (`rejectionReason: "RevokedRecord"`) instead of `AuthorityLimitRevised` (Clarified 2026-08-09).
- **FR-011**: System MUST reject a grant that would duplicate an existing `Active` grant for the same scope, producing **CellAuthorityLimitGrantRejected** (Cell tier) or **UnderwriterAuthorityLimitRejected** with `rejectionReason: "DuplicateActiveGrant"` (Underwriter tier) (Clarified 2026-08-09).
- **FR-012**: System MUST reject **ReviseAuthorityLimit** on an Underwriter-tier record when the revised amount would exceed the Cell's current limit, producing **AuthorityLimitRevisionRejected** (`rejectionReason: "ExceedsCellLimit"`) (Clarified 2026-08-09).

### Key Entities *(include if feature involves data)*

- **AuthorityLimit**: aggregate in the **Authority Administration** context; touched by **AuthorityLimitRevised**, **AuthorityLimitRevoked**, **CellAuthorityLimitGranted**, **GrantCellAuthorityLimit**, **GrantUnderwriterAuthorityLimit**, **ReviseAuthorityLimit**, **RevokeAuthorityLimit**, **UnderwriterAuthorityLimitGranted**, **UnderwriterAuthorityLimitRejected**. `status` is `Active` or `Revoked` only (Clarified 2026-08-09) — `Revoked` is terminal (see FR-010).
- **CellAuthorityIncreaseRequest**: aggregate in the **Authority Administration** context; touched by **CellAuthorityIncreaseRequested**, **RequestCellAuthorityIncrease**.
- **SubmissionAssessment**: aggregate in the **Authority Administration** context; touched by **InFlightSubmissionReassessed**, **ReassessInFlightSubmissionsOnRuleChange**.
- **AuthorityMatrix** (read model): The view Context 2's AssessSubmission actually queries at decision time. Renamed from an earlier generic 'AuthorityLimit' readmodel to match the terminology used once Context 0 was detailed - same underlying projection. Fields: `authorityLimitId`, `tier`, `providerId`, `cellId`, `underwriterId`, `classOfBusiness`, `maxGrossPremium`, `maxLimit`, `currency`, `status`, `version`, `grantedBy`, `grantedAt`.
- **CellAuthorityRegister** (read model): S0.1: current and historical authority limits per cell, queryable at any point in time. Fields: `cellId`, `currentScope`, `currentVersion`, `effectiveDate`, `history`.
- **UnderwriterAuthorityRegister** (read model): S0.2: current authority per underwriter, with full history - distinct from AuthorityMatrix (the decision-time query view). Fields: `underwriterId`, `cellId`, `currentScope`, `currentVersion`, `effectiveDate`, `history`.

## Success Criteria *(mandatory)*

<!--
  NOTE: the source eventmodelers board did not define measurable success metrics for this
  context. SC-001 was resolved via /speckit-clarify (Session 2026-08-09); SC-002–004 remain
  template placeholders — fill in manually before /speckit-plan.
-->

### Measurable Outcomes

- **SC-001**: `AuthorityMatrix` reflects a prior write (grant/revise/revoke) on the very next read within the same request — zero observed staleness, no retry/poll required (Clarified 2026-08-09).
- **SC-002**: [Measurable metric, e.g., "System handles 1000 concurrent users without degradation"]
- **SC-003**: [User satisfaction metric, e.g., "90% of users successfully complete primary task on first attempt"]
- **SC-004**: [Business metric, e.g., "Reduce support tickets related to [X] by 50%"]

## Assumptions

- Derived from the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Authority Administration**); command/event names, field lists, descriptions, aggregates, and lanes in the Event Model Detail section below are transcribed directly from a live pull of that board, not invented for this document.
- Pulled live on 2026-08-10 via the `slicedata` endpoint (see `event-model-to-speckit-guide.md`, "The real export endpoint"), not the raw event-replay log — re-run `event-model/build-scripts/gen_specs_from_slices.py` after any further board edits to keep this in sync; nothing here watches the board automatically.
- All 9 slice(s) in this context currently carry board status `Created` — none are `Planned` or built yet.
- The source board did not specify performance, scale, or business-metric assumptions for this context — the Success Criteria above are template placeholders.
- [Assumption about scope boundaries — confirm before /speckit.plan]

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export, including every field's type, cardinality, and flags (`id` = identifier field, `generated` = system-generated, `optional` = nullable). Nested `List`/object fields show their subfields indented beneath them with `↳`.

### Slice: CellAuthorityLimitGranted (`f9e0aa76-7a4d-4587-8a88-a8efaf85060e`, status: Created, type: STATE_CHANGE)

**GrantCellAuthorityLimit** (command, id `f62b101a-5b93-4fdc-84bf-d006494f974d`, aggregate `AuthorityLimit`, lane `Interaction`, modelContext `Authority Administration`)

> S0.1: actor is Underwriting Governance or senior executive - reference/configuration data, not transactional flow. OPEN QUESTION: is this purely internal (TFP governance), or does it require the capacity provider's own sign-off captured as part of the event?

Dependencies: → CellAuthorityLimitGranted (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `cellId` | String | Single | — |
| `grantingParty` | String | Single | — |
| `sourceAgreementReference` | String | Single | — |
| `authorityScope` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `classesOfBusiness` | String | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `territory` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `maxLineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `maxAggregate` | Decimal | Single | — |
| `currency` | String | Single | — |
| `effectiveDate` | Date | Single | — |

**CellAuthorityLimitGranted** (event, id `40a4a21f-506e-408c-a2c9-82f4ec943eba`, aggregate `AuthorityLimit`, lane `Authority`, modelContext `Authority Administration`)

> S0.1: becomes the ceiling that all underwriter-level grants (S0.2) within that cell must fit inside.

Dependencies: → AuthorityMatrix (READMODEL); ← GrantCellAuthorityLimit (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `authorityLimitId` | UUID | Single | id, generated |
| `cellId` | String | Single | — |
| `authorityScope` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `classesOfBusiness` | String | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `territory` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `maxLineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `maxAggregate` | Decimal | Single | — |
| `sourceAgreementReference` | String | Single | — |
| `grantedBy` | String | Single | — |
| `effectiveDate` | Date | Single | — |
| `version` | Integer | Single | generated |
| `grantedAt` | DateTime | Single | generated |

### Slice: UnderwriterAuthorityLimitGranted (`879a0064-763b-49af-b47c-f03638d2e14c`, status: Created, type: STATE_VIEW)

**UnderwriterAuthorityLimitGranted** (event, id `58f0f31f-341d-42b3-8e38-73b2ac562f0b`, aggregate `AuthorityLimit`, lane `Authority`, modelContext `Authority Administration`)

> S0.2: this is the record AssessSubmission (Context 2) checks against, via AuthorityMatrix.

Dependencies: ← GrantUnderwriterAuthorityLimit (COMMAND); → AuthorityMatrix (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `authorityLimitId` | UUID | Single | id, generated |
| `underwriterId` | String | Single | — |
| `cellId` | String | Single | — |
| `scope` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `classesOfBusiness` | String | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `territory` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `maxLineSize` | Decimal | Single | — |
| `grantedBy` | String | Single | — |
| `effectiveDate` | Date | Single | — |
| `version` | Integer | Single | generated |
| `validationResult` | String | Single | — |
| `grantedAt` | DateTime | Single | generated |

**AuthorityMatrix** (read model, id `84fc097f-de83-4ae1-9a6f-77ee3c74422c`, aggregate `AuthorityLimit`, lane `Interaction`, modelContext `Authority Administration`)

> The view Context 2's AssessSubmission actually queries at decision time. Renamed from an earlier generic 'AuthorityLimit' readmodel to match the terminology used once Context 0 was detailed - same underlying projection.

Dependencies: ← CellAuthorityLimitGranted (EVENT); ← AuthorityLimitRevised (EVENT); ← AuthorityLimitRevoked (EVENT); ← UnderwriterAuthorityLimitGranted (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `authorityLimitId` | UUID | Single | id |
| `tier` | String | Single | — |
| `providerId` | String | Single | optional |
| `cellId` | String | Single | — |
| `underwriterId` | String | Single | optional |
| `classOfBusiness` | String | Single | — |
| `maxGrossPremium` | Decimal | Single | — |
| `maxLimit` | Decimal | Single | — |
| `currency` | String | Single | — |
| `status` | String | Single | — |
| `version` | Integer | Single | — |
| `grantedBy` | String | Single | optional |
| `grantedAt` | DateTime | Single | — |

### Slice: UnderwriterAuthorityLimitRejected (`013ae3ab-4e48-4bd2-9aa9-863dffbf8cbb`, status: Created, type: STATE_CHANGE)

**GrantUnderwriterAuthorityLimit** (command, id `3a6196cc-ed64-4c66-b8f8-a4b9139e5bdf`, aggregate `AuthorityLimit`, lane `Interaction`, modelContext `Authority Administration`)

> S0.2/S0.3: Cell CUO granting authority to an individual underwriter, validated against the cell's own limit (S0.1). OPEN QUESTION: does validation need to account for other underwriters' existing grants - an aggregate pool per cell (sum of all underwriters can't exceed the cell's limit) vs. each underwriter's limit being independent (a per-transaction ceiling, not a shared pool)? Real domain question for governance stakeholders, not a technicality.

Dependencies: → UnderwriterAuthorityLimitGranted (EVENT); → UnderwriterAuthorityLimitGranted (EVENT); → UnderwriterAuthorityLimitRejected (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `underwriterId` | String | Single | — |
| `cellId` | String | Single | — |
| `requestedScope` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `classesOfBusiness` | String | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `territory` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `maxLineSize` | Decimal | Single | — |
| `grantingAuthority` | String | Single | — |

**UnderwriterAuthorityLimitRejected** (event, id `90fd73e6-2a78-46c9-999e-19c6f9c9aa18`, aggregate `AuthorityLimit`, lane `Authority`, modelContext `Authority Administration`)

> S0.3: requested delegation would exceed the cell's own limit. OPEN QUESTION: does rejection trigger escalation (a request to increase the cell's own overall limit, see CellAuthorityIncreaseRequested) or dead-end requiring the Cell CUO to reallocate existing underwriters' authority instead? Modeled the escalation path explicitly rather than leaving a pure dead end - see CellAuthorityIncreaseRequested.

Dependencies: ← GrantUnderwriterAuthorityLimit (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `underwriterId` | String | Single | — |
| `cellId` | String | Single | — |
| `requestedScope` | Custom | Single | — |
| `rejectionReason` | String | Single | — |
| `attemptedBy` | String | Single | — |
| `rejectedAt` | DateTime | Single | generated |

### Slice: AuthorityLimitRevised (`118aec35-6676-4ad0-8f4b-baadc4baf6ae`, status: Created, type: STATE_CHANGE)

**ReviseAuthorityLimit** (command, id `80a71127-fccd-4ceb-977a-87e35029bea7`, aggregate `AuthorityLimit`, lane `Interaction`, modelContext `Authority Administration`)

> Either tier's authority record is revised - e.g. an annual treaty renewal resets the Cell's limit, or a Cell Head Underwriter adjusts an individual underwriter's delegation.

Dependencies: → AuthorityLimitRevised (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `authorityLimitId` | UUID | Single | id |
| `newMaxGrossPremium` | Decimal | Single | — |
| `newMaxLimit` | Decimal | Single | — |
| `revisedBy` | String | Single | — |
| `reason` | String | Single | — |

**AuthorityLimitRevised** (event, id `e17d213c-4261-4c18-bf40-f6a45fe173fc`, aggregate `AuthorityLimit`, lane `Authority`, modelContext `Authority Administration`)

> S0.4: sharpest open question in this context - a submission already in-flight (post-1a, pre-bind), assessed against the old limit, when the revision drops below what it needs. Grandfather (evaluate against authority in effect when it entered decisioning) vs re-evaluate (immediately re-check, force referral if it now breaches) are both defensible; leaning toward re-evaluation as the safer default for a governance-critical system, but this is a business decision, not an architecture default. Either way, always triggers ReassessInFlightSubmissionsOnRuleChange producing InFlightSubmissionReassessed, so the decision and its trigger are visible in the audit trail regardless of which policy stance is chosen. Same underlying 'rules changed mid-flight' problem as S1e.2's freeze-during-decisioning - solved with the same mechanism, not two bespoke ones.

Dependencies: → AuthorityMatrix (READMODEL); ← ReviseAuthorityLimit (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `authorityLimitId` | UUID | Single | id |
| `target` | String | Single | — |
| `previousLimit` | Custom | Single | — |
| `newLimit` | Custom | Single | — |
| `reason` | String | Single | — |
| `revisedBy` | String | Single | — |
| `effectiveDate` | Date | Single | — |
| `version` | Integer | Single | generated |
| `revisedAt` | DateTime | Single | generated |

### Slice: AuthorityLimitRevoked (`5ea7eefa-2114-4655-af38-76f5b1f5b626`, status: Created, type: STATE_CHANGE)

**RevokeAuthorityLimit** (command, id `a5b22189-aba5-43ef-b761-98a938c7a417`, aggregate `AuthorityLimit`, lane `Interaction`, modelContext `Authority Administration`)

> Either tier's authority record is revoked outright - e.g. an underwriter leaves the cell, or a Provider ends a Cell relationship for a class of business.

Dependencies: → AuthorityLimitRevoked (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `authorityLimitId` | UUID | Single | id |
| `revokedBy` | String | Single | — |
| `reason` | String | Single | — |

**AuthorityLimitRevoked** (event, id `c05ac25c-d24d-46cd-9841-be0fe777cfc7`, aggregate `AuthorityLimit`, lane `Authority`, modelContext `Authority Administration`)

> S0.5: an underwriter with zero authority cannot have any submission proceed under their name, referral cascade or not - same in-flight question as S0.4 with sharper urgency, triggers ReassessInFlightSubmissionsOnRuleChange immediately. OPEN QUESTION: does revocation trigger a review flag on their recently bound business, similar to a thematic file review trigger? Not modeled as its own event yet - flagged for confirmation.

Dependencies: → AuthorityMatrix (READMODEL); ← RevokeAuthorityLimit (COMMAND); → CellAuthorityRegister (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `authorityLimitId` | UUID | Single | id |
| `target` | String | Single | — |
| `priorLimit` | Custom | Single | — |
| `reason` | String | Single | — |
| `revokedBy` | String | Single | — |
| `immediacy` | String | Single | — |
| `revokedAt` | DateTime | Single | generated |

### Slice: CellAuthorityLimitGranted (`7e3099b4-c3ee-4fab-8a3e-19331b449bfe`, status: Created, type: STATE_VIEW)

**CellAuthorityLimitGranted** (event, id `6394d2de-60e1-444a-9300-8ea5c7698016`, aggregate `AuthorityLimit`, lane `Authority`, modelContext `Authority Administration`)

> Second instance of the same event type, paired here with the register projection it feeds.

Dependencies: → CellAuthorityRegister (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `authorityLimitId` | UUID | Single | id, generated |
| `cellId` | String | Single | — |
| `version` | Integer | Single | generated |

**CellAuthorityRegister** (read model, id `894b712c-89d4-4b15-bbc3-0d67e1eb58ef`, aggregate `AuthorityLimit`, lane `Interaction`, modelContext `Authority Administration`)

> S0.1: current and historical authority limits per cell, queryable at any point in time.

Dependencies: ← CellAuthorityLimitGranted (EVENT); ← AuthorityLimitRevoked (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `cellId` | String | Single | id |
| `currentScope` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `classesOfBusiness` | String | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `territory` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `maxLineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `maxAggregate` | Decimal | Single | — |
| `currentVersion` | Integer | Single | — |
| `effectiveDate` | Date | Single | — |
| `history` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `version` | Integer | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `scope` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `effectiveDate` | Date | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `supersededAt` | DateTime | Single | optional |

### Slice: CellAuthorityIncreaseRequested (`a0c8b6f6-a13b-4828-97e3-3baccd8db6f0`, status: Created, type: STATE_CHANGE)

**RequestCellAuthorityIncrease** (command, id `7228fcc8-105e-4b0b-866a-2d183cd09fe2`, aggregate `CellAuthorityIncreaseRequest`, lane `Interaction`, modelContext `Authority Administration`)

> Escalation path from S0.3's rejection - modeled explicitly rather than a pure dead end, since in practice someone needs to know 'how do we actually get this underwriter the authority they need.'

Dependencies: → CellAuthorityIncreaseRequested (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `cellId` | String | Single | — |
| `requestedBy` | String | Single | — |
| `currentLimit` | Custom | Single | — |
| `requestedLimit` | Custom | Single | — |
| `justification` | String | Single | — |

**CellAuthorityIncreaseRequested** (event, id `e2f73fd5-5d5f-4daf-9c94-7fb329622674`, aggregate `CellAuthorityIncreaseRequest`, lane `Authority`, modelContext `Authority Administration`)

> S0.3 follow-on: loops back into a variant of S0.1 (GrantCellAuthorityLimit / AuthorityLimitRevised) if approved by the capacity provider/governance - the request itself is the auditable fact, distinct from whatever decision follows.

Dependencies: ← RequestCellAuthorityIncrease (COMMAND); → UnderwriterAuthorityRegister (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `cellId` | String | Single | — |
| `requestedBy` | String | Single | — |
| `currentLimit` | Custom | Single | — |
| `requestedLimit` | Custom | Single | — |
| `justification` | String | Single | — |
| `requestedAt` | DateTime | Single | generated |

### Slice: UnderwriterAuthorityLimitGranted (`7f339999-19a1-4cc8-ac73-ecfb8dab5a7e`, status: Created, type: STATE_VIEW)

**UnderwriterAuthorityLimitGranted** (event, id `1495da8a-8392-4c92-aceb-e3f75308358a`, aggregate `AuthorityLimit`, lane `Authority`, modelContext `Authority Administration`)

> Second instance of the same event type, paired here with the register projection it feeds (distinct from AuthorityMatrix, which is what Context 2 queries at decision time - this is the underwriter-facing historical register).

Dependencies: ← GrantUnderwriterAuthorityLimit (COMMAND); → UnderwriterAuthorityRegister (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `authorityLimitId` | UUID | Single | id, generated |
| `underwriterId` | String | Single | — |
| `version` | Integer | Single | generated |

**UnderwriterAuthorityRegister** (read model, id `d18458a0-3066-431a-8fd6-de2bea50824c`, aggregate `AuthorityLimit`, lane `Interaction`, modelContext `Authority Administration`)

> S0.2: current authority per underwriter, with full history - distinct from AuthorityMatrix (the decision-time query view).

Dependencies: ← UnderwriterAuthorityLimitGranted (EVENT); ← CellAuthorityIncreaseRequested (EVENT); → ReassessInFlightSubmissionsOnRuleChange (AUTOMATION)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `underwriterId` | String | Single | id |
| `cellId` | String | Single | — |
| `currentScope` | Custom | Single | — |
| `currentVersion` | Integer | Single | — |
| `effectiveDate` | Date | Single | — |
| `history` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `version` | Integer | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `scope` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `effectiveDate` | Date | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `supersededAt` | DateTime | Single | optional |

### Slice: InFlightSubmissionReassessed (`c6877dbc-349f-4df2-9777-9f34f5b86146`, status: Created, type: AUTOMATION)

**ReassessInFlightSubmissionsOnRuleChange** (automation/processor, id `71d42826-935c-4d80-ac29-fbacad53e33f`, aggregate `SubmissionAssessment`, lane `Actor`, modelContext `Authority Administration`)

> Triggered by AuthorityLimitRevised, AuthorityLimitRevoked (Context 0), and TerritoryUnderwritingFrozen (Context 1e) - one shared policy for 'rules changed mid-flight', not three separate reactive processes solving the same problem differently.

Dependencies: ← UnderwriterAuthorityRegister (READMODEL)

_(no fields)_

**InFlightSubmissionReassessed** (event, id `1ac3b9d6-7054-4279-84ff-7c97bd0a05b7`, aggregate `SubmissionAssessment`, lane `Authority`, modelContext `Authority Administration`)

> S0.4/S0.5's sharpest open question, generalized: what happens to a submission already in-flight when the rules it's being evaluated against change underneath it. Leaning toward re-evaluation as the safer default for a governance-critical system, but this is a business decision (policyApplied), not an architecture default. Revocation (S0.5) pushes for the stricter, immediate interpretation given it correlates with higher-risk situations; routine revisions (S0.4) may be more relaxed (checked at next decision point).

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | — |
| `triggerType` | String | Single | — |
| `triggerReference` | UUID | Single | — |
| `previousBasis` | Custom | Single | — |
| `newBasis` | Custom | Single | — |
| `policyApplied` | String | Single | — |
| `reassessmentResult` | String | Single | — |
| `reassessedAt` | DateTime | Single | generated |

