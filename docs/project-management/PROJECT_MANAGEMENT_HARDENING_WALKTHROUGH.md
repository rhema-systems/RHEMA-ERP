# Project Management Hardening Walkthrough

## Scope

This walkthrough covers the project-owned hardening work completed after the main Project Management feature track:

- permission walkthroughs
- performance tuning
- report and export verification
- frontend type-check cleanup work adjacent to Projects

It does not include Finance module system-of-record ownership. Project invoice and payment truth must continue to come from the Finance team integration.

## Permission Walkthrough

Project authorization continues to use the existing Project service authorization layer and existing ERP workflow engine.

Primary authorization split:

- `ManageExecution`
  - owner and management project roles
  - execution contributor roles such as `TeamMember`, `TaskOwner`, `Resource`, `ResourceManager`
  - operational contributors inferred from allocations, work items, timesheets, and expenses
- `ManageFinancials`
  - owner and management project roles
  - financial contributor roles such as `Finance Officer`, `Billing Officer`, `Commercial Manager`
- `ManageGovernance`
  - owner and management project roles
  - governance contributor roles such as `Risk Officer`, `Compliance Officer`, `Auditor`

Walkthrough scenarios now covered in service tests:

- execution contributor can add resource allocations
- execution-only contributor is denied financial summary access
- governance contributor can add risks
- finance-only contributor is denied governance edits
- focused permission verification executed successfully on March 12, 2026: `6/6` tests passed

Relevant files:

- `src/ErpSystem.Core/Services/Projects/ProjectService.Authorization.cs`
- `tests/ErpSystem.Core.Tests/Services/Projects/ProjectServiceTests.cs`

## Performance Tuning

Resource routing enrichment previously loaded tenant-wide employee and skill data even when an allocation only required a narrow skill profile.

The current tuning in `EnrichResourceAllocationsAsync` now:

- normalizes required skill names first
- loads only matching `Skill` rows for the required names
- loads only verified `EmployeeSkill` rows for those skill ids
- limits candidate employees to assigned users plus users who actually match required skills
- limits overlapping allocation scans to the reduced employee set

Expected impact:

- lower tenant-wide memory pressure
- lower repository query volume for resource recommendation refresh
- faster project detail and resource allocation reads where routing analysis is enabled

Relevant file:

- `src/ErpSystem.Core/Services/Projects/ProjectServices.cs`

## Report And Export Verification

The report workspace and API surface now have explicit coverage for the project-owned report endpoints added in the final delivery slices.

Engineering verification completed on March 12, 2026.

Focused service-level report verification passed:

- `resource-capacity`
- `resource-capacity-recommendations`
- `resource-optimization`
- `material-reconciliation`
- `procurement-reconciliation`
- `billing-summary`
- `external-collaboration`
- `portfolio-prioritization`
- `strategic-initiatives`
- `dependency-watch`
- focused report service verification executed successfully on March 12, 2026: `13/13` tests passed

Verified report routes added to controller-route coverage:

- `material-cost-ledger`
- `resource-capacity`
- `resource-capacity-recommendations`
- `resource-optimization`
- `billing-summary`

Existing verified routes already covered earlier:

- `invoice-request-queue`
- `workflow-approval-queue`
- `portfolio-prioritization`
- `strategic-initiatives`
- `dependency-watch`
- `material-reconciliation`
- `procurement-reconciliation`
- `external-collaboration`
- focused report route verification executed successfully on March 12, 2026: `13/13` tests passed

Relevant files:

- `src/ErpSystem.Api/Controllers/Projects/ProjectsController.cs`
- `tests/ErpSystem.Api.Tests/Controllers/Projects/ProjectsControllerRouteTests.cs`
- `tests/ErpSystem.Core.Tests/Services/Projects/ProjectServiceTests.cs`
- `frontend/src/app/development/project-reports/page.tsx`

## Frontend Type-Cleanliness Sweep

The cleanup pass targeted the repo areas that were blocking broader type-check progress outside the core Projects workspace.

Completed cleanup batches:

- shared `DataTable` row cell typing
- helpdesk administration pages
- identity-management online-users pages
- helpdesk runtime pages
- nullable `usePathname()` guards in administration, helpdesk, maintenance, and sidebar layouts

Primary files touched:

- `frontend/src/components/ui/DataTable/DataTable.tsx`
- `frontend/src/app/administration/helpdesk/...`
- `frontend/src/app/administration/identity-management/online-users/...`
- `frontend/src/app/helpdesk/...`
- `frontend/src/app/administration/layout.tsx`
- `frontend/src/app/helpdesk/layout.tsx`
- `frontend/src/app/maintenance/layout.tsx`
- `frontend/src/components/layout/sidebar.tsx`
- `frontend/src/components/external-portal/external-sidebar.tsx`

## Remaining Hardening Work

Still outstanding after this pass:

- run full frontend type-check to capture the next repo-wide error set after the current cleanup batch
- complete UAT walkthroughs with project execution, finance-facing project users, governance users, and external collaboration users
- validate exported files with business users during UAT for business acceptance formatting expectations
- continue performance profiling against realistic tenant data

## Constraints Preserved

- Project approvals still use the existing ERP workflow engine
- no fake Finance module integration was introduced
- the enhanced project Gantt planner was not reverted
