---
name: place-element
description: Place a COMMAND, READMODEL, or EVENT onto an existing eventmodelers board timeline at a specific position
---

# Place Element

> **Before doing anything else**, invoke the `connect` skill to resolve `TOKEN`, `BOARD_ID`, and `BASE_URL`. Then invoke the `learn-eventmodelers-api` skill to load the full API reference. Do not proceed until both skills have been loaded.

Place a single element — COMMAND, READMODEL, or EVENT — onto an existing timeline on an eventmodelers board. Uses an existing column when a position is given; only creates a new column when appending.

---

## Step 1 — Parse arguments

From `$ARGUMENTS`, extract:

| Field | How to find it | Default |
|-------|---------------|---------|
| `elementType` | `event`, `command`, or `readmodel` (case-insensitive) | **required** |
| `title` | the element name, e.g. "Order Placed" | **required** |
| `boardId` | a board UUID | from `connect` skill (`BOARD_ID`) |
| `timelineId` | the chapter/timeline UUID | auto-detect (see Step 2) |
| `position` | column index (0-based number), `"after <title>"`, or omitted | append at end |
| `baseUrl` | explicit URL override | from `connect` skill (`BASE_URL`) |

Normalise `elementType` to uppercase: `event` → `EVENT`, `command` → `COMMAND`, `readmodel` → `READMODEL`.

Use `BOARD_ID` and `BASE_URL` from the `connect` skill. If a `boardId` argument is explicitly passed, it overrides `BOARD_ID`.

---

## Step 2 — Resolve the timeline

If `timelineId` is not provided, discover chapters on the board:

```bash
curl -s "$BASE_URL/api/org/$ORG_ID/boards/$BOARD_ID/nodes?type=CHAPTER"
```

- **Exactly one chapter** → use it automatically, tell the user which one was selected.
- **Multiple chapters** → list them by name/ID and ask the user which to target.
- **No chapters** → stop and tell the user to create a chapter first (e.g. via the `/timeline` skill).

---

## Step 3 — Fetch existing columns and resolve position

Always fetch the chapter node first to get the current timeline state:

```bash
curl -s "$BASE_URL/api/org/$ORG_ID/boards/$BOARD_ID/nodes/$TIMELINE_ID"
```

From `meta.timelineData`, read `columns` (ordered array of column objects with `id` and `index`) and `cells`.

Then resolve `position`:

| Input | Behaviour |
|-------|-----------|
| A number (e.g. `2`) | Find the existing column whose `index === 2`. Save its `id` as `columnId`. **Do NOT create a new column.** |
| `"after <title>"` | Cross-reference node titles to find the named column, then target the column at position + 1. If that next column already exists use it; if not, create one at that index. |
| Omitted | No existing column is targeted → create a new column at the end (Step 5). |

If `position` is a number and no column exists at that index, stop and tell the user: "No column at index `<n>` — did you mean to append instead?"

---

## Step 4 — Determine the target lane

| `elementType` | Target lane `type` |
|---------------|--------------------|
| `EVENT`       | `swimlane`         |
| `COMMAND`     | `interaction`      |
| `READMODEL`   | `interaction`      |

---

## Step 5 — Create a column only when appending

**Skip this step entirely** when `columnId` was already resolved in Step 3 (i.e. the user targeted an existing column).

Only run this when position was omitted (append mode):

```bash
curl -s -X POST "$BASE_URL/api/org/$ORG_ID/boards/$BOARD_ID/timelines/$TIMELINE_ID/columns" \
  -H "Content-Type: application/json" \
  -d '{}'
```

Response: `{ "columnId": "<uuid>", "index": <n>, "totalColumns": <n> }`

Save `columnId` from the response.

---

## Step 6 — Find the target cell and check availability

Using the `timelineData` already fetched in Step 3 (re-fetch if a column was just created):

- Find the row in `rows` whose `type` matches the target lane (`swimlane` or `interaction`).
- Find the cell in `cells` where `colId === columnId` AND `rowId === targetRow.id`.

Save that cell's `id` as `CELL_ID`.

**Check if the cell is already occupied**: query nodes in that cell:

```bash
curl -s "$BASE_URL/api/org/$ORG_ID/boards/$BOARD_ID/nodes?cellId=$CELL_ID"
```

If the response contains any nodes, stop and tell the user: "Cell `<CELL_ID>` at column index `<n>` already contains `<existing node titles>`. Choose a different position or confirm overwrite."

If no matching row or cell is found, stop and report the error — the timeline may be missing the required lane type.

---

## Step 7 — Create the node

```bash
curl -s -X POST "$BASE_URL/api/org/$ORG_ID/boards/$BOARD_ID/nodes/events" \
  -H "Content-Type: application/json" \
  -H "x-user-id: place-element-skill" \
  -d '[{
    "eventType": "node:created",
    "nodeId": "<node-uuid>",
    "boardId": "<BOARD_ID>",
    "timestamp": <Date.now()>,
    "chapterId": "<TIMELINE_ID>",
    "cellId": "<CELL_ID>",
    "meta": {
      "type": "<ELEMENT_TYPE>",
      "title": "<title>"
    },
    "node": { "data": { "title": "<title>" } }
  }]'
```

Response: `{ "hashes": { "<event-uuid>": "<hash>" } }`

---

## Step 8 — Report back

Tell the user:

- **What was placed**: element type and title
- **Where**: column index on the timeline
- **Node ID**: the UUID of the placed element
- **Cell ID**: the cell it was placed into
- **Any errors**: raw API message if something failed

Example success output:
```
Placed: EVENT "Order Placed" at column 3
Node ID: a1b2c3d4-…
Cell ID: e5f6g7h8-…
Timeline: <timelineId>
```

---

## Example — place an EVENT via curl

Full working example placing an EVENT called "Order Placed" at the end of a timeline:

```bash
# 1. Add a column (append at end)
curl -s -X POST "http://localhost:3000/api/org/<ORG_ID>/boards/<BOARD_ID>/timelines/<TIMELINE_ID>/columns" \
  -H "Content-Type: application/json" \
  -d '{}'

# 2. Fetch chapter to find the swimlane cell for the new column
curl -s -H "x-user-id: place-element-skill" \
  "http://localhost:3000/api/org/<ORG_ID>/boards/<BOARD_ID>/nodes/<TIMELINE_ID>"

# 3. Create the EVENT node

Do not skip the User-ID. 


curl -s -X POST "http://localhost:3000/api/org/<ORG_ID>/boards/<BOARD_ID>/nodes/events" \
  -H "Content-Type: application/json" \
  -H "x-user-id: place-element-skill" \
  -d '[{
    "id": "<event-uuid>",
    "eventType": "node:created",
    "nodeId": "<node-uuid>",
    "boardId": "<BOARD_ID>",
    "timestamp": 1714900000000,
    "chapterId": "<TIMELINE_ID>",
    "cellId": "<CELL_ID>",
    "meta": { "type": "EVENT", "title": "Order Placed" },
    "node": { "id": "<node-uuid>", "data": { "title": "Order Placed" } }
  }]'
```

Replace `<TIMELINE_ID>`, `<BOARD_ID>`, `<CELL_ID>`, `<event-uuid>`, and `<node-uuid>` with real UUIDs. Use `Date.now()` or a current unix-ms timestamp for `timestamp`.
