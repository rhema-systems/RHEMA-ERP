# TDC Inventory and Stores Architecture Requirements

## Purpose and source of truth

This baseline extracts the Inventory and Stores requirements from:

`D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\test scripts\TDC ERP Architecture and Design Document (002).docx`

The document does **not** contain formally numbered `FR-INV-*`, `FR-IN-*`, `FR-ST-*`, or `FR-WH-*` requirements. Inventory and Stores is specified through Section 16.1, Figure 10, the enterprise capability model, and formal cross-module `FR-*` and `NFR-*` controls. This baseline therefore cites the document's real section and requirement identifiers and does not invent Inventory requirement IDs.

Section 18.2 states that exact approval thresholds, roles, and routing rules must be confirmed during detailed configuration. Configurable implementation choices must not be presented as fixed TDC requirements.

## Exact Inventory and Stores requirements

| TDC source | Requirement |
| --- | --- |
| Section 16.1, *Stores, Inventory, and Fixed Asset Design* | Stores, Inventory, and Fixed Assets must share item, location, custodian, valuation, approval, and ledger-posting controls. |
| Section 16.1 | Maintain item masters, reorder levels, stock locations, goods-receipt records, issue notes, discrepancy records, stock-taking evidence, fixed-asset schedules, asset codes, custodians, acquisition values, asset movements, disposals, verification results, and reconciliation outputs. |
| Section 16.1 | Control goods receipt and inventory updates through the approved purchase order, waybill, inspection, supervisor approval, and relevant invoice evidence. |
| Section 16.1 | Inventory issues and asset movements must update stock, asset, and ledger records through approved workflows and audit trails. |
| Figure 10, *Procurement and Inventory Workflow* | The governed route is department requisition, budget availability check, HOD approval, procurement review, purchase order or contract, supplier fulfilment, goods receipt and inspection, inventory update, three-way match, payment-voucher linkage, and reports/audit trail. |
| Figure 10 | Cross-cutting controls are requisition approval, commitment creation, GRN, inspection checklist, stock ledger, three-way matching, and audit trail. |
| Section 18.1 | Stores workflows control stock replenishment, goods receipt, issue notes, discrepancies, ledger updates, and stock reporting. |
| Section 19.1 | Stores owns stock review, replenishment initiation, goods receipt, issue notes, discrepancy escalation, and stock records. |
| Section 20, Enterprise Capability Model | Inventory and Stores covers item master, receipts, inspections, issues, transfers, counts, reorder alerts, valuation, returns, and disposals. |
| Section 22.3, *Procurement and Inventory* | Use approved supplier, item, warehouse, and contract master data. Route receipts, issues, transfers, and adjustments through workflows and apply approval limits and segregation of duties. |
| Section 22.3 | Match purchase orders, goods receipts, invoices, and stock movements. Maintain inventory valuation and stock-count controls. Retain audit records for inventory adjustments. |
| Section 19.3 | Attach scanned or generated waybills, issue notes, GRNs, invoices, approvals, and audit evidence to the relevant ERP transaction with version, access, retention, and audit controls. |
| Section 21 | Success includes stronger three-way matching and segregation of duties, better inventory and asset reconciliation, and reduced repeat audit findings. |
| Section 22.4.10, *Compliance, Risk, and Control Assurance Requirements* | Inventory and asset reconciliation evidence must be retained with reviewer sign-off, an unresolved-items log, and resolution evidence. |

## Required Inventory and Stores lifecycle

The document requires the following coherent lifecycle:

1. A governed business demand originates from an approved requisition or another authorized request.
2. Budget, requester, department, item specification, supporting evidence, and approval routing are validated upstream.
3. An approved purchase order or contract creates the budget commitment before receipt.
4. The supplier delivers against the approved source and provides delivery documentation.
5. Stores records the goods receipt against the purchase order and selected approved warehouse/location.
6. An authorized reviewer records inspection results and resolves failed inspection or delivery discrepancies through correction and re-approval.
7. Only the governed received/accepted outcome updates the stock ledger and inventory valuation.
8. Issues, transfers, returns, counts, adjustments, write-offs, and disposals update stock only through their approved workflows.
9. Capital or fixed-asset-eligible issues create or update the governed fixed-asset record and retain source lineage.
10. The purchase order, GRN/certificate, VAT invoice, and stock movement are reconciled before payment processing unless an approved exception applies.
11. Inventory, valuation, reconciliation, exception, workflow, audit, and management reports are produced from governed records.

Figure 10 places inventory update after receipt/inspection and before three-way matching/payment. The document must not be interpreted as requiring a supplier invoice before operational stock can be updated from an approved, inspected receipt.

## Formal cross-module functional controls

| Requirement | Inventory and Stores application |
| --- | --- |
| `FR-PR-006` Goods Receipt or Certificate | Record a GRN, service-completion certificate, work certificate, or project certificate before invoice matching. |
| `FR-PR-007` Three-Way Matching | Match the purchase order, GRN/certificate, and VAT invoice before payment processing. |
| `FR-AP-005` Three-Way Matching | Enforce the same match unless an approved exception applies. |
| `FR-BG-008` Budget Availability Check | Check budget at requisition, PO, contract, invoice, and payment stages. |
| `FR-BG-009` Commitment Accounting | Create the commitment when the approved PO or contract is issued, not when goods are received. |
| `FR-BG-010` Obligations and Actuals | Convert commitments to obligations or actual expenditure through invoice, certificate, and payment processing without duplicate exposure. |
| `FR-FA-001` Asset Register | Retain asset number, description, class, location, custodian, acquisition data, supplier, cost, funding source, department, useful life, depreciation, value, and status. |
| `FR-FA-002` Asset Capitalization | Create an asset from approved procurement, stores issue, invoice, or manual capitalization with supporting documents. |
| `FR-FA-003` to `FR-FA-010` | Control asset classification, depreciation, posting, transfer, verification, disposal, impairment/write-off, and ledger reconciliation. |
| `FR-FA-011` Fixed Asset Reports | Provide asset listing, depreciation, movement, disposal, verification exception, transfer, and asset-to-ledger reconciliation reports. |
| `FR-RP-003` Management Reporting | Include inventory information in management and Board reporting packs. |
| `FR-RP-006` to `FR-RP-012` | Provide report filters, drill-down, online viewing, Excel/PDF export, templates, scheduling, role-based security, and authorized ad hoc reporting. |

## Formal non-functional controls

| Requirement family | Required behaviour |
| --- | --- |
| `NFR-SEC-001` to `NFR-SEC-005` | Role-based access, least privilege, segregation of duties, secure authentication, sensitive-data restrictions, and encryption. |
| `NFR-AUD-001` to `NFR-AUD-003` | Tamper-evident audit history for creation, change, approval, rejection, posting, reversal, cancellation, master-data changes, workflow changes, and reprocessing. Each record includes user, time, action, values where applicable, reference, status, and reason. |
| `NFR-REL-001` to `NFR-REL-003` | Prevent duplicate or missing postings, preserve transactional integrity on failure, and return meaningful corrective error messages. |
| `NFR-DAT-001` to `NFR-DAT-004` | Enforce required fields, active-state and duplicate checks, approval/reconciliation rules, referential consistency, and exception reports. |
| `NFR-US-001` to `NFR-US-003` | Provide guided workflows, clear validation/errors, actionable work queues, and search/filter/sort/drill-down/export. |
| `NFR-INT-001` to `NFR-INT-003` | Use secure, traceable imports/exports; retain accepted, rejected, corrected, and retried records; prevent duplicate integration postings; reconcile totals. |
| `NFR-BCK-001` and `NFR-BCK-002` | Back up application data, attachments, workflows, reports, and audit logs and verify recovery procedures. |
| `NFR-MNT-001` and `NFR-MNT-002` | Maintain master data, workflows, validation, roles, and reports through governed configuration rather than direct database changes; retain change/release records. |
| `NFR-CMP-001` and `NFR-CMP-002` | Support applicable policy, procurement, statutory, audit, tax, financial-control, and retention obligations. |
| `NFR-RPT-001` and `NFR-RPT-002` | Provide standard and ad hoc operational, reconciliation, audit, and management reports with governed parameters and Excel/PDF export. |

### Document malware-security boundary

The architecture document requires secure, access-controlled, versioned, auditable document attachment and retention, but it does not prescribe an antivirus product, a status named `Clean`/`AV Clean`, or an Inventory approval rule based on that exact status. In the current implementation, malware scanning is a shared DMS/platform security control. The platform may quarantine or reject unsafe uploads independently of the Inventory workflow. Requiring a particular scan-status label as a business-stage gate is configuration/implementation behaviour, not a fixed TDC Inventory requirement, and end users must not enter scanner identifiers, checksums, or DMS record IDs.

## Explicit responsibilities

| Role or function named by TDC | Responsibility |
| --- | --- |
| Stores | Stock review, replenishment initiation, goods receipt, issue notes, discrepancy escalation, and stock records. |
| Procurement and suppliers | Purchase-order processing, delivery documentation, and supplier-side fulfilment. |
| Supervisor or control owner | Configured review and approval of governed receipts, discrepancies, movements, and exceptions. |
| Finance and General Ledger | Posting, reconciliation, ledger reporting, period close, and financial-control oversight. |
| Accounts Payable/Expenditure | Invoice capture, three-way matching, payment-voucher preparation, and payment routing. |
| Internal Audit | Vouching, compliance checks, exception review, and control assurance where required. |
| Maintenance | Work-order material requirements/usage and cost capture integrated with Inventory, Procurement, Finance, and Document Management. Exact reservation, consume, return, and adjustment mechanics are implementation/configuration choices. |
| System/Security administration | Configuration and access administration without business-transaction approval authority. |

TDC does not define exact Stores job titles, user names, thresholds, or a complete approval matrix in the architecture document. Those are configuration decisions subject to Section 18.2 and must be represented by controlled roles and workflow configuration rather than hard-coded user identities.

## Stock disposal versus fixed-asset disposal

The source was rechecked on **12 September 2026**: **Section16.1** supplies shared item, location, valuation, approval, ledger and audit controls; **Section18.2** leaves precise thresholds, roles and routing to configuration; **Section20.1** includes disposals in Inventory and Stores. These passages do not prescribe a universal three-person committee or separate Internal Audit step for every ordinary stock disposal.

**FR-FA-008 explicitly requires the Board of Survey/procurement-disposal controls for fixed assets.** That is a separate owner and requirement, not authority to impose an unconditional fixed-asset committee on ordinary stock. The current Inventory Disposal screen accepts ordinary `StockItem` records only; fixed assets must use their governed Fixed Asset disposal process.

The agreed implementation uses the configured shared approval workflow when active. When none is active, it records a server-controlled no-approval decision and permits authorized continuation without inventing a reviewer. This direct-mode behavior is an implementation/configuration decision, not a claim that the architecture waives legal, stock, Finance, tenant, security or audit controls. Retained in-flight approvals and historical decisions must remain intact.

Supporting files are optional in the current no-workflow stock-disposal path; the active approval path requires evidence. Any attached file still belongs to central DMS security, versioning, access and audit rules. Final stock and Finance posting remains transactional and duplicate-resistant. UAT must distinguish estimated draft value from authoritative posted value.

Acceptance is recorded in **UAT-INV-018** and the customer walkthrough's **B23**. As of this update, implementation/testing is in progress and live disposal has **not** been verified; `ERR_BLOCKED_BY_CLIENT` is the current browser blocker. Do not infer a pass from the design or code changes.

## Required exception routes

Figure 10 explicitly requires these correction paths:

| Exception | Required route |
| --- | --- |
| Insufficient budget | Correction and escalation. |
| Incomplete specification | Clarification and re-approval. |
| Supplier issue | Resolution or supplier change. |
| Delivery discrepancy | Investigation and re-approval. |
| Failed inspection | Investigation and re-approval. |
| Stock variance | Governed adjustment or write-off. |
| Obsolete or damaged stock | Governed adjustment or write-off. |

Section 18 requires exceptions to enter a correction/escalation/rework route with retained evidence and re-approval. They must not be silently bypassed, and implementation-specific policy metadata must not become an undocumented hard stop.

## Requirements-to-UAT evidence matrix

| TDC requirement area | Positive UAT evidence | Negative/control UAT evidence |
| --- | --- | --- |
| Approved master data | Active item, warehouse/location, supplier, and source records are selectable and retained on the transaction. | Inactive, unauthorized, invalid, or cross-scope masters are rejected without mutation. |
| Replenishment and reorder | Reorder level/alert leads to a governed replenishment request. | Duplicate execution does not create duplicate demand or movement. |
| PO receipt | Receipt retains PO, supplier, delivery, warehouse, location, quantities, user, and timestamp. | Missing/invalid PO, unauthorized warehouse, over-receipt, or self-conflicting action is rejected. |
| Waybill and receipt evidence | Governed DMS record is linked, versioned, access-controlled, and auditable. | Missing mandatory evidence blocks only the stage for which it is configured; unauthorized access is denied. |
| Inspection | Independent reviewer records accepted/rejected/discrepant outcome and supporting evidence. | Same-actor approval, invalid quantities, or unresolved failed inspection cannot update available stock. |
| Stock posting | Approved accepted quantity updates the stock ledger and valuation once. | Retry or partial failure does not duplicate movement, journal, or audit records. |
| Issue | Approved issue reduces stock once and generates the issue note and cost/accounting lineage. | Self-approval, insufficient stock, invalid location, or repeated issue is rejected without mutation. |
| Maintenance material linkage | Work-order material usage, Inventory movements, and work-order cost capture remain linked and reconcilable. | Failure leaves no partial work-order/stock update; controlled retry does not duplicate movement or cost. Exact reservation/consume/return mechanics remain an extension. |
| Fixed-asset issue | Eligible issue creates one asset with cost, source, location/custodian, classification, and audit lineage. | Retry does not duplicate the asset; missing controlled asset data blocks capitalization. |
| Return | Approved return restores the governed quantity/value and reverses related allocation/accounting/asset custody as applicable. | Excess or duplicate return is rejected. |
| Transfer | Approved dispatch reduces the source; governed receipt increases the destination; discrepancy remains open until resolved. | Same warehouse, unauthorized actor/location, duplicate dispatch/receipt, or unresolved close is rejected. |
| Count and variance | Count evidence compares physical to system quantity and approved variance posts once. | Unapproved variance or direct stock editing is impossible. |
| Damage, obsolescence, write-off, stock disposal | Applicable evidence, reason, configured approval or truthful no-approval decision, valuation impact and audit trail are retained. | Active-route maker/checker conflicts, invalid stock/location/access, duplicate posting or incomplete Finance execution are rejected; fixed assets cannot bypass their separate disposal owner. |
| Valuation and ledger | Movement register, stock balance, valuation and ledger postings reconcile by period and location. | Unbalanced, missing, duplicate, or unexplained postings surface as exceptions. |
| Three-way match and AP | PO, GRN/certificate, inspected quantity, VAT invoice, and payment voucher reconcile before payment. | Duplicate invoice, excess invoice, missing receipt/certificate, or mismatch blocks payment unless an approved exception exists. |
| Reporting | Online inventory, movement, receipt, issue, transfer, count/variance, valuation, reconciliation, exception, and audit views drill to source and export correctly. | Role and data scope prevent unauthorized visibility/export. |
| Audit/security | Every lifecycle action identifies actor, timestamp, outcome, reason, evidence, approval, and source reference. | Unauthorized and rejected actions create no stock, asset, journal, commitment, or business-state mutation. |
| Recovery/idempotency | Controlled retry after a failure completes or safely reports the existing result. | Duplicate submission never creates duplicate stock, asset, journal, document, or audit business events. |

## Implementation extensions not mandated by TDC

The following may be useful product controls, but the architecture document does not prescribe them. They must be described as configured or implementation extensions, not as exact TDC requirements:

- MRN: the document contains no `MRN` reference.
- An Inventory workflow gate tied to an exact antivirus status such as `Clean` or `AV Clean`. Malware scanning itself is a shared DMS/platform security control, not an optional Inventory business control.
- Exact accepted, rejected, damaged, and short quantity buckets.
- Exact GRN/MRN numbering, signature count, or requirement for two distinct signatures.
- FIFO, weighted-average, standard-cost, or any other inventory costing method.
- Negative-stock policy.
- Exact item-code, UOM, category, lot, batch, serial-number, or expiry-field rules.
- Reorder formula, minimum/maximum calculation, lead-time calculation, or replenishment quantity.
- Stock-count frequency, tolerance, blind-count method, recount route, or count-team composition.
- Exact approval paths for inventory transfer, return, adjustment, write-off, and stock disposal. Fixed-asset disposal is separately governed by `FR-FA-008`, including the Board of Survey process; only its detailed roles, limits, and routing remain configurable.
- Named role codes such as `TDC_STORES_OFFICER` or `TDC_STORES_MANAGER`.
- Warehouse-responsibility assignment mechanics.
- A named SOD pair such as stock issuer versus adjustment approver. General SOD is mandatory; exact conflicts are configurable.
- Mandatory invoice receipt before inspected stock is updated. Figure 10 places stock update before three-way match and payment.
- Exact journal accounts, debit/credit mappings, landed-cost allocation method, or valuation accounts.
- Report names beyond the required inventory, valuation, reconciliation, exception, audit, operational, and management reporting capabilities.

Extensions must still comply with the common role, workflow, DMS, audit, reliability, reporting, and data-integrity controls. They must not introduce hidden identifiers, technical-only input fields, or over-restrictive gates that the architecture document does not require.

## Acceptance boundary

Architecture compliance is demonstrated only when the complete Inventory and Stores lifecycle can be executed with:

- governed source and master-data lineage;
- maker/checker and authorized-role enforcement;
- controlled receipt, inspection, movement, valuation, asset, Finance/AP, and document integration;
- correction paths for every documented exception;
- transaction-safe, duplicate-resistant posting and retry behaviour;
- reconciliation, reviewer sign-off, report drill-down, export, audit, and evidence retention; and
- production-target database integration evidence for stock, valuation, asset, journal, document, workflow, and audit persistence (SQL Server for the current implementation).

Features outside this boundary may be retained as extensions, but their presence does not replace evidence for the TDC requirements above.
