# Supplier invoice integration for Estate

Estate creates supplier invoices from its existing land-acquisition documents. The supplier workspace owns invoice review and distribution editing; Finance AP continues to own approval, accounting and payments. No purchase order is required for an invoice created by the trusted Estate handoff.

## Estate developer quick start

1. Integrate the reviewed branch's backend, frontend and migrations together. Review the target migration history and apply the approved prerequisites below before starting the updated API. Copying only the Estate controller or supplier page is insufficient.
2. Open the existing Estate workspace at `/estate/land-acquisition`. Save the acquisition's source evidence and use its existing Accounts Payable request action at the applicable stage. The endpoints below take the acquisition ID and an empty JSON body; the server resolves the supplier, amount, accounts and source identity.
3. Reuse `estateAcquisitionService.ensureAccountsPayableRequest(acquisitionId)` for the primary stage payment, or `estateAcquisitionService.ensureOtherAcquisitionCostsPayableRequest(acquisitionId)` for other costs. Both return `WorkspaceData`. Read the primary invoice ID from `result.values.accountsPayableInvoiceId`, or the other-cost invoice ID from `result.values.otherAccountsPayableInvoiceId`. Keep the returned workspace values in the Estate screen as the existing handlers do.
4. Render `<SupplierInvoiceWorkspaceButton invoiceId={invoiceId} />` once an ID exists. This checks the saved invoice's ownership and opens the correct route. It is already wired beside both Estate payable actions. Use this component for older records too; a legacy Finance invoice must retain its Finance route.
5. In the supplier workspace, review the draft, select the applicable invoice tax treatment and withholding decision, save, and review **Distribution**. Continue through the existing AP approval/posting/payment controls when the invoice is ready. Creating an Estate request alone does not perform any of those financial actions.

| Screen or integration surface | Route or source |
| --- | --- |
| Existing Estate workspace | `/estate/land-acquisition` |
| Supplier invoice register | `/procurement/supplier-invoices` |
| Supplier invoice view | `/procurement/supplier-invoices/{invoiceId}` |
| Supplier invoice edit | `/procurement/supplier-invoices/{invoiceId}/edit` |
| Existing manual/legacy Finance invoice | `/finance/ap/invoices/{invoiceId}` |
| Estate client methods | `frontend/src/services/estate-acquisition.service.ts` |
| Invoice navigation component | `frontend/src/components/procurement/SupplierInvoiceWorkspaceButton.tsx` |

The generic `/procurement/supplier-invoices/create` page is for its supported purchasing sources. Estate creation goes through its source action, not a manually populated generic create form.

## Entry points

| Estate source | Existing endpoint | Source kind |
| --- | --- | --- |
| External cadastral surveyor fee | `POST /api/estate/land-acquisitions/{id}/accounts-payable-request` at Cadastral Survey | `SurveyorFee` |
| Agreed land-vendor consideration | Same endpoint at Vendor Payment | `VendorConsideration` |
| Stamp duty | Same endpoint at Stamp Duty Payment | `StampDuty` |
| Other acquisition services/costs | `POST /api/estate/land-acquisitions/{id}/other-acquisition-costs/accounts-payable-request` at Stamp Duty Payment | `OtherAcquisitionCosts` |

The Estate action validates its current source stage and the user's existing access before handing off to AP. Use these endpoints from the originating Estate screens. Do not send an invented purchase order or a client-authored source flag to the supplier invoice create endpoint.

New Estate invoices arrive as drafts with tax review pending. Open the supplier invoice, choose the applicable treatment and complete the existing AP approval flow. The Estate payment action appears only once the invoice is ready for payment (or a payment already exists). A source request does not approve or post the invoice.

After creation, use the returned workspace's invoice ID to open `/procurement/supplier-invoices/{invoiceId}`. `SupplierInvoiceWorkspaceButton` resolves persisted invoice ownership before navigation, allowing existing Finance invoices to retain their original route. It is included beside the primary and other-costs payable actions in Land Acquisitions.

## Source cardinality and editing

The supported contract is **one invoice per tenant, acquisition and payable kind**. An acquisition may therefore have one surveyor-fee invoice, one vendor-consideration invoice, one stamp-duty invoice and one other-cost invoice. Repeat the source action to reuse its existing invoice. A simultaneous request can return a reload/retry error while the first request completes; reload the acquisition and reuse the linked invoice. Soft deletion or voiding does not release that source key for replacement billing.

Other acquisition costs are the existing Estate aggregate: service-cost rows become separate lines of one invoice under `Land Acquisition Other Service Providers` (`LAND-ACQ-OTHER-COSTS`). This contract does not create independent bills for multiple payees or multiple invoices of the same kind. Additional Estate document types, multiple surveyor bills and replacement billing need a separate source contract before callers can use them.

New source-owned invoices start in Draft with tax review pending. Supplier, source reference, line identity/description/type, quantities, prices, discounts, source GL accounts and currency/FX remain locked to the Estate evidence. Supplier invoice number, supported dates, notes and tax review use normal AP editing. Distribution lines may split an allowed source purpose while retaining its source account and balanced totals. If the Estate net amount changes after creation, AP update/submit/approve/post rejects the discrepancy for reconciliation; it does not silently rewrite the liability.

## Contract and boundaries

- The shared `VendorInvoice` read contract adds nullable `estateAcquisitionId` and `estatePayableKind`. These identify the originating acquisition and payable kind. Create-source metadata is server-only; clients cannot declare Estate ownership.
- The source link is relational, tenant-scoped and unique per acquisition/payable kind. Retrying a creation must reuse its linked invoice, not generate another bill.
- Existing invoices identified only by a text reference are not automatically adopted. A matching reference or client-editable workspace JSON is not proof of ownership.
- The supplier register/detail/edit screens show Estate origin. Source-controlled supplier, line quantities, unit prices, discounts, source GL accounts and currency cannot be replaced through invoice editing. Notes, dates and supported tax review retain AP validation.
- Distribution save is a draft operation. Posting revalidates balanced lines and retains source accounting controls. Estate source accounts remain controlled while splitting is permitted.
- Estate invoices retain the existing AP permissions, workflow and Finance SOD. The Procurement-only SOD switch must not exempt an Estate-only invoice.
- Existing Finance invoice pages remain independently owned. This change does not copy or replace the Finance team's manual-invoice implementation.

## Migration and integration review

Apply the reviewed branch migrations with the API and frontend. The Estate addition is `20260926210000_EstateSupplierInvoiceLineage`; it depends on the earlier supplier invoice, partner account and distribution schema changes in this branch. Review the target database's migration history first. Do not run the verification-only posting-book repair script against another database without a separate schema review.

The shared supplier workspace uses the branch's receipt/Auto Invoice, landed-cost document, partner-role/account, supplier-tax fallback and editable-distribution models. In particular, review `20260924230000_ProcurementAutoInvoiceReceipts`, `20260925180442_SupplierInvoiceTaxFallback`, `20260925230000_ProcurementInvoiceDistributionDraft` and their earlier dependencies before `20260926210000_EstateSupplierInvoiceLineage`. These names are dependency landmarks, not a standalone four-migration installation list: use the complete reviewed pending migration chain for the integrated commit. Keep the EF model snapshot aligned with that chain. The accounting-period reconciliation migration and verification repair scripts require their own target-schema review; they are not instructions to open a financial period.

Shared integration surfaces include `VendorInvoiceService`, AP DTOs/model, the AP controller's supplier workspace boundary, `ApplicationDbContext`, its snapshot, and the four existing Estate payable handoffs. Resolve overlap with Finance/Estate changes at these shared boundaries instead of replacing whole files. The original Finance AR invoice pages have no task edits; the original Finance AP create page retains the `GoodsReceiptConsolidation` enum compatibility addition.

## Verification status

Implementation and final checks are in progress. This document is the integration contract, not a release sign-off. Before merge readiness is claimed, record the final normal build, focused backend/UI test results, migration review and the live verification limits below.

Current local development uses `http://localhost:3000`, API `http://127.0.0.1:5003`, and only `RhemaERP_Procurement_Verification_20260925`. Startup seeding and background workers are disabled in that verification environment. The source `RhemaERP` database is unchanged.

Outstanding financial acceptance requires the invoice reviewer's tax decision, purchase tax GL mappings where applicable and an independently approved open accounting-book period. No invoice approval, payment or live GL posting is implied by successful compilation or draft distribution save.

## Backend ownership and migration review

The Estate handoff is the only writer of the two new source fields. They are ignored when deserializing public create requests; update requests have no source setters. Source creation checks the acquisition tenant, active record and current payable stage, uses a transaction with a source-specific application lock, and relies on a unique tenant/acquisition/kind index as the final concurrency guard. A second concurrent request may need to reload the existing invoice; it must not create another liability. The source key remains reserved after soft deletion or voiding. Replacement billing requires an explicit future reconciliation workflow rather than silently recreating the source invoice.

Migration `20260926210000_EstateSupplierInvoiceLineage` adds nullable acquisition/kind columns without changing existing invoice rows. Its composite foreign key references the acquisition's tenant and ID, and its check constraint requires a complete, supported Estate source without PO, accepted-supply, auto-invoice or opening-balance ownership. An update trigger prevents changing or removing source identity. EF declares that trigger. Rollback refuses to drop source evidence while any linked Estate invoice remains. It does not backfill text references or workspace JSON and does not open accounting periods or assign accounts/permissions.

The Estate controller retains authorization and stage-specific source checks. The shared AP service retains canonical supplier resolution, tax calculation, approval, posting and payment controls. Invoice edits preserve source reference, currency/FX, line identity, type, description, quantity, price, discount, unit, GL account, budget, asset and procurement links. Tax classification remains reviewable. Estate distributions may split source purposes but cannot redirect source accounts. Supplier invoice reference, dates and notes retain normal invoice editing. Before update, submission, approval and posting, the source's current net evidence must match the invoice's `SubTotal` (already net of discounts). Settlement uses gross `TotalAmount`, allowing reviewed tax to add to a fixed net Estate charge.

Do not replace the shared Finance service/controller/DTO files wholesale during integration. Review the additive Estate partial, source-query/distribution predicates, Estate handoff changes, model/snapshot and migration together. Finance's manual invoice pages and their existing ownership remain separate. Existing reference-only Estate invoices stay on their original Finance route until explicitly reconciled; calling the creation action will reject an unverified reference collision rather than adopting it.

Focused tests cover API source-metadata rejection, tenant/source boundaries, all four source kinds, model keys/FK, immutable commercial fields, reviewed-tax allowance, pending-tax refusal and typed-net versus legacy-gross reconciliation. These checks do not by themselves prove concurrent SQL execution, complete Estate workflow authorization, tax-calculated ledger posting, or payment settlement. Record the real-service, relational migration and live lifecycle results separately; retain any unexecuted acceptance checks as open.


## Invoice entry layout follow-up

The shared Procurement create/edit form now uses one compact table row per item, with a full-screen toggle and optional row details for budget/warehouse. Tax and trade discount are selected once in the invoice totals footer. Explicit selections propagate into the existing persisted line accounting fields; saved mixed values are retained until the reviewer chooses replacements. Trade discount entry uses an invoice-wide percentage with the calculated amount shown alongside. Source-controlled Estate/landed-cost discounts remain locked. Tax breakdown is expandable. Live UI verified add/remove, expansion/escape with unsaved values retained, tax dropdown stacking and discount/tax/total recalculation without saving or posting. Scoped TypeScript passed; final backend build and financial acceptance remain separate open gates.

## Supplier invoice Save fix (2026-09-26)

Reproduced the silent Save Changes failure on VI-2026-00003. The client schema rejected the API's null optional accepted-supply kind/source and PO-item links, preventing the submit handler from running. Procurement's form schema now normalizes nullable optional fields to omitted values while preserving required-field and source-kind validation. Invalid submissions show a persistent summary beside Save and a toast.

Verification: three focused schema regressions passed; scoped TypeScript passed. The live browser saved the existing draft successfully, displayed the update confirmation and opened its invoice detail. It remains Draft at GHS 200.00 with tax review pending; no approval or posting was performed. Finance's manual invoice forms were not changed.

## Supplier invoice view cleanup (2026-09-26)

Procurement invoice detail now leads with the bill. Matching controls and receipt/cost/accounting details are below it in expandable sections. Matching opens when approval is blocked; a brief top notice links to it. Procurement uses a compact presentation of shared matching controls: internal AP/DEC/TDC labels and explanatory boilerplate are omitted; an empty, ineligible exception panel is not rendered. Eligible requests, active exceptions, history, errors and approval enforcement remain available. Shared Finance screens retain their existing presentation. Duplicate supplier details and raw supplier/item/account IDs were removed from the Procurement bill body; account names are shown when available.

Five focused matching/exception presentation tests and the scoped TypeScript check passed. The invoice-first layout was visually checked on VI-2026-00004. Subsequent navigation to a PO invoice timed out in the browser, so no additional live matching-action check is claimed. No invoice transactions were changed.

## Description save/reopen fix (2026-09-26)

Procurement invoice create/edit now installs the API's saved invoice in the shared invoice query cache before navigating. It cancels older invoice fetches so they cannot overwrite that response and invalidates dependent registers/matching/workflow data. Previously, navigation could reuse the pre-edit invoice for the five-minute cache lifetime; both first-line edits and added lines were affected.

Six focused schema/cache tests and scoped TypeScript passed. Live verification on draft VI-2026-00003 replaced the main description and added a second line using Shift+Enter, saved through the UI, confirmed the exact two-line value in the isolated verification database, displayed both lines on invoice view and reopened the editor with both lines retained. Temporary verification wording was then replaced with the original Freight / Shipping description through the UI. No approval, posting or payment was performed.
