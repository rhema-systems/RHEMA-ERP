# Financial Statement Layouts — Backend-First Implementation Plan

## Objective

Introduce versioned, coded, sequenced, and hierarchical financial statement layouts without building a visual row-layout designer in the first release.

Existing Balance Sheet and Income Statement generation remains available as a fallback until a published layout is explicitly selected or configured as the default. Trial Balance ordering remains based on the GL account number.

## First-release user experience

Authorized finance administrators manage layouts through validated API requests or controlled imports. Report users can select a published layout when running a Balance Sheet or Income Statement.

The first release is not a free-form report designer. It does not include drag-and-drop editing, arbitrary report columns, charts, scheduling, or a canvas.

## Domain model

### FinancialStatementLayout

- Tenant-owned layout identity and metadata.
- Unique business code and name.
- Statement type: Balance Sheet or Income Statement.
- Accounting book used as the source of posted balances.
- Active/default flags.
- Owns multiple versions.

### FinancialStatementLayoutVersion

- Immutable version number within a layout.
- Lifecycle: Draft, Published, Retired.
- Effective date range.
- Publication audit fields.
- Published versions cannot be edited. A new draft is cloned from a prior version.

### FinancialStatementRow

- Stable row code for formulas and references.
- Label and row type.
- Parent row and display sequence.
- Optional restricted formula.
- Sign, zero-suppression, account-detail, visibility, and basic emphasis settings.

Initial row types:

- Header
- Account
- Formula
- Total
- Spacer

### FinancialStatementRowMapping

Initial mapping types:

- Individual GL account.
- Inclusive GL account-number range.
- GL account hierarchy node, including its descendants.

Mappings are version-specific so an account can appear differently in separate layouts without changing the account master.

## Backend API

Implemented management routes under `/api/finance/financial-statement-layouts`:

- `GET /` — list tenant layouts.
- `GET /{layoutId}` — retrieve a layout and its versions.
- `POST /` — create a layout with draft version 1.
- `PUT /{layoutId}` — update layout metadata.
- `POST /{layoutId}/versions` — create a draft by cloning a selected version.
- `PUT /versions/{versionId}/rows` — replace a draft version definition atomically.
- `POST /versions/{versionId}/validate` — validate without publishing.
- `POST /versions/{versionId}/publish` — validate and publish.
- `POST /versions/{versionId}/preview` — execute a validated draft or published version without changing it.
- `POST /execute` — execute the selected or default version effective for the report date.

## Publication validation

Publication must fail when:

- Row codes are missing or duplicated.
- Parent rows are missing, cross-version, or cyclic.
- A formula references an unknown row or creates a formula cycle.
- A mapping references another tenant's account.
- Mapping fields do not match the selected mapping type.
- An individual account is mapped to more than one contributing row in the same version.
- An account range is reversed or malformed.
- A Balance Sheet layout directly maps revenue or expense accounts, or an Income Statement directly maps balance-sheet accounts.
- A layout has no contributing account rows.

Range and hierarchy overlap diagnostics will be included before a version becomes a selectable production default.

## Formula safety

Formula text is never evaluated as executable code.

The initial grammar will support:

- Direct row references, such as `1110 + 1120 - 1130`.
- Parentheses.
- `SUM(1110:1180)` over display-ordered rows.

Division, percentages, conditionals, cross-layout references, and user-defined functions are deferred.

## Report execution

The execution engine will:

1. Resolve the selected published version for the tenant, statement type, book, and report date.
2. Reuse the existing posted-GL balance queries.
3. Resolve account, range, and hierarchy mappings to tenant-owned accounts.
4. Calculate account rows once.
5. Calculate formulas in dependency order.
6. Apply display rules.
7. Return generic layout rows plus compatibility totals required by the existing Balance Sheet and Income Statement screens.

Existing generators remain the fallback when no published layout is selected.

### Integrated statement request behavior

Balance Sheet and Income Statement requests accept:

- `layoutId` to require a particular active layout and its version effective on
  the report end date.
- `useDefaultLayout=true` to use the active default layout when available.
- `useDefaultLayout=false` with no `layoutId` to force the legacy
  account-classification presentation.

When a default is requested but is missing or has no effective published
version, the existing presentation is returned. An explicit invalid layout is
reported as an error. The response retains legacy sections and totals for
compatibility and adds `layoutExecution` with sequenced rows, layout/version
provenance, account details, validation warnings, and reconciliation coverage.
The same selection is propagated to CSV export and PDF/print output.

## Migration and rollout

1. Add the new tables without changing journal or account transaction tables.
2. Create draft default layouts from existing IFRS/statutory/management line-item mappings.
3. Preserve the current alphabetical order during migration because the legacy fields have no explicit sequence.
4. Require finance review before publishing migrated layouts.
5. Add an optional `layoutId` to statement requests.
6. Add report-screen layout selection after backend publication and execution are stable.
7. Retire the legacy presentation path only after reconciliation and UAT.

## Delivery slices

Current implementation status: Slices 1 through 5 are complete. Balance Sheet
and Income Statement requests support selected/default layouts with legacy
fallback. Controlled import and legacy draft migration are available without a
visual designer. Authorised finance users can operate the workflow from the
Financial Statement Layouts workspace.

### Slice 1 — Management foundation

- Entities and database mapping.
- CRUD, cloning, validation, and publication APIs.
- Tenant isolation, permissions, auditing, and concurrency controls.
- Tests for version immutability and validation.

### Slice 2 — Execution foundation

- Account/range/hierarchy resolution.
- Restricted formula parser and dependency evaluator.
- Preview endpoint.
- Reconciliation diagnostics for mapped, unmapped, and duplicate accounts.

### Slice 3 — Report integration

- Optional layout selection on Balance Sheet and Income Statement requests. Complete.
- Existing report DTO compatibility. Complete.
- CSV/PDF/print parity. Complete.
- Default-layout selection and fallback behavior. Complete.
- Report-screen layout selectors and sequenced row rendering. Complete.

### Slice 4 — Operational setup

- Controlled JSON/Excel import template. Complete.
- Draft migration from existing line-item mappings. Complete.
- Finance permissions and import/publication audit. Complete.
- Accountant UAT and sign-off procedure. Complete.

### Slice 5 — Operations workspace

- Permission-aware layout register and version inspection. Complete.
- Excel/JSON import and legacy-migration preview/commit workflows. Complete.
- Validation findings and GL reconciliation preview. Complete.
- Draft cloning, publication, default/active controls, and confirmations. Complete.
- Layout-specific audit history. Complete.
- Frontend service contract tests and UAT documentation. Complete.

## Explicitly deferred

- Visual drag-and-drop row designer.
- User-defined columns and comparative-column formulas.
- Reporting trees/page axes.
- Charts and dashboards.
- Scheduling and distribution.
- Cross-layout formulas.
- Arbitrary scripting or expressions.
