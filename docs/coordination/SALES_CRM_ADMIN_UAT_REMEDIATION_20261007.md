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
- Remaining work: commit this follow-up, integrate the separately verified Sales Order workflow-summary correction, refresh from `origin/master`, and publish the authorized consolidated follow-up PR.
