# HTTP Contracts: Authority Administration

Wolverine.Http endpoints, `BrokerConnect.Modules.AuthorityAdministration.Api`.
Per constitution Principle VII: endpoints return DTOs, never the `AuthorityLimit`
aggregate itself; errors are `ProblemDetails`/`ValidationProblemDetails`; list
endpoints are paginated from v1.

## `POST /api/v1/authority/cells/{cellId}/limits`
**Command**: `GrantCellAuthorityLimit`

Request:
```json
{
  "grantingParty": "string",
  "sourceAgreementReference": "string",
  "authorityScope": { "classesOfBusiness": ["string"], "territory": "string", "maxLineSize": 0, "maxAggregate": 0 },
  "currency": "string",
  "effectiveDate": "2026-08-09"
}
```
`201 Created` → `{ authorityLimitId, cellId, version, grantedAt }`
`409 Conflict` — `application/problem+json` (`ProblemDetails`, constitution Principle VII): `{ title, detail, status: 409, authorityLimitId, rejectionReason: "DuplicateActiveGrant" }`, appended as `CellAuthorityLimitGrantRejected` — an Active grant already exists for this cell/scope/class.

## `POST /api/v1/authority/cells/{cellId}/underwriters/{underwriterId}/limits`
**Command**: `GrantUnderwriterAuthorityLimit`

Request:
```json
{ "requestedScope": { "classesOfBusiness": ["string"], "territory": "string", "maxLineSize": 0 }, "grantingAuthority": "string" }
```
`201 Created` → `{ authorityLimitId, underwriterId, cellId, version, validationResult, grantedAt }`
`409 Conflict` — `application/problem+json`: `{ title, detail, status: 409, authorityLimitId, rejectionReason: "ExceedsCellLimit" | "DuplicateActiveGrant" }`, appended as `UnderwriterAuthorityLimitRejected`

## `POST /api/v1/authority/limits/{authorityLimitId}/increase-requests`
**Command**: `RequestCellAuthorityIncrease` — records the request only; no approval workflow modeled yet (see `data-model.md`, `CellAuthorityIncreaseRequest`).

Request: `{ "requestedLimit": { "maxLineSize": 0, "maxAggregate": 0 }, "justification": "string" }`
`201 Created` → `{ requestId, cellId, requestedAt }`

## `PUT /api/v1/authority/limits/{authorityLimitId}/revision`
**Command**: `ReviseAuthorityLimit`

Request: `{ "newMaxGrossPremium": 0, "newMaxLimit": 0, "reason": "string" }`
`200 OK` → `{ authorityLimitId, version, revisedAt }`
`409 Conflict` — `application/problem+json`: `{ title, detail, status: 409, authorityLimitId, rejectionReason: "RevokedRecord" | "ExceedsCellLimit" }` (Clarified 2026-08-09), appended as `AuthorityLimitRevisionRejected`

## `POST /api/v1/authority/limits/{authorityLimitId}/revocation`
**Command**: `RevokeAuthorityLimit`

Request: `{ "reason": "string", "immediacy": "Immediate" | "NextDecisionPoint" }`
`200 OK` → `{ authorityLimitId, revokedAt }`

## `GET /api/v1/authority/matrix/{authorityLimitId}`
**Read model**: `AuthorityMatrix` (`Inline` snapshot — same-request read-after-write guaranteed, Clarified 2026-08-09 / SC-001)

`200 OK` → full `AuthorityMatrix` shape (see `data-model.md`)

## `GET /api/v1/authority/cells/{cellId}/register`
**Read model**: `CellAuthorityRegister`

`200 OK` → current + `history[]` (paginated if `history` grows large — v1 default page size TBD, not specified by source, flag at implementation time)

## `GET /api/v1/authority/underwriters/{underwriterId}/register`
**Read model**: `UnderwriterAuthorityRegister`

`200 OK` → current + `history[]`, same pagination note as above.
