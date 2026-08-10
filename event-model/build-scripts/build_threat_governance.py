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


column_labels = [
    # External Threat (5)
    "StormTrackUpdateReceived",
    "ThreatFeedStale",
    "PMLRecalculated-1d",
    "ThreatResolved",
    "ExposureAtRiskCleared",
    # Portfolio Governance (6)
    "TerritoryUnderwritingFrozen",
    "SubmissionBlockedByPortfolioFreeze",
    "TerritoryUnderwritingFrozenLifted",
    "PortfolioFreezeOverridden",
    "HedgingActionRequested-1",
    "HedgingActionRequested-2",
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

with open("/tmp/claude_column_ids3.json", "w") as f:
    json.dump(column_ids, f, indent=2)
