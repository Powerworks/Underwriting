# Phase 0 Research: Authority Administration

## Decision 1: Solution scaffold location

**Decision**: `src/BrokerConnect.slnx` at repo root, modules under `src/Modules/<Context>/`,
shared libraries under `src/BuildingBlocks/`.

**Rationale**: No `.NET` solution exists anywhere in this repo yet (verified: no
`.sln`/`.csproj` files). `build-kit-dotnet-es`'s own setup docs treat
`<path-to-your-.NET-solution>` as a required placeholder to fill in per-project, not
something with a fixed answer — and Solution Arch §3 fixes the *module* naming
(`BrokerConnect.Modules.<Context>.{Api,Domain,Contracts}`) but not the repo-relative
root. `src/` is the path of least surprise: it doesn't collide with any existing
top-level content (`event-model/`, `build-kit-dotnet-es/`, `HLD/`, `Requirements/`,
`Solution Arch/`, `Project Plan/`, `specs/`), and matches the two-part
`<repo>/src/<Solution>.sln` shape common to .NET repos generally (a lighter
convention than the reference `code/K9Crush-scaffold/K9Crush` nesting
`build-kit-dotnet-es/README.md` mentions from a prior project, which doesn't
apply here since nothing forced that project's specific nesting).

**Alternatives considered**:
- Repo root directly (`BrokerConnect.slnx` next to `HLD/`, `Requirements/`, etc.) —
  rejected: mixes prototype documentation and product source at the same level,
  makes `.gitignore`ing `bin/`/`obj/` messier alongside doc folders.
- Mirroring `code/<Name>-scaffold/<Name>` from the K9Crush reference — rejected:
  that nesting existed for a reason specific to that project's history; nothing
  here requires two levels of nesting.

**Consequence for later features**: every other feature's `/speckit-plan` (`002`
onward) should read this decision rather than re-deciding it — flag if a later
plan feels the need to deviate.

## Decision 2: `AuthorityLimit` aggregate is a single stream keyed by `authorityLimitId`

**Decision**: One Marten event stream per grant (Cell-tier or Underwriter-tier),
identified by the `authorityLimitId` generated on the first `*Granted` event.
Every subsequent event that references an existing `authorityLimitId`
(`AuthorityLimitRevised`, `AuthorityLimitRevoked`, and the new rejection events —
see Decision 3) appends to that same stream.

**Rationale**: Confirmed directly from the board's own field data — `Revise`/`Revoke`
commands take `authorityLimitId` as a caller-supplied (non-`generated`) field, while
`Grant*` commands never do and their `*Granted` events mark `authorityLimitId` as
both `idAttribute` and `generated`. That's the board's own signal for "this creates
a new stream" vs. "this targets an existing one" (see `event-model-to-speckit-guide.md`,
field flags), matching constitution Principle I exactly (self-aggregating
`Create`/`Apply` stream, no per-slice document/event-sourced choice to make).

**Alternatives considered**: A single shared stream for the whole Cell (all its
underwriters' grants living on one stream) — rejected: `AuthorityLimitRevoked`
already carries `target` distinguishing cell vs. underwriter as separate records
with separate `authorityLimitId`s per the Event Model Detail appendix; forcing them
onto one stream would make one underwriter's revision compete for the same
stream-version with every other underwriter's grants in that cell, an unforced
concurrency hazard with no requirement driving it.

## Decision 3: Closing the 3 rejection-event gaps (FR-006/007/010/011/012)

**Decision**: Every rejection event appends to the stream of the *existing*
`AuthorityLimit` record that caused the rejection, rather than living on its own
transient/log stream:

| New/extended event | Appends to | Covers |
|---|---|---|
| `CellAuthorityLimitGrantRejected` (**new**) | the existing Active Cell-tier stream being duplicated | FR-011 (Cell tier half) |
| `UnderwriterAuthorityLimitRejected` (**extended** — add `rejectionReason: "ExceedsCellLimit" \| "DuplicateActiveGrant"`) | the Cell's own AuthorityLimit stream (the entity the rejection was evaluated against) | FR-003/FR-006 (existing, unchanged) + FR-011 (Underwriter tier half, new reason value) |
| `AuthorityLimitRevisionRejected` (**new** — `rejectionReason: "RevokedRecord" \| "ExceedsCellLimit"`) | the target record's own stream (the one `ReviseAuthorityLimit` was trying to revise) | FR-010 (Revoked-terminal) + FR-012 (cascade check on revise) |

**Rationale**: Every rejection is evaluated *against* an existing record's current
state, and a stream that already exists to append to is available in every case —
no rejection in this feature happens against a record that doesn't yet exist. This
keeps the same "load your own mutation target live, never a snapshot" rule
(constitution Architecture Constraints) applying uniformly to rejections too, and
gives each affected record's own event stream a complete audit trail including
attempts that failed against it — directly useful for `CellAuthorityRegister`/
`UnderwriterAuthorityRegister` if a later feature wants to surface rejected
attempts there.

**Alternatives considered**: A separate `GrantAttempt`/rejection-log document
(non-event-sourced) — rejected: violates Principle I's "one storage strategy,"
and the audit-trail value of keeping rejections on the same stream as what they
were evaluated against is exactly the kind of thing Principle I exists to
preserve. Reusing `UnderwriterAuthorityLimitRejected` for the Cell tier too
(instead of a new `CellAuthorityLimitGrantRejected`) — rejected: the two tiers'
grant commands are genuinely different (`GrantCellAuthorityLimit` vs.
`GrantUnderwriterAuthorityLimit`), and conflating their rejection events would
mean one event type's `rejectionReason` enum needs tier-conditional valid values
(`ExceedsCellLimit` never applies to a Cell-tier grant, since a Cell has no
enclosing tier to exceed) — cleaner as two event types.

## Decision 4 (scope-narrowing, not a technology decision): the cross-module boundary on `InFlightSubmissionReassessed`

**Finding**: The board models `ReassessInFlightSubmissionsOnRuleChange` →
`InFlightSubmissionReassessed` under the Authority Administration chapter/context,
but the event's own fields (`submissionId`, `previousBasis`, `newBasis`,
`policyApplied`, `reassessmentResult`) and its assigned aggregate
(`SubmissionAssessment`, fixed on the board — see the guide's "aggregate field"
section) show it actually operates on Underwriting Decisioning's aggregate, not
this module's.

**Decision**: Per ADR-004 (module boundaries = the board's `MODEL_CONTEXT` nodes
exactly; cross-module only via `Contracts`/integration events), this module's scope
is narrowed to **publishing** `AuthorityLimitChangedV1` (a new versioned
integration event, `V1`-suffixed per constitution naming convention) whenever
`AuthorityLimitRevised` or `AuthorityLimitRevoked` is appended. The actual
`ReassessInFlightSubmissionsOnRuleChange` automation — the one that reads
`SubmissionAssessment` and appends `InFlightSubmissionReassessed` — is out of
scope for `001-authority-administration` and belongs in
`004-underwriting-decisioning`'s own plan, consuming this integration event.

**Rationale**: Implementing it here would mean this module's automation reaching
directly into another module's aggregate, which Architecture Constraints and
ADR-004 both explicitly forbid ("Cross-module communication is versioned
integration events over durable per-module queues, never a shared table or direct
cross-module query"). Publishing the integration event is the correct-sized piece
of this cross-cutting concern that actually belongs to Authority Administration.

**Consequence**: `004-underwriting-decisioning/plan.md` must account for consuming
`AuthorityLimitChangedV1` and implementing `ReassessInFlightSubmissionsOnRuleChange`
— flag this explicitly when that feature's plan is written so it isn't silently
dropped. FR-009 in `001-authority-administration/spec.md` ("System MUST support
ReassessInFlightSubmissionsOnRuleChange...") is satisfied by the *publishing* half
here; the *consuming* half is `004`'s responsibility, not a gap in this plan.

## Open items not resolved here (deferred, not fabricated)

- **Numeric performance/scale targets**: none stated by the board or Requirements
  docs for this context beyond the AuthorityMatrix consistency requirement already
  resolved. Not fabricated — flagged as an open Success Criteria placeholder
  (SC-002–004) in `spec.md`, to be filled via a future `/speckit-clarify` pass or
  stakeholder input, not invented here.
- **Cell-scoped vs. cross-cell authorization** (Solution Arch §8, NFR-006): real
  requirement, but the concrete policy shape (claims-based? role+cellId claim?)
  depends on ADR-010's identity-provider decision, which is explicitly still open
  (DEC-032). Handler-level enforcement is planned; the exact mechanism is deferred
  until ADR-010 lands, not decided here.
