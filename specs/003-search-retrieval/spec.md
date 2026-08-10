# Feature Specification: Search Retrieval

**Feature Branch**: `003-search-retrieval`

**Created**: 2026-08-10

**Status**: Draft

**Input**: Derived from the "BrokerConnect" eventmodelers.ai board (`80f53178-c291-43a0-8aa5-bc723990c5db`), chapter **Broker Connect**, context **Search & Retrieval** — pulled live via `GET .../slicedata?contextName=...` on 2026-08-10 and cached at `event-model/slices/*.json` / `event-model/import-config.json` (Phase A / MVP scope only — see `Project Plan/01-project-plan.md` §4; the 4 exploratory contexts are not yet pulled). Supersedes an earlier partial export now kept at `event-model/archive/` for provenance — see `event-model-to-speckit-guide.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SubmissionHistorySearchPerformed (Priority: P1)

As **SearchSubmissionHistory**, I want to submissionHistorySearchPerformed so that **SubmissionHistorySearchPerformed** is recorded.

**Narrative** (verbatim from the board export): Used by underwriters (cell-scoped, 1b.1) and operations (broker-scoped, cross-cell, 1b.3) with the same shape, different searcherRole/scope. OPEN QUESTION (flagged by BA): is logging every search actually valuable, or over-engineering an audit trail for a read-only convenience feature? Leaning toward logging at the query level, not per-keystroke, given fast search-as-you-type UX. Also open: does free-text hit normalized ADEPT fields only, or raw broker documents too (a much bigger document-search problem)?

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **SearchSubmissionHistory** under the right preconditions and asserting that **SubmissionHistorySearchPerformed** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** 1b.1: underwriter searches by risk attribute, scoped to their own cell by default (same authority-context mechanism as Context 0)., **When** SearchSubmissionHistory, **Then** SubmissionHistorySearchPerformed

---

### User Story 2 - SubmissionHistorySearchPerformed (Priority: P2)

The system maintains **SubmissionSearchResults**, projected from **SubmissionHistorySearchPerformed**.

**Narrative** (verbatim from the board export): Ranked/filtered list of matching historic submissions, each linking back to the full submission record including downstream decisioning/bind/claims history if it progressed that far.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **SubmissionHistorySearchPerformed** and asserting that **SubmissionSearchResults** reflects the update.

**Acceptance Scenarios**:

1. **Given** Second instance of the same event type, paired here with the SubmissionSearchResults projection it feeds., **When** SubmissionHistorySearchPerformed is appended, **Then** **SubmissionSearchResults** reflects it

---

### User Story 3 - CrossCellSearchAttempted (Priority: P2)

The system maintains **BrokerActivityView**, projected from **CrossCellSearchAttempted**.

**Narrative** (verbatim from the board export): 1b.3: all submissions from a given broker across all cells they're authorized for, with status breakdown - a cross-cell view ops needs for reconciliation even though individual underwriters don't for day-to-day work. Confirms Search & Retrieval needs at least two permission tiers baked in from day one (cell-scoped vs cross-cell), not a generic search with permission filtering bolted on afterward.

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **CrossCellSearchAttempted** and asserting that **BrokerActivityView** reflects the update.

**Acceptance Scenarios**:

1. **Given** 1b.4 permission-boundary design decision: default to cell-scoped visibility for underwriters (an underwriter in Aviation cannot by default see a submission that went to Energy for the same named insured), with cross-cell visibility as an explicit, logged, elevated permission for ops/governance (1b.3). Same audit-value reasoning as SubmissionRoutingRejected in Submission Intake - kept visible for whoever manages access policy, not just silently enforced. Possible middle ground worth exploring: a narrower 'same named insured, cross-cell' flag visible to underwriters without exposing full submission/pricing detail., **When** CrossCellSearchAttempted is appended, **Then** **BrokerActivityView** reflects it

---

### User Story 4 - SubmissionLineageRetrieved (Priority: P2)

As a background policy in **Search & Retrieval**, the system reacts by executing **RetrieveSubmissionLineageOnClaimNotified**, producing **SubmissionLineageRetrieved**.

**Narrative** (verbatim from the board export): Triggered automatically whenever ClaimNotified fires (Context 5), not a manual search action - matches how claims handlers actually work.

**Why this priority**: System-driven policy that keeps derived state (bordereaux, projections, notifications) consistent after the primary action(s) that trigger it.

**Independent Test**: Can be tested by invoking **RetrieveSubmissionLineageOnClaimNotified** under the right preconditions and asserting that **SubmissionLineageRetrieved** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** 1b.2, modeled deliberately as its own capability rather than folded into generic search (BA judgment call, agreed): a claims handler needing to verify a claim against original bind terms is a direct traversal by policy/bind reference, not a search over criteria. Likely one of the highest-value pieces of the whole 'AI-assisted search' capability described in the press coverage - solves exactly the 'days of manual reconstruction' problem., **When** RetrieveSubmissionLineageOnClaimNotified, **Then** SubmissionLineageRetrieved

---

### Edge Cases

- None documented as separate branches in the source export for this context; every command in this context has exactly one outcome event.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support **SearchSubmissionHistory**, producing the **SubmissionHistorySearchPerformed** domain event(s).
- **FR-002**: System MUST project **SubmissionSearchResults** from the **SubmissionHistorySearchPerformed** domain event(s).
- **FR-003**: System MUST project **BrokerActivityView** from the **CrossCellSearchAttempted** domain event(s).
- **FR-004**: System MUST support **RetrieveSubmissionLineageOnClaimNotified**, producing the **SubmissionLineageRetrieved** domain event(s).

### Key Entities *(include if feature involves data)*

- **SearchActivityLog**: aggregate in the **Search & Retrieval** context; touched by **CrossCellSearchAttempted**, **RetrieveSubmissionLineageOnClaimNotified**, **SearchSubmissionHistory**, **SubmissionHistorySearchPerformed**, **SubmissionLineageRetrieved**.
- **SubmissionSearchResults** (read model): Ranked/filtered list of matching historic submissions, each linking back to the full submission record including downstream decisioning/bind/claims history if it progressed that far. Fields: `submissionId`, `namedInsured`, `classOfBusiness`, `territory`, `brokerFirmId`, `status`, `receivedAt`.
- **BrokerActivityView** (read model): 1b.3: all submissions from a given broker across all cells they're authorized for, with status breakdown - a cross-cell view ops needs for reconciliation even though individual underwriters don't for day-to-day work. Confirms Search & Retrieval needs at least two permission tiers baked in from day one (cell-scoped vs cross-cell), not a generic search with permission filtering bolted on afterward. Fields: `brokerFirmId`, `cellBreakdown`, `periodStart`, `periodEnd`, `generatedAt`.
- **PolicyOriginationView** (read model): Given a bound policy reference from a claim, the full chain: original submission -> decisioning path -> quote -> bind terms, without constructing a search query. Retrieved by policy/bind reference, not free-text search. Fields: `policyId`, `submissionId`, `namedInsured`, `classOfBusiness`, `originalSubmissionReceivedAt`, `decisioningPath`, `referredTo`, `quoteTerms`, `boundTerms`, `boundAt`.

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

- Derived from the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Search & Retrieval**); command/event names, field lists, descriptions, aggregates, and lanes in the Event Model Detail section below are transcribed directly from a live pull of that board, not invented for this document.
- Pulled live on 2026-08-10 via the `slicedata` endpoint (see `event-model-to-speckit-guide.md`, "The real export endpoint"), not the raw event-replay log — re-run `event-model/build-scripts/gen_specs_from_slices.py` after any further board edits to keep this in sync; nothing here watches the board automatically.
- All 4 slice(s) in this context currently carry board status `Created` — none are `Planned` or built yet.
- The source board did not specify performance, scale, or business-metric assumptions for this context — the Success Criteria above are template placeholders.
- [Assumption about scope boundaries — confirm before /speckit.plan]

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export, including every field's type, cardinality, and flags (`id` = identifier field, `generated` = system-generated, `optional` = nullable). Nested `List`/object fields show their subfields indented beneath them with `↳`.

### Slice: SubmissionHistorySearchPerformed (`5198e4b4-62ba-45ce-b55c-150d84c91451`, status: Created, type: STATE_CHANGE)

**SearchSubmissionHistory** (command, id `76be35e2-f967-4c57-ba37-20876f22f040`, aggregate `SearchActivityLog`, lane `Interaction`, modelContext `Search & Retrieval`)

> Used by underwriters (cell-scoped, 1b.1) and operations (broker-scoped, cross-cell, 1b.3) with the same shape, different searcherRole/scope. OPEN QUESTION (flagged by BA): is logging every search actually valuable, or over-engineering an audit trail for a read-only convenience feature? Leaning toward logging at the query level, not per-keystroke, given fast search-as-you-type UX. Also open: does free-text hit normalized ADEPT fields only, or raw broker documents too (a much bigger document-search problem)?

Dependencies: → SubmissionHistorySearchPerformed (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `searcherId` | String | Single | — |
| `searcherRole` | String | Single | — |
| `searcherCellContext` | String | Single | optional |
| `searchCriteria` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `classOfBusiness` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `territory` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `brokerFirmId` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `namedInsured` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `dateRangeStart` | Date | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `dateRangeEnd` | Date | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `freeText` | String | Single | optional |

**SubmissionHistorySearchPerformed** (event, id `41576cb0-ebc9-41af-bfed-abfc0c527565`, aggregate `SearchActivityLog`, lane `Submission Search`, modelContext `Search & Retrieval`)

> 1b.1: underwriter searches by risk attribute, scoped to their own cell by default (same authority-context mechanism as Context 0).

Dependencies: → SubmissionSearchResults (READMODEL); ← SearchSubmissionHistory (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `searchId` | UUID | Single | id, generated |
| `searcherId` | String | Single | — |
| `searcherRole` | String | Single | — |
| `criteriaUsed` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `classOfBusiness` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `territory` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `brokerFirmId` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `namedInsured` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `dateRangeStart` | Date | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `dateRangeEnd` | Date | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `freeText` | String | Single | optional |
| `resultCount` | Integer | Single | — |
| `performedAt` | DateTime | Single | generated |

### Slice: SubmissionHistorySearchPerformed (`72117f92-ea38-4ef6-ab4a-be316862c68f`, status: Created, type: STATE_VIEW)

**SubmissionHistorySearchPerformed** (event, id `30a66da8-0b2f-4f6a-b910-d8d695b03a25`, aggregate `SearchActivityLog`, lane `Submission Search`, modelContext `Search & Retrieval`)

> Second instance of the same event type, paired here with the SubmissionSearchResults projection it feeds.

Dependencies: → SubmissionSearchResults (READMODEL); → BrokerActivityView (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `searchId` | UUID | Single | id, generated |
| `resultCount` | Integer | Single | — |

**SubmissionSearchResults** (read model, id `c0a974ae-3b8b-4a4a-a1bb-abe361d9c7e6`, aggregate `SearchActivityLog`, lane `Interaction`, modelContext `Search & Retrieval`)

> Ranked/filtered list of matching historic submissions, each linking back to the full submission record including downstream decisioning/bind/claims history if it progressed that far.

Dependencies: ← SubmissionHistorySearchPerformed (EVENT); ← SubmissionHistorySearchPerformed (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `namedInsured` | String | Single | — |
| `classOfBusiness` | String | Single | — |
| `territory` | String | Single | — |
| `brokerFirmId` | String | Single | — |
| `status` | String | Single | — |
| `receivedAt` | DateTime | Single | — |

### Slice: CrossCellSearchAttempted (`727c0f78-ecaa-4129-b940-c1b60e3a58da`, status: Created, type: STATE_VIEW)

**CrossCellSearchAttempted** (event, id `82b14e31-b8c9-47ef-8fb3-4114332eead6`, aggregate `SearchActivityLog`, lane `Submission Search`, modelContext `Search & Retrieval`)

> 1b.4 permission-boundary design decision: default to cell-scoped visibility for underwriters (an underwriter in Aviation cannot by default see a submission that went to Energy for the same named insured), with cross-cell visibility as an explicit, logged, elevated permission for ops/governance (1b.3). Same audit-value reasoning as SubmissionRoutingRejected in Submission Intake - kept visible for whoever manages access policy, not just silently enforced. Possible middle ground worth exploring: a narrower 'same named insured, cross-cell' flag visible to underwriters without exposing full submission/pricing detail.

Dependencies: → BrokerActivityView (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `searcherId` | String | Single | — |
| `searcherRole` | String | Single | — |
| `requestedScope` | String | Single | — |
| `searchCriteria` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `classOfBusiness` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `territory` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `brokerFirmId` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `namedInsured` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `dateRangeStart` | Date | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `dateRangeEnd` | Date | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `freeText` | String | Single | optional |
| `permitted` | Boolean | Single | — |
| `deniedReason` | String | Single | optional |
| `attemptedAt` | DateTime | Single | generated |

**BrokerActivityView** (read model, id `65488c76-9271-45be-9855-4396da244af7`, aggregate `SearchActivityLog`, lane `Interaction`, modelContext `Search & Retrieval`)

> 1b.3: all submissions from a given broker across all cells they're authorized for, with status breakdown - a cross-cell view ops needs for reconciliation even though individual underwriters don't for day-to-day work. Confirms Search & Retrieval needs at least two permission tiers baked in from day one (cell-scoped vs cross-cell), not a generic search with permission filtering bolted on afterward.

Dependencies: ← CrossCellSearchAttempted (EVENT); ← SubmissionHistorySearchPerformed (EVENT); → RetrieveSubmissionLineageOnClaimNotified (AUTOMATION)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `brokerFirmId` | String | Single | id |
| `cellBreakdown` | Custom | List | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `cellId` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `submissionCount` | Integer | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `boundCount` | Integer | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `declinedCount` | Integer | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `expiredCount` | Integer | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `inFlightCount` | Integer | Single | — |
| `periodStart` | Date | Single | — |
| `periodEnd` | Date | Single | — |
| `generatedAt` | DateTime | Single | generated |

### Slice: SubmissionLineageRetrieved (`681560c2-146b-4005-9137-cbb246e33012`, status: Created, type: AUTOMATION)

**RetrieveSubmissionLineageOnClaimNotified** (automation/processor, id `848e1c78-44b6-489d-aaa2-aaed3b7ab4d6`, aggregate `SearchActivityLog`, lane `Actor`, modelContext `Search & Retrieval`)

> Triggered automatically whenever ClaimNotified fires (Context 5), not a manual search action - matches how claims handlers actually work.

Dependencies: ← PolicyOriginationView (READMODEL); ← BrokerActivityView (READMODEL)

_(no fields)_

**SubmissionLineageRetrieved** (event, id `91aa46fa-45ed-453c-ba61-75e572a714b6`, aggregate `SearchActivityLog`, lane `Submission Search`, modelContext `Search & Retrieval`)

> 1b.2, modeled deliberately as its own capability rather than folded into generic search (BA judgment call, agreed): a claims handler needing to verify a claim against original bind terms is a direct traversal by policy/bind reference, not a search over criteria. Likely one of the highest-value pieces of the whole 'AI-assisted search' capability described in the press coverage - solves exactly the 'days of manual reconstruction' problem.

Dependencies: → PolicyOriginationView (READMODEL)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | — |
| `submissionId` | UUID | Single | — |
| `lineageChain` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `originalSubmissionId` | UUID | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `decisioningPath` | String | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `referredTo` | String | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `quoteId` | UUID | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `boundAt` | DateTime | Single | optional |
| `retrievedAt` | DateTime | Single | generated |

**PolicyOriginationView** (read model, id `0d027666-f0f4-45e0-a1de-e129937798c8`, aggregate `SearchActivityLog`, lane `Interaction`, modelContext `Search & Retrieval`)

> Given a bound policy reference from a claim, the full chain: original submission -> decisioning path -> quote -> bind terms, without constructing a search query. Retrieved by policy/bind reference, not free-text search.

Dependencies: ← SubmissionLineageRetrieved (EVENT); → RetrieveSubmissionLineageOnClaimNotified (AUTOMATION)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `policyId` | UUID | Single | id |
| `submissionId` | UUID | Single | — |
| `namedInsured` | String | Single | — |
| `classOfBusiness` | String | Single | — |
| `originalSubmissionReceivedAt` | DateTime | Single | — |
| `decisioningPath` | String | Single | — |
| `referredTo` | String | Single | optional |
| `quoteTerms` | Custom | Single | — |
| `boundTerms` | Custom | Single | — |
| `boundAt` | DateTime | Single | — |

