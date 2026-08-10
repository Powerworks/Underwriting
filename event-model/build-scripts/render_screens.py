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


with open("screen_node_ids.json") as f:
    nodes = json.load(f)

bg = {"type": "rectangle", "gridX": 0, "gridY": 0, "gridWidth": 50, "gridHeight": 40, "fill": "white"}


def header(title):
    return [
        {"type": "rectangle", "gridX": 0, "gridY": 0, "gridWidth": 50, "gridHeight": 3, "fill": "violet"},
        {"type": "headline", "gridX": 2, "gridY": 1, "text": title, "fontSize": 16, "fill": "white"},
    ]


# --- 1. SubmissionQueue ---
submission_queue = {
    "semanticDescription": "Underwriter's Submission Queue - worklist of received/normalized submissions with status and duplicate badges.",
    "elements": [bg, *header("Submission Queue - itasca-mga"),
        {"type": "input", "gridX": 32, "gridY": 4, "gridWidth": 16, "gridHeight": 2, "text": "Search submissions..."},
        {"type": "text", "gridX": 2, "gridY": 7, "text": "Insured", "fontSize": 11, "fill": "grey"},
        {"type": "text", "gridX": 15, "gridY": 7, "text": "Class", "fontSize": 11, "fill": "grey"},
        {"type": "text", "gridX": 27, "gridY": 7, "text": "Line Size", "fontSize": 11, "fill": "grey"},
        {"type": "text", "gridX": 36, "gridY": 7, "text": "Received", "fontSize": 11, "fill": "grey"},
        {"type": "text", "gridX": 43, "gridY": 7, "text": "Status", "fontSize": 11, "fill": "grey"},
        {"type": "line", "gridX": 1, "gridY": 9, "gridX2": 49, "gridY2": 9, "stroke": "grey"},

        {"type": "text", "gridX": 2, "gridY": 11, "text": "Acme Aviation Leasing Ltd", "fontSize": 11},
        {"type": "text", "gridX": 15, "gridY": 11, "text": "Aircraft Non-Payment Credit", "fontSize": 10},
        {"type": "text", "gridX": 27, "gridY": 11, "text": "$10.0M", "fontSize": 11},
        {"type": "text", "gridX": 36, "gridY": 11, "text": "09:14 today", "fontSize": 10},
        {"type": "rectangle", "gridX": 42, "gridY": 10.5, "gridWidth": 7, "gridHeight": 2.2, "fill": "light-green"},
        {"type": "text", "gridX": 42.5, "gridY": 11, "text": "Ready", "fontSize": 9, "fill": "green"},
        {"type": "line", "gridX": 1, "gridY": 14, "gridX2": 49, "gridY2": 14, "stroke": "light-grey"},

        {"type": "text", "gridX": 2, "gridY": 16, "text": "Meridian Port Holdings", "fontSize": 11},
        {"type": "text", "gridX": 15, "gridY": 16, "text": "Property (Cat-exposed)", "fontSize": 10},
        {"type": "text", "gridX": 27, "gridY": 16, "text": "$25.0M", "fontSize": 11},
        {"type": "text", "gridX": 36, "gridY": 16, "text": "Yesterday", "fontSize": 10},
        {"type": "rectangle", "gridX": 41, "gridY": 15.5, "gridWidth": 9, "gridHeight": 2.2, "fill": "orange"},
        {"type": "text", "gridX": 41.5, "gridY": 16, "text": "Possible Dup", "fontSize": 8.5, "fill": "white"},
        {"type": "line", "gridX": 1, "gridY": 19, "gridX2": 49, "gridY2": 19, "stroke": "light-grey"},

        {"type": "text", "gridX": 2, "gridY": 21, "text": "Blackstone Grid Energy LLC", "fontSize": 11},
        {"type": "text", "gridX": 15, "gridY": 21, "text": "Energy Liability", "fontSize": 10},
        {"type": "text", "gridX": 27, "gridY": 21, "text": "$8.5M", "fontSize": 11},
        {"type": "text", "gridX": 36, "gridY": 21, "text": "2 days ago", "fontSize": 10},
        {"type": "rectangle", "gridX": 42, "gridY": 20.5, "gridWidth": 7, "gridHeight": 2.2, "fill": "light-green"},
        {"type": "text", "gridX": 42.5, "gridY": 21, "text": "Ready", "fontSize": 9, "fill": "green"},

        {"type": "rectangle", "gridX": 0, "gridY": 36, "gridWidth": 50, "gridHeight": 4, "fill": "light-violet"},
        {"type": "text", "gridX": 2, "gridY": 37.5, "text": "3 submissions - 1 flagged as possible duplicate", "fontSize": 10, "fill": "grey"},
    ],
}
visual_desc_1 = ("Underwriter's Submission Queue screen: a violet header bar reads 'Submission Queue - itasca-mga' with a search input top-right. "
    "Below is a column-header row (Insured, Class, Line Size, Received, Status) over a divider line, followed by three submission rows: "
    "Acme Aviation Leasing Ltd ($10.0M, Aircraft Non-Payment Credit) with a green 'Ready' badge; Meridian Port Holdings ($25.0M, cat-exposed Property) "
    "with an orange 'Possible Dup' badge; and Blackstone Grid Energy LLC ($8.5M, Energy Liability) with a green 'Ready' badge. "
    "A light-violet footer bar summarizes '3 submissions - 1 flagged as possible duplicate'.")

# --- 2. SubmissionAssessment ---
submission_assessment = {
    "semanticDescription": "Submission Assessment - underwriter reviews proposed terms against their authority limit.",
    "elements": [bg, *header("Assess Submission - Acme Aviation Leasing Ltd"),
        {"type": "rectangle", "gridX": 2, "gridY": 5, "gridWidth": 22, "gridHeight": 20, "fill": "light-blue", "stroke": "grey"},
        {"type": "text", "gridX": 4, "gridY": 6.5, "text": "SUBMISSION", "fontSize": 10, "fill": "blue"},
        {"type": "text", "gridX": 4, "gridY": 10, "text": "Class: Aircraft Non-Payment Credit", "fontSize": 10.5},
        {"type": "text", "gridX": 4, "gridY": 13, "text": "Territory: US / Bermuda", "fontSize": 10.5},
        {"type": "text", "gridX": 4, "gridY": 16, "text": "Proposed line size: $10,000,000", "fontSize": 10.5},
        {"type": "text", "gridX": 4, "gridY": 19, "text": "Proposed premium: $185,000", "fontSize": 10.5},
        {"type": "text", "gridX": 4, "gridY": 22, "text": "Effective: 2026-09-01", "fontSize": 10.5},

        {"type": "rectangle", "gridX": 26, "gridY": 5, "gridWidth": 22, "gridHeight": 20, "fill": "light-green", "stroke": "grey"},
        {"type": "text", "gridX": 28, "gridY": 6.5, "text": "YOUR AUTHORITY (v3)", "fontSize": 10, "fill": "green"},
        {"type": "text", "gridX": 28, "gridY": 10, "text": "Limit: $250,000 / $15,000,000", "fontSize": 10.5},
        {"type": "text", "gridX": 28, "gridY": 13, "text": "Proposed: $185,000 / $10,000,000", "fontSize": 10.5},
        {"type": "circle", "gridX": 29, "gridY": 17, "gridRadius": 1, "fill": "green"},
        {"type": "text", "gridX": 31, "gridY": 16.3, "text": "Within authority", "fontSize": 11, "fill": "green"},
        {"type": "text", "gridX": 28, "gridY": 20, "text": "Freeze check: passed (no active", "fontSize": 9.5, "fill": "grey"},
        {"type": "text", "gridX": 28, "gridY": 22, "text": "freeze on this territory/class)", "fontSize": 9.5, "fill": "grey"},

        {"type": "button", "gridX": 26, "gridY": 30, "gridWidth": 10, "gridHeight": 3, "text": "Approve & Quote", "fill": "green"},
        {"type": "button", "gridX": 38, "gridY": 30, "gridWidth": 10, "gridHeight": 3, "text": "Decline", "fill": "light-red"},
    ],
}
visual_desc_2 = ("Submission Assessment screen: violet header 'Assess Submission - Acme Aviation Leasing Ltd'. Two side-by-side panels below: "
    "a light-blue Submission panel (left) listing class, territory, proposed line size ($10,000,000), premium ($185,000), and effective date; "
    "a light-green Your Authority panel (right, versioned v3) showing the underwriter's limit ($250,000 / $15,000,000) against the proposed terms, "
    "a green checkmark reading 'Within authority', and a note that the freeze check passed. Two buttons at the bottom: green 'Approve & Quote' "
    "and light-red 'Decline'.")

# --- 3. QuoteView ---
quote_view = {
    "semanticDescription": "Broker's Quote screen - view issued quote, accept, decline, or request amendment.",
    "elements": [bg, *header("Quote QT-2026-0912 - Howden"),
        {"type": "rectangle", "gridX": 6, "gridY": 6, "gridWidth": 38, "gridHeight": 18, "fill": "light-violet", "stroke": "grey"},
        {"type": "text", "gridX": 8, "gridY": 8, "text": "Insured: Acme Aviation Leasing Ltd", "fontSize": 12},
        {"type": "text", "gridX": 8, "gridY": 11, "text": "Class: Aircraft Non-Payment Credit", "fontSize": 12},
        {"type": "text", "gridX": 8, "gridY": 14, "text": "Line size: $10,000,000", "fontSize": 12},
        {"type": "headline", "gridX": 8, "gridY": 17, "text": "Premium: $185,000", "fontSize": 15, "fill": "violet"},
        {"type": "text", "gridX": 8, "gridY": 21, "text": "Valid until: 2026-09-15 (14 days)", "fontSize": 10.5, "fill": "grey"},

        {"type": "button", "gridX": 6, "gridY": 27, "gridWidth": 11, "gridHeight": 3, "text": "Accept Quote", "fill": "green"},
        {"type": "button", "gridX": 19, "gridY": 27, "gridWidth": 13, "gridHeight": 3, "text": "Request Amendment", "fill": "yellow"},
        {"type": "button", "gridX": 34, "gridY": 27, "gridWidth": 10, "gridHeight": 3, "text": "Decline", "fill": "light-red"},
    ],
}
visual_desc_3 = ("Broker's Quote screen: violet header 'Quote QT-2026-0912 - Howden'. A central light-violet card shows the insured (Acme Aviation "
    "Leasing Ltd), class (Aircraft Non-Payment Credit), line size ($10,000,000), a large premium figure ($185,000), and a validity note "
    "('Valid until 2026-09-15, 14 days'). Three buttons below the card: green 'Accept Quote', yellow 'Request Amendment', and light-red 'Decline'.")

# --- 4. ClaimHandling ---
claim_handling = {
    "semanticDescription": "Claims handler screen - notify a claim, view policy origination lineage, set reserve, pay.",
    "elements": [bg, *header("Claim CL-2026-0447"),
        {"type": "rectangle", "gridX": 2, "gridY": 5, "gridWidth": 46, "gridHeight": 9, "fill": "light-blue", "stroke": "grey"},
        {"type": "text", "gridX": 4, "gridY": 6, "text": "POLICY ORIGINATION LINEAGE (from Search & Retrieval)", "fontSize": 9.5, "fill": "blue"},
        {"type": "text", "gridX": 4, "gridY": 9, "text": "Submission received 2026-08-10 -> Within authority -> Quote issued -> Bound 2026-09-01", "fontSize": 10},
        {"type": "text", "gridX": 4, "gridY": 12, "text": "Bind terms: $10,000,000 limit / $185,000 premium, no sub-limit exclusions on file", "fontSize": 10},

        {"type": "text", "gridX": 4, "gridY": 17, "text": "Date of loss: 2026-11-03", "fontSize": 11},
        {"type": "text", "gridX": 4, "gridY": 20, "text": "Description: Engine lease non-payment following lessee default", "fontSize": 11},

        {"type": "rectangle", "gridX": 4, "gridY": 24, "gridWidth": 20, "gridHeight": 3, "fill": "white", "stroke": "grey"},
        {"type": "text", "gridX": 5, "gridY": 24.8, "text": "Reserve amount", "fontSize": 9, "fill": "grey"},
        {"type": "text", "gridX": 5, "gridY": 26.2, "text": "$4,500,000", "fontSize": 12},

        {"type": "circle", "gridX": 27, "gridY": 25.5, "gridRadius": 0.8, "fill": "green"},
        {"type": "text", "gridX": 29, "gridY": 25, "text": "Validated against bind terms", "fontSize": 10, "fill": "green"},

        {"type": "button", "gridX": 4, "gridY": 32, "gridWidth": 11, "gridHeight": 3, "text": "Set Reserve", "fill": "light-blue"},
        {"type": "button", "gridX": 17, "gridY": 32, "gridWidth": 11, "gridHeight": 3, "text": "Pay Claim", "fill": "green"},
        {"type": "button", "gridX": 30, "gridY": 32, "gridWidth": 11, "gridHeight": 3, "text": "Close Claim", "fill": "grey"},
    ],
}
visual_desc_4 = ("Claims handler screen: violet header 'Claim CL-2026-0447'. A light-blue Policy Origination Lineage panel at top (pulled from "
    "Search & Retrieval) shows the submission-to-bind chain and the original bind terms ($10,000,000 limit / $185,000 premium). Below, claim "
    "details show date of loss and a loss description. A reserve-amount field shows $4,500,000 next to a green checkmark reading 'Validated "
    "against bind terms'. Three buttons at the bottom: light-blue 'Set Reserve', green 'Pay Claim', and grey 'Close Claim'.")


designs = [
    ("SubmissionQueue", submission_queue, visual_desc_1),
    ("SubmissionAssessment", submission_assessment, visual_desc_2),
    ("QuoteView", quote_view, visual_desc_3),
    ("ClaimHandling", claim_handling, visual_desc_4),
]

for title, payload, visual_desc in designs:
    node_id = nodes[title]["nodeId"]
    status, body = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/images/{node_id}/sketch", payload)
    print(f"{title} sketch:", status, body[:150])
    status2, body2 = call("PATCH", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/{node_id}",
                           {"meta": {"description": visual_desc}})
    print(f"{title} description patch:", status2, body2[:150])
