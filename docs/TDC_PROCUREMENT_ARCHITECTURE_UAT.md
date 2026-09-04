# TDC Procurement Architecture UAT

## Purpose

This script verifies the procurement requirements in the TDC ERP Architecture and Design Document, especially `FR-PR-001` through `FR-PR-012`. It covers procurement only, with the minimum Finance, DMS, workflow, receipt and payment interactions needed to prove the procurement-to-payment lifecycle.

The architecture document does not prescribe fixed approval thresholds, an 18-step tender workflow, or one mandatory procurement method. Those values and routes must be configured and published for the tenant before UAT.

## Required test identities and configuration

Use different active users for incompatible actions:

- Department requester or requisitioner
- Department reviewer, where configured
- Procurement officer
- Procurement approver
- Finance/budget reviewer
- Managing Director or delegated executive, where the configured route requires it
- Supplier-controller/onboarding reviewer
- Supplier representative
- Tender evaluator and independent award approver
- Stores receiver and independent inspection approver
- Accounts Payable reviewer
- Internal Audit reviewer for exceptions
- An unauthorised user and a user from another tenant

Before testing, confirm:

1. The tenant has a base currency, active departments, cost centres, fiscal year, chart-of-account mappings and warehouses.
2. The required shared workflows are published and their stages use controlled tenant roles.
3. Procurement thresholds and methods are effective for the test date, category, amount and currency.
4. DMS classifications and retention rules are active for supplier, requisition, tender, contract, receipt and invoice records.
5. Finance has an approved budget for the test department and period.
6. Supplier onboarding requirements are configured for Goods, Works and Services as applicable.
7. Maker/checker test users are not the same person and have refreshed their sessions after role changes.

Record the transaction number, actor, timestamp, workflow instance, audit event and any ProblemDetails `code`/`correlationId` for every test.

## 1. Supplier onboarding and controlled supplier master

1. As a supplier representative, request an onboarding token and select the registration category.
2. Complete the supplier profile and upload every configured mandatory document.
3. Remove and replace one draft attachment. Expected: removal is possible before submission, audited, tenant-scoped and blocked after the document becomes immutable.
4. Submit the application.
5. As a supplier-controller reviewer, verify documents and approve the application using the configured workflow.
6. Open the resulting business-partner record.

Expected:

- Required document types come from controlled onboarding configuration, not free-text IDs.
- The approved registration category is carried to the supplier/business-partner category and is visible in the partner details.
- Supplier approval does not grant payment-approval privileges.
- A rejected, expired, suspended, other-tenant or unapproved supplier cannot be invited or awarded.
- Application submission, document changes, verification and approval are retained in history.

## 2. Budget and procurement plan

1. As the procurement maker, create a department budget for the current fiscal period and base currency.
2. Submit it and approve it with a different configured Finance/budget approver.
3. Create a procurement plan for the same department and link the approved budget.
4. Add Goods, Works or Services plan items with quantity, estimate, required date, budget allocation, category, method inputs and specifications.
5. Edit an item before submission, then submit and approve the plan with different users.
6. Open the plan workflow/history and filter the plan register by department.

Expected:

- Only approved, current, same-department, same-period and same-tenant budgets are selectable.
- Budget allocation/category fields use controlled data rather than raw record IDs.
- Plan item edits are allowed only while the governing version is editable.
- The approved plan retains its exact budget and plan-item lineage.
- The plan total reconciles to its items and does not exceed the approved allocation.

## 3. Controlled GHANEPS exchange

1. Open the APP/GHANEPS exchange register for the approved plan or governed sourcing record.
2. Generate or upload the exchange file and record the manual submission attempt.
3. Confirm the system calculates the SHA-256 checksum.
4. Record an acknowledgement, then exercise a failed attempt and retry/reconciliation where supported.

Expected:

- Users do not type checksums, shared-record IDs or workflow-evidence IDs.
- The system retains every attempt, file reference, generated checksum, acknowledgement and reconciliation outcome.
- A retry does not create a duplicate successful exchange.
- APP traceability is visible but does not block PR submission unless an explicitly configured, approved exception route requires it.
- No direct GHANEPS API connection is assumed by this UAT.

## 4. Purchase requisition initiation and validation (`FR-PR-001` and `FR-PR-002`)

1. From an approved plan item, select **Create Purchase Requisition**, or select several approved items and choose the bulk PR action.
2. Confirm the plan, plan items, department, approved budget, budget allocation/category, cost centre and base currency are derived automatically.
3. Enter or verify description, specification, quantity, estimated cost, required date and justification.
4. Upload supporting documents through the requisition Documents area. Remove and replace one attachment while the PR is Draft.
5. Save the PR and reopen it.
6. Submit it for approval.

Expected:

- The user does not reselect a budget already fixed by the approved plan.
- Cost centre is derived from the department and is not an unnecessary free-text field.
- Base currency defaults from Finance; another currency may be selected only where the business process permits it.
- The approved plan-item estimate is the first controlled estimate source; item-master cost is a fallback when no plan-item estimate exists.
- Description, specification, positive quantity/estimate, budget, required date, justification and required documents are validated before submission.
- Central DMS owns file storage, access, versioning, retention and audit history.
- A draft attachment can be removed by an authorised actor; submitted evidence cannot be silently deleted.
- Another tenant cannot read or mutate the PR or its documents.

## 5. Purchase requisition approval (`FR-PR-003`)

1. Complete each configured review stage: department, budget, procurement and executive where applicable.
2. Attempt positive approval as the requester.
3. Complete approval as a different assigned user.
4. Replay the final approval request.

Expected:

- The requester cannot approve their own PR.
- Only configured stages apply; the architecture does not impose an unconfigured authority band.
- APP acknowledgement and policy-authority guidance are advisory unless explicitly configured as part of an approved route.
- The approved outcome and actor metadata are retained once; replay does not duplicate workflow history.
- PR submission and approval validate the effective approved budget and current availability only. They do not reserve funds, create a formal commitment or reduce availability.

## 6. Sourcing method, RFQ and tender lifecycle

1. Release the approved requisition for sourcing.
2. Create a sourcing case or use the approved-PR direct RFQ/tender action supported by the configured route.
3. Confirm the method is selected from the effective category, amount, currency and threshold rule.
4. If the method is automatic, verify the system-generated rationale. Do not enter a manual justification unless the lot structure is unusual or the method rule requires it.
5. Attempt a method override. Expected: manual reason, approved exception, evidence and workflow approval are required.
6. For RFQ, invite eligible suppliers, publish/send the request, accept sealed quotations, close/open quotations, evaluate and recommend an award.
7. For tender, publish controlled tender documents, register bids, constitute and activate the evaluation committee, complete member appointment acceptance/conflict declarations, close the bidding window, complete meeting attendance and quorum, open technical proposals, evaluate, open financial proposals at the permitted stage, recommend and approve the award. Committee constitution or activation is not permitted before publication and an on-time sealed bid; meeting, attendance and quorum are not permitted before the submission deadline closes; formal opening requires current server-confirmed quorum recorded after that deadline.
8. Exercise the configured QBS/QCBS route where applicable and verify server-calculated ranking/combined scores.

Expected:

- Every released line belongs to exactly one sourcing lot.
- Supplier invitations use approved eligible suppliers for the governed category.
- Commercial submissions remain sealed until formal opening. Any governed early opening requires a reason and immutable audit event.
- A supplier can revise a quote only while the configured submission window permits it; the current version replaces the active quote while prior versions remain in history.
- Quote totals equal the submitted line quantities and prices.
- Evaluation, conflict declarations, opening, recommendation, approval and exceptions are audited.
- The evaluator or requester cannot approve their own award.
- The winning amount is validated against available budget before award conversion.
- Direct PR sourcing retains equivalent requisition/release/source lineage; it is not rejected merely because an optional advanced sourcing-case layer was not used.

## 7. Purchase order and contract (`FR-PR-004`, `FR-PR-005`, `FR-PR-008`, `FR-PR-009`)

1. Create a PO from the approved award (or another approved source allowed by configuration).
2. Verify supplier, source, PR, items, quantities, prices, currency, warehouse and budget are copied from controlled records.
3. Submit the PO for approval as its creator.
4. Approve it as a different assigned procurement approver.
5. Open the approved PO and verify its Finance commitment reference, active reservation-envelope status, formally committed PO amount and ordered evidence history.
6. Refresh the approved record and confirm the same commitment evidence. Where an API/concurrency test deliberately repeats the now-invalid approval or activation command, confirm it is rejected without changing any budget or ledger row.
7. Where the award produces a contract, create, approve and activate the contract; then create any governed child POs.
8. Open the contract register and contract operations view.

Expected:

- The action is labelled **Submit for Approval**, not **Finalize**.
- The creator cannot approve the PO and cannot confirm its governed receipt.
- Final PO approval or approved-contract activation revalidates the effective approved budget, creates or reuses the requisition's reservation envelope, and atomically appends one immutable formal commitment keyed to the PO or contract source ID.
- The approved PO shows one commitment reference, the active reservation-envelope status and its formally committed PO amount. Ordered history identifies the `BudgetCommitmentReserved` (or reuse) control event followed by the immutable `FormalCommitment` ledger entry; it is not presented as a change to the reservation envelope's status.
- Multiple POs cannot cumulatively exceed the approved source exposure or current budget availability.
- A contract plus its child POs does not double-count commitment; child POs consume allocations within the parent contract commitment.
- Refreshing after success returns the same commitment. A repeated terminal-state approval/activation command is rejected without duplicating the commitment ledger or reducing availability twice; concurrent attempts leave exactly one successful final transition.
- The contract register shows contract number, supplier, project, value, approval/start/end dates, retention, variations, certificates, invoices, payments and balance.
- Contract/project monitoring exposes delivery, certificate and commercial progress appropriate to the configured project/QS route.

## 8. Receipt or certification (`FR-PR-006`)

1. As an authorised stores user other than the PO creator, receive Goods into a controlled warehouse and location.
2. If a PO line does not map to an inventory item, use the controlled prompt to create/link the item before receipt; cancel once to verify that no partial receipt is posted.
3. Upload the waybill/delivery evidence and complete independent inspection.
4. Record accepted, rejected, damaged and short quantities.
5. Generate the GRN/MRN where applicable.
6. For Services or Works, complete the service-completion/work-certificate route instead of a Goods GRN.

Expected:

- Warehouse and location come from controlled records and responsibility checks use the selected warehouse.
- Only accepted quantity becomes stock/AP eligible.
- DMS evidence and inspection lineage remain accessible to authorised stores/procurement users without misleading requisition-role errors.
- The PO creator cannot confirm receipt.
- Receipt/certificate utilization updates the formal commitment once and cannot exceed it.
- GRN/MRN document kinds render safely even when an optional display mapping is absent.

## 9. Invoice, three-way match and payment (`FR-PR-007`)

1. In Finance/AP, create the supplier VAT invoice against the approved PO.
2. Match invoice lines to the accepted GRN/service completion/work certificate.
3. Attempt duplicate invoicing and over-invoicing.
4. Submit and approve the invoice, then create and approve payment using different authorised users.

Expected:

- Payment is blocked until PO, receipt/certificate and VAT invoice agree within configured tolerances.
- Rejected/damaged/unaccepted quantities cannot be invoiced.
- The same receipt quantity or supplier invoice cannot be used twice.
- Invoice and payment checks revalidate budget, supplier, source, currency and tenant.
- Payment updates procurement-to-payment reporting; commitment utilization is not duplicated.

## 10. Procurement exception (`FR-PR-010`)

1. Create an exception for a genuine method, budget or process deviation.
2. Enter the reason, attach supporting documents, complete Internal Audit vouching and obtain MD/delegated approval where configured.
3. Link the approved exception to the governed transaction and retry the previously blocked action.

Expected:

- Exceptions are not informal status overrides.
- Initiation, evidence, vouching, approval, expiry and use are retained in the global exception/audit logs.
- An expired, rejected, other-tenant or unrelated exception cannot authorize the action.

## 11. Reports and audit (`FR-PR-011` and `FR-PR-012`)

Run and export the following tenant-scoped reports:

1. Purchase Requisition Status Register
2. Purchase Order Register
3. Procurement Commitment Register
4. Contract Register
5. Procurement Certificate Tracking
6. Exception Register
7. Supplier Performance
8. Procurement-to-Payment
9. The applicable planning, sourcing, award and GHANEPS exchange registers

Expected:

- Online, CSV, XLSX and PDF results reconcile to the tested transactions.
- The Contract Register includes the full `FR-PR-008` commercial lifecycle.
- The Exception Register identifies its approval/evidence lineage.
- Procurement-to-Payment traces PR → source/award → PO/contract → receipt/certificate → invoice → payment.
- Exported files have correct extensions and do not expose other tenants.
- Audit entries identify actor, timestamp, action, record and outcome and cannot be edited by operational users.

## 12. Negative, concurrency and idempotency gate

Repeat selected reads and mutations using an unauthenticated user, a user without permission, another-tenant user, the maker attempting approval and two concurrent requests.

Expected:

- Unauthenticated: `401`.
- Authenticated but unauthorised: `403` with a specific permission/control code.
- Other tenant: `404` or governed denial without information leakage.
- Stale/concurrent mutation: `409` or another governed conflict, not an unhandled `500`.
- Business validation: `422`/`400` with actionable ProblemDetails detail and code.
- Replayed submit, approve, award, issue, receive, match or pay calls do not duplicate workflows, ledger entries, stock, documents, invoices or payments.
- All unexpected failures appear in the central exception log with correlation ID.

## Acceptance evidence

UAT is complete only when:

- every numbered scenario has screenshots/API evidence and audit/workflow references;
- the disposable SQL Server integration gate passes against all migrations;
- no disposable test databases remain;
- migration parity, frontend focused tests and procurement API/core tests pass;
- unresolved failures are recorded against the exact architecture requirement rather than bypassed with direct SQL updates.
