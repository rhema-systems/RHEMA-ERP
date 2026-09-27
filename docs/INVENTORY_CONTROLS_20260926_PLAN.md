# Inventory controls and accounting implementation

Baseline: `b8307938eb` (deployed master), branch `codex/inventory-controls-and-accounting-20260926`.
Current continuation: `codex/inventory-workflows-20260927`, based on `1413bb6fa5ac2a6ec20637ceb03416669a333e63`. The baseline analysis below is retained; the verification checkpoints record subsequent work.
Scope authority: [original requirements](INVENTORY_CONTROLS_20260926_REQUIREMENTS.txt). All 17 requirements and the original acceptance scenarios remain in scope. This document records the required initial analysis before implementation. Analysis is code inspection, not acceptance evidence.

## Existing architecture

The domain services own transactions, tenant checks, workflow, audit and source identity. Inventory valuation owns cost layers, balances and authoritative InventoryMovement entries; existing services also maintain WarehouseQuantity, InventoryLocation and legacy StockMovement projections. Finance uses canonical posting events/journals and, for disposal, independent producer/checker controls. New features must use those owners together rather than write isolated balances or journals.

Requisitions issue through `InventoryRequisitionService.IssueAsync`, creating immutable Store Issue Voucher lines, source inventory movements and Finance lineage. Acknowledgement currently changes only voucher status/comment: it is custody confirmation, not a second inventory receipt. Therefore actual acknowledgements must not issue stock again or restore shortages to a store. Returns use their separate governed process.

Transfers submit and approve through configured workflow, then dispatch through InventoryValuationService and receive retained carrying value. Dispatch reduces source stock and increases source allocated quantity; receipt transfers carrying value to the destination. Existing transfer actions, discrepancy cases and reversal paths provide lineage. `WarehouseLocation.IsInTransitLocation`, `WarehouseLocationType.InTransit`, and `InventoryTransfer.InTransitLocationId` already exist but are not wired to transfer movements.

Physical counts have immutable action history, reviewed uploads in DMS, row versions, per-count locks, freeze guards, and shared StockAdjustment/Finance posting. They currently have one user counter and one whole-count approval/adjustment lifecycle. Recount overwrites original values; this is insufficient for independent immutable addenda.

Supplier invoices already retain quantity-level GRN receipt allocations and original posting-account authority. The distribution preview shares the posting builder. Supplier return dispatch uses `SupplierDebitNoteService.InventoryReturns`, not the older fail-closed adapter. Disposal already uses shared adjustments and governed Finance intents. Canonical non-supplier invoices are Finance AR `Invoice` records; Procurement Supplier Invoices are AP and are not the auction destination.

Notifications already queue Notification records for asynchronous delivery. Reuse this queue with durable event deduplication. External supplier tickets have a separate controller/service entry point; internal tickets and Estate property enquiries must remain distinct.

## Requirements and implementation checklist

No row is complete until its service, API, UI, SQL and acceptance gates pass where applicable.

| Req | Current gap and implementation | Principal existing owner | Status |
| --- | --- | --- | --- |
| 1 | Add explicit per-line actual receipts, cumulative/outstanding quantities, repeated partial acknowledgements, receiver security and replay protection. Preserve issued lines and historical acknowledgements. | InventoryRequisitionService; IssueRequisitionDialog; InventoryIssueVoucher entities/configuration | In progress: implementation and migration present; 17 service tests, model parity, 21 SQL guard checks and isolated full-schema upgrade pass; 9 SQL concurrency checks and full 0-to-60 fresh installation also pass; HTTP/UI lifecycle acceptance pending |
| 2 | Remove request/approval bin requirement; capture validated source picks at issue and destination bin at receipt. Preserve partial dispatch. | InventoryTransferService, valuation Transfers, Transfer/Ship/ReceiveTransferDialog, SQL lifecycle guards | Implemented; focused backend/frontend tests and 39 isolated SQL checks pass; combined migration passed; live lifecycle pending |
| 3 | Canonical supplier BusinessPartner carrier selector/FK plus historical name snapshot in both shipping paths. | Transfer shipping DTO/service and ShipTransferDialog | In progress: canonical carrier selection and immutable dispatch snapshots implemented; integrated verification pending |
| 4 | Activate system-managed transit location with balanced physical/carrying-value legs, shipment/container lineage and ledger-backed report; reconcile historical open transfers explicitly. | InventoryTransferService and InventoryValuationService | In progress: physical transit legs, ordinary-operation guards and ledger report implemented; migration/valuation acceptance and explicit legacy reconciliation remain pending |
| 5 | Multiple Employee assignments, authorized lookup, active committee access, audited draft edits, queued deduplicated notifications. | PhysicalCountService; Employee/ApplicationUser; NotificationTopicPublisher | Implemented; committee recovery SQL checks 35/35, UI tests 12/12 and rebuilt count/recovery families 126/126 pass; combined migration passed; live checks pending |
| 6 | Independent original/addendum sheet snapshots, selective recount flags/reasons, repeatable approval, root-line adjustment claims, freeze retention. | PhysicalCountService.Controlled/Review; StockAdjustmentService; DMS sheet lineage | Implemented; isolated count SQL checks pass. The legacy investigation recovery gap is corrected and covered by the rebuilt 126/126 count/recovery tests; combined migration passed; live acceptance pending |
| 7 | Informational defective quantity/comment across manual entry, XLSX, upload grid and audit; validate against physical quantity without affecting variance. | PhysicalCountItemsGrid; PhysicalCountSheetReader; physical-count-sheet.ts | Implemented; focused backend/frontend checks pass; combined migration passed; live acceptance pending |
| 8 | Tenant PPV/revalue policy and immutable receipt/invoice cost-difference allocation; clear GRNI at original basis; eligible remaining-stock revaluation through valuation service. | ProcurementSettings; VendorInvoiceService; existing item PPV account; valuation owner | Implemented; focused lifecycle and 31 isolated SQL checks pass; explicit decimal precision correction, combined migration passed; live acceptance pending |
| 9 | Quantity-level uninvoiced/invoiced return allocation, multiple invoice notes, original tax/account/FX lineage, synchronized invoice/return locks. | PurchaseReturnService; SupplierDebitNoteService.InventoryReturns | Implemented; focused lifecycle and 33 isolated SQL checks pass, including competing returns; combined migration passed; live acceptance pending |
| 10 | Select all filtered/clear/manual choices; fix mixed-warehouse eligibility and stale selections. | inventory/warehouse-items page | Implemented: bulk filtered selection and stale selection handling; 6 focused frontend tests pass; live acceptance pending |
| 11 | Match UI capabilities to server permission and expose ProblemDetails; reproduce actual 403 and correlate access decision before changing grants. | WarehouseItemsController; ProcurementAccessControlService | Read-only runtime verification confirms administrator lacks the required warehouse-assignment capability; original reported request has no matching event. UI gating implemented; authorized Stores Manager and denied administrator HTTP checks pending |
| 12 | Stage-appropriate non-sales waybill and idempotent auction canonical AR draft with buyer BP and disposal lineage. | InventoryDisposalService; document infrastructure; Finance InvoiceService | Implemented; focused tests and fixture PDF rendering pass; combined migration passed; live acceptance pending |
| 13 | Reject new Sales disposal while preserving history; canonical Sales invoice integration/distribution preview with read-only cost quote. | InventoryDisposalService; SalesOrderService; Finance InvoiceService | Implemented; focused tests and 35 isolated Sales SQL checks pass; combined migration passed; live acceptance pending |
| 14 | Item disposal posting account, original-cost debit and proceeds credit through existing adjustment/AR/payment owners; shared previews. | InventoryItem accounts; StockAdjustmentValuationIntentBuilder; Finance AR/payment | Implemented; producer/account resolution and rollback checks pass; combined migration passed; live acceptance pending |
| 15 | Supplier category first; server resolves category/ancestor AppliesToType before SLA/workflow. | EhcTicketService.CreateExternalTicketAsync; external-portal ticket form | Implemented; 19 policy tests pass; authenticated/tampered HTTP acceptance pending |
| 16 | Hide supplier priority and enforce existing Medium/active-priority fallback on server; retain internal priority. | External ticket entry point and priority configuration | Implemented; same pending policy and live acceptance gates as requirement 15 |
| 17 | Force Website (`Web`) server-side for supplier tickets; hidden/read-only UI; preserve internal/Estate behavior. | External ticket entry point | Implemented; same pending policy and live acceptance gates as requirement 15 |

## Exact bin guards and required changes

`InventoryTransferService.SubmitForApprovalAsync` currently rejects every active line without both SourceLocationId and DestinationLocationId. Create/update/add/update-line paths additionally enforce inter-bin locations. `InventoryTransferService.Valuation.cs` correctly requires an exact source bin at dispatch and destination bin at receipt. `InventoryValuationService.Transfers.cs` binds valuation to the line's recorded bin and action identity; looping multiple bins under one existing action would currently fail its duplicate guard.

`TR_InventoryTransfers_ControlledLifecycle` also checks draft-line completeness; its consolidated definition and the `InventoryTransferDraftLineCompletionGuard` patches are in `ArchivedGovernanceBaselineSql.cs`. New migrations must change the live trigger and EF model together. Do not edit archived migrations to implement a new deployment. Source-pick allocations need explicit action/line/bin lineage and valuation identities; nullable request fields alone do not solve this requirement.

## Database and compatibility strategy

Use additive typed relationships for receipt lines, counter assignments, sheet/addendum snapshots, adjustment claims, invoice variance evidence, return allocations and disposal-to-AR linkage. Add only missing account/carrier/policy fields. Preserve immutable posted documents and snapshots. Existing PPV account fields and transit location fields are reused.

New migrations must include model snapshot changes, indexes/unique replay/source claims, tenant-safe foreign-key validation and updated lifecycle/append-only/freeze guards. Inspect both fresh migration and upgrade paths on an isolated copy. Never rerun historical stock issues, silently fabricate receipt allocations, or recalculate posted GL history. Legacy acknowledgement interpretation must be explicit; historical unallocated invoice/return/transit cases need actionable reconciliation instead of guessed costs.

## Accounting and movement controls

- Acknowledgement records physical custody only in the current requisition architecture. Source issue postings remain unchanged.
- Transfers conserve company quantity/value across source, transit and destination. Partial receipts retain unresolved transit. Transit cannot be picked through normal selectors or mutated by ordinary adjustments.
- Recounts exclude flagged root lines from the passed sheet's adjustment. A unique root-line claim prevents another key/addendum from adjusting the same variance twice. Retain freezes on unresolved lines.
- Invoice PPV separates invoice commercial net value, original receipt accrual basis, tax and FX. Revalue only evidenced eligible stock/layers; consumed-stock differences use configured variance treatment.
- Uninvoiced returns reverse GRNI; invoiced portions use governed AP debit notes and return clearing, preserving independent approval. No AP credit is claimed merely because stock was dispatched.
- Disposal removes stock once, debiting item disposal account. Auction AR lines must be non-stock lines to avoid a second issue. AR credits the disposal account; payment clears AR through the selected cash/bank. Disable the new auction path's old direct-proceeds posting to avoid duplicate proceeds.

## Security, workflow, notification and audit

Retain active tenant membership, DB-backed permissions, warehouse access, designated receiver, row versions and separation of duties. Workflow presence is evaluated/snapshotted through the existing optional-workflow architecture; no new unconditional approval requirement.

Counter assignments extend editing permission but must also extend self-approval exclusions. Employee lookup must not grant general HR access. Email-only employees and employees without a unique linked user require explicit delivery/access handling. Queue assignment/recount notifications transactionally with stable event identities; never SMTP inside a stock transaction.

Warehouse assignment requires `procurement.inventory.master-data.manage`; its current registry scope is unscoped and its seeded roles include Stores Manager and ICT Administrator. The updated UI checks that exact server capability before exposing or performing assignment mutations. The precise reported 403 remains unproven until an actual request/access audit is captured; do not fix it by weakening authorization.

Supplier helpdesk normalization belongs in the supplier entry point before SLA, audit and workflow. Existing category AppliesToType supports mapping; unmapped/disabled configuration must return actionable validation. Do not alter shared Estate enquiry behavior accidentally.

## Dependencies and risks

1. Transfer valuation currently retains in-transit value as outbound minus inbound movement evidence. Adding physical transit legs must revise this calculation without double counting.
2. Split-bin dispatch needs valuation identities below the existing transfer-line/action grain.
3. Physical count freeze and approval currently assume one aggregate/one adjustment. Selective addenda require a coherent new sheet lifecycle, not a visual filter.
4. Legacy JSON count import bypasses stronger counter/row-version checks used by XLSX; align every mutation path.
5. Receipt cost-layer descendants after transfers and issued/sold quantities constrain safe revaluation.
6. Supplier return schema currently binds one return to one original invoice/active note. Mixed allocations require additive schema and trigger changes.
7. SalesOrderService.GenerateInvoiceAsync currently throws NotImplementedException. Canonical AR posting exists, but Sales conversion is an explicit dependency to implement, not assumed functionality.
8. Disposal has current C7/C8/C9 producer/checker controls; never bypass them to make auction posting appear complete.
9. Existing unrelated untracked files are preserved. No deployment or shared database mutation is part of initial analysis.

## Execution and validation order

Complete and validate each workstream before unrelated implementation: (1) requisition acknowledgement; (2) transfer request/issue bins; (3) carrier; (4) transit; (5) counters; (6) selective recount/addenda; (7) defective quantities; (8) PPV/revaluation; (9) supplier returns; (10) warehouse assignment/403; (11) disposal/Sales/accounts; (12) external helpdesk.

For each workstream record exact focused test results, migration/SQL evidence, permission and tenant denial, replay/concurrent-write handling, and visible browser lifecycle evidence. Include workflow-present/absent behavior where relevant. Use existing records before generating new verification documents. SQL Server tests are required for database triggers and locking; InMemory tests cannot stand in for them. Run full backend analyzer build, frontend production build and relevant broader regression suites before final delivery. All original acceptance scenarios remain pending until evidenced; build success alone does not mark business acceptance complete.

## Requirement 1: SQL receipt concurrency evidence (2026-09-27)

Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/acceptance/Test-InventoryReceiptConcurrency.ps1` from the repository root. The harness creates a uniquely named isolated database, reads API user secrets in memory, installs the exact archived voucher lifecycle triggers and current receipt guards on minimal fixture tables, and retains the database plus SQL/JSON evidence. It neither opens nor changes the original database or the app verification clone.

The completed run used `RhemaERP_ReceiptConcurrent_20260927_082101_37703711`. Evidence is in `tmp/receipt-concurrency-20260927_082101_37703711.json` and the matching `.sql`. All nine checks passed: three trigger compilations, three overlapping two-session cases and three committed-state invariants. Each case required `sys.dm_exec_requests` evidence that session B was blocked by session A before A could commit; an elapsed delay alone does not pass the test.

| Concurrent case | Observed contention | Outcome after first receipt of 60/100 commits |
| --- | --- | --- |
| Second receipt of 60 | Sessions 61/62, `LCK_M_S` | SQL error 51632; one receipt remains, total 60, outstanding 40 |
| Same replay key | Sessions 62/61, `LCK_M_X` | SQL error 2601; one receipt/action remains, total 60 |
| Remaining receipt of 40 | Sessions 61/62, `LCK_M_S` | Both commit; two receipt actions, total 100, voucher acknowledged |

Each invariant also verifies the original issued quantity remains 100 and no Finance binding is introduced. Connections and transactions are disposed; the isolated database is retained for inspection. These tests certify the database receipt guards under real SQL Server contention, including defense below the service's per-voucher application lock. They do not execute HTTP or EF service requests and do not replace visible lifecycle acceptance. The separate full fresh installation subsequently passed all 60 migrations with an explicit 8 GiB subprocess heap limit; see `INVENTORY_RECEIPT_FRESH_INSTALL_ACCEPTANCE.md`. Requirement 1 remains in progress until HTTP/UI lifecycle acceptance passes.

## Live receipt acceptance prerequisites (2026-09-27)

Read-only inspection of the isolated running verification database found no Inventory items, warehouses, stock movements, warehouse quantities, requisitions or issue vouchers. Its eleven dedicated operational UAT accounts are also absent. This is not a populated receipt lifecycle fixture.

Reuse the existing `seed-operational-uat` command after configuring `UatBootstrap:SharedPassword` locally. The create-only reconciler provisions accounts, roles, responsibility scopes and Inventory masters without resetting existing passwords; it preserves existing workflow definitions and governed Finance mappings. The bootstrap secret must not be pasted into chat or included in logs. The user configured the secret, and the existing seeder completed successfully on the isolated application database at 09:32 UTC. It created 11 dedicated actors, 29 role assignments, 5 UOMs, 6 categories, 2 warehouses, 3 bins, 9 items, 1 missing supplier and 8 responsibility assignments. Read-only verification passed with no readiness failures; existing canonical supplier identities were retained. Evidence: `tmp/uat-20260927T093007/seed-result.json`. The API was not restarted.

Master-data seeding does not establish stock or certify acknowledgement. Prepare stock through its normal governed owner, then exercise issue, partial receipt, reload, final receipt, replay and over-receipt rejection through the visible UI/API. Verify resulting receipt evidence and unchanged issue/Finance postings. Do not fabricate posted vouchers with direct SQL or disable approvals to satisfy this gate.

Live prerequisite findings after seeding: the administrator has no effective Inventory warehouse responsibility, while `storesofficer` has the seeded warehouse scope but cannot open Finance's opening-balance workspace. Read-only role verification identifies the existing `ap.officer` as having both `Finance.Read` and `procurement.inventory.adjust.request`, with its existing DEMO-PM responsibility. This is an account/workspace prerequisite, not evidence that the receipt lifecycle passes. No role grants, stock or journals were changed to bypass it. The ordinary adjustment dialog also exposed `INITIAL_STOCK` although its endpoint rejects that reason; the option is now excluded from ordinary entry while historical display remains available.

## Transfer allocation implementation in progress (2026-09-27)

The current continuation branch is `codex/inventory-workflows-20260927`, based on merged `1413bb6fa5ac2a6ec20637ceb03416669a333e63`. Requesters no longer choose physical bins. New immutable dispatch picks and receipt allocations retain bin, carrier and carrying-value lineage. The ordinary stock/master-data owners reject system transit mutations; controlled transfer owners create source/transit/destination legs. Company valuation retains transit once, with the new ledger-backed `GET /api/inventory/transfers/transit-stock` report providing an informational subset and a separate legacy-reconciliation count.

The final focused frontend run passed 35 tests across five suites, and the narrow TypeScript check passed with zero diagnostics (tmp/inventory-transfer-frontend-final-tests.log and tmp/inventory-transfer-frontend-types.log). The backend analyzer build, additive migration, SQL guard/upgrade tests and real transfer walkthrough are pending. New report tests cover split-pick retained value, partial receipts and source returns, closed documents with physical stock outstanding, tenant/deleted/unposted exclusion and hidden unauthorized legacy evidence. These additions are not yet acceptance evidence until executed successfully.


### Live opening stock and requisition prerequisite update

Opening stock ADJ260001 (486da09b-8e8c-4a6d-9797-11ee9396187e) posted through the visible governed UI in the isolated verification database. Read-only SQL confirms 100 units at GHS 840 and journal 78eafcb5-1477-474e-a709-53efc4f36179 with two lines, debit 840 and credit 840. The original attempt to create a requisition exposed a frontend/backend contract mismatch: the form sent legacy DepartmentId while the owner requires OrganizationUnitId. The form now reuses the HR organisation-unit picker and sends the typed identity, retaining legacy names for historical display. The API's organisation-unit validation is unchanged. Live receipt acceptance remains pending.

### Warehouse assignment capability evidence (2026-09-27)

Read-only inspection of `RHEMA-MICHAEL\SQL2017 / RhemaERP_ReceiptUpgrade_20260927_023608_918225d0` verified the current `admin` actor (`df6393d9-3aad-4a02-b37f-08df1bec46e0`) is active and has an active, nondeleted, unexpired DEFAULT tenant membership. Its Security roles are `SuperAdmin` and `TDC_SUPERVISING_QUANTITY_SURVEYOR`. The live `procurement.inventory.master-data.manage` permission is granted to `TDC_STORES_MANAGER` and `TDC_ICT_ADMINISTRATOR`; neither is held by this actor, so its matching grant count is zero.

`WarehouseItemsController.HasMutationPermissionAsync` enforces this permission for assignment, metadata update and removal. `ProcurementAccessControlService` resolves effective Security roles from the active tenant membership and does not add a SuperAdmin bypass. The current actor therefore cannot pass this capability. This explains a possible current denial without proving the user's earlier request: the database contains **zero** `WarehouseItemAssignment` control events. Four observed denied events at 11:09 UTC concern `InventoryIssueAccountingRule`, a different endpoint, and must not be presented as warehouse-assignment HTTP evidence. No grants or application data were changed during this inspection.

The assignment UI now gates mutations on the same capability, supports filtered bulk/manual selection and removes stale selections. Its six focused tests passed, and the narrow TypeScript check passed with zero diagnostics (`tmp/warehouse-assignment-tests.log`, `tmp/warehouse-assignment-types.log`). Live acceptance remains pending: use the existing Stores Manager account to demonstrate an allowed assignment, verify the administrator cannot mutate, and correlate the actual warehouse-assignment response with its access event. Preserve server authorization and existing grants.


### Consolidated verification checkpoint (2026-09-27 12:15 UTC)

Normal Core build passed with zero errors (513 warnings). The combined API diagnostic build uses the explicit fast-EF path to find source errors; it is not release/migration acceptance. Complete generated migration metadata exhausted the earlier 4/8/12 GiB compile attempts. The next normal build changes compiler concurrency/debug/optimization settings while retaining metadata and SDK/EF analyzers. No live API binary has been replaced with a diagnostic build.

Isolated SQL checks now include transfer allocation concurrency (39 checks), selective-count/addendum freezes and competing root-line claims (17 checks), supplier-return allocations/ledger seals (24 checks), and disposal auction/staged-adjustment guards (23 checks). Detailed evidence lives in the workstream docs and `tmp` acceptance JSON. These fixtures never changed app stock, GL, permissions or approval periods and do not substitute for the pending full migration and authenticated UI workflows.

The browser is currently at Sign In after its session was terminated by another login; neither server was restarted. The outer September fiscal period was opened through the authorized administrator UI, but the BASE book's September period and exact independent approver configuration remain pending. The existing opening stock and approved requisition must be reused for the final 92 + 8 receipt walkthrough.

## Integrated checkpoint (2026-09-27, still in progress)

- Branch is based on current `origin/master` (`1413bb6f`); a fresh fetch reported no divergence.
- Combined diagnostic Core/Data/API build passed with zero errors. It uses the explicit fast-EF contract path and is not a releasable migration/runtime build. The full metadata/analyzer build is running separately with serialized compiler work.
- Focused Core checkpoint: 197/199 passed; the two legacy no-approval count behavior regressions were corrected after that captured binary and require rerun.
- Focused API checkpoint: 285/302 passed. Failures identified a real disposal draft-line tracking bug (corrected), canonical partner/tenant fixture mismatches, a historical auction fixture that needed explicit version-0 authority, and a missing SQLite date function. Corrections require rerun; failed cases are not treated as accepted.
- Separate SQL Server fixtures passed transfer 39 checks, count 25, mixed returns 31, invoice cost differences 20, disposal 28, and Sales source protection 32. These are exact helper/locking checks, not full migration or visible business lifecycle acceptance.
- Return UI 26 tests passed; Sales UI 7 tests passed; disposal UI 22 tests passed. Each area also passed its targeted TypeScript checks. Full frontend production verification remains outstanding.
- Live fixture remains the existing approved requisition `REQ-20260927-0001` and opening stock `ADJ260001`; do not recreate either. Issuing and the 92+8 receipt walkthrough remain pending the posting-period workflow/accounting prerequisites and rebuilt application.
- Remaining delivery gates: full normal build, one additive combined migration with fresh/upgrade checks, corrected backend reruns, complete frontend build, and visible permission-aware business lifecycles for each affected workstream. No deployment/merge or all-complete claim is supported yet.

### Follow-up regression evidence (2026-09-27 13:30 UTC)

- Final diagnostic API build: zero errors, 662 warnings (`tmp/inventory-final-fast-build.log`). This still uses fast-EF and is not the release build.
- Combined Core tests: 273/273 passed (`tmp/inventory-workflows-final-tests/core-final.trx`). Supplemental Core families: 231 passed; 16 older requisition fixtures used Department instead of the already-required HR OrganizationUnit. Their fixtures were corrected with original valuation, stock and history assertions preserved; the complete affected requisition family then passed 34/34 (`core-corrected-families.trx`).
- Combined API tests: 313/314 passed. Supplemental API families: 278/286 passed. The corrected disposal relational rollback, supplier-return group read and receipt distribution families then passed 28/28 (`api-corrected-families.trx`). The sole remaining earlier API failure is a migration-discovery assertion requiring the full assembly; it must run after the normal build.
- Frontend: all 240 tests across 24 files passed (`tmp/inventory-frontend-regression.log`). One combined TypeScript check covering all changed frontend files passed with zero diagnostics (`tmp/inventory-all-changed-types.log`). Three invalid Testing Library option types were corrected without changing matching semantics.
- Disposal waybill: the controller-generated one-page A4 PDF was rendered and visually inspected; no clipping or unreadable fields. This is fixture output, not live business acceptance.
- Normal compiler work now preserves all 30 complete migration/snapshot models, 116,008 ordered statements and their discovery attributes in generated intermediate copies. The first transformation build exposed a duplicate snapshot path and stopped; corrected item removal is under verification. Original historical migration sources remain unchanged. Full build, semantic-model comparison and migration installation remain gates, not assumed successes.

The latest-outcome union of the six backend TRX reports contains 1,118 passed cases and one outstanding migration-discovery assertion. Three requisition test-method names changed from Department to OrganizationUnit with their fixtures; those identities are explicitly normalized in `tmp/inventory-latest-test-outcomes.json` so obsolete failed names are not counted twice. This aggregate covers the focused and supplemental families, not the entire repository test suite.

### Complete migration build restored

The final shared-statement transformation passed the normal Data/API build in 6m46s with zero errors and 149 warnings, retaining SDK/EF analyzers and all current migration metadata (`tmp/inventory-workflows-normal-shared-build.log`). It retains all 116,008 ordered statements across 30 models, compiling 8,277 byte-identical unique statements in two checked lookup contexts. It does not edit historical source files.

An independent compiler check constructed EF models from the untouched snapshot and the generated snapshot/shared helpers. Their complete 9,321,423-character model representations matched exactly: SHA256 `E54EE9834ABA70A9E814DD8824D9FD708582A09ED2EF4F41D5009985576FDE40` (`tmp/migration-model-snapshot-equivalence.log`). Full Core and API test projects also compile with zero errors; complete test execution, additive migration, fresh/upgrade installation and frontend production build remain in progress.

### Full-suite follow-up

The complete Core run finished with 3,520 passed, 75 failed and 35 skipped (`core-full.trx`). The full API attempt recorded 2,180 passed, 218 failed and 35 skipped before the coordinator stopped its owned test host after observed memory exhaustion under the 3 GiB cap (`api-full.trx`). The API result is an aborted partial run, not a completed suite. Four captured failures explicitly report `OutOfMemoryException`; fresh bounded test processes are required to separate those from application behavior.

The two `INVENTORY_FULL_SUITE_BASELINE_20260927_*` documents retain failure triage and source comparisons. Source equality with HEAD does not by itself certify no regression. In-scope corrections include a nullable legacy adjustment key, governed recovery for submitted legacy counts awaiting recount, disposal-account catalogue expectations and a moved transfer guard source assertion. Model-parity checks require the rebuilt combined migration. All remain distinct from the earlier passing focused checkpoints and pending visible business acceptance.

Fresh-process API follow-up passed 38/40 cases, including all four previously OOM-affected cases; the remaining two unchanged Finance fixtures attempt to mutate a persisted account tenant key. Updated count-page tests pass 48/48, committee UI tests pass 12/12, and the combined changed-file TypeScript check again passes. The expanded isolated count SQL harness passes 35/35, including governed legacy recovery and immutable original observations. Rebuilt C# recovery/return-currency tests and combined migration acceptance remain pending.

### Resumed verification checkpoint (2026-09-27 18:30 UTC)

The rebuilt count/recovery service families passed **126/126** (`tmp/inventory-workflows-final-tests/core-final-recovery.trx`). This includes the governed legacy investigation recovery path; the original observations remain immutable. All newly added C# test files are now explicitly included in the normal test projects, which disable default compile items.

The combined migration is `20260927141036_InventoryControlledWorkflowsAndAccounting`. Its final normal build passed with zero errors and 149 warnings before the session gap (`tmp/inventory-workflows-final-normal-build.log`). After resuming, API output corruption was detected, so that historical pass is not being treated as proof of a currently runnable API. Core/Data and runtime dependencies passed metadata validation; an API-only normal rebuild is in progress before migration discovery, fresh installation and populated-copy upgrade verification. The original application database remains at migration 60.

The existing verification servers have been restored using the previously verified API. The user restarted the installed ClamAV scanner; `/health` confirms the database, scanner and application startup are healthy. Memory is reported as degraded while build jobs run. A separate production frontend build and the final Core model-parity tests are running. Browser lifecycle checks still require reattaching to the restored app and completing the existing Finance period prerequisites; no issue or receipt has been fabricated to bypass them.

The API-only recovery build subsequently passed with zero errors and 40 warnings. Its implementation/reference artifacts pass load validation. A damaged Data copy in the API output was restored from the validated exact final-build intermediate assembly; the migration sources were not rebuilt or changed to work around the failure. The final Core model/stock-attribution families passed **49/49** in 8m9s (`core-final-models.trx`), using the correct final Data assembly. A bounded independent source review found no confirmed introduced financial/tenant defect in Sales invoices, disposal auction linkage, receipt revaluation and mixed returns; this review is not a substitute for the remaining migration and live acceptance checks.

The final API accounting/model families passed **151/151** in 7m27s (`api-final-accounting-models.trx`), including retained original book/currency, canonical Sales invoice posting, supplier cost differences, precision and complete migration discovery. EF separately reported no pending model changes and discovered all 61 migrations.

The staged production frontend build completed successfully, exit 0, with nonempty `BUILD_ID` **QpaeO2rlm3hlLrijXnyIu** (`tmp/inventory-frontend-production.log`). Its build-generated configuration changes stayed inside the isolated build stage. TypeScript and frontend test results above are separate gates because the production configuration skips those checks.

The first full 60-to-61 rehearsal caught an EF SQL-generation integration defect: two raw SQL lines beginning `GoodsReceiptNoteItemId` were interpreted as batch separators, although direct SQL fixtures passed. The isolated rehearsal transaction rolled back; the original application database was untouched. The two leading identifiers are being delimited, with a regression against the real SQL Server migration generator. Normal rebuild and repeated full migration rehearsals remain required before the app upgrade.

### Partner account-tab clarification

The user asked why the original partner account tabs disappeared. The exact first-parent diff of Finance integration commit `ead698da0c` removes both tab triggers and the old `PartnerAccountsFields` / `BusinessPartnerReceivablesFields` detail panels. Later commit `b29cf068f2` restores the headings but substitutes `BusinessPartnerCurrentAccountsPanel`, which shows Finance settings and transaction-source descriptions. Live inspection of Ama Mensah's customer detail confirms the Accounts Receivable tab exists and loads its three tenant-default accounts; this does **not** establish restoration of the original GP-style account-maintenance feature.

Current canonical AP profiles expose one default expense account plus payment/tax/WHT defaults; AR profiles expose no GL mappings. Customer balance adjustments require explicit contra accounts. Some receipt and older return paths still read retained legacy mappings, so restoring the deprecated bulk partner payload would be misleading. The historical integration cause has been explained to the user; full GP account-view parity remains distinct from the seven Inventory workstreams and must not be claimed complete by the tab-heading restoration alone.

### Verified migration and current Finance period policy (2026-09-27 19:10 UTC)

The corrected real SQL generation regression passed **14/14**. The populated 60-to-61 copy upgrade and empty-to-61 installation both passed, including all 12 new tables, 32 authority triggers, trusted foreign keys, precision checks and original-record preservation. Evidence: `tmp/inventory-workflows-final-migration-acceptance.json`. The full untouched/generated snapshot representations match exactly (9,402,859 characters). A verified COPY_ONLY backup preceded the application test-database upgrade, which also passed with original stock, journals and Finance profiles preserved: `tmp/inventory61-app-upgrade-20260927_190859.json`. The current verification API now runs migration 61 from `tmp/gs-20260927T190927`.

The earlier proposed separate book-period approval prerequisite was obsolete. Current FinancePostingEngine validates the tenant fiscal calendar; live September 2026 is Open and its INV/FIN/PROC/SALES/HR module locks are Open. No book-period workflow, extra permission or fiscal-period mutation was necessary. The next live step is an issue-accounting mapping through the existing storesmanager role, followed by the existing requisition issue and partial/final receipt checks. Those live actions are not yet complete.

The user explicitly requested the BP account-view repair alongside final posting verification, followed by VPS preparation for QS UAT. The view is being connected to actual supported canonical settings, approved effective profiles and retained overrides verified against their posting consumers. An additive read-only COGS settings DTO property exposes the existing configured account; it does not alter the database or posting policy. Its normal rebuild and focused contract test are separate follow-up gates. No VPS deployment or QS seed completion is implied by local acceptance.

### BP account view and Finance adjustment verification

The account tabs now render a compact purpose/code/name/source table on both customer and supplier detail/edit pages. Actual live DEFAULT checks confirmed supplier AP 2000, discounts 4910 and GRV accrual 2110; customer AR 1100, discounts 4210 and Inventory 1200. Supplier approved profile v1 is displayed even though Finance maintains a newer draft v2, so unapproved defaults are not represented as effective posting configuration. Unconfigured values and inaccessible values remain distinct. Transaction-owned finance charges, writeoffs and overpayment writeoffs show their real Finance adjustment owner rather than unused legacy partner defaults.

The same-page Finance Profiles navigation was tested from an edit URL already containing `tab=finance-profiles`, after switching to each account tab. An explicit local callback now returns to the Finance Profiles tab without submitting the partner form; supplier and customer live checks passed. No partner/profile record was modified during these checks.

The customer adjustment form previously described the contra account as optional while its posting service requires an explicit eligible account. The form now requires an account for all four purposes and identifies the existing expense/revenue requirements. The Finance-charge form was inspected live without posting. Focused schema tests cover blank/whitespace selections for every purpose. The initial combined frontend set passed 40/40; after the same-query fix, the changed panel/edit set passed 22/22, and changed-file TypeScript passed. Final staged production build and the COGS read-contract backend rebuild remain running at this checkpoint.

Deployment guard checks passed for operational seeding, canonical preflight and release prerequisites. API artifact reuse now includes `tools/ErpSystem.MigrationModelCompiler` among its source inputs. The latest origin fetch matched current HEAD/master (no pending master integration). VPS QS preparation boundaries and pending live checks are recorded in `QS_VPS_UAT_READINESS_20260928.md`.

The COGS read-contract follow-up normal Core/Data/API build passed with zero errors (661 warnings), its selected settings tests passed **19/19**, and EF confirmed no pending model changes. The Data assembly hash is unchanged from the accepted fresh/upgrade migration rehearsals. Runtime `tmp/gs-20260927T192932` uses the final API/Core artifacts; all five health checks pass. The live customer grid now receives the COGS property and correctly shows **Not configured**, matching this database's null setting, rather than the previous unavailable field. No account mapping was invented or written to make that row look configured.

Live detail-to-customer-adjustment navigation passed after the dev route completed loading. The storesmanager sign-in requested for issue-rule setup and the existing requisition's live posting/partial/final receipts remains pending. Wider full-suite failures and unexecuted business lifecycles above remain open; focused passes are not a claim that the full repository suite or VPS UAT has passed.

Final staged production frontend build passed with BUILD_ID `fdfRj2coz-H0u6pxtaqAx` (`tmp/bp-final-frontend-production.log`). All 64 copied frontend source files match both their recorded hashes and the current workspace after the build. The latest account-panel/edit-navigation suite passed **22/22** and its narrow TypeScript check passed. The live development server and user-owned configuration were not used as the production build directory.
