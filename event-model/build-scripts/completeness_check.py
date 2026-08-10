import json
import re

def load(t):
    return json.load(open(f"completeness_{t}.json"))

events = load("EVENT")
commands = load("COMMAND")
readmodels = load("READMODEL")
screens = load("SCREEN")
automations = load("AUTOMATION")

def field_names(node, top_only=False):
    names = []
    for f in node.get("meta", {}).get("fields", []) or []:
        names.append(f["name"])
        if not top_only:
            for sf in f.get("subfields", []) or []:
                names.append(sf["name"])
    return names

def idfields(node):
    return [f["name"] for f in node.get("meta", {}).get("fields", []) or [] if f.get("idAttribute")]

def generated_fields(node):
    return [f["name"] for f in node.get("meta", {}).get("fields", []) or [] if f.get("generated")]

def title(node):
    return node["meta"]["title"]

def desc(node):
    return node.get("meta", {}).get("description", "") or ""

# ---------------------------------------------------------------------------
# Build global index: which elements mention which field name
# ---------------------------------------------------------------------------
field_to_elements = {}  # fieldName -> list of (elementType, title, id)
for t, nodes in [("EVENT", events), ("COMMAND", commands), ("READMODEL", readmodels)]:
    for n in nodes:
        for fname in set(field_names(n)):
            field_to_elements.setdefault(fname, []).append((t, title(n), n["id"]))

# all id-attribute/generated identifier field names anywhere (the "origins" a reference could trace to)
all_id_or_generated_names = set()
for n in events + readmodels:
    for f in n.get("meta", {}).get("fields", []) or []:
        if f.get("idAttribute") or f.get("generated"):
            all_id_or_generated_names.add(f["name"])

# ---------------------------------------------------------------------------
# GAP CLASS A: command *reference-looking* required fields with no traceable origin
# ---------------------------------------------------------------------------
REF_PATTERN = re.compile(r"(Id|Reference)$")
gapsA = []
for n in commands:
    for f in n.get("meta", {}).get("fields", []) or []:
        fname = f["name"]
        if f.get("optional"):
            continue
        if REF_PATTERN.search(fname):
            # does this name match a generated/idAttribute field anywhere (an origin)?
            if fname not in all_id_or_generated_names:
                gapsA.append((title(n), fname))

# ---------------------------------------------------------------------------
# GAP CLASS B: event-produced, non-technical fields never referenced elsewhere by name
# ---------------------------------------------------------------------------
TECHNICAL_SUFFIXES = ("At", "Version", "Status")  # timestamps/version/status are commonly self-contained, lower priority
gapsB = []
for n in events:
    for f in n.get("meta", {}).get("fields", []) or []:
        fname = f["name"]
        if f.get("idAttribute"):
            continue  # id fields are meant to be referenced by future commands, not necessarily by readmodels
        occurrences = field_to_elements.get(fname, [])
        other_occurrences = [o for o in occurrences if o[2] != n["id"]]
        if not other_occurrences:
            gapsB.append((title(n), fname, f.get("type")))

print("=" * 70)
print(f"GAP CLASS A: command required reference-like fields with no traceable origin ({len(gapsA)})")
print("=" * 70)
for cmd_title, fname in gapsA:
    print(f"  {cmd_title}.{fname}")

print()
print("=" * 70)
print(f"GAP CLASS B: event fields never referenced by name anywhere else ({len(gapsB)})")
print("=" * 70)
for evt_title, fname, ftype in gapsB:
    print(f"  {evt_title}.{fname} ({ftype})")

print()
print(f"Total events: {len(events)}, commands: {len(commands)}, readmodels: {len(readmodels)}, screens: {len(screens)}, automations: {len(automations)}")
