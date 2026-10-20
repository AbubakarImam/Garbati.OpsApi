# Garbati.OpsApi

GARBATI's ops-console backend-for-frontend, built with ASP.NET Core 8, EF Core and PostgreSQL. It is the **only** thing the ops console's browser ever talks to: it owns the staff-facing login session (cookie-based), holds every real service credential for `StaffIdentityApi`, `IdentityApi`, `OrchestratorApi` and `LedgerApi` (none of which the browser ever sees), and decides whether a staff member's role may do a specific action right now.

The design, including every trade-off, is in [docs/DESIGN.md](docs/DESIGN.md) - start there for the *why*. This README covers the *how*. See [`garbati-platform/ARCHITECTURE.md`](https://github.com/AbubakarImam/garbati-platform/blob/main/ARCHITECTURE.md) Section 3.1 for how this service fits the rest of the platform.

## What this build covers

This is OpsApi's **first** build task (`garbati-platform` T-033). It covers everything in `docs/DESIGN.md` Section 6 except the two stateful maker-checker workflows (reversal requests, customer-detail-edit staging/approval) - those are T-034's job, built directly on top of what this task leaves behind:

- `OpsSession` - the staff login session (2.1/2.1a/2.2), with instant cascade revocation on a role/status change (2.3).
- Four downstream HTTP clients, each scoped exactly per `docs/DESIGN.md` Section 3's table.
- Same-origin static hosting for the eventual ops-console frontend (2.1b), with a placeholder `wwwroot-console/index.html` - the real frontend (`garbati-ops-console`) is design-only as of this build.
- Section 6 groups 1 (customer & account lookup), 4 (transfer/transaction testing console), 5 (staff & role management) and 6 (joint-account approval test console).

**Not built here:** `OpsReversalRequest`, `OpsCustomerEditRequest`, Section 6 groups 2 and 3, and any call to `OrchestratorApi`'s reversal endpoint. See the PR description for the full list of judgment calls.

## Core pieces

- **OpsSession** - `SessionId`, `SessionTokenHash` (SHA-256 of the raw token - the raw token is never persisted), `StaffId`/`FullName`/`Role` (cached at login), `CreatedAt`/`LastActivityAt`/`ExpiresAt`. Idle (30 min) and absolute (8 hr) timeouts are both configurable (`Session:IdleTimeoutMinutes`/`Session:AbsoluteTimeoutHours`), not hardcoded.
- **Authorization/SessionAuthenticationHandler** - this service's own inbound authentication: resolves the session from the `ops_session` cookie, enforces idle/absolute expiry, and exposes `{staffId, fullName, role}` as claims to every controller - the one shared mechanism every authenticated endpoint runs through. This is deliberately **not** the `ApiKeyAuthenticationHandler` pattern every sibling service uses for its own inbound callers (OpsApi's only caller is the browser).
- **Authorization/OpsRoles** - `docs/DESIGN.md` Section 4's matrix, enforced with ASP.NET Core's own built-in `[Authorize(Roles = "...")]`.
- **Clients/** - this service as a client of all four downstream services: `IStaffIdentityApiClient`/`IIdentityApiClient`/`IOrchestratorApiClient`/`ILedgerApiClient`, backed by `IHttpClientFactory`-managed `HttpClient`s carrying this service's own `X-Api-Key` to each, and propagating `X-Correlation-ID` downstream. Mirrors `Garbati.OrchestratorApi`'s proven pattern, extended from two clients to four.
- **Services/** - `SessionService` (login/logout/cascade revocation), `StaffManagementService` (the one place cascade revocation actually runs, on role/status changes), `CustomerLookupService`, `TestingConsoleService`, `JointApprovalConsoleService`.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (pinned by `global.json`)
- [Docker](https://www.docker.com/) (for the local database and for the tests)

## Getting started

**1. Start PostgreSQL** (port `5436` - LedgerApi uses `5433`, IdentityApi `5434`, OrchestratorApi `5435`):

```bash
docker run -d --name opsapi-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=opsapi -p 5436:5432 postgres:16-alpine
```

**2. Apply the migrations:**

```bash
dotnet tool restore
dotnet ef database update --project src/Garbati.OpsApi
```

**3. Point it at real downstream instances.** Edit `src/Garbati.OpsApi/appsettings.Development.json`'s `DownstreamServices` section with the real base URLs and API keys each of `StaffIdentityApi`/`IdentityApi`/`OrchestratorApi`/`LedgerApi` has issued this service - see `docs/DESIGN.md` Section 3 for the exact scopes each one needs.

**4. Run the API:**

```bash
dotnet run --project src/Garbati.OpsApi --launch-profile http
```

## Running the tests

```bash
dotnet test
```

Every automated test replaces all four downstream clients with hand-rolled fakes (`tests/Garbati.OpsApi.Tests/Fixtures/Fake*ApiClient.cs`) - no real `StaffIdentityApi`/`IdentityApi`/`OrchestratorApi`/`LedgerApi` instance is needed to run this suite, and none is contacted. Service- and controller-level tests run against a real PostgreSQL container started by [Testcontainers](https://dotnet.testcontainers.org/), so Docker must be running.

## Authentication

**Inbound** (the browser, 2.1/2.1a): an `HttpOnly`/`Secure`/`SameSite=Strict` cookie (`ops_session`) containing an opaque random token, issued by `POST /v1/ops/login` and validated by `SessionAuthenticationHandler` on every other request. There is no API-key-authenticated caller of this service in this version.

**Outbound** (this service calling the other four, Section 3): `X-Api-Key`, same convention as every other inter-service client on this platform - a separate concern from the cookie above.

## Endpoints

All under `/v1/ops`, session-cookie-authenticated except login.

| Method | Route | Role | Purpose |
|---|---|---|---|
| `POST` | `/v1/ops/login` | - | Authenticates against StaffIdentityApi, issues the session cookie |
| `GET` | `/v1/ops/me` | any session | `{staffId, fullName, role}` from the cached session |
| `POST` | `/v1/ops/logout` | any session | Deletes the session, clears the cookie |
| `GET` | `/v1/ops/customers/{customerId}` | any session | Combined IdentityApi + LedgerApi lookup |
| `POST` | `/v1/ops/console/account-openings` | Teller, Supervisor | Testing console: account opening via OrchestratorApi |
| `POST` | `/v1/ops/console/transfers` | Teller, Supervisor | Testing console: transfer via OrchestratorApi |
| `POST` | `/v1/ops/console/funding` | Teller, Supervisor | Testing console: deposit directly via LedgerApi (3.4) |
| `POST`/`GET`/`PATCH` | `/v1/ops/staff/**` | Administrator | Thin pass-through to StaffIdentityApi's 8 endpoints; role/status PATCH trigger cascade revocation |
| `GET` | `/v1/ops/joint-approvals` | Supervisor, Auditor | Pending joint-account approval queue |
| `GET` | `/v1/ops/joint-approvals/{id}` | Supervisor, Auditor | One request's status |
| `POST` | `/v1/ops/joint-approvals/{id}/approve` | Supervisor only | Approves a joint-account request |

### Status codes

| Status | Meaning |
|---|---|
| `200`/`204` | Success |
| `400` | Request failed validation |
| `401` | Missing, unknown or expired session |
| `403` | The session's role is not allowed to perform this action |
| `404` | Not found |
| `422` | A downstream business-rule failure (e.g. a failed account opening/transfer/deposit) |
| `500` | Unexpected error |

## Cascade revocation (the most important behaviour in this build)

`docs/DESIGN.md` Section 2.3, decision #2: whenever this service calls `StaffIdentityApi PATCH .../role` or `.../status` (via `StaffManagementService`), every active `OpsSession` row for that staff member is deleted in the same request, before returning success - the actual "instant revocation" mechanism. See `tests/Garbati.OpsApi.Tests/Controllers/CascadeRevocationTests.cs`.

## Deployment

```bash
docker build -t opsapi .                           # the API
docker build --target migrator -t opsapi-migrator . # applies pending migrations, then exits
```

GitHub Actions (`.github/workflows/ci.yml`) runs the tests and builds both images on every pull request and push to `main`. The `deploy` job is a placeholder (same bootstrapping every sibling service's first build task went through) - getting `test`/`docker` green is this task's own done-means.

## Project layout

```
src/Garbati.OpsApi/
  Controllers/      SessionController, CustomerLookupController, TestingConsoleController,
                     StaffManagementController, JointApprovalConsoleController
  Services/         Interfaces/, Implementations/
  Clients/           This service as a client of all four downstream services:
                      Contracts/{StaffIdentity,Identity,Orchestrator,Ledger}/, Interfaces/, Implementations/
  Entities/          OpsSession
  Data/              OpsDbContext
  Migrations/
  Authorization/     SessionAuthenticationHandler, OpsRoles, OpsSessionCookie, SessionTokenHasher
  Configuration/     DownstreamServicesOptions, OpsSessionOptions
  Contracts/         This service's own browser-facing request/response records
  Middleware/        Correlation id, exception-to-status mapping
  Exceptions/
  wwwroot-console/   Placeholder static assets for the eventual ops-console frontend (T-029)
tests/Garbati.OpsApi.Tests/
  Controllers/       Integration tests against a real Postgres container + fake downstream clients
  Clients/           Each downstream client's own request-building/response-parsing + scope-correctness
  Authorization/, Configuration/, Data/, Fixtures/
docs/DESIGN.md        Design document and decisions log
```

## Out of scope (T-034's job)

`OpsReversalRequest`, `OpsCustomerEditRequest`, and every endpoint under `docs/DESIGN.md` Section 6 groups 2 and 3. `OrchestratorApi`'s reversal endpoint is never called from this build.
