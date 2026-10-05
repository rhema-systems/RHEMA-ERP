---
integration_cycle: FIN-AUTH-AUDIT-2026-10-05
integration_status: complete-with-runtime-evidence-gap
integration_decision: safe-fixes-verified-auth-11-deferred
candidate_branch: codex/finance-uat-remediation-20261004
candidate_head: 7540f6ade
audit_start_commit: 089336fa9
base_commit: 415fe5fbd
target_ref: origin/master
depends_on: FIN-UAT-2026-10-04-A
migration_status: none
verification_status: passed
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/352
---

# Finance transaction, approval route, role, and permission audit

## Objective and scope

Build and verify the Finance authorization contract from workflow entity and lifecycle decision to API route, permission, seeded role, workflow stage, approval-workbench projection, and outcome owner. Classify every mismatch as a code defect, provisioning drift, data remediation, deliberate policy, or unresolved runtime evidence.

## Workspace and authorization boundary

- Repository: C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP
- Evidence worktree: .w/fin-uat-remediation-20261004
- Branch: codex/finance-uat-remediation-20261004
- Exact audit start: 089336fa9
- Authorized: repository inspection, read-only UAT database inspection, focused local implementation, tests, and this ledger.
- Not authorized: push, PR #352 mutation, deployment, service restart, migration application, or UAT data mutation.
- Subsequent authorization on 2026-10-05: push the committed remediation through PR #352, then deploy and restart only after a successful build and focused contract tests. UAT data mutation and migration application remain unauthorized.

## Authority model

Decision patterns used in the matrix:

| Code | Decision route and permission | Workbench/outcome owner |
| --- | --- | --- |
| G | POST /api/finance/approvals/{approvalId}/approve -> Finance.Workflow.Approve; reject -> Finance.Workflow.Reject | Generic Finance workbench and entity-specific outcome handler |
| JE | Journal-entry direct approve/reject routes -> Finance.JournalEntries.Approve; the assigned item can also be decided through G | Journal-entry service/outcome handler |
| JB | POST /api/finance/journal-batches/{id}/review-stage -> Finance.JournalBatches.Approve | Visible in queue; decision remains on batch detail because it carries per-entry decisions and control totals |
| RJ | Recurring template/occurrence direct approve/reject routes -> Finance.Workflow.Approve/Finance.Workflow.Reject; G is also supported | Recurring service or generic workbench outcome |
| AB | Accounting-book initialization/period/book transition approve/reject routes -> typed accounting-book approval permission | Visible in Finance inbox; generic action disabled; accounting-book service owns outcome |
| APX | POST /api/ap/vendor-invoices/match-exceptions/{id}/decision -> AP match-exception decision permission | Dedicated AP exception service; not a generic workbench action |
| SDN | POST /api/ap/supplier-debit-notes/{id}/approval -> Finance.AP.SupplierDebitNotes.Approve | Dedicated supplier-debit-note service; not a generic workbench action |
| BANK | Banking deposit/returned-cheque approve/reject routes under /api/finance/banking -> typed deposit/returned-cheque permission | Dedicated banking service; not a generic workbench action |
| BP | Business-partner detail/status route and its typed permission | Visible in Finance inbox; Procurement adapter owns outcome |

Stage profiles:

| Code | Ordered workflow stages and baseline roles |
| --- | --- |
| F3 | Accounts Officer Review: Accounts Officer or Senior Accountant -> Finance Manager Approval: Finance Manager -> Financial Controller Final Approval: Financial Controller |
| P2 | Finance Manager Approval: Finance Manager -> Financial Controller Final Approval: Financial Controller |
| C1 | Single Financial Controller approval |
| CH1 | Single Chief Accountant approval |
| BP | Finance Manager / Financial Controller / Manager / Super Admin, as defined by the Business Partner workflow |

The role seed grants Accounts Officer and Senior Accountant Finance.Workflow.Approve and Finance.Workflow.Reject. Accounts Officer also has Finance.JournalBatches.Approve. Finance Manager and Financial Controller have the typed and generic approval grants used by their stages. Workflow assignment remains the resource-level check after the permission gate.

## Complete Finance workflow authority matrix

Starter means a repository submission path calls the workflow service for the entity. G outcome means the generic workbench has a terminal entity handler for both approval and rejection.

| Domain | Workflow entity | Decision | Stage | Starter | Queue/action/outcome | Audit result |
| --- | --- | --- | --- | --- | --- | --- |
| GL | JournalEntry | JE + G | F3 | Yes | Yes / G outcome | Aligned |
| GL | DeltaAdjustmentJournal | JE + G | C1 | Yes | Yes / G outcome | Fixed: added missing workbench key and shared journal outcome |
| GL | JournalBatch | JB | F3 | Yes | Visible / detail action / batch service | Deliberate detail-only decision; UAT runtime denial remains unresolved |
| GL | AccountingBookInitialization | AB | C1 | Yes | Visible / dedicated action | Aligned |
| GL | AccountingBookPeriodLifecycle | AB | C1 | Yes | Visible / dedicated action | Aligned |
| GL | AccountingBookLifecycle | AB | C1 | Yes | Visible / dedicated action | Aligned |
| GL | RecurringJournalTemplate | RJ | C1 | Yes | Yes / G outcome | Fixed permission, critical provisioning, atomic submit, and orphan reconciliation |
| GL | RecurringJournalOccurrence | RJ | C1 | Yes | Yes / G outcome | Fixed permission and critical provisioning; processor already retries submission failures |
| GL | RecurringJournalOccurrenceWaiver | RJ | C1 | Yes | Yes / G outcome | Fixed permission and critical provisioning |
| AP | FinancePurchaseOrder | G | F3 | Yes | Yes / G outcome | Aligned |
| AP | FinancePurchaseOrderReceipt | G | F3 | Yes | Yes / G outcome | Aligned |
| AP | VendorInvoice | G plus typed AP route | F3 | Yes | Yes / G outcome | Aligned |
| AP | VendorInvoiceMatchException | APX | P2 | Yes | Dedicated / dedicated outcome | Deliberate domain-owned decision |
| AP | VendorPayment | G plus typed AP route | P2 | Yes | Yes / G outcome | Aligned |
| AP | PaymentBatch | G plus typed AP route | P2 | Yes | Yes / G outcome | Fixed missing rejection outcome; rejection now cancels batch and records reason |
| AP | SupplierDebitNote | SDN | F3 | Yes | Dedicated / dedicated outcome | Deliberate domain-owned decision |
| AR | Quote | G | F3 | Yes | Yes / G outcome | Aligned |
| AR | SalesOrder | G | F3 | Yes | Yes / G outcome | Aligned |
| AR | Invoice | G | F3 | Yes | Yes / G outcome | Aligned |
| AR | ReturnOrder | G | F3 | Yes | Yes / G outcome | Aligned |
| AR | CreditNote | G | F3 | Yes | Yes / G outcome | Aligned |
| AR | CustomerPayment | G | F3 | Yes | Yes / G outcome | Aligned |
| AR | Refund | G | F3 | Yes | Yes / G outcome | Aligned |
| Budget | BudgetScenario | G | F3 | Yes | Yes / G outcome | Aligned |
| Budget | BudgetReturn | G | F3 | Yes | Yes / G outcome | Aligned |
| Budget | BudgetRevision | G | F3 | Yes | Yes / G outcome | Fixed missing seed specification |
| Budget | FinanceBudgetOverride | G | F3 | Yes | Yes / G outcome | Aligned |
| Unit accounting | UnitJournalEntry | G | F3 | Yes | Yes / G outcome | Aligned |
| Unit accounting | UnitAccountBudget | G | F3 | Yes | Yes / G outcome | Aligned |
| Allocation | AllocationRule | G | F3 | Yes | Yes / G outcome | Aligned |
| Allocation | AllocationRunBatch | G | F3 | Yes | Yes / G outcome | Aligned |
| Cash/bank | CashTransaction | G | F3 | Yes | Yes / G outcome | Aligned |
| Cash/bank | BankReconciliation | G | F3 | Yes | Yes / G outcome | Aligned |
| Migration | OpeningBalanceBatch | G | F3 | Yes | Yes / G outcome and explicit detail link | Aligned |
| FX | ExchangeRate | G | F3 | Yes | Yes / G outcome and explicit detail link | Aligned; prior atomic/orphan remediation retained |
| Fixed assets | FixedAsset | G | F3 | Yes | Yes / G outcome | Aligned |
| Fixed assets | FixedAssetDepreciationRun | G | F3 | Yes | Yes / G outcome | Aligned |
| Fixed assets | AssetValuation | G | F3 | Yes | Yes / G outcome | Aligned |
| Fixed assets | AssetTransfer | G | F3 | Yes | Yes / G outcome | Aligned |
| Fixed assets | AssetDisposal | G | F3 | Yes | Yes / G outcome | Aligned |
| Fixed assets | AssetVerificationSession | G | F3 | Yes | Yes / G outcome | Aligned |
| Fixed assets | CapitalProject | G | F3 | Yes | Yes / G outcome | Aligned |
| Leases | LeaseContract | G | F3 | Yes | Yes / G outcome | Aligned |
| Banking | BankDepositBatch | BANK | CH1 | Yes | Dedicated / dedicated outcome | Deliberate domain-owned decision |
| Banking | ReturnedChequeCase | BANK | CH1 | Yes | Dedicated / dedicated outcome | Deliberate domain-owned decision |
| Master data | BusinessPartner | BP | BP | Yes | Visible / detail action / adapter outcome | Deliberate cross-module ownership |

## Explicit exclusions and retired routes

| Entity | Evidence | Classification/action |
| --- | --- | --- |
| SupplierReturn | Seeder explicitly retires active SupplierReturn definitions while FIN-INT-012/013 remain quarantined | Deliberate quarantine. Removed stale generic workbench key and unreachable outcome branches. SupplierDebitNote remains the Finance-owned AP correction. |
| DeliveryNote | A Finance workflow seed and queue key existed, but no Finance submission starter exists; delivery lifecycle is owned by the delivery service | Code/catalogue defect. Removed stale seed/workbench key and added idempotent retirement of legacy active definitions rather than inventing approval authority. |
| AssetDepreciationSchedule | Queue alias and outcome alias existed, but no workflow seed or starter exists; the real entity is FixedAssetDepreciationRun | Code/catalogue defect. Removed stale alias from queue, fact projection, and outcomes. |
| AccountingBookApplicability | Seeder retires these workflows; resolution is a typed policy operation rather than approval workflow | Deliberate retirement; excluded from queue matrix. |

## Mismatch register

| ID | Mismatch | Classification | Resolution/status |
| --- | --- | --- | --- |
| AUTH-01 | Recurring direct decisions used Finance.JournalEntries.Approve while the stage is governed by workflow permissions; convention mapping fell back to an unrelated write policy | Code defect | Fixed: explicit and convention policies use Finance.Workflow.Approve/Finance.Workflow.Reject |
| AUTH-02 | Critical startup seeded ExchangeRate only; three recurring workflow types were absent in exact UAT | Provisioning drift caused by code gap | Fixed for future startup. No UAT mutation performed. |
| AUTH-03 | Recurring template status was saved before workflow creation, allowing an orphan | Code defect | Fixed: state, workflow instance, and audit share one relational transaction and require an instance ID |
| AUTH-04 | UAT has one PendingApproval recurring template with no workflow history | Data remediation | Safe startup reconciliation added; only pending rows with no history and a durable initiator are repaired. Not run against UAT. |
| AUTH-05 | BudgetRevision starts workflow but had no Finance seed specification | Code/provisioning defect | Fixed: added to Finance workflow catalogue and critical existing-tenant convergence |
| AUTH-06 | DeltaAdjustmentJournal starts and is seeded but was absent from workbench/outcomes | Code defect | Fixed: queue/action coverage and shared journal outcomes |
| AUTH-07 | PaymentBatch had approval but no rejection outcome | Code defect | Fixed: rejection sets Cancelled, clears approval evidence, and appends reason |
| AUTH-08 | SupplierReturn generic branches contradicted explicit quarantine | Code defect | Fixed: stale queue/fact/outcome handling removed |
| AUTH-09 | DeliveryNote seed/queue entries had no starter | Code/catalogue defect | Fixed: stale seed and queue entry removed; legacy active definitions retire during critical convergence |
| AUTH-10 | AssetDepreciationSchedule alias had no seed or starter | Code/catalogue defect | Fixed: alias removed; FixedAssetDepreciationRun remains |
| AUTH-11 | Journal Batch review returned ACCESS_FORBIDDEN for Accounts Officer UAT user | Unresolved runtime evidence | Database authority is consistent. Do not change code without rejected request claims/token or authorization trace. |

## Journal Batch UAT evidence

Read-only SQL against RHEMAERP_BOOKV2_UAT_20260922 established:

- accounts.officer (8e55b5f5-a2fe-4a40-0ce5-08df1c7ae08f) is active in tenant 00000000-0000-0000-0000-000000000001;
- the active role is Accounts Officer;
- effective grants include Finance.JournalBatches.Approve, Finance.Workflow.Approve, and Finance.Workflow.Reject;
- batch JB-2026-00001 is PendingApproval;
- current step is Accounts Officer Review;
- pending WorkflowApproval has ApproverRole = Accounts Officer; and
- the user/role assignment matches the pending approval.

The persisted route -> permission -> role -> stage chain is valid. ACCESS_FORBIDDEN is most likely request authentication/claims/runtime-context drift. This is an inference from database evidence, not yet a reproduced root cause.

## Changed files and safe fixes

- src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs
- src/ErpSystem.Api/Controllers/Finance/RecurringJournalController.cs
- src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs
- src/ErpSystem.Api/Services/DatabaseSeedingService.cs
- src/ErpSystem.Api/Services/Finance/GL/RecurringJournalService.cs
- src/ErpSystem.Api/Services/Finance/GL/RecurringJournalWorkflowReconciliationService.cs
- src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs
- src/ErpSystem.Api/Program.cs
- tests/ErpSystem.Api.Tests/Services/Finance/FinanceApprovalAuthorityContractTests.cs
- tests/ErpSystem.Api.Tests/Services/Finance/RecurringJournalWorkflowReconciliationTests.cs
- tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj
- this ledger

## Migrations and application state

- Schema migrations: none.
- UAT data changes: none.
- Startup reconciliation implemented but not run against UAT.
- Deployment/service restart: not performed.

## Completed local commit

- 7540f6ade - fix(finance): align approval authority contracts
- The coordination-ledger update that records this result is a documentation-only successor commit.

## Verification evidence

- Read-only UAT SQL evidence: complete, as recorded above.
- dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore: passed with 0 errors (43 pre-existing warnings).
- Test assembly build with --no-dependencies after the assertion correction: passed with 0 errors (42 pre-existing warnings).
- FinanceApprovalAuthorityContractTests plus RecurringJournalWorkflowReconciliationTests: passed 21/21.
- Existing FinanceRouteContractTests: passed 17/17.
- git diff --check: passed.

### Post-push missing-type report triage

- A later local build report showed CS0234 at `Program.cs` and `ServiceCollectionExtensions.cs` for `ErpSystem.Api.Services.Finance.GL.RecurringJournalWorkflowReconciliationService`.
- The implementation file is tracked in both local and remote PR head `348d8a37b85de2543d15c274b1fd809b426b19ac`, and its declared namespace and public type exactly match both references.
- `ErpSystem.Api.csproj` uses the SDK default compile-item rules and does not exclude the implementation file.
- A full rebuild of `src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore` from that exact head passed with 0 errors. The focused approval-authority and reconciliation suite passed 21/21, and the reconciliation tests directly construct the reported service type.
- GitHub checks on that exact head passed: `Finance consumer and SQL Server contracts` and `Validate Windows VPS release contracts`.
- Classification: stale or incomplete local build snapshot, not a committed source/namespace mismatch. No corrective source change is warranted.
- Deployment and restart were not performed: the repository's Windows test-VPS workflow rejects non-`master` deployment candidates, while PR #352 remains open. Bypassing that release guard or merging the PR was not inferred from the deployment request.

## Remaining work

The safe local code work is complete. For AUTH-11, capture the rejected Journal Batch request authenticated user ID, tenant claim, role claims, permission claims, route/action, HTTP status/body, correlation ID, and matching authorization log. This needs a deployed/runtime evidence pass and is not authorization to deploy, restart, or mutate UAT.

## Authorization boundaries retained

Push/update of PR #352 is authorized. Deployment and restart are authorized only through the repository's guarded release path after a green build; the current PR branch is not an eligible deployment candidate. Do not merge PR #352, bypass release guards, apply migrations, or mutate UAT data without separate authorization.

## 2026-10-05 local exchange-rate runtime follow-up

- The frontend production server was confirmed running from .w/fin-uat-remediation-20261004/frontend.
- The local API process started at 07:22, while the remediation API DLL in that worktree was rebuilt at 07:34. The process therefore retained the older assembly and could not expose the committed exchange-rate queue/reconciliation changes.
- Local testing does not require merging PR #352 to master; the master restriction applies to the guarded UAT deployment only.
- The user authorized manually restarting the local API from the remediation worktree. Codex did not restart it.
- Directly changing exchange-rate approval-status columns remains unsafe and unauthorized because it bypasses workflow history, approval evidence, and schedule lifecycle.
- Exchange-rate dialog Cancel and in-flight submission defects are tracked separately in FINANCE_EXCHANGE_RATE_DEMO_BLOCKERS_20261005.md.
- The corrective UI and AP/AR tax-persistence set is committed as 3f310872d.
