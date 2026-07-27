# TDC Civil Engineering Gap Implementation Tracker

Last updated: 2026-07-17

## Purpose

This tracker is the delivery ledger for closing the gaps between the current ERP implementation and the requirements in `CIVIL ENGINEERING SECTION ERP RESPONSE -1_.docx`, completed for TDC's Development / Civil Engineering Section.

The source document defines five Civil Engineering operating workflows: engineering designs for new projects, project supervision, maintenance works, development approval/permitting review, and complaint resolution for company building assets. It also states two cross-cutting expectations: upload/attachment support for third-party engineering design outputs and direct task assignment/feedback between Supervising Civil Engineer, Civil Engineers, Technicians, Draftsmen, and Artisans.

## Source Of Truth And Boundaries

- Business source: `C:\Users\micha\Downloads\Questionnairs and SOPS\Questionnairs and SOPS\Development\Civil Engineering\CIVIL ENGINEERING SECTION ERP RESPONSE -1_.docx`
- Authoritative source render evidence: Microsoft Word rendered 3 pages to `artifacts\tdc-civil-engineering-srs-revalidation\run-20260717-1\source-render` during revalidation. The earlier 5-page artifact-tool render is retained only as superseded analysis evidence.
- Structured extraction evidence: 73 paragraphs and 0 tables were extracted from the source DOCX.
- Current implementation evidence: Development/Projects workspace, operation-level project authorization, external business-partner access policies, design controls, drawings, submittals, RFIs, site instructions, approval register, stage gates, deliverables and external reviews, quality checkpoints, non-conformance, risks/issues, project documents/comments, handover, defect-liability cases, material reconciliation, construction/site/design reports, mobile assigned-task updates/time/expense/evidence upload, Maintenance job card/work-order follow-through, selected workflow adapters, and project administration settings.
- This tracker covers Civil Engineering as a Development/Projects operating model. It references Maintenance/Assets, Workflow, Document Management, Procurement/Contracts, Finance/QS, Building Inspectorate/Permitting, HR/resource assignment, and Reporting only where Civil Engineering needs those modules to complete an end-to-end workflow.
- This tracker does not certify engineering sign-off, statutory permitting, professional liability, or regulatory compliance policy. TDC Development, Civil Engineering, Architecture, Geodetic Engineering, Town Planning, Building Inspectorate, Maintenance, Finance/QS, Legal, Internal Audit, ICT, contractors, and consultants must approve final configuration values and acceptance scenarios.

## Project Module Fit Conclusion

Civil Engineering should fit primarily inside the existing Development/Projects module, not as a separate standalone ERP module.

The current Projects module already contains the correct backbone for Civil Engineering: project phases and stage gates, drawings, submittals, RFIs, site instructions, approvals, deliverables, quality checkpoints, non-conformances, risks/issues, documents, comments, handover items, defect-liability cases, material reconciliation, design/site control reports, and project mobile assignment concepts.

The gap is not module placement. The gap is that Civil Engineering's section-specific workflows are not yet configured and enforced end to end. The implementation should extend Projects with Civil Engineering workflow templates, role/task routing, design-file metadata and versioning, review/sign-off controls, maintenance/complaint intake links, permitting review states, supervision reports, test-report endorsement, direct assignment to technicians/draftsmen/artisans, and acceptance reports.

Maintenance works and complaints on company building assets should be a bridge between Projects and Maintenance/Assets. Project-level capital or contract works should stay in Projects. Routine or asset-based corrective maintenance should create or link Maintenance job cards/work orders while preserving Civil Engineering assessment, scope, costing, supervision, completion, inspection, payment, and closure evidence.

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

No requirement is complete merely because a generic project field, page, document upload, or workflow node exists. TDC requires enforced Civil Engineering behavior from assignment through review, supervision, payment recommendation, inspection, and closure.

## Status Legend

| Status | Meaning |
| --- | --- |
| Verified baseline | Current implementation has material capability confirmed in source; final TDC acceptance is still required. |
| Partial | Useful capability exists, but required Civil Engineering controls, fields, workflow steps, reports, integrations, or hard stops are incomplete or bypassable. |
| Gap | Required capability or enforcement is absent. |
| Configuration required | TDC must approve configurable values, roles, templates, workflows, access rules, evidence rules, report packs, or migration ownership before acceptance. |
| Not started | Approved implementation task has not started. |
| In progress | Implementation is actively being changed and is not yet acceptance-ready. |
| Done | Full delivery rule above has passed and evidence is recorded. |
| Blocked | Work cannot safely continue until a named dependency is resolved. |

## Honest Readiness Statement

The ERP already contains a strong Projects foundation for Civil Engineering. Drawings, submittals, RFIs, site instructions, deliverables, external deliverable reviews, quality checkpoints, non-conformances, project documents, stage gates, approval registers, operation-level project permissions, business-partner access policies, mobile assigned-task updates, handover, defect-liability, material reporting, and design/site watch reports are present.

The main gap is not the absence of a project module. The main gap is that TDC's Civil Engineering Section workflows are not yet modelled as named, role-driven processes with hard stops, evidence, review outcomes, weekly reports, test-report endorsement, maintenance/complaint closure, permitting comments, and direct task assignment to technical staff.

Most "decision" items below should become configuration rather than hard-coded code blockers. They are listed because TDC must approve the values before the system can be acceptance-tested.

## Source Requirement Baseline

The source document is a workflow response rather than a scored questionnaire. It names five key workflows and describes the actor handoff steps inside each.

| Area | Stated TDC requirement baseline |
| --- | --- |
| Engineering designs for new projects | HOD receives directive, directs SCE to gather information/site reconnaissance, SCE receives information from HOD and other section heads, SCE assigns design task to CE, CE submits design to SCE, SCE assigns drafting task to Draftsman, Draftsman submits drawings to SCE, SCE submits designs/drawings/specification to HOD. |
| Project supervision | HOD and/or Projects Coordinator assigns supervision role to SCE or CE as Project Engineer, PE reports to Project Manager, site instructions are issued through PM, test reports are vetted, RFIs are responded to through PM, IPCs are reviewed and endorsed back to Projects Coordinator. |
| Maintenance works | HOD receives scheduled or breakdown maintenance request, SCE/CE assesses scope and remedy, scope/remediation report goes to HOD, HOD requests costing/approval, team is constituted after award, CE supervises works, weekly reports go CE -> SCE -> HOD, completion report goes CE -> SCE -> HOD, HOD directs inspection, payment, and closure. |
| Development approval/permitting | Building Inspectorate receives development approval applications and opens a file, file passes through sections after site inspection, file is forwarded from Architecture to SCE, SCE reviews engineering drawings, comments and recommends approval or otherwise, SCE forwards to HOD for final review/approval or otherwise. |
| Complaint resolution on company building assets | HOD receives complaint, SCE/CE assesses and recommends remedy, HOD requests costing/approval, team is constituted after award, CE supervises works, weekly and completion reports flow CE -> SCE -> HOD, HOD directs inspection, payment, and closure. |
| Third-party engineering tools | Civil Engineering relies on AutoCAD, Protastructure, Revit, PDF, StaadPro, Microsoft Office, and related tools; ERP must allow upload/attachment of outputs from these tools. |
| Direct task assignment | Workflow should allow direct assignment of jobs and feedback between SCE, Civil Engineers, Technicians, Draftsmen, and Artisans for urgent tasks/jobs. |

## Source Paragraph Traceability

The source contains 42 substantive workflow steps and cross-cutting expectations in addition to headings and explanatory text. The paragraph references below correspond to the authoritative Microsoft Word paragraph sequence and prevent any individual handoff from disappearing inside a broad workflow summary.

| Source ref | Source workflow step or expectation | Current coverage IDs | Implementation and acceptance coverage |
| --- | --- | --- | --- |
| P013 | HOD receives a directive to begin design or pre-contract activity for a new project. | `DES-002` | `CIV-0101`, `CIV-E2E-001` |
| P014 | HOD directs SCE to gather information and undertake site reconnaissance. | `DES-002` | `CIV-0101`, `CIV-0102`, `CIV-E2E-001` |
| P015 | SCE receives information from HOD, Architecture, Geodetic Engineering, Town Planning, and related sections. | `DES-005` | `CIV-0102`, `CIV-0104`, `CIV-E2E-001` |
| P016 | SCE assigns the engineering design task to a Civil Engineer. | `DES-002`, `ASN-001` | `CIV-0101`, `CIV-0501`, `CIV-E2E-001` |
| P017 | Civil Engineer submits the completed design to SCE. | `DES-002`, `DES-004` | `CIV-0101`, `CIV-0103`, `CIV-E2E-001` |
| P018 | SCE assigns the approved design for drafting by a Draftsman. | `DES-002`, `ASN-001` | `CIV-0101`, `CIV-0501`, `CIV-E2E-001` |
| P019 | Draftsman submits drawings to SCE for review. | `DES-002`, `ASN-002` | `CIV-0101`, `CIV-0502`, `CIV-E2E-001` |
| P020 | SCE submits the design, drawings, and specification package to HOD. | `DES-003`, `DES-004` | `CIV-0103`, `CIV-0106`, `CIV-E2E-001` |
| P023 | HOD and/or Projects Coordinator assigns SCE or CE as Project Engineer. | `SUP-002` | `CIV-0201`, `CIV-E2E-002` |
| P024 | Project Engineer reports through the Project Manager, routes site instructions and RFIs, and vets test reports. | `SUP-003`, `SUP-004` | `CIV-0202`, `CIV-0203`, `CIV-0204`, `CIV-E2E-002` |
| P025 | Project Engineer reviews and endorses IPCs and returns them to the Projects Coordinator. | `SUP-005` | `CIV-0206`, `CIV-E2E-002` |
| P029 | HOD receives a scheduled or breakdown maintenance request. | `MNT-001` | `CIV-0301`, `CIV-E2E-003` |
| P030 | HOD directs SCE to assess the maintenance requirement. | `MNT-002` | `CIV-0302`, `CIV-E2E-003` |
| P031 | SCE assigns a Civil Engineer to inspect and assess the work. | `MNT-002` | `CIV-0302`, `CIV-E2E-003` |
| P032 | Civil Engineer prepares the scope and remediation recommendation. | `MNT-002` | `CIV-0302`, `CIV-E2E-003` |
| P033 | Scope/remediation report is submitted to HOD for costing and approval action. | `MNT-002`, `MNT-005` | `CIV-0303`, `CIV-E2E-003` |
| P036 | Following award, the responsible work team is constituted. | `MNT-003`, `MNT-005` | `CIV-0303`, `CIV-0305`, `CIV-E2E-003` |
| P037 | Civil Engineer supervises execution of the maintenance work. | `MNT-003`, `ASN-001` | `CIV-0305`, `CIV-0501`, `CIV-E2E-003` |
| P038 | Civil Engineer submits weekly contractor/labour-gang reports to SCE. | `SUP-006`, `MNT-003` | `CIV-0205`, `CIV-0305`, `CIV-E2E-003` |
| P039 | SCE reviews and submits the weekly report to HOD. | `SUP-006`, `MNT-003` | `CIV-0205`, `CIV-0305`, `CIV-E2E-003` |
| P040 | Civil Engineer submits a completion report to SCE. | `MNT-003` | `CIV-0305`, `CIV-E2E-003` |
| P041 | SCE reviews and submits the completion report to HOD. | `MNT-003` | `CIV-0305`, `CIV-E2E-003` |
| P042 | HOD directs inspection, payment processing, and closure. | `MNT-003`, `MNT-005` | `CIV-0305`, `CIV-E2E-003` |
| P045 | Building Inspectorate receives a development approval application and opens a file. | `PER-001` | `CIV-0401`, `CIV-E2E-004` |
| P046 | The application file moves through relevant sections after site inspection. | `PER-002` | `CIV-0402`, `CIV-E2E-004` |
| P047 | Architecture forwards the file and engineering drawings to SCE. | `PER-002` | `CIV-0402`, `CIV-E2E-004` |
| P048 | SCE reviews, comments on, and recommends approval or otherwise. | `PER-003` | `CIV-0403`, `CIV-E2E-004` |
| P049 | SCE forwards the recommendation to HOD for final decision. | `PER-004` | `CIV-0404`, `CIV-E2E-004` |
| P053 | HOD receives a complaint concerning a company building asset. | `MNT-004` | `CIV-0301`, `CIV-0306`, `CIV-E2E-005` |
| P054 | HOD directs SCE to assess the complaint. | `MNT-004` | `CIV-0302`, `CIV-0306`, `CIV-E2E-005` |
| P055 | SCE assigns a Civil Engineer to inspect and assess the complaint. | `MNT-004` | `CIV-0302`, `CIV-0306`, `CIV-E2E-005` |
| P056 | Civil Engineer prepares the remedy and scope recommendation. | `MNT-004` | `CIV-0302`, `CIV-0306`, `CIV-E2E-005` |
| P057 | Remedy/scope report is submitted to HOD for costing and approval action. | `MNT-004`, `MNT-005` | `CIV-0303`, `CIV-0306`, `CIV-E2E-005` |
| P060 | Following award, the complaint remediation team is constituted. | `MNT-003`, `MNT-004` | `CIV-0303`, `CIV-0306`, `CIV-E2E-005` |
| P061 | Civil Engineer supervises complaint remediation work. | `MNT-003`, `ASN-001` | `CIV-0305`, `CIV-0501`, `CIV-E2E-005` |
| P062 | Civil Engineer submits weekly complaint-work reports to SCE. | `SUP-006`, `MNT-004` | `CIV-0205`, `CIV-0306`, `CIV-E2E-005` |
| P063 | SCE reviews and submits the weekly report to HOD. | `SUP-006`, `MNT-004` | `CIV-0205`, `CIV-0306`, `CIV-E2E-005` |
| P064 | Civil Engineer submits the complaint-work completion report to SCE. | `MNT-004` | `CIV-0306`, `CIV-E2E-005` |
| P065 | SCE reviews and submits the completion report to HOD. | `MNT-004` | `CIV-0306`, `CIV-E2E-005` |
| P066 | HOD directs inspection, payment processing, and closure. | `MNT-004`, `MNT-005` | `CIV-0306`, `CIV-E2E-005` |
| P070 | ERP supports upload/attachment of AutoCAD, Protastructure, Revit, PDF, StaadPro, Office, and related engineering outputs. | `DOC-001`, `DOC-002`, `DOC-003` | `CIV-0105`, `CIV-0601`, `CIV-E2E-006` |
| P072 | SCE/CE can assign urgent work directly to Technicians, Draftsmen, and Artisans and receive feedback. | `ASN-001`, `ASN-002`, `ASN-003` | `CIV-0501` through `CIV-0504`, `CIV-E2E-007` |

## Business Configuration Inputs

| ID | Priority | Status | Configuration or decision required | Why it matters | Owner |
| --- | --- | --- | --- | --- | --- |
| CIV-CFG-001 | P0 | Configuration required | Confirm Civil Engineering roles and authority levels: HOD, SCE, Civil Engineer, Project Engineer, Project Manager, Projects Coordinator, Draftsman, Technician, Artisan, Building Inspectorate, Architecture, Geodetic Engineering, Town Planning, QS/Finance, contractors, and consultants. | Workflow routing, assignment, document visibility, endorsements, and dashboards depend on approved role mapping. | Development + Civil Engineering + ICT |
| CIV-CFG-002 | P0 | Configuration required | Confirm project type and work classification for new project design, supervision, scheduled maintenance, breakdown maintenance, permitting review, and company-asset complaint resolution. | Each workflow needs different forms, evidence, approvals, reports, and integration points. | Civil Engineering + Development |
| CIV-CFG-003 | P0 | Configuration required | Confirm design review template, required site reconnaissance fields, cross-section information inputs, design disciplines, drafting review checklist, and HOD submission package. | The design workflow cannot be accepted if it only stores loose drawings without controlled review/sign-off. | Civil Engineering + Architecture + Geodetic + Town Planning |
| CIV-CFG-004 | P0 | Configuration required | Confirm allowed engineering file types, maximum sizes, metadata, version naming, file owner, reviewer, approval status, retention, and preview/download permissions for AutoCAD, Protastructure, Revit, PDF, StaadPro, Office, and related outputs. | Third-party design deliverables must be traceable and secure. | Civil Engineering + ICT |
| CIV-CFG-005 | P0 | Configuration required | Confirm supervision workflow for PE reporting to PM, site instructions through PM, RFI response path, test-report vetting, IPC review, endorsement, and submission back to Projects Coordinator. | Supervision must be enforceable instead of being comments spread across project records. | Civil Engineering + Project Management + QS/Finance |
| CIV-CFG-006 | P0 | Configuration required | Confirm weekly report template, contractor/labour-gang activity categories, progress fields, site issues, photos, test reports, materials, safety notes, and escalation rules. | The source explicitly requires weekly reporting up CE -> SCE -> HOD. | Civil Engineering + Development |
| CIV-CFG-007 | P0 | Configuration required | Confirm maintenance and complaint assessment template: request source, asset/building, defect category, site assessment, scope, remedy, urgency, estimate/costing, approval route, inspection, payment, and closure evidence. | Maintenance and complaint workflows need a shared intake but may post to Maintenance job cards/work orders. | Civil Engineering + Maintenance + Finance |
| CIV-CFG-008 | P0 | Configuration required | Confirm costing/approval route for maintenance and complaints, including when QS, Procurement, Finance, HOD, Management, or contractor workflow is required. | HOD requests costing/approval in the source process, but authority thresholds are not stated. | Civil Engineering + QS + Finance + Procurement |
| CIV-CFG-009 | P1 | Configuration required | Confirm permitting/development approval review states, inter-section handoff sequence, engineering comment categories, recommendation outcomes, rejection reasons, and HOD decision rules. | Building Inspectorate file review cannot be a free-form attachment if TDC expects traceable approvals. | Building Inspectorate + Development + Civil Engineering |
| CIV-CFG-010 | P1 | Configuration required | Confirm direct task assignment rules for technicians, draftsmen, artisans, urgent work, reassignment, feedback, attachments, due dates, and closure acceptance. | The source asks for direct assignment and feedback outside only SCE/CE links. | Civil Engineering + HR + ICT |
| CIV-CFG-011 | P1 | Configuration required | Confirm quality/test-report catalogue: concrete, soil, materials, structural, laboratory, field tests, reviewer, pass/fail thresholds, and endorsement evidence. | PE must submit vetted test reports and project quality controls must be auditable. | Civil Engineering + QA/QC + Consultants |
| CIV-CFG-012 | P1 | Configuration required | Confirm dashboard/report pack: design task status, drafts pending, RFIs, site instructions, weekly supervision reports, test reports, maintenance works, complaints, permitting files, overdue assignments, and closure/payment status. | Management reporting must reflect the actual Civil Engineering workload. | Civil Engineering + Management |
| CIV-CFG-013 | P2 | Configuration required | Confirm migration owners and source columns/files for historical designs, drawings, specifications, site reports, maintenance scopes, complaints, permits, test reports, and physical file references. | Historical work cannot be accepted without controlled migration and reconciliation. | Civil Engineering + Records + ICT |

## Current Coverage And Gap Matrix

### Governance, Roles, Workflow, And Audit

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| GOV-001 | Partial | The ERP has RBAC, operation-level project authorization, project administration, approval registers, and external business-partner policies for read/comment/upload/approve access. TDC-specific Civil Engineering roles, authority levels, inter-section access, technician/draftsman/artisan assignment rights, record-level restrictions, and contractor/consultant visibility are not acceptance-tested as one Civil role matrix. |
| GOV-002 | Partial | Shared workflow exists, but the current project status adapters cover only Project, ProjectDeliverable, ProjectClosure, and ProjectBudgetRevision. Drawings, submittals, RFIs, site instructions, test reports, weekly reports, IPC endorsements, maintenance scopes, complaints, permitting recommendations, and direct technical assignments are not yet shared-workflow entities with direct-API enforcement. |
| GOV-003 | Gap | No dedicated Civil Engineering policy/config register exists for workflow templates, evidence rules, engineering file types, report formats, assessment templates, permitting comment categories, urgent task rules, and authority levels. |
| GOV-004 | Partial | Project history, workflow history, and audit fields exist, but Civil-specific immutable audit events are not guaranteed across every assignment, review, drawing/spec revision, site instruction, RFI response, test report, weekly report, recommendation, inspection, payment direction, and closure. |

### Engineering Designs For New Projects

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| DES-001 | Verified baseline | Project design surfaces exist through Project Design tab, drawings, submittals, RFIs, approval register, deliverables, project documents, comments, and phase/stage-gate structures. |
| DES-002 | Partial | Project drawings exist with drawing number/title/revision/status and APIs/UI. Civil Engineering-specific design task assignment from HOD -> SCE -> CE, site reconnaissance, inter-section inputs, CE review submission, drafting assignment, SCE review, and HOD package submission are not enforced as a named workflow. |
| DES-003 | Partial | Project documents and deliverables can hold files and a version label, but immutable version lineage is absent. Document artifact relationships are limited to Project, Phase, Package, and WorkItem rather than first-class drawing, RFI, site-instruction, test-report, permit, calculation, and specification records; Civil metadata, review status, and reviewer sign-off remain incomplete. |
| DES-004 | Gap | No Civil design review checklist ensures designs/drawings/specifications are complete before SCE submission to HOD. |
| DES-005 | Gap | No structured cross-section input request/response path exists for Supervising Architect, Geodetic Engineer, Town Planner, and Civil Engineering design dependencies. |

### Project Supervision

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| SUP-001 | Verified baseline | Projects supports site instructions, RFIs, quality checkpoints, non-conformances, risks/issues, deliverables, material reporting, commercial administration, phase gates, and project reports. |
| SUP-002 | Partial | Project Engineer assignment can be approximated through project members/resources, but HOD/Projects Coordinator assignment of SCE or CE as Project Engineer is not a controlled role event with acceptance evidence. |
| SUP-003 | Partial | Site instructions and RFIs exist, but the required routing through Project Manager and feedback back to the Project Engineer is not hard-enforced. |
| SUP-004 | Partial | Quality checkpoints and non-conformances exist, but vetted engineering test reports with pass/fail result, lab/source, reviewer endorsement, attachment, and IPC/certificate linkage are not first-class Civil records. |
| SUP-005 | Partial | Interim payment certificates exist in QS/project commercial foundations, but PE review/endorsement of completed IPCs from Projects Coordinator and return for processing is not enforced. |
| SUP-006 | Gap | Weekly contractor/labour-gang activity reports from CE -> SCE -> HOD are not implemented as a controlled recurring supervision report with photos/evidence, review comments, and overdue escalation. |

### Maintenance Works And Company Asset Complaints

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| MNT-001 | Partial | Projects has defect-liability follow-through to create job cards/work orders, and Maintenance has assets/job card/work-order concepts. Civil Engineering scheduled/breakdown maintenance request intake is not connected to a Civil assessment/scope/costing workflow. |
| MNT-002 | Gap | HOD request -> SCE assessment -> CE assessment -> scope/remediation report -> HOD costing/approval is not configured as an end-to-end workflow. |
| MNT-003 | Partial | Work assignment and project deliverables exist, but post-award team constitution, CE supervision of maintenance works, weekly report, completion report, inspection, payment direction, and closure are not controlled. |
| MNT-004 | Partial | Company building asset complaints can likely be represented through service/maintenance/issue concepts, but there is no Civil Engineering complaint workflow with assessment, scope, remedy, costing, supervision, completion, inspection, payment, and closure. |
| MNT-005 | Gap | Maintenance/complaint workflows do not yet reconcile Civil Engineering scope/remedy decisions with Maintenance job card/work order, Procurement/contractor award, QS costing, Finance payment, and asset history. |

### Development Approval And Permitting

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| PER-001 | Partial | Project approval register, documents, stage gates, and approval reports exist. A Building Inspectorate development-approval file workflow is not represented as a named process. |
| PER-002 | Gap | Inter-section file movement after site inspection from Building Inspectorate through Architecture to SCE is not captured with received date, sender, section comments, due date, and handoff history. |
| PER-003 | Gap | SCE engineering drawing review comments, approval/rejection recommendation, correction requests, and final forwarding to HOD are not implemented as structured permitting review records. |
| PER-004 | Gap | HOD review/approval or rejection of Civil Engineering permitting recommendations is not workflow-enforced with evidence and final outcome. |

### Direct Assignment, Field Feedback, And Mobile Execution

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| ASN-001 | Partial | Projects includes work items, deliverables, mobile assignments, users/resources, comments, and due dates. Direct assignment from SCE/CE to Technicians, Draftsmen, and Artisans with feedback and urgent-task escalation is not delivered as a polished shared Civil task board. |
| ASN-002 | Partial | The mobile project workspace loads assignments and supports assigned-user status/progress changes, notes, time, expenses, and evidence upload. Uploaded evidence is currently stored as project-level document metadata rather than reliably linked to the work item, and Civil measurements, drawing markups, structured test evidence, offline capture, and completion acknowledgement remain incomplete. |
| ASN-003 | Gap | No direct assignment lifecycle records assignee acknowledgement, start, blocked/review state, completion feedback, reviewer acceptance, send-back, and rework as enforceable transitions. |

### Documents, Engineering Files, And External Tool Outputs

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| DOC-001 | Verified baseline | Project documents, drawings, deliverables, external deliverable submission/review records, and business-partner access policies exist. |
| DOC-002 | Partial | Global file upload policy exists, but the default virus-scan implementation reports `Skipped` and the baseline seed does not require scanning. Civil Engineering policy for AutoCAD, Protastructure, Revit, PDF, StaadPro, Office, and related outputs is not configured with approved extensions, metadata, preview/download controls, immutable version lineage, retention, and malware hard stops. |
| DOC-003 | Gap | Design calculation packages, specifications, drawing packages, and third-party software outputs are not tied to Civil workflow checklist evidence, record-specific artifact links, and approval gates. |

### Reports, Dashboards, And Integration Fit

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| RPT-001 | Verified baseline | Projects has reports for approval watch, design-control watch, site-control watch, post-handover watch, material reconciliation, phase-gate readiness, construction commercial, risk/issues, and workflow approval queue. |
| RPT-002 | Partial | Existing reports are project-wide. Civil-specific workload reports for design tasks, drafting backlog, site reconnaissance, RFIs, site instructions, weekly supervision, test reports, IPC endorsements, maintenance works, complaints, and permitting files are incomplete. |
| RPT-003 | Partial | Project module integrates with QS/commercial, materials, procurement, limited maintenance follow-through, workflow, reports, operation-level authorization, and external collaboration controls. Civil Engineering acceptance still requires explicit interface rules for Maintenance/Assets, Building Inspectorate/permitting, QS/payment certification, document storage, assignment evidence, contractors/consultants, and notifications. |

## Key Workflow Traceability

| No. | Source workflow or requirement | Current coverage | Implementation task IDs |
| ---: | --- | --- | --- |
| 1 | Engineering designs for new projects | Partial. Projects has drawings/submittals/RFIs/documents, but Civil design task routing and review package control are incomplete. | `CIV-0101` through `CIV-0106`, `CIV-E2E-001` |
| 2 | Project supervision | Partial. Site instructions, RFIs, quality, reports, and commercial records exist, but PE assignment, weekly reports, test-report vetting, and IPC endorsement are not controlled. | `CIV-0201` through `CIV-0206`, `CIV-E2E-002` |
| 3 | Maintenance works | Partial. Maintenance and project follow-through foundations exist, but Civil assessment/scope/costing/supervision/closure workflow is missing. | `CIV-0301` through `CIV-0305`, `CIV-E2E-003` |
| 4 | Development approval/permitting | Gap/Partial. Approval registers exist, but Building Inspectorate file movement and engineering drawing review are not configured. | `CIV-0401` through `CIV-0404`, `CIV-E2E-004` |
| 5 | Complaint resolution on company building assets | Partial. Related issue/maintenance concepts exist, but Civil complaint lifecycle is missing. | `CIV-0301`, `CIV-0303`, `CIV-0306`, `CIV-E2E-005` |
| 6 | Third-party engineering file upload | Partial. Project documents exist; Civil file policy, metadata, versioning, and checklist linkage are incomplete. | `CIV-0105`, `CIV-0601`, `CIV-E2E-006` |
| 7 | Direct assignment to Technicians, Draftsmen, and Artisans | Partial. Project assignments exist; direct Civil task assignment and feedback board are incomplete. | `CIV-0501` through `CIV-0504`, `CIV-E2E-007` |
| 8 | Reports and management visibility | Partial. Project reports exist; Civil-specific dashboards and weekly packs are incomplete. | `CIV-0602` through `CIV-0604`, `CIV-E2E-008` |

## Implementation Roadmap

### Phase 0 - Configuration, Workflow, Access, And Audit Architecture

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| CIV-0001 | P0 | Not started | Resolve `CIV-CFG-001` through `CIV-CFG-013` as effective-dated configuration. | Every configuration item has owner, approved value, effective date, evidence, and tenant seed plan. |
| CIV-0002 | P0 | Not started | Build Civil Engineering policy/config register. | Workflow templates, file policies, assessment templates, report formats, review checklists, urgent task rules, and authority levels are versioned and queryable. |
| CIV-0003 | P0 | Not started | Define Civil Engineering audit event map. | Every Civil action records actor, role, source, before/after values, file/version evidence, approval state, and correlation ID. |
| CIV-0004 | P0 | Not started | Configure Civil roles, permissions, and project/asset access scopes. | Test users can only see and act by role, project, asset/building, section, assignment, and approval authority. |
| CIV-0005 | P0 | Not started | Add shared workflow integration for Civil records. | Design reviews, drafting tasks, supervision reports, test reports, maintenance scopes, complaints, permitting reviews, and direct assignments use shared workflow controls and direct-API enforcement. |

### Phase 1 - Engineering Design, Drawings, Specifications, And File Control

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| CIV-0101 | P0 | Not started | Configure Civil design workflow inside Projects. | HOD -> SCE -> CE -> SCE -> Draftsman -> SCE -> HOD routing is enforced with task states, due dates, evidence, and review outcomes. |
| CIV-0102 | P0 | Not started | Add site reconnaissance and design-input records. | Reconnaissance notes, photos, constraints, information sources, and cross-section inputs link to project/design package. |
| CIV-0103 | P0 | Not started | Add Civil design review checklist. | Design, drawings, calculations, specifications, and dependencies must pass SCE review before HOD submission. |
| CIV-0104 | P1 | Not started | Add cross-section information request workflow. | Architecture, Geodetic, Town Planning, and other sections can provide inputs, respond, attach evidence, and appear in design readiness. |
| CIV-0105 | P0 | Not started | Configure engineering file metadata/versioning. | AutoCAD, Protastructure, Revit, PDF, StaadPro, Office, and related files store discipline, revision, owner, reviewer, status, and lineage. |
| CIV-0106 | P1 | Not started | Add design package submission to HOD. | SCE submits complete designs/drawings/specification package to HOD; incomplete packages are blocked and audited. |

### Phase 2 - Project Supervision, Site Instructions, RFIs, Test Reports, And IPC Endorsement

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| CIV-0201 | P0 | Not started | Implement Project Engineer assignment control. | HOD/Projects Coordinator assigns SCE or CE as PE with project role, effective date, authority, and audit trail. |
| CIV-0202 | P0 | Not started | Enforce site instruction routing through Project Manager. | Site instructions raised by PE route through PM and show acknowledgement/response history. |
| CIV-0203 | P0 | Not started | Enforce RFI response routing through Project Manager. | RFIs from contractors/consultants route to PE/PM and responses are versioned, approved, and auditable. |
| CIV-0204 | P0 | Not started | Add engineering test-report register and vetting. | Test report type, source/lab, result, pass/fail, attachment, reviewer, endorsement, and linkage to package/IPC are captured. |
| CIV-0205 | P0 | Not started | Add weekly supervision report workflow. | CE reports contractor/labour-gang activities to SCE; SCE submits to HOD with comments, evidence, and overdue escalation. |
| CIV-0206 | P1 | Not started | Add PE review/endorsement for IPCs. | Completed IPC from Projects Coordinator requires PE review, endorsement/rejection, notes, and return status before onward processing. |

### Phase 3 - Maintenance Works And Company Building Asset Complaints

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| CIV-0301 | P0 | Not started | Build Civil maintenance/complaint intake. | Scheduled maintenance, breakdown maintenance, and company asset complaints can be logged with asset/building, requester, priority, evidence, and source. |
| CIV-0302 | P0 | Not started | Add assessment, scope, and remediation report workflow. | HOD -> SCE -> CE assessment produces scope/remedy report and HOD review decision. |
| CIV-0303 | P0 | Not started | Add costing and approval handoff. | Approved scopes route to QS/Procurement/Finance/Management as configured and return award/approval status. |
| CIV-0304 | P0 | Not started | Link to Maintenance job cards/work orders where appropriate. | Asset-based corrective works create or link Maintenance job cards/work orders without losing Civil scope and supervision evidence. |
| CIV-0305 | P0 | Not started | Add maintenance supervision, completion, inspection, payment, and closure controls. | CE weekly reports, completion report, SCE/HOD review, inspection direction, payment direction, and closure are enforced. |
| CIV-0306 | P1 | Not started | Add company building complaint resolution lifecycle. | Complaint assessment, remedy, approval, work execution, completion, inspection, payment, and closure are traceable and reportable. |

### Phase 4 - Development Approval And Permitting Review

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| CIV-0401 | P1 | Not started | Add development approval file register. | Building Inspectorate applications/files capture applicant, property, site inspection, current section, due date, attachments, and status. |
| CIV-0402 | P1 | Not started | Add inter-section file handoff workflow. | File movements between Building Inspectorate, Architecture, Civil Engineering, HOD, and other sections are timestamped and auditable. |
| CIV-0403 | P1 | Not started | Add SCE engineering drawing review. | SCE comments, correction requests, recommendation, reviewer, and evidence are captured against the file. |
| CIV-0404 | P1 | Not started | Add HOD permitting decision. | HOD approves, rejects, or sends back Civil recommendations with reason and audit history. |

### Phase 5 - Direct Assignment, Feedback, And Mobile Field Execution

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| CIV-0501 | P0 | Not started | Add Civil task assignment board. | SCE/CE can assign work to Civil Engineers, Technicians, Draftsmen, and Artisans with role, due date, priority, files, and instructions. |
| CIV-0502 | P0 | Not started | Add feedback and acceptance workflow. | Assignees acknowledge, update progress, attach evidence, mark complete, and reviewer accepts or sends back. |
| CIV-0503 | P1 | Not started | Add urgent task path. | Urgent assignments can bypass normal batching with reason, SLA, notification, escalation, and audit. |
| CIV-0504 | P1 | Not started | Extend mobile project workspace for Civil field feedback. | Field users can capture site notes, photos, measurements, task progress, and completion feedback online/offline. |

### Phase 6 - Reports, Dashboards, Integrations, Migration, And UAT

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| CIV-0601 | P0 | Not started | Configure Civil document and engineering file policy. | File extensions, metadata, virus scanning, retention, legal hold, versioning, preview/download, and access rules are enforced. |
| CIV-0602 | P1 | Not started | Deliver Civil Engineering dashboard and report pack. | Design backlog, drafting tasks, RFIs, site instructions, weekly reports, test reports, IPC endorsements, maintenance works, complaints, permits, and overdue assignments reconcile. |
| CIV-0603 | P1 | Not started | Produce Civil Engineering interface control document. | Projects, Maintenance/Assets, Workflow, DMS, QS/Finance, Building Inspectorate, contractors/consultants, notifications, and reports have owners and controls. |
| CIV-0604 | P0 | Not started | Enforce Civil security and direct-API hard stops. | Unauthorized design approvals, file access, task reassignment, permitting recommendations, maintenance closure, and report drilldowns are rejected and audited. |
| CIV-0605 | P1 | Not started | Build Civil migration and data-quality workbench. | Historical drawings, specifications, site reports, permits, maintenance scopes, complaints, test reports, and physical file references validate before posting. |
| CIV-0606 | P1 | Not started | Run end-to-end Civil UAT and go-live acceptance. | Representative design, supervision, maintenance, complaint, permitting, file upload, direct task, dashboard, and integration scenarios pass. |

## Mandatory End-To-End Acceptance Scenarios

| Scenario ID | Status | Scenario |
| --- | --- | --- |
| CIV-E2E-001 | Not started | HOD directive -> SCE information gathering -> CE design task -> SCE design review -> Draftsman drawing task -> SCE drawing review -> HOD design/drawing/specification submission. |
| CIV-E2E-002 | Not started | Project Engineer assigned -> site instruction through PM -> contractor/RFI response -> vetted test report -> weekly report CE -> SCE -> HOD -> IPC endorsement. |
| CIV-E2E-003 | Not started | Scheduled/breakdown maintenance request -> SCE/CE assessment -> scope/remediation report -> costing/approval -> award -> CE supervision -> completion report -> inspection/payment/closure. |
| CIV-E2E-004 | Not started | Building Inspectorate application -> file handoff after site inspection -> Architecture to SCE -> engineering drawing review -> SCE recommendation -> HOD decision. |
| CIV-E2E-005 | Not started | Company building asset complaint -> assessment -> remedy recommendation -> costing/approval -> work execution -> weekly report -> completion -> inspection/payment/closure. |
| CIV-E2E-006 | Not started | AutoCAD/Revit/StaadPro/PDF design output uploaded -> metadata/version captured -> checklist evidence satisfied -> reviewer approval -> access-controlled archive. |
| CIV-E2E-007 | Not started | SCE urgent task assignment to Technician/Draftsman/Artisan -> acknowledgement -> feedback/photos/files -> completion -> reviewer acceptance or send-back. |
| CIV-E2E-008 | Not started | Civil Engineering dashboard reproduces design backlog, drafting workload, RFIs, site instructions, weekly reports, test reports, maintenance works, complaints, permits, and overdue items from source records. |
| CIV-E2E-009 | Not started | Direct API attempts for unauthorized design approval, drawing access, task reassignment, permitting recommendation, maintenance closure, and report drilldown are rejected and audited. |

## Current Code Evidence Anchors

These anchors justify the baseline classifications; they are not proof of final TDC acceptance:

- Project entities: `src/ErpSystem.Core/Entities/Projects/ProjectManagementEntities.cs`
- Project DTOs: `src/ErpSystem.Core/DTOs/Projects/ProjectManagementDtos.cs`, `src/ErpSystem.Core/DTOs/Projects/ProjectManagementSummaryDtos.cs`
- Project service interface: `src/ErpSystem.Core/Interfaces/Projects/IProjectServices.cs`
- Project services: `src/ErpSystem.Core/Services/Projects/ProjectServices.cs`, `ProjectService.Authorization.cs`, `ProjectService.DesignSiteControls.cs`, `ProjectService.ConstructionReports.cs`, `ProjectService.HandoverDefects.cs`, `ProjectService.MaintenanceFollowThrough.cs`, `ProjectService.Mobile.cs`, `ProjectService.LinkedRecords.cs`, `ProjectService.PackageWorkflowSync.cs`
- Project API: `src/ErpSystem.Api/Controllers/Projects/ProjectsController.cs`, `src/ErpSystem.Api/Controllers/Projects/ProjectMobileController.cs`, `src/ErpSystem.Api/Controllers/Projects/ProjectAdministrationController.cs`
- Project frontend service: `frontend/src/services/projectService.ts`
- Project workspace: `frontend/src/app/development/projects/[id]/ProjectWorkspacePage.tsx`
- External project collaboration: `frontend/src/app/development/projects/[id]/components/ProjectAccessTab.tsx`, `frontend/src/app/external-portal/projects/[id]/page.tsx`
- Civil-fitting project tabs: `frontend/src/app/development/projects/[id]/components/ProjectDesignTab.tsx`, `ProjectSiteControlsTab.tsx`, `ProjectApprovalsTab.tsx`, `ProjectGovernanceTab.tsx`, `ProjectHandoverTab.tsx`, `ProjectDefectsTab.tsx`, `ProjectDocumentsTab.tsx`, `ProjectExecutionTab.tsx`, `ProjectMaterialsTab.tsx`
- Mobile project assignments: `frontend/src/app/development/project-mobile/page.tsx`, `frontend/src/app/development/project-mobile/page.test.tsx`
- Project reports: `frontend/src/app/development/project-reports/page.tsx`, `frontend/src/app/development/project-analytics/page.tsx`
- Project administration: `frontend/src/components/projects/ProjectManagementAdminPage.tsx`, `frontend/src/components/projects/ProjectPhaseLibraryAdmin.tsx`
- Maintenance follow-through endpoints: defect-liability/customer-variation job card and work order creation in `ProjectsController.cs`
- Shared workflow adapters: `src/ErpSystem.Core/Services/Workflow/WorkflowStatusAdapters.cs`
- Existing QS tracker for overlapping commercial/IPC/certificate controls: `docs/tdc-quantity-survey-gap-implementation-tracker.md`

## Verification Log

| Date | Check | Status | Result |
| --- | --- | --- | --- |
| 2026-07-09 | Initial source DOCX render with artifact-tool | Superseded | Initial 5-page render in `artifacts\tdc-civil-engineering-doc-render` was useful for analysis but did not match Microsoft Word pagination. |
| 2026-07-09 | Structured DOCX extraction | Passed | 73 paragraphs and 0 tables were extracted, including the five key workflows and two other comments. |
| 2026-07-09 | Rendered page visual contact review | Passed | Contact sheet confirmed source structure from key workflows through other comments. |
| 2026-07-09 | Live project-module source audit | Passed | Project entities, DTOs, services, controllers, workspace tabs, project reports, design/site controls, handover/defects, mobile assignments, maintenance follow-through, and workflow touchpoints were inspected. |
| 2026-07-09 | Tracker creation | Passed | Project-module fit conclusion, coverage matrix, configuration inputs, roadmap, acceptance scenarios, requirement traceability, and code anchors created. |
| 2026-07-17 | Authoritative Microsoft Word source render and visual review | Passed | Word rendered the source to 3 pages in `artifacts\tdc-civil-engineering-srs-revalidation\run-20260717-1\source-render`; every page was reviewed against the extracted paragraph sequence. |
| 2026-07-17 | Source-to-tracker reconciliation | Passed | All 42 substantive source workflow steps and cross-cutting expectations are mapped to requirement IDs, implementation tasks, and acceptance scenarios. |
| 2026-07-17 | Live implementation revalidation | Passed | Operation-level project authorization, external access policies/deliverable review, mobile assigned-task updates, project-level evidence limitation, workflow adapter coverage, maintenance follow-through boundaries, permitting absence, document lineage, and file-scan policy were rechecked in source. |
| 2026-07-17 | Tracker task-reference integrity audit | Passed | Every referenced `CIV-*` configuration, implementation, and acceptance ID resolves to a defined tracker entry. |

## Tracker Maintenance

- Start every implementation slice by selecting explicit `CIV-*` task IDs.
- Keep this tracker synchronized when code changes alter baseline coverage.
- Do not mark a task `Done` until backend, frontend, configuration, workflow, audit, tests, migrations, and smoke evidence are present.
- Prefer configuration over hard-coded policy for Civil roles, workflow routing, review checklists, file types, task SLAs, report formulas, and access controls.
- Keep Civil Engineering inside the Projects module unless a future signed architecture decision proves a separate module is necessary.
- Do not remove requirements from the tracker. Record an approved scope decision with evidence if TDC defers or changes a requirement.
