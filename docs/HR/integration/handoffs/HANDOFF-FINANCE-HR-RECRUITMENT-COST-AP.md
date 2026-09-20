# Hand-off to the Finance module owner — HR recruitment costs need Finance approval and payment

**Raised by:** HR module work, 2026-09-10 (demo feedback round 2b, lane R7/R8).
**Severity:** not a defect — an integration request. Nothing is broken; a demo-stated requirement
cannot be met without a contract from Finance.
**Status:** open for the answers in § 5; **the adapter is built (2026-09-20, HR finish plan lane 8
slice 5) and ships with its posting rule OFF.** See § 7 — nothing reaches Accounts Payable until
the rule is enabled, and each of the five answers changes one line or a setting.

This is a self-contained note — nothing in it requires reading HR's plans or code. The design it
describes is also in `docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-2B-RECRUITMENT-PLAN.md` § 4 (R8), and the
decision that HR does not post per area until one sweep is in `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md`
§ "The rule". This request is deliberately made **within** that rule: HR is asking for the
contract, not building around its absence.

---

## 1. What is being asked, in one line

HR records the cost of filling a post (adverts, agency fees, test venues, medicals, candidate
travel) against a staff requisition, and the client wants **Finance to approve and pay those
costs and the voucher to be visible from the requisition**. HR needs an agreed way for an
HR-approved cost to become a Finance payable, and to read the payment back.

---

## 2. What HR has today, and what it is changing on its own

`StaffRequisitionCost` (`src/ErpSystem.Core/Entities/HR/StaffRequisitionEntities.cs:144`) holds
`Category`, `Purpose`, `Amount`, `Currency` (3 chars, free text), `ExchangeRate` (typed by the
user, default 1), `PaymentVoucherNumber` (free text, read by nothing), `RecordedById`.

HR is fixing, without any Finance change (lane R7, 2026-09):

- the currency is chosen from Finance's currencies through HR's existing read door
  (`api/hr/currencies`) and validated by `HrCurrencyBridge.RequireKnownCurrencyAsync`;
- the rate is **no longer typed** — it is resolved by `HrCurrencyBridge.GetRateToBaseAsync`
  (Finance's `IExchangeRateService`), the same way HR travel, assets, letters, separation and
  succession already do; this was the last caller-supplied rate left in HR;
- the payee becomes a Procurement `Supplier` (`SupplierId`, Restrict), read through a new narrow
  HR door (`api/hr/suppliers`, the `HrCurrenciesController` precedent) because Procurement's
  supplier routes are Procurement-gated;
- HR approves the cost itself (`Status: Recorded → Approved | Rejected`, approver from the token,
  never the recorder). This is HR's own business approval of its own source transaction, which the
  governance rule leaves with the producing module.

What HR is **not** doing: creating journals, invoices, payments or any payment status. The voucher
number stays a typed field until § 4 exists.

---

## 3. What already exists on the Finance side, and why HR is not just using it

Two external modules already create Finance payables through `IVendorInvoiceService`:

| Producer | Call | Correlation | Status back to the producer |
|---|---|---|---|
| Estate — land acquisition | `LandAcquisitionsController.cs:1989-2016` `CreateAsync(VendorInvoiceCreateDto)`, then `SubmitForApprovalAsync :2242` | `Reference = "LAND-VENDOR-PAYMENT:{id:N}"`, idempotent by lookup on `Reference` | Pulled: `:2308-2419` reads the invoice + allocations into a JSON snapshot |
| Quantity Survey — payment certificate | `QuantitySurveyPaymentCertificateService.cs:485` (`CreateApInvoiceAsync :469`) | `Reference = "QS-CERT:{id:N}"` + `AcceptedSupplyKind`, `IsTrustedAcceptedSupplyHandoff` | Pulled: `RefreshPaymentStatusAsync :535`; columns `VendorInvoiceId`, `ApHandoffStatus {NotReady, Ready, Created, Failed}`, `ApHandoffAt`, `ApHandoffFailure`, `PaymentStatusSnapshot` (`ProjectManagementEntities.cs:1612, 1700-1703`) |

Neither has a row in `FinanceIntegrationContractCatalog.cs`. FIN-INT-016 (direct AP expense
budget control) names **Finance AP itself** as the producer, and
`FinanceDimensionRouteCatalog.cs:64-76` says of the manual AP invoice route: *"External producers
using the same service require their own route."* The Finance owner's 2026-08-31 message asks
modules to **coordinate before implementing anything Planned or Decision Required** — so HR is
asking first rather than copying the land-acquisition call.

---

## 4. The adapter HR proposes to build (R8), so the request is concrete

- **Source event that authorises accounting:** `StaffRequisitionCost.Status = Approved` (HR's
  approve action, not "saved"). Immutable source id = the cost id; human reference
  `REQ-2026-00012/C3`; tenant id carried.
- **Call:** `IVendorInvoiceService.CreateAsync(VendorInvoiceCreateDto)` with `SupplierId`
  (required — a cost with no supplier payee cannot be handed off, see § 5 q4), `CurrencyCode`,
  **`ExchangeRateId`** (Finance's rate row, not a number), `InvoiceDate = CostDate`, one expense
  line (`LineItemType = "Expense"`, `GLAccountId` from a new tenant setting
  `CompanyHrPolicySettings.RecruitmentExpenseAccountId` chosen through HR's chart-of-accounts read
  door — **never resolved by account name**, which is what the land-acquisition path does at
  `:2260-2286`), `Reference = "HR-REQ-COST:{costId:N}"`, a `Notes` provenance label, then
  `SubmitForApprovalAsync`. Idempotent: lookup by `Reference` before create; a retry returns the
  original.
- **Back-references on the cost** (QS's shape): `VendorInvoiceId`, `VendorInvoiceNumber`,
  `ApHandoffStatus`, `ApHandoffAt`, `ApHandoffFailure`, `PaymentStatusSnapshot`,
  `PaymentStatusUpdatedAt`. `PaymentVoucherNumber` becomes **written from
  `VendorPayment.PaymentNumber`** on refresh and read-only once linked.
- **Status sync is pull** (`POST costs/{id}/refresh-ap-status`), as both precedents do. No
  Finance → HR callback is requested.
- **Evidence HR will ship** (adapter checklist § 5): a consumer-contract test using
  `FinanceConsumerContractAssertions.ShouldSatisfyPostingContract`; negative tests for a
  wrong-tenant supplier, a missing expense account, a retry (one invoice), and Finance throwing
  (cost not marked Created); the entry in `.github/workflows/finance-integration-gate.yml`.
- **Failure behaviour:** a failed Finance step leaves the cost `Approved` with
  `ApHandoffStatus = Failed` and the message; it never advances to `Created` on an exception.

This would be the **first HR AP adapter**, and the template medical claims, travel claims and
separation settlements would follow when the HR Finance sweep runs — worth reviewing it as a
pattern, not a one-off.

---

## 5. What HR needs from Finance to proceed

1. **A producer route** for HR AP invoices (a `FinanceDimensionRouteId` and its
   `FinanceDimensionRouteDefinition`), and whether HR should pass `FinancePostingProducerContext`
   on `CreateAsync` (the overload exists at `IVendorInvoiceService.cs:41`).
2. **A catalogue row** — proposed `FIN-INT-017 "HR recruitment cost → AP vendor invoice"`,
   producer owner HR, Finance owner AP — added by you or with your sign-off, per the rule that the
   catalogue status changes only when the boundary and evidence exist.
3. **Confirmation of the authorising event.** Is HR's `Approved` status on the cost acceptable as
   the event that creates a Draft vendor invoice and submits it into AP approval? Or should AP
   receive it as Draft and Finance submit it?
4. **The non-supplier payee.** A candidate reimbursed for interview travel is a person, not a
   `Supplier`. Is there any AP path for that, or does it belong with the standing "payroll vs
   direct payment" question (backlog decision #1) — in which case HR keeps such costs HR-side and
   never hands them off?
5. **The expense account.** One tenant-level recruitment expense account, or one per cost
   category (agency fee, advertising, medical, travel …)? HR defaults to one setting.
6. **Budget control.** Should HR's expense line carry a `BudgetEntryId` (FIN-INT-016's
   reservation) when the account has `BudgetTrackingEnabled`? HR has its own manpower budget
   envelope (`ManpowerBudget.RecruitmentBudget`) which it enforces HR-side; it does not want to
   reserve twice.

---

## 6. Evidence

- Surveyed 2026-09-10 against branch `hrdev` at `231c74e2`: `IVendorInvoiceService.cs:15-98`,
  `AccountsPayableDtos.cs:98` (`VendorInvoiceCreateDto`), `AccountsPayable.cs:109` (`VendorInvoice`,
  no source-module fields — only `Reference` and `Notes`), `:464` (`VendorPayment.PaymentNumber`
  = the voucher), `ApControllersConsolidated.cs:29, 482` (routes), the two producers above.
- HR's side: `StaffRequisitionService.cs:605-660` (costs recorded, nothing validated, nothing
  handed anywhere), `HrCurrencyBridge.cs:35` (the bridge every other HR money surface uses).
- Governance: `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md:38-88` (the 2026-08-31 message and the
  coordination gate), `docs/Finance/finance-integration-adapter-checklist.md`,
  `docs/Finance/finance-integration-contract-catalogue.md` § FIN-INT-016.

---

## 7. What HR built on 2026-09-20, and how each answer lands

The HR ↔ Finance sweep (lane 8) reached the third-party payees and built the adapter in § 4 as
proposed, inside HR's posting register rather than as a one-off (`HrFinancePostingAdapter`,
kind `VendorInvoice`; design `docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md` § 3.1e):

- **Trigger:** HR's approval of the cost (`POST StaffRequisitions/costs/{id}/approve`) — the
  approval and the invoice commit together, or neither does.
- **Call:** the route-less `IVendorInvoiceService.CreateAsync` (the Estate / QS precedent), one
  `Expense` line on the account mapped to the HR role *Recruitment expense*, `Reference =
  "HR-REQUISITION_COST_APPROVED:{costId:N}"`, then `SubmitForApprovalAsync`. `ExchangeRate` is
  Finance's rate through `HrCurrencyBridge`; no `ExchangeRateId` is sent (D-4).
- **Back-references:** on HR's register row (`VendorInvoiceId/Number`, `ExternalStatus/At`), not
  on the cost; `PaymentVoucherNumber` is written from `VendorPayment.PaymentNumber` on refresh.
- **Status sync is pull** (`POST api/hr/finance-posting/records/{id}/refresh`). Rejected/Voided in
  Finance → the HR row is Reversed and the cost can be re-handed after correction.
- **Withdrawal:** HR deletes only a Draft invoice; anything Finance has taken into approval is
  Finance's to reject or void.
- **Non-supplier payee:** Skipped with the reason; stays HR-side.
- **Gate:** `HrFinancePostingCatalogueTests` (AP event = one expense leg + a supplier, source guards)
  in the Finance consumer gate; live `dev-harness/hr-finance/run-slice5.mjs`.

| § 5 question | Built as | Your answer changes |
|---|---|---|
| 1 producer route | **the manual-AP route** (`FinanceDimensionRouteId.FinanceApVendorInvoice`) — your AP screen approves only an invoice with dimension provenance recorded under a producer route, and the route-less overload records none, so an HR invoice raised route-less could never be approved (seen live 2026-09-20); under the manual route it is, to AP, an invoice on their desk | one enum value → HR's own route |
| 2 catalogue row | none (Finance's to add) | — |
| 3 authorising event | HR approval creates AND submits | one line: stop at Draft |
| 4 non-supplier payee | Skipped, HR-side | a factory branch, if an AP path exists |
| 5 expense account | one role, *Recruitment expense* | one role per category (settings only) |
| 6 budget control | no `BudgetEntryId`; on a budget-controlled expense account the invoice is **left as a Draft** for AP to attach the cell and submit (your submit rule refuses it otherwise — seen live on the UAT chart, where every expense account but depreciation is budget-controlled) | one field, if HR should pick the cell itself |

**The rule is disabled by default.** The tenant enables `REQUISITION_COST_APPROVED` under HR
Settings → Finance posting once you confirm; until then approving a cost writes an *Unposted* row
and nothing else.

