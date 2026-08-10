import json
import time
import urllib.request
import uuid

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "chapter-migration"}


def call(method, path, body=None):
    url = f"{BASE_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, headers=HEADERS, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read())


with open("migration_columns.json") as f:
    COLUMNS = json.load(f)  # list indexed by old column index

CONTEXTS = {
    "Authority Administration": [0, 1, 2, 3, 4, 57, 58, 59, 60],
    "Submission Intake": [5, 6, 7, 8, 9, 10, 11, 12, 79, 80, 81, 82],
    "Underwriting Decisioning": [13, 14, 15, 16, 17, 18, 19, 61, 62, 63, 64, 65, 66, 67],
    "Binding": [20, 21, 22, 23, 68, 69, 70, 71],
    "Bordereaux Settlement": [24, 25, 26, 27, 28, 29, 72, 73, 74],
    "Claims": [30, 31, 32, 33, 34, 75, 76, 77, 78],
    "Search & Retrieval": [35, 36, 37, 38],
    "Exposure Intelligence": [39, 40, 41, 42, 43, 44, 45],
    "External Threat": [46, 47, 48, 49, 50],
    "Portfolio Governance": [51, 52, 53, 54, 55, 56],
    "Capital & Reinsurance Instruments": [83, 84, 85, 86, 87],
}

assert sorted(sum(CONTEXTS.values(), [])) == list(range(88)), "column coverage mismatch"

now = int(time.time() * 1000)

id_map = {}  # oldNodeId -> newNodeId
column_map = {}  # oldColumnIndex -> {"chapterId":..., "newColumnId":...}
chapter_ids = {}  # context label -> chapterId

for label, col_indices in CONTEXTS.items():
    # 1. create chapter
    status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/chapters", {"position": {"x": 0, "y": 0}})
    assert status == 200, (label, status, resp)
    chapter_id = resp["id"] if "id" in resp else resp.get("data", {}).get("id")
    if not chapter_id:
        print("UNEXPECTED chapter response shape:", resp)
        raise SystemExit(1)
    chapter_ids[label] = chapter_id
    print(f"Chapter '{label}' -> {chapter_id}")

    # 2. set title
    evt_id = str(uuid.uuid4())
    call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", [{
        "id": evt_id, "eventType": "node:changed", "nodeId": chapter_id, "boardId": BOARD_ID,
        "timestamp": now, "changedAttributes": ["meta.title"],
        "meta": {"type": "CHAPTER", "title": label},
        "node": {"id": chapter_id, "data": {"title": label}},
    }])

    # 3. create columns sequentially, one per old column index, preserving order
    new_col_ids = []
    for old_idx in col_indices:
        status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/timelines/{chapter_id}/columns", {})
        assert status == 200, (label, old_idx, status, resp)
        new_col_ids.append(resp["columnId"])
        column_map[old_idx] = {"chapterId": chapter_id, "newColumnId": resp["columnId"]}

    # 4. fetch chapter to get row ids
    status, chap = call("GET", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/{chapter_id}")
    rows_by_type = {r["type"]: r["id"] for r in chap["meta"]["timelineData"]["rows"]}

    # 5. batch-create all nodes for this chapter
    node_events = []
    for old_idx, new_col_id in zip(col_indices, new_col_ids):
        col_entry = COLUMNS[old_idx]
        for lane, lane_data in col_entry["lanes"].items():
            if lane == "spec":
                continue
            row_id = rows_by_type.get(lane)
            if not row_id:
                continue
            new_node_id = str(uuid.uuid4())
            id_map[lane_data["oldNodeId"]] = new_node_id
            cell_id = f"{row_id}-{new_col_id}"
            node_events.append({
                "id": str(uuid.uuid4()), "eventType": "node:created", "nodeId": new_node_id, "boardId": BOARD_ID,
                "timestamp": now, "chapterId": chapter_id, "cellId": cell_id,
                "meta": {"type": lane_data["type"], "title": lane_data["title"],
                         "fields": lane_data.get("fields", []), "description": lane_data.get("description", "")},
                "node": {"id": new_node_id, "data": {"title": lane_data["title"]}},
            })

    # send in chunks to keep payload size safe (learned earlier: keep POSTs compact/reasonably sized)
    CHUNK = 40
    for i in range(0, len(node_events), CHUNK):
        chunk = node_events[i:i + CHUNK]
        status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", chunk)
        print(f"  {label}: nodes batch {i}-{i+len(chunk)}: {status}")
        if status != 200:
            print("    ERROR:", resp)

    # 6. create SLICE_BORDER per new column, title = swimlane element's title
    border_events = []
    for old_idx, new_col_id in zip(col_indices, new_col_ids):
        swimlane = COLUMNS[old_idx]["lanes"].get("swimlane")
        title = swimlane["title"] if swimlane else COLUMNS[old_idx]["lanes"].get("interaction", {}).get("title", "Untitled")
        border_id = str(uuid.uuid4())
        border_events.append({
            "id": str(uuid.uuid4()), "eventType": "node:created", "nodeId": border_id, "boardId": BOARD_ID,
            "timestamp": now, "chapterId": chapter_id,
            "meta": {"type": "SLICE_BORDER", "colId": new_col_id, "title": title},
            "node": {"id": border_id, "data": {}},
        })
    status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", border_events)
    print(f"  {label}: {len(border_events)} slice borders: {status}")

with open("chapter_ids.json", "w") as f:
    json.dump(chapter_ids, f, indent=2)
with open("column_map.json", "w") as f:
    json.dump(column_map, f, indent=2)
with open("id_map.json", "w") as f:
    json.dump(id_map, f, indent=2)

print()
print("Chapters created:", len(chapter_ids))
print("Nodes remapped:", len(id_map))
print("Columns remapped:", len(column_map))
