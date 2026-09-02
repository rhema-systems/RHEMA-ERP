# TDC Procurement Architecture Compliance Evidence

## Baseline

Authoritative source: **TDC ERP Architecture and Design Document (002)**. This matrix uses the document's formal procurement requirements `FR-PR-001` through `FR-PR-012`; it does not substitute a project tracker or invent fixed thresholds and routing rules that the document leaves for configuration.

Status meanings:

- **Implemented**: the owning code path exists and has focused automated evidence.
- **Configuration**: the document requires the capability, but tenant-specific roles, thresholds or workflows must be published before UAT.
- **UAT required**: automated evidence exists, but authenticated browser/business acceptance remains required.

## Requirement matrix

| Requirement | Architecture outcome | Implemented owner/evidence | Status |
| --- | --- | --- | --- |
| `FR-PR-001` | PR captures description, specification, quantity, estimate, budget line, required date, justification and supporting documents | PR DTO/controller/service validation; approved plan-item estimate with controlled fallback; central DMS PR document catalogue/controller and draft upload/remove UI | Implemented; UAT required |
| `FR-PR-002` | Validate specification, budget, requester authority, department, category and documents before submit | PR submission and linkage controls; tenant permissions; positive amount/specification/document checks; budget reservation service | Implemented; UAT required |
| `FR-PR-003` | Configured department, budget, procurement and MD approvals where applicable | Shared workflow binding and independent approval; APP and optional authority metadata do not create undocumented hard stops | Implemented; workflow configuration required |
| `FR-PR-004` | PO generated from an approved PR/source and required for purchases | Approved PR/RFQ/tender/contract source lineage; PO submit/approve permissions and maker-checker controls | Implemented; UAT required |
| `FR-PR-005` | Commitment created when an approved PO or contract is issued | PR reserve-only lifecycle; final PO approval/contract activation formalizes; immutable commitment ledger; contract child-PO allocations avoid double count; receipt/certificate utilization is idempotent | Implemented; SQL verified |
| `FR-PR-006` | Goods receipt, service completion or work certificate exists before invoice match | PO receipt/inspection/GRN controls and QS certificate path; accepted value utilizes formal commitment | Implemented; UAT required |
| `FR-PR-007` | Three-way PO/receipt-or-certificate/VAT-invoice match before payment | Finance/AP matching owners and procurement-to-payment report; stale/foreign evidence negative SQL gate | Implemented; UAT required |
| `FR-PR-008` | Complete contract register | Contract report includes supplier, project, value, approval/start/end, retention, variations, certificates, invoices, payments and balance | Implemented; SQL verified; UAT required |
| `FR-PR-009` | Contract/project monitoring | Contract operations, milestones and Projects/QS payment-certificate integration | Implemented; configuration and UAT required |
| `FR-PR-010` | Controlled exception reason, evidence, Internal Audit vouching and MD approval | Exceptional sourcing controls plus new tenant-scoped Exception Register; no direct status override | Implemented; configured workflow/UAT required |
| `FR-PR-011` | Controlled GHANEPS exchange | Manual APP/GHANEPS attempts, system-calculated SHA-256, acknowledgement, retry/reconciliation and audit; no user-entered internal IDs | Implemented; external/manual UAT required |
| `FR-PR-012` | Procurement reports | 13 shared procurement reports, including the formal PR/PO/commitment/contract/certificate/exception/supplier-performance/procurement-to-payment registers | Implemented; SQL verified; export UAT required |

## Cross-cutting controls

| Control | Evidence |
| --- | --- |
| Tenant isolation | Explicit tenant predicates on report sources; tenant-scoped workflow, DMS, reservation, commitment and source-lineage services; foreign-tenant negative tests |
| Segregation of duties | Requester/creator cannot approve the same PR/PO/award; PO creator cannot confirm governed receipt; tenant-configured controlled roles |
| Budget lifecycle | Available = allocated - reserved - formally committed - utilized; serializable reservation/formalization/utilization and immutable ledger |
| Controlled master data | Departments, cost centres, budgets, categories, methods, roles, suppliers, items, warehouses and workflows use owning master data |
| Documents | Central DMS owns storage, access, retention and versions; procurement retains governed references and lineage |
| Audit and exceptions | Stable ProblemDetails codes/correlation IDs, workflow/audit history, immutable ledger/evidence, global exception reporting |
| Idempotency/concurrency | Submit/approve/commit/utilize/retry operations retain one outcome; stale/conflicting operations fail with governed errors rather than duplicate mutation |

## Automated checkpoint (28 August 2026)

- API build: **passed**, zero errors (existing warnings remain).
- Focused Core procurement controls: **51 passed, 0 failed**.
- Focused statutory-report suite: **12 passed, 0 failed**, including current-schema SQL Server translation/execution in an automatically removed disposable database.
- Commitment migration source guards: **2 passed, 0 failed**.
- Focused frontend PR/linkage/DMS behavior: **13 passed, 0 failed**; scoped ESLint passed.
- Disposable SQL Server architecture gate: **3 passed, 0 failed**. This covered the complete reservation/formal commitment/utilization ledger, multi-PO and contract-child allocation, tenant/lineage/idempotency guards, accepted-receipt/VAT-invoice three-way matching, 13-report idempotent seeding and the new exception/procurement-to-payment queries.
- Production-like migration rehearsal: **passed**. A copy-only compressed backup of the configured database passed `VERIFYONLY`, restored into a disposable database, applied both pending procurement migrations, passed integrity/backfill/DMS/trigger probes and `CHECKDB`, and was removed. The immutable-ledger, duplicate-source and illegal-commitment probes were rejected by SQL Server as designed.
- Disposable SQL databases remaining after the gate: **0**.
- Configured database read-only parity: **390 applied; 2 pending** (`20260828190000_AlignProcurementReservationAndFormalCommitmentLifecycle` and `20260828191500_AddPurchaseRequisitionDmsGovernance`). The pending migrations were not applied to the shared database during verification.
- Pre-existing parity note: the configured database records historical migration `20260402003233_InitialCreate`, but its source file is absent from this checkout. The production-like upgrade rehearsal still passed; the release runbook must retain the existing history row and must not attempt to recreate the database baseline.
- Focused API central-DMS controller gate: **7 passed, 0 failed**.

## Release and UAT gate

The disposable production-like database upgrade gate is complete. Release still requires:

1. the verified migrations are included in the release package and reach migration parity during the controlled deployment;
2. package hashes, backups, service readiness, public API/assets/CORS and authenticated browser smoke checks pass;
3. the scenarios in `TDC_PROCUREMENT_ARCHITECTURE_UAT.md` are executed by distinct business users after deployment.

Deployment is intentionally not evidence of compliance by itself.
