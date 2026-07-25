# Cutover Input Collection Pack

Target database: `RhemaERP_UAT_DryRun`

Target tenant: `Default Tenant`

Tenant ID: `00000000-0000-0000-0000-000000000001`

Purpose: collect the approved accounting and product inputs needed to rerun the tenant-level dry-run sign-off pack.

This pack does not post anything, mutate UAT data, run repairs, run rebuilds, or execute the sign-off pack.

## Current Status

- UAT schema is aligned through migration `20260709120000_AddOpeningBalancePostingFoundation`.
- Required sign-off tables are present.
- The previous representative cutover attempt remains No-Go because approved cutover inputs were not supplied.
- The workbook `outputs/opening-balance-demo/rhema-opening-balance-demo.xlsx` is demo data only and must not be used as approved cutover evidence.
- `ALL_ACTIVE_BOOKS` is not allowed for controlled opening balances. Use an explicit book classification such as `IFRS`.

## 1. Cutover Context Form

Accounting/product must provide:

- Tenant name
- Tenant ID
- Approved cutover date
- Approved fiscal period
- Book classification, for example `IFRS`
- Preparer name
- Reviewer/accountant name
- Approval date

Record the approved context in every CSV template, or attach a signed approval note that references the same tenant, cutover date, fiscal period, and book classification.

## 2. Opening Trial Balance Template

Use `opening-trial-balance-template.csv`.

Required fields:

- Account code
- Account name
- Debit amount
- Credit amount
- Currency
- Segment/dimension string, if applicable
- Reference/notes

Validation rules:

- Total debits must equal total credits.
- Book classification must be explicit and must not be `ALL_ACTIVE_BOOKS`.
- Every account must map to a same-tenant active posting account.
- Opening balances must post through `IFinancePostingEngine`.
- No direct `Account.Balance` mutation is allowed.
- No direct `BankAccount.CurrentBalance` mutation is allowed.

## 3. AP Cutover Declaration

Use `ap-cutover-declaration-template.csv`.

Declare yes/no and attach data files where applicable for:

- Open AP invoices
- Supplier advances
- Unapplied AP payments
- AP WHT certificate balances
- Foreign-currency AP balances

Warning: AP opening balances cannot be faked through GL-only opening balances if AP aging or AP control sign-off is required. Open AP balances must be loaded through supported posted AP source-document/import paths.

## 4. AR Cutover Declaration

Use `ar-cutover-declaration-template.csv`.

Declare yes/no and attach data files where applicable for:

- Open AR invoices
- Customer advances
- Unapplied AR receipts
- AR WHT/VAT withholding certificate balances
- Foreign-currency AR balances

Warning: AR opening balances cannot be faked through GL-only opening balances if AR aging or AR control sign-off is required. Open AR balances must be loaded through supported posted AR source-document/import paths.

## 5. Fixed Asset Cutover Declaration

Use `fixed-asset-cutover-declaration-template.csv`.

Declare yes/no and attach data files where applicable for:

- Fixed asset opening register required
- Asset cost opening balances
- Accumulated depreciation opening balances
- Impairment opening balances
- Revaluation reserve balances
- Asset location/custodian/segment details

Warning: fixed asset opening balances cannot be faked through GL-only opening balances if asset register or fixed asset reconciliation sign-off is required. Fixed asset openings must reconcile to posted GL and fixed asset register evidence.

## 6. Bank/Cash Cutover Declaration

Use `bank-cash-cutover-declaration-template.csv`.

Declare yes/no and attach data files where applicable for:

- Bank balances only
- Detailed cashbook history required
- Unreconciled bank items
- Foreign-currency bank accounts

Warning: bank snapshots are read-side only and must reconcile to posted GL.

## 7. Limitation Acceptance Matrix

Use `limitation-acceptance-matrix-template.csv`.

For each open limitation, classify:

- Applies to this tenant? yes/no
- Accepted non-blocking? yes/no
- Go-live blocking? yes/no
- Owner
- Target date
- Accounting/product approval
- Notes

Do not leave a required limitation unclassified. A limitation is blocking until accounting/product explicitly marks it accepted non-blocking or not applicable for this tenant.

## 8. FIN-LIM-0048 Decision

`FIN-LIM-0048` is the specific subledger opening-balance decision.

Accounting/product may mark `FIN-LIM-0048` not applicable for this tenant dry-run only if all of these are true:

- No open AP invoices are required at cutover.
- No open AR invoices are required at cutover.
- No supplier/customer advances or unapplied AP/AR cash are required at cutover.
- No WHT/VAT withholding certificate balances are required at cutover.
- No foreign-currency open AP/AR balances are required at cutover.
- No fixed asset opening register is required at cutover.

If source-level openings are required, the dry-run remains No-Go until those openings are loaded through proper posted source-document/import paths.

GL-only opening balances must not be used to fake AP aging, AR aging, or fixed asset register balances.

## 9. Ready-to-Rerun Checklist

Before rerunning tenant-level dry-run sign-off:

- Cutover context is approved.
- Opening TB template is complete and balanced.
- Account mappings have been checked against same-tenant active posting accounts.
- AP declaration is complete.
- AR declaration is complete.
- Fixed asset declaration is complete.
- Bank/cash declaration is complete.
- Limitation acceptance matrix is complete.
- `FIN-LIM-0048` is explicitly marked not applicable or blocking for this tenant.
- Demo data has not been used as approval evidence.
- `ALL_ACTIVE_BOOKS` does not appear in the approved input pack.

## 10. Next Action After Completion

After accounting/product completes and approves this pack, rerun the tenant-level dry-run sign-off against `RhemaERP_UAT_DryRun`.

The rerun should then load approved UAT data, post approved opening balances through `IFinancePostingEngine`, run diagnostics/rebuilds only as approved, generate the evidence manifest, and return a new Go/Conditional Go/No-Go recommendation.
