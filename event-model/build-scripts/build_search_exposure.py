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


# ---------------------------------------------------------------------------
# Step 1: append columns (append mode = no index needed, always lands at end)
# ---------------------------------------------------------------------------
column_labels = [
    # Search & Retrieval (4)
    "SearchSubmissionHistory-1",
    "SearchSubmissionHistory-2",
    "CrossCellSearchAttempted",
    "SubmissionLineageRetrieved",
    # Exposure Intelligence (7)
    "ExposureProjectionUpdated-Bind",
    "ExposureConcentrationWarningRaised",
    "ExposureConcentrationWarningAcknowledged",
    "ExposureProjectionUpdated-Endorsement",
    "ExposureProjectionUpdated-Cancellation",
    "ExposureExtractGenerated-1",
    "ExposureExtractGenerated-2",
]

column_ids = {}
for label in column_labels:
    status, resp = call(
        "POST",
        f"/api/org/{ORG_ID}/boards/{BOARD_ID}/timelines/{CHAPTER_ID}/columns",
        {},
    )
    if status != 200:
        print(f"FAILED creating column for {label}: {status} {resp}")
        raise SystemExit(1)
    column_ids[label] = resp["columnId"]
    print(f"Created column for {label} at index {resp['index']} -> {resp['columnId']}")

with open("/tmp/claude_column_ids2.json", "w") as f:
    json.dump(column_ids, f, indent=2)
