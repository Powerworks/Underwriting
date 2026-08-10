# Integration Requirements

Every exploratory context (1c/1d/1e/1f) — and, following the S1a.5 pricing revision, part of the confirmed scope too — has at least one explicit "this is someone else's specialist system" boundary. This is the single most important section for anyone scoping what actually needs to be built versus integrated against.

### IR-001: Broker system data exchange (ACORD/ADEPT)
**Source:** FR-SI-001
**This system owns:** receiving and normalizing a broker submission into the ACORD ADEPT standard schema.
**The external system owns:** the broker's own originating system and its native payload format.
**Contract:** the ACORD ADEPT schema itself, enabling automated exchange across global broker systems generally, not a bespoke Howden-specific integration.

### IR-002: Cat-modeling extract (Exposure Intelligence → external cat model)
**Source:** FR-EI-008, FR-EI-009
**This system owns:** producing a correct, complete exposure extract in the vendor's schema, and auditing what was sent and when.
**The external system owns:** the cat model run itself, PML calculation, and any hurricane-track overlay.

### IR-003: PML recalculation (External Threat ← external cat model)
**Source:** FR-ET-004, FR-ET-005
**This system owns:** the trigger (storm track update / exposure change) and the display of the result (`ThreatExposureView`).
**The external system owns:** storm physics against policy terms — deductibles, limits, sub-limits, per-occurrence vs. aggregate treatment. Same integration-boundary shape as IR-002, in the reactive direction rather than the batch direction.

### IR-004: Reinsurance placement (Portfolio Governance → outwards reinsurance)
**Source:** FR-PG-006
**This system owns:** recording a hedging action request as an intent, tied to a specific freeze/breach.
**The external system owns:** actual placement of a reinsurance layer — TFP's existing, already-established outwards reinsurance function with its own systems and market relationships. Modeling the placement itself would be scope creep into a different domain.

### IR-005: AI baseline pricing (Submission Intake ← external rating engine)
**Source:** FR-SI-011
**This system owns:** the trigger (successful normalization), the display (`PricedSubmissionView`), and measuring underwriter deviation from the baseline for model-performance feedback.
**The external system owns:** the EBM risk segmentation model, and cross-referencing of alternative data (weather trends, satellite imagery, historical loss patterns) that produces the baseline premium figure.

### IR-006: Industry loss index (Capital & Reinsurance Instruments ← external index provider)
**Source:** FR-CRI-002
**This system owns:** ingesting index updates and calculating distance-to-attachment for registered bonds.
**The external system owns:** the industry-wide loss aggregation methodology and the index figure itself (e.g. a PCS-style provider).

## Common pattern across all six boundaries

In every case, this system is the system of record for **why** something happened (which trigger, tied to which business fact) — it is never the system that performs the specialist calculation or execution on the other side of the boundary. Any future integration should be scoped the same way: identify what this system needs to trigger and display, and explicitly exclude the computation/execution itself from build scope unless a deliberate decision is made to bring it in-house.
