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

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "timeline-skill"}


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
        "name": name, "type": type_, "cardinality": opts.get("cardinality", "Single"),
        "optional": opts.get("optional", False), "idAttribute": opts.get("idAttribute", False),
        "generated": opts.get("generated", False), "edited": opts.get("edited", True),
        "query": opts.get("query", False), "showAttributes": opts.get("showAttributes", False),
        "technicalAttribute": opts.get("technicalAttribute", False), "subfields": opts.get("subfields", []),
    }


now = int(time.time() * 1000)
events = []

proposed_terms_fields = [field("lineSize", "Decimal"), field("pricing", "Decimal"), field("conditions", "String", optional=True)]

# --- Update existing nodes ---

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "bb31da46-5322-426a-8fbd-68b349aa80b1",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "COMMAND", "title": "AssessSubmission", "fields": [
        field("submissionId", "UUID"), field("underwriterId", "String"),
        field("proposedTerms", "Custom", subfields=proposed_terms_fields),
    ], "description": "S2.1-S2.4: checked against both gates before the event fires - Context 0 (underwriter's current authority per UnderwriterAuthorityRegister covers the proposed terms) and Context 1e (no active TerritoryUnderwritingFrozen covering this submission's territory/class)."},
    "node": {"id": "bb31da46-5322-426a-8fbd-68b349aa80b1", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "05ff8410-9830-4a21-a720-fa0db2153462",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "SubmissionWithinAuthority", "fields": [
        field("submissionId", "UUID", idAttribute=True), field("underwriterId", "String"),
        field("proposedTerms", "Custom", subfields=proposed_terms_fields),
        field("authorityVersionChecked", "Integer", description="References the specific UnderwriterAuthorityRegister version applied, not just 'checked' - so a later audit can see exactly what rule was in force."),
        field("freezeCheckPassed", "Boolean", description="Explicitly records that the 1e freeze check ran and passed, not just that the submission wasn't blocked - same completeness-of-audit-trail reasoning as S1d.4's zero-impact PMLRecalculated. Without this you can't later prove the check ran at all vs. not existing yet at the time."),
        field("decidedAt", "DateTime", generated=True),
    ], "description": "S2.1: submission moves from SubmissionQueue to a ready-to-quote state."},
    "node": {"id": "05ff8410-9830-4a21-a720-fa0db2153462", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "ed21346c-eacd-4915-ad4e-b4388af11be2",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "SubmissionReferred", "fields": [
        field("referralId", "UUID", idAttribute=True, generated=True),
        field("submissionId", "UUID"), field("underwriterId", "String"),
        field("proposedTerms", "Custom", subfields=proposed_terms_fields),
        field("breachedDimension", "String", example="line size | class | territory"),
        field("referredTo", "String", description="Determined by the authority cascade - the next tier for this underwriter/cell."),
        field("parentReferralId", "UUID", optional=True, description="S2.4: set when this referral is itself an escalation from a prior referral whose resolving tier still couldn't approve it - same event type, chained, forming a visible multi-tier escalation chain. Terminates at the cell's own overall limit (Context 0/S0.1's CellAuthorityLimitGranted) rather than escalating indefinitely."),
        field("referredAt", "DateTime", generated=True),
    ], "description": "S2.2-S2.4: reused for every tier of a multi-level escalation, distinguished by parentReferralId."},
    "node": {"id": "ed21346c-eacd-4915-ad4e-b4388af11be2", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "51a58eb7-ec11-4daa-8328-070b0fdf5a5f",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "COMMAND", "title": "DecideReferral", "fields": [
        field("referralId", "UUID"), field("resolvingPartyId", "String"),
        field("decision", "String", example="approve"), field("modifiedTerms", "Custom", optional=True, subfields=proposed_terms_fields),
    ], "description": "S2.2/S2.3: resolving party approves, declines, or (if their own authority still doesn't cover it) implicitly triggers a further SubmissionReferred escalation."},
    "node": {"id": "51a58eb7-ec11-4daa-8328-070b0fdf5a5f", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "11cac2ae-9603-437f-be3b-732e9c18bb2c",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "ReferralApproved", "fields": [
        field("referralId", "UUID", idAttribute=True), field("resolvingPartyId", "String"),
        field("approvedTerms", "Custom", subfields=proposed_terms_fields),
        field("termsModified", "Boolean", description="True if approvedTerms differ from originally proposed (negotiated down)."),
        field("decidedAt", "DateTime", generated=True),
    ], "description": "S2.2: if termsModified, requires the original underwriter's acknowledgment (see ReferralTermsAcknowledged) before proceeding to quoting - they hold the broker relationship, so the referee doesn't unilaterally finalize terms the underwriter never actually offered."},
    "node": {"id": "11cac2ae-9603-437f-be3b-732e9c18bb2c", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "1c845fe4-db81-4640-88e7-fcd44615938b",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "ReferralDeclined", "fields": [
        field("referralId", "UUID", idAttribute=True), field("resolvingPartyId", "String"),
        field("declineReason", "String"), field("decidedAt", "DateTime", generated=True),
    ], "description": "S2.3: dead end for these specific terms, by this specific referee, for this specific authority reason - distinct from SubmissionDeclined (off-appetite entirely). Loops back to a fresh AssessSubmission with revised terms, not a hard stop - same submissionId persists (a revised assessment of the same underlying risk, same pattern as PolicyEndorsed being a new ledger entry on the same policy rather than a new one)."},
    "node": {"id": "1c845fe4-db81-4640-88e7-fcd44615938b", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "8f532043-4c7a-43ad-a4fc-36ec9821424c",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "SubmissionDeclined", "fields": [
        field("submissionId", "UUID", idAttribute=True), field("underwriterId", "String"),
        field("declineReasonCode", "String", example="off-appetite | sanctioned-territory | excluded-class | other", description="Structured/categorized, not free text only - sanctioned-territory declines feed compliance reporting, not just an underwriting note."),
        field("declineReasonDetail", "String", optional=True),
        field("declinedAt", "DateTime", generated=True),
    ], "description": "S2.6: at any point in assessment, doesn't require a referral attempt first. Sanctioned-territory declines trigger ComplianceNotifiedOfSanctionsDecline - a mandatory downstream notification given the regulatory seriousness versus an ordinary off-appetite decline."},
    "node": {"id": "8f532043-4c7a-43ad-a4fc-36ec9821424c", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "a366fab2-19e7-4a63-a0af-450bbcb39120",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "QuoteIssued", "fields": [
        field("quoteId", "UUID", idAttribute=True, generated=True), field("submissionId", "UUID"),
        field("terms", "Custom", subfields=proposed_terms_fields),
        field("validityPeriodDays", "Integer"), field("expiryDate", "Date", generated=True),
        field("issuedBy", "String"), field("issuedAt", "DateTime", generated=True),
    ], "description": "S2.7: this is also the exact point S1e.2's freeze-block check applies - issuing a quote is the 'attempted action' intercepted if a freeze is active."},
    "node": {"id": "a366fab2-19e7-4a63-a0af-450bbcb39120", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "baf89a72-84b3-419d-add0-6997fbad084c",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields"],
    "meta": {"type": "EVENT", "title": "QuoteAccepted", "fields": [
        field("quoteId", "UUID", idAttribute=True), field("acceptedTerms", "Custom", subfields=proposed_terms_fields,
            description="Should match issued terms exactly - any variation is really a counter (see QuoteAmendmentRequested), not a plain acceptance."),
        field("brokerContact", "String"), field("acceptedAt", "DateTime", generated=True),
    ]},
    "node": {"id": "baf89a72-84b3-419d-add0-6997fbad084c", "data": {}}})

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print("update existing:", status, json.dumps(resp)[:300])

# --- New columns/nodes ---
col = {
    "ReferralTermsAcknowledged": "f5f84b1f-c146-4278-85b3-4b5cb608a557",
    "PendingReferralReassigned": "d349fc4f-4fa5-43c9-93e6-b408a1e6bdae",
    "PendingReferralHeld": "88ca9fd6-a001-4d76-92f0-5d2a24faad0b",
    "ComplianceNotifiedOfSanctionsDecline": "40a93fef-6113-4a67-a17e-bbda51f458d8",
    "QuoteExpired": "e448d980-dc85-496f-bcc1-774701980e52",
    "QuoteDeclined": "ada44738-c64a-4e9b-b072-34184831ec46",
    "QuoteAmendmentRequested": "793deecc-d9d8-4265-a9fc-e6fc9bc72627",
}
events2 = []


def cell(col_label, row_id):
    return f"{row_id}-{col[col_label]}"


def add_node(node_type, title, col_label, row_id, fields_list, description):
    node_id = str(uuid.uuid4())
    events2.append({
        "id": str(uuid.uuid4()), "eventType": "node:created", "nodeId": node_id, "boardId": BOARD_ID,
        "timestamp": now, "chapterId": CHAPTER_ID, "cellId": cell(col_label, row_id),
        "meta": {"type": node_type, "title": title, "fields": fields_list, "description": description},
        "node": {"id": node_id, "data": {"title": title}},
    })
    return node_id


add_node("COMMAND", "AcknowledgeReferralTerms", "ReferralTermsAcknowledged", INTERACTION_ROW,
    [field("referralId", "UUID"), field("submissionId", "UUID"), field("underwriterId", "String")],
    "S2.2: original underwriter acknowledges referee-modified terms before the submission proceeds to quoting.")
add_node("EVENT", "ReferralTermsAcknowledged", "ReferralTermsAcknowledged", SWIMLANE_ROW,
    [field("referralId", "UUID", idAttribute=True), field("underwriterId", "String"), field("acknowledgedAt", "DateTime", generated=True)],
    "Required whenever ReferralApproved.termsModified is true - the underwriter, not the referee, has final say on what actually gets offered to the broker.")

add_node("EVENT", "PendingReferralReassigned", "PendingReferralReassigned", SWIMLANE_ROW,
    [field("referralId", "UUID", idAttribute=True), field("previousResolvingParty", "String"), field("newResolvingParty", "String"),
     field("reassignedAt", "DateTime", generated=True)],
    "S2.5: fires when AuthorityLimitRevoked (or Revised) removes the pending referral's resolving party's ability to act on it - rerouted to whoever now holds equivalent authority, rather than sitting unresolved against someone who can no longer act. Produced by ReassessInFlightSubmissionsOnRuleChange (Context 0) - same mechanism, third trigger.")
add_node("EVENT", "PendingReferralHeld", "PendingReferralHeld", SWIMLANE_ROW,
    [field("referralId", "UUID", idAttribute=True), field("reason", "String"), field("heldAt", "DateTime", generated=True)],
    "S2.5: flagged for governance attention when no automatic reroute can be safely inferred (e.g. no clear equivalent-authority holder).")

add_node("EVENT", "ComplianceNotifiedOfSanctionsDecline", "ComplianceNotifiedOfSanctionsDecline", SWIMLANE_ROW,
    [field("submissionId", "UUID"), field("declineReasonDetail", "String"), field("notifiedAt", "DateTime", generated=True)],
    "S2.6: mandatory downstream notification specifically for declineReasonCode=sanctioned-territory, given the regulatory seriousness versus an ordinary off-appetite decline.")

add_node("AUTOMATION", "ExpireStaleQuotes", "QuoteExpired", ACTOR_ROW, [],
    "Scheduled/time-triggered policy, not a human command - runs when a quote's validity period elapses with no QuoteAccepted.")
add_node("EVENT", "QuoteExpired", "QuoteExpired", SWIMLANE_ROW,
    [field("quoteId", "UUID", idAttribute=True), field("terms", "Custom", subfields=proposed_terms_fields, description="Kept for record - useful if the broker later wants to revive similar terms."),
     field("expiredAt", "DateTime", generated=True)],
    "S2.8: submission moves to a closed/lapsed state distinct from decline - no one said no, it just wasn't taken up.")

add_node("COMMAND", "DeclineQuote", "QuoteDeclined", INTERACTION_ROW,
    [field("quoteId", "UUID"), field("declineReason", "String", optional=True, example="placed elsewhere | client withdrew")],
    "Broker-initiated, active decline rather than passive expiry.")
add_node("EVENT", "QuoteDeclined", "QuoteDeclined", SWIMLANE_ROW,
    [field("quoteId", "UUID", idAttribute=True), field("declineReason", "String", optional=True), field("declinedAt", "DateTime", generated=True)],
    "S2.9: kept distinct from QuoteExpired - an active decline (especially 'placed elsewhere') is genuinely useful competitive-intelligence data that passive expiry isn't.")

add_node("COMMAND", "RequestQuoteAmendment", "QuoteAmendmentRequested", INTERACTION_ROW,
    [field("quoteId", "UUID"), field("requestedChanges", "Custom", subfields=proposed_terms_fields), field("brokerContact", "String")],
    "Broker-initiated.")
add_node("EVENT", "QuoteAmendmentRequested", "QuoteAmendmentRequested", SWIMLANE_ROW,
    [field("quoteId", "UUID"), field("submissionId", "UUID"), field("requestedChanges", "Custom", subfields=proposed_terms_fields),
     field("brokerContact", "String"), field("requestedAt", "DateTime", generated=True)],
    "S2.10: triggers a fresh AssessSubmission pass against the amended terms - not a lightweight negotiation sub-flow. Amended terms could push the submission outside the underwriter's authority (e.g. higher line size requested), so it must go through the same Context 0/1e gate checks as any other assessment; a casual in-context negotiation would silently bypass both gates. Produces a fresh SubmissionWithinAuthority or SubmissionReferred, chained back to this event as prior context.")

print(f"Total new node operations: {len(events2)}")
status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events2)
print(status)
print(json.dumps(resp, indent=2)[:1000])
