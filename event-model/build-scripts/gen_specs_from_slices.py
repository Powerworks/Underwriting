#!/usr/bin/env python3
"""Generate spec-kit spec.md files from the Underwriting event-model static export.

Source of truth: event-model/import-config.json (the same 25 slices as
event-model/slices/*.json, bundled into one file). Every command/event/
readmodel/processor and every field on them is rendered into the spec so
nothing from the static export is lost.

Run from anywhere (paths are resolved relative to this file); it
overwrites specs/*/spec.md in place. The FEATURES map below encodes this
board's context -> spec-kit-feature grouping — re-check/adjust it if the
board's contexts change. See ../../event-model-to-speckit-guide.md for the
full mapping rationale and how to verify completeness after running this.
"""
import datetime
import json
import re
from collections import OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]  # event-model/build-scripts/ -> event-model/ -> repo root
SRC = ROOT / "event-model" / "import-config.json"
TODAY = datetime.date.today().isoformat()

# context -> (feature number, spec dir slug) — must match the directories
# already created via create-new-feature.sh. This is Phase A / MVP scope only
# (Project Plan/01-project-plan.md §4) — the 4 exploratory contexts (Exposure
# Intelligence, External Threat, Portfolio Governance, Capital & Reinsurance
# Instruments) are Phase B, not yet pulled/generated.
FEATURES = OrderedDict([
    ("Authority Administration", "001-authority-administration"),
    ("Submission Intake", "002-submission-intake"),
    ("Search & Retrieval", "003-search-retrieval"),
    ("Underwriting Decisioning", "004-underwriting-decisioning"),
    ("Binding", "005-binding"),
    ("Bordereaux Settlement", "006-bordereaux-settlement"),
    ("Claims", "007-claims"),
])

NEGATIVE_WORDS = ("Declined", "Rejected", "Referred", "Blocked", "Failed")


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


def build_stories(slices):
    """Returns (story_markdown, fr_lines, edge_case_lines, entities, event_detail_sections, story_count)."""
    stories = []
    fr_lines = []
    edge_lines = []
    entities = OrderedDict()  # aggregate -> set of event/readmodel titles touching it
    readmodel_entities = OrderedDict()  # readmodel title -> (description, fields)
    detail_sections = []
    fr_num = 0
    story_num = 0

    for s in slices:
        story_num += 1
        events = s["events"]
        readmodels = s.get("readmodels", [])

        # sliceType is missing on some board exports for standalone alternate-outcome
        # events (a command's happy-path lives in one slice; its "declined"/"referred"/
        # "for-cause" counterpart event got modeled as its own slice with no command,
        # processor, or readmodel of its own). Infer a type rather than crash — see
        # ../../event-model-to-speckit-guide.md, "Slices with no sliceType" for the
        # empirical basis of this heuristic.
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

        title = s["title"]
        narrative = actor.get("description") if actor else (
            (readmodels[0].get("description") if readmodels else None)
            or (events[0].get("description") if events else "")
        )

        stories.append(f"### User Story {story_num} - {title} (Priority: {priority})")
        stories.append("")
        if is_automation:
            stories.append(
                f"As a background policy in **{s['context']}**, the system reacts by executing "
                f"{bold(actor['title']) if actor else '(automation)'}, producing "
                + " or ".join(bold(e['title']) for e in events) + "."
            )
        elif is_state_view:
            rm_join = " and ".join(bold(r["title"]) for r in readmodels) if readmodels else "a read model"
            stories.append(
                f"The system maintains {rm_join}, projected from "
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
        cmd_title = actor["title"] if actor else (
            "(projection, no command)" if is_state_view else
            "(no command in source — see narrative)" if is_fact_only else
            "(automation trigger)"
        )
        if is_state_view:
            stories.append(
                f"**Independent Test**: Can be tested by appending "
                + " and/or ".join(bold(e["title"]) for e in events)
                + f" and asserting that {rm_join} reflects the update."
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
                stories.append(f"{i}. **Given** {given}, **When** {e['title']} is appended, **Then** {rm_join} reflects it")
            elif is_fact_only:
                stories.append(f"{i}. **Given** {given}, **When** the triggering condition occurs, **Then** {e['title']}")
            else:
                stories.append(f"{i}. **Given** {given}, **When** {cmd_title}, **Then** {e['title']}")
        stories.append("")
        stories.append("---")
        stories.append("")

        # FRs — one per command/processor/projection, covering all its events
        fr_num += 1
        ev_join = " or ".join(bold(e["title"]) for e in events) if events else "(no event)"
        if is_state_view:
            fr_lines.append(f"- **FR-{fr_num:03d}**: System MUST project {rm_join} from the {ev_join} domain event(s).")
        elif is_fact_only:
            fr_lines.append(f"- **FR-{fr_num:03d}**: System MUST record the {ev_join} domain event(s) under the condition described in its narrative (source export has no command/processor of its own for this outcome).")
        else:
            fr_lines.append(f"- **FR-{fr_num:03d}**: System MUST support {bold(cmd_title)}, producing the {ev_join} domain event(s).")

        # Edge cases — any event beyond the first, or any event with a negative-sounding title,
        # or the whole slice if it's a P3 off-ramp. Not applicable to pure projections.
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

        # Entities: aggregates touched by this slice's commands/events. "default" is
        # the board's un-set placeholder (confirmed: literally every element on this
        # board carries it — see event-model-to-speckit-guide.md, "The aggregate field
        # is unusable on this board"), not a real aggregate name — skip it rather than
        # bucket the entire feature under a fake "default" entity.
        all_elements = (s["commands"] + s["events"] + s["processors"])
        for el in all_elements:
            agg = el.get("aggregate")
            if not agg or agg == "default":
                continue
            entities.setdefault(agg, set()).add(el["title"])

        # Read models are their own entity class
        for rm in s.get("readmodels", []):
            readmodel_entities[rm["title"]] = (rm.get("description", ""), rm.get("fields", []))

        # Full detail appendix — every command/event/readmodel/processor, every field
        type_label = raw_slice_type or f"{slice_kind} (inferred — sliceType missing in source export)"
        block = [f"### Slice: {title} (`{s['id']}`, status: {s['status']}, type: {type_label})", ""]
        for c in s["commands"]:
            block.append(element_block(c, "command"))
        for p in s["processors"]:
            block.append(element_block(p, "automation/processor"))
        for e in s["events"]:
            block.append(element_block(e, "event"))
        for rm in s.get("readmodels", []):
            block.append(element_block(rm, "read model"))
        detail_sections.append("\n".join(block))

    return "\n".join(stories), fr_lines, edge_lines, entities, readmodel_entities, detail_sections


def render_spec(context, feature_slug, slices):
    feature_num = feature_slug.split("-")[0]
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


def main():
    by_context = load_slices()
    for context, feature_slug in FEATURES.items():
        slices = by_context[context]
        assert slices, f"no slices found for context {context}"
        content = render_spec(context, feature_slug, slices)
        out_path = ROOT / "specs" / feature_slug / "spec.md"
        out_path.write_text(content)
        print(f"wrote {out_path} ({len(slices)} slices, {len(content)} bytes)")


if __name__ == "__main__":
    main()
