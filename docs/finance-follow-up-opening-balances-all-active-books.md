# FIN-FOLLOW-OPENING-BALANCES: Migrate ALL_ACTIVE_BOOKS Opening-Balance Posting

## Problem Statement

Batch 5 disabled direct `ALL_ACTIVE_BOOKS` opening-balance posting because the old path mutates account balances and creates book-specific journals outside the central `IFinancePostingEngine`. This path must be migrated before production opening balances or migrated historical balances are posted.

## Affected Files And Modules

- `src/ErpSystem.Api/Services/Finance/GL/JournalEntryService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs`
- `src/ErpSystem.Core/DTOs/Finance/FinancePostingDtos.cs`
- `src/ErpSystem.Core/Entities/Finance/FinancePostingEvent.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFinancePostingEngine.cs`
- `src/ErpSystem.Api/Services/Finance/Settings/AccountingBookService.cs`
- data migration and opening-balance import scripts or diagnostics

## Accounting And Operational Impact

Opening balances establish the starting ledger position for go-live. If they are posted through legacy balance mutation or uncontrolled book-copying logic, the GL source-of-truth model can be compromised and later trial balance, balance sheet, audit trail, and migration sign-off reports may not reconcile.

## Proposed Implementation Approach

1. Design an opening-balance source document type such as `OpeningBalanceBatch`.
2. Validate tenant, fiscal period, accounting book, account ownership, account status, debit/credit balance, segment dimensions, and functional currency.
3. Extend `IFinancePostingEngine` or its request model only if needed to support controlled multi-book opening-balance batches.
4. Create one posting event per tenant, source document, accounting book, and posting action, with database-level idempotency.
5. Generate immutable posted journal entries and account transactions from the posting engine.
6. Do not mutate `Account.Balance`; rebuild any balance read model from posted GL entries.
7. Audit the import, validation, posting, duplicate attempts, adjustments, and accountant sign-off through `IFinanceAuditService`.
8. Produce accountant-readable reconciliation outputs before sign-off.

## Dependencies

- Posting engine foundation and idempotency
- Journal lifecycle and immutability
- Finance audit trail foundation
- Tenant isolation diagnostics and fixes
- Data migration diagnostics and cleanup phase
- Functional currency configuration
- Segment-based reporting rules

## Acceptance Criteria

- `ALL_ACTIVE_BOOKS` opening-balance posting no longer uses direct account balance mutation.
- Opening-balance journals are posted only through `IFinancePostingEngine`.
- Opening-balance posting is tenant-scoped and rejects cross-tenant accounts, books, periods, and source batches.
- Opening-balance batches are idempotent and cannot duplicate GL postings.
- Posted opening-balance journals are immutable and reversible only through controlled adjustment entries.
- Audit events exist for diagnostics, posting, duplicate attempts, adjustments, and accountant sign-off.
- Trial balance by tenant, book, and configured segment reconciles to the signed opening-balance file.

## Test Cases

- balanced opening-balance batch posts successfully
- unbalanced batch is rejected
- cross-tenant account is rejected
- closed or locked opening period is rejected
- inactive or non-postable account is rejected
- duplicate batch post returns existing posting or is safely rejected without duplicate GL
- multi-book batch creates controlled per-book posting events
- posted opening-balance journals are immutable
- adjustment/reversal path preserves audit trail
- accountant sign-off report reconciles to posted GL entries

## Priority

Critical.

## Go-Live Blocker

Yes, for data migration, opening-balance load, and accountant sign-off.
