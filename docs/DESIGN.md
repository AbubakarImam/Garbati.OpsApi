# Garbati.OpsApi — Design Document (v1, minimal)

**Status:** Design v1 (minimal) — ratified, not yet built.
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

1. **Customer & account lookup** — search/view, proxying `IdentityApi` (`identity.customer.read`) and `LedgerApi` (`ledger.read`) reads, combined into one response the frontend doesn't have to assemble from two calls itself.
2. **Account/customer detail updates** — stages an `OpsCustomerEditRequest` (4.1); a separate approve/reject action applies it via `IdentityApi` (`identity.customer.write`).
3. **Reversal requests** — create (5.1), list/queue (by role per 4), approve/reject (5.2), decided live against 5.4's current limit.
4. **Transfer/transaction testing console** — account-opening and transfers via `OrchestratorApi` (`orchestrator.account-openings`/`.transfers`); funding directly via `LedgerApi` (3.4), `Teller`/`Supervisor` only.
5. **Staff & role management** — thin pass-through to `StaffIdentityApi`'s own endpoints (create/list/role/status/supervisor), `Administrator`-only (4).
6. **Joint-account approval test console** — thin pass-through to `IdentityApi`'s existing single-request lookup/approve endpoints, explicitly labeled in the UI as a stand-in for a real customer channel that does not exist yet (6.6.1) — useful for demo/QA, not a claim that staff normally approve a customer's own joint-account mandate action.

### 6.6.1 The second real gap this design surfaces: no queue, only lookup-by-id

`IdentityApi`'s `AuthorizationController` has `GET /v1/authorization/requests/{id}` (fetch one, by id) and nothing that lists multiple pending requests (confirmed directly against its source — no plain `[HttpGet]` list route exists). A "queue" with nothing to list is not a queue. **This is a genuine, minimal, read-only contract addition to `IdentityApi`** — not a judgment call this document can make unilaterally (API contract changes are an explicit human-only gate), so it is surfaced here as a proposed follow-up task rather than assumed. Until it exists, item 6's "test console" can only operate on a request id a caller already knows (e.g., one just created in the same session) — still useful for demo/QA purposes, just not a browsable queue.

## 7. What "done" looks like for the first build task

A follow-up task builds: `OpsSession`/`OpsReversalRequest`/`OpsCustomerEditRequest` EF Core entities and migrations, the four downstream `HttpClient`s with the exact scopes in Section 3, session cookie issuance/validation middleware, the Section 4 authorization matrix as enforcement (not just documentation), and the endpoints in Section 6. Tests cover: login/logout, idle/absolute session expiry, cascade revocation on role/status change (2.3), the reversal maker-checker hard rule (requester ≠ approver, approver must hold `Supervisor` at decision time, re-checked live), and the account-detail-edit equivalent (4.1).

## 8. Open questions / explicitly deferred

1. **Two real contract gaps found by this design, not yet scheduled as tasks:** `OrchestratorApi` needs a reversal-execution endpoint (5.4); `IdentityApi` needs a read-only "list pending authorization requests" endpoint (6.6.1). Both are new work on already-shipped services and both are proposed, not decided, by this document.
2. Whether `ComplianceOfficer`/`RiskOfficer` need any *write* access to the reversal queue (e.g., flagging a request for further review, distinct from approving it) — not in this version; Section 4 gives them read-only oversight only, which may prove insufficient once this is used for real.
3. MFA for privileged actions (approving a reversal, editing a customer) — `Garbati.StaffIdentityApi/docs/DESIGN.md` flagged this without a concrete mechanism; still not designed here either.
4. Session store today is `OpsApi`'s own Postgres database (sharing `LedgerApi`'s server, per the platform's existing cost-sharing convention) — fine at this scale; a distributed cache (Redis) would be the natural upgrade if `OpsApi` is ever horizontally scaled, not needed now.
