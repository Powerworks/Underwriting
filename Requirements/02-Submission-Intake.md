# Context 1a — Submission Intake: Functional Requirements

**Status:** Confirmed. **Board chapter:** `Submission Intake`.

## Overview

Broker submission received and normalized to the ACORD ADEPT standard schema — the same schema used for automated data exchange across global broker systems generally, not a Howden-specific format. Includes AI-generated baseline pricing, added after TFP's own published pipeline description confirmed pricing isn't fully deferred (see FR-SI-009 onward).

## Actors

Broker (via Howden or other broker system), Broker Connect's own ingestion service, external rating engine, underwriter, operations.

## Functional Requirements

### FR-SI-001: Receive and record a broker submission
**Source:** S1a.1 — `ReceiveBrokerSubmission` → `BrokerSubmissionReceived`
The system shall accept a broker submission (broker firm ID, submitting contact, raw payload, source channel) and record its receipt as an immutable fact regardless of what downstream processing later finds. The raw payload shall be stored as-is and never discarded, mapped to the ACORD ADEPT schema.

### FR-SI-002: Normalize a received submission
**Source:** S1a.1 — `SubmissionNormalized`
The system shall extract structured fields (class of business, territory, line size sought, named insured, effective date requested) from a successfully-normalized submission.

### FR-SI-003: Surface ready submissions to underwriters
**Source:** S1a.1 — Read Model `SubmissionQueue`
The system shall present normalized submissions to underwriters in a queryable worklist with status "Ready for review."

### FR-SI-004: Record normalization failure without discarding data
**Source:** S1a.2 — `SubmissionNormalizationFailed`
The system shall record a normalization failure with a specific reason (missing required field, malformed data, unrecognized class code, schema validation error) while preserving the raw payload, and route the submission to an exception queue (FR-SI-005) separate from the underwriter's main queue.

### FR-SI-005: Provide an operations exception queue
**Source:** S1a.2 — Read Model `SubmissionExceptionQueue`
The system shall provide operations staff a queue of submissions stuck in a failed-normalization state, for broker follow-up or manual correction.

### FR-SI-006: Support manual correction and re-normalization
**Source:** S1a.2 — `SubmissionManuallyCorrected`
The system shall allow operations or the broker to submit a correction to a failed submission, re-triggering normalization.

### FR-SI-007: Reject submissions outside a broker's cell/class authorization
**Source:** S1a.3 — `SubmissionRoutingRejected`
The system shall record, rather than silently drop, a submission that targets a cell/class the broker isn't authorized to place into, and route the rejection to a broker-authorization exception log (FR-SI-008) rather than any underwriter queue.

### FR-SI-008: Provide a broker-authorization exception log
**Source:** S1a.3 — Read Model `BrokerAuthorizationExceptionLog`
The system shall provide a view, for whoever manages broker/cell panel relationships, of submissions rejected for lack of broker authorization.

### FR-SI-009: Detect potential duplicate submissions
**Source:** S1a.4 — `PotentialDuplicateSubmissionDetected`
The system shall always record a new submission regardless of suspected duplication, and separately flag it as a possible duplicate of an existing open submission with a stated match basis, surfaced as a badge on the underwriter's normal queue (not a separate queue) so resolution uses underwriter domain judgment.

### FR-SI-010: Support duplicate resolution
**Source:** S1a.4 — `SubmissionSuperseded` / `SubmissionConfirmedDistinct`
The system shall allow an underwriter or operations to resolve a flagged duplicate either by linking the new submission as superseding the original (preserving both records) or by confirming the submissions are genuinely distinct.

### FR-SI-011: Generate an AI baseline premium after successful normalization
**Source:** S1a.5 — `BaselinePremiumGenerated`
The system shall, on every successful `SubmissionNormalized` event, request a baseline premium from the external rating engine and record it along with a summary of contributing risk factors and the pricing model version used. Normalization failures shall not trigger a baseline pricing attempt.

### FR-SI-012: Present the broker's ask alongside the AI baseline
**Source:** S1a.5 — Read Model `PricedSubmissionView`
The system shall present the broker's requested terms alongside the AI-generated baseline premium before the underwriter opens the file for assessment.

### FR-SI-013: Track underwriter deviation from the AI baseline
**Source:** S1a.5 — `PricingBaselineAccepted` / `PricingBaselineOverridden`
The system shall record, at the point of underwriting assessment, whether the underwriter's proposed terms match the AI baseline exactly or diverge from it (with the signed variance), to support model-performance evaluation.

### FR-SI-014: Track pricing model version deployment
**Source:** S1a.5 follow-on — `PricingModelVersionDeployed`
The system shall record when a new pricing model version is deployed by the external rating engine, as reference data only — the system does not govern the model's content, only records which version priced which submission.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-006 (sync vs. async normalization), DEC-007 (broker notification on normalization failure), DEC-008 (retry/abandonment timeout), DEC-009 (hard vs. soft routing rejection, broker feedback), DEC-010 (duplicate-match heuristic), DEC-011 (duplicate resolution: supersede vs. version chain).
