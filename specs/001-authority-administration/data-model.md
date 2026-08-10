# Phase 1 Data Model: Authority Administration

Field names/types below are transcribed from `spec.md`'s Event Model Detail
appendix (itself transcribed verbatim from the live board — see
`event-model-to-speckit-guide.md`). New fields introduced to close the
rejection-event gaps (`research.md` Decision 3) are marked **(new)**.

## Aggregate: `AuthorityLimit`

Self-aggregating event stream (constitution Principle I), one stream per grant,
keyed by `authorityLimitId`. `tier` (`Cell` | `Underwriter`) is fixed at creation
and never changes across the stream's life.

### State shape (`Apply`-computed)

| Field | Type | Notes |
|---|---|---|
| `authorityLimitId` | `Guid` | Stream id |
| `tier` | `string` | `"Cell"` \| `"Underwriter"` — set once, at creation |
| `cellId` | `string` | Always present |
| `underwriterId` | `string?` | Present only when `tier == "Underwriter"` |
| `providerId` | `string?` | Optional (matches `AuthorityMatrix.providerId`, optional) |
| `classesOfBusiness` | `List<string>` | From `authorityScope`/`scope` |
| `territory` | `string` | From `authorityScope`/`scope` |
| `maxLineSize` / `maxGrossPremium` | `decimal` | Naming differs between Cell-tier (`maxLineSize`/`maxAggregate`) and the `AuthorityMatrix` projection (`maxGrossPremium`/`maxLimit`) — **naming inconsistency inherited from the board** (see `event-model-to-speckit-guide.md`'s "naming inconsistencies" caveat from `15-Completeness-Check.md`); reconcile to one internal name during implementation, don't propagate the inconsistency into code. |
| `maxAggregate` / `maxLimit` | `decimal` | Same naming note as above |
| `currency` | `string` | |
| `sourceAgreementReference` | `string?` | Cell-tier only |
| `status` | `string` | `"Active"` \| `"Revoked"` (Clarified 2026-08-09) |
| `version` | `int` | Incremented on every `Revised` event |
| `grantedBy`, `grantedAt` | `string`, `DateTime` | |

### Events (`Create` / `Apply`)

1. **`CellAuthorityLimitGranted`** (creates stream, `tier = "Cell"`)
   `authorityLimitId` (id, generated), `cellId`, `authorityScope {classesOfBusiness, territory, maxLineSize, maxAggregate}`, `sourceAgreementReference`, `grantedBy`, `effectiveDate`, `version` (generated), `grantedAt` (generated).
2. **`UnderwriterAuthorityLimitGranted`** (creates stream, `tier = "Underwriter"`)
   `authorityLimitId` (id, generated), `underwriterId`, `cellId`, `scope {classesOfBusiness, territory, maxLineSize}`, `grantedBy`, `effectiveDate`, `version` (generated), `validationResult`, `grantedAt` (generated).
3. **`AuthorityLimitRevised`** (existing stream)
   `authorityLimitId` (id), `target`, `previousLimit`, `newLimit`, `reason`, `revisedBy`, `effectiveDate`, `version` (generated), `revisedAt` (generated).
4. **`AuthorityLimitRevoked`** (existing stream, terminal)
   `authorityLimitId` (id), `target`, `priorLimit`, `reason`, `revokedBy`, `immediacy`, `revokedAt` (generated). `Apply` sets `status = "Revoked"`.
5. **`CellAuthorityLimitGrantRejected`** (new — appends to the *existing* Active Cell-tier stream being duplicated)
   `authorityLimitId` (id, of the existing record), `attemptedCellId`, `attemptedScope`, `rejectionReason: "DuplicateActiveGrant"`, `attemptedBy`, `rejectedAt` (generated). Does not change `status`/`version` — a rejected attempt is recorded, not a state transition.
6. **`UnderwriterAuthorityLimitRejected`** (existing event, **extended** — appends to the Cell's own stream)
   Existing fields (`underwriterId`, `cellId`, `requestedScope`, `rejectionReason`, `attemptedBy`, `rejectedAt`) **plus (new)**: `rejectionReason` now typed as `"ExceedsCellLimit" | "DuplicateActiveGrant"` instead of free-text `string` — narrowing an existing field's type, not adding a new one. Does not change the Cell's own `status`/`version`.
7. **`AuthorityLimitRevisionRejected`** (new — appends to the target record's own stream)
   `authorityLimitId` (id, existing), `attemptedNewLimit`, `rejectionReason: "RevokedRecord" | "ExceedsCellLimit"`, `attemptedBy`, `rejectedAt` (generated). Does not change `status`/`version`.

### Invariants (from Clarifications, 2026-08-09)

- At most one `Active` `AuthorityLimit` per (`tier`, `cellId`/`underwriterId`, `classOfBusiness`) — enforced by the handler querying `AuthorityMatrix` before appending a `*Granted` event, not by a database unique constraint (Marten event streams don't support cross-stream uniqueness constraints natively; the read-your-own-write guarantee from the `Inline` snapshot below is what makes this check reliable).
- `Revoked` is terminal — `ReviseAuthorityLimit` handler checks `status` before appending `AuthorityLimitRevised`.
- `ReviseAuthorityLimit` on `tier == "Underwriter"` re-runs the same cascade check as `GrantUnderwriterAuthorityLimit` (against the Cell's current limit).

## Entity: `CellAuthorityIncreaseRequest`

Its own small aggregate, not part of the `AuthorityLimit` stream — per
`scenarios.md`: "the request itself is the auditable fact, distinct from whatever
decision follows." No lifecycle beyond creation is modeled by the source board
(no `Approved`/`Denied` event exists yet) — implement as a single-event stream for
now; extending it is future scope, not invented here.

**`CellAuthorityIncreaseRequested`** (creates stream): `cellId`, `requestedBy`,
`currentLimit`, `requestedLimit`, `justification`, `requestedAt` (generated).

## Read Models (Marten projections)

### `AuthorityMatrix` — **`Inline` snapshot** (Clarified 2026-08-09 / SC-001)

The decision-time view Underwriting Decisioning's `AssessSubmission` queries
synchronously. Fields: `authorityLimitId` (id), `tier`, `providerId` (optional),
`cellId`, `underwriterId` (optional), `classOfBusiness`, `maxGrossPremium`,
`maxLimit`, `currency`, `status`, `version`, `grantedBy` (optional), `grantedAt`.
Projected from `CellAuthorityLimitGranted`, `UnderwriterAuthorityLimitGranted`,
`AuthorityLimitRevised`, `AuthorityLimitRevoked`.

**Why `Inline` and not `Async`**: constitution Architecture Constraints — "add one
only when a read model queries the entity by id... `Inline` means a caller's next
read always sees their own prior write." This is exactly the AssessSubmission
same-request scenario the clarification named.

### `CellAuthorityRegister` — plain async projection

Historical register, current + full history, keyed by `cellId`. Fields:
`cellId` (id), `currentScope {classesOfBusiness, territory, maxLineSize,
maxAggregate}`, `currentVersion`, `effectiveDate`, `history: List<{version, scope,
effectiveDate, supersededAt?}>`. Projected from `CellAuthorityLimitGranted`,
`AuthorityLimitRevoked`. No same-request consistency requirement — nothing reads
this synchronously after writing to it within one request.

### `UnderwriterAuthorityRegister` — plain async projection

Same shape as `CellAuthorityRegister`, keyed by `underwriterId`. Projected from
`UnderwriterAuthorityLimitGranted`, `CellAuthorityIncreaseRequested` (per the
board's own dependency edge — an increase request is relevant history for the
underwriter's register even though it doesn't itself change their limit).

## Cross-module boundary: `AuthorityLimitChangedV1` (published integration event)

Per `research.md` Decision 4 — **this module's actual responsibility** for the
"rules changed mid-flight" concern, not the reassessment itself:

```
AuthorityLimitChangedV1 {
  authorityLimitId: Guid
  changeType: "Revised" | "Revoked"
  cellId: string
  underwriterId: string?
  changedAt: DateTime
}
```

Published (via `WolverineFx.Marten` outbox, same transaction) whenever
`AuthorityLimitRevised` or `AuthorityLimitRevoked` is appended. Consumed by
`004-underwriting-decisioning`'s own `ReassessInFlightSubmissionsOnRuleChange`
automation, operating on its own `SubmissionAssessment` aggregate — out of scope
here, flagged for that feature's plan.
