# Step 8 — Completeness Check: Field Traceability Report

Analysis run against the live board: 90 events, 37 commands, 24 read models, 11 screens, 15 automations. Method: built a global field-name index across all events/commands/read models, then checked (a) every reference-shaped required command field (`*Id`/`*Reference`) against every `idAttribute`/`generated` field anywhere in the model for a traceable origin, and (b) every non-identifier event field against whether it's referenced by name anywhere else. Class (a) produced 11 candidates; class (b) produced 140 — the vast majority of which are legitimate (audit timestamps, actor-attribution fields, and narrative text are correctly event-only; the event *is* their permanent record and they don't need a downstream consumer). Both lists were filtered by judgment before being reported here — a raw name-match dump would be mostly noise.

## Confirmed gaps (real, fixed)

### GAP-001: No event ever assigns a bordereau line identifier — ✅ FIXED
`lineId` is required throughout Bordereaux Settlement — `RaiseBordereauQuery.lineReference`, `BordereauLineQueried.lineId`, `RaisePriorPeriodAdjustment.originalLineId` — but `BordereauDrafted` only recorded `lineCount` (an integer), never enumerated individual lines with generated IDs. Confirmed by inspecting the actual field definition: `BordereauLineQueried.lineId` is `generated: false, idAttribute: false` — it was always modeled as caller-supplied, with no event actually supplying it.
**Fix applied:** `BordereauDrafted` now carries a `lines` list (each entry with a generated `lineId`, `policyTransactionId`, `transactionType`, `grossPremium`, `netPremium`, `providerShare`), matching the shape `UnbilledTransactionsForPeriod`/`BordereauDetail` already use. `lineCount` is retained as a derived summary field. See `event-model/build-scripts/fix_completeness_gaps.py`.

### GAP-002: No event ever assigns a concentration warning identifier — ✅ FIXED
`AcknowledgeExposureConcentrationWarning` requires a `warningId`, but `ExposureConcentrationWarningRaised`'s fields (`geocode`, `perilCategory`, `contributingCells`, `combinedExposureValue`, `thresholdBreached`, `severityLevel`, `raisedAt`) included no identifier at all.
**Fix applied:** Added a generated, `idAttribute` `warningId` as the first field on `ExposureConcentrationWarningRaised`. See `event-model/build-scripts/fix_completeness_gaps.py`.

## Naming inconsistencies (data exists, but not traceable by name — lower priority)

- `NotifyClaim.policyReference` vs. `PolicyBound.policyId` — same concept, different field name.
- `RaisePriorPeriodAdjustment.originalBordereauId` vs. `BordereauDrafted.bordereauId` — same pattern.
- `RequestHedgingAction.freezeOrThreatReference` bundles two distinct possible origins (`TerritoryUnderwritingFrozen.freezeId` or a threat ID) into one ambiguous field, rather than two distinct optional fields.

These are cosmetic — the data genuinely exists and the connection is obvious to a reader — but they'd fail a strict automated traceability check and are worth aligning if the model is regenerated.

## False positives, confirmed not gaps

The following command fields matched the "reference-shaped, no traceable origin" pattern but are correctly *not* gaps — they're actor/external identifiers, not references to something this system generates: `DecideReferral.resolvingPartyId`, `SearchSubmissionHistory.searcherId`, `OverridePortfolioFreeze.overridingExecutiveId` (all identify a person, sourced from an identity system out of scope), `GrantCellAuthorityLimit.sourceAgreementReference` (references an external legal document, not a domain event), `IngestExternalThreatUpdate.threatExternalId` (deliberately the external feed's own ID scheme, per S1d.1's explicit design decision not to invent custom matching logic).

## Systemic finding: audit-compliance fields exist with no aggregate query view

Several fields were added specifically for audit-trail completeness per NFR-003/NFR-004 — `SubmissionWithinAuthority.authorityVersionChecked`/`freezeCheckPassed`, `PMLRecalculated.hasImpact`/`sourcedFromCatModel`, `ClaimPaid.validatedAgainstBindTerms`. Each individually satisfies its own scenario's audit requirement, but there is currently **no read model that lets someone query across many decisions** — e.g. "show every assessment where the freeze check didn't run" or "show every claim payment not yet validated against bind terms." The data is captured; nothing currently surfaces it in aggregate. This is a read-model-layer gap, not an event-layer one, and is lower priority than GAP-001/GAP-002 since the underlying facts are at least recorded.

## What was checked and found clean

The remaining ~135 event-field candidates from the raw analysis are correctly event-only: audit timestamps (`*At`), actor attribution (`*By`), and narrative/reason text that is the terminal record of what happened, not data meant to be re-projected elsewhere. `PolicyBound.providerAuthorityLineage` is intentionally unresolved pending DEC-001, already tracked in the Open Decisions Register, not a new finding.

## Status

GAP-001 and GAP-002 are fixed on the board (both were additive — a new list field and a new generated ID field, no breaking changes to existing scenarios or GWT specifications). Naming inconsistencies and the audit-aggregate-view gap were left as open decisions rather than resolved silently — tracked as DEC-029 (align reference-field naming with source `idAttribute` field names) and DEC-030 (build a cross-cutting compliance/audit dashboard read model, or accept per-event audit fields as sufficient) in `14-Open-Decisions-Register.md`.
