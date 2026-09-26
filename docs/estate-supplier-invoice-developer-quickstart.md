# Estate supplier invoice: developer quick start

The supplier invoice integration is on `master` through merged PRs [256](https://github.com/rhema-systems/RHEMA-ERP/pull/256) and [257](https://github.com/rhema-systems/RHEMA-ERP/pull/257) (merge commit `70934b402970c9895843b1311df15ae0ff43db7b`). Integrate current master into your working branch, preserving your local changes. Take backend, frontend and the reviewed migration chain together.

Estate owns the source document, amounts, payee and workflow. Its existing source actions create or retrieve a supplier invoice. The shared supplier invoice workspace handles review, tax and distributions; Finance AP owns approval, posting and payment. A purchase order is not required for these Estate handoffs.

## 1. Use the existing source actions

All requests below use the signed-in application's existing API client and an empty JSON body `{}`. Save the source workspace first. The server reads the source record; do not send invoice amounts, supplier IDs or Estate ownership flags from a generic create form.

| Expense | Acquisition stage (procedure ID) | Request |
| --- | --- | --- |
| External surveyor fee | Cadastral Survey (2) | `POST /api/estate/land-acquisitions/{id}/accounts-payable-request` |
| Land vendor consideration | Vendor Payment (8) | Same endpoint |
| Stamp duty | Stamp Duty Payment (14) | Same endpoint |
| Other acquisition costs | Stamp Duty Payment (14) | `POST /api/estate/land-acquisitions/{id}/other-acquisition-costs/accounts-payable-request` |

The primary action chooses the payable kind from the current source stage. These actions and the invoice navigation button are already wired into `frontend/src/app/estate/land-acquisition/page.tsx`.

```tsx
import { estateAcquisitionService } from '@/services/estate-acquisition.service';
import { SupplierInvoiceWorkspaceButton } from '@/components/procurement/SupplierInvoiceWorkspaceButton';

// Inside your existing async handler, after saving the source workspace:
const result = await estateAcquisitionService.ensureAccountsPayableRequest(acquisitionId);
// Retain result.values in your workspace state, as the existing page does.
const invoiceId = result.values.accountsPayableInvoiceId;

// Render in your component once the returned ID is available:
// {typeof invoiceId === 'string' && invoiceId && (
//   <SupplierInvoiceWorkspaceButton invoiceId={invoiceId} />
// )}
```

For other costs, call `ensureOtherAcquisitionCostsPayableRequest(acquisitionId)` and read `result.values.otherAccountsPayableInvoiceId` instead. Both methods return `WorkspaceData`, including `values`, `stageInputsComplete`, `missingInputs` and `documentRequirements`. Preserve server validation detail/code in the error toast; reuse `getProcurementProblemMessage(error)` from the existing page.

`SupplierInvoiceWorkspaceButton` checks the saved invoice's ownership. New Estate invoices open `/procurement/supplier-invoices/{id}`; older Finance-owned invoices retain `/finance/ap/invoices/{id}`. Do not redirect every legacy AP invoice into the supplier workspace.

## 2. Required source and Finance setup

- **Surveyor:** save External surveyor source, `surveyorBusinessPartnerId`, positive `surveyorFeeAmount`, and optional `surveyorFeeDueDate` in the Cadastral Survey workspace.
- **Vendor consideration:** capture the canonical vendor/owner Business Partner and a positive negotiated/agreed amount through the existing acquisition screens.
- **Stamp duty:** a valid approved assessment with positive duty amount, plus a configured `GRA-STAMP-DUTY` Business Partner.
- **Other costs:** positive service costs in the Stamp Duty Payment workspace, plus a configured `LAND-ACQ-OTHER-COSTS` Business Partner. This action currently creates one combined invoice for that configured payee; it does not split bills among different service providers.
- Each payee needs an active Supplier or Contractor role and an approved AP profile effective on the invoice accounting date. Use canonical Business Partner IDs, not legacy Supplier IDs. The current Estate action requires one unambiguous active AP role; supporting a partner with both roles requires capturing and forwarding the selected `businessPartnerRoleId`.
- Finance must configure its AP control account, applicable taxes and an active, direct-posting, non-control land debit account. The current resolver recognizes `Land Under Acquisition`, `Land Acquisition Costs` or `Land`; required accounting-book mappings must exist.
- Use the normal Estate workflow, stage permissions and required evidence. Use Finance's approval and open-period controls for posting.

## 3. Behavior to retain

- Creation produces a **draft with tax review pending**. Users select applicable invoice-wide tax and withholding treatment before submission/posting. Creation does not approve, post or pay.
- One invoice is supported per tenant, acquisition and payable kind. Retrying returns the linked invoice; it must not create duplicates. Multiple surveyor invoices or installments for the same payable kind need an explicit source-model/cardinality extension.
- `estateAcquisitionId` and `estatePayableKind` are server-controlled source lineage. Reference text alone is not proof of ownership.
- Supplier, source amount, quantities/prices, currency and source GL accounts remain controlled by Estate. Supported notes/date/tax edits still use AP validation.
- Distribution permits supported splits, saves drafts and validates debit/credit balance again before posting. Estate source accounts stay controlled.
- Estate invoices retain Finance AP permissions and segregation of duties. The Procurement-only SOD switch does not exempt Estate-only invoices.

For another Estate document family, add a trusted backend handoff with its own tenant-safe source identity, stage/permission checks and duplicate prevention. Reuse the shared invoice service and workspace. The existing land-acquisition endpoints are not an unrestricted API for arbitrary Estate documents. Do not copy Finance's invoice pages or manufacture a purchase order.

## 4. Acceptance checks for the Estate developer

1. Create/advance a test acquisition normally to its payable stage and save its source values/evidence.
2. Create its payable twice; confirm both calls resolve to the same invoice ID and correct supplier/amount/source kind.
3. Open the invoice via the shared button. Complete tax review, save, reload and confirm persistence.
4. Edit/split draft distributions. Confirm an unbalanced save/post is rejected and a balanced saved distribution survives reload.
5. Complete independent AP approval, post in an approved open period, and inspect the journal, subledger and source link. Test payment/retry through the existing Finance flow.
6. Repeat for the other three payable kinds. Test wrong stage, missing AP profile, ambiguous partner role and unauthorized access.

Automated supplier/Estate contracts passed **403/403**; normal builds and all **59 migrations** passed on the fresh verification database. Live source-to-posting acceptance is still pending and is now handed to the Estate developer. These automated results are not a claim that the live financial journey is complete.

## Local verification environment handover (26 September 2026)

This setup is local only; pulling Git does not copy it into another developer's database.

- Frontend: `http://localhost:3000`; API: `http://127.0.0.1:5003`.
- Isolated database: `RhemaERP_Finance_Procurement_Verification_20260926_b749655e6b68`, tenant `DEFAULT`. Original databases and user secrets were preserved.
- Land account created: `DEFAULT-1540 / Land Under Acquisition`, ID `703a5cd1-468a-4e18-80af-c6df041b943b`, mapped to BASE / `ASSET_UNDER_CONSTRUCTION`.
- Full 17-step Estate procedure imported and published: `Land Acquisition Procedure (Imported 202609261730)`, ID `ff1b3371-3340-4eb5-bd7b-c57fb5dacc2c`. Its 47 required document definitions and sequential transitions were retained. An unused inactive draft named `Estate verification workflow setup` remains from registering the entity type; it is not the published procedure.
- Existing `estate.manager`, `estate.officer1`, `survey.officer1` and `financial.controller` are seeded. `financeapprover` is not seeded here. September 2026 remains Future and needs normal period-opening approval before posting into it.
- Two pre-existing completed demo acquisitions remain. The partially entered new test acquisition was closed **without saving**. No new acquisition, supplier invoice, approval, journal or payment was created in this rehearsal.
- A follow-up template-helper correction changes workflow step orders to 1–17 while leaving acquisition procedure IDs at 0–16; seven focused tests passed. The already-published local procedure uses the corrected order. Check that fix is integrated before generating a new template elsewhere.

Detailed integration/migration history: [supplier-invoice-estate-handoff-20260926.md](supplier-invoice-estate-handoff-20260926.md). Shared Finance identity contract: [CANONICAL_BUSINESS_PARTNER_FINANCE_CONTRACT.md](Finance/CANONICAL_BUSINESS_PARTNER_FINANCE_CONTRACT.md).
