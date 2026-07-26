# Financial Report Segment Filter UX Implementation Plan

## Objective

Improve the segment-filter experience shared by Trial Balance, Income Statement, and Balance Sheet without changing the accounting meaning of the reports.

The target experience is:

- every active reporting dimension remains metadata-driven;
- lookup-backed dimensions such as Department and Project use keyboard-accessible searchable comboboxes;
- the Natural Account dimension uses a validated searchable autocomplete sourced from actual tenant GL account segment values;
- selected segment filters affect the backend report, totals, print, and export consistently;
- Trial Balance **Find in Results** remains a separate local navigation aid and never implies that it recalculates the report.

## Current-state distinction

### Find in Results

`Find in Results` filters only the Trial Balance rows already returned to the browser. It matches account code, full account number, or account name. It does not call the report API, change totals, or affect print/export. Its purpose is quick navigation and detailed-ledger drill-down.

### Natural Account report filter

The Natural Account control is a report criterion. Its selected value is sent as a structured `FinanceSegmentFilterDto`, applied before balances and totals are calculated, and included in print/export requests. It must therefore select a valid segment value rather than accept unchecked free text.

## Recommended design

### 1. Shared searchable dimension control

Replace the current lookup `Select` inside `ReportSegmentFilters` with a reusable `ReportingDimensionCombobox` built from the existing `Popover` and `Command` components.

Required behavior:

- button text shows `All {Dimension}` or the selected code and description;
- opening the control focuses a search input;
- filter by both segment value and description;
- support keyboard navigation, Enter selection, Escape close, and clear-to-All;
- preserve one selected value per dimension, matching current backend AND semantics across dimensions;
- show an explicit empty state and disabled/loading state;
- keep the original segment order from `SegmentPosition`;
- remain usable on narrow screens and with long descriptions.

Lookup-backed dimensions continue using the active `lookupValues` returned by `/api/finance/segments/reporting-dimensions`. Search can initially be client-side because these values are already loaded with the definitions.

### 2. Valid Natural Account option source

Do not turn Natural Account into a normal lookup-table segment merely to obtain a dropdown. Preserve `LookupTableRequired = false` and derive valid choices from actual account master data.

Add a tenant-scoped endpoint:

`GET /api/finance/segments/{segmentStructureId}/reporting-options?search={text}&take={n}`

For the Natural Account dimension, it should:

- require an active, tenant-owned reporting dimension;
- query active, non-deleted Accounts joined to their `AccountSegmentValue` for that structure;
- return distinct segment values across Department/Project account combinations;
- search by natural-account value, account code, and account name;
- return a stable description and the number of account combinations represented;
- order by segment value;
- cap results and support cancellation;
- never expose values from another tenant.

Suggested response:

```json
{
  "items": [
    {
      "segmentValue": "6200",
      "description": "Utilities Expense",
      "accountCombinationCount": 4
    }
  ],
  "hasMore": false
}
```

The endpoint can also support non-lookup reporting dimensions generally, avoiding future hard-coded Natural Account behavior.

### 3. Natural Account autocomplete

Render non-lookup Natural Account as an async searchable combobox rather than an editable text box.

Required behavior:

- debounce server search by approximately 250 ms;
- require at least one search character before remote search when the option set is large;
- show value and account name together;
- only commit a value selected from returned options;
- provide an `All Natural Account` clear option;
- cache recent queries per tenant/session;
- cancel stale requests when search text changes;
- display a recoverable loading/error state without preventing the unfiltered report from running.

The selected value continues to generate the existing structured filter containing segment structure ID, code, position, and value.

### 4. Backend validation hardening

Extend `ResolveSegmentFiltersAsync` for non-lookup segments. After validating the tenant-owned reporting dimension, validate:

- exact configured segment length;
- Numeric/Alphanumeric data type;
- existence of the value on an active, non-deleted tenant account through `AccountSegmentValue`;
- effective/active rules used by the account master where applicable.

Reject invalid values with a clear 400 response. This protects API, print, and export callers even if they bypass the UI.

### 5. Integration across all three reports

Update the shared component once and consume it unchanged in:

- Trial Balance;
- Income Statement;
- Balance Sheet.

For each report verify that:

- Run Report sends identical structured segment keys;
- displayed rows/sections and totals reflect the filters;
- Print and Export send the same criteria;
- generated document subtitles list applied dimensions;
- clearing a selection restores the unfiltered dimension;
- changing a control does not silently claim the previous result has been regenerated.

Consider storing an `appliedSegmentFilters` snapshot after a successful run and displaying it above the result. This prevents confusion when a user changes a control but has not yet clicked Run Report.

## Implementation slices

### Slice 1 — API and validation

1. Add reporting-option DTOs and the tenant-scoped endpoint.
2. Implement distinct Natural Account option queries with search and result caps.
3. Harden non-lookup filter validation in `GeneralLedgerService`.
4. Add tenant, invalid-value, inactive-account, length, and data-type tests.

### Slice 2 — Shared controls

1. Add `ReportingDimensionCombobox` using the existing Command/Popover UI foundation.
2. Add an async option hook/service method for non-lookup dimensions.
3. Replace lookup dropdowns and the Natural Account text input in `ReportSegmentFilters`.
4. Add loading, empty, error, clear, and keyboard-accessibility states.

### Slice 3 — Report integration

1. Exercise the component on all three pages.
2. Add applied-filter summaries.
3. Verify view/print/export parameter parity.
4. Rename or help-text any remaining controls whose scope could be misunderstood.

### Slice 4 — Verification and rollout

1. Component tests for search, selection, clear, keyboard navigation, and async race cancellation.
2. Service tests for indexed structured-filter serialization.
3. Backend integration tests proving Natural Account filters change each report and totals correctly.
4. Print/export tests proving the same filters reach document builders.
5. Browser tests using at least Department + Natural Account + Project together.

## Acceptance criteria

- Department and Project dropdowns are searchable by value and description.
- Natural Account is a searchable selector populated from valid tenant GL account values, not an arbitrary text input.
- Invalid or cross-tenant non-lookup values are rejected server-side.
- Selecting Natural Account changes all three backend-generated reports and their totals.
- View, print, and export use identical filter criteria.
- `Find in Results` only narrows visible Trial Balance rows and is visibly distinguished from report criteria.
- All controls are keyboard accessible and responsive.
- Segment names and control order remain derived from active reporting-dimension definitions; no Department, Natural Account, or Project name is hard-coded into the report pages.

## Out of scope

- Multi-select values within the same dimension. The current backend applies filters with AND semantics, so multi-select requires an explicit OR-within-dimension contract and reconciliation tests.
- Changing the tenant's COA shape or converting Natural Account into a lookup-table segment.
- Adding segment filtering to Cash Flow in this slice; that should be assessed separately against cash-flow classification and reconciliation requirements.
