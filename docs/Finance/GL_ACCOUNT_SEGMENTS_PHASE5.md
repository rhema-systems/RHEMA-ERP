# Phase 5 — GL identity segments and transaction dimensions

## Target model

GL account identity and transaction analysis are separate Finance concepts.

- Account-number structure starts with `COMPANY` (Legal Entity / Company) followed by `NATURAL_ACCOUNT` (Natural Account).
- Every Active or Frozen account-number segment is required. There is no administrator or API `IsMandatory` choice.
- `DEPARTMENT`, `PROJECT`, `ESTATE`, `CONTRACT`, `FUNDING_SOURCE`, and `ACTIVITY` are transaction coding dimensions in the existing Finance dimension definition/value/rule/route model. They are not duplicated in account identity.

The server loads the exact active tenant structure, requires one submitted value per definition and no extras, validates tenant, position, length, data type and lookup lineage, and composes the account number in stable position order. A client-supplied number is comparison evidence only and cannot override the composed identity.

## Lifecycle and change control

Definitions move from Draft to Active and may then be Frozen. Structural edits and reorder are allowed only before use under the governed service. Frozen definitions cannot be changed. Row versions reject stale updates; mutations and Finance audit evidence share a serializable transaction on relational databases. Existing accounts remain readable. An account whose stored values do not exactly equal the active structure is reported by the segment-readiness endpoint and must be remediated explicitly before structure freeze.

## Deterministic manifest

`FinanceSegmentDimensionManifestSeeder` resolves by tenant plus stable code. It creates missing standard records with tenant-derived stable IDs, upgrades only untouched system-managed `ACCT` identity to `NATURAL_ACCOUNT`, and retires only system-managed `DEPT`/`PROJ` identity definitions. Existing administrator-managed records and dimension definitions are not overwritten. COMPANY values are derived from the tenant code; the seed never guesses operational natural-account or analytical dimension values.

Run the manifest for each tenant during normal Finance provisioning. It is repeatable and tenant-isolated. The normal Finance chart seed creates new accounts with both identity values; it does not fabricate missing values on older accounts.

## Development reset and upgrade gate

The Phase 5 migration is forward-only and remains unapplied in this branch. It includes a fail-closed preflight for duplicate stable codes, active positions, natural segments, account/segment assignments and account positions; it does not repair ambiguous data. Before a shared reset or upgrade:

1. run migration-operation and EF pending-model checks;
2. inventory duplicate active segment positions, duplicate account/segment values, and incomplete identities;
3. migrate an existing disposable development database and also build/reseed an empty database;
4. review the readiness endpoint for every retained account;
5. recreate each developer database only at the coordinator-approved integration commit.

No historical account identity is silently reinterpreted. If retained development data cannot pass readiness, reset it or process it through a separately approved remediation workflow.

## External owner boundary

Finance provisioning continues to own account creation and now derives the COMPANY/NATURAL_ACCOUNT identity itself. Other modules must supply a reviewed natural account code through the Finance-owned provisioning boundary and must not compose GL account numbers or create account-segment rows directly. V1 posting compatibility is unchanged by this phase.
