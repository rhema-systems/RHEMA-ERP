# TDC Fixed Asset Depreciation Method Policy Decision

Date: 2026-08-09

Limitation: `FIN-LIM-0031`

## Research Basis

IAS 16 requires the depreciation method to reflect how the asset's future economic benefits are expected to be consumed. It identifies straight-line, diminishing-balance, and units-of-production as acceptable examples; requires consistent application unless the consumption pattern changes; and requires at least annual review. A method change is treated prospectively as a change in accounting estimate under IAS 8.

Primary sources:

- [IFRS Foundation, IAS 16 Property, Plant and Equipment, paragraphs 60-62A](https://www.ifrs.org/content/dam/ifrs/publications/pdf-standards/english/2021/issued/part-a/ias-16-property-plant-and-equipment.pdf)
- [IFRS Foundation, clarification of acceptable depreciation and amortisation methods](https://www.ifrs.org/projects/completed-projects/2014/clarification-of-acceptable-methods-of-depreciation-and-amortisation/)
- [IFRS Foundation, IAS 16 standard page](https://www.ifrs.org/issued-standards/list-of-standards/ias-16-property-plant-and-equipment/)

## Implemented TDC Method Catalogue

The `FIN-LIM-0031` implementation exposes this controlled catalogue:

1. **Straight-line** - constant periodic charge where consumption is broadly even.
2. **Diminishing balance** - decreasing charge based on opening carrying amount and an approved annual rate.
3. **Double-declining balance** - the accelerated diminishing-balance variant. TDC may supply an approved annual rate; otherwise the system derives the conventional `200% / useful life in years` rate, capped at 100%.
4. **Units of production** - charge based on verified actual usage/output against an approved lifetime capacity estimate.

`SumOfYearsDigits` remains unavailable by default. It can be considered only if TDC documents an asset class whose benefit-consumption pattern is better represented by that method and approves the accounting policy. `None` is not a depreciation method; it is appropriate only where an asset is not depreciable or depreciation has not yet commenced under an explicit lifecycle rule.

Revenue-based depreciation is not offered. The IFRS Foundation states that revenue normally reflects factors other than consumption of the asset's economic benefits.

## Implemented Product Controls

- Asset categories carry the default method, useful life and method-specific rate/capacity. A new asset inherits those assumptions, while its accounting-book snapshots preserve the approved values used for calculation.
- Diminishing-balance rates are validated, bounded above zero and at or below 100%, and frozen on each schedule line.
- Units-of-production assets require an approved lifetime capacity. Every run requires positive period usage, an evidence reference and optional explanatory notes; cumulative usage cannot exceed capacity.
- Posted schedules freeze the method, rate/capacity, usage and before/after cumulative usage. Reversal restores both accounting values and production counters without erasing the original evidence.
- Depreciation always observes the residual-value floor, deterministic rounding, open-period controls, configurable workflow approval and central posting-engine rules.
- Method and estimate fields cannot be destructively edited after capitalization. A future prospective-estimate-change workflow may relax this conservative restriction only when TDC approves the change-governance design.
- The operator UI explains and requests only the evidence relevant to the selected method. Bulk processing deliberately redirects units-of-production assets to an asset-specific run because usage evidence cannot safely be guessed.

## Calculation Basis

- Straight-line: remaining depreciable amount divided across the remaining useful-life months.
- Diminishing balance: opening net book value multiplied by the approved annual rate and divided by 12.
- Double-declining balance: the diminishing-balance calculation using the approved or derived accelerated annual rate.
- Units of production: remaining depreciable amount multiplied by period units and divided by remaining lifetime capacity.

Every calculation is capped so net book value cannot fall below residual value.

## Verification

The focused `FinanceGoLive-FixedAssetDepreciation` regression set contains 27 passing tests. The added cases verify diminishing-balance rate snapshots, the derived double-declining rate, units-of-production evidence and charge calculation, capacity/usage validation, posting, approval and reversal compatibility.

This implementation resolves the product behavior recorded by `FIN-LIM-0031`. Migration application and representative TDC UAT remain release gates rather than missing algorithms.
