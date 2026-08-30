# TDC Civil Engineering UAT Execution Matrix

## Purpose and status

This is the execution record for `CIV-0606`. It maps each Civil acceptance flow to the implemented UI and API boundaries. It is not evidence that a scenario has passed: each row remains pending until the named tester records the IDs, timestamps, correlated audit event and outcome in the Civil tracker.

## Controlled test setup

Use a non-production tenant and a single named project. Before running any scenario, confirm all of the following through the existing administration pages and APIs:

- The active Civil configuration profile is published, the relevant `CIV-CFG-001` through `CIV-CFG-013` decisions are approved/verified, and their DMS template/workflow/role selectors resolve to active tenant records.
- The HOD, SCE, Civil Engineer, Project Engineer, Project Manager, Draftsman, Technician/Artisan, Building Inspectorate, and external contractor test users are active. Each internal user has both the applicable Security role and active `ProjectMember` role for the test project.
- The test project has the controlled contractor Business Partner, a permitted external-access policy, and published central-DMS evidence available where the scenario needs it. Do not use a document ID from another tenant or an unpublished version.
- Record IDs, role/user IDs, central-DMS document/version references, workflow IDs, API correlation IDs, and Audit Log entries in the evidence sheet. Do not use free-text IDs in place of the selectors in the UI.

## UI locations

| Area | Tested UI location |
| --- | --- |
| Project design and supervision controls | `Development -> Projects -> selected project -> Design` (`/development/projects/{projectId}/design`) and `Site` (`/development/projects/{projectId}/site-controls`) |
| Civil maintenance flows | `Development -> Civil Maintenance Intake`, `Assessments`, `Costing Handoffs`, `Execution`, and `Completion` |
| Civil permitting | `Development -> Development Approval Files`, `Development File Handoffs`, `SCE Engineering Reviews`, and `HOD Permitting Decisions` |
| Direct field work | `Development -> Civil Task Assignments` and `Development -> Mobile` |
| Reports | `Reports -> Civil Engineering` (`/reports/civil-engineering`) |
| Historical data staging | `Development -> Civil Migration Workbench` |
| Configuration | `Administration -> Project Management -> Civil Engineering Configuration` |

## Required acceptance scenarios

| ID | Tester sequence | Expected controlled result | Evidence to retain |
| --- | --- | --- | --- |
| `CIV-E2E-001` | HOD/SCE/CE/Draftsman work in the selected project’s **Design** tab: create the governed design case; complete reconnaissance/information-gathering; assign and submit the design; review it; assign and submit drafting; review the drawing; submit the complete design/drawing/specification package to HOD. | Each action is available only to the configured active project role, required current-Published DMS evidence is selected, reviewer and maker are separated, and the final HOD submission rejects missing, superseded, or unapproved package evidence. | Design case ID, workflow instance, DMS record/version IDs, history/audit entries, and screenshots of the final state and one rejected missing-evidence attempt. |
| `CIV-E2E-002` | In the project’s **Site** tab, assign the Project Engineer, issue a site instruction through the Project Manager, submit an RFI using the controlled external contractor account, record Project Engineer/Manager responses, record a quality test and its review, submit/review weekly supervision, then submit/review the IPC endorsement. | Project Engineer/Manager/contractor identities are derived from controlled project membership or Business Partner access. The test, report, instruction, RFI and IPC keep immutable history, configured workflow/evidence and no cross-project access. | Assignment, routing, external-response, test/report/IPC IDs; workflow and audit IDs; screenshots from the Site tab. |
| `CIV-E2E-003` | Use the dedicated Civil Maintenance pages: create a scheduled or breakdown intake; carry out independent assessment and HOD outcome; submit/approve the costing handoff; create/refresh the authoritative Maintenance execution link; submit completion direction, inspection outcome and closure. | Civil retains assessment/direction evidence while Maintenance remains the job-card/work-order owner and Finance remains payment/ledger owner. The Civil page must not create an invoice, payment or journal entry. | Intake, assessment, costing-handoff, execution-link and completion-control IDs, linked Maintenance reference, Finance-direction reference, audit trail and screenshots. |
| `CIV-E2E-004` | Create a controlled development-approval file, record site inspection, use the handoff register for the Architecture-to-SCE handoff, submit the SCE engineering review, then make the HOD decision. | Only configured active tenant/project/section actors may progress the file; required drawing/evidence is current Published DMS; SCE review and HOD decision are distinct and immutable. | File, handoff, review and HOD-decision IDs; DMS versions; audit history; screenshot of final decision. |
| `CIV-E2E-005` | Create the complaint in the authoritative Helpdesk/Maintenance owner flow, then open **Civil Complaint Resolution** and follow the linked assessment, costing, execution, weekly-report, completion, inspection and closure records through their owning pages. | Civil projects the controlled complaint lifecycle but does not create a duplicate complaint, work order, payment or close record. Unauthorized users cannot view the linked timeline. | Helpdesk ticket, Civil projection/timeline, assessment/execution/completion IDs, linked owner records and audit entries. |
| `CIV-E2E-006` | Upload approved representative AutoCAD/Revit/StaadPro/PDF/Office outputs through central DMS; publish the required version/metadata; choose it from the Civil Design document selector; submit and independently approve it; prove authorized preview/download and an invalid extension/unpublished version rejection. | DMS owns files, scan status, metadata, retention, legal hold, versioning and access. Civil stores only controlled current-Published DMS references. | DMS document/version IDs, metadata and scan outcome, Civil document history, authorized and unauthorized access outcome, audit events. |
| `CIV-E2E-007` | In **Civil Task Assignments**, SCE/CE creates an urgent task for an eligible Technician/Draftsman/Artisan; assignee acknowledges/updates/completes from the assignment or **Mobile** page; reviewer accepts or returns it; run one offline queue/replay attempt with the original idempotency key. | Roles, active project membership, controlled assignee/DMS/unit selectors, SLA, maker-checker, row version and idempotency are enforced. A duplicate replay does not make a second feedback event. | Task/control ID, feedback history, notification/audit entries, original client request ID, offline timestamp and replay result. |
| `CIV-E2E-008` | Open `Reports -> Civil Engineering`, select the test project and inclusive date range, run each applicable report and export one permitted format; compare the reported counts/IDs to the source design, task, RFI, site instruction, weekly, test, IPC, maintenance, complaint and permitting records. | Reports and exports honour Civil report permission plus project/tenant scope. Each reported record reconciles to an authoritative source record; a user without export permission is denied. | Report run/audit ID, filters, source-record comparison, export result, no-export denial and screenshots. |
| `CIV-E2E-009` | With a signed-in user lacking the relevant Civil permission or project role, call direct APIs for design approval, design-document decision, task assignment/feedback review, permitting engineering review/HOD decision, maintenance completion process, report/export and the migration workbench. Repeat one lookup/read with a project or DMS ID from a second tenant. | Calls return a safe `401/403/404` as applicable; no state changes; the application records the security/audit outcome according to the central logging policy. Never expose stack traces or cross-tenant record details. | Request route/method, actor, correlation ID, HTTP status and safe problem code, before/after record state, audit/security-log reference. |

## CIV-0605 migration-workbench operational check

Run after the new migration has been applied to the disposable test database:

1. In **Civil Migration Workbench**, stage a spreadsheet/document-register batch against the selected project using only current Published DMS documents. A repeated source reference or invalid/missing DMS pair must be retained as a validation error, without creating an owner record.
2. Reconcile a clean batch using a different configured reviewer and the DMS template selected by `CIV-CFG-013`. The submitter must be denied.
3. Sign off with a third configured role. The batch may become `ReadyForOwnerPosting`, but the UI/API must expose no owner-post action and no Design, Permitting, Maintenance, Complaint, Quality, DMS or Finance record may be added.
4. Repeat the original client request unchanged and confirm it returns the same batch; repeat it with changed content and confirm a safe conflict.
5. Repeat lookup, stage and history APIs from an unauthorized user and a second tenant. Retain the 403/404 result and audit evidence.

## Completion rule

`CIV-0606` is not Done until every required scenario is executed against the configured test database, failures are remediated and rerun, the migration/trigger checks pass, browser/API evidence is attached, and the tracker records the result. This document must be updated from exact runtime evidence, not inferred from source code.
