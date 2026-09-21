# HR → Finance posting — the one treatment, and what is built

**Written 2026-09-20.** Lane 8 of `../programme/HR-FINISH-PLAN.md`. This is the design the
register (`HR-FINANCE-INTEGRATION-BACKLOG.md`) was kept open for: one accounting treatment applied
to every HR money event, through the boundary the Finance owner published (FIN-INT-001), with the
consumer-contract tests the Finance adapter checklist requires. Slice 1 (this document's build)
covers medical claims and staff travel — the priority pair the register named because they must
post the same way. The remaining events are listed in § 7 with their treatment already decided.

Read `HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md` first if you have not; this document does not
repeat the governance message.

---

## 1. What "end to end" means here, and what it does not

| HR → Finance, built | Not HR's, by the Finance owner's rule |
|---|---|
| The HR action that authorises accounting (approve, pay, disburse) and the Finance journal commit **together**; neither exists without the other | Account policy, classification, fiscal periods, module locks, balances, audit — Finance's engine enforces all of them; HR sends lines and identities |
| Accounts are chosen **once per tenant, by role**, through HR's chart-of-accounts read door — never hard-coded, never per screen | The bank movement. HR's "paid" credits a **clearing** account; Finance's Cash module clears it against the bank, exactly as payroll's clearing account already works |
| A **register** row per (event, source): Posted with the journal, Failed with Finance's reason, Unposted while the rule is off, Skipped, Reversed — retryable from a screen | Dimensions (cost centre, project). TDC's cost-attribution answer is outstanding; the engine treats HR as an uncertified route and reads none. Adding them is additive |
| **Reversal** through Finance's exact reversal, with a reason, from the same screen | Producer routes. Finance's route catalogue has one HR route (payroll). HR posts through the route-less FIN-INT-001 overload today; the hand-off asks for routes |
| Contract tests in the Finance CI gate | AP for supplier payees (recruitment costs, insurers, facilities) — still the R8 hand-off; nothing here touches it |

---

## 2. The boundary in code

| Piece | Where | What it is |
|---|---|---|
| Event catalogue | `src/ErpSystem.Core/Services/HR/Finance/HrFinancePostingEventCatalog.cs` | The events, their Finance `SourceDocumentType`, the roles each debits and credits, the required account type per role |
| Command factory | `.../HrFinancePostingCommandFactory.cs` | Pure functions from an entity to the lines. **The trigger and the retry call the same builder**, so a back-fill can never post something different from what the trigger would have |
| Adapter | `.../HrFinancePostingAdapter.cs` (`IHrFinancePostingAdapter`) | Owns the transaction, resolves roles to accounts, builds the V2 request, calls `IFinancePostingEngine.PostAsync`, writes the register row. The HR side of FIN-INT-001 |
| Store | `.../HrFinancePostingStore.cs` | The adapter's data access, split out so the contract tests need no EF provider |
| Admin service | `.../HrFinancePostingAdminService.cs` | Settings (mappings, rules with readiness), register, retry, reverse |
| Controller | `src/ErpSystem.Api/Controllers/HR/HrFinancePostingController.cs` | `api/hr/finance-posting/*` — read `HR.Company.Read`, write `HR.Company.Admin` |
| Tables | `src/ErpSystem.Data/ApplicationDbContext.HrFinancePosting.cs` | `HrFinanceAccountMappings`, `HrFinancePostingRules`, `HrFinancePostingRecords` |
| Screens | `frontend/src/app/administration/hr/settings/finance-posting/page.tsx`; `components/hr/common/FinancePostingCard.tsx` | HR Settings → Finance Posting (roles · events · register); the Finance card on a medical claim, a travel claim, and the status column on a trip's advances |
| Tests | `tests/ErpSystem.Api.Tests/Services/HR/HrFinancePosting*.cs` | 26 contracts, registered in `.github/workflows/finance-integration-gate.yml` |

---

## 3. The one treatment

Five account **roles**, mapped once per tenant:

| Role | Type | Meaning |
|---|---|---|
| Staff claims payable | Liability | What the company owes employees for approved claims until paid |
| Staff advances receivable | Asset | Money handed to an employee ahead of the spend |
| Staff payments clearing | Asset | Where HR's "paid" lands until Finance's Cash module clears it against the bank |
| Medical expense | Expense | Reimbursements to employees and dependants |
| Travel expense | Expense | Per diems, accommodation, transport, incidentals |

Three verbs, applied to every event:

| Verb | Journal | HR trigger |
|---|---|---|
| **Recognise** | Dr expense / Cr staff claims payable | approval |
| **Settle** | Dr staff claims payable / Cr staff payments clearing (and Cr staff advances receivable for the part recovered from an advance) | payment |
| **Advance** | Dr staff advances receivable / Cr staff payments clearing | disbursement |

Approval and payment are two events on purpose. HR may approve in one month and pay in the next;
Finance must see the liability the day it arises and the cash the day it goes.

**Settled through payroll** (a travel claim paid by payroll offset; a medical claim paid by salary
deduction): HR posts only the advance-recovery leg, if any, and records the rest as *Skipped* —
payroll's own journal, already on the posting engine via its Finance route, clears the payable when
the allowance component is mapped to the same staff-claims-payable account. That mapping is the
payroll owner's; it is in the hand-off.

### 3.1 The five events in slice 1

| Event | Source type / action | Lines | Trigger |
|---|---|---|---|
| `MEDICAL_CLAIM_APPROVED` | `MedicalExpenseClaim` / `Approve` | Dr Medical expense, Cr Staff claims payable — `AmountApproved` | `POST medical-expense-claims/{id}/approval` with Approved |
| `MEDICAL_CLAIM_PAID` | `MedicalExpenseClaim` / `Pay` | Dr payable, Cr clearing — `AmountApproved` | `POST …/{id}/payment` |
| `TRAVEL_CLAIM_APPROVED` | `StaffTravelExpenseClaim` / `Approve` | Dr Travel expense, Cr payable — `TotalApproved` | `POST staff-travel/finance/claims/{id}/review` to Approved or Partially Approved |
| `TRAVEL_CLAIM_PAID` | `StaffTravelExpenseClaim` / `Pay` | Dr payable `TotalApproved`; Cr receivable `AdvanceDeducted`; Cr clearing `NetPayable` | `POST …/{id}/pay` — built **after** the advance settlement arithmetic |
| `TRAVEL_ADVANCE_DISBURSED` | `StaffTravelAdvance` / `Disburse` | Dr receivable, Cr clearing — `ApprovedAmount` | `POST …/advances/{id}/disburse` |

### 3.1b The six events in slice 2 — employee payables (built 2026-09-20)

| Event | Source type / action | Lines | Route |
|---|---|---|---|
| `LEAVE_ENCASHMENT_PROCESSED` | `LeaveEncashment` / `Process` | Dr Leave encashment expense / Cr clearing (direct) or Cr payable (payroll) — `AmountPaid` | rule; default **payroll** |
| `AWARD_CONFERRED` | `EmployeeAward` / `Confer` | Dr Awards expense / Cr payable — `MonetaryAmount`; no money → Skipped | — |
| `AWARD_PAID` | `EmployeeAward` / `Pay` | Dr payable (conferred) / Cr clearing (`AmountPaid`, new column) ± Awards expense for the difference. If the conferral never posted (priced later, or rule off then), the payment recognises and settles in one journal | rule; default **direct**; payroll → Skipped (or Dr expense / Cr payable when nothing was recognised) |
| `LONG_SERVICE_AWARD_PROCESSED` | `LongServiceAward` / `Process` | Dr Awards expense / Cr clearing or payable — `MonetaryAmount`; unpriced rung → Skipped | rule; default **payroll** |
| `BENEFIT_UTILIZATION_APPROVED` | `BenefitUtilization` / `Approve` | Dr Benefits expense / Cr payable — `Amount` in the enrolment's currency | — |
| `BENEFIT_UTILIZATION_PAID` | `BenefitUtilization` / `Pay` | Dr payable / Cr clearing | rule; default **direct**; payroll → Skipped |

**The settlement route is a per-event toggle** (HR Settings → Finance Posting → Events → "Settled")
for every event whose document names no payment method. TDC's "payroll or direct payment" answer is
that toggle, not a code change. Events whose document does name the method (travel, medical) keep
taking it from the document.

Behaviour the slice changed on the way: processing a long-service award is now once (it had no
state check) and, for a priced rung, writes `PaymentProcessed`/`PaymentDate` (columns nothing wrote
before); an award's `AmountPaid` is now stored; a benefit claim must be Approved before it can be
Paid and cannot leave Approved/Paid while its posting stands; award and long-service edits/deletes
are guarded like claims. New account roles: leave encashment expense, awards expense, benefits
expense.

### 3.1c Slice 3 — the separation final settlement (built 2026-09-20)

| Event | Source type / action | Lines | Route |
|---|---|---|---|
| `SEPARATION_SETTLEMENT_RELEASED` | `SeparationSettlement` / `Release` | one journal, a leg per settlement line: earnings Dr Separation expense (unpaid salary, notice, gratuity, pension, other), Dr Leave encashment expense, Dr Benefits expense; deductions Cr Staff advances receivable (loans, salary and travel advances), Cr Employee recoveries income (property, other), Cr Statutory deductions payable (tax); the net Cr clearing (direct) or Cr Staff claims payable (payroll's final run); a leaver who owes more than they are due is Dr Staff advances receivable | rule; default **payroll** |

**The authorising event is Internal Audit's approval** (FR-HR-185), which the service already
called "payment may be released": there is no later pay step and no payment fact on the
settlement. Finalising posts nothing — the register said so and the test holds it. Once released,
returning the settlement or changing a line refuses until the posting is reversed. Lines Finance
could not value (`CannotCompute`, null amount) cannot survive finalisation, so they never reach the
journal. The settlement's own currency (HR default validated against Finance) is the command's
transaction currency; a foreign settlement is valued through the bridge like an advance. Three new
roles: separation expense, employee recoveries income (Revenue), statutory deductions payable.

⚠ **Finance de-duplicates on (source type, source id, posting action)**, not only on the
idempotency key. Two events on one claim therefore carry different actions, and a re-post after a
reversal carries a generation suffix (`Approve#2`), or Finance would hand back the reversed
original as a duplicate. `HrFinancePostingCatalogueTests` asserts the actions are distinct.

### 3.1d Slice 4 — employee receivables (built 2026-09-20)

What an employee comes to OWE the company is one receivable, whatever raised it. Two new roles:
**Staff receivables** (Asset — kept apart from advances so the two ageings do not mix) and **Staff
receivable write-off** (Expense). The income side reuses Employee recoveries income.

| Event | Source type / action | Lines | Route |
|---|---|---|---|
| `ASSET_SURCHARGE_APPROVED` | `AssetSurcharge` / `Approve` | Dr Staff receivables / Cr Employee recoveries income, the approved amount in the surcharge's currency | rule |
| `ASSET_SURCHARGE_RECOVERED` | `AssetSurchargeRecovery` / `Recover` — **one row per recovery** | Dr Staff payments clearing / Cr Staff receivables. Method payroll deduction → **Skipped** (payroll's journal credits the receivable); method exit settlement → **Skipped** (the settlement's release posts it) | rule; the method is on the row, so no toggle |
| `ASSET_SURCHARGE_WAIVED` | `AssetSurcharge` / `Waive` | Dr Staff receivable write-off / Cr Staff receivables for the balance still outstanding; Skipped when the approval never posted | rule |
| `DISCIPLINE_FINE_IMPOSED` | `StaffDisciplineFine` / `Impose` | Dr Staff receivables / Cr Employee recoveries income. Imposing IS the authorising event: the case decision was the approval and a fine has no approve step | rule |
| `DISCIPLINE_FINE_SETTLED` | `StaffDisciplineFine` / `Settle` — when the fine reaches Fully Paid **or Waived** | Dr clearing (what was paid) and/or Dr write-off (what was forgiven) / Cr Staff receivables (the fine). Part payments accumulate and post once, at close | rule; default **direct**; payroll route posts only the write-off |
| `TRAINING_BOND_BREACHED` | `TrainingServiceBond` / `Breach` | Dr Staff receivables / Cr Employee recoveries income for the pro-rata repayment in the bond's currency; an exit after full service posts nothing | rule |
| `TRAINING_BOND_SETTLED` | `TrainingServiceBond` / `Settle` | Dr clearing / Cr Staff receivables | rule; default **direct** |
| `TRAINING_BOND_WAIVED` | `TrainingServiceBond` / `Waive` | Dr write-off / Cr Staff receivables; Skipped when no breach was posted | rule |

**The slice-3 correction.** A leaver's settlement carries the outstanding surcharge as a
`PropertyRecovery` deduction (clearance item → `SourceSurchargeId`). Slice 3 credited every such
line to recoveries income; once the surcharge's approval has posted, that is the same income twice.
`ApproveSettlementReviewAsync` now traces each deduction line to its clearance item and, where the
surcharge behind it has a Posted `ASSET_SURCHARGE_APPROVED` row, credits **Staff receivables**
instead. A surcharge that never posted (rule off, raised before the sweep) still lands on income,
which is the only place it can. `SettlementLinesRecoveringAPostedSurcharge_CreditTheReceivable_NotIncomeTwice`
holds it — and asserts the builder's roles sit inside the catalogue entry's, per side, because the
first live run failed exactly there: the adapter refuses a role the entry does not declare, the
release rolled back as designed, and a unit test that only checked the lines had passed.

⚠ **The fine's payment is not a payment event in HR.** `RecordPaymentAsync` accumulates
`FinePaidAmount` and takes whatever status the desk sends; there is no receipt. Finance is told once,
when the fine closes, with paid and forgiven split — a Waived close after a part payment posts both
legs. A Waived fine refuses further payments (new; before this it silently re-opened).

Back-fill: surcharges approved, fines imposed and bonds breached before this slice have no register
row; their later recovery/settlement finds no Posted recognition and is recorded Skipped with the
reason. Retry from the register rebuilds each command from the live source document.

### 3.1e Slice 5 — third-party payees (built 2026-09-20)

Two shapes, because the money goes two ways.

**Money HR owes a third party goes to Accounts Payable, not to a journal.** A new *kind* on the
catalogue entry, `HrFinancePostingKind.VendorInvoice`: the adapter raises a Finance vendor invoice
to the Procurement supplier with one expense line, submits it into AP approval, and records the
invoice id, number and Finance's status on the register row. Finance approves, posts and pays it
under its own controls; HR never journals a third party's payable. Status comes back by **pull**
(`POST records/{id}/refresh`, HR.Company.Read): the row carries Finance's status; Rejected or
Voided in Finance marks the row Reversed so HR's guards lift; once Paid, the payment numbers are
written onto the source (`StaffRequisitionCost.PaymentVoucherNumber`, which used to be typed). HR
can withdraw an AP row only while Finance still holds it as a draft — after that the register
refuses and names Accounts Payable. **On a budget-controlled expense account the invoice is left as
a Draft** rather than submitted: Finance's AP refuses to submit an expense line on such an account
without an adopted Finance budget cell (FIN-INT-016), HR does not reserve Finance budget (D-8), so
Accounts Payable attaches the cell and submits; the row's note says exactly that. The first live
run found this rule. This is the R8 hand-off, built as the hand-off document
proposed (route-less `CreateAsync`, as Estate and Quantity Survey do; `HR-{event}:{sourceId}` as
the reference). Finance's answers to that document's questions change one line each; **the rule
ships disabled**, so nothing reaches AP until an administrator turns it on.

**Money a third party pays HR is income through clearing.** One new Revenue role, *Insurance
recoveries income*, for insurer, NHIS and incident-insurance proceeds alike.

| Event | Kind | Source type / action | Lines / effect |
|---|---|---|---|
| `REQUISITION_COST_APPROVED` | **AP invoice** | `StaffRequisitionCost` / `Approve` (HR's approval of the cost) | one line Dr Recruitment expense to the cost's supplier, in the cost's currency, submitted for AP approval. Payee a person (no supplier) → Skipped, stays HR-side (backlog decision #1) |
| `MEDICAL_PREMIUM_PAID` | journal | `MedicalInsurancePremiumRecord` / `Pay` | Dr Medical expense (employer share) + Dr Staff receivables (employees' share, payroll recovers it) / Cr Staff payments clearing (the bill) |
| `MEDICAL_INSURER_RECOVERY_RECEIVED` | journal | `MedicalInsuranceClaim` / `Receive` | Dr clearing / Cr Insurance recoveries income, the amount the insurer paid |
| `NHIS_CLAIM_REIMBURSED` | journal | `NHISClaim` / `Receive` | Dr clearing / Cr Insurance recoveries income, the approved amount (else the covered amount) |
| `SHE_INSURANCE_CLAIM_RECEIVED` | journal | `SafetyIncident` / `Receive` | Dr clearing / Cr Insurance recoveries income, the amount paid; filed-but-unpaid is Skipped; the claim form stays re-submittable but its money is fixed once posted |

Two new roles: Recruitment expense (Expense), Insurance recoveries income (Revenue). Three payment
paths that silently overwrote a payment (premium, insurer claim, NHIS claim) now refuse a second
payment. Register columns added: `VendorInvoiceId`, `VendorInvoiceNumber`, `ExternalStatus`,
`ExternalStatusAt` (migration `AddHrFinancePostingSlice5`).

**Deliberately not posted: training budget transactions.** A `TrainingBudgetTransaction` is HR's
memo of a spend whose `Reference` is an invoice or PO number — that is, a Finance document that
already exists and already posted. Journaling it again would double the expense; the right
integration is the budget *read* (slice 6, with the manpower actuals). The same reasoning keeps the
awards and travel budget figures out of the ledger.

### 3.1f Slice 6 — HR's one revenue, and the budget reads (built 2026-09-21)

**Consulting invoices go to Accounts Receivable.** The receivables twin of the AP hand-off: kind
`CustomerInvoice`. When HR *sends* a timesheet invoice to a client whose `ConsultantClient` is
linked to a Finance customer (`FinanceCustomerId`, chosen through the new read door
`api/hr/customers`), the adapter raises a Finance customer invoice under the manual-AR route with
one service line on the new Revenue role *Consulting revenue* — **the billed hours before tax**:
HR's typed tax percentage is advisory, Finance's tax group is authoritative for the receivable —
and submits it into Finance's AR approval. Finance issues, collects and posts it; the register's
refresh pulls the status back and, once Finance records a receipt, writes `PaidAmount`, `PaidDate`
and Paid / Partially paid onto the HR invoice, which HR may no longer mark paid or void by hand
while Finance holds it. A client with no Finance customer is recorded Skipped and the invoice stays
HR-side. Entity-sweep decision #11, answered. The rule ships OFF like the AP one.

⚠ **The AR invoice is raised after HR's commit, not inside it.** Finance's AR service wraps
every call in its own transaction and cannot join HR's ("the connection is already in a
transaction" — found live), so for this one kind the send commits first and the customer invoice
follows immediately; a Finance refusal leaves the HR invoice Sent with a **Failed** register row to
post again, instead of rolling the send back. Journals and AP invoices keep the all-or-nothing path
(§ 4).

| Event | Kind | Source type / action | Lines / effect |
|---|---|---|---|
| `TIMESHEET_INVOICE_SENT` | **AR invoice** | `TimesheetInvoice` / `Issue` (HR's send) | one service line Cr Consulting revenue for `SubTotal`, in the invoice's currency, to the client's Finance customer, submitted into AR approval; Skipped when the client is not linked |

Register columns added: `CustomerInvoiceId`, `CustomerInvoiceNumber`; `ConsultantClients.FinanceCustomerId`
(migration `AddHrFinancePostingSlice6`).

**The budget reads.** `ManpowerBudget.ActualSpent` / `Variance` keep having no writer — that was the
backlog's decision: HR does not invent a number. What Finance says was spent is now *read*:
`GET JobAnalysis/budgets/{id}/finance-actuals` and `GET training-budgets/{id}/finance-actuals`
(`HrFinanceActualsService`) resolve the account the budget is charged to (the unit's
`OrganizationUnit.FinanceAccountId` from round 2 lane B2; for a training budget its typed
`GLAccountCode` first, the unit's account second), the fiscal periods touching the budget's window,
and Finance's book balances on that account per period (`IBookBalanceReadModelService`); the
result is budget, actual (net movement), variance and a period table, or a sentence saying why it
could not be read (no unit, unit not charged to an account, code not on the chart, no periods). No
dimension is sent or read; a unit is one account, which is what the link says today. This is the
shape of every HR budget conversation with Finance until FIN-INT-015's reserve → consume →
release is opened for HR (§ 6, D-9).

**Deliberately not posted: succession development activity cost.** `SuccessionDevelopmentActivity
.ActualCost` names an external provider by free text and contact, not a supplier, and like a
training budget transaction it is HR's memo of a cost Finance pays through its own AP. The
three-way training double-count (manpower training envelope, training budget, development cost)
stays a planning question the budget reads make visible rather than a posting.

### 3.2 Identity

- `SourceModule = "HR"`, `OriginModuleCode = "HR"` — Finance's module-lock catalogue already knows HR, so a Finance administrator can lock HR out of a period without touching payroll.
- Idempotency key `HR|{event}|{sourceId}|{book}|POST|G{generation}`; the book comes from Finance's own `FinanceAccountingBookCodeResolver`, the same way Procurement's tender-fee adapter resolves it.
- Functional currency = Finance's base currency. Claim totals are already functional sums (each travel line is valued through `HrCurrencyBridge` when written). A **foreign-currency advance** is converted through the same bridge and posted as functional lines; the original currency and amount are kept on the register row and in the narration. Finance-side FX evidence on the journal is not attempted — see § 6.

---

## 4. Transaction and failure semantics — read before wiring a new event

`IHrFinancePostingAdapter.RunAsync(mutate, userId)`:

1. Opens the transaction (or joins the caller's).
2. Runs the area's mutation; it returns the command, or `null` when the change carries no money event (a rejection).
3. Looks up the rule. **Rule off or absent → the HR action proceeds and the event is logged `Unposted`.** Nothing that worked before this seam stops working; the log is the back-fill queue.
4. Rule on → resolves accounts (mapped, active, right type), builds the request, calls Finance, writes `Posted`, commits everything together.
5. **Finance refuses → everything rolls back**, including the HR change, and a `Failed` row with Finance's reason is written on a clean tracker afterwards. The desk sees "Posting period is not open", not a canned string, and the register offers **Post now** once Finance is fixed.

A source document with a `Posted` row cannot be edited, re-lined, re-reviewed to a non-approved
state, or deleted: every such path calls `EnsureNotPostedAsync` and refuses, naming the journal. The
way out is the register's **Reverse**, which is explicit and reasoned, after which the locks lift.

**Why strict rather than best-effort.** A claim "paid" in HR while Finance holds nothing is a
liability that vanished. Payroll and Procurement's tender fee made the same choice; HR follows them.

---

## 5. Configuration, and what the demo database has

Everything is off until an administrator maps the five roles and enables an event (HR Settings →
Finance Posting). Enabling is refused until the event's roles are mapped to active accounts of the
right type and Finance's book resolves.

The UAT chart (63 accounts) has usable candidates for a walk-through — `2120 Accrued Payroll
Payables` (payable), `1120 Staff Loans and Salary Advances` (receivable), `1010 Cash and Bank -
Payroll Clearing` (clearing), `6020` / `6000` (expenses). It has **no** dedicated staff-claims,
medical or travel accounts; whether to add them is Finance's, and is in the hand-off.

⚠ **Live posting needs the database on the Finance baseline.** The posting engine requires the
book-period table (`AccountingBookPeriods`) that arrived with master's disposable baseline
(PR #219). A database migrated up the legacy chain does not have it and every producer — payroll
included — fails on the first posting. Recreate the database (`scripts/New-UatDatabase.ps1`) before
any live verification, and open the current fiscal period (September 2026 is seeded closed).

---

### 5.1 What a freshly built UAT database still needs before HR can post (measured 2026-09-20)

`New-UatDatabase.ps1` builds the schema from the model and stamps the chain, so the Finance
baseline's own authority SQL never runs. On the result, every producer is refused until Finance's
governed setup has been done — or, for a verification box, until these are set directly:

| Gate | Seeded state | Engine refusal | What the harness prep did |
|---|---|---|---|
| `AccountAccountingBooks` | 48 accounts × 3 books present, all `IsEnabled = 0` | "not enabled for the requested accounting book" | enabled them (classification left null, which the engine tolerates as a transition state) |
| `AccountingBooks` IFRS | `IsActive = 0`, `AllowsPosting = 0`, lifecycle Configuring | "Accounting book is unavailable for posting" | `IsActive = 1`, `AllowsPosting = 1`, lifecycle Active |
| `AccountingBookPeriods` | no rows | `ACCOUNTING_BOOK_PERIOD_REQUIRED` | one row per book × period, Open where the fiscal period is open |
| `FiscalPeriods` September 2026 | closed | "Posting period is not open" | opened like August |

These are Finance's governed operations (C3 book lifecycle, C4 book periods, period management).
The SQL shortcut is acceptable only on a rebuilt demo database; on anything that becomes go-live
they go through Finance's endpoints and workflow. Raised in the hand-off so the seed can do it.

**Verified live 2026-09-20 on `ErpSystemDB_UAT`:** `dev-harness/hr-finance/run-slice1.mjs`,
51 assertions, green twice — medical approve/pay, advance disburse, claim approve/pay with advance
recovery, each journal read back from Finance with the expected lines; reversal produces a real
reversal journal; a second approval is a duplicate; posted sources refuse edits; rule off → Unposted
→ retry posts.

**Verified live 2026-09-20 (slice 2) on `ErpSystemDB_UAT`:** `run-slice2.mjs`, 56 assertions —
an encashment credits the payable on the payroll route and clearing on the direct route (the
toggle proven in both positions); an award conferred at 2,000 and paid 1,800 clears the payable
in full and credits 200 back to expense; a payroll-route award payment is Skipped and says so; an
unpriced long-service rung is Skipped; a benefit claim is refused Pending→Paid and refused back to
Pending once posted. Benefit utilisations have no list screen in the frontend (only the enrolment
rollup), so their posting is visible in the register and the API but not on a claim page — a UI
gap noted, not built here.

**Verified live 2026-09-20 (slice 3) on `ErpSystemDB_UAT`:** `run-slice3.mjs`, 23 assertions — two
separations taken through clearance (one loan left owing), settlement, finalisation (posts nothing)
and Internal Audit approval; the journal's expense legs equal the statement's earnings, the loan
credits the advances receivable, the net lands on the payable (payroll) or clearing (direct), and a
released settlement refuses return and line edits.

**Verified live 2026-09-20 (slice 4) on `ErpSystemDB_UAT`:** `run-slice4.mjs`, 86 assertions, green
twice — a surcharge approved at 300 raises the receivable; a direct recovery of 100 clears through
clearing, a payroll recovery of 50 is Skipped, the waiver writes off exactly 150; a charge waived
before approval is Skipped; a leaver with a lost asset and 800 outstanding on an approved surcharge
carries it through clearance and settlement, and the release journal credits **Staff receivables**,
not recoveries income (the slice-3 double count, closed — its first run failed because the settlement
entry had not declared the role, which is now a per-side assertion in the test); fines imposed, part
paid, closed Fully Paid and closed Waived post the paid/forgiven split, and the payroll route is
Skipped; bonds breached, settled, waived after breach, waived while active (Skipped) and settled on
the payroll route (Skipped). Roles mapped to `1100` and `6600` on the UAT chart for want of dedicated
accounts; a live tenant creates *Staff receivables* and *Staff receivable write-off* accounts first.
⚠ Waiving a bond needs `HR.Training.Admin` **and** an employee-linked user: on the UAT seed only
TenantAdmin holds it and `admin` is not linked, so the harness mints a TenantAdmin actor.

**Verified live 2026-09-20 (slice 5) on `ErpSystemDB_UAT`:** `run-slice5.mjs`, 68 assertions, green
twice. The AP hand-off in both shapes: on a budget-controlled expense account (every UAT expense
account but depreciation) HR's approval raises a Draft invoice for the cost amount to the supplier,
the row says why, HR can withdraw it (Finance no longer serves it) and post it again as a fresh
invoice; on a plain expense account the invoice goes straight into AP approval, Finance's three
seeded tiers (Accounts Officer, then Finance Manager, then Financial Controller) approve it, the
register's refresh pulls "Approved", and HR can no longer withdraw it. A cost paid to a person is
Skipped; a handed-off cost cannot be deleted. Premium paid: Dr 6020 (employer) + Dr 1100 (employees)
/ Cr 1010; insurer, NHIS and incident proceeds: Dr 1010 / Cr 4900; second payments refused; an
incident claim re-filed with the same money stays one row, with a different amount is refused.
Three live lessons, each now in the code: AP refuses to submit an expense line on a
budget-controlled account without a Finance budget cell (HR leaves a Draft); AP approves only an
invoice with dimension provenance recorded under a producer route (HR uses the manual-AP route
until Finance grants one); a withdrawn row must be re-postable from the register (Retry now
accepts Reversed). Finance answers 500, not 404, for a deleted invoice id: recorded for the Finance
owner, not fixed.

**Verified live 2026-09-21 (slice 6) on `ErpSystemDB_UAT`:** `run-slice6.mjs`, 50 assertions, green
twice. A Finance customer created in AR is read through HR's `api/hr/customers` door as
`{id, code, name, isActive, currencyCode}`; a client naming a customer that does not exist is
refused; a client linked to one, an engagement, a timesheet with entries submitted, approved, sent
for confirmation and confirmed through the public tokenised door, an invoice generated (posts
nothing) and sent → a Finance AR customer invoice to that customer for the billed hours before tax,
PendingApproval, on a register row of kind CustomerInvoice; HR's mark-paid and void are refused
while Finance holds it; Finance's three AR tiers sign it through `api/finance/approvals`, refresh
pulls the status, HR can no longer withdraw it; an unlinked client's invoice is Skipped and its
receipt is still recorded by hand. A training budget naming GL 6020 reads Finance's fiscal periods
and an actual at least the 14,000 the premium slice posted there, with the current period's
transaction count; a code not on the chart is reported as such; a manpower budget answers linked
figures or the sentence saying what to set. Two live lessons, both now in the code: a Finance
"customer" is a BusinessPartner row read through Finance's customer service, not the Sales
Customer entity; and Finance's AR service cannot join HR's transaction, so the AR invoice is raised
after HR's commit (§ 3.1f).

## 6. Decisions taken here, and what they wait on

| # | Decision | Taken as | Waits on |
|---|---|---|---|
| D-1 | Payroll vs direct payment for reimbursements | **Both.** Documents that name a payment method decide per payment; events without one carry a per-event settlement-route toggle in HR Settings (slice 2) | TDC's answer is a toggle per event; no code change |
| D-2 | Bank leg | HR never credits a bank account. Clearing is HR's edge; the bank is Finance's Cash module | Finance owner to confirm the clearing account (or one per method) — hand-off Q2 |
| D-3 | Employee receivable for advances | A GL receivable on a shared role account, not a named AR customer; the register + `StaffTravelAdvance` are the sub-ledger | Decision #2 of the entity sweep still open for loans and surcharges |
| D-4 | FX evidence on the journal | Not sent; HR values through the bridge and keeps the original on its row | Finance owner — whether HR may pass `ExchangeRateId` and under which rate policy (hand-off Q3) |
| D-5 | Dimensions | None sent | TDC's cost-attribution answer; then a Finance route with dimension rules |
| D-6 | Posting date | The HR action date by default; per event switchable to the document date | — |
| D-7 | Routes | Journals: route-less FIN-INT-001 overload, as Procurement's tender fee. **AP invoices: the manual-AP dimension route** (`FinanceApVendorInvoice`, the one Finance's own AP screen uses), because Finance approves an invoice only when it carries dimension provenance recorded under a producer route, and the route-less overload records none — the first live run proved an HR invoice could never be approved | Finance to add HR routes (hand-off Q1); switch is one line per event / one enum value for AP |
| D-9 | HR's budgets and Finance's budget control | **Read, not reserve** (slice 6): HR keeps its own envelopes (manpower, training, awards, travel) and reads Finance's actuals on the account the budget is charged to. No `BudgetEntryId`, no reservation, nothing written back to `ActualSpent`. | The budget-commitment conversation (finish plan 8a): whether HR budgets become Finance budget entries. Until then HR plans, Finance posts, HR reads the difference. |
| D-8 | The AP hand-off before Finance answered | **Built behind a rule that ships OFF** (slice 5), on the route-less `IVendorInvoiceService.CreateAsync` the Estate and QS producers already use; HR's approval submits the invoice into AP approval; a non-supplier payee is Skipped; one expense role, not one per category; no `BudgetEntryId` (HR enforces its own envelope). | The recruitment-cost hand-off's five questions. Each answer is one line or a toggle: a producer route (D-7), "receive as Draft" (one call), per-category accounts (roles), budget reservation (one field). Turning the rule on is the tenant's decision, made once Finance has confirmed. |

---

## 7. The remaining events — treatment already decided, builders not yet written

Each follows § 3 verbatim; adding one is a catalogue entry, a factory method, two service calls
(`RunAsync` at the trigger, `EnsureNotPostedAsync` at the edit paths), a settings role if a new
expense line is needed, and a contract test.

| Register row | Event(s) | Roles | Note |
|---|---|---|---|
| ~~2.1 Leave encashment~~ | **built, slice 2** — L-D8 is the existing `AllowInServiceEncashment` flag; the posting only follows the event | | |
| ~~14 Awards~~ | **built, slice 2** (conferred, paid, long-service processed) | | budget figures stay the awards desk's bookkeeping |
| ~~Benefit utilisations~~ | **built, slice 2** (approved, paid) | | |
| ~~9b Separation settlement~~ | **built, slice 3** — posts on Internal Audit's approval, never on finalise | | |
| ~~16.1–16.5 Asset surcharge~~ | **built, slice 4** (approved, recovered per row, waived; settlement recovery credits the receivable) | | |
| ~~3.4 Discipline fine~~ | **built, slice 4** (imposed, settled on close with paid/forgiven split) | | |
| ~~7 Training service bond~~ | **built, slice 4** (breached, settled, waived) | | |
| ~~6 Recruitment cost (R8)~~ | **built, slice 5** — AP vendor invoice on HR's approval; rule ships OFF until Finance confirms | | |
| ~~11 Medical premium, insurer recovery, NHIS recovery~~ | **built, slice 5** | | |
| ~~10 SHE incident insurance proceeds~~ | **built, slice 5** | | |
| ~~7 Training budget transactions~~ | **not posted, by decision** (§ 3.1e) — a memo of a Finance document | | |
| ~~Consultant-client billing (sweep § 3.1b)~~ | **built, slice 6** — AR customer invoice on HR's send; rule ships OFF | | |
| ~~13 Succession development actual cost~~ | **not posted, by decision** (§ 3.1f) — a memo of a cost Finance pays through AP | | |
| ~~Budget surfaces (manpower, training)~~ | **read, slice 6** — Finance's book balances on the budget's account, per period; nothing written | | awards and travel budgets have no account link to read against; FIN-INT-015 for HR stays a Planned conversation (D-9) |

---

## 8. Verification

- `HrFinancePostingAdapterTests` (21) — contract helper on every request; retry returns the original without a second Finance call; refusal rolls back and logs Failed; disabled/missing rule logs Unposted; unmapped, wrong-type, inactive and cross-tenant accounts refuse; travel settlement legs; payroll-offset skip; foreign advance evidence; reversed-row generation; edit guard.
- `HrFinancePostingCatalogueTests` (16) — every event (26) has a balanced builder inside its roles (an AP event: exactly one expense leg and a supplier; an AR event: exactly one revenue leg and a customer); distinct actions per source type; zero amounts skip; the settlement route toggle in both positions; receivables raised once, cleared by what was collected and written off for the rest; a settlement line recovering a posted surcharge credits the receivable; every wired area service routes through the adapter and never touches `IJournalEntryService`.
- Live: `D:\Rhema\TDC ERPS\dev-harness\hr-finance\run-slice1.mjs` (outside the repo, see its README) — maps the roles, enables the events, walks medical approve → pay and travel advance → claim → pay, reads the journals back from Finance, asserts idempotency, reversal and the edit guard. Requires the baseline database (§ 5).
