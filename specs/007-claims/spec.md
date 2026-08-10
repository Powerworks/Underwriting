# Feature Specification: Claims

**Feature Branch**: `007-claims`

**Created**: 2026-08-10

**Status**: Draft

**Input**: Derived from the "BrokerConnect" eventmodelers.ai board (`80f53178-c291-43a0-8aa5-bc723990c5db`), chapter **Broker Connect**, context **Claims** — pulled live via `GET .../slicedata?contextName=...` on 2026-08-10 and cached at `event-model/slices/*.json` / `event-model/import-config.json` (Phase A / MVP scope only — see `Project Plan/01-project-plan.md` §4; the 4 exploratory contexts are not yet pulled). Supersedes an earlier partial export now kept at `event-model/archive/` for provenance — see `event-model-to-speckit-guide.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - ClaimNotified (Priority: P1)

As **NotifyClaim**, I want to claimNotified so that **ClaimNotified** is recorded.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **NotifyClaim** under the right preconditions and asserting that **ClaimNotified** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S5.1: notification may originate from broker/insured, but processing is the claims team's. References the original bind (and transitively the whole submission/decisioning lineage) via policyReference - a claims handler pulling up a claim gets the full PolicyOriginationView designed back in Search & Retrieval (1b.2)., **When** NotifyClaim, **Then** ClaimNotified

---

### User Story 2 - ClaimReserveSet (Priority: P1)

As **SetClaimReserve**, I want to claimReserveSet so that **ClaimReserveSet** is recorded.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **SetClaimReserve** under the right preconditions and asserting that **ClaimReserveSet** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S5.2: explicitly recurring - fires again whenever new information changes the loss estimate. A material increase crossing the claims handler's own authority threshold instead produces ReserveRevisionReferred - the authority/governance pattern from Context 0 isn't scoped only to underwriting decisioning, it applies here too., **When** SetClaimReserve, **Then** ClaimReserveSet

---

### User Story 3 - ClaimPaid (Priority: P1)

As **PayClaim**, I want to claimPaid so that **ClaimPaid** is recorded.

**Narrative** (verbatim from the board export): S5.1: validates the payment against the policy's limits/sub-limits/deductibles from the original bind terms - an explicit, visible step referencing PolicyOriginationView/bind terms directly, not just trusted to the claims handler's manual check. This is precisely the kind of governance point this system exists to make auditable rather than assumed.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **PayClaim** under the right preconditions and asserting that **ClaimPaid** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S5.3: allocation mirrors the bind-time quota share automatically. OPEN QUESTION (unresolved, parallel to S0.1's per-provider question): could quota share change post-bind (novation, reinsurance restructure)? If so, this needs to reference whichever allocation was in effect at the relevant point, not blindly inherit the original split - would need point-in-time versioning like Context 0's authority model, not a simple lookup. Not resolved here., **When** PayClaim, **Then** ClaimPaid

---

### User Story 4 - ClaimClosed (Priority: P1)

As **CloseClaim**, I want to claimClosed so that **ClaimClosed** is recorded.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **CloseClaim** under the right preconditions and asserting that **ClaimClosed** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** the preconditions for this step are met, **When** CloseClaim, **Then** ClaimClosed

---

### User Story 5 - ClaimReopened (Priority: P1)

As **ReopenClaim**, I want to claimReopened so that **ClaimReopened** is recorded.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **ReopenClaim** under the right preconditions and asserting that **ClaimReopened** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S5.4: appended after ClaimClosed, doesn't erase it - the same append-only 'corrections are new entries, not edits' principle used for Bordereaux (AdjustmentLineRaised) and Binding (PolicyEndorsed) applies here too, one architectural principle consistently applied rather than three separate decisions. Typically followed by a fresh ClaimReserveSet cycle. Reopen-limit/repeated-reopening-as-a-governance-signal not modeled yet - flagged as a natural extension, not essential day one., **When** ReopenClaim, **Then** ClaimReopened

---

### User Story 6 - ClaimNotified (Priority: P2)

The system maintains **ClaimRegister**, projected from **ClaimNotified**.

**Narrative** (verbatim from the board export): S5.1: the claim's permanent record, canonical current state.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **ClaimNotified** and asserting that **ClaimRegister** reflects the update.

**Acceptance Scenarios**:

1. **Given** Second instance of the same event type, paired here with the register projection it feeds., **When** ClaimNotified is appended, **Then** **ClaimRegister** reflects it

---

### User Story 7 - ReserveRevisionReferred (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **ReserveRevisionReferred** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): S5.2: a material reserve increase crossing the claims handler's own authority threshold - genuine parallel to S3.3's endorsement/referral resolution. Suggests Context 0's authority engine is a cross-cutting capability (decisioning, endorsements, AND claims reserve authority), not something scoped only to Context 2 - worth reframing Context 0 as a general authority/governance engine rather than 'underwriting authority' specifically. Reuses the same DecideReferral resolution path as Context 2/Context 3's EndorsementReferred.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **ReserveRevisionReferred** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S5.2: a material reserve increase crossing the claims handler's own authority threshold - genuine parallel to S3.3's endorsement/referral resolution. Suggests Context 0's authority engine is a cross-cutting capability (decisioning, endorsements, AND claims reserve authority), not something scoped only to Context 2 - worth reframing Context 0 as a general authority/governance engine rather than 'underwriting authority' specifically. Reuses the same DecideReferral resolution path as Context 2/Context 3's EndorsementReferred., **When** the triggering condition occurs, **Then** ReserveRevisionReferred

---

### User Story 8 - ClaimNotifiedAgainstInactivePolicy (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **ClaimNotifiedAgainstInactivePolicy** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): S5.5: fires when NotifyClaim references a policy whose current PolicyRegister state shows Cancelled or lapsed/non-renewed. Does NOT reject the notification - the loss may have occurred while cover was active. A flag, not a block.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **ClaimNotifiedAgainstInactivePolicy** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S5.5: fires when NotifyClaim references a policy whose current PolicyRegister state shows Cancelled or lapsed/non-renewed. Does NOT reject the notification - the loss may have occurred while cover was active. A flag, not a block., **When** the triggering condition occurs, **Then** ClaimNotifiedAgainstInactivePolicy

---

### User Story 9 - ClaimPeriodValidated (Priority: P1)

As **ValidateClaimPeriod**, I want to claimPeriodValidated so that **ClaimPeriodValidated** is recorded.

**Narrative** (verbatim from the board export): Explicit human confirmation, not an automated pass/fail - date-of-loss-vs-active-period disputes (was the policy actually in force at the exact moment of loss, retroactive date issues) are exactly the kind of determination that ends up in coverage disputes and shouldn't be silently auto-decided.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **ValidateClaimPeriod** under the right preconditions and asserting that **ClaimPeriodValidated** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S5.5: only required when ClaimNotifiedAgainstInactivePolicy has fired - a claims handler manually confirms the loss falls within the period on cover before reserve/payment proceeds., **When** ValidateClaimPeriod, **Then** ClaimPeriodValidated

---

### Edge Cases

- What happens under the condition described for **ReserveRevisionReferred**? System records it as a standalone fact — S5.2: a material reserve increase crossing the claims handler's own authority threshold - genuine parallel to S3.3's endorsement/referral resolution. Suggests Context 0's authority engine is a cross-cutting capability (decisioning, endorsements, AND claims reserve authority), not something scoped only to Context 2 - worth reframing Context 0 as a general authority/governance engine rather than 'underwriting authority' specifically. Reuses the same DecideReferral resolution path as Context 2/Context 3's EndorsementReferred. (no command/processor of its own in the source export).
- What happens under the condition described for **ClaimNotifiedAgainstInactivePolicy**? System records it as a standalone fact — S5.5: fires when NotifyClaim references a policy whose current PolicyRegister state shows Cancelled or lapsed/non-renewed. Does NOT reject the notification - the loss may have occurred while cover was active. A flag, not a block. (no command/processor of its own in the source export).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support **NotifyClaim**, producing the **ClaimNotified** domain event(s).
- **FR-002**: System MUST support **SetClaimReserve**, producing the **ClaimReserveSet** domain event(s).
- **FR-003**: System MUST support **PayClaim**, producing the **ClaimPaid** domain event(s).
- **FR-004**: System MUST support **CloseClaim**, producing the **ClaimClosed** domain event(s).
- **FR-005**: System MUST support **ReopenClaim**, producing the **ClaimReopened** domain event(s).
- **FR-006**: System MUST project **ClaimRegister** from the **ClaimNotified** domain event(s).
- **FR-007**: System MUST record the **ReserveRevisionReferred** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-008**: System MUST record the **ClaimNotifiedAgainstInactivePolicy** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-009**: System MUST support **ValidateClaimPeriod**, producing the **ClaimPeriodValidated** domain event(s).

### Key Entities *(include if feature involves data)*

- **Claim**: aggregate in the **Claims** context; touched by **ClaimClosed**, **ClaimNotified**, **ClaimNotifiedAgainstInactivePolicy**, **ClaimPaid**, **ClaimPeriodValidated**, **ClaimReopened**, **ClaimReserveSet**, **CloseClaim**, **NotifyClaim**, **PayClaim**, **ReopenClaim**, **ReserveRevisionReferred**, **SetClaimReserve**, **ValidateClaimPeriod**.
- **ClaimRegister** (read model): S5.1: the claim's permanent record, canonical current state. Fields: `claimId`, `policyReference`, `status`, `currentReserve`, `totalPaid`, `dateOfLoss`, `dateNotified`, `reopenCount`.

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

- Derived from the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Claims**); command/event names, field lists, descriptions, aggregates, and lanes in the Event Model Detail section below are transcribed directly from a live pull of that board, not invented for this document.
- Pulled live on 2026-08-10 via the `slicedata` endpoint (see `event-model-to-speckit-guide.md`, "The real export endpoint"), not the raw event-replay log — re-run `event-model/build-scripts/gen_specs_from_slices.py` after any further board edits to keep this in sync; nothing here watches the board automatically.
- All 9 slice(s) in this context currently carry board status `Created` — none are `Planned` or built yet.
- The source board did not specify performance, scale, or business-metric assumptions for this context — the Success Criteria above are template placeholders.
- [Assumption about scope boundaries — confirm before /speckit.plan]

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export, including every field's type, cardinality, and flags (`id` = identifier field, `generated` = system-generated, `optional` = nullable). Nested `List`/object fields show their subfields indented beneath them with `↳`.

### Slice: ClaimNotified (`1327986f-6bc0-4386-bf21-4747fe5154ef`, status: Created, type: STATE_CHANGE)

**NotifyClaim** (command, id `76ea9ece-aef7-4ee7-bf9b-8ca29e5ebb85`, aggregate `Claim`, lane `Interaction`, modelContext `Claims`)

Dependencies: ← ClaimHandling (SCREEN); → ClaimNotified (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyReference` | UUID | Single | — |
| `lossDescription` | String | Single | — |
| `dateOfLoss` | Date | Single | — |
| `notifyingParty` | String | Single | — |

**ClaimNotified** (event, id `b68db8b1-058e-454e-856d-270ac90ec68a`, aggregate `Claim`, lane `Claim`, modelContext `Claims`)

> S5.1: notification may originate from broker/insured, but processing is the claims team's. References the original bind (and transitively the whole submission/decisioning lineage) via policyReference - a claims handler pulling up a claim gets the full PolicyOriginationView designed back in Search & Retrieval (1b.2).

Dependencies: ← NotifyClaim (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id, generated |
| `policyReference` | UUID | Single | — |
| `dateOfLoss` | Date | Single | — |
| `dateNotified` | DateTime | Single | generated |
| `notifyingParty` | String | Single | — |
| `initialLossDescription` | String | Single | — |

### Slice: ClaimReserveSet (`f8ecf1d4-bd90-45bf-8674-5f983b9ee4ea`, status: Created, type: STATE_CHANGE)

**SetClaimReserve** (command, id `0a6caaff-a90b-4e6f-aae3-e03f215d4a11`, aggregate `Claim`, lane `Interaction`, modelContext `Claims`)

Dependencies: → ClaimReserveSet (EVENT); ← ClaimHandling (SCREEN)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | — |
| `reserveAmount` | Decimal | Single | — |
| `basisRationale` | String | Single | — |

**ClaimReserveSet** (event, id `b02be6d2-6ec8-48e2-907b-10ac1f717f47`, aggregate `Claim`, lane `Claim`, modelContext `Claims`)

> S5.2: explicitly recurring - fires again whenever new information changes the loss estimate. A material increase crossing the claims handler's own authority threshold instead produces ReserveRevisionReferred - the authority/governance pattern from Context 0 isn't scoped only to underwriting decisioning, it applies here too.

Dependencies: ← SetClaimReserve (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id |
| `reserveAmount` | Decimal | Single | — |
| `setBy` | String | Single | — |
| `basis` | String | Single | — |
| `sequenceNumber` | Integer | Single | generated |
| `setAt` | DateTime | Single | generated |

### Slice: ClaimPaid (`97cb2b6c-0e12-40e1-987e-b094d45ae2aa`, status: Created, type: STATE_CHANGE)

**PayClaim** (command, id `c009bf8b-f568-4ee5-bd4e-0df97adca242`, aggregate `Claim`, lane `Interaction`, modelContext `Claims`)

> S5.1: validates the payment against the policy's limits/sub-limits/deductibles from the original bind terms - an explicit, visible step referencing PolicyOriginationView/bind terms directly, not just trusted to the claims handler's manual check. This is precisely the kind of governance point this system exists to make auditable rather than assumed.

Dependencies: → ClaimPaid (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | — |
| `paymentAmount` | Decimal | Single | — |
| `providerAllocation` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `providerId` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `amount` | Decimal | Single | — |

**ClaimPaid** (event, id `9c5b7228-2907-4514-82d0-6d51faa05734`, aggregate `Claim`, lane `Claim`, modelContext `Claims`)

> S5.3: allocation mirrors the bind-time quota share automatically. OPEN QUESTION (unresolved, parallel to S0.1's per-provider question): could quota share change post-bind (novation, reinsurance restructure)? If so, this needs to reference whichever allocation was in effect at the relevant point, not blindly inherit the original split - would need point-in-time versioning like Context 0's authority model, not a simple lookup. Not resolved here.

Dependencies: ← PayClaim (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id |
| `paymentAmount` | Decimal | Single | — |
| `providerAllocation` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `providerId` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `amount` | Decimal | Single | — |
| `paymentDate` | Date | Single | — |
| `paymentReference` | String | Single | — |
| `validatedAgainstBindTerms` | Boolean | Single | — |

### Slice: ClaimClosed (`b3609363-d20f-4a7d-90bb-524e11062c9c`, status: Created, type: STATE_CHANGE)

**CloseClaim** (command, id `ec894cc3-fd6d-437f-9e15-a8e1f5cafad5`, aggregate `Claim`, lane `Interaction`, modelContext `Claims`)

Dependencies: → ClaimClosed (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id |
| `closureReason` | String | Single | — |

**ClaimClosed** (event, id `f608b157-407e-4ac4-adea-5b09c9d909d7`, aggregate `Claim`, lane `Claim`, modelContext `Claims`)

Dependencies: ← CloseClaim (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id |
| `closureReason` | String | Single | — |
| `finalTotalPaid` | Decimal | Single | — |
| `closedBy` | String | Single | — |
| `closedAt` | DateTime | Single | generated |

### Slice: ClaimReopened (`ced882f8-5643-4cf4-b5bb-aaed3024d518`, status: Created, type: STATE_CHANGE)

**ReopenClaim** (command, id `afb40732-c3b4-42e4-a1f3-8d421611e250`, aggregate `Claim`, lane `Interaction`, modelContext `Claims`)

Dependencies: → ClaimReopened (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id |
| `reason` | String | Single | — |

**ClaimReopened** (event, id `a2480670-b8c5-4a49-ac2a-ae08feddac23`, aggregate `Claim`, lane `Claim`, modelContext `Claims`)

> S5.4: appended after ClaimClosed, doesn't erase it - the same append-only 'corrections are new entries, not edits' principle used for Bordereaux (AdjustmentLineRaised) and Binding (PolicyEndorsed) applies here too, one architectural principle consistently applied rather than three separate decisions. Typically followed by a fresh ClaimReserveSet cycle. Reopen-limit/repeated-reopening-as-a-governance-signal not modeled yet - flagged as a natural extension, not essential day one.

Dependencies: ← ReopenClaim (COMMAND); → ClaimRegister (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id |
| `reopenReason` | String | Single | — |
| `newInformation` | String | Single | — |
| `reopenedBy` | String | Single | — |
| `reopenedAt` | DateTime | Single | generated |

### Slice: ClaimNotified (`8aaa4dda-780b-43c6-9f15-ff32e8cda91c`, status: Created, type: STATE_VIEW)

**ClaimNotified** (event, id `4327c8a8-481f-4615-853f-7d7be24aa344`, aggregate `Claim`, lane `Claim`, modelContext `Claims`)

> Second instance of the same event type, paired here with the register projection it feeds.

Dependencies: → ClaimRegister (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id, generated |

**ClaimRegister** (read model, id `85d97ffb-0796-4c0a-86fa-095ea7f86c51`, aggregate `Claim`, lane `Interaction`, modelContext `Claims`)

> S5.1: the claim's permanent record, canonical current state.

Dependencies: ← ClaimNotified (EVENT); ← ClaimReopened (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id |
| `policyReference` | UUID | Single | — |
| `status` | String | Single | — |
| `currentReserve` | Decimal | Single | — |
| `totalPaid` | Decimal | Single | — |
| `dateOfLoss` | Date | Single | — |
| `dateNotified` | DateTime | Single | — |
| `reopenCount` | Integer | Single | — |

### Slice: ReserveRevisionReferred (`a04db400-0c7a-4be3-940c-fd3ce5914e33`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**ReserveRevisionReferred** (event, id `a97072d5-92e5-40a8-b2e6-e0ed31566c19`, aggregate `Claim`, lane `Claim`, modelContext `Claims`)

> S5.2: a material reserve increase crossing the claims handler's own authority threshold - genuine parallel to S3.3's endorsement/referral resolution. Suggests Context 0's authority engine is a cross-cutting capability (decisioning, endorsements, AND claims reserve authority), not something scoped only to Context 2 - worth reframing Context 0 as a general authority/governance engine rather than 'underwriting authority' specifically. Reuses the same DecideReferral resolution path as Context 2/Context 3's EndorsementReferred.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | — |
| `claimsHandlerId` | String | Single | — |
| `requestedReserveAmount` | Decimal | Single | — |
| `currentAuthorityLimit` | Decimal | Single | — |
| `referredTo` | String | Single | — |
| `referredAt` | DateTime | Single | generated |

### Slice: ClaimNotifiedAgainstInactivePolicy (`36714763-c499-4636-ba28-f6ea16e6b3bf`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**ClaimNotifiedAgainstInactivePolicy** (event, id `38da8315-a12b-44ee-9a07-356f0bda91ab`, aggregate `Claim`, lane `Claim`, modelContext `Claims`)

> S5.5: fires when NotifyClaim references a policy whose current PolicyRegister state shows Cancelled or lapsed/non-renewed. Does NOT reject the notification - the loss may have occurred while cover was active. A flag, not a block.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | — |
| `policyReference` | UUID | Single | — |
| `policyStatusAtNotification` | String | Single | — |
| `dateOfLoss` | Date | Single | — |
| `flaggedAt` | DateTime | Single | generated |

### Slice: ClaimPeriodValidated (`ea6063ed-fd19-48fd-b97b-c60b6f359b19`, status: Created, type: STATE_CHANGE)

**ValidateClaimPeriod** (command, id `ffc1e09b-133c-4471-9ab5-3f9386cc2451`, aggregate `Claim`, lane `Interaction`, modelContext `Claims`)

> Explicit human confirmation, not an automated pass/fail - date-of-loss-vs-active-period disputes (was the policy actually in force at the exact moment of loss, retroactive date issues) are exactly the kind of determination that ends up in coverage disputes and shouldn't be silently auto-decided.

Dependencies: → ClaimPeriodValidated (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | — |
| `determination` | String | Single | — |

**ClaimPeriodValidated** (event, id `243f77f6-84f3-43b3-b93a-3d7b8782149a`, aggregate `Claim`, lane `Claim`, modelContext `Claims`)

> S5.5: only required when ClaimNotifiedAgainstInactivePolicy has fired - a claims handler manually confirms the loss falls within the period on cover before reserve/payment proceeds.

Dependencies: ← ValidateClaimPeriod (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `claimId` | UUID | Single | id |
| `determination` | String | Single | — |
| `validatedBy` | String | Single | — |
| `validatedAt` | DateTime | Single | generated |

