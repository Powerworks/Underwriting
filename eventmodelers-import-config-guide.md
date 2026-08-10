# Populating and importing an eventmodelers-native.json into app.eventmodelers.ai

Reference for converting a domain model (from whatever format you have it
in — a bespoke JSON schema, a DSL, hand-written notes) into the JSON format
`app.eventmodelers.ai`'s bulk import endpoint actually accepts, and for
calling that endpoint correctly. Everything here was reverse-engineered
empirically — by reading real data back out of a populated board and
round-tripping it through the import endpoint — because this format is
**not documented anywhere** in the platform's own `/swagger.json` or public
docs. Treat unconfirmed pieces (flagged below) as best-effort, not gospel.

## Prerequisites

You need, for the target board:
- `TOKEN` — API token (`x-token` header)
- `BOARD_ID` — target board UUID
- `ORG_ID` — organization UUID
- `BASE_URL` — e.g. `https://api.eventmodelers.ai`

If you're working in a repo that already has the `connect` eventmodelers
skill, run that first — it resolves all four from `.eventmodelers/config.json`
or asks the user.

## The endpoint

```
POST <BASE_URL>/api/org/<ORG_ID>/boards/<BOARD_ID>/import-config
Content-Type: application/json

{ "slices": [ ... ] }
```

Headers:
```
x-token: <TOKEN>
x-board-id: <BOARD_ID>
x-user-id: <any string identifying the caller>
```

**Important discrepancy:** the platform's own `/swagger.json` documents this
route as `POST /api/boards/{boardId}/import-config` — no `/org/:orgId/`
segment. That path **404s in practice**. The org-scoped path above is the
one that actually works. Don't trust the swagger doc's path for this one
endpoint; trust this file.

Response on success: `200` with a full ReactFlow-style canvas (`{ boardId,
timelineId, nodes: [...], edges: [...] }`) — the server's own generated
node ids, not the ones you sent. On failure: `400` with a parse/validation
error.

### Critical behavior: this is a replace, not a merge

Confirmed by running two throwaway single-slice imports back to back: the
first import's nodes were completely gone once the second was posted, with
no explicit delete call in between. **Every `import-config` call replaces
the board's entire content** (or at least everything under the same
chapter/timeline scheme — behavior wasn't tested per-chapter). Don't call
it more than once per board unless you mean to overwrite. If you need to
add to existing content, use the incremental node/chapter/slice APIs
instead (see the `learn-eventmodelers-api` skill / this repo's API
reference for those).

### Payload size: keep the request body compact

Confirmed by testing (Pine Walk / Broker Connect model, 21 slices, ~120 elements):
a **pretty-printed** multi-slice payload of ~142KB (2-space indent via
`JSON.stringify(data, null, 2)`) returned a bare `HTTP 500 {"error":"Internal
server error"}` with no useful detail, in well under a second (not a timeout).
The exact same slice content serialized **compactly** (`JSON.stringify(data)`,
no whitespace) came to ~86KB and imported successfully in a single call.
Splitting the pretty-printed payload in half also worked at ~44KB each. This
points to a server-side body-size ceiling somewhere between ~90KB and
~140KB — **always POST `import-config` bodies compactly, never
pretty-printed**, especially once you're importing more than a handful of
slices at once. If you hit an unexplained 500 with no validation detail on a
bulk import, minifying the JSON before re-sending is the first thing to try,
before assuming the payload content itself is invalid.

### Verifying an import worked

```
GET <BASE_URL>/api/org/<ORG_ID>/boards/<BOARD_ID>/nodes
```
Count node types back and compare against what you expect (e.g. N commands
+ N-or-more events + M read models + one SLICE_BORDER per slice + 1
CHAPTER). An empty board after posting import-config with a 200 response
usually means you hit the wrong path (see discrepancy above) or the
importer silently accepted a body it couldn't fully interpret — always
verify with a `GET`, don't trust the 200 alone.

## Top-level shape

```json
{ "slices": [ <Slice>, <Slice>, ... ] }
```

## `Slice` object

```json
{
  "id": "any-string-id",
  "title": "Human-readable slice name",
  "status": "Done | InProgress | Created | Planned | ...",
  "sliceType": "STATE_CHANGE | STATE_VIEW | AUTOMATION",
  "chapter": "Name of the timeline/chapter this slice belongs to",
  "context": "Name of the MODEL_CONTEXT this slice belongs to",
  "commands": [ <Element> ],
  "events": [ <Element> ],
  "readmodels": [ <Element> ],
  "screens": [ <Element> ],
  "processors": [ <Element> ],
  "tables": [ <Element> ],
  "specifications": [ <Specification> ],
  "actors": [],
  "aggregates": [],
  "screenImages": [],
  "comments": []
}
```

Notes:
- `sliceType` maps to what kind of elements populate the slice:
  `STATE_CHANGE` → commands + events, `STATE_VIEW` → read models, no
  commands/events, `AUTOMATION` → processors + events/commands.
- **All slices sharing the same `chapter` value land on one timeline**,
  each slice becoming one column. If you want everything on a single board
  as one big timeline, give every slice the same `chapter` string. If you
  want separate timelines/swimlanes per feature area, vary it.
  **Recommendation, learned the expensive way**: vary it, one chapter per
  bounded context / `MODEL_CONTEXT`. A single shared chapter works fine
  early on but does not scale - past a few dozen columns it becomes
  unwieldy, and critically, **there is no API to move a node between
  chapters once it exists**. Undoing a single-chapter choice later means
  fully recreating every node's placement in new chapters, not a migration.
  Decide this up front. See `event-model/build-scripts/migrate_chapters.py`
  and its README for what that recreation actually involves if you're stuck
  needing to do it anyway.
- `actors` / `aggregates` / `screenImages` at the slice level were empty
  `[]` in every real board sampled during reverse-engineering, even for
  slices that clearly had actors and aggregates conceptually — that
  information lives on the individual elements instead (`aggregate` field
  on commands/events, actor context implied by `screens`). Leaving these
  four arrays empty is safe and matches observed real data; their exact
  purpose when populated is unconfirmed.

## `Element` object (used in `commands`/`events`/`readmodels`/`screens`/`processors`/`tables`)

```json
{
  "id": "any-string-id",
  "title": "Element name",
  "type": "COMMAND | EVENT | READMODEL | SCREEN | AUTOMATION | TABLE",
  "fields": [ <Field> ],
  "dependencies": [],
  "modelContext": "same as parent slice's context",
  "lane": "Actor | Interaction | Swimlane",
  "aggregate": "aggregate/entity name, or \"default\"",
  "aggregateDependencies": [],
  "triggers": [],
  "tags": [],
  "slice": "must equal the parent slice's title",
  "description": "free text — good place for implementation status/notes",
  "context": "INTERNAL",
  "elementContext": "INTERNAL",
  "listElement": false,
  "todoList": false,
  "createsAggregate": false,
  "elementCopy": false,
  "sketched": false,
  "comments": []
}
```

`lane` convention (matches the timeline's row types):
| Array | `type` | `lane` |
|---|---|---|
| `commands` | `COMMAND` | `Interaction` |
| `events` | `EVENT` | `Swimlane` |
| `readmodels` | `READMODEL` | `Interaction` |
| `screens` | `SCREEN` | `Actor` |
| `processors` | `AUTOMATION` | `Actor` |

`dependencies` / `aggregateDependencies` / `triggers`: left empty `[]` in
every real sample seen. Cross-element relationships appear to be
established implicitly by grouping elements under the same `slice`, not by
populating these arrays on import. Unconfirmed what format they'd take if
populated — don't guess at it without testing first.

`createsAggregate: true` on a command signals it's the one that
instantiates a new aggregate instance (vs. one that acts on an existing
one) — set it to match your domain's actual create-vs-modify commands.

## `Field` object

```json
{
  "name": "fieldName",
  "type": "String | Integer | Long | UUID | DateTime | Boolean | Custom | ...",
  "cardinality": "Single | List",
  "optional": false,
  "idAttribute": false,
  "generated": false,
  "edited": true,
  "query": false,
  "showAttributes": false,
  "technicalAttribute": false,
  "subfields": []
}
```
- `idAttribute: true` marks the field that identifies the aggregate
  instance (usually a generated UUID on the creating event).
- `generated: true` marks server-generated values (e.g. a new UUID) rather
  than caller-supplied input.
- `cardinality: "List"` + non-empty `subfields` lets you model a
  list-of-records field (e.g. a read model's list of rows) — `subfields`
  is itself an array of `Field` objects.
- `pii` is not part of any real sample seen, but appears tolerated as an
  extra key (the schema's `additionalProperties` are generally permissive)
  — include it if you want to flag sensitive fields for your own
  downstream tooling, just don't rely on the platform doing anything with it.

## `Specification` object (given/when/then scenarios) — use with caution

```json
{
  "id": "any-string-id",
  "title": "Scenario title",
  "chapter": "same chapter as the slice",
  "sliceName": "same as parent slice's title",
  "vertical": false,
  "expectEmptyList": false,
  "examples": [],
  "given": [ { "id": "<event-id>", "title": "", "tags": [], "index": 0, "specRow": 0, "type": "", "fields": [] } ],
  "when": [ { "id": "<command-id>", "title": "", "tags": [], "index": 0, "specRow": 0, "type": "", "fields": [] } ],
  "then": [ { "id": "<event-or-readmodel-id>", "title": "", "tags": [], "index": 0, "specRow": 0, "type": "", "fields": [] } ]
}
```

Key finding from a real sample: the `given`/`when`/`then` entries reference
**actual COMMAND/EVENT node ids from elsewhere in the model** (often a
*different, earlier* slice's event feeding into this slice's `given`) —
this is wired at the type/definition level, not filled with concrete
example instance data. It is **not** a place for narrative text like
"CreateOrder with quantity=5" — put concrete example values in
`examples`/`Field.example` instead, and use `given`/`when`/`then` purely as
cross-references to other elements' `id`s.

This also means **rejection-only outcomes don't fit cleanly**: if your
domain has a command whose failure path produces no event (a pure
rejection, common when tracking "rejected, not an event" semantics), there
is no event id to put in a `then` for that case. The platform's own
separately-documented scenario endpoint —

```
POST /api/org/:orgId/boards/:boardId/contexts/:contextName/slices/:sliceName/scenarios
```

— enforces (per its actual documented validation rules): `given` must be
EVENTs from the same timeline; `when` at most one COMMAND; `then` must be
all-EVENTs **or** exactly one READMODEL, never mixed, and never empty with
a rejection in mind. If your model has meaningful GWT scenarios to
express, especially ones with rejection outcomes, **use that dedicated
endpoint after the bulk import**, not the `specifications` array inside
`import-config` — its handling of malformed/mismatched scenarios is
unconfirmed and risks either silently dropping them or producing corrupted
nodes. Safest default: leave `specifications: []` in your `import-config`
payload and wire scenarios up afterward through the dedicated endpoint,
one slice at a time, where you get real validation errors to react to.

## IDs: strings are fine, don't feel obligated to use UUIDs

Confirmed by testing: the importer accepts arbitrary non-UUID strings for
every `id` field (`cmd-create-order`, `evt-order-created`, etc.) — it
doesn't require real UUIDs. Keep whatever ids trace back to your source
model/spec docs; the server generates its own internal node ids
independently on import (visible in the response), so your ids are just
your own bookkeeping, not something the platform depends on structurally.

## Minimal end-to-end example

A single `STATE_CHANGE` slice, generic domain (swap in your own):

```json
{
  "slices": [
    {
      "id": "slice-create-order",
      "title": "Create Order",
      "status": "Created",
      "sliceType": "STATE_CHANGE",
      "chapter": "Orders",
      "context": "Orders",
      "commands": [
        {
          "id": "cmd-create-order",
          "title": "CreateOrder",
          "type": "COMMAND",
          "fields": [
            { "name": "customerId", "type": "UUID", "cardinality": "Single", "optional": false, "idAttribute": false, "generated": false, "edited": true, "query": false, "showAttributes": false, "technicalAttribute": false, "subfields": [] }
          ],
          "dependencies": [], "modelContext": "Orders", "lane": "Interaction",
          "aggregate": "Order", "aggregateDependencies": [], "triggers": [], "tags": [],
          "slice": "Create Order", "description": "", "context": "INTERNAL", "elementContext": "INTERNAL",
          "listElement": false, "todoList": false, "createsAggregate": true, "elementCopy": false,
          "sketched": false, "comments": []
        }
      ],
      "events": [
        {
          "id": "evt-order-created",
          "title": "OrderCreated",
          "type": "EVENT",
          "fields": [
            { "name": "orderId", "type": "UUID", "cardinality": "Single", "optional": false, "idAttribute": true, "generated": true, "edited": true, "query": false, "showAttributes": false, "technicalAttribute": false, "subfields": [] },
            { "name": "customerId", "type": "UUID", "cardinality": "Single", "optional": false, "idAttribute": false, "generated": false, "edited": true, "query": false, "showAttributes": false, "technicalAttribute": false, "subfields": [] }
          ],
          "dependencies": [], "modelContext": "Orders", "lane": "Swimlane",
          "aggregate": "Order", "aggregateDependencies": [], "triggers": [], "tags": [],
          "slice": "Create Order", "description": "", "context": "INTERNAL", "elementContext": "INTERNAL",
          "listElement": false, "todoList": false, "createsAggregate": false, "elementCopy": false,
          "sketched": false, "comments": []
        }
      ],
      "readmodels": [], "screens": [], "processors": [], "tables": [], "specifications": [],
      "actors": [], "aggregates": [], "screenImages": [], "comments": []
    }
  ]
}
```

Send it:
```bash
curl -X POST \
  -H "Content-Type: application/json" \
  -H "x-token: $TOKEN" \
  -H "x-board-id: $BOARD_ID" \
  -H "x-user-id: import-script" \
  --data @model.json \
  "$BASE_URL/api/org/$ORG_ID/boards/$BOARD_ID/import-config"
```

Then verify:
```bash
curl -H "x-token: $TOKEN" -H "x-board-id: $BOARD_ID" -H "x-user-id: import-script" \
  "$BASE_URL/api/org/$ORG_ID/boards/$BOARD_ID/nodes"
```
and count node types against what you expect.

## Suggested conversion workflow for a new project

1. Get your domain model into any structured form (JSON/YAML/whatever you
   already have — commands, events, read models, grouped into
   slices/use-cases).
2. Write a small script (Python is easiest) that maps your source shape
   onto the `Slice`/`Element`/`Field` shapes above. Reuse field-level
   metadata you already have (types, id markers, optionality) — don't
   invent new ones.
3. Decide your `chapter` grouping up front — one shared chapter puts
   everything on one timeline; per-feature chapters split it up.
4. Leave `specifications: []` for the first pass. Get the bulk structure
   imported and verified first.
5. POST to `import-config`, then `GET .../nodes` and diff counts
   (commands/events/readmodels/slice borders) against your source model's
   counts before declaring it done.
6. If you have GWT scenarios to express, add them afterward via the
   dedicated `/scenarios` endpoint, slice by slice, reacting to its
   documented validation errors rather than guessing at the
   `specifications` array's exact rules.
