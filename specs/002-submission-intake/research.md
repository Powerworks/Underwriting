---
spec: 002-submission-intake
phase: research
created: 2026-08-10
---

# Research: Submission Intake

## Executive Summary

This spec is sourced directly from the "BrokerConnect" event-modeling board's **Submission Intake** context, not free-form research — the domain (commands, events, read models, and screens) is board-authored, not derived here. This file exists to satisfy `/ralph-specum:design`'s context-gathering step (it reads `research.md` if present), not because external/codebase research was actually performed for this feature.

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
| Technical Viability | High | Domain already modeled end-to-end on the board (12 slices) |
| Effort Estimate | See `tasks.md` once generated | Not estimated here — board doesn't carry effort data |
| Risk Level | Low | Board-sourced, not speculative |

## Recommendations for Requirements

1. Treat `requirements.md`'s Event Model Detail appendix as the literal field/event source — no invented fields, per constitution Principle III.
2. Follow the same Marten/Wolverine conventions as `001-authority-administration` (`build-state-change`/`build-state-view` skills) — this is the same tech-kit, not a new stack decision.

## Open Questions

- `SubmissionManuallyCorrected` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.
- `PotentialDuplicateSubmissionDetected` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.
- `SubmissionSuperseded` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.
- `SubmissionConfirmedDistinct` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.
- `PricingBaselineAccepted` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.
- `PricingBaselineOverridden` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.
- `PricingModelVersionDeployed` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.

## Sources

- `event-model/import-config.json` — live pull from the "BrokerConnect" eventmodelers.ai board, chapter **Broker Connect**, context **Submission Intake**, as of 2026-08-10.

## UI Reference (event-modeling board — for Design phase)

Screens from the board export, for `architect-reviewer` to fold into `design.md`'s own `## Screens` section — map each screen's displayed fields back to the API/read-model surface that has to support it. Not a requirement; UI reference only (decided while adapting this generator for the Ralph Specum workflow: screens belong in design, not requirements).

_(from slice: SubmissionNormalized)_

**SubmissionQueue** (screen, id `dc1e9bec-a64d-49b3-b0ef-e7bf28866adf`, aggregate `—`, lane `Actor`, modelContext `Submission Intake`)

> Underwriter's Submission Queue screen: a violet header bar reads 'Submission Queue - itasca-mga' with a search input top-right. Below is a column-header row (Insured, Class, Line Size, Received, Status) over a divider line, followed by three submission rows: Acme Aviation Leasing Ltd (\$10.0M, Aircraft Non-Payment Credit) with a green 'Ready' badge; Meridian Port Holdings (\$25.0M, cat-exposed Property) with an orange 'Possible Dup' badge; and Blackstone Grid Energy LLC (\$8.5M, Energy Liability) with a green 'Ready' badge. A light-violet footer bar summarizes '3 submissions - 1 flagged as possible duplicate'.

Dependencies: ← SubmissionQueue (READMODEL)

_(no fields)_

