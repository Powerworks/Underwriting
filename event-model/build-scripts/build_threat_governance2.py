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


with open("/tmp/claude_column_ids3.json") as f:
    col = json.load(f)

now = int(time.time() * 1000)
events = []
node_ids = {}


def cell(col_label, row_id):
    return f"{row_id}-{col[col_label]}"


def add_node(node_type, title, col_label, row_id, fields_list, description):
    node_id = str(uuid.uuid4())
    events.append({
        "id": str(uuid.uuid4()),
        "eventType": "node:created",
        "nodeId": node_id,
        "boardId": BOARD_ID,
        "timestamp": now,
        "chapterId": CHAPTER_ID,
        "cellId": cell(col_label, row_id),
        "meta": {"type": node_type, "title": title, "fields": fields_list, "description": description},
        "node": {"id": node_id, "data": {"title": title}},
    })
    node_ids[title + "@" + col_label] = node_id
    return node_id


# ===========================================================================
# 1d. External Threat (exploratory)
# ===========================================================================

add_node(
    "COMMAND", "IngestExternalThreatUpdate", "StormTrackUpdateReceived", INTERACTION_ROW,
    [
        field("sourceFeed", "String", example="National Hurricane Center"),
        field("threatExternalId", "String", description="Trust the source feed's own ID scheme for storm continuity rather than inventing custom matching logic - avoids duplicate-threat creation across feed naming/versioning quirks."),
        field("trackCoordinates", "Custom"),
        field("category", "String"),
        field("updateTimestamp", "DateTime"),
        field("forecastConfidenceCone", "Custom", optional=True),
    ],
    "1d.1: technically closer to a scheduled poll or webhook receipt than a user command, modeled as a command to keep the triple consistent. Actor is an external system (NHC or commercial weather/cat feed), not a TFP user - the first context in the whole model where the trigger isn't internal.",
)
add_node(
    "EVENT", "StormTrackUpdateReceived", "StormTrackUpdateReceived", SWIMLANE_ROW,
    [
        field("threatId", "UUID", idAttribute=True, generated=True),
        field("sourceFeed", "String"),
        field("threatExternalId", "String"),
        field("trackCoordinates", "Custom"),
        field("category", "String"),
        field("forecastConfidenceCone", "Custom", optional=True),
        field("updateTimestamp", "DateTime", generated=True),
    ],
    "Recorded against the same threat ID as prior updates for that storm, not as a new unrelated threat.",
)

add_node(
    "EVENT", "ThreatFeedStale", "ThreatFeedStale", SWIMLANE_ROW,
    [
        field("sourceFeed", "String"),
        field("expectedUpdateBy", "DateTime"),
        field("lastReceivedAt", "DateTime"),
        field("staleDurationMinutes", "Integer"),
        field("detectedAt", "DateTime", generated=True),
    ],
    "Unlike a broker submission (which someone will chase if it goes missing), a missed storm update has no one on the sending side accountable for TFP receiving it - an explicit stale alert prevents leadership silently looking at an outdated map without knowing it's outdated. Arguably more important than the happy path given what's riding on this data.",
)
add_node(
    "AUTOMATION", "MonitorThreatFeedHealth", "ThreatFeedStale", ACTOR_ROW,
    [],
    "Ingestion boundary needs its own reliability/staleness handling - checks time since last update per source feed against expected cadence.",
)
add_node(
    "READMODEL", "ActiveThreatRegister", "ThreatFeedStale", INTERACTION_ROW,
    [
        field("threatId", "UUID", idAttribute=True),
        field("sourceFeed", "String"),
        field("category", "String"),
        field("latestUpdateAt", "DateTime"),
        field("status", "String", example="Active"),
    ],
    "List of currently tracked live threats, each with latest update timestamp and status - surfaces staleness directly rather than presenting a silently-outdated view as current.",
)

add_node(
    "EVENT", "PMLRecalculated", "PMLRecalculated-1d", SWIMLANE_ROW,
    [
        field("threatId", "UUID"),
        field("calculationTimestamp", "DateTime", generated=True),
        field("triggerReason", "String", example="storm-update"),
        field("affectedGeocodes", "Custom", cardinality="List"),
        field("modeledPMLByScenario", "Custom", cardinality="List", subfields=[field("scenarioName", "String"), field("modeledLoss", "Decimal")]),
        field("affectedPolicies", "Custom", cardinality="List"),
        field("priorPMLFigure", "Decimal", optional=True),
        field("hasImpact", "Boolean", description="1d.4: fires with hasImpact=false and a zero/null modeled loss when a tracked storm has no intersecting bound policies, rather than the process doing nothing - distinguishes 'checked, no exposure' from 'never checked' in the audit trail."),
        field("sourcedFromCatModel", "Boolean", description="Always true - flags that this figure is requested/ingested from a specialist cat-modeling capability, never computed by Broker Connect itself. Same integration-boundary pattern as ExportExposureExtract (1c.5), in the reactive direction."),
    ],
    "1d.2: reacts to BOTH StormTrackUpdateReceived AND exposure changes under an already-tracked threat (ExposureProjectionUpdated while a storm is active) - two independent triggers converging on one event, not one policy with two names. OPEN QUESTION resolved as a design stance (not decided unilaterally, flagged for confirmation): the actual PML calculation is a specialist actuarial/cat-modeling capability - this system requests and displays the result, it does not compute storm physics against policy terms itself.",
)
add_node(
    "AUTOMATION", "RecalculatePMLOnThreatUpdate", "PMLRecalculated-1d", ACTOR_ROW,
    [],
    "Dual trigger: StormTrackUpdateReceived (1d.1), or ExposureProjectionUpdated (1c.1/1c.3) while a threat is active.",
)
add_node(
    "READMODEL", "ThreatExposureView", "PMLRecalculated-1d", INTERACTION_ROW,
    [
        field("threatId", "UUID", idAttribute=True),
        field("trackSummary", "Custom"),
        field("modeledLossByScenario", "Custom", cardinality="List", subfields=[field("scenarioName", "String"), field("modeledLoss", "Decimal")]),
        field("affectedGeocodes", "Custom", cardinality="List"),
        field("lastRecalculatedAt", "DateTime"),
    ],
    "The live 'storm overlaid on policy map' view, showing modeled loss by scenario (e.g. by category/landfall scenario if the source cat model supports multiple).",
)

add_node(
    "EVENT", "ThreatResolved", "ThreatResolved", SWIMLANE_ROW,
    [
        field("threatId", "UUID", idAttribute=True),
        field("resolutionReason", "String", example="dissipated | downgraded | made landfall"),
        field("finalTrackData", "Custom"),
        field("resolvedAt", "DateTime", generated=True),
    ],
    "1d.3: fires when the feed reports the storm dissipated, made landfall and passed, or downgraded below a tracking threshold. Any active ExposureConcentrationWarning or TerritoryUnderwritingFrozen tied to this threat should be explicitly reassessed, not left dangling - direct link forward into 1e's freeze-lift logic.",
)

add_node(
    "EVENT", "ExposureAtRiskCleared", "ExposureAtRiskCleared", SWIMLANE_ROW,
    [
        field("threatId", "UUID"),
        field("relatedWarningId", "UUID", optional=True),
        field("clearedAt", "DateTime", generated=True),
    ],
    "Explicit stand-down of a concentration warning tied to a resolved threat, rather than the warning just quietly stopping updates - same 'not left dangling' discipline as ThreatFeedStale.",
)

# ===========================================================================
# 1e. Portfolio Governance (exploratory)
# ===========================================================================

add_node(
    "EVENT", "TerritoryUnderwritingFrozen", "TerritoryUnderwritingFrozen", SWIMLANE_ROW,
    [
        field("freezeId", "UUID", idAttribute=True, generated=True),
        field("territory", "String"),
        field("perilCategory", "String", optional=True),
        field("triggeringThreatId", "UUID", optional=True, description="Set when PML-driven."),
        field("triggeringMetric", "String", optional=True, example="CombinedRatio", description="Set when Combined-Ratio-driven."),
        field("thresholdBreached", "Decimal"),
        field("breachMagnitude", "Decimal"),
        field("affectedCells", "Custom", cardinality="List"),
        field("freezeScope", "Custom", description="Which classes/cells it applies to - a freeze might be property-only in a region, not blanket."),
        field("activeTriggerReasons", "Custom", cardinality="List", description="A freeze may have multiple concurrent trigger reasons (e.g. both an active storm and a Combined Ratio concern) - only lifts when this set is empty, not on any single cause clearing."),
        field("frozenAt", "DateTime", generated=True),
    ],
    "1e.1: two independent gates now exist into Context 2 - Context 0's per-underwriter authority cascade, and this freeze mechanism. Reacts to PMLRecalculated (1d) and, separately, periodic Combined Ratio assessment. OPEN QUESTIONS: threshold ownership (domain-expert-owned, same pattern as 1c's concentration thresholds - system enforces, doesn't set; may vary by season or current reinsurance program); is the freeze automatic on breach, or does it require human sign-off? Leaning toward automatic alert + recommended freeze + human confirmation, at least until there's a track record - flagged as a risk-appetite decision for TFP stakeholders, not defaulted silently.",
)
add_node(
    "AUTOMATION", "EvaluatePortfolioThreshold", "TerritoryUnderwritingFrozen", ACTOR_ROW,
    [],
    "Reacts to PMLRecalculated events and, separately, to periodic Combined Ratio assessment.",
)
add_node(
    "READMODEL", "ActiveFreezeRegister", "TerritoryUnderwritingFrozen", INTERACTION_ROW,
    [
        field("freezeId", "UUID", idAttribute=True),
        field("territory", "String"),
        field("affectedCells", "Custom", cardinality="List"),
        field("freezeScope", "Custom"),
        field("activeTriggerReasons", "Custom", cardinality="List"),
        field("frozenAt", "DateTime"),
        field("status", "String", example="Active"),
    ],
    "Visible to underwriting executives and, critically, surfaced to underwriters in affected cells at the point of decisioning (Context 2).",
)

add_node(
    "EVENT", "SubmissionBlockedByPortfolioFreeze", "SubmissionBlockedByPortfolioFreeze", SWIMLANE_ROW,
    [
        field("submissionId", "UUID"),
        field("underwriterId", "String"),
        field("freezeReference", "UUID"),
        field("attemptedAction", "String", example="IssueQuote"),
        field("blockedAt", "DateTime", generated=True),
    ],
    "1e.2: intercepts whatever Context 2 command is in flight (e.g. IssueQuote). The underwriter sees a clear block reason distinguishing this from a personal-authority referral (SubmissionReferred) - 'you're not authorized' and 'this territory is frozen regardless of who you are' must read differently, not collapse into one generic rejection. OPEN QUESTION: does a blocked submission auto-resume once the freeze lifts, or need active resubmission/re-evaluation (terms/pricing may have moved on during the freeze)? Leaning toward auto-resume with a re-validation step - flagged as a product decision.",
)

add_node(
    "EVENT", "TerritoryUnderwritingFrozenLifted", "TerritoryUnderwritingFrozenLifted", SWIMLANE_ROW,
    [
        field("freezeReference", "UUID", idAttribute=True),
        field("liftReason", "String"),
        field("liftTimestamp", "DateTime", generated=True),
        field("finalMetricValue", "Decimal", optional=True),
        field("remainingActiveTriggerReasons", "Custom", cardinality="List", description="Should be empty for the freeze to actually lift - if a freeze was raised by combined factors, resolving one alone doesn't clear it."),
    ],
    "1e.3: fires when ThreatResolved (1d.3) clears for the triggering storm and no other active threat/metric justifies continuing the freeze. Submissions blocked under SubmissionBlockedByPortfolioFreeze (1e.2) become eligible to resume.",
)

add_node(
    "COMMAND", "OverridePortfolioFreeze", "PortfolioFreezeOverridden", INTERACTION_ROW,
    [
        field("freezeReference", "UUID"),
        field("overridingExecutiveId", "String"),
        field("justification", "String", optional=False, description="Mandatory, not optional, given what this is bypassing."),
        field("overrideScope", "String", example="single-submission | full-lift"),
        field("coSignedBy", "String", optional=True, description="Field included from the start even though whether four-eyes is mandatory is a policy decision, not an architecture default - cheaper to have the field unused than retrofit it."),
    ],
    "1e.4: a senior executive with override authority - structurally similar to Context 0's authority model but for a different kind of permission (portfolio override, not underwriting line authority). Modeled as a distinct, short, tightly-controlled authority concept rather than an extension of Context 0's per-underwriter cascade.",
)
add_node(
    "EVENT", "PortfolioFreezeOverridden", "PortfolioFreezeOverridden", SWIMLANE_ROW,
    [
        field("freezeReference", "UUID"),
        field("overridingExecutiveId", "String"),
        field("justification", "String"),
        field("overrideScope", "String"),
        field("coSignedBy", "String", optional=True),
        field("overriddenAt", "DateTime", generated=True),
    ],
    "Underlying freeze state is explicitly marked as overridden - permanently distinguishable in the audit trail from TerritoryUnderwritingFrozenLifted (genuine threshold-clear), never silently indistinguishable from it.",
)

add_node(
    "COMMAND", "RequestHedgingAction", "HedgingActionRequested-1", INTERACTION_ROW,
    [
        field("freezeOrThreatReference", "UUID"),
        field("requestedActionType", "String", example="seek micro-targeted reinsurance layer for Gulf Coast wind"),
        field("requestingParty", "String"),
    ],
    "Issued by a portfolio manager, not automatically fired by the system itself.",
)
add_node(
    "EVENT", "HedgingActionRequested", "HedgingActionRequested-1", SWIMLANE_ROW,
    [
        field("freezeOrThreatReference", "UUID"),
        field("requestedActionType", "String"),
        field("requestingParty", "String"),
        field("requestedAt", "DateTime", generated=True),
    ],
    "1e.5: the clearest scope boundary in the whole exploratory set. Recorded as an intent, handed off to whatever system/team actually executes reinsurance placement - the actual placement of a reinsurance layer is a distinct, already-existing TFP function (outwards reinsurance) with its own systems and market relationships. This is the hard edge of Broker Connect's responsibility; modeling the placement itself would be significant, unjustified scope creep into a different domain.",
)
add_node(
    "READMODEL", "PortfolioActionLog", "HedgingActionRequested-2", INTERACTION_ROW,
    [
        field("freezeOrThreatReference", "UUID"),
        field("requestedActionType", "String"),
        field("requestingParty", "String"),
        field("requestedAt", "DateTime"),
        field("status", "String", example="Handed off"),
    ],
    "Visible to the reinsurance/outwards placement team.",
)
add_node(
    "EVENT", "HedgingActionRequested", "HedgingActionRequested-2", SWIMLANE_ROW,
    [field("freezeOrThreatReference", "UUID"), field("requestedActionType", "String")],
    "Second instance of the same event type, paired here with the action-log projection it feeds.",
)

print(f"Total node operations: {len(events)}")
status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/nodes/events", events)
print(status)
print(json.dumps(resp, indent=2)[:1200])

with open("/tmp/claude_node_ids3.json", "w") as f:
    json.dump(node_ids, f, indent=2)
