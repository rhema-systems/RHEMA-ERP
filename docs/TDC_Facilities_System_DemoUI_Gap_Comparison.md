# TDC Facilities Requirements vs What We Have vs Gaps

**Date:** 14 July 2026  
**Purpose:** Compare the Facilities Management ERP module and BRS documents against the combined solution baseline: current RHEMA ERP plus DemoUI items intended to be brought into the ERP, before producing the final module-owner requirements document.

## Source Documents Reviewed

| Source document | Main process covered |
|---|---|
| `FACILITIES MANAGEMENT ERP MODULE.docx` | Facilities module purpose, user roles, property/site management, lease management, maintenance, complaints, service providers, staff/cleaners, inventory, finance, budget, asset register, document management, reporting, dashboard/search/notifications and recommended enhancements. |
| `Facilities_Management_ERP_Business_Requirement_Specification.docx` | BRS for user management, property/site, lease, maintenance, complaint, service provider, staff/cleaner, inventory, financial, budget, asset, document, reporting and non-functional requirements. |

## Status Legend

| Status | Meaning |
|---|---|
| Available | The combined baseline already has a usable module, entity, API, or page for this area. |
| Partial | The system has a foundation, but it does not yet meet the Facilities requirement end-to-end. |
| Reference only | A UI or related feature exists, but it is not yet a complete Facilities operational workflow. |
| To add | Required for Facilities, but not currently implemented as a complete usable feature. |

## Key Finding

Facilities has a strong baseline. The ERP already includes a Facilities procedure catalogue, Facilities landing/workspace pages, live `ProcedureCase` support for the Facilities module, Maintenance work orders, maintenance assets, inspections, schedules, contractors, invoices, performance metrics, expenses, Inventory, Finance AR/AP/Budgeting, Fixed Assets, Helpdesk, document attachments, workflow administration, notifications and audit foundations.

The main gap is orchestration. Facilities requirements describe one integrated operating module that connects tenant/client intake, property/unit records, leases, billing/payment confirmation, maintenance, complaints, service providers, cleaners, inventory, budgets, assets, documents and reporting. The current baseline has many of the pieces, but Facilities still needs stronger end-to-end integrations, dashboards, alerts, self-service, SLA controls, document/template rules, financial handoffs and operational reports.

## Baseline Position

For this comparison, current ERP and DemoUI are treated together as what we have because DemoUI items are expected to be brought into the ERP.

Facilities has live application coverage through:

- `frontend/src/app/estate/facilities/page.tsx`
- `frontend/src/app/estate/facilities/[entityType]/page.tsx`
- `frontend/src/services/estate-facilities.service.ts`
- `src/ErpSystem.Core/Services/Estate/FacilitiesProcedureCatalogService.cs`
- `src/ErpSystem.Api/Controllers/Estate/FacilitiesProceduresController.cs`

Facilities cases are supported by the shared `ProcedureCase` engine with module value `Facilities`. Related operational capabilities exist in Maintenance, Helpdesk, Inventory, Finance, Fixed Assets, Estate Managed Assets and Business Partners.

## Baseline Components Relevant to Facilities

| Component | Coverage |
|---|---|
| Facilities procedure catalogue | Defines eight Facilities procedure areas: Property and Site Management, Lease Management, Maintenance Management, Complaint Management, Service Provider Management, Staff and Cleaner Management, Asset Register, and Facilities Document Control. |
| Facilities frontend pages | Show Facilities procedure cards and open Facilities workspaces with stages, handoffs, intake fields, required documents and outputs. |
| `ProcedureCase` engine | Persists Facilities cases, fields, checklist items, required documents, uploaded documents, activity history, stage completion and workflow linkage. |
| Maintenance module | Provides maintenance assets, work orders, schedules, inspections, quality control, attachments, technicians, contractors, contractor invoices, performance reviews, expenses, dashboards, analytics, mobile/fleet features and reports. |
| Estate managed assets | Holds property/facility records, location, purpose, zoning, status, lease/sale availability, documents and project handoff information. |
| Helpdesk / EHC | Provides complaint/request/ticket handling, queues, SLA views, escalation, internal/external pages and dashboards. |
| Inventory module | Provides inventory items, categories, stock movements, stock adjustments, transfers, requisitions, locations, minimum/reorder levels and valuation. |
| Finance AR/AP/Budgeting | Provides customer/vendor financial foundations, invoices/payments/payment terms, accounts payable, budgeting and finance reporting pages. |
| Fixed Assets / Lease Accounting | Provides fixed asset register, asset categories, verification, depreciation, lease contracts and lease schedules. |
| Business Partners | Provides customer, supplier, contractor and business partner master data foundations. |
| Notification and audit foundations | Support in-app/email/SMS-related settings, activity logs, audit fields and workflow notifications across modules. |

## Comparison Matrix

| TDC Facilities requirement area | What we have | Status | What we still need to add |
|---|---|---|---|
| Facilities procedure catalogue | Facilities catalogue exists for property/site, lease, maintenance, complaints, providers, staff/cleaners, asset register and document control. Facilities landing page lists cards and opens procedure workspaces. | Available | Confirm final Facilities process names, stage counts, role labels and whether financial/budget/inventory/reporting should become separate Facilities procedure cards. |
| Live Facilities case workspace | `ProcedureCase` supports module `Facilities`; Facilities workspace pages embed `ProcedureCaseWorkspace` and seed fields, documents, checklists and stages from the catalogue. | Available / Partial | Add Facilities-specific dashboards, SLA ageing, approval queues, operational reports and deeper links to Maintenance, Finance, Inventory and Helpdesk records. |
| User management and role access | ERP has authentication, role-based access, user profile foundations and workflow/user audit support. Facilities procedure roles include manager, supervisor, officer, provider, document control and finance handoffs. | Partial | Add Facilities-specific permission matrix for manager, supervisor, officer, finance officer, contractor/service provider, cleaner/staff, document control and tenant/customer portal users. |
| Property and site management | Facilities procedure workspace exists. EstateManagedAsset stores property/facility records, locations, purpose, status, area, block/floor, project/unit links, lease/sale availability and documents. Maintenance also has site and asset pages. | Partial | Add Facilities-specific site hierarchy: site, building, floor, unit, space, common area, occupancy capacity, responsible officer, availability dashboard and site-level operating documents. |
| Unit/space occupancy and availability | EstateManagedAsset has status, block/floor, project unit linkage and lease availability flags. Facilities Property/Site workspace has occupancy validation stages. | Partial | Add operational occupancy register with current tenant/client, occupancy date, vacant/occupied/under maintenance status, viewing history, allocation status and availability reporting. |
| Client/customer/tenant database | Business Partner and finance/customer foundations exist. Procedure cases capture applicant/requester fields. Helpdesk captures requester details. | Partial | Add Facilities tenant/client profile linked to property/unit, lease, invoices, complaints, documents, contacts, portal user account and activity history. |
| Lease management | Facilities Lease workspace exists. Finance Fixed Assets has lease accounting. Sales/agreements and Legal foundations exist elsewhere. | Partial | Add operational lease lifecycle: prospective client, viewing, proforma, draft lease, e-signature, payment confirmation, allocation, renewal/expiry reminders, termination, rent review and lease reports. |
| Electronic signing | The BRS requires e-signing. No dedicated e-sign workflow was found in Facilities baseline. Documents can be uploaded and tracked. | To add | Add e-signature integration or internal signature workflow for lease agreements, variations, renewal letters and acknowledgements, including signed-copy storage. |
| Lease expiry, renewal and billing reminders | Facilities Lease workspace includes alert configuration stages. Notification foundations exist. Finance/payment terms exist. | Partial | Implement automated lease expiry reminders, renewal notices, rent review alerts, billing reminders, escalation rules and dashboard widgets. |
| Proforma invoice and client billing | Finance AR/invoice/payment foundations exist; project billing has invoice request patterns; Facilities Lease workflow mentions finance handoff. | Partial | Add direct Facilities billing integration for proforma invoices, lease/service charges, invoice dispatch, payment confirmation, receipt references and automatic unit allocation after payment. |
| Maintenance request management | Facilities Maintenance workspace exists. Maintenance module has work orders, tasks, parts, labor, comments, attachments, scheduling, inspections, quality checks and mobile support. | Available / Partial | Link Facilities maintenance cases directly to Maintenance WorkOrder records with property/unit/tenant/service-provider context, SLA targets and closure feedback. |
| Preventive maintenance | Maintenance schedules, assets, triggers, inspections and dashboards exist. | Available / Partial | Add Facilities preventive maintenance plans for AC units, generators, building systems, elevators, common areas and site-specific equipment, with Facilities reporting and reminders. |
| Corrective maintenance | Work orders support issue description, priority, assignment, parts, labor, completion, quality control and closure. | Available / Partial | Ensure Facilities corrective categories align to electrical, plumbing, civil works, repairs and building systems; add customer/tenant feedback and site/unit cost attribution. |
| Maintenance inspection and closure | Maintenance inspections and quality-control workflows exist. Facilities Maintenance workspace includes inspect completion and close stages. | Available / Partial | Add Facilities acceptance checklist, requester satisfaction confirmation, rework rules, closure evidence and update of asset/property history. |
| Complaint management | Facilities Complaint workspace exists. Helpdesk/EHC provides tickets, internal/external queues, SLA, escalation, dashboards and request pages. | Partial | Decide whether Facilities complaints use ProcedureCase, Helpdesk, or a linked model; integrate complaint number, SLA, escalation, investigation, resolution, customer feedback and closure history. |
| Tenant/customer portal | Helpdesk has external complaint/support pages and business partner portal foundations. Facilities documents require tenant complaint, invoice, document upload and e-sign interactions. | Partial | Add Facilities tenant/customer portal views for complaints, lease documents, invoices, payment status, document upload, e-signing and service request tracking. |
| Service provider / contractor management | Facilities Service Provider workspace exists. Maintenance has contractor profiles, capabilities, service areas, status, rating, invoices, work orders, performance metrics and reviews. Business Partners and procurement contractor specialization also exist. | Available / Partial | Connect Facilities provider records to Maintenance contractors/Business Partners, contract documents, rates, insurance/license expiry, provider portal access, invoice approval and performance scorecards. |
| Contractor invoice processing | Maintenance contractor invoices support invoice number, due date, amount, tax, status, approval and paid dates. Finance AP supports vendor invoices and payments. | Partial | Add Facilities-to-AP handoff: verify work, approve invoice, match work order/contract rate, send to AP, update payment status and report outstanding contractor invoices. |
| Vendor performance ratings | Maintenance contractor performance metrics and reviews exist, including quality, completion, response time, cost variance, safety/compliance and rating. | Available / Partial | Expose Facilities-specific vendor scorecards by site, service category, SLA, complaint recurrence and renewal decision. |
| Staff and cleaner management | Facilities Staff/Cleaner workspace exists. Maintenance has technician scheduling and staff-related maintenance structures. HR/employee foundations exist. | Partial | Add cleaner database, assigned site/zone, attendance, duty roster, work status, supervisor inspection, absence exceptions and performance reports. |
| Inventory management | Inventory module supports items, stock, available stock, allocated stock, minimum/reorder levels, suppliers, stock movements, adjustments, transfers and requisitions. Maintenance inventory service is registered. | Partial | Add Facilities inventory categories for cleaning materials, consumables and maintenance supplies, issue-to-site/work-order flow, low-stock alerts and inventory status reports. |
| Low inventory alerts | Inventory minimum/reorder levels exist. Notification foundations exist. | Partial | Add Facilities-specific low-stock notification rules, reorder approvals, responsible officer and dashboard alerts. |
| Financial management | Finance AR/AP, payments, taxes, payment terms, cash, reports and budgeting modules exist. Facilities workflows mention billing, payment confirmation and contractor invoice handoff. | Partial | Add Facilities financial sub-ledger/reporting view: lease invoices, service charges, payments, arrears, contractor payments, taxes, expenses and revenue by property/site. |
| Budget and expense management | Finance budgeting exists. Maintenance expense service and MaintenanceExpense entity exist. Facilities documents require budget year, department, planned amount, actual expense and variance. | Partial | Add Facilities budget lines by site/property/service category, committed spend, actuals, variance dashboards and approval workflow for budget exceptions. |
| Asset register | Facilities Asset Register workspace exists. Maintenance assets support asset number, location, purchase date/price, warranty, status, category, condition, inspections and work-order history. Fixed Assets also has asset register/accounting. | Available / Partial | Decide system of record between Facilities Asset Register, MaintenanceAsset and FixedAsset; add cross-links, QR labels, warranty alerts, custody, transfer/disposal and maintenance history rollups. |
| QR asset tracking | Maintenance assets page includes QR code generation support. | Partial | Standardize QR labels for Facilities assets/spaces and link scans to mobile asset profile, maintenance history, warranty and service request creation. |
| Document management | Facilities Document Control workspace exists. ProcedureCase documents, Estate asset documents, Maintenance work order documents, contractor invoice attachments and general upload patterns exist. | Available / Partial | Add Facilities document taxonomy, retention/access rules, secure search, document expiry alerts, scanning workflow and linked storage for leases, certificates, contracts, invoices, payment records and property files. |
| Document expiry alerts | Facilities Document Control includes expiry/renewal alert outputs. Notification foundations exist. | Partial | Implement expiry reminders for contracts, insurance, licenses, certificates, warranties and lease documents with owner/escalation rules. |
| Reporting and dashboard | Facilities workspaces exist. Maintenance dashboards/reports/analytics, Helpdesk dashboards, Finance reports and Inventory reports exist. | Partial | Add integrated Facilities dashboard and report pack: lease, occupancy, maintenance, complaints, contractor, financial, inventory, budget, asset, SLA and management decision reports with PDF/Excel/CSV export. |
| Search | The documents require search by client, property, invoice, complaint and asset. Individual modules have search/filter pages. | Partial | Add a Facilities global search across tenant/client, property/unit, invoice, complaint, work order, asset, provider and document records. |
| Notifications and reminders | Notification/email/SMS settings and workflow notifications exist. Maintenance and helpdesk have reminder/escalation concepts. | Partial | Configure Facilities notification rules for lease expiry, billing dates, maintenance deadlines, low inventory, pending approvals, complaints and document expiry. |
| SLA monitoring | Helpdesk has SLA views; Maintenance has scheduling and overdue/priority data; Facilities cases have stages. | Partial | Add Facilities SLA policies for complaints, maintenance requests, contractor response, lease processing, document review and invoice approval. |
| Mobile responsive access | Frontend app is web-based and has mobile/fleet/inspection support in Maintenance. | Partial | Confirm Facilities mobile workflows for tenants, service providers, inspectors, cleaners and managers; add mobile-first screens where needed. |
| Audit trail | ProcedureCase activities, workflow history, tenant audit fields and helpdesk/maintenance histories exist. | Partial | Ensure Facilities field-level audit for lease changes, billing/payment confirmation, occupancy status, complaint resolution, work-order closure, invoice approval and document access. |
| Finance/accounting integration | Finance modules exist and maintenance contractor invoices/expenses exist, but Facilities procedures are not fully posted to Finance. | Partial | Define source of truth and automated handoffs between Facilities, AR, AP, budgeting, cash receipts, taxes and general ledger reporting. |
| Approval workflow | Workflow administration and ProcedureCase workflow linkage exist. Facilities stages include manager/supervisor approvals. | Partial | Configure Facilities approval matrices for leases, payments, contractor invoices, budget exceptions, maintenance thresholds, provider approvals, document access and asset disposals. |

## What We Should Say in the Requirements Document

### Already Available

- Facilities procedure catalogue and live Facilities workspace pages.
- ProcedureCase engine for Facilities case creation, stages, checklists, fields, documents and activity history.
- Maintenance work orders, schedules, inspections, quality checks, assets, contractors, contractor invoices, expenses, dashboards and reports.
- Estate managed assets for property/facility records and documents.
- Helpdesk/EHC complaint, SLA, escalation and portal-style support foundations.
- Inventory item, stock movement, reorder level, transfer, requisition and valuation foundations.
- Finance AR/AP, payment terms, budgeting, fixed assets and lease accounting foundations.
- Business Partner master data for customers, suppliers and contractors.
- Notification, audit and workflow foundations.

### Partially Available

- Facilities workflows are represented, but many outputs are still generic cases rather than integrated operational records.
- Maintenance has mature work order capabilities, but Facilities cases need direct linking to work orders, tenants, units, providers and finance.
- Finance exists, but Facilities billing, payment confirmation, service charges, contractor invoice approval and budget variance are not yet one Facilities workflow.
- Inventory exists, but Facilities low-stock/reorder/issue-to-site controls need configuration.
- Helpdesk exists, but Facilities must decide whether complaints live in Helpdesk, ProcedureCase, or a linked complaint model.
- Asset register capability exists in both Maintenance and Fixed Assets, but Facilities needs a clear system-of-record decision.

### To Add

- End-to-end Facilities operating dashboard.
- Facilities-specific property/site/unit/space hierarchy and occupancy register.
- Tenant/client profile linked to lease, unit, invoices, complaints, documents and portal access.
- Lease lifecycle with proforma, e-signature, payment confirmation, allocation, renewal, termination and reminders.
- Facilities billing, payment confirmation and arrears/service charge integration with Finance.
- Direct Facilities case to Maintenance WorkOrder integration.
- Facilities complaint SLA/escalation model and customer feedback capture.
- Service provider portal/access, contract/rate management, compliance expiry and invoice-to-AP handoff.
- Cleaner/staff duty roster, attendance and supervision reporting.
- Facilities inventory categories, issue history, low-stock alerts and reorder approvals.
- Budget vs actual reporting by site, property and service category.
- Facilities document taxonomy, secure access, expiry reminders and scanned-file migration plan.
- Integrated reporting pack with PDF/Excel/CSV export.
- Facilities global search and mobile-friendly user journeys.
- Approval matrix and audit trail controls for all sensitive Facilities actions.

## Recommended Final Facilities Requirements Document Structure

1. Module overview and Facilities scope.
2. Source documents reviewed.
3. Existing baseline coverage.
4. User roles, responsibilities and access matrix.
5. Functional requirements by module area.
6. Property/site/unit/space master data.
7. Lease, billing and tenant portal requirements.
8. Maintenance, complaint and service provider workflows.
9. Staff/cleaner, inventory, budget and asset requirements.
10. Document management, templates, alerts and retention.
11. Integrations with Maintenance, Helpdesk, Finance, Inventory, Fixed Assets, Estate, Legal and Business Partners.
12. Reports, dashboards and exports.
13. Gaps and implementation priorities.
14. Open clarifications for TDC Facilities.

## Open Clarifications for TDC Facilities

1. Should Facilities sit under Estate, or should it be a standalone top-level module?
2. Should Facilities complaints be managed in Helpdesk/EHC, ProcedureCase, or both through linked records?
3. What is the official property/site/unit/space hierarchy for TDC facilities?
4. Which lease documents require e-signature and which require manual upload only?
5. Which billing lines are in scope: rent, service charge, utilities, deposits, penalties, maintenance recharge, CAM or others?
6. What payment confirmation source is authoritative: Finance AR, cashier receipt, bank statement, or manual confirmation?
7. Which maintenance thresholds require Facilities Manager, Finance, Procurement or MD approval?
8. Should service providers access a portal to update jobs and upload invoices?
9. How should cleaners/staff attendance be captured: manual entry, mobile check-in, QR/site scan or HR integration?
10. Which reports are required monthly by Facilities Manager, Finance, Estate, MD and Audit?
