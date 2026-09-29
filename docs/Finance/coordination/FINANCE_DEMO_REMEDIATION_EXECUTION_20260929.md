# Finance demo remediation execution ledger — 29 September 2026

## Authority and baseline

- Objective: implement the demo defect-register plan, review accounting changes independently, and consolidate verified Finance changes on latest master for stakeholder UAT.
- Integration base: `52ce3a71595ba18d26d40658c1a368743c44a853` (`origin/master` fetched 29 September). Earlier consolidated PR #275 is merged.
- Integration branch: `codex/finance-demo-remediation-20260929`.
- Integration worktree: `C:/Users/Akwas/Documents/DEV_WORK/RHEMA-ERP-finance-demo-remediation-20260929`.
- Main dirty checkout remains untouched except ignored read-only inspection helper under outputs.
- Migrations to the named UAT database have prior explicit user approval, subject to read-only preflight and safe rehearsal. No accounting-data changes, permission waivers, production deployment, or external filing are inferred from this approval.

## Decisions received

1. D07: research Ghana primary tax sources and use findings as implementation direction. Preserve ambiguity as an explicit gate rather than silently assume tax policy.
2. D08: lease instalments create governed AP open items, then settle through AP.
3. D09: deposits and returned cheques support foreign currency using approved exchange-rate evidence.

## Workstreams

| Scope | Worker/model | Worktree suffix | Status |
| --- | --- | --- | --- |
| D01/D02/D09 deposits, returned cheques, FX | demo_cash_banking / Sol High | finance-demo-cash-20260929 | implementation dispatched |
| D03/D04 subledger adjustment, capitalization | demo_adjustment_assets / Sol High | finance-demo-adjustment-assets-20260929 | implementation dispatched |
| D05/D06 year-end exact-book isolation | demo_year_end / Astra Ultra | finance-demo-year-end-20260929 | implementation dispatched |
| D07 Ghana WHT and D08 lease AP | coordinator | finance-demo-remediation-20260929 | research / design |
| configuration preflight and release evidence | coordinator | integration | pending |
| independent accounting/security/migration review | Astra Ultra per user guide | to be assigned after first handoff | pending |

Worker branches use the same exact integration base and own disjoint Finance services. Shared contracts/migration snapshots require coordination. Only one broad compiler process at a time on this workstation.

## Progress method

Progress is implementation-and-verification progress, not a production-readiness certification. Baseline/scope 10%, implementation 45%, independent review and automated verification 25%, permitted configuration/runtime checks and handoff 20%. Current: approximately 50% (banking, capitalization, WHT and year-end focused tests passed; year-end policy correction, final WHT review, lease/source-book callers, combined regression and UAT remain open). No defect is marked closed from a narrative alone.

## Ghana research checkpoint

- GRA WHT practice note DT/2016/001, sections 4.6/4.9: threshold crossing includes cumulative qualifying amounts, not only the crossing payment; tax base excludes VAT/CST. This corrects the earlier current-payment-only description.
- GRA 2026 VAT guidance: 15% VAT, 2.5% NHIL, 2.5% GETFund on the same base, effective 1 January 2026. Historical configurations must not be rewritten.
- Sources: https://gra.gov.gh/wp-content/uploads/2020/09/Practice-Note-on-Withholding-of-Tax.pdf ; https://gra.gov.gh/wp-content/uploads/2026/01/VAT-Guidelines-for-VAT-ACT-1151.pdf ; https://gra.gov.gh/domestic-tax/tax-types/withholding-tax/ .
- Remaining D07 checks: current statutory amendments, contract aggregation, known contract value vs partial settlement, discounts and tax adjustments, frozen evidence for certificates/reversal and prior-history sufficiency.

## Verification and mutations

- Integration checkout clean at exact base; no new build/test pass claimed.
- No database migration/data mutation, production deployment, push or new PR in this execution phase yet.
- Prior UAT screen checks are not end-to-end posting evidence and are not reused as proof here.

## Next safe action

Complete scoped implementations, record exact commits and tests, independent accounting review, integrate approved units in dependency order, then run combined regression and permitted UAT preflight.

## Checkpoint — reviewed corrections and compiler recovery

- Year-end committed in worker tree: `70eb22bbd94cf89efa7f0ecc567f3625d7261dc1`, `768f03d654d9b26ed744bbfc140bbb7d717977bf`, `fe30feb3e5cf558745ba50cede6c06f580dccb47`. Adds exact-book close/reopen cycles, guarded internal posting authority and dimension-preserving close-plan validation. Independent root review requested dimension preservation and exact payload checks; worker delivered these. Runtime tests, SQL rehearsal and coordinator-owned snapshot delta remain pending. Not yet integrated.
- Retained-earnings coding policy has been sent to user for confirmation: preserve each historical coding bucket (recommended) versus one uncoded balance. No live close has been performed.
- Independent Astra review of capitalization found source-dimension selection, generated parallel-replica ambiguity, maker/checker and asset register book-value issues. Sol worker is correcting these, with narrowly authorized Finance approval-controller change. No general FixedAssetService rewrite authorized.
- First D03 compile completed with no production-code errors;18 tests ran,12 passed and6 stopped on unapproved business-partner fixtures. This is not an accepted final result; fixture updates and final source rerun remain pending.
- Root review of banking found duplicate FX-policy selection and returned-cheque AR carrying/control-account restoration problems. Worker corrected them; USD2400 original AR21600, receipt24000, return26400 regression has been added, not yet run.
- D07 review corrections: public preview now requires invoice settlement evidence; historical unfrozen bases fail closed; contract qualification uses other governed invoices and frozen source FX; non-GHS functional tenants cannot treat Ghana thresholds as local currency. Reviewer withdrew editable-FX finding after confirming the field is read-only and ID/value come from one approved snapshot. Remaining source limitations are documented in `GHANA_WHT_DEMO_CALCULATION_BASIS_20260929.md`.
- D08 dispatched to Sol High `demo_lease_ap`, exact base52ce3a7, isolated branch/worktree `finance-demo-lease-20260929`. Owns canonical lease-to-AP adapter and additive migration003; no SQL writes or automatic approvals. Root retains combined model snapshot ownership.
- Root WHT Core/Data compilation completed. Original test session became unavailable after a user progress-message interruption; no compiler remained and no test assembly was produced, so no API/test pass inferred. Incremental run resumed with persistent build log and TRX output under ignored `outputs/finance-demo`.
- Remaining hard-coded IFRS Finance callers are confirmed demo dependencies. Reversals must bind retained source journal/event, not current default; several source-event queries also incorrectly include generated parallel replicas. A global IFRS-to-BASE alias is explicitly rejected.

## Checkpoint — implementation and read-only UAT preflight

- Integration checkout now contains coordinator-owned uncommitted D07 changes, an additive allocation-evidence migration and its snapshot entry. Earlier clean-checkout verification above describes the starting state only.
- AP payment frontend focused tests: **19 passed**. Focused ESLint: **passed** after an existing non-null assertion was corrected. Backend tests remain unverified.
- Year-end Core/Data/API build succeeded; test-project compilation exposed an assertion mismatch and a newly added migration missed by the initial build evaluation. Worker corrected the assertion and will rerun incrementally. No year-end test pass yet.
- Single compiler queue: adjustment/assets now building, then cash/banking, coordinator WHT, then year-end incremental rerun.
- Independent Sol High review of D07 returned CHANGES_REQUIRED: immutable historical basis, complete/frozen contract scope, public preview trust boundary, and frontend FX-ID/value consistency. Coordinator is correcting these before acceptance.
- Read-only integrated-auth SQL preflight reached `RHEMA-AKWASI\EXPRESS22` / `RHEMAERP_BOOKV2_UAT_20260922`; no connection secrets were printed.
- Books: primary BASE/GHS, parallel USD_PARALLEL/USD and IFRS_ADJUSTMENTS. Accounting-book periods: 0. Bank accounts: 0. Lease contracts: 0. AP invoices/payments: 0.
- Existing configuration: 4 currencies, 3 rate records, 6 dimension definitions, 22 dimension values, 0 account rules, 7 financial-statement layouts. Preserve existing values/layouts rather than reseeding from the older register's counts.
- VAT/NHIL/GETFund lack GL mappings and are sales-only. Their historical tax evidence must be preserved during governed setup.
- Awaiting user reply authorizing normal-workflow UAT setup, labelled synthetic FX rates, and disposable migration/rollback rehearsals. No such data/configuration changes have occurred.

## Checkpoint — continued execution after user progress request

- Root WHT fast-mode build/test exited0:16/16 WHT lifecycle tests passed. TRX `outputs/finance-demo/test-results/wht/Akwas_RHEMA-AKWASI_2026-09-29_19_44_49.trx`. Full historical EF target-model/snapshot compilation was excluded; this is not a migration certification.
- Adjacent AP/payment/tax/certificate test run:128 passed,2 failed. Failures were stale fixtures: missing frozen historical WHT base and inconsistent invoice subtotal/gross/rate. Fixtures corrected; combined incremental rerun started with persistent `wht-final-build.log` and TRX. No new pass inferred yet.
- Banking worker reports16/16 focused tests passed in fast mode. Compiler released to root. Commit and independent final diff review still pending.
- D07 additional primary-source review: GRA-hosted Act915 section21 requires statutory GHS conversion at the relevant BoG inter-bank rate/date. Approved commercial FX and frozen accounting book values do not prove that authority. Earlier foreign-WHT support assumption is superseded: selected/unselected foreign invoice scope and foreign historical payment scope fail closed pending governed statutory evidence. GHS-only demo calculation remains supported; foreign WHT is an explicit open release gap. Four new guard cases await execution.
- D08 Sol High has resumed canonical lease-to-AP implementation. New AP drafts never mark instalments paid. Legacy active leases cannot silently adopt today's interest account as historical authority; immutable source evidence or governed remediation is required. Tax/WHT review blocks posting. Migration003 remains unapplied.
- D03/D04 corrected source/dimension/parallel-register tests are present in worker tree; no new verification result yet. No direct general FixedAssetService changes authorized.
- Read-only UAT refinement: only tenant DEFAULT, ID00000000-0000-0000-0000-000000000001. September2026 is not open (IsOpen0,IsClosed0); July/August are open. Book periods remain0. Normal-workflow period/book-period configuration is therefore a real demo prerequisite.
- No worker commits integrated, no new migrations applied, no UAT accounting data changed, no new deployment/push/PR.

## Checkpoint — independent WHT boundary approval and caller follow-up

- Combined WHT/AP-payment/tax/certificate run completed:149 passed,1 failed of150. TRX `outputs/finance-demo/test-results/wht-final/Akwas_RHEMA-AKWASI_2026-09-29_20_06_15.trx`. Remaining fixture expected WHT at exactly the threshold under the retired rule; revised case now tests crossing149 with prior50/current100,15 WHT including5 catch-up, and idempotent replay. Latest modifications not yet rerun.
- Independent Sol High D07 review first identified two bypasses: foreign payment against GHS invoice, and new certificate/remittance actions on foreign historical payments. Coordinator added create/post/synchronize currency guards, statutory issue/reissue and create/submit/pay remittance guards, and corresponding tests. Reviewer returned APPROVED for this bounded safety change; read/issued-version replay/cancellation remain available. This does not certify foreign WHT support.
- Focused frontend tests now20/20; focused ESLint passed with no output. AP payment preview, auto-allocation and submission explain statutory FX evidence rather than accepting commercial FX as authority.
- Banking first commit `c9521ec00d0b24c262c6a45b1a96af65d042f8fe`:16/16 focused tests. Final review requested explicit approved-rate status and rejection of ambiguous/invalid account FX policy; correction plus tests authored, rerun queued after assets. No integration yet.
- Assets owns compiler next/current. Queue after it: banking correction; root WHT final; year-end focused suite. No simultaneous broad compilers.
- AR/cash exact-book follow-up design reviewed read-only. Do not enable AccountingEvents/ProducerIntents or reuse C5 applicability as a drop-in: its source catalog/readiness scope differs. Proposed generic source-bound immutable authority will freeze tenant/source/book/currency before approval and retain exact original event/journal, then AR/cash callers inherit or reverse that identity. Implementation only after banking correction verification, in a new isolated exact-base worktree; shared helper/schema must be reviewed before caller edits. Root retains snapshot and AP ownership.
- Lease adapter/API/UI and migration003 are authored; worker is adding real-service idempotency/rollback tests. Not yet compiled, integrated or deployed.

## Checkpoint — reviewed handoffs accepted

- Root independently reviewed and APPROVED local integration of banking commits `c9521ec00d0b24c262c6a45b1a96af65d042f8fe` then `7544233900e477756b76517394ef1c669195c635`. Latest focused fast-mode banking run:20/20 passed. Exact rate-policy correction rejects duplicate policies, invalid enums and non-approved rate statuses. No SQL or UAT certification inferred.
- Root independently reviewed and APPROVED local integration of D03/D04 commit `ea1c345cdeecea63172a3361c71e54c8496ce97b`. Focused results:CustomerBalanceAdjustmentPostingTests20/20, CapitalProjectPostingTests7/7, FinanceApprovalQueueProjectionTests5/5. New capitalization test file is tracked and explicitly included by the test project. Assets are reconciled to exact posted primary/parallel representations; unsupported newly initialized Delta values are removed within the capitalization transaction. Real FixedAssetService boundary and relational rollback remain runtime gates.
- No commits integrated yet: waiting for the current root WHT compiler session42831 to finish. Combined WHT/AP-payment/certificate/tax regression writes persistent log `wht-reviewed-build.log` and TRX under `test-results/wht-reviewed`.
- Cash worker continues Sol High in new clean exact-base worktree `C:/Users/Akwas/Documents/DEV_WORK/RHEMA-ERP-finance-demo-ar-cash-authority-20260929`, branch `codex/finance-demo-ar-cash-authority-20260929`. First unit is immutable source-book authority helper/entity/migration004/tests only. Caller edits wait for helper review; no compilation until coordinator grants the slot.
- Assets worker continues Sol High with a bounded read-only inventory of remaining Finance-owned fixed-asset, receipt, valuation and supplier-debit-note source-book callers. No implementation or other-owner changes authorized by that inventory task.
- Year-end schema/migration handoff is reconciled; combined snapshot and queued focused runtime tests remain pending. No database changes, pushes, PRs or deployment in this checkpoint.

## Checkpoint — WHT final focused regression passed

- Root combined WHT/AP-payment/certificate/Ghana-tax test run exited0:154 passed,0 failed,0 skipped. TRX `outputs/finance-demo/test-results/wht-reviewed/Akwas_RHEMA-AKWASI_2026-09-29_20_24_15.trx`. Independent final WHT math/posting review assigned to Sol High assets worker after its read-only caller inventory. Migration002 downgrade now refuses to drop populated frozen basis evidence; SQL execution remains unverified.
- Root compiler transferred to year-end isolated tree: session42532, filters FiscalYearCloseTests and FiscalYearDeletionGuardTests, durable `year-end-reviewed-build.log` and TRX. No parallel compiler granted to workers.
- Lease real-service tests authored for idempotent readable pending-tax draft, ordering rollback, submit tax/WHT gates, immutable source, duplicate standalone period rejection, legacy-authority remediation, delete/recreate, paid void guard and tenant isolation. Approval/post gate and AP-post accounting-not-paid test are being added. No passing result claimed yet.

## Checkpoint — first ordered local integration

- Clean authorized cherry-picks completed without conflict: banking `c9521ec00` to `d70914e7bb70f46cc7385d2dfc75ca6832d54247`; banking policy correction `754423390` to `2e23e99b6338788256a459aeba7ea78c2aa98f74`; D03/D04 `ea1c345cd` to `6dd4524929187e83c442381977df9382817679b5` (current integration HEAD).
- Coordinator's uncommitted WHT changes were preserved. Combined regression on the merged tree is still pending; individual worker passes are not substituted for it.
- Year-end model snapshot patch prepared from exact entity/context/migration, not yet applied. Year-end runtime tests and final acceptance precede local integration.
- No remote push/new PR, application restart/deployment or database mutation has occurred.

## Checkpoint — year-end integration and canonical V2 conflict

- Year-end focused runtime test run exited0:36 passed,0 failed,0 skipped. TRX `outputs/finance-demo/test-results/year-end-reviewed/Akwas_RHEMA-AKWASI_2026-09-29_20_33_06.trx` in worker tree. Fast-mode, not relational migration certification.
- Following prior accounting review and these focused results, local cherry-picks completed without conflict:70eb22bbd→`40651c252ea4246746f4f0e470bac9be487c13c8`;768f03d65→`5230ac1b2f94202d2af0ad74d0f385b4509a1d99`;fe30feb3e→`acba892d48568231745736066a375456816b6fda` (current integration HEAD). Coordinator added uncommitted snapshot entity/key/relationships matching migration001; snapshot compile/relational parity remain pending.
- SUBSEQUENT REVIEW FINDING — CHANGES_REQUIRED before year-end release: canonical `FINANCE_ACCOUNTING_BOOK_MODEL_V2_20260921.md`, already merged by a5baaf88f, deliberately uses tenant fiscal periods as sole period authority and retires per-book-period UX and Primary replacement. The older remediation plan and new year-end implementation incorrectly revived per-book approval requirements. Passing tests encoded that outdated assumption and do not close this finding.
- User asked to confirm preserving current V2 shared calendar while retaining book-specific year-close evidence. Pending reply, year-end stays unreleased and no database mutation is permitted. Zero AccountingBookPeriods is EXPECTED under V2 and is not a UAT blocker; the earlier ledger comments treating it as a blocker are superseded. September tenant period not open remains a genuine prerequisite.
- Shared source-book authority helper will honor exactly one perpetual active/default PrimaryFull, no designation or per-book-period reads. Historical exact journal/event inheritance remains valid. Workers received the canonical policy document and correction.
- Root L01 diagnostic now preserves the closed-period error prefix while adding authoritative posting date, book code, fiscal period code/name/state and approved-workflow guidance. Existing exact-string assertions now allow appended context; one engine test asserts all identifiers. No posting permission was relaxed. Changed diagnostic has not yet been compiled/run.
- Compiler granted to lease worker: session4552, LeaseInstallmentApOpenItemTests, fast EF + focused test build, durable artifacts/lease-ap-tests outputs. Root starts no parallel compiler.
- All integrated work remains local only. No migrations applied, no UAT configuration/data mutation, no deployment/push/new PR.

## Checkpoint — user confirmed V2; independent correction loops active

- User explicitly selected **Keep the shared fiscal calendar**. Year-end correction is authorized: use governed tenant FiscalPeriod close evidence, retain book-specific cycles, never recreate retired per-book calendar controls. Sol High assets worker assigned isolated branch/worktree `finance-demo-year-end-v2-20260929` at exact base `acba892d48568231745736066a375456816b6fda`. Owns year-end service/leaf guards, focused tests and handoff only; root snapshot and ordinary L01 message stay separate.
- Final independent D07 review returned CHANGES_REQUIRED for two additional predicates: Posted-but-GL-reversed payment journals remained eligible; non-midnight same-day invoices could be omitted from qualifying contract value. Root corrected all three journal eligibility guards and used an exclusive next-day invoice cutoff. Six new theory cases cover both reversal markers and selected/unselected same-day timestamps; retained certificate reads/cancellations remain possible. Combined rerun session42395 (`wht-period-review-corrections-build.log`, TRX directory `test-results/wht-period-review-corrections`) includes WHT/AP/tax/certificate and FinancePostingEngineTests; not yet completed at this checkpoint.
- Year-end initial36/36 pass is retained as historical evidence but DOES NOT certify the pending V2 correction. New correction needs independent review/rerun.
- Lease first compile stopped on six composite-FK migration overload errors before any test ran (session4552 exited1, no TRX). Worker is correcting these, retired period dependency, primary/replica legacy recognition selection and posted/unreversed exact legacy interest-account evidence. No compiler granted after that failure; root42395 owns slot.
- Lease cancellation/replacement design approved: immutable same-tenant predecessor link, unique active schedule owner, preserved voided history and unique terminal-chain resolution; no timestamp guessing, no deletion of posted/voided evidence. Migration003 remains unapplied and can incorporate these additions. UI must respect AP-create permission and preserve historical period dimension evidence.
- AP source-book linkage planned as server-owned nullable SourceBookAuthorityId on VendorInvoice with same-tenant FK to shared authority. Caller migration005 reserved for root after reviewed helper004; no historical approval fabrication. Shared helper explicitly follows V2 perpetual Primary, never retired designation/period lookups.
- Focused frontend payment tests rerun on combined branch:20/20 at20:50:59. Combined WHT/year-end frontend lint session71230 exited0. Original shared dirty workspace checked and unrelated changes preserved.

## Checkpoint — integrated regression and lifecycle corrections

- Root combined fast-mode WHT/AP/certificate/Ghana-tax/FinancePostingEngine run42395 completed:223 passed,2 failed,0 skipped of225. TRX `outputs/finance-demo/test-results/wht-period-review-corrections/Akwas_RHEMA-AKWASI_2026-09-29_20_59_22.trx`. Both failures exposed historical certificate read/cancellation incorrectly using the newly strengthened unreversed-journal guard. Root now distinguishes historical read/print/idempotent issued-version replay/cancellation from new issue/reissue/remittance; tests include print/replay preserving the single issued version. Latest correction remains unverified until rerun.
- Year-end shared-calendar correction uses actual FinanceCloseCycle/FinanceCloseCertification signed authority, deterministic frozen snapshot and zero legacy book-period prerequisites. Independent review additionally found shared-period reopening could supersede evidence while a book-year remained closed, making its reversal impossible. Worker is adding the narrow real period-reopen blocker/fingerprint and sequence tests before acceptance. Compiler slot transferred to that worker after root42395 completed.
- Shared source-book authority remains REVIEW_REQUIRED. Root requested exact workflow type/state validation, unreversed/date/module-bound original evidence, latest-version-only retries, no superseding bound sources, and explicit inherited resubmission behavior. Schema004 and source/caller005 remain unapplied; no caller may adopt unreviewed helper behavior.
- No database/configuration mutation, deployment, remote push or new PR. Approximate completion remains50%; correction loops are not counted as new completed release gates.

## Safe pause — user reassessing model and usage

- User asked whether to switch coordinator to GPT-5.6 Sol. All three active workers were already assigned Sol High and have confirmed safe pause. Do not restart implementation until user resumes. Live account usage at this checkpoint:73% used/27% remaining, shared account-wide; this is not per-task attribution.
- Integration HEAD remains `acba892d48568231745736066a375456816b6fda`; coordinator WHT/L01/snapshot/docs remain uncommitted. Root test42395 has finished. Latest historical WHT read/list/print/replay/cancellation corrections and assertions are applied but not yet rerun; 223/225 is the preceding result, not the latest source pass. No AP authority adapter/caller005 implementation started.
- Year-end V2 worktree at acba892d: five modified files (handoff, FiscalPeriodService, FinancePostingEngine, GeneralLedgerService, FiscalYearCloseTests); no commit/compiler. Shared-period reopen blocker and book-cycle fingerprint are applied; real AccountingPeriodClosePostingDateTests sequence regression and updated handoff still pending. Then focused compile/test, independent WHT read-only review and year-end commit/handoff.
- Lease worktree at52ce3a7:14 modified+3 new files, no commit/compiler, migration003 unapplied. Corrected source/period/permission/replacement logic needs compile/focused tests and final review. Root AP transaction change will supersede lease's PostCore bypass predicate hunk, after accepted integration.
- Source-book helper worktree at52ce3a7:10 files staged, no commit/compiler, migration004 unapplied; no caller conversion started. Latest workflow/version/original-evidence review corrections need compile/tests and final review. Verify explicit test-csproj inclusion for the two new test files before claiming default-suite coverage. Model snapshot remains root-owned.

## Resume checkpoint — WHT green and shared-calendar year-end verified

- Coordinator resumed on GPT-5.6 Sol High. Astra remains prohibited unless the user explicitly approves it. Work continues sequentially from the saved checkpoint before any caller-scope expansion.
- Final WHT/AP-payment/certificate/Ghana-tax/FinancePostingEngine gate: **225 passed, 0 failed, 0 skipped**. TRX: `outputs/finance-demo/test-results/wht-final-green/Akwas_RHEMA-AKWASI_2026-09-29_21_41_41.trx`.
- The last two failures were a regression-fixture defect: the serializable service boundary had cleared EF tracking, so the test mutated a detached payment. The fixture now reloads the persisted payment before setting Reversed/Voided; the two focused cases pass **2/2**.
- Final lifecycle rule: immutable issued-certificate history remains readable/printable and an active issued version may be replayed idempotently after source retirement; reissue, new remittance evidence and a new certificate after cancellation remain fail-closed.
- Focused AP payment frontend tests remain **20/20** and focused lint remains green from the combined-branch run recorded above.
- Shared-calendar year-end correction commit `3c22104ad` is clean and `git diff --check` passes. Independent TRX summary: FiscalYearCloseTests **34 passed**; AccountingPeriodClosePostingDateTests **22 passed**; total **56/56**. No migration or database mutation was performed by that package.
- WHT package is staged and awaiting the bounded independent lifecycle review. Integration order remains: commit WHT/L01/snapshot/docs, cherry-pick `3c22104ad`, run combined verification, then resume the paused lease package followed by the source-authority helper.
- Safe resume: preserve all worktrees and staged/unstaged changes; reconcile any unrelated running dotnet processes before starting one compiler. Complete these bounded review/test units before further caller expansion. The user-confirmed shared fiscal calendar supersedes retired per-book period assumptions throughout the older plan.
