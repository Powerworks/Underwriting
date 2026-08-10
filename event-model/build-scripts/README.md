# Build scripts — the actual history of how this board was built

These are the real, tested, run-in-order scripts that built the Broker Connect
board (`80f53178-c291-43a0-8aa5-bc723990c5db`) beyond the initial 21-slice
bulk `import-config` (see `../generate-slices.mjs` for that original layer).

## Why these exist instead of one regenerated `import-config.json`

`import-config` **replaces the entire board** on every call (confirmed in
`eventmodelers-import-config-guide.md`), so it can't be used to incrementally
patch an already-populated board — every subsequent addition in this session
(Authority Administration extensions, Submission Intake rebuild, Search &
Retrieval, Exposure Intelligence, External Threat, Portfolio Governance,
Context 2/3/4/5 rebuilds, and the GWT scenarios) had to go through the raw
node/column/scenario APIs directly instead. `generate-slices.mjs` was kept in
sync for the earlier layers (Authority Administration base, 1a-1c) but was
**not** extended to cover the later, much richer rebuilds of Context 0, 2, 3,
4, and 5 — doing so would mean re-deriving several thousand lines of already-
working, already-verified API calls into a second format with no
corresponding gain, since `import-config`'s replace-everything semantics make
it unusable for what these scripts actually needed to do (add to an existing
board without destroying it).

**These scripts are therefore the actual regenerable source for everything
they cover** — not the generator script — should the board ever need to be
rebuilt from scratch on a fresh instance.

## Run order

1. `build_submission_intake.py` / `build_submission_intake2.py` — Submission
   Intake rebuild (S1a.1-1a.4): 7 new events, 3 read models, command
   rename+refield, existing event refield. Discovered the raw "add column"
   endpoint doesn't create cell records (fixed via the `{rowId}-{colId}`
   deterministic cellId trick used in every script after this one).
2. `build_search_exposure.py` / `build_search_exposure2.py` — Search &
   Retrieval (S1b.1-1b.4) and Exposure Intelligence (S1c.1-1c.5).
3. `build_threat_governance.py` / `build_threat_governance2.py` — External
   Threat (S1d.1-1d.4) and Portfolio Governance (S1e.1-1e.5).
4. `rebuild_context0.py` / `rebuild_context0_2.py` — Context 0 rebuild
   (S0.1-S0.5): versioning, `CellAuthorityRegister`/`UnderwriterAuthorityRegister`,
   `AuthorityMatrix` rename, escalation path, the shared
   `ReassessInFlightSubmissionsOnRuleChange` mechanism. Also fixes the
   `GrantUnderwriterAuthorityLimit` orphaned-command bug (see below).
5. `rebuild_context2.py` — Underwriting Decisioning rebuild (S2.1-S2.10).
   Fixes the `AssessSubmission` orphaned-command bug.
6. `rebuild_context3.py` — Binding rebuild (S3.1-S3.5): allocation-list
   shape, `EndorsementReferred`, `PolicyCancelledForCause`, renewal rename.
7. `rebuild_context4.py` — Bordereaux Settlement rebuild (S4.1-S4.5):
   two-party agreement, `UnbilledTransactionsForPeriod`,
   `BordereauLineDeferred`. Fixes the `RaiseBordereauQuery` orphaned-command
   bug.
8. `rebuild_context5.py` — Claims rebuild (S5.1-S5.5): `ClaimRegister`,
   `ReserveRevisionReferred` (extends Context 0's authority engine to
   claims), `ClaimNotifiedAgainstInactivePolicy` / `ClaimPeriodValidated`.
9. `write_gwt_scenarios.py` — core-path GWT scenarios for all ~85 dictated
   scenarios across all 8 contexts, via the (differently-pathed than
   documented) real scenarios endpoint. Builds a column-index lookup from
   live board state first rather than trusting scattered saved node IDs.
10. `build_pricing_catbond.py`, `write_gwt_pricing_catbond.py`,
    `build_indemnity_trigger.py` — AI baseline pricing (S1a.5), Context 1f
    (Capital & Reinsurance Instruments), and the indemnity-trigger extension,
    added after new source material (TFP's own published pipeline
    description) revised the "pricing is fully out of scope" assumption.
11. `migrate_chapters.py`, `migrate_scenarios.py` — the single-chapter-to-11
    migration. The board started as one shared chapter ("one flowing
    timeline" - a deliberate early choice, see `generate-slices.mjs`'s
    original header comment) and grew to 88 columns / 294 nodes before it
    became clear the platform's intended pattern is one chapter per
    `MODEL_CONTEXT`, not one big timeline. Since **no API exists to move a
    node between chapters**, this is a full recreation: every node's
    placement was rebuilt across 11 new context-scoped chapters, then the
    old chapter was deleted. `migrate_scenarios.py` rewrites every GWT
    scenario against the new chapter/column IDs, dropping any cross-chapter
    `given`/`when` reference — confirmed by testing that the scenarios
    endpoint enforces same-timeline-only for both (`given` accepts EVENTs
    only, `when` accepts COMMANDs only, neither accepts a cross-chapter
    reference or an AUTOMATION-as-trigger). The cross-context relationship
    those dropped references used to carry lives in two other places
    instead: each element's own `description` text (states its trigger
    explicitly) and the `MODEL_CONTEXT` connection edges (the context map).

## Known platform bugs discovered and worked around

- **Command/read-model same-cell collision**: when a slice's `import-config`
  JSON has both a COMMAND and a READMODEL competing for one column's
  interaction cell, the bulk importer only grids one (the read model wins),
  silently orphaning the command as an unplaced node. Fixed post-hoc for 3
  orphans via `POST .../timelines/:id/cells/:cellId/drop` using the
  synthetic `{rowId}-{colId}` cellId format (the drop endpoint rejects real
  DB cell UUIDs — confirmed by testing).
- **Raw column-add endpoint doesn't create cells**: `POST
  .../timelines/:id/columns` creates a column entry but not matching cell
  records for any row. The node-create endpoint accepts the synthetic
  `{rowId}-{colId}` cellId directly even when no literal cell record exists,
  so this is used throughout instead of pre-fetching cells.
- **Two specific columns 500 on every scenario POST**, including a fully
  empty payload, while every sibling column works fine — data corruption
  from early in the session, root cause not identified. Routed around by
  attaching those two scenarios to a sibling column in the same slice
  instead (see `eventmodelers-import-config-guide.md` for the general
  pattern).
- **New chapters start with 3 pre-created empty columns**, not zero — not
  documented anywhere, discovered by auditing why 11 new chapters totaled
  121 columns when exactly 88 were explicitly created. Delete these (via the
  normal column-delete endpoint) after populating a new chapter if a clean
  board is wanted.
- **Deleting a `CHAPTER` node does not reliably cascade-delete everything
  under it.** After deleting one chapter that held 283 nodes, most children
  were correctly removed, but 25 `SLICE_BORDER` nodes (referencing the
  deleted chapter's now-gone column IDs) and 1 `COMMAND` node survived as
  true orphans (`node.parentId: null`, not placed in any surviving chapter's
  grid). Always audit after deleting a chapter: cross-reference every
  `SLICE_BORDER.meta.colId` against the current set of valid column IDs
  across all remaining chapters, and check for duplicate-titled elements
  whose `node.parentId` is `null`.
- **`given`/`when` in scenarios are hard-scoped to one timeline, with no
  escape hatch** — confirmed by testing, not assumed from docs. `given`
  rejects an EVENT from a different chapter with an explicit
  `"belongs to a different timeline"` error; `when` rejects a COMMAND the
  same way; and `when` separately rejects an AUTOMATION node entirely
  (`"'when' only allows a COMMAND node"`), even when that automation lives
  in the correct (same) timeline as the scenario and is the actual local
  reaction to a foreign trigger. There is no way to express a cross-chapter
  trigger inside a GWT scenario — the relationship has to live in
  `description` text and/or `MODEL_CONTEXT` connection edges instead.
  `MODEL_CONTEXT` and chapter/timeline are two fully independent grouping
  mechanisms on this platform - the context map has no bearing on what a
  scenario is allowed to reference, only physical chapter placement
  (`node.parentId`) does.

## Prerequisites to re-run

Each script hardcodes `TOKEN`/`BOARD_ID`/`ORG_ID`/`BASE_URL` at the top
(pulled from `.eventmodelers/config.json` at the time). Update these before
re-running against a different board. Several scripts depend on node/column
IDs written to `/tmp/claude_*.json` by earlier scripts in the sequence —
re-running out of order or after clearing `/tmp` will not work without
re-deriving those IDs from a fresh `GET .../nodes`.
