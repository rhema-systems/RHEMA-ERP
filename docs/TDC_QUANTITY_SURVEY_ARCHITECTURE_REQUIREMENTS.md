# TDC Quantity Survey Architecture Requirements and Traceability Baseline

Last reviewed: 2026-08-29

## Purpose and authority

This document is the implementation and acceptance baseline for Quantity Surveying requirements found in:

`D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\test scripts\TDC ERP Architecture and Design Document (002).docx`

The authoritative Quantity Survey requirements are primarily in:

- Section 16.4, **Detailed Quantity Surveying Design**.
- Section 16.4.1, **Quantity Surveying Integration, Controls, and Acceptance**.
- Section 18, **Unified Workflow, Approval, and Control Design**.
- Section 18.1, **Consolidated Workflow Control Model**.
- Section 18.2, **Example Workflow and Approval Processes**.
- Section 19.1, **Consolidated Responsibility Model**.
- Sections 19.2 through 19.4 covering reporting, document exchange, integration, security, and governance.
- Section 20.4, **Land, Lease, Property, Maintenance, QS, Civil Engineering, and Permit Workflow Design**.
- Section 21, **Consolidated Implementation Roadmap, Traceability, and Acceptance Model**.
- Sections 22.2 through 22.4 covering module integration, consolidated controls, data, integration, security, reporting, testing, and operational readiness.

Sections 9 through 13 provide the applicable enterprise security, integration, migration, non-functional, and acceptance requirements.

The architecture document does **not** assign `FR-QS-*` requirement IDs. The `TDC-QS-ARCH-*` keys below are local traceability keys only; they are not represented as identifiers taken from the source document.

This baseline does not replace `docs/tdc-quantity-survey-gap-implementation-tracker.md`. That tracker is based on the separate *Quantity Survey Questionnaire - Response.docx* and contains a broader set of questionnaire requirements. Where that tracker exceeds the architecture document, the additional capability remains useful but is not treated as an architecture-mandated publication or transaction blocker.

## Architecture outcome

The Quantity Surveying module shall control cost estimation, bills of quantities, valuations, contract cost monitoring, variations, interim payment certificates, retention, final accounts, and cost certification for works, maintenance, construction, and engineering activities.

The module shall use shared Procurement, Civil Engineering, Maintenance, Finance, Accounts Payable, Contracts, Inventory, Document Management, Workflow, Security, Audit, and Reporting owners. It shall not introduce parallel supplier, contract, project, budget, AP, inventory, document, workflow, authorization, or audit stores.

## Core capability requirements

| Local trace key | Architecture capability | Required design outcome | Principal current implementation evidence | Current assessment |
| --- | --- | --- | --- | --- |
| `TDC-QS-ARCH-001` | Estimates and cost plans | Prepare and approve estimates, cost plans, rate build-ups, assumptions, contingency, funding source, project reference, property reference, and approval history. | `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyEstimateEntities.cs`; `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyRateBuildUpEntities.cs`; `src/ErpSystem.Core/Services/Projects/ProjectService.QuantitySurveyEstimates.cs`; `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyEstimateSourceSnapshotBuilder.cs`; `src/ErpSystem.Data/Services/QuantitySurveyRateLibraryService.BuildUps.cs`; `frontend/src/components/quantity-survey/QuantitySurveyEstimateVersionsDialog.tsx`; `frontend/src/components/quantity-survey/QuantitySurveyRateBuildUpDialog.tsx`. | Implemented slice; workflow acceptance open. Versioned estimates, assumptions, markups, rate sources, DMS evidence, workflow status, approval history, budget/actual reconciliation, and server-derived immutable project funding and linked property snapshots exist. Distinct technical and Finance/budget review must be proven through the configured workflow. |
| `TDC-QS-ARCH-002` | Bills of quantities | Maintain BOQ headers and lines, descriptions, units, quantities, rates, amounts, revisions, approvals, attachments, and procurement or contract links. | `src/ErpSystem.Core/Entities/Projects/ProjectBoqVersionEntities.cs`; `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyTenderBoqEntities.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyTenderBoqSubmissionService.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyBoqSpreadsheetService.cs`; `frontend/src/components/quantity-survey/QuantitySurveyBoqVersionDialog.tsx`; `frontend/src/components/quantity-survey/QuantitySurveyBoqImportDialog.tsx`; `frontend/src/components/quantity-survey/QuantitySurveyTenderBoqVettingPanel.tsx`. | Substantial implementation; acceptance open. Versioning, line snapshots, classification, workflow approval, publication, comparison, controlled import, tender submission, and vetting exist. UAT must verify attachments and immutable approved procurement/contract lineage for every BOQ creation route, including non-imported versions. |
| `TDC-QS-ARCH-003` | Valuations and certificates | Support interim valuations, work-done measurements, certificate numbers, certified amount, previous payments, retention, deductions, VAT or tax treatment, approval routing, and AP linkage. | `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyMeasurementEntities.cs`; `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyValuationWorksheetEntities.cs`; `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyPaymentCertificateEntities.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyMeasurementService.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyValuationWorksheetService.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyPaymentCertificateService.cs`; `frontend/src/components/quantity-survey/QuantitySurveyMeasurementsDialog.tsx`; `frontend/src/components/quantity-survey/QuantitySurveyValuationWorksheetDialog.tsx`; `frontend/src/components/quantity-survey/QuantitySurveyPaymentCertificateDialog.tsx`. | Strong implemented slice. Controlled measurements, line valuation, previous-certificate checks, server numbering, retention, deductions, tax, workflow, DMS output, and Finance-owned AP handoff exist. A fresh full lifecycle remains to be demonstrated rather than relying on a pre-created contract, BOQ, and valuation fixture. |
| `TDC-QS-ARCH-004` | Variations and claims | Record requests, reasons, evidence, cost impact, time impact, approval status, revised contract value, and audit trail. | `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyVariationEntities.cs`; `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyContractClaimEntities.cs`; `src/ErpSystem.Core/Entities/QuantitySurvey/QuantitySurveyDayworkEntities.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyVariationService.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyContractClaimService.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyDayworkService.cs`; `frontend/src/components/quantity-survey/QuantitySurveyVariationDialog.tsx`; `frontend/src/components/quantity-survey/QuantitySurveyContractClaimsWorkspace.tsx`; `frontend/src/components/quantity-survey/QuantitySurveyDayworkWorkspace.tsx`. | Implemented slices; cross-functional acceptance open. The architecture-specific variation route from Engineer initiation through QS valuation, Procurement contract review, Finance budget validation, and independent approval must be demonstrated end to end. |
| `TDC-QS-ARCH-005` | Contract cost monitoring | Track original contract value, approved variations, revised contract sum, certified payments, retention balance, outstanding balance, budget commitment, and cost-to-complete. | `src/ErpSystem.Core/Services/Projects/ProjectService.QuantitySurveyCostReconciliation.cs`; `src/ErpSystem.Core/Services/Projects/ProjectService.QuantitySurveyCostDashboard.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyFinalAccountService.cs`; `frontend/src/components/quantity-survey/QuantitySurveyCostReconciliationDialog.tsx`; `frontend/src/components/quantity-survey/QuantitySurveyCostDashboardPage.tsx`; `frontend/src/components/quantity-survey/QuantitySurveyFinalAccountDialog.tsx`. | Substantial implementation; representative reconciliation acceptance open. Budget, commitment, certificate, actual, variation, forecast, cost-to-complete, final account, and Finance payment sources are combined without a separate QS ledger. UAT must reconcile each total to its source record and prove section, cost-code, contract, and project access. |
| `TDC-QS-ARCH-006` | Controls and reporting | Provide certificate approval history, valuation reports, BOQ reports, variation register, retention report, cost-to-complete report, contract balance report, and QS audit trail. | `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyStatutoryReportCatalogue.cs`; `src/ErpSystem.Api/Services/QuantitySurvey/QuantitySurveyStatutoryReportService.cs`; `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyAuditEventMap.cs`; `frontend/src/app/reports/quantity-survey/page.tsx`; `frontend/src/app/reports/quantity-survey/dashboard/page.tsx`; `frontend/src/app/reports/quantity-survey/[reportCode]/page.tsx`. | Implemented catalogue; report UAT open. Ten governed report families now include the architecture-required retention register, cost-to-complete, contract balance, and project-scoped QS audit trail in addition to BOQ, valuation, variation, final-account, project-cost, and certificate reporting. Acceptance must prove approval-history visibility, totals and filters, Excel/PDF export, project/contract authority, tenant isolation, and immutable audit read-back. |

## Controlled Quantity Survey lifecycle

Section 16.4 describes the standard QS lifecycle. For test traceability, its final reporting and audit-closure activities are grouped as one terminal stage. This produces 13 testable stages without removing either source requirement.

| Stage | Architecture-required activity | Required evidence and result |
| --- | --- | --- |
| 1 | Estimate or BOQ preparation | New governed record linked to an approved project, with controlled source data and supporting evidence. |
| 2 | Technical and budget validation | Technical completeness, quantities, rates, funding, budget, currency, and required documents validate before approval. |
| 3 | Estimate or BOQ approval | Configured workflow completes using an independent authorized user; maker self-approval is rejected and audited. |
| 4 | Procurement or contract linkage | Approved tender, award, supplier or contractor, purchase order, and Works contract references resolve from their authoritative owners. |
| 5 | Site measurement or valuation | Work-done quantities and site evidence link to the approved BOQ and current contract. |
| 6 | Certificate preparation | Server-controlled certificate reference, gross work, previous payments, retention, deductions, tax, and net amount reconcile. |
| 7 | QS review | Assigned Quantity Survey reviewer confirms measurement, valuation, rate, deduction, and certificate evidence. |
| 8 | Engineering or project confirmation | An authorized Engineer, Project Officer, consultant, or configured technical confirmer verifies progress and technical evidence where applicable. |
| 9 | Finance validation | Finance validates budget, commitment, contract balance, tax, account treatment, and payment readiness. |
| 10 | Independent approval | The configured approving authority decides without violating preparation, technical confirmation, Finance validation, or approval segregation. |
| 11 | Accounts Payable payment linkage | Approved certificate creates or links one Finance-owned AP obligation; invoice, posting, payment, balance, reversal, and retry remain Finance-owned and reconciled. |
| 12 | Retention tracking | Retention held, eligible release, defects or completion conditions, releases, and outstanding balance reconcile by contract and certificate. |
| 13 | Reporting and audit closure | Reports reconcile to source transactions; documents, approvals, changes, exceptions, payment lineage, and final-account closure remain retained and auditable. |

No stage may require a business user to enter an internal database identifier, workflow-definition identifier, document-record identifier, checksum, hash, or other technical lineage key. Controlled selectors and server-generated lineage shall be used.

## Integration, control, and acceptance requirements

### Governed operating data

Quantity Surveying shall use tenant-scoped project, contract, BOQ, valuation, variation, certificate, retention, supplier, contractor, procurement, civil engineering, maintenance, inventory, finance, and document records.

| Integration owner | Required Quantity Survey use | Acceptance boundary |
| --- | --- | --- |
| Procurement | Approved contracts, purchase orders, supplier or contractor records, tender documents, and award information feed QS cost control. | QS reads approved and current procurement sources; it does not recreate or directly mutate Procurement-owned records except through approved owner services. |
| Civil Engineering and Projects | Progress, site measurements, defects, completion status, drawings, instructions, and technical evidence support valuation and certification. | Certificate approval is blocked when a configured technical confirmation is required and absent. |
| Maintenance | Estimates, BOQs, valuations, and certificates support maintenance work requiring technical cost certification. | Maintenance remains the work-order owner; QS owns cost certification records. |
| Finance and Accounts Payable | Budget checks, commitments, contract balances, tax, payment vouchers, AP invoices, ledger postings, payments, and reversals support certification and settlement. | QS does not post an independent ledger. Amounts, currency, posting, payment, and reversal must reconcile to Finance-owned records. |
| Contracts | Original terms, amendments, revised contract sum, retention, claims, completion, and final-account sources constrain QS actions. | Only active, approved, tenant-safe Works contracts are selectable. |
| Inventory and Stores | TDC-issued and governed on-site or off-site material values support reconciliation and certificate deductions. | Inventory owns receipts, issues, returns, valuation, and stock postings; QS retains certified deduction lineage. |
| Document Management | Measurements, drawings, BOQs, estimates, variations, certificates, approvals, photographs, and audit packs retain versioned evidence. | The central DMS owns physical files, access, malware status, versions, retention, and download authorization. |

### Mandatory transaction controls

- Use approved rate build-ups for governed estimates and valuations where the configured QS policy requires them.
- Preserve immutable BOQ versions and require a revision workflow for changes to an approved publication.
- Require approval for variations and apply approved cost or time impact through controlled downstream owner services.
- Generate certificate numbers on the server and prevent duplicate certificates or duplicate AP obligations.
- Validate previous approved certificates and payments before calculating the current certificate.
- Calculate and reconcile retention using controlled contract and certificate rules.
- Validate budget availability, active commitments, revised contract balance, and currency before approval or payment linkage.
- Separate preparation, technical or project confirmation, Finance validation, and final approval according to the configured workflow.
- Retain immutable audit history for estimates, BOQs, valuations, variations, certificates, retention, final accounts, integration retries, and rejected actions.
- Roll back or flag partial failures; retries shall not duplicate workflows, certificates, invoices, payments, journals, or audit mutations.
- Reject unauthorized and cross-tenant direct API attempts without leaking the existence or content of protected records.

### Architecture acceptance outcome

Architecture acceptance requires a representative QS case to be prepared, validated, approved, linked to Procurement and engineering evidence, certified, routed to Finance/AP, paid or otherwise reconciled, reported, and audited with complete documents and approval history.

Source-level models, pages, and isolated automated tests are implementation evidence, not business acceptance on their own.

## Cross-cutting architecture requirements

### Shared workflow and configurable approval route

Sections 18 and 18.1 require the central Workflow owner to define, for each governed record family:

- Initiating role.
- Validation rules.
- Review role.
- Approval role.
- Exception and rework path.
- Posting or downstream-update point.
- Required document evidence.
- Notifications and reporting output.
- Audit events for submission, review, approval, rejection, amendment, posting, reversal, and escalation.

Section 18.2 states that exact approval thresholds, roles, and routing rules shall be confirmed during detailed configuration. Therefore, missing business configuration shall produce clear administrative readiness guidance, but architecture acceptance shall not depend on hard-coded people, thresholds, or one test-only role name.

Current shared workflow evidence includes:

- `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyWorkflowBindingRegistry.cs`.
- `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyConfigurationDecisionRegistry.cs`.
- `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyAccessControlRegistry.cs`.
- `src/ErpSystem.Core/Services/QuantitySurvey/QuantitySurveyAuditEventMap.cs`.

### Responsibility and segregation of duties

The architecture assigns these primary responsibilities:

| Function | Architecture responsibility |
| --- | --- |
| Quantity Surveying | Estimates, BOQs, interim payment certificates, valuation support, variations, and cost certification. |
| Civil Engineering or Project function | Technical inspections, site instructions, progress, defects, completion, and technical confirmation. |
| Procurement and supplier or contractor functions | Contract, tender, award, purchase-order, delivery, and supplier-side fulfilment records. |
| Finance and General Ledger | Budget and financial validation, posting, reconciliation, ledger reporting, period control, and financial oversight. |
| Accounts Payable and Expenditure | Invoice or certificate payment processing, voucher preparation, coding checks, approval routing, and payment release. |
| Internal Audit | Vouching, compliance checks, exception review, and control assurance. |
| System Administration | Users, roles, workflow configuration, access restrictions, integration settings, and master-data control; not business approval. |

The same actor shall not perform incompatible preparation, technical confirmation, Finance validation, final approval, payment, or audit-review stages unless TDC has approved a documented exception and compensating control.

### Reporting and document governance

Reports shall:

- Reconcile to governed BOQ, valuation, variation, certificate, contract, budget, commitment, actual-cost, retention, payment, and final-account sources.
- Support controlled filters such as period, department, project, contract, supplier or contractor, cost centre, approval status, and authorized role.
- Support search, sort, drill-down, and Excel/PDF export where authorized.
- Preserve report parameters, execution identity, time, source version, and export audit where required.

Documents shall:

- Be attached to the relevant ERP transaction rather than stored as unexplained external paths or user-entered IDs.
- Preserve metadata, version, access, malware status, retention, approval, and audit history through the central DMS.
- Remain visible only to authorized project, contract, partner, department, and tenant users.
- Use controlled correction or supersession rather than deletion of approved evidence.

### Security, audit, reliability, data, and usability NFRs

The following Section 12 requirement families apply directly to Quantity Surveying:

| Requirement family | Applicable architecture expectations |
| --- | --- |
| `NFR-SEC-001` through `NFR-SEC-005` | RBAC, least privilege, assigned modules and records, segregation of duties, secure authentication, sensitive-data restriction, and encryption. |
| `NFR-AUD-001` through `NFR-AUD-003` | Tamper-evident audit for creates, changes, approvals, rejections, posting, reversal, cancellation, access and configuration changes; actor/time/before/after/reference/reason; no ordinary-user mutation. |
| `NFR-PER-001` through `NFR-PER-004` | Responsive transaction, search, approval, and report behavior under agreed operating and period-end load. |
| `NFR-AVL-001` through `NFR-AVL-003` | Agreed availability, controlled maintenance, restart verification, and recovery of critical approval, reporting, and reconciliation functions. |
| `NFR-REL-001` through `NFR-REL-003` | No duplicate or missing postings; transaction integrity on failure; clear validation and corrective messages. |
| `NFR-DAT-001` through `NFR-DAT-004` | Mandatory fields, validation, duplicate prevention, approval rules, reconciliation, referential integrity, and exception reporting. |
| `NFR-US-001` through `NFR-US-003` | Clear menus, guided workflows, meaningful errors, work queues, search, filters, sort, drill-down, and export. |
| `NFR-INT-001` through `NFR-INT-003` | Secure and traceable integration, batch or source audit, accepted/rejected/reprocessed history, duplicate prevention, and source-to-ledger reconciliation. |
| `NFR-BCK-001` through `NFR-BCK-002` | Back up and recover data, configuration, attachments, workflows, reports, and audit logs. |
| `NFR-MNT-001` through `NFR-MNT-002` | Authorized configuration without direct database changes; configuration documentation, release notes, and controlled changes. |
| `NFR-CMP-001` through `NFR-CMP-002` | Internal policy, procurement, statutory, tax, audit, and retention compliance. |
| `NFR-RPT-001` through `NFR-RPT-002` | Standard, operational, audit, reconciliation, management, and ad hoc reporting with controlled parameters and Excel/PDF export. |
| `NFR-SUP-001` through `NFR-SUP-002` | Role-based training, documentation, support, issue logging, prioritization, escalation, ownership, and post-resolution verification. |

## Current automated and database evidence

The repository contains material QS test evidence, including:

- Rules and lifecycle tests under `tests/ErpSystem.Core.Tests/Services/QuantitySurvey/`.
- Controller authorization tests under `tests/ErpSystem.Api.Tests/Controllers/QuantitySurvey/`.
- Tender BOQ and rate-library service tests under `tests/ErpSystem.Api.Tests/Services/QuantitySurvey/`.
- Browser and API journeys under `e2e-tests/tests/qs*.spec.ts`.
- SQL readiness and reconciliation checks in:
  - `tests/sql/qs-phases-0-6-acceptance-readiness.sql`.
  - `tests/sql/QS0501_ValuationWorksheetReleaseGate.sql`.
  - `tests/sql/QS0502_InterimValuationWorkflowReleaseGate.sql`.
  - `tests/sql/qs-final-finance-security-acceptance.sql`.
- The controlled non-production fixture under `scripts/quantity-survey/seed-quantity-survey-e2e.sql` and `scripts/quantity-survey/Invoke-QuantitySurveyE2ESeed.ps1`.

This evidence proves many focused boundaries. It does not yet prove one clean architecture lifecycle because the broad browser journey starts from a pre-created project, contract, approved BOQ, and draft valuation, and the SQL scripts use known acceptance identifiers.

For release acceptance, the tests shall run against a disposable or explicitly isolated SQL Server database created from current migrations. The acceptance data shall be generated or uniquely scoped, all foreign keys and lifecycle triggers shall remain enabled and trusted, and the database shall be discarded or retained as named test evidence without modifying production business records.

## Honest open acceptance gates

| Gate | Required closure evidence |
| --- | --- |
| Controlled estimate references | Implemented server-side derivation and immutable snapshots from the tenant project and linked Estate property records; UAT must prove visibility, history, and tenant isolation without any free-text or raw-ID workaround. |
| Full workflow route | The configured route demonstrates technical review, engineering/project confirmation where applicable, Finance validation, independent approval, exception/rework, and maker-checker denial. |
| BOQ documents and lineage | Every tested BOQ route retains current DMS evidence plus immutable approved Procurement/contract lineage. |
| Fresh end-to-end QS case | One new case completes all 13 traceability stages without depending on a pre-completed BOQ, valuation, certificate, or Finance transaction. |
| Variation cross-functional route | Engineer request, QS valuation, Procurement contract review, Finance budget validation, approval, revised contract and budget impact, and audit history reconcile. |
| Cross-module positive integrations | Procurement, Civil Engineering, Maintenance, Inventory, Contracts, Finance/AP, DMS, Workflow, Reports, and Audit each demonstrate their source-owner boundary and recovery behavior. |
| Retention and final account | Retention held and release conditions, certificate history, revised contract sum, variations, payments, and final account close without unexplained differences. |
| Reports and history | Required reports, approval history, audit history, filters, drill-down, Excel/PDF export, totals, project/contract authority, and tenant isolation pass with representative classified data. |
| Idempotency and failure recovery | Duplicate requests, retries, stale row versions, concurrent approvals, downstream owner failure, reversals, and reprocessing leave no duplicate or partial records. |
| Security matrix | Anonymous, missing-permission, wrong-project, wrong-contract, wrong-partner, maker-as-approver, and cross-tenant attempts are rejected and centrally audited. |
| Migration and reconciliation | TDC-approved QS source data is cleansed, staged, validated, reconciled, signed off, and retained with exception and correction history. |
| Operational readiness | Roles, workflows, parameters, report packs, training, support ownership, backups, restore evidence, deployment, and UAT sign-off are approved. |

## Scope classification

| Classification | Content | Treatment |
| --- | --- | --- |
| **TDC architecture required** | The six core capability groups; the complete controlled lifecycle; project/contract/procurement/engineering/maintenance/inventory/Finance/AP/DMS integration; approved rates and BOQ versions; variation approval; certificate numbering and duplicate prevention; previous-payment, retention, budget, commitment, tax and contract-balance checks; segregation of duties; audit history; reports; documents; security; migration; reliability; support and acceptance. | Must be implemented and proven. A missing requirement remains an open architecture gate. |
| **TDC configuration required** | Exact approval thresholds, named roles, workflow step count, routing order, authority limits, rate formulas, retention percentages, tax rules, report formats, evidence templates, migration history window, response-time targets, recovery targets, and support dates. | Configure through controlled administration after TDC owner approval. Do not hard-code or require business users to enter internal IDs. Section 18.2 explicitly leaves exact roles, thresholds, and routing for detailed configuration. |
| **Questionnaire-only or implementation extension** | SMM7/CESMM catalogues, locked Excel tender BOQs, Candy/PlanSwift/Bani adapters, PBCI/GSS formula sources, detailed joint-measurement signatures, subcontract back/contra-charge workflow, exact `QS-DEC-*` decision count, exact workflow-family count, hashes, fingerprints, and specific test metadata templates. | Preserve useful implemented controls and validate them when TDC confirms the broader questionnaire scope. They shall not block an architecture-required transaction merely because an optional extension is unconfigured, unless a separately approved TDC policy makes that extension mandatory. |

## Release decision rule

Quantity Surveying is architecture-ready only when:

1. The six core capability groups are available through controlled, tenant-safe business screens and APIs.
2. A fresh representative case completes the 13-stage lifecycle using separate authorized users.
3. The current SQL Server schema, triggers, constraints, migrations, and cross-module financial results pass on an isolated real SQL Server database.
4. Required browser, API, document, report, audit, security, retry, concurrency, and reversal tests pass.
5. TDC approves the configured roles, workflow routes, thresholds, formulas, retention rules, report formats, migration reconciliation, training, and operational support plan.

Until those gates are evidenced, source-level implementation may be described as implemented or partially accepted, but not as complete business acceptance.
