import json
import time
import urllib.request
import uuid

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"
HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "role-screens"}


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

SCREENS = [
    ("CellAuthorityAdministration", "03192307-3472-445e-81df-ff572b8c2d3b", "bfe4d87f-6ab4-43bc-b55a-ad4f092fd0fd",
     "Underwriting Governance's screen for granting/revising/revoking a cell's own authority limit."),
    ("UnderwriterAuthorityGrant", "03192307-3472-445e-81df-ff572b8c2d3b", "6d6ebadd-7798-4067-aa81-6aaba8922007",
     "Cell Head Underwriter/CUO's screen for granting an individual underwriter authority within the cell's own limit."),
    ("BordereauSettlementWorkbench", "67128723-85a0-4cc1-8b56-5d13a41108c0", "9478014b-acaa-467e-a2ad-bdf14f2f89bb",
     "Operations/Finance's screen for reviewing a drafted bordereau and progressing it to agreement and settlement."),
    ("BordereauProviderReview", "67128723-85a0-4cc1-8b56-5d13a41108c0", "1170ddda-cffc-4218-a470-86da8fb37130",
     "Capacity Provider/TPA's screen for reviewing a bordereau, raising line queries, and confirming agreement."),
    ("PortfolioFreezeHedging", "12d16da6-8f97-48cb-84c1-f170c2bbd931", "4b04a5b2-a254-42cf-9bed-ef22287fd9ab",
     "Portfolio Manager's screen for viewing active freezes and requesting a hedging action."),
    ("CatBondRegistry", "6d0d91c1-5165-44ab-82b9-2ff98f0487a9", "e1d300a7-0d6e-479b-9004-1dfdf5fe53a0",
     "Capital Markets/Outwards Reinsurance Team's screen for registering a cat bond and viewing live coverage distance."),
    ("IndemnityLossAudit", "6d0d91c1-5165-44ab-82b9-2ff98f0487a9", "be6ac9a0-6b83-44e6-85d5-8a8231e5235f",
     "Actuarial/Claims Audit Function's screen for auditing TFP's own incurred losses against an indemnity cat bond's trigger threshold."),
]

node_ids = {}
for title, chapter_id, col_id, description in SCREENS:
    status, chap = call("GET", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/{chapter_id}")
    actor_row = [r["id"] for r in chap["meta"]["timelineData"]["rows"] if r["type"] == "actor"][0]
    cell_id = f"{actor_row}-{col_id}"
    node_id = str(uuid.uuid4())
    node_ids[title] = {"nodeId": node_id, "chapterId": chapter_id, "colId": col_id}
    events = [{
        "id": str(uuid.uuid4()), "eventType": "node:created", "nodeId": node_id, "boardId": BOARD_ID,
        "timestamp": now, "chapterId": chapter_id, "cellId": cell_id,
        "meta": {"type": "SCREEN", "title": title, "description": description},
        "node": {"id": node_id, "data": {"title": title}},
    }]
    status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
    print(title, "->", status)

with open("role_screen_ids.json", "w") as f:
    json.dump(node_ids, f, indent=2)
print(json.dumps(node_ids, indent=2))
