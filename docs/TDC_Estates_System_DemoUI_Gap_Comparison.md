# TDC Estates Requirements vs What We Have vs Gaps

**Date:** 14 July 2026  
**Purpose:** Compare the TDC Estates questionnaire/SOP requirements against the combined solution baseline: current RHEMA ERP plus DemoUI items intended to be brought into the ERP, before producing the final module-owner requirements document.

## Status Legend

| Status | Meaning |
|---|---|
| Available | The combined baseline already has a usable module, entity, API, or page for this area. |
| Partial | The system has a foundation, but it does not yet meet the TDC process end-to-end. |
| Reference only | Route/stage definitions or UI references exist, but the underlying operational screen or workflow is not fully implemented in this repo. |
| To add | Required for TDC, but not currently implemented as a usable feature. |

## Key Finding

The current system is not starting from zero. It already has a strong land acquisition workflow, an estate managed asset/land bank, generic procedure cases for facilities workspaces, maintenance work orders, sales allocation, finance AR, payment plans, document upload, audit fields, and workflow administration.

The biggest gap is that TDC Estates needs these pieces connected into a department-specific operating module: property/customer registers, TDC letter templates, ground rent and arrears controls, HOS/rental workflows, facilities cases/work orders, Revenue/Finance integration, reporting packs, and clear approval/control rules.

## Baseline Position

For this comparison, current ERP and DemoUI are treated together as what we have because DemoUI items are expected to be brought into the ERP.


The repository contains `DemoUiRoute` values for land acquisition stages, for example `/LandParcel/ParcelIdentificationView`, `/LandParcel/CadastralSurvey`, `/LandParcel/OwnershipVerification`, `/LandParcel/RegistrationStage`, and `/LandParcel/AssetCreation`. These are referenced in:

- `src/ErpSystem.Api/Controllers/Estate/LandAcquisitionsController.cs`
- `frontend/src/services/estate-acquisition.service.ts`

However, the actual `LandParcel` DemoUI screens are not present in this repo. Therefore, DemoUI should be used as a stage/screen reference, not as implemented functionality unless a separate DemoUI project is provided.

## Comparison Matrix

| TDC requirement area | What we have | Status | What we still need to add |
|---|---|---|---|
| Estate procedure catalogue | Estate procedure list exists for registry, records, inspections, searches, record amendments, certified copies, joint ownership, transfers, assignments, lease preparation, lease renewal, serviced plots/HOS allocation, lands, housing/HOS, traditional lands, regularisation, reporting and controls. No separate DemoUI found for these estate SOP processes. Good catalogue based on TDC manual. | Partial | Convert Estate procedure catalogue into live cases like Facilities, with saved fields, documents, checklists, stage submission, and reports. |
| Land acquisition workflow | Full backend/frontend workflow exists with 16 stages, required fields, documents, workflow actions, approvals, stage summaries, and asset creation. DemoUI route references exist for each stage. Parcel identification through asset creation is already modelled. | Available | Confirm TDC-specific acquisition outside Tema, compensation, stakeholder engagement, and Board approval rules. |
| Cadastral survey and boundary capture | Cadastral survey entity and land management map/boundary fields exist. DemoUI route reference exists for cadastral survey and verification screens. Survey plan number, map sheet, beacon count, boundary coordinates, verification, and map panel. | Available | Confirm LI 1444 validation and exact survey document checklist. |
| Ownership history / due diligence | Ownership history, ownership classification, ownership verification, title search, identity checks, authority to sell, risk level, witnesses and notes exist in land acquisition. DemoUI route references exist. Strong acquisition due-diligence foundation. | Available | Add TDC-specific duplicate claim, dispute, compensation, traditional council, and regularization checks where needed. |
| Land bank / managed asset register | EstateManagedAsset exists for land/property/facility with code, location, purpose, zoning, survey data, status, lease/sale availability, documents, and project handoff. No separate DemoUI found beyond acquisition asset creation route. Land bank and manual existing-land entry exist. | Partial | Extend fields for TDC Estate Register: DOT/right of entry, lease term, ground rent, acreage/plot size, lessee details, rent/HOS status, property file reference. |
| Property/unit inventory | EstateManagedAsset can represent property and facility assets; project units can be published to estate assets. No separate DemoUI source found. Basic property/facility inventory capability. | Partial | Add TDC-specific unit/block/site structures, rental units, HOS units, TDC Towers spaces, Site 3, Community 26, common areas, meters, parking, open spaces. |
| Customer/tenant/lessee records | Finance AR Customer and Sales/BusinessPartner links exist. Procedure cases can store applicant name. No DemoUI source found. Customer master and business partner entities exist. | Partial | Add Estates relationship model: tenant, lessee, purchaser, occupant, joint tenant, assignee, mortgagee, applicant, legal tenant, multi-tenant unit history. |
| File and correspondence tracking | ProcedureCase has reference, applicant, source department, fields, documents, activities. File upload exists. No estate file movement DemoUI found. Generic case/document/activity tracking. | Partial | Add TDC file movement register: incoming files, incoming letters, dispatch, officer assignment, location tracing, action/comment, due dates, letter book/forms purchase book equivalents. |
| Application/form intake | ProcedureCase can create generic cases; external portal has land registration placeholder; estate procedures list forms. No executable Estate application DemoUI found. Generic case intake and external portal foundation. | Partial | Add Estates application types: HOS form, rental unit form, estate transfer form, regularization form, search, transfer, assignment, lease, certified true copy, change of address. |
| Plot/unit allocation | SalesAllocation supports reservations/allocations against saleable sources; property register adapter can search estate assets. Land acquisition DemoUI does not cover allocation after asset creation. Generic allocation ledger, status history, approval endpoints. | Partial | Add TDC allocation workflow: PAC consideration, MD approval, waiting list, availability, proposal letter, deposit/full payment rule, reservation, substitution/reallocation, transparency history. |
| Proposal/offer/Right of Entry letters | No TDC-specific template engine found. Some document upload exists. No DemoUI source found. File/document storage can hold generated letters after build. | To add | Add controlled templates for proposal letters, offer letters, Right of Entry letters, completion letters, demand letters, rate revision notices, acknowledgement letters, search reports, certified true copies. |
| Lease preparation and title tracking | Land acquisition has registration milestones. Finance fixed asset lease accounting exists. SalesAgreement supports lease/tenancy agreement types. Estate procedure catalogue includes lease preparation and renewal. DemoUI route references cover acquisition registration, not TDC lessee lease preparation. Several pieces exist. | Partial | Add TDC lease preparation workflow: arrears check, building permit confirmation, substantial development inspection, cadastral plan request, demand letter, Legal lease request, Lands Commission registration, detachment, Revenue/Estate Records update. |
| Lease renewal / surrender and renewal | Estate procedure catalogue includes lease surrender and renewal; workflow admin can configure it. No DemoUI source found. Process is catalogued. | Partial | Add persisted renewal cases, LRTC approval, 10-year threshold rule, premium/improved ground rent calculation, invoice, offer/deed variation, renewal reports. |
| Housing/rental management | Finance AR customers, invoices/payments, payment plans, collection activities exist. Estate catalogue includes Housing and HOS. No DemoUI source found. Finance and generic procedure foundations. | Partial | Add rental housing register, rent cards, rent registers, legal tenant recognition, rent roll, house inspection, rental transfer, tenancy disputes, rent reminders, 6,000-unit HOS/rental portfolio fields. |
| Home Ownership Scheme | SalesAllocation, payment plans, customers, sales agreements, and estate assets exist. Estate catalogue includes HOS. No DemoUI source found. Generic sales/payment plan foundation. | Partial | Add HOS lifecycle: sitting tenant conversion, deposit/payment rules, multi-tenant completion rule, offer letter after completion, lease request, completion letter, default reversion to rental, HOS debtor reports. |
| Ground rent / arrears prerequisite controls | Finance AR, invoices, customer payments, collections, and payment plans exist. No DemoUI source found. Finance data structures can support balances and collection activity. | Partial | Add Estates-specific arrears check service integrated into searches, certified copies, transfers, assignments, leases, recognition, conversion, and regularization. |
| Revenue/Finance integration | Finance AR, invoices, customer payments, allocations, payment plans, and collection activities exist. No DemoUI source found. Finance module exists. | Partial | Define source of truth and integration between Estates and Revenue for receipts, ground rent, land management fees, regularization fees, deposits, CAM, rent, arrears, cashier monthly reports. |
| Facilities management | Facilities procedure catalogue and live ProcedureCase workspace exist for property/site, lease, maintenance, complaints, service providers, staff/cleaners, asset register, and documents. Maintenance module has work orders, assets, inspections, contractors, invoices, performance metrics. No separate DemoUI source found. Strong foundations: facilities cases plus mature maintenance module. | Partial | Connect Facilities ProcedureCase to Maintenance work orders/assets/contractors or create Estates-specific facilities records for TDC Towers, Site 3, Community 26, cleaners/gardeners, security, utilities, service charges. |
| Maintenance requests / work orders | Maintenance module has WorkOrder, WorkOrderTask, parts, labor, documents, comments, quality checks, approvals, contractors, schedules and mobile support. Facilities workspace describes maintenance stages. Work order engine exists. | Available / Partial | Wire Estates facilities requests into Maintenance WorkOrder and add TDC-specific requester/property/unit/tenant/service-charge context. |
| Inspections | Maintenance has AssetInspection and inspection documents. Estate procedure catalogue includes land/property inspection. Land acquisition demo references include survey verification, not routine estate inspections. Generic inspections exist. | Partial | Add TDC inspection templates: site report, substantial development, tenancy compliance, weekly site checks, house inspection, handover/return, defect close-out. |
| Complaints / customer service cases | Facilities ProcedureCase workspace includes complaint management. EHC/helpdesk modules also exist elsewhere. Facilities workspace describes complaint stages. Generic complaint/case workflow exists. | Partial | Decide whether Estates uses ProcedureCase, Helpdesk/EHC, or a dedicated Customer Service case module; add call-centre logs, notices, SLA, escalation, communication history. |
| Contractor/service provider oversight | Maintenance contractor entities support contractors, work orders, invoices, performance metrics, reviews, logistics, expenses. Facilities procedure catalogue includes service providers. Facilities workspace describes provider registration and performance stages. Contractor foundation exists. | Available / Partial | Link outsourced cleaners, gardeners, security, utilities and facilities contractors to Estates sites, contracts, rates, SLA, performance and reports. |
| Common asset register | MaintenanceAsset exists; EstateManagedAsset can represent facilities; Facilities catalogue includes asset register. Facilities workspace describes asset register. Multiple asset foundations exist. | Partial | Decide system of record for common assets such as meters, streetlights, open spaces, parking, drainage, common equipment; add EstateManagedAsset-to-MaintenanceAsset linkage if needed. |
| Service charges / CAM / utilities | Finance invoices/payments exist. No TDC estate service charge model found. No DemoUI source found. Finance can bill if configured. | To add | Add estate charge setup, CAM formulas, utility pass-throughs, shared-cost recoveries, dispute workflow, billing cycle, debt recovery integration. |
| Reports and dashboards | Maintenance reports exist; land acquisition board exists; finance reports exist; estate reporting is catalogued. No report DemoUI found. Reporting infrastructure exists in several modules. | Partial | Add TDC report pack: allocation, transfer fees, proposals/revenue, leases, assignments, building permits, ROE/offer letters, mortgages, rent roll, HOS debtors, facilities performance, Board pack. |
| Approval hierarchy | Workflow administration exists; land acquisition has role-based stage approvals; ProcedureCase can use configured workflow. DemoUI stage definitions include role names for land acquisition. Workflow engine and role-based stages exist. | Partial | Configure TDC-specific approval matrix: HOE, MD, Marketing Manager, LRTC, PAC, Legal, Finance, Development, Board, exception overrides, emergency maintenance thresholds. |
| Segregation of duties | Workflow and roles exist; audit fields exist. No DemoUI source found. Role/permission foundation exists. | Partial | Define and enforce incompatible role combinations for records, allocation, billing, receipting, maintenance approval, and record amendment. |
| Audit trail | TenantEntity audit fields, WorkflowActivityLogs, ProcedureCaseActivities and AuditLogs exist. DemoUI does not add extra audit detail. Audit foundations exist. | Partial | Ensure field-level old/new values for Estates master records, approvals, payment status overrides, letter generation, and exception decisions. |
| Exception/override register | Workflow can reject/approve; ProcedureCase activities can record actions. No DemoUI source found. Generic workflow can support this after configuration. | To add | Add explicit override request, reason, approver, expiry, affected policy, evidence, and reporting. |
| Document repository | Land acquisition documents, estate asset documents, procedure case documents, work order documents, sales agreement documents and file upload exist. DemoUI routes imply document stages but source not present. Strong document upload/storage foundation. | Available / Partial | Add TDC document taxonomies, mandatory document checklists, retention rules, access rules, scanned historical file migration plan. |

## What We Should Say in the Requirements Document

### Already Available

- Land acquisition workflow from parcel identification to registration and estate asset creation.
- Estate managed asset / land bank with manual existing-land entry, cadastral fields, ownership history, map/boundary data, document upload and project handoff.
- Facilities procedure case workspace for facilities operations.
- Generic procedure case engine with fields, checklists, documents, activities and workflow linkage.
- Maintenance module for assets, work orders, contractors, inspections, schedules, quality checks and maintenance reports.
- Sales allocation ledger for reserving/allocating saleable items, including property-register adapter foundation.
- Finance AR customer, invoice, customer payment, collection activity and payment plan foundations.
- Workflow administration, role-based approvals, workflow activity logs and general audit fields.

### Partially Available

- Estate SOP procedures are listed, but most are not live persisted operational cases yet.
- Facilities procedures exist as cases, but they are not fully connected to Maintenance work orders, finance billing, estate assets and TDC-specific reporting.
- Property/unit inventory exists as generic managed assets, but it does not yet capture the full TDC estate register, rent card, HOS ledger and unit occupancy model.
- Sales/Finance can support parts of allocation, payments and installments, but the TDC-specific ground rent, arrears prerequisite and HOS/rental business rules are not yet wired.
- Lease/tenancy concepts exist in Sales/Finance, but TDC lease preparation, lease renewal, LRTC approval and Lands Commission milestones need specific workflows.

### To Add

- TDC Estate Register fields and migration templates.
- Tenant/lessee/purchaser/occupant relationship history.
- Forms purchase and application intake workflows.
- File and letter movement register.
- TDC document/letter templates.
- Proposal, offer, Right of Entry and completion workflows.
- Ground rent and arrears prerequisite checks.
- HOS/rental lifecycle and rent-card/rent-roll reporting.
- CAM/service charge/utilities formulas and dispute workflow.
- Post-sale defects, handover/return inspections and structured customer support.
- Board/management report packs and estate service report templates.
- Segregation-of-duties matrix and exception/override register.

## Recommended Structure for Final Requirement Document

1. Current-state summary from TDC documents.
2. Current system/DemoUI comparison.
3. Requirements already covered by the system.
4. Requirements partially covered and requiring configuration or integration.
5. Requirements to add as new development.
6. Gaps/open decisions.
7. Prioritized implementation phases.

