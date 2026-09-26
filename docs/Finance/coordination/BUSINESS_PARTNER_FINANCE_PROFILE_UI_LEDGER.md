# Business Partner Finance-profile implementation ledger

- Objective: expose canonical multi-role Business Partners and governed AP/AR Finance profiles through supported API and UI workflows.
- Authorized boundary: Procurement-owned Business Partner create/edit UI and service may be extended for canonical roles; Finance owns profile versioning, WHT defaults, permissions and approval decisions.
- Branch: `codex/finance-accounting-book-model-v2`
- Base: `c998c446`
- Database policy: existing profile tables only; no migration or database mutation is authorized by this implementation phase.
- Required controls: draft/submitted/approved lifecycle, effective dating, one AP default WHT line, maker-checker, tenant isolation, role eligibility and Finance permission enforcement.
- Documentation requirement: cross-module ownership and compatibility behavior must be explicit in code comments.
- Implemented surface:
  - canonical multi-select Supplier, Contractor and Customer roles at Business Partner creation;
  - Finance-owned AP and AR profile tabs on the Procurement Business Partner record;
  - effective-from/effective-to profile versions with Draft, Submitted, Approved and Rejected states;
  - independent maker-checker decisions protected by Finance permissions;
  - multiple AP WHT category defaults with exactly one default-for-AP selection;
  - AR credit-limit and withholding-agent defaults;
  - explicit compatibility projection to the legacy Procurement `PartnerType` field, documented in code so Procurement maintainers can remove it safely later.
- Verification completed:
  - isolated API production build: passed (0 errors; existing repository warnings remain);
  - changed frontend files ESLint: passed;
  - focused Business Partner creation UI tests: 2 passed;
  - changed-file TypeScript diagnostics: passed;
  - whitespace validation: passed.
- Verification baseline constraint: the broad Core test project cannot compile because pre-existing canonical-partner cutover tests still reference removed `SupplierId`/`Supplier` members and an obsolete migration type. These failures are outside this change and occur before the requested policy test can execute.
- Status: IMPLEMENTED_AND_FOCUSED_VERIFIED
- Remaining gate: manual two-user maker/checker UAT against the configured UAT database, using the canonical Business Partner UAT dataset.

## Canonical-partner Core test repair

- Scope: test-only repair after the AP `SupplierId`/`Supplier` bridge was intentionally removed; no production entity, service, migration or database changes.
- Repaired contracts:
  - AP and payment fixtures now use `BusinessPartnerId` and retain the immutable partner code/name snapshots required by Finance;
  - landed-cost invoice-link coverage uses the same canonical partner identity as AP;
  - the obsolete one-off WHT migration reflection test now verifies the durable current EF relational model;
  - Procurement-plan fixtures now use active HR organisation units and current nullable-to-canonical organisation-unit semantics.
- Verification:
  - complete `ErpSystem.Core.Tests` project build: passed with 0 errors;
  - five affected suites: 63 passed, 1 SQL Server integration test skipped, 0 failed;
  - complete Core suite: 3,164 passed, 30 skipped and 168 failed in 8m 41s;
  - the complete-suite failures are broader pre-existing baseline failures outside the five repaired suites, including repository-root discovery under isolated output, stale permission and organisation-unit fixtures, legacy migration-guard expectations, and unrelated CRM/Inventory coverage.
- Concurrent AP/WHT stream:
  - task `Vet AP invoices and payments` completed implementation in the dirty primary worktree;
  - no files overlap this test-only repair;
  - its changes are intentionally not staged, committed, copied or integrated here because the primary worktree also contains unrelated user/developer state;
  - final integration remains deferred until both clean commit ranges can be replayed against updated master and verified together.

## Combined latest-master integration

- Integration base: `origin/master` at `31dab7f32`; merged by `d781969f2` before the AP/WHT replay.
- AP/WHT source commit: `f868dd1b3`; replayed onto the Finance branch with conflicts resolved against the canonical Business Partner contracts.
- Resolution guarantees:
  - forward AP invoices and payments retain `BusinessPartnerId`; no legacy `SupplierId` bridge was restored;
  - Finance AP/AR control accounts remain authoritative;
  - invoice-owned WHT decisions survive payment posting, while older payment-configured transactions remain supported;
  - statutory thresholds aggregate only posted allocation evidence within contract/category scope;
  - the agreed exchange-rate direction is retained in cross-currency payment and supplier-advance evidence;
  - latest-master Estate and Quantity Survey fixtures were updated to canonical partner identity rather than compatibility aliases.
- Verification after integration:
  - Data project build: passed with 0 errors;
  - isolated API build: passed with 0 errors;
  - complete API test-project compilation: passed with 0 errors;
  - focused AP/WHT/subledger regression set: 129 passed, 0 failed;
  - full frontend TypeScript gate remains red on the latest-master baseline across HR, Inventory, Procurement, Civil Engineering and stale generated Next routes; Finance-owned diagnostics found during integration were corrected and verified separately.
- Operational note: a running local API held the normal Debug API output, so verification used isolated output directories and did not interrupt the user's server.

## Finance integration-gate recovery

- Failed run: `36234391858`, job `108383425785`, against `5f51bf1eb`.
- Root cause: the workflow requested `TdcFastEfBuild=true`, but the post-baseline Data project no longer implemented that item-selection path. The hosted runner compiled about 259 MB of generated EF migration target-model source, stopped making progress after Core completed, and GitHub cancelled the build before any Finance contract ran.
- Correction:
  - restored an explicit contract-test-only `TdcFastEfBuild` item group that omits migration designers and the model snapshot;
  - retained the full migration assembly for normal application, EF tooling, release and migration-verification builds;
  - pinned the gate to `ubuntu-24.04` ahead of the announced `ubuntu-latest` image migration;
  - moved the gate's official checkout and .NET setup actions to their Node 24 releases.
- Database state: unchanged; no migration was added or applied.
- Verification:
  - evaluated MSBuild's focused compile set: 439 source items, 0 migration designers, 0 model snapshots and 45 executable migration bodies;
  - reproduced the workflow's exact API test-assembly build locally: passed with 0 errors in 8m 55s;
  - pushed run `36237894891` proved the optimized assembly build completes on the hosted runner, then exposed seven stale consumer fixtures;
  - aligned AR rate fixtures to the governed functional-to-transaction storage convention while retaining transaction-to-functional posting snapshots;
  - supplied required functional-currency authority in fixed-asset disposal book fixtures;
  - replaced retired per-book-period expectations with tenant fiscal-period authority checks;
  - made migration discovery assert the reset boundary plus valid post-baseline migrations instead of assuming the baseline remains the only migration;
  - retained the reset baseline designer in the focused build so migration discovery continues to prove the active chain boundary;
  - all seven formerly failing contracts pass together locally (7/7 in 2m 20s).
  - the complete Finance-owned consumer filter passes locally (141/141 in 3m 23s);
  - independent accounting-contract review: APPROVED with no blocking findings; reviewer independently reproduced the seven corrected contracts (7/7).
- Remaining verification: the next pushed PR gate.

### Checkout compatibility follow-up

- Run `36239822934` stopped before compilation because `actions/checkout@v7` was configured with `persist-credentials: false`.
- Repository finding: `docs/EzFMC` is a legacy gitlink (`600f1c10b8a4c69e7385094fe753cede3ddcfdb3`) with no `.gitmodules` metadata in current or historical repository state.
- Decision: do not fabricate an external submodule URL or mutate the separately owned documentation component as part of the Finance gate repair.
- Correction: retain the Node 24 checkout action but use its standard credential lifecycle, which already completed the main checkout successfully in run `36237894891`. The orphaned gitlink remains a repository-hygiene item outside the Finance accounting change.
- Pending verification: rerun the complete hosted Finance integration gate.

### SQL Server contract follow-up

- Run `36239964462` proved checkout, the optimized API assembly, all Finance-owned consumer contracts, the Procurement consumer contract and the Inventory consumer contract pass on the hosted runner.
- The real SQL Server suite then exposed one stale test fixture: its supposed Parallel book omitted its governed base, replication cutoff and opening treatment, and incorrectly used the Primary currency. SQL Server correctly rejected that row under `CK_AccountingBooks_BaseShape`; 10 other relational contracts passed.
- Correction: model the fixture as a valid USD Parallel derived from the Primary, with a zero-opening cutoff after the test posting date. This keeps automatic replication (and its rate requirement) outside this concurrency contract. The contract now requires the current `PARALLEL_DIRECT_POSTING_FORBIDDEN` error and proves only the Primary can succeed and receive balances.
- Independent review initially identified the replication-cutoff and obsolete-error-code implications; both findings were incorporated before commit.
- Database state: unchanged; this is a test-fixture correction only.
- Pending verification: focused review and another complete hosted Finance integration gate.
