# Context 3 — Binding: Functional Requirements

**Status:** Confirmed. **Board chapter:** `Binding`.

## Overview

"The immutable ledger" — once a policy is bound, it is the anchor everything downstream (Bordereaux Settlement, Claims, and exploratory Exposure Intelligence) keys off. Endorsements and cancellations append to history; they never rewrite the original bind record.

## Actors

Underwriter, broker (requests changes/cancellation), capacity provider (quota-share allocation).

## Functional Requirements

### FR-BND-001: Bind a policy with explicit underwriter confirmation
**Source:** S3.1/S3.2 — `BindPolicy` → `PolicyBound`
The system shall require an explicit underwriter confirmation to bind a policy, distinct from and subsequent to broker quote acceptance — the two are conceptually different moments (documentation checks, subjectivities being cleared) and shall never be collapsed into one automatic action.

### FR-BND-002: Model capacity provider allocation as a list, always
**Source:** S3.1/S3.2 — `PolicyBound`
The system shall record capacity provider allocation as a list of (provider, quota share %) pairs, even when a single provider holds 100%, so single- and multi-provider binds share one shape. Allocations shall be validated to sum to exactly 100%; the system shall not trust unvalidated allocation data at entry.

### FR-BND-003: Maintain a canonical policy register
**Source:** S3.1 — Read Model `PolicyRegister`
The system shall provide the canonical current-state view of every bound policy.

### FR-BND-004: Maintain an active book of business per cell
**Source:** S3.1 — Read Model `ActiveBookOfBusiness`
The system shall provide a per-cell view of currently active bound business.

### FR-BND-005: Process administrative endorsements directly
**Source:** S3.3 — `EndorsePolicy` → `PolicyEndorsed`
The system shall process a policy endorsement whose change does not affect premium, limit, or exposure directly, recording the change as a delta from current terms (not a full restatement).

### FR-BND-006: Route material endorsements through the authority/referral gate
**Source:** S3.3 — `EndorsementReferred`
The system shall route an endorsement whose change affects premium, limit, or exposure through the same authority/referral check as an original bind (reusing `DecideReferral`/`ReferralApproved`/`ReferralDeclined`, FR-UD-004), rather than allowing it to bypass governance as a lightweight edit. The material-vs-administrative classification shall be an explicit, rule-based determination (keyed to whether the change affects premium/limit/exposure), not left to underwriter judgment case by case.

### FR-BND-007: Process routine cancellations
**Source:** S3.4 — `CancelPolicy` → `PolicyCancelled`
The system shall process a routine (broker-requested or underwriter-initiated) cancellation, recording whether pro-rata or short-rate return premium applies. Return premium calculation itself shall feed Bordereaux Settlement, not be computed here.

### FR-BND-008: Distinguish for-cause cancellations
**Source:** S3.4 — `PolicyCancelledForCause`
The system shall record an underwriter-for-cause cancellation (e.g. material misrepresentation discovered post-bind) as a distinct event from a routine cancellation, given its compliance/dispute implications.

### FR-BND-009: Initiate renewal as a fresh submission cycle
**Source:** S3.5 — `InitiateRenewal` → `PolicyRenewalInitiated`
The system shall initiate renewal by creating a genuinely new submission that references the expiring policy as prior history and re-enters Submission Intake/Underwriting Decisioning in full — expiring terms shall never grandfather authority, risk appetite, or freeze status. There shall be no "renewal fast-track" shortcut that bypasses fresh assessment.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-001 (per-provider authority lineage on bind, linked to Context 0), DEC-016 (renewal exposure/PML context carry-forward), DEC-017 (renewal grace period vs. lapse if broker doesn't respond before expiry).
