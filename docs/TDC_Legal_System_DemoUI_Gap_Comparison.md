# TDC Legal Requirements vs What We Have vs Gaps

**Date:** 14 July 2026  
**Purpose:** Compare the TDC Legal Department SOP/manual requirements against the combined solution baseline: current RHEMA ERP plus DemoUI items intended to be brought into the ERP, before producing the final module-owner requirements document.

## Source Documents Reviewed

| Source document | Main process covered |
|---|---|
| `LEGAL DEPARTMENT PROCEDURE MANUAL - 1.docx` | Legal department synopsis, leases, supplementary leases, deed of variation, consent to assign, recognition of vesting, consent to mortgage, transfers, termination/recognition of tenancy, court processes, glossary. |
| `LEASES-DEED OF VARIATION-RENEWAL  SUBLEASE.docx` | Lease/deed of variation/renewal/sublease drafting, LO approval, client signature, HOL and MD signature. |
| `ASSIGNMENT-SUBLEASE-VESTING.docx` | File minuting, LAA drafting, LO approval, HOL signature, MD signature. |
| `MORTGAGES.docx` | Payment follow-up, draft mortgage consent, LO vetting, HOL and MD signature. |
| `MORTGAGE IN PRINCIPLE.docx` | Payment follow-up, draft mortgage-in-principle letter, LO vetting, HOL signature. |
| `TRANSFERS.docx` | Transfer fee payment, transfer form drafting, LO vetting, client signature, LO/LAA/HOL signatures. |
| `TERMINATION-RECOGNITION.docx` | Termination letter drafting/approval, payment approval, recognition draft vetting, LO/LAA/HOL signatures. |
| `COURT PROCESSES.docx` | Writ receipt, LAA recording, secretary routing, HOL assignment to LO, court jacket, process preparation and filing. |
| `OTHER COURT PROCESSES - Copy.docx` | Other court process receipt, LAA recording, secretary routing, LO action and LAA court filing. |

## Status Legend

| Status | Meaning |
|---|---|
| Available | The combined baseline already has a usable module, entity, API, or page for this area. |
| Partial | The system has a foundation, but it does not yet meet the TDC process end-to-end. |
| UI reference only | A page/workspace exists for guiding the process, but it is not a separate dedicated operational subsystem. |
| To add | Required for TDC, but not currently implemented as a complete usable feature. |

## Key Finding

The current RHEMA ERP has a Legal module that is already closely aligned to the SOP names shared by TDC. It provides Legal procedure cards and live Legal procedure case workspaces using the shared `ProcedureCase` engine. This means Legal has a stronger starting point than many modules: users can open legal procedure cases, capture intake fields, manage checklists, attach documents, complete stages, and record activity history.

The main gap is depth. The current system treats Legal processes mostly as generic procedure cases. TDC will still need stronger legal matter management features: court deadlines and hearing calendars, docket/court jacket tracking, legal instrument template generation, signature/sealing control, payment verification, Lands Commission registration handoff, Estate records amendment tracking, legal reporting, risk/status dashboards, and integration with Estate, Finance, Records, and Workflow.

## Baseline Position

For this comparison, current ERP and DemoUI are treated together as what we have because DemoUI items are expected to be brought into the ERP. No separate Legal DemoUI source project was found in this workspace. The available UI is the live app Legal module:

- `frontend/src/app/legal/page.tsx`
- `frontend/src/app/legal/[entityType]/page.tsx`
- `frontend/src/services/legal-procedure.service.ts`

The legacy `/workflow-demo` page redirects to Workflow Administration and is not a Legal DemoUI. Therefore, Legal comparison should treat the live Legal procedure pages as the UI reference, not as a separate DemoUI implementation.

## Baseline Components Relevant to Legal

| Component | Coverage |
|---|---|
| `LegalProcedureCatalogService` | Defines Legal procedure catalogue and workspaces for Legal Procedure Manual, Mortgages, Mortgage in Principle, Court Processes, Other Court Processes, Termination/Recognition, Assignment/Sublease/Vesting, Leases/Deed of Variation/Renewal/Sublease, and Transfers. |
| `LegalProceduresController` | Exposes Legal procedures and workspace definitions through `api/legal/procedures`. |
| Legal frontend pages | Show Legal procedure cards and open Legal procedure workspaces. |
| `ProcedureCase` engine | Persists cases, fields, checklist items, required documents, document attachments, activities, stage completion, workflow instance linkage and current assigned role. |
| Workflow administration | Can configure workflows for Legal entity types. |
| Sales Agreement module | Supports property/lease/tenancy style agreements, milestones, renewals, documents and approval fields. |
| Procurement Contract module | Supports contracts, milestones, amendments, signatures, documents and termination fields, mainly for procurement contracts. |
| Estate module | Holds Estate property/land records and related lease/title workflows that Legal depends on. |
| Finance/AR | Provides invoice/payment/customer foundations that can support legal fee/payment checks if integrated. |

## Comparison Matrix

| TDC Legal requirement area | What we have | Status | What we still need to add |
|---|---|---|---|
| Legal procedure catalogue | Legal catalogue exists for all SOP names supplied by TDC. Legal landing page lists procedure cards. Exact Legal procedure types are present. | Available | Confirm names, stage counts and role labels with TDC Legal. |
| Live legal matter/case workspace | Shared `ProcedureCase` engine supports Legal cases, intake fields, documents, checklists, activities, and stage completion. Legal workspace page embeds `ProcedureCaseWorkspace` for each Legal procedure. Users can open and manage generic Legal procedure cases. | Available / Partial | Add legal matter-specific fields, dashboards, deadline controls and legal reports. |
| General Legal Procedure Manual | Legal Procedure workspace covers receive/register, minute/assign, due diligence, draft/vet, approve/execute, dispatch/records. Live Legal Procedure workspace exists. Core manual flow is reflected in system. | Available | Add TDC-specific legal register numbering, file source rules, and completion/dispatch controls. |
| Leases / supplementary leases / deed of variation / renewal / sublease | Legal workspace exists with receive/minute, due diligence, draft, vetting, signatures, release/records. Sales Agreement and Estate modules cover related agreement/property data. Live LegalLeaseVariationRenewalSublease workspace exists. Process is modelled and can be run as a case. | Partial | Add lease template generation, sealing register, client execution tracking, Lands Commission registration handoff, Estate/Revenue records update status. |
| Consent to assign / sublease / vesting | LegalAssignmentSubleaseVesting workspace exists. Live workspace exists. Draft/vet/sign/release flow is represented. | Partial | Add specific CTA/CTS/CTV document templates, prerequisite checks, party verification, fee/payment confirmation and registration tracking. |
| Consent to mortgage | LegalMortgage workspace exists with payment follow-up, drafting, vetting, HOL/MD signature, release and return file. Live workspace exists. SOP sequence is strongly represented. | Partial | Add mortgagee/bank register, 30-day payment deadline automation, consent template generation, MD signature tracking, release/collection log. |
| Mortgage in principle | LegalMortgageInPrinciple workspace exists. Live workspace exists. Payment follow-up, drafting, LO vetting and HOL signature are represented. | Partial | Add preliminary legal recommendation status, conditions, response template, payment deadline alert and close-out report. |
| Transfers | LegalTransfer workspace exists and includes due diligence, interviews, fee calculation/approval, payment, draft transfer, client execution, legal signatures, records. Live workspace exists. Better than the short SOP because workspace already includes interviews and Estate/MD fee approval handoffs. | Partial | Add transfer declaration template, transferor/transferee party register, witness capture, fee calculation integration, Estate records amendment confirmation. |
| Termination / recognition of tenancy | LegalTerminationRecognition workspace exists with due diligence, site review, termination notice, notice posting, payment, recognition draft, execution, records. Live workspace exists. Manual's termination/recognition flow is represented and expanded. | Partial | Add 21-day notice countdown, notice-pasting evidence, recognition form template, tenancy declaration generation, legal/estate records status. |
| Writ of summons / court processes | LegalCourtProcess workspace exists for receiving, recording, opening docket, assignment, response preparation, court filing and monitoring. Live Court Process workspace exists. Court process receipt and filing steps are represented. | Partial | Add suit register, court/venue, service date, response deadline alerts, hearing calendar, court jacket/docket repository, appearance/defence/affidavit templates, judgment/outcome tracking. |
| Other court processes | LegalOtherCourtProcess workspace exists. Live workspace exists. Receipt, recording, assignment, process preparation and filing are represented. | Partial | Add process categories, endorsed service details, bailiff dispatch book reference, filing receipt, subsequent hearing/process tracking. |
| Legal file movement | ProcedureCase activities and stage ownership exist. Activity history visible in case workspace. Case movement is captured at a generic level. | Partial | Add formal Legal file register: Registry receipt, LAA receipt, Secretary routing, HOL assignment, LO ownership, court jacket movement, Estate file return. |
| Payment confirmation | Legal workspaces mention payment evidence and payment status fields. Finance has invoice/payment foundations. Payment fields shown in workspace. Fields exist; Finance module can support payment data. | Partial | Integrate with Finance/Revenue to verify payment, enforce 30-day payment windows, and prevent drafting/signature before payment where required. |
| Drafting and vetting | Workspaces include drafting/checklist and LO vetting stages. Checklist stages exist in Legal UI. Draft/vet process can be tracked manually. | Partial | Add document generation, versioning, draft review comments, approval history, redline/support for corrected drafts. |
| Signature routing | Workspaces include LO, LAA, HOL and MD handoffs depending on process. Handoff cards show signature owners. Signature routing is represented in stages. | Partial | Add signature status fields, date signed, signer, sealed date, document copy status, and escalation when signatures delay. |
| Sealing and dispatch | Workspaces include final release/records stage. Required outputs show signed/sealed copies. Final stage exists. | Partial | Add legal seal register, collection/dispatch log, recipient, collection date, registered mail/courier reference and scanned final copy. |
| Lands Commission registration handoff | Manual states lessee collects lease/consent for registration at Lands Commission. Estate/Land Acquisition has registration concepts. Legal UI does not provide full registration tracking. Registration concepts exist elsewhere. | Partial | Add Legal-to-Estate registration handoff status, Lands Commission submission/registration number/return copy tracking, and detachment/update confirmation. |
| Estate records amendment | Workspaces include return to Estate Records. Estate module has managed assets and procedure cases. Handoff cards include Estate Records. Handoff is represented. | Partial | Add formal amendment confirmation from Estate Records, record-change evidence, Revenue update status and closed-loop completion. |
| Court calendar and deadlines | Court process workspace has service date/response deadline fields. Intake fields include deadline. Basic deadline fields exist. | Partial | Add automated reminders, hearing calendar, limitation/filing deadline alerts, adjournment history and next action dashboard. |
| Litigation case status and risk | No dedicated litigation matter entity found. ProcedureCase can hold generic status. Case status exists at procedure level. Generic status can be used temporarily. | To add | Add litigation matter status, claim amount, risk rating, counsel, court, judge, parties, hearing dates, judgment, appeal, settlement, exposure and provisioning fields. |
| External counsel management | No Legal-specific external counsel module found. Business Partner/Contractor can store third parties. No Legal UI for counsel management. Business Partner foundation exists. | To add | Add external counsel/law firm register, instructions, fees, retainers, performance, matter assignment and invoices. |
| Legal opinions/advisory matters | General Legal Procedure can be used, but no specific advisory/legal opinion workflow found. No dedicated opinion workspace. Generic Legal Procedure can be a fallback. | To add | Add legal opinion request, issue summary, research notes, advice memo, approver, recipient, confidentiality and closure. |
| Legal reports | Procedure cases can be listed. No Legal report pack found. Legal UI lists cases by procedure only. Case list data exists. | Partial | Add reports for open matters, court deadlines, cases by LO, documents pending signature, payments pending, leases/consents issued, Estate records pending amendment, litigation exposure. |
| Document repository | ProcedureCase documents and file upload exist; SalesAgreement and Contract documents exist. Workspace supports document attachment and preview. Legal cases can store required documents and uploads. | Available / Partial | Add legal document taxonomy, mandatory documents by matter type, final executed document register, version control and retention/access rules. |
| Audit trail | ProcedureCase activities, workflow logs, audit fields and tenant audit foundations exist. Activity history appears in workspace. Basic action history exists. | Partial | Add field-level audit for legal matter changes, document versions, approval decisions, signature events and deadline changes. |
| Workflow approvals | Workflow admin can configure entity workflows; ProcedureCase can link workflow instances. Legal case workspace indicates configured workflow use. Workflow engine exists. | Partial | Configure Legal entity type workflows for HOL, LO, LAA, MD, Estate, Finance and Registry handoffs; add escalation and delegation rules. |
| Integration with Estate | Estate sends property files; Legal returns files for records amendment. Estate module exists. Handoffs mention Estate. Both modules exist. | Partial | Build direct links between Estate file/property records and Legal cases, including status sync and record amendment completion. |
| Integration with Sales Agreements / Contracts | SalesAgreement supports lease/tenancy agreements; Procurement Contract supports formal contracts. No direct Legal case linkage observed. Agreement/contract foundations exist. | Partial | Link legal drafting/vetting cases to SalesAgreement/Contract records where the legal instrument becomes a controlled agreement. |

## What We Should Say in the Requirements Document

### Already Available

- Legal procedure catalogue covering the exact SOP families provided by TDC.
- Legal landing page and procedure workspaces.
- Live Legal case management using the shared `ProcedureCase` engine.
- Intake fields, required documents, checklists, activity history and stage completion.
- Workflow administration foundation for configurable Legal approvals.
- Document upload/attachment support.
- Related Sales Agreement, Procurement Contract, Estate, Finance and Workflow modules that can be integrated into Legal processes.

### Partially Available

- Legal matter handling is available as generic procedure cases, but not yet a dedicated Legal Matter Management subsystem.
- SOP stages are represented, but detailed TDC controls such as payment verification, 30-day windows, 21-day notice periods, signature/sealing registers and court calendars require additional implementation.
- Documents can be uploaded, but legal template generation, versioning, execution copies and legal document registers are not complete.
- Estate and Finance dependencies are acknowledged in workflows but not fully integrated as system-enforced prerequisites and close-out confirmations.

### To Add

- Legal matter register with matter numbers, matter type, property reference, party details, responsible officer, status, priority, risk and deadlines.
- Court/litigation register with suit numbers, court, parties, service dates, response deadlines, hearings, filings, judgments, appeals and exposure.
- Court calendar and automated deadline reminders.
- Legal file movement register for Registry, LAA, Secretary, HOL, LO, court jacket and Estate file return.
- Legal document template generation for leases, deed variations, consents, transfers, termination letters, recognition forms, appearances, defences and other court filings.
- Draft version control, vetting comments and approval history.
- Signature, sealing, dispatch and collection register.
- Finance/Revenue payment verification and payment-deadline controls.
- Estate integration for property file retrieval, cadastral/schedule checks and records amendment confirmation.
- Lands Commission registration handoff and registration-status tracking.
- Legal reports and dashboards for management, HOL, officers and audit.
- External counsel and legal opinion workflows if they are in scope.

## Recommended Final Legal Requirements Document Structure

1. Current-state Legal process summary from TDC SOPs.
2. Current RHEMA ERP and UI comparison.
3. Requirements already covered by current system.
4. Requirements partially covered and requiring configuration/integration.
5. New requirements to add.
6. Legal gap register and open decisions.
7. Prioritized implementation phases.

## Open Clarifications for TDC Legal

1. Should Legal matters be managed only through generic procedure cases, or should a dedicated Legal Matter Register be created?
2. What is the official numbering format for legal files, court dockets, leases, consents, transfers, recognition forms and court matters?
3. Which Legal documents must be generated from templates inside ERP?
4. Which matters require Managing Director signature versus Head of Legal signature only?
5. Should the system enforce 30-day payment windows and 21-day termination notice windows automatically?
6. Should court hearing dates, filing deadlines and service deadlines trigger notifications?
7. Should external counsel/law firms be tracked inside the Legal module?
8. Which reports do HOL, MD, Estate, Finance and Audit need monthly or quarterly?
9. Should Lands Commission registration be tracked by Legal, Estate, or both?
10. Which historical Legal files should be migrated first: active court matters, pending leases/consents, pending transfers, recognition matters, or all matters?
