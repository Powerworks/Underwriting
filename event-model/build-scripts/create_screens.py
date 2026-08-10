import json
import time
import urllib.request
import uuid

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"
CHAPTER_ID = "e96f1046-4ab4-4654-89f3-8074c0aa06f4"
ACTOR_ROW = "1af53c6a-6c34-4cd3-987c-7ac085fe0cf0"

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "screen-design"}

with open("column_lookup_by_index.json") as f:
    LOOKUP = json.load(f)


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
screens = [
    ("SubmissionQueue", 7, "Underwriter's Submission Queue - worklist of received/normalized submissions with status and duplicate badges."),
    ("SubmissionAssessment", 13, "Submission Assessment - underwriter reviews proposed terms against their authority limit."),
    ("QuoteView", 18, "Broker's Quote screen - view issued quote, accept, decline, or request amendment."),
    ("ClaimHandling", 30, "Claims handler screen - notify a claim, view policy origination lineage, set reserve, pay."),
]

node_ids = {}
events = []
for title, col_idx, desc in screens:
    node_id = str(uuid.uuid4())
    node_ids[title] = {"nodeId": node_id, "colIdx": col_idx}
    col_id = LOOKUP[col_idx]["columnId"]
    cell_id = f"{ACTOR_ROW}-{col_id}"
    events.append({
        "id": str(uuid.uuid4()), "eventType": "node:created", "nodeId": node_id, "boardId": BOARD_ID,
        "timestamp": now, "chapterId": CHAPTER_ID, "cellId": cell_id,
        "meta": {"type": "SCREEN", "title": title, "description": desc},
        "node": {"id": node_id, "data": {"title": title}},
    })

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print("create screens:", status, json.dumps(resp)[:400])

with open("screen_node_ids.json", "w") as f:
    json.dump(node_ids, f, indent=2)
print(json.dumps(node_ids, indent=2))
