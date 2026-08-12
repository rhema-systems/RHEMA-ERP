# Controlled Subledger Opening-Balance Cutover Foundation

## Intent

TDC's go-live opening balance is not only a balanced trial balance. Finance must also be able to explain which supplier invoices make up Accounts Payable, which customer invoices make up Accounts Receivable, and which imported assets make up fixed-asset cost, accumulated depreciation, and net book value.

This slice reuses the existing source-document and central-posting architecture instead of creating a parallel migration ledger:

- AP opening invoices remain canonical AP invoices and therefore appear in supplier aging and settlement.
- AR opening invoices remain canonical AR invoices and therefore appear in customer aging and receipt allocation.
- Imported fixed-asset opening book values can now be converted into a controlled opening-balance batch whose posted journal is linked back to the exact register records.

The result is one auditable chain from cutover source evidence to subledger inquiry and posted GL.

## Stakeholder Summary

The opening-balance workspace now gives Finance a consolidated readiness view of AP, AR, and fixed-asset opening evidence. For fixed assets, the accountant selects the imported register rows, and the server derives the accounts and amounts. A maker-checker-approved batch then posts through the same Finance posting engine as normal accounting activity.

The important control is that approval is attached to frozen evidence. If an imported asset's cost, accumulated depreciation, net book value, category accounts, or opening date changes after preparation, posting is refused and the batch must be prepared again.

## Fixed-Asset Accounting

For each selected asset and book, the generated batch records:

- Debit fixed-asset cost account for opening acquisition cost.
- Credit accumulated-depreciation account for opening accumulated depreciation.
- Credit migration clearing account for opening net book value.

The migration-clearing credit equals cost less accumulated depreciation, so the generated batch balances without a manually maintained suspense amount. The exact `FixedAssetBookValue` identifier is retained on the generated line evidence; account and amount derivation is not delegated to the browser.

## Operational Workflow

1. Load AP and AR opening invoices through their existing opening source-document workflows.
2. Import fixed-asset opening register records with acquisition cost, accumulated depreciation, net book value, and an opening as-of date.
3. Open **Finance > Opening Balances** and review the Subledger Opening Readiness card.
4. Select the fixed-asset rows for the intended opening date, period, and accounting book.
5. Prepare the generated GL batch.
6. Validate and submit the batch through the existing opening-balance maker-checker workflow.
7. An independent approver approves the frozen evidence.
8. Post the approved batch and inspect the linked journal and updated register evidence.
9. Reconcile AP aging, AR aging, and the fixed-asset register to their posted GL control balances before cutover sign-off.

## API Surface

- `GET /api/finance/opening-balances/subledger-readiness` returns tenant-scoped AP, AR, and fixed-asset opening counts, posted counts, totals, candidate rows, and warnings.
- `POST /api/finance/opening-balances/fixed-assets` creates a controlled batch from explicitly selected fixed-asset book-value records.
- Existing opening-balance validate, submit, approve, reject, and post endpoints continue to govern the generated batch.

Viewing readiness requires the ordinary Finance view permission. Preparing a fixed-asset batch requires the dedicated opening-balance preparation permission.

## Integrity And Security Controls

- Every selected asset, book value, account, period, and book is revalidated in the current tenant.
- The request must identify exact fixed-asset book-value IDs; a selection cannot drift between parallel IFRS/Tax books and there is no accidental "post every imported asset" action.
- Already-posted register records cannot be selected again.
- Cost, depreciation, net book value, opening date, and category account mappings are rechecked before posting.
- Generated fixed-asset lines cannot be edited through the generic batch editor.
- The central `IFinancePostingEngine` remains the only journal-creation boundary.
- The posted journal and posting event are linked to the opening batch and the exact fixed-asset register records.
- Existing opening asset transactions are linked to the journal rather than duplicated.

## Verification

The focused `FinanceGoLive-SubledgerOpeningBalances` regression slice covers:

- derived fixed-asset accounting and register/journal linkage;
- refusal when register evidence changes after preparation;
- cross-tenant asset selection refusal; and
- readiness reporting for posted and unposted source evidence.

The Core project build and focused frontend lint also pass. This feature uses existing schema fields, so it requires no new EF migration.

## Scope Boundary

This materially resolves the core source-opening portion of `FIN-LIM-0048`: AP opening invoices, AR opening invoices, and fixed-asset opening register-to-GL evidence. Foreign-currency AP/AR opening invoices use the established canonical invoice posting and immutable FX evidence rules.

`FIN-LIM-0048` remains partially open until TDC confirms and rehearses any specialised cutover requirement for unapplied supplier/customer advances, withholding balances, or withholding-certificate opening records. Final production migration execution, representative-data reconciliation, and accountant sign-off remain under `FIN-LIM-0017`.
