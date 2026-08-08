# TDC AP Supplier Statement - Controlled PDF and Spreadsheet Output

**Implemented:** 2026-08-03  
**Work package:** WP4 - AP vouchers, supporting evidence, statements, and WHT  
**Scope:** Finance-owned supplier statement presentation only; no Procurement, bank, mobile-money, tax-authority, or other module interface

## Outcome

RHEMA ERP now produces controlled supplier statements in PDF and native XLSX formats from the
same AP detailed-ledger result used by the Finance report screen and existing CSV export. The
renderer does not maintain a second invoice, payment, WHT, adjustment, currency, or balance
calculation path.

The obsolete `IApReportsService.ExportSupplierStatementAsync` contract, which always threw
`NotImplementedException`, has been removed. The older single-supplier JSON endpoint now maps
from `GetSupplierDetailedLedgerAsync`, closing the prior risk that screen, API and export could use
opposite debit/credit conventions.

## Statement controls

- The parameterized document type is `Finance.AP.SupplierStatement`.
- The generic document controller requires `Finance.Reports.Export` before PDF or XLSX rendering.
- Tenant identity comes from the authenticated tenant. Supplier selection is resolved by the
  tenant-scoped AP report service; the renderer does not accept a client tenant identifier.
- Period validation rejects an end date before the start date. The TDC default is the current
  calendar month when dates are not supplied.
- The on-screen report, CSV, PDF and XLSX use the same supplier selection and optional
  supplier-currency presentation basis.
- Every successful output records `Finance.Report.SupplierStatementExported`, including format,
  period, supplier/line counts, control totals, warning count, emitted byte length and SHA-256
  hash. Failed generation records `Finance.Report.ExportFailed`.
- Statement output is read-only. It creates no AP transaction, allocation, WHT settlement,
  document number, approval, posting event or journal.
- Both formats explain the AP control-account convention: credits increase the payable; debits
  such as payments, discounts, WHT and supplier credits reduce it.
- Every output states that the statement is not a payment instruction.

## PDF output

The PDF is A4 landscape and contains:

- TDC/tenant identity and statement period;
- supplier identity, code and statement currency;
- opening balance, period debits, period credits and closing balance;
- dated transaction type, canonical document number, reference, description and running balance;
- currency presentation warnings where direct supplier-currency values are unavailable;
- generation actor/time, controlled-output label and page numbering.

Batch selection is supported. Each supplier starts a separate page section, and long transaction
tables flow across pages with repeated headers. Standard font ligatures are disabled so retained
PDF text remains searchable and does not extract with null glyphs.

Direct route:

```text
GET /api/documents/Finance.AP.SupplierStatement
    ?format=pdf
    &fromDate=2026-07-01
    &toDate=2026-07-31
    &supplierIds=<comma-separated IDs>
    &showSupplierCurrency=false
```

## Native spreadsheet output

The XLSX is generated in the backend with typed dates and amounts. It is not HTML, CSV with an
`.xlsx` extension, or a browser-only table dump.

- `Summary` shows selected basis, supplier totals, a formula-driven difference, per-supplier
  PASS/FAIL and an aggregate control status.
- Each supplier has a named worksheet containing metadata, statement summary, filterable detail,
  frozen headers and explicit date/money formats.
- Running balances are live formulas using `prior balance - debit + credit`.
- Closing totals and the reconciliation difference are formulas, making later edits visible rather
  than preserving a misleading precomputed PASS.
- The workbook uses compact TDC-aligned dark-blue/light-blue hierarchy, restrained borders,
  readable widths and hidden gridlines.

## User workflow

The AP Reports > Supplier Statements screen now exposes CSV, PDF and Excel actions to users with
`Finance.Reports.Export`. The selected period, suppliers and currency toggle are passed unchanged
to all three output paths. Users without export permission can still run the report when otherwise
authorized, but the export actions are not shown and the API remains authoritative.

## Verification

- API project build passes with zero errors.
- Four focused tests cover PDF bytes/canonical-service use/audit, native XLSX structure and live
  formulas, invalid-period failure audit, and explicit Finance export policy.
- The sample PDF was rendered at 150 DPI and visually inspected: one A4 landscape page, no clipped
  or overlapping content, corrected opening-date row, and searchable text with no null glyphs.
- The production XLSX was imported with the bundled spreadsheet verifier. Both worksheets were
  rendered and visually inspected; typed source cells, 22 formulas, filters, formula PASS controls
  and zero formula-error matches were verified.

## Limitation disposition

- `FIN-LIM-0001`: the resolved settlement aging/control foundation is not changed. Statement
  presentation continues to use the existing canonical AP detailed-ledger service.
- `FIN-LIM-0002`: extended beyond the resolved CSV foundation for this AP statement subset with
  controlled PDF/XLSX output, permission enforcement and generation/failure audit.
- `FIN-LIM-0004`, `FIN-LIM-0019` and `FIN-LIM-0054`: existing WHT statement lines are preserved;
  WHT threshold, remittance and managed certificate lifecycle work remains the next WP4 slice.
- `FIN-LIM-0014`: no bank-account number or new bank-resource path is disclosed. Authenticated
  tenant isolation and explicit Finance export permission are enforced.
- `FIN-LIM-0018`: statement output is read-only and cannot write to GL or bypass the posting engine.
- `FIN-LIM-0045`: supplier advances/unapplied payments remain visible through the existing AP
  detailed-ledger path; the renderer does not fabricate invoice allocations.
- `FIN-LIM-0046`: resolved for the AP supplier-statement PDF/native-XLSX subset. It remains accepted
  non-blocking for other rich Finance report packs not yet selected for product delivery.
- `FIN-LIM-0047`: statutory filing packs and full certificate workflow remain open and are not
  represented by this commercial supplier statement.

No database migration or seeded data was required because this slice extends existing AP report,
document-output, permission and audit foundations.
