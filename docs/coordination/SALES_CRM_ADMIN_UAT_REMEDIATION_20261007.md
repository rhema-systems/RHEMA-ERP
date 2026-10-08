# Sales, CRM, Helpdesk, and Administration UAT Remediation

## Objective

Complete the 2026-10-07 non-Finance UAT fixes for CRM opportunity access, CRM activity attendees, public property-enquiry source display, property-enquiry sales-order UOM, Sales Order workflow summaries, role and user administration usability, and the shared environment banner.

## Source and ownership

- Workstream type: Sales / CRM / Administration UAT (outside Finance coordination)
- Branch: `codex/sales-opportunities-role-fixes`
- Isolated worktree: `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2\.worktrees\sales-opportunities-roles`
- Exact base: `2da59240dfeba7d1fd0d6bcdf4c59d3bbaf90feb` (`origin/master` at workstream creation; includes merged PR #368)
- Implementation commit: `641f308bf07` (`Fix CRM access and sales administration workflows`)

## Implemented application changes

1. CRM opportunity-stage reads require the database-backed `crm.read` permission and updates require `crm.manage`. The CRM permission catalogue is seeded for the Roles UI. Access is driven by tenant-scoped role-permission assignments rather than role names. The established SuperAdmin authorization-handler bypass remains, and TenantAdmin receives the seeded CRM permissions.
2. CRM activity create/edit separates searchable internal employee attendees from comma-separated external attendees. Employee IDs are tenant validated, de-duplicated, capped at 25, persisted as JSON, and resolved to display details on reads. The legacy attendees field remains the external-attendee compatibility field.
3. External-enquiry ticket rows expose and use the persisted public property-enquiry relationship to label public `/property-listings` submissions as `Public Site` under Submitted By.
4. Sales orders created from a property enquiry show the fixed UOM label `Each` and submit the canonical `EA` UOM code expected by commercial quantity validation.
5. Workflow entity summary normalizes the requested entity alias to the registered workflow entity code for active-state, current-step, and approver checks.
6. Role permission groups support per-module select all, indeterminate state, selected counts, and collapse/expand behavior.
7. The shared full-width environment banner was removed from the dashboard shell. The compact environment-aware navbar badge remains and stays hidden in production.
8. The shared Users create/edit dialog supports accessible, case-insensitive role search over role names and descriptions, preserves selected roles hidden by the filter, and provides a no-results/clear state.
9. The merged external-portal logout call now supplies the required logout reason so the frontend typecheck succeeds.

## Migration and data status

- Added migration `20261007173000_AddCrmActivityInternalAttendees`.
- Adds nullable `nvarchar(2000)` column `InternalAttendeeEmployeeIdsJson` to `CrmActivities`.
- `ApplicationDbContextModelSnapshot` is updated.
- Migration has not been applied to a local or deployed database in this workstream.
- Existing/stale internal employee IDs are safely omitted when resolving attendee display records.

## Verification evidence

- Frontend focused Vitest: 13/13 passed across six test files.
- Frontend TypeScript: `tsc --noEmit --incremental false` passed.
- Targeted ESLint across all changed TypeScript/TSX files passed.
- CRM permission authorization and route tests: 8/8 passed. Coverage includes an arbitrarily named role with assigned permission succeeding, a literal `Sales Manager` role without permission failing, and the established SuperAdmin handler path succeeding.
- CRM activity service tests: 2/2 passed, including tenant validation.
- Workflow entity-summary test: 1/1 passed using C-drive artifacts.
- Shared/Core/Data/API/API-test projects compiled successfully through the redirected workflow test; existing repository warnings remain.
- Frontend production build passed. The retained-cache rerun compiled in 5.0 minutes, collected page data, generated 3/3 static pages, finalized optimization, and emitted the full route manifest.
- The first C-redirected attempt compiled successfully, then page-data collection could not resolve `react/jsx-runtime` because the generated output was outside the repository tree. Setting `NODE_PATH` to the repository `node_modules` resolved the C-output module lookup.
- `git diff --check` and staged `git diff --cached --check`: passed.

## Known failures and constraints

- D: has less than 1 GB free. A normal API test build exhausted the disk while copying the test project's dependencies after the product projects had compiled. Generated `bin` directories under this isolated worktree were removed, and final .NET artifacts were redirected to `C:\Users\USER\AppData\Local\Temp\rhema-sales-opportunities-dotnet`.
- The first redirected frontend build's source compilation succeeded; its post-compile page-data failure was caused by module lookup from the C-drive output junction, not a source compile error.
- Existing compile warnings are outside this workstream and were not expanded into unrelated fixes.

## Authorization boundaries

- No Sales role names are hardcoded for opportunity-stage access.
- Roles obtain opportunity access through `crm.read` and `crm.manage` assignments in the Roles UI.
- Users without the required permission remain denied even if their role happens to be named `Sales Manager`.
- No authorization bypass was added. The existing tenant-aware permission handler and its established SuperAdmin behavior are reused.

## Remaining work

- No implementation or verification work remains in this worktree.
- Do not push, open, or merge a PR until the parent task explicitly proceeds.

## Follow-up: configured Won stage and Estate handoff

- Branch: `codex/fix-estate-won-handoff`
- Exact base: `22fd949a26e5bbe39bbee4af09a64ab0c6f3c7e3` (`origin/master`, merged PR #370)
- Defect: the CRM opportunity displayed the tenant-configured stage `Won`, while the property-enquiry page and final Estate handoff service still required the legacy literal `Closed Won`.
- Resolution: the handoff projection and service now use the linked opportunity stage definition's `IsWon` outcome. Historical opportunities without a stage-definition link retain compatibility for `Won` and `Closed Won`.
- API response: the estate-handoff opportunity projection now exposes `isWon`; the frontend consumes that governed outcome instead of comparing display text.
- Regression coverage: frontend property-enquiry test passes for a stage named `Won`; six Estate handoff service tests pass, including the new configured-Won case; the focused property-enquiry controller test passes and verifies `isWon` plus `canHandoff`.
- Build environment: focused .NET tests used `TdcFastEfBuild=true`, C-drive artifacts, and the installed .NET 10 SDK with runtime roll-forward because the workstation currently lacks the repository-pinned .NET 9 SDK and .NET 8 runtime. The temporary `global.json` change was restored and is not part of the worktree diff.
- Remaining work: publish this follow-up together with the separate Sales Order workflow-summary correction once that investigation and verification complete.

## Follow-up: existing customer qualification without duplicate Lead

- Decision: a property enquiry already linked to an approved, active Customer Business Partner qualifies against that customer account. It does not create another CRM Lead.
- Compatibility: enquiries without a linked customer retain the existing Lead lifecycle. Enquiries that already have a Lead keep that lineage even if a customer is linked later.
- Data model: `EhcPropertyEnquiryProspect.LeadId` and `ProspectDepositReceipt.LeadId` are nullable. Opportunity and allocation creation pass the optional Lead reference and the existing Business Partner reference through their canonical DTOs.
- Migration: `20261007190000_AllowExistingCustomerPropertyProspectsWithoutLead` makes both Lead foreign keys nullable and filters the tenant/Lead uniqueness index to non-null Lead values. The down migration refuses rollback while customer-only records exist rather than manufacturing fake Lead identifiers.
- Validation: the linked Business Partner must exist in the tenant, be active, be approved, and have the Customer role before the no-Lead path is allowed.
- UI: the Sales qualification card states when the linked customer account is being used and that no duplicate Lead will be created. Sales handoff URLs omit `leadId` when the opportunity is customer-only.
- Verification: the focused backend lifecycle suite passes 17/17, including record-contact, qualification, and Existing Customer opportunity creation with no Lead row or ticket `CrmLeadId`; the frontend property-enquiry suites pass 24/24; the full frontend TypeScript check passes.
- Release preflight: the nullable-Lead migration's rollback-only guard is source-hash pinned in the canonical CRM migration preflight manifest. `Test-CanonicalMigrationPreflight.ps1` passes with all 42 guarded-migration coverage IDs.
- Remaining work: commit this follow-up, integrate the separately verified Sales Order workflow-summary correction, refresh from `origin/master`, and publish the authorized consolidated follow-up PR.

## Follow-up: production CRM and Sales permission catalogues

- Defect: `crm.read` and the other CRM permission definitions were registered as authorization policies and included in development seeding, but production startup does not run the development-data seeder. Existing production databases therefore had no CRM permission rows for the Roles UI to display or assign.
- Resolution: data migration `20261007191000_SeedCrmAndSalesPermissionCatalogues` idempotently creates or repairs `crm.access`, `crm.read`, and `crm.manage` in the `CRM` category and six Sales capabilities in the `Sales` category. The Roles UI groups the server catalogue dynamically, so these permissions are selectable for any management-created role.
- Authorization: opportunity-stage access remains permission based. No Sales role names are embedded in the policy. `crm.manage` satisfies CRM read and access policies; `crm.read` satisfies the access and read policies; `crm.access` grants workspace access only.
- Rollback safety: the migration retains the catalogue on downgrade because administrators may have attached the permissions to live dynamic roles after deployment.
- Role UI: a control beside permission search now collapses or expands all currently visible module accordions. Search results remain controllable instead of being forced open.
- Verification scope: backend authorization policy tests cover the CRM and Sales permission hierarchies. Roles UI tests cover CRM/Sales catalogue display, permission selection, per-module selection, and expand/collapse-all behavior.
- Authorization boundary requiring confirmation: applying the new Sales capabilities across every existing Sales controller would change production access for a large endpoint family. An automatic approval review rejected a heuristic all-controller convention due to the risk of misclassifying endpoints and denying valid users. The proposed explicit mapping is GET/read -> `sales.read`, create/edit/lifecycle -> `sales.manage`, approval/confirmation/closing -> `sales.approve`, setup writes -> `sales.configure`, and reports -> `sales.reports.read`. Endpoint-by-endpoint enforcement remains pending explicit user approval of that access change.

## Follow-up: property enquiry customer action, Estate handoff, Finance alert, and error UX

- Branch/worktree: `codex/fix-property-customer-action` in the retained `sales-opportunities-roles` worktree.
- Exact base: `4c32bbb258fe77e3a80899c650cbc3dcb19dff02` (`origin/master`, merged PR #371).
- Customer action: when the cleared deposit threshold is met, `Create customer` now runs the existing-customer match check itself. It opens customer creation only when no approved match is returned and directs the operator to the matches when a possible duplicate exists.
- Estate handoff: a successful handoff immediately changes the action to disabled `Handed to Estate`; persisted Estate case or handoff timestamp also keeps it disabled after reload.
- Finance notification: recording a pending prospect deposit now sends an in-app alert to active, unexpired tenant users whose dynamically named role grants `Finance.AR.Payments.Receive`. The alert carries the receipt, ticket, amount, currency, status, and a direct property-enquiry URL. Notification delivery is best effort after the receipt commit so a channel failure cannot duplicate or undo the deposit.
- Error UX: the page-top aggregate error banner was removed. Query and mutation failures now use destructive toasts with the server message, while the contextual Sales Order source alert remains beside its retry action.
- Migrations: none.
- Verification: focused property-enquiry Vitest passed 7/7; focused ESLint passed; full frontend TypeScript check passed; focused `PropertyEnquiryProspectLifecycleTests` passed 18/18 with .NET SDK 9.0.315; `git diff --check` passed.
- Known constraints: the backend build still emits the repository's existing warning set; no new warning or test failure was introduced by this change.
- Publication: implementation commit `f73826d229f9762a7379a855509f99e8d948aea6` was published in PR #372 and merged to `master` as `c8d781c355eaa75767a186f26e3d4e5ffc337347`.
- Deployment: Windows VPS CI/CD run `37697511362` built and deployed merge commit `c8d781c355eaa75767a186f26e3d4e5ffc337347`. Release-contract validation, immutable build and package upload, application and SQL backups, migration guards and application, service readiness, public API/assets/CORS checks, and headless Chrome smoke all passed. The VPS `/api/health/live` endpoint returned HTTP 200 with `Healthy` after activation.
- Workflow reporting constraint: the application deployment passed and its evidence was published to the VPS, but the GitHub workflow concluded `failure` because the final `actions/upload-artifact` evidence-retention step hit the repository artifact-storage quota. No application rollback or deployment failure was reported.
- Remaining work: none for this property-enquiry follow-up. GitHub artifact quota cleanup or expansion remains a repository-administration task if the sanitized evidence must also be retained in GitHub Actions.

## Follow-up: GitHub artifact quota must not mask a successful VPS deployment

- Branch/worktree: `codex/soft-fail-deploy-evidence-upload` in the retained `sales-opportunities-roles` worktree.
- Exact base: `c023a81a1c0571fbddc979faab7c5123f16e0883` (`origin/master`, merged documentation PR #374).
- Implementation commit: `b1da676d0d0` (`Do not fail deployments on evidence quota`).
- Publication: PR #375 merged the workflow, contract-test, runbook, and ledger changes to `master` as `f7a9c60c6969c186e54a40ee706704b8a638472e`.
- Resolution: the final GitHub copy of sanitized deployment evidence uses `continue-on-error: true`. A storage-quota failure now produces a warning after deployment instead of changing a verified activation to a failed workflow. The immutable release artifact upload, VPS activation, backups, migrations, readiness checks, public smoke checks, and browser smoke remain mandatory and fail the job normally.
- Evidence ownership: `Deploy-RhemaVps.ps1` continues publishing the authoritative sanitized deployment evidence under `C:\RhemaERP\logs` on the VPS before the optional GitHub upload runs.
- Migration and application status: no migration and no application-runtime change. The already deployed application remains merge commit `c8d781c355eaa75767a186f26e3d4e5ffc337347`, verified HTTP 200/Healthy.
- Verification: `scripts/vps/Test-RhemaReleaseArtifactFlow.ps1` passed with its new assertion for the nonblocking evidence-upload contract; `git diff --check` passed.
- Remaining work: none. The PR and merged-master release-contract validations passed. No VPS redeployment is required for this workflow-only correction.

## Follow-up: GitHub Actions artifact quota cleanup and prevention

- Branch/worktree: `codex/reduce-vps-artifact-retention` in the retained `sales-opportunities-roles` worktree.
- Exact base: `9061198c7778c8d4cb3376d18df310d6f88988ff` (`origin/master`, merged ledger PR #376).
- Implementation commit: `2cc660b8c03` (`Reduce VPS release artifact retention`).
- Publication: PR #377 merged the retention policy, contract-test, runbook, and ledger changes to `master` as `27132d05904554645ec7541d2ca4fc2f67f76eca`.
- Cleanup authorization and result: after explicit user approval, 22 older `rhema-vps-*` GitHub Actions artifacts were deleted. They accounted for approximately 13,677.80 MB. The three newest reviewed release packages were preserved: `c8d781c3`, `4c32bbb2`, and `22fd949a`.
- Verified inventory after cleanup: 98 total artifacts using approximately 1,890.44 MB, including three immutable VPS release packages using approximately 1,887.27 MB.
- Prevention: immutable VPS release transport artifacts now retain for three days instead of 14. The mandatory release upload remains blocking; the VPS continues retaining its current, previous, and recent rollback releases independently.
- Migration and application status: no migration and no application-runtime change. No VPS deployment is required for this workflow-only retention correction.
- Verification: `scripts/vps/Test-RhemaReleaseArtifactFlow.ps1` passed with a new assertion for the three-day release artifact contract; `git diff --check` passed.
- Remaining work: none. The PR and merged-master release-contract validations passed. GitHub may take 6-12 hours to recalculate the account-level quota after deletion.

## Follow-up: deployment retry blocked by delayed GitHub quota recalculation

- Deployment candidate: current `master` commit `cec3306f49ae02d2ce6842936d456106e90c2e46` (merged documentation PR #378).
- First dispatch: Windows VPS CI/CD run `37707932832`. Release-contract validation and the complete immutable release build passed, but the mandatory release-artifact upload failed with `Artifact storage quota has been hit`; VPS activation was skipped.
- Additional cleanup: after the authorized cleanup, the final three retained `rhema-vps-*` packages were deleted. This removed another 1,978,945,799 bytes and left 95 small artifacts totaling 3,328,808 bytes in the repository API inventory.
- Second dispatch: Windows VPS CI/CD run `37711731850` rebuilt the same exact commit. Release-contract validation and release generation passed again, but the mandatory upload returned the same quota error because GitHub had not recalculated account storage yet; VPS activation was skipped.
- Application status: neither retry changed the VPS. The last verified deployed application remains commit `c8d781c355eaa75767a186f26e3d4e5ffc337347`, whose activation and public health checks passed in run `37697511362`.
- Authorization boundary: the mandatory immutable release upload remains blocking. It was not weakened or bypassed, and no manual VPS copy was performed.
- Remaining work: after GitHub completes its documented 6-12 hour quota recalculation, rerun Windows VPS CI/CD with `build_release=true` and `deploy_to_test_vps=true`; require artifact upload, VPS activation, migrations/readiness, public smoke, browser smoke, and post-deployment `/api/health/live` verification before closing deployment.
