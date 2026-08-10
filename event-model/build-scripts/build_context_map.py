import json
import time
import urllib.request
import uuid

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "context-map"}


def call(method, path, body=None):
    url = f"{BASE_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, headers=HEADERS, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read())


now = int(time.time() * 1000)

contexts = [
    ("0", "Authority Administration", "Governance/reference data - the Provider->Cell->Underwriter authority cascade. Turned out to be a cross-cutting engine: also gates Binding's material endorsements and Claims' reserve revisions, not just Decisioning."),
    ("1a", "Submission Intake", "Broker submission received and normalized to ACORD ADEPT. Confirmed scope."),
    ("1b", "Search & Retrieval", "Query-side capability over the event stream - historic submission search plus automatic lineage retrieval for claims handlers. Confirmed scope."),
    ("1c", "Exposure Intelligence", "Aggregate exposure by geocode, stacking detection, feeds cat modeling. Exploratory - establishes the ingest/display, not compute integration-boundary pattern."),
    ("1d", "External Threat", "Ingesting live threat data (storm tracks), triggering PML recalculation. Exploratory - the first context triggered by an external feed rather than an internal actor."),
    ("1e", "Portfolio Governance", "Freezing/unfreezing territories based on PML/Combined Ratio thresholds, overrides, hedging requests. Exploratory - the second gate into Decisioning, alongside Authority Administration."),
    ("2", "Underwriting Decisioning", "Within-authority vs. referred (cascade) vs. declined, quote issuance, quote acceptance/expiry/decline. Gated by both Authority Administration and, in the exploratory extension, Portfolio Governance."),
    ("3", "Binding", "The immutable ledger - policy bound (quota share split), endorsed, cancelled, renewed. Everything downstream keys off PolicyBound."),
    ("4", "Bordereaux Settlement", "Periodic, scoped to cell+provider+period: drafted -> queried/resolved -> agreed -> settled, with adjustment lines for corrections."),
    ("5", "Claims", "Notified -> reserved (recurring) -> paid (split by provider quota share) -> closed, with reopening as a loop back. Closes the loop back to submission/decisioning lineage."),
]

node_ids = {}
events = []
for label, title, desc in contexts:
    node_id = str(uuid.uuid4())
    node_ids[label] = node_id
    events.append({
        "id": str(uuid.uuid4()), "eventType": "node:created", "nodeId": node_id, "boardId": BOARD_ID,
        "timestamp": now,
        "meta": {"type": "MODEL_CONTEXT", "title": title, "description": desc},
        "node": {"id": node_id, "data": {"title": title}},
    })

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print("create contexts:", status, json.dumps(resp)[:300])

with open("context_node_ids.json", "w") as f:
    json.dump(node_ids, f, indent=2)

# Relationships: (source_label, target_label) - directed "feeds into / gates"
relationships = [
    ("0", "2", "Authority cascade gates decisioning"),
    ("1e", "2", "Portfolio freeze gates decisioning - second, independent gate"),
    ("1a", "2", "Received/normalized submission feeds decisioning"),
    ("2", "3", "Accepted quote proceeds to bind"),
    ("3", "4", "Bound/endorsed/cancelled transactions feed bordereaux settlement"),
    ("3", "5", "Bound policy is the claim's origination reference and quota-share basis"),
    ("3", "1c", "PolicyBound/Endorsed/Cancelled trigger exposure projection updates"),
    ("1c", "1d", "Exposure map is overlaid against incoming storm tracks for PML recalculation"),
    ("1d", "1e", "PML/threshold breach triggers territory freeze"),
    ("1b", "5", "Submission lineage view validates claim payment against original bind terms"),
    ("0", "3", "Authority engine reused: material endorsements re-trigger the same referral mechanism"),
    ("0", "5", "Authority engine reused: material reserve revisions re-trigger the same referral mechanism"),
]

conn_events = []
for src, tgt, desc in relationships:
    status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/connections",
                         {"source": node_ids[src], "target": node_ids[tgt]})
    print(f"{src} -> {tgt}:", status, json.dumps(resp)[:150])

print("Done.")
