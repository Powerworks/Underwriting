import json
import time
import urllib.request
import uuid
from collections import defaultdict

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "scenario-migration"}


def call(method, path, body=None):
    url = f"{BASE_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, headers=HEADERS, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read())


with open("old_col_to_spec.json") as f:
    OLD_COL_TO_SPEC = json.load(f)  # oldColumnId -> specNodeId
with open("old_scenarios.json") as f:
    OLD_SCENARIOS = {n["id"]: n for n in json.load(f)}
with open("id_map.json") as f:
    ID_MAP = json.load(f)  # oldNodeId -> newNodeId
with open("column_map.json") as f:
    COLUMN_MAP = json.load(f)  # oldColumnIndex(str) -> {chapterId, newColumnId}
with open("migration_columns.json") as f:
    OLD_COLUMNS = json.load(f)  # list by old index, has oldColumnId

# build oldColumnId (uuid) -> oldColumnIndex
old_colid_to_index = {}
# need the old chapter's column order; reconstruct from OLD_COLUMNS which already stored oldColumnId
for i, c in enumerate(OLD_COLUMNS):
    old_colid_to_index[c["oldColumnId"]] = i

# build oldNodeId -> which chapter it now lives in (via id_map + which old column it came from)
old_node_to_new_chapter = {}
for i, c in enumerate(OLD_COLUMNS):
    chapter_id = COLUMN_MAP[str(i)]["chapterId"]
    for lane, data in c["lanes"].items():
        if lane == "spec":
            continue
        old_node_to_new_chapter[data["oldNodeId"]] = chapter_id


def remap_step(step, target_chapter):
    """Return remapped step dict, or None if it can't be included (missing id_map entry)."""
    old_id = step["id"]
    new_id = ID_MAP.get(old_id)
    if not new_id:
        return None, None
    step_chapter = old_node_to_new_chapter.get(old_id)
    cross_chapter = step_chapter != target_chapter
    return {"id": new_id, "title": step.get("title", ""), "type": step.get("type", "")}, cross_chapter


results = {"ok": 0, "fail": 0, "dropped_given": 0, "skipped_scenarios": 0, "errors": []}
by_new_target = defaultdict(list)  # (chapterId, newColumnId) -> list of scenario dicts

for old_col_uuid, spec_node_id in OLD_COL_TO_SPEC.items():
    old_idx = old_colid_to_index.get(old_col_uuid)
    if old_idx is None:
        print(f"WARNING: old column {old_col_uuid} not found in migration_columns.json - skipping")
        continue
    target_chapter = COLUMN_MAP[str(old_idx)]["chapterId"]
    target_col = COLUMN_MAP[str(old_idx)]["newColumnId"]

    spec_node = OLD_SCENARIOS.get(spec_node_id)
    if not spec_node:
        print(f"WARNING: spec node {spec_node_id} not found - skipping")
        continue
    scenarios = spec_node.get("meta", {}).get("givenWhenThenScenario", {}).get("scenarios", [])

    for sc in scenarios:
        title = sc.get("title", "")
        if not title or title.startswith("Probe scenario"):
            results["skipped_scenarios"] += 1
            continue  # drop leftover test-probe scenarios, don't migrate junk

        new_given = []
        for step in sc.get("given", []):
            remapped, cross = remap_step(step, target_chapter)
            if remapped is None:
                continue
            if cross:
                results["dropped_given"] += 1
                continue
            new_given.append(remapped)

        new_when = []
        for step in sc.get("when", []):
            remapped, cross = remap_step(step, target_chapter)
            if remapped:
                new_when.append(remapped)  # when/then should never legitimately cross chapters by design

        new_then = []
        for step in sc.get("then", []):
            remapped, cross = remap_step(step, target_chapter)
            if remapped:
                new_then.append(remapped)

        new_scenario = {
            "id": str(uuid.uuid4()),
            "title": title,
            "given": new_given,
            "when": new_when,
            "then": new_then,
        }
        by_new_target[(target_chapter, target_col)].append(new_scenario)

print(f"Scenarios to migrate: {sum(len(v) for v in by_new_target.values())}, skipped (probes): {results['skipped_scenarios']}, dropped cross-chapter givens: {results['dropped_given']}")
print(f"Target columns: {len(by_new_target)}")

for (chapter_id, col_id), scenarios in by_new_target.items():
    status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/timelines/{chapter_id}/columns/{col_id}/scenarios", scenarios)
    if status == 201:
        results["ok"] += len(scenarios)
    else:
        results["fail"] += len(scenarios)
        results["errors"].append((chapter_id, col_id, status, resp))
        print(f"FAILED {chapter_id}/{col_id}: {status} {resp}")

print(json.dumps({"ok": results["ok"], "fail": results["fail"]}, indent=2))
