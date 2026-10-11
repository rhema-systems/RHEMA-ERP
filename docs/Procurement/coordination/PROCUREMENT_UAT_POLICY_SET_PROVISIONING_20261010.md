# Procurement UAT policy-set provisioning — 10 October 2026

## Objective

Restore repeatable Procurement configuration after a recreated database and make two distinct Published UAT policies usable from Purchase Requisitions through sourcing, award, and contract creation:

- `UAT-WORKS-TENDER-50K-100K`: Works, National Competitive Tendering, GHS 50,000 through GHS 100,000 inclusive.
- `UAT-CLEANING-RFQ-1K-50K`: General Services / cleaning services, Request for Quotation, GHS 1,000 through GHS 50,000 inclusive.

## Git and workspace

- Branch: `codex/procurement-uat-policy-sets`
- Worktree: `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2\.worktrees\procurement-uat-policy-sets`
- Original base: `57d565aeffd340f40cd1dd5b35017a0c064f4e3d` (`origin/master` at worktree creation)
- Final exact base: `f7cccb50851db41862f7d5fcfb2f0a96ce6a2ae2` (`origin/master`, merge of Finance PR #398 after PR #399)
- Commit: one local commit above the final exact base, subject `Provision Procurement UAT policy sets`; the final hash is recorded in the handoff because a commit cannot contain its own hash.
- Pull request: none

## Implementation state

- `ProcurementUatPolicySetSeeder` is invoked by the repeatable `seed-deployment-uat` / `OperationalUatBaselineSeeder` path.
- Provisioning is create-only by reserved policy/profile code. Running it again converges without duplicate policies or workflows.
- An existing record using a reserved policy code is preserved without changing its name, lifecycle, currency, rules, or tenant edits; only a missing sibling policy is added.
- Both policies are separate, Published, effective, tenant-scoped, non-default records with exact category, method, GHS threshold, authority, evidence, segregation-of-duty, and workflow references.
- The seeder reuses compatible active Published workflow definitions and creates a narrow Published UAT definition only where none exists.
- Purchase Requisitions now persist `ProcurementPolicySetId`. The create/edit UI lists only eligible Published policies for the selected category, document currency, and total value; it auto-selects one match and requires an explicit selection when several policies match.
- The stored policy identity is passed to requisition authority routing and sourcing compliance, eliminating the multiple-Published-policy ambiguity.
- Requisition detail shows the immutable governing policy code, name, and version.

## Migration

- `20261010224448_AddPurchaseRequisitionPolicySelection`
- Adds nullable `PurchaseRequisitions.ProcurementPolicySetId`, its indexes, and a restrictive foreign key to `ProcurementPolicySets`.
- Application status: generated and compiled; not applied to any database in this task.

## Verification evidence

- Product/test-project build: passed, 0 errors. The complete build reported existing repository warnings only.
- `ProcurementUatPolicySetSeederTests`: 5 passed, 0 failed after the final Finance rebase (39s, `--no-build --no-restore`); the preceding clean compile plus focused execution also passed 5/5 (1m21s test duration).
- Test coverage proves exact categories, methods, GHS currency, inclusive lower/upper boundaries, out-of-band rejection, two-run idempotence, preservation of an existing reserved record, explicit selection for overlapping policies, exact selected-policy resolution, and single-Published-policy backward compatibility.
- EF Core `migrations has-pending-model-changes --context ApplicationDbContext --no-build`: passed after rebasing onto the Finance migration; the Finance migration, Procurement migration/designer, and combined model snapshot are aligned.
- Frontend `tsc --noEmit`: passed using the main checkout dependency tree through a temporary worktree junction; the junction was removed after validation. The final rebase did not overlap any Procurement frontend path, so this result remains applicable.
- `git diff --check`: passed after migration and documentation generation.

## UAT documentation

- `docs/TDC_QS_END_TO_END_UAT_WALKTHROUGH.html` now includes the policy preflight, exact Works/NCT band, Purchase Request policy selection, and downstream tender lineage.
- `docs/PROCUREMENT_END_TO_END_UAT.md` now includes both exact policies, selector behavior, boundary expectations, the cleaning-services RFQ with three responsive quotations, and the awarded cleaning contract handoff to Facilities.

## Known constraints and risks

- These are fictional UAT thresholds. They must not be treated as a statutory production policy without business approval.
- Existing reserved-code records are intentionally preserved. If one exists in Draft, Retired, deleted, or tenant-modified state, an administrator must review it rather than expecting this seeder to overwrite it.
- The fallback for a tenant with exactly one Published policy is retained for backward compatibility. With more than one Published policy, the requisition must bind an eligible policy explicitly or through unambiguous auto-selection.
- Repository-wide ImageSharp advisory and existing compiler warnings remain outside this Procurement workstream.

## Remaining work and authorization boundaries

- Final diff/schema checks completed; the migration contains only the intended nullable policy binding, indexes, and restrictive foreign key.
- The clean local Procurement commit is authorized for handoff. Push, pull request, merge, database migration application, seed execution against VPS, and deployment remain owned by the root integration task.
