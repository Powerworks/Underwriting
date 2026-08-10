# Context 4 — Bordereaux Settlement: Functional Requirements

**Status:** Confirmed. **Board chapter:** `Bordereaux Settlement`.

## Overview

Meaningfully different in shape from everything before it — Contexts 0–3 are triggered by discrete business events; this context is triggered by the calendar, sweeping up a batch of prior transactions on a period-close schedule, strictly scoped to one cell + one capacity provider + one period.

## Actors

System (scheduled process, drafts), operations/finance, capacity provider (or TPA).

## Functional Requirements

### FR-BS-001: Draft a bordereau on period close
**Source:** S4.1 — `DraftBordereauOnPeriodClose` → `BordereauDrafted`
The system shall, on a scheduled period-close trigger, sweep every unbilled transaction (bind, endorsement, cancellation) for a given cell + provider + period into a draft bordereau. Each swept transaction shall be assigned its own generated line identifier at draft time (fixed 2026-08 — see `15-Completeness-Check.md` GAP-001: no event previously assigned individual line IDs, even though line-level query/dispute/adjustment requirements throughout this context depend on one existing).

### FR-BS-002: Track which transactions have been billed
**Source:** S4.1 — Read Model `UnbilledTransactionsForPeriod`
The system shall maintain an explicit link from each policy transaction to at most one bordereau (or none, if not yet swept), as a queryable fact rather than an assumption.

### FR-BS-003: Require two-party agreement before settlement
**Source:** S4.1 — `BordereauSubmittedForAgreement` → `AgreeBordereau` → `BordereauAgreed`
The system shall require both TFP's internal sign-off and the capacity provider's own confirmation before a bordereau is considered agreed — bordereaux are the mechanism by which money moves to/from providers and shall not rely on TFP's internal view alone.

### FR-BS-004: Settle an agreed bordereau
**Source:** S4.1 — `SettleBordereau` → `BordereauSettled`
The system shall record settlement (amount, date, payment reference) only for a bordereau that has been agreed.

### FR-BS-005: Support line-level query and resolution
**Source:** S4.2 — `RaiseBordereauQuery` → `BordereauLineQueried`, `ResolveBordereauQuery` → `BordereauLineResolved`
The system shall allow a capacity provider to query a specific bordereau line and operations to resolve it (confirmed as-is, or corrected).

### FR-BS-006: Defer unresolved lines rather than block settlement
**Source:** S4.3 — `DeferQueriedLineAtPeriodClose` → `BordereauLineDeferred`
The system shall, when a queried line remains unresolved as the period's processing timeline elapses, exclude that specific line from the current bordereau and carry it into the next period's draft, rather than blocking the whole bordereau's settlement. A bordereau shall never be agreed (FR-BS-003) while a genuinely unresolved query remains attached — every open query is resolved (FR-BS-005) or deferred (this requirement) before agreement.

### FR-BS-007: Raise post-agreement corrections as new lines, never edits
**Source:** S4.4 — `RaiseAdjustmentLine` → `AdjustmentLineRaised`
The system shall record a correction to an already-agreed bordereau as a new line targeting a future settlement period, referencing the original bordereau/line for traceability. The original historical bordereau shall remain untouched, consistent with the immutable-ledger principle applied throughout this model.

### FR-BS-008: Scope every bordereau to exactly one cell + provider + period
**Source:** S4.5
The system shall never span a bordereau across multiple capacity providers. A multi-provider bind's allocated share (Context 3, FR-BND-002) shall contribute independently to each provider's own bordereau, matching how providers expect to be settled with (their own statement, not a shared one).

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-018 (materiality threshold for adjustment-line separate sign-off vs. riding along in the normal cycle).
