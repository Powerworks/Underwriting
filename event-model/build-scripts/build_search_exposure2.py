import json
import time
import urllib.request
import uuid

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"
CHAPTER_ID = "e96f1046-4ab4-4654-89f3-8074c0aa06f4"
SWIMLANE_ROW = "05b234f6-6f03-48fd-9ddd-9d0f4b0f316b"
INTERACTION_ROW = "e98ea909-683d-465b-be6c-cd6596b4e0cd"
ACTOR_ROW = "1af53c6a-6c34-4cd3-987c-7ac085fe0cf0"

HEADERS = {
    "Content-Type": "application/json",
    "x-token": TOKEN,
    "x-board-id": BOARD_ID,
    "x-user-id": "timeline-skill",
}


def call(method, path, body=None):
    url = f"{BASE_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, headers=HEADERS, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read())


def field(name, type_, **opts):
    return {
        "name": name,
        "type": type_,
        "cardinality": opts.get("cardinality", "Single"),
        "optional": opts.get("optional", False),
        "idAttribute": opts.get("idAttribute", False),
        "generated": opts.get("generated", False),
        "edited": opts.get("edited", True),
        "query": opts.get("query", False),
        "showAttributes": opts.get("showAttributes", False),
        "technicalAttribute": opts.get("technicalAttribute", False),
        "subfields": opts.get("subfields", []),
    }


with open("/tmp/claude_column_ids2.json") as f:
    col = json.load(f)

now = int(time.time() * 1000)
events = []
node_ids = {}


def cell(col_label, row_id):
    return f"{row_id}-{col[col_label]}"


def add_node(node_type, title, col_label, row_id, fields_list, description):
    node_id = str(uuid.uuid4())
    events.append({
        "id": str(uuid.uuid4()),
        "eventType": "node:created",
        "nodeId": node_id,
        "boardId": BOARD_ID,
        "timestamp": now,
        "chapterId": CHAPTER_ID,
        "cellId": cell(col_label, row_id),
        "meta": {"type": node_type, "title": title, "fields": fields_list, "description": description},
        "node": {"id": node_id, "data": {"title": title}},
    })
    node_ids[title + "@" + col_label] = node_id
    return node_id


# ===========================================================================
# Search & Retrieval
# ===========================================================================

search_criteria_subfields = [
    field("classOfBusiness", "String", optional=True),
    field("territory", "String", optional=True),
    field("brokerFirmId", "String", optional=True),
    field("namedInsured", "String", optional=True),
    field("dateRangeStart", "Date", optional=True),
    field("dateRangeEnd", "Date", optional=True),
    field("freeText", "String", optional=True),
]

add_node(
    "COMMAND", "SearchSubmissionHistory", "SearchSubmissionHistory-1", INTERACTION_ROW,
    [
        field("searcherId", "String"),
        field("searcherRole", "String", example="Underwriter"),
        field("searcherCellContext", "String", optional=True, description="Scopes results to the searcher's own cell by default."),
        field("searchCriteria", "Custom", subfields=search_criteria_subfields),
    ],
    "Used by underwriters (cell-scoped, 1b.1) and operations (broker-scoped, cross-cell, 1b.3) with the same shape, different searcherRole/scope. OPEN QUESTION (flagged by BA): is logging every search actually valuable, or over-engineering an audit trail for a read-only convenience feature? Leaning toward logging at the query level, not per-keystroke, given fast search-as-you-type UX. Also open: does free-text hit normalized ADEPT fields only, or raw broker documents too (a much bigger document-search problem)?",
)

add_node(
    "EVENT", "SubmissionHistorySearchPerformed", "SearchSubmissionHistory-1", SWIMLANE_ROW,
    [
        field("searchId", "UUID", idAttribute=True, generated=True),
        field("searcherId", "String"),
        field("searcherRole", "String"),
        field("criteriaUsed", "Custom", subfields=search_criteria_subfields),
        field("resultCount", "Integer", description="Count only - the result set itself is transient, not an audit-worthy fact in itself."),
        field("performedAt", "DateTime", generated=True),
    ],
    "1b.1: underwriter searches by risk attribute, scoped to their own cell by default (same authority-context mechanism as Context 0).",
)

add_node(
    "READMODEL", "SubmissionSearchResults", "SearchSubmissionHistory-2", INTERACTION_ROW,
    [
        field("submissionId", "UUID", idAttribute=True),
        field("namedInsured", "String"),
        field("classOfBusiness", "String"),
        field("territory", "String"),
        field("brokerFirmId", "String"),
        field("status", "String"),
        field("receivedAt", "DateTime"),
    ],
    "Ranked/filtered list of matching historic submissions, each linking back to the full submission record including downstream decisioning/bind/claims history if it progressed that far.",
)
add_node(
    "EVENT", "SubmissionHistorySearchPerformed", "SearchSubmissionHistory-2", SWIMLANE_ROW,
    [field("searchId", "UUID", idAttribute=True, generated=True), field("resultCount", "Integer")],
    "Second instance of the same event type, paired here with the SubmissionSearchResults projection it feeds.",
)

add_node(
    "EVENT", "CrossCellSearchAttempted", "CrossCellSearchAttempted", SWIMLANE_ROW,
    [
        field("searcherId", "String"),
        field("searcherRole", "String"),
        field("requestedScope", "String", example="cross-cell"),
        field("searchCriteria", "Custom", subfields=search_criteria_subfields),
        field("permitted", "Boolean"),
        field("deniedReason", "String", optional=True),
        field("attemptedAt", "DateTime", generated=True),
    ],
    "1b.4 permission-boundary design decision: default to cell-scoped visibility for underwriters (an underwriter in Aviation cannot by default see a submission that went to Energy for the same named insured), with cross-cell visibility as an explicit, logged, elevated permission for ops/governance (1b.3). Same audit-value reasoning as SubmissionRoutingRejected in Submission Intake - kept visible for whoever manages access policy, not just silently enforced. Possible middle ground worth exploring: a narrower 'same named insured, cross-cell' flag visible to underwriters without exposing full submission/pricing detail.",
)

add_node(
    "READMODEL", "BrokerActivityView", "CrossCellSearchAttempted", INTERACTION_ROW,
    [
        field("brokerFirmId", "String", idAttribute=True),
        field("cellBreakdown", "Custom", cardinality="List", subfields=[
            field("cellId", "String"),
            field("submissionCount", "Integer"),
            field("boundCount", "Integer"),
            field("declinedCount", "Integer"),
            field("expiredCount", "Integer"),
            field("inFlightCount", "Integer"),
        ]),
        field("periodStart", "Date"),
        field("periodEnd", "Date"),
        field("generatedAt", "DateTime", generated=True),
    ],
    "1b.3: all submissions from a given broker across all cells they're authorized for, with status breakdown - a cross-cell view ops needs for reconciliation even though individual underwriters don't for day-to-day work. Confirms Search & Retrieval needs at least two permission tiers baked in from day one (cell-scoped vs cross-cell), not a generic search with permission filtering bolted on afterward.",
)

add_node(
    "EVENT", "SubmissionLineageRetrieved", "SubmissionLineageRetrieved", SWIMLANE_ROW,
    [
        field("policyId", "UUID"),
        field("submissionId", "UUID"),
        field("lineageChain", "Custom", description="originalSubmissionId, decisioningPath (within-authority|referred), quoteId, bindDetails", subfields=[
            field("originalSubmissionId", "UUID"),
            field("decisioningPath", "String", example="within-authority"),
            field("referredTo", "String", optional=True),
            field("quoteId", "UUID", optional=True),
            field("boundAt", "DateTime", optional=True),
        ]),
        field("retrievedAt", "DateTime", generated=True),
    ],
    "1b.2, modeled deliberately as its own capability rather than folded into generic search (BA judgment call, agreed): a claims handler needing to verify a claim against original bind terms is a direct traversal by policy/bind reference, not a search over criteria. Likely one of the highest-value pieces of the whole 'AI-assisted search' capability described in the press coverage - solves exactly the 'days of manual reconstruction' problem.",
)
add_node(
    "AUTOMATION", "RetrieveSubmissionLineageOnClaimNotified", "SubmissionLineageRetrieved", ACTOR_ROW,
    [],
    "Triggered automatically whenever ClaimNotified fires (Context 5), not a manual search action - matches how claims handlers actually work.",
)
add_node(
    "READMODEL", "PolicyOriginationView", "SubmissionLineageRetrieved", INTERACTION_ROW,
    [
        field("policyId", "UUID", idAttribute=True),
        field("submissionId", "UUID"),
        field("namedInsured", "String"),
        field("classOfBusiness", "String"),
        field("originalSubmissionReceivedAt", "DateTime"),
        field("decisioningPath", "String"),
        field("referredTo", "String", optional=True),
        field("quoteTerms", "Custom"),
        field("boundTerms", "Custom"),
        field("boundAt", "DateTime"),
    ],
    "Given a bound policy reference from a claim, the full chain: original submission -> decisioning path -> quote -> bind terms, without constructing a search query. Retrieved by policy/bind reference, not free-text search.",
)

# ===========================================================================
# Exposure Intelligence
# ===========================================================================

exposure_projection_fields = [
    field("geocode", "String", description="OPEN QUESTION (flagged by BA, pushing back on exact lat/long): should be a grid/hex-bucket aggregation, not point-in-point matching - avoids false precision and keeps aggregation queries performant, standard practice in cat modeling."),
    field("cellId", "String"),
    field("classOfBusiness", "String"),
    field("policyReference", "UUID"),
    field("priorExposure", "Decimal"),
    field("newExposure", "Decimal"),
    field("deltaExposure", "Decimal", description="Signed - positive on bind/increase, negative on cancellation/reduction. Delta is explicitly modeled, not just a restated total, so the projection's history is itself auditable."),
    field("providerShares", "Custom", cardinality="List", subfields=[field("provider", "String"), field("quotaShare", "Decimal")]),
    field("effectiveDate", "Date"),
    field("changeReason", "String", example="bind | endorsement | cancellation"),
    field("changeReference", "UUID", description="Bind, endorsement, or cancellation reference that caused this delta."),
    field("updatedAt", "DateTime", generated=True),
]

add_node(
    "EVENT", "ExposureProjectionUpdated", "ExposureProjectionUpdated-Bind", SWIMLANE_ROW,
    exposure_projection_fields,
    "1c.1: PolicyBound triggers this reactive update (Policy: UpdateExposureProjectionOnBind - a standing rule, not a user-issued command; no one asks for this to happen). OPEN QUESTIONS: (1) geocode precision - see field note; (2) does this run synchronously with bind, or async as a downstream projection? Almost certainly async - the underwriter doesn't need to wait for it at bind time; worth stating explicitly so it isn't accidentally built as a blocking step.",
)
add_node(
    "AUTOMATION", "UpdateExposureProjectionOnBind", "ExposureProjectionUpdated-Bind", ACTOR_ROW,
    [],
    "Trigger: PolicyBound event from Context 3 (Binding). Reactive process/policy, not a human command.",
)
add_node(
    "READMODEL", "GeographicExposureMap", "ExposureProjectionUpdated-Bind", INTERACTION_ROW,
    [
        field("geocode", "String", idAttribute=True),
        field("peril", "String"),
        field("classOfBusiness", "String"),
        field("runningTotalExposure", "Decimal"),
        field("contributingPolicies", "Custom", cardinality="List", subfields=[
            field("policyReference", "UUID"), field("cellId", "String"), field("exposureContribution", "Decimal"),
        ]),
        field("asOfDate", "Date", description="OPEN QUESTION: as-of-today vs at-any-future-date are genuinely different questions (see SubmissionSuperseded-style reasoning applied to exposure) - a single running total may be the wrong shape; may need to be date-aware."),
    ],
    "Running aggregate exposure by geocode/region, queryable by peril, class, or provider.",
)

add_node(
    "EVENT", "ExposureConcentrationWarningRaised", "ExposureConcentrationWarningRaised", SWIMLANE_ROW,
    [
        field("geocode", "String"),
        field("perilCategory", "String"),
        field("contributingCells", "Custom", cardinality="List", subfields=[field("cellId", "String"), field("policyReference", "UUID"), field("exposureContribution", "Decimal")]),
        field("combinedExposureValue", "Decimal"),
        field("thresholdBreached", "Decimal"),
        field("severityLevel", "String"),
        field("raisedAt", "DateTime", generated=True),
    ],
    "1c.2: fires when a new projection update crosses a configured concentration threshold (e.g. Florida-wind at a geocode). OPEN QUESTION: threshold ownership is a domain-expert (actuarial/exposure management) decision, not an architecture decision - system enforces it, doesn't define it, same pattern as Context 0's authority rules. HAND-OFF NOTE: this is the natural hand-off point into future Context 1e (Portfolio Governance).",
)
add_node(
    "AUTOMATION", "DetectExposureStacking", "ExposureConcentrationWarningRaised", ACTOR_ROW,
    [],
    "Trigger: ExposureProjectionUpdated, evaluated against a configured concentration threshold. Runs after every projection update.",
)
add_node(
    "READMODEL", "ExposureConcentrationDashboard", "ExposureConcentrationWarningRaised", INTERACTION_ROW,
    [
        field("geocode", "String"),
        field("perilCategory", "String"),
        field("combinedExposureValue", "Decimal"),
        field("thresholdBreached", "Decimal"),
        field("severityLevel", "String"),
        field("acknowledgedStatus", "String", example="Unacknowledged"),
    ],
    "Visible to underwriting executives/portfolio managers, not individual cell underwriters - a cross-cell view by nature, same permission question as Search & Retrieval's cross-cell boundary.",
)

add_node(
    "COMMAND", "AcknowledgeExposureConcentrationWarning", "ExposureConcentrationWarningAcknowledged", INTERACTION_ROW,
    [field("warningId", "UUID"), field("acknowledgedBy", "String"), field("actionTaken", "String", optional=True)],
    "Added per BA judgment call (agreed): without an explicit acknowledgment/escalation event, 'warned' and 'ignored' are indistinguishable in the audit trail.",
)
add_node(
    "EVENT", "ExposureConcentrationWarningAcknowledged", "ExposureConcentrationWarningAcknowledged", SWIMLANE_ROW,
    [field("warningId", "UUID"), field("acknowledgedBy", "String"), field("actionTaken", "String", optional=True), field("acknowledgedAt", "DateTime", generated=True)],
    "Closes the audit loop on ExposureConcentrationWarningRaised.",
)

add_node(
    "EVENT", "ExposureProjectionUpdated", "ExposureProjectionUpdated-Endorsement", SWIMLANE_ROW,
    exposure_projection_fields,
    "1c.3: PolicyEndorsed triggers a delta update (not a re-stated total) - e.g. limit increased $10m -> $15m yields a +$5m delta, which can itself re-trigger 1c.2's stacking detection. OPEN QUESTION (raised for underwriting governance, not decided here): does an endorsement that materially increases exposure need to re-trigger the authority/referral check from Context 2 (AssessSubmission/referral cascade), even though the original bind didn't breach it? Real gap if endorsements can silently grow exposure past what was ever authorized.",
)
add_node(
    "AUTOMATION", "UpdateExposureProjectionOnEndorsement", "ExposureProjectionUpdated-Endorsement", ACTOR_ROW,
    [],
    "Trigger: PolicyEndorsed event from Context 3 (Binding).",
)

add_node(
    "EVENT", "ExposureProjectionUpdated", "ExposureProjectionUpdated-Cancellation", SWIMLANE_ROW,
    exposure_projection_fields,
    "1c.4: PolicyCancelled triggers a full-removal negative delta. OPEN QUESTION: is exposure removed effective the cancellation date, or immediately on the cancellation event regardless of effective date (e.g. a future-dated cancellation)? Mirrors the bind-time-vs-settlement-time gap already modeled in Bordereaux Settlement - 'as-of-today' and 'at-any-future-date' exposure are genuinely different questions; the projection may need to be date-aware rather than a single running total.",
)
add_node(
    "AUTOMATION", "UpdateExposureProjectionOnCancellation", "ExposureProjectionUpdated-Cancellation", ACTOR_ROW,
    [],
    "Trigger: PolicyCancelled event from Context 3 (Binding).",
)

add_node(
    "COMMAND", "ExportExposureExtract", "ExposureExtractGenerated-1", INTERACTION_ROW,
    [
        field("requestingTeam", "String"),
        field("scopePeril", "String", optional=True),
        field("scopeRegion", "String", optional=True),
        field("scopeCellId", "String", optional=True),
        field("scopeProviderId", "String", optional=True),
        field("asOfDate", "Date"),
        field("formatRequired", "String", example="RMS location/policy file"),
    ],
    "Requested by exposure management/actuarial team, or an automated scheduled job (e.g. quarterly cat model refresh).",
)
add_node(
    "EVENT", "ExposureExtractGenerated", "ExposureExtractGenerated-1", SWIMLANE_ROW,
    [
        field("extractId", "UUID", idAttribute=True, generated=True),
        field("requestedScope", "Custom"),
        field("asOfDate", "Date"),
        field("generatedAt", "DateTime", generated=True),
        field("recordCount", "Integer"),
        field("destination", "String", example="RMS cat model"),
    ],
    "1c.5: the cleanest integration boundary in this context. Broker Connect's job stops at 'produce a correct, complete extract' - the cat model run, PML calculation, and any hurricane-track overlay genuinely live outside this system's build scope, even though they consume this output. Matches the pre-existing out-of-scope note in scenarios.md ('Cat/exposure aggregation... treated as an external system this prototype would integrate with, not compute'). HAND-OFF NOTE: this is the hand-off into future Context 1d (External Threat) via the cat model.",
)

add_node(
    "READMODEL", "ExposureExtractHistory", "ExposureExtractGenerated-2", INTERACTION_ROW,
    [
        field("extractId", "UUID", idAttribute=True),
        field("requestedScope", "Custom"),
        field("asOfDate", "Date"),
        field("generatedAt", "DateTime"),
        field("recordCount", "Integer"),
        field("destination", "String"),
    ],
    "Audit trail of what was sent to cat modeling, when, and covering what scope - useful for reconciling 'what did the cat model actually see' if a PML figure is later questioned.",
)
add_node(
    "EVENT", "ExposureExtractGenerated", "ExposureExtractGenerated-2", SWIMLANE_ROW,
    [field("extractId", "UUID", idAttribute=True, generated=True), field("recordCount", "Integer")],
    "Second instance of the same event type, paired here with the audit-trail projection it feeds.",
)

print(f"Total node operations: {len(events)}")
status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print(status)
print(json.dumps(resp, indent=2)[:1500])

with open("/tmp/claude_node_ids2.json", "w") as f:
    json.dump(node_ids, f, indent=2)
