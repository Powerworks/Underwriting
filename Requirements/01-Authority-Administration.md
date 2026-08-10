# Context 0 — Authority Administration: Functional Requirements

**Status:** Confirmed. **Board chapter:** `Authority Administration`.

## Overview

Governance/reference data underpinning the Provider → Cell → Underwriter authority cascade that Underwriting Decisioning checks against. Reference/configuration data, not transactional flow. This context is a **cross-cutting capability** — the same escalation mechanism it establishes is reused by Binding (material endorsements) and Claims (reserve revisions); see NFR-002.

## Actors

Underwriting Governance function, senior executive, Head of Class / Cell CUO.

## Functional Requirements

### FR-AA-001: Grant a cell's authority limit
**Source:** S0.1 — `GrantCellAuthorityLimit` → `CellAuthorityLimitGranted`
The system shall allow Underwriting Governance to grant a cell an authority limit scoped by class(es) of business, territory, max line size, and max aggregate, referencing the source capacity provider agreement. Each grant shall be versioned from its first record, not versioned retroactively.

### FR-AA-002: Maintain a queryable cell authority register
**Source:** S0.1 — Read Model `CellAuthorityRegister`
The system shall provide a view of current and historical authority limits per cell, queryable at any point in time.

### FR-AA-003: Grant underwriter authority within a cell's limit
**Source:** S0.2 — `GrantUnderwriterAuthorityLimit` → `UnderwriterAuthorityLimitGranted`
The system shall allow a Cell CUO to grant an individual underwriter a delegated authority limit, validated against the cell's own current limit. The validation result shall be recorded on the grant.

### FR-AA-004: Provide the decision-time authority view
**Source:** S0.2 — Read Model `AuthorityMatrix`
The system shall provide the authority view that Underwriting Decisioning's assessment step queries at decision time, distinct from the underwriter-facing historical register (FR-AA-005).

### FR-AA-005: Maintain a queryable underwriter authority register
**Source:** S0.2 — Read Model `UnderwriterAuthorityRegister`
The system shall provide a view of current authority per underwriter, with full history.

### FR-AA-006: Reject an underwriter authority grant that exceeds the cell's limit
**Source:** S0.3 — `UnderwriterAuthorityLimitRejected`
The system shall reject an underwriter authority grant request that would exceed the cell's own delegated limit, recording the rejection reason and the amount by which it was exceeded.

### FR-AA-007: Support escalation from a rejected grant
**Source:** S0.3 — `CellAuthorityIncreaseRequested` (via `RequestCellAuthorityIncrease`)
The system shall allow a Cell CUO to request an increase to the cell's own overall authority limit following a rejected underwriter grant, as an explicit, auditable next step rather than a dead end.

### FR-AA-008: Revise an authority limit with an audit trail
**Source:** S0.4 — `ReviseAuthorityLimit` → `AuthorityLimitRevised`
The system shall allow governance to revise a cell's or underwriter's authority limit, recording the previous and new limit and incrementing the record's version. Previous values shall never be overwritten silently.

### FR-AA-009: Reassess in-flight submissions on authority change
**Source:** S0.4/S0.5 — `ReassessInFlightSubmissionsOnRuleChange` → `InFlightSubmissionReassessed`
The system shall, on every `AuthorityLimitRevised` or `AuthorityLimitRevoked` event, reassess any submission currently in-flight under the changed authority, recording which policy stance was applied (grandfathered vs. re-evaluated) and the reassessment result. **The policy stance itself (grandfather vs. re-evaluate) is a business decision requiring stakeholder input — see DEC-004.**

### FR-AA-010: Revoke an authority limit
**Source:** S0.5 — `RevokeAuthorityLimit` → `AuthorityLimitRevoked`
The system shall allow governance to revoke a cell's or underwriter's authority limit entirely, recording the prior limit for audit, the reason, and whether the revocation is immediate or effective-from-next-decision-point. Revocation shall trigger FR-AA-009 with immediacy appropriate to the higher-risk nature of a revocation versus a routine revision.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-001 (multi-provider-per-cell authority grants), DEC-002 (aggregate vs. independent underwriter authority pool), DEC-003 (internal-only vs. provider-sign-off grants), DEC-004 (grandfather vs. re-evaluate policy default), DEC-005 (reopened-file review flag on revocation).
