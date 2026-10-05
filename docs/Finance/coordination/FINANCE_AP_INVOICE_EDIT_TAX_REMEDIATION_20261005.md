# Finance AP/AR Invoice Edit Tax Remediation - 2026-10-05

## Objective

Diagnose and remediate the draft AP invoice edit path where Save Changes can appear unresponsive and an explicit no-tax edit previews correctly but reloads and prints with tax restored. Confirm and correct the mirrored AR invoice edit contract.

## Scope and source evidence

- Branch: `codex/finance-uat-remediation-20261004`
- Worktree: `.w/fin-uat-remediation-20261004`
- Exact base: `7acca585c8be5f8bef241232da705cc73aedb004`
- UAT screenshots: draft invoice `VI-2026-00004` for Volta Office Solutions Ltd; subtotal GHS 2,400.00, tax GHS 180.00, total/balance GHS 2,580.00.
- User evidence: selecting no tax recalculates the browser totals to zero tax, but save/reload and print restore tax; due-date edits persist.
- AR confirmation: the AR form also submitted a null taxGroupId without taxTreatment, so the API defaulted the line to TaxTreatment.Standard and exposed the same server-side fallback behavior.

## Root cause

The edit form represented explicit no tax as a null `taxGroupId` while retaining `TaxTreatment.Standard`. The UI preview treated null as zero tax. The server tax engine intentionally treats a standard line with no explicit group as eligible for rule/default tax resolution, so the configured purchase tax was recalculated during persistence and appeared again in the detail and print views.

The Save Changes button could also be disabled without an explanatory state while supplier defaults were loading and the persisted invoice had no withholding decision. Supplier defaults are advisory during draft edit and must not block the save action.

## Implemented safe remediation

- Normalize explicit or inherited no-tax line selections to `TaxTreatment.Exempt` with `taxGroupId = null`.
- Preserve explicit Exempt, ZeroRated, and OutOfScope treatments.
- Keep configured tax groups standard-rated by default.
- Do not disable draft edit saves while supplier defaults load.
- Share the normalization contract across AP and AR.
- Add focused frontend normalization tests and backend AP/AR persistence regression tests.

## Changed files

- frontend/src/lib/finance/invoice-tax-selection.ts
- frontend/src/lib/finance/invoice-tax-selection.test.ts
- frontend/src/components/finance/ap/VendorInvoiceFormPage.tsx
- frontend/src/app/finance/ar/invoices/new/page.tsx
- frontend/src/types/ar.ts
- tests/ErpSystem.Api.Tests/Services/Finance/ApInvoicePostingMigrationTests.SupplierTax.cs
- tests/ErpSystem.Api.Tests/Services/Finance/ArInvoicePostingMigrationTests.cs
- this ledger

## Migrations and data

- Database migrations: none.
- UAT data mutation: none.

## Verification

- Focused frontend tests: passed 12/12 (invoice-tax-selection.test.ts and exchange-rates/page.test.ts).
- ESLint for all changed Finance frontend files: passed with no findings.
- Focused AP/AR backend persistence contracts: passed 2/2.
- Full frontend type-check remains blocked by existing unrelated baseline errors. No new error identifies the shared tax-selection helper, its new fields, or the exchange-rate changes. The generated Next route type for the AR page continues to reject its pre-existing named InvoiceFormPage export.
- Final git diff --check: pending after the ledger update.

## Authorization boundaries

Local diagnosis, implementation, tests, commit, and push to the already-open PR #352 are in scope. The user separately authorized manually restarting the local API from the remediation worktree; no restart was performed by Codex. This workstream does not authorize merging PR #352, bypassing the master-only deployment guard, deploying, applying migrations, or mutating UAT data.
