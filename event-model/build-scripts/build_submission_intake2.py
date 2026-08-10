import json
import time
import urllib.request
import uuid

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"
CHAPTER_ID = "e96f1046-4ab4-4654-89f3-8074c0aa06f4"

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


with open("/tmp/claude_column_ids.json") as f:
    column_ids = json.load(f)

# ---------------------------------------------------------------------------
# Step 2: re-fetch chapter, build colId -> {swimlane cellId, interaction cellId}
# ---------------------------------------------------------------------------
status, chap = call("GET", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/{CHAPTER_ID}")
assert status == 200, (status, chap)
td = chap["meta"]["timelineData"]
swimlane_row_id = [r["id"] for r in td["rows"] if r["type"] == "swimlane"][0]
interaction_row_id = [r["id"] for r in td["rows"] if r["type"] == "interaction"][0]

# The raw "add column" endpoint does NOT create cell records for the new column
# (confirmed empirically - column count went 28->35 but cell count stayed at 112 = 4*28).
# However cellId follows a deterministic "{rowId}-{colId}" pattern that the node-create
# endpoint accepts even when no literal cell record exists yet - confirmed by probe test.
# So we synthesize cellIds directly rather than looking them up.
cell_lookup = {}  # (colId, rowType) -> cellId
for col in td["columns"]:
    cell_lookup[(col["id"], "swimlane")] = f"{swimlane_row_id}-{col['id']}"
    cell_lookup[(col["id"], "interaction")] = f"{interaction_row_id}-{col['id']}"

# ---------------------------------------------------------------------------
# Step 3: build the batch node-change payload
# ---------------------------------------------------------------------------
now = int(time.time() * 1000)
events = []


def new_event_node(label, fields_list, description):
    node_id = str(uuid.uuid4())
    col_id = column_ids[label]
    cell_id = cell_lookup[(col_id, "swimlane")]
    events.append({
        "id": str(uuid.uuid4()),
        "eventType": "node:created",
        "nodeId": node_id,
        "boardId": BOARD_ID,
        "timestamp": now,
        "chapterId": CHAPTER_ID,
        "cellId": cell_id,
        "meta": {"type": "EVENT", "title": label, "fields": fields_list, "description": description},
        "node": {"id": node_id, "data": {"title": label}},
    })
    return node_id


def new_readmodel_node(title, on_label, fields_list, description):
    node_id = str(uuid.uuid4())
    col_id = column_ids[on_label]
    cell_id = cell_lookup[(col_id, "interaction")]
    events.append({
        "id": str(uuid.uuid4()),
        "eventType": "node:created",
        "nodeId": node_id,
        "boardId": BOARD_ID,
        "timestamp": now,
        "chapterId": CHAPTER_ID,
        "cellId": cell_id,
        "meta": {"type": "READMODEL", "title": title, "fields": fields_list, "description": description},
        "node": {"id": node_id, "data": {"title": title}},
    })
    return node_id


node_ids = {}

node_ids["SubmissionRoutingRejected"] = new_event_node(
    "SubmissionRoutingRejected",
    [
        field("submissionId", "UUID", idAttribute=True),
        field("brokerFirmId", "String"),
        field("requestedCellId", "String"),
        field("requestedClassOfBusiness", "String", optional=True),
        field("rejectionReason", "String", example="broker not authorized for this cell"),
        field("rejectedAt", "DateTime", generated=True),
    ],
    "Broker's panel authorization doesn't cover the requested cell/class. Attempt is still recorded for broker relationship management - never silently dropped.",
)

node_ids["SubmissionNormalized"] = new_event_node(
    "SubmissionNormalized",
    [
        field("submissionId", "UUID", idAttribute=True),
        field("classOfBusiness", "String"),
        field("territory", "String"),
        field("lineSizeSought", "Decimal"),
        field("keyTerms", "String", optional=True),
        field("namedInsured", "String"),
        field("effectiveDateRequested", "Date"),
        field("normalizationStatus", "String", example="success"),
        field("normalizedAt", "DateTime", generated=True),
    ],
    "ADEPT normalization succeeded - structured fields extracted from the raw payload. OPEN QUESTION (flagged by BA): is this genuinely a separate async event from BrokerSubmissionReceived, or should they collapse into one atomic event if normalization is synchronous/fast? Depends on actual ADEPT integration latency - revisit once that's known.",
)

node_ids["SubmissionNormalizationFailed"] = new_event_node(
    "SubmissionNormalizationFailed",
    [
        field("submissionId", "UUID", idAttribute=True),
        field("failureReason", "String", example="missing required field: TIV"),
        field("rawPayloadRef", "Custom"),
        field("attemptedAt", "DateTime", generated=True),
    ],
    "Missing required field, malformed data, unrecognized class code, or schema validation error. Raw payload is always preserved, never discarded. OPEN QUESTIONS (flagged by BA): (1) does the broker get an automatic notification, or is chasing manual - silent failure risks brokers bypassing the platform entirely; (2) is there a retry limit / timeout before a failed submission is considered abandoned?",
)

node_ids["SubmissionManuallyCorrected"] = new_event_node(
    "SubmissionManuallyCorrected",
    [
        field("submissionId", "UUID", idAttribute=True),
        field("correctedBy", "String"),
        field("correctionDescription", "String"),
        field("resubmittedForNormalization", "Boolean"),
        field("correctedAt", "DateTime", generated=True),
    ],
    "Manual remediation path: operations or the broker (re-sending) fixes the data, re-triggering normalization.",
)

node_ids["PotentialDuplicateSubmissionDetected"] = new_event_node(
    "PotentialDuplicateSubmissionDetected",
    [
        field("submissionId", "UUID", idAttribute=True),
        field("suspectedOriginalSubmissionId", "UUID"),
        field("matchBasis", "String", example="same broker + same named insured + same class + overlapping effective date"),
        field("confidenceLevel", "Decimal", optional=True),
        field("detectedAt", "DateTime", generated=True),
    ],
    "New submission is always recorded regardless (never silently dropped) - this event flags it as a possible resubmission alongside an existing open submission. OPEN QUESTION (flagged by BA): exact matching heuristic (exact-field vs fuzzy/probabilistic) materially affects false-positive rate and needs a real answer, not a placeholder.",
)

node_ids["SubmissionSuperseded"] = new_event_node(
    "SubmissionSuperseded",
    [
        field("originalSubmissionId", "UUID", idAttribute=True),
        field("supersedingSubmissionId", "UUID"),
        field("linkedBy", "String"),
        field("linkedAt", "DateTime", generated=True),
    ],
    "Underwriter/ops confirms the new submission is a genuine resubmission/update of the original - modeled as its own linking event (old -> new) rather than silently discarding the original, preserving event-stream integrity.",
)

node_ids["SubmissionConfirmedDistinct"] = new_event_node(
    "SubmissionConfirmedDistinct",
    [
        field("submissionId", "UUID", idAttribute=True),
        field("suspectedOriginalSubmissionId", "UUID"),
        field("confirmedBy", "String"),
        field("confirmedAt", "DateTime", generated=True),
    ],
    "Underwriter/ops confirms the flagged submissions are coincidentally similar but genuinely distinct risks - both proceed independently.",
)

node_ids["SubmissionQueue"] = new_readmodel_node(
    "SubmissionQueue",
    "SubmissionNormalized",
    [
        field("submissionId", "UUID", idAttribute=True),
        field("brokerFirmId", "String"),
        field("classOfBusiness", "String"),
        field("territory", "String"),
        field("lineSizeSought", "Decimal"),
        field("receivedAt", "DateTime"),
        field("status", "String", example="Ready for review"),
        field("isPossibleDuplicate", "Boolean"),
        field("suspectedOriginalSubmissionId", "UUID", optional=True),
    ],
    "Underwriter's main worklist. Possible-duplicate submissions stay in this normal flow with a badge (rather than a separate queue) since resolving them needs underwriter domain judgment, not just ops triage.",
)

node_ids["SubmissionExceptionQueue"] = new_readmodel_node(
    "SubmissionExceptionQueue",
    "SubmissionNormalizationFailed",
    [
        field("submissionId", "UUID", idAttribute=True),
        field("brokerFirmId", "String"),
        field("failureReason", "String"),
        field("attemptedAt", "DateTime"),
        field("status", "String", example="Awaiting Correction"),
    ],
    "Separate from the underwriter's main queue - submissions stuck in a failed normalization state, for operations to chase the broker or manually intervene.",
)

node_ids["BrokerAuthorizationExceptionLog"] = new_readmodel_node(
    "BrokerAuthorizationExceptionLog",
    "SubmissionRoutingRejected",
    [
        field("submissionId", "UUID", idAttribute=True),
        field("brokerFirmId", "String"),
        field("requestedCellId", "String"),
        field("requestedClassOfBusiness", "String", optional=True),
        field("rejectionReason", "String"),
        field("rejectedAt", "DateTime"),
        field("reviewStatus", "String", example="Pending Review"),
    ],
    "Never appears in any underwriter's queue. Visible to whoever manages broker/cell panel relationships (ops or business development) - may represent a legitimate request to extend the broker's panel, not just an error.",
)

# Rename + refield the existing command
events.append({
    "id": str(uuid.uuid4()),
    "eventType": "node:changed",
    "nodeId": "3d4038e6-e7d7-49e4-9361-81f8f9c2df0d",
    "boardId": BOARD_ID,
    "timestamp": now,
    "changedAttributes": ["meta.title", "meta.fields", "meta.description"],
    "meta": {
        "type": "COMMAND",
        "title": "ReceiveBrokerSubmission",
        "fields": [
            field("brokerFirmId", "String"),
            field("submittingContact", "String"),
            field("cellIdHint", "String", optional=True),
            field("classOfBusinessHint", "String", optional=True),
            field("rawPayload", "Custom", example="broker system's native format"),
            field("sourceChannel", "String", example="Howden ADEPT integration"),
        ],
        "description": "Renamed from SubmitBrokerSubmission - actor is the broker's system or Broker Connect's own ingestion service acting on the broker's behalf, not necessarily a direct user action.",
    },
    "node": {"id": "3d4038e6-e7d7-49e4-9361-81f8f9c2df0d", "data": {}},
})

# Refield the existing BrokerSubmissionReceived event
events.append({
    "id": str(uuid.uuid4()),
    "eventType": "node:changed",
    "nodeId": "1e09d318-e41a-454e-947c-69de559c5195",
    "boardId": BOARD_ID,
    "timestamp": now,
    "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {
        "type": "EVENT",
        "title": "BrokerSubmissionReceived",
        "fields": [
            field("submissionId", "UUID", idAttribute=True, generated=True),
            field("brokerFirmId", "String"),
            field("submittingContact", "String"),
            field("rawPayloadRef", "Custom", example="stored as-is, immutable"),
            field("sourceChannel", "String"),
            field("receivedAt", "DateTime", generated=True),
        ],
        "description": "The raw receipt always succeeds if the payload arrives at all - always recorded regardless of what normalization/routing/duplicate-checking finds downstream. Class of business, territory, and other risk detail move to SubmissionNormalized once extraction succeeds.",
    },
    "node": {"id": "1e09d318-e41a-454e-947c-69de559c5195", "data": {}},
})

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print(status)
print(json.dumps(resp, indent=2)[:2000])

with open("/tmp/claude_node_ids.json", "w") as f:
    json.dump(node_ids, f, indent=2)
print("node_ids saved")
