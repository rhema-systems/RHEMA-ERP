# Procurement Planning Requirement Tracker

Last updated: 2026-06-09

## Active Scope

This tracker follows the procurement planning requirement merge: annual and quarterly planning, budget-line capture, configurable workflow support, controlled publishing to procurement execution, consolidation, amendments, emergency procurement, dashboards, and reports.

## Task List

| Status | Slice | Notes |
| --- | --- | --- |
| Done | Compact multi-item entry dialog | Plan detail item dialog now supports left-side product selection, compact item details, queued item additions, and one final save to the plan. |
| Done | Annual/quarterly schema and DTO fields | Plan cycle and quarter fields added across entity, DTOs, API service, create/edit forms, and plan list/detail display. |
| Done | Budget-line fields | Item budget line/category/approved amount/notes fields added across entity, DTOs, API service, migration, detail add/edit UI, and item tables. |
| Done | ProcurementPlan workflow adapter | ProcurementPlan status adapter added and registered for workflow status transitions. |
| Done | Publish to procurement execution | Publish endpoint and detail-page publish dialog added; item conversion now requires a published Active plan. |
| Done | EF migration | Added migration for planning cycle, publish metadata, and budget-line fields. |
| Done | Departmental submission workflow UI | Plan detail now uses the existing workflow module through `WorkflowApprovalActions`, workflow history tab, ProcurementPlan entity-type registration, and workflow context enrichment. |
| Done | Consolidation engine | Approved/active plan needs are grouped by item/category/specification/UOM/currency, with consolidation opportunities exposed in the API, dashboard, and supplier-consolidation page. |
| Done | Plan amendments and version history | Approved/active/completed plans can create draft amendments with revision lineage, and the detail page includes amendment and version-history UI. |
| Done | Emergency procurement request flow | Emergency plans can now be activated, triggered into procurement, and deactivated from the planning UI while remaining linked to the existing emergency-plan module. |
| Done | Emergency plan frontend completion | Added emergency plan create, detail, and edit routes with department/currency/product/supplier master-data selectors, critical item entry, emergency supplier entry, and plan action controls. |
| Done | Planning analytics dashboard | Added procurement planning overview with budget utilization, category spend, consolidation savings, department summaries, and quarterly delivery view. |
| Done | Strategic procurement analytics | Added market and supplier analytics to the planning dashboard, including price increase, inflation/risk, long lead-time items, supplier risk score, active/preferred suppliers, supplier spend, and high-risk category signals. |
| Done | Market intelligence register enhancement | Added market availability, supply risk, lead time, previous price, price variance, inflation impact, and budget adjustment fields across schema, DTOs, services, and UI forms. |
| Done | Strategic form master-data selectors | Market analysis and supplier consolidation forms now use Finance currency and Inventory item/category selectors instead of free-text master-data fields. |
| Done | Finance fiscal-year selectors | Procurement planning plan/budget forms and dashboard/report filters now source fiscal-year choices from Finance fiscal years instead of numeric free-text entry. |
| Done | Market survey quotes | Added survey quote capture from the market analysis detail page with average, lowest, highest, latest quote, and recommended planning estimate calculations. |
| Done | Plan item market analysis link | Plan items can now link to published market analysis records from the compact item dialogs, with selected analyses prefilling item/category/unit-cost context. |
| Done | Consistent plan item add dialog | The edit-plan Items tab now opens the same compact multi-item batch dialog used by the plan detail Add Item button, while pencil actions remain single-item edit focused. |
| Done | Compact tabbed plan item dialog master data | Plan item add dialogs now use a compact Details/Suppliers tab layout, load supplier-capable approved Business Partners, and source UOM choices from active Inventory units of measure. |
| Done | Search-first plan item dialog | Plan item add/edit dialogs now share a compact search-first item body, hide the preloaded product list grid, show item results only after search, keep suppliers visible in the same dialog, and require only item description, UOM, and quantity up front. |
| Done | Emergency item free-text entry | Emergency plan critical items can now be selected from inventory or entered manually, with editable item description and selectable Inventory UOM for requesters who do not know the exact item master record. |
| Done | Procurement schedule filters | Procurement schedules now support department and date-window filters through the paged API and schedule list UI. |
| Done | Supplier consolidation UI completion | Added supplier consolidation create/detail/edit pages with preferred supplier selection, approve/implement actions, actual savings recording, and backend DTO exposure for implementation tracking. |
| Done | Reports and exports | Added annual, quarterly, department, budget-variance, and publish-register reports with spreadsheet export. |
| Done | Live workflow configuration and lifecycle verification | Configured the real workflow module for ProcurementPlan with department, procurement, finance, and final approval stages; latest .NET 8 verification ran `PP-2026-0004` through draft, submit, staged approvals, and publish. |
| Done | .NET 8 rollback and migration repair | Reverted runtime targets/packages/tooling to .NET/EF 8, pinned the repo SDK with `global.json`, stabilized seeded role concurrency stamps, and repaired the procurement migration so it completes cleanly from the partially migrated DB state. |
| Done | Workflow designer approver display fix | Workflow editor now hydrates ProcurementPlan approval step config with the same workflow JSON options used by the engine and normalizes numeric/string assignment enums so role names do not show as `0`. |
| Done | Global workflow recall action | Shared workflow summaries now expose requester recall permission; the global workflow action component shows Recall for requesters with active submitted workflows, confirms before recall, calls the shared recall endpoint, and registered workflow adapters return recalled records to draft. |

## Verification Log

| Check | Status | Result |
| --- | --- | --- |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore` | Passed | Existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore -p:OutDir="$PWD\artifacts\build-verify\api\"` | Passed | Isolated output build used because the normal Debug output was locked by a running API process. |
| `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore` | Passed | Migration compiles cleanly. |
| `dotnet ef migrations add AddProcurementPlanningCyclePublishBudgetLines --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext --output-dir Migrations --configuration MigrationVerify` | Passed | Generated the planning cycle, publish metadata, and budget-line migration. |
| Targeted ESLint for changed procurement planning files | Passed | No lint errors in service, list, create, detail, or edit files. |
| Filtered frontend type-check for procurement planning files | Passed | Repo-wide type-check output contained no procurement planning errors. |
| Full frontend type-check | Blocked by existing repo issues | Failures are in finance/CRM/mock-data areas, not procurement planning. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore` | Passed | Re-verified after workflow/dashboard/report/amendment slices. |
| `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore` | Passed | Re-verified after workflow/dashboard/report/amendment slices. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore -p:OutDir="$PWD\artifacts\build-verify\api-workflow\"` | Passed | ProcurementPlan workflow integration, controller endpoints, and service wiring compile cleanly. |
| Targeted ESLint for latest procurement planning files | Passed | Service, workflow mapping, overview, reports, supplier consolidation, emergency plans, and dynamic plan detail page lint clean. |
| Filtered frontend type-check for latest procurement planning files | Passed | Repo-wide type-check output contained no procurement planning or workflow mapping errors. |
| Local Next route smoke check | Passed | `GET /procurement/planning` and `GET /procurement/planning/reports` returned 200 on `http://localhost:3001`. |
| `dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext` | Passed | Applied `20260607233542_AddProcurementPlanningCyclePublishBudgetLines`; later rerun against the secret-backed DB reported no pending migrations. |
| API user-secret configuration check | Passed | Confirmed the active API `DefaultConnection` comes from user secrets and targets the `RhemaERP` database; secret values were kept redacted. |
| `dotnet --version` | Passed | Repo-level `global.json` resolves the SDK to `8.0.206`; `dotnet ef --version` reports EF tooling `8.0.0`. |
| .NET 10 residue scan | Passed | No active .NET 10 target, EF/OpenAPI 10 package, or .NET 10 Swagger API references remain in tracked source; stale generated output folders from the .NET 10 attempt were removed from the workspace. |
| `dotnet restore src/ErpSystem.Api/ErpSystem.Api.csproj` | Passed | Restore completed under .NET 8. |
| `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore /nr:false /m:1` | Passed | Data project and repaired migration compile under .NET 8; existing repo warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore /nr:false /m:1` | Passed | API compiles under .NET 8; existing finance/inventory/payroll nullable/async warnings only. |
| `dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext --no-build` | Passed | Completed `20260607233542_AddProcurementPlanningCyclePublishBudgetLines` against the user-secret `RhemaERP` DB after repairing the partial migration state. |
| SQL schema/history verification | Passed | Migration history row is present with product version `8.0.0`; all planning, publish, and budget-line columns, indexes, and FKs exist. |
| `dotnet build artifacts/procurement-plan-workflow-test/procurement-plan-workflow-test.csproj --no-restore /nr:false /m:1` | Passed | ProcurementPlan workflow verification harness compiled under .NET 8; existing warning noise only. |
| Live ProcurementPlan workflow lifecycle harness | Passed | Configured ProcurementPlan workflow version 6 and verified `PP-2026-0004` reached `Active` with workflow status `Completed` after create, submit, department/procurement/finance/final approvals, and publish. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore /nr:false /m:1 -p:OutDir="$PWD\artifacts\build-verify\api-workflow-designer-fix\"` | Passed | Workflow definition config mapping and role-based approval history fallback compile under .NET 8; normal Debug output remained locked by a running API process. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` | Passed | Core procurement DTO/entity/service changes compile; existing warning noise only. |
| `dotnet ef migrations add AddProcurementStrategicMarketSupplierAnalytics --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext --output-dir Migrations --configuration MigrationVerify` | Passed | Generated migration `20260608180925_AddProcurementStrategicMarketSupplierAnalytics` for market intelligence fields, plan-item market analysis link, and market price precision alignment. |
| `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore ... -p:OutDir=artifacts\build-verify\data-strategic\` | Blocked by build hang | Isolated Data project build still timed out after 4 minutes with no errors emitted; the timeout wrapper killed only the build process started for this verification. |
| Targeted ESLint for strategic procurement planning files | Passed | Market analysis, supplier consolidation, dashboard, plan item dialog, and service files lint clean. |
| Filtered frontend type-check for strategic procurement planning files | Passed | Repo-wide type-check still exits nonzero from existing unrelated issues, but filtered output contains no procurement planning/service errors. |
| Local Next route smoke check for strategic planning pages | Passed | Existing dev server on `http://localhost:3001` returned 200 for planning overview, market analysis list/new, and supplier consolidation list/new routes. |
| Targeted ESLint for master-data selectors and emergency planning routes | Passed | Market analysis form, supplier consolidation form, emergency list/form/new/detail/edit routes, and emergency service wrappers lint clean. |
| Filtered frontend type-check for emergency planning and strategic forms | Passed | Repo-wide type-check still exits nonzero from existing unrelated issues, but filtered output contains no errors for emergency planning, strategic forms, or procurement planning service. |
| Local Next route smoke check for emergency planning pages | Passed | Existing dev server on `http://localhost:3001` returned 200 for emergency list, new, detail, and edit routes, plus the updated strategic new-form routes. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core workflow recall contracts, DTOs, outcomes, and status adapters compile under .NET 8; existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore /nr:false /m:1 -p:OutDir="$PWD\artifacts\build-verify\api-workflow-recall\" -clp:ErrorsOnly` | Passed | Global workflow recall endpoint and SimpleWorkflowService recall implementation compile cleanly under .NET 8; existing warning noise only. |
| Targeted ESLint for global workflow recall files | Passed | `WorkflowApprovalActions`, `workflow-api.service`, and workflow types lint clean using the local frontend ESLint binary. |
| Filtered frontend type-check for global workflow recall files | Passed | Repo-wide type-check still exits nonzero from existing unrelated issues, but filtered output contains no errors for the changed workflow action, service, or type files. |
| Targeted ESLint for finance fiscal-year selectors and item dialog alignment | Passed | Fiscal-year selector, planning dashboard/reports, plan/budget create/edit pages, and edit-plan item dialog changes lint clean. |
| Filtered frontend type-check for finance fiscal-year selectors and item dialog alignment | Passed | Repo-wide type-check still exits nonzero from existing unrelated issues, but filtered output contains no errors for the changed procurement planning files. |
| Local Next route smoke check for fiscal-year selector and edit-plan item dialog | Passed | Dev server on `http://localhost:3001` returned 200 for plan new, budget new, reports, and edit-plan route shell. |
| Targeted ESLint for compact tabbed plan item dialogs | Passed | Plan detail and edit item dialogs lint clean after Business Partner supplier loading, Inventory UOM selectors, and Details/Suppliers tabs. |
| Filtered frontend type-check for compact tabbed plan item dialogs | Passed | Repo-wide type-check still exits nonzero from existing unrelated issues, but filtered output contains no errors for plan detail/edit dialog files. |
| Local Next route smoke check for compact tabbed plan item dialogs | Passed | Dev server on `http://localhost:3001` returned 200 for plan detail and edit route shells. |
| Targeted ESLint for search-first plan item, emergency item, and schedule filters | Passed | Shared plan item dialog body, plan detail/edit pages, emergency plan form, and schedule list lint clean. |
| Filtered frontend type-check for search-first plan item, emergency item, and schedule filters | Passed | Repo-wide type-check still exits nonzero from existing unrelated issues, but filtered output contains no errors for the changed procurement planning files. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core schedule service interface changes compile under .NET 8; existing warning noise only. |
| `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Data schedule repository date-window filtering compiles under .NET 8; existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore /nr:false /m:1 -p:OutDir="$PWD\artifacts\build-verify\api-plan-dialog-schedules-required-uom-2\" /v:minimal` | Passed | API schedule filter endpoint, required-UOM DTO validation, and changed procurement planning services compile under .NET 8; existing warning noise only. |
| Local Next route smoke check for search-first item and schedule pages | Passed | Dev server on `http://localhost:3001` returned 200 for plan detail, edit-plan, emergency-plan new, and procurement schedule route shells. |
