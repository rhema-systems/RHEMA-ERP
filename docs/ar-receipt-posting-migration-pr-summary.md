# AR Receipt Posting Migration to IFinancePostingEngine

## Definition Of Done

- [x] Normal AR receipt posting uses `IFinancePostingEngine`.
- [x] No live normal AR receipt path calls legacy `ISubledgerPostingService.PostArPaymentAsync`.
- [x] Legacy AR receipt discount adjustment posting is disabled.
- [x] Posted customer receipts are immutable through update, allocation, reverse-allocation, clear, and bounce mutation paths.
- [x] Tenant/account/source-document validation is enforced below the controller layer.
- [x] Finance audit events are emitted for posted, failed, and duplicate/idempotent receipt posting attempts.
- [x] Tests were added and targeted posting regressions pass.

## Posting Design

AR receipt posting is now routed through `PaymentService.PostAsync(Guid id)`, which builds a `FinancePostingRequestDto` and delegates ledger creation to `IFinancePostingEngine`.

Normal receipt accounting treatment:

- Debit bank/cash GL account linked to the tenant bank account.
- Debit sales discount allowed account when existing allocation discount data is present.
- Credit AR control account for the cash receipt plus allowed discount settlement amount.
- Do not mutate `Account.Balance`; posted GL entries and `FinancePostingEvents` remain the source of truth.

`CustomerPayment.JournalEntryId` and `CustomerPayment.Status` are operational back-references. The posted ledger and `FinancePostingEvents` are authoritative, and `docs/finance-posting-backreference-diagnostics.md` now includes AR receipt relink diagnostics and accountant sign-off requirements.

## Lifecycle Rules

- `Pending`, `Approved`, or `Processed` customer receipts are postable when no existing posting exists.
- `PendingApproval` and other non-postable states are rejected unless a valid posted event already exists for idempotent relink.
- Receipts must have at least one same-tenant invoice allocation before posting.
- Allocated invoices must already be posted as `CustomerInvoice` source documents.
- Customer advances/overpayments are not supported in this batch.
- `Cancelled` and `Bounced` receipts cannot be posted.
- Posted receipts cannot be edited, reallocated, cleared, bounced, or reversed by direct mutation. Reversal/void must be implemented as a future posting-engine workflow.

## Affected Files

- `src/ErpSystem.Api/Services/Finance/AR/PaymentService.cs`
- `src/ErpSystem.Api/Controllers/Finance/ArControllersConsolidated.cs`
- `src/ErpSystem.Api/Services/Finance/GL/SubledgerPostingService.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IPaymentService.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ISubledgerPostingService.cs`
- `src/ErpSystem.Core/DTOs/Finance/PaymentDtos.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/ArReceiptPostingMigrationTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `docs/finance-posting-backreference-diagnostics.md`
- `docs/ar-receipt-posting-migration-pr-summary.md`

## API Impact

- Added `POST /api/ar/payments/{id}/post`.
- Added `CustomerPaymentDto.JournalEntryId`.
- Existing create behavior still auto-posts normal customer receipts after allocation by calling `PaymentService.PostAsync`.

## Migration Impact

No database migration was added. Existing `CustomerPayment.JournalEntryId`, `FinancePostingEvents`, allocations, bank account, and tenant fields support this batch.

## Tests

- `dotnet build tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-restore --disable-build-servers -m:1 -p:BaseOutputPath=.\.codex-build\ar-receipts\bin\`
  - Passed with 8 existing warnings.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-build --filter "FullyQualifiedName~ArReceiptPostingMigrationTests" -p:BaseOutputPath=.\.codex-build\ar-receipts\bin\`
  - 12 passed.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-build --filter "FullyQualifiedName~ArReceiptPostingMigrationTests|FullyQualifiedName~ArInvoicePostingMigrationTests|FullyQualifiedName~ApPaymentPostingMigrationTests|FullyQualifiedName~ApInvoicePostingMigrationTests|FullyQualifiedName~FinancePostingEngineTests|FullyQualifiedName~FinanceAuditFoundationTests|FullyQualifiedName~JournalEntryLifecycleBatch5Tests" -p:BaseOutputPath=.\.codex-build\ar-receipts\bin\`
  - 74 passed.

## Tenant Isolation Verification

The posting path rejects:

- customer references outside the current `TenantId`
- invoice allocations outside the current `TenantId`
- AR control accounts outside the current `TenantId`
- bank/cash accounts outside the current `TenantId`
- inactive posting accounts
- receipt posting in closed fiscal periods

## Accounting Impact

This completes the core AR loop through the central posting engine:

- AR invoice posting creates the receivable.
- AR receipt posting settles the receivable.

The implementation keeps the posted GL as the accounting source of truth. Settlement status fields remain operational read-side fields and must be reconciled against posted invoice and receipt events during go-live cleanup.

## Rollback Considerations

Code rollback would restore the legacy AR receipt posting path, which bypasses central posting idempotency and can create journals outside the controlled posting engine. If rollback is required, normal AR receipt posting should be suspended until the central posting path is restored.

## Known Limitations

- AR credit notes remain on the guarded legacy compatibility path and need a separate migration batch.
- AR receipt void/reversal posting is not implemented in this batch.
- Customer advances and overpayments are rejected until an advance/overpayment accounting design is implemented.
- Full Ghana VAT withholding/WHT handling is deferred to the tax batch because current AR receipt DTOs do not expose complete effective-dated withholding fields.
- Cash/bank reconciliation integration is intentionally not migrated in this batch; bank/cash GL is posted, but operational bank reconciliation behavior remains a later batch.
