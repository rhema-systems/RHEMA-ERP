# HR Closure — Consultant Client Portal block (build plan)

> The closure programme's consultant-client block: retire the last tenant of the bespoke
> `PortalBearer` scheme and rebuild the client surface on the main scheme inside the
> external-portal shell — the candidate-portal recipe, second run. Canonical plan; the ledger
> (`docs/HR/programme/HR-CLOSURE-LEDGER.md`) carries the queue rows this closes.

**Status: ✅ COMPLETE 2026-08-31.** All three slices delivered in one pass: retirement +
identity + rebuilt API (slice 1), the four frontend surfaces (slice 2), harness + ledger
regeneration (slice 3). Suite: `dev-harness/hr-consulting/run.mjs`, **78/78 assertions × 2
runs** (the second run also proved the 429-aware rate-limit wrapper). Scoped tsc clean, lint
clean, all five new routes 200 under `next dev`. Migration
`20260831083551_RetireConsultantClientPortalAndAddClientContacts` applied (guarded/idempotent,
listed in FastBuildMigrationMetadata). Ledger regenerated: queue 302 → 292, confirmed
unreachable 19 → 17, both surviving consultant rows `DONE`. One fixture lesson worth keeping:
SensitivePolicy is a 5/min window and the suite makes exactly five sensitive calls — a 429 is
the limiter talking, not the endpoint; the harness waits it out rather than asserting on it.

## Decisions (user, 2026-08-31)

1. **Own `ConsultantClient` role**, NOT `ExternalUser` — mirror the Candidate precedent: a
   sibling access-middleware fence with its own narrow prefix allowlist. ExternalUser's
   allowlist includes `/api/procurement` wholesale (cross-module hole #11a) plus
   projects/estate/support; a client billing contact must inherit none of it.
2. **Invite-only** — HR invites from the client detail screen (already wired); the contact
   completes setup via emailed link. The anonymous register-with-client-code flow retires
   with the portal (never had a UI; client codes are low-entropy and visible on internal
   screens).
3. **Keep both doors** — the anonymous tokenised timesheet-confirmation link (email, no
   account) AND the logged-in portal confirm/reject. Offer-response precedent.

## What the survey established

| Piece | State |
| --- | --- |
| `PortalBearer` scheme (`Security/PortalAuth.cs`) | Consultant portal is its **only remaining tenant** (comment says so since the candidate retirement) — retiring this block retires the scheme |
| `ConsultantClientPortalAuthController` (`api/client-portal/auth`, 7 anonymous writes) | No UI has ever called it; register/login/verify/resend/forgot/reset/complete-setup on the bespoke account table |
| `ConsultantClientPortalController` (`api/client-portal`, PortalBearer) | Dashboard + timesheet get/confirm/reject + change-password; no UI |
| `ClientTimesheetConfirmationController` (`api/client-timesheet-confirmation`, anonymous token) | validate/confirm/reject; no page exists at the emailed path |
| `ConsultantClientPortalAccounts` table | **0 rows** (sqlcmd 2026-08-31) — schema drop needs no data story |
| HR-side invite pair (`consultant-clients/{id}/portal-invite`, `/resend`, `/portal-accounts`) | **Already wired** from `/hr/consulting/clients/[id]` |
| HR-side send/resend confirmation (`consultant-timesheets/{id}/send-confirmation`) | **Already wired** from `/hr/consulting/timesheets/[id]`; email link = `{PortalUrl}/client-timesheet/confirm/{token}`, `PortalUrl = http://localhost:3000` (the Next app) |
| Existing data | 8 clients, 33 timesheets, 8 invoices, 8 confirmations — all confirmation rows are harness fixtures (`client_*@example.com`) |
| Internal consulting UI | `/hr/consulting` (clients, engagements, timesheets incl. detail + send-confirmation dialog) — healthy, stays |

Status chain the portal serves: `Approved → SentToClient → ClientConfirmed / ClientRejected`;
invoicing reads `ClientConfirmed`. Without a client surface the chain dead-ends at
`SentToClient` — "consultant billing has no client step".

## Target architecture

- `Constants.Roles.ConsultantClient = "ConsultantClient"` — seeded beside Candidate
  (`DatabaseSeedingService` ~7796), added to `IsProtectedSystemRole`, **named in the
  `InternalOnly` blocklist** (the recruitment-block lesson: InternalOnly is a blocklist — a
  new public role satisfies it until named), and in `auth-routing.ts`.
- `ConsultantClientAccessMiddleware` — a **sibling** of the Candidate fence, no admin bypass.
  Allowlist (each prefix audited for bare `[Authorize]` before listing):
  `/api/auth`, `/api/tenant`, `/api/notifications`, `/api/hubs`, `/api/user/profile`,
  `/api/user/change-password`, `/api/client-portal` (every endpoint `ConsultantClientOnly`),
  `/health`, `/swagger`.
- `ConsultantClientOnly` policy (assertion on the role, like `CandidateOnly`).
- **`ConsultantClientContact`** replaces `ConsultantClientPortalAccount`: the link row
  (TenantId, ConsultantClientId FK, `UserId` FK → Identity `Users` — table is named `Users`,
  not AspNetUsers —, ContactName, ContactRole, IsActive, invite audit fields). Auth columns
  (password hash, verification/reset/setup tokens, lockout) die with the table — Identity
  owns all of that now.
- Invite flow rework: HR invite → create/adopt Identity user (role ConsultantClient) +
  contact row → emailed setup link → contact sets password and proves the mailbox → login on
  the main scheme → routed to the external-portal shell. Adoption-by-email requires mailbox
  proof, not just an OTP (recruitment-block lesson). Re-invite of an existing main-scheme
  user (e.g. the same contact serving two clients) adds a contact row, not an account.
- `api/client-portal` rebuilt on the main scheme: dashboard, timesheet read, confirm,
  reject. Actor = token → UserId → contact row(s) → ConsultantClientId. Change-password
  drops (main auth owns it). A contact with rows at N clients sees N clients' pending
  timesheets (dashboard groups by client).
- `api/client-timesheet-confirmation` stays as-is; gains its page at
  `/client-timesheet/confirm/[token]` — the exact path the email template already carries.

## What retires (code + schema + config)

- `Security/PortalAuth.cs`; the `AddJwtBearer(PortalAuth.Scheme…)` registration and
  `ValidateDistinctFromInternal` startup call (`ServiceCollectionExtensions.cs` ~111, ~156);
  the `ConsultantClientPortal` policy (~1406).
- `ConsultantClientPortalAuthController`, `ConsultantClientPortalController`,
  `ConsultantClientPortalAuthService` (+ interface), `ConsultantClientPortalJwtService`
  (+ interface), the `IPasswordHasher<ConsultantClientPortalAccount>` registration
  (`HrModuleServiceRegistration.cs` ~819–842).
- `ConsultantClientPortalAccount` entity, DbSet, snapshot model, table (drop migration —
  user scaffolds `RetireConsultantClientPortalAndAddClientContacts`, I edit + list in
  `FastBuildMigrationMetadata`, user updates; the migration also creates
  `ConsultantClientContacts`).
- Portal-only DTOs in `AttendanceDTOs.cs` (register/login/verify/forgot/reset/
  complete-setup/change-password + auth result); invite/summary DTOs survive reshaped.
- `JwtSettings:PortalSecretKey` / `PortalAudience` config keys (all appsettings variants).
- The portal service's `RequireCurrentTenant` dance (portal JWT tenant claim) — main-scheme
  tokens carry tenant like every other HR surface.

**Keep:** the internal consulting surface untouched; `SendConfirmationAsync` /
`ResendConfirmationAsync` / `ValidateConfirmationTokenAsync` / `ConfirmByClientAsync` /
`RejectByClientAsync` and the `ConfirmByPortalClientAsync` / `RejectByPortalClientAsync`
pair (re-plumbed to the contact context).

## Slices

### Slice 1 — identity, fence, retirement, rebuilt API (backend)

1. Role + `IsProtectedSystemRole` + seeding + `InternalOnly` + `ConsultantClientOnly` +
   `ConsultantClientAccessMiddleware` (registered beside the Candidate fence, Program.cs ~560).
2. `ConsultantClientContact` entity + migration handoff (drop portal accounts, create
   contacts). ⚠ three homes: entity/DbContext, the migration, the snapshot.
3. Invite service rework onto Identity (`InvitePortalAccountAsync` → `InviteContactAsync`
   semantics kept for the wired HR endpoints; response DTO reshaped to contact + account
   status). Setup email → main-scheme setup page. Resend keeps its cooldown (a resend
   endpoint is a mailbox-bombing tool without one).
4. Rebuild `api/client-portal` (`ConsultantClientOnly`): `GET dashboard`,
   `GET timesheets/{id}`, `POST timesheets/{id}/confirm`, `POST timesheets/{id}/reject`.
   Read the DTOs against what the screens must render (per-collection read ≠ summary).
5. Delete the portal auth stack + scheme + config.
6. Standing checks on every touched write: actor from token (never the body), tenant
   stamped, Guid.Empty guards (D-17 shape).

**Gate:** user scaffolds migration mid-slice; user rebuilds (I stop the running API first);
harness slice A green ×2.

### Slice 2 — frontend

1. Public page `/client-timesheet/confirm/[token]` — validate → entries table →
   confirm/reject with notes; expired/responded states. Anonymous; body-class print-safe not
   needed; UI-payload probe against the running API before any TypeScript (no fiction types).
2. Setup page for the invite link (path decided by the new email template) — set password,
   prove mailbox, land in the shell.
3. External-portal shell: `isConsultantClientUser` in `auth-routing.ts` (NOT folded into
   `isExternalPortalUser`), home path → the client section; nav section with dashboard +
   timesheet detail (confirm/reject + history).
4. HR side: client detail invite panel re-pointed at the reshaped DTOs; portal-accounts
   panel becomes contacts panel.
5. Checks: scoped tsc vs stashed baseline (`npm run type-check` crashes on the clean tree —
   scoped tsconfig only), lint, route resolution under `next dev`.

### Slice 3 — harness, ledger, close-out

1. `dev-harness/hr-consulting/`: setup.mjs (fixture client/engagement/timesheet →
   approve → send-confirmation; sqlcmd `-I -b` as the mailbox stand-in for setup + reset
   tokens), then:
   - anonymous door: validate→confirm; validate→reject; expiry; double-respond refusals
   - invite → setup → login → dashboard → portal confirm/reject; unverified refusals
   - fence: ConsultantClient token 403s outside the allowlist (probe an HR read, a
     procurement read, an estate read); `InternalOnly` refuses the role; internal token
     refused on `ConsultantClientOnly`
   - no-regression: internal consulting routes still green
2. Regenerate the ledger (dispositions in `scripts/hr-coverage/04_build_ledger.py`):
   `ConsultantClientPortalAuth` → retired/DONE, `ConsultantClientPortal` → DONE,
   `ClientTimesheetConfirmation` → DONE, C-section `ConsultantClientPortalAuth` row closed.
3. Stage everything; hand over the commit message; update memory.

## Risks / watch-list

- **The fence is load-bearing**: every prefix allowlisted must be audited for bare
  `[Authorize]` beneath it before it ships (candidate-precedent rule).
- EF fixup on same-context response reads (recruitment lesson) — the contact→client
  navigation on invite responses.
- `TryGetEmployeeWriteContext` does not apply — the portal actor is a User, not an Employee;
  the contact row is the authorisation anchor.
- Multi-client contacts: unique index on (TenantId, ConsultantClientId, UserId); soft
  delete must not make a contact permanently un-reinvitable (unique-index face #9 —
  filtered index or revive-on-upsert).
- The dashboard's `GetByClientIdAsync` + in-memory filter — check tenant scoping inside the
  repo before reuse (tenantless-paging shape found in recruitment).
