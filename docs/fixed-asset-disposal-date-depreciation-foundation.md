# Fixed Asset Disposal-Date Depreciation Foundation

## Outcome

`FIN-LIM-0039` is resolved for whole-asset disposals. Finance now calculates depreciation through
the disposal date, places the calculation evidence inside the existing disposal maker-checker
decision, and posts the final charge and derecognition atomically through `IFinancePostingEngine`.

This implements the IAS 16 principle that depreciation continues until derecognition. IAS 16 does
not prescribe a daily convention, so TDC's explicit operational policy is:

- straight-line and diminishing-balance charges use actual inclusive days in the disposal fiscal
  period (`eligible days / fiscal-period days`);
- units-of-production charges use verified disposal-period units, with a required meter-reading or
  production-report reference;
- depreciation must already be posted through the preceding fiscal period;
- an existing current-period schedule must be reversed before a mid-period disposal, avoiding a
  full-period and partial-period double charge.

Authoritative standard reference: [IFRS Foundation — IAS 16 Property, Plant and Equipment](https://www.ifrs.org/issued-standards/list-of-standards/ias-16-property-plant-and-equipment/).

## One controlled posting

The disposal journal first records final depreciation:

- debit depreciation expense;
- credit accumulated depreciation.

The same journal then clears total accumulated depreciation (including that final charge),
derecognises the asset, records proceeds and gain/loss, and transfers any remaining asset-specific
revaluation surplus within equity. A failure rolls back the complete event; Finance cannot leave a
disposed asset without its final depreciation, or a final depreciation charge without disposal.

## Maker-checker and immutable evidence

At request time Finance stores the final amount, calculation dates, period and eligible days,
method, proration basis, diminishing rate or production assumptions, usage evidence, and a stable
schedule ID. Completion recalculates against the current book and cancels a stale approval if any
approved assumption has changed.

After successful posting, Finance creates a normal posted `AssetDepreciationSchedule` linked to the
`AssetDisposal`, journal, and posting event. Existing depreciation reports and GL reconciliation
therefore see the final charge without a parallel report path or a synthetic second approval run.

## Operator workflow

1. Post ordinary depreciation through the period immediately before disposal.
2. Open **Finance → Fixed Assets → Disposals** and select the asset and disposal date.
3. For units-of-production assets, enter verified units and the evidence reference.
4. Submit. Review the final depreciation amount and basis in Disposal History.
5. A different authorised user approves the disposal.
6. Complete the approved disposal; Finance posts one idempotent journal and exposes the linked
   depreciation schedule to reporting.

## Deliberate boundaries

- Partial/component disposal remains `FIN-LIM-0042`.
- Foreign-currency disposal proceeds remain `FIN-LIM-0043`.
- Sale invoice, tax, AR, and cash/bank settlement integration remains `FIN-LIM-0040`.
- Existing development records are not backfilled; only new disposal requests use this evidence.

## Verification coverage

Focused tests cover actual-day proration, journal lines, immutable schedule linkage, missing prior
depreciation, prevention of a duplicate current-period charge, units-of-production evidence,
revaluation-surplus interaction, idempotency, and the established disposal failure controls.
