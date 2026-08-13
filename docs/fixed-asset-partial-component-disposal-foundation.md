# Fixed Asset Partial and Component Disposal Foundation

Date: 2026-08-10
Scope: Finance-only FIN-LIM-0042

## Outcome

The established fixed-asset disposal workflow now supports whole-asset, percentage-based partial,
and identified-component derecognition. A partial/component disposal removes only the approved
portion of carrying value; the retained asset stays active with a reduced, internally consistent
cost, depreciation reserve, residual value, production capacity and net book value.

This extends the existing maker-checker, central posting, final depreciation, revaluation-surplus,
reporting and audit paths. It does not introduce a parallel disposal engine.

## Accounting background and TDC policy

IAS 16 requires the carrying amount of a replaced part to be derecognised and permits an estimated
replacement cost to indicate the original component cost when direct history is impracticable. It
also requires significant parts to be depreciated separately. TDC's Finance-only baseline therefore
uses an evidenced allocation percentage for a portion or identified component, while retaining the
component reference and valuation/engineering/source-register evidence approved by the checker.

For each request, Finance allocates the current remaining layers using the approved percentage:

- acquisition cost and posted revaluation adjustment;
- accumulated depreciation after depreciation through the disposal date;
- accumulated impairment;
- asset-specific revaluation surplus;
- residual value; and
- units-of-production capacity and accumulated usage.

Independently rounded posting layers determine the disposed NBV so the journal cannot develop a
one-pesewa imbalance. Any rounding remainder stays with the retained asset.

## Workflow and controls

1. The maker selects Whole asset, Partial portion, or Identified component.
2. Partial/component scope requires a percentage greater than zero and less than 100.
3. An allocation evidence reference is mandatory; component scope also requires a component
   reference. Optional descriptions and notes explain the physical portion and allocation method.
4. Finance calculates the ordinary whole-asset benchmark through the disposal date and recognises
   only the disposed portion's share. The retained portion remains eligible for its normal period run.
5. Cost, contra balances, NBV, reserve transfer, proceeds, gain/loss, and remaining balances are
   frozen on the request for independent approval.
6. Completion recalculates every material allocation layer. Any book, valuation, depreciation, or
   policy drift cancels the stale approval.
7. One idempotent central posting event records final depreciation and partial derecognition.
8. The parent asset remains active after partial/component completion. A later request is allowed;
   only unfinished requests lock the asset.

## Posting pattern

- Debit depreciation expense / credit accumulated depreciation for the disposed portion's share of
  depreciation through the disposal date, when applicable.
- Debit the allocated accumulated depreciation and impairment balances.
- Debit proceeds clearing for net proceeds, when applicable.
- Credit the allocated fixed-asset carrying amount.
- Record the difference as disposal gain or loss.
- Transfer only the allocated asset-specific revaluation surplus directly within equity.

The resulting AssetTransaction records the remaining NBV instead of zero. Fixed-asset reports show
scope, percentage, component reference, allocation evidence and remaining NBV.

## Data and migration

Migration `20260810160000_AddFixedAssetPartialComponentDisposal` adds immutable scope, percentage,
component/evidence, allocated-layer, and remaining-balance fields. Existing disposals default to
Whole asset and 100 percent. A database check constraint prevents invalid scope/percentage pairs.

## Demonstration highlights

- Show a 25% disposal retaining 75% of the asset as active and depreciable.
- Open the one journal and trace the allocated cost, depreciation, impairment, proceeds and gain/loss.
- Show the component/evidence reference approved by the checker.
- Compare the before/after register NBV and show that rounding stays balanced.
- Submit a later disposal request to demonstrate that completed partial history does not lock the
  remaining asset.

## Boundaries

- This resolves FIN-LIM-0042 for controlled proportional partial/component derecognition.
- Significant-component master records with independent useful lives and depreciation schedules
  remain a broader asset-componentisation enhancement, not a prerequisite for evidenced disposal.
- Foreign-currency proceeds are now resolved by the shared disposal FX snapshot under FIN-LIM-0043; proportional disposals use the same translated functional-proceeds basis.
- Tax, AR and Cash/Bank settlement integration was subsequently resolved under FIN-LIM-0040; see `docs/fixed-asset-sale-settlement-foundation.md`.
- Migration application and representative TDC UAT remain release gates.
