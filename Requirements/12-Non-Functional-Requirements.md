# Cross-Cutting Non-Functional Requirements

These requirements recur across multiple contexts and are stated once here rather than repeated per context. Each links back to the functional requirements that motivated it.

### NFR-001: Append-only audit trail — corrections are new entries, never edits
Every correction to a previously-recorded financial or governance fact shall be recorded as a new, appended entry referencing the original — never an edit to history. This single principle, established independently for Bordereaux Settlement (FR-BS-007), applies identically to Binding endorsements (FR-BND-005) and Claims reopening (FR-CLM-007). A system that only stores current state cannot satisfy this; every bind/endorsement/cancellation/reopening must leave an immutable transaction record.

### NFR-002: The authority/governance engine is a cross-cutting capability
The authority-check-and-escalate mechanism established in Authority Administration (Context 0) is not scoped to underwriting decisioning alone. The same mechanism (`DecideReferral`/`ReferralApproved`/`ReferralDeclined`) is reused by material endorsements (FR-BND-006) and claims reserve revisions (FR-CLM-006). Any future extension of governance to a new area of the system should reuse this mechanism rather than build a bespoke one.

### NFR-003: Explicit human confirmation for disputed or high-stakes determinations
The following determinations shall never be automatically decided, regardless of how strong the supporting data appears: claim-period validity against date of loss (FR-CLM-009), indemnity cat bond trigger confirmation (FR-CRI-006), and (pending DEC-024) portfolio freeze imposition. Each of these is the kind of determination that ends up in a dispute, audit finding, or coverage argument, and an automated silent decision would be indefensible.

### NFR-004: Versioned governance records with point-in-time reference
Authority limits (FR-AA-001, FR-AA-003) and their downstream checks (FR-UD-002) shall be versioned from their first record. Every decision that depends on a governance rule shall record which specific version of that rule was checked against, not merely that a check occurred, so a later audit can reconstruct exactly what rule was in force at decision time.

### NFR-005: Asynchronous processing for non-blocking background reactions
Reactive projections and calculations that are not required for the triggering user action to complete (exposure projection updates, FR-EI-001; PML recalculation, FR-ET-004) shall run asynchronously. The underwriter or broker whose action triggered the reaction shall never wait on it.

### NFR-006: Tiered access control — cell-scoped by default, cross-cell as an explicit elevated permission
Underwriter-facing views and searches (FR-SR-001, FR-SR-003) default to the searcher's own cell. Cross-cell visibility (FR-SR-006, FR-SR-007, FR-PG-003) is an explicit, separately-logged elevated permission for operations/governance roles (FR-SR-008), never a permission-filter bolted onto a single generic capability.

### NFR-007: Multi-currency and FX handling
Bordereaux and settlement figures (Context 4) span multiple currencies where premium is written in local currency but reported figures may be required in a reporting currency (e.g. USD). Every settlement-relevant figure shall capture both the transaction currency and the FX rate at the transaction date — this is a currency-and-FX-rate-at-transaction-date concern on every settlement line, not a global conversion applied at reporting time.

### NFR-008: Data completeness over data optimism — record "checked, no result" explicitly
Where a process runs but finds nothing actionable, the system shall record that fact explicitly (FR-ET-006's zero-impact PML result) rather than producing no event. A system that only emits events when something is wrong cannot later distinguish "we checked and there was nothing" from "we never checked" — a real audit-defensibility gap if a later-relevant event (e.g. a storm that later mattered) turns out to have been silently unmonitored.

### NFR-009: Integration boundary discipline — request/display, never compute
Wherever this system depends on a specialist external capability (rating engine, cat model, industry loss index), it shall own the trigger and the display, and never attempt to reproduce the specialist calculation itself. See `13-Integration-Requirements.md` for the full boundary list.
