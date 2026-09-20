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

⚠ **Finance de-duplicates on (source type, source id, posting action)**, not only on the
idempotency key. Two events on one claim therefore carry different actions, and a re-post after a
reversal carries a generation suffix (`Approve#2`), or Finance would hand back the reversed
original as a duplicate. `HrFinancePostingCatalogueTests` asserts the actions are distinct.

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

## 6. Decisions taken here, and what they wait on

| # | Decision | Taken as | Waits on |
|---|---|---|---|
| D-1 | Payroll vs direct payment for reimbursements | **Both.** Documents that name a payment method decide per payment; events without one carry a per-event settlement-route toggle in HR Settings (slice 2) | TDC's answer is a toggle per event; no code change |
| D-2 | Bank leg | HR never credits a bank account. Clearing is HR's edge; the bank is Finance's Cash module | Finance owner to confirm the clearing account (or one per method) — hand-off Q2 |
| D-3 | Employee receivable for advances | A GL receivable on a shared role account, not a named AR customer; the register + `StaffTravelAdvance` are the sub-ledger | Decision #2 of the entity sweep still open for loans and surcharges |
| D-4 | FX evidence on the journal | Not sent; HR values through the bridge and keeps the original on its row | Finance owner — whether HR may pass `ExchangeRateId` and under which rate policy (hand-off Q3) |
| D-5 | Dimensions | None sent | TDC's cost-attribution answer; then a Finance route with dimension rules |
| D-6 | Posting date | The HR action date by default; per event switchable to the document date | — |
| D-7 | Routes | Route-less FIN-INT-001 overload, as Procurement's tender fee | Finance to add HR routes (hand-off Q1); switch is one line per event |

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
| 9b Separation settlement released | `SEPARATION_SETTLEMENT_RELEASED` | per line category → expense roles; payable; recoverables → receivable | post on **release after audit**, never on finalise |
| 16.1–16.4 Asset surcharge | `SURCHARGE_APPROVED` (Dr receivable / Cr recovery income or asset), `SURCHARGE_RECOVERED`, `SURCHARGE_WAIVED` | receivable; new role Surcharge recoveries | payroll deduction route = Skipped + payroll projection, as travel |
| 3.4 Discipline fine | as surcharge | | |
| 13 Succession development actual cost | `DEVELOPMENT_ACTIVITY_COMPLETED` | Training expense role | only after the three-way training decision |
| Budget surfaces (manpower, training, awards, travel) | none — reads of Finance actuals | | FIN-INT-015 shape needs a Planned conversation; not wired unilaterally |

---

## 8. Verification

- `HrFinancePostingAdapterTests` (21) — contract helper on every request; retry returns the original without a second Finance call; refusal rolls back and logs Failed; disabled/missing rule logs Unposted; unmapped, wrong-type, inactive and cross-tenant accounts refuse; travel settlement legs; payroll-offset skip; foreign advance evidence; reversed-row generation; edit guard.
- `HrFinancePostingCatalogueTests` (5) — every event has a balanced builder inside its roles; distinct actions per source type; zero amounts skip; the two area services still route through the adapter and never touch `IJournalEntryService`.
- Live: `D:\Rhema\TDC ERPS\dev-harness\hr-finance\run-slice1.mjs` (outside the repo, see its README) — maps the roles, enables the events, walks medical approve → pay and travel advance → claim → pay, reads the journals back from Finance, asserts idempotency, reversal and the edit guard. Requires the baseline database (§ 5).
