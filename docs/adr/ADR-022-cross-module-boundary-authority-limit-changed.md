# ADR-022: Cross-module boundary on `InFlightSubmissionReassessed`

**Status**: Accepted (decided during `001-authority-administration` planning, `research.md` Decision 4)

## Context

The board models `ReassessInFlightSubmissionsOnRuleChange` →
`InFlightSubmissionReassessed` under the Authority Administration
chapter/context, but the event's own fields (`submissionId`,
`previousBasis`, `newBasis`, `policyApplied`, `reassessmentResult`) and its
assigned aggregate (`SubmissionAssessment`, fixed on the board) show it
actually operates on Underwriting Decisioning's aggregate, not this
module's.

## Decision

Per ADR-004 (module boundaries = the board's `MODEL_CONTEXT` nodes exactly;
cross-module only via `Contracts`/integration events), `001-authority-administration`'s
scope is narrowed to **publishing** `AuthorityLimitChangedV1` (a versioned
integration event) whenever `AuthorityLimitRevised` or `AuthorityLimitRevoked`
is appended. The `ReassessInFlightSubmissionsOnRuleChange` automation itself —
reading `SubmissionAssessment` and appending `InFlightSubmissionReassessed` —
is out of scope here and belongs in `004-underwriting-decisioning`'s own
plan, consuming this integration event.

## Rationale

Implementing the automation here would mean this module's own automation
reaching directly into another module's aggregate, which Architecture
Constraints and ADR-004 both explicitly forbid ("cross-module communication
is versioned integration events over durable per-module queues, never a
shared table or direct cross-module query"). Publishing the integration
event is the correct-sized piece of this cross-cutting concern that actually
belongs to Authority Administration.

## Alternatives Considered

No alternative was seriously considered beyond "implement it here anyway" —
rejected outright as a direct violation of ADR-004, not a close call.

## Consequences

`004-underwriting-decisioning/plan.md` must account for consuming
`AuthorityLimitChangedV1` and implementing
`ReassessInFlightSubmissionsOnRuleChange` — flagged explicitly (tasks.md
T056) for when that feature's plan is written, since `004` has no `plan.md`
yet as of this ADR. FR-009 in `001-authority-administration/spec.md`
("System MUST support ReassessInFlightSubmissionsOnRuleChange...") is
satisfied by the *publishing* half here (verified: tasks.md T055,
`ReviseAuthorityLimitIntegrationTests`/`RevokeAuthorityLimitIntegrationTests`);
the *consuming* half is `004`'s responsibility, not a gap in this plan.
