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

terms_fields = [field("lineSize", "Decimal"), field("premium", "Decimal"), field("effectiveDate", "Date"), field("expiryDate", "Date")]
allocation_fields = [field("providerId", "String"), field("quotaSharePercent", "Decimal")]

# --- Update existing nodes ---

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "e128a447-2005-482c-898e-6cf60ebe8c2e",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "COMMAND", "title": "BindPolicy", "fields": [
        field("quoteId", "UUID"), field("submissionId", "UUID"),
        field("finalTerms", "Custom", subfields=terms_fields),
        field("capacityProviderAllocation", "Custom", cardinality="List", subfields=allocation_fields,
            description="Always modeled as a list, even at a single provider (100% to one) - keeps S3.1 and S3.2 the same shape. Must sum to exactly 100% - hard validation rule, not trusted at entry."),
    ], "description": "S3.1/S3.2: requires explicit underwriter confirmation, not fired automatically on QuoteAccepted - broker acceptance and the underwriter's final bind action are conceptually different moments (documentation checks, subjectivities being cleared)."},
    "node": {"id": "e128a447-2005-482c-898e-6cf60ebe8c2e", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "546c0a0d-c814-4324-9718-e45e7bb9ac6a",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "PolicyBound", "fields": [
        field("policyId", "UUID", idAttribute=True, generated=True),
        field("submissionId", "UUID"), field("quoteId", "UUID"),
        field("cellId", "String"), field("classOfBusiness", "String"), field("territory", "String"), field("namedInsured", "String"),
        field("finalTerms", "Custom", subfields=terms_fields),
        field("capacityProviderAllocation", "Custom", cardinality="List", subfields=allocation_fields),
        field("providerAuthorityLineage", "Custom", cardinality="List", optional=True,
            description="OPEN QUESTION (unresolved, linked to S0.1): should reference which specific Context 0 authority grant justified writing on behalf of each provider, if a cell can hold per-provider authority grants. Depends on S0.1's still-open multi-provider-per-cell question - not resolved independently here."),
        field("boundBy", "String"), field("boundAt", "DateTime", generated=True),
    ], "description": "S3.1/S3.2: the immutable anchor for the policy's lifecycle - once fired, endorsements/cancellations append to history rather than rewriting this record. Everything downstream (Bordereaux in Context 4, Claims in Context 5) keys off it."},
    "node": {"id": "546c0a0d-c814-4324-9718-e45e7bb9ac6a", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "ec9f25e5-21d7-4a4c-8234-6c8ef2a45b9c",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "COMMAND", "title": "EndorsePolicy", "fields": [
        field("policyId", "UUID"), field("underwriterId", "String"),
        field("requestedChange", "Custom", subfields=[
            field("description", "String"),
            field("deltaType", "String", example="premium | limit | exposure | administrative",
                description="Explicit, rule-based classification - not left to underwriter judgment case by case, since inconsistent judgment calls are exactly what an audit would flag. premium/limit/exposure changes are material; purely administrative detail (spelling correction, contact update) is not."),
        ]),
    ], "description": "S3.3: broker-initiated change request, processed by the underwriter."},
    "node": {"id": "ec9f25e5-21d7-4a4c-8234-6c8ef2a45b9c", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "7f24ef51-f3d1-4207-9446-25ab1c63b53b",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "PolicyEndorsed", "fields": [
        field("policyId", "UUID", idAttribute=True), field("endorsementId", "UUID", generated=True),
        field("changeDelta", "Custom", description="A delta from current terms, not a full restatement - keeps the ledger honest about what actually changed."),
        field("materialityClassification", "String", example="material"),
        field("effectiveDate", "Date"), field("endorsedBy", "String"), field("endorsedAt", "DateTime", generated=True),
    ], "description": "S3.3: fires for administrative changes directly. Material changes (premium/limit/exposure) instead produce EndorsementReferred if they exceed the underwriter's authority - resolves the open thread from S1c.3: an endorsement that increases exposure now goes through the same authority/referral check as an original bind, reusing Context 2's existing mechanism rather than a lightweight edit path that quietly bypasses governance."},
    "node": {"id": "7f24ef51-f3d1-4207-9446-25ab1c63b53b", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "1a8071bb-714e-4b3a-bddd-b07c09f21d33",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "COMMAND", "title": "CancelPolicy", "fields": [
        field("policyId", "UUID"), field("cancellationReason", "String"), field("effectiveDate", "Date"),
        field("initiatedBy", "String", example="broker-request | underwriter-for-cause",
            description="Meaningfully different scenarios - underwriter-for-cause (e.g. material misrepresentation discovered post-bind) is more sensitive than a routine broker request."),
    ], "description": "S3.4"},
    "node": {"id": "1a8071bb-714e-4b3a-bddd-b07c09f21d33", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "d9f92200-8c84-4328-a6ba-8c806092f839",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "PolicyCancelled", "fields": [
        field("policyId", "UUID", idAttribute=True), field("reason", "String"), field("effectiveDate", "Date"),
        field("initiatedBy", "String"), field("returnPremiumBasis", "String", example="pro-rata | short-rate"),
        field("cancelledAt", "DateTime", generated=True),
    ], "description": "S3.4: routine broker-requested or underwriter-initiated cancellation. Triggers the exposure reduction already modeled in S1c.4 - return premium calculation itself feeds Bordereaux/Settlement (Context 4), not computed here. For-cause cancellations use the separate PolicyCancelledForCause event instead, given the compliance/dispute implications of material misrepresentation."},
    "node": {"id": "d9f92200-8c84-4328-a6ba-8c806092f839", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "68d1e9a3-d326-4a6e-b993-39d0a61085c3",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.title", "meta.fields", "meta.description"],
    "meta": {"type": "COMMAND", "title": "InitiateRenewal", "fields": [
        field("expiringPolicyId", "UUID"), field("proposedRenewalTerms", "Custom", subfields=terms_fields, optional=True),
    ], "description": "S3.5: broker-initiated or system-prompted ahead of expiry. Renamed from RenewPolicy for clarity that this only initiates the loop, it doesn't itself bind anything."},
    "node": {"id": "68d1e9a3-d326-4a6e-b993-39d0a61085c3", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "ef2ae481-db8c-4515-a1af-276d21c77d5e",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.title", "meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "PolicyRenewalInitiated", "fields": [
        field("expiringPolicyId", "UUID", idAttribute=True), field("newSubmissionId", "UUID", generated=True,
            description="A genuinely new submission re-entering Context 1a/1b, not a special shortcut path."),
        field("proposedRenewalTerms", "Custom", subfields=terms_fields, optional=True),
        field("unconditionalReassessment", "Boolean", generated=True, example="true",
            description="Always true - expiring terms don't grandfather anything, since risk appetite, authority, and even the underwriter may have changed since original bind. No 'renewal fast-track' shortcut, resolved explicitly rather than left to default."),
        field("initiatedAt", "DateTime", generated=True),
    ], "description": "S3.5: 'loops back into a fresh submission/bind cycle, not shown as a literal loop on this timeline' - genuinely re-enters Context 1a/2 rather than a special renewal path. OPEN QUESTIONS (unresolved): does the new submission carry forward exposure/PML context from the expiring policy automatically, given Context 1c/1d/1e's exploratory status - flagged for later, but the linkage should exist even before those contexts are built. What happens if renewal is initiated but the broker doesn't respond before original expiry - lapse vs. grace/held-covered period is a real contractual question for underwriting stakeholders, not an architecture default."},
    "node": {"id": "ef2ae481-db8c-4515-a1af-276d21c77d5e", "data": {}}})

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print("update existing:", status, json.dumps(resp)[:300])

# --- New columns/nodes ---
col = {
    "PolicyRegister": "2738387b-d2f1-412f-a2e8-2d0558ee9928",
    "ActiveBookOfBusiness": "757c2001-1390-4b7c-9631-36a94ebea8de",
    "EndorsementReferred": "1ccd7800-b431-4df8-b110-ff786ec90f12",
    "PolicyCancelledForCause": "c0507208-12de-474a-83ca-07969007b218",
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


add_node("EVENT", "PolicyBound", "PolicyRegister", SWIMLANE_ROW,
    [field("policyId", "UUID", idAttribute=True, generated=True)],
    "Second instance of the same event type, paired here with the register projection it feeds.")
add_node("READMODEL", "PolicyRegister", "PolicyRegister", INTERACTION_ROW,
    [
        field("policyId", "UUID", idAttribute=True), field("cellId", "String"), field("classOfBusiness", "String"),
        field("namedInsured", "String"), field("currentTerms", "Custom", subfields=terms_fields),
        field("capacityProviderAllocation", "Custom", cardinality="List", subfields=allocation_fields),
        field("status", "String", example="Bound"), field("endorsementCount", "Integer"),
    ],
    "S3.1: the canonical current-state view of every bound policy.")

add_node("EVENT", "PolicyBound", "ActiveBookOfBusiness", SWIMLANE_ROW,
    [field("policyId", "UUID", idAttribute=True, generated=True)],
    "Third instance of the same event type, paired here with the per-cell book-of-business projection it feeds.")
add_node("READMODEL", "ActiveBookOfBusiness", "ActiveBookOfBusiness", INTERACTION_ROW,
    [
        field("cellId", "String", idAttribute=True), field("policyCount", "Integer"),
        field("totalPremium", "Decimal"), field("totalLineSize", "Decimal"),
        field("policies", "Custom", cardinality="List", subfields=[field("policyId", "UUID"), field("namedInsured", "String"), field("premium", "Decimal")]),
    ],
    "S3.1: per-cell view of currently active bound business.")

add_node("EVENT", "EndorsementReferred", "EndorsementReferred", SWIMLANE_ROW,
    [
        field("endorsementId", "UUID"), field("policyId", "UUID"), field("underwriterId", "String"),
        field("changeDelta", "Custom"), field("breachedDimension", "String", example="limit"),
        field("referredTo", "String"), field("referredAt", "DateTime", generated=True),
    ],
    "S3.3: fires instead of PolicyEndorsed when a material change (premium/limit/exposure) exceeds the underwriter's authority - reuses Context 2's DecideReferral/ReferralApproved/ReferralDeclined resolution path rather than inventing a parallel one. Concrete example of the event model paying off architecturally: three contexts (Decisioning, Binding, and Context 0/1e's rule-change reassessment) now share one escalation mechanism instead of three bespoke ones.")

add_node("EVENT", "PolicyCancelledForCause", "PolicyCancelledForCause", SWIMLANE_ROW,
    [
        field("policyId", "UUID", idAttribute=True), field("cause", "String", example="material misrepresentation"),
        field("evidenceReference", "String", optional=True), field("effectiveDate", "Date"),
        field("initiatedBy", "String"), field("cancelledAt", "DateTime", generated=True),
    ],
    "S3.4: distinct from routine PolicyCancelled given the compliance/dispute implications of a for-cause cancellation (e.g. material misrepresentation discovered post-bind) - not just a different reason code on the same event.")

print(f"Total new node operations: {len(events2)}")
status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events2)
print(status)
print(json.dumps(resp, indent=2)[:1000])
