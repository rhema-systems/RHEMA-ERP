# RHEMA Mobile POS accounting certification

## Objective

MPOS-0803 certifies that a completed Mobile POS sale remains a source envelope around canonical RHEMA Finance documents. The mobile application does not keep an accounting ledger. The certification reconciles the source sale and tenders to AR invoices, posted customer payments, allocations, till sessions, optional bank-deposit proposals, and mutation replay evidence.

## Read-only certification command

Run against an authorized UAT copy after the Mobile POS migrations are applied and at least one controlled sale has completed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/acceptance/Invoke-MobilePosAccountingReconciliation.ps1 `
  -SqlServer 'SERVER\INSTANCE' `
  -DatabaseName 'RhemaERP_UAT' `
  -TenantId '00000000-0000-0000-0000-000000000000' `
  -FromUtc '2026-10-10T00:00:00Z' `
  -ToUtc '2026-10-11T00:00:00Z'
```

The window is half open: `FromUtc` is included and `ToUtc` is excluded. Integrated Windows authentication is used. The wrapper rejects system databases, validates all target parameters, runs only the committed reconciliation SQL, and writes local evidence beneath ignored `.artifacts/mobile-pos/accounting-reconciliation/`.

`-AllowNoCompletedSales` is available only for a schema/dry-run check. It is not valid certification evidence.

## Controls

| Control | Required result |
| --- | --- |
| MPOS-ACC-001 | Every completed sale has a canonical invoice, sync timestamp and valid header arithmetic. |
| MPOS-ACC-002 | Active line subtotal, tax and discount reproduce the sale snapshot. |
| MPOS-ACC-003 | At least one completed tender exists and the tender total equals the sale total. |
| MPOS-ACC-004 | Invoice tenant, customer, currency and amounts equal the sale; journal and source-book authority are linked. |
| MPOS-ACC-005 | Each completed tender links to one posted same-tenant CustomerPayment with matching customer, method, currency, amount and destination. |
| MPOS-ACC-006 | Each tender payment has exactly one active non-reversal allocation to the sale invoice for the tender amount. |
| MPOS-ACC-007 | The completed mutation receipt resolves to the same sale and canonical invoice. |
| MPOS-ACC-008 | A finalized Mobile POS close belongs to the same closed Finance till session and liquidity account. |
| MPOS-ACC-009 | An optional deposit proposal links to a same-tenant deposit, retains its immutable source marker and reconciles allocations to the deposit net amount. |
| MPOS-ACC-010/011 | Active mutation identities and canonical tender-payment links remain unique. |

## Certification scenario

1. Open one cashier session on an approved physical till.
2. Complete a walk-in cash sale and confirm the store default customer is used.
3. Complete an approved-customer split-tender sale using two configured methods.
4. Queue an offline cash sale, interrupt response delivery, reconnect and synchronize the exact mutation twice.
5. Submit the denomination count, resolve any retained sync evidence independently, approve the till close, and create the bank-deposit proposal.
6. Record the tenant ID and an exact UTC window covering these records.
7. Run the reconciliation command without `-AllowNoCompletedSales` and retain its JSON/text evidence.
8. From Finance inquiry screens, record the invoice, payment, journal, liquidity, till-close and deposit references for the same sample.

## Completion boundary

The SQL and wrapper are implemented and source-reviewable. MPOS-0803 remains **In progress** until the scenario is executed against an authorized relational UAT database, the script returns zero findings with nonzero completed sales, Finance reviews the source-to-ledger sample, and evidence is retained with the signed mobile candidate release.
