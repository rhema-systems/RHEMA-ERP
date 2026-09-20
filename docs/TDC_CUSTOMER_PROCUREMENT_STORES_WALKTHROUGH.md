# TDC customer demonstration & UAT walkthrough

**B12–B22 session guide:** use the [short, step-by-step script](TDC_B12_B22_REHEARSAL_STEPS.md) ([printable version](TDC_B12_B22_REHEARSAL_STEPS.html)). [Readiness and outstanding checks](TDC_B12_B22_READINESS.md) are separate. The material below retains the full reference and historical evidence; old dates, costs, build and migration counts are not current readiness claims.

**Supplier onboarding, budget, procurement, receipt, invoice matching and Stores**<br>
Prepared: **7 September 2026, 09:04 UTC account check** · Environment: **localhost:3000 / DEFAULT**<br>
Receipt-flow update: **9 September 2026 — GRN-only walkthrough; rehearsal screen verified**.<br>
Invoice-flow update: **10 September 2026 — separate landed-cost drafts from the goods invoice**.<br>
Physical-count update: **12 September 2026 — B20 separates counter, Stores, Finance and Audit; remaining verification is listed explicitly**.<br>
Transfer/return update: **12 September 2026 — B21 inter-bin transfer and automatic completion passed; B22 supplier-return edit and physical dispatch passed. Supplier-return Finance settlement remains pending.**<br>
Disposal update: **12 September 2026 — B23 implementation/testing in progress; live disposal not yet verified.**<br>
All amounts are **GHS**. All examples are **LOCAL UAT SIMULATION ONLY**.

## Start here — which script should I use today?

**For the customer session in 30 minutes, use Part A: the completed-record demonstration.** It follows the actual record chain without repeating approvals, publishing another tender or posting stock again. Part B is the detailed fresh-transaction script for a prepared session, not a promise that an entire new procurement can be completed in 30 minutes.

The Goods supplier-onboarding example reached approved supplier-portal access, with two contacts, two bank accounts and two verified test documents visible on both sides. The main Goods/NCT example then reached an active Supply contract, accepted receipt, issued GRN/MRN, an independently approved invoice, Stores issue/acknowledgement/return and a posted zero-variance count. The Petty example reached accepted receipt and issued GRN/MRN. These checkpoints were tested over several sessions, with fixes between stages. **An uninterrupted rehearsal of this complete script has not been performed. Full UAT is not complete.**

### Important before the customer arrives

- **GRN-only testing:** use the [rehearsal receipt](http://127.0.0.1:3002/procurement/purchase-receipts/490b5d42-1a70-438a-9240-08de2223df82) and confirm the **REHEARSAL** banner. Refresh if needed; the tab is **GRN**, with no MRN step. Stay on **127.0.0.1:3002** for every test and account change; this guide's **localhost:3000** links open the original UAT, not the rehearsal. Both production frontends have now been rebuilt and started; see B23's deployment checkpoint.
- **Control details:** on the updated frontend, policy/compliance/gate details use **Show details / Hide details** accordions. Status and any current blocker remain visible; expand the relevant row for evidence or history. Collapsing a row does not bypass a check or remove a required action.
- **Stores access — corrected 9 September in both copies:** **`manager` (John Manager)** records and submits the receipt inspection using `TDC_STORES_OFFICER`. **`procurementapprover`** independently reviews it using the restored `TDC_STORES_MANAGER` role and a fresh active **DEMO-PM** assignment. Re-login as the approver after this access change. Neither account should approve its own work. These assignments remain available during the ongoing local UAT; review them for removal after demonstrations. Audit/count Finance and contract-review access still need their separate preparation checks.
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
| `procurementofficer` | Procurement Officer / `TDC_PROCUREMENT_OFFICER` | Supplier application-fee verification/token-delivery controls; budget, market analysis, plan, PR, tender, publication, committee organization, award notification, PO maker and contract activation submitter | Role present; current browser account at preparation. Not the supplier document-review/registration approver. |
| `financereviewer` | Finance Reviewer / `TDC_FINANCE_REVIEWER` | Budget approval; third plan review; count Finance review and posting | Count Finance responsibility restored in both copies on 12 September; current rehearsal Finance review and posting verified |
| `manager` | John Manager / `TDC_USER_DEPARTMENT_HEAD` | First plan review | Role present |
| `procurementapprover` | Head of Procurement / `TDC_HEAD_OF_PROCUREMENT` | Supplier document verification and registration approval; second plan review, tender/PO approval; configured document/bond review; tested contract draft creator | Role present; do not approve its own contract or use as final Harbourline award approver: supplier-onboarding segregation blocked the latter in UAT |
| `employee` | Jane Employee / `TDC_MANAGING_DIRECTOR` | Final plan and PR approval; independent award approval; Stores requester and recipient; count Audit review | Count `TDC_INTERNAL_AUDIT` role/responsibility restored in both copies on 12 September; MD role alone is insufficient |
| `tdc0102-checker-201531` | Specification Checker / `TDC_EVALUATOR` | Committee Chair and evaluator | Role present; retain actual tender-specific appointment |
| `procurementevaluator` | Procurement Submitter / `TDC_EVALUATOR` | Committee Secretary and evaluator | Role present; username differs from display name |
| `finance.manager` | Yaw Osei / Finance Manager and `TDC_EVALUATOR` | Committee voting member/evaluator; second invoice approval | Both roles present; different responsibilities remain separate workflow actions |
| `ap.officer` | Akua Owusu / Accounts Payable Officer | Receipt-linked invoice maker and submitter | Role present |
| `accounts.officer` | Kofi Boateng / Accounts Officer | First invoice approval | Role present |
| `financial.controller` | Abena Dapaah / Financial Controller | Final invoice approval | Role present |
| `michael.martey@proton.me` | Harbourline Goods Supply Ltd supplier | Supplier portal, bid and performance-bond submission | Previously used supplier login; recheck privately before a supplier demonstration |
| New applicant's own accessible email | Applicant first; supplier user only after approval | Fresh onboarding in Part S: email OTP, application token, then approved email/password login | Supply a new email you control; do not invent an inbox or reuse Harbourline's existing email for a second registration |
| `admin` | Administrator | Authorized setup only | Never substitute Administrator for all business approvals |

### Access that must be prepared for new transactions

The following accounts use explicitly approved local-UAT access. Manager receiving and procurementapprover Stores Manager access were restored on 9 September; the count Finance/Audit responsibilities were restored in both copies on 12 September. Contract-review access still needs its separate preparation check. A listed username alone does not confirm every permission.

| Intended actor | Additional access used in the test | Scope / independence |
| --- | --- | --- |
| `manager` | `TDC_STORES_OFFICER` — restored 9 September in main UAT and rehearsal | Existing DEMO-PM warehouse scope, including LOC-001, retained. Distinct from requisition requester and Stores approver. Rehearsal receipt-source check verified Ready; refresh/re-login in the environment you will demonstrate. |
| `procurementapprover` | `TDC_STORES_MANAGER` — restored 9 September in main UAT and rehearsal | Fresh active DEMO-PM assignment, including its locations; no overnight expiry during ongoing UAT. Distinct from `manager`, who receives/submits. Sign out/in before reviewing the submitted inspection. |
| `manager` | `TDC_HEAD_OF_PROCUREMENT` for the Supply contract review | Independent of Procurement Approver (recorded creator) and Procurement Officer (activation submitter). This temporary role was removed. An equivalent independent authorized reviewer can be used only after preparation. |
| `financereviewer` | Active count Finance responsibility assignment — restored in both copies, 12 September | Project Demo Warehouse (DEMO-PM), all its locations; count Finance review/posting only. Separate from counter and Stores reviewer. |
| `employee` | `TDC_INTERNAL_AUDIT` plus Audit responsibility assignment — restored in both copies, 12 September | Project Demo Warehouse (DEMO-PM), all its locations; separate from counter/Stores/Finance. Keep for the authorized UAT, then review temporary access for removal. |

Have the authorized administrator and the Finance owner confirm the remaining demo assignments through existing administration screens before new work. Do not bypass missing access or change Finance implementation. Re-login after any authorized role change. Keep the restored manager receiving and procurementapprover Stores approval access available while this UAT is ongoing; review temporary access for removal only after the relevant demonstrations are finished.

## Part A — 30-minute completed-record demonstration

**Mode: review only.** Explain the business handoff, show status and workflow history, then move forward. Completed records usually do not show the action buttons used earlier. That is expected; do not reopen a transaction just to make a button appear. The links below are presenter shortcuts. During the explanation, point out the equivalent process-flow sidebar or module menu.

| Minutes | Account | Open and show | Suggested explanation / expected evidence |
| --- | --- | --- | --- |
| 0–3 | `procurementapprover` | [Harbourline approved registration](http://localhost:3000/administration/procurement/registrations/a5e48ae9-11ad-41cc-91d4-6e7b06384f81) | Show APP260EFAA91E, Goods, Approved, Contacts (2), Bank Accounts (2) and both Verified test documents. “The applicant verified their email, completed the fee/token stage and submitted these details; an independent staff reviewer approved them. Approval created the supplier account.” Do not approve or charge again. |
| 3–5 | Sign out; sign in `procurementofficer` | [Budget PB-2026-0001](http://localhost:3000/procurement/planning/budgets/81efe55a-9741-49ad-8ece-6e2685c18391) | “The approved Operations Goods budget is GHS 100,000. Finance reviewed it independently.” Current campaign remaining amount is GHS 47,250 after the main purchase and Petty receipt, not the earlier GHS 48,000 snapshot. |
| 5–7 | Same | [Market analysis](http://localhost:3000/procurement/planning/market-analysis/2e485512-27d3-4321-804b-ba9203da5e42), then [PP-2026-0001](http://localhost:3000/procurement/planning/plans/b3889e4f-5101-42ba-ad73-cb956cd768df) | “Market analysis informed the Barcode estimate. The GHS 55,000 plan passed Department, Procurement, Finance and MD review, then publication.” Show two lines and workflow history. |
| 7–9 | Same | [APP submissions](http://localhost:3000/procurement/planning/app-submissions), then [PR-2026-0001](http://localhost:3000/procurement/purchase-requisitions/b9a17bc2-eb37-44ba-8601-dbf7c64250fc) | “The approved plan has a controlled manual exchange record. This is a simulated acknowledgement, not an actual GHANEPS transmission. The PR retains its source lines and approval.” |
| 9–13 | Same | [Tender TND-2026-0001](http://localhost:3000/procurement/tenders/55d2a153-a1ee-4217-9a8d-6014cd27a520), then [bid](http://localhost:3000/procurement/bids/5367ed43-04bf-4b1e-a6c3-4d6b174b6b90) | “Approved PR → sourcing decision → approved tender → controlled publication → supplier bid. After closing, committee attendance/quorum and opening permitted evaluation.” Show one GHS 52,000 bid and evaluation history. Proposal files are not tax-clearance evidence. |
| 13–17 | Same | [Award](http://localhost:3000/procurement/awards/7b579964-b892-46a8-a713-475087cae333), [PO-2026-0001](http://localhost:3000/procurement/purchase-orders/a0eae5c8-1915-4c68-9e94-001354ae5a34), [Supply contract](http://localhost:3000/procurement/contracts/8a6347b8-b82b-4393-8b1c-db9e01c86a14) | “The award, PO and active Supply contract represent the same GHS 52,000 purchase, not two purchases. Commitment must not double. The performance-bond lifecycle was simulated.” Contract activation preceded receiving. |
| 17–20 | Same if read access permits; otherwise pre-authorized Stores viewer | On the main PO, open the saved receipt → GRN | Show REC260001 Accepted, inspection Closed and GRN-2026-0002 Issued/Reconciled. “Only accepted goods move into stock and become eligible for invoicing.” Avoid an unverified live PDF promise. |
| 20–24 | Sign out; sign in `ap.officer` | [Invoice VI-2026-00002](http://localhost:3000/finance/ap/invoices/64e2e5a3-efe7-468c-a57f-cde051c67b76) | Show GHS 52,000, matching Passed, Approved and Paid Amount zero. Explain `accounts.officer` → `finance.manager` → `financial.controller`; show history instead of repeating approvals. “The invoice was entered against accepted receipt quantities; it was not automatically generated as a supplier invoice PDF.” |
| 24–28 | Prepared authorized Stores viewer only | [Warehouse Items](http://localhost:3000/inventory/warehouse-items), [Requisitions](http://localhost:3000/inventory/requisitions), [Physical Counts](http://localhost:3000/inventory/physical-counts) | Show 2 PVC issued, recipient acknowledgement, 1 returned, and the historical zero-variance count. Current PVC is 19; Barcode is now 21 after Petty. If view access is unavailable, use the recorded results below and explicitly say this part is evidence review, not a live click demonstration. |
| 28–30 | `procurementofficer` | [Petty PO-2026-0002](http://localhost:3000/procurement/purchase-orders/f00862f4-712a-481b-8ee3-044489ff3934) | Show one Barcode at GHS 750, Received, REC260002 and issued GRN. Explain the shorter approved-quotation route. Close with the outstanding RFQ/QBS/QCBS/Emergency tests, not a claim that they passed. |

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

## Part S — supplier onboarding, before the budget walkthrough

**Account sequence:** new applicant/email owner → `procurementofficer` (fee verification/token delivery) → applicant (application and documents) → `procurementapprover` (document review and approval) → newly approved supplier/email login.

For today's three-minute review, use the approved **Harbourline Goods Supply Ltd / APP260EFAA91E / SUP260001** record linked in Part A. The steps below are for a **new** application. A fresh run depends on an accessible inbox, OTP/token delivery and the approved local fee-posting arrangement; it is not a guaranteed three-minute registration.

### S0. Prepare the new supplier and evidence

- New example name: **Seabright Demo Goods Ltd — LOCAL UAT**; registration category **Goods**. This is a proposed fresh fixture, not an already registered company.
- Use a **new email you control** and a designated test contact number. Write the email on your private presenter sheet. Do not invent an inbox or use the existing Harbourline email for a duplicate application.
- Have two clearly labelled simulated contacts and two **DO NOT PAY** bank-detail fixtures ready. Use approved dummy account numbers, not real customer banking details. Choose one primary contact and one primary bank account; the historical example used GHS and USD bank-account currencies, while procurement remained GHS.
- Have a business-registration placeholder and tax-clearance placeholder ready, clearly marked **LOCAL UAT ONLY — NOT A VALID CERTIFICATE**. The earlier test reused `docs/erp-architecture-diagram.pdf` for both upload slots; that proved document handling, not genuine statutory compliance. Use current simulated issue/expiry dates where required.
- Confirm the configured application fee and payment methods. The tested Goods application used **GHS 100**, which is a local configuration value, not a universal TDC/PPA fee.
- Obtain the required authorization for any new **simulated fee reconciliation/Finance posting** before S3. No real transfer or bank settlement is part of this demonstration, and this script does not authorize Finance-code/configuration changes. If that setup is not ready, review the existing reconciled application instead.

### S1. Applicant verifies the email and starts the application

**Actor:** applicant; no staff account and no approved supplier password yet. Sign out of staff access and open [Supplier application](http://localhost:3000/supplier-application).

Select the start-application tab, verification channel **Email**, enter the new email, company name and **Goods** category. Complete the displayed security challenge if enabled. Click **Send verification code** once. The email owner retrieves the newest OTP privately, enters it in the verification field and clicks **Verify and continue**.

**Pass:** a new application reference appears, initially Draft, with restricted payment-only access. Record its reference; do not expect a full approved supplier account yet. The OTP verifies contact ownership; it is **not** the later application token or supplier password.

### S2. Applicant records the configured application fee

**Actor:** same applicant → [Applicant portal](http://localhost:3000/supplier-application/portal) → **Payment**.

Check the displayed fee/currency. For the authorized local simulation, choose **Bank Transfer** and a unique reference such as `LOCAL-UAT-SEABRIGHT-APP-FEE-001`; click **Record configured payment** once. This records the applicant's claim, not proof that money settled.

**Pass:** payment pending trusted verification; applicant returns to token login. Application editing, uploads and Submit for review remain unavailable while payment is unverified. Do not record another payment merely because the application token has not arrived.

### S3. Procurement Officer verifies the fee and delivers the token

**Actor:** sign in as **`procurementofficer` / DEFAULT** → Administration → Procurement → [Supplier onboarding tokens](http://localhost:3000/administration/procurement/supplier-onboarding-tokens).

Find the **new** application/company and pending payment. Open it → **Verify and post**. Check amount, currency, payer and the authorized independent evidence; complete **Provider transaction / cashier receipt reference** and retain a clear simulated-UAT verification note. In real processing, use authentic provider/bank/cashier evidence, not only the applicant's reference. In the authorized simulation, explicitly label the evidence and posting as a test, with no real-settlement assertion. Confirm once through the ERP dialog.

**Pass:** payment Reconciled/Posted, one receipt and balanced fee journal, token Active and delivery recorded. Historical reference **SUP-REC-2026-000008** belongs to Harbourline and must not be reused for the new application. This onboarding fee is separate from the later purchase budget and supplier invoice.

If delivery fails after posting, use **Retry token delivery** only when the application exposes that recovery action. It rotates/delivers a fresh token without reposting the fee. Do not verify/post the same payment again. A “Sent” audit entry alone is not proof of usable inbox content; confirm the applicant received the token. The earlier redacted-email-body defect required repair, so never accept “Sensitive email content omitted…” as a usable token email.

### S4. Applicant opens the full application with the emailed token

**Actor:** applicant. Sign out of staff access → [Application token login](http://localhost:3000/supplier-application?tab=login). Enter the **latest application token** privately in its named field and submit once.

**Pass:** the correct company/application opens at the restricted applicant portal, with **Token Active**, **Payment Reconciled** and no outstanding payment action. Application/Documents editing is now available. This is still the applicant portal, not the approved supplier's external portal.

### S5. Capture company details, multiple contacts and multiple banks

**Actor:** applicant → **Application**.

Complete Company name, Email, Phone, Tax number, Physical address, City, Country and **Goods** category using the prepared simulated fixture. Keep the verified email consistent. Under **Contact Persons → Add contact**, enter both test contacts and select exactly one primary. Under **Bank Accounts → Add bank account**, enter both DO NOT PAY test accounts, their currencies and exactly one primary. Use the fields shown; do not enter internal business-partner IDs.

Click **Save application**, reopen Application and inspect both lists.

**Pass:** company details persist; **2 contacts and 2 bank accounts**, one primary in each list. Seeing only a legacy primary-contact/account summary is not sufficient to pass the multiple-record requirement.

### S6. Upload the required documents and submit for review

**Actor:** applicant → **Documents**. Select each configured requirement by name and upload its corresponding labelled placeholder using the file picker. Enter required simulated issue/expiry metadata. Check that the files are present under the correct requirement and their scanning state is usable; the historical configuration required business registration and tax clearance.

Return to the application header → **Submit for review** once.

**Pass:** Submitted with retained contacts, banks and required attachments. Submit/upload/delete editing should no longer be offered for the submitted application. A filename alone does not verify the document or approve the supplier.

### S7. Independent staff review verifies documents and approves onboarding

**Actor:** sign in as **`procurementapprover` / DEFAULT** → Administration → Procurement → [Registrations](http://localhost:3000/administration/procurement/registrations) → open the **new** Submitted application.

Inspect Company Details, **Contacts (2)** and **Bank Accounts (2)**, including primary flags and currencies. Review each scanned-clean file and use its **Verify** confirmation with explicit UAT-only notes. Then use the registration's approval action and proper confirmation, documenting that these are simulated records and **not genuine certificates, banking instructions or real procurement authorization**.

**Pass:** both configured documents Verified; application Approved; separate supplier master/account created; correct DEFAULT mapping and supplier-scoped access. The reviewer is different from the applicant and fee-verifying Procurement Officer. **Do not use `procurementofficer` for document verification:** its attempted review returned a permission denial in the tested run despite a visible button. Do not bypass it by granting broad admin access.

The application token/session is retired when approval transitions the applicant to a supplier account. Do not continue using token login for normal supplier operations. Retain this onboarding reviewer's identity: later award segregation may prevent the same person approving an award to this supplier.

### S8. Supplier activates the approved account and checks both data views

**Actor:** newly approved supplier/email owner. Retrieve the **Supplier portal account approved** email privately. Use the approved email identifier and emailed temporary password at the normal [ERP sign-in page](http://localhost:3000/login), not the application-token form.

Complete **Change password** if prompted, keeping both temporary and replacement credentials out of the script/screenshots. If returned to login, sign in again with the replacement password; select DEFAULT if required. Open [External portal](http://localhost:3000/external-portal) and the approved application/business-partner entry from its dashboard.

**Pass:** the correct supplier sees Company Details / Goods, both contacts, both masked bank accounts and the two Verified onboarding documents. Staff already checked the same collections at S7. Confirm the account is supplier-scoped, not a platform administrator. For the historical supplier, the review shortcut is [Harbourline supplier details](http://localhost:3000/external-portal/business-partner/a5e48ae9-11ad-41cc-91d4-6e7b06384f81); access it only under its authorized supplier account. This route uses the registration reference, not the separately created supplier-master ID.

**Handoff:** record the supplier number and approved login identifier, sign out, then use `procurementofficer` for Part B's budget/plan/PR work. In a fresh procurement, choose this **new supplier** at participation/award stages rather than silently switching back to Harbourline. The existing-record presentation still uses Harbourline throughout its original linked chain. TDC supplier onboarding is not by itself proof of genuine GHANEPS registration or tender-specific eligibility.

### Supplier-specific recovery and acceptance notes

- **Expired/invalid OTP:** use the latest message and the same email; do not guess repeatedly. The tested page lacked a resend control after a challenge; recovery used a reload/re-entry/new request while preserving CAPTCHA/rate limits. If an application was already created, locate it before requesting another to avoid duplicates. This remains a recorded UX limitation.
- **Payment posted but token missing:** inspect delivery state; recover delivery only, never pay/post again. A retried token can supersede the previous token.
- **Form Completion looks low:** the historical submitted application displayed 10% despite saved data. Inspect the actual fields and required documents rather than approving solely from the percentage.
- **Password/portal acceptance:** the earlier run verified a completed password-change state and subsequent supplier login, but did not directly observe the change screen or independently prove old-token/old-password rejection in that slice. This guide does not convert those gaps into passes.
- **Evidence:** U01 core flow passed with two contacts, two banks, two Verified placeholders and approved portal access (EV-U01-001–019). Genuine statutory compliance, all negative/security cases and a fresh uninterrupted onboarding rehearsal remain unaccepted. No new emails, fees, approvals or account changes were triggered by adding this section.

## Part B — fresh-run transaction script

### B0. Preparation gate — do this before a live fresh demonstration

- [ ] Record the environment/build and use DEFAULT, Operations department and cost centre OPS; confirm the financial year is open and currency is GHS.
- [ ] Verify every needed account can log in; restore only authorized temporary responsibilities and test their scope. Never use the maker for its independent approval.
- [ ] Use a unique reference prefix, for example `LOCAL-DEMO-20260907-A`. Let the ERP generate document numbers. Do not type the historical IDs into business fields.
- [ ] Arrange a **fresh approved GHS 100,000 budget and GHS 55,000 plan**, or an explicitly approved equivalent allocation. The existing budget's GHS 47,250 remaining is insufficient for another GHS 55,000 PR. Do not silently increase it.
- [ ] Use the existing known Goods items and a verified supplier. Fresh supplier onboarding needs an accessible email and OTP/token delivery, followed by approval and portal login. Reusing Harbourline skips onboarding; disclose that choice.
- [ ] Check published workflow, method/threshold policy, compatible evaluation template and controlled tender-document version. Match the **exact policy version, source profile, procurement method and effective dates**. A document published for policy v5 is not automatically valid for v8. Their local configuration is not automatically deployed by a code pull.
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

Use Goods/NCT, ITB, GHS and inherited items. Choose the **standard Goods evaluation strategy before a compatible template**; do not enable QCBS for this Goods example. The verified standard template is “LOCAL UAT - Standard Goods Evaluation”. Set future submission/opening dates and clear simulated terms. In **Basic Information**, record **Bid validity period (calendar days)** from the tender terms; the approver reviews this with the tender, and document binding calculates expiry from submission closing. No 30/60/90-day policy default is assumed. Confirm quantities, specification and lot totals before saving.

Keep technical/commercial proposal requirements under their proper proposal classification. If the demonstration includes business registration, tax clearance or financial statements, configure those as the actual supporting-document requirements **before publication**, with matching labelled test files. That expanded supporting-document route needs a separate rehearsal; it was not covered by the original two-proposal test.

Save the existing draft, Submit for Approval. **`procurementapprover`** independently reviews and approves.

**Pass:** Approved, not yet Published; correct method/template; one lot/two items and intentionally configured requirements. No premature document issue or bid opening.

### B8. Controlled documents and publication

**Document-step restart — TND-2026-0003:** as `procurementofficer`, open [the empty v3 Draft](http://localhost:3000/procurement/tender-documents/fd4b772b-5c76-43fd-8ba8-5e9fc96b9bf2) → **Start document preparation**, then follow B8a from step 2. This route passed through tender publication on 8 September and was reset to Approved/unbound. Demonstrate v3, not the retained Published v2. After **8 September 11:00 UTC**, use B8d before tender publication.

**Start:** the tender is **Approved**, but not Published. Sign in as `procurementofficer` and use the process sidebar → **Open document register**.

Choose the matching starting point:

- **An Effective document is already listed:** it is already bound; go to **B8c**.
- **Bind approved version is available:** a published document exists; go to **B8b**.
- **No matching published document:** prepare one using **B8a**.

For a fresh document rehearsal, use B8a. A new Draft has no uploaded file; it does **not** reset a tender's existing binding.

#### B8a. Prepare and approve a document

Have TDC's tender document ready: bidding instructions, specifications and terms—not the supplier's proposal. A clearly labelled sample file is acceptable for local UAT only.

| Step | Account | What to do |
| --- | --- | --- |
| 1. Create Draft | `procurementofficer` | Open [Controlled tender documents](http://localhost:3000/procurement/tender-documents) → **New controlled Draft**, or open a suitable published version → **Clone new Draft**. Match the tender's policy, profile, method and workflow; save. |
| 2. Start preparation | `procurementofficer` | **Start document preparation** → enter the preparation reference/comment → **Start preparation**. Expect **Awaiting document upload**. |
| 3. Upload and send | `procurementofficer` | Choose the file → **Upload controlled content** → wait for success → **Send for approval**. Check **Waiting with** for the reviewer. |
| 4. Review | Assigned reviewer: `procurementapprover` in this setup | Open the same document, review the clean-scanned file, complete required verification/checklists, then **Approve workflow**. |
| 5. Attach | `procurementofficer` | Select the reviewed file under **Controlled workflow evidence document** → **Attach eligible content**. |
| 6. Publish document | `procurementapprover` | **Publish approved version** → review and confirm. Expect **Document published**. |

**Current NCT setup:** policy **TDC-F05B-NCT · v8**; profile **TDC-ACCEPTANCE-20260720201906**; method **NCT**; workflow **TDC Sourcing Approval · v1**. Use the actual tender's settings for other routes.

**Start preparation and Send for approval advance one workflow—not two approvals. Publishing the document does not publish the tender.**

#### B8b. Bind the published document

As `procurementofficer`:

1. Return to the tender → process sidebar → **Open document register** → **Refresh** → **Bind approved version**.
2. Select the matching published version and check the submission/opening dates.
3. If the validity period is blank, copy the days stated in the approved document; do not guess. **Bid valid until** calculates automatically. The **page/clause note is optional**.
4. Check currency and free/paid document terms. If dates are suitable, leave **Request new dates for approval** unchecked.
5. Click **Bind immutable version** once. Expect the effective document to be listed and readiness **Ready**; resolve any displayed blocker first.

#### B8c. Publish the tender

As `procurementofficer`, click **Tender** → **Publish Tender**. Review the publication checks and any required advertisement evidence, then confirm when ready.

**Pass:** tender status is **Published**. Only then issue documents to suppliers and continue to **B9: supplier bidding**.

#### B8d. Date changes — exception only

<details>
<summary>Open only if submission/opening dates need changing before publication</summary>

Use this only when the application offers the action.

1. **Not bound yet:** in **Bind approved version**, select **Request new dates for approval**. **Already bound:** use **Reschedule before publication**.
2. Enter a future submission deadline later than the original, and an opening time later than both that deadline and the original opening. Select the approval workflow and enter the reason/evidence reference.
3. Submit once. Follow **Waiting with**: complete any preparation task first, then the assigned independent reviewer approves the change. Requesters must not approve their own changes.
4. Return as `procurementofficer` → **Refresh**. Continue only when the change is **Approved**, the effective dates are correct and readiness checks pass.

Requesting dates does **not** immediately change them or publish the tender. Do not backdate or bypass controls.

</details>

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

**Fresh rehearsal starting from the current UAT copy:** continue **PO-2026-0003** and its existing active **CTR-2026-00002**. Do not create a second PO or contract from the same award. The creation steps below apply only when those records do not already exist.

**Signed contract upload:** open **Contracts → CTR-2026-00002 → Documents → Upload Document**. Set **Document Type: Signed copy**, choose **CTR-2026-00002-SIMULATED-UAT-Supply-Contract.pdf**, enter **Simulated UAT only — not legally executed**, then click **Upload Document**. Open the saved document and check the contract number, Harbourline supplier, GHS 52,000 total and both simulated signature sections. Refresh the PO's contract-signature check. The test file is prepared; upload and the refreshed check are not yet verified. Do not use the unrelated supplier-registration PDF as contract evidence.

**Bond, when required:** `procurementofficer` requests it with a clearly labelled simulated template; supplier uploads a simulated submission from My Bid → Performance Bond; the assigned independent reviewer reviews it. This is lifecycle testing, not a genuine bank guarantee.

**PO maker:** `procurementofficer` → award → **Create Purchase Order / Contract** → Purchase Order. Retain supplier, award and item lineage, GHS 52,000, warehouse **DEMO-PM** and line prices 700/1,900. Submit; **`procurementapprover`** independently approves. Check the formal budget commitment and the procurement's contract requirement.

**PO line type:** choose **Stock goods**, **Non-stock goods**, or **Service** as appropriate. Catalogue selection is optional: an ad hoc line needs its approved description, quantity, unit and price. For this goods walkthrough, keep Barcode and PVC as **Stock goods**; map or create their inventory records before stock receipt. Non-stock/service lines do not require a stock warehouse and must not generate inventory movements. Choosing an ad hoc line does not permit changing the approved source terms.

**Contract maker:** sign in as `procurementapprover`, the draft-creator account recorded in the tested contract history; return to the award's same action → **Contract** → type **Supply**, value 52,000, agreed simulated dates/45-day term and the required evidence. Retention is an architecture requirement to capture, but **5% is not an architecture-prescribed default**. The historical Supply test used **0% as an explicit UAT assumption**; positive retention needs its agreed clause/release terms.

Return as `procurementofficer` and submit through **Approval & activation**. **`manager`, only after the authorized independent Head-of-Procurement access is prepared**, performs the assigned review and **Revalidate and activate** when all signature/evidence/bond conditions pass. The creator does not approve its own contract.

**Pass:** PO approved; required Supply contract Active **before receipt**; one GHS 52,000 purchase exposure, not GHS 104,000 for the same PO/contract. If contract is required but inactive, stop before receiving. Do not assume every Goods purchase is contract-exempt.

**Check the PO document before issue:** open the saved PO → **Export PDF**. Check the company, supplier, PO number/status, every item, quantity, unit, price, delivery details, agreed terms and **GHS 52,000** total. The export is a purchase-order document, not a copy of the screen's internal readiness panels. Draft/unapproved exports are marked **NOT FOR ISSUE**. **Print** opens the same PDF; use the PDF viewer's Print button. Planned landed-cost estimates are not added to the supplier's PO total by this export.

### B13. Receive and inspect — accepted quantities only

**Account:** `manager` (John Manager). Use **127.0.0.1:3002** for rehearsal; **localhost:3000** is the original UAT. The Stores Officer role was restored in both copies on 9 September. The PO must still be Approved and its required contract Active.

**1. Create the receipt — on the Receive Goods page**

1. Open the approved PO → **Receive Goods**. At the top, above **Purchase Order Summary**, check **Governed receipt source: Ready** and **Receiving responsibility: Available to you**. These are automatic status checks, not approval buttons. Use the circular refresh icon only if a status is stale after correcting a problem.
2. Complete **Receipt Date** and the supplier's **Delivery Note Number**. Leave **Requires Quality Inspection** switched on for this walkthrough.
3. For this full-delivery example, enter **Received: 20** for each item. Select **DEMO-PM / Project Demo Warehouse** and **LOC-001 – Main on both lines**. Use actual delivered quantities for a different delivery.
4. Click **Create Receipt** once. After a successful save, the system opens the **saved receipt details page**. Note its receipt number. If saving fails, stay on the form and resolve the displayed error.

**2. Attach the waybill — on the saved receipt page**

5. Stay as `manager`. Select the **GRN** tab at the top of the saved receipt. The **GRN register** button opens the same tab. **This upload is not on the earlier Receive Goods form.**
6. Under **Supplier delivery evidence**, select **Evidence type: Waybill**, enter **Document reference** and **Document date**, then use the file chooser under **Clean-scanned file** to select the simulated waybill PDF or image. Click **Attach**.
7. Confirm **Waybill ready**, **Attached** and the correct filename. Entering a delivery-note/waybill number alone does not attach a file.

**3. Record and submit inspection — on the same saved receipt**

On the updated frontend, **Inspection history & technical details** starts collapsed. DEC policy references and posting/audit details are for reference, not fields you need to complete. Quantities, required evidence, comments and action buttons remain visible.

8. Select **Quality Inspection**. If the page says no inspection case exists and shows **Initialize inspection**, click it once.
9. For the full-acceptance example, record **Accepted: 20**, **Rejected: 0** on each line and appropriate inspection notes, then click **Save inspection**. **Inspection actions → Comments (optional)** is only for an additional note; leave it blank if not needed. Neither **Save inspection** nor **Submit** requires a comment. An independent approval/rejection or exception decision still requires its own reason.
10. Under **Controlled evidence**, the **Published DMS document** list contains only current published evidence linked to this receipt or inspection, including its saved delivery attachments. Generated GRN/MRN documents and other receipts' documents are excluded. For **Waybill**, the file already attached in step 6 is linked automatically; confirm its filename and **Linked evidence**. If it is missing after an upload, click **Refresh inspection evidence**. For any additional requirement (such as an inspection report), select its correct receipt-linked published document. An attached waybill does not replace an inspection report. If the required report is absent, have it prepared/published and linked to this receipt or inspection through Document Management. Click **Submit** only after the displayed requirements are complete; do not select a GRN or MRN as substitute evidence.

**4. Independent approval**

11. After **`manager`** successfully submits and the inspection shows **Pending approval**, sign out and sign in as **`procurementapprover`**. Its **`TDC_STORES_MANAGER`** role and fresh active DEMO-PM scope were configured in **both local databases on 9 September**. Reopen the **same receipt number in the same environment** → **Quality Inspection** → review quantities and evidence → enter the approval **Comments** → **Approve**. Do not approve as `manager`. The access repair did not submit or approve the inspection for you; a Draft inspection must first be submitted in step 10.

**Pass:** Receipt Accepted, inspection Closed, exactly the accepted quantities posted once. If only 10 units were accepted, only those 10 become eligible for stock and invoicing. Continue to **B14 on this same receipt's GRN tab**; attaching the waybill does not itself issue a GRN.

**Accounting checkpoint:** Approving accepted stock also creates the receipt's GL journal automatically, using **Finance → Settings → Inventory Control** (debit) and **GRV Accrual Control** (credit). This is separate from issuing the GRN document and from any later landed-cost posting. The local rehearsal REC260003 posted GHS 52,000 to Inventory (1200) / GRV Accrual Control (2110); do not manually post it again.

**Display check:** after inspection completion, **PO → Receipts** and the receipt details page should both show **Inspection Complete**. An unfinished inspection shows **Inspection Required**. Do not repeat a completed inspection because the receipt was originally marked as requiring one.

### B14. Complete GRN issuance

**Where:** on the saved receipt → **GRN** tab (or **GRN register** button). There is **no MRN step**.

**Already issued?** Skip signing and issuing. Review the GRN and continue to B15; use **Open PDF** only to check the output. Do not cancel or reissue it to repeat the demonstration.

For a GRN that has not yet been issued:

1. Confirm inspection approval is complete. Read **Receipt document checks**; expand **Show details** only if you need to resolve a failed check.
2. **`procurementapprover`:** on the GRN card, select **Signatory role → Approving Officer** → **Sign**. Skip if that signature is already present.
3. Sign out and log in as **`manager`**, reopening the same receipt in the same environment. Select **Signatory role → Stores** → **Sign**. Skip if already signed. A signing comment is optional.
4. With both configured signatures present and checks passing, the account authorized to issue enters **Action comment / cancellation reason** → clicks **Issue** on the GRN card. This issue comment is required; it is separate from optional inspection comments. If **Issue** is unavailable, resolve the displayed check or use the configured authorized issuer; do not repeat inspection approval.

**Pass:** GRN **Issued**, with **GRN reconciliation: Reconciled** under **GRN history & technical details**. Continue to **B15** against the same accepted receipt, using only quantities not already invoiced. PDF presentation is a separate check; issued status alone does not prove the PDF opened.

**Verified rehearsal checkpoint — 9 September:** **REC260003 → GRN-2026-0004** showed **Issued**, both signatures, four history events and **6 of 6 checks passed / Ready**. No MRN card or action was displayed. This is a completed checkpoint to review, not a request to create another receipt or issue another GRN.

**Historical records:** existing MRNs and their audit history remain stored. Earlier MRN references in this guide record past tests, not current operator steps. Approval checks and saved policies remain in force.

### B15. Landed costs and supplier invoices

**Keep the two flows separate:** landed-cost invoices are generated by **Post**. The goods invoice is recorded separately against accepted GRN quantities.

**Stay in the same environment:** [Rehearsal invoices — 127.0.0.1:3002](http://127.0.0.1:3002/finance/ap/invoices) · [Main UAT invoices — localhost:3000](http://localhost:3000/finance/ap/invoices). Rehearsal deployment does not update main UAT automatically.

**Continuing the current rehearsal?** LC26090977 already has its GHS 310 and GHS 50 invoices. Start at **B15b**; do not repeat B15a.

#### B15a. Post new landed costs — `manager`

Skip this step if there are no extra charges or all the voucher's invoices are already linked. Use the receiving account with the receipt's warehouse access.

1. Open the saved receipt → **Landed Cost → Add receipt costs**. No PO estimate is required.
2. Enter each charge's type, description, amount, currency/exchange rate and **Cost supplier**. Under **Applies to**, choose all received stock items and an allocation method, or one received item. Review → **Save draft costs**.
3. Click **Allocate**. Check that allocated charges equal the voucher total and unallocated is **0**.
4. Click **Post**. Confirm each supplier, supplier invoice reference and invoice date → **Confirm Post**. Capture tax later, not here.

**Pass:** inventory posted and one AP draft per supplier/currency/bill reference, with invoice links. This does not change the goods PO total or approve/pay the invoices.

**Inventory posted but invoices missing?** Use **Retry Post → Finish invoice drafts**. Do not allocate again or add duplicate charges.

<details>
<summary>Allocation formula and GHS 360 worked example</summary>

**Allocate** splits charges across eligible received items; it does not post inventory or create invoices.

**Shared charge (ByValue):** `Line allocation = charge amount × (eligible receipt line value ÷ total eligible receipt line value)`. Calculate each charge separately in the voucher currency. For this fully accepted receipt, line value is quantity × original unit cost, before landed costs.

**Item-specific charge:** choose that item under **Applies to**. Only its matching eligible receipt lines share the charge; when there is one matching line, it receives 100%.

**Example — REC260003 / LC26090977, all amounts in GHS:** freight **310** shared **ByValue**, plus handling **50** for PVC Pipe only. The eligible receipt value is **38,000 + 14,000 = 52,000**.

| Received item | Receipt value | Shared freight calculation | Item-only handling | Total allocation |
| --- | ---: | --- | ---: | ---: |
| PVC Pipe 50mm | 20 × 1,900 = 38,000 | 310 × 38,000 ÷ 52,000 = 226.54 | 50.00 | **276.54** |
| Barcode Device Kit | 20 × 700 = 14,000 | 310 × 14,000 ÷ 52,000 = 83.46 | 0.00 | **83.46** |
| **Total** | **52,000.00** | **310.00** | **50.00** | **360.00** |

**Per-unit check:** `Additional cost per unit = line's total allocation ÷ eligible quantity`; `New unit cost = original unit cost + additional cost per unit`. PVC: **1,900 + 276.54 ÷ 20 = 1,913.8270**. Barcode: **700 + 83.46 ÷ 20 = 704.1730**. Count each line's 20 units once, even when it receives both freight and handling.

**Other methods:** ByQuantity uses eligible quantity instead of value; ByWeight/ByVolume use eligible quantity × the item's saved unit weight/volume; Equal splits the charge equally between eligible receipt lines, not individual units. The selected method applies only within the charge's **Applies to** scope. Allocations are rounded to two decimals, with the final line taking any rounding remainder so the charge reconciles exactly.

**Expected allocation:** Total **360.00**, Allocated **360.00**, Unallocated **0.00**. LC26090977 is already posted in rehearsal; review it without reposting.

</details>

#### B15b. Review generated landed-cost invoices — `ap.officer`

**Do not click Record Invoice for these charges.**

1. Go to **Finance → Accounts Payable → Invoices** and open the existing landed-cost draft, or use its link on the receipt.
2. Click **Edit invoice**. Check supplier, charges, invoice reference and date.
3. Set each line's **Tax treatment**. For **Standard**, select the applicable purchase **Tax Group**; otherwise select the correct exempt/zero-rated/out-of-scope treatment. Do not leave **Pending review**.
4. Click **Save Changes**, check the updated totals, then **Submit for Approval** when the invoice checks pass. Follow its configured approval workflow; stop before payment.

**Current rehearsal — LC26090977 (GHS 360 before tax):**

| Existing invoice | Supplier | Charge before tax |
| --- | --- | ---: |
| VI-2026-00003 | Adom Construction Ltd | GHS 310 |
| VI-2026-00004 | Seabright Demo Goods Ltd | GHS 50 |

**Pass:** existing drafts updated with the correct tax treatment; no duplicate invoices or inventory posting. If already submitted, review its status instead of submitting again.

<details>
<summary>View cost links or link an already-recorded bill</summary>

**View:** PO → **Overview → Actual receipt landed costs → Show details**. On an invoice, check **Linked to this invoice**.

**Already recorded elsewhere?** As `ap.officer`: receipt → **Landed Cost → Cost Lines → Link invoice** → select the existing bill. Check supplier, currency and charge. Do not generate another invoice for it.

Posting a landed-cost invoice clears the landed-cost accrual and credits the supplier payable; it does not add inventory value again.

</details>

#### B15c. Record the goods invoice — accepted GRN quantities only — `ap.officer`

**Only do this if the goods invoice has not already been recorded.** This is Harbourline's goods bill, separate from the freight/handling invoices above.

1. Go to **Finance → Accounts Payable → Invoices**. Search for the existing goods invoice first. If absent, click **Record Invoice**.
2. Select **Harbourline Goods Supply Ltd**, the approved PO and the supplier invoice reference/date.
3. Use only **accepted receipt quantities not already invoiced**. For the full example: **20 × 700 + 20 × 1,900 = GHS 52,000 before tax**. Do not add the separately invoiced landed costs.
4. Enter the actual tax details → **Save** once → reopen the saved invoice.
5. Continue to **B15d — Three-way invoice matching** before submitting for approval.

**No accepted quantities available?** Stop; do not manually enter ordered quantities to create another invoice.

**Pass:** goods invoice saved with the PO and accepted receipt linked. Landed-cost draft generation does not create this goods invoice.

#### B15d. Three-way invoice matching — `ap.officer`

**Match these three records: approved PO ↔ accepted GRN ↔ goods supplier invoice.** This step is for Harbourline's goods invoice, not the GHS 310 and GHS 50 landed-cost invoices.

1. Go to **Finance → Accounts Payable → Invoices** → open the saved **goods invoice**.
2. On the invoice details page, find **Mandatory three-way matching** → click **Re-evaluate**.
3. Review **Control checks**: the supplier and currency must match; prices must agree with the PO within configured tolerances; cumulative invoiced quantities must not exceed accepted receipt quantities within those tolerances. Rejected or unreceived goods must not be invoiced.
4. For this no-variance UAT case, confirm **Approval ready**, **Control passed**, and the individual checks **Passed**. Review any **Variances** before proceeding.
5. If **Approval blocked** or **Hard stop active** appears, stop and resolve the displayed issue. Otherwise click **Submit for Approval** → continue to **B16**.

**Example:** 20 Barcode Device Kits × GHS 700 and 20 PVC Pipes × GHS 1,900 = **GHS 52,000 before tax**, only when all those quantities are accepted and not already billed on another invoice.

**Pass:** the matching result allows approval and retains the PO/accepted-GRN links. If the invoice is already approved, review the result without submitting it again.

### B16. Complete three independent invoice reviews

Open the same saved invoice after each sign-out/sign-in:

1. **`accounts.officer`** — Accounts Officer Review → inspect matching → Approve.
2. **`finance.manager`** — Finance Manager Approval → inspect matching → Approve.
3. **`financial.controller`** — Financial Controller Final Approval → inspect matching → Approve.

**Pass:** Approved, Completed workflow, three distinct review entries, balanced AP journal, Paid Amount **0**. The AP maker does not approve its own invoice. Refresh read-only workflow state after a successful approval if the previous button lingers; do not click it a second time. **Stop before payment.** Finance implementation and payment permission changes are outside this walkthrough.

### B17. Reconcile inventory and create the Stores requisition

**Prepared Stores viewer:** Inventory → Items & Catalogue → [Warehouse Items](http://localhost:3000/inventory/warehouse-items) → DEMO-PM. Record the new receipt's movements and before/after quantity/value. Do not confuse the historical count or a legacy movement list with the current posted inventory ledger.

**Requester:** sign in as `employee` → **Inventory → My requisitions → New Requisition** ([open register](http://localhost:3000/inventory/requisitions)). Select Operations, **Project Demo Warehouse (DEMO-PM)**, required date and a departmental-use reason. Leave location as **All locations** if unknown; the stores officer selects the actual location when issuing. Cost centre is automatic. Under **Items → Add Item → Select an item**, search **PVC Pipe 50mm**, select it, enter **2**, then **Add → Save → Submit**. This account requests items; it does not issue stock or approve its own request.

**Pass:** Requisition submitted; no stock issue yet. Requester, issuer and approver must be distinct.

### B18. Approve, issue and acknowledge receipt

**Prepared `procurementapprover / TDC_STORES_MANAGER`:** open the requisition → **Approve Inventory Requisition**.

**Prepared `manager / TDC_STORES_OFFICER`:** same register → **Issue Items** → **Default issue location: LOC-001 - Main** → **Fill remaining quantities** (or enter **2** for a partial issue) → confirm the PVC line's quantity/location, movement reason **Department consumption**, and **Receiver: Jane Employee (requester)** → **Issue 2 Items** once. Confirm a Store Issue Voucher appears.

An **active storage location is required for every stock line at issue or receipt**, not when making the request. The default fills missing/unavailable locations; existing active selections stay unchanged and each line can be changed separately. **Fill remaining quantities** only fills the form; **Issue** posts stock. Keep the automatic receiver unless another person will collect (**Change receiver**). Leave **Advanced tracking options** closed for a normal issue.

**Recipient `employee`:** **Inventory → My requisitions** → find the same requisition → **Issue vouchers → Acknowledge receipt**. Enter a short handover comment confirming the simulated delivery. The voucher action is also available after a partial issue.

**Pass:** Posted SIV, 2-unit stock reduction and separate recipient acknowledgement. Historical source cost was **2 × 1,900 = 3,800**, not the draft planning price of 2,000. On a fresh run use actual retained valuation and compare with opening stock.

### B19. Return one unused PVC unit

**Prepared `manager`:** original requisition/issue register → **Return Items** → enter 1 PVC → select **Unused stock** → **Submit Return For Approval**. Details and notes are optional.

**Prepared `procurementapprover`:** open the same return → review the saved quantity and reason → **Approve** → **Approve return** (comments optional) → **Post**. This is a review screen; do not create or submit another return. The return creator cannot approve or post their own return. Rejection and reversal still require a reason.

**Pass:** SRV **Posted** and 1 PVC restored at its original issue cost. Open the requisition → **Items**: **Requested 2 · Approved 2 · Issued 2 · Returned 1 · Net issued 1**. The requisition stays **Issued**. Returning stock does not reopen it for another issue; use a new requisition if more stock is needed. The original issue and return history remain available.

### B20. Physical count — step by step

**Use the correct copy:** [Rehearsal — port 3002](http://127.0.0.1:3002/inventory/physical-counts) for practice; [Main UAT — port 3000](http://localhost:3000/inventory/physical-counts) for the customer session. Stay in that copy when changing accounts and keep the same count number throughout.

**Current rehearsal — complete:** **PC-20260911-0004** and **ADJ260001** are **Posted** (12 September). Separate Stores, Finance and Audit reviews were completed, then `financereviewer` posted. Verified: eight stock movements, reconciled stock/bin values, released freeze and one balanced GL journal — debits and credits each **GHS 850,444.23**. Use steps 13–14 to review the result; do not post again. Superseded rehearsal **PC-20260910-0001** and **PC-20260911-0002** remain **Cancelled**.

**Main UAT preparation:** both databases are at **484 recorded migrations**, with existing stock, count and approval records preserved. Use **B23's deployment checkpoint** for the latest build/start status; database migration parity alone is not proof of running-version parity. Do not copy rehearsal count quantities into main UAT or treat the remaining ERP-wide approval rollout as complete.

**Default bin setup:** **Administration → Inventory → Warehouses → Locations**. For a new bin, enter **Code**, **Name**, **Type: Bin**, enable **Use as default bin**, then **Add**. For an existing bin, select **Edit → Default bin → Save Changes**. Each warehouse has one active normal default bin. New warehouse-item assignments use it. Changing the default does not move stock already assigned elsewhere.

**Choose the count scope.** **Warehouse-wide** includes the whole selected warehouse, with a separate row for each item/location. **Selected location** includes only stock assigned to that location. Unlocated stock uses the warehouse default bin, without changing warehouse totals. Draft items, Excel and variances use the same scope. Older count snapshots retain their original quantities; a safe default-bin resolution is recorded in Control history. An old warehouse-total row spanning multiple bins needs a new scoped count.

#### B20.1. Prepare and check the draft

1. **Use four separate users:** `manager` — counter; `procurementapprover` — Stores Manager; `financereviewer` — Finance; `employee` — Internal Audit. Their count responsibilities cover **Project Demo Warehouse (DEMO-PM), all locations**, including LOC-001 and the default bin. Finance/Audit setup is restored in both copies; the employee/MD role alone is not the Audit permission. Confirm **Administration → Workflow → Definitions → TDC Stock Adjustment Approval** remains **Published** with an independent Stores Manager approver. Re-login after setup; do not start a count while a later reviewer lacks access.
2. **`manager`:** sign in → **Inventory → Transactions → Physical Counts → New Count**. Select **Warehouse**, then **Count scope: Warehouse-wide** or **Selected location → Location**. For this exercise, use **Project Demo Warehouse → Selected location → LOC-001 / Main → Full Count**, keep **Freeze inventory** checked, then **Create Count**. Open **Edit draft** and write down the count number. No evidence file is needed for a draft.
3. **Items:** confirm that only the chosen scope is included. Remove unwanted rows using **Remove → Remove item**. To add a missing assigned item, click **Add item → Location → search/select Item → Add item**; a location-scoped count keeps its location fixed. Each item change saves immediately. For this exercise, retain **PVC Pipe 50mm** and **Barcode Device Kit** at **LOC-001 / Main**. The support reviewer retains opening balances separately from the blind counter. Items and scope are locked after Start; do not start an empty count.

**Large item lists:** use **Search** and **Rows: 25 / 50 / 100** with the page arrows. Click **Full page** to expand Items, then **Restore** to return. **Save Counts** and the red **Submit for approval** button stay together in the bottom action bar in both views. Save all quantity changes before submitting. Hidden quantities show **xxx**. The count register shows newest counts first.

**Cancel an unwanted draft:** in the register, click its red **Cancel draft count** icon → enter **Cancellation reason** → **Cancel count**. **Keep draft** closes the popup without cancelling. The cancelled record is retained; stock quantities are unchanged.

#### B20.2. Start, record quantities and attach evidence

4. **`manager`:** click **Close** to return to the register → **Start** beside that draft → **View** again. Expect **In Progress**. Starting activates the configured stock freeze; pause receipts, issues and returns in the affected scope until the count is posted or properly cancelled.
5. **Choose direct entry or Excel.** Direct: **Items → Search → Counted Qty → Save Counts**; **Full page** is available. Attach supporting count evidence in **Details**, then go to step 8. Excel: **Items → Download count sheet**. Fill **Counted Qty**; enter **0** for none, leave blank only if not counted. Keep the other columns unchanged and save the `.xlsx` file. **Location** appears only when saved locations exist.
6. **Details → Current count sheet → Upload → Choose File.** Select the completed Excel file, review the quantities → **Save count sheet**. This saves the quantities and marks that file **Current** together. Selecting a file alone does not save. Blank rows remain uncounted; no stock adjustment is posted.
7. Confirm the uploaded filename marked **Current** and **Counted = Total Items**. To import another file, use **Replace** → select file → review → **Save count sheet**. Alternatively, correct quantities directly in **Items → Save Counts**; no replacement file is needed. Earlier files remain in **Supporting files and upload history**. In a blind count, saved inputs show **Saved**. **Attach supporting file** adds evidence only, not quantities.
8. Click **Review variance** → **Items**. Expect **Under review**; compare **System, Counted and Variance**. Search for an item → edit **Counted Qty** → **Save Counts**. **No revised Excel upload is needed.** Earlier uploads remain as evidence/history. **Save Counts** and the red **Submit for approval** button stay together at the bottom in dialog and **Full page** views. When satisfied, **Submit for approval** → confirm. Expect **Stores Approval**; stock is unchanged.

**Investigation:** an approver selects **Send for investigation**, enters the issue and clicks **Save decision**. Posting is blocked. The original counter opens the same count, records **Investigation findings → Resume review**, corrects the quantities/current sheet and submits again. All approval stages restart; prior history is preserved. An older **Awaiting review** count uses **Review variance** to enter this flow.

#### B20.3. Review and post the same count

**Administrator setup:** **Administration → Inventory → Physical Count Decisions**. Maintain decision labels and select each effect: **Approve adjustment** (advances approval; stock changes only at final Post) or **Start investigation** (blocks posting). Save changes. Keep at least one active choice for each effect; the default labels below may be renamed.

After each account change: select the same organization → **Inventory → Transactions → Physical Counts → Refresh → View** on the recorded count number. Review **Details** evidence and the variance columns in **Items** before acting. A visible button does not grant approval permission.

**Decision loading:** the choices are loaded for the saved count and its warehouse/location. Wait for **Loading decisions...** to finish. If **Could not load decisions** appears, select **Retry decisions**; if it still fails, resolve the displayed error before continuing. Do not change warehouse or substitute another reviewer to bypass access.

**Before approval/posting:** the fresh-run example is zero variance. The completed rehearsal used its saved nonzero test quantities; do not copy those figures into a new UAT count. Review the actual quantities and values for the count being approved. Stock-adjustment validation uses the same location valuation source as the count; do not substitute the item-wide average cost. Do not post main UAT to imitate the rehearsal result.

**See the costs:** an authorized reviewer opens **Items → Show costs**; use **Full page** if needed. **Count unit cost** is historical: rehearsal **PC-20260911-0004 → SKU-001** retains **GHS 1,918.85**. **Item-wide avg. cost** is current across owned locations; **Warehouse Items → Average cost** is warehouse-scoped. Compare current figures with the authoritative valuation balances for the same scope. **GHS 1,907.09** was the earlier stored item projection, not a fixed expected current cost. A later transfer, return or valuation correction must not rewrite the saved count cost. Blind counters do not see these costs.

**If no approval process is active:** a new count uses **Complete count → Ready to Post → Post**, without the Stores/Finance/Audit approval steps below. Posting permission and stock/evidence/valuation checks still apply. The prepared UAT has an active workflow, so follow steps 9–12. Already-submitted approvals retain their original route; deactivation does not silently complete them.

9. **`procurementapprover` — Stores Manager:** at **Stores Approval**, review the quantities, location and evidence → **Decision: Approve adjustment → Save decision**. Expect **Finance Approval**.
10. **`financereviewer` — Finance Reviewer:** review the variance quantity/value → **Decision: Approve adjustment → Save decision**. Expect **Audit Attestation**.
11. **`employee` — Internal Audit:** sign in separately and reopen the same count → review evidence, **Items** variances and **Control history** → **Decision: Approve adjustment → Save decision**. Expect **Ready to Post**. Do not use the counter, Stores reviewer or Finance reviewer for this stage.
12. **`financereviewer`:** sign in separately and reopen the count at **Ready to Post** → check the approved results → **Post** once. The updated page shows Post only when the server confirms this user's posting access. Expect **Posted**. This final action posts any approved variance and releases the count freeze; the earlier approvals do not post stock.

#### B20.4. Confirm the result

13. **View → Details:** confirm **Counted = Total Items**. For the zero-variance exercise, **With Variance = 0**; for the current nonzero rehearsal, confirm the saved approved variances instead. **Control history**, newest first, must show separate Stores, Finance and Audit users, then final posting.
14. **Inventory → Items & Catalogue → Warehouse Items:** select **Project Demo Warehouse** and compare balances with the opening snapshot plus the approved quantity changes. Check each affected bin and **Average cost** in **Reports → Inventory Reports → Balance Register**. Confirm freeze release and any adjustment/journal against the same count. Zero variance must not change stock/value or create an unnecessary adjustment/journal. **Details → Freeze: Yes** is the original setting; use **Posted** and the recorded freeze release as completion evidence.

**If preparation is not ready:** demonstrate historical **PC-20260906-0002 → Details (Posted) → Items → Control history** without changing it. Its **Barcode 20 / PVC 19** values are historical, not today's balances; its old attachment was a plumbing-test placeholder. Use **PC-20260911-0004** for the verified current rehearsal result.

### B21. Internal stock transfer

**Actor:** `manager`, with transfer permission and active responsibility for the selected source and destination. **Page:** Inventory → [Transfers — rehearsal](http://127.0.0.1:3002/inventory/transfers) / [main UAT](http://localhost:3000/inventory/transfers). Use the same environment throughout. **Pass — TRF26091922, including automatic completion, 12 September 2026 at 14:42 UTC.**

1. Note the item's available quantity in both bins. Click **New Transfer**. For the prepared inter-bin example, select **Project Demo Warehouse** as both source and destination; notes are optional. Select **Create Transfer** and record the draft number.
2. Open **Items → Add Item**. Select **SKU-001 / PVC Pipe**, quantity **2**, source bin **LOC-001 / Main**, destination bin **DEFAULT**. Confirm those bins are active and the source has at least 2 available, then **Add Item**. The same bin cannot be both source and destination. Use **Full page** to maximize Items and **Restore** to return; current edits are retained.
3. Use the blue **pencil (Edit)** in the register to reopen the draft; use the item row's pencil to correct its quantity, then save. With no active approval process, select **Finalize**; expect **Ready to ship**, without a fabricated approver. An active process instead requires its assigned approvals. Existing in-flight approvals retain their route.
4. Select **Ship**, enter **2** in **Qty to Ship**, check **From Bin / To Bin**, then **Ship Items**. The compact grid shows Item, From Bin, To Bin, Remaining and Qty to Ship. Use **Columns** for Requested, Already Shipped or UOM; use **Full page / Restore** for more space. Shipping cost and comments are optional for this ordinary transfer. Expect **In Transit** and a source-bin reduction of 2. For a partial dispatch, enter 1 and later use **Dispatch More** for the remainder.
5. Select **Receive**. Check the common bins above the grid; use **Columns → From Bin / To Bin** for line-level bins and **Full page / Restore** for more space. Enter **Qty to receive: 1** → **Receive Items**. Reopen Receive and record the remaining 1. Confirm the destination increases by 2 in total; do not receive the same quantity twice.
6. Receive only the quantity available in good condition. Leave missing or damaged units unreceived; the transfer stays **In Transit** until the remaining good units arrive. On the final receipt, expect **Completed** automatically. There is no separate **Close transfer** action. For a cost-free inter-bin transfer, total owned quantity/value must remain unchanged.

With no approval process, the same authorized operator can perform this ordinary transfer. Active approval routes retain their assigned approval, dispatch and receiving checks. Receiving fewer good units does not require a damage report, DMS upload or separate closing action.

**Earlier manual-close result:** `manager` saved quantity **2 → 3 → 2**, finalized, dispatched 2 from LOC-001 to DEFAULT, received **1 + 1**, then closed. No approver, carrier reference or optional action comments were entered. Final PVC quantity stayed **300** and owned value **GHS 575,654.86**. The ledger shows **GHS 3,800 out → 1,900 in + 1,900 in**, no quantity in transit, and five control actions. The first receipt attempt failed and rolled back; the corrected retry completed. [Saved reconciliation evidence](../local-artifacts/inventory-final-transfer-closed-20260912-130840346.json). This pass covers the earlier **cost-free inter-bin flow only**, not automatic completion, inter-warehouse transfers, reversals, discrepancies or charges.

**Automatic-completion result:** **TRF26091922**, PVC Pipe **2**, **DEFAULT → LOC-001**. The first receipt of **1** left it **In Transit**; the second **1** completed it automatically, without a Close action or human approval. Quantity remained **300** and stock value **GHS 575,654.86**. Verified three valuation movements, three physical stock movements and five history actions; the automatic-completion entry links to the final receipt. [Saved completion evidence](../local-artifacts/inventory-final-fresh-transfer-completion-verifier-20260912-144318012.json). This proves the ordinary, zero-charge inter-bin flow only, not the separate removed-draft-line guard correction.

**Current UI:** the transfer dialog has fixed normal width/height, equal-width tabs and internal scrolling, with pencil editing and no transfer cost columns. **History** retains the audit trail. **V5 Items → Full page / Restore passed visibly at 15:12 UTC** on completed TRF26091922: 16px viewport margins when expanded, fixed normal width after Restore, stable width across tabs, seven columns without costing, and quantities **2 / 2 / 2** unchanged. Ship and Receive also have **Full page / Restore**, compact grids and Columns in V5. Their 13 focused tests passed, including retained quantities, notes and action buttons; those two dialogs were not separately checked live. Supplier-return grids remain scrollable without Full page.

**Environment checkpoint:** main UAT and rehearsal are now at **schema 484**, including **20260912233000_InventoryTransferDraftLineCompletionGuard** and the disposal migration. Verified migration checks preserved existing business records; no transfer or supplier-return test stock transactions were made in main UAT. See **B23** for current build/start status. The **15-minute completion target was missed**. The transfer result passed, but this is not a claim that all inventory scenarios are bug-free.

### B22. Supplier return from an accepted GRN

**Actor:** `manager`, with stock-issue permission and active **Project Demo Warehouse** responsibility. **Page:** Inventory → [Supplier Returns — rehearsal](http://127.0.0.1:3002/inventory/supplier-returns) / [main UAT](http://localhost:3000/inventory/supplier-returns). **Pass — draft editing, direct completion and physical dispatch, 12 September 2026 at 15:10 UTC. Finance settlement is pending.**

1. Click **New supplier return**. Select the accepted, stock-updated GRN linked to **REC260003** in rehearsal. In main UAT, select that database's own eligible receipt. Supplier, warehouse and source bins come from the accepted receipt; only stock actually accepted and posted is eligible.
2. Select a **Return reason**, enter **1** against **PM-BARCODE-DEVICE / Barcode Device Kit**, leave other quantities blank/zero, then **Create draft**. Notes are optional. Confirm the exact source bin still has 1 available and the quantity is within the receipt's unreturned balance.
3. Record the return number. Use the blue **pencil (Edit)** to correct quantities or the reason; zero removes a draft line. **Save**. The original GRN stays fixed; use a new draft for a different GRN. **View** shows each source location and the GRN cost estimate.
4. With no active approval process, select **Complete**. Expect **Ready to dispatch**, with no human approval recorded. If a process is active, use **Submit** and the assigned independent reviewer completes **Approve** before dispatch; the final approver must not dispatch it.
5. Select **Dispatch**. The carrier tracking reference is optional. Check the return and select **Dispatch stock** once. Expect **Shipped** and **Finance resolution pending**. The authorized requester may dispatch when approval is not required.
6. Check **Stock Movement History**, **Warehouse Items** and the source bin. Quantity decreases by 1 once, with the return reference and exact bin. The movement uses current carrying value; the draft's GRN cost estimate remains historical. Reopening the shipped return must not offer another dispatch.

**Chosen supplier outcome: credit against the original goods invoice.** The following Finance steps are being implemented; **not yet deployed or live-verified**. Do not treat the earlier physical-dispatch pass as a Finance pass.

7. Sign in as an authorized AP officer. Open this shipped return with the **eye** icon → **Create credit draft**. Select the original posted goods invoice, enter the supplier's credit reference and date, then **Create draft**. The returned quantities, price and tax come from the original invoice; do not enter a second stock reversal.
8. Click **Open credit**. Review the supplier, original invoice, returned lines and credit total. Use the **pencil** to correct the draft's credit reference/date, then **Save**. If approval is inactive, click **Continue**. If active, click **Submit for approval** and have the configured reviewer approve it.
9. The authorized poster selects **Post**, checks the invoice and amount in the confirmation, then confirms **Post** once. This applies the commercial credit directly to the invoice. No cash payment or second physical stock movement is created.
10. Return to **Supplier Returns**, refresh and expect **Credit applied**. Open the original invoice: its outstanding balance must decrease by this credit. Verify the dispatch/credit journals are balanced, the return's Finance status remains resolved after reload, and stock is unchanged by credit posting.

**Setup gate:** Finance must configure Supplier Returns Clearing and Purchase Return Cost Variance before the integrated posting test. Existing uninvoiced or ambiguous returns remain visibly Finance-pending. Do not use the legacy Finance/AP returns screen to imitate completion. Record physical dispatch and Finance resolution separately.

**Completed rehearsal:** **SRT-20260912131108-4cdc2070a96** is **Shipped — Finance resolution pending**. Draft editing passed: saved Barcode **2** plus PVC **1** (GRN estimate **GHS 3,300**), reopened, then removed PVC and reduced Barcode to **1** (**GHS 700**). **Complete** produced **Ready to dispatch**, with no human approver. **Dispatch** reduced Barcode item/warehouse/LOC-001 stock **430 → 429** and value **GHS 303,953.97 → 303,247.10**. Exactly one physical and one valuation movement recorded **1 unit / GHS 706.87**; the historical GRN cost remained **GHS 700**. Edit and Dispatch were no longer offered. PVC and the posted physical count were unchanged. [Saved dispatch evidence](../local-artifacts/inventory-final-supplier-dispatched-verifier-20260912-151007516.json). Review this shipped record; do not dispatch it again. No supplier debit/credit note or Finance settlement was created.

**Display labels (V6):** View resolves **Source bin** to its code and name within the return's warehouse. The draft column is **GRN unit cost**, meaning the original receipt cost, not today's carrying cost. The production build and **23 focused UI tests passed**. Final live label confirmation is pending because the browser blocked automated navigation. No stock or costing calculation changed, and this cleanup adds no migration.

### B23. Inventory stock disposal

**Status: Implementation/testing in progress; live disposal not yet verified.** The browser currently reports **ERR_BLOCKED_BY_CLIENT**. No disposal reference or live pass is claimed. Both main UAT and rehearsal are now at **schema 484**. Fresh COPY_ONLY/CHECKSUM backups were verified; checks across **86 protected business tables** confirmed that stock, count and human-approval records were unchanged. Preserve the completed B21/B22 records.

**Deployment checkpoint — 12 September, 17:06 UTC:** **120 API, 229 Core and 14 UI tests passed.** Both production frontends are built and running: rehearsal **3002**, main UAT **3000**. Both APIs passed live/readiness checks; four page routes and 61 assets per copy returned HTTP200. Build IDs, matching source hashes, CORS and anonymous disposal-API rejection (401) were verified. [Release evidence](../local-artifacts/disposal-release-verification.json). These checks do not replace the blocked live disposal walkthrough.

**Page:** Inventory → [Disposals — rehearsal](http://127.0.0.1:3002/inventory/disposals) / [main UAT](http://localhost:3000/inventory/disposals). **Actor:** an authorized Stores user with disposal and stock-posting rights for the chosen warehouse/bin. Verify that account's access first. Finance mappings and the posting period must be ready.

1. Click **New disposal**. In **Details**, select the warehouse, **Method** and **Reason**. Notes are optional. For the first controlled test, use **Write-off** and a small available quantity of an ordinary stock item. This screen does not dispose of fixed assets.
2. In **Items**, search for the item and bin by code/name. The warehouse's default bin is suggested; confirm it is the bin holding the stock to be disposed of. Enter a positive quantity → **Add**. Enter lot/batch/serial details only for tracked items.
3. Correct quantities directly in the compact grid; use the remove icon to take out a line. Use **Search item or bin**, **25 / 50 / 100 per page**, **Columns → Cost and value / Tracking**, and **Full page / Restore** as needed. Costs are hidden initially; newly added lines show **On save** until the server calculates the estimate.
4. In **Supporting documents**, select a current published document by name if needed. Files are optional when no approval process is active; the active approval route requires supporting evidence. Any attached file still has to pass the shared document-security checks. Do not type document IDs.
5. Click **Save draft** and record the generated reference. Reopen with the register's pencil icon; add/remove an item or change a quantity, then **Save draft** again. Draft saving must not change stock. The eye icon opens View. To test cancellation, use a separate draft's cancel icon and enter its reason; do not cancel the disposal being posted.
6. View the saved draft. With no active approval process, select **Continue** → **Ready to post**; no reviewer or fabricated approval is required. With an active process, select **Submit for approval** and have its eligible independent actor **Approve**. Existing in-flight approval history must remain intact. There is no separate compulsory stock-disposal committee outside the configured route.
7. Check **Details** before posting. **Auction / Sale** requires buyer, positive proceeds and a searchable proceeds account. **Donation** requires a recipient. **Write-off / Destruction** has no sale proceeds. The disposal reference defaults to the case reference.
8. Select **Post** and confirm **Post** once. For a new ready disposal, preparation and final stock/Finance posting run in one transaction; a failure rolls them back together. Correct the displayed issue and retry the same disposal. An older, already-prepared disposal reuses its linked adjustment; it must not create another. Any configured approval still applies and must finish before stock posts.
9. Expect **Completed** after successful posting. Review **Posted stock value**, the exact source-bin reduction, stock movement, valuation and Finance journal. New adjustments use the execution date, not the draft creation date; an existing prepared adjustment retains its saved date. The saved **Estimated stock value** is not a promise of the final carrying cost. Verify balanced postings and any sale/auction proceeds separately. **History** must show newest actions first, with no invented human approval in the direct path.
10. Reopen the completed record and verify that Post is no longer available. Record the actual reference, before/after quantity and value, movement/journal references and screenshot/history evidence. A controlled retry must not duplicate stock or Finance posting.

**Architecture boundary:** Sections **16.1**, **18.2** and **20.1** cover shared item/location/valuation/approval/ledger/audit controls and configurable routing for inventory disposals. **FR-FA-008's Board of Survey requirement belongs to the separate fixed-asset disposal process**; it is not an automatic three-person committee prerequisite for ordinary stock disposal. Stock, tenant, location, access, document security, accounting and duplicate-posting safeguards still apply.

**Technical verification limit:** Multi-line FIFO disposal is not verified. Different lot/serial rows for the same item and bin can reuse an opening-layer estimate; where layer costs differ, posting can fail the protected valuation check. The transaction must roll back without stock or Finance posting. Do not mark this case passed or bypass the valuation guard.

## Part C — alternative procurement routes

All routes still require the approved source/budget controls and a PO for procurement purchases. Do not add NCT publishing and committee steps automatically to a route that does not use them. Amount bands below are **local UAT policy fixtures (TDC-F05B-NCT v8)**, not statements of statutory thresholds. Recheck the published policy when beginning a fresh branch.

| Route | Planned sequence and accounts | Test data / preparation | Evidence status |
| --- | --- | --- | --- |
| **Petty Purchase** | `procurementofficer`: plan/PR and approved-source Petty quotation; `procurementapprover`: independent quotation approval; `procurementevaluator`: recommendation; `employee`: award. Officer creates PO; Procurement Approver approves; prepared Stores actors receive/inspect and issue GRN. | Local Goods band 0–1,000; tested 1 Barcode × GHS 750, Harbourline, DEMO-PM. One quote, evidence and independent approval; no public tender bidding window. | **Historical positive path passed through accepted receipt and issued GRN/MRN; MRN is now hidden from the receipt screen.** Petty-specific invoice/payment not executed. PDF/negative cases remain. |
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
| No Published template matches the tender | Compare its exact locked policy version/profile/method with the document template. Complete B8a–B8d for a matching version; do not change the tender's locked lineage or weaken readiness checks. |
| Document was submitted without a file / generic Pending approval | Starting preparation intentionally precedes upload. Stay as the recorded preparer, upload the file, then Send for approval. Check the current task/Waiting with panel; do not create a duplicate request. Older running builds may still show the former button labels until refreshed after deployment. |
| Approval is complete but tender is not published | Verify/attach eligible content and Publish approved version first, bind that document to the tender, then separately Publish Tender. These are different records and actions. |
| Invoice quantities are unavailable/exceeded | Check accepted inspection quantity less already invoiced quantity for the same PO line. Do not invoice unreceived/rejected goods or duplicate the main receipt's completed invoice. |
| Matching fails or account mapping is missing | Record the message/reference and hand the Finance-owned issue to its owner. Do not change source prices, tax or Finance implementation merely to get a green status. |
| PDF/email is not visible | Do not repeatedly send notifications. Show the issued record/history only and disclose the presentation gap; verify delivery separately before promising the customer a PDF. |
| A genuine defect blocks the next stage | Stop that transaction, capture reference, user/time and visible error/correlation ID. Continue the review-only demonstration from an already completed linked record, explicitly labelled as such. Never claim the blocked fresh step passed. |

Deliberate negative tests belong **after** the customer happy path: maker self-approval denial, duplicate/replay protection, expired bid dates, missing required documents, over-receipt/over-invoice, unauthorized warehouse and nonzero count variance. Do not introduce these into the 30-minute presentation unless explicitly planned.

## Part E — test record and sign-off

For each fresh step, record the new reference, actual actor, timestamp, expected versus actual state, screenshot/history and any stock/journal change. Use **Pass / Fail / Blocked / Not run**; a reviewed historical record is **Reviewed**, not a new test pass.

| Stage | New reference | Account(s) | Result / evidence / defect |
| --- | --- | --- | --- |
| Supplier OTP → fee/token → details/documents → approval → portal login | | | |
| Budget → plan → APP | | | |
| PR → sourcing → tender/RFQ | | | |
| Publication → bid → committee/opening | | | |
| Evaluation → verification → award | | | |
| PO/commitment → required contract activation | | | |
| Accepted receipt → GRN | | | |
| Receipt-linked invoice → 3-way match → approvals | | | |
| Stock → Stores issue → acknowledgement → return | | | |
| Count / final reconciliation | | | |
| B21 Earlier transfer → partial receipt → manual close | TRF26090177 | manager | Pass — cost-free LOC-001 → DEFAULT, 2 received as 1 + 1; 12 Sep 2026 13:08 UTC |
| B21 Current transfer → partial receipt → automatic completion | TRF26091922 | manager | Pass — 2 dispatched, received 1 + 1, automatic completion; 12 Sep 14:42 UTC |
| B22 Supplier return → dispatch | SRT-20260912131108-4cdc2070a96 | manager | Pass — draft add/edit/remove, direct Complete, Barcode 1 dispatched; Finance pending; 12 Sep 15:10 UTC |
| B22 Supplier-return Finance resolution | | | Pending — no automatic debit note / GL integration |
| B23 Inventory stock disposal → posting | | | Live disposal walkthrough blocked by ERR_BLOCKED_BY_CLIENT; no transaction pass. Both databases484; API120/Core229/UI14 and both HTTP/runtime checks passed |
| Petty / RFQ / QBS / QCBS / Emergency (separate results) | | | |

**Completion decision:** the main positive chain and Petty receipt have recorded passes, but the alternative branches, browser-only scoped count creation, PDF presentation and remaining regression/negative observations prevent full UAT sign-off. No guarantee of zero bugs is made. This script was prepared for immediate use; exact-script fresh browser rehearsal remains pending.

### Architecture and evidence references

- **TDC ERP Architecture and Design Document (002).docx:** FR-PR-004 (PO creation), FR-PR-005 (commitments), FR-PR-006 (receipt/certificate before matching), FR-PR-007 (PO/receipt/invoice matching before payment), FR-PR-008 (contract register including retention), FR-PR-010 (exceptions, evidence, Audit and MD), FR-PR-011 (GHANEPS controlled exchange). These passages were re-read for this guide. Exact test accounts, local thresholds, MRN/signature detail and scoring fixtures are configuration/implementation choices, not verbatim architectural mandates.
- [Fresh campaign evidence and open observations](PROCUREMENT_INVENTORY_STORES_FRESH_UAT_TRACKER.md), particularly supplier onboarding EV-U01-001–019, U02–U17 and EV-U17-028 through EV-U17-030.
- [Procurement test catalogue](PROCUREMENT_END_TO_END_UAT.md) and [Inventory/Stores test catalogue](INVENTORY_STORES_END_TO_END_UAT.md).
- [Inventory/Stores architecture traceability](TDC_INVENTORY_STORES_ARCHITECTURE_REQUIREMENTS.md) and [QCBS formula reference](QCBS_EVALUATION_FORMULA.md).

**Presenter's closing statement:** “This demonstration shows the completed Goods purchase and its controlled handoffs into Finance and Stores. It is local simulated evidence, not live procurement or payment. The remaining method-specific and regression tests are tracked separately before final acceptance.”
