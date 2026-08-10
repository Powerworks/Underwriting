# Broker Connect (Pine Walk Underwriting Prototype) — Requirements Overview

## Purpose

This requirements set documents a prototype of **Broker Connect**, the AI-enabled placement platform launched by The Fidelis Partnership (TFP) with Howden as first broking partner, extended to cover TFP's full underwriting operating model as delivered through the Pine Walk MGA platform. It is derived directly from a fully-built event model (363 nodes across 11 bounded contexts, 98 Given/When/Then specifications) — every requirement below traces to a specific command, event, read model, or scenario on that model. See `../scenarios.md` for the full narrative scenario catalogue and `../event-model/build-scripts/` for the model's build history.

## Scope

### In scope (confirmed)
Submission intake and ACORD/ADEPT normalization, historic-submission search and claims-lineage retrieval, AI-assisted baseline pricing, underwriting decisioning (authority-checked assessment, referral cascade, quoting), binding (multi-provider quota-share), bordereaux settlement, and claims handling — end to end, matching what TFP's own published materials describe as live today.

### In scope (exploratory extension)
Exposure aggregation and stacking detection, external threat (storm) tracking, portfolio-level underwriting freezes, and catastrophe bond capacity tracking (both index and indemnity triggers). These extend logically from the confirmed scope and from TFP's own published Woody Re / cat bond materials, but are **not confirmed TFP build scope** — flagged as exploratory throughout.

### Explicitly out of scope
- **Pricing & Actuarial computation** — the EBM rating model itself, satellite/weather/loss-history cross-referencing. This system requests and displays a baseline premium; it does not compute it. (Revised from an earlier assumption that pricing was deferred wholesale — see `08-Non-Functional-Requirements.md` and FR-SI-009.)
- **Cat/exposure aggregation compute** (RMS/AIR-style PML calculation) — requested and displayed, never computed here.
- **Reinsurance placement execution** — recorded as intent and handed off to TFP's existing outwards reinsurance function; this system never executes a placement.

## Bounded Contexts

| # | Context | Status | Requirements doc |
|---|---|---|---|
| 0 | Authority Administration | Confirmed | `01-Authority-Administration.md` |
| 1a | Submission Intake | Confirmed | `02-Submission-Intake.md` |
| 1b | Search & Retrieval | Confirmed | `03-Search-Retrieval.md` |
| 2 | Underwriting Decisioning | Confirmed | `04-Underwriting-Decisioning.md` |
| 3 | Binding | Confirmed | `05-Binding.md` |
| 4 | Bordereaux Settlement | Confirmed | `06-Bordereaux-Settlement.md` |
| 5 | Claims | Confirmed | `07-Claims.md` |
| 1c | Exposure Intelligence | Exploratory | `08-Exposure-Intelligence.md` |
| 1d | External Threat | Exploratory | `09-External-Threat.md` |
| 1e | Portfolio Governance | Exploratory | `10-Portfolio-Governance.md` |
| 1f | Capital & Reinsurance Instruments | Exploratory | `11-Capital-Reinsurance-Instruments.md` |

Cross-cutting requirements that don't belong to a single context:
- `12-Non-Functional-Requirements.md`
- `13-Integration-Requirements.md`
- `14-Open-Decisions-Register.md`
- `15-Completeness-Check.md` — field traceability analysis; 2 confirmed gaps found and fixed on the board (GAP-001, GAP-002), 2 more logged as decisions (DEC-029, DEC-030)

## Context Relationships

Two independent gates must clear before a submission can proceed to quote in Underwriting Decisioning: **Authority Administration** (per-underwriter delegated authority) and, in the exploratory extension, **Portfolio Governance** (territory/class freeze status). Binding is the immutable anchor everything downstream keys off — Bordereaux Settlement, Claims, and (exploratory) Exposure Intelligence all consume `PolicyBound`/`PolicyEndorsed`/`PolicyCancelled`. The full context map, including all 12 modeled cross-context relationships, is available on the live board as `MODEL_CONTEXT` nodes and connection edges.

## Actors

| Actor | Contexts |
|---|---|
| Broker (e.g. Howden) | Submission Intake, Underwriting Decisioning (quote accept/decline/amend), Claims (notification) |
| Underwriter | Underwriting Decisioning, Binding, Search & Retrieval |
| Cell Head Underwriter / CUO | Authority Administration |
| Underwriting Governance / senior executive | Authority Administration |
| Operations / Finance | Submission Intake (exception handling), Bordereaux Settlement |
| Claims handler | Claims, Search & Retrieval (lineage retrieval) |
| Capacity Provider (or TPA) | Authority Administration, Bordereaux Settlement |
| Portfolio manager / executive | Portfolio Governance |
| External systems (rating engine, NHC/weather feed, industry loss index provider) | Submission Intake, External Threat, Capital & Reinsurance Instruments |
| Capital markets / outwards reinsurance team | Capital & Reinsurance Instruments, Portfolio Governance |
| Actuarial / claims audit function | Capital & Reinsurance Instruments (indemnity trigger auditing) |

## Requirement Numbering

Each functional requirement is numbered `FR-<CTX>-<NNN>` where `<CTX>` is a short context code (AA, SI, SR, UD, BND, BS, CLM, EI, ET, PG, CRI). Every requirement cites its source scenario ID (e.g. `S2.1`) and the underlying board element names (`Command`, `Event`, `Read Model`) for direct traceability back to the event model. Non-functional requirements are numbered `NFR-<NNN>`; integration requirements `IR-<NNN>`.
