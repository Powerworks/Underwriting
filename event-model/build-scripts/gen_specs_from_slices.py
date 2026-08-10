#!/usr/bin/env python3
"""Generate spec artifacts from the Underwriting event-model static export.

Source of truth: event-model/import-config.json (bundled slices export).
Every command/event/readmodel/processor and every field on them is rendered
into the output so nothing from the static export is lost.

Two target formats, one per feature (see FEATURES below):

- **spec-kit**: the original GitHub Spec Kit `spec.md` shape. Used only for
  `001-authority-administration`, which is already complete under that
  workflow and not retroactively migrated (constitution v1.3.0).
- **ralph**: `requirements.md` (Ralph Specum's shape: User Stories/AC,
  FR/NFR tables, Glossary, etc., plus the same lossless Event Model Detail
  appendix spec-kit had) and `research.md` (a stub — this feature's
  "research" is the board itself, not external/codebase exploration — plus
  a UI Reference section transcribing any screens for the design phase to
  fold into design.md). Used for every feature from `002-submission-intake`
  onward, per constitution v1.3.0.

Run from anywhere (paths are resolved relative to this file); it overwrites
specs/*/{spec,requirements,research}.md in place, per each feature's target.
The FEATURES map below encodes this board's context -> feature grouping AND
target format — re-check/adjust it if the board's contexts change. See
../../event-model-to-speckit-guide.md for the original mapping rationale and
how to verify completeness after running this (the Event Model Detail
appendix substring-check applies to both targets).
"""
import datetime
import json
import re
from collections import OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]  # event-model/build-scripts/ -> event-model/ -> repo root
SRC = ROOT / "event-model" / "import-config.json"
TODAY = datetime.date.today().isoformat()

# context -> (spec dir slug, target format) — target must match the directories
# already created via create-new-feature.sh (spec-kit) or /ralph-specum:start
# (ralph). This is Phase A / MVP scope only (Project Plan/01-project-plan.md
# §4) — the 4 exploratory contexts (Exposure Intelligence, External Threat,
# Portfolio Governance, Capital & Reinsurance Instruments) are Phase B, not
# yet pulled/generated.
FEATURES = OrderedDict([
    ("Authority Administration", ("001-authority-administration", "spec-kit")),
    ("Submission Intake", ("002-submission-intake", "ralph")),
    ("Search & Retrieval", ("003-search-retrieval", "ralph")),
    ("Underwriting Decisioning", ("004-underwriting-decisioning", "ralph")),
    ("Binding", ("005-binding", "ralph")),
    ("Bordereaux Settlement", ("006-bordereaux-settlement", "ralph")),
    ("Claims", ("007-claims", "ralph")),
])

NEGATIVE_WORDS = ("Declined", "Rejected", "Referred", "Blocked", "Failed")
MOSCOW = {"P1": "Must", "P2": "Should", "P3": "Could"}


def load_slices():
    data = json.loads(SRC.read_text())
    slices = data["slices"]
    by_context = OrderedDict((ctx, []) for ctx in FEATURES)
    for s in slices:
        by_context[s["context"]].append(s)
    return by_context


def bold(s):
    return f"**{s}**"


def field_rows(fields, indent=0):
    rows = []
    for f in fields:
        prefix = "&nbsp;&nbsp;&nbsp;&nbsp;↳ " * indent
        flags = []
        if f.get("idAttribute"):
            flags.append("id")
        if f.get("generated"):
            flags.append("generated")
        if f.get("optional"):
            flags.append("optional")
        flagstr = ", ".join(flags) if flags else "—"
        rows.append(
            f"| {prefix}`{f['name']}` | {f['type']} | {f['cardinality']} | {flagstr} |"
        )
        if f.get("subfields"):
            rows.extend(field_rows(f["subfields"], indent=indent + 1))
    return rows


def format_edge_list(edges, label):
    """Renders the board's dependency-edge shape ({id,type,title,elementType}) as
    readable prose instead of a raw Python dict repr."""
    if not edges:
        return None
    parts = []
    for edge in edges:
        arrow = "←" if edge.get("type") == "INBOUND" else "→"
        parts.append(f"{arrow} {edge.get('title', '?')} ({edge.get('elementType', '?')})")
    return f"{label}: " + "; ".join(parts)


def element_block(el, kind):
    lines = []
    agg = el.get("aggregate")
    agg_label = agg if agg and agg != "default" else "—"
    lines.append(f"**{el['title']}** ({kind}, id `{el['id']}`, aggregate `{agg_label}`, lane `{el.get('lane') or '—'}`, modelContext `{el.get('modelContext') or '—'}`)")
    lines.append("")
    if el.get("description"):
        lines.append(f"> {el['description']}")
        lines.append("")
    dep_lines = [
        format_edge_list(el.get("dependencies"), "Dependencies"),
        format_edge_list(el.get("aggregateDependencies"), "Aggregate dependencies"),
    ]
    if el.get("triggers"):
        dep_lines.append(f"Triggers: {', '.join(str(t) for t in el['triggers'])}")
    if el.get("tags"):
        dep_lines.append(f"Tags: {', '.join(str(t) for t in el['tags'])}")
    dep_lines = [d for d in dep_lines if d]
    if dep_lines:
        lines.append("; ".join(dep_lines))
        lines.append("")
    if el.get("fields"):
        lines.append("| Field | Type | Cardinality | Flags |")
        lines.append("|---|---|---|---|")
        lines.extend(field_rows(el["fields"]))
        lines.append("")
    else:
        lines.append("_(no fields)_")
        lines.append("")
    return "\n".join(lines)


def slug(title):
    return re.sub(r"[^a-z0-9]+", "-", title.lower()).strip("-")


def classify_slice(s):
    """Shared slice-kind/actor/priority inference — the one tricky heuristic in
    this script (see event-model-to-speckit-guide.md, 'Slices with no
    sliceType' for the empirical basis), factored out so every renderer
    (spec-kit and ralph targets alike) uses the same source of truth instead
    of two copies that can quietly diverge. Returns computed data, not markdown.
    """
    events = s["events"]
    readmodels = s.get("readmodels", [])

    raw_slice_type = s.get("sliceType")
    if raw_slice_type == "AUTOMATION":
        slice_kind = "AUTOMATION"
    elif raw_slice_type == "STATE_VIEW":
        slice_kind = "STATE_VIEW"
    elif raw_slice_type == "STATE_CHANGE" or s["commands"]:
        slice_kind = "STATE_CHANGE"
    elif s["processors"]:
        slice_kind = "AUTOMATION"
    elif not s["commands"] and readmodels:
        slice_kind = "STATE_VIEW"
    elif not s["commands"] and not s["processors"] and events:
        slice_kind = "FACT_ONLY"
    else:
        slice_kind = "STATE_CHANGE"

    is_automation = slice_kind == "AUTOMATION"
    is_state_view = slice_kind == "STATE_VIEW"
    is_fact_only = slice_kind == "FACT_ONLY"
    if is_automation:
        actor_list = s["processors"]
    elif is_state_view or is_fact_only:
        actor_list = []
    else:
        actor_list = s["commands"]
    actor = actor_list[0] if actor_list else None

    priority = "P2" if (is_automation or is_state_view) else "P1"
    if is_fact_only or s["id"] == "decline-submission":
        priority = "P3"

    narrative = actor.get("description") if actor else (
        (readmodels[0].get("description") if readmodels else None)
        or (events[0].get("description") if events else "")
    )
    cmd_title = actor["title"] if actor else (
        "(projection, no command)" if is_state_view else
        "(no command in source — see narrative)" if is_fact_only else
        "(automation trigger)"
    )
    rm_join = " and ".join(r["title"] for r in readmodels) if readmodels else "a read model"

    return {
        "slice_kind": slice_kind,
        "raw_slice_type": raw_slice_type,
        "is_automation": is_automation,
        "is_state_view": is_state_view,
        "is_fact_only": is_fact_only,
        "actor": actor,
        "priority": priority,
        "narrative": narrative,
        "cmd_title": cmd_title,
        "rm_join": rm_join,
        "events": events,
        "readmodels": readmodels,
    }


def collect_entities_and_detail(slices, include_screens):
    """Entities (aggregate -> touched-by titles), readmodel entities, and the
    per-slice Event Model Detail block — shared across every renderer, since
    this part is a straight transcription, not renderer-specific prose.

    include_screens=False for requirements.md (screens live in research.md's
    UI Reference section instead, for the design phase — not a requirement).
    """
    entities = OrderedDict()
    readmodel_entities = OrderedDict()
    detail_sections = []
    for s in slices:
        all_elements = s["commands"] + s["events"] + s["processors"]
        for el in all_elements:
            agg = el.get("aggregate")
            if not agg or agg == "default":
                continue
            entities.setdefault(agg, set()).add(el["title"])
        for rm in s.get("readmodels", []):
            readmodel_entities[rm["title"]] = (rm.get("description", ""), rm.get("fields", []))

        info = classify_slice(s)
        type_label = info["raw_slice_type"] or f"{info['slice_kind']} (inferred — sliceType missing in source export)"
        block = [f"### Slice: {s['title']} (`{s['id']}`, status: {s['status']}, type: {type_label})", ""]
        for c in s["commands"]:
            block.append(element_block(c, "command"))
        for p in s["processors"]:
            block.append(element_block(p, "automation/processor"))
        for e in s["events"]:
            block.append(element_block(e, "event"))
        for rm in s.get("readmodels", []):
            block.append(element_block(rm, "read model"))
        if include_screens:
            for sc in s.get("screens", []):
                block.append(element_block(sc, "screen"))
        detail_sections.append("\n".join(block))
    return entities, readmodel_entities, detail_sections


def collect_screens(slices):
    out = []
    for s in slices:
        for sc in s.get("screens", []):
            out.append((s["title"], sc))
    return out


def collect_open_questions(slices):
    """Slices where sliceType had to be inferred (see classify_slice) are
    genuine ambiguity in the source, not settled fact — surface them rather
    than let the inference pass silently."""
    qs = []
    for s in slices:
        if not s.get("sliceType"):
            kind = classify_slice(s)["slice_kind"]
            qs.append(f"`{s['title']}` has no `sliceType` in the source export — kind was inferred as {kind}; confirm this is correct.")
    return qs


# ---------------------------------------------------------------------------
# spec-kit target (001-authority-administration only)
# ---------------------------------------------------------------------------

def build_stories(slices):
    """Returns (story_markdown, fr_lines, edge_case_lines, entities, readmodel_entities, event_detail_sections).
    spec-kit (GitHub Spec Kit) target only — see render_requirements_md for the ralph target."""
    stories = []
    fr_lines = []
    edge_lines = []
    fr_num = 0
    story_num = 0

    for s in slices:
        story_num += 1
        info = classify_slice(s)
        events = info["events"]
        readmodels = info["readmodels"]
        is_automation = info["is_automation"]
        is_state_view = info["is_state_view"]
        is_fact_only = info["is_fact_only"]
        actor = info["actor"]
        priority = info["priority"]
        narrative = info["narrative"]
        cmd_title = info["cmd_title"]
        rm_join_bold = " and ".join(bold(r["title"]) for r in readmodels) if readmodels else "a read model"
        title = s["title"]

        stories.append(f"### User Story {story_num} - {title} (Priority: {priority})")
        stories.append("")
        if is_automation:
            stories.append(
                f"As a background policy in **{s['context']}**, the system reacts by executing "
                f"{bold(actor['title']) if actor else '(automation)'}, producing "
                + " or ".join(bold(e['title']) for e in events) + "."
            )
        elif is_state_view:
            stories.append(
                f"The system maintains {rm_join_bold}, projected from "
                + " and ".join(bold(e["title"]) for e in events) + "."
            )
        elif is_fact_only:
            stories.append(
                "As an alternate/terminal outcome recorded elsewhere in this context, the system records "
                + " and ".join(bold(e["title"]) for e in events)
                + " — this slice has no command or processor of its own in the source export; it documents "
                "a fact produced by a decision modeled in another slice."
            )
        else:
            stories.append(
                f"As {bold(actor['title']) if actor else '(no command)'}, I want to "
                f"{title[0].lower() + title[1:]} so that "
                + " or ".join(bold(e['title']) for e in events) + " is recorded."
            )
        stories.append("")
        if narrative:
            stories.append(f"**Narrative** (verbatim from the board export): {narrative}")
            stories.append("")
        if is_fact_only:
            why = "Documented alternate/terminal outcome from the source event model, standing alone as its own slice — must be handled explicitly rather than left implicit in another slice's happy path."
        elif priority == "P3":
            why = "Documented off-ramp/rejection path from the source event model — must be handled explicitly rather than left to fail silently."
        elif is_automation:
            why = "System-driven policy that keeps derived state (bordereaux, projections, notifications) consistent after the primary action(s) that trigger it."
        elif is_state_view:
            why = "Read-side projection supporting other slices' decisions/queries — supporting infrastructure, not a primary user action in its own right."
        else:
            why = "Primary, human-initiated action in this bounded context — without it the underlying business process cannot proceed."
        stories.append(f"**Why this priority**: {why}")
        stories.append("")
        if is_state_view:
            stories.append(
                f"**Independent Test**: Can be tested by appending "
                + " and/or ".join(bold(e["title"]) for e in events)
                + f" and asserting that {rm_join_bold} reflects the update."
            )
        elif is_fact_only:
            stories.append(
                "**Independent Test**: Can be tested by asserting that, under the triggering condition "
                "described in the narrative above, "
                + " and/or ".join(bold(e["title"]) for e in events)
                + " is the resulting domain event."
            )
        else:
            stories.append(
                f"**Independent Test**: Can be tested by invoking {bold(cmd_title)} under the right "
                f"preconditions and asserting that "
                + " (or, on the alternate path, ".join(bold(e["title"]) for e in events)
                + (")" if len(events) > 1 else "")
                + " is the resulting domain event."
            )
        stories.append("")
        stories.append("**Acceptance Scenarios**:")
        stories.append("")
        for i, e in enumerate(events, start=1):
            given = e.get("description") or "the preconditions for this step are met"
            if is_state_view:
                stories.append(f"{i}. **Given** {given}, **When** {e['title']} is appended, **Then** {rm_join_bold} reflects it")
            elif is_fact_only:
                stories.append(f"{i}. **Given** {given}, **When** the triggering condition occurs, **Then** {e['title']}")
            else:
                stories.append(f"{i}. **Given** {given}, **When** {cmd_title}, **Then** {e['title']}")
        stories.append("")
        stories.append("---")
        stories.append("")

        fr_num += 1
        ev_join = " or ".join(bold(e["title"]) for e in events) if events else "(no event)"
        if is_state_view:
            fr_lines.append(f"- **FR-{fr_num:03d}**: System MUST project {rm_join_bold} from the {ev_join} domain event(s).")
        elif is_fact_only:
            fr_lines.append(f"- **FR-{fr_num:03d}**: System MUST record the {ev_join} domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).")
        else:
            fr_lines.append(f"- **FR-{fr_num:03d}**: System MUST support {bold(cmd_title)}, producing the {ev_join} domain event(s).")

        if is_fact_only:
            for e in events:
                edge_lines.append(
                    f"- What happens under the condition described for {bold(e['title'])}? "
                    f"System records it as a standalone fact"
                    + (f" — {e['description']}" if e.get("description") else "")
                    + " (no command/processor of its own in the source export)."
                )
        elif not is_state_view:
            for i, e in enumerate(events):
                if i > 0 or any(w in e["title"] for w in NEGATIVE_WORDS) or priority == "P3":
                    edge_lines.append(
                        f"- What happens when {cmd_title} cannot proceed on the happy path? "
                        f"System records {bold(e['title'])} instead"
                        + (f" — {e['description']}" if e.get("description") else "") + "."
                    )

    entities, readmodel_entities, detail_sections = collect_entities_and_detail(slices, include_screens=True)
    return "\n".join(stories), fr_lines, edge_lines, entities, readmodel_entities, detail_sections


def render_spec(context, feature_slug, slices):
    feature_name = " ".join(w.capitalize() for w in feature_slug.split("-")[1:])
    stories_md, fr_lines, edge_lines, entities, readmodel_entities, detail_sections = build_stories(slices)

    lines = []
    lines.append(f"# Feature Specification: {feature_name}")
    lines.append("")
    lines.append(f"**Feature Branch**: `{feature_slug}`")
    lines.append("")
    lines.append(f"**Created**: {TODAY}")
    lines.append("")
    lines.append("**Status**: Draft")
    lines.append("")
    lines.append(
        f"**Input**: Derived from the \"BrokerConnect\" eventmodelers.ai board "
        f"(`80f53178-c291-43a0-8aa5-bc723990c5db`), chapter **Broker Connect**, context "
        f"**{context}** — pulled live via `GET .../slicedata?contextName=...` on {TODAY} and "
        f"cached at `event-model/slices/*.json` / `event-model/import-config.json` (Phase A / "
        f"MVP scope only — see `Project Plan/01-project-plan.md` §4; the 4 exploratory contexts "
        f"are not yet pulled). Supersedes an earlier partial export now kept at "
        f"`event-model/archive/` for provenance — see `event-model-to-speckit-guide.md`."
    )
    lines.append("")
    lines.append("## User Scenarios & Testing *(mandatory)*")
    lines.append("")
    lines.append(stories_md)
    lines.append("### Edge Cases")
    lines.append("")
    if edge_lines:
        lines.extend(edge_lines)
    else:
        lines.append("- None documented as separate branches in the source export for this context; every command in this context has exactly one outcome event.")
    lines.append("")
    lines.append("## Requirements *(mandatory)*")
    lines.append("")
    lines.append("### Functional Requirements")
    lines.append("")
    lines.extend(fr_lines)
    lines.append("")
    lines.append("### Key Entities *(include if feature involves data)*")
    lines.append("")
    if not entities:
        lines.append(
            "- **Aggregate names not derivable from the source export**: every command/event/automation "
            "in this context carries the board's literal placeholder `aggregate: \"default\"` — never "
            "customized per element. Read-model entities below are still meaningful (they have real "
            "titles); aggregate/stream boundaries for the write side need to be decided during "
            "`/speckit-plan` from the command/event field shapes in the Event Model Detail section, not "
            "read off this field."
        )
    for agg, touched_by in entities.items():
        lines.append(f"- {bold(agg)}: aggregate in the **{context}** context; touched by {', '.join(bold(t) for t in sorted(touched_by))}.")
    for rm_title, (desc, fields) in readmodel_entities.items():
        field_names = ", ".join(f"`{f['name']}`" for f in fields)
        lines.append(f"- {bold(rm_title)} (read model): {desc} Fields: {field_names}.")
    lines.append("")
    lines.append("## Success Criteria *(mandatory)*")
    lines.append("")
    lines.append("<!--")
    lines.append("  NOTE: the source eventmodelers board did not define measurable success metrics for this")
    lines.append("  context — these are template placeholders only. Fill in via /speckit-clarify or manually")
    lines.append("  before /speckit-plan.")
    lines.append("-->")
    lines.append("")
    lines.append("### Measurable Outcomes")
    lines.append("")
    lines.append('- **SC-001**: [Measurable metric, e.g., "Users can complete account creation in under 2 minutes"]')
    lines.append('- **SC-002**: [Measurable metric, e.g., "System handles 1000 concurrent users without degradation"]')
    lines.append('- **SC-003**: [User satisfaction metric, e.g., "90% of users successfully complete primary task on first attempt"]')
    lines.append('- **SC-004**: [Business metric, e.g., "Reduce support tickets related to [X] by 50%"]')
    lines.append("")
    lines.append("## Assumptions")
    lines.append("")
    lines.append(
        f"- Derived from the \"BrokerConnect\" eventmodelers.ai board (chapter **Broker Connect**, "
        f"context **{context}**); command/event names, field lists, descriptions, aggregates, and "
        f"lanes in the Event Model Detail section below are transcribed directly from a live pull "
        f"of that board, not invented for this document."
    )
    lines.append(
        f"- Pulled live on {TODAY} via the `slicedata` endpoint (see "
        "`event-model-to-speckit-guide.md`, \"The real export endpoint\"), not the raw event-replay "
        "log — re-run `event-model/build-scripts/gen_specs_from_slices.py` after any further board "
        "edits to keep this in sync; nothing here watches the board automatically."
    )
    lines.append(
        f"- All {len(slices)} slice(s) in this context currently carry board status `Created` — none "
        "are `Planned` or built yet."
    )
    lines.append("- The source board did not specify performance, scale, or business-metric assumptions for this context — the Success Criteria above are template placeholders.")
    lines.append("- [Assumption about scope boundaries — confirm before /speckit.plan]")
    lines.append("")
    lines.append("## Event Model Detail (Source of Truth)")
    lines.append("")
    lines.append(
        "Full, unabridged transcription of every command, automation/processor, event, and read model "
        "in this context from the static export, including every field's type, cardinality, and flags "
        "(`id` = identifier field, `generated` = system-generated, `optional` = nullable). Nested "
        "`List`/object fields show their subfields indented beneath them with `↳`."
    )
    lines.append("")
    lines.extend(detail_sections)

    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------
# ralph target (002-submission-intake onward)
# ---------------------------------------------------------------------------

def render_requirements_md(context, feature_slug, slices):
    feature_name = " ".join(w.capitalize() for w in feature_slug.split("-")[1:])
    entities, readmodel_entities, detail_sections = collect_entities_and_detail(slices, include_screens=False)

    us_blocks = []
    fr_rows = []
    glossary = OrderedDict()
    fr_num = 0

    for story_num, s in enumerate(slices, start=1):
        info = classify_slice(s)
        events = info["events"]
        readmodels = info["readmodels"]
        actor = info["actor"]
        cmd_title = info["cmd_title"]
        priority = info["priority"]
        rm_join = info["rm_join"]
        title = s["title"]

        if info["is_automation"]:
            as_a = f"background policy in {context}"
            i_want = f"the system to execute {cmd_title} automatically"
            so_that = "derived state stays consistent after the triggering action(s): " + " or ".join(e["title"] for e in events)
        elif info["is_state_view"]:
            as_a = "a consumer of this read-side projection"
            i_want = f"{rm_join} to reflect {' and '.join(e['title'] for e in events)}"
            so_that = "downstream queries/decisions see current state"
        elif info["is_fact_only"]:
            as_a = "the system"
            i_want = f"to record {' and '.join(e['title'] for e in events)}"
            so_that = "the alternate/terminal outcome is captured explicitly, not left implicit"
        else:
            as_a = actor["title"] if actor else "a user"
            i_want = title[0].lower() + title[1:]
            so_that = " or ".join(e["title"] for e in events) + " is recorded"

        us_blocks.append(f"### US-{story_num}: {title}")
        us_blocks.append("")
        us_blocks.append(f"**As a** {as_a}")
        us_blocks.append(f"**I want to** {i_want}")
        us_blocks.append(f"**So that** {so_that}")
        us_blocks.append("")
        if info["narrative"]:
            us_blocks.append(f"_Narrative (verbatim from the board export)_: {info['narrative']}")
            us_blocks.append("")
        us_blocks.append("**Acceptance Criteria:**")
        for i, e in enumerate(events, start=1):
            given = e.get("description") or "the preconditions for this step are met"
            if info["is_state_view"]:
                when = f"{e['title']} is appended"
                then = f"{rm_join} reflects it"
            elif info["is_fact_only"]:
                when = "the triggering condition occurs"
                then = e["title"]
            else:
                when = cmd_title
                then = e["title"]
            us_blocks.append(f"- AC-{story_num}.{i}: Given {given}, When {when}, Then {then}")
        us_blocks.append("")

        fr_num += 1
        ev_join = " or ".join(e["title"] for e in events) if events else "(no event)"
        ac_refs = ", ".join(f"AC-{story_num}.{i}" for i in range(1, len(events) + 1)) or "—"
        moscow = MOSCOW[priority]
        modal = "MUST" if moscow == "Must" else moscow.upper()
        if info["is_state_view"]:
            req_text = f"System {modal} project {rm_join} from the {ev_join} domain event(s)"
        elif info["is_fact_only"]:
            req_text = f"System {modal} record the {ev_join} domain event(s) under the condition described in US-{story_num}'s narrative"
        else:
            req_text = f"System {modal} support {cmd_title}, producing the {ev_join} domain event(s)"
        fr_rows.append(f"| FR-{fr_num} | {req_text} | {moscow} | {ac_refs} |")

        for el in s["commands"] + s["events"] + s["processors"]:
            agg = el.get("aggregate")
            if agg and agg != "default" and agg not in glossary:
                glossary[agg] = f"Aggregate in the {context} context (see Event Model Detail for the elements that touch it)."
        for rm in readmodels:
            if rm["title"] not in glossary:
                desc = rm.get("description", "")
                glossary[rm["title"]] = (f"Read model. {desc}").strip()

    lines = []
    lines.append("---")
    lines.append(f"spec: {feature_slug}")
    lines.append("phase: requirements")
    lines.append(f"created: {TODAY}")
    lines.append("---")
    lines.append("")
    lines.append(f"# Requirements: {feature_name}")
    lines.append("")
    lines.append("## Problem Statement")
    lines.append("")
    lines.append(
        f"{feature_name} is one of the bounded contexts modeled on the \"BrokerConnect\" "
        f"eventmodelers.ai board (chapter **Broker Connect**, context **{context}**). Evidence: "
        "the board's own live export (`event-model/import-config.json`), transcribed verbatim in "
        "the Event Model Detail appendix below — this is not a hypothesis needing validation, the "
        "domain already exists as a modeled event model."
    )
    lines.append("")
    lines.append("## Goal")
    lines.append("")
    lines.append(
        f"Implement the **{context}** bounded context's commands, events, automations, and read "
        "models exactly as modeled on the board, per constitution Principle III (the spec is the "
        "source of truth — no invented fields, no guessed names)."
    )
    lines.append("")
    lines.append("## User Stories")
    lines.append("")
    lines.extend(us_blocks)
    lines.append("## Functional Requirements")
    lines.append("")
    lines.append("| ID | Requirement | Priority | Acceptance Criteria |")
    lines.append("|----|-------------|----------|---------------------|")
    lines.extend(fr_rows)
    lines.append("")
    lines.append("## Non-Functional Requirements")
    lines.append("")
    lines.append(
        "<!-- The source board does not specify numeric NFR targets for this context — every row is "
        "N/A, not invented, per constitution Principle III. Revisit via a clarification pass before "
        "/ralph-specum:design if any of these genuinely matter for this feature. -->"
    )
    lines.append("")
    lines.append("| ID | Requirement | Metric | Target |")
    lines.append("|----|-------------|--------|--------|")
    lines.append("| NFR-1 | Performance | N/A | N/A: not specified by board export |")
    lines.append("| NFR-2 | Reliability | N/A | N/A: not specified by board export |")
    lines.append("| NFR-3 | Security | N/A | N/A: not specified by board export |")
    lines.append("")
    lines.append("## Glossary")
    lines.append("")
    if glossary:
        for term, definition in glossary.items():
            lines.append(f"- **{term}**: {definition}")
    else:
        lines.append("- No named aggregates/read models beyond the board's placeholder `\"default\"` — see Event Model Detail below.")
    lines.append("")
    lines.append("## Out of Scope")
    lines.append("")
    lines.append("Default-scope rule: anything not listed here that falls under the Goal is in scope.")
    lines.append("")
    lines.append("- The 4 exploratory contexts (Phase B) — not yet pulled from the board, out of scope for this pass.")
    lines.append("- Any command/event/read model not present in the Event Model Detail appendix below.")
    lines.append("")
    lines.append("## Dependencies")
    lines.append("")
    lines.append(
        "- Cross-context dependencies are visible in the Event Model Detail appendix below (each "
        "element's own `Dependencies`/`Aggregate dependencies` lines) but not resolved to spec names "
        "here — cross-reference `elementType`/`title` against the other features' own Event Model "
        "Detail sections manually."
    )
    lines.append("")
    lines.append("## Success Criteria")
    lines.append("")
    lines.append(
        "<!-- The source board does not define measurable success metrics for this context — fill in "
        "via a clarification pass, don't invent numbers. -->"
    )
    lines.append("")
    lines.append("- TBD (user, next review)")
    lines.append("")
    lines.append("## Risks")
    lines.append("")
    lines.append("| Risk | Impact | Mitigation |")
    lines.append("|------|--------|------------|")
    lines.append("| Board export goes stale relative to a live board edit | Medium | Re-run `gen_specs_from_slices.py` against a fresh pull before trusting this file; nothing here watches the board automatically |")
    lines.append("")
    lines.append("## Unresolved Questions")
    lines.append("")
    open_qs = collect_open_questions(slices)
    if open_qs:
        for q in open_qs:
            lines.append(f"- {q} Owner: user, next review")
    else:
        lines.append("- None")
    lines.append("")
    lines.append("## Event Model Detail (Source of Truth)")
    lines.append("")
    lines.append(
        "Full, unabridged transcription of every command, automation/processor, event, and read "
        "model in this context from the static export — every field's type, cardinality, and flags. "
        "This is the lossless source; the User Stories above are a readable summary of it, not the "
        "other way around. Screens are deliberately excluded here — see this spec's `research.md` "
        "(UI Reference section), which the design phase reads directly; screens are UI reference, "
        "not a requirement."
    )
    lines.append("")
    lines.extend(detail_sections)

    return "\n".join(lines) + "\n"


def render_research_md(context, feature_slug, slices):
    feature_name = " ".join(w.capitalize() for w in feature_slug.split("-")[1:])
    screens = collect_screens(slices)

    lines = []
    lines.append("---")
    lines.append(f"spec: {feature_slug}")
    lines.append("phase: research")
    lines.append(f"created: {TODAY}")
    lines.append("---")
    lines.append("")
    lines.append(f"# Research: {feature_name}")
    lines.append("")
    lines.append("## Executive Summary")
    lines.append("")
    lines.append(
        f"This spec is sourced directly from the \"BrokerConnect\" event-modeling board's "
        f"**{context}** context, not free-form research — the domain (commands, events, read "
        "models, and screens) is board-authored, not derived here. This file exists to satisfy "
        "`/ralph-specum:design`'s context-gathering step (it reads `research.md` if present), not "
        "because external/codebase research was actually performed for this feature."
    )
    lines.append("")
    lines.append("## External Research")
    lines.append("")
    lines.append("N/A — requirements are board-sourced, not derived from external research. See `requirements.md`.")
    lines.append("")
    lines.append("## Codebase Analysis")
    lines.append("")
    lines.append("N/A — deferred to the design phase's own codebase exploration (`architect-reviewer`).")
    lines.append("")
    lines.append("## Related Specs")
    lines.append("")
    lines.append("| Spec | Relevance | Relationship | May Need Update |")
    lines.append("|------|-----------|--------------|-----------------|")
    lines.append("| _(not automatically cross-referenced)_ | — | See this context's Event Model Detail appendix in `requirements.md` for raw dependency edges | — |")
    lines.append("")
    lines.append("## Feasibility Assessment")
    lines.append("")
    lines.append("| Aspect | Assessment | Notes |")
    lines.append("|--------|------------|-------|")
    lines.append(f"| Technical Viability | High | Domain already modeled end-to-end on the board ({len(slices)} slices) |")
    lines.append("| Effort Estimate | See `tasks.md` once generated | Not estimated here — board doesn't carry effort data |")
    lines.append("| Risk Level | Low | Board-sourced, not speculative |")
    lines.append("")
    lines.append("## Recommendations for Requirements")
    lines.append("")
    lines.append("1. Treat `requirements.md`'s Event Model Detail appendix as the literal field/event source — no invented fields, per constitution Principle III.")
    lines.append("2. Follow the same Marten/Wolverine conventions as `001-authority-administration` (`build-state-change`/`build-state-view` skills) — this is the same tech-kit, not a new stack decision.")
    lines.append("")
    lines.append("## Open Questions")
    lines.append("")
    open_qs = collect_open_questions(slices)
    if open_qs:
        lines.extend(f"- {q}" for q in open_qs)
    else:
        lines.append("- None")
    lines.append("")
    lines.append("## Sources")
    lines.append("")
    lines.append(
        "- `event-model/import-config.json` — live pull from the \"BrokerConnect\" eventmodelers.ai "
        f"board, chapter **Broker Connect**, context **{context}**, as of {TODAY}."
    )
    lines.append("")
    lines.append("## UI Reference (event-modeling board — for Design phase)")
    lines.append("")
    lines.append(
        "Screens from the board export, for `architect-reviewer` to fold into `design.md`'s own "
        "`## Screens` section — map each screen's displayed fields back to the API/read-model "
        "surface that has to support it. Not a requirement; UI reference only (decided while "
        "adapting this generator for the Ralph Specum workflow: screens belong in design, not "
        "requirements)."
    )
    lines.append("")
    if screens:
        for slice_title, sc in screens:
            lines.append(f"_(from slice: {slice_title})_")
            lines.append("")
            lines.append(element_block(sc, "screen"))
    else:
        lines.append("_(no screens populated on this context's slices in the source export)_")
        lines.append("")

    return "\n".join(lines) + "\n"


def main():
    by_context = load_slices()
    for context, (feature_slug, target) in FEATURES.items():
        slices = by_context[context]
        assert slices, f"no slices found for context {context}"
        out_dir = ROOT / "specs" / feature_slug

        if target == "spec-kit":
            content = render_spec(context, feature_slug, slices)
            out_path = out_dir / "spec.md"
            out_path.write_text(content)
            print(f"wrote {out_path} ({len(slices)} slices, {len(content)} bytes)")
        elif target == "ralph":
            req_content = render_requirements_md(context, feature_slug, slices)
            req_path = out_dir / "requirements.md"
            req_path.write_text(req_content)
            print(f"wrote {req_path} ({len(slices)} slices, {len(req_content)} bytes)")

            research_content = render_research_md(context, feature_slug, slices)
            research_path = out_dir / "research.md"
            research_path.write_text(research_content)
            print(f"wrote {research_path} ({len(slices)} slices, {len(research_content)} bytes)")
        else:
            raise ValueError(f"unknown target {target!r} for context {context!r}")


if __name__ == "__main__":
    main()
