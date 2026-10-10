---
integration_cycle: FIN-UAT-2026-10-10-A
integration_status: integrated
integration_decision: include
candidate_branch: codex/finance-uat-consolidated-20261010
candidate_head: 74bb13b6f
base_commit: ed768bc0f5cd1177c4a60af66fa35791555aae67
target_ref: origin/master
depends_on: merged PR #384
migration_status: one data-repair migration; locally applied in source UAT database; remote unapplied
verification_status: focused backend/frontend tests and changed-file lint passed
integration_commit: 74bb13b6f
pull_request: pending
---

# Finance UAT consolidation — 2026-10-10 A

## Objective and scope

Consolidate the outstanding, ledger-backed Finance changes from the current UAT thread without sweeping unrelated primary-checkout changes or historical branches into the pull request.

Included workstreams:

- Customer Register query translation and visible load-error handling after merged AR profile PR #384.
- Business Partner workflow entity-type resolution and legacy-alias consolidation.
- Fixed-asset capitalization transaction execution-strategy correction.
- AR receipt/detailed-ledger date clarity.
- Discoverable application of posted customer advances to open invoices without duplicate cash posting.

Explicitly excluded:

- Dirty primary-checkout files, untracked artifacts, historical Finance branches, and unrelated worktrees.
- The already-merged AR invoice authority fix from PR #381 and the already-merged AR profile implementation from PR #384.

## Workspace and Git state

- Repository: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP`
- Integration worktree: `.w\finance-uat-consolidated-20261010`
- Integration branch: `codex/finance-uat-consolidated-20261010`
- Exact base: `ed768bc0f5cd1177c4a60af66fa35791555aae67` (`origin/master` when created)
- Integration content head before this ledger commit: `74bb13b6f`
- Candidate source branches were clean when inventoried.

## Implementation record

- Integrated five post-PR-#384 commits from `codex/ar-profile-replacement-credit-authority`.
- Integrated eight commits from `codex/business-partner-workflow-fixed-asset-transaction`.
- All commits applied without conflicts.
- The repository candidate inventory script found three ready/include ledgers across the two source branches for cycle `FIN-UAT-2026-10-10-A`.

## Migration record

- `20261009143000_ConsolidateBusinessPartnerWorkflowEntityType` is included.
- It is a data-repair migration with no model change; no model snapshot update is required.
- No duplicate migration timestamp was found.
- It was previously applied only to local `RHEMAERP_BOOKV2_UAT_20260922` after a verified backup.
- It has not been applied to any remote database. This consolidation performed no database mutation.

## Verification evidence

- PASS: `git diff --check origin/master...HEAD`.
- PASS: frontend focused Vitest command covering advance selection, payment service serialization, receipt modes, and detailed-ledger dates — 5 files, 14 tests.
- PASS: changed-file ESLint over 13 changed TypeScript/TSX files.
- PASS: focused combined backend command covering Customer Register SQL translation, workflow entity resolution, Business Partner lifecycle governance, the exact fixed-asset execution-strategy regression, and customer-advance eligibility/application — 8 tests.
- PASS: clean project restore and compilation completed; repository baseline compiler warnings and known ImageSharp advisories remain.
- EXPECTED ENVIRONMENT FAILURE: `npm run type-check` reaches only `src/lib/phone-number.ts(7,8)` and fails because the shared local dependency tree cannot resolve `libphonenumber-js/max`; no changed file is implicated.
- DIAGNOSTIC ONLY: an initially over-broad backend filter ran the full fixed-asset foundation class and exposed 21 existing fixture-sensitive failures outside this change. The exact intended combined filter subsequently passed 8/8.

## Known failures and risks

- The frontend dependency-resolution problem prevents a clean full type-check in this local environment.
- Existing ImageSharp moderate/high vulnerability advisories remain outside this workstream.
- Deployment must apply the included Business Partner data-repair migration before validating new maker-checker evidence.

## Remaining work

- Complete pull-request review and merge.
- Deploy through the normal reviewed release process.
- Apply the included migration remotely only through deployment.
- Perform browser UAT for Customer Register loading, a newly submitted Business Partner, fixed-asset capitalization, date labels, and applying a posted customer advance to an open invoice.

## Authorization boundaries

- Authorized: consolidation, push, and pull-request creation.
- Not authorized: merge, deployment, remote database mutation, worktree removal, or branch deletion.

## Integration outcome

- Consolidation branch created and verified.
- Pull request: pending creation.
