# Finance bank-deposit approval visibility remediation - 2026-10-05

## Objective and scope

Verify the reported bank-deposit workflow and maker/checker behavior, hide self-review and self-confirmation actions, and expose the existing assigned bank-deposit workflow in the shared Finance approval workbench without bypassing banking-domain controls.

## Branch and worktree

- Branch: `codex/finance-uat-remediation-20261004`
- Worktree: `.w/fin-uat-remediation-20261004`
- Exact starting commit: `121f051149c1bdcf4d7385521c08c31db9fe88c8`
- Pull request: #356 (follow-up to merged #353; the remediation chain began at #352)

## Confirmed evidence and classification

- `BankingSettlementService.SubmitDepositAsync` already starts a `BankDepositBatch` workflow and stores its workflow instance ID.
- Tenant workflow provisioning already creates a Chief Accountant approval stage for `BankDepositBatch`.
- The workflow engine emits an approval-request notification for the assigned user or role when that stage becomes pending.
- The banking service already rejects approve, reject, and return decisions by the submitting user.
- The deposit detail page used only `Finance.Banking.Deposits.Approve` and therefore exposed decision buttons to a permission-bearing submitter.
- `BankingSettlementService.ConfirmDepositAsync` correctly rejects the submitter through `EnsureMakerChecker`, but the posted-deposit UI exposed the bank-acknowledgement form using only `Finance.Banking.Deposits.Confirm`.
- The global exception handler intentionally maps that domain `UnauthorizedAccessException` to generic `ACCESS_FORBIDDEN`, so the invalid UI action appeared to be a missing role permission.
- The shared Finance approval workbench excluded `BankDepositBatch`, despite the valid pending workflow.
- Classification: UI authorization defects plus shared-workbench catalogue/outcome-routing defect. The bank-confirmation denial is an expected backend maker/checker control exposed through an invalid frontend action, not a missing permission.

## Safe remediation

- Add the API-provided `submittedById` to the frontend contract.
- Hide approve/return/reject actions from the submitter and show an explicit assigned-checker message.
- Add `BankDepositBatch` to the Finance workbench allowlist, facts, and detail routing.
- Require both workflow decision permission and `Finance.Banking.Deposits.Approve` in the workbench.
- Preserve maker/checker separation in the generic workbench guard.
- Route approve/reject workbench decisions through `IBankingSettlementService`; do not apply a bare workflow transition.
- Require both `Finance.Banking.Deposits.Confirm` and an authenticated identity different from `submittedById` before rendering the bank-acknowledgement form.
- Show the submitter an explicit independent-user message instead of an actionable form that can only return 403.

## Notification interpretation

The configured approval target is the `Chief Accountant` role. A System Administrator who has broad route permission but is not assigned that workflow role should not receive or action the approval. If an independently logged-in Chief Accountant still receives no notification after this code is active, capture the workflow instance, approval row, notification activity, authenticated roles, and correlation logs before changing notification delivery.

## Changed files

- `frontend/src/types/cash-management.ts`
- `frontend/src/app/finance/cash/deposits/[id]/page.tsx`
- `frontend/src/lib/finance/bank-deposit-access.ts`
- `frontend/src/lib/finance/bank-deposit-access.test.ts`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FinanceApprovalAuthorityContractTests.cs`
- this ledger

## Verification

- Focused bank-deposit frontend access suite: passed 5/5 after adding bank-acknowledgement maker/checker coverage.
- Finance approval authority contracts plus `Deposit_ShouldRequireMakerCheckerThenPostOneNetBankTransaction`: passed 21/21.
- Targeted ESLint for the changed till/deposit pages, access helpers, tests, and cash-management types: passed.
- Full frontend `tsc --noEmit`: blocked by existing unrelated repository errors in civil engineering, inventory, HR medical, reporting, fixed assets, and other unchanged files; no changed till/deposit file appeared in the error set.
- `git diff --check`: passed (line-ending conversion notices only).

## Migrations and application state

- Schema migration: none.
- UAT data mutation: none.
- Deployment/restart: not performed.

## Completed commit

- `4ce834927a78b742ea264194ab6274b5338636e4` - `fix(finance): close receipt and cash approval gaps`
- `4e30e26d43be376f5eb49717cba94728e43c73e4` - `fix(finance): hide self-confirmation of bank deposits`

## Authorization boundaries

Push/update of PR #356 is authorized. Do not merge, deploy, restart services, apply migrations, or mutate UAT data without separate authorization.
