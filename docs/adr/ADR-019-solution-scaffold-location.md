# ADR-019: Solution scaffold location

**Status**: Accepted (decided during `001-authority-administration` planning, `research.md` Decision 1)

## Context

No .NET solution existed anywhere in this repo before `001-authority-administration`
was scaffolded (verified: no `.sln`/`.slnx`/`.csproj` files present). Solution
Arch §3 fixes the *module* naming convention
(`BrokerConnect.Modules.<Context>.{Api,Domain,Contracts}`) but not the
repo-relative root the solution itself lives under. `build-kit-dotnet-es`'s own
setup docs treat `<path-to-your-.NET-solution>` as a placeholder to fill in
per-project, not a fixed answer.

## Decision

`src/BrokerConnect.slnx` at the repo root, modules under
`src/Modules/<Context>/`, shared libraries under `src/BuildingBlocks/`.

## Rationale

`src/` is the path of least surprise: it doesn't collide with any existing
top-level content (`event-model/`, `build-kit-dotnet-es/`, `HLD/`,
`Requirements/`, `Solution Arch/`, `Project Plan/`, `specs/`), and matches the
two-part `<repo>/src/<Solution>.sln` shape common to .NET repos generally — a
lighter convention than the reference `code/K9Crush-scaffold/K9Crush` nesting
`build-kit-dotnet-es/README.md` mentions from a prior project, which doesn't
apply here since nothing forces that project's specific nesting.

## Alternatives Considered

- **Repo root directly** (`BrokerConnect.slnx` next to `HLD/`, `Requirements/`,
  etc.) — rejected: mixes prototype documentation and product source at the
  same level, makes `.gitignore`ing `bin/`/`obj/` messier alongside doc
  folders.
- **Mirroring `code/<Name>-scaffold/<Name>`** from the K9Crush reference —
  rejected: that nesting existed for a reason specific to that project's
  history; nothing here requires two levels of nesting.

## Consequences

Every other feature's `/speckit-plan` (`002` onward) should read this decision
rather than re-deciding it — flag if a later plan feels the need to deviate.
`.slnx` (the XML solution format) was chosen over legacy `.sln` at the same
time, decided while scaffolding `001-authority-administration` before
anything depended on the older format (see `plan.md`'s Technical Context).
