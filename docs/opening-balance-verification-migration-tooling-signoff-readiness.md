# Opening-Balance Verification, Migration Tooling, and Sign-Off Readiness Hardening

## Scope

This pass verifies the controlled opening-balance foundation end to end and adds backend migration sign-off tooling.

It does not execute production migration, build frontend migration screens, implement AP/AR reversal redesign, implement cash/bank reversal redesign, or create final go-live scenario evidence.

The follow-on final sign-off evidence pack is documented in `docs/final-migration-signoff-scenario-pack.md`.

## Build Verification

The opening-balance implementation now compiles through:

- `ApplicationDbContext` with `OpeningBalanceBatches` and `OpeningBalanceLines` DbSets.
- `20260709120000_AddOpeningBalancePostingFoundation`.
- `OpeningBalanceService`.
- `OpeningBalancesController`.
- `IOpeningBalanceService` DI registration.
- `OpeningBalanceBatch` workflow entity handling in `FinanceApprovalsController` and `SimpleWorkflowService`.
- API and test project references.

Analyzer execution is disabled in verification commands because the solution has a large existing analyzer/warning surface that caused earlier timeout behavior. With analyzers disabled, Data, API, and test builds complete and expose real compile errors.

## Opening-Balance Verification

Confirmed behavior:

- Balanced GL trial-balance opening batches post through `IFinancePostingEngine`.
- Unbalanced batches fail validation and do not post.
- `ALL_ACTIVE_BOOKS` remains rejected.
- Direct `Account.Balance` mutation is not used.
- Direct `BankAccount.CurrentBalance` mutation is not used by opening-balance posting.
- Nonzero bank account opening balance remains blocked outside the controlled opening-balance flow.
- Duplicate posting returns the existing posted journal/posting event.
- Closed/locked periods are rejected through the posting engine.
- Pending/rejected workflow batches cannot post.
- Approved batches post only through the posting engine.
- Journal and posting-event back-references are written to posted opening-balance batches.

## Back-Reference Repair Tooling

`IMigrationSignOffService` provides tenant-safe posting back-reference diagnostics and explicit repair mode.

Supported source documents:

- AP invoices.
- AP payments.
- AR invoices.
- AR receipts/customer payments.
- AR compatibility customer credit notes.
- Sales credit notes.
- Cash/bank receipts, payments, and transfers.
- Fixed asset direct capitalization records.
- Fixed asset depreciation runs.
- Fixed asset valuation/revaluation/impairment records.
- Fixed asset disposals.
- Subledger adjustment journals.
- Opening-balance batches.

Rules:

- Diagnostic mode is dry-run and does not mutate data.
- Repair mode is explicit.
- Repair is tenant-scoped.
- Repair creates no journals.
- Repair mutates no posted GL, account transactions, journal entries, or posting events.
- Repair only fills missing source-document `JournalEntryId` and, where supported by the model, `PostingEventId`.
- Existing conflicting non-null references are not overwritten.
- Ambiguous matches are refused.
- Missing/cross-tenant journal references are refused.
- Finance audit events are emitted for diagnostics and applied repairs.

## Bank Snapshot Rebuild Tooling

`IMigrationSignOffService` provides tenant-safe bank snapshot diagnostics and explicit rebuild mode.

Rules:

- Diagnostic mode compares stored bank read-side balances to posted GL movement on the linked same-tenant bank GL account.
- Rebuild mode is explicit.
- Rebuild creates no journals.
- Rebuild mutates no posted GL, account transactions, journal entries, or posting events.
- Rebuild updates only `BankAccount.CurrentBalance` and `BankAccount.AvailableBalance`.
- `BankAccount.OpeningBalance` is reported as stored snapshot context and is not used as the accounting source of truth.
- Bank accounts with missing or cross-tenant GL accounts are refused.
- Finance audit events are emitted for diagnostics and applied rebuilds.

## SQL Diagnostics

SQL Server diagnostics are in `docs/sql/opening-balance-migration-signoff-diagnostics.sql`.

The diagnostics cover:

- unposted or failed opening-balance batches;
- posted opening-balance batches missing journal/posting-event references;
- duplicate posted opening-balance posting events;
- opening-balance lines referencing missing or cross-tenant accounts;
- bank stored snapshot variance versus posted GL movement;
- posted AP/AR invoices with missing source journal back-references;
- posted opening-balance GL lines without source batch references;
- `FIN-LIM-0048` subledger opening-balance scope warning.

## Subledger Opening Migration Decision

GL trial-balance opening balances are supported through controlled opening-balance batches. The same workspace now exposes consolidated subledger readiness and can prepare controlled GL evidence from selected fixed-asset opening register records.

GL-only opening balances are not sufficient for subledger sign-off where open source balances exist:

- AP opening invoices must be loaded through the existing canonical AP opening-invoice path if production cutover needs AP aging.
- AR opening invoices must be loaded through the existing canonical AR opening-invoice path if production cutover needs AR aging.
- Fixed asset opening registers must be imported as opening book values and posted through the generated, maker-checker-controlled fixed-asset opening batch if production cutover needs asset register reporting.
- Unapplied supplier/customer advances must be prepared through **Advances & WHT** so they remain available as individually allocatable advance lots without replaying historical cash.
- Unremitted AP WHT and outstanding AR WHT certificates must be prepared through the same workspace so statutory evidence survives cutover and reconciles to the tax control accounts.

Generic GL lines still cannot masquerade as subledger evidence. TDC confirmed the specialised cutover shape, and `FIN-LIM-0048` implementation now covers it; representative-data rehearsal and accountant sign-off remain under `FIN-LIM-0017`.

## Final Migration/Sign-Off Checklist

Pre-migration configuration:

- Tenant functional currency configured and locked after accounting activity.
- Fiscal years and periods created.
- Opening period is open/unlocked.
- Chart of accounts active, tenant-valid, and direct-posting rules reviewed.
- Bank accounts linked to same-tenant GL accounts.
- AP/AR control accounts configured.
- Tax accounts configured and tenant-valid.
- Fixed asset category mappings configured.
- FX accounts and approved rates configured where foreign-currency balances exist.
- Workflow requirements seeded for opening-balance and high-risk Finance actions.

Opening-balance posting:

- Import balanced GL trial balance by explicit book.
- Reject `ALL_ACTIVE_BOOKS`.
- Validate totals and account tenant/direct-posting status.
- Submit for approval where workflow is configured.
- Post through `IFinancePostingEngine`.
- Verify journal and posting-event references on every posted opening-balance batch.

AP/AR settlement read model:

- Load posted AP/AR source documents where open balances exist.
- Rebuild AP settlement read model.
- Rebuild AR settlement read model.
- Compare operational paid/credited snapshots to read-model balances.
- Resolve or accept diagnostics before sign-off.

Fixed asset register:

- Load fixed asset opening/import records only through supported source-document/import flow.
- Reconcile fixed asset register cost, accumulated depreciation, impairment, and NBV to posted GL.
- Reject GL-only fixed asset opening balances when asset register reporting is required.

Bank snapshot diagnostics:

- Run bank snapshot diagnostic mode.
- Review variance report.
- Rebuild read-side snapshots only after accountant approval.
- Verify no posted GL was mutated.

Posting back-reference diagnostics:

- Run back-reference diagnostic mode.
- Review missing, ambiguous, conflicting, and invalid reference items.
- Repair only unambiguous gaps.
- Refuse ambiguous matches for manual review.
- Verify no posted GL was mutated.

Core reconciliations:

- Trial balance balances by tenant/book/period.
- AP settlement read model ties to AP control GL.
- AR settlement read model ties to AR control GL.
- Cash/bank ledger ties to posted GL and bank read-side snapshot variance is explained.
- Tax reports reconcile snapshots to posted GL tax accounts.
- Fixed asset reports reconcile subledger snapshots to posted GL.
- Export packs generated from backend report/export services.

Evidence:

- Store build/test command output.
- Store opening-balance batch validation/posting output.
- Store AP/AR rebuild output and reconciliation variances.
- Store bank snapshot diagnostic/rebuild output.
- Store back-reference diagnostic/repair output.
- Store tax, fixed asset, trial balance, and control reconciliation exports.
- Store limitation acceptance decisions.
- Store accountant sign-off notes and go/no-go decision.

Rollback/reset for dev/UAT:

- Schema rollback only removes opening-balance batch/line tables.
- Posted opening-balance journals must be reversed through controlled accounting or the environment reset.
- Repair/rebuild tooling does not create journals and can be rerun safely in diagnostic mode.
- Bank snapshot rebuild is read-side only and can be repeated after posted GL changes.

## Tests

Focused tests are in `ControlledOpeningBalancePostingTests`:

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
- `FinalSignOffCleanFixture_ShouldPassWithAcceptedLimitations`
- `FinalSignOffUnbalancedTrialBalance_ShouldFail`
- `FinalSignOffMissingOpeningBalanceReferences_ShouldFail`
- `FinalSignOffBankSnapshotVariance_ShouldFailUnlessAccepted`
- `FinalSignOffFinLim0048_ShouldBlockWhenSubledgerOpeningsAreRequired`
- `FinalSignOffUnacceptedGoLiveLimitation_ShouldFail`
- `FinalSignOffBackReferenceCandidate_ShouldAppearInDiagnostics`
- `FinalSignOffActiveCurrentCovidLevy_ShouldFail`
- `FinalSignOff_ShouldIgnoreCrossTenantData`
- `FinalSignOffReview_ShouldEmitAuditEvent`

## Limitations Register

- `FIN-LIM-0006`: resolved and build-verified for balanced GL opening-balance posting.
- `FIN-LIM-0007`: resolved for tenant-safe back-reference diagnostics and repair.
- `FIN-LIM-0008`: resolved for bank snapshot diagnostics and rebuild.
- `FIN-LIM-0017`: materially narrowed but still open until production migration execution and accountant sign-off evidence are completed.
- `FIN-LIM-0048`: implementation resolved for canonical AP/AR opening invoices, controlled fixed-asset register-to-GL evidence, unapplied advances, and WHT/certificate opening records. Representative-data rehearsal remains a `FIN-LIM-0017` acceptance activity.
