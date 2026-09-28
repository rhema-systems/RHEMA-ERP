# Finance for the HR/SHE developer — an accounting primer

**Written 2026-09-21.** Every example in this document is an account, an event or a journal that
exists in this repository. Nothing here is invented for teaching.

**Who this is for.** Whoever maintains HR/SHE and has to reason about money **with no accounting
background at all**. It assumes you know nothing — not what a "receivable" is, not what "debit"
means. It defines every term the first time it appears, in plain English, before using it.

**What this is not.** It is not a statement of what is built — that is
[`HR-FINANCE-POSTING-DESIGN.md`](HR-FINANCE-POSTING-DESIGN.md), and where the two disagree, that
one is right. It is not a Finance-module specification; the Finance owner's docs live in
`docs/Finance/`. It does not decide anything.

**How to read it.** Lesson 0 is vocabulary — read it slowly, it does most of the work. Lessons 1–3
are the entire model. Lessons 4–9 are the consequences, and you can stop after any of them and
come back. The appendices are lookup tables.

---

## Contents

| | Lesson | The question it answers |
|---|---|---|
| **0** | [**The words, in plain English**](#lesson-0--the-words-in-plain-english) | **What is a receivable? A payable? An asset?** |
| 1 | [Accounting is a conservation law](#lesson-1--accounting-is-a-conservation-law) | What *are* debits and credits? |
| 2 | [The chart of accounts](#lesson-2--the-chart-of-accounts) | Which bucket does this money go in? |
| 3 | [One journey, traced: the travel claim](#lesson-3--one-journey-traced-the-travel-claim) | How do the pieces fit together? |
| 4 | [Accrual — why approval and payment are two events](#lesson-4--accrual--why-approval-and-payment-are-two-events) | Why post twice for one claim? |
| 5 | [Clearing accounts and module boundaries](#lesson-5--clearing-accounts-and-module-boundaries) | Why does HR never touch the bank? |
| 6 | [Subledgers, control accounts, reconciliation](#lesson-6--subledgers-control-accounts-and-reconciliation) | Why does the period close block? |
| 7 | [Trial balance, P&L, balance sheet](#lesson-7--trial-balance-pl-balance-sheet) | Where does my journal end up? |
| 8 | [Periods, books and the close](#lesson-8--periods-books-and-the-close) | Why can't I post into last year? |
| 9 | [The three shapes HR money takes](#lesson-9--the-three-shapes-hr-money-takes) | Journal, AP invoice or AR invoice? |
| A | [Every HR/SHE event and its journal](#appendix-a--every-hrshe-event-and-its-journal) | |
| B | [The sixteen account roles](#appendix-b--the-sixteen-account-roles) | |
| C | [Glossary](#appendix-c--glossary) | |

---

## Lesson 0 — The words, in plain English

Accounting is not conceptually hard. It is **badly named**. The vocabulary is five hundred years
old, translated from Italian, and almost every term means something narrower or stranger than it
sounds. Once the words are decoded, the ideas underneath are simple. So: the words first.

### An account is a bucket

An **account** is just a labelled bucket that money-amounts are added to and taken out of. "Cash",
"Rent", "Money employees owe us" — each is an account. Nothing more mysterious than that.

An account's **balance** is the total sitting in it right now.

The **general ledger** (**GL**) is the complete set of every account and every movement ever
recorded in them. "The ledger" and "the books" mean the same thing. It is, quite literally, a
table of transfers.

### A journal is one transfer

A **journal entry** (or just "a journal") is one recorded movement: *this much came out of that
bucket and went into this one.* **Posting** a journal means committing it to the ledger. Once
posted, it is permanent — you never edit or delete a posted journal. If it was wrong, you post an
opposite one to cancel it, which is called a **reversal**. (That's not bureaucracy: an auditor must
be able to see that a mistake was made and corrected, not just see the tidy end state.)

### The five kinds of bucket

Every account in the world is one of these five. This is the most important table in the document.

| Type | In plain English | Everyday example | Our example |
|---|---|---|---|
| **Asset** | Something we **own**, or that someone **owes us**. Things of value we hold. | Your bank balance. Your car. A friend who owes you 50. | Cash `1000`, the money an employee holds as a travel advance |
| **Liability** | Something we **owe** someone else. A future obligation. | Your credit card bill. Your mortgage. | Accounts Payable `2000`, an approved claim not yet paid to the employee |
| **Equity** | What would be **left over for the owners** if we sold every asset and paid every debt. Assets minus liabilities. | Your net worth. | Share Capital `3000`, Retained Earnings `3100` |
| **Revenue** | Value we **earned**. Money coming in *because we did something*. | Your salary. | Consulting revenue, an insurance payout |
| **Expense** | Value we **used up**. The cost of operating. | Your rent, your groceries. | Travel expense, Salaries `6000` |

Two distinctions people get wrong, worth pausing on:

- **Revenue is not the same as cash coming in.** If you borrow money, cash comes in, but you haven't
  *earned* anything — you've taken on a liability. Revenue means value earned.
- **An expense is not the same as cash going out.** If you lend someone money, cash goes out, but
  nothing was consumed — you swapped cash for a claim on that person. You still have the value; it
  just changed shape. **This distinction is the single most important one in this whole document**,
  and Lesson 3 is built around it.

### The "-able" words: receivable and payable

These two trip up everyone, and they are the same word in two directions.

> **Receivable** = money we are *able to receive*. **Somebody owes us.** It is an **Asset**,
> because being owed money is a thing of value.
>
> **Payable** = money we are *liable to pay*. **We owe somebody.** It is a **Liability**, because
> it is an obligation hanging over us.

So:

- **Accounts Receivable** (**AR**) = money **customers owe us** for work we've done. Asset.
- **Accounts Payable** (**AP**) = money **we owe suppliers** for things we've bought. Liability.
- **Staff advances receivable** = money **employees owe us** because we handed them cash in advance
  for a trip. Asset.
- **Staff claims payable** = money **we owe employees** for expense claims we approved but haven't
  paid yet. Liability.

The trick that makes it stick: **read the word from the company's point of view, and ask "who is
waiting for money?"** If we're waiting — receivable, asset. If they're waiting — payable, liability.

A few more terms built from these:

- **Aging** (or "ageing") — a report that groups receivables or payables by how long they've been
  outstanding: 0–30 days, 31–60, 61–90, 90+. The older a receivable is, the less likely you'll ever
  collect it. This is why receivables of different kinds must not be mixed together: a travel
  advance from last week and a disciplinary fine from last year are different collection stories.
- **Write-off** — giving up on a receivable. You accept that the money is never coming, so you
  remove the asset and record the loss as an expense.
- **Settle** — to clear a payable or receivable, usually by paying it.
- **Outstanding** — not yet settled. Still owed.

### Some other words you'll meet

| Word | What it actually means |
|---|---|
| **Debit** / **Credit** | Two sides of a transfer. **Not** "in" and "out", **not** good and bad. Lesson 1 defines them properly |
| **Accrual** | Recording an obligation when it *arises*, not when cash moves. Lesson 4 |
| **Post** | Commit a journal to the ledger, permanently |
| **Reversal** | A new, opposite journal that cancels a posted one |
| **Period** | Usually a calendar month. Every journal belongs to one |
| **Close** | Freezing a finished period so its numbers stop changing |
| **Reconcile** | Prove two records that should agree actually agree, to the cent |
| **Chart of accounts** | The list of all the buckets. Lesson 2 |
| **Subledger** | A detailed breakdown behind one summary account. Lesson 6 |
| **Clearing account** | A temporary holding bucket between two systems. Lesson 5 |

That's the vocabulary. Everything from here builds on it.

---

## Lesson 1 — Accounting is a conservation law

Here is the core idea, and it is genuinely one idea:

> **Money never appears and never vanishes. It only moves between buckets.**

Every event is a *transfer*. When you pay rent, money doesn't disappear — it moves from the "Cash"
bucket into the "Rent expense" bucket. When a customer pays an invoice, value moves from "Accounts
receivable" into "Cash". Accounting is a ledger of transfers with a type system, and the types are
the five from Lesson 0.

### The equation

The five bucket types arrange into one equation that must **always** be true:

```
    Assets  +  Expenses      =      Liabilities  +  Equity  +  Revenue
    └──────── LEFT ────────┘        └─────────────── RIGHT ───────────┘
```

And now the two words:

> **Debit means "add to the left side". Credit means "add to the right side".**

That is the whole definition. Debit is not "money in" and credit is not "money out" — your bank
uses those words from *its* point of view, which is the opposite of yours, which is exactly why
they feel backwards. Forget the bank. Debit = left, credit = right.

Everything follows:

| A **debit** increases | A **credit** increases |
|---|---|
| **Assets** — what we own or are owed | **Liabilities** — what we owe |
| **Expenses** — value consumed | **Equity** — the owners' stake |
| | **Revenue** — value earned |

And to *decrease* a bucket, you do the opposite: a credit reduces an asset, a debit reduces a
liability.

### The invariant

> **In every journal, total debits equal total credits.**

That is what keeps the equation balanced. It's the whole reason the system is called
**double entry**: every transfer is recorded twice, once on each side, so the books can never
silently drift. Finance's posting engine enforces it — an unbalanced journal is rejected before it
touches the ledger. If your builder emits lines that don't add up, this is the error you get.

### Why expenses sit on the left with assets

This is the one genuinely odd part, so here is the reason. An expense makes the company *worth
less* — paying rent reduces the owners' stake. Equity lives on the right. So an expense is really
a negative on the right-hand side. Rather than carry negatives around, accounting **moves it to
the left**, where it behaves like an asset: it grows with a debit.

That's it. That's the only sleight of hand in the entire system. Sit with it until it feels
obvious, because "why on earth is an expense a debit?" is the question that blocks most people.

### Contra accounts

Occasionally an account lives in one section but carries the **opposite** balance, because netting
the two together would destroy information you need. These are called **contra accounts**. Three
are in our chart:

- **`1590` Accumulated Depreciation** is typed `Asset` but holds a credit balance. It sits next to
  `1500` Property, Plant & Equipment and reduces it. Why not just lower `1500`? Because then you'd
  lose what the asset originally cost. Keeping them separate lets you see *"cost 100,000, worn down
  by 30,000, worth 70,000"* instead of just *"70,000"*.
- **`4210` Sales Discounts Allowed** is typed `Revenue` but holds a debit balance — it reduces
  revenue while preserving how much was invoiced before discounts.
- **`1990` Migration Clearing Account** — a clearing account, covered in Lesson 5.

If you meet an account whose type looks backwards, check for this before assuming a bug.

> **In our code.** Every line your builders emit picks a side. `Dr Travel expense / Cr Staff claims
> payable` reads as: *"the expense bucket grew by X (left), and the bucket of what-we-owe-employees
> grew by X (right)."* Left grew, right grew, equally — equation still true. That is the only test
> a single journal must pass.

---

## Lesson 2 — The chart of accounts

The **chart of accounts** is simply the list of every bucket available. Ours is created by
[`FinanceDataSeeder.cs`](../../../src/ErpSystem.Data/Seeders/FinanceDataSeeder.cs) — 45 accounts,
numbered so the first digit tells you the type at a glance.

| Range | Type |
|---|---|
| `1xxx` | Asset — what we own or are owed |
| `2xxx` | Liability — what we owe |
| `3xxx` | Equity — the owners' stake |
| `4xxx` | Revenue — what we earn |
| `5xxx`–`6xxx` | Expense — what we consume |
| `7xxx` | foreign-exchange gains (Revenue) and losses (Expense) |
| `9999` | Suspense — a holding pen for anything that couldn't be classified |

### The seeded accounts in full

**Assets — what we own, or are owed**

| Code | Name | Note |
|---|---|---|
| `1000` | Cash and Cash Equivalents | |
| `1010` | Cash and Bank — Payroll Clearing | payroll's clearing account; the model HR's copied |
| `1020` | Undeposited Cash | clearing |
| `1021` | Cheques Awaiting Deposit | clearing |
| `1022` | Mobile Money Clearing | clearing |
| `1023` | Card Settlement Clearing | clearing |
| `1100` | Accounts Receivable | what customers owe us — **AR control account**, Lesson 6 |
| `1120` | Staff Loans and Salary Advances | payroll's |
| `1130` | Withholding Tax Receivable | tax withheld from us, reclaimable |
| `1200` | Inventory | stock we hold |
| `1500` | Property, Plant & Equipment | buildings, machines, vehicles |
| `1510` | Buildings | |
| `1520` | Equipment | |
| `1530` | Vehicles | |
| `1590` | Accumulated Depreciation | **contra-asset** — wear-and-tear to date |
| `1990` | Migration Clearing Account | the opening-balance buffer, Lesson 5 |
| `9999` | Suspense Account | |

**Liabilities — what we owe**

| Code | Name | Note |
|---|---|---|
| `2000` | Accounts Payable | what we owe suppliers — **AP control account** |
| `2100` | Accrued Expenses | costs incurred but not yet invoiced to us |
| `2110` | GRV Accrual Control | goods received, supplier's invoice not yet arrived |
| `2120` | Accrued Payroll Payables | payroll's |
| `2200` | Tax/VAT Control | tax we've collected and owe the authority |
| `2500` | Long-term Debt | |

**Equity — the owners' stake**

| Code | Name | Note |
|---|---|---|
| `3000` | Share Capital | what the owners put in |
| `3100` | Retained Earnings | profits kept in the business rather than paid out |

**Revenue — what we earn**

| Code | Name | Note |
|---|---|---|
| `4000` | Sales Revenue | |
| `4100` | Service Revenue | |
| `4110` | Rental Income | |
| `4210` | Sales Discounts Allowed | **contra-revenue** |
| `4900` | Other Income | |
| `4910` | Purchase Discounts Received | |
| `4920` | Payroll Recoveries and Interest Income | payroll's |
| `7100` | Unrealized Exchange Gain | |
| `7200` | Realized Exchange Gain | |

**Expenses — what we consume**

| Code | Name |
|---|---|
| `5000` | Cost of Goods Sold |
| `6000` | Salaries and Wages |
| `6020` | Salaries, Wages and Payroll Costs *(payroll's)* |
| `6100` | Rent Expense |
| `6200` | Utilities Expense |
| `6300` | Depreciation Expense |
| `6400` | Marketing and Advertising |
| `6500` | Professional Fees |
| `6600` | Bank Charges |
| `7110` | Unrealized Exchange Loss |
| `7210` | Realized Exchange Loss |

> **A note on depreciation**, since it appears twice above and is a classic beginner stumble. Buy a
> vehicle for 100,000 and you haven't spent 100,000 this month — you swapped cash for a vehicle,
> which you still own. But the vehicle *does* wear out over, say, five years. So each month you
> record a slice of that wearing-out as an expense (`6300` Depreciation Expense) and add the same
> slice to `1590` Accumulated Depreciation, which reduces what the vehicle is carried at. Spreading
> a long-lived asset's cost over its useful life is all depreciation is.

### Why HR posts to *roles*, not to these codes

Those 45 accounts are a **demonstration** chart. A real client brings their own, with different
numbers and usually far more detail. So HR never names an account code. It names a **role** — "the
travel expense account", "the staff claims payable account" — and an administrator maps each role
to one of that client's real accounts, once, in HR Settings → Finance Posting.

Sixteen roles exist ([Appendix B](#appendix-b--the-sixteen-account-roles)). This isn't
over-engineering; it's the line between *what kind of money this is* (HR's business knowledge) and
*which account that maps to* (the client's accounting policy). It's also why
`HrFinancePostingAdapter` **refuses a role the catalogue entry didn't declare** — the catalogue is
the contract, so a builder reaching outside it fails at the boundary instead of quietly landing in
the wrong bucket.

> **The rule to remember.** Never hard-code an account code in HR. If a new event needs a bucket no
> role covers, add a role.

---

## Lesson 3 — One journey, traced: the travel claim

This is the whole model in one story. An employee takes a **GHS 2,000** advance for a trip; the
trip costs **GHS 3,000**, approved in full.

### Step 1 — `TRAVEL_ADVANCE_DISBURSED` — we hand over 2,000

| | Account role | Type | Effect | Amount |
|---|---|---|---|---|
| Dr | Staff advances receivable | Asset | ↑ | 2,000 |
| Cr | Staff payments clearing | Asset | ↓ | 2,000 |

**Read it as:** *"We now hold a 2,000 claim on this employee, and 2,000 of our money is on its way
out."*

Handing an employee money for a trip **is not a cost**. Nothing has been consumed yet. The company
swapped one asset for another: less cash available, but now it holds a *receivable* — the employee
owes either the trip or the money back. Both are assets. The company is exactly as wealthy as it
was a minute ago.

This is why `StaffAdvancesReceivable` is an Asset and not an Expense, and it's Lesson 0's "cash out
is not the same as an expense" made concrete.

### Step 2 — `TRAVEL_CLAIM_APPROVED` — the trip happened, 3,000 approved

| | Account role | Type | Effect | Amount |
|---|---|---|---|---|
| Dr | Travel expense | Expense | ↑ | 3,000 |
| Cr | Staff claims payable | Liability | ↑ | 3,000 |

**Read it as:** *"The trip cost us 3,000, and we now owe the employee 3,000."*

*Now* it's a cost — the travel was consumed and the company accepted the bill. Two things happened
at once: the profit-and-loss took a 3,000 hit, and a 3,000 obligation to the employee appeared.
**No cash has moved.** That is not a gap; it's the point (Lesson 4).

### Step 3 — `TRAVEL_CLAIM_PAID` — settling up

| | Account role | Type | Effect | Amount |
|---|---|---|---|---|
| Dr | Staff claims payable | Liability | ↓ | 3,000 |
| Cr | Staff advances receivable | Asset | ↓ | 2,000 |
| Cr | Staff payments clearing | Asset | ↓ | 1,000 |

**Read it as:** *"We've cleared the 3,000 we owed. 2,000 of it by cancelling the advance the
employee already has, and 1,000 by actually paying them."*

Cancelling a receivable against a payable instead of moving cash twice — rather than the employee
repaying 2,000 and us paying 3,000 — is called **contra-settlement**. It's the most common pattern
in employee accounting, and it's `AdvanceDeducted` in the code.

### Step 4 — Finance's Cash module, not HR

| | Account | Amount |
|---|---|---|
| Dr | Staff payments clearing | 3,000 |
| Cr | Bank | 3,000 |

### The tally — why all of this was worth it

| Account role | Movements | Final balance |
|---|---|---|
| Staff advances receivable | +2,000, −2,000 | **0** |
| Staff claims payable | +3,000, −3,000 | **0** |
| Staff payments clearing | −2,000, −1,000, +3,000 | **0** |
| Travel expense | +3,000 | **3,000** |
| Bank | −3,000 | **−3,000** |

Everything temporary nets to **zero**. What survives is exactly the truth: **the trip cost 3,000,
and 3,000 left the bank.**

So why not just record "3,000 left the bank" and skip the rest? Because the intermediate accounts
record **who owed what to whom, at each moment** — and that is the entire value of accounting. On
the day after step 1, the books can tell you an employee is holding 2,000 of company money. On the
day after step 2, they can tell you the company owes an employee 3,000 and that September carried
a 3,000 travel cost. A bank statement can tell you none of that.

Read that table until it's obvious. Every other event in
[Appendix A](#appendix-a--every-hrshe-event-and-its-journal) is a variation on it.

---

## Lesson 4 — Accrual — why approval and payment are two events

Notice the expense landed at **approval**, not at payment. That's deliberate, and it's the
foundational rule of **accrual accounting**:

> An obligation is recorded when it **arises**, not when the cash moves.

The alternative is **cash accounting** — record only actual cash movements. It's simpler, it's what
your bank statement does, and it lies about *which month* a cost belongs to.

Take a trip approved 28 September, paid 3 October. Under cash accounting, September looks
artificially profitable (the cost hasn't shown up) and October artificially expensive (it carries a
cost from work done in September). Neither month tells you what it actually cost to run the
business. Under accrual, the cost sits in September where it belongs, and October just shows cash
leaving.

The formal name is the **matching principle**: costs are reported in the same period as the
activity they relate to.

Two consequences for us:

1. **Every "recognise" event must fire at the approval, in the same database transaction.** If the
   approval commits and the posting doesn't, the ledger is missing a September cost with no trace
   that it should be there. This is why the adapter owns the transaction, and why
   `HR-FINANCE-POSTING-DESIGN.md` § 4 keeps journals and AP invoices on the all-or-nothing path.
2. **The pair isn't optional.** An event that only posts on payment loses the period information
   permanently — there's no way to reconstruct it later. Where a document genuinely has no approval
   step (a disciplinary fine, where *imposing it* is the authorising act), the catalogue says so
   explicitly rather than quietly skipping recognition.

> **In our code.** This is the one-line justification for the design doc's *"Approval and payment
> are two events on purpose. HR may approve in one month and pay in the next; Finance must see the
> liability the day it arises and the cash the day it goes."*

---

## Lesson 5 — Clearing accounts and module boundaries

Why does HR credit *Staff payments clearing* instead of *Bank*?

Because **HR is not entitled to claim that money left the bank.** Only Finance's Cash module knows
that, and only after matching against a real bank statement. HR knows something weaker but true:
*a payment was authorised and recorded.*

A **clearing account** is a temporary holding bucket between two parties. One side puts money in,
the other side takes it out, and — this is the property that matters —

> **a clearing account should always return to zero.**

A clearing balance that sits there and never clears isn't a balance; it's a **dropped handoff**.
That makes it one of the most useful things to go looking for when something's wrong, because it
tells you precisely which side failed to do its half.

Think of it like a parcel locker. The courier puts a parcel in; you take it out. An empty locker is
normal. A locker with a parcel that's been sitting for three weeks means somebody didn't collect.

Six clearing accounts exist in our chart, all working this way:

| Account | Sits between |
|---|---|
| `1010` Cash and Bank — Payroll Clearing | payroll's journal ↔ the Cash module |
| `1020`–`1023` Undeposited Cash, Cheques, Mobile Money, Card Settlement | a cashier taking money ↔ the bank deposit that confirms it |
| `1990` Migration Clearing Account | every opening-balance source ↔ the final equity entry |

`1990` shows the pattern at its cleanest, and it's worth understanding because it explains the
whole opening-balance process. When a company first goes live on a new system, you have to load
where they already stand — existing supplier debts, customer debts, assets, stock, bank balances.
That's called loading **opening balances**. Each source posts its own side against Migration
Clearing. When they're all in, the accumulated balance in `1990` *is* the opening equity, and a
final entry clears it to zero.

The Finance owner's rule is that **the server derives that final amount from what's actually
posted** — nobody types it. If `1990` doesn't reach zero, a source is missing or wrong and the
go-live isn't done. See [`docs/Finance/governed-opening-balances.md`](../../Finance/governed-opening-balances.md).

> **The general lesson.** When two modules share a money event, neither writes to the real account.
> They meet at a clearing account, and the fact that it nets to zero is the proof the handoff
> completed. That is the design pattern behind HR's entire posting boundary.

---

## Lesson 6 — Subledgers, control accounts and reconciliation

The general ledger holds **one** account for everything we owe suppliers: `2000` Accounts Payable.
But the business obviously needs to know *which* supplier is owed *what*, and since when. That
detail lives in a **subledger** — a separate, detailed table with one row per invoice, per supplier.

| | Holds | Where |
|---|---|---|
| **Subledger** | one row per document, per counterparty | `api/ap/invoices`, `api/ar/invoices` |
| **Control account** | the single GL account summarising the whole subledger | `2000` for AP, `1100` for AR |

The rule binding them:

> **The sum of the subledger must equal the control account. Exactly. To the cent.**

Reconciling is just checking that. If they differ, one of two things is true: something was posted
straight to the control account with no subledger document behind it, or a subledger document
never posted. Both are serious. The first means a supplier's balance is wrong; the second means the
accounts understate what the company owes — which, if you publish them, is a misstatement.

This is why the period close *blocks* rather than warns.
`GET api/ap/reports/control-reconciliation` computes the difference, and a variance of `0.01` or
more stops both close preparation and final close.
`docs/Finance/tdc-finance-subledger-close-controls-and-baseline-seeding.md` records the decision:
AP and AR control checks are **non-waivable**.

Two practical notes:

- **Posting isn't enough; the read model must be rebuilt.** The aging reports, the control
  reconciliation and the close checks all read a precomputed *settlement projection*, not the
  invoice rows directly. After posting AP or AR documents, call
  `POST api/ap/reports/settlements/rebuild` (and the AR twin), or everything downstream reads empty
  and looks broken when it isn't.
- **HR's `Staff claims payable` has no subledger.** It's a plain GL account, so the ledger knows the
  total owed to employees but not the per-employee breakdown. The nearest thing to a subledger is
  `HrFinancePostingRecords` — the posting register. That's a real structural difference from AP, and
  worth knowing before anyone asks for an employee-payable aging report.

---

## Lesson 7 — Trial balance, P&L, balance sheet

Everything you post ends up in three reports, all derived from the same ledger. These three *are*
"the accounts" that a company publishes.

### The trial balance

Every account, with its balance — debits in one column, credits in the other. **The two columns
must total the same number.** That's Lesson 1's invariant, aggregated across the whole ledger. It's
the first thing an accountant checks, because if it doesn't balance, nothing built on top of it can
be trusted. `/finance/reports/trial-balance`.

### The profit & loss (also "income statement", "P&L")

**Revenue minus Expenses, over a period.** Only the `4xxx`, `5xxx`, `6xxx` and `7xxx` accounts
appear. It answers: *"did we make money last month?"* `/finance/reports/income-statement`.

### The balance sheet

**Assets = Liabilities + Equity, at a single moment.** Only `1xxx`, `2xxx` and `3xxx` appear. It
answers: *"what do we own and owe today?"* `/finance/reports/balance-sheet`.

The mental split: **the P&L is a video of a period; the balance sheet is a photograph of a day.**

### How they connect

At year end, the P&L accounts are emptied out and the year's profit is rolled into `3100` Retained
Earnings — an Equity account. That's what keeps the balance sheet balanced from one year to the
next, and it's why `3100` exists in the chart even though nothing posts to it day to day. Profit
the owners leave in the business becomes part of their stake.

### Where HR's roles land

| Role | Statement | What a non-zero balance means |
|---|---|---|
| Travel / Medical / Awards / Benefits / Separation / Recruitment expense | **P&L** | cost incurred this period |
| Staff receivable write-off | **P&L** | debts we gave up collecting |
| Employee recoveries income, Insurance recoveries income, Consulting revenue | **P&L** | earned this period |
| Staff claims payable | **Balance sheet** | we owe employees this, right now |
| Statutory deductions payable | **Balance sheet** | we owe a tax authority this |
| Staff advances receivable | **Balance sheet** | employees are holding this much of our money |
| Staff receivables | **Balance sheet** | employees owe us this |
| Staff payments clearing | **Balance sheet** | *should be about zero* — see Lesson 5 |

> **The distinction that matters most.** An **advance** is a balance-sheet item; a **claim** is a
> P&L item. Booking an advance as an expense overstates this month's costs *and* hides money the
> company is owed — two errors from one mistake. Preventing that is exactly why the roles are split
> the way they are, and why `StaffAdvancesReceivable` (money we handed out) is kept separate from
> `StaffReceivables` (money employees owe us for fines, surcharges and broken bonds). Both are
> "an employee owes us", but they age differently and are chased differently, so mixing them ruins
> both reports.

---

## Lesson 8 — Periods, books and the close

### Fiscal years and periods

A **fiscal year** is the company's reporting year; a **fiscal period** is usually a month inside it.
Every journal must name a period, and that period must be **open and unlocked** to accept it.

Our seed creates FY2025 **closed and locked** and FY2026 **open** with twelve periods. So a test
posting dated 2025 is refused. That's not a bug — it's the mechanism working.

### The close

At the end of each period, Finance **closes** it: runs the checks, then freezes it so nobody can
change history. The reason is simple — a published statement can't stay true if last month's
numbers keep moving underneath it.

Our close templates ship with ten tasks, including the non-waivable AP and AR control
reconciliations from Lesson 6, seeded by
[`FinanceCloseTemplateBaselineSeeder`](../../../src/ErpSystem.Data/Seeders/FinanceCloseTemplateBaselineSeeder.cs)
for every active client.

**What this means for HR:** once a period is closed, an HR correction to an approved claim *cannot*
silently change last month's cost. It has to be a **reversal** — a new, opposite journal in the
current period, leaving the original in place as evidence. That's exactly what the register's
reverse action does, and why HR's guards refuse to let you edit a source document whose posting
still stands. If that guard has ever felt like friction, this is what it's protecting.

### Accounting books

A **book** is a set of rules a journal is posted under. The same real-world transaction can
legitimately be valued one way for statutory reporting and another for management reporting, so a
system can keep parallel books. Most clients run one. `AccountingBooksController`; a book's period
must be initialised before anything can post to it.

---

## Lesson 9 — The three shapes HR money takes

`HrFinancePostingKind` has three values, and choosing between them is an accounting decision, not a
technical one.

### `Journal` — the general case

A direct entry to the general ledger. Use it when **the counterparty is an employee**, or there
isn't one at all. 24 of our 26 events are journals.

### `VendorInvoice` — money we owe an outside party

Use it when **the payee is not an employee** — a recruitment agency, an insurer, a facilities firm.

Why not just write a journal? Because an external debt belongs in the **AP subledger** (Lesson 6).
A journal straight to `2000` would put a balance in the control account with no subledger row
behind it — instantly breaking the reconciliation that the close depends on, and leaving no
supplier record, no payment terms, no aging, no payment voucher. So HR raises a proper Finance
vendor invoice and submits it into AP; Finance approves, pays and posts it under its own controls,
and HR reads the status back.

> **The rule.** HR never journals an outside party's debt. `REQUISITION_COST_APPROVED` is the one
> event of this kind today.

### `CustomerInvoice` — money an outside party owes us

The mirror image. `TIMESHEET_INVOICE_SENT` raises an **AR customer invoice** for consulting hours,
for the same reason: receivables belong in the AR subledger where Finance can age and chase them.

⚠ One asymmetry worth knowing: the AR invoice is raised **after** HR's commit, not inside it,
because Finance's AR service opens its own database transaction and can't join HR's. So a Finance
refusal leaves a Failed register row to retry, rather than rolling the whole send back. Journals
and AP invoices keep the all-or-nothing path.

---

## Appendix A — every HR/SHE event and its journal

26 events, from [`HrFinancePostingEventCatalog.cs`](../../../src/ErpSystem.Core/Services/HR/Finance/HrFinancePostingEventCatalog.cs).
Shorthand: `clearing` = Staff payments clearing · `payable` = Staff claims payable ·
`receivable` = Staff advances receivable. For authoritative detail and every edge case, read
[`HR-FINANCE-POSTING-DESIGN.md`](HR-FINANCE-POSTING-DESIGN.md) § 3.

### Medical

| Event | Journal |
|---|---|
| `MEDICAL_CLAIM_APPROVED` | Dr Medical expense / Cr payable |
| `MEDICAL_CLAIM_PAID` | Dr payable / Cr clearing |
| `MEDICAL_PREMIUM_PAID` | Dr Medical expense (employer share) + Dr Staff receivables (employee share) / Cr clearing |
| `MEDICAL_INSURER_RECOVERY_RECEIVED` | Dr clearing / Cr Insurance recoveries income |
| `NHIS_CLAIM_REIMBURSED` | Dr clearing / Cr Insurance recoveries income |

### Staff travel

| Event | Journal |
|---|---|
| `TRAVEL_ADVANCE_DISBURSED` | Dr receivable / Cr clearing |
| `TRAVEL_CLAIM_APPROVED` | Dr Travel expense / Cr payable |
| `TRAVEL_CLAIM_PAID` | Dr payable / Cr receivable (advance recovered) + Cr clearing (net paid) |

### Leave, awards, benefits

| Event | Journal |
|---|---|
| `LEAVE_ENCASHMENT_PROCESSED` | Dr Leave encashment expense / Cr clearing *(direct)* or Cr payable *(payroll)* |
| `AWARD_CONFERRED` | Dr Awards expense / Cr payable; no money attached → Skipped |
| `AWARD_PAID` | Dr payable / Cr clearing; recognises and settles in one journal if the conferral never posted |
| `LONG_SERVICE_AWARD_PROCESSED` | Dr Awards expense / Cr clearing or payable; unpriced rung → Skipped |
| `BENEFIT_UTILIZATION_APPROVED` | Dr Benefits expense / Cr payable |
| `BENEFIT_UTILIZATION_PAID` | Dr payable / Cr clearing |

### Separation

| Event | Journal |
|---|---|
| `SEPARATION_SETTLEMENT_RELEASED` | one journal, a leg per line — earnings Dr Separation / Leave encashment / Benefits expense; deductions Cr receivable, Cr Employee recoveries income, Cr Statutory deductions payable; the net Cr clearing *(direct)* or Cr payable *(payroll)*. A leaver who owes more than they are due is Dr receivable |

### Employee receivables — money employees owe us

| Event | Journal |
|---|---|
| `ASSET_SURCHARGE_APPROVED` | Dr Staff receivables / Cr Employee recoveries income |
| `ASSET_SURCHARGE_RECOVERED` | Dr clearing / Cr Staff receivables; payroll or exit-settlement method → Skipped |
| `ASSET_SURCHARGE_WAIVED` | Dr Staff receivable write-off / Cr Staff receivables |
| `DISCIPLINE_FINE_IMPOSED` | Dr Staff receivables / Cr Employee recoveries income |
| `DISCIPLINE_FINE_SETTLED` | Dr clearing (paid) and/or Dr write-off (forgiven) / Cr Staff receivables |
| `TRAINING_BOND_BREACHED` | Dr Staff receivables / Cr Employee recoveries income (pro-rata repayment) |
| `TRAINING_BOND_SETTLED` | Dr clearing / Cr Staff receivables |
| `TRAINING_BOND_WAIVED` | Dr write-off / Cr Staff receivables |

### Safety (SHE)

| Event | Journal |
|---|---|
| `SHE_INSURANCE_CLAIM_RECEIVED` | Dr clearing / Cr Insurance recoveries income, for the amount paid; filed-but-unpaid → Skipped |

SHE's **only** money event. Everything else in SHE — surveillance, first aid, wellness,
return-to-work — carries no monetary field, so there's nothing to post. Note the direction: money
coming *in* credits Revenue, the exact mirror of an expense claim.

### Outside parties — not journals

| Event | Kind | Effect |
|---|---|---|
| `REQUISITION_COST_APPROVED` | **AP invoice** | one line Dr Recruitment expense to the cost's supplier, submitted into AP approval; payee a person rather than a supplier → Skipped |
| `TIMESHEET_INVOICE_SENT` | **AR invoice** | one service line Cr Consulting revenue for the billed hours before tax; client not linked to a Finance customer → Skipped |

⚠ **Finance de-duplicates on (source type, source id, posting action)** — not only on the
idempotency key. That's why the two events on one claim carry different actions, and why a re-post
after a reversal carries a generation suffix (`Approve#2`).

---

## Appendix B — The sixteen account roles

From [`HrFinancePostingEnums.cs`](../../../src/ErpSystem.Core/Enums/HrFinancePostingEnums.cs).
Each is mapped once per client, in HR Settings → Finance Posting.

| # | Role | Type | Statement | In plain English |
|---|---|---|---|---|
| 1 | Staff claims payable | Liability | Balance sheet | we owe employees this for approved claims and awards |
| 2 | Staff advances receivable | Asset | Balance sheet | money we handed over ahead of the spend |
| 3 | Staff payments clearing | Asset | Balance sheet | HR's "paid", until Cash clears it to the bank — *should be ~0* |
| 4 | Medical expense | Expense | P&L | reimbursements to employees and dependants |
| 5 | Travel expense | Expense | P&L | per diems, accommodation, transport, incidentals |
| 6 | Leave encashment expense | Expense | P&L | leave days paid out instead of taken |
| 7 | Awards expense | Expense | P&L | cash and long-service awards |
| 8 | Benefits expense | Expense | P&L | benefit claims reimbursed |
| 9 | Separation expense | Expense | P&L | final pay on exit |
| 10 | Employee recoveries income | Revenue | P&L | recovered from employees — property, penalties |
| 11 | Statutory deductions payable | Liability | Balance sheet | withheld from a settlement, owed to an authority |
| 12 | Staff receivables | Asset | Balance sheet | employees owe us — surcharges, fines, broken bonds |
| 13 | Staff receivable write-off | Expense | P&L | a staff debt we gave up collecting |
| 14 | Recruitment expense | Expense | P&L | the cost of filling a post; the AP invoice line |
| 15 | Insurance recoveries income | Revenue | P&L | insurer, NHIS and incident-claim proceeds |
| 16 | Consulting revenue | Revenue | P&L | consultants' billed hours; the AR invoice line |

Roles **2** and **12** are deliberately separate accounts even though both mean "an employee owes
us". An advance is money we chose to hand over and expect back within weeks; a fine or a broken
bond is a debt we're chasing. They age differently and are collected differently, so combining them
would make both reports useless.

---

## Appendix C — Glossary

| Term | Plain English |
|---|---|
| **Account** | A labelled bucket that amounts go into and out of |
| **Accrual accounting** | Record an obligation when it *arises*, not when cash moves. Lesson 4 |
| **Aging** | A report grouping debts by how long they've been outstanding |
| **AP / Accounts Payable** | What **we owe suppliers**. A liability. Control account `2000` |
| **AR / Accounts Receivable** | What **customers owe us**. An asset. Control account `1100` |
| **Asset** | Something we own, or that someone owes us |
| **Accounting book** | A set of rules a journal is posted under (statutory, management) |
| **Balance** | The total currently sitting in an account |
| **Balance sheet** | Assets = Liabilities + Equity, at a moment in time. A photograph |
| **Chart of accounts** | The list of all available buckets |
| **Clearing account** | A temporary holding bucket between two systems. Should return to zero |
| **Close** | Freezing a finished period so its numbers stop changing |
| **Contra account** | An account carrying the opposite balance to its section, to preserve detail |
| **Control account** | The single GL account summarising a whole subledger |
| **Credit** | Add to the **right** of the equation. Increases Liability, Equity, Revenue |
| **Debit** | Add to the **left** of the equation. Increases Asset, Expense |
| **Depreciation** | Spreading a long-lived asset's cost over the years it's used |
| **Double entry** | Every transaction is recorded on both sides, and must balance |
| **Equity** | What's left for the owners: assets minus liabilities |
| **Expense** | Value consumed. A cost of operating |
| **Fiscal period** | Usually a month. Every journal names one, and it must be open |
| **Fiscal year** | The company's reporting year |
| **GL / General ledger** | Every account and every movement ever recorded. "The books" |
| **Journal entry** | One balanced transfer between accounts |
| **Liability** | Something we owe. A future obligation |
| **Matching principle** | Costs belong to the period whose activity they relate to |
| **Opening balance** | Where a company already stands when it first goes live. Lesson 5 |
| **Outstanding** | Not yet settled. Still owed |
| **P&L / Income statement** | Revenue − Expenses over a period. A video |
| **Payable** | Money we are liable to **pay**. We owe someone. A liability |
| **Posting** | Committing a journal to the ledger. After this it's permanent |
| **Receivable** | Money we are able to **receive**. Someone owes us. An asset |
| **Reconcile** | Prove two records that should agree actually agree, to the cent |
| **Retained earnings** | Profit kept in the business instead of paid to owners |
| **Revenue** | Value we earned (not merely cash that arrived) |
| **Reversal** | Correcting a posted journal with a new, opposite one — never by editing |
| **Settle** | Clear a debt, usually by paying it |
| **Subledger** | Per-counterparty detail sitting behind one summary account |
| **Suspense account** | A holding pen for amounts that couldn't yet be classified |
| **Trial balance** | Every account and its balance; the two columns must total the same |
| **Write-off** | Giving up on a debt: remove the asset, record the loss as an expense |

---

## Where to go next

| You want to | Read |
|---|---|
| Wire a new HR money event | [`HR-FINANCE-POSTING-DESIGN.md`](HR-FINANCE-POSTING-DESIGN.md) § 3 and § 7 |
| Know which HR entity carries money | [`HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md) |
| Understand the Finance boundary rules | [`HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md`](HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md) |
| Load opening balances | [`docs/Finance/governed-opening-balances.md`](../../Finance/governed-opening-balances.md) |
| Understand the period close | [`docs/Finance/tdc-finance-subledger-close-controls-and-baseline-seeding.md`](../../Finance/tdc-finance-subledger-close-controls-and-baseline-seeding.md) |
| See exactly what Finance seeds | [`FinanceDataSeeder.cs`](../../../src/ErpSystem.Data/Seeders/FinanceDataSeeder.cs) |
