# GL Classification and Revaluation Refactor — Execution Ledger

Last reconciled: 2026-09-05 (Africa/Accra)

## Objective

Execute the approved GL classification and revaluation refactor in bounded phases, preserving Finance ownership and V1 external-producer compatibility until a separately coordinated cutover.

## Current package

| Field | Value |
|---|---|
| Phase | Post-Phase-6 Stage B1 — Procurement owner V2/provisioning conversion |
| Status | `IN_PROGRESS` |
| Implementing task | `Assess GL configuration changes` (`01a0648a-99d2-7320-aced-b2b22d160334`) |
| Exact base | `0f88eff289ba563cdd0707166df6b3e10fe585af` |
| Branch | `codex/procurement-finance-v2-cutover` |
| Worktree | `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP-procurement-finance-v2-cutover` |
| Primary branch | `codex/finance-budget-posting-evidence` |
| Primary HEAD at activation | `0f88eff289ba563cdd0707166df6b3e10fe585af` |
| Coordinator | Current primary Finance task |
| Recovery heartbeat | `finance-gl-cutover-coordinator` — active every 15 minutes |
| Model routing | Implementer: GPT-5.6 Terra Medium; independent contract review: GPT-5.6 Sol High |
| Review status | Stage A.1 approved and integrated; Stage B1 owner packet issued |
| Connectivity state | Available; configured `RHEMAERP` remains protected and read-only |

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

Independent review approved Stage A HEAD `1722866c55e2f7b94bd2391ccc3a89eb3a1c8442` as an accurate
readiness/blocker package. Safety refusals, 448-migration discovery, EF no-drift, V1 inventory and direct
account-writer inventory were verified. The three Stage A commits were integrated as `78b5d5e0`,
`fa385c0e` and `7bc24b22`. Approval explicitly does not authorize reset/reseed or Phase B until the CRM
table collision, Phase 2 filtered-index syntax and `apply-migrations` DI registration are corrected and
both guarded rehearsals reach their required terminal gates.

## Post-Phase-6 Stage A.1 rehearsal corrections

The authoritative Stage A.1 evidence-bearing candidate is
`0263b16acffce062ebc106bf6fe30e348431b7fe`. Its complete ancestry from exact base
`7bc24b22c0624aec9acae580ba8049d5f5a83425` is, without omission:

1. `0130ac4e02aa306010b09e59ef325bef8556131d`
2. `18a5c8a4187b8ab0c72d19ce105d1960c4af76e5`
3. `0fe40ac6f770beac5844c6d70773836ec9c187b9`
4. `f911931255dd8fc400c2e0d41fc388e529c5aab9`
5. `1fa2a86b082fc77964eef9a8523fb0d416ecb4ed`
6. `5404a1c8fa4c018a8f51a5fda2ad1124771fd00e`
7. `b85d88b19219e4f7c1bf3b0959057e07a03271ba`
8. `ba0e8bfa35dd6f54f31f3193003328b4127f19d3`
9. `dfe7d12bf6dacc5ab5007ece72300d0184014d1e`
10. `0263b16acffce062ebc106bf6fe30e348431b7fe`
11. `HEAD` — the metadata-only handoff correction that contains this list

This list must match `git rev-list --ancestry-path --reverse 7bc24b22c0624aec9acae580ba8049d5f5a83425..0263b16acffce062ebc106bf6fe30e348431b7fe`
exactly. Its LF-terminated SHA-256 is
`83C5CE3DD66525AB7BEAFC869343AB7A70116DEECB06F7A90B6C72209E59C6C4`, and the candidate tree is
`ce279a45ec09d59936a7475721b0a9cf397a7353`. For final-branch validation, resolve entry 11 with
`git rev-parse HEAD`, append it to the ten literal candidate hashes, and compare the resulting sequence to
`git rev-list --ancestry-path --reverse 7bc24b22c0624aec9acae580ba8049d5f5a83425..HEAD`. The handoff reports
the resolved hash. This `HEAD` sentinel is necessary because a Git commit cannot embed its own final hash.

Stage A.1 repaired the CRM clean-chain collision, SQL Server singleton-role filter, and application
`apply-migrations` DI registration. Continuing the guarded fresh rehearsal exposed and corrected a missing
vendor-invoice compatibility rename, two invalid AR compatibility batches, payroll accounts bypassing the
canonical segment-identity seed pass, and protected balance-sheet layouts using obsolete root codes.

`RHEMAERP_GL_REHEARSAL_EMPTY_A4` applied all 448 migrations from an empty database, ran the real seed path twice from the final committed code, and produced the
same canonical invariant hash on both passes. A separate SQL Server full-chain regression passed. The
checksum-verified COPY_ONLY clone `RHEMAERP_GL_REHEARSAL_CLONE_A1` applied through Phase 3, then stopped at
the intended Phase 4 guard because its one historical FX batch has no truthful immutable policy evidence.
The configured source fingerprint remained unchanged. Both databases and the temporary backup were removed;
zero rehearsal-prefixed databases remain.

Independent review then found that CRM downgrade did not restore the plural predecessor graph and the
Projects migration did not rename `VendorInvoice` back to `VendorInvoices`. On 2026-09-05 the user
explicitly authorized the narrowly named CRM/Projects historical-migration correction and the minimum
shared Quantity Survey chain-test safety contract. The correction preserves rows in both directions,
fails before destructive mutation for incomplete/populated CRM conflicts, restores each exact immediate
predecessor, and moves compatibility-specific SQL Server assertions into Finance/data-owned tests.

Post-review rehearsal `RHEMAERP_GL_REHEARSAL_EMPTY_A5` then completed all 448 migrations, exercised the
repaired application `apply-migrations` command, and ran two real seed passes with identical canonical
Finance SHA-256 `F51CEBF3ABCFD1C92BB64ACB8FCF9B2740D90D0AFB322C275A8363A3729A5549`.
The checksum-verified COPY_ONLY clone `RHEMAERP_GL_REHEARSAL_CLONE_A2` again applied through Phase 3 and
stopped at the intended Phase 4 historical-FX-evidence guard. The source fingerprint remained
`446|20260902140000_AddFixedAssetDepreciationConventionEvidence|1|9|28`; both disposable databases and
the exact backup were removed, and the server reported zero rehearsal-prefixed databases.

## Connectivity checkpoint

Last verified durable point: Stage A.1 corrections pass the guarded fresh rehearsal and preserve the
expected Phase 4 clone hard stop. The configured development database was read only; all disposable
rehearsal targets and backup artifacts were dropped. Phase B must not start before independent review and
coordinator integration.

Stage A.1 candidate `f911931255dd8fc400c2e0d41fc388e529c5aab9` repaired the forward chain sufficiently for
an empty 448-migration/two-pass seed rehearsal, while the representative clone reached the deliberate
Phase 4 historical FX-evidence stop. Independent review rejected integration because the CRM and Projects
historical changes do not restore predecessor schema on downgrade, populated-data safety coverage is
incomplete, the readiness document retains a superseded `apply-migrations` diagnosis, and CRM/Projects/QS
files fall outside the previously named Procurement/Inventory/Sales/HR cross-module authorization.
`RHEMAERP` remained read only and all disposable databases were removed. The implementer is paused at the
clean candidate pending a user decision on those named module-owned migration corrections.

On 2026-09-05 the user explicitly authorized the narrowly identified CRM and Projects historical migration
repairs and the related Quantity Survey/shared full-chain test. The authorization requires owner-facing code
comments explaining the historical conflict, forward/data-preservation behavior, exact downgrade contract,
and the need to retain the compatibility logic until the full migration-chain gates are rerun. It does not
authorize broader CRM, Projects or Quantity Survey feature changes.

On 2026-09-05 the user authorized automatic risk-based model routing for all subsequent coordinator
dispatches. The current Stage A.1 historical migration/rehearsal correction remains on GPT-5.6 Sol Medium,
and high-risk independent review remains on GPT-5.6 Sol High. After Stage A, routine bounded UI,
documentation, test-maintenance, adapter and owner-scoped producer-conversion work should use GPT-5.6 Terra
Medium. Finance accounting logic, migrations, posting/reversal behavior, concurrency, security, schema,
database rehearsal/cutover and substantive correction cycles use GPT-5.6 Sol Medium. Escalate implementation
temporarily to Sol High only for an unresolved material defect or unusually difficult accounting/architecture
decision. Apply switches only between completed turns after reconciling the task checkpoint; record each
exception or escalation here.

Independent Sol High re-review of Stage A.1 candidate `0263b16a` found the migration, downgrade,
owner-comment, guarded-rehearsal, sanitized-evidence and documentation corrections technically sound. One
P1 integration-control correction remains: the durable handoff/evidence stops at `b85d88b1` and omits the
three later commits required to reproduce final candidate `0263b16a`. The coordinator dispatched this
narrow ancestry/final-tree evidence correction to the implementer on GPT-5.6 Sol Medium. No production or
database behavior needs to change for this cycle, and `RHEMAERP` remains protected/read-only.

Stage A.1 review cycle 2 examined clean candidate `5404a1c8` using an independent GPT-5.6 Sol High reviewer
and returned `CHANGES_REQUIRED`. The code-level migration corrections, 448-migration discovery, EF model
alignment and focused tests passed, but the declared integration list omitted ancestor commit `0130ac4e`;
the sanitized A5/A2 SQL Server rehearsal evidence was not retained for independent audit; and the readiness
guide still contradicted the actual `apply-migrations`/clone procedure. The coordinator dispatched these
corrections directly to the implementer using GPT-5.6 Sol Medium. Integration, Phase B and any mutation of
configured `RHEMAERP` remain blocked pending a clean corrected handoff and satisfactory re-review.

Stage A.1 final candidate `286f2a294d9f14bd31c7f01a3447dd682f666a89` passed independent GPT-5.6
Sol High review. The complete eleven-commit candidate was integrated locally as `85489e04`, `9cbc68a2`,
`2118aa13`, `1c62406f`, `44acbbab`, `a5a666cc`, `985afe21`, `dd8b8fb9`, `a0ff5dc4`, `70149939`, and
`59025ea9`. Fresh-checkout validation exposed that the retained evidence manifest described literal CRLF
bytes while the original Git blobs were LF. Integration corrections `ceefa127` and `0f88eff2` stored the
reviewed bytes exactly and declared path-scoped `whitespace=cr-at-eol`; independent review then approved the
integrated state. Evidence validation and plain range `git diff --check` pass, all 448 migrations are
discoverable, EF reports no pending model changes, the test project builds with zero errors, and the focused
migration suite reports 6 passed/5 environment-gated SQL tests skipped. The retained guarded SQL Server
evidence records 11/11 full-chain tests passed. No configured `RHEMAERP` mutation occurred and no disposable
rehearsal database or backup remains.

## Next action

Execute the bounded Procurement owner packet from exact base `0f88eff2`: convert the two active posting
producers to V2 with explicit governed `AccountingBookCode`, replace the executable supplier-onboarding
seeder's direct Finance `Account` writes with `IFinanceAccountProvisioningService`, add the required
owner-facing comments and denial/idempotency/canonical-identity tests, and return a clean structured handoff.
Do not change Procurement economics or unrelated Procurement behavior. Independently review the packet before
local integration, then issue the Inventory packet. Keep configured `RHEMAERP` read-only until the later
reset/migration gate is separately reconciled.
