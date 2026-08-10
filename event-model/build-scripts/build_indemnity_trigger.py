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
ACTOR_ROW = "1af53c6a-6c34-4cd3-987c-7ac085fe0cf0"

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "indemnity-trigger"}


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

# --- Update CatBondRegistered command + event: constrain triggerType ---
events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "197dbffd-3eea-4076-83e9-4720b1ee55f0",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields"],
    "meta": {"type": "COMMAND", "title": "RegisterCatBond", "fields": [
        field("bondName", "String", example="Woody Re"),
        field("coveredSyndicate", "String", example="Lloyd's Syndicate 3123"),
        field("capacity", "Decimal", example="75000000"),
        field("triggerType", "String", example="index", description="Constrained to 'indemnity' (relies on TFP's own actual company losses, requires auditing before it can fire) or 'index' (third-party industry-wide loss calculations, enables rapid liquidity)."),
        field("attachmentPoint", "Decimal", example="78000000000"),
        field("coveredPerils", "String", cardinality="List", example="North American storms, earthquakes, wildfires"),
        field("interestRate", "Decimal", optional=True, example="8.25"),
        field("termStart", "Date"), field("termEnd", "Date"),
    ]},
    "node": {"id": "197dbffd-3eea-4076-83e9-4720b1ee55f0", "data": {}}})

events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "8f7a478d-b0c7-423e-958d-a265b92c4c72",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "CatBondRegistered", "fields": [
        field("bondId", "UUID", idAttribute=True, generated=True),
        field("bondName", "String"), field("coveredSyndicate", "String"), field("capacity", "Decimal"),
        field("triggerType", "String", example="index", description="'indemnity' or 'index' - determines which downstream trigger-confirmation path applies (IndemnityLossesAudited vs CatBondAttachmentDistanceUpdated)."),
        field("attachmentPoint", "Decimal"),
        field("coveredPerils", "String", cardinality="List"),
        field("interestRate", "Decimal", optional=True), field("termStart", "Date"), field("termEnd", "Date"),
        field("registeredAt", "DateTime", generated=True),
    ], "description": "S1f.1: reference/capital data, same discipline as Context 0's authority grants. triggerType determines which trigger-confirmation mechanism this bond uses downstream - index bonds track an external feed (S1f.2), indemnity bonds require an internal audit (S1f.5)."},
    "node": {"id": "8f7a478d-b0c7-423e-958d-a265b92c4c72", "data": {}}})

# --- Update CatBondTriggered: two-path resolution ---
events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "e46d2181-94f6-4748-9da8-875930b36788",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.description"],
    "meta": {"type": "EVENT", "title": "CatBondTriggered", "description": "S1f.4: the moment losses cross the attachment/trigger point and investor capital is legally forfeited, paid to the syndicate to cover claims. RESOLVED: two legitimate upstream paths depending on CatBondRegistered.triggerType - CatBondAttachmentDistanceUpdated (S1f.2) crossing zero for index bonds (third-party data, enables rapid liquidity, closer to self-executing), or IndemnityLossesAudited (S1f.5) crossing the threshold for indemnity bonds (TFP's own losses, always audit-gated, never self-executing). Same event, same 'don't silently auto-decide a money-moving event' discipline as ClaimPeriodValidated (S5.5) - the index path is faster but still not literally automatic without the underlying feed's own reliability guarantees."},
    "node": {"id": "e46d2181-94f6-4748-9da8-875930b36788", "data": {}}})

# --- Update ReceiveBrokerSubmission: tighten ACORD description ---
events.append({"id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "3d4038e6-e7d7-49e4-9361-81f8f9c2df0d",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.description"],
    "meta": {"type": "COMMAND", "title": "ReceiveBrokerSubmission", "description": "Renamed from SubmitBrokerSubmission - actor is the broker's system or Broker Connect's own ingestion service acting on the broker's behalf, not necessarily a direct user action. rawPayload is mapped to the ACORD standard schema (ADEPT) specifically - this is what enables automated data exchange across global broker systems generally, not a Howden-specific format, eliminating manual translation/mapping into internal databases."},
    "node": {"id": "3d4038e6-e7d7-49e4-9361-81f8f9c2df0d", "data": {}}})

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print("updates:", status, json.dumps(resp)[:300])

# --- New: IndemnityLossesAudited + AuditIndemnityLossesAgainstTrigger ---
col_id = "9bd12843-c62b-436e-a65b-5f82c95d2d53"
events2 = []


def add_node(node_type, title, row_id, fields_list, description):
    node_id = str(uuid.uuid4())
    events2.append({
        "id": str(uuid.uuid4()), "eventType": "node:created", "nodeId": node_id, "boardId": BOARD_ID,
        "timestamp": now, "chapterId": CHAPTER_ID, "cellId": f"{row_id}-{col_id}",
        "meta": {"type": node_type, "title": title, "fields": fields_list, "description": description},
        "node": {"id": node_id, "data": {"title": title}},
    })
    return node_id


add_node("EVENT", "IndemnityLossesAudited", SWIMLANE_ROW,
    [
        field("bondId", "UUID"),
        field("auditedLossTotal", "Decimal", description="Pulled from TFP's own actual incurred losses (ClaimPaid/ClaimReserveSet, Context 5) on policies within the bond's covered perils/syndicate - not the external industry loss index."),
        field("triggerThreshold", "Decimal"),
        field("distanceToTrigger", "Decimal"),
        field("auditedBy", "String"),
        field("auditedAt", "DateTime", generated=True),
    ],
    "S1f.5: for indemnity-triggered bonds. Requires auditing means this is never self-executing the way index-crossing can be - a human/audit-function confirmation step, same discipline as ClaimPeriodValidated (S5.5). Periodic or loss-event-driven, scoped to policies/claims within the bond's covered perils/syndicate.")
add_node("AUTOMATION", "AuditIndemnityLossesAgainstTrigger", ACTOR_ROW, [],
    "Pulls actual incurred losses from ClaimPaid/ClaimReserveSet (Context 5), not IndustryLossIndexUpdated (S1f.2) - a structurally different data source than the index-trigger path.")

status2, resp2 = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events2)
print("new nodes:", status2, json.dumps(resp2)[:400])
