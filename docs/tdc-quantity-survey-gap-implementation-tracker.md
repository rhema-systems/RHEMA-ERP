# TDC Quantity Survey Gap Implementation Tracker

Last updated: 2026-08-08

## Purpose

This tracker is the delivery ledger for closing the gaps between the current ERP implementation and the requirements in `Quantity Survey Questionnaire - Response.docx`, completed for TDC's Development / Quantity Survey function.

The source document defines the expected Quantity Survey operating model: Bills of Quantities, taking-off, rate build-up, cost libraries, price adjustment, measurement, remeasurement, interim valuation, payment certification, retention, material deductions, variations, claims, contract administration, subcontractor control, final accounts, cost reporting, integrations, workflow, security, migration, training, and post-go-live support.

## Source Of Truth And Boundaries

- Business source: `C:\Users\micha\Downloads\Questionnairs and SOPS\Questionnairs and SOPS\Development\Quantity Survey\Quantity Survey Questionnaire - Response.docx`
- Source render evidence: all 15 Word-paginated pages rendered and visually reviewed in `artifacts\tdc-quantity-survey-srs-revalidation\run-20260717-1\source-render`; this supersedes the earlier incomplete three-page render artifact.
- Current implementation evidence: Development/Projects entities, DTOs, services, controllers, frontend routes, project commercial administration, BoQ lines, work packages, budget revisions, forecast versions, variations, interim valuations, payment certificates, final account, material cost ledger, construction reports, procurement contracts, workflow adapters, finance/project budget touchpoints, and existing project-management tests.
- This tracker covers Quantity Survey as a commercial project-control lifecycle. It references Development/Projects, Procurement, Inventory/Stores, Finance/AP/AR/GL, Contract Management, Workflow, Document Management, external contractors/consultants, and Reporting only where QS requires those modules to complete an end-to-end process.
- This tracker does not certify TDC's contract, measurement, retention, fluctuation, or statutory procurement policy interpretation. TDC Quantity Survey, Development, Procurement, Finance, Legal/Contracts, Internal Audit, ICT, contractors, and consultants must approve final configuration values and acceptance scenarios.

## Delivery Rule

A slice may be marked `Done` only when all applicable parts are complete:

1. Backend domain model and business rules.
2. Database migration and data backfill.
3. API endpoints and authorization.
4. Frontend routes, controls, validation, and status visibility.
5. Workflow, notifications, documents, audit events, and reports where required.
6. Automated tests for happy paths, hard stops, overrides, tenant isolation, and direct API attempts.
7. Migration applied to the test database.
8. Browser/API smoke verification.
9. Tracker evidence updated with commands, routes, and test results.

No requirement is complete merely because a field, page, or generic project-commercial record exists. TDC requires enforced QS behavior from design/site measurement through approval, valuation, certification, payment, reporting, and final account.

## Status Legend

| Status | Meaning |
| --- | --- |
| Verified baseline | Current implementation has material capability confirmed in source; final TDC acceptance is still required. |
| Partial | Useful capability exists, but required QS controls, fields, workflow steps, reports, integrations, or hard stops are incomplete or bypassable. |
| Gap | Required capability or enforcement is absent. |
| Configuration required | TDC must approve configurable values, roles, methods, formulas, templates, workflows, access rules, report packs, or migration ownership before acceptance. |
| Not started | Approved implementation task has not started. |
| In progress | Implementation is actively being changed and is not yet acceptance-ready. |
| Done | Full delivery rule above has passed and evidence is recorded. |
| Blocked | Work cannot safely continue until a named dependency is resolved. |

## Honest Readiness Statement

The ERP already contains a strong project-commercial foundation: project packages/work components, BoQ items, budget worksheet values, budget revisions, forecast versions, variation orders, interim valuations, payment certificates, derived final accounts and final-payment billing steps, material cost entries, project commercial summaries, construction-commercial reports, procurement contracts with milestones and retention percentage, project/external-party access policies, and workflow adapters for selected project records.

The main gap is not the absence of a Development/Projects module. The main gap is that TDC's Quantity Survey operating model is not yet enforced as one QS lifecycle. The implementation must tighten BoQ versioning, SMM7/CESMM code support, Excel/standard-format import, rate libraries, rate build-up, market survey inputs, price-index escalation, contractor claim submission and vetting, joint measurements, line-level valuation, retention release rules, advance recovery, material deductions including off-site materials, variation-to-budget/certificate automation, subcontractor account controls, access by project/contract/authority, and migration of historical Excel/physical records.

Most "decision" items below should become configuration rather than hard-coded code blockers. They are listed because TDC must approve the values before the system can be acceptance-tested.

The 2026-07-17 revalidation confirmed that the earlier tracker materially covers every substantive questionnaire response. The revalidation adds explicit row-by-row traceability, corrects the source render evidence, recognizes current final-account and external-access capabilities more precisely, and removes one undefined roadmap reference. No client requirement was removed or downgraded.

## Source Requirement Baseline

The questionnaire contains one 43-row table under `3.3.2 Quantity Survey`. Every answered functional item is marked `Yes`. Most requirements are `Critical`; variation/change initiation, cost-control dashboards, and broad module integration are marked `High`.

| Area | Stated TDC requirement baseline |
| --- | --- |
| BoQ management | Create, import, revise, approve, version, structure, compare, export, and control BoQs by project type, project, section, trade, cost code, and work package. |
| BoQ line content | Capture item codes, editable descriptions, units, quantities, unit rates, and totals, with reference to SMM7 and CESMM3/4 sections/codes. |
| BoQ source process | BoQ can be design-based or site-measurement-based; designs are studied, queries raised, measurements taken, taking-off completed, rate build-up prepared, peer reviewed, and submitted for approval. |
| External exchange | BoQs may be shared with tenderers and MIS, secured in Excel, imported/exported, and potentially integrated with third-party estimation tools such as Candy, Planswift, and Bani Estimation. |
| Cost libraries | Centralized QS database should store quantity survey data, standard items, historical costs, rate libraries, basic material prices, plant/equipment rates, and periodic updates by project type, region, contractor, supplier, and time period. |
| Rate analysis | Rate build-up must support labour, materials, plant, equipment, subcontract, overheads, profit, attendance, contingencies, and other configurable assumptions. |
| Estimates and budgets | Cost plans, tender estimates, budget estimates, approved budget comparison, actual cost comparison, and cost analysis are required. |
| Price adjustment | Fluctuation/escalation must use configurable formulas, indices, contract clauses, effective dates, Ghana Statistical Service/PBCI inputs, Roads/Infrastructure indices, coefficients, base rates, revised rates, and audit history. |
| Measurement and remeasurement | Support measurements, remeasurements, taking-off sheets, design changes, variation design add-ons, quantity updates against BoQ items, contractor remeasurement requests, joint site measurement, and endorsement by consultant and contractor. |
| Valuation and certification | Support interim valuations, progress payment certificates, retention calculations, advance recovery, final account statements, previous certificates, deductions, materials on site, off-site materials, and net payable amounts. |
| Material deductions | Track TDC-supplied materials issued to contractors and deduct values from contractor certificates; support reconciliation by contractor and certificate. |
| Variations and claims | Register variation instructions, change orders, dayworks, claims, and additional works from initiation through approval and valuation, with workflow tracking and audit trail. |
| Contract administration | Manage contract sums, provisional sums, contingencies, retention terms, defects liability periods, subcontract payment terms, subcontractor valuations, certificates, back charges, contra charges, and final account settlement. |
| Cost control and reporting | Provide dashboards and reports for budgets, commitments, certified value, actual costs, variations, forecasts, final projected costs, BoQ summaries, valuation statements, variation logs, final accounts, and project cost status. |
| Drilldown and access | Users need drilldown from summaries to contract, project, cost code, and line item details, with configurable access by section/unit. |
| Integrations | QS must integrate with procurement, inventory, project management, contract management, accounts payable, accounts receivable, and general ledger. |
| Automation | Commitments, actual costs, and payment data should update automatically when purchase orders, goods receipts, invoices, or payment certificates are processed. |
| Workflow and security | Configurable approval workflows are required for BoQs, estimates, valuations, variations, and payment certificates; access must be controlled by role, project, contract, and approval authority level. |
| Audit | Full audit logs are required for creation, edits, approvals, cancellations, and financial adjustments. |
| Migration and support | Existing BoQs, rate libraries, historical project costs, contracts, valuation records, Excel files, and physical file records must be migrated and preserved by name and year. |
| Implementation expectations | TDC expects structured implementation, configuration, data migration, testing, go-live support, manuals, user training, timely issue resolution, system maintenance, and temporary 24/7 on-site support. |

## Source Questionnaire Row Traceability

The source contains 30 substantive response rows in addition to instruction, column-header, and section-header rows. The row numbers below refer to the single 43-row Word table and provide a direct audit path from each client response to the detailed gap IDs, implementation tasks, and end-to-end acceptance scenarios.

| Source row and requirement | Priority | Current coverage IDs | Implementation and acceptance coverage |
| --- | --- | --- | --- |
| 4 - Create, import, revise, approve, and version BoQs for different project types | Critical | `BOQ-003`, `BOQ-004`, `BOQ-005` | `QS-0103` through `QS-0105`; `QS-E2E-001`, `QS-E2E-002` |
| 5 - Structure BoQs by project, section, trade, cost code, and work package; receive and vet contractor claims | Critical | `BOQ-002`, `BOQ-006` | `QS-0101`, `QS-0106`, `QS-0502`, `QS-0509`; `QS-E2E-002`, `QS-E2E-008` |
| 6 - Capture codes, editable descriptions, units, quantities, rates, and totals using SMM7/CESMM references | Critical | `BOQ-001`, `BOQ-002` | `QS-0101`, `QS-0102`, `QS-0103`; `QS-E2E-001` |
| 7 - Import/export standard files, secure tenderer BoQs, import indices, and support external estimation tools | Critical | `BOQ-004`, `BOQ-006`, `ESC-001`, `INT-004` | `QS-0103`, `QS-0106`, `QS-0302`, `QS-0405`; `QS-E2E-002`, `QS-E2E-005` |
| 8 - Compare original, revised, tender, executed, outstanding/repackaged, and final-account quantities with arithmetic checks | Critical | `BOQ-005`, `BOQ-006`, `VAL-007` | `QS-0104`, `QS-0106`, `QS-0506`; `QS-E2E-002`, `QS-E2E-014` |
| 10 - Centralize QS data, rate libraries, historical costs, and standard items with controlled updates | Critical | `RATE-001` | `QS-0201`, `QS-0203`; `QS-E2E-003` |
| 11 - Categorize rates by project type, region, contractor, supplier, period, material, plant, and equipment | Critical | `RATE-002`, `RATE-003` | `QS-0201` through `QS-0203`; `QS-E2E-003` |
| 12 - Audit quantity, rate, assumption, late-information, and personnel-handover changes | Critical | `GOV-004`, `RATE-004` | `QS-0003`, `QS-0202`; `QS-E2E-016` |
| 14 - Build up rates from labour, materials, plant/equipment, subcontract, overhead, and profit | Critical | `EST-001` | `QS-0204`; `QS-E2E-003` |
| 15 - Prepare cost plans, tender estimates, and budgets with configurable markups and assumptions | Critical | `EST-002` | `QS-0205`; `QS-E2E-004` |
| 16 - Compare estimates against approved budgets and actual costs | Critical | `EST-003` | `QS-0206`, `QS-0602`; `QS-E2E-004`, `QS-E2E-013` |
| 18 - Calculate price adjustments from formulas, indices, clauses, coefficients, and effective dates | Critical | `ESC-001` | `QS-0301` through `QS-0303`; `QS-E2E-005` |
| 19 - Retain base/revised rates and escalation history for audit and disputes | Critical | `ESC-002`, `ESC-003` | `QS-0303`, `QS-0304`, `QS-0506`; `QS-E2E-005` |
| 21 - Measure and remeasure work, update BoQ quantities, compare designs, accept contractor requests, and jointly endorse site measurements | Critical | `MEAS-001` through `MEAS-004` | `QS-0401` through `QS-0405`; `QS-E2E-006`, `QS-E2E-007` |
| 22 - Manage interim valuations, certificates, retention releases, advance recovery, fluctuations, deductions, and final accounts | Critical | `VAL-001` through `VAL-007` | `QS-0501` through `QS-0506`; `QS-E2E-008`, `QS-E2E-009`, `QS-E2E-014`, `QS-E2E-015` |
| 23 - Track on-site/off-site and TDC-supplied materials, deductions, net payable, and reconciliation by contractor/certificate | Critical | `VAL-005` | `QS-0507`, `QS-0605`; `QS-E2E-010` |
| 25 - Register variation instructions, change orders, dayworks, claims, and additional works through approval and valuation | High | `VAR-001`, `VAR-002`, `VAR-004` | `QS-0508` through `QS-0510`; `QS-E2E-011` |
| 26 - Update revised contract sums, budgets, forecasts, certificates, and final accounts from approved variations | Critical | `VAR-003`, `CON-004` | `QS-0511`; `QS-E2E-011` |
| 28 - Configure contract sums, provisional sums, contingencies, retention, defects liability, and subcontract terms | Critical | `CON-001`, `CON-002` | `QS-0520`; `QS-E2E-008`, `QS-E2E-009` |
| 29 - Manage subcontractor valuations, certificates, back/contra charges, payment, and final settlement | Critical | `CON-003` | `QS-0521`, `QS-0522`; `QS-E2E-012` |
| 31 - Provide real-time cost dashboards for budget, commitments, certified value, actuals, variations, forecasts, and projected final cost | High | `RPT-001`, `RPT-002` | `QS-0601`, `QS-0602`; `QS-E2E-013` |
| 32 - Provide standard and customizable BoQ, valuation, variation, final-account, and project-cost reports | Critical | `RPT-004` | `QS-0601`, `QS-0603`; `QS-E2E-013` |
| 33 - Drill from summary to contract, project, cost code, and line item with section/unit access control | Critical | `GOV-001`, `RPT-003` | `QS-0004`, `QS-0602`, `QS-0606`; `QS-E2E-013`, `QS-E2E-016` |
| 35 - Integrate Procurement, Inventory, Projects, Contracts, AP, AR, and GL | High | `INT-001` | `QS-0604`; `QS-E2E-015` |
| 36 - Automatically update commitments, actuals, certificates, invoices, payments, and workflow progress | Critical | `INT-002`, `INT-003` | `QS-0605`; `QS-E2E-015` |
| 38 - Configure approvals for BoQs, estimates, valuations, variations, and payment certificates | Critical | `GOV-002` | `QS-0005`; `QS-E2E-001`, `QS-E2E-008`, `QS-E2E-011`, `QS-E2E-016` |
| 39 - Control access by role, project, contract, and approval authority | Critical | `GOV-001`, `RPT-003` | `QS-0004`, `QS-0606`; `QS-E2E-016` |
| 40 - Audit creation, edits, approvals, cancellations, and financial adjustments | Critical | `GOV-004`, `RATE-004`, `ESC-002` | `QS-0003`, `QS-0606`; `QS-E2E-005`, `QS-E2E-016` |
| 42 - Migrate BoQs, rate libraries, historical costs, contracts, valuations, and physical-file references searchable by name/year | Critical | `MIG-001`, `MIG-002` | `QS-0701`, `QS-0702`; migration reconciliation within `QS-0704` |
| 43 - Deliver structured implementation, testing, training/manuals, go-live support, maintenance, and temporary 24/7 on-site assistance | Stated expectation | `CHG-001` | `QS-0703` through `QS-0705` |

## Business Configuration Inputs

| ID | Priority | Status | Configuration or decision required | Why it matters | Owner |
| --- | --- | --- | --- | --- | --- |
| QS-CFG-001 | P0 | Configuration required | Confirm QS roles, authority levels, approval actors, and access groups: QS officers, senior QS, project managers, Development, Procurement, Finance, Legal/Contracts, Internal Audit, consultants, contractors, and management. | Workflow routing, access control, document visibility, and audit depend on role mapping. | QS + Development + ICT |
| QS-CFG-002 | P0 | Configuration required | Confirm BoQ standards and code catalogues: SMM7, CESMM3/4, local TDC sections, cost codes, trades, work packages, and project types. | BoQ import, validation, reporting, and tender sharing require approved structures. | QS + Development |
| QS-CFG-003 | P0 | Configuration required | Confirm BoQ versioning policy for original, tender, approved, revised, remeasurement, terminated/repackaged, and final account states. | Comparisons and audit lineage cannot be accepted without version rules. | QS + Legal/Contracts |
| QS-CFG-004 | P0 | Configuration required | Confirm rate build-up component structure: materials, labour, plant, equipment, subcontract, overhead, profit, attendance, contingencies, wastage, transport, and other markups. | Estimates and tender rates must be calculated consistently and auditable. | QS + Finance |
| QS-CFG-005 | P0 | Configuration required | Confirm rate library dimensions and update cadence by project type, region, contractor, supplier, time period, material, plant, and equipment. | TDC's regional expansion and periodic market survey updates need controlled catalogues. | QS + Procurement |
| QS-CFG-006 | P0 | Configuration required | Confirm price adjustment formula templates, coefficient controls, index sources, effective dates, and import formats for PBCI/Ghana Statistical Service and Roads/Infrastructure indices. | Escalation/fluctuation cannot be automated safely from inferred formulas. | QS + Finance + Legal/Contracts |
| QS-CFG-007 | P0 | Configuration required | Confirm measurement workflow, taking-off sheet template, contractor remeasurement request rules, joint measurement attendance, endorsement/signature requirements, and consultant role. | Valuation should be based on controlled measurement evidence. | QS + Development + Contractors/Consultants |
| QS-CFG-008 | P0 | Configuration required | Confirm valuation and payment certificate templates, required supporting documents, previous-certificate logic, deductions, VAT/tax handling, advance recovery, retention, and net payable rules. | Payment certificates and Finance/AP integration need exact financial rules. | QS + Finance |
| QS-CFG-009 | P0 | Configuration required | Confirm retention terms: maximum retention, partial release at practical completion, sectional takeover release, defects-liability release, retention bond alternatives, and approval route. | The questionnaire explicitly asks for partial/sectional retention release behavior. | QS + Legal/Contracts + Finance |
| QS-CFG-010 | P0 | Configuration required | Confirm material deduction rules for TDC-supplied materials, materials on site, off-site materials, contractor reconciliation, certificate deduction, and evidence requirements. | Material values must reconcile to Inventory/Stores and certificates. | QS + Stores + Finance |
| QS-CFG-011 | P0 | Configuration required | Confirm variation, claim, daywork, additional work, site instruction, and change-order workflow, including budget/cost forecast updates. | Approved variations must update revised contract sums, budgets, forecasts, and certificates. | QS + Development + Legal/Contracts |
| QS-CFG-012 | P1 | Configuration required | Confirm contract administration controls for provisional sums, contingencies, defects liability periods, subcontract terms, back charges, contra charges, and final account settlement. | Contract records need QS-specific commercial terms beyond generic milestones. | QS + Procurement + Legal |
| QS-CFG-013 | P1 | Configuration required | Confirm external party submission model for contractors and consultants: portal, Excel upload, API, signed PDF, or hybrid. | Contractor claims, quotations, remeasurement requests, and endorsements require a controlled intake path. | QS + ICT + Contractors |
| QS-CFG-014 | P1 | Configuration required | Confirm third-party estimation/design integration scope for Candy, Planswift, Bani Estimation, CAD/BIM/design files, design comparison, and quantity extraction. | The source asks whether external estimation/design tools can be incorporated. | QS + ICT + Development |
| QS-CFG-015 | P1 | Configuration required | Confirm QS report catalogue, dashboard formulas, drilldown permissions, custom report scope, export templates, and distribution schedule. | Reports must reconcile to accepted formulas and access rules. | QS + Management |
| QS-CFG-016 | P1 | Configuration required | Confirm integration control document for Procurement, Inventory, Project Management, Contract Management, AP, AR, GL, document storage, and workflow. | Commitments, actuals, certificates, and payments must synchronize without double posting. | ICT + Finance + QS |
| QS-CFG-017 | P1 | Configuration required | Confirm migration owners and source columns for BoQ files, rate libraries, market surveys, contracts, certificates, valuations, claims, variations, final accounts, and physical records. | Migration cannot be acceptance-ready without signed source ownership and reconciliation. | QS + ICT |

## Current Coverage And Gap Matrix

### Governance, Policy, Roles, And Audit

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| GOV-001 | Partial | The ERP has RBAC, operation-level project authorization, project access tabs, and business-partner external access policies with read/comment/upload/approve flags. TDC-specific QS roles, contract/authority limits, contractor/consultant submission identities, and section/unit report visibility are not acceptance-tested as one QS role matrix. |
| GOV-002 | Partial | Workflow adapters exist for Project, ProjectDeliverable, ProjectClosure, and ProjectBudgetRevision. QS workflows for BoQ approval, estimate approval, valuation approval, variation approval, payment certificate approval, retention release, claims, and final account are not all integrated into the shared workflow engine. |
| GOV-003 | Gap | No dedicated QS policy register for BoQ standards, rate build-up assumptions, market survey cadence, indices, retention rules, material deductions, variation workflows, certificate templates, report formulas, and authority levels. |
| GOV-004 | Partial | Generic project history and workflow history exist, but QS-specific immutable audit events are not guaranteed across every BoQ import/revision, rate update, assumption change, measurement, valuation, certificate, deduction, variation, cancellation, and financial adjustment. |

### BoQ Management, Versioning, Import, And Comparison

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| BOQ-001 | Verified baseline | Project BoQ items exist with line number, item code, item type, description, quantity, unit of measure, unit rate, budget quantity, budget unit rate, budget amount, committed amount, actual amount, forecast amount, currency, and links to inventory, tender, procurement plan, PR, and PO items. |
| BOQ-002 | Partial | Existing project packages remain the controlled work-package hierarchy. BoQ lines now select effective tenant section, trade, cost-code, and SMM7/CESMM/TDC measurement-code records and preserve immutable snapshots; approved catalogue-pack loading, staged import, versioning, and browser acceptance remain incomplete. |
| BOQ-003 | Partial | BoQ item CRUD, protected spreadsheet import/export, immutable candidate snapshots, exact `QS-DEC-003` shared-workflow submission, authority-gated approval/rejection/recall, and separately published immutable Approved snapshots now reuse the Development project workspace. Authenticated role/authority browser acceptance remains incomplete under `QS-0105`. |
| BOQ-004 | Partial | QS-0103 provides a protected server-generated workbook, locked control/formula cells, controlled lookups, central-DMS staging, validation findings/report, signed reconciliation, transactional posting, and idempotent retry. Authenticated round-trip acceptance and third-party estimation adapters remain outstanding. |
| BOQ-005 | Partial | QS-0104 manages immutable, project-scoped BoQ snapshots with stable line keys and comparisons for added, removed, changed, and unchanged lines plus quantity, rate, and value deltas. QS-0105 now blocks direct Approved creation, publishes only after the configured shared workflow and central authority checks succeed, retires the preceding publication atomically, and hard-blocks downstream retrieval when no approved publication exists. Authenticated end-to-end acceptance and executed/outstanding quantity derivation remain outstanding. |
| BOQ-006 | Gap | External contractor quotation/claim submission and QS vetting against BoQ lines is not available as a controlled portal/upload workflow. |

### Rate Libraries, Market Survey, And Cost Database

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| RATE-001 | Partial | QS-0201 provides the centralized, tenant-safe QS item and effective-dated rate library while reusing Inventory UOM/items, Finance currencies, Projects/QS catalogues, Procurement partners, and central DMS. QS-0202 consumes the existing published Procurement market-analysis/price-history register for governed survey updates. Historical project-cost promotion remains outstanding under QS-0203. |
| RATE-002 | Partial | The rate library now classifies standard, material, labour, plant/equipment, and subcontract items and supports controlled project type, location/region, contractor, supplier, source, date, and evidence dimensions. Authenticated acceptance and historical-cost promotion remain outstanding. |
| RATE-003 | Partial | QS-0202 prepares evidence-backed market-survey revisions from published Procurement analyses, records previous/new values and review cadence, and reuses the independent Draft-to-Published rate approval lifecycle. Authenticated preparer/checker acceptance remains outstanding. |
| RATE-004 | Partial | QS-0201 and QS-0202 write the shared QS audit register for item/rate lifecycle and market-survey lineage, including immutable DMS evidence, previous-rate snapshots, decision reasons, actors, and correlations. Quantity, late-information, assumption, and personnel-handover audit coverage remains incomplete. |

### Rate Analysis, Estimating, Budgets, And Cost Planning

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| EST-001 | Partial | Project BoQ lines carry unit rates and budget values, and budget revisions/forecast versions exist. Detailed rate build-up by labour, material, plant/equipment, subcontract, overhead, profit, attendance, contingency, and other markups is missing. |
| EST-002 | Partial | Project budgets, budget revisions, forecasts, and budget-vs-actual reports exist. Cost plans, tender estimates, and budget estimates are not yet delivered as QS-controlled estimate versions with configurable assumptions and approval. |
| EST-003 | Partial | Approved budget, committed cost, actual cost, forecast cost, budget consumption, and project commercial summaries exist. QS acceptance still requires line-level reconciliation from BoQ/rate build-up to approved budget, commitments, certificates, actual costs, and final projected cost. |

### Price Adjustment, Fluctuation, And Escalation

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| ESC-001 | Gap | No QS price adjustment engine exists for configurable formulas, contract clauses, coefficient sets, index sources, base month, effective dates, PBCI/Ghana Statistical Service imports, or Roads/Infrastructure indices. |
| ESC-002 | Gap | No history exists for base rates, revised rates, imported indices, calculation runs, contractor submissions, reviewer adjustments, and dispute-resolution audit packs. |
| ESC-003 | Gap | Final account reconciliation does not automatically calculate fluctuations/escalation or apply contract-specific price adjustment clauses. |

### Measurement, Remeasurement, Taking-Off, And Design Controls

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| MEAS-001 | Partial | Project design/site control foundations, packages, BoQ quantities, interim valuation package completions, and milestone progress linkage exist. QS taking-off sheets, measurement worksheets, and remeasurement records are not first-class controlled records. |
| MEAS-002 | Partial | Design packages and site instructions exist in the project module, but revised design comparison, design add-on tracking, architectural/structural quantity extraction, and revision impact on BoQ quantities are incomplete. |
| MEAS-003 | Gap | Contractor remeasurement requests, joint measurement scheduling, consultant/contractor endorsement, evidence upload, and signed measurement record are not implemented end to end. |
| MEAS-004 | Gap | Quantity updates are not yet posted to BoQ line-level measurement history with previous quantity, measured-to-date quantity, certified quantity, rejected/disputed quantity, and reviewer notes. |

### Valuation, Payment Certification, Retention, And Materials

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| VAL-001 | Verified baseline | Interim valuation entities exist with valuation number, title, status, date, gross work value, materials-on-site value, variation value, retention percentage, retention amount, previous certified amount, net valuation amount, currency, project/phase/package/milestone/contract links, and completed package linkage. |
| VAL-002 | Verified baseline | Payment certificate entities exist with project/phase/package/contract/interim valuation links, certificate number, status, issue date, payment due date, gross certified amount, retention held, retention released, other deductions, and related financial values. |
| VAL-003 | Partial | The commercial administration UI calculates previews for completed work components, gross work value, retention, and net due. QS line-level measurement-to-valuation linkage and certificate worksheet generation are incomplete. |
| VAL-004 | Partial | Retention percentage and release fields exist, but contract-driven partial retention release at practical completion, partial/sectional takeover, and defects-liability release is not enforced. |
| VAL-005 | Partial | Materials-on-site value and project material cost ledger exist. Controlled material-on-site/off-site evidence, contractor material reconciliation, TDC-supplied material deductions, and certificate-level deduction automation are incomplete. |
| VAL-006 | Gap | Advance payment recovery and its certificate-by-certificate amortization are not implemented as a QS/payment certificate rule. |
| VAL-007 | Partial | Final-account automation currently derives contract value, approved variations, valuation claims, certified amounts, retention, other deductions, final value, and a final-payment billing step. It does not yet reconcile approved/revised BoQ versions, line measurements, formal claims, escalation, advance recovery, material deductions, or all downstream payments. |

### Variations, Claims, Dayworks, And Change Management

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| VAR-001 | Partial | Variation orders exist with project, phase, package, contract, reference number, title, description, type, status, requested date, approved date, implemented date, requester/approver names, estimated amount, approved amount, currency, schedule impact, and notes. |
| VAR-002 | Partial | Site instructions and customer variations exist in the broader project module. QS variation instructions, change orders, dayworks, claims, additional works, and contractor submissions are not one controlled workflow from initiation through approval and valuation. |
| VAR-003 | Partial | Budget revisions and forecast versions exist. Approved variations do not consistently hard-update revised contract sum, BoQ version, budget, forecast, valuation, payment certificate, and final account lineage. |
| VAR-004 | Gap | Contractor claims, QS vetting, dispute status, approved/rejected amounts, daywork sheets, and claim settlement audit packs are missing. |

### Contract Administration, Subcontractors, And Final Account

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| CON-001 | Partial | Procurement contract entities support contract value, payment terms, retention percentage, milestones, amendments, documents, and status. QS-specific contract administration for provisional sums, contingencies, defects liability periods, subcontract terms, back charges, contra charges, and final account settlement is incomplete. |
| CON-002 | Partial | Project commercial records link to contracts, and project final account exists. Contract sum changes, provisional sum releases, contingency drawdowns, and final account adjustments are not controlled as QS financial events. |
| CON-003 | Gap | Subcontractor valuations, subcontractor certificates, back charges, contra charges, contra-charge notices, subcontract final account, and settlement tracking are missing. |
| CON-004 | Partial | Contract amendments exist in Procurement. QS-driven contract amendments are not automatically synchronized with BoQ revisions, variation approvals, forecasts, and payment certificates. |

### Cost Control, Dashboards, Reports, And Drilldown

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| RPT-001 | Verified baseline | Development reports exist for budget-vs-actual, material reconciliation, procurement reconciliation, phase gate readiness, approval watch, construction commercial report, billing summary, workflow approval queue, and commercial summary metrics. |
| RPT-002 | Partial | QS-specific dashboards for budget, commitments, certified value, actual cost, variations, forecasts, and final projected cost exist in parts, but TDC named QS report pack is not acceptance-ready. |
| RPT-003 | Partial | Drilldown is available in project workspace and reports, but access by section/unit, project, contract, cost code, and line-item authority is not configured and tested. |
| RPT-004 | Partial | Excel/PDF/reporting infrastructure exists in the platform, but configurable QS report templates for BoQ summaries, valuation statements, variation logs, final accounts, project cost status, and custom QS reports are incomplete. |

### Integrations And Automatic Data Flow

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| INT-001 | Partial | QS-related project records integrate with project management, procurement lookups, contracts, material cost ledger, purchase/procurement links, and finance project budgets. The full QS interface contract across Procurement, Inventory, Project Management, Contract Management, AP, AR, and GL is not documented or acceptance-tested. |
| INT-002 | Partial | BoQ lines can reference tender, procurement plan, PR, PO, and inventory items. Commitments, actual costs, certificates, invoices, payments, and GL postings are not fully synchronized from source transactions without manual intervention. |
| INT-003 | Partial | Final-account refresh can synchronize a final-payment billing schedule and editable invoice requests, but payment certificate approval does not automatically create or update AP/payment workflow, retention and advance-recovery ledgers, GL commitment reversal, or payment-status reconciliation. |
| INT-004 | Gap | External estimation/design integrations for Candy, Planswift, Bani Estimation, CAD/BIM/design quantity extraction, design comparison, and secure tenderer BoQ exchange are missing. |

### Migration, Training, Support, And Readiness

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| MIG-001 | Gap | No QS-specific migration workbench exists for historical BoQ files, rate libraries, market surveys, project costs, contracts, valuations, certificates, claims, variations, final accounts, and physical file metadata. |
| MIG-002 | Gap | No cleansing/reconciliation process exists for duplicate BoQ item codes, inconsistent units, missing SMM/CESMM codes, stale rates, incomplete certificates, unmatched deductions, or physical-file references. |
| CHG-001 | Gap | QS role-based training manuals, contractor/consultant instructions, train-the-trainer materials, UAT scripts, support plan, and post-go-live issue process are not prepared. |

## Key Questionnaire Requirement Traceability

| No. | Questionnaire requirement | Current coverage | Implementation task IDs |
| ---: | --- | --- | --- |
| 1 | BoQ creation, import, revision, approval, and version control | Partial. BoQ lines exist; versioned BoQ lifecycle, import/export, approval, and comparison are incomplete. | `QS-0101` through `QS-0106`, `QS-E2E-001`, `QS-E2E-002` |
| 2 | BoQ structure by project, section, trade, cost code, and work package | Partial. Existing project packages and BoQ items persist controlled tenant section, trade, cost-code, and SMM/CESMM measurement-code snapshots, enforce effective `QS-DEC-002`, and support controlled staged import; governed versioning and authenticated round-trip acceptance remain outstanding. | `QS-0101`, `QS-0102`, `QS-E2E-001` |
| 3 | BoQ line item codes, descriptions, UOM, quantity, rates, totals | Partial. Core line fields, controlled effective-dated catalogue administration, protected spreadsheet staging, formula validation, export, and central audit interception exist. TDC-approved licensed SMM7/CESMM/local catalogue data and authenticated acceptance remain outstanding. | `QS-0102`, `QS-0103`, `QS-E2E-001` |
| 4 | BoQ Excel/standard import/export and third-party estimation tools | Partial. A protected tenant/project template, controlled export, central-DMS retained upload, server preview, validation report, signed reconciliation, transactional posting, and idempotent retry are implemented. Authenticated round-trip smoke and third-party estimation-tool adapters remain outstanding. | `QS-0103`, `QS-0604`, `QS-E2E-002`, `QS-E2E-016` |
| 5 | Original/revised/tender/final account comparison | Partial. QS-0104 provides immutable project-scoped Original, Tender, Revised, Remeasurement, Terminated/Repackaged, and Final Account snapshots with stable line lineage, quantity/rate/value deltas, added/removed-line classification, and currency totals. QS-0105 now owns workflow-authorized Approved publication and controlled replacement; authenticated browser acceptance and executed/outstanding quantity derivation remain outstanding. | `QS-0104`, `QS-0105`, `QS-0504`, `QS-E2E-014` |
| 6 | Centralized QS database and rate libraries | Partial. QS-0201 provides the controlled item/rate library and QS-0202 adds Procurement-owned market-survey update lineage without duplicating currencies, UOMs, partners, documents, workflows, or market-analysis stores. Historical project-cost promotion and authenticated end-to-end acceptance remain outstanding. | `QS-0201` through `QS-0203`, `QS-E2E-003` |
| 7 | Rate build-up using labour/material/plant/subcontract/OH/profit | Gap. Unit rates exist, but build-up components are missing. | `QS-0204`, `QS-E2E-003` |
| 8 | Cost plans, tender estimates, and budget estimates | Partial. Project budgets/revisions exist; QS estimate versions and assumptions are incomplete. | `QS-0205`, `QS-E2E-004` |
| 9 | Estimate vs approved budget and actual cost comparison | Partial. Reports exist; line-level QS reconciliation is incomplete. | `QS-0206`, `QS-0602`, `QS-E2E-004` |
| 10 | Price adjustment and escalation formulas/indices | Gap. Needs formula engine, index import, history, and dispute audit. | `QS-0301` through `QS-0304`, `QS-E2E-005` |
| 11 | Measurement/remeasurement and taking-off | Partial. Quantities and progress exist; taking-off sheets and measurement history are missing. | `QS-0401`, `QS-0402`, `QS-E2E-006` |
| 12 | Contractor remeasurement and joint measurement endorsement | Gap. Needs request, schedule, evidence, consultant/contractor sign-off. | `QS-0403`, `QS-E2E-007` |
| 13 | Interim valuation, payment certificate, retention, advance recovery, final account | Partial. Core valuation/certificate/final-account records exist; rules and line-level certification are incomplete. | `QS-0501` through `QS-0506`, `QS-E2E-008`, `QS-E2E-009`, `QS-E2E-014` |
| 14 | Materials on site, off-site materials, deductions, and payable amounts | Partial. Material ledger and valuation fields exist; deduction automation is incomplete. | `QS-0507`, `QS-E2E-010` |
| 15 | Variations, claims, dayworks, and additional works | Partial. Variation orders exist; claims/dayworks/submission/vetting are missing. | `QS-0508` through `QS-0510`, `QS-E2E-011` |
| 16 | Approved variations update contract sum, budgets, forecasts, and certificates | Partial. Budget revisions exist; automatic lineage is incomplete. | `QS-0511`, `QS-E2E-011` |
| 17 | Contract administration and subcontractor commercial controls | Partial. Procurement contracts exist; QS-specific subcontractor valuations/back charges are missing. | `QS-0520` through `QS-0522`, `QS-E2E-012` |
| 18 | Cost dashboards, custom reports, and drilldown | Partial. Project reports exist; named QS report pack and access-controlled drilldowns are incomplete. | `QS-0601` through `QS-0603`, `QS-E2E-013` |
| 19 | Integrations with procurement, inventory, projects, contracts, AP, AR, GL | Partial. Touchpoints exist; payment certificate and actual-cost automation are incomplete. | `QS-0604` through `QS-0606`, `QS-E2E-015` |
| 20 | Workflow, security, audit, migration, training, and support | Partial/Gaps. Shared workflow exists, but QS-specific workflows, audit map, migration, and training are incomplete. | `QS-0001` through `QS-0005`, `QS-0701` through `QS-0705`, `QS-E2E-016` |

## Implementation Roadmap

### Phase 0 - Configuration, Policy, Workflow, And Audit Architecture

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0001 | P0 | In progress | Resolve `QS-CFG-001` through `QS-CFG-017` as effective-dated configuration. | Every configuration item has owner, approved value, effective date, evidence, and tenant seed plan. |
| QS-0002 | P0 | In progress | Build QS policy/config register. | BoQ standards, rate libraries, formulas, retention rules, material deductions, workflows, templates, and access rules are versioned and queryable. |
| QS-0003 | P0 | In progress | Define QS audit event map. | Every QS action records actor, role, source, before/after values, formula inputs, evidence links, approval state, and correlation ID. |
| QS-0004 | P0 | In progress | Configure TDC QS roles, permissions, and authority levels. | Test users can only see and act by role, project, contract, section/unit, and approval authority. |
| QS-0005 | P0 | In progress | Add shared workflow integration for QS records. | BoQs, estimates, valuations, variations, certificates, retention releases, claims, and final accounts use shared workflow controls and direct-API enforcement. |

### Phase 1 - BoQ Standards, Versioning, Import, And Comparison

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0101 | P0 | In progress | Extend BoQ model for QS hierarchy and standards. | Project, section, trade, cost code, work package, SMM/CESMM code, measurement rule, and sort order persist/display/import/export. |
| QS-0102 | P0 | In progress | Seed BoQ code and work classification catalogues. | SMM7/CESMM/TDC catalogues are effective-dated, searchable, reportable, and controlled by admin permissions. |
| QS-0103 | P0 | In progress | Build BoQ Excel/standard import/export staging. | Imports validate codes, units, quantities, formulas, duplicates, locked tenderer cells, and signed reconciliation before posting. |
| QS-0104 | P0 | In progress | Implement BoQ versioning and comparison. | Original, approved, tender, revised, remeasurement, terminated/repackaged, and final-account versions compare by line and quantity. |
| QS-0105 | P0 | In progress | Add BoQ approval workflow and immutable published versions. | BoQ cannot be used for tender/valuation/certificate until approved; published versions are immutable except through revision workflow. |
| QS-0106 | P1 | In progress | Add contractor/tenderer BoQ submission and vetting intake. | External uploads/portal entries are validated, compared to tender BoQ, arithmetically checked, and audited. |

### Phase 2 - Rate Libraries, Rate Build-Up, Estimates, And Budgets

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0201 | P0 | In progress | Build centralized QS rate library. | Standard items, material rates, labour, plant/equipment, subcontract, contractor, supplier, region, source, date, and evidence persist. |
| QS-0202 | P0 | In progress | Add market survey update workflow. | Periodic price updates require evidence, reviewer approval, effective date, previous/new values, and audit. |
| QS-0203 | P1 | Not started | Link historical project costs to rate library. | Completed BoQs, valuations, procurement prices, and actual costs can be promoted into reusable rate history. |
| QS-0204 | P0 | Not started | Implement rate build-up engine. | Labour, material, plant, equipment, subcontract, overhead, profit, attendance, contingency, and custom components calculate unit rates. |
| QS-0205 | P0 | Not started | Add QS cost plan/tender estimate/budget estimate versions. | Estimate versions capture assumptions, markups, source rates, BoQ lines, review status, and approval history. |
| QS-0206 | P0 | Not started | Reconcile estimates with approved budgets and actuals. | Budget, committed, certified, actual, forecast, and variance values reconcile from line item to project summary. |

### Phase 3 - Price Adjustment, Fluctuation, And Escalation

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0301 | P0 | Not started | Build price adjustment formula register. | Formula, coefficient, index family, base date, contract clause, effective period, and authority are configurable. |
| QS-0302 | P0 | Not started | Add index import and approval workflow. | PBCI/Ghana Statistical Service and Roads/Infrastructure indices import with source evidence, validation, approval, and history. |
| QS-0303 | P0 | Not started | Implement escalation calculation runs. | Runs calculate base/revised rates, fluctuation amount, reviewer adjustments, and certificate/final-account impact. |
| QS-0304 | P1 | Not started | Add escalation dispute/audit pack. | Calculation inputs, version, reviewer notes, contractor response, attachments, and outcome are exportable. |

### Phase 4 - Measurement, Remeasurement, And Taking-Off

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0401 | P0 | Not started | Add taking-off sheet and measurement records. | Design/site measurement records link to BoQ line, drawing/design revision, dimensions, formula, measured quantity, and evidence. |
| QS-0402 | P0 | Not started | Add remeasurement and quantity revision workflow. | Remeasurements compare previous/current quantities, update BoQ revision, require approval, and preserve lineage. |
| QS-0403 | P0 | Not started | Implement contractor remeasurement request and joint measurement endorsement. | Request, scheduling, site evidence, QS review, consultant/contractor signature, approval, and audit work end to end. |
| QS-0404 | P1 | Not started | Add design revision/add-on linkage. | Revised drawings/designs identify affected BoQ lines and trigger measurement/variation workflow. |
| QS-0405 | P2 | Not started | Produce design/external quantity extraction integration contract. | CAD/BIM/design comparison and third-party estimating tools have documented file formats, ownership, security, and reconciliation. |

### Phase 5 - Valuation, Certificates, Variations, Claims, Contracts, And Final Account

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0501 | P0 | Not started | Add line-level valuation worksheets. | Measurement-to-date, previously certified, current claimed, current certified, disputed, retention, and net values reconcile per BoQ line. |
| QS-0502 | P0 | Not started | Complete interim valuation workflow. | Contractor claim, QS vetting, consultant endorsement, supporting evidence, approval, status, and certificate readiness are enforced. |
| QS-0503 | P0 | Not started | Complete payment certificate workflow. | Certificate generation, previous certificate, deductions, retention, advance recovery, AP handoff, payment status, and audit are controlled. |
| QS-0504 | P0 | Not started | Implement retention ledger and release rules. | Retention held/released by certificate, practical completion, sectional takeover, defects liability, and final release reconcile. |
| QS-0505 | P0 | Not started | Implement advance recovery ledger. | Advance amount, recovery percentage, certificate deductions, balance, and payment impact are auditable. |
| QS-0506 | P0 | Not started | Complete final account automation. | Approved BoQ, revisions, variations, claims, deductions, escalation, certificates, retention, and payments reconcile to final account. |
| QS-0507 | P0 | Not started | Complete material-on-site/off-site and TDC-supplied material deduction workflow. | Inventory/Stores issues, off-site evidence, quantities, values, certificate deductions, and contractor reconciliation match. |
| QS-0508 | P0 | Not started | Complete variation/change-order/daywork workflow. | Site instruction/change request, valuation, approval, budget/forecast/contract update, certificate impact, and audit are enforced. |
| QS-0509 | P0 | Not started | Implement contractor claims and QS vetting. | Contractor submission, claim type, amount, evidence, review, approved/rejected value, dispute status, and settlement are tracked. |
| QS-0510 | P1 | Not started | Add daywork sheets and additional works register. | Labour/material/plant daywork evidence, rates, signatures, valuation, and certificate inclusion are controlled. |
| QS-0511 | P0 | Not started | Automate approved variation updates. | Approved variations update revised contract sum, BoQ version, budget revision, forecast version, valuation, certificate, and final account as configured. |
| QS-0520 | P1 | Not started | Extend contract commercial terms for QS. | Provisional sums, contingencies, defects liability, retention clauses, sectional takeover, subcontract terms, and claim clauses are configurable. |
| QS-0521 | P1 | Not started | Implement subcontractor valuations/certificates. | Subcontractor claim, valuation, certificate, back charge, contra charge, payment, and final account work end to end. |
| QS-0522 | P1 | Not started | Add back-charge and contra-charge workflow. | Notice, evidence, approval, deduction, subcontractor/contractor communication, and certificate impact are auditable. |

### Phase 6 - Reports, Dashboards, Integrations, And Security

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0601 | P1 | Not started | Deliver TDC QS report catalogue. | BoQ summaries, valuation statements, variation logs, final accounts, project cost status, and certificate registers reconcile. |
| QS-0602 | P1 | Not started | Deliver QS cost dashboard and drilldown. | Budget, commitments, certified value, actual cost, variations, forecast, final projected cost, and line drilldown are visible by authority. |
| QS-0603 | P1 | Not started | Add custom report builder filters/templates for QS. | Users with permission can create reusable reports with project, contract, cost code, BoQ line, contractor, and period filters. |
| QS-0604 | P0 | Not started | Produce QS interface control document. | Procurement, Inventory, Projects, Contracts, AP, AR, GL, workflow, DMS, contractor portal, and external tools have owners and controls. |
| QS-0605 | P0 | Not started | Automate commitments, actuals, certificate, invoice, payment, and GL reconciliation. | PO, GRN, inventory issue, certificate, invoice, payment, retention, and reversal flows are idempotent and balanced. |
| QS-0606 | P0 | Not started | Enforce QS security and direct-API hard stops. | Unauthorized BoQ edits, valuation approvals, certificate changes, rate updates, and report drilldowns are rejected and audited. |

### Phase 7 - Migration, Training, UAT, And Go-Live

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0701 | P1 | Not started | Build QS migration and data-quality workbench. | BoQ, rates, contracts, valuations, certificates, variations, claims, final accounts, and physical file indexes validate before posting. |
| QS-0702 | P1 | Not started | Execute QS migration rehearsal. | Row counts, monetary totals, contract sums, certificate totals, retention balances, and physical-file references reconcile. |
| QS-0703 | P2 | Not started | Prepare QS role-based training and manuals. | QS, Development, Procurement, Finance, Audit, contractors, consultants, and management training materials are approved. |
| QS-0704 | P1 | Not started | Run end-to-end QS UAT. | Representative BoQ, estimate, rate, valuation, certificate, variation, claim, material deduction, final account, report, and integration scenarios pass. |
| QS-0705 | P1 | Not started | Define go-live support and acceptance plan. | Owners, dates, support channels, 24/7 on-site support period, issue SLA, rollback, and signatories are approved. |

## Mandatory End-To-End Acceptance Scenarios

| Scenario ID | Status | Scenario |
| --- | --- | --- |
| QS-E2E-001 | Not started | Design-based BoQ -> taking-off -> rate build-up -> peer review -> BoQ approval -> immutable approved BoQ version. |
| QS-E2E-002 | Not started | Excel/standard BoQ import -> validation -> secured tenderer export -> contractor submission -> arithmetic check -> QS vetting. |
| QS-E2E-003 | Not started | Market survey update -> rate library approval -> rate build-up -> tender estimate -> approved budget. |
| QS-E2E-004 | Not started | Cost plan/tender estimate -> budget revision -> approved budget -> committed cost -> actual cost -> variance dashboard. |
| QS-E2E-005 | Not started | Imported GSS/PBCI or road index -> escalation formula run -> reviewed fluctuation -> certificate/final-account impact -> audit pack. |
| QS-E2E-006 | Not started | Site measurement -> taking-off sheet -> BoQ quantity update -> approval -> valuation worksheet. |
| QS-E2E-007 | Not started | Contractor remeasurement request -> joint site measurement -> consultant/contractor endorsement -> QS approval -> revised quantity. |
| QS-E2E-008 | Not started | Contractor interim claim -> QS vetting -> valuation -> retention calculation -> payment certificate -> AP/payment status. |
| QS-E2E-009 | Not started | Practical completion or sectional takeover -> retention partial release -> certificate/payment -> retention ledger balance. |
| QS-E2E-010 | Not started | TDC material issue/off-site material evidence -> contractor reconciliation -> certificate deduction -> Inventory/GL reconciliation. |
| QS-E2E-011 | Not started | Site instruction/change order -> variation valuation -> approval -> revised contract sum/budget/forecast/certificate impact. |
| QS-E2E-012 | Not started | Subcontractor valuation -> certificate -> back/contra charge -> payment -> subcontract final account. |
| QS-E2E-013 | Not started | QS report dashboard reproduces BoQ, valuation, variation, certificate, actual, forecast, and final projected cost from source records. |
| QS-E2E-014 | Not started | Terminated contract -> compare original/executed/outstanding quantities -> repackage BoQ -> final account reconciliation. |
| QS-E2E-015 | Not started | Approved payment certificate -> AP invoice/payment/GL integration -> payment progress visible to QS without manual follow-up. |
| QS-E2E-016 | Not started | Direct API attempts for unauthorized BoQ, rate, valuation, certificate, variation, retention, and report access are rejected and audited. |

## Current Code Evidence Anchors

These anchors justify the baseline classifications; they are not proof of final TDC acceptance:

- Project entities: `src/ErpSystem.Core/Entities/Projects/ProjectManagementEntities.cs`
- Project DTOs: `src/ErpSystem.Core/DTOs/Projects/ProjectManagementDtos.cs`, `src/ErpSystem.Core/DTOs/Projects/ProjectManagementSummaryDtos.cs`
- Project service interface: `src/ErpSystem.Core/Interfaces/Projects/IProjectServices.cs`
- Project services: `src/ErpSystem.Core/Services/Projects/ProjectServices.cs`, `ProjectService.CommercialAdministration.cs`, `ProjectService.FinalAccountAutomation.cs`, `ProjectService.FinancialControl.cs`, `ProjectService.MaterialCosts.cs`, `ProjectService.ConstructionReports.cs`, `ProjectService.DesignSiteControls.cs`, `ProjectService.PackageCommercialDerivation.cs`, `ProjectService.PackageWorkflowSync.cs`
- Project API: `src/ErpSystem.Api/Controllers/Projects/ProjectsController.cs`
- Project frontend service: `frontend/src/services/projectService.ts`
- Project workspace routes/components: `frontend/src/app/development/projects/[id]/**`
- Project BoQ/budgeting UI: `frontend/src/app/development/projects/[id]/components/ProjectPackagesTab.tsx`, `ProjectBudgetingTab.tsx`, `frontend/src/components/projects/ProjectPackageDialogs.tsx`
- Project commercial admin UI: `frontend/src/app/development/projects/[id]/components/ProjectCommercialAdminTab.tsx`
- Project reports UI: `frontend/src/app/development/project-reports/page.tsx`, `frontend/src/app/development/project-analytics/page.tsx`
- Procurement contracts: `src/ErpSystem.Core/Entities/Procurement/ContractEntities.cs`, `src/ErpSystem.Core/Services/Procurement/ContractService.cs`, `frontend/src/services/contractService.ts`
- Tender/procurement price touchpoints: `src/ErpSystem.Core/Entities/Procurement/TenderEntities.cs`, `src/ErpSystem.Core/Services/Procurement/TenderBidService.cs`, `TenderAwardService.cs`, `RfqService.cs`
- Inventory/project material cost touchpoints: `src/ErpSystem.Core/Services/Inventory/InventoryValuationService.cs`, `src/ErpSystem.Core/Services/Projects/ProjectService.MaterialCosts.cs`
- Finance project/budget touchpoints: `src/ErpSystem.Api/Controllers/Finance/CapitalProjectsController.cs`, `src/ErpSystem.Api/Controllers/Finance/BudgetController.cs`, `src/ErpSystem.Api/Services/Finance/Budget/BudgetService.cs`
- Workflow adapters: `src/ErpSystem.Core/Services/Workflow/WorkflowStatusAdapters.cs`
- Project tests: `tests/ErpSystem.Api.Tests/Controllers/Projects/**`, `tests/ErpSystem.Core.Tests/Services/Projects/**`, `tests/ErpSystem.Core.Tests/Services/Workflow/ProjectWorkflowStatusAdapterTests.cs`
- QS configuration domain and decision registry: `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyConfigurationEntities.cs`, `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyConfigurationDecisionRegistry.cs`, `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyConfigurationLifecyclePolicy.cs`
- QS shared audit event map and coverage contribution: `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyAuditEventMap.cs`, `src/ErpSystem.Core/Services/Audit/AuditOperationClassifier.cs`
- QS tenant-safe lifecycle service and API: `src/ErpSystem.Data/Services/QuantitySurveyConfigurationService.cs`, `src/ErpSystem.Api/Controllers/QuantitySurvey/QuantitySurveyConfigurationProfilesController.cs`
- QS central access-control and tenant seed integration: `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyAccessControlRegistry.cs`, `src/ErpSystem.Data/Seeders/QuantitySurveyAccessControlSeeder.cs`, `src/ErpSystem.Data/Seeders/QuantitySurveyConfigurationProfileSeeder.cs`, `src/ErpSystem.Core/Services/Projects/ProjectService.Authorization.cs`
- QS shared-workflow binding and status adapter: `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyWorkflowBindingRegistry.cs`, `src/ErpSystem.Core/Services/Workflow/WorkflowStatusAdapterRegistry.cs`, `src/ErpSystem.Data/Services/QuantitySurveyConfigurationService.cs`
- QS configuration migration and model: `src/ErpSystem.Data/Migrations/20260807202000_AddQuantitySurveyConfigurationRegister.cs`, `src/ErpSystem.Data/ApplicationDbContext.QuantitySurvey.cs`
- QS shared-control frontend: `frontend/src/app/administration/project-management/quantity-survey-config/**`, `frontend/src/components/quantity-survey/QuantitySurveyDecisionEditor.tsx`, `frontend/src/components/configuration/ControlledDecisionFields.tsx`
- QS Phase 0 tests: `tests/ErpSystem.Core.Tests/Services/QuantitySurvey/QuantitySurveyConfigurationRegistryTests.cs`, `tests/ErpSystem.Core.Tests/Services/QuantitySurvey/QuantitySurveyAuditEventMapTests.cs`, `tests/ErpSystem.Core.Tests/Services/QuantitySurvey/QuantitySurveyAccessControlRegistryTests.cs`, `tests/ErpSystem.Core.Tests/Services/QuantitySurvey/QuantitySurveyWorkflowBindingRegistryTests.cs`, `tests/ErpSystem.Api.Tests/Controllers/QuantitySurvey/QuantitySurveyConfigurationControllerSecurityTests.cs`
- QS-0101 controlled BoQ hierarchy and standards: `src/ErpSystem.Core/Services/Projects/ProjectCatalogDefaults.cs`, `src/ErpSystem.Core/Services/Projects/ProjectService.Packages.cs`, `src/ErpSystem.Api/Controllers/Projects/ProjectsController.cs`, `frontend/src/components/projects/ProjectPackageDialogs.tsx`, `frontend/src/app/development/projects/[id]/components/ProjectPackagesTab.tsx`, `frontend/src/app/development/projects/[id]/components/ProjectBudgetingTab.tsx`
- QS-0101 additive migration and model: `src/ErpSystem.Data/Migrations/20260808023000_ExtendProjectBoqQuantitySurveyClassifications.cs`, `src/ErpSystem.Data/Configuration/Projects/ProjectManagementConfiguration.cs`
- QS-0101 focused tests: `tests/ErpSystem.Core.Tests/Services/Projects/ProjectServiceTests.cs`, `tests/ErpSystem.Api.Tests/Controllers/Projects/ProjectsControllerRouteTests.cs`, `tests/ErpSystem.Api.Tests/Controllers/QuantitySurvey/ProjectBoqQuantitySurveySecurityTests.cs`
- QS-0102 governed catalogue API and shared storage: `src/ErpSystem.Api/Controllers/QuantitySurvey/QuantitySurveyCataloguesController.cs`, `src/ErpSystem.Core/Services/Projects/ProjectServices.cs`, `src/ErpSystem.Core/Services/Projects/ProjectCatalogDefaults.cs`
- QS-0102 dedicated administration, search, and export: `frontend/src/app/administration/project-management/quantity-survey-catalogues/page.tsx`, `frontend/src/components/quantity-survey/QuantitySurveyCatalogueAdminPage.tsx`, `frontend/src/services/quantity-survey-catalogue.service.ts`
- QS-0102 focused tests: `tests/ErpSystem.Core.Tests/Services/QuantitySurvey/QuantitySurveyCatalogueDefaultsTests.cs`, `tests/ErpSystem.Api.Tests/Controllers/QuantitySurvey/QuantitySurveyCatalogueSecurityTests.cs`
- QS-0103 protected spreadsheet staging API and service: `src/ErpSystem.Api/Controllers/QuantitySurvey/QuantitySurveyBoqSpreadsheetController.cs`, `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyBoqSpreadsheetService.cs`, `src/ErpSystem.Core/Interfaces/QuantitySurvey/IQuantitySurveyBoqSpreadsheetService.cs`
- QS-0103 central-DMS staging model and migration: `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyConfigurationEntities.cs`, `src/ErpSystem.Data/ApplicationDbContext.QuantitySurvey.cs`, `src/ErpSystem.Data/Migrations/20260808035054_AddQuantitySurveyBoqImportStaging.cs`
- QS-0103 shared project-workspace frontend and focused tests: `frontend/src/components/quantity-survey/QuantitySurveyBoqImportDialog.tsx`, `frontend/src/app/development/projects/[id]/components/ProjectPackagesTab.tsx`, `frontend/src/services/projectService.ts`, `tests/ErpSystem.Api.Tests/Controllers/QuantitySurvey/QuantitySurveyBoqSpreadsheetSecurityTests.cs`
- QS-0104 immutable snapshot domain and lineage model: `src/ErpSystem.Core/Entities/Projects/ProjectBoqVersionEntities.cs`, `src/ErpSystem.Core/Entities/Projects/ProjectManagementEntities.cs`, `src/ErpSystem.Data/ApplicationDbContext.QuantitySurvey.cs`, `src/ErpSystem.Data/Migrations/20260808055534_AddProjectBoqVersionSnapshots.cs`
- QS-0104 tenant/project service, comparison, API, and audit map: `src/ErpSystem.Core/Services/Projects/ProjectService.BoqVersions.cs`, `src/ErpSystem.Core/Interfaces/Projects/IProjectServices.cs`, `src/ErpSystem.Api/Controllers/Projects/ProjectsController.cs`, `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyAuditEventMap.cs`
- QS-0104 shared Project workspace UI and focused tests: `frontend/src/components/quantity-survey/QuantitySurveyBoqVersionDialog.tsx`, `frontend/src/app/development/projects/[id]/components/ProjectPackagesTab.tsx`, `frontend/src/services/projectService.ts`, `tests/ErpSystem.Core.Tests/Services/Projects/ProjectServiceTests.cs`, `tests/ErpSystem.Api.Tests/Controllers/QuantitySurvey/ProjectBoqQuantitySurveySecurityTests.cs`
- QS-0105 shared-workflow lifecycle and immutable publication service: `src/ErpSystem.Core/Services/Projects/ProjectService.BoqApproval.cs`, `src/ErpSystem.Core/Services/Projects/ProjectService.BoqVersions.cs`, `src/ErpSystem.Api/Services/SimpleWorkflowService.cs`, `src/ErpSystem.Core/Interfaces/Projects/IProjectServices.cs`
- QS-0105 API, audit, authorization, and shared Project workspace controls: `src/ErpSystem.Api/Controllers/Projects/ProjectsController.cs`, `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyAuditEventMap.cs`, `src/ErpSystem.Core/Services/Audit/AuditOperationClassifier.cs`, `frontend/src/components/quantity-survey/QuantitySurveyBoqVersionDialog.tsx`, `frontend/src/services/projectService.ts`
- QS-0105 additive publication migration and focused tests: `src/ErpSystem.Data/Migrations/20260808124500_AddProjectBoqApprovalPublication.cs`, `src/ErpSystem.Data/ApplicationDbContext.QuantitySurvey.cs`, `tests/ErpSystem.Core.Tests/Services/Projects/ProjectServiceTests.cs`, `tests/ErpSystem.Core.Tests/Services/QuantitySurvey/QuantitySurveyAuditEventMapTests.cs`, `tests/ErpSystem.Api.Tests/Controllers/QuantitySurvey/ProjectBoqQuantitySurveySecurityTests.cs`

## Verification Log

| Date | Check | Status | Result |
| --- | --- | --- | --- |
| 2026-07-09 | Initial source render | Superseded | The initial rendering path produced only three page images and is not the authoritative pagination; the complete 15-page Microsoft Word render and review was completed on 2026-07-17. |
| 2026-07-09 | Structured DOCX extraction | Passed | 2 paragraphs and 1 table with 43 rows were extracted, including all QS requirement rows and comments. |
| 2026-07-09 | Rendered page visual contact review | Passed | Contact sheet confirmed the source document structure and rendered questionnaire table page. |
| 2026-07-09 | Live source audit | Passed | Project entities, DTOs, services, controllers, frontend workspace, commercial admin UI, BoQ/budget UI, contracts, material ledger, reports, workflow adapters, and tests were inspected. |
| 2026-07-09 | Tracker creation | Passed | Full coverage matrix, configuration inputs, roadmap, acceptance scenarios, requirement traceability, and code anchors created. |
| 2026-07-17 | Full source pagination and visual review | Passed | The original questionnaire was re-rendered through Microsoft Word as 15 pages; all 43 table rows, including 30 substantive response rows and their comments, were visually reviewed. |
| 2026-07-17 | Questionnaire-to-tracker reconciliation | Passed | Added direct row traceability for every substantive client response and confirmed coverage of secured tenderer files, arithmetic checks, personnel handover, sectional retention release, off-site materials, external submissions, records by name/year, and temporary 24/7 support. |
| 2026-07-17 | Live implementation revalidation | Passed | Rechecked project authorization and external access, BoQ/package structures, commercial administration, final-account automation, material-cost synchronization, reports, workflow adapters, controllers, frontend controls, and focused tests. |
| 2026-07-17 | Tracker integrity audit | Passed | Corrected the undefined contract-administration traceability range; all referenced configuration, roadmap, and acceptance identifiers now resolve to defined tracker rows. |
| 2026-08-07 | Phase 0 overlap and boundary audit | Passed | Reused central Security/RBAC, workflow lookups, DMS evidence, report/template lookups, project types, locations, and currencies; no BoQ, valuation, certificate, procurement, inventory, or Finance runtime behavior was changed. |
| 2026-08-07 | QS configuration, audit-map, access-registry, and workflow-binding tests | Passed | Focused Core suite passed 23 of 23 tests covering all 17 mappings, canonical enum/UI values, controlled field schemas, strict payload validation, lifecycle immutability, deleted-family version continuation, clone approval/evidence reset, tenant isolation, independent approval, current-published DMS evidence, idempotent multi-tenant seeding, registered audit actions, shared operation classification, required audit facets, explicit source identity, unmapped-action rejection, permission/operation integrity, authority-gated approval, central RBAC seeding, shared workflow adapter transitions, workflow entity seeding, and server-side rejection of a published workflow from the wrong QS entity family. |
| 2026-08-07 | QS configuration API authorization tests | Passed | Focused API suite passed 6 of 6 tests; every HTTP action requires authentication and the read, manage, approve, and audit endpoints carry their explicit central authorization policies. |
| 2026-08-07 | QS frontend source parse and formatting | Passed | Prettier parsed and normalized all six new Quantity Survey frontend route, shared-control, service, and type files without syntax errors; shared team files were not bulk-formatted. |
| 2026-08-07 | Test-database migration apply and schema verification | Passed | `20260807202000_AddQuantitySurveyConfigurationRegister` is recorded in `RhemaERP`; SQL verification found 4 QS tables, 7 checks, 19 indexes, the append-only revision trigger, 1 tenant Draft profile, 17 decision rows, and 1 initial revision. Startup seeding also produced 4 QS permissions, 3 QS roles, and 8 role-permission assignments. |
| 2026-08-07 | API hard-stop smoke | Passed | Temporary API startup on `127.0.0.1:5110` completed against the configured database and an unauthenticated QS schema request returned `401`, confirming the endpoint is not anonymously accessible. |
| 2026-08-07 | QS foundation compile and focused verification | Passed | Core, Data, and API builds completed with 0 errors; the QS API authorization suite passed 6 of 6; targeted QS ESLint and Prettier checks passed; and QS-scoped TypeScript passed. The full frontend type-check still reports 26 unrelated baseline errors in other modules. |
| 2026-08-07 | QS-0003 shared audit-map foundation | Passed for implemented QS configuration lifecycle | Registered 13 QS configuration actions with the existing shared audit-governance coverage service. Immutable revisions now expose operation plus explicit source type/ID and preserve actor, roles, before/after values, typed decision/formula payload, DMS evidence identifiers, approval state, and correlation. `QS-0003` remains `In progress` until the later BoQ, rate, valuation, certificate, cancellation, and financial-adjustment services emit their mapped events. |
| 2026-08-07 | QS-0004 central RBAC and authority-map foundation | Passed for registry and seed behavior | Expanded the shared Security role/permission model to governed QS work areas and defined an operation map. Transaction approval requires the central permission plus project membership and the role/amount authority selected in effective `QS-DEC-001`; no separate QS role or authority table was added. `QS-0004` remains `In progress` until project/contract/section/unit enforcement is wired into the later runtime services and role-based API/browser scenarios pass. |
| 2026-08-07 | QS-0005 shared-workflow foundation | Passed for binding, discovery, seed, and configuration enforcement | Reused the central workflow engine and status-adapter registry for 11 QS record families. Effective configuration now exposes only active, current Published workflow definitions from the exact entity family required by each decision field, and the service rejects inactive, unpublished, cross-tenant, missing, or wrong-entity IDs even when submitted directly to the API. `QS-0005` remains `In progress` until the later BoQ, estimate, valuation, variation, certificate, retention, claim, and final-account runtime records implement the shared QS workflow contract and end-to-end direct-API scenarios pass. |
| 2026-08-07 | Full frontend baseline and interactive browser smoke | Outstanding | The QS frontend scope is clean, but the full workspace still has 26 unrelated TypeScript errors and interactive authenticated browser smoke has not been rerun. `QS-0001` and `QS-0002` remain `In progress` until browser evidence and tenant-approved configuration/evidence are complete. |
| 2026-08-08 | QS-0101 existing-foundation and boundary audit | Passed | Extended the existing Project/ProjectPackage/ProjectBoqItem/ProjectCatalogEntry service, API, admin, workspace, and export surfaces. No parallel QS BoQ store, access table, workflow engine, procurement runtime, inventory runtime, or Finance posting path was introduced. |
| 2026-08-08 | QS-0101 controlled hierarchy, standards, and authorization | Passed for implemented slice | BoQ lines use their existing work package and now select tenant-safe effective section, trade, cost code, and measurement code records through searchable controlled selectors. Immutable code/name/standard/rule snapshots are retained on lines; `QS-DEC-002` enforces applicable trade, cost-code, and allowed-standard policy; reads require `quantity-survey.workspace.read`, mutations require `quantity-survey.boq.manage`, and central project membership still applies. |
| 2026-08-08 | QS-0101 focused backend/API/frontend verification | Passed | Core tests passed 3 of 3 for controlled snapshot/default behavior, cross-tenant rejection, and effective `QS-DEC-002` enforcement. API route/authorization tests passed 7 of 7. Core, Data, API, and both test projects compiled with 0 errors; targeted ESLint passed for all changed BoQ/admin/service files; `git diff --check` passed apart from line-ending notices. |
| 2026-08-08 | QS-0101 configured test-database apply and schema probe | Passed | Applied `20260808023000_ExtendProjectBoqQuantitySurveyClassifications` to the configured user-secret SQL database. Direct SQL verification returned 2/2 QS migrations, 5/5 Project catalogue metadata columns, 13/13 BoQ classification snapshot columns, 4/4 catalogue foreign keys, and 1/1 effective-period check constraint. |
| 2026-08-08 | QS-0101 model-drift and runtime/browser gates | Outstanding | EF confirms repository-wide pending model changes outside the explicit QS migration boundary; QS-0103 now provides controlled staging/import coverage without absorbing that drift. The required in-app browser runtime remains unavailable. `QS-0101` remains `In progress` until authenticated browser/API smoke and the repository-wide model baseline are closed. |
| 2026-08-08 | QS-0102 overlap, storage, and authorization boundary | Passed for implemented slice | Reused tenant-owned `ProjectCatalogEntry`, the central unit-of-measure master, the existing audit interceptor, and central QS read/manage permissions. A dedicated `/api/quantity-survey/catalogues` surface prevents the generic project-admin API from bypassing QS permission controls; no parallel catalogue store, UOM master, role table, or audit subsystem was added. |
| 2026-08-08 | QS-0102 effective, searchable, reportable administration | Passed for implemented slice | The dedicated settings workspace supports the four governed catalogue families, required effective-from dates, optional end dates, active/inactive state, controlled SMM7/CESMM3/CESMM4/TDC Local selection, controlled UOM lookup, search, as-of/standard/status filters, and CSV/XLSX export. Catalogue type cannot be changed during update, and generic default seeding explicitly excludes QS catalogues. |
| 2026-08-08 | QS-0102 focused verification | Passed | Core build completed with 0 errors; Core tests passed 8 of 8 for registered families, absence of unlicensed bundled data, normalized type recognition, effective/standard/search/status filtering, required effective dates, and generic-route bypass rejection. API permission tests passed 5 of 5. Targeted ESLint and Prettier passed, full TypeScript reported no QS errors while retaining unrelated Finance/Inventory/Maintenance/Reports baseline errors, and `git diff --check` passed apart from line-ending notices. |
| 2026-08-08 | QS-0102 approved-data and smoke gates | Outstanding | No proprietary SMM7/CESMM catalogue content or unapproved TDC classifications were fabricated. QS-0103 now provides the controlled template/validation/posting path, but `QS-CFG-002` must still provide licensed/approved source data and evidence for loading; authenticated API/browser smoke remains required. `QS-0102` therefore remains `In progress` and is not acceptance-ready. |
| 2026-08-08 | QS-0103 existing-foundation and control-boundary audit | Passed | Reused the existing project/package/BoQ service for membership and `QS-DEC-002`, central QS permissions, tenant UOM/currency/catalogue masters, shared spreadsheet security inspector, controlled file scanning, central DMS repository, EF transaction, and central exception/audit infrastructure. No parallel BoQ store, document repository, role table, workflow engine, procurement behavior, inventory behavior, or Finance posting path was introduced. |
| 2026-08-08 | QS-0103 protected template, preview, and signed posting | Passed for implemented slice | The server-generated `.xlsx` template binds tenant/project identity and version through a protected control token; locks identity and total-formula cells; supplies controlled package, catalogue, unit, currency, and item-type lookups; rejects macros, external relationships, unexpected formulas, changed locked cells, invalid codes/units/quantities/rates, duplicates, and stale references; retains the original clean-scanned workbook and SHA-256 in central DMS; and posts only after the same authenticated user confirms a fixed reconciliation declaration. Commit revalidates inside a transaction, verifies payload integrity, and supports safe idempotent/concurrent retry. |
| 2026-08-08 | QS-0103 frontend, authorization, and focused verification | Passed | The existing Work Components & BOQ workspace now exposes template download, protected export, staged import, validation findings/report, line preview, and signed posting through a reusable QS component. Template/preview/commit/report routes require `quantity-survey.boq.manage`; export requires `quantity-survey.workspace.read`; focused API tests passed 12 of 12; API builds completed with 0 errors; targeted Prettier/ESLint passed; and full TypeScript reported no QS diagnostics while retaining unrelated Finance/Inventory/Maintenance/Reports baseline errors. |
| 2026-08-08 | QS-0103 narrow migration and configured test-database apply | Passed | The generated migration was reviewed and seven unrelated Finance precision operations were removed from both migration directions and target/snapshot metadata. Applied `20260808035054_AddQuantitySurveyBoqImportStaging` through the configured user-secret SQL connection. Direct SQL probes confirm the migration row, 34-column staging table, 8 indexes including filtered tenant idempotency, 5 restrictive foreign keys, and 2 enabled/trusted check constraints. |
| 2026-08-08 | QS-0103 authenticated runtime acceptance | Outstanding | Authenticated API/browser round-trip remains required for template download, valid preview, invalid workbook report, signed commit, DMS evidence visibility, audit visibility, permission denial, and idempotent retry. `QS-0103` remains `In progress` and is not marked `Done` until this smoke evidence and the repository-wide EF baseline gate are closed. |
| 2026-08-08 | QS-0104 existing-foundation and control-boundary audit | Passed | Extended the existing Project BoQ aggregate, project membership/operation authorization, QS configuration decision `QS-DEC-003`, central audit interceptor/event map, and Project workspace. No parallel editable BoQ store, workflow engine, role table, document repository, Procurement runtime, Inventory runtime, or Finance posting path was added. Direct creation of an Approved snapshot is rejected so approval remains owned by `QS-0105` and the shared workflow. |
| 2026-08-08 | QS-0104 immutable lineage and comparison implementation | Passed for implemented slice | Added stable `VersionLineKey` lineage to live BoQ rows and immutable tenant/project-scoped snapshots for Original, Tender, Revised, Remeasurement, Terminated/Repackaged, and Final Account states. Creation verifies a SHA-256 working-set hash, serializes version numbering, copies classification/measurement/package snapshots, records actor roles/action/correlation, and compares versions by stable line key with added/removed/changed/unchanged status plus quantity, rate, value, and currency-total deltas. |
| 2026-08-08 | QS-0104 frontend, authorization, build, and focused tests | Passed | The existing Work Components & BOQ toolbar now opens reusable version history, create-snapshot, immutable-detail, and comparison dialogs. Workspace/detail/compare routes require `quantity-survey.workspace.read`; create requires `quantity-survey.boq.manage`. Targeted Prettier/ESLint passed, the consolidated API build completed with 0 errors, focused service/audit tests passed 7 of 7, and focused API authorization/route tests passed 9 of 9. |
| 2026-08-08 | QS-0104 narrow migration and configured test-database apply | Passed | EF's first diff was rejected because the fast startup assembly proposed a full-schema recreation; that unsafe artifact was deleted. The replacement migration contains only the live-line lineage column/backfill, two immutable snapshot tables, and their constraints/indexes. Applied `20260808055534_AddProjectBoqVersionSnapshots` through the configured user-secret connection. Direct SQL confirms the lineage column, both tables, and exactly one migration-history row. |
| 2026-08-08 | QS-0104 local API/page smoke | Passed with authenticated browser gate outstanding | A temporary local API returned `401` for the new project BoQ-version route without credentials, proving routing and authorization enforcement, and the Next.js Project workspace compiled and returned HTTP 200. The in-app browser runtime was unavailable, so authenticated role/project UI creation, comparison, audit visibility, and direct cross-tenant denial remain required. Temporary API/frontend listeners were stopped and ports 5098/3098 were confirmed clear. `QS-0104` remains `In progress`. |
| 2026-08-08 | QS-0105 existing-foundation and control-boundary audit | Passed | Reused the existing Project BoQ snapshot aggregate, central `QS_BOQ` workflow adapter/engine, effective `QS-DEC-003` profile, project membership and authority checks, central audit interceptor/event map, and Project workspace. No parallel workflow, role, audit, BoQ, Procurement, Inventory, tender, valuation, certificate, or Finance runtime was introduced. |
| 2026-08-08 | QS-0105 governed approval and publication lifecycle | Passed for implemented slice | Candidate versions submit only through the exact effective workflow definition configured by `QS-DEC-003`; server-side amount/currency/hash context drives central authority routing; approve, reject, and recall reject contradictory workflow outcomes; completed-workflow retry is recoverable; final approval creates a separate immutable Approved publication with copied stable line lineage; and replacement publication plus retirement is serialized and atomic. Downstream published-version retrieval fails closed when no approved publication exists. |
| 2026-08-08 | QS-0105 authorization, audit, frontend, and focused verification | Passed | Read, submit/recall, and approve/reject routes enforce their explicit central QS policies. The shared Project BoQ-version dialog exposes permission-aware submit, approve, reject, recall, publication badges, and controlled revision sourcing. The consolidated API build completed with 0 errors; focused service/workflow/audit tests passed 12 of 12; focused API route/permission tests passed 14 of 14; targeted Prettier/ESLint passed; and `git diff --check` passed apart from line-ending notices. A later redundant no-build rerun exceeded the command time budget without a reported test failure and was stopped. |
| 2026-08-08 | QS-0105 configured test-database apply and schema verification | Passed | Applied `20260808124500_AddProjectBoqApprovalPublication` through the configured user-secret SQL connection. Direct SQL verification found all 5 lifecycle columns, both trusted lifecycle/status checks, the unique filtered current-publication index, both enabled immutability triggers, and exactly one migration-history row. The migration is discoverable as the latest entry in focused and normal builds. |
| 2026-08-08 | QS-0105 local API/page smoke | Passed with authenticated browser gate outstanding | A temporary local API returned `401` for the new BoQ-version route and the Next.js Project workspace compiled and returned HTTP 200. The general health endpoint remained `503` because an existing dependency was degraded, while the QS route and database migration were operational. The in-app browser execution tool was unavailable, so authenticated maker/checker, authority-limit, publication-replacement, audit-visibility, and direct cross-tenant scenarios remain required. Temporary API/frontend listeners were stopped and ports 5099/3099 were confirmed clear; `QS-0105` remains `In progress`. |
| 2026-08-08 | QS-0106 existing-foundation and overlap audit | Passed for implementation boundary | The existing Procurement tender/bid aggregate already owns external-portal identity-to-business-partner resolution, invited tender access, draft bid items, submission deadlines/receipts, document readiness, bidder documents, controlled scanning, central-DMS registration, and internal bid review pages. The Project requisition lineage already exposes `ProjectId`; approved QS publications and stable BoQ line keys are supplied by QS-0105; and `QS-DEC-013` already controls portal/Excel/API/PDF channels, extensions, size, identity, evidence, and signature. QS-0106 will extend these records with approved-publication binding, protected tenderer workbook staging, arithmetic/lineage vetting, and QS audit evidence rather than creating another bidder portal, bid store, identity model, file repository, workflow engine, or permission table. |
| 2026-08-08 | QS-0106 governed tenderer intake and existing-bid integration | Passed for implemented slice | Project-linked tender bids now resolve the current immutable Approved BoQ publication through the existing PR/project lineage. The existing bidder identity and `TenderBid`/`TenderBidItem` lifecycle remain authoritative. The external portal can download a protected `.xlsx`, stage it through shared spreadsheet security and mandatory clean scanning, retain the original in central DMS, validate locked tenant/bid/publication/line identities, units, quantities, formulas, completeness, duplicates, and arithmetic, confirm the configured `QS-DEC-013` reconciliation/signature controls, and commit validated quantities/rates back to existing bid items. Portal-entered bid lines pass the same approved-publication, evidence, completeness, total, and arithmetic gate during existing bid submission; non-project Procurement bids remain unchanged. |
| 2026-08-08 | QS-0106 vetting, authorization, audit, and frontend verification | Passed for implemented slice | The existing external bid-pricing step hosts the reusable tenderer-BoQ panel and the existing internal bid detail hosts line reconciliation/history. Internal history requires `quantity-survey.workspace.read`; final accept/reject requires `quantity-survey.transactions.approve`, optimistic row-version concurrency, a review note, terminal-outcome consistency, and idempotent same-outcome retry. Stage, commit, accept, and reject use the shared QS audit register and central audit classifier. The final API build completed with 0 errors; QS audit tests passed 7/7; API authorization/private-storage tests passed 21/21; protected-template, portal arithmetic/idempotency, and external-owner service tests passed 3/3; targeted Prettier/ESLint passed; and both changed Next.js bid pages compiled locally. The repository-wide TypeScript check exceeded its three-minute bound without emitting a QS diagnostic, so it was stopped rather than left consuming CPU. |
| 2026-08-08 | QS-0106 narrow migration and configured test-database apply | Passed | The first scaffold exposed unrelated Procurement column drops and QS-0105 lifecycle-column additions; all were removed from both migration directions before apply. Migration `20260808143557_AddQuantitySurveyTenderBoqSubmissions` contains only the two QS tender-BoQ header/line tables, indexes, foreign keys, and checks. It was applied through the configured user-secret SQL connection. Direct SQL probes returned 1/1 migration-history row, 2/2 tables, 7 enabled/trusted check constraints, 14 enabled foreign keys, and 21 named indexes. |
| 2026-08-08 | QS-0106 local API/page smoke and remaining acceptance | Passed with authenticated role gate outstanding | Temporary local instances returned HTTP `401` for both unauthenticated external/internal QS tender-BoQ route families and HTTP `200` for the existing external submit-bid and internal bid-detail pages after route compilation. The smoke caught and corrected an invalid auth-hook import before handoff. Temporary API/frontend listeners were stopped, ports 5106/3106 were confirmed clear, and .NET build servers were shut down. Authenticated valid/invalid workbook round-trip, external ownership denial, DMS/audit visibility, bidder submission retry, supervising-QS accept/reject, and cross-tenant scenarios remain required; `QS-0106` therefore remains `In progress` and is not marked `Done`. |
| 2026-08-08 | QS-0201 existing-foundation and ownership audit | Passed | Reused the shared Inventory `UnitOfMeasure` and item cost sources, Finance `Currency`, Project type and QS catalogues, HR locations, Procurement business partners, central DMS published-document versions, effective `QS-DEC-005`, central QS permissions, and the shared audit classifier. The implementation adds only the stable QS rate item, append-only effective-dated rate versions, and immutable revision evidence; it does not create parallel UOM, currency, supplier, contractor, location, inventory, document, role, workflow, Procurement transaction, Inventory transaction, or Finance posting stores. |
| 2026-08-08 | QS-0201 governed rate lifecycle and controls | Passed for implemented slice | Standard, material, labour, plant, equipment, and subcontract items retain controlled UOM/catalogue/inventory links. Draft rate versions retain currency, project type, location, supplier/contractor, source type/reference/date, published central-DMS evidence, effective period, maker, role, reason, row version, and correlation. `QS-DEC-005` validates enabled dimensions and allowed values; market sources fail closed when configured evidence is absent. Publication requires an independent checker, is serialized under the SQL retry strategy, rejects conflicting effective periods, preserves the current Published rate until a future replacement becomes effective, and supports idempotent terminal retries. Version allocation includes soft-deleted history. |
| 2026-08-08 | QS-0201 permission-aware shared-control frontend | Passed for implemented slice | Added the dedicated Project Management settings route `/administration/project-management/quantity-survey-rate-library` with search/category/as-of/status filters, export, item and rate history, controlled create/edit selectors, draft preparation, independent publish/retire actions, and immutable audit history. Read, prepare, approve, and audit affordances require `quantity-survey.workspace.read`, `quantity-survey.rates.manage`, `quantity-survey.transactions.approve`, and `quantity-survey.audit.read` respectively. No GUID master is entered as free text; only the external business reference and required change/decision reasons remain textual. |
| 2026-08-08 | QS-0201 focused build, test, migration, and SQL-diff evidence | Passed | Data fast-build completed with 0 errors after disabling the stale shared compiler. Focused service/controller tests passed 15/15 for tenant boundaries, SOD, future-effective continuity, DMS evidence, deleted-version sequencing, audit, routes, and permissions; shared QS audit-map tests passed 8/8. Targeted ESLint passed. The full TypeScript checker reported no QS error and retained unrelated Finance/Inventory/Maintenance/Reports baseline errors. The corrected migration script contains exactly 3 rate-library table creates, 0 alters, and 0 drops; `20260808155330_AddQuantitySurveyRateLibrary` was applied through the configured user-secret connection and EF reports the complete QS chain as applied. |
| 2026-08-08 | QS-0201 local API/page smoke and remaining acceptance | Passed with authenticated gate outstanding | The temporary API enforced HTTP `401` on both rate-library and lookup routes; the Next server recognized and compiled the dedicated route under the protected application surface. The seeded-admin scripted login was correctly blocked by the tenant CAPTCHA requirement, and the in-app browser execution runtime was unavailable, so CAPTCHA was not bypassed. Authenticated preparer/checker lifecycle, selector contents, audit visibility, direct cross-tenant denial, and visual browser evidence remain required. General health returned the pre-existing degraded `503`. Temporary listeners were terminated and ports 5188/3199 were confirmed clear; `QS-0201` remains `In progress` and is not marked `Done`. |
| 2026-08-08 | QS-0202 overlap audit and governed market-source integration | Passed | Reused Procurement's tenant-scoped, published `MarketAnalysis` and price-history quote lines as the market-survey source, central DMS published versions as evidence, effective `QS-DEC-005` dimensions/cadence, the existing QS rate Draft-to-Published independent-checker lifecycle, shared QS permissions, and the central audit classifier. No parallel market survey, supplier, contractor, currency, UOM, document, workflow, approval, or audit store was created. Generic rate create/update routes reject MarketSurvey mutations so the controlled preparation route cannot be bypassed. |
| 2026-08-08 | QS-0202 lifecycle, lineage, authorization, and frontend | Passed for implemented slice | A preparer selects a published Procurement analysis and existing rate item, then the service validates tenant ownership, quote completeness, controlled item UOM, currency, configured dimensions, survey/effective dates, cadence, and published DMS evidence. The Draft snapshots analysis identity, quote count, previous rate/currency, new value, next review date, evidence, maker/reason, and correlation; publication remains an independent permission-gated action and preserves current coverage for future-effective replacements. The shared rate-library page exposes controlled selectors and previous/new variance without accepting master-data GUIDs as free text, and hides generic editing for market-survey drafts. |
| 2026-08-08 | QS-0202 focused verification and configured-database apply | Passed | Targeted Prettier/ESLint passed. Focused tenant, evidence, authorization, lineage, direct-bypass, and route tests passed 18/18; Core, Data, API, and API-test project builds completed with 0 errors in their bounded verification runs. Migration SQL was reviewed before apply and contains only 7 nullable lineage columns on `QuantitySurveyRateLibraryRates`, 2 checks, 4 indexes, and 2 foreign keys, with 0 drops. `20260808172100_AddQuantitySurveyMarketSurveyLineage` was then applied through the configured user-secret SQL connection. |
| 2026-08-08 | QS-0202 remaining acceptance | Authenticated gate outstanding | Authenticated preparer/checker browser evidence must still prove permitted source visibility, mandatory evidence and date failures, previous/new values, SOD publication denial/approval, audit visibility, cross-tenant rejection, and downstream rate-history continuity. The tenant CAPTCHA blocks scripted seeded-admin login and is not bypassed. `QS-0202` therefore remains `In progress` and is not marked `Done`. |
| 2026-08-08 | QS-0202 final consolidated regression review | Verification rerun pending | The consolidated supplier/QS Core run compiled successfully and completed 24 focused tests: 21 passed and 3 review assertions exposed an audit-action classification ambiguity plus a raw JSON escaping assertion. The audit action was renamed to the unambiguous `CreateMarketSurveyRate`, and the supplier assertion was changed to parse JSON structurally. The repeated slow Core build was stopped at the user's request, its task-owned process tree was terminated, and the corrected final two changes remain queued for the next normal regression run rather than being represented as green. |

## Tracker Maintenance

- Start every implementation slice by selecting explicit `QS-*` task IDs.
- Keep this tracker synchronized when code changes alter baseline coverage.
- Do not mark a task `Done` until backend, frontend, configuration, workflow, audit, tests, migrations, and smoke evidence are present.
- Prefer configuration over hard-coded policy for QS standards, rate build-up, markups, escalation formulas, retention rules, material deduction rules, approval routes, report formulas, and access controls.
- Do not remove requirements from the tracker. Record an approved scope decision with evidence if TDC defers or changes a requirement.
