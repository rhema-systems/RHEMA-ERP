---
integration_cycle: FIN-UAT-2026-10-07-A
integration_status: integrated_pr_pending
integration_decision: include
candidate_branch: codex/fin-uat-bank-recon-fx-override-20261007-v2
candidate_head: 905eed2c3
base_commit: 28c067ae2fbe847cc4b1c24a9fa7781a0c420ed7
target_ref: origin/master
depends_on: none
migration_status: generated_and_applied_to_named_local_uat_only
verification_status: passed_with_documented_baseline_drift
integration_commit: 8372d2847
pull_request: pending
---

# Transaction-specific exchange-rate override implementation ledger

## Objective

Implement privileged, transaction-specific numeric exchange-rate overrides for eligible Finance transactions. An override must apply only to the identified source transaction and currency, require a meaningful reason, preserve the governed rate as immutable evidence, complete an independent maker-checker workflow before posting, and remain auditable after posting.

## Repository state

- Integration worktree: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP\.w\fin-uat-bank-recon-fx-20261007-v2`
- Branch: `codex/fin-uat-bank-recon-fx-override-20261007-v2`
- Exact integration base: `28c067ae2fbe847cc4b1c24a9fa7781a0c420ed7`
- The primary checkout and the earlier failed candidate worktree are preserved; unrelated changes were not imported.

## Surface inventory and scope decision

| Area | User-facing surface | Posting identity / durable source | Decision |
| --- | --- | --- | --- |
| AP invoice | `/finance/ap/invoices/create` plus invoice detail/edit | `VendorInvoice` | Include for draft foreign-currency invoices. |
| AP payment | `/finance/ap/payments/create` plus payment detail | `VendorPayment` | Include for draft foreign-currency payments. Settlement-specific later-rate calculations remain governed separately. |
| AP purchase order / receipt | `/finance/ap/purchase-orders/create`, PO detail and receipt preparation | `FinancePurchaseOrder` / `FinancePurchaseOrderReceipt` | Excluded from this implementation. The current posting identities and rate hand-off are not the same durable source contract used by invoice posting; the audit's misleading editable-rate gap remains separate work. |
| AP supplier debit note | supplier debit-note detail | `SupplierDebitNote` | Exclude from ordinary manual override. Linked notes deliberately preserve the original approved invoice snapshot. |
| AR invoice | `/finance/ar/invoices/new` plus invoice detail/edit | `CustomerInvoice` | Include for draft foreign-currency invoices. |
| AR receipt/payment | `/finance/ar/payments/new` plus payment detail | `CustomerPayment` | Include for unposted foreign-currency receipts. Allocation/advance FX calculations remain governed separately. |
| Cash receipt/payment/transfer | `/finance/cash/transactions/receipts`, `/payments`, `/transfers` | `CashTransaction` | Include for captured/unposted foreign-currency transactions after the durable transaction exists. One numeric override is supported per posting request; independently overridden multi-currency transfer legs remain fail-closed. |
| Manual journal | `/finance/journal-entries/new`, edit and detail | `ManualJournalEntry` | Include for draft entries with one governed foreign-currency snapshot. Independently overridden multi-currency journals remain fail-closed because the posting contract currently carries one approval-evidence tuple. |
| Opening balances | `/finance/opening-balances` | `OpeningBalanceBatch` | Include for unsubmitted persisted batches with one governed foreign-currency snapshot. Independently overridden multi-currency batches remain fail-closed. |
| Subledger adjustment | `/finance/subledger-adjustments/new` | `SubledgerAdjustmentJournal` | Not directly eligible while creation and posting are one atomic action. The misleading editable rate must remain blocked or be split into a durable draft lifecycle before an override can be offered. |
| Fixed-asset disposal | `/finance/fixed-assets/disposals` | `AssetDisposal` | Backend eligibility is implemented for a true Draft disposal, but the current UI submits on creation and exposes no durable Draft window. No misleading override control was added; a separate draft/save lifecycle is required before this surface can use the control. |
| Revaluations, depreciation, recurring reversals, year-end, automatic settlement FX, posting reversals | system/control surfaces | system-generated posting identities | Exclude. These use configured closing/historical rates and existing correction controls; a transaction-entry override would weaken those controls. |
| Procurement landed cost and Payroll payment-method conversion | non-Finance operational surfaces | Procurement / Payroll records | Record as adjacent gaps, not silently broadened into this Finance implementation. |

## Control contract

### Permissions

- `Finance.FX.TransactionRateOverride.Request` is required to create, resubmit, or supersede a transaction-rate override request.
- `Finance.FX.TransactionRateOverride.Approve` is required in addition to normal Finance workflow approval authority to approve or reject these requests.
- Read access follows `Finance.Read` and the source transaction's ordinary visibility rules.

### Maker-checker rules

- The requester must be an authenticated tenant user and is retained on the request.
- The final approving user must differ from the requester. Delegation, workflow assignment, or generic workflow permission must never bypass this separation.
- A completed workflow instance for the exact override-request entity is mandatory. Caller-supplied approver IDs/timestamps are not trusted.
- The reason is mandatory, trimmed, and at least 10 characters.

### Transaction binding and posting snapshot

- Each request is bound to tenant, canonical source-document type, source-document ID, transaction currency, functional currency, the governed exchange-rate record/value, the requested value, and a server-derived source version/hash.
- The governed rate ID and full rate snapshot remain unchanged as evidence even when the override is approved and consumed.
- Posting may consume only the latest Approved request matching the exact source, currency, governed snapshot, and unchanged source version.
- The posting snapshot records the override request/workflow identity, governed rate, applied rate, reason, requester, approver, approval time, and posting event.
- Approval affects only the identified source transaction and currency; it never edits or creates an exchange-rate master record.

### Resubmission, rejection, and editing

- Rejection is terminal for that immutable request and retains the workflow history and rejection reason.
- A corrected request is a new request/workflow. Prior rejected or superseded evidence is retained.
- Changing the source transaction, governed rate, currency, or other snapshot-significant source data supersedes any pending/approved unconsumed request. Pending workflow instances are cancelled. An active request for an unchanged snapshot cannot be silently replaced with a different requested rate/reason; it must first be rejected (or consumed if approved), after which resubmission creates a new immutable request/workflow.
- A consumed request cannot be reused by another transaction or later posting action.

### Audit evidence

- Emit dedicated request, approval, rejection, supersession, posting-consumption, and blocked-posting audit events.
- Workflow activity remains the approval authority record; Finance audit events provide transaction-oriented evidence.
- Posting-engine request hashing/idempotency must include the override request/workflow identity and both governed and applied rate evidence.

## Implemented surfaces

- Finance permission catalog and route policy map.
- A durable `FinanceExchangeRateOverrideRequest` entity, DTOs, service interface/service, controller, workflow catalog entry, approval-inbox routing/outcome handling, and dependency injection registration.
- Finance posting command/engine integration that validates and consumes approved workflow evidence instead of trusting caller-supplied approval metadata.
- Reusable frontend service/types and override panel integrated into eligible persisted transaction surfaces.
- Focused backend tests cover mandatory reason enforcement, maker-checker separation, caller-evidence tamper rejection, legacy corrective-document compatibility, and permission separation.

## Changed files

- Coordination: `docs/Finance/coordination/TRANSACTION_EXCHANGE_RATE_OVERRIDE_LEDGER.md`
- Core contract/evidence: `src/ErpSystem.Core/Entities/Finance/FinanceExchangeRateOverrideRequest.cs`, `src/ErpSystem.Core/DTOs/Finance/FinanceExchangeRateOverrideDtos.cs`, `src/ErpSystem.Core/DTOs/Finance/FinancePostingDtos.cs`, `src/ErpSystem.Core/Interfaces/Finance/IFinanceExchangeRateOverrideService.cs`
- API/control path: `src/ErpSystem.Api/Services/Finance/MultiCurrency/FinanceExchangeRateOverrideService.cs`, `src/ErpSystem.Api/Controllers/Finance/FinanceExchangeRateOverridesController.cs`, `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`, `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs`, `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`, `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`, `src/ErpSystem.Api/Services/DatabaseSeedingService.cs`
- Permissions/audit: `src/ErpSystem.Shared/FinancePermissions.cs`, `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- Persistence: `src/ErpSystem.Data/ApplicationDbContext.cs`, `src/ErpSystem.Data/Migrations/20261006150254_AddFinanceTransactionExchangeRateOverrides.cs`, its designer, and `ApplicationDbContextModelSnapshot.cs`
- Frontend shared control: `frontend/src/services/finance/exchange-rate-override.service.ts`, `frontend/src/components/finance/TransactionExchangeRateOverridePanel.tsx`
- Frontend integrations: AP invoice/payment details, AR invoice/receipt details, cash transaction details, manual-journal details, and the persisted opening-balance workspace.
- Tests: `tests/ErpSystem.Api.Tests/Services/Finance/FinanceExchangeRateOverrideTests.cs` and its project include.

## Commits

- `dfb38fae0` — combined scoped implementation commit for transaction-specific FX overrides and bank-reconciliation hardening.
- `8372d2847` — integration commit resolved against current `origin/master`.

## Migrations and application status

- Source migration created: `20261006150254_AddFinanceTransactionExchangeRateOverrides`.
- Creates only the durable override-evidence table, its restricted evidence foreign keys, six-decimal governed/requested rate columns, one-live-request filtered uniqueness, workflow uniqueness, and lookup indexes.
- Migration application status: **applied on 2026-10-07 to `RHEMA-AKWASI\EXPRESS22` / `RHEMAERP_BOOKV2_UAT_20260922` with explicit user authorization**.
- The target database contained the prior migration `20261006010000_AllowBankReconciliationRematchAfterUnmatch`, but its history also has older gaps that caused an ordinary `dotnet ef database update` to attempt replaying `20260304155434_RecreateHRTables`. That attempt failed during SQL generation before executing any SQL. The authorized migration was therefore applied as one bounded, `XACT_ABORT` SQL transaction translated directly from the migration `Up` operations, together with its EF `8.0.0` history row.
- Post-application verification: the history row is present; `FinanceExchangeRateOverrideRequests` exists with 40 columns and zero initial rows; the five migration-defined secondary indexes plus its primary key exist; and all four evidence foreign keys reference the expected tables with `NO_ACTION` delete behavior.

## Verification evidence

- Integrated API build: passed, 0 errors and 45 existing warnings.
- Finance permission/route/FX override focused backend run: passed, 33/33.
- Approval Workbench Vitest: passed, 5/5.
- Targeted ESLint passed for the shared panel/service and all changed frontend files except AP payment detail; its only finding is the pre-existing non-null assertion at line 227 on `origin/master`, outside this PR's changed lines.
- `git diff --cached --check`: passed before the integration commit.
- EF `migrations has-pending-model-changes` reports 12 operations, but the model-differ probe identifies only pre-existing Security/Audit/Estate changes. No pending operation concerns `FinanceExchangeRateOverrideRequests` or this migration.
- UAT database migration verification on 2026-10-07: passed for `RHEMAERP_BOOKV2_UAT_20260922` (history row, table shape, indexes, and restricted foreign keys).

## Known failures / risks

- The checkout contains unrelated work, including overlapping Finance approval and API-service files; edits must be narrow and reviewed against their pre-existing diffs.
- Existing transaction services do not all expose a uniform draft snapshot API. Source eligibility/version validation must be explicit and fail closed.
- The workflow-provisioning ledger records an existing tenant provisioning gap. This implementation must add the new workflow catalog entry but must not mutate tenant databases in this task.
- Existing tenants still require the normal authorized workflow-provisioning/migration process before requests can start; neither was applied here.
- Multi-currency postings with more than one independently overridden currency deliberately fail closed because `FinancePostingCommandDto` currently retains a single override approval-evidence tuple.
- Fixed-asset disposal has no usable Draft UI lifecycle, and atomic subledger adjustment creation/posting has no durable pre-posting source. Those screens do not expose an override button.

## Remaining work

- Provision the transaction exchange-rate override workflow in the selected UAT tenant(s); workflow provisioning was not part of the migration authorization and remains unapplied.
- Review and merge the combined PR only when separately authorized; no deployment was performed.
- UAT requester/checker separation, rejection then resubmission, stale-source supersession, posting consumption, duplicate posting, and audit export after provisioning.
- Decide separately whether to extend the posting contract to multiple override evidence tuples and whether to add durable Draft lifecycles for asset disposals and subledger adjustments.

## Authorization boundaries

Authorized: inspect and edit repository source/tests/docs, create the migration source, run local verification, and apply migration `20261006150254_AddFinanceTransactionExchangeRateOverrides` specifically to `RHEMA-AKWASI\EXPRESS22` / `RHEMAERP_BOOKV2_UAT_20260922` on 2026-10-07.

Authorized on 2026-10-07: create scoped commits, push the clean integration branch, and create the combined pull request.

Not authorized: deploy, perform any further database mutation or migration, merge the pull request, alter live workflow configuration, remove worktrees/branches, or discard unrelated working-tree changes.
