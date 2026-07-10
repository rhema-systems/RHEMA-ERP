# TDC Procurement and Inventory Gap Implementation Tracker

Last updated: 2026-07-04

## Purpose

This tracker is the delivery ledger for closing the gaps between the current ERP implementation and the requirements in `Procurement ERP.docx`, completed by the Head of Procurement of TDC Ghana Ltd.

The source document covers more than purchasing. It defines the complete procurement and inventory operating model: governance, annual planning, requisitions, sourcing, tendering, supplier management, frameworks, contracts, purchase orders, receiving, accounts payable matching, stores operations, reporting, audit, integrations, migration, training, and go-live success measures.

## Source Of Truth And Boundaries

- Business source: `C:\Users\micha\Downloads\Questionnairs and SOPS\Questionnairs and SOPS\Procurement\Procurement ERP.docx`
- Legal references stated by TDC: Act 663, Act 914, Act 1139, L.I. 2516, Act 921, PPA/GHANEPS obligations, and TDC internal procurement policy.
- Current implementation evidence: live entities, DTOs, services, controllers, workflow adapters, migrations, frontend routes, and existing repository trackers.
- This tracker does not independently certify the legal interpretation of the stated Acts or thresholds. TDC Procurement, Legal, Finance, Internal Audit, and the relevant PPA authority must approve the final policy configuration.
- The tracker covers both Procurement and Inventory because TDC treats requisition-to-payment, stores, stock valuation, disposal, and audit as one controlled lifecycle.

## Delivery Rule

A slice may be marked `Done` only when all applicable parts are complete:

1. Backend domain model and business rules.
2. Database migration and data backfill.
3. API endpoints and authorization.
4. Frontend routes, controls, validation, and status visibility.
5. Notifications, audit events, and reports where required.
6. Automated tests for happy paths, hard stops, overrides, SOD, and tenant isolation.
7. Migration applied to the test database.
8. Browser and API smoke verification.
9. Tracker evidence updated with commands, routes, and test results.

No requirement is complete merely because a field, entity, page, or configurable workflow exists. TDC requires enforced end-to-end behavior.

## Status Legend

| Status | Meaning |
| --- | --- |
| Verified baseline | Current implementation has material capability confirmed in source; final TDC acceptance is still required. |
| Partial | Useful capability exists, but required controls or workflow steps are incomplete or bypassable. |
| Gap | Required capability or enforcement is absent. |
| Configuration pending | Implementation can proceed using configurable, effective-dated policy fields; approved values are required before the TDC profile is published for UAT or production. |
| Not started | Approved implementation task has not started. |
| In progress | Implementation is actively being changed and is not yet acceptance-ready. |
| Done | Full delivery rule above has passed and evidence is recorded. |
| Blocked | Work cannot safely continue until a named dependency or policy decision is resolved. |

## Honest Readiness Statement

The requirements are sufficiently understood to design and implement the full solution. The decisions in `DEC-*` are mostly configuration inputs, not implementation blockers. Development should proceed with effective-dated settings, draft and published policy versions, configurable workflow stages, role mappings, evidence rules, and safe validation defaults.

Approved TDC values are required before the corresponding policy profile, workflow, integration mapping, or cutover plan is published for UAT or production. The document contains several incomplete or competing threshold statements, so those values must not be hard-coded or silently assumed.

The current ERP has broad transaction coverage. The main delivery risk is not missing screens; it is bypassable controls between screens. The implementation must therefore prioritize the policy engine, hard stops, SOD, source-document lineage, matching, and audit before adding more presentation features.

## Configurable Business Decisions And Release Gates

These decisions do not stop implementation unless a task depends on a specific external file or service. The system should support them through configuration. An unresolved value blocks only publication of the affected TDC configuration to UAT or production.

| ID | Priority | Decision class | Configuration or confirmation required | Implementation treatment and release effect | Owner |
| --- | --- | --- | --- | --- | --- |
| DEC-001 | P0 | Configuration input | Confirm the legally applicable procurement method thresholds for Goods, Works, Technical Services, and Consultancy Services. | Build an effective-dated method-threshold table now. Seed draft values from the questionnaire, clearly marked unapproved. Final confirmation is required before publishing the TDC policy profile. | TDC Procurement + Legal/PPA |
| DEC-002 | P0 | Configuration input | Confirm the approval authority matrix and whether the stated GHS 800,000 / 3,300,000 / 8,000,000 / 54,000,000 limits are current and inclusive at boundaries. | Build configurable lower/upper bounds, boundary inclusivity, currency, authority, escalation, and effective dates. Final values gate threshold-routing UAT. | TDC Procurement + Legal/PPA |
| DEC-003 | P0 | Workflow configuration | Confirm the exact approval sequence for every PR. | Use the existing workflow designer plus policy-selected workflow definitions. Department, Procurement, Finance, MD, and other stages remain configurable. Final sequence gates PR workflow UAT, not development. | MD + Procurement + Finance |
| DEC-004 | P0 | Workflow and role configuration | Confirm which transactions require ETC, Board, Central Tender Review Committee, PPA, Legal, Finance, and Internal Audit approval or observation. | Build configurable authority stages, committee roles, quorum, observer, evidence, escalation, and amount/category conditions. Final mappings gate authority-routing UAT. | TDC Procurement + Legal/Internal Audit |
| DEC-005 | P0 | Configuration input | Confirm petty-purchase limits and when evaluation may be waived. | Build configurable petty thresholds, waiver eligibility, required justification/evidence, approver, and expiry. Unapproved waiver rules cannot be published. | TDC Procurement + Finance |
| DEC-006 | P0 | Workflow and evidence configuration | Confirm restricted tender and single-source prerequisites, approval authorities, post-award filing, and mandatory PPA references. | Build configurable exception workflows and evidence checklists. Final statutory checklist gates restricted/single-source UAT. | TDC Procurement + Legal/PPA |
| DEC-007 | P1 | Finance configuration | Define supplier registration fees, payment methods, receipt numbering, exemptions, refunds, and renewal rules. | Build fee schedules, effective dates, tax, payment channels, exemption rules, numbering, refund, and renewal settings. Final fee setup gates payment acceptance testing. | Procurement + Finance |
| DEC-008 | P1 | Document and signature configuration | Confirm signature policy: electronic signature, uploaded manual signature evidence, or both, including who may sign each document. | Support per-document signature mode, required signatory roles, signing order, evidence, and verification. Final legal modes gate document-signing UAT. | Legal + ICT + Procurement |
| DEC-009 | P1 | External mapping configuration | Approve GHANEPS phase-one files/templates, reference fields, submission frequencies, and reconciliation ownership. | Build a configurable export/import profile and integration queue now. Exact schemas and acknowledgements gate only the GHANEPS mapping and reconciliation acceptance. | Procurement + ICT/PPA |
| DEC-010 | P1 | Inventory policy configuration | Confirm negative-stock policy and whether any controlled emergency override is permitted. | Build a default hard stop with optional, separately permissioned exception workflow. Final override policy gates inventory-control UAT. | Stores + Finance + Internal Audit |
| DEC-011 | P1 | Risk policy configuration | Confirm supplier AVL annual review process, risk scoring bands, concentration limits, and minimum acceptable performance score. | Build configurable scorecards, bands, review frequency, eligibility actions, and thresholds. Final values gate supplier-risk UAT. | Procurement + Internal Audit |
| DEC-012 | P2 | Deployment release gate | Confirm go-live cutover date, dual-running period, data ownership, and acceptance signatories. | Does not block product implementation. It gates rehearsal scheduling, production migration, cutover, and final go-live approval. | Steering Committee |

## Current Coverage And Gap Matrix

### Governance, Policy, And Controls

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| GOV-001 | Partial | Departments, users, locations, projects, cost centres, and warehouses exist, but TDC-specific procurement responsibilities and store assignments are not configured as one controlled operating model. |
| GOV-002 | Partial | Generic RBAC and workflows exist, but MD, HOP, Procurement Officer, ETC, Central Review, Board, Legal, Finance, Stores, Internal Audit, evaluators, observers, and requisitioners are not delivered as a tested TDC role matrix. |
| GOV-003 | Gap | No effective-dated statutory procurement policy register covering Acts, regulations, internal policy, methods, categories, thresholds, authorities, evidence, and escalation. |
| GOV-004 | Gap | No automatic procurement-method selection across petty purchase, RFQ, NCT, ICT, restricted tender, single source, QBS, and QCBS. |
| GOV-005 | Gap | No reusable hard-stop SOD engine for the six prohibited role combinations stated by TDC. |
| GOV-006 | Partial | Audit logging exists, but procurement decisions are not guaranteed immutable and the default retention is one year rather than seven years. |
| GOV-007 | Partial | Generic workflow routing exists, but statutory threshold routing and committee composition are not enforced centrally. |
| GOV-008 | Gap | No controlled exception/override register with type, justification, authority, evidence, expiry, post-award filing, and bypass-attempt alerting. |
| GOV-009 | Gap | Internal Audit read-only access across all procurement, inventory, supplier, contract, invoice, payment, and audit records is not configured and acceptance-tested. |

### Planning, Specifications, And Requisitions

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| PLN-001 | Verified baseline | Annual/quarterly procurement plans, department ownership, consolidation, budgets, revisions, schedules, workflow, publishing, and planning reports exist. |
| PLN-002 | Partial | Plan publishing exists, but PPA/GHANEPS submission reference, status, due date, acknowledgement, rejection, resubmission, and evidence are missing. |
| PLN-003 | Partial | Plan items contain specifications and justification, but TDC's structured specification template is not enforced for Goods, Works, and Services/TOR. |
| PLN-004 | Partial | PRs capture department, description, specification, justification, and amount, but the create contract does not expose the existing plan/budget linkage fields consistently. |
| PLN-005 | Gap | A PR can be submitted without an approved APP item, approved budget code, Finance confirmation, or approved exception. |
| PLN-006 | Gap | No real-time budget reservation/check at PR submission with a hard stop and controlled override. |
| PLN-007 | Partial | Workflow approval exists, but approval route selection by category, value, and authority matrix is not enforced. |
| PLN-008 | Gap | Sourcing can commence without a complete PR compliance review proving APP alignment, specification clarity, cost centre validity, and budget availability. |
| PLN-009 | Partial | Procurement calendar/schedules exist, but the TDC annual calendar and deadline reminder jobs are not configured and tested. |

### Sourcing, Tendering, Evaluation, And Award

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| SRC-001 | Partial | RFQ and tender modules exist, but there is no sourcing-case record that locks the selected method and records the rule/threshold/exception that authorized it. |
| SRC-002 | Partial | Tender types exist, but distinct statutory flows for petty purchase, RFQ, NCT, ICT, restricted tender, single source, QBS, and QCBS are incomplete. |
| SRC-003 | Partial | Supplier invitations and submissions exist, but minimum RFQ quotation count, sealed-receipt controls, late submission rules, quotation receipt log, and observer attendance are not fully enforced. |
| SRC-004 | Partial | Bid opening fields exist, but a signed opening register with attendees, declared prices, securities, late/rejected bids, and immutable opening evidence is incomplete. |
| SRC-005 | Partial | Prequalification and required documents exist, but the complete prequalification advertisement, criteria, evaluation, approval, and reusable qualified-list workflow is incomplete. |
| SRC-006 | Partial | Tender documents and revisions exist, but standard document templates, version approval, controlled issue/sale, addenda acknowledgement, and validity-extension workflow are incomplete. |
| SRC-007 | Verified baseline | Evaluator assignment, scorecards, clarifications, technical/financial scoring, templates, consolidated evaluation, reports, and QCBS calculation materially exist. |
| SRC-008 | Gap | Evaluator conflict-of-interest declaration, acceptance, committee quorum/composition, signed score sheets, secretary/chair sign-off, and remote meeting evidence are not enforced. |
| SRC-009 | Gap | Award creation does not hard-stop when evaluations, recommendations, due diligence, verification, and required approvals are incomplete. |
| SRC-010 | Gap | An evaluator can potentially participate in sole award approval; award SOD is not centrally checked. |
| SRC-011 | Gap | No complete unsuccessful-tenderer letter generation, dispatch, acknowledgement, and award-notification register. |
| SRC-012 | Gap | Tender security return/release workflow is not complete. |
| SRC-013 | Gap | GHANEPS publication, tender reference, event export, acknowledgement, award notification, and submission reconciliation are missing. |

### Supplier, Risk, And Performance

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| SUP-001 | Verified baseline | Digital business-partner registration, documents, licences, approval, portal users, categories, and supplier profile management materially exist. |
| SUP-002 | Partial | TIN, VAT, PPA, GRA clearance, incorporation, bank confirmation, Works classification, trade certificates, and introductory-letter requirements are configurable only indirectly and are not delivered as a TDC mandatory evidence pack. |
| SUP-003 | Partial | Document/license expiry and status signals exist, but scheduled supplier notifications, renewal escalation, and automatic eligibility suspension need completion. |
| SUP-004 | Partial | Approved, active, and blacklist checks exist on some paths, but the full supplier validation service is not consistently invoked before RFQ invitation, award, contract, manual PO, and call-off PO. |
| SUP-005 | Gap | Supplier registration fee/payment, exemption, receipt generation, and financial reconciliation are missing. |
| SUP-006 | Partial | Blacklist and performance data exist, but external PPA debarment evidence, sanctions, tax clearance, financial stability, reputation, and annual reassessment workflow are incomplete. |
| SUP-007 | Partial | Supplier reporting exists, but concentration exposure, single-source dependency, spend share, and risk limits are not enforced. |
| SUP-008 | Partial | Supplier performance scoring exists, but automatic measures from promised/actual delivery, GRN rejection, PO pricing, responsiveness, complaints, and contract close are incomplete. |
| SUP-009 | Gap | Formal annual AVL review, approval, publication, expiry, suspension, reinstatement, and historical snapshots are incomplete. |

### Frameworks, Contracts, Purchase Orders, And Commitments

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| CON-001 | Gap | No dedicated framework agreement register with suppliers, categories, ceilings, validity, price lists, call-off rules, extensions, and status. |
| CON-002 | Partial | Blanket/contract PO fields exist, but call-off linkage, spend-balance deduction, price-list validation, expiry hard stop, and call-off approval are not implemented end to end. |
| CON-003 | Verified baseline | Tender-linked contracts, values, retention, dates, scope, penalties, signatures, milestones, amendments, documents, activation, completion, suspension, and termination materially exist. |
| CON-004 | Partial | Structured KPI/SLA measures, spend-to-date, milestone evidence, payment status, renewal pipeline, automated penalty prompts, and performance dashboard are incomplete. |
| CON-005 | Gap | Works-specific initial takeover, final takeover, defects-liability period, completion/acceptance certificates, dispute resolution, and warranty release workflow are incomplete. |
| CON-006 | Gap | Contract approval does not enforce Legal/Audit/oversight evidence or statutory authority before activation. |
| PO-001 | Gap | Manual PO creation is possible without an approved PR, sourcing case, award, framework call-off, contract, or approved exception. |
| PO-002 | Partial | PO source fields exist, but source lineage is not mandatory and source state is not revalidated at submission/approval. |
| PO-003 | Gap | PO approval does not hard-stop on missing budget commitment, evaluation evidence, supplier eligibility, award approval, GHANEPS evidence, or required contract. |
| PO-004 | Gap | Award-to-PO supports self auto-approval, violating TDC SOD. |
| PO-005 | Partial | PO revisions exist, but quantity, price, delivery, source, and supplier amendments do not consistently force documented reapproval and financial commitment adjustment. |
| PO-006 | Partial | PO document generation exists in parts, but approved electronic/manual signature evidence and dispatch acknowledgement need completion. |
| PO-007 | Partial | Open PO status and receipts exist, but overdue, promised-date, uncommitted, under/over-received, and closeout exception monitoring need a complete dashboard/report. |

### Receiving, Inspection, Invoice Matching, And Payment

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| RCV-001 | Verified baseline | PO receipts, GRN generation, warehouse posting, quantities, inspection completion, and inventory valuation integration materially exist. |
| RCV-002 | Partial | Accepted/rejected/pending quantities exist in parts, but formal rejection note, supplier acknowledgement, return/replacement, quarantine/quality hold, and closure workflow are incomplete. |
| RCV-003 | Partial | GRN supports PO linkage, but receipt without an approved PO or approved exception must be prohibited consistently across all receiving endpoints. |
| RCV-004 | Gap | PO creator versus goods-receipt confirmer SOD is not enforced. |
| AP-001 | Verified baseline | Two-way and three-way matching calculations exist with price/quantity discrepancies. |
| AP-002 | Gap | Unmatched or exception invoices can still be submitted and approved because matching currently warns rather than blocks. |
| AP-003 | Gap | Payment allocation and payment batch processing do not require a successful three-way match or approved exception. |
| AP-004 | Gap | Invoice processor versus payment approver SOD is not enforced. |
| AP-005 | Partial | GL and AP posting exist, but procurement commitment, invoice, payment, reversal, retention, and contract milestone reconciliation need end-to-end acceptance testing. |
| AP-006 | Gap | Three-way-match exception workflow with tolerance, reason, evidence, dual approval, expiry, and audit report is incomplete. |

### Inventory And Warehouse Operations

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| INV-001 | Verified baseline | Central item master, category, UOM, conversion, stock code, costing, reorder parameters, projects, warehouses, and locations materially exist. |
| INV-002 | Partial | Item barcode fields appear in DTO/UI and item UOM, but primary item barcode/QR persistence and unique lookup are inconsistent. |
| INV-003 | Gap | Barcode/QR label generation and handheld/mobile scanning for GRN, issue, return, transfer, and counts are not complete. |
| INV-004 | Verified baseline | Serial, lot, batch, manufacture/expiry fields and FIFO/WAC structures materially exist. |
| INV-005 | Partial | Tracking fields exist, but category-based mandatory capture and FIFO/expiry enforcement at receipt, issue, return, and transfer are incomplete. |
| INV-006 | Verified baseline | Multi-warehouse, zone/rack/bin locations, bin stock, transfers, in-transit status, and receiving confirmation materially exist. |
| INV-007 | Gap | Store officer access is not restricted to assigned warehouses/locations. |
| INV-008 | Partial | Bin/location records exist, but directed put-away, picking, replenishment, and location-capacity rules are incomplete. |
| INV-009 | Verified baseline | Store requisitions, issues, project/cost-centre references, fulfillment, returns, transfers, and stock movements materially exist. |
| INV-010 | Gap | Issue without approved requisition must be hard-stopped across every issue endpoint; issuer versus adjustment approver SOD is not enforced. |
| INV-011 | Partial | Return and adjustment records exist, but standard Store Issue Voucher, Store Return Voucher, damage/expiry/loss reasons, evidence, and approval rules need completion. |
| INV-012 | Verified baseline | Physical/cycle counts, freeze, count sheets, recount, variance calculation, rejection, approval, and posting materially exist. |
| INV-013 | Partial | ABC classes exist, but automatic ABC-based cycle schedules, dual approval, Internal Audit participation, and year-end cut-off controls are incomplete. |
| INV-014 | Partial | Min/max, reorder level, reorder quantity, safety stock, lead time, and suggestions exist, but demand-based calculation, alerts, ownership, and approval are incomplete. |
| INV-015 | Gap | Negative-stock prevention is not delivered as one enforced policy across issues, adjustments, transfers, work orders, and project consumption. |
| INV-016 | Partial | Project requisitions and allocated quantities exist, but explicit project reservation, release, expiry, partial fulfillment, and requesting-department notification are incomplete. |
| INV-017 | Verified baseline | FIFO, WAC, standard cost, landed-cost allocation, layers, movements, and valuation reports materially exist. |
| INV-018 | Partial | Real-time Finance GL reconciliation, cut-off controls, and exception reconciliation report are incomplete. |
| INV-019 | Gap | Stock ageing bands, slow-moving/non-moving analysis, stockout analytics, and disposal triggers are incomplete. |
| INV-020 | Gap | No complete inventory disposal lifecycle covering identification, audit verification, valuation, committee scheduling, approval, auction/write-off/donation/destruction, evidence, proceeds, and automatic stock write-off. |

### Reporting, Audit, Integration, Migration, And Readiness

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| RPT-001 | Partial | Planning and supplier reports exist, but the full statutory procurement report catalogue is incomplete. |
| RPT-002 | Partial | Stock movement, valuation, reorder, count, and expiry data exist, but ageing and slow/non-moving reports are incomplete. |
| RPT-003 | Gap | TDC management dashboard covering spend, open PO, contract utilization/expiry, stock value, cycle time, service level, and supplier risk is incomplete. |
| RPT-004 | Gap | Audit report pack for opening registers, committee sign-off, due diligence, three-way exceptions, payment status, adjustments, and disposals is incomplete. |
| RPT-005 | Partial | Excel/PDF generation exists in selected areas, but configurable PPA/GHANEPS, Finance, Audit, and Board templates are missing. |
| AUD-001 | Partial | Generic audit logs and inventory movement immutability exist, but procurement-wide immutable event coverage is incomplete. |
| AUD-002 | Gap | Seven-year minimum retention is not the enforced default and audit deletion protection is insufficient. |
| INT-001 | Partial | Finance, HR identity, projects, maintenance, document storage, and internal module integration exist, but the full interface contract is undocumented. |
| INT-002 | Gap | GHANEPS manual export/import and API-ready integration are missing. |
| INT-003 | Gap | Scanner/device integration contract and offline queue behavior for stores are missing. |
| INT-004 | Gap | No agreed Interface Control Document covering direction, frequency, ownership, retries, reconciliation, security, and failure handling. |
| MIG-001 | Gap | No controlled migration suite for supplier records, AVL, open POs, active contracts, stock balances, WAC history, contract payments, and digital tender archive. |
| MIG-002 | Gap | No cleansing workbench for duplicate item codes, incomplete TIN/documents, UOM conflicts, contract gaps, and Stores-versus-Finance variances. |
| MIG-003 | Gap | No cutover reconciliation, signed opening balances, migration exception register, or rollback criteria. |
| CHG-001 | Gap | TDC role-based training materials, supplier training, train-the-trainer plan, and competency sign-off are not prepared. |
| CHG-002 | Gap | Dual-running, phased cutover, support, incident triage, and go-live acceptance plan are not defined. |
| KPI-001 | Gap | Success measures are not implemented as baseline/target KPIs with auditable calculation definitions. |

## Implementation Roadmap

### Phase 0 - Policy Confirmation And Architecture

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0001 | P0 | Not started | Define configuration schemas for `DEC-001` through `DEC-012`, seed draft TDC profiles, and attach approvals as they arrive. | Every decision supports draft/published state, owner, decision date, effective date, evidence, approved values, and publication protection; unresolved values do not block unrelated development. |
| TDC-0002 | P0 | Not started | Create procurement policy, category, method, authority, threshold, evidence, exception, and SOD domain model. | Model supports effective dating, currency, Goods/Works/Services/Consultancy, tenant overrides, and immutable published versions. |
| TDC-0003 | P0 | Not started | Build central procurement compliance decision service. | Given category, amount, method, source, user, and date, service returns required authority, evidence, route, hard stops, and explainable rule IDs. |
| TDC-0004 | P0 | Not started | Build reusable SOD guard service and policy administration UI. | All six TDC conflicts are blocked at API and hidden/disabled in UI with audited attempted-bypass events. |
| TDC-0005 | P0 | Not started | Seed and verify TDC roles, permissions, committees, workflow definitions, and Internal Audit read-only role. | Test users can perform only assigned duties; tenant and warehouse restrictions pass integration tests. |
| TDC-0006 | P0 | Not started | Establish procurement event/audit architecture. | Every control decision records actor, role, rule, values, source, result, old/new state, timestamp, correlation ID, and evidence links. |

### Phase 1 - APP, Specifications, Budget, And PR Hard Stops

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0101 | P0 | Not started | Add GHANEPS/PPA APP submission register and timeline. | Published plan tracks export, reference, submitter, submission date, acknowledgement, rejection, resubmission, and evidence. |
| TDC-0102 | P1 | Not started | Add standard specification/TOR templates for Goods, Works, and Services. | Template captures purpose, functional/performance, process/materials, dimensions/marking, testing, standards, deliverables, and acceptance criteria. |
| TDC-0103 | P0 | Not started | Complete PR DTO/entity/UI linkage to plan item, budget, category, cost centre, project, request type, specification template, and exception. | Required values persist, display, export, and remain auditable. |
| TDC-0104 | P0 | Not started | Enforce APP linkage or approved exception before PR submission. | API rejects unlinked PR; UI explains the blocking rule; approved emergency/exception path is traceable. |
| TDC-0105 | P0 | Not started | Enforce real-time Finance budget availability and commitment reservation. | PR cannot progress without sufficient approved budget or authorized override; concurrent requests cannot overspend. |
| TDC-0106 | P0 | Not started | Route PR approval by confirmed TDC authority matrix. | Department, Procurement, Finance, MD/authority stages and amount/category rules are deterministic and tested at boundaries. |
| TDC-0107 | P0 | Not started | Add PR compliance review and sourcing-release gate. | Sourcing is impossible until mandatory fields, APP, budget, specification, approvals, and SOD checks pass. |
| TDC-0108 | P1 | Not started | Configure annual procurement calendar and reminders. | APP, mid-year review, cycle count, year-end, renewal, and GHANEPS deadlines generate owned tasks and escalations. |

### Phase 2 - Sourcing, Tender, Evaluation, And Award Controls

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0201 | P0 | Not started | Create sourcing-case record from approved PR/plan demand. | Case stores selected method, rule version, estimated value, authority, justification, source requests, lots, and status. |
| TDC-0202 | P0 | Not started | Implement automatic method selection and controlled override. | Correct petty/RFQ/NCT/ICT/restricted/single/QBS/QCBS recommendation is produced; unauthorized method changes are blocked. |
| TDC-0203 | P1 | Not started | Complete RFQ statutory workflow. | Minimum qualified suppliers, receipt log, sealed/late rules, opening register, observers, evaluation, recommendation, approval, and LPO/contract creation work end to end. |
| TDC-0204 | P1 | Not started | Complete NCT and ICT workflows. | Advertisement, issue/sale, submission, public opening, technical/financial evaluation, authority/PPA approval, award, contract, acceptance, and records are enforced. |
| TDC-0205 | P1 | Not started | Complete restricted and single-source workflows. | Justification, Board/MD/PPA approvals, negotiation, post-award filing, and exception evidence are mandatory. |
| TDC-0206 | P1 | Not started | Complete prequalification workflow and reusable qualified lists. | Advertisement, criteria, submissions, evaluation, approval, expiry, and sourcing eligibility are queryable and auditable. |
| TDC-0207 | P1 | Not started | Add controlled tender document templates and issuance register. | Version, approval, issued recipient, fee, receipt, addendum acknowledgement, deadline extension, and validity extension are tracked. |
| TDC-0208 | P0 | Not started | Enforce committee composition, declarations, quorum, signatures, and score-sheet locking. | Evaluators cannot score before acceptance/COI declaration; submitted scores are immutable except controlled recall. |
| TDC-0209 | P0 | Not started | Add recommendation approval and award-readiness gate. | Award is blocked until all required evaluations, verification, due diligence, recommendations, authority approvals, and evidence are complete. |
| TDC-0210 | P0 | Not started | Enforce evaluator-versus-award-approver SOD. | Sole award approval by an evaluator is rejected and audited. |
| TDC-0211 | P1 | Not started | Implement successful/unsuccessful bidder communications and security returns. | Letters, dispatch channels, acknowledgements, standstill/appeal dates, and tender security release are registered. |
| TDC-0212 | P1 | Not started | Add GHANEPS tender and award event exchange. | Required events export/import with references, checksums, acknowledgements, failures, retry, and reconciliation. |

### Phase 3 - Supplier Portal, AVL, Risk, And Performance

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0301 | P1 | Not started | Configure TDC supplier registration evidence packs by Goods, Works, and Services. | Mandatory documents, classifications, validity rules, and approval steps are category-aware. |
| TDC-0302 | P1 | Not started | Implement supplier registration payment and receipt workflow. | Fee, exemption, payment status, receipt number, Finance posting/reconciliation, and renewal are auditable. |
| TDC-0303 | P0 | Not started | Centralize supplier eligibility validation. | The same validator blocks invitation, award, contract, manual PO, and call-off for noncompliant suppliers. |
| TDC-0304 | P1 | Not started | Complete due diligence and annual reassessment. | PPA debarment, tax/GRA, sanctions, bank, financial, reputation, expiry, and reviewer evidence are tracked. |
| TDC-0305 | P1 | Not started | Complete AVL lifecycle. | Annual review, approval, effective/expiry dates, suspension, reinstatement, publication, and snapshots are available. |
| TDC-0306 | P1 | Not started | Implement supplier risk and concentration engine. | Risk bands, single-source concentration, spend exposure, alerts, escalation, and award hard stops use approved policy. |
| TDC-0307 | P1 | Not started | Automate supplier performance scorecards. | PO, promised/actual delivery, GRN quality, rejection, price, response, complaint, and contract data produce explainable scores. |

### Phase 4 - Frameworks, Contracts, POs, And Commitments

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0401 | P1 | Not started | Build framework agreement register and price-list administration. | Agreement controls supplier, category, ceiling, validity, items, prices, call-off authority, extension, and documents. |
| TDC-0402 | P1 | Not started | Implement call-off PO workflow. | Call-off validates agreement, price, balance, date, supplier, threshold, source demand, and approval before issue. |
| TDC-0403 | P0 | Not started | Make approved source lineage mandatory for every PO. | PO requires approved PR + sourcing/award/contract/framework or approved exception; manual bypass is impossible. |
| TDC-0404 | P0 | Not started | Add PO pre-submit and pre-approve compliance gate. | Supplier, budget, commitment, source, evaluation, award, GHANEPS, contract, signature, and SOD checks pass. |
| TDC-0405 | P0 | Not started | Remove self auto-approval and enforce PO SOD. | Requester/creator cannot satisfy prohibited approval or receiving roles. |
| TDC-0406 | P1 | Not started | Complete PO amendment and commitment-adjustment workflow. | Changes require reason, version diff, reapproval, budget adjustment, redispatch, and supplier acknowledgement. |
| TDC-0407 | P1 | Not started | Complete contract approval and activation gate. | Legal, Audit, authority, award, GHANEPS, signature, performance security, and document evidence are mandatory as configured. |
| TDC-0408 | P1 | Not started | Add contract operations dashboard and automated penalties. | Spend, milestone, payment, retention, SLA/KPI, delay, penalty prompt, expiry, renewal, and risk are visible. |
| TDC-0409 | P2 | Not started | Add Works takeover, defects liability, warranty, dispute, and final closeout workflow. | Initial/final acceptance, defects, releases, termination, and final account evidence are controlled. |

### Phase 5 - Receiving, Inspection, Matching, And Payment

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0501 | P0 | Not started | Enforce receipt against approved PO/contract/call-off or approved exception. | Every receipt has valid source and remaining quantity/tolerance; direct phantom stock is blocked. |
| TDC-0502 | P1 | Not started | Complete inspection, quality hold, rejection note, return, replacement, and acceptance workflow. | Accepted/rejected/pending quantities and closure evidence update PO, stock, supplier performance, and AP eligibility. |
| TDC-0503 | P0 | Not started | Enforce PO creator versus goods receiver SOD. | Prohibited confirmation is rejected and audited at every receipt endpoint. |
| TDC-0504 | P0 | Not started | Make three-way matching mandatory before invoice approval. | PO-linked invoice cannot be approved unless matched within tolerance or an approved exception exists. |
| TDC-0505 | P0 | Not started | Block payment allocation/batches for unmatched invoices. | Direct allocation and batches enforce invoice state, match result, GRN approval, SOD, and exception status. |
| TDC-0506 | P0 | Not started | Enforce invoice processor versus payment approver SOD. | Same-user conflict is blocked across manual and batch payments. |
| TDC-0507 | P1 | Not started | Build three-way exception workflow and report. | Variance type, tolerance, root cause, evidence, approvals, expiry, corrective action, and audit extraction are complete. |
| TDC-0508 | P1 | Not started | Verify commitments, GL, AP, retention, reversals, and milestone reconciliation. | Automated integration tests prove balanced postings and controlled reversal paths. |

### Phase 6 - Inventory Control, Traceability, Counting, And Disposal

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0601 | P1 | Not started | Repair item barcode/QR persistence and uniqueness. | Primary/alternate/UOM barcodes persist, resolve uniquely, import/export, and survive edits. |
| TDC-0602 | P2 | Not started | Add label designer/printing and mobile scanning workspace. | GRN, issue, return, transfer, and count can scan item/location/lot/serial with manual fallback and audit. |
| TDC-0603 | P1 | Not started | Enforce category-based lot, batch, serial, manufacture, expiry, and FIFO rules. | Required tracking cannot be omitted; expired/incorrect lots and non-FIFO issue are blocked or approved as exceptions. |
| TDC-0604 | P0 | Not started | Add user-to-warehouse/location access assignments. | Stores users see and transact only assigned stores; cross-store operations require explicit authority. |
| TDC-0605 | P2 | Not started | Complete directed put-away, picking, and replenishment. | Location suggestions, capacity/status, quarantine, pick confirmation, and replenishment tasks are traceable. |
| TDC-0606 | P0 | Not started | Enforce approved store requisition before issue and issuer SOD. | Every issue has approved source, available stock, cost/project, issuer, receiver, and voucher evidence. |
| TDC-0607 | P1 | Not started | Complete issue/return/damage/write-off reason and evidence controls. | Standard vouchers, attachments, approvals, stock/GL impact, and reversals are consistent. |
| TDC-0608 | P1 | Not started | Complete transfer approval, dispatch, in-transit, receipt, discrepancy, and closure. | Both locations update only at valid states; shortages/damage and partial receipts are reconciled. |
| TDC-0609 | P1 | Not started | Complete ABC cycle-count scheduling and dual variance approval. | Schedule, freeze, blind count, recount, investigation, Stores/Finance/Audit approval, posting, and cut-off work end to end. |
| TDC-0610 | P1 | Not started | Implement one negative-stock policy across all inventory consumers. | Concurrent tests prove no unauthorized negative stock through issue, transfer, adjustment, work order, or project posting. |
| TDC-0611 | P2 | Not started | Complete project reservations and fulfillment notifications. | Reserve, partially fulfill, release, expire, substitute, and notify are visible by project and department. |
| TDC-0612 | P1 | Not started | Complete replenishment calculations and alerts. | Min/max, reorder, safety stock, lead time, demand, approval, recommendation, and PR generation are explainable. |
| TDC-0613 | P1 | Not started | Complete WAC/landed-cost/GL reconciliation and year-end cut-off. | Receipt costs, freight/duty allocation, valuation, GL balance, close/freeze, and exceptions reconcile. |
| TDC-0614 | P1 | Not started | Add ageing, slow/non-moving, stockout, and expiry analytics. | Required ageing bands and actionable disposal/replenishment recommendations are available by item/location. |
| TDC-0615 | P0 | Not started | Build inventory disposal lifecycle. | Identification, verification, valuation, committee/MD/Board approval, method, auction/proceeds or destruction evidence, and stock/GL write-off are complete. |

### Phase 7 - Reporting, Audit, And Statutory Evidence

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0701 | P1 | Not started | Deliver procurement statutory report catalogue. | APP vs actual, tender, contract, supplier performance, award notification, savings, and ETC minutes registers reconcile to source transactions. |
| TDC-0702 | P1 | Not started | Deliver inventory report catalogue. | Balance, movement, ageing, reorder, count variance, valuation/GL, slow/non-moving, expiry, and disposal reports reconcile. |
| TDC-0703 | P1 | Not started | Deliver management dashboards. | Spend/category/department, open PO, contract utilization/expiry, stock value, cycle time, service level, and supplier risk support date/location filters. |
| TDC-0704 | P1 | Not started | Deliver audit and compliance report pack. | Opening, committee sign-off, due diligence, matching exceptions, PO payment, adjustments, overrides, and disposals are exportable. |
| TDC-0705 | P1 | Not started | Add configurable online/Excel/PDF report templates. | PPA/GHANEPS, Finance, Audit, Board, monthly, quarterly, and ad hoc formats retain filters and generation metadata. |
| TDC-0706 | P0 | Not started | Enforce seven-year immutable audit retention. | Default minimum is 2,555 days; procurement records cannot be altered/deleted; legal hold and archive restore are tested. |
| TDC-0707 | P0 | Not started | Complete procurement/inventory audit event coverage. | Automated tests verify every required create/update/approve/reject/override/post/reverse/dispatch/receive action. |

### Phase 8 - Integrations, Migration, And Data Quality

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0801 | P1 | Not started | Produce Interface Control Document and integration registry. | Finance, budget, GL, AP, HR, projects, maintenance, DMS, GHANEPS, scanners, and reporting interfaces have owners and controls. |
| TDC-0802 | P0 | Not started | Verify real-time budget, commitment, GL, and AP integration. | Idempotency, retries, reconciliation, reversal, timeout, and failure alerts pass integration tests. |
| TDC-0803 | P1 | Not started | Implement GHANEPS manual exchange with API-ready queue. | Monthly/per-event files, validation, reference tracking, acknowledgement, retry, and reconciliation are operational. |
| TDC-0804 | P2 | Not started | Implement scanner/device integration and offline queue. | Devices authenticate, cache permitted masters, queue transactions, sync idempotently, and expose failures. |
| TDC-0805 | P1 | Not started | Build reusable import templates and staging/validation workbench. | Supplier, AVL, item, UOM, warehouse, user/role, contract, PO, stock, WAC, payment, and archive datasets validate before posting. |
| TDC-0806 | P1 | Not started | Implement data cleansing rules and stewardship queues. | Duplicates, invalid TIN, expired documents, UOM conflicts, contract gaps, and balance discrepancies have owners and resolution history. |
| TDC-0807 | P1 | Not started | Execute rehearsal migrations and signed reconciliation. | Row/value counts, WAC, stock, AP/GL, errors, corrections, reruns, cut-off, and rollback are evidenced. |

### Phase 9 - Training, Cutover, Operations, And Success Measures

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| TDC-0901 | P2 | Not started | Prepare role-based training and train-the-trainer materials. | Procurement, Stores, Finance, requisitioners, approvers, Audit, ICT, suppliers, and contractors pass competency checks. |
| TDC-0902 | P1 | Not started | Define dual-running, cutover, freeze, support, and rollback plan. | Owners, dates, go/no-go criteria, incident process, opening balances, and signatories are approved. |
| TDC-0903 | P1 | Not started | Implement KPI definitions and baseline capture. | Error rate, PR-to-PO days, APP linkage, stockouts, match rate, audit repeats, and GHANEPS timeliness are reproducible. |
| TDC-0904 | P1 | Not started | Build post-go-live KPI dashboard and scheduled review pack. | Targets show baseline, trend, denominator, owner, evidence, exception, and corrective action. |
| TDC-0905 | P1 | Not started | Run end-to-end UAT and statutory control simulation. | TDC signs off representative Goods, Works, Services, emergency, RFQ, NCT/ICT, receipt, payment, count, transfer, and disposal scenarios. |
| TDC-0906 | P1 | Not started | Run security, tenant, SOD, performance, backup, restore, and audit-retention acceptance. | No critical findings remain; restore and seven-year archive retrieval are demonstrated. |

## Mandatory End-To-End Acceptance Scenarios

| Scenario ID | Status | Scenario |
| --- | --- | --- |
| E2E-001 | Not started | Approved APP item -> budgeted PR -> method selection -> RFQ -> three quotations -> opening -> evaluation -> approval -> PO -> GRN -> three-way invoice -> payment -> GL. |
| E2E-002 | Not started | High-value Goods procurement routed through correct ETC/Central authority with tender, evaluation, GHANEPS references, award, contract, and PO. |
| E2E-003 | Not started | Works procurement with prequalification, NCT/ICT, contract milestones, retention, delay penalty, initial/final takeover, defects liability, and final payment. |
| E2E-004 | Not started | Consultancy procurement using QBS/QCBS with technical threshold, financial opening, combined scoring, recommendation, and approval. |
| E2E-005 | Not started | Restricted/single-source procurement with justification, PPA/Board approval, negotiation, post-award filing, and exception report. |
| E2E-006 | Not started | Emergency purchase with controlled APP/budget exception, MD/Board approval, post-award justification, and full audit trail. |
| E2E-007 | Not started | Supplier registration with mandatory documents, fee/receipt, due diligence, AVL approval, expiry suspension, renewal, and reinstatement. |
| E2E-008 | Not started | Framework agreement -> valid call-off -> spend deduction -> receipt -> invoice -> remaining balance and expiry alert. |
| E2E-009 | Not started | Rejected receipt -> rejection note -> quarantine/return/replacement -> supplier performance impact -> invoice blocked until accepted. |
| E2E-010 | Not started | Store requisition -> project reservation -> approved issue -> partial fulfillment -> return -> requester notification and project cost. |
| E2E-011 | Not started | Inter-store transfer -> approval -> dispatch -> in-transit -> partial/damaged receipt -> discrepancy resolution -> both balances reconciled. |
| E2E-012 | Not started | ABC cycle count -> freeze -> blind count -> variance -> recount -> dual approval -> adjustment -> GL/audit reconciliation. |
| E2E-013 | Not started | Obsolete/expired stock -> disposal request -> valuation -> Audit -> committee/MD/Board -> auction/destruction -> proceeds or write-off -> GL. |
| E2E-014 | Not started | SOD negative tests for all prohibited combinations and attempted bypass through direct API calls. |
| E2E-015 | Not started | Concurrent budget, stock, framework-balance, and payment operations prove no overspend, negative stock, double call-off, or duplicate payment. |
| E2E-016 | Not started | Seven-year audit archive retrieval proves unaltered history, evidence links, actor/role, approval chain, and report reproduction. |

## Success Targets From TDC

| KPI | Target | Status | Implementation note |
| --- | --- | --- | --- |
| Manual data-entry errors | More than 80% reduction within 6 months | Not started | Define baseline sampling method and error taxonomy before go-live. |
| PR-to-PO cycle time | Reduce from 15 days to under 7 days | Not started | Measure from valid PR submission to approved/dispatched PO, excluding documented supplier waiting periods only if TDC approves. |
| APP compliance | 100% linked to approved APP | Not started | Approved exception must remain separately visible and must not silently count as compliant. |
| Stockout incidents | More than 60% reduction in first year | Not started | Define item/location stockout event and critical-item weighting. |
| Three-way match rate | More than 95% | Not started | Report matched invoices separately from approved exceptions. |
| Repeat audit findings | Zero | Not started | Link findings, corrective actions, owners, due dates, and retest evidence. |
| GHANEPS submissions | 100% on time | Not started | Use statutory due date, actual acknowledgement date, and resubmission status. |

## Current Code Evidence Anchors

These anchors justify the baseline classifications; they are not proof of final acceptance:

- Procurement planning/versioning/budget: `src/ErpSystem.Core/Entities/Procurement/ProcurementPlanningEntities.cs`
- PR creation/submission/approval: `src/ErpSystem.Api/Controllers/Procurement/PurchaseRequisitionsController.cs`
- PO creation/submission/approval/receipt: `src/ErpSystem.Api/Controllers/Procurement/PurchaseOrdersController.cs`
- Tender/evaluator/clarification/revision entities: `src/ErpSystem.Core/Entities/Procurement/TenderEntities.cs`
- Evaluation and QCBS: `src/ErpSystem.Core/Services/Procurement/TenderEvaluationService.cs`
- Award and award-to-PO behavior: `src/ErpSystem.Core/Services/Procurement/TenderAwardService.cs`
- Supplier validation: `src/ErpSystem.Core/Services/Procurement/SupplierValidationService.cs`
- Contracts/milestones/amendments: `src/ErpSystem.Core/Entities/Procurement/ContractEntities.cs`
- Invoice matching: `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs`
- Vendor payments: `src/ErpSystem.Api/Services/Finance/AP/VendorPaymentService.cs`
- Inventory master/location/tracking: `src/ErpSystem.Core/Entities/Inventory/InventoryEntities.cs`
- Enhanced inventory/count/transfer structures: `src/ErpSystem.Core/Entities/Inventory/InventoryEnhancedEntities.cs`
- Inventory valuation/FIFO/WAC: `src/ErpSystem.Core/Services/Inventory/InventoryValuationService.cs`
- Data retention policy: `src/ErpSystem.Core/Entities/DataRetentionPolicy.cs`
- Existing procurement planning delivery ledger: `docs/procurement-planning-tasklist.md`

## Verification Log

| Date | Check | Status | Result |
| --- | --- | --- | --- |
| 2026-07-04 | Source DOCX render with artifact-tool | Passed | All 19 pages rendered and visually reviewed. |
| 2026-07-04 | Structured DOCX table extraction | Passed | All 17 tables were inventoried, including appendices and declaration. |
| 2026-07-04 | Live procurement/inventory source audit | Passed | Entities, DTOs, controllers, services, routes, reports, workflows, matching, payments, audit, and retention paths were checked. |
| 2026-07-04 | Threshold consistency review | Configuration pending | Multiple method/authority threshold statements are incomplete or inconsistent; implementation proceeds with effective-dated draft configuration, while publication gates are tracked as `DEC-001` and `DEC-002`. |
| 2026-07-04 | Decision-blocker classification review | Passed | Reclassified `DEC-001` through `DEC-011` as configurable inputs/workflow mappings and `DEC-012` as a deployment release gate; none blocks unrelated implementation. |
| 2026-07-04 | Tracker creation | Passed | Full coverage matrix, blocking decisions, implementation phases, acceptance scenarios, and success measures created. |

## Tracker Maintenance

- Start every implementation slice by selecting explicit `TDC-*` task IDs.
- Update task status when work starts; do not wait until the end of a large batch.
- Record migrations, tests, API routes, UI routes, and runtime evidence in the Verification Log.
- Keep `Current Coverage And Gap Matrix` synchronized when implementation changes the baseline.
- Do not remove statutory requirements from the tracker. Record an approved scope decision with evidence when TDC changes or defers a requirement.
- Do not mark a phase complete until matching backend, frontend, permissions, migration, reporting, and acceptance tests are present.
