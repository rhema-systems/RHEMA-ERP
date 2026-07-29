# TDC Quantity Survey Gap Implementation Tracker

Last updated: 2026-07-17

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
| BOQ-002 | Partial | Project packages/work components and phase structures exist, but TDC-specific BoQ hierarchy by project, section, trade, cost code, work package, SMM7/CESMM section, and local TDC catalogue is not enforced. |
| BOQ-003 | Partial | BoQ item CRUD endpoints and UI exist in the Development project workspace. Creation/import/revision/approval/version control of original, tender, revised, and final-account BoQs is incomplete. |
| BOQ-004 | Partial | Budgeting export exists for BoQ worksheet data, but acceptance-ready BoQ Excel import/export with locked tenderer cells, standard templates, validation, and round-trip audit is incomplete. |
| BOQ-005 | Gap | Original BoQ, revised BoQ, tender BoQ, executed quantities, outstanding quantities after termination/re-award, and final account quantities are not managed as comparable effective versions. |
| BOQ-006 | Gap | External contractor quotation/claim submission and QS vetting against BoQ lines is not available as a controlled portal/upload workflow. |

### Rate Libraries, Market Survey, And Cost Database

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| RATE-001 | Partial | BoQ item rates, inventory item references, procurement prices, material cost ledger, and historical project actuals exist. No centralized QS rate library exists for standard items, historical costs, market surveys, and reusable rate assumptions. |
| RATE-002 | Gap | Rate libraries are not categorized by project type, region, contractor, supplier, time period, material, plant, equipment, or labour source. |
| RATE-003 | Gap | Periodic updates to basic material prices, plant/equipment fees/hire rates, labour rates, and supplier/contractor rates are not controlled with effective dates and approval. |
| RATE-004 | Partial | Entity audit fields exist, but a QS audit trail for rate changes, quantity changes, late information, cost assumptions, handover between personnel, and supporting market survey evidence is incomplete. |

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
| 2 | BoQ structure by project, section, trade, cost code, and work package | Partial. Project packages and BoQ items exist; QS cost-code/trade/standard structure is not enforced. | `QS-0101`, `QS-0102`, `QS-E2E-001` |
| 3 | BoQ line item codes, descriptions, UOM, quantity, rates, totals | Verified baseline for core fields; needs SMM/CESMM catalogue, validation, import, and audit. | `QS-0102`, `QS-0103`, `QS-E2E-001` |
| 4 | BoQ Excel/standard import/export and third-party estimation tools | Partial for frontend export; import/round-trip/external tool integration is missing. | `QS-0103`, `QS-0604`, `QS-E2E-002`, `QS-E2E-016` |
| 5 | Original/revised/tender/final account comparison | Gap. Needs comparable version snapshots and quantity/status lineage. | `QS-0104`, `QS-0504`, `QS-E2E-014` |
| 6 | Centralized QS database and rate libraries | Gap/Partial. Historical costs exist in scattered transactions, but no QS rate library. | `QS-0201` through `QS-0203`, `QS-E2E-003` |
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
| QS-0001 | P0 | Not started | Resolve `QS-CFG-001` through `QS-CFG-017` as effective-dated configuration. | Every configuration item has owner, approved value, effective date, evidence, and tenant seed plan. |
| QS-0002 | P0 | Not started | Build QS policy/config register. | BoQ standards, rate libraries, formulas, retention rules, material deductions, workflows, templates, and access rules are versioned and queryable. |
| QS-0003 | P0 | Not started | Define QS audit event map. | Every QS action records actor, role, source, before/after values, formula inputs, evidence links, approval state, and correlation ID. |
| QS-0004 | P0 | Not started | Configure TDC QS roles, permissions, and authority levels. | Test users can only see and act by role, project, contract, section/unit, and approval authority. |
| QS-0005 | P0 | Not started | Add shared workflow integration for QS records. | BoQs, estimates, valuations, variations, certificates, retention releases, claims, and final accounts use shared workflow controls and direct-API enforcement. |

### Phase 1 - BoQ Standards, Versioning, Import, And Comparison

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0101 | P0 | Not started | Extend BoQ model for QS hierarchy and standards. | Project, section, trade, cost code, work package, SMM/CESMM code, measurement rule, and sort order persist/display/import/export. |
| QS-0102 | P0 | Not started | Seed BoQ code and work classification catalogues. | SMM7/CESMM/TDC catalogues are effective-dated, searchable, reportable, and controlled by admin permissions. |
| QS-0103 | P0 | Not started | Build BoQ Excel/standard import/export staging. | Imports validate codes, units, quantities, formulas, duplicates, locked tenderer cells, and signed reconciliation before posting. |
| QS-0104 | P0 | Not started | Implement BoQ versioning and comparison. | Original, approved, tender, revised, remeasurement, terminated/repackaged, and final-account versions compare by line and quantity. |
| QS-0105 | P0 | Not started | Add BoQ approval workflow and immutable published versions. | BoQ cannot be used for tender/valuation/certificate until approved; published versions are immutable except through revision workflow. |
| QS-0106 | P1 | Not started | Add contractor/tenderer BoQ submission and vetting intake. | External uploads/portal entries are validated, compared to tender BoQ, arithmetically checked, and audited. |

### Phase 2 - Rate Libraries, Rate Build-Up, Estimates, And Budgets

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| QS-0201 | P0 | Not started | Build centralized QS rate library. | Standard items, material rates, labour, plant/equipment, subcontract, contractor, supplier, region, source, date, and evidence persist. |
| QS-0202 | P0 | Not started | Add market survey update workflow. | Periodic price updates require evidence, reviewer approval, effective date, previous/new values, and audit. |
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

## Tracker Maintenance

- Start every implementation slice by selecting explicit `QS-*` task IDs.
- Keep this tracker synchronized when code changes alter baseline coverage.
- Do not mark a task `Done` until backend, frontend, configuration, workflow, audit, tests, migrations, and smoke evidence are present.
- Prefer configuration over hard-coded policy for QS standards, rate build-up, markups, escalation formulas, retention rules, material deduction rules, approval routes, report formulas, and access controls.
- Do not remove requirements from the tracker. Record an approved scope decision with evidence if TDC defers or changes a requirement.
