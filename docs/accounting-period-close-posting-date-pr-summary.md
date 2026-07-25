# Accounting Period Close, Period Locking, And Posting-Date Enforcement

## Scope

This batch hardens fiscal period close/reopen controls and shared posting-date enforcement after the posting engine migration batches. It does not implement tax, FX, fixed assets, reporting UI, frontend changes, or data migration.

## Files Changed

- `src/ErpSystem.Api/Services/Finance/Cash/BankReconciliationService.cs`
- `src/ErpSystem.Api/Services/Finance/Fiscal/FiscalPeriodService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs`
- `src/ErpSystem.Api/Controllers/Finance/FiscalPeriodController.cs`
- `src/ErpSystem.Core/DTOs/Finance/PeriodCloseDtos.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/AccountingPeriodClosePostingDateTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FinancePostingEngineTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/BankReconciliationPostingMigrationTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `docs/sql/finance-accounting-period-close-diagnostics.sql`

## Implementation Notes

- Reconciliation adjustment creation now requires `IdempotencyKey`; retrying the same adjustment returns the existing posted adjustment instead of creating another cash/bank adjustment.
- `FinancePostingEngine` remains the central posting-date gate. It rejects periods that are not open, closed, or locked, and now records `Finance.PostingEvent.BlockedPeriodClosedLocked` when Finance audit is configured.
- Fiscal period close now only closes open periods and always runs critical close validation.
- Fiscal period reopen and unlock require explicit reasons. The controller no longer fabricates default reopen/unlock reasons.
- Period close validation now checks unposted approved journals, submitted journals awaiting approval, approved/unposted AP/AR/cash documents, unfinalized bank reconciliations, orphan posting events, posted journals/documents missing posting events, cross-tenant period references, and unbalanced posted journals.
- Period close/reopen/lock/unlock audit events use the existing `IFinanceAuditService` and central `AuditLogs` table.

## Tenant Isolation

All period service reads and writes are scoped by the current Finance `TenantId`. The close diagnostics explicitly check for cross-tenant journal and account-transaction references to the target fiscal period.

## Accounting Impact

Closed or locked accounting periods reject posting through the posting engine for manual journals, AP, AR, cash/bank, reconciliation adjustments, and future posting-engine callers. Period close now blocks known unresolved accounting conditions before a period is marked closed.

## Migrations

No database schema migration was added. Changes are service/controller logic, audit event constants, tests, and SQL diagnostics.

## Rollback Considerations

Rollback is code-only. If rolled back, reconciliation adjustments may again be vulnerable to duplicate adjustment creation when clients retry without an idempotency key, and period close validation will return to the narrower pre-batch checks.

## Known Limitations

- Existing `SkipValidation` remains on `PeriodCloseRequestDto` for API compatibility, but critical close validation is no longer skipped by this service.
- Segment-level close is not implemented; close remains tenant-level.
- Soft-close/restricted-period behavior is not modeled beyond the existing open/closed/locked flags.
- The SQL diagnostic file assumes default EF table names and may need table-name-only adaptation in deployments with custom mappings.

## Definition Of Done

- Backend build passes.
- Finance go-live regression slice passes.
- Reconciliation adjustment idempotency is required and tested.
- Closed/locked period posting rejection is audited at the posting-engine layer.
- Period close/reopen service tests cover tenant scope, validation failure, reason-required reopen, orphan posting events, and missing posting events.
- Tenant-safe SQL close diagnostics are documented.
