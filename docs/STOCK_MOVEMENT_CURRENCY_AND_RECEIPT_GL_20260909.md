# Stock movement currency and receipt GL trace — 9 September 2026

## Scope

Movement history, summary and detail amounts no longer embed a dollar symbol. The Inventory stock-movement response includes the current tenant's functional currency from `FinanceSettings.BaseCurrency`; the UI uses that returned currency. Missing currency is not guessed, and mixed-currency results are not summed into one total. No migration, posting rules, Finance code, permissions or account mappings are changed.

The first browser check caught HTTP 403 on the separate Finance currency endpoint for `manager`. The final implementation avoids that dependency: stores users receive only currency display metadata through the existing authorized Inventory endpoint. It does not expose Finance settings or grant Finance access.

## Actual rehearsal receipt posting (read-only verification)

- Database: `RhemaERP_PO_Rehearsal_20260909`; receipt REC260003, ID `490b5d42-1a70-438a-9240-08de2223df82`.
- Source action: `PostAcceptedInventoryReceipt`, status Posted, at `2026-09-09 17:50:32 UTC`, requested by `procurementapprover`.
- Journal ID: `b98d1763-7376-4667-a10e-0c2cad76bb67`.
- Debit: GHS 52,000 to Inventory, code **1200**, account number **001-000-1200**.
- Credit: GHS 52,000 to GRV Accrual Control, code **2110**, account number **000-2110-0000**.
- Account selection comes from `dbo.FinanceSettings.ControlAccountInventoryId` and `.ControlAccountGRVAccrualId`, joined to `dbo.Accounts.Id`. These are configurable under Finance → Settings, not selected on the receipt page or hard-coded account IDs.
- `ProcurementReceiptInspectionService` calls the existing `IInventoryReceiptFinancePostingService` after final inspection approval posts the accepted stock. `InventoryReceiptFinancePostingService` uses the above mappings and the central Finance posting engine. If a purchase-price variance exists, it additionally uses the configured write-off expense account; this receipt journal has only the two lines above.
- This posting precedes the draft-only landed-cost test. Saving or allocating landed costs does not itself create this receipt journal. Allocate distributes shared costs by their chosen method and keeps item-specific costs on their target; posting is separate.

## Validation

- Six frontend currency tests passed (history, summary, detail, alternate currency, missing-currency retry, mixed-currency total).
- Focused TypeScript check passed.
- API build passed (0 errors); four focused controller tests passed, covering both movement sources and tenant-scoped currency resolution.
- Runtime deployment and the final browser check are paused at the user's request to prioritize landed-cost allocation/posting rehearsal. The Inventory API was not restarted or replaced. The preview frontend has the display changes, so currency values will remain unavailable until the corresponding API change is loaded. Main UAT runtime was not updated.
