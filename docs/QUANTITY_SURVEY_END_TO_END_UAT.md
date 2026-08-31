# TDC Quantity Survey End-to-End UAT

Last reviewed: 2026-08-29

## Purpose and authority

This script validates the Quantity Surveying requirements in the *TDC ERP Architecture and Design Document (002).docx*. It follows the architecture traceability baseline in `docs/TDC_QUANTITY_SURVEY_ARCHITECTURE_REQUIREMENTS.md`, especially:

- Section 16.4, **Detailed Quantity Surveying Design**.
- Section 16.4.1, **Quantity Surveying Integration, Controls, and Acceptance**.
- Sections 18 through 18.2, **Unified Workflow, Approval, and Control Design**.
- Sections 19.1 through 19.4, responsibility, reporting, document exchange, integration, security, and governance.
- Section 20.4, the integrated Land, Property, Maintenance, Quantity Surveying, Civil Engineering, and Permit workflow.
- Sections 21 and 22.2 through 22.4, traceability, controls, data, integration, security, reporting, testing, and operational readiness.
- Applicable enterprise requirements in Sections 9 through 13.

The architecture document does not assign `FR-QS-*` identifiers. Test case references in this script are UAT execution references only and are not represented as source requirement IDs.

## Configuration boundary

Section 18.2 states that exact approval thresholds, named roles, authority limits, and workflow routes must be confirmed during detailed configuration. Before execution, the tenant owner must approve and record:

- The users assigned to preparation, QS review, Engineering or Project confirmation, Finance validation, independent approval, AP processing, payment approval, Inventory or Stores, Maintenance, Procurement, and Internal Audit responsibilities.
- The monetary thresholds and authority limits that determine the configured approval route.
- The number and order of workflow stages, escalation rules, delegation rules, exception paths, rework paths, and evidence requirements.
- Approved rate sources and formulas, contingency rules, retention rules, tax treatment, report formats, and document templates.

The values selected for UAT must be recorded in the execution evidence. They are tenant configuration, not hard-coded architecture mandates. Business users must select friendly controlled records and must never enter internal database IDs, workflow-definition IDs, document-record IDs, hashes, fingerprints, or checksums.

## Test principles

1. Use fresh records created for this UAT. Existing records may be used only for read-only comparison.
2. Use a unique run reference such as `QS-UAT-YYYYMMDD-NNN` in every new record description or external reference.
3. Use separate authorized users for preparation, review, confirmation, Finance validation, approval, payment, receipt, and audit activities.
4. Do not give one administrator every business role. Administration configures access but does not perform business approval.
5. Execute against the current application and a current real SQL Server schema with migrations, constraints, triggers, and foreign keys enabled.
6. Retain screenshots, record references, exported reports, generated documents, API correlation IDs, workflow history, audit history, and SQL reconciliation results.
7. Retry tests must reuse the same business request or idempotency key where supported. A retry must not create a second governed outcome.
8. Negative tests must prove that rejected requests do not mutate business, financial, document, workflow, or audit state beyond the authorized security or exception event.

## Automated harness boundary and actor matrix

The supported non-production automation is deliberately narrower than this complete UAT script. Run `rebuild-db` against a uniquely named disposable SQL Server database, run `seed-hr-all` for the controlled location master, then run `scripts/quantity-survey/Invoke-QuantitySurveyE2ESeed.ps1` with `ConnectionStrings__DefaultConnection` scoped to that same database. The launcher refuses system and non-local databases. Do not point it at production or treat a persistent development fixture as a fresh UAT case.

The guarded launcher performs that setup, starts the API and frontend, runs the authenticated Chromium lifecycle and SQL reconciliation, and drops only the database it created:

```powershell
& .\scripts\quantity-survey\Invoke-QuantitySurveyBrowserAcceptance.ps1
```

It refuses an existing database instead of rebuilding it. Use `-DatabaseName RhemaQsUatAssurance_YYYYMMDD` only for a verified-absent disposable target when another guarded run already owns today's default name.

Use `e2e-tests/.qs-assurance-runner.mjs` for the governed automated run. After Chromium succeeds, it runs `scripts/quantity-survey/Invoke-QuantitySurveyE2EVerification.ps1`, which fails unless SQL Server proves the unique recorded measurement, approved valuation and certificate, four distinct valuation and certificate workflow performers, one budget-utilization ledger entry, matching formal commitment utilization, central DMS lineage, and one Finance AP handoff. The runner also supports an identical replay against the same disposable records to prove idempotency.

`e2e-tests/tests/qs-phases-0-6-lifecycle.spec.ts` must use separate authenticated actors for every positive approval stage:

| Automated responsibility | Acceptance environment | Separation proved by the lifecycle spec |
| --- | --- | --- |
| QS maker | `QS_ACCEPTANCE_MAKER_*` | Creates the measurement, submits the governed records, and is denied direct self-approval. |
| QS reviewer | `QS_ACCEPTANCE_REVIEWER_*` | Records the independent QS assessment, vets it, and completes only the QS-review workflow stage. |
| Engineer or Project confirmer | `QS_ACCEPTANCE_ENGINEER_*` | Completes only the engineering/project-confirmation workflow stage. |
| Finance validator | `QS_ACCEPTANCE_FINANCE_VALIDATOR_*` | Completes only the budget, commitment, tax, retention, and AP-readiness workflow stage. |
| Independent approving authority | `QS_ACCEPTANCE_CHECKER_*` | Completes the final valuation and certificate approval stages and must differ from the maker, reviewer, Engineer, and Finance validator. |
| Contractor representative | `QS_ACCEPTANCE_CONTRACTOR_*` | Creates and submits the external valuation claim for the assigned project and partner. |
| Consultant representative | `QS_ACCEPTANCE_CONSULTANT_*` | Independently endorses the QS-vetted claim for the assigned project and partner. |

`e2e-tests/tests/qs-final-finance-security-acceptance.spec.ts` then uses separate AP and Finance approval identities for posting, payment, reversal, and the direct-API denial matrix. Its non-QS Finance actor is unauthorized for QS operations even though that actor is authorized for the separate Finance-owned AP responsibility.

On a new disposable database, the fixture's project, procurement source, Works contract, and approved BOQ are fresh prerequisite records, and the lifecycle spec creates the measurement, worksheet, certificate, AP linkage, and follow-on Finance records through authenticated APIs. This is not evidence that the automated spec created and approved the estimate, rate build-up, tender, contract, or BOQ through their complete business screens. It also does not close the positive variation, Maintenance, Inventory, retention/final-account, report/export, backup/restore, or owner sign-off cases below. Record those cases separately and leave their checklist rows open until their own fresh browser, API, document, audit, and SQL evidence exists.

## Required UAT users

Map each responsibility to a different active tenant user unless the approved tenant workflow explicitly allows a compatible combination.

| UAT responsibility | Required access | Must be different from |
| --- | --- | --- |
| Estimate or BOQ maker | Create and edit QS estimates, cost plans, rate build-ups, and BOQs | Reviewer and final approver |
| QS reviewer | Review measurements, rates, valuations, variations, and certificates | Maker and final approver where configured |
| Engineer or Project confirmer | Confirm site progress, technical evidence, defects, and completion | Maker and final approver where configured |
| Procurement reviewer | Validate tender, award, supplier or contractor, purchase order, and Works contract implications | Variation initiator and final approver where configured |
| Finance validator | Validate budget, commitment, contract balance, tax, accounting, and payment readiness | Maker and payment approver where configured |
| Independent approving authority | Make the configured positive or negative approval decision | Every prohibited maker, reviewer, or validator in the configured SOD route |
| AP processor | Create or validate the Finance-owned payable from an approved certificate | Certificate maker and payment approver where configured |
| Payment approver | Approve or release payment | AP processor and certificate maker where configured |
| Engineer variation initiator | Initiate the Civil Engineering variation and provide technical evidence | Final approving authority |
| Maintenance user | Create or manage the linked maintenance requirement or work order | QS or Finance approver where configured |
| Stores or Inventory user | Record controlled material receipt, issue, return, or reconciliation | QS certificate maker and Inventory adjustment approver where configured |
| Internal Audit user | Read audit evidence, vouch documents, and review exceptions | Transaction maker and business approver |
| Unauthorized user | No QS business permission | Not applicable |
| Alternate-project or contract user | Tenant user without the tested project or contract assignment | Not applicable |
| Alternate-tenant user | Active user in another tenant | Not applicable |

## UAT data pack

Create or identify the following fresh, tenant-scoped records before the main lifecycle begins.

| Data | Required condition | Evidence |
| --- | --- | --- |
| Project | Approved and active; linked to the responsible department, property where applicable, cost centre, funding source, currency, dates, and project team | Project reference and approval history |
| Budget | Approved, current, sufficient for the representative Works scope, and available to Finance validation | Budget reference, currency, allocated, committed, utilized, and available balances |
| Rate sources | Current approved labour, material, plant, overhead, and other configured rate sources | Source references and effective dates |
| Works procurement source | Fresh approved tender or other controlled procurement source with approved contractor or supplier | Tender, evaluation, award, and supplier or contractor references |
| Works contract | Active approved contract linked to the project and procurement award | Contract reference, original sum, currency, retention terms, and approval history |
| DMS document set | Clean test PDF or image evidence for estimate assumptions, drawings, measurement, variation, and certificate support | Document name, version, malware status, owner record, and access test |
| Maintenance source | Fresh maintenance work order or approved maintenance requirement requiring QS cost certification | Maintenance reference and status |
| Inventory material | Controlled inventory item, warehouse, location, and material movement relevant to the Works contract | Item, warehouse, location, quantity, value, and movement references |

## Part A: readiness and configuration

### QS-UAT-01 — Confirm access, workflow, and parameter readiness

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Sign in as the tenant configuration administrator and review active QS roles, project or contract assignments, workflows, authority limits, evidence rules, retention rules, rate formulas, tax rules, and report access. | Every configured value is active, effective, tenant-scoped, and presented by friendly name and version. | Configuration export or screenshots with secrets excluded. |
| 2 | Confirm the configured estimate or BOQ, valuation or certificate, variation, final-account, and retention routes. | Each governed family defines initiator, validation, reviewer, approver, exception or rework path, downstream update point, document evidence, notifications, reporting, and audit events. | Workflow version and stage summary. |
| 3 | Confirm separate test users are assigned to each required stage. | No missing assignment or incompatible maker-checker assignment remains. | User-to-role and project or contract assignment evidence. |
| 4 | Confirm the current tenant currency, fiscal period, approved budget, cost codes, rate sources, property or project references, funding source, contract, supplier or contractor, warehouse, and Inventory locations are selectable. | Controlled selectors load only authorized, current, tenant-safe data. | Selector screenshots and selected record references. |
| 5 | Attempt to load the same selectors as an alternate-tenant user. | No tested tenant record is returned or disclosed. | Denial response and central audit event. |

## Part B: estimate, cost plan, rate build-up, and BOQ

### QS-UAT-02 — Create and approve a rate build-up

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Sign in as the Estimate or BOQ maker and create a rate build-up using approved labour, material, plant, overhead, and other configured components. | Components resolve from controlled sources and the server calculates the total rate. | Rate build-up reference, inputs, formula version, and calculated total. |
| 2 | Add assumptions, effective dates, classification, unit of measure, and clean DMS evidence. | Mandatory data and document controls pass; evidence remains linked and versioned. | Document metadata and build-up details. |
| 3 | Submit the build-up for its configured review and approval. | The record enters the configured workflow and becomes read-only except through controlled rework. | Workflow instance and submitted timestamp. |
| 4 | Attempt approval with the maker account. | Self-approval is rejected and audited without changing approval state. | Error code or message and audit event. |
| 5 | Review and approve with the configured independent user or users. | The build-up becomes approved and effective only after every configured positive stage completes. | Approval history, effective status, and approver timestamps. |
| 6 | Retry the approval request. | No duplicate workflow outcome, version, or audit mutation is created. | Before-and-after counts. |

### QS-UAT-03 — Prepare and approve an estimate or cost plan

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Create a fresh estimate or cost plan against the approved project using controlled project, property where applicable, funding source, budget, currency, department, cost centre, and rate references. | Controlled references populate without raw IDs; project-owned data is derived where appropriate and frozen for audit. | Estimate reference and source snapshots. |
| 2 | Add representative Works lines, quantities, units, approved rate build-ups, assumptions, contingency, overhead, tax treatment, and notes. | Totals are calculated server-side and reconcile to lines, markups, contingency, and tax. | Line and summary calculation evidence. |
| 3 | Attach the estimate basis, drawing, and cost assumptions through DMS. | Clean current documents are linked to the estimate; unauthorized users cannot download them. | DMS version, malware status, and access results. |
| 4 | Save a draft, then create a new controlled version or revision. | The earlier version remains unchanged and traceable; the new version records its source and reason. | Version history and change comparison. |
| 5 | Submit for technical and budget validation. | Mandatory fields, approved rates, budget, currency, dates, evidence, and workflow readiness validate. | Validation result and workflow instance. |
| 6 | Perform the configured technical review and Finance or budget validation with separate users. | Both outcomes, comments, timestamps, and evidence are retained. | Workflow history. |
| 7 | Approve with the independent authority. | The approved version is immutable and available to downstream BOQ or contract activities. | Approval status and history. |
| 8 | Attempt to edit the approved version directly. | Direct mutation is rejected; only a governed revision or amendment can proceed. | Denial and audit evidence. |

### QS-UAT-04 — Create, version, approve, and link a BOQ

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Create a BOQ from the approved estimate or as a controlled new BOQ for the same project. | Header, project, estimate lineage, currency, classification, and version are retained. | BOQ reference and lineage. |
| 2 | Add sections and representative line descriptions, units, quantities, rates, and amounts. | Every amount reconciles to quantity and rate; BOQ total reconciles to sections and lines. | BOQ calculation export. |
| 3 | Where spreadsheet import is authorized, import a valid template and then test an invalid or altered template. | Valid rows are staged and validated; invalid structure, totals, duplicates, or unauthorized content are rejected with row-level guidance. | Import history and error report. |
| 4 | Attach the approved drawings, BOQ basis, and tender documents through DMS. | Documents remain versioned, access-controlled, and linked to the BOQ. | DMS evidence. |
| 5 | Submit and complete the configured BOQ review and independent approval. | Maker-checker and evidence controls pass; approved version becomes immutable. | Workflow and approval history. |
| 6 | Publish or release the approved BOQ to the approved Works procurement route. | Procurement receives the approved, current BOQ version and immutable lineage. | Procurement source and BOQ version references. |
| 7 | Complete the fresh tender or approved procurement source and create the approved Works contract. | Tender, evaluation, award, contractor, contract sum, currency, and BOQ version remain linked. | Tender, award, and contract evidence. |
| 8 | Amend the BOQ after approval using a governed revision. | A new version is created; the prior approved and tendered version is not overwritten. Downstream use requires the configured approval or amendment route. | Version comparison, reason, and workflow history. |

## Part C: measurement, valuation, certification, payment, and retention

### QS-UAT-05 — Record controlled site measurement

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Record a fresh site measurement against the active Works contract and approved BOQ. | Only authorized contract and current BOQ lines are selectable. | Measurement reference and source lineage. |
| 2 | Enter work-done quantities by line and attach site sheets, photographs, drawings, or other configured evidence. | Quantities validate against applicable rules and evidence remains linked through DMS. | Measurement lines and document evidence. |
| 3 | Submit for joint or technical confirmation where configured. | The configured Engineer or Project confirmer receives the task and the maker cannot confirm their own measurement where prohibited. | Workflow task and SOD denial. |
| 4 | Confirm with the authorized Engineer or Project user. | Confirmed quantities, comments, user, timestamp, and evidence become traceable inputs to valuation. | Confirmation history. |
| 5 | Create a correction through the controlled rework route. | The prior confirmed measurement remains retained; the correction identifies its reason and supersession lineage. | Measurement version or correction history. |

### QS-UAT-06 — Prepare valuation and payment certificate

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Create a valuation worksheet from confirmed measurements and the approved contract BOQ. | Contract, BOQ, measurement, contractor, currency, and prior-certificate sources populate automatically. | Valuation reference and source lineage. |
| 2 | Review work-done value, approved variation value, eligible materials, previous certified amounts, deductions, retention, taxes, and net amount. | The server calculates each component and the valuation reconciles to source records. | Calculation breakdown. |
| 3 | Attempt to add an unapproved variation, duplicate prior payment, unsupported material value, or amount beyond the revised contract or available budget. | Invalid content is rejected with corrective guidance and no partial financial record. | Validation response and unchanged balances. |
| 4 | Generate the draft payment certificate. | A unique server-controlled certificate reference is assigned; duplicate generation for the same governed basis is prevented. | Certificate reference and duplicate test. |
| 5 | Attach measurement summary, valuation worksheet, contractor submission, and configured supporting evidence. | Required clean DMS documents are present and linked to the certificate version. | Document checklist. |

### QS-UAT-07 — Complete QS, technical, Finance, and independent approval

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Submit the certificate from the maker account. | The configured workflow starts and the submitted version is frozen. | Workflow instance and submitted timestamp. |
| 2 | Review with the QS reviewer and record findings. | Measurement, rate, variation, deduction, retention, and previous-payment checks are recorded. | QS review outcome. |
| 3 | Confirm progress with the Engineer or Project confirmer. | Technical progress, defects, completion, and site evidence are confirmed or returned for rework. | Technical confirmation outcome. |
| 4 | Validate with Finance. | Budget, commitment, revised contract balance, currency, tax, account treatment, and payment readiness pass or return clear rework guidance. | Finance validation outcome and balances. |
| 5 | Attempt final approval using the maker, QS reviewer, Engineer or Project confirmer, or Finance validator where the configured SOD route prohibits that actor. | Incompatible positive approval is rejected and audited. | SOD response and audit event. |
| 6 | Approve with the assigned independent authority. | Certificate becomes approved only after all configured stages are complete. | Approval history and final status. |
| 7 | Retry approval concurrently from two sessions. | At most one approval transition succeeds; the other receives a controlled stale-state or already-completed outcome. No duplicate downstream record is created. | Two correlation IDs, final row version, and counts. |

### QS-UAT-08 — Create AP linkage, post, pay, reverse, and reconcile

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | From the approved certificate, invoke the configured Finance or AP handoff. | Exactly one Finance-owned payable or AP obligation is created or linked using the approved certificate snapshot. | Certificate-to-AP reference. |
| 2 | Retry the handoff. | No duplicate payable, invoice, voucher, journal, or audit outcome is created. | Before-and-after counts. |
| 3 | Sign in as the AP processor and validate contractor, currency, approved amount, deductions, tax, retention, due date, and coding. | AP values reconcile to the approved certificate and controlled Finance sources. | AP validation evidence. |
| 4 | Complete posting and payment using the configured separate Finance users. | Balanced Finance entries and payment references are created; certificate and contract balances update through Finance-owned services. | Voucher, journal, payment, debit, and credit evidence. |
| 5 | Attempt to process the same approved certificate a second time. | Duplicate settlement is rejected. | Duplicate-control response. |
| 6 | Reverse or cancel the test payment through the approved Finance reversal workflow. | Compensating entries are balanced; payable, certificate payment status, contract balance, and audit lineage reconcile without deleting history. | Reversal reference and before-and-after balances. |
| 7 | Reprocess the approved payment after the permitted correction route. | Reprocessing follows the configured controls and produces one current settlement outcome. | Corrected payment and lineage. |

### QS-UAT-09 — Track retention and close the final account

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Review retention held across approved certificates. | Retention balance equals governed certificate deductions and contract rules. | Retention reconciliation. |
| 2 | Attempt release before the configured completion or defects conditions are met. | Early release is rejected with clear guidance. | Denial response. |
| 3 | Record the configured completion, defects, or eligibility evidence through its owning Project, Engineering, Contract, or Maintenance process. | Eligible retention amount and date are derived from approved owner records. | Completion and eligibility references. |
| 4 | Submit, review, and approve the retention release using separate configured users. | Approved release updates Finance/AP and outstanding retention once. | Workflow, AP, and retention balance evidence. |
| 5 | Prepare the final account using original contract value, approved variations, revised contract sum, certificates, payments, deductions, retention, claims, and outstanding balances. | Final-account totals reconcile to every authoritative source. | Final-account reconciliation. |
| 6 | Complete the configured final-account review and approval. | Contract cost closure is retained with documents, approvals, exceptions, and audit history. | Final status and approval history. |

## Part D: exact Civil Engineering variation route

### QS-UAT-10 — Engineer to QS to Procurement to Finance to approval

Execute this route in the stated functional order. The named users and monetary authority level remain tenant configuration under Section 18.2.

| Step | Responsible function | Action | Expected result | Evidence |
| --- | --- | --- | --- | --- |
| 1 | Engineer | Initiate a fresh variation against the active project and Works contract. Enter technical reason, scope, time impact, affected BOQ lines, instruction reference, and site evidence. | The variation starts in Draft or Submitted status with immutable Engineer and contract lineage. | Variation reference, initiator, contract, BOQ, reason, time impact, and DMS evidence. |
| 2 | QS | Measure and value the variation using approved quantities, rates, build-ups, assumptions, and evidence. | Cost impact is calculated and the QS valuation is retained separately from the Engineer request. | QS valuation, lines, rates, source versions, and reviewer timestamp. |
| 3 | Procurement | Validate contract, tender, award, supplier or contractor, amendment, procurement-method, and commercial implications. | Procurement outcome records whether a contract amendment or other controlled procurement action is required. | Procurement review, contract implication, and source references. |
| 4 | Finance | Validate available budget, commitment, revised contract exposure, currency, tax, and financial treatment. | Finance outcome identifies sufficient funding or the approved budget adjustment or exception route. | Budget and commitment before-and-after evidence. |
| 5 | Approving authority | Review the complete Engineer, QS, Procurement, Finance, workflow, and DMS evidence and approve or reject. | A separate authorized approver decides according to the configured authority limit. | Approval decision, authority basis, timestamp, and comments. |
| 6 | System | Apply an approved variation through controlled owner services. | Revised contract sum, approved variation register, budget or commitment exposure, BOQ or cost forecast, and audit history update once. | Cross-module references and reconciled totals. |
| 7 | System | Retain a rejected variation without applying financial or contract changes. | Rejection reason, evidence, and history remain visible; contract, budget, and certificate sources remain unchanged. | Rejected record and unchanged balances. |
| 8 | Security test | Attempt to skip QS, Procurement, Finance, or approving-authority stages, or directly change status through the API. | Stage skipping and direct status mutation are rejected and centrally audited. | API denial and audit event. |

## Part E: Maintenance and Inventory integrations

### QS-UAT-11 — Use Quantity Surveying for Maintenance cost certification

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Create or open a fresh approved Maintenance work order requiring an estimate, BOQ, valuation, or cost certificate. | Maintenance remains the work-order owner; QS receives the authorized project or work reference. | Work-order and QS linkage. |
| 2 | Prepare the required QS cost record using controlled rates, quantities, budget, and evidence. | QS calculates and retains cost certification without duplicating the Maintenance work order. | Estimate or certificate reference and source lineage. |
| 3 | Complete technical, QS, Finance, and approval stages as configured. | Separate authorized users complete the workflow and all outcomes remain traceable. | Workflow history. |
| 4 | Update the Maintenance work order through the approved integration outcome. | Maintenance cost status updates once; retries do not duplicate cost, Finance, document, or audit records. | Work-order before-and-after state and retry result. |

### QS-UAT-12 — Reconcile Inventory and site materials

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Record a controlled receipt, issue, transfer, or return of a representative material through Inventory or Stores against the project or Works contract. | Inventory owns quantity, warehouse, location, valuation, and stock movement. | Inventory movement reference and value. |
| 2 | Link the eligible material record to measurement or valuation where the configured contract permits on-site or off-site material certification. | QS reads the current approved Inventory value and retains immutable lineage; it does not create a second stock ledger. | Valuation material line and Inventory source. |
| 3 | Apply any governed certificate deduction, recovery, or material reconciliation. | Certified material, issued material, consumed material, returned material, and deduction values reconcile. | Quantity and value reconciliation. |
| 4 | Reverse or correct the Inventory movement through Inventory's controlled workflow. | QS valuation or subsequent certificate recognizes the approved correction; prior certificate history is not overwritten. | Inventory reversal and QS adjustment lineage. |
| 5 | Retry the linkage or reconciliation. | No duplicate material value, deduction, journal, or audit outcome is created. | Before-and-after counts. |

## Part F: documents, reports, audit, security, and resilience

### QS-UAT-13 — DMS governance

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Upload required estimate, drawing, BOQ, measurement, variation, certificate, approval, and final-account evidence through each governed transaction. | Files are stored by the central DMS with owner record, document type, version, malware status, access, retention, and audit metadata. | DMS register export. |
| 2 | Upload a prohibited, infected, empty, oversized, or unsupported test file according to the approved security test procedure. | The file is rejected or quarantined and cannot satisfy a business evidence requirement. | Controlled rejection evidence. |
| 3 | Supersede a draft document and inspect an approved document. | Draft correction preserves version history; approved evidence cannot be silently replaced or deleted. | Version and supersession history. |
| 4 | Attempt download as an unauthorized, wrong-project, wrong-contract, and alternate-tenant user. | Access is denied without disclosing protected content or storage paths. | Denials and audit events. |

### QS-UAT-14 — Reports, exports, and record history

Run the configured Quantity Survey report catalogue for the fresh UAT records, including at minimum:

- BOQ report.
- Valuation or certificate report and certificate approval history.
- Variation register.
- Retention report.
- Cost-to-complete report.
- Contract balance or cost-monitoring report.
- Quantity Survey audit trail.

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Run each report using period, department, project, contract, supplier or contractor, cost centre, approval status, and other authorized filters. | Results contain only matching, authorized, tenant-scoped records. | Report parameters and result counts. |
| 2 | Reconcile report totals to BOQ, valuation, variation, certificate, contract, budget, commitment, actual, retention, payment, and final-account sources. | No unexplained difference remains. | Signed reconciliation sheet. |
| 3 | Drill from a summary to its governed source. | Drill-down opens the authorized current record and preserves report context. | Screenshots or trace. |
| 4 | Export Excel and PDF. | File type, filename, parameters, totals, dates, currency, headers, and page layout are correct. | Exported files. |
| 5 | Inspect transaction and central audit histories. | Creation, change, submission, review, approval, rejection, amendment, posting, payment, reversal, access, configuration, export, and exception events identify actor, timestamp, reference, before and after state, and reason where applicable. | Audit export. |
| 6 | Run the same report as an alternate-project, alternate-contract, and alternate-tenant user. | Protected records and totals are not disclosed. | Security results. |

### QS-UAT-15 — Authorization, tenant isolation, and segregation of duties

| Test | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| Anonymous request | Call representative read and mutation APIs without authentication. | `401` or governed authentication denial; no business mutation. | Response and record counts. |
| Missing permission | Call the same actions as an authenticated unauthorized user. | `403` or governed authorization denial; no business mutation. | Response and audit event. |
| Wrong project or contract | Access a record without its project or contract assignment. | Governed denial or tenant-safe not-found response. | Response and access audit. |
| Cross-tenant access | Read or mutate the UAT records as the alternate-tenant user. | Governed denial or tenant-safe not-found response; no data leakage. | Response and tenant-filter evidence. |
| Maker self-approval | Maker attempts positive approval for build-up, estimate, BOQ, measurement, certificate, variation, retention, or final account. | Rejected wherever the configured route prohibits the actor. | SOD response and audit event. |
| Stage conflict | QS reviewer, Engineer or Project confirmer, Finance validator, AP processor, payment approver, or Internal Audit user attempts an incompatible stage. | Rejected according to the approved SOD matrix. | SOD result. |
| Direct status update | Bypass workflow and update an approved or posted status through API payload or stale page. | Rejected; immutable state remains unchanged. | API response and row-state proof. |
| Administration misuse | System administrator attempts business approval without the configured business authority. | Rejected unless the administrator separately holds a valid, approved business assignment that does not violate SOD. | Authorization result. |

### QS-UAT-16 — Idempotency, concurrency, failure recovery, and reconciliation

| Test | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| Duplicate submission | Replay estimate, BOQ, valuation, certificate, variation, AP, payment, retention, and final-account submissions. | One current workflow or transaction outcome exists for each governed request. | Counts and references. |
| Concurrent edit | Save two updates from the same starting row version. | One valid update succeeds; the stale update receives a controlled concurrency response without overwriting current data. | Correlation IDs and row versions. |
| Concurrent approval | Approve the same task from two authorized sessions. | One state transition succeeds; the second is already-completed or stale. | Workflow and audit counts. |
| Downstream failure | Use an approved non-production fault test to fail DMS, Workflow, Procurement, Inventory, Maintenance, Contract, Finance, AP, or Reporting handoff. | The transaction rolls back or enters a clear recoverable exception state; no partial posting or duplicate record remains. | Exception, transaction, and owner-system evidence. |
| Retry after recovery | Restore the dependency and retry the same governed request. | Processing completes once and retains original failure plus successful retry history. | Retry lineage. |
| Reversal | Reverse representative Finance, AP, Inventory, and integration outcomes through owner workflows. | Compensating results balance and QS sources reconcile without deleting original history. | Reversal and reconciliation. |
| Database integrity | Run approved SQL checks against the isolated UAT database. | Current migrations are applied; foreign keys and constraints are enabled and trusted; no orphan, duplicate, cross-tenant, unbalanced, or invalid lifecycle records exist. | SQL output and database identifier. |
| Restart recovery | Restart the non-production application during an approved test window and resume an in-progress QS task. | Committed work remains; incomplete work is safely recoverable; no duplicate outcome is created. | Service and transaction evidence. |

## Final architecture acceptance checklist

Record `Pass`, `Fail`, or `Not Executed` and link evidence for every item.

| Acceptance item | Result | Evidence or defect reference |
| --- | --- | --- |
| Approved rate build-up uses controlled effective sources and completes independent approval. |  |  |
| Fresh estimate or cost plan retains approved project, property where applicable, funding, budget, currency, assumptions, contingency, evidence, versions, and approval history. |  |  |
| Fresh BOQ retains sections, lines, amounts, versions, DMS evidence, approval, Procurement tender or award, and approved Works contract lineage. |  |  |
| Fresh site measurement retains confirmed quantities, technical evidence, and controlled correction history. |  |  |
| Valuation and certificate reconcile work done, prior certificates or payments, approved variations, eligible materials, deductions, tax, retention, and net amount. |  |  |
| QS review, Engineering or Project confirmation, Finance validation, and independent approval complete using separate authorized users. |  |  |
| Approved certificate creates or links one Finance-owned AP obligation and prevents duplicate settlement. |  |  |
| Posting, payment, reversal, reprocessing, contract balance, budget, commitment, and ledger outcomes reconcile. |  |  |
| Retention held, eligibility, release, outstanding balance, and final account reconcile. |  |  |
| Civil Engineering variation completes Engineer initiation, QS valuation, Procurement validation, Finance validation, and independent approval in that functional order. |  |  |
| Maintenance cost certification uses the Maintenance owner and updates its source once. |  |  |
| Inventory material quantity, value, certification, deduction, return, and reversal reconcile without a second stock ledger. |  |  |
| DMS evidence is clean, versioned, authorized, retained, and auditable across every tested family. |  |  |
| Required reports, filters, drill-down, Excel/PDF exports, totals, approval history, and audit trail pass. |  |  |
| Anonymous, unauthorized, wrong-project, wrong-contract, maker-conflict, and cross-tenant attempts are rejected without prohibited mutation or disclosure. |  |  |
| Duplicate, retry, stale version, concurrent approval, downstream failure, reversal, and restart tests produce no duplicate or partial outcome. |  |  |
| Real SQL Server migration, constraint, trigger, referential-integrity, tenant, financial-balance, and reconciliation checks pass in the isolated UAT database. |  |  |
| Tenant owners approve the configured roles, routes, thresholds, formulas, retention rules, tax rules, report formats, migration reconciliation, training, support, backup, restore, and operational readiness. |  |  |

## Sign-off

Quantity Surveying may be accepted against the TDC architecture only when every required checklist item passes or has an explicitly approved, documented exception with owner, risk, compensating control, and closure date.

| Signatory | Name | Decision | Date | Comments |
| --- | --- | --- | --- | --- |
| Quantity Surveying owner |  |  |  |  |
| Civil Engineering or Project owner |  |  |  |  |
| Procurement owner |  |  |  |  |
| Finance and AP owner |  |  |  |  |
| Maintenance owner |  |  |  |  |
| Inventory or Stores owner |  |  |  |  |
| Internal Audit |  |  |  |  |
| Information Security or System Administration |  |  |  |  |
| UAT manager |  |  |  |  |
