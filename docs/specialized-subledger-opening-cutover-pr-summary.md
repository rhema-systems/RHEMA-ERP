# Specialised Subledger Opening Cutover — PR Summary

## Outcome

This slice resolves the product implementation portion of `FIN-LIM-0048` after TDC confirmed that cutover includes unapplied supplier advances, customer advances, unremitted withholding tax, and outstanding withholding certificates.

Finance users can prepare these balances in **Finance > Opening Balances > Advances & WHT**. Each preparation creates both a canonical AP/AR source record and a balanced opening batch. The approved batch posts through `IFinancePostingEngine`; it does not replay cash that moved before RHEMA ERP go-live.

## Accounting Treatment

| Opening fact | Debit | Credit | Continuing operational evidence |
| --- | --- | --- | --- |
| Supplier advance | Configured supplier-advance asset | Migration clearing | Unapplied `VendorPayment` advance lot |
| Customer advance | Migration clearing | Configured customer-advance liability | Unapplied `CustomerPayment` advance lot |
| Unremitted AP WHT | Migration clearing | WHT payable from selected tax master | Vendor-payment WHT remittance evidence |
| Outstanding AR WHT certificate | WHT receivable from selected tax master | Migration clearing | Customer-payment certificate evidence |

Foreign-currency advances preserve native and functional values separately and require an approved exchange-rate record. Statutory WHT openings are restricted to the functional currency.

## Controls

- Tenant-scoped supplier, customer, tax, account, period, and exchange-rate validation.
- WHT account must equal the payable or receivable account approved on the tax master.
- Generated source-facing and migration-clearing lines cannot be edited after preparation.
- Source evidence is revalidated before approval/posting to detect amount, account, or linkage drift.
- Canonical payment evidence is marked cleared and linked to GL only after central posting succeeds.
- Opening advances flow into the existing unapplied settlement read model; WHT records flow into existing remittance/certificate inquiries.
- Existing opening-balance permissions and maker-checker workflow govern preparation and posting.

## Schema

Migration `20260812053000_AddSpecializedSubledgerOpeningCutover` adds opening-batch linkage/type/source fields to `VendorPayments` and `CustomerPayments`, plus transaction-currency debit/credit evidence to `OpeningBalanceLines`. It intentionally changes only those focused fields; no migration has been applied to a database as part of this code slice.

## Verification

Regression coverage verifies:

- supplier and customer advances post and remain available as unapplied lots;
- AP WHT and AR certificate openings feed the established statutory evidence workflows;
- arbitrary GL accounts cannot replace the tax-master WHT mapping; and
- the existing controlled opening-balance suite continues to pass.

The Finance owner handbook page `features/subledger-opening-balance-cutover.html` has also been updated in the local, git-ignored handbook artifact.

## Remaining Acceptance Work

`FIN-LIM-0048` is implementation-resolved. TDC must still run representative cutover data through UAT, reconcile source registers and control accounts, and obtain accountant sign-off under `FIN-LIM-0017`.
