# Fixed Asset Sale Settlement PR Summary

## Outcome

This change resolves `FIN-LIM-0040` for TDC Finance-owned fixed-asset sales. Completing an approved positive-proceeds disposal now posts the existing derecognition journal, creates/posts the canonical statutory AR invoice, and optionally creates/posts a fully allocated immediate receipt.

## Main implementation

- Extends disposal request and evidence records with the canonical buyer, tax decision, settlement mode, payment destination and linked AR documents.
- Reuses `InvoiceService`, the effective-dated Tax engine, `PaymentService` and `IFinancePostingEngine`; it does not introduce parallel receivables, tax, cash or GL logic.
- Executes derecognition, invoice, tax, receipt allocation and posting inside one serializable SQL transaction.
- Reconciles an evidenced buyer/auctioneer deduction through a restricted, out-of-scope AR contra line while keeping statutory tax on gross sale consideration.
- Rejects spoofed negative AR lines, stale/non-tenant master data, incompatible payment destinations, currency mismatches and unsafe bulk sales.
- Adds direct disposal-history links to the normal AR invoice and receipt workspaces.

## Persistence

Migration `20260813103000_AddFixedAssetDisposalSettlement` adds the settlement evidence, restrictive foreign keys, unique linked-document indexes and database check constraints to `AssetDisposals`.

The migration is deliberately focused and hand-authored because the shared multi-module model snapshot contains unrelated historical drift. EF discovers it as the current end of the migration chain, and the generated SQL was rendered successfully for dry-run review.

## Verification completed

- Backend/API test-project build: passed with zero errors; reported warnings are the existing shared repository warning backlog.
- Focused Finance tests: 50 passed, 0 failed.
- Targeted frontend ESLint for the disposal page and fixed-asset types: passed.
- EF migration discovery: passed; the new migration is listed after `20260812170000_ExtendControlledSourcingMethods`.
- Focused migration SQL generation: passed.
- Finance owner handbook generation and link verification: passed.
- `git diff --check`: passed.

## Deployment and UAT gates

Do not treat source merge as database deployment. Apply the migration first to the TDC UAT/dry-run database and execute the representative scenarios in `docs/fixed-asset-sale-settlement-foundation.md`, including a taxable credit sale, taxable immediate receipt, non-bank holding-account receipt, foreign-currency sale and an injected downstream failure proving atomic rollback.
