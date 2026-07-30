# TDC Fleet Management Gap Implementation Tracker

Last updated: 2026-07-17

## Purpose

This tracker is the delivery ledger for closing the gaps between the current ERP implementation and the requirements in `Fleet Management ERP Requirements Questionnaire.docx`, completed for TDC's Administration/Transport function.

The source document defines the expected Fleet Management operating model: transport governance, vehicle master data, vehicle acquisition and commissioning, asset movement, driver assignment, trip requests and dispatch, journey logs, fuel, maintenance, workshops, tyres, batteries, statutory compliance, safety, incidents, theft, breakdowns, insurance claims, dashboards, approvals, audit trail, mobile logbooks, and analytics.

## Source Of Truth And Boundaries

- Business source: `C:\Users\micha\Downloads\Questionnairs and SOPS\Questionnairs and SOPS\Administration\Fleet Management ERP Requirements Questionnaire.docx`
- Source render evidence: 12 pages rendered to `artifacts\tdc-fleet-doc-render` during analysis.
- Current implementation evidence: Maintenance/Fleet entities, DTOs, services, controllers, frontend routes, offline mobile inspection tracker, workflow integration, asset movement/import surfaces, maintenance reports, and fleet services.
- This tracker covers Fleet Management as a business operating model. It references Maintenance, Fixed Assets, HR, Procurement, Stores/Inventory, Finance, Workflow, Notifications, and Reporting only where Fleet requires those modules to complete an end-to-end process.
- This tracker does not certify TDC policy interpretation. TDC Administration, Transport, Finance, Internal Audit, Procurement, Stores, HR, MERC, and Management must approve final configuration values and acceptance scenarios.

## Delivery Rule

A slice may be marked `Done` only when all applicable parts are complete:

1. Backend domain model and business rules.
2. Database migration and data backfill.
3. API endpoints and authorization.
4. Frontend routes, controls, validation, and status visibility.
5. Workflow, notifications, audit events, evidence, and reports where required.
6. Automated tests for happy paths, hard stops, overrides, tenant isolation, and direct API attempts.
7. Migration applied to the test database.
8. Browser/API smoke verification.
9. Tracker evidence updated with commands, routes, and test results.

No requirement is complete merely because a page or field exists. TDC requires enforced behavior from request through approval, execution, reporting, and audit.

## Status Legend

| Status | Meaning |
| --- | --- |
| Verified baseline | Current implementation has material capability confirmed in source; final TDC acceptance is still required. |
| Partial | Useful capability exists, but required controls, fields, integration, reports, or workflow steps are incomplete or bypassable. |
| Gap | Required capability or enforcement is absent. |
| Configuration required | TDC must approve configurable values, roles, thresholds, routes, or templates before the slice can be accepted. |
| Not started | Approved implementation task has not started. |
| In progress | Implementation is actively being changed and is not yet acceptance-ready. |
| Done | Full delivery rule above has passed and evidence is recorded. |
| Blocked | Work cannot safely continue until a named dependency is resolved. |

## Honest Readiness Statement

The ERP already contains a broad fleet/maintenance foundation: fleet vehicles are represented as maintenance assets, trips are workflow-enabled, compliance can block dispatch, fuel transactions create cost entries, mobile QR inspections work offline, failed inspections create defects and Work Orders, driver licence validity is enforced, incidents and insurance claim fields exist, external repairs exist, and fleet dashboards/reports exist.

The main gap is not the absence of Fleet screens. The main gap is that TDC's full transport operating model is not yet enforced as one controlled lifecycle. The implementation must tighten acquisition, commissioning, Stores handover, Transport budget control, request approval, daily dispatch planning, complete driver eligibility, fuel authorization, external repair quotations, Internal Audit validation before payment, statutory-renewal and insurance payment reconciliation, incident/theft/breakdown evidence, insurance claims, GPS integration, and management reporting.

Most "decision" items below should become configuration rather than hard-coded code blockers. They are listed because TDC must approve the values before the system can be acceptance-tested.

## Source Fleet Baseline From Questionnaire

The questionnaire gives an initial fleet population of 52 fleet/equipment records. This should be treated as the first migration reconciliation baseline until TDC supplies the final signed fleet register.

| Fleet group | Vehicle or asset type | Quantity | Implementation relevance |
| --- | --- | ---: | --- |
| Administrative/Self-Drive Fleet | Toyota Tundra | 1 | Fleet classification seed, import validation, utilization reporting. |
| Administrative/Self-Drive Fleet | Toyota Land Cruiser | 1 | Fleet classification seed, import validation, utilization reporting. |
| Administrative/Self-Drive Fleet | Saloon Cars | 1 | Fleet classification seed, import validation, utilization reporting. |
| Administrative/Self-Drive Fleet | SUVs | 4 | Fleet classification seed, import validation, utilization reporting. |
| Administrative/Self-Drive Fleet | Mitsubishi 4x4 | 1 | Fleet classification seed, import validation, utilization reporting. |
| Administrative/Self-Drive Fleet | Pickups | 6 | Fleet classification seed, import validation, utilization reporting. |
| Operational Fleet | Operational Saloons | 3 | Dispatch availability, fuel allocation, operational reporting. |
| Operational Fleet | Operational Pickups | 21 | Dispatch availability, fuel allocation, operational reporting. |
| Operational Fleet | Mitsubishi 4x4 | 1 | Dispatch availability, fuel allocation, operational reporting. |
| Operational Fleet | Mini Vans | 2 | Dispatch availability, fuel allocation, operational reporting. |
| Operational Fleet | Coaster Bus | 1 | Dispatch availability, fuel allocation, operational reporting. |
| Equipment and Other Assets | Backhoe Loaders | 2 | Equipment classification, maintenance schedule, usage tracking. |
| Equipment and Other Assets | Motorcycles | 6 | Six-month roadworthiness rule, dispatch and compliance filtering. |
| Equipment and Other Assets | Tricycle | 1 | Equipment classification, maintenance schedule, usage tracking. |
| Equipment and Other Assets | Tipper Truck | 1 | Equipment classification, maintenance schedule, usage tracking. |
| Total | All fleet/equipment records stated in questionnaire | 52 | Migration row-count target and dashboard opening-balance check. |

## Policy Values Stated In The Questionnaire

These values are explicitly stated by the source document. They should still be loaded as effective-dated configuration rather than hard-coded constants.

| Policy area | Stated value | Tracker coverage |
| --- | --- | --- |
| Daily trip request timing | Most vehicle requests are received by 8:00 AM daily. | `FLT-CFG-004`, `TRP-004`, `FLT-0204`, `FLT-E2E-003`, `FLT-E2E-004` |
| Vehicle service interval | Every 5,000 km or 6 months. | `FLT-CFG-006`, `MTN-001`, `FLT-0401`, `FLT-E2E-007` |
| Tyre rotation interval | Every 8,000 km or 6 months. | `FLT-CFG-006`, `MTN-006`, `FLT-0401`, `FLT-E2E-011` |
| Battery replacement interval | Every 2 years. | `FLT-CFG-006`, `MTN-006`, `FLT-0401`, `FLT-E2E-011` |
| Tyre replacement interval | Every 32,000 km or approximately 2 years. | `FLT-CFG-006`, `MTN-006`, `FLT-0401`, `FLT-E2E-011` |
| Roadworthiness certificate | Annually. | `FLT-CFG-007`, `CMP-002`, `FLT-0501`, `FLT-E2E-012` |
| Vehicle insurance | Annually. | `FLT-CFG-007`, `CMP-002`, `CLM-001`, `FLT-0501`, `FLT-E2E-016` |
| Motorcycle roadworthiness | Every 6 months. | `FLT-CFG-007`, `CMP-002`, `FLT-0501`, `FLT-E2E-012` |

## Business Configuration Inputs

| ID | Priority | Status | Configuration or decision required | Why it matters | Owner |
| --- | --- | --- | --- | --- | --- |
| FLT-CFG-001 | P0 | Configuration required | Confirm Transport roles, responsibilities, and workflow actors: Management, Administration, Finance, Internal Audit, MERC, Transport Section, Department Heads, Stores, Procurement, drivers, and supervisors. | Approval routing, notifications, dashboards, and audit visibility depend on role mapping. | Administration + Transport + ICT |
| FLT-CFG-002 | P0 | Configuration required | Confirm fleet categories/classification: administrative/self-drive fleet, operational fleet, equipment, motorcycles, tricycles, tipper trucks, buses, saloons, SUVs, pickups, and heavy equipment. | Master data validation and reporting should use approved categories. | Transport + Fixed Assets |
| FLT-CFG-003 | P0 | Configuration required | Confirm mandatory vehicle master fields: registration number, VIN/chassis number, engine number, ownership status, assigned location, condition, category, fuel type, year of manufacture, make, type, and classification. | The current asset model does not expose every field as mandatory for Fleet. | Transport + Fixed Assets |
| FLT-CFG-004 | P0 | Configuration required | Confirm trip approval route, deadline rule, and escalation: user department request, Department Head approval, Head of Administration review, Transport Supervisor assignment, and 8:00 AM daily request timing. | Trip dispatch should be policy-driven rather than ad hoc. | Administration + Transport |
| FLT-CFG-005 | P0 | Configuration required | Confirm standard fuel allocation per vehicle/class, additional fuel approval route, variance tolerance, and whether fuel card controls are required in phase one. | Fuel requisition and consumption analytics need approved thresholds. | Transport + Finance |
| FLT-CFG-006 | P1 | Configuration required | Confirm preventive maintenance intervals: service every 5,000 km or 6 months, tyre rotation every 8,000 km or 6 months, battery replacement every 2 years, tyre replacement every 32,000 km or approximately 2 years. | PM schedules must use approved intervals and effective dates. | Transport |
| FLT-CFG-007 | P1 | Configuration required | Confirm statutory renewal rules: roadworthiness annually, insurance annually, motorcycle roadworthiness every 6 months, plus any local authority exceptions. | Compliance templates and dispatch blockers depend on due dates. | Transport + Finance + MERC |
| FLT-CFG-008 | P1 | Configuration required | Confirm driver eligibility requirements: licence classes, verification, medical fitness, training, permits, disciplinary restrictions, leave status, and overtime capture. | Dispatch and driver scheduling must prevent ineligible assignment. | HR + Transport |
| FLT-CFG-009 | P1 | Configuration required | Confirm external workshop workflow: quotation requirement, Transport estimate review, Finance approval, vendor selection, completion verification, invoice evidence, and vendor performance scoring. | Repairs need procurement/finance evidence before approval and payment. | Transport + Procurement + Finance |
| FLT-CFG-010 | P1 | Configuration required | Confirm incident, theft, breakdown, police/security evidence, corrective action, and final decision authority. | Incident workflows and audit evidence must match TDC policy. | Transport + Security + Management |
| FLT-CFG-011 | P1 | Configuration required | Confirm GPS provider, API format, device identifiers, update frequency, geofence policy, retention, and offline behavior. | Real-time tracking cannot be implemented generically without provider details. | Transport + ICT |
| FLT-CFG-012 | P2 | Configuration required | Confirm reporting pack, KPI definitions, distribution list, and weekly/monthly deadlines. | Dashboards must reconcile to the reports TDC expects. | Transport + Management |
| FLT-CFG-013 | P2 | Configuration required | Confirm migration owners and source Excel columns for fleet register, driver records, logbook readings, compliance renewals, fuel history, maintenance history, incident history, and insurance claims. | Migration cannot be acceptance-ready without signed source ownership and reconciliation. | Transport + ICT |
| FLT-CFG-014 | P0 | Configuration required | Confirm Transport budget planning, Management approval, Finance oversight, budget-control dimensions, commitment rules, spending thresholds, and exception authority. | The questionnaire assigns Transport budget approval to Management and budget oversight to Finance; fleet expenditure must reconcile to an approved budget. | Management + Transport + Finance |
| FLT-CFG-015 | P0 | Configuration required | Confirm the Internal Audit pre-payment validation matrix, mandatory payment-pack evidence, sequencing, segregation of duties, rejection/correction route, and controlled emergency exceptions for fuel, repairs, tyres, batteries, lubricants, insurance, roadworthiness, acquisition, and other transport expenditure. | The questionnaire requires Internal Audit verification and validation of transport-related payment documents before payment. | Internal Audit + Finance + Transport |
| FLT-CFG-016 | P1 | Configuration required | Confirm comprehensive-insurance policy rules, approved insurer/broker register, renewal lead times, premium approval/payment route, policy evidence, claim authority, and settlement-reconciliation rules. | Insurance compliance, payments, claims, and dispatch eligibility must operate as one controlled lifecycle. | Transport + Finance + Legal/Compliance |

## Current Coverage And Gap Matrix

### Governance, Roles, And Policy

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| GOV-001 | Partial | The ERP has roles, workflow, notifications, and tenant/location scoping. TDC-specific Transport governance across Management, Administration, Finance, Internal Audit, MERC, Stores, Procurement, and Transport Section is not delivered as a tested fleet role matrix. |
| GOV-002 | Partial | Workflow integration exists for FleetTrip and FleetTripInspection, but the exact TDC approval sequences for trip, fuel, repairs, acquisition, commissioning, disposal, incident decisions, and claims are not configured and acceptance-tested. |
| GOV-003 | Gap | No dedicated Fleet policy register for Transport Policy, Vehicle Maintenance Policy, Asset Disposal Policy, approved intervals, evidence requirements, decision authority, and effective-dated policy versions. |
| GOV-004 | Partial | Audit fields exist on entities and workflow history exists, but fleet-specific immutable audit events are not guaranteed across every transport action, approval, document, dispatch block, override, fuel issue, repair quotation, incident decision, and insurance claim. |
| GOV-005 | Partial | Finance budgets, procurement commitments, maintenance costs, and payment capabilities exist elsewhere in the ERP, but there is no Fleet Transport budget lifecycle linking Management approval and Finance oversight to acquisition, fuel, maintenance, parts, insurance, statutory renewal, and other fleet expenditure. |
| GOV-006 | Gap | Internal Audit is not enforced as a pre-payment verification gate for every configured class of transport-related payment document, and direct API/payment attempts are not proven to reject an incomplete or unvalidated Fleet payment pack. |

### Fleet Scope, Master Data, And Lifecycle

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| MST-001 | Verified baseline | Fleet vehicles are represented as `MaintenanceAsset` records with `IsFleetAsset`, category/type, license plate, VIN, make/model, fuel type, location, mileage, operating hours, status, project/site assignment, and fleet list/card UI. |
| MST-002 | Partial | Generic maintenance asset Excel import exists, but no TDC fleet-specific staging import validates all required master fields, fleet grouping, duplicates, ownership status, engine number, year of manufacture, and source reconciliation. |
| MST-003 | Partial | Asset categories and vehicle asset type exist, but TDC's fleet groupings from the questionnaire are not seeded as a governed fleet classification catalogue with mandatory reporting groups. |
| MST-004 | Partial | Vehicle year, engine number, ownership status, and condition may exist in adjacent asset/fixed-asset fields or specifications, but they are not confirmed as mandatory, first-class Fleet master fields with import/edit validation and visible reporting. |
| LIF-001 | Partial | Procurement, fixed assets, and maintenance assets exist, but vehicle acquisition is not linked end to end from Management approval, Procurement sourcing, Transport technical specification, supplier delivery, and asset creation. |
| LIF-002 | Gap | No fleet commissioning workflow captures joint inspection by Procurement, Internal Audit, Stores, and Transport Section with signed report evidence. |
| LIF-003 | Gap | No controlled Stores handover and Transport requisition workflow for newly commissioned vehicles. |
| LIF-004 | Partial | Asset movement history exists with project/site/location, reason, notes, effective date/time, and reports. The TDC Department Head request plus Head of HR & Administration approval route for vehicle reassignment is not enforced. |
| LIF-005 | Partial | Fixed-asset disposal exists and asset statuses include retired/disposed concepts, but Fleet-specific replacement, retirement, sale, write-off, legal evidence, and disposal authority are not implemented as one lifecycle. |

### Driver And User Management

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| DRV-001 | Verified baseline | Fleet Drivers page reads HR employees and shows licence number/status/expiry/verification, vehicle assignment, trip activity, KPIs, filters, pagination, and detail view. |
| DRV-002 | Partial | Licence validity is enforced for trip creation/dispatch, but training records, medical fitness, permit information, and disciplinary restrictions are not centrally validated before driver assignment. |
| DRV-003 | Partial | Fleet vehicle assignment exists, with current assigned driver visibility. It is not yet tied to a formal departmental request/approval process and operational priority scoring. |
| DRV-004 | Partial | Trips can assign drivers and detect dispatched conflicts. A daily driver scheduling board with availability, leave integration, planned rosters, and reassignment audit is incomplete. |
| DRV-005 | Gap | Driver overtime capture from daily logbooks and after-hours trip records is not implemented as a payroll/HR-integrated workflow. |
| DRV-006 | Partial | Fleet reads HR employee and licence data, but no single eligibility decision combines licence class/verification, training, medical fitness, permits, disciplinary restrictions, active status, approved leave, roster, and current trip conflict before assignment and again before dispatch. |

### Trip Planning, Dispatch, And Journey Logs

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| TRP-001 | Verified baseline | Fleet trips support vehicle, driver, purpose, origin, destination/route template, planned/actual start/end, workflow submit/approve/reject, dispatch, completion, odometer/hour-meter readings, status, and conflict checks. |
| TRP-002 | Partial | Workflow is available, but the TDC route of User Department -> Department Head -> Head of Administration -> Transport Supervisor assignment is not configured as the default acceptance workflow. |
| TRP-003 | Partial | Vehicle and driver assignment validates availability/licence/conflicts, but urgency, importance, operational priority, and request timing are not captured as structured allocation criteria. |
| TRP-004 | Gap | The 8:00 AM daily vehicle request deadline is not enforced with SLA, exception, or escalation behavior. |
| TRP-005 | Verified baseline | Journey log basics are present through trip origin/destination/purpose/driver/mileage and reports. |
| TRP-006 | Partial | A phone-first mobile logbook for trip start/end, mileage, fuel, incident, driver signature, and offline journey updates is not fully implemented; mobile support currently focuses on inspections. |
| TRP-007 | Gap | No GPS tracking integration for real-time vehicle location, route trace, geofencing, stop events, or map dashboards. |

### Fuel Management

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| FUEL-001 | Partial | Fuel transactions exist and are required to link to a trip; quantity, unit, unit cost, total cost, mileage/hour reading, vendor/station, receipt reference, notes, and cost entry are captured. |
| FUEL-002 | Gap | Standard fuel allocation per operational vehicle is not configured and enforced. |
| FUEL-003 | Gap | Additional fuel requisition, Finance approval, fuel issue, and approval evidence are not implemented as a workflow. |
| FUEL-004 | Partial | Fuel consumption analytics exist in dashboard/reports as average km/l and fuel cost per km. Full variance reporting against allocation, logbook reconciliation, anomalies, and exception approval are incomplete. |
| FUEL-005 | Gap | Fuel card management, card assignment, limits, provider import, statement reconciliation, PIN/driver controls, and exception handling are missing. |

### Budget, Financial Control, Payments, And Assurance

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| FIN-001 | Partial | General budget and project/maintenance cost capabilities exist, but no effective-dated Transport budget links approved fleet categories and cost centres to acquisition, fuel, repairs, tyres, batteries, lubricants, insurance, roadworthiness, incidents, and disposal. |
| FIN-002 | Partial | Fleet fuel, external-repair, tyre, battery, incident, Work Order, and manual cost entries provide operational cost history. They do not yet produce complete commitment, invoice, payment, budget-actual, and GL reconciliation from the originating Fleet transaction. |
| FIN-003 | Gap | Internal Audit validation of transport-related payment documents is not implemented as a configurable, evidence-backed hard stop before Finance payment processing. |
| FIN-004 | Partial | Compliance and incident records can hold insurance and roadworthiness information, but renewal premium/fee approval, payment, receipt, policy/certificate evidence, broker/insurer reconciliation, and dispatch-blocker clearance are not one lifecycle. |

### Maintenance, Workshop, And Parts

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| MTN-001 | Verified baseline | Maintenance schedules support time/usage/condition triggers; fleet assets show due/overdue maintenance and Work Orders can be created. |
| MTN-002 | Verified baseline | QR/mobile pre-trip, post-trip, service, weekly, and preventive inspection sheets are supported with offline capture and checklist validation. |
| MTN-003 | Verified baseline | Failed or flagged mobile inspections create Fleet Defects and linked Work Orders with actionable tasks. |
| MTN-004 | Partial | TDC's repair process of driver defect report -> Transport job card -> approval -> repair -> completion verification exists across defects/work orders/QC, but the exact TDC workflow and evidence requirements are not configured end to end. |
| MTN-005 | Partial | External repairs exist with vendor, estimate, approval/completion/invoicing statuses, cost entries, and Work Order linkage. Workshop quotation, Transport estimate review, Finance approval, supplier evidence, and vendor performance scoring are incomplete. |
| MTN-006 | Partial | Tyres and batteries have fleet-specific records and event history. Procurement/Stores inventory linkage for tyres, lubricants, and batteries is not fully enforced through requisition, issue, receipt, and Work Order consumption. |
| MTN-007 | Partial | Maintenance cost analysis and lifecycle costing exist in FleetCostEntry and reports, but asset lifecycle costing across acquisition, fuel, repairs, tyre/battery, insurance, depreciation/disposal, and downtime needs a complete Fleet management dashboard/report. |

### Compliance, Safety, Incidents, Theft, Breakdown, And Claims

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| CMP-001 | Verified baseline | Fleet compliance items/templates support expiry, critical flag, documents, due-soon/overdue reminders, and dispatch-blocking checks. |
| CMP-002 | Partial | Roadworthiness, insurance, and motorcycle roadworthiness frequencies can be configured, but the exact TDC templates and renewal workflow are not seeded and tested. |
| SFT-001 | Verified baseline | Pre-trip and post-trip inspections plus failed/flagged safety defect handling exist. |
| SFT-002 | Partial | Safety reporting for accidents, near misses, unsafe conditions, and vehicle hazards exists partially through incidents/defects, but classification, investigation, corrective action, management notification, and closure evidence need standardization. |
| INC-001 | Partial | Fleet incidents capture accident/incident details, severity, location, damage assessment, repair cost, driver, trip, Work Order, and insurance claim fields. |
| INC-002 | Gap | Accident workflow does not enforce driver incident report, Transport investigation, police involvement evidence where required, management notification, and corrective action approval. |
| INC-003 | Gap | Theft workflow does not enforce security footage review, Security and Transport investigation, management findings, and recovery/write-off outcome. |
| INC-004 | Gap | Breakdown workflow does not enforce immediate Transport contact, assistance arrangement, formal incident report, downtime, recovery cost, and follow-up maintenance. |
| CLM-001 | Partial | Insurance company, policy, claim number, claim status, claim amount, submitted date, and settled date fields exist on incidents. Full claim workflow with broker, documents, approvals, settlement reconciliation, and audit pack is incomplete. |

### Reporting, Dashboards, Analytics, And Alerts

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| RPT-001 | Partial | Fleet dashboard summary and reports exist for utilization, cost, trips, defects, compliance, fuel, and maintenance-related data. |
| RPT-002 | Partial | Weekly operational reports can be derived from trips and reports, but TDC's exact weekly pack covering distance travelled, vehicle utilization, odometer readings, and activity summary is not delivered as a named report with schedule/export. |
| RPT-003 | Partial | Cost and efficiency analytics include average fuel consumption and fuel cost per km, but weekly fuel utilization, allocation variance, logbook reconciliation, and cost-per-vehicle distribution need completion. |
| RPT-004 | Partial | Dashboards exist, but the questionnaire specifically requests real-time fleet dashboards including utilization, fuel, maintenance, compliance, vehicle availability, and cost analysis. Real-time tracking and complete KPI definitions are incomplete. |
| ALT-001 | Partial | Notifications and compliance reminders exist, but comprehensive alerts for trip request SLA, overdue approvals, fuel variance, maintenance due, compliance expiry, incident actions, insurance claims, and GPS exceptions are not fully configured. |

### Controls, Approvals, Audit, And Mobile

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| CTL-001 | Partial | Shared workflow platform can handle trip, inspection, Work Order, and other approvals. TDC-specific trip, fuel, repair, acquisition, commissioning, transfer, disposal, incident, claim, and inventory approval workflows are not all configured. |
| CTL-002 | Partial | Workflow history and entity audit fields exist. A complete Fleet audit trail report across vehicle, trip, maintenance, fuel, compliance, statutory renewal, incident, and decision records is incomplete. |
| MOB-001 | Verified baseline | Mobile QR inspection and offline checklist flow is implemented for fleet inspections/service sheets. |
| MOB-002 | Partial | Mobile inspection is strong, but mobile journey logbook, fuel issue capture, breakdown/incident capture, photo evidence, and driver daily logbook workflows are not complete as one mobile fleet workspace. |

### Integrations, Migration, And Standardisation

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| INT-001 | Partial | HR employees, licence data, Maintenance assets, Work Orders, Inventory parts, Procurement vendors, Fixed Assets, Workflow, Notifications, and Reports have integration touchpoints. The full Fleet interface contract is not documented or acceptance-tested. |
| INT-002 | Gap | GPS/telematics provider integration is missing. |
| INT-003 | Gap | Fuel card/provider integration and statement reconciliation are missing. |
| INT-004 | Partial | Stores/Inventory supports Work Order parts, but fleet-specific tyres, batteries, lubricants, store requisitions, issues, and maintenance consumption are not fully enforced end to end. |
| MIG-001 | Partial | Generic asset import exists. TDC fleet migration staging, duplicate checks, source reconciliation, and historical logbook/fuel/compliance/incident migration are missing. |
| STD-001 | Partial | Configurable templates and categories exist. TDC Fleet standardization for vehicle classes, workflows, inspection sheets, report packs, alerts, and KPI definitions is incomplete. |

## Source Governance And Process Control Traceability

This table closes the governance and financial-control responses that sit outside the questionnaire's final 20-item functional list.

| Questionnaire control | Current coverage | Implementation task IDs |
| --- | --- | --- |
| Management approves the Transport budget and vehicle acquisition. | Partial. Acquisition approval can use workflow, but the Fleet budget lifecycle and budget hard stops are not integrated. | `FLT-0005`, `FLT-0103`, `FLT-0108`, `FLT-0607`, `FLT-E2E-021` |
| Administration controls vehicle allocation, maintenance approval, driver duty assignment, and maintenance scheduling. | Partial. Assignment, schedules, Work Orders, and workflow foundations exist; TDC authority routes and duty roster controls require configuration. | `FLT-0004`, `FLT-0201`, `FLT-0203`, `FLT-0207`, `FLT-0401`, `FLT-0403` |
| Finance approves fuel allocation, processes payments, pays insurance/roadworthiness, and oversees budget. | Partial. Fuel and cost records exist, but allocation workflow, renewal payments, budget reconciliation, and payment lineage are incomplete. | `FLT-0303`, `FLT-0304`, `FLT-0507`, `FLT-0607`, `FLT-E2E-008`, `FLT-E2E-021`, `FLT-E2E-022` |
| Internal Audit validates transport payment documents before payment. | Gap. No universal evidence-backed pre-payment gate exists for Fleet-originated expenditure. | `FLT-CFG-015`, `FLT-0005`, `FLT-0607`, `FLT-E2E-021` |
| MERC governs Transport policy and compliance. | Partial. Policy and role infrastructure exist, but MERC authority, read access, policy versioning, and compliance oversight are not configured and tested. | `FLT-0001`, `FLT-0002`, `FLT-0004`, `FLT-0501`, `FLT-0604` |
| Transport Section owns operations, maintenance coordination, utilization, reporting, and records. | Partial. Operational screens exist; complete authority, audit, mobile, and management-reporting coverage remains incomplete. | `FLT-0003`, `FLT-0201` through `FLT-0207`, `FLT-0401` through `FLT-0406`, `FLT-0601` through `FLT-0607` |
| Transport, Vehicle Maintenance, and Asset Disposal policies govern the lifecycle. | Gap/Partial. Generic configuration exists, but no effective-dated Fleet policy register connects policy versions to runtime rules and evidence. | `FLT-0001`, `FLT-0002`, `FLT-0107`, `FLT-0401`, `FLT-0501` |
| Incident decisions are made by the relevant Head of Department. | Partial. Incident records exist, but decision authority, targeted workflow, evidence, outcome, and audit are not enforced. | `FLT-CFG-010`, `FLT-0502` through `FLT-0505`, `FLT-E2E-013` through `FLT-E2E-015` |

## Key ERP Functional Requirement Traceability

This maps the questionnaire's final "Key ERP Functional Requirements Identified" list directly to the current tracker. It is the quick checklist to decide whether a requirement is already materially covered or still needs an implementation slice.

| No. | Questionnaire requirement | Current coverage | Implementation task IDs |
| ---: | --- | --- | --- |
| 1 | Fleet Master Data Management | Partial. Fleet assets exist, but TDC mandatory fields, governed classifications, source Excel staging, and reconciliation are incomplete. | `FLT-0101`, `FLT-0102`, `FLT-0701` |
| 2 | Vehicle Lifecycle Management | Partial. Asset movement and fixed-asset disposal exist, but acquisition, commissioning, Stores handover, transfer approval, and Fleet disposal lifecycle are incomplete. | `FLT-0103` through `FLT-0107`, `FLT-E2E-002`, `FLT-E2E-017`, `FLT-E2E-018` |
| 3 | Driver Management | Partial. HR-linked driver directory and licence checks exist, but training, medical fitness, permit, disciplinary, leave, roster, and overtime controls are incomplete. | `FLT-0203`, `FLT-0206`, `FLT-0207`, `FLT-E2E-005` |
| 4 | Vehicle Allocation and Dispatch | Partial. Trips support vehicle/driver assignment and conflict checks, but TDC priority allocation, daily dispatch board, and 8:00 AM exception controls are incomplete. | `FLT-0201` through `FLT-0204`, `FLT-E2E-003`, `FLT-E2E-004` |
| 5 | Electronic Trip Requests and Approvals | Partial. Workflow-enabled trip records exist, but the exact Department/Administration/Transport approval route is not configured and tested as the default. | `FLT-0201`, `FLT-E2E-003`, `FLT-E2E-020` |
| 6 | GPS Vehicle Tracking | Gap. The source states no GPS tracking system, and the ERP does not yet have provider integration, route traces, geofences, or real-time map dashboard. | `FLT-0301`, `FLT-0302`, `FLT-E2E-009` |
| 7 | Fuel Management and Consumption Analysis | Partial. Fuel transactions and basic analytics exist; standard allocation, additional-fuel workflow, variance reporting, logbook reconciliation, and fuel cards are missing. | `FLT-0303` through `FLT-0306`, `FLT-0602`, `FLT-E2E-008` |
| 8 | Preventive Maintenance Scheduling | Verified baseline for generic schedules and Work Order generation, but TDC interval seeding and acceptance scenarios are still required. | `FLT-0401`, `FLT-E2E-007` |
| 9 | Electronic Job Cards | Partial. Work Orders/job cards exist, but the exact driver defect -> Transport job card -> approval -> repair -> completion verification flow needs TDC workflow/evidence configuration. | `FLT-0403`, `FLT-E2E-006`, `FLT-E2E-010` |
| 10 | Workshop and Vendor Management | Partial. External repair records exist, but quotation review, Finance approval, invoice evidence, and vendor/workshop performance scoring are incomplete. | `FLT-0404`, `FLT-0406`, `FLT-E2E-010` |
| 11 | Compliance and Renewal Management | Partial. Compliance templates/items exist and can block dispatch; TDC renewal templates, seeded rules, evidence, reminders, payment reconciliation, and renewal workflow need completion. | `FLT-0501`, `FLT-0507`, `FLT-0607`, `FLT-E2E-012`, `FLT-E2E-022` |
| 12 | Accident and Incident Management | Partial. Incident records exist; accident, theft, breakdown, investigation, corrective-action, and management decision workflows are incomplete. | `FLT-0502` through `FLT-0505`, `FLT-E2E-013`, `FLT-E2E-014`, `FLT-E2E-015` |
| 13 | Insurance Claims Tracking | Partial. Claim fields exist on incidents, but policy/broker control, premium/renewal payment, broker submission, document pack, settlement/rejection, proceeds, and financial reconciliation are incomplete. | `FLT-0506`, `FLT-0507`, `FLT-0607`, `FLT-E2E-016`, `FLT-E2E-022` |
| 14 | Inventory Management for Tyres, Batteries and Lubricants | Partial. Tyre and battery records exist; Stores/Inventory requisition, receipt, issue, returns, Work Order consumption, and lubricants control are incomplete. | `FLT-0405`, `FLT-E2E-011` |
| 15 | Fleet Cost Management | Partial. Fleet cost entries and some reports exist; approved Transport budget, commitments, AP/payment/GL reconciliation, and lifecycle costing across fuel, repairs, tyres, batteries, insurance, depreciation, disposal, and downtime are incomplete. | `FLT-0005`, `FLT-0602`, `FLT-0603`, `FLT-0607`, `FLT-E2E-019`, `FLT-E2E-021` |
| 16 | Automated Alerts and Notifications | Partial. Compliance reminders and generic notifications exist; trip SLA, fuel variance, open defects, incidents, claims, and GPS exception alerts are incomplete. | `FLT-0605`, `FLT-E2E-020` |
| 17 | Fleet Dashboards and KPI Reporting | Partial. Fleet dashboards/reports exist, but TDC's real-time dashboard, KPI definitions, scheduled packs, and GPS-backed availability are incomplete. | `FLT-0601`, `FLT-0603`, `FLT-E2E-019` |
| 18 | Complete Audit Trail and Approval Workflows | Partial. Workflow history and audit fields exist; a complete immutable Fleet audit map/report, Internal Audit pre-payment control, and direct-API bypass tests are incomplete. | `FLT-0003`, `FLT-0005`, `FLT-0604`, `FLT-0607`, `FLT-E2E-020`, `FLT-E2E-021` |
| 19 | Mobile Inspection and Logbook Management | Partial. Mobile offline inspection is strong; mobile journey logbook, fuel, incident/breakdown, photo evidence, and driver daily logbook are incomplete. | `FLT-0205`, `FLT-0606`, `FLT-E2E-003` |
| 20 | Management Reporting and Analytics | Partial. Reports exist, but the complete management pack for utilization, fuel, maintenance, compliance, availability, cost, exceptions, and scheduled exports is incomplete. | `FLT-0601` through `FLT-0604`, `FLT-E2E-019` |

## Implementation Roadmap

### Phase 0 - Configuration, Policy, And Acceptance Design

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| FLT-0001 | P0 | Not started | Resolve `FLT-CFG-001` through `FLT-CFG-016` as effective-dated configuration. | Every configuration item has owner, approved value, effective date, evidence, and tenant seed plan. |
| FLT-0002 | P0 | Not started | Build Fleet policy/config register. | Transport policy, maintenance policy, disposal policy, intervals, evidence rules, alert rules, and approval route keys are versioned and queryable. |
| FLT-0003 | P0 | Not started | Define Fleet audit event map. | Every lifecycle action has event type, actor, role, before/after values, source, evidence links, and report visibility. |
| FLT-0004 | P0 | Not started | Configure TDC Fleet roles, permissions, and dashboard scopes. | Test users can only see and act on the records permitted by role/location. |
| FLT-0005 | P0 | Not started | Define Fleet financial control, payment-pack, and Internal Audit assurance architecture. | Budget dimensions, commitments, evidence, maker-checker roles, Internal Audit validation, Finance payment sequencing, exceptions, and reconciliation controls are approved and testable. |

### Phase 1 - Fleet Master, Migration, Acquisition, Commissioning, Transfer, And Disposal

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| FLT-0101 | P0 | Not started | Add TDC fleet master profile and required field validation. | Registration, VIN/chassis, engine number, ownership, location, condition, category, fuel type, year, make, type, classification, and fleet group persist/display/import/export. |
| FLT-0102 | P0 | Not started | Build fleet-specific import staging and reconciliation. | Excel source validates duplicates, missing fields, category mapping, old/new values, and signed load results before posting. |
| FLT-0103 | P1 | Not started | Link acquisition request to Procurement and Fixed Assets. | Management approval, Transport specification, Procurement sourcing, supplier delivery, and asset record are traceable. |
| FLT-0104 | P1 | Not started | Implement commissioning workflow. | Procurement, Internal Audit, Stores, and Transport joint inspection evidence/sign-off is mandatory before asset release. |
| FLT-0105 | P1 | Not started | Implement Stores handover and Transport requisition. | Vehicle cannot become available until Stores handover and Transport requisition are complete. |
| FLT-0106 | P0 | Not started | Add vehicle transfer/reassignment workflow. | Department Head request and Head of HR/Admin approval update assignment/location and movement history with evidence. |
| FLT-0107 | P1 | Not started | Add Fleet disposal lifecycle. | Replacement, retirement, sale, write-off, legal evidence, approvals, fixed-asset disposal, stock/insurance closure, and audit trail reconcile. |
| FLT-0108 | P1 | Not started | Integrate vehicle acquisition with approved Transport budget and capitalization. | Acquisition approval validates budget, creates the required commitment, links Procurement delivery and commissioning, and reconciles to the Fixed Asset record and actual cost. |

### Phase 2 - Trip Requests, Dispatch Planning, Driver Scheduling, And Journey Logs

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| FLT-0201 | P0 | Not started | Configure TDC trip request workflow. | User department, Department Head, Head of Administration, and Transport Supervisor stages route correctly and are enforced by API. |
| FLT-0202 | P0 | Not started | Add trip priority and allocation criteria. | Urgency, importance, request timing, department, and vehicle/driver availability influence assignment and remain auditable. |
| FLT-0203 | P1 | Not started | Add daily dispatch planning board. | Transport can see requests, vehicles, drivers, licence status, leave/availability, open trips, and conflicts in one schedule view. |
| FLT-0204 | P1 | Not started | Enforce 8:00 AM request rule and exceptions. | Late requests require reason/approval and appear on SLA/exception report. |
| FLT-0205 | P1 | Not started | Add mobile journey logbook. | Driver can capture trip start/end, odometer, hour-meter, notes, fuel, incidents, and photos online/offline. |
| FLT-0206 | P2 | Not started | Add driver overtime calculation from trip/logbook. | After-hours trip records produce reviewable overtime transactions for HR/Payroll. |
| FLT-0207 | P0 | Not started | Complete driver qualification, availability, and duty eligibility controls. | Licence class/verification, training, medical fitness, permits, disciplinary restrictions, active status, leave, roster, and trip conflicts are checked at assignment and dispatch with override audit where policy permits. |

### Phase 3 - GPS, Fuel, Fuel Cards, And Variance Control

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| FLT-0301 | P1 | Not started | Implement GPS/telematics integration contract. | Provider, device mapping, polling/webhook, retry, route trace, current location, geofence, and retention are documented and tested. |
| FLT-0302 | P1 | Not started | Add real-time vehicle tracking dashboard. | Map/list shows current location, status, last ping, trip linkage, and exception states. |
| FLT-0303 | P0 | Not started | Configure standard fuel allocation per vehicle/class. | Allocation is effective-dated and visible during trip/fuel planning. |
| FLT-0304 | P0 | Not started | Add additional fuel requisition workflow. | Additional fuel requires request, Finance approval, issue evidence, and trip/vehicle linkage. |
| FLT-0305 | P1 | Not started | Add fuel variance and logbook reconciliation. | Fuel usage compares allocation, trip distance, odometer, fuel records, and exception approvals. |
| FLT-0306 | P2 | Not started | Add fuel card management. | Cards, drivers, vehicles, limits, provider statements, exceptions, and reconciliations are controlled. |

### Phase 4 - Maintenance, External Workshop, Stores, And Vendor Performance

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| FLT-0401 | P0 | Not started | Seed TDC preventive maintenance schedules. | Service, tyre rotation, battery replacement, and tyre replacement use approved interval rules and generate due statuses/Work Orders. |
| FLT-0402 | P0 | Not started | Configure daily/pre/post inspection sheets. | Required checklist items, QR labels, mobile capture, failures, defects, Work Orders, and workflow review are accepted by TDC. |
| FLT-0403 | P1 | Not started | Enforce repair workflow from defect to completion. | Driver defect report, Transport job card/Work Order, approval, repair, QC, completion verification, and defect closure work end to end. |
| FLT-0404 | P1 | Not started | Complete external workshop quotation and approval workflow. | Vendor quote, Transport estimate review, Finance approval, Work Order, invoice, evidence, and cost posting are mandatory as configured. |
| FLT-0405 | P1 | Not started | Link tyres, batteries, and lubricants to Stores/Inventory. | Procurement receipt, Stores issue, Transport requisition, Work Order consumption, returns, and cost history are traceable. |
| FLT-0406 | P2 | Not started | Add vendor/workshop performance scoring. | Delivery time, cost variance, quality, rework, invoice exceptions, and user ratings produce performance history. |

### Phase 5 - Compliance, Safety, Incidents, Theft, Breakdown, And Insurance Claims

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| FLT-0501 | P0 | Not started | Seed statutory compliance templates and renewal workflows. | Roadworthiness, insurance, motorcycle roadworthiness, reminders, evidence, dispatch blockers, and renewal reports work. |
| FLT-0502 | P1 | Not started | Add safety event taxonomy and corrective action workflow. | Accident, near miss, unsafe condition, hazard, defect, and corrective actions are routed and closed with evidence. |
| FLT-0503 | P1 | Not started | Complete accident workflow. | Driver report, investigation, police evidence, management notification, repair/claim/work order linkage, and corrective action are enforced. |
| FLT-0504 | P1 | Not started | Complete theft workflow. | Security evidence, joint investigation, findings, management decision, claim/recovery/write-off outcome, and audit pack exist. |
| FLT-0505 | P1 | Not started | Complete breakdown workflow. | Immediate report, assistance arranged, downtime, recovery action, formal report, Work Order, and cost follow-up are captured. |
| FLT-0506 | P1 | Not started | Complete insurance claims lifecycle. | Broker, claim submission, documents, approvals, settlement, rejection, proceeds, repair linkage, and reconciliation are tracked. |
| FLT-0507 | P0 | Not started | Complete insurance, roadworthiness, and statutory-renewal payment lifecycle. | Approved insurer/broker, comprehensive cover, renewal request, fee/premium approval, payment-pack evidence, policy/certificate receipt, compliance update, and dispatch-blocker clearance reconcile. |

### Phase 6 - Dashboards, Reports, Alerts, Audit, And Mobile Workspace

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| FLT-0601 | P1 | Not started | Deliver TDC weekly operational report pack. | Distance travelled, utilization, odometer readings, activity summary, exceptions, and export schedule reconcile to trips/logbooks. |
| FLT-0602 | P1 | Not started | Deliver fuel and efficiency report pack. | Average fuel consumption, average fuel cost per vehicle, weekly fuel utilization, cost/km, and variance report reconcile to source records. |
| FLT-0603 | P1 | Not started | Deliver management dashboard. | Fleet utilization, fuel, maintenance, compliance, availability, incidents, claims, and cost analysis support period/location filters. |
| FLT-0604 | P1 | Not started | Deliver complete fleet audit report. | Vehicle, trip, maintenance, fuel, compliance, statutory renewal, incident, claim, approval, and override events are exportable. |
| FLT-0605 | P1 | Not started | Configure automated alerts. | Due/overdue maintenance, compliance expiry, request SLA, dispatch conflicts, fuel variance, open defects, incidents, claims, and GPS exceptions notify correct roles. |
| FLT-0606 | P1 | Not started | Expand mobile fleet workspace. | Mobile app covers inspections, journey log, fuel capture, incident/breakdown capture, pending sync, submitted status, and evidence. |
| FLT-0607 | P0 | Not started | Integrate Transport budget, commitments, AP/payment, Internal Audit validation, and GL reconciliation. | Fleet-originated expenditure cannot be paid without the configured approvals/evidence; budget, commitment, invoice, payment, reversal, and actual-cost status reconcile without duplicate posting. |

### Phase 7 - Migration, Training, UAT, And Go-Live

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| FLT-0701 | P1 | Not started | Execute fleet data migration rehearsal. | Vehicle, driver, compliance, logbook, fuel, maintenance, incident, and claim datasets reconcile row/value counts. |
| FLT-0702 | P2 | Not started | Prepare role-based training. | Transport, drivers, Administration, Finance, Internal Audit, Stores, Procurement, MERC, and Management training materials are approved. |
| FLT-0703 | P1 | Not started | Run end-to-end Fleet UAT. | Representative acquisition, commissioning, trip, fuel, maintenance, incident, claim, disposal, mobile, and reporting scenarios pass. |
| FLT-0704 | P1 | Not started | Define go-live support and acceptance plan. | Owners, dates, cutover, support, rollback, KPI baseline, and signatories are approved. |

## Mandatory End-To-End Acceptance Scenarios

| Scenario ID | Status | Scenario |
| --- | --- | --- |
| FLT-E2E-001 | Not started | Fleet Excel register -> staging validation -> approved import -> fleet master record -> audit report. |
| FLT-E2E-002 | Not started | Vehicle acquisition approved by Management -> Procurement sourcing -> Transport specification -> supplier delivery -> joint commissioning -> Stores handover -> Transport requisition -> vehicle available. |
| FLT-E2E-003 | Not started | Department trip request before 8:00 AM -> Department Head approval -> Head of Administration review -> Transport assignment -> dispatch -> mobile journey log -> completion -> utilization report. |
| FLT-E2E-004 | Not started | Late urgent trip request -> exception reason -> approval escalation -> dispatch -> exception report. |
| FLT-E2E-005 | Not started | Driver with expired/unverified licence, medical, training, permit, or leave conflict is blocked from dispatch. |
| FLT-E2E-006 | Not started | Pre-trip mobile inspection fails -> Fleet Defect -> Work Order -> technician work -> QC pass -> Work Order closed -> defect closed. |
| FLT-E2E-007 | Not started | PM schedule reaches 5,000 km or 6 months -> Work Order generated -> service completed -> next due date recalculated. |
| FLT-E2E-008 | Not started | Additional fuel request -> Finance approval -> fuel issue -> trip fuel capture -> cost entry -> variance report. |
| FLT-E2E-009 | Not started | GPS ping stream updates vehicle current location, trip route trace, dashboard, and exception alert. |
| FLT-E2E-010 | Not started | External repair quote -> Transport review -> Finance approval -> vendor repair -> invoice/evidence -> vendor performance -> cost report. |
| FLT-E2E-011 | Not started | Tyre/battery/lubricant purchased -> Stores receipt -> Transport requisition -> Work Order issue/consumption -> cost history. |
| FLT-E2E-012 | Not started | Roadworthiness due soon -> reminder -> renewal evidence -> dispatch blocker cleared. |
| FLT-E2E-013 | Not started | Accident -> driver report -> Transport investigation -> police evidence -> management notification -> Work Order/claim -> corrective action closure. |
| FLT-E2E-014 | Not started | Theft -> security footage evidence -> Security/Transport investigation -> management decision -> insurance/write-off/recovery closure. |
| FLT-E2E-015 | Not started | Breakdown -> immediate report -> assistance -> downtime -> Work Order -> recovery/repair cost -> formal report. |
| FLT-E2E-016 | Not started | Insurance claim -> broker submission -> settlement/rejection -> repair/disposal linkage -> financial reconciliation. |
| FLT-E2E-017 | Not started | Vehicle transfer -> Department Head request -> HR/Admin approval -> reassignment -> movement history -> report. |
| FLT-E2E-018 | Not started | Vehicle disposal -> authority approval -> legal evidence -> fixed asset disposal -> fleet status retired/sold/written-off -> audit pack. |
| FLT-E2E-019 | Not started | Weekly report pack reproduces distance, utilization, odometer, activity, fuel, maintenance, compliance, availability, and cost from source transactions. |
| FLT-E2E-020 | Not started | Direct API attempts for dispatch, approval, fuel, repair, renewal, claim, disposal, budget, payment, or Internal Audit bypasses are rejected and audited. |
| FLT-E2E-021 | Not started | Approved Transport budget -> Fleet acquisition/fuel/repair/parts expenditure -> commitment/invoice -> complete payment pack -> Internal Audit validation -> Finance payment -> budget/actual/GL reconciliation. |
| FLT-E2E-022 | Not started | Insurance or roadworthiness due -> approved insurer/broker or authority -> renewal request -> premium/fee approval -> Internal Audit validation -> Finance payment -> certificate/policy evidence -> dispatch blocker cleared. |

## Current Code Evidence Anchors

These anchors justify the baseline classifications; they are not proof of final TDC acceptance:

- Fleet operating note: `fleet management.md`
- Mobile/offline fleet inspection tracker: `docs/offline-fleet-inspection-mobile-tasklist.md`
- Maintenance module overview: `docs/MAINTENANCE_MANAGEMENT_MODULE.md`
- Fleet entities: `src/ErpSystem.Core/Entities/Maintenance/FleetEntities.cs`
- Fleet phase 2 entities: `src/ErpSystem.Core/Entities/Maintenance/FleetPhase2Entities.cs`
- Fleet incident entities: `src/ErpSystem.Core/Entities/Maintenance/FleetIncidentEntities.cs`
- Maintenance asset and inspection entities: `src/ErpSystem.Core/Entities/Maintenance/MaintenanceEntities.cs`
- Fleet DTOs: `src/ErpSystem.Core/DTOs/Maintenance/FleetDtos.cs`
- Fleet service interfaces: `src/ErpSystem.Core/Interfaces/Maintenance/IFleetServices.cs`
- Fleet vehicle service: `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetVehicleService.cs`
- Fleet trip service: `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetTripService.cs`
- Fleet inspection service: `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetInspectionService.cs`
- Fleet fuel service: `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetFuelService.cs`
- Fleet compliance service: `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetComplianceService.cs`
- Fleet incident service: `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetIncidentService.cs`
- Fleet external repair service: `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetExternalRepairService.cs`
- Fleet assignment, tyre, battery, cost, health, and compliance-template services: `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetAssignmentService.cs`, `FleetTyreService.cs`, `FleetBatteryService.cs`, `FleetCostService.cs`, `FleetHealthService.cs`, `FleetComplianceTemplateService.cs`
- Fleet dashboard and reports: `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetDashboardService.cs`, `src/ErpSystem.Core/Services/Maintenance/Fleet/FleetReportsService.cs`
- Fleet API controllers: `src/ErpSystem.Api/Controllers/Maintenance/FleetVehiclesController.cs`, `FleetTripsController.cs`, `FleetDriversController.cs`, `FleetAssignmentsController.cs`, `FleetFuelController.cs`, `FleetComplianceController.cs`, `FleetComplianceTemplatesController.cs`, `FleetIncidentsController.cs`, `FleetDefectsController.cs`, `FleetTyresController.cs`, `FleetBatteriesController.cs`, `FleetExternalRepairsController.cs`, `FleetCostsController.cs`, `FleetHealthController.cs`, `FleetDashboardController.cs`, `FleetReportsController.cs`
- Maintenance asset import/movement API: `src/ErpSystem.Api/Controllers/Maintenance/MaintenanceAssetsController.cs`
- Finance budget, AP/payment, audit, and Fixed Asset touchpoints: `src/ErpSystem.Api/Controllers/Finance/BudgetController.cs`, `src/ErpSystem.Api/Controllers/Finance/ApControllersConsolidated.cs`, `src/ErpSystem.Api/Controllers/AuditLogController.cs`, `src/ErpSystem.Core/Entities/Finance/FixedAssets/**`
- Fleet frontend routes: `frontend/src/app/maintenance/fleet/**`
- Mobile fleet inspection route: `frontend/src/app/mobile/fleet/inspection/page.tsx`
- Asset details fleet tabs: `frontend/src/components/maintenance/AssetVehicleFleetTabs.tsx`
- Maintenance asset master page: `frontend/src/app/maintenance/assets/page.tsx`
- Fleet frontend service: `frontend/src/services/fleetService.ts`

## Verification Log

| Date | Check | Status | Result |
| --- | --- | --- | --- |
| 2026-07-09 | Source DOCX render with artifact-tool | Passed | `Fleet Management ERP Requirements Questionnaire.docx` rendered to 12 page images. |
| 2026-07-09 | Structured DOCX extraction | Passed | All paragraphs and six tables were extracted, including fleet scope, maintenance frequencies, and compliance frequencies. |
| 2026-07-09 | Rendered page visual contact review | Passed | Contact sheet confirmed the rendered source structure from governance through key functional requirements. |
| 2026-07-09 | Live source audit | Passed | Fleet entities, DTOs, services, controllers, frontend routes, mobile tracker, maintenance asset import/movement, reports, workflow integration, and dashboards were inspected. |
| 2026-07-09 | Source baseline and requirement traceability update | Passed | Added the 52-record questionnaire fleet baseline, explicit stated policy values, and a 20-item functional traceability matrix mapped to `FLT-*` tasks. |
| 2026-07-09 | Tracker creation | Passed | Full coverage matrix, configuration inputs, roadmap, acceptance scenarios, and code anchors created. |
| 2026-07-17 | Source questionnaire revalidation | Passed | Re-extracted 399 paragraphs and all six source tables; confirmed the 52-record baseline, stated intervals, governance actors, 20 functional requirements, and end-to-end process responses. |
| 2026-07-17 | Gap-analysis completeness review | Passed with additions | Added explicit Transport budget governance, Internal Audit pre-payment validation, driver eligibility, insurance/roadworthiness payment reconciliation, financial integration tasks, governance traceability, and two end-to-end scenarios. |
| 2026-07-17 | Live Fleet implementation re-audit | Passed | Rechecked Fleet entities, DTOs, services, controllers, workflow adapters, driver licence hard stops, compliance dispatch blockers, fuel-to-trip/cost lineage, reports, and frontend routes; baseline classifications remain materially accurate. |
| 2026-07-17 | Revalidated tracker DOCX render and visual QA | Passed | The corrected engineering tracker was regenerated and visually reviewed across all 22 pages, including 16 configuration inputs, 68 coverage requirements, 50 roadmap tasks, and 22 end-to-end scenarios. |

## Tracker Maintenance

- Start every implementation slice by selecting explicit `FLT-*` task IDs.
- Keep this tracker synchronized when code changes alter baseline coverage.
- Do not mark a task `Done` until backend, frontend, configuration, workflow, audit, tests, migrations, and smoke evidence are present.
- Prefer configuration over hard-coded policy for TDC-specific roles, intervals, workflows, thresholds, alerts, and reports.
- Do not remove requirements from the tracker. Record an approved scope decision with evidence if TDC defers or changes a requirement.
