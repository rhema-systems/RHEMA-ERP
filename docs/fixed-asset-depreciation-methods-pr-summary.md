# Fixed Asset Standards-Aligned Depreciation Methods - PR Summary

Date: 2026-08-09

Requirement focus: `FR-FA-004`, `FR-FA-005`, `FR-FA-010`

Limitation resolved: `FIN-LIM-0031`

## Outcome

Finance can now calculate and post straight-line, diminishing-balance, double-declining-balance and units-of-production depreciation through the existing fixed-asset run, workflow approval, central posting, reporting and correction paths.

## Accounting Policy Implemented

- Diminishing balance applies a category/asset-approved annual percentage to opening net book value.
- Double-declining balance uses an approved accelerated percentage or derives `200% / useful life in years` when the override is zero.
- Units of production applies verified period usage to the remaining depreciable amount over remaining approved lifetime capacity.
- Every method observes the residual-value floor and deterministic money rounding.
- Sum-of-years-digits, `None` and revenue-based depreciation remain unavailable under the recorded TDC policy decision.

## Controls And Evidence

- Category defaults flow into new assets and their accounting-book records.
- Annual rates use four-decimal policy precision; production quantities use four-decimal operational precision.
- Units-of-production runs require positive asset-specific usage and a meter/production evidence reference.
- Cumulative usage cannot exceed approved lifetime capacity.
- Schedule lines freeze method, effective rate, capacity, usage, cumulative before/after values and evidence.
- Approval-time delays are safe because posting revalidates the asset book and method assumptions before creating the journal.
- Depreciation reversal restores both net book value and production capacity consumption while retaining the original schedule and journal.
- Post-capitalization method and estimate fields remain locked against direct edits pending a separately approved prospective-change workflow.

## User Experience

- Category and asset workspaces expose only the supported method catalogue.
- Method-specific inputs appear only when relevant.
- The depreciation workspace captures units-of-production evidence and displays method/usage in results.
- Bulk processing does not guess production evidence; eligible production-based assets are run individually.

## Schema

Migration `20260809170000_AddStandardsAlignedDepreciationMethods` adds method assumptions to categories, assets and accounting books, plus immutable calculation/evidence fields to depreciation schedules.

The migration has not been applied to a database by this PR preparation step.

## Verification

- `FinanceGoLive-FixedAssetDepreciation`: 27/27 passing.
- Changed Finance frontend files: ESLint passing.
- Focused EF tooling build: passing with zero warnings/errors.
- Handbook generator/verifier: 32 pages and all local links/assets passing.
- Repository-wide frontend type-check remains blocked only by pre-existing Inventory and Reports errors; no changed Finance file appears in the error list.

## Reviewer Notes

The repository has pre-existing EF model drift for Estate decimal metadata. A disposable full-metadata delta confirmed that, after aligning annual-rate fields with the repository-wide `decimal(18,4)` convention, the remaining generated alterations are outside this Finance slice. No diagnostic migration or database change is included.
