# HR & SHE finance posting — operations manual

**Written 2026-09-21.** For the people who operate this daily: the HR desk, the HR administrator,
and the Finance desk receiving what HR sends.

**What this covers.** What to do, what happens by itself, what to check, and what to do when
something is not right. It assumes no accounting knowledge. Where a term needs explaining, it is
explained at the point of use; the fuller explanation is in
[`HR-FINANCE-ACCOUNTING-PRIMER.md`](../integration/HR-FINANCE-ACCOUNTING-PRIMER.md).

**What this does not cover.** Why the accounting is shaped the way it is (the primer) or how it is
built ([`HR-FINANCE-POSTING-DESIGN.md`](../integration/HR-FINANCE-POSTING-DESIGN.md)). For the
management summary and the outstanding decisions, see
[`HR-FINANCE-INTEGRATION-EXECUTIVE-BRIEF.md`](../programme/HR-FINANCE-INTEGRATION-EXECUTIVE-BRIEF.md).

---

## Contents

1. [The one-paragraph version](#1-the-one-paragraph-version)
2. [For the HR desk — what changes in your day](#2-for-the-hr-desk--what-changes-in-your-day)
3. [Reading the Finance box on a record](#3-reading-the-finance-box-on-a-record)
4. [For the HR administrator — the register](#4-for-the-hr-administrator--the-register)
5. [The five statuses, and what to do about each](#5-the-five-statuses-and-what-to-do-about-each)
6. [Setting it up — account roles and events](#6-setting-it-up--account-roles-and-events)
7. [Corrections: reversal, and why you cannot just edit](#7-corrections-reversal-and-why-you-cannot-just-edit)
8. [For the Finance desk — what arrives from HR](#8-for-the-finance-desk--what-arrives-from-hr)
9. [Troubleshooting — the messages you will actually see](#9-troubleshooting--the-messages-you-will-actually-see)
10. [Routine checks](#10-routine-checks)
11. [Where every screen is](#11-where-every-screen-is)

---

## 1. The one-paragraph version

When you approve or pay something in HR that involves money, the entry is created in Finance at
the same moment, automatically. You do not tell Finance separately. If Finance will not accept it
— most often because the accounting month is closed — **your HR action is cancelled too**, and
you are told why in plain words. Nothing is ever half-done. Everything that happens is listed on
one screen, the *register*, where an administrator can retry a failure or reverse something posted
in error.

---

## 2. For the HR desk — what changes in your day

**Almost nothing, and that is the point.** You approve a travel claim exactly as you did before.
You pay a medical claim exactly as you did before. The difference is that the accounts are updated
as you do it, and you can see that it worked.

Three things are genuinely new:

### 2.1 A Finance box appears on the record

After an action that involves money, the record shows a small box saying what reached Finance. See
§ 3 for how to read it.

### 2.2 An approval can now be refused for an accounting reason

If Finance will not accept the entry, your approval **does not go through**. You will see a
message such as *"Posting period is not open"*. This is not a system error — it means the
accounting month has been closed and nothing more can be recorded in it.

**What to do:** do not retry repeatedly. Tell your HR administrator, who will check with Finance.
The action will work once the underlying cause is fixed, and nothing has been lost in the
meantime.

### 2.3 Once something has reached Finance, you cannot edit it

If you try to change or delete a claim whose entry has already reached the accounts, you will be
refused, and the message will name the accounting entry. This is deliberate: the accounts have
already reported that cost, and silently changing it would make the reported figures wrong.

**What to do:** ask your HR administrator to *reverse* the posting (§ 7). Once reversed, the
record unlocks and you can correct it normally.

---

## 3. Reading the Finance box on a record

The box appears on these screens:

| Area | Screen |
|---|---|
| Medical claims | HR → Medical → Claims → *open a claim* |
| Medical NHIS | HR → Medical → NHIS |
| Travel claims | HR → Travel → Claims → *open a claim* |
| Awards | HR → Awards → *open an award* |
| Long-service awards | HR → Awards → Long service |
| Leave encashments | HR → Leave → Encashments |
| Separations | HR → Separations → *open a separation* |
| Asset surcharges | HR → Assets → Surcharges → *open a surcharge* |
| Training service bonds | HR → Service bonds |
| Consulting invoices | HR → Consulting → Invoices |
| Safety incidents | HR → Safety → Incidents → *open an incident* |

It shows one line per money event on that record — a claim will usually have two, one for approval
and one for payment. Each line gives the status, the amount, and either the Finance entry number
(if it worked) or the reason (if it did not).

**Buttons you may see:**

| Button | Who sees it | What it does |
|---|---|---|
| **Post now** | HR administrators | Sends an entry that has not yet reached Finance |
| **Refresh** | Anyone who can view | Asks Finance for the current status of an invoice it is handling. Only on supplier and client invoices |
| **Reverse** | HR administrators | Cancels a posted entry. Asks for a reason. See § 7 |

If you see no buttons, there is nothing to do — which is the normal case.

---

## 4. For the HR administrator — the register

**Administration → HR Settings → Finance Posting → Register**

The register is the complete list of every money event HR and SHE have produced, with what
happened to each. This is the screen to watch.

It can be filtered by status. In normal running you are looking for two things:

- **Failed** — something Finance refused. Needs attention.
- **Unposted** — the event fired but its rule is switched off. Expected while a rule is
  deliberately off; unexpected otherwise.

**Posted**, **Skipped** and **Reversed** need no action.

Each row gives the event, the source record, the amount, the date, and — for anything that did not
post — Finance's own reason in its own words, not a generic message.

---

## 5. The five statuses, and what to do about each

| Status | What it means | What to do |
|---|---|---|
| **Posted** | The entry reached the accounts. The row holds the Finance entry number | Nothing |
| **Failed** | Finance refused it. **The HR action was cancelled too** — the claim was not approved, the payment was not recorded. The row exists so the refusal is visible | Fix the cause (§ 9), then **Post now**. The HR user may also need to redo their action |
| **Unposted** | The event happened but its rule is switched off, so nothing was sent. **The HR action went ahead normally** | Nothing, if the rule is off on purpose. If you later switch it on, these rows are the backlog — post them from the register |
| **Skipped** | Nothing needed posting. A zero amount, an award with no money attached, or a payment that payroll will handle | Nothing. Not a failure, and it cannot be retried |
| **Reversed** | A posted entry was deliberately cancelled. The row keeps the reason and the reversal entry number | Nothing |

> **The distinction that matters most: Failed means nothing happened anywhere.** Unposted means
> the HR side went through and only the accounting was skipped. Confusing the two leads to chasing
> claims that were never approved, or assuming a cost reached the accounts when it did not.

---

## 6. Setting it up — account roles and events

**Administration → HR Settings → Finance Posting**

Requires the HR administrator permission. Everything is off until this is done.

### 6.1 Account roles tab

HR does not know your chart of accounts. It knows *kinds* of money — "travel expense", "what we
owe employees" — and calls each one a **role**. Here you map each role to a real account.

There are sixteen. You only need the ones your enabled events use; the Events tab tells you which
are missing.

The system checks the account you pick is active and of the right kind — you cannot map an expense
role to a bank account. If the account you need does not exist, that is a conversation with
Finance, not something to work around by picking a near-enough account.

### 6.2 Events tab

One card per money event, showing what triggers it, what it does, and a switch.

| On the card | Meaning |
|---|---|
| **ready** | Its accounts are mapped; it can be switched on |
| **map …** | Named roles are still unmapped. **The switch will not turn on** until they are |
| **AP invoice** / **AR invoice** badge | This one creates a supplier or client invoice in Finance rather than a direct accounting entry |
| **Settled** dropdown | Only on events where the document does not say how the employee is paid. Choose *Directly* (HR's action is the payment) or *Through payroll* (payroll clears it) |
| The counts at the bottom | How many have posted, and how many are waiting |

> **A note on "Through payroll".** Choosing it means HR records the obligation and expects
> payroll's own entry to clear it. That only works if the payroll component is mapped to the same
> account HR uses. **Until the payroll owner has done that mapping, choosing this route will leave
> balances that never clear.** Confirm with the payroll owner before selecting it.

### 6.3 Recommended order

1. Map the account roles for the area you are switching on.
2. Confirm each event shows **ready**.
3. Switch on one event, put a test transaction through, and check the register.
4. Then switch on the rest of the area.

Do not switch everything on at once. If the account mapping is wrong, one event's worth of
corrections is much easier than twenty-six.

---

## 7. Corrections: reversal, and why you cannot just edit

Once an entry has reached the accounts, the source record in HR is locked — you cannot edit it,
re-review it, or delete it.

**Why.** The accounts have already reported that cost in a particular month. If HR could quietly
change the figure afterwards, the reported month would be wrong and nothing would show that it had
changed. Every properly controlled accounting system works this way.

**How to correct something:**

1. Open the record, or find the row in the register.
2. Press **Reverse** and give a reason. The reason is stored permanently and is visible to Finance.
3. Finance creates a cancelling entry. The original stays, with the reversal beside it.
4. The HR record unlocks. Correct it, and re-approve.
5. A new entry is created. The register shows both attempts.

**Who can do this:** HR administrators only. A reversal creates a real accounting entry, so it is
not a routine desk action.

**What Finance sees:** the original, the reversal, the reason, and the corrected entry. That
complete trail is the point — a clean-looking record with no evidence of the correction is worth
less to an auditor than a messy one that shows exactly what happened.

---

## 8. For the Finance desk — what arrives from HR

Three different things arrive, and they land in three different places.

### 8.1 Direct accounting entries — most of what HR sends

Twenty-four of the twenty-six events create an accounting entry directly. They appear in the
general ledger as they happen, identified as coming from HR, and need no action from you.

They will not appear in a closed month — the system refuses, and the HR user is told.

**You can lock HR out of a period independently.** HR is registered as its own source, so
restricting HR does not affect payroll or any other module.

### 8.2 Supplier invoices — recruitment costs

*Currently switched off; see the executive brief § 6.1.*

When enabled, an approved recruitment cost arrives as a **supplier invoice in Accounts Payable**,
submitted for approval, referencing the HR record. You approve, pay and post it under your normal
controls. HR reads the status back and shows it on the HR record; once you record payment, the
payment details are written onto the HR record automatically.

**HR cannot withdraw an invoice once you are working on it.** Withdrawal is only possible while it
is still a draft; after that HR is refused and told to speak to Accounts Payable.

**One case needs you:** if the expense account is under budget control, the invoice arrives as a
**draft** rather than submitted, because HR does not reserve Finance budget. Accounts Payable
attaches the budget line and submits it. The register row says so explicitly.

### 8.3 Customer invoices — consulting

*Currently switched off; see the executive brief § 6.2.*

When enabled, sending a consulting invoice in HR raises a **customer invoice in Accounts
Receivable** for the billed hours before tax — **your tax group is authoritative, not the
percentage typed in HR**. You issue and collect it as normal. When you record the receipt, HR's
record is updated to Paid or Partially paid, and HR can no longer mark it paid by hand.

### 8.4 What HR never does

- **HR never touches a bank account.** HR records that a payment was authorised, into a holding
  account. Your cash function moves it to the bank after reconciling. If that holding account
  carries a balance that never clears, a handoff has been missed — worth checking periodically.
- **HR never records a debt owed to an outside party directly.** Those always come to you as a
  supplier invoice.
- **HR does not attribute costs to a department or project** — no decision has been made on how it
  should. See the executive brief § 7.1.

---

## 9. Troubleshooting — the messages you will actually see

| Message | What it means | Who fixes it |
|---|---|---|
| **"Posting period is not open"** | The accounting month is closed | Finance — either open the period or the entry waits for the next one |
| **"Accounting book is unavailable for posting"** | Finance's accounting book is not active | Finance |
| **"…not enabled for the requested accounting book"** | The account exists but is not enabled for that book | Finance |
| **"map <role>" on an event card** | An account role is unmapped, so the event cannot be switched on | HR administrator — § 6.1 |
| A posting row says **Skipped — payroll** | Correct behaviour: payroll's own entry clears this one | Nobody, unless the payroll mapping is not in place (§ 6.2) |
| **Duplicate** on a retry | Finance already holds this entry. It is not lost | Nobody. Check the register for the original |
| An HR edit is refused, naming an entry | The record is locked because it has posted | HR administrator — reverse first (§ 7) |
| A consulting invoice shows **Failed** but the invoice was still sent | Expected for client invoices only: the send completes first, then the invoice is raised. A refusal leaves a retryable row rather than undoing the send | HR administrator — **Post now** once the cause is fixed |

**If a whole area suddenly fails**, it is almost always one of three things, in this order of
likelihood: the month has closed, an account mapping was changed, or Finance's book setup changed.
Check the register — the reason is on every row, in Finance's own words.

---

## 10. Routine checks

| When | Check | Where |
|---|---|---|
| **Daily** | Any **Failed** rows | Register, filter Failed |
| **Weekly** | **Unposted** rows for events that are supposed to be on | Register, filter Unposted |
| **Monthly, before close** | Everything for the month is Posted, not Failed | Register |
| **Monthly** | The holding account balance is near zero | Finance — see executive brief § 4 |
| **Monthly** | Payroll-settled obligations are being cleared | Finance, on the staff-claims account |
| **When anything is switched on** | One test transaction end to end before enabling the rest | Register |

The monthly holding-account check is the most valuable and the most often skipped. A balance that
does not clear means HR recorded a payment that the cash function never completed — a real
discrepancy, and this is the only place it shows up.

---

## 11. Where every screen is

| Screen | Path | Permission |
|---|---|---|
| Account roles, events, register | Administration → HR Settings → Finance Posting | View: HR Company read · Change: HR Company admin |
| The Finance box on a record | On each source record — see § 3 | View: HR Company read · Retry and reverse: HR Company admin |
| Manpower budget vs Finance actuals | HR → Job analysis → *a budget* | HR read |
| Training budget vs Finance actuals | HR → Training → Budgets → *a budget* | HR read |

**Note on the budget screens.** These *read* what Finance says was actually spent on the account
the budget is charged to, and show the variance. HR does not write to Finance's budget and does
not reserve against it. If a budget cannot be read, the screen says why in a sentence — usually
that the organisation unit is not linked to an account.

---

## Related documents

| For | Document |
|---|---|
| Management summary and outstanding decisions | [`HR-FINANCE-INTEGRATION-EXECUTIVE-BRIEF.md`](../programme/HR-FINANCE-INTEGRATION-EXECUTIVE-BRIEF.md) |
| The accounting explained from scratch | [`HR-FINANCE-ACCOUNTING-PRIMER.md`](../integration/HR-FINANCE-ACCOUNTING-PRIMER.md) |
| The technical design | [`HR-FINANCE-POSTING-DESIGN.md`](../integration/HR-FINANCE-POSTING-DESIGN.md) |
| Building the demo database | [`UAT-DEMO-DATABASE.md`](UAT-DEMO-DATABASE.md) |
