# Inventory and Stores End-to-End UAT Script

## Purpose and source boundary

This script validates Inventory and Stores against the TDC ERP Architecture and Design Document, specifically Section 16.1, Figure 10, Sections 18.1, 19.1, 19.3, 20, 21, 22.3 and 22.4, together with the formal cross-module requirements `FR-PR-006`, `FR-PR-007`, `FR-AP-005`, `FR-BG-008` to `FR-BG-010`, `FR-FA-001` to `FR-FA-011`, and the applicable security, audit, reliability, data, usability, integration and reporting NFRs.

The architecture document does not define numbered `FR-INV-*` or `FR-ST-*` requirements. This script therefore does not invent them. The traceability baseline is `docs/TDC_INVENTORY_STORES_ARCHITECTURE_REQUIREMENTS.md`.

Use fresh records for transactional testing. Existing VPS references may be used only for read-only comparison.

Menu names and route labels in this script describe the current implementation and are test navigation aids; TDC does not mandate those exact labels. A missing implementation-extension screen is not, by itself, a failure of the core TDC requirement. A missing core capability, correction path, or cross-module control is a defect.

## Architecture requirement versus implementation extension

Every expected outcome in this script is classified as follows:

- **TDC required**: directly required by the architecture document or a formal cross-module `FR-*`/`NFR-*` control.
- **Configured control**: required only when the tenant has enabled that workflow, evidence rule, threshold, tolerance or approval stage.
- **Implementation extension**: a richer product feature that may be tested, but must not be represented as a fixed TDC requirement or become an unexplained hard stop.

Examples of implementation extensions include MRN, using an exact antivirus status as an Inventory workflow gate, exact accepted/damaged/short buckets, exact signature counts, FIFO or weighted-average costing, blind counts, recount stages, barcode/mobile scanning, negative-stock policy, directed put-away, immutable fingerprints, DEC identifiers, and detailed stock-disposal committee stages. Malware scanning itself is a shared DMS/platform security control rather than an optional Inventory business control.

As an implementation acceptance control supporting the TDC usability and data-integrity NFRs, the user must select controlled business records by friendly name. UAT must fail any screen that requires a user to type an internal GUID, workflow-definition ID, DMS record ID, evidence ID, checksum, warehouse ID, location ID or policy key.

## UAT result convention

For every test case, record:

- Result: `Pass`, `Fail` or `Blocked`
- New record/reference number
- Tester, user account and configured role
- Tenant, warehouse and location
- Date and time
- Before-and-after quantity and value, where applicable
- Workflow/action history reference
- DMS document name, type and version, where applicable
- Stock movement, valuation layer, asset and journal references, where applicable
- Correlation ID and defect reference for a failure
- Screenshot, online report or exported evidence

Do not mark a test passed from the screen alone when the expected result includes stock, valuation, asset, journal, workflow, document, reconciliation, tenant-scope or audit persistence.

## Required users and segregation

TDC defines responsibilities, not exact role codes. The following are current test-role mappings only and may be replaced by equivalent approved tenant roles with the required permissions:

- Stores maker/receiver/issuer: `TDC_STORES_OFFICER`
- Stores reviewer/approver: `TDC_STORES_MANAGER`
- Procurement source-record maker: `TDC_PROCUREMENT_OFFICER`
- Procurement or PO approver: `TDC_HEAD_OF_PROCUREMENT` or configured approver
- Finance/General Ledger reviewer with inventory-journal and reconciliation access
- Accounts Payable reviewer with invoice and three-way-match access
- Internal Audit reviewer: `TDC_INTERNAL_AUDIT`
- Maintenance user authorized for work orders and material use
- Fixed Asset reviewer/custodian user
- Disposal reviewer/committee role, only when the configured extension route requires it
- Unauthorized user with no Inventory/Stores permission
- User assigned to a different warehouse
- Alternate-tenant user

Use distinct users for maker, approver, receiver, variance approver and other incompatible stages. Administrator access is not evidence of business-role acceptance.

## UAT preparation

Confirm these business prerequisites before starting. Do not create them through direct SQL changes during UAT.

1. The tenant base currency is `GHS`, the fiscal period is open, and controlled inventory/valuation/clearing/expense/asset account mappings exist.
2. Two active warehouses exist, each with at least one active storage location. A quarantine or exception location exists if the configured inspection process uses one.
3. Test Stores users have explicit responsibility for the warehouse on which they act. The UI must pass the selected controlled warehouse/location; it must not ask for a warehouse GUID.
4. Active item masters exist for:
   - a normal consumable;
   - a tracked serial, lot or batch item, if tracking is configured;
   - a capital/fixed-asset-eligible item.
5. Controlled UOMs, item categories, stock locations, reorder levels and valuation configuration exist.
6. An approved supplier and an approved/open PO exist with positive Goods lines and sufficient unreceived quantity. Include one line not yet linked to an inventory item to test controlled item creation/linkage.
7. The PO has an approved/current budget and a valid commitment created when the approved PO or contract was issued.
8. Test DMS files exist for a waybill, optional VAT-invoice copy, inspection/discrepancy evidence, issue note, return evidence, count evidence and disposal evidence.
9. Published workflows and eligible users exist for each lifecycle the tenant has configured, including receipt inspection, inventory requisition, transfer, adjustment, count variance, return and disposal.
10. An active maintenance work order exists for the maintenance-material test.
11. Finance/AP users can view the relevant journals, commitment, invoice and matching records.

Expected:

- Controlled master data is tenant-scoped, active and searchable by business name/code.
- Missing optional extension configuration is advisory unless the business explicitly enabled it for the tested route.
- Missing mandatory TDC data or a breached configured control produces a clear corrective message and no partial mutation.
- A different approved role name, approval limit, threshold, or route is not a defect merely because it differs from this script; Section 18.2 makes those details configurable. The configured matrix and its approval evidence are the acceptance source.

## Runtime gate boundary

- **Always mandatory:** authenticated tenant access, authorization, active approved master data, positive quantities, a valid governed source where applicable, valid warehouse/location, maker-checker where approval is required, transaction integrity, audit, and no duplicate posting.
- **Mandatory at the relevant stage:** approved PO/contract before procurement receipt; inspection outcome before only accepted stock is made available when inspection is required; approved movement before posting; PO/GRN or certificate/VAT invoice match before payment unless an approved exception exists.
- **Conditional when configured:** waybill before inspection, quarantine routing, tracking fields, tolerance, recount, negative-stock exception, additional signatures, stock-disposal committee, supplier dispute/replacement, extra evidence and exact approval limits. Fixed-asset disposal still follows `FR-FA-008`.
- **Advisory when absent:** DEC metadata, immutable fingerprints, advanced policy lineage and other implementation-only governance metadata not required by TDC.
- A supplier invoice is not a prerequisite for operational stock update after an approved inspected receipt. Figure 10 places inventory update before three-way matching and payment.
- The budget commitment is created when the approved PO or contract is issued, not at receipt. Receipt/invoice/payment must not create the same commitment again.

## Executable assurance gates

Use this document as the business UAT script and run the following automated gates before recording customer UAT. The SQL gate requires `RHEMA_TEST_SQLSERVER` to reference a disposable-database-capable SQL Server 2022 instance; never commit or print its value.

```powershell
dotnet test tests/ErpSystem.Core.Tests/ErpSystem.Core.Tests.csproj --no-restore `
  -p:TdcFocusedTestBuild=true `
  -p:TdcFocusedTestFile="Services\Inventory\InventoryStoresArchitectureSqlServerIntegrationTests.cs" `
  --filter "FullyQualifiedName~InventoryStoresArchitectureSqlServerIntegrationTests" -m:1
```

`InventoryStoresArchitectureSqlServerIntegrationTests` creates and removes a uniquely named database and uses only fresh records. Its representative transfer actor matrix is:

| Test actor | Fresh-record action | Database control proved |
| --- | --- | --- |
| Maker | Creates and submits the transfer | Draft-only creation and an immutable submitted source |
| Unauthorized user | Attempts to submit without an active tenant assignment | SQL error `51802`; no action or state mutation |
| Maker acting as approver | Attempts approval inside a rolled-back transaction | SQL error `51854`; no self-approved action or status survives |
| Independent approver | Approves the submitted transfer | Separate approval actor and durable action history |
| Independent dispatcher | Dispatches and posts the source movement | Source balance and governed `TransferOut` lineage update once |
| Independent receiver | Receives and posts the destination movement | Destination balance and governed `TransferIn` lineage update once |
| Independent closer | Completes the reconciled transfer | Completed state requires a separate closer and closed quantities |
| Alternate-tenant maker | Owns a separate fresh transfer | Tenant-filtered reads hide the alternate-tenant row |

The same SQL journey also proves negative-stock rejection, post-submit amendment rejection, persisted DB read-back after each transition, tenant-scoped movement-number uniqueness, and action-idempotency replay rejection. It is a representative database lifecycle gate, not a substitute for the receipt/inspection/DMS, issue/asset, adjustment/count, Finance/AP, reporting/export, or visible browser scenarios below.

Run the broader focused Core/API and frontend suites for those cross-module seams. Authenticated browser scripts live in `e2e-tests/tests/inv-fu-002-maintenance-reservation.spec.ts`, `inv-fu-003-issue-return-asset.spec.ts`, and `inv-fu-004-transfer.spec.ts`; they require a running seeded local host and their named role credentials. A component or service test must not be recorded as browser evidence.

The executable fresh-record INV-FU-002 / INV-FU-003 journey is:

```powershell
& .\scripts\acceptance\Invoke-InvFu002003.ps1
```

For a self-contained fresh SQL Server and authenticated Chromium run, use the guarded launcher. It refuses an existing database and drops only the disposable database it created:

```powershell
& .\scripts\acceptance\Invoke-InventoryStoresBrowserAcceptance.ps1
```

Run it against the configured local acceptance database with the API listening on `127.0.0.1:5100` and branch-locked dependencies installed in `frontend` and `e2e-tests`. The script uses the central Workflow, Inventory, Maintenance, Finance and Fixed Assets owners for lifecycle mutations. Database access is limited to prerequisite discovery, temporary isolated actor setup, durable postcondition read-back and cleanup. It restores temporary passwords and grants in `finally` and starts its own frontend for the two Chromium read-back specifications.

| Acceptance actor | Fresh-record responsibility | Control evidence |
| --- | --- | --- |
| `employee` | Creates the maintenance work order/reservation and fixed-asset requisition | Maker identity and controlled source records |
| `manager` | Independently approves the requisition and Store Return Voucher | Maker-checker separation and shared-workflow approval |
| `admin` | Independently issues stock and posts the approved return | Warehouse/location authorization plus Finance/asset lineage |
| `finance.clerk` | Returns unused maintenance stock and acknowledges/requests the fixed-asset return | Separate custody and return actors |
| `ap.officer` | Independently reverses the posted return | Compensating stock, Finance and fixed-asset lineage |
| `estate.officer1` | Attempts unauthorized reservation and issue mutations | HTTP 403 with no inventory mutation |
| Anonymous caller | Attempts the requisition list | HTTP 401 authentication boundary |

Before browser evidence can pass, the harness proves idempotent reserve/use/return actions, idempotent issue/return/post/reversal actions, exact Store Return Voucher allocations, four distinct return actors, one Finance reversal event and one fixed-asset lineage record. Use `-SkipBrowser` only for API/database diagnosis; it is not full acceptance evidence.

The executable fresh-record INV-FU-004 / E2E-011 transfer journey is:

```powershell
& .\scripts\acceptance\Invoke-InvFu004E2E011.ps1
```

Run it against the configured local acceptance database with the API listening on `127.0.0.1:5100`, the central ClamAV scanner healthy, and branch-locked dependencies installed in `frontend` and `e2e-tests`. The script starts its own frontend on port 3001, uses governed APIs rather than direct lifecycle mutation, runs the Chromium transfer spec, and restores temporary passwords, grants, and tenant changes in `finally`. Its fresh-record actor matrix is:

| Acceptance actor | Fresh-record responsibility | Control evidence |
| --- | --- | --- |
| `employee` | Creates and submits the transfer | Maker identity and submitted lineage |
| `manager` | Independently approves and performs the final read | Maker-checker separation and completed-state visibility |
| `admin` | Creates protected DMS evidence and independently dispatches | Clean-scanned evidence plus source stock movement |
| `finance.clerk` | Independently records partial/damaged receipt | Destination receipt, discrepancy and stock movement |
| `ap.officer` | Independently resolves the discrepancy and closes | Evidence-backed resolution and reconciled closure |
| `estate.officer1` | Attempts an unauthorized mutation and an alternate-tenant read | HTTP 403 mutation denial and HTTP 404 tenant isolation |
| Anonymous caller | Attempts the transfer list | HTTP 401 authentication boundary |

The script reads the completed transfer back through the API and database, verifies exact source/destination stock deltas, immutable action history, distinct actors, audit rows, control events, protected DMS evidence, replay idempotency and direct-SQL tamper rejection before browser evidence can pass.

## UAT-INV-001: Controlled item, warehouse, location and access setup

1. Sign in as an authorized master-data administrator.
2. Open the Inventory item, UOM, warehouse and location setup pages.
3. Create a fresh consumable item with a controlled UOM and category.
4. Assign it to the first warehouse/location and set a reorder level.
5. Open the record using the authorized Stores user.
6. Attempt the same mutation using an unauthorized user, wrong-warehouse user and alternate-tenant user.

Expected:

- **TDC required:** item, UOM, location, active state and audit lineage are retained; only approved/active master data is selectable in transactions.
- **TDC required:** unauthorized mutation returns `403`; cross-tenant access returns `404` or governed denial; denied actions do not change stock.
- **Configured control:** where warehouse responsibility/data scope is enabled, a user outside the selected warehouse scope is denied without mutation.
- **TDC required:** master-data changes identify actor, timestamp, old/new values and reason where applicable.
- **Configured control:** approval of item or warehouse master changes follows the configured maker-checker route.
- **Implementation extension:** exact item-code pattern, tracking fields and warehouse-responsibility mechanics are product configuration, not fixed TDC rules.

## UAT-INV-002: Reorder alert and replenishment initiation

1. Set available stock below the configured reorder level through governed test transactions.
2. Open `Inventory -> Replenishment` or the configured reorder-alert work queue.
3. Confirm the item/warehouse appears with the current balance and replenishment need.
4. Initiate a Draft replenishment request or Purchase Requisition.
5. Repeat the same action using the same source alert.

Expected:

- **TDC required:** Stores can review stock and initiate replenishment from controlled item, warehouse and stock data.
- **TDC required:** the resulting demand retains source item, warehouse, quantity, requester and audit lineage.
- **TDC required:** replay does not create duplicate demand silently.
- **Implementation extension:** min/max formula, lead-time formula and suggested replenishment quantity are configurable and must be explained in the UI.

## UAT-INV-003: Approved-PO goods receipt and missing-item handling

1. Sign in as an authorized Stores receiver who did not create the PO where the configured SOD rule prohibits that conflict.
2. Open the approved/open PO and select `Receive`.
3. Confirm supplier, PO lines, ordered quantity and previously received quantity are server-derived.
4. Select the controlled receiving warehouse and storage location by name/code.
5. Enter a partial or full received quantity within the remaining quantity.
6. For the PO line not linked to an inventory item, confirm the system shows a line-specific choice to link an existing item or create a new controlled item.
7. Choose to create/link the item, review the proposed item data, confirm it and create the receipt.
8. Retry the create action using the same request/reference.

Expected:

- **TDC required:** receipt retains PO, supplier, delivery reference, item, quantity, warehouse/location, receiver and timestamp.
- **TDC required:** no receipt can be created from a Draft, rejected, closed, foreign-tenant or invalid PO.
- **TDC required:** over-receipt, invalid location and unauthorized warehouse are rejected without stock mutation.
- **TDC required:** retry creates one receipt and one set of stock consequences only.
- **Implementation extension:** missing-item creation/linking is a usability extension. It must be an explicit per-line confirmation and must not silently create duplicate item masters.

## UAT-INV-004: Waybill, invoice copy and controlled DMS evidence

1. Open the new purchase receipt.
2. Open the Documents or receipt-evidence area.
3. Upload the waybill using its configured friendly document type.
4. Upload a VAT-invoice copy as supporting evidence.
5. Replace one file with a new version and review document history.
6. Attempt to view/download the evidence using an unauthorized and alternate-tenant user.

Expected:

- **TDC required:** documents are stored through central DMS and linked to the receipt with type, version, access, retention and audit metadata.
- **TDC required:** VAT copy upload does not itself create/post an AP invoice or payment.
- **TDC required:** unauthorized/cross-tenant access is denied without leaking metadata.
- **Configured control:** missing mandatory waybill blocks only the configured downstream stage and gives a corrective message.
- **System security control:** the shared DMS may quarantine, reject, or withhold unsafe files after malware scanning. This is enforced independently of Inventory business approval.
- **Implementation extension:** an exact status label such as `Clean`/`AV Clean`, or using that label as a downstream Inventory-stage gate, is not prescribed by TDC and is enforced only when configured.

## UAT-INV-005: Independent receipt inspection and discrepancy correction

1. Submit the receipt for inspection when inspection is configured or required by item/category.
2. Record accepted and non-accepted quantities. If supported, distinguish rejected, damaged and short quantities.
3. Attach discrepancy/inspection evidence by friendly document selector.
4. Attempt approval using the receipt/inspection maker.
5. Approve or reject using a different authorized reviewer.
6. For a failed/discrepant result, complete the correction, replacement, supplier-change or re-approval route and close the discrepancy.

Expected:

- **TDC required:** maker cannot approve their own governed inspection.
- **TDC required:** quantities reconcile to the received quantity; only the governed accepted outcome can become available stock.
- **TDC required:** failed inspection or delivery discrepancy remains visible until investigated, corrected and re-approved.
- **TDC required:** evidence and inspection history are retained and cannot be silently overwritten.
- **Implementation extension:** exact quantity buckets, quarantine location, tolerance and replacement workflow are configurable. The user must never type raw location, DMS or workflow IDs.

## UAT-INV-006: GRN and receipt-document lifecycle

1. Open the receipt's GRN/document tab.
2. Generate the GRN from the governed receipt and inspection outcome.
3. Complete configured signature/approval steps using distinct users where required.
4. Issue/finalize the GRN and download the generated document.
5. Repeat the issue/finalize action.
6. If MRN is enabled, generate, sign, issue and reconcile it as a separate implementation-extension test.

Expected:

- **TDC required (`FR-PR-006`):** one controlled GRN or applicable certificate is retained before invoice matching.
- **TDC required:** the generated document retains receipt, PO, supplier, quantities, actor, date and DMS lineage.
- **TDC required:** retry does not create duplicate issued documents or business events.
- **Configured control:** configured signature/approval stages are enforced with visible history.
- **Implementation extension:** MRN, exact numbering format and an exact count of two signatures are not mandated by TDC.

## UAT-INV-007: Stock ledger, valuation layer and receipt journal

1. Open the received item in `Inventory -> Warehouse Items`.
2. Confirm on-hand/available stock increased only by the approved accepted quantity.
3. Open stock movements and trace the movement to the receipt/GRN/PO.
4. Open Inventory Valuation and the Finance journal for the receipt.
5. Compare quantity, unit value, total value, debit and credit.
6. Reopen/retry the completed receipt action.

Expected:

- **TDC required:** stock, valuation and ledger records are updated through the governed receipt once.
- **TDC required:** the journal balances and retains source references.
- **TDC required:** no rejected/damaged/unresolved quantity increases available stock.
- **TDC required:** retry does not duplicate movement, valuation layer, journal or audit business event.
- **Implementation extension:** the selected costing method and exact account mapping are configured choices, not fixed TDC rules.

## UAT-INV-008: Landed-cost allocation and posting

This scenario validates an implementation extension that supports TDC valuation and ledger reconciliation; the architecture does not prescribe a landed-cost method.

1. Create a landed-cost record for the receipt using a fresh freight/duty amount.
2. Select a configured allocation method and preview the allocation.
3. Submit/approve/post it through the configured workflow.
4. Review the updated valuation layer and Finance journal.
5. Attempt to post the same landed-cost record again.

Expected:

- **TDC required outcome:** inventory valuation and Finance postings remain balanced, traceable and reconcilable.
- **Configured control:** configured approval and open-period rules are enforced.
- **Implementation extension:** allocation method, exact accounts and landed-cost document fields are product configuration.
- Replay does not duplicate cost, valuation or journal postings.

## UAT-INV-009: Inventory requisition maker-checker approval

1. Open `Inventory -> Transactions -> Requisitions`.
2. Create a requisition for the consumable item from a controlled warehouse/location.
3. Enter positive quantity, receiver/department/purpose and required date.
4. Save and submit it.
5. Attempt approval using the requester.
6. Approve with a different configured Stores approver.

Expected:

- **TDC required:** the request retains item, quantity, requester, receiver/cost purpose, warehouse/location and workflow history.
- **TDC required:** self-approval is rejected; unauthorized approval changes no state.
- **TDC required:** approval alone does not reduce stock unless the configured process combines approval and issue explicitly.
- **Implementation extension:** exact form fields and approval thresholds are configured under Section 18.2.

## UAT-INV-010: Consumable issue and receiver acknowledgement

1. Sign in as an authorized Stores issuer who did not perform an incompatible approval stage.
2. Open the approved inventory requisition and select `Issue`.
3. Confirm controlled item, source location, approved/remaining quantity and receiver.
4. Issue part or all of the approved quantity.
5. Generate/review the issue note or voucher.
6. Complete receiver acknowledgement if configured.
7. Repeat the Issue action.

Expected:

- **TDC required:** stock reduces once by the issued quantity and the issue note retains recipient, purpose, item, value, actor and timestamp.
- **TDC required:** insufficient stock, excess issue, invalid location or unauthorized issuer is rejected without partial mutation.
- **TDC required:** movement/value and Finance cost lineage reconcile.
- **TDC required:** retry does not issue stock twice.
- **Configured control:** receiver acknowledgement is enforced only when configured.

## UAT-INV-011: Fixed-asset-eligible issue and capitalization handoff

1. Create and approve a separate inventory requisition for the capital/fixed-asset item.
2. Issue it to a selected employee/custodian and location.
3. Enter controlled serial/identifier data when required by the item.
4. Open the resulting Fixed Asset and Finance records.
5. Retry the issue or capitalization action.

Expected:

- **TDC required (`FR-FA-001`/`FR-FA-002`):** one asset is created or updated from the approved stores issue with source, supplier/acquisition, cost, class, location, custodian, funding/department and status lineage.
- **TDC required:** stock decreases once and the asset cost equals the governed issued inventory value.
- **TDC required:** missing mandatory controlled asset data blocks capitalization without leaving partial stock/asset/journal changes.
- **TDC required:** retry does not create a duplicate asset.

## UAT-INV-012: Inventory return and reversal

1. Open an issued requisition and select `Return Items`.
2. Enter a valid return quantity, reason, condition and destination location.
3. Submit and complete configured independent approval.
4. Post/confirm the return.
5. Review stock, allocation, valuation, Finance and fixed-asset custody/status where applicable.
6. Repeat the same return request.

Expected:

- **TDC required:** stock/value is restored through a governed movement and source issue lineage is retained.
- **TDC required:** related allocation, accounting and asset custody/status are reversed or updated consistently where applicable.
- **TDC required:** excess return, self-approval and duplicate return are rejected without mutation.
- **Implementation extension:** exact return-condition codes and approval path are configurable.

## UAT-INV-013: Maintenance reservation, consumption and unused return

1. Sign in as the Maintenance user and open the active work order.
2. Add the stocked consumable as a material requirement and select warehouse/location.
3. Save the required quantity and review the inventory reservation/allocation.
4. Increase, then reduce, the required quantity.
5. Consume part of the allocation.
6. Return the unused quantity and close/reconcile the material requirement.

Expected:

- **TDC required cross-module outcome:** work-order material usage, Inventory movements and work-order cost capture remain linked and reconcilable.
- **Implementation extension:** reservation/allocation, incremental quantity amendment, partial consumption and unused-return mechanics are product workflow choices; when enabled, they must operate coherently and remain auditable.
- **TDC required NFR outcome:** an Inventory failure leaves no partial work-order/stock update, and controlled retry does not duplicate movement or cost.

## UAT-INV-014: Warehouse transfer and discrepancy closure

1. Open `Inventory -> Transactions -> Transfers`.
2. Create a transfer between two different controlled warehouses for quantity `10`.
3. Submit it and attempt approval with the creator.
4. Approve using a different authorized Stores user.
5. Dispatch all `10` from the source.
6. At the destination, record `7` accepted, `2` damaged and `1` short if the extension buckets are enabled; otherwise record the equivalent accepted/discrepant totals.
7. Attach controlled discrepancy evidence.
8. Resolve the damaged/short quantity through the configured replacement, return or adjustment route.
9. Close the transfer using an authorized independent user.
10. Retry dispatch, receipt, resolution and close.

Expected:

- **TDC required:** dispatch reduces source stock exactly once; governed receipt increases destination stock only by the accepted/resolved quantity.
- **TDC required:** the discrepancy remains open and reportable until resolved.
- **TDC required:** same warehouse, excess quantity, unauthorized actor/location, self-approval and unresolved closure are rejected without invalid movement.
- **TDC required:** retries do not duplicate movements.
- **Implementation extension:** exact damaged/short buckets and closure stages are configurable; raw exception/evidence IDs are never user input.

## UAT-INV-015: Controlled stock adjustment and reversal

1. Open `Inventory -> Transactions -> Adjustments`.
2. Create a positive or negative adjustment with item, warehouse/location, quantity, reason and evidence.
3. Submit it and attempt approval/posting as the maker.
4. Approve/post with a different authorized user.
5. Review stock, valuation, journal and audit history.
6. Reverse the adjustment through the governed reversal route.
7. Retry post and reversal.

Expected:

- **TDC required:** direct stock editing is not possible; the approved adjustment posts once with reason, evidence and audit history.
- **TDC required:** self-approval, inactive item/location, invalid quantity or unauthorized warehouse is rejected.
- **TDC required:** reversal creates a compensating trace, not deletion of the original movement.
- **TDC required:** stock, valuation and journal remain balanced after posting and reversal.

## UAT-INV-016: Physical count, variance and reconciliation

1. Open `Inventory -> Transactions -> Physical Counts`.
2. Create a count for a controlled warehouse/location and item scope.
3. Freeze/capture the system quantity according to the configured count process.
4. Record physical quantities and supporting stock-taking evidence.
5. Submit the count and complete any configured recount.
6. Have Stores, Finance and Internal Audit review/sign off where configured.
7. Approve and post the variance once.
8. Re-run posting and review the unresolved-items log.

Expected:

- **TDC required:** system quantity, physical quantity, variance, value, evidence, reviewer sign-off and resolution are retained.
- **TDC required:** unapproved variance cannot change stock or ledger.
- **TDC required:** posted variance updates stock/valuation/journal once and remains auditable.
- **TDC required (Section 22.4.10):** unresolved items remain visible with resolution evidence.
- **Implementation extension:** blind count, tolerance, recount stages and team composition are configured choices.

## UAT-INV-017: Supplier return after receipt

1. Open `Inventory -> Transactions -> Supplier Returns`.
2. Create a return against the governed receipt/PO for a valid returnable quantity.
3. Enter supplier, source location, reason and evidence.
4. Submit and complete configured independent approval.
5. Dispatch/complete the supplier return.
6. Review stock, valuation, supplier/PO lineage and Finance consequences.
7. Retry completion.

Expected:

- **TDC required outcome:** supplier issue is resolved through a governed correction route with stock/value, evidence and audit lineage.
- **TDC required:** return cannot exceed governed received/available quantity.
- **TDC required:** retry does not duplicate outbound movement, valuation or journal effects.
- **Implementation extension:** supplier-return status model, replacement/dispute stages and exact workflow are configurable.

## UAT-INV-018: Damage, obsolescence, write-off and disposal

**Current result:** Implementation/testing in progress; live disposal not yet verified. Browser access is blocked by `ERR_BLOCKED_BY_CLIENT`. No example transaction or live pass is recorded. Both databases are now at **schema484**, following verified fresh COPY_ONLY/CHECKSUM backups; **86 protected business-table checks** confirmed unchanged stock, count and approval records. **120 API, 229 Core and 14 UI tests passed.** Both production frontends are built and running. At **17:06 UTC, 12 September**, both APIs passed live/readiness checks; four page routes and 61 assets per copy returned HTTP200. Build IDs/source parity, CORS and anonymous disposal-API rejection (401) were verified. [Release evidence](../local-artifacts/disposal-release-verification.json). These are HTTP/runtime checks, not a completed disposal walkthrough. B21/B22's earlier physical-flow passes remain separate evidence.

**Page:** `Inventory → Disposals` (`/inventory/disposals`). Use ordinary **StockItem** records only; fixed assets use the separate Fixed Asset disposal owner. Verify the actor's disposal/stock-posting permission, warehouse/bin scope, available stock, open posting period and Finance mappings before testing.

1. Record baseline item/warehouse/bin quantity, available quantity and carrying value. Click **New disposal**.
2. In **Details**, select warehouse, method and reason; leave optional notes blank for the simple case.
3. In **Items**, search an ordinary stock item and its exact bin. Confirm the suggested default warehouse bin is appropriate. Enter a small positive quantity and **Add**; enter tracking identity only when configured for the item.
4. Test grid flexibility: search by item/bin, edit a quantity, add a second line and remove it, change page size **25 / 50 / 100**, toggle **Columns**, and use **Full page / Restore**. Hidden cost columns must not alter valuation. Save the draft, reopen by the pencil icon and confirm edits persist without any stock movement.
5. Test **Supporting documents** by friendly document name. Without an active approval process, an empty selection must not block the normal path. With an active approval process, attach the required supporting evidence before submission. Any provided document must remain current and satisfy central security/access checks.
6. Test the inactive path: **Continue → Ready to post**, with no workflow approval button or compulsory reviewer/committee stage. Verify the recorded no-approval decision and null human approver fields. A delegated generated stock adjustment must not introduce an unrelated second approval workflow.
7. Separately test the active path: **Submit for approval → eligible independent actor Approve**. Confirm configured assignments, limits and maker/checker rules; unauthorized or conflicting approval fails. Deactivation must not erase an existing in-flight instance or its history.
8. Prepare method-specific execution: Auction/Sale requires buyer, positive proceeds and a controlled proceeds account; Donation requires a recipient; Write-off/Destruction must not include sale proceeds.
9. Click **Post**, review the confirmation and confirm **Post**. New ready disposals prepare the adjustment and complete stock/Finance posting in one serializable transaction. A posting error must roll back preparation, stock, Finance and completion together. Correct the cause and retry the same disposal. Older prepared records use the existing adjustment and completion path; do not recreate them. Configured approvals remain required before posting.
10. Expect **Completed** after successful posting. Compare **Posted stock value** with the authoritative movement and balanced Finance journal, not just the saved **Estimated stock value**. New adjustments use the execution date; previously prepared adjustments retain their recorded date. Check exact bin, quantity, tracking, source reference, actor and newest-first History. Verify proceeds and their journal where applicable.
11. Reopen and attempt a controlled replay: no duplicate outbound stock movement, valuation or Finance entry is allowed, and completed records must not offer another Post.
12. On a separate unposted draft, click the cancel icon and supply a reason. Expect cancellation with retained history and no stock/Finance mutation. Confirm read-only or out-of-scope users cannot create, edit, cancel, approve or post.

Expected:

- **TDC required:** damage/obsolescence follows a governed adjustment or write-off route; it is not removed by direct edit.
- **TDC required:** retain the reason, item/location/value impact, applicable supporting documents, configured approval decision and complete audit trail; retain no-approval decisions truthfully when the direct mode applies.
- **Configured control:** an active approval route enforces its assignments, limits and segregation. The agreed direct mode must not fabricate approval or require a separate stock committee when no process is active. It does not waive non-approval stock, Finance, source, tenant, access or document-security controls.
- **Implementation acceptance:** searchable controls, compact editable grid, pagination, optional columns, fixed normal dialog dimensions and Full page/Restore preserve state. Draft edits/cancellation change no stock. Costs shown before posting are estimates; final posted value comes from the authoritative valuation/posting owner.
- **Implementation extension for stock disposal:** exact committee structure and stages are not fixed architecture requirements. Auction/Sale proceeds-account selection is required by the current execution owner; select controlled accounts by name/code, not internal IDs.
- **TDC required for fixed assets (`FR-FA-008`):** the separate fixed-asset disposal process retains Board of Survey and procurement-disposal controls. Ordinary inventory stock disposal must not be used to bypass it.

**Technical verification limit:** Multiple FIFO tracking rows sharing one item/bin can reuse the same opening-layer estimate. Differing layer costs can then fail the final valuation comparison. This case is not verified/passed; retain the guard and require a complete transaction rollback with no stock/Finance posting.

Traceability: Section16.1 shared item/location/valuation/approval/ledger/audit; Section18.2 configurable thresholds/roles/routing; Section20.1 inventory disposals; `FR-FA-008` fixed-asset distinction. See [B23 customer walkthrough](TDC_CUSTOMER_PROCUREMENT_STORES_WALKTHROUGH.md#b23-inventory-stock-disposal).

## UAT-INV-019: Inventory valuation and General Ledger reconciliation

1. Open Inventory Valuation for the test period, item and warehouse.
2. Compare opening quantity/value, receipts, issues, returns, transfers, adjustments and closing quantity/value.
3. Open the valuation-to-GL reconciliation view.
4. Have Finance review/sign off the reconciliation.
5. Record and resolve an intentionally isolated non-production discrepancy if the test environment supports it.
6. Confirm period controls prevent unauthorized posting into a closed/frozen period.

Expected:

- **TDC required:** movement register, stock balance, valuation and ledger reconcile by period and location.
- **TDC required:** reviewer sign-off, unresolved-items log and resolution evidence are retained.
- **TDC required:** missing, duplicate, unbalanced or unexplained posting appears as an exception rather than being hidden.
- **Implementation extension:** costing method, freeze workflow and exact reconciliation tolerance are configured choices.

## UAT-INV-020: PO, GRN/certificate, invoice and payment matching

1. Sign in as the Finance/AP reviewer.
2. Create or open the supplier invoice for the accepted supply.
3. Match the invoice to the approved PO and governed GRN/certificate.
4. Confirm invoice quantity/value does not exceed the accepted/matchable supply or available commitment.
5. Attempt matching with a missing GRN, duplicate invoice, excess quantity/value and mismatched supplier.
6. Complete the match and route the payment voucher without posting the payment unless it is in test scope.

Expected:

- **TDC required (`FR-PR-007`/`FR-AP-005`):** PO, GRN/certificate and VAT invoice match before payment unless an approved exception exists.
- **TDC required:** duplicate invoice, missing receipt/certificate, supplier mismatch and excess invoice are blocked with corrective messages.
- **TDC required (`FR-BG-009`/`FR-BG-010`):** the PO commitment was created at approved PO/contract issue; receipt does not recreate it; invoice/payment convert exposure without double counting.
- **TDC required:** any approved exception retains reason, approver, evidence and audit lineage.

## UAT-INV-021: Inventory, valuation, reconciliation and audit reports

1. Open `Reports -> Inventory`.
2. Run the available reports covering:
   - movement register;
   - stock balance;
   - receipt register;
   - issue register;
   - transfer register;
   - count/variance and adjustment exceptions;
   - valuation;
   - reconciliation/unresolved items;
   - returns/disposals where available.
3. Filter by period, item, category, warehouse/location and status.
4. Drill from a report row to the source transaction where supported.
5. Export each required format supported by the report, including Excel/XLSX and PDF; test CSV where provided.
6. Repeat using unauthorized, wrong-warehouse and alternate-tenant users.

Expected:

- **TDC required:** reports reconcile to governed balances and source transactions.
- **TDC required:** online view, filters, drill-down and Excel/PDF export work according to authorized scope.
- **TDC required:** exported files have correct content, extension and tenant/warehouse scope.
- **TDC required:** report access/export is audited and unauthorized data is not exposed.
- **Implementation extension:** exact report titles and CSV availability are product choices; the required reporting capabilities must still be present.

## UAT-INV-022: End-to-end security, audit, failure recovery and idempotency

Repeat a representative read and mutation from receipt, inspection, issue, return, transfer, adjustment, count, disposal and reporting using:

1. An unauthenticated request.
2. A user without the required permission.
3. A user assigned to another warehouse.
4. An alternate-tenant user.
5. The original maker attempting an incompatible approval.
6. The same submission/idempotency key or browser action repeated after a timeout.
7. A forced integration or posting failure in an isolated test route, followed by controlled retry.

Expected:

- **TDC required:** unauthenticated request returns `401`; unauthorized action returns `403`; cross-tenant record returns `404` or governed denial.
- **TDC required:** wrong-warehouse and maker-checker conflicts are denied with a meaningful corrective message.
- **TDC required:** rejected actions create no stock, valuation, asset, journal, commitment, document or business-state mutation.
- **TDC required:** partial failure rolls back atomically or records a recoverable exception; controlled retry completes once.
- **TDC required:** duplicate submission never creates duplicate stock movement, valuation layer, asset, journal, GRN/document or audit business event.
- **TDC required:** every successful and rejected lifecycle action retains actor, timestamp, action, outcome, reason/reference and before/after values where applicable.

## Final acceptance reconciliation

At the end of UAT, reconcile all fresh references created by this script:

1. Approved PO/contract and commitment.
2. Receipt, inspection outcome and GRN/certificate.
3. Stock movements and warehouse/location balances.
4. Valuation layers and Finance journals.
5. Inventory requisition, issue note and receiver/cost lineage.
6. Fixed asset and custody record for the capital item.
7. Return, transfer, adjustment, count variance, supplier return and disposal records.
8. Supplier invoice, three-way match and payment-voucher linkage.
9. DMS documents, versions and access history.
10. Workflow/action history, exception records and audit log.
11. Inventory, valuation, reconciliation and management reports/exports.

Acceptance requires:

- All 22 scenarios are `Pass`, or any `Blocked` scenario has a documented configuration prerequisite rather than an unexplained implementation gate.
- Stock quantity/value, assets, commitments, obligations/actuals and journals reconcile without duplicate exposure.
- No user was required to enter a technical-only identifier.
- Richer implementation extensions did not replace or obstruct the core TDC lifecycle.
- SQL Server persistence evidence confirms stock, valuation, asset, journal, document, workflow and audit records for the fresh UAT references.
- The approved role matrix, thresholds and routing configuration used for the test are retained as evidence; acceptance does not depend on the illustrative role codes or example quantities in this script.

## Defect classification guidance

- **Critical:** cross-tenant leakage, unauthorized posting, duplicate stock/asset/journal, unbalanced Finance posting, lost audit trail or silent bypass of approval.
- **High:** valid core TDC route cannot complete; rejected/discrepant stock becomes available; three-way match or maker-checker is bypassed; unexplained technical gate blocks business processing.
- **Medium:** required report/export, drill-down, document history, controlled selector or correction route is missing or misleading.
- **Low:** presentation, wording or optional extension defect that does not compromise the governed lifecycle.
