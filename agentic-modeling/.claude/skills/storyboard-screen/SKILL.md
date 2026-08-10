---
name: storyboard-screen
description: Design and render a single AI-generated wireframe screen onto an existing SCREEN node using the sketch API
---

# Storyboard Screen Designer

> **Before doing anything else**, invoke the `connect` skill to resolve `TOKEN`, `BOARD_ID`, and `BASE_URL`. Do not proceed until the connect skill has completed.

Design a single wireframe screen and render it onto an existing SCREEN node. Use this to redesign a screen, add detail to a placeholder, or update a screen after a flow changes.

## Step 1 — Parse arguments

From `$ARGUMENTS`, extract:

| Field | How to find it | Default |
|-------|---------------|---------|
| `description` | what the screen should contain, e.g. "login with email and password" | required |
| `boardId` | a board ID string | from `connect` skill (`BOARD_ID`) |
| `nodeId` | the SCREEN node UUID to update | **ask the user if missing** |
| `baseUrl` | explicit URL override | from `connect` skill (`BASE_URL`) |

If `nodeId` is missing, ask for it before doing anything. `BOARD_ID` and `BASE_URL` come from the `connect` skill.

## Step 2 — Design the screen

Design the screen yourself using the grid description language. Think carefully about the layout — what elements does this screen need? Where should they go on the 50×40 grid?

Also compose a `visualDescription` — a prose description (2–4 sentences) of the screen's visual layout and content, written so that someone who cannot see the image can understand what is shown: what UI sections appear, what text/labels are visible, where buttons and inputs are placed, and the overall purpose of the screen.

## Grid description language

Canvas: **50 × 40 grid units** (1000 × 800 px, 1 unit = 20 px).

Always start with a full white background:
```json
{"type":"rectangle","gridX":0,"gridY":0,"gridWidth":50,"gridHeight":40,"fill":"white"}
```

### Element types

| type | required fields | optional fields |
|------|----------------|-----------------|
| `rectangle` | gridX, gridY, gridWidth, gridHeight | fill, stroke |
| `text` | gridX, gridY, text | fontSize (default 12), fill, gridWidth |
| `headline` | gridX, gridY, text | fontSize (default 20), fill, gridWidth |
| `button` | gridX, gridY, gridWidth, gridHeight, text | fill, stroke |
| `input` | gridX, gridY, gridWidth, gridHeight, text (placeholder) | fill, stroke |
| `image` | gridX, gridY, gridWidth, gridHeight | fill (placeholder color) |
| `line` | gridX, gridY, gridX2, gridY2 | stroke |
| `circle` | gridX, gridY, gridRadius | fill, stroke |

### Colors
Named: `black` `grey` `light-violet` `violet` `blue` `light-blue` `yellow` `orange` `green` `light-green` `light-red` `red` `white` `transparent`
Or any hex code.

Keep all coordinates within bounds: gridX 0–50, gridY 0–40.

### Example
```json
{
  "elements": [
    {"type":"rectangle","gridX":0,"gridY":0,"gridWidth":50,"gridHeight":40,"fill":"white"},
    {"type":"rectangle","gridX":0,"gridY":0,"gridWidth":50,"gridHeight":3,"fill":"violet"},
    {"type":"headline","gridX":2,"gridY":1,"text":"Dashboard","fontSize":18,"fill":"white"},
    {"type":"text","gridX":2,"gridY":6,"text":"Welcome back","fontSize":14,"fill":"grey"},
    {"type":"rectangle","gridX":2,"gridY":9,"gridWidth":21,"gridHeight":8,"fill":"light-blue","stroke":"grey"},
    {"type":"headline","gridX":4,"gridY":11,"text":"142","fontSize":24,"fill":"blue"},
    {"type":"text","gridX":4,"gridY":14,"text":"Orders this month","fontSize":11,"fill":"grey"},
    {"type":"button","gridX":35,"gridY":36,"gridWidth":12,"gridHeight":2,"text":"Logout","fill":"light-red"}
  ]
}
```

## Step 3 — Render the sketch

Include `semanticDescription` (the original natural-language `description` argument) alongside the `elements` array so the server persists it in the node's metadata for future adjustments:

```bash
curl -s -X POST "$BASE_URL/api/org/$ORG_ID/boards/$BOARD_ID/images/$NODE_ID/sketch" \
  -H "Content-Type: application/json" \
  -d '{"semanticDescription": "<description>", "elements": [...]}'
```

This replaces the existing image on the SCREEN node and fires a `node:changed` event so the board updates live.

## Step 4 — Write the visual description into node meta

After the sketch call succeeds, write the visual description onto the node so it is accessible to anyone who cannot see the rendered image. **`PATCH .../nodes/:nodeId` does not exist on the deployed API** (confirmed by testing — only `GET` is registered on that route, `PATCH` 404s with `Cannot PATCH ...`). Use a `node:changed` event instead, same mechanism as any other node update:

```bash
curl -s -X POST "$BASE_URL/api/org/$ORG_ID/boards/$BOARD_ID/nodes/events" \
  -H "Content-Type: application/json" \
  -H "x-user-id: storyboard-screen-skill" \
  -d "[{
    \"id\": \"<event-uuid>\",
    \"eventType\": \"node:changed\",
    \"nodeId\": \"$NODE_ID\",
    \"boardId\": \"$BOARD_ID\",
    \"timestamp\": $(date +%s000),
    \"changedAttributes\": [\"meta.description\"],
    \"meta\": {\"type\": \"SCREEN\", \"title\": \"<screen title>\", \"description\": \"<visualDescription>\"},
    \"node\": {\"id\": \"$NODE_ID\", \"data\": {}}
  }]"
```

Note `meta` is shallow-merged server-side but `title` is required alongside `type` in practice — omitting it risks the title being cleared, so always resend the node's current title, not just the new description.

## Step 5 — Report back

Tell the user:
- The node ID that was updated
- Whether the render succeeded (look for HTTP 200)
- Any errors
