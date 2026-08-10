# Feature Specification: Submission Intake

**Feature Branch**: `002-submission-intake`

**Created**: 2026-08-10

**Status**: Draft

**Input**: Derived from the "BrokerConnect" eventmodelers.ai board (`80f53178-c291-43a0-8aa5-bc723990c5db`), chapter **Broker Connect**, context **Submission Intake** — pulled live via `GET .../slicedata?contextName=...` on 2026-08-10 and cached at `event-model/slices/*.json` / `event-model/import-config.json` (Phase A / MVP scope only — see `Project Plan/01-project-plan.md` §4; the 4 exploratory contexts are not yet pulled). Supersedes an earlier partial export now kept at `event-model/archive/` for provenance — see `event-model-to-speckit-guide.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - BrokerSubmissionReceived (Priority: P1)

As **ReceiveBrokerSubmission**, I want to brokerSubmissionReceived so that **BrokerSubmissionReceived** is recorded.

**Narrative** (verbatim from the board export): Renamed from SubmitBrokerSubmission - actor is the broker's system or Broker Connect's own ingestion service acting on the broker's behalf, not necessarily a direct user action. rawPayload is mapped to the ACORD standard schema (ADEPT) specifically - this is what enables automated data exchange across global broker systems generally, not a Howden-specific format, eliminating manual translation/mapping into internal databases.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **ReceiveBrokerSubmission** under the right preconditions and asserting that **BrokerSubmissionReceived** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** The raw receipt always succeeds if the payload arrives at all - always recorded regardless of what normalization/routing/duplicate-checking finds downstream. Class of business, territory, and other risk detail move to SubmissionNormalized once extraction succeeds., **When** ReceiveBrokerSubmission, **Then** BrokerSubmissionReceived

---

### User Story 2 - SubmissionRoutingRejected (Priority: P2)

The system maintains **BrokerAuthorizationExceptionLog**, projected from **SubmissionRoutingRejected**.

**Narrative** (verbatim from the board export): Never appears in any underwriter's queue. Visible to whoever manages broker/cell panel relationships (ops or business development) - may represent a legitimate request to extend the broker's panel, not just an error.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **SubmissionRoutingRejected** and asserting that **BrokerAuthorizationExceptionLog** reflects the update.

**Acceptance Scenarios**:

1. **Given** Broker's panel authorization doesn't cover the requested cell/class. Attempt is still recorded for broker relationship management - never silently dropped., **When** SubmissionRoutingRejected is appended, **Then** **BrokerAuthorizationExceptionLog** reflects it

---

### User Story 3 - SubmissionNormalized (Priority: P2)

The system maintains **SubmissionQueue**, projected from **SubmissionNormalized**.

**Narrative** (verbatim from the board export): Underwriter's main worklist. Possible-duplicate submissions stay in this normal flow with a badge (rather than a separate queue) since resolving them needs underwriter domain judgment, not just ops triage.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **SubmissionNormalized** and asserting that **SubmissionQueue** reflects the update.

**Acceptance Scenarios**:

1. **Given** ADEPT normalization succeeded - structured fields extracted from the raw payload. OPEN QUESTION (flagged by BA): is this genuinely a separate async event from BrokerSubmissionReceived, or should they collapse into one atomic event if normalization is synchronous/fast? Depends on actual ADEPT integration latency - revisit once that's known., **When** SubmissionNormalized is appended, **Then** **SubmissionQueue** reflects it

---

### User Story 4 - SubmissionNormalizationFailed (Priority: P2)

The system maintains **SubmissionExceptionQueue**, projected from **SubmissionNormalizationFailed**.

**Narrative** (verbatim from the board export): Separate from the underwriter's main queue - submissions stuck in a failed normalization state, for operations to chase the broker or manually intervene.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **SubmissionNormalizationFailed** and asserting that **SubmissionExceptionQueue** reflects the update.

**Acceptance Scenarios**:

1. **Given** Missing required field, malformed data, unrecognized class code, or schema validation error. Raw payload is always preserved, never discarded. OPEN QUESTIONS (flagged by BA): (1) does the broker get an automatic notification, or is chasing manual - silent failure risks brokers bypassing the platform entirely; (2) is there a retry limit / timeout before a failed submission is considered abandoned?, **When** SubmissionNormalizationFailed is appended, **Then** **SubmissionExceptionQueue** reflects it

---

### User Story 5 - SubmissionManuallyCorrected (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **SubmissionManuallyCorrected** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): Manual remediation path: operations or the broker (re-sending) fixes the data, re-triggering normalization.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **SubmissionManuallyCorrected** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** Manual remediation path: operations or the broker (re-sending) fixes the data, re-triggering normalization., **When** the triggering condition occurs, **Then** SubmissionManuallyCorrected

---

### User Story 6 - PotentialDuplicateSubmissionDetected (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **PotentialDuplicateSubmissionDetected** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): New submission is always recorded regardless (never silently dropped) - this event flags it as a possible resubmission alongside an existing open submission. OPEN QUESTION (flagged by BA): exact matching heuristic (exact-field vs fuzzy/probabilistic) materially affects false-positive rate and needs a real answer, not a placeholder.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **PotentialDuplicateSubmissionDetected** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** New submission is always recorded regardless (never silently dropped) - this event flags it as a possible resubmission alongside an existing open submission. OPEN QUESTION (flagged by BA): exact matching heuristic (exact-field vs fuzzy/probabilistic) materially affects false-positive rate and needs a real answer, not a placeholder., **When** the triggering condition occurs, **Then** PotentialDuplicateSubmissionDetected

---

### User Story 7 - SubmissionSuperseded (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **SubmissionSuperseded** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): Underwriter/ops confirms the new submission is a genuine resubmission/update of the original - modeled as its own linking event (old -> new) rather than silently discarding the original, preserving event-stream integrity.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **SubmissionSuperseded** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** Underwriter/ops confirms the new submission is a genuine resubmission/update of the original - modeled as its own linking event (old -> new) rather than silently discarding the original, preserving event-stream integrity., **When** the triggering condition occurs, **Then** SubmissionSuperseded

---

### User Story 8 - SubmissionConfirmedDistinct (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **SubmissionConfirmedDistinct** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): Underwriter/ops confirms the flagged submissions are coincidentally similar but genuinely distinct risks - both proceed independently.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **SubmissionConfirmedDistinct** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** Underwriter/ops confirms the flagged submissions are coincidentally similar but genuinely distinct risks - both proceed independently., **When** the triggering condition occurs, **Then** SubmissionConfirmedDistinct

---

### User Story 9 - BaselinePremiumGenerated (Priority: P2)

As a background policy in **Submission Intake**, the system reacts by executing **GenerateBaselinePremiumOnNormalization**, producing **BaselinePremiumGenerated**.

**Narrative** (verbatim from the board export): Trigger: SubmissionNormalized (success only).

**Why this priority**: System-driven policy that keeps derived state (bordereaux, projections, notifications) consistent after the primary action(s) that trigger it.

**Independent Test**: Can be tested by invoking **GenerateBaselinePremiumOnNormalization** under the right preconditions and asserting that **BaselinePremiumGenerated** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S1a.5: fires only on successful SubmissionNormalized - an incomplete/failed normalization shouldn't get a baseline price against partial data. Rating computation (EBM segmentation, satellite/weather cross-referencing) is a specialist capability this system requests and displays, never computes - same boundary discipline as ExportExposureExtract (1c.5) and PMLRecalculated (1d.2)., **When** GenerateBaselinePremiumOnNormalization, **Then** BaselinePremiumGenerated

---

### User Story 10 - PricingBaselineAccepted (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **PricingBaselineAccepted** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): Fires alongside AssessSubmission (S2.1) when the underwriter's proposedTerms match BaselinePremiumGenerated exactly. Actuarial needs to measure how often/how much underwriters deviate from the model to evaluate model performance - invisible unless captured explicitly, so this and PricingBaselineOverridden are modeled even though they add no new business decision by themselves.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **PricingBaselineAccepted** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** Fires alongside AssessSubmission (S2.1) when the underwriter's proposedTerms match BaselinePremiumGenerated exactly. Actuarial needs to measure how often/how much underwriters deviate from the model to evaluate model performance - invisible unless captured explicitly, so this and PricingBaselineOverridden are modeled even though they add no new business decision by themselves., **When** the triggering condition occurs, **Then** PricingBaselineAccepted

---

### User Story 11 - PricingBaselineOverridden (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **PricingBaselineOverridden** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): Fires alongside AssessSubmission when the underwriter's proposedTerms diverge from the baseline. Same model-feedback rationale as PricingBaselineAccepted.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **PricingBaselineOverridden** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** Fires alongside AssessSubmission when the underwriter's proposedTerms diverge from the baseline. Same model-feedback rationale as PricingBaselineAccepted., **When** the triggering condition occurs, **Then** PricingBaselineOverridden

---

### User Story 12 - PricingModelVersionDeployed (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **PricingModelVersionDeployed** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): Rapid model deployment ('quarters to hours' per TFP's own materials) means BaselinePremiumGenerated.modelVersion needs a real referent - this is that referent, audit reference data only.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **PricingModelVersionDeployed** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** Rapid model deployment ('quarters to hours' per TFP's own materials) means BaselinePremiumGenerated.modelVersion needs a real referent - this is that referent, audit reference data only., **When** the triggering condition occurs, **Then** PricingModelVersionDeployed

---

### Edge Cases

- What happens under the condition described for **SubmissionManuallyCorrected**? System records it as a standalone fact — Manual remediation path: operations or the broker (re-sending) fixes the data, re-triggering normalization. (no command/processor of its own in the source export).
- What happens under the condition described for **PotentialDuplicateSubmissionDetected**? System records it as a standalone fact — New submission is always recorded regardless (never silently dropped) - this event flags it as a possible resubmission alongside an existing open submission. OPEN QUESTION (flagged by BA): exact matching heuristic (exact-field vs fuzzy/probabilistic) materially affects false-positive rate and needs a real answer, not a placeholder. (no command/processor of its own in the source export).
- What happens under the condition described for **SubmissionSuperseded**? System records it as a standalone fact — Underwriter/ops confirms the new submission is a genuine resubmission/update of the original - modeled as its own linking event (old -> new) rather than silently discarding the original, preserving event-stream integrity. (no command/processor of its own in the source export).
- What happens under the condition described for **SubmissionConfirmedDistinct**? System records it as a standalone fact — Underwriter/ops confirms the flagged submissions are coincidentally similar but genuinely distinct risks - both proceed independently. (no command/processor of its own in the source export).
- What happens under the condition described for **PricingBaselineAccepted**? System records it as a standalone fact — Fires alongside AssessSubmission (S2.1) when the underwriter's proposedTerms match BaselinePremiumGenerated exactly. Actuarial needs to measure how often/how much underwriters deviate from the model to evaluate model performance - invisible unless captured explicitly, so this and PricingBaselineOverridden are modeled even though they add no new business decision by themselves. (no command/processor of its own in the source export).
- What happens under the condition described for **PricingBaselineOverridden**? System records it as a standalone fact — Fires alongside AssessSubmission when the underwriter's proposedTerms diverge from the baseline. Same model-feedback rationale as PricingBaselineAccepted. (no command/processor of its own in the source export).
- What happens under the condition described for **PricingModelVersionDeployed**? System records it as a standalone fact — Rapid model deployment ('quarters to hours' per TFP's own materials) means BaselinePremiumGenerated.modelVersion needs a real referent - this is that referent, audit reference data only. (no command/processor of its own in the source export).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support **ReceiveBrokerSubmission**, producing the **BrokerSubmissionReceived** domain event(s).
- **FR-002**: System MUST project **BrokerAuthorizationExceptionLog** from the **SubmissionRoutingRejected** domain event(s).
- **FR-003**: System MUST project **SubmissionQueue** from the **SubmissionNormalized** domain event(s).
- **FR-004**: System MUST project **SubmissionExceptionQueue** from the **SubmissionNormalizationFailed** domain event(s).
- **FR-005**: System MUST record the **SubmissionManuallyCorrected** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-006**: System MUST record the **PotentialDuplicateSubmissionDetected** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-007**: System MUST record the **SubmissionSuperseded** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-008**: System MUST record the **SubmissionConfirmedDistinct** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-009**: System MUST support **GenerateBaselinePremiumOnNormalization**, producing the **BaselinePremiumGenerated** domain event(s).
- **FR-010**: System MUST record the **PricingBaselineAccepted** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-011**: System MUST record the **PricingBaselineOverridden** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-012**: System MUST record the **PricingModelVersionDeployed** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).

### Key Entities *(include if feature involves data)*

- **Submission**: aggregate in the **Submission Intake** context; touched by **BaselinePremiumGenerated**, **BrokerSubmissionReceived**, **GenerateBaselinePremiumOnNormalization**, **PotentialDuplicateSubmissionDetected**, **PricingBaselineAccepted**, **PricingBaselineOverridden**, **ReceiveBrokerSubmission**, **SubmissionConfirmedDistinct**, **SubmissionManuallyCorrected**, **SubmissionNormalizationFailed**, **SubmissionNormalized**, **SubmissionRoutingRejected**, **SubmissionSuperseded**.
- **PricingModel**: aggregate in the **Submission Intake** context; touched by **PricingModelVersionDeployed**.
- **BrokerAuthorizationExceptionLog** (read model): Never appears in any underwriter's queue. Visible to whoever manages broker/cell panel relationships (ops or business development) - may represent a legitimate request to extend the broker's panel, not just an error. Fields: `submissionId`, `brokerFirmId`, `requestedCellId`, `requestedClassOfBusiness`, `rejectionReason`, `rejectedAt`, `reviewStatus`.
- **SubmissionQueue** (read model): Underwriter's main worklist. Possible-duplicate submissions stay in this normal flow with a badge (rather than a separate queue) since resolving them needs underwriter domain judgment, not just ops triage. Fields: `submissionId`, `brokerFirmId`, `classOfBusiness`, `territory`, `lineSizeSought`, `receivedAt`, `status`, `isPossibleDuplicate`, `suspectedOriginalSubmissionId`.
- **SubmissionExceptionQueue** (read model): Separate from the underwriter's main queue - submissions stuck in a failed normalization state, for operations to chase the broker or manually intervene. Fields: `submissionId`, `brokerFirmId`, `failureReason`, `attemptedAt`, `status`.
- **PricedSubmissionView** (read model): The broker's ask alongside the AI baseline, side by side, before the underwriter opens the file - this is what makes assessment 'auditing the AI-generated model' rather than pricing from scratch. Fields: `submissionId`, `brokerRequestedTerms`, `baselinePremium`, `riskFactorSummary`, `modelVersion`.

## Success Criteria *(mandatory)*

<!--
  NOTE: the source eventmodelers board did not define measurable success metrics for this
  context — these are template placeholders only. Fill in via /speckit-clarify or manually
  before /speckit-plan.
-->

### Measurable Outcomes

- **SC-001**: [Measurable metric, e.g., "Users can complete account creation in under 2 minutes"]
- **SC-002**: [Measurable metric, e.g., "System handles 1000 concurrent users without degradation"]
- **SC-003**: [User satisfaction metric, e.g., "90% of users successfully complete primary task on first attempt"]
- **SC-004**: [Business metric, e.g., "Reduce support tickets related to [X] by 50%"]

## Assumptions

- Derived from the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Submission Intake**); command/event names, field lists, descriptions, aggregates, and lanes in the Event Model Detail section below are transcribed directly from a live pull of that board, not invented for this document.
- Pulled live on 2026-08-10 via the `slicedata` endpoint (see `event-model-to-speckit-guide.md`, "The real export endpoint"), not the raw event-replay log — re-run `event-model/build-scripts/gen_specs_from_slices.py` after any further board edits to keep this in sync; nothing here watches the board automatically.
- All 12 slice(s) in this context currently carry board status `Created` — none are `Planned` or built yet.
- The source board did not specify performance, scale, or business-metric assumptions for this context — the Success Criteria above are template placeholders.
- [Assumption about scope boundaries — confirm before /speckit.plan]

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export, including every field's type, cardinality, and flags (`id` = identifier field, `generated` = system-generated, `optional` = nullable). Nested `List`/object fields show their subfields indented beneath them with `↳`.

### Slice: BrokerSubmissionReceived (`8b6e908b-6bed-4af5-a482-c6682147ab61`, status: Created, type: STATE_CHANGE)

**ReceiveBrokerSubmission** (command, id `62463707-86df-4058-9131-c52ddad00f86`, aggregate `Submission`, lane `Interaction`, modelContext `Submission Intake`)

> Renamed from SubmitBrokerSubmission - actor is the broker's system or Broker Connect's own ingestion service acting on the broker's behalf, not necessarily a direct user action. rawPayload is mapped to the ACORD standard schema (ADEPT) specifically - this is what enables automated data exchange across global broker systems generally, not a Howden-specific format, eliminating manual translation/mapping into internal databases.

Dependencies: → BrokerSubmissionReceived (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `brokerFirmId` | String | Single | — |
| `submittingContact` | String | Single | — |
| `cellIdHint` | String | Single | optional |
| `classOfBusinessHint` | String | Single | optional |
| `rawPayload` | Custom | Single | — |
| `sourceChannel` | String | Single | — |

**BrokerSubmissionReceived** (event, id `4162ded2-de9d-4ade-808f-7f873168f48f`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> The raw receipt always succeeds if the payload arrives at all - always recorded regardless of what normalization/routing/duplicate-checking finds downstream. Class of business, territory, and other risk detail move to SubmissionNormalized once extraction succeeds.

Dependencies: ← ReceiveBrokerSubmission (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id, generated |
| `brokerFirmId` | String | Single | — |
| `submittingContact` | String | Single | — |
| `rawPayloadRef` | Custom | Single | — |
| `sourceChannel` | String | Single | — |
| `receivedAt` | DateTime | Single | generated |

### Slice: SubmissionRoutingRejected (`e1fc8e36-328f-4c5a-bd50-19dfb41d7499`, status: Created, type: STATE_VIEW)

**SubmissionRoutingRejected** (event, id `41e10e7a-dcb8-46fe-a7a0-692453a07e8b`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> Broker's panel authorization doesn't cover the requested cell/class. Attempt is still recorded for broker relationship management - never silently dropped.

Dependencies: → BrokerAuthorizationExceptionLog (READMODEL); → SubmissionQueue (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `brokerFirmId` | String | Single | — |
| `requestedCellId` | String | Single | — |
| `requestedClassOfBusiness` | String | Single | optional |
| `rejectionReason` | String | Single | — |
| `rejectedAt` | DateTime | Single | generated |

**BrokerAuthorizationExceptionLog** (read model, id `0723e966-26ba-412a-a067-1f800fc45d3a`, aggregate `Submission`, lane `Interaction`, modelContext `Submission Intake`)

> Never appears in any underwriter's queue. Visible to whoever manages broker/cell panel relationships (ops or business development) - may represent a legitimate request to extend the broker's panel, not just an error.

Dependencies: ← SubmissionRoutingRejected (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `brokerFirmId` | String | Single | — |
| `requestedCellId` | String | Single | — |
| `requestedClassOfBusiness` | String | Single | optional |
| `rejectionReason` | String | Single | — |
| `rejectedAt` | DateTime | Single | — |
| `reviewStatus` | String | Single | — |

### Slice: SubmissionNormalized (`3193140f-a140-4e8f-83ee-32d4ebc9ee46`, status: Created, type: STATE_VIEW)

**SubmissionNormalized** (event, id `aee58f84-8eab-4642-991b-b63cb4e8d0d6`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> ADEPT normalization succeeded - structured fields extracted from the raw payload. OPEN QUESTION (flagged by BA): is this genuinely a separate async event from BrokerSubmissionReceived, or should they collapse into one atomic event if normalization is synchronous/fast? Depends on actual ADEPT integration latency - revisit once that's known.

Dependencies: → SubmissionQueue (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `classOfBusiness` | String | Single | — |
| `territory` | String | Single | — |
| `lineSizeSought` | Decimal | Single | — |
| `keyTerms` | String | Single | optional |
| `namedInsured` | String | Single | — |
| `effectiveDateRequested` | Date | Single | — |
| `normalizationStatus` | String | Single | — |
| `normalizedAt` | DateTime | Single | generated |

**SubmissionQueue** (read model, id `1482bc8d-feaf-4f27-8a3b-759b936a6e0e`, aggregate `Submission`, lane `Interaction`, modelContext `Submission Intake`)

> Underwriter's main worklist. Possible-duplicate submissions stay in this normal flow with a badge (rather than a separate queue) since resolving them needs underwriter domain judgment, not just ops triage.

Dependencies: ← SubmissionNormalized (EVENT); → SubmissionQueue (SCREEN); ← SubmissionRoutingRejected (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `brokerFirmId` | String | Single | — |
| `classOfBusiness` | String | Single | — |
| `territory` | String | Single | — |
| `lineSizeSought` | Decimal | Single | — |
| `receivedAt` | DateTime | Single | — |
| `status` | String | Single | — |
| `isPossibleDuplicate` | Boolean | Single | — |
| `suspectedOriginalSubmissionId` | UUID | Single | optional |

### Slice: SubmissionNormalizationFailed (`21e05979-88d6-4900-b793-ff9801ce1707`, status: Created, type: STATE_VIEW)

**SubmissionNormalizationFailed** (event, id `77a810d6-3146-4012-b78c-bcbf64c696c2`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> Missing required field, malformed data, unrecognized class code, or schema validation error. Raw payload is always preserved, never discarded. OPEN QUESTIONS (flagged by BA): (1) does the broker get an automatic notification, or is chasing manual - silent failure risks brokers bypassing the platform entirely; (2) is there a retry limit / timeout before a failed submission is considered abandoned?

Dependencies: → SubmissionExceptionQueue (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `failureReason` | String | Single | — |
| `rawPayloadRef` | Custom | Single | — |
| `attemptedAt` | DateTime | Single | generated |

**SubmissionExceptionQueue** (read model, id `ac0209b1-39ac-4c41-994a-d658cdb64ea6`, aggregate `Submission`, lane `Interaction`, modelContext `Submission Intake`)

> Separate from the underwriter's main queue - submissions stuck in a failed normalization state, for operations to chase the broker or manually intervene.

Dependencies: ← SubmissionNormalizationFailed (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `brokerFirmId` | String | Single | — |
| `failureReason` | String | Single | — |
| `attemptedAt` | DateTime | Single | — |
| `status` | String | Single | — |

### Slice: SubmissionManuallyCorrected (`e382fb6c-b5a2-4054-abaf-a434271efc4c`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**SubmissionManuallyCorrected** (event, id `825b0bd5-217a-413a-afad-5e723e1fbab2`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> Manual remediation path: operations or the broker (re-sending) fixes the data, re-triggering normalization.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `correctedBy` | String | Single | — |
| `correctionDescription` | String | Single | — |
| `resubmittedForNormalization` | Boolean | Single | — |
| `correctedAt` | DateTime | Single | generated |

### Slice: PotentialDuplicateSubmissionDetected (`1a638cc6-e2be-4403-815a-3067d3d22b83`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**PotentialDuplicateSubmissionDetected** (event, id `d2066894-0f37-4237-934b-0027da71b0ca`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> New submission is always recorded regardless (never silently dropped) - this event flags it as a possible resubmission alongside an existing open submission. OPEN QUESTION (flagged by BA): exact matching heuristic (exact-field vs fuzzy/probabilistic) materially affects false-positive rate and needs a real answer, not a placeholder.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `suspectedOriginalSubmissionId` | UUID | Single | — |
| `matchBasis` | String | Single | — |
| `confidenceLevel` | Decimal | Single | optional |
| `detectedAt` | DateTime | Single | generated |

### Slice: SubmissionSuperseded (`82de995d-43ce-4d24-b03c-85ed2f276caf`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**SubmissionSuperseded** (event, id `8963a8e5-52ee-4a51-a8d9-5076361874b7`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> Underwriter/ops confirms the new submission is a genuine resubmission/update of the original - modeled as its own linking event (old -> new) rather than silently discarding the original, preserving event-stream integrity.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `originalSubmissionId` | UUID | Single | id |
| `supersedingSubmissionId` | UUID | Single | — |
| `linkedBy` | String | Single | — |
| `linkedAt` | DateTime | Single | generated |

### Slice: SubmissionConfirmedDistinct (`9b53a519-b5ea-494a-b538-07eb3836433b`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**SubmissionConfirmedDistinct** (event, id `3d1dc042-b6d6-43db-a269-8b02f3591984`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> Underwriter/ops confirms the flagged submissions are coincidentally similar but genuinely distinct risks - both proceed independently.

Dependencies: → PricedSubmissionView (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `suspectedOriginalSubmissionId` | UUID | Single | — |
| `confirmedBy` | String | Single | — |
| `confirmedAt` | DateTime | Single | generated |

### Slice: BaselinePremiumGenerated (`69f0d3b4-7a22-4623-bb83-a7720c8dfc02`, status: Created, type: AUTOMATION)

**GenerateBaselinePremiumOnNormalization** (automation/processor, id `8c0b207b-cb39-4704-9b83-e0bccb07f577`, aggregate `Submission`, lane `Actor`, modelContext `Submission Intake`)

> Trigger: SubmissionNormalized (success only).

Dependencies: ← PricedSubmissionView (READMODEL)

_(no fields)_

**BaselinePremiumGenerated** (event, id `572cf336-11cf-44df-8a55-7f8c2049adbd`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> S1a.5: fires only on successful SubmissionNormalized - an incomplete/failed normalization shouldn't get a baseline price against partial data. Rating computation (EBM segmentation, satellite/weather cross-referencing) is a specialist capability this system requests and displays, never computes - same boundary discipline as ExportExposureExtract (1c.5) and PMLRecalculated (1d.2).

Dependencies: → PricedSubmissionView (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | — |
| `baselinePremium` | Decimal | Single | — |
| `riskFactorSummary` | Custom | Single | — |
| `modelVersion` | String | Single | — |
| `generatedAt` | DateTime | Single | generated |

**PricedSubmissionView** (read model, id `221f7b05-3a95-49a5-bd71-516527ceafaa`, aggregate `Submission`, lane `Interaction`, modelContext `Submission Intake`)

> The broker's ask alongside the AI baseline, side by side, before the underwriter opens the file - this is what makes assessment 'auditing the AI-generated model' rather than pricing from scratch.

Dependencies: ← BaselinePremiumGenerated (EVENT); → GenerateBaselinePremiumOnNormalization (AUTOMATION); ← SubmissionConfirmedDistinct (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `brokerRequestedTerms` | Custom | Single | — |
| `baselinePremium` | Decimal | Single | — |
| `riskFactorSummary` | Custom | Single | — |
| `modelVersion` | String | Single | — |

### Slice: PricingBaselineAccepted (`c8ea745d-c494-4d36-9dad-887b5b88956d`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**PricingBaselineAccepted** (event, id `fd60dce0-7875-49f2-bd36-19e837f02552`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> Fires alongside AssessSubmission (S2.1) when the underwriter's proposedTerms match BaselinePremiumGenerated exactly. Actuarial needs to measure how often/how much underwriters deviate from the model to evaluate model performance - invisible unless captured explicitly, so this and PricingBaselineOverridden are modeled even though they add no new business decision by themselves.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `baselinePremium` | Decimal | Single | — |
| `underwriterId` | String | Single | — |
| `acceptedAt` | DateTime | Single | generated |

### Slice: PricingBaselineOverridden (`4f0f9a6b-479c-4686-b703-55b1531aec94`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**PricingBaselineOverridden** (event, id `06397ac6-cb22-409b-8c75-fb5392541e92`, aggregate `Submission`, lane `Submission`, modelContext `Submission Intake`)

> Fires alongside AssessSubmission when the underwriter's proposedTerms diverge from the baseline. Same model-feedback rationale as PricingBaselineAccepted.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `baselinePremium` | Decimal | Single | — |
| `proposedPremium` | Decimal | Single | — |
| `variance` | Decimal | Single | — |
| `underwriterId` | String | Single | — |
| `overriddenAt` | DateTime | Single | generated |

### Slice: PricingModelVersionDeployed (`924d3801-6847-4059-b26d-fe00f334b56b`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**PricingModelVersionDeployed** (event, id `b790038e-ba27-4347-bfb8-41a3292c819b`, aggregate `PricingModel`, lane `Submission`, modelContext `Submission Intake`)

> Rapid model deployment ('quarters to hours' per TFP's own materials) means BaselinePremiumGenerated.modelVersion needs a real referent - this is that referent, audit reference data only.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `modelVersion` | String | Single | — |
| `deployedBy` | String | Single | — |
| `deployedAt` | DateTime | Single | generated |
| `changeSummary` | String | Single | optional |

