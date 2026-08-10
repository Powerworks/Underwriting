# Context 1d — External Threat: Functional Requirements

**Status:** Exploratory — not confirmed TFP build scope. **Board chapter:** `External Threat`.

## Overview

The first context in the whole model where the trigger isn't internal — an external feed (e.g. National Hurricane Center) pushes data on its own schedule. Unlike a broker submission, a missed storm update has no accountable party on the sending side, so the ingestion boundary needs its own reliability/staleness handling as a first-class concern, not an afterthought.

## Actors

External system (NHC or commercial weather/cat data feed), automated PML recalculation process.

## Functional Requirements

### FR-ET-001: Ingest storm track updates
**Source:** S1d.1 — `IngestExternalThreatUpdate` → `StormTrackUpdateReceived`
The system shall ingest storm track updates from an external feed, trusting the source feed's own persistent threat ID for continuity across updates rather than inventing custom storm-matching logic.

### FR-ET-002: Detect and surface feed staleness
**Source:** S1d.1 — `MonitorThreatFeedHealth` → `ThreatFeedStale`
The system shall monitor time-since-last-update per source feed against an expected cadence and raise an explicit staleness alert when exceeded, so leadership is never silently looking at an outdated exposure map without knowing it's outdated. This requirement is at least as important as the happy-path ingestion itself.

### FR-ET-003: Maintain an active threat register
**Source:** S1d.1 — Read Model `ActiveThreatRegister`
The system shall provide a list of currently tracked live threats with latest-update timestamp and status (including stale status, FR-ET-002).

### FR-ET-004: Recalculate PML on threat or exposure change
**Source:** S1d.2 — `RecalculatePMLOnThreatUpdate` → `PMLRecalculated`
The system shall recalculate modeled PML whenever a storm track updates or, separately, when exposure changes at a geocode within an already-tracked threat's path — two independent triggers producing the same event, not two separate mechanisms.

### FR-ET-005: Request, never compute, PML figures
**Source:** S1d.2
The system shall request the PML calculation from a specialist cat-modeling capability and record/display the result; it shall never compute storm physics against policy terms itself. See IR-003.

### FR-ET-006: Record zero-impact assessments explicitly
**Source:** S1d.2 (S1d.4 in original scenario numbering) — `PMLRecalculated` with `hasImpact=false`
The system shall record an explicit zero/null-impact PML result when a tracked storm has no intersecting bound policies, rather than producing no event — distinguishing "checked, no exposure" from "never checked" in the audit trail.

### FR-ET-007: Provide the live threat exposure view
**Source:** S1d.2 — Read Model `ThreatExposureView`
The system shall provide the live "storm overlaid on policy map" view, showing modeled loss by scenario.

### FR-ET-008: Resolve threats and stand down dependent warnings
**Source:** S1d.3 — `ThreatResolved`, follow-on `ExposureAtRiskCleared`
The system shall record when a tracked storm dissipates, makes landfall and passes, or downgrades below a tracking threshold, and explicitly stand down any concentration warning or portfolio freeze tied to that specific threat rather than letting it silently lapse.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: none unique to this context beyond DEC-021/DEC-022 (shared with Exposure Intelligence).
