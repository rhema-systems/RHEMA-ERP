# TDC customer demonstration & UAT walkthrough

**Budget to procurement, receipt, invoice matching and Stores**<br>
Prepared: **7 September 2026, 09:04 UTC account check** · Environment: **localhost:3000 / DEFAULT**<br>
All amounts are **GHS**. All examples are **LOCAL UAT SIMULATION ONLY**.

## Start here — which script should I use today?

**For the customer session in 30 minutes, use Part A: the completed-record demonstration.** It follows the actual record chain without repeating approvals, publishing another tender or posting stock again. Part B is the detailed fresh-transaction script for a prepared session, not a promise that an entire new procurement can be completed in 30 minutes.

The main Goods/NCT example reached an active Supply contract, accepted receipt, issued GRN/MRN, an independently approved invoice, Stores issue/acknowledgement/return and a posted zero-variance count. The Petty example reached accepted receipt and issued GRN/MRN. These checkpoints were tested over several sessions, with fixes between stages. **An uninterrupted rehearsal of this complete script has not been performed. Full UAT is not complete.**

### Important before the customer arrives

- **Access:** temporary Stores/Audit roles were removed after testing. New receipt, inspection, Stores issue/return and count actions need authorized role/scope setup first. Contract approval also used a temporary independent reviewer role that was removed. See the account matrix. No access changes were made while preparing this guide.
- **Use existing records for today's demonstration.** Do not reset data, resubmit completed approvals or create another invoice for the fully invoiced main receipt.
- **Do not pull, rebuild, restart or change policies immediately before the demonstration.** The tested local checkout is not a claim that every fix is merged into the customer's deployment. Local preparation baseline: `e5ad6446c6`; Petty delivery work is in PR 205. Keep the currently running environment stable.
- **Sign in once before the session and open the intended pages.** Local development pages have had slow initial compilation and occasional loading/chunk errors. A successful HTTP health check is not a browser rehearsal.
- **PDF presentation remains unverified:** GRN/MRN records were issued, but the Open PDF click did not visibly open a PDF during the last tests. Do not promise a live PDF presentation without first opening it successfully. The award email was reported delivered by the user after a delay.
- **Supporting documents:** the original bid had technical and commercial proposal files only. It is not evidence that business registration, tax clearance or financial statements were requested and submitted. Do not describe two proposal files as those compliance documents.
- **Alternative routes:** RFQ, QBS, QCBS and Emergency remain unexecuted end to end. Demonstrate their intended route only, not a claimed completed transaction.

## Account card — keep this beside you

Use [Sign in](http://localhost:3000/login). After every account change, select **DEFAULT**, check the displayed user, then reopen the record. Use the profile menu to sign out; changing the URL does not change the user. Passwords and OTPs are deliberately excluded: keep the privately agreed credentials separately.

These are the local test-account mappings, not a statement that the architecture mandates these exact role names. Security-role memberships below were rechecked read-only at **09:04 UTC on 7 September**. This is not a fresh login test of every account or every permission.

| Username | Responsibility / relevant role | Use in this script | Current preparation note |
| --- | --- | --- | --- |
| `procurementofficer` | Procurement Officer / `TDC_PROCUREMENT_OFFICER` | Budget, market analysis, plan, PR, tender, publication, committee organization, award notification, PO maker and contract activation submitter | Role present; current browser account at preparation |
| `financereviewer` | Finance Reviewer / `TDC_FINANCE_REVIEWER` | Budget approval; third plan review | Role present; count Finance scope used in UAT is inactive |
| `manager` | John Manager / `TDC_USER_DEPARTMENT_HEAD` | First plan review | Role present |
| `procurementapprover` | Head of Procurement / `TDC_HEAD_OF_PROCUREMENT` | Second plan review, tender/PO approval; configured document/bond review; tested contract draft creator | Role present; do not approve its own contract or use as final Harbourline award approver: supplier-onboarding segregation blocked the latter in UAT |
| `employee` | Jane Employee / `TDC_MANAGING_DIRECTOR` | Final plan and PR approval; independent award approval; Stores requester and recipient | MD role present; temporary Internal Audit role is absent |
| `tdc0102-checker-201531` | Specification Checker / `TDC_EVALUATOR` | Committee Chair and evaluator | Role present; retain actual tender-specific appointment |
| `procurementevaluator` | Procurement Submitter / `TDC_EVALUATOR` | Committee Secretary and evaluator | Role present; username differs from display name |
| `finance.manager` | Yaw Osei / Finance Manager and `TDC_EVALUATOR` | Committee voting member/evaluator; second invoice approval | Both roles present; different responsibilities remain separate workflow actions |
| `ap.officer` | Akua Owusu / Accounts Payable Officer | Receipt-linked invoice maker and submitter | Role present |
| `accounts.officer` | Kofi Boateng / Accounts Officer | First invoice approval | Role present |
| `financial.controller` | Abena Dapaah / Financial Controller | Final invoice approval | Role present |
| `michael.martey@proton.me` | Harbourline Goods Supply Ltd supplier | Supplier portal, bid and performance-bond submission | Previously used supplier login; recheck privately before a supplier demonstration |
| `admin` | Administrator | Authorized setup only | Never substitute Administrator for all business approvals |

### Access that must be prepared for new transactions

The following accounts successfully performed these actions earlier **with explicitly approved temporary access**. They are not currently ready for the same actions solely because their username is in this guide.

| Intended actor | Additional access used in the test | Scope / independence |
| --- | --- | --- |
| `manager` | `TDC_STORES_OFFICER` | DEMO-PM warehouse, LOC-001; distinct from requisition requester and Stores approver. Existing warehouse assignment alone is insufficient without the Security role. |
| `procurementapprover` | `TDC_STORES_MANAGER` | DEMO-PM; matching responsibility assignment must be active and unexpired. Distinct from receiving/issuing maker. |
| `manager` | `TDC_HEAD_OF_PROCUREMENT` for the Supply contract review | Independent of Procurement Approver (recorded creator) and Procurement Officer (activation submitter). This temporary role was removed. An equivalent independent authorized reviewer can be used only after preparation. |
| `financereviewer` | Active count Finance responsibility assignment | Restricted to DEMO-PM for count review/posting. |
| `employee` | `TDC_INTERNAL_AUDIT` plus Audit responsibility assignment | Restricted to DEMO-PM for the count test, separate from counter/Stores/Finance reviewers. Removed after UAT. |

Have the authorized administrator and the Finance owner confirm the appropriate demo assignments through existing administration screens before new work. Do not bypass missing access or change Finance implementation. Re-login after any authorized role change. Remove temporary additions after the demonstration.

## Part A — 30-minute completed-record demonstration

**Mode: review only.** Explain the business handoff, show status and workflow history, then move forward. Completed records usually do not show the action buttons used earlier. That is expected; do not reopen a transaction just to make a button appear. The links below are presenter shortcuts. During the explanation, point out the equivalent process-flow sidebar or module menu.

| Minutes | Account | Open and show | Suggested explanation / expected evidence |
| --- | --- | --- | --- |
| 0–2 | `procurementofficer` | [Budget PB-2026-0001](http://localhost:3000/procurement/planning/budgets/81efe55a-9741-49ad-8ece-6e2685c18391) | “The approved Operations Goods budget is GHS 100,000. Finance reviewed it independently.” Current campaign remaining amount is GHS 47,250 after the main purchase and Petty receipt, not the earlier GHS 48,000 snapshot. |
| 2–5 | Same | [Market analysis](http://localhost:3000/procurement/planning/market-analysis/2e485512-27d3-4321-804b-ba9203da5e42), then [PP-2026-0001](http://localhost:3000/procurement/planning/plans/b3889e4f-5101-42ba-ad73-cb956cd768df) | “Market analysis informed the Barcode estimate. The GHS 55,000 plan passed Department, Procurement, Finance and MD review, then publication.” Show two lines and workflow history. |
| 5–7 | Same | [APP submissions](http://localhost:3000/procurement/planning/app-submissions), then [PR-2026-0001](http://localhost:3000/procurement/purchase-requisitions/b9a17bc2-eb37-44ba-8601-dbf7c64250fc) | “The approved plan has a controlled manual exchange record. This is a simulated acknowledgement, not an actual GHANEPS transmission. The PR retains its source lines and approval.” |
| 7–12 | Same | [Tender TND-2026-0001](http://localhost:3000/procurement/tenders/55d2a153-a1ee-4217-9a8d-6014cd27a520), then [bid](http://localhost:3000/procurement/bids/5367ed43-04bf-4b1e-a6c3-4d6b174b6b90) | “Approved PR → sourcing decision → approved tender → controlled publication → supplier bid. After closing, committee attendance/quorum and opening permitted evaluation.” Show one GHS 52,000 bid and evaluation history. Proposal files are not tax-clearance evidence. |
| 12–16 | Same | [Award](http://localhost:3000/procurement/awards/7b579964-b892-46a8-a713-475087cae333), [PO-2026-0001](http://localhost:3000/procurement/purchase-orders/a0eae5c8-1915-4c68-9e94-001354ae5a34), [Supply contract](http://localhost:3000/procurement/contracts/8a6347b8-b82b-4393-8b1c-db9e01c86a14) | “The award, PO and active Supply contract represent the same GHS 52,000 purchase, not two purchases. Commitment must not double. The performance-bond lifecycle was simulated.” Contract activation preceded receiving. |
| 16–19 | Same if read access permits; otherwise pre-authorized Stores viewer | On the main PO, open Receipts / GRN-MRN areas | Show REC260001 Accepted, inspection Closed, GRN-2026-0002 and MRN-2026-0002 Issued/Reconciled. “Only accepted goods move into stock and become eligible for invoicing.” Avoid an unverified live PDF promise. |
| 19–23 | Sign out; sign in `ap.officer` | [Invoice VI-2026-00002](http://localhost:3000/finance/ap/invoices/64e2e5a3-efe7-468c-a57f-cde051c67b76) | Show GHS 52,000, matching Passed, Approved and Paid Amount zero. Explain `accounts.officer` → `finance.manager` → `financial.controller`; show history instead of repeating approvals. “The invoice was entered against accepted receipt quantities; it was not automatically generated as a supplier invoice PDF.” |
| 23–27 | Prepared authorized Stores viewer only | [Warehouse Items](http://localhost:3000/inventory/warehouse-items), [Requisitions](http://localhost:3000/inventory/requisitions), [Physical Counts](http://localhost:3000/inventory/physical-counts) | Show 2 PVC issued, recipient acknowledgement, 1 returned, and the historical zero-variance count. Current PVC is 19; Barcode is now 21 after Petty. If view access is unavailable, use the recorded results below and explicitly say this part is evidence review, not a live click demonstration. |
| 27–30 | `procurementofficer` | [Petty PO-2026-0002](http://localhost:3000/procurement/purchase-orders/f00862f4-712a-481b-8ee3-044489ff3934) | Show one Barcode at GHS 750, Received, REC260002 and issued GRN/MRN. Explain the shorter approved-quotation route. Close with the outstanding RFQ/QBS/QCBS/Emergency tests, not a claim that they passed. |

**Optional supplier view:** if the supplier login is already prechecked, substitute two minutes of tender review with [My Bid](http://localhost:3000/external-portal/my-bids/5367ed43-04bf-4b1e-a6c3-4d6b174b6b90). Sign out of the staff account first. Show submitted proposal and bond history; do not upload or resubmit anything.

### The figures to say aloud

| Record / calculation | Amount or quantity |
| --- | --- |
| Approved budget | GHS 100,000 |
| Main plan / PR estimate | 20 Barcode × 750 + 20 PVC × 2,000 = GHS 55,000 |
| Main bid / award / PO / contract / invoice | 20 Barcode × 700 + 20 PVC × 1,900 = GHS 52,000 |
| Historical budget availability immediately after main PO commitment | GHS 48,000; not a second award or contract value |
| Additional Petty purchase | 1 Barcode × 750 = GHS 750 |
| Campaign budget remaining after both receipts | 100,000 − 52,000 − 750 = GHS 47,250 |
| Current Barcode stock after Petty | 21 units / GHS 14,750 |
| Current PVC stock after issue and return | 20 − 2 + 1 = 19 units / GHS 36,100 |
| Main invoice paid amount | GHS 0 — no payment demonstration |

These stock/budget values are the last reconciled campaign position around **08:30 UTC on 7 September**, not live telemetry. Recheck before presenting if another team member has posted transactions. The historical count shows Barcode 20 because it preceded the Petty receipt; that is not a contradiction.

## Part B — fresh-run transaction script

### B0. Preparation gate — do this before a live fresh demonstration

- [ ] Record the environment/build and use DEFAULT, Operations department and cost centre OPS; confirm the financial year is open and currency is GHS.
- [ ] Verify every needed account can log in; restore only authorized temporary responsibilities and test their scope. Never use the maker for its independent approval.
- [ ] Use a unique reference prefix, for example `LOCAL-DEMO-20260907-A`. Let the ERP generate document numbers. Do not type the historical IDs into business fields.
- [ ] Arrange a **fresh approved GHS 100,000 budget and GHS 55,000 plan**, or an explicitly approved equivalent allocation. The existing budget's GHS 47,250 remaining is insufficient for another GHS 55,000 PR. Do not silently increase it.
- [ ] Use the existing known Goods items and a verified supplier. Fresh supplier onboarding needs an accessible email and OTP/token delivery, followed by approval and portal login. Reusing Harbourline skips onboarding; disclose that choice.
- [ ] Check published workflow, method/threshold policy, compatible evaluation template and controlled tender-document version. Their local configuration is not automatically deployed by a code pull.
- [ ] Choose future submission/opening dates with enough time for bidding. The historical 5 September dates are expired. Use only authorized accelerated local-test windows, never represent them as compliant public-notice periods.
- [ ] Prepare separately named, clearly labelled simulated technical proposal, commercial proposal, supplier compliance documents, delivery evidence and any bond/signed-contract evidence. No genuine certificates or signatures are being asserted.
- [ ] Record opening warehouse quantities and values. If replaying the main quantities against today's balance, expected ending stock will differ from the historical figures.
- [ ] Confirm the required accounting mappings through the existing setup; coordinate unresolved Finance-owned errors with that owner. No real bank/payment action belongs in this script.

**Navigation rule:** use the process-flow sidebar for the next stage and module menus for registers. “Open document register” is navigation; “Publish tender” changes tender state; formal bid opening is a later committee-controlled action. They are not interchangeable.

### B1. Create and approve the procurement budget

**Maker:** `procurementofficer` → Procurement → Planning → [Budgets](http://localhost:3000/procurement/planning/budgets) → New.

Enter the fresh reference, Operations, open FY2026, GHS 100,000, valid effective date, and Goods allocation GHS 100,000. **Check GHS explicitly**: a USD default was observed earlier. Save, reopen and check Draft/zero usage, then Submit once using the ERP confirmation.

**Handoff:** `financereviewer` → open that budget → review allocation → Approve with a simulated-UAT comment.

**Pass:** Approved; independent Finance reviewer in workflow history; amount/allocation agree. Any later budget revision is a separate request and independent approval, not an immediately applied maker edit.

### B2. Publish market analysis before completing the plan

**Actor:** `procurementofficer` → Planning → [Market Analysis](http://localhost:3000/procurement/planning/market-analysis) → New.

Select **PM-BARCODE-DEVICE / Barcode Device Kit**, GHS, current test dates, historical price 700, estimate 750 and 14-day lead time. For a simulation, label survey entries “Simulated Vendor A/B/C — not received quotations”, with 700, 750 and 800 per EA. Save and Publish.

**Pass:** Published, three survey entries, average 750. Do not use an unknown catalogue product in the customer run: that path remains an open observation. These are planning survey entries, not three submitted RFQ offers.

### B3. Plan, four reviews, then publish

**Maker:** `procurementofficer` → Planning → [Plans](http://localhost:3000/procurement/planning/plans) → New. Link the new approved budget, Operations, Annual plan, current period and GHS 55,000 plan cap.

Add Barcode **20 EA × 750 = 15,000**, linked to the Barcode analysis. Add PVC Pipe 50mm **20 × 2,000 = 40,000**, using its saved UOM and appropriate specification. Link both to Goods allocation. Set valid future delivery dates. Do not attach the Barcode analysis to PVC merely to fill a field. Final sourcing method is not chosen on the plan item.

Submit. Sign out/in for **this exact review order**:

1. `manager` — Department Head review.
2. `procurementapprover` — Head of Procurement review.
3. `financereviewer` — Finance review.
4. `employee` — Managing Director final approval.

Return as `procurementofficer` and Publish the approved plan.

**Pass:** Active/published plan; two approved lines totaling 55,000; four distinct reviewers in history. Do not assume approval alone means publication.

### B4. Record the controlled manual GHANEPS exchange

**Actor:** `procurementofficer` → Planning → [APP submissions](http://localhost:3000/procurement/planning/app-submissions).

Generate export for the new published plan/revision; inspect its package and server-generated reference. Record submission with a unique `LOCAL-UAT-ONLY` reference, then record the simulated acknowledgement only as an explicitly labelled test event. Retain package/version/checksum and the event history. The negative rejection/resubmission branch is not needed in the customer happy path.

**Pass:** Acknowledged record for the exact published plan revision. This demonstrates controlled exchange, **not an actual GHANEPS registration or transmission**. In a real procurement use the actual external registration/submission and authentic response evidence required by the approved process; do not fabricate acknowledgement.

### B5. Create the PR from approved plan items

**Maker:** `procurementofficer` → open the Active plan → select both approved lines → Create/open Purchase Requisitions → **Stock replenishment** → **Process 2 items**.

Open the new Draft PR. Check Operations/OPS, GHS 55,000, both source lines and dates. If category is blank, choose **Goods** on Edit. Upload a clearly named current simulated specification using the document selector; wait for its clean/usable DMS status. Review required details and acknowledged APP linkage; Submit for Approval once.

**Handoff:** `employee` → open the PR → assigned MD approval → Approve.

**Pass:** Approved; maker differs from approver; two 20-unit source lines; sourcing ready. The account named `procurementapprover` is not automatically the approver for every PR: follow the assigned published workflow.

### B6. Follow the sourcing decision from the approved PR

**Actor:** `procurementofficer` → approved PR → inspect recommended sourcing method → **Create Tender** when the approved policy recommends NCT.

For the GHS 55,000 Goods example, the local policy routes to NCT. The PR-driven action creates/retains the sourcing release and case; do not separately create a duplicate case. Inspect source lineage using the sidebar if needed.

**Pass:** One linked sourcing release/case and tender draft, with both PR items. RFQ, Petty, QBS and QCBS are separate policy-driven branches; do not force their records through this NCT wizard.

### B7. Prepare and approve the tender

**Maker:** `procurementofficer` → continue the PR-derived tender wizard.

Use Goods/NCT, ITB, GHS and inherited items. Choose the **standard Goods evaluation strategy before a compatible template**; do not enable QCBS for this Goods example. The verified standard template is “LOCAL UAT - Standard Goods Evaluation”. Set future submission/opening dates and clear simulated terms. Confirm quantities, specification and lot totals before saving.

Keep technical/commercial proposal requirements under their proper proposal classification. If the demonstration includes business registration, tax clearance or financial statements, configure those as the actual supporting-document requirements **before publication**, with matching labelled test files. That expanded supporting-document route needs a separate rehearsal; it was not covered by the original two-proposal test.

Save the existing draft, Submit for Approval. **`procurementapprover`** independently reviews and approves.

**Pass:** Approved, not yet Published; correct method/template; one lot/two items and intentionally configured requirements. No premature document issue or bid opening.

### B8. Controlled documents and publication

**Actor:** `procurementofficer` → tender process sidebar → **Open document register**, under Controlled documents and publication.

Bind the approved effective document/version, check issue/fee terms and the required publication/advertisement evidence. Obtain any pending document approval from the assigned reviewer. Return to the tender and **Publish Tender** after the readiness conditions pass. Do not issue documents to bidders before publication. After publication, record document issue/sale to the actual recipient using saved records; retain any fee/receipt evidence required by the terms.

**Pass:** Published with publication history and usable controlled documents. An open NCT is not invitation-only: zero invitations is not the same as zero eligible bidders. A recorded document recipient identifies who received that exact version; it does not turn NCT into a restricted supplier list.

### B9. Supplier submits the bid before closing

**Actor:** supplier `michael.martey@proton.me` → [External portal](http://localhost:3000/external-portal) → Available Tenders → the **new** published tender → create bid.

Quote Barcode **20 × 700 = 14,000** and PVC **20 × 1,900 = 38,000**, total **GHS 52,000**, delivery 45 days for this simulated scenario. Complete terms, upload separately named technical/commercial proposals, and upload every actual supporting document configured on that tender. Verify each file appears under its intended requirement before submitting once.

**Pass:** Submitted before deadline; saved bid reference, amounts and files visible after reopening My Tender Bids. Check supporting-file count separately from the Proposals section. Do not substitute a portal registration for authentic GHANEPS eligibility evidence where required by the applicable tender.

### B10. Committee, attendance, quorum and bid opening

**Organizer:** `procurementofficer` → tender process sidebar → Committee controls. Use the tender-specific evaluation committee, with the prepared appointments below.

| Account | Assignment | Own-account actions |
| --- | --- | --- |
| `tdc0102-checker-201531` | Chair | Accept invitation, record the test conflict declaration, confirm attendance, perform authorized Chair controls |
| `procurementevaluator` | Secretary | Accept invitation, record declaration and attendance; retain meeting/opening record |
| `finance.manager` | Voting member | Accept invitation, record declaration and attendance |

Create the meeting for after submission closes. Activate/complete the required appointments and collect each member's own response. For the tested configuration, minimum quorum is two **and requires the Chair and Secretary**; the historical meeting had all three present. A “No conflict” entry must only describe the simulated case, not invent a real disclosure.

As `procurementofficer`, confirm quorum after the members' attendance is complete. After the actual deadline, use the tender register's **Close** action. From the Closed tender → Bids → View Details, use **Open bid** and its **Mark Bid as Opened** confirmation at/after the opening time. Closing submissions and opening the sealed bid are separate actions. Do not reopen an expired historical tender or backdate a new one to save time.

**Pass:** Closed/opened in the correct sequence; accepted members, meeting record, attendance, quorum and opening history retained. Evaluation documents become available only at the permitted stage.

### B11. Evaluate, verify and award

**Evaluators:** each appointed member signs in separately and opens the bid's evaluation action. Use the compatible Standard Goods template. For the simulated scoring example enter **90 for each criterion**, with test-only reasons: Financial Stability 10%, Price Competitiveness 20%, Payment Terms 10%, Quality of Delivery 10%, Technical 50%. Total is 90%; template passing score is 80%. Submit each independent evaluation and the required recommendation.

**Organizer / award preparation:** `procurementofficer` → tender → Award / Award readiness. Review current evaluation results, bidder verification and any genuine policy blockers. If **Verify selected** is available, select the bidder and complete the actual configured verification/evidence action; a selection alone is not a passed verification. Supplier risk remains a feature, but a risk workflow is mandatory only where the applicable policy requires it.

**Final approver:** `employee` → assigned award approval → approve **GHS 52,000** with simulated evidence. Do not use the original supplier-onboarding reviewer to bypass the supplier/award segregation check. Do not type GHS 51,000 without the required authorized negotiation record and revised approval.

**Pass:** Approved award to Harbourline for 52,000, independent approver and current readiness evidence. Send the award notification once as `procurementofficer`; allow delivery time rather than repeatedly sending it.

### B12. Bond, PO and required Supply contract

**Bond, when required:** `procurementofficer` requests it with a clearly labelled simulated template; supplier uploads a simulated submission from My Bid → Performance Bond; the assigned independent reviewer reviews it. This is lifecycle testing, not a genuine bank guarantee.

**PO maker:** `procurementofficer` → award → **Create Purchase Order / Contract** → Purchase Order. Retain supplier, award and item lineage, GHS 52,000, warehouse **DEMO-PM** and line prices 700/1,900. Submit; **`procurementapprover`** independently approves. Check the formal budget commitment and the procurement's contract requirement.

**Contract maker:** sign in as `procurementapprover`, the draft-creator account recorded in the tested contract history; return to the award's same action → **Contract** → type **Supply**, value 52,000, agreed simulated dates/45-day term and the required evidence. Retention is an architecture requirement to capture, but **5% is not an architecture-prescribed default**. The historical Supply test used **0% as an explicit UAT assumption**; positive retention needs its agreed clause/release terms.

Return as `procurementofficer` and submit through **Approval & activation**. **`manager`, only after the authorized independent Head-of-Procurement access is prepared**, performs the assigned review and **Revalidate and activate** when all signature/evidence/bond conditions pass. The creator does not approve its own contract.

**Pass:** PO approved; required Supply contract Active **before receipt**; one GHS 52,000 purchase exposure, not GHS 104,000 for the same PO/contract. If contract is required but inactive, stop before receiving. Do not assume every Goods purchase is contract-exempt.

### B13. Receive and inspect — accepted quantities only

**Receiver:** prepared `manager / TDC_STORES_OFFICER` → approved PO → receipt action. Warehouse **DEMO-PM / Project Demo Warehouse**, location **LOC-001**. Enter delivered quantities **20 Barcode and 20 PVC** for the full-delivery demonstration; keep inspection required where configured.

Attach the simulated delivery/waybill file through **GRN/MRN → Supplier delivery evidence → Choose File → Attach**. Then select that actual saved evidence on the inspection form. A typed waybill number alone did not satisfy the tested evidence requirement.

Save inspection results (20 accepted for each line, zero damaged/short in the full-acceptance case) and Submit. **Prepared `procurementapprover / TDC_STORES_MANAGER`** independently approves inspection.

**Pass:** Receipt Accepted, inspection Closed, exactly the accepted quantities posted once. If only 10 units were accepted, only those 10 become eligible for stock and invoicing; ordered quantities are not a substitute for acceptance.

### B14. Complete controlled GRN/MRN issuance

**Prepared `procurementapprover`:** record the Approving Officer attestation on each receipt document. **Prepared `manager`:** record the separate Stores attestation, then **Issue GRN**, followed by **Issue MRN** once its prerequisite is satisfied.

**Pass:** Both documents Issued/Reconciled, distinct signatories and retained DMS versions. MRN and exact attestation counts are configured implementation controls, not a claim that the architecture universally mandates this exact screen. Test PDF opening separately; saved/issued status does not prove a visible PDF was delivered.

### B15. Record the supplier invoice against accepted supply

**Maker:** `ap.officer` → Finance → Accounts Payable → [Invoices](http://localhost:3000/finance/ap/invoices) → [Record Invoice](http://localhost:3000/finance/ap/invoices/create).

Select Harbourline from the saved supplier records, then the **new PO** and a unique simulated supplier-invoice reference. Review receipt-derived available quantities. For a fully accepted, previously uninvoiced delivery, the expected lines are **20 × 700** and **20 × 1,900**, total **52,000**. Use the invoice's actual agreed tax treatment; do not invent a tax rate to force this example total.

Save once and reopen the saved invoice. This is **manually initiated, receipt-linked invoice entry**, not an automatically generated supplier invoice from the GRN. If no accepted quantities remain, do not enter ordered quantities manually to force a duplicate invoice.

Select **Re-evaluate** matching. Review PO, accepted GRN/certificate and invoice supplier/currency/quantities/prices under the configured tolerances. Resolve discrepancies through the source process before Submit for Approval.

**Pass:** Mandatory three-way matching Passed; exact PO-line/accepted-supply lineage retained. Invoice entry does not create another goods receipt or duplicate stock movement.

### B16. Complete three independent invoice reviews

Open the same saved invoice after each sign-out/sign-in:

1. **`accounts.officer`** — Accounts Officer Review → inspect matching → Approve.
2. **`finance.manager`** — Finance Manager Approval → inspect matching → Approve.
3. **`financial.controller`** — Financial Controller Final Approval → inspect matching → Approve.

**Pass:** Approved, Completed workflow, three distinct review entries, balanced AP journal, Paid Amount **0**. The AP maker does not approve its own invoice. Refresh read-only workflow state after a successful approval if the previous button lingers; do not click it a second time. **Stop before payment.** Finance implementation and payment permission changes are outside this walkthrough.

### B17. Reconcile inventory and create the Stores requisition

**Prepared Stores viewer:** Inventory → Items & Catalogue → [Warehouse Items](http://localhost:3000/inventory/warehouse-items) → DEMO-PM. Record the new receipt's movements and before/after quantity/value. Do not confuse the historical count or a legacy movement list with the current posted inventory ledger.

**Requester:** `employee` → [Inventory Requisitions](http://localhost:3000/inventory/requisitions) → create request for Operations, **2 PVC Pipe 50mm**, warehouse DEMO-PM, location LOC-001, required date and a simulated departmental-use reason. Save and Submit through the ERP confirmation.

**Pass:** Requisition submitted; no stock issue yet. Requester, issuer and approver must be distinct.

### B18. Approve, issue and acknowledge receipt

**Prepared `procurementapprover / TDC_STORES_MANAGER`:** open the requisition → **Approve Inventory Requisition**.

**Prepared `manager / TDC_STORES_OFFICER`:** same register → **Issue Items** → quantity 2, movement reason **DEPARTMENT_CONSUMPTION**, designated receiver **Jane Employee**. Post once.

**Recipient `employee`:** open **Issue vouchers → Acknowledge receipt** and confirm the actual simulated delivery.

**Pass:** Posted SIV, 2-unit stock reduction and separate recipient acknowledgement. Historical source cost was **2 × 1,900 = 3,800**, not the draft planning price of 2,000. On a fresh run use actual retained valuation and compare with opening stock.

### B19. Return one unused PVC unit

**Prepared `manager`:** original requisition/issue register → **Return Items** → 1 PVC against the actual SIV → reason → Save and Submit.

**Prepared `procurementapprover`:** independently Approve the return, then **Post stock**.

**Pass:** SRV Posted, original issue-cost reversal, quantity restored by 1 and linked issue/return history. For the historical case this was GHS 1,900; net issue is one PVC and stock is 19. Approval and stock posting are distinct actions; do not assume one did both.

### B20. Physical count — prepared scope only

**Not ready for an unscripted fresh live creation.** The tested count-create UI selected unrelated warehouse items and did not expose the required category/location scope; the replacement two-item count was prepared through the supported application API. Therefore browser-only count creation is **not accepted**. For today's session, review the already posted **PC-20260906-0002** instead.

For a future authorized, correctly scoped new count: `manager` starts and records the two items with actual simulated counted quantities and clean count evidence → Complete Count; `procurementapprover` performs Stores approval → `financereviewer` Finance approval → separately authorized `employee / TDC_INTERNAL_AUDIT` Audit vouch → `financereviewer` **Finance post**.

**Pass for the tested zero-variance path:** Posted, freeze released, immutable action history, no unnecessary adjustment/journal and unchanged balance. Historical counted values were Barcode 20 / PVC 19. **Today's corresponding snapshot is Barcode 21 / PVC 19; read actual current quantities for any new count.** Nonzero variance, recount, transfer, disposal and broader reconciliation remain separate unpassed scenarios.

## Part C — alternative procurement routes

All routes still require the approved source/budget controls and a PO for procurement purchases. Do not add NCT publishing and committee steps automatically to a route that does not use them. Amount bands below are **local UAT policy fixtures (TDC-F05B-NCT v8)**, not statements of statutory thresholds. Recheck the published policy when beginning a fresh branch.

| Route | Planned sequence and accounts | Test data / preparation | Evidence status |
| --- | --- | --- | --- |
| **Petty Purchase** | `procurementofficer`: plan/PR and approved-source Petty quotation; `procurementapprover`: independent quotation approval; `procurementevaluator`: recommendation; `employee`: award. Officer creates PO; Procurement Approver approves; prepared Stores actors receive/inspect and issue GRN/MRN. | Local Goods band 0–1,000; tested 1 Barcode × GHS 750, Harbourline, DEMO-PM. One quote, evidence and independent approval; no public tender bidding window. | **Positive path passed through accepted receipt and issued GRN/MRN.** Petty-specific invoice/payment not executed. PDF/negative cases remain. |
| **Standard RFQ** | Officer creates the policy-routed RFQ from approved PR; eligible suppliers submit quotations; assigned independent reviewers evaluate/recommend; configured independent award approver approves; PO → receipt → matching/Stores as applicable. | Local Goods band above 1,000 through 10,000; three quotations required. Only one eligible Goods supplier was available: prepare two more genuine test supplier accounts/contact channels. Exact RFQ screen/role sequence still needs rehearsal. | **Not executed end to end.** Do not treat three planning survey rows as three RFQ submissions. |
| **QBS** | Officer selects Consultancy and **QBS before the compatible template**; qualified consultants submit; appointed committee evaluates technical quality; authorized negotiation/approval follows the configured QBS route, then PO/required contract and service certification before matching. | Prepare an eligible consultancy supplier; do not reclassify Harbourline Goods just to bypass eligibility. Local explicit Consultancy route up to 100,000. | **Not executed.** Do not use stock GRNs/Stores issues for pure consultancy services. |
| **QCBS** | Officer selects Consultancy and **QCBS before the template**; technical evaluation and threshold decision precede controlled financial opening; apply published technical/financial weighting, rank, approve award and continue PO/contract/certification/matching. | Consultancy suppliers, compatible QCBS template, approved weights/threshold and technical/financial proposals. Do not reuse Standard Goods 90-point scoring as a QCBS result. | **Not executed.** Weights and formula must come from the configured method/template, not an invented default. |
| **Emergency** | Officer documents the exception and evidence; separately authorized Internal Audit vouches; `employee` as MD approves under the configured route; execute purchase/PO and required retrospective filing, then receipt/certification and matching. | Actual simulated emergency reason/evidence; independent Audit actor not yet prepared. Local rule uses a 7-day filing control; this is not a universal architecture time limit. | **Not executed.** No bypass of exception approval, budget or audit just to save time. |

### Completed Petty example to open today

- Plan **PP-2026-0002**, PR **PR-2026-0002**, source **TND-2026-0002 / PettyPurchase**. The underlying source number does not mean it followed an advertised NCT competition.
- [PO-2026-0002](http://localhost:3000/procurement/purchase-orders/f00862f4-712a-481b-8ee3-044489ff3934): GHS 750, Received; receipt **REC260002** accepted 1 Barcode.
- **GRN-2026-0003 / MRN-2026-0003:** issued and reconciled on 7 September. Contract was not required for this configured Petty purchase; that is not a global Goods exemption.
- Barcode stock increased from 20 / 14,000 to 21 / 14,750 exactly once. No Petty invoice or payment was created.

## Part D — if something unexpected happens

| Symptom | Safe response |
| --- | --- |
| Page loads slowly or is blank | Wait for the pending action. Check the register for its generated reference before retrying. A read-only reload recovered previous development-page failures; do not repeat Create/Submit/Approve blindly. |
| Approve / Issue / Activate is missing | Check current user, DEFAULT, record status, assigned workflow task, matching Security role and active warehouse responsibility. Completed actions intentionally disappear. Do not grant arbitrary roles or use admin as an approval bypass. |
| USD or unexpected amount | Check saved GHS currency and source-line values before saving. 55,000 is the estimate; 52,000 is the main award; 48,000 was remaining budget before Petty; 47,250 is the later campaign remainder. |
| Documents count is not what you expect | Check the tender's configured supporting requirements and their actual saved attachments. Proposal files and registration/tax/financial-statement evidence are different categories. |
| Inspection says evidence missing | Upload delivery evidence to the PO's controlled evidence area, wait for usable DMS status and select that saved record in inspection. A typed reference alone is not an uploaded file. |
| Invoice quantities are unavailable/exceeded | Check accepted inspection quantity less already invoiced quantity for the same PO line. Do not invoice unreceived/rejected goods or duplicate the main receipt's completed invoice. |
| Matching fails or account mapping is missing | Record the message/reference and hand the Finance-owned issue to its owner. Do not change source prices, tax or Finance implementation merely to get a green status. |
| PDF/email is not visible | Do not repeatedly send notifications. Show the issued record/history only and disclose the presentation gap; verify delivery separately before promising the customer a PDF. |
| A genuine defect blocks the next stage | Stop that transaction, capture reference, user/time and visible error/correlation ID. Continue the review-only demonstration from an already completed linked record, explicitly labelled as such. Never claim the blocked fresh step passed. |

Deliberate negative tests belong **after** the customer happy path: maker self-approval denial, duplicate/replay protection, expired bid dates, missing required documents, over-receipt/over-invoice, unauthorized warehouse and nonzero count variance. Do not introduce these into the 30-minute presentation unless explicitly planned.

## Part E — test record and sign-off

For each fresh step, record the new reference, actual actor, timestamp, expected versus actual state, screenshot/history and any stock/journal change. Use **Pass / Fail / Blocked / Not run**; a reviewed historical record is **Reviewed**, not a new test pass.

| Stage | New reference | Account(s) | Result / evidence / defect |
| --- | --- | --- | --- |
| Budget → plan → APP | | | |
| PR → sourcing → tender/RFQ | | | |
| Publication → bid → committee/opening | | | |
| Evaluation → verification → award | | | |
| PO/commitment → required contract activation | | | |
| Accepted receipt → GRN/MRN | | | |
| Receipt-linked invoice → 3-way match → approvals | | | |
| Stock → Stores issue → acknowledgement → return | | | |
| Count / final reconciliation | | | |
| Petty / RFQ / QBS / QCBS / Emergency (separate results) | | | |

**Completion decision:** the main positive chain and Petty receipt have recorded passes, but the alternative branches, browser-only scoped count creation, PDF presentation and remaining regression/negative observations prevent full UAT sign-off. No guarantee of zero bugs is made. This script was prepared for immediate use; exact-script fresh browser rehearsal remains pending.

### Architecture and evidence references

- **TDC ERP Architecture and Design Document (002).docx:** FR-PR-004 (PO creation), FR-PR-005 (commitments), FR-PR-006 (receipt/certificate before matching), FR-PR-007 (PO/receipt/invoice matching before payment), FR-PR-008 (contract register including retention), FR-PR-010 (exceptions, evidence, Audit and MD), FR-PR-011 (GHANEPS controlled exchange). These passages were re-read for this guide. Exact test accounts, local thresholds, MRN/signature detail and scoring fixtures are configuration/implementation choices, not verbatim architectural mandates.
- [Fresh campaign evidence and open observations](PROCUREMENT_INVENTORY_STORES_FRESH_UAT_TRACKER.md), particularly U02–U17 and EV-U17-028 through EV-U17-030.
- [Procurement test catalogue](PROCUREMENT_END_TO_END_UAT.md) and [Inventory/Stores test catalogue](INVENTORY_STORES_END_TO_END_UAT.md).
- [Inventory/Stores architecture traceability](TDC_INVENTORY_STORES_ARCHITECTURE_REQUIREMENTS.md) and [QCBS formula reference](QCBS_EVALUATION_FORMULA.md).

**Presenter's closing statement:** “This demonstration shows the completed Goods purchase and its controlled handoffs into Finance and Stores. It is local simulated evidence, not live procurement or payment. The remaining method-specific and regression tests are tracked separately before final acceptance.”
