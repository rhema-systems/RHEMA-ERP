# Financial Statement Layout Import and UAT

## Scope

This operating procedure covers controlled JSON/workbook imports and migration
of existing GL financial-statement line-item mappings. It does not provide a
visual row-layout designer.

All imported or migrated definitions remain **Draft**. Import does not publish
a version and does not make a newly created layout the production default.

## Operations workspace

Authorised finance users can perform this workflow from:

`/finance/reports/layouts`

The workspace provides:

- Layout filtering by statement type, accounting book, and active status.
- Controlled template download and JSON/workbook preview and commit.
- Legacy-mapping migration preview and commit.
- Version history with read-only row, formula, and mapping inspection.
- Validation findings and GL execution/reconciliation preview.
- Draft cloning, publication, default selection, and layout activation controls.
- Layout-specific audit events.

It intentionally does not permit direct row editing. Structural changes are
prepared in the controlled workbook or JSON definition and imported as Draft
content.

## Authorisation

All template, preview, commit, and legacy-migration endpoints require
`Finance.Reports.Layouts.Manage`. Publication separately requires
`Finance.Reports.Layouts.Publish`. The register, detail, and audit endpoints
require `Finance.Read`. The workspace hides management and publication actions
when the signed-in user lacks the corresponding permission.

## API workflow

Routes are under `/api/finance/financial-statement-layouts`.

- `GET /import-template` downloads the controlled `.xlsx` template.
- `POST /imports/workbook/preview` validates an uploaded workbook.
- `POST /imports/workbook/commit` commits the same workbook with the
  `expectedDefinitionHash` returned by preview.
- `POST /imports/json/preview` validates a JSON definition.
- `POST /imports/json/commit` commits a JSON definition and its preview hash.
- `POST /legacy-migration/preview` generates and validates a draft definition
  from existing account/book line-item mappings.
- `POST /legacy-migration/commit` commits the generated definition when its
  hash still matches.
- `GET /{layoutId}/audit-trail` returns the layout-specific business audit
  events shown in the workspace.

If the definition or the underlying legacy mapping changes after preview,
commit fails and a new preview is required.

## Workbook structure

### Metadata

One definition row containing template version, optional target layout/draft,
layout code and name, statement type, accounting book, effective dates, and
notes.

For a new layout, leave target identifiers blank. To replace an existing draft,
provide `TargetLayoutId`, `TargetVersionId`, and
`ExpectedTargetVersionRevision`. To create a new version in an existing layout,
provide `TargetLayoutId` and optionally `SourceVersionId`.

### Rows

One row per statement row. Supported types are `Header`, `Account`, `Formula`,
`Total`, and `Spacer`. Formula text uses the restricted layout formula grammar;
workbook cell formulas are prohibited.

### Mappings

Mappings reference their statement `RowCode`.

- `Account`: use `AccountNumber`.
- `AccountHierarchy`: use the hierarchy root `AccountNumber`.
- `AccountRange`: use `FromAccountNumber` and `ToAccountNumber`.

Account numbers are resolved only against accounts enabled for the selected
tenant accounting book.

### Lookups

Read-only tenant accounting-book and GL-account reference information used to
prepare the import.

## Legacy migration behaviour

Migration reads `AccountAccountingBook.FinancialStatementLineItem`, falling
back to the corresponding legacy IFRS, statutory, or management line-item
field. It:

1. Selects accounts enabled for the requested tenant accounting book.
2. Keeps only account types compatible with the requested statement.
3. Groups accounts by Assets/Liabilities/Equity or Revenue/Expenses.
4. Preserves alphabetical line-item order because legacy data has no explicit
   sequence.
5. Creates exact account mappings and generated section-total formulas.
6. Places accounts without a line-item label in an `Unclassified` row.
7. Produces a non-default draft requiring finance review.

## UAT checklist

Record evidence for each accounting book and statement type.

- Confirm the selected layout code, accounting book, and statement type.
- Confirm every required row code and label.
- Confirm parent-child hierarchy and visible row sequence.
- Confirm headings, totals, formulas, sign presentation, bold/underline rules,
  zero suppression, and account-detail settings.
- Confirm exact, range, and hierarchy mappings resolve as intended.
- Resolve every duplicate-account validation error.
- Review all unmapped accounts; require zero unmapped non-zero accounts unless
  an exception is documented and approved.
- Run the prior month, current month, quarter end, and year end.
- Compare layout-driven totals with the legacy statement and Trial Balance.
- Confirm Balance Sheet assets equal liabilities plus equity.
- Confirm Income Statement net profit agrees to the approved control total.
- Confirm segment-filtered runs preserve the same selected layout.
- Confirm CSV, PDF, print, and on-screen row sequence agree.
- Confirm historical dates resolve the expected effective version.
- Confirm an unauthorised user cannot import, change, or publish layouts.
- Confirm Trial Balance remains ordered by GL account number.
- Confirm layout-management navigation and action visibility follow the
  assigned manage/publish permissions.
- Confirm changing the workbook or JSON after preview requires a new preview.
- Confirm publishing retires the previously published version and leaves it
  visible in version and audit history.

## Sign-off record

Capture:

- Tenant, accounting book, statement type, layout code, and version.
- Tested period range and currencies.
- Reconciliation totals and any approved exceptions.
- Preparer, reviewer, finance approver, and approval timestamps.
- Links to exported CSV/PDF evidence.
- Decision: approved for publication, returned for correction, or rejected.

After approval, publish the draft. Only then set it as the default layout and
run one final production-equivalent reconciliation before general release.
