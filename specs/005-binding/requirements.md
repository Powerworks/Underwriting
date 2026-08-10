---
spec: 005-binding
phase: requirements
created: 2026-08-10
---

# Requirements: Binding

## Problem Statement

Binding is one of the bounded contexts modeled on the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Binding**). Evidence: the board's own live export (`event-model/import-config.json`), transcribed verbatim in the Event Model Detail appendix below — this is not a hypothesis needing validation, the domain already exists as a modeled event model.

## Goal

Implement the **Binding** bounded context's commands, events, automations, and read models exactly as modeled on the board, per constitution Principle III (the spec is the source of truth — no invented fields, no guessed names).

## User Stories

### US-1: PolicyBound

**As a** BindPolicy
**I want to** policyBound
**So that** PolicyBound is recorded

_Narrative (verbatim from the board export)_: S3.1/S3.2: requires explicit underwriter confirmation, not fired automatically on QuoteAccepted - broker acceptance and the underwriter's final bind action are conceptually different moments (documentation checks, subjectivities being cleared).

**Acceptance Criteria:**
- AC-1.1: Given S3.1/S3.2: the immutable anchor for the policy's lifecycle - once fired, endorsements/cancellations append to history rather than rewriting this record. Everything downstream (Bordereaux in Context 4, Claims in Context 5) keys off it., When BindPolicy, Then PolicyBound

### US-2: PolicyEndorsed

**As a** EndorsePolicy
**I want to** policyEndorsed
**So that** PolicyEndorsed is recorded

_Narrative (verbatim from the board export)_: S3.3: broker-initiated change request, processed by the underwriter.

**Acceptance Criteria:**
- AC-2.1: Given S3.3: fires for administrative changes directly. Material changes (premium/limit/exposure) instead produce EndorsementReferred if they exceed the underwriter's authority - resolves the open thread from S1c.3: an endorsement that increases exposure now goes through the same authority/referral check as an original bind, reusing Context 2's existing mechanism rather than a lightweight edit path that quietly bypasses governance., When EndorsePolicy, Then PolicyEndorsed

### US-3: PolicyCancelled

**As a** CancelPolicy
**I want to** policyCancelled
**So that** PolicyCancelled is recorded

_Narrative (verbatim from the board export)_: S3.4

**Acceptance Criteria:**
- AC-3.1: Given S3.4: routine broker-requested or underwriter-initiated cancellation. Triggers the exposure reduction already modeled in S1c.4 - return premium calculation itself feeds Bordereaux/Settlement (Context 4), not computed here. For-cause cancellations use the separate PolicyCancelledForCause event instead, given the compliance/dispute implications of material misrepresentation., When CancelPolicy, Then PolicyCancelled

### US-4: PolicyRenewalInitiated

**As a** InitiateRenewal
**I want to** policyRenewalInitiated
**So that** PolicyRenewalInitiated is recorded

_Narrative (verbatim from the board export)_: S3.5: broker-initiated or system-prompted ahead of expiry. Renamed from RenewPolicy for clarity that this only initiates the loop, it doesn't itself bind anything.

**Acceptance Criteria:**
- AC-4.1: Given S3.5: 'loops back into a fresh submission/bind cycle, not shown as a literal loop on this timeline' - genuinely re-enters Context 1a/2 rather than a special renewal path. OPEN QUESTIONS (unresolved): does the new submission carry forward exposure/PML context from the expiring policy automatically, given Context 1c/1d/1e's exploratory status - flagged for later, but the linkage should exist even before those contexts are built. What happens if renewal is initiated but the broker doesn't respond before original expiry - lapse vs. grace/held-covered period is a real contractual question for underwriting stakeholders, not an architecture default., When InitiateRenewal, Then PolicyRenewalInitiated

### US-5: PolicyBound

**As a** a consumer of this read-side projection
**I want to** PolicyRegister to reflect PolicyBound
**So that** downstream queries/decisions see current state

_Narrative (verbatim from the board export)_: S3.1: the canonical current-state view of every bound policy.

**Acceptance Criteria:**
- AC-5.1: Given Second instance of the same event type, paired here with the register projection it feeds., When PolicyBound is appended, Then PolicyRegister reflects it

### US-6: PolicyBound

**As a** a consumer of this read-side projection
**I want to** ActiveBookOfBusiness to reflect PolicyBound
**So that** downstream queries/decisions see current state

_Narrative (verbatim from the board export)_: S3.1: per-cell view of currently active bound business.

**Acceptance Criteria:**
- AC-6.1: Given Third instance of the same event type, paired here with the per-cell book-of-business projection it feeds., When PolicyBound is appended, Then ActiveBookOfBusiness reflects it

### US-7: EndorsementReferred

**As a** the system
**I want to** to record EndorsementReferred
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: S3.3: fires instead of PolicyEndorsed when a material change (premium/limit/exposure) exceeds the underwriter's authority - reuses Context 2's DecideReferral/ReferralApproved/ReferralDeclined resolution path rather than inventing a parallel one. Concrete example of the event model paying off architecturally: three contexts (Decisioning, Binding, and Context 0/1e's rule-change reassessment) now share one escalation mechanism instead of three bespoke ones.

**Acceptance Criteria:**
- AC-7.1: Given S3.3: fires instead of PolicyEndorsed when a material change (premium/limit/exposure) exceeds the underwriter's authority - reuses Context 2's DecideReferral/ReferralApproved/ReferralDeclined resolution path rather than inventing a parallel one. Concrete example of the event model paying off architecturally: three contexts (Decisioning, Binding, and Context 0/1e's rule-change reassessment) now share one escalation mechanism instead of three bespoke ones., When the triggering condition occurs, Then EndorsementReferred

### US-8: PolicyCancelledForCause

**As a** the system
**I want to** to record PolicyCancelledForCause
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: S3.4: distinct from routine PolicyCancelled given the compliance/dispute implications of a for-cause cancellation (e.g. material misrepresentation discovered post-bind) - not just a different reason code on the same event.

**Acceptance Criteria:**
- AC-8.1: Given S3.4: distinct from routine PolicyCancelled given the compliance/dispute implications of a for-cause cancellation (e.g. material misrepresentation discovered post-bind) - not just a different reason code on the same event., When the triggering condition occurs, Then PolicyCancelledForCause

## Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| FR-1 | System MUST support BindPolicy, producing the PolicyBound domain event(s) | Must | AC-1.1 |
| FR-2 | System MUST support EndorsePolicy, producing the PolicyEndorsed domain event(s) | Must | AC-2.1 |
| FR-3 | System MUST support CancelPolicy, producing the PolicyCancelled domain event(s) | Must | AC-3.1 |
| FR-4 | System MUST support InitiateRenewal, producing the PolicyRenewalInitiated domain event(s) | Must | AC-4.1 |
| FR-5 | System SHOULD project PolicyRegister from the PolicyBound domain event(s) | Should | AC-5.1 |
| FR-6 | System SHOULD project ActiveBookOfBusiness from the PolicyBound domain event(s) | Should | AC-6.1 |
| FR-7 | System COULD record the EndorsementReferred domain event(s) under the condition described in US-7's narrative | Could | AC-7.1 |
| FR-8 | System COULD record the PolicyCancelledForCause domain event(s) under the condition described in US-8's narrative | Could | AC-8.1 |

## Non-Functional Requirements

<!-- The source board does not specify numeric NFR targets for this context — every row is N/A, not invented, per constitution Principle III. Revisit via a clarification pass before /ralph-specum:design if any of these genuinely matter for this feature. -->

| ID | Requirement | Metric | Target |
|----|-------------|--------|--------|
| NFR-1 | Performance | N/A | N/A: not specified by board export |
| NFR-2 | Reliability | N/A | N/A: not specified by board export |
| NFR-3 | Security | N/A | N/A: not specified by board export |

## Glossary

- **Policy**: Aggregate in the Binding context (see Event Model Detail for the elements that touch it).
- **PolicyRegister**: Read model. S3.1: the canonical current-state view of every bound policy.
- **ActiveBookOfBusiness**: Read model. S3.1: per-cell view of currently active bound business.

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

- `EndorsementReferred` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review
- `PolicyCancelledForCause` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export — every field's type, cardinality, and flags. This is the lossless source; the User Stories above are a readable summary of it, not the other way around. Screens are deliberately excluded here — see this spec's `research.md` (UI Reference section), which the design phase reads directly; screens are UI reference, not a requirement.

### Slice: PolicyBound (`f2fbe251-f913-4514-80aa-d17e4003f08d`, status: Created, type: STATE_CHANGE)

**BindPolicy** (command, id `c67d6818-1efb-4b5a-8291-adc0497e11b9`, aggregate `Policy`, lane `Interaction`, modelContext `Binding`)

> S3.1/S3.2: requires explicit underwriter confirmation, not fired automatically on QuoteAccepted - broker acceptance and the underwriter's final bind action are conceptually different moments (documentation checks, subjectivities being cleared).

Dependencies: → PolicyBound (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `quoteId` | UUID | Single | — |
| `submissionId` | UUID | Single | — |
| `finalTerms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `premium` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `effectiveDate` | Date | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `expiryDate` | Date | Single | — |
| `capacityProviderAllocation` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `providerId` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `quotaSharePercent` | Decimal | Single | — |

**PolicyBound** (event, id `0111a7c6-81c5-4209-a3c2-3940cecdf72a`, aggregate `Policy`, lane `Policy`, modelContext `Binding`)

> S3.1/S3.2: the immutable anchor for the policy's lifecycle - once fired, endorsements/cancellations append to history rather than rewriting this record. Everything downstream (Bordereaux in Context 4, Claims in Context 5) keys off it.

Dependencies: ← BindPolicy (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | id, generated |
| `submissionId` | UUID | Single | — |
| `quoteId` | UUID | Single | — |
| `cellId` | String | Single | — |
| `classOfBusiness` | String | Single | — |
| `territory` | String | Single | — |
| `namedInsured` | String | Single | — |
| `finalTerms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `premium` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `effectiveDate` | Date | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `expiryDate` | Date | Single | — |
| `capacityProviderAllocation` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `providerId` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `quotaSharePercent` | Decimal | Single | — |
| `providerAuthorityLineage` | Custom | List | optional |
| `boundBy` | String | Single | — |
| `boundAt` | DateTime | Single | generated |

### Slice: PolicyEndorsed (`ef705d65-fc60-4f87-acba-b2ce94de45be`, status: Created, type: STATE_CHANGE)

**EndorsePolicy** (command, id `404a37a0-cb38-4314-af28-c04385090092`, aggregate `Policy`, lane `Interaction`, modelContext `Binding`)

> S3.3: broker-initiated change request, processed by the underwriter.

Dependencies: → PolicyEndorsed (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | — |
| `underwriterId` | String | Single | — |
| `requestedChange` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `description` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `deltaType` | String | Single | — |

**PolicyEndorsed** (event, id `3d05c53e-a0f5-4118-a5b4-a2ed314d131d`, aggregate `Policy`, lane `Policy`, modelContext `Binding`)

> S3.3: fires for administrative changes directly. Material changes (premium/limit/exposure) instead produce EndorsementReferred if they exceed the underwriter's authority - resolves the open thread from S1c.3: an endorsement that increases exposure now goes through the same authority/referral check as an original bind, reusing Context 2's existing mechanism rather than a lightweight edit path that quietly bypasses governance.

Dependencies: ← EndorsePolicy (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | id |
| `endorsementId` | UUID | Single | generated |
| `changeDelta` | Custom | Single | — |
| `materialityClassification` | String | Single | — |
| `effectiveDate` | Date | Single | — |
| `endorsedBy` | String | Single | — |
| `endorsedAt` | DateTime | Single | generated |

### Slice: PolicyCancelled (`60181785-1bc3-4140-b27e-46d2cf09edf0`, status: Created, type: STATE_CHANGE)

**CancelPolicy** (command, id `72015568-3a12-4387-97a1-523deb543818`, aggregate `Policy`, lane `Interaction`, modelContext `Binding`)

> S3.4

Dependencies: → PolicyCancelled (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | — |
| `cancellationReason` | String | Single | — |
| `effectiveDate` | Date | Single | — |
| `initiatedBy` | String | Single | — |

**PolicyCancelled** (event, id `ba10d1f5-1812-41c5-a1eb-9c974bd86492`, aggregate `Policy`, lane `Policy`, modelContext `Binding`)

> S3.4: routine broker-requested or underwriter-initiated cancellation. Triggers the exposure reduction already modeled in S1c.4 - return premium calculation itself feeds Bordereaux/Settlement (Context 4), not computed here. For-cause cancellations use the separate PolicyCancelledForCause event instead, given the compliance/dispute implications of material misrepresentation.

Dependencies: ← CancelPolicy (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | id |
| `reason` | String | Single | — |
| `effectiveDate` | Date | Single | — |
| `initiatedBy` | String | Single | — |
| `returnPremiumBasis` | String | Single | — |
| `cancelledAt` | DateTime | Single | generated |

### Slice: PolicyRenewalInitiated (`66b5af58-5973-4f0a-a19c-060b90510525`, status: Created, type: STATE_CHANGE)

**InitiateRenewal** (command, id `5e56127f-2d16-4c56-9e55-d29472a79aa5`, aggregate `Policy`, lane `Interaction`, modelContext `Binding`)

> S3.5: broker-initiated or system-prompted ahead of expiry. Renamed from RenewPolicy for clarity that this only initiates the loop, it doesn't itself bind anything.

Dependencies: → PolicyRenewalInitiated (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `expiringPolicyId` | UUID | Single | — |
| `proposedRenewalTerms` | Custom | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `premium` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `effectiveDate` | Date | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `expiryDate` | Date | Single | — |

**PolicyRenewalInitiated** (event, id `0e5e2851-8bb6-42f5-b225-57953d4450d0`, aggregate `Policy`, lane `Policy`, modelContext `Binding`)

> S3.5: 'loops back into a fresh submission/bind cycle, not shown as a literal loop on this timeline' - genuinely re-enters Context 1a/2 rather than a special renewal path. OPEN QUESTIONS (unresolved): does the new submission carry forward exposure/PML context from the expiring policy automatically, given Context 1c/1d/1e's exploratory status - flagged for later, but the linkage should exist even before those contexts are built. What happens if renewal is initiated but the broker doesn't respond before original expiry - lapse vs. grace/held-covered period is a real contractual question for underwriting stakeholders, not an architecture default.

Dependencies: ← InitiateRenewal (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `expiringPolicyId` | UUID | Single | id |
| `newSubmissionId` | UUID | Single | generated |
| `proposedRenewalTerms` | Custom | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `premium` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `effectiveDate` | Date | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `expiryDate` | Date | Single | — |
| `unconditionalReassessment` | Boolean | Single | generated |
| `initiatedAt` | DateTime | Single | generated |

### Slice: PolicyBound (`cb9a918d-aecd-4be9-a372-107891330a7d`, status: Created, type: STATE_VIEW)

**PolicyBound** (event, id `78f64ca6-f0fa-4331-8a00-4e532b911a5e`, aggregate `Policy`, lane `Policy`, modelContext `Binding`)

> Second instance of the same event type, paired here with the register projection it feeds.

Dependencies: → PolicyRegister (READMODEL); → ActiveBookOfBusiness (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | id, generated |

**PolicyRegister** (read model, id `b2cc3ae4-cfae-40ee-bd28-483fd83837bc`, aggregate `Policy`, lane `Interaction`, modelContext `Binding`)

> S3.1: the canonical current-state view of every bound policy.

Dependencies: ← PolicyBound (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | id |
| `cellId` | String | Single | — |
| `classOfBusiness` | String | Single | — |
| `namedInsured` | String | Single | — |
| `currentTerms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `premium` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `effectiveDate` | Date | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `expiryDate` | Date | Single | — |
| `capacityProviderAllocation` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `providerId` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `quotaSharePercent` | Decimal | Single | — |
| `status` | String | Single | — |
| `endorsementCount` | Integer | Single | — |

### Slice: PolicyBound (`186ce08a-2b3b-4edd-960e-69cec62407fb`, status: Created, type: STATE_VIEW)

**PolicyBound** (event, id `0b33eb30-3069-4b58-8c71-cdf1b950b736`, aggregate `Policy`, lane `Policy`, modelContext `Binding`)

> Third instance of the same event type, paired here with the per-cell book-of-business projection it feeds.

Dependencies: → ActiveBookOfBusiness (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | id, generated |

**ActiveBookOfBusiness** (read model, id `ea8fc289-52e6-4954-9cb9-5a3f350121af`, aggregate `Policy`, lane `Interaction`, modelContext `Binding`)

> S3.1: per-cell view of currently active bound business.

Dependencies: ← PolicyBound (EVENT); ← PolicyBound (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `cellId` | String | Single | id |
| `policyCount` | Integer | Single | — |
| `totalPremium` | Decimal | Single | — |
| `totalLineSize` | Decimal | Single | — |
| `policies` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `policyId` | UUID | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `namedInsured` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `premium` | Decimal | Single | — |

### Slice: EndorsementReferred (`497a416d-7ab6-4304-a8dd-985dfa701bec`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**EndorsementReferred** (event, id `d8b7396e-a43c-4853-9afa-1b5cde341b7a`, aggregate `Policy`, lane `Policy`, modelContext `Binding`)

> S3.3: fires instead of PolicyEndorsed when a material change (premium/limit/exposure) exceeds the underwriter's authority - reuses Context 2's DecideReferral/ReferralApproved/ReferralDeclined resolution path rather than inventing a parallel one. Concrete example of the event model paying off architecturally: three contexts (Decisioning, Binding, and Context 0/1e's rule-change reassessment) now share one escalation mechanism instead of three bespoke ones.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `endorsementId` | UUID | Single | — |
| `policyId` | UUID | Single | — |
| `underwriterId` | String | Single | — |
| `changeDelta` | Custom | Single | — |
| `breachedDimension` | String | Single | — |
| `referredTo` | String | Single | — |
| `referredAt` | DateTime | Single | generated |

### Slice: PolicyCancelledForCause (`71d7df21-13fa-4146-86e3-fc0eddab11ff`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**PolicyCancelledForCause** (event, id `609f4600-30e4-4fb4-a457-dbbde0f544d0`, aggregate `Policy`, lane `Policy`, modelContext `Binding`)

> S3.4: distinct from routine PolicyCancelled given the compliance/dispute implications of a for-cause cancellation (e.g. material misrepresentation discovered post-bind) - not just a different reason code on the same event.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | id |
| `cause` | String | Single | — |
| `evidenceReference` | String | Single | optional |
| `effectiveDate` | Date | Single | — |
| `initiatedBy` | String | Single | — |
| `cancelledAt` | DateTime | Single | generated |

