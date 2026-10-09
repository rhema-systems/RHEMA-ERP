# AR Invoice Final Approval Authority Mismatch

## Objective

Diagnose and remediate the application and SQL evidence checks that reject a Sales/property-originated AR invoice when a final Finance approver releases it, including the deployed `SOURCE_BOOK_AUTHORITY_POSTING_EVIDENCE_MISMATCH` follow-up failure.

## Scope and authorization

- Repository investigation, a narrow source fix, focused tests, and Finance coordination-ledger updates are authorized.
- The user authorized committing, pushing, merging the focused PR, and triggering the guarded GitHub Actions VPS deployment on 2026-10-09.
- Manual database authority edits and business-workflow mutation remain outside authorization.

## Repository state

- Worktree: `.worktrees/ar-invoice-producer-authority`
- Original implementation branch: `codex/ar-invoice-producer-authority`
- Publication checkpoint branch: `codex/ar-invoice-authority-publication`
- Trigger remediation branch: `codex/ar-invoice-authority-trigger-origin`
- Trigger remediation exact base: `41522940015e4288ff5ce381ca2e3ff48bc2369b` (`origin/master` on 2026-10-09).
- Prior merged fix/test commits contained by the base: `311c3469823` and `bd753c15e3b` (PR #381).
- Verified implementation/test commit: `4334e1b2faf` (`fix(finance): preserve AR producer origin on approval`).
- Ledger implementation checkpoint: `1511ead16be` (`docs(finance): record AR producer authority remediation`).
- PR #391 merged both commits to `master` as `f01aec28d49` on 2026-10-09.
- The primary checkout and the mobile POS worktree were not modified.

## Deployment and incident evidence

- Guarded deployment run `37951681381` completed successfully and the live public environment descriptor subsequently reported build `4152294`, application version `41522940-20261009-152900`, deployed at `2026-10-09T16:00:23.6072792+00:00`.
- Final approval then advanced past the earlier application exception and reached `SaveChanges`, where SQL Server trigger `TR_FinanceSourceBookAuthorities_Evidence` rejected the new authority binding with error 51006, `SOURCE_BOOK_AUTHORITY_POSTING_EVIDENCE_MISMATCH`.
- The new trace reaches `FinanceSourceBookAuthorityService.BindOriginalPostingAsync` line 165 from `InvoiceService.CompleteArInvoicePostingAsync`, confirming the posted event/journal and retained authority were created in the approval transaction before the database trigger rejected the update.

- Newly attached deployed trace still reports `SOURCE_BOOK_AUTHORITY_POSTING_MISMATCH: posted event/journal differs from frozen authority.` from `FinanceSourceBookAuthorityService.BindOriginalPostingAsync`, reached by final workflow approval through `FinanceApprovalsController.ApplyApprovedOutcomeAsync`.
- The live public environment descriptor reported build `2b8a6ff`, application version `2b8a6ff8-20261009-120809`, deployed at `2026-10-09T12:45:22.4498465+00:00`.
- Git ancestry checks confirmed deployed commit `2b8a6ff8644` contains both PR #381 commits `311c3469823` and `bd753c15e3b`. A stale deployment is therefore ruled out for this repeat incident.
- Original incident evidence remains invoice `INV-202610-0005`, amount GHS 520,000.00, approval `9fe166e3-ed5b-4caa-afed-8406c5487d79`, and actor `financial.controller`.
- Workflow completion and posting share a transaction, so the event/journal is rolled back when authority binding fails.

## Root cause

- PR #381 corrected authority freezing for direct Finance invoices (`FIN`) and producer-routed invoices (for example, Sales -> `SALES`), and added a regression for the direct Finance path.
- `BuildArInvoicePostingRequestAsync` still emitted `SourceModule = "AR"` without `OriginModuleCode`. The posting engine therefore canonicalized the posting event and journal to Finance origin `FIN`, even when the retained authority was correctly frozen to `SALES` for a Sales-order customer invoice.
- Authority binding also compared `journal.SourceModule` alone and ignored the journal's explicit `OriginModuleCode`. A journal whose accounting source remains `AR` but whose canonical producer origin is `SALES` was therefore rejected.
- The PR #381 regression used only a direct/manual AR invoice whose correct origin is `FIN`; it could not expose the producer-route mismatch.
- A focused baseline regression using a real Sales producer context and a correctly frozen `SALES` authority reproduced the deployed exception at `BindOriginalPostingAsync`.
- The application fix now validates the journal's explicit `OriginModuleCode`, but the persisted SQL evidence trigger still derived journal origin only from `JournalEntry.SourceModule`. It therefore mapped accounting source `AR` to `FIN` and rejected the correctly persisted explicit `SALES` origin.

## Correct compatibility boundary

- The source fix preserves `SourceModule = "AR"` as the accounting source and explicitly propagates the producer's canonical origin (`SALES`) into the posting request, event, and journal.
- Authority validation now resolves journal origin from both `SourceModule` and `OriginModuleCode`, matching the posting engine's governed identity model.
- This safely supports existing invoices whose retained authority was correctly frozen as `SALES`; no data migration is required for them.
- An older Sales-origin invoice whose authority was incorrectly frozen as `FIN` must still be rejected/resubmitted through the supported workflow so authority is re-frozen. Automatically adapting a posting to an incorrect retained origin would bypass the source-book control and is not a safe compatibility remedy.

## Changes

- `InvoiceService` now uses one producer-origin resolver for both authority freezing and posting requests.
- Normal and opening-balance AR posting requests now carry the resolved canonical `OriginModuleCode`.
- Source-book journal checks now honor `JournalEntry.OriginModuleCode` while retaining `SourceModule` as fallback for legacy Finance-origin journals.
- The Sales stock-invoice regression now freezes a real `SALES` authority, posts through the real Sales producer route, and asserts bound event/journal origin.
- The test fixture now supplies stable Inventory UOM evidence required by current commercial-quantity governance.
- Migration `20261009163000_AlignSourceBookAuthorityJournalOrigin` replaces only the evidence trigger definition. It resolves journal origin from the normalized explicit origin first and retains the legacy source-module mapping as fallback.
- The migration downgrade is guarded: it refuses to restore the legacy trigger while retained authorities rely on explicit journal origins that the old source-only mapping would reject.

## Verification

- Baseline focused Sales producer regression reproduced the exact incident for the non-tax case:
  - `SOURCE_BOOK_AUTHORITY_POSTING_MISMATCH` at `FinanceSourceBookAuthorityService.BindOriginalPostingAsync`.
- API-only Release compile against the repository's pinned .NET 9 SDK passed with 0 errors and 5 existing ImageSharp advisory warnings.
- Post-fix Sales producer regression passed both taxable and non-tax cases:
  - 2 passed, 0 failed, 0 skipped.
  - Both cases bind the frozen `SALES` authority to the canonical posting event/journal, replay idempotently, and preserve the delivery no-double-issue guarantee.
- Compatibility batch covering the direct/manual AR final-approval regression, `FinanceSourceBookAuthorityServiceTests`, and `FinanceSourceBookAuthorityCallerAdoptionTests` passed:
  - 28 passed, 0 failed, 0 skipped.
- Trigger-remediation Data Release build passed with 0 errors and 114 existing warnings.
- Focused trigger-remediation compatibility batch passed:
  - 32 passed, 0 failed, 0 skipped.
  - Coverage includes migration guards, source authority validation, caller adoption, final AR approval, and Sales invoice posting.
- Strengthened migration operation test passed:
  - 4 passed, 0 failed, 0 skipped.
  - It verifies one explicit-origin `Up` SQL operation and ordered guarded-rollback plus legacy-trigger `Down` operations.
- `git diff --check`: passed.
- Existing repository warnings include ImageSharp advisories and unrelated compiler/analyzer warnings.

## Migrations and application status

- Migration added: `20261009163000_AlignSourceBookAuthorityJournalOrigin`.
- Application status: compiled and tested locally; the migration has not yet been applied to the VPS database.
- Branch `codex/ar-invoice-producer-authority` was pushed and PR #391 was opened and merged.
- A guarded Windows VPS CI/CD dispatch was created from merge commit `f01aec28d49` as run `37943695807`; the publication checkpoint advances `master`, so the workflow must be re-dispatched from the final ledger merge commit to satisfy the deployment freshness gate.
- No manual VPS action or database mutation was performed.

## Remaining work

1. Commit and publish the focused trigger migration and test, merge its pull request, and dispatch the guarded Windows VPS CI/CD workflow from resulting current `master`.
2. Confirm the deployment applies `20261009163000_AlignSourceBookAuthorityJournalOrigin` and exposes the corresponding build.
3. Retry final approval. A correctly frozen `SALES` authority should bind to the `AR` journal carrying explicit origin `SALES`; an old incorrectly frozen `FIN` authority still requires supported reject/resubmit and re-freeze.

## Known limitations

- The attached application trace does not expose the retained authority's individual origin coordinate, so it cannot by itself distinguish a correctly frozen `SALES` authority from a legacy incorrect `FIN` authority.
- No production database mutation was performed to inspect or repair the specific invoice.
- SQL Server LocalDB is available on the workstation, but this focused cycle did not create a disposable full-schema database; trigger SQL was verified through compiled migration operations and the existing application-level relational regressions.
