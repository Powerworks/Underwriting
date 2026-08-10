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


now = int(time.time() * 1000)
events = []

# ---------------------------------------------------------------------------
# Step 1: fix the orphaned GrantUnderwriterAuthorityLimit command via cell drop
# ---------------------------------------------------------------------------
status, resp = call(
    "POST",
    f"/api/org/{ORG_ID}/boards/{BOARD_ID}/timelines/{CHAPTER_ID}/cells/ab474c62-bbd8-4fdd-be90-ac556640cba5/drop",
    {"nodeId": "2741c72c-22ef-485f-aca4-5d45c73b6a63", "nodeType": "COMMAND"},
)
print("drop orphaned command:", status, resp)

# ---------------------------------------------------------------------------
# Step 2: update existing nodes' fields/descriptions (node:changed)
# ---------------------------------------------------------------------------

# GrantCellAuthorityLimit command - richer scope per S0.1
events.append({
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "db520252-d61c-4a02-8383-6e56321f957a",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "COMMAND", "title": "GrantCellAuthorityLimit", "fields": [
        field("cellId", "String"),
        field("grantingParty", "String", example="Underwriting Governance / CUO"),
        field("sourceAgreementReference", "String", description="Which capacity provider agreement this derives from (e.g. Delegated Underwriting Authority Agreement). OPEN QUESTION: a cell backed by multiple providers may need multiple concurrent authority grants, one per provider relationship, rather than a single number - not yet resolved."),
        field("authorityScope", "Custom", subfields=[
            field("classesOfBusiness", "String", cardinality="List"), field("territory", "String"),
            field("maxLineSize", "Decimal"), field("maxAggregate", "Decimal"),
        ]),
        field("currency", "String"),
        field("effectiveDate", "Date"),
    ], "description": "S0.1: actor is Underwriting Governance or senior executive - reference/configuration data, not transactional flow. OPEN QUESTION: is this purely internal (TFP governance), or does it require the capacity provider's own sign-off captured as part of the event?"},
    "node": {"id": "db520252-d61c-4a02-8383-6e56321f957a", "data": {}},
})
events.append({
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "a64b8581-bd3e-4d68-bff7-2b734b269f98",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "CellAuthorityLimitGranted", "fields": [
        field("authorityLimitId", "UUID", idAttribute=True, generated=True),
        field("cellId", "String"),
        field("authorityScope", "Custom", subfields=[
            field("classesOfBusiness", "String", cardinality="List"), field("territory", "String"),
            field("maxLineSize", "Decimal"), field("maxAggregate", "Decimal"),
        ]),
        field("sourceAgreementReference", "String"),
        field("grantedBy", "String"),
        field("effectiveDate", "Date"),
        field("version", "Integer", generated=True, description="Versioned from the very first event, not bolted on later - this becomes the ceiling all underwriter-level grants within the cell must fit inside."),
        field("grantedAt", "DateTime", generated=True),
    ], "description": "S0.1: becomes the ceiling that all underwriter-level grants (S0.2) within that cell must fit inside."},
    "node": {"id": "a64b8581-bd3e-4d68-bff7-2b734b269f98", "data": {}},
})

# GrantUnderwriterAuthorityLimit command - add version/validation
events.append({
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "2741c72c-22ef-485f-aca4-5d45c73b6a63",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "COMMAND", "title": "GrantUnderwriterAuthorityLimit", "fields": [
        field("underwriterId", "String"),
        field("cellId", "String"),
        field("requestedScope", "Custom", subfields=[
            field("classesOfBusiness", "String", cardinality="List"), field("territory", "String"), field("maxLineSize", "Decimal"),
        ]),
        field("grantingAuthority", "String", description="Who's issuing this - Head of Class / Cell CUO."),
    ], "description": "S0.2/S0.3: Cell CUO granting authority to an individual underwriter, validated against the cell's own limit (S0.1). OPEN QUESTION: does validation need to account for other underwriters' existing grants - an aggregate pool per cell (sum of all underwriters can't exceed the cell's limit) vs. each underwriter's limit being independent (a per-transaction ceiling, not a shared pool)? Real domain question for governance stakeholders, not a technicality."},
    "node": {"id": "2741c72c-22ef-485f-aca4-5d45c73b6a63", "data": {}},
})
events.append({
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "c6c2750c-015f-4844-a4dd-99e614d4f444",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "UnderwriterAuthorityLimitGranted", "fields": [
        field("authorityLimitId", "UUID", idAttribute=True, generated=True),
        field("underwriterId", "String"),
        field("cellId", "String"),
        field("scope", "Custom", subfields=[
            field("classesOfBusiness", "String", cardinality="List"), field("territory", "String"), field("maxLineSize", "Decimal"),
        ]),
        field("grantedBy", "String"),
        field("effectiveDate", "Date"),
        field("version", "Integer", generated=True),
        field("validationResult", "String", example="within cell's own limit"),
        field("grantedAt", "DateTime", generated=True),
    ], "description": "S0.2: this is the record AssessSubmission (Context 2) checks against, via AuthorityMatrix."},
    "node": {"id": "c6c2750c-015f-4844-a4dd-99e614d4f444", "data": {}},
})
events.append({
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "5150f8ee-f63d-4a3f-a920-433551793b6b",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "UnderwriterAuthorityLimitRejected", "fields": [
        field("underwriterId", "String"),
        field("cellId", "String"),
        field("requestedScope", "Custom"),
        field("rejectionReason", "String", example="exceeds cell ceiling by $X"),
        field("attemptedBy", "String"),
        field("rejectedAt", "DateTime", generated=True),
    ], "description": "S0.3: requested delegation would exceed the cell's own limit. OPEN QUESTION: does rejection trigger escalation (a request to increase the cell's own overall limit, see CellAuthorityIncreaseRequested) or dead-end requiring the Cell CUO to reallocate existing underwriters' authority instead? Modeled the escalation path explicitly rather than leaving a pure dead end - see CellAuthorityIncreaseRequested."},
    "node": {"id": "5150f8ee-f63d-4a3f-a920-433551793b6b", "data": {}},
})

# authority-limit-view -> rename to AuthorityMatrix (their exact terminology)
events.append({
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "e5c55c48-3358-4529-b164-7279d821fdf0",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.title", "meta.description"],
    "meta": {"type": "READMODEL", "title": "AuthorityMatrix", "fields": [
        field("authorityLimitId", "UUID", idAttribute=True),
        field("tier", "String", example="Underwriter"),
        field("providerId", "String", optional=True),
        field("cellId", "String"),
        field("underwriterId", "String", optional=True),
        field("classOfBusiness", "String"),
        field("maxGrossPremium", "Decimal"),
        field("maxLimit", "Decimal"),
        field("currency", "String"),
        field("status", "String", example="Active"),
        field("version", "Integer"),
        field("grantedBy", "String", optional=True),
        field("grantedAt", "DateTime"),
    ], "description": "The view Context 2's AssessSubmission actually queries at decision time. Renamed from an earlier generic 'AuthorityLimit' readmodel to match the terminology used once Context 0 was detailed - same underlying projection."},
    "node": {"id": "e5c55c48-3358-4529-b164-7279d821fdf0", "data": {}},
})

# AuthorityLimitRevised - add version increment, immediacy consideration
events.append({
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "457b70c8-2593-409f-bef6-977490d37e74",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "AuthorityLimitRevised", "fields": [
        field("authorityLimitId", "UUID", idAttribute=True),
        field("target", "String", example="cell | underwriter"),
        field("previousLimit", "Custom"),
        field("newLimit", "Custom"),
        field("reason", "String"),
        field("revisedBy", "String"),
        field("effectiveDate", "Date"),
        field("version", "Integer", generated=True, description="Incremented on each revision."),
        field("revisedAt", "DateTime", generated=True),
    ], "description": "S0.4: sharpest open question in this context - a submission already in-flight (post-1a, pre-bind), assessed against the old limit, when the revision drops below what it needs. Grandfather (evaluate against authority in effect when it entered decisioning) vs re-evaluate (immediately re-check, force referral if it now breaches) are both defensible; leaning toward re-evaluation as the safer default for a governance-critical system, but this is a business decision, not an architecture default. Either way, always triggers ReassessInFlightSubmissionsOnRuleChange producing InFlightSubmissionReassessed, so the decision and its trigger are visible in the audit trail regardless of which policy stance is chosen. Same underlying 'rules changed mid-flight' problem as S1e.2's freeze-during-decisioning - solved with the same mechanism, not two bespoke ones."},
    "node": {"id": "457b70c8-2593-409f-bef6-977490d37e74", "data": {}},
})

# AuthorityLimitRevoked - add immediacy field
events.append({
    "id": str(uuid.uuid4()), "eventType": "node:changed", "nodeId": "20775210-7cf3-4f1a-9970-ee1c6b03c5cd",
    "boardId": BOARD_ID, "timestamp": now, "changedAttributes": ["meta.fields", "meta.description"],
    "meta": {"type": "EVENT", "title": "AuthorityLimitRevoked", "fields": [
        field("authorityLimitId", "UUID", idAttribute=True),
        field("target", "String", example="underwriter (cell-level revocation would be a much bigger event - effectively withdrawing a cell's ability to write anything)"),
        field("priorLimit", "Custom", description="Kept for record."),
        field("reason", "String"),
        field("revokedBy", "String"),
        field("immediacy", "String", example="immediate", description="Revocation correlates with higher-risk situations (suspension, termination) - pushed for the stricter, immediate interpretation here specifically, even though S0.4's routine revisions can be more relaxed (checked at next decision point)."),
        field("revokedAt", "DateTime", generated=True),
    ], "description": "S0.5: an underwriter with zero authority cannot have any submission proceed under their name, referral cascade or not - same in-flight question as S0.4 with sharper urgency, triggers ReassessInFlightSubmissionsOnRuleChange immediately. OPEN QUESTION: does revocation trigger a review flag on their recently bound business, similar to a thematic file review trigger? Not modeled as its own event yet - flagged for confirmation."},
    "node": {"id": "20775210-7cf3-4f1a-9970-ee1c6b03c5cd", "data": {}},
})

status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print("update existing nodes:", status)
print(json.dumps(resp, indent=2)[:800])
