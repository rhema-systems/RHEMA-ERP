# Foreign-currency AP/AR opening invoices

## Scope

This change adds controlled foreign-currency evidence to AP and AR opening invoices only. It does not redesign the existing ordinary-invoice exchange-rate override workflow.

An opening invoice in the tenant functional currency continues to use rate `1` and has no exchange-rate record. A foreign-currency opening invoice must reference an active, approved or auto-approved Daily exchange-rate record that:

- belongs to the same tenant;
- is effective on the invoice date;
- represents functional currency to invoice currency;
- uses the tenant's AP or AR invoice quote side when directional policy is enabled, otherwise Mid; and
- exactly matches the numeric rate stored on the invoice.

The rate record is revalidated when AP is submitted, approved, and posted, and when AR is finally sent/posted. A missing, changed, inactive, unapproved, cross-tenant, wrong-side, wrong-pair, or out-of-window rate fails closed before ledger posting.

## Accounting contract

For a USD 100 opening invoice at GHS 15 per USD:

| Source | Debit | Credit | Transaction evidence |
| --- | ---: | ---: | --- |
| AP opening | Migration clearing GHS 1,500 | AP control GHS 1,500 | AP control retains USD 100 and the approved rate ID; clearing is functional-only |
| AR opening | AR control GHS 1,500 | Migration clearing GHS 1,500 | AR control retains USD 100 and the approved rate ID; clearing is functional-only |

This avoids putting foreign-currency quantity on Migration Clearing while preserving the native supplier/customer balance on the subledger control account.

## UI behavior

When `Opening balance` is selected, the invoice screen resolves the approved rate for the selected historical invoice date and configured quote side. The rate becomes read-only, and the request carries its durable ID. Changing the date or currency clears the previous ID before a replacement lookup, so Save cannot knowingly reuse stale evidence. If no compliant rate exists, creation is blocked and Finance must load/approve the missing rate in Multi-Currency.

Ordinary invoices retain their current editable-rate behavior and do not require the new nullable ID.

## Deployment and verification

Migration `20260822231500_AddOpeningInvoiceExchangeRateEvidence` adds nullable `ExchangeRateId` foreign keys and tenant lookup indexes to `VendorInvoice` and `Invoices`. It does not backfill or mutate existing invoices.

Focused regression coverage proves:

- AP and AR native/functional line separation and balanced GHS totals;
- rate ID, rate value, source, date, and transaction currency reach the posted journal;
- numeric rate drift is rejected before a posting event; and
- a foreign opening invoice cannot be created without an approved rate ID.

After deployment, apply the reviewed migration before creating foreign-currency opening invoices. Existing functional-currency openings and ordinary invoices remain compatible.
