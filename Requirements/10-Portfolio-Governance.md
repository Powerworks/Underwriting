# Context 1e — Portfolio Governance: Functional Requirements

**Status:** Exploratory — not confirmed TFP build scope. **Board chapter:** `Portfolio Governance`.

## Overview

Where the two independent gates into Underwriting Decisioning become concrete. This context consumes a computed PML/threshold signal from a specialist capability and reacts to it operationally (freeze, block, unfreeze, escalate) — it never computes the signal itself.

## Actors

Automated threshold evaluation process, underwriter (blocked party), senior executive (override authority), portfolio manager.

## Functional Requirements

### FR-PG-001: Freeze underwriting on threshold breach
**Source:** S1e.1 — `EvaluatePortfolioThreshold` → `TerritoryUnderwritingFrozen`
The system shall evaluate every PML recalculation and, separately, periodic Combined Ratio assessment against configured thresholds, and freeze underwriting for the affected territory/class/cells when breached. A freeze shall record every active concurrent trigger reason (not assume a single cause) and shall only lift once that set is empty (FR-PG-004).

### FR-PG-002: Block gated actions during an active freeze
**Source:** S1e.2 — `SubmissionBlockedByPortfolioFreeze`
The system shall intercept and block any Underwriting Decisioning action (e.g. quote issuance) attempted against a submission whose territory/class is subject to an active freeze, presenting a block reason distinguishable from a personal-authority referral — "you're not authorized" and "this territory is frozen regardless of who you are" are different facts and shall be presented as such.

### FR-PG-003: Maintain an active freeze register
**Source:** S1e.1 — Read Model `ActiveFreezeRegister`
The system shall provide underwriting executives, and underwriters in affected cells at the point of decisioning, a view of all currently active freezes.

### FR-PG-004: Lift a freeze only when all trigger causes have cleared
**Source:** S1e.3 — `TerritoryUnderwritingFrozenLifted`
The system shall lift a freeze only when every concurrent trigger reason (FR-PG-001) has resolved, not on any single cause clearing alone.

### FR-PG-005: Support executive override with mandatory justification
**Source:** S1e.4 — `OverridePortfolioFreeze` → `PortfolioFreezeOverridden`
The system shall allow a senior executive to override an active freeze despite an ongoing breach, requiring mandatory (not optional) written justification and supporting an optional co-signer field for four-eyes control. The overridden state shall be permanently and explicitly distinguishable in the audit trail from a genuine threshold-clear lift (FR-PG-004) — never conflated.

### FR-PG-006: Record hedging action requests as intent, hand off execution
**Source:** S1e.5 — `RequestHedgingAction` → `HedgingActionRequested`
The system shall allow a portfolio manager to record a hedging action request tied to a specific freeze/breach, and hand it off to TFP's existing outwards reinsurance function. This system shall never execute the placement itself — see IR-004.

### FR-PG-007: Maintain a portfolio action log
**Source:** S1e.5 — Read Model `PortfolioActionLog`
The system shall provide the reinsurance/outwards placement team a log of hedging action requests and their handoff status.

## Related Open Decisions
See `14-Open-Decisions-Register.md`: DEC-024 (automatic freeze vs. human-confirmed alert-and-recommend), DEC-025 (auto-resume-with-revalidation vs. active resubmission for blocked submissions), DEC-026 (override authority list — configurable vs. fixed short list), DEC-027 (mandatory four-eyes on override).
