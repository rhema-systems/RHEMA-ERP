# Receipt landed-cost supplier invoice handoff — 9 September 2026

> The separate-button instructions below are historical. The current implementation uses one **Post** action and defers tax to the AP draft. See [the combined posting flow](LANDED_COST_COMBINED_POST_20260909.md) and the updated customer walkthrough for the current sequence.

## Delivered scope

- A cost supplier is selectable for each receipt charge. The AP-authorized **Create supplier invoices** action reviews supplier, bill reference/date and explicit tax choices before writing anything.
- One draft per canonical supplier, currency and bill reference. Charge lines are preserved; different bill dates are processed separately. Missing suppliers are never silently assigned to the goods supplier.
- AP owns creation, tax calculation, numbering, approval and posting. Generation returns drafts, not approved/posted invoices, and never pays a supplier.
- Posting verifies the original inventory posting and clears its actual credited account, even if current account mappings changed. It does not capitalize inventory twice. Reversed/missing source journals and mismatched supplier/currency/amount/account are rejected.
- The protected nullable `VendorInvoiceLineItem.LandedCostItemId` FK and unique active-line index prevent duplicate charge invoicing. Generic invoice JSON cannot assign this link. Unchanged retries return existing invoices. Draft edits retain source amounts/links; draft deletion releases the active reservation while retaining deleted-line history. Voided/reversed invoices require an explicit reviewed recovery, not automatic replacement.
- PO and invoice receipt-cost summaries retain links; the goods PO amount and its three-way matched invoice remain separate.

## Verification

- API build passed: zero errors (27 warnings on the final incremental build).
- New backend suite: **22 passed**. Covers grouping, distinct supplier identities with duplicate display names, separate bill references, retries, ineligible/foreign suppliers, source validation, approved accrual-clearing posting request, blocked unapproved posting, locked draft source fields, deleted-draft recovery and forged JSON source IDs.
- Frontend suite: **31 passed** across supplier selection, grouping, invoice creation dialog and existing PO/invoice source linkage.
- Affected frontend typecheck reports only pre-existing duplicate `fiscalPeriodId` declarations in `src/types/finance.ts:307,351`. That unrelated Finance file was not changed; a clean full frontend typecheck is not claimed.
- These automated checks are not live end-to-end AP approval/posting evidence.
- After restart and model warm-up, rehearsal `/health` returned HTTP 200: database, scanner, self and startup Healthy; memory Degraded (about 2.2 GB API working set). The first warm-up health request timed out; do not describe this as a clean performance pass.
- Browser sign-in as the existing manager restored REC260003 as Accepted / Inspection Complete. Its Landed Cost tab loaded LC26090977 Posted, Total/Allocated GHS 360, the unchanged GHS 310/50 charges and allocations, both Not linked, and the new disabled **Create supplier invoices** action with **An AP invoice officer can create the supplier invoices**. Manager permissions were not expanded. The AP-enabled dialog and successful creation were tested automatically, not in this live session.

## Environment and data boundaries

- Migration `20260909213000_LinkLandedCostsToSupplierInvoiceLines` applied **only** to `RhemaERP_PO_Rehearsal_20260909`. Verified nullable 16-byte link, unique active index and enabled/trusted foreign key. Exact guarded SQL: `scripts/procurement/Apply-RehearsalLandedCostInvoiceLink.sql`.
- Rehearsal API artifacts updated with matching hashes; previous DLL/PDB files retained under `local-artifacts/landed-cost-invoices-20260909/rehearsal-before-deploy`. Rehearsal API restarted on port 5002. Four changed frontend runtime files match regular source and were mirrored to `frontend-pdf-preview` on port 3002.
- Main UAT servers (3000/5000) were not restarted. `RhemaERP` still has no new invoice-link column and zero active landed-cost vouchers. Deploy the additive migration together with the matching API before enabling this feature there.
- Rehearsal **LC26090977** remains **Posted, GHS 360**, with freight **310** and handling **50**. Both supplier identities and invoice numbers remain unset. No invoice or GL posting was created by this delivery.

## Pending live acceptance

Obtain the actual billing supplier for each charge, bill references/dates and tax treatment, then sign in with an AP invoice creator. Walk through **Receipt → Landed Cost → Create supplier invoices**, verify grouped drafts and receipt/PO links, complete configured independent approval, then verify the authorized posting and its accrual/AP journal. Confirm before final approval/posting during rehearsal. Do not claim this live flow passed until those steps have evidence.
