# Controlled Subledger Opening-Balance Cutover Foundation

## Intent

TDC's go-live opening balance is not only a balanced trial balance. Finance must also be able to explain which supplier invoices make up Accounts Payable, which customer invoices make up Accounts Receivable, which imported assets make up fixed-asset cost, accumulated depreciation, and net book value, and which advance and withholding items remain individually manageable after cutover.

This slice reuses the existing source-document and central-posting architecture instead of creating a parallel migration ledger:

- AP opening invoices remain canonical AP invoices and therefore appear in supplier aging and settlement.
- AR opening invoices remain canonical AR invoices and therefore appear in customer aging and receipt allocation.
- Imported fixed-asset opening book values can now be converted into a controlled opening-balance batch whose posted journal is linked back to the exact register records.
- Unapplied supplier/customer advances become canonical payment lots without replaying historical cash.
- Unremitted AP withholding and outstanding AR withholding certificates become canonical statutory evidence linked to controlled opening journals.

The result is one auditable chain from cutover source evidence to subledger inquiry and posted GL.

## Stakeholder Summary

The opening-balance workspace now gives Finance a consolidated readiness view of AP, AR, fixed-asset, advance, and withholding opening evidence. For fixed assets and specialised balances, the server derives the controlled accounts and amounts from tenant configuration rather than allowing freehand GL routing. A maker-checker-approved batch then posts through the same Finance posting engine as normal accounting activity.

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
9. In **Advances & WHT**, prepare each supplier advance, customer advance, unremitted AP WHT item, and outstanding AR WHT certificate from its legacy source reference.
10. Validate, approve, and post those generated batches; no historical cash movement is replayed.
11. Rebuild the AP/AR settlement read model and reconcile advance lots, WHT evidence, aging, and the fixed-asset register to posted GL before cutover sign-off.

## API Surface

- `GET /api/finance/opening-balances/subledger-readiness` returns tenant-scoped AP, AR, and fixed-asset opening counts, posted counts, totals, candidate rows, and warnings.
- `POST /api/finance/opening-balances/fixed-assets` creates a controlled batch from explicitly selected fixed-asset book-value records.
- `GET /api/finance/opening-balances/specialized-options` returns tenant-scoped suppliers, customers, functional currency, and active WHT mappings.
- `POST /api/finance/opening-balances/supplier-advances` and `customer-advances` create canonical unapplied advance lots and their balanced cutover journals.
- `POST /api/finance/opening-balances/ap-withholding` and `ar-withholding` create canonical statutory WHT/certificate evidence and their balanced cutover journals.
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
- Specialised source records are linked only after central posting succeeds and generated lines cannot be edited.
- WHT accounts must match the payable/receivable mapping on the selected WHT tax master; arbitrary active accounts are rejected.
- Foreign-currency advances require approved exchange-rate evidence and retain native and functional amounts separately. Statutory WHT openings use functional currency.
- Supplier/customer and tax lookups are tenant-scoped; cross-tenant identifiers cannot be used to prepare a batch.

## Verification

The focused `FinanceGoLive-SubledgerOpeningBalances` and `FinanceGoLive-SpecializedOpeningBalances` regression slices cover:

- derived fixed-asset accounting and register/journal linkage;
- refusal when register evidence changes after preparation;
- cross-tenant asset selection refusal; and
- readiness reporting for posted and unposted source evidence.
- supplier/customer advance posting and visibility in the existing unapplied-settlement workspace;
- AP WHT remittance and AR certificate evidence integration; and
- rejection of WHT accounts that do not match the tax master.

The backend and test projects compile and focused Finance regression tests pass. The specialised source linkage and native-amount evidence require migration `20260812053000_AddSpecializedSubledgerOpeningCutover`.

## Scope Boundary

This resolves the implementation scope of `FIN-LIM-0048`: AP opening invoices, AR opening invoices, fixed-asset opening register-to-GL evidence, unapplied supplier/customer advances, unremitted AP WHT, and outstanding AR WHT certificates now have canonical source records and controlled posting paths. Foreign-currency invoices and advances use immutable FX evidence rules.

TDC confirmed that the specialised data shapes are present at cutover. The remaining activity is representative-data rehearsal, reconciliation, and accountant approval under `FIN-LIM-0017`; that acceptance work is not a missing product feature.

## Legacy retirement and controlled correction

Manual-journal `Opening Balance`, `ALL_ACTIVE_BOOKS` opening posting, subledger opening adjustments, and the Finance Settings auto-routing toggle are retired runtime paths. Historical journals remain readable and can use their existing historical correction controls, but new cutover facts must enter through the controlled Opening Balances workspace or the canonical AP/AR opening-invoice processes.

Every controlled opening-balance type now requires a real pending maker/checker approval workflow; missing workflow integration and completed-at-start workflows fail closed. A posted controlled batch can be corrected only through a separate opening-batch reversal request. The request freezes the original batch, journal, posting event, date, book, source kind, and totals. An independent reviewer must approve it before the central posting engine creates the compensating journal.

Source-aware reversal checks preserve operational evidence:

- reverse the residual GL/equity close-out before upstream opening sources;
- bank snapshots must still equal the posted cutover amount;
- supplier/customer advances must be unallocated;
- issued WHT certificate evidence must first be cancelled;
- fixed assets must have no later depreciation, valuation, transfer, disposal, or other downstream accounting.

The schema changes are supplied by `20260831234500_RetireLegacyOpeningBalanceAutoRouting` and `20260901003000_AddOpeningBalanceBatchReversals`. They are migration source only until deliberately applied in the target environment.
