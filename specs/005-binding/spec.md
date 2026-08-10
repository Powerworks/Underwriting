# Feature Specification: Binding

**Feature Branch**: `005-binding`

**Created**: 2026-08-10

**Status**: Draft

**Input**: Derived from the "BrokerConnect" eventmodelers.ai board (`80f53178-c291-43a0-8aa5-bc723990c5db`), chapter **Broker Connect**, context **Binding** — pulled live via `GET .../slicedata?contextName=...` on 2026-08-10 and cached at `event-model/slices/*.json` / `event-model/import-config.json` (Phase A / MVP scope only — see `Project Plan/01-project-plan.md` §4; the 4 exploratory contexts are not yet pulled). Supersedes an earlier partial export now kept at `event-model/archive/` for provenance — see `event-model-to-speckit-guide.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - PolicyBound (Priority: P1)

As **BindPolicy**, I want to policyBound so that **PolicyBound** is recorded.

**Narrative** (verbatim from the board export): S3.1/S3.2: requires explicit underwriter confirmation, not fired automatically on QuoteAccepted - broker acceptance and the underwriter's final bind action are conceptually different moments (documentation checks, subjectivities being cleared).

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **BindPolicy** under the right preconditions and asserting that **PolicyBound** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S3.1/S3.2: the immutable anchor for the policy's lifecycle - once fired, endorsements/cancellations append to history rather than rewriting this record. Everything downstream (Bordereaux in Context 4, Claims in Context 5) keys off it., **When** BindPolicy, **Then** PolicyBound

---

### User Story 2 - PolicyEndorsed (Priority: P1)

As **EndorsePolicy**, I want to policyEndorsed so that **PolicyEndorsed** is recorded.

**Narrative** (verbatim from the board export): S3.3: broker-initiated change request, processed by the underwriter.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **EndorsePolicy** under the right preconditions and asserting that **PolicyEndorsed** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S3.3: fires for administrative changes directly. Material changes (premium/limit/exposure) instead produce EndorsementReferred if they exceed the underwriter's authority - resolves the open thread from S1c.3: an endorsement that increases exposure now goes through the same authority/referral check as an original bind, reusing Context 2's existing mechanism rather than a lightweight edit path that quietly bypasses governance., **When** EndorsePolicy, **Then** PolicyEndorsed

---

### User Story 3 - PolicyCancelled (Priority: P1)

As **CancelPolicy**, I want to policyCancelled so that **PolicyCancelled** is recorded.

**Narrative** (verbatim from the board export): S3.4

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **CancelPolicy** under the right preconditions and asserting that **PolicyCancelled** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S3.4: routine broker-requested or underwriter-initiated cancellation. Triggers the exposure reduction already modeled in S1c.4 - return premium calculation itself feeds Bordereaux/Settlement (Context 4), not computed here. For-cause cancellations use the separate PolicyCancelledForCause event instead, given the compliance/dispute implications of material misrepresentation., **When** CancelPolicy, **Then** PolicyCancelled

---

### User Story 4 - PolicyRenewalInitiated (Priority: P1)

As **InitiateRenewal**, I want to policyRenewalInitiated so that **PolicyRenewalInitiated** is recorded.

**Narrative** (verbatim from the board export): S3.5: broker-initiated or system-prompted ahead of expiry. Renamed from RenewPolicy for clarity that this only initiates the loop, it doesn't itself bind anything.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **InitiateRenewal** under the right preconditions and asserting that **PolicyRenewalInitiated** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S3.5: 'loops back into a fresh submission/bind cycle, not shown as a literal loop on this timeline' - genuinely re-enters Context 1a/2 rather than a special renewal path. OPEN QUESTIONS (unresolved): does the new submission carry forward exposure/PML context from the expiring policy automatically, given Context 1c/1d/1e's exploratory status - flagged for later, but the linkage should exist even before those contexts are built. What happens if renewal is initiated but the broker doesn't respond before original expiry - lapse vs. grace/held-covered period is a real contractual question for underwriting stakeholders, not an architecture default., **When** InitiateRenewal, **Then** PolicyRenewalInitiated

---

### User Story 5 - PolicyBound (Priority: P2)

The system maintains **PolicyRegister**, projected from **PolicyBound**.

**Narrative** (verbatim from the board export): S3.1: the canonical current-state view of every bound policy.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **PolicyBound** and asserting that **PolicyRegister** reflects the update.

**Acceptance Scenarios**:

1. **Given** Second instance of the same event type, paired here with the register projection it feeds., **When** PolicyBound is appended, **Then** **PolicyRegister** reflects it

---

### User Story 6 - PolicyBound (Priority: P2)

The system maintains **ActiveBookOfBusiness**, projected from **PolicyBound**.

**Narrative** (verbatim from the board export): S3.1: per-cell view of currently active bound business.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **PolicyBound** and asserting that **ActiveBookOfBusiness** reflects the update.

**Acceptance Scenarios**:

1. **Given** Third instance of the same event type, paired here with the per-cell book-of-business projection it feeds., **When** PolicyBound is appended, **Then** **ActiveBookOfBusiness** reflects it

---

### User Story 7 - EndorsementReferred (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **EndorsementReferred** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): S3.3: fires instead of PolicyEndorsed when a material change (premium/limit/exposure) exceeds the underwriter's authority - reuses Context 2's DecideReferral/ReferralApproved/ReferralDeclined resolution path rather than inventing a parallel one. Concrete example of the event model paying off architecturally: three contexts (Decisioning, Binding, and Context 0/1e's rule-change reassessment) now share one escalation mechanism instead of three bespoke ones.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **EndorsementReferred** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S3.3: fires instead of PolicyEndorsed when a material change (premium/limit/exposure) exceeds the underwriter's authority - reuses Context 2's DecideReferral/ReferralApproved/ReferralDeclined resolution path rather than inventing a parallel one. Concrete example of the event model paying off architecturally: three contexts (Decisioning, Binding, and Context 0/1e's rule-change reassessment) now share one escalation mechanism instead of three bespoke ones., **When** the triggering condition occurs, **Then** EndorsementReferred

---

### User Story 8 - PolicyCancelledForCause (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **PolicyCancelledForCause** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): S3.4: distinct from routine PolicyCancelled given the compliance/dispute implications of a for-cause cancellation (e.g. material misrepresentation discovered post-bind) - not just a different reason code on the same event.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **PolicyCancelledForCause** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S3.4: distinct from routine PolicyCancelled given the compliance/dispute implications of a for-cause cancellation (e.g. material misrepresentation discovered post-bind) - not just a different reason code on the same event., **When** the triggering condition occurs, **Then** PolicyCancelledForCause

---

### Edge Cases

- What happens under the condition described for **EndorsementReferred**? System records it as a standalone fact — S3.3: fires instead of PolicyEndorsed when a material change (premium/limit/exposure) exceeds the underwriter's authority - reuses Context 2's DecideReferral/ReferralApproved/ReferralDeclined resolution path rather than inventing a parallel one. Concrete example of the event model paying off architecturally: three contexts (Decisioning, Binding, and Context 0/1e's rule-change reassessment) now share one escalation mechanism instead of three bespoke ones. (no command/processor of its own in the source export).
- What happens under the condition described for **PolicyCancelledForCause**? System records it as a standalone fact — S3.4: distinct from routine PolicyCancelled given the compliance/dispute implications of a for-cause cancellation (e.g. material misrepresentation discovered post-bind) - not just a different reason code on the same event. (no command/processor of its own in the source export).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support **BindPolicy**, producing the **PolicyBound** domain event(s).
- **FR-002**: System MUST support **EndorsePolicy**, producing the **PolicyEndorsed** domain event(s).
- **FR-003**: System MUST support **CancelPolicy**, producing the **PolicyCancelled** domain event(s).
- **FR-004**: System MUST support **InitiateRenewal**, producing the **PolicyRenewalInitiated** domain event(s).
- **FR-005**: System MUST project **PolicyRegister** from the **PolicyBound** domain event(s).
- **FR-006**: System MUST project **ActiveBookOfBusiness** from the **PolicyBound** domain event(s).
- **FR-007**: System MUST record the **EndorsementReferred** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-008**: System MUST record the **PolicyCancelledForCause** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).

### Key Entities *(include if feature involves data)*

- **Policy**: aggregate in the **Binding** context; touched by **BindPolicy**, **CancelPolicy**, **EndorsePolicy**, **EndorsementReferred**, **InitiateRenewal**, **PolicyBound**, **PolicyCancelled**, **PolicyCancelledForCause**, **PolicyEndorsed**, **PolicyRenewalInitiated**.
- **PolicyRegister** (read model): S3.1: the canonical current-state view of every bound policy. Fields: `policyId`, `cellId`, `classOfBusiness`, `namedInsured`, `currentTerms`, `capacityProviderAllocation`, `status`, `endorsementCount`.
- **ActiveBookOfBusiness** (read model): S3.1: per-cell view of currently active bound business. Fields: `cellId`, `policyCount`, `totalPremium`, `totalLineSize`, `policies`.

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

- Derived from the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Binding**); command/event names, field lists, descriptions, aggregates, and lanes in the Event Model Detail section below are transcribed directly from a live pull of that board, not invented for this document.
- Pulled live on 2026-08-10 via the `slicedata` endpoint (see `event-model-to-speckit-guide.md`, "The real export endpoint"), not the raw event-replay log — re-run `event-model/build-scripts/gen_specs_from_slices.py` after any further board edits to keep this in sync; nothing here watches the board automatically.
- All 8 slice(s) in this context currently carry board status `Created` — none are `Planned` or built yet.
- The source board did not specify performance, scale, or business-metric assumptions for this context — the Success Criteria above are template placeholders.
- [Assumption about scope boundaries — confirm before /speckit.plan]

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export, including every field's type, cardinality, and flags (`id` = identifier field, `generated` = system-generated, `optional` = nullable). Nested `List`/object fields show their subfields indented beneath them with `↳`.

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

