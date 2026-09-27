# Inventory controls and accounting implementation

Baseline: `b8307938eb` (deployed master), branch `codex/inventory-controls-and-accounting-20260926`.
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
| 1 | Add explicit per-line actual receipts, cumulative/outstanding quantities, repeated partial acknowledgements, receiver security and replay protection. Preserve issued lines and historical acknowledgements. | InventoryRequisitionService; IssueRequisitionDialog; InventoryIssueVoucher entities/configuration | In progress: implementation and migration present; 17 service tests, model parity, 21 SQL guard checks and isolated full-schema upgrade pass; fresh installation, two-session concurrency and live acceptance pending |
| 2 | Remove request/approval bin requirement; capture validated source picks at issue and destination bin at receipt. Preserve partial dispatch. | InventoryTransferService, valuation Transfers, Transfer/Ship/ReceiveTransferDialog, SQL lifecycle guards | Analysis complete |
| 3 | Canonical supplier BusinessPartner carrier selector/FK plus historical name snapshot in both shipping paths. | Transfer shipping DTO/service and ShipTransferDialog | Analysis complete |
| 4 | Activate system-managed transit location with balanced physical/carrying-value legs, shipment/container lineage and ledger-backed report; reconcile historical open transfers explicitly. | InventoryTransferService and InventoryValuationService | Analysis complete |
| 5 | Multiple Employee assignments, authorized lookup, active committee access, audited draft edits, queued deduplicated notifications. | PhysicalCountService; Employee/ApplicationUser; NotificationTopicPublisher | Analysis complete |
| 6 | Independent original/addendum sheet snapshots, selective recount flags/reasons, repeatable approval, root-line adjustment claims, freeze retention. | PhysicalCountService.Controlled/Review; StockAdjustmentService; DMS sheet lineage | Analysis complete |
| 7 | Informational defective quantity/comment across manual entry, XLSX, upload grid and audit; validate against physical quantity without affecting variance. | PhysicalCountItemsGrid; PhysicalCountSheetReader; physical-count-sheet.ts | Analysis complete |
| 8 | Tenant PPV/revalue policy and immutable receipt/invoice cost-difference allocation; clear GRNI at original basis; eligible remaining-stock revaluation through valuation service. | ProcurementSettings; VendorInvoiceService; existing item PPV account; valuation owner | Analysis complete |
| 9 | Quantity-level uninvoiced/invoiced return allocation, multiple invoice notes, original tax/account/FX lineage, synchronized invoice/return locks. | PurchaseReturnService; SupplierDebitNoteService.InventoryReturns | Analysis complete |
| 10 | Select all filtered/clear/manual choices; fix mixed-warehouse eligibility and stale selections. | inventory/warehouse-items page | Analysis complete |
| 11 | Match UI capabilities to server permission and expose ProblemDetails; reproduce actual 403 and correlate access decision before changing grants. | WarehouseItemsController; ProcurementAccessControlService | Code trace complete; runtime cause unverified |
| 12 | Stage-appropriate non-sales waybill and idempotent auction canonical AR draft with buyer BP and disposal lineage. | InventoryDisposalService; document infrastructure; Finance InvoiceService | Analysis complete |
| 13 | Reject new Sales disposal while preserving history; canonical Sales invoice integration/distribution preview with read-only cost quote. | InventoryDisposalService; SalesOrderService; Finance InvoiceService | Analysis complete |
| 14 | Item disposal posting account, original-cost debit and proceeds credit through existing adjustment/AR/payment owners; shared previews. | InventoryItem accounts; StockAdjustmentValuationIntentBuilder; Finance AR/payment | Analysis complete |
| 15 | Supplier category first; server resolves category/ancestor AppliesToType before SLA/workflow. | EhcTicketService.CreateExternalTicketAsync; external-portal ticket form | Analysis complete |
| 16 | Hide supplier priority and enforce existing Medium/active-priority fallback on server; retain internal priority. | External ticket entry point and priority configuration | Analysis complete |
| 17 | Force Website (`Web`) server-side for supplier tickets; hidden/read-only UI; preserve internal/Estate behavior. | External ticket entry point | Analysis complete |

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

Warehouse assignment requires `procurement.inventory.master-data.manage`; its current registry scope is unscoped and its seeded roles include Stores Manager and ICT Administrator. UI currently exposes the action without checking that capability. The precise reported 403 remains unproven until an actual request/access audit is captured; do not fix it by weakening authorization.

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
