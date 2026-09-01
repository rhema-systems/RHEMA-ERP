# Finance Coding Dimensions Architecture

## Requirement

**FR-GL-002 Financial Coding Dimensions** — The ERP shall maintain configurable financial
dimension definitions and values, including department, cost centre, project, programme, fund,
property, revenue stream and bank account. Applicable dimensions shall be captured, defaulted or
derived at transaction-line level independently of the natural GL account. Account- and source-
specific rules shall support required, optional, fixed and prohibited values. Finance shall preserve
the posted dimension set for audit and reversal and support filtering, aggregation and drill-down by
dimension and dimension combination.

"Mandatory transaction dimensions" means mandatory where an applicability rule requires them. It
does not mean that every dimension is valid or required on every posting line.

## Deliberate hybrid model

Rhema retains two separate concepts:

1. **Structural account segments** identify the GL account. Natural account and a genuine legal-
   entity or balancing segment may remain in `AccountSegmentStructure` and `AccountSegmentValue`.
2. **Transaction dimensions** classify an individual journal line independently of `AccountId`.
   Department, cost centre, project, programme, fund, property, revenue stream and derived bank
   account normally belong here.

Each business attribute has one authoritative reporting location. A tenant must not independently
encode Department as a variable account segment and post a conflicting Department transaction
dimension. An account rule may default or fix the posted Department value, but the immutable posted
dimension set remains authoritative for transaction-dimension reporting.

## Phase 1 data model

- `FinanceDimensionDefinition` — tenant-owned definition and Analytical, Balancing or Derived
  classification.
- `FinanceDimensionValue` — effective-dated lookup or operational-entity-backed value.
- `FinanceDimensionSet` — deduplicated immutable combination identified by a canonical hash.
- `FinanceDimensionSetItem` — exact value plus readable code/name snapshots.
- `FinanceDimensionAccountRule` — Required, Optional, Prohibited or Fixed account applicability.
- `AccountTransaction.FinanceDimensionSetId` — nullable during adapter certification.

`SegmentString` remains a legacy account-segment display snapshot. It is not a substitute for a
validated dimension set.

## Implementation status (25 August 2026)

The data model, optional posting-engine contract, immutable set persistence, exact reversal reuse,
tenant administration API/UI, manual-journal capture/read display, and transaction-dimension report
filters are implemented. Manual journals are the first certified producer and may use source-scoped
Required, Optional, Fixed and Prohibited rules (`GL` / `ManualJournalEntry` / `Post`).

Trial Balance, Income Statement, Balance Sheet and Detailed Ledger now resolve tenant-owned
dimension values and filter immutable posted ledger-line sets. The same criteria flow through the
screen, drill-down, CSV export, PDF/print output and published financial-statement layouts. Historical
inactive values remain selectable for historical reporting; cross-tenant or unknown values fail
closed. Detailed Ledger exposes the frozen dimension display and assignments carried by each line.

Operational adapters remain deliberately uncertified. AP, AR, Cash, Fixed Assets, Payroll,
Procurement and Inventory continue posting without mandatory transaction dimensions until their
owner-specific source evidence and consumer tests land. This is a compatibility gate, not permission
to encode those facts as free-text or arbitrary stored dimension-set IDs.

Filtered report totals include only ledger lines that carry the selected immutable coding. Because
operational adapters remain uncertified, a filtered view is not yet a complete module-wide view of
all AP, AR, Cash, Fixed Asset, Payroll, Procurement or Inventory activity. The report UI labels that
coverage boundary; it must not imply that untagged legacy or operational lines belong to the selected
dimension.

Dimension breakdown/pivot remains deferred. Dimension-grained budget cells and actual matching are
implemented in the Finance budget provider; worksheet presentation and producer certification remain
separate phases. Legacy scenarios retain their account/period grain and must not be reinterpreted.

Producers submit structured dimension facts on `FinancePostingLineDto.Dimensions`. They do not
select a stored Finance dimension-set ID. Finance resolves the tenant-owned value, effective date,
canonical combination and immutable set. A stored set ID may be reused only by Finance when posting
an existing journal or reversing the exact historical journal line.

## Ownership and integration

The existing adapter architecture remains authoritative:

1. The operational module owns its source document, workflow and business evidence.
2. The producer provides dimension facts already known by its header, line or master records.
3. Finance applies defaults, fixed values, account applicability and combination rules.
4. Finance resolves or creates the immutable dimension set and posts through
   `IFinancePostingEngine`.
5. Reversal copies the exact historical set; it never recalculates current defaults.

Examples:

- Procurement supplies Department, Project and Fund lineage from the requisition/PO/receipt.
- Inventory supplies Department/Project from the approved issue or reservation; it continues to own
  item, warehouse, location, quantity and valuation evidence.
- Cash supplies the selected bank master; Finance derives the Bank Account dimension.
- Fixed Assets supplies the asset/property/project lineage.
- Payroll supplies the approved employee assignment used to derive Department and Cost Centre.

Phase 1 keeps `Dimensions` optional. Empty input preserves existing posting behaviour. Required-rule
enforcement must be activated only after the relevant adapter has consumer-contract evidence.

## Reporting semantics

Unfiltered Trial Balance, Income Statement and Balance Sheet continue to group posted transactions by
`AccountId` and must retain the same totals.

When transaction dimensions are requested, the calculation changes from filtering account masters to
filtering posted ledger lines:

```text
posted AccountTransactions
  -> tenant, book, status and date scope
  -> DimensionSetItem filters
  -> group by AccountId
  -> optional dimension breakdown/pivot
  -> existing account classification and statement layout
```

Different dimensions combine with AND semantics. Multiple selected values in one dimension use OR
semantics. Filtering and breakdown are distinct report operations.

Cash Flow is deliberately outside the first filter slice. Its operating/investing/financing
classification is derived from cash and counterpart lines, so naively filtering individual ledger
lines could misclassify or double count an activity. Dimension-aware Cash Flow requires an explicit
counterpart-attribution contract and dedicated reconciliation tests before it is exposed.

An Analytical dimension does not guarantee that a filtered Trial Balance or Balance Sheet balances.
A Balancing dimension requires all journal lines to balance by value or requires Finance-generated
inter-dimension balancing entries. That policy must be configured explicitly; reports must not imply
that an analytical slice is a formal balanced statement.

## Budget-control impact

The Finance budget commitment foundation remains reusable, but the budget cell must become
dimension-aware before Procurement consumes it.

An adopted budget scenario shall declare its **budget-control dimensions**. Each budget entry must
provide exactly one value for each controlling dimension. A posted transaction may contain additional
analytical dimensions; matching succeeds when the budget entry's controlling assignments are a subset
of the posted dimension set.

Example:

```text
Budget cell: 6100 Repairs + Department=EST + Fund=CAPEX
Posting:     6100 Repairs + Department=EST + Fund=CAPEX + Project=PRJ-007
```

The posting consumes the cell. Scenario rules must prohibit ambiguous overlapping cells that could
match the same account, period and controlling coordinates. Budget actuals must be calculated from
posted `AccountTransaction` lines whose dimension sets contain the controlling assignments, not from
all activity on the account.

The dimension-aware budget follow-up therefore extends:

- `BudgetEntry` with the controlling assignment set;
- eligible-cell and commitment DTOs with structured dimension evidence;
- reservation snapshots and idempotency hashes with the canonical assignments;
- posted-actual queries with dimension-set matching; and
- FIN-INT-015 before a Procurement adapter is certified.

The reservation lifecycle, FX evidence, concurrency, idempotency and posting-event validation remain
unchanged.

## Rollout

1. Add the Finance dimension masters, sets, rules and nullable ledger linkage.
2. Accept optional structured dimensions through `IFinancePostingEngine`.
3. Preserve exact sets on existing-journal posting and reversal.
4. Add Finance administration and journal-line UX, defaults and warning diagnostics. **Implemented
   for manual journals.**
5. Move financial-report filters to transaction-line semantics. **Implemented for Trial Balance,
   Income Statement, Balance Sheet and Detailed Ledger; Cash Flow and breakdown/pivot remain
   deferred.**
6. Extend budget cells and actual matching to the configured control dimensions. **Implemented
   in the Finance budget provider; producer certification remains adapter-specific.**
7. Certify Finance and operational adapters individually.
8. Enable mandatory enforcement by account/source only after certification.

### Finance-owned settlement expansion

The next certified settlement slice is deliberately limited to Finance-owned vendor payments,
customer payments/receipts, direct Cash payments, direct Cash receipts and cash/bank transfers.
It captures stable source-line evidence for allocations, advances, direct offsets and both transfer
legs. Invoice settlements inherit the exact originating invoice-line combinations; discounts,
withholding, fees, write-offs and realised FX must remain split over those combinations with the
final deterministic allocation carrying any rounding residual.

Bank deposits, returned cheques, reconciliation adjustments, fixed-asset settlement and payment
paths produced by another module remain later dimension-expansion candidates. They require their
own route identity, additive Finance adapter/contract, consumer tests and governed certification;
sharing an underlying Finance service does not certify them implicitly.

## Acceptance criteria

- Two postings to the same GL account can carry different dimension sets.
- Cross-tenant, inactive, expired and malformed values are rejected.
- Repeated combinations reuse one canonical dimension set.
- Ordinary producers cannot select arbitrary stored dimension-set IDs.
- Reversal preserves the exact historical set after master-data changes.
- Existing postings with no dimensions remain valid during certification.
- Unfiltered statement totals do not change.
- Transaction-dimension filters include only matching posted lines.
- Analytical report slices are labelled as potentially unbalanced.
- Budget actual and available amounts use the configured controlling dimension grain.
