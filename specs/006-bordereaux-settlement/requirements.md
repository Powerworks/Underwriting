---
spec: 006-bordereaux-settlement
phase: requirements
created: 2026-08-10
---

# Requirements: Bordereaux Settlement

## Problem Statement

Bordereaux Settlement is one of the bounded contexts modeled on the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Bordereaux Settlement**). Evidence: the board's own live export (`event-model/import-config.json`), transcribed verbatim in the Event Model Detail appendix below — this is not a hypothesis needing validation, the domain already exists as a modeled event model.

## Goal

Implement the **Bordereaux Settlement** bounded context's commands, events, automations, and read models exactly as modeled on the board, per constitution Principle III (the spec is the source of truth — no invented fields, no guessed names).

## User Stories

### US-1: BordereauDrafted

**As a** background policy in Bordereaux Settlement
**I want to** the system to execute GenerateBordereauAutomation automatically
**So that** derived state stays consistent after the triggering action(s): BordereauDrafted

_Narrative (verbatim from the board export)_: Scheduled period-close automation, scoped to one cell + one capacity provider + one period. Pulls together every policy transaction (bind/endorsement/cancellation) in that window not yet included in a prior bordereau.

**Acceptance Criteria:**
- AC-1.1: Given S4.1: draft bordereau created for a cell+provider+period. Becomes a real financial obligation only once agreed (see AgreeBordereau). Each line carries its own generated lineId (fixed per completeness check GAP-001)., When GenerateBordereauAutomation, Then BordereauDrafted

### US-2: BordereauLineQueried

**As a** a consumer of this read-side projection
**I want to** BordereauDetail to reflect BordereauLineQueried
**So that** downstream queries/decisions see current state

_Narrative (verbatim from the board export)_: Working view of a bordereau and its lines for the provider/TPA review window - each line carries its own status (draft/queried/agreed/settled), not just the bordereau's overall status.

**Acceptance Criteria:**
- AC-2.1: Given Line status becomes Queried, independent of the bordereau's own overall status - each line tracks its own draft/queried/agreed state., When BordereauLineQueried is appended, Then BordereauDetail reflects it

### US-3: BordereauLineResolved

**As a** ResolveBordereauQuery
**I want to** bordereauLineResolved
**So that** BordereauLineResolved is recorded

_Narrative (verbatim from the board export)_: Underwriter/Operations resolves a previously raised query.

**Acceptance Criteria:**
- AC-3.1: Given Line status moves out of Queried. A single disputed line can span multiple periods before resolution., When ResolveBordereauQuery, Then BordereauLineResolved

### US-4: BordereauAgreed

**As a** AgreeBordereau
**I want to** bordereauAgreed
**So that** BordereauAgreed is recorded

_Narrative (verbatim from the board export)_: Once every queried line is resolved, the capacity provider agrees the bordereau - this is the point it becomes a real financial obligation, not just a draft.

**Acceptance Criteria:**
- AC-4.1: Given S4.1/S4.2: only fires once BordereauSubmittedForAgreement has been confirmed by the provider AND zero queries remain open on the bordereau - by construction, since any query still open at period-close is resolved via BordereauLineResolved or excluded via BordereauLineDeferred before this point. No bordereau is ever agreed with a genuinely unresolved query still attached., When AgreeBordereau, Then BordereauAgreed

### US-5: BordereauSettled

**As a** SettleBordereau
**I want to** bordereauSettled
**So that** BordereauSettled is recorded

_Narrative (verbatim from the board export)_: Cash actually moves - net premium cell to provider (or claims float top-up provider to cell). Multi-currency: cell writes premium in local currency, providers may need it reported in USD, so FX rate at settlement is captured.

**Acceptance Criteria:**
- AC-5.1: Given the preconditions for this step are met, When SettleBordereau, Then BordereauSettled

### US-6: AdjustmentLineRaised

**As a** RaisePriorPeriodAdjustment
**I want to** adjustmentLineRaised
**So that** AdjustmentLineRaised is recorded

_Narrative (verbatim from the board export)_: An error found after a bordereau was already agreed is corrected via a new adjustment line in a future period - never by editing history. Bordereaux are an append-only ledger, mirroring the underlying policy transactions.

**Acceptance Criteria:**
- AC-6.1: Given S4.4: the correction flows into next period's BordereauDrafted sweep as a new line - the original historical bordereau remains untouched, consistent with the immutable-ledger principle running through the whole model., When RaisePriorPeriodAdjustment, Then AdjustmentLineRaised

### US-7: BordereauSubmittedForAgreement

**As a** SubmitBordereauForAgreement
**I want to** bordereauSubmittedForAgreement
**So that** BordereauSubmittedForAgreement is recorded

_Narrative (verbatim from the board export)_: TFP finance/ops signals internal readiness - distinct from the provider's own confirmation (BordereauAgreed).

**Acceptance Criteria:**
- AC-7.1: Given S4.1: two-party agreement modeled as two events (BordereauSubmittedForAgreement -> BordereauAgreed) rather than one - TFP's internal sign-off is not the same fact as the provider's actual confirmation, and bordereaux are literally how money moves., When SubmitBordereauForAgreement, Then BordereauSubmittedForAgreement

### US-8: BordereauDrafted

**As a** a consumer of this read-side projection
**I want to** UnbilledTransactionsForPeriod to reflect BordereauDrafted
**So that** downstream queries/decisions see current state

_Narrative (verbatim from the board export)_: S4.1: explicit link from each transaction to at most one bordereau (or none) - answers 'how does the system know a transaction hasn't already been included' as a queryable read model concern rather than an assumed fact.

**Acceptance Criteria:**
- AC-8.1: Given Second instance of the same event type, paired here with the read model that determines what's swept into it., When BordereauDrafted is appended, Then UnbilledTransactionsForPeriod reflects it

### US-9: BordereauLineDeferred

**As a** background policy in Bordereaux Settlement
**I want to** the system to execute DeferQueriedLineAtPeriodClose automatically
**So that** derived state stays consistent after the triggering action(s): BordereauLineDeferred

_Narrative (verbatim from the board export)_: Trigger: a BordereauLineQueried remains open as the period's normal processing timeline elapses.

**Acceptance Criteria:**
- AC-9.1: Given S4.3: recommended default over blocking the whole bordereau - a single disputed line shouldn't hold up cash movement on everything else in the period. Mirrors the AdjustmentLineRaised pattern (S4.4) already established: a line rolls into a future period's draft, never editing history. This is what makes BordereauAgreed's 'zero open queries' rule always achievable by period-close - open queries are always either resolved or deferred before agreement, never left dangling., When DeferQueriedLineAtPeriodClose, Then BordereauLineDeferred

## Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| FR-1 | System SHOULD support GenerateBordereauAutomation, producing the BordereauDrafted domain event(s) | Should | AC-1.1 |
| FR-2 | System SHOULD project BordereauDetail from the BordereauLineQueried domain event(s) | Should | AC-2.1 |
| FR-3 | System MUST support ResolveBordereauQuery, producing the BordereauLineResolved domain event(s) | Must | AC-3.1 |
| FR-4 | System MUST support AgreeBordereau, producing the BordereauAgreed domain event(s) | Must | AC-4.1 |
| FR-5 | System MUST support SettleBordereau, producing the BordereauSettled domain event(s) | Must | AC-5.1 |
| FR-6 | System MUST support RaisePriorPeriodAdjustment, producing the AdjustmentLineRaised domain event(s) | Must | AC-6.1 |
| FR-7 | System MUST support SubmitBordereauForAgreement, producing the BordereauSubmittedForAgreement domain event(s) | Must | AC-7.1 |
| FR-8 | System SHOULD project UnbilledTransactionsForPeriod from the BordereauDrafted domain event(s) | Should | AC-8.1 |
| FR-9 | System SHOULD support DeferQueriedLineAtPeriodClose, producing the BordereauLineDeferred domain event(s) | Should | AC-9.1 |

## Non-Functional Requirements

<!-- The source board does not specify numeric NFR targets for this context — every row is N/A, not invented, per constitution Principle III. Revisit via a clarification pass before /ralph-specum:design if any of these genuinely matter for this feature. -->

| ID | Requirement | Metric | Target |
|----|-------------|--------|--------|
| NFR-1 | Performance | N/A | N/A: not specified by board export |
| NFR-2 | Reliability | N/A | N/A: not specified by board export |
| NFR-3 | Security | N/A | N/A: not specified by board export |

## Glossary

- **Bordereau**: Aggregate in the Bordereaux Settlement context (see Event Model Detail for the elements that touch it).
- **BordereauDetail**: Read model. Working view of a bordereau and its lines for the provider/TPA review window - each line carries its own status (draft/queried/agreed/settled), not just the bordereau's overall status.
- **UnbilledTransactionsForPeriod**: Read model. S4.1: explicit link from each transaction to at most one bordereau (or none) - answers 'how does the system know a transaction hasn't already been included' as a queryable read model concern rather than an assumed fact.

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

- None

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export — every field's type, cardinality, and flags. This is the lossless source; the User Stories above are a readable summary of it, not the other way around. Screens are deliberately excluded here — see this spec's `research.md` (UI Reference section), which the design phase reads directly; screens are UI reference, not a requirement.

### Slice: BordereauDrafted (`92701df5-01e7-477d-9bed-f79fcd51e79f`, status: Created, type: AUTOMATION)

**GenerateBordereauAutomation** (automation/processor, id `23aef16a-fa37-4219-80c3-17e3285045e8`, aggregate `Bordereau`, lane `Actor`, modelContext `Bordereaux Settlement`)

> Scheduled period-close automation, scoped to one cell + one capacity provider + one period. Pulls together every policy transaction (bind/endorsement/cancellation) in that window not yet included in a prior bordereau.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `cellId` | String | Single | — |
| `providerId` | String | Single | — |
| `periodStart` | Date | Single | — |
| `periodEnd` | Date | Single | — |

**BordereauDrafted** (event, id `1bacdc7c-b263-47ad-909f-6cc7b91ef689`, aggregate `Bordereau`, lane `Bordereau`, modelContext `Bordereaux Settlement`)

> S4.1: draft bordereau created for a cell+provider+period. Becomes a real financial obligation only once agreed (see AgreeBordereau). Each line carries its own generated lineId (fixed per completeness check GAP-001).

Dependencies: → BordereauDetail (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | id, generated |
| `cellId` | String | Single | — |
| `providerId` | String | Single | — |
| `periodStart` | Date | Single | — |
| `periodEnd` | Date | Single | — |
| `lines` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineId` | UUID | Single | generated |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `policyTransactionId` | UUID | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `transactionType` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `grossPremium` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `netPremium` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `providerShare` | Decimal | Single | — |
| `lineCount` | Integer | Single | generated |
| `totalNetPremium` | Decimal | Single | — |
| `currency` | String | Single | — |
| `status` | String | Single | — |
| `draftedAt` | DateTime | Single | generated |

### Slice: BordereauLineQueried (`b498120d-252f-40ee-a036-41df7bdea135`, status: Created, type: STATE_VIEW)

**BordereauLineQueried** (event, id `cf06c905-289e-4415-b1fd-4205ca8ec751`, aggregate `Bordereau`, lane `Bordereau`, modelContext `Bordereaux Settlement`)

> Line status becomes Queried, independent of the bordereau's own overall status - each line tracks its own draft/queried/agreed state.

Dependencies: → BordereauDetail (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | — |
| `lineId` | UUID | Single | — |
| `queryId` | UUID | Single | id, generated |
| `queryReason` | String | Single | — |
| `raisedAt` | DateTime | Single | generated |

**BordereauDetail** (read model, id `97beb5e4-0619-4339-9b15-7bbc4af15b52`, aggregate `Bordereau`, lane `Interaction`, modelContext `Bordereaux Settlement`)

> Working view of a bordereau and its lines for the provider/TPA review window - each line carries its own status (draft/queried/agreed/settled), not just the bordereau's overall status.

Dependencies: ← BordereauLineQueried (EVENT); ← BordereauDrafted (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | id |
| `cellId` | String | Single | — |
| `providerId` | String | Single | — |
| `periodStart` | Date | Single | — |
| `periodEnd` | Date | Single | — |
| `status` | String | Single | — |
| `currency` | String | Single | — |
| `lines` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineId` | UUID | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `policyTransactionId` | UUID | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `transactionType` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `grossPremium` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `brokerageDeducted` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `mgaFeeDeducted` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `netPremium` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `providerShare` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `status` | String | Single | — |

### Slice: BordereauLineResolved (`d343a675-35a7-4336-bc94-b88a174f2f38`, status: Created, type: STATE_CHANGE)

**ResolveBordereauQuery** (command, id `a5239e2f-e152-4d57-82c7-69730e9ed444`, aggregate `Bordereau`, lane `Interaction`, modelContext `Bordereaux Settlement`)

> Underwriter/Operations resolves a previously raised query.

Dependencies: → BordereauLineResolved (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `queryId` | UUID | Single | id |
| `resolution` | String | Single | — |

**BordereauLineResolved** (event, id `7bece8ea-a51e-4850-9fc4-3d8ea26b5ed1`, aggregate `Bordereau`, lane `Bordereau`, modelContext `Bordereaux Settlement`)

> Line status moves out of Queried. A single disputed line can span multiple periods before resolution.

Dependencies: ← ResolveBordereauQuery (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `queryId` | UUID | Single | id |
| `resolution` | String | Single | — |
| `resolvedAt` | DateTime | Single | generated |

### Slice: BordereauAgreed (`ca6ec27b-82fb-4514-b2ed-5636ae857a33`, status: Created, type: STATE_CHANGE)

**AgreeBordereau** (command, id `d3f17a34-1e48-4f91-b728-c98cfc4e9b7c`, aggregate `Bordereau`, lane `Interaction`, modelContext `Bordereaux Settlement`)

> Once every queried line is resolved, the capacity provider agrees the bordereau - this is the point it becomes a real financial obligation, not just a draft.

Dependencies: → BordereauAgreed (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | id |

**BordereauAgreed** (event, id `19748a2d-950c-4b4c-9668-f511201aa0a1`, aggregate `Bordereau`, lane `Bordereau`, modelContext `Bordereaux Settlement`)

> S4.1/S4.2: only fires once BordereauSubmittedForAgreement has been confirmed by the provider AND zero queries remain open on the bordereau - by construction, since any query still open at period-close is resolved via BordereauLineResolved or excluded via BordereauLineDeferred before this point. No bordereau is ever agreed with a genuinely unresolved query still attached.

Dependencies: ← AgreeBordereau (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | id |
| `agreedByTFP` | String | Single | — |
| `agreedByProvider` | String | Single | — |
| `finalAgreedTotal` | Decimal | Single | — |
| `agreedAt` | DateTime | Single | generated |

### Slice: BordereauSettled (`7da6c491-d2a4-4feb-9a85-b5a5e3f3e355`, status: Created, type: STATE_CHANGE)

**SettleBordereau** (command, id `916fc824-8f4f-4019-a159-efa5abb26f16`, aggregate `Bordereau`, lane `Interaction`, modelContext `Bordereaux Settlement`)

> Cash actually moves - net premium cell to provider (or claims float top-up provider to cell). Multi-currency: cell writes premium in local currency, providers may need it reported in USD, so FX rate at settlement is captured.

Dependencies: → BordereauSettled (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | id |
| `settledAmount` | Decimal | Single | — |
| `settlementDate` | Date | Single | — |
| `currency` | String | Single | — |
| `fxRate` | Decimal | Single | optional |

**BordereauSettled** (event, id `a906519d-df7f-44f5-94f0-ec59804da92e`, aggregate `Bordereau`, lane `Bordereau`, modelContext `Bordereaux Settlement`)

Dependencies: ← SettleBordereau (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | id |
| `settledAmount` | Decimal | Single | — |
| `settlementDate` | Date | Single | — |
| `currency` | String | Single | — |
| `fxRate` | Decimal | Single | optional |
| `settledAt` | DateTime | Single | generated |

### Slice: AdjustmentLineRaised (`323a5e11-2a90-4625-a4d6-05b68b5fd95a`, status: Created, type: STATE_CHANGE)

**RaisePriorPeriodAdjustment** (command, id `a5f872b5-e37a-4c35-a742-82e9004c40f4`, aggregate `Bordereau`, lane `Interaction`, modelContext `Bordereaux Settlement`)

> An error found after a bordereau was already agreed is corrected via a new adjustment line in a future period - never by editing history. Bordereaux are an append-only ledger, mirroring the underlying policy transactions.

Dependencies: → AdjustmentLineRaised (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `originalBordereauId` | UUID | Single | — |
| `originalLineId` | UUID | Single | — |
| `adjustmentAmount` | Decimal | Single | — |
| `reason` | String | Single | — |

**AdjustmentLineRaised** (event, id `d1f5b340-1a8f-4dad-8f05-07c3e3edd27a`, aggregate `Bordereau`, lane `Bordereau`, modelContext `Bordereaux Settlement`)

> S4.4: the correction flows into next period's BordereauDrafted sweep as a new line - the original historical bordereau remains untouched, consistent with the immutable-ledger principle running through the whole model.

Dependencies: ← RaisePriorPeriodAdjustment (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `adjustmentId` | UUID | Single | id, generated |
| `originalBordereauId` | UUID | Single | — |
| `originalLineReference` | String | Single | — |
| `correctionAmount` | Decimal | Single | — |
| `reason` | String | Single | — |
| `raisedBy` | String | Single | — |
| `targetSettlementPeriod` | String | Single | — |
| `requiresSeparateAgreement` | Boolean | Single | — |
| `raisedAt` | DateTime | Single | generated |

### Slice: BordereauSubmittedForAgreement (`279e4f19-761f-4784-8576-aee4ae8df29a`, status: Created, type: STATE_CHANGE)

**SubmitBordereauForAgreement** (command, id `361f9a6c-5243-4f05-913d-c09d2d7e0a97`, aggregate `Bordereau`, lane `Interaction`, modelContext `Bordereaux Settlement`)

> TFP finance/ops signals internal readiness - distinct from the provider's own confirmation (BordereauAgreed).

Dependencies: → BordereauSubmittedForAgreement (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | — |

**BordereauSubmittedForAgreement** (event, id `6896defc-5417-4505-a1fa-429ad3606538`, aggregate `Bordereau`, lane `Bordereau`, modelContext `Bordereaux Settlement`)

> S4.1: two-party agreement modeled as two events (BordereauSubmittedForAgreement -> BordereauAgreed) rather than one - TFP's internal sign-off is not the same fact as the provider's actual confirmation, and bordereaux are literally how money moves.

Dependencies: ← SubmitBordereauForAgreement (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | id |
| `submittedBy` | String | Single | — |
| `submittedAt` | DateTime | Single | generated |

### Slice: BordereauDrafted (`6b2a9cf5-e44c-47fd-b832-f433ebadaf1e`, status: Created, type: STATE_VIEW)

**BordereauDrafted** (event, id `94bd52ba-ea00-415c-8a5b-588a73a5aae7`, aggregate `Bordereau`, lane `Bordereau`, modelContext `Bordereaux Settlement`)

> Second instance of the same event type, paired here with the read model that determines what's swept into it.

Dependencies: → UnbilledTransactionsForPeriod (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | id, generated |

**UnbilledTransactionsForPeriod** (read model, id `1a5c048b-6573-46b8-a667-3da0ada3d75a`, aggregate `Bordereau`, lane `Interaction`, modelContext `Bordereaux Settlement`)

> S4.1: explicit link from each transaction to at most one bordereau (or none) - answers 'how does the system know a transaction hasn't already been included' as a queryable read model concern rather than an assumed fact.

Dependencies: → DeferQueriedLineAtPeriodClose (AUTOMATION); ← BordereauDrafted (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `cellId` | String | Single | — |
| `providerId` | String | Single | — |
| `period` | String | Single | — |
| `transactions` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `transactionReference` | UUID | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `transactionType` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `premiumAmount` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `includedInBordereauId` | UUID | Single | optional |

### Slice: BordereauLineDeferred (`48f45bf9-4e30-42b2-8c25-fbec783f40d4`, status: Created, type: AUTOMATION)

**RaiseBordereauQuery** (command, id `a047d9c8-14d3-4535-ab5d-163e6d336c0a`, aggregate `Bordereau`, lane `Interaction`, modelContext `Bordereaux Settlement`)

> Relocated here from its original column, where it had never actually landed on the grid due to the command/readmodel same-cell collision (BordereauDetail took the interaction cell there).

Dependencies: → BordereauLineDeferred (EVENT); ← DeferQueriedLineAtPeriodClose (AUTOMATION)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | — |
| `lineReference` | String | Single | — |
| `queryReason` | String | Single | — |

**DeferQueriedLineAtPeriodClose** (automation/processor, id `209a92fc-4d9b-4b9e-b7c3-95e8376a38bb`, aggregate `Bordereau`, lane `Actor`, modelContext `Bordereaux Settlement`)

> Trigger: a BordereauLineQueried remains open as the period's normal processing timeline elapses.

Dependencies: → RaiseBordereauQuery (COMMAND); ← UnbilledTransactionsForPeriod (READMODEL)

_(no fields)_

**BordereauLineDeferred** (event, id `9a49e4fc-6994-4265-83fd-7d7639cebc40`, aggregate `Bordereau`, lane `Bordereau`, modelContext `Bordereaux Settlement`)

> S4.3: recommended default over blocking the whole bordereau - a single disputed line shouldn't hold up cash movement on everything else in the period. Mirrors the AdjustmentLineRaised pattern (S4.4) already established: a line rolls into a future period's draft, never editing history. This is what makes BordereauAgreed's 'zero open queries' rule always achievable by period-close - open queries are always either resolved or deferred before agreement, never left dangling.

Dependencies: ← RaiseBordereauQuery (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `bordereauId` | UUID | Single | — |
| `lineReference` | String | Single | — |
| `deferredToBordereauId` | UUID | Single | optional |
| `reason` | String | Single | — |
| `deferredAt` | DateTime | Single | generated |

