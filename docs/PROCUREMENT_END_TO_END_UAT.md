# Procurement End-to-End UAT Script

## Purpose and scope

This script validates the procurement requirements in the TDC ERP Architecture and Design Document from planning through supplier onboarding, requisition, sourcing, tender/RFQ, evaluation, award, contract or purchase order, receipt/certification handoff, matching, reporting and audit.

Inventory and Stores operations after the procurement receipt handoff are outside this UAT and will be tested separately.

Use fresh records for every transactional test. Existing records may be used only for read-only comparison.

## UAT result convention

For every test case, record:

- Result: `Pass`, `Fail` or `Blocked`
- New record/reference number
- Tester and user account
- Date and time
- Screenshot or exported evidence
- Actual result and defect reference, if any

Do not mark a test passed from the screen alone when the expected result includes a workflow, commitment, document, journal, audit event or tenant restriction.

## Required users

- Procurement maker: `TDC_PROCUREMENT_OFFICER`
- Procurement approver: `TDC_HEAD_OF_PROCUREMENT`
- Executive approver: `TDC_MANAGING_DIRECTOR` or configured delegate
- Internal Audit: `TDC_INTERNAL_AUDIT`
- Supplier controller/onboarding reviewer
- Tender or RFQ evaluator
- Award approver who is different from the evaluator and initiator
- Finance/AP reviewer with budget, commitment and journal-read access
- Supplier portal applicant user
- Unauthorized user with no procurement permission
- Alternate-tenant user

Never use one Administrator account for all actions. Maker-checker and segregation-of-duty results are invalid unless distinct users perform incompatible stages.

## UAT preparation

Before transactional testing, confirm the following configuration exists. These are business configuration prerequisites, not values to invent during UAT.

1. The tenant base currency is `GHS` in Finance.
2. The test department has a controlled cost centre mapping.
3. The current fiscal year is open.
4. Published shared workflows exist for the lifecycles being tested, including:
   - Procurement Budget
   - Procurement Plan
   - Supplier Onboarding/Approval
   - Purchase Requisition
   - RFQ or Tender Evaluation, where configured
   - Award Approval
   - Purchase Order
   - Contract Approval, where applicable
5. Workflow stages use controlled tenant roles and have at least one eligible user for each stage.
6. An effective procurement policy exists for the test date and currency. For automatic routing, it must contain matching enabled Category, Method and Threshold rules for the test category and amount.
7. Authority, evidence, exception and policy SOD rules are optional unless the business has explicitly configured them for the route under test.
8. Approved items, units of measure and supplier categories exist.
9. DMS document types required for supplier and tender evidence exist.
10. At least two independent eligible suppliers can complete the competitive route.

Expected:

- Users select controlled names, roles, workflows, categories and documents; they do not enter internal GUIDs, workflow-definition IDs, evidence IDs, shared-record IDs or checksums.
- Real user identity conflicts remain blocked even when optional policy metadata is not configured.

## Runtime gate boundary

Use this boundary when interpreting any warning or stop during UAT:

- Always mandatory: authenticated tenant access, permission, complete positive transaction lines/specifications, approved source record, approved/active/non-suspended supplier, budget availability, maker-checker, sealed-submission timing and immutable source linkage.
- Conditional when configured: authority bands, DEC-011 AVL/risk/performance assessments, award-verification checklists, GHANEPS mappings, advanced sourcing-case controls, performance security and additional evidence/signature rules.
- Advisory when absent: APP/GHANEPS acknowledgement, optional policy-authority metadata, DEC-011 assessments, award-verification checklist and advanced sourcing-case metadata.
- A configured control remains a hard stop when it is ambiguous, stale, breached or incomplete. The system must not silently bypass a control the tenant explicitly enabled.

## UAT-PRC-001: Supplier onboarding and approval

### A. Create and issue supplier access

1. Sign in as the authorized supplier onboarding officer.
2. Open `Administration → Procurement → Supplier Applicant Access` or `Supplier Onboarding Tokens`.
3. Create access for a new supplier applicant using a unique email and company identifier.
4. If an onboarding fee is configured, complete only the configured payment/reconciliation route. If no fee is configured, confirm payment is not requested.
5. Issue the applicant access link/token.

Expected:

- A controlled, time-bound applicant access record is created.
- Retrying the same create/issue action does not create a second active token.
- No internal database ID is requested from the user.

### B. Submit supplier application

1. Open the public `Supplier Application` page.
2. Sign in using the issued applicant access.
3. Enter legal name, registration/tax identifiers, contacts, addresses, bank information and requested supplier categories.
4. Upload the required registration, tax, licence and ownership documents.
5. Save as Draft, sign out, sign in again and confirm saved data remains.
6. Submit the application.

Expected:

- Required fields and document types are clearly identified.
- Documents are stored through central DMS and remain linked to the application.
- Duplicate supplier identifiers are detected.
- The submitted application is locked from uncontrolled applicant changes.

### C. Due diligence and maker-checker approval

1. Sign in as the supplier controller/reviewer.
2. Open `Administration → Procurement → Registrations`, `Supplier Due Diligence` or the assigned review queue.
3. Review applicant data, evidence, category eligibility, sanctions/risk indicators and duplicate checks.
4. Record the review outcome and submit it for approval.
5. Attempt final approval using the same reviewer.
6. Sign in as a different authorized approver and approve the supplier.
7. Open the supplier record and Approved Vendor List.

Expected:

- Self-approval is rejected.
- Approved supplier master data is created once.
- Supplier categories, approval status and evidence lineage are retained.
- The supplier is available for eligible sourcing only after approval.
- Applicant, reviewer and approver actions appear in audit history.

### D. Supplier negative tests

1. Repeat submission with the same registration or tax identifier.
2. Attempt approval with an unauthorized user.
3. Attempt to award to a Draft, Rejected, Suspended or ineligible supplier.
4. Attempt to view the application with an alternate-tenant user.

Expected:

- Duplicate creation is blocked or routed to governed duplicate review.
- Unauthorized action returns `403`.
- Ineligible supplier cannot be selected for award.
- Cross-tenant record returns `404` or the governed denial without leaking data.

## UAT-PRC-002: Procurement budget lifecycle

1. Sign in as the Procurement Officer.
2. Open `Procurement → Planning → Budgets`.
3. Create a current-year `GHS` budget for the test department with valid dates and sufficient approved amount.
4. Submit it for approval.
5. Attempt approval using the maker account.
6. Sign in as the configured budget approver and approve it.
7. Open the budget detail page and review the Workflow tab/history.
8. If revision is enabled, select Amend/Revise, create a new revision, submit it and approve it with separate users.

Expected:

- Maker cannot approve their own budget.
- Workflow status, current stage and full action history are visible.
- An unapproved, expired or superseded budget cannot be used for a new transaction.
- A revision does not silently overwrite the approved version or existing commitments.
- Approved, committed, spent and available amounts remain distinguishable.

## UAT-PRC-003: Procurement plan and plan items

1. Create a procurement plan for the current fiscal year and test department.
2. Add at least two Goods items against the approved budget allocation.
3. For each item, select the controlled product, UOM, quantity, unit cost, required date, category and other applicable planning fields.
4. Edit one saved plan item and confirm the update is retained.
5. Remove a disposable test item and confirm the remaining totals recalculate.
6. Submit the plan and approve it with a different user.
7. Confirm the plan becomes Active/Approved.
8. On the plan list, filter by department.
9. On the approved plan Items tab, select one item and then multiple items using the row checkboxes.

Expected:

- Budget allocation is selected from controlled approved budgets; users do not type an ungoverned budget category.
- Plan totals equal the sum of its items.
- Edit and delete controls are available only while the lifecycle permits changes.
- Department filter returns only matching plans.
- Approved plan items retain plan, budget, department, category, currency and version lineage.
- Actions show PR, RFQ, Tender and PO only when the selected records and governed route make that action eligible.

## UAT-PRC-004: Manual APP/GHANEPS exchange

`APP Submission` means Annual Procurement Plan Submission.

1. Open `Procurement → Planning → APP Submissions`.
2. Create an export for the approved plan version.
3. Download the configured JSON, XML or CSV file.
4. Confirm the system calculates and displays the SHA-256 checksum automatically.
5. Select any additional shared evidence by friendly document name/type; do not enter an internal record ID.
6. Record the manual submission attempt.
7. Record an acknowledgement/reference returned by GHANEPS.
8. Test a failed attempt followed by Retry/Reconcile.

Expected:

- The end user is not asked to calculate a checksum.
- Shared evidence selectors do not expose internal IDs as required input.
- Attempt, acknowledgement and reconciliation history is visible.
- Retrying does not duplicate the same exchange.
- No direct GHANEPS API connection is assumed for this manual process.
- Missing acknowledgement is traceability information and does not by itself block PR submission.

## UAT-PRC-005: Purchase requisition from approved plan items

1. From the approved plan Items tab, select one or more compatible items.
2. Select `Create PR`.
3. Confirm the PR form is pre-populated from the selected plan items.
4. Confirm the linked approved budget is loaded automatically.
5. Confirm the department-derived cost centre is not displayed as a user-entry field.
6. Confirm currency defaults to the Finance base currency `GHS`.
7. Enter justification, required date and a positive specification for every line, or select an approved requisition specification template.
8. Save as Draft and reopen it.
9. Submit the PR.
10. Attempt approval as the requester.
11. Sign in as a different configured PR approver and approve it.

Expected:

- Plan, plan-item, budget, department and cost-centre lineage is retained automatically.
- A user may select a different permitted currency only when the transaction requires it; GHS is the default.
- Missing line specifications produce clear actionable guidance.
- APP exchange and optional policy-authority metadata are advisory and do not block ordinary PR submission.
- Maker cannot approve their own PR.
- Approval creates one approved outcome and preserves workflow history.
- Replaying submit/approve does not create another workflow or duplicate budget exposure.

## UAT-PRC-006: Budget availability and Finance commitment

1. Review the approved PR budget section with the Finance reviewer.
2. Confirm the budget is still effective and has enough available funds.
3. Record/reserve the configured commitment at the supported lifecycle point.
4. Repeat the same action or refresh/retry after the first success.
5. Create a separate PR whose amount exceeds available funds and attempt to proceed.

Expected:

- The valid PR reserves or records its Finance exposure exactly once.
- Budget available balance reduces by the correct amount.
- Debit/credit or commitment register entries balance where a journal is created.
- Insufficient funds block the governed financial action with a clear message.
- Optional policy authority guidance is not substituted for the configured PR approval workflow.

## UAT-PRC-007: Automatic sourcing release and method selection

1. Open the approved PR.
2. Confirm it shows `Ready for sourcing release` only after approval and required details are complete.
3. Start the available sourcing action.
4. Confirm the system automatically records the immutable sourcing-release audit entry.
5. Confirm the method is selected from the effective Category, Method and Threshold rule for category, amount and currency.
6. Review the displayed automatic rationale: policy/version, category, amount, currency, threshold and matched method rule.
7. For a normal single-lot policy-selected route, proceed without entering a manual method justification.
8. If using unusual/multiple lots, enter the lot-structure explanation.

Expected:

- Users do not manually click a separate release control merely to unlock sourcing.
- Approved PR and release audit are sufficient to create RFQ or Tender; an advanced sourcing case is optional.
- Automatic method rationale is retained by the server.
- A manual reason is required only for a method override, exceptional/noncompetitive rule that explicitly requires it, or unusual lot structure.

### Method override negative test

1. Attempt to change the policy-selected method.
2. Do not provide an approved exception or reason.
3. Retry with the configured approved exception, required evidence and approval workflow.

Expected:

- Unapproved override is blocked.
- An approved override retains reason, exception, evidence, approver and workflow lineage.

## UAT-PRC-008: Standard RFQ route

Use a fresh approved PR whose published policy resolves to Request for Quotation.

1. Select `Create RFQ` from the approved PR or eligible plan-item action.
2. Confirm PR, sourcing release, items, quantities, currency, estimate and specification are pre-populated.
3. Add the required controlled documents and dates.
4. Add at least the policy-required number of approved eligible suppliers.
5. Publish/dispatch the RFQ.
6. Record at least two supplier quotations/bids.
7. Complete committee, conflict-of-interest and quorum controls where configured.
8. Open quotations only at the permitted stage.
9. Evaluate against the controlled criteria.
10. Generate an award recommendation.
11. Attempt approval using the evaluator.
12. Approve using a different authorized award approver.

Expected:

- Direct PR-to-RFQ creation succeeds without requiring an advanced sourcing case.
- Commercial information remains hidden before the permitted opening stage.
- Ineligible suppliers and self-approval are rejected.
- Evaluation result is calculated and retained; users cannot alter a completed server result outside the governed process.
- Dispatch, opening, evaluation, recommendation and approval are audited.
- Retrying creation/dispatch/award does not duplicate the RFQ or award.

## UAT-PRC-009: Tender route from publication to award

Use a fresh approved PR whose published policy resolves to an applicable tender method.

1. Select `Create Tender`.
2. Confirm PR, release, plan item, budget, specification, estimate and currency lineage.
3. Add the tender schedule, evaluation template, controlled tender documents and submission deadline.
4. Assign the tender committee using controlled users/roles.
5. Record conflict-of-interest declarations and confirm quorum.
6. Publish the tender.
7. Invite or admit at least two eligible suppliers.
8. Record technical and financial proposals before the deadline.
9. Close submissions and perform the authorized opening.
10. Complete compliance and technical evaluation.
11. Open financial proposals only after the permitted technical stage.
12. Complete financial/commercial evaluation and comparison.
13. Generate the evaluation report and award recommendation.
14. Attempt award approval with the evaluator or tender initiator.
15. Approve the award with a different authorized user.
16. Send/record award and unsuccessful-bidder notifications.

Expected:

- A direct approved-PR tender is allowed without mandatory advanced sourcing-case lineage.
- Late or unauthorized bid access is blocked.
- Technical and financial information follows the configured opening sequence.
- Committee, COI, quorum, evaluation and award history remains traceable.
- Supplier eligibility is rechecked at award.
- Evaluator/initiator cannot approve their own award.
- One approved tender result produces one award.

### QBS/QCBS variants, where configured

- QBS: only the unique highest-ranked qualified technical bidder proceeds to permitted financial opening/negotiation; any lower-ranked selection requires a governed exception.
- QCBS: technical and financial weights come from policy/template, combined score is calculated by the server, and an unresolved tie is blocked or routed to the configured tie process.

## UAT-PRC-010: Exceptional, direct, petty or emergency route

Run only the exceptional methods configured by TDC.

1. Create a separate approved PR eligible for the exceptional route.
2. Select or confirm the policy method.
3. Supply the required reason and evidence only when the published rule requires it.
4. Complete the configured exception approval with distinct users.
5. For emergency procurement, complete Internal Audit vouching and executive approval where configured.
6. Complete required post-award filing.

Expected:

- Normal RFQ/tender routes are not blocked by petty/emergency evidence requirements.
- Exceptional/noncompetitive actions retain justification, evidence and approval.
- Incompatible stages cannot be performed by the same actor.
- Direct status manipulation is rejected.

## UAT-PRC-011: Award to purchase order or contract

### A. Purchase order

1. Open the approved Award and select `Create Purchase Order`, or use another supported approved exceptional source.
2. Confirm supplier, items, quantities, prices, `GHS` default currency, warehouse/delivery location, budget and source are populated.
3. Submit the PO.
4. Attempt approval as the PO creator.
5. Approve with the configured different user.
6. Review the commitment/exposure.

Expected:

- Ordinary PO creation requires an approved award/contract or governed exceptional source; a plan item or unapproved PR cannot bypass sourcing.
- Maker cannot approve their own PO.
- PO exposure is committed exactly once and is not double-counted with the PR or contract.
- Repeating create/submit/approve does not duplicate PO or commitment.

### B. Contract register and monitoring

1. From an approved award, select `Create Contract`.
2. Complete contract dates, value, supplier, milestones, deliverables and required documents.
3. Submit and approve with separate users.
4. Activate the contract.
5. Open `Procurement → Contract Operations` and review obligations, milestones and status.

Expected:

- Contract retains award, supplier, budget and approval lineage.
- The published Contract approval workflow and maker-checker remain mandatory.
- Policy authority, GHANEPS, DEC-004/DEC-008 evidence and performance security are enforced only when configured for the contract route.
- Contract activation does not require an unrelated fixed fourteen-decision profile or fixed legal/Internal Audit evidence when those controls were not configured.
- Invalid budget/commitment conditions block activation.
- Contract and PO exposure are not counted twice.
- Expiry, milestone and performance information is visible in the register.

## UAT-PRC-012: Receipt, certification and three-way-match handoff

This test stops at the procurement/Finance handoff. Detailed warehouse stock, GRN/MRN and Stores movements will be covered by Inventory and Stores UAT.

1. Open the approved/open PO and record a partial or full receipt/certification using the independent receiving user.
2. Attach the delivery/waybill and inspection/certification evidence through DMS.
3. Confirm only accepted/certified quantity is eligible for invoice matching.
4. Have Finance create or review the supplier invoice match against PO and accepted receipt/certification.
5. Attempt to match more than the accepted quantity or match the same quantity again.

Expected:

- PO, receipt/certification and invoice lineage is visible.
- Rejected/damaged/unaccepted quantities are not AP eligible.
- Quantity and value tolerances follow configured controls.
- Duplicate or excess matching is blocked.
- Any resulting Finance posting is balanced and auditable.

## UAT-PRC-013: Procurement reports and exports

Run the available procurement reports, including:

- Procurement Plan/APP status
- Purchase Requisition Status Register
- Sourcing/RFQ/Tender Register
- Evaluation and Award Register
- Purchase Order Register
- Commitment Register
- Contract Register/monitoring
- Supplier onboarding/eligibility status
- Procurement exceptions

For each report:

1. Filter by fiscal year, department, supplier, status and date where supported.
2. Confirm the new UAT references appear once.
3. Export CSV, XLSX and PDF.
4. Confirm the file extension matches its content.
5. Repeat with an alternate-tenant user.

Expected:

- Reports reconcile to source transactions and commitments.
- Current tenant data only is returned.
- Export and report access follows permissions and is audited.

## UAT-PRC-014: Security, audit and retry tests

Repeat representative reads and mutations using:

- Unauthenticated browser/request
- User without the required permission
- Original maker attempting approval
- Evaluator attempting award approval
- Supplier controller attempting an incompatible award action
- Alternate-tenant user
- Double-click/retry of submit, approve, release, create RFQ/Tender, award and create PO

Expected:

- Unauthenticated request returns `401` or redirects to login.
- Unauthorized action returns `403`.
- Cross-tenant record returns `404` or governed denial.
- Maker-checker and real SOD identity conflicts remain blocked.
- Rejected requests create no business mutation, commitment or workflow action.
- Successful retries are idempotent and do not duplicate transactions.
- Audit history identifies actor, timestamp, action, outcome and reference without exposing secrets.

## End-to-end exit criteria

Procurement UAT passes only when all of the following are true:

- A fresh supplier completes onboarding and independent approval.
- A fresh budget and procurement plan complete maker-checker approval.
- Manual APP export/acknowledgement is traceable without user-entered technical IDs/checksums.
- A plan item creates a complete PR with automatic budget, cost-centre derivation and GHS default.
- The PR completes independent approval and valid Finance exposure once.
- Automatic sourcing release and policy method rationale are retained.
- At least one RFQ completes dispatch, bid, evaluation and award.
- At least one Tender completes publication, bid opening, evaluation and award.
- An approved award creates a governed PO or contract and correct commitment.
- Receipt/certification and three-way-match handoff is traceable.
- Reports, tenant isolation, permissions, maker-checker, audit and retry controls pass.
- Every failure has a logged defect and no test data is misrepresented as production acceptance.
