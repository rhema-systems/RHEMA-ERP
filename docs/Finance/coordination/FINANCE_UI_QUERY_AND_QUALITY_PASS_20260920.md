# Finance UI Query and Quality Pass — 2026-09-20

## Objective

Use the approved unattended Finance quality window to remove the account-classification query regression, identify
comparable Finance UI read paths that can time out as data grows, and then perform focused functional audits of the
next Finance surfaces without applying migrations or mutating accounting data.

## Repository state

- Branch: `codex/finance-gl-latest-master-20260916`
- Starting HEAD: `f734091bd38e866b9af7a2aa8e0ff8dbac01e0a1`
- Worktree: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP-finance-gl-latest-master-20260916`
- Existing dirty Finance work is user-owned/current-session work and must be preserved.
- Database migrations, destructive cleanup, accounting-data mutations, remote pushes, and cross-module edits are excluded.

## Work queue

| Order | Package | Scope | Status |
|---|---|---|---|
| 1 | Classification read regression | Replace full child/mapping graph materialization with scalar counts; verify tests and live API latency. | COMPLETE |
| 2 | Finance list-query hardening | Audit and repair graph-heavy or unbounded list endpoints used by Finance UIs. | COMPLETE — five query families corrected; remaining risks recorded |
| 3 | Financial-statement layouts and reporting | Vet layout/version lifecycle, selectors, publication evidence, execution/read performance, permissions, and client-facing UX. | COMPLETE — layout register and audit lookup corrected |
| 4 | GL inquiry and journal surfaces | Vet account activity, journal registers/batches, pagination, exact-book authority, errors, and evidence navigation. | COMPLETE — journal entry and batch registers corrected |
| 5 | Period-close and readiness | Vet accounting-book periods, close blockers, readiness projections, empty/error states, and authoritative drill-downs. | COMPLETE — focused readiness/period tests passed |
| 6 | Finance approval and navigation consistency | Vet maker-checker visibility, workbench projection, action URLs, permission parity, and stale/pending-state UX. | COMPLETE — queue tests passed; book workflow deep links added |
| 7 | Consolidated verification | Run focused backend/frontend suites and document remaining risks and recommended stakeholder tests. | COMPLETE |

## Package 1 evidence

- Root cause: `AccountClassificationService.GetAsync` loaded `Children` and every `AccountMapping` into memory only
  to calculate four counts. The now-populated book-mapping rows made the split include query exceed the frontend's
  20-second timeout.
- Correction: project the classification summary and correlated scalar counts directly in SQL; use the same bounded
  projection for create/update/retire response reloads.
- Focused backend verification: `AccountingBookClassificationAuthorityTests` — 45 passed, 0 failed.
- Live API verification against the current local development database:
  - `/health/live`: HTTP 200.
  - login: 4,109 ms.
  - IFRS classifications (`includeInactive=true`): 29 rows in 346 ms, previously cancelled after 20,000 ms.

## Package 2 initial findings

1. **High — Journal batch register:** the paged list includes each batch's items, journal entry, and every journal
   transaction although the list needs only totals and counts. Replace with an aggregate projection.
2. **High — Budget revision register:** the unpaged list includes complete source-scenario returns and entries plus
   every revision line and related account/period/segment, then maps each full graph. Introduce a bounded summary
   projection and reserve the full graph for the detail endpoint.
3. **High — Finance-owned source dimension readiness:** validation issued independent account/rule/definition/value
   queries for every source line. Batch the governed validation context once per readiness request.
4. **High — Journal entry register:** the header list loaded transactions, accounts, dimension sets/snapshots and
   attachments for every row. Reserve that graph for detail and project the register fields in SQL.
5. **Medium — Cashier till register:** its shared query includes count lines and then performs two activity queries
   for every open session. Batch the live activity window for the register.
6. **Medium — Banking settlement registers:** deposit and returned-cheque shared queries include attachment and
   allocation graphs. Confirm list/detail separation and paging.
7. **Medium — Financial-statement layout detail:** full layout/version/row/mapping/publication graphs are loaded for
   single-record operations. This is appropriate for some detail actions but needs size limits and purpose-specific
   projections for list or status calls.
8. **Low — Account segment structures:** the list graph is naturally small and separately aggregates usage counts;
   retain unless measurement shows a problem.
9. **High — Authentication provider routing:** an LDAP-enabled tenant attempted an LDAP network call even for known
   local identities. An unavailable directory therefore presented as an API/login timeout for local Admin users.

## Package 2 completed corrections and evidence

- **Journal batch register:** replaced the paged `Items -> JournalEntry -> Transactions` graph load with one server-side
  summary projection containing only batch scalars, monetary totals, and status counts. The detail endpoint remains
  unchanged. `JournalBatchServiceTests`: 21 passed, 0 failed.
- **Budget revision register:** replaced the unpaged scenario-return-entry and revision-line graph load with a summary
  projection. Full revision lines and current-budget calculations remain on `GET revisions/{id}`. Added a direct
  register-summary regression test proving increases, reductions, net change, scenario/fiscal-year labels, and an
  intentionally empty detail-line collection. Focused regression: 1 passed, 0 failed.
- The broader `BudgetServiceHardeningTests` run compiled and passed 15 tests; one unrelated baseline fixture failed
  before reaching the changed code because its `JournalEntry` omitted the required `AccountingBookId`.
- Live SQL Server API timings after the corrections:
  - classifications: 29 rows in 347 ms;
  - journal-batch register: 452 ms;
  - budget-revision register: 96 ms.
- API was restarted from the corrected Debug build and `/health/live` returned HTTP 200 on PID 61316.

## Packages 2–6 additional corrections and evidence

- **Finance-owned source dimension readiness:** account existence, applicable account rules, active definitions and
  allowed values are now loaded once and evaluated in memory for all source lines. This removes the per-line query
  fan-out while retaining date-effective validation. `FinanceOwnedSourceDimensionReadinessProviderTests`: 4 passed.
- **Journal entry register:** replaced the transaction/account/dimension/attachment graph with a scalar header
  projection, correlated attachment count and batch scalars. `GetJournalEntriesAsync_ShouldFilterProcurementSource_AndExposeSourceLineage`
  passed; detail-by-id and detail-by-number retain their complete evidence graph.
- **Financial-statement layouts:** the layout register now projects latest/published version metadata instead of
  including every version. Audit-trail existence checks use `AnyAsync` rather than materializing layout, version,
  row, mapping and publication graphs. The new register-summary regression passed.
- **Accounting-book workflow navigation:** lifecycle, period, initialization and applicability-policy workflow
  entities now resolve to their canonical Finance settings/readiness pages. This allows existing notification and
  approval consumers to populate actionable URLs without redesigning the notification subsystem. New focused tests:
  2 passed.
- **Cashier till register:** open-session activity is now loaded in two batched operational-window queries and
  calculated for all register rows in memory. This replaces up to two SQL round trips per open session while keeping
  the detail endpoint's custody-entry evidence unchanged.
- **Authentication provider routing:** known local users no longer contact LDAP merely because their tenant supports
  directory identities. Existing LDAP users and unknown users eligible for LDAP auto-provisioning retain the LDAP
  path. This removes the observed local Admin sign-in timeout when the directory is slow or unavailable.
- **Period/readiness controls and approval queue:** focused backend suites passed 6 tests. Focused accounting-book,
  applicability, readiness and journal-detail frontend suites passed 47 tests across 4 files.
- **Consolidated backend verification:** 80 focused tests passed, 0 failed, covering classification authority,
  source-dimension readiness, journal batches and entry lineage, financial-layout summaries, book workflow links,
  activation readiness and approval-queue projection.
- **Cashier-till verification:** the updated live-register calculation test passed. The complete three-test class had
  2 passes and one unrelated existing failure in the legacy customer-payment credit-note write guard.
- **Build hygiene:** the complete API build finished with 0 errors; `git diff --check` found no whitespace errors.
- **Live host state:** `/health/live` is HTTP 200. After the first EF model initialization, the database readiness
  check completed in about 3 ms. Overall readiness is HTTP 503 only because the optional local file-virus scanner is
  absent. The API has intentionally been left running on `127.0.0.1:5012` for continued UI testing.

## Remaining bounded risks

- The Finance approval queue is capped at 100 rows but some accounting-book entity types still perform per-row
  authorization/display lookups. It is bounded today; a future queue projection should batch these lookups before
  raising the cap.
- Banking deposit and returned-cheque registers still share rich detail graphs because the returned-cheque screen
  consumes deposit allocation evidence. Split these into explicit summary and selectable-evidence contracts before
  high-volume rollout.
- This very large EF model takes longer than the frontend's 20-second request timeout to compile on the first
  database-backed request of a fresh API process. Warm requests are normal and SQL readiness is healthy. Address
  cold start separately with a generated compiled model or a deliberate pre-listen model warm-up; do not mistake it
  for a recurring Finance-query timeout.
- Local readiness reports unhealthy when ClamAV is not installed. That accurately reflects the configured security
  dependency but should be made explicit in the developer runbook so testers use liveness plus the database check
  rather than treating the Finance API as down.
- Two broader pre-existing test-fixture failures remain outside the changed paths: a budget fixture omits required
  `AccountingBookId`; financial-layout baseline fixtures disagree with the protected-standard seed/base-shape rules.

## Next safe action

Complete the consolidated build/focused suite and live SQL/API timing pass, then retain this ledger as the handoff for
the next stakeholder test session. The next dedicated performance package should split banking settlement register
summaries from detail/evidence payloads and add server-side paging.
