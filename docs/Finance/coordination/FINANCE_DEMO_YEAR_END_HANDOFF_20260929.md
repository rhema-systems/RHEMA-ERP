# D05/D06 year-end V2 correction handoff — 2026-09-29

## Scope and base

- Branch: `codex/finance-demo-year-end-v2-20260929`
- Exact base: `acba892d48568231745736066a375456816b6fda`
- Accounting-book authority: `FINANCE_ACCOUNTING_BOOK_MODEL_V2_20260921.md`
- Owned implementation: year-end period-authority capture in `GeneralLedgerService`, the server-only year-end posting leaf in `FinancePostingEngine`, the shared-period reopen sequencing guard in `FiscalPeriodService`, focused year-end/period-reopen tests, and this handoff.
- No schema, model snapshot, migration, database, deployment, push, or remote change is part of this correction.

## Corrected V2 authority

Tenant `FiscalPeriod` is the sole posting-calendar and period-close authority. Year-end no longer reads or requires `AccountingBookPeriod`; zero legacy book-period rows is the normal supported state, and any retained legacy rows are irrelevant to the decision.

Every non-deleted tenant fiscal period must cover the fiscal year exactly once without gaps or overlaps and must be consistently closed and unlocked (`PeriodStatus == Closed`, `IsClosed`, not `IsOpen`, not `IsLocked`). The close must retain the real `FiscalPeriodService` lifecycle evidence:

- exactly one current closed `FinanceCloseCycle` for the period;
- exactly one active, non-superseded `FinanceCloseCertification` for that cycle;
- signed preparation and review/approval timestamps;
- distinct preparer and approver identities;
- reviewer and approver identity/time consistency;
- close-cycle preparation/close timestamps matching the certification; and
- fiscal-period closer matching the certification approver.

The year-end book cycle freezes a deterministic JSON snapshot of those exact tenant-period and maker-checker fields. The server-only posting leaf independently re-derives the snapshot inside the same serializable close/reopen transaction and requires exact equality. This prevents a forged leaf call or changed period/certification evidence from using the closed-period bypass.

The existing per-book year-end behavior remains unchanged: each exact book has its own immutable close cycle, nominal balances and currency are book-scoped, ordinary Primary replication is suppressed only for the bounded year-end leaf, and reopening reverses the exact original close journal with its original native amounts, coding, dimensions, and journal lineage. Locked or legacy globally closed fiscal years remain fail-closed.

A shared tenant period cannot be reopened while any non-reopened book-year close cycle remains for the same tenant and fiscal year. The real period-reopen impact validation fingerprints those deterministically ordered retained cycles and instructs Finance to reopen every affected book-year first. Only after those book reversals are complete may the shared period certificate be superseded. Cycles belonging to another tenant are excluded.

## Preserved accounting behavior

The shared internal `YearEndClosingPlan` still closes each account plus historical `FinanceDimensionSetId` and `SegmentString` bucket separately. Retained earnings is split across the same buckets, preserving dimension-level balance. The posting leaf independently re-derives the full plan. Current dimension defaults, renamed master data, and current active flags do not recode frozen historical balances.

## Focused regression intent

`FiscalYearCloseTests` now proves:

- BASE, Parallel, and Delta book isolation and exact functional currencies;
- zero `AccountingBookPeriod` rows can close successfully;
- deliberately inconsistent legacy book-period rows are ignored;
- open, missing-coverage, locked, inconsistent, missing-evidence, and self-approved tenant fiscal-period states fail closed;
- a governed tenant-period reopen supersedes cycle N/certificate N and the subsequent year-end capture uses only current reclose cycle N+1;
- a real tenant-period reopen request is blocked by same-tenant/year active book-year cycles, succeeds after they are reopened, and ignores another tenant's cycle;
- the captured close evidence is stable and a changed source period is rejected at the posting leaf;
- exact closing-plan, bound-cycle, source, currency, book, tenant, and reversal forgeries are rejected;
- close retry, reopen/reclose, original reversal lineage, frozen dimensions, no-activity close, unposted selected-book activity, and ordinary closed-book blocking remain intact.

## Verification state

- Static diff validation: PASS (`git diff --check`; line-ending conversion warnings only).
- Focused compilation/tests: PASS, 56/56 `FiscalYearCloseTests` + `AccountingPeriodClosePostingDateTests` using `TdcFastEfBuild=true`.
- No SQL/UAT/database certification is claimed.
- Independent coordinator review remains required before integration.

## Release boundaries

The existing year-end schema/migration is already integrated and is not changed here. This correction does not revive Primary replacement, effective Primary designation, or per-book period workflows. The tenant fiscal calendar remains shared across the perpetual Primary, explicit Delta layers, and atomically replicated Parallel representations.
