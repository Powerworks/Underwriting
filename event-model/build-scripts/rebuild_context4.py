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

# --- Update existing nodes ---

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "b66eb41f-58c6-46af-8b1a-4cc178a9e597",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.description"],
    "meta": {"type": "EVENT", "title": "BordereauDrafted", "description": "S4.1: sweeps all unbilled transactions (binds, endorsements, cancellations) for a cell+provider+period. Bordereaux are strictly scoped to one cell+provider+period each (S4.5) - a multi-provider bind (S3.2) contributes its allocated share independently to each provider's own bordereau, using the allocation data already captured in PolicyBound/PolicyEndorsed. Matches how capacity providers actually expect to be settled with (their own statement, not a shared one)."},
    "node": {"id": "b66eb41f-58c6-46af-8b1a-4cc178a9e597", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "abf534a9-e8bc-4ed9-8e4a-ebe1dd0613d2",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "BordereauAgreed", "fields": [
        field("bordereauId", "UUID", idAttribute=True),
        field("agreedByTFP", "String"), field("agreedByProvider", "String", description="Provider's own confirmation, not just TFP's internal view - bordereaux are the literal mechanism by which money moves to/from providers, so this needs a genuine two-party sign-off."),
        field("finalAgreedTotal", "Decimal"), field("agreedAt", "DateTime", generated=True),
    ], "description": "S4.1/S4.2: only fires once BordereauSubmittedForAgreement has been confirmed by the provider AND zero queries remain open on the bordereau - by construction, since any query still open at period-close is resolved via BordereauLineResolved or excluded via BordereauLineDeferred before this point. No bordereau is ever agreed with a genuinely unresolved query still attached."},
    "node": {"id": "abf534a9-e8bc-4ed9-8e4a-ebe1dd0613d2", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "f9c72f16-950b-4616-8744-94916a62ad83",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "AdjustmentLineRaised", "fields": [
        field("adjustmentId", "UUID", idAttribute=True, generated=True),
        field("originalBordereauId", "UUID"), field("originalLineReference", "String"),
        field("correctionAmount", "Decimal"), field("reason", "String"), field("raisedBy", "String"),
        field("targetSettlementPeriod", "String", description="Always future, never retroactive into the closed bordereau."),
        field("requiresSeparateAgreement", "Boolean", description="Defaults false - rides along within the next period's normal BordereauDrafted/BordereauAgreed cycle as a line item, rather than its own mini agreement cycle, unless the adjustment exceeds a materiality threshold."),
        field("raisedAt", "DateTime", generated=True),
    ], "description": "S4.4: the correction flows into next period's BordereauDrafted sweep as a new line - the original historical bordereau remains untouched, consistent with the immutable-ledger principle running through the whole model."},
    "node": {"id": "f9c72f16-950b-4616-8744-94916a62ad83", "data": {}}})

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print("update existing:", status, json.dumps(resp)[:300])

# --- New columns/nodes ---
col = {
    "BordereauSubmittedForAgreement": "23dee479-f6a6-4664-961d-8cd6aac0c50e",
    "UnbilledTransactionsForPeriod": "70861973-fbd5-4e81-bf25-4b1442880920",
    "BordereauLineDeferred": "f11fdaf2-c897-4d56-8120-c18f00d625e0",
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


add_node("COMMAND", "SubmitBordereauForAgreement", "BordereauSubmittedForAgreement", INTERACTION_ROW,
    [field("bordereauId", "UUID")],
    "TFP finance/ops signals internal readiness - distinct from the provider's own confirmation (BordereauAgreed).")
add_node("EVENT", "BordereauSubmittedForAgreement", "BordereauSubmittedForAgreement", SWIMLANE_ROW,
    [field("bordereauId", "UUID", idAttribute=True), field("submittedBy", "String"), field("submittedAt", "DateTime", generated=True)],
    "S4.1: two-party agreement modeled as two events (BordereauSubmittedForAgreement -> BordereauAgreed) rather than one - TFP's internal sign-off is not the same fact as the provider's actual confirmation, and bordereaux are literally how money moves.")

add_node("EVENT", "BordereauDrafted", "UnbilledTransactionsForPeriod", SWIMLANE_ROW,
    [field("bordereauId", "UUID", idAttribute=True, generated=True)],
    "Second instance of the same event type, paired here with the read model that determines what's swept into it.")
add_node("READMODEL", "UnbilledTransactionsForPeriod", "UnbilledTransactionsForPeriod", INTERACTION_ROW,
    [
        field("cellId", "String"), field("providerId", "String"), field("period", "String"),
        field("transactions", "Custom", cardinality="List", subfields=[
            field("transactionReference", "UUID"), field("transactionType", "String", example="bind | endorsement | cancellation"),
            field("premiumAmount", "Decimal"), field("includedInBordereauId", "UUID", optional=True, description="Null/absent means not yet swept - each transaction links to at most one bordereau."),
        ]),
    ],
    "S4.1: explicit link from each transaction to at most one bordereau (or none) - answers 'how does the system know a transaction hasn't already been included' as a queryable read model concern rather than an assumed fact.")

add_node("COMMAND", "RaiseBordereauQuery", "BordereauLineDeferred", INTERACTION_ROW,
    [field("bordereauId", "UUID"), field("lineReference", "String"), field("queryReason", "String")],
    "Relocated here from its original column, where it had never actually landed on the grid due to the command/readmodel same-cell collision (BordereauDetail took the interaction cell there).")
add_node("AUTOMATION", "DeferQueriedLineAtPeriodClose", "BordereauLineDeferred", ACTOR_ROW, [],
    "Trigger: a BordereauLineQueried remains open as the period's normal processing timeline elapses.")
add_node("EVENT", "BordereauLineDeferred", "BordereauLineDeferred", SWIMLANE_ROW,
    [field("bordereauId", "UUID"), field("lineReference", "String"), field("deferredToBordereauId", "UUID", optional=True), field("reason", "String"), field("deferredAt", "DateTime", generated=True)],
    "S4.3: recommended default over blocking the whole bordereau - a single disputed line shouldn't hold up cash movement on everything else in the period. Mirrors the AdjustmentLineRaised pattern (S4.4) already established: a line rolls into a future period's draft, never editing history. This is what makes BordereauAgreed's 'zero open queries' rule always achievable by period-close - open queries are always either resolved or deferred before agreement, never left dangling.")

print(f"Total new node operations: {len(events2)}")
status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events2)
print(status)
print(json.dumps(resp, indent=2)[:1000])
