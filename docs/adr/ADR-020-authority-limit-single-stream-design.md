# ADR-020: `AuthorityLimit` aggregate is a single stream keyed by `authorityLimitId`

**Status**: Accepted (decided during `001-authority-administration` planning, `research.md` Decision 2)

## Context

`AuthorityLimit` covers two tiers — a Cell's own delegated authority, and an
individual underwriter's authority within a cell — and needs a stream design
decided before any handler can be written.

Confirmed directly from the board's own field data: `Revise`/`Revoke`
commands take `authorityLimitId` as a caller-supplied (non-`generated`)
field, while `Grant*` commands never do, and their `*Granted` events mark
`authorityLimitId` as both `idAttribute` and `generated`. That is the board's
own signal for "this creates a new stream" vs. "this targets an existing
one."

## Decision

One Marten event stream per grant (Cell-tier or Underwriter-tier), identified
by the `authorityLimitId` generated on the first `*Granted` event. Every
subsequent event that references an existing `authorityLimitId`
(`AuthorityLimitRevised`, `AuthorityLimitRevoked`, and the rejection events —
see ADR-021) appends to that same stream.

## Rationale

Matches constitution Principle I exactly: a self-aggregating `Create`/`Apply`
stream, with no per-slice document/event-sourced choice to make. The board's
own field flags already answer the stream-boundary question; no further
inference was needed.

## Alternatives Considered

- **A single shared stream for the whole Cell** (all its underwriters' grants
  living on one stream) — rejected: `AuthorityLimitRevoked` already carries
  `target` distinguishing cell vs. underwriter as separate records with
  separate `authorityLimitId`s per the Event Model Detail appendix; forcing
  them onto one stream would make one underwriter's revision compete for the
  same stream-version with every other underwriter's grants in that cell —
  an unforced concurrency hazard with no requirement driving it.

## Consequences

Every handler that reads or mutates an `AuthorityLimit` (`GrantCellAuthorityLimit`,
`GrantUnderwriterAuthorityLimit`, `ReviseAuthorityLimit`, `RevokeAuthorityLimit`,
`RequestCellAuthorityIncrease`) addresses exactly one stream by its
`authorityLimitId`, with no cross-stream transaction ever required for a
single grant's own lifecycle. Read models that need a *per-cell* or
*per-underwriter* view spanning multiple `AuthorityLimit` streams
(`CellAuthorityRegister`, `UnderwriterAuthorityRegister`) are built as
separate multi-stream projections, not by widening this stream's own
boundary.
