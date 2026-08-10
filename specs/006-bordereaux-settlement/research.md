---
spec: 006-bordereaux-settlement
phase: research
created: 2026-08-10
---

# Research: Bordereaux Settlement

## Executive Summary

This spec is sourced directly from the "BrokerConnect" event-modeling board's **Bordereaux Settlement** context, not free-form research — the domain (commands, events, read models, and screens) is board-authored, not derived here. This file exists to satisfy `/ralph-specum:design`'s context-gathering step (it reads `research.md` if present), not because external/codebase research was actually performed for this feature.

## External Research

N/A — requirements are board-sourced, not derived from external research. See `requirements.md`.

## Codebase Analysis

N/A — deferred to the design phase's own codebase exploration (`architect-reviewer`).

## Related Specs

| Spec | Relevance | Relationship | May Need Update |
|------|-----------|--------------|-----------------|
| _(not automatically cross-referenced)_ | — | See this context's Event Model Detail appendix in `requirements.md` for raw dependency edges | — |

## Feasibility Assessment

| Aspect | Assessment | Notes |
|--------|------------|-------|
| Technical Viability | High | Domain already modeled end-to-end on the board (9 slices) |
| Effort Estimate | See `tasks.md` once generated | Not estimated here — board doesn't carry effort data |
| Risk Level | Low | Board-sourced, not speculative |

## Recommendations for Requirements

1. Treat `requirements.md`'s Event Model Detail appendix as the literal field/event source — no invented fields, per constitution Principle III.
2. Follow the same Marten/Wolverine conventions as `001-authority-administration` (`build-state-change`/`build-state-view` skills) — this is the same tech-kit, not a new stack decision.

## Open Questions

- None

## Sources

- `event-model/import-config.json` — live pull from the "BrokerConnect" eventmodelers.ai board, chapter **Broker Connect**, context **Bordereaux Settlement**, as of 2026-08-10.

## UI Reference (event-modeling board — for Design phase)

Screens from the board export, for `architect-reviewer` to fold into `design.md`'s own `## Screens` section — map each screen's displayed fields back to the API/read-model surface that has to support it. Not a requirement; UI reference only (decided while adapting this generator for the Ralph Specum workflow: screens belong in design, not requirements).

_(no screens populated on this context's slices in the source export)_

