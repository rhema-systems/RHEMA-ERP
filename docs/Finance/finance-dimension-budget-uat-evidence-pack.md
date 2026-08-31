# Finance dimension-budget UAT and evidence pack

## Purpose and release boundary

This pack certifies two separately gated Finance releases:

- **Phase A (merged):** the canonical transaction-dimension budget grain from PR `#114` and the
  dimension-aware Finance budget worksheet from PR `#115`;
- **Phase B (merged; live evidence still required):** direct Accounts Payable expense reservation
  and consumption, originally reviewed in PR `#116` and promoted to `master` by PR `#120`.

Phase A may be executed after the controlled deployment has applied migration
`20260825235055_AddFinanceBudgetControlDimensions` and rebuilt the API and frontend from a commit
containing PRs `#114` and `#115`. PR `#120` satisfies the Phase B source gate, and migration
`20260826013000_AddApVendorInvoiceBudgetEvidence` was applied on 30 August 2026. The matching safe
API runtime is available; authenticated Phase B business evidence is still required. Do not use
production data for the first execution.

The pack does not certify Procurement commitments, employee expenses, supplier returns, standalone
supplier debit notes, or AP correction/reversal budget evidence. Those remain separate lifecycle
contracts.

Primary implementation anchors:

- `frontend/src/app/finance/budgeting/scenarios/page.tsx` — scenario control-dimension selection;
- `frontend/src/app/finance/budgeting/returns/[id]/page.tsx` — dimension-combination worksheets;
- `frontend/src/lib/finance/budget-dimension-grid.ts` — stable combination and effective-date rules;
- `frontend/src/app/finance/ap/invoices/create/page.tsx` — AP BudgetEntry selection;
- `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs` — AP reservation lifecycle;
- `src/ErpSystem.Api/Services/Finance/Budget/FinanceBudgetCommitmentService.cs` — authoritative
  position, reservation, release, and consumption;
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs` — transaction-bound consumption.

## Required actors and evidence

Use distinct authenticated users where the configured workflow requires maker/checker separation.
Record usernames or user IDs, but never passwords or tokens.

| Actor | Minimum capability | Expected action |
| --- | --- | --- |
| Budget preparer | `Finance.Budgeting.Write` | Create the scenario, return, combinations, and cells |
| Budget reviewer/adopter | Tenant-configured Budget workflow permissions | Approve returns and adopt the scenario |
| AP preparer | AP invoice create/edit and workflow-submit permissions | Create and submit the direct expense invoice |
| AP reviewer/poster | Tenant-configured AP approval and posting permissions | Approve and post the invoice |
| Auditor | Finance read/report access | Capture read-only position, journal, and audit evidence |

Retain the following evidence for every case:

1. tenant code and actor identity;
2. scenario, return, budget-entry, invoice, reservation, posting-event, and journal IDs where applicable;
3. before/after screenshots of the relevant Finance UI;
4. API response or controlled database read showing lifecycle status and amounts;
5. the exact account, fiscal period, dimension combination, functional currency, and exchange-rate ID;
6. a pass/fail result and any controlled error code/message.

## Controlled fixture

Use one open fiscal period and one active, direct-posting expense account with
`BudgetTrackingEnabled = true`. Configure two active analytical dimensions, for example:

- `DEPT`: `FIN` and `OPS`;
- `PROJECT`: `P100` and `P200`.

The values selected for the positive case must be effective for the whole fiscal period. Prepare an
adopted budget cell for `DEPT=FIN + PROJECT=P100` with a functional-currency amount of `GHS 10,000`.
Do not reuse this cell for unrelated test cases unless the expected available balance is adjusted.

## Deployment gates

| Gate | Required evidence | Permitted execution |
| --- | --- | --- |
| Phase A source | PRs `#114` and `#115` are ancestors of the deployed commit | Worksheet cases `UAT-BUD-001` through `UAT-BUD-005` |
| Phase A schema | Migration history contains `20260825235055_AddFinanceBudgetControlDimensions` and the physical schema matches it | Worksheet persistence and reload |
| Phase B source | PR `#120` is an ancestor of the deployed commit | AP cases `UAT-APB-001` through `UAT-APB-008` |
| Phase B schema | Migration history contains `20260826013000_AddApVendorInvoiceBudgetEvidence` and `VendorInvoiceLineItem.BudgetEntryId` has its governed FK/indexes | AP selection, reservation, and posting |
| Runtime | API and frontend build identifiers match the recorded deployed commit | Any sign-off assertion |

Stop at the first failed gate. Never stamp migration history, edit budget evidence, or substitute a
different tenant/account/dimension combination to make the pack pass.

## Pre-UAT automated baseline — 27 August 2026

This preparation run used merged `master` commit `eacaf677` (PRs `#114` and `#115`) and made no
database or business-document changes.

| Verification | Result |
| --- | --- |
| `BudgetServiceHardeningTests` | Pass — 13/13 |
| `FinanceDimensionAdministrationServiceTests` | Pass — 8/8 |
| `FinanceBudgetCommitmentServiceTests` | Pass — 11/11 |
| `budget-dimension-grid.test.ts` | Pass — 6/6 |
| Phase A source gate | Pass — merged source present |
| Phase A schema/runtime gate | Pass — the three reviewed migrations are applied, physical schema checks passed, and the rebuilt safe API is healthy |
| Phase B source gate | Hold — PR `#120` is not yet in `master` |

Automated regression success is a prerequisite, not business UAT sign-off. The cases below still
require authenticated UI/API execution and retained evidence in the designated non-production
tenant.

The pre-deployment migration inventory for the configured environment was:

1. `20260825141500_AddFinanceControlledDocumentRetention` — pending;
2. `20260825190000_AddFinanceDimensionRuleScope` — pending;
3. `20260825235055_AddFinanceBudgetControlDimensions` — pending.

The controlled deployment described below applied that complete sequence. EF now reports zero
pending migrations, so Phase A schema/runtime execution is permitted. No seed routine or business
document creation was part of the deployment.

### Phase A migration deployment review — 27 August 2026

The bounded idempotent SQL was generated from
`20260824223000_GrantFinanceReportExportToChiefAccountant` through
`20260825235055_AddFinanceBudgetControlDimensions` with:

```powershell
dotnet ef migrations script `
  20260824223000_GrantFinanceReportExportToChiefAccountant `
  20260825235055_AddFinanceBudgetControlDimensions `
  --project src/ErpSystem.Data/ErpSystem.Data.csproj `
  --startup-project src/ErpSystem.Api/ErpSystem.Api.csproj `
  --context ApplicationDbContext `
  --no-build `
  --idempotent
```

The reviewed artifact contains 283 lines and has SHA-256
`FD508C00542971A26C0D82BA40BD34B9BFC4E864BE0A95F320A67967A96B9868`. Regenerate and require the
same hash from the reviewed source/build before deployment; do not copy an untracked local artifact
between environments.

| Migration | Reviewed forward operations | Existing-row effect |
| --- | --- | --- |
| `20260825141500_AddFinanceControlledDocumentRetention` | Add four nullable retention columns and an all-null-or-complete retained-artifact check constraint | Existing hash-only issue rows satisfy the all-null branch; no data rewrite |
| `20260825190000_AddFinanceDimensionRuleScope` | Replace the legacy three-column rule index; add nullable source module, document type, and posting action; create the scoped unique index | Existing rules retain null scope and are not rewritten |
| `20260825235055_AddFinanceBudgetControlDimensions` | Replace the BudgetEntry uniqueness index; add nullable dimension-set evidence to entries/reservations; create scenario-control table, indexes, and restrictive foreign keys | Existing legacy entries and reservations remain null-grain records; no backfill |

The script contains three ordered `BEGIN TRANSACTION`/`COMMIT` units and records each migration only
after its DDL succeeds. It contains no `UPDATE`, `DELETE`, seed operation, trigger replacement,
forward table drop, or forward column drop. The two index replacements are transactional with their
successor indexes.

When applying this artifact with SQLCMD, use `-I` so the session has `QUOTED_IDENTIFIER ON`; SQL
Server requires that setting for the filtered unique indexes. The first deployment invocation
omitted `-I`: migration 1 committed, migration 2 stopped at its new filtered index, and migration 2's
transaction rolled back completely. Verification proved the legacy rule index remained, no new
scope columns remained, and no budget-dimension schema had been created. The unchanged idempotent
artifact was then rerun with `-I`; it skipped migration 1 and completed migrations 2 and 3.

Before applying it, stop or quiesce the API, take the environment's normal recoverable database
backup/restore point, and require all of the following:

1. SQL Server is the configured provider and the resolved database is exactly the intended
   non-production UAT database;
2. the last applied migration is exactly
   `20260824223000_GrantFinanceReportExportToChiefAccountant`;
3. the pending set is exactly the three migrations listed above, in that order;
4. the deployed binaries and model snapshot come from the reviewed commit containing PRs `#114`
   and `#115`;
5. no competing migration or application startup initializer is running.

Abort without stamping history or manually repairing schema if the pending set, artifact hash,
database identity, provider, DDL preconditions, or post-deployment physical schema differs. After
application, require zero pending migrations, rebuild/restart the API and frontend from the recorded
commit, and run the automated baseline again before starting Phase A UAT.

### Phase A deployment execution record — 27 August 2026

| Evidence | Result |
| --- | --- |
| Target | SQL Server `RHEMA-AKWASI\\EXPRESS22`, database `RhemaERP` |
| Pre-deployment backup | `RhemaERP_PhaseA_Pre_20260827-101757.bak`, `COPY_ONLY`, checksum enabled |
| Backup verification | `RESTORE VERIFYONLY ... WITH CHECKSUM` passed |
| Backup SHA-256 | `F1AD85E3921A9EB76356A15BE35EE39B3F58DA508643D0DE3065469531FA38B7` |
| Applied migration head | `20260825235055_AddFinanceBudgetControlDimensions` |
| EF pending migrations after deployment | `0` |
| Physical schema | Retention columns/constraint, scoped rule columns/index, budget dimension columns/table/indexes/FKs all present |
| Constraint check | `DBCC CHECKCONSTRAINTS` passed for existing controlled-document issue rows |
| Backend baseline after deployment | Pass — 32/32 |
| Frontend dimension-grid baseline | Pass — 6/6 |
| Safe API runtime | Zero product workers; `RhemaERP`; startup initialization skipped; loopback health HTTP 200 |
| Frontend runtime | Clean reviewed UAT worktree on port 3000; `/login` HTTP 200 |

### Phase A authenticated execution checkpoint — 27 August 2026

Tenant `FINANCE-DEMO` was exercised through the Finance UI with the `admin` actor. The controlled
fixture uses FY2026 and September 2026 because the four lookup values are effective from
27 August 2026; January through August must therefore remain unavailable for this full-period
budget grain.

| Evidence | Recorded value |
| --- | --- |
| Scenario | `UAT Dimension Budget FY2026` — `e0ec8670-cee0-4cdf-9a73-aa5e6d7918b7`, `Collecting` |
| Return | `d65d9f6d-cfdf-4981-b750-32d87bf3457d`, Finance & Administration, `Draft`, initially unassigned |
| DEPT definition | `8bc6174e-6697-48de-ad44-37b6df7a6cb9` |
| PROJECT definition | `4dfcf296-f941-4577-bc67-65a69a482487` |
| Values | `FIN`, `OPS`, `P100`, and `P200`; active from `2026-08-27` |
| Controlled account | `100-6000-0000` — Salaries - Finance & Administration |
| Controlled period | `2026-09` — `b50f7d32-dee1-4d52-96a6-08ac64ed75b4` |
| FIN/P100 entry | `41f00c0f-e1e0-421a-a4bb-deb78fecaf7f`; GHS 10,000; set `4d912757-dd92-f1c4-fab3-abe3a3847ad9`; hash `B7CF81D4EE75B0D4C1846A3C29B177919F8FCE61267DC3668A0DBB566750CFAC` |
| OPS/P200 entry | `82682379-d3bf-46e8-bda2-703c79b49db5`; GHS 4,000; set `979bab42-46f7-908e-fa58-2adae0555a8d`; hash `29146971290363FC6899DC7D1897C4FA06431B5395014C5EEC569D740B72608C` |
| Checkpoint database state | 2 active entries; GHS 14,000 total; return `Draft`; submitted/approved dates null |

| Case | Result | Evidence summary |
| --- | --- | --- |
| `UAT-BUD-001` | Pass | The scenario reopened with exact `DEPT` then `PROJECT` controls and one Finance return. |
| `UAT-BUD-002` | Pass | A DEPT-only combination produced `Select one value for every budget-control dimension`; January-August cells were disabled for the complete values while September-December were enabled. |
| `UAT-BUD-003` | Pass | The two amounts were saved, reloaded, and remained isolated under their exact immutable dimension sets and hashes. |
| `UAT-BUD-004` | Pass | Existing scenario `Test Budget` (`17455956-749a-40c8-ad5d-47df6cd86a88`) and return `61f68e83-768d-4181-becc-d98e40666aa7` rendered all 12 periods with `Budget grain: Account and period (legacy)` and no invented control selection. |
| `UAT-BUD-005` | Pass | The return and scenario completed their separate approval workflows, the scenario was adopted as the official FY2026 budget, and the exact FIN/P100 and OPS/P200 entries became eligible for budget control. |
| `UAT-BUD-006` | Pass | Posted journal `JE-2026-000006` proved future-date policy, OPS/P200 matching, an approved GHS 1,500 override shortfall, separate journal approval, and immutable posting evidence. |

Two defects were discovered and contained during this execution:

1. creating a top-level lookup value compared null parent and null current IDs as if the new value
   were its own parent. The guard now runs only when both IDs exist; the focused Finance dimension
   administration suite passes 9/9;
2. the worksheet expected fiscal-year summary DTOs to embed periods, producing an Account/Total-only
   grid. It now calls the dedicated tenant-scoped fiscal-period endpoint, sorts the returned periods,
   and labels legacy grain explicitly. The focused worksheet and dimension-grid suites pass 7/7.

These fixes are local to the controlled evidence branch at this checkpoint. Do not claim deployed
or merged status until their focused change set has completed the normal review path.

## Phase A — worksheet and immutable budget grain

### UAT-BUD-001: create a dimension-controlled scenario

1. Open `/finance/budgeting/scenarios`.
2. Create a scenario for the controlled fiscal year.
3. Select `DEPT` and `PROJECT` as budget-control dimensions.
4. Save and reopen the scenario.

Expected:

- the scenario displays both dimensions in configured display order;
- derived or inactive dimensions are not selectable;
- the persisted scenario returns the exact two immutable control-dimension IDs.

Evidence: scenario ID, screenshot of the selected grain, and scenario API response.

### UAT-BUD-002: reject incomplete and ineffective combinations

1. Open the assigned return at `/finance/budgeting/returns/{returnId}`.
2. Attempt to add a combination with only `DEPT=FIN`.
3. Attempt a complete combination containing an inactive value or one not effective for the full
   fiscal period.

Expected:

- the incomplete combination is blocked with “Select one value for every budget-control dimension”;
- an ineffective combination cannot be used to save an affected period cell;
- no incomplete budget entry is persisted.

Evidence: validation message and a read proving zero incomplete entries.

### UAT-BUD-003: save and reload independent dimensional cells

1. Add `DEPT=FIN + PROJECT=P100` and enter `GHS 10,000` for the controlled expense account/period.
2. Add `DEPT=OPS + PROJECT=P200` and enter a different amount for the same account/period.
3. Save, reload, and switch between both combinations.

Expected:

- each combination retains its own account/period amount and row version;
- selecting the same values in a different order selects the existing combination instead of
  creating a duplicate;
- readable labels show both dimension codes and values.

Evidence: both BudgetEntry IDs, combination hashes, assignments, and reload screenshots.

### UAT-BUD-004: preserve the legacy budget grain

Open a pre-existing scenario with no control dimensions.

Expected:

- the worksheet remains one account/fiscal-period grid;
- the UI labels it `Account and period (legacy)`;
- no dimension assignment is invented for an existing BudgetEntry.

### UAT-BUD-005: submit, approve, and adopt

Submit and approve the controlled return, then adopt the scenario using the configured workflow.

Precondition and actor handoff:

1. From the scenario page, assign the controlled return to the intended preparer. An unassigned
   return must not expose submission as an available action.
2. The assigned preparer submits the return through the normal return workflow.
3. A different authorized reviewer approves it; the preparer must not approve their own return.
4. An authorized budget controller adopts the approved scenario.

Expected:

- only an active, approved return in the adopted and unlocked scenario becomes eligible for budget
  control;
- the scenario’s control-dimension policy cannot be silently changed after entries exist;
- audit history identifies the actors and lifecycle timestamps.

Recorded result — 27 to 30 August 2026:

- the Finance return completed its configured three-stage approval workflow;
- the scenario completed its separate approval workflow and was adopted as the official FY2026
  scenario;
- the approved FIN/P100 and OPS/P200 entries remained isolated by their immutable dimension-set
  hashes;
- no database bypass or direct workflow-state mutation was used.

### UAT-BUD-006: future-period journal, budget override, approval, and posting evidence

Use an open future fiscal period whose posting-date policy explicitly permits future dates. Create a
balanced manual journal against the adopted OPS/P200 budget cell:

- date: `15 September 2026`;
- reference: `UAT-BUD-FUTURE-202609`;
- debit: `100-6000-0000` Salaries - Finance & Administration, `GHS 500`;
- credit: `000-2100-0000` Accrued Expenses, `GHS 500`;
- dimensions on both lines: `DEPT=OPS + PROJECT=P200`.

Expected:

- the draft cannot use the future date while the period policy has `AllowFutureDating = false`;
- after an authorized policy change, the same date is accepted without reopening or replacing the
  period;
- the budget engine matches the exact September OPS/P200 entry rather than another dimensional
  cell;
- an insufficient position blocks journal submission until a governed override is approved;
- override approval does not approve the journal: the journal completes its own maker-checker
  workflow before posting;
- posting freezes the budget position and override evidence shown on the journal.

Recorded evidence — 30 August 2026:

| Evidence | Recorded value |
| --- | --- |
| Journal | `JE-2026-000006` — `fea375a5-2a4b-4d6f-8684-74cbfd0d58fe` |
| Final state | `Posted`; entry date `15 September 2026`; posted `30 August 2026` |
| Lines | GHS 500 debit Salaries / GHS 500 credit Accrued Expenses; both `DEPT=OPS + PROJECT=P200` |
| Budget entry | `82682379-d3bf-46e8-bda2-703c79b49db5`; FY2026 September OPS/P200 |
| Frozen position | Budget GHS 4,000; posted before entry GHS 5,000; available GHS -1,000; request GHS 500; shortfall GHS 1,500 |
| Override | Approved through the configured workflow; journal displays `Approved budget override applies` |
| Journal authority | Submitted by `admin`; final approval by `finance.demo.controller` |
| Posting | Posted by `admin` at `30 August 2026 06:37`; immutable budget evidence remains visible |

This case belongs after the dimensional-budget adoption case and before direct AP consumption UAT.
It proves the manual-journal control boundary and the distinction between override approval, journal
approval, and posting.

## Journal approval-withdrawal follow-up

The hardened withdrawal implementation and migration
`20260830143000_AddJournalApprovalWithdrawalMetadata` were deployed to the controlled local UAT
runtime on 30 August 2026. Execute the complete checklist before final sign-off; the first
withdrawal cycle below records the functional checkpoint only.

### UAT-JRW-001: withdraw and safely resubmit a pending journal

1. Create a disposable balanced journal against a budget-tracked expense and submit it for approval.
2. Record the active workflow, pending approval and active budget-reservation identifiers.
3. As the authorized maker, withdraw the approval request with a non-empty reason.
4. Confirm the journal returns to its documented editable state, the workflow is cancelled, pending
   approvals are no longer actionable, and the reservation is released exactly once.
5. Confirm an approve-only reviewer and an unrelated writer cannot withdraw another maker's request.
6. Resubmit the journal and prove exactly one new active workflow and one correct reservation exist.
7. Complete approval and posting, then prove withdrawal is no longer offered for the posted journal.

Required evidence:

- before/after journal, workflow, approval and reservation IDs and statuses;
- maker, reviewer and attempted unauthorized actor identities;
- withdrawal reason, audit event and approver notification evidence;
- retry evidence proving no duplicate cancellation, reservation release or workflow;
- a controlled failure test proving workflow, journal and budget state cannot commit partially.

Recorded functional checkpoint — 30 August 2026:

| Evidence | Recorded value |
| --- | --- |
| Journal | `JE-2026-000007` — `c327e08b-a13c-453b-8054-fc08efea0e45` |
| Actor and reason | `finance.demo.controller`; `UAT verification of journal approval withdrawal controls.` |
| Journal result | `Draft` / approval `Withdrawn`; dedicated withdrawn-by/date/reason populated; rejection reason remains null |
| Workflow result | Instance `e09be5d8-8479-4ec6-9162-91b14a5f50bc` cancelled; 2/2 active steps cancelled; 2/2 pending approvals expired |
| Budget result | GHS 500 `ManualJournalEntry` reservation changed to `Released` once with matching reason |
| Notifications | Withdrawal notifications sent to `finance.demo.accounts` and `finance.demo.senior` |
| UI result | Workflow assignment and Withdraw action removed; Edit, Resubmit and Delete restored; immutable audit event displayed |
| Resubmission | Journal returned to `Pending Approval`; withdrawal metadata cleared; exactly one new active workflow `3034402d-cf4f-4dd4-9fd4-31e68b127554` with 2 pending approvals |
| Reservation replay | Prior reservation `33090d16-1f44-4a44-966a-a164c4930129` remains Released; exactly one new GHS 500 reservation `86e1b52f-b4e3-4336-9603-5b0537d547bb` is Reserved |
| Reviewer boundary | `finance.demo.senior` / Adwoa Reviewer sees the pending first-stage Approve/Reject task, but the journal review exposes no Withdraw, Edit or Post action |
| First-stage decision | `finance.demo.senior` approved at `30 August 2026 11:10`; the workflow advanced to Finance Manager Approval and the reservation remained exactly GHS 500 Reserved. |
| Approval-queue follow-up | The completed `Single` first stage left the unused Accounts Officer sibling approval Pending. The shared engine now expires unused Pending/Queued siblings before completing a satisfied group or step; focused workflow regressions pass 2/2. Because this workflow's decision predated deployment of the fix, it must be withdrawn and rerun before later-stage evidence is accepted. |
| Cancellation-date follow-up | The shared workflow engine wrote terminal `CompletedDate` but left `CancelledDate` null. Both cancellation paths now populate the dedicated date; focused engine regression and API withdrawal regression pass. A later fresh cycle must retain live timestamp evidence. |
| Corrective withdrawal | `finance.demo.controller` withdrew workflow `3034402d-cf4f-4dd4-9fd4-31e68b127554` at `30 August 2026 16:30` with reason `UAT rerun after approval-sibling audit correction.` The journal returned to Draft/Withdrawn, the Finance Manager step was cancelled, the unused first-stage sibling and manager approvals were Expired, and no approval remained Pending. |
| Corrective reservation | Reservation `86e1b52f-b4e3-4336-9603-5b0537d547bb` changed from Reserved to Released for exactly GHS 500 with the matching withdrawal reason. The earlier released reservation remained unchanged. |
| Live cancellation timestamp | The corrective workflow has `CompletedDate` and `CancelledDate` both populated at `2026-08-30T16:30:15.6309407`. The older workflow retains its historical pre-fix null `CancelledDate`; no retrospective database rewrite was performed. |
| Fresh post-fix submission | `finance.demo.controller` submitted at `30 August 2026 16:37`. Withdrawal metadata cleared, the journal returned to Pending Approval, and `edecc13d-00b1-4a6a-bea7-2c2b7278ce44` is the only active workflow with exactly the Senior Accountant and Accounts Officer first-stage approvals Pending. |
| Fresh post-fix reservation | `2636189f-f184-41cb-907d-32d1e2c09579` is the only active reservation for exactly GHS 500. Both prior reservations remain Released. |
| Post-fix first-stage result | `finance.demo.senior` approved at `30 August 2026 16:44:55`. The Senior Accountant approval is Approved, the unused Accounts Officer sibling is Expired with `Approval request closed because the step approval requirement was satisfied.`, the first step is Completed, and the workflow advanced to Finance Manager Approval with zero first-stage approvals Pending. |
| Manager result | `finance.demo.manager` approved at `30 August 2026 16:54:04`. The Finance Manager step is Completed, the workflow advanced to Financial Controller Final Approval, and the only active reservation remains exactly GHS 500. |
| Final-stage stop-line | The journal initiator is `finance.demo.controller`, which is also the only active FINANCE-DEMO user carrying the Financial Controller role. The other global Financial Controller user belongs only to the DEFAULT tenant. Final self-approval or an administrative bypass would invalidate maker-checker evidence, so neither was attempted. |
| Corrective rerun withdrawal | `finance.demo.controller` withdrew workflow `edecc13d-00b1-4a6a-bea7-2c2b7278ce44` at `30 August 2026 17:39` with reason `UAT rerun after mandatory approver eligibility hardening.` The journal returned to Draft and the GHS 500 reservation returned to zero before the fail-fast rerun. |
| Fail-fast correction | Workflow startup now checks every mandatory, unconditional approval stage before creating an instance. Active tenant membership, role assignment and `PreventInitiatorApproval` are applied together, so a controller-made journal with no independent tenant controller is rejected before workflow persistence. The route lookup was also corrected to order by mapped `WorkflowStep.Order`, and the governance reader now uses the engine's canonical string-enum JSON contract. Shared workflow regressions pass 3/3 and tenant/role/repository eligibility regressions pass 3/3. |
| Authenticated controller-maker negative | **Pass — 30 August 2026.** Submitting Draft `JE-2026-000007` as its maker `finance.demo.controller` returned: `Workflow 'Journal Entry Approval' cannot start because approval step 'Financial Controller Final Approval' has no independent active user in this tenant eligible for role 'Financial Controller'.` The journal remained Draft, no Workflow Assignment panel appeared, Reserved remained GHS 0, Available remained GHS 10,000, and no new `Submitted For Approval` audit entry was recorded. |
| Fresh Accounts-maker source | `finance.demo.accounts` created `JE-2026-000008` (`ecb23cc5-6a47-4457-a14c-e0105df64825`) for 15 September 2026: Dr `100-6000-0000` / Cr `000-2100-0000`, GHS 500, exact `DEPT: FIN` + `PROJECT: P100`, reference `UAT-JRW-ACCOUNTS-202609`. The adopted September cell resolved to GHS 10,000 available before submission. |
| Fresh Accounts-maker submission | **Pass — 30 August 2026 18:17.** Submission created exactly one active workflow `6bed4f0d-e864-4df8-9b07-67dcef587a2b`, initiated by the Accounts maker, at `Accounts Officer Review`. The UI correctly states that the maker cannot act and identifies Senior Accountant or Accounts Officer as the pending authority. |
| Fresh Accounts-maker reservation | Exactly one active reservation `55b098de-2cfa-4a41-9c59-e7de4a673767` holds GHS 500 against BudgetEntry `41f00c0f-e1e0-421a-a4bb-deb78fecaf7f`. The evaluation deliberately excludes this journal's own reservation from `ReservedAmount` to keep revalidation hash-stable; the UI label was corrected from `Reserved` to `Other reserved` so GHS 0 is not mistaken for missing source evidence. |
| Fresh Accounts-maker first-stage result | **Pass — 30 August 2026 18:23:47.** `finance.demo.senior` approved the Senior Accountant task. The alternative Accounts Officer approval was automatically Expired with `Approval request closed because the step approval requirement was satisfied.`, the workflow advanced to `Finance Manager Approval`, and the same GHS 500 reservation remains active. |
| Fresh Accounts-maker manager-stage result | **Pass — 30 August 2026 18:58:37.** `finance.demo.manager` approved the Finance Manager task. The workflow advanced to `Financial Controller Final Approval`, exactly one controller approval is Pending, and reservation `55b098de-2cfa-4a41-9c59-e7de4a673767` still holds GHS 500. |
| Fresh Accounts-maker final approval | **Pass — 30 August 2026 19:03:23.** `finance.demo.controller` approved the Financial Controller task. Workflow `6bed4f0d-e864-4df8-9b07-67dcef587a2b` completed, `JE-2026-000008` is Approved/Approved rather than Posted, and the same GHS 500 reservation remains Reserved pending the separate posting action. |
| Fresh Accounts-maker posting | **Pass — 30 August 2026 19:05:27.** `finance.demo.controller` performed the separate posting action. `JE-2026-000008` is Posted with balanced GHS 500 debit/credit totals; both ledger lines retain canonical dimension sets for `DEPT: FIN` + `PROJECT: P100`. Reservation `55b098de-2cfa-4a41-9c59-e7de4a673767` changed once from Reserved to Consumed and links to posting event `ba0670c8-a7d6-41c2-ade1-e77436b06d94`. |
| Post-consumption budget position | **Pass — 30 August 2026.** BudgetEntry `41f00c0f-e1e0-421a-a4bb-deb78fecaf7f` for September, Salaries and exact `DEPT: FIN` + `PROJECT: P100` retains GHS 10,000 budget, has GHS 500 posted actual, zero active reservation and GHS 9,500 available. The Consolidated Budget continues to show the intentional account-level aggregate (GHS 14,000 budget / GHS 6,000 actual), and its new `2 dimension cell(s)` drill-down reconciles the exact cells independently: FIN/P100 = GHS 10,000 / GHS 500 / GHS 0 / GHS 9,500 and OPS/P200 = GHS 4,000 / GHS 5,500 / GHS 0 / negative GHS 1,500 for Budget / Actual / Reserved / Available. Codes and names are visible for every dimension assignment. Focused backend reporting tests passed 2/2; the drill-down component test passed 1/1; targeted ESLint passed. |

The approve-only reviewer half of step 5, resubmission/idempotency, corrective withdrawal and live
cancellation timestamp, post-fix first-stage sibling expiry, and authenticated controller-maker
startup rejection now pass. The unrelated-writer negative and a fresh accounts-maker positive
approval/posting cycle remain open. The controller-authored journal must not be used for that
positive cycle because creator identity is immutable. This checkpoint must not be represented as
complete `UAT-JRW-001` sign-off.

## Phase B — direct AP expense control

**Execution point:** PR `#120`, the Phase B migration and the matching safe runtime gates now pass.
Authenticated AP execution evidence remains outstanding. A successful Phase A or withdrawal result
does not authorize or imply AP budget-control sign-off.

### Phase B read-only gate checkpoint — 30 August 2026

The controlled preflight stopped before opening or creating an AP invoice:

| Gate | Result |
| --- | --- |
| Source | Pass — merge commit `0259d281` for PR `#120` is an ancestor of `origin/master` |
| API containment | Pass — `FinanceUatSafeHost` listens only on `127.0.0.1:5100`, targets `RHEMAERP`, starts zero product workers, and skips startup initialization |
| Frontend | Pass — the local Next.js listener is available on port `3000` |
| Phase A migration | Pass — `20260825235055_AddFinanceBudgetControlDimensions` is applied |
| Phase B migration | **Hold** — `20260826013000_AddApVendorInvoiceBudgetEvidence` is pending |
| Business mutation | None — no AP document, reservation, workflow, journal, or posting event was created |

Do not proceed to `UAT-APB-001` until the named migration is reviewed and applied through the
controlled deployment path, the API is rebuilt/restarted from the matching source, and the same
read-only gate reports the migration as applied.

### Phase B deployment gate update — 30 August 2026

| Gate | Result |
| --- | --- |
| Migration | Pass — `20260826013000_AddApVendorInvoiceBudgetEvidence` applied; EF reports zero pending migrations |
| Physical/runtime source | Pass — API rebuilt from the current source and the AP budget-cell query defect was corrected to use mapped account status and selected-date eligibility |
| Automated regression | Pass — `FinanceBudgetCommitmentServiceTests` 13/13 |
| Safe runtime | Pass — worker-free `FinanceUatSafeHost`, `RHEMAERP`, startup initialization skipped, loopback-only `127.0.0.1:5100` |
| Read-only AP endpoint | Pass — the previously failing eligible-budget-cell request returns HTTP 200 |
| Authenticated AP lifecycle | Open — continue with `UAT-APB-001` through `UAT-APB-008` |

### UAT-APB-001: list only eligible budget cells

1. Open `/finance/ap/invoices/create`.
2. Create an ordinary, non-opening invoice with no purchase order.
3. Add an `Expense` line and select the controlled budget-tracked expense account.
4. Set the invoice date inside the controlled fiscal period.

Expected:

- the line loads selectable adopted Finance budget cells;
- each option identifies its fiscal period and dimension combination and shows approved, actual,
  reserved, and available functional-currency amounts;
- cells for another tenant, account, date, unapproved return, inactive scenario, or mismatched
  dimension combination are absent;
- changing the invoice date, account, opening-balance flag, or purchase-order context clears the
  prior BudgetEntry selection.

Evidence: request to `GET /api/ap/invoices/budget-cells`, response, and selected BudgetEntry ID.

Execution evidence — 30 August 2026: **Pass**.

- The controlled account `100-6000-0000` on `15 September 2026` returned exactly the two adopted
  September cells for `DEPT: FIN / PROJECT: P100` and `DEPT: OPS / PROJECT: P200`.
- The selector displayed period, Approved, Actual, Reserved, and Available functional-currency
  amounts. The observed FIN/P100 position was `GHS 10,000 / GHS 500 / GHS 0 / GHS 9,500`; the
  observed OPS/P200 position was `GHS 4,000 / GHS 5,500 / GHS 0 / -GHS 1,500`.
- FIN/P100 resolved to BudgetEntry `41f00c0f-e1e0-421a-a4bb-deb78fecaf7f`.
- Changing the invoice date after selection removed the prior BudgetEntry selector and evidence.
- The controlled date input retained `15 September 2026` through the account-selection rerender.
  Its focused component suite passed `4/4`, and targeted ESLint passed.
- No supplier invoice, workflow, reservation, journal, or posting event was created during this case.

### UAT-APB-002: fail closed without a required budget cell

Submit a direct expense invoice on the budget-tracked account without selecting a BudgetEntry, then
repeat with a stale or mismatched BudgetEntry ID.

Expected:

- submission fails before workflow starts;
- no active reservation, journal, or posting event is created;
- the controlled error distinguishes missing selection from account/period/dimension mismatch.

Execution evidence — 31 August 2026: **Pass**.

- Draft invoice `VI-2026-00005` (`bb0bd8ac-5ea7-440c-bfb7-005444400ac8`) was created for
  `GHS 500` on `15 September 2026` against `100-6000-0000`, deliberately without a BudgetEntry.
- Submission failed with: `AP expense line 'UAT missing required budget cell' requires an adopted
  Finance budget cell before submission.` The invoice remained `Draft`; no workflow, reservation,
  journal, or posting event was started.
- The canonical mismatch path is covered by the focused Finance commitment regression, which
  returns `BUDGET_CELL_MISMATCH` when the producer account/period/dimension evidence does not match
  the selected cell. The mismatch and valid AP reservation tests passed `2/2`.
- UAT also exposed and corrected a supplier-identity defect: manual AP entry now loads the
  tenant-scoped `/api/ap/invoices/suppliers` projection and submits `Supplier.Id`, never a
  `BusinessPartner.Id`. The first rejected identity attempt made no database write.
- The draft list exposed an `Edit Invoice` action whose `/finance/ap/invoices/{id}/edit` route did
  not exist. The route now reuses the controlled AP form in edit mode, loads the full saved draft,
  preserves hidden payment/WHT/matching/rate evidence, keeps supplier identity immutable, and
  refuses non-Draft/non-Rejected documents. Browser verification loaded `VI-2026-00005` with its
  saved supplier, dates, account, description and `GHS 500` amount; no update was submitted.

### UAT-APB-003: reserve on submission

Create a `GHS 2,000` direct expense invoice against the `GHS 10,000` cell and submit it.

Expected:

- the invoice becomes pending approval;
- exactly one active reservation exists for the invoice and BudgetEntry;
- reserved amount is `GHS 2,000`; from an otherwise unused `GHS 10,000` cell, available becomes
  `GHS 8,000` (subtract any pre-existing posted actual or active reservation from that clean
  baseline), and the idempotency key is stable;
- retrying submission does not create a second reservation or workflow.

Execution evidence — 31 August 2026: **Pass**.

- Invoice `VI-2026-00006` (`36bed279-ee5d-40e3-a440-f02154c1d51a`) was created for `GHS 2,000`
  against FIN/P100 and submitted successfully.
- The invoice moved to `PendingApproval`. The live cell moved from `GHS 500` posted / `GHS 0`
  reserved / `GHS 9,500` available to `GHS 500` posted / `GHS 2,000` reserved / `GHS 7,500`
  available.
- A second submission attempt was rejected after the invoice had left Draft; the cell still showed
  exactly one `GHS 2,000` reservation and the workflow remained at its first pending stage.
- The invoice-list action no longer bubbles into row navigation. Submission now invalidates list,
  detail, and workflow-summary queries, preventing a stale Draft action from being rendered after a
  successful submit.

### UAT-APB-004: reject and release

Reject the pending invoice through its configured workflow.

Expected:

- the reservation becomes `Released` with reason, actor, and timestamp;
- available amount returns to `GHS 10,000`;
- no journal or posted actual exists.

Execution checkpoint — 31 August 2026: **Retest required**.

- Adwoa Reviewer rejected `VI-2026-00006` through the shared Finance approval workbench with the
  controlled reason `UAT APB-004 rejection to verify the Finance budget reservation is released.`
- The invoice and workflow reached `Rejected`, and no journal or posting event was created, but the
  GHS 2,000 reservation remained `Reserved`; FIN/P100 therefore remained at GHS 500 actual,
  GHS 2,000 reserved and GHS 7,500 available.
- Root cause: the shared approval controller applied the terminal VendorInvoice status directly and
  bypassed AP's authoritative budget-release operation. It now delegates the terminal outcome to
  `IVendorInvoiceService.ApplyRejectedWorkflowOutcomeAsync`, which releases the reservation before
  changing status. An idempotent retry also repairs a pre-fix rejected invoice with stranded active
  evidence without replaying the workflow or duplicate rejection audit.
- Focused direct-rejection and stranded-retry regressions pass 2/2. The pre-fix live reservation is
  retained as defect evidence and must not be represented as released; execute a fresh rejection
  cycle on the rebuilt API before marking this case Pass.

### UAT-APB-005: approve, post, and consume atomically

Submit a replacement `GHS 2,000` invoice, complete approval, and post it.

Expected:

- the reservation becomes `Consumed` with the exact journal and posting-event IDs;
- the expense AccountTransaction is `GHS 2,000` debit and carries the selected immutable Finance
  dimension-set ID;
- posted actual is `GHS 2,000`, active reservations are zero, and available amount is `GHS 8,000`;
- invoice, reservation operation, posting event, journal, and AccountTransaction commit together;
- a posting retry returns the existing event and does not create or consume another reservation.

### UAT-APB-006: enforce concurrency and availability

With only `GHS 8,000` available, submit two independent invoices whose combined functional amount
exceeds `GHS 8,000`.

Expected:

- serializable Finance budget control allows only amounts within the authoritative available
  balance;
- at least one request is rejected rather than oversubscribing the cell;
- no partial workflow or orphan reservation remains for the rejected request.

### UAT-APB-007: controlled foreign currency

Create a foreign-currency direct expense invoice whose exact approved exchange-rate snapshot converts
the line to `GHS 1,500`.

Expected:

- availability and reservation use `GHS 1,500`, not the transaction-currency amount;
- the exact ExchangeRate ID is retained and revalidated at submission and posting;
- the GL expense line retains transaction currency, functional amount, and the selected dimension set.

### UAT-APB-008: prove exclusions and ownership boundaries

Repeat the entry flow for each excluded source:

- opening AP invoice;
- PO/GRV-backed invoice;
- Inventory or Product line;
- direct fixed-asset line.

Expected:

- the direct-AP adapter creates no Finance budget reservation for these sources;
- existing opening, Procurement/GRV, Inventory, and fixed-asset controls remain authoritative;
- no source is counted twice merely because its invoice reaches AP.

## Release stop-lines

Do not sign off or work around any of the following:

- an incomplete or ineffective dimension combination is persisted;
- two entries occupy the same scenario/return/account/period/dimension hash;
- an unapproved or unlocked-ineligible cell appears in AP;
- a budget-tracked direct expense invoice submits without its exact BudgetEntry;
- rejection leaves an active reservation;
- posting succeeds without consumed reservation evidence or without the GL dimension set;
- duplicate submission/posting creates additional workflow, reservation, journal, or event rows;
- any excluded source acquires a direct-AP reservation;
- tenant, period, account, currency, or dimension evidence can be substituted client-side.

## Sign-off record

| Field | Value |
| --- | --- |
| Tenant | |
| Build/commit | |
| Phase A migration head | `20260825235055_AddFinanceBudgetControlDimensions` / Applied 27 August 2026 |
| Phase B migration head | `20260826013000_AddApVendorInvoiceBudgetEvidence` / Applied 30 August 2026 |
| Phase executed | A / B / Both |
| Scenario / return | |
| BudgetEntry / combination hash | |
| AP invoice | |
| Reservation / operation | |
| Posting event / journal | |
| Preparer | |
| Reviewer/poster | |
| Auditor | |
| Execution date | |
| Result | Pass / Fail |
| Evidence location | |
| Exceptions accepted | None |
