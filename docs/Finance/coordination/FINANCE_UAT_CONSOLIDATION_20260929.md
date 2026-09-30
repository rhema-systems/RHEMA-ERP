# Finance UAT consolidation — 2026-09-29

## Release basis

- Branch: `codex/finance-demo-remediation-20260929`
- Base: latest integrated `origin/master` at `bdbe9c625`
- Target database: `RHEMAERP_BOOKV2_UAT_20260922` on `RHEMA-AKWASI\EXPRESS22`
- Scope: governed opening balances, FX/multicurrency, taxation/WHT, budgeting, cash/bank, recurring journals, journal batches, AP/AR invoice hardening, and fixed-asset maintenance eligibility.

## Fixed-asset maintenance contract

- `RequiresMaintenance` is controlled on the fixed-asset category and defaults to `false` for existing and new categories.
- Finance users can maintain the flag through category create/edit screens.
- Maintenance receives a tenant-scoped, read-only list containing operational asset/category identity only.
- Deleted assets, deleted categories, and categories that are not opted in are excluded.
- The feed intentionally exposes no acquisition value, depreciation, net book value, GL account, or posting data.

## Consolidation corrections

- Reconciled opening-balance and Ghana tax fixtures with canonical approved Business Partners, roles, and effective Finance profiles.
- Reconciled posting fixtures with exact accounting-book functional-currency authority.
- Corrected directional FX conversion lookup and triangulation, and rounded results to the target currency's configured precision.
- Preserved source-controlled AP invoice lines while keeping manual AP lines restricted to GL expense/asset accounts.
- Updated focused journal-batch and AP UI fixtures to the current production contracts.

## UAT database deployment

Read-only preflight confirmed the database was `ONLINE`, `MULTI_USER`, and at migration `20260928221517_AddCustomerInvoiceCurrencyOverrideReason`.

Applied successfully:

1. `20260929102006_JournalBatchStableAccountingBook`
2. `20260929115243_ArInvoiceDiscountGovernance`
3. `20260929153242_FixedAssetCategoryMaintenanceEligibility`

Postflight confirmed all three migration-history rows and expected columns. Existing fixed-asset categories remained opted out (3 categories, 0 enabled). `DBCC CHECKDB` completed without errors.

## Verification evidence

- Consolidated focused backend gate: 252 passed, 0 failed.
- Opening-balance, Ghana tax, and FX governance group: 129 passed, 0 failed.
- Consolidated focused frontend gate: 45 passed, 0 failed.
- API build: 0 warnings, 0 errors before the final integration-fixture correction; the subsequent test build completed with no errors and repository-existing warnings.
- UAT migration preflight, deployment, schema postflight, and database consistency check: passed.
- Final cross-package Finance gate: 177 passed, 0 failed.
- Source-book authority trigger regression gate: 30 passed, 0 failed.
- EF model-drift scaffold: empty `Up` and `Down`; reconciled snapshot committed.
- SQL Server disposable migration rehearsal and UAT hardening postflight: passed.

## Authenticated browser smoke — 2026-09-29

- Replaced the stale frontend previously listening on `http://localhost:3002` with this consolidated branch and connected it to the UAT API at `http://127.0.0.1:5012/api`; the existing authenticated browser session remained intact.
- Verified the active Default-tenant session is `System Administrator` with the `SuperAdmin` role.
- Fixed-asset categories render the Maintenance column, and the create form exposes `Requires maintenance` defaulted off with the read-only-feed explanation.
- AP and AR invoice forms load governed currency, tax, account, dimension, discount, readiness, and totals controls from the UAT API. The configured default tax treatment is present on the initial line and on an added AP line; the AR initial line also inherits it.
- The Finance Approval Workbench, governed opening balances, recurring journals, journal batches, budget reconciliation, bank accounts, currency master, and tax-definition register loaded without an authentication or API error.
- The WHT-SERV detail shows the configured 7.5% rate and GHS 2,000 threshold. Its edit dialog is internally scrollable and keeps Cancel/Update actions visible; it was closed without saving.
- No invoice, journal, approval, posting, reversal, tax, currency, budget, bank, or asset record was created or changed during this read-only smoke.
- The UAT tenant currently has no fixed-asset categories in the signed-in Default-tenant view and no recurring journal or bank-account records, so populated-list behavior was not exercised in this browser pass.

## Hardening deployment — 2026-09-30

A checksum-verified `COPY_ONLY` backup of UAT was restored into `RHEMAERP_BOOKV2_UAT_FIN_REHEARSAL_20260930`. The full pending migration chain was applied there before UAT. Rehearsal exposed and prevented a SQL Server aggregate error in the source-book-authority evidence trigger; migrations `004` and `005` were corrected, regression-tested, and the disposable database was restored from the clean backup before the successful rerun.

Applied successfully to rehearsal and UAT:

1. `20260930000100_YearEndBookCloseCycles`
2. `20260930000200_ApWithholdingNetBasisEvidence`
3. `20260930000300_LeaseInstalmentApOpenItems`
4. `20260930000400_FinanceSourceBookAuthority`
5. `20260930000500_AddFinanceSourceBookAuthorityCallerBindings`
6. `20260930000600_AddCapitalizationLineage`

The fresh UAT rollback point is `RHEMAERP_BOOKV2_UAT_20260922_pre_finance_demo_remediation_20260930_0545.bak`; it was created with `COPY_ONLY, CHECKSUM` and passed `RESTORE VERIFYONLY WITH CHECKSUM`. Rehearsal and UAT postflight both confirmed every migration row and governed object, zero untrusted foreign keys, zero untrusted check constraints, and a clean `DBCC CHECKDB`.

## Governed UAT configuration rollout

Read-only inspection found zero bank accounts, zero Finance access-scope grants, and zero dimension account rules. These are tenant configuration gaps, not schema or posting-code failures. They must not be repaired with direct SQL.

- Keep `EnforceFinanceAccessScopes` disabled until grants exist and positive/negative authorization tests pass.
- Create a GHS operating bank through the Finance bank-account workflow against the existing active direct-posting cash GL. Create or approve a USD cash/bank GL through governed chart-of-accounts workflow before creating a USD bank account.
- Grant the minimum book/unit scopes to distinct creator, approver, and poster/reverser identities from authenticated application context. Do not infer or invent identity GUIDs from the Finance database.
- Configure dimension-account rules through Finance Dimensions, run certification, remediate every blocker, and promote routes to enforced only after zero-blocker evidence is recorded.
- Enable scope enforcement last, then repeat allowed and denied posting tests for each role.

The browser automation helper was unavailable during the 2026-09-30 configuration pass, so no governed configuration record was created and no enforcement flag was changed. This is deliberately fail-safe.

## Remaining UAT/cutover gates

These are not code defects in this package, but still require operational completion before production certification:

- Complete maker/checker browser evidence with separate creator/importer, approver, and poster/reverser users. The SuperAdmin smoke above verifies integrated loading but cannot prove separation of duties across identities.
- Rehearse deployment and rollback against a production-shaped backup and obtain accountant/security sign-off.
- Resolve or formally waive the unavailable ClamAV dependency that causes readiness health to return 503.
- Resolve or accept the pre-existing repository-wide frontend TypeScript baseline outside the changed Finance files.
- Review tenant-specific role grants and close or formally accept the documented Finance security limitation.
