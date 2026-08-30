# TDC Civil Engineering End-to-End UAT

## 1. Purpose and authority

This script validates the Civil Engineering capability against the TDC ERP Architecture and Design Document (002), specifically sections 16.5, 16.5.1, 16.5.2 and the shared technical-work workflow in section 20.4.

The architecture document is authoritative for this UAT. Civil Engineering questionnaires or departmental process notes may help configure actors and labels, but they do not add mandatory approval bands, evidence identifiers, thresholds or hard stops that the architecture document does not state.

The architecture requires one controlled lifecycle covering:

`Works request -> technical assessment -> site validation -> BOQ/cost linkage -> approval -> procurement/contractor assignment -> site instructions -> progress monitoring -> inspection -> defect correction -> completion certification -> handover -> QS/Finance linkage -> property/asset update -> reporting and audit closure`

## 2. Architecture requirements covered

| Architecture area | UAT coverage |
| --- | --- |
| Works request and technical assessment | Governed source, project, property/site/parcel, category, scope, constraints, risks and recommendation. |
| Planning/GIS validation | Site location, spatial reference, layout conformity and development constraints. |
| QS, Procurement and Finance linkage | BOQs, valuations, variations, certificates, budgets, contracts, purchase orders, contractors and Finance references where applicable. |
| Site instruction and contractor response | Versioned instruction, responsible engineer, contractor response, documents/drawings and corrective follow-up. |
| Progress monitoring | Milestones, progress percentage, planned/actual dates, contractor updates, delays, risks, dependencies and management actions. |
| Inspection and defects | Schedule, findings, photographs, drawings, GPS/spatial evidence, pass/fail result, corrective action and reinspection. |
| Variation and time/cost impact | QS valuation, Finance budget impact, Procurement contract impact and configured approval before affected execution where required. |
| Completion and handover | Defect clearance, completion inspection/certificate, retention or defects-liability reference, handover evidence, current as-built records where applicable and approval. |
| Property and asset reconciliation | Completed work, improvement, defects and handover update or link the authoritative Property, Maintenance or Asset history. |
| Reporting and audit | Engineering work register, inspection report, site instruction log, progress report, defect report, completion-certificate report, project dashboard and engineering audit trail. |

## 3. UAT users and controlled data

Use fresh records in a non-production tenant. Assign different users where the configured workflow or segregation-of-duties policy requires it.

| Test responsibility | Required access |
| --- | --- |
| Works initiator | Create Civil Engineering cases for governed sources. |
| Technical assessor/engineer | Record assessment, instructions, progress, inspections and completion evidence. |
| Planning/GIS reviewer | Validate site and spatial information. |
| QS reviewer | Review BOQ, valuation, variation and certificate references. |
| Finance reviewer | Review budget or Finance impact where a cost implication exists. |
| Procurement/contract reviewer | Confirm contractor, contract and purchase-order impact where applicable. |
| Configured approver | Make the positive or negative workflow decision. |
| Contractor test user | View permitted instructions and submit a contractor response. |
| Property/Maintenance/Asset custodian | Verify the completed-work history link. |
| Internal Audit/read-only reviewer | Verify immutable history, report output and tenant-safe access. |

Prepare these controlled records before testing:

- an approved capital project;
- an operational Maintenance escalation or work order;
- a current property/site/parcel record with a spatial reference;
- an active project with milestones and relevant project members;
- an approved contractor Business Partner and active contract where contractor execution is used;
- an approved BOQ/budget reference where the selected work has a cost implication;
- current Published central-DMS versions for site evidence, drawings, photographs, instruction evidence, variation evidence, completion evidence and as-built documents;
- a development/permit case requiring Civil Engineering technical review;
- published and effective workflow/configuration values selected by TDC for the test tenant.

Do not enter raw database IDs, workflow IDs, DMS record IDs, role IDs, contractor IDs or property IDs. Select controlled records by their business names/references. The application retains stable IDs internally.

## 4. UI locations

| Activity | UI location |
| --- | --- |
| Engineering Case, technical assessment, Planning/GIS and design evidence | `Development -> Projects -> selected project -> Design` |
| Site instructions, contractor response, RFIs, inspections, quality, progress, IPC and variation/EOT | `Development -> Projects -> selected project -> Site` |
| Operational Maintenance lifecycle | `Development -> Civil Maintenance Intake`, `Assessments`, `Costing Handoffs`, `Maintenance Execution`, and `Maintenance Completion` |
| Civil permit review | `Development -> Development Approval Files`, `SCE Engineering Reviews`, and the configured permit decision page |
| Civil reports | `Reports -> Civil Engineering` |
| Policy/configuration | `Administration -> Project Management -> Civil Engineering Policy` |

The labels above describe the current UI. The acceptance boundary remains the architecture lifecycle, regardless of how menus are grouped.

## 5. UAT-CE-01 — Capital works initiation, assessment and approval

1. Sign in as the configured works initiator.
2. Open the approved capital project and its `Design` tab.
3. Select `New Engineering Case`.
4. Select `Approved capital project` as the initiation source, then choose the actual approved project from the controlled list.
5. Select the property/site, engineering category and work classification from controlled lists.
6. Record the scope, technical assessment, constraints, risks and recommendation.
7. Save the case and note its generated reference.
8. Record the site/reconnaissance evidence required by the configured case.
9. Where spatial review is required, have the Planning/GIS reviewer select the governed site/parcel, record the spatial reference, layout conformity, conditions and constraints, attach current Published evidence, and complete the review.
10. Link the applicable BOQ, budget, contractor, contract or purchase order using controlled records. Leave a link absent when it is genuinely not applicable; the system must not invent a commercial prerequisite.
11. Submit the technical assessment and complete the configured approval workflow.

Expected:

- The case retains its authoritative source, project, property/site, category, assessment and approval history.
- A duplicate active case for the same governed source is rejected clearly without creating another case.
- Planning/GIS review is required only when the configured case requires spatial validation.
- BOQ, Finance, Procurement or contract checks apply only where scope, cost, land-use, budget or contract impact exists.
- The approval history identifies actor, action, date/time and evidence.

## 6. UAT-CE-02 — Operational maintenance initiation and execution linkage

1. Sign in as the operational works initiator.
2. Open `Civil Maintenance Intake` and select a current Maintenance escalation, request, asset and/or property from the controlled lists.
3. Record the engineering scope, constraints, risks and recommendation without copying the Maintenance owner record.
4. Complete the technical assessment.
5. Link an approved costing/BOQ and budget only where the intervention has a cost implication.
6. Use `Maintenance Execution` to link the authoritative job card or work order, or create the permitted Maintenance owner record through the offered controlled action.
7. Confirm that Civil Engineering retains the technical assessment while Maintenance remains the work-order owner.

Expected:

- The operational intervention is traceable to the Maintenance source, property/asset and Engineering Case.
- The UI does not create a duplicate request, work order, invoice, payment or journal.
- The work can proceed to monitoring only after the applicable configured technical/commercial decisions are current.

## 7. UAT-CE-03 — Versioned site instruction and contractor response

1. Open the approved project’s `Site` tab.
2. Create a site instruction and select the project, responsible engineer, controlled contractor/contract, applicable drawing or document versions and instruction date.
3. Submit or issue the instruction through the configured workflow.
4. Sign in as the selected contractor user and open only the instruction made available to that contractor.
5. Record acknowledgement/response and attach a current Published DMS version where evidence is needed.
6. Sign in as the responsible engineer, review the contractor response and record follow-up, closure or supersession as applicable.

Expected:

- Instruction number, version, source drawing/document versions, contractor and responsible engineer remain traceable.
- The response is visible only to the governed contractor and authorized project users.
- Superseding an instruction preserves the earlier version and its responses.
- Issue, acknowledgement, response, review, follow-up and closure appear in history and audit.

## 8. UAT-CE-04 — Failed inspection, corrective action and reinspection

1. Open the project’s `Site` tab and create an inspection schedule.
2. Select the site/property, purpose, scheduled date, assigned qualified inspector and required evidence.
3. Record findings, photographs, drawing references, GPS/spatial reference and a `Failed` result.
4. Create the required corrective action with an owner and target date.
5. Attempt to complete/certify the work while the failed inspection or corrective action is open.
6. Record the correction evidence.
7. Schedule and complete the reinspection.
8. Record `Passed` only after confirming the corrective work.

Expected:

- The failed inspection blocks completion, not unrelated drafting or reporting work.
- Findings, evidence, corrective action and reinspection remain linked to the same work.
- The original failed result is not overwritten.
- Completion becomes eligible only after the corrective action and passed reinspection are recorded.

## 9. UAT-CE-05 — Delayed weekly progress and recovery action

1. Open the project’s `Site` tab and create the current progress/weekly supervision update.
2. Select the governed milestone and record progress percentage, planned/actual dates, site status, contractor activity, risks, delays and dependencies.
3. Mark the update `At risk`, `Delayed` or the equivalent configured state.
4. Record the delay reason, recovery action, responsible project member and due date.
5. Submit the update for the configured management review.
6. On the next period, record the updated progress and recovery result.

Expected:

- Progress is tied to a governed milestone and retains period-by-period history.
- Invalid percentages and unexplained regressions are rejected or routed as an explicit correction according to configuration.
- The delay, dependency, recovery owner and management action remain visible in progress reports and audit history.

## 10. UAT-CE-06 — Variation or extension of time with cost/contract impact

1. From the project’s `Site` tab, start a variation/extension-of-time record against the active works contract.
2. Record the reason, scope change, time impact and current Published evidence.
3. Set `Cost impact` and select the authoritative QS variation/valuation.
4. Have Finance review the budget impact.
5. Have Procurement review the contract impact.
6. Complete the configured approval workflow.
7. Attempt to execute the affected additional work before and after the positive decision.
8. Repeat with a time-only change and confirm cost-specific checks are not demanded when there is no cost impact.

Expected:

- Cost-impacting changes cannot be treated as authorized before applicable QS, Finance, Procurement and approval decisions are complete.
- Approved values link to QS, budget and contract records without duplicating their ledgers.
- Time-only changes do not acquire invented cost gates.
- Rejection/return retains the reason and supports controlled correction/resubmission.

## 11. UAT-CE-07 — Defects, completion certificate, as-builts, handover and asset/property history

1. Record all identified defects/snags and their corrective actions.
2. Attach correction evidence and complete the required reinspection.
3. Confirm the completion inspection has passed and all completion-blocking defects are cleared.
4. Prepare the completion record/certificate and select the current completion evidence.
5. Record retention or defects-liability information where applicable.
6. Select current as-built drawings/documents where applicable.
7. Complete the configured handover approval and record the receiving party/date.
8. Verify that the authoritative Property, Maintenance or Asset history is linked/updated exactly once.
9. Verify the applicable QS certificate/payment recommendation and Finance reference without posting a duplicate financial transaction from Civil Engineering.

Expected:

- Completion is rejected while a failed inspection or completion-blocking defect remains open.
- The certificate, handover, retention/defects-liability and as-built evidence retain current document versions.
- The property/asset history shows the completed improvement and traceable Civil case reference exactly once.
- QS and Finance remain owners of valuation, payment and ledger records.

## 12. UAT-CE-08 — Civil Engineering permit technical review

1. Open a current development/permit case that requires Civil Engineering review.
2. Confirm the case retains its applicant, property/parcel, site plan/drawings, Planning/GIS status and inspection evidence from their owning records.
3. Record the Civil Engineering technical assessment, structural/infrastructure comments, recommendation, conditions or correction request.
4. Attach/select current Published technical evidence.
5. Submit the recommendation to the configured permit approving authority.
6. Complete approval, conditional approval, rejection, deferment or correction according to the configured permit workflow.

Expected:

- Civil Engineering records a technical review and recommendation; it does not create a second permit case.
- Review, comments, evidence, recommendation and final permit outcome remain traceable.
- Only relevant technical/spatial/fee/legal checks configured for that permit type apply.

## 13. UAT-CE-09 — Reports, dashboard, export and audit reconciliation

Using the records created above, open `Reports -> Civil Engineering` and run:

1. Engineering Work Register.
2. Inspection Report.
3. Site Instruction Log.
4. Project Progress Report.
5. Defect Report.
6. Completion Certificate Report.
7. Project Dashboard.
8. Engineering Audit Trail.

For every output:

- filter by tenant, project/case and inclusive date range;
- reconcile record counts, references, statuses and dates to the source pages;
- drill down to one source record where supported;
- export one authorized format and confirm its extension and contents;
- repeat export as a user without export permission.

Expected:

- Reports reproduce governed source records rather than maintaining a separate Civil reporting ledger.
- Tenant/project/permission filters apply to online results, drill-down and exports.
- The engineering audit trail shows site instructions, inspections, progress updates, defects, certificates and handover actions with actor and timestamp.

## 14. UAT-CE-10 — Security, SOD, tenant, DMS, concurrency, retry and NFR negatives

Perform the following against representative create, update, approve, report and export actions:

1. Call without authentication — expect `401`.
2. Call as an authenticated user without the required permission/project access — expect `403` or a governed denial.
3. Read or mutate another tenant’s project, site, DMS version, contractor or case — expect `404` or governed denial with no record disclosure.
4. Where the configured SOD rule separates maker/reviewer/approver, attempt the positive decision as the maker — expect rejection and no state change.
5. Select an unpublished, superseded, quarantined, wrong-project or wrong-tenant DMS version — expect rejection without losing form data.
6. Submit the same client request/idempotency key twice with the same payload — expect one business action.
7. Reuse the same client request/idempotency key with different content — expect a safe conflict.
8. Update a record using an old row version after another user changes it — expect a safe concurrency response and a refresh/retry path.
9. Enter invalid progress, date, required-selector or defect/inspection data — expect field-specific guidance and retained inputs.
10. Force an API/business failure — confirm the UI shows the server Problem Details message/code, exposes no stack trace and uses no browser-native `alert`, `confirm` or `prompt`.
11. Confirm standard list/detail pages remain usable at supported desktop width and keyboard navigation reaches labelled actions and selectors.
12. Verify the representative list/report response time and export duration against TDC’s approved non-functional acceptance target. Record actual timings; do not invent a target absent an approved value.

Expected:

- Rejected requests make no business, document, financial, audit-success or integration mutation.
- Error messages state what failed and the practical next action without exposing internal IDs or stack traces.
- Retry, stale-row and duplicate-request handling do not create duplicate instructions, inspections, progress updates, certificates, handovers or asset-history updates.

## 15. Evidence sheet

Complete one row for every scenario. Do not mark a scenario Passed from source-code inspection alone.

| Scenario | Tenant | Project/case references | Source/linked owner references | Workflow/audit references | Tester/date | Result | Defect/reference |
| --- | --- | --- | --- | --- | --- | --- | --- |
| UAT-CE-01 |  |  |  |  |  |  |  |
| UAT-CE-02 |  |  |  |  |  |  |  |
| UAT-CE-03 |  |  |  |  |  |  |  |
| UAT-CE-04 |  |  |  |  |  |  |  |
| UAT-CE-05 |  |  |  |  |  |  |  |
| UAT-CE-06 |  |  |  |  |  |  |  |
| UAT-CE-07 |  |  |  |  |  |  |  |
| UAT-CE-08 |  |  |  |  |  |  |  |
| UAT-CE-09 |  |  |  |  |  |  |  |
| UAT-CE-10 |  |  |  |  |  |  |  |

## 16. Completion rule

Civil Engineering UAT is complete only when all ten scenarios pass against the real configured SQL Server test environment, the source and linked owner records reconcile, failures are corrected and rerun, browser/API/audit evidence is attached, and authorized TDC representatives sign off.

Implementation, automated test success, migration success or one health endpoint is not UAT acceptance on its own.

## 17. Automated pre-UAT checkpoint — 2026-08-29

Before business UAT, the release candidate passed `236` Core/shared Civil tests (`6` SQL-only tests were exercised separately), `60` Civil API tests, `58` Civil frontend tests across `29` files, focused lint over `115` changed/new Civil, workflow and report files, and `6` disposable real SQL Server integration suites with zero leftover test databases. EF discovered all `38/38` new Civil migrations and reported no pending model changes. The complete results and remaining release gates are recorded in `docs/TDC_CIVIL_ENGINEERING_ARCHITECTURE_COMPLIANCE.md`.

This checkpoint does not pre-populate any evidence-sheet result. The release candidate still requires repair/validation of the pre-existing historical empty-database bootstrap, a successful full frontend compiler gate, legitimate Published tenant configuration, deployment and execution of UAT-CE-01 through UAT-CE-10 by the named distinct users.
