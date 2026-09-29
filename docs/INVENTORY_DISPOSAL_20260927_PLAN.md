# Disposal implementation checkpoint

Requirements 12–14 remain in progress. This design is not acceptance evidence.

## Existing owners and required compatibility

`InventoryDisposalService` already stages an inventory StockAdjustment and its Finance producer approval. Auction and historical Sale currently form a C8 group with a direct cash/recovery posting. `InvoiceService` in Finance AR owns non-supplier invoices, tax, counterparty profiles, numbering, dimensions and posting. Procurement AP must not be used for auction collection.

New Sale disposal creation and changing another method to Sale must be rejected server-side. Existing Sale records, including their idempotent create replay and historical posting lineage, remain readable. The frontend keeps the historical selected method but removes Sale from other method choices.

## Waybill

Reuse QuestPDF, already used by Inventory transfer documents. The endpoint must load the disposal through `IInventoryDisposalService.GetByIdAsync` so tenant and warehouse authorization run before rendering. Permit Approved, ReadyForExecution, AdjustmentPending and Completed non-sales cases only. Include number, method, stage, date, warehouse, recipient, reference, item/tracking/bin/quantity/UOM, preparer and approval information. Existing disposal schema has no carrier/vehicle identity; provide clearly unfilled handover fields rather than inventing values. This document does not post or issue stock.

The frontend download, authorized PDF endpoint and server Sale guards are implemented. The final focused frontend run passed 22/22 tests: disposal page 18, auction dialog 3 and item accounts 1. The narrow TypeScript check completed with zero diagnostics. Backend stage/permission/PDF, auction idempotency and account-preview cases are written and queued for the coordinated API test run. PDF visual and live lifecycle checks remain pending.

## Auction and item account integration

Add a typed, tenant-validated disposal-to-AR invoice link with a unique disposal claim and stable line identities, protected by the existing disposal transaction lock. Creating the invoice produces one canonical AR draft, never an AP invoice and never a second stock movement. Replays return the same invoice; changed payload under the same key fails. A manually deleted/cancelled linked invoice must not silently generate another receivable.

Use an explicit new accounting mode for new auction cases. Preserve existing grouped C8/direct-proceeds authority for historical cases; do not reinterpret previously staged or posted events. For new auction mode, remove direct proceeds from the disposal group and retain independent Finance approval of the inventory-value effect. Update all method-based group/approval identity selectors consistently.

Add a typed Item Inventory Disposal Account selector and tenant/active/postable validation. The inventory-value posting builder must derive this account from the disposal's authoritative item source; do not expose a general arbitrary account override on adjustments. Preview and execution must share this resolution.

Accounting sequence through existing owners:

1. Disposal adjustment: debit item disposal account, credit original inventory account at retained carrying value.
2. Auction AR invoice: debit receivable, credit item disposal account for proceeds; tax uses the selected configured treatment. Lines are non-stock GL lines, avoiding a second issue.
3. Existing AR receipt/payment: debit selected cash/bank and credit receivable.

The resulting disposal balance reflects gain/loss without duplicate cash or revenue. Preserve Finance's dimension-route registry and source protections; adding an Inventory auction producer route requires matching validation in AR and route/catalog tests.

## Implemented source checkpoint and SQL evidence

New cases retain `AccountingVersion=1`; existing cases remain version 0 and keep their original case hash and direct-proceeds authority. The typed `InventoryDisposalAuctionInvoice` link claims each case and invoice once. Route 78 (`inventory.disposals.auction-invoices`) uses the canonical AR owner with matching readiness and payment dimension adapters. Invoice creation shares the outer disposal transaction and stable non-stock line identities. The compact dialog requires an explicit tax treatment and uses Finance's two-decimal unit-price precision; validation failures retain inputs and the retry key.

New item disposal accounts are typed Expense/Revenue accounts, active, directly postable and non-control. Auction cost posting retains the account captured on the generated invoice even if the item profile later changes. Missing accounting profiles fail closed. Normal stock adjustments and historical disposal accounting retain their existing behavior.

A pre-existing staging defect was found: staging set `StockAdjustmentId` before the StockAdjustment row existed, violating its FK and SQL lineage trigger. Staging now retains a unique `PreparedStockAdjustmentId`; completion sets the actual FK only after the controlled owner creates/posts that exact adjustment. Case hashes for the new accounting version include the prepared identity. The additive trigger update accepts this prepared stage without weakening completion's posted-adjustment check.

`Test-InventoryDisposalAccountingSqlGuards.ps1` installs the exact four production triggers against an isolated schema copy. The run at `tmp/disposal-guards-20260927_121725_2eba4632.json` passed all 28 checks (four trigger compilations and 24 behavioral checks). It verified matching .NET/SQL stable line identities, quantity/account/price/header/link immutability, retained legacy identity, staged-versus-posted adjustment lineage and ordinary AR status/payment updates, active-invoice cancellation denial, status-only void denial, and rejection of empty or altered invoice-economics snapshots. This is not full EF migration or HTTP lifecycle evidence.

The existing general AR void implementation does not produce a governed reversal. New auction invoices therefore allow draft cancellation only before stock preparation, without subtracting an unposted draft from customer balances. Posted or staged auction cancellation fails closed with a request for a governed Finance reversal/disposal reconciliation; this change does not pretend the legacy void method reverses GL.

## Remaining validation

2026-09-27 verification update: the combined API run passed 313 of 314 cases, including the disposal service, controller/waybill, historical auction, item-account and Sales posting cases. The remaining disposal C7 SQLite rollback fixture failed during schema creation because SQLite lacked SQL Server's EOMONTH function; its provider adapter has been corrected and the case is being rerun. This is not yet an all-green result.

The real controller's waybill test exported `tmp/disposal-waybill-preview.pdf` (one A4 page), rendered to PNG and visually inspected. The document number, warehouse, recipient, bin, quantity/UOM, preparer, approval status and handover signature fields are readable with no clipping. This verifies the generated test-fixture document; authenticated live waybill acceptance remains pending.

- Waybill stage, tenant, warehouse and PDF output checks.
- New Sale rejection, historical read and exact replay compatibility.
- Concurrent duplicate auction requests and changed-payload rejection.
- Non-stock invoice lines, buyer role/profile and tax/account validation.
- Matching preview/post distributions, one inventory issue, one receivable, no legacy direct-proceeds event in new mode.
- Existing Sales invoice distribution behavior and payment account selection.
- New migration fresh/upgrade, historical case compatibility, complete normal build and live authorized lifecycle.
