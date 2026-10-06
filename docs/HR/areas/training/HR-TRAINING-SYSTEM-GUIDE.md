# HR Training & Learning — System Guide and Demonstration Workbook

**Status:** written 2026-10-06 from the source, the demo seeders and the demo scenario scripts
(`dev-harness/hr-demo-smoke/scenarios/040-training.mjs`, `041-learning-and-mentoring.mjs`,
`TdcDemoTrainingBudgetSeeder`). The sources were:
- the 27 desk pages under `/hr/training`, `/hr/service-bonds`, and the 8 setup areas under
  `/administration/hr/training`;
- the 13 portal pages under `/me/training`, `/me/learning` and `/me/mentoring`;
- the training controllers, services, entities, enums and the permission map;
- the seeded demonstration data.

It describes what the code is built to do. Where a screen promises something the server does not do,
the step says so rather than smoothing it over. ⚠ **Nothing in it has been walked in a browser.**
Every screen was read, not clicked, and **no number below was read off a live database** — the
numbers come from the seed scripts, and each one that depends on the day you run the demo says so.
**Do one full rehearsal on a fresh rebuild before you present** (§ 1.5 is the checklist).

**Scope:** the whole **Training** group of the HR sidebar, the **Mentoring** group, the **Training &
Learning** setup area under Administration → HR, the **Service Bonds** screen, and the employee
portal's **My Training**, **My Learning Paths** and **My Mentoring**.

Safety training (`/hr/safety/training`) is a different store — it records contractors and statutory
expiry — and is **not** covered here (see chapter 31, question 12).

---

## This document is two things at once

Like the other guides in this series, this is a **reference** and a **script you can perform**.
Every chapter has the same parts, and you can read only the ones you need:

| Part | Marked | Use it for |
|---|---|---|
| **Where you are** | 📍 | the sidebar path, the URL, which persona, how long |
| **What it is** | 📖 | the words you say to a non-technical room |
| **On the page** | 👁 | what is on the screen, so nothing surprises you |
| **Walk it** | ▶ | numbered steps: click this, expect that, say this |
| **Behind the page** | ⚙ | the endpoint, the rule and the permission, for the technical question |
| **Careful** | ⚠ | what goes wrong in front of people, and what to do instead |

Three more markers appear inside the walks:

- **🔴 LIVE WRITE** — this step changes real data. Every one is numbered **LW-1 … LW-26**, and chapter 30
  says how (or whether) each can be undone.
- **⚠ CAREFUL** — a way this step goes wrong, and what to do instead.
- **🚫 DO NOT PRESS** — a control that renders for your persona and answers 403 or 400, or one that
  writes something you cannot undo inside a demo.

**Say-lines are in quotation marks and indented.** Read them more or less as they stand. Change the
names, keep the order of the ideas — the order is doing the work.

---

## Chapter 0 — The story in one page

Say this first, before you open the application. It is the map the audience carries through the next
hour; if they have it, no screen will confuse them.

```
 ┌─ ACT I ──────────┐   ┌─ ACT II ─────────────────┐   ┌─ ACT III ───────────┐
 │ THE CATALOGUE    │   │ NEED → PLAN → BUDGET     │   │ THE EMPLOYEE ASKS   │
 │ what we can teach│──▶│ who needs what, in which │◀──│ a request from the  │
 │ and who teaches  │   │ quarter, with what money │   │ portal, decided by  │
 │ it               │   │                          │   │ HR                  │
 └──────────────────┘   └────────────┬─────────────┘   └─────────────────────┘
                                     ▼
 ┌─ ACT IV — DELIVERY ───────────────────────────────────────────────────────┐
 │ Schedule a run → nominate people → approve → (waitlist if full) →         │
 │ register attendance → record completion → verify → issue certificate      │
 └───────┬───────────────────────────────┬───────────────────────────────────┘
         ▼                               ▼
 ┌─ ACT V ─────────────┐        ┌─ ACT VI ────────────────────────────────┐
 │ MONEY & OBLIGATIONS │        │ DID IT WORK?                            │
 │ budget spend,       │        │ feedback after the course, follow-up    │
 │ service bonds       │        │ 30/60/90 days later, manager's view     │
 └─────────────────────┘        └─────────────────────────────────────────┘
         ▼
 ┌─ ACT VII ───────────────────┐  ┌─ ACT VIII ─────────────┐  ┌─ ACT IX ───────────┐
 │ COMPLIANCE & CREDENTIALS    │  │ GROWTH BEYOND COURSES  │  │ OVERSIGHT          │
 │ mandatory training, renewal;│  │ learning paths,        │  │ one person's file; │
 │ qualifications from outside │  │ mentoring              │  │ the analytics      │
 └─────────────────────────────┘  └────────────────────────┘  └────────────────────┘
```

> "Training in this system is a cycle, not a register. We decide what we are able to teach — that
> is the catalogue. We find who needs what and plan and budget for it. People ask for what is not
> on the list. We run a course: we invite people, someone approves, we take the register, record the
> result, check it, and issue the certificate. If we paid for it, the person owes us time, and that is
> a service bond. After the course we ask whether it worked. Some training is not optional — that is
> compliance. And not all learning is a course: there are learning paths and there is mentoring.
> Everything feeds one dashboard, so the Managing Director can see what we spent and what we got."

**Three places, three audiences.** Repeat this whenever someone asks "where do I find…":

| Place | Who works there | What lives there |
|---|---|---|
| **Administration → HR → Training & Learning** | the HR head, once, then rarely | the catalogue: categories, groups, vendors, trainers, programmes, compliance rules, learning paths, mentoring schemes |
| **Human Resources → Talent & Performance → Training** (and **Mentoring**) | the HR training desk, every day | needs, plans, budgets, requests, schedules, nominations, approvals, completions, certificates, compliance, enrolments, bonds, analytics |
| **The employee portal** — *My Training*, *My Learning Paths*, *My Mentoring* | every employee | their own requests, nominations, waitlist, bonds, feedback, learning steps, mentoring |

---

## The eleven rules

Read these before chapter 1. Each is a fact about the code or the demo database that changes what you
can safely show. **T-n** refers to the finding in chapter 29.

| Rule | |
|---|---|
| **1 · Rebuild the database, then check it.** | The harness suites leave fixtures behind. Run the clean-database check in § 1.3 the evening before. On a dirty database no number in this guide holds |
| **2 · Nominations do not reach the approval queue by themselves (T-1).** | A nomination HR creates is saved as *Submitted*. The *Awaiting supervisor* tab reads only *Supervisor review*; the *Awaiting HR* tab reads only *HR review*; **nothing in the product sets *Supervisor review***. So on a fresh database **both tabs are empty**, and there is no Approve button on the nomination's own page. **The prepare script in § 1.4 moves seven nominations to *HR review* so the HR tab has something to approve.** Without it, skip chapter 14's live steps and talk through them |
| **3 · "Seats taken" always reads 0 (T-2).** | The schedule's *Seats* column and *Seats taken* tile count only the status *Confirmed*, and nothing sets *Confirmed*. The real capacity check (at approval and on enrolling from the waitlist) counts *Approved* and *Confirmed*. Do not point at the seat counts; say "capacity is enforced when HR approves" |
| **4 · Four approvals are an Administer-tier act that nobody in the demo holds (T-4).** | **Approving a training plan or a budget, revoking a certificate, waiving a bond, and every delete** need `HR.Training.Admin`. The HR role does not hold it (`hr.head` answers **403**), and the platform `admin` login has no employee record (answers **400** "not linked to an employee"). The budgets were pre-approved by a seeder; the 2027 plan sits at *Pending approval*. See chapters 7 and 8 for what to say |
| **5 · Managers have no desk (T-5).** | Only the HR role (and administrators) hold `HR.Training.Read/Write`. `head.dev`, `gm.ops`, `md.tdc` and `auditor` cannot use the training desk (a couple of its menu entries show, but their data calls answer 403). Every desk step in this guide is done as **`hr.head`**. Managers and employees use the portal |
| **6 · "Mark completed" is not offered on an open run (T-3).** | The button shows only when a schedule is *In progress* or *Registration closed*, and nothing moves a schedule there. So you cannot close a run from its page. **SCHED-2026-004 was closed by the seed** — show that one |
| **7 · No screen takes a score (T-6).** | *Record completion* captures pass/fail only. The five scores on SCHED-2026-004 (88, 74, 91, 67, 42) were keyed through the API. Do not promise "enter the mark" |
| **8 · "Awaiting your feedback" is empty for everyone (T-7).** | The portal lists only *Confirmed* nominations, and nothing sets *Confirmed*. The page's second half, *What you have already said*, works — it shows Efua's PPA form |
| **9 · Two programme flags drive nothing you can show (T-8).** | *Requires approval* and *Min participants* are stored and displayed; I found no code that reads either. Do not say "this makes approval mandatory" |
| **10 · Every write is permanent.** | Approvals, completions, verifications, bond acceptances, exits and settlements cannot be reopened from a screen. Chapter 30 lists every write and its remedy. **Rebuild the database after you rehearse and after you present** |
| **11 · Every date is relative to the build.** | Schedules fall at +7, +14 and +28 days after the build and one three weeks before it. A demo a fortnight after the build finds the First Aid run already under way. Read the dates off the screen; this guide never gives you one |

---

## Chapter 1 — Before you demonstrate

### 1.1 The demonstration data

Built by `scripts/New-UatDatabase.ps1` (`seed-hr-demo`, scenarios 040 and 041) and the training
budget seeder. Everything below exists on a clean rebuild.

**The catalogue**

| Thing | What is there |
|---|---|
| Categories (3) | `TECH` Technical & Professional · `LEAD` Leadership & Management · `SHE` Safety, Health & Environment |
| Programme groups (2) | `GRP-STAT` Statutory & Mandatory · `GRP-PROF` Professional Development |
| Vendors (2) | **GIMPA Executive Education** · **Ghana Institution of Engineers** (both *Training Firm*) |
| Trainers (2) | **Dr. Efua Mensah-Bonsu** (GIMPA; leadership, public-sector management) · **Ing. Kofi Asamoah** (Ghana Institution of Engineers; structural engineering, site safety) — each with certified skills, and Asamoah with **one blocked window** ("On assignment at the Kpone waste-water works") |
| Programmes (4) | `LEAD-101` Supervisory Skills for New Managers (3 days, GHS 2,400, certificate valid 24 months) · `TECH-210` AutoCAD Civil 3D — Advanced (5 days, **GHS 3,800, service bond 24 months**) · `SHE-050` First Aid at Work (2 days, GHS 900, certificate) · `TECH-115` Public Procurement Act 663 — Practitioner Update (1 day, GHS 650, no certificate flag). Each has two materials and target skills; no competencies are linked |

**The runs**

| Schedule | Programme | Trainer | Where | Seats | State | People |
|---|---|---|---|---|---|---|
| `SCHED-2026-001` | LEAD-101 | Dr. Mensah-Bonsu | GIMPA Greenhill Campus, Accra | 16 | Registration open, +14 days | 8 supervisors/managers nominated (*Submitted*); **Efua Seidu on the waitlist** |
| `SCHED-2026-002` | TECH-210 | Ing. Asamoah | MIS Training Room, Tema Head Office | 12 | **Planned** (not yet approved), +28 days | up to 8 engineers/architects, plus Efua's self-nomination; **a GHS 3,800 / 24-month bond raised and awaiting her acceptance**; two engineers waitlisted |
| `SCHED-2026-003` | SHE-050 | Ing. Asamoah | SHE Training Room, Tema Head Office | 20 | Registration open, +7 days | about 9 first-aiders (Efua, Kojo Ansah, the SHE officer and six more) |
| `SCHED-2026-004` | TECH-115 | Dr. Mensah-Bonsu | Boardroom, Tema Head Office | 25 | **Completed**, three weeks ago | about 7 officers; attendance register (the last on the roll absent: "Called to the Tender Committee"); **5 completions (scores 88, 74, 91, 67, 42 — the last one fails)**; 3–4 evaluation forms; 2 thirty-day follow-ups; 4 certificates |

**Money**

| Thing | What is there |
|---|---|
| Budgets (2, **Approved**) | `TRN-2026-CORP` **GHS 250,000** (HR & Administration) · `TRN-2026-DEV` **GHS 80,000** (Development), both against GL `5410-000` |
| Spend (3 payments) | **GHS 19,200** GIMPA, supervisory skills, 8 participants (`INV/GIMPA/2026/0431`) · **GHS 8,100** GhIE, first aid, 9 participants (`INV/GHIE/2026/0118`) · **GHS 4,550** PPA update, 7 participants (`INV/GIMPA/2026/0388`). Total **GHS 31,850** of **GHS 330,000** — about **9.7 %** |
| 2027 plan (1) | the Development Department's training plan: **5 items** (Civil 3D Q1, Supervisory Q2, First Aid Q2, PPA Q3, **FIDIC contract administration Q4 — not in the catalogue**), 41 estimated participants, **GHS 83,600** estimated cost; **4 budget lines** totalling **GHS 117,100** (course fees 83,600; travel 18,000; materials 6,500; venue and refreshments 9,000). Submitted; **should read *Pending approval*** (Rule 4) |
| Needs assessments (3), year 2027 | Efua Seidu (performance review, *High*; Civil 3D; skill gap Project Management *Working knowledge → Advanced*) · Kojo Ansah (manager request, *Medium*; Supervisory Skills; Team Leadership *Basic → Proficient*) · the Head of Development (skills-gap analysis, *Critical*; PPA update; Risk Assessment *Working knowledge → Advanced*) |

**The people side**

| Thing | What is there |
|---|---|
| Training request | Efua's request for **AutoCAD Civil 3D — Advanced**, linked to the programme, **Submitted and waiting** |
| Learning paths (2) | **New Supervisor Pathway** (LEAD-101 then TECH-115; certificate "TDC Certificate in Supervisory Practice") — Efua enrolled, her PPA step done on the strength of her attendance · **Estate Design Technician Pathway** (TECH-210, then SHE-050 optional) — Kojo enrolled, his first-aid step recorded by HR as prior learning |
| Mentoring | **TDC Mentoring Scheme 2026** (coordinator: the Head of HR). Two pairs: the **Head of Development → Kojo Ansah** (two logged sessions) and **the General Manager, Operations → Efua Seidu** (one) |
| Compliance requirements (2) | `CMP-FA` Statutory first-aid certification (custom, every 1,095 days, 30 days' grace) · `CMP-PPA` Public procurement practitioner update (annual, 60 days' grace, Procurement unit). Assigned to the people on the matching runs; **one first-aider exempted** on medical advice; the PPA passers marked compliant |
| Employee certificates (4) | Efua's **PMP** (expires in about 80 days, verified) · Kojo's **First Aid at Work** (verified) · Kojo's **Probationer Membership, Ghana Institute of Architects** (**unverified** — your live write) · the Head of Development's **MGhIE** (verified) |

### 1.2 The cast

Sign in as the **persona who really does the act**. Every persona login except `admin` uses the
password `Demo123!`.

| Sign in as | Who | Chapters |
|---|---|---|
| `hr.head` | **Akpene Amoah**, TDC/00009, Head of HR — HR role: Training Read and Write, **not Admin** | every desk and setup chapter |
| `staff` | **Efua Seidu**, TDC/00017, Development Department | 9, 10 (to read the decisions), 15, 19, 20, 23, 27 |
| `new.hire` | **Kojo Ansah**, TDC/00063, Architecture — on probation | 24 (to show what a mentee cannot see) |
| `head.dev` | the **Head of Development**, Efua's line manager and Kojo's mentor | 24 |
| `gm.ops` | the General Manager, Operations — Efua's mentor | 24 (mention only) |
| `admin` | the platform administrator (`Admin123!`) — **no employee record** | only to show Rule 4's 400 |

Use **two browser profiles**, so `hr.head` and `staff` stay signed in side by side. Chapters 9–10, 15
and 19 hand the work back and forth; switching profiles is faster than signing out.

### 1.3 The evening before: is the database clean?

Run as a database administrator. It only reads.

```sql
SELECT (SELECT COUNT(*) FROM TrainingPrograms           WHERE IsDeleted = 0) AS programmes,
       (SELECT COUNT(*) FROM TrainingSchedules          WHERE IsDeleted = 0) AS schedules,
       (SELECT COUNT(*) FROM TrainingVendors            WHERE IsDeleted = 0) AS vendors,
       (SELECT COUNT(*) FROM TrainingBudgets            WHERE IsDeleted = 0) AS budgets,
       (SELECT COUNT(*) FROM TrainingBudgetTransactions WHERE IsDeleted = 0) AS payments,
       (SELECT COUNT(*) FROM TrainingPlans              WHERE IsDeleted = 0) AS plans,
       (SELECT COUNT(*) FROM TrainingNeedsAssessments   WHERE IsDeleted = 0) AS needs,
       (SELECT COUNT(*) FROM LearningPaths              WHERE IsDeleted = 0) AS paths,
       (SELECT COUNT(*) FROM MentoringPairs             WHERE IsDeleted = 0) AS pairs,
       (SELECT COUNT(*) FROM TrainingServiceBonds       WHERE IsDeleted = 0) AS bonds;
```

| | programmes | schedules | vendors | budgets | payments | plans | needs | paths | pairs | bonds |
|---|---|---|---|---|---|---|---|---|---|---|
| **A clean rebuild** | 4 | 4 | 2 | 2 | 3 | 1 | 3 | 2 | 2 | 1 |

Anything higher means a harness suite or a rehearsal has run since the rebuild (Rule 1). Rebuild
again rather than demonstrate on it. Confirm the API is serving the demo database:

```powershell
powershell -File .\scripts\Test-ErpApiDatabase.ps1      # expect: YES  The API on port 5000 is serving ErpSystemDB_UAT.
```

### 1.4 The prepare script (needed for chapters 14, 16 and 19)

Rule 2 explains why. This script signs in as `hr.head` and uses the same approval endpoint the
Approvals screen uses (`POST /api/training-nominations/{id}/approve`) to put nominations where a
screen can then act on them. Run it **once**, after the rebuild and **before** your rehearsal. It only
moves nominations that are still *Submitted*, so running it twice does nothing the second time.

| Run | Result |
|---|---|
| `SCHED-2026-004` (delivered) — **all** nominations, both stages | *Approved*, so the **Attendance** tab shows the real roll and its recorded register |
| `SCHED-2026-003` (First Aid) — the first four, supervisor stage only | *HR review*, so **Awaiting HR** has rows |
| `SCHED-2026-002` (Civil 3D) — Efua Seidu and the first two others, supervisor stage only | *HR review*; **approving one of the others in chapter 14 mints a service bond** |

```powershell
$api   = 'http://localhost:5000/api'
$login = Invoke-RestMethod -Method Post -Uri "$api/auth/login" -ContentType 'application/json' `
         -Body (@{ username = 'hr.head'; password = 'Demo123!' } | ConvertTo-Json)
$token = if ($login.token) { $login.token } elseif ($login.accessToken) { $login.accessToken } else { $login.data.token }
$h     = @{ Authorization = "Bearer $token" }

function Get-List($path) {
  $r = Invoke-RestMethod -Uri "$api$path" -Headers $h
  if ($r.items) { $r.items } elseif ($r.data) { $r.data } else { $r }
}
function Step($n, $role) {
  $b = @{ nominationId = $n.id; approverRole = $role; comments = 'Demonstration preparation' } | ConvertTo-Json
  Invoke-RestMethod -Method Post -Uri "$api/training-nominations/$($n.id)/approve" -Headers $h `
    -ContentType 'application/json' -Body $b | Out-Null
}

$sched = @{}
Get-List '/training-schedules' | ForEach-Object { $sched[$_.scheduleNumber] = $_.id }
$noms  = { param($num) @(Get-List "/training-nominations/schedule/$($sched[$num])") }

# 004 — delivered: Supervisor, then HR
foreach ($n in (& $noms 'SCHED-2026-004')) { if ($n.status -eq 'Submitted') { Step $n 'Supervisor'; Step $n 'HR' } }

# 003 — first four, Supervisor stage only
(& $noms 'SCHED-2026-003') | Where-Object status -eq 'Submitted' | Select-Object -First 4 | ForEach-Object { Step $_ 'Supervisor' }

# 002 — Efua plus two engineers, Supervisor stage only
$c3 = @(& $noms 'SCHED-2026-002') | Where-Object status -eq 'Submitted'
$efua   = $c3 | Where-Object employeeName -like '*Efua*' | Select-Object -First 1
$others = $c3 | Where-Object { $_.id -ne $efua.id } | Select-Object -First 2
foreach ($n in @($efua) + @($others)) { if ($n) { Step $n 'Supervisor' } }
'Prepared.'
```

If it errors on a line, skip that line and note which run it served: the 004 lines serve chapter 16 (the
register), the 003 lines serve chapter 14 (the HR queue), the 002 lines serve chapters 14 and 19 (the bond).
Without them, talk through those steps instead of performing them. After it runs, **Awaiting HR** should list seven rows (four First Aid, three
Civil 3D).

### 1.5 The checklist

- [ ] § 1.3 reads clean, and the API is on the demo database.
- [ ] The prepare script has run (§ 1.4).
- [ ] Both `hr.head` and `staff` are signed in, each in its own browser profile. `hr.head` can open
      **Administration → HR → Training & Learning**; if that is refused, the HR role lacks the
      administration grant on this build — sign in as `admin` for Act I only.
- [ ] You have opened **Human Resources → Talent & Performance → Training → Schedules** and noted the
      dates of `SCHED-2026-003` (you will reuse them in chapter 11).
- [ ] You have read the eleven rules, and know that **Rule 4's four approvals are narrated, not
      pressed**.
- [ ] You know you will not press **Mark completed** (Rule 6), **Waive** or **Revoke** (Rule 4).
- [ ] You will rebuild the database after the demo.

### 1.6 The agenda

| Act | Chapters | Persona | Time |
|---|---|---|---|
| The story, the desk's map | 0, 2 | `hr.head` | 7 min |
| **I · The catalogue** | 3–5 | `hr.head` | 11 min |
| **II · Need, plan, budget** | 6–8 | `hr.head` | 12 min |
| **III · The employee asks** | 9–10 | `staff`, `hr.head` | 7 min |
| **IV · Delivery** | 11–18 | `hr.head`, `staff` | 32 min |
| **V · Money and obligations** | 19 | `hr.head`, `staff` | 7 min |
| **VI · Did it work?** | 20 | `hr.head`, `staff` | 4 min |
| **VII · Compliance and credentials** | 21–22 | `hr.head` | 8 min |
| **VIII · Growth beyond courses** | 23–24 | `hr.head`, `staff`, `head.dev` | 10 min |
| **IX · Oversight** | 25–27 | `hr.head`, `staff` | 8 min |
| Close and questions | 28–31 | — | 5 min + questions |

About **100 minutes** performed in full. **The 35-minute short path:** chapters 0, 2, 5, 7 (look, do not
press), 9, 10, 11 (look, do not create), 13, 14, 17, 18, 19 (steps 1–4), 22, 26 — then the closing.

---

## Chapter 2 — Orient the room: the sidebar and the Training home page

📍 **Human Resources → Talent & Performance → Training** · `/hr/training` · `hr.head` · 3 minutes

📖 This is the desk's map. Do it before any setup: the audience sees every area they are about to
visit, as one page, and the three groups on it are the three questions a training officer asks every
morning.

> "Before I show you how it is built, here is how it is used. This is the training desk's front page.
> Three questions. *What is happening this month* — schedules, who is enrolled, requests from staff.
> *What is waiting for me to decide* — nominations, requests, completions to verify, who is behind
> on mandatory training. And *what do we hold on record* — completions, certificates, bonds,
> analytics. Everything I show you today hangs off one of these cards."

👁 **On the page.** The title is **Training & Learning**. Three groups of cards:

| Group | Cards |
|---|---|
| **Day to day** | Schedules · Enrollments · Training Activities · Mentoring · Training Requests |
| **Approvals** | Nomination Approvals · Request Approvals · Completion Verification · Compliance |
| **Records** | Completions · Certificates · Employee Certificates · Service Bonds · Feedback & Follow-up · Analytics · Setup & Catalog |

The **sidebar** group (Human Resources → Talent & Performance → **Training**) lists thirteen entries:
*Needs Assessments · Training Plans · Training Budgets · Enrollments · Training Activities ·
Analytics · Schedules · Requests · Nomination Approvals · Completions · Certificates · Employee
Certificates · Compliance*. **Mentoring** is its own sidebar group beside it (*Mentoring Pairs*,
*Programmes*).

▶ **Walk it**
1. Sign in as `hr.head`. Open the sidebar group and read the thirteen names aloud, in order. The
   order is the cycle: the first three are *planning*, the middle are *delivery*, the last are
   *outcomes*.
2. Open the home page. Hover each of the three groups; do not click yet.
3. Point at **Service Bonds**: "It is filed under Pay & Benefits in the sidebar, because the obligation is a
   commitment, not a course — and it is linked from here."

⚠ **Careful.**
- The home page does not list **Needs Assessments, Training Plans or Training Budgets** — they were
  moved to the sidebar, and the page's subtitle ("The catalog and planning live under
  Administration") and the *Setup & Catalog* card ("…plans and budgets") still describe the old
  layout. Say "the planning screens are in the sidebar" and move on (T-9).
- *Request Approvals* opens **Requests** already filtered to *Awaiting approval*; *Completion
  Verification* and *Completions* open the same screen.

⚙ Pages gate on `HR.Training.Read`; *Schedules* and *Nomination Approvals* carry no menu permission
(the parent stays open so self-service children stay reachable), but their data calls are gated.

---

# ACT I — The catalogue

> "Nothing can be scheduled, budgeted or requested until the Corporation has said what it can teach.
> That is the catalogue. It is set up once and rarely changes, which is why it lives under
> Administration, not on the desk."

## Chapter 3 — The setup hub, categories and programme groups

📍 **Administration → HR → Training & Learning** · `/administration/hr/training` · `hr.head` · 3 minutes

📖 The hub is a page of cards, one per catalogue area. Categories and groups are the two ways a
programme is filed: **category** is its *subject* (Technical, Leadership, Safety); **group** is its
*curriculum* (Statutory & Mandatory, Professional Development). Both are optional on a programme.

👁 **On the hub.** Title **Training & Learning**; subtitle *"The training catalogue — what can be
delivered, by whom, and what it must renew."* Eight cards in this order: **Categories · Program
Groups · Vendors · Trainers · Programs · Compliance Requirements · Learning Paths · Mentoring
Schemes**. A note says Needs Assessments, Plans and Budgets have moved to the training desk.

▶ **Walk it**
1. Open the hub. Say the eight names as the eight things the Corporation has to decide.
2. **Categories** (`/administration/hr/training/categories`, "Training Categories"): three rows —
   `TECH`, `LEAD`, `SHE`. Columns: **Code · Name** (with a colour dot) **· Programs · Status**.
   Search box filters name and code. Open `SHE` (row click → **Edit Training Category**).
3. On the edit page: **Code** is greyed out with "Cannot be changed after creation"; **Name**, **Color**,
   **Description**, **Sort order**, **Active**. Press **Cancel** — you are only looking.
4. **Program Groups** (`/administration/hr/training/program-groups`): two rows — `GRP-STAT` Statutory
   & Mandatory and `GRP-PROF` Professional Development; same form shape. Open the **Programs** count
   to say "two programmes sit in each".

> "A category is what the course is about. A group is the curriculum it belongs to. A first-aid
> course is a *Safety* course and a *Statutory* course. The system lets you file it both ways and
> never forces you to."

⚠ **Careful.** Do not press **New Category** unless you mean it; the `Create` writes a row you cannot
delete as `hr.head` (Rule 4 — every delete is Administer-tier). A category or group **cannot be
deleted while any programme uses it**.

⚙ `GET/POST/PUT /api/training-category-options`, `/api/training-program-groups`; Read for GET, Write
for POST/PUT, **Admin for DELETE**. Codes are unique per tenant and immutable once created.

---

## Chapter 4 — Vendors and trainers

📍 `/administration/hr/training/vendors` · `/administration/hr/training/trainers` · `hr.head` · 4 minutes

📖 A **vendor** is an outside organisation we buy training from. A **trainer** is a person who stands
in front of the room — internal (linked to an employee), external (linked to a vendor), or both
absent for a purely external individual. A schedule names a trainer, a vendor, or both.

▶ **Walk it — vendors**
1. Open **Vendors** ("Training Vendors", *"External training firms, consultants and institutions."*).
   Two rows: **GIMPA Executive Education** and **Ghana Institution of Engineers**. Columns **Code ·
   Name** (with *Preferred* / *Blacklisted* badges) **· Type · Trainers · Status**.
2. Open **GIMPA Executive Education** (**Edit Training Vendor**). Walk the four cards:
   - **Vendor** — *Vendor code* (locked), *Name*, *Vendor type* (Individual Consultant, Training Firm,
     Accredited Institution, University / Tertiary, Government Agency, NGO / Non-Profit, Other),
     *Currency* (GHS), *Active*, *Preferred vendor* (reveals *Preferred since*).
   - **Accreditation** — body, number, *Accreditation status* (Active, Pending, Expired, Suspended,
     Revoked; "Not accredited" if blank) and expiry.
   - **Contact** — name, email, phone, website, address.
   - **Contract & Rate** — *Default daily rate*, contract reference, start and end, notes.

> "We keep the contract and the accreditation on the vendor, so that when the finance officer asks
> who we paid and under what agreement, the answer is on the same page as the course."

▶ **Walk it — trainers**
3. Open **Trainers** ("Internal and external trainers who can be scheduled to deliver training.").
   Columns **Name · Source · Expertise · Rating · Sessions · Status**. Two rows. **Dr. Mensah-Bonsu**
   should show a rating and one session — both come from the delivered PPA run (SCHED-2026-004: its
   evaluation forms feed the trainer's average and closing the run credits the session). **Ing.
   Asamoah shows "—" and 0**, because none of his runs has closed (Rule 6). Say so before someone
   asks.
4. Open **Ing. Kofi Asamoah**. Three tabs:
   - **Overview** — name, *Linked employee (internal trainer)*, *Vendor (external trainer)* (here:
     the Ghana Institution of Engineers), bio, direct contact, expertise areas, *Active*.
   - **Skills** — *Safety Compliance* (Expert, certified to train, `GhIE/MEM/2011/0873`), *First Aid*
     (Advanced, certified to train, St John Ambulance Instructor), *Risk Assessment* (Advanced, not
     certified). Columns **Skill · Proficiency · Certified to train · Certificate**.
   - **Availability** — two windows: a long **Available** one, and **one blocked window** ("On
     assignment at the Kpone waste-water works — not available for training"). **Read the blocked
     window's dates aloud and write them down** — chapter 11 uses them.

> "A trainer is not just a name. We record what they are certified to teach and when they are
> free. When you schedule a course the system checks the trainer's diary for you — I will show you
> that when we schedule one."

⚠ **Careful.** A skill's link cannot be changed once added ("remove and re-add to link a different
one"). A vendor with trainers cannot be deleted.

⚙ `/api/training-vendors`, `/api/trainers` (+ `/skills`, `/availability`). Skills are reference data
owned by the employee-profile area; the programme's and trainer's skill pickers read the same list.

---

## Chapter 5 — Programmes: the centre of the catalogue

📍 `/administration/hr/training/programs` · `hr.head` · 4 minutes

📖 A **programme** is the thing that can be taught: what it is, how long, what it costs, whether it
earns a certificate, whether it binds the person who attends. Every other training record points at
one.

👁 **List** ("Training Programs", *"The catalog of training programs that can be scheduled and
delivered."*): **Code · Name** (colour dot, a *Certificate* badge) **· Category · Type · Duration**
(e.g. "3d / 24h") **· Cost** (e.g. "GHS 2400.00") **· Status**.

▶ **Walk it**
1. Open the list. Four rows: `LEAD-101`, `TECH-210`, `TECH-115`, `SHE-050`. Say the cost column
   aloud: "this is what one seat costs".
2. Open **`TECH-210` AutoCAD Civil 3D — Advanced**. The **Overview** tab is the full form, seven cards:
   - **Program** — *Program code* (locked), *Program name*, *Description*, *Category*, *Program group*.
   - **Type & Level** — *Type* (Workshop · Seminar · Conference · On-the-Job Training ·
     Mentoring/Coaching · Certification Program), *Source* (Internal · External · Online /
     E-Learning), *Level* (Beginner · Intermediate · Advanced · Expert · Any Level).
   - **Duration & Content** — *Duration (days)*, *Duration (hours)*, *Prerequisites*, *Learning
     objectives*.
   - **Cost & Inclusions** — *Cost per participant*, *Currency*, and three switches: *Includes
     accommodation / meals / transport*.
   - **Certification** — *Provides certificate*; when on: *Certificate name*, *Certificate validity
     (months)*.
   - **Capacity & Approval** — *Min participants*, *Max participants*, *Active*, *Requires approval*
     (Rule 9: do not say it enforces anything).
   - **Service Bond** — *Requires service bond*; when on: *Service bond (months)* (here **24**) and
     *Service bond terms* (the text the employee accepts).
3. **Stop at the Service Bond card** and say the line below. This one switch is what connects the
   catalogue to money and to chapter 19.

> "Civil 3D costs three thousand eight hundred cedis a seat. We do not send someone on that and let
> them leave next month. So this programme is marked *requires a service bond*, for twenty-four
> months. The moment HR approves a nomination onto it, the system creates the bond — with the cost,
> the duration and these exact terms — and waits for the employee to accept. I will show you that
> in chapter 19."

4. **Materials** tab: two rows (a data pack, a link to the Autodesk certification outline). Columns
   **Name · Type · Public · Status**. *Public* means "visible to nominees before the session".
   *Material types:* Handbook · Presentation Slides · Video · Document · Exercise/Activity ·
   Assessment · Reference Material.
5. **Competencies** tab: **empty** — say "this is where a programme is tied to the competency
   framework; we have not linked any for the demonstration". **Skills** tab: *Project Management*,
   target **Advanced**. Say the line below.

> "When an employee passes this course and their manager verifies it, the system adds these skills
> to their profile at this level. The training record writes into the skills record. That is how a
> course changes what we know about a person."

6. Open **`SHE-050`** to show *Provides certificate* on (certificate name "First Aid at Work",
   validity 24 months) and **`LEAD-101`** for *Program group* `GRP-PROF`.

⚠ **Careful.** *Program code* cannot be changed. A programme with schedules cannot be deleted (and
every delete is Administer-tier — Rule 4). Materials show a **path or URL only**: there is no upload
button anywhere in Training, so do not say "upload the handbook".

⚙ `/api/training-programs` (+ `/materials`, `/competencies`, `/skills`). Verifying a passed completion
upserts the programme's skills onto the employee (`TrainingCompletionService.WriteBackSkillsAsync`).
A nomination reaching *Approved* mints a bond when `RequiresServiceBond` is true
(`TrainingServiceBondService.EnsureBondForNominationAsync`): duration = the programme's months (default
12), amount = the nomination's actual cost, else the programme's cost per participant.

# ACT II — Find the need, plan the year, hold the money

> "We have said what we *can* teach. Now: what *should* we teach, to whom, when, and with what
> money? Three records answer that, and they are deliberately separate. A **needs assessment** is
> about one person. A **plan** is about a unit and a year. A **budget** is the money voted for it."

## Chapter 6 — Training needs assessments

📍 **Training → Needs Assessments** · `/hr/training/needs-assessments` · `hr.head` · 4 minutes

📖 A needs assessment is a recorded gap: *this person, in this year, lacks this, for this reason,
and here is what we recommend.* It is the evidence the plan is built from.

👁 **List** ("Training Needs Assessments", *"Gaps identified for individual employees, with
recommended programs and skill targets."*): employee and number, year, source, priority, whether
training has been provided, how many programmes are recommended and how many skill gaps are
recorded. Search by employee name or number. Buttons **Bulk Create** and **New Assessment**; a row
menu with **View** and **Delete**.

▶ **Walk it**
1. Open the list: three rows for 2027 — Efua Seidu, Kojo Ansah, the Head of Development. Read one
   aloud: "Efua — performance review — *High*: no Civil 3D capability in the section; estate
   layouts are being outsourced."
2. Open **Efua's** row. The header reads *"Efua Seidu — 2027"* with her number and a status badge
   (*Pending* until training is provided, then *Fulfilled*). Three tabs:
   - **Overview** — *Source* (Performance Review · Self Assessment · Manager Request · Skills Gap
     Analysis · Job Role Change), *Priority* (Critical · High · Medium · Low), *Identified gaps*,
     *Additional notes*, *Year*, *Identified date*, *Identified by*, and — on edit — **Training
     provided** (with a date).
   - **Recommended Programs** — *AutoCAD Civil 3D — Advanced*, priority High, with a rationale.
     Columns **Program · Priority · Rationale**.
   - **Skill Gaps** — *Project Management*, current **Working knowledge**, required **Advanced**,
     gap priority **High**. Columns **Skill · Current · Required · Gap priority**.
3. **🔴 LIVE WRITE LW-1.** Back on the list, press **New Assessment**. The form: *Employee* (picker,
   required), *Identified by* (defaults to you), *Year* (defaults to this year), *Identified date*
   (defaults to today), *Source*, *Priority*, *Identified gaps* (required), *Additional notes*. Use an
   employee **who has no assessment** (not Efua, Kojo or the Head of Development); source *Manager
   Request*, priority *Medium*, gap "Needs formal grounding in contract administration before taking
   the Community 25 consultancy." Press **Create Assessment**.
4. **🔴 LIVE WRITE LW-2.** On the new assessment, open **Recommended Programs → add**: *Program*
   (Active programmes only, shown as `CODE — Name`), *Priority*, *Rationale*. Choose `TECH-115`.
   Rows here can be **added and removed but not edited** — "remove and add again to change it".
5. **Bulk Create** (`/hr/training/needs-assessments/bulk`, *"Record the same identified gap across
   several employees at once — for example, after a skills-gap analysis."*): **look, do not submit**.
   Add two names with the picker (they appear as removable chips) to show how it works, then leave.
   Submitting writes one assessment per person and reports *"Created X of Y requested assessments"*.

> "A needs assessment can come from three places: HR records it, a manager asks for it, or the
> appraisal throws it up. When an appraiser writes 'this person needs training' on an appraisal, the
> performance module raises a training request for that employee automatically — the two modules
> talk. And one gap across a whole unit is one screen, not forty."

⚠ **Careful.** The assessment's **Training provided** switch is a flag HR sets by hand; completing a
course does **not** set it. Say "it records whether the need was met", not "it updates itself".

⚙ `/api/training-needs-assessments` (+ `/programs`, `/skill-gaps`, `/bulk`). Read, Write; Delete is
Admin. `TrainingNeedsAssessmentService` stamps `IdentifiedBy` from the caller if none is sent.
`TrainingRequestHandler` (performance module) writes the cross-module request.

---

## Chapter 7 — Training plans

📍 **Training → Training Plans** · `/hr/training/plans` · `hr.head` · 4 minutes

📖 A plan is a unit's intention for a year: the courses it expects to run, in which quarter, for how
many people, at what estimated cost — and the budget lines that estimate adds up to.

👁 **List** ("Training Plans", *"Annual or quarterly training plans by organization scope, with items
and budget lines."*): plan number, year, organisation unit, status, items done against items, approval
date; **New Plan**; row menu **View**, **Submit for approval** (Draft only), **Delete** (Draft only).
**Status ladder:** Draft → Pending Approval → Approved → In Progress → Completed (and *Revised*).

▶ **Walk it**
1. Open the list: one plan, for the **Development Department, 2027**.
2. Open it. Header: the plan number, *"2027 — Development Department"*, a status badge. **Read the
   badge to the room.** It should say **Pending Approval** (Rule 4).
3. **Overview** tab: read-only once submitted — org level and unit, approved by (blank), notes —
   with the hint *"An approved or completed plan cannot be edited here."* (Only a **Draft** shows the
   editable form.)
4. **Items (5)** tab. Columns **Title · Program · Quarter · Est. participants · Est. cost ·
   Completed**. Walk the five rows: Civil 3D (Q1, 8 people, GHS 30,400) · Supervisory Skills (Q2, 6,
   14,400) · First Aid (Q2, 10, 9,000) · PPA update (Q3, 12, 7,800) · **Contract administration under
   FIDIC (Q4, 5, 22,000 — "Not from the catalog")**. Open one to show the dialog: *Training title*,
   *Catalog program*, *Description*, *Quarter*, *Planned start/end*, *Estimated participants*,
   *Estimated cost*, and *Completed* (which reveals *Completion date*, *Actual participants*, *Actual
   cost*).
5. **Budget Lines (4)** tab: **Category · Budgeted · Actual · Committed · Variance** — course fees
   83,600; travel and accommodation 18,000; materials 6,500; venue and refreshments 9,000 =
   **GHS 117,100**.

> "The fifth item is the one I want you to notice. It is not in the catalogue — it came from a
> request a staff member raised, which I will reject in a moment. Rejecting it was not the end: it
> is in next year's plan, in the fourth quarter, with a price. The plan is where unmet demand goes to
> wait for a budget."

6. **About approval — say, do not press.**

> "A plan moves from draft to submitted, and then someone with the authority to commit the
> Corporation approves it. That is a higher tier than the HR desk — in practice the Managing
> Director's office. In this demonstration environment nobody with an employee record holds that
> tier yet, so the plan stays at *Pending approval*. Which role should hold it is a configuration
> decision for TDC."

🚫 **DO NOT PRESS Approve.** As `hr.head` it answers **403**; as `admin` it answers **400** "Your user
account is not linked to an employee record" (T-4).

⚠ **Careful.** Do not describe the five items as "committed money". Plan estimates are not reserved
against any budget; the three records are not linked by a rule (a plan item may name the schedule that
fulfilled it, and a budget may be attached to a schedule — chapter 11).

⚙ `/api/training-plans` (+ `/items`, `/budget-lines`, `/submit`, `/approve`). Create/Update/Submit are
Write; **Approve and Delete are Admin**. Only **Draft** is editable or deletable; *Submit* accepts
Draft or Revised. The plan number comes from the tenant number sequence.

---

## Chapter 8 — Training budgets

📍 **Training → Training Budgets** · `/hr/training/budgets` · `hr.head` · 4 minutes

📖 A budget is the money voted for training in a period and unit, with every payment recorded against
it. It is created as a draft and **cannot take a payment until it is approved**.

👁 **List** ("Training Budgets", *"Allocated training spend by year/quarter and organization scope,
with spend transactions."*): code, year and period, unit, currency, allocated, spent, remaining,
status; **New Budget**; row menu **View**, and — for a Draft — **Approve** and **Delete**.
**Status:** Draft → Approved → Active … Closed (also *Denied*).

▶ **Walk it**
1. Open the list: **`TRN-2026-CORP`** (GHS 250,000, HR & Administration) and **`TRN-2026-DEV`**
   (GHS 80,000, Development), both **Approved**.
2. Open **`TRN-2026-CORP`**. The header card reads **Allocated 250,000 · Spent 27,300 · Remaining
   222,700 · Committed 0 · Utilization ≈ 10.9 %**. Below it, the **Finance actuals** card.
3. **Finance actuals.** Read the card's own explanation: HR's *Spent* is the sum of the payments HR
   recorded here; the card shows what **Finance** says was spent on the same GL account
   (`5410-000`). **The two are allowed to disagree, and the disagreement is the point.** On the demo
   database Finance holds no matching postings, so expect an empty or zero card — say so.
4. **Transactions** tab. Columns **Date · Description · Amount · Recorded by · Reference**. Two rows:
   GIMPA — Supervisory Skills for New Managers, 8 participants, **GHS 19,200** (`INV/GIMPA/2026/0431`,
   voucher `PV-2026-0912`) and Ghana Institution of Engineers — First Aid at Work, 9 participants,
   **GHS 8,100** (`INV/GHIE/2026/0118`, `PV-2026-0918`).
5. **🔴 LIVE WRITE LW-3** (open **`TRN-2026-DEV`** first). **Transactions → add**: *Description*
   ("Demonstration — PPA e-procurement portal licence"), *Amount* (**650**; positive is spend,
   negative is a refund or credit), *Transaction date* (today), *Recorded by* (you), *Reference*
   (`DEMO-001`), optionally *GL account code* and *Voucher number*. Save. *Spent* moves from 4,550 to
   5,200 and *Utilization* to 6.5 %.

> "Three things on a payment: what it was for, the invoice reference and the voucher number. When the
> auditor asks, every cedi has a document behind it. And a payment can never be *edited* — if I
> made a mistake, I record a credit. The trail never has a hole in it."

6. Open the **2027 Development plan budget lines** again only if asked: "the plan is what we intend; the
   budget is what we hold; they are separate records."

🚫 **DO NOT PRESS Approve** on any budget — there is no Draft in the demo, so the button should not
show; if you create a budget to look at the form (**New Budget**: *Budget code*, *Year*, *Quarter*,
*Organization level/unit*, *Allocated amount*, *Currency*, *GL account code*, *Cost center code*,
*Notes*), it will sit in Draft forever as `hr.head` (Rule 4). **Do not create one.** The seeder
approved these two *because* the screen cannot.

⚠ **Careful.** The undo for LW-3 is a **credit of −650** with the same reference, not a delete.
*Spent* will net back to 4,550, and both rows stay on the trail.

⚙ `/api/training-budgets` (+ `/transactions`, `/finance-actuals`, `/approve`). `RemainingAmount =
Allocated − Spent − Committed`; `RecordTransaction` refuses unless the budget is *Approved* or
*Active*; `BudgetCode` is unique per tenant; a negative amount is a credit. `/over-budget` returns
budgets whose remaining is below zero. Approve and Delete are Admin.

---

# ACT III — The employee asks

> "Planning from the top finds most needs. It never finds all of them. The person who spots that the
> section has no one trained on the Corporation's new design standard is the person doing the work. So
> the employee can ask — from their own portal — and HR decides."

## Chapter 9 — The employee raises a request (the portal)

📍 **Portal → My work life → My Training** · `/me/training` → `/me/training/requests/new` · **`staff`**
(Efua Seidu) · 4 minutes

📖 My Training is the employee's whole training life on one page. The request is the only thing she
*asks*; everything else on it she is *told* or *shows*.

👁 **My Training** (`/me/training`): header buttons **My Waitlist · Service Bonds · Course Feedback ·
Request training**; an amber **service-bond banner** when a bond awaits acceptance; five tiles
(**Upcoming · Awaiting approval · Mandatory outstanding · Completed · Passed**) and a second row (training
hours, active certificates, learning paths, mentoring); five tabs — **Nominations · My Requests ·
Completions · Certificates · Mandatory**.

▶ **Walk it** (switch to the `staff` profile)
1. Open **My Training**. **Do not read the numbers**; they depend on the day and on the prepare script.
   Point at the **banner** — "1 service bond is waiting for your acceptance" — and say "we come
   back to that in chapter 19".
2. Walk the five tabs in a sentence each. **Nominations**: the runs she has been put on and where each
   approval stands. **My Requests**: the Civil 3D request, *Submitted*. **Completions**: results.
   **Certificates**: two sections, *issued to you* (with a verification code) and *your external
   qualifications* (her PMP). **Mandatory**: the compliance training she owes.
3. **🔴 LIVE WRITE LW-4.** Press **Request training**. The form is *What do you want to learn?*:
   *Training wanted* (required, 200 characters), *Description*, *Justification*, *Existing programme
   (optional)* ("Not in the catalog"). Enter:
   - *Training wanted:* `Contract Administration under FIDIC (Red Book)`
   - *Description:* `Two days on administering works contracts under the FIDIC conditions.`
   - *Justification:* `I administer the Community 25 consultancy contract and have had no formal
     training in the conditions of contract.`
   - *Existing programme:* leave as **Not in the catalog**.

   Press **Create request**. The toast says it was saved as a **draft**, and you land on its page.
4. The page shows **Submit** and **Delete** (only because it is a draft) and a *Your request* card.
5. **🔴 LIVE WRITE LW-5.** Press **Submit** → *"Submit for approval?"* — *"It moves out of draft and
   goes to the training desk for a decision."* → **Submit**. The badge becomes **Submitted** and the
   buttons disappear.

> "It starts as a draft, so she can think about it. She submits it, and from that moment it is out of
> her hands: she cannot edit it, and she cannot delete it. It is with the training desk."

⚠ **Careful.** She raises it **for herself only** — the form has no employee picker; the employee id
comes from her sign-in, never from the page.

⚙ `POST /api/training-requests` is self-or-Write (the **self arm** is the sign-in);
`/{id}/submit` likewise. A request is `Draft → Submitted → Approved | Rejected` (or *Cancelled*). Only a
draft can be edited or deleted.

---

## Chapter 10 — HR decides the request

📍 **Training → Requests** · `/hr/training/requests` · `hr.head` · 3 minutes

📖 This is the desk's register of requests, with the on-behalf option. Approving can **link** a free-text
ask to a catalogue programme — which is what turns a wish into something schedulable.

👁 **Requests** (*"Ad-hoc asks for training. Approving one can link it to a catalog programme so it
becomes schedulable."*): tabs **All requests** and **Awaiting approval**; a search box; columns
**Request · Employee · Training wanted · Linked programme · Raised · Status**; button **New Request**
(HR raising on someone's behalf); row menu **View details**, and by status **Submit / Approve /
Reject / Delete**.

▶ **Walk it** (back to `hr.head`)
1. Open **Awaiting approval**. Two rows from Efua: **AutoCAD Civil 3D — Advanced** (linked) and the
   **FIDIC** request you just raised ("Not in the catalog").
2. **🔴 LIVE WRITE LW-6.** On the Civil 3D row: menu → **Approve**. The dialog *Approve request*:
   *"Optionally link it to a catalog programme — that is what turns a free-text ask into something
   you can schedule."* **Choose `TECH-210 — AutoCAD Civil 3D — Advanced` explicitly** (the picker opens
   on *Do not link*) → **Approve**. Toast **Approved**.
3. **🔴 LIVE WRITE LW-7.** On the FIDIC row: menu → **Reject**. The dialog *Reject request*: *"The
   reason is shown to whoever raised it."* *Reason* (required): `Not in the 2026 training vote; carried
   into the 2027 plan as the Q4 contract-administration item.` → **Reject**.
4. Switch to `staff` and open **My Training → My Requests**. Open each. The approved one shows a *The
   decision* card — *Decided by*, *Decided on* and *"Your request was linked to AutoCAD Civil 3D —
   Advanced — look out for a nomination when a run is scheduled."* The rejected one shows the reason in
   red.

> "She gets an answer either way, and a reason when the answer is no. Nobody has to chase HR to find out
> why. And notice what approval did *not* do: it did not enrol her. A request is a wish. A nomination
> is a seat. They are different records, and the next act is about the seat."

⚠ **Careful.** Approving **does not create a nomination**, a schedule or a budget line. It sets the
status and the link. The line manager is **not** in this path: the decision belongs to the desk
(`HR.Training.Write`). If asked "does her supervisor approve?", the honest answer is "not at the request
stage — the supervisor's release is at nomination" (and see T-1).

⚙ `/api/training-requests/{id}/approve` and `/reject` — **`HR.Training.Write`**, no workflow engine.
`Approve` accepts an optional `linkedProgramId` and refuses a programme that does not exist in the
tenant; `Reject` requires a reason (≤ 1,000 characters).

# ACT IV — Delivery

> "Now the middle of the cycle, where most of the work is. A course is *scheduled*. People are
> *nominated* onto it. Someone *approves*. If it is full, they *wait*. When it runs we *take the
> register*, *record the result*, *verify it* and *issue the certificate*. Every one of those steps
> leaves a trace, and every trace is attached to the same schedule."

## Chapter 11 — Schedules: planning a run

📍 **Training → Schedules** · `/hr/training/schedules` · `hr.head` · 5 minutes

📖 A programme is the idea; a **schedule** is one run of it — dates, venue, who delivers it, how many
seats, and when registration opens and closes. Schedule numbers read `SCHED-2026-001` and so on.

👁 **List** ("Training Schedules", *"Planned runs of a catalog programme — dates, who delivers it, and
how many seats are taken."*): a status filter (*All statuses*, *Planned, Registration Open, Registration
Closed, In Progress, Completed, Cancelled, Postponed*), a search box (number, programme, trainer,
vendor, venue), columns **Schedule · Programme · Dates · Delivered by · Venue · Seats · Status**, a
**New Schedule** button, and a row menu with **View details**.

▶ **Walk it**
1. Open the list. Four rows; read them as a calendar: *Completed* three weeks ago (PPA update), then
   First Aid next week, Supervisory Skills at GIMPA the week after, and Civil 3D at the end of the
   month — **Planned**, not yet open. **Do not read the Seats column** (Rule 3: it will say 0 / n).
2. Filter to **Registration Open** and back — "the board the nominating officers work from".
3. **🔴 LIVE WRITE LW-8.** Press **New Schedule**. The form is one card, *Schedule details*:
   - *Programme* → **First Aid at Work**.
   - *Start date* / *End date* → **the same two dates as `SCHED-2026-003`** (you noted them in § 1.5).
     *Start time* / *End time* optional.
   - *Trainer* → **Ing. Kofi Asamoah**. *Vendor* stays empty. The hint reads *"Set at least one. Both is
     fine — a vendor supplying a named trainer."*
   - Press **Check trainer availability**. Expect a red box, **Trainer has a clash**, listing **Already
     scheduled: SCHED-2026-003 — First Aid at Work (dates)**, and the line *"This is a warning, not a
     block — you can still save if this training takes priority."*
   - *Optional second check.* Change the dates to the **blocked window you wrote down in chapter 4** and
     check again: the box lists **Blocked time: the dates — On assignment at the Kpone waste-water
     works.** Change the dates back.
   - **Resolve it.** Set *Trainer* to **No individual trainer** and *Vendor* to **Ghana Institution of
     Engineers**.
   - *Venue* `Kpone Site Office` · *Max participants* `12` · *Priority* `Medium` · *Registration
     opens* today · *Registration closes* the day before the start · *Cost* `0` · *Charged to budget*
     `TRN-2026-CORP` · *Budget notes* optional.
   - Press **Save schedule**. You land on the new schedule's page, status **Planned**.

> "The system warned me and did not stop me. That is on purpose: a training officer may knowingly
> double-book a trainer for something more urgent. What it will not do is let me *not know*. And the
> rule underneath is simple: a run needs someone to deliver it — a trainer, a vendor, or both."

⚠ **Careful.**
- The form refuses, with these exact messages: *"Pick a trainer or a vendor — a schedule needs someone
  to deliver it."*, *"End date cannot be before the start date."*, *"Registration cannot close before it
  opens."*
- A schedule's **Cost** is typed in; it is not computed from seats × programme cost.
- *Charged to budget* only **links** the schedule to a budget; it does not draw money. Spend is a
  payment recorded on the budget (chapter 8).

⚙ `POST /api/training-schedules` (Write) creates with status *Planned* and a generated number. The
availability button calls `CheckTrainerAvailabilityAsync`: overlapping non-cancelled schedules for that
trainer, plus *blocked* availability windows. It is advisory. A **Planned** schedule is the only one that can
be edited or deleted through the service; a *Completed* or *Cancelled* one cannot be edited.

---

## Chapter 12 — One schedule: approve it and plan its days

📍 **the schedule you just created** · `/hr/training/schedules/[id]` · `hr.head` · 4 minutes

📖 The schedule's page is the run's control room. Eight tabs: **Overview · Sessions · Nominees ·
Waitlist · Attendance · Completion · Feedback · Follow-up** — which are, left to right, the lifecycle
of one run.

👁 **Header.** The programme name, then *"SCHED-… · start – end"*, a status badge, and buttons by
state: **Approve** (Planned), **Mark completed** (*In progress* or *Registration closed* — Rule 6) and
**Cancel** (anything not finished). Four tiles: **Seats taken** (Rule 3), **Delivered by**,
**Priority**, **Registration closes**.

▶ **Walk it**
1. Read the tabs aloud, left to right, then press **Overview**. It is the editable form while the run is
   *Planned*; after approval it becomes a read-only card — *Programme, Venue, Address, Online link,
   Trainer, Vendor, Cost, Budget, Approved by, Approved on, Completed on, Completion notes* — with
   *"Only a planned schedule can be edited here."*
2. **🔴 LIVE WRITE LW-9.** Press **Approve** → *"Approve this schedule? Registration opens and people can
   be nominated onto it."* → **Approve**. The badge becomes **Registration Open**; the toast says
   *"Registration is now open."*
3. **🔴 LIVE WRITE LW-10.** **Sessions → add**: *Topic* `Practical: bleeding, burns and the site first-aid
   box`, *Date* the start date, *Start time* `09:00`, *End time* `16:30`, *Description*. The tab becomes
   **Sessions (1)**. Say: "a two-day course is two rows here — topic and timing for each day."
4. Now open **`SCHED-2026-004`** (the delivered PPA run). Show: badge **Completed**; the Overview card with
   *Approved by*, *Completed on* and *Completion notes* ("Delivered in the Boardroom, Tema Head Office.
   All but one nominee attended; certificates issued to those who passed the assessment."). **Say the
   line below, and do not look for the Mark completed button.**

> "When a run finishes, HR marks it completed. That does one more thing than change a badge — it
> credits the trainer with the session and its hours, which is what builds the trainer's record. A
> completed run is frozen: it cannot be edited, and it cannot be cancelled."

⚠ **Careful.**
- **Cancel** (header) asks for a reason and tells you *"Nominees keep their records but the run will not
  go ahead. You will be recorded as the person who cancelled it."* It is the way you undo LW-8 at the
  end (chapter 30).
- **Mark completed** will not appear on your new schedule: it is *Registration open*, and the button
  needs *In progress* or *Registration closed*. There is no status transition between them in the
  product (T-3) — do not announce that you are "about to complete it".

⚙ `POST /api/training-schedules/{id}/approve` — Write; refuses anything but *Planned*. `/cancel` refuses
*Completed* and *Cancelled*. `/complete` refuses *Cancelled* and *Completed* but **accepts *Registration
open*** — only the button's visibility rule stops you, which is why the seed could close SCHED-2026-004
through the API.

---

## Chapter 13 — Nominees: putting people on the run

📍 **the new schedule → Nominees** · `hr.head` · 5 minutes

📖 A **nomination** is one person's seat on one run — who put them there (themselves, their supervisor,
HR, management, or a mandatory-training rule), why, and where the approval stands.

👁 **Nominees tab** (*"Who is booked onto this run. Approval follows the published nomination
workflow."* — the sentence is stale; see chapter 14). Columns **Nomination · Employee · Type ·
Nominated · Status**; buttons **Nominate several** and **Nominate**; a row menu with **View details**,
**Submit** (a draft) and **Withdraw**.

▶ **Walk it**
1. Open `SCHED-2026-001` first (eight supervisors, all *Submitted*) and read the columns. Open one row's
   **View details** (this is the page chapter 14 returns to).
2. Go back to **your new schedule**. Press **Nominate**. *"Nominate onto this schedule — If the schedule
   is full, add the employee to the waitlist instead."* Choose **Efua Seidu** (she is on `SCHED-2026-003`
   — the same dates) and press **Check availability**. Expect the box **"1 clash over these dates"**,
   with an entry of the form **Training: … (dates)** naming the First Aid run, and *"You can still nominate
   them — this is a warning, not a rule."*
3. Change the employee to **someone not on the First Aid run** and press **Check availability** again:
   **"Nothing in their diary over these dates."** (Leave and travel clashes appear in the same box, by
   source.)
4. **🔴 LIVE WRITE LW-11.** With that second employee chosen: *Nomination type* **HR Nomination**,
   *Justification* `Statutory first-aider cover for the Kpone site.` → **Nominate**. Toast **Nominated**; a
   row appears with status **Submitted**.
5. **🔴 LIVE WRITE LW-12.** Press **Nominate several**. Add **three** employees with the picker (they appear as
   removable chips — include the one from step 4), press **Check availability**, leave the type on *HR
   Nomination*, add a justification and press **Nominate 3**. The alert reads **"2 of 3 nominated"** and
   lists the skipped reason: *"Already has an active nomination for this schedule."*

> "Nominating is a request for a seat, not the seat. And the system tells me the things a human would
> forget — that this person is on leave that week, that they are already booked on another course, that
> they have already been nominated. Three people at once is one dialog, and the ones it could not do
> come back with the reason."

⚠ **Careful.**
- The five nomination types are **Self-Nomination · Supervisor Nomination · HR Nomination · Management
  Nomination · Mandatory Training**. The type is a label for *who asked*; it changes no rule.
- Every nomination you create is saved as **Submitted** — there is no "save as draft" on this panel.
  **Withdraw** works on any status except *Withdrawn* and *Rejected*, and "frees their seat".
- A full schedule **refuses the approval, not the nomination** (the dialog says so) — that is why the
  waitlist exists (chapter 15).

⚙ `POST /api/training-nominations` and `/bulk` (Write; self-nomination is the self-arm of the single
route); `POST /availability-check` (Read) scans active leave, active travel and live nominations on
overlapping dates. Duplicate guard: one live (not *Rejected*/*Withdrawn*) nomination per employee per
schedule, per tenant.

---

## Chapter 14 — Approvals: the two gates

📍 **Training → Nomination Approvals** · `/hr/training/approvals` · `hr.head` · 4 minutes

📖 A nomination is approved in two gates: the **supervisor releases** the person to attend; **HR confirms
the seat and the spend**. The second gate is where capacity is enforced, and where a service bond is
raised if the programme needs one.

👁 **Page** ("Nomination Approvals", *"Nominations waiting on a decision. Supervisor first, then HR."*):
two tabs — **Awaiting supervisor** (card: *Supervisor queue — Releasing a report to attend — the first
gate*) and **Awaiting HR** (*HR queue — Confirming the seat and the spend — the final gate*). Columns
**Nomination · Employee · Programme · Starts · Status · Decision**. The Decision column has **three
icon-only buttons**: a **green tick** (Approve), a **red cross** (Reject) and an **eye** (View).

▶ **Walk it**
1. Open **Awaiting supervisor**. **It is empty** — *"Nothing waiting. No nominations are sitting with a
   supervisor."* Say the line below. (T-1)

> "The first gate is the line manager's. On this database it is empty, and I want to be straight about why:
> nominations HR enters directly are not routed into the supervisor's queue by the current build — it is a
> logged finding. What I will show you is the second gate, which works, on nominations I have already put
> through the first."

2. Open **Awaiting HR**. **Seven rows** (four First Aid, three Civil 3D) if you ran the prepare script.
   Open one with the **eye**: **Approval trail** reads *Supervisor — Akpene Amoah, decided <today>,
   comments "Demonstration preparation"*, *HR — Not yet*. **Say that this first stage was placed by the
   preparation script, if asked.**
3. Back on the queue, **🔴 LIVE WRITE LW-13**: on **one of the two *engineers* on the Civil 3D run — not
   Efua** — press the green tick. The dialog is **Approve as HR** (*"<name> — AutoCAD Civil 3D —
   Advanced."*) with a *Comments* box. Type `Approved against TRN-2026-CORP; bond applies.` → **Approve**.
   Toast **Approved**; the row leaves the queue.
4. **Say the line below, then keep the person's name — you need it in chapter 19.**

> "The moment HR approved, two things happened without anyone clicking. The system counted the seats —
> had the run been full, it would have refused and told me to use the waitlist. And because this
> programme requires a service bond, it created one: twenty-four months, thirty-eight hundred cedis,
> the terms I showed you on the programme. We will meet that bond in a few minutes."

5. Show the **Reject** dialog without pressing it: **Reject nomination** — *"The reason is recorded
   against the nomination."* — *Reason* (required; an empty reason answers *"A reason is required"*).

⚠ **Careful.**
- **Efua's** Civil 3D nomination is in the queue too. **Leave it.** She already has a bond, raised by hand
  (chapter 19), so approving her would show nothing new, and the engineer's approval is the cleaner story.
- Both gates are **legacy chain** decisions (`HR` role, administrators): the same person can do both
  stages, and nothing separates them. If asked about segregation of duties, say so (question 8).
- The Nominees panel's subtitle "Approval follows the published nomination workflow" is stale: a
  workflow definition (`TRAINING_NOMINATION`) is seeded, but it is used **only** when a *Draft* nomination
  is submitted, and no screen creates one.

⚙ `GET /api/training-nominations/pending-supervisor` = status **Supervisor review** only;
`/pending-hr` = **HR review** only. `POST /{id}/approve` `{ approverRole: 'Supervisor' | 'HR', comments }`:
*Supervisor* accepts *Submitted* or *Supervisor review* and moves to *HR review*; *HR* requires *HR review*,
runs `EnforceScheduleCapacityAsync` (refuses when *Approved + Confirmed* ≥ max participants: *"Cannot
approve: this schedule is full (n seats taken). Add the employee to the waitlist instead."*), sets
*Approved*, and calls `EnsureBondForNominationAsync`. Decisions on this path require the HR role,
SuperAdmin or TenantAdmin. `HR.Training.Approve` is the interim authority granted to the HR role.

---

## Chapter 15 — The waitlist: when the run is full

📍 **`SCHED-2026-001` → Waitlist**, then the portal's **My Waitlist** · `hr.head` and `staff` · 5 minutes

📖 When a run is full, the next person does not vanish: they join a queue. When a seat frees up HR
**offers** it, the employee **answers**, and HR **enrols** whoever accepts. Three separate acts, by
design — *accepting does not enrol on its own*.

👁 **Waitlist tab** (*"Offer a freed seat, then enrol whoever accepts. Accepting does not enrol on its
own."*): button **Add to waitlist**; columns **Position · Employee · Added · Offer · Status ·
Nomination**; a row menu whose items change with the status:

| Status | Row menu |
|---|---|
| **Active** | Offer slot · Remove |
| **Offered** | Record acceptance · Record decline · Remove |
| **Accepted** (no nomination yet) | **Enrol (create nomination)** · Remove — and the Nomination column reads *Awaiting enrolment* |
| **Declined · Expired · Removed** | Remove only |

▶ **Walk it**
1. As `hr.head`, open `SCHED-2026-001` → **Waitlist**. **Efua Seidu is #1** (*Active*). Also say that
   `SCHED-2026-002` holds two engineers who did not get one of the twelve Civil 3D seats.
2. **🔴 LIVE WRITE LW-14.** Efua's menu → **Offer slot**. The dialog **Offer a slot** takes *Offer date*
   (today) and *Expires* (a few days on); both required. **Offer**. Status becomes **Offered**, and the
   Offer column shows the date with *"expires …"*.
3. **Switch to `staff`** (second profile). **My Training → My Waitlist** (`/me/training/waitlist`, *"Where
   you stand in the queue for full training runs."*). An amber banner: **"1 seat has been offered to
   you — Answer before the offer expires."** The row shows **Accept** and **Decline**.
4. **🔴 LIVE WRITE LW-15.** Press **Accept** → *"Accept this offer? You hold the seat; the training desk
   then confirms your enrolment."* → confirm. Say the line below.

> "She has answered, and she holds the seat. But she is not on the course yet — until HR enrols her,
> there is no nomination. That is deliberate: the person answering is not the person who commits the
> Corporation's money."

5. Back to `hr.head`: refresh the Waitlist. Efua is **Accepted**, with **Awaiting enrolment**.
6. **🔴 LIVE WRITE LW-16.** Menu → **Enrol (create nomination)** → *"Enrol from the waitlist?"* →
   confirm. The Nomination column now shows a nomination number. Open **Nominees**: Efua has a new row,
   **Submitted**, with the justification *"Enrolled from the waitlist (queue position 1)."* Say: "which
   brings us back to the approval gates."

⚠ **Careful.**
- Enrolling re-checks capacity under a lock and **refuses if the run has filled since the offer** — with a
  message that tells HR to re-offer or move the person.
- Enrolling is **idempotent**: pressing it twice does not make two nominations.
- **Add to waitlist** is not restricted to full runs; it simply queues someone. The portal also lets an
  employee **Withdraw from waitlist** while *Active*.
- *Offered* offers carry an expiry date, but I found **no job that expires them**; a stale offer stays
  *Offered* until someone records an answer. Do not promise automatic expiry.

⚙ `POST /api/training-waitlist` (self-or-Write), `/{id}/offer-slot` (Write), `/{id}/respond` (self, or the
desk records it), `/{id}/promote` (Write) → `TrainingWaitlistService.PromoteToNominationAsync`, which
adopts an existing live nomination if one is there, otherwise calls `TrainingNominationService.CreateAsync`
as type **HR Nomination**. `TrainingWaitlistStatus`: Active · Offered · Accepted · Declined · Expired ·
Removed.

---

## Chapter 16 — The attendance register

📍 **`SCHED-2026-004` → Attendance** · `hr.head` · 2 minutes

📖 Attendance is taken **per day, for approved nominees only**, and saving again for the same day
*corrects* it rather than adding a second set.

👁 **Attendance tab** (*"Confirmed nominees for one day. Saving again corrects the same day rather than
adding to it."*): a **Date** box (opens on the schedule's first day); a table **Employee · Present** (a
switch) **· Absence reason** (placeholder *"Why were they away?"*); **Save register**. Everyone defaults to
**present** — "a register is quicker to correct than to fill in from nothing".

▶ **Walk it**
1. Open **`SCHED-2026-004` → Attendance**. The roll lists the officers who attended (about seven), **all
   present except the last on the roll**, whose reason reads *"Called to the Tender Committee the same
   morning."* The rows come from the register saved for that day.
2. Change the date to any other day: no register was saved for it, so **everyone appears present until
   someone saves** — "a register is quicker to correct than to fill in from nothing". Put the date back.
3. **Do not press Save register**. It would rewrite the same rows, which is harmless, but it is noise.

> "A three-day course has three registers. The absentee and the reason are on the record, and the record
> is what a manager asks for when a certificate is disputed."

⚠ **Careful.** If the prepare script was **not** run, this tab says **"Nobody to mark — The register lists
approved and confirmed nominees only. Approve nominations first."** (Rule 2). That is the single clearest
way to show *why* the approval gate matters; use it as a teaching moment, then run the script.

⚙ `POST /api/training-nominations/attendance/bulk` (Write; one actor from the token),
`GET …/schedule/{id}/attendance/by-date` pre-fills the roll. The roll is `Approved` ∪ `Confirmed`
nominations.

---

## Chapter 17 — Completion, verification and the certificate

📍 **`SCHED-2026-004` → Completion**, **Training → Completions**, then a nomination's page ·
`hr.head` · 6 minutes

📖 Three different acts, three different people in principle: HR **records** the outcome, a manager
**verifies** it, and a **certificate** is issued if the programme earns one. Only a *verified pass*
changes the employee's skills.

▶ **Walk it — record**
1. Open the **Completion** tab on `SCHED-2026-004`. *"Record completion — Mark everyone who finished this
   run in one pass. Anyone already recorded is shown as done."* Fields: **Completed on** (today) and
   **Outcome** (*Completed · Failed · Incomplete*, with the note *"Applies to everyone ticked. Record a
   different outcome in a second pass."*).
2. The table lists the roll with a checkbox, **Nominee · Nomination · Status**. Five rows are **done**
   (their boxes are disabled) on a clean rebuild; **two are not** — the sixth officer and **Efua Seidu** (she
   joined the roll after the first five were recorded). Confirm the split in rehearsal; tick whoever is not
   yet recorded.
3. **🔴 LIVE WRITE LW-17.** Tick the two, set **Completed on** to the run's end date, leave **Outcome =
   Completed**, press **Record 2 completions**. The alert reads **"2 of 2 recorded"** (*"Every row was
   accepted."*).

> "Five of seven were already recorded, with scores — 88, 74, 91, 67, and 42 for the one who did not
> pass. I recorded the other two in one pass instead of two screens. Notice what this screen records: that
> they finished and passed. It does not take a mark. Scores come from the assessment system or the
> trainer's sheet, and were loaded for the five."

▶ **Walk it — verify**
4. **Training → Completions** (`/hr/training/completions`, **Completion Verification** — *"Outcomes a
   manager still has to confirm. Verifying a pass adds the programme's skills to the employee's
   profile."*). Columns **Nomination · Employee · Programme · Completed · Score · Status · Result**
   (*Passed* / *Not passed*). Seven rows are waiting, scores visible for five.
5. **🔴 LIVE WRITE LW-18.** On **Efua's** row press **Verify** (the badge-check icon). The dialog **Verify
   completion** takes optional *Verification notes*. Type `Attended in full; practical exercise seen.` →
   **Verify**. The row leaves the queue.

> "At this instant the system did one more thing: it took the skills this course builds — procurement
> thresholds, risk assessment — and wrote them into Efua's skills record at working-knowledge level, dated
> today. A failed completion verified writes nothing. Verified training is how a course changes what we know
> about a person."

▶ **Walk it — issue**
6. Open **Efua's nomination** (from the Nominees tab, or **Training Activities**). The page shows three
   cards — *Nomination*, *Approval trail*, **Outcome** (*Status Completed · Completed <date> · Score — ·
   Result Passed · Verified*) — and in the header **Issue certificate** (it appears only for a *passed*
   completion).
7. **🔴 LIVE WRITE LW-19.** **Issue certificate**. The dialog fills *Certificate name* with `Public
   Procurement Act 663 — Practitioner Update — Certificate of Completion`; *Issued* today; *Expires (optional)*.
   Press **Issue**. The toast shows the **certificate number and a verification code. Write the code
   down.** You use it in chapter 18.

⚠ **Careful.**
- **Verification is gated:** when a programme *provides a certificate* and the completion is a pass, the
  verification is **refused** until an active certificate exists (*"Issue certificate for this nomination
  before verifying the completion."*). PPA update does not provide one, so Efua is not caught; on a
  programme such as First Aid or Supervisory Skills the order is **issue, then verify**.
- A nomination whose completion **failed** shows **Not passed** and offers no certificate button.
- The bulk screen records **a status and a pass flag** only; the server trusts the list you send it
  (it checks only that the nomination has no completion yet — not that it belongs to this run, nor that it was
  ever approved). That is why the panel greys out rows already recorded: "a checkbox that always fails is a
  trap".
- **Mark completed** on the *schedule* is a different act (chapter 12); closing a run does not record anyone's
  completion.

⚙ `POST /api/training-completions/bulk` (Write), `/{id}/verify` (Write → `WriteBackSkillsAsync` upserts the
programme's target skills onto `EmployeeSkills`, raising a lower level, in the same save), `POST
/api/training-completions/certificates` (Write) generates a per-tenant **certificate number** and a
**globally unique verification code**, and supports renewal chains (`IsRenewal`, `PreviousCertificateId`).

---

## Chapter 18 — Certificates: the register and the public check

📍 **Training → Certificates** · `/hr/training/certificates` · `hr.head` · 3 minutes

📖 The register of every certificate we issued, and the **code** a third party uses to check one is real.

👁 **Page** ("Training Certificates", *"Certificates issued off completed training, and the codes third
parties use to check them."*): a **Check a code** button; tabs **Mine · Expiring** (opens here, a 90-day
window) **· By employee**; a search box; columns **Certificate · Holder · Programme · Issued · Expires ·
Status**; a row menu with **copy code** and **Revoke**.

▶ **Walk it**
1. The page opens on **Expiring**: **"Nothing expiring"** — the certificates last twenty-four months. Say
   so; it is the right answer.
2. **By employee** → **Efua Seidu**. Her certificate from LW-19 is the only row. Note *Issued*, *Expires*,
   *Status Active*, and the verification code with its copy button.
3. Press **Check a code**, paste the code, and submit. The dialog shows the result — *found*, *valid*,
   status, the certificate name, holder and programme.
4. Type a wrong code to show the other branch (*not found*).

> "This is what a bank, a regulator or another employer sees if they are given the code: the certificate
> exists, it is valid, here is whose it is — and nothing else. No identifiers, no tenant details. The check
> works without signing in, and it is rate-limited so it cannot be used to guess codes."

5. Show **Revoke** in the row menu and say, without pressing it: *"Revoking needs the administer tier, and
   needs a reason; a revoked certificate fails this check instantly."* 🚫 (Rule 4 — answers 403.)

⚠ **Careful.** There is **no public web page** for outsiders yet — the endpoint exists, the page does
not, and TDC has not yet answered whether they want one (`HR-OPEN-QUESTIONS-FOR-TDC.md`). Do not say "we
publish a verification page".

⚙ `GET /api/training-certificates/verify/{code}` — `AllowAnonymous`, `PublicPortalPolicy` rate-limit,
returns only the narrow `CertificateVerificationResultDto`. `POST …/certificates/{id}/revoke` is
**Admin**. Statuses: Active · Expired · Revoked · Pending.

---

# ACT V — Money and obligations

> "Training that costs the Corporation real money and makes someone more employable is a risk: they
> could leave next month. A service bond is how the Corporation protects itself — and how it stays
> fair to the employee, because the bond is pro-rata."

## Chapter 19 — Service bonds

📍 **Service Bonds** (**Human Resources → Pay & Benefits → Service Bonds**, or the home page card) ·
`/hr/service-bonds` · `hr.head`, then `staff` · 7 minutes

📖 A bond is created when a nomination onto a *bonded* programme is approved. It is **not binding until
the employee accepts**. After acceptance it runs for the stated months. Leaving early **breaches** it and
the employee owes the **unserved fraction** of the amount; HR then **settles** or **waives** the debt.

👁 **Page** ("Training service bonds", *"Service obligations attached to sponsored training — who owes time,
who owes money, and what has been settled."*): a **Raise a bond** button; tiles (first: **Awaiting
acceptance**, hint *"Not binding until accepted"*); tabs **Awaiting acceptance (n) · Running (n) ·
Breached (n) · All (n)**; columns **Employee · Programme · Bond · Obligation · Status · Finance · Owed**; and
row buttons by status:

| Status | Buttons |
|---|---|
| **Pending acceptance** | Record acceptance · *Correct the terms* (pencil) · *Delete a bond raised in error* (bin) |
| **Active** (Running) | Record exit · Waive |
| **Breached** | Settle · Waive |

▶ **Walk it — HR's side**
1. Open **Awaiting acceptance**. Two rows: **Efua Seidu**, raised by hand in the seed, and the **engineer you
   approved in chapter 14**, minted by the system. Both: *AutoCAD Civil 3D — Advanced*, **24 months**,
   **GHS 3,800**, *Pending acceptance*. Say the line below.

> "Two bonds, two origins. Efua's was raised by hand — sponsorship agreed after the fact, which is the
> exception. The engineer's was created by the system the moment I approved the nomination — which is the
> rule. Same terms, taken from the programme. Neither binds anyone yet."

2. **🔴 LIVE WRITE LW-20.** On the **engineer's** bond press **Record acceptance** (HR recording a signature
   obtained on paper). Notes: `Signed copy received and filed.` → confirm. The bond moves to **Running**,
   with an obligation window: **start = today, end = start + 24 months**.
3. **🔴 LIVE WRITE LW-21.** **Running** → **Record exit**. *Exit date*: **six months after the start**. Notes:
   `Resigned to join another employer.` → confirm. The bond becomes **Breached**, and **Owed** shows the
   pro-rata: the bond × (months unserved ÷ months in total) — **18 of 24 months unserved × GHS 3,800 =
   GHS 2,850.00** (rehearse; the page rounds to the pesewa). The **Finance** column records the receivable.
4. **🔴 LIVE WRITE LW-22.** **Breached** → **Settle**. *Settled on* today. The bond becomes **Settled**; Finance's
   receivable is cleared.

> "Leaving after six months of a twenty-four month bond means repaying three quarters of it. Not all, not
> nothing. The system computes it, the finance side sees the debt raised and cleared, and nobody argues about
> arithmetic. If the officer had served the full term, the exit would have closed the bond as *Fulfilled* with
> nothing owed."

5. Show **Waive** on a Running or Breached row and say, without pressing: *"Waiving forgives the debt — and, if
   the debt had been booked, writes it off in Finance. It needs a reason, and it is an administer-tier decision."*
   🚫 (Rule 4.)

▶ **Walk it — the employee's side**
6. **Switch to `staff`.** **My Training**: the **amber banner** — *"1 service bond is waiting for your
   acceptance — Sponsored training carries a service obligation — read the terms and accept them."* Press the
   link to **My Service Bonds** (`/me/training/bonds`); the pending bond sorts first with the badge **Needs your
   acceptance**.
7. Open it (`/me/training/bonds/[id]`). An alert — *"This bond is waiting for your acceptance … Accepting starts
   a 24-month service obligation; leaving before it ends means repaying a pro-rated share of GHS 3,800."* Then
   **The terms** (*Serve for 24 months · Bond amount GHS 3,800 · Programme*), a **Full terms** box with the
   programme's text (*"The officer undertakes to remain in the service of Tema Development Corporation for
   twenty-four (24) months …"*), and **Accept these terms**.
8. **🔴 LIVE WRITE LW-23.** **Accept these terms** → *"Accept this service bond?"* with optional *Acceptance notes*
   → **Accept**. Toast **"Bond accepted — The obligation window has started — its dates are on the record
   below."** The page changes to **The record**: *Accepted on*, *Recorded by You*, *Obligation window*,
   *Remaining* (*"24 months"*).

> "She read the terms before she signed, the signature is her own account and today's date, and the clock
> starts then. If something in the terms is wrong, HR can correct a *pending* bond; once she has accepted,
> the terms are fixed."

⚠ **Careful.**
- **Withdrawing a nomination cancels its bond.** The portal says so in the withdraw dialog. Never press it in a
  demo.
- **Record exit** accepts any date, including one in the future; if the exit is on or after the end date the
  bond closes as *Fulfilled*, not *Breached*.
- **Raise a bond** (header) lists **approved nominations** to raise against; it is for the exception. Do not
  open it unless asked.
- The Finance column reflects the bond's posting to Finance through the HR posting adapter: a breach raises a
  receivable, a settlement clears it, a waiver of a breached bond writes it off. Course fees themselves are
  **not** posted by HR (question 4).

⚙ `/api/training-service-bonds`: `accept` (self), `accept-on-behalf`, `record-exit`, `settle` (Write);
`waive` and `DELETE` (**Admin**); `PUT` edits terms **only while *Pending acceptance***. A bond's duration comes
from the programme's *Service bond (months)* (default 12); its amount from the nomination's actual cost, else the
programme's cost per participant. Posting: `HrFinancePostingEventCatalog.TrainingBondBreached/Settled/Waived`
(see [`HR-FINANCE-POSTING-DESIGN.md`](../../integration/HR-FINANCE-POSTING-DESIGN.md)).

# ACT VI — Did it work?

## Chapter 20 — Feedback and follow-up

📍 **`SCHED-2026-004` → Feedback** and **→ Follow-up**, then the portal's **Course Feedback** ·
`hr.head`, then `staff` · 4 minutes

📖 Attending is not learning. The system asks twice: **immediately** — did you like it, was the trainer
good, will you use it? — and **30, 60 or 90 days later** — did you actually apply it, and does your manager
agree? These are the first three of Kirkpatrick's four levels (reaction, learning, behaviour).

👁 **Feedback tab:** four tiles — **Responses · Overall · Trainer · Would recommend** — and a table **Respondent ·
Content · Trainer · Delivery · Venue · Overall · Recommends · Submitted**. Read-only: HR does not write
feedback. **Follow-up tab:** **Employee · Type · Assessed · Uses skills · Manager · Further training**, and a
speech-bubble action that opens **Manager observation** (*Your observation*).

▶ **Walk it**
1. Open the **Feedback** tab. Three or four forms. Read the tiles: overall satisfaction out of five, the
   trainer's score, the share who would recommend. Point at the **Trainer** tile: "this is the number that
   feeds the trainer's profile — we saw it on the Trainers screen in chapter 4."
2. Open the **Follow-up** tab. Two thirty-day assessments. For the first, read the cells aloud: *Uses skills*
   (how often they have used it, 1–5) and the **Manager** column with the manager's confirmation — *"Confirmed.
   The unit's requisitions have come through clean since the course and the checklist is now in use across the
   section."* **Do not press the speech-bubble** — it writes.
3. **Switch to `staff`**: **My Training → Course Feedback** (`/me/training/feedback`, *"Tell us how your
   training went — and see what you have already said."*). **Awaiting your feedback** is empty (Rule 8, T-7) —
   *"Nothing to review"*. Scroll to **What you have already said**: Efua's form on the PPA update.
4. Describe the form without opening a new one: six star questions — *relevance of the content, the trainer's
   knowledge, the delivery, the materials, the venue and facilities, overall satisfaction* — three free-text
   boxes (*strengths, areas for improvement, comments*) and *Would you recommend this training?*

> "Two moments of evidence, not one. The form on the day tells us whether the room was comfortable. The
> follow-up three months later tells us whether anything changed at the desk. And the manager's note is the
> check on the employee's own claim."

⚠ **Careful.**
- **One form per person per course** — a second is refused (*"You have already given feedback for this course.
  It cannot be submitted twice."*). Every submission is counted into the trainer's average, which is why a
  second would be a repeatable vote.
- The follow-up types are **Pre-training · Post-training · 30-day · 60-day · 90-day**; no screen creates one.
  The two on this run were keyed through the API. Say "raised after the training", not "HR enters them here".
- The manager's observation **can be submitted by any signed-in user** on the API; the screen stamps the manager
  from the token. If asked who may write it, say the manager is intended.

⚙ `/api/training-nominations/feedback` (self-or-Write), `…/schedule/{id}/feedback`, `…/follow-up`,
`…/follow-up/{id}/manager-observation`. `CreditTrainerRatingAsync` folds each form into the trainer's rating
in the same save.

---

# ACT VII — Compliance and credentials

> "Some training is not a choice. The law or our own policy says who must hold it and how often it renews. And
> some qualifications the employee brought with them — we need to know about those too."

## Chapter 21 — Mandatory training: define it, assign it, watch it

📍 **Administration → HR → Training & Learning → Compliance Requirements** and **Training → Compliance** ·
`hr.head` · 5 minutes

📖 A **requirement** says: *this training, for this population, this often, with this much grace, and this
consequence if missed.* A **record** is one employee's standing against it.

👁 **Requirements** (`/administration/hr/training/compliance`, *"Training that specific populations must hold,
and how often it has to be renewed."*): **Code · Requirement · Satisfied by · Frequency · Effective ·
Compliance · Status**. The **Compliance** cell reads *"X % of Y assigned"* coloured red below 60, amber below
90, green from 90 — or *"Nobody assigned"*.

▶ **Walk it — define**
1. Open the list: `CMP-FA` *Statutory first-aid certification* (**Custom**, 1,095 days) and `CMP-PPA` *Public
   procurement practitioner update* (**Annual**). Read the **Compliance** cells.
2. Open **`CMP-PPA`**. Four tiles — **Assigned · Compliant · Compliance rate · Frequency** — and a header button
   **Assign employee**. Tabs **Configuration** and **Who it applies to**.
3. **Configuration** has four cards:
   - *Requirement* — *Code*, *Requirement*, *Description*, *Regulatory reference* (`Public Procurement Act 2003
     (Act 663) as amended by Act 914, s.16`).
   - *Scope* — *Organization level*, *Organization unit*, *Position*; *"Leave all blank for organisation-wide"*.
   - *Frequency & Timing* — *Program* (the training that satisfies it), *Frequency* (**One-time · Annual ·
     Bi-annual · Quarterly · Monthly · Custom**, and *Custom interval (days)* when Custom), *Grace period (days)*,
     *Effective date*, *Expiry date*.
   - *Consequences* — *Non-compliance consequences* (`Requisition-raising rights are suspended until the update
     is completed.`) and *Active*.
4. **Who it applies to**: **Employee · Status · Next due date · Exemption**; a row action **Exempt**. Open the
   first-aid requirement as well to see **one exempted person** and the reason (*"Exempted on medical advice …"*,
   expiry end of next year).

▶ **Walk it — assign (optional)**
5. **🔴 LIVE WRITE LW-24.** On `CMP-PPA` press **Assign employee**, pick someone not already listed, **Assign**.
   A new row appears with status **Non-compliant** and no due date.

▶ **Walk it — watch**
6. **Training → Compliance** (`/hr/training/compliance`, *"Who is behind on mandatory training, and how far."*).
   Tiles **Overdue · Non-compliant · Not yet due · Requirements** (a *Configure* link back to the catalogue); tabs
   **Overdue** (opens here), **Non-compliant**, **By employee**; columns **Employee · Requirement · Satisfied by ·
   Next due · Status**; badges for *Overdue* and *Exempt*.
7. **Overdue** is probably **"Nothing overdue"** — an overdue mark needs a due date, and a due date is set only when
   a record is fulfilled. Move to **Non-compliant**: the first-aid assignees, the PPA participant who failed and
   anyone you just assigned. **By employee** → Efua: her two requirements.

> "We say once what the law requires. We assign it to the people it applies to. And this page is the answer to
> the auditor's question: 'show me everyone who is behind on mandatory training'. It is organised by who owes
> what, not by who happened to attend a course."

⚠ **Careful (T-10).**
- **Assignment is explicit.** The scope boxes describe the population; they do **not** enrol anyone. I found no
  job that assigns by scope, raises due dates or marks anyone overdue.
- **Nothing marks a person compliant after they pass a course.** *Fulfil* — which sets the due date from the
  frequency (annual → +1 year, quarterly → +3 months, custom → +N days) and the grace expiry — is an API call
  with **no button**. The PPA passers were fulfilled by the seed. Efua's own PPA completion (LW-17) leaves her
  record *Non-compliant*; expect to be asked, and say "that link is a to-do".
- *Exempt* makes a record *Not applicable* with a reason and an optional expiry; it appears as **Exempt**.

⚙ `/api/compliance-training/requirements` and `…/records` (`assign`, `exempt`, `fulfill`, `overdue`,
`non-compliant`, `mine`); Read/Write, Delete = Admin. A new record is created **Non-compliant**; `Fulfill` →
**Compliant** and `NextDueDate = ComputeNextDueDate(frequency)`.

---

## Chapter 22 — Employee certificates: qualifications from outside

📍 **Training → Employee Certificates** · `/hr/training/employee-certificates` · `hr.head` · 3 minutes

📖 These are not certificates *we* issued. They are the diplomas, licences and memberships an employee holds
from other bodies. The employee **registers** them; HR **verifies** them against the evidence.

👁 **Page** ("Employee Certificates", *"Qualifications staff hold from outside bodies. Recorded here, then
verified by HR against the evidence."*): tabs **Unverified** (opens here) **· Expiring · By employee · Mine**;
columns **Certificate · Holder · Issuing body · Issued · Expires · Status**; row actions **Verify · Edit ·
Delete**; and an add dialog (employee, certificate name, issuing body, number, issued, expires, description).

▶ **Walk it**
1. **Unverified:** one row — Kojo Ansah's **Probationer Membership, Ghana Institute of Architects**
   (`GIA/PROB/2026/0231`, issued about six weeks ago, no expiry, *"Probationer member pending the Part III
   professional practice examination"*).
2. **🔴 LIVE WRITE LW-25.** Row action **Verify** → *"Verify this certificate?"* → confirm. It leaves the queue; the
   **Verified** state records *who* and *when*.
3. **Expiring** (90-day window): **Efua's PMP**, expiring in about eighty days — *"Held since 2023. Renewal requires
   60 PDUs; 42 recorded so far."*
4. **By employee** → the Head of Development to show a *Corporate Membership (MGhIE)* with no expiry.

> "A diploma on paper is a claim. HR looking at the original and clicking *verify* makes it a fact, with the
> verifier's name and the date. And the system warns us ninety days before a licence lapses, so a professional
> membership does not expire in the middle of a project."

⚠ **Careful.** *Verify* means **evidence was seen** — the screen does not attach a scan (there is no upload in
Training). **Delete** is Administer-tier. "Warns us" means *the Expiring tab lists it*; I did not find a reminder
job for these, so do not say "emails".

⚙ `/api/employee-certificates` (+ `/mine`, `/unverified`, `/expiring`, `/{id}/verify`). Self-service creates a
certificate for oneself; HR verifies (Write).

---

# ACT VIII — Growth beyond courses

> "A course is an event. A career is a path. Two more structures take training beyond the class: a **learning
> path**, which is a curriculum with an order, and **mentoring**, which is a relationship."

## Chapter 23 — Learning paths

📍 **Administration → HR → Training & Learning → Learning Paths**, **Training → Enrollments**, and the portal's
**My Learning Paths** · `hr.head`, then `staff` · 5 minutes

📖 A path is an ordered list of programmes, each optionally *locked* until an earlier one is finished, with the
skills it targets and, optionally, a certificate on completion. An employee **enrols**, and the system tracks
progress.

👁 **Learning Paths** (*"Ordered curricula — a sequence of programmes someone works through, each optionally gated
on the one before."*): **Path · Applies to · Programmes · Duration · Certificate · Status** (*Draft · Active ·
Inactive*); a note, *"A path with no programmes has nothing to enrol anyone onto."*

▶ **Walk it — design**
1. Open **New Supervisor Pathway**. Tiles **Programmes · Enrolled · Target skills · Estimated**; a header button
   **Enrol someone**; four tabs.
2. **Sequence**: `1  LEAD-101 — Mandatory`, `2  TECH-115 — Mandatory`, each *Available from the start*. Press **Add
   programme** (do not save): *Program*, *Sequence order*, *Mandatory*, **Unlocked by** (*"Available from start"* or an
   earlier step), *Notes*. Say: "set *Unlocked by* and step two stays shut until step one is done".
3. **Target skills**: *Team Leadership — Proficient · Communication — Proficient · Time Management — Working
   knowledge*. **Enrolments**: Efua. **Settings**: name, description, who it is aimed at, duration, and the certificate
   (*TDC Certificate in Supervisory Practice*).

▶ **Walk it — the desk register**
4. **Training → Enrollments** (`/hr/training/enrollments`, **Learning Path Enrollments**): tabs **Everyone · By
   employee**; columns **Employee · Path · Enrolled · Target · Progress**. Two rows: Efua and Kojo. A row opens the
   learner's own enrolment page.
5. Open **Kojo's** enrolment (still as `hr.head`). Steps list: *First Aid at Work* — **Completed** with the note that
   it was recorded by HR from **prior learning**; *Civil 3D* — outstanding. Click the outstanding step: because you
   are HR, the page says *"No attendance or completion record sits behind this step. You can still record it —
   prior learning and self-paced study are ordinary reasons — but the reason you give is kept on the record
   alongside your name."* **Do not record it.**

▶ **Walk it — the learner**
6. **Switch to `staff`** → **My Learning Paths** (`/me/learning`, *"Curricula you are working through, step by
   step."*): tiles **My paths · In progress · Completed · Average progress**; a table with a progress bar. Open the
   path. Header button **Recalculate progress**; tiles **Progress · Steps done · Target · Assigned by**; **Next up:**
   *Supervisory Skills for New Managers*; the **Steps** card (*"In sequence. A locked step opens once the one it
   names is finished."*): **TECH-115 — Completed**, evidenced by her PPA attendance and nomination number;
   **LEAD-101 — Available now**.
7. Click **LEAD-101**. The completion button is **disabled** with *"Nothing to evidence this yet … Attend a scheduled
   run, or have a completion recorded against the nomination. Completion is evidenced rather than self-declared, so
   the button stays disabled until then."*

> "A learner cannot tick their own box. The step completes when the evidence exists — they attended a scheduled run
> of that programme, or HR recorded a completion. The only exception is HR recording prior learning, and then a
> reason is compulsory and stays on the record beside HR's name. A learning path certificate therefore means
> something."

⚠ **Careful.** Both seeded paths have **no locked steps** (no prerequisites were set). A path with no programmes
cannot be enrolled onto — the **Enrol someone** button is disabled until it has one. Read the progress percentage
as the screen shows it; do not promise how it is weighted.

⚙ `/api/learning-paths` (+ `/programs`, `/skills`, `/enroll`, `/enrollments/…`). A step is *NotCompleted ·
Completed* (evidenced) *· CompletedByOverride* (HR, with a reason). Enrolment writes one step per programme in the
same save.

---

## Chapter 24 — Mentoring

📍 **Administration → HR → Training & Learning → Mentoring Schemes**, **Mentoring → Mentoring Pairs**, and the
portal's **My Mentoring** · `hr.head`, then `head.dev` · 5 minutes

📖 A **scheme** sets the terms (objectives, cadence, coordinator). A **pair** is one mentor and one mentee inside
it. A **session** is one logged conversation. The point of mentoring is trust, so **each side's private notes stay
private**.

👁 **Schemes** (`/administration/hr/training/mentoring`, *"The schemes. Pairs and session logs live under HR →
Training → Mentoring."*): tabs *All / Active*; tiles **Programmes · Active · Pairs · Active pairs**; columns
**Programme · Coordinator · Runs · Pairs · Active · Status**. **Detail:** tiles **Pairs · Active pairs · Runs ·
Cadence** (*"1/month · 60m"*), buttons **Edit · Add pair · Delete**, and a pairs table **Mentor · Mentee · Started
· Sessions · Status**.

▶ **Walk it**
1. Open **TDC Mentoring Scheme 2026**: its objective ("Shorten the time a new officer takes to become effective …"),
   coordinator (the Head of HR), cadence (one session a month, sixty minutes — *"guidance for pairs, not a limit the
   system enforces"*) and **two pairs**. Press **Add pair** to show the form (*Mentor, Mentee, Start date, End date,
   Goals, Focus areas*) — **do not save**.
2. **Mentoring → Mentoring Pairs** (`/hr/training/mentoring`, *"Active mentoring pairs across the organisation. Your
   own relationships live in your self-service portal."*): **Mentor · Mentee · Programme · Started · Sessions ·
   Status**. Two active pairs.
3. **Switch to `head.dev`** (the Head of Development; mentor of Kojo Ansah) → **My Mentoring** (`/me/mentoring`):
   tiles **My pairs · Active · Sessions logged · Completed**, and the table. Open the pair **Head of Development →
   Kojo Ansah**. *"Sessions — Logged conversations between you. Your notes are your own."* Two sessions, each with
   *Date · Duration · Format · Topics · Action items* and **My notes** — the mentor's private write-up — while the
   mentee's notes show as **withheld**.
4. **🔴 LIVE WRITE LW-26 (optional).** **Log session**: *Session date*, *Duration (minutes)*, *Format* (**In person ·
   Virtual · Hybrid**), *Topics discussed*, *Action items*, *Your notes*. Save. The count on the pair becomes three.
5. **Switch to `new.hire`** and open the same pair: Kojo sees the topics and actions you just logged, **not your
   notes**.

> "I wrote that this person is hesitant to challenge a drawing he thinks is wrong. He cannot read that, and neither
> can HR. What both of us can see is what we agreed to do next. That is what makes people honest in a mentoring
> session — and what makes it worth running."

⚠ **Careful.** The **Close pair** button requires **closure notes** and ends the relationship — do not press it. A
mentee can log a session too, with their own private notes. *Pair visibility:* mentor, mentee, the scheme's
coordinator and HR; anyone else sees **"Not available"**.

⚙ `/api/mentoring/programs`, `/pairs`, `/sessions`. `MentoringService.EnsurePairVisible` and per-author filtering of
`MentorNotes`/`MenteeNotes` and the two ratings. Create/Update scheme and Add pair = Write; Delete scheme = Admin.

---

# ACT IX — Oversight

## Chapter 25 — Training Activities: one person, everything

📍 **Training → Training Activities** · `/hr/training/activities` · `hr.head` · 2 minutes

📖 When a manager asks "what has Efua done?", this is the answer: one employee's nominations, requests,
completions, certificates and mandatory training on one page — the desk twin of her own *My Training*.

▶ **Walk it**
1. Open the page: *"Choose an employee above to see their training activities."* Pick **Efua Seidu**.
2. Five tabs with counts: **Nominations (n) · Requests (n) · Completions (n) · Certificates (n) · Mandatory (n[, k
   overdue])**. Click through; the Requests tab shows the request you approved and the one you rejected.

> "Everything we have been through, filtered to one person. This is the page I would open for an appraisal, a
> promotion board, or a tribunal."

⚙ Reads only: the per-employee endpoints of nominations, requests, completions, certificates and compliance.

---

## Chapter 26 — Analytics

📍 **Training → Analytics** · `/hr/training/analytics` · `hr.head` · 3 minutes

📖 The Managing Director's page: what we did, whether it worked, and what it cost.

👁 **Page** ("Training Analytics", *"Operations and effectiveness across the organisation."*): a **year** selector;
tiles **Completions · Pass rate · Certificates issued · Compliance**, then **Active programmes · Budget allocated ·
Budget used · Cost per completion**; a **monthly completions** chart and table (**Month · Completed · Passed · Not
passed**); a **funnel**; an **evaluation** block; **by category** and **trainer utilisation** tables.

▶ **Walk it** (leave the year on this year)
1. **Tiles.** Read them with the seed in mind — *Completions* **7** (the five plus your two), *Pass rate* about
   **86 %** (6 of 7), *Certificates issued* **5** (the four plus LW-19), *Budget allocated* **GHS 330,000**, *Budget
   used* about **9.8 %** (GHS 32,500 of 330,000 after LW-3). **Say that these will differ from yours if you skipped
   a write; the *method* is the point.** *Compliance* reads *"Nobody assigned"* only on a database with no records.
2. **Monthly completions.** All of them fall in **one month** — the seed dated them the day before the build. Say
   so.
3. **The funnel — read it left to right:** **Nominated (approved/confirmed) → Attended → Completed → Gave feedback
   (L1) → Follow-up assessed (L2/3)**. *"Each stage should be smaller than the one before it. If it is not, the data
   is wrong, and that is how we would find out."*
4. **Evaluation:** *Avg satisfaction* (out of 5), *Would recommend*, *likelihood to apply*, pre- and post-score gain.
   Where there is no data it shows **"—"**, never zero.
5. **By category** and **Trainer utilisation** (**Trainer · Sessions · Hours · Rating**): Dr. Mensah-Bonsu shows one
   session; Ing. Asamoah none yet.

> "One page, and every number on it is traceable to a record we have just looked at. Pass rate is the passed over
> everything recorded, not over those who tried. Budget used is spent over allocated, for approved budgets in the
> year. Cost per completion is the training spend over completions."

⚠ **Careful.** Do not read **Cost per completion** aloud unless you have checked it in rehearsal: it divides the
year's recorded spend by the year's completions, and the seeded payments are for three runs while the completions
are for one. There is **no export button** on this page.

⚙ `GET /api/training-dashboard/analytics?year=` (Read) → `TrainingDashboardService`. Rates are computed over the
tenant's rows only; a rate with no denominator is returned as *null*, which the screen renders as "—" or "Nobody
assigned".

---

## Chapter 27 — The employee's day: a portal recap

📍 **Portal → My Training / My Learning Paths / My Mentoring** · `staff` · 3 minutes (optional)

📖 You have seen the employee's side in pieces. Close the loop by showing it as the employee sees it, after
everything you just did.

▶ **Walk it**
1. **My Training.** Tiles now show an **Upcoming** run or two, **Awaiting approval**, **Completed** and **Passed**.
   **Nominations** tab: the PPA run (*Approved*), the First Aid run, the Civil 3D run, the one you enrolled her on
   from the waitlist. Click one to open **its page** (`/me/training/nominations/[id]`): *Nomination*, *Where the
   approval sits*, the **service bond card** (*"Review & accept the terms"* / *"View the bond"*), and **Outcome**.
2. Note the **Withdraw** button on a nomination: its dialog warns that **withdrawing also cancels any service bond**.
   **Do not press it.**
3. **Completions** tab: the PPA update, *Passed*, *Verified*. **Certificates**: *issued to you* with the
   verification code, and *your external qualifications* (the PMP, *expiring*). **Mandatory**: her two compliance
   items.
4. **My Learning Paths** and **My Mentoring** (chapters 23 and 24) — one sentence each.

> "That is the whole cycle from the other side of the desk. She asked, she was told, she held a seat, she turned up,
> she was recorded, she was verified, she holds the certificate, she owes us two years — and every step of it she can
> see, on her own page, and no one else's."

---

## Chapter 28 — Closing the demonstration

📍 `hr.head`, the Training home page · 3 minutes

▶ **Walk it**
1. Return to `/hr/training`. Run your finger across the three groups, *Day to day · Approvals · Records*, one more
   time.
2. Say the closing, and then the four things the demonstration surfaced.

> "We began with what the Corporation can teach. We found who needs what, planned it, budgeted it. Staff asked for
> what we had not thought of, and were answered with reasons. We ran a course: scheduled it, put people on it,
> approved them, took the register, recorded and verified the result and issued a certificate that a stranger can
> check. Where we paid, the employee owes us time, and the system works out the debt if they leave. We asked whether
> it worked, twice. We tracked the training the law requires and the qualifications people bring. We gave people
> paths and mentors. And one dashboard shows the Managing Director what we spent and what we got."

> "You saw some things I was straight about. Four need a decision from TDC rather than a line of code:
> **who holds the authority to approve a plan or a budget, waive a bond or revoke a certificate**; **where the
> supervisor's release comes from**, because HR-entered nominations do not reach that queue yet; **whether an
> outsider may check a certificate on a public page**; and **whether passing a course should mark a mandatory
> requirement as met by itself**. The rest is built as I have shown it."

3. **Do not** open any chapter's reset steps in front of the room. Section 30 is for afterwards.

---

## Chapter 29 — The known-gaps register

Every finding the guide mentions, with the chapter you meet it in and what to say. They are findings against the
**code as read on 2026-10-06**; none has been fixed by this guide. Item numbers are `T-n`.

| ID | What the product does | Where you meet it | What to say, or do |
|---|---|---|---|
| **T-1** | A nomination HR creates (single, bulk, or enrolled from the waitlist) is saved as *Submitted*. The *Awaiting supervisor* queue reads only *Supervisor review*, the *Awaiting HR* queue only *HR review*; **nothing sets *Supervisor review***. The nomination's own page has no Approve button. The only way to put a nomination in front of a screen is the API (`POST …/approve`, `Supervisor` then `HR`) | 13, 14, 16 | Run the prepare script (§ 1.4). Say the supervisor gate is a logged finding; show the HR gate |
| **T-2** | *Seats taken* and the *Seats* column count only the status *Confirmed*, which nothing sets. The capacity rule itself counts *Approved* + *Confirmed* | 11, 12 | Do not read the seat counts. Say "capacity is enforced at HR approval" |
| **T-3** | *Mark completed* renders only for *In progress* / *Registration closed*; no screen or job moves a schedule there. The API accepts completion from *Registration open* | 12 | Show `SCHED-2026-004`, already completed. Do not announce closing a run |
| **T-4** | Approve plan, Approve budget, Revoke certificate, Waive bond and every Delete need `HR.Training.Admin`. The HR role lacks it (403); the platform `admin` has no employee record (400). Budgets are pre-approved by a seeder; the 2027 plan stays *Pending approval* | 7, 8, 18, 19 | Narrate, do not press. Name the decision TDC owes |
| **T-5** | Only the HR role and administrators hold `HR.Training.*`. Managers (`head.dev`, `gm.ops`), the MD and Internal Audit cannot open the desk. *Training request* approval is `Write`, so it is HR's — **the runbook's "switch to `head.dev` and approve" does not work** | 1, 10 | Do every desk step as `hr.head` |
| **T-6** | No screen takes a score. *Record completion* captures an outcome and a pass flag; scores exist only through the API | 17 | Say scores come from the assessment; five were loaded |
| **T-7** | The portal's *Awaiting your feedback* list shows only *Confirmed* nominations, which nothing sets, so it is empty for everyone | 20 | Show *What you have already said* |
| **T-8** | *Requires approval* and *Min participants* on a programme are stored and shown; no training code reads them | 5 | Do not claim they enforce anything |
| **T-9** | Stale text: the Training home page subtitle and *Setup & Catalog* card still say planning lives under Administration; the Nominees panel says approval "follows the published nomination workflow" | 2, 13 | Mention once, move on |
| **T-10** | Compliance never updates from training. *Assign* creates a *Non-compliant* record with no due date; **Fulfil** (which sets the next due date from the frequency) is API-only; no job assigns by scope or marks overdue | 21 | Say "that link is a to-do" |
| **T-11** | No file upload in Training: materials, employee certificates and certificates hold a path or URL only | 5, 17, 22 | Do not say "upload" |
| **T-12** | The nomination workflow definition (`TRAINING_NOMINATION`, roles Manager/HR/TenantAdmin) is seeded but is used **only** when a *Draft* is submitted — and no screen creates a draft. Request decisions use no workflow | 14, Q1 | Answer question 1 |

---

## Chapter 30 — What you wrote, and how to undo it

Every write is permanent in the sense that nothing here is *deleted* by the HR role (Rule 4). Where a counter-act
exists it is shown; where none exists, the answer is the same: **rebuild the database** (`scripts/New-UatDatabase.ps1`,
45–60 minutes — [`UAT-DEMO-DATABASE.md`](../../operations/UAT-DEMO-DATABASE.md)). Do that before the next rehearsal
and after the last performance.

| LW | Chapter | Write | Undo |
|---|---|---|---|
| 1 | 6 | New needs assessment | None (delete is Admin). Harmless — it is one more row on the list |
| 2 | 6 | Recommended programme on it | **Remove** the row on its tab |
| 3 | 8 | Payment of GHS 650 on `TRN-2026-DEV` | Record a **−650 credit**, same reference |
| 4 | 9 | Draft request (FIDIC) | **Delete** it — **before** LW-5 only |
| 5 | 9 | Request submitted | None |
| 6 | 10 | Civil 3D request approved | None — a decided request cannot be reopened |
| 7 | 10 | FIDIC request rejected | None |
| 8 | 11 | New schedule | **Cancel** it (reason required): it stays listed as *Cancelled* |
| 9 | 12 | Schedule approved | Cancel, as above |
| 10 | 12 | Session added | **Remove** from the Sessions tab |
| 11 | 13 | One nomination | **Withdraw** (frees the seat; the row stays as *Withdrawn*) |
| 12 | 13 | Two or three nominations | Withdraw each |
| 13 | 14 | HR approval (and a bond minted) | None; withdrawing the nomination **cancels** the bond |
| 14 | 15 | Slot offered to Efua | **Remove** from the waitlist |
| 15 | 15 | Efua accepted | None |
| 16 | 15 | Efua enrolled (nomination created) | Withdraw that nomination |
| 17 | 17 | Two completions recorded | None |
| 18 | 17 | Efua's completion verified | None; the skills it wrote to her profile remain |
| 19 | 17 | Certificate issued | None (revoke is Admin) |
| 20 | 19 | Bond acceptance recorded | None |
| 21 | 19 | Bond exit recorded (*Breached*; receivable raised) | None |
| 22 | 19 | Bond settled | None |
| 23 | 19 | Efua accepted her bond | None |
| 24 | 21 | Employee assigned to `CMP-PPA` | **Exempt** with the reason "Demonstration" (it becomes *Not applicable*); the row remains |
| 25 | 22 | Kojo's certificate verified | None |
| 26 | 24 | Mentoring session logged | The mentor can **delete** their own session |

If you did every write, the database is no longer a demonstration database. **Rebuild it.**

---

## Chapter 31 — Questions you will be asked

**1 · Is nomination approval configurable?** A workflow definition for nominations is seeded for every tenant
(approvers: Manager, HR, TenantAdmin). It is used **only** if a *Draft* nomination is submitted — and no screen
creates drafts. Every nomination created from a screen takes the **legacy two-gate chain** (supervisor, then HR),
which only HR roles and administrators can decide. TDC decides which it wants; T-1 and T-12.

**2 · Who approves training plans and budgets?** Someone holding `HR.Training.Admin`: the Managing Director's
office in practice. No employee-linked account holds it in the demonstration database (T-4).

**3 · How do skills get updated?** When a manager **verifies** a passed completion, the programme's target skills are
written onto the employee's skills record — a lower existing level is raised, a missing skill is added. A failed
completion writes nothing.

**4 · Does training spend post to Finance?** **Bond events do**, through the one HR posting adapter: a breach raises a
receivable, settling clears it, and waiving a breached bond writes it off. **Course fees and budget payments are HR's
own record**; the budget page reads Finance's actuals for the same GL account and shows both, expecting them to differ.
See [`HR-FINANCE-POSTING-DESIGN.md`](../../integration/HR-FINANCE-POSTING-DESIGN.md).

**5 · Can an outside body verify a certificate?** There is a **public, rate-limited verification endpoint** that returns
only validity and the certificate's basic details. There is **no public web page**, and TDC has not yet said whether
it wants one ([`HR-OPEN-QUESTIONS-FOR-TDC.md`](../../programme/HR-OPEN-QUESTIONS-FOR-TDC.md)).

**6 · Can employees nominate themselves?** The API allows it (the *Self-Nomination* type is the self-arm of the create
endpoint), but **the portal has no "nominate me" screen**: an employee raises a *request* or joins a *waitlist*, and HR
nominates. Say so rather than demonstrate it.

**7 · How is this different from Orientation?** Orientation is the **induction of new joiners** — programmes with
in-system content, a quiz and a signed acknowledgement, plus an onboarding checklist. Training is **managed delivery of
courses, often external**: schedules, seats, trainers, budgets, bonds. They share the training vendor register and
nothing else. See [`HR-ORIENTATION-SYSTEM-GUIDE.md`](../orientation/HR-ORIENTATION-SYSTEM-GUIDE.md).

**8 · Is there segregation of duties between the supervisor and HR gates?** **No.** On the legacy chain the same HR
user can decide both gates; neither is checked against the nominee's reporting line. A seeded workflow could route the
first step to the line manager if TDC wants it (question 1).

**9 · Are there bulk operations?** Yes, three in Training: **nominate several**, **record completion in bulk** and
**needs assessment in bulk**, plus the **attendance register**, which saves a whole day at once. Each loops the same
single-item service. See [`HR-BULK-OPERATIONS-CATALOGUE.md`](../../catalogues/HR-BULK-OPERATIONS-CATALOGUE.md).

**10 · Is training money counted once?** Not necessarily. The manpower budget's training line, the training budget in
this module and a succession development activity's cost can each hold the same spend; the three are not reconciled
and the integration documents list it as an open decision
([`HR-FINANCE-ENTITY-SWEEP.md`](../../integration/HR-FINANCE-ENTITY-SWEEP.md)).

**11 · What does it connect to?** The **appraisal** raises training requests; **verified completions** write to the
skills record; the **competency gap** screen (Talent & Performance → Competencies) sits beside it; the
**availability check** reads leave and travel; **bonds** post to Finance; **succession** plans point at mentoring; the
**orientation** facilitator form reads the vendor register; and **leave** and **attendance** do not know about training.

**12 · What about safety training?** It is a **separate store** at `/hr/safety/training` (Safety, Health &
Environment): it records contractors as well as staff and tracks statutory expiry, which corporate training does not
model. The two share nothing but the employee reference.

**13 · Can someone be double-booked?** The availability check **warns and does not block**, for leave, travel and
other courses on the same dates. The one hard rule is a single live nomination per person per run.

**14 · Do people get notified?** I have not verified training's notices, and the demonstration database has **no mail
server**, so nothing is emailed. Check **My Notifications** as `staff` after LW-6 and LW-7 before promising anything.

**15 · Can we load existing training history?** I found no import for training. See
[`HR-IMPORT-EXPORT-CATALOGUE.md`](../../catalogues/HR-IMPORT-EXPORT-CATALOGUE.md).

**16 · Can we export reports?** The Analytics page has no export. See
[`HR-REPORTS-CATALOGUE.md`](../../catalogues/HR-REPORTS-CATALOGUE.md) for the three reporting patterns HR uses and which
a training report would take.

---

## Appendix A — Vocabulary

| Family | Values (as displayed) |
|---|---|
| **Programme type** | Workshop · Seminar · Conference · On-the-Job Training · Mentoring/Coaching · Certification Program |
| **Programme source** | Internal · External · Online / E-Learning |
| **Programme level** | Beginner · Intermediate · Advanced · Expert · Any Level |
| **Material type** | Handbook · Presentation Slides · Video · Document · Exercise/Activity · Assessment · Reference Material |
| **Vendor type** | Individual Consultant · Training Firm · Accredited Institution · University / Tertiary · Government Agency · NGO / Non-Profit · Other |
| **Proficiency** | Basic · Working Knowledge · Proficient · Advanced · Expert |
| **Needs source** | Performance Review · Self Assessment · Manager Request · Skills Gap Analysis · Job Role Change |
| **Priority** | Critical · High · Medium · Low |
| **Plan status** | Draft → Pending Approval → Approved → In Progress → Completed (also Revised) |
| **Budget status** | Draft → Approved → Active → Closed (also Denied) |
| **Request status** | Draft → Submitted → Approved or Rejected (also Cancelled) |
| **Schedule status** | Planned → Registration Open → Registration Closed → In Progress → Completed (also Cancelled, Postponed) |
| **Nomination type** | Self-Nomination · Supervisor Nomination · HR Nomination · Management Nomination · Mandatory Training |
| **Nomination status** | Draft · **Submitted** · Supervisor Review · **HR Review** · **Approved** · Rejected · Waitlisted · Confirmed · Withdrawn |
| **Waitlist status** | Active → Offered → Accepted (then enrolled) · Declined · Expired · Removed |
| **Completion status** | Not started · In progress · Completed · Failed · Incomplete · Exempted |
| **Certificate status** | Active · Expired · Revoked · Pending |
| **Bond status** | Pending acceptance → Active → Fulfilled · Breached → Settled · Waived · Cancelled |
| **Compliance status** | Compliant · Non-compliant · Partially compliant · Not applicable (an exemption) |
| **Compliance frequency** | One-time · Annual · Bi-annual · Quarterly · Monthly · Custom (days) |
| **Learning path** | Draft · Active · Inactive; step: Not completed · Completed (evidenced) · Completed by override |
| **Mentoring** | pair: Active · Paused · Completed · Cancelled; session format: In person · Virtual · Hybrid |
| **Follow-up type** | Pre-training · Post-training · 30-day · 60-day · 90-day |

## Appendix B — The permission ladder

| Permission | Grants | HR role | `admin` |
|---|---|---|---|
| `HR.Training.Read` | the organisation-wide training surface: registers, completions, needs, budgets, plans, vendors, trainers, enrolments, mentoring pairs, bonds, dashboard | **yes** | yes |
| `HR.Training.Write` | run the desk: maintain the catalogue, nominate and enrol, decide requests, record and verify completions, issue certificates, mark attendance, maintain needs, budgets, paths, mentoring, compliance assignments | **yes** | yes |
| `HR.Training.Admin` | approve **budgets and annual plans**, revoke certificates, blacklist vendors, waive bonds, **delete** records and configuration | **no** | yes — but no employee record, so the controllers answer 400 |
| `HR.Training.Approve` | interim: approve or reject nominations where no workflow is published | yes | yes |

The policies nest: Admin satisfies Write satisfies Read. **Self-service is not a permission** — "my" screens and
self-acts (request, waitlist response, bond acceptance, feedback, mentoring sessions, learning steps) are ownership
checks on the endpoint. Managers, the Managing Director and Internal Audit hold **no** grant here.

## Appendix C — Where every screen lives

| Screen | Route |
|---|---|
| Training home | `/hr/training` |
| Needs assessments · new · bulk · one | `/hr/training/needs-assessments` · `/new` · `/bulk` · `/[id]` |
| Training plans · new · one | `/hr/training/plans` · `/new` · `/[id]` |
| Training budgets · new · one | `/hr/training/budgets` · `/new` · `/[id]` |
| Requests · new · one | `/hr/training/requests` · `/new` · `/[id]` |
| Schedules · new · one | `/hr/training/schedules` · `/new` · `/[id]` |
| Nomination approvals | `/hr/training/approvals` |
| One nomination | `/hr/training/nominations/[id]` |
| Enrollments (learning paths) | `/hr/training/enrollments` |
| Training activities (one employee) | `/hr/training/activities` |
| Completion verification / completions | `/hr/training/completions` |
| Certificates | `/hr/training/certificates` |
| Employee certificates | `/hr/training/employee-certificates` |
| Compliance | `/hr/training/compliance` |
| Analytics | `/hr/training/analytics` |
| Mentoring pairs | `/hr/training/mentoring` |
| Service bonds | `/hr/service-bonds` |
| Setup hub | `/administration/hr/training` |
| Categories · groups | `/administration/hr/training/categories` · `/program-groups` (each `/new`, `/[id]/edit`) |
| Vendors · trainers | `/administration/hr/training/vendors` (`/new`, `/[id]/edit`) · `/trainers` (`/new`, `/[id]`) |
| Programmes | `/administration/hr/training/programs` (`/new`, `/[id]`) |
| Compliance requirements | `/administration/hr/training/compliance` (`/new`, `/[id]`) |
| Learning paths | `/administration/hr/training/learning-paths` (`/new`, `/[id]`) |
| Mentoring schemes | `/administration/hr/training/mentoring` (`/new`, `/[id]`) |
| My Training · request · request detail | `/me/training` · `/requests/new` · `/requests/[id]` |
| My nomination · waitlist · feedback | `/me/training/nominations/[id]` · `/waitlist` · `/feedback` |
| My service bonds · one bond | `/me/training/bonds` · `/me/training/bonds/[id]` |
| My learning paths · a path · a step | `/me/learning` · `/me/learning/[id]` · `/me/learning/[id]/steps/[stepId]` |
| My mentoring · a pair | `/me/mentoring` · `/me/mentoring/[id]` |

