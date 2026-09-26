# Finance Cash and Reporting Autonomous Pass — 2026-09-21

## Objective

Complete two authorized Finance packages without applying migrations or mutating accounting data:

1. Banking settlement and cash controls: separate register summaries from detail evidence, bound list queries,
   preserve returned-cheque allocation selection, and verify maker-checker/audit behavior.
2. Financial reporting execution: verify layout execution, exact-book and base-plus-Delta presentation, drill-down,
   export behavior, permissions, and performance; fix bounded defects with focused tests.

## Repository state

- Branch: `codex/finance-gl-latest-master-20260916`
- Base HEAD: `f734091bd38e866b9af7a2aa8e0ff8dbac01e0a1`
- Existing dirty Finance work is preserved in place.
- No database migration application, data reset, posting, approval, or remote operation is authorized.

## Work queue

| Order | Package | Status |
|---|---|---|
| 1 | Reconcile banking/cash list and detail contracts | COMPLETE |
| 2 | Implement bounded projections/paging without weakening evidence | COMPLETE |
| 3 | Verify deposit/returned-cheque/till controls and focused UI behavior | COMPLETE |
| 4 | Audit financial-report execution and base-plus-Delta presentation | COMPLETE |
| 5 | Correct reporting gaps and add focused tests | COMPLETE |
| 6 | Consolidated verification and handoff | COMPLETE |

## Constraints and known overlap

- The existing untracked `delta-report` page and current reporting service edits are treated as user/current-session
  work and inspected before any overlapping change.
- Cross-module producer implementations remain read-only.
- Schema changes may be documented but are not necessary for the intended read-model corrections.

## Findings and corrections

### Banking settlement and cash controls

- The deposit register reused the rich detail graph, loading allocations, source entries, attachments, bank-confirmation
  evidence, and reconciliation objects for every row. Returned-cheque listing followed the same detail-graph pattern.
- Register endpoints are now explicitly bounded to 200 rows by default and 500 maximum.
- Deposit summaries use a scalar projection. Allocation evidence is excluded by default and, when requested, is loaded
  in one batched projection for the already-bounded deposit IDs.
- The returned-cheque workflow explicitly requests allocation evidence because it needs that evidence to identify the
  deposited receipt. The ordinary deposit register remains summary-only.
- Returned-cheque summaries use a bounded scalar projection and one batched customer-name lookup; full attachment and
  audit evidence remains available through the existing detail endpoint.
- The bank-account cash-position fallback now supplies its required `asOfDate` instead of constructing an incomplete
  summary contract.

### Financial reporting execution

- The base-plus-Delta report previously materialized every matching posted transaction before aggregating in memory.
  It now performs account/book aggregation in the database.
- The report previously restricted transaction history to accounts that are currently enabled in book mappings. A
  later mapping disablement could therefore make genuine historical balances disappear. Report accounts now use the
  union of current enabled mappings and posted historical balance rows.
- A non-zero base-plus-Delta regression covers base `1,000`, Delta `125`, combined `1,125`, exclusion of draft and
  future lines, and continued visibility after a Delta mapping is disabled.
- The report UI now explains debit-minus-credit signs, labels book-wide sums as net reconciliation controls, warns
  that they are not financial-statement totals, highlights adjusted accounts, handles an empty report, and exports the
  evidence to CSV.

## Verification

- Backend focused suite: `AccountingBookPeriodInitializationC4Tests` plus
  `BankingSettlementReleaseGateTests` — **34 passed, 0 failed**.
- Frontend focused suite: Delta combined report plus cash register query contracts — **4 passed, 0 failed**.
- Targeted ESLint for all touched Finance UI/service files — **passed**.
- Full frontend `tsc --noEmit` remains red on the repository's pre-existing cross-module baseline. The output includes
  unrelated Administration, Development, Inventory, Procurement, Projects, QS, and test-fixture errors. One in-scope
  incomplete cash-position fallback contract found by that run was corrected; focused tests and lint remain green.
- API project and test assembly compiled successfully during the focused backend run. Existing compiler warnings remain.
- The rebuilt API was restored on `http://127.0.0.1:5012` (PID 41472); `/health/live` returns 200. Readiness returns
  the known 503 because ClamAV is unavailable, while its database and startup checks are healthy after warm-up.
