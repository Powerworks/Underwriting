import json
import uuid
import urllib.request
from collections import defaultdict

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"
CHAPTER_ID = "e96f1046-4ab4-4654-89f3-8074c0aa06f4"

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "gwt-build"}

with open("column_lookup_by_index2.json") as f:
    LOOKUP = json.load(f)


def n(idx, lane="swimlane"):
    e = LOOKUP[idx]["ids"][lane]
    return {"id": e["id"], "title": e["title"], "type": e["type"]}


def col_id(idx):
    return LOOKUP[idx]["columnId"]


def call(method, path, body=None):
    url = f"{BASE_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, headers=HEADERS, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read())


# (target_column_index, title, given[(idx,lane)], when[(idx,lane)], then[(idx,lane)])
S = [
    (79, "S1a.5 Baseline premium generated after successful normalization", [(7, "swimlane")], [], [(79, "swimlane")]),
    (79, "S1a.5 PricedSubmissionView reflects the baseline alongside the broker's ask", [(79, "swimlane")], [], [(79, "interaction")]),
    (80, "S1a.5 Underwriter accepts the AI baseline as proposed", [(79, "swimlane")], [(14, "interaction")], [(80, "swimlane")]),
    (81, "S1a.5 Underwriter overrides the AI baseline", [(79, "swimlane")], [(14, "interaction")], [(81, "swimlane")]),
    (82, "S1a.5 New pricing model version deployed", [], [], [(82, "swimlane")]),

    (83, "S1f.1 Cat bond registered as capacity reference", [], [(83, "interaction")], [(83, "swimlane")]),
    (84, "S1f.2 Industry loss index update received", [], [], [(84, "swimlane")]),
    (85, "S1f.2 Attachment distance recalculated on index update", [(84, "swimlane"), (83, "swimlane")], [], [(85, "swimlane")]),
    (85, "S1f.2 CatBondCoverageView reflects the current distance to attachment", [(85, "swimlane")], [], [(85, "interaction")]),
    (86, "S1f.4 Cat bond triggered - losses cross the attachment point", [(85, "swimlane")], [], [(86, "swimlane")]),
]

by_target = defaultdict(list)
for target, title, given, when, then in S:
    scenario = {
        "id": str(uuid.uuid4()), "title": title,
        "given": [n(i, l) for i, l in given], "when": [n(i, l) for i, l in when], "then": [n(i, l) for i, l in then],
    }
    by_target[target].append(scenario)

print(f"Total scenarios: {len(S)}, across {len(by_target)} target columns")
for target, scenarios in by_target.items():
    cid = col_id(target)
    status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/timelines/{CHAPTER_ID}/columns/{cid}/scenarios", scenarios)
    print(f"col {target}:", status, "OK" if status == 201 else resp)
