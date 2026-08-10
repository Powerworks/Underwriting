# Context 1b — Search & Retrieval: Functional Requirements

**Status:** Confirmed. **Board chapter:** `Search & Retrieval`.

## Overview

Query-side capability over the event stream everything else produces. Command/event pairs capture search *activity* for audit/usage purposes; the read model is the actual point of the context. Requires at least two permission tiers from day one: cell-scoped (underwriters) and cross-cell (ops/governance).

## Actors

Underwriter, claims handler, operations, capacity provider (indirectly, via ops reconciliation).

## Functional Requirements

### FR-SR-001: Search historic submissions by risk attribute
**Source:** S1b.1 — `SearchSubmissionHistory` → `SubmissionHistorySearchPerformed`
The system shall allow an underwriter to search historic submissions by class, territory, broker, date range, and free text, scoped by default to their own cell's authority context.

### FR-SR-002: Record search activity for audit
**Source:** S1b.1 — `SubmissionHistorySearchPerformed`
The system shall record each search's criteria and result count (not the result set itself) for audit purposes.

### FR-SR-003: Present ranked search results
**Source:** S1b.1 — Read Model `SubmissionSearchResults`
The system shall present matching historic submissions, each linking back to the full submission record including downstream decisioning/bind/claims history where applicable.

### FR-SR-004: Automatically retrieve submission lineage on claim notification
**Source:** S1b.2 — `RetrieveSubmissionLineageOnClaimNotified` → `SubmissionLineageRetrieved`
The system shall, automatically upon `ClaimNotified` (Context 5), retrieve and record the full submission-to-bind lineage for the claimed policy, without requiring the claims handler to construct a search query.

### FR-SR-005: Present the policy origination view
**Source:** S1b.2 — Read Model `PolicyOriginationView`
The system shall present, given a bound policy reference, the full chain from original submission through decisioning path, quote, and bind terms. This view feeds claim payment validation (FR-CLM-003).

### FR-SR-006: Support cross-cell broker reconciliation search
**Source:** S1b.3 — `SearchSubmissionHistory` (scoped by broker, cross-cell)
The system shall allow operations to search a broker's submission history across all cells the broker is authorized for, for reconciliation purposes.

### FR-SR-007: Present broker activity with cross-cell status breakdown
**Source:** S1b.3 — Read Model `BrokerActivityView`
The system shall present, for a given broker, a status breakdown (bound/declined/expired/in-flight) per cell across all cells the broker is authorized for.

### FR-SR-008: Enforce and log the cell-scoped vs. cross-cell permission boundary
**Source:** S1b.4 — `CrossCellSearchAttempted`
The system shall default underwriter search visibility to the searcher's own cell, and treat cross-cell visibility as an explicit, logged, elevated permission for operations/governance roles. Every cross-cell search attempt shall be recorded with its permitted/denied outcome, kept visible for whoever manages access policy.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-012 (query-level vs. per-keystroke search logging), DEC-013 (free-text scope: structured fields only vs. raw documents), DEC-014 (narrower same-named-insured cross-cell flag for underwriters).
