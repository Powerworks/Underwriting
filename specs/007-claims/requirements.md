---
spec: 007-claims
phase: requirements
created: 2026-08-10
---

# Requirements: Claims

## Problem Statement

Claims is one of the bounded contexts modeled on the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Claims**). Evidence: the board's own live export (`event-model/import-config.json`), transcribed verbatim in the Event Model Detail appendix below — this is not a hypothesis needing validation, the domain already exists as a modeled event model.

## Goal

Implement the **Claims** bounded context's commands, events, automations, and read models exactly as modeled on the board, per constitution Principle III (the spec is the source of truth — no invented fields, no guessed names).

## User Stories

### US-1: ClaimNotified

**As a** NotifyClaim
**I want to** claimNotified
**So that** ClaimNotified is recorded

**Acceptance Criteria:**
- AC-1.1: Given S5.1: notification may originate from broker/insured, but processing is the claims team's. References the original bind (and transitively the whole submission/decisioning lineage) via policyReference - a claims handler pulling up a claim gets the full PolicyOriginationView designed back in Search & Retrieval (1b.2)., When NotifyClaim, Then ClaimNotified

### US-2: ClaimReserveSet

**As a** SetClaimReserve
**I want to** claimReserveSet
**So that** ClaimReserveSet is recorded

**Acceptance Criteria:**
- AC-2.1: Given S5.2: explicitly recurring - fires again whenever new information changes the loss estimate. A material increase crossing the claims handler's own authority threshold instead produces ReserveRevisionReferred - the authority/governance pattern from Context 0 isn't scoped only to underwriting decisioning, it applies here too., When SetClaimReserve, Then ClaimReserveSet

### US-3: ClaimPaid

**As a** PayClaim
**I want to** claimPaid
**So that** ClaimPaid is recorded

_Narrative (verbatim from the board export)_: S5.1: validates the payment against the policy's limits/sub-limits/deductibles from the original bind terms - an explicit, visible step referencing PolicyOriginationView/bind terms directly, not just trusted to the claims handler's manual check. This is precisely the kind of governance point this system exists to make auditable rather than assumed.

**Acceptance Criteria:**
- AC-3.1: Given S5.3: allocation mirrors the bind-time quota share automatically. OPEN QUESTION (unresolved, parallel to S0.1's per-provider question): could quota share change post-bind (novation, reinsurance restructure)? If so, this needs to reference whichever allocation was in effect at the relevant point, not blindly inherit the original split - would need point-in-time versioning like Context 0's authority model, not a simple lookup. Not resolved here., When PayClaim, Then ClaimPaid

### US-4: ClaimClosed

**As a** CloseClaim
**I want to** claimClosed
**So that** ClaimClosed is recorded

**Acceptance Criteria:**
- AC-4.1: Given the preconditions for this step are met, When CloseClaim, Then ClaimClosed

### US-5: ClaimReopened

**As a** ReopenClaim
**I want to** claimReopened
**So that** ClaimReopened is recorded

**Acceptance Criteria:**
- AC-5.1: Given S5.4: appended after ClaimClosed, doesn't erase it - the same append-only 'corrections are new entries, not edits' principle used for Bordereaux (AdjustmentLineRaised) and Binding (PolicyEndorsed) applies here too, one architectural principle consistently applied rather than three separate decisions. Typically followed by a fresh ClaimReserveSet cycle. Reopen-limit/repeated-reopening-as-a-governance-signal not modeled yet - flagged as a natural extension, not essential day one., When ReopenClaim, Then ClaimReopened

### US-6: ClaimNotified

**As a** a consumer of this read-side projection
**I want to** ClaimRegister to reflect ClaimNotified
**So that** downstream queries/decisions see current state

_Narrative (verbatim from the board export)_: S5.1: the claim's permanent record, canonical current state.

**Acceptance Criteria:**
- AC-6.1: Given Second instance of the same event type, paired here with the register projection it feeds., When ClaimNotified is appended, Then ClaimRegister reflects it

### US-7: ReserveRevisionReferred

**As a** the system
**I want to** to record ReserveRevisionReferred
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: S5.2: a material reserve increase crossing the claims handler's own authority threshold - genuine parallel to S3.3's endorsement/referral resolution. Suggests Context 0's authority engine is a cross-cutting capability (decisioning, endorsements, AND claims reserve authority), not something scoped only to Context 2 - worth reframing Context 0 as a general authority/governance engine rather than 'underwriting authority' specifically. Reuses the same DecideReferral resolution path as Context 2/Context 3's EndorsementReferred.

**Acceptance Criteria:**
- AC-7.1: Given S5.2: a material reserve increase crossing the claims handler's own authority threshold - genuine parallel to S3.3's endorsement/referral resolution. Suggests Context 0's authority engine is a cross-cutting capability (decisioning, endorsements, AND claims reserve authority), not something scoped only to Context 2 - worth reframing Context 0 as a general authority/governance engine rather than 'underwriting authority' specifically. Reuses the same DecideReferral resolution path as Context 2/Context 3's EndorsementReferred., When the triggering condition occurs, Then ReserveRevisionReferred

### US-8: ClaimNotifiedAgainstInactivePolicy

**As a** the system
**I want to** to record ClaimNotifiedAgainstInactivePolicy
**So that** the alternate/terminal outcome is captured explicitly, not left implicit

_Narrative (verbatim from the board export)_: S5.5: fires when NotifyClaim references a policy whose current PolicyRegister state shows Cancelled or lapsed/non-renewed. Does NOT reject the notification - the loss may have occurred while cover was active. A flag, not a block.

**Acceptance Criteria:**
- AC-8.1: Given S5.5: fires when NotifyClaim references a policy whose current PolicyRegister state shows Cancelled or lapsed/non-renewed. Does NOT reject the notification - the loss may have occurred while cover was active. A flag, not a block., When the triggering condition occurs, Then ClaimNotifiedAgainstInactivePolicy

### US-9: ClaimPeriodValidated

**As a** ValidateClaimPeriod
**I want to** claimPeriodValidated
**So that** ClaimPeriodValidated is recorded

_Narrative (verbatim from the board export)_: Explicit human confirmation, not an automated pass/fail - date-of-loss-vs-active-period disputes (was the policy actually in force at the exact moment of loss, retroactive date issues) are exactly the kind of determination that ends up in coverage disputes and shouldn't be silently auto-decided.

**Acceptance Criteria:**
- AC-9.1: Given S5.5: only required when ClaimNotifiedAgainstInactivePolicy has fired - a claims handler manually confirms the loss falls within the period on cover before reserve/payment proceeds., When ValidateClaimPeriod, Then ClaimPeriodValidated

## Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| FR-1 | System MUST support NotifyClaim, producing the ClaimNotified domain event(s) | Must | AC-1.1 |
| FR-2 | System MUST support SetClaimReserve, producing the ClaimReserveSet domain event(s) | Must | AC-2.1 |
| FR-3 | System MUST support PayClaim, producing the ClaimPaid domain event(s) | Must | AC-3.1 |
| FR-4 | System MUST support CloseClaim, producing the ClaimClosed domain event(s) | Must | AC-4.1 |
| FR-5 | System MUST support ReopenClaim, producing the ClaimReopened domain event(s) | Must | AC-5.1 |
| FR-6 | System SHOULD project ClaimRegister from the ClaimNotified domain event(s) | Should | AC-6.1 |
| FR-7 | System COULD record the ReserveRevisionReferred domain event(s) under the condition described in US-7's narrative | Could | AC-7.1 |
| FR-8 | System COULD record the ClaimNotifiedAgainstInactivePolicy domain event(s) under the condition described in US-8's narrative | Could | AC-8.1 |
| FR-9 | System MUST support ValidateClaimPeriod, producing the ClaimPeriodValidated domain event(s) | Must | AC-9.1 |

## Non-Functional Requirements

<!-- The source board does not specify numeric NFR targets for this context — every row is N/A, not invented, per constitution Principle III. Revisit via a clarification pass before /ralph-specum:design if any of these genuinely matter for this feature. -->

| ID | Requirement | Metric | Target |
|----|-------------|--------|--------|
| NFR-1 | Performance | N/A | N/A: not specified by board export |
| NFR-2 | Reliability | N/A | N/A: not specified by board export |
| NFR-3 | Security | N/A | N/A: not specified by board export |

## Glossary

- **Claim**: Aggregate in the Claims context (see Event Model Detail for the elements that touch it).
- **ClaimRegister**: Read model. S5.1: the claim's permanent record, canonical current state.

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

- `ReserveRevisionReferred` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review
- `ClaimNotifiedAgainstInactivePolicy` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct. Owner: user, next review

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export — every field's type, cardinality, and flags. This is the lossless source; the User Stories above are a readable summary of it, not the other way around. Screens are deliberately excluded here — see this spec's `research.md` (UI Reference section), which the design phase reads directly; screens are UI reference, not a requirement.

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

