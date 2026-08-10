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

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "pricing-catbond"}


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
    "BaselinePremiumGenerated": "36a2a1c4-3c9d-4981-815b-3b99c2ee064f",
    "PricingBaselineAccepted": "ff330bac-5b18-4776-a642-c25106442335",
    "PricingBaselineOverridden": "817b1197-8f82-476f-a988-85ab41f86403",
    "PricingModelVersionDeployed": "ae40e063-69e7-445a-a0fb-8fc884428a7b",
    "CatBondRegistered": "36ab7a12-4d6b-4f2f-8c1c-26688030334a",
    "IndustryLossIndexUpdated": "9cc03a9e-48a0-4416-8750-605d93e72af9",
    "CatBondAttachmentDistanceUpdated": "5337bc1b-9e8e-4b6b-8461-c4c52fcc4363",
    "CatBondTriggered": "f0c8052f-9c6c-4769-b619-5fb735767da1",
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


# ===========================================================================
# S1a.5 AI Baseline Pricing (extends Submission Intake)
# ===========================================================================

add_node("EVENT", "BaselinePremiumGenerated", "BaselinePremiumGenerated", SWIMLANE_ROW,
    [
        field("submissionId", "UUID"),
        field("baselinePremium", "Decimal"),
        field("riskFactorSummary", "Custom", description="Which alternative data points contributed (weather trend flag, satellite imagery flag, historical loss pattern summary) - not the model's internals."),
        field("modelVersion", "String"),
        field("generatedAt", "DateTime", generated=True),
    ],
    "S1a.5: fires only on successful SubmissionNormalized - an incomplete/failed normalization shouldn't get a baseline price against partial data. Rating computation (EBM segmentation, satellite/weather cross-referencing) is a specialist capability this system requests and displays, never computes - same boundary discipline as ExportExposureExtract (1c.5) and PMLRecalculated (1d.2).")
add_node("AUTOMATION", "GenerateBaselinePremiumOnNormalization", "BaselinePremiumGenerated", ACTOR_ROW, [],
    "Trigger: SubmissionNormalized (success only).")
add_node("READMODEL", "PricedSubmissionView", "BaselinePremiumGenerated", INTERACTION_ROW,
    [
        field("submissionId", "UUID", idAttribute=True),
        field("brokerRequestedTerms", "Custom"),
        field("baselinePremium", "Decimal"),
        field("riskFactorSummary", "Custom"),
        field("modelVersion", "String"),
    ],
    "The broker's ask alongside the AI baseline, side by side, before the underwriter opens the file - this is what makes assessment 'auditing the AI-generated model' rather than pricing from scratch.")

add_node("EVENT", "PricingBaselineAccepted", "PricingBaselineAccepted", SWIMLANE_ROW,
    [
        field("submissionId", "UUID", idAttribute=True),
        field("baselinePremium", "Decimal"),
        field("underwriterId", "String"),
        field("acceptedAt", "DateTime", generated=True),
    ],
    "Fires alongside AssessSubmission (S2.1) when the underwriter's proposedTerms match BaselinePremiumGenerated exactly. Actuarial needs to measure how often/how much underwriters deviate from the model to evaluate model performance - invisible unless captured explicitly, so this and PricingBaselineOverridden are modeled even though they add no new business decision by themselves.")
add_node("EVENT", "PricingBaselineOverridden", "PricingBaselineOverridden", SWIMLANE_ROW,
    [
        field("submissionId", "UUID", idAttribute=True),
        field("baselinePremium", "Decimal"),
        field("proposedPremium", "Decimal"),
        field("variance", "Decimal", description="proposedPremium - baselinePremium, signed."),
        field("underwriterId", "String"),
        field("overriddenAt", "DateTime", generated=True),
    ],
    "Fires alongside AssessSubmission when the underwriter's proposedTerms diverge from the baseline. Same model-feedback rationale as PricingBaselineAccepted.")

add_node("EVENT", "PricingModelVersionDeployed", "PricingModelVersionDeployed", SWIMLANE_ROW,
    [
        field("modelVersion", "String"),
        field("deployedBy", "String", description="External rating engine / actuarial team - reference data, TFP records which version priced which submission but doesn't govern the model's content."),
        field("deployedAt", "DateTime", generated=True),
        field("changeSummary", "String", optional=True, example="inflation adjustment, Q3 territory re-weighting"),
    ],
    "Rapid model deployment ('quarters to hours' per TFP's own materials) means BaselinePremiumGenerated.modelVersion needs a real referent - this is that referent, audit reference data only.")

# ===========================================================================
# 1f. Capital & Reinsurance Instruments (exploratory)
# ===========================================================================

add_node("COMMAND", "RegisterCatBond", "CatBondRegistered", INTERACTION_ROW,
    [
        field("bondName", "String", example="Woody Re"),
        field("coveredSyndicate", "String", example="Lloyd's Syndicate 3123"),
        field("capacity", "Decimal", example="75000000"),
        field("triggerType", "String", example="Industry Loss Index"),
        field("attachmentPoint", "Decimal", example="78000000000"),
        field("coveredPerils", "String", cardinality="List", example="North American storms, earthquakes, wildfires"),
        field("interestRate", "Decimal", optional=True, example="8.25"),
        field("termStart", "Date"), field("termEnd", "Date"),
    ],
    "Capital markets / outwards reinsurance team - the same TFP function HedgingActionRequested (S1e.5) already hands off to.")
add_node("EVENT", "CatBondRegistered", "CatBondRegistered", SWIMLANE_ROW,
    [
        field("bondId", "UUID", idAttribute=True, generated=True),
        field("bondName", "String"), field("coveredSyndicate", "String"), field("capacity", "Decimal"),
        field("triggerType", "String"), field("attachmentPoint", "Decimal"),
        field("coveredPerils", "String", cardinality="List"),
        field("interestRate", "Decimal", optional=True), field("termStart", "Date"), field("termEnd", "Date"),
        field("registeredAt", "DateTime", generated=True),
    ],
    "S1f.1: reference/capital data, same discipline as Context 0's authority grants - this system tracks the bond's terms, doesn't issue or legally manage it.")

add_node("EVENT", "IndustryLossIndexUpdated", "IndustryLossIndexUpdated", SWIMLANE_ROW,
    [
        field("indexProvider", "String", example="PCS-style industry loss index"),
        field("currentIndexValue", "Decimal"),
        field("periodCovered", "String", optional=True),
        field("updatedAt", "DateTime", generated=True),
    ],
    "S1f.2: a second external feed, structurally parallel to StormTrackUpdateReceived (S1d.1) - tracks industry-WIDE aggregated catastrophe losses, not TFP's own modeled PML. Same staleness-handling concern applies (no accountable sender if an update goes missing) - not modeled as its own event yet, flagged as a follow-up matching ThreatFeedStale's pattern.")

add_node("EVENT", "CatBondAttachmentDistanceUpdated", "CatBondAttachmentDistanceUpdated", SWIMLANE_ROW,
    [
        field("bondId", "UUID"),
        field("currentIndexValue", "Decimal"),
        field("attachmentPoint", "Decimal"),
        field("distanceToAttachment", "Decimal", description="attachmentPoint - currentIndexValue."),
        field("percentToAttachment", "Decimal"),
        field("updatedAt", "DateTime", generated=True),
    ],
    "S1f.2/S1f.3: recalculated on IndustryLossIndexUpdated. Becomes a third trigger type for EvaluatePortfolioThreshold (S1e.1), alongside PML and Combined Ratio - TerritoryUnderwritingFrozen's activeTriggerReasons already supports multiple concurrent causes, so this is additive. Can also directly produce HedgingActionRequested (S1e.5) - 'purchase immediate micro-targeted traditional reinsurance layers to complement the bond' is exactly what that event already models.")
add_node("AUTOMATION", "TrackCatBondAttachmentDistance", "CatBondAttachmentDistanceUpdated", ACTOR_ROW, [],
    "Trigger: IndustryLossIndexUpdated.")
add_node("READMODEL", "CatBondCoverageView", "CatBondAttachmentDistanceUpdated", INTERACTION_ROW,
    [
        field("bondId", "UUID", idAttribute=True),
        field("bondName", "String"), field("coveredSyndicate", "String"), field("capacity", "Decimal"),
        field("attachmentPoint", "Decimal"), field("currentIndexValue", "Decimal"),
        field("distanceToAttachment", "Decimal"), field("percentToAttachment", "Decimal"),
    ],
    "'Executives can see exactly where the bond's coverage ends' - cross-referenced against GeographicExposureMap (1c.1) and ExposureConcentrationDashboard (1e.1).")

add_node("EVENT", "CatBondTriggered", "CatBondTriggered", SWIMLANE_ROW,
    [
        field("bondId", "UUID", idAttribute=True),
        field("qualifyingEvent", "String"),
        field("triggerConfirmedAt", "DateTime", generated=True),
        field("payoutAmount", "Decimal"),
    ],
    "S1f.4: the moment losses cross the attachment point and investor capital is legally forfeited, paid to the syndicate to cover claims. OPEN QUESTION (unresolved): who confirms the trigger - is index-crossing self-executing, or does it require an external determination (bond calculation agent)? Same 'don't silently auto-decide a money-moving event' instinct as ClaimPeriodValidated (S5.5).")

print(f"Total node operations: {len(events)}")
status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print(status)
print(json.dumps(resp, indent=2)[:1000])
