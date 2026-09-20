# B12–B22: step-by-step rehearsal

Use this guide during the session. The [full walkthrough](TDC_CUSTOMER_PROCUREMENT_STORES_WALKTHROUGH.md) is reference material, not required reading.

**Status — 13 September 2026: operational walkthrough complete; Finance handoff pending.** Receipt, landed-cost posting, requisition issue/return, separately reviewed count, transfer receipts and physical supplier-return dispatch have passed. Invoice submission, approval/posting and supplier-credit processing are explicitly deferred to the Finance UAT team; do not treat them as an operational rehearsal blocker. Both updated copies have 580 migrations. Detailed evidence is in the [readiness checklist](TDC_B12_B22_READINESS.md).

## Before you start

1. Open [Rehearsal login — port 3002](http://127.0.0.1:3002/login), not the offline guide. Sign in and check the **REHEARSAL** company banner. Use [UAT — port 3000](http://localhost:3000/dashboard) only for the stakeholder session.
2. In the fresh copy, open **PO-2026-0003** (Draft), source **PR-2026-0003** (Approved), and **CTR-2026-00002** (Active). There is no receipt or goods invoice for this PO yet. Record the new numbers during this run; never duplicate an existing receipt, invoice or posting.
3. Use **Project Demo Warehouse (DEMO-PM)**, **LOC-001 / Main**, and GHS. Use **DEFAULT / Default bin** as the transfer destination. Confirm current stock before issuing, counting or returning; do not enter old balances as counted quantities.
4. After changing users, reopen the **same record in the same copy**. If an approval process is active, use its assigned independent reviewers. If inactive, use the displayed direct action; do not invent approval steps. Ordinary stock, accounting and access checks still apply.

Have each account's current login ready; do not replace independent reviewers with the maker. Keep passwords separately. **No supplier payment is included in this walkthrough.**

| Person | Username |
| --- | --- |
| PO maker | `procurementofficer` |
| Receiver, issuer, counter, transfer/return maker | `manager` |
| Independent Stores / PO reviewer | `procurementapprover` |
| Invoice / supplier-credit maker | `ap.officer` |
| Configured invoice / supplier-credit reviewers | `accounts.officer` → `finance.manager` → `financial.controller` |
| Supplier-credit poster | `financial.controller` |
| Count Finance reviewer/poster | `financereviewer` |
| Stock requester/recipient; separate count Audit reviewer | `employee` |

## Login run sheet — B12 to B22

Use the accounts below at each switch. The reviewer steps must use a different account from the maker. Passwords are deliberately kept outside this guide.

| Step | Sign in as | Action before the next switch |
| --- | --- | --- |
| B12.1 | `procurementofficer` | Review the PO and contract, refresh readiness, submit PO and export/check its PDF. |
| B12.2 | `procurementapprover` | Reopen the same PO and approve it. |
| B13.1 | `manager` | Create the receipt, attach the waybill, review/save Distribution and complete/submit inspection. |
| B13.2 | `procurementapprover` | Reopen the receipt and approve the inspection route, when the route is active. |
| B14.1 | `procurementapprover` | Sign the GRN as Approving Officer, if the signature is missing. |
| B14.2 | `manager` | Sign the GRN as Stores, issue it once and check the GRN PDF. |
| B15.A | `manager` | Save, allocate and post the landed-cost draft; open the generated invoice links. |
| B15.B | `ap.officer` | Edit the two generated charge invoices, select tax/WHT, save, reopen and review Distribution. |
| B15.C | `ap.officer` | Search for or record the Harbourline goods invoice, complete matching and review Distribution. |
| B16.1 | `ap.officer` | Submit each ready invoice for approval. |
| B16.2 | `accounts.officer` | Complete the first assigned invoice approval. |
| B16.3 | `finance.manager` | Complete the second assigned invoice approval. |
| B16.4 | `financial.controller` | Complete final approval or the permitted direct Post, then check the posted Distribution. |
| B17 | `employee` | Create and submit the two-PVC requisition. |
| B18.1 | `procurementapprover` | Approve the requisition if its route is active. |
| B18.2 | `manager` | Issue the two PVC units from LOC-001. |
| B18.3 | `employee` | Acknowledge the issued voucher. |
| B19.1 | `manager` | Submit the one-PVC unused-stock return. |
| B19.2 | `procurementapprover` | Approve and post that return. |
| B20.1 | `manager` | Create, start, enter/save and submit the physical count. |
| B20.2 | `procurementapprover` | Approve the Stores count-review step. |
| B20.3 | `financereviewer` | Approve the Finance count-review step. |
| B20.4 | `employee` | Approve the Audit count-review step. |
| B20.5 | `financereviewer` | Post the approved count once. |
| B21 | `manager` | Create, finalize, ship and receive the two-unit transfer in two receipts; check the transfer note. |
| B22.1 | `manager` | Create/edit/complete and dispatch the one-Barcode supplier return. |
| B22.2 | `ap.officer` | Create, save and submit the supplier-credit draft against the posted Harbourline goods invoice. |
| B22.3 | `accounts.officer` | Complete the first supplier-credit approval. |
| B22.4 | `finance.manager` | Complete the second supplier-credit approval. |
| B22.5 | `financial.controller` | Complete final supplier-credit approval and post it once. |
| B22.6 | `manager` then `financial.controller` | Confirm Credit applied on the return, then confirm reduced invoice balance and journals. |

## B12. PO and required Supply contract

**Sign in:** `procurementofficer`. **Open:** Procurement → Purchase Orders → **PO-2026-0003**.

1. Check Harbourline Goods Supply Ltd, **20 Barcode × 700 + 20 PVC × 1,900 = GHS 52,000**, DEMO-PM, and the linked Supply contract. Keep approved source quantities, units and prices.
2. Open **CTR-2026-00002 → Documents** → the existing Signed copy **CTR-2026-00002-SIMULATED-UAT-Supply-Contract.pdf**. Check the supplier, contract number, amount, signatures and **UAT only — not legally executed** label. It is already saved with a Clean scan; DMS status is Submitted, not Published. **Do not upload another copy.** If missing after restore, stop and check the copied record.
3. Confirm the required contract is **Active**, both signatories/dates are recorded, and any required bond is accepted. Return to the PO → **Refresh compliance readiness**.
4. For the prepared active route, select **Submit for Approval**. `procurementapprover` opens the same PO and approves. Do not resubmit an approved PO.
5. Select **Export PDF**; open it and check the supplier, lines and GHS 52,000 total.

**Check:** PO Approved; required contract Active; signature check passed. The existing **PB-2026-0002 / BCR-PR-2026-0003** commitment covers GHS 52,000; do not create a second commitment.

## B13. Receive, review Distribution and inspect

**Sign in:** `manager`. **Open:** approved PO → **Receive Goods**.

1. Enter receipt date and delivery-note reference **UAT-WB-PO-2026-0003**. Switch **Requires Quality Inspection** on. Enter **20 received** for each item and **LOC-001 / Main** on both lines → **Create Receipt**. Record the receipt number.
2. On the saved receipt → **GRN → Supplier delivery evidence**: select **Waybill**, reference **UAT-WB-PO-2026-0003**, and the document date. Choose [PO-2026-0003-SIMULATED-UAT-Waybill.pdf](../output/pdf/PO-2026-0003-SIMULATED-UAT-Waybill.pdf) → **Attach**. Check Barcode **20 EA**, PVC **20 EACH**, saved filename and **Waybill ready**. Do not use the old `LOCAL-UAT-ONLY-simulated-waybill.pdf`.
3. Select **Distribution**. Check the debit/credit accounts and equal totals. To test an override, use an approved account; **Split line**, **Add line** or remove a row, then **Save** and reopen. Keep each item's posting-purpose totals unchanged. Otherwise retain the defaults.
4. Open **Quality Inspection** → **Initialize inspection**, if shown. Enter **Accepted 20 / Rejected 0** on both lines → **Save inspection**. Confirm linked waybill and any other displayed required evidence → **Submit**.
5. If approval is active, sign in as `procurementapprover`, reopen this receipt, review quantities/evidence, enter any requested approval comment → **Approve**.

**Check:** receipt Accepted, **Inspection Complete**, accepted quantities posted once. Reopen **Distribution**: the original journal is balanced and read-only. Receipt overrides do not change item defaults for later stock operations.

## B14. Issue the GRN document

**Open:** the same receipt → **GRN**. If already Issued, only open/check its PDF.

1. `procurementapprover`: **Signatory role → Approving Officer → Sign**, if that configured signature is missing.
2. `manager`: reopen the same GRN → **Signatory role → Stores → Sign**, if missing.
3. `manager`: check readiness, enter the requested issue comment → **Issue** once.
4. Select **Open PDF** and check receipt number, quantities and signatories.

**Check:** GRN **Issued / Reconciled**, PDF opens. There is no MRN step and no second stock posting here.

## B15. Landed costs, invoices and three-way matching

### A. Post landed costs — `manager`

1. Receipt → **Landed Cost → Add receipt costs**. Enter freight **310**, supplier **Adom Construction Ltd**, **all received items / By item value**. Enter handling **50**, supplier **Seabright Demo Goods Ltd**, **PVC only** → **Save draft costs**.
2. Select **Allocate**. Check **Total 360 / Allocated 360 / Unallocated 0**.
3. Select **Post**. Check each supplier, invoice reference and date → **Confirm Post**.
4. Open the generated invoice links. Expect separate AP drafts for these two suppliers: **310** and **50**, before tax. If already posted/linked, use those drafts instead of posting again.

**Check:** landed cost increases stock value once; draft creation does not approve or pay the invoices. The goods PO total remains 52,000.

**Allocation:** shared charge × (item receipt value ÷ total eligible receipt value). Here freight is **226.54 PVC + 83.46 Barcode**; PVC-only handling adds **50**. Total allocation: **276.54 PVC + 83.46 Barcode = 360**. This receipt's landed unit costs are **1,913.827 PVC / 704.173 Barcode**, not a promise of the current blended stock average.

### B. Review generated charge invoices — `ap.officer`

1. Open each existing draft → **Edit invoice**. Check supplier, reference, date and amount. **Do not use Record Invoice for these charges.**
2. Complete each line's **Tax treatment** and applicable purchase **Tax Group**; leave no **Pending review**. For this explicitly approved simulated rehearsal only, use **Zero rated** for the 310, 50 and 52,000 invoices. Do not change supplier defaults or copy this test treatment into a real transaction without checking its tax requirements.
3. If asked **Apply withholding to this invoice?**, choose **Yes** or **No**. Check **Subject to withholding** and the editable rate; this is the invoice's choice, not a change to the supplier.
4. **Save Changes**, reopen, and check saved tax/WHT and totals. Open **Distribution** to review proposed accounts. Continue to B16.

### C. Record and match the goods invoice — `ap.officer`

1. Finance → Accounts Payable → **Invoices**. Search first. Only if absent, select **Record Invoice** for Harbourline, the prepared PO and accepted GRN.
2. Enter the supplier invoice reference/date and only **accepted, not-yet-invoiced quantities**. For full acceptance, the goods total is **52,000 before tax**. Do not add the separately invoiced 310/50 charges.
3. Complete tax details; answer any WHT confirmation and check its toggle/rate → **Record Invoice**. Reopen and check **Distribution**.
4. Under **Mandatory three-way matching**, select **Re-evaluate**. Confirm **PO ↔ accepted GRN ↔ invoice** agree on supplier, currency, price and eligible quantities; check **Control passed / Approval ready**. Resolve a displayed variance before proceeding.

**Check:** one goods invoice plus the two charge invoices; correct links and no duplicate bills. WHT is shown on the invoice, but its liability is recognized at payment, not invoice posting.

## B16. Complete invoice processing — Finance-owned UAT follow-up

**Deferred from this rehearsal closeout.** Leave existing invoice drafts unchanged. The steps below are retained for the Finance team’s later UAT; do not submit, approve or post an invoice during this operational closeout.

1. `ap.officer`: open each ready invoice → **Submit for Approval** if an approval route is active.
2. For the prepared three-stage route, sign in separately as **`accounts.officer` → `finance.manager` → `financial.controller`**. Each opens the assigned invoice, reviews it and selects **Approve**. Final approval posts this normal invoice automatically. Follow the actual assigned route if its configuration differs.
3. If no route is active, the permitted invoice actor uses **Post**, not an approval action.
4. Reopen the invoice → **Distribution**. Check the posted balanced journal, original receipt/landed-cost clearing and **Paid Amount 0**. Verify the saved WHT choice/rate remains unchanged.

**Check:** invoice processing complete, journal posted once, no payment made. The invoice maker does not approve their own invoice on an active route.

## B17. Request two PVC units

**Sign in:** `employee`. **Open:** Inventory → My requisitions.

1. Select **New Requisition**. Choose Operations, DEMO-PM, required date and purpose. Cost centre is automatic; leave location unspecified if unknown.
2. **Items → Add Item**: search **PVC Pipe 50mm**, quantity **2** → **Add → Save**.
3. **Submit** using the displayed route; record the requisition number.

**Check:** request saved/submitted; no stock issued yet.

## B18. Issue and acknowledge

1. `procurementapprover`: approve the same requisition if its route requires approval.
2. `manager`: **Issue Items → Default issue location: LOC-001 / Main → Fill remaining quantities**. Check quantity **2**, reason **Department consumption**, and suggested receiver **Jane Employee** → **Issue 2 Items** once.
3. `employee`: reopen the requisition → **Issue vouchers**. Enter the receiver handover comment → **Acknowledge receipt**. This button saves the acknowledgement; there is no separate Save.

**Check:** one posted issue voucher, stock reduced by 2 in the selected bin, recipient acknowledgement saved. Filling quantities alone does not issue stock.

## B19. Return one unused PVC unit

1. `manager`: original requisition → **Return Items**. Enter **1 PVC**, select **Unused stock**, leave optional notes blank → **Submit return**. The prepared Stores Return approval is active.
2. `procurementapprover` opens that saved return → **Approve → Approve return**, then authorized **Post**. Do not create a second return. Optional approval comments can remain blank.
3. Reopen requisition → **Items** and check the return voucher and bin balance.

**Check:** Requested **2**, Approved **2**, Issued **2**, Returned **1**, Net issued **1**; status stays **Issued**. Stock increases by 1 at original issue cost. The return does not reopen the issue quantity.

## B20. Physical count

**Sign in:** `manager`. **Open:** Inventory → Physical Counts.

1. **New Count**: DEMO-PM → **Selected location → LOC-001 / Main → Full Count**. Keep **Freeze inventory** on → **Create Count**. Record its number. For a whole-warehouse exercise instead select **Warehouse-wide**.
2. Open the draft → **Items**. Keep PVC and Barcode at the selected location. Test **Add item** and remove an unwanted row before starting. Verify the final two rows. A separate unwanted draft can be cancelled with the register's red cancel icon and a reason.
3. Close the draft → register **Start** → reopen. Expect **In Progress**. Pause movements in the counted scope.
4. Enter actual **Counted Qty** in **Items → Save Counts**. Use **Search**, pagination and **Full page / Restore**. For a real count, physically confirm the balances. This automated rehearsal uses explicitly labelled simulated values, not physical-stock certification; do not reuse them as tomorrow's actual counted quantities.
5. Attach the completed supporting count record in **Details**. Alternatively use Excel: **Items → Download count sheet**, complete Counted Qty, then **Details → Count sheet (optional) → Upload**, choose file, review → **Save count sheet**. Only the file marked **Current** supplies imported quantities; supporting files are evidence only.
6. **Review variance → Items**. Correct any entry directly → **Save Counts**; no edited Excel re-upload is necessary. When every item is counted and results are satisfactory, use the red **Submit for approval**, also available in Full page.
7. For the prepared active route: `procurementapprover` (**Stores**), then `financereviewer` (**Finance**), then `employee` (**Audit**) each selects **Approve adjustment → Save decision**. Expect **Ready to Post**. **Send for investigation** returns an unresolved result for correction instead of posting it. If no approval process is active, use **Complete count** and the direct posting path.
8. Authorized `financereviewer`: review the final result → **Post** once. Confirm **Posted**, released freeze and newest-first **Control history**. Compare affected bin/warehouse balances with the approved variance.

**Check:** zero variance leaves quantity/value unchanged and creates no unnecessary adjustment. A nonzero result posts only its approved difference, once, with a balanced journal. Do not post merely to reproduce an old example.

## B21. Transfer two units; receive one plus one

**Sign in:** `manager`. **Open:** Inventory → Transfers.

1. **New Transfer**: DEMO-PM as source and destination → **Create Transfer**. The dialog closes. Reopen the new draft with the **pencil → Items → Add Item**: PVC, **2**, **LOC-001 / Main → DEFAULT** → **Add Item**. Confirm at least 2 available and two different active bins.
2. Reopen with the **pencil**, check/edit the quantity and save. The prepared transfer approval is inactive: **Finalize** → **Ready to ship**, with no approval step.
3. **Ship → Qty to Ship 2 → Ship Items**. Expect **In Transit** and source bin reduced by 2.
4. **Receive → Qty to receive 1 → Receive Items**. Confirm it remains **In Transit**. Reopen Receive and receive the remaining **1**.
5. Confirm **Completed** automatically, destination increased by 2 and nothing left in transit. Test **Full page / Restore**; use **Columns** only for extra detail.

**Check:** no manual Close action; receive only good quantities actually available. For this no-charge inter-bin transfer, total owned quantity and value are unchanged.

## B22. Return one Barcode and apply the supplier credit

**Verification check:** the new **Credit from supplier return** AP entry is deployed in both copies; its role-based browser walkthrough is still pending.

**Finance portion deferred from this rehearsal closeout.** Complete and verify the physical return only (steps 1–3). Leave the credit draft, its submission, approvals, posting and invoice-balance checks for the Finance UAT team (steps 4–7).

1. `manager`: Inventory → Supplier Returns → **New supplier return**. Choose this run's accepted, stock-updated GRN. Select reason and **1 Barcode** → **Create draft**. Check available stock and unreturned receipt quantity.
2. Reopen with the **pencil**, check/edit quantity or reason → **Save**. The prepared supplier-return approval is inactive: **Complete** → **Ready to dispatch**, with no approval step.
3. Authorized dispatcher: **Dispatch → Dispatch stock** once. Expect **Shipped / Finance resolution pending**. Check the source bin fell by 1 and the movement references this return. Do not dispatch again.
4. `ap.officer`: **Finance → Accounts Payable → Supplier Debit Notes → Credit from supplier return**. Search/select this run's dispatched return → **Create credit draft**. In **Original invoice**, select the posted Harbourline goods invoice; enter supplier credit reference/date → **Create draft → Open credit**. Check returned quantity **1**, original invoice prices/tax and total. Correct draft details with the pencil if needed → **Save**. This route does not require Inventory access.
5. `ap.officer`: **Submit for approval**. Supplier-credit approval is active even though the stock-return approval is inactive. Sign in separately as **`accounts.officer` → `finance.manager` → `financial.controller`**; each reviews the same credit and selects **Approve**.
6. `financial.controller`: on the approved credit, select **Post**, check original invoice/amount → confirm **Post** once. The AP officer creates/submits; the Financial Controller posts.
7. `manager`: reopen the supplier return and check **Credit applied**. `financial.controller`: reopen the original invoice and check its reduced outstanding balance and balanced dispatch/credit journals. Confirm no second stock movement or cash payment.

**Final check:** saved references connect PO → receipt/GRN → goods/charge invoices → Stores vouchers → count → transfer → supplier return/credit. Every completed action survives reopening. Record any unresolved error; do not mark the rehearsal passed until all steps above have been verified.
