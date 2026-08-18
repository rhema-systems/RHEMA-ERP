# Controlled Opening-Balance Posting and Migration Sign-Off Preparation Foundation

## Scope

This batch adds a controlled backend foundation for tenant-scoped GL trial-balance opening balances.

It does not execute final production migration, build frontend import screens, implement AP/AR/fixed-asset subledger opening-document imports, or add reversal/correction workflows.

## Source Of Truth

Posted GL remains the accounting source of truth.

Opening balances now enter GL through `IFinancePostingEngine`, which creates the posted `JournalEntry`, `AccountTransaction`, and `FinancePostingEvent` rows. `Account.Balance`, `BankAccount.CurrentBalance`, and other stored balances remain read-side snapshots and are not directly mutated by opening-balance posting.

## Model

New tables:

- `OpeningBalanceBatches`
- `OpeningBalanceLines`

Batch records capture tenant, batch/reference, opening date, fiscal period, book classification, status, totals, idempotency key, workflow instance, journal entry, posting event, and failure metadata.

Line records capture tenant, account, debit/credit amount, transaction/functional currency, optional exchange-rate reference, optional bank/counterparty reference, segment string, source reference, and notes.

## Supported Opening-Balance Scope

Supported:

- Balanced GL trial-balance opening-balance batches by account.
- Single explicit book classification such as `IFRS`.
- Optional line-level bank account, counterparty, functional-currency, and segment references as migration metadata.
- Posting through `IFinancePostingEngine` after approval.

Rejected or deferred:

- `ALL_ACTIVE_BOOKS`.
- Unbalanced or single-sided imports.
- Foreign-currency GL opening lines. The current model stores functional debit/credit amounts only and therefore rejects a non-functional transaction currency before approval or posting rather than inventing an original foreign amount or FX snapshot.
- Silent suspense/equity plug creation.
- Direct mutation of `Account.Balance`.
- Direct mutation of `BankAccount.CurrentBalance`.
- Nonzero `BankAccount.OpeningBalance` creation outside the controlled opening-balance flow.
- AP/AR/fixed-asset subledger opening-document generation.

## Posting Rules

Opening-balance posting validates:

- current tenant context exists;
- batch and fiscal period belong to the current tenant;
- batch is approved before posting;
- batch is balanced;
- `ALL_ACTIVE_BOOKS` is not used;
- line accounts are same-tenant, active, not deleted, and direct-posting-enabled;
- duplicate posting is idempotent or safely returns the existing posting;
- posting engine enforces open fiscal period, tenant, account, currency, balance, and posting-event rules.

## Workflow And Permissions

Opening-balance batches are registered as Finance workflow entities under `OpeningBalanceBatch`.

Approval outcomes are handled by the existing Finance approval controller:

- completed approvals move `PendingApproval` batches to `Approved`;
- rejected workflows move non-posted batches to `Rejected`;
- posting is blocked for pending or rejected batches;
- high-risk submitter self-approval protection includes opening-balance batches.

Controller endpoints require authenticated Finance context. Production permission policy tightening remains part of final migration/sign-off hardening and tenant role assignment review.

## Audit Events

Added or used Finance audit events:

- `Finance.Migration.OpeningBalanceBatchCreated`
- `Finance.Migration.OpeningBalanceBatchValidated`
- `Finance.Workflow.Submitted`
- `Finance.Workflow.Approved`
- `Finance.Workflow.Rejected`
- `Finance.Workflow.PostingBlockedPendingApproval`
- `Finance.Workflow.PostingBlockedAfterRejection`
- `Finance.Migration.OpeningBalancePosted`
- `Finance.Migration.OpeningBalancePostingFailed`
- `Finance.Migration.DiagnosticRun`

## Diagnostics

Backend diagnostics expose:

- unposted opening-balance batches;
- failed opening-balance batches;
- posted batches missing journal/posting-event references;
- duplicate opening-balance posting events for one source batch.

Migration sign-off tooling now also exposes:

- tenant-scoped posting back-reference dry-run diagnostics;
- explicit posting back-reference repair mode for unambiguous source-document links;
- ambiguity/conflict refusal diagnostics;
- tenant-scoped bank snapshot variance diagnostics against posted GL movement;
- explicit bank snapshot rebuild mode for read-side `BankAccount.CurrentBalance` and `AvailableBalance` only;
- subledger opening-balance migration scope decisions for GL, AP, AR, and fixed assets.

The repair/rebuild tooling creates no journals and does not mutate posted GL.

## Migration Notes

Migration added:

- `20260709120000_AddOpeningBalancePostingFoundation`

Rollback drops the opening-balance batch and line tables. Posted GL created by already-posted opening-balance batches is not rolled back by the schema rollback and would need a controlled accounting reversal or environment reset.

## Tests Added

Focused tests are in `tests/ErpSystem.Api.Tests/Services/Finance/ControlledOpeningBalancePostingTests.cs`:

- `BalancedGlOpeningBalanceBatch_ShouldPostThroughFinancePostingEngine`
- `UnbalancedOpeningBalanceBatch_ShouldBeRejectedWithoutPosting`
- `ClosedPeriodOpeningBalancePosting_ShouldBeRejectedByPostingEngine`
- `CrossTenantAccount_ShouldBeRejected`
- `InactiveOrNonPostingAccount_ShouldBeRejected`
- `DuplicateOpeningBalancePosting_ShouldReturnExistingJournal`
- `OpeningBalancePosting_ShouldRequireWorkflowApproval_WhenWorkflowIsConfigured`
- `RejectedOpeningBalanceBatch_ShouldNotPost`
- `AllActiveBooksOpeningBalance_ShouldRemainRejected`
- `BankNonzeroOpeningBalance_ShouldRemainBlockedUnlessPostedThroughOpeningBalanceFlow`
- `OpeningBalancePosting_ShouldNotUseLegacySubledgerPostingService`
- `PostingBackReferenceRepairDryRun_ShouldDetectMissingLinksWithoutMutation`
- `PostingBackReferenceRepair_ShouldRepairOnlyUnambiguousLinks`
- `PostingBackReferenceRepair_ShouldRefuseAmbiguousMatches`
- `BankSnapshotDiagnosticAndRepair_ShouldUsePostedGlOnly`
- `SubledgerOpeningMigrationDecision_ShouldRejectGlOnlySubledgerOpenings`

## Limitations

- `FIN-LIM-0006` is resolved for controlled balanced GL opening-balance posting through the central posting engine.
- `FIN-LIM-0007` is resolved for tenant-safe back-reference diagnostic and repair tooling.
- `FIN-LIM-0008` is resolved for bank snapshot diagnostic and rebuild tooling.
- `FIN-LIM-0017` is narrowed by the opening-balance and migration tooling foundation but final production migration execution and accountant sign-off evidence remain open.
- `FIN-LIM-0048` implementation is resolved by canonical AP/AR opening invoices, controlled fixed-asset register-to-GL posting, and specialised supplier/customer advance and WHT/certificate opening workflows documented in `docs/subledger-opening-balance-cutover-foundation.md`. TDC representative-data rehearsal and accountant sign-off remain governed by `FIN-LIM-0017`.

## PR Definition Of Done

- Opening-balance posting path uses `IFinancePostingEngine`.
- No opening-balance code uses `ISubledgerPostingService` or direct GL posting.
- Balanced GL opening-balance batches create posted GL, posting event, and back-references.
- Pending/rejected/unbalanced/cross-tenant/closed-period cases fail clearly.
- Bank nonzero opening-balance creation remains blocked outside the controlled flow.
- Data, API, and test projects compile with opening-balance DbSets, service/controller, DI, and workflow wiring.
- Back-reference repair and bank snapshot rebuild tooling are dry-run capable, explicit, tenant-scoped, and audited.
- Limitations register is updated for resolved and remaining migration/sign-off work.
