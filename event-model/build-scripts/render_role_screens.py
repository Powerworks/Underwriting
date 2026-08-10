import json
import urllib.request

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"
HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID}


def call(method, path, body=None):
    url = f"{BASE_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, headers=HEADERS, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


with open("role_screen_ids.json") as f:
    nodes = json.load(f)

bg = {"type": "rectangle", "gridX": 0, "gridY": 0, "gridWidth": 50, "gridHeight": 40, "fill": "white"}


def header(title):
    return [
        {"type": "rectangle", "gridX": 0, "gridY": 0, "gridWidth": 50, "gridHeight": 3, "fill": "violet"},
        {"type": "headline", "gridX": 2, "gridY": 1, "text": title, "fontSize": 15, "fill": "white"},
    ]


designs = {}

# 1. CellAuthorityAdministration
designs["CellAuthorityAdministration"] = {
    "semanticDescription": "Underwriting Governance's screen for granting/revising/revoking a cell's authority limit.",
    "elements": [bg, *header("Cell Authority Administration - itasca-mga"),
        {"type": "text", "gridX": 2, "gridY": 6, "text": "Current limit (v3): $250,000 / $15,000,000 - Aircraft Non-Payment Credit", "fontSize": 11},
        {"type": "text", "gridX": 2, "gridY": 9, "text": "Source agreement: Pelagos DUA-2026-014", "fontSize": 10, "fill": "grey"},
        {"type": "rectangle", "gridX": 2, "gridY": 13, "gridWidth": 30, "gridHeight": 12, "fill": "light-blue", "stroke": "grey"},
        {"type": "text", "gridX": 4, "gridY": 14.5, "text": "New limit", "fontSize": 10, "fill": "blue"},
        {"type": "input", "gridX": 4, "gridY": 17, "gridWidth": 24, "gridHeight": 2.5, "text": "Max gross premium"},
        {"type": "input", "gridX": 4, "gridY": 20.5, "gridWidth": 24, "gridHeight": 2.5, "text": "Max aggregate limit"},
        {"type": "button", "gridX": 2, "gridY": 28, "gridWidth": 11, "gridHeight": 3, "text": "Revise Limit", "fill": "yellow"},
        {"type": "button", "gridX": 15, "gridY": 28, "gridWidth": 11, "gridHeight": 3, "text": "Revoke Entirely", "fill": "light-red"},
        {"type": "text", "gridX": 2, "gridY": 33, "text": "Revising or revoking may trigger in-flight submission reassessment.", "fontSize": 9, "fill": "grey"},
    ],
}

# 2. UnderwriterAuthorityGrant
designs["UnderwriterAuthorityGrant"] = {
    "semanticDescription": "Cell Head Underwriter/CUO's screen for granting an individual underwriter authority within the cell's own limit.",
    "elements": [bg, *header("Grant Underwriter Authority - itasca-mga"),
        {"type": "text", "gridX": 2, "gridY": 6, "text": "Cell's own limit: $250,000 / $15,000,000 (v3)", "fontSize": 11, "fill": "grey"},
        {"type": "input", "gridX": 2, "gridY": 10, "gridWidth": 20, "gridHeight": 2.5, "text": "Underwriter"},
        {"type": "input", "gridX": 2, "gridY": 13.5, "gridWidth": 20, "gridHeight": 2.5, "text": "Class of business"},
        {"type": "input", "gridX": 2, "gridY": 17, "gridWidth": 20, "gridHeight": 2.5, "text": "Requested line size"},
        {"type": "rectangle", "gridX": 26, "gridY": 10, "gridWidth": 20, "gridHeight": 9, "fill": "light-green", "stroke": "grey"},
        {"type": "text", "gridX": 28, "gridY": 11, "text": "u-4471 currently holds:", "fontSize": 10, "fill": "green"},
        {"type": "text", "gridX": 28, "gridY": 14, "text": "$180,000 / $12M (Property)", "fontSize": 10},
        {"type": "text", "gridX": 28, "gridY": 17, "text": "Remaining cell headroom: $70,000", "fontSize": 10},
        {"type": "button", "gridX": 2, "gridY": 24, "gridWidth": 12, "gridHeight": 3, "text": "Grant Authority", "fill": "green"},
        {"type": "text", "gridX": 2, "gridY": 29, "text": "Rejected if requested amount would exceed the cell's own remaining limit.", "fontSize": 9, "fill": "grey"},
    ],
}

# 3. BordereauSettlementWorkbench
designs["BordereauSettlementWorkbench"] = {
    "semanticDescription": "Operations/Finance's screen for reviewing a drafted bordereau and progressing it to agreement and settlement.",
    "elements": [bg, *header("Bordereau BDX-2026-Q3-Pelagos - Draft"),
        {"type": "text", "gridX": 2, "gridY": 6, "text": "Cell: itasca-mga | Provider: Pelagos | Period: Q3 2026", "fontSize": 10.5, "fill": "grey"},
        {"type": "text", "gridX": 2, "gridY": 9, "text": "14 lines swept - 12 clean, 2 queried", "fontSize": 11},
        {"type": "rectangle", "gridX": 2, "gridY": 12, "gridWidth": 46, "gridHeight": 10, "fill": "light-blue", "stroke": "grey"},
        {"type": "text", "gridX": 4, "gridY": 13, "text": "Line 7: Acme Aviation Leasing - quota share mismatch flagged", "fontSize": 10},
        {"type": "text", "gridX": 4, "gridY": 16, "text": "Line 11: Meridian Port Holdings - premium calculation queried", "fontSize": 10},
        {"type": "text", "gridX": 4, "gridY": 19, "text": "Total net premium: $1,240,500", "fontSize": 11, "fill": "blue"},
        {"type": "button", "gridX": 2, "gridY": 25, "gridWidth": 13, "gridHeight": 3, "text": "Resolve Queries", "fill": "yellow"},
        {"type": "button", "gridX": 17, "gridY": 25, "gridWidth": 13, "gridHeight": 3, "text": "Submit for Agreement", "fill": "light-violet"},
        {"type": "button", "gridX": 32, "gridY": 25, "gridWidth": 10, "gridHeight": 3, "text": "Settle", "fill": "green"},
        {"type": "text", "gridX": 2, "gridY": 30, "text": "Settle disabled until zero queries remain open (resolved or deferred to next period).", "fontSize": 9, "fill": "grey"},
    ],
}

# 4. BordereauProviderReview
designs["BordereauProviderReview"] = {
    "semanticDescription": "Capacity Provider/TPA's screen for reviewing a bordereau, raising line queries, and confirming agreement.",
    "elements": [bg, *header("Pelagos - Bordereau Review BDX-2026-Q3-Pelagos"),
        {"type": "text", "gridX": 2, "gridY": 6, "text": "Submitted for agreement by itasca-mga - 2026-10-02", "fontSize": 10.5, "fill": "grey"},
        {"type": "rectangle", "gridX": 2, "gridY": 10, "gridWidth": 46, "gridHeight": 14, "fill": "white", "stroke": "grey"},
        {"type": "text", "gridX": 4, "gridY": 11, "text": "Line", "fontSize": 10, "fill": "grey"},
        {"type": "text", "gridX": 14, "gridY": 11, "text": "Insured", "fontSize": 10, "fill": "grey"},
        {"type": "text", "gridX": 30, "gridY": 11, "text": "Net Premium", "fontSize": 10, "fill": "grey"},
        {"type": "text", "gridX": 40, "gridY": 11, "text": "Status", "fontSize": 10, "fill": "grey"},
        {"type": "line", "gridX": 3, "gridY": 13, "gridX2": 47, "gridY2": 13, "stroke": "light-grey"},
        {"type": "text", "gridX": 4, "gridY": 15, "text": "7", "fontSize": 10},
        {"type": "text", "gridX": 14, "gridY": 15, "text": "Acme Aviation Leasing", "fontSize": 10},
        {"type": "text", "gridX": 30, "gridY": 15, "text": "$74,000", "fontSize": 10},
        {"type": "rectangle", "gridX": 39, "gridY": 14.5, "gridWidth": 8, "gridHeight": 2, "fill": "orange"},
        {"type": "text", "gridX": 39.5, "gridY": 15, "text": "Query", "fontSize": 8.5, "fill": "white"},
        {"type": "button", "gridX": 2, "gridY": 27, "gridWidth": 13, "gridHeight": 3, "text": "Raise Query", "fill": "light-red"},
        {"type": "button", "gridX": 17, "gridY": 27, "gridWidth": 13, "gridHeight": 3, "text": "Confirm Agreement", "fill": "green"},
    ],
}

# 5. PortfolioFreezeHedging
designs["PortfolioFreezeHedging"] = {
    "semanticDescription": "Portfolio Manager's screen for viewing active freezes and requesting a hedging action.",
    "elements": [bg, *header("Portfolio Governance - Active Freezes"),
        {"type": "rectangle", "gridX": 2, "gridY": 6, "gridWidth": 46, "gridHeight": 9, "fill": "light-red", "stroke": "red"},
        {"type": "text", "gridX": 4, "gridY": 7, "text": "Gulf Coast Property/Cat-exposed - FROZEN", "fontSize": 12, "fill": "red"},
        {"type": "text", "gridX": 4, "gridY": 10, "text": "Trigger reasons: PML breach ($220M vs $200M threshold), Combined Ratio", "fontSize": 10},
        {"type": "text", "gridX": 4, "gridY": 13, "text": "Active since: 2026-09-14 08:20", "fontSize": 9.5, "fill": "grey"},
        {"type": "rectangle", "gridX": 2, "gridY": 18, "gridWidth": 46, "gridHeight": 10, "fill": "light-blue", "stroke": "grey"},
        {"type": "text", "gridX": 4, "gridY": 19, "text": "Request hedging action", "fontSize": 10, "fill": "blue"},
        {"type": "input", "gridX": 4, "gridY": 22, "gridWidth": 42, "gridHeight": 2.5, "text": "Requested action - e.g. seek micro-targeted reinsurance layer for Gulf Coast wind"},
        {"type": "button", "gridX": 4, "gridY": 26, "gridWidth": 14, "gridHeight": 2.5, "text": "Submit Request", "fill": "violet"},
        {"type": "button", "gridX": 2, "gridY": 32, "gridWidth": 14, "gridHeight": 3, "text": "Request Override", "fill": "yellow"},
        {"type": "text", "gridX": 18, "gridY": 33, "text": "Requires executive sign-off + mandatory justification", "fontSize": 9, "fill": "grey"},
    ],
}

# 6. CatBondRegistry
designs["CatBondRegistry"] = {
    "semanticDescription": "Capital Markets/Outwards Reinsurance Team's screen for registering a cat bond and viewing live coverage distance.",
    "elements": [bg, *header("Cat Bond Registry"),
        {"type": "rectangle", "gridX": 2, "gridY": 6, "gridWidth": 46, "gridHeight": 11, "fill": "light-violet", "stroke": "grey"},
        {"type": "text", "gridX": 4, "gridY": 7, "text": "Woody Re - Lloyd's Syndicate 3123", "fontSize": 13, "fill": "violet"},
        {"type": "text", "gridX": 4, "gridY": 10, "text": "Capacity: $75,000,000 | Trigger: Index (Industry Loss Index)", "fontSize": 10},
        {"type": "text", "gridX": 4, "gridY": 12.5, "text": "Attachment: $78,000,000,000 | Perils: storms, earthquakes, wildfires", "fontSize": 10},
        {"type": "text", "gridX": 4, "gridY": 15, "text": "Current index value: $70,200,000,000", "fontSize": 10, "fill": "grey"},
        {"type": "rectangle", "gridX": 2, "gridY": 19, "gridWidth": 46, "gridHeight": 5, "fill": "light-green", "stroke": "green"},
        {"type": "text", "gridX": 4, "gridY": 20.5, "text": "Distance to attachment: $7.8B (10% remaining)", "fontSize": 12, "fill": "green"},
        {"type": "button", "gridX": 2, "gridY": 27, "gridWidth": 14, "gridHeight": 3, "text": "Register New Bond", "fill": "violet"},
    ],
}

# 7. IndemnityLossAudit
designs["IndemnityLossAudit"] = {
    "semanticDescription": "Actuarial/Claims Audit Function's screen for auditing TFP's own incurred losses against an indemnity cat bond's trigger threshold.",
    "elements": [bg, *header("Indemnity Trigger Audit"),
        {"type": "text", "gridX": 2, "gridY": 6, "text": "Bond: [Indemnity-triggered example] | Covered perils: North American storms", "fontSize": 10.5, "fill": "grey"},
        {"type": "rectangle", "gridX": 2, "gridY": 10, "gridWidth": 46, "gridHeight": 11, "fill": "light-blue", "stroke": "grey"},
        {"type": "text", "gridX": 4, "gridY": 11, "text": "TFP incurred losses (ClaimPaid + ClaimReserveSet), covered scope", "fontSize": 10, "fill": "blue"},
        {"type": "text", "gridX": 4, "gridY": 14, "text": "Total audited: $42,000,000", "fontSize": 12},
        {"type": "text", "gridX": 4, "gridY": 17, "text": "Trigger threshold: $50,000,000", "fontSize": 11, "fill": "grey"},
        {"type": "text", "gridX": 4, "gridY": 19.5, "text": "Distance to trigger: $8,000,000", "fontSize": 12, "fill": "green"},
        {"type": "button", "gridX": 2, "gridY": 25, "gridWidth": 14, "gridHeight": 3, "text": "Confirm Audit", "fill": "green"},
        {"type": "text", "gridX": 2, "gridY": 30, "text": "Never self-executing - requires explicit audit confirmation before any trigger event fires.", "fontSize": 9, "fill": "grey"},
    ],
}

for title, payload in designs.items():
    node_id = nodes[title]["nodeId"]
    status, body = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/images/{node_id}/sketch", payload)
    print(f"{title} sketch:", status)
    import uuid, time
    now = int(time.time() * 1000)
    desc_event = [{
        "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": node_id, "boardId": BOARD_ID,
        "timestamp": now, "changedAttributes": ["meta.description"],
        "meta": {"type": "SCREEN", "title": title, "description": payload["semanticDescription"]},
        "node": {"id": node_id, "data": {}},
    }]
    status2, body2 = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", desc_event)
    print(f"{title} description:", status2)
