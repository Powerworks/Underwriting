# Context 1c — Exposure Intelligence: Functional Requirements

**Status:** Exploratory — not confirmed TFP build scope. **Board chapter:** `Exposure Intelligence`.

## Overview

Largely event-driven off Binding (Context 3) rather than user commands — the "actor" for most of this context is the system reacting to a bind/endorsement/cancellation elsewhere. This context establishes the "ingest/display, not compute" integration-boundary pattern that recurs through External Threat and Portfolio Governance (see `13-Integration-Requirements.md`).

## Actors

System (automated, reacting to Binding events), exposure management/actuarial team.

## Functional Requirements

### FR-EI-001: Update exposure projection on bind
**Source:** S1c.1 — `UpdateExposureProjectionOnBind` → `ExposureProjectionUpdated`
The system shall, on every `PolicyBound` event, update the exposure projection at the relevant geocode with a signed delta (not merely a restated total), so the projection's own history remains auditable. This shall run asynchronously — the underwriter shall never wait on this update at bind time.

### FR-EI-002: Provide a geographic exposure map
**Source:** S1c.1 — Read Model `GeographicExposureMap`
The system shall provide a running aggregate exposure view by geocode/region, queryable by peril, class, or provider.

### FR-EI-003: Detect exposure stacking across cells
**Source:** S1c.2 — `DetectExposureStacking` → `ExposureConcentrationWarningRaised`
The system shall evaluate every exposure projection update against a configured concentration threshold and raise a warning when combined exposure at a geocode/region crosses it, identifying all contributing cells and policies. Each warning shall be assigned a generated identifier at the point it's raised (fixed 2026-08 — see `15-Completeness-Check.md` GAP-002: the warning-acknowledgment requirement, FR-EI-004, always needed one but no event previously generated it).

### FR-EI-004: Require explicit acknowledgment of concentration warnings
**Source:** S1c.2 — `ExposureConcentrationWarningAcknowledged`
The system shall require an explicit acknowledgment/escalation action for a raised concentration warning — "warned" and "ignored" shall never be indistinguishable in the audit trail.

### FR-EI-005: Provide an exposure concentration dashboard
**Source:** S1c.2 — Read Model `ExposureConcentrationDashboard`
The system shall provide underwriting executives and portfolio managers (not individual cell underwriters) a cross-cell view of active concentration warnings.

### FR-EI-006: Update exposure projection on endorsement
**Source:** S1c.3 — `UpdateExposureProjectionOnEndorsement`
The system shall update the exposure projection as a delta on every material `PolicyEndorsed` event, capable of re-triggering stacking detection (FR-EI-003).

### FR-EI-007: Update exposure projection on cancellation
**Source:** S1c.4 — `UpdateExposureProjectionOnCancellation`
The system shall update the exposure projection with a full-removal negative delta on every `PolicyCancelled` event.

### FR-EI-008: Export exposure data for cat modeling
**Source:** S1c.5 — `ExportExposureExtract` → `ExposureExtractGenerated`
The system shall produce a correct, complete exposure extract in the cat-modeling vendor's schema, scoped by peril/region/cell/provider/as-of-date, on request. Production of the extract is this system's full responsibility for this integration — see IR-002.

### FR-EI-009: Maintain an audit trail of exposure extracts
**Source:** S1c.5 — Read Model `ExposureExtractHistory`
The system shall record what was sent to cat modeling, when, and covering what scope, to support reconciling "what did the cat model actually see" if a PML figure is later questioned.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-021 (geocode precision — grid/hex bucket vs. exact coordinates), DEC-022 (concentration threshold ownership and seasonality), DEC-023 (exposure date-awareness — as-of-today vs. any-future-date).
