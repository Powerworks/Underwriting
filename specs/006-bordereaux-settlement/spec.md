# Feature Specification: Bordereaux Settlement

**Feature Branch**: `006-bordereaux-settlement`

**Created**: 2026-08-10

**Status**: Draft

**Input**: Derived from the "BrokerConnect" eventmodelers.ai board (`80f53178-c291-43a0-8aa5-bc723990c5db`), chapter **Broker Connect**, context **Bordereaux Settlement** — pulled live via `GET .../slicedata?contextName=...` on 2026-08-10 and cached at `event-model/slices/*.json` / `event-model/import-config.json` (Phase A / MVP scope only — see `Project Plan/01-project-plan.md` §4; the 4 exploratory contexts are not yet pulled). Supersedes an earlier partial export now kept at `event-model/archive/` for provenance — see `event-model-to-speckit-guide.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - BordereauDrafted (Priority: P2)

As a background policy in **Bordereaux Settlement**, the system reacts by executing **GenerateBordereauAutomation**, producing **BordereauDrafted**.

**Narrative** (verbatim from the board export): Scheduled period-close automation, scoped to one cell + one capacity provider + one period. Pulls together every policy transaction (bind/endorsement/cancellation) in that window not yet included in a prior bordereau.

**Why this priority**: System-driven policy that keeps derived state (bordereaux, projections, notifications) consistent after the primary action(s) that trigger it.

**Independent Test**: Can be tested by invoking **GenerateBordereauAutomation** under the right preconditions and asserting that **BordereauDrafted** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S4.1: draft bordereau created for a cell+provider+period. Becomes a real financial obligation only once agreed (see AgreeBordereau). Each line carries its own generated lineId (fixed per completeness check GAP-001)., **When** GenerateBordereauAutomation, **Then** BordereauDrafted

---

### User Story 2 - BordereauLineQueried (Priority: P2)

The system maintains **BordereauDetail**, projected from **BordereauLineQueried**.

**Narrative** (verbatim from the board export): Working view of a bordereau and its lines for the provider/TPA review window - each line carries its own status (draft/queried/agreed/settled), not just the bordereau's overall status.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **BordereauLineQueried** and asserting that **BordereauDetail** reflects the update.

**Acceptance Scenarios**:

1. **Given** Line status becomes Queried, independent of the bordereau's own overall status - each line tracks its own draft/queried/agreed state., **When** BordereauLineQueried is appended, **Then** **BordereauDetail** reflects it

---

### User Story 3 - BordereauLineResolved (Priority: P1)

As **ResolveBordereauQuery**, I want to bordereauLineResolved so that **BordereauLineResolved** is recorded.

**Narrative** (verbatim from the board export): Underwriter/Operations resolves a previously raised query.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **ResolveBordereauQuery** under the right preconditions and asserting that **BordereauLineResolved** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** Line status moves out of Queried. A single disputed line can span multiple periods before resolution., **When** ResolveBordereauQuery, **Then** BordereauLineResolved

---

### User Story 4 - BordereauAgreed (Priority: P1)

As **AgreeBordereau**, I want to bordereauAgreed so that **BordereauAgreed** is recorded.

**Narrative** (verbatim from the board export): Once every queried line is resolved, the capacity provider agrees the bordereau - this is the point it becomes a real financial obligation, not just a draft.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **AgreeBordereau** under the right preconditions and asserting that **BordereauAgreed** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S4.1/S4.2: only fires once BordereauSubmittedForAgreement has been confirmed by the provider AND zero queries remain open on the bordereau - by construction, since any query still open at period-close is resolved via BordereauLineResolved or excluded via BordereauLineDeferred before this point. No bordereau is ever agreed with a genuinely unresolved query still attached., **When** AgreeBordereau, **Then** BordereauAgreed

---

### User Story 5 - BordereauSettled (Priority: P1)

As **SettleBordereau**, I want to bordereauSettled so that **BordereauSettled** is recorded.

**Narrative** (verbatim from the board export): Cash actually moves - net premium cell to provider (or claims float top-up provider to cell). Multi-currency: cell writes premium in local currency, providers may need it reported in USD, so FX rate at settlement is captured.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **SettleBordereau** under the right preconditions and asserting that **BordereauSettled** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** the preconditions for this step are met, **When** SettleBordereau, **Then** BordereauSettled

---

### User Story 6 - AdjustmentLineRaised (Priority: P1)

As **RaisePriorPeriodAdjustment**, I want to adjustmentLineRaised so that **AdjustmentLineRaised** is recorded.

**Narrative** (verbatim from the board export): An error found after a bordereau was already agreed is corrected via a new adjustment line in a future period - never by editing history. Bordereaux are an append-only ledger, mirroring the underlying policy transactions.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **RaisePriorPeriodAdjustment** under the right preconditions and asserting that **AdjustmentLineRaised** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S4.4: the correction flows into next period's BordereauDrafted sweep as a new line - the original historical bordereau remains untouched, consistent with the immutable-ledger principle running through the whole model., **When** RaisePriorPeriodAdjustment, **Then** AdjustmentLineRaised

---

### User Story 7 - BordereauSubmittedForAgreement (Priority: P1)

As **SubmitBordereauForAgreement**, I want to bordereauSubmittedForAgreement so that **BordereauSubmittedForAgreement** is recorded.

**Narrative** (verbatim from the board export): TFP finance/ops signals internal readiness - distinct from the provider's own confirmation (BordereauAgreed).

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **SubmitBordereauForAgreement** under the right preconditions and asserting that **BordereauSubmittedForAgreement** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S4.1: two-party agreement modeled as two events (BordereauSubmittedForAgreement -> BordereauAgreed) rather than one - TFP's internal sign-off is not the same fact as the provider's actual confirmation, and bordereaux are literally how money moves., **When** SubmitBordereauForAgreement, **Then** BordereauSubmittedForAgreement

---

### User Story 8 - BordereauDrafted (Priority: P2)

The system maintains **UnbilledTransactionsForPeriod**, projected from **BordereauDrafted**.

**Narrative** (verbatim from the board export): S4.1: explicit link from each transaction to at most one bordereau (or none) - answers 'how does the system know a transaction hasn't already been included' as a queryable read model concern rather than an assumed fact.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **BordereauDrafted** and asserting that **UnbilledTransactionsForPeriod** reflects the update.

**Acceptance Scenarios**:

1. **Given** Second instance of the same event type, paired here with the read model that determines what's swept into it., **When** BordereauDrafted is appended, **Then** **UnbilledTransactionsForPeriod** reflects it

---

### User Story 9 - BordereauLineDeferred (Priority: P2)

As a background policy in **Bordereaux Settlement**, the system reacts by executing **DeferQueriedLineAtPeriodClose**, producing **BordereauLineDeferred**.

**Narrative** (verbatim from the board export): Trigger: a BordereauLineQueried remains open as the period's normal processing timeline elapses.

**Why this priority**: System-driven policy that keeps derived state (bordereaux, projections, notifications) consistent after the primary action(s) that trigger it.

**Independent Test**: Can be tested by invoking **DeferQueriedLineAtPeriodClose** under the right preconditions and asserting that **BordereauLineDeferred** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S4.3: recommended default over blocking the whole bordereau - a single disputed line shouldn't hold up cash movement on everything else in the period. Mirrors the AdjustmentLineRaised pattern (S4.4) already established: a line rolls into a future period's draft, never editing history. This is what makes BordereauAgreed's 'zero open queries' rule always achievable by period-close - open queries are always either resolved or deferred before agreement, never left dangling., **When** DeferQueriedLineAtPeriodClose, **Then** BordereauLineDeferred

---

### Edge Cases

- None documented as separate branches in the source export for this context; every command in this context has exactly one outcome event.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support **GenerateBordereauAutomation**, producing the **BordereauDrafted** domain event(s).
- **FR-002**: System MUST project **BordereauDetail** from the **BordereauLineQueried** domain event(s).
- **FR-003**: System MUST support **ResolveBordereauQuery**, producing the **BordereauLineResolved** domain event(s).
- **FR-004**: System MUST support **AgreeBordereau**, producing the **BordereauAgreed** domain event(s).
- **FR-005**: System MUST support **SettleBordereau**, producing the **BordereauSettled** domain event(s).
- **FR-006**: System MUST support **RaisePriorPeriodAdjustment**, producing the **AdjustmentLineRaised** domain event(s).
- **FR-007**: System MUST support **SubmitBordereauForAgreement**, producing the **BordereauSubmittedForAgreement** domain event(s).
- **FR-008**: System MUST project **UnbilledTransactionsForPeriod** from the **BordereauDrafted** domain event(s).
- **FR-009**: System MUST support **DeferQueriedLineAtPeriodClose**, producing the **BordereauLineDeferred** domain event(s).

### Key Entities *(include if feature involves data)*

- **Bordereau**: aggregate in the **Bordereaux Settlement** context; touched by **AdjustmentLineRaised**, **AgreeBordereau**, **BordereauAgreed**, **BordereauDrafted**, **BordereauLineDeferred**, **BordereauLineQueried**, **BordereauLineResolved**, **BordereauSettled**, **BordereauSubmittedForAgreement**, **DeferQueriedLineAtPeriodClose**, **GenerateBordereauAutomation**, **RaiseBordereauQuery**, **RaisePriorPeriodAdjustment**, **ResolveBordereauQuery**, **SettleBordereau**, **SubmitBordereauForAgreement**.
- **BordereauDetail** (read model): Working view of a bordereau and its lines for the provider/TPA review window - each line carries its own status (draft/queried/agreed/settled), not just the bordereau's overall status. Fields: `bordereauId`, `cellId`, `providerId`, `periodStart`, `periodEnd`, `status`, `currency`, `lines`.
- **UnbilledTransactionsForPeriod** (read model): S4.1: explicit link from each transaction to at most one bordereau (or none) - answers 'how does the system know a transaction hasn't already been included' as a queryable read model concern rather than an assumed fact. Fields: `cellId`, `providerId`, `period`, `transactions`.

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

- Derived from the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Bordereaux Settlement**); command/event names, field lists, descriptions, aggregates, and lanes in the Event Model Detail section below are transcribed directly from a live pull of that board, not invented for this document.
- Pulled live on 2026-08-10 via the `slicedata` endpoint (see `event-model-to-speckit-guide.md`, "The real export endpoint"), not the raw event-replay log — re-run `event-model/build-scripts/gen_specs_from_slices.py` after any further board edits to keep this in sync; nothing here watches the board automatically.
- All 9 slice(s) in this context currently carry board status `Created` — none are `Planned` or built yet.
- The source board did not specify performance, scale, or business-metric assumptions for this context — the Success Criteria above are template placeholders.
- [Assumption about scope boundaries — confirm before /speckit.plan]

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export, including every field's type, cardinality, and flags (`id` = identifier field, `generated` = system-generated, `optional` = nullable). Nested `List`/object fields show their subfields indented beneath them with `↳`.

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

