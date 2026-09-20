# Receipt landed-cost posting and source visibility — 9 September 2026

## Scope and boundaries

- Repair the FIFO calculation for multiple charges on one receipt line; retry LC26090977 in the isolated rehearsal database only.
- Display actual receipt costs on the PO, separate from the PO plan and approved payable amount.
- Display related receipt costs on saved AP invoice details. Explicitly link a saved supplier invoice to each charge using the existing invoice-number/date and supplier fields on the cost line.
- No schema migration. No Finance posting, matching, approval, invoice amount, account mapping or permission changes. The AP invoice detail page only hosts the shared Procurement cost-summary component.
- This does **not** generate AP invoice drafts. The recommended follow-up is an explicit “Create supplier invoice draft” action, grouped by supplier, with receipt/source lineage and duplicate prevention, using the existing AP owner. Supplier reference/tax details and normal AP controls must remain intact.

## Calculation

The former calculation summed allocation quantities once per charge. For 20 PVC units with freight and handling it counted 40 units, incorrectly treating half the cost as a variance. The repair groups charges by receipt line, identifies its FIFO layer, and applies the retained-quantity ratio once to that line's total charges. All charges on the line retain the same layer reference. Different receipt lines retain their own cost/quantity ratios. Ambiguous layer identities fail explicitly instead of guessing which stock should receive a targeted charge.

The FIFO lookup uses the procurement receipt ID when the Inventory GRN is a separate snapshot, includes consumed layers for traceability, and matches the exact location/lot.

## PO and invoice display

- **PO → Overview → Actual receipt landed costs → Show details:** recorded totals by currency, posted receipt-charge totals, voucher status, source receipt link and cost/invoice references. Only posted charges enter the PO-plus-posted-charges comparison. Different currencies are never summed.
- **Saved invoice → Actual receipt landed costs → Show details:** distinguish charges explicitly linked to that invoice, charges linked elsewhere, and merely related PO/receipt costs. This is not an increase in the invoice amount due.
- **Receipt → Landed Cost → Cost Lines → Link invoice** is available to AP invoice Create/Edit/Write users. Search saved invoices, select the invoice that actually bills the charge, then explicitly link it. No invoice is created or posted by this action.
- Server validation checks company, unique supplier identity (ID/code, never name), currency, PO when present, valid invoice status, and existing links. A charge cannot silently switch to another invoice. Draft cost replacement is refused once invoice references exist.

## Verification

- Core build: passed, zero errors.
- Focused backend tests: 16 passed (7 FIFO calculation cases and 9 invoice-link/source cases).
- Focused frontend tests: 15 passed (7 summary/link tests and 8 receipt-entry regressions). Tests use a 20-second limit on this busy workstation; the original 5-second run timed out on two receipt-entry cases.
- Type-check of affected pages: only the pre-existing duplicate `fiscalPeriodId` in `frontend/src/types/finance.ts` at lines 307 and 351 remains; both declarations also exist in HEAD. No unrelated Finance type change was made.
- Six affected frontend files match the regular source and rehearsal preview, ignoring line endings. Main UAT runtime is not switched by these file mirrors.
- Before retry, read-only SQL confirmed rehearsal LC26090977 is Allocated for GHS 360 with zero posting events; its PVC FIFO layer retains all 20 units. Main UAT has zero landed-cost vouchers.
- API build: passed, zero errors (27 existing warnings). Only API/Core DLL/PDB files were replaced in the rehearsal runtime; pre-switch copies are in `local-artifacts/po-rehearsal-20260909/api/before-fifo-linkage-20260909-2015`.
- Rehearsal API restarted as PID 15920 on port 5002 with its isolated database/outbound guards. API SHA-256 `95F9ED2423BBCB9098F418A6CA04458A938D76ABFBC046CC333B8D0D7EA028F0`; Core SHA-256 `81DB4A343658AF6143779E78957A0485C86EB4B5B40BC87FD587E766F0960995`. Both match the successful build.
- Rehearsal API health returned HTTP 200 after the cold EF-model startup. Visible browser posting verification is **not complete**: after restart, the earlier session expired; the previously supplied manager credentials were rejected. The user was asked to sign in. No post retry was sent with the updated API, and no account/password/role was reset.
- There are no supplied saved freight/handling invoices to link; do not attach the charges to the goods invoice merely to satisfy a demo. The user expects draft generation from landed costs; that is a proposed follow-up, not part of the implemented existing-invoice reference action.

## Visible posting retry — passed

- The user supplied the rehearsal sign-in information and the visible browser was already signed in as John Manager in the REHEARSAL company. No password or role was changed.
- Opened REC260003 → Landed Cost, verified LC26090977 was Allocated for GHS 360, and clicked **Post to Inventory** once. The UI changed to **Posted** and disabled the posting action.
- Read-only SQL confirmed PostedDate `2026-09-09T20:43:04.6986145`, total/allocated GHS 360 and unallocated zero.
- Exactly two value-only inventory movements were created: `IMV-2609-81B6AA` for PVC GHS 276.54 and `IMV-2609-1D3FE4` for Barcode GHS 83.46. Both have quantity zero and no variance. The PVC layer retains all 20 units; RemainingValue is GHS 38,276.54. Both PVC charge allocations reference layer `f934ce32-1ff2-4759-b977-d8d2835c02ec`.
- Exactly one posted Finance event for LC26090977 links to journal `7fea7287-b105-44b2-93e2-e6f6966f44fc`, debit GHS 360 and credit GHS 360. The false FIFO variance is eliminated; no write-off account was added to force posting through.
- The same read-only check confirmed main UAT `RhemaERP` still has zero landed-cost vouchers. The original PO remains reserved for UAT.
- Visible PO verification passed: PO-2026-0003 → Overview → **Actual receipt landed costs → Show details** displayed GHS 360 recorded and posted, LC26090977 Posted, and **GHS 52,360** for PO value plus posted receipt charges while retaining the approved PO amount **GHS 52,000**. Both supplier-invoice references remain Not linked. The rehearsal dev server's first PO compilation took 172 seconds; a transient layout bundle parse error cleared after reloading the completed bundle, which also passed `node --check`. No frontend/API restart or record change was needed for that reload.

Automatic AP invoice draft generation remains a proposed follow-up, not a completed feature. Existing-invoice linking has focused regression evidence but no live freight/handling invoice was supplied for a truthful association.
