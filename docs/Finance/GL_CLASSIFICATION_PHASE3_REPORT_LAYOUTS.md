# Configurable GL classifications — Phase 3 report layouts

Status: Finance implementation pending independent review

## Delivered authority model

`FinancialStatementRowMappingType.Classification` is a first-class mapping in
the existing financial-statement layout engine. It stores a Finance-owned
classification identifier and the explicit descendant-expansion choice. API and
workbook boundaries also carry the stable classification code so imports never
depend on editable display text.

Resolution is always scoped to the layout tenant and its exact active,
posting-enabled accounting book. Draft validation expands the live hierarchy,
rejects retired or foreign-book nodes, checks statement/account-type
compatibility, and rejects overlap both within a row and across contributing
rows. Account, AccountHierarchy, AccountRange, and Formula behavior remains
supported.

## Immutable publication evidence

Publishing a Draft resolves every contributing mapping and persists snapshot
schema `2` evidence in the same serializable transaction as the version state,
prior-version retirement, and Finance audit event. Each captured account records:

- exact accounting-book ID and stable code on every captured row;
- row and source-mapping identity plus a readable frozen selector;
- account ID, number, name, and core account type;
- classification ID, stable code, frozen name, and frozen hierarchy path;
- deterministic hierarchy and resolution fingerprints on the version, including
  the frozen header book ID, stable code, and name;
- publication actor and timestamp on the version.

An audit or snapshot write failure rolls back publication. Published and retired
execution verifies the snapshot schema, exact requested book, and resolution
fingerprint, then queries ledger evidence for only the frozen account set. It
does not consult current membership. Draft preview intentionally re-resolves the
live hierarchy.

This makes historical results insensitive to later account reclassification,
classification rename/reparenting, mapping disablement, or later Draft edits.
Snapshot evidence is append-only: corrections require a new version and
publication, never mutation of an old snapshot.

## Configuration and contracts

- The Finance layouts workspace loads compatible classifications from the API
  for the layout's exact book and supports adding/removing classification
  mappings on Draft account rows.
- Published snapshot count and fingerprints are visible in version inspection.
- Contract/template version `2` adds `ClassificationCode` and
  `IncludeClassificationDescendants`. JSON imports require the stable code;
  an optional ID must identify the same exact-book classification. ID-only,
  version 1, unknown, mismatched, and free-text values fail with compatibility
  errors.
- JSON and XLSX exports preserve stable classification selectors.
  JSON definitions deliberately omit tenant-local classification IDs.
- Protected Balance Sheet and Income Statement standards are seeded per active,
  posting-enabled canonical book. They are clone-only and never edited,
  retired, or published in place.
- Classification where-used evidence distinguishes live Draft dependencies from
  immutable published/retired snapshot references. Unsafe structural changes
  are blocked by live Draft references; historical snapshots remain visible but
  do not weaken Phase 2 lifecycle controls.

Permissions remain split: `Finance.Read` is the prerequisite for workspace
browsing, loading, audit, and export. `Finance.Reports.Run`,
`Finance.Reports.Layouts.Manage`, and
`Finance.Reports.Layouts.Publish` are additional action gates and never
substitute for read access.

## Migration and existing data

Migration `20260903130000_AddFinancialStatementClassificationSnapshots` is a
narrow forward schema change and remains unapplied. It adds classification row
selectors, protected-standard lineage, publication metadata, and immutable
publication-account evidence. The authoritative EF model snapshot and fast-build
migration metadata are updated; the three large SQL reference snapshots remain
untouched.

Preexisting Published or Retired versions have no trustworthy historic resolved
membership to backfill. They therefore fail closed until recreated and published
under snapshot schema 2. This is compatible with the approved pre-live reset and
avoids fabricating historical evidence from today's hierarchy.

## Legacy consumer inventory and cutover boundary

The layout import service retains a deliberate, controlled legacy-migration
bridge from `AccountAccountingBook.FinancialStatementLineItem` and the old
IFRS/statutory/management account fields into reviewed Draft layouts. The legacy
Income Statement and Cash Flow presentation path still reads those fields when
callers explicitly decline layout execution. Those fields cannot be dropped in
Phase 3 because that fallback and external/cross-module DTO consumers remain
active.

The new classification mapping/snapshot path is authoritative whenever a layout
is previewed or a published/default layout is executed. Retiring the fallback
requires separate reconciliation/UAT proving every statement entry point uses a
published snapshot-backed layout, followed by the coordinated external-owner
and V1 cutover. No other module implementation or posting contract changes are
required by Phase 3.

## Deferred

Revaluation defaults/overrides and signed math remain Phase 4. Mandatory GL
identity segments remain Phase 5. Journal-line description copying and recent
account transactions remain Phase 6.
