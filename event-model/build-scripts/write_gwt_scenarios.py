import json
import time
import urllib.request
import uuid
from collections import defaultdict

TOKEN = "3f04cb99-3107-426d-988c-0977dca27d06"
BOARD_ID = "80f53178-c291-43a0-8aa5-bc723990c5db"
ORG_ID = "401d5851-7972-43a6-be5c-59d30f844b4c"
BASE_URL = "https://api.eventmodelers.ai"
CHAPTER_ID = "e96f1046-4ab4-4654-89f3-8074c0aa06f4"

HEADERS = {"Content-Type": "application/json", "x-token": TOKEN, "x-board-id": BOARD_ID, "x-user-id": "gwt-build"}

with open("column_lookup_by_index.json") as f:
    LOOKUP = json.load(f)


def n(idx, lane="swimlane"):
    e = LOOKUP[idx]["ids"].get(lane)
    if not e:
        raise ValueError(f"No {lane} at column {idx}")
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


# Each tuple: (target_column_index, title, given[(idx,lane)], when[(idx,lane)], then[(idx,lane)])
S = [
    (0, "S0.1 Cell granted a new authority limit", [], [(0, "interaction")], [(0, "swimlane")]),
    (1, "S0.2 Underwriter granted authority within cell's limit", [(0, "swimlane")], [(2, "interaction")], [(1, "swimlane")]),
    (2, "S0.3 Underwriter authority request rejected - exceeds cell limit", [], [(2, "interaction")], [(2, "swimlane")]),
    (3, "S0.4 Authority limit revised downward mid-cycle", [], [(3, "interaction")], [(3, "swimlane")]),
    (4, "S0.5 Authority revoked entirely", [], [(4, "interaction")], [(4, "swimlane")]),
    (57, "S0.1 CellAuthorityRegister reflects the granted limit", [(57, "swimlane")], [], [(57, "interaction")]),
    (58, "S0.3 follow-on: cell authority increase requested after rejection", [(2, "swimlane")], [(58, "interaction")], [(58, "swimlane")]),
    (59, "S0.2 UnderwriterAuthorityRegister reflects the grant", [(59, "swimlane")], [], [(59, "interaction")]),
    (60, "S0.4/S0.5 in-flight submission reassessed on authority change", [(3, "swimlane")], [], [(60, "swimlane")]),

    (5, "S1a.1 Clean broker submission received and normalized", [], [(5, "interaction")], [(5, "swimlane"), (7, "swimlane")]),
    (8, "S1a.2 Submission received but fails ADEPT normalization", [], [(5, "interaction")], [(5, "swimlane"), (8, "swimlane")]),
    (6, "S1a.3 Submission for a cell/class the broker isn't authorized for", [], [(5, "interaction")], [(5, "swimlane"), (6, "swimlane")]),
    (10, "S1a.4 Duplicate submission received", [(5, "swimlane")], [(5, "interaction")], [(5, "swimlane"), (10, "swimlane")]),
    (7, "S1a.1 SubmissionQueue reflects the normalized submission", [(7, "swimlane")], [], [(7, "interaction")]),
    (8, "S1a.2 SubmissionExceptionQueue reflects the failed normalization", [(8, "swimlane")], [], [(8, "interaction")]),
    (6, "S1a.3 BrokerAuthorizationExceptionLog reflects the routing rejection", [(6, "swimlane")], [], [(6, "interaction")]),

    (35, "S1b.1 Underwriter searches submission history by risk attribute", [], [(35, "interaction")], [(35, "swimlane")]),
    (36, "S1b.1 SubmissionSearchResults reflects the search", [(36, "swimlane")], [], [(36, "interaction")]),
    (37, "S1b.3 Operations searches a broker's history for reconciliation, cross-cell", [], [(35, "interaction")], [(37, "swimlane")]),
    (37, "S1b.4/1b.3 BrokerActivityView reflects the cross-cell search", [(37, "swimlane")], [], [(37, "interaction")]),
    (38, "S1b.2 Claims handler retrieves submission lineage for a bound policy", [(30, "swimlane")], [], [(38, "swimlane")]),
    (38, "S1b.2 PolicyOriginationView reflects the retrieved lineage", [(38, "swimlane")], [], [(38, "interaction")]),

    (39, "S1c.1 New bind triggers exposure projection update", [(20, "swimlane")], [], [(39, "swimlane")]),
    (39, "S1c.1 GeographicExposureMap reflects the updated projection", [(39, "swimlane")], [], [(39, "interaction")]),
    (40, "S1c.2 Stacking detected, concentration warning raised", [(39, "swimlane")], [], [(40, "swimlane")]),
    (40, "S1c.2 ExposureConcentrationDashboard reflects the warning", [(40, "swimlane")], [], [(40, "interaction")]),
    (41, "S1c.2 Concentration warning acknowledged", [], [(41, "interaction")], [(41, "swimlane")]),
    (42, "S1c.3 Endorsement changes exposure at an existing location", [(21, "swimlane")], [], [(42, "swimlane")]),
    (43, "S1c.4 Cancellation reduces exposure at a location", [(22, "swimlane")], [], [(43, "swimlane")]),
    (44, "S1c.5 Exposure projection exported as a batch extract", [], [(44, "interaction")], [(44, "swimlane")]),
    (45, "S1c.5 ExposureExtractHistory reflects the export", [(45, "swimlane")], [], [(45, "interaction")]),

    (46, "S1d.1 Storm track update received from external feed", [], [(46, "interaction")], [(46, "swimlane")]),
    (47, "S1d.1 ActiveThreatRegister reflects the tracked storm", [(46, "swimlane")], [], [(47, "interaction")]),
    (48, "S1d.2 Storm track overlaid against exposure map, PML recalculated", [(46, "swimlane")], [], [(48, "swimlane")]),
    (48, "S1d.2 ThreatExposureView reflects the recalculation", [(48, "swimlane")], [], [(48, "interaction")]),
    (49, "S1d.3 Threat resolved/downgraded", [(46, "swimlane")], [], [(49, "swimlane")]),
    (50, "S1d.3 follow-on: exposure-at-risk explicitly cleared", [(49, "swimlane")], [], [(50, "swimlane")]),

    (51, "S1e.1 PML threshold breach triggers territory freeze", [(48, "swimlane")], [], [(51, "swimlane")]),
    (51, "S1e.1 ActiveFreezeRegister reflects the freeze", [(51, "swimlane")], [], [(51, "interaction")]),
    (52, "S1e.2 Underwriter blocked by portfolio freeze", [(51, "swimlane")], [(18, "interaction")], [(52, "swimlane")]),
    (53, "S1e.3 Freeze lifted once threat resolved", [(49, "swimlane")], [], [(53, "swimlane")]),
    (54, "S1e.4 Executive overrides freeze despite ongoing breach", [(51, "swimlane")], [(54, "interaction")], [(54, "swimlane")]),
    (55, "S1e.5 Freeze triggers hedging action request", [(51, "swimlane")], [(55, "interaction")], [(55, "swimlane")]),
    (56, "S1e.5 PortfolioActionLog reflects the hedging request", [(56, "swimlane")], [], [(56, "interaction")]),

    (13, "S2.1 Submission within authority, straight to quote", [], [(14, "interaction")], [(13, "swimlane")]),
    (14, "S2.2/S2.4 Submission referred", [], [(14, "interaction")], [(14, "swimlane")]),
    (15, "S2.2 Referral approved at next tier", [], [(15, "interaction")], [(15, "swimlane")]),
    (61, "S2.2 Original underwriter acknowledges modified referral terms", [(15, "swimlane")], [(61, "interaction")], [(61, "swimlane")]),
    (16, "S2.3 Referral declined at next tier", [], [(15, "interaction")], [(16, "swimlane")]),
    (62, "S2.5 Pending referral reassigned after referee's authority revoked", [(14, "swimlane"), (4, "swimlane")], [], [(62, "swimlane")]),
    (63, "S2.5 Pending referral held for governance attention", [(14, "swimlane")], [], [(63, "swimlane")]),
    (17, "S2.6 Submission declined outright, off-appetite/sanctioned", [], [(17, "interaction")], [(17, "swimlane")]),
    (64, "S2.6 Compliance notified of sanctions-related decline", [(17, "swimlane")], [], [(64, "swimlane")]),
    (18, "S2.7 Quote issued", [], [(18, "interaction")], [(18, "swimlane")]),
    (19, "S2.7 Quote accepted promptly", [], [(19, "interaction")], [(19, "swimlane")]),
    (65, "S2.8 Quote expires unaccepted", [(18, "swimlane")], [], [(65, "swimlane")]),
    (66, "S2.9 Broker explicitly declines the quote", [], [(66, "interaction")], [(66, "swimlane")]),
    (67, "S2.10 Broker requests amended terms on an issued quote", [], [(67, "interaction")], [(67, "swimlane")]),

    (20, "S3.1/S3.2 Policy bound, single or multi-provider allocation", [], [(20, "interaction")], [(20, "swimlane")]),
    (68, "S3.1 PolicyRegister reflects the bound policy", [(68, "swimlane")], [], [(68, "interaction")]),
    (69, "S3.1 ActiveBookOfBusiness reflects the bound policy", [(69, "swimlane")], [], [(69, "interaction")]),
    (21, "S3.3 Administrative endorsement processed directly", [], [(21, "interaction")], [(21, "swimlane")]),
    (70, "S3.3 Material endorsement referred for authority check", [], [(21, "interaction")], [(70, "swimlane")]),
    (22, "S3.4 Routine policy cancellation", [], [(22, "interaction")], [(22, "swimlane")]),
    (71, "S3.4 Policy cancelled for cause", [], [(22, "interaction")], [(71, "swimlane")]),
    (23, "S3.5 Renewal initiated ahead of expiry", [(20, "swimlane")], [(23, "interaction")], [(23, "swimlane")]),

    (24, "S4.1 Period closes, bordereau drafted", [], [], [(24, "swimlane")]),
    (73, "S4.1 UnbilledTransactionsForPeriod reflects what's swept in", [(73, "swimlane")], [], [(73, "interaction")]),
    (72, "S4.1 Bordereau submitted for provider agreement", [], [(72, "interaction")], [(72, "swimlane")]),
    (27, "S4.1 Bordereau agreed by provider", [(72, "swimlane")], [(27, "interaction")], [(27, "swimlane")]),
    (28, "S4.1 Bordereau settled, cash moves", [(27, "swimlane")], [(28, "interaction")], [(28, "swimlane")]),
    (25, "S4.2 Provider raises query on a bordereau line", [], [(74, "interaction")], [(25, "swimlane")]),
    (25, "S4.2 BordereauDetail reflects the queried line", [(25, "swimlane")], [], [(25, "interaction")]),
    (26, "S4.2 Query resolved, agreement can proceed", [], [(26, "interaction")], [(26, "swimlane")]),
    (74, "S4.3 Unresolved line deferred to next period rather than blocking settlement", [(25, "swimlane")], [], [(74, "swimlane")]),
    (29, "S4.4 Post-agreement correction via adjustment line", [], [(29, "interaction")], [(29, "swimlane")]),

    (30, "S5.1 Claim notified against a bound policy", [], [(30, "interaction")], [(30, "swimlane")]),
    (75, "S5.1 ClaimRegister reflects the notified claim", [(75, "swimlane")], [], [(75, "interaction")]),
    (31, "S5.1/S5.2 Claim reserve set (recurring)", [], [(31, "interaction")], [(31, "swimlane")]),
    (32, "S5.1/S5.3 Claim paid, split by provider quota share", [], [(32, "interaction")], [(32, "swimlane")]),
    (33, "S5.1 Claim closed", [], [(33, "interaction")], [(33, "swimlane")]),
    (76, "S5.2 Material reserve increase referred for authority check", [(31, "swimlane")], [], [(76, "swimlane")]),
    (34, "S5.4 Claim closed then reopened", [(33, "swimlane")], [(34, "interaction")], [(34, "swimlane")]),
    (77, "S5.5 Claim notified against an inactive policy", [(22, "swimlane")], [(30, "interaction")], [(30, "swimlane"), (77, "swimlane")]),
    (78, "S5.5 Claim period validated against date of loss", [(77, "swimlane")], [(78, "interaction")], [(78, "swimlane")]),
]

by_target = defaultdict(list)
for target, title, given, when, then in S:
    scenario = {
        "id": str(uuid.uuid4()),
        "title": title,
        "given": [n(i, l) for i, l in given],
        "when": [n(i, l) for i, l in when],
        "then": [n(i, l) for i, l in then],
    }
    by_target[target].append(scenario)

print(f"Total scenarios: {len(S)}, across {len(by_target)} target columns")

results = {"ok": 0, "fail": 0, "errors": []}
for target, scenarios in by_target.items():
    cid = col_id(target)
    status, resp = call("POST", f"/api/org/{ORG_ID}/boards/{BOARD_ID}/timelines/{CHAPTER_ID}/columns/{cid}/scenarios", scenarios)
    if status == 201:
        results["ok"] += len(scenarios)
    else:
        results["fail"] += len(scenarios)
        results["errors"].append((target, status, resp))
        print(f"FAILED col {target}: {status} {resp}")

print(json.dumps({"ok": results["ok"], "fail": results["fail"]}, indent=2))
if results["errors"]:
    print("Errors:")
    for e in results["errors"]:
        print(e)
