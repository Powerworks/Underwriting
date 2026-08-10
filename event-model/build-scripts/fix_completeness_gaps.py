import json
import time
import urllib.request
import uuid

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"
HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "completeness-fix"}


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
        "name": name, "type": type_, "cardinality": opts.get("cardinality", "Single"),
        "optional": opts.get("optional", False), "idAttribute": opts.get("idAttribute", False),
        "generated": opts.get("generated", False), "edited": opts.get("edited", True),
        "query": opts.get("query", False), "showAttributes": opts.get("showAttributes", False),
        "technicalAttribute": opts.get("technicalAttribute", False), "subfields": opts.get("subfields", []),
    }


now = int(time.time() * 1000)

# GAP-001 fix: BordereauDrafted gets a 'lines' list with generated lineId per line
line_subfields = [
    field("lineId", "UUID", generated=True),
    field("policyTransactionId", "UUID"),
    field("transactionType", "String", example="new business"),
    field("grossPremium", "Decimal"),
    field("netPremium", "Decimal"),
    field("providerShare", "Decimal"),
]
evt1 = {
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "1bacdc7c-b263-47ad-909f-6cc7b91ef689",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "BordereauDrafted", "fields": [
        field("bordereauId", "UUID", idAttribute=True, generated=True),
        field("cellId", "String"), field("providerId", "String"), field("periodStart", "Date"), field("periodEnd", "Date"),
        field("lines", "Custom", cardinality="List", subfields=line_subfields,
              description="Each swept transaction gets a generated lineId here - fixes GAP-001 (completeness check): no event previously assigned individual bordereau line identifiers, even though RaiseBordereauQuery/BordereauLineQueried/RaisePriorPeriodAdjustment all reference one."),
        field("lineCount", "Integer", generated=True, description="Derived count, kept for quick display - the lines list is now the source of truth."),
        field("totalNetPremium", "Decimal"), field("currency", "String"), field("status", "String", example="Draft"),
        field("draftedAt", "DateTime", generated=True),
    ], "description": "S4.1: draft bordereau created for a cell+provider+period. Becomes a real financial obligation only once agreed (see AgreeBordereau). Each line carries its own generated lineId (fixed per completeness check GAP-001)."},
    "node": {"id": "1bacdc7c-b263-47ad-909f-6cc7b91ef689", "data": {}},
}
print("BordereauDrafted fix:", call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", [evt1]))

# GAP-002 fix: ExposureConcentrationWarningRaised gets a generated warningId
evt2 = {
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "21532fa8-5f54-4c0f-bbe9-60663d728002",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "ExposureConcentrationWarningRaised", "fields": [
        field("warningId", "UUID", idAttribute=True, generated=True,
              description="Fixes GAP-002 (completeness check): AcknowledgeExposureConcentrationWarning always required a warningId but no event previously generated one."),
        field("geocode", "String"), field("perilCategory", "String"),
        field("contributingCells", "Custom", cardinality="List"),
        field("combinedExposureValue", "Decimal"), field("thresholdBreached", "Decimal"), field("severityLevel", "String"),
        field("raisedAt", "DateTime", generated=True),
    ], "description": "S1c.2: fires when a new projection update crosses a configured concentration threshold. OPEN QUESTION: threshold ownership (domain-expert-owned). HAND-OFF NOTE: natural hand-off point into future Context 1e (Portfolio Governance). warningId added per completeness check GAP-002."},
    "node": {"id": "21532fa8-5f54-4c0f-bbe9-60663d728002", "data": {}},
}
print("ExposureConcentrationWarningRaised fix:", call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", [evt2]))
