# Project Plan — Broker Connect (Pine Walk Underwriting Prototype)

## 1. Document Purpose
This plan defines scope, phasing, timeline, team structure, and risks for building a .NET modular monolith backend (event-sourced vertical slice architecture, Marten/PostgreSQL, Wolverine/RabbitMQ) with a Blazor Server frontend, implementing the 11-context event model already built on the eventmodelers board (363 nodes, 98 GWT scenarios, 14-role Role Catalog, 11 designed screens).

## 2. Assumptions

| # | Assumption |
|---|---|
| A1 | Modules in scope: the 11 board contexts exactly (Authority Administration, Submission Intake, Search & Retrieval, Underwriting Decisioning, Binding, Bordereaux Settlement, Claims, Exposure Intelligence, External Threat, Portfolio Governance, Capital & Reinsurance Instruments) |
| A2 | Target runtime: .NET 10 (LTS) — Marten/Wolverine's current major versions require net9.0/net10.0 |
| A3 | Blazor Server is the frontend (ADR-009), hosted inside the same solution |
| A4 | Single Postgres cluster, one Marten schema per module |
| A5 | Deployment target: Kubernetes from day one (ADR-011) — org's existing cluster/cloud platform if available, provisioned fresh otherwise |
| A6 | Team size: 6–9 engineers — larger than a typical consumer-MVP team given 11 modules, 6 external integrations, and a compliance-heavy domain (2–3 backend, 1 frontend/Blazor, 1 DevOps/platform, 1–2 QA, 1 shared PM/tech lead) |
| A7 | Six real-time/near-real-time external integrations (IR-001–IR-006) are stubbed/mocked through most of the build and swapped for real endpoints once TFP provides access — this is explicitly called out because it's a bigger integration surface than a typical greenfield project and a common source of schedule risk if treated as an afterthought |

## 3. Objectives
- Deliver the **confirmed scope** (Contexts 0, 1a, 1b, 2, 3, 4, 5 — Authority Administration through Claims) as a working, auditable underwriting platform matching what TFP's Broker Connect already does in production, per the source material.
- Establish the event-sourced architecture, `BuildingBlocks.Governance`, and `Money` value object early enough that the **exploratory scope** (Contexts 1c–1f) can be added later without rework — these were designed together on the board specifically so this would be true.
- Keep the event store itself as the audit trail (ADR-014) from day one, not retrofitted once compliance asks for one.
- Establish a repeatable, automated path from commit → container image → deployed environment, with architecture fitness tests enforcing the module-boundary and command-state rules (ADR-004/ADR-005) from the first module onward, not introduced after a violation is found.

## 4. Scope

### In scope — Phase A (confirmed contexts, target MVP)
- Authority Administration: cell/underwriter authority grant, revision, revocation, escalation
- Submission Intake: broker submission, ACORD ADEPT normalization, AI baseline pricing (IR-001, IR-005)
- Search & Retrieval: submission search, automatic claims-lineage retrieval
- Underwriting Decisioning: assessment, referral cascade, quoting
- Binding: bind, endorse, cancel, renew
- Bordereaux Settlement: period-close drafting, query/resolution, agreement, settlement
- Claims: notify, reserve, pay, close, reopen
- 11 screens already designed on the board (`Requirements/00-Overview.md` §Actors) — implemented as Blazor pages
- CI/CD pipeline, container images, IaC for at least one environment (staging) + production promotion

### In scope — Phase B (exploratory contexts, contingent on Phase A + a real go/no-go decision)
- Exposure Intelligence, External Threat, Portfolio Governance, Capital & Reinsurance Instruments (IR-002, IR-003, IR-004, IR-006)
- **Not started until TFP explicitly confirms this extension is real build scope** — these were modeled as a speculative but logically-grounded extension (`scenarios.md`'s own framing), not confirmed requirements. Starting Phase B work before that confirmation risks building against assumptions that were flagged, not validated.

### Out of scope (both phases)
- The rating engine / EBM risk segmentation model itself (IR-005) — this system requests and displays, never computes
- The cat model / PML calculation itself (IR-002/IR-003) — same boundary
- Reinsurance placement execution (IR-004) — intent recorded, handoff only
- Native mobile apps
- Full payment processing beyond what bordereaux settlement/claims payment already model as domain events (no card/payment-gateway integration — this domain's money movement is bank transfer between TFP and capacity providers, not consumer payments)

## 5. Team & Roles

### 5.1 Engineering team

| Role | Responsibility |
|---|---|
| Tech Lead / Architect | Module boundaries, ADR log ownership, code review gate on cross-module changes, owns `BuildingBlocks.Governance`/`BuildingBlocks.Domain.Money` |
| Backend Engineers (2–3) | Vertical slices per module, Marten event streams, Wolverine handlers |
| Frontend Engineer | Blazor pages (11 screens), authorization policy wiring per the Role Catalog |
| DevOps/Platform Engineer | Dockerfiles, Kubernetes manifests/Helm, CI/CD pipeline, observability stack |
| QA Engineer(s) | Test strategy, integration/E2E suites incl. the 98 GWT scenarios as acceptance criteria, release sign-off |
| Product/PM | Backlog, phase sign-off, owns driving the 34-item Open Decisions Register to closure |

### 5.2 Domain stakeholders (not engineering headcount — UAT, sign-off, and decision-register owners)

Directly from the board's Role Catalog (`Requirements/00-Overview.md`) — each of these 10 human roles needs a real TFP/Pine Walk counterpart engaged for UAT on their own screen and for closing the decisions that affect their workflow:

| Role | Primary decision-register items owned |
|---|---|
| Underwriting Governance / Senior Executive | DEC-001–005 (Authority Administration design questions) |
| Cell Head Underwriter / CUO | DEC-002, DEC-015 |
| Underwriter | UAT on `SubmissionQueue`/`SubmissionAssessment` |
| Operations / Finance | DEC-006–011 (Submission Intake), DEC-018 (Bordereaux) |
| Claims Handler | DEC-019, DEC-020 |
| Capacity Provider (or TPA) | UAT on `BordereauProviderReview` |
| Portfolio Manager | DEC-024–027 (Portfolio Governance — Phase B) |
| Capital Markets / Outwards Reinsurance Team | DEC-028 (Phase B) |
| Actuarial / Claims Audit Function | Reporting-tier questions, DEC-028 audit process (Phase B) |
| Compliance | DEC-034 (data privacy/retention) |

## 6. Phased Delivery Plan

Phasing follows the dependency order the context map itself already establishes (Authority Administration and Submission Intake gate Decisioning; Decisioning gates Binding; Binding gates Bordereaux/Claims) — this isn't an arbitrary schedule, it's the same dependency graph as the 12 `MODEL_CONTEXT` connection edges on the board.

| Phase | Name | Duration | Goal |
|---|---|---|---|
| 0 | Discovery & Architecture | 3 weeks | Finalize ADR log, close build-blocking decision-register items (DEC-031–034), environment strategy, repo scaffolding, `build-kit-dotnet-es` validated against this board |
| 1 | Platform Foundation | 3 weeks | Solution skeleton, Marten/Postgres, Wolverine/RabbitMQ, `BuildingBlocks.Governance`, `BuildingBlocks.Domain.Money`, Docker Compose dev env, base CI pipeline with architecture fitness tests from slice one |
| 2 | Authority Administration | 2 weeks | The gate everything else depends on — build and stabilize first |
| 3 | Submission Intake + Search & Retrieval | 4 weeks | Broker intake, ACORD normalization, AI pricing integration (stubbed IR-005 initially), search/lineage capability |
| 4 | Underwriting Decisioning | 5 weeks | The branchiest module — two-gate check, referral cascade via `BuildingBlocks.Governance`, quoting. Largest single-module estimate, reflects the scenario count (10 vs. 4–5 elsewhere) |
| 5 | Binding + Bordereaux Settlement | 4 weeks | Immutable ledger, quota-share allocation model (reused downstream), period-close settlement cycle |
| 6 | Claims | 3 weeks | Notify-reserve-pay-close-reopen, bind-terms validation via Search & Retrieval's lineage view |
| 7 | Hardening, UAT, Launch Prep (Phase A) | 3 weeks | Full confirmed-scope regression against the 98 GWT scenarios, UAT per stakeholder role, staging soak test, go-live checklist |

**Phase A total: ~27 weeks (≈6.5 months)** with the assumed team size. 15% contingency recommended given the external-integration surface (A7) and the volume of still-open decisions (34 items) relative to a typical greenfield project.

**Phase B (exploratory, contingent on a go/no-go decision after Phase A)**:

| Phase | Name | Duration | Goal |
|---|---|---|---|
| 8 | Exposure Intelligence + External Threat | 4 weeks | Reactive exposure tracking off Binding events, external storm feed ingestion, PML recalculation integration (IR-002, IR-003) |
| 9 | Portfolio Governance + Capital & Reinsurance Instruments | 4 weeks | Freeze/override mechanism (DEC-024/026/027 must be closed before this phase, not during it), cat bond tracking, indemnity audit workflow (IR-004, IR-006) |
| 10 | Hardening, UAT, Launch Prep (Phase B) | 2 weeks | Full exploratory-scope regression, stakeholder UAT, staged rollout (feature-flagged, per ADR-015) |

**Phase B total: ~10 weeks**, starting only after an explicit decision to build it, not scheduled by default.

## 7. Deliverables per Phase

| Phase | Key Deliverables |
|---|---|
| 0 | ADR log finalized for build-blocking items, all Phase-A-relevant open decision items closed, ready repo + `build-kit-dotnet-es` wired to the board |
| 1 | Buildable solution, docker-compose for local infra, first green CI pipeline, `BuildingBlocks` shared libraries, base module template validated end-to-end |
| 2 | Authority Administration module: grant/revise/revoke, `AuthorityMatrix` live |
| 3 | Submission Intake + Search & Retrieval modules, `SubmissionQueue`/`SubmissionAssessment`/lineage screens |
| 4 | Underwriting Decisioning module, `QuoteView` screen, referral cascade proven against a multi-tier scenario |
| 5 | Binding + Bordereaux Settlement modules, `BordereauSettlementWorkbench`/`BordereauProviderReview` screens |
| 6 | Claims module, `ClaimHandling` screen, bind-terms validation proven end-to-end |
| 7 | Production deployment pipeline, runbooks, go-live checklist, all Phase A UAT sign-offs |
| 8–9 | Exploratory modules, remaining 5 screens, all IR-002/003/004/006 integrations live |
| 10 | Feature-flagged production rollout of Phase B, updated runbooks |

## 8. Risks & Mitigations

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Build-blocking decisions (DEC items) not closed before the phase that needs them | High | High | Phase 0 explicitly scoped to close Phase-A-blocking items; PM owns the register, not engineering — see Section 5.1 |
| Module boundaries leak (tight coupling reintroduced) | High | Medium | Architecture fitness tests (ADR-017) in CI from Phase 1, not retrofitted |
| `BuildingBlocks.Governance` becomes a shared-aggregate anti-pattern instead of a generic workflow | High | Medium | Explicit code review checklist item per ADR-006/ADR-005; the K9Crush precedent this pattern is based on documents exactly this failure mode and its fix |
| External integration (IR-001–006) endpoints not available from TFP until late in the schedule | High | High (per A7) | Build against stubs/mocks matching the documented contract shape from `Requirements/13-Integration-Requirements.md` from Phase 2 onward; swap for real endpoints as they become available, not gate module completion on external readiness |
| Marten event-sourcing learning curve for the team | Medium | Medium | Phase 1 spike using `build-kit-dotnet-es`'s own documented gotchas (`AGENT.md`) as a starting reference; pair programming on the first module (Authority Administration) |
| Multi-currency/FX handling (`Money`, ADR-007) implemented inconsistently across modules | Medium | Medium | `BuildingBlocks.Domain.Money` built and code-reviewed in Phase 1, before any module that touches money is built |
| Portfolio Governance's freeze mechanism (Phase B) ships with an implicit "automatic" default rather than a deliberate one | High | Medium | DEC-024 explicitly gated as a required stakeholder decision before Phase 9 starts, not a default engineers pick under schedule pressure |
| Scope creep from "the article mentioned more" (per the S1a.5/1f precedent, real new scope arrived mid-session during modeling) | Medium | Medium | New scope goes through the same draft-then-build discipline used throughout modeling, not straight into a sprint |
| Regulatory/compliance requirements (DEC-034) surface late and require rework | High | Medium | Compliance stakeholder engaged from Phase 0, not Phase 7 |

## 9. Definition of Done (per module)
- Vertical slices implemented with unit + integration tests
- Every GWT scenario for the module's context (from the 98 on the board) passes as an acceptance test
- Marten event stream/projection schema documented in the module's README
- Published/consumed integration events documented, matching the context-map edges already on the board
- OpenAPI spec generated and reviewed
- Passes architecture fitness tests in CI (ADR-017)
- Relevant decision-register items (Section 5.2 table) closed, not just noted
- Stakeholder UAT sign-off from the role(s) whose screen(s) the module serves

## 10. Success Metrics (Phase A)
- P95 API latency < 300ms for read endpoints (excluding external-integration-dependent calls)
- Zero cross-module compile-time references outside published `Contracts`
- 98/98 GWT scenarios passing as automated acceptance tests before Phase 7 sign-off
- Every Phase-A-relevant decision-register item (Section 5.2) closed with a recorded rationale, not defaulted silently
- Pipeline: commit-to-staging-deploy < 15 minutes
