# Mapping an eventmodelers.ai event model to a spec-kit feature set

Reference for turning an eventmodelers.ai board into a GitHub spec-kit
(`specify`) project: one `spec.md` per bounded context, with full
field-level fidelity to the source model. Written up after doing this
twice against the same repo — once against a stale 25-slice/6-context
static export, then again (this time correctly) against a live 65-slice/
7-context pull of the actual "BrokerConnect" board — so the second half of
this guide is as much about *not trusting a static export blindly* as it
is about the mechanics. Treat the mechanics as confirmed; treat the
mapping *rules* (priorities, grouping) as one reasonable convention, not
the only valid one — adjust them per board.

## Check live API reachability before assuming you need a static export

`build-kit-dotnet-es` and the `connect` skill assume a live
`.eventmodelers/config.json` (`token`/`boardId`/`orgId`/`baseUrl`) and call
`https://api.eventmodelers.ai` directly. It's tempting to assume "we're
waiting on a license" means the API is unreachable and fall back to
whatever static export is lying around — **don't assume, check**:

```bash
curl -s https://api.eventmodelers.ai/api/boards \
  -H "x-token: <TOKEN>" -H "x-board-id: <any-uuid>" -H "x-user-id: check"
```

If this returns `200` with a board list, the API is reachable *right now*
with that token — whatever licensing issue prompted the assumption may be
scoped to something else entirely (a UI seat, a specific paid feature, a
different token/account) and may already be resolved without anyone
telling you. In this repo, that's exactly what happened: a static export
had been used for a full round of spec generation before anyone re-checked
reachability, and the API had been working the whole time.

If a live board isn't reachable and no static export exists either,
you're genuinely blocked — there's no other source for board content.

## The real export endpoint — and the one that looks right but isn't

The `learn-eventmodelers-api` skill documents `GET
/api/org/:orgId/boards/:boardId/events` ("Get all board events in
sequence") right next to the boards endpoints — it's tempting to reach for
it as "the export," and it does return `200` with real data. **Don't use
it for this.** It's the platform's own raw event-sourcing log (every
`node:created`/`node:changed`/`node:deleted`/`edge:added` mutation, in
ReactFlow-node terms) — on this board, 1928 raw events serializing to
16MB, none of it in slice/command/event/field shape. Turning it into
something usable means replaying the log yourself (track the latest
non-deleted state per `node_id`, dedupe types, reconstruct edges) and you
still don't get clean command/event/field records out of it — just
ReactFlow node positions and metadata blobs.

The correct endpoint is under "8. Slice Data" in the same skill doc, and
is easy to miss because its own heading is a blank `GET` with the path in
the query-param description rather than the URL:

```
GET /api/org/:orgId/boards/:boardId/slicedata?contextName=<name>
GET /api/org/:orgId/boards/:boardId/slicedata/slices        # list all slices: {id, title, status, contextName, sliceType}
```

This returns data **already in the same `{"slices": [...]}` shape as
`import-config.json`** — the schema this whole guide is written against.
Pull the slice list first (cheap, tells you real per-context slice counts
before committing to anything), then pull full data per context (or per
`sliceId`, if you need just one slice) and merge:

```python
contexts = [...]  # from the slice-list call's distinct contextName values
merged = {"slices": []}
for ctx in contexts:
    r = requests.get(f"{BASE}/api/org/{ORG}/boards/{BOARD}/slicedata",
                      params={"contextName": ctx}, headers=HEADERS)
    merged["slices"].extend(r.json()["slices"])
```

## Cross-check any export against what's already in the repo before trusting it

The single biggest mistake made in this pass: treating the 25-slice/
6-context static `event-model/import-config.json` as complete just because
a completeness check passed against it (see Verifying completeness below —
that check is only ever as good as its source). This repo already had
`scenarios.md` (70 GWT-narrative sections) and a full `Requirements/*.md`
suite (`00-Overview.md` through `15-Completeness-Check.md`, FR-numbered,
with its own 34-item Open Decisions Register) sitting right next to the
static export, both stating outright that the *real* model has **363
nodes across 11 bounded contexts, 98 GWT scenarios** — nothing close to
25/6. A live pull confirmed it: **65 slices across the 7 confirmed
("Phase A") contexts alone**, more than double the static export, with
entire slices (an escalation command, a whole automation) and entire read
models missing from what had been treated as ground truth.

**Before generating anything from an export you didn't just pull
yourself, grep the repo for independent corroboration of its scale** —
`grep -rn "nodes\|scenarios\|slices\|commands\|events" Requirements/
scenarios.md 'Project Plan/' 2>/dev/null` and read whatever comes back. If
another document in the repo states different totals than your export,
trust neither blindly — go pull live and compare, the way this session
eventually did. A completeness check that only verifies "the spec matches
its own source" can't catch "the source itself is a partial snapshot" —
that requires an outside reference point.

## Source shape: what one slice contains

Each slice (`event-model/import-config.json`'s `slices[]`, or the single
entry in a `event-model/slices/<id>.json`) has this top-level shape:

```
{
  "id": "submit-broker-submission",       // kebab-case, stable
  "title": "Submit Broker Submission",    // human-readable
  "status": "Created",                    // board workflow status — see caveat below
  "sliceType": "STATE_CHANGE" | "AUTOMATION" | "STATE_VIEW",  // sometimes missing — see below
  "chapter": "Broker Connect",            // top-level board grouping
  "context": "Submission Intake",         // bounded-context grouping, sits under chapter
  "commands": [ ... ],                    // STATE_CHANGE: 0 or 1
  "events": [ ... ],                      // 1 or more — see branching note below
  "readmodels": [ ... ],                  // usually empty; present when a slice reads a projection
  "processors": [ ... ],                  // AUTOMATION: 1, replaces `commands`
  "screens": [], "tables": [], "specifications": [],
  "actors": [], "aggregates": [], "screenImages": [], "comments": []
}
```

The last line of buckets (`screens`, `tables`, `specifications`, `actors`,
`aggregates`, `screenImages`, `comments`) were **still empty on every one
of the 65 Phase-A slices** in the live pull, same as the earlier partial
export. The schema (`event-model/eventmodeling.schema.json`) supports
them — if your board actually uses screens, GWT `specifications`, or named
`actors`, the generator described below does not currently render them
and you'll need to extend it. Don't assume "empty in our export" means
"the generator handles it" — it means "we never exercised that path." (The
`Requirements/00-Overview.md` mentions "11 designed screens" exist
somewhere on the full board — they may live under a different context, or
in the Phase B exploratory contexts not yet pulled; don't assume "empty in
what I pulled" means "doesn't exist on the board.")

### `sliceType` is sometimes missing entirely

On the live pull, 15 of 65 slices had no `sliceType` key at all — every
one of them a standalone alternate/terminal-outcome event with no command,
processor, or readmodel of its own (e.g. `ReferralDeclined`,
`PolicyCancelledForCause`, `PotentialDuplicateSubmissionDetected`). These
represent a fourth slice shape beyond `STATE_CHANGE`/`AUTOMATION`/
`STATE_VIEW`: a bare fact, produced by a decision made in some *other*
slice's command, split onto the board as its own node rather than bundled
as a second event on that command (contrast with `grant-underwriter-
authority-limit`, where the rejection event lives on the *same* slice as
the granting command — see Branching below; both patterns coexist on the
same board). Infer this case (`not sliceType and not commands and not
processors and events`) rather than crashing on the missing key or
silently mis-rendering it as a command-driven story with no command.

### `aggregate` can be a useless placeholder

Every element (command/event/readmodel/processor) carries an `aggregate`
field — on the earlier partial export these were meaningful (`AuthorityLimit`,
`Submission`, ...); on the live pull, **100% of all 118 elements across
Phase A carry the literal string `"default"`**, i.e. never customized.
Grouping "Key Entities" by this field silently produces one fake `default`
bucket touching every command/event in the feature — worse than useless,
actively misleading. Check `Counter(el['aggregate'] for ... )` before
trusting this field for anything; treat `"default"` as equivalent to
missing, and fall back to readmodel titles (which *are* real) plus a note
that aggregate/stream boundaries need deciding at `/speckit-plan` time
from the field shapes themselves.

### `dependencies` — when populated, it's structured, not a string

Also empty on the earlier partial export; populated on the live pull, as
a list of `{id, type: "INBOUND"|"OUTBOUND", title, elementType}` edge
objects — the board's own dependency graph between elements. Render it as
prose (`← Title (TYPE)` / `→ Title (TYPE)`), not `str(the_list)` — a raw
Python/JS repr of a list-of-dicts in the middle of a spec reads as broken
formatting, not data.

### Duplicate slice titles are real

Two different slices can share the same `title` — e.g. `CellAuthorityLimitGranted`
appears once as the `STATE_CHANGE` slice with the granting command, and
again as a `STATE_VIEW` slice pairing the same event with the
`CellAuthorityRegister` projection. They have different `id`s and are
genuinely different slices. If you're writing one file per slice (as this
repo's `event-model/slices/*.json` convention does), slugifying by title
alone silently collides and overwrites — disambiguate with `sliceType` or
a counter suffix when the title isn't unique.

### Element shape (command / event / readmodel / processor)

All four bucket types share one element shape:

```
{
  "id": "submit-broker-submission-cmd",
  "title": "SubmitBrokerSubmission",
  "type": "COMMAND" | "EVENT" | "READMODEL" | "AUTOMATION",
  "fields": [ ... ],
  "aggregate": "Submission",              // the entity/stream this belongs to
  "lane": "Interaction" | "Swimlane" | "Actor",
  "modelContext": "Submission Intake",    // == the slice's `context`, redundantly
  "description": "Broker submits a new risk into the platform. ...",
  "dependencies": [], "aggregateDependencies": [], "triggers": [], "tags": [],
  ... several other always-empty/always-false bookkeeping flags
}
```

`description` is free text written by whoever built the board and is
consistently the single most useful field — it reads like a domain expert's
note-to-self, not boilerplate ("Cannot exceed the Cell's own granted limit
for that class of business"). Pull it into the spec verbatim; don't
paraphrase it away.

`triggers` and `tags` were empty on every element in both pulls.
`dependencies`/`aggregateDependencies` were empty on the earlier partial
export but populated on the live Phase A pull — see "`dependencies` — when
populated, it's structured, not a string" above; the generator renders
`dependencies` (not yet `aggregateDependencies`, `triggers`, or `tags` —
extend it if your board populates those).

### Field shape

```
{
  "name": "authorityLimitId",
  "type": "String" | "UUID" | "Date" | "DateTime" | "Decimal" | "Integer" | "Boolean" | "Custom",
  "cardinality": "Single" | "List",
  "optional": false,
  "idAttribute": true,      // this field identifies the aggregate instance
  "generated": true,        // system-generated (timestamps, derived ids), not caller-supplied
  "subfields": []            // populated when cardinality is List of an object, e.g. quotaShareSplits
}
```

Several other flags exist (`edited`, `query`, `showAttributes`,
`technicalAttribute`) — in this export `edited` was `true` on effectively
every field and the other three were `false` on all of them, so none of
them carried signal worth surfacing in the spec. `idAttribute`, `generated`,
and `optional` are the three worth rendering.

**Nested fields are real.** `List`-cardinality fields on complex data
(`bind-policy`'s `quotaShareSplits`, `raise-bordereau-query`'s readmodel
`lines`) carry a populated `subfields` array with the same field shape,
recursively. Don't flatten these away — a `quotaShareSplits: List<Custom>`
field with no subfield detail loses the fact that each entry is a
`(provider: String, percentage: Decimal)` pair.

### Branching: multiple events per command

A single `STATE_CHANGE` slice can have **one command producing two
alternative events**, not two separate commands. E.g.
`grant-underwriter-authority-limit`'s one `GrantUnderwriterAuthorityLimit`
command produces either `UnderwriterAuthorityLimitGranted` (happy path) or
`UnderwriterAuthorityLimitRejected` (the requested limit exceeds the
cell's own delegation). This is different from how some other boards model
rejection as its own separate command/slice (e.g. this same board's
`decline-submission` **is** its own standalone slice with no positive
counterpart — a genuine off-ramp, not a branch).

When mapping to spec-kit, treat these as two different things:
- **Branch** (one command, 2+ events): one User Story, one acceptance
  scenario per event, and add the non-first event(s) to Edge Cases too.
- **Off-ramp** (a whole slice whose only event is a rejection/decline):
  its own User Story at lower priority (P3), *and* an Edge Cases entry.

Heuristic used here to tell them apart automatically: an event is
"negative" if it's not the first event on its command AND/OR its title
contains one of `Declined`, `Rejected`, `Referred`, `Blocked`, `Failed`.
A whole slice is an off-ramp if it's the only event on its only command and
matches that same word list — in this export that was just
`decline-submission`. This is a naming-convention heuristic, not something
the schema states explicitly — re-check it by eye against your own board's
vocabulary before trusting it.

## Grouping slices into spec-kit features

spec-kit wants `specs/NNN-slug/spec.md` per feature, numbered sequentially.
The board gives you two grouping levels — `chapter` (coarse) and `context`
(bounded-context-sized). **Use whichever level actually varies.** This
board has all 88 slices under a single chapter (`Broker Connect`) but 11
distinct contexts — so `context` is the right grouping here, one spec-kit
feature per context. A multi-chapter board (e.g. the powergym board, which
produced `specs/002-membership`, `specs/003-bookings`, etc.) would group by
`chapter` instead. Check `set(s['context'] for s in slices)` vs.
`set(s['chapter'] for s in slices)` before deciding — don't assume.

Order the features by the domain's natural lifecycle if there is one
(here: authority setup → submission → search/retrieval → decisioning →
binding → bordereaux settlement, with claims last) rather than
alphabetically — it makes the `specs/NNN-...` numbering read as a coherent
pipeline.

**Also check for an explicit MVP/phase scope before deciding how much to
generate at once.** This repo's `Project Plan/01-project-plan.md` §4
already defines the split most projects would otherwise have to invent:
7 "confirmed" contexts (Authority Administration, Submission Intake,
Search & Retrieval, Underwriting Decisioning, Binding, Bordereaux
Settlement, Claims — 65 slices) as Phase A/MVP, and 4 "exploratory"
contexts (Exposure Intelligence, External Threat, Portfolio Governance,
Capital & Reinsurance Instruments — 23 slices) as Phase B, contingent on a
later go/no-go. Generating only Phase A's 7 features first (`specs/001`
through `specs/007`) and leaving room to add Phase B's 4 later (`specs/008`
onward) tracks the project's own stated scope instead of dumping all 11
contexts in regardless of what's actually meant to be built first. If your
project has no such document, this is a reasonable question to raise with
the human before generating everything at once, not just default to "all
of it."

## Setting up spec-kit itself

This part has nothing board-specific about it:

```bash
cd <target-repo>
specify init --here --integration claude --script sh --force   # --force only needed if the dir isn't empty
```

This installs `.specify/` (templates, scripts, memory/constitution) and
`.claude/skills/speckit-*` — verified byte-for-byte structurally identical
to what a from-scratch `specify init` produces (compared against the
powergym repo's install). It does **not** require the target repo to be a
git repository — `create-new-feature.sh` happily creates
`specs/NNN-slug/spec.md` on a plain filesystem with no `.git` present; git
branch creation is an optional hook (`before_specify`) that silently no-ops
without git, not a hard dependency.

Create each feature directory explicitly rather than letting
`/speckit-specify` invent the slug from a prompt, so the numbering and
naming match your grouping decision exactly:

```bash
bash .specify/scripts/bash/create-new-feature.sh --json --short-name "authority-administration" \
  "Authority Administration: grant, revise, and revoke underwriting authority limits"
```

`--json` prints `{"BRANCH_NAME", "SPEC_FILE", "FEATURE_NUM"}`; run it once
per feature, in your chosen order — numbering is sequential and automatic.
Each call also overwrites `.specify/feature.json` to point at the
just-created feature; that file is what `/speckit-clarify` and
`/speckit-plan` treat as "the current feature" by default, so the **last**
feature you create becomes the default target for those commands until you
point them elsewhere.

## Generating spec.md content programmatically

Given structured source data, don't hand-write the specs or lean on
`/speckit-specify`'s free-text generation — write a small script that reads
the export and renders each `spec.md` directly. This is the only way to
*guarantee* nothing gets lost, and it's mechanically checkable afterwards
(see Verifying completeness below). The script used here lives at
[`event-model/build-scripts/gen_specs_from_slices.py`](event-model/build-scripts/gen_specs_from_slices.py)
— rerun it any time the export is refreshed from a live board pull; it
overwrites `specs/*/spec.md` in place.

Mapping rules it encodes, spelled out so you can adjust them for a
different board:

- **One User Story per slice, and the slice's kind drives the phrasing.**
  Four kinds, inferred as described in "Source shape" above:
  `STATE_CHANGE` (command-driven) → P1, "primary, human-initiated action";
  `AUTOMATION` (processor-driven) → P2, "system-driven policy"; `STATE_VIEW`
  (a bare event feeding a readmodel, no command) → P2, phrased as "the
  system maintains `<readmodel>`, projected from `<event>`" rather than a
  user action; `FACT_ONLY` (a standalone alternate-outcome event with no
  command/processor/readmodel at all — the inferred fourth kind) → P3,
  phrased as "the system records `<event>`" with the triggering condition
  left generic since the source genuinely doesn't name a driving command.
  A pure off-ramp `STATE_CHANGE` slice (see Branching above) is also P3.
- **One Acceptance Scenario per event** on that slice's command/processor —
  `Given <event's own description>, When <command title>, Then <event
  title>` (or, for `STATE_VIEW`, `When <event> is appended, Then <readmodel>
  reflects it`). Using the event's description as the *Given* works because
  these descriptions are typically written as "what has to be true for this
  outcome," which is exactly a Given clause.
- **Edge Cases** section gets one bullet per non-first/negative-titled
  event, every P3 off-ramp slice, and every `FACT_ONLY` slice's event(s).
- **Functional Requirements**: one `FR-NNN` per command/processor/projection
  (not per event) — "System MUST support `<Command>`, producing the `<Event
  A>` or `<Event B>` domain event(s)" (or "MUST project `<readmodel>` from
  `<event>`" for `STATE_VIEW`).
- **Key Entities**: one bullet per distinct `aggregate` name encountered
  across the feature's commands/events — **skip this if `aggregate` is a
  useless placeholder like `"default"`** (see Source shape above) rather
  than emit a misleading catch-all bucket — plus one bullet per `readmodel`
  (read models are called out separately since they're structurally
  different from write-side aggregates — this project's convention is
  event-sourced aggregates, Marten-document read models, see
  `build-kit-dotnet-es/README.md`).
- **Event Model Detail** (appendix, not part of the standard spec-kit
  template): a full, unabridged transcription of every command / processor
  / event / readmodel and every one of their fields (type, cardinality,
  `id`/`generated`/`optional` flags, recursing into subfields), grouped by
  slice. This section is what makes "every element from the export is in
  the spec" a checkable claim rather than a hope — the narrative User
  Story prose above it is a *readable summary*, this appendix is the
  *lossless* one.

Everything else (Success Criteria numeric targets, out-of-scope
assumptions) the board simply doesn't specify — leave the standard spec-kit
template placeholders (`[Measurable metric, e.g., ...]`) in place rather
than inventing numbers, and let `/speckit-clarify` fill them in with the
human.

## The constitution: adapt, don't regenerate

`specify init` seeds `.specify/memory/constitution.md` from the generic
`[PROJECT_NAME] Constitution` template — all placeholders, no content.
`/speckit-constitution` will happily fill it in interactively, but if a
sibling project in the same repo (or same org) already has a constitution
built for the **same tech-kit** (here: `build-kit-dotnet-es`, event-sourced
Wolverine.Http + Marten + RabbitMQ), running the interactive flow from
scratch just re-derives principles that are already correct and already
battle-tested — Event-Sourced Everywhere, the three vertical-slice shapes,
test-first in three layers, async correctness, and so on are properties of
the *kit*, not of the specific board/domain sitting on top of it.

What we did here: diffed powergym's constitution.md against the raw
template, confirmed the only project-specific string in the whole file was
the H1 title ("Powergym Constitution" — everything else, including the
worked examples, was already generic to any `build-kit-dotnet-es`
consumer), and copied it into Underwriting with just the title, the
adaptation note, and the version/ratified/amended fields changed (fresh
`1.0.0`, not a continuation of powergym's `1.2.0` — it's a new document
now, even though the content is inherited). **Grep the source constitution
for the other project's name before copying** — if it comes back clean
(like it did here), a copy-and-rename is the right call; if the source
constitution has grown domain-specific principles (e.g. gym-membership
concepts leaking into "Core Principles"), you'd need to hand-pick which
sections actually transfer.

`/speckit-clarify`, by contrast, is *not* a copy-and-adapt candidate — it's
inherently interactive and per-spec: it scans one feature's actual
ambiguities (this board's domain, this feature's fields and branches) and
asks targeted questions about *this* content, not the tech stack. There's
nothing upstream to inherit it from. Run it fresh, per feature, the normal
way.

## Verifying completeness

After generating, don't eyeball it — check it. For every slice, for every
element in every bucket, assert the element's `id`, `title`, `description`,
and every field name (including nested `subfields`, recursively) is a
substring of the corresponding feature's `spec.md`:

```python
for s in slices:
    text = open(f"specs/{feature_slug(s)}/spec.md").read()
    assert s["id"] in text
    for bucket in ["commands", "events", "readmodels", "processors"]:
        for el in s.get(bucket, []):
            assert el["id"] in text and el["title"] in text
            if el.get("description"):
                assert el["description"] in text
            # recurse into fields/subfields, assert each `name` in text
```

This is a substring check, not semantic — it won't catch a field that's
present but mis-described, and it will false-negative if you paraphrase a
description instead of quoting it verbatim (another reason to quote
descriptions verbatim in the Event Model Detail appendix, even if the
narrative prose above summarizes them). It's cheap, mechanical, and it's
the check that actually matters for "did the *generator* lose anything
from its *source*" — run it every time you regenerate. It cannot answer
the different question "is the source itself complete" — that's what the
cross-checking step earlier in this guide is for; the two checks catch
different failure modes and neither substitutes for the other.

## Caveats / things not yet exercised

- **Board `status` field** (`Created`/`Planned`/etc.) wasn't load-bearing
  here — all 65 Phase A slices were `Created`, so every generated feature
  just notes "none are Planned or built yet" in Assumptions. If your export
  has a mix of statuses, decide whether `Planned`/built slices should be
  reflected differently in the spec (e.g. already-implemented behavior
  documented as given, not proposed).
- **Nothing here re-pulls automatically.** The specs are only as current as
  the last time `gen_specs_from_slices.py` was run against a fresh
  `import-config.json` — if the board changes after that, the specs go
  stale silently. There's no watcher; re-pulling and re-running is a
  manual step, and re-running it after `/speckit-clarify` has already
  touched a spec will blow away its `## Clarifications` section (see below).
- **Phase B (the 4 exploratory contexts, 23 slices) is not pulled or
  generated yet** — this pass deliberately scoped to Phase A only, per
  `Project Plan/01-project-plan.md` §4. Extend `FEATURES` in
  `gen_specs_from_slices.py` and re-pull those contexts' `slicedata` when
  Phase B is greenlit.
- **Screens / actors / GWT `specifications`**: schema-supported, still
  empty on every Phase A slice even in the live pull, unhandled by the
  generator (see Source shape above). If you hit a context that populates
  these, extend `gen_specs_from_slices.py` before trusting its output
  completeness-checkable claim for that context.

## Regenerating after `/speckit-clarify`: reconcile, don't blindly overwrite

`gen_specs_from_slices.py` overwrites `specs/*/spec.md` wholesale — it has
no idea a `## Clarifications` section exists and will silently delete one
if you re-run it after `/speckit-clarify` already ran on that feature.
This happened here: Authority Administration was clarified (4 questions)
against the stale 25-slice export, the export was then replaced with the
real 65-slice pull, and regenerating for the richer data wiped the
Clarifications section along with everything else.

What saved the work was checking each clarification against the *new*
source **before** re-adding it, rather than pasting the old answers back
unexamined: 3 of 4 held up untouched (the new data didn't contradict them,
and in fact the richer export still had no rejection event for two of the
gaps they'd flagged, confirming those gaps were real board gaps, not
artifacts of a thin export). The 4th had to be corrected — it referenced
a merged `AuthorityLimit` read model that turned out not to exist on the
real board, which actually has three distinct read models
(`AuthorityMatrix`/`CellAuthorityRegister`/`UnderwriterAuthorityRegister`);
the consistency requirement only ever applied to `AuthorityMatrix`
specifically, and the clarification's *content* was still correct once
re-pointed at the right entity.

So: regenerating from a richer source doesn't necessarily invalidate prior
clarifications, but it can silently narrow or misname what they were
about. Re-verify each one against the new Event Model Detail appendix
before re-inserting it — don't paste blindly, and don't discard blindly
either.
