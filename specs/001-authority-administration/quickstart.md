# Quickstart: Validating Authority Administration

Run-guide only — implementation details belong in `tasks.md` (Phase 2) and the
actual code, not here.

## Prerequisites

- .NET 10 SDK
- Postgres (local Docker container is fine for dev; Testcontainers handles this
  automatically for Layer 3 tests — no manual setup needed to run the test suite)
- The `src/BrokerConnect.slnx` scaffold from this plan's Project Structure exists
  and builds (`dotnet build src/BrokerConnect.slnx`)

## Running the test suite (constitution Principle IV, 3 layers)

```bash
# Layer 1 — domain, no infra
dotnet test tests/Modules/AuthorityAdministration/AuthorityAdministration.Domain.Tests

# Layer 2 — handlers, IDocumentSession mocked via NSubstitute
dotnet test tests/Modules/AuthorityAdministration/AuthorityAdministration.Api.Tests

# Layer 3 — Testcontainers-backed real Postgres
dotnet test tests/Modules/AuthorityAdministration/AuthorityAdministration.IntegrationTests
```

## Scenarios that prove the feature works end-to-end

Each maps directly to an Acceptance Scenario in `spec.md` — cross-reference there
for the full Given/When/Then. Run via `dotnet test --filter <SliceName>` once
implemented (constitution Quality Gates: "running only the affected slice's tests
is enough").

1. **Grant → read back via AuthorityMatrix same-request** (SC-001): `POST
   .../cells/{cellId}/limits`, then immediately `GET
   .../matrix/{authorityLimitId}` in the same test — must reflect the grant with
   no retry/poll. This is the executable proof of the `Inline` snapshot decision.
2. **Underwriter grant within cell limit succeeds**: grant a Cell limit, grant an
   Underwriter limit within it → `201`, `validationResult` recorded.
3. **Underwriter grant exceeding cell limit rejected**: same setup, request above
   the Cell's limit → `409`, `UnderwriterAuthorityLimitRejected` with
   `rejectionReason: "ExceedsCellLimit"`.
4. **Duplicate Active grant rejected** (Clarified 2026-08-09, FR-011): grant a
   Cell limit for (cellId, classOfBusiness), grant again for the same scope while
   the first is still Active → `409`, `CellAuthorityLimitGrantRejected`.
5. **Revise on a Revoked record rejected** (Clarified 2026-08-09, FR-010): grant,
   revoke, then attempt revise → `409`, `AuthorityLimitRevisionRejected` with
   `rejectionReason: "RevokedRecord"`.
6. **Revise exceeding cell limit rejected** (Clarified 2026-08-09, FR-012): grant
   an Underwriter limit, revise the Cell's limit down below it, attempt to revise
   the Underwriter's limit above the new Cell ceiling → `409`,
   `rejectionReason: "ExceedsCellLimit"`.
7. **Escalation request recorded**: `POST .../increase-requests` after a
   rejection → `201`, auditable independent of any later decision (no
   approval/denial modeled yet — see `data-model.md`).
8. **Revision/revocation publishes the integration event**: assert
   `AuthorityLimitChangedV1` is published (via the Wolverine/Marten outbox) on
   both `AuthorityLimitRevised` and `AuthorityLimitRevoked` — this is what
   `004-underwriting-decisioning`'s reassessment automation will consume; this
   feature only needs to prove the event is published, not that it's consumed.

## Out of scope for this quickstart

- Actually reassessing in-flight submissions (`InFlightSubmissionReassessed`) —
  belongs to `004-underwriting-decisioning`'s own quickstart, per the
  cross-module boundary in `research.md` Decision 4.
- Any UI/screen validation — no screens are modeled for this context on the
  board (see `spec.md`'s Source shape caveat on `screens`).
