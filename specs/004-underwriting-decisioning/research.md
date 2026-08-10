---
spec: 004-underwriting-decisioning
phase: research
created: 2026-08-10
---

# Research: Underwriting Decisioning

## Executive Summary

This spec is sourced directly from the "BrokerConnect" event-modeling board's **Underwriting Decisioning** context, not free-form research — the domain (commands, events, read models, and screens) is board-authored, not derived here. This file exists to satisfy `/ralph-specum:design`'s context-gathering step (it reads `research.md` if present), not because external/codebase research was actually performed for this feature.

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
| Technical Viability | High | Domain already modeled end-to-end on the board (14 slices) |
| Effort Estimate | See `tasks.md` once generated | Not estimated here — board doesn't carry effort data |
| Risk Level | Low | Board-sourced, not speculative |

## Recommendations for Requirements

1. Treat `requirements.md`'s Event Model Detail appendix as the literal field/event source — no invented fields, per constitution Principle III.
2. Follow the same Marten/Wolverine conventions as `001-authority-administration` (`build-state-change`/`build-state-view` skills) — this is the same tech-kit, not a new stack decision.

## Open Questions

- `ReferralDeclined` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.
- `PendingReferralReassigned` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.
- `PendingReferralHeld` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.
- `ComplianceNotifiedOfSanctionsDecline` has no `sliceType` in the source export — kind was inferred as FACT_ONLY; confirm this is correct.

## Sources

- `event-model/import-config.json` — live pull from the "BrokerConnect" eventmodelers.ai board, chapter **Broker Connect**, context **Underwriting Decisioning**, as of 2026-08-10.

## UI Reference (event-modeling board — for Design phase)

Screens from the board export, for `architect-reviewer` to fold into `design.md`'s own `## Screens` section — map each screen's displayed fields back to the API/read-model surface that has to support it. Not a requirement; UI reference only (decided while adapting this generator for the Ralph Specum workflow: screens belong in design, not requirements).

_(from slice: SubmissionWithinAuthority)_

**SubmissionAssessment** (screen, id `ba30b993-cb53-480f-8f41-8a599dfd2dde`, aggregate `—`, lane `Actor`, modelContext `Underwriting Decisioning`)

> Submission Assessment screen: violet header 'Assess Submission - Acme Aviation Leasing Ltd'. Two side-by-side panels below: a light-blue Submission panel (left) listing class, territory, proposed line size (\$10,000,000), premium (\$185,000), and effective date; a light-green Your Authority panel (right, versioned v3) showing the underwriter's limit (\$250,000 / \$15,000,000) against the proposed terms, a green checkmark reading 'Within authority', and a note that the freeze check passed. Two buttons at the bottom: green 'Approve & Quote' and light-red 'Decline'.

Dependencies: ← AuthorityLimit (READMODEL)

_(no fields)_

_(from slice: QuoteIssued)_

**QuoteView** (screen, id `8c28ebd0-b1b5-42c0-90ac-a1770b9c4a58`, aggregate `—`, lane `Actor`, modelContext `Underwriting Decisioning`)

> Broker's Quote screen: violet header 'Quote QT-2026-0912 - Howden'. A central light-violet card shows the insured (Acme Aviation Leasing Ltd), class (Aircraft Non-Payment Credit), line size (\$10,000,000), a large premium figure (\$185,000), and a validity note ('Valid until 2026-09-15, 14 days'). Three buttons below the card: green 'Accept Quote', yellow 'Request Amendment', and light-red 'Decline'.

Dependencies: → IssueQuote (COMMAND); → AcceptQuote (COMMAND)

_(no fields)_

