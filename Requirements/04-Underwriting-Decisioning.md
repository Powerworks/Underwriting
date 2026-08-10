# Context 2 — Underwriting Decisioning: Functional Requirements

**Status:** Confirmed. **Board chapter:** `Underwriting Decisioning`.

## Overview

The branchiest context, and the payoff for Authority Administration (Context 0) and Portfolio Governance (Context 1e, exploratory) both being fully specified first. Every entry point into this context — the happy path, the referral cascade, and the amendment loop — passes through the same two-gate check.

## Actors

Underwriter, senior underwriter / Head of Class (referral resolution), broker.

## Functional Requirements

### FR-UD-001: Assess a submission against both governance gates
**Source:** S2.1 — `AssessSubmission`
The system shall check a proposed assessment against both the underwriter's current delegated authority (Context 0) and, where the exploratory extension is in scope, any active portfolio freeze (Context 1e) before recording an outcome.

### FR-UD-002: Record a within-authority decision with full audit reference
**Source:** S2.1 — `SubmissionWithinAuthority`
The system shall, when an assessment is within authority, record the specific authority register version checked against (not merely that a check occurred) and explicitly record that the freeze check ran and passed — not merely that the submission wasn't blocked.

### FR-UD-003: Refer a submission that exceeds the underwriter's authority
**Source:** S2.2/S2.4 — `SubmissionReferred`
The system shall refer a submission whose proposed terms breach the underwriter's authority on any dimension (line size, class, territory) to the next tier determined by the authority cascade, recording which dimension was breached. Referrals shall chain via a parent-referral reference to form a visible multi-tier escalation, terminating at the cell's own overall authority limit rather than escalating indefinitely.

### FR-UD-004: Resolve a referral
**Source:** S2.2/S2.3 — `DecideReferral` → `ReferralApproved` / `ReferralDeclined`
The system shall allow the resolving tier to approve (with optionally modified terms) or decline a referred submission.

### FR-UD-005: Require original-underwriter acknowledgment of modified referral terms
**Source:** S2.2 — `ReferralTermsAcknowledged`
The system shall require the original underwriter to acknowledge referee-modified terms before a referral-approved submission proceeds to quoting, since the underwriter — not the referee — holds the broker relationship.

### FR-UD-006: Allow resubmission after a declined referral
**Source:** S2.3 — `ReferralDeclined`
The system shall allow the underwriter to resubmit revised terms after a referral decline as a fresh assessment against the same submission ID, not requiring a new submission.

### FR-UD-007: Reassign or hold a referral when the referee's authority changes mid-flight
**Source:** S2.5 — `PendingReferralReassigned` / `PendingReferralHeld`
The system shall, when a pending referral's resolving party has their authority revised or revoked before resolution, either automatically reassign the referral to whoever now holds equivalent authority, or flag it for governance attention if no safe automatic reroute can be inferred. This shall be treated as required functionality, not deferrable.

### FR-UD-008: Decline a submission outright with structured reason
**Source:** S2.6 — `DeclineSubmission` → `SubmissionDeclined`
The system shall allow an underwriter to decline a submission at any point in assessment, without requiring a prior referral attempt, recording a structured (not free-text-only) decline reason code.

### FR-UD-009: Notify compliance of sanctions-related declines
**Source:** S2.6 — `ComplianceNotifiedOfSanctionsDecline`
The system shall generate a mandatory compliance notification whenever a submission is declined for `sanctioned-territory`, distinct from ordinary off-appetite declines.

### FR-UD-010: Issue and accept quotes
**Source:** S2.7 — `IssueQuote` → `QuoteIssued`, `AcceptQuote` → `QuoteAccepted`
The system shall allow an underwriter to issue a quote with a validity period, and a broker to accept it within that window, making the submission eligible to proceed to bind. Quote issuance is the point at which a Portfolio Governance freeze check (FR-PG-002) applies.

### FR-UD-011: Expire stale quotes
**Source:** S2.8 — `ExpireStaleQuotes` → `QuoteExpired`
The system shall automatically expire a quote whose validity period elapses without acceptance, moving the submission to a closed/lapsed state distinct from an active decline.

### FR-UD-012: Support explicit broker quote decline
**Source:** S2.9 — `DeclineQuote` → `QuoteDeclined`
The system shall allow a broker to actively decline an outstanding quote with an optional reason, kept distinct from passive expiry for competitive-intelligence purposes.

### FR-UD-013: Route quote amendment requests through a fresh assessment
**Source:** S2.10 — `RequestQuoteAmendment` → `QuoteAmendmentRequested`
The system shall treat a broker's request to amend issued-quote terms as triggering a fresh `AssessSubmission` pass against the amended terms, chained back to the original quote/submission as prior context, rather than a lightweight negotiation exempt from the authority/freeze gate checks.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-015 (referral resolution: does a modified-terms referral always need underwriter acknowledgment, or only above some materiality threshold).
