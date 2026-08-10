// Generates slice JSON matching the confirmed import-config shape documented in
// agentic-modeling/eventmodelers-import-config-guide.md, from the compact domain
// descriptors below (sourced from ../scenarios.md and ../bordereaux.md).
//
// Usage: node generate-slices.mjs
// Writes one file per slice into ./slices/, plus a combined ./import-config.json
//
// COVERAGE NOTE: this file covers the board's original layer only - the initial
// 21-slice bulk import plus Authority Administration's first pass and Submission
// Intake/Search & Retrieval/Exposure Intelligence's first pass (1a-1c). It does
// NOT reflect the much richer rebuilds of Context 0, 2, 3, 4, 5, or the addition
// of External Threat (1d) and Portfolio Governance (1e) done later via direct
// API calls, because import-config replaces the entire board on every call and
// so can't incrementally patch an already-populated one. The actual regenerable
// source for everything past this point is ./build-scripts/ (run in the order
// documented in its README) - trust that directory over this file for the
// board's current full state.

import { writeFileSync, mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const CHAPTER = "Broker Connect";

function field(name, type, opts = {}) {
  return {
    name,
    type,
    cardinality: opts.cardinality || "Single",
    optional: opts.optional || false,
    idAttribute: opts.idAttribute || false,
    generated: opts.generated || false,
    edited: opts.edited !== undefined ? opts.edited : true,
    query: opts.query || false,
    showAttributes: opts.showAttributes || false,
    technicalAttribute: opts.technicalAttribute || false,
    subfields: opts.subfields || [],
  };
}

const LANE = {
  COMMAND: "Interaction",
  EVENT: "Swimlane",
  READMODEL: "Interaction",
  SCREEN: "Actor",
  AUTOMATION: "Actor",
};

function el(id, title, type, sliceTitle, context, opts = {}) {
  return {
    id,
    title,
    type,
    fields: opts.fields || [],
    dependencies: [],
    modelContext: context,
    lane: LANE[type],
    aggregate: opts.aggregate || "default",
    aggregateDependencies: [],
    triggers: [],
    tags: [],
    slice: sliceTitle,
    description: opts.description || "",
    context: "INTERNAL",
    elementContext: "INTERNAL",
    listElement: false,
    todoList: false,
    createsAggregate: opts.createsAggregate || false,
    elementCopy: false,
    sketched: false,
    comments: [],
  };
}

// Shared field set for the "authority-limit-view" read model, which is populated by
// the Authority Administration slices (Grant/Revise/Revoke) and consumed by
// Underwriting Decisioning's AssessSubmission. Kept as one function so every slice
// that references it stays in sync.
function authorityLimitViewFields() {
  return [
    field("authorityLimitId", "UUID", { idAttribute: true }),
    field("tier", "String", { example: "Underwriter" }),
    field("providerId", "String", { optional: true }),
    field("cellId", "String"),
    field("underwriterId", "String", { optional: true }),
    field("classOfBusiness", "String"),
    field("maxGrossPremium", "Decimal"),
    field("maxLimit", "Decimal"),
    field("currency", "String"),
    field("status", "String", { example: "Active" }),
    field("grantedBy", "String", { optional: true }),
    field("grantedAt", "DateTime"),
  ];
}

function slice(id, title, sliceType, context, parts = {}) {
  return {
    id,
    title,
    status: "Created",
    sliceType,
    chapter: CHAPTER,
    context,
    commands: parts.commands || [],
    events: parts.events || [],
    readmodels: parts.readmodels || [],
    screens: parts.screens || [],
    processors: parts.processors || [],
    tables: parts.tables || [],
    specifications: [],
    actors: [],
    aggregates: [],
    screenImages: [],
    comments: [],
  };
}

const slices = [];

// ---------------------------------------------------------------------------
// 0. Authority Administration (governance/reference data underpinning the
//    Provider -> Cell -> Underwriter authority cascade used by Underwriting
//    Decisioning). Previously flagged in scenarios.md as out of scope /
//    "assumed to exist as reference data" - now modeled explicitly.
// ---------------------------------------------------------------------------
{
  const ctx = "Authority Administration";
  const t = "Grant Cell Authority Limit";
  const cmd = el("grant-cell-authority-limit-cmd", "GrantCellAuthorityLimit", "COMMAND", t, ctx, {
    aggregate: "AuthorityLimit",
    createsAggregate: true,
    description: "Capacity Provider delegates an authority limit to a Cell (MGA) for a given class of business - the top tier of the Provider -> Cell -> Underwriter cascade.",
    fields: [
      field("providerId", "String"),
      field("cellId", "String"),
      field("classOfBusiness", "String"),
      field("maxGrossPremium", "Decimal"),
      field("maxLimit", "Decimal"),
      field("currency", "String"),
    ],
  });
  const evt = el("cell-authority-limit-granted", "CellAuthorityLimitGranted", "EVENT", t, ctx, {
    aggregate: "AuthorityLimit",
    description: "New Cell-tier authority record - projects into authority-limit-view with tier=Cell.",
    fields: [
      field("authorityLimitId", "UUID", { idAttribute: true, generated: true }),
      field("providerId", "String"),
      field("cellId", "String"),
      field("classOfBusiness", "String"),
      field("maxGrossPremium", "Decimal"),
      field("maxLimit", "Decimal"),
      field("currency", "String"),
      field("grantedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("grant-cell-authority-limit", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Authority Administration";
  const t = "Grant Underwriter Authority Limit";
  const cmd = el("grant-underwriter-authority-limit-cmd", "GrantUnderwriterAuthorityLimit", "COMMAND", t, ctx, {
    aggregate: "AuthorityLimit",
    createsAggregate: true,
    description: "Cell Head Underwriter delegates part of the Cell's own authority to an individual underwriter. Cannot exceed the Cell's own granted limit for that class of business.",
    fields: [
      field("cellId", "String"),
      field("underwriterId", "String"),
      field("classOfBusiness", "String"),
      field("maxGrossPremium", "Decimal"),
      field("maxLimit", "Decimal"),
      field("currency", "String"),
      field("grantedBy", "String"),
    ],
  });
  const granted = el("underwriter-authority-limit-granted", "UnderwriterAuthorityLimitGranted", "EVENT", t, ctx, {
    aggregate: "AuthorityLimit",
    description: "New Underwriter-tier authority record - projects into authority-limit-view with tier=Underwriter.",
    fields: [
      field("authorityLimitId", "UUID", { idAttribute: true, generated: true }),
      field("cellId", "String"),
      field("underwriterId", "String"),
      field("classOfBusiness", "String"),
      field("maxGrossPremium", "Decimal"),
      field("maxLimit", "Decimal"),
      field("currency", "String"),
      field("grantedBy", "String"),
      field("grantedAt", "DateTime", { generated: true }),
    ],
  });
  const rejected = el("underwriter-authority-limit-rejected", "UnderwriterAuthorityLimitRejected", "EVENT", t, ctx, {
    aggregate: "AuthorityLimit",
    description: "Requested underwriter limit would exceed the Cell's own delegated limit for this class of business - the cascade cannot delegate more authority than it holds.",
    fields: [
      field("cellId", "String"),
      field("underwriterId", "String"),
      field("classOfBusiness", "String"),
      field("requestedMaxGrossPremium", "Decimal"),
      field("requestedMaxLimit", "Decimal"),
      field("cellMaxGrossPremium", "Decimal"),
      field("cellMaxLimit", "Decimal"),
      field("reason", "String"),
      field("rejectedAt", "DateTime", { generated: true }),
    ],
  });
  const readmodel = el("authority-limit-view", "AuthorityLimit", "READMODEL", t, ctx, {
    aggregate: "AuthorityLimit",
    description: "Read here to validate that the requested underwriter limit does not exceed the Cell's own current authority limit for the class of business.",
    fields: authorityLimitViewFields(),
  });
  slices.push(slice("grant-underwriter-authority-limit", t, "STATE_CHANGE", ctx, {
    commands: [cmd], events: [granted, rejected], readmodels: [readmodel],
  }));
}

{
  const ctx = "Authority Administration";
  const t = "Revise Authority Limit";
  const cmd = el("revise-authority-limit-cmd", "ReviseAuthorityLimit", "COMMAND", t, ctx, {
    aggregate: "AuthorityLimit",
    description: "Either tier's authority record is revised - e.g. an annual treaty renewal resets the Cell's limit, or a Cell Head Underwriter adjusts an individual underwriter's delegation.",
    fields: [
      field("authorityLimitId", "UUID", { idAttribute: true }),
      field("newMaxGrossPremium", "Decimal"),
      field("newMaxLimit", "Decimal"),
      field("revisedBy", "String"),
      field("reason", "String"),
    ],
  });
  const evt = el("authority-limit-revised", "AuthorityLimitRevised", "EVENT", t, ctx, {
    aggregate: "AuthorityLimit",
    description: "Appended revision - previous values are kept for audit rather than overwritten silently.",
    fields: [
      field("authorityLimitId", "UUID", { idAttribute: true }),
      field("previousMaxGrossPremium", "Decimal"),
      field("newMaxGrossPremium", "Decimal"),
      field("previousMaxLimit", "Decimal"),
      field("newMaxLimit", "Decimal"),
      field("revisedBy", "String"),
      field("reason", "String"),
      field("revisedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("revise-authority-limit", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Authority Administration";
  const t = "Revoke Authority Limit";
  const cmd = el("revoke-authority-limit-cmd", "RevokeAuthorityLimit", "COMMAND", t, ctx, {
    aggregate: "AuthorityLimit",
    description: "Either tier's authority record is revoked outright - e.g. an underwriter leaves the cell, or a Provider ends a Cell relationship for a class of business.",
    fields: [
      field("authorityLimitId", "UUID", { idAttribute: true }),
      field("revokedBy", "String"),
      field("reason", "String"),
    ],
  });
  const evt = el("authority-limit-revoked", "AuthorityLimitRevoked", "EVENT", t, ctx, {
    aggregate: "AuthorityLimit",
    description: "Authority record's status becomes Revoked in authority-limit-view - kept as history, not deleted.",
    fields: [
      field("authorityLimitId", "UUID", { idAttribute: true }),
      field("revokedBy", "String"),
      field("reason", "String"),
      field("revokedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("revoke-authority-limit", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

// ---------------------------------------------------------------------------
// 1a. Submission Intake
// Rebuilt 2026-08 from BA-dictated scenarios (see scenarios.md 1a.1-1a.4).
// Command renamed Submit->Receive: actor is the broker's system or Broker
// Connect's own ingestion service, not necessarily a direct user action.
// ---------------------------------------------------------------------------
{
  const ctx = "Submission Intake";
  const t = "Receive Broker Submission";
  const cmd = el("receive-broker-submission-cmd", "ReceiveBrokerSubmission", "COMMAND", t, ctx, {
    aggregate: "Submission",
    createsAggregate: true,
    description: "Broker (or Broker Connect's ingestion service acting on the broker's behalf) submits a new risk. Duplication and cell-authorization are detected downstream, not prevented at this command.",
    fields: [
      field("brokerFirmId", "String"),
      field("submittingContact", "String"),
      field("cellIdHint", "String", { optional: true }),
      field("classOfBusinessHint", "String", { optional: true }),
      field("rawPayload", "Custom", { example: "broker system's native format" }),
      field("sourceChannel", "String", { example: "Howden ADEPT integration" }),
    ],
  });
  const received = el("broker-submission-received", "BrokerSubmissionReceived", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "1a.1: the raw receipt always succeeds if the payload arrives at all - always recorded regardless of what normalization/routing/duplicate-checking finds downstream.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true, generated: true }),
      field("brokerFirmId", "String"),
      field("submittingContact", "String"),
      field("rawPayloadRef", "Custom", { example: "stored as-is, immutable" }),
      field("sourceChannel", "String"),
      field("receivedAt", "DateTime", { generated: true }),
    ],
  });
  const routingRejected = el("submission-routing-rejected", "SubmissionRoutingRejected", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "1a.3: broker's panel authorization doesn't cover the requested cell/class. Attempt is still recorded (never silently dropped) - useful for broker relationship management.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("brokerFirmId", "String"),
      field("requestedCellId", "String"),
      field("requestedClassOfBusiness", "String", { optional: true }),
      field("rejectionReason", "String", { example: "broker not authorized for this cell" }),
      field("rejectedAt", "DateTime", { generated: true }),
    ],
  });
  const normalized = el("submission-normalized", "SubmissionNormalized", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "1a.1: ADEPT normalization succeeded - structured fields extracted. OPEN QUESTION: separate async event vs collapsing into BrokerSubmissionReceived if normalization is synchronous/fast - depends on actual ADEPT integration latency.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("classOfBusiness", "String"),
      field("territory", "String"),
      field("lineSizeSought", "Decimal"),
      field("keyTerms", "String", { optional: true }),
      field("namedInsured", "String"),
      field("effectiveDateRequested", "Date"),
      field("normalizationStatus", "String", { example: "success" }),
      field("normalizedAt", "DateTime", { generated: true }),
    ],
  });
  const normalizationFailed = el("submission-normalization-failed", "SubmissionNormalizationFailed", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "1a.2: missing required field, malformed data, unrecognized class code, or schema validation error. Raw payload always preserved. OPEN QUESTIONS: automatic broker notification vs manual chasing; retry limit/timeout before considered abandoned.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("failureReason", "String", { example: "missing required field: TIV" }),
      field("rawPayloadRef", "Custom"),
      field("attemptedAt", "DateTime", { generated: true }),
    ],
  });
  const manuallyCorrected = el("submission-manually-corrected", "SubmissionManuallyCorrected", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "Manual remediation path: operations or the broker (re-sending) fixes the data, re-triggering normalization.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("correctedBy", "String"),
      field("correctionDescription", "String"),
      field("resubmittedForNormalization", "Boolean"),
      field("correctedAt", "DateTime", { generated: true }),
    ],
  });
  const duplicateDetected = el("potential-duplicate-submission-detected", "PotentialDuplicateSubmissionDetected", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "1a.4: new submission always recorded regardless (never silently dropped) - flagged as a possible resubmission alongside an existing open submission. OPEN QUESTION: exact matching heuristic (exact-field vs fuzzy/probabilistic) materially affects false-positive rate.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("suspectedOriginalSubmissionId", "UUID"),
      field("matchBasis", "String", { example: "same broker + same named insured + same class + overlapping effective date" }),
      field("confidenceLevel", "Decimal", { optional: true }),
      field("detectedAt", "DateTime", { generated: true }),
    ],
  });
  const superseded = el("submission-superseded", "SubmissionSuperseded", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "Underwriter/ops confirms the new submission is a genuine resubmission/update of the original - a linking event (old -> new) rather than silently discarding the original, preserving event-stream integrity.",
    fields: [
      field("originalSubmissionId", "UUID", { idAttribute: true }),
      field("supersedingSubmissionId", "UUID"),
      field("linkedBy", "String"),
      field("linkedAt", "DateTime", { generated: true }),
    ],
  });
  const confirmedDistinct = el("submission-confirmed-distinct", "SubmissionConfirmedDistinct", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "Underwriter/ops confirms the flagged submissions are coincidentally similar but genuinely distinct risks - both proceed independently.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("suspectedOriginalSubmissionId", "UUID"),
      field("confirmedBy", "String"),
      field("confirmedAt", "DateTime", { generated: true }),
    ],
  });
  const submissionQueue = el("submission-queue", "SubmissionQueue", "READMODEL", t, ctx, {
    aggregate: "Submission",
    description: "Underwriter's main worklist. Possible-duplicate submissions stay in this normal flow with a badge (rather than a separate queue) since resolving them needs underwriter domain judgment, not just ops triage.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("brokerFirmId", "String"),
      field("classOfBusiness", "String"),
      field("territory", "String"),
      field("lineSizeSought", "Decimal"),
      field("receivedAt", "DateTime"),
      field("status", "String", { example: "Ready for review" }),
      field("isPossibleDuplicate", "Boolean"),
      field("suspectedOriginalSubmissionId", "UUID", { optional: true }),
    ],
  });
  const exceptionQueue = el("submission-exception-queue", "SubmissionExceptionQueue", "READMODEL", t, ctx, {
    aggregate: "Submission",
    description: "1a.2: separate from the underwriter's main queue - submissions stuck in a failed normalization state, for operations to chase the broker or manually intervene.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("brokerFirmId", "String"),
      field("failureReason", "String"),
      field("attemptedAt", "DateTime"),
      field("status", "String", { example: "Awaiting Correction" }),
    ],
  });
  const authExceptionLog = el("broker-authorization-exception-log", "BrokerAuthorizationExceptionLog", "READMODEL", t, ctx, {
    aggregate: "Submission",
    description: "1a.3: never appears in any underwriter's queue. Visible to whoever manages broker/cell panel relationships (ops or business development) - may represent a legitimate request to extend the broker's panel, not just an error.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("brokerFirmId", "String"),
      field("requestedCellId", "String"),
      field("requestedClassOfBusiness", "String", { optional: true }),
      field("rejectionReason", "String"),
      field("rejectedAt", "DateTime"),
      field("reviewStatus", "String", { example: "Pending Review" }),
    ],
  });
  slices.push(slice("receive-broker-submission", t, "STATE_CHANGE", ctx, {
    commands: [cmd],
    events: [received, routingRejected, normalized, normalizationFailed, manuallyCorrected, duplicateDetected, superseded, confirmedDistinct],
    readmodels: [submissionQueue, exceptionQueue, authExceptionLog],
  }));
}

// ---------------------------------------------------------------------------
// 1b. Search & Retrieval
// Query-side capability over the event stream everything else produces.
// Command/event pairs capture search activity itself (audit/usage
// analytics); the read model is the actual point of the context.
// ---------------------------------------------------------------------------
{
  const ctx = "Search & Retrieval";
  const t = "Search Submission History";
  const searchCriteriaFields = [
    field("classOfBusiness", "String", { optional: true }),
    field("territory", "String", { optional: true }),
    field("brokerFirmId", "String", { optional: true }),
    field("namedInsured", "String", { optional: true }),
    field("dateRangeStart", "Date", { optional: true }),
    field("dateRangeEnd", "Date", { optional: true }),
    field("freeText", "String", { optional: true }),
  ];
  const cmd = el("search-submission-history-cmd", "SearchSubmissionHistory", "COMMAND", t, ctx, {
    aggregate: "Search",
    description: "1b.1 (underwriter, cell-scoped) and 1b.3 (operations, broker-scoped, cross-cell) - same command shape, different searcherRole/scope. OPEN QUESTIONS: is logging every search actually valuable, or over-engineering an audit trail for a read-only convenience feature (leaning toward query-level logging, not per-keystroke)? Does free-text hit normalized ADEPT fields only, or raw broker documents too (a much bigger document-search problem)?",
    fields: [
      field("searcherId", "String"),
      field("searcherRole", "String", { example: "Underwriter" }),
      field("searcherCellContext", "String", { optional: true }),
      field("searchCriteria", "Custom", { subfields: searchCriteriaFields }),
    ],
  });
  const performed = el("submission-history-search-performed", "SubmissionHistorySearchPerformed", "EVENT", t, ctx, {
    aggregate: "Search",
    description: "1b.1: underwriter searches by risk attribute, scoped to their own cell by default (same authority-context mechanism as Context 0).",
    fields: [
      field("searchId", "UUID", { idAttribute: true, generated: true }),
      field("searcherId", "String"),
      field("searcherRole", "String"),
      field("criteriaUsed", "Custom", { subfields: searchCriteriaFields }),
      field("resultCount", "Integer"),
      field("performedAt", "DateTime", { generated: true }),
    ],
  });
  const searchResults = el("submission-search-results", "SubmissionSearchResults", "READMODEL", t, ctx, {
    aggregate: "Search",
    description: "Ranked/filtered list of matching historic submissions, each linking back to the full submission record including downstream decisioning/bind/claims history if it progressed that far.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("namedInsured", "String"),
      field("classOfBusiness", "String"),
      field("territory", "String"),
      field("brokerFirmId", "String"),
      field("status", "String"),
      field("receivedAt", "DateTime"),
    ],
  });
  const crossCellAttempted = el("cross-cell-search-attempted", "CrossCellSearchAttempted", "EVENT", t, ctx, {
    aggregate: "Search",
    description: "1b.4 permission boundary: default to cell-scoped visibility for underwriters, cross-cell as an explicit logged elevated permission for ops/governance. Same audit-value reasoning as SubmissionRoutingRejected - kept visible for whoever manages access policy, not just silently enforced.",
    fields: [
      field("searcherId", "String"),
      field("searcherRole", "String"),
      field("requestedScope", "String", { example: "cross-cell" }),
      field("searchCriteria", "Custom", { subfields: searchCriteriaFields }),
      field("permitted", "Boolean"),
      field("deniedReason", "String", { optional: true }),
      field("attemptedAt", "DateTime", { generated: true }),
    ],
  });
  const brokerActivity = el("broker-activity-view", "BrokerActivityView", "READMODEL", t, ctx, {
    aggregate: "Search",
    description: "1b.3: all submissions from a given broker across all cells they're authorized for, with status breakdown - a cross-cell view ops needs for reconciliation even though individual underwriters don't for day-to-day work.",
    fields: [
      field("brokerFirmId", "String", { idAttribute: true }),
      field("cellBreakdown", "Custom", { cardinality: "List", subfields: [
        field("cellId", "String"), field("submissionCount", "Integer"), field("boundCount", "Integer"),
        field("declinedCount", "Integer"), field("expiredCount", "Integer"), field("inFlightCount", "Integer"),
      ] }),
      field("periodStart", "Date"),
      field("periodEnd", "Date"),
      field("generatedAt", "DateTime", { generated: true }),
    ],
  });
  const lineageRetrieved = el("submission-lineage-retrieved", "SubmissionLineageRetrieved", "EVENT", t, ctx, {
    aggregate: "Search",
    description: "1b.2, deliberately modeled as its own capability rather than folded into generic search: a claims handler verifying a claim against original bind terms is a direct traversal by policy/bind reference, not a search over criteria. Likely the highest-value piece of the whole 'AI-assisted search' claim - solves exactly the 'days of manual reconstruction' problem.",
    fields: [
      field("policyId", "UUID"),
      field("submissionId", "UUID"),
      field("lineageChain", "Custom", { subfields: [
        field("originalSubmissionId", "UUID"), field("decisioningPath", "String", { example: "within-authority" }),
        field("referredTo", "String", { optional: true }), field("quoteId", "UUID", { optional: true }),
        field("boundAt", "DateTime", { optional: true }),
      ] }),
      field("retrievedAt", "DateTime", { generated: true }),
    ],
  });
  const lineageAutomation = el("retrieve-submission-lineage-on-claim-notified", "RetrieveSubmissionLineageOnClaimNotified", "AUTOMATION", t, ctx, {
    aggregate: "Search",
    description: "Triggered automatically whenever ClaimNotified fires (Context 5), not a manual search action - matches how claims handlers actually work.",
    fields: [],
  });
  const originationView = el("policy-origination-view", "PolicyOriginationView", "READMODEL", t, ctx, {
    aggregate: "Search",
    description: "Given a bound policy reference from a claim, the full chain: original submission -> decisioning path -> quote -> bind terms, without constructing a search query.",
    fields: [
      field("policyId", "UUID", { idAttribute: true }),
      field("submissionId", "UUID"),
      field("namedInsured", "String"),
      field("classOfBusiness", "String"),
      field("originalSubmissionReceivedAt", "DateTime"),
      field("decisioningPath", "String"),
      field("referredTo", "String", { optional: true }),
      field("quoteTerms", "Custom"),
      field("boundTerms", "Custom"),
      field("boundAt", "DateTime"),
    ],
  });
  slices.push(slice("search-submission-history", t, "STATE_CHANGE", ctx, {
    commands: [cmd],
    events: [performed, crossCellAttempted, lineageRetrieved],
    readmodels: [searchResults, brokerActivity, originationView],
    processors: [lineageAutomation],
  }));
}

// ---------------------------------------------------------------------------
// 1c. Exposure Intelligence
// Largely event-driven off Context 3 (Binding) rather than user commands -
// the "actor" for most of this context is the system reacting to a
// bind/endorsement/cancellation event elsewhere.
// ---------------------------------------------------------------------------
{
  const ctx = "Exposure Intelligence";
  const t = "Update Exposure Projection";
  const exposureFields = [
    field("geocode", "String", { description: "OPEN QUESTION: should be a grid/hex-bucket aggregation, not exact lat/long - avoids false precision, keeps aggregation performant (standard cat-modeling practice)." }),
    field("cellId", "String"),
    field("classOfBusiness", "String"),
    field("policyReference", "UUID"),
    field("priorExposure", "Decimal"),
    field("newExposure", "Decimal"),
    field("deltaExposure", "Decimal", { description: "Signed - positive on bind/increase, negative on cancellation/reduction. Delta is explicitly modeled, not just a restated total, so the projection's history is itself auditable." }),
    field("providerShares", "Custom", { cardinality: "List", subfields: [field("provider", "String"), field("quotaShare", "Decimal")] }),
    field("effectiveDate", "Date"),
    field("changeReason", "String", { example: "bind | endorsement | cancellation" }),
    field("changeReference", "UUID"),
    field("updatedAt", "DateTime", { generated: true }),
  ];
  const updatedOnBind = el("exposure-projection-updated-bind", "ExposureProjectionUpdated", "EVENT", t, ctx, {
    aggregate: "ExposureProjection",
    description: "1c.1: PolicyBound triggers this reactive update. OPEN QUESTIONS: geocode precision (see field note); does this run synchronously with bind, or async as a downstream projection? Almost certainly async - the underwriter doesn't need to wait for it.",
    fields: exposureFields,
  });
  const onBindAutomation = el("update-exposure-projection-on-bind", "UpdateExposureProjectionOnBind", "AUTOMATION", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Trigger: PolicyBound event from Context 3. Reactive process/policy, not a human command - no one asks for this to happen.",
    fields: [],
  });
  const exposureMap = el("geographic-exposure-map", "GeographicExposureMap", "READMODEL", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Running aggregate exposure by geocode/region, queryable by peril, class, or provider. OPEN QUESTION: as-of-today vs at-any-future-date are genuinely different questions - may need to be date-aware rather than a single running total (see 1c.4).",
    fields: [
      field("geocode", "String", { idAttribute: true }),
      field("peril", "String"),
      field("classOfBusiness", "String"),
      field("runningTotalExposure", "Decimal"),
      field("contributingPolicies", "Custom", { cardinality: "List", subfields: [
        field("policyReference", "UUID"), field("cellId", "String"), field("exposureContribution", "Decimal"),
      ] }),
      field("asOfDate", "Date"),
    ],
  });
  const warningRaised = el("exposure-concentration-warning-raised", "ExposureConcentrationWarningRaised", "EVENT", t, ctx, {
    aggregate: "ExposureProjection",
    description: "1c.2: fires when a new projection update crosses a configured concentration threshold. OPEN QUESTION: threshold ownership is a domain-expert (actuarial/exposure management) decision, not an architecture decision - system enforces it, doesn't define it, same pattern as Context 0's authority rules. HAND-OFF NOTE: natural hand-off point into future Context 1e (Portfolio Governance).",
    fields: [
      field("geocode", "String"),
      field("perilCategory", "String"),
      field("contributingCells", "Custom", { cardinality: "List", subfields: [
        field("cellId", "String"), field("policyReference", "UUID"), field("exposureContribution", "Decimal"),
      ] }),
      field("combinedExposureValue", "Decimal"),
      field("thresholdBreached", "Decimal"),
      field("severityLevel", "String"),
      field("raisedAt", "DateTime", { generated: true }),
    ],
  });
  const stackingAutomation = el("detect-exposure-stacking", "DetectExposureStacking", "AUTOMATION", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Trigger: ExposureProjectionUpdated, evaluated against a configured concentration threshold. Runs after every projection update.",
    fields: [],
  });
  const concentrationDashboard = el("exposure-concentration-dashboard", "ExposureConcentrationDashboard", "READMODEL", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Visible to underwriting executives/portfolio managers, not individual cell underwriters - a cross-cell view by nature, same permission question as Search & Retrieval's cross-cell boundary.",
    fields: [
      field("geocode", "String"),
      field("perilCategory", "String"),
      field("combinedExposureValue", "Decimal"),
      field("thresholdBreached", "Decimal"),
      field("severityLevel", "String"),
      field("acknowledgedStatus", "String", { example: "Unacknowledged" }),
    ],
  });
  const acknowledgeCmd = el("acknowledge-exposure-concentration-warning-cmd", "AcknowledgeExposureConcentrationWarning", "COMMAND", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Without an explicit acknowledgment/escalation event, 'warned' and 'ignored' are indistinguishable in the audit trail.",
    fields: [
      field("warningId", "UUID"),
      field("acknowledgedBy", "String"),
      field("actionTaken", "String", { optional: true }),
    ],
  });
  const acknowledged = el("exposure-concentration-warning-acknowledged", "ExposureConcentrationWarningAcknowledged", "EVENT", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Closes the audit loop on ExposureConcentrationWarningRaised.",
    fields: [
      field("warningId", "UUID"),
      field("acknowledgedBy", "String"),
      field("actionTaken", "String", { optional: true }),
      field("acknowledgedAt", "DateTime", { generated: true }),
    ],
  });
  const updatedOnEndorsement = el("exposure-projection-updated-endorsement", "ExposureProjectionUpdated", "EVENT", t, ctx, {
    aggregate: "ExposureProjection",
    description: "1c.3: PolicyEndorsed triggers a delta update (not a re-stated total) - e.g. limit increased $10m -> $15m yields a +$5m delta, which can re-trigger 1c.2's stacking detection. OPEN QUESTION (for underwriting governance): does an endorsement that materially increases exposure need to re-trigger the authority/referral check from Context 2, even though the original bind didn't breach it?",
    fields: exposureFields,
  });
  const onEndorsementAutomation = el("update-exposure-projection-on-endorsement", "UpdateExposureProjectionOnEndorsement", "AUTOMATION", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Trigger: PolicyEndorsed event from Context 3.",
    fields: [],
  });
  const updatedOnCancellation = el("exposure-projection-updated-cancellation", "ExposureProjectionUpdated", "EVENT", t, ctx, {
    aggregate: "ExposureProjection",
    description: "1c.4: PolicyCancelled triggers a full-removal negative delta. OPEN QUESTION: is exposure removed effective the cancellation date, or immediately regardless of effective date (e.g. future-dated cancellation)? Mirrors the bind-time-vs-settlement-time gap already modeled in Bordereaux Settlement.",
    fields: exposureFields,
  });
  const onCancellationAutomation = el("update-exposure-projection-on-cancellation", "UpdateExposureProjectionOnCancellation", "AUTOMATION", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Trigger: PolicyCancelled event from Context 3.",
    fields: [],
  });
  const exportCmd = el("export-exposure-extract-cmd", "ExportExposureExtract", "COMMAND", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Requested by exposure management/actuarial team, or an automated scheduled job (e.g. quarterly cat model refresh).",
    fields: [
      field("requestingTeam", "String"),
      field("scopePeril", "String", { optional: true }),
      field("scopeRegion", "String", { optional: true }),
      field("scopeCellId", "String", { optional: true }),
      field("scopeProviderId", "String", { optional: true }),
      field("asOfDate", "Date"),
      field("formatRequired", "String", { example: "RMS location/policy file" }),
    ],
  });
  const extractGenerated = el("exposure-extract-generated", "ExposureExtractGenerated", "EVENT", t, ctx, {
    aggregate: "ExposureProjection",
    createsAggregate: true,
    description: "1c.5: the cleanest integration boundary in this context - Broker Connect's job stops at 'produce a correct, complete extract'. The cat model run, PML calculation, and hurricane-track overlay genuinely live outside this system's build scope. HAND-OFF NOTE: hand-off into future Context 1d (External Threat) via the cat model.",
    fields: [
      field("extractId", "UUID", { idAttribute: true, generated: true }),
      field("requestedScope", "Custom"),
      field("asOfDate", "Date"),
      field("generatedAt", "DateTime", { generated: true }),
      field("recordCount", "Integer"),
      field("destination", "String", { example: "RMS cat model" }),
    ],
  });
  const extractHistory = el("exposure-extract-history", "ExposureExtractHistory", "READMODEL", t, ctx, {
    aggregate: "ExposureProjection",
    description: "Audit trail of what was sent to cat modeling, when, and covering what scope - useful for reconciling 'what did the cat model actually see' if a PML figure is later questioned.",
    fields: [
      field("extractId", "UUID", { idAttribute: true }),
      field("requestedScope", "Custom"),
      field("asOfDate", "Date"),
      field("generatedAt", "DateTime"),
      field("recordCount", "Integer"),
      field("destination", "String"),
    ],
  });
  slices.push(slice("update-exposure-projection", t, "AUTOMATION", ctx, {
    commands: [exportCmd, acknowledgeCmd],
    events: [updatedOnBind, warningRaised, acknowledged, updatedOnEndorsement, updatedOnCancellation, extractGenerated],
    readmodels: [exposureMap, concentrationDashboard, extractHistory],
    processors: [onBindAutomation, stackingAutomation, onEndorsementAutomation, onCancellationAutomation],
  }));
}

// ---------------------------------------------------------------------------
// 2. Underwriting Decisioning
// ---------------------------------------------------------------------------
{
  const ctx = "Underwriting Decisioning";
  const t = "Assess Submission Against Authority";
  const cmd = el("assess-submission-cmd", "AssessSubmission", "COMMAND", t, ctx, {
    aggregate: "SubmissionAssessment",
    description: "Underwriter records a coverage/pricing decision on a received submission, checked against their delegated authority.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("underwriterId", "String"),
      field("cellId", "String"),
      field("proposedGrossPremium", "Decimal"),
      field("proposedLimit", "Decimal"),
      field("currency", "String"),
    ],
  });
  const withinAuth = el("submission-within-authority", "SubmissionWithinAuthority", "EVENT", t, ctx, {
    aggregate: "SubmissionAssessment",
    description: "Underwriter's decision falls within their delegated authority - no referral needed, ready to quote.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("underwriterId", "String"),
      field("cellId", "String"),
      field("approvedGrossPremium", "Decimal"),
      field("approvedLimit", "Decimal"),
      field("currency", "String"),
      field("decidedAt", "DateTime", { generated: true }),
    ],
  });
  const referred = el("submission-referred", "SubmissionReferred", "EVENT", t, ctx, {
    aggregate: "SubmissionAssessment",
    description: "Underwriter's decision exceeds their delegated authority and is referred up the Provider -> Cell -> Underwriter cascade.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("underwriterId", "String"),
      field("cellId", "String"),
      field("requestedGrossPremium", "Decimal"),
      field("requestedLimit", "Decimal"),
      field("breachedTier", "String"),
      field("referredTo", "String"),
      field("reason", "String"),
      field("decidedAt", "DateTime", { generated: true }),
    ],
  });
  const authorityView = el("authority-limit-view", "AuthorityLimit", "READMODEL", t, ctx, {
    aggregate: "AuthorityLimit",
    description: "Projection of the 3-tier authority cascade (Capacity Provider -> Cell -> Underwriter) used to validate an assessment. Populated by the Authority Administration slices (grant/revise/revoke).",
    fields: authorityLimitViewFields(),
  });
  slices.push(slice("assess-submission-against-authority", t, "STATE_CHANGE", ctx, {
    commands: [cmd], events: [withinAuth, referred], readmodels: [authorityView],
  }));
}

{
  const ctx = "Underwriting Decisioning";
  const t = "Decide Referral";
  const cmd = el("decide-referral-cmd", "DecideReferral", "COMMAND", t, ctx, {
    aggregate: "SubmissionAssessment",
    description: "Whoever the referral landed on (Cell Head Underwriter, or Capacity Provider if the Cell's own limit is also breached) approves or declines the referred terms.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("decidedBy", "String"),
      field("decision", "String", { example: "approve" }),
      field("comments", "String", { optional: true }),
    ],
  });
  const approved = el("referral-approved", "ReferralApproved", "EVENT", t, ctx, {
    aggregate: "SubmissionAssessment",
    description: "Referred terms are approved at this tier - submission moves to quotable status.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("decidedBy", "String"),
      field("approvedGrossPremium", "Decimal"),
      field("approvedLimit", "Decimal"),
      field("decidedAt", "DateTime", { generated: true }),
    ],
  });
  const declined = el("referral-declined", "ReferralDeclined", "EVENT", t, ctx, {
    aggregate: "SubmissionAssessment",
    description: "Referred terms are declined at this tier.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("decidedBy", "String"),
      field("reason", "String"),
      field("decidedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("decide-referral", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [approved, declined] }));
}

{
  const ctx = "Underwriting Decisioning";
  const t = "Decline Submission";
  const cmd = el("decline-submission-cmd", "DeclineSubmission", "COMMAND", t, ctx, {
    aggregate: "Submission",
    description: "Underwriter declines a submission outright - off-appetite class, sanctioned territory, or incomplete information beyond a reasonable follow-up window.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("underwriterId", "String"),
      field("reason", "String"),
    ],
  });
  const evt = el("submission-declined", "SubmissionDeclined", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "Submission will not proceed to quote.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("underwriterId", "String"),
      field("reason", "String"),
      field("declinedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("decline-submission", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Underwriting Decisioning";
  const t = "Issue Quote";
  const cmd = el("issue-quote-cmd", "IssueQuote", "COMMAND", t, ctx, {
    aggregate: "Submission",
    description: "Once terms are within authority (directly or via approved referral), the underwriter issues a formal quote back to the broker. Not yet a bind.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("grossPremium", "Decimal"),
      field("limit", "Decimal"),
      field("terms", "String"),
      field("validUntil", "Date"),
    ],
  });
  const evt = el("quote-issued", "QuoteIssued", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "Broker can now see a quote awaiting acceptance.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("quoteId", "UUID", { generated: true }),
      field("grossPremium", "Decimal"),
      field("limit", "Decimal"),
      field("terms", "String"),
      field("validUntil", "Date"),
      field("issuedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("issue-quote", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Underwriting Decisioning";
  const t = "Accept Quote";
  const cmd = el("accept-quote-cmd", "AcceptQuote", "COMMAND", t, ctx, {
    aggregate: "Submission",
    description: "Broker accepts the quote - this is what makes the submission eligible to bind. Modeled as its own event because a quote can also expire or be declined without binding.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("acceptedAt", "DateTime", { optional: true }),
    ],
  });
  const evt = el("quote-accepted", "QuoteAccepted", "EVENT", t, ctx, {
    aggregate: "Submission",
    description: "Submission is now eligible to bind.",
    fields: [
      field("submissionId", "UUID", { idAttribute: true }),
      field("acceptedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("accept-quote", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

// ---------------------------------------------------------------------------
// 3. Binding
// ---------------------------------------------------------------------------
{
  const ctx = "Binding";
  const t = "Bind Policy";
  const cmd = el("bind-policy-cmd", "BindPolicy", "COMMAND", t, ctx, {
    aggregate: "Policy",
    createsAggregate: true,
    description: "Binding is the moment a policy transaction is written to the immutable ledger. A single bind can span multiple capacity providers, split by quota share.",
    fields: [
      field("submissionId", "UUID"),
      field("cellId", "String"),
      field("effectiveDate", "Date"),
      field("grossPremium", "Decimal"),
      field("quotaShareSplits", "Custom", {
        cardinality: "List",
        subfields: [field("provider", "String"), field("percentage", "Decimal")],
      }),
    ],
  });
  const evt = el("policy-bound", "PolicyBound", "EVENT", t, ctx, {
    aggregate: "Policy",
    description: "Immutable ledger entry for a new bind, with per-provider financial breakdown (gross, brokerage, MGA fee, net premium).",
    fields: [
      field("policyId", "UUID", { idAttribute: true, generated: true }),
      field("submissionId", "UUID"),
      field("grossPremium", "Decimal"),
      field("brokerageDeducted", "Decimal"),
      field("mgaFeeDeducted", "Decimal"),
      field("netPremiumPerProvider", "Custom", {
        cardinality: "List",
        subfields: [field("provider", "String"), field("amount", "Decimal")],
      }),
      field("quotaShareSplits", "Custom", {
        cardinality: "List",
        subfields: [field("provider", "String"), field("percentage", "Decimal")],
      }),
      field("boundAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("bind-policy", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Binding";
  const t = "Endorse Policy";
  const cmd = el("endorse-policy-cmd", "EndorsePolicy", "COMMAND", t, ctx, {
    aggregate: "Policy",
    description: "A mid-term change to a bound policy (extended limit, additional insured, corrected term). A new ledger entry, not an edit to the original bind.",
    fields: [
      field("policyId", "UUID", { idAttribute: true }),
      field("changeDescription", "String"),
      field("premiumAdjustment", "Decimal"),
      field("effectiveDate", "Date"),
    ],
  });
  const evt = el("policy-endorsed", "PolicyEndorsed", "EVENT", t, ctx, {
    aggregate: "Policy",
    description: "Appended endorsement record - original bind's ledger line is untouched.",
    fields: [
      field("policyId", "UUID", { idAttribute: true }),
      field("endorsementId", "UUID", { generated: true }),
      field("changeDescription", "String"),
      field("premiumAdjustment", "Decimal"),
      field("effectiveDate", "Date"),
    ],
  });
  slices.push(slice("endorse-policy", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Binding";
  const t = "Cancel Policy";
  const cmd = el("cancel-policy-cmd", "CancelPolicy", "COMMAND", t, ctx, {
    aggregate: "Policy",
    description: "Broker-requested or underwriter-initiated cancellation.",
    fields: [
      field("policyId", "UUID", { idAttribute: true }),
      field("cancellationDate", "Date"),
      field("returnPremium", "Decimal"),
      field("reason", "String"),
    ],
  });
  const evt = el("policy-cancelled", "PolicyCancelled", "EVENT", t, ctx, {
    aggregate: "Policy",
    description: "Appended cancellation record - generates a return-premium bordereau line, not an edit to the original bind.",
    fields: [
      field("policyId", "UUID", { idAttribute: true }),
      field("returnPremium", "Decimal"),
      field("reason", "String"),
      field("cancelledAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("cancel-policy", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Binding";
  const t = "Renew Policy";
  const cmd = el("renew-policy-cmd", "RenewPolicy", "COMMAND", t, ctx, {
    aggregate: "Policy",
    createsAggregate: true,
    description: "Renewal is modeled as a fresh submission/assessment/bind cycle referencing the expiring policy, not a mutation of it.",
    fields: [
      field("expiringPolicyId", "UUID"),
      field("newSubmissionId", "UUID"),
    ],
  });
  const evt = el("policy-renewed", "PolicyRenewed", "EVENT", t, ctx, {
    aggregate: "Policy",
    description: "Links a new policy back to the one it renews.",
    fields: [
      field("newPolicyId", "UUID", { idAttribute: true, generated: true }),
      field("expiringPolicyId", "UUID"),
      field("renewedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("renew-policy", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

// ---------------------------------------------------------------------------
// 4. Operations & Bordereaux Settlement
// ---------------------------------------------------------------------------
{
  const ctx = "Bordereaux Settlement";
  const t = "Generate Bordereau";
  const proc = el("generate-bordereau-automation", "GenerateBordereauAutomation", "AUTOMATION", t, ctx, {
    aggregate: "Bordereau",
    description: "Scheduled period-close automation, scoped to one cell + one capacity provider + one period. Pulls together every policy transaction (bind/endorsement/cancellation) in that window not yet included in a prior bordereau.",
    fields: [
      field("cellId", "String"),
      field("providerId", "String"),
      field("periodStart", "Date"),
      field("periodEnd", "Date"),
    ],
  });
  const evt = el("bordereau-drafted", "BordereauDrafted", "EVENT", t, ctx, {
    aggregate: "Bordereau",
    createsAggregate: true,
    description: "Draft bordereau created for a cell+provider+period. Becomes a real financial obligation only once agreed (see AgreeBordereau).",
    fields: [
      field("bordereauId", "UUID", { idAttribute: true, generated: true }),
      field("cellId", "String"),
      field("providerId", "String"),
      field("periodStart", "Date"),
      field("periodEnd", "Date"),
      field("lineCount", "Integer"),
      field("totalNetPremium", "Decimal"),
      field("currency", "String"),
      field("status", "String", { example: "Draft" }),
      field("draftedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("generate-bordereau", t, "AUTOMATION", ctx, { processors: [proc], events: [evt] }));
}

{
  const ctx = "Bordereaux Settlement";
  const t = "Raise Bordereau Query";
  const cmd = el("raise-bordereau-query-cmd", "RaiseBordereauQuery", "COMMAND", t, ctx, {
    aggregate: "Bordereau",
    description: "Capacity provider (or their TPA) queries a specific line - e.g. wrong quota share applied, a risk outside authority, a rate mismatch.",
    fields: [
      field("bordereauId", "UUID"),
      field("lineId", "UUID"),
      field("queryReason", "String"),
    ],
  });
  const evt = el("bordereau-line-queried", "BordereauLineQueried", "EVENT", t, ctx, {
    aggregate: "Bordereau",
    description: "Line status becomes Queried, independent of the bordereau's own overall status - each line tracks its own draft/queried/agreed state.",
    fields: [
      field("bordereauId", "UUID"),
      field("lineId", "UUID"),
      field("queryId", "UUID", { idAttribute: true, generated: true }),
      field("queryReason", "String"),
      field("raisedAt", "DateTime", { generated: true }),
    ],
  });
  const readmodel = el("bordereau-detail", "BordereauDetail", "READMODEL", t, ctx, {
    aggregate: "Bordereau",
    description: "Working view of a bordereau and its lines for the provider/TPA review window - each line carries its own status (draft/queried/agreed/settled), not just the bordereau's overall status.",
    fields: [
      field("bordereauId", "UUID", { idAttribute: true }),
      field("cellId", "String"),
      field("providerId", "String"),
      field("periodStart", "Date"),
      field("periodEnd", "Date"),
      field("status", "String"),
      field("currency", "String"),
      field("lines", "Custom", {
        cardinality: "List",
        subfields: [
          field("lineId", "UUID"),
          field("policyTransactionId", "UUID"),
          field("transactionType", "String", { example: "new business" }),
          field("grossPremium", "Decimal"),
          field("brokerageDeducted", "Decimal"),
          field("mgaFeeDeducted", "Decimal"),
          field("netPremium", "Decimal"),
          field("providerShare", "Decimal"),
          field("status", "String", { example: "Draft" }),
        ],
      }),
    ],
  });
  slices.push(slice("raise-bordereau-query", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt], readmodels: [readmodel] }));
}

{
  const ctx = "Bordereaux Settlement";
  const t = "Resolve Bordereau Query";
  const cmd = el("resolve-bordereau-query-cmd", "ResolveBordereauQuery", "COMMAND", t, ctx, {
    aggregate: "Bordereau",
    description: "Underwriter/Operations resolves a previously raised query.",
    fields: [
      field("queryId", "UUID", { idAttribute: true }),
      field("resolution", "String"),
    ],
  });
  const evt = el("bordereau-line-resolved", "BordereauLineResolved", "EVENT", t, ctx, {
    aggregate: "Bordereau",
    description: "Line status moves out of Queried. A single disputed line can span multiple periods before resolution.",
    fields: [
      field("queryId", "UUID", { idAttribute: true }),
      field("resolution", "String"),
      field("resolvedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("resolve-bordereau-query", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Bordereaux Settlement";
  const t = "Agree Bordereau";
  const cmd = el("agree-bordereau-cmd", "AgreeBordereau", "COMMAND", t, ctx, {
    aggregate: "Bordereau",
    description: "Once every queried line is resolved, the capacity provider agrees the bordereau - this is the point it becomes a real financial obligation, not just a draft.",
    fields: [field("bordereauId", "UUID", { idAttribute: true })],
  });
  const evt = el("bordereau-agreed", "BordereauAgreed", "EVENT", t, ctx, {
    aggregate: "Bordereau",
    fields: [
      field("bordereauId", "UUID", { idAttribute: true }),
      field("agreedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("agree-bordereau", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Bordereaux Settlement";
  const t = "Settle Bordereau";
  const cmd = el("settle-bordereau-cmd", "SettleBordereau", "COMMAND", t, ctx, {
    aggregate: "Bordereau",
    description: "Cash actually moves - net premium cell to provider (or claims float top-up provider to cell). Multi-currency: cell writes premium in local currency, providers may need it reported in USD, so FX rate at settlement is captured.",
    fields: [
      field("bordereauId", "UUID", { idAttribute: true }),
      field("settledAmount", "Decimal"),
      field("settlementDate", "Date"),
      field("currency", "String"),
      field("fxRate", "Decimal", { optional: true }),
    ],
  });
  const evt = el("bordereau-settled", "BordereauSettled", "EVENT", t, ctx, {
    aggregate: "Bordereau",
    fields: [
      field("bordereauId", "UUID", { idAttribute: true }),
      field("settledAmount", "Decimal"),
      field("settlementDate", "Date"),
      field("currency", "String"),
      field("fxRate", "Decimal", { optional: true }),
      field("settledAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("settle-bordereau", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Bordereaux Settlement";
  const t = "Raise Prior-Period Adjustment";
  const cmd = el("raise-prior-period-adjustment-cmd", "RaisePriorPeriodAdjustment", "COMMAND", t, ctx, {
    aggregate: "Bordereau",
    description: "An error found after a bordereau was already agreed is corrected via a new adjustment line in a future period - never by editing history. Bordereaux are an append-only ledger, mirroring the underlying policy transactions.",
    fields: [
      field("originalBordereauId", "UUID"),
      field("originalLineId", "UUID"),
      field("adjustmentAmount", "Decimal"),
      field("reason", "String"),
    ],
  });
  const evt = el("adjustment-line-raised", "AdjustmentLineRaised", "EVENT", t, ctx, {
    aggregate: "Bordereau",
    createsAggregate: false,
    description: "New adjustment line appended to a future period's bordereau - the original line and bordereau remain untouched.",
    fields: [
      field("adjustmentId", "UUID", { idAttribute: true, generated: true }),
      field("originalBordereauId", "UUID"),
      field("originalLineId", "UUID"),
      field("adjustmentAmount", "Decimal"),
      field("appearsInBordereauId", "UUID"),
      field("reason", "String"),
      field("raisedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("raise-prior-period-adjustment", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

// ---------------------------------------------------------------------------
// 5. Claims
// ---------------------------------------------------------------------------
{
  const ctx = "Claims";
  const t = "Notify Claim";
  const cmd = el("notify-claim-cmd", "NotifyClaim", "COMMAND", t, ctx, {
    aggregate: "Claim",
    createsAggregate: true,
    fields: [
      field("policyId", "UUID"),
      field("lossDate", "Date"),
      field("description", "String"),
      field("notifiedBy", "String"),
    ],
  });
  const evt = el("claim-notified", "ClaimNotified", "EVENT", t, ctx, {
    aggregate: "Claim",
    fields: [
      field("claimId", "UUID", { idAttribute: true, generated: true }),
      field("policyId", "UUID"),
      field("lossDate", "Date"),
      field("description", "String"),
      field("notifiedBy", "String"),
      field("notifiedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("notify-claim", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Claims";
  const t = "Set Claim Reserve";
  const cmd = el("set-claim-reserve-cmd", "SetClaimReserve", "COMMAND", t, ctx, {
    aggregate: "Claim",
    fields: [
      field("claimId", "UUID", { idAttribute: true }),
      field("reserveAmount", "Decimal"),
      field("currency", "String"),
    ],
  });
  const evt = el("claim-reserve-set", "ClaimReserveSet", "EVENT", t, ctx, {
    aggregate: "Claim",
    fields: [
      field("claimId", "UUID", { idAttribute: true }),
      field("previousReserve", "Decimal", { optional: true }),
      field("newReserve", "Decimal"),
      field("currency", "String"),
      field("setAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("set-claim-reserve", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Claims";
  const t = "Pay Claim";
  const cmd = el("pay-claim-cmd", "PayClaim", "COMMAND", t, ctx, {
    aggregate: "Claim",
    description: "Like premium, a claim payment splits by each capacity provider's quota share.",
    fields: [
      field("claimId", "UUID", { idAttribute: true }),
      field("paymentAmount", "Decimal"),
      field("providerShares", "Custom", {
        cardinality: "List",
        subfields: [field("provider", "String"), field("amount", "Decimal")],
      }),
    ],
  });
  const evt = el("claim-paid", "ClaimPaid", "EVENT", t, ctx, {
    aggregate: "Claim",
    fields: [
      field("claimId", "UUID", { idAttribute: true }),
      field("paymentId", "UUID", { generated: true }),
      field("paymentAmount", "Decimal"),
      field("providerShares", "Custom", {
        cardinality: "List",
        subfields: [field("provider", "String"), field("amount", "Decimal")],
      }),
      field("paidAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("pay-claim", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Claims";
  const t = "Close Claim";
  const cmd = el("close-claim-cmd", "CloseClaim", "COMMAND", t, ctx, {
    aggregate: "Claim",
    fields: [
      field("claimId", "UUID", { idAttribute: true }),
      field("closureReason", "String"),
    ],
  });
  const evt = el("claim-closed", "ClaimClosed", "EVENT", t, ctx, {
    aggregate: "Claim",
    fields: [
      field("claimId", "UUID", { idAttribute: true }),
      field("closureReason", "String"),
      field("closedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("close-claim", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

{
  const ctx = "Claims";
  const t = "Reopen Claim";
  const cmd = el("reopen-claim-cmd", "ReopenClaim", "COMMAND", t, ctx, {
    aggregate: "Claim",
    fields: [
      field("claimId", "UUID", { idAttribute: true }),
      field("reason", "String"),
    ],
  });
  const evt = el("claim-reopened", "ClaimReopened", "EVENT", t, ctx, {
    aggregate: "Claim",
    fields: [
      field("claimId", "UUID", { idAttribute: true }),
      field("reason", "String"),
      field("reopenedAt", "DateTime", { generated: true }),
    ],
  });
  slices.push(slice("reopen-claim", t, "STATE_CHANGE", ctx, { commands: [cmd], events: [evt] }));
}

// ---------------------------------------------------------------------------
// Write output
// ---------------------------------------------------------------------------
const slicesDir = join(__dirname, "slices");
mkdirSync(slicesDir, { recursive: true });

for (const s of slices) {
  writeFileSync(join(slicesDir, `${s.id}.json`), JSON.stringify({ slices: [s] }, null, 2) + "\n");
}

// Combined payload MUST be compact (no indentation) - a pretty-printed multi-slice
// payload (~140KB for 21 slices) triggers an opaque HTTP 500 from import-config that
// a compact payload with identical content (~86KB) does not. See
// eventmodelers-import-config-guide.md "Payload size" section.
writeFileSync(join(__dirname, "import-config.json"), JSON.stringify({ slices }));

console.log(`Wrote ${slices.length} slice files to ${slicesDir}`);
console.log(`Wrote combined import-config.json (${slices.length} slices)`);
let commands = 0, events = 0, readmodels = 0, processors = 0, screens = 0;
for (const s of slices) {
  commands += s.commands.length;
  events += s.events.length;
  readmodels += s.readmodels.length;
  processors += s.processors.length;
  screens += s.screens.length;
}
console.log(`Totals: ${commands} commands, ${events} events, ${readmodels} readmodels, ${processors} processors, ${screens} screens`);
