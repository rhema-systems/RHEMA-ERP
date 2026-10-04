---
integration_cycle: FIN-UAT-2026-10-03-A
integration_status: integrated
integration_decision: include
candidate_branch: codex/finance-uat-precision-consolidated-20261003
candidate_head: f8b836269cac7ec71c486692bca44e29bb5287a3
base_commit: 0e69222faf4026de32c2df22757cd5e31ad9cbf3
target_ref: origin/master
depends_on: none
migration_status: authored-unapplied
verification_status: passed
integration_commit: 28dc3449656de9fa26a47d79b65e5360ac34e897
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/343
---

# Finance UAT workstream: consolidated precision completion

## Objective and scope

Consolidate the governed Finance precision completion, customer-credit/recurring-journal stabilization, and already-present WHT and settings/calendar hardening into one PR from clean `origin/master`. The dirty primary checkout, unrelated worktrees and artifacts were excluded; only the authorized root `AGENTS.md` was copied from primary.

## Workspace and Git state

- Worktree: `C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP/.w/fupc1003`
- Branch: `codex/finance-uat-precision-consolidated-20261003`
- Exact refreshed base: `0e69222faf4026de32c2df22757cd5e31ad9cbf3`
- Precision candidate head: `f8b836269cac7ec71c486692bca44e29bb5287a3`

## Implementation record

- `17c4b5059` and historical `671d971c3`, `4ea2465a2`, `a5aa89a5f`, `b531b8011`, and `c228c1ce8` are ancestors of refreshed `origin/master`; they were not replayed.
- `0ff788a98` is not hash-ancestral, but semantic replay became empty after preserving both compatible calendar tests because refreshed master already contains the implementation.
- `3ab690f0d` integrated as `839e9bad1`.
- Precision source commits integrated in order as:
  `4cb174ca3→6bc70b543`, `192eda1a6→bc0cd5d36`, `79d14f619→85c7983d3`,
  `40efdc0b0→c5a5387d9`, `bd27fa867→dede34186`, `7d8653f7b→beeef039a`,
  `c2c5c6968→65383afd1`, `6679c4fac→eea653b62`, `a4a7ee922→ecf241026`,
  `e36b43342→f64af9050`, `29a191862→d3ca5e89e`, `db3e1c2b1→a555e4d49`,
  `ff45b8c21→ad6791f6e`, and `f8b836269→12e81c3be`.
- All eight `FinanceRoundingEvidence` type references are fully qualified.
- Integration reconciliation explicitly preserves Procurement-owned UOM increments/evidence at `decimal(18,6)`; no four-decimal narrowing was accepted.
- The RFQ quote-item shadow UOM relationship is retained, preventing a destructive EF drop, and the model snapshot is reconciled.

## Migration record

Included and **unapplied**: `20261002143000_EnableInvoiceCashRoundingPosting`,
`20261002153000_AddCommercialUomQuantityGovernance`,
`20261002183000_FinanceTaxPrecisionCompletion`,
`20261002210600_AddCommercialQuantityLifecycleEvidence`,
`20261002213000_FinancePrecisionStorageCorrections`,
`20261003070000_AddFinanceRoundingEvidenceReconciliation`, and
`20261003090000_AddSalesCommercialQuantityAuthorities`.
Migration IDs are ordered with no duplicate 14-digit IDs. No migration was applied to a UAT, development, or user database; the relational tests used uniquely named LocalDB databases created with `EnsureCreatedAsync` and removed with `EnsureDeletedAsync`.

## Verification evidence

- Solution build: passed, 0 errors (695 warnings); API build: passed, 0 errors (156 warnings).
- EF `has-pending-model-changes`: exit 0, no pending model changes.
- Relational AR/AP JPY/KWD/CLF precision batch: 2/2 passed.
- Commercial UOM policy/persistence/sales evidence: 24/24 passed, including six-decimal micro-increment persistence.
- Invoice guard/customer/calendar/journal API group: 72/72 passed.
- Settings/tax/cash-rounding/migration-contract API group: 74/74 passed.
- Core precision policy: 22/22 passed.
- Unit-accounting/UOM services: 52/52 passed.
- Changed-file ESLint: 18/18 files passed (existing Next.js pages-directory advisory only).
- Recurring-journal frontend test: 5/5 passed.

## Known failures and risks

No focused failure remains. Existing compiler warnings are baseline warnings. Deployment must apply and validate migrations under separate authorization.

## Remaining work

Await PR review and merge. No implementation or focused verification work remains.

## Authorization boundaries

Push and one PR are authorized. `git pull`, primary-checkout mutation, migration application, deployment, force-push, worktree removal, and branch deletion are not authorized and were not performed.

## Integration outcome

One mechanical calendar-test conflict preserved both compatible tests and resolved to an empty cherry-pick. Model parity and all focused verification are green. Integration reconciliation commit: `28dc3449656de9fa26a47d79b65e5360ac34e897`. Pull request: https://github.com/rhema-systems/RHEMA-ERP/pull/343.
