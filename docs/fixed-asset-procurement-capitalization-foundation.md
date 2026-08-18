# Fixed Asset Procurement Capitalization Foundation

## Outcome

FIN-INT-007 provides the Finance-owned boundary for turning an accepted procured fixed-asset item into an individually controlled fixed-asset register entry. It resolves `FIN-LIM-0028` without moving purchase-order, receipt or inspection workflow logic into Finance.

## Ownership boundary

- Procurement owns purchase orders, receipts, independent inspections and the accepted-supply integrity hash.
- Inventory owns item classification, receipt valuation movements and stock balances.
- Finance owns asset category and depreciation policy, maker-checker capitalization approval, register/book values, GL reclassification, reversal and audit evidence.

The adapter calls `IProcurementAcceptedSupplyService`; it does not infer that an unapproved receipt is acceptable and never changes Procurement status.

## Accounting flow

1. Accepted receipt posting remains `Dr Inventory Control / Cr GRV Accrual`.
2. FIN-INT-007 capitalizes one approved register asset using `Dr Fixed Asset Cost / Cr Inventory Control` at the posted receipt carrying value.
3. The matched supplier invoice uses `Dr GRV Accrual / Cr Accounts Payable` and does not debit the fixed-asset account again.

This three-event chain preserves receipt accrual accounting and avoids the former duplicate asset/expense risk.

## Operational workflow

1. Query `GET /api/finance/fixed-assets/procurement-capitalization/candidates?purchaseOrderId={id}`.
2. Create one asset draft per individually controlled accepted unit with `POST /api/finance/fixed-assets/procurement-capitalizations` and a retry-safe idempotency key.
3. Submit and approve the generated asset through the existing fixed-asset capitalization maker-checker workflow.
4. Post the handoff with `POST /api/finance/fixed-assets/procurement-capitalizations/{id}/post`.
5. Inspect the resulting asset, posting event and journal through the ordinary Fixed Assets and Journal Inquiry workspaces.

## Controls and evidence

- Only Goods accepted-supply evidence and Inventory items classified as `FixedAsset` are eligible.
- One handoff reserves one accepted unit because the fixed-asset register has individual rather than bulk-quantity identity.
- Repeated Inventory items on separate PO lines are blocked where current valuation movements cannot prove line-specific cost.
- Every contributing accepted receipt needs a posted `ProcurementPurchaseOrderReceipt` Finance event.
- The Procurement snapshot/hash and Finance receipt movement/event evidence are stored on the handoff.
- Posting revalidates the accepted-supply hash and requires the asset to have completed Finance approval.
- The source document and idempotency key are stable, so retries return the original journal.
- A controlled capitalization reversal marks the handoff reversed and releases its source-unit reservation.

## Wow factors for stakeholder demonstrations

- Drill from one asset to the exact accepted Procurement evidence and the immutable Finance journal that created its cost.
- Demonstrate that Finance refuses ordinary stock, pending inspections, missing receipt journals and ambiguous cost evidence.
- Show the balanced three-event accounting chain and prove that the AP invoice does not capitalize the same laptop, vehicle or equipment twice.
- Retry the post safely and show that no duplicate journal or asset transaction is created.

## Deployment and UAT

Apply migration `20260814103000_AddProcurementFixedAssetCapitalization` first. Representative TDC UAT should cover partial receipts, multiple individually tagged units, workflow approval, a matched supplier invoice, journal inquiry, reversal before downstream depreciation, and reconciliation of GRV plus Inventory Control to the asset register.
