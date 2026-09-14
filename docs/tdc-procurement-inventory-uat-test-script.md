# Procurement and Inventory UAT Test Script

> Procurement acceptance is now governed by the architecture-aligned script in
> [TDC_PROCUREMENT_ARCHITECTURE_UAT.md](TDC_PROCUREMENT_ARCHITECTURE_UAT.md). The procurement
> sections below are retained only as a shorter regression reference; use the architecture script
> for `FR-PR-001` through `FR-PR-012` sign-off.

## Purpose and status

This script covers the implemented, user-visible procurement and stores controls. Annual Procurement Plan (APP) packages and RFQ/tender exchanges are manual GHANEPS handoffs: the application generates and retains the files and checksums, while the user performs the external submission. No direct GHANEPS API connection is claimed.

Status: prepared for UAT. The final authenticated browser/API acceptance for the corrective slice is still pending runtime verification.

## UAT preconditions

1. Start the API once with the configured test database so the idempotent Procurement access-control seeder runs.
2. In **Administration → Workflow**, configure and publish these shared workflow definitions before testing:
   - `TDC Procurement Budget Approval` (`Procurement Budget`): initiator `TDC Procurement Officer`; approver `TDC Finance Reviewer`.
   - The existing Procurement Plan and Purchase Requisition workflow definitions used by the tenant.
   - `TDC Supplier Return Approval` (`Supplier Return`): initiator `TDC Stores Officer`; approver `TDC Stores Manager`.
3. In **Security**, assign distinct people the following roles, then have each person sign out and back in:
   - `TDC Procurement Officer` (budget/plan preparation and PR creation),
   - `TDC Finance Reviewer` (budget approval),
   - `TDC Requisitioner` or `TDC User Department Head` (PR creation),
   - `TDC Stores Officer` and `TDC Stores Manager` (supplier return), with the required warehouse responsibility assignments for the stores roles.
4. Have an active HR department, an approved plan item with a positive controlled estimate (or an active inventory item with a controlled fallback cost), and a **Stock Updated** GRN with accepted quantity available in the same tenant. Create these through their owning modules; do not use SQL updates.

## A. Budget workflow and plan linkage

1. Go to **Procurement → Planning → Budgets** and create a budget for the chosen HR department and fiscal year. Save it as `Draft`.
2. Open the budget and choose **Submit for approval**. Expected: it enters the shared workflow; there is no direct one-click budget approval on the budget screen.
3. As a different `TDC Finance Reviewer`, complete the assigned workflow approval. Expected: the budget status becomes `Approved` (or active according to the tenant's workflow mapping) and the audit/workflow history identifies the distinct approver.
4. Go to **Procurement → Planning → Procurement Plans → Create**. Select the same department and fiscal year.
5. In **Budget information**, open **Approved budget**. Expected: only approved/current-tenant budgets for the selected department and fiscal year are selectable; drafts, other departments, and already-linked budgets are excluded.
6. Select the approved budget, create the plan, then add required plan items and submit it through the configured plan workflow. Expected: a plan cannot complete final approval without the selected approved budget; no arbitrary matching budget is auto-selected.

## B. Purchase requisition

1. Go to **Procurement → Purchasing → Purchase Requests → New**.
2. Open the **Department** selector. Expected: it is an active HR department dropdown, not free text.
3. Add an approved plan item. Expected: the rendered line first uses the approved plan-item estimate. When no plan-item estimate exists on an otherwise permitted route, the controlled item-master cost is the fallback; a zero estimate is rejected.
4. Save the requisition and submit it through the configured shared PR workflow.
5. Negative access check: repeat as a user without a requisition role. Expected: `403 PR_LINKAGE_FORBIDDEN`.
6. Positive access check: repeat after assigning `TDC Requisitioner`, `TDC User Department Head`, `TDC Procurement Officer`, or `TDC Senior Procurement Officer`, then signing out/in. Expected: the PR is created/submitted successfully. A warehouse responsibility is not required just to create a PR.
7. API tamper check (optional): post a deliberately inflated estimate. Expected: the server derives the stored line price and total from the approved plan item or controlled fallback source rather than trusting the posted value.

## C. Manual GHANEPS exchange

1. Publish the approved procurement plan, then open **Procurement → Planning → APP Submissions**.
2. Select the published plan and choose CSV, JSON, or XML. Optionally upload a supporting document or enter a normal external reference. Expected: no file-record GUID or workflow-evidence GUID is requested from the user.
3. Choose **Generate and register export**. Expected: the server creates the package, stores and virus-scans it, calculates the SHA-256 checksum, and records the package and checksum in the immutable APP timeline.
4. Download the generated APP package, submit it through the external manual process, then record the external submission reference and acknowledgement or rejection. A rejected attempt must be resubmitted with a newly generated package and checksum.
5. Complete the approved plan/PR path through the existing sourcing flow to an eligible RFQ or tender.
6. Open **Procurement → Purchasing → RFQs** (or **Tenders**) and open its **GHANEPS Exchange** action. Complete the configured export/import, attempt, acknowledgement, retry, and reconciliation checks.
7. Expected: both APP and RFQ/tender exchanges calculate their SHA-256 checksums on the server. An externally supplied checksum, where shown, is reconciliation input only. No direct GHANEPS API connection is used.

## D. Supplier return after goods receipt

1. Go to **Inventory → Transactions → Supplier Returns** and choose **New supplier return**.
2. Select a source GRN. Expected: only current-tenant GRNs at `Stock Updated` are available, and their accepted receipt lines load.
3. Choose return quantities no greater than the unreturned accepted quantity, then select a controlled reason (Quality, Damage, Excess, Wrong, or Other). Expected: supplier, warehouse, item, and cost are derived from the source GRN; they are not trusted from the browser payload.
4. Submit the return as a `TDC Stores Officer`.
5. As a different `TDC Stores Manager`, approve the assigned return workflow. Expected: a requester cannot approve their own return.
6. As the authorised dispatch actor, dispatch the approved return. Expected: stock changes only at dispatch and the stock/audit/control-event history identifies the return and source GRN.
7. Confirm that Finance/AP debit-note and invoice treatment remains under **Finance → AP**; the supplier-return screen governs the physical return and stock movement, not a parallel Finance ledger.

## E. Posting defaults, distributions and withholding

Candidate check — 13 September 2026: these additions are not yet deployed or browser-accepted. Backend tests: 249 passed (196 API, 53 Core). Frontend tests: 111 passed. Deployment and browser acceptance remain pending. Run these steps after deployment; do not treat them as completed UAT evidence.

### E1. Supplier and item defaults

1. Open **Procurement → Business Partners → Edit → Details**. Enable **Subject To Withholding Deduction**, select the purchase **WHT Configuration**, enter the supplier's default **WHT Rate**, and select **Tax** where applicable. Save.
2. In **Options**, save payment terms, TIN, ChequeBook ID and credit limit. In **Accounts**, save the required posting accounts. Reopen and check the saved values.
3. Open **Inventory → Items → Edit → Accounts**. Confirm an authorised internal user, including SuperAdmin, can load the current tenant's GL accounts and save a mapping. External users and other tenants' accounts must remain excluded.

### E2. Receipt Distribution

1. Open an unposted Purchase Receipt. Select **Distribution** beside the receipt tabs; the button remains available on every tab.
2. Change an account using its searchable selector. Use **Split line** or **Add line** for additional rows; use the trash icon to remove a row.
3. Keep the net amount for each item/posting type unchanged. Confirm total debits equal total credits, then select **Save**. Reopen to verify the changes.
4. Use **Full page** and **Restore**. Test **Reset defaults** on the draft and confirm the reset; review and save any required overrides again.
5. Post through the normal receipt process. Reopen **Distribution**: the original posted journal is read-only.
6. Confirm the overrides affect this receipt only, not the item master. Later stock postings continue to use item defaults and existing original-journal clearing/reversal rules.

### E3. Supplier invoice and WHT decision

1. Create an invoice for the WHT-enabled supplier. At **Apply withholding to this invoice?**, select **Yes**. Check the selected configuration, supplier rate, WHT deduction and Net Payable.
2. Change the invoice's **WHT Rate (%)** and save. Reopen the draft: the saved choice and rate must remain unchanged. A rate of zero is a valid explicit override.
3. On a separate invoice, select **No**. Confirm **Subject to withholding** is off and no WHT is applied. The supplier master must remain unchanged. Use the invoice toggle to change that invoice's decision; changing supplier requires a fresh decision.
4. For a draft automatically created by landed-cost posting, open **Edit invoice** and answer **Yes** or **No** before submitting/posting the invoice. Creating the draft does not apply WHT or block receipt/landed-cost inventory posting while this decision is pending.
5. Open the saved invoice's **Distribution**. Check the proposed entries before posting and the original journal after posting. This view is read-only, including for landed-cost invoices; use **Edit invoice** to change permitted draft fields.
6. Post the invoice and continue to payment. Confirm the invoice journal contains no WHT deduction: WHT GL recognition occurs at payment, once, using the saved invoice decision/rate and applicable payment rules.

## Evidence to capture

For every scenario capture the record number, workflow/history screen, user role, date/time, correlation ID from any ProblemDetails response, and the relevant audit/control-event entries. Any 401/403/409/422 result must be recorded as an expected or unexpected outcome; do not work around it with direct database changes.
