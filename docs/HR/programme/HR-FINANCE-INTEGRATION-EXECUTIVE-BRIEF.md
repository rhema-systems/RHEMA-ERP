# HR & SHE → Finance integration — executive brief

**Prepared:** 2026-09-21 · **For:** TDC management and the Finance owner
**Status:** Built and verified on the UAT database. Two capabilities ship switched off, awaiting
decisions in § 6 and § 7.

**Purpose.** To describe, in business terms, what now happens to money that moves through HR and
SHE; what has stopped being manual; what remains manual deliberately; and the decisions TDC needs
to make before the remainder can be switched on.

This brief contains no accounting detail and no technical detail. The working documents behind it
are listed at the end.

---

## 1. What changed, in one page

**Before.** HR and SHE approved money every day — travel claims, medical reimbursements, staff
advances, awards, final settlements, fines, recruitment invoices. None of it reached the accounts
by itself. Each approval was a record in HR that somebody had to re-enter into Finance, usually in
batches, usually later, and sometimes not at all. The gap between "HR approved it" and "Finance
knows about it" was measured in days or weeks, and nothing in either system proved the two agreed.

**Now.** Twenty-six money events across HR and SHE reach the general ledger **at the moment the
authorising decision is taken**, through a single controlled connection to the Finance module.
Approving a travel claim and recording the cost in the accounts are no longer two jobs; they are
one action that either succeeds completely or does not happen at all.

**What that buys the business:**

| | |
|---|---|
| **Costs land in the right month** | A claim approved on 28 September is a September cost, even if it is paid in October. Monthly figures stop being distorted by payment timing |
| **Nothing is re-keyed** | The single largest source of error between two systems is removed, along with the effort |
| **Employee balances are visible** | What staff owe the company (advances outstanding, fines, unrecovered surcharges) and what the company owes staff are now real figures in the accounts, not spreadsheets in HR |
| **Nothing can be silently lost** | Every event leaves a record — posted, failed, skipped or deliberately not posted. There is no fourth outcome where something quietly disappears |
| **The books cannot be edited behind Finance's back** | Once a claim has reached the accounts, HR cannot change it. Corrections are explicit, reasoned and traceable |

**What it does not change.** Finance remains in sole control of the accounts, the chart of
accounts, the calendar and the bank. HR does not choose accounts, does not touch bank balances,
and cannot post into a closed month. HR supplies the business facts; Finance's rules decide what
happens to them.

---

## 2. The money events, by area

All twenty-six are live. "Recognise" means the cost or obligation is recorded at approval;
"settle" means the payment is recorded when it happens.

| Area | What now reaches Finance automatically |
|---|---|
| **Staff travel** | An advance handed to a traveller is recorded as money the employee holds. The trip's cost is recognised when the claim is approved. On payment, the advance is recovered automatically and only the balance is paid out |
| **Medical** | Claim costs at approval and payment. Insurance premiums, split between the employer's share and the employees' share. Money recovered from insurers and from NHIS |
| **Leave** | Leave days paid out instead of taken |
| **Awards** | Cash awards when conferred and when paid, including where the amount changes between the two. Long-service awards when processed |
| **Benefits** | Benefit claims at approval and at payment |
| **Separation** | A leaver's full and final settlement in one entry — earnings, leave encashment, benefits, and every deduction: outstanding loans and advances, property recoveries, statutory deductions. A leaver who owes more than they are due is handled correctly |
| **Employee recoveries** | Asset surcharges (raised, recovered, waived), disciplinary fines (imposed, settled, forgiven), and training bonds broken by early exit (raised, repaid, waived) |
| **Recruitment** | Agency and advertising costs go to Accounts Payable as a supplier invoice — *switched off, see § 6* |
| **Consulting** | Client invoices for consultants' billed hours go to Accounts Receivable — *switched off, see § 6* |
| **SHE (Safety)** | Insurance proceeds received against a workplace incident |

**SHE's footprint is deliberately small.** Safety has exactly one money event, because insurance
proceeds are the only monetary figure the SHE module holds. Incident management, inspections,
health surveillance, first aid, wellness and return-to-work carry no money and therefore have
nothing to send. This is correct, not incomplete.

---

## 3. Who does what now

| Role | Before | Now |
|---|---|---|
| **HR desk** | Approve in HR, then notify Finance separately | Approve in HR. That is the whole job. The screen shows whether the entry reached Finance |
| **Finance desk** | Re-enter HR's approvals; chase HR for what was missed | Receives entries as they happen. Reviews the exception list rather than the whole flow |
| **Payroll** | Unchanged | Unchanged. Where a claim is settled through the payroll run, HR records the obligation and payroll's own entry clears it — *subject to one configuration step, § 7* |
| **HR administrator** | — | New responsibility: maintain the account mapping and watch the exception register (§ 5) |
| **Finance owner** | — | Retains full control: account policy, the calendar, and the ability to lock HR out of a period independently of any other module |

**The one genuinely new duty** is watching the register. It is a short daily or weekly look at a
single screen, described in the operations manual.

---

## 4. Three routes, and why they differ

Money does not all travel the same way, because the party at the other end is not always the same.

**1. Employees — a direct ledger entry.** Twenty-four of the twenty-six events. The company is
dealing with its own staff, and the obligation is recorded straight into the accounts.

**2. Suppliers — an Accounts Payable invoice.** When the payee is an outside company — a
recruitment agency, an insurer — HR does not record the debt itself. It raises a proper supplier
invoice in Finance, which then runs through Finance's own approval, payment and controls. HR reads
the status back and shows it on the HR record.

*Why it matters to you:* the supplier appears in Finance's payables, gets a payment voucher, ages
correctly, and is paid under Finance's authorisation rules — not HR's. This is a control
improvement, not a technical nicety.

**3. Clients — an Accounts Receivable invoice.** The same principle in reverse. When HR bills a
consulting client, Finance raises the customer invoice, chases it and records the receipt. HR is
told when it is paid.

**In every case, HR never touches the bank.** HR can record that a payment was authorised; only
Finance's cash function confirms that money actually left the account, after matching the bank
statement. That separation is deliberate and is a standard financial control.

---

## 5. When something goes wrong

Every event produces a record with one of five outcomes, visible on one screen:

| Outcome | Meaning | Action |
|---|---|---|
| **Posted** | It reached the accounts | None |
| **Failed** | Finance refused it — most often a closed month or a missing account setup. **The HR approval was rolled back too**, so the two systems never disagree | Fix the cause in Finance, then retry from the register |
| **Unposted** | The rule for this event is switched off. The HR action went ahead and the event was logged | Nothing, unless you later switch the rule on — the log is then the back-fill queue |
| **Skipped** | Nothing to post — a zero amount, or a payment that payroll will clear | None |
| **Reversed** | A posted entry was deliberately cancelled | None |

Two design choices worth stating to a board:

- **A failure cancels the HR action as well.** We chose not to let HR mark a claim "paid" while
  Finance holds no record of it — that would be an obligation that vanished from the books. If
  Finance refuses, nothing happened anywhere, and the reason is displayed in plain words.
- **A posted entry cannot be quietly edited.** Once a claim has reached the accounts, HR refuses
  to change or delete it. A correction must be an explicit reversal with a stated reason, leaving
  both the original and the correction on the record. This is what makes the figures defensible in
  an audit.

---

## 6. Built, but switched off pending your decision

Two capabilities are complete and tested but **disabled by default**. Neither can be enabled by
accident; each requires an administrator to turn it on deliberately.

### 6.1 Recruitment costs → Accounts Payable

**What it does.** When HR approves a recruitment cost (agency fee, advertising, assessment), a
supplier invoice is raised in Finance and submitted for AP approval.

**Why it is off.** It hands work to the Finance AP function that the Finance owner has not yet
agreed to receive. Five questions were put to Finance and are unanswered — chiefly whether the
invoice should arrive submitted for approval or as a draft for AP to review first, and whether
recruitment spend should be split across several expense categories rather than one.

**What is needed:** the Finance owner's confirmation. Each answer is a configuration change, not
a rebuild.

**If it stays off:** recruitment costs remain an HR record only, and someone continues to enter
the agency's invoice into Finance by hand.

### 6.2 Consulting invoices → Accounts Receivable

**What it does.** When HR sends a timesheet invoice to a consulting client, Finance raises the
customer invoice, collects it, and the payment is reflected back on the HR record.

**Why it is off.** It requires each consulting client in HR to be linked to a customer record in
Finance. That linking has not been done, and doing it is a data decision about which HR clients
are the same legal entities as which Finance customers.

**What is needed:** the client-to-customer links, and confirmation that HR's send is the right
trigger for raising a receivable.

**If it stays off:** consulting invoices remain HR records and are not chased by Finance's
credit-control process.

---

## 7. Decisions we need from TDC

These are not outstanding work. They are choices only TDC can make, and the system is configured
to accept whichever answer is given.

### 7.1 Cost attribution — the significant one

**Today, HR's costs reach the accounts but are not attributed to a department, project or cost
centre.** A travel expense is recorded as a travel expense for the company; it is not recorded
against the unit whose staff travelled or the project they travelled for.

**Why:** Finance supports cost attribution, but the rules for how HR's costs should be attributed
have not been set, and attributing them wrongly is worse than not attributing them at all.

**What is needed:** a decision on how HR costs should be attributed — by the employee's
organisation unit, by the project named on the document, or by a rule per cost type.

**The consequence of deferring:** departmental and project profitability will not include
HR-originated costs. For staff travel and recruitment in particular, that is a material omission
from any per-project cost figure.

### 7.2 Payroll settlement — a configuration step

Several events can be settled either by direct payment or through the payroll run, and TDC can
choose per event. Where payroll is chosen, HR records the obligation and payroll's own entry is
expected to clear it — **which only works if the relevant payroll component is mapped to the same
account HR used.** That mapping belongs to the payroll owner and has not yet been made.

**Until it is made:** payroll-settled obligations will be recorded but will not be cleared,
leaving a growing balance of amounts that appear still owed to staff when they have in fact been
paid.

**What is needed:** the payroll owner to map the relevant components. This has been raised with
them formally.

### 7.3 Budgets — confirm the approach

HR maintains its own budget envelopes (manpower, training, awards, travel). These now **read**
what Finance says has actually been spent, and show the variance. They do not reserve money in
Finance's budget or write back to it.

**What is needed:** confirmation that HR budgets remain planning tools, with Finance's budget as
the single controlling authority — or a decision to merge them, which is a larger piece of work.

**Recommendation:** leave as is. It is the simpler arrangement and it keeps one authority over
budget control.

---

## 8. Deliberately still manual — and why

Not everything with a money figure on it should reach the ledger. Three were excluded on purpose:

| | Why |
|---|---|
| **Training budget transactions** | These record spending against an invoice or purchase order that Finance has *already* processed. Posting them again would double-count the cost |
| **Succession development activity costs** | The same reason: a record of a cost Finance pays through its own supplier process |
| **Awards and travel budget figures** | Planning figures maintained by the relevant desks, not accounting transactions |

The general rule applied throughout: **HR posts the events it authorises, and reads — never
re-posts — costs that another module already owns.** This is what prevents the same expense
appearing twice in the accounts.

---

## 9. Readiness and what comes next

**Built and verified.** All twenty-six events were built between 20 and 21 September 2026 and
verified against a live UAT database, with each entry read back from Finance and checked line by
line — including reversals, duplicate protection, and the refusal of edits to posted records.

**Before go-live, three things are required:**

1. An administrator maps each account role to a real account in the live chart. The system refuses
   to enable an event whose accounts are not properly mapped, so this cannot be half-done.
2. Finance confirms which accounts HR should use. The demonstration chart has no dedicated
   staff-claims, medical or travel accounts; whether to create them is Finance's decision.
3. Finance's standard setup must be complete for the live company — the accounting calendar open
   for the current month, and the accounting book active.

**Then the decisions in §§ 6 and 7**, each of which is a configuration change rather than
development.

---

## Supporting documents

| For | Document |
|---|---|
| The people operating this daily | `docs/HR/operations/HR-FINANCE-POSTING-OPERATIONS-MANUAL.md` |
| The complete technical design | `docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md` |
| Every HR/SHE record that carries money | `docs/HR/integration/HR-FINANCE-ENTITY-SWEEP.md` |
| The accounting explained from first principles | `docs/HR/integration/HR-FINANCE-ACCOUNTING-PRIMER.md` |
| Other questions awaiting TDC | `docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md` |
