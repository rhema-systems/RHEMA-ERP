# Estate End-to-End Lifecycle Manual

Last updated: 2026-08-09  
System: RHEMA ERP  
Scope: Land Acquisition to Legal, Planning, DMS, Project/Property Management, Facilities, Finance, Maintenance, and External Portal

## 1. Purpose

This manual explains the complete operational journey from land acquisition to final estate/property operations.

It is written as one end-to-end flow, not as separate module notes.

The full lifecycle is:

```text
Land Acquisition
  -> Legal / Planning / DMS checks and records
  -> Land Bank / Land Management
  -> Project Management / development handoff
  -> Property and Unit Register
  -> Property listing / lease / sale / occupancy
  -> Ground rent / rent / billing / finance handoff
  -> Move-in / handover
  -> Facilities operations
  -> Maintenance / complaints / providers / cleaners
  -> Ongoing DMS, Legal, Planning, Finance and Reports
```

## 2. Core operating rules

### 2.1 Workflows must come from Workflow Setup

Stages, required documents, checklists, approvals, and assignments must not be hard-coded into the module screens.

The screens may show registers, dashboards, handoff panels, operational guidance, and live case workspaces, but the workflow itself must be configured and published through Administration / Workflow Setup.

If no workflow has been configured, the workspace should clearly say:

```text
Workflow not configured.
Configure and publish the workflow in Administration > Workflow Setup.
```

### 2.2 Each module owns its part

No module should silently take over another module’s job.

- Estate Acquisition owns land acquisition intake and acquisition-stage processing.
- Legal owns legal reviews, opinions, legal instruments, counsel, mortgages, terminations, transfers, and court/legal processes.
- Planning owns planning reviews, scheme/layout/site-plan/site-report/search/regularization/inspection/committee processes.
- DMS owns central document storage, metadata, versions, access, retention, and document integration queue.
- Land Management owns the land bank and project-readiness handoff.
- Project Management owns project development and unit creation.
- Property Management owns property/unit register, listing, lease, occupancy, billing readiness, handover, and property records.
- Facilities owns property/site service operations, maintenance intake, complaints, providers, cleaners, service charge context, and facilities documents.
- Maintenance owns job cards, work orders, technicians, parts, inspections, and technical closure.
- Finance owns AR/AP invoices, receipts, allocations, balances, postings, journals, and finance reports.
- External Portal owns the customer-facing experience.

### 2.3 Handoffs must carry context

Every handoff should carry:

- Source module.
- Source reference.
- Property/land/unit reference.
- Customer/business partner where applicable.
- Documents/evidence.
- Decision/status.
- Required next action.
- Return reference after completion.

Example:

```text
Facilities -> Maintenance

Facilities captures property, unit, requester, evidence, SLA and service impact.
Maintenance creates and executes job card/work order.
Maintenance returns job card, work order, inspection, cost and closure references.
Facilities closes its intake after confirmation and feedback.
```

## 3. End-to-end lifecycle overview

## Stage 1: Land Acquisition Intake

### Purpose

This is where a proposed land acquisition begins.

### Users

Typical users:

- Estate Officer.
- Estate Manager.
- Acquisition Committee.
- Survey / Demarcation officers.
- Legal officers.
- Finance officers.
- Lands liaison / registry officers.
- Executive approvers.

Seeded acquisition users use password:

```text
Acquire123!
```

### What happens

The Estate team creates or receives a land acquisition record.

The acquisition workspace should capture:

- Parcel identification.
- Vendor/current owner details.
- Past ownership history.
- Location/region/district/town where available.
- Parcel size.
- Land use/purpose.
- Site/suitability information.
- Documents required by workflow.
- Notes and evidence.
- Ownership and title checks.
- Survey/demarcation information.
- Legal and finance handoff requirements.

### Important rules agreed

- Required documents must be listed from the workflow configuration.
- Do not duplicate document upload/checklist controls in approval panels if the documents are already handled by workspace stages.
- If approval checkboxes are true workflow approval checklist items, they may remain; otherwise redundant document checkboxes should be removed from workflow configuration.
- Soil type and similar parcel-identification fields belong in the correct early acquisition stage if already moved there.
- Date gap reason should only show when there is a real gap between ownership dates.
- Only one witness should be allowed to swear oath. If Witness 1 is marked as sworn, the other witness oath fields should be hidden or disabled.

### Live test result

Land Acquisition rendered successfully.

Observed live data:

- 3 acquisitions.
- Documents visible.
- Workflow stage information visible.

## Stage 2: Ownership and Vendor Validation

### Purpose

This validates who owns the land and whether the acquisition history is clean.

### Key behavior

The system should support:

- Current owner.
- Past owners.
- Ownership start/end dates.
- Date gap reason only when dates do not align.
- Vendor/payment information.
- Evidence documents.

### Acquisition ownership rule

When the land is acquired:

- TDC becomes the current owner.
- The entered current owner becomes a past owner.
- The previous owner’s end date should align with the acquisition/asset creation date.

Dates should display in readable date format, not raw ISO timestamp format.

Example bad display:

```text
2017-01-01T00:00:00 - 2023-01-01T00:00:00
```

Expected:

```text
2017-01-01 - 2023-01-01
```

## Stage 3: Suitability, Survey and Demarcation

### Purpose

This confirms whether the land is usable and whether boundaries are known.

### Key activities

- Physical assessment.
- Planning/zoning/access/utilities evidence where workflow requires.
- Cadastral survey.
- Beacon/coordinate capture.
- Boundary confirmation.
- Demarcation.

### Important rule

Verification checks such as cadastral match, overlap cleared, boundary confirmed, verification officer/date, and verification notes should happen in the correct survey/boundary stage, not be repeated unnecessarily later.

If boundary ownership has already been verified in the correct stage, later stages should reference the saved result instead of asking users to do the same work again.

### Map behavior

The Cadastral Survey Boundary panel should show the saved beacon coordinates captured during the Cadastral Survey stage.

If the map is empty, the likely issue is missing coordinate data or the map component not rendering saved boundary data.

## Stage 4: Legal Involvement During Acquisition

### Purpose

Legal supports acquisition by reviewing title, ownership evidence, instruments, risks, notices, consent, and legal documents.

### Legal handoffs from acquisition

Acquisition may require Legal for:

- Legal opinion/advisory.
- Title review.
- Ownership dispute review.
- Mortgage/encumbrance checks.
- Assignment/sublease/vesting issues.
- Transfers.
- Termination/recognition.
- Court or other legal processes.
- External counsel where needed.

### Legal workspaces available

Legal includes 11 procedure workspaces:

1. Legal Department Procedure Manual.
2. Legal Opinions / Advisory.
3. External Counsel Management.
4. Mortgages.
5. Mortgage In Principle.
6. Court Processes.
7. Other Court Processes.
8. Termination / Recognition.
9. Assignment / Sublease / Vesting.
10. Leases / Deed of Variation / Renewal / Sublease.
11. Transfers.

### Legal principles

Legal workspaces should:

- Use workflow setup for stages and approvals.
- Use DMS for legal documents.
- Keep source department references.
- Return legal outcome/status back to the requesting module.

### Live test result

Legal rendered successfully for:

- Legal dashboard.
- Legal Procedure.
- Legal Opinion / Advisory.
- External Counsel.

Earlier sweep confirmed all 11 legal route keys render with correct keys.

## Stage 5: Planning Involvement

### Purpose

Planning handles planning-related reviews and outputs that support acquisition, land bank, development, regularization, and property operations.

### Planning procedures available

Planning includes 12 procedure workspaces:

1. Vetting of Applications for Land Allocations or Temporary License.
2. Change of Use Review.
3. Preparation of Planning Scheme / Layout.
4. Site Report.
5. Preparation of Site Plan.
6. Official Search and Provision of Data.
7. Development Permit Conformity Review.
8. Undertake Regularization.
9. Layout Review and Correction.
10. Compliance Site Inspection and Reporting.
11. Dispute Resolution and Client Complaint Management.
12. District Assembly Spatial Planning Committee Meetings.

### Planning principles

Planning should:

- Use Estate/Land Bank references when parcel/allocation/ownership context is needed.
- Store plans, layouts, site reports, committee evidence, searches, and responses in DMS.
- Route recommendations and approvals back to Project/HOD/Estate where needed.
- Use central Reports for planning reports.
- Use Workflow Setup for stages/documents/checklists.

### Live test result

Planning rendered earlier with admin/planning-capable user.

In the latest live pass, the current Estate user was redirected to dashboard when opening Planning routes, which indicates role/permission limitation.

Planning must be tested with admin or planning-authorized user.

## Stage 6: DMS During Acquisition

### Purpose

DMS stores controlled documents and document metadata from acquisition and later modules.

### DMS should receive

From Acquisition:

- Parcel documents.
- Owner/vendor evidence.
- Physical assessment reports.
- Zoning/planning clearance.
- Access and utility evidence.
- Survey documents.
- Legal opinions.
- Agreements.
- Approvals.
- Payment evidence.
- Registration documents.

From Legal:

- Legal opinions.
- Draft instruments.
- Signed instruments.
- Counsel documents.
- Court documents.
- Mortgage/transfer/lease/termination documents.

From Planning:

- Site plans.
- Layouts.
- Reports.
- Search data.
- Regularization documents.
- Committee evidence.

### DMS workspaces

DMS includes:

- Document Records.
- Metadata Template Management.
- Version & Revision Control.
- Access, Retention & Audit.
- Module Integration Queue.

### Live test result

DMS rendered successfully:

- Central DMS.
- Document Records.
- Integration Queue.

## Stage 7: Acquisition Completion and Asset Creation

### Purpose

When acquisition is approved and completed, the system creates or updates the Estate asset record.

### Expected result

The acquired land becomes an Estate-managed land asset.

At this point:

- Owner should become TDC.
- Former owner becomes past owner.
- Asset receives land/property reference.
- DMS documents remain linked.
- Legal/planning outputs remain linked.
- Land becomes available in Land Management / Land Bank.

### Finance consideration

There may be a need for vendor payment during acquisition.

If vendor payment is part of the process:

- Acquisition captures vendor payment context.
- Finance AP/payment process owns the actual payment.
- Payment evidence should return to Acquisition/DMS.

## Stage 8: Land Management / Land Bank

### Purpose

Land Management is where acquired/demarcated land is held as land bank.

### Land bank shows

- Land records.
- Source from acquisition or manual entry.
- Boundary/demarcation readiness.
- Project readiness status.
- GIS link where available.
- DMS and document references.

### Handoff to Project Management

When land is ready for development:

1. Estate marks the land as ready for Project Management.
2. Project Management receives the land reference.
3. Project creates/develops units.
4. Completed units are handed back into Property Management.

### Live test result

Land Management rendered successfully.

Observed live data:

- 4 land bank records.
- Records from acquisition/project context visible.

## Stage 9: Project Management Handoff

### Purpose

Project Management takes land that is ready for development and creates project output such as plots, apartments, buildings, and units.

### Project output

Project Management should eventually return:

- Completed project.
- Unit list.
- Property/unit references.
- Project code/title.
- Handover documents.
- Completion status.
- DMS documents.

### Important property rule

Finished project units should be pushed into the Property and Unit Register.

## Stage 10: Property and Unit Register

### Purpose

The Property and Unit Register is the master property record after acquisition/project handoff.

This is where the project module manager can push finished projects/units into Estate/Property Management.

### It stores

- Property/unit identity.
- Asset code.
- Name.
- Location.
- Land/plot/apartment/building/unit type.
- Source: acquisition, project unit, manual.
- Availability status.
- Lease/sale flags.
- Occupant/tenant/customer reference.
- DMS references.
- Listing references.

### Live test result

Property and Unit Register rendered successfully after retry.

Observed live data:

- 10 register records.
- 6 land/plot records.
- 4 property/unit records.
- 0 leased/occupied at time of test.

## Stage 11: Occupancy / Availability Operations

### Purpose

This controls whether a property/unit is available, reserved, occupied, leased, sold, blocked, or under maintenance.

### Portal visibility rules

Only genuinely available units should appear externally.

Do not display as available if:

- Sold.
- Occupied.
- Leased/rented/full.
- Reserved.
- Blocked.
- Under maintenance.

### Live test result

Occupancy / Availability rendered successfully.

Observed live data:

- Available: 4.
- Reserved: 0.
- Leased/occupied: 0.
- Blocked/maintenance: 0.

## Stage 12: Property Listings

### Purpose

Property Listings publishes available sale/rent properties to the External Portal.

### Rules

- If a customer already requested/bid for a property, the action should be disabled or hidden for that customer.
- A listing that is sold/occupied/rented/full/unavailable should not show externally.
- Listing view should look good even when fewer than 4 items are displayed.
- Bidding/request should use the logged-in customer’s identity.
- If the user has multiple Business Partner accounts, the user should select which Business Partner is used for the transaction.
- Business Partner registration remains owned by the Business Partner module.

### Live test result

Portal Listings rendered successfully.

Observed live data:

- 4 published listings.

## Stage 13: External Portal Customer Flow

### Purpose

External Portal is the customer-facing entry point.

### Customer can access

- Property Listings.
- My Property Requests.
- Estate Services.
- Estate Documents.
- Business Partner Registration.
- Profile.
- Notifications.

### Expected property request flow

1. Customer logs into External Portal.
2. Customer creates/selects Business Partner account if required.
3. Customer browses property listings.
4. Customer selects property.
5. Customer submits purchase bid or rental request.
6. Request appears in Property Management Listing/Application Operations.
7. Property team reviews and approves/rejects.
8. Customer is notified.
9. Customer accepts/rejects offer.
10. If accepted, agreement process begins.

### Live test result

Earlier test with external user rendered:

- External Portal dashboard.
- Property Listings.
- My Property Requests.
- Business Partner page.
- Estate Services.
- Estate Documents.

Latest Chrome pass timed out navigating to `/external-portal/*`, so External Portal should be retested.

## Stage 14: Listing / Application Operations

### Purpose

This is the Property Management workspace where external customer requests/bids are reviewed.

### It should show

- Customer property requests.
- Customer/business partner.
- Listing/property reference.
- Request type: sale/rent.
- Submitted documents.
- Review status.
- Workflow status.
- Approval/rejection.
- Handoff to lease/sale/agreement.

### Live test result

Listing / Application Operations rendered successfully.

## Stage 15: Lease Management / Agreement Flow

### Purpose

Lease Management handles lease assignment, terms, agreement generation, signatures, and lease lifecycle.

### Rental process

1. Property is available for rent.
2. Ground rent/rent basis is known before listing.
3. Customer submits request.
4. Property team approves customer.
5. Customer is notified.
6. Customer accepts or rejects.
7. Agreement is generated from uploaded template.
8. Customer signs and uploads signed agreement.
9. Move-in/agreement start date is set.
10. Billing starts from move-in/agreement start date.

### Lease controls expected

- New lease assignment.
- Lease renewal.
- Lease termination.
- Agreement generation.
- Signed document upload.
- Move-in/move-out.
- Handover.
- DMS record update.
- Finance billing handoff.

### Live test result

Lease Management rendered successfully.

## Stage 16: Ground Rent

### Purpose

Ground rent is for land only.

Apartments/units use agreed rent amount, not ground rent.

### Ground rent setup should include

- Payment frequency.
- Due dates.
- Calculation rules.
- Rate per acre/approved assessment.
- Rent review/escalation.
- Invoice generation.
- Arrears and penalties.
- Finance posting and receipts.

### Important billing rule

Ground rent may be calculated before listing land for rent, but billing should not start until:

- Agreement is accepted/signed, and
- Move-in date or agreement start date begins.

## Stage 17: Billing / Service Charge / Finance AR

### Purpose

Property/Facilities prepare billing context. Finance AR owns invoices, receipts, allocation, balances and postings.

### Property/Facilities provide

- Source reference.
- Customer/business partner.
- Property/unit/lease reference.
- Charge type.
- Amount/currency.
- Due date.
- Supporting evidence.
- DMS link.

### Finance AR owns

- AR invoices.
- Receipts.
- Allocations.
- Customer balances.
- Statements.
- GL postings.
- Finance reports.

### Live test result

Finance AR handoffs rendered:

- `/finance/ar/invoices`.
- `/finance/ar/payments`.

## Stage 18: Move-in / Handover

### Purpose

Move-in/Handover confirms possession and starts operational responsibility.

### It should capture

- Move-in date.
- Possession date.
- Key/access handover.
- Utility handover.
- Condition/evidence.
- Billing start.
- Maintenance/facilities readiness.
- DMS documents.

### Billing rule

Billing starts from the move-in date or agreement start date, not before agreement/possession is ready.

## Stage 19: Facilities Operations

### Purpose

After a property/unit is active, Facilities handles ongoing site/property service operations.

### Facilities workspaces

Facilities includes:

- Property / Site Operating View.
- Lease / Occupancy Coordination.
- Maintenance Intake.
- Complaint Management.
- Service Provider Management.
- Staff & Cleaner Duty Operations.
- Facilities Asset Operating View.
- Facilities Billing / Service Charge Operations.
- Facilities Document Index & DMS Readiness.

### Live test result

Facilities rendered successfully:

- Facilities landing.
- Maintenance Intake.
- Staff & Cleaner Duty Operations.
- Billing / Service Charge.
- Document Index.
- Service Provider Management.

## Stage 20: Facilities Maintenance Intake

### Purpose

Facilities captures maintenance need and sends execution to Maintenance Management.

### Facilities captures

- Requester.
- Property/unit.
- Asset.
- SLA.
- Priority.
- Safety.
- Evidence.
- Service impact.

### Maintenance owns

- Job card.
- Work order.
- Technician assignment.
- Parts/materials.
- Execution.
- Inspection.
- Technical closure.

### Permission rule

Maintenance routes require:

```text
maintenance.access
```

If the current user lacks this permission, `/maintenance/*` redirects to dashboard.

### Fix implemented

The Facilities Maintenance Intake handoff now checks permissions:

- If user has `maintenance.access`, Maintenance buttons work.
- If user does not have `maintenance.access`, Maintenance buttons are disabled and a clear message explains why.

### Live test result

As Estate user `Ama Estate`:

- Facilities Maintenance Intake rendered.
- Maintenance Job Cards route redirected to dashboard.
- Maintenance Work Orders route redirected to dashboard.
- Facilities page now shows the access warning instead of silently bouncing.

## Stage 21: Staff & Cleaner Duty Operations

### Purpose

This handles cleaners/staff assignment and duty operations for apartments/properties/facilities areas.

### Expected behavior

Facilities should reference HR employees, not create duplicate employee records.

It should support:

- Cleaner/staff duty profile.
- Assignment to apartment/property/site.
- Timetable/roster.
- Tools/access readiness.
- Attendance exceptions.
- Quality checks.
- Follow-up routing.

### Live test result

Staff & Cleaner Duty Operations rendered successfully.

## Stage 22: Service Provider Management

### Purpose

Facilities manages operational use of approved providers.

### Ownership split

- Business Partner/Procurement owns supplier registration and approval.
- Legal owns contract/legal review.
- Facilities owns operational profile, service categories, coverage, SLA and performance.
- Finance AP owns supplier invoices and payments.

### Live test result

Service Provider Management rendered successfully.

## Stage 23: Facilities Billing / Service Charge

### Purpose

Facilities prepares service charge context and hands finance execution to AR.

### Handoffs

Facilities Billing links to:

- Finance AR invoices.
- Finance AR payments/receipts.
- AR customers.
- AR reports.
- Property billing.
- Central DMS.

### Live test result

Facilities Billing rendered successfully.

Finance AR invoices and payments rendered successfully.

## Stage 24: Facilities Documents and DMS

### Purpose

Facilities Document Index prepares Facilities records for Central DMS.

### Documents may include

- Maintenance evidence.
- Service charge documents.
- Provider documents.
- Cleaner duty evidence.
- Complaint documents.
- Site/property operational records.

### Live test result

Facilities Document Index rendered successfully.

DMS Integration Queue rendered successfully.

## Stage 25: Ongoing Legal / Planning / DMS / Reports

### Legal ongoing

Legal may be triggered later for:

- Lease renewals.
- Lease variations.
- Terminations.
- Transfers.
- Assignments.
- Subleases.
- Court processes.
- Legal advice.

When termination goes to a case/workspace, parcel/property details and references should be auto-filled from the source record.

### Planning ongoing

Planning may be triggered later for:

- Change of use.
- Site plan.
- Regularization.
- Layout review.
- Planning inspections.
- District assembly committee meetings.

### DMS ongoing

All major outputs should be pushed to DMS:

- Agreements.
- Signed documents.
- Legal instruments.
- Planning documents.
- Property records.
- Facilities evidence.
- Finance documents.
- Maintenance closure evidence.

### Reports ongoing

Reports from Estate, Facilities, Legal, Planning and DMS should go into the central Reports module where report fields and templates are managed.

## 26. Live test summary

### Passed

- Estate dashboard.
- Land Acquisition.
- Land Management.
- Property Management landing.
- Property and Unit Register.
- Lease Management.
- Occupancy / Availability.
- Portal Listings.
- Facilities landing.
- Facilities Maintenance Intake.
- Facilities Staff & Cleaner Duty Operations.
- Facilities Billing / Service Charge.
- Facilities Document Index.
- Facilities Service Provider Management.
- Legal dashboard.
- Legal Procedure.
- Legal Opinion / Advisory.
- Legal External Counsel.
- Central DMS.
- DMS Document Records.
- DMS Integration Queue.
- Finance AR Invoices.
- Finance AR Payments.

### Access-limited / requires correct user

- Maintenance Job Cards and Work Orders require `maintenance.access`.
- Planning requires admin/planning-capable user.

### Needs retry

- External Portal latest live Chrome pass timed out, though earlier external-user test rendered the main external estate pages.

## 27. Known follow-ups

1. Retest Planning with admin/planning user.
2. Retest Maintenance with maintenance-authorized user.
3. Retest External Portal with external user.
4. Confirm Ground Rent Administration loader under fresh session.
5. Confirm Property Billing loader under fresh session.
6. Complete agreement template generation/sign/upload test.
7. Test duplicate property request prevention.
8. Test sold/occupied/rented/full listing visibility.
9. Test cleaner timetable assignment to apartments/properties.
10. Confirm lease termination auto-fills legal case/workspace with property and parcel details.
11. Confirm all report outputs go to central Reports.
12. Confirm DMS metadata templates for Estate, Facilities, Legal, Planning and Finance handoffs.

## 28. Current verdict

The Estate lifecycle is largely functional from Acquisition through Land Bank, Property Management, Facilities, Legal, DMS and Finance AR handoffs.

The most recent handoff issue is resolved in user experience:

- Maintenance links redirect because of missing `maintenance.access`.
- The Facilities Maintenance Intake screen now explains the access requirement and disables the handoff buttons for unauthorized users.

The remaining work is role-specific testing and a few end-to-end transaction confirmations, especially Planning, Maintenance-authorized execution, External Portal customer request, agreement signing, and Ground Rent/Billing completion.

