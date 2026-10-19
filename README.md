# Garbati.OpsApi

GARBATI's ops console backend-for-frontend. Built with ASP.NET Core 8, EF Core and PostgreSQL, matching the rest of the platform.

This is the **only** thing the ops console's browser talks to. It holds every real service credential (`StaffIdentityApi`, `IdentityApi`, `OrchestratorApi`, `LedgerApi`) server-side, issues staff login sessions as `HttpOnly`/`Secure`/`SameSite` cookies, and enforces role-based authorization and maker-checker for every staff action — the "ops channel" [Garbati.IdentityApi](https://github.com/AbubakarImam/Garbati.IdentityApi)'s reversal-authority design has been waiting on since 18 October 2026.

The design, including every trade-off, is in [docs/DESIGN.md](docs/DESIGN.md) — start there for the *why*. See [`garbati-platform/ARCHITECTURE.md`](https://github.com/AbubakarImam/garbati-platform/blob/main/ARCHITECTURE.md) Section 3.1 for how this service fits the ops-console flow: **Ops console (browser) → OpsApi → StaffIdentityApi / IdentityApi / OrchestratorApi / LedgerApi**.

## Status

Design v1 (minimal) ratified, 19 October 2026. Not yet built. The design surfaces two real gaps on already-shipped sibling services that block part of this version's scope — see `docs/DESIGN.md` Sections 5.4 and 6.6.1 — both proposed as follow-up tasks, not assumed.

## This service's one caller

The ops console's browser (React + TypeScript, not yet built — `garbati-platform` T-029) — nothing else. No other GARBATI service calls `OpsApi`, and `OpsApi` is the only caller of `StaffIdentityApi`.
