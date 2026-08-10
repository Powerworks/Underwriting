# Context 1f — Capital & Reinsurance Instruments: Functional Requirements

**Status:** Exploratory — not confirmed TFP build scope. **Board chapter:** `Capital & Reinsurance Instruments`.

## Overview

Cat bond reference data plus live tracking against two structurally different trigger mechanisms — a third-party industry loss index (rapid liquidity) and TFP's own audited actual losses. Complements Portfolio Governance rather than replacing its hedging/freeze mechanisms. Added after TFP's own published materials on the Woody Re cat bond.

## Actors

Capital markets / outwards reinsurance team, actuarial/claims audit function, external industry loss index provider.

## Functional Requirements

### FR-CRI-001: Register a cat bond as capacity reference data
**Source:** S1f.1 — `RegisterCatBond` → `CatBondRegistered`
The system shall record a cat bond's terms (name, covered syndicate, capacity, trigger type constrained to `indemnity` or `index`, attachment point, covered perils, interest rate, term) as reference/capital data, tracking the bond's terms without issuing or legally managing it.

### FR-CRI-002: Track industry loss index updates
**Source:** S1f.2 — `IndustryLossIndexUpdated`
The system shall ingest updates from an external industry-wide aggregated catastrophe loss index provider, structurally distinct from the exposure/threat feeds in Context 1d, with the same staleness-handling concern applying.

### FR-CRI-003: Recalculate distance-to-attachment for index-triggered bonds
**Source:** S1f.2 — `TrackCatBondAttachmentDistance` → `CatBondAttachmentDistanceUpdated`
The system shall recalculate an index-triggered bond's distance to its attachment point on every industry loss index update.

### FR-CRI-004: Provide the cat bond coverage view
**Source:** S1f.2 — Read Model `CatBondCoverageView`
The system shall present executives a live view of where each bond's coverage currently stands relative to its attachment point, cross-referenced against exposure and concentration views (Contexts 1c/1e).

### FR-CRI-005: Feed narrowing attachment distance into portfolio governance
**Source:** S1f.3
The system shall treat a narrowing cat bond attachment distance as an additional, concurrent trigger type for the threshold evaluation in Portfolio Governance (FR-PG-001), and as a basis for a hedging action request (FR-PG-006) to complement bond coverage with traditional reinsurance layers.

### FR-CRI-006: Audit TFP's own losses for indemnity-triggered bonds
**Source:** S1f.5 — `AuditIndemnityLossesAgainstTrigger` → `IndemnityLossesAudited`
The system shall, for indemnity-triggered bonds, periodically or on loss-event audit TFP's own actual incurred losses (drawn from Claims data, `ClaimPaid`/`ClaimReserveSet` — never from the industry loss index) against the bond's trigger threshold, recording the audited total, threshold, and distance. This shall require an explicit audit confirmation and shall never be self-executing.

### FR-CRI-007: Confirm cat bond trigger via the correct upstream path
**Source:** S1f.4 — `CatBondTriggered`
The system shall record a cat bond trigger event via exactly one of two legitimate paths depending on the bond's trigger type: `CatBondAttachmentDistanceUpdated` crossing zero for index bonds, or `IndemnityLossesAudited` crossing the threshold for indemnity bonds. Index bonds may be closer to self-executing given third-party data; indemnity bonds shall always require the audit confirmation of FR-CRI-006.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-028 (who confirms an index-triggered bond's crossing — self-executing vs. external determination).
