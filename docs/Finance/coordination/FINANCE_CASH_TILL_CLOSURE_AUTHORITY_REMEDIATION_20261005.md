# Finance cash-till closure authority remediation - 2026-10-05

## Objective and scope

Close the observed cashier-till maker/checker UI gap, verify the backend segregation-of-duties boundary, and determine whether submitted till closures are represented in the shared Finance approval workbench and its workflow notifications.

## Branch and worktree

- Branch: `codex/finance-uat-remediation-20261004`
- Worktree: `.w/fin-uat-remediation-20261004`
- Exact starting commit: `121f051149c1bdcf4d7385521c08c31db9fe88c8`
- Pull request: #352

## Evidence and classification

- The till detail UI showed `Approve closure` and `Return for recount` to the cashier whenever that user also held `Finance.CashTills.Closures.Review`.
- `CashierTillService.ReturnForRecountAsync` already rejected the cashier unconditionally.
- `CashierTillService.ApproveClosureAsync` rejected self-approval only when the legacy `RequireIndependentCashTillClosure` setting was true.
- `CashierTillSession` is absent from the Finance workflow seed catalogue and approval-workbench allowlist, and `SubmitCountAsync` does not start a workflow.
- Classification:
  - button visibility: confirmed UI authorization defect;
  - conditional self-approval boundary: confirmed backend authorization defect;
  - shared approval workbench/notification: confirmed route-to-workbench integration gap, not an environment or role-assignment problem.

## Safe remediation in this atomic change

- Fail closed in the UI until the authenticated user identity is known.
- Hide both till-review decisions from the cashier who owns the session and show an explicit different-checker message.
- Reject self-approval unconditionally in the backend, regardless of the legacy tenant switch.
- Add frontend and backend contract coverage.

## Workbench status and remaining work

There is currently no dedicated till-closure workflow instance, shared Finance approval-workbench row, or workflow notification. The till page remains the only pending-review register. Adding the shared workflow requires a separate coherent change covering workflow definition provisioning, atomic submission, workbench facts/detail routing, action outcomes, notification assignment, and reconciliation of existing `PendingReview` sessions. It must not be represented as fixed by the button change alone.

## Changed files

- `src/ErpSystem.Api/Services/Finance/Cash/CashierTillService.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/CashierTillControlTests.cs`
- `frontend/src/app/finance/cash/till-sessions/page.tsx`
- `frontend/src/lib/finance/cashier-till-access.ts`
- `frontend/src/lib/finance/cashier-till-access.test.ts`
- this ledger

## Verification

- `TillCount_ShouldDeriveExpectedCashAndRequireIndependentClosure`: passed in the combined receipt/till backend run (2/2 total).
- Focused frontend independent-review suite: passed 3/3.
- Targeted ESLint for the changed till/deposit pages, access helpers, tests, and cash-management types: passed.
- Full frontend `tsc --noEmit`: blocked by existing unrelated repository errors in civil engineering, inventory, HR medical, reporting, fixed assets, and other unchanged files; no changed till/deposit file appeared in the error set.
- `git diff --check`: passed (line-ending conversion notices only).

## Migrations and application state

- Schema migration: none.
- UAT data mutation: none.
- Deployment/restart: not performed.

## Completed commit

- `4ce834927a78b742ea264194ab6274b5338636e4` - `fix(finance): close receipt and cash approval gaps`

## Authorization boundaries

Push/update of PR #352 is authorized. Do not merge, deploy, restart services, apply migrations, or mutate UAT data without separate authorization.
