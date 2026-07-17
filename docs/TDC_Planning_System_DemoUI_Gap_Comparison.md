# TDC Planning Requirements vs What We Have vs Gaps

**Date:** 14 July 2026  
**Purpose:** Compare the TDC Town Planning Section SOP against the combined solution baseline: current RHEMA ERP plus DemoUI items intended to be brought into the ERP, before producing the final module-owner requirements document.

## Source Documents Reviewed

| Source document | Main process covered |
|---|---|
| `PLANNING Standard Operating Procedure.docx` | Town Planning Section SOP covering vetting land allocation/licence applications, change of use reviews, planning schemes/layouts, site reports, site plans, official search/data provision, development permit conformity review, regularization, layout review/correction, compliance inspections, dispute/client complaints, and District Assembly Spatial Planning Committee meetings. |

## Status Legend

| Status | Meaning |
|---|---|
| Available | The combined baseline already has a usable module, entity, API, or page for this area. |
| Partial | The system has a foundation, but it does not yet meet the TDC process end-to-end. |
| UI reference only | A page/workspace exists for adjacent work, but not as a dedicated Town Planning workflow. |
| To add | Required for TDC Planning, but not currently implemented as a complete usable feature. |

## Key Finding

The current ERP has useful foundations for Planning through Estate land acquisition, Estate managed assets, Development project management, project design registers, and project approval registers. These support planning compatibility, zoning, land reference, cadastral/survey data, statutory approvals, drawing registers, and project planning tabs.

However, the Town Planning Section SOP is not yet implemented as a dedicated Planning module. There is an `IPlanningProcedureCatalogService` interface, but no matching Planning procedure catalog implementation, no Planning API controller, no Planning frontend procedure page, and the shared `ProcedureCaseWorkspace` currently accepts only Legal and Facilities modules. The external portal permit page is also marked "Coming Soon." Therefore, Planning should be described as partially scaffolded with strong adjacent data, but the actual TDC Planning workflow still needs to be added.

## Baseline Position

For this comparison, current ERP and DemoUI are treated together as what we have because DemoUI items are expected to be brought into the ERP. No separate Planning DemoUI source project was found in this workspace. The available UI references are adjacent live application areas:

- `frontend/src/app/development/projects/[id]/...` for Development project planning, design, approvals, documents, and project workspace tabs.
- `frontend/src/app/estate/land-acquisition/page.tsx` for land acquisition physical assessment, planning scheme reference, planning compatibility, zoning and planning clearance.
- `frontend/src/app/estate/land-management/page.tsx` for Estate managed asset planning compliance status.
- `frontend/src/app/external-portal/permits/page.tsx`, which is a "Coming Soon" permit application placeholder.

The procurement `/procurement/planning` module is not relevant to the Town Planning Section SOP; it is procurement planning, not physical/spatial planning.

## Baseline Components Relevant to Planning

| Component | Coverage |
|---|---|
| `IPlanningProcedureCatalogService` | Planning procedure catalog contract exists, but no implementation/controller/UI wiring was found. |
| `ProcedureCase` engine | Could support Planning cases, fields, checklists, documents, activities, stages, and workflow linkage, but backend/frontend currently support only Legal and Facilities as procedure modules. |
| Estate Land Acquisition | Captures intended use, location, coordinates, physical assessment, zoning classification, planning scheme reference through UI workspace data, planning compatibility, planning evidence, statutory consent, cadastral survey, and documents. |
| Estate Managed Assets | Stores zoning classification, planning compliance status, GIS layer reference, boundary coordinates, survey plan number, map sheet number, cadastre description, area, district/town, and readiness for project management. |
| Development Project Management | Captures development profile, site name/address, land reference, project phases, planning tab, design/drawing register, documents, approvals, governance, and history. |
| Project Approval Register | Tracks planning permission, building permit, environmental permit, fire clearance, utility clearance, occupancy certificate, authority, reference number, status, submitted date, target decision date, approved date, expiry date, conditions, and notes. |
| Project Design Register | Tracks drawings, discipline, revision, issue date, review due date, status, submittals, and review workflow for project design artifacts. |
| Workflow administration | Can configure workflows generally, but Planning is not yet registered as a supported procedure module. |
| External permit portal | Permit applications page exists but is marked "Coming Soon." |

## Comparison Matrix

| TDC Planning requirement area | What we have | Status | What we still need to add |
|---|---|---|---|
| Planning procedure catalogue | Interface exists for Planning procedure catalogue, but no implementation, API controller, or registration was found. No dedicated Planning procedure page found. Contract shape exists and can mirror Legal/Facilities catalog pattern. | To add | Implement `PlanningProcedureCatalogService`, Planning API controller, frontend Planning landing page, Planning procedure service, and workspace routes. |
| Live Planning case workspace | Shared `ProcedureCase` engine can support procedure cases, stages, fields, checklists, documents, and activity logs. `ProcedureCaseWorkspace` currently accepts only Legal and Facilities. Reusable case engine already exists. | Partial | Extend procedure case backend/frontend to support module `Planning`, including seed stages for all 12 SOP processes. |
| Vetting applications for land allocation or temporary licence | Estate land acquisition records capture intended use, location, coordinates, planning compatible flag, zoning classification, and planning evidence. Estate land-acquisition UI includes planning purpose and planning/constraints sections. Land acquisition has relevant data capture and document upload. | Partial | Add Planning vetting workflow from HOD to STP to assigned planner, internal technical review, master plan/layout cross-reference, site visit report, recommendation/rejection with justification, and return to HOD. |
| Change of Use Review | No dedicated change-of-use entity or committee workflow found. Estate managed assets have zoning and planning status fields. No dedicated change-of-use UI found. Zoning and planning status fields can support the decision record. | To add | Add change-of-use application register, committee review stage, permissible-use check, site visit, stakeholder/neighborhood consultation, decision recommendation, and HOD committee-chair approval. |
| Preparation of Planning Scheme / Layout | Development projects have plan/design tabs and drawing registers. Estate land records hold cadastral/survey data. Development project workspace has Planning and Design tabs. Project design/drawing register can track layout drawings generally. | Partial | Add planning scheme/layout workflow for new acquisitions, base map intake, DOS/draughtsman assignment, layout versioning, technical reports, HOD approval, and final layout archive. |
| Site Report | Land acquisition physical assessment and project site controls provide some site data capture. Estate acquisition UI and Development site-controls tab are adjacent references. Site visit-related fields and documents exist in adjacent modules. | Partial | Add Planning site-report request workflow, assigned officer, visit date, ground situation, photos/GIS evidence, STP review, and HOD submission. |
| Preparation of Site Plan | Estate and Development hold survey plan numbers, map sheet numbers, boundary coordinates, cadastral descriptions, and drawing registers. Estate land management and Development design tabs are relevant. Survey/cadastral/drawing foundations exist. | Partial | Add site-plan production workflow: DOS assignment, draughtsman tasking, plot dimensions, road access, utility corridor, buffer/easement checks, coordinate validation, sign-off by draughtsman/DOS/STP, and HOD return. |
| Official Search and Provision of Data | Estate managed assets and land acquisition records can hold zoning, site plan, cadastral, ownership and registration information. Estate land management UI shows planning status and land record details. Land and estate records can be searched if populated. | Partial | Add official search request register, consent-for-search document, manual/digital search checklist, layout superimposition result, Estate/Revenue verification requests, search outcome letter/data sheet, and HOD return. |
| Development Permit Conformity Review | Development project approval register supports planning permission/building permit status and authority details. Development approvals tab tracks statutory approvals. External permit page is Coming Soon. Approval register has useful permit tracking fields. | Partial | Add conformity review workflow for site plan/layout/land-use/height-zoning compatibility, revision requests, endorsement, and onward processing. |
| Undertake Regularization | No dedicated regularization module found. Estate land acquisition and managed assets have land record fields that can support it. No regularization UI found. Estate land and site plan foundations can be reused. | To add | Add regularization request workflow, record verification, layout/on-ground cross-reference, area profiling/site report, regularization committee visit, MD approval/disapproval, HOE/HOD/STP handoff, and site plan preparation. |
| Layout Review and Correction | Project design/drawing register can track drawings/revisions; Estate records hold layout-adjacent data. Development design tab can track drawing revisions, but not official planning layout corrections. Drawing register can manage revisions generally. | Partial | Add approved-layout anomaly register, audit-finding linkage, Estate file request, DOS correction workflow, updated layout version control, STP vetting, and HOD submission. |
| Compliance Site Inspection and Reporting | Maintenance/inspection modules exist generally, and Estate/Development capture site-related data. No dedicated Planning compliance inspection page found. General inspection and site controls concepts exist. | Partial | Add Planning compliance inspection request, assigned technical officer, inspection checklist, photo/GIS evidence, non-compliance findings, recommendations, report approval, and follow-up actions. |
| Dispute Resolution and Client Complaint Management | CRM may manage complaints generally, and Estate records can support land/boundary facts. No Planning-specific dispute workflow found. No dedicated Planning dispute/complaint UI found. General customer/contact and land record foundations exist. | Partial / To add | Add Planning dispute register, boundary complaint intake, linked parcel/site plan, internal review, evidence from records, assigned officer, resolution recommendation, HOD decision, and client response tracking. |
| District Assembly Spatial Planning Committee meetings | Project approval register can track external statutory approvals, but not meeting attendance/reporting. No committee-meeting UI found. Authority/reference/status fields may support external body records. | To add | Add MMDA/SPC meeting invitation register, officer assignment, attendance record, agenda/minutes upload, TDC position/issues log, meeting report, and HOD follow-up action. |
| HOD to STP file movement | ProcedureCase can capture assigned role and activities generally if Planning is enabled. No Planning case UI yet. Case/activity framework exists. | Partial | Add Planning file movement register with HOD receipt, STP receipt, assigned planner/physical planner/DOS/draughtsman, dates, status, and return-to-HOD confirmation. |
| Internal technical review | No Planning-specific technical review checklist found. No Planning technical review UI found. Workflow/checklist framework can support it. | To add | Add configurable Planning review checklist for land use, master plan/layout, access, utilities, buffers, easements, flood risk, height zoning, conflicts and constraints. |
| Master plan / layout cross-reference | Estate land records store zoning, GIS reference, boundary coordinates and survey details. Estate land management UI references planning status. Data fields exist for zoning/GIS/survey references. | Partial | Add master plan/layout repository, GIS/map overlay or attachment workflow, cross-reference result, conflict flags, and layout reference version. |
| Site visitation evidence | Adjacent modules support documents and some inspection/site fields. Estate/Development pages can attach documents. Document upload exists in several places. | Partial | Add site visit schedule, visit officer, coordinates, photo evidence, observations, constraints, recommendation, and signed site report output. |
| Recommendation / rejection with justification | ProcedureCase fields can support decisions if Planning is enabled. No Planning decision UI yet. Generic workflow fields/checklists can be reused. | Partial | Add formal recommendation status, justification, conditions, STP endorsement, HOD action, and decision letter/report output. |
| Planning document repository | ProcedureCase documents, Estate documents, project documents and drawing registers exist. Adjacent document tabs exist. Upload/document structures are available. | Partial | Add Planning document taxonomy: application letter, site plan, base map, master plan extract, layout, site report, committee minutes, search consent, consultation evidence, endorsement letter. |
| Drawing/layout version control | Development drawing register includes drawing number, title, discipline, revision, issue date, review due date and status. Development Design tab has Drawing Register. Drawing register is a strong reusable foundation. | Partial | Add official Planning layout/site-plan versioning, superseded layout control, approval signatures, spatial metadata and release history. |
| GIS / map overlay | Estate managed assets hold GIS layer reference and coordinates; land acquisition has cadastral survey coordinates. Estate land-acquisition includes map/cadastral panels. Coordinates, cadastral fields and map panels exist. | Partial | Add Planning map overlays for master plan/layout, proposed use, existing use, conflict zones, buffer/easement layers, and printable map extracts. |
| Stakeholder / neighborhood consultation | No dedicated consultation workflow found. No consultation UI found. Contacts and document upload foundations may be reused. | To add | Add consultation recommendation, invitees/stakeholders, meeting dates, objections/comments, evidence, outcome summary and decision impact. |
| Estate and Revenue checks | Estate land records exist. Finance/Revenue foundations exist, but no Planning search workflow integration found. Estate UI exists; Finance is separate. Estate and finance data can be integrated. | Partial | Add Planning-to-Estate and Planning-to-Revenue verification tasks, status, response evidence, and dependency controls before issuing official search/data response. |
| Regularization committee and MD approval | Workflow approval engine exists generally. No Planning regularization UI found. Workflow approvals can support MD approval. | Partial / To add | Add regularization committee case type, committee report, MD approval/disapproval field, committee return action and site-plan handoff. |
| District Assembly / MMDA representation | Project approval authority fields can store external authority names. Approvals tab tracks authority and references. External authority details can be captured in project approvals. | Partial | Add meeting-specific workflow for invitations, representation, reports, decisions, follow-up actions, and institutional memory. |
| Planning dashboards and reports | Development and Estate have adjacent views; no Planning Section reporting found. No dedicated Planning dashboard found. Data foundations can support reports once Planning cases exist. | To add | Add dashboards for open applications, pending STP/HOD actions, site visits due, change-of-use cases, site plans in drafting, official searches, committee meetings, disputes, and SLA ageing. |
| Audit trail and accountability | ProcedureCase and workflow entities support activities and stage history generally. Legal/Facilities case workspace shows activity history, but Planning is not enabled. Audit/activity framework exists. | Partial | Enable Planning audit trail for assignment, decisions, document uploads, recommendation changes, approvals, dispatch and closure. |
| External/client permit portal | External permit page exists. Page says Permit Applications are Coming Soon. Placeholder route exists. | UI reference only / To add | Build client-facing permit/application submission and tracking only if TDC wants public/external intake in scope. |

## What We Should Say in the Requirements Document

### Already Available

- Estate land acquisition captures planning compatibility, zoning, intended use, physical assessment, statutory consent, cadastral survey, documents and planning evidence.
- Estate managed assets capture zoning classification, planning compliance status, GIS reference, survey plan, map sheet, boundary coordinates and cadastre details.
- Development project management has project planning, design/drawing register, approval register, project documents, governance and history.
- Project approval register supports planning permission, building permit, environmental permit, fire clearance, utility clearance and occupancy certificate tracking.
- A Planning procedure catalog interface exists, which gives a starting contract for a future Planning procedure module.
- The shared workflow/procedure-case foundations can be reused for Planning once Planning is registered and wired into backend/frontend.

### Partially Available

- Planning-related data exists across Estate and Development, but not as one Town Planning Section workspace.
- Site reports, planning compatibility checks and site plan data can be represented manually through adjacent modules, but the SOP-specific handoffs are not enforced.
- Development project approvals can track permit outcomes, but not the internal Planning conformity review that happens before endorsement or onward processing.
- Drawing registers can track project drawings, but not official master plan/layout correction, superimposition, site-plan signing and release controls.
- Generic workflow and audit features exist, but Planning has not been added as a supported procedure module.

### To Add

- Dedicated Planning module landing page and procedure catalogue covering all 12 SOP activities.
- Planning procedure workspaces using `ProcedureCase` for HOD/STP/Town Planner/Physical Planner/DOS/Draughtsman/Committee/MD handoffs.
- Planning application register with applicant, file reference, source department, parcel/plot, location, proposed use, current use, zoning, master plan/layout reference, status and responsible officer.
- Change-of-use review register and committee workflow.
- Planning scheme/layout preparation workflow with base maps, reports, layout versions and HOD approval.
- Site report workflow with visit schedule, officer assignment, photos, coordinates and signed report output.
- Site plan preparation workflow with DOS/draughtsman tasks, dimension/access/utility/buffer/easement/coordinate validation and multi-signature signoff.
- Official search/data provision workflow with search consent, superimposition result, Estate/Revenue verification and HOD response.
- Development permit conformity review workflow with revision request, endorsement and onward processing.
- Regularization workflow with record checks, committee report, MD approval and site-plan handoff.
- Layout anomaly/review/correction register.
- Compliance inspection and reporting workflow.
- Planning dispute/client complaint register.
- District Assembly Spatial Planning Committee meeting register and report workflow.
- Planning dashboards, SLA ageing, officer workload, pending HOD/STP/committee actions and monthly reports.
- Document templates for site reports, search responses, recommendations, rejection justifications, endorsement letters, committee reports and meeting reports.
- GIS/master-plan/layout repository and overlay functionality, if spatial tooling is in scope.

## Recommended Final Planning Requirements Document Structure

1. Module overview and Planning Section scope.
2. Source SOP processes reviewed.
3. Existing RHEMA ERP coverage.
4. Required Planning roles and responsibilities: HOD, STP, Town Planner, Physical Planner, DOS, Draughtsman, Regularization Committee, MD, Estate, Revenue and clients.
5. Functional requirements by process area.
6. Core registers and master data.
7. Documents, templates and evidence requirements.
8. Workflow, approvals, notifications and SLA requirements.
9. Integrations with Estate, Development Projects, Finance/Revenue, Records and external permit portal.
10. Reports and dashboards.
11. Gaps and implementation priorities.
12. Open questions for TDC Planning.

## Open Clarifications for TDC Planning

1. Should Planning be a standalone module, or a sub-module under Development?
2. What official file/reference numbering format should be used for Planning applications, site reports, site plans, searches and committee meetings?
3. Which Planning outputs must be generated from templates, and who signs each output?
4. Which SOP activities require formal SLA targets?
5. Should Planning cases be linked directly to Estate land records, Development projects, or both?
6. Does TDC need GIS/map overlay functionality in phase one, or should the first release store map/layout attachments and references only?
7. Should clients submit Planning/permit requests through the external portal, or is intake internal through HOD/file registry only?
8. Which master plan/layout data exists digitally and can be migrated?
9. What are the exact regularization committee members, approval thresholds and MD approval rules?
10. What reports does HOD/STP/MD need weekly or monthly?
