# Sales Management Requirement Tracker

Last updated: 2026-06-09

## Active Scope

This tracker follows the Sales Management expansion for customer sales transactions, refunds, sales agreements, contracts, configurable saleable inventory sources, plot/unit allocation, CRM handoff, workflow approvals, competitor intelligence, and future Land Management integration.

The Land Management module is not yet merged into this master repo. Until it lands, Sales must expose a stable source-adapter contract and keep a clear integration placeholder so land plots can be connected without rewriting the Sales workflows.

## Existing Repo Findings

| Area | Current state | Direction |
| --- | --- | --- |
| Sales orders | `SalesOrder`, `SalesOrderLine`, status history, API endpoints, list/create/detail pages, quote conversion, project-unit context, and lifecycle actions already exist. | Extend the existing order flow instead of creating a parallel sales process. |
| Sales agreements | `SalesAgreement` already supports lease, tenancy, plot allocation, milestones, property references, customer linkage, and project-unit context. | Reuse this as the commercial agreement layer for plot/property sales and leases. |
| Contract agreements | Procurement `Contract` exists for tender-award contracts, milestones, amendments, and documents. CRM also exposes contract views. | Marry through shared customer/partner, document, milestone, and renewal context rather than duplicating procurement contract logic. |
| Lease contracts | Finance has fixed-asset lease contract DTOs/entities for accounting treatment. | Keep finance lease accounting separate from customer-facing sales/tenancy agreements, but link when a lease agreement needs accounting recognition. |
| Project units | Projects already has released-unit sales handoff into sales orders/agreements and status sync to Reserved/Sold/Leased. | Use this as the first concrete saleable-source adapter and as the pattern for future Land Management plots. |
| CRM | `/crm` contains accounts, leads, opportunities, quotes, contracts, conversions, renewals, reports, and account detail pages. Older `/sales/crm/*` pages also exist. | Consolidate around the CRM module and connect Sales Orders/Agreements through shared links and actions. |
| Workflow | Global workflow components exist, including shared approval/recall actions. Sales Order, Sales Agreement, and Sales Allocation now have shared workflow entity registration/adapters; refund and credit note remain on the refund lifecycle slice. | Keep deeper allocation/refund workflow handling tied to those lifecycle slices rather than creating parallel approval paths. |
| Sales setup UI | Sidebar advertises Administration -> Sales setup links, and `/administration/sales` now manages active saleable source configuration. | Extend the setup page as additional source adapters become available. |

## Implementation Principles

| Principle | Notes |
| --- | --- |
| Reuse existing patterns | Follow existing API controller, service, DTO, EF migration, Next route, shadcn UI, and workflow patterns already used in Sales, Procurement, Projects, and Procurement Planning. |
| Source adapter design | Sales should not hardcode Land, Projects, Inventory, Assets, or Property Register directly into the order page. It should read active configured saleable sources and call the matching adapter. |
| No duplicate allocation | Allocation/reservation must be enforced in backend services and database indexes, not only hidden in the UI. |
| Land module pending | Add a placeholder adapter and integration notes now; complete live plot lookup/status updates after the Land Management module is merged. |
| CRM stays CRM | CRM should initiate leads, opportunities, quotes, and customer-context sales actions; Sales should own order/agreement/refund execution. |
| Workflow stays global | Use the existing workflow module and shared `WorkflowApprovalActions`, including requester recall. |

## Task List

| Status | Slice | Notes |
| --- | --- | --- |
| Done | Initial repo review and tracker creation | Reviewed current Sales, CRM, agreements, contracts, project-unit handoff, workflow mapping, and administration route gaps. |
| Done | Sales setup schema and DTOs | Added `SalesSaleableSource`, DTOs, EF mapping, and migration for source type, display name, active flag, icon/color, sort order, supported transaction types, default currency, default workflow entity, adapter key, external-module flag, system-source flag, and source settings JSON. |
| Done | Sales setup service/controller | Added `/api/sales/setup/saleable-sources` endpoints for listing, creating, updating, activating, deactivating, deleting custom sources, seeding defaults, and searching items through adapters. |
| Done | Sales setup admin page | Implemented `/administration/sales` with source stats, active/inactive filtering, create/edit dialog, enable/disable controls, seed defaults, and delete for custom sources. |
| Done | Sales navigation exposure | Added explicit sidebar child links for Sales Overview, Sales Orders, and Administration > Sales Setup, and made the main sidebar menu scrollable so lower modules are reachable on smaller screens. |
| Done | Saleable source filter/scope settings | Sales setup now exposes optional filter parameter/value controls for source adapters; Inventory sources additionally support warehouse and optional bin/location scoping without requiring administrators to hand-edit JSON. |
| Done | Seed default saleable sources | Seeded Project Units as active; Inventory, Property Register, Asset Register, and Land Management are seeded inactive placeholders, with Land marked as pending external-module integration. |
| Partial | Saleable source adapter contract | Added adapter contract and source-item search through Sales, with active allocation state hydration. Source-specific reserve/allocate/release hooks remain for Inventory, Assets, Property Register, and future Land adapters. |
| Done | Project Unit source adapter | Wrapped the existing released project-unit sales lookup behind the Sales saleable-source adapter and mapped it to generic saleable item results. |
| Done | Inventory source adapter | Inventory saleable sources now search active item, warehouse, or selected bin/location stock; apply optional setup filters such as inventory type/status/category/brand; return warehouse/location/UOM/quantity context; and prefill sales order lines with inventory, warehouse, and location IDs. |
| Partial | Asset/Property Register source adapters | Added a Finance Fixed Asset saleable-source adapter for asset-register with HeldForSale default, search/filter support, value/UOM mapping, and backend registration; Property Register remains a non-crashing placeholder until a standalone property/land register module is available. |
| Blocked | Land Management source adapter | Complete when the Land Management module is merged. It must retrieve available plots, reserve plots, allocate/sell/lease plots, release on cancellation/refund, and update ownership/allocation history. |
| Done | Dynamic Sales landing tiles | Sales landing now loads active saleable sources from setup and hides disabled sources automatically, while preserving operational sales shortcuts. |
| Done | Saleable item picker dialog | Active saleable sources open a compact searchable item picker with selection details and proceed actions for sales order, agreement, and lease creation; Inventory sources now search through their configured warehouse/location adapter instead of being blocked as unconnected. |
| Done | Sales order create refactor | Sales Order create now uses the shared saleable-source quick start, accepts source-aware query context, pre-fills the first line from the selected source item, checks active allocations, creates a linked reservation ledger row where applicable, and still links Project Units when applicable. |
| Done | Source-aware order line item picker | Sales Order line item cells now become type-to-search dropdowns as soon as a saleable source is selected, so Inventory users can search items from the configured warehouse/location directly in the order lines grid before linking a specific source item. |
| Done | Order-line picker visibility fix | Sales Order line item search results now render through a popover portal and the Order Lines tab has extra vertical room, preventing search results from being clipped inside the table/grid area. |
| Done | Sales order create tab layout | Sales Order create now keeps customer/source context visible and places Order Details and Order Lines in horizontal tabs instead of stacking both sections vertically. |
| Done | Sales agreement create refactor | Sales Agreement create now uses the shared saleable-source quick start, supports agreement/lease intent, prefills property/value/line context, checks active allocations, creates a linked reservation ledger row, and still links Project Units when applicable. |
| Done | Allocation/reservation ledger | Added `SalesAllocation` and history entities, DTOs, service, controller, frontend service wrapper, migration, and workflow display support for generic saleable-source reservations/allocations. |
| Done | Double allocation prevention | Added service-level active-allocation checks, saleable item search hydration/disablement, create-flow active checks, and a filtered unique index for active allocation statuses. |
| Done | Allocation ledger UI | Added Sales allocation list/detail routes, sidebar link, source/status/activity filters, export, detail cards, related order/agreement links, manual release/cancel/allocation actions, allocation history, and shared workflow action/history panels. |
| Done | Allocation history and transfers | Reservation/allocation status history is captured, allocation detail now supports customer/prospect transfer with old/new holder history, linked document cleanup, and an ownership-sync note; external Land/property ownership updates remain in the Land integration slice. |
| Done | Workflow entity registration | Added SalesOrder, SalesAgreement, SalesAllocation/PlotAllocation, Refund, and CreditNote to backend workflow entity defaults and the frontend Sales & CRM module filter. |
| Done | Sales workflow adapters | Added SalesOrder, SalesAgreement, SalesAllocation, CreditNote, and Refund status adapters; Sales Order, Sales Agreement, Sales Allocation, Refund, and Credit Note approval surfaces now use shared workflow actions where applicable. |
| Done | Shared workflow UI integration | Replaced local Sales Order and Sales Agreement submit/review controls with shared `WorkflowApprovalActions`, including requester recall support and workflow history panels. |
| Done | CRM to Sales handoff | Added shared CRM sales handoff actions on CRM account, opportunity, quote, and converted-lead detail surfaces; Sales Order create now preserves quote/opportunity IDs and both Sales Order/Agreement create pages show CRM-origin context with customer/value prefill. |
| Done | Sales to CRM updates | CRM account detail now surfaces Sales Order, Sales Agreement, Sales Allocation, return, credit-note, and refund milestones with direct Sales links; CRM opportunity/quote conversion chains now include linked Sales Orders. Invoice/payment milestones remain paused with the finance merge. |
| Done | Quote to order hardening | Accepted CRM quotes now convert through the Sales Order conversion endpoint with idempotent backend handling, preserved quote lines/currency/totals/reference context, a clear Convert/Open Sales Order action on CRM quote detail, and legacy `/sales/crm/*` routes redirect to the main CRM module. |
| Partial | Refund workflow completion | CreditNote and Refund now have workflow submit/approval endpoints, registered workflow adapters, shared workflow list actions, recall-compatible status rollback, and refund completion releases linked sales allocations; finance payment reversal/accounting posting remains with the finance integration slice. |
| Done | Competitor intelligence completion | Registered the Competitor service, added portfolio analytics, richer competitor/deal metrics, resilient enum handling, resolved-date exposure, list-page threat/industry dashboard signals, and profile editing for products, pricing, strengths, weaknesses, and market activity notes. |
| Paused | Finance invoice/payment integration | Paused until the finance invoice/payment/posting work from the other team is fully merged; resume later for Sales Order invoice generation, payment posting, refund reversal, and customer account balance integration. |
| Done | Reports and dashboards | Expanded the Sales dashboard summary with allocation source/status metrics, sold/leased counts, reservation exposure, agreement expiry watch, refund exposure, CRM conversion/pipeline indicators, and competitor open-deal/win-rate/high-threat signals. |
| Pending | Land Management integration after merge | Wire Land plot lookup, plot status updates, allocation history, ownership history, and GIS/cadastral references into the saleable-source adapter after the module is available. |

## Immediate Next Slices

| Priority | Slice | Outcome |
| --- | --- | --- |
| 1 | Property Register live integration | Replace the safe placeholder adapter with real property-register lookup once that module's entity ownership and lifecycle rules are confirmed. |
| 2 | Land Management integration readiness | Keep the Land adapter contract and status-sync placeholders aligned until the external Land module lands. |
| 3 | Finance invoice/payment integration | Resume only after the finance invoice/payment/posting merge is complete. |
| 4 | Final sales smoke/lifecycle verification | Apply pending Sales migrations and run a source-aware sale through setup, selection, order/agreement, allocation, workflow, and recall/transfer paths when the local API/UI are stable. |
| 5 | Documentation cleanup | Reconcile pending/blocked Sales integration notes after Property Register, Land Management, and Finance merge status is confirmed. |

## Land Management Integration Note

When the Land Management module is merged, complete these specific checks before enabling the Land source:

| Check | Requirement |
| --- | --- |
| Available plot query | Sales must read only plots that Land marks Available or otherwise saleable. |
| Reservation | Sales reservation must update Land plot status to Reserved and persist reserved-until and customer/prospect context. |
| Allocation/sale/lease | Final approval must update Land status to Allocated, Sold, or Leased as appropriate. |
| Cancellation/refund | Rejected, cancelled, expired, or refunded transactions must release or roll back plot status according to Land rules. |
| Ownership history | Sold/allocated plots must create/update Land ownership/allocation history. |
| Double allocation | Land and Sales must enforce the same active allocation uniqueness rule. |
| GIS/cadastral context | Sales item picker should show plot number, block, size, cadastral reference, location, and status when available. |

## Verification Log

| Check | Status | Result |
| --- | --- | --- |
| Repo inspection for Sales/CRM/agreement/contract/workflow surfaces | Passed | Confirmed existing SalesOrder, SalesAgreement, project-unit handoff, contract, CRM, and workflow surfaces plus missing Administration/Sales routes. |
| Tracker creation | Passed | Created `docs/sales-management-tasklist.md`. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core Sales setup DTOs, service, adapter contract, and Project Unit adapter compile; existing warning noise only. |
| `dotnet ef migrations add AddSalesSaleableSourceSetup --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext --output-dir Migrations --configuration MigrationVerify` | Passed | Generated `20260609054619_AddSalesSaleableSourceSetup` for `SalesSaleableSources` and indexes. |
| `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Data project and Sales setup migration compile; existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore /nr:false /m:1 -p:OutDir="$PWD\artifacts\build-verify\api-sales-setup\" -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Sales setup controller, DI registration, and adapter endpoint compile; existing warning noise only. |
| Targeted ESLint for Sales setup files | Passed | `salesSetupService`, `/administration/sales`, and `/sales` lint clean using the local frontend ESLint binary. |
| Filtered frontend type-check for Sales setup files | Passed | Repo-wide type-check still has unrelated existing issues, but filtered output contains no errors for the Sales setup service or pages. |
| Local Next route smoke check for Sales setup pages | Passed | Existing dev server on `http://localhost:3001` returned 200 for `/sales` and `/administration/sales`. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core Sales workflow adapters and Sales Order workflow service wiring compile; existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --no-dependencies /nr:false /m:1 -p:OutDir="$PWD\artifacts\build-verify\api-sales-workflow-shared\" -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | API Sales Agreement workflow service wiring, workflow entity defaults, and DI registrations compile; shared compiler path used after isolated compiler runs hung without errors. |
| Targeted ESLint for Sales workflow files | Passed | Sales Order detail, Sales Agreement detail, and workflow entity mapping lint clean. |
| Filtered frontend type-check for Sales workflow files | Passed | Repo-wide type-check still exits nonzero from unrelated existing issues, but filtered output contains no errors for the changed Sales workflow files. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core saleable-source quick-start DTO additions, allocation service/contracts/entities, and SalesAllocation workflow adapter compile; existing warning noise only. |
| `dotnet ef migrations add AddSalesAllocationReservationLedger --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext --output-dir Migrations --no-build` | Passed | Generated official migration `20260609110243_AddSalesAllocationReservationLedger`; allocation money fields were corrected to `decimal(18,2)` after the repo's late decimal convention initially scaffolded them as `decimal(18,4)`. |
| `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Data project, allocation EF configuration, migration, and updated model snapshot compile; existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | API allocation controller, DI registration, workflow defaults, and startup output compile; existing warning noise only. |
| `dotnet ef migrations list --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext --no-build` | Passed | Shows `20260609110243_AddSalesAllocationReservationLedger (Pending)`. |
| Targeted ESLint for saleable-source refactor and allocation files | Passed | Shared quick-start component, Sales create pages, Sales landing, Sales setup service, allocation service, and workflow mapping lint clean. |
| Filtered frontend type-check for saleable-source refactor and allocation files | Passed | Repo-wide type-check still exits nonzero from unrelated existing issues, but filtered output contains no errors for the changed Sales source/allocation files. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core Inventory saleable-source adapter, source settings DTO exposure, and Sales order source-context changes compile; existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --no-dependencies ... -p:OutDir=artifacts\build-verify\api-sales-inventory-source-nodeps\` | Passed | API DI registration for the Inventory saleable-source adapter compiles; broader isolated API build attempt timed out without emitted errors before this narrower verification passed. |
| Targeted ESLint for Inventory saleable-source files | Passed | Sales setup page, shared quick-start component, Sales order create page, Sales setup/order services, and inventory warehouse service lint clean when run per file. |
| Filtered frontend type-check for Inventory saleable-source files | Passed | Repo-wide type-check still exits nonzero from unrelated existing issues, but filtered output contains no errors for the changed Sales setup/source/order files. |
| Local Next route smoke check for Sales inventory source pages | Passed | Existing dev server on `http://localhost:3001` returned 200 for `/sales`, `/sales/orders/create`, and `/administration/sales`. |
| Sidebar Sales navigation verification | Passed | `sidebar.tsx` now exposes Sales Overview, Sales Orders, and Administration > Sales Setup as child links; targeted ESLint passed and local route checks returned 200 for `/sales/orders` and `/administration/sales`. |
| Targeted ESLint for source-aware Sales item picking | Passed | Sales landing page and Sales Order create page lint clean after enabling generic saleable-source search and source-aware order-line dropdowns. |
| Filtered frontend type-check for source-aware Sales item picking | Passed | Repo-wide type-check still exits nonzero from unrelated existing issues, but filtered output contains no errors for `sales/page.tsx` or `sales/orders/create/page.tsx`. |
| Local Next route smoke check for source-aware Sales item picking | Passed | Existing dev server on `http://localhost:3001` returned 200 for `/sales` and `/sales/orders/create`. |
| Sales Order create tab layout verification | Passed | Targeted ESLint passed for `sales/orders/create/page.tsx`; filtered type-check output contains no errors for that page; local route check returned 200 for `/sales/orders/create`. |
| Inventory line-item picker correction | Passed | `SaleableSourceQuickStart` now passes the selected saleable source into Sales Order create, so the Order Lines Item Name cell can type-search Inventory items from the source warehouse/location before a specific item is linked; targeted ESLint passed, filtered type-check showed no changed-file errors, and `/sales/orders/create` returned 200. |
| Order-line picker visibility verification | Passed | Sales Order create now renders item search results with a portaled popover anchored to the editable item input instead of wrapping the input as a popover trigger; targeted ESLint passed, filtered type-check showed no changed-file errors, and `/sales/orders/create` returned 200. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core Sales allocation workflow DTO/service additions compile with zero warnings/errors in the targeted build. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --no-dependencies ... -p:OutDir=artifacts\build-verify\api-sales-allocation-ui\` | Passed | Sales allocation submit/approve controller endpoints compile through isolated API output; existing warning noise only. |
| Targeted ESLint for Sales allocation UI | Passed | Allocation list/detail pages, allocation frontend service, and sidebar link lint clean. |
| Filtered frontend type-check for Sales allocation UI | Passed | Repo-wide type-check still exits nonzero from unrelated existing issues, but filtered output contains no errors for allocation pages, allocation service, or sidebar changes. |
| Local Next route smoke check for Sales allocation UI | Blocked by dev-server hang | Existing `http://localhost:3001` dev server stopped responding for both existing Sales pages and the new `/sales/allocations` route during verification, so route status could not be trusted in this run. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core build re-verified after CRM handoff frontend work; zero warnings/errors in the targeted build. |
| Targeted ESLint for CRM to Sales handoff | Passed | Shared CRM handoff component, CRM account/opportunity/quote/lead pages, Sales create pages, saleable-source parser, and Sales Order service lint clean. |
| Filtered frontend type-check for CRM to Sales handoff | Passed | Repo-wide type-check still exits nonzero from unrelated legacy `/sales/crm/*` skeleton pages, but filtered output contains no errors for the changed CRM/Sales handoff files. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core refund/credit-note workflow DTOs, service methods, status adapters, and allocation release logic compile; existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --no-dependencies ... -p:OutDir=artifacts\build-verify\api-sales-refund-workflow\` | Passed | API refund/credit-note workflow endpoints and adapter DI registrations compile through isolated output; existing warning noise only. |
| Targeted ESLint for refund workflow files | Passed | Refund list, credit-note list, and return-order frontend service lint clean after switching approval actions to shared workflow controls. |
| Filtered frontend type-check for refund workflow files | Passed | Repo-wide type-check still exits nonzero from unrelated existing issues, but filtered output contains no errors for the changed refund/credit-note workflow files. |
| `dotnet build-server shutdown` | Passed | Build servers were reset after an initial silent build hang; subsequent targeted .NET builds completed normally. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore --no-dependencies /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core CRM/Sales milestone projection DTOs and conversion-chain changes compile with zero warnings/errors. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --no-dependencies ... -p:OutDir=artifacts\build-verify\api-sales-crm-updates\` | Passed | API compile verifies CRM service projection changes through isolated output after build-server reset. |
| Targeted ESLint for Sales-to-CRM update files | Passed | CRM service types and CRM account/opportunity/quote pages lint clean from the `frontend` workspace. |
| Filtered frontend type-check for Sales-to-CRM update files | Passed | Repo-wide type-check still exits nonzero from unrelated legacy `/sales/crm/*` skeleton pages, but filtered output contains no errors for the changed `/crm/*` pages or `crmService.ts`. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore --no-dependencies /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core quote-to-order conversion hardening compiles; existing repo warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --no-dependencies ... -p:OutDir=artifacts\build-verify\api-sales-quote-order\` | Passed | API quote conversion endpoint wiring compiles through isolated output; existing repo warning noise only. |
| Targeted ESLint for quote-to-order hardening files | Passed | CRM quote page, shared CRM sales handoff component, legacy `/sales/crm/*` redirects, and Sales Order frontend service lint clean. |
| Filtered frontend type-check for quote-to-order hardening files | Passed | Repo-wide type-check produced no errors for the changed quote-to-order files after legacy `/sales/crm/*` pages were redirected. |
| Local Next route smoke check for CRM quote routes | Blocked by dev-server timeout | Existing `http://localhost:3001` did not respond within 10 seconds for `/crm/quotes` or legacy `/sales/crm/*` routes during this run, so route status could not be trusted. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore --no-dependencies /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core asset/property saleable-source adapters compile; zero warnings/errors in the targeted adapter check. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --no-dependencies ... -p:OutDir=artifacts\build-verify\api-sales-asset-adapters\` | Passed | API DI registration for the asset-register and property-register saleable-source adapters compiles through isolated output; existing warning noise only. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore --no-dependencies /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core allocation transfer DTO/service/history additions compile; existing repo warning noise only. |
| `dotnet build-server shutdown` | Passed | Build servers were reset after a full API build hung without emitted errors. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore ... -p:OutDir=artifacts\build-verify\api-sales-allocation-transfer\` | Passed | API allocation transfer endpoint compiles with project references included after the build-server reset; existing warning noise only. |
| Targeted ESLint for Sales allocation transfer files | Passed | Allocation detail page and Sales allocation frontend service lint clean after adding the transfer dialog/action. |
| Filtered frontend type-check for Sales allocation transfer files | Passed | Repo-wide type-check still exits nonzero from unrelated existing issues, but filtered output contains no errors for allocation transfer files. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore --no-dependencies /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core competitor analytics DTOs, service mapping, resilient enum parsing, and resolved-date exposure compile; existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --no-dependencies /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | API Competitor analytics route and DI registration compile through a narrower no-dependency build after the full build hung without emitted errors. |
| Targeted ESLint for competitor intelligence files | Passed | Competitor list/detail pages and frontend competitor service lint clean after analytics/profile-edit updates. |
| Filtered frontend type-check for competitor intelligence files | Passed | Repo-wide type-check still exits nonzero from unrelated existing issues, but filtered output contains no errors for the changed competitor files. |
| Local Next route smoke check for competitor pages | Blocked by dev-server timeout | Existing `http://localhost:3001` did not respond within 10 seconds for `/sales/competitors` or `/sales/allocations`, so route status could not be trusted in this run. |
| `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore --no-dependencies /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | Core Sales reporting summary additions for allocations, agreements, refunds, and competitors compile; existing warning noise only. |
| `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --no-dependencies /nr:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -clp:ErrorsOnly` | Passed | API Sales reporting DI registration and summary endpoint compile through the narrowed API check; existing warning noise only. |
| Targeted ESLint for Sales reporting dashboard files | Passed | Sales reports page, sales reporting service, competitor pages, and competitor service lint clean. |
| Filtered frontend type-check for Sales reporting dashboard files | Passed | Repo-wide type-check still exits nonzero from unrelated existing issues, but filtered output contains no errors for the changed reporting/competitor files. |
| Local Next route smoke check for Sales reports | Blocked by dev-server timeout | Existing `http://localhost:3001` did not respond within 10 seconds for `/sales/reports`, so route status could not be trusted in this run. |
