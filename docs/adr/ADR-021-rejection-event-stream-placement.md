# ADR-021: Rejection events append to the stream they were evaluated against

**Status**: Accepted (decided during `001-authority-administration` planning, `research.md` Decision 3)

## Context

Three gaps existed between what the board specified and what a complete
implementation needs (FR-006/007/010/011/012): a duplicate-active Cell-tier
grant, a duplicate-active Underwriter-tier grant / cascade-exceeded grant,
and a revise attempt against a Revoked or cascade-exceeded record all need a
rejection event, and none of the three had an obvious existing home.

## Decision

Every rejection event appends to the stream of the *existing* `AuthorityLimit`
record that caused the rejection, rather than living on its own
transient/log stream:

| Event | Appends to | Covers |
|---|---|---|
| `CellAuthorityLimitGrantRejected` (new) | the existing Active Cell-tier stream being duplicated | FR-011 (Cell tier half) |
| `UnderwriterAuthorityLimitRejected` (extended — `rejectionReason: "ExceedsCellLimit" \| "DuplicateActiveGrant"`) | the Cell's own `AuthorityLimit` stream (the entity the rejection was evaluated against) | FR-003/FR-006 (existing) + FR-011 (Underwriter tier half) |
| `AuthorityLimitRevisionRejected` (new — `rejectionReason: "RevokedRecord" \| "ExceedsCellLimit"`) | the target record's own stream (the one `ReviseAuthorityLimit` was trying to revise) | FR-010 (Revoked-terminal) + FR-012 (cascade check on revise) |

## Rationale

Every rejection is evaluated *against* an existing record's current state,
and a stream that already exists to append to is available in every case —
no rejection in this feature happens against a record that doesn't yet
exist. This keeps the same "load your own mutation target live, never a
snapshot" rule (Architecture Constraints) applying uniformly to rejections
too, and gives each affected record's own event stream a complete audit
trail including attempts that failed against it — directly useful for
`CellAuthorityRegister`/`UnderwriterAuthorityRegister` if a later feature
wants to surface rejected attempts there.

## Alternatives Considered

- **A separate `GrantAttempt`/rejection-log document** (non-event-sourced) —
  rejected: violates Principle I's "one storage strategy," and the
  audit-trail value of keeping rejections on the same stream as what they
  were evaluated against is exactly the kind of thing Principle I exists to
  preserve.
- **Reusing `UnderwriterAuthorityLimitRejected` for the Cell tier too**
  (instead of a new `CellAuthorityLimitGrantRejected`) — rejected: the two
  tiers' grant commands are genuinely different (`GrantCellAuthorityLimit`
  vs. `GrantUnderwriterAuthorityLimit`), and conflating their rejection
  events would mean one event type's `rejectionReason` enum needs
  tier-conditional valid values (`ExceedsCellLimit` never applies to a
  Cell-tier grant, since a Cell has no enclosing tier to exceed) — cleaner
  as two event types.

## Consequences

`AuthorityLimit.Apply` gained three intentional no-op overloads (one per
rejection event) — Apply mutates unconditionally per Principle I, and a
rejection is recorded for audit without changing `status`/`version`. Any
future read model wanting rejection history reads it off the same stream
the corresponding grant/revision lives on, not a separate source.
