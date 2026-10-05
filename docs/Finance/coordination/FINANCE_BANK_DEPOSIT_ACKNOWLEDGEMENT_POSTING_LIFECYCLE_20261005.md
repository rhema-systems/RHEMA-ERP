# Finance bank-deposit acknowledgement and posting lifecycle - 2026-10-05

## Objective and scope

Reorder the governed bank-deposit lifecycle so an independently recorded bank acknowledgement is required before final GL posting, while preserving confirmation of legacy deposits that were already posted under the former sequence.

Target lifecycle:

`Draft -> Submitted -> Approved -> Bank acknowledged -> Posted -> Reconciled`

The existing `BankDepositStatus` and `BankDepositConfirmationStatus` fields remain the authoritative state model. No redundant lifecycle enum is introduced.

## Branch and worktree

- Branch: `codex/finance-uat-remediation-20261004`
- Worktree: `.w/fin-uat-remediation-20261004`
- Exact starting commit: `283a8d35811eab0e16d7779e78db05d5ae91915e`
- Pull request: #356

## Confirmed design and classification

- The former implementation posted the final bank GL entry immediately after approval when `AutoPostBankDepositAfterApproval` was enabled.
- Bank acknowledgement was then restricted to already-posted deposits, so the evidence could not govern posting.
- The natural control sequence is approval, independent bank acknowledgement, final posting, and later bank-statement reconciliation.
- Classification: lifecycle/control-order defect. The existing two-dimensional status model is sufficient; the defect is in transition guards, action visibility, and the auto-post trigger.
- Existing posted deposits with pending acknowledgement are grandfathered and remain confirmable without reposting.
- This work does not introduce a deposits-in-transit GL account. The existing final bank posting is delayed until acknowledgement.

## Implemented safe remediation

- Rename `AutoPostBankDepositAfterApproval` to `AutoPostBankDepositAfterConfirmation` in the entity, API contract, frontend settings, and database column.
- Stop automatic posting during submit/approve.
- Require `ConfirmationStatus == Confirmed` before manual posting.
- Permit acknowledgement for approved/pending deposits and legacy posted/pending deposits.
- Auto-post an approved deposit after acknowledgement when the renamed setting is enabled.
- Preserve the existing maker/checker rule for acknowledgement.
- Prevent cancellation after acknowledgement; an acknowledged deposit must be posted or handled through an explicit corrective control.
- Update deposit-detail actions and explanations to reflect the new order.
- Add release-gate coverage for automatic and manual post-after-confirmation modes and legacy confirmation.

## Changed files

- `src/ErpSystem.Api/Services/Finance/Cash/BankingSettlementService.cs`
- `src/ErpSystem.Api/Services/Finance/Settings/FinanceSettingsService.cs`
- `src/ErpSystem.Core/DTOs/Finance/FinanceSettingsDtos.cs`
- `src/ErpSystem.Core/Entities/Finance/BankingSettlement.cs`
- `src/ErpSystem.Core/Entities/Finance/FinanceSettings.cs`
- `src/ErpSystem.Core/Enums/CashManagementEnums.cs`
- `frontend/src/app/finance/cash/deposits/[id]/page.tsx`
- `frontend/src/app/finance/settings/page.tsx`
- `frontend/src/types/finance.ts`
- `tests/ErpSystem.Api.Tests/Services/Finance/BankingSettlementReleaseGateTests.cs`
- `src/ErpSystem.Data/Migrations/20261005211546_ReorderBankDepositAcknowledgementBeforePosting.cs`
- `src/ErpSystem.Data/Migrations/20261005211546_ReorderBankDepositAcknowledgementBeforePosting.Designer.cs`
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`

## Migrations and application state

- Migration: `20261005211546_ReorderBankDepositAcknowledgementBeforePosting`.
- The migration only renames `FinanceSettings.AutoPostBankDepositAfterApproval` to `AutoPostBankDepositAfterConfirmation`; unrelated scaffold drift was removed before application.
- Authorized target: `RHEMAERP_BOOKV2_UAT_20260922`.
- Application status: applied successfully on 2026-10-05 and confirmed in `__EFMigrationsHistory`; the renamed column is present.

## Verification

- Focused backend release-gate suite: 26/26 passed.
- Focused frontend bank-deposit access suite: 5/5 passed.
- Targeted ESLint for the changed deposit/settings/types/access files: passed.
- `git diff --check`: passed; only repository line-ending conversion warnings were emitted.

## Remaining work

- Commit the completed lifecycle change locally as an atomic remediation commit.
- Retain it for the user's later finalized, unified Finance PR.
- Do not push or update an existing PR until the user explicitly authorizes the consolidated PR step.

## Authorization boundaries

- Implementation and local commit are authorized.
- Applying this workstream's verified migration to `RHEMAERP_BOOKV2_UAT_20260922` is authorized.
- The user subsequently withdrew authorization to create, update, or push a PR until the remaining gaps are closed and a finalized unified PR is requested.
- Do not push, create or update a PR, merge, deploy, restart services, or perform other UAT data mutation without separate authorization.
