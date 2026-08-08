# PR Summary: Fixed Asset Capitalization Reversal and Correction

## What this delivers

- Resolves Finance limitation `FIN-LIM-0030`.
- Implements TDC `FR-GL-008` and `FR-GL-010` for fixed-asset capitalization corrections.
- Adds maker-checker request, review, reject, and post stages.
- Posts the compensating journal through `IFinancePostingEngine` and retains original/reversal lineage.
- Blocks correction after downstream asset accounting.
- Synchronizes register and accounting-book values with an immutable negative asset transaction.
- Reuses the AP invoice reversal journal for AP-origin asset cost instead of posting a duplicate asset journal.
- Adds permission-filtered operator actions and correction history to the fixed-asset workspace.
- Adds audit events, persistence, EF migration, and focused requirement-tagged tests.

## Important integration behavior

- Direct Finance capitalization is corrected from Fixed Assets.
- AP-origin capitalization is corrected by voiding/reversing the AP invoice.
- Other source modules must retain ownership of their business reversal and call the canonical Finance contract.
- A corrected re-capitalization receives a new posting cycle identity; the original idempotency key remains immutable.

## Migration scope

`AddFixedAssetCapitalizationReversalControls` adds only capitalization-correction tables, columns, indexes, and foreign keys. The branch has been rebased onto merged PR #37, and its cross-currency precision changes remain owned by the earlier migrations rather than being replayed here. The migration follows the recent hand-scoped Finance convention: explicit migration attributes plus the authoritative shared model snapshot, without another generated full-model designer file.

## Verification

- API/test projects built in Release with zero errors before the PR #37 rebase.
- A post-rebase Release build of the changed API surface (using the already-built project references) succeeds with zero errors; its 24 warnings are pre-existing repository warnings.
- Four focused `FinanceGoLive-FixedAssetCapitalizationReversal` tests pass.
- The combined capitalization suites pass 17/17.
- Targeted frontend ESLint passes.
- Post-rebase frontend lint passes and `git diff --check` is clean.
- A post-rebase clean Data build was stopped when the repository's full EF migration compilation reached approximately 20 GB working memory; no compiler error was reported. CI should run the normal clean build on the team runner.

## Reviewer focus

- Maker-checker independence and permission boundaries.
- Reverse-order lifecycle blocking.
- Original/reversal journal and posting-event linkage.
- AP invoice void atomicity and single-journal ownership.
- Tracker rehydration around posting-engine concurrency recovery.
- Migration isolation after the PR #37 rebase.
