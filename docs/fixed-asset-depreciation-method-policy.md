# TDC Fixed Asset Depreciation Method Policy Decision

Date: 2026-08-09

Limitation: `FIN-LIM-0031`

## Research Basis

IAS 16 requires the depreciation method to reflect how the asset's future economic benefits are expected to be consumed. It identifies straight-line, diminishing-balance, and units-of-production as acceptable examples; requires consistent application unless the consumption pattern changes; and requires at least annual review. A method change is treated prospectively as a change in accounting estimate under IAS 8.

Primary sources:

- [IFRS Foundation, IAS 16 Property, Plant and Equipment, paragraphs 60-62A](https://www.ifrs.org/content/dam/ifrs/publications/pdf-standards/english/2021/issued/part-a/ias-16-property-plant-and-equipment.pdf)
- [IFRS Foundation, clarification of acceptable depreciation and amortisation methods](https://www.ifrs.org/projects/completed-projects/2014/clarification-of-acceptable-methods-of-depreciation-and-amortisation/)
- [IFRS Foundation, IAS 16 standard page](https://www.ifrs.org/issued-standards/list-of-standards/ias-16-property-plant-and-equipment/)

## Recommended TDC Method Catalogue

The next `FIN-LIM-0031` implementation should expose this controlled catalogue:

1. **Straight-line** — constant periodic charge where consumption is broadly even. Already implemented.
2. **Diminishing balance** — decreasing charge based on opening carrying amount and an approved annual rate. This includes double-declining as a configured accelerated-rate variant rather than a separate accounting family.
3. **Units of production** — charge based on verified actual usage/output against an approved lifetime capacity estimate.

`SumOfYearsDigits` should remain unavailable by default. It can be considered only if TDC documents an asset class whose benefit-consumption pattern is better represented by that method and approves the accounting policy. `None` is not a depreciation method; it is appropriate only where an asset is not depreciable or depreciation has not yet commenced under an explicit lifecycle rule.

Revenue-based depreciation must not be offered. The IFRS Foundation explicitly states that revenue normally reflects factors other than consumption of the asset's economic benefits.

## Required Product Controls For The Next Slice

- Effective-dated method policy by asset category/accounting book.
- Diminishing-balance annual rate with sensible bounds and snapshotting on each posted schedule.
- Units-of-production lifetime capacity, period usage, evidence, and cumulative-usage ceiling.
- Prospective method/estimate changes only; no rewriting of posted schedules.
- Independent approval for method or estimate changes after capitalization.
- Method-specific calculation diagnostics, rounding tests, residual-value floors, disclosures, and reversal/rerun coverage.
- Clear UI explanations so operators choose the economic-consumption pattern, not the method that produces a preferred expense result.

This policy converts `FIN-LIM-0031` from an open-ended list of algorithms into a standards-aligned TDC implementation scope.
