# TDC WHT Threshold, Certificate, and Remittance Controls

**Implemented:** 2026-08-03  
**Scope:** Finance-owned AP and AR transaction entry, WHT certificate evidence, remittance evidence, and statutory register output  
**Related limitations:** `FIN-LIM-0004`, `FIN-LIM-0019`, `FIN-LIM-0047`, `FIN-LIM-0054`

## Outcome

RHEMA ERP now treats withholding tax as part of the canonical Finance transaction chain rather
than as a disconnected certificate utility. AP payment entry obtains its rate, annual supplier
threshold, and payable account from the active tenant tax configuration. Posted payment facts then
drive immutable certificate versions, remittance evidence batches, and the downloadable WHT
register. No certificate or remittance action creates or changes a GL posting.

AR receipt entry now exposes both ordinary WHT suffered and VAT withholding suffered. It requires
the authority/counterparty certificate reference and resolves the receivable accounts from active,
effective-dated sales-side tax configurations on the server. Client-supplied account identifiers are
not trusted for statutory posting direction.

## TDC/Ghana policy defaults

The AP invoice screen also replaces its former fixed percentage menu with active tenant purchase-WHT
configurations. That classification is an invoice estimate; the actual threshold and liability are
still calculated at payment, where the statutory event and supplier aggregate are known.

The implementation baseline was checked against the Ghana Revenue Authority guidance available on
2026-08-03:

- GRA's withholding-tax page identifies resident entity rates of 3% for goods, 5% for works, and
  7.5% for services, with a GH¢2,000 annual goods/works/services threshold:
  <https://gra.gov.gh/domestic-tax/tax-types/withholding-tax/>.
- GRA's VAT-withholding page identifies 7% VAT withholding and the associated withholding
  certificate/return evidence:
  <https://gra.gov.gh/domestic-tax/tax-types/vat-withholding/>.

The idempotent Finance seed therefore includes purchase configurations `WHT-GOODS` (3%),
`WHT-WORKS` (5%), and `WHT-SERV` (7.5%) with a GH¢2,000 threshold, plus sales-side
`WHT-REC-SERV` (7.5%) and `VAT-WHT-REC` (7%) receipt configurations. Missing statutory mappings
default to the existing WHT payable control and the new WHT receivable control account. Existing
tenant account overrides are preserved.

These are baseline configuration values, not hard-coded posting rules. Finance administrators must
confirm the effective dates, supplier/payment classification, account mappings, and any future GRA
rate change before production activation.

## AP calculation control

The server owns the final WHT decision:

1. The selected tax must be active, effective on the payment date, purchase-applicable, and in the
   WHT category for the current tenant.
2. The taxable base is the gross invoice settlement represented by cash, discount, and WHT across
   the payment allocations. Supplier advances cannot carry WHT without an invoice allocation.
3. The annual aggregate uses payment date, supplier, tax configuration, and calendar year. Voided,
   failed, and reversed payments are excluded.
4. Below-threshold payments persist their tax and base snapshot with zero WHT, so later payments can
   cross the annual aggregate without reconstructing history.
5. When the aggregate reaches the configured threshold, the configured rate applies to the full
   current payment base. The payment stores the base, prior aggregate, threshold, applied flag, and
   explanatory calculation note for audit.
6. Submission is rejected if the browser-calculated WHT differs from the server result or if the
   configured WHT payable account is absent.

The AP screen can preview and auto-allocate this result, but the backend repeats the calculation at
creation time. That prevents a stale browser session or hand-edited request from bypassing the
threshold.

## Controlled certificate lifecycle

Only posted, non-reversed AP payments with a positive WHT amount are eligible. Initial issue creates
version 1 from an immutable payment/supplier/tax/account/journal snapshot. Reissue requires a reason,
supersedes the current issued version, creates the next numbered version, and retains both sides of
the lineage. Cancellation requires a reason and retains a visibly watermarked historical print.

Database constraints enforce unique certificate numbers, unique payment/version pairs, and no more
than one issued version per payment. Reissue and cancellation are blocked while the payment belongs
to a non-cancelled remittance because changing the certificate beneath that evidence would make the
register disagree with the submitted batch.

Certificate issue, reissue, cancellation, print, and register export are separately audited. Tax
administrators own lifecycle mutations; Finance report users can read/print; statutory register
export requires the Finance export permission.

## Remittance evidence lifecycle

The remittance workspace lists posted, non-reversed WHT liabilities not already assigned to an
active batch. Finance selects one currency and period, then creates a draft containing immutable
payment, supplier, TIN, tax, taxable-base, amount, certificate, and journal references.

The controlled states are:

- `Draft`: Finance can review the selected liabilities or cancel the batch.
- `Submitted`: requires the authority submission reference; Finance can subsequently record payment
  or cancel/correct the evidence batch.
- `Paid`: requires payment date and bank/payment reference and optionally retains the GRA receipt
  reference. A paid batch cannot be cancelled through the ordinary path.
- `Cancelled`: retains the historical batch and releases its source liabilities to a replacement
  remittance.

The default due date is the fifteenth day of the month following the selected liability period and
can be overridden to match an approved statutory calendar. This record is evidence and
reconciliation metadata only: the actual authority payment must continue through the existing
Finance cash/bank/AP posting workflow.

The CSV statutory register joins each source payment to its journal, current/latest certificate
version, active remittance, submission reference, and remittance payment reference.

## Finance-only boundary and remaining work

This slice deliberately does not submit to a GRA portal, consume authority APIs, create a portal-
specific upload schema, or send certificates through email/document-distribution integrations.
Those are external-interface capabilities and remain the deferred portion of `FIN-LIM-0047`.

Tenant UAT should confirm the classification used for TDC suppliers and receipts, the real control
accounts, certificate numbering convention, signatory/presentation wording, and one complete
month-end liability-to-bank-payment reconciliation before production sign-off.

## Verification

- API project build: passed with zero errors.
- WHT release gate: 4/4 tests passed for server-owned invoice configuration, annual threshold
  crossing/reset, immutable certificate versions/cancellation, and remittance submission/payment
  with certificate-drift protection.
- Ghana statutory regression: 18/18 tests passed.
- Finance permission mapping: 75/75 cases passed.
- Targeted TypeScript and ESLint checks passed for AP/AR entry, certificate, remittance, report, tax
  service/type, and navigation changes.
- Migration `20260803144941_AddWhtComplianceLifecycle` was applied successfully to `RHEMAERP` on
  2026-08-03 and no longer appears as pending.
- The idempotent `seed-db` path completed successfully against `RHEMAERP`; Finance seeding added the
  missing control account and reconciled the tax configuration without replacing existing users or
  tenant account overrides.
