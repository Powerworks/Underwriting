import json
import time
import urllib.request
import uuid

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "role-catalog"}


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

# (title, kind, description)
ROLES = [
    ("Broker", "Human role", "External (e.g. Howden). Submits risk submissions, accepts/declines/requests amendment on quotes, notifies claims. Appears in: Submission Intake, Underwriting Decisioning, Claims."),
    ("Underwriter", "Human role", "Assesses submissions against delegated authority, issues quotes, binds policies, processes endorsements/cancellations, searches submission history. Appears in: Search & Retrieval, Underwriting Decisioning, Binding."),
    ("Cell Head Underwriter / CUO", "Human role", "Grants underwriter-tier authority within a cell's own limit, resolves referred submissions at the cell tier. Appears in: Authority Administration, Underwriting Decisioning."),
    ("Underwriting Governance / Senior Executive", "Human role", "Grants and revises cell-tier authority limits; per DEC-026, may or may not be the same tier that holds portfolio freeze override authority - not yet resolved. Appears in: Authority Administration, Portfolio Governance."),
    ("Operations / Finance", "Human role", "Handles submission exceptions (failed normalization, broker authorization rejections), processes bordereaux settlement, raises prior-period adjustments. Appears in: Submission Intake, Bordereaux Settlement."),
    ("Claims Handler", "Human role", "Notifies and processes claims: sets reserves, validates payment against bind terms, pays, closes, reopens. Appears in: Claims, Search & Retrieval (automatic lineage retrieval on their behalf)."),
    ("Capacity Provider (or TPA)", "Human role", "Grants cell-tier authority (as the delegating party), raises and confirms agreement on bordereaux queries/lines. Appears in: Authority Administration, Bordereaux Settlement."),
    ("Portfolio Manager", "Human role", "Requests hedging actions tied to an active freeze or narrowing cat bond coverage. Appears in: Portfolio Governance, Capital & Reinsurance Instruments."),
    ("Capital Markets / Outwards Reinsurance Team", "Human role", "Registers cat bonds as capacity reference data; the handoff destination for hedging action requests (actual placement execution is outside this system's scope - see IR-004). Appears in: Capital & Reinsurance Instruments, Portfolio Governance."),
    ("Actuarial / Claims Audit Function", "Human role", "Audits TFP's own actual incurred losses against indemnity cat bond trigger thresholds - a distinct, always-human-confirmed determination, never self-executing. Appears in: Capital & Reinsurance Instruments."),
    ("External Rating Engine", "System actor", "Generates the AI baseline premium from ACORD-normalized submission data, cross-referencing alternative data (weather, satellite imagery, historical loss patterns). This system requests and displays its output; it never reproduces the calculation. Appears in: Submission Intake."),
    ("External Weather/Threat Feed (e.g. NHC)", "System actor", "Pushes live storm track updates on its own schedule - the first actor in the whole model whose trigger isn't internal to TFP. No accountable sender if an update goes missing, hence explicit feed-staleness monitoring. Appears in: External Threat."),
    ("External Industry Loss Index Provider", "System actor", "Pushes industry-wide aggregated catastrophe loss updates (e.g. a PCS-style provider), structurally distinct from the weather/threat feed - tracks industry losses, not TFP's own modeled exposure. Appears in: Capital & Reinsurance Instruments."),
    ("Broker Connect Ingestion Service", "System actor", "Acts on the broker's behalf to receive and normalize a raw submission to the ACORD ADEPT schema; may be the same actor as \"Broker\" from the system's point of view depending on integration channel. Appears in: Submission Intake."),
]

events = []
for title, kind, description in ROLES:
    node_id = str(uuid.uuid4())
    events.append({
        "id": str(uuid.uuid4()), "eventType": "node:created", "nodeId": node_id, "boardId": BOARD_ID,
        "timestamp": now,
        "meta": {"type": "ACTOR", "title": title, "description": f"[{kind}] {description}"},
        "node": {"id": node_id, "data": {"title": title}},
    })

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print("create role catalog:", status)
print(json.dumps(resp, indent=2)[:600])
print(f"Total roles created: {len(ROLES)}")
