# Finance report output standard

## Required user contract

Every user-facing Finance report must provide:

1. **Download PDF** — a server-rendered, tenant-scoped artifact generated from the same canonical
   report read model used by the screen.
2. **Print** — the same PDF opened through the isolated document print pipeline. The application
   shell, sidebar, filters, and action buttons must never form part of the printed document.
3. **Data export where useful** — CSV or XLSX is supplementary for analysis and reconciliation; it
   does not replace PDF or Print.

All three outputs must preserve the selected dates, book, entity filters, dimensional filters, and
presentation-currency choice. Unlike currencies must remain separate unless an explicitly labelled
functional-currency conversion is part of the canonical report.

## Security and evidence

- PDF, Print, CSV, and XLSX require `Finance.Reports.Export` unless a stricter controlled-document
  permission applies.
- The backend must resolve the current tenant and must never accept tenant identity from browser
  report parameters.
- A printable builder maps an existing canonical report DTO; it must not independently reconstruct
  balances from transaction tables.
- Generated documents show the tenant, report title, selected period/as-at date, generation time,
  actor, page numbers, and relevant currency or book context.
- Transaction documents such as vouchers, receipts, invoices, purchase orders, and tax
  certificates retain their stronger document-specific issuance and replacement controls.

## Coverage register

| Finance surface | PDF | Print | Data export | Delivery |
| --- | --- | --- | --- | --- |
| Trial balance, income statement, balance sheet, cash flow | Yes | Yes | Existing | Existing controlled builders |
| Multi-currency detail and detailed ledger | Yes | Yes | Existing | Existing controlled builders |
| AP aging and cash requirements | Yes | Yes | CSV | Finance report-output phase 1 |
| AP supplier statements / detailed ledger | Yes | Yes | CSV/XLSX | Finance report-output phase 1 |
| AP match exceptions | Yes | Yes | CSV | Finance report-output phase 1 |
| Procurement/Finance reconciliation | Yes | Yes | CSV | Finance report-output phase 1 |
| AR aging | Yes | Yes | CSV | Finance report-output phase 1 |
| AR customer statements / detailed ledger | Yes | Yes | CSV | Finance report-output phase 1 |
| Fixed-asset register, disposals, and transfers | Yes | Yes | XLSX | Finance report-output phase 1 |
| Cash position | Yes | Yes | Existing where applicable | Finance report-output phase 2; monetary totals are explicitly functional/base-currency equivalents |
| Input/output VAT registers, VAT reconciliation, and WHT payable summary | Yes | Yes | Existing where applicable | Finance report-output phase 2; canonical Tax reporting services replace legacy stub actions |
| Budget consolidated and scenario comparison reports | Yes | Yes | CSV where applicable | Finance report-output phase 2; scenario scope and approved/working selection are preserved |
| WHT statutory certificate | Existing controlled print | Existing controlled print | N/A | Document-specific issuance/version lifecycle; retained-PDF delivery is tracked separately |
| WHT statutory certificate register | Yes | Yes | CSV | Finance report-output phase 3; certificate, payment, status, and remittance lineage are preserved |
| WHT remittance register | Yes | Yes | Existing where applicable | Finance report-output phase 3; batch lifecycle and underlying liability evidence are preserved |

Rows marked **Required** are release-tracked gaps, not exceptions to the standard. A button labelled
Export that has no handler, a whole-page `window.print()`, or a CSV-only report does not satisfy
this contract.

The Cash Reports hub currently exposes Cash Position as its report surface, and that report is
covered above. Cash-account, deposit, till, reconciliation, and transaction pages are operational
workflows rather than report-only gaps; any future report added to those workflows must satisfy this
standard when it is introduced.
