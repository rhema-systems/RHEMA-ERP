# Opening Balances, Multi-Currency, and Tax Production-Readiness Ledger

Date opened: 2026-09-29

## Objective

Audit and harden the Finance-owned governed opening-balance, multi-currency, and taxation features to production-readiness, close evidenced defects within Finance ownership, add focused regression coverage, and produce a gap/remediation summary.

## Boundaries

- Exact base: `8c45108ced691e29d655ed45fa4aec846f1b9a06`
- Branch: `codex/finance-ob-fx-tax-production-readiness`
- Worktree: `C:\Users\Akwas\Documents\DEV_WORK\RHEMA-ERP-finance-ob-fx-tax-production-readiness`
- Finance implementation and additive Finance contracts/adapters only.
- No Procurement, Inventory, HR/Payroll, Sales, Estate, Projects, or other module implementation changes.
- No migration application, database mutation, accounting posting, remote push, PR, or destructive cleanup.
- Schema changes require evidence and user escalation before migration source is created.
- Preserve existing primary-worktree changes and all other worktrees.

## Phases

| Phase | Scope | Status | Verification | Notes |
|---|---|---|---|---|
| 0 | Reconcile repository, inventory surfaces, baseline targeted tests | Complete | Repository/worktree inventory and targeted baselines completed | Primary worktree was preserved; all changes were made in the isolated worktree from the exact base. |
| 1 | Governed opening balances: GL batches, governed sources, subledgers, advances/WHT, diagnostics, reversal/replacement | Complete | 74 backend tests and 16 focused frontend tests passed | Production implementation already enforced maker/checker, exact-book periods, idempotent posting, immutable reversal/replacement lineage and diagnostics. The stale release fixture was repaired to exercise the current exact-book authority. |
| 2 | Multi-currency: currency/rate lifecycle, directional policy, transaction evidence, realized/unrealized FX, revaluation/reversal, reports/UI | Complete | 76 backend tests and 13 focused frontend tests passed | Silent conversion and legacy-rate bypasses were closed; rate reads, trends and bulk upload now fail closed against approved tenant evidence. |
| 3 | Taxation: configuration/versioning, calculation/posting evidence, VAT/WHT lifecycle, certificates/remittances, reports/exports/UI | Complete | 35 Tax tests, 6 WHT lifecycle tests and 106 Finance controller-security tests passed | Tax-rule governance and WHT remittance transactionality were hardened; statutory release fixtures were aligned with current book and WHT evidence requirements. |
| 4 | Integrated verification, documentation, ordered commits and final handoff | Complete | API build: 0 warnings/0 errors; `git diff --check`: clean | No schema change or data migration was required. Deployment/database-backed smoke testing remains an operational release gate. |

## Review and escalation record

- 2026-09-29: User authorised audit and remediation for all three Finance areas.
- 2026-09-29: No worker/reviewer tasks created; the request did not authorise task creation. Coordinator is implementing and reviewing locally.
- 2026-09-29: Database and migration application remain prohibited without separate user approval.

## Commits

1. `a845109c0` — `test(finance):restore-governed-opening-release-gate`
2. `e0052777f` — `fix(finance):fail-closed-in-multicurrency-governance`
3. `21f06dcc0` — `fix(finance):harden-tax-configuration-and-remittance-lifecycle`
4. Readiness ledger commit recorded in the final handoff after this document is committed.

## Findings and correction cycles

### Governed opening balances

The production service already had the required accounting controls: tenant and permission isolation, exact accounting-book and fiscal-period authority, balanced functional-currency batches, maker/checker separation, idempotent posting, protected-source dependency checks, immutable original/reversal/replacement evidence, and server-derived diagnostics/readiness.

The release suite had drifted behind those controls. Its fixtures created journals, transactions, posting events, periods and accounts without the now-mandatory exact accounting-book relationships. Twenty-eight scenarios therefore failed closed for the correct production reason. The fixtures now seed an active posting book, exact-book period, account-book classifications, and book-stamped posting evidence. The complete governed-opening suite passes 74/74 without weakening production validation.

### Multi-currency

Identified and corrected:

- Currency creation and the legacy `SetBaseCurrency` route could bypass the governed functional-currency change path. They now direct users to Finance Settings and fail closed.
- The legacy quick-rate update returned success without recording a rate. It now rejects the request and directs the user to the governed exchange-rate workflow.
- Currency conversion silently returned the unconverted amount when no rate existed. It now requires active tenant currencies and an active approved Daily/Mid direct, inverse or functional-currency-triangulated rate path; otherwise it raises an explicit failure.
- Deleted rates could appear in lookups, and trend data could include inactive, pending, rejected or non-Daily/non-Mid evidence. Reads and trends now use only eligible governed evidence.
- Rate creation accepted inactive/deleted currencies. Every pair now resolves to distinct active tenant currencies.
- Bulk rate validation previously allowed valid rows to persist while invalid rows failed, and workflow-start failures could leave unexplained pending records. The complete file is now validated before persistence, including intra-file overlap detection and row-numbered errors. Workflow failures are retained as rejected audit evidence and returned explicitly.

### Taxation

Identified and corrected:

- Tax-rule mutations lacked Finance audit events, could reference inactive tax groups, allowed ambiguous duplicate active matching criteria, returned a fabricated product-category name, and documented a hard delete while performing a soft delete. The controller now normalizes and validates inputs, requires an active tenant tax group, prevents ambiguous active matches under serializable mutation control, records create/update/deactivate audit evidence, returns no fabricated category, and explicitly deactivates/soft-deletes.
- WHT remittance submit, paid and cancel transitions did not wrap state and audit changes in the service's existing serializable execution boundary. They now do, preserving state/audit atomicity and concurrency safety.
- Tax reporting fixtures omitted the current exact-book identity on posted journals, lines and posting events; the AP WHT payment fixture also omitted required contract and supply-category evidence. Those release fixtures were brought up to the current production contract. No production control was weakened.

The WHT remittance register remains statutory evidence rather than an alternative cash/GL posting engine. Cash settlement continues through the normal governed Finance payment/journal path.

## Verification record

| Gate | Result |
|---|---|
| Governed opening-balance backend | 74/74 passed |
| Multi-currency/FX backend | 76/76 passed |
| Tax backend | 35/35 passed |
| WHT compliance lifecycle | 6/6 passed |
| Finance controller permissions/security | 106/106 passed |
| Focused Opening Balance and multicurrency frontend | 29/29 passed across 7 files |
| API build | Succeeded, 0 warnings, 0 errors |
| Diff hygiene | `git diff --check` clean; only repository line-ending notices |
| Repository-wide frontend type check | Baseline failure outside this change set; no frontend files were modified. Errors span Civil Engineering, Inventory, Procurement, Quantity Survey and pre-existing Finance test/mock files. |

## Release conditions and residual risk

- No migration or schema change is required.
- No database was migrated or mutated during this work.
- The automated suites use their configured test providers. Before production deployment, run the normal staging deployment, relational-database migration-status check, and a tenant-isolated smoke/UAT pass for one governed opening post/reversal/replacement, one approved FX rate and conversion, one revaluation, one tax-rule change, and one WHT remittance lifecycle.
- Bulk FX import is validation-atomic. Workflow submissions are intentionally auditable rather than transactionally coupled to an external workflow engine: a workflow-start failure is retained as rejected evidence and reported to the caller.
- The repository-wide frontend type-check baseline is not green. This change set introduced no frontend source changes and its 29 focused Finance UI tests pass, but the independent type errors listed in the verification output must be cleared by their owning workstreams before treating a repository-wide frontend build as a release gate.
