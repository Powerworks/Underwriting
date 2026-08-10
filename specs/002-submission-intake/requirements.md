---
spec: 002-submission-intake
phase: requirements
created: 2026-08-10
---

# Requirements: Submission Intake

## Problem Statement

Submission Intake is one of the bounded contexts modeled on the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Submission Intake**). Evidence: the board's own live export (`event-model/import-config.json`), transcribed verbatim in the Event Model Detail appendix below — this is not a hypothesis needing validation, the domain already exists as a modeled event model.

## Goal

Implement the **Submission Intake** bounded context's commands, events, automations, and read models exactly as modeled on the board, per constitution Principle III (the spec is the source of truth — no invented fields, no guessed names).

## User Stories

### US-1: BrokerSubmissionReceived

**As a** ReceiveBrokerSubmission
**I want to** brokerSubmissionReceived
**So that** BrokerSubmissionReceived is recorded

_Narrative (verbatim from the board export)_: Renamed from SubmitBrokerSubmission - actor is the broker's system or Broker Connect's own ingestion service acting on the broker's behalf, not necessarily a direct user action. rawPayload is mapped to the ACORD standard schema (ADEPT) specifically - this is what enables automated data exchange across global broker systems generally, not a Howden-specific format, eliminating manual translation/mapping into internal databases.

**Acceptance Criteria:**
- AC-1.1: Given The raw receipt always succeeds if the payload arrives at all - always recorded regardless of what normalization/routing/duplicate-checking finds downstream. Class of business, territory, and other risk detail move to SubmissionNormalized once extraction succeeds., When ReceiveBrokerSubmission, Then BrokerSubmissionReceived

### US-2: SubmissionRoutingRejected

**As a** a consumer of this read-side projection
**I want to** BrokerAuthorizationExceptionLog to reflect SubmissionRoutingRejected
**So that** downstream queries/decisions see current state

_Narrative (verbatim from the board export)_: Never appears in any underwriter's queue. Visible to whoever manages broker/cell panel relationships (ops or business development) - may represent a legitimate request to extend the broker's panel, not just an error.

**Acceptance Criteria:**
- AC-2.1: Given Broker's panel authorization doesn't cover the requested cell/class. Attempt is still recorded for broker relationship management - never silently dropped., When SubmissionRoutingRejected is appended, Then BrokerAuthorizationExceptionLog reflects it

### US-3: SubmissionNormalized

**As a** a consumer of this read-side projection
**I want to** SubmissionQueue to reflect SubmissionNormalized
**So that** downstream queries/decisions see current state

_Narrative (verbatim from the board export)_: Underwriter's main worklist. Possible-duplicate submissions stay in this normal flow with a badge (rather than a separate queue) since resolving them needs underwriter domain judgment, not just ops triage.

**Acceptance Criteria:**
- AC-3.1: Given ADEPT normalization succeeded - structured fields extracted from the raw payload. OPEN QUESTION (flagged by BA): is this genuinely a separate async event from BrokerSubmissionReceived, or should they collapse into one atomic event if normalization is synchronous/fast? Depends on actual ADEPT integration latency - revisit once that's known., When SubmissionNormalized is appended, Then SubmissionQueue reflects it

### US-4: SubmissionNormalizationFailed

**As a** a consumer of this read-side projection
**I want to** SubmissionExceptionQueue to reflect SubmissionNormalizationFailed
**So that** downstream queries/decisions see current state

_Narrative (verbatim from the board export)_: Separate from the underwriter's main queue - submissions stuck in a failed normalization state, for operations to chase the broker or manually intervene.

**Acceptance Criteria:**
- AC-4.1: Given Missing required field, malformed data, unrecognized class code, or schema validation error. Raw payload is always preserved, never discarded. OPEN QUESTIONS (flagged by BA): (1) does the broker get an automatic notification, or is chasing manual - silent failure risks brokers bypassing the platform entirely; (2) is there a retry limit / timeout before a failed submission is considered abandoned?, When SubmissionNormalizationFailed is appended, Then SubmissionExceptionQueue reflects it

### US-5: SubmissionManuallyCorrected

**As a** the system
**I want to** to record SubmissionManuallyCorrected
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: Manual remediation path: operations or the broker (re-sending) fixes the data, re-triggering normalization.

**Acceptance Criteria:**
- AC-5.1: Given Manual remediation path: operations or the broker (re-sending) fixes the data, re-triggering normalization., When the triggering condition occurs, Then SubmissionManuallyCorrected

### US-6: PotentialDuplicateSubmissionDetected

**As a** the system
**I want to** to record PotentialDuplicateSubmissionDetected
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: New submission is always recorded regardless (never silently dropped) - this event flags it as a possible resubmission alongside an existing open submission. OPEN QUESTION (flagged by BA): exact matching heuristic (exact-field vs fuzzy/probabilistic) materially affects false-positive rate and needs a real answer, not a placeholder.

**Acceptance Criteria:**
- AC-6.1: Given New submission is always recorded regardless (never silently dropped) - this event flags it as a possible resubmission alongside an existing open submission. OPEN QUESTION (flagged by BA): exact matching heuristic (exact-field vs fuzzy/probabilistic) materially affects false-positive rate and needs a real answer, not a placeholder., When the triggering condition occurs, Then PotentialDuplicateSubmissionDetected

### US-7: SubmissionSuperseded

**As a** the system
**I want to** to record SubmissionSuperseded
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: Underwriter/ops confirms the new submission is a genuine resubmission/update of the original - modeled as its own linking event (old -> new) rather than silently discarding the original, preserving event-stream integrity.

**Acceptance Criteria:**
- AC-7.1: Given Underwriter/ops confirms the new submission is a genuine resubmission/update of the original - modeled as its own linking event (old -> new) rather than silently discarding the original, preserving event-stream integrity., When the triggering condition occurs, Then SubmissionSuperseded

### US-8: SubmissionConfirmedDistinct

**As a** the system
**I want to** to record SubmissionConfirmedDistinct
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: Underwriter/ops confirms the flagged submissions are coincidentally similar but genuinely distinct risks - both proceed independently.

**Acceptance Criteria:**
- AC-8.1: Given Underwriter/ops confirms the flagged submissions are coincidentally similar but genuinely distinct risks - both proceed independently., When the triggering condition occurs, Then SubmissionConfirmedDistinct

### US-9: BaselinePremiumGenerated

**As a** background policy in Submission Intake
**I want to** the system to execute GenerateBaselinePremiumOnNormalization automatically
**So that** derived state stays consistent after the triggering action(s): BaselinePremiumGenerated

_Narrative (verbatim from the board export)_: Trigger: SubmissionNormalized (success only).

**Acceptance Criteria:**
- AC-9.1: Given S1a.5: fires only on successful SubmissionNormalized - an incomplete/failed normalization shouldn't get a baseline price against partial data. Rating computation (EBM segmentation, satellite/weather cross-referencing) is a specialist capability this system requests and displays, never computes - same boundary discipline as ExportExposureExtract (1c.5) and PMLRecalculated (1d.2)., When GenerateBaselinePremiumOnNormalization, Then BaselinePremiumGenerated

### US-10: PricingBaselineAccepted

**As a** the system
**I want to** to record PricingBaselineAccepted
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: Fires alongside AssessSubmission (S2.1) when the underwriter's proposedTerms match BaselinePremiumGenerated exactly. Actuarial needs to measure how often/how much underwriters deviate from the model to evaluate model performance - invisible unless captured explicitly, so this and PricingBaselineOverridden are modeled even though they add no new business decision by themselves.

**Acceptance Criteria:**
- AC-10.1: Given Fires alongside AssessSubmission (S2.1) when the underwriter's proposedTerms match BaselinePremiumGenerated exactly. Actuarial needs to measure how often/how much underwriters deviate from the model to evaluate model performance - invisible unless captured explicitly, so this and PricingBaselineOverridden are modeled even though they add no new business decision by themselves., When the triggering condition occurs, Then PricingBaselineAccepted

### US-11: PricingBaselineOverridden

**As a** the system
**I want to** to record PricingBaselineOverridden
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: Fires alongside AssessSubmission when the underwriter's proposedTerms diverge from the baseline. Same model-feedback rationale as PricingBaselineAccepted.

**Acceptance Criteria:**
- AC-11.1: Given Fires alongside AssessSubmission when the underwriter's proposedTerms diverge from the baseline. Same model-feedback rationale as PricingBaselineAccepted., When the triggering condition occurs, Then PricingBaselineOverridden

### US-12: PricingModelVersionDeployed

**As a** the system
**I want to** to record PricingModelVersionDeployed
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: Rapid model deployment ('quarters to hours' per TFP's own materials) means BaselinePremiumGenerated.modelVersion needs a real referent - this is that referent, audit reference data only.

**Acceptance Criteria:**
- AC-12.1: Given Rapid model deployment ('quarters to hours' per TFP's own materials) means BaselinePremiumGenerated.modelVersion needs a real referent - this is that referent, audit reference data only., When the triggering condition occurs, Then PricingModelVersionDeployed

## Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| FR-1 | System MUST support ReceiveBrokerSubmission, producing the BrokerSubmissionReceived domain event(s) | Must | AC-1.1 |
| FR-2 | System SHOULD project BrokerAuthorizationExceptionLog from the SubmissionRoutingRejected domain event(s) | Should | AC-2.1 |
| FR-3 | System SHOULD project SubmissionQueue from the SubmissionNormalized domain event(s) | Should | AC-3.1 |
| FR-4 | System SHOULD project SubmissionExceptionQueue from the SubmissionNormalizationFailed domain event(s) | Should | AC-4.1 |
| FR-5 | System COULD record the SubmissionManuallyCorrected domain event(s) under the condition described in US-5's narrative | Could | AC-5.1 |
| FR-6 | System COULD record the PotentialDuplicateSubmissionDetected domain event(s) under the condition described in US-6's narrative | Could | AC-6.1 |
| FR-7 | System COULD record the SubmissionSuperseded domain event(s) under the condition described in US-7's narrative | Could | AC-7.1 |
| FR-8 | System COULD record the SubmissionConfirmedDistinct domain event(s) under the condition described in US-8's narrative | Could | AC-8.1 |
| FR-9 | System SHOULD support GenerateBaselinePremiumOnNormalization, producing the BaselinePremiumGenerated domain event(s) | Should | AC-9.1 |
| FR-10 | System COULD record the PricingBaselineAccepted domain event(s) under the condition described in US-10's narrative | Could | AC-10.1 |
| FR-11 | System COULD record the PricingBaselineOverridden domain event(s) under the condition described in US-11's narrative | Could | AC-11.1 |
| FR-12 | System COULD record the PricingModelVersionDeployed domain event(s) under the condition described in US-12's narrative | Could | AC-12.1 |

## Non-Functional Requirements

<!-- The source board does not specify numeric NFR targets for this context — every row is N/A, not invented, per constitution Principle III. Revisit via a clarification pass before /ralph-specum:design if any of these genuinely matter for this feature. -->

| ID | Requirement | Metric | Target |
|----|-------------|--------|--------|
| NFR-1 | Performance | N/A | N/A: not specified by board export |
| NFR-2 | Reliability | N/A | N/A: not specified by board export |
| NFR-3 | Security | N/A | N/A: not specified by board export |

## Glossary

- **Submission**: Aggregate in the Submission Intake context (see Event Model Detail for the elements that touch it).
- **BrokerAuthorizationExceptionLog**: Read model. Never appears in any underwriter's queue. Visible to whoever manages broker/cell panel relationships (ops or business development) - may represent a legitimate request to extend the broker's panel, not just an error.
- **SubmissionQueue**: Read model. Underwriter's main worklist. Possible-duplicate submissions stay in this normal flow with a badge (rather than a separate queue) since resolving them needs underwriter domain judgment, not just ops triage.
- **SubmissionExceptionQueue**: Read model. Separate from the underwriter's main queue - submissions stuck in a failed normalization state, for operations to chase the broker or manually intervene.
- **PricedSubmissionView**: Read model. The broker's ask alongside the AI baseline, side by side, before the underwriter opens the file - this is what makes assessment 'auditing the AI-generated model' rather than pricing from scratch.
- **PricingModel**: Aggregate in the Submission Intake context (see Event Model Detail for the elements that touch it).

## Out of Scope

Default-scope rule: anything not listed here that falls under the Goal is in scope.

- The 4 exploratory contexts (Phase B) — not yet pulled from the board, out of scope for this pass.
- Any command/event/read model not present in the Event Model Detail appendix below.

## Dependencies

- Cross-context dependencies are visible in the Event Model Detail appendix below (each element's own `Dependencies`/`Aggregate dependencies` lines) but not resolved to spec names here — cross-reference `elementType`/`title` against the other features' own Event Model Detail sections manually.

## Success Criteria

<!-- The source board does not define measurable success metrics for this context — fill in via a clarification pass, don't invent numbers. -->

- TBD (user, next review)

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Board export goes stale relative to a live board edit | Medium | Re-run `gen_specs_from_slices.py` against a fresh pull before trusting this file; nothing here watches the board automatically |

## Unresolved Questions

- `SubmissionManuallyCorrected` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review
- `PotentialDuplicateSubmissionDetected` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review
- `SubmissionSuperseded` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review
- `SubmissionConfirmedDistinct` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review
- `PricingBaselineAccepted` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review
- `PricingBaselineOverridden` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review
- `PricingModelVersionDeployed` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export — every field's type, cardinality, and flags. This is the lossless source; the User Stories above are a readable summary of it, not the other way around. Screens are deliberately excluded here — see this spec's `research.md` (UI Reference section), which the design phase reads directly; screens are UI reference, not a requirement.

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

