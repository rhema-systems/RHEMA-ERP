# Supplier invoice integration for Estate

**Estate developer handover:** Start with [estate-supplier-invoice-developer-quickstart.md](estate-supplier-invoice-developer-quickstart.md) for current endpoints, code examples, limitations and local setup. PRs 256 and 257 are merged. On 26 September the user assigned the remaining Estate workflow/source-to-posting rehearsal to the Estate developer. The local land account and published 17-step workflow are now configured; the new acquisition form was closed without saving. Older readiness notes below describe earlier checkpoints and are superseded by the quick start's local handover section.

Estate creates supplier invoices from its existing land-acquisition documents. The supplier workspace owns invoice review and distribution editing; Finance AP continues to own approval, accounting and payments. No purchase order is required for an invoice created by the trusted Estate handoff.

## Finance PR 255 prerequisites

The integrated handoff follows Finance's canonical Business Partner contract. `businessPartnerId` is the counterparty identity; do not pass a legacy Supplier or Customer ID. The partner must have an active Supplier or Contractor role and an approved AP profile effective on the invoice accounting date. Where both roles exist, pass the selected `businessPartnerRoleId`; receipt Auto Invoice and landed-cost generation preserve that selection through grouping and retry. Estate currently delegates role resolution to AP without an explicit role selection, so its existing handoff supports a partner with one active AP role; a partner with both Supplier and Contractor roles is rejected until Estate captures and forwards that choice. No Finance profile is silently approved.

Stamp-duty and other-cost requests require preconfigured `GRA-STAMP-DUTY` and `LAND-ACQ-OTHER-COSTS` partners with governed AP setup. The source actions do not create or approve these payees. Both payable endpoints return structured validation details and a code so the existing Estate toast can explain missing setup. The remaining explicit integration limitation is dual-role selection: Estate currently needs one active AP role per payee until its role picker is added.

Tenant Finance settings own AP control accounts, and the selected bank or cheque book owns cash accounts. The approved effective AP profile supplies expense, tax-group, payment-term and withholding defaults. Legacy Procurement partner account fields do not override those Finance controls. Historical posted-account and tax snapshots remain evidence for replay/reversal. See `docs/Finance/CANONICAL_BUSINESS_PARTNER_FINANCE_CONTRACT.md` for the shared contract.

Finance's canonical migration chain requires its documented clean cutover baseline; it must not be applied blindly to a populated legacy Procurement verification database. The user-secret target is `.\SQL2017 / RhemaERP`, a legacy database without Book V2/canonical columns or migration history, and the documented `.\EXPRESS22` instance is unavailable. The user has authorized creating a separate, empty verification database on the available SQL2017 instance and applying the full migration/seed commands there. Final database identity, applied migration evidence and runtime checks must be recorded before release sign-off; both the original database and previous Procurement clone remain preserved. A fresh fetch confirms supplier-invoice PR 256 was merged into `origin/master` at `500f0acb91`; this does not close the acceptance gates below.

A separate empty database is now prepared on `RHEMA-MICHAEL\SQL2017`: `RhemaERP_Finance_Procurement_Verification_20260926_b749655e6b68`. All **59 migrations** match the current source chain. Two successful seed passes produced identical counts: 11 Business Partners, 6 partner roles, 3 AP profiles, 3 AR profiles, 2 AP withholding defaults and 3 accounting books; there are zero legacy Suppliers and zero JournalEntries. `DBCC CHECKDB ... PHYSICAL_ONLY` passed. The original database and old Procurement verification clone were not mutated; user secrets were not changed.

The API at `http://127.0.0.1:5003` now uses this fresh database and the verified full migration assembly. Liveness and readiness return HTTP 200, including database and ClamAV checks; `http://localhost:3000/login` returns HTTP 200. The frontend was restarted with a 4096 MB Node heap limit after correcting one invalid encoding byte in the supplier invoice form. The existing local scanner was restarted with its existing configuration. The runtime receives the existing user-secret encryption key without modifying or displaying it; a nonexistent-user login probe reaches normal authentication (HTTP 401), without the prior encryption-configuration error. Startup initialization and background workers remain disabled. Safe provisioning/runtime evidence is under `tmp/canonical-verification-evidence-RhemaERP_Finance_Procurement_Verification_20260926_b749655e6b68/`.

This is a fresh seeded environment, not a restored Finance backup. The seeded administrator username is `admin` in tenant `DEFAULT`; prior test sessions and records are not carried over. Live acceptance still needs normal setup: the seed creates `financial.controller` rather than `financeapprover`; September 2026 remains Future; and a published LandAcquisition workflow plus an acquisition at a payable stage are not supplied by the broad seed. Prepare these through the normal governed setup before claiming live approval/posting acceptance.

## Existing-database upgrade limitation

A fresh verification database proves the new application flow; it does not prove an upgrade of an existing customer database. Finance's current migrations deliberately require selected legacy transaction tables to be empty: VendorInvoice, VendorPayment, SupplierDebitNotes, AR invoices/payments, subledger adjustments, withholding and collections contain old identity/evidence that the migration cannot safely infer. AccountingBookModelV2 similarly requires a fresh database or a separately approved accounting-book conversion.

An in-place upgrade remains technically possible but requires a reviewed data-conversion implementation: explicit legacy counterparty-to-Business-Partner mappings, governed role/profile evidence, accounting-book mappings, preserved transaction lineage, and reconciled balances/history. Rehearse that conversion on a restored copy before applying it to a populated database. Do not remove the migration guards or reset transactions to make an existing database pass. Separate clean-database acceptance is not approval to reset or upgrade the user's retained databases.
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

Finance PR 255 is integrated. Supplier/Estate PR 256 is merged; this document remains an integration contract rather than a release sign-off.

Final canonical contract verification on 26 September:

- API invoice contracts: **403 passed, zero skipped**, covering Estate lineage, Procurement boundaries, Auto Invoice, landed cost, AP/AR posting and replay, payments, partner defaults and adjustments. This final run uses the normal full migration assembly and includes the previously excluded migration-discovery case.
- Core partner defaults and procurement budget selection: **52 passed, zero skipped**.
- Frontend: **38 focused tests passed**, followed by **5 Auto Invoice role tests passed** after its final role-selection change (overlapping runs, not 43 distinct tests). Scoped TypeScript passed.
- Shared/Core, Data, API and API test assemblies passed normal builds with `TdcFastEfBuild=false` and analyzers enabled. The Data compiler includes all 28 migration designers and the full model snapshot. Its project now excludes only the transitive Roslyn extension-authoring meta-analyzers, which exhausted memory while traversing generated models despite the project using no Roslyn APIs. SDK .NET/ASP.NET analyzers, EF analyzers, generators and compiler diagnostics remain enabled. The constrained verification build used one MSBuild worker, a serial compiler response for Data and workstation GC; existing warnings remain.
- The first compiled SQL migration run exposed three missing parentheses in two archived prerequisite migrations linked into the test project. Those predicates were corrected. Direct execution of the extracted SQL passed **25 checks** against a fresh disposable database, which was removed afterward. Existing databases were not changed. The rebuilt, combined C3/C4 SQL migration suite then passed **15 of 15 tests with zero skips**, covering real SQL application, preflight rejection, schema reconciliation, lineage constraints and bounded rollback.

Evidence is retained in `tmp/procurement-validation-20260925/` and `tmp/c3-c4-actual-precursor-source-sql-verification-20260926.log`. Normal full builds, migration discovery, fresh canonical database migration, seed repeatability and physical integrity checks passed. The live Estate-to-posting lifecycle remains a separate acceptance gate. Normal build logs are `canonical-normal-data-target-fix-build.log`, `canonical-normal-ErpSystem.Api-build.log` and `canonical-normal-ErpSystem.Api.Tests-build.log`; the 403-case result is `canonical-normal-supplier-contracts.trx`. The final Data rebuild including the canonical trigger repair also passed with zero errors and 109 existing warnings; see `canonical-trigger-repair-data-build.log`. All 403 supplier/Estate contracts passed again against that rebuilt Data assembly with zero skips; see `canonical-trigger-repair-supplier-contracts.trx`.

Prior live checks used `http://localhost:3000`, API `http://127.0.0.1:5003`, and `RhemaERP_Procurement_Verification_20260925`. Those results predate the Finance canonical merge and are not validation of the new database model. Startup seeding and background workers remain disabled for verification. The fresh canonical database and current runtime checks are recorded above; authenticated Estate lifecycle checks remain outstanding.

Outstanding financial acceptance requires the invoice reviewer's tax decision, purchase tax GL mappings where applicable and an independently approved open accounting-book period. No invoice approval, payment or live GL posting is implied by successful compilation or draft distribution save.

## Backend ownership and migration review

The Estate handoff is the only writer of the two new source fields. They are ignored when deserializing public create requests; update requests have no source setters. Source creation checks the acquisition tenant, active record and current payable stage, uses a transaction with a source-specific application lock, and relies on a unique tenant/acquisition/kind index as the final concurrency guard. A second concurrent request may need to reload the existing invoice; it must not create another liability. The source key remains reserved after soft deletion or voiding. Replacement billing requires an explicit future reconciliation workflow rather than silently recreating the source invoice.

Migration `20260926210000_EstateSupplierInvoiceLineage` adds nullable acquisition/kind columns without changing existing invoice rows. Its composite foreign key references the acquisition's tenant and ID, and its check constraint requires a complete, supported Estate source without PO, accepted-supply, auto-invoice or opening-balance ownership. An update trigger prevents changing or removing source identity. EF declares that trigger. Rollback refuses to drop source evidence while any linked Estate invoice remains. It does not backfill text references or workspace JSON and does not open accounting periods or assign accounts/permissions.

The Estate controller retains authorization and stage-specific source checks. The shared AP service retains canonical supplier resolution, tax calculation, approval, posting and payment controls. Invoice edits preserve source reference, currency/FX, line identity, type, description, quantity, price, discount, unit, GL account, budget, asset and procurement links. Tax classification remains reviewable. Estate distributions may split source purposes but cannot redirect source accounts. Supplier invoice reference, dates and notes retain normal invoice editing. Before update, submission, approval and posting, the source's current net evidence must match the invoice's `SubTotal` (already net of discounts). Settlement uses gross `TotalAmount`, allowing reviewed tax to add to a fixed net Estate charge.

Do not replace the shared Finance service/controller/DTO files wholesale during integration. Review the additive Estate partial, source-query/distribution predicates, Estate handoff changes, model/snapshot and migration together. Finance's manual invoice pages and their existing ownership remain separate. Existing reference-only Estate invoices stay on their original Finance route until explicitly reconciled; calling the creation action will reject an unverified reference collision rather than adopting it.

Focused tests cover API source-metadata rejection, tenant/source boundaries, all four source kinds, model keys/FK, immutable commercial fields, reviewed-tax allowance, pending-tax refusal and typed-net versus legacy-gross reconciliation. These checks do not by themselves prove concurrent SQL execution, complete Estate workflow authorization, tax-calculated ledger posting, or payment settlement. Record the real-service, relational migration and live lifecycle results separately; retain any unexecuted acceptance checks as open.


## Invoice entry layout follow-up

The shared Procurement create/edit form now uses one compact table row per item, with a full-screen toggle and optional row details for budget/warehouse. Tax and trade discount are selected once in the invoice totals footer. Explicit selections propagate into the existing persisted line accounting fields; saved mixed values are retained until the reviewer chooses replacements. Trade discount entry uses an invoice-wide percentage with the calculated amount shown alongside. Source-controlled Estate/landed-cost discounts remain locked. Tax breakdown is expandable. Live UI verified add/remove, expansion/escape with unsaved values retained, tax dropdown stacking and discount/tax/total recalculation without saving or posting. Scoped TypeScript and the subsequent normal backend builds passed; financial acceptance remains a separate open gate.

## Supplier invoice Save fix (2026-09-26)

Reproduced the silent Save Changes failure on VI-2026-00003. The client schema rejected the API's null optional accepted-supply kind/source and PO-item links, preventing the submit handler from running. Procurement's form schema now normalizes nullable optional fields to omitted values while preserving required-field and source-kind validation. Invalid submissions show a persistent summary beside Save and a toast.

Verification: three focused schema regressions passed; scoped TypeScript passed. The live browser saved the existing draft successfully, displayed the update confirmation and opened its invoice detail. It remains Draft at GHS 200.00 with tax review pending; no approval or posting was performed. Finance's manual invoice forms were not changed.

## Supplier invoice view cleanup (2026-09-26)

Procurement invoice detail now leads with the bill. Matching controls and receipt/cost/accounting details are below it in expandable sections. Matching opens when approval is blocked; a brief top notice links to it. Procurement uses a compact presentation of shared matching controls: internal AP/DEC/TDC labels and explanatory boilerplate are omitted; an empty, ineligible exception panel is not rendered. Eligible requests, active exceptions, history, errors and approval enforcement remain available. Shared Finance screens retain their existing presentation. Duplicate supplier details and raw supplier/item/account IDs were removed from the Procurement bill body; account names are shown when available.

Five focused matching/exception presentation tests and the scoped TypeScript check passed. The invoice-first layout was visually checked on VI-2026-00004. Subsequent navigation to a PO invoice timed out in the browser, so no additional live matching-action check is claimed. No invoice transactions were changed.

## Description save/reopen fix (2026-09-26)

Procurement invoice create/edit now installs the API's saved invoice in the shared invoice query cache before navigating. It cancels older invoice fetches so they cannot overwrite that response and invalidates dependent registers/matching/workflow data. Previously, navigation could reuse the pre-edit invoice for the five-minute cache lifetime; both first-line edits and added lines were affected.

Six focused schema/cache tests and scoped TypeScript passed. Live verification on draft VI-2026-00003 replaced the main description and added a second line using Shift+Enter, saved through the UI, confirmed the exact two-line value in the isolated verification database, displayed both lines on invoice view and reopened the editor with both lines retained. Temporary verification wording was then replaced with the original Freight / Shipping description through the UI. No approval, posting or payment was performed.

## App font-size controls (2026-09-26)

The shared header now offers S/M/L font controls. Medium preserves the existing size; Small and Large scale rem-based text and controls to 87.5% and 112.5%. The preference persists in browser storage, synchronizes across tabs and still works for the current session when storage is blocked. Print sizing stays unchanged. The header wraps its search field on narrow screens.

Validation: all 10 header tests passed, including preference restoration and blocked storage; scoped TypeScript and ESLint passed. On the fresh canonical database, signed-in browser verification confirmed Small/Medium/Large root sizes of 14/16/18px, Large retained after a reload, and the dashboard/header layout remained usable at Large. Medium was restored after verification.

## Canonical Procurement trigger repair (2026-09-26)

The fresh migration rehearsal found SQL207 when the WHT backfill updated VendorInvoice: its retained mandatory-matching trigger still referenced SupplierId after Finance renamed that column. A catalog/source audit identified five affected guards spanning invoice matching, optional payment approval, inventory return postings, supplier debit-note credits and QS advance recovery.

The additive migration `20260925190000_CanonicalProcurementFinanceTriggers` runs before the WHT backfill. It repairs only the reviewed identity predicates in the installed triggers, preserving their remaining tenant, source, status, matching, approval and audit checks. Inventory returns use canonical Business Partners directly rather than the retired supplier bridge. Debit-note identity uses VendorId; BusinessPartnerRoleId is not a counterparty id. QS matching now requires exact canonical partner identity instead of code/name fallback. Unknown definitions fail closed before any trigger is changed, and independent rollback cannot restore retired identity guards. The original pre-cutover baseline remains unchanged.

A real SQL Server rollback-only probe reproduced SupplierId/SQL207, then passed the repair plus all eight WHT commands. Zero-row updates bound all five trigger bodies successfully, matching safeguards remained present, and a second repair pass preserved all five canonical definition hashes. A further rehearsal passed all 30 commands across the six remaining migrations, including period reconciliation, customer adjustment accounts, distribution drafts and Estate lineage. Rollback restored all affected column schemas and all five original trigger hashes. Evidence: `tmp/procurement-validation-20260925/wht-migration-probe-20260926T160703413.json` and `tmp/procurement-validation-20260925/wht-migration-probe-20260926T161323942.json`.

## Fresh canonical authenticated UI checks (2026-09-26)

The signed-in DEFAULT tenant dashboard and supplier invoice workspace were checked in the visible in-app browser. The Procurement menu exposes Supplier Invoices; the empty register and Record Invoice form load successfully. New description rows are 32px high; Shift+Enter expands the tested two-line description to 52px. Add/remove, full-screen entry and Escape exit preserve the unsaved text. Invoice-wide tax and trade discount remain in the totals footer. An empty submission displays persistent required-field errors, confirming the button responds. Test text was discarded and the register remained empty. No invoice was saved, approved, posted or paid.

Evidence: `tmp/procurement-validation-20260925/canonical-authenticated-ui-20260926.json`. This closes the font and empty-entry UI checks; saved draft/distribution and financial lifecycle acceptance still require governed source records in this fresh environment.
Read-only Estate readiness checks on the same fresh database found zero supplier invoices and two demo acquisitions (`TDC-PORTAL-ACQ-001/002`), both already at Asset Created (stage 16), without workflow instances. There is no Land Acquisition workflow definition and no acquisition at the supported payable stages (2/8/14). Three demo suppliers have one active Supplier role and an approved effective AP profile, so one can support the surveyor test after source setup. The `GRA-STAMP-DUTY` and `LAND-ACQ-OTHER-COSTS` payees are absent. No active land debit account has any of the names currently accepted by the Estate resolver (`Land Under Acquisition`, `Land Acquisition Costs`, `Land`).

The next financial rehearsal requires a published Estate workflow, an acquisition advanced through normal stages, and a Finance-configured land debit account. Stamp-duty/other-cost scenarios also require governed payee profiles. Existing completed acquisitions must not have their stage or financial identity overwritten to manufacture test eligibility.
