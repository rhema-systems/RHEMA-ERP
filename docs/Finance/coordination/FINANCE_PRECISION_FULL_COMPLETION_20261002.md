# Finance precision full completion ledger — 2 October 2026

## Authority and exact base

- Objective: close every documented decimal-precision governance gap and replace the deliberately fail-closed adapters with fully governed production behavior.
- User authority: implement the feature fully; local implementation, tests, migrations and clean integration are authorized.
- Exact base: `d47edeb9422d6d687f7a55b45aa2a862e9585e3c` (`origin/master` at package start).
- Coordinator branch: `codex/finance-precision-full-completion-20261002`.
- Coordinator worktree: `C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP/.codex-worktrees/finance-precision-full-completion`.
- Migrations may be authored but remain unapplied. No database/accounting-data mutation, push, PR or merge is authorized in this phase.
- The primary dirty checkout remains untouched.

## Completion scope and acceptance criteria

1. Document- and tax-code-group aggregate tax rounding operates across complete AR/AP documents, with deterministic residual allocation and durable evidence.
2. Tax calculation and posting support transaction currencies with ISO 0–4 minor units without truncation, including signed credit/reversal and idempotent replay paths.
3. Invoice/cash rounding posts an explicit balanced gain/loss line through canonical posting paths, reverses and replays safely, and can be activated only with valid governed accounts.
4. Configured UOM quantity precision/increments are enforced at Finance-owned write/import/posting boundaries; unsupported activation gates are removed only after complete coverage.
5. Nullable precision/rounding settings distinguish omitted fields from explicit null clear operations across API, service, audit evidence and UI.
6. Existing two-decimal line-rounding behavior remains backward compatible.
7. All schema changes are additive; migrations are reviewed and left unapplied.
8. High-risk accounting changes receive independent review after integration.

## Work packages

| Package | Branch | Owner | Status | Dependencies |
| --- | --- | --- | --- | --- |
| Tax orchestration, storage and signed flows | `codex/finance-precision-tax-completion-20261002` | `/root/precision_tax_completion` | In progress | Exact base |
| Invoice/cash rounding gain/loss posting | `codex/finance-precision-rounding-posting-20261002` | `/root/precision_rounding_posting` | In progress | Exact base |
| UOM enforcement and null-clear PATCH semantics | `codex/finance-precision-uom-patch-20261002` | `/root/precision_uom_patch` | In progress | Exact base |

## Integration and review plan

- Reconcile every handoff against the exact commit range and changed files.
- Review migrations, posting balance, account eligibility, reversal and replay evidence before integration.
- Integrate approved commits in the lowest-conflict dependency order, resolving overlaps only where behavior is unambiguous.
- Run focused suites per package followed by combined Core/API builds and relevant frontend checks.
- Dispatch an independent read-only Sol High review of the integrated branch; return findings for correction until approved.

## Current checkpoint

- Existing governance and additive migration are already on `master` through the prior precision integration.
- The six deferred gaps were confirmed from the durable package ledger.
- Three isolated implementation tasks were dispatched from the same exact master base.
- No migration has been applied and no database has been contacted.

## Integrated completion checkpoint — 3 October 2026

### Working state and authority

- Coordinator branch remains `codex/finance-precision-full-completion-20261002` at `ff45b8c21`; the integrated completion changes remain uncommitted for review.
- The user explicitly required this continuation to run without sub-agents. No sub-agent or independent review task was started in this continuation.
- No migration was applied, no database was contacted, and no push, PR or merge was performed.

### Final compile and fixture corrections

- `ApplicationDbContext` now fully qualifies all eight `FinanceRoundingEvidence` entity-type references in the final precision override (`OriginalAmount`, `RoundedAmount`, `DeltaAmount`, `Increment`, `OriginalFunctionalAmount`, `RoundedFunctionalAmount`, `FunctionalDeltaAmount`, and `ExchangeRate`). This removes the collision with the `FinanceRoundingEvidence` `DbSet` property.
- The `FinanceRoundingEvidence` tenant relationship now binds the concrete `Tenant` navigation rather than synthesizing a shadow `TenantId1` property.
- AR/AP relational precision fixtures use `CLF` as the valid ISO three-letter, four-decimal currency instead of the invalid pseudo-code `X04`.
- `SalesOrderInvoiceGuardTests` now supplies governed UOM evidence and a matching validator authority, keeping the fixture compliant with the integrated fail-closed UOM boundary.

### Verification results

- `dotnet build ErpSystem.sln --no-restore`: passed with 0 errors (156 existing warnings).
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore`: passed with 0 errors after the tenant-navigation correction.
- AR/AP relational precision batch (`Batch=FinancePrecisionRelational`): 2/2 passed across JPY, KWD and CLF cases.
- Core commercial-quantity/UOM suites (`CommercialQuantityPolicyTests`, `CommercialQuantityEvidencePersistenceTests`, and `SalesCommercialQuantityEvidenceTests`): 24/24 passed.
- `SalesOrderInvoiceGuardTests`: 16/16 passed after adding governed UOM evidence to the fixture.
- Package-owned frontend lint surface (11 changed Finance/UOM files, evaluated with the repository ESLint configuration): passed. The full repository lint remains baseline-red with 148 errors and 29 warnings in unrelated files; the worktree itself has no local `frontend/node_modules`, so the matching primary-checkout ESLint installation was used.
- `git diff --check`: passed before the ledger update; only Git line-ending notices were emitted during earlier diff inspection.

### Migration and model-parity evidence

- Static inspection of the focused migration-discovery assertion confirms it covers the authored `20261002183000_FinanceTaxPrecisionCompletion` and `20261002213000_FinancePrecisionStorageCorrections` migrations, physical AR/AP invoice tables, and nullable legacy evidence. Its final rebuilt invocation stalled in the repository's generated migration-model compiler and was terminated after repeated no-output waits; the test did not complete and no database operation was involved.
- `dotnet ef migrations has-pending-model-changes` no longer reports the erroneous `FinanceRoundingEvidence.TenantId1` shadow property after the navigation fix, but still reports pending model changes.
- A temporary, unapplied EF probe was generated solely to classify that remaining drift. It contained 86 operations: 35 decimal alterations, 23 index creations, 24 foreign keys, one dropped foreign key, one dropped column, and a check-constraint replacement. The operations span previously authored UOM/tax migrations and unrelated snapshot drift; several would incorrectly reverse governed UOM rounding-increment storage from `decimal(18,6)` to `decimal(18,4)`.
- The probe migration was rejected and removed, and its generated snapshot rewrite was restored. The remaining EF parity failure is therefore recorded as broader pre-existing/manual-migration snapshot reconciliation work, not converted into a new schema migration and not hidden by accepting regressive generated operations.

### Remaining completion boundary

- Functional precision behavior, relational AR/AP coverage, governed UOM coverage, and compilation are green.
- Repository-wide EF runtime-model/snapshot parity remains red and requires a separately bounded reconciliation of the accumulated hand-authored migration metadata. That work must preserve the governed six-decimal UOM evidence columns and must not be applied to a database without renewed authorization.

## Consolidated integration outcome — 4 October 2026

- Precision completion was committed as `f8b836269cac7ec71c486692bca44e29bb5287a3` and integrated into `codex/finance-uat-precision-consolidated-20261003` as `12e81c3be` from refreshed base `0e69222faf4026de32c2df22757cd5e31ad9cbf3`.
- Integration reconciliation preserves Procurement-owned commercial UOM increments and evidence at `decimal(18,6)`; no four-decimal narrowing was accepted. The RFQ quote-item shadow UOM relationship was also retained to avoid a destructive generated drop.
- The model snapshot was reconciled to the authored migrations. A definitive `dotnet ef migrations has-pending-model-changes` check now exits 0 with no pending changes.
- Combined verification passed: solution/API builds; 2/2 relational AR/AP JPY/KWD/CLF tests; 24/24 commercial UOM tests; 72/72 guard/customer/calendar/journal tests; 74/74 settings/tax/cash-rounding/migration tests; 22/22 precision-policy tests; 52/52 unit-accounting/UOM tests; 18-file frontend lint; and 5/5 recurring-journal frontend tests.
- All migrations remain authored and unapplied. No database was contacted or mutated.
- Consolidated PR: pending creation.
