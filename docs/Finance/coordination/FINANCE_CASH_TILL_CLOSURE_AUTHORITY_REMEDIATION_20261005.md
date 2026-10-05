# Finance cash-till closure authority remediation - 2026-10-05

## Objective and scope

Close the observed cashier-till maker/checker UI gap, verify the backend segregation-of-duties boundary, determine whether submitted till closures are represented in the shared Finance approval workbench and its workflow notifications, and add a controlled correction path for unused till sessions.

## Branch and worktree

- Branch: `codex/finance-uat-remediation-20261004`
- Worktree: `.w/fin-uat-remediation-20261004`
- Exact starting commit: `121f051149c1bdcf4d7385521c08c31db9fe88c8`
- Pull request: #353 (follow-up to externally merged #352)

## Evidence and classification

- The till detail UI showed `Approve closure` and `Return for recount` to the cashier whenever that user also held `Finance.CashTills.Closures.Review`.
- `CashierTillService.ReturnForRecountAsync` already rejected the cashier unconditionally.
- `CashierTillService.ApproveClosureAsync` rejected self-approval only when the legacy `RequireIndependentCashTillClosure` setting was true.
- `CashierTillSession` is absent from the Finance workflow seed catalogue and approval-workbench allowlist, and `SubmitCountAsync` does not start a workflow.
- Classification:
  - button visibility: confirmed UI authorization defect;
  - conditional self-approval boundary: confirmed backend authorization defect;
  - shared approval workbench/notification: confirmed route-to-workbench integration gap, not an environment or role-assignment problem.
  - unused-session maintenance: confirmed product gap. An owner could open an erroneous custody window but had no audited way to correct opening float/notes/evidence or retire the unused record.

## Safe remediation in this atomic change

- Fail closed in the UI until the authenticated user identity is known.
- Hide both till-review decisions from the cashier who owns the session and show an explicit different-checker message.
- Reject self-approval unconditionally in the backend, regardless of the legacy tenant switch.
- Add frontend and backend contract coverage.
- Allow only the owning cashier with `Finance.CashTills.Operate` to edit opening float, notes, or evidence while the server proves the session is unused.
- Keep till, cashier, business date, currency, session number, and opening timestamp immutable.
- Replace hard deletion with an audited `Cancelled` terminal status, cancellation actor/time/reason, and a preserved opening-balance snapshot.
- Freeze edit/cancellation once canonical custody entries, posted deposit allocations, a denomination count, submission evidence, review evidence, or correction lineage exists.
- Return server-computed eligibility and a lock reason so the UI fails closed; permit a replacement session after cancellation.

## Workbench status and remaining work

There is currently no dedicated till-closure workflow instance, shared Finance approval-workbench row, or workflow notification. The till page remains the only pending-review register. Adding the shared workflow requires a separate coherent change covering workflow definition provisioning, atomic submission, workbench facts/detail routing, action outcomes, notification assignment, and reconciliation of existing `PendingReview` sessions. It must not be represented as fixed by the button change alone.

## Changed files

- `src/ErpSystem.Api/Services/Finance/Cash/CashierTillService.cs`
- `src/ErpSystem.Api/Controllers/Finance/CashierTillController.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Core/DTOs/Finance/CashierTillDtos.cs`
- `src/ErpSystem.Core/Entities/Finance/BankingSettlement.cs`
- `src/ErpSystem.Core/Enums/CashManagementEnums.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ICashManagementServices.cs`
- `src/ErpSystem.Data/Migrations/20261005191611_AddCashierTillSessionCancellationControls.cs`
- `src/ErpSystem.Data/Migrations/20261005191611_AddCashierTillSessionCancellationControls.Designer.cs`
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/CashierTillControlTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/CashBankPermissionContractTests.cs`
- `frontend/src/app/finance/cash/till-sessions/page.tsx`
- `frontend/src/lib/finance/cashier-till-access.ts`
- `frontend/src/lib/finance/cashier-till-access.test.ts`
- `frontend/src/services/finance/cash-management-data.service.ts`
- `frontend/src/types/cash-management.ts`
- this ledger

## Verification

- `TillCount_ShouldDeriveExpectedCashAndRequireIndependentClosure`: passed in the combined receipt/till backend run (2/2 total).
- Focused frontend independent-review suite: passed 3/3.
- Focused frontend till-access suite after unused-session coverage: passed 5/5.
- Combined Release backend service and permission-contract suite: passed 13/13. Release output was used so the user's running local Debug API remained uninterrupted.
- Finance model compiler completed successfully while building the targeted suite: 73 full models, 287,080 ordered statements, and 8,758 distinct statements across two lookup contexts.
- Targeted ESLint for the changed till/deposit pages, access helpers, tests, and cash-management types: passed.
- Full frontend `tsc --noEmit`: blocked by existing unrelated repository errors in civil engineering, inventory, HR medical, reporting, fixed assets, and other unchanged files; no changed till/deposit file appeared in the error set.
- `git diff --check`: passed (line-ending conversion notices only).
- A post-migration Debug test retry could not replace `ErpSystem.Data.dll` because the user's local API process (PID 61180) held the Debug output open. No process was stopped; the equivalent Release build and tests passed.

## Migrations and application state

- Schema migration authored: `20261005191611_AddCashierTillSessionCancellationControls` adds nullable `CancelledAt`, `CancelledById`, and `CancellationReason` columns to `CashierTillSessions`.
- Migration scope was reviewed and stripped of unrelated pre-existing Estate model drift before commit.
- Migration application: not applied locally or to UAT.
- UAT data mutation: none.
- Deployment/restart: not performed.

## Completed commit

- `4ce834927a78b742ea264194ab6274b5338636e4` - `fix(finance): close receipt and cash approval gaps`
- Pending - `feat(finance): govern unused till session corrections`

## Authorization boundaries

Push/update of PR #353 is authorized. Do not merge, deploy, restart services, apply migrations, or mutate UAT data without separate authorization.
