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
allocation_fields = [field("providerId", "String"), field("amount", "Decimal")]

# --- Update existing nodes ---

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "3fe5fc04-cdd1-4ce0-8c74-b09b805ed883",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields"],
    "meta": {"type": "COMMAND", "title": "NotifyClaim", "fields": [
        field("policyReference", "UUID"), field("lossDescription", "String"),
        field("dateOfLoss", "Date"), field("notifyingParty", "String"),
    ]},
    "node": {"id": "3fe5fc04-cdd1-4ce0-8c74-b09b805ed883", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "c7963647-9a00-4f44-a7b7-4ae505da7953",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "ClaimNotified", "fields": [
        field("claimId", "UUID", idAttribute=True, generated=True), field("policyReference", "UUID"),
        field("dateOfLoss", "Date"), field("dateNotified", "DateTime", generated=True),
        field("notifyingParty", "String"), field("initialLossDescription", "String"),
    ], "description": "S5.1: notification may originate from broker/insured, but processing is the claims team's. References the original bind (and transitively the whole submission/decisioning lineage) via policyReference - a claims handler pulling up a claim gets the full PolicyOriginationView designed back in Search & Retrieval (1b.2)."},
    "node": {"id": "c7963647-9a00-4f44-a7b7-4ae505da7953", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "8c9976d0-c213-4508-b808-2baea5f6b91b",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields"],
    "meta": {"type": "COMMAND", "title": "SetClaimReserve", "fields": [
        field("claimId", "UUID"), field("reserveAmount", "Decimal"), field("basisRationale", "String"),
    ]},
    "node": {"id": "8c9976d0-c213-4508-b808-2baea5f6b91b", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "5bcaff91-7b84-4691-8915-3630581a5e15",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "ClaimReserveSet", "fields": [
        field("claimId", "UUID", idAttribute=True), field("reserveAmount", "Decimal"),
        field("setBy", "String"), field("basis", "String"),
        field("sequenceNumber", "Integer", generated=True, description="The claim's reserve history is a full sequence, not a single mutable field - same event type, new instance each time."),
        field("setAt", "DateTime", generated=True),
    ], "description": "S5.2: explicitly recurring - fires again whenever new information changes the loss estimate. A material increase crossing the claims handler's own authority threshold instead produces ReserveRevisionReferred - the authority/governance pattern from Context 0 isn't scoped only to underwriting decisioning, it applies here too."},
    "node": {"id": "5bcaff91-7b84-4691-8915-3630581a5e15", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "56919635-92c5-46ce-a806-3d70c7afa569",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "COMMAND", "title": "PayClaim", "fields": [
        field("claimId", "UUID"), field("paymentAmount", "Decimal"),
        field("providerAllocation", "Custom", cardinality="List", subfields=allocation_fields,
            description="Auto-derived from the provider split recorded in the original PolicyBound event (S3.2), not re-entered manually - same allocation model reused across Bind, Bordereaux, and Claims."),
    ], "description": "S5.1: validates the payment against the policy's limits/sub-limits/deductibles from the original bind terms - an explicit, visible step referencing PolicyOriginationView/bind terms directly, not just trusted to the claims handler's manual check. This is precisely the kind of governance point this system exists to make auditable rather than assumed."},
    "node": {"id": "56919635-92c5-46ce-a806-3d70c7afa569", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "2bd701c5-010d-446a-aeb0-fb9af23fbfb0",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "ClaimPaid", "fields": [
        field("claimId", "UUID", idAttribute=True), field("paymentAmount", "Decimal"),
        field("providerAllocation", "Custom", cardinality="List", subfields=allocation_fields),
        field("paymentDate", "Date"), field("paymentReference", "String"),
        field("validatedAgainstBindTerms", "Boolean", description="Explicit visible validation step against the original PolicyBound terms."),
    ], "description": "S5.3: allocation mirrors the bind-time quota share automatically. OPEN QUESTION (unresolved, parallel to S0.1's per-provider question): could quota share change post-bind (novation, reinsurance restructure)? If so, this needs to reference whichever allocation was in effect at the relevant point, not blindly inherit the original split - would need point-in-time versioning like Context 0's authority model, not a simple lookup. Not resolved here."},
    "node": {"id": "2bd701c5-010d-446a-aeb0-fb9af23fbfb0", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "2bda7ab4-1c88-4e05-89a5-20620abfda11",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields"],
    "meta": {"type": "EVENT", "title": "ClaimClosed", "fields": [
        field("claimId", "UUID", idAttribute=True), field("closureReason", "String"),
        field("finalTotalPaid", "Decimal"), field("closedBy", "String"), field("closedAt", "DateTime", generated=True),
    ]},
    "node": {"id": "2bda7ab4-1c88-4e05-89a5-20620abfda11", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "ab3a7bb8-8867-4dd4-acf7-2b603ef8cee9",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "ClaimReopened", "fields": [
        field("claimId", "UUID", idAttribute=True), field("reopenReason", "String"),
        field("newInformation", "String"), field("reopenedBy", "String"), field("reopenedAt", "DateTime", generated=True),
    ], "description": "S5.4: appended after ClaimClosed, doesn't erase it - the same append-only 'corrections are new entries, not edits' principle used for Bordereaux (AdjustmentLineRaised) and Binding (PolicyEndorsed) applies here too, one architectural principle consistently applied rather than three separate decisions. Typically followed by a fresh ClaimReserveSet cycle. Reopen-limit/repeated-reopening-as-a-governance-signal not modeled yet - flagged as a natural extension, not essential day one."},
    "node": {"id": "ab3a7bb8-8867-4dd4-acf7-2b603ef8cee9", "data": {}}})

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print("update existing:", status, json.dumps(resp)[:300])

# --- New columns/nodes ---
col = {
    "ClaimRegister": "f11c9e0f-07be-4ac1-8b5c-56ca84a291a6",
    "ReserveRevisionReferred": "c7338586-e6b7-4442-a37b-7e66fa234149",
    "ClaimNotifiedAgainstInactivePolicy": "3deeb6d0-027d-4c5b-abd3-9bf17bda3e6d",
    "ClaimPeriodValidated": "bc506136-6adb-46b7-9cc8-763780359737",
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


add_node("EVENT", "ClaimNotified", "ClaimRegister", SWIMLANE_ROW,
    [field("claimId", "UUID", idAttribute=True, generated=True)],
    "Second instance of the same event type, paired here with the register projection it feeds.")
add_node("READMODEL", "ClaimRegister", "ClaimRegister", INTERACTION_ROW,
    [
        field("claimId", "UUID", idAttribute=True), field("policyReference", "UUID"),
        field("status", "String", example="Open"), field("currentReserve", "Decimal"),
        field("totalPaid", "Decimal"), field("dateOfLoss", "Date"), field("dateNotified", "DateTime"),
        field("reopenCount", "Integer"),
    ],
    "S5.1: the claim's permanent record, canonical current state.")

add_node("EVENT", "ReserveRevisionReferred", "ReserveRevisionReferred", SWIMLANE_ROW,
    [
        field("claimId", "UUID"), field("claimsHandlerId", "String"),
        field("requestedReserveAmount", "Decimal"), field("currentAuthorityLimit", "Decimal"),
        field("referredTo", "String"), field("referredAt", "DateTime", generated=True),
    ],
    "S5.2: a material reserve increase crossing the claims handler's own authority threshold - genuine parallel to S3.3's endorsement/referral resolution. Suggests Context 0's authority engine is a cross-cutting capability (decisioning, endorsements, AND claims reserve authority), not something scoped only to Context 2 - worth reframing Context 0 as a general authority/governance engine rather than 'underwriting authority' specifically. Reuses the same DecideReferral resolution path as Context 2/Context 3's EndorsementReferred.")

add_node("EVENT", "ClaimNotifiedAgainstInactivePolicy", "ClaimNotifiedAgainstInactivePolicy", SWIMLANE_ROW,
    [
        field("claimId", "UUID"), field("policyReference", "UUID"),
        field("policyStatusAtNotification", "String", example="Cancelled"),
        field("dateOfLoss", "Date", description="The key question is whether this falls within the period the policy was actually active, regardless of its current status."),
        field("flaggedAt", "DateTime", generated=True),
    ],
    "S5.5: fires when NotifyClaim references a policy whose current PolicyRegister state shows Cancelled or lapsed/non-renewed. Does NOT reject the notification - the loss may have occurred while cover was active. A flag, not a block.")
add_node("COMMAND", "ValidateClaimPeriod", "ClaimPeriodValidated", INTERACTION_ROW,
    [field("claimId", "UUID"), field("determination", "String", example="within period of cover")],
    "Explicit human confirmation, not an automated pass/fail - date-of-loss-vs-active-period disputes (was the policy actually in force at the exact moment of loss, retroactive date issues) are exactly the kind of determination that ends up in coverage disputes and shouldn't be silently auto-decided.")
add_node("EVENT", "ClaimPeriodValidated", "ClaimPeriodValidated", SWIMLANE_ROW,
    [field("claimId", "UUID", idAttribute=True), field("determination", "String"), field("validatedBy", "String"), field("validatedAt", "DateTime", generated=True)],
    "S5.5: only required when ClaimNotifiedAgainstInactivePolicy has fired - a claims handler manually confirms the loss falls within the period on cover before reserve/payment proceeds.")

print(f"Total new node operations: {len(events2)}")
status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events2)
print(status)
print(json.dumps(resp, indent=2)[:1000])
