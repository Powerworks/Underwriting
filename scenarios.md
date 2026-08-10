# Pine Walk Underwriting — Scenario Catalogue

High-level scenarios describing the system this project prototypes: **Broker Connect** — a placement-streamlining platform (per the Fidelis Partnership / Howden launch) covering submission intake, search/retrieval, and (as a confirmed core) underwriting decisioning → binding → bordereaux settlement → claims. Pricing & actuarial is deliberately excluded — Broker Connect defers it to "a subsequent phase," and so does this prototype.

Contexts **1c, 1d, and 1e are exploratory** — a speculative extension of the confirmed scope (0, 1a, 1b, 2–5), following the same "ingestion → exposure → threat → governance" narrative the confirmed contexts already establish. They are not confirmed TFP build scope.

Each scenario names its actor(s), the command that triggers it, the event(s) it produces, the read model it feeds (if any), a given/when/then, and open design questions raised but not always resolved. Command and event names are written in `PascalCase` for easy extraction. This numbering (`0`, `1a`–`1e`, `2`–`5`) reflects how the model was actually built up in conversation — Context 1 expanded into five sub-contexts once Search & Retrieval and the exploratory extension were folded in.

---

## 0. Authority Administration

Governance/reference data underpinning the Provider → Cell → Underwriter authority cascade Context 2 checks against. Reference/configuration data, not transactional flow — the actor throughout is typically a Head of Class, CUO, or governance function, not someone processing a risk. **Turned out to be a cross-cutting capability, not scoped only to underwriting decisioning** — see Cross-Cutting Principles below.

### S0.1 Cell granted a new authority limit (initial setup)

**Actor:** Underwriting Governance function or senior executive (tied to the Delegated Underwriting Authority Agreement with the relevant capacity provider).

- **Command:** `GrantCellAuthorityLimit` (cellId, grantingParty, sourceAgreementReference, authorityScope: {classesOfBusiness, territory, maxLineSize, maxAggregate}, currency, effectiveDate)
- **Event:** `CellAuthorityLimitGranted` (authorityLimitId, cellId, authorityScope, sourceAgreementReference, grantedBy, effectiveDate, **version** — versioned from the first event, not bolted on later)
- **Read model:** `CellAuthorityRegister` — current and historical authority limits per cell, queryable at any point in time
- **Given/When/Then:** Given a new capacity provider agreement is signed for a cell, when governance grants the cell's authority limit under that agreement, then `CellAuthorityLimitGranted` is recorded, versioned, and becomes the ceiling all underwriter-level grants within that cell must fit inside.
- **Open questions (unresolved):** Does a cell's limit need to reference which capacity provider(s) it derives from if backed by multiple providers with different limits — possibly multiple concurrent grants per cell, not a single number? Is granting purely internal (TFP governance), or does it require the capacity provider's own sign-off captured as part of the event?

### S0.2 Underwriter granted authority within their cell's limit (accepted)

**Actor:** Head of Class / Cell CUO.

- **Command:** `GrantUnderwriterAuthorityLimit` (underwriterId, cellId, requestedScope, grantingAuthority)
- **Event:** `UnderwriterAuthorityLimitGranted` (authorityLimitId, underwriterId, cellId, scope, grantedBy, effectiveDate, version, validationResult="within cell's own limit")
- **Read models:** `UnderwriterAuthorityRegister` (current + historical per underwriter) and `AuthorityMatrix` — the view Context 2's `AssessSubmission` actually queries at decision time
- **Given/When/Then:** Given the cell's own authority permits up to $20m line size in Property, when the Cell CUO grants an underwriter $10m, then `UnderwriterAuthorityLimitGranted` is recorded — this is the exact record `AssessSubmission` checks against.
- **Open question (unresolved):** Does validation need to account for other underwriters' existing grants — an aggregate pool per cell vs. each underwriter's limit being independent (a per-transaction ceiling)? Real domain question for governance stakeholders.

### S0.3 Underwriter authority request rejected — exceeds cell's own limit

- **Command:** `GrantUnderwriterAuthorityLimit` (same command, different outcome)
- **Event:** `UnderwriterAuthorityLimitRejected` (underwriterId, cellId, requestedScope, rejectionReason — by how much, attemptedBy)
- **Follow-on:** `CellAuthorityIncreaseRequested` (via `RequestCellAuthorityIncrease`) — the escalation path modeled explicitly rather than a pure dead end, looping back into a variant of S0.1
- **Given/When/Then:** Given the cell's ceiling is fully allocated across existing underwriters, when the Cell CUO attempts to grant a new underwriter authority, then `UnderwriterAuthorityLimitRejected` fires; the Cell CUO can either reallocate existing underwriters' authority or raise `CellAuthorityIncreaseRequested`.

### S0.4 Authority limit revised downward mid-cycle

- **Command:** `ReviseAuthorityLimit` (target: cell|underwriter, newMaxGrossPremium, newMaxLimit, revisedBy, reason, effectiveDate)
- **Event:** `AuthorityLimitRevised` (authorityLimitId, previousLimit, newLimit, reason, revisedBy, effectiveDate, **version increment**)
- **The sharpest open question in this context:** what happens to a submission already in-flight (post-1a, pre-bind), assessed against the old limit, if the revision drops below what it needs? Grandfather (evaluate against authority in effect when it entered decisioning) vs. re-evaluate (immediately re-check, force referral if it now breaches) are both defensible. **Resolved as a mechanism, not as a policy stance**: `AuthorityLimitRevised` always triggers `ReassessInFlightSubmissionsOnRuleChange`, producing `InFlightSubmissionReassessed` with a `policyApplied` field ("grandfathered" | "re-evaluated") — the decision and its trigger are both visible in the audit trail regardless of which stance TFP configures. Leaning toward re-evaluation as the safer default for a governance-critical system, but this is a business decision, not an architecture default.

### S0.5 Authority revoked entirely (e.g. underwriter leaves/is suspended)

- **Command:** `RevokeAuthorityLimit` (target, reason, revokedBy, immediacy)
- **Event:** `AuthorityLimitRevoked` (authorityLimitId, target, priorLimit, reason, revokedBy, **immediacy** — pushed for the stricter, immediate interpretation here specifically, since revocation correlates with higher-risk situations, even though S0.4's routine revisions can be more relaxed)
- **Given/When/Then:** Given an underwriter's employment ends or they're suspended, when governance revokes their authority, then `AuthorityLimitRevoked` fires immediately, and the same in-flight question from S0.4 applies with much sharper urgency — an underwriter with zero authority cannot have any submission proceed under their name, referral cascade or not. Also triggers `ReassessInFlightSubmissionsOnRuleChange`.
- **Open question (unresolved):** Does revocation trigger a review flag on the underwriter's recently bound business, similar to a thematic file review?

---

## 1a. Submission Intake (confirmed)

Broker submission received, normalized to ACORD ADEPT.

### S1a.1 Clean broker submission received, normalizes without issue

**Actor:** Broker (via Howden's system) or Broker Connect's own ingestion service acting on the broker's behalf.

- **Command:** `ReceiveBrokerSubmission` (brokerFirmId, submittingContact, cellIdHint?, classOfBusinessHint?, rawPayload, sourceChannel)
- **Events:** `BrokerSubmissionReceived` (submissionId, brokerFirmId, submittingContact, rawPayloadRef, sourceChannel, receivedAt — the raw receipt always succeeds if the payload arrives at all) then `SubmissionNormalized` (classOfBusiness, territory, lineSizeSought, namedInsured, effectiveDateRequested, normalizationStatus)
- **Read model:** `SubmissionQueue` — underwriter's main worklist, status="Ready for review"
- **Open question (unresolved):** Is `SubmissionNormalized` genuinely a separate async event, or should it collapse into `BrokerSubmissionReceived` if normalization is synchronous/fast? Depends on actual ADEPT integration latency.

### S1a.2 Submission received but fails/partially fails ADEPT normalization

- **Event:** `SubmissionNormalizationFailed` (submissionId, failureReason, rawPayloadRef — always preserved, never discarded, attemptedAt)
- **Follow-on:** `SubmissionManuallyCorrected` — operations or the broker re-sending fixes the data, re-triggering normalization
- **Read model:** `SubmissionExceptionQueue` — separate from the underwriter's queue, for ops to chase
- **Open questions (unresolved):** Automatic broker notification vs. manual chasing — silent failure risks brokers bypassing the platform. Retry limit/timeout before considered abandoned?

### S1a.3 Submission received for a cell/class the broker isn't authorized to place into

- **Event:** `SubmissionRoutingRejected` (submissionId, brokerFirmId, requestedCellId, rejectionReason="broker not authorized for this cell") — attempt still recorded, never silently dropped
- **Read model:** `BrokerAuthorizationExceptionLog` — never appears in any underwriter's queue; visible to whoever manages broker/cell panel relationships (may represent a legitimate request to extend the broker's panel, not just an error)
- **Open questions (unresolved):** Hard rejection or soft "held for authorization review"? Does the broker get any feedback, or does it silently disappear from their perspective (a worse broker-experience problem than S1a.2's failure)?

### S1a.4 Duplicate submission received

- **Event:** `PotentialDuplicateSubmissionDetected` (submissionId, suspectedOriginalSubmissionId, matchBasis, confidenceLevel?) — new submission always recorded regardless, never silently dropped
- **Follow-on:** `SubmissionSuperseded` (originalSubmissionId → supersedingSubmissionId, a linking event, not silent discard) or `SubmissionConfirmedDistinct`
- **Read model:** flagged item in `SubmissionQueue` with a "possible duplicate" badge — kept in the underwriter's normal flow since resolving it needs their domain judgment, not just ops triage
- **Open questions (unresolved):** Exact matching heuristic (exact-field vs. fuzzy/probabilistic)? Does confirmation supersede the original submission ID or spawn a version chain?

### S1a.5 AI generates a baseline premium after successful normalization

**Actor:** external rating engine (EBM-based, cross-referencing weather/satellite/loss-history data) — not a TFP user. **Resolves a scope revision**: pricing isn't fully deferred to "a subsequent phase" as originally assumed — an AI-generated baseline premium arrives before the underwriter opens the file, per TFP's own published pipeline.

- **Trigger:** `SubmissionNormalized`, success only
- **Automation:** `GenerateBaselinePremiumOnNormalization`
- **Event:** `BaselinePremiumGenerated` (submissionId, baselinePremium, riskFactorSummary, modelVersion)
- **Read model:** `PricedSubmissionView` — broker's ask alongside the AI baseline, side by side, before assessment
- **Design stance (a fourth ingest/display-not-compute boundary, matching S1c.5/S1d.2):** the rating computation (EBM segmentation, satellite/weather cross-referencing) is a specialist capability this system requests and displays, never computes.
- **Resolved change to S2.1:** `AssessSubmission.proposedTerms` is now the underwriter's *adjustment* of the baseline (`baselinePremiumReference`), not an independently originated figure — fires `PricingBaselineAccepted` or `PricingBaselineOverridden` (with variance) alongside, since actuarial needs to measure model-deviation rates and that's invisible otherwise.
- **Follow-on:** `PricingModelVersionDeployed` — rapid model deployment ("quarters to hours" per TFP's materials) means `modelVersion` needs a real referent; reference data only, TFP doesn't govern the model's content.

---

## 1b. Search & Retrieval (confirmed)

Query-side capability over the event stream everything else produces — command/event pairs capture search *activity* (audit/usage analytics); the read model is the actual point of the context. Needs at least two permission tiers from day one: cell-scoped (underwriters) and cross-cell (ops/governance), not one generic search with permission filtering bolted on.

### S1b.1 Underwriter searches historic submissions by risk attribute

- **Command:** `SearchSubmissionHistory` (searcherId, searcherRole, searcherCellContext?, searchCriteria: {classOfBusiness, territory, broker, dateRange, freeText})
- **Event:** `SubmissionHistorySearchPerformed` (searchId, criteriaUsed, resultCount — count only, the result set itself is transient, not audit-worthy)
- **Read model:** `SubmissionSearchResults`
- **Open questions (unresolved):** Is logging every search actually valuable, or over-engineering an audit trail for a read-only convenience feature (leaning toward query-level logging, not per-keystroke)? Does free-text hit normalized ADEPT fields only, or raw broker documents too (a much bigger document-search problem)?

### S1b.2 Claims handler retrieves submission lineage for a bound policy

**Deliberately modeled as its own capability, not folded into generic search** — a claims handler verifying a claim against original bind terms is a direct traversal by policy/bind reference, not a search over criteria. Likely the highest-value piece of the whole "AI-assisted search" claim in the press coverage.

- **Trigger:** automatic on `ClaimNotified` (Context 5), not a manual search action — matches how claims handlers actually work
- **Automation:** `RetrieveSubmissionLineageOnClaimNotified`
- **Event:** `SubmissionLineageRetrieved` (policyId, submissionId, lineageChain: {originalSubmissionId, decisioningPath, referredTo?, quoteId?, boundAt?})
- **Read model:** `PolicyOriginationView` — the full chain: original submission → decisioning path → quote → bind terms, without constructing a search query. **Pays off directly in S5.1's claim payment validation.**

### S1b.3 Operations searches a broker's submission history for reconciliation

- **Command:** `SearchSubmissionHistory` (same command, scoped by broker firm ID, cross-cell)
- **Read model:** `BrokerActivityView` — status breakdown (bound/declined/expired/in-flight) across all cells the broker is authorized for
- **Given/When/Then:** Given operations is reconciling Howden's placement volume for a period, when they search by broker across all cells, then they get a consolidated cross-cell activity view an underwriter-level search wouldn't be permitted to see.

### S1b.4 Cross-cell search permission boundary

Less a scenario than the permission-boundary decision underlying S1b.1–S1b.3.

- **Event:** `CrossCellSearchAttempted` (searcherId, searcherRole, requestedScope, permitted, deniedReason?) — kept visible for whoever manages access policy, not silently enforced (same audit-value reasoning as `SubmissionRoutingRejected`)
- **Design stance:** default to cell-scoped visibility for underwriters; cross-cell visibility is an explicit, logged, elevated permission for ops/governance (S1b.3). A narrower "same named insured, cross-cell" flag — enough to prompt "loop in the other cell's underwriter" without leaking pricing — is a possible middle ground, not yet modeled.

---

## 1c. Exposure Intelligence (exploratory)

Largely event-driven off Context 3 (Binding) rather than user commands — the "actor" for most of this context is the system reacting to a bind/endorsement/cancellation elsewhere. **This context establishes the "ingest/display, not compute" integration-boundary pattern that recurs through 1d and 1e** — see Integration Boundaries below.

### S1c.1 New bind triggers exposure projection update at a geocode

- **Trigger:** `PolicyBound` (Context 3) — a Policy/rule ("whenever X happens, automatically do Y"), not a user command
- **Automation:** `UpdateExposureProjectionOnBind`
- **Event:** `ExposureProjectionUpdated` (geocode, cellId, classOfBusiness, policyReference, priorExposure, newExposure, **deltaExposure** — signed, explicitly modeled rather than just a restated total so the projection's history is itself auditable, providerShares, changeReason="bind")
- **Read model:** `GeographicExposureMap` — running aggregate exposure by geocode/region, queryable by peril/class/provider
- **Open questions (unresolved):** Geocode precision — grid/hex-bucket aggregation (standard cat-modeling practice), not exact lat/long, to avoid false precision. Runs async, not synchronous with bind — the underwriter doesn't need to wait for it.

### S1c.2 Two cells bind risk at the same/nearby geocode — stacking detected

- **Trigger:** `ExposureProjectionUpdated`, evaluated against a configured threshold
- **Automation:** `DetectExposureStacking`
- **Event:** `ExposureConcentrationWarningRaised` (**warningId** — generated, required so `ExposureConcentrationWarningAcknowledged` has something to reference; found missing by the Step 8 completeness check — geocode, perilCategory, contributingCells, combinedExposureValue, thresholdBreached, severityLevel) — **natural hand-off point into future Context 1e (Portfolio Governance)**
- **Follow-on:** `ExposureConcentrationWarningAcknowledged` — without an explicit acknowledgment/escalation event, "warned" and "ignored" are indistinguishable in the audit trail
- **Read model:** `ExposureConcentrationDashboard` — visible to underwriting executives/portfolio managers, not individual cell underwriters
- **Open question (unresolved):** Who owns the threshold — static config, or varies by peril/season/reinsurance program? Domain-expert-owned, same pattern as authority rules — the system enforces it, doesn't define it.

### S1c.3 Endorsement changes exposure at an existing location

- **Trigger:** `PolicyEndorsed` (Context 3)
- **Automation:** `UpdateExposureProjectionOnEndorsement`
- **Event:** `ExposureProjectionUpdated` (delta, not a re-stated total — a $10m→$15m increase yields a +$5m delta) — can re-trigger S1c.2's stacking detection
- **Resolved via Context 3's rebuild:** an endorsement materially increasing exposure now re-triggers the same authority/referral check as an original bind (`EndorsementReferred`), rather than silently bypassing governance.

### S1c.4 Cancellation reduces exposure at a location

- **Trigger:** `PolicyCancelled` (Context 3)
- **Automation:** `UpdateExposureProjectionOnCancellation`
- **Event:** `ExposureProjectionUpdated` (negative delta — full removal of the policy's contribution)
- **Open question (unresolved):** Is exposure removed effective the cancellation date, or immediately regardless of effective date (future-dated cancellation)? Mirrors the bind-time-vs-settlement-time gap already modeled in Bordereaux Settlement — "as-of-today" and "at-any-future-date" exposure are genuinely different questions; the projection may need to be date-aware, not a single running total.

### S1c.5 Exposure projection queried/exported as a batch extract (feeds cat modeling)

- **Command:** `ExportExposureExtract` (requestingTeam, scope: {peril, region, cell, provider}, asOfDate, formatRequired — vendor location/policy file schema)
- **Event:** `ExposureExtractGenerated` (extractId, requestedScope, recordCount, destination)
- **Read model:** `ExposureExtractHistory` — audit trail of what was sent to cat modeling, for reconciling "what did the cat model actually see" if a PML figure is later questioned
- **The cleanest integration boundary in this context**: Broker Connect's job stops at "produce a correct, complete extract" — the cat model run, PML calculation, and hurricane-track overlay genuinely live outside this system's build scope, even though 1d consumes the output. Matches the original out-of-scope note below.

---

## 1d. External Threat (exploratory)

The first context where the trigger isn't internal — not a broker, not an underwriter, not a downstream system reacting to a bind. An external feed (e.g. National Hurricane Center) pushes data on its own schedule. Unlike a broker submission (which someone will chase if it goes missing), **a missed storm update has no one on the sending side accountable for TFP receiving it** — the ingestion boundary needs its own reliability/staleness handling.

### S1d.1 Storm track update received from an external feed

**Actor:** External system (NHC or a commercial weather/cat data feed), not a TFP user.

- **Command:** `IngestExternalThreatUpdate` (technically closer to a poll/webhook receipt than a user command, modeled as a command to keep the triple consistent)
- **Event:** `StormTrackUpdateReceived` (threatId — persistent across the storm's lifecycle, sourceFeed, trackCoordinates, category, forecastConfidenceCone?) — trusts the source feed's own ID scheme for continuity rather than inventing custom matching logic
- **Follow-on:** `ThreatFeedStale` (via `MonitorThreatFeedHealth`) — arguably more important than the happy path, given what's riding on this data; prevents leadership silently looking at an outdated map
- **Read model:** `ActiveThreatRegister`

### S1d.2 Storm track overlaid against the exposure map, triggers PML recalculation

- **Trigger:** `StormTrackUpdateReceived` **or** `ExposureProjectionUpdated` while a threat is active — two independent triggers converging on one event, not one policy with two names
- **Automation:** `RecalculatePMLOnThreatUpdate`
- **Event:** `PMLRecalculated` (threatId, affectedGeocodes, modeledPMLByScenario, affectedPolicies, priorPMLFigure?, `hasImpact` — fires with hasImpact=false and zero/null loss when a tracked storm has no intersecting bound policies rather than doing nothing, distinguishing "checked, no exposure" from "never checked" in the audit trail; `sourcedFromCatModel`=true always)
- **Read model:** `ThreatExposureView` — the live "storm overlaid on policy map" view
- **The biggest open design question in the whole exploratory scope, resolved as a stance:** the actual PML calculation (storm physics against policy terms) is a specialist actuarial/cat-modeling capability. **`RecalculatePMLOnThreatUpdate` requests/ingests from an external cat model — it does not compute the physics itself.** Same integration-boundary shape as S1c.5 (extract out, result in), just in the reactive direction.

### S1d.3 Threat resolved/downgraded

- **Event:** `ThreatResolved` (threatId, resolutionReason: dissipated|downgraded|made landfall, finalTrackData) — any active `ExposureConcentrationWarningRaised` or `TerritoryUnderwritingFrozen` tied to this threat should be explicitly reassessed, not left dangling
- **Follow-on:** `ExposureAtRiskCleared` — explicit stand-down rather than the warning quietly stopping updates

---

## 1e. Portfolio Governance (exploratory)

Where the two independent gates into Context 2 become concrete. Consumes a computed PML/threshold signal from an external cat-modeling capability; this context's job is to react to that signal operationally (freeze, block, unfreeze, escalate), not to compute it.

### S1e.1 PML or Combined Ratio threshold breach detected, triggers a territory/cell underwriting freeze

- **Automation:** `EvaluatePortfolioThreshold` — reacts to `PMLRecalculated` and, separately, periodic Combined Ratio assessment
- **Event:** `TerritoryUnderwritingFrozen` (territory, perilCategory?, triggeringThreatId?/triggeringMetric?, thresholdBreached, breachMagnitude, affectedCells, freezeScope, **activeTriggerReasons** — a freeze may have multiple concurrent causes, only lifts when this set is empty)
- **Read model:** `ActiveFreezeRegister` — surfaced to underwriters in affected cells at the point of decisioning
- **Open questions (unresolved):** Threshold ownership — domain-expert-owned, same pattern as S1c.2, may vary by season/reinsurance program. Automatic freeze on breach, or human sign-off required? Leaning toward auto-alert + recommended freeze + human confirmation until there's a track record — a risk-appetite decision for TFP, not a silent default.

### S1e.2 Underwriter attempts to progress a submission against a frozen territory, blocked

- **Event:** `SubmissionBlockedByPortfolioFreeze` (submissionId, underwriterId, freezeReference, attemptedAction) — intercepts whatever Context 2 command is in flight (e.g. `IssueQuote`)
- **Design stance:** the block reason must read differently from a personal-authority referral to the underwriter — "you're not authorized" vs. "this territory is frozen regardless of who you are."
- **Open question (unresolved):** Auto-resume with re-validation once the freeze lifts, or does the submission need active resubmission (terms/pricing may have moved on during the freeze)? Leaning toward auto-resume with re-validation, flagged as a product decision.

### S1e.3 Threat subsides, threshold no longer breached, freeze lifted

- **Event:** `TerritoryUnderwritingFrozenLifted` (freezeReference, liftReason, finalMetricValue?, **remainingActiveTriggerReasons** — must be empty for the freeze to actually lift; resolving one of several concurrent causes doesn't clear it)

### S1e.4 Manual override: an executive unfreezes despite an ongoing breach

**Actor:** A senior executive with override authority — a distinct, short, tightly-controlled authority concept, structurally similar to Context 0's pattern but *not* an extension of the per-underwriter cascade (a circuit-breaker override, not a delegation of underwriting judgment).

- **Command:** `OverridePortfolioFreeze` (freezeReference, overridingExecutiveId, justification — mandatory, not optional, given what this is bypassing; overrideScope: single-submission|full-lift; `coSignedBy`? — field included from the start even though whether four-eyes is mandatory is a policy decision, not an architecture default)
- **Event:** `PortfolioFreezeOverridden` — the underlying freeze state is explicitly marked as overridden, **permanently distinguishable** in the audit trail from `TerritoryUnderwritingFrozenLifted` (genuine threshold-clear)
- **Open questions (unresolved):** Who exactly holds override authority — configurable/versioned like Context 0, or a small fixed list (e.g. CUO only)? Should it require dual sign-off (four-eyes) given it's overriding a risk control?

### S1e.5 Freeze triggers a hedging/reinsurance-layer request as a downstream instruction

- **Command:** `RequestHedgingAction` (freezeOrThreatReference, requestedActionType, requestingParty) — issued by a portfolio manager, not automatically fired
- **Event:** `HedgingActionRequested`
- **Read model:** `PortfolioActionLog` — visible to the reinsurance/outwards placement team
- **The clearest scope boundary in the whole exploratory set:** recorded as an intent, handed off to whatever system/team actually executes reinsurance placement — a distinct, already-existing TFP function with its own systems and market relationships. Modeling the placement itself would be significant, unjustified scope creep into a different domain.

---

## 1f. Capital & Reinsurance Instruments (exploratory)

Cat bond reference data plus a second external live-tracking feed — industry-wide aggregated catastrophe losses, structurally parallel to 1d's storm tracking but a distinct feed. Complements Portfolio Governance (1e) rather than replacing its hedging/freeze mechanisms.

### S1f.1 Cat bond registered as capacity reference

**Actor:** Capital markets / outwards reinsurance team — the same TFP function `HedgingActionRequested` (S1e.5) already hands off to.

- **Command:** `RegisterCatBond` (bondName e.g. "Woody Re", coveredSyndicate e.g. Lloyd's Syndicate 3123, capacity, **triggerType constrained to `indemnity` | `index`**, attachmentPoint, coveredPerils, interestRate, termStart/End)
- **Event:** `CatBondRegistered` — reference/capital data, same discipline as Context 0's authority grants: this system tracks the bond's terms, doesn't issue or legally manage it. `triggerType` determines which downstream trigger-confirmation path applies.

### S1f.2 Industry loss index updates, distance-to-attachment recalculated

- **Trigger:** `IndustryLossIndexUpdated` — a second external feed (e.g. a PCS-style index provider), structurally parallel to `StormTrackUpdateReceived` (S1d.1); same staleness-handling concern applies, not yet modeled as its own event (flagged, matching `ThreatFeedStale`'s pattern)
- **Automation:** `TrackCatBondAttachmentDistance` → **Event:** `CatBondAttachmentDistanceUpdated` (bondId, currentIndexValue, attachmentPoint, distanceToAttachment, percentToAttachment)
- **Read model:** `CatBondCoverageView` — "executives can see exactly where the bond's coverage ends," cross-referenced against `GeographicExposureMap` (1c.1) and `ExposureConcentrationDashboard` (1e.1)
- **Given/When/Then:** Given Woody Re attaches at $78B industry-wide losses with $75M capacity protecting Syndicate 3123, when the index updates to $70B following a wildfire season, then distance-to-attachment reflects $8B, visible on `CatBondCoverageView`.

### S1f.3 Narrowing distance-to-attachment feeds Portfolio Governance

`CatBondAttachmentDistanceUpdated` is a **third trigger type** for `EvaluatePortfolioThreshold` (S1e.1), alongside PML and Combined Ratio — additive, since `TerritoryUnderwritingFrozen.activeTriggerReasons` already supports multiple concurrent causes. Can also directly produce `HedgingActionRequested` (S1e.5) — "purchase immediate micro-targeted traditional reinsurance layers to complement the bond" is exactly what that event already models.

### S1f.4 Cat bond actually triggered — investor capital forfeited, claims paid

- **Event:** `CatBondTriggered` (bondId, qualifyingEvent, payoutAmount) — the moment losses cross the attachment/trigger point and capital legally moves.
- **Resolved (two legitimate upstream paths, by `triggerType`):** `CatBondAttachmentDistanceUpdated` (S1f.2) crossing zero for **index** bonds — third-party data, enables rapid liquidity, closer to self-executing; or `IndemnityLossesAudited` (S1f.5) crossing the threshold for **indemnity** bonds — TFP's own losses, always audit-gated, never self-executing. Same event, two routes in, matching how `TerritoryUnderwritingFrozen` already accepts multiple concurrent trigger types.

### S1f.5 Indemnity-triggered bond: TFP's own losses audited against the trigger threshold

**Actor:** Actuarial/claims audit function — distinct from the external index feed, this draws on TFP's own data.

- **Trigger:** periodic or loss-event-driven, scoped to policies/claims within the bond's covered perils/syndicate
- **Automation:** `AuditIndemnityLossesAgainstTrigger` — pulls actual incurred losses from `ClaimPaid`/`ClaimReserveSet` (Context 5), **not** `IndustryLossIndexUpdated` — a structurally different data source than the index-trigger path
- **Event:** `IndemnityLossesAudited` (bondId, auditedLossTotal, triggerThreshold, distanceToTrigger, auditedBy) — requires auditing means this is never self-executing the way index-crossing can be, same discipline as `ClaimPeriodValidated` (S5.5)
- **Given/When/Then:** Given Woody Re were instead indemnity-triggered at $50M of TFP's own incurred losses on covered perils, when quarterly audit totals actual paid + reserved claims at $42M, then `IndemnityLossesAudited` records $8M distance-to-trigger, auditable and separate from any index-based figure.

---

## 2. Underwriting Decisioning (confirmed)

The branchiest context — and the payoff for having fully specified both gates (Context 0 authority, Context 1e freeze) first. Every entry point (S2.1 happy path, S2.2–S2.5 referral cascade, S2.10 amendment loop) passes through the same two-gate check.

### S2.1 Submission within authority, straight to quote

**Actor:** Underwriter.

- **Command:** `AssessSubmission` (submissionId, underwriterId, proposedTerms: {lineSize, pricing, conditions}) — checked against both gates before the event fires: Context 0 (`UnderwriterAuthorityRegister` covers the terms) and Context 1e (no active freeze on this territory/class)
- **Event:** `SubmissionWithinAuthority` (proposedTerms, **authorityVersionChecked** — references the specific register version applied, not just "checked", so a later audit can see exactly what rule was in force; **freezeCheckPassed** — explicitly records the freeze check ran and passed, same completeness-of-audit-trail reasoning as S1d.4's zero-impact case)

### S2.2 Referred once, approved at next tier

- **Event:** `SubmissionReferred` (referralId, breachedDimension: line size|class|territory, referredTo — determined by the authority cascade)
- **Command:** `DecideReferral` → **Event:** `ReferralApproved` (approvedTerms, termsModified)
- **Resolved:** if approved terms differ from originally proposed, requires the **original underwriter's acknowledgment** (`ReferralTermsAcknowledged`) before proceeding — they hold the broker relationship, so the referee doesn't unilaterally finalize terms the underwriter never actually offered.

### S2.3 Referred once, declined at next tier

- **Event:** `ReferralDeclined` (referralId, declineReason) — distinct from `SubmissionDeclined` (S2.6): declined at these terms by this referee for this authority reason, not off-appetite entirely.
- **Resolved:** loops back to a fresh `AssessSubmission` with revised terms, not a hard stop — **same submissionId persists**, a revised assessment of the same underlying risk (same pattern as `PolicyEndorsed` being a new ledger entry on the same policy, not a new one).

### S2.4 Referred, escalates through multiple tiers before resolution

- Same `SubmissionReferred` event type, chained via `parentReferralId` referencing the prior referral — forms a visible escalation chain.
- **Resolved:** the cascade terminates at the cell's own overall limit (Context 0/S0.1's `CellAuthorityLimitGranted`/`CellAuthorityRegister`), not unlimited escalation.

### S2.5 Referred, but the referee's own authority is revised/revoked mid-referral

- **Trigger:** `AuthorityLimitRevised`/`AuthorityLimitRevoked` (Context 0) while a referral is pending — same `ReassessInFlightSubmissionsOnRuleChange` mechanism as S0.4/S0.5, third trigger.
- **Events:** `PendingReferralReassigned` (rerouted to whoever now holds equivalent authority) or `PendingReferralHeld` (flagged for governance when no automatic reroute can be safely inferred).
- **Treated as required-for-build, not an edge case to defer** — follows directly from org-change realities (people leaving, being suspended) Context 0 already anticipates.

### S2.6 Declined outright (off-appetite/sanctioned), independent dead end

- **Command:** `DeclineSubmission` (declineReasonCode — structured, not free text only) → **Event:** `SubmissionDeclined`
- **Follow-on:** `ComplianceNotifiedOfSanctionsDecline` — mandatory downstream notification specifically for sanctions-related declines, given the regulatory seriousness vs. an ordinary off-appetite decline.

### S2.7 Quote issued, accepted promptly — happy path to bind

- **Command:** `IssueQuote` → **Event:** `QuoteIssued` (quoteId, terms, validityPeriodDays, expiryDate) — **this is also the exact point S1e.2's freeze-block check applies**, since issuing a quote is the "attempted action" intercepted if a freeze is active.
- **Command:** `AcceptQuote` (broker-initiated) → **Event:** `QuoteAccepted` (acceptedTerms — should match issued terms exactly; any variation is really a counter, see S2.10).

### S2.8 Quote issued, expires unaccepted

- **Automation:** `ExpireStaleQuotes` — scheduled/time-triggered, not a human command.
- **Event:** `QuoteExpired` — the submission moves to a closed/lapsed state distinct from decline (no one said no, it just wasn't taken up).

### S2.9 Quote issued, broker explicitly declines

- **Command:** `DeclineQuote` → **Event:** `QuoteDeclined` — kept distinct from `QuoteExpired`; an active decline (especially "placed elsewhere") is genuinely useful competitive-intelligence data that passive expiry isn't.

### S2.10 Quote issued, broker requests amended terms

- **Command:** `RequestQuoteAmendment` → **Event:** `QuoteAmendmentRequested`
- **Resolved:** treated as looping back to a **new `AssessSubmission` pass** against the amended terms, not a lightweight negotiation sub-flow — amended terms could push the submission outside the underwriter's authority, so must go through the same Context 0/1e gate checks as any other assessment. A casual in-context negotiation would silently bypass both gates.

---

## 3. Binding (confirmed)

"The immutable ledger" — once `PolicyBound` fires, it's the anchor everything downstream (Bordereaux in Context 4, Claims in Context 5) keys off. Endorsements/cancellations append to history, never rewrite the record.

### S3.1 / S3.2 Clean bind, single or multiple capacity providers

- **Command:** `BindPolicy` (quoteId, submissionId, finalTerms, **capacityProviderAllocation** — always a list, even at a single provider (100% to one), keeping single- and multi-provider binds the same shape; must sum to exactly 100%, a hard validation rule)
- **Event:** `PolicyBound` (policyId, submission lineage, finalTerms, capacityProviderAllocation, boundBy)
- **Read models:** `PolicyRegister` (canonical current-state view) and `ActiveBookOfBusiness` (per-cell)
- **Resolved:** requires explicit underwriter confirmation, not fired automatically on `QuoteAccepted` — broker acceptance and the underwriter's final bind action are conceptually different moments (documentation checks, subjectivities being cleared).
- **Open question (unresolved):** Does the allocation need to reference which specific Context 0 authority grant justified writing on behalf of each provider? Depends on S0.1's still-open multi-provider-per-cell question.

### S3.3 Mid-term endorsement

- **Command:** `EndorsePolicy` (requestedChange: {description, deltaType: premium|limit|exposure|administrative})
- **Event:** `PolicyEndorsed` (changeDelta — a delta from current terms, not a full restatement, materialityClassification)
- **Resolved (closes a thread from S1c.3):** a material change (premium/limit/exposure) that exceeds the underwriter's authority produces `EndorsementReferred` instead — **reuses Context 2's `DecideReferral`/`ReferralApproved`/`ReferralDeclined` resolution path** rather than a lightweight edit path that quietly bypasses governance. Administrative changes (spelling, contact updates) don't re-check. The material/administrative line is keyed explicitly to `deltaType`, not left to underwriter judgment case by case.

### S3.4 Cancellation

- **Command:** `CancelPolicy` (cancellationReason, initiatedBy: broker-request|underwriter-for-cause)
- **Event:** `PolicyCancelled` (returnPremiumBasis: pro-rata|short-rate) — triggers S1c.4's exposure reduction; return premium calculation feeds Bordereaux/Settlement (Context 4), not computed here.
- **Resolved:** underwriter-for-cause cancellation (e.g. material misrepresentation) gets its own distinct event, `PolicyCancelledForCause`, given the compliance/dispute implications versus a routine broker-requested cancellation.

### S3.5 Renewal

- **Command:** `InitiateRenewal` (expiringPolicyId, proposedRenewalTerms?)
- **Event:** `PolicyRenewalInitiated` (newSubmissionId — a genuinely new submission re-entering Context 1a/2, not a special shortcut path; **unconditionalReassessment**=true — expiring terms don't grandfather anything, since risk appetite, authority, and even the underwriter may have changed since original bind. No renewal fast-track shortcut.)
- **Open questions (unresolved):** Does the new submission carry forward exposure/PML context from the expiring policy automatically (flagged for later given 1c/1d/1e's exploratory status, but the linkage should exist even before those contexts are built)? What happens if renewal is initiated but the broker doesn't respond before original expiry — lapse vs. grace/held-covered period is a contractual question for underwriting stakeholders, not an architecture default.

---

## 4. Bordereaux Settlement (confirmed)

Meaningfully different in shape from everything before it — Contexts 0–3 are triggered by discrete business events; Context 4 is triggered by the calendar, sweeping up a batch of prior transactions.

### S4.1 Clean period close, bordereau drafted, agreed, settled without dispute

- **Automation:** `DraftBordereauOnPeriodClose` (period-end scheduled, per the provider agreement) → **Event:** `BordereauDrafted` (bordereauId, cell, provider, period, **lines** — each swept transaction with its own generated `lineId`, plus a derived `lineCount` for quick display)
- **Read model:** `UnbilledTransactionsForPeriod` — explicit link from each transaction to at most one bordereau (or none) — answers "how does the system know a transaction hasn't already been included" as a queryable concern, not an assumed fact.
- **Resolved (two-party agreement):** `BordereauSubmittedForAgreement` (TFP's internal readiness) → **Command:** `AgreeBordereau` → **Event:** `BordereauAgreed` (agreedByTFP, **agreedByProvider** — genuine two-party sign-off, not just TFP's internal view, since bordereaux are literally how money moves) → **Command:** `SettleBordereau` → **Event:** `BordereauSettled`.
- **Resolved:** `BordereauAgreed` only fires once zero queries remain open — by construction, since any query still open at period-close is resolved (S4.2) or deferred (S4.3) before agreement.

### S4.2 Line queried, resolved, then agreement proceeds

- **Command:** `RaiseBordereauQuery` → **Event:** `BordereauLineQueried` → **Command:** `ResolveBordereauQuery` → **Event:** `BordereauLineResolved` (confirmed as-is, or corrected).
- **Read model:** `BordereauDetail`.

### S4.3 Line queried but never resolved within the period

- **Recommended default (not a toss-up):** the line is **deferred**, not blocking. **Automation:** `DeferQueriedLineAtPeriodClose` → **Event:** `BordereauLineDeferred` — excludes the specific line, letting the rest settle on schedule, carried into next period's draft. A single disputed line shouldn't hold up cash movement on everything else. Mirrors the `AdjustmentLineRaised` pattern (S4.4) already established.

### S4.4 Post-agreement correction via AdjustmentLineRaised

- **Command:** `RaiseAdjustmentLine` (original bordereau/line reference for traceability, correction amount and reason, **target period — always future, never retroactive**) → **Event:** `AdjustmentLineRaised`
- The correction flows into the next period's `BordereauDrafted` sweep as a new line — the original historical bordereau remains untouched, consistent with the immutable-ledger principle running through the whole model.
- **Open question (unresolved):** Does an adjustment need its own approval/agreement cycle, or ride along within the next period's normal cycle as a line item? Leaning toward the latter for simplicity, unless large/contentious enough to warrant a materiality threshold for separate sign-off.

### S4.5 Bordereau spanning multiple capacity providers on one cell

**Resolved (not a toss-up):** bordereaux stay strictly single cell+provider+period. A `PolicyBound` event with a 60/40 split across two providers contributes its 60% portion to Provider A's bordereau and 40% to Provider B's bordereau independently — matches how capacity providers actually expect to be settled with (their own statement, not a shared one). No new event type needed; `BordereauDrafted`'s sweep pulls the correct provider-share portion using the allocation data already captured in `PolicyBound`/`PolicyEndorsed`. **The allocation model from S3.2 threads directly through here.**

---

## 5. Claims (confirmed)

The last context, closing the loop back to nearly everything before it — a claim references the original bind (and transitively the whole submission/decisioning lineage), and its provider split must mirror the quota share established at bind time.

### S5.1 Clean claim: notified, reserved, paid, closed

**Actor:** Claims handler (notification may originate from broker/insured; processing is the claims team's).

- **Command:** `NotifyClaim` → **Event:** `ClaimNotified` (claimId, policyReference, dateOfLoss, dateNotified, notifyingParty) — references the original bind lineage via `policyReference`; a claims handler pulling up a claim gets the full `PolicyOriginationView` from S1b.2.
- **Command:** `SetClaimReserve` → **Event:** `ClaimReserveSet`
- **Command:** `PayClaim` (providerAllocation mirroring the original bind's quota share) → **Event:** `ClaimPaid` (**validatedAgainstBindTerms**=true — explicit, visible validation against the policy's limits/sub-limits/deductibles from the original bind, not just trusted to the claims handler's manual check; this is precisely S1b.2's use case)
- **Command:** `CloseClaim` → **Event:** `ClaimClosed`
- **Read model:** `ClaimRegister`

### S5.2 Reserve revised multiple times before payment

- `ClaimReserveSet` fires again — same event type, new instance (`sequenceNumber`); the reserve history is a full sequence, not a mutable field.
- **Resolved (a genuine parallel to S3.3, not assumed out of scope):** a material reserve increase crossing the claims handler's own authority threshold produces `ReserveRevisionReferred`, reusing the same `DecideReferral` resolution path as Context 2/3's referral mechanisms. **The authority/governance pattern from Context 0 is a cross-cutting capability** — see Cross-Cutting Principles below.

### S5.3 Claim paid, split across provider quota shares matching the original bind split

- `ClaimPaid`'s `providerAllocation` is auto-derived from the split recorded in the original `PolicyBound` event, not re-entered manually.
- **Open question (unresolved):** Could quota share change post-bind (novation, reinsurance restructure)? If so, this needs point-in-time versioning like Context 0's authority model, not a simple lookup — parallel to S0.1's per-provider question, not resolved independently here.

### S5.4 Claim closed, then reopened

- **Command:** `ReopenClaim` → **Event:** `ClaimReopened` — appended after `ClaimClosed`, doesn't erase it; the same append-only "corrections are new entries, not edits" principle used for Bordereaux and Binding applies here too. Typically followed by a fresh `ClaimReserveSet` cycle.
- **Open question (unresolved, not essential day one):** Limit on reopen count, or escalation if it reopens repeatedly (a pattern-detection signal, similar to thematic file reviews)?

### S5.5 Claim notified against a policy that's since been cancelled or non-renewed

- **Event:** `ClaimNotifiedAgainstInactivePolicy` (policyStatusAtNotification, dateOfLoss) — a flag, **not a rejection**; the loss may have occurred while cover was active, regardless of the policy's current status.
- **Resolved:** `ClaimPeriodValidated` is an **explicit human confirmation** step, not an automated pass/fail — date-of-loss-vs-active-period disputes (was the policy actually in force at the exact moment of loss, retroactive date issues) are exactly the kind of determination that ends up in coverage disputes and shouldn't be silently auto-decided.

---

## Integration Boundaries

Every exploratory context (1c/1d/1e) has at least one explicit "this is someone else's specialist system" boundary — arguably the single most important section if this scope is ever presented as a real proposal rather than a design exercise:

| Seam | This system owns | The specialist system owns |
|---|---|---|
| `ExportExposureExtract` → cat model (S1c.5) | Producing a correct, complete extract, audited via `ExposureExtractHistory` | The cat model run, PML calculation, hurricane-track overlay |
| `PMLRecalculated` ← cat model (S1d.2) | The trigger (storm update / exposure change) and the display (`ThreatExposureView`) | Storm physics against policy terms — deductibles, limits, sub-limits |
| `HedgingActionRequested` → reinsurance placement (S1e.5) | Recording the intent, tied to a specific freeze/breach, handed off | Actual placement of a reinsurance layer — TFP's existing outwards reinsurance function |
| `BaselinePremiumGenerated` ← rating engine (S1a.5) | The trigger (successful normalization) and the display (`PricedSubmissionView`), plus measuring underwriter deviation from the baseline | EBM risk segmentation, satellite/weather/loss-history cross-referencing — the actual pricing algorithm |

Broker Connect is the system of record for *why* something happened; it is never the system that performs the specialist calculation or execution on the other side of the seam.

---

## Cross-Cutting Architectural Principles

Three patterns emerged independently across contexts and are worth naming explicitly rather than treating as separate decisions:

1. **Quota-share allocation, established once, reused three times.** `capacityProviderAllocation`, captured at `PolicyBound` (S3.2), drives settlement scoping at the bordereau level (S4.5) and claim payment splits (S5.3) without re-entry anywhere downstream.
2. **The authority/governance engine is cross-cutting, not scoped to underwriting decisioning alone.** The same referral/escalation mechanism (`DecideReferral` → `ReferralApproved`/`ReferralDeclined`) now serves Context 2 (`SubmissionReferred`), Context 3 (`EndorsementReferred`), and Context 5 (`ReserveRevisionReferred`) — three contexts, one governance mechanism. Context 0 is better understood as a general authority/governance engine than as "underwriting authority" specifically.
3. **Append-only ledger: corrections are new entries, never edits.** First stated for Bordereaux (`AdjustmentLineRaised`), the same principle governs Binding (`PolicyEndorsed`) and Claims (`ClaimReopened`) — one architectural principle, three applications, not three separate decisions.

A fourth, structural thread: the confirmed/exploratory line runs straight through a coherent story (ingestion → exposure → threat → governance) — even the speculative extension follows logically from the confirmed foundation. It also tracks the article almost exactly: TFP's own "subsequent phase" language for pricing/actuarial is precisely where 1c–1e pick up.

---

## Explicitly out of scope

- **Pricing & Actuarial computation** (the EBM rating model itself, satellite/weather/loss-history cross-referencing) — **revised**: pricing is not deferred wholesale as originally assumed from the article's "subsequent phase" language. TFP's own materials describe AI generating a baseline premium before human review (S1a.5). What stays out of scope is the computation; the integration boundary (`BaselinePremiumGenerated`) is in scope and modeled.
- **Cat/exposure aggregation compute** (RMS/AIR-style PML calculation itself) — see Integration Boundaries; this system requests/displays, never computes.
- **Reinsurance placement execution** — see Integration Boundaries; this system records intent and hands off.
