# GL Classification and Revaluation Refactor — Execution Ledger

Last reconciled: 2026-09-04 (Africa/Accra)

## Objective

Execute the approved GL classification and revaluation refactor in bounded phases, preserving Finance ownership and V1 external-producer compatibility until a separately coordinated cutover.

## Current package

| Field | Value |
|---|---|
| Phase | Post-Phase-6 Stage A — migration/reset rehearsal and V1 inventory |
| Status | `APPROVED_WITH_BLOCKERS` |
| Implementing task | `Assess GL configuration changes` (`01a0648a-99d2-7320-aced-b2b22d160334`) |
| Exact base | `3c5620a3de9c5d3038be4b07b5026149815f4296` |
| Branch | `codex/finance-gl-cutover-readiness` |
| Worktree | `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP-finance-gl-cutover-readiness` |
| Primary branch | `codex/finance-budget-posting-evidence` |
| Primary HEAD at activation | `3c5620a3de9c5d3038be4b07b5026149815f4296` |
| Coordinator | Current primary Finance task |
| Recovery heartbeat | `finance-gl-cutover-coordinator` — active every 15 minutes |
| Review status | Stage A independently approved as an accurate readiness/blocker package |
| Connectivity state | Available; all Stage A work is durably committed before handoff |

## Authoritative inputs

- `docs/Finance/GL_CLASSIFICATION_REVALUATION_REFACTOR_HANDOFF.md`
- `docs/Finance/GL_CLASSIFICATION_PHASE0_ASSESSMENT.md`
- `docs/Finance/GL_CLASSIFICATION_PHASE0_CONSUMER_INVENTORY.md`
- `docs/Finance/FINANCE_AGENT_COORDINATION_PROTOCOL.md`

## Authorization

The user authorized the coordinator to:

- communicate with and manage the active implementing task directly;
- review results and issue corrections without user relaying;
- continue automatically through Phases 3–6, issuing each next phase immediately after satisfactory independent review and integration;
- cherry-pick clean, reviewed Finance commits into the primary Finance branch.

The implementing task should not be left idle between these approved phases. User intervention is reserved for the escalation gates below or an unrecoverable blocker.

On 2026-09-04 the user explicitly authorized proceeding with the gated database migration/reset/reseed track and the bounded Procurement, Inventory, Sales and HR/Payroll AccountingBookCode V2 producer cutover. Disposable rehearsal databases and owner-scoped cross-module conversions are therefore in scope. The configured `RHEMAERP` database remains protected until rehearsal, preflight, backup/restore and independent-review gates pass. Destructive repository cleanup, risky conflict resolution, pushes and PR operations remain escalation gates.

## Phase 1A review checklist

- [x] Handoff supplies exact base, branch, worktree, ordered commits, changed files, tests, migration state, dependencies, and unresolved findings.
- [x] Work remains inside the authorized Finance-only Phase 1A boundary.
- [x] V1 external producer compatibility is preserved.
- [x] Accounting-book authority and V2 contracts match the approved Phase 0 decisions.
- [x] Central posting enforcement fails closed and exact reversal behavior is preserved or strengthened.
- [x] Startup SQL, deterministic seeding, migrations, and model snapshot are mutually consistent.
- [x] Relevant backend, contract, migration, and frontend tests pass or all baseline failures are evidenced.
- [x] No migration or database mutation was performed without authorization.
- [x] Integration conflicts and dependencies were checked before cherry-picking; no dirty-file path overlapped.

## Review cycles

| Cycle | Implementer HEAD | Result | Findings/corrections | Integration result |
|---|---|---|---|---|
| 1 | `f50f460612bd301e9082b85b5efd228f44a636a7` | `CHANGES_REQUIRED` | P1: exact-reversal validation bypass for ordinary V1/V2 requests; recurring-reversal test fake no longer compiles; migration snapshot has pending model changes; used classifications can be retired/demoted and invalidate live mappings. P2: account-book rowversion is not round-tripped, so stale web edits can overwrite assignments. Corrections and required tests were sent directly to the implementer. | Not integrated |
| 2 | `d282be9c3a39a35d1d24ca0c8c85823ce415233b` | `APPROVED` | All five findings closed. Independent verification: 68 focused tests, EF no-pending-model check and diff check passed; worktree clean. Primary tree matches the reviewed task tree for every task-owned file. | Fourteen commits cherry-picked without conflict as `8c78c3a6` through `5a6d55f3`. Primary isolated-output build passed with 0 errors and 78 focused tests passed. Migration remains unapplied. |

## Phase 2 review cycles

| Cycle | Implementer HEAD | Result | Findings/corrections | Integration result |
|---|---|---|---|---|
| 1 | `1d6521e4976c0e9334dd7290e8d5c47d715ca2f2` | `CHANGES_REQUIRED` | P1: deterministic v1-to-v2 seed upgrades are incomplete for existing tenants; dashboard aggregation is not bound to one authoritative book; classification mutation/audit and relationship checks are not atomic; singleton system-role cardinality is unenforced. P2: ad-hoc Chart of Accounts book/tenant selection is nondeterministic; mutation controls lack permission-aware UI gating; parent retirement readiness disagrees with backend lifecycle rules. Corrections and required regression gates were sent directly. | Not integrated |
| 2 | `62f1a138efaaef78b5bcf2f7e4a1e3776ffb518b` | `APPROVED` | All seven findings closed. Independent verification passed 58 backend tests, 9 frontend tests, targeted ESLint, EF no-pending-model and diff checks. The worktree was clean and no persistent database was mutated. | Six commits cherry-picked without conflict as `ff6e35b6`, `e8a31156`, `b11099a3`, `81426b0a`, `81f28b26`, and `8a026995`. Primary verification passed: test-project build with 0 errors, 137 focused backend tests, 9 frontend tests, targeted ESLint, EF no-pending-model, and diff check. Migration remains unapplied. |

## Phase 3 review cycles

| Cycle | Implementer HEAD | Result | Findings/corrections | Integration result |
|---|---|---|---|---|
| 1 | `ab53f09e503efd6fee9205129f841b1f5de4bce2` | `CHANGES_REQUIRED` | P2: V2 JSON imports accept classification ID without the required portable stable code; UI view access treats action permissions as substitutes for the API's Finance.Read prerequisite; publication fingerprints omit the frozen book name and per-row book ID/code. Corrections and targeted regression requirements were sent directly. The unrelated Supplier Returns lockdown baseline was independently confirmed unchanged. | Not integrated |
| 2 | `534fa89696bed17cfe4e6f456b02df760ff98ed7` | `APPROVED` | All three findings closed. Independent verification passed 24 backend and 15 frontend tests, targeted ESLint, EF no-pending-model and diff checks. V2 imports require portable stable classification codes; UI/API read authority is aligned; publication fingerprint v2 covers all frozen book evidence and fails closed on tampering. | Four commits cherry-picked without conflict as `b4af3e4d`, `657cc5d4`, `b6dd0611`, and `48610595`. Primary task-owned blobs match the reviewed tree. Primary verification passed: test-project build with 0 errors, 24 focused backend tests, 15 frontend tests, targeted ESLint, EF no-pending-model, and diff check. Migration remains unapplied. |

## Phase 4 review cycles

| Cycle | Implementer HEAD | Result | Findings/corrections | Integration result |
|---|---|---|---|---|
| 1 | `1716ba43f18758685ef4b56c3796486e172db4f6` | `CHANGES_REQUIRED` | P1: posting can omit the preview fingerprint and its domain omits governance-warning and gain/loss-account evidence. P1: an ancillary failure after central posting can downgrade an already-committed batch to failed and make retry unrecoverable. P2: revaluation UI lacks Finance.Read/Run permission parity and masks history errors as empty. P2: account policy UI performs two independent writes concurrently and can partially save while reporting a combined failure. Independent checks passed 170 focused backend and 5 frontend tests, targeted ESLint, EF no-pending-model and diff checks; 38 broader failures were confirmed as pre-existing Phase 1A fixture debt. Corrections and fault-injection/UI regression gates were sent directly. | Not integrated |
| 2 | `d385b7f04fded7f5fc9cbd7768b006ef43a1845f` | `CHANGES_REQUIRED` | The original four findings are substantively closed, but one P1 remains: ancillary finalization after a committed central posting is not idempotent. A retry after partial rate-use/audit persistence can increment rate usage twice and duplicate audit events before reaching Posted. Independent verification passed 176 backend and 16 frontend tests, ESLint, EF no-pending-model and diff checks. A narrow concurrency-safe correction and fault injection after each ancillary stage were sent directly. | Not integrated |
| 3 | `1178347853a8120836f606a3ce270069024cb7e6` | `APPROVED` | Narrow correction adds durable exactly-once rate-use and audit finalization, amends the unapplied Phase 4 migration/model metadata, and adds fault-injection/idempotency coverage. Independent verification confirmed exact recovery identity, idempotent rate-use/audit evidence, staged fault recovery, EF/model alignment, and a clean worktree. | Seven commits cherry-picked without conflict as `89db61a1`, `21acfbbe`, `5d9ce6aa`, `08f171f2`, `280104a8`, `70e4defc`, and `dda86fe0`. All 49 task-owned blobs match the reviewed tree. Primary verification passed: test-project build with 0 errors, 197 focused Phase 4 tests, 16 frontend tests, targeted ESLint, EF no-pending-model, and diff check. Three additional FinanceAuditFoundation tests fail on the documented unrelated budget-control fixture baseline. Migration remains unapplied. |

## Phase 5 review cycles

| Cycle | Implementer HEAD | Result | Findings/corrections | Integration result |
|---|---|---|---|---|
| 1 | `8b45c555dcd88cb47cb4c4b3c9d26c4c421b7f9e` | `CHANGES_REQUIRED` | P1: bulk combinations bypass full Alpha/Alphanumeric and normalization validation; Freeze checks only segment IDs rather than complete persisted identity readiness; cross-tenant persisted assignments lack sufficient migration/schema/runtime protection. P2: account update validates canonical identity but persists raw client rows; delete/reorder lack stale-write tokens and the reorder API text contradicts the actual lifecycle. Independent checks passed 12 backend and 8 frontend tests, ESLint, EF no-pending-model and diff checks. All five corrections and regression gates were sent directly. | Not integrated |
| 2 | `45fa499901f35821c655b82966dc86a6d2bf260b` | `CHANGES_REQUIRED` | The original five findings are closed, but one P2 regression remains: account update persists normalized rows, then maps the stale tracked navigation and can return soft-deleted raw rows instead of the new canonical identity. Independent gates passed 29 backend and 8 frontend tests, ESLint, EF no-pending-model and diff checks. A narrow response-reload/synchronization fix and direct returned-DTO assertion were sent to the implementer. | Not integrated |
| 3 | `4d2891e676a82c912c54c61abcf488379c70f244` | `APPROVED` | Narrow correction `4d2891e6` returns only the canonical active account identity after update and adds direct returned-DTO regression coverage. Independent review reconfirmed all earlier closures and passed 29 backend tests, build, EF no-pending-model and diff checks; prior 8 frontend tests and ESLint remained valid. | Five commits cherry-picked without conflict as `58b43652`, `fcb84743`, `28763284`, `f2c06f4e`, and `1f273978`. Task-owned files match the reviewed tree; only the coordination ledger differs. Primary verification passed: build with 0 errors, 29 focused backend tests, 8 frontend tests, targeted ESLint, EF no-pending-model, and diff check. Migration remains unapplied. |

## Phase 6 review cycles

| Cycle | Implementer HEAD | Result | Findings/corrections | Integration result |
|---|---|---|---|---|
| 1 | `a9a149c8c09056e03e3c3eb8ebac824e87b75832` | `CHANGES_REQUIRED` | P1: returned dimension snapshot/set/item evidence is not fully tenant-validated; exact-book/Posted authority is checked only on the transaction row rather than consistently against journal and posting-event evidence. P2: a current response carrying the wrong book code leaves the UI blank instead of showing an integrity error with Retry. Description-only line copying passed review. Focused backend 8/8 and frontend 10/10 tests passed; diff and worktree were clean. All three corrections and regression gates were sent directly. | Not integrated |
| 2 | `0cbd203c7fd9269632fabe6fe90bfa701ed55595` | `APPROVED` | All three findings are closed: dimension evidence is tenant-validated and fails closed; transaction, journal, event and selected-book evidence must agree on tenant/book/Posted state; stale UI responses are suppressed while a current wrong-book response shows an integrity error with Retry. Independent verification passed 20 backend and 11 frontend tests, ESLint and diff checks; the worktree was clean and no schema/cross-module/posting behavior changed. | Four commits cherry-picked without conflict as `de8d026e`, `7875be0b`, `73543118`, and `744b9f48`. Task-owned files match the reviewed tree; only the coordination ledger differs. Primary verification passed: build with 0 errors, 20 focused backend tests, 11 frontend tests, targeted ESLint, and diff check. No migration was introduced. |

## Post-Phase-6 Stage A rehearsal

Stage A repaired focused EF discovery from 444 to the authoritative 448 migrations and added a guarded,
prefix-locked rehearsal harness plus operator diagnostics. The configured `RhemaERP` database was inspected
read-only. Its five pending Finance migrations were identified in order, together with one live FX batch,
nine active currency links, absent classification tables and three historical published/retired layouts.

The empty rehearsal proved the exact-base chain cannot currently migrate from the consolidated baseline:
`20260317115118_AddCrmEntities` attempts to rename `QuoteLineItem` to the already-existing `QuoteLineItems`.
A safe `COPY_ONLY` clone proved the configured-database upgrade is independently blocked in Phase 2 because
SQL Server rejects the singleton-role filtered-index predicate containing `NOT IN`. No historical migration
was weakened or manually stamped past either failure. Every disposable database was explicitly dropped;
none remains.

The exact V1 inventory records five active request paths: two Procurement, one Inventory and two Sales
(including the reversal path). HR/Payroll has no V1 posting constructor, but it and Procurement each retain
a direct Finance `Account` writer. Owner-scoped Phase B packets are documented in
`docs/Finance/GL_CUTOVER_STAGE_A_READINESS.md`.

## Connectivity checkpoint

Last verified durable point: Stage A is ready for independent review. The configured development database
was read only; all disposable rehearsal targets were dropped. Migration-chain blockers prevent reset/reseed
approval and Phase B must not start until the reviewed migration corrections pass both rehearsals.

## Next action

Independently review the harness, discovery metadata, diagnostics, inventories and owner packets. Then assign
the historical baseline collision, `apply-migrations` DI defect and invalid Phase 2 filtered-index predicate
to their owners. Re-run empty and clone rehearsals through double seeding and invariant checks before
authorizing the Phase B producer stacks or any developer-database reset.
