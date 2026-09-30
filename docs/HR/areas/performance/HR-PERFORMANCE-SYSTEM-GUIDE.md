# HR Performance Management — System Guide and Demonstration Workbook

**Module:** Human Resources → Talent & Performance → **Performance**
**Coverage:** 19 sidebar leaves · 46 desk routes · 12 Administration setup routes · 15 self-service portal routes — **62 screens**
**Written:** 2026-09-17 · **Verified against** `ErpSystemDB_UAT` as it stands today, and against the source of every screen and service behind it.
**Updated 2026-09-29** for the performance closure's lane A (scoring and the settle path): Rule 2, § 1.5, P-6/P-39/P-40, chapter 30's commit and chapter 31's finalise wording, Appendix C. **And for lane P** (privacy and access): chapter 16's cascade rows and P-19, chapter 21's private notes, check-in rules and P-27, chapter 25's plan authorship, chapter 26's outcome release, chapter 28's nominations, chapter 29's Evidence tab, chapter 30's panel reads, chapter 36's PIP rules and the review meeting's right of reply. **And for lane B1** (one gate evaluator): Rule 3, Rule 9, § 1.6, chapter 4's P-3 and P-62–P-70, and chapters 23, 26–33, 37 and 38 — every step a write waits for, as the gates now hold it. **And for lane L-a** (the goal set's governance): § 1.4, § 1.7 and chapters 17–19 and 38 — a lock that freezes what a goal is and not its year, *lock goal set*, the approved-goal edit rule and *Send back*. **And for lane L-b** (goal rows and scoring): § 1.4 and
chapters 8, 19, 24, 27–31, 33 and 38 — a template's goals section, filled by each employee's locked
goals — and, found on the way, the remand's dead end (P-71: chapters 29 and 33, Appendices C and E).
**And for lane L-c** (the screens): § 1.4; chapter 8's section kind, and P-8, now fixed; the *Lock
set* button in chapters 18 and 19; chapters 27–31 and 33, where the forms, calibration, the sign-off
and HR's appeal decision handle goal rows; and Appendix C, whose Rule 8 row had borrowed P-8's number.
**And for lane C, slice C-a** (the appeal machine, 2026-09-30): chapters 29 and 33 and Appendices C
and E — the remand works (P-71 fixed), HR can extend its deadline or decide once it lapses, a
rejection restores the original, and HR does not decide an appeal it is party to.
**And for lane C, slice C-b** (the appeal reads, 2026-09-30): Rule 9 and chapter 33 — every criterion
the manager scored can be appealed on the page, KPIs and goals included; every appeal screen names,
weighs and scores each row on its own terms (a KPI by its actual against its target); each item keeps
what it scored when the appeal was filed; HR's review shows the peers and the weights and opens a
decided appeal; and the outcome says whether a score moved (P-47, P-49 and P-50 fixed).
**And for lane D** (peer nominations, 2026-09-30): chapters 28 and 29 — a nomination starts pending
and changes only its due date and instructions; the line manager cannot be a peer; a rejected
nomination leaves room for another; in *Manager* mode the manager's nominations need no approval;
HR's advance past the step asks the peers; the peer reads the nomination's due date and
instructions; with anonymous reviews in *Manager* mode the appraisee sees counts, not names; and
*Peer feedback* lists every criterion a peer scored, weighted.
**And for lane E, slice E-a** (the appraisal routes, 2026-09-30): chapters 31 and 32 — *Correct dates*
changes the window alone, and only before the appraisal is final; *Remove* only before anything in it
counts; *Return to manager* only before the sign-off, taking the calibration with it; the raw status
routes open a Draft appraisal and close a Completed one, nothing else; HR does not act on their own
appraisal; the acknowledgment keeps a comment.
**And for lane E, slice E-b** (calibration, 2026-09-30): Rule 2 and chapter 30 — a commit takes each
appraisal once, and only the evaluation the panel sat over, so running it again undoes neither an upheld
appeal nor a return; the grid says why a commit would leave each row alone, and *Commit ratings* appears
only when a commit would take someone; a session can be cancelled, which releases its appraisals, as
deleting one now does; *Mark convened* is gone — opening stamps the start; the facilitator is whoever
creates and opens the session; an open session's scope is fixed; an adjustment stays on what it restated;
and the grid's *Calibrated* column reads the appraisal's settled score (P-41 fixed).
**And for lane E, slice E-d1** (Withdrawn, 2026-09-30): chapters 14, 31 and 37 — HR's review page can
withdraw an appraisal, with a reason, until it is final; leaving withdraws the leaver's unfinished
appraisals; a withdrawn appraisal leaves every count, queue and list and takes no more work, and stays
on the record without a score; the cycle's *Targeted* reads its scope, beside *Appraisals* and
*Withdrawn*; the dashboard's feed records withdrawals, and the dashboard no longer times out.
**The database was rebuilt on 2026-09-29**: Rule 2, Rule 9, the demo tables and the Efua arithmetic give its figures (Efua 88.56, Cynthia 87.00, Efua's appeal window to 6 October). The rest of the guide still describes 2026-09-17; the closure plan (`HR-PERFORMANCE-FINAL-CLOSURE-PLAN.md`) tracks what each later lane changes.
**Seventh in the series**, after Recruitment, Employees, Leave, Attendance & Time, Company Schedule and Staff Travel.

---

## This document is two things at once

**A system guide.** Every screen in the Performance module: what each control does, which endpoint it
calls, which table it writes, and which rule decides whether it is allowed. Read it in order and you
will know the module the way its author does.

**A demonstration workbook.** Every chapter carries a numbered walk — where to click, what you will
see, what to say — written so you can follow it from a second laptop without looking up from the
script. Where a step changes the database it is marked **🔴 LIVE WRITE** and numbered, and Appendix E
tells you how to put each one back.

The two halves are interleaved on purpose. A demonstration the presenter does not understand reads as
a demonstration. One the presenter understands reads as a product.

> **Why this module needs a longer preface than the others.** Performance is the largest and most
> interconnected module in HR. It has more switches than any other (the settings profile alone carries
> forty), more actors (employee, peer, manager, calibration panel, HR, Managing Director), and it is
> the only module whose *output* becomes another module's *input* — a pay rise, a promotion, a
> training place, an improvement plan. That is why it is worth mastering, and it is why the nine rules
> below exist: there are nine things about the demonstration database that will trip you if you meet
> them for the first time in front of the room.

---

## The map

**The full walk is about 95 minutes** plus 20 minutes' preparation. Appendix D has a 25-minute short
path for when it is not.

| | Chapter | Screen | As | min |
|---|---|---|---|---|
| | **Rules · Conventions** | the nine things that decide whether this works | — | *read first* |
| | **1** | How performance hangs together — the spine, the tables, the vocabularies, **the arithmetic**, the settings profile, where approval happens, the cast | — | |
| | **2** | Before the room fills — the prep | — | 20 |
| **I · SETUP** | **3** | `/administration/hr/performance` — the setup hub | `hr.head` | 2 |
| | **4** | Appraisal Settings — **the policy profile** | `hr.head` | 5 |
| | **5** | Appraisal Criteria | `hr.head` | 1 |
| | **6** | Grade Definitions | `hr.head` | 2 |
| | **7** | KPI Definitions | `hr.head` | 1 |
| | **8** | Appraisal Templates — **the form** | `hr.head` | 5 |
| | **9** | Strategic Goals | `hr.head` | 1 |
| | **10** | Goal Library | `hr.head` | 2 |
| | **11** | Goal Risk Thresholds | `hr.head` | 2 |
| **II · THE CYCLE** | **12** | `/hr/performance` — the module hub | `hr.head` | 2 |
| | **13** | Appraisal Cycles — the register | `hr.head` | 2 |
| | **14** | The cycle and its **seven tabs** — targets, templates, **coverage**, progress | `hr.head` | 8 |
| **III · GOALS** | **15** | Company Goals | `hr.head` | 3 |
| | **16** | Unit Goals | `hr.head` / `head.dev` | 3 |
| | **17** | My Goals — the employee's leg 🔴 | `staff` | 4 |
| | **18** | Employee Goals — the desk register 🔴 | `hr.head` | 4 |
| | **19** | Team Goals — the manager's governance desk 🔴 | `head.dev` | 4 |
| | **20** | Goals At Risk | `hr.head` | 3 |
| **IV · THE YEAR** | **21** | Check-ins, and what they change 🔴 | `head.dev` | 4 |
| | **22** | The performance journal | `head.dev` / `staff` | 3 |
| | **23** | Conversations 🔴 | `staff` / `head.dev` | 3 |
| | **24** | Interim reviews | `head.dev` | 3 |
| | **25** | Development plans | `hr.head` / `head.dev` | 3 |
| **V · YEAR-END** | **26** | My Appraisals | `staff` | 4 |
| | **27** | The self-evaluation form | `staff` | 3 |
| | **28** | Peer reviews | `new.hire` | 2 |
| | **29** | **Team Appraisals — the manager evaluation** | `head.dev` | 6 |
| | **30** | Calibration 🔴 | `hr.head` | 5 |
| | **31** | **HR Review — the sign-off** 🔴 | `hr.head` | 6 |
| | **32** | Acknowledgment and the employee's response 🔴 | `head.dev` | 3 |
| | **33** | Appeals — both sides 🔴 | `staff` / `hr.head` | 6 |
| **VI · OUTCOMES** | **34** | Recommendations | `hr.head` | 4 |
| | **35** | Proposals — pay and employment actions 🔴 | `hr.head` / `md.tdc` | 5 |
| | **36** | Improvement plans | `hr.head` | 5 |
| **VII · OVERSIGHT** | **37** | Analytics | `hr.head` | 4 |
| | **38** | Deadline enforcement | `hr.head` | 2 |
| | **39** | The close | — | 2 |
| **Appendices** | **A** | Route index — every route, its gate, who opens it | | |
| | **B** | **The demo data card** — tear this out | | |
| | **C** | Known gaps, ranked | | |
| | **D** | The 25-minute short path | | |
| | **E** | Reset — what the walk changed and how to undo it | | |
| | **F** | If something goes wrong on stage | | |

🔴 = the chapter contains a numbered live write.

---

## Before anything else: the nine rules that decide whether this demo works

Read these once now, and once again on the morning. Every one of them is something the system
genuinely does — none is a defect you are covering up — and every one has a sentence you can say out
loud.

---

### Rule 1 — There is exactly **one** cycle, **one** settings profile and **one** form

The whole demonstration lives inside these three records:

| | |
|---|---|
| **Cycle** | `APC2026` — *Annual Performance Cycle 2026*, 1 Jan → 31 Dec 2026, status **Open**, opened 2 Jan 2026 |
| **Settings profile** | *Standard Annual Appraisal* — self 10%, peers 20%, manager 70%; 2–4 peers; calibration, HR review, acknowledgment and appeals all ON |
| **Form** | *Standard Employee Template 2026* — 2 sections (KPIs 60%, Core Competencies 40%), 4 scored items, 1 free-text question |
| **Appraisals** | **107** generated — 73 Draft, 30 Active, 1 Governance, 3 Completed |

Every screen with a cycle picker preselects `APC2026` and offers one option. Do not go looking for a
second cycle; there isn't one.

💬 *"Everything you are about to see happens inside one appraisal cycle. The cycle is the container —
it decides who is appraised, on which form, against which deadlines, and under which policy. Change
the policy and you change the year, not the software."*

---

### Rule 2 — Calibration is a **gate**, and the panel's number **survives** HR sign-off

> **Changed 2026-09-29** (performance closure lane A). Until then the panel's number did *not*
> survive sign-off; this rule said so. **The database was rebuilt the same day**, so the two
> appraisals that were signed off before the fix — the evidence this rule used to point at — now
> carry the panel's figures like every other: points 1 and 3 below describe the rebuilt database.

This is the single most important thing to understand before you demonstrate calibration.

**1. The gate — the real feature.** Because the settings profile has *Require a calibration session*
switched on, an appraisal **cannot be finalised by HR** until a calibration session covering it has
been **committed**. Committing now calibrates the appraisals **at the calibration step** — the ones
whose manager has submitted — including the people the panel discussed and left exactly as they
were. The rest of the session's scope is left alone and **listed with the reason** in the commit's
result ("the manager has not submitted", "under appeal", "already final, and this session made no
adjustment to it"). On this database both panels (Operations Directorate, and Finance &
Administration) were committed under that rule, so **4 of the 107 appraisals carry the stamp** —
Kojo Fiadzo, Kwasi Danquah, Efua Seidu and Cynthia Sarpong, the four whose manager had submitted —
and the other 103 were listed with the reason. *(Before the 2026-09-29 rebuild a commit stamped
everyone in scope, and 76 carried it.)*

**2. The number — it now survives.** A committed **overall** restatement is kept on the appraisal as
its *calibrated overall*, and HR sign-off keeps it: the final score, the grade and the talent-pool
rating all come from the panel's figure. A **per-criterion** restatement goes into the score through
the item — a **KPI** included, where the panel restates the *achievement percentage* and every screen
labels the row **"overridden by calibration/appeal"**.

**3. What this database shows — both panels' restatements are in the scores.**
- **Cynthia Sarpong** — the Finance & Administration panel restated her overall to **87.0**, and her
  appraisal reads **87.00**: the calibrated overall is the final score. The calibration grid shows
  87.0 too.
- **Efua Seidu** — the Operations panel moved *Project Delivery Timeliness* from 92 to 88 (the row
  carries `NumericScore 88` beside `ActualValue 92`), and her appraisal reads **88.56**, the panel's
  88 counting. The grid shows **88.56** too, 2.24 below the 90.80 the panel started from — *since
  2026-09-30* (lane E-b, P-41): the grid read the adjustment record, which for a criterion restatement
  holds no overall, so her *Calibrated* cell was blank.

Both stay *Exceeds*. *(Until the 2026-09-29 rebuild both still read the figures they had been signed
off at before the fix — 88.74 and 89.40. Finalised scores are never restated automatically, decision
D-13; the rebuild scored them afresh.)*

**What to do on stage.** Calibration now demonstrates end to end — restate an overall in a panel,
commit, sign off in HR review, and the number that reaches the employee and the talent pool is the
panel's. **Cynthia is the example of an overall restatement and Efua of a criterion restatement**:
the grid, the HR review and the record agree on both.

💬 *"The panel's decision is the final number: restate the overall and that is what HR signs off and
what reaches the talent pools; restate a criterion and the score moves with it."*

---

### Rule 3 — The **Finalise** button is enabled before the calibration gate is satisfied

On `/hr/performance/hr-review/[id]`, **Finalise** is disabled only when self, manager or the minimum
peer evaluations are outstanding. It is **not** disabled when the appraisal has never been through a
panel. Press it on an uncalibrated appraisal and the server refuses with a 422 whose message is
perfectly clear —

> *This cycle requires calibration: the appraisal must be committed through a calibration session
> before it can be finalised.*

— but it is still a red toast in front of an audience.

On this database the **only** appraisal in HR's *Ready* queue is Kwasi Danquah's (`TDC/00006`,
head.dev) **and it is calibrated**, so the scripted walk in chapter 31 is safe. The rule matters if
you improvise: do not open a second appraisal and press Finalise on it.

> **Changed 2026-09-29** (performance closure lane B1, B4). **Finalise is now disabled until the
> appraisal is at the HR-review step.** The page reads `canProceedToHRReview`, which now comes from
> the gates the sign-off itself is held to — calibration included (it counted only the self, manager
> and peer evaluations). A refusal that does reach the server names the step: *"HR cannot sign this
> appraisal off yet: this appraisal is at Calibration — no calibration session has committed it."*
>
> Kwasi Danquah passes: the profile requires a mid-year conversation before the year-end evaluation,
> and demo scenario 061 — re-run on this database on 2026-09-29 — holds his mid-year and his final
> review, so the gates put him at *HR Review*, he is the *Ready* row, and LIVE WRITE 10 goes through.
> *(Since slice B-w the mid-year holds the manager's submission, not goal setting — and his manager's
> is in — so the caveat that follows applies only to a database from before this change.)*
> On a database built before that re-run he sits at *Goal Setting* and the sign-off is refused; the
> way through there is HR's audited advance past goal setting (chapter 38, with a reason) and
> chapter 32's final conversation.
>
> ⚠ **Most of the cycle reads *Goal Setting*, as it did before the gates.** 102 of APC2026's 107
> appraisals have no goals, or fewer than the three this profile asks for, so the gates hold them
> there and the dashboard calls them overdue. What is new is that their self-evaluations are refused
> too. That is the rule working, not a fault; say so if the dashboard is on screen.

---

### Rule 4 — Nine goals sit at 0% progress, and are therefore all "at risk"

Only **Efua Seidu** has recorded progress against her goals. Kojo Fiadzo (`gm.ops`), Kwasi Danquah
(`head.dev`) and Cynthia Sarpong (`she.officer`) each have three approved goals sitting at **0%**.

The risk evaluator's second rule is *behind the run rate*: a goal is flagged when
`progress + tolerance < expected`, where *expected* is a straight line from start date to due date. On
17 September 2026, a goal running 1 Jan → 31 Dec is **71.2%** through its life, so with the default
20-point tolerance anything below **51.2%** is flagged. Nine goals at 0% are flagged, and so are the
five fixture goals at 48% and Efua's Civil 3D goal at 40%.

**Expect Goals At Risk to read about 16 rows, 0 of them high-severity, across 9 employees.** The exact
figure moves with today's date — read the tile, do not quote this one.

This is not an embarrassment. It is the best possible illustration of the feature:

💬 *"Nobody set these goals to 'at risk'. The system worked out that a goal due on the 31st of
December, sitting at zero in the middle of September, is behind where a straight line says it should
be — and it did that against a threshold HR owns, on the Goal Risk Thresholds screen, not one we
hard-coded."*

---

### Rule 5 — No TDC employee goal is aligned to a company or unit goal

The cascade is **strategic → company → unit → employee**, and the first three levels are properly
populated: 1 strategic goal, 2 company goals, 3 unit goals, each unit goal linked to a company goal.

But every one of the twelve TDC employee goals shows **Standalone** in the *Aligned to* column. Only
the ten fixture goals belonging to non-TDC facilities and estate employees carry a company-goal link.

Two consequences you will meet:

* On `/hr/performance/unit-goals/[id]`, **"Employee goals cascaded from this goal"** is **empty** for
  all three unit goals.
* On `/hr/performance/employee-goals`, Efua's three goals all read *Standalone*.

**Fix it live — it is the best thirty seconds in the goals chapter.** Chapter 18 has the step: edit
Efua's *Community 25 Phase 2 layout* goal and set **Aligned to** = *Cut the drawing-office cost per
approved layout by 10%* (the Development Department unit goal). The unit-goal detail page then shows
her against it, and the cascade lands end to end. It is **🔴 LIVE WRITE 6**.

---

### Rule 6 — Two screens open empty, and neither is broken

* **`/hr/performance/conversations` → My diary is empty for every persona.** The diary is deliberately
  "scheduled or down to hold **and not yet held**", and all fourteen conversations on this database
  were completed by the process that created them. Use the **About me** tab instead (as `staff`, Efua
  sees her kick-off, her mid-year and her final review), or schedule one live from inside an appraisal —
  chapter 23 does exactly that. *(Fourteen since 2026-09-29: closure lane B1 holds the self-evaluation
  until the kick-off and mid-year are held, so the demo holds a mid-year for all five tracks, and Kwasi's
  final review ahead of his live sign-off. It was eight. Slice B-w moved the mid-year to hold the
  manager's submission instead; the demo's mid-years stay.)*
* **`/hr/performance/calibration` → Open is empty.** Both panels are Completed; the **Completed** tab
  has the two you want. Chapter 30 creates a third session live so the room sees the lifecycle from
  Pending.

---

### Rule 7 — `hr.head` cannot delete anything in this module

The HR role holds `HR.Performance.Read`, `HR.Performance.Write` and `HR.Performance.Approve`. It does
**not** hold `HR.Performance.Admin`, and every destructive action in Performance sits on Admin:

| Action | Screen | What `hr.head` gets |
|---|---|---|
| Delete a cycle | Appraisal Cycles | 403 |
| Delete a settings profile | Administration → Appraisal Settings | 403 |
| Delete an appraisal template | Administration → Appraisal Templates | 403 |
| Delete a company goal | Company Goals | 403 |
| Delete a calibration session | Calibration | 403 |
| Remove a generated appraisal | HR Review detail | **button is hidden** (permission-gated) |

Only the *Remove* button on the HR review screen is hidden rather than shown-and-refused; the rest
render and fail. **Do not press Delete anywhere in this module during the demonstration.**

If it comes up, the design is defensible and worth saying:

💬 *"Deleting an appraisal instrument is a different authority from running the appraisal cycle. HR
runs the cycle; destroying a form somebody has already been scored on is an administrator's act, and
the system will not let the two be confused."*

---

### Rule 8 — Five appraisals belong to non-TDC fixtures and open **completely empty**

`APR-2026-001` … `APR-2026-005` belong to Kofi Facilities, Abena Supervisor, Yaw Facilities, Ama
Estate and Kojo Estate — employees from another module's fixtures, not the TDC establishment. They
were written straight into the table by a reference seeder and therefore **carry no criterion
snapshot**. Open any evaluation screen on one and you get:

> *This appraisal has no scoreable criteria. That means the cycle generated it without a criterion
> snapshot — HR needs to check the template assigned to this employee.*

Every other appraisal on the database — 102 of them — carries its snapshot and opens correctly.

**Never open an appraisal whose number starts `APR-2026-00`.** The real ones are numbered
`APR-2026-APC2026-TDC/00017-0034`: cycle code, employee number, sequence. If you sort or search, sort
by employee name, never by appraisal number.

---

### Rule 9 — An appeal can contest **any criterion the manager scored**, inside the appeal window

* **Every scored criterion is appealable** — a competency, a KPI, or one of the employee's own goals.
  When Efua opens her appeal form she is offered four, by section as her form shows them:
  **Sales Target Achievement** (her actual 104 against a target of 100 — 100 %), **Project Delivery
  Timeliness** (restated by calibration to 88 %), **Communication (84)** and **Teamwork (82)**. A KPI is
  contested on its figure — what was achieved, or how it was counted; a competency on the judgement.
* **The window is enforced** — *Appeal window (days)*, 7 on this profile, counted from the employee's
  acknowledgment (below).

> **Changed 2026-09-30** (performance closure lane C, slice C-b). **This rule read "a competency
> only"**: the appeal page offered competencies alone, though the API took a KPI appeal (Cynthia
> Sarpong's is one) and, since lane L-b, a goal. The page now lists every criterion the manager's
> evaluation scored; the rest of chapter 33 follows.
>
> What the rule said before B1 fixed the window: *"`appealWindowDays` (7 on this profile) is not
> checked. Eligibility is 'the appraisal is Completed and has not been appealed before'."*

The upside is large: **Efua can file a real appeal live**, because her appraisal is Completed and
unappealed. That is **🔴 LIVE WRITE 13**, and it is the strongest five minutes in Part V.

> **Changed 2026-09-29** (performance closure lane B1). **The appeal window is enforced now** —
> *Appeal window (days)* counted from the employee's acknowledgment (else HR's sign-off, else
> completion) — and *Allow appeals* off refuses the appeal outright. The submit, the appeal page and
> *My Appraisals* read one rule. **Efua acknowledged on 29 September 2026 (the day the database was
> rebuilt), so LIVE WRITE 13 works until 6 October 2026**; after that her appeal is refused — *"The
> appeal window closed on 6 Oct 2026…"* — until the database is rebuilt, which acknowledges it afresh.
> Book 0's evening-before rebuild keeps the demo inside the window. (The competency-only half of this
> rule went with closure lane C-b — above.)

---

## Conventions

| Symbol | Meaning |
|---|---|
| 📍 | Where you are — route, sidebar path, persona, minutes |
| 📖 | What it is — the plain-language explanation, before any control is named |
| 👁 | On the page — an exhaustive inventory of what is on screen |
| ▶ | Walk it — numbered steps. Follow these literally |
| 💬 | A line to say. The words in quotation marks are meant to be spoken as written |
| ⚙ | Behind the page — UI element → endpoint → service → table.column |
| ⚠ | Known gaps — what does not work, or works differently from how it reads |
| 🔴 | **LIVE WRITE** — this step changes the database. Numbered; Appendix E reverses it |

**Routes** are written as they appear in the address bar. **Sidebar paths** are written in full, because
a demonstration that navigates by URL looks like a developer's demonstration.

**Table names** have no prefix: the table name is the `DbSet<>` name — `PerformanceAppraisals`,
`EmployeeGoals`, `CalibrationSessions`.

**Personas** are the login usernames. The password for all of them is `Demo123!`.

---
## 1. How performance hangs together

### 1.1 The spine

Everything in this module hangs off four records, in this order:

```
   SETTINGS PROFILE        the policy — who evaluates, at what weight, what must happen before a
        │                  score is final. Named, reusable, and a cycle picks exactly one.
        ▼
   APPRAISAL CYCLE         the run — its year, its window, its phase deadlines, who it covers
        │                  (targets), and which forms it may use (template assignments).
        ▼
   APPRAISAL TEMPLATE      the form — weighted sections holding weighted items. Each item is a
        │                  competency, a KPI, or a free-text question.
        ▼
   PERFORMANCE APPRAISAL   one record per employee per cycle, created by "Generate appraisals".
                           It takes a FROZEN COPY of the template's items, weights and grade
                           bands, so later edits to the form cannot move a scored appraisal.
```

That frozen copy is the single most important design decision in the module. It is why the
self-evaluation, the peer form, the manager form, the calibration grid and the HR review screen all
show the *same* criteria in the *same* order with the *same* weights: they are not four reads of the
template, they are four reads of one snapshot taken the day the appraisal was generated.

### 1.2 The six moving parts

Around that spine, six things run in parallel over the year:

| Part | What it is | Who drives it |
|---|---|---|
| **The goal cascade** | strategic → company → unit → employee goals, weighted to 100 per person | HR sets the top; the employee drafts their own; their manager approves |
| **Check-ins** | one-to-ones held during the year; recording one **moves the goal itself** | the manager |
| **The journal** | contemporaneous evidence, private by default | anyone, about themselves or a direct report |
| **Conversations** | the four formal meetings the cycle requires — kick-off, quarterly, mid-year, final | the manager |
| **Interim reviews** | generated checkpoints; light-touch, or a scored appraisal of the period | employee submits, manager closes |
| **Development plans** | what an appraisal's "areas for improvement" become | the manager drafts, the employee works it |

And at the end, three things come *out* of an appraisal: **outcome recommendations**, which dispatch
into **salary review proposals** and **employment action proposals**, and — where performance has
fallen short — a **performance improvement plan**.

### 1.3 The tables

**Fifty-one tables** carry this module — every name below was checked against the database. Table
name = `DbSet<>` name; there is no prefix.

**Setup**
`AppraisalSettings` · `AppraisalTemplates` · `AppraisalTemplateSections` · `AppraisalTemplateItems` ·
`TemplateItemGradeRanges` · `AppraisalCompetencies` · `AppraisalGradeDefinitions` · `KpiDefinitions` ·
`GoalLibraries` · `StrategicGoals` · `GoalRiskSetting`

**The cycle**
`AppraisalCycles` · `AppraisalCycleTargets` · `AppraisalCycleTargetExclusions` ·
`AppraisalCycleTemplates`

**The run**
`PerformanceAppraisals` · `PerformanceAppraisalCriterionConfigs` (the snapshot) ·
`PerformanceAppraisalCriterionConfigGradeRanges` · `EvaluatorEvaluations` · `CriterionScores` ·
`PeerNominations` · `AppraisalHRReviews` · `AppraisalEmployeeResponses` ·
`AppraisalCustomQuestionResponses` · `AppraisalAttachments` · `AppraisalAppeals` ·
`AppraisalAppealItems`

**The goals**
`CompanyGoals` · `UnitGoals` · `EmployeeGoals` · `GoalProgressEntries` · `GoalRequiredSkills` ·
`EmployeeGoalAppraisalAssessments`

**Around it**
`CheckIns` · `CheckInGoalUpdates` · `CheckInObjectiveLinks` · `PerformanceJournalEntries` ·
`AppraisalConversations` · `AppraisalReviewEvents` · `CalibrationSessions` · `CalibrationParticipants`
· `CalibrationRatingAdjustments` · `EmployeeDevelopmentPlans` · `EmployeeDevelopmentObjectives` ·
`EmployeeDevelopmentPlanFeedbacks`

**Out of it**
`AppraisalOutcomeRecommendations` · `SalaryReviewProposals` · `EmploymentActionProposals` ·
`PerformanceImprovementPlans` · `PipGoals` · `PipReviewMeetings`

### 1.4 The vocabularies

Enums serialise as their **member names**, so what you see on screen is what the API expects back.

**`AppraisalCycleStatus`** — `Draft` → `Open` → `Closed`, moved only by the open and close actions (a
cycle is always created as a Draft, whatever a request says). Only a cycle that has never been opened can
be deleted; a closed cycle refuses every edit. *(`InProgress` went on 2026-09-30, performance closure
D-14: only the demo seeder ever wrote it, and the demo cycle was moved to Open.)*

**`AppraisalStatus`** (the record's own coarse lifecycle) — `Draft`, `Active`, `Governance`,
`Appealed`, `Completed`, `Closed`. Everything between opening and HR sign-off is `Active`; the
fine-grained step is the *phase*, below. (`Open` is a legacy member this flow never produces.)

**`AppraisalPhase`** — **computed live, never stored**: `GoalSetting` → `SelfEvaluation` →
`PeerEvaluation` → `ManagerEvaluation` → `Calibration` → `HRReview` → `EmployeeReview` → `Closed`. A
step the settings profile switches off is never reported, which is why the rail at the top of every
appraisal screen drops it rather than greying it out. Calibration and HR review swap places when
`hrReviewTiming` is `BeforeCalibration`.

> **The one phase rule that surprises people.** Phase 1 is the goal-setting gate, and it reads
> *"every goal is past Draft / PendingApproval / Rejected **and there is at least one**"*. An employee
> with **no goals at all** therefore sits at `GoalSetting` for ever — which is why the 30 opened
> appraisals and Kojo Ansah's all show *Goal setting* on the rail even though Kojo has submitted his
> self-evaluation.
>
> **Changed 2026-09-29** (performance closure lane B1). The gate is now the profile's: at least
> *Minimum goals per employee*, every one approved (a draft or pending goal holds it), the set locked
> where the profile asks, and — on this profile — the kick-off and mid-year conversations held. The
> rail and the refusal both give the reason. Since the demo scenarios' re-run on 29 September Kojo
> Ansah has three approved goals and both conversations, so he reads *Peer Evaluation*; the opened
> appraisals with no goals still read *Goal setting*.
>
> **Changed 2026-09-29** (performance closure lane B2, slice B-w). **The mid-year left this gate** —
> it holds the manager's submission (chapter 29) — so goal setting is the goals and the kick-off. A
> draft goal holds it whether or not the profile needs the manager's approval; on a profile that does
> not, a goal is approved the moment the employee submits it. No demo appraisal moved.

**`GoalStatus`** — one enum spanning two concerns, which is why the manager workspace splits them:
`Draft`, `PendingApproval`, `Approved`, `Rejected`, `Locked` are the **approval lifecycle**;
`InProgress`, `AtRisk`, `OnTrack`, `Completed` describe **execution**. A goal in an execution state has
already passed approval.

> **Changed 2026-09-29** (performance closure lane L-a, decision D-29). **A lock is no longer a
> status.** Locking sets the goal's *Locked* flag and date and leaves its status alone: it freezes
> what the goal is — title, measure, target, weight, owner — and **not its year**, so progress
> entries, check-ins and interim reviews keep moving it. The `Locked` status used to be written by the
> lock and read everywhere as "finished"; nothing writes it now, and a goal still in it reads as
> approved and locked.

**`GoalPriority`** — `Low`, `Medium`, `High`, `Critical`.
**`GoalPeriod`** — `FullCycle`, `Q1`, `Q2`, `H1`, `Q3`, `Q4`, `H2`.
**`MeasurementType`** — `NumericAbsolute`, `PercentageTarget`, `Boolean`, `Range`.
**`AppraisalSectionKind`** — `Fixed`, `EmployeeGoals`. What fills a template section: its own items, or
each employee's locked goals.
**`CriterionScoringMethod`** — `Measured`, `Rated`. How one row of the form is scored: an actual against
a target, or a score on the grade bands.

> **Changed 2026-09-29** (performance closure lane L-b, decisions D-15 and D-16). **A template can have
> a goals section.** It holds no items of its own: on each appraisal it is filled with one row per goal
> in the employee's **locked** set — the goal's title, its own target and unit, and its weight rescaled
> so that the set adds up to 100 within the section. A goal with a numeric target is **measured**
> (its actual against the target, as a KPI item is); a goal without one is **rated** on the tenant's
> overall grade scale. The year-end section takes the goals scoped to the full cycle, H2 or Q4; a goal
> scoped to an earlier quarter or half (Q1, Q2, H1, Q3) is appraised at that period's interim review
> and is not counted again at year end. **No template on the demo database has a goals section**, so
> none of this shows in the demo. Since lane L-c the screens carry it: the template editor asks what
> fills each section (chapter 8), the manager locks a set from the team desk (chapter 19), and the
> forms, calibration and the HR review show each goal row with a **Goal** badge (chapters 27–31).
**`TeamGovernanceStatus`** — `NotStarted`, `InProgress`, `AwaitingApproval`, `InvalidWeight`,
`StructurallyComplete`. Derived from counts and weights only — never from anything a user typed.

**`ConversationType`** — `KickOff`, `QuarterlyQ1`, `MidYear`, `QuarterlyQ3`, `QuarterlyQ4`,
`FinalReview`, `PIPDiscussion`, `AdHocMeeting`.
**`ReviewEventType`** — `GoalSetting`, `QuarterlyQ1`, `QuarterlyQ2`, `MidYearReview`, `QuarterlyQ3`,
`QuarterlyQ4`, `YearEndReview`.
**`CheckInType`** — `OneOnOne`, `AdHocFeedback`, `GoalProgressUpdate`, `CoachingSession`,
`MidYearCheckIn`.
**`CalibrationStatus`** — `Pending` → `InProgress` → `Completed` (→ `Cancelled`). *Committing* is a
separate act **after** `Completed`.
**`AppraisalAppealStatus`** — `Submitted` → `UnderReview` → `Remanded` / `Upheld` / `Rejected`.
Remanded is **not** a verdict.
**`PipStatus`** — `Draft` → `PendingApproval` → `Active` → `InProgress` → `Completed` /
`Unsuccessful` / `Cancelled`.
**`PipOutcome`** — `PerformanceImproved`, `Extended`, `Demotion`, `Termination`, `Transferred`.
`Extended` is the one that does **not** close the plan.
**`RecommendationType`** — `MeritIncrease`, `Bonus`, `Promotion`, `TrainingNomination`,
`SuccessionNomination`, `PerformanceImprovementPlan`, `ConfirmProbation`, `ExtendProbation`,
`ContractRenewal`, `Demotion`, `Termination`, `Recognition`.
**`RecommendationStatus`** — `Proposed` → `Approved` → `Actioned` (or `Rejected` / `Dismissed`).
**`Approved` but not `Actioned` is a failure, not a success** — see chapter 34.

**The grades on this database** (`AppraisalGradeDefinitions`):

| Grade | Overall band | Maps to rating |
|---|---|---|
| Unsatisfactory | 0 – 40 | Unsatisfactory |
| Below Expectations | 41 – 55 | Below Expectations |
| Meets Expectations | 56 – 75 | Meets Expectations |
| Exceeds Expectations | 76 – 90 | Exceeds Expectations |
| Outstanding | 91 – 100 | Outstanding |

### 1.5 The arithmetic — worked on Efua Seidu's real numbers

This is the part to be word-perfect on, because it is the question every HR director asks.

**A score is a three-level weighted mean.** Each level is normalised to 0–100 before the next uses it.

```
  LEVEL 1  ITEM        a criterion's achievement, 0–1
                         competency → numericScore / 100
                         KPI        → achievement of target, capped at 100%
  LEVEL 2  EVALUATOR   weighted mean of the items THAT EVALUATOR SCORED, 0–100
                         Σ(item × share) / Σ(share)
  LEVEL 3  OVERALL     weighted mean across evaluator ROLES, 0–100
                         Σ(roleScore × roleWeight) / Σ(roleWeight)
                         with all peers collapsed to ONE role score first

  share  =  sectionWeight / 100  ×  itemWeight       (sums to 100 across the form)
```

**Two invariants.** The evaluator weight is applied **exactly once**, at level 3. And peers aggregate
to **one voice** — three peers do not out-vote one manager.

Now the real figures. The form is KPIs 60% (two items at 50/50) and Competencies 40% (two scored
items at 50/50 plus a weight-0 question), so the four shares are **30, 30, 20, 20**.

**Efua's self-evaluation** — she claimed 110 and 96 against KPI targets of 100, and 90 and 88 on the
competencies:

| Item | Her entry | Achievement | Share | Contribution |
|---|---|---|---|---|
| Sales Target Achievement | actual 110 (target 100) | 100% *(capped — no extra credit)* | 30 | 30.0 |
| Project Delivery Timeliness | actual 96 (target 100) | 96% | 30 | 28.8 |
| Communication | 90 | 90% | 20 | 18.0 |
| Teamwork | 88 | 88% | 20 | 17.6 |
| | | | **100** | **94.4** |

**Self score = 94.40.** That is the number on her record.

**Her manager (Kwasi Danquah) scored** 104, 92, 84, 82 → 30.0 + 27.6 + 16.8 + 16.4 = **90.80.**

**Her two peers** each scored only the competencies (this cycle does not let peers score KPIs). Kojo
Ansah gave 84 / 84 → **84.00**. Cynthia Sarpong gave 80 / 80 → **80.00**. Collapsed to one peer voice:
**82.00**.

**The overall**, at the profile's weights of 0.1 / 0.2 / 0.7:

```
  (94.40 × 0.1) + (82.00 × 0.2) + (90.80 × 0.7)
=    9.44       +    16.40      +    63.56        =  89.40   →  Exceeds Expectations (76–90)
```

**That is the arithmetic without the panel.** The Operations panel restated *Project Delivery
Timeliness* from 92 to 88 (Rule 2), and the restatement counts: her manager's line becomes
88% × 30 = 26.4, his score **89.60**, and the overall

```
  (94.40 × 0.1) + (82.00 × 0.2) + (89.60 × 0.7)
=    9.44       +    16.40      +    62.72        =  88.56   →  Exceeds Expectations (76–90)
```

**88.56 is what her appraisal reads** (since the 2026-09-29 rebuild — before it, the pre-fix 89.40).
You can put both calculations on the screen: the first is what the people scored, the second what
the panel decided.

Four more facts worth carrying:

* **Only submitted evaluations count.** A self-evaluation or peer review saved as a draft is never a
  leg of the overall; nothing scored at all gives *no* score, not zero. (Before 2026-09-29 a saved
  draft moved the final score.)

* **Hitting target is full marks.** A KPI actual of 110 against a target of 100 scores 100%, not 110%.
  There is no extra credit, and an actual beyond the item's ceiling counts as attaining the ceiling.
  Where a floor is set, achievement is measured across the min→target band rather than from zero.
* **An evaluator is marked out of what they scored**, not out of the whole form. That is what stops
  peers being silently capped at the competency share.
* **The test that catches every scoring bug**: have every evaluator score the *same* value. A weighted
  mean of identical values is that value, whatever the weights are. Run it with **three** peers, not
  one — with one peer the denominator lands on exactly 1.0 and a per-record aggregation bug hides.

### 1.6 What the settings profile decides

Forty switches, grouped the way the editor groups them. The values below are the ones on
*Standard Annual Appraisal*, which is the profile every appraisal on this database runs under.

> ### ⚠ Fourteen of these fifty fields do not enforce what they say
>
> A field-by-field audit — **[`HR-APPRAISAL-SETTINGS-AUDIT.md`](HR-APPRAISAL-SETTINGS-AUDIT.md)**,
> every claim carrying a `file:line` — found that of the 50 configurable fields on this profile,
> **36 are genuinely enforced, 9 are advisory, 2 are hidden by the browser rather than withheld by the
> server, and 3 have no reader anywhere in the codebase.**
>
> | Do not claim these work | Why |
> |---|---|
> | *Managers see peer scores* · *Allow acknowledgment before the final conversation* · *Require a development plan update* | **Nothing reads them.** Three complete ghosts |
> | *Managers see the self-score* · *Employees see the score breakdown* | **Client-side only** — the React page hides the value; the API still sends it |
> | *Goals need manager approval* · *Kick-off / mid-year / final conversation required* · *Minimum goals per employee* | Read **only** by the analytics dashboard and the deadline-advance path. Nothing refuses anything |
> | *Allow appeals* · *Appeal window (days)* | The appeal submit checks neither |
> | *Peer window opens* (`AfterSelfEval`) | Changes the notification wording; does **not** stop a peer submitting early |
> | *The manager's score is authoritative* | Affects one list's **sort order**. No effect on scoring |
>
> Everything else on this table is real. On stage, demonstrate the weights, the calibration and HR-review
> gates, the acknowledgment branch, the goal ceiling, check-ins, the private journal, the interim-review
> gates and the appeal re-evaluation deadline — all of those refuse something. Avoid the fourteen above.

> **Changed 2026-09-29** (performance closure lane B1). **Nine of the fourteen are enforced now**:
> *Goals need manager approval*, *Minimum goals per employee*, the three conversation switches,
> *Allow acknowledgment before the final conversation*, both appeal switches and *Peer window opens*.
> Every write — the self, peer and manager submissions, the calibration commit, HR's sign-off, the
> acknowledgment, the appeal and HR's advance — is held to the step the appraisal is at, and a
> refusal names that step, the same step the phase rail and the HR dashboard show. Still not to be
> claimed: *Managers see peer scores*, *Managers see the self-score* and *Employees see the score
> breakdown* (lane B2). *The manager's score is authoritative* and *Require a development plan update*
> are gone — the closure's first migration removed them.

> **Changed 2026-09-29** (performance closure lane B2, slice B-v). **The last three are enforced now**,
> by the server, on every read of an evaluation (one rule, `AppraisalVisibility`). *Managers see the
> self-score* and *Managers see peer scores* off: the line manager reads the employee's and the peers'
> entries only once they have submitted their own evaluation — on the evaluation form, the submitted
> self-evaluation, the HR review, the peer feedback and the goal assessments. *Employees see the score
> breakdown* off: once the outcome is released the employee reads the overall, the grade and the
> manager's narrative, not the criteria — on the HR review, the goal assessments and the three appeal
> pages. And whatever the switches, nobody but its author reads a self or peer draft, and the employee
> reads no manager score before HR's sign-off (the appeal page and the goal assessments used to show
> them). Every switch on this profile now does what it says.

> **Changed 2026-09-29** (performance closure lane B2, slice B-w). *Goals need manager approval* off:
> a submitted goal is agreed on the spot and needs no line manager. On or off, an unsubmitted draft
> holds goal setting. The **mid-year conversation** holds the manager's submission, not the employee's
> — the kick-off still holds the self-evaluation, the final one the acknowledgment. *Peers may score
> KPIs* off: a peer's KPI score is refused when saved, not only at the submit. The soft-skill switch is
> relabelled for what it does. The profile refuses a minimum above its maximum and risk bands out of
> order, and one profile is the tenant's **default** (chapter 4). A criterion can require **evidence**
> (chapter 5).

| Group | Setting | Here |
|---|---|---|
| **Self-evaluation** | Require a self-evaluation | **On** |
| | Weight | **0.10** |
| | Employees must score every behavioural criterion before submitting *(was "may rate their own soft skills")* | Off |
| **Peer review** | Require peer reviews | **On** |
| | Weight | **0.20** |
| | Who nominates | **The employee** |
| | Minimum / maximum peers | **2 / 4** |
| | Peer window opens | With the self-evaluation |
| | Peer reviews are anonymous | **On** *(to the appraisee — never to the manager)* |
| | Peers may score KPIs | **Off** |
| **Manager** | Require a manager evaluation | **On** |
| | Weight | **0.70** |
| | ~~The manager's score is authoritative~~ | *removed (closure batch 1)* |
| **Visibility** | Managers see the self-score | **On** |
| | Managers see peer scores | On |
| | Employees see the score breakdown | **On** |
| **Calibration & HR** | Require a calibration session | **On** ← the gate |
| | Require an HR review | **On** |
| | HR review runs | **After calibration** |
| | HR may change scores | **Off** ← why the appeal screen hides its score fields |
| **Acknowledgment** | Require the employee to acknowledge | **On** |
| | Employees may record a written response | **On** |
| | Allow acknowledgment before the final conversation | **Off** |
| **Appeals** | Allow appeals | **On** |
| | Appeal window / re-evaluation window | **7 / 5 days** *(both enforced — the appeal window since lane B1, counted from the acknowledgment; Rule 9)* |
| **Goals** | Require goal setting | **On** |
| | Goals need manager approval | **On** |
| | Minimum / maximum goals | **3 / 6** |
| **Check-ins** | Enable check-ins | **On** |
| | Enable the private journal | **On** |
| **Conversations** | Kick-off / mid-year / final required | **On / On / On** |
| **Interim reviews** | Frequency | **Mid-year only** |
| | Depth | **Light touch** |
| | Require a mid-year self-assessment | On |
| | Require a goal progress update at each review | On |
| | ~~Require a development plan update~~ | *removed (closure batch 1)* |
| **Deadlines** | Advance overdue appraisals on deadline | **Off** ← why the sweep reports zero |
| | High / medium / low risk within | **2 / 5 / 7 days** |
| | Manager workload threshold | 10 |
| **Outcomes** | Default HR reviewer | *(not set — reviewers are assigned by load)* |
| | Succession pool | Appraisal Nominations |

> **The weight rule that catches everyone.** Self + peer + manager must total **exactly 1.0** — but
> the API **zeroes the weight of any evaluator whose switch is off before checking**. Turn peer
> reviews off and you are not freed from the total: self and manager must then carry the whole 1.0
> between them. The live total on the editor applies the same rule, so what it shows is what the
> server will validate.

### 1.7 Where approval actually happens

Performance uses **three different approval mechanisms**, and knowing which is which stops you
promising the wrong thing.

| What | Mechanism | Who decides |
|---|---|---|
| **Employee goal** | **Bespoke** — `submit` / `approve` / `reject` / `lock` on the goal itself, and *lock goal set* over the employee's whole set for a cycle *(since closure lane L-a)*; `reject` also sends an approved goal back for changes | The employee's **direct manager**, read from their HR record. Not configurable, and not on the workflow engine. HR's audited advance past goal setting is the one other door (chapter 38). |
| **Appraisal itself** | **Bespoke** — the phase machine plus HR sign-off | The appraisee, their peers, their manager, then HR |
| **Appraisal template** | **Workflow engine** — `APPRAISAL_TEMPLATE` | HR, Manager or TenantAdmin (published definition) |
| **Improvement plan** | **Workflow engine** — `PERFORMANCE_IMPROVEMENT_PLAN` | HR, Manager or TenantAdmin |
| **Salary review proposal** | **Workflow engine** — `SALARY_REVIEW_PROPOSAL` | **Managing Director**, TenantAdmin or HR |
| **Employment action proposal** | **Workflow engine** — `EMPLOYMENT_ACTION_PROPOSAL` | **Managing Director**, TenantAdmin or HR |

All four workflow definitions are **published and active on this database** — you can demonstrate
submit/approve on any of them without preparation.

> **Why goal approval is deliberately *not* on the engine.** `GoalStatus` has two writers: the
> approval lifecycle, and execution states written every time somebody logs progress. An engine
> adapter would leave the entity's status diverging from the workflow the moment progress was logged.
> Add that the approver is always the direct manager (read from the token and the employee record, so
> it cannot be misconfigured), and a configurable definition would be strictly worse. It is the one
> place in HR where "always use the engine" does not apply, and it is a decision, not an omission.
>
> 💬 *"Goal approval is the one thing here that is not configurable, on purpose. Your goals go to your
> manager — the person the establishment says you report to — and nobody can re-route that."*

### 1.8 The cast, and what each of them can open

| Login | Person | Post | Roles | What they are for in this demo |
|---|---|---|---|---|
| `hr.head` | **Akpene Amoah** (TDC/00009) | Head of HR & Administration | HR, Manager, Employee | Every HR desk screen, every Administration setup screen, calibration, HR review, appeals, outcomes |
| `head.dev` | **Kwasi Danquah** (TDC/00006) | Head of Development | Manager, Employee | The manager's world: team appraisals, team goals, check-ins, the journal, the PIP. **Also the subject of the appraisal HR signs off.** |
| `staff` | **Efua Seidu** (TDC/00017) | Project Coordinator | Employee | The employee's world: my goals, my appraisal, my journal, the appeal |
| `md.tdc` | **Nana Nyaho** (TDC/00001) | Managing Director | Managing Director, Manager, Employee | Approves salary and employment-action proposals. **Excluded from the cycle** — his appraisal is a Board matter |
| `new.hire` | **Kojo Ansah** (TDC/00063) | Supervising Architect | Employee | A part-finished appraisal — self-evaluation in, the two peers still to write |
| `she.officer` | **Cynthia Sarpong** (TDC/00081) | Environmental Officer | Safety Officer, Employee | The appealed appraisal |
| `gm.ops` | **Kojo Fiadzo** (TDC/00003) | General Manager – Operations | Manager, Employee | A completed appraisal at the top of the tree |
| `she.manager` | **Josephine Appiah** (TDC/00071) | HSE Supervisor | SHE Manager, Employee | A peer evaluator |
| `auditor` | **Yakubu Aryee** (TDC/00004) | Chief Internal Auditor | Internal Audit, Employee | A peer evaluator; reports to the Board |

**Password for all nine: `Demo123!`**

**Which sidebar leaves each persona can see.** Eleven of the nineteen leaves under *Performance*
require `HR.Performance.Read`, which only `hr.head` holds. The other eight are open to every internal
user, because the API holds them to the **token actor** rather than to a permission.

| Leaf | Gate | `hr.head` | `head.dev` | `staff` |
|---|---|---|---|---|
| Analytics | `HR.Performance.Read` | ✅ | — | — |
| Appraisal Cycles | `HR.Performance.Read` | ✅ | — | — |
| **Team Appraisals** | *open* | ✅ | ✅ | ✅ *(empty)* |
| **Interim Reviews** | *open* | ✅ | ✅ | ✅ |
| **Conversations** | *open* | ✅ | ✅ | ✅ |
| Development Plans | `HR.Performance.Read` | ✅ | — | — |
| **Improvement Plans** | *open* | ✅ | ✅ | ✅ |
| HR Review | `HR.Performance.Read` | ✅ | — | — |
| Calibration | `HR.Performance.Read` | ✅ | — | — |
| Appeals | `HR.Performance.Read` | ✅ | — | — |
| Recommendations | `HR.Performance.Read` | ✅ | — | — |
| Proposals | `HR.Performance.Read` | ✅ | — | — |
| Company Goals | `HR.Performance.Read` | ✅ | — | — |
| **Unit Goals** | *open* | ✅ | ✅ | ✅ |
| **Employee Goals** | *open* | ✅ | ✅ | ✅ |
| **Team Goals** | *open* | ✅ | ✅ | ✅ *(no reports → empty state)* |
| Goals At Risk | `HR.Performance.Read` | ✅ | — | — |
| Deadline Enforcement | `HR.Performance.Write` | ✅ | — | — |

💬 A good line when you first switch to `head.dev`: *"Notice the menu is shorter. The manager has not
been given a cut-down copy of HR's screens — those screens are simply not his. What he has instead are
screens the system scopes to him from his own identity: his team, his goals, his approvals."*

---
## 2. Before the room fills — the prep

Twenty minutes, the morning of. Do not skip 2.2; the figures are what make you sound like you built it.

### 2.1 What the demo database already holds

This is the card. Everything below was verified on `ErpSystemDB_UAT` on 17 September 2026.

**Setup**

| | Count | What they are |
|---|---|---|
| Settings profiles | **1** | Standard Annual Appraisal |
| Appraisal templates | **1** | Standard Employee Template 2026 — Approved, active, in use |
| Appraisal criteria | **5** | Leadership, Communication, Adaptability, Problem Solving, Teamwork |
| Grade definitions | **5** | Unsatisfactory → Outstanding, with overall bands |
| KPI definitions | **5** | Revenue Growth, Project Delivery Timeliness, Sales Target Achievement, Quality Defect Rate, Customer Satisfaction Score |
| Goal library templates | **3** | layouts to approval · client correspondence standard · a professional qualification |
| Strategic goals | **1** | Operational Excellence 2026–2028 |
| Goal risk thresholds | **configured** | 14 days / 60% / 20% |

**The cycle**

| | |
|---|---|
| `APC2026` *Annual Performance Cycle 2026* | 1 Jan → 31 Dec 2026, **Open**, opened 2 Jan 2026 |
| Targets | **2** — *Managing Director's Office* (the whole tree) and *Internal Audit Department* (which reports to the Board, not to the MD) |
| Exclusions | **1** — Nana Nyaho, the Managing Director: *"appraised by the Board of Directors under his contract of engagement"* |
| Template assignments | **1** — Standard Employee Template 2026, priority 10 |
| Appraisals generated | **107** |

**Phase deadlines on the cycle**

| Phase | Opens | Due |
|---|---|---|
| Goal setting | 5 Jan 2026 | **31 Jan 2026** *(passed)* |
| Mid-year | 1 Jun 2026 | **30 Jun 2026** *(passed)* |
| Q1 / Q3 review | — | *not set* |
| Peer nomination | — | 10 Nov 2026 |
| Self-evaluation | 1 Nov 2026 | 15 Nov 2026 |
| Peer evaluation | 1 Nov 2026 | 20 Nov 2026 |
| Manager evaluation | 21 Nov 2026 | 5 Dec 2026 |
| Calibration | 6 Dec 2026 | 12 Dec 2026 |
| HR review | 13 Dec 2026 | 18 Dec 2026 |
| Acknowledgment / final conversation | — | 24 Dec 2026 |

> 💬 Worth saying when the dates come up: *"The demonstration data has been driven forward so you can
> see the whole year in one sitting — the self-evaluations you are about to read were submitted ahead
> of a window that formally opens in November. In live use the window governs; here it is deliberately
> out of the way."*

**The five appraisals that carry the story**

| Person | Login | State | Self | Peers | Manager | Final | Grade |
|---|---|---|---|---|---|---|---|
| **Efua Seidu** TDC/00017 | `staff` | **Completed**, acknowledged | 94.40 | 84 / 80 | 90.80 | **88.56** *(the panel's 88 on one criterion — Rule 2)* | Exceeds Expectations |
| **Kojo Fiadzo** TDC/00003 | `gm.ops` | **Completed**, acknowledged | 94.00 | 81.5 / 77.5 | 91.40 | **89.28** | Exceeds Expectations |
| **Cynthia Sarpong** TDC/00081 | `she.officer` | **Completed**, acknowledged, **appeal upheld** | 94.00 | 83 / 79 | 90.20 | **87.00** *(the panel's overall — Rule 2)* | Exceeds Expectations |
| **Kwasi Danquah** TDC/00006 | `head.dev` | **Governance — waiting on HR** ← *the one you finalise live* | 92.20 | 80 / 76 | 88.80 | — | — |
| **Kojo Ansah** TDC/00063 | `new.hire` | **Active**, at *Peer Evaluation* — goals, both setup conversations and the self-evaluation in; the two peers still to write *(since 2026-09-29)* | 85.60 | — | — | — | — |

Everyone else: 73 in Draft, 29 more Active (opened but untouched).

**The goal cascade**

* 1 strategic goal → 2 company goals (*Improve Customer Satisfaction by 15%*, *Reduce Operational
  Costs by 10%*) → 3 unit goals (Development, Estates, HR / Administration).
* **22 employee goals.** Twelve belong to four TDC people at three goals each, all weighted to 100.
  Ten belong to non-TDC fixtures.
* **Only Efua's three carry progress**: Community 25 Phase 2 layout **70% On track**, drawing-office
  turnaround **55% At risk**, Civil 3D certification **40% In progress**. Four progress entries
  between them.

**Around the cycle**

| | Count | Detail |
|---|---|---|
| Check-ins | **1** | *Q3 one-to-one — Community 25 progress*, head.dev with Efua, held, **3 goal updates** |
| Journal entries | **4** | 3 by head.dev about Efua (2 shared, 1 private) + 1 private note of Efua's own |
| Conversations | **14** | 5 kick-offs (Feb) + 5 mid-years (Jul) + 4 final reviews (Sept — the three finished appraisals and Kwasi's). **All completed** — see Rule 6 |
| Interim reviews | **107** | All mid-year, light-touch. 102 Pending, 5 Completed |
| Calibration sessions | **2** | Operations Directorate (panel of 4, 1 adjustment) · Finance & Administration (panel of 3, 1 adjustment). Both **Completed and committed**; 76 appraisals calibrated |
| Development plans | **3** | Efua (3 objectives, 2 feedback notes) · Cynthia (2 + 1) · Kojo Ansah (2 + 1). All **Active** |
| Appeals | **1** | Cynthia Sarpong — **Upheld** |
| Improvement plans | **1** | `PIP-2026-0001`, **Patrick Appiah** (TDC/00018), supervisor head.dev, HR owner hr.head, 31 Aug → 29 Nov, **Active**, 3 goals, 8 review meetings |
| Outcome recommendations | **7** | all **Actioned** — 3 merit increases, 3 training nominations, 1 promotion |
| Salary review proposals | **3** | all **Proposed**, all with **no figure yet** ← the live write in chapter 35 |
| Employment action proposals | **1** | a promotion, **Proposed** |
| Appraisal attachments | 3 · employee responses 5 · free-text answers 5 · goal assessments 12 |

### 2.2 The seven figures to write down

Put these on a card beside the laptop. Quoting a number without looking is the difference between
presenting and reading.

1. **107** appraisals in the cycle — 73 Draft, 30 Active, 1 with HR, 3 finished.
2. **10 / 20 / 70** — the self, peer and manager weights.
3. **88.56** — Efua's final score, and **Exceeds Expectations** is the band it falls in (76–90).
4. **2 to 4** peers, and **2** is the minimum that lets a self-evaluation be submitted.
5. **76 of 107** calibrated; **1** appraisal is sitting in HR's Ready queue.
6. **3 to 6** goals per person; Efua has **3**, weighted **40 / 30 / 30 = 100**.
7. **14 days / 60% / 20%** — the goal-risk thresholds, and the reason roughly **16** goals show as at
   risk today.

### 2.3 Two windows, four personas

Performance is a four-actor story and switching accounts on stage kills the pace. Use **two browser
windows**, one of them a private/incognito window, and keep two sessions alive:

| Window | Signed in as | Holds |
|---|---|---|
| **A — the desk** | `hr.head` | Administration setup, cycles, calibration, HR review, appeals, outcomes, analytics |
| **B — the people** | `head.dev`, then `staff`, then `md.tdc` | Team appraisals, team goals, the journal, My Goals, My Appraisal, the appeal, the MD's approval |

Window B changes hands three times. That is fine — say *"let me sign in as Efua"* out loud each time;
the switch is part of the story, not an interruption to it.

### 2.4 Pre-open every screen

Open these as tabs before the room fills, in this order, in the window shown. Every one of them is a
read.

**Window A — `hr.head`**

1. `/administration/hr/performance` — the setup hub
2. `/administration/hr/performance/settings` → open *Standard Annual Appraisal*
3. `/administration/hr/performance/templates` → open *Standard Employee Template 2026*
4. `/hr/performance/cycles/…` — the `APC2026` detail page, on the **Coverage** tab
5. `/hr/performance/company-goals`
6. `/hr/performance/at-risk`
7. `/hr/performance/calibration` → **Completed** tab
8. `/hr/performance/hr-review` → **Ready** tab
9. `/hr/performance/recommendations`
10. `/hr/performance/proposals`
11. `/hr/performance/analytics`

**Window B — `head.dev`**

12. `/hr/performance/team-appraisals`
13. `/hr/performance/team-goals`
14. `/me/performance/journal`

Leave tab 4 on **Coverage**, not Overview: the coverage table is the strongest single screen in the
first ten minutes and you want it already rendered.

### 2.5 The two decisions to take before you start

**Decision 1 — do you finalise Kwasi Danquah's appraisal live?** (chapter 31, 🔴 LIVE WRITE 10.)
It is the most satisfying moment in the workbook: a real appraisal moves from *Ready* to *Finalised*,
a score is computed in front of the room, and the employee is notified. It is also **not reversible
through the UI**. If the same database is being used for another demonstration afterwards, either
accept that the HR Review queue will then read 0 ready / 4 finalised, or rebuild between runs. My
recommendation: **do it.** A performance demonstration that never signs anything off is a tour of
forms.

**Decision 2 — do you let Efua file the appeal live?** (chapter 33, 🔴 LIVE WRITE 13.) Also
recommended, and this one *is* recoverable: you resolve it in the same sitting as `hr.head`, which
returns the appraisal to Completed. Doing both gives you the full arc — score, sign-off, contest,
adjudication — in one session.

### 2.6 Prep checklist

- [ ] API running against **`ErpSystemDB_UAT`** — not the development database
- [ ] The scanner stub is up if you intend to upload anything (chapter 29's Evidence tab). Without it
      every upload answers **422**
- [ ] Two windows open, `hr.head` in A, `head.dev` in B
- [ ] Fourteen tabs pre-opened per 2.4
- [ ] The seven figures on a card
- [ ] Rules 2, 3 and 8 re-read — those are the three that bite
- [ ] Decisions 1 and 2 taken
- [ ] You know which appraisal is which: **Efua = finished**, **Kwasi = with HR**, **Kojo Ansah = half done**

---

# PART I — SETUP

*Administration → HR → Performance. Fifteen minutes. This is the part that makes the rest credible: an
audience that has seen the instruments believes the measurements.*

> **A note on sequencing.** You can skip Part I entirely and start at chapter 12 if the room is short
> of time — the short path in Appendix D does exactly that. But if your audience includes anyone who
> will have to *operate* this, Part I is the half they will remember, because it is the half that
> shows the system is configurable rather than opinionated.

---

## 3. `/administration/hr/performance` — the setup hub

### 📍 Where you are
**Route:** `/administration/hr/performance`
**Sidebar:** Administration → HR → Performance
**As:** `hr.head` · **2 minutes**

### 📖 What it is
Eight cards, one per instrument, in the order they are actually set up: the **policy** first, then the
**vocabulary** an appraisal scores in, then the **forms**, then the **goal cascade** pieces. Cycles are
not here — a cycle is operational, not setup, and lives under `/hr/performance/cycles`.

### 👁 On the page
Page header *"Performance Setup — the policy, forms, measures and thresholds that appraisals and the
goal cascade are built on"*, then eight navigation cards:

| Card | Goes to | One-line description on the card |
|---|---|---|
| **Appraisal Settings** | `/settings` | Weighting, rounding and the rules a cycle is scored under |
| **Appraisal Templates** | `/templates` | The form each population is appraised on |
| **Appraisal Criteria** | `/criteria` | The named behaviours a form can score |
| **Grade Definitions** | `/grade-definitions` | The grades items are scored into |
| **Strategic Goals** | `/strategic-goals` | Multi-year company intent |
| **Goal Library** | `/goal-library` | Reusable goal wording |
| **KPI Definitions** | `/kpi-definitions` | How goals are measured |
| **Goal Risk Thresholds** | `/goal-risk-settings` | When a goal is flagged at risk |

### ▶ Walk it

1. Open the tab. Do not click anything yet.
2. Read the eight card titles aloud, slowly, in order.

💬 *"Before an appraisal can happen, eight things have to exist. The policy that says who evaluates and
at what weight. The vocabulary — the behaviours you score, the grades you score them into, the
indicators you measure. The forms themselves. And underneath, the goal machinery: the multi-year
intent, the reusable wording, and the rule that decides when a goal is in trouble. Everything you see
for the rest of this session is assembled out of these eight."*

### ⚙ Behind the page
A static card grid defined once in `config/hr-setup-nav.ts` and rendered by three surfaces — this hub,
the sidebar node, and the HR module page under `/settings/modules/human-resources` — so they cannot
drift apart.

### ⚠ Known gaps
**P-1.** The whole `/administration/hr` tree is gated on the `admin.hr` permission, which `hr.head`
holds and no other demo persona does. If you navigate here in window B you get the access-denied page.

---
## 4. `/administration/hr/performance/settings` — the policy profiles

### 📍 Where you are
**Route:** `/administration/hr/performance/settings` and `…/settings/[id]`
**Sidebar:** Administration → HR → Performance → Appraisal Settings
**As:** `hr.head` · **5 minutes** — the longest setup chapter, and worth every second

### 📖 What it is
A **named policy profile** that a cycle runs under. It decides who evaluates, how their scores
combine, what has to happen before a result is final, and the thresholds the dashboards read. A cycle
names exactly one. Most organisations end up with two or three — a standard annual policy, a lighter
probation one, perhaps a senior-management variant.

One profile is the tenant's **default** — a flag HR moves with **Make default**, at most one per
tenant. *(Since closure B6, 2026-09-29. `GET /default` used to hand back the most recently created
profile, so any new or test profile silently became the default, and the list marked the newest.)*

### 👁 On the list page
Header, a **New profile** button, and a table — the default first, then the newest:

| Column | What it shows | On this database |
|---|---|---|
| **Profile** | Name, with a *Default* badge on the tenant's default | Standard Annual Appraisal *(Default)* |
| **Evaluation weights** | The three weights as a summary | Self 10% · Peer 20% · Manager 70% |
| **Sign-off** | Which of calibration / HR review / acknowledgment are required | all three |
| **Appeals** | On/off and the window | On, 7 days |
| **Interim reviews** | Frequency and depth | Mid-year only, light touch |
| *(actions)* | **Make default** on every other row, then **Edit** | — |

Empty-state copy, if you ever see it: *"A cycle cannot be created without one — start with a profile
describing your standard annual appraisal."*

### 👁 On the editor (`/settings/[id]`)
A header carrying the **profile name** field and a live **Evaluation weights total** badge, then four
tabs. The full inventory is in §1.6; what matters on screen is the grouping:

| Tab | Cards inside |
|---|---|
| **Evaluation** | Self-evaluation · Peer review · Manager evaluation · Score visibility |
| **Sign-off & appeals** | Calibration and HR review · Employee acknowledgment · Appeals |
| **Goals & conversations** | Goal setting · Check-ins and journals · Conversations · Interim reviews |
| **Operations** | Deadlines · Outcomes |

Bottom of the page: **Save**, and a **Delete** with the confirmation *"Cycles already using it will
block the delete."*

### ▶ Walk it

1. Open **Appraisal Settings**. One row: *Standard Annual Appraisal*.
2. Click it. Land on the **Evaluation** tab.
3. Point at the badge beside the profile name: **Evaluation weights total — 1.00**.

   💬 *"Three people score this appraisal and the system will not let their weights come to anything
   other than one. Ten per cent the employee, twenty per cent their peers, seventy per cent their
   manager. That is not a convention — the API rejects the save."*

4. Toggle **Require peer reviews** off. Watch the total badge: it stays **1.00**, because the peer
   weight is now excluded from the sum — and the peer card collapses away.

   💬 *"And here is the thing that catches people. Turning peers off does not give you twenty points
   to play with. The API zeroes a disabled evaluator's weight before it checks the total, so self and
   manager now have to carry the whole thing between them. The number on screen is the number the
   server will validate — not a client-side guess."*

5. **Toggle peer reviews back ON.** Do not save. *(If you saved by accident, set the peer weight back
   to 0.2 and save again — see Appendix E.)*
6. Move to **Sign-off & appeals**. Read three switches aloud: *Require a calibration session*
   (**on** — "this is the gate"), *HR may change scores* (**off**), *Allow acknowledgment before the
   final conversation* (**off**).

   💬 *"'HR may change scores' is off, and that is a policy choice with teeth. It means that when an
   employee appeals, HR cannot simply rewrite the number — it has to send the appraisal back to the
   manager to re-evaluate. You will see that screen refuse to offer the fields, later on."*

7. Move to **Goals & conversations**. Point at **Minimum / maximum goals — 3 and 6**, and at the three
   conversation switches.
8. Move to **Operations**. Point at **Advance overdue appraisals on deadline — off**, and at the risk
   bands 2 / 5 / 7 days.

   💬 *"Auto-advance is off on this profile, which means HR's deadline sweep will examine every
   overdue appraisal and change none of them. That is deliberate: pushing somebody's appraisal past a
   step they never completed is an intervention, and it should be a decision, not a default."*

9. Leave without saving.

### ⚙ Behind the page

| Element | Endpoint | Service → table |
|---|---|---|
| The list | `GET api/AppraisalSettings` | `AppraisalSettingsService` → `AppraisalSettings` |
| The editor | `GET api/AppraisalSettings/{id}` | same |
| Save | `PUT api/AppraisalSettings/{id}` — **`HR.Performance.Write`** | validates the weight total = 1.0 *after* zeroing disabled evaluators; *since B6*, also that no minimum (peers, goals) is above its maximum and the risk bands run high ≤ medium ≤ low — each refusal a 400 that says which |
| Make default | `POST api/AppraisalSettings/{id}/make-default` — **`HR.Performance.Write`** | clears the previous default and flags this one in one transaction (a filtered unique index allows one per tenant) |
| The default | `GET api/AppraisalSettings/default` | the flagged profile, or **404** when none is flagged |
| Delete | `DELETE api/AppraisalSettings/{id}` — **`HR.Performance.Admin`** | 400 while any cycle uses it, **and for the default** (make another the default first); **403 for `hr.head`** |
| The total badge | client-side `evaluationWeightTotal()` | reproduces the server's rule exactly |
| Validate weights | `GET api/AppraisalSettings/{id}/validate-weights` | returns `{ isValid, message }` |

### ⚠ Known gaps
**P-2.** ~~There is no "default profile" concept. `GET /default` returns the newest row, so creating a
new profile silently becomes the default for anything that asks for one.~~ **Fixed 2026-09-29**
(closure B6): a flag HR sets with **Make default**; the seeder flags *Standard Annual Appraisal* on a
rebuild, and UAT's was flagged through the same door.
**P-3.** ~~`appealWindowDays` and `appealReevaluationWindowDays` are stored and displayed but **not
enforced** on the appeal path (Rule 9).~~ **Fixed 2026-09-29** — the re-evaluation window was already
enforced; the appeal window is since closure lane B1, counted from the acknowledgment.
**P-4.** `Delete` renders for `hr.head` and answers 403 — it needs `HR.Performance.Admin`.
**P-62.** **Three switches on this form are ghosts** — *Managers see peer scores*, *Allow
acknowledgment before the final conversation* and *Require a development plan update* have **no reader
anywhere** in the backend, the frontend or the tests. *One left:* the acknowledgment switch is
enforced since lane B1 and the development-plan switch was removed (batch 1); *Managers see peer
scores* is lane B2's. **Fixed 2026-09-29** (closure lane B2, slice B-v): *Managers see peer scores*
withholds the peers' scores and comments from the manager until they have submitted.
**P-63.** ~~**Two "visibility controls" are client-side only.** *Managers see the self-score* and
*Employees see the score breakdown* are honoured by the React page; the API returns the values either
way. Their neighbour *Peer reviews are anonymous* **is** enforced server-side, so on one card two
controls are real and two are presentation.~~ **Fixed 2026-09-29** (closure lane B2, slice B-v): both
are withheld by the server on every read that carried them — five for the manager, five for the
employee.
**P-64.** ~~**Four governance switches are honoured by the analytics pipeline but not by the enforcing
one** — *Goals need manager approval*, and the three conversation requirements. See P-67.~~ **Fixed
2026-09-29** (closure lane B1): all four refuse — the goal and setup-conversation switches hold the
self-evaluation, the final conversation holds the acknowledgment and completion.
**P-65.** ~~**The appeal switches do not gate the appeal.** `SubmitAppealAsync` checks neither *Allow
appeals* nor the window; both are read only into a `canFileAppeal` DTO flag the frontend never reads.~~
**Fixed server-side 2026-09-29** (closure lane B1): the submit refuses on either, by the rule the appeal
page and *My Appraisals* also read. The portal's appeal button still keys off the status alone (lane I).
**P-66.** ~~***Peer window opens* does not gate the peer form.** In `AfterSelfEval` the peer is *told*
to wait — `IsEditableByRole` is a read-only query nothing calls before a write.~~ **Fixed 2026-09-29**
(closure lane B1): the peer's draft and submission are refused before the self-evaluation in
`AfterSelfEval`.
**P-67.** ~~**The module has two pipeline resolvers with different rules.** `GetCurrentPhase` enforces;
`AppraisalSubStatusResolver` — used by the dashboard and the deadline advance — has five extra gates.
The dashboard therefore reports a stricter pipeline than the system enforces.~~ **Fixed 2026-09-29**
(closure lane B1): one gate evaluator, `AppraisalGates`, read by every write, the phase rail, the
dashboard and the advance.
**P-68.** ~~***The manager's score is authoritative* changes only a sort order.** It is copied to
`EvaluatorEvaluation.IsAuthoritative`, whose sole reader is an `OrderByDescending`.~~ **Removed**
(closure batch 1).
**P-69.** ~~***Employees may rate their own soft skills* is mislabelled.** It does not decide whether the
employee may score behavioural criteria — they always can, and the form always shows them. It decides
whether the **submit is refused** unless every competency is scored. "May rate" is really "must rate".~~
**Fixed 2026-09-29** (closure lane B2, slice B-w): the switch reads *Employees must score every
behavioural criterion before submitting*, and the self-evaluation page says so to the employee.
**P-70.** ~~***Minimum goals per employee* is not enforced**, while its sibling *Maximum* is.~~ The minimum
computes a `meetsMinGoalCount` flag the frontend never reads. *Enforced 2026-09-29* (closure lane B1):
the self-evaluation is refused below the minimum. Showing the flag on the manager's desk is lane B2's.
**Fixed 2026-09-29** (slice B-w): the team desk reads *Below minimum* or *Above maximum* (chapter 19).

The full audit, with a `file:line` for every claim and a prioritised fix list, is in
**[`HR-APPRAISAL-SETTINGS-AUDIT.md`](HR-APPRAISAL-SETTINGS-AUDIT.md)**.

---

## 5. `/administration/hr/performance/criteria` — the behaviours you score

### 📍 Where you are
**Route:** `/administration/hr/performance/criteria` · Administration → HR → Performance → Appraisal Criteria
**As:** `hr.head` · **1 minute**

### 📖 What it is
The **qualitative** half of an appraisal form. A template item scores either a *criterion* — "Judgement",
"Teamwork" — or a *KPI*. The split is deliberate: a KPI carries a measurement type, a unit and a
target; a criterion is judged against the grade bands the template item defines, and nothing else.

### 👁 On the page
A single-table register with inline create/edit dialogs.

| Column | On this database |
|---|---|
| **Code** | LEADERSHIP · COMMUNICAT · ADAPTABILI · PROBLEMSOL · TEAMWORK |
| **Criterion** | Leadership · Communication · Adaptability · Problem Solving · Teamwork |
| **Description** | *"Demonstrates strong … skills"* |
| **Evidence** | *Required* or — · all — |
| **Status** | all Active |

The dialog carries **Code**, **Criterion**, **Description**, a **Require evidence** switch and an
**Active** switch whose help text is worth reading aloud: *"Inactive criteria stay on existing templates
but cannot be added to new items."*

> **Changed 2026-09-29** (performance closure lane B2, slice B-w). *Require evidence* is new on the
> screen — the flag was always on the criterion, but no screen or API could set it, and no save kept
> the evidence links the forms sent. On, a **score** on this criterion needs an evidence link before
> the employee, the manager or a peer can submit; the refusal names the criterion, and the scoring
> form marks the link *required for this item*. An unscored criterion needs none. No demo criterion
> requires it.

### ▶ Walk it
1. Open it. Five rows.
2. Point at the Active switch description.

   💬 *"Retiring a behaviour is not deleting it. Anything already scored against 'Adaptability' keeps
   its score and its history; the criterion simply stops being offered on new forms. That is the rule
   everywhere in this module — the past is not editable."*

### ⚙ Behind the page
`api/AppraisalCompetency` (singular, and the field is `criteriaName` — the two names are used
interchangeably in the ported code and both are kept). Writes need `HR.Performance.Write`. The rule
behind *Require evidence* is `AppraisalEvidence`, asked by the three submissions.

### ⚠ Known gaps
**P-5.** Codes are auto-truncated to 10 characters, which is why *Communication* reads `COMMUNICAT`
and *Adaptability* reads `ADAPTABILI`. Cosmetic, but visible.

---

## 6. `/administration/hr/performance/grade-definitions` — the grades

### 📍 Where you are
**Route:** `/administration/hr/performance/grade-definitions` · Administration → HR → Performance → Grade Definitions
**As:** `hr.head` · **2 minutes**

### 📖 What it is
The vocabulary a score is expressed in. A grade is used at **two** levels, and the difference is the
interesting part:

* **Per item** — every template item maps score bands onto grades, and those bands live **on the item**,
  not here. The same grade can mean 80–100 on one item and 70–89 on another.
* **Overall** — the optional band on this screen maps a whole appraisal's score onto a rating. Fill it
  in only for the grades that describe an overall outcome.

### 👁 On the page

| Grade | Overall band | Rating | Status |
|---|---|---|---|
| Unsatisfactory | 0 – 40 | Unsatisfactory | Active |
| Below Expectations | 41 – 55 | Below Expectations | Active |
| Meets Expectations | 56 – 75 | Meets Expectations | Active |
| Exceeds Expectations | 76 – 90 | Exceeds Expectations | Active |
| Outstanding | 91 – 100 | Outstanding | Active |

A grade with no band shows *"Item-level only"* in the band column. The dialog carries **Grade**,
**Description**, **Overall min**, **Overall max**, **Maps to rating** and **Active**.

### ▶ Walk it
1. Open it. Point at the bands.

   💬 *"Efua's final score was 89.4. Eighty-nine point four falls in this band — Exceeds Expectations,
   76 to 90 — and that is why her appraisal says what it says. The band is data, not code. If the
   Corporation decides next year that 'Exceeds' starts at 80, this is the one row you change."*

2. Point at the *Item-level only* concept.

   💬 *"And the same five grades do a second job inside the form: each scored line has its own bands,
   so 'Exceeds' on a safety criterion can demand a higher number than 'Exceeds' on a drafting one."*

### ⚙ Behind the page
`api/AppraisalGradeDefinitions`. `OverallMinScore`/`OverallMaxScore` on `AppraisalGradeDefinitions`;
the per-item bands live on `TemplateItemGradeRanges`, edited from the template editor.

### ⚠ Known gaps
**P-6.** ~~The overall bands are **not validated against each other**. Two overlapping bands resolve
to whichever is found first. Keep them tidy by hand.~~ **Fixed 2026-09-29** (closure A2): saving a
band refuses an overlap with another active band, a half band, a band outside 0–100 or inverted,
and a band with no mapped rating. One resolver grades everywhere; a score between two published
ranges (90.5 between 76–90 and 91–100) takes the lower band.

---

## 7. `/administration/hr/performance/kpi-definitions` — how things are measured

### 📍 Where you are
**Route:** `/administration/hr/performance/kpi-definitions` · Administration → HR → Performance → KPI Definitions
**As:** `hr.head` · **1 minute**

### 📖 What it is
The reusable "how is this measured" half of a goal. An employee goal that names a KPI inherits its
measurement type and unit, so the same indicator is scored the same way wherever it appears.

**Definitions carry no target.** The target is per goal, because the same KPI means different numbers
for different people.

### 👁 On the page

| KPI | Measurement | Unit | Tolerance |
|---|---|---|---|
| Revenue Growth | Percentage of target | % | 5% |
| Project Delivery Timeliness | Percentage of target | % | 5% |
| Sales Target Achievement | Percentage of target | % | 5% |
| Quality Defect Rate | Percentage of target | % | 5% |
| Customer Satisfaction Score | Numeric (absolute) | pts | 5% |

Dialog: **KPI name**, **Description**, **Measurement type**, **Unit**, **Tolerance (%)** — *"Blank for an
exact target"* — and **Active**.

### ▶ Walk it
1. Open it.

   💬 *"This is the difference between a target and a measure. 'Sales Target Achievement' is a measure —
   percentage of target, in per cent, with five points of tolerance. What the target actually is gets
   set per person, per goal, because a hundred per cent means a different number for a regional officer
   than for a director."*

### ⚙ Behind the page
`api/KpiDefinitions` → `KpiDefinitions`. A goal naming one copies `MeasurementType` and `Unit`; the
delete is a soft delete and is refused where usage is detected.

---

## 8. `/administration/hr/performance/templates` — the forms

### 📍 Where you are
**Route:** `/administration/hr/performance/templates` and `…/templates/[id]`
**Sidebar:** Administration → HR → Performance → Appraisal Templates
**As:** `hr.head` · **5 minutes** — the second-best setup screen after the settings profile

### 📖 What it is
The form an appraisal is scored on: **weighted sections holding weighted items**, where each item is a
criterion, a KPI, or a free-text question.

**Scope is what decides who gets which form**, most specific first:

```
   position   beats   organisation unit   beats   organisation level   beats   global
```

A cycle assigns the templates it may draw on, and the cycle's **Coverage** tab is where you see the
result of that resolution *before* anything is generated. Where two templates are equally specific,
**priority** breaks the tie.

### 👁 On the list

| Column | On this database |
|---|---|
| **Template** | Standard Employee Template 2026 |
| **Scope** | Global |
| **Structure** | 2 sections · 5 items |
| **Approval** | Approved |
| **Active** | Yes |
| **In use** | *Assigned to a cycle* |

Row actions: **Edit**, **Copy…**, **Delete** *(Admin — 403 for `hr.head`)*.

### 👁 On the detail page
Header: the template name, an **approval status** badge, an **active** badge, an
**Activate / Deactivate** button, and the workflow **Submit / Approve / Reject / Recall** actions.

A prominent amber banner, because the template is assigned to a running cycle:

> **This template is assigned to a cycle** — *Structural edits are refused while a cycle is Open or
> InProgress, so the form an appraisal was scored on cannot change underneath it. Copy the template
> from the list page and change the copy.*

Four tabs: **Structure**, **Details** (scope + approval dates), **Cycles (1)**, **Workflow**.

**Inside Structure** — the editor shows, live:

* A **section weights** total. Here: KPIs **60** + Core Competencies **40** = **100** ✅
* Per section, an **item weights** total. KPIs 50 + 50 = 100 ✅ · Competencies 50 + 50 + 0 = 100 ✅
* Per item: **Item · Type · Target · Weight · Grade bands**

| Section (weight) | Item | Type | Target | Weight | Bands |
|---|---|---|---|---|---|
| Key Performance Indicators (60) | Sales Target Achievement | KPI | 100 (0–150) | 50 | 5 |
| | Project Delivery Timeliness | KPI | 100 (0–120) | 50 | 5 |
| Core Competencies (40) | Communication | Criterion | — | 50 | 5 |
| | Teamwork | Criterion | — | 50 | 5 |
| | *"What did you contribute this year that you are most proud of, and what support do you need from the Corporation next year?"* | Question | — | **0** | *Not scored* |

### ▶ Walk it

1. Open **Appraisal Templates**, then the one template.
2. Read the amber banner out loud.

   💬 *"This form is locked, and the reason is the whole point of the module. A hundred and seven
   appraisals have been scored on it. If somebody could reweight a section now, every score already
   recorded would silently mean something different. So the system freezes the form the moment a cycle
   is running — and gives you a copy button instead."*

3. Move to **Structure**. Point at the three live totals.

   💬 *"Three rules decide whether a form can be used at all, and the editor shows all three while you
   build rather than refusing at the end: sections total a hundred, items total a hundred within each
   section, and every scored line has grade bands. There is a fourth the server enforces — you cannot
   put the same criterion on a form twice, anywhere on it."*

4. Point at the free-text question: weight **0**, and *Not scored* where the bands would be.

   💬 *"And that last line is the one people ask for: a question with no score. It is weighted zero on
   purpose — the scored lines already total a hundred, so a weighted question would change every number
   on the form. The employee answers it in their self-evaluation, and it travels with the appraisal as
   context."*

5. Go back to the list, click **Copy…** on the row, and **cancel** the dialog without confirming. Just
   show it exists.

   💬 *"And when the form does need to change mid-year, that is the answer: copy it, change the copy,
   point next year's cycle at the copy. The scored history stays intact."*

### ⚙ Behind the page

| Element | Endpoint | Note |
|---|---|---|
| List | `GET api/AppraisalTemplates/summaries` | projection with counts and the in-use flag |
| Structure | `GET …/{id}/sections`, `GET …/sections/{id}/items` | one items query per section — the section read does not carry `gradeRangeCount`, which is what rule 3 needs |
| Add / edit section | `POST` / `PUT …/{templateId}/sections[/{id}]` | |
| Add / edit item | `POST` / `PUT …/sections/{sectionId}/items[/{id}]` | **409** if the same criterion or KPI is already on the template, in any section |
| Reorder | `PATCH …/sections/reorder`, `…/items/reorder` | body is a bare array of ids |
| Grade bands | `PUT api/AppraisalTemplates/items/{itemId}/grade-ranges` | **replace-all**; overlaps and repeated grades are refused |
| Activate | `PATCH …/{id}/active-status` | body is a bare boolean; runs the structure validation first |
| Clone | `POST …/{id}/clone` | the copy lands **inactive and in Draft**, whatever the source was, and its scope is stated on the copy rather than inherited |
| Submit / Approve / Reject / Recall | `POST …/{id}/submit-for-approval` etc. | **workflow engine**, `APPRAISAL_TEMPLATE` — published on this database, approvers HR / Manager / TenantAdmin |
| Delete | `DELETE …/{id}` | **`HR.Performance.Admin`** — 403 for `hr.head` |

### ⚠ Known gaps
**P-7.** Every structural write is refused while the template is on a live cycle — including on a
*Draft* cycle in some readings, because the assignment rows do not carry a cycle status and the client
treats any live assignment as a freeze. Erring this way shows the reason rather than letting the write
fail.
~~**P-8.** The free-text question has **0 grade bands**, which is one of the three activation rules. The
template is already Approved and active so nothing is blocked today, but a re-activation attempt could
be refused on it.~~ **Fixed 2026-09-29** (closure lane L-c): a free-text question with **weight 0** is
never scored, so activation and submit-for-approval no longer ask it for bands — the demo template's
question is weight 0 — and the editor shows *Not scored* where it used to flag the bands missing. A
**weighted** question still needs them. The refusal names each item by its criterion, KPI or question;
a criterion or KPI item used to be named by its id.
**P-9.** `Delete` renders and 403s for `hr.head`.

> **Changed 2026-09-29** (performance closure lane L-b). **A section can be a goals section** (§ 1.4).
> The API takes a section *kind* and holds three rules: a template has one goals section; a goals
> section takes no items (they would share its weight with the goals and count the section twice); and
> a section with items cannot become one. A copy keeps the kind, and activation counts an empty goals
> section complete. A section write that breaks a rule — these three, or the template being on a
> live cycle — now answers **409 with the reason**; it used to be a bare 500.
>
> **Changed 2026-09-29** (performance closure lane L-c). **The section dialog asks *What fills this
> section*:** *This template's items* — competencies, shared KPIs with one target for everyone, and
> questions — or *Each employee's locked goals*, which takes no items: every appraisal gets one row per
> goal the manager locked, weighted by the goals' own weights. The second choice is greyed out, with
> the reason, when the template already has a goals section or the section being edited holds items.
> A goals section's card carries an **Employee goals** badge and no *Add item*, and says how it fills;
> a note above the structure explains the two kinds — *personal targets are goals, not template KPIs*.
> The live checks leave an empty goals section alone and flag one that holds items.

---

## 9. `/administration/hr/performance/strategic-goals` — multi-year intent

### 📍 Where you are
**Route:** `/administration/hr/performance/strategic-goals` · Administration → HR → Performance → Strategic Goals
**As:** `hr.head` · **1 minute**

### 📖 What it is
The top of the cascade, and **the only level that outlives a cycle**. A strategic goal carries a year
span rather than a cycle, and each year's company goals link back to one. That link is what makes a
cycle's objectives traceable to a multi-year intent — and it is also what blocks deletion.

### 👁 On the page

| Title | Priority | Years | Company goals | Status |
|---|---|---|---|---|
| Operational Excellence 2026–2028 | High | 2026–2028 | **2** | Active |

The *Company goals* count is shown deliberately: it is the number that decides whether a delete will be
refused, and it is better seen before someone tries.

### ▶ Walk it
1. Open it. One row.

   💬 *"Everything else in this module is annual. This is not. 'Operational Excellence, 2026 to 2028' is
   a three-year intent, and each year's corporate objectives hang off it — which is how you answer the
   question 'what did the last three appraisal cycles actually deliver against the strategy?' rather
   than 'what did we appraise people on last year?'"*

2. Point at **Company goals — 2**.

   💬 *"And the system will not let you delete it while those two exist. Deactivate, yes — that stops it
   being chosen for new company goals. Delete, no."*

### ⚙ Behind the page
`api/StrategicGoals` → `StrategicGoals`. `DELETE` answers **400** while company goals still link to it.

---

## 10. `/administration/hr/performance/goal-library` — reusable goal wording

### 📍 Where you are
**Route:** `/administration/hr/performance/goal-library` and `…/goal-library/[id]`
**Sidebar:** Administration → HR → Performance → Goal Library
**As:** `hr.head` · **2 minutes**

### 📖 What it is
Reusable goal wording, so the same objective is not rewritten from scratch for every employee who
carries it. **Scope is a hint, not a rule**: a template restricted to a level, unit or position is
offered first to someone in that scope, but nothing stops a goal being written without a template.
Unscoped templates are offered everywhere.

**Copying is a snapshot.** Editing a template does not reach goals already created from it.

### 👁 On the list

| Title | Scope | Status |
|---|---|---|
| Deliver assigned layouts to planning approval within the cycle | Global | Active |
| Keep client correspondence inside the service standard | Global | Active |
| Complete one recognised professional qualification or refresher | Global | Active |

Each row links to a detail page carrying **Goals created / Employees / Cycles** tiles and a
**"Goals made from this template"** table.

### ▶ Walk it
1. Open the library. Three rows.
2. Open the first one.

   💬 *"The usage list is the point of this page. Because copying takes a snapshot, editing the wording
   here changes nothing on the goals already written from it — so this is the list of goals your edit
   will **not** reach. It is the argument for creating a new template mid-year rather than rewriting an
   old one."*

3. Expect **Not used yet** on this database — the demo goals were written directly rather than from
   the library. Say so; it is a fair thing to say.

### ⚙ Behind the page
`api/GoalLibrary`. Note the paging quirk: the `/{id}/usage` and `/selector` endpoints take **`page`**,
not `pageNumber` — the two exceptions in the module. The picker dialog on the employee-goal form reads
`/selector` and shows a pre-rendered `scopeSummary`.

### ⚠ Known gaps
**P-10.** The three library items are not used by any goal on the demo database, so the usage tiles all
read zero.

---

## 11. `/administration/hr/performance/goal-risk-settings` — what "at risk" means

### 📍 Where you are
**Route:** `/administration/hr/performance/goal-risk-settings` · Administration → HR → Performance → Goal Risk Thresholds
**As:** `hr.head` · **2 minutes** — small screen, big moment

### 📖 What it is
Two rules use these three numbers, and both fire only on goals that are still in flight:

1. **Close to the deadline with too little done** — inside `daysRemainingThreshold` of the due date
   **and** below `minimumProgressPercent`. *Severity 70.*
2. **Behind the run rate** — actual progress plus `expectedProgressTolerancePercent` is still under
   the progress a straight line from start to due date would predict by now. *Severity 60.*

A third rule, which needs no configuration: a goal a manager has **flagged by hand** in a progress
update or a check-in. *Severity 50.*

Rules are evaluated highest-severity first and the **first one that fires wins** — a goal is flagged for
one reason, not three.

### 👁 On the page

| | Value here |
|---|---|
| **Rule 1 — close to the deadline**: Days remaining | **14** |
| **Rule 1**: Minimum progress (%) | **60** |
| **Rule 2 — behind the run rate**: Tolerance (%) | **20** |

Buttons: **Save**, and **Reset to defaults** with the confirmation *"the tenant's own row is dropped
and the built-in defaults (14 days / 60% / 20%) apply again."*

### ▶ Walk it
1. Open it. Read the two rules aloud from the screen — they are written in English on the page.
2. Do the arithmetic out loud. This is the moment.

   💬 *"Take a goal that runs the calendar year. Today is the seventeenth of September, so that goal is
   about seventy-one per cent of the way through its life. Rule two says: if what you have actually
   done, plus twenty points of tolerance, is still short of seventy-one — you are behind. So anything
   under about fifty-one per cent gets flagged today. Not by a manager. By the system, against a
   threshold HR owns, on this screen."*

3. Do **not** change the numbers — the Goals At Risk chapter depends on them. If you want to show the
   lever, change **Tolerance** from 20 to 40, save, open Goals At Risk in another tab to show the list
   shrink, then come back and set it to 20. That is **🔴 LIVE WRITE 1** *(optional)*.

### ⚙ Behind the page
`api/performance/goal-risk-settings` → `GoalRiskSetting`. **Nothing seeds this table** — the evaluator
runs on the documented defaults until someone saves, and saving is what creates the tenant's row.
On this database a row **has** been saved, so the *"Running on the defaults"* banner is absent.

`GoalRiskEvaluator` is pure, stateless and does no database access: it takes a goal, the settings and
the clock, and returns `{ isAtRisk, severityScore, reason }`.

### ⚠ Known gaps
**P-11.** The thresholds are tenant-wide. There is no per-cycle or per-unit override — a three-month
probation goal and a twelve-month strategic goal are judged by the same fourteen days.

---

# PART II — THE CYCLE

*Ten minutes. This is the container everything else lives inside, and the Coverage tab is the single
strongest screen in the first half of the demonstration.*

---

## 12. `/hr/performance` — the module hub

### 📍 Where you are
**Route:** `/hr/performance` · Human Resources → Talent & Performance → Performance
**As:** `hr.head` · **2 minutes**

### 📖 What it is
Nineteen navigation cards, ordered by **who acts**: HR's overview first, then the cycle, then the
employee's and manager's work, then what happens around the sign-off, then the goal cascade, and the
deadline override last because it is the exception path.

### 👁 On the page
Header *"Performance — Appraisal cycles, and the goal cascade that runs inside them, from company
objectives down to each employee"*, then the cards. The order, with the sentence each one carries:

| # | Card | The sentence on the card |
|---|---|---|
| 1 | **Analytics** | Where a cycle has actually got to — pipeline, deadlines, score spread, per-unit progress, what needs chasing, and what the outcomes became |
| 2 | **Appraisal Cycles** | Each run of the appraisal process — its dates, who it covers, which forms it uses, and how far along it is |
| 3 | **Team Appraisals** | For managers: your reports in a cycle, what they have submitted, and your evaluation of each |
| 4 | **Conversations** | Kick-off, quarterly, mid-year and final review meetings held against an appraisal — the agenda before, the notes after |
| 5 | **Development Plans** | The organisation-wide register. Your own and your team's plans live in My Self-Service |
| 6 | **Improvement Plans** | Formal plans where performance has fallen short, their review meetings, and the outcome each one closed with |
| 7 | **HR Review** | For HR: appraisals ready for sign-off, what is outstanding on the rest, and the final score |
| 8 | **Calibration** | Panels that reconcile managers' ratings across a unit. On cycles that require it, nothing reaches HR review until a session is committed |
| 9 | **Appeals** | Employees contesting a finalised appraisal — what is waiting on HR, and what is back with a manager |
| 10 | **Recommendations** | What appraisals say should happen next, and whether the pay change, PIP or promotion it called for actually got created |
| 11 | **Proposals** | Pay changes and employment actions raised from appraisal outcomes, on their way to payroll and the modules that own them |
| 12 | **Company Goals** | A cycle's organisation-wide objectives, and how far each has cascaded into units and people |
| 13 | **Unit Goals** | What each org unit is accountable for, and whether it rolls up to a company goal or stands alone |
| 14 | **Employee Goals** | An employee's goals for a cycle: weights, alignment, progress entries and the approval workflow |
| 15 | **Team Goals** | For managers: goals awaiting your approval, whose sets are incomplete, and what is slipping |
| 16 | **Goals At Risk** | For HR: every at-risk goal across the organisation, most severe first, filterable by unit and level |
| 17 | **My Notifications** | Your own appraisal queue — cycle openings and deadline reminders — in the portal feed that carries every other notification too |
| 18 | **Deadline Enforcement** | For HR: push a stalled appraisal past a step nobody is going to complete. Audited against you |

*(Interim Reviews sits on the sidebar between Team Appraisals and Conversations; it does not have its
own hub card.)*

### ▶ Walk it
1. Open `/hr/performance`. Let the grid render; do not scroll immediately.
2. Point at the first two cards, then the middle block, then the last.

   💬 *"The menu is arranged the way the year happens, not alphabetically. At the top, HR's view of
   where the cycle has got to, and the cycle itself. Then the work, in the order people do it — the
   employee, their peers, their manager, HR. Then what happens around the sign-off: calibration before
   it, appeals after it, and the outcomes that come off the back of it. And right at the bottom, the
   override for when something has stalled and nobody is going to finish it."*

3. Move straight to **Appraisal Cycles**.

### ⚙ Behind the page
A static `NavCardGrid`. The self-shaped screens — My Appraisals, Peer Reviews, Check-ins, the Journal,
My Goals, My Development Plans — are **not** here; they live in the self-service portal, and desk users
reach them through *My Self-Service*. That is a deliberate split: this hub is the desk's work.

---

## 13. `/hr/performance/cycles` — the register

### 📍 Where you are
**Route:** `/hr/performance/cycles` · Human Resources → Talent & Performance → Performance → Appraisal Cycles
**As:** `hr.head` · **2 minutes**

### 📖 What it is
Every run of the appraisal process. A cycle is created as a **Draft**, given target groups and template
assignments on its detail page, and then **opened**. Opening is checked against the other cycles of the
same type and year: an employee may be covered by only one open cycle at a time, and the refusal names
the cycles that overlap (a Draft that overlaps is shown on the coverage preview as advice, and blocks
nothing).

Generation of the appraisal records themselves is deliberately a **separate step after opening**, so
the coverage preview can be read first.

### 👁 On the page

**Four tiles**

| Tile | Here | Hint |
|---|---|---|
| Cycles | **1** | |
| Live | **1** | *APC2026* |
| In draft | **0** | Not yet opened — still editable and deletable |
| Settings profiles | **1** | A cycle cannot be created without one |

**The table**

| Cycle | Name | Type | Year | Period | Settings | Status |
|---|---|---|---|---|---|---|
| **APC2026** | Annual Performance Cycle 2026 | Annual | 2026 | 2026-01-01 → 2026-12-31 | Standard Annual Appraisal | **Open** |

**The New cycle dialog** — *"Phase dates are all optional — a phase with no deadline never appears on
the calendar or in reminders."*

* **Cycle code** *(e.g. FY2026)* · **Year**
* **Cycle name**
* **Type** — Quarterly · Mid-year · Annual · One-off · **Probation**
* **Settings profile** *(required — the dialog says "No profiles configured" if none exist)*
* **Start date** · **End date** *(validated: end after start)*
* Then, below a divider, **all twenty-two phase dates** in pipeline order: goal setting opens/due, Q1
  opens/due, mid-year opens/due, Q3 opens/due, peer nomination due, self-evaluation opens/due, peer
  evaluation opens/due, manager evaluation opens/due, calibration opens/due, HR review opens/due,
  acknowledgment due, final conversation due.

### ▶ Walk it

1. Open **Appraisal Cycles**.
2. Read the tiles: one cycle, one live, none in draft, one settings profile.
3. Click **New cycle**, let the dialog open, scroll it to show the phase-date block, then **Cancel**.

   💬 *"A cycle is the year's contract. Its code and name, its window, the policy it runs under — and
   then every deadline in the process, from goal setting in January to the final conversation in
   December. Every one of those is optional, and a phase with no deadline simply never appears on the
   calendar or in the reminder sweep. You are not forced into a process shape you do not run."*

4. Click **APC2026** to open the detail page.

### ⚙ Behind the page

| Element | Endpoint | Note |
|---|---|---|
| List / tiles | `GET api/AppraisalCycle` | |
| Create | `POST api/AppraisalCycle` | always created as **Draft** — a status in the request is ignored (it was stored until 2026-09-30); Open and Closed are separate endpoints |
| Edit | `PUT api/AppraisalCycle/{id}` | **the payload deliberately carries no `status`** — it used to post a hard-coded `'Draft'`, so editing an open cycle's phase dates quietly reverted it. Once a cycle is opened or has appraisals, its **settings profile, year and type** are fixed (the dialog greys the type and profile); once it has appraisals, its **start and end dates** — a change is refused, 422, saying why. Name, code and phase deadlines stay editable |
| Delete | `DELETE api/AppraisalCycle/{id}` | only a cycle that has never been opened; **`HR.Performance.Admin`** |

### ⚠ Known gaps
**P-12.** `Delete` renders for `hr.head` and 403s.

---

## 14. `/hr/performance/cycles/[id]` — the cycle, and its seven tabs

### 📍 Where you are
**Route:** `/hr/performance/cycles/{id}`
**As:** `hr.head` · **8 minutes** — the anchor chapter of Part II

### 📖 What it is
One cycle, end to end. The tab order **is** the order the work happens in, and it is not arbitrary:

```
  Targets    who this cycle covers, as RULES rather than a list of names
  Templates  which forms it may draw on, and how ties between them break
  Coverage   a dry run of the two above. Nothing is written — this is where a missing template
             or a tie between two equally-specific ones shows up
  Generate   only once coverage is clean, because generation refuses on a gap or a conflict
```

Opening the cycle sits before all of that and is checked separately.

### 👁 The header

**Title:** `APC2026 · Annual Performance Cycle 2026`
**Subtitle:** `Annual · 2026-01-01 → 2026-12-31 · Standard Annual Appraisal`
**Status badge:** *Open*

**Buttons**, which change with status:

| Button | Shown when | What it does |
|---|---|---|
| **Open cycle** | Draft only | *(absent here — the cycle is already open)* |
| **Generate appraisals** | any non-Closed cycle | Creates the appraisal records for everyone in scope |
| **Send reminders** | open, not closed | Raises an in-app notice for every phase overdue or closing soon, to everyone in scope |
| **Close cycle** | open, not closed | Irreversible; a closed cycle refuses every edit |

### 👁 The four tiles

| Tile | Here | What it means |
|---|---|---|
| **Employees in scope** | ~107 | Resolved live from the target rules |
| **Template coverage** | **100%** | Share of in-scope employees a template resolves for |
| **Without a template** | **0** | Blocks generation until zero |
| **Template conflicts** | **0** | Two templates tied at the same priority |

Below them, an amber card appears **only** when generation would be refused. On this database it does
not appear — coverage is clean.

### 👁 Tab 1 — Overview
Two cards. **Cycle**: code, type, year, period, settings profile, status, opened (date + by whom),
closed. **Phase dates**: eleven read-only rows with an **Edit dates** button that opens a dialog
carrying all twenty-two.

### 👁 Tab 2 — Targets
Intro line: *"Who this cycle covers, stated as rules rather than a list of names — so someone who joins
the unit tomorrow is in scope without anyone editing anything. Exclusions carve people back out of a
rule."*

| Type | Scope | Estimated | Resolves to | Status |
|---|---|---|---|---|
| Organisation unit | **Managing Director's Office** | 0 | **96** | Active |
| Organisation unit | **Internal Audit Department** | 0 | **6** | Active |

Each row has an **Exclusions…** action, which opens a panel below. The MD's Office target carries
**one** exclusion:

> **Excludes:** Nana Nyaho · **Reason:** *"The Managing Director is appraised by the Board of Directors
> under his contract of engagement, not through the corporate appraisal cycle."*

The target dialog: **Target type** (Organisation level · Organisation unit · Position), an org-unit
picker (level, then unit), a position picker, **Estimated headcount** *("Your planning figure — the real
count is resolved live")*, **Notes**, **Active** *("Inactive target groups are ignored entirely, including
by the coverage preview")*. The dialog's hint — *"Pick the scope that matches the target type — the other
two are ignored"* — is what the server does: it keeps the scope the type names and clears the others.

> **Changed 2026-09-30** (performance closure lane E, slice E-c). **Resolves to** is resolved live: the
> target's active staff whom the cycle appraises, after the exclusions — 96 is the MD's Office less the
> MD, and 96 + 6 is the 102 the cycle appraises. It showed the typed estimate (0 here), whatever the
> guide said. An inactive target reads 0. **A unit target can be made from this dialog**: the picker sends
> the level it was chosen under beside the unit, which the server refused (400). The *Individual employee*
> type is gone — no column could hold one, and it covered nobody; one person is covered through their
> position, or left out by an exclusion. A target stays in its cycle, one per scope, and a closed cycle's
> targets and exclusions no longer change.

The exclusion dialog: **Employee** picker, **Position**, **Organisation unit**, **Reason** *(required)*,
**Active**.

### 👁 Tab 3 — Templates
Intro line: *"Each employee gets the most specific one that matches them — position beats unit beats
level beats global — and **priority** is what breaks a tie between two that are equally specific. Only
approved, active templates are offered."*

| Template | Scope | Priority | Status |
|---|---|---|---|
| Standard Employee Template 2026 | Global | 10 | Active |

### 👁 Tab 4 — Coverage *(the one to spend time on)*
Intro: *"A dry run of generation. Nothing here is written — it simulates resolving every in-scope
employee to a template, and reports whom it could not place."*

Three cards:

* **Competing cycles** — other cycles of the same type and year sharing employees. A running one
  **blocks opening**; a draft one is advisory. *Absent here: there is only one cycle.*
* **By template** — Template · Scope · Priority · Employees. One row, carrying everybody.
* **By employee** — the full table: **Employee · Position · Unit · Template · Matched on · Status**,
  where status is `Covered` / `NoTemplate` / `Conflict` / `Excluded`, in name order with the excluded
  last (since 2026-09-30; the order was whatever the database returned). Nana Nyaho appears as
  **Excluded**, with the exclusion reason on hover. The preview reads the same scope rule generation
  does, so what it lists is who a generation would appraise.

### 👁 Tab 5 — Progress & alerts
* **Completion** — four bars: self-evaluation, peer evaluation, manager evaluation, HR review, each
  reading *completed / total (percent)*. A step the settings profile does not require is greyed with
  *"Not required"* rather than shown at zero.
* **Deadline risks** — the five year-end phases only, banded by the profile's risk days, each row
  showing phase, date, relative time and a severity badge (or **Overdue**).
* **Bottlenecks** — where the cycle is actually stuck, with a count per category.
* **Participation** — Targeted · Excluded · Appraisals · Withdrawn · Not started self-evaluation ·
  Managers over workload · and the target breakdown by level / unit / position. *Targeted* is the scope
  — the active staff the targets reach, less the excluded: **102** (it read the count of appraisals, 107,
  until 2026-09-30, slice E-d1); *Excluded* is how many of the targets' staff an exclusion leaves out —
  **1**, the MD (it read 0 until slice E-c); *Appraisals* is the appraisals in play — **107**, the five
  Rule 8 fixtures included — and every bar above is out of them; *Withdrawn* counts those taken out of
  the cycle (chapter 31), **0** here, which no other figure on the tab includes.

### 👁 Tab 6 — Interim reviews
HR's org-wide view of the cycle's checkpoints. Four tiles — **Checkpoints 107**, **Not started 102**,
**Awaiting manager 0**, **Completed 5** — then the table: Employee · Checkpoint · Date · Depth ·
Period score · Status.

### 👁 Tab 7 — Calendar
*"Derived, not stored: every phase date on the cycle plus its review events and check-ins, in date
order."* Columns: **Date · Event · Phase · Kind**, where Kind is *Deadline* or *Event*.

### ▶ Walk it

1. You are on **Overview**. Point at *Opened 2026-01-02*.
2. Go to **Targets**.

   💬 *"Who is in this cycle? Not a list of names — two rules. Everybody under the Managing Director's
   Office, which is both directorates and everything beneath them. And Internal Audit separately,
   because Internal Audit reports to the Board, not to the Managing Director, so it does not sit inside
   that tree. Somebody who joins the Development Department tomorrow is in scope tomorrow, and nobody
   edits anything."*

3. Click **Exclusions…** on the MD's Office row. Read the exclusion out loud.

   💬 *"And one carve-out. The Managing Director is appraised by the Board under his contract of
   engagement, so he is excluded — with the reason on the record, not in somebody's head."*

4. Go to **Templates**. Point at the priority.

   💬 *"One form this year, scoped globally. When there are several — a supervisory form, a technical
   form, a form for the depot — each employee gets the most specific one that matches them: position
   beats unit, unit beats level, level beats global. Priority is only there to break a tie between two
   that are equally specific."*

5. Go to **Coverage**. **Stop here.** Let the table render and scroll it slowly.

   💬 *"This is the screen I would not run a cycle without. Before anything is created, the system
   simulates the whole thing: it takes every employee the rules put in scope, works out which form they
   would be scored on, and tells you who it could not place. A hundred per cent covered, nobody without
   a form, no conflicts — and one person excluded, by name, with the reason. Nothing has been written.
   If this were not clean, generation would refuse, and it would refuse naming the people."*

6. Point at the tile **Without a template — 0**.

   💬 *"That zero is a gate, not a statistic. While it is anything else, the Generate button answers
   with the list of names."*

7. Go to **Progress & alerts**. Read the four completion bars and one deadline row.

   💬 *"And once it is running, this is where it has got to. Self-evaluation, peers, manager, HR —
   completed over total, with the steps this policy does not require greyed out rather than sitting at
   zero, so a switched-off step never reads as a late one."*

8. Go to **Calendar**, show the derived list, and move on.

9. **Do not press Generate, Send reminders, or Close.** If you want to show what Generate does, open
   the confirmation and read it, then cancel:

   > *"Creates the appraisal records for everyone in scope. Refused if anyone has no template or a
   > template conflict — check the Coverage tab first."*

### ⚙ Behind the page

| Element | Endpoint | Table |
|---|---|---|
| Header + Overview | `GET api/AppraisalCycle/{id}` | `AppraisalCycles` |
| Tiles + Coverage tab | `GET …/{id}/coverage-preview` | read-only simulation; `isGenerationSafe` is the single flag to check |
| Progress tab | `GET …/{id}/progress` | not fetched at all while the cycle is Draft |
| Calendar tab | `GET …/{id}/calendar` | derived, never stored |
| Targets | `GET/POST/PUT/DELETE …/{cycleId}/targets[/{id}]` | `AppraisalCycleTargets` |
| Exclusions | `…/api/AppraisalCycleTarget/{targetId}/exclusions` | `AppraisalCycleTargetExclusions` |
| Template assignments | `api/AppraisalCycleTemplates` (`by-cycle`, `bulk-assign`, `resolve/{cycle}/{employee}`) | `AppraisalCycleTemplates` |
| **Open cycle** | `POST …/{id}/open` | 400 when another open cycle of the same type and year covers any of the same employees — **and the message names them**. Also notifies everyone in scope: the people a generation would appraise (since 2026-09-30 — it also reached leavers, inactive targets and anyone holding a post a template names) |
| **Generate appraisals** | `POST …/{id}/generate-appraisals` | creates `PerformanceAppraisals`, **takes the criterion snapshot**, creates the self and manager evaluator records and the review events. **Skips anyone who already has an appraisal in the cycle** |
| **Send reminders** | `POST …/{id}/deadline-reminders` | repeat-safe — an identical unread reminder is skipped rather than duplicated |
| **Close cycle** | `POST …/{id}/close` | |

All four lifecycle actions require **`HR.Performance.Write`**.

> **Worth knowing.** *Send reminders* covers **more** phases than the deadline table above it — goal
> setting, peer nomination and the final conversation as well as the five year-end ones. So it can
> legitimately raise notices for phases that are not listed as risks. The card on the Progress tab says
> so.

### ⚠ Known gaps
**P-13.** Generation **skips employees who already have an appraisal in this cycle**, and does not
repair one that is wrong. That is how the five snapshot-less fixture appraisals of Rule 8 survive —
regenerating will not fix them; only removing and regenerating will, and removal is Admin-tier.
**P-14.** Q1 and Q3 review dates are unset on this cycle, so those rows read an em dash on the Overview
and never appear on the calendar.
**P-15.** *Estimated headcount* on both targets is 0 — it is HR's own planning figure and nobody filled
it in. The *Resolves to* column beside it is the real, live number (since 2026-09-30, slice E-c; before
that it repeated the estimate).

---

# PART III — THE GOAL CASCADE

*Fifteen minutes, four personas. This is the half of the module that runs all year, and it is the half
that answers "what is anyone actually being measured on?"*

**The shape, in one line:** a multi-year **strategic goal** → this year's **company goals** → each
unit's **unit goals** → each person's **employee goals**, weighted to 100 and approved by their own
manager.

---

## 15. `/hr/performance/company-goals` — this year's corporate objectives

### 📍 Where you are
**Route:** `/hr/performance/company-goals` and `…/company-goals/[id]`
**Sidebar:** Human Resources → Talent & Performance → Performance → Company Goals
**As:** `hr.head` · **3 minutes**

### 📖 What it is
One appraisal cycle's organisation-wide objectives — the top of the cascade managers align their unit
goals to.

**Visibility is the lever that matters here.** A *visible* goal appears to everyone choosing an
alignment for their own goal; an invisible one is HR's to cascade deliberately. Turning it off does not
hide anything already aligned — it stops **new** alignments rather than unpicking existing ones.

The counts on each row are cascade depth: how many unit goals took this up, and how many employee
goals point at it directly. Those are genuinely different numbers, because an employee goal can align
straight to a company goal without going through a unit.

### 👁 On the page

**Cycle picker** at the top — every goal screen opens with one.

**Five tiles**

| Tile | Here |
|---|---|
| Company goals | **2** |
| Unit goals cascaded | **3** |
| Employee goals aligned | **10** *(all of them fixtures — see Rule 5)* |
| Visible to employees | **2** |

**Filters:** Search *(title or description)* · Priority *(Any / Critical / High / Medium / Low)* ·
Visibility *(Any / Visible / Hidden)* · Due date to.

**The table**

| Goal | Priority | Target | Due | Unit goals | Employee goals | Visibility |
|---|---|---|---|---|---|---|
| Reduce Operational Costs by 10% | **Critical** | 10 % | 31 Dec 2026 | 2 | 5 | Visible |
| Improve Customer Satisfaction by 15% | **High** | 15 % | 31 Dec 2026 | 1 | 5 | Visible |

**The dialog**: Title · **Strategic goal** *(picker)* · Description · Success criteria · Priority ·
Due date · Target value · Unit · **Visible to employees** — whose help text is worth quoting: *"Visible
goals can be chosen as an alignment. Hiding one stops new alignments; it does not unpick existing
ones."*

### 👁 The detail page (`/company-goals/[id]`)
Four tiles — **Unit goals · Employee goals · Total aligned · Average progress** — then two cards
(*Goal*, with description and success criteria; *At a glance*, with cycle, strategic goal, target and
employee progress), then **"Unit goals cascaded from this goal"**: Unit goal · Org unit · Owner ·
Priority · Target · Due.

### ▶ Walk it

1. Open **Company Goals**. The cycle picker preselects APC2026.
2. Read the two goals out loud, with their priorities.

   💬 *"Two corporate objectives for 2026. Reduce operational costs by ten per cent — critical. Improve
   customer satisfaction by fifteen — high. Both trace back to the three-year strategic goal we looked
   at in setup, and both are visible, which means anybody in the Corporation writing their own goal can
   choose to align to them."*

3. Point at the **Unit goals** and **Employee goals** columns.

   💬 *"And these two numbers are the cascade. Two departments have taken up the cost goal; one has
   taken up the customer one. Those are different numbers from the employee count beside them, because
   somebody can align straight to a corporate objective without going through their department."*

4. Open **Reduce Operational Costs by 10%**. Show the two unit goals under it — the Development
   Department's drawing-office cost goal and HR's vacancy fill-rate goal.

   💬 *"So 'reduce operational costs by ten per cent' is not a slogan. In the Development Department it
   means cutting the drawing-office cost per approved layout by ten per cent. In HR it means filling
   ninety per cent of approved vacancies within ninety days. Same corporate objective, two departmental
   translations, each with its own owner and its own measure."*

### ⚙ Behind the page

| Element | Endpoint |
|---|---|
| Tiles | `GET api/CompanyGoals/dashboard/metrics?cycleId=` |
| Table | `GET api/CompanyGoals/dashboard/paged` — HR/Admin only |
| Alignment picker elsewhere | `GET api/CompanyGoals/visible/{cycleId}` — **only the visible ones** |
| Detail cascade counts | `GET api/CompanyGoals/{id}/cascade-stats` |
| Visibility toggle | `PATCH api/CompanyGoals/{id}/visibility` — body is a bare boolean |
| Delete | `DELETE api/CompanyGoals/{id}` — **`HR.Performance.Admin`** |

Table: `CompanyGoals`, keyed to `AppraisalCycleId` and optionally `StrategicGoalId`.

### ⚠ Known gaps
**P-16.** The list is a projection carrying only a 200-character description preview, so opening the
edit dialog fetches the full goal first — a brief spinner on Edit is expected, not a fault.
**P-17.** `Delete` renders for `hr.head` and 403s.

---

## 16. `/hr/performance/unit-goals` — what each department is accountable for

### 📍 Where you are
**Route:** `/hr/performance/unit-goals` and `…/unit-goals/[id]`
**Sidebar:** Human Resources → Talent & Performance → Performance → Unit Goals
**As:** `hr.head` *(and worth re-opening as `head.dev` — see the walk)* · **3 minutes**

### 📖 What it is
A company goal taken up by one org unit, **or** an objective a unit sets for itself. The middle of the
cascade: employee goals align to these.

**"Unlinked" is the number to watch.** A unit goal with no parent company goal is legitimate — not
everything a department does rolls up to a corporate objective — but a cycle where most of them are
unlinked usually means the cascade was never done. So it gets its own filter and its own tile.

**And this screen shows different things to different people without being told which.** A caller
holding only the `Manager` role is narrowed **server-side** to the goals they created. HR sees the whole
cycle. Same request, same screen.

### 👁 On the page

**Four tiles:** Unit goals **3** · Linked to a company goal **3** · Unlinked **0** · Employee goals
cascaded **0** *(Rule 5)*.

**Filters:** Search · Organisation unit · Priority · Alignment *(Linked / Unlinked)*.

**The table**

| Goal | Org unit | Owner | Aligned to | Priority | Due | Employee goals |
|---|---|---|---|---|---|---|
| Cut the drawing-office cost per approved layout by 10% | Development Department | Kwasi Danquah | Reduce Operational Costs by 10% | High | 31 Dec 2026 | 0 |
| Close 95% of lessee enquiries within 10 working days | Estates Department | *(Head of Estates)* | Improve Customer Satisfaction by 15% | High | 31 Dec 2026 | 0 |
| Fill 90% of approved vacancies within 90 days of requisition | HR / Administration Department | Akpene Amoah | Reduce Operational Costs by 10% | High | 31 Dec 2026 | 0 |

**The dialog:** Title · **Aligned to company goal** *(only visible ones are offered)* · Organisation
unit · Priority · **Owning manager** *(employee picker — "Search for the accountable manager…")* ·
Description · Success criteria · Target value · Unit · Due date.

> The **organisation level is not asked for**: it is a property of the unit, so it is taken from the
> chosen unit. The two cannot disagree.

### 👁 The detail page
*Goal* and *At a glance* cards (org unit, owner, aligned-to, target, **cascade progress**), a
**Supporting evidence** attachment panel, and **"Employee goals cascaded from this goal"** — Employee ·
Goal · Priority · Status · Progress · Due.

> **Changed 2026-09-29** (performance closure lane P, P11). The per-employee rows are shown to HR,
> the manager who raised the goal, and the head of the goal's unit or of any unit above it. Everyone
> else — the page is open to the whole tenant — sees the cascade in numbers instead: *"N employee
> goals aligned to this goal"* and the cascade progress, naming nobody. Before, any member of staff
> could read which colleague was behind on which goal.

### ▶ Walk it

1. Open **Unit Goals**. Three rows, all linked.
2. Point at the **Unlinked — 0** tile.

   💬 *"Zero unlinked. Every departmental goal this year rolls up to a corporate objective. When that
   number is high, the cascade did not happen — people wrote departmental goals in isolation — and it is
   worth knowing in February rather than in December."*

3. Open **Cut the drawing-office cost per approved layout by 10%**.

   💬 *"The Head of Development owns this one. Ten per cent off the cost per approved layout, measured
   from the department's own vote against the 2025 figure, due at the end of the year."*

4. Scroll to **Employee goals cascaded from this goal**. It is **empty**. Say so plainly — and then fix
   it two chapters from now.

   💬 *"And nothing has been aligned to it yet, which is exactly the gap this screen exists to show.
   In a moment I am going to align one of Efua Seidu's goals to it, and we will come back and watch it
   appear."*

5. *(Optional, 30 seconds, strong.)* Switch to window B as `head.dev` and open the same screen.

   💬 *"Same URL, same screen — and he sees only what he owns. Nobody built him a cut-down page; the
   API narrowed the answer to the person asking."*

### ⚙ Behind the page

| Element | Endpoint |
|---|---|
| Tiles / table | `GET api/UnitGoals/dashboard/metrics`, `…/dashboard/paged` |
| Detail | `GET api/UnitGoals/{id}`, `…/{id}/cascade-stats`, `…/{id}/employee-goals` |
| Attachments | `GET/POST/DELETE api/UnitGoals/{goalId}/attachments` — uploads go through the controlled scan gate |

> ⚠ **The attachment entitlement here is weaker than anywhere else in the module**: a unit goal's
> attachments are readable by **anyone authenticated in the tenant**, because a departmental target is
> not personal data. Do not attach anything sensitive to a unit goal. This is deliberate (closure
> decision D-26), and the upload card says so beside the Attach button.

> **A quirk worth knowing if you ever script against it:** `UnitGoals/dashboard/paged` binds
> `priority` as an **integer** while every other endpoint takes the enum name. The client converts it;
> sending `"High"` there would silently drop the filter.

### ⚠ Known gaps
**P-18.** Employee-goal cascade counts read 0 on every unit goal (Rule 5).
**P-19.** Unit-goal attachments are tenant-readable rather than entitlement-scoped. *Kept by decision
(D-26, closure P18): a departmental target is not personal data, and the upload card has said so since
2026-08-09.*

---

## 17. `/me/performance/goals` — the employee's own leg

### 📍 Where you are
**Route:** `/me/performance/goals` · My Self-Service → Performance → My Goals
**As:** `staff` *(Efua Seidu)* · **4 minutes** — 🔴 **LIVE WRITE 2 and 3 live here**

### 📖 What it is
The employee's own half of the goal lifecycle, which is deliberately **not** on the workflow engine:

```
   I draft my goal  →  I submit it to my manager  →  they approve, or reject with feedback
   →  I record progress against it until the cycle scores it
```

Every read and write is **self-armed**: the list is "my goals", and submit and progress take the actor
from the token. Creating or acting on anyone else's goal is refused server-side.

### 👁 On the page

**Header** *"My goals — Draft your goals for the cycle, send them to your manager, and record progress
as you go"*, a cycle picker, and **New goal**.

**Four tiles:** Goals **3** · Awaiting approval **0** · Overall progress **~55%** · Goal setting
*(the cycle's goal-setting deadline — 31 Jan 2026)*.

**A card per goal**, not a table:

* Title, then a grey line: `priority · weight N · KPI: name · start – due`
* Description
* A red quote box when the status is Rejected: *Your manager: "…"*
* Status badge, plus a **Locked** badge when locked
* A progress bar with the percentage
* Buttons, which depend on the status:
  * **Edit** and **Submit for approval** — only while Draft or Rejected, and not locked
  * **Record progress** — only while the goal is live (Approved / InProgress / OnTrack / AtRisk),
    **locked or not**

> **Changed 2026-09-29** (performance closure lane L-a). **Record progress stays on a locked goal**
> (decision D-29): the lock freezes what the goal is, and its year runs on — the button used to vanish
> the moment a goal was locked. **Editing a draft no longer wipes what the dialog does not show**: the
> KPI link, the minimum and maximum, the success criteria and the alignment used to be cleared by
> every save from this page.

**Efua's three cards, as they stand:**

| Goal | Weight | Status | Progress |
|---|---|---|---|
| Deliver the Community 25 Phase 2 layout to planning approval | 40 | **On track** | **70%** |
| Cut drawing-office turnaround on client amendments to 5 working days | 30 | **At risk** | **55%** |
| Complete Civil 3D certification | 30 | **In progress** | **40%** |

**The goal dialog:** Title · Description · Priority · Weight (%) · Measured as · Period · Target value ·
Unit · Starts · Due.

**The progress dialog:** Progress % · Actual value · Status · Notes *("What moved, and what is in the
way")*.

### ▶ Walk it

1. In **window B**, sign out and sign in as `staff` / `Demo123!`.
2. Go to **My Self-Service → Performance → My Goals**.
3. Read the three cards aloud with their weights.

   💬 *"Efua Seidu is a Project Coordinator in the Development Department. Three goals for the year,
   weighted forty, thirty and thirty — a hundred. The layout is on track at seventy per cent. The
   turnaround goal is at risk. The certification is in progress at forty."*

4. Point at the **At risk** badge on the turnaround goal.

   💬 *"And she did not set that herself. Her manager flagged it in their September one-to-one, and the
   flag travelled from the meeting straight onto the goal. We will see that conversation in a moment."*

5. 🔴 **LIVE WRITE 2 — record progress.** Click **Record progress** on *Complete Civil 3D
   certification*. Enter **Progress %** = `55`, **Status** = *In progress*, **Notes** = `Modules 5 and 6
   complete; examination booked for 14 November.` Save.

   Expect the toast **"Progress recorded — 100% completes the goal"**, the card's bar to move to 55%,
   and the tile *Overall progress* to rise.

   💬 *"That entry is not a comment. The newest progress entry becomes the goal's own percentage and its
   own status — which is how a goal reaches 'on track' or 'at risk', and therefore how it reaches HR's
   at-risk report. And a hundred per cent completes it outright, whatever the entry says."*

6. 🔴 **LIVE WRITE 3 — draft a fourth goal.** Click **New goal**. Title: `Mentor one draughtsman to
   supervising grade readiness`. Priority **Medium**, **Weight 0**, Measured as *Percentage of target*,
   Period *Full cycle*, Target `100`, Unit `%`, Starts today, Due `2026-12-31`. Save.

   Expect a new card in **Draft**, with **Edit** and **Submit for approval** on it.

   💬 *"A new goal is a draft. It counts for nothing — it is not on her manager's list, it is not in any
   report — until she submits it. Weight zero on purpose, because her three existing goals already total
   a hundred and I do not want to unbalance her set in front of you."*

7. Click **Submit for approval** on the new card. Expect the toast **"Submitted — Your manager has it
   now"** and the status to become **Pending approval**. The *Awaiting approval* tile goes to **1**.

   💬 *"And it has gone to Kwasi Danquah — because the establishment says she reports to him. Nobody
   chose an approver on that form. There is no approver field."*

8. Leave it there. You will approve it as `head.dev` in chapter 19.

### ⚙ Behind the page

| Control | Endpoint | Note |
|---|---|---|
| The list | `GET api/EmployeeGoals/by-employee/{me}?cycleId=` | |
| Tiles | `GET api/EmployeeGoals/summary/{employeeId}/{cycleId}` | counts by status, overall progress, set completeness |
| Save | `POST` / `PUT api/EmployeeGoals[/{id}]` | content only — **an edit never changes status** |
| Submit | `POST api/EmployeeGoals/{id}/submit` | 204; **422 when the employee has no manager on their HR record** |
| Record progress | `POST api/EmployeeGoals/{id}/progress` | writes `GoalProgressEntries`, then **carries the percent and status onto the goal**. Only accepted while approved / in progress / at risk |

> **No `recordedById` is sent.** The recorder is the signed-in user, stamped server-side. It used to be
> a picker defaulting to the goal's owner "so HR could record on somebody's behalf" — which is the same
> thing as letting anyone attribute a progress claim to a colleague who never made it.

### ⚠ Known gaps
**P-20.** Nothing blocks a goal set whose weights do not total 100. It is flagged in the manager's
governance view, not refused at submit.

---

## 18. `/hr/performance/employee-goals` — the desk register

### 📍 Where you are
**Route:** `/hr/performance/employee-goals` and `…/employee-goals/[id]`
**Sidebar:** Human Resources → Talent & Performance → Performance → Employee Goals
**As:** `hr.head` · **4 minutes** — 🔴 **LIVE WRITE 6 lives here**

### 📖 What it is
One employee's goals for a cycle — the bottom of the cascade and the only level with an approval
workflow. HR-shaped: an employee picker, the whole cascade, the KPI library.

**Two rules govern the set rather than any single goal**, and both are shown above the table:

* **The weights should total 100.** Nothing blocks a submit that leaves them unbalanced, but it is what
  the manager's governance view flags — and, since closure lane L-a, **the manager cannot lock the set
  until it does** (below).
* **A goal must be aligned to something for the cascade to mean anything.** Unaligned goals are allowed,
  and called out.

**Status only ever moves through submit / approve / reject.** Editing a goal never changes its
status — the API ignores a status sent on an edit — so the row actions here are the only route between
states.

> **Changed 2026-09-29** (performance closure lane L-a, decisions D-29 and D-30).
> - **A lock is a flag, not a status.** It freezes what the goal is — title, measure, target, weight,
>   period, success criteria, owner — and not its year: progress still moves a locked goal.
> - **The manager locks the whole set** once it is complete (`POST api/EmployeeGoals/lock-set`; since
>   lane L-c, the **Lock set** button on the team desk, chapter 19): every live goal approved, the count
>   inside the cycle's minimum and maximum, the weights adding to 100. Each refusal names what is
>   missing — *"the goal weights add up to 90%, not 100%"*. A rejected goal is not part of the set.
> - **What an approved goal measures cannot be edited.** Its description, priority, dates and
>   alignment can; its title, measure, target, weight, period and success criteria cannot — *"Ask
>   your manager to send it back to you for changes."* The manager's **Send back** (on the detail
>   page) returns an approved or running goal, not locked and not completed, to the employee with a
>   reason; its progress entries are kept.
> - **An edit never changes a goal's owner, cycle or appraisal link.** It used to copy all three from
>   the form, so an edit could move a goal into a colleague's set, and every edit made in the UI
>   cleared the link.
> - **Unlocking returns a goal left in the old Locked status to Approved**, so it can be locked
>   again.

### 👁 On the page

**Employee picker** and **cycle picker** at the top. Until both are chosen: *"Choose an employee and a
cycle — goals are held per employee per appraisal cycle."*

**Four tiles:** Goals · **Total weight** *(with an amber "Weights do not total 100%" warning under it
when they don't)* · Overall progress · Awaiting approval.

**The table**

| Goal | Aligned to | Weight | Priority | Status | Progress | Due |
|---|---|---|---|---|---|---|

with a **Locked** badge beside the status where applicable, and a **⋯** menu per row:

* **Edit** *(hidden when locked)*
* **Submit for approval** *(only from Draft or Rejected)*
* **Lock** *(only from a post-approval status, and not already locked)* / **Unlock**
* **Open detail**
* **Delete** *(hidden when locked)*

**The goal dialog** — the fullest form in the module:

* A dashed header panel: **Start from a template** → opens the **goal library picker**
* Title · **Aligned to** *(one select holding both company and unit goals — a goal aligns to at most
  one, and the server derives the parent type from whichever FK it receives)*
* Description · Success criteria
* Weight (%) · Priority
* Measurement type · Period
* **KPI definition** *(optional)*
* Target value · Unit · Minimum · Maximum
* Start date · Due date

Dialog hint: *"New goals start as a draft. Submitting is what sends them to the manager."*

### 👁 The detail page (`/employee-goals/[id]`)
* Header with status, plus **Submit**, **Approve**, **Reject**, **Send back** *(an approved or running
  goal, not locked — since closure lane L-a)*, **Lock** / **Unlock** offered **on status alone** — so a
  manager-only action attempted by someone else comes back 403 *with the reason* rather than being
  hidden as if it did not exist.
* Banners: *Returned by the manager* (with the feedback) · *Locked* — *"What the goal measures — its
  title, target, weight and period — cannot change until it is unlocked. Progress is still recorded
  against it through the year."*
* **Goal** card — description, success criteria, manager feedback
* **At a glance** — progress, weight, priority, **aligned to**, measured as, target, range, from
  template, period, runs
* **Approval trail** — submitted to, submitted, approved
* **Progress entries** — Recorded · By · Progress · Actual · Status · Notes, with an add form carrying
  Progress (%) *("Leave blank to note without moving progress")*, Actual value, Status, Challenges,
  Notes

### ▶ Walk it

1. In **window A** as `hr.head`, open **Employee Goals**.
2. Pick **Efua Seidu** in the employee picker. The cycle preselects.
3. Point at **Total weight — 100%**.

   💬 *"A hundred. The system lets her submit an unbalanced set — the weights settle as the goals are
   agreed — but her manager cannot lock the set until it adds up to a hundred, and it is the first
   thing his governance screen tells him about, and the first thing HR sees here."*

4. Point at the **Aligned to** column: all three read **Standalone**.

   💬 *"And here is the gap I promised you. Three good goals, none of them aligned to anything. Which
   means the Head of Development cannot answer the question 'how much of my departmental cost goal is
   actually being carried by somebody?' Let me fix one."*

5. 🔴 **LIVE WRITE 6 — align a goal.** Open the **⋯** menu on *Deliver the Community 25 Phase 2 layout
   to planning approval* → **Edit**. In **Aligned to**, choose **Cut the drawing-office cost per
   approved layout by 10%**. Save.

   Expect the row's *Aligned to* cell to change from *Standalone* to the unit goal's title.

6. Switch to the **Unit Goals** tab you left open, open **Cut the drawing-office cost per approved
   layout by 10%**, and refresh.

   Expect **Efua Seidu** to appear under *Employee goals cascaded from this goal*, with her status
   (On track) and her progress (70%).

   💬 *"And there it is. One corporate objective, one departmental translation, one person's actual
   work — and now the department head can see, on his own goal, who is carrying it and how far along
   they are."*

7. Back on Employee Goals, open the **⋯** menu again and show the lifecycle actions without pressing
   them.

   💬 *"Everything that moves a goal between states is its own command, and the API decides who may run
   each one. The owner submits. Their direct manager approves, rejects, and locks. HR can see all of it
   and cannot do any of it — and if I press Approve here, I get a 403 that says so in words, rather than
   a button that was never there."*

### ⚙ Behind the page

| Control | Endpoint | Who |
|---|---|---|
| List | `GET api/EmployeeGoals/by-employee/{id}?cycleId=` | |
| Save | `POST` / `PUT api/EmployeeGoals[/{id}]` | content only |
| Submit | `POST …/{id}/submit` | **the owner** |
| Approve | `POST …/{id}/approve` *(optional feedback)* | **the direct manager** |
| Reject | `POST …/{id}/reject` — **feedback is mandatory**, a bare rejection is 422 | the direct manager |
| Lock / Unlock | `POST …/{id}/lock` / `…/unlock` | the direct manager |
| Progress | `GET/POST/PUT/DELETE …/{id}/progress[/{entryId}]` | |
| Library picker | `GET api/GoalLibrary/selector` | copies title, description and success criteria, and records the template id — which is what the library's usage figures count |

All four lifecycle commands return **204** and take **no ids**: the caller is resolved from the token.
**403** means "not your goal" or "not the employee's manager"; **422** means the transition is not
allowed from the current status.

### ⚠ Known gaps
**P-21.** The desk register offers **Submit** but not **Approve** / **Reject** — those are on the goal
detail page and on the manager's Team Goals screen. A reasonable split, but it means HR cannot approve
a goal from the register even in principle.
**P-22.** The goal library link is set **only at creation**; there is nothing to re-point it at
afterwards.

---

## 19. `/hr/performance/team-goals` — the manager's governance desk

### 📍 Where you are
**Route:** `/hr/performance/team-goals` · Human Resources → Talent & Performance → Performance → Team Goals
**As:** `head.dev` *(Kwasi Danquah)* · **4 minutes** — 🔴 **LIVE WRITE 4 lives here**

### 📖 What it is
Everything a manager has to act on for their direct reports in one cycle. **Scope is entirely
server-side** — every read is narrowed to the calling manager's direct reports, so there is no employee
filter and nothing to leak. A 401 from these endpoints means *"you have no direct reports in this
cycle"*, not an expired session, which is why the screen shows an empty state rather than bouncing you
to the login page.

**Governance and execution are kept apart** on the overview tab, matching the backend's own split:
*whether the goal set is structurally complete* is a different question from *whether the goals are
going well*, and a manager needs the first answered before the second matters.

### 👁 On the page

Cycle picker, then **four tiles**: Direct reports **8** · Awaiting your approval · At risk ·
Unbalanced weights.

**Five tabs**

| Tab | What it lists |
|---|---|
| **Overview** | One row per direct report: Employee · **Governance** · Goals · Draft · Pending · Approved · At risk · Overdue · **Weight** |
| **Awaiting approval** | The flat goal table, with **Approve** and **Reject** on every row |
| **At risk** | The same table plus a **Risk** column carrying the reason |
| **Overdue** | Live goals past their due date |
| **Locked** | Goals frozen against change |
| **Progress** | Employee · Average progress · Goals · Not started · On track · At risk · Completed · Overdue |

> **Changed 2026-09-29** (performance closure lane L-a, decision D-29). **The Locked tab and the locked
> count read the lock itself** — the goal's *Locked* flag — and no longer a status nothing writes. **A
> locked goal can be overdue**: it is still running, so it stays on the Overdue tab and in the counts
> once its due date passes (the old lock's status used to hide it). **The weight total leaves rejected
> goals out**, as the set lock does — a rejected goal is not part of the set. The manager locks the set
> from this desk (lane L-c, below), or HR's advance past goal setting locks it (chapter 38).
>
> **Changed 2026-09-29** (performance closure lane L-b). **Where the template has a goals section, the
> lock fills it.** Locking a goal or the whole set — or HR's advance past goal setting — writes one row
> per locked goal onto the employee's appraisal (§ 1.4); unlocking a goal takes its row out again,
> with any *draft* score on it. Once a goal's row is scored in a **submitted** evaluation the section
> is settled: the unlock is refused — *"This goal has been scored in its appraisal, so it cannot be
> unlocked."* — and a later lock adds no row.
>
> **Changed 2026-09-29** (performance closure lane L-c). **The Overview tab has a *Lock set* button**
> on each report whose verdict is **Structurally complete**. It asks first — *"Lock this goal set?"* —
> and says what the lock means: the goals become fixed for the year (title, measure, target and
> weight), progress and check-ins still move them, and a goals section on the form is filled from the
> set. A report whose every live goal is locked reads **Set locked** instead. ~~The verdict does not
> check the cycle's minimum and maximum goal count; the lock does (chapter 18), and a refusal's toast
> names what is missing.~~ *(It does since slice B-w, below.)* A rejected goal keeps the verdict at
> *In progress*, so the button waits until the employee reworks or deletes it — although the lock
> itself, like HR's advance, leaves a rejected goal out.
>
> **Changed 2026-09-29** (performance closure lane B2, slice B-w). **The verdict reads the cycle's
> minimum and maximum goal count**, as the lock and the goal-setting gate do: a set of approved goals
> below the minimum reads **Below minimum**, above the maximum **Above maximum**, before the weights
> are looked at — one goal at weight 100 on a three-goal profile used to read *Structurally complete*
> and offer a *Lock set* the lock refused. The **Goals** cell adds *(min n)* or *(max n)* when the
> count is outside the bounds. On a cycle whose profile does not need the manager's approval, a goal
> is approved the moment the employee submits it, so nothing waits on this desk. No demo desk changes:
> Kojo Ansah and Efua Seidu hold three goals each, on a profile of three to six.

**The governance verdicts** (`TeamGovernanceStatus`), each with a hint under the badge:

| Verdict | Means |
|---|---|
| **Not started** | no goals in this cycle |
| **In progress** | one or more goals still Draft or Rejected |
| **Awaiting approval** | everything submitted, something still pending |
| **Below minimum** | fewer live goals than the cycle requires *(since B-w)* |
| **Above maximum** | more live goals than the cycle allows *(since B-w)* |
| **Invalid weight** | the set does not total 100 |
| **Structurally complete** | approved, within the bounds, balanced, done |

**Kwasi Danquah's eight direct reports**: Efua Seidu (3 goals), Patrick Appiah, Kwabena Sowah, Kwaku
Owusu, Yaw Bruce-Quaye, **Kojo Ansah** (3 goals), Mohammed Bruce-Quaye, Prince Nyaho — **six of whom
have no goals at all**, and therefore read **Not started**. *(Kojo Ansah's three came with the demo
scenarios' re-run on 2026-09-29: closure lane B1's gates hold the self-evaluation until the profile's
three goals are agreed.)*

### ▶ Walk it

1. In **window B**, sign in as `head.dev`.
2. Go to **Team Goals**. The **Overview** tab.
3. Read the governance column down the page.

   💬 *"Eight people report to the Head of Development. Kojo Ansah has a complete, balanced, approved
   set; Efua's is waiting on him — the goal she drafted a moment ago. Six have not started. That is not
   a report he had to ask for; it is the first thing the screen tells him, and 'not started' is a fact
   about counts and weights, not somebody's opinion."*

4. Point at Efua's **Weight — 100%**.

   💬 *"And the weight column is the other half of governance. A set that totals eighty is not a set —
   it is twenty per cent of somebody's year that nobody has claimed. The system will not stop it, and it
   will not let him not know about it."*

5. Go to the **Awaiting approval** tab. Efua's new goal from chapter 17 is sitting there.
6. 🔴 **LIVE WRITE 4 — approve it.** Click **Approve** on *Mentor one draughtsman to supervising grade
   readiness*. Add the feedback `Agreed — take Kwabena, and we will review at the January one-to-one.`

   Expect the row to leave the tab, and the *Awaiting your approval* tile to fall back to 0.

   💬 *"And that is the whole approval. No routing table, no definition, no configuration — his
   authority comes from the fact that the establishment says she reports to him, read from her record
   at the moment he pressed the button. If I signed in as HR and tried it, I would get a 403."*

   ⚠ **Kojo Ansah's row has carried a *Lock set* button since you arrived, and Efua's carries one now**
   — both sets are complete and, on the demo database, not locked (lane L-c). **Do not press it here.**
   A lock is not one of Appendix E's writes, and a locked goal cannot be deleted, which is how writes 3
   and 4 are put back. If it is pressed, unlock each goal (Employee Goals → **⋯** → **Unlock**) before
   the reset.

7. *(Optional, worth 20 seconds.)* Show **Reject** on another goal without confirming, to make the
   point that the feedback box is mandatory: *"The API refuses a rejection with no explanation, so the
   button waits for one."*

8. Go to the **At risk** tab and read one **Risk** reason aloud.

### ⚙ Behind the page

`api/performance/team-goals` — **read-only governance queries**, always scoped server-side to the
calling manager's direct reports. There are **no mutations here**: approve and reject call the
employee-goal endpoints.

| Tab | Endpoint |
|---|---|
| Overview | `GET …/overview/{cycleId}` |
| Awaiting approval | `GET …/awaiting-approval/{cycleId}` |
| At risk | `GET …/at-risk/{cycleId}` |
| Overdue | `GET …/overdue/{cycleId}` |
| Locked | `GET …/locked/{cycleId}` |
| Progress | `GET …/progress/{cycleId}` |

`daysRemaining`, `isOverdue` and the risk fields are **all computed server-side against a UTC clock** —
nothing on the client recomputes them from the dates.

All six tabs are fetched together on arrival: they are cheap reads and a manager flips between them
constantly, so paying for all of them up front beats a spinner on every click.

### ⚠ Known gaps
**P-23.** `getGoalDetail` answers **404** for both "no such goal" and "not your report" —
indistinguishable on purpose, so the endpoint cannot be used to probe who reports to whom.

---

## 20. `/hr/performance/at-risk` — the organisation-wide risk report

### 📍 Where you are
**Route:** `/hr/performance/at-risk` · Human Resources → Talent & Performance → Performance → Goals At Risk
**As:** `hr.head` · **3 minutes**

### 📖 What it is
HR's counterpart to the manager workspace's at-risk tab, with the manager scope removed. **Same two
rules, same thresholds; only the population differs.** Rows arrive sorted by severity, so the top of
the list is where to start.

### 👁 On the page

Cycle picker, a link out to **Goal Risk Thresholds**, then **four tiles**:

| Tile | Expect today |
|---|---|
| Goals at risk | **~16** |
| High severity | **0** |
| Already overdue | **0** |
| Employees affected | **9** *(with average progress as the hint)* |

A filter card: **Organisation level** and **Organisation unit** pickers, with the note *"Filters match
the employee's own unit and level, not the goal's."*

**The table:** Employee · Goal · Weight · Status · Progress · **Risk** · Due — where the Risk cell
carries the *reason string the evaluator produced*, not a generic badge.

The three reasons you will see:

| Reason | Severity | Fired by |
|---|---|---|
| Low progress near deadline | 70 | within 14 days of due **and** below 60% |
| Progress behind expected timeline | 60 | progress + 20 < the straight-line expectation |
| *(manually flagged)* | 50 | a manager ticked "at risk" on a progress entry or check-in |

### ▶ Walk it

1. Open **Goals At Risk**. Read the tiles.

   💬 *"Sixteen goals across nine people. Zero of them high-severity, none overdue — so nothing is in
   crisis, but sixteen things are drifting."*

2. Sort your eye down the **Risk** column and read two different reasons aloud.

   💬 *"And look at the reasons, because they are not the same reason. Most of these say 'progress
   behind expected timeline' — that is the system drawing a straight line from the goal's start date to
   its due date and noticing that reality is below the line by more than the tolerance HR set. But
   Efua's turnaround goal says something different. That one a human flagged, in a one-to-one, in
   September."*

3. Point at the **Goal Risk Thresholds** link in the header.

   💬 *"And the rule that produced all of this is one screen away, owned by HR, editable without a
   developer. That is the difference between a dashboard and a control."*

4. *(If the room is analytical.)* Do the arithmetic out loud, as in chapter 11.

### ⚙ Behind the page
`GET api/performance/goals-at-risk/{cycleId}` with optional `organizationUnitId` / `organizationLevelId`.
**HR/Admin only** — a non-HR caller gets the empty state *"This report is restricted to HR and admin
roles."* Rows are sorted by `riskSeverityScore` descending, server-side.

The thresholds come from `GET api/performance/goal-risk-settings`, evaluated by `GoalRiskEvaluator` —
pure, stateless, no database access.

### ⚠ Known gaps
**P-24.** The filters match the **employee's** unit and level, not the goal's. On a matrixed
organisation those differ, and the screen says so in small print rather than offering both.
**P-25.** There is no export. The list is read on screen or not at all.

---

# PART IV — RUNNING THE YEAR

*Twelve minutes. Everything between goal setting in January and the year-end run in November. This is
the part that separates a performance system from an appraisal form.*

---

## 21. `/me/performance/check-ins` — the one-to-ones, and what they change

### 📍 Where you are
**Route:** `/me/performance/check-ins` and `…/check-ins/[id]`
**Portal path:** My Self-Service → Performance → My Check-Ins
**As:** `head.dev` · **4 minutes** — 🔴 **LIVE WRITE 5 lives here**

### 📖 What it is
One-to-ones and interim conversations held during a cycle — and **the goal updates that come out of
them**, which is the substantive part.

**Recording a goal update here moves the goal itself.** The percent, the reported status and the
at-risk flag are applied to the `EmployeeGoal`, so a goal flagged in a one-to-one turns up in the
at-risk reports without anyone re-entering it. A goal that is not live — draft, awaiting approval,
locked, already complete — keeps its status and only the note is kept.

**Two lists, not one**, because "check-ins about me" and "check-ins I run" are different jobs: the
first is a record of conversations you have had, the second is a queue of ones you still need to hold.
Both read from `/me` routes, so neither needs an employee id.

### 👁 On the list page
Two tabs — **Check-ins I run** and **Check-ins about me** — a cycle filter, a **Schedule a check-in**
button, and a table: **Check-in · Employee · Scheduled · Status**.

**The schedule dialog** picks from the caller's **own direct reports** (not the organisation-wide
employee search, which needs a permission a plain manager may not hold — and a check-in you run is with
one of your own people anyway): Employee · Title · Type · Scheduled for · Agenda.

On this database `head.dev` has **one** check-in: *Q3 one-to-one — Community 25 progress*, with Efua,
**held on 14 September**.

> ⚠ **On this database its shared and private notes are empty**, and its held date is the day the demo
> scenarios last ran, not 14 September — the scenario sent a field name the endpoint drops (fixed
> 2026-09-29). Steps 5 and 6 below read those notes: re-run demo scenario 060, which records the
> check-in as held again with its notes, or rebuild (Book 0's evening-before rebuild does).

### 👁 On the detail page

| Block | What is in it |
|---|---|
| **Header** | Title, `type · with <employee> · held by <conductor>`, and either a **Held <date>** badge or a **Record as held** button |
| **Three fields** | Scheduled · Cycle · Follow-up |
| **Objectives this was about** | Which of the cycle's company goals the conversation served. **Replace-set** — each save sends the whole list. Read-only once held |
| **Agenda** | Free text, set when the check-in was scheduled |
| **Shared notes** / **Action items** / **Private notes** / **Employee comments** | Four cards, shown once held. **Private notes reach only whoever conducted the check-in, and HR** — the server leaves them out of every other reader's copy, and always out of the subject's, even when the subject is an HR officer (closure lane P, 2026-09-29) |
| **Goal updates** | Goal · Progress · Status, with an **Update a goal** button — and the standing line *"Recording an update here moves the goal itself, so it shows up in progress and at-risk reporting."* |

**The "Record as held" dialog** carries **Shared notes** *("What you both agreed. The employee can see
this.")*, **Action items**, **Private notes** *("Your own record.")* — and warns: *"These fields are
replaced wholesale each time, not merged — paste back anything you want to keep."*

> **Changed 2026-09-29** (closure lane P, P6). **Record as held** is offered to the conductor and to
> HR, never to the check-in's subject (the subject sees *Not yet held*), and the **Private notes**
> field to the conductor alone — the server writes private notes only from the conductor, so HR
> closing a meeting it did not hold cannot overwrite them. Who the check-in is about and who holds it
> are fixed once it exists, and it can be opened only about yourself, a direct report, or — for HR —
> anyone in the tenant.

**The "Update a goal" dialog**: Goal *(the employee's own, for this cycle)* · Progress % · Status ·
Note · and a **"This changes the goal"** confirmation line.

### ▶ Walk it

1. In **window B** as `head.dev`, open **My Self-Service → Performance → My Check-Ins**.
2. The **Check-ins I run** tab has one row. Open it.
3. Read the header: *Q3 one-to-one — Community 25 progress · One-to-one with Efua Seidu · held by
   Kwasi Danquah · **Held 14 Sep 2026***.
4. Show the **Objectives this was about** panel.

   💬 *"Small feature, large consequence. He recorded which of the Corporation's objectives this
   conversation actually served. Do that all year and you can answer 'we met every fortnight — about
   what, exactly?' with a record instead of a memory."*

5. Read the **Agenda**, then the **Shared notes** and **Action items**.
6. Point at **Private notes**.

   💬 *"And that box is his. When Efua opens this check-in, the server does not send it to her at
   all — not hidden on her screen, absent from what her browser receives. That is a rule in the code,
   not a convention."*

7. Scroll to **Goal updates**. Three rows: the layout at 70% *On track*, the turnaround at 55%
   *At risk*, the certification at 40% *In progress*.

   💬 *"And this is the join. Three goals discussed, three figures agreed — and those figures did not
   stay in the meeting note. They went onto the goals themselves. The 'at risk' flag you saw on HR's
   organisation-wide report an hour ago was set here, in this conversation, by this manager."*

8. 🔴 **LIVE WRITE 5 — schedule the next one.** Go back to the list, click **Schedule a check-in**.
   Employee **Efua Seidu**; Title `Q4 one-to-one — Phase 3 handover and the Civil 3D examination`;
   Type **One-to-one**; Scheduled for a date next week; Agenda `Phase 3 layout team; November
   examination; December leave cover.` Save.

   Expect the toast **"Check-in scheduled"** and a second row in the tab, unheld.

   💬 *"And she is notified. Not by an email somebody remembered to send — by the system, because a
   check-in has a subject and the subject is told."*

### ⚙ Behind the page

| Control | Endpoint | Note |
|---|---|---|
| I run / About me | `GET api/CheckIns/me/conducting`, `GET api/CheckIns/me` | both token-scoped |
| Direct reports picker | `GET api/PerformanceAppraisals/manager/me/direct-reports` | self-armed |
| Create | `POST api/CheckIns` | **422 when the cycle's settings profile has check-ins switched off** — a policy decision, not a permission problem |
| Record as held | `POST api/CheckIns/{id}/complete` | stamps it held, **now**; the three note fields are replaced wholesale |
| Goal updates | `GET/POST/PUT/DELETE api/CheckIns/{id}/goal-updates[/{id}]` | writes `CheckInGoalUpdates` **and applies the figures to `EmployeeGoals`** |
| Objectives | `PUT api/PerformanceLinks/check-ins/{id}/objectives` | **replace-set**; `[]` clears it |
| Attachments | `GET/POST/DELETE api/CheckIns/{id}/attachments[/{id}]` | a file is removed by whoever attached it, or HR — never as the check-in's subject — and only until the check-in is held (closure P9) |
| Goal updates, ownership | `POST …/goal-updates` | **only a goal of the check-in's subject** (404 otherwise); an edit never moves an update to another check-in or goal (closure P7 — anyone could open a check-in about themselves and move a colleague's goal through it) |

> **Deleting a goal update does not undo what it did.** The toast says so: *"The goal keeps the figures
> this update put on it."* That is honest rather than convenient — the goal has moved on, and rolling it
> back would be a second, unrecorded change.

### ⚠ Known gaps
**P-26.** There is exactly **one** check-in on this database, so the "I run" tab has one row and the
"About me" tab is empty for everyone except Efua.
**P-27.** ~~`privateNotes` come back on **every** read of a check-in, including the employee's. The client
hides them; the API does not.~~ **Fixed on every path** — by id, the employee's and the conductor's
lists, by employee, by conductor, upcoming, and HR's paged list, which was never redacted (closure lane
P, P15; `run-final-privacy.mjs`).

---

## 22. `/me/performance/journal` — the evidence notebook

### 📍 Where you are
**Route:** `/me/performance/journal` · My Self-Service → Performance → My Journal
**As:** `head.dev`, then briefly `staff` · **3 minutes**

### 📖 What it is
Evidence gathered **as it happens**, so the year-end appraisal is not written from memory.

Three views, because there are genuinely three jobs:

* **My journal** — my own notes, **private by default**. Nobody else can read a private one, **including
  HR**; sharing is a deliberate act, per entry.
* **About my team** — the notes I keep on one direct report. They are mine, so my private ones appear.
* **Team journal** — everything visible to me as a manager: my notes about reports, **plus whatever they
  have chosen to share**. Previews only; it is for scanning.

**Nothing on this screen sends an employee id for the caller.** Every route derives it from the token —
the id-bearing versions of these routes were how anyone could read anyone's private journal.

### 👁 On the page

Three tabs, a cycle filter, and a **New entry** button.

Each entry is a **card collapsed to its title**, carrying a **Private** / **Shared** badge — deliberately
a badge rather than an icon, because someone scanning their journal needs to see at a glance which
notes their manager can read. Expanding shows the body. Actions: **Share** (or **Unshare**), **Edit**,
**Delete** — all author-only.

The **Team journal** tab adds a person filter and two toggles — **My notes** / **Shared with me** — so a
manager can tell *what I observed* from *what they told me*. A **Private** badge there marks the
manager's own notes the subject cannot see; a report's private entries are **not in the response at
all**.

**The entry dialog**: Appraisal cycle · **About** *(Myself, or a direct report)* · Related goal
*(optional)* · Title · Notes · and a privacy switch that spells out the consequence either way rather
than labelling itself "private" and leaving the reader to infer who that excludes.

**On this database:** four entries, all dated 14 September 2026 —

| Owner | About | Title | Privacy |
|---|---|---|---|
| Kwasi Danquah | Efua Seidu | Handled the Community 25 boundary dispute well | **Shared** |
| Kwasi Danquah | Efua Seidu | Stood in at the Development Control meeting | **Shared** |
| Kwasi Danquah | Efua Seidu | Late issue of the amended block plans | **Private** |
| Efua Seidu | *(herself)* | Civil 3D — modules 1 to 4 done | **Private** |

### ▶ Walk it

1. As `head.dev`, open **My Journal** → **About my team**, and pick **Efua Seidu**.
2. Expand *Handled the Community 25 boundary dispute well* and read two sentences of it aloud.

   💬 *"May the fourteenth. She went to site with the Lands Section and the affected allottee and came
   back with a signed acceptance of the revised boundary. It saved the Corporation an arbitration, and
   she did it without being asked. That is a sentence written in May and read in November — which is the
   entire point. Appraisals written from memory are appraisals about the last six weeks."*

3. Expand *Late issue of the amended block plans* and point at its **Private** badge.

   💬 *"And this one is private. He recorded it because it matters to her turnaround goal, and he has
   not shared it — so she has not seen it, and neither has HR. Not 'HR can see it if they ask'. HR
   cannot read it at all. Only the author's own view returns a private entry."*

4. *(Optional, 30 seconds.)* Switch window B to `staff` and open **My Journal**. Efua's own private
   note is there; the manager's three are not.

   💬 *"And from her side, she sees hers. His notes about her are his."*

### ⚙ Behind the page

| View | Endpoint |
|---|---|
| My journal | `GET api/PerformanceJournal/mine` — **includes my private entries** |
| About one report | `GET api/PerformanceJournal/about/{subjectId}` — my notes about them, private ones included, because I wrote them |
| Team journal | `GET api/PerformanceJournal/team` |
| Someone else's journal, as their manager or HR | `GET api/PerformanceJournal/by-owner/{ownerId}` — **private entries are never included** |
| Share / unshare | `PATCH api/PerformanceJournal/{id}/privacy` — **author only** |

> Share/unshare being author-only is not politeness — it closes a read hole by another route. Anyone
> able to un-private somebody's entry could then read it through the shared views.

**Two refusals worth knowing:** creating a **private** entry is **422** when the cycle has
`enablePrivateJournal` switched off; and writing a note **about** somebody who does not report to you is
**403**.

### ⚠ Known gaps
**P-28.** **Every journal entry on this database is dated 14 September 2026**, including the three that
were written to be dated months apart. The entry date supplied on create is ignored and the server
stamps the creation date. Do not build a "written in May, read in November" line around the *date on
screen* — say the date out loud from the body text, which does carry it.

---

## 23. `/hr/performance/conversations` — the formal meetings

### 📍 Where you are
**Route:** `/hr/performance/conversations` and `…/conversations/[id]`
**Sidebar:** Human Resources → Talent & Performance → Performance → Conversations
**As:** `staff`, then `head.dev` · **3 minutes** — 🔴 **LIVE WRITE 7 lives here**

### 📖 What it is
The scheduled meetings that punctuate an appraisal cycle — kick-off, quarterly, mid-year, final review.
The **agenda** before; the **notes** after.

The cycle's settings profile says which of them are **required**: on this profile, kick-off, mid-year
and final are all on.

**Completing is a one-way door.** It stamps the held date and notifies the employee that the notes are
up, and a completed conversation can no longer be edited — which is the point, since the notes are then
the record of what was said.

### 👁 On the page

Two tabs:

* **My diary** — *"everything I scheduled or am down to hold and have not yet held"*, including
  **overdue** ones, which are flagged rather than filtered out because they are the rows that need
  action. **Empty on this database — see Rule 6.**
* **About me** — conversations about the signed-in employee.

Table: **Conversation · Employee · Scheduled · Status**, with **Overdue** / **Scheduled** / **Held**
badges and an **Open** link.

### 👁 On the detail page
* Header with an **Open the appraisal** link and a **Held and recorded** badge once complete
* **Before the meeting** — Type · Scheduled for · **Agenda** *("What will be covered, so nobody walks
  in cold")*
* **After the meeting** — **Notes** *("What was actually discussed")* · **Key takeaways** *("The two or
  three things to act on. These go into the notification the employee gets.")*
* **Save** while open; **Record as held** to complete

**On this database:** fourteen conversations, **all completed** — five kick-offs dated 10 February 2026
and five mid-years dated 14 July (one of each per track), and four final reviews in September (the
three finished appraisals, and Kwasi's ahead of his live sign-off). *(It was eight — five kick-offs and
three final reviews — until closure lane B1 made the mid-year a condition of the self-evaluation. Since
slice B-w it is a condition of the manager's evaluation instead.)*

> ⚠ **On this database, until it is rebuilt, the kick-off notes and the three finished appraisals'
> final-review notes are empty** — the demo scenario sent field names the endpoint drops, and a held
> conversation cannot be completed again (fixed in the scenario 2026-09-29). Steps 2 and 3 below read
> them, so rebuild before walking this chapter (Book 0's evening-before rebuild does). The mid-years
> and Kwasi's final review have theirs.

### ▶ Walk it

1. In **window B** as `staff`, open **Conversations** → **About me**. Three rows: a **Kick-off** in
   February, a **Mid-year** in July and a **Final review** in September. *(Changed 2026-09-29 —
   closure lane B1 holds the self-evaluation until both setup conversations are held, so the demo
   holds a mid-year for every track. It was two rows before.)*
2. Open the **Kick-off**.

   💬 *"The tenth of February. Goals agreed and weighted, evidence sources named for each — the scheme
   file, the request log, the training record. That meeting is not a diary entry; it is the record of
   what was agreed, and it sits on her appraisal for the rest of the year."*

3. Open the **Final review**. Read the *Notes* and the *Key takeaways*.

   💬 *"And the same meeting at the other end of the year: her manager's assessment, and what they
   agreed. Her own answer is not typed in here by someone else — she gives it herself, on the appraisal,
   when she acknowledges it."* *(Changed 2026-09-29: a conversation holds only the manager's notes and
   takeaways; the employee's comment is her acknowledgment, chapter 32.)*

4. Switch to `head.dev`, open **My diary**, and expect it to be **empty**. Say so.

   💬 *"His diary is empty, and that is correct — every meeting he owed has been held. The diary is
   deliberately 'scheduled or down to hold and **not yet** held', because a list of things you have
   already done is not a to-do list. Let me put something in it."*

5. 🔴 **LIVE WRITE 7 — schedule one.** Conversations are created **from inside an appraisal**, not from
   this list — a conversation has no meaning without one. So: open **Team Appraisals → Efua Seidu →
   Conversations tab**, click **Schedule a conversation**, choose Type **Quarterly Q4**, a date next
   week, Agenda `Q4 review: Phase 3 layout team, the Civil 3D examination result, and next year's
   goals.` Save.

   Expect the toast **"Conversation scheduled — the employee has been notified."**

6. Come back to **Conversations → My diary**. The new row is there.

   💬 *"And now his diary has something in it — and so does hers, and she was told."*

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| My diary | `GET api/AppraisalConversations/my-diary` |
| About me | `GET api/AppraisalConversations/mine` |
| By appraisal *(the panel)* | `GET …/by-appraisal/{appraisalId}` |
| Schedule | `POST api/AppraisalConversations` — `conductedById` left null so the server stamps the scheduler; **the type is required** *(since B-w — a body without one booked a kick-off)* |
| Amend | `PUT …/{id}` — **422 once completed**; the type is required, and the conversation stays on its own appraisal *(since B-w — the body's appraisal id used to move it)* |
| Complete | `POST …/{id}/complete` — the **only** way one closes; stamps the held date and notifies |

Anything keyed on an id is **403** unless the caller is HR, the appraisee, the appraisee's manager, or
whoever scheduled or is holding the meeting.

> **Changed 2026-09-29** (performance closure lane B1). **A held conversation can move the appraisal
> on.** The kick-off and mid-year conversations the profile requires are part of *goal setting*: the
> self-evaluation is refused until they are held. The final review conversation stands before the
> acknowledgment, and on a cycle with no acknowledgment, **recording it as held completes the
> appraisal** and settles its score — it used to leave it in Governance for good. A conversation held
> on a Draft appraisal does not open it.
>
> **Changed 2026-09-29** (performance closure lane B2, slice B-w). **The mid-year holds the manager's
> submission**, not the self-evaluation: while it is missing the appraisal waits at *Manager
> Evaluation*, the manager can save drafts, and the submit is refused naming the mid-year. **A
> conversation must say which it is** — the type decides which gate it opens, and a request without
> one used to book a kick-off.

### ⚠ Known gaps
**P-29.** *My diary* is empty for every persona on this database (Rule 6).
**P-30.** The conversations **list** cannot schedule — only the panel inside an appraisal can. Correct
design, but it means an empty diary offers no way forward from the screen you are on.

---

## 24. `/hr/performance/interim-reviews` — the mid-year checkpoints

### 📍 Where you are
**Route:** `/hr/performance/interim-reviews` and `…/interim-reviews/[id]`
**Sidebar:** Human Resources → Talent & Performance → Performance → Interim Reviews
*(the same page under `/me/performance/interim-reviews` shows only the employee's own scope)*
**As:** `head.dev` · **3 minutes**

### 📖 What it is
The quarterly and mid-year checkpoints inside an appraisal cycle.

**These are generated, not created.** How many exist, and whether each is a light-touch conversation or
a **scored appraisal of the period**, comes from the cycle's `reviewFrequency` and `interimReviewDepth`
settings, and is written when the cycle generates its appraisals. An empty list usually means the cycle
is set to `None`, or that appraisals have not been generated — not that anything is broken. Only
`Custom` expects HR to add them by hand.

On this profile: **frequency = mid-year only**, **depth = light touch**. So there are **107** checkpoints,
one per appraisal, all of type *Mid-year review*, all light-touch.

### 👁 On the list
Two tabs — **My team's** and (under `/me`) **Mine** — and a table: **Employee · Checkpoint · Cycle ·
Date · Depth · Period score · Status**.

### 👁 On the detail page

**Two shapes, one screen.** A light-touch event runs **submit → complete**. A full appraisal
(`isFullAppraisal`, set from the cycle's depth) runs **submit → finalize**, which scores the period's
goals and produces a weighted `overallPeriodScore`.

Four tabs:

| Tab | What is in it |
|---|---|
| **Overview** | *Employee self-assessment* — **What went well** / **What got in the way** / submitted date; then *Manager close-out* — review notes and manager notes. A banner reads **"This is a full interim appraisal"** where applicable |
| **Goal progress** | Goal · Progress · Status · Notes · Recorded by · When, with a **Record goal progress** form |
| **Scoring** | Only on a full interim appraisal: score the period's goals, with a live **period score** preview |
| **Evidence** | Attachments — File · Description · Uploaded by · When |

### ▶ Walk it

1. As `head.dev`, open **Interim Reviews**. His reports' mid-year checkpoints are listed.
2. Point at the **Depth** column — every row reads *Light touch*.

   💬 *"One checkpoint per person, mid-year, light touch — and that is a decision taken once, on the
   settings profile, not a form somebody had to build. Switch that profile to quarterly and this
   becomes four checkpoints each. Switch the depth to 'full appraisal' and each one becomes a scored
   assessment of the period, with its own weighted score."*

3. Open one and walk the four tabs without writing anything.
4. Point at the **Goal progress** tab.

   💬 *"And exactly as in the one-to-one: progress recorded here moves the goal. A goal flagged at risk
   at a mid-year review turns up in the at-risk report without anybody re-entering it."*

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| Mine / team | `GET api/AppraisalReviewEvents/mine`, `…/team` — both token-scoped |
| HR org-wide | `GET …/by-cycle/{cycleId}` — **HR only**; a manager builds a team view from `by-appraisal` per report |
| Employee submits | `POST …/{id}/submit` |
| Manager closes | `POST …/{id}/complete` |
| Full appraisal | `GET …/{id}/full-appraisal-context`, `POST …/{id}/finalize-full-appraisal` |
| Progress | `POST …/{id}/progress` — records the entry **and moves the goal** |
| Attachments | `GET/POST/DELETE …/{id}/attachments` — through the scan gate |

**Two 422s carry the useful message**: the cycle's settings may require a **self-assessment** before the
manager can close, and may require a **progress update on every live goal** before either side can move
— and the message names how many are missing.

> **On a full interim appraisal, weights are relative to the goals in the list, not to 100.** A period
> with two goals weighted 30 and 10 divides by 40, so a single goal scored 80 yields 80, not a diluted
> 24.

> **Changed 2026-09-29** (performance closure lane L-b, L7). **A full interim appraisal scores the
> locked goal set only.** Its scoring tab listed every goal of the cycle — drafts and rejected goals
> included — and the finalise scored whatever it was sent. Both now take the employee's agreed, locked
> goals, and a finalise that names any other goal is refused with 422.

### ⚠ Known gaps
**P-31.** All 102 live checkpoints sit at **Pending** — nobody has submitted one. The *Awaiting manager*
tile on the cycle's Interim reviews tab therefore reads 0, and the detail pages open with empty
self-assessments. Fine to show; do not promise a populated mid-year.
**P-32.** `/me/performance/interim-reviews` is **not on the portal menu**. It exists and works, but the
employee has to be given the URL.

---

## 25. Development plans — `/hr/performance/development-plans` and `/me/performance/development-plans`

### 📍 Where you are
**Routes:** `/hr/performance/development-plans` *(HR's register)* · `/me/performance/development-plans`
and `…/[id]` *(the working surface)*
**Sidebar:** Human Resources → Talent & Performance → Performance → Development Plans
**Portal:** My Self-Service → Performance → My Development Plans
**As:** `hr.head`, then `head.dev` · **3 minutes**

### 📖 What it is
What an appraisal's *areas for improvement* and *training needs* turn into: a plan, its objectives, and
the manager's running commentary on them.

**The split is deliberate.** The desk register is HR's organisation-wide, paged view and the place to
draft a plan for **any** employee. The detail page — where objectives are worked, progress recorded and
feedback added — lives in the portal, because the subject updates it.

**Draft is a real state.** A draft plan is invisible in the sense that matters: nothing has told the
employee about it. **Activating raises the notification.**

### 👁 HR's register
Table: **Plan · Employee · Period · Objectives · Progress · Status**, and a **New development plan**
dialog — Employee · Title · Appraisal cycle *(optional)* · Starts · Ends *(optional)* · Notes. A plan
created here is deliberately born a **Draft**: *"a plan the employee has not agreed to is not yet theirs
to work on."*

**On this database: three plans, all Active.**

| Plan | Employee | Objectives | Feedback |
|---|---|---|---|
| Efua Seidu — development plan 2026 | TDC/00017 | 3 | 2 |
| Cynthia Sarpong — development plan 2026 | TDC/00081 | 2 | 1 |
| Kojo Ansah — first-year development plan | TDC/00063 | 2 | 1 |

### 👁 The portal detail page
Four tiles — **Objectives · Completed · Average progress · Feedback entries** — a *"Still a draft — the
employee has not been told"* banner where applicable, and three tabs:

| Tab | What is in it |
|---|---|
| **Objectives** | Objective · Target · Progress · Status, with add / edit / remove and a **Record progress** dialog (Progress % · Status · Notes — *"Left blank, the previous note stands"*) |
| **Feedback** | The manager's running commentary. Adding one **notifies the employee**. Types: General comment · Progress update · Mid-cycle review · Risk flag |
| **Notes** | The plan's own overall notes |

The objective dialog: Title · **What good looks like** · **Actions agreed** *("Training, mentoring,
stretch work — whatever was agreed")* · Target date · Status.

> **Changed 2026-09-29** (closure lane P, P10). A plan records **who wrote it**. The employee may
> complete, cancel or delete only a plan they wrote themselves; on one their manager or HR set for
> them, the status picker offers neither Completed nor Cancelled and the server answers 403 — they work
> it, the manager closes it. A plan written before that date has no author and counts as set for the
> employee (the three on this database). The plan's edit route no longer moves a plan to another
> employee or changes its status; status moves only through the picker's own route.

Alongside it, on the employee's own plan, sits **Development skill suggestions** — competencies this
person's *goals* say they need, each listed with the goals that asked for it. Not generic
recommendations: somebody ticked "development needed" on a real goal.

### ▶ Walk it

1. As `hr.head`, open **Development Plans**. Three rows.

   💬 *"HR's register is the whole organisation. Notice it does not let HR work the plan — the rows link
   into the portal, because the person the plan is about is the person who updates it."*

2. Click **Efua Seidu — development plan 2026**. You land in the portal shell.
3. On **Objectives**, read the three: the Civil 3D certification, leading the Phase 3 layout team of
   three draughtsmen, and the supervision short course.

   💬 *"And look where they came from. 'Complete the Civil 3D certification' is one of her goals.
   'Lead the Phase 3 layout team' is her manager's 'areas for improvement' — supervision of a larger
   team. The development plan is not a separate exercise; it is what the appraisal concluded, written
   down as work."*

4. Go to **Feedback** and read the mid-cycle review note.

   💬 *"'The supervisory objective has not started because Phase 2 took the whole of the third quarter;
   it moves to Q1 next year with the Phase 3 team.' That is the manager's own commentary, dated, on the
   record — and she was notified when he wrote it."*

### ⚙ Behind the page

| Control | Endpoint | Who |
|---|---|---|
| HR register | `GET api/DevelopmentPlans/paged` | **HR only — 403 for everyone else** |
| Mine / my team | `GET api/DevelopmentPlans/mine`, `…/my-team` | token-scoped |
| Anything keyed on an id | `GET api/DevelopmentPlans/{id}` | HR, the employee, or that employee's line manager |
| Activate | `PATCH api/DevelopmentPlans/{id}/status` | **activating is what tells the employee the plan is theirs** |
| Objectives | `…/{planId}/objectives[/{id}]`, `PATCH …/progress` | |
| Feedback | `api/DevelopmentPlanFeedback` | author from the token; **`managerId` in the payload is ignored**; adding notifies the employee; only its author or HR may withdraw it |
| Skill suggestions | `GET api/PerformanceLinks/employees/{id}/cycles/{cycleId}/skill-suggestions` | |

Business rules answer **422** with a readable message: a completed plan cannot be edited, a shared plan
cannot go back to draft.

### ⚠ Known gaps
**P-33.** Creating a plan for **yourself** from the portal makes it **Active** immediately — you do not
need to agree with yourself. Creating one for a report makes it a **Draft**. Same button, two outcomes,
and the screen does not say which you are about to get.

---

# PART V — THE YEAR-END RUN

*Twenty-five minutes, and the heart of the demonstration. Six actors in sequence, one appraisal moving
between them.*

**The sequence, and who owns each step:**

```
  employee nominates peers  →  employee self-evaluates  →  peers evaluate  →  manager evaluates
     →  CALIBRATION PANEL (the gate)  →  HR signs off  →  employee acknowledges  →  appeal
```

**The two appraisals you will use:**

* **Efua Seidu** (`staff`) — **finished**. Score 88.56, acknowledged. Use her for *reading* a completed
  appraisal, and for the live appeal.
* **Kwasi Danquah** (`head.dev`) — **with HR**. Self 92.20, peers 80 and 76, manager 88.80, calibrated,
  no final score yet. Use him for the live sign-off.

---

## 26. `/me/performance/appraisals` — the employee's own view

### 📍 Where you are
**Route:** `/me/performance/appraisals` and `…/appraisals/[id]`
**Portal:** My Self-Service → Performance → My Appraisals
**As:** `staff` *(Efua Seidu)* · **4 minutes**

### 📖 What it is
Every appraisal the signed-in employee is the **subject of**. Scoped to *your own* on purpose: peer
feedback you owe on other people's appraisals is a separate screen, because those rows carry a
colleague's scores and do not belong under a heading that says "mine".

**`Next step` and `Due` come from the server**, read off the appraisal's status and the cycle's
deadlines — so what the row asks you to do is always the current step, never a guess made in the
browser.

### 👁 On the list
Three tabs — **All · In progress · Completed** — a link to *Peer reviews I owe*, and a table:
**Cycle · Period · Status · Next step · Score**, with **Nothing from you** where no action is owed.

Efua has **one** row: *Annual Performance Cycle 2026*, **Completed**, **89.4**.

### 👁 On the detail page

**Header:** the cycle name, `APR-2026-APC2026-TDC/00017-0034 · 01 Jan 2026 – 31 Dec 2026`, a status
badge, and — while the self-evaluation is still open — a **Self-evaluation** button.

**The phase rail**, immediately under it. This is the *computed* phase, not the stored status: the
stored status lumps everything between opening and HR sign-off into `Active`, so on its own it cannot
say whether the appraisal is waiting on the employee, their peers or their manager. The phase can.
Steps the cycle does not require are **dropped** from the rail rather than greyed out.

**Banners, depending on where it is:**

* **"Your appraisal has been finalised"** — with an **Acknowledge** button, and the warning *"You cannot
  undo this."*
* **"Disagree with the outcome?"** — with **File an appeal** and **My appeal**, shown once the appraisal
  is Completed

**Four tiles:** Self-evaluation · Peer reviews · Manager evaluation · **Final score**

**Four tabs:**

| Tab | What is in it |
|---|---|
| **Overview** | *How this appraisal is scored* — the three weights, stated as percentages, from the cycle's own profile |
| **My evaluation** | The submitted self-evaluation, read-only, with scores **as stored** — grade and achievement resolved by the server against the frozen snapshot, not recomputed here |
| **My peer nominations** | Only when the cycle nominates in *Employee* mode. Nominate, see who was approved |
| **Outcome** | Empty until HR finalises. Then: **Final score · Finalised · Acknowledged**, **How the score was made up** (Self / Peers / Manager), **HR remarks**, and **Your response** |

> **Changed 2026-09-29** (closure lane P, P2). **The outcome is released to the employee, not merely
> hidden from them.** Until it is released — Completed or Closed, or in governance with every required
> calibration committed and HR's sign-off given — the server sends the employee's own copy of their
> appraisal, their lists, their trend and the HR review **without** the overall and calibrated scores,
> the grade, the ranks, the manager's recommendations and narrative, the manager's and peers' scores
> and HR's remarks; each carries `outcomeReleased: false`, and *My Appraisals* shows *"Not yet
> released"*. Their own self-evaluation score is theirs throughout. HR and the manager see everything
> as before. With anonymous peer reviews on, no payload of the employee's names a peer.

### ▶ Walk it

1. In **window B**, sign in as `staff`. Go to **My Self-Service → Performance → My Appraisals**.
2. One row. Open it.
3. Point at the **phase rail** — every step ticked through to *Complete*.

   💬 *"That rail is not a stored field. Every time this page loads, the system works out where the
   appraisal actually is from what has been submitted and which steps this year's policy requires. A
   cycle without peer reviews never shows a peer step at all — it is not greyed out, it is not there,
   because implying she is stuck at a step that will never happen would be a lie."*

4. Read the four tiles, ending on **Final score 89.4**.
5. Go to **Overview** and read the three weights.

   💬 *"And she is told how her own score was made. Ten per cent her own assessment, twenty per cent her
   peers, seventy per cent her manager. That is not buried in a policy document; it is on her appraisal,
   because the cycle's profile said employees may see the breakdown."*

6. Go to **My evaluation** and scroll the stored scores.
7. Go to **Outcome**. Read **How the score was made up** — Self **94.4**, Peers **82.0**, Manager
   **90.8** — then the **HR remarks**, then **Your response**.

   💬 *"Ninety-four and a half from her, eighty-two from her peers, ninety-one from her manager, and
   eighty-nine point four as the weighted result. And underneath, her own written response — 'the
   plotter was out of service for three weeks in June and the service requisition sat with Procurement,
   which is what cost the turnaround goal'. She wrote that. Not HR on her behalf — the route that
   records an employee response refuses anybody but the appraisee."*

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| The list | `GET api/PerformanceAppraisals/my-appraisals/me` — `cycleFilter` accepts `open` or `completed` |
| The rail | `GET api/AppraisalWorkflow/{id}/phase` — computed live, never stored |
| Self-evaluation context | `GET api/PerformanceAppraisals/{id}/self-evaluation-context` |
| Submitted view | `GET …/{id}/view-submitted-evaluation` — **404 until it is submitted**, which is the normal early state, not an error |
| Outcome | `GET …/{id}/hr-review` — but the client shows it **only once finalised**: before that the read still answers, with an in-progress picture that is not the employee's to act on |
| Acknowledge | `POST …/{id}/acknowledge` — **only from Governance, only by the appraisee, and only once**; all three answer 422/403 rather than failing quietly. The employee id in the body is ignored; the token decides |
| Write a response | `POST performance-appraisals/me/{id}/responses` — **the `/me` route, deliberately** |

> **Why the response route matters.** The desk route sits on HR's write policy and the response row has
> **no author column of any kind** — so through it, "the employee's response" was whatever HR typed, and
> nothing on the record contradicted that. The `/me` route refuses anyone but the appraisal's own
> employee (404, never 403), which is the only thing making the record mean what it says.

> **Changed 2026-09-29** (performance closure lane B1). **The row's *Next step* is the step the
> appraisal is at**, the one the employee's next write is held to: *Goal setting — …* with the reason
> (too few goals, one waiting for approval, a conversation not yet held), *Nominate your peer
> reviewers*, *Complete self-evaluation*, or *Acknowledge appraisal* — the last only once every step
> before it is done, where it used to appear the moment the manager submitted (and the acknowledgment
> was then refused). *Can file an appeal* follows the enforced appeal rule (Rule 9).

### ⚠ Known gaps
**P-34.** The *Outcome* tab is empty until HR finalises, including the score breakdown — correct, since
the manager's scores are not the appraisee's to read before sign-off, but it means an in-flight
appraisal shows the employee very little.

---

## 27. `…/appraisals/[id]/self-evaluation` — the employee's scoring form

### 📍 Where you are
**Route:** `/me/performance/appraisals/{id}/self-evaluation`
**As:** `staff` · **3 minutes** *(read-only on this database — hers is submitted)*

### 📖 What it is
The employee's own scoring form. Draft and submit are **the same endpoint with a flag flipped**, so the
form holds one set of values and the two buttons differ only in that flag. **Submitting is one-way**:
the server refuses every later write once the submitted date is set, which is why it sits behind a
confirmation.

### 👁 On the page

* **Header** — *My self-evaluation*, `cycle · period`, with **Save draft** and **Submit**
* A **Submitted <date>** banner where applicable: *"A submitted self-evaluation cannot be changed. Speak
  to HR if something needs correcting."*
* A **due date** line where it is still open
* **The scoring form** — the same component all three evaluation legs use, scoring the same frozen
  snapshot keyed by template item:
  * A **Scored *n* of *N*** progress bar, with *"Only items you have scored are sent. Everything
    required must be scored before you can submit."*
  * A card per section, badged **Weight 60%** / **Weight 40%**
  * A row per item, badged **KPI** where applicable and **Weight 50%**:
    * **KPI items** take an **Actual achieved** number, and show `Target 100 % · 96% of target` live
      underneath
    * **Everything else** takes a **Score (0–100)** and resolves a **Grade** live from the item's own
      bands — until a score is typed, the bands themselves are listed, so they are visible *while*
      deciding rather than after
    * **Comments** — *"What supports this score?"*
    * **Evidence link** — only on items whose criterion demands evidence
* **The goal assessment panel** — the year-end verdict on the goals the cycle spent the year cascading.
  Per goal: **Final actual · Final progress % · Final status · Assessment notes · Evidence**. A counter
  reads *n of N assessed*, and goals left blank are **not recorded either way**.
  *(Since closure lane L-b, where the template has a goals section the goals are scored **on the form
  itself**, one row each, and the panel's actual and percentage for such a goal are taken from its row
  — the achievement is entered once. The panel's status, notes and evidence stay the employee's own. An
  assessment of a goal that is not the employee's is refused.)*
  *(Since closure lane L-c the screen does it. A goals section's card says it holds the goals agreed
  and locked for this cycle; each goal row is badged **Goal**, and a goal with a target takes an
  **Actual achieved** against its own target while one without takes a **Score**. In the panel, such a
  goal's actual and percentage are greyed out: "Scored on the form — the actual and the percentage
  come from its row." The submitted view shows a measured goal's actual against its target, as it
  does a KPI's.)*
* **Questions** — the free-text item, with the template's question as its label
* The submit confirmation: *"You will not be able to change it afterwards, and your manager will be able
  to see your scores alongside theirs."* — plus, on this cycle, *"Your peer nominations must also be
  within the required range, or this will be refused."*

### ▶ Walk it

1. From Efua's appraisal, follow the **My evaluation** tab through to the submitted view *(the form
   itself is closed to her now, which is the point)*.
2. Walk the four scored lines, reading her numbers: 110 and 96 against targets of 100, then 90 and 88.
3. Point at the **weight badges**.

   💬 *"Sixty per cent of this form is the two indicators, forty is the two behaviours, and within each
   section the items split evenly. So each KPI is worth thirty points of the hundred and each behaviour
   twenty — which is exactly the arithmetic that produced her 94.4."*

4. Point at the KPI row showing 110 against a target of 100.

   💬 *"And she over-delivered on the first one — a hundred and ten against a target of a hundred. The
   system scores that as a hundred per cent, not a hundred and ten. Hitting target is full marks; there
   is no extra credit for overshooting, and that is a deliberate anti-gaming rule."*

5. Scroll to the **goal assessment** block and read one of her notes.

   💬 *"And the goals get closed off here, by her, in her words. 'Improved but short of the five-day
   target; plotter downtime in June.' Her manager will write his own conclusion in his own column — the
   two are stored separately and neither overwrites the other, because the year-end conversation is
   precisely the comparison between them."*

6. Point at the free-text **question** and read it.

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| Load | `GET api/PerformanceAppraisals/{id}/self-evaluation-context` |
| Save draft / Submit | `POST api/PerformanceAppraisals/{id}/self-evaluation` with `isDraft` true/false |

> ⚠ **This endpoint answers 200 with `success: false`** for a rejected save — a failed peer nomination
> count, an unscored competency — as well as 400. Both are handled; treating a 2xx as success would
> silently lose the user's work. You will see a **red** toast with the server's own rule in it.

**Submit runs the completeness rules:** every scored item carries a value; peer nominations within range
when the cycle requires employee-driven nomination; all competencies rated when soft-skill rating is on.

**On a KPI-backed goal the server recomputes the percentage** from the actual value against the goal's
target/min/max, and its number wins — so the *Final progress %* field disables itself and says why once
an actual value is present.

> **Changed 2026-09-29** (performance closure lane B1). **Submit is refused until the appraisal is at
> the self-evaluation step** — the goals set as the profile requires (the minimum, the manager's
> approval), the kick-off and mid-year conversations held, and the peer nominations in — with a 422
> naming the step and the reason, e.g. *"The self-evaluation cannot be submitted yet: this appraisal is
> at Goal Setting — the mid-year conversation has not been held."* **Save draft** works at any point.
> The goals counted are the employee's in the cycle, including any agreed before the appraisal existed.
>
> **Changed 2026-09-29** (performance closure lane B2, slice B-w). **The mid-year no longer holds the
> self-evaluation** — it holds the manager's (chapter 29); the example refusal above now reads *"…at Goal
> Setting — the kick-off conversation has not been held"*. When the profile asks for every behavioural
> criterion, the page says so above the form. **A criterion that requires evidence** (chapter 5)
> refuses the submit while it is scored without a link, naming it; the link is now saved with the
> score — every save used to drop it.

### ⚠ Known gaps
**P-35.** Efua's self-evaluation is already submitted, so this chapter is a **read**. If you want a live
self-evaluation, use `new.hire` — but his is submitted too. The only genuinely open self-evaluations
belong to the 30 opened Draft appraisals, none of which have goals, and their rail therefore reads
*Goal setting* (see §1.4).

---

## 28. `/me/performance/peer-reviews` — the feedback you owe a colleague

### 📍 Where you are
**Route:** `/me/performance/peer-reviews` and `…/peer-reviews/[id]`
**Portal:** My Self-Service → Performance → Peer Reviews
**As:** `new.hire` *(Kojo Ansah)* or `she.manager` — **2 minutes**

### 📖 What it is
Peer feedback **you owe on other people's appraisals**. Separate from *My Appraisals* on purpose: mixing
them would put a colleague's appraisal under a heading that implies it is yours.

**A row appears only once the nomination has been approved** — approval is what creates the evaluation
record. A nomination still pending is invisible here, which is correct: there is nothing to fill in yet.

### 👁 On the list
**Colleague · Cycle · Status · Due**, with a link to *My own appraisals*. **Due** is the date the
nomination set, else the cycle's peer deadline; the nominator's instructions sit under the colleague's
name.

### 👁 On the detail page
The **same scoring form** as the self and manager legs — the snapshot is shared, so the criteria and
their weights are identical to what everyone else is scoring against. Two things are peer-specific:

* **KPI rows may be read-only.** When the cycle's settings do not let peers score KPIs — as on this
  profile — those items still appear, so the criterion is visible in context, but cannot be scored. The
  server enforces the same rule when validating the submission.
  *(Since closure lane L-b the same setting governs the employee's **goal rows**: shown, read-only, and
  a peer's score on one is refused when saved. For KPI items the server checked only the submission
  until lane B-w, which refuses the draft too.)*
  *(Since closure lane L-c the screen takes which rows are read-only from the server and sends only
  the rows you may score. Its note reads: "This cycle does not ask peers to score KPI targets or the
  employee's goals — those rows are shown for context but cannot be scored.")*
* **Anonymity is about the appraisee, not you.** `isAnonymous` means *they* will not see who said what.
  **Their manager always sees your name**, and the screen says so plainly rather than letting the word
  "anonymous" imply more than it means.

Above the form, a card — *What you were asked to comment on* — carries the nominator's instructions.

**Save draft** and **Submit** — and submit is *score-then-submit*: the draft carries the scores, the
submit validates completeness, and doing both in one click saves the user from remembering to save
first.

### ▶ Walk it

1. Sign in to window B as `new.hire` *(Kojo Ansah)*.
2. Open **Peer Reviews**. He owes — or owed — feedback on Efua Seidu and Cynthia Sarpong. Both rows
   are due **9 Oct 2026**, the date their nominations set, and carry the instructions under the name:
   *"Please comment on how you have found working with this colleague over the year. …"*

   💬 *"The date is the one his nomination gave him, and the line under each name is what he was asked
   to speak to — it is on the form too."*

3. Open one.
4. Point at the KPI rows, which are present but not editable.

   💬 *"The KPI lines are here so he can see what the person is being measured on — but he cannot score
   them, because this year's policy says peers speak to behaviour, not to somebody else's numbers. He
   scores the two competencies, and that is it."*

5. Point at the anonymity line.

   💬 *"And 'anonymous' is precise here. Efua will not see who said what. Her manager will, and so will
   HR — because a manager weighing peer feedback needs to know whose it is, and pretending otherwise
   would either hide information he needs or leak it to the person being reviewed."*

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| My queue | `GET api/PeerEvaluations/me` — evaluator from the token; **there is no way to open somebody else's form** |
| Load one | `GET api/PeerEvaluations/{id}` |
| Draft | `POST api/PeerEvaluations/{id}/draft` — **422 once submitted** |
| Submit | `POST api/PeerEvaluations/{id}/submit` — everything required must be scored; **422 names how many are outstanding** |

**Peer nominations** live on the appraisal, not here: `…/peer-nominations/batch` to nominate,
`…/peer-nominations/approve` and `…/reject` in batches. In *Employee* mode nominations land
**Pending**, and approving is what creates each peer's evaluation record and notifies them; in
*Manager* mode the manager's nominations are approved as they are made. Only a pending nomination can
be edited — its due date and instructions — or removed.

> **Changed 2026-09-29** (closure lane P, P14). **The nominated peer can read their nomination but
> never change or delete it** — they could mark it approved or withdraw one they did not want. A
> nomination records **who actually made it**, from the login (the batch recorded every one as the
> appraisee's, whoever sent it). In a cycle that nominates in **Manager** mode the appraisee is
> refused both nominate routes (*"In this cycle your manager chooses your peer evaluators."*).

> **Changed 2026-09-29** (performance closure lane B1). **The peer form has a window.** With *Peer
> window opens* set to after the self-evaluation, a peer's draft and submission are refused until the
> employee has submitted; alongside it (as on this profile), from the self-evaluation step. Either way
> the window closes when the manager submits. The nominations count towards the minimum once made —
> a pending one counts, a rejected one does not — so the self-evaluation waits only for the employee's
> own step, nominating. A peer's submission that is the last step (no manager evaluation, nothing
> after) completes the appraisal.

> **Changed 2026-09-30** (closure lane D). **A nomination is what it says it is.** It starts *Pending* —
> a request that says otherwise is refused — and an edit changes only its due date and instructions,
> while it is pending (it could move a nomination to another appraisal, swap the peer or mark it
> approved). The appraisee's **line manager cannot be nominated** — they evaluate as the manager — on
> either route, nor can anyone who is not an employee. **A rejected nomination leaves room for
> another**: the counts, the maximum and the self-evaluation's rule count pending and approved ones
> only (with one peer allowed, a rejection stranded the employee). In **Manager** mode the manager's
> nominations are approved as they are made — the peer is asked at once — and nobody is asked to
> approve them. **HR's advance past peer nomination** approves the pending nominations the same way,
> forms and notices (it set the status alone, so its "approved" peers had no form). **The peer sees the
> nomination's due date and the nominator's instructions** — on this database *9 Oct*, where the list
> showed the cycle's *20 Nov*. **The peer reads a nomination once it is approved**, and their own lists
> carry approved ones only (they listed pending and rejected ones, with the reason written for the
> appraisee). In *Manager* mode with anonymous reviews **the appraisee sees how many peers were asked,
> not who** — the manager chose them, and with one peer the peers' average would be that person's
> score. The *send invitation* door, which sent nothing, is gone: approval tells the peer.

### ⚠ Known gaps
**P-36.** Every peer evaluation on this database is already submitted, so the forms open read-only.

---

## 29. `/hr/performance/team-appraisals` — the manager's evaluation

### 📍 Where you are
**Route:** `/hr/performance/team-appraisals` and `…/team-appraisals/[id]`
**Sidebar:** Human Resources → Talent & Performance → Performance → Team Appraisals
**As:** `head.dev` · **6 minutes** — the richest single screen in the module

### 📖 What it is
The manager's appraisal workspace: everyone reporting to you in a cycle, and how far each has got.
Scoped **by cycle** rather than showing everything at once, because the questions a manager actually
asks are per cycle — *"who still owes me a self-evaluation this year"*, *"whose evaluation have I not
written yet"*. The cycle list only contains cycles you have somebody in, and the manager identity comes
from the token: **there is no "view as" here.**

### 👁 On the list
Cycle picker, a **Check-ins** link, **four tiles** — Team members · Evaluated · In progress · Not
started — and a table:

| Employee | Self-evaluation | My evaluation | Status | Score |
|---|---|---|---|---|

where the two evaluation columns read **Outstanding / Draft saved / Not started / Submitted**, or
**Not required** where the cycle switched the step off.

### 👁 On the detail page — the manager evaluation

**Header:** the employee's name, `number · position · cycle`, a status badge, and two buttons:
**Save draft** and **Submit**.

> **Submitting is one-way and does more than save.** It assigns the HR reviewer, moves the appraisal to
> **Governance**, and locks every later write. HR's **return to manager** at the sign-off (chapter 31)
> is the only route back: it clears the submission first. An appeal **remand** reopens it too, until the
> remand's deadline, keeping the original submission on record *(since closure C-a, 2026-09-30 — it
> reopened nothing, P-71)*; the re-submission goes to HR for the appeal's final decision, not back into
> sign-off. That is why it sits behind a confirmation and why draft-saving is the prominent action.

**The phase rail**, then banners: *Re-evaluation after an appeal* (with the appealed items highlighted
and the remand deadline), or *Submitted <date> — This evaluation is locked. HR can return it to you if
it needs changing.*

**Eight tabs:**

| Tab | What is in it |
|---|---|
| **Evaluation** | The scoring form — **with the employee's own score beside every row** |
| **Goals** | The goal assessment panel in *manager* mode: his conclusion, **with her claim shown read-only above it** |
| **Assessment & recommendations** | Six narrative boxes, each 2000 characters with a live counter: **Overall comments · Strengths · Areas for improvement · Training needs · Career aspirations · Notes on your recommendations** — then five checkboxes: **Promotion · Salary increment · Training · Performance improvement plan · Termination** |
| **Peer feedback** | Every peer's scores and comments, attributed — each criterion a peer scored, with its section and weight |
| **Nominations** | Only when the cycle nominates in *Manager* mode — the manager chooses the peers, and each is asked as soon as they are nominated |
| **History** | The employee's score across previous cycles |
| **Conversations** | The panel that schedules and completes appraisal conversations |
| **Evidence** | Attachments — *"Evidence behind the ratings — reports, certificates, correspondence. Scanned on upload; max 10 MB."* **Remove** is offered on a file to whoever attached it, or to HR, and on nothing once the appraisal is complete — the server refuses the rest (closure P9, 2026-09-29; it removed anyone's file, at any stage) |

Below the tabs, when the cycle nominates in *Employee* mode, a separate card: **Peer nominations
awaiting your approval** — because the employee nominates, but approving is still the manager's call.

**The comparison column is the point of the Evaluation tab.** Beside each row sits a panel headed *Their
self-assessment* with the employee's number, their achieved grade and their note. It appears once the
employee has **submitted** — a self-evaluation draft is never on this form (until then each row reads
*"… has not submitted their self-evaluation yet"*). When the cycle's *Managers see the self-score* is
off, the server withholds it until the manager has submitted their own evaluation: the aside is
**dropped** rather than shown blank, with a line at the top of the page saying *"This cycle shows you
{name}'s self-scores once you have submitted your own evaluation."* After the submission the comparison
is there. The **Peer feedback** tab follows *Managers see peer scores* the same way — names and the
submitted count always, the scores and comments once the manager has submitted.

> **Changed 2026-09-29** (performance closure lane B2, slice B-v). The switch used to be a React check
> on this page alone — the server sent the self-scores whatever it said, a draft's included, and four
> other reads the manager can open (the submitted self-evaluation, the self-evaluation context, the HR
> review, the goal assessments) carried them too; when it was off, the page hid the comparison for good,
> even after the manager had submitted. Nothing changes on the demo, whose profile shows both.

> **Changed 2026-09-29** (performance closure lane L-c). **The manager scores goal rows** on the same
> form as the employee (chapter 27): each badged **Goal**, a measured goal taking the actual achieved
> against its own target, with the employee's figure beside it where the cycle shows self-scores. On
> the **Goals** tab, a goal scored on the form has its actual and percentage greyed out — the row is
> where they are entered. Before this lane every goal row shared one key on this screen and the
> employee's: a score typed on one showed on all of them, and the save could not name the row.

> **Changed 2026-09-30** (performance closure lane D). **Peer feedback lists every criterion a peer
> scored** — a competency, and a KPI or goal row where the cycle lets peers score them — each with its
> kind, its section, its weight in the section and its score, a measured row by its achievement with
> the actual against the target. It listed competencies alone, every one at weight 0 % with a *Grade*
> of "—", beside a KPI table that was always empty. On the demo (peers score competencies only) each
> peer's rows are *Communication* and *Teamwork* in *Core Competencies*, 50 % each. In *Manager* mode
> the **Nominations** tab asks each peer as soon as the manager nominates them — there is no approval
> step — and HR's advance past peer nomination asks the pending ones too.

### ▶ Walk it

1. In **window B** as `head.dev`, open **Team Appraisals**.
2. Read the tiles and the table. Efua is **Submitted**; the rest of his eight reports are mostly *Not
   started*.
3. Open **Efua Seidu**.
4. Stay on **Evaluation**. Scroll to the first competency row and point at the panel on the right.

   💬 *"And here is the single best thing on this screen. He is not scoring her from memory and then
   discovering what she said. Her number is beside his, on the same line, while he types — because the
   whole point of a manager evaluation is the comparison, and putting the two a click apart makes it an
   act of memory. She gave herself ninety on communication. He gave her eighty-four, and he had to look
   at hers to do it."*

5. Point at the **weight badge** on the section header.

   💬 *"Same form, same weights, same frozen snapshot she scored against. Three people, one instrument."*

6. Go to **Goals**. Show the read-only *Employee's assessment* box above each of his fields.

   💬 *"Same idea on the goals. Her claim on the left, his conclusion underneath — stored in separate
   columns, neither overwriting the other. In December you can put the two side by side and have an
   actual conversation instead of an argument about what was said in May."*

7. Go to **Assessment & recommendations**. Read his overall comments aloud — they are good copy.

   💬 *"'Efua carried the Community 25 Phase 2 layout from base plan to approval and settled the eastern
   boundary dispute on site. Turnaround on client amendments improved from eleven days to seven, short
   of the five-day goal, but the cause was plotter downtime rather than the officer.' That is a manager
   writing an appraisal with the evidence in front of him — the journal entries, the check-in record,
   the progress log."*

8. Point at the five recommendation checkboxes.

   💬 *"And these five are recorded against the appraisal for HR to act on. Ticking 'salary increment'
   does not start a salary increment — it tells HR that this appraisal called for one. What happens next
   is a separate, approved chain, and we will follow it in Part Six."*

9. Go to **Peer feedback**. Point at the banner — *2 of 2 peer evaluations submitted* — then at a
   peer's rows: *Communication* and *Teamwork*, each in *Core Competencies* at 50 %.

   💬 *"Both her peers have submitted — and he sees their names, because this setting is about what **she**
   sees, not what he does. The screen says so, so he knows before he quotes a comment back to her in a
   conversation."*

10. Go to **History**.

    💬 *"And previous years, next to this one. A sixty-eight is a good year for one person and a slide
    for another — and this is the moment the number is being decided."*

11. Go to **Evidence**, then **Conversations** *(where you scheduled the Q4 meeting in chapter 23)*.

12. **Do not press Submit** — Efua's evaluation is already submitted and locked, so the buttons are
    absent anyway. Read the locked banner instead.

    💬 *"And once he submits, it is locked. Not by convention — the API refuses every later write. The
    only way back is HR returning it to him, which reopens it and clears the submission. An appraisal
    that can be quietly re-scored after the fact is not an appraisal."*

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| Cycle list | `GET api/PerformanceAppraisals/manager/me/team-cycles` |
| Team members | `GET …/manager/me/cycle/{cycleId}/team-members` |
| The form | `GET …/{id}/manager-evaluation-context` — **403 when the caller is not this employee's manager** |
| Save / Submit | `POST …/{id}/manager-evaluation` with `isDraft` — **submitting assigns the HR reviewer and moves the appraisal to Governance** |
| Peer feedback | `GET …/{id}/manager-peer-evaluations` |
| Nominations | `GET …/{id}/peer-nominations/summary`, `POST …/approve`, `POST …/reject` |
| Attachments | `GET/POST api/PerformanceAppraisals/{id}/attachments` — the controlled scan gate |

> ⚠ Like the self-evaluation, **a rejected save answers 200 with `success: false`**. The screen surfaces
> the rule rather than claiming success.

> The `managerId` in the payload is **overwritten server-side from the token** — it is sent only to match
> the documented shape.

> **Changed 2026-09-29** (performance closure lane B1, decision 5). **The manager submits after the
> employee.** *Submit* is refused until the appraisal is at the manager-evaluation step — the
> self-evaluation and the minimum of peer evaluations in — with a 422 naming what is outstanding
> (*"…this appraisal is at Self-Evaluation — the employee has not submitted their self-evaluation"*).
> Drafts save at any point. If a step will not be completed, HR's audited advance (chapter 38) is the
> way past it; a self-evaluation waived that way leaves the employee's draft a draft, counted nowhere.
> Where the submission leaves the appraisal is the gates' call: Completed when nothing follows, the
> employee's end when only the conversation or the acknowledgment does, Governance otherwise.

### ⚠ Known gaps
**P-37.** Uploading on the **Evidence** tab needs the malware scanner running. Without it every upload
answers **422** — correct behaviour (every `hr-*` category is scan-mandatory and tenant policy cannot
override it), but it will look like a failure. Start the stub, or skip the tab.
**P-38.** Seven of `head.dev`'s eight reports have no goals and no evaluations, so the list is mostly
*Not started*. Say it: *"one complete appraisal, and seven people whose year has not been set up — which
is exactly the report a Head of Department needs in February."*

---

## 30. `/hr/performance/calibration` — the panel

### 📍 Where you are
**Route:** `/hr/performance/calibration` and `…/calibration/[id]`
**Sidebar:** Human Resources → Talent & Performance → Performance → Calibration
**As:** `hr.head` · **5 minutes** — 🔴 **LIVE WRITE 8 and 9 live here**
**⚠ Read Rule 2 before this chapter.**

### 📖 What it is
The panel that reconciles managers' ratings across a unit **before** HR signs the appraisals off. When
the cycle's profile has calibration on — as this one does — it is **not optional**: an appraisal cannot
reach HR review until a session covering it has been **committed**.

**Scope is the thing to get right when creating one.** A session covers its cycle, narrowed to an
organisation unit *(and everything beneath it)* or an organisation level. Leave both blank and it covers
the whole cycle — which is what you want for a small organisation and almost never what you want
otherwise.

**The lifecycle is three steps, and the last two are different decisions:**

```
  Open        records you as facilitator, stamps the start, and links every appraisal
              in scope that is waiting for calibration
  Close       no further adjustments; the panel is notified the ratings are ready
  COMMIT      irreversible. Writes the agreed ratings onto the appraisals AND lifts the
              calibration gate on everyone at the calibration step (manager submitted) —
              including the people left alone. The rest are listed as skipped, with why.
              Once per appraisal: run again, it skips what it calibrated.

  Cancel      before the session completes: called off with a reason; its appraisals
              are released for another session, and nothing of it is ever applied
```

### 👁 On the list
Cycle picker, three tabs — **Open · Completed · All** — four tiles *(Not yet opened · In progress ·
Completed · Total)* — and a table: **Session · Scope · Status · Scheduled · Facilitator**.

**On this database the Open tab is empty.** The **Completed** tab has two:

| Session | Scope | Panel | Adjustments |
|---|---|---|---|
| Operations Directorate calibration panel 2026 | Operations Directorate | 4 | 1 |
| Finance & Administration calibration panel 2026 | Finance & Administration Directorate | 3 | 1 |

**The new-session dialog:** Session name *(e.g. "Finance — FY2026 calibration")* · organisation scope ·
Scheduled for · Agenda *("What the panel will work through.")*.

### 👁 On the detail page

**Header:** the session name, the scope in words — *"Operations Directorate, and every unit beneath
it."* — and the buttons for the current state: **Open session** / **Cancel session** / **Close session** /
**Commit ratings**. *Commit ratings* renders only when a commit would take at least one row.

**Banners:** *Not open yet* — *"Add the panel first, then open the session. Opening records you as
facilitator and links every appraisal in scope, so no ratings can be recorded before it."* · and, once
closed but not committed, *Closed, but not committed* — *"N appraisals in this session have not been
through the calibration gate. Until you commit, none of them can reach HR review on a cycle that
requires calibration — including the ones the panel agreed to leave as they are."* — N counts the rows a
commit would take · and on a cancelled session, *Cancelled* — *"This session was called off — the reason
is in its meeting notes. Its appraisals were released for another session, and none of its adjustments
is applied."*

**Four tiles:** In scope · Adjusted · Calibrated · Average score.

**Four tabs:**

| Tab | What is in it |
|---|---|
| **Grid** | Employee · Manager · **Manager proposed** · **Pre-calibration** · **Calibrated** · **Δ** · **Gate** · action. The Δ is coloured — green up, red down. Under *Gate*, a row waiting for calibration — or one the panel moved — says why a commit would leave it alone, when it would (*"Not at the calibration step: it is at Goal Setting — …"*, *"Already calibrated by this session …"*) |
| **Panel** | Panelist · Role in the room · **Attended** checkbox · Remove, plus an **Add a panelist** form |
| **Decisions** | Employee · **Applies to** *(Overall score, or a named criterion)* · From · To · **Rationale** · Recorded *(when, by whom)* |
| **Notes** | The agenda, and the meeting notes recorded at close |

**The Calibrate dialog** — *"Restates the overall score. Nothing changes on the appraisal until the
session is committed."* — shows **Manager proposed** and **Pre-calibration** side by side, takes a
**Calibrated score** and a **Rationale** *("Why the panel moved this rating. Kept as the audit trail.")*,
and hides a disclosure: **Adjust individual criteria (n)**, each with its weight, the manager's score,
a field, and a **Record** button. *"Each criterion is recorded separately from the overall score above.
Leave one blank to leave it alone."*

> **Changed 2026-09-29** (performance closure lane L-b). **The Decisions list names the criterion** — an
> item adjustment read *one criterion*, because the list never loaded the item. Where the template has
> a goals section, the per-criterion read lists the **goal rows** by their goal's title, and an
> adjustment on one moves that row, not the overall: the overall adjustment is told apart by its own
> flag, where a missing template item used to mean the overall.
>
> **Changed 2026-09-29** (performance closure lane L-c). **The dialog follows**: *Adjust individual
> criteria* lists the goal rows, each badged **Goal**, and records one by its snapshot row; the
> Decisions list and the dialog find the overall adjustment by its flag, so a recorded goal row no
> longer shows up as the *Overall score*.

### ▶ Walk it

1. Open **Calibration**. The **Open** tab is empty — go to **Completed**. Two sessions.
2. Open **Operations Directorate calibration panel 2026**.
3. Read the scope line and the four tiles.
4. Go to the **Panel** tab. Four panellists, all marked attended.

   💬 *"The Managing Director, the General Manager Operations, the Head of Development and the Head of
   HR. Attendance recorded per person, because a calibration decision taken by two of the four people
   who were supposed to be in the room is a different thing from one taken by all four."*

5. Go to the **Grid** and read one row across: manager proposed, pre-calibration, calibrated, Δ, gate.
6. Go to **Decisions** and read the one adjustment in full.

   💬 *"One decision, on Efua Seidu's project-delivery score — moved from ninety-two to eighty-eight,
   with the reasoning on the record: 'the panel accepts the plotter outage in June as outside the
   officer's control, but holds the delivery timeliness score below the manager's figure so the
   directorate distribution is not skewed.' Who recorded it, and when. That is what a calibration panel
   is for — not to move numbers, but to be answerable for having moved them."*

7. **Now create a live one, so the room sees the lifecycle.**

   🔴 **LIVE WRITE 8 — create and open a session.** Back on the list, click **New calibration session**.
   Name: `Corporate Planning & Communications calibration panel 2026`. Scope: **Corporate Planning &
   Communications Department**. Scheduled: today. Agenda: `Rank Corporate Planning & Communications
   against the corporate distribution and settle the outliers.` Save.

   > **Use this department and not another.** It sits under the Managing Director's Office but under
   > *neither* directorate, so its **nine** appraisals — two in the department itself, four in Client
   > Relations, two in Corporate Planning, one in Communications — are among the 31 the two existing
   > panels never covered. Pick a unit inside Operations or Finance & Administration instead and
   > everybody in it is already calibrated, the *not committed* banner never appears, and the **Commit
   > ratings** button never renders. *(The other uncovered units, if you want a smaller one: Internal
   > Audit 6 · Legal 4 · Procurement 4 · MERC 3.)*

   It appears in the **Open** tab as **Pending**. Open it — read the *Not open yet* banner.

   Go to **Panel**, add **Akpene Amoah** as *Facilitator* and **Nana Nyaho** as *Manager*. Then press
   **Open session**.

   💬 *"Opening does three things. It records me as facilitator, it stamps the start, and it links every
   appraisal in scope that is waiting for calibration to the session — so from this moment every one of
   them reports 'calibration in progress'. Nothing can be adjusted before that, because there is
   nothing to adjust against."*
   *(Corrected 2026-09-29: only an appraisal at the calibration step reads "in progress"; the rest show
   their own step. None of this department's nine has reached calibration.)*

8. Go to the **Grid**. Under each row's *Gate* the grid says why a commit would leave it alone — here,
   *"Not at the calibration step: it is at Goal Setting …"*. Press **Adjust** on any row, read the dialog,
   expand **Adjust individual criteria**, and then **Cancel** — you do not need to record one.
   *(Mark convened is gone since 2026-09-30: opening stamps the start.)*

9. 🔴 **LIVE WRITE 9 — close it.** Press **Close session**, add meeting notes
   `Panel confirmed every rating in Corporate Planning & Communications as proposed.` Confirm.

   ⚠ **On this database no banner appears and no Commit button renders** — none of the nine has
   reached calibration, so a commit would take nobody. *(True since the 2026-09-29 rebuild; the old
   screen counted appraisals in governance, and these are at goal setting.)* The *Gate* column says why.

   💬 *"And now the important half. Closing the room is not committing the decisions. The grid tells me
   whom a commit would take and why the rest wait: here, nobody's manager has submitted yet, so there is
   nothing to commit — the panel sat early. When an appraisal in a session like this reaches the step,
   committing confirms it — including every one the panel agreed to leave exactly as it was."*

10. **When a commit is on offer** (on a cycle with appraisals at the calibration step), press **Commit
    ratings**, read the confirmation out loud, and confirm.

    > *"This writes the agreed scores onto N appraisal(s) at the calibration step and marks them
    > calibrated — including the M the panel left as they are. The other K are left as they are, each
    > for the reason on its row; the result lists them. It cannot be undone."*
    > *(Wording since 2026-09-30.)*

    💬 *"Committed. Those appraisals are now through the gate, and HR can sign them off. And a commit is
    made once: run again, it leaves alone what it calibrated — an appeal decided since stands."*

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| By cycle | `GET api/CalibrationSessions/by-cycle/{cycleId}` |
| Create / amend | `POST` / `PUT api/CalibrationSessions[/{id}]` — particulars only; status is owned by the actions. The creator is the facilitator (the token's — the body named one until 2026-09-30); the scope changes only while Pending, and a completed or cancelled session is not edited (422) |
| Open | `POST …/{id}/open` — Pending → InProgress, records the caller as facilitator, stamps the start, links every appraisal in scope waiting for calibration |
| Cancel | `POST …/{id}/cancel` `{ reason }` — from Pending or InProgress; the reason heads the meeting notes; the appraisals it holds are released *(since 2026-09-30; `POST …/start` is gone)* |
| Close | `POST …/{id}/complete` — adjustments refused afterwards; the panel is notified |
| **Commit** | `POST …/{id}/apply-adjustments` — **only from Completed, and irreversible**; once per appraisal, and only an evaluation submitted before the panel closed |
| Grid | `GET …/{id}/matrix` — **every appraisal the session covers, not only the adjusted ones**; each row carries `commitSkipReason` |
| Per-criterion detail | `GET …/{id}/appraisals/{appraisalId}/criteria` — a *read*, so panellists get it, not just HR; **404 for an appraisal outside the session's scope** (any session id used to open any appraisal's criteria) |
| Adjustments | `GET/POST/PUT/DELETE …/{id}/adjustments[/{id}]` — **422 while the session is Pending, Completed or Cancelled** (the removal too, since 2026-09-30), and 422 if the appraisal is outside scope; an edit changes the score and rationale, and 422s a body naming another appraisal or criterion |
| Panel | `…/{id}/participants[/{id}]`, `PATCH …/attendance` |
| Attachments | `…/{id}/attachments` — HR only, through the scan gate |

**Reads are HR's and the panel's; every write needs an HR role.** *(Changed 2026-09-29, closure lane
P, P3 — reads were open to any authenticated user.)* A session is readable by the performance desk and
by its panellists — the participants and the facilitator — and the lists (by cycle, paged) give
everyone else only the sessions they sit on. The per-criterion read answers only for an appraisal in
the session's scope. **A panellist's own appraisal is left out of every read** — their matrix row,
its adjustments and criteria, and the grid's counts and average, which would otherwise let them work
their own score out. Panellists reach the detail page from the *"session complete"* notification.
Deleting a session needs `HR.Performance.Admin` — 403 for `hr.head` — and releases the appraisals it
holds; a completed session is not deleted.

**Scope resolution:** every appraisal in the cycle whose employee sits in the named unit **or any unit
beneath it**; or, when a level is named instead, everyone at that level. Anything already linked to the
session, or carrying one of its adjustments, is pulled in even if the scope has since changed.

> **Changed 2026-09-29** (performance closure lane B1). **The commit calibrates the appraisals at the
> calibration step by the gates** — the manager's evaluation in, and, where HR reviews *before*
> calibration, HR's sign-off given — plus one it already calibrated that this session restates, or a
> final one it adjusts. The rest are listed with the step each is at (*"Not at the calibration step: it
> is at HR Review — HR has not signed it off."*), and **leave the session**: opening linked them to it,
> and a skipped appraisal used to stay pinned to a panel that had already sat, reading "in progress".
> On a cycle with no calibration step the commit skips everything. **A commit that is the last step
> completes the appraisal** and settles its score — it used to leave it in Governance for good. The
> confirmation dialog still counts every appraisal in Governance as *at the step*, so where some are
> already calibrated, or wait on HR under HR-first timing, its *N* runs high; the result's skipped list
> is the truth (lane I aligns the dialog).
>
> **Changed 2026-09-30** (performance closure lane E-b). **A commit takes each appraisal once, and only
> the evaluation the panel sat over.** Run again, it re-applied every decision to a final appraisal it had
> adjusted — after an upheld appeal it wrote the manager's criterion back, restored the panel's overall,
> re-settled and published — and an appraisal HR returned to its manager, re-evaluated and back at the
> step took the old panel's decisions. Now it skips *"Already calibrated by this session"* and, once the
> session has closed, anything whose manager submitted after it (*"the panel did not see that
> evaluation"*); the next panel calibrates those. **The grid says in advance** why a commit would leave
> each row alone, and the dialog and the *Commit ratings* button count from it — the dialog's *N* is now
> exact (the paragraph above). **Opening links only appraisals waiting for calibration**, and HR's advance
> past calibration drops the link. **A session can be cancelled** (a reason required) and **a deleted one
> releases its appraisals** — both left them linked to a session that would never commit. **Mark
> convened is gone**: opening stamps the start. **The facilitator** is the creator, then the opener,
> never a field. **An open session's scope is fixed.** **An adjustment stays on its appraisal and
> criterion**, and is changed or removed only while the panel sits.

### ⚠ Known gaps
**P-39.** ~~**The overall calibrated score does not survive HR sign-off** (Rule 2). HR's finalise
recalculates from the evaluation legs.~~ **Fixed 2026-09-29** (closure A3): the calibrated overall
is kept and sign-off settles *from* it. Cynthia Sarpong's record predates the fix (Rule 2).
**P-40.** ~~**A per-criterion adjustment on a KPI item does nothing to the score.** It writes
`NumericScore`; a KPI is scored from `ActualValue`. Competency items are fine.~~ **Fixed 2026-09-29**
(closure A4) — and it was wider than written: *no* item adjustment reached the overall, because the
overall re-summed the stored weighted scores. The settle recomputes every item from its raw inputs,
and a KPI restatement is an achievement percentage, labelled on every screen. Efua Seidu's record
predates the fix (Rule 2).
**P-41.** ~~The grid's **Calibrated** column reads the *adjustment record*, not the appraisal — so it can
legitimately disagree with the score on the HR review screen.~~ **Fixed 2026-09-30** (closure E-b): a
calibrated row reads the appraisal's settled score, unless the session is still proposing another — Efua
Seidu's reads 88.56 (it was blank).
**P-42.** **Only 4 of 107 appraisals are calibrated** *(76 before the 2026-09-29 rebuild; Rule 2)*.
The two panels cover two directorates, and only the four whose manager had submitted were at the step;
no appraisal can be finalised until a session covering it is committed.

---
## 31. `/hr/performance/hr-review` — the sign-off

### 📍 Where you are
**Route:** `/hr/performance/hr-review` and `…/hr-review/[id]`
**Sidebar:** Human Resources → Talent & Performance → Performance → HR Review
**As:** `hr.head` · **6 minutes** — 🔴 **LIVE WRITE 10 lives here**
**⚠ Read Rule 3 before this chapter.**

### 📖 What it is
HR's sign-off queue: **every appraisal in a cycle**, and whether it is ready to be finalised.

"Ready" is a real gate, not a hint — finalising an appraisal whose self, manager or minimum peer
evaluations are outstanding is refused with 422. So the list shows each of those three, and the reason
is visible from the queue rather than only after opening a record.

**Two outcomes, and they are not symmetrical:**

* **Finalise** settles the score — **the panel's restated overall where a calibration committed one**,
  otherwise the weighted mean of the submitted evaluations (since 2026-09-29; before, it recalculated
  over the panel's figure, Rule 2) — records the sign-off, and **pushes the rating onto the employee's
  talent records**. Where it lands depends on the cycle: `Governance` when an acknowledgment is
  required — the employee closes it out — or `Completed` when one is not.
* **Return to manager** reopens the manager's evaluation and puts the appraisal back to `Active`.
  **Remarks are mandatory**: they are the whole message the manager gets. It is made **before the
  sign-off** — at HR's review, or while the appraisal waits for a calibration panel that has not sat —
  and a calibrated appraisal loses its calibration with it: the panel restated the evaluation now being
  revised, so the appraisal is calibrated again on the new one.

### 👁 On the queue

Cycle picker *(with an "All cycles" option — this queue is cross-cycle by nature)*, **four tiles** and
**four tabs**.

| Tile | Here | Hint |
|---|---|---|
| **Ready to finalise** | **1** | |
| Waiting on evaluations | **103** | Self, manager or peer feedback outstanding |
| Finalised | **3** | |
| Total | **107** | |

Tabs: **Ready (1) · Waiting (103) · Finalised (3) · All (107)**.

**The table:** Employee · Cycle · **Self** · **Manager** · **Peers** · HR review · Score · action —
where Self and Manager are a green tick or an amber clock, and Peers is `2/2` in green or `0/2` in
amber.

The single **Ready** row is **Kwasi Danquah — Head of Development**, self ✅, manager ✅, peers **2/2**,
HR review *Not started*.

### 👁 On the detail page

**Header:** the employee's name, `position · organisation unit · cycle`, a status badge, and five
actions:

| Button | When | What it does |
|---|---|---|
| **Withdraw** | Draft, Active, or governance **before the appraisal is final** — and not on your own | Takes the appraisal out of its cycle, with a reason the employee can read (since 2026-09-30). It leaves every count and queue, no one can write to it again, what was written stays on the page without a score, and outcomes proposed on it are dismissed. **No undo from here** |
| **Correct dates** | not finalised | Amends the window — the year and the dates — **nothing else**; refused once the appraisal is final |
| **Remove** | only while nothing in it counts — Draft, or Active with nothing submitted — **and only with `HR.Performance.Admin`** | Removes an appraisal generated against somebody who should not have been in scope. **Hidden for `hr.head`**, and on this page nearly always (an appraisal reaches HR's review once its manager has submitted) |
| **Return to manager** | manager evaluation complete, in governance, before the sign-off, no panel sitting | Reopens it; a calibrated appraisal is calibrated again |
| **Finalise** | self + manager + minimum peers are in | Signs it off |

**The phase rail.**

**Then the precondition panel** — the important part of this screen — which is one of three:

* *Not ready to finalise* — **"Finalising is blocked because the employee has not submitted a
  self-evaluation, and only 1 of 2 peer evaluations are in."**
* *Ready to finalise* — **"All required evaluations are in. Finalising settles the score — the
  calibration panel's overall where it restated one, otherwise the weighted evaluations — and hands
  the appraisal on for acknowledgment."**
* *Finalised <date> by <name>* — with the acknowledgment state and HR's remarks

Plus, where the weights are wrong: *"Evaluator weights do not total 100% — they come to N%. The final
score will be calculated from them as they stand; fix the cycle's settings profile before signing this
off."* *(Absent here: 10 + 20 + 70 = 100.)*

**Four tiles:** Self *(weight 10%)* · Peers *(weight 20% · 2/2 in)* · Manager *(weight 70%)* ·
**Final** *(hint: "Computed on finalisation")*.

**Five tabs:**

| Tab | What is in it |
|---|---|
| **Manager evaluation** | Competencies — Criterion · Weight · Score · **Weighted** — and KPIs — KPI · Target · Actual · **Achieved** — plus the general comments |
| **Self-evaluation** | The same two tables for the employee's leg |
| **Peer feedback** | Every peer, attributed, with their scores and comments |
| **History** | Previous years next to this one |
| **Outcomes** | The recommendation panel, **with approve / reject / dismiss** |

> **Changed 2026-09-29** (performance closure lane L-b). **Goal rows are in the two tables**: a measured
> goal with the KPIs (target, actual, achieved), a rated goal with the competencies (score, weight,
> weighted), each named by the goal's title and flagged as a goal. They were left out of both.
> *(Lane L-c)* The screen shows the flag: a **Goal** badge on each goal row, and the tables' titles
> read **Competencies and rated goals** and **KPIs and measured goals** when a goal is in them.

**The Finalise dialog:** *"The weighted final score is calculated and recorded, and the result is pushed
onto the employee's talent records. This cannot be undone from here."* — plus an optional **HR remarks**
box, *"Visible to the employee with their result."*

**The Return dialog:** *"Their evaluation is reopened for editing and the appraisal goes back to Active.
Your remarks are the only explanation they get, so be specific."* — and the button stays disabled until
you type something.

### ▶ Walk it

1. In **window A** as `hr.head`, open **HR Review**.
2. Read the four tiles.

   💬 *"A hundred and seven appraisals in this cycle. Three finished. A hundred and three still waiting
   on somebody — and one, exactly one, waiting on me."*

3. Point at the **Self / Manager / Peers** columns on the *Waiting* tab for two or three rows.

   💬 *"And the queue tells me why each of them is not ready without my having to open it. Self-
   evaluation in, manager's not. Manager's in, one peer of two. That is the difference between a work
   queue and a list."*

4. Go to the **Ready** tab and open **Kwasi Danquah**.
5. Read the green banner: **Ready to finalise**.
6. Read the four tiles aloud.

   💬 *"Ninety-two point two from him, seventy-eight from his peers, eighty-eight point eight from the
   General Manager Operations — and no final score, because the final score does not exist until I press
   the button. It is not stored anywhere waiting to be revealed; it is computed at sign-off from those
   three legs at those three weights."*

7. Go to **Manager evaluation** and walk one competency row: criterion, weight, score, weighted. Then a
   KPI row: target, actual, achieved.

   💬 *"And this is where the arithmetic is visible line by line. Weight, score, weighted contribution.
   Four lines, a hundred points of weight, one evaluator score."*

8. Go to **Self-evaluation** and put the two side by side conceptually.

   💬 *"His own view, and his manager's. He scored himself ninety-two; the General Manager gave him
   eighty-nine. The gap is three points, and it is visible rather than argued about."*

9. Go to **Peer feedback**, then **History**.

10. 🔴 **LIVE WRITE 10 — finalise it.** Press **Finalise**. In the remarks box type:

    `Scores confirmed at the Operations Directorate calibration panel. The backlog clearance and the
    digital issue register are both noted; leadership development carried into the 2027 plan.`

    Read the confirmation aloud, then confirm.

    Expect the toast **"Appraisal finalised — the employee has been asked to acknowledge it"**, the
    banner to change to *Finalised <today> by Akpene Amoah*, the **Final** tile to fill with a score and
    a grade, and the phase rail to advance to **Employee acknowledgment**.

    💬 *"Done. The weighted score is computed and recorded, HR's remarks go on the record where he can
    read them, and the result is pushed onto his talent records — his succession readiness and his
    talent-pool rating move with it, without anyone re-keying a number into the succession module.
    And he has been notified, because the cycle requires his acknowledgment: it is not finished until
    he says he has read it."*

11. Go back to **HR Review**. The **Ready** tile now reads **0** and **Finalised** reads **4**.

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| Queue | `GET api/PerformanceAppraisals/hr-review-list?cycleId=&status=` |
| Detail | `GET …/{id}/hr-review` |
| Phase | `GET api/AppraisalWorkflow/{id}/phase` |
| **Finalise** | `POST …/{id}/approve` — writes the HR review record **and** the HR evaluator record, sets the status and settles the score in one transaction (calibrated overall first, else the submitted legs), then syncs the talent rating after the commit |
| **Return** | `POST …/{id}/return-to-manager` — remarks required; **422** after the sign-off, once final, or while a panel sits |
| Assign HR reviewer | `POST …/{id}/progress-to-hr-review` — a repair route, offered only when no reviewer was resolved at submission time |
| Correct dates | `PUT api/PerformanceAppraisals/{id}` — the year and the dates; **422** once final |
| Remove | `DELETE api/PerformanceAppraisals/{id}` — **`HR.Performance.Admin`**; **422** once anything in it counts |
| **Withdraw** | `POST …/{id}/withdraw { reason }` — HR write; **400** without a reason, **403** on your own appraisal, **422** once it is final (Completed, Closed, Appealed, or signed off in governance) or already withdrawn |

**The 422s Finalise can answer**, in the order they are checked: self evaluation outstanding · manager
evaluation outstanding · fewer than the minimum peer reviews · **the calibration gate** *(Rule 3 — the
only one the button does not pre-empt)*.

**The HR reviewer is assigned automatically** when the manager submits: the profile's *Default HR
reviewer* if one is set, otherwise the **least-loaded** active HR employee, found by position title or
organisation unit containing "HR". On this database Kwasi's assigned reviewer is **Esi Vanderpuye**, not
Akpene Amoah — and `hr.head` can still finalise, because the endpoint is gated on the HR **write**
policy rather than on being the named reviewer. Worth knowing; not worth mentioning on stage.

> **Why *Correct dates* refuses to move an appraisal between people.** The route was a replace and its
> DTO marked the cycle, the employee and the status as required — but **the server ignored all three**,
> and since closure lane E-a it takes the window alone.
> Until it did, a "correction" could move an appraisal, with its goals, self-evaluation and scores, onto
> a different person, into a different cycle, and walk it Draft → Completed straight past the
> forward-only state machine. The dialog says it plainly: *"An appraisal raised against the wrong person
> is removed and regenerated, not moved onto somebody else."*

> **Changed 2026-09-29** (performance closure lane B1, B4). **The sign-off is held to the HR-review
> step**, the same gates the rest of the pipeline uses: the self-evaluation and the peer minimum only
> when the profile requires them (both were demanded regardless), calibration first only where HR
> reviews after it — under *HR review runs: before calibration*, HR signs off first and the panel comes
> after (such a cycle could not be finalised at all). *Ready for HR* on the list, *Finalise* on the page
> and the server's refusal all read the gates. Where the sign-off leaves the appraisal is theirs too:
> Completed when it was the last step, otherwise waiting on the panel, the final conversation or the
> acknowledgment — it chose between Governance and Completed on the acknowledgment switch alone, which
> completed appraisals a required final conversation still held. The employee is told when the sign-off
> releases the outcome to them, not while a panel may still restate it.

> **Changed 2026-09-30** (performance closure lane E, slice E-a). **Correct dates changes the window
> and nothing else** — it used to take the whole record back, and since the page sends none of the
> manager's fields, **every correction blanked the manager's comments, strengths, development points
> and all five recommendation flags**; it is refused once the appraisal is final. **Return to manager**
> is made before the sign-off, in governance, and never while a panel sits — it used to reopen an
> appraisal from **any** status, a Completed or Closed one included, keeping its calibration and HR's
> sign-off. **Remove** takes an appraisal only while nothing in it counts. **HR does not act on their own
> appraisal**: the sign-off, the return, a correction and the raw status routes answer 403 to its
> appraisee — another HR officer does. The raw status routes (`PATCH …/{id}/status`, `POST
> AppraisalWorkflow/{id}/transition`) open a Draft appraisal and close a Completed one with a score, and
> nothing else — they could complete an appraisal nobody signed off, and publish its score. The unused
> *calculate score* route, which restated finished scores, is gone; `GET …/{id}/score-preview` shows
> what the settle would store and writes nothing. When the employee's acknowledgment carries a note, the
> finalised banner shows it.

> **Changed 2026-09-30** (performance closure lane E, slice E-d1). **An appraisal can be withdrawn** —
> taken out of its cycle for someone who left, or should not be appraised this year. Nothing wrote the
> Withdrawn status before, so a leaver's appraisal waited in its cycle for evaluations no one would write,
> counted in every figure. **Withdraw** takes a reason, records who and when, and is offered from Draft,
> Active, or governance until HR's sign-off (and the panel's commit, where the cycle calibrates): a final
> appraisal's result stands. A withdrawn appraisal leaves this queue, the manager's team list, diary and
> interim reviews, the peer queue, the calibration grid, the cycle's progress and the dashboard's counts;
> every write on it is refused. It stays on the employee's record and on this page — a red banner with the
> reason, who withdrew it and when; its scores and grade are not shown to anyone, though they stay on the
> record; the phase rail says it went no further. **Leaving withdraws it too**: a direct termination or a
> separation completing withdraws the leaver's unfinished appraisals in the same save — a signed-off one
> stands, and HR's advance takes it past the acknowledgment. There is no reinstate yet (closure lane N).

### ⚠ Known gaps
**P-43.** ~~**Finalise is not disabled by the calibration gate** (Rule 3).~~ **Fixed 2026-09-29**
(closure lane B1/B4): the button reads the gates.
**P-44.** Finalising **cannot be undone** from the UI. There is no un-finalise — an audited reopen
comes with closure lane N (D-17). Since lane E-a the return can no longer reopen one by accident.
**P-45.** *Remove* is the only route that deletes an appraisal, and it is Admin-tier — established by
probe, not assumed, which is why the control is permission-gated rather than shown and refused.

---

## 32. Acknowledgment, and the employee's written response

### 📍 Where you are
**Route:** `/me/performance/appraisals/{id}` → the **Outcome** tab
**As:** `head.dev` *(whose appraisal you just finalised)* · **3 minutes** — 🔴 **LIVE WRITEs 11 and 12**

### 📖 What it is
The last gate. When the cycle requires it, a finalised appraisal lands in **Governance** and stays there
until the appraisee acknowledges it. Only from Governance, only by the appraisee, and only once — all
three answer 422 or 403 rather than failing quietly.

Alongside it, if the cycle allows it, the employee may record a **written response** that sits with the
appraisal.

*(Since closure lane E-a the acknowledgment itself can carry a short note, kept on the appraisal and
shown on HR's review page. This screen sends none — the written response below is the employee's
answer; the demo pack's acknowledgments send one.)*

### ▶ Walk it

1. In **window B**, sign in as `head.dev`. Go to **My Self-Service → Performance → My Appraisals**.
2. His row now carries a **Next step** of *Acknowledge*. Open it.
3. Read the banner: **"Your appraisal has been finalised — Review the outcome below, then acknowledge
   it to close the appraisal. You cannot undo this."**
4. Go to the **Outcome** tab first. Read **Final score**, **How the score was made up**, and **HR
   remarks** — the remarks you typed two minutes ago.

   💬 *"And there is the sentence I wrote as HR, in front of you, thirty seconds ago — on his appraisal,
   where he reads it. Not in an email. Not in a file."*

5. 🔴 **LIVE WRITE 11 — write a response.** In **Your response**, type:

    `I accept the assessment. I would like the public-sector leadership programme brought forward to the
    first half of 2027 rather than the second.`

    Save. Expect **"Your response is recorded — it sits with the appraisal and is visible to HR."**

   💬 *"And that is his, not HR's. The route that records an employee response is the only one in this
   module that refuses HR outright — because the response row has no author column, so if HR could
   write it, 'the employee's response' would be whatever HR typed and nothing on the record would
   contradict that."*

6. 🔴 **LIVE WRITE 12 — acknowledge.** Press **Acknowledge**. Expect **"Appraisal acknowledged — your
   appraisal is now complete"**, the status to become **Completed**, and the rail to reach the end.

   💬 *"And now it is closed. Goal setting in January, a check-in in September, a self-evaluation, two
   peers, his manager, a calibration panel, HR sign-off, and his own acknowledgment — eight steps, six
   people, one record, and every one of them stamped with who did it and when."*

7. Point at the appeal banner that has just appeared.

   💬 *"And the moment it is complete, his right to contest it opens."*

### ⚙ Behind the page
`POST api/PerformanceAppraisals/{id}/acknowledge` — the employee id in the body is ignored; the token
decides. `POST performance-appraisals/me/{id}/responses` for the written answer.

> **Changed 2026-09-29** (performance closure lane B1). **The acknowledgment waits for the final
> review conversation.** With *Allow acknowledgment before the final conversation* off (as here), it is
> refused until a FinalReview conversation has been held — *"The appraisal cannot be acknowledged yet:
> this appraisal is at Final Conversation — the final review conversation has not been held."* — and
> until then the row's *Next step* is empty, not *Acknowledge*. Holding the conversation moves it on;
> on a cycle with no acknowledgment, holding it completes the appraisal and settles its score.
>
> Kwasi's final review conversation is already held — demo scenario 061, re-run on this database on
> 2026-09-29 — so the sign-off in chapter 31 lands him at *Acknowledgment* and the walk runs as
> written. On a database built before that, his manager (`gm.ops`) holds one first, from the
> conversations panel on his appraisal (chapter 23), or LIVE WRITE 12 is refused.

### ⚠ Known gaps
**P-46.** ~~The cycle's *Allow acknowledgment before the final conversation* switch is **off** on this
profile, which reads as though a final conversation is required first. In practice the acknowledge
endpoint does not check for one.~~ **Fixed 2026-09-29** (closure lane B1): the acknowledgment is refused
until the final conversation is held, unless the switch lets it go first.

---

## 33. Appeals — the employee's side and HR's

### 📍 Where you are
**Routes:**
`/me/performance/appraisals/{id}/appeal` · `…/appeal-status` · `…/appeal-outcome` *(the employee)*
`/hr/performance/appeals` and `…/appeals/[id]` *(HR)*
**As:** `staff`, then `hr.head` · **6 minutes** — 🔴 **LIVE WRITE 13**
**⚠ Read Rule 9 before this chapter.**

### 📖 What it is
An employee contesting a finalised appraisal. Three things make it worth demonstrating:

* **An appeal is per criterion, not per appraisal.** Each contested item carries its own reason, and at
  least one is required. The overall reason is context on top of that, not a substitute. **Any
  criterion the manager scored can be contested** — a competency, a KPI or one of the employee's goals
  (Rule 9) — and every screen of the appeal shows it on its own terms: a KPI by its actual against its
  target and the achievement that gave, a competency by its score.
* **There is one appeal per appraisal, ever.** The server refuses a second, so the form says so before
  the submit rather than after the refusal.
* **Remand is not a verdict.** `Upheld` and `Rejected` are final — the appraisal returns to Completed and
  the appellant is notified. `Remanded` is a process state: it **freezes a snapshot** of the manager's
  evaluation, **reopens that evaluation** until a re-evaluation deadline, and notifies the manager; the
  appraisal stays under appeal, and the employee sees no score until HR decides. The final call happens
  on the post-remand screen once the manager has re-submitted — or once the deadline has passed without
  it — and a *Rejected* there restores the scores from before the remand. *(Since closure C-a,
  2026-09-30: the remand used to roll the appraisal back to `Active` and reopen nothing — P-71.)*
* **HR does not judge an appeal it is party to.** An HR officer cannot pick up, decide, extend or
  finalise an appeal on their own appraisal, or one against an evaluation they wrote or would re-evaluate
  as the employee's manager (403) — another HR officer does (closure D-35).

### 👁 The employee's appeal form
Three tiles — **Final score · Grade · Items you can contest** — then:

* A red banner when you cannot appeal, carrying the server's own reason, written for the employee
* **"One appeal per appraisal"** warning
* **"What are you contesting?"** — every criterion the manager scored, grouped by section as on her form
  (*Key Performance Indicators · Weight 60%*, *Core Competencies · Weight 40%*). A card per criterion,
  each with a checkbox, its kind (**KPI**, **Competency** or **Goal**), its weight within the section,
  and what the manager scored: a KPI as *"Measured: 104 against a target of 100 % — achievement 100 %"*,
  with *"The achievement was restated by calibration"* where the panel moved it; a competency as
  *"Your manager scored 84 of 100"*; and what it contributes. Ticked, a **required** reason box — for a
  competency *"Point to what you did and where the evidence is. Required."*, for a KPI *"What the figure
  should be, and where the record of it is. Required."*
* **"Anything else HR should know"** — optional overall context
* A **Submit appeal** button that refuses until at least one item is selected

### 👁 The employee's status page (`appeal-status`)
Live from submission to verdict. Three tiles — **Score when you appealed · Score now · Items
contested** — then *What you said*, *Items you contested* (Item, with its kind and section · Your reason
· **When you appealed** · and, once decided, **Now** with *Changed by the appeal* or *Unchanged*), and
**HR's response**. Each item's score when appealed is kept at the filing, so it stays what she appealed
against whatever happens next. While the manager re-evaluates on a remand, the scores as they stand —
and *Score now* — are withheld until HR decides. The wording carries the distinction that matters:

| State | What the page says |
|---|---|
| Submitted | **With HR** |
| Under review | **Being reviewed** |
| Remanded | **Sent back for re-evaluation — not yet decided** |
| Upheld | **Upheld** |
| Rejected | **Not upheld** |

### 👁 HR's queue
Five tabs — **New · In review · With the manager · Decided · All** — four tiles, and a table: Employee ·
Cycle · Submitted · Items · Status. The whole list is **HR-only**: it carries every appellant's name and
number.

### 👁 HR's adjudication page
* Header with **Pick up** *(Submitted → UnderReview, stamping the reviewer)* and **Open appraisal**
* Four tiles — Self · Peers · Manager · Overall. *Self* reads a submitted self-evaluation only: a
  draft — one HR waived to move the appraisal on — shows as *No submitted self-evaluation*
* **What the employee says**
* **The contested items**, as a table with **all three evaluation legs side by side**: Criterion (with
  its kind and section) · Weight · **When appealed** · **Self** · **Peers** · **Manager** · Weighted —
  and, *only when the cycle allows it*, a **New score** column with a justification field. A KPI's cell
  is its achievement % over its actual; its new score is an achievement %, a competency's a score on its
  own scale. Scores change only on the contested rows
* An officer party to the appeal — the appellant, the author of the contested evaluation, the
  appellant's line manager — sees *"You cannot act on this appeal"* with the reason, and no buttons
* **Once decided, the page is the record**: *Decided*, the decision with its date and officer, the
  notes, and the overall appealed against the overall now — the queue's *Decided* tab opens it
* Where it does not: a lock banner — *"The settings profile **Standard Annual Appraisal** does not let HR
  change scores while resolving an appeal. To change a score, remand the appeal and let the manager
  re-evaluate."* *(Since closure C-a the advice holds: a remand reopens the manager's evaluation. It was
  a dead end — P-71.)*
* Three buttons: **Send back to the manager** · **Reject the appeal** · **Uphold the appeal**. New scores
  go only with *Uphold*, each with a justification HR writes; the dialog says so when scores are typed
  and another decision is chosen.
* While the manager re-evaluates: a **With the manager** banner with the deadline, **Extend the
  deadline** (a new day and a reason the manager reads), and — once the deadline has passed — **Decide on
  the original scores**
* After the re-evaluation, a **Before and after the remand** comparison — the overall appealed, the
  overall after re-evaluation, the manager's total before → after, and a per-criterion table with
  Before · After · Δ · Appealed (a KPI's achievement over its actual, before and after) — and a single
  **Final decision**: *Uphold* keeps the re-evaluation, *Reject* restores the scores from before the
  remand

### ▶ Walk it

1. In **window B** as `staff` *(Efua)*, open her appraisal and press **File an appeal**.
2. Read the three tiles and the one-appeal warning.
3. Point at the four items on offer, section by section.

   💬 *"Everything her manager scored is on offer. Her two KPIs, which are measured — sales, where she
   beat her target, and delivery timeliness, which the calibration panel restated to eighty-eight per
   cent. And her two competencies — Communication, which her manager scored eighty-four, and Teamwork,
   eighty-two. A KPI she would contest on the figure: what was achieved, or how it was counted. A
   competency on the judgement. She is going to contest a judgement."*

   *(Since closure C-b, 2026-09-30. The page offered the two competencies alone, and this line told the
   room KPIs could not be appealed — though the demo's other appeal, Cynthia Sarpong's, is a KPI's.)*

4. 🔴 **LIVE WRITE 13 — file it.** Tick **Communication**. Reason:

    `I presented the Phase 2 layout to the Development Control Unit on my supervisor's behalf on 2
    August and answered the drainage questions unaided. The Development Control minutes record it.`

    In **Anything else HR should know**:

    `I accept the delivery-timeliness position. This appeal is only about the communication score.`

    Submit. Expect **"Appeal submitted — HR and your manager have been notified. You can follow it from
    here."**

5. Show **My appeal** — the status page — reading **With HR**.

   💬 *"And she can follow it. Not 'ring HR and ask' — follow it, on a page that tells her where it is
   in words rather than in a status code."*

6. Switch to **window A** as `hr.head` and open **Appeals**. The **New** tab now has one.
7. Open it and press **Pick up**.

   💬 *"Picking it up is its own step, and nothing else depends on it. It exists so that a queue of
   untouched appeals is distinguishable from ones somebody is already working through."*

8. Read **What the employee says**, then walk the contested-item table across.

   💬 *"And here is why this screen is worth the effort. One row, five numbers: what she gave herself,
   what her peers gave her, what her manager gave her, the weight, and the weighted contribution. HR is
   not adjudicating on a feeling — it is looking at a disagreement with all three views of it on one
   line."*

   On Efua's Communication row: Self **90**, Peers **82** (her two peers' 84 and 80), Manager **84** —
   84 when she appealed — weight **50 %** of the competencies, contributing **16.8**. *(Since closure C-b:
   the row showed a blank name, Peers "—", a weight of 0 and a weighted 0.0 — two of the five numbers.)*

9. Point at the **Scores are locked on this cycle** banner.

   💬 *"And I cannot simply change it. This year's policy says HR may not modify scores while resolving
   an appeal, and the screen does not offer me the fields. If the appeal has merit, my route is to send
   it back to her manager and make **him** re-evaluate — which is, I would argue, the right answer
   anyway."*

10. Choose your ending. Either is good:

    * **Uphold it.** Press **Uphold the appeal**, notes:
      `Upheld. The Development Control minutes of 2 August confirm the officer presented unaided and
      answered technical questions on the drainage design. The communication assessment is recorded as
      understated and a note to that effect goes on the file.`
    * **Or reject it** — *Reject the appeal*, with your reasons. The original score stands.

    **Recommended: uphold.** It closes the loop inside the session. *A remand works since closure C-a
    (it was a dead end, P-71), but it is the long ending*: Kwasi Danquah re-scores the communication
    item in window B (**Team Appraisals → Efua Seidu**, which opens for him until the deadline), then
    HR makes the final decision on the before-and-after comparison. If a remand is left open, decide it
    before the reset (Appendix E, row 13).

11. Switch back to `staff` and show **appeal-outcome**: HR's decision, her own words, and the final
    scores with the contested one marked. On the recommended ending it reads *"Your appeal was upheld,
    but no score was changed: your overall score stays at 88.56"*, and Communication shows **84 when
    she appealed, 84 final — unchanged**.

    💬 *"Upheld is not the same as re-scored, and the page says which. HR agreed she was
    under-credited, on a policy that does not let HR change a score; if it should move, the remand is
    the route. Either way she is told exactly what happened to her number."*

    *(Since closure C-b: an upheld appeal said "your appraisal scores were adjusted after review" whether
    or not one had moved — the demo's own appeal was upheld at 87 → 87 and said so.)*

### ⚙ Behind the page

Every route hangs off `api/PerformanceAppraisals` and is keyed by the **appraisal** id — there is at
most one appeal per appraisal, so the appeal id is never a path parameter.

| Control | Endpoint |
|---|---|
| Can I appeal? | `GET …/{id}/appeal-page-data` — check `canAppeal`; `cannotAppealReason` is written for the employee to read; `appealableCriteria` is every criterion the manager scored — KPI, competency, goal — each with its kind, section, weight and the manager's score on its own terms *(since C-b: competencies only — C6)* |
| File | `POST …/{id}/submit-appeal` — at least one appealed item, each a criterion the manager scored on this appraisal, once, named by its template item or its snapshot row (or both, naming one row); or 400. Each item keeps what it scored at the filing *(C8 since C-a; the pair rule and the kept score since C-b — D-38)* |
| Follow it | `GET …/{id}/appeal-status` — each item's kind, its score when appealed and, once decided, now; the scores as they stand are withheld while a remand is open *(since C-b: a KPI read "Competency"/"Item", and the page showed the manager's re-scoring)* |
| Final outcome | `GET …/{id}/appeal-outcome` — **400 until Upheld or Rejected**, because a remand is a process state, not a verdict; the message says whether a score moved *(since C-b)* |
| HR queue | `GET …/appeals?cycleId=&status=` — **HR only** |
| HR review | `GET …/{id}/appeal-review` — carries `hrCanModifyScores` from the cycle's profile; names, weights and every leg from the snapshot, no self draft; a decided appeal answers too, with its decision; `partyToAppealReason` when the reader may not act *(since C-b: blank names, weight 0, no peers — C9; a decided appeal answered 400 — D-37)* |
| Pick up | `POST …/{id}/begin-appeal-review` — 403 for an officer party to the appeal *(D-35)* |
| Decide | `POST …/{id}/resolve-appeal` — Upheld, Rejected or Remanded, on an appeal not yet remanded; score changes only with Upheld, only on a contested criterion; 422 otherwise, 403 for a party *(C4, D-35; contested-only since C-b)* |
| Where the remand stands, and the comparison | `GET …/{id}/post-remand-review` — while the manager re-evaluates: the deadline, whether it has passed, `canDecide` / `canExtend`; after: the comparison |
| Extend the deadline | `POST …/{id}/extend-remand` — a later day and a reason, while the manager has not re-evaluated *(D-34)* |
| Post-remand decision | `POST …/{id}/finalize-post-remand-appeal` — Upheld or Rejected, once the manager has re-evaluated or the deadline has passed (422 before); *Rejected*, or any decision after a lapse, restores the scores from before the remand — a calibrated overall included *(C3, C5, D-34 — it waited for nothing and kept the current scores, P-71)* |

**The appellant's three reads resolve the employee from the token.** They used to take it from the
query string, which let anyone read anyone's appeal.

> **Changed 2026-09-29** (performance closure lane L-b). **A goal row can be appealed** — named by its
> snapshot row, which must be this appraisal's. The employee's status page lists it as a *Goal* with
> its target and the manager's actual; HR's review and the outcome name it; an upheld appeal can restate
> it. The appeal **page** still offers competencies only (Rule 9); lane C6 widens it. The remand
> snapshot now keeps each row's actual value and its snapshot row, which a measured goal's score lives
> in and which lane C5's restore will need.
>
> **Changed 2026-09-29** (performance closure lane L-c). **HR's decision screen restates a goal row**:
> an upheld appeal's new score is sent by the row's snapshot row, and the before-and-after comparison
> and the employee's outcome page list goal rows as they do criteria. Only an appeal made through the
> API can name a goal row until lane C6 widens the appeal page.
>
> **Changed 2026-09-30** (performance closure lane C, slice C-b). **The appeal page lists every
> criterion the manager scored**, goal rows and KPIs included, and sends each back by its snapshot row.
> Every appeal read shows a row the same way — its kind, its section and weight, and one score: a rated
> row's on its scale, a measured row's achievement % beside its actual and target (a KPI read "—"
> wherever its actual was the score). Each appealed item keeps what it scored at the filing (D-38), so
> the status and the outcome say *was → now*; the status withholds the scores as they stand while a
> remand is open. HR's review names and weighs its rows from the snapshot, shows the peers' average and
> the weighted contribution, reads no self-evaluation draft, and opens a decided appeal (D-37). The
> outcome says whether a score moved.

### ⚠ Known gaps
**P-47.** ~~**KPI items are not appealable** (Rule 9) — `appealableKpis` is always empty.~~ **Fixed
2026-09-30** (closure lane C, slice C-b): the page lists every criterion the manager scored, KPIs and
goals included (Rule 9).
**P-48.** ~~**The appeal window is not enforced** (Rule 9).~~ **Fixed 2026-09-29** (closure lane B1):
the submit refuses outside the window — counted from the acknowledgment — or with appeals off, and
the appeal page's *can appeal* and *My Appraisals* read the same rule.
**P-49.** ~~The `Weight` column on the appeal review reads from the appraisal's frozen snapshot; where
a snapshot row is missing it falls back to **0**.~~ **Fixed 2026-09-30** (closure lane C, slice C-b):
it read 0 on every row, and a competency's or KPI's name blank; both come from the snapshot now, with
the peers' average and the weighted contribution, which were never set.
**P-50.** ~~The one pre-existing appeal — Cynthia Sarpong's — was filed **against a KPI item** through
the API, which does not validate against the appealable list the form builds. It is therefore an appeal
the UI could not have produced.~~ **Fixed 2026-09-30** (closure lanes C-a and C-b): the submit checks
the list (C8) and the page offers KPIs, so her appeal is one the form can produce. To show a decided
appeal, open it from the Appeals queue's **Decided** tab — it opened to an error until C-b (D-37):
**Project Delivery Timeliness**, a KPI, 92 against a target of 100; upheld at 87 → 87, and her outcome
says no score was changed. Filed before appeals kept each item's score, it shows *When appealed* as "—".

---

# PART VI — OUTCOMES

*Twelve minutes. The part that answers "and then what?" — and the part most appraisal systems do not
have. An appraisal here does not end with a score; it ends with a pay change, a promotion, a training
place or an improvement plan, each one routed to the module that owns it.*

**The chain, in one line:**

```
  manager ticks a recommendation on the appraisal
      →  HR proposes it formally               (AppraisalOutcomeRecommendations)
      →  HR approves it, which DISPATCHES it   (a handler in the owning module)
      →  a real downstream record appears      (SalaryReviewProposal / EmploymentActionProposal /
                                                training request / succession nomination / PIP /
                                                the probation record)
      →  that record goes for approval on the workflow engine
      →  somebody marks it Applied or Actioned when the change is actually made
```

---

## 34. `/hr/performance/recommendations` — what the appraisals asked for

### 📍 Where you are
**Route:** `/hr/performance/recommendations`
**Sidebar:** Human Resources → Talent & Performance → Performance → Recommendations
**As:** `hr.head` · **4 minutes**

### 📖 What it is
HR's cross-organisation queue of appraisal outcome recommendations.

**Three tabs, and the middle one is the important one:**

* **Proposed** — the ordinary inbox.
* **Needs dispatch** — recommendations that were **approved but whose downstream record was never
  created**. The handler failed. They are not finished work, and nothing else in the system will pick
  them up, so they sit here until somebody retries them or actions the outcome in its own module.
* **Closed** — everything settled: actioned, rejected or dismissed.

> **`Approved` is a warning here, not a success.** Approving *dispatches* the recommendation; when that
> works the row lands on **Actioned** with a link to the record it created. A row sitting on **Approved**
> means the dispatch failed and the outcome does not exist yet — so the screen styles it as outstanding
> work and offers a **Retry** rather than a tick.

### 👁 On the page

**Four tiles:** Awaiting your decision · **Needs dispatch** · Actioned · Total.

**The table:** Employee · **Outcome** · Status · Proposed · **Result** — where *Result* links out to the
record the recommendation became, where that screen exists.

**On this database: seven recommendations, all Actioned.**

| Employee | Outcome | Became |
|---|---|---|
| Kojo Fiadzo | Merit increase | **Salary review proposal** |
| Kojo Fiadzo | Training nomination | Training request |
| Efua Seidu | Merit increase | **Salary review proposal** |
| Efua Seidu | Training nomination | Training request |
| Cynthia Sarpong | Merit increase | **Salary review proposal** |
| Cynthia Sarpong | Training nomination | Training request |
| *(TDC/00034)* | Promotion | **Employment action proposal** |

**The twelve outcomes a recommendation can be**, and where each one lands:

| Recommendation | Dispatches to |
|---|---|
| Merit increase · Bonus | **Salary review proposal** |
| Promotion · Demotion · Contract renewal · Termination · Recognition | **Employment action proposal** |
| Training nomination | a training request |
| Succession nomination | a succession nomination |
| Performance improvement plan | a PIP |
| Confirm probation · Extend probation | the probation record |

### ▶ Walk it

1. Open **Recommendations**.
2. Read the tiles. *Needs dispatch* is **0**.

   💬 *"Nothing stuck. That middle number is the one I would watch on a Monday morning — it counts
   recommendations HR approved where the downstream record never got created. They are not failures you
   find out about in December; they are a work queue with a retry button."*

3. Read the seven rows, grouping them by person.

   💬 *"Three people finished their appraisals, and between them their appraisals asked for six things:
   three merit increases and three training places. And every one of them **became** something — look at
   the Result column. Not 'recommended'. Not 'noted'. A salary review proposal, and a training request,
   with a link to each."*

4. Click the **Result** link on Efua Seidu's merit increase. It takes you to chapter 35.

   💬 *"That is the join. The appraisal said she deserved an increase; the system raised the pay
   proposal; and from here it goes to the Managing Director on a published approval route. Nobody
   re-typed her name into a payroll form."*

### ⚙ Behind the page

| Control | Endpoint | Who |
|---|---|---|
| Worklist | `GET api/AppraisalOutcomeRecommendations/worklist?status=` | HR. Capped at 500 rows, newest first |
| By appraisal *(the panel on the appraisal screens)* | `GET …/by-appraisal/{id}` | |
| Propose | `POST api/AppraisalOutcomeRecommendations` | **the appraisee's own manager, or HR** — 403 otherwise |
| Approve | `POST …/{id}/approve` | HR. **Idempotent** — an already-actioned recommendation comes back unchanged |
| Retry | `POST …/{id}/retry-dispatch` | **422 unless the row is Approved-but-not-Actioned** |
| Reject / Dismiss | `POST …/{id}/reject`, `…/dismiss` | **422 once actioned** — the downstream record already exists |

> ⚠ **Approve answers 200 whether or not the dispatch succeeded.** Check the returned `status`.
> `Actioned` means the downstream record exists and `targetEntityId` points at it; `Approved` means the
> handler failed and the item is a work-list entry, not a finished one.

### ⚠ Known gaps
**P-51.** The **Result** column only links out for salary and employment-action proposals. A training
request or a succession nomination shows *"the outcome exists, but its module's screen is not built
yet"* rather than a link that goes nowhere.
**P-52.** Everything on this database is already Actioned, so the *Proposed* and *Needs dispatch* tabs
are both empty. To demonstrate the proposing half, use the **Outcomes** tab on an HR review screen,
which carries the same panel with a **Propose an outcome** form.

---

## 35. `/hr/performance/proposals` — pay changes and employment actions

### 📍 Where you are
**Routes:** `/hr/performance/proposals` · `…/proposals/salary-review/[id]` ·
`…/proposals/employment-action/[id]`
**Sidebar:** Human Resources → Talent & Performance → Performance → Proposals
**As:** `hr.head`, then `md.tdc` · **5 minutes** — 🔴 **LIVE WRITEs 14, 15 and 16**

### 📖 What it is
The two intake queues an approved appraisal outcome feeds. **Neither of these *is* the change.**

* A **salary review proposal** is a note that says *"this person should get 4%"*. **Payroll** makes the
  change, and the proposal is marked **Applied**.
* An **employment action proposal** is a note that says *"promote this person"*. The promotion is created
  in the module that owns it, and the proposal is marked **Actioned**.

Keeping them separate is the point: it means an appraisal can recommend something without seizing the
other module's data model.

> ⚠ **A merit increase with no percentage cannot be submitted.** The handler that raises these only knew
> that an appraisal called for a rise, not how much. That is what the **"Needs a figure"** tile counts —
> and on this database it counts **three**.

### 👁 On the list

**Four tiles:** **Needs a figure (3)** · Out for approval · Approved, not yet done · Total.

A standing note: *"Approval routing is configured, not hard-coded."*

Two tables.

**Salary review proposals**

| Employee | Kind | Proposed | Status |
|---|---|---|---|
| Kojo Fiadzo | Merit increase | **Not set** | Proposed |
| Efua Seidu | Merit increase | **Not set** | Proposed |
| Cynthia Sarpong | Merit increase | **Not set** | Proposed |

**Employment action proposals**

| Employee | Action | Status | Notes |
|---|---|---|---|
| *(TDC/00034)* | Promotion | Proposed | |

### 👁 The salary proposal detail

* Header with the employee's name, the status badge, a link **Open the appraisal**, and the workflow
  **Submit / Approve / Reject / Recall** actions — plus **Mark applied** once Approved
* A banner: **"Needs a figure before it can go for approval"** — *"The recommendation that raised this
  only knew an appraisal called for an increase — not how much. Set a percentage and save, then
  submit."*
* Once approved: **"Approved — nothing has changed yet"** — *"This is an instruction to payroll, not the
  change itself. Mark it applied once the pay change has actually been made."*
* Two tabs: **Details** and **Workflow**
* On Details: **Increase percentage*** · **Bonus amount** *(disabled — "Not used for a merit increase")*
  · **Notes** · **Save**. Below, once it has left *Proposed*: *"Locked: a proposal can only be amended
  while it is still Proposed."*
* The **Mark applied** dialog takes notes — *"Effective date, payroll reference, anything worth
  keeping."*

### 👁 The employment action proposal detail
Deliberately thin: **Employee · Action · Source appraisal · Actioned in** and **Notes**. No target
position, no salary, no award type, no effective date — because demanding those at approval time would
mean an appraisal outcome could not be recorded until somebody had done the destination module's work.
**The real record is created there; marking this Actioned is the receipt.**

The screen names the destination for each action type: Promotion → *Staff promotions* · Demotion →
*Staff demotions* · Contract renewal → *Employee contracts* · Termination → *Employee lifecycle* ·
Recognition → *Awards and nominations*.

### ▶ Walk it

1. Open **Proposals**. Read the **Needs a figure — 3** tile.

   💬 *"Three pay proposals, and not one of them has a number on it yet — which is correct, and it is
   the most honest thing on this screen. The appraisal said 'this person merits an increase'. An
   appraisal does not know what the increase is. Somebody has to decide that, and until they do, the
   proposal cannot go anywhere."*

2. Open **Efua Seidu**'s row.
3. Read the **Needs a figure** banner.
4. 🔴 **LIVE WRITE 14 — set the figure.** **Increase percentage** = `4`. **Notes**:

    `Calibrated score 89.4, Exceeds Expectations. Four per cent in line with the 2026 merit band for
    Exceeds. Carried from the Operations Directorate calibration panel.`

    **Save.** The banner disappears and **Submit** becomes available.

5. 🔴 **LIVE WRITE 15 — submit it.** Press **Submit**. The status becomes **Pending approval** and the
   **Workflow** tab fills with the live instance.

   💬 *"And now it is out for approval — on the generic workflow engine, not on anything this module
   invented. Which is the reason for that line up on the register: the routing is configured. Who signs
   off a four per cent merit increase is a published definition, and on this database it routes to the
   Managing Director."*

6. Go to the **Workflow** tab and show the step, the approver and the history.
7. Switch **window B** to `md.tdc` and open the same proposal.
8. 🔴 **LIVE WRITE 16 — approve it.** Press **Approve**.

   Back on the screen, the status becomes **Approved** and the banner changes to **"Approved — nothing
   has changed yet."**

   💬 *"And read that banner, because it is the discipline of this whole part of the module. Approved is
   not paid. Nobody's salary moved. This is an instruction to payroll, and it stays open until somebody
   marks it applied — which is a receipt that the change was actually made, not a second approval."*

9. *(Optional.)* As `hr.head`, press **Mark applied** with a note like
   `Applied on the October 2026 payroll run; reference PR-2026-10.` The proposal closes.

10. Back on the register, open the **employment action proposal** and read it.

    💬 *"And this one is deliberately thin. A promotion proposal with no target position, no salary and
    no effective date — because if it demanded those, an appraisal outcome could not be recorded until
    somebody had already done the promotions module's job. The intent is captured here; the real record
    is created there; and marking this actioned is the receipt that closes the loop."*

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| Salary list / detail | `GET api/SalaryReviewProposals[?status=]`, `…/{id}` |
| Set the figure | `PUT api/SalaryReviewProposals/{id}` — **422 once the proposal has left Proposed** |
| Submit / Approve / Reject / Recall | `POST …/{id}/submit` etc. — **workflow engine**, `SALARY_REVIEW_PROPOSAL`, published; approvers **Managing Director, TenantAdmin, HR** |
| Mark applied | `POST …/{id}/mark-applied` — **only from Approved** |
| Employment actions | `api/EmploymentActionProposals` — same shape, `mark-actioned` instead; `EMPLOYMENT_ACTION_PROPOSAL`, same approvers |

**The page never sets a status.** The service drives the workflow, the status adapter maps the outcome
onto the record, and the screen refetches and lets it decide.

> **On this database the pay change itself is a separate record**, on the employee's Salary tab: a
> salary change request, applied when approved there. The proposal is the recommendation; the salary
> change request is the instrument.

### ⚠ Known gaps
**P-53.** Three proposals have been sitting at *Proposed* with no figure since the demonstration data
was built. That is the intended state — but if you are not going to do LIVE WRITE 14, say so, or the
tile looks like neglect rather than design.
**P-54.** Nothing on either proposal carries an **effective date**. It goes in the notes.

---

## 36. `/hr/performance/pip` — performance improvement plans

### 📍 Where you are
**Routes:** `/hr/performance/pip` · `…/pip/new` · `…/pip/[id]` · `…/pip/[id]/meetings/[meetingId]`
**Sidebar:** Human Resources → Talent & Performance → Performance → Improvement Plans
**As:** `hr.head` · **5 minutes**

### 📖 What it is
The corrective half of the module: a formal plan agreed where performance has fallen short of what the
role needs, its review meetings, and the outcome it closes with.

**Three scopes, because who may see what differs sharply here.** The org-wide dashboard lists every
employee in the tenant who is on a plan and is **HR only** — a 403 on that tab is the system working,
not a fault. Managers and employees get their own slice from *supervising* and *mine*, which take the
employee from the token.

**Three owners, three stages.** The plan text and its goals belong to whoever is writing it, and are
editable only while it is a **Draft**. **Approval belongs to the workflow engine** — whoever the
published `PerformanceImprovementPlan` definition routes it to, which is why there is no bespoke approve
button. **The outcome belongs to HR** and closes the record.

> ⚠ **`Extended` is not a closure.** It needs a new end date and leaves the plan running.

### 👁 On the list
Three tabs — **Plans I own · My plans · All plans (HR)** — four tiles *(Active · Awaiting approval ·
Overdue · Closed this year)*, and a table: **Plan · Employee · Period · Elapsed · Goals · Status**.

**On this database: one plan.**

| | |
|---|---|
| **`PIP-2026-0001`** | **Patrick Appiah** (TDC/00018), Supervising Civil Engineer |
| Supervisor | Kwasi Danquah *(head.dev)* |
| HR owner | Akpene Amoah *(hr.head)* |
| Period | 31 Aug → 29 Nov 2026 |
| Status | **Active** |
| Goals | 3 |
| Review meetings | **8** |

The issue on the record: *"Repeated late submission of monthly progress reports (4 of the last 6 months)
and two site inspections missed without notice."*

### 👁 The new-plan form (`/pip/new`)
Picking the employee calls `prepare`, which fills in their line manager and the default 90-day window —
and, when the page was opened from an appraisal (`?appraisalId=`), **the score and grade that prompted
it**, so the plan carries its own justification.

Two cards: **Who the plan is for** *(Employee · Position · Department · Supervisor · From appraisal)*
and **The plan** *(Starts · Ends · Performance issues · Expected standards · Improvement actions ·
Support provided · Measurement criteria · Review schedule)* — each with a placeholder worth reading:

| Field | Placeholder |
|---|---|
| Performance issues | *What has fallen short, with specifics and dates.* |
| Expected standards | *What the role requires — the bar being measured against.* |
| Improvement actions | *What the employee will do differently.* |
| **Support provided** | *Coaching, training, cover, adjusted workload — what the organisation is doing.* |
| Measurement criteria | *How success will be judged at the end of the period.* |
| Review schedule | *e.g. fortnightly, Thursdays at 10.* |

The page header says it: *"Saved as a draft. It binds nobody until it has been approved."*

> **Changed 2026-09-29** (closure lane P, P4). A manager opens a plan — and `prepare`, which carries the
> source appraisal's score — **only for a direct report**; HR for anyone except themselves. The
> supervisor is the employee's line manager unless HR names someone else, and the **HR owner must be
> an HR officer with an active login** (422 otherwise): the supervisor and HR owner can read and manage
> the plan, so naming them used to hand anyone the plan. **The employee does not see a draft**, or one
> out for approval, in their list or by id — the Draft status's own contract — until it is in force.

### 👁 The detail page

Header with **Open the appraisal**, the status badge, and the workflow actions. Banners:
*"Needs at least one goal before it can go for approval"* · *"Out for approval — not yet in force"*.

Four tabs:

| Tab | What is in it |
|---|---|
| **The plan** | Six read-only blocks: Performance issues · Expected standards · Improvement actions · Support provided · Measurement criteria · Review schedule and notes |
| **Goals (3)** | Goal · Due · Progress · Status, with add / edit / remove. *"Each goal is one measurable thing that has to change, with a date and a way of telling."* |
| **Reviews (8)** | Date · Conducted by · Attended · Notes, plus **Schedule a review** |
| **Documents** | *"Meeting records, written warnings and evidence of progress belong here."* Streamed through an authorising endpoint — improvement plans are sensitive employment records, so attachments are **never public URLs** |

And a **Record the outcome** panel: **Outcome** *(Performance improved · Extended · Demotion ·
Transferred · Termination)* · **New end date** *(only for Extended)* · **Notes**.

Each outcome carries its own description on the picker:

| Outcome | What it does |
|---|---|
| **Performance improved** | The plan worked. Closes it as successfully completed |
| **Extended** | More time needed. **Keeps the plan running** to a new end date |
| **Demotion** | Closes the plan as unsuccessful and hands the action to HR |
| **Transferred** | Closes the plan as unsuccessful; the employee moves elsewhere |
| **Termination** | Closes the plan as unsuccessful and hands the action to HR |

### 👁 The review-meeting page
The server hands back the **whole form** — the plan's context and a line per goal — and takes the same
object back, so this screen edits one object rather than juggling several calls.

**What was discussed**: Progress since the last review · Issues discussed · Actions agreed.
**Goal progress agreed in this review**: a row per goal with progress and status — and **what is typed
here is written onto the goals themselves when the form is saved**. That is why progress is recorded in
a meeting rather than on the goals tab: what changed and the conversation that agreed it stay together.
**The employee's comments**: their right of reply — *"Replaces whatever is above — this field holds one
comment, not a thread."*

> **Changed 2026-09-29** (closure lane P, P13). The reply is **the employee's own write**: the
> comment box is shown to the plan's employee only, and the server refuses anyone else — the
> supervisor, the HR owner and HR included (it said *"anyone on the plan can enter it here on their
> behalf"*). The supervisor's save carries the whole form back, but the server takes neither the reply
> nor the conductor from it: **whoever books a review holds it**, whatever the body names. The
> employee sees the record read-only — no Save or Record meeting.

> ⚠ A meeting has **no stored status**. "Completed" is derived from its date being in the past, so the
> **Complete** button saves the notes and stamps nothing extra. It is a save with a fuller name.

### ▶ Walk it

1. Open **Improvement Plans** → **All plans (HR)**. One row.
2. Open `PIP-2026-0001`.
3. Read **Performance issues** and **Expected standards** aloud.

   💬 *"Four late monthly returns out of six, and two missed site inspections. And against it, the
   standard: reports by the fifth working day, inspections attended or reassigned twenty-four hours
   ahead. Specific, dated, and measurable — which is what makes this a plan rather than a warning."*

4. Read **Support provided**.

   💬 *"And this is the field I would point at if you take one thing from this screen. 'Time-management
   coaching, two sessions. Reduced drawing load for sixty days.' An improvement plan that lists only
   what the employee must do differently is a disciplinary document wearing a different hat. This one
   records what the Corporation is doing too — and it is a required field."*

5. Go to **Goals (3)** and read them: monthly reports by the fifth working day · every scheduled
   inspection attended or reassigned 24 hours ahead · the two coaching sessions completed.
6. Go to **Reviews (8)** and show the schedule.

   💬 *"Fortnightly reviews, with a formal one at day forty-five and day ninety. Each of them is its own
   record, with notes, attendance, and the employee's own comments."*

7. Open one review meeting and show the **Goal progress agreed in this review** block.

   💬 *"And the progress is agreed in the meeting, not typed afterwards. What goes in here goes onto the
   goals — so the record of what changed and the record of the conversation that agreed it cannot drift
   apart."*

8. Come back and show the **Record the outcome** panel **without saving**. Read the five outcomes and
   their descriptions.

   💬 *"Five ways this ends. One of them — 'Extended' — does not end it at all; it needs a new date and
   the plan keeps running. Two of them close it as unsuccessful and hand the action to HR, which is the
   input to discipline or to separation. And the system does not decide which: a human does, on the
   record, with notes."*

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| Org-wide dashboard | `GET api/PipDashboard` — **HR only, 403 otherwise** |
| Mine / supervising | `GET api/Pip/mine`, `…/supervising` — token-scoped |
| Detail | `GET api/Pip/{id}/detail` — plan, goals, attachments and meetings in one call |
| Prepare | `GET api/Pip/prepare?employeeId=&appraisalId=` |
| Create | `POST api/Pip` — returns the new id; created as a **Draft** |
| Amend | `PUT api/Pip/{id}` — **a draft only** (422 otherwise), and the plan's content only: its employee, source appraisal, supervisor, HR owner, status and outcome are never taken from the body (closure P5, 2026-09-29) |
| Submit / Approve / Reject / Recall | **workflow engine**, `PERFORMANCE_IMPROVEMENT_PLAN`, published; approvers **HR, Manager, TenantAdmin** |
| Running states | `PATCH api/Pip/{id}/status` — Draft / PendingApproval / Active are refused; this moves a **live** plan between its running states |
| Outcome | `POST api/Pip/{id}/outcome` — `Extended` needs `newEndDate` |
| Goals | `POST api/Pip/{pipId}/goals`, then `PUT`/`DELETE api/Pip/goals/{goalId}` — note the **flat** route once a goal exists |
| Meetings | `api/PipMeeting` — `prepare`, `schedule`, create, update, `complete`, `schedule/{pipId}`, `{id}/comment` |
| Attachments | `api/Pip/attachments/{id}/download` — streamed, entitlement checked per request |

**Who can see one:** HR, the employee it is about (once it is in force), the named supervisor and the
named HR owner. Anything keyed on a plan id is 403 for anybody else. **The employee is the employee
whatever else they hold**: an HR officer on a plan reads it and never manages it (closure P4, the
two-actor rule — the desk exemption used to come first).

**Only a draft can be deleted.** A plan that has been in force is **cancelled**, not erased.

### ⚠ Known gaps
**P-55.** The three PIP goals show **no progress** — the status and percentage supplied when they were
created were not honoured, so all three read *Not started* with a blank percentage.
**P-56.** There are **8** review meetings where there should be 2: four identical rows on 14 September
and four on 15 October, created by repeated runs of the data build. Show one; do not count them out
loud.
**P-57.** A review meeting has no stored status — *Completed* is inferred from the date.

---

# PART VII — OVERSIGHT

*Six minutes. Two screens: where the cycle has got to, and what to do when it has stalled.*

---

## 37. `/hr/performance/analytics` — the cycle dashboard

### 📍 Where you are
**Route:** `/hr/performance/analytics`
**Sidebar:** Human Resources → Talent & Performance → Performance → Analytics
**As:** `hr.head` · **4 minutes**

### 📖 What it is
Where a running cycle has actually got to. **Everything on the first four tabs comes from one call**,
which aggregates server-side — nothing here is stitched together from list endpoints, so the figures
cannot disagree with each other the way separately-fetched tiles do.

**Two things on this screen write, and both write notifications rather than records:**

* **Send reminders** is the cycle-wide sweep. It notifies *everyone in scope* for *every* phase whose
  deadline is inside the settings' risk bands. ⚠ It covers goal setting, peer nomination and the final
  conversation as well as the five year-end phases, so it is **not** the same set of steps as the
  deadline table below it.
* **Nudge**, on an attention row, is the targeted one: a single appraisal, reaching only the people who
  can clear its current step.

**Approvals are deliberately absent.** The outcomes this cycle produced run on the workflow engine and
are approved on their own screens; the Outcomes tab counts what is queued and links there rather than
growing a second approval surface.

### 👁 On the page

A cycle picker, **Send reminders**, and **four tiles**: Appraisals in the cycle · Completed · **Average
score** · **Needs attention**.

**Six tabs:**

| Tab | What is in it |
|---|---|
| **Pipeline & deadlines** | A row per stage with its own bar — *deliberately not a funnel*, because the counts do not nest: an appraisal at HR review is *past* self-evaluation, so it counts as complete there and pending here, and the two figures for a stage never sum to the cycle total. Below it, a deadline table: **Phase · Deadline · State · Days left · Done** |
| **Scores** | **Grade distribution** and **Rating distribution**, as horizontal bars |
| **By unit** | **Unit · Head · Appraisals · Complete · Overdue · Avg score · Calibration** |
| **Outcomes** | *What managers asked for* — six counters: Award · Promotion · Increment · Training · Improvement plan · Termination. Then four streams with links out: **Recommendations · Salary review proposals · Employment action proposals · Improvement plans**, and a *Recommendations by type* chart |
| **Activity** | What has happened — withdrawals (with the reason and who made them), acknowledgments, advances, appeals |
| **Employee trend** | One person's overall score across cycles, with a picker |

**And the Needs attention list**, which is the actionable part: **Employee · Unit · Manager · Reason ·
Stuck at · Score · Action** — where *Action* is a **Nudge** button. The reasons are typed:
`OverdueAtStep` · `PIPrecommendation` · `TerminationRecommendation` · `AppealFiled` · `AppealOverdue` ·
`CalibrationAdjustmentLarge` · `ManagerEvalIncomplete` · `LowScore`.

### ▶ Walk it

1. Open **Analytics**. Read the four tiles.

   💬 *"A hundred and seven appraisals, three finished — four, now — an average score in the high
   eighties across the ones that are scored, and a handful that need somebody to do something."*

2. Stay on **Pipeline & deadlines**. Walk the stage bars.

   💬 *"And this is not a funnel, on purpose. A funnel implies people are falling out of the process.
   They are not — somebody sitting at HR review is *past* self-evaluation, so they count as complete
   there and pending here, and the two numbers for a stage will never add up to the total. A row per
   stage with its own bar says exactly that."*

3. Point at the deadline table and the **Passed** states on goal setting and mid-year.

   💬 *"Goal setting closed in January, mid-year in June. Both passed — passed, not late: the phase is
   behind the cycle. Self-evaluation opens in November, and this is where HR would start chasing."*

4. Press nothing, but point at **Send reminders**.

   💬 *"That button raises an in-app notice to everyone in scope for every phase inside the risk bands —
   and it is repeat-safe: if somebody already has the same unread reminder, they do not get a second
   one. Which matters, because the alternative is that HR stops using it in week two."*

5. Go to **Scores**. Show the grade distribution.

   💬 *"Four scored appraisals, all landing in Exceeds Expectations. On a real year that shape is the
   first thing a calibration chair looks at — if everybody is in one band, the band is not doing any
   work."*

6. Go to **By unit**. Point at the **Calibration** column.

   💬 *"Per department: how many appraisals, how many complete, how many overdue, the average, and
   whether calibration has happened. That last column is the one that tells HR which panels still need
   to convene."*

7. Go to **Outcomes**.

   💬 *"And what the whole year produced. What managers asked for, on the left — increments, training,
   promotions. And what those requests actually became, on the right, with the counts sitting behind an
   approver. A cycle can read as finished while its outcomes are still queued, and this is the screen
   that says so."*

8. Go to **Needs attention** and, if there is a row, hover the **Nudge** button.

   💬 *"And the targeted reminder. Not everybody in the cycle — the people who can clear *this*
   appraisal's current step. If the step is not one an individual can be nudged about — it is finished,
   or it is waiting on a calibration panel, or on HR itself — the button tells you so and names the
   step."*

### ⚙ Behind the page

| Element | Endpoint |
|---|---|
| The whole dashboard | `GET api/HRCycleDashboard/{cycleId}` — one pre-aggregated payload |
| Which cycle | `GET api/HRCycleDashboard/active-cycle` — ⚠ answers with the **all-zero GUID**, not a 404, when no cycle is open |
| Nudge | `POST api/HRCycleDashboard/{cycleId}/appraisals/{appraisalId}/nudge` — **422 when the step is not one an individual can be nudged about**; show the message, it names the step |
| Send reminders | `POST api/AppraisalCycle/{cycleId}/deadline-reminders` |
| Rating distribution | `GET api/PerformanceAnalytics/cycle/{cycleId}/rating-distribution` |
| Employee trend | `GET api/PerformanceAnalytics/employee/{id}/trend` — readable by **HR, the employee, and their line manager** |

> ⚠ **Both analytics reads are derived from `overallScore`, which is only set at HR sign-off.** Early in
> a cycle they are legitimately empty; that is not an error state, and each chart says so in words
> rather than drawing an empty axis.

> **Changed 2026-09-29** (performance closure lane B1). **The stage counts, the attention rows and
> Nudge read the same gates as every write** — the dashboard used to run its own, stricter resolver
> while the writes enforced a looser one. A row's step (*"Overdue: Goal Setting"*) is the step the
> phase rail shows and a refused write names, with the same reason; goals are counted by employee and
> cycle, so a goal agreed before generation counts. *Goal Setting* is still most of APC2026 — most
> staff have no agreed goals — as it was.

> **Changed 2026-09-30** (performance closure lane E, slice E-d1). **A withdrawn appraisal is out of every
> figure** — the tiles, the stages and deadlines, the scores, each unit's row (it counted as *overdue*), the
> outcome counters and the attention list — and the *Activity* tab records its withdrawal. And **the page
> loads its appraisals in separate queries**: as one, it asked SQL Server for some 700 MB of working memory
> on a fresh start and could wait past the 30-second timeout for it — a 500 even for a small cycle, 13
> seconds cold on APC2026 (2 now).

### ⚠ Known gaps
**P-58.** With four scored appraisals the distributions are a single bar. Honest, but not impressive —
consider skipping the *Scores* tab if the room is small and sceptical.
**P-59.** *Needs attention* may be empty on this database. If it is, say what would put a row in it.

---

## 38. `/hr/performance/deadline-enforcement` — the override

### 📍 Where you are
**Route:** `/hr/performance/deadline-enforcement`
**Sidebar:** Human Resources → Talent & Performance → Performance → Deadline Enforcement
**As:** `hr.head` · **2 minutes**

### 📖 What it is
HR's manual override on a stalled pipeline. **Both actions are audited against you**, and **there is no
background job** — nothing here happens on a schedule; somebody has to run it.

Two actions:

* **Sweep a cycle** — advances every active appraisal whose current step is overdue.
* **Advance one appraisal** past one stalled sub-step, with a reason.

### 👁 On the page

A standing banner: **"Every override is recorded against you."**

**The sweep**: a cycle picker and a **Run the sweep** button, which returns **Examined · Advanced ·
Auto-lock**, a *What happened* list — and, when the setting is off, the line
**"Auto-lock is off for this cycle — nothing was changed."**

**The single advance**: **Appraisal id** *(paste it from the appraisal)* · **Step to advance past**
*(or "Whatever is blocking")* · a reason box — *"Why the step is being skipped, and on whose
authority."*

The steps you can name: Goal setting · Peer nomination · Self-evaluation · Peer evaluation · Manager
evaluation · Pending calibration · Calibration in progress · Pending HR review · HR review in progress ·
Pending conversation · Pending acknowledgment · Appeal submitted · Appeal under review · Appeal
resolved.

### ▶ Walk it

1. Open **Deadline Enforcement**. Read the banner.
2. Select the cycle and press **Run the sweep**.

   Expect: **Examined** some number, **Advanced 0**, and the line **"Auto-lock is off for this cycle —
   nothing was changed."**

   💬 *"And that is the answer I want you to see, because it is the difference between a system that
   reports a successful no-op and one that tells you the truth. It examined the overdue appraisals. It
   changed none of them — because this cycle's policy has 'advance overdue appraisals on deadline'
   switched off, and the endpoint honours the setting rather than ignoring it. If the screen just said
   'zero advanced' you would reasonably conclude nothing was overdue."*

3. Point at the single-advance form and the reason box.

   💬 *"And the individual override. One appraisal, past one step, with a reason — because somebody has
   left, or is on long leave, and a hundred other people's cycle cannot wait on them. It is recorded
   against me, by name, with what I typed."*

4. **Do not advance anything.**

### ⚙ Behind the page

| Control | Endpoint |
|---|---|
| Sweep | `POST api/DeadlineEnforcement/enforce/{cycleId}` — **honours `autoLockOnDeadline`**; check `autoLockEnabled` on the result and say so, rather than reporting a successful no-op |
| Advance one | `POST api/DeadlineEnforcement/advance/{appraisalId}` — `targetSubStatus` optional |

Both sit on **`HR.Performance.Write`**, and the advancing officer comes from the token and is written to
the audit log.

> ⚠ A refused advance answers **422** with the reason when it names a step the appraisal is not at,
> and **400** with the reason for a finished or appealed appraisal. *(Corrected 2026-09-29 — this said
> 200 with `success: false`; the controller has always turned that into a 400.)*

> **Changed 2026-09-29** (performance closure lane B1). **An advance moves the appraisal past the step
> it is at, and no other**: naming a different step is refused (*"This appraisal is at Goal Setting,
> not Manager Evaluation…"*), so leave the picker on *Whatever is blocking*. **Before the manager's
> evaluation the advance is a waiver**: the log row it writes is what lets the appraisal past goal
> setting, peer nomination, the self-evaluation or the peer minimum, however few goals, nominations or
> peers it has — and a waived self-evaluation leaves the employee's draft a draft, counted nowhere (it
> used to submit the draft for them). From the manager's evaluation on, it still writes the record the
> step needs — the evaluation, the calibration stamp, the sign-off, the conversation, the
> acknowledgment — because a return to the manager or a remand re-opens those steps, and a waiver
> would outlive the re-opening. The major status then follows the gates, a Draft appraisal is opened,
> and completion settles the score. The sweep resolves and advances each appraisal the same way.
>
> **Changed 2026-09-29** (performance closure lane L-a). **The advance past goal setting locks the
> agreed set.** The goals the employee submitted are approved on HR's recorded reason, and every
> approved or running goal is locked, so the year is appraised on what was agreed; a draft (never put
> to the manager) and a rejected goal (refused by them) are left out. It used to approve drafts and
> rejected goals along with the rest and lock nothing. The log lists each part — *"Approved 1
> submitted goal(s) on HR's recorded reason."*, *"Locked the agreed goal set: 2 goal(s)."*, *"Left 2
> draft or rejected goal(s) out of the set."*, then the waiver. Where the template has a goals section
> the log adds *"Built the goals section: 2 goal row(s)."* (lane L-b, § 1.4).

### ⚠ Known gaps
**P-60.** The single-advance form asks for an **appraisal id** — a GUID, pasted from the address bar.
There is no picker. It is a deliberately awkward control for a deliberately exceptional act, but it does
mean you cannot demonstrate it without copying a GUID.
**P-61.** `autoLockOnDeadline` is **off** on the only settings profile, so the sweep can never advance
anything on this database. That is the demonstration, not a limitation — but do not promise the sweep
will move something.

---
## 39. The close — how to land it

Two minutes. Go back to `/hr/performance` — the hub you started on — and say this, or something close
to it:

💬 *"Let me put back together what we have just taken apart.*

*One policy profile, set once, deciding who evaluates and at what weight. One form, weighted section by
section and line by line, and frozen the moment a cycle started scoring people on it. One cycle,
covering the organisation by rule rather than by list, with a coverage check that simulates the whole
thing before a single record is created.*

*Underneath it, a goal cascade: a three-year strategy, this year's corporate objectives, each
department's translation of them, and each person's own three goals — drafted by them, approved by the
person the establishment says they report to, and tracked all year by a system that works out for
itself when one is drifting.*

*Through the year: one-to-ones whose conclusions move the goals rather than sitting in a note, a
journal so that December is not written from memory, and mid-year checkpoints the cycle generated
without anyone scheduling them.*

*Then the run itself: an employee scoring herself, two peers scoring her behaviours, her manager
scoring her with her own answers on the same line, a panel reconciling the directorate before anything
reached HR, HR signing off the arithmetic, and the employee closing it with an acknowledgment and a
written response of her own.*

*And out the other end — not a score. A four per cent pay proposal on its way to the Managing Director,
a training place in next year's plan, and, for somebody else, an improvement plan with fortnightly
reviews and a named HR owner.*

*That is the difference I would want you to take away. Most appraisal systems produce a number. This one
produces a decision, routed to whoever owns it, with every step attributable to the person who took
it."*

Then stop. Do not open another screen.

---

# APPENDIX A — Route index

Every route in the module, its gate, and who should open it.

### Desk — `/hr/performance/*`

| Route | Chapter | Gate | Open it as |
|---|---|---|---|
| `/hr/performance` | 12 | *open* | `hr.head` |
| `/hr/performance/analytics` | 37 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/cycles` | 13 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/cycles/[id]` | 14 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/team-appraisals` | 29 | *open* | `head.dev` |
| `/hr/performance/team-appraisals/[id]` | 29 | *open* — service checks you are the manager | `head.dev` |
| `/hr/performance/interim-reviews` | 24 | *open* | `head.dev` |
| `/hr/performance/interim-reviews/[id]` | 24 | *open* — HR / appraisee / manager | `head.dev` |
| `/hr/performance/conversations` | 23 | *open* | `staff`, `head.dev` |
| `/hr/performance/conversations/[id]` | 23 | *open* — HR / appraisee / manager / scheduler | `head.dev` |
| `/hr/performance/development-plans` | 25 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/pip` | 36 | *open* — tabs differ by role | `hr.head` |
| `/hr/performance/pip/new` | 36 | *open* | `hr.head` |
| `/hr/performance/pip/[id]` | 36 | *open* — HR / employee / supervisor / HR owner | `hr.head` |
| `/hr/performance/pip/[id]/meetings/[meetingId]` | 36 | as above | `hr.head` |
| `/hr/performance/hr-review` | 31 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/hr-review/[id]` | 31 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/calibration` | 30 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/calibration/[id]` | 30 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/appeals` | 33 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/appeals/[id]` | 33 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/recommendations` | 34 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/proposals` | 35 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/proposals/salary-review/[id]` | 35 | `HR.Performance.Read` | `hr.head`, then `md.tdc` |
| `/hr/performance/proposals/employment-action/[id]` | 35 | `HR.Performance.Read` | `hr.head`, then `md.tdc` |
| `/hr/performance/company-goals` | 15 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/company-goals/[id]` | 15 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/unit-goals` | 16 | *open* — narrowed server-side for a Manager | `hr.head`, `head.dev` |
| `/hr/performance/unit-goals/[id]` | 16 | *open* | `hr.head` |
| `/hr/performance/employee-goals` | 18 | *open* | `hr.head` |
| `/hr/performance/employee-goals/[id]` | 18 | *open* | `hr.head` |
| `/hr/performance/team-goals` | 19 | *open* — manager-scoped | `head.dev` |
| `/hr/performance/at-risk` | 20 | `HR.Performance.Read` | `hr.head` |
| `/hr/performance/deadline-enforcement` | 38 | `HR.Performance.Write` | `hr.head` |

### Setup — `/administration/hr/performance/*` *(all gated on `admin.hr` + the per-action policy)*

| Route | Chapter | Write needs | Delete needs |
|---|---|---|---|
| `/administration/hr/performance` | 3 | — | — |
| `…/settings` · `…/settings/[id]` | 4 | `HR.Performance.Write` | **Admin** |
| `…/criteria` | 5 | Write | Write |
| `…/grade-definitions` | 6 | Write | Write |
| `…/kpi-definitions` | 7 | Write | Write |
| `…/templates` · `…/templates/[id]` | 8 | Write | **Admin** |
| `…/strategic-goals` | 9 | Write | Write *(400 while company goals link)* |
| `…/goal-library` · `…/goal-library/[id]` | 10 | Write | Write |
| `…/goal-risk-settings` | 11 | Write | — |

### Self-service portal — `/me/performance/*`

| Route | Chapter | On the portal menu? | Open it as |
|---|---|---|---|
| `/me/performance/appraisals` | 26 | ✅ My Appraisals | `staff` |
| `/me/performance/appraisals/[id]` | 26 | via the list | `staff` |
| `…/[id]/self-evaluation` | 27 | via the appraisal | `staff` |
| `…/[id]/appeal` | 33 | via the appraisal | `staff` |
| `…/[id]/appeal-status` | 33 | via the appraisal | `staff` |
| `…/[id]/appeal-outcome` | 33 | via the status page | `staff` |
| `/me/performance/goals` | 17 | ✅ My Goals | `staff` |
| `/me/performance/peer-reviews` | 28 | ✅ Peer Reviews | `new.hire` |
| `/me/performance/peer-reviews/[id]` | 28 | via the list | `new.hire` |
| `/me/performance/check-ins` | 21 | ✅ My Check-Ins | `head.dev` |
| `/me/performance/check-ins/[id]` | 21 | via the list | `head.dev` |
| `/me/performance/journal` | 22 | ✅ My Journal | `head.dev`, `staff` |
| `/me/performance/development-plans` | 25 | ✅ My Development Plans | `staff` |
| `/me/performance/development-plans/[id]` | 25 | via the list | `staff` |
| `/me/performance/interim-reviews` | 24 | ❌ **not on the menu** | `staff` |

---

# APPENDIX B — The demo data card

*Tear this out and keep it beside the laptop.*

```
  LOGINS                                   password for all: Demo123!
    hr.head      Akpene Amoah      TDC/00009   Head of HR & Administration
    head.dev     Kwasi Danquah     TDC/00006   Head of Development
    staff        Efua Seidu        TDC/00017   Project Coordinator
    md.tdc       Nana Nyaho        TDC/00001   Managing Director
    new.hire     Kojo Ansah        TDC/00063   Supervising Architect
    she.officer  Cynthia Sarpong   TDC/00081   Environmental Officer
    gm.ops       Kojo Fiadzo       TDC/00003   General Manager - Operations
    she.manager  Josephine Appiah  TDC/00071   HSE Supervisor
    auditor      Yakubu Aryee      TDC/00004   Chief Internal Auditor

  THE CYCLE    APC2026 · Annual Performance Cycle 2026 · 1 Jan – 31 Dec · Open
  THE POLICY   Standard Annual Appraisal · self 10 / peers 20 / manager 70 · 2–4 peers
               calibration ON · HR review ON · acknowledgment ON · appeals ON (7 days)
               goals 3–6 · check-ins ON · private journal ON · auto-advance OFF
  THE FORM     Standard Employee Template 2026
               KPIs 60%  : Sales Target Achievement 50 · Project Delivery Timeliness 50
               Comps 40% : Communication 50 · Teamwork 50 · free-text question (weight 0)
  GRADES       0–40 Unsatisfactory · 41–55 Below · 56–75 Meets · 76–90 Exceeds · 91–100 Outstanding

  APPRAISALS   107   73 Draft · 30 Active · 1 Governance · 3 Completed · 76 calibrated

    Efua Seidu     COMPLETE     self 94.40  peers 84/80    mgr 90.80   FINAL 88.56  Exceeds
    Kojo Fiadzo    COMPLETE     self 94.00  peers 81.5/77.5 mgr 91.40  FINAL 89.28  Exceeds
    Cynthia S.     COMPLETE     self 94.00  peers 83/79    mgr 90.20   FINAL 87.00  Exceeds  + appeal UPHELD
    Kwasi Danquah  WITH HR  ←   self 92.20  peers 80/76    mgr 88.80   FINAL —      (finalise this one)
    Kojo Ansah     ACTIVE       self 85.60  —              —           —

  EFUA'S GOALS   Community 25 Phase 2 layout        w40   70%  On track
                 Drawing-office turnaround → 5 days w30   55%  AT RISK  (flagged in the Q3 one-to-one)
                 Civil 3D certification             w30   40%  In progress

  CASCADE      1 strategic → 2 company → 3 unit → 22 employee goals
               ⚠ NO TDC employee goal is aligned to anything (fix one live — chapter 18)

  PANELS       Operations Directorate (4 panellists, 1 adjustment)   both COMPLETED + COMMITTED
               Finance & Administration (3 panellists, 1 adjustment)
               ⚠ Open tab is EMPTY.  Uncovered units for a live session:
                 Corporate Planning & Communications Dept (9) · Internal Audit (6)
                 Legal (4) · Procurement (4) · MERC (3)

  OUTCOMES     7 recommendations, all Actioned
               3 salary proposals, all Proposed, ALL WITH NO FIGURE  ← chapter 35
               1 employment action proposal (promotion), Proposed
               PIP-2026-0001  Patrick Appiah · supervisor head.dev · Active · 3 goals

  RISK RULE    14 days / 60% / 20%.  On 17 Sep a calendar-year goal is 71.2% through its life,
               so anything under ~51% is flagged.  Expect ~16 at-risk goals across 9 people.

  NEVER OPEN   any appraisal numbered APR-2026-001 … 005  (no criterion snapshot — opens empty)
  NEVER PRESS  Delete, anywhere in this module (hr.head has Write, not Admin → 403)
```

---

# APPENDIX C — Known gaps, ranked

Sixty-two findings, ordered by how likely each is to matter in front of an audience.

### The settings audit — read this before you describe the policy screen

A separate field-by-field audit (**[`HR-APPRAISAL-SETTINGS-AUDIT.md`](HR-APPRAISAL-SETTINGS-AUDIT.md)**)
found that **14 of the 50 fields on the Appraisal Settings profile do not enforce what they say**:
3 ghosts (P-62), 2 client-side-only visibility controls (P-63), 4 governance switches only the
analytics pipeline honours (P-64, P-67), 2 appeal switches the submit ignores (P-65), a peer window
that only changes a notification (P-66), an "authoritative" flag that changes a sort order (P-68), a
mislabelled soft-skill switch (P-69) and an unenforced goal minimum (P-70). The block at the head of
§ 1.6 lists exactly which to avoid claiming.

### The six that will bite you on stage

| # | Gap | Where |
|---|---|---|
| **P-39 / P-41** | ~~The panel's **overall** calibrated score is overwritten by HR sign-off~~ — **fixed 2026-09-29**; the grid and the HR review now agree, *except* on the two appraisals signed off before the fix (Cynthia Sarpong, Efua Seidu), which keep their signed-off figures | ch. 30, Rule 2 |
| **P-43** | **Finalise is not disabled by the calibration gate**; on an uncalibrated appraisal it 422s | ch. 31, Rule 3 |
| **Rule 8** | Five appraisals have **no criterion snapshot** and open every evaluation screen empty | Rule 8 |
| **P-18 / Rule 5** | No TDC employee goal is aligned to anything, so every unit goal's cascade table is empty | ch. 16, Rule 5 |
| **P-29 / Rule 6** | The conversations **diary is empty** for every persona, and the calibration **Open tab** is empty | ch. 23, ch. 30 |
| **P-4 etc. / Rule 7** | Every **Delete** in the module renders for `hr.head` and answers 403 | throughout |

### Correctness

| # | Gap |
|---|---|
| ~~**P-40**~~ | ~~A per-criterion calibration adjustment on a **KPI item** writes `NumericScore`, which a KPI is not scored from — so it changes nothing~~ — **fixed 2026-09-29** (every item adjustment now reaches the score) |
| ~~**P-47**~~ | ~~**KPI items are not appealable** — `appealableKpis` is always empty~~ — **fixed 2026-09-30** (closure lane C-b; Rule 9): every criterion the manager scored is on the page |
| ~~**P-48**~~ | ~~The **appeal window is not enforced**; eligibility is "Completed and unappealed"~~ — **fixed 2026-09-29** (closure lane B1; Rule 9) |
| ~~**P-71**~~ | ~~**A remand is a dead end** *(found 2026-09-29, lane L-b)*. It snapshots the manager's evaluation and re-opens the appraisal, but leaves the manager's evaluation marked submitted, so every save of the re-evaluation is refused; the post-remand decision does not wait for one, and *Rejected* keeps the current scores rather than the snapshot's.~~ **Fixed 2026-09-30** (closure lane C, slice C-a): the remand reopens the evaluation until its deadline (HR can extend it), the final decision waits for the re-evaluation or the deadline, and *Rejected* restores the original |
| **P-28** | **Journal entry dates are ignored** on create; the server stamps the creation date |
| **P-55** | PIP goal **status and progress supplied at creation are not honoured** |
| ~~**P-6**~~ | ~~Overall grade bands are **not validated against each other**; overlaps resolve to whichever is found first~~ — **fixed 2026-09-29** (refused at save) |
| ~~**P-49**~~ | ~~The appeal review's Weight column falls back to **0** where a snapshot row is missing~~ — **fixed 2026-09-30** (closure lane C-b): it was 0 on every row; weights and names come from the snapshot |
| **P-13** | Generation **skips** anyone who already has an appraisal, and cannot repair a bad one |

### Design decisions that read like gaps

| # | What | Why it is right |
|---|---|---|
| **P-19** | Unit-goal attachments are readable by anyone in the tenant | A departmental target is not personal data — but do not attach anything sensitive |
| **P-27** | `privateNotes` come back on every check-in read | The client hides them; the API trusts the client here |
| **P-23** | `getGoalDetail` 404s for both "no such goal" and "not your report" | So the endpoint cannot be used to probe reporting lines |
| **P-21** | HR cannot approve a goal from the desk register | Approval is the direct manager's, by design |
| **P-61** | The deadline sweep can never advance anything here | `autoLockOnDeadline` is off, and the endpoint honours it and says so |
| **P-45** | Removing an appraisal is Admin-tier | Established by probe, not assumed |

### Demo-data gaps *(not defects — this database, today)*

| # | What |
|---|---|
| **P-10** | No goal has been created from a goal-library template, so every usage figure is 0 |
| **P-26** | Exactly **one** check-in exists |
| **P-31** | All 102 live interim checkpoints sit at **Pending** |
| **P-32** | `/me/performance/interim-reviews` is not on the portal menu |
| **P-35 / P-36** | Every self-evaluation and peer evaluation is already submitted, so those forms open read-only |
| **P-38** | Seven of `head.dev`'s eight reports have no goals and no evaluations |
| **P-42** | Only 76 of 107 appraisals are calibrated |
| ~~**P-50**~~ | ~~The one existing appeal was filed against a **KPI**, which the UI could not have produced~~ — **fixed 2026-09-30** (closure lanes C-a, C-b): the submit checks the list and the page offers KPIs |
| **P-52** | Every recommendation is already Actioned, so two of the three tabs are empty |
| **P-53** | Three salary proposals have no figure — intended, but say so if you skip LIVE WRITE 14 |
| **P-56** | The PIP has **8** review meetings where it should have 2 (duplicates from repeated data builds) |
| **P-58 / P-59** | With four scored appraisals the distributions are a single bar, and *Needs attention* may be empty |

### Cosmetic and minor

**P-1** admin.hr gates the whole setup tree · **P-2** no "default profile" concept · **P-3** appeal
windows displayed not enforced · **P-5** criterion codes truncate to 10 characters · **P-7** template
freeze is client-side pessimistic · **P-9 / P-12 / P-17** more 403-ing deletes · **P-11** goal-risk
thresholds are tenant-wide with no per-cycle override · **P-14** Q1/Q3 dates unset · **P-15** estimated
headcounts are 0 · **P-16** the company-goal list carries only a description preview · **P-20** an
unbalanced goal set is flagged, not refused · **P-22** the goal-library link is set only at creation ·
**P-24** at-risk filters match the employee's unit, not the goal's · **P-25** no export on the at-risk
report · **P-30** the conversations list cannot schedule · **P-33** a self-created development plan is
born Active, a report's is born Draft, from the same button · **P-34** the Outcome tab is empty before
sign-off · **P-37** uploads 422 without the scanner running · **P-44** finalising cannot be undone ·
**P-46** acknowledgment does not check for a final conversation · **P-51** only two of six dispatch
targets have a screen to link to · **P-54** proposals carry no effective date · **P-57** a PIP meeting
has no stored status · **P-60** the single-advance form wants a pasted GUID.

---

# APPENDIX D — The 25-minute short path

When the room has half an hour and you need the whole arc. Skip Part I entirely.

| min | Screen | The one thing |
|---|---|---|
| 0–3 | `/hr/performance/cycles/{id}` → **Coverage** | *"Before a single record is created, the system simulates the whole cycle and tells you who it could not place."* |
| 3–5 | `…/settings/[id]` → Evaluation | *"Three evaluators, weights that must total one, and the API zeroes a disabled one before it checks."* |
| 5–8 | `/me/performance/goals` as `staff` | Three goals, weighted to 100. **Record progress** live. *"The newest entry becomes the goal's own status."* |
| 8–10 | `/hr/performance/team-goals` as `head.dev` → Overview | Governance per report. **Approve** a goal live. *"Nobody chose an approver."* |
| 10–12 | `/me/performance/check-ins/{id}` as `head.dev` | The Q3 one-to-one and its three goal updates. *"The at-risk flag was set in this conversation."* |
| 12–16 | `/hr/performance/team-appraisals/{id}` → Evaluation | The self-score beside the manager's on every line. Then **Assessment & recommendations**. |
| 16–18 | `/hr/performance/calibration/{id}` → Decisions | One panel decision, with its reasoning and its author. *"Nothing reaches HR until a panel releases it."* |
| 18–22 | `/hr/performance/hr-review` → Ready → Kwasi Danquah | The four legs, then **Finalise** live. *"The final score does not exist until I press this."* |
| 22–25 | `/hr/performance/proposals` → Efua's salary proposal | Set 4%, **Submit**, approve as `md.tdc`. *"Approved is not paid."* |

Close on the hub with the paragraph from chapter 39.

---

# APPENDIX E — Reset: what the walk changed, and how to put it back

Sixteen numbered live writes. Column three tells you how to undo each one; where it says **cannot**,
either accept it or rebuild the demonstration database.

| # | Chapter | What it did | How to reverse it |
|---|---|---|---|
| **1** *(opt.)* | 11 | Changed the goal-risk tolerance | Set **Tolerance** back to **20** and save, or press **Reset to defaults** |
| **2** | 17 | Progress entry on Efua's Civil 3D goal (40% → 55%) | Open `/hr/performance/employee-goals/{id}` → *Progress entries* → delete the newest row. **The goal keeps the 55%** — re-add an entry at 40% to restore it |
| **3** | 17 | Created a fourth goal for Efua, weight 0 | As `hr.head`, Employee Goals → **⋯** → **Delete** on *Mentor one draughtsman…* *(only while unlocked)* |
| **4** | 19 | `head.dev` approved that goal | Delete the goal (as above) and it goes with it |
| **5** | 21 | Scheduled a Q4 check-in with Efua | `DELETE api/CheckIns/{id}` — there is no delete button on the screen |
| **6** | 18 | Aligned Efua's layout goal to the Development unit goal | Edit the goal, set **Aligned to** back to blank |
| **7** | 23 | Scheduled a Q4 conversation | `DELETE api/AppraisalConversations/{id}` |
| **8** | 30 | Created + opened the *Corporate Planning & Communications* panel | Deleting a session needs **`HR.Performance.Admin`**, so **not as `hr.head`** — and **422 once completed**. Sign in as `admin` before you close it, or leave it |
| **9** | 30 | Closed **and committed** that panel | **Cannot.** Committing is irreversible, and it marks 9 appraisals calibrated |
| **10** | 31 | **Finalised Kwasi Danquah's appraisal** | **Cannot.** There is no un-finalise |
| **11** | 32 | Recorded his written response | **Cannot** from the UI |
| **12** | 32 | He acknowledged it | **Cannot** |
| **13** | 33 | Efua filed an appeal | **Resolve it in the same sitting** (Upheld or Rejected) — that returns her appraisal to Completed. If it was remanded, finish the remand: Kwasi Danquah re-evaluates (or the deadline passes), then HR's final decision returns it to Completed — *Rejected* restores the scores she appealed |
| **14** | 35 | Set 4% on her salary proposal | Editable only while *Proposed* — clear it **before** you submit, or it locks |
| **15** | 35 | Submitted that proposal | **Recall** it from the same screen |
| **16** | 35 | `md.tdc` approved it | **Cannot** — but you can leave it Approved rather than marking it applied |

**The clean reset**, if the database is rebuilt between runs:

```
powershell -File .\scripts\New-UatDatabase.ps1
```

which reinstalls the EF layer and then runs `Invoke-UatDemoScenarios.ps1` to rebuild the transactional
layer through the API. **Re-running the scenarios alone is not a reset** — they are written as *ensure*
steps: they complete what is missing and leave what already exists.

---

# APPENDIX F — If something goes wrong on stage

| What you see | What it is | What to say / do |
|---|---|---|
| **Red toast: "This cycle requires calibration…"** | You pressed Finalise on an uncalibrated appraisal (Rule 3) | *"And there is the gate doing its job — this one has not been through a panel."* Move on |
| **"This appraisal has no scoreable criteria"** | You opened one of the five fixture appraisals (Rule 8) | Close it. Open an appraisal by **employee name**, never by number |
| **A 403 page after clicking Delete** | `hr.head` has Write, not Admin (Rule 7) | *"Destroying an appraisal instrument is an administrator's act, not HR's."* Move on |
| **An upload answers 422** | The malware scanner is not running (P-37) | *"Every HR document goes through a scan gate, and with no scanner answering the gate correctly refuses."* Skip the tab |
| **A screen is empty that you expected to be full** | Check Rule 6 (diary, calibration Open tab), P-31 (interim reviews), P-52 (recommendations tabs) | Say what *would* put a row in it |
| **The at-risk count is not 16** | It moves with today's date (Rule 4) | Read the tile out loud and explain the rule, not the number |
| **`hr.head` sees no menu under Administration** | You are in window B | Switch windows |
| **A goal action answers 403** | You are not that employee's direct manager | *"Approval is not a permission here — it is a fact about the reporting line."* |
| **The workflow tab is empty on a proposal** | It has not been submitted yet | Set the figure, then submit |
| **Everything is slow on first load** | Cold start | Pre-open the fourteen tabs in §2.4 |

---

*End of the Performance guide. Sixty-two screens, thirty-nine chapters, sixteen live writes, sixty-one
recorded gaps.*
