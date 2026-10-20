# Garbati.OpsApi — Design Document (v1, minimal)

**Status:** Design v1 (minimal) — ratified and **foundation built** (`garbati-platform` T-033, `feat/foundation` — see Appendix A for build notes and judgment calls). Sections 5 and Section 6 groups 2/3 (the two maker-checker workflows) are not built yet — that is T-034's job, on top of this foundation.
**Last updated:** 19 October 2026
**Service name:** `OpsApi` (`Garbati.OpsApi`)
**Platform:** Garbati — see [`garbati-platform/ARCHITECTURE.md`](https://github.com/AbubakarImam/garbati-platform/blob/main/ARCHITECTURE.md) Section 2 (service map), Section 3.1 (ops-console dependency chain) and P-15. This document never repeats that file, only this service's detail.
**Decision history:** `garbati-platform/TASKS.md` D-13 (ops console initiative), T-026 (`research/staff-ops-identity-patterns.md`), T-027 (`Garbati.StaffIdentityApi/docs/DESIGN.md` — the staff/role model this service authorizes against).

## 1. Purpose and scope

`OpsApi` is the **only** thing the ops console's browser ever talks to. It owns:

- **The staff-facing session** — login, an `HttpOnly`/`Secure`/`SameSite=Strict` cookie, instant revocation.
- **Every real service credential** for `StaffIdentityApi`, `IdentityApi`, `OrchestratorApi` and `LedgerApi` — none of which the browser ever sees.
- **Role-based authorization for every staff action** — `StaffIdentityApi` only reports *who* someone is and *what role* they hold (`Garbati.StaffIdentityApi/docs/DESIGN.md` 1.1); deciding whether that role may do a *specific* thing right now is entirely this service's job.
- **Maker-checker enforcement for staff-initiated reversals** — the "ops channel" `IdentityApi`'s reversal-authority design (its Section 8) has been waiting on since 18 October 2026.

It owns **no truth of its own** about customers, products, money, or staff identity — the same "orchestrates, owns nothing" role `OrchestratorApi` plays on the customer-facing path (`ARCHITECTURE.md` Section 3.1). Its own database holds exactly two kinds of thing: staff sessions (2), and in-flight reversal-request workflow state (5) — nothing else.

### 1.1 Dependency rule this document follows (ARCHITECTURE.md Section 3, applied to staff actions)

`ARCHITECTURE.md`'s existing rule — "LedgerApi calls nobody... the Orchestrator is the single gate for every ledger-state-changing action" (also stated directly in `Garbati.IdentityApi/docs/DESIGN.md` Section 8.9) — governs `OpsApi` exactly as it governs any other caller. **`OpsApi` never holds a `LedgerApi` scope that moves money through any path other than `OrchestratorApi`**, with one deliberate, narrow exception (3.4, funding).

## 2. Staff sessions

### 2.1 Login

`POST /v1/ops/login` → calls `StaffIdentityApi POST /v1/staff/authenticate` (own service credential, scope `staffidentity.authenticate`). On success, creates an `OpsSession` row and sets a cookie containing only an opaque, random session token — never the `staffId` or `role` directly (OWASP Session Management Cheat Sheet, cited in T-026 Q3). On failure, returns `StaffIdentityApi`'s own generic `401` unchanged — no additional information added or removed.

### 2.1a Two endpoints T-029 (the frontend) needs that this document didn't originally specify

Found while designing the frontend, the same way T-028 itself surfaced real gaps while designing against T-027 — not invented scope, a genuine missing piece a caller needs:

- **`GET /v1/ops/me`** — because the session cookie is `HttpOnly` by design (2.1 — the browser's JavaScript cannot read it), the frontend has no way to know who is logged in, or what role they hold, without asking. Every full page load (a refresh, a bookmarked URL) needs this to rebuild its UI state — it's not optional, the SPA is otherwise blind to its own login state. Returns `{staffId, fullName, role}` from the current session (2.2's cached fields — no downstream call), or `401` if the session cookie is missing/expired/revoked.
- **`POST /v1/ops/logout`** — deletes the calling session's `OpsSession` row (the same cascade-revocation mechanism 2.3 already uses for a role/status change, just triggered by the staff member themselves) and clears the cookie.

Neither needs a new scope or touches any downstream service — both are pure `OpsSession`-table operations.

### 2.1b Same-origin hosting (decision #6) — resolves a real conflict with 2.2's `SameSite=Strict` choice

**The conflict:** `SameSite=Strict` cookies (2.2, chosen on OWASP's own recommendation, T-026 Q3) are not sent on a cross-site request — if the frontend and `OpsApi` ever ended up on different domains (e.g. a separately-hosted static site calling an API on another domain), the login cookie would simply never reach `OpsApi` on any subsequent request, breaking the entire session mechanism this document already committed to. Relaxing to `SameSite=None` to work around that would reopen exactly the CSRF exposure `Strict` was chosen to close — walking back a considered decision to paper over a hosting choice, not a good trade.

**Decision #6:** the ops console's built static assets are served by `OpsApi` itself, from the same origin — no second hosting resource, no CORS configuration anywhere, and `SameSite=Strict` works exactly as intended because there is no cross-site request to begin with. `OpsApi`'s own build needs `app.UseStaticFiles()` serving a dedicated folder (not `wwwroot`'s implicit root, to avoid any collision with anything else this API might ever serve from there) plus `MapFallbackToFile` for that folder's `index.html`, so a full-page load of any client-side route (e.g. a bookmarked `/reversals/OPS-REV-...`) still resolves to the React app rather than a `404` — standard SPA-hosting shape for ASP.NET Core, not a new pattern.

**How two independently-deployed repos land on one origin:** the ops-console frontend repo (T-029) gets its **own** GitHub Actions deploy job, with its **own** scoped Azure credential — but deploying *into that same dedicated static-assets folder* on `OpsApi`'s already-running Azure Web App, via a plain zip/file deploy (no `dotnet publish`, nothing API-related), not by touching `OpsApi`'s own repository or build pipeline at all. The two deploy pipelines stay fully independent (matching this platform's existing one-repo-per-service convention) while still landing on one origin. **Local development:** Vite's dev server has a built-in reverse-proxy option (`server.proxy`) that forwards `/v1/ops/*` requests from its own dev port to a locally-running `OpsApi` — the browser sees same-origin requests throughout, in dev and in production alike, so no CORS code path needs to exist in `OpsApi` at all, in any environment.

### 2.2 `OpsSession` (own database table)

| Field | Notes |
|---|---|
| `SessionId` | Internal row id. |
| `SessionTokenHash` | SHA-256 of the raw token — the raw token lives only in the cookie and is never stored, mirroring the hash-only convention every GARBATI service already uses for API keys (`ARCHITECTURE.md` Section 5). A database leak alone cannot be used to forge a session. |
| `StaffId`, `FullName`, `Role` | Captured at login time (3.1 explains why this is safe to cache for a session's lifetime, not re-fetched per request). |
| `CreatedAt`, `LastActivityAt`, `ExpiresAt` | |

**Decision #1 (timeouts):** 30-minute idle timeout, 8-hour absolute maximum. Reasonable starting values for a small back-office staff, not an architectural commitment — easy to change later, same spirit as `IdentityApi`'s 30-second grant expiry being a specific, documented, adjustable number rather than a load-bearing constant.

### 2.3 Why role is cached for the session instead of re-checked against StaffIdentityApi on every request

`IdentityApi`'s own principle — "never cache positive authorization decisions... fail closed on any outage" (D-9) — is about *customer* authorization decisions that can change at any moment from an action outside the deciding system's control. Staff role changes are different: **every** staff change (`PATCH /v1/staff/{staffId}/role` or `.../status`) can only ever be made *through `OpsApi` itself*, because `OpsApi` is `StaffIdentityApi`'s only caller (`Garbati.StaffIdentityApi/docs/DESIGN.md` 1). That means `OpsApi` is never in the dark about a change happening "elsewhere" — it is the one place any such change has to pass through.

**Decision #2 (cascade revocation, not polling):** whenever `OpsApi` calls `PATCH /v1/staff/{staffId}/role` or `.../status` (Suspended/Terminated) on `StaffIdentityApi`, it deletes every active `OpsSession` row for that `staffId` in the same request, before returning success to the calling Administrator. This gives the "instant revocation" property sessions are supposed to provide (T-026 Q3) without a round trip to `StaffIdentityApi` on every single ops action — the alternative (re-validating role on every request) would add latency and a hard per-request dependency on `StaffIdentityApi`'s availability for no real gain, since there is no path by which a role change could reach `OpsApi`'s session store without `OpsApi` already knowing about it.

## 3. Downstream service clients (least privilege per service)

Each is a plain `HttpClient` (via `IHttpClientFactory`, matching `OrchestratorApi`'s own proven pattern) carrying `OpsApi`'s own `X-Api-Key` and propagating `X-Correlation-ID`, exactly like every other inter-service client on this platform.

| Service | Scopes granted | Why exactly these |
|---|---|---|
| `StaffIdentityApi` | `staffidentity.authenticate`, `staffidentity.admin` | Both needed: login (2.1) and every staff/role management screen action (6.5). |
| `IdentityApi` | `identity.customer.read`, `identity.customer.write`, `identity.authorization.check` | **Not granted:** `identity.ops.reversal` — this scope belongs to `OrchestratorApi` instead (5.3's correction), not `OpsApi`; `OpsApi` never calls `IdentityApi` about a reversal at all. Also not granted: `identity.kyc.manage` (KYC decisioning isn't in this version's scope — `ARCHITECTURE.md`'s existing KYC-provider gap is unchanged by this service), `identity.mandate.read`/`.write` (mandate attachment stays `OrchestratorApi`'s job during account opening; no v1 screen edits a mandate directly), `identity.ops.freeze` (freeze/unfreeze wasn't in this round's v1 scope — natural v2 addition, not built now). |
| `OrchestratorApi` | `orchestrator.account-openings`, `orchestrator.transfers` | The testing console (6.4) drives exactly these two existing operations. |
| `LedgerApi` | `ledger.read`, `ledger.funding` | `ledger.read` for the lookup screen (6.1). `ledger.funding` is the one deliberate exception to 1.1 (3.4) — **not** `ledger.accounts`, `ledger.transfer`, `ledger.reverse`, or `ledger.admin`, all of which stay exclusively behind `OrchestratorApi` or out of this service's reach entirely. |

### 3.4 Why `ledger.funding` is a deliberate exception to "only the Orchestrator moves money"

A deposit is not a customer-authorized transfer between two accounts — it is new money entering the system, in person, at a teller window, with the customer physically present. There is no "who may authorize this" question for `IdentityApi` to answer the way there is for a transfer (that's exactly why `scripts/demo-key.sh`-issued keys were used to fund accounts directly against `LedgerApi` during this platform's own first live end-to-end test, 18 October 2026 — not through `OrchestratorApi`, because there was nothing for `OrchestratorApi` to orchestrate). The testing console's funding action (6.4, `Teller`-only) reflects that same real-world shape, not a shortcut around the dependency rule — `OrchestratorApi` genuinely has no funding endpoint to call, and inventing one there only to immediately call it from here would add a layer with nothing to decide.

## 4. Authorization matrix (role → v1 action)

| Action | Teller | CustomerServiceOfficer | Supervisor | ComplianceOfficer | RiskOfficer | Auditor | Administrator |
|---|---|---|---|---|---|---|---|
| Customer & account lookup (read-only, 6.1) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Update customer details (6.2) | | ✓ (maker) | ✓ (checker, 4.1) | | | | |
| Open account / fund / transfer — testing console (6.4) | ✓ | | ✓ | | | | |
| Request a reversal (6.3) | ✓ | ✓ | | | | | |
| Approve/reject a reversal (6.3) | | | ✓ | | | | |
| Reversal queue — read-only oversight (6.3) | | | ✓ | ✓ | ✓ | ✓ | |
| Joint-account approval test console (6.6) | | | ✓ | | | ✓ (read-only) | |
| Staff & role management (6.5) | | | | | | | ✓ |

This table is this service's actual enforcement surface (every endpoint in Section 6 checks the calling session's `Role` against it) — `StaffIdentityApi`'s own design deliberately stops short of defining this (`Garbati.StaffIdentityApi/docs/DESIGN.md` 1.1), so this is the first place any of it is actually decided.

### 4.1 Account-detail-update maker-checker (new this round, alongside the `CustomerServiceOfficer` role and `SupervisorStaffId`, `Garbati.StaffIdentityApi/docs/DESIGN.md` decision #4)

A `CustomerServiceOfficer`'s customer-detail edit is **not applied immediately** — it is staged (`OpsCustomerEditRequest`, same shape as `OpsReversalRequest` in 5.1, with a `TargetCustomerId` and a JSON `ProposedChanges` payload instead of a transaction reference) and requires a `Supervisor`'s approval before `OpsApi` calls `IdentityApi PATCH`-equivalent `identity.customer.write` to actually apply it. **Decision #3:** any `Supervisor` may approve, not strictly the requester's own named `SupervisorStaffId` — same reasoning as 5.2's identical choice for reversals: requiring one specific named person risks blocking a real action when that person is unavailable, and the control that actually matters (different person, correct role) holds either way. The requester's own supervisor is shown first in the UI's queue ordering, not hard-enforced.

## 5. Reversal requests (`OpsReversalRequest` — the maker-checker workflow `IdentityApi`'s design has been waiting on)

### 5.1 Entity (own database table — this workflow's state belongs to `OpsApi`, not `IdentityApi`, exactly as `Garbati.StaffIdentityApi/docs/DESIGN.md`'s boundary intends)

| Field | Notes |
|---|---|
| `Id` | Public id, `OPS-REV-XXXXXXXXXX`. |
| `OriginalTransactionReference` | The `LedgerApi` `TXN-...` reference being reversed. |
| `Amount`, `Currency` | Caller-supplied at request time — `LedgerApi` has no lookup-by-reference endpoint to derive these from (checked directly against its source; `TransactionResponse` doesn't even carry an amount), and the staff member filing the request already has these from whatever record prompted it. Mirrors `TransferRequest`/`AuthorizationRequest` already requiring the caller to supply amount/currency rather than deriving it from anywhere — not a new pattern. |
| `ReasonCode` | One of `IdentityApi`'s existing `ReversalReasonCode` values (`DUPL`/`TECH`/`FRAD`/`CUST`/`UNAUTH`) — reused, not duplicated, since the eventual authorization call (5.3) needs exactly this vocabulary. |
| `RequestedByStaffId` | Must hold `Teller` or `CustomerServiceOfficer` (4). |
| `Status` | `PendingApproval` → `Approved`/`Rejected` → (once 5.4's gap is closed) `Executed`/`ExecutionFailed`. `Expired` after 24 hours in `PendingApproval` — longer than `IdentityApi`'s joint-approval 1-hour window (T-018), a deliberately different operational cadence: a live customer transfer approval is time-pressured, a staff review of a reversal is not. |
| `ApprovedByStaffId` | Nullable. **Decision #4 (hard rule, enforced in code, not just the UI):** must not equal `RequestedByStaffId`, and must currently hold `Supervisor` — re-checked live at decision time, not just at queue-display time, mirroring T-018's own "re-check every approver's standing live at the moment of completion" pattern. Any `Supervisor` qualifies (same reasoning as 4.1). |
| `RejectionReason` | Required free text if `Status = Rejected`. |
| `RequestedAt`, `DecidedAt` | |

### 5.2 Why "any Supervisor," not strictly the requester's own named one

`Garbati.StaffIdentityApi/docs/DESIGN.md` decision #4 explicitly left this open for this document to decide. Requiring the one specific named supervisor is a real option, but it creates a real operational risk this version chooses not to take on: if that one person is unavailable (leave, suspension, termination), a time-sensitive reversal has no path to approval at all. The actual control a maker-checker rule protects — a different, correctly-privileged person reviews the action — holds either way. The requester's named supervisor (if any) is simply shown first in the UI queue.

### 5.3 Who actually asks IdentityApi — corrected to match the existing Transfer pattern

**Decision #5 (correcting an initial inconsistency in this same document):** `OpsApi` does **not** call `IdentityApi` about a reversal at all — no `identity.ops.reversal` scope for this service (3). Instead, `OpsApi`'s only action on `Approved` is to call `OrchestratorApi`'s reversal-execution endpoint (5.4) once it exists, the same way the testing console (6.4) already treats `OrchestratorApi`'s existing transfer endpoint as one self-contained call. `OrchestratorApi` itself asks `IdentityApi` for the grant, executes against `LedgerApi`, and reports the outcome back to `IdentityApi` — exactly the shape `TransferService` already uses for a transfer (ask, then execute, both inside the Orchestrator, never split across two different callers). An earlier draft of this section had `OpsApi` obtaining the grant itself and `OrchestratorApi` only executing it — inconsistent with that proven pattern, and corrected here before any task file was written against it.

### 5.4 The real gap this design surfaces: nothing can execute the reversal yet

Per 1.1's dependency rule, only `OrchestratorApi` may call `LedgerApi`'s own `ledger.reverse`-scoped chain-reversal endpoint — "the Orchestrator is the single gate for every ledger-state-changing action" (`Garbati.IdentityApi/docs/DESIGN.md` Section 8.9, restating decision #17). **`OrchestratorApi` has no reversal endpoint today** (confirmed directly against its source — grepped for any `Reversal`-named controller action; none exists). This is not an oversight this document can quietly route around — `OrchestratorApi`'s own v1 design explicitly deferred reversal ("IdentityApi's reversal authority ... was deliberately left undesigned" at the time, `Garbati.OrchestratorApi/docs/DESIGN.md` 1.1), and that design has not been revisited since T-020 built the authorization side.

**This version ships with `Status` stopping at `Approved`** — a real, usable maker-checker queue, fully built and testable, with execution genuinely blocked on a new `OrchestratorApi` endpoint (`garbati-platform` D-14/T-030). Once that endpoint exists, `OpsApi`'s only addition is: on `Approved`, call `OrchestratorApi POST /v1/reversals`; store its response (`Executed`/`ExecutionFailed`) on the `OpsReversalRequest` row. `OrchestratorApi` itself closes the loop with `IdentityApi` via the already-built `POST /v1/authorization/requests/{id}/reversal-outcome` (T-020) — `IdentityApi`'s own design anticipated this call coming from "the ops channel (or the Orchestrator)" (Section 8); this document settles it as the Orchestrator, for the reasons in 5.3.

## 6. Endpoints `OpsApi` exposes to the frontend (T-029's contract)

All under `/v1/ops`, session-cookie-authenticated (2), each individually checked against Section 4's matrix. Not a line-by-line API spec (that's T-029's and the eventual build task's job) — the shape each v1 scope item takes:

0. **Session** (2.1a) — `POST /v1/ops/login`, `GET /v1/ops/me`, `POST /v1/ops/logout`.
1. **Customer & account lookup** — search/view, proxying `IdentityApi` (`identity.customer.read`) and `LedgerApi` (`ledger.read`) reads, combined into one response the frontend doesn't have to assemble from two calls itself.
2. **Account/customer detail updates** — stages an `OpsCustomerEditRequest` (4.1); a separate approve/reject action applies it via `IdentityApi` (`identity.customer.write`).
3. **Reversal requests** — create (5.1), list/queue (by role per 4), approve/reject (5.2). Execution is live, not just authorized — `garbati-platform` D-14/T-030 built and live-verified `OrchestratorApi POST /v1/reversals` on 19 Oct 2026.
4. **Transfer/transaction testing console** — account-opening and transfers via `OrchestratorApi` (`orchestrator.account-openings`/`.transfers`); funding directly via `LedgerApi` (3.4), `Teller`/`Supervisor` only.
5. **Staff & role management** — thin pass-through to `StaffIdentityApi`'s own endpoints (create/list/role/status/supervisor), `Administrator`-only (4).
6. **Joint-account approval test console** — thin pass-through to `IdentityApi`'s single-request lookup/approve endpoints, **now including a real browsable queue** (`garbati-platform` D-15/T-031 built `GET /v1/authorization/requests?status=Pending`, 19 Oct 2026 — 6.6.1's gap below is resolved), explicitly labeled in the UI as a stand-in for a real customer channel that does not exist yet — useful for demo/QA, not a claim that staff normally approve a customer's own joint-account mandate action.

### 6.6.1 Resolved: IdentityApi now has a real queue, not just lookup-by-id

Originally: `IdentityApi`'s `AuthorizationController` only had `GET /v1/authorization/requests/{id}` (fetch one, by id) — nothing listed multiple pending requests, so a "queue" had nothing to browse. **Resolved by T-031** (merged, deployed, 19 Oct 2026): `GET /v1/authorization/requests?status=Pending&pageSize=&pageToken=`, reusing the existing scope, paged, correctly excluding single-holder requests (which never create the row type this reads) and stale-past-expiry rows. Item 6's test console can now show a real browsable queue, not just operate on a request id already known.

## 7. What "done" looks like for the first build task

A follow-up task builds: `OpsSession`/`OpsReversalRequest`/`OpsCustomerEditRequest` EF Core entities and migrations, the four downstream `HttpClient`s with the exact scopes in Section 3, session cookie issuance/validation middleware, the Section 4 authorization matrix as enforcement (not just documentation), `app.UseStaticFiles()`/`MapFallbackToFile` for the frontend's assets (2.1b), and the endpoints in Section 6 (including `login`/`me`/`logout`, 2.1a). Tests cover: login/logout, `GET /v1/ops/me` on a missing/expired/revoked session, idle/absolute session expiry, cascade revocation on role/status change (2.3), the reversal maker-checker hard rule (requester ≠ approver, approver must hold `Supervisor` at decision time, re-checked live), and the account-detail-edit equivalent (4.1).

## 8. Open questions / explicitly deferred

1. **Both real contract gaps found by this design are now resolved.** `OrchestratorApi`'s reversal-execution endpoint (5.4) and `IdentityApi`'s pending-requests list endpoint (6.6.1) were both built, deployed, and live-verified 19 October 2026 (`garbati-platform` D-14/T-030, D-15/T-031).
2. Whether `ComplianceOfficer`/`RiskOfficer` need any *write* access to the reversal queue (e.g., flagging a request for further review, distinct from approving it) — not in this version; Section 4 gives them read-only oversight only, which may prove insufficient once this is used for real.
3. MFA for privileged actions (approving a reversal, editing a customer) — `Garbati.StaffIdentityApi/docs/DESIGN.md` flagged this without a concrete mechanism; still not designed here either.
4. Session store today is `OpsApi`'s own Postgres database (sharing `LedgerApi`'s server, per the platform's existing cost-sharing convention) — fine at this scale; a distributed cache (Redis) would be the natural upgrade if `OpsApi` is ever horizontally scaled, not needed now.

---

## Appendix A. T-033 build notes (foundation: sessions, downstream clients, lookup/testing-console/staff-management/joint-approval endpoints)

Built everything in Section 6 groups 0/1/4/5/6 in one task, on `feat/foundation`. `OpsReversalRequest`, `OpsCustomerEditRequest` and Section 6 groups 2/3 are explicitly **not** built — T-034's job. Judgment calls made along the way, beyond what the task file already settled:

1. **Inbound role enforcement uses ASP.NET Core's own built-in `[Authorize(Roles = "...")]`, not a bespoke policy-per-scope mechanism.** Every sibling service enforces its own callers' permissions with one `AddPolicy` per API-key scope (`OrchestratorScopes`, `LedgerScopes`, `StaffIdentityScopes`), because those services authenticate callers by API key and a key can hold an arbitrary combination of scopes. OpsApi's own callers authenticate by session, and a session carries exactly one `Role` (`Garbati.StaffIdentityApi`'s own model, Section 2.2) — there is no "combination of roles" to express, so the framework's own `Roles` check on a single `ClaimTypes.Role` claim is the "one shared, reusable check" the task file asks for, with no new attribute type needed. `GET /v1/ops/me` and `POST /v1/ops/logout` use a bare `[Authorize]` (any authenticated session) since Section 4's matrix does not govern either action.
2. **`IIdentityApiClient` includes three authorization-request methods (`ListPendingAuthorizationRequestsAsync`/`GetAuthorizationRequestAsync`/`ApproveAuthorizationRequestAsync`) that the task file's own "Downstream clients" bullet list for this client did not name** (it listed only "customer read"). Section 6 item 6 (the joint-account approval test console) and Section 3's own scope table (which grants `identity.authorization.check`) both require them, and the task file's framing is "build the client interfaces/implementations for every operation this task's endpoints need" — read as the bullet list being non-exhaustive rather than as a reason to leave group 6 unbuilt. Flagging this explicitly rather than silently deciding it.
3. **`IIdentityApiClient.GetAuthorizationRequestAsync` assumes the id is always a `PendingApprovalRequest` id** (the joint-approval row type this test console's queue produces), never a single-holder `AuthorizationDecision` id — `IdentityApi`'s own `GET /v1/authorization/requests/{id}` endpoint can return either shape, and this client only deserializes the first. This is safe for what this console actually calls (every id it operates on comes from its own `ListPendingAsync` queue, which is scoped to `PendingApprovalRequest` rows only — confirmed against `Garbati.IdentityApi.Services.JointApprovalService.ListPendingRequestsAsync`'s own comment), but the client would silently produce a mostly-null response if ever called with a single-holder decision id instead. Not guarded against with a runtime check, since nothing in this task's own endpoints can reach that path.
4. **Cascade revocation (Section 2.3) runs only after `StaffIdentityApi` confirms the role/status change succeeded, and only for `PATCH .../role`/`.../status`, not `.../supervisor` or `.../password`.** The design document names only role/status explicitly; a supervisor reassignment or a password reset do not change what a staff member is currently allowed to do, so treating them as revocation triggers would be revoking sessions for no reason tied to the design's own stated purpose. Tested directly (`CascadeRevocationTests.cs`), including the case where the same staff member has two concurrent sessions (both must be deleted) and the case where a supervisor-only change must *not* revoke anything.
5. **Session cookie tests use manual `Set-Cookie`/`Cookie` header handling, not `HttpClient`'s built-in cookie jar.** The session cookie is marked `Secure` (Section 2.1/2.2), and `WebApplicationFactory`'s default `BaseAddress` is `http://`, not `https://` — a real `CookieContainer` would correctly refuse to resend a `Secure` cookie over that scheme, which would make every cookie-dependent test fail for a reason that has nothing to do with this service's own logic. Tests extract the raw `name=value` pair from `Set-Cookie` and attach it manually as a `Cookie` header on later requests instead.
6. **No new audit table for session lifecycle events**, per the task file's own instruction — session create/destroy events rely on the existing correlation-id-tagged request logging (Serilog), same as the task file anticipated. Flagging here, not inventing one silently: if session lifecycle needs a durable, queryable audit trail beyond request logs (e.g. for a compliance review of who was logged in when), that is an open question for the owner, not decided in this task.
7. **`POST /v1/ops/staff/{staffId}/password` takes no request body.** `StaffIdentityApi`'s own endpoint serves two branches (admin-reset vs. self-service) from one shape, but OpsApi's `Administrator`-only staff-management screen (Section 6 item 5) only ever exercises the admin-reset branch — `CurrentPassword`/`NewPassword` are always sent as `null` to `StaffIdentityApi`, which ignores them on that branch. Self-service password change (a staff member changing their own password) is not a screen this task builds.
8. **Testing against hand-rolled fakes for all four downstream clients, not real locally-run `StaffIdentityApi`/`IdentityApi`/`OrchestratorApi`/`LedgerApi` instances** — the same judgment call `Garbati.OrchestratorApi`'s own foundation task made, for the same reason (standing up four more full services inside this repo's own CI run is a lot of added infrastructure for a first build). **What this does not prove:** that any of the four siblings' currently deployed code still returns exactly the response shapes this service's clients were written against on 20 October 2026. A drift in any of their contracts would not be caught by this suite; it would only surface as a `DownstreamApiException` at runtime. See the PR description for what manual verification (if any) was done beyond this automated suite.

See the PR description for how this was tested, what was run and not run, and the full LEARNING_LOG entry.
