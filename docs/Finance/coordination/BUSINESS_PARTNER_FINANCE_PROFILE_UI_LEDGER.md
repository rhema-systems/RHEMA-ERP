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
