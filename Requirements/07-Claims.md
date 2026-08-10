# Context 5 — Claims: Functional Requirements

**Status:** Confirmed. **Board chapter:** `Claims`.

## Overview

The last confirmed context, closing the loop back to nearly everything before it — a claim references the original bind (and transitively the whole submission/decisioning lineage), and its provider split must mirror the quota share established at bind time.

## Actors

Claims handler, broker/insured (notification origin).

## Functional Requirements

### FR-CLM-001: Notify and register a claim
**Source:** S5.1 — `NotifyClaim` → `ClaimNotified`
The system shall record a claim notification referencing the original bound policy, making the full `PolicyOriginationView` (FR-SR-005) available to the claims handler automatically (FR-SR-004).

### FR-CLM-002: Set and revise claim reserves
**Source:** S5.1/S5.2 — `SetClaimReserve` → `ClaimReserveSet`
The system shall allow a claim reserve to be set and revised any number of times, recording each revision as a new sequenced entry so the full reserve history is visible, not a single mutable field.

### FR-CLM-003: Validate claim payment against original bind terms
**Source:** S5.1 — `PayClaim` → `ClaimPaid`
The system shall validate a claim payment against the policy's limits, sub-limits, and deductibles from the original bind terms as an explicit, visible step, not merely trusted to the claims handler's manual check.

### FR-CLM-004: Split claim payment by bind-time quota share
**Source:** S5.1/S5.3 — `ClaimPaid`
The system shall derive the provider allocation of a claim payment automatically from the quota share split recorded in the original `PolicyBound` event, not re-entered manually.

### FR-CLM-005: Close a claim
**Source:** S5.1 — `CloseClaim` → `ClaimClosed`
The system shall allow a claims handler to close a claim, recording the closure reason and final total paid.

### FR-CLM-006: Route material reserve revisions through the authority engine
**Source:** S5.2 — `ReserveRevisionReferred`
The system shall route a reserve increase that crosses the claims handler's own authority threshold through the same referral resolution mechanism used elsewhere in this model (FR-UD-004), rather than treating claims as outside the governance model established in Context 0.

### FR-CLM-007: Reopen a closed claim without erasing history
**Source:** S5.4 — `ReopenClaim` → `ClaimReopened`
The system shall allow a closed claim to be reopened on new information, appending the reopening to the claim's history rather than erasing the prior closure — the same append-only principle applied to Bordereaux corrections and Binding endorsements.

### FR-CLM-008: Flag (not reject) claims against inactive policies
**Source:** S5.5 — `ClaimNotifiedAgainstInactivePolicy`
The system shall flag, rather than automatically reject, a claim notified against a policy whose current status is cancelled or lapsed, since the loss may have occurred while cover was still active.

### FR-CLM-009: Require human validation of claim period
**Source:** S5.5 — `ClaimPeriodValidated`
The system shall require an explicit human confirmation that a flagged claim's date of loss falls within the policy's period of cover before reserve-setting or payment proceeds — this determination shall never be automatically decided, given its coverage-dispute implications.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-019 (post-bind quota-share mutability — novation/restructure), DEC-020 (reopen-count limit or escalation pattern detection).
