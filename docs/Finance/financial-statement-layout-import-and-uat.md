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
- API-driven classification mappings can be added to or removed from account
  rows in an editable Draft. Other structural changes continue through the
  controlled workbook or JSON definition.
- Validation findings and GL execution/reconciliation preview.
- Draft cloning, publication, default selection, and layout activation controls.
- JSON and workbook export of any version.
- Layout-specific audit events.

Protected standards are clone-only. An authorised user first clones the
standard into an editable tenant Draft and leaves the protected source intact.

## Authorisation

All template, preview, commit, and legacy-migration endpoints require
`Finance.Reports.Layouts.Manage`. Publication separately requires
`Finance.Reports.Layouts.Publish`. Preview and published execution require
`Finance.Reports.Run`. The register, detail, audit, and version-export endpoints
require `Finance.Read`. The workspace hides management, preview, and publication
actions when the signed-in user lacks the corresponding permission.

## API workflow

Routes are under `/api/finance/financial-statement-layouts`.

- `GET /import-template` downloads the controlled `.xlsx` template.
- `GET /versions/{versionId}/exports/json` exports a version as contract v2.
- `GET /versions/{versionId}/exports/workbook` exports a version as contract v2.
- `POST /{layoutId}/clone-standard` clones a protected standard into a Draft.
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
notes. The current template/JSON contract version is `2`; version 1 inputs are
rejected rather than reinterpreted.

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
- `Classification`: use the exact stable `ClassificationCode` from the selected
  book and set `IncludeClassificationDescendants` explicitly. Version 2 JSON
  imports require the stable code even when an internal classification ID is
  supplied. An optional ID must resolve to the same tenant/book classification
  as the code; IDs alone are rejected because they are not portable. Finance
  JSON exports omit tenant-local classification IDs and carry the stable code.

Account numbers are resolved only against accounts enabled for the selected
tenant accounting book. Classification display names are explanatory only;
free-text classification values and codes from another book are rejected.

### Lookups

Read-only tenant accounting-book, GL-account, and stable classification-code
reference information used to prepare the import.

## Publication and reproducibility

Draft validation and preview resolve classification membership from the live
hierarchy. Publication runs in one transaction with its audit event and stores
an immutable membership snapshot containing the exact tenant book, row/mapping
lineage, resolved account identity, frozen classification explanation, and
deterministic hierarchy/resolution fingerprints.

Published and retired versions execute only from that snapshot. Renaming or
reparenting a classification, reclassifying an account, disabling an account
mapping, or editing a later Draft does not change historical execution. A
fingerprint mismatch fails closed as snapshot tampering. Existing published
versions created before snapshot schema v2 cannot be executed through the new
path; during the approved development reset they must be recreated and
published, not silently backfilled from current hierarchy state.

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
- Confirm exact, range, account-hierarchy, and classification-hierarchy mappings
  resolve only inside the selected accounting book.
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
- After publication, rename/reparent a classification, reclassify one account,
  and disable one live mapping in a test tenant; confirm the Draft preview moves
  with live configuration while published/retired execution remains unchanged.
- Confirm the publication hierarchy and resolution fingerprints are stable for
  identical ordered evidence and that altered snapshot evidence is rejected.
- Confirm protected standards cannot be edited, retired, or published in place
  and their clones are ordinary editable tenant Drafts.

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
