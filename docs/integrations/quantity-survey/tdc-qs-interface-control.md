# TDC Quantity Survey Interface Control Document

- Control ID: `TDC-QS-ICD-001`
- Tracker task: `QS-0604`
- Status: implementation baseline; authenticated cross-module acceptance remains open
- Applies to: Quantity Survey, Projects, Procurement, Inventory, Finance, Workflow, DMS, contractor portal, reporting and approved external estimation/design adapters

## Purpose

This document defines which ERP module owns every record consumed or affected by Quantity Survey (QS), which states QS may select, which references QS may retain, and which mutations QS must never perform directly.

It is a control document, not a new integration engine. Existing module services, entities, workflows, authorization, DMS, audit and exception handling remain authoritative. `QS-0605` owns missing automatic reconciliation and `QS-0606` owns direct-API security certification and gap fixes.

## Non-Negotiable Rules

1. The authenticated server context supplies tenant, user and roles. Client-supplied tenant, actor, role, approval status, storage path or posting status is never trusted.
2. A QS selector returns only records from the current tenant that are active, approved/published/effective and operational for the selected project and date.
3. A GUID supplied by a client is re-resolved and revalidated at every mutation. A prior lookup result is not authorization.
4. Projects owns project membership and operational access. A QS permission does not grant access to every project.
5. Procurement owns tenders, bids, awards, suppliers/business partners, purchase orders and Works contracts. QS stores approved-source references and immutable commercial snapshots only.
6. Inventory owns receipts, inspections, issues, returns, transfers and stock valuation. QS reads posted/acknowledged source movements and must not create inventory movements to make a reconciliation balance.
7. Finance owns budgets, AP invoices, payments, taxes and posted GL. QS hands approved certificates to Finance through Finance contracts and reads Finance status; it does not post journals or rewrite payment state.
8. Central DMS owns physical files and versions. A QS record retains the central document/version and controlled-upload references; it never owns a second file repository.
9. Shared Workflow owns definitions, instances, steps and approval assignments. QS owns its domain lifecycle but cannot manufacture a completed workflow outcome.
10. Central audit and exception middleware own operational diagnostics. Domain history may supplement them but must not expose secrets, file paths or stack traces.
11. Every cross-module mutation is idempotent, transactionally bounded where the owners share a database, and recoverable where an external durable step cannot share the transaction.
12. Cancellation, rejection, reversal and deletion never erase approved lineage. They create a controlled compensating state or owner-managed reversal.

## Authoritative Owner Registry

| Business object or responsibility | Authoritative owner | QS may do | QS may retain | QS must not do |
| --- | --- | --- | --- | --- |
| Tenant, user, roles and permissions | Identity/Security | Read authenticated context and enforce QS permissions | Actor ID, role snapshot and correlation ID in governed history | Accept a client tenant/actor, create a QS role store, or bypass a central authorization result |
| Project, project membership and reporting currency | Projects | Select assigned/readable projects and request existing project authorization | Project ID/code and approved currency snapshot where required | Create a parallel project, infer access from possession of a project ID, or silently convert without the Projects/Finance rate owner |
| Project package and BoQ publication | Projects/QS BoQ | Maintain QS-owned governed BoQ versions and read project package lineage | Stable line key, approved publication ID/hash, package and classification snapshots | Mutate an Approved publication, match only by description/item code, or relink a line across projects |
| Tender, lot, bid and bid documents | Procurement | Read tenant/project-linked tender and Draft external bid; reconcile against an Approved published BoQ | Tender/bid/item IDs, approved BoQ hash and vetted submission lineage | Change tender award state, admit a bid from another partner/project, or accept uncontrolled tender evidence |
| Award and procurement decision | Procurement | Read an approved/current award through its tender/contract lineage | Award/tender reference and approved value snapshot | Create or approve an award, or treat a user-entered award reference as authoritative |
| Supplier, contractor and subcontractor identity | Procurement Business Partner | Select active, non-blacklisted, operational Supplier/Contractor/Both records in the tenant | Business-partner ID and name/registration snapshot | Create a duplicate supplier, select a rejected/unapproved registration, or link an external user to another partner |
| Purchase requisition and procurement plan | Procurement | Read approved project-linked demand lineage | PR/plan IDs and approved reference snapshot | Approve demand, change budget reservation, or link unrelated project demand |
| Purchase order and purchase-order lines | Procurement | Read project/contract-linked, non-cancelled orders in committed states; derive commitments from owner amounts | PO/line IDs, currency and committed amount snapshot | Set PO approval/status, duplicate the commitment ledger, or treat Draft/Cancelled orders as commitment |
| Receipt, GRN and inspection | Procurement/Inventory receiving | Read accepted/posted receipt quantities and costs | Receipt/line IDs, accepted quantity, unit cost and source integrity hash | Receive, inspect or post stock from QS, or count rejected/unapproved quantities as actual cost |
| Works contract and amendments | Procurement Contracts | Select a tenant-owned project-linked Works contract; require Active for downstream transactions; create only the governed QS-approved amendment/application through the existing contract owner | Contract/amendment IDs, currency, original/revised sum and approved policy hashes | Create a second contract master, activate a contract, bypass award/tender lineage, or directly overwrite the original sum |
| Inventory item, issue, return and material cost | Inventory | Select active items/rates and acknowledged project issues; reconcile owner-provided values | Item/issue/return IDs, quantity, cost and integrity hash | Adjust stock, fabricate issues/returns, or overwrite Inventory valuation |
| QS rates, estimates, measurements, valuations, certificates, variations and final accounts | Quantity Survey | Own governed QS lifecycle records and immutable revisions | Full QS domain lineage required by the relevant task | Write Procurement, Inventory or Finance outcomes merely to close a QS lifecycle |
| Project budget and forecast | Projects/Finance budget owners | Read approved budget; apply a governed approved variation through the existing budget-revision/forecast owners | Budget/forecast version ID and before/after snapshot | Replace Finance budget control, activate an unapproved revision, or double-apply a variation |
| AP supplier invoice | Finance AP | Hand an independently approved certificate to `IVendorInvoiceService`; read invoice status and paid amount | Vendor-invoice ID, handoff status, amount/currency/tax snapshot | Insert/update an AP invoice directly, force invoice approval, or accept a recalculated total mismatch |
| Supplier payment and advance | Finance AP/Payments | Read posted supplier advances and allocations through Finance contracts | Vendor-payment/allocation IDs and recovery snapshot | Mark an invoice paid, create a payment, or recover more than the posted available advance |
| AR billing/invoice request | Projects/Finance AR | Request or synchronize an editable final-payment billing schedule through the existing Projects/AR owner when applicable | Billing schedule/request ID and status snapshot | Post an AR invoice or receipt from QS, or amend locked/invoiced requests |
| Tax, withholding and currency rates | Finance | Select active/effective controlled tax/rate records and accept Finance recalculation | Controlled IDs, rate/currency and calculation snapshot | Accept a free-text account/rate, create a QS tax table, or silently override Finance totals |
| General ledger and posting events | Finance posting engine | Read posted Finance results and reconcile source references | Finance posting-event/journal reference and status | Create direct GL journals, set posting status or reverse a posting outside Finance |
| Workflow definition and instance | Shared Workflow | Select Published/effective QS workflow definitions, submit the QS entity and process assigned steps | Definition/version/instance IDs, outcome and actor/time snapshot | Accept a free-text definition ID, self-approve, infer approval from entity status, or update workflow tables directly |
| Physical evidence and generated documents | Central DMS/controlled upload/document output | Upload through the controlled scanner, retain current Published clean versions and request controlled document rendering | Upload, document-record/version IDs, checksum, MIME and scan-result snapshot | Store a local path/public executable upload, accept Pending/Skipped/Failed/Infected scans, or rewrite a Published version |
| Contractor/consultant portal identity | Business Partner plus Projects external access | Allow only the exact linked active partner and project policy to view/submit permitted artifacts | Partner/user/project-policy and submission/signature lineage | Infer access from email, expose another partner/project, or let an external actor perform internal approval |
| Notifications | Central notification service | Publish domain events after the durable transaction and retry delivery idempotently | Topic, recipient reference, attempt/outcome and correlation ID | Treat notification delivery as business approval or include credentials/secrets in audit payloads |
| Audit and unexpected exceptions | Central audit/exception middleware | Append redacted domain audit/history and allow unexpected exceptions to reach central handling | Correlation, actor, action, resource and redacted before/after snapshots | Return stack traces, swallow an exception without logging, or aggregate distinct incidents into one mutable exception row |
| External estimating/design tools | Approved adapter contract | Accept normalized allowlisted profiles and stage non-mutating reconciliation | Adapter/profile/version, exchange ID, hashes and DMS source lineage | Parse/write native vendor data directly into BoQ, permit direct database access, or infer units/classifications silently |

## Controlled Selector Contract

All selectors are server-produced. Search text may narrow an already-authorized query but cannot broaden it. Disabled choices may be shown only with an explicit reason and may never be submitted successfully.

| Selector | Mandatory server filters | Mutation-time revalidation |
| --- | --- | --- |
| Project | Current tenant, not deleted, operation-level Projects access | Project still exists, remains in tenant and actor still has required operation |
| Tender/bid | Same tenant, tender linked to selected project through governed PR/project lineage; bid belongs to exact business partner; mutable external submission requires Draft bid | Tender/bid/project/partner lineage and current bid lifecycle; approved published project BoQ still matches |
| Award | Same tenant/project/tender; award is approved/current and not cancelled/superseded | Approved award and supplier/contract lineage still current |
| Supplier/contractor | Same tenant; active; not deleted or blacklisted; operational approved registration; permitted partner type | Operational status, project/contract relationship and exact external identity |
| Purchase requisition/plan | Same tenant/project; approved and active for downstream procurement | Approval and project/budget lineage still valid |
| Purchase order/line | Same tenant/project/contract; non-cancelled owner status; line belongs to order and project scope | Status is eligible for commitment/actual calculation; currency and source quantities unchanged or conflict |
| Works contract | Same tenant; `ContractType = Works`; exactly one governed project; Active for valuation/certificate/subcontract/variation application | Contract, project, tender/award, supplier, currency and configured commercial-policy lineage remain valid |
| Inventory issue | Same tenant/project; acknowledged owner voucher; active referenced item; line belongs to voucher | Voucher remains acknowledged and integrity/quantity/cost source remains unchanged |
| DMS evidence | Same tenant; non-deleted active record; current Published version; version references exact controlled upload; scan is `Clean` | Record/version/upload/checksum relation and clean scan remain current |
| Workflow definition | Same tenant/entity type; Published/effective; expected QS workflow group/version | Current definition/step assignment, actor approval authority and separation of duties |
| Finance payment term/tax/rate/account | Same tenant; active/effective; correct Finance-controlled type/use | Owner record remains active/effective and permitted for exact transaction/date/currency |
| Report project/contract/cost code/line | QS report permission plus Projects access; contract/line belongs to selected project | Same authority applied inside provider query; client filters never replace tenant/project predicates |

No master identifier in this table may be entered as an unrestricted text field. Reference/description text remains permissible only when it is not an identifier for a governed master.

## Interface Flows

### Tender And Award To QS

1. Procurement creates and approves the PR/tender/lot/bid/award through Procurement lifecycle controls.
2. QS exposes only the project-linked tender/bid and the current Approved published BoQ.
3. An external submission is accepted only from the exact linked partner identity while the bid is mutable.
4. Controlled tender evidence must resolve to central DMS where policy requires it.
5. QS vetting records immutable line/source hashes; it does not award the tender.
6. Procurement remains responsible for award and contract creation/activation.

### Contract, Budget And Forecast

1. Procurement owns the Active project-linked Works contract and original contract sum.
2. QS measurements, valuations, claims and variations reference that contract and freeze relevant approved-source snapshots.
3. Only an independently approved QS variation may invoke the existing governed application that creates the canonical contract amendment, BoQ candidate, configured budget revision and forecast version.
4. Unchanged retries return the same application lineage. A different payload using the same request identity is rejected.
5. Revised BoQ publication and budget/forecast approval remain independent owner lifecycles.

### Procurement And Inventory Cost To Project/QS

1. Procurement owns PO approval and ordered values.
2. Receiving/Inventory owns receipt acceptance, inspection and stock posting.
3. Inventory owns issues, returns and valuation; Projects owns the material-cost read model.
4. Project commercial derivation reads eligible PO commitments, accepted receipt actuals, acknowledged inventory issues/returns and posted material-cost entries.
5. QS dashboards and reconciliation consume the derived read model. They never write a balancing stock or procurement transaction.

### Valuation And Certificate To Finance

1. QS calculates a valuation/certificate from approved measurements, BoQ/contract, deductions, retention, tax and prior-certificate lineage.
2. Shared Workflow independently approves or rejects the certificate.
3. An Approved certificate is handed to Finance AP through `IVendorInvoiceService` with a stable source/request reference.
4. Finance recalculates and owns invoice, tax, approval, payment and GL posting. Any amount/currency mismatch fails closed.
5. QS stores the Finance invoice reference and refreshes payment status from Finance; it never marks itself paid from a client request.
6. Reversal/void/payment failure is read from Finance and must be reflected as a reconciliation exception under QS-0605.

### Final Account And AR Boundary

1. QS final-account reconciliation reads Approved BoQ versions, applied variations/escalation, approved certificates, deductions, retention and Finance paid amounts.
2. Open or disputed owner records prevent closure.
3. Where a final-payment billing schedule/invoice request is required, QS calls the existing Projects/AR synchronization and may update only editable owner records.
4. Finance AR owns invoice posting, receipt and GL. QS retains references and reconciliation status only.

### Contractor Portal

1. External access starts from an authenticated user linked to an active Business Partner.
2. Projects external-access policy and the selected artifact/project are checked for every request.
3. The portal may prepare/submit/sign only explicitly allowed QS artifacts.
4. Internal vetting, approval, Finance handoff, posting and configuration remain unavailable to external actors.

### External Design/Estimation Adapter

The separate `tdc.qs.design-extraction.v1` contract remains authoritative. Native files are DMS evidence only; an allowlisted adapter emits normalized JSON/CSV/controlled-XLSX, and QS stages validation/reconciliation without direct BoQ mutation.

## Transaction, Idempotency And Recovery

| Situation | Required control |
| --- | --- |
| Same-database QS plus owner mutation | Serializable or owner-approved transaction; stable client request ID and canonical request hash; audit/history committed with domain state |
| Finance/DMS/notification durable operation outside a shared transaction | Persist Pending/Ready state first; invoke owner with stable source key; store returned owner ID; expose an idempotent Retry action; never repeat a completed owner mutation |
| Timeout after owner succeeds | Query owner by stable source/request reference before retrying create; link the existing result when hashes/totals agree |
| Payload changed on retry | Return conflict; do not overwrite the prior result |
| Source status/policy changed after Draft | Revalidate at submit/approve/apply/handoff; return to a controlled editable state or fail closed |
| Partial notification failure | Business state remains durable; delivery retries independently and cannot change approval/status |
| Concurrency | Use row-version/current-state checks; reject stale updates with a friendly conflict response |
| Reconciliation mismatch | Persist/report the exception with source references and correlation ID; never create an artificial balancing business transaction |

## Cancellation, Rejection And Reversal Ownership

| Source outcome | QS response |
| --- | --- |
| Tender/bid cancelled or superseded | Block new QS submission/application; retain historical snapshot and flag open dependent Drafts for review |
| PO cancelled or receipt reversed | Recompute commitment/actual through Projects owner and surface variance; do not edit the PO/receipt |
| Inventory issue returned/reversed | Consume Inventory return/net movement in the material read model and reopen reconciliation where policy requires |
| Contract suspended/terminated | Block new valuation/certificate/variation application except configured closeout; preserve approved history |
| Certificate rejected | Release only QS-owned Draft allocations in the same transaction; no AP handoff |
| AP invoice voided/reversed | Finance owns reversal; QS refreshes status and prevents paid/final closure until reconciled |
| Payment failed/reversed | Finance owns payment status/posting; QS recalculates paid-to-date and flags recovery/retention/final-account mismatch |
| Workflow cancelled/failed | Map only the owner-defined rejected/cancelled outcome; never treat Completed as rejected |
| DMS evidence quarantined/withdrawn | Block dependent submission/approval/application and preserve the historical document/version reference |

## Security And Audit Boundary

- Controller policy and project/contract/partner authorization are cumulative; passing one never bypasses the others.
- Reads, manages, approvals, exports and external access use separate permissions.
- Maker/checker identity and workflow assignment are rechecked at the decision point.
- Provider/report queries include tenant and authorized-project predicates before applying user filters.
- Every create/update/submit/approve/reject/apply/handoff/refresh/reversal-reconciliation action retains actor, action, resource, client request ID/hash where applicable, correlation ID and redacted before/after state.
- Expected domain failures return structured Problem Details. Unexpected failures reach central exception middleware and return a friendly correlation reference.
- QS-0606 must test direct API denial for missing role, wrong project, wrong contract, wrong tenant, wrong partner, stale row version, maker/checker conflict and unauthorized report drilldown, and verify the denial/audit behavior.

## Current Implementation Coverage And Remaining Owners

| Interface | Current implementation evidence | Remaining acceptance owner |
| --- | --- | --- |
| Tender/bid/approved BoQ/external partner | QS tender-BoQ service checks tenant, project PR lineage, Draft bid, exact partner identity, approved published BoQ and controlled evidence | Authenticated cross-partner/project negative journey under QS-0606 |
| Active Works contract and operational partner | QS contract/subcontract services filter Works/Active contracts, exact project lineage, active non-blacklisted operational partner and approved/effective QS policy | Governed representative contract fixture and browser/API acceptance |
| DMS evidence | Existing QS services require same-tenant active records, current Published versions, matching controlled-upload references and `Clean` scan | Cross-tenant/version/quarantine negative acceptance under QS-0606 |
| PO/receipt commitment and actual | Projects commercial derivation reads Procurement POs/lines, accepted receipt quantities and posted material costs | Automatic refresh, reversal and balancing evidence under QS-0605 |
| Inventory material issues | QS material reconciliation exposes same-tenant/project acknowledged issue vouchers and integrity/cost lineage | Return/reversal and Inventory/GL balancing evidence under QS-0605 |
| Variation downstream application | Existing application creates canonical amendment, revised BoQ candidate, configured budget revision and forecast version with idempotency lineage | Cross-owner governed positive/retry/reversal acceptance under QS-0605 |
| Certificate to AP/payment | Existing certificate service uses `IVendorInvoiceService`, rejects Finance total mismatch and refreshes Finance status | Automatic payment/void/reversal/GL reconciliation under QS-0605 |
| Advance recovery | QS reads posted Finance supplier advances and allocations | Reversal and over-recovery cross-owner acceptance under QS-0605 |
| Final account | QS reconciles approved commercial records and Finance-paid amounts | Full close/reopen/reversal journey under QS-0605 and UAT |
| QS authorization | Dedicated QS permissions and project/partner checks exist across controllers/services | Complete direct-API matrix, denial audit and section/unit authority under QS-0606 |
| External adapters | `tdc.qs.design-extraction.v1` defines normalized, non-mutating exchange | Approved live adapter and representative conformance acceptance remain separate business work |

## QS-0604 Acceptance Checklist

- [x] Procurement, Inventory, Projects, Contracts, AP, AR, GL, Workflow, DMS, portal, reporting, audit and external-tool owners are named.
- [x] Allowed reads, retained references and prohibited parallel mutations are explicit.
- [x] Project, tender/bid, award, supplier, PR/plan, PO, Works contract, Inventory issue, DMS, Workflow, Finance-master and report selector rules are defined.
- [x] Tender, cost, certificate/AP, final-account, portal and external-adapter flows are documented.
- [x] Idempotency, partial failure, retry, cancellation and reversal ownership are documented.
- [x] Existing implemented boundaries and QS-0605/QS-0606 remaining work are separated.
- [ ] Authenticated representative-data API/browser evidence proves approved-only selector contents and cross-tenant/project/partner denial.
- [ ] QS-0605 proves automated PO/receipt/Inventory/certificate/invoice/payment/GL reconciliation and reversal.
- [ ] QS-0606 proves the complete direct-API hard-stop and denial-audit matrix.

QS-0604 remains `In progress` until the first open acceptance item is evidenced. The latter two open items remain owned by QS-0605 and QS-0606 and do not authorize duplicate integrations inside QS.
