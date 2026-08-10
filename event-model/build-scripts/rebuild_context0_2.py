import json
import time
import urllib.request
import uuid

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"
CHAPTER_ID = "e96f1046-4ab4-4654-89f3-8074c0aa06f4"
SWIMLANE_ROW = "05b234f6-6f03-48fd-9ddd-9d0f4b0f316b"
INTERACTION_ROW = "e98ea909-683d-465b-be6c-cd6596b4e0cd"
ACTOR_ROW = "1af53c6a-6c34-4cd3-987c-7ac085fe0cf0"

HEADERS = {
    "Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "timeline-skill",
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
        "name": name, "type": type_, "cardinality": opts.get("cardinality", "Single"),
        "optional": opts.get("optional", False), "idAttribute": opts.get("idAttribute", False),
        "generated": opts.get("generated", False), "edited": opts.get("edited", True),
        "query": opts.get("query", False), "showAttributes": opts.get("showAttributes", False),
        "technicalAttribute": opts.get("technicalAttribute", False), "subfields": opts.get("subfields", []),
    }


col = {
    "CellAuthorityRegister": "7f78b9e4-bb84-46ff-ad82-79829de67836",
    "CellAuthorityIncreaseRequested": "8e0367e7-3820-4838-ba51-e711ca7e9f72",
    "UnderwriterAuthorityRegister": "bb67d785-acbf-4069-adb3-a50c2c0305c0",
    "InFlightSubmissionReassessed": "800c64d6-2e73-4854-8f51-de4b52255c54",
}

now = int(time.time() * 1000)
events = []


def cell(col_label, row_id):
    return f"{row_id}-{col[col_label]}"


def add_node(node_type, title, col_label, row_id, fields_list, description):
    node_id = str(uuid.uuid4())
    events.append({
        "id": str(uuid.uuid4()), "eventType": "node:created", "nodeId": node_id, "boardId": BOARD_ID,
        "timestamp": now, "chapterId": CHAPTER_ID, "cellId": cell(col_label, row_id),
        "meta": {"type": node_type, "title": title, "fields": fields_list, "description": description},
        "node": {"id": node_id, "data": {"title": title}},
    })
    return node_id


add_node(
    "EVENT", "CellAuthorityLimitGranted", "CellAuthorityRegister", SWIMLANE_ROW,
    [field("authorityLimitId", "UUID", idAttribute=True, generated=True), field("cellId", "String"), field("version", "Integer", generated=True)],
    "Second instance of the same event type, paired here with the register projection it feeds.",
)
add_node(
    "READMODEL", "CellAuthorityRegister", "CellAuthorityRegister", INTERACTION_ROW,
    [
        field("cellId", "String", idAttribute=True),
        field("currentScope", "Custom", subfields=[
            field("classesOfBusiness", "String", cardinality="List"), field("territory", "String"),
            field("maxLineSize", "Decimal"), field("maxAggregate", "Decimal"),
        ]),
        field("currentVersion", "Integer"),
        field("effectiveDate", "Date"),
        field("history", "Custom", cardinality="List", subfields=[
            field("version", "Integer"), field("scope", "Custom"), field("effectiveDate", "Date"), field("supersededAt", "DateTime", optional=True),
        ]),
    ],
    "S0.1: current and historical authority limits per cell, queryable at any point in time.",
)

add_node(
    "COMMAND", "RequestCellAuthorityIncrease", "CellAuthorityIncreaseRequested", INTERACTION_ROW,
    [field("cellId", "String"), field("requestedBy", "String"), field("currentLimit", "Custom"), field("requestedLimit", "Custom"), field("justification", "String")],
    "Escalation path from S0.3's rejection - modeled explicitly rather than a pure dead end, since in practice someone needs to know 'how do we actually get this underwriter the authority they need.'",
)
add_node(
    "EVENT", "CellAuthorityIncreaseRequested", "CellAuthorityIncreaseRequested", SWIMLANE_ROW,
    [field("cellId", "String"), field("requestedBy", "String"), field("currentLimit", "Custom"), field("requestedLimit", "Custom"), field("justification", "String"), field("requestedAt", "DateTime", generated=True)],
    "S0.3 follow-on: loops back into a variant of S0.1 (GrantCellAuthorityLimit / AuthorityLimitRevised) if approved by the capacity provider/governance - the request itself is the auditable fact, distinct from whatever decision follows.",
)

add_node(
    "EVENT", "UnderwriterAuthorityLimitGranted", "UnderwriterAuthorityRegister", SWIMLANE_ROW,
    [field("authorityLimitId", "UUID", idAttribute=True, generated=True), field("underwriterId", "String"), field("version", "Integer", generated=True)],
    "Second instance of the same event type, paired here with the register projection it feeds (distinct from AuthorityMatrix, which is what Context 2 queries at decision time - this is the underwriter-facing historical register).",
)
add_node(
    "READMODEL", "UnderwriterAuthorityRegister", "UnderwriterAuthorityRegister", INTERACTION_ROW,
    [
        field("underwriterId", "String", idAttribute=True),
        field("cellId", "String"),
        field("currentScope", "Custom"),
        field("currentVersion", "Integer"),
        field("effectiveDate", "Date"),
        field("history", "Custom", cardinality="List", subfields=[
            field("version", "Integer"), field("scope", "Custom"), field("effectiveDate", "Date"), field("supersededAt", "DateTime", optional=True),
        ]),
    ],
    "S0.2: current authority per underwriter, with full history - distinct from AuthorityMatrix (the decision-time query view).",
)

add_node(
    "EVENT", "InFlightSubmissionReassessed", "InFlightSubmissionReassessed", SWIMLANE_ROW,
    [
        field("submissionId", "UUID"),
        field("triggerType", "String", example="authority-revised | authority-revoked | portfolio-freeze", description="Shared mechanism: the same underlying 'rules changed mid-flight' problem shows up as S0.4/S0.5's authority-change case AND S1e.2's freeze-during-decisioning case - one event type, distinguished by trigger, rather than two bespoke solutions."),
        field("triggerReference", "UUID", description="The AuthorityLimitRevised/Revoked or TerritoryUnderwritingFrozen event that caused this reassessment."),
        field("previousBasis", "Custom", description="Authority/freeze state the submission was originally being evaluated against."),
        field("newBasis", "Custom", description="Authority/freeze state after the change."),
        field("policyApplied", "String", example="re-evaluated", description="grandfathered | re-evaluated - which stance governance has configured. Recorded explicitly so the decision and its trigger are both visible in the audit trail regardless of which policy stance TFP chooses - not defaulted silently in the architecture."),
        field("reassessmentResult", "String", example="still-within-authority | now-referred | now-blocked"),
        field("reassessedAt", "DateTime", generated=True),
    ],
    "S0.4/S0.5's sharpest open question, generalized: what happens to a submission already in-flight when the rules it's being evaluated against change underneath it. Leaning toward re-evaluation as the safer default for a governance-critical system, but this is a business decision (policyApplied), not an architecture default. Revocation (S0.5) pushes for the stricter, immediate interpretation given it correlates with higher-risk situations; routine revisions (S0.4) may be more relaxed (checked at next decision point).",
)
add_node(
    "AUTOMATION", "ReassessInFlightSubmissionsOnRuleChange", "InFlightSubmissionReassessed", ACTOR_ROW,
    [],
    "Triggered by AuthorityLimitRevised, AuthorityLimitRevoked (Context 0), and TerritoryUnderwritingFrozen (Context 1e) - one shared policy for 'rules changed mid-flight', not three separate reactive processes solving the same problem differently.",
)

print(f"Total node operations: {len(events)}")
status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print(status)
print(json.dumps(resp, indent=2)[:1000])
