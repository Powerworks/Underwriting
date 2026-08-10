# Feature Specification: Underwriting Decisioning

**Feature Branch**: `004-underwriting-decisioning`

**Created**: 2026-08-10

**Status**: Draft

**Input**: Derived from the "BrokerConnect" eventmodelers.ai board (`80f53178-c291-43a0-8aa5-bc723990c5db`), chapter **Broker Connect**, context **Underwriting Decisioning** — pulled live via `GET .../slicedata?contextName=...` on 2026-08-10 and cached at `event-model/slices/*.json` / `event-model/import-config.json` (Phase A / MVP scope only — see `Project Plan/01-project-plan.md` §4; the 4 exploratory contexts are not yet pulled). Supersedes an earlier partial export now kept at `event-model/archive/` for provenance — see `event-model-to-speckit-guide.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SubmissionWithinAuthority (Priority: P2)

The system maintains **AuthorityLimit**, projected from **SubmissionWithinAuthority**.

**Narrative** (verbatim from the board export): Projection of the 3-tier authority cascade (Capacity Provider -> Cell -> Underwriter) used to validate an assessment. Populated by the Authority Administration slices (grant/revise/revoke).

**Why this priority**: Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right.

**Independent Test**: Can be tested by appending **SubmissionWithinAuthority** and asserting that **AuthorityLimit** reflects the update.

**Acceptance Scenarios**:

1. **Given** S2.1: submission moves from SubmissionQueue to a ready-to-quote state., **When** SubmissionWithinAuthority is appended, **Then** **AuthorityLimit** reflects it

---

### User Story 2 - SubmissionReferred (Priority: P1)

As **AssessSubmission**, I want to submissionReferred so that **SubmissionReferred** is recorded.

**Narrative** (verbatim from the board export): S2.1-S2.4: checked against both gates before the event fires - Context 0 (authority) and Context 1e (freeze). Per S1a.5, proposedTerms is now an adjustment of the AI-generated BaselinePremiumGenerated baseline (fires PricingBaselineAccepted or PricingBaselineOverridden alongside), not pricing from scratch.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **AssessSubmission** under the right preconditions and asserting that **SubmissionReferred** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.2-S2.4: reused for every tier of a multi-level escalation, distinguished by parentReferralId., **When** AssessSubmission, **Then** SubmissionReferred

---

### User Story 3 - ReferralApproved (Priority: P1)

As **DecideReferral**, I want to referralApproved so that **ReferralApproved** is recorded.

**Narrative** (verbatim from the board export): S2.2/S2.3: resolving party approves, declines, or (if their own authority still doesn't cover it) implicitly triggers a further SubmissionReferred escalation.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **DecideReferral** under the right preconditions and asserting that **ReferralApproved** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.2: if termsModified, requires the original underwriter's acknowledgment (see ReferralTermsAcknowledged) before proceeding to quoting - they hold the broker relationship, so the referee doesn't unilaterally finalize terms the underwriter never actually offered., **When** DecideReferral, **Then** ReferralApproved

---

### User Story 4 - ReferralDeclined (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **ReferralDeclined** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): S2.3: dead end for these specific terms, by this specific referee, for this specific authority reason - distinct from SubmissionDeclined (off-appetite entirely). Loops back to a fresh AssessSubmission with revised terms, not a hard stop - same submissionId persists (a revised assessment of the same underlying risk, same pattern as PolicyEndorsed being a new ledger entry on the same policy rather than a new one).

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **ReferralDeclined** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.3: dead end for these specific terms, by this specific referee, for this specific authority reason - distinct from SubmissionDeclined (off-appetite entirely). Loops back to a fresh AssessSubmission with revised terms, not a hard stop - same submissionId persists (a revised assessment of the same underlying risk, same pattern as PolicyEndorsed being a new ledger entry on the same policy rather than a new one)., **When** the triggering condition occurs, **Then** ReferralDeclined

---

### User Story 5 - SubmissionDeclined (Priority: P1)

As **DeclineSubmission**, I want to submissionDeclined so that **SubmissionDeclined** is recorded.

**Narrative** (verbatim from the board export): Underwriter declines a submission outright - off-appetite class, sanctioned territory, or incomplete information beyond a reasonable follow-up window.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **DeclineSubmission** under the right preconditions and asserting that **SubmissionDeclined** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.6: at any point in assessment, doesn't require a referral attempt first. Sanctioned-territory declines trigger ComplianceNotifiedOfSanctionsDecline - a mandatory downstream notification given the regulatory seriousness versus an ordinary off-appetite decline., **When** DeclineSubmission, **Then** SubmissionDeclined

---

### User Story 6 - QuoteIssued (Priority: P1)

As **IssueQuote**, I want to quoteIssued so that **QuoteIssued** is recorded.

**Narrative** (verbatim from the board export): Once terms are within authority (directly or via approved referral), the underwriter issues a formal quote back to the broker. Not yet a bind.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **IssueQuote** under the right preconditions and asserting that **QuoteIssued** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.7: this is also the exact point S1e.2's freeze-block check applies - issuing a quote is the 'attempted action' intercepted if a freeze is active., **When** IssueQuote, **Then** QuoteIssued

---

### User Story 7 - QuoteAccepted (Priority: P1)

As **AcceptQuote**, I want to quoteAccepted so that **QuoteAccepted** is recorded.

**Narrative** (verbatim from the board export): Broker accepts the quote - this is what makes the submission eligible to bind. Modeled as its own event because a quote can also expire or be declined without binding.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **AcceptQuote** under the right preconditions and asserting that **QuoteAccepted** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** Submission is now eligible to bind., **When** AcceptQuote, **Then** QuoteAccepted

---

### User Story 8 - ReferralTermsAcknowledged (Priority: P1)

As **AcknowledgeReferralTerms**, I want to referralTermsAcknowledged so that **ReferralTermsAcknowledged** is recorded.

**Narrative** (verbatim from the board export): S2.2: original underwriter acknowledges referee-modified terms before the submission proceeds to quoting.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **AcknowledgeReferralTerms** under the right preconditions and asserting that **ReferralTermsAcknowledged** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** Required whenever ReferralApproved.termsModified is true - the underwriter, not the referee, has final say on what actually gets offered to the broker., **When** AcknowledgeReferralTerms, **Then** ReferralTermsAcknowledged

---

### User Story 9 - PendingReferralReassigned (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **PendingReferralReassigned** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): S2.5: fires when AuthorityLimitRevoked (or Revised) removes the pending referral's resolving party's ability to act on it - rerouted to whoever now holds equivalent authority, rather than sitting unresolved against someone who can no longer act. Produced by ReassessInFlightSubmissionsOnRuleChange (Context 0) - same mechanism, third trigger.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **PendingReferralReassigned** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.5: fires when AuthorityLimitRevoked (or Revised) removes the pending referral's resolving party's ability to act on it - rerouted to whoever now holds equivalent authority, rather than sitting unresolved against someone who can no longer act. Produced by ReassessInFlightSubmissionsOnRuleChange (Context 0) - same mechanism, third trigger., **When** the triggering condition occurs, **Then** PendingReferralReassigned

---

### User Story 10 - PendingReferralHeld (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **PendingReferralHeld** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): S2.5: flagged for governance attention when no automatic reroute can be safely inferred (e.g. no clear equivalent-authority holder).

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **PendingReferralHeld** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.5: flagged for governance attention when no automatic reroute can be safely inferred (e.g. no clear equivalent-authority holder)., **When** the triggering condition occurs, **Then** PendingReferralHeld

---

### User Story 11 - ComplianceNotifiedOfSanctionsDecline (Priority: P3)

As an alternate/terminal outcome recorded elsewhere in this context, the system records **ComplianceNotifiedOfSanctionsDecline** — this slice has no command or processor of its own in the source export; it documents a fact produced by a decision modeled in another slice.

**Narrative** (verbatim from the board export): S2.6: mandatory downstream notification specifically for declineReasonCode=sanctioned-territory, given the regulatory seriousness versus an ordinary off-appetite decline.

**Why this priority**: Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path.

**Independent Test**: Can be tested by asserting that, under the triggering condition described in the narrative above, **ComplianceNotifiedOfSanctionsDecline** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.6: mandatory downstream notification specifically for declineReasonCode=sanctioned-territory, given the regulatory seriousness versus an ordinary off-appetite decline., **When** the triggering condition occurs, **Then** ComplianceNotifiedOfSanctionsDecline

---

### User Story 12 - QuoteExpired (Priority: P2)

As a background policy in **Underwriting Decisioning**, the system reacts by executing **ExpireStaleQuotes**, producing **QuoteExpired**.

**Narrative** (verbatim from the board export): Scheduled/time-triggered policy, not a human command - runs when a quote's validity period elapses with no QuoteAccepted.

**Why this priority**: System-driven policy that keeps derived state (bordereaux, projections, notifications) consistent after the primary action(s) that trigger it.

**Independent Test**: Can be tested by invoking **ExpireStaleQuotes** under the right preconditions and asserting that **QuoteExpired** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.8: submission moves to a closed/lapsed state distinct from decline - no one said no, it just wasn't taken up., **When** ExpireStaleQuotes, **Then** QuoteExpired

---

### User Story 13 - QuoteDeclined (Priority: P1)

As **DeclineQuote**, I want to quoteDeclined so that **QuoteDeclined** is recorded.

**Narrative** (verbatim from the board export): Broker-initiated, active decline rather than passive expiry.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **DeclineQuote** under the right preconditions and asserting that **QuoteDeclined** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.9: kept distinct from QuoteExpired - an active decline (especially 'placed elsewhere') is genuinely useful competitive-intelligence data that passive expiry isn't., **When** DeclineQuote, **Then** QuoteDeclined

---

### User Story 14 - QuoteAmendmentRequested (Priority: P1)

As **RequestQuoteAmendment**, I want to quoteAmendmentRequested so that **QuoteAmendmentRequested** is recorded.

**Narrative** (verbatim from the board export): Broker-initiated.

**Why this priority**: Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed.

**Independent Test**: Can be tested by invoking **RequestQuoteAmendment** under the right preconditions and asserting that **QuoteAmendmentRequested** is the resulting domain event.

**Acceptance Scenarios**:

1. **Given** S2.10: triggers a fresh AssessSubmission pass against the amended terms - not a lightweight negotiation sub-flow. Amended terms could push the submission outside the underwriter's authority (e.g. higher line size requested), so it must go through the same Context 0/1e gate checks as any other assessment; a casual in-context negotiation would silently bypass both gates. Produces a fresh SubmissionWithinAuthority or SubmissionReferred, chained back to this event as prior context., **When** RequestQuoteAmendment, **Then** QuoteAmendmentRequested

---

### Edge Cases

- What happens when AssessSubmission cannot proceed on the happy path? System records **SubmissionReferred** instead — S2.2-S2.4: reused for every tier of a multi-level escalation, distinguished by parentReferralId..
- What happens under the condition described for **ReferralDeclined**? System records it as a standalone fact — S2.3: dead end for these specific terms, by this specific referee, for this specific authority reason - distinct from SubmissionDeclined (off-appetite entirely). Loops back to a fresh AssessSubmission with revised terms, not a hard stop - same submissionId persists (a revised assessment of the same underlying risk, same pattern as PolicyEndorsed being a new ledger entry on the same policy rather than a new one). (no command/processor of its own in the source export).
- What happens when DeclineSubmission cannot proceed on the happy path? System records **SubmissionDeclined** instead — S2.6: at any point in assessment, doesn't require a referral attempt first. Sanctioned-territory declines trigger ComplianceNotifiedOfSanctionsDecline - a mandatory downstream notification given the regulatory seriousness versus an ordinary off-appetite decline..
- What happens under the condition described for **PendingReferralReassigned**? System records it as a standalone fact — S2.5: fires when AuthorityLimitRevoked (or Revised) removes the pending referral's resolving party's ability to act on it - rerouted to whoever now holds equivalent authority, rather than sitting unresolved against someone who can no longer act. Produced by ReassessInFlightSubmissionsOnRuleChange (Context 0) - same mechanism, third trigger. (no command/processor of its own in the source export).
- What happens under the condition described for **PendingReferralHeld**? System records it as a standalone fact — S2.5: flagged for governance attention when no automatic reroute can be safely inferred (e.g. no clear equivalent-authority holder). (no command/processor of its own in the source export).
- What happens under the condition described for **ComplianceNotifiedOfSanctionsDecline**? System records it as a standalone fact — S2.6: mandatory downstream notification specifically for declineReasonCode=sanctioned-territory, given the regulatory seriousness versus an ordinary off-appetite decline. (no command/processor of its own in the source export).
- What happens when DeclineQuote cannot proceed on the happy path? System records **QuoteDeclined** instead — S2.9: kept distinct from QuoteExpired - an active decline (especially 'placed elsewhere') is genuinely useful competitive-intelligence data that passive expiry isn't..

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST project **AuthorityLimit** from the **SubmissionWithinAuthority** domain event(s).
- **FR-002**: System MUST support **AssessSubmission**, producing the **SubmissionReferred** domain event(s).
- **FR-003**: System MUST support **DecideReferral**, producing the **ReferralApproved** domain event(s).
- **FR-004**: System MUST record the **ReferralDeclined** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-005**: System MUST support **DeclineSubmission**, producing the **SubmissionDeclined** domain event(s).
- **FR-006**: System MUST support **IssueQuote**, producing the **QuoteIssued** domain event(s).
- **FR-007**: System MUST support **AcceptQuote**, producing the **QuoteAccepted** domain event(s).
- **FR-008**: System MUST support **AcknowledgeReferralTerms**, producing the **ReferralTermsAcknowledged** domain event(s).
- **FR-009**: System MUST record the **PendingReferralReassigned** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-010**: System MUST record the **PendingReferralHeld** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-011**: System MUST record the **ComplianceNotifiedOfSanctionsDecline** domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).
- **FR-012**: System MUST support **ExpireStaleQuotes**, producing the **QuoteExpired** domain event(s).
- **FR-013**: System MUST support **DeclineQuote**, producing the **QuoteDeclined** domain event(s).
- **FR-014**: System MUST support **RequestQuoteAmendment**, producing the **QuoteAmendmentRequested** domain event(s).

### Key Entities *(include if feature involves data)*

- **SubmissionAssessment**: aggregate in the **Underwriting Decisioning** context; touched by **AcknowledgeReferralTerms**, **AssessSubmission**, **ComplianceNotifiedOfSanctionsDecline**, **DecideReferral**, **DeclineSubmission**, **PendingReferralHeld**, **PendingReferralReassigned**, **ReferralApproved**, **ReferralDeclined**, **ReferralTermsAcknowledged**, **SubmissionDeclined**, **SubmissionReferred**, **SubmissionWithinAuthority**.
- **Quote**: aggregate in the **Underwriting Decisioning** context; touched by **AcceptQuote**, **DeclineQuote**, **ExpireStaleQuotes**, **IssueQuote**, **QuoteAccepted**, **QuoteAmendmentRequested**, **QuoteDeclined**, **QuoteExpired**, **QuoteIssued**, **RequestQuoteAmendment**.
- **AuthorityLimit** (read model): Projection of the 3-tier authority cascade (Capacity Provider -> Cell -> Underwriter) used to validate an assessment. Populated by the Authority Administration slices (grant/revise/revoke). Fields: `authorityLimitId`, `tier`, `providerId`, `cellId`, `underwriterId`, `classOfBusiness`, `maxGrossPremium`, `maxLimit`, `currency`, `status`, `grantedBy`, `grantedAt`.

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

- Derived from the "BrokerConnect" eventmodelers.ai board (chapter **Broker Connect**, context **Underwriting Decisioning**); command/event names, field lists, descriptions, aggregates, and lanes in the Event Model Detail section below are transcribed directly from a live pull of that board, not invented for this document.
- Pulled live on 2026-08-10 via the `slicedata` endpoint (see `event-model-to-speckit-guide.md`, "The real export endpoint"), not the raw event-replay log — re-run `event-model/build-scripts/gen_specs_from_slices.py` after any further board edits to keep this in sync; nothing here watches the board automatically.
- All 14 slice(s) in this context currently carry board status `Created` — none are `Planned` or built yet.
- The source board did not specify performance, scale, or business-metric assumptions for this context — the Success Criteria above are template placeholders.
- [Assumption about scope boundaries — confirm before /speckit.plan]

## Event Model Detail (Source of Truth)

Full, unabridged transcription of every command, automation/processor, event, and read model in this context from the static export, including every field's type, cardinality, and flags (`id` = identifier field, `generated` = system-generated, `optional` = nullable). Nested `List`/object fields show their subfields indented beneath them with `↳`.

### Slice: SubmissionWithinAuthority (`bee3ac36-fcd5-45d4-90df-19e1b67cfe11`, status: Created, type: STATE_VIEW)

**SubmissionWithinAuthority** (event, id `69882c6f-8ab4-4245-8718-4160df58ff61`, aggregate `SubmissionAssessment`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.1: submission moves from SubmissionQueue to a ready-to-quote state.

Dependencies: → AuthorityLimit (READMODEL); ← AssessSubmission (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `underwriterId` | String | Single | — |
| `proposedTerms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |
| `authorityVersionChecked` | Integer | Single | — |
| `freezeCheckPassed` | Boolean | Single | — |
| `decidedAt` | DateTime | Single | generated |

**AuthorityLimit** (read model, id `2f229a89-6bcf-4f54-bdf2-fc4ba11b4f04`, aggregate `AuthorityLimit`, lane `Interaction`, modelContext `Underwriting Decisioning`)

> Projection of the 3-tier authority cascade (Capacity Provider -> Cell -> Underwriter) used to validate an assessment. Populated by the Authority Administration slices (grant/revise/revoke).

Dependencies: → SubmissionAssessment (SCREEN); ← SubmissionWithinAuthority (EVENT)

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
| `grantedBy` | String | Single | optional |
| `grantedAt` | DateTime | Single | — |

### Slice: SubmissionReferred (`e4766299-5133-45ce-9d05-b01a2071fab6`, status: Created, type: STATE_CHANGE)

**AssessSubmission** (command, id `fed9664e-98a6-445c-8d59-3e8fca2823d8`, aggregate `SubmissionAssessment`, lane `Interaction`, modelContext `Underwriting Decisioning`)

> S2.1-S2.4: checked against both gates before the event fires - Context 0 (authority) and Context 1e (freeze). Per S1a.5, proposedTerms is now an adjustment of the AI-generated BaselinePremiumGenerated baseline (fires PricingBaselineAccepted or PricingBaselineOverridden alongside), not pricing from scratch.

Dependencies: → SubmissionReferred (EVENT); → SubmissionWithinAuthority (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | — |
| `underwriterId` | String | Single | — |
| `baselinePremiumReference` | UUID | Single | optional |
| `proposedTerms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |

**SubmissionReferred** (event, id `f597069b-ec96-4af6-a2b7-4b58ec069a4a`, aggregate `SubmissionAssessment`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.2-S2.4: reused for every tier of a multi-level escalation, distinguished by parentReferralId.

Dependencies: ← AssessSubmission (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `referralId` | UUID | Single | id, generated |
| `submissionId` | UUID | Single | — |
| `underwriterId` | String | Single | — |
| `proposedTerms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |
| `breachedDimension` | String | Single | — |
| `referredTo` | String | Single | — |
| `parentReferralId` | UUID | Single | optional |
| `referredAt` | DateTime | Single | generated |

### Slice: ReferralApproved (`e6079f6d-4612-4c02-ae2f-e1fa08af1009`, status: Created, type: STATE_CHANGE)

**DecideReferral** (command, id `e025428a-5770-4852-8d5f-4353f19a848f`, aggregate `SubmissionAssessment`, lane `Interaction`, modelContext `Underwriting Decisioning`)

> S2.2/S2.3: resolving party approves, declines, or (if their own authority still doesn't cover it) implicitly triggers a further SubmissionReferred escalation.

Dependencies: → ReferralApproved (EVENT); → ReferralDeclined (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `referralId` | UUID | Single | — |
| `resolvingPartyId` | String | Single | — |
| `decision` | String | Single | — |
| `modifiedTerms` | Custom | Single | optional |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |

**ReferralApproved** (event, id `0bf3f2ec-0b11-44be-8be5-80f941beb6a2`, aggregate `SubmissionAssessment`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.2: if termsModified, requires the original underwriter's acknowledgment (see ReferralTermsAcknowledged) before proceeding to quoting - they hold the broker relationship, so the referee doesn't unilaterally finalize terms the underwriter never actually offered.

Dependencies: ← DecideReferral (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `referralId` | UUID | Single | id |
| `resolvingPartyId` | String | Single | — |
| `approvedTerms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |
| `termsModified` | Boolean | Single | — |
| `decidedAt` | DateTime | Single | generated |

### Slice: ReferralDeclined (`d967263d-6885-46b5-8d2b-a5a607429301`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**ReferralDeclined** (event, id `0e73f5a2-dbd4-45ad-8ab4-0bba31b50cb2`, aggregate `SubmissionAssessment`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.3: dead end for these specific terms, by this specific referee, for this specific authority reason - distinct from SubmissionDeclined (off-appetite entirely). Loops back to a fresh AssessSubmission with revised terms, not a hard stop - same submissionId persists (a revised assessment of the same underlying risk, same pattern as PolicyEndorsed being a new ledger entry on the same policy rather than a new one).

Dependencies: ← DecideReferral (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `referralId` | UUID | Single | id |
| `resolvingPartyId` | String | Single | — |
| `declineReason` | String | Single | — |
| `decidedAt` | DateTime | Single | generated |

### Slice: SubmissionDeclined (`2eda6cdd-9b8c-4470-8dd5-e73fa12238ea`, status: Created, type: STATE_CHANGE)

**DeclineSubmission** (command, id `edd0689d-f9eb-42d7-a081-3c9f1397fc1b`, aggregate `SubmissionAssessment`, lane `Interaction`, modelContext `Underwriting Decisioning`)

> Underwriter declines a submission outright - off-appetite class, sanctioned territory, or incomplete information beyond a reasonable follow-up window.

Dependencies: → SubmissionDeclined (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `underwriterId` | String | Single | — |
| `reason` | String | Single | — |

**SubmissionDeclined** (event, id `e0262010-7666-4bfd-bd18-c7aaede3a760`, aggregate `SubmissionAssessment`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.6: at any point in assessment, doesn't require a referral attempt first. Sanctioned-territory declines trigger ComplianceNotifiedOfSanctionsDecline - a mandatory downstream notification given the regulatory seriousness versus an ordinary off-appetite decline.

Dependencies: ← DeclineSubmission (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `underwriterId` | String | Single | — |
| `declineReasonCode` | String | Single | — |
| `declineReasonDetail` | String | Single | optional |
| `declinedAt` | DateTime | Single | generated |

### Slice: QuoteIssued (`d83e5c49-a4ef-4d72-85d0-8c84fa1dcd16`, status: Created, type: STATE_CHANGE)

**IssueQuote** (command, id `9074bb54-8297-4170-a4fd-9af167de2fa7`, aggregate `Quote`, lane `Interaction`, modelContext `Underwriting Decisioning`)

> Once terms are within authority (directly or via approved referral), the underwriter issues a formal quote back to the broker. Not yet a bind.

Dependencies: → QuoteIssued (EVENT); ← QuoteView (SCREEN)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `grossPremium` | Decimal | Single | — |
| `limit` | Decimal | Single | — |
| `terms` | String | Single | — |
| `validUntil` | Date | Single | — |

**QuoteIssued** (event, id `09d9affa-11a5-4478-a4df-313e2ab535d0`, aggregate `Quote`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.7: this is also the exact point S1e.2's freeze-block check applies - issuing a quote is the 'attempted action' intercepted if a freeze is active.

Dependencies: ← IssueQuote (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `quoteId` | UUID | Single | id, generated |
| `submissionId` | UUID | Single | — |
| `terms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |
| `validityPeriodDays` | Integer | Single | — |
| `expiryDate` | Date | Single | generated |
| `issuedBy` | String | Single | — |
| `issuedAt` | DateTime | Single | generated |

### Slice: QuoteAccepted (`ab5fdad8-bb29-418d-beb5-57b8f6214dc1`, status: Created, type: STATE_CHANGE)

**AcceptQuote** (command, id `c2a3859a-1e48-41d9-8a39-f25b953dc74e`, aggregate `Quote`, lane `Interaction`, modelContext `Underwriting Decisioning`)

> Broker accepts the quote - this is what makes the submission eligible to bind. Modeled as its own event because a quote can also expire or be declined without binding.

Dependencies: → QuoteAccepted (EVENT); ← QuoteView (SCREEN)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | id |
| `acceptedAt` | DateTime | Single | optional |

**QuoteAccepted** (event, id `30962c6e-fba0-4ad5-ac13-323a6d455e79`, aggregate `Quote`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> Submission is now eligible to bind.

Dependencies: ← AcceptQuote (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `quoteId` | UUID | Single | id |
| `acceptedTerms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |
| `brokerContact` | String | Single | — |
| `acceptedAt` | DateTime | Single | generated |

### Slice: ReferralTermsAcknowledged (`9e0c87e6-42e7-4a56-bab2-c0fa81350deb`, status: Created, type: STATE_CHANGE)

**AcknowledgeReferralTerms** (command, id `6cead284-b3b1-4fad-b342-ddb1ea8a9bc9`, aggregate `SubmissionAssessment`, lane `Interaction`, modelContext `Underwriting Decisioning`)

> S2.2: original underwriter acknowledges referee-modified terms before the submission proceeds to quoting.

Dependencies: → ReferralTermsAcknowledged (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `referralId` | UUID | Single | — |
| `submissionId` | UUID | Single | — |
| `underwriterId` | String | Single | — |

**ReferralTermsAcknowledged** (event, id `5976f02d-da95-4d62-b511-7521d6924684`, aggregate `SubmissionAssessment`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> Required whenever ReferralApproved.termsModified is true - the underwriter, not the referee, has final say on what actually gets offered to the broker.

Dependencies: ← AcknowledgeReferralTerms (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `referralId` | UUID | Single | id |
| `underwriterId` | String | Single | — |
| `acknowledgedAt` | DateTime | Single | generated |

### Slice: PendingReferralReassigned (`60066237-b32b-43f5-ae40-9fa3d26d6237`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**PendingReferralReassigned** (event, id `78991795-e97a-473b-ba7e-ab9ce2fa349d`, aggregate `SubmissionAssessment`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.5: fires when AuthorityLimitRevoked (or Revised) removes the pending referral's resolving party's ability to act on it - rerouted to whoever now holds equivalent authority, rather than sitting unresolved against someone who can no longer act. Produced by ReassessInFlightSubmissionsOnRuleChange (Context 0) - same mechanism, third trigger.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `referralId` | UUID | Single | id |
| `previousResolvingParty` | String | Single | — |
| `newResolvingParty` | String | Single | — |
| `reassignedAt` | DateTime | Single | generated |

### Slice: PendingReferralHeld (`f3e6ec95-113a-4525-bebf-f46cd9ce942a`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**PendingReferralHeld** (event, id `929a0bce-4e51-4a2b-8812-06d0b93af136`, aggregate `SubmissionAssessment`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.5: flagged for governance attention when no automatic reroute can be safely inferred (e.g. no clear equivalent-authority holder).

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `referralId` | UUID | Single | id |
| `reason` | String | Single | — |
| `heldAt` | DateTime | Single | generated |

### Slice: ComplianceNotifiedOfSanctionsDecline (`d8de5239-9507-4338-9830-2935c7e7a4be`, status: Created, type: FACT_ONLY (inferred — sliceType missing in source export))

**ComplianceNotifiedOfSanctionsDecline** (event, id `327597ac-c41a-43a7-ab19-7dd76ca82160`, aggregate `SubmissionAssessment`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.6: mandatory downstream notification specifically for declineReasonCode=sanctioned-territory, given the regulatory seriousness versus an ordinary off-appetite decline.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `submissionId` | UUID | Single | — |
| `declineReasonDetail` | String | Single | — |
| `notifiedAt` | DateTime | Single | generated |

### Slice: QuoteExpired (`89d450b4-b525-4f2b-9daf-490ca81e7da9`, status: Created, type: AUTOMATION)

**ExpireStaleQuotes** (automation/processor, id `d1aad284-c596-4945-8f92-ec1601ff20f1`, aggregate `Quote`, lane `Actor`, modelContext `Underwriting Decisioning`)

> Scheduled/time-triggered policy, not a human command - runs when a quote's validity period elapses with no QuoteAccepted.

_(no fields)_

**QuoteExpired** (event, id `78dfec6d-f4cb-456f-9b4b-ed5d9e797ec2`, aggregate `Quote`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.8: submission moves to a closed/lapsed state distinct from decline - no one said no, it just wasn't taken up.

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `quoteId` | UUID | Single | id |
| `terms` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |
| `expiredAt` | DateTime | Single | generated |

### Slice: QuoteDeclined (`77190303-8428-42ce-b298-3ea38903e3ce`, status: Created, type: STATE_CHANGE)

**DeclineQuote** (command, id `7f8d4fd3-e5ec-4d4f-8708-34e32b66f2fd`, aggregate `Quote`, lane `Interaction`, modelContext `Underwriting Decisioning`)

> Broker-initiated, active decline rather than passive expiry.

Dependencies: → QuoteDeclined (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `quoteId` | UUID | Single | — |
| `declineReason` | String | Single | optional |

**QuoteDeclined** (event, id `68b016ac-bfab-443c-950f-bfaa0c38ef57`, aggregate `Quote`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.9: kept distinct from QuoteExpired - an active decline (especially 'placed elsewhere') is genuinely useful competitive-intelligence data that passive expiry isn't.

Dependencies: ← DeclineQuote (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `quoteId` | UUID | Single | id |
| `declineReason` | String | Single | optional |
| `declinedAt` | DateTime | Single | generated |

### Slice: QuoteAmendmentRequested (`ff3b9020-82a4-44a6-ade3-56ce7997c402`, status: Created, type: STATE_CHANGE)

**RequestQuoteAmendment** (command, id `c28c6f1e-c38e-46ca-821d-571382b527d4`, aggregate `Quote`, lane `Interaction`, modelContext `Underwriting Decisioning`)

> Broker-initiated.

Dependencies: → QuoteAmendmentRequested (EVENT)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `quoteId` | UUID | Single | — |
| `requestedChanges` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |
| `brokerContact` | String | Single | — |

**QuoteAmendmentRequested** (event, id `c6038043-0101-4f51-8c22-2b58002a6f59`, aggregate `Quote`, lane `Submission Decision`, modelContext `Underwriting Decisioning`)

> S2.10: triggers a fresh AssessSubmission pass against the amended terms - not a lightweight negotiation sub-flow. Amended terms could push the submission outside the underwriter's authority (e.g. higher line size requested), so it must go through the same Context 0/1e gate checks as any other assessment; a casual in-context negotiation would silently bypass both gates. Produces a fresh SubmissionWithinAuthority or SubmissionReferred, chained back to this event as prior context.

Dependencies: ← RequestQuoteAmendment (COMMAND)

| Field | Type | Cardinality | Flags |
|---|---|---|---|
| `quoteId` | UUID | Single | — |
| `submissionId` | UUID | Single | — |
| `requestedChanges` | Custom | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `lineSize` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `pricing` | Decimal | Single | — |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ `conditions` | String | Single | optional |
| `brokerContact` | String | Single | — |
| `requestedAt` | DateTime | Single | generated |

