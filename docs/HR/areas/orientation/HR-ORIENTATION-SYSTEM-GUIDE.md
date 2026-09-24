# HR Orientation & Onboarding — System Guide and Demonstration Workbook

**Status:** written 2026-09-24 from the source and the demo database. The sources were:
- the twenty page components and their shared panels;
- the ten controllers, the services and the three background hosts;
- the EF model and the seeded permission map;
- the demo seeder and the two demo scenarios;
- round 4's execution log — lanes I, I-b, J, K-a, K-b1, K-b2, L and M, which rebuilt most of what
  this module does automatically.

It describes what the code is built to do. Where a screen promises something the server does not
do, the step says so rather than smoothing it over. ⚠ **Nothing in it has been walked in a
browser.** Every screen was read, not clicked.

**Scope:** the whole **Orientation & Onboarding** group of the HR sidebar, the **Orientation &
Onboarding** setup area under Administration → HR, and the three portal screens every employee
has.

| Where | Screens | Chapters |
|---|---|---|
| **HR → Orientation & Onboarding** — nine | the landing · *Dashboard* · *Sessions*, and one session · *Enrollments* · *Enrollment Triggers* · *Onboarding Plans*, and one plan · *Task Queues* | 3–11 |
| **Administration → HR → Orientation & Onboarding** — eight | the setup hub · *Programmes*, a new programme, and one programme · *Categories* · *Onboarding Templates*, and one template · *Reminders & notices* | 12–17 |
| **The employee portal** — three | *My Orientations*, and one programme · *My Onboarding* | 18–19 |

Chapter 20 lists where the module shows up elsewhere — recruitment, the employee record, staff
movements, HR policy settings, the email templates and the training vendor register. Chapter 23
lists the findings: 65 in the code, four in the demo data.

---

## This document is two things at once

Like the recruitment, company schedule and other guides in this series, this is a **reference** and
a **script you can perform**. Every chapter has the same five parts, then its known gaps, and you can read
only the ones you need:

| Part | Marked | Use it for |
|---|---|---|
| **Where you are** | 📍 | the sidebar path, the URL, which persona, how long |
| **What it is** | 📖 | one paragraph you could say to a non-technical room |
| **On the page** | 👁 | every control on the screen, exhaustively — nothing omitted |
| **Walk it** | ▶ | numbered steps: click this, expect that, say this |
| **Behind the page** | ⚙ | endpoint → service → table, and the permission that gates it |
| **Known gaps** | ⚠ | what the screen promises and does not do — numbered findings, collected in chapter 23 |

Three more markers appear inside the walks:

- **🔴 LIVE WRITE** — this step changes real data. Every one is numbered, and the reset chapter
  says how to undo each.
- **⚠ CAREFUL** — a way this step goes wrong in front of people, and what to do instead.
- **🚫 DO NOT PRESS** — a control that renders for your persona and returns 403, or one that
  writes something you cannot undo inside a demo.

**Say-lines are in quotation marks and indented.** They are written to be read aloud more or less
as they stand. Change the names, keep the order of the ideas — the order is doing the work.

---

## The eight rules

Read these before chapter 1. Each one is a fact about the code or the demo database that changes
what you can safely show.

| Rule | |
|---|---|
| **1 · Rebuild the database, then check it.** | The harness suites for this module leave their fixtures live (**D-1**): on UAT, 505 programmes, 1,523 enrolments and 6,727 overdue onboarding tasks. On such a database no number in this guide holds. § 2.2 is a thirty-second check that you are on a clean rebuild |
| **2 · `hr.head` is offered every delete, and can make only one.** | Every delete and remove in the module needs `HR.Orientation.Admin`, which the HR role does not hold: a programme, a session, a module, a content item, a question, a prerequisite, a rule, a category, a template, a template task, a facilitator. Each answers 403 (**O-10**). Do not press them. **The exception is a template's audience** (chapter 16): HR can remove one, and its trash button acts at once, with no confirmation |
| **3 · Nobody can finish a programme in this demo.** | New Employee Onboarding and the code of conduct course need a signed declaration that no screen can create. The Q3 briefing has nothing that ever evaluates completion (**O-15**). Do not promise a completion or a certificate. The one completed enrolment, with certificate OCERT-2026-00001, is the seeder's |
| **4 · Never open a participant's page as HR.** | *View progress* on the enrolments screen, and a name on the dashboard's overdue list, open the participant's own player with its buttons live. A signature made there records HR's IP address against the participant (**O-4**) |
| **5 · An onboarding plan is fixed once it is made.** | No screen edits, cancels or deletes a plan, removes a task or reopens one (**O-32**, **O-36**). Everything you add in a demo stays; chapter 21 undoes it with SQL |
| **6 · Fill in every number box.** | An empty number box is sent as 0. *Max participants* and *Validity (months)* are then refused, although both placeholders say they may be left empty (**O-9**, **O-44**) |
| **7 · Nothing is emailed on the demo database.** | There is no mail server (**D-4**). Every notice arrives in *My Notifications*, and every email reads *No mail server*. Say "notified", not "emailed" |
| **8 · Every date is relative to the build.** | The starters were hired six weeks to four months before it, so every open onboarding task is overdue from the first day. A month after the build, every induction enrolment is overdue too. Kojo Ansah counts as a new hire for six weeks after the build. The sessions fall a week, a fortnight and six weeks out |

---

## Conventions

**Routes.** `/hr/orientation/sessions/[id]` is the file
`frontend/src/app/hr/orientation/sessions/[id]/page.tsx`. A segment in square brackets is a
parameter.

**Table names.** A table is named after its `DbSet<>` property in `ApplicationDbContext.HR.cs`:
`OrientationProgram` lives in `OrientationPrograms`, `OnboardingPlan` in `OnboardingPlans`. The
onboarding entities are declared in `RecruitmentEntities.cs`, not beside the orientation ones,
because a plan began life as the last step of a hire.

**The permission ladder.** Three permissions gate the module:

| Permission | Grants | Held by the `HR` role? |
|---|---|---|
| `HR.Orientation.Read` | *"View the orientation surface: programs and their content, categories, session schedules and attendance, employee orientation records and progress, notifications and the dashboard."* Every HR screen in this guide needs it | **yes** |
| `HR.Orientation.Write` | *"Run the orientation desk: maintain programs, categories and sessions, enroll and progress employees through orientation, record attendance and completion, and send notifications."* Also onboarding plans, tasks and templates, and running the reminder sweep | **yes** |
| `HR.Orientation.Admin` | *"Delete orientation records, programs, categories and sessions."* Also the trigger sweep's *run now* | **no** |

The policies nest: Write satisfies Read, and Admin satisfies both. The HR role holds Read and Write.
**Nothing an HR officer does in a demo needs Admin, except deleting and the trigger sweep's button**,
and both come up where they matter.

**Self-service is not a permission.** *"A new joiner does not need this for their own orientation —
self access is an ownership check on the endpoint."* The two portal pages, *My Orientations* and *My
Onboarding*, take the employee from the sign-in and refuse anybody else's records. An employee with
no HR permission at all uses them every day.

**The actor is the token.** Who enrolled somebody, who completed a task, who verified it and who
issued a certificate come from the signed-in user. Where a request body still carries an id for the
actor, the service ignores it.

---

## 1. How orientation and onboarding hang together

### 1.1 Two things that share a menu

The sidebar calls them one area. They are two different artefacts, and the difference is worth a
sentence in any demonstration:

```
 ┌──────────────── ORIENTATION — a course people complete ─────────────────┐
 │                                                                          │
 │   OrientationProgram   ORI-ONB-001 · New Employee Onboarding             │
 │   type · delivery · priority · deadline · pass mark · certificate        │
 │   Draft → Pending approval → Active → Suspended / Retired → Archived     │
 │                                                                          │
 │     ├── Modules → content items   video · PDF · link · text · …          │
 │     ├── Assessment questions → options (which one is right)             │
 │     ├── Prerequisites             another programme first                │
 │     ├── Audience rules            who is enrolled automatically, when    │
 │     └── Sessions                  scheduled runs: date, venue, seats,    │
 │                                   facilitators, an attendance register   │
 │                                                                          │
 │   EmployeeOrientation — one person's enrolment on one programme          │
 │     progress through the content · quiz attempts · acknowledgement ·    │
 │     certificate · feedback · attendance                                   │
 └──────────────────────────────────────────────────────────────────────────┘

 ┌──────────────── ONBOARDING — a checklist people work through ──────────┐
 │                                                                          │
 │   OnboardingPlanTemplate   TDC New Starter Checklist                     │
 │     └── task templates     "Issue the laptop" · due day 5 · owned by    │
 │                            a position · needs sign-off?                  │
 │     └── audiences          which hires this template is for             │
 │                                                                          │
 │   OnboardingPlan — one new hire's first weeks                            │
 │     start date · coordinator · buddy                                     │
 │     ├── tasks   Pending → In progress → Completed                        │
 │     │                          ↘ Pending verification → Completed        │
 │     ├── comments on a task                                               │
 │     └── assets  the kit: laptop, access card, email account, keys…       │
 └──────────────────────────────────────────────────────────────────────────┘
```

Four points follow, and they are the four worth landing in a room:

**1. A programme is a course; a plan is a checklist.** Orientation is what a person learns and
proves they learned — content, a quiz, a signed acknowledgement, a certificate. Onboarding is what
the organisation does for a new hire — the ID card, the laptop, the bank details, the oath — and most
of its tasks belong to somebody other than the new hire.

**2. People arrive on a programme three ways.** HR enrols them by hand. An **audience rule** enrols
them when something happens: a hire, a transfer, a promotion, a programme published, or a date. And a
programme that **recurs** renews each person's cycle when their certificate is due. The last two are
round 4's, and chapter 8's diagnostic says, for any one person, which of them applies and why.

**3. A plan arrives two ways.** HR creates it from a template, or — since round 4 — it is created
**automatically when a hire's start is confirmed**, from the most specific template whose audience
reaches that hire.

**4. The two halves do not know about each other.** The standard checklist has a task called *"Attend
the corporate induction"*. It is not connected to the enrolment on the corporate induction
programme: completing one does not tick the other (**O-1**).

### 1.2 The tables

Twenty-seven, in four groups.

| Group | Tables |
|---|---|
| **The catalogue** | `OrientationCategories` · `OrientationPrograms` · `OrientationModules` · `OrientationContentItems` · `OrientationAssessmentQuestions` · `OrientationAssessmentOptions` · `OrientationPrerequisites` · `OrientationAudienceRules` · `OrientationSessions` · `OrientationSessionFacilitators` |
| **What happens to people** | `EmployeeOrientations` (the enrolment) · `OrientationContentProgresses` · `OrientationAssessmentResponses` · `OrientationAttendanceRecords` · `OrientationAcknowledgements` · `OrientationCertificates` · `OrientationFeedbacks` · `OrientationNotifications` — which is also the email outbox |
| **Onboarding** | `OnboardingPlanTemplates` · `OnboardingTaskTemplates` · `OnboardingPlanTemplateAudiences` · `OnboardingPlans` · `OnboardingTasks` · `OnboardingTaskComments` · `OnboardingAssets` |
| **The reminder sweep** | `OnboardingOrientationReminderRuns` · `OnboardingOrientationReminderDispatchLogs` |

### 1.3 The vocabularies

Eleven families matter; the rest are look-ups.

**Programme status** — *Draft* · *Pending approval* · *Active* · *Suspended* · *Retired* · *Archived*.
Only **Active, and within its effective dates**, takes enrolments — by any route: HR's, a rule's, a
renewal's.

**Session status** — *Draft* · *Published* · *Enrollment open* · *Enrollment closed* · *In progress*
· *Completed* · *Cancelled* · *Postponed*.

**Enrolment status** — who the person is to the programme: *Pending confirmation* · *Confirmed* ·
*Waitlisted* · *Active* · *Completed* · *Cancelled* · *No show* · *Withdrawn*.

**Completion status** — how far they have got: *Not started* · *In progress* · *Pending assessment*
· *Pending acknowledgement* · *Completed* · *Failed* · *Overdue* · *Exempted*. ⚠ Two statuses, not
one: a person HR withdrew keeps the completion status they had (**O-2**).

**Enrolment source** — *Auto rule* · *Self enrolment* · *HR assigned* · *Manager assigned* ·
*Recurrence*.

**Trigger** — what fires an audience rule: *On hire* · *On transfer* · *On promotion* · *On programme
publish* · *Scheduled* · *Manual*.

**Population** — narrows a rule's target: *Anyone* · *New hires* (employed 90 days or less) ·
*Management* (heads a unit or manages somebody) · *Contractors*.

**Delivery mode** — *In person* · *Virtual instructor-led* · *Self-paced online* · *Blended* ·
*Video on demand* · *Printed material*. It decides whether sessions make sense at all.

**Plan status** — *Not started* · *In progress* · *Completed* · *Overdue* · *Cancelled*.

**Task status** — *Pending* · *In progress* · *Completed* · *Overdue* · *Waived* · *Blocked* ·
*Pending verification*.

**Certificate status** — *Active* · *Expired* · *Revoked* · *Reissued*.

### 1.4 When is an enrolment finished?

**One rule, in one place.** An enrolment is **Completed** when three gates are all open:

| Gate | Open when |
|---|---|
| **Content** | every **live** content item is done — or the programme has none |
| **Assessment** | the programme requires none, or an attempt has passed |
| **Acknowledgement** | the programme requires none, or it has been signed |

- Content done and the quiz passed, with only the signature outstanding, reads **Pending
  acknowledgement** — its own state, so HR can see who is waiting to sign.
- A quiz that was attempted and not passed reads **Failed** until a retake passes.
- The **first** completion issues the certificate, if the programme gives one, and tells the person.

*"Live"* matters. A content item that has been retired, or that sits on a retired module, is
invisible to the participant, so it is not counted either. Before that rule, retiring one slide deck
stranded every enrolment on the programme short of 100% (the area 15 harness found it).

⚠ **Every** live item means every one: an item or module marked *Optional* is counted too
(**O-58**).

⚠ **Nothing but these three acts evaluates the gates:** tracking content, submitting the quiz and
signing a declaration. Two consequences follow for the demo database (**O-15**, Rule 3):
- **the acknowledgement gate cannot open.** A declaration is created per enrolment, only by an HR
  endpoint no screen calls. So New Employee Onboarding and the code of conduct course stop at
  *Pending acknowledgement*;
- **a programme with no content and no quiz never completes.** The Q3 briefing gives its participant
  nothing to track or submit, and the attendance register is not read.

⚠ **"Overdue" is worked out, not stored.** Nothing writes the *Overdue* completion status except the
demo seeder. The overdue lists read *"past its due date and not completed or exempted"* — and that
read does not ask whether HR has withdrawn the person (**O-2**).

### 1.5 How people get enrolled — the rules that fire

Before round 4 the answer to *"do the triggers fire?"* was **no**. Rules were stored and read by
nothing, and a plan was made only by a person. Now:

| Trigger | Fires from | When |
|---|---|---|
| **On hire** | creating an employee — the form and the import — and confirming a hire's start in recruitment | on the day, or after the rule's delay |
| **On transfer / On promotion** | implementing a staff movement: a transfer, lateral move or secondment counts as a transfer; a promotion as a promotion | the same |
| **On programme publish** | the programme becoming Active | the same |
| **Scheduled** | the nightly sweep | **every** pass, for anyone the rule reaches who is not yet on the programme. ⚠ On *all employees* that is the whole workforce on the first night — which is why the seeded compliance rule is *Manual* (§ 2.1) |
| **Manual** | only HR's *Enrol audience now*, after a preview | when pressed |

The rules the triggers keep, each decided in round 4:

1. **A dated trigger fires within 30 days of its event, and no later.** A late-entered hire is
   caught up; an import of the existing workforce does not reach back into history.
2. **Hire rules count from the employment date only.** No employment date, no enrolment — and the
   diagnostic says to set it.
3. **Any earlier enrolment blocks a rule**, including one HR withdrew. A rule never puts back
   somebody a person took off, and that is also what makes every re-run harmless.
4. **An exclusion applies whatever trigger it was written for.**
5. **A mandatory prerequisite holds an automatic enrolment back** until it is met. A manual one is
   not gated: automation is not a judgement.
6. **Demotion, acting appointments and redesignation fire nothing.**
7. **Only people the programme is still for are renewed**, when a recurring programme's cycle comes
   round — and the automation **never re-enrols** anybody who ended a cycle any other way than by
   completing it. HR can: chapter 7's *Re-enrol* and *Open the next cycle now*.

⚠ **"Tonight" is a timer, not a time of day.** The trigger sweep runs 13 minutes after the API
starts and then every 24 hours; *"enrols tonight"* on the diagnostic means *at the next pass*.

### 1.6 What reminds people, and what tells them

Three mechanisms, all round 4's:

| | What | When |
|---|---|---|
| **The reminder sweep** | eight rules — a task due soon, overdue (at due, a week late, a fortnight late) or waiting for sign-off; an orientation due soon or overdue; a quiz not attempted and an acknowledgement not signed after the chase window; a certificate about to expire. **One digest per person per run**, in-app and by email | 19 minutes after the API starts, then every 24 hours; or HR's *Run now* (chapter 17) |
| **Lifecycle notices** | **enrolled**; **placed on or moved into** a session; a session going live (its **employee facilitators** — *"You are facilitating…"*); a live session **moved** (everybody on it, with *Was:* and *Now:*), **postponed** or **cancelled**; **completed**, with the certificate; a certificate **issued by HR**; a **plan made** (the new hire, the coordinator, the buddy); a task **given or passed on**; a task **done that needs signing off** (the coordinator). ⚠ An **external** facilitator is told nothing: they have no inbox here | at the moment it happens, in the same save as the change |
| **The email outbox** | every notice and digest is an in-app notification first; its email is queued on the same row and sent by a host that runs every minute | within a minute or two |

Every notice lands in the person's **My Notifications** in the portal whether or not an email can be
sent. The outbox records what each email did: *Sent*, *Failed* or *Timed out* (retried twice, five
minutes apart), *No mail server* and *No address* (final at once), *Stale* (three days in the queue).
⚠ **The demo database has no mail server**, so every email there ends *No mail server* and the
in-app notification is the delivery (Rule 7).

A programme's **Send reminders** switch governs its enrolment notices and its five orientation
reminder kinds, and nothing else: a session called off is announced either way.

### 1.7 Where the approval happens

**Nowhere.** No record in this module goes through the workflow engine:

- a programme is published by changing its status;
- an enrolment is HR's decision, or a rule's;
- a task that needs a second person's **sign-off** is the nearest thing to an approval. The person
  who completed it cannot sign it off.

*Pending approval* exists as a programme status, but no screen routes a programme to anybody. It
is a label HR can choose, and any status can follow it (chapter 14, **O-46**). A session's *Requires
approval* switch does nothing either (**O-11**).

---

## 2. Before the room fills — the prep

**Time needed: 20 minutes the evening before.** Three things decide whether this module demonstrates
well, and none of them is a screen:
- the database has to be **clean**, because the harness suites for this module leave hundreds of
  records behind;
- you need to know **which records are the demo's**;
- **every date in it is relative to the day it was built**, and the onboarding dates were already
  in the past on that day.

### 2.1 What the demo database holds after a rebuild

Three builders make it. `OrientationDataSeeder`, an EF seeder, lays down the catalogue.
`scenarios/140-orientation-benefits.mjs` adds the starters' enrolments, the knowledge check one of
them sat, and the induction days with their facilitators. `scenarios/145-onboarding.mjs` adds the
checklists, the plans, the queues and the kit. ⚠ **This is what a fresh rebuild holds**; § 2.2 says
what the harness adds on top.

**The catalogue — three programmes in three categories** (*Onboarding* · *Compliance & Regulatory* ·
*Product & Launches*):

| Programme | Shape | Content | What it demonstrates |
|---|---|---|---|
| **ORI-ONB-001 New Employee Onboarding** | Onboarding · **Blended** · High priority · 30-day deadline · pass mark 70% · acknowledgement · certificate valid 24 months | 4 modules, 6 items, 3 questions | the whole completion gate, and the induction days |
| **ORI-CMP-001 Anti-Harassment & Code of Conduct** | Compliance · **Self-paced online** · Mandatory · 14-day deadline · pass mark 80% · acknowledgement · certificate valid 12 months · **recurs annually** | 1 module, 2 items, 1 question | renewal, and a programme that rightly has no sessions |
| **ORI-PRD-001 Q3 Product Launch Briefing** | Product launch · **Virtual instructor-led** · Medium · 21-day deadline | no content, no quiz | a programme that is only its live session |

**Its rules.** ORI-ONB-001's *"All new hires on joining"* (**On hire**, the *New hires* population, a
one-day delay). ORI-CMP-001's *"All employees annually"* (**Manual** — *"Enrol all employees for the
annual compliance refresh, by hand: press Enrol audience now on the programme."*). ORI-CMP-001 also
names ORI-ONB-001 as an **advisory** prerequisite: *"Recommended to complete onboarding before the
compliance module."*

**Three sessions:**

| Session | When | Who delivers it |
|---|---|---|
| **OSN-PRD-001 Q3 Product Launch — Live Briefing** | 7 days after the build, 10:00–11:30 UTC · virtual · 50 seats with a waiting list · enrolment closes the day before | two employees: a lead who has confirmed and a subject-matter expert who has not |
| **Corporate Induction Day — *month*** | ORI-ONB-001, a fortnight after the build · open for enrolment | the Head of HR leads; **GIMPA** co-facilitates from the training vendor register — *Dr. Efua Mensah-Bonsu*, confirmed |
| **Corporate Induction Day — *month*** | six weeks after the build · open for enrolment | the Head of HR, and GIMPA with its trainer *to be confirmed* |

**People on programmes:**

- **The six new starters** — Patrick Appiah, Adwoa Fiadzo, **Kojo Ansah**, Beatrice Gbedemah,
  Cynthia Sarpong and Lariba Zakaria — enrolled on ORI-ONB-001 by HR. Kojo Ansah, the `new.hire`
  persona, has sat the knowledge check: **100% at the first attempt**. Kojo is still *in progress*,
  because the content and the acknowledgement are not done.
- **Three people the seeder chose alphabetically.** On UAT they are:
  - **Abdul-Rahman Aryee**, who *completed* ORI-ONB-001 — with certificate **OCERT-2026-00001**,
    valid 24 months, a signed acknowledgement and five-star feedback — and is booked on the Q3 live
    briefing;
  - **Abena Lartey**, *in progress* at 40%;
  - a third person, **overdue** on compliance.

  ⚠ "Alphabetically" means whoever exists when the seeder runs. On UAT the third is *Abena Legal*
  (LA-LEG-002), a persona from another module.
- **Twenty-nine more, by the second day after the build.** The personas of the other modules — 26
  legal and estates staff (`LA-…`) and 3 facilities staff (`FAC-…`) — are all employed on the build
  day (**D-3**). So the new-hire rule enrols them onto New Employee Onboarding at its first nightly
  pass once its one-day delay has run. UAT was built on 2026-09-20, and the rule enrolled them on
  2026-09-22. Before that pass the programme holds **8** enrolments and the database **10**; after
  it, **37** and **39**. None of them gets an onboarding plan: only recruitment's confirm-start
  makes one (**O-29**).

**Onboarding:**

- **TDC New Starter Checklist** — the **default** template, nine tasks, each owned by a post. The
  registry opens the personal file (day 1) and issues the ID card (day 2). The site safety induction
  is day 3; MIS creates the email account (day 3) and issues the laptop (day 5). HR captures the bank
  details (day 5); the Head of HR takes the oath of secrecy (day 7). The starter meets the directorate
  (day 10) and attends the corporate induction (day 14).
- **TDC Field Staff Starter** — five tasks, site and kit first, **for the Operations Directorate**:
  the audience that makes chapter 8's template verdict worth showing.
- **A plan for each of the six starters.** Each is coordinated by the Head of HR (**Akpene Amoah**)
  and has a buddy from the starter's own unit. Each has eleven tasks: the template's nine, plus two
  queued to a unit — the biometric clock (the *Human Resource* section) and the telephone
  directory (the *Communications* section).
- **Kojo Ansah's plan is In progress:**
  - three tasks done;
  - a two-comment thread on *"Create the corporate email account"*;
  - four kit items — the ID card and laptop *Issued*, the email account *Ready*, the desk keys
    *Pending*.

⚠ **Every plan starts on the starter's hire date, and every hire date is six weeks to four months
before the build.** So every open task is **overdue from the first day** (63 of the 66 on UAT). That
is what fills the overdue queue, and it is also why the new-hire rule never reached them: it looks
back 30 days. Scenario 140 enrols them by hand for that reason.

**What the sweeps will have done by the time you open it.**
- **The trigger sweep** runs 13 minutes after the API starts and then every 24 hours. From the day
  after the build it has enrolled the twenty-nine, above.
- **The reminder sweep** runs 19 minutes after the API starts. It sends the Head of HR **one
  digest** of the starters' overdue tasks: on UAT, *"41 onboarding tasks need your attention"*,
  twenty-five by name and *"…and 16 more"*. Two starters' tasks are not in it, because they are more
  than 90 days late and count as history. The overdue compliance participant gets a digest too.
- Each digest lands in **My Notifications**, and none is emailed (Rule 7).

### 2.2 The evening before

**1 — Is the database clean?** Run this as a database administrator. It only reads:

```sql
SELECT (SELECT COUNT(*) FROM OrientationPrograms WHERE IsDeleted = 0)                      AS programmes,
       (SELECT COUNT(*) FROM OrientationSessions WHERE IsDeleted = 0)                      AS sessions,
       (SELECT COUNT(*) FROM EmployeeOrientations WHERE IsDeleted = 0)                     AS enrolments,
       (SELECT COUNT(*) FROM OnboardingPlans WHERE IsDeleted = 0)                          AS plans,
       (SELECT COUNT(*) FROM OnboardingPlanTemplates WHERE IsDeleted = 0 AND IsActive = 1) AS templates;
```

| | programmes | sessions | enrolments | plans | templates |
|---|---|---|---|---|---|
| **A clean rebuild** | 3 | 3 | 10, then 39 by the second day after the build | 6 | 2 |
| **UAT on 2026-09-24**, measured | 505 | 108 | 1,523 | 820 | 122 |

Anything above the clean numbers means a harness suite has run since the rebuild (Rule 1). Rebuild
again rather than demonstrate on it.

**2 — The personas:**

| Sign in as | Who | For |
|---|---|---|
| `hr.head` | **Akpene Amoah**, TDC/00009, Head of HR: the HR role, with Orientation Read and Write and not Admin | chapters 3–17, and the end of 19 |
| `new.hire` | **Kojo Ansah**, TDC/00063, Architecture, under the Operations Directorate: on New Employee Onboarding, quiz passed; plan *In progress* | chapters 18 and 19 |
| `staff` | **Efua Seidu**, TDC/00017, Development Department: on nothing until LIVE WRITE 4 | optional, after chapter 7 |
| a SuperAdmin | nobody in the story | chapter 8's sweep button, and chapter 21's deletes |

**3 — Two browser profiles**, so that `hr.head` and `new.hire` stay signed in side by side. As
`hr.head`, open tabs on the Dashboard, Enrollments, Enrollment Triggers, Kojo Ansah's plan and the
Task Queues. As `new.hire`, open *My Orientations* and *My Onboarding*.

**4 — The checklist:**
- [ ] the counts above read clean;
- [ ] the API has been up for **20 minutes** since its last start, so both sweeps have run;
- [ ] it is **at least two days after the build**, so the twenty-nine are enrolled (§ 2.1);
- [ ] you know what has moved with the date (Rule 8);
- [ ] you will not promise a completion or a certificate (Rule 3);
- [ ] you will not open a participant's page as HR (Rule 4).

---

## 3. `/hr/orientation` — the group landing

### 📍 Where you are

**Sidebar:** Human Resources → **Orientation & Onboarding** (the group header is the link) ·
`/hr/orientation` · as **hr.head** · **2 minutes**

### 📖 What it is

> *"Two jobs under one roof. Orientation is what a new joiner — or anybody — has to learn and prove
> they learned: the induction, the compliance refreshers, the product briefings. Onboarding is what the
> organisation has to do for a new hire: the ID card, the laptop, the bank details, the oath."*

### 👁 On the page

**Header:** *Orientation & Onboarding* — *"Induction programmes and their scheduled sessions, plus the
onboarding checklist a new hire works through. Catalogue authoring lives under Administration."* No
back-link; this is a group root.

**Eight navigation cards in three groups**, each a whole-card link:

| Group | Card | Goes to |
|---|---|---|
| **Day to day** | **Dashboard** — *Completion rates, overdue participants and expiring certificates.* | `/hr/orientation/dashboard` |
| | **Sessions** — *Scheduled runs of a programme — dates, venue, facilitators and seats.* | `/hr/orientation/sessions` |
| | **Enrollments** — *Who is on which programme, how far through, and what is overdue.* | `/hr/orientation/enrollments` |
| | **Enrollment Triggers** — *Why a rule did — or did not — enrol someone, and which onboarding template they get.* | `/hr/orientation/triggers` |
| **Onboarding** | **Onboarding Plans** — *A new hire’s checklist — tasks, owners, due dates and provisioned assets.* | `/hr/orientation/onboarding` |
| | **Task Queues** — *Everything overdue or awaiting verification across every open plan.* | `/hr/orientation/onboarding/queues` |
| **Setup** | **Programmes** — *The induction catalogue — modules, content, prerequisites and assessments.* | `/administration/hr/orientation/programs` |
| | **Onboarding Templates** — *Reusable task checklists instantiated onto each new hire’s plan.* | `/administration/hr/orientation/onboarding-templates` |

No data, no counts: the page is a map. There is no *My orientation* card — an employee's own view
moved to the portal (chapter 18).

### ▶ Walk it

**1 — Open `/hr/orientation`.** Read the three group names, not the cards.

> *"Day-to-day work, the onboarding checklists, and the setup behind both. The setup — the catalogue
> of programmes and the checklist templates — is decided once and lives under Administration, the
> way every other module in this product splits it."*

**2 — Click *Dashboard*** and continue into chapter 4.

### ⚙ Behind the page

No API call. The eight cards are hard-coded. Each target enforces its own permission; the two
**Setup** cards lead into `/administration/hr/*`, which needs the `admin.hr` gate the HR role holds.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-3 · The Setup cards are offered to everyone who reaches the page.** A viewer without `admin.hr` is sent silently to `/dashboard` by the Administration layout. Harmless for the HR role, which holds it | |

---

## 4. `/hr/orientation/dashboard` — what needs chasing

### 📍 Where you are

**Sidebar:** … → Orientation & Onboarding → **Dashboard** · `/hr/orientation/dashboard` · as
**hr.head** · **4 minutes**

### 📖 What it is

> *"The orientation desk's morning view: how much of the catalogue is live, how far through people
> are, who is overdue, what is about to run and whose certificate is about to lapse."*

### 👁 On the page

**Header:** *Orientation Dashboard* — *"Completion across the induction catalogue, plus what needs
chasing."*, back-link to the landing, and an outline **View enrollments** button.

**Six tiles in two rows** — none clickable:

| Tile | Shows | Tone |
|---|---|---|
| **Programmes** | every programme, with *"N active, N draft"* | — |
| **Enrollments** | every enrolment, with *"N in progress, N not started"* | — |
| **Completion rate** | completed ÷ every enrolment, with *"N completed"* | green at 80% or more |
| **Overdue** | past their due date and not completed (**O-2**) | red when any |
| **Upcoming sessions** | *"Scheduled and still to run"* — starting within **30** days | — |
| **Certificates expiring** | *"Holders will need to retake the programme"* — within **30** days | amber when any |

**Two bar cards:** **Programmes by status** (*"Where the catalogue sits."*) and **Enrollments by
completion** (*"How far through people are."*), one bar per status that has anything in it.

**Three lists:**

| Card | Rows | Empty |
|---|---|---|
| **Overdue participants** — *"Past their completion deadline. These are the ones to chase."* | a table: **Participant** (a link, with the employee number) · **Programme** · **Progress** · **Due** (red) · **Status** | *"Nobody is overdue"* |
| **Upcoming sessions** — *"What is about to run."* | each a link to the session: title, programme, start, a status badge and *"N seats left"* or *"Uncapped"* | *"Nothing scheduled."* |
| **Certificates expiring** — *"Holders will need to retake the programme to stay current."* | holder, programme, certificate number, *"Expires …"* | *"Nothing lapsing soon."* |

⚠ **Each list holds the first ten only**, while the tiles count everything, and nothing on the screen
says so (**O-6**).

### ▶ Walk it

**1 — Open the dashboard.** On a clean rebuild, by the second day after the build, expect roughly:
- **Programmes 3** (*"3 active, 0 draft"*);
- **Enrollments 39** — 10 before the hire rule's first pass (§ 2.1);
- a completion rate of about **3%**, one completed (10% before that pass);
- **Overdue 1**, **Upcoming sessions 2**, **Certificates expiring 0**.

*(Derived from what the builders create, not measured: UAT is too full of harness records to show
the clean numbers — Rule 1.)*

> *"Three programmes live. Thirty-nine people on them, one finished. One overdue. Two sessions in the
> next month, and no certificate about to lapse."*

**2 — Point at *Completion rate*.**

> *"Three per cent looks bad and is honest. Nearly everybody on these programmes was put there this
> month — twenty-nine of them by the new-hire rule, by itself — and they have thirty days."*

**3 — Read the *Overdue participants* row.** On a clean rebuild it is the one person the seeder put
overdue on the compliance programme.

> *"Past the deadline, nothing started — and this is the list the reminder sweep has already chased.
> The system told them before it told me."*

⚠ **CAREFUL — do not click the participant's name.** It opens **that person's own orientation
page in the employee portal** — their content, their quiz, their acknowledgement — and as HR you can
act on it: mark their content done, submit their assessment, sign their declaration. Nothing on the
screens would say it was not them: a signature records HR's IP address, and only an audit column
holds HR's user id (**O-4**). Read the row; do not open it.

**4 — Point at *Upcoming sessions*, then open *Sessions* from the sidebar** and continue into
chapter 5. Its LIVE WRITE 1 makes the session that chapter 6 is walked on.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Everything | `GET api/orientation-dashboard` — one aggregate read | `HR.Orientation.Read` |

`OrientationDashboardService` computes it all server-side:
- *Upcoming sessions* are those starting in the next 30 days in **any status but Cancelled** — a Draft
  and a Postponed session count (**O-6**).
- *Certificates expiring* are Active ones lapsing in the next 30 days.
- *Overdue* is the rule in § 1.4.
- The completion rate is completed ÷ all, to one decimal; the page rounds it.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-2 · Withdrawn enrolments are counted and listed as overdue.** The overdue read looks at the completion status and the due date, not at whether HR withdrew the person — and withdrawing changes only the enrolment status. Measured on UAT on 2026-09-24: the tile read **11**, and **10** of them were withdrawn. The reminder sweep gets this right; the dashboard does not | |
| **O-4 · The participant link puts HR inside that person's own orientation, with their controls.** It goes to `/me/orientation/{enrollmentId}` — the portal player, in the portal shell, which does not name the participant and whose back-link goes to the viewer's own list. The service lets HR through every participant action on any enrolment: tracking content, submitting the assessment, signing or declining the acknowledgement, giving feedback. The acknowledgement then reads *Signed* with HR's IP address; only the audit column holds HR's user id. **For a code-of-conduct acknowledgement that is evidence HR can make for somebody.** The enrolments screen's *View progress* goes to the same place (chapter 7) | |
| **O-5 · Refusals and failures read as "empty" across the module.** Here a 403 reads *"Could not load the dashboard — Please try again in a moment."*, which is at least distinct. Most other screens do worse: their queries have no error branch, so a refusal is drawn as an empty list. Each chapter names its own | |
| **O-6 · The dashboard's windows and cut-offs are unstated.** Lists show the first ten; *Upcoming* and *Expiring* use 30 days while the sessions screen uses 60; Drafts and Postponed sessions count as *"about to run"* | |

---

## 5. `/hr/orientation/sessions` — the scheduled runs

### 📍 Where you are

**Sidebar:** … → Orientation & Onboarding → **Sessions** · `/hr/orientation/sessions` · as
**hr.head** · **4 minutes**

### 📖 What it is

> *"A programme is the course; a session is one run of it — a date, a room or a link, a number of
> seats, the people delivering it, and a register of who came."*

### 👁 On the page

**Header:** *Orientation Sessions* — *"Scheduled runs of an induction programme — dates, venue,
facilitators and the attendance register."*, back-link, and a **New session** button.

**Three tabs and a search box.** The search runs in the browser over the loaded tab, matching the
title, the session code or the programme. Placeholder: *"Search by title, code or programme…"*

| Tab | Loads |
|---|---|
| **Next 60 days** *(default)* | sessions starting in the next 60 days, in **any status but Cancelled** — Drafts included |
| **Open for enrollment** | status *Enrollment open*, with its enrolment deadline not passed |
| **Published** | status **exactly** *Published* — not *Enrollment open*, not anything later (**O-7**) |

**Nine columns:** **Code** (a link) · **Session** (a link) · **Programme** · **Delivery** · **Starts**
(or *"Not scheduled"*) · **Enrolled** (seat-holders only) · **Seats left** (a badge — red at 0, grey at
1–3 — or *"Uncapped"*) · **Status** · and a **Run again** button on every row.

**Empty:** *"No sessions"* with the tab's own line — *"Nothing is scheduled in the next 60 days."* /
*"No session is currently accepting enrollments."* / *"No published sessions yet."* ⚠ The same text
appears when the search matches nothing, and **when the read is refused**: this list has no error
state (**O-5**).

**The "Schedule a session" dialog** (from **New session**, or opened by itself when the URL carries
`?schedule=<programme>` — which is where the enrolment dialog's *Schedule one* link lands, chapter 7):
- description: *"A new session starts as a draft. Publish it from its own page once the facilitators
  and joining details are settled."* (**O-8**);
- the session form (below), with **Programme** — only **active** programmes are offered;
- buttons **Cancel** · **Create session**.

It creates the session as a **Draft**, numbered `OSN-{year}-NNNN` by the server, and lands on its
page with *"Session created — OSN-2026-000N — it starts as a draft."*

**The session form** — the same one the session's own page edits (chapter 6):

| Card | Fields |
|---|---|
| **Scheduling** | **Programme** * *(create only)* · **Title** * · **Description** · **Delivery mode** * · **Max participants** — placeholder *"Leave empty for uncapped"* · **Scheduled start** · **Scheduled end** · **Enrollment deadline** — and on the session's page, **Actual start** · **Actual end** |
| **Logistics** | **Venue** *(in person, blended)* or **Joining link** *(virtual, self-paced, video)* · **Recording URL** · **Participant instructions** · **Allow a waitlist** — *"Enrolling onto a full session queues the participant instead of being refused."* · **Requires approval** — *"Enrollments start as pending confirmation."* (**O-11**) |

The form refuses an end before the start, and an enrolment deadline after the session starts —
*"The enrollment deadline should fall on or before the session starts."*

⚠ **Fill in *Max participants*.** Its placeholder says an empty box means uncapped. From the code,
an empty box is sent as zero and refused under the field — so a session cannot be made or saved
uncapped from this form (Rule 6, **O-9**, not browser-walked).

### ▶ Walk it

**1 — Open the list.** On *Next 60 days*, a clean rebuild shows three: the **Q3 Product Launch — Live
Briefing** a week out, and the two **Corporate Induction Days**.

> *"Three runs in the next two months. The product briefing is virtual — a link, fifty seats. The two
> induction days are the classroom half of our blended onboarding programme: the online modules
> people do on their own, the day they come in."*

**2 — Open the *Open for enrollment* tab.** The same three, because all three are open.

**3 — 🔴 LIVE WRITE 1 — *Run again* on the October Corporate Induction Day.** Starts: the same
weekday a month after the November one, 09:00. Leave *Ends* blank, and **change the title** to name
the new month — the hint says so: *"Change it if it names the old date or month."*

> *"Running the same briefing again is the common case, so it is one action. Same programme, same
> venue, same seats, same facilitators — reset to unconfirmed, because they agreed to the old date,
> so somebody has to ask them again — and it keeps the same length and the same notice for
> enrolment."*

The toast reads *"Session scheduled — OSN-2026-000N is a draft. Publish it once the facilitators
confirm."*, and you land on the new session: chapter 6 continues there.

*Undo:* chapter 21. It is a Draft with nobody on it.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| *Next 60 days* | `GET api/orientation-sessions/upcoming?daysAhead=60` | `HR.Orientation.Read` |
| *Open for enrollment* | `GET …/open-for-enrollment` | `HR.Orientation.Read` |
| *Published* | `GET …/status/Published` | `HR.Orientation.Read` |
| Programme options | `GET api/orientation-programs/active` | `HR.Orientation.Read` |
| **Create session** | `POST api/orientation-sessions` | `HR.Orientation.Write` |
| **Run again** | `POST api/orientation-sessions/{id}/clone` | `HR.Orientation.Write` |

Table: `OrientationSessions`. Every write here needs the signed-in account **linked to an
employee**; an unlinked administrator gets a 400.

**What a copy carries** (*Run again*):
- **Carried:** a new code; status **Draft**; the same programme, description, delivery mode, venue,
  joining link, capacity, waitlist and approval flags, and instructions.
- **Recalculated:** the end is the new start plus the old length, unless given; the enrolment
  deadline keeps the same notice.
- **Facilitators:** copied **unconfirmed**, register picks with their snapshot.
- **Not carried:** enrolments, attendance, the actual start and end, and the recording.

**A session can only be scheduled for an active programme**, and the server now says so — 422 *"Sessions
can only be scheduled for an active programme; "…" is Retired. Publish it first."* — where it used to
accept anything (round 4, lane L).

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-7 · There is no "all sessions" view.** The third tab is labelled *Published* and loads status Published only. A session that is closed, in progress, completed, cancelled or postponed and outside the next 60 days, and **any Draft with no start date**, appears on no tab — including one you just created without a date, once you leave its page. A finished session is reachable only from its own page, which is why *Run again* is also on the session page | |
| **O-8 · "Publish it" is not the step that opens enrolment.** There is no Publish button — status changes go through *Change status…* — and a *Published* session still refuses enrolment. Only **Enrollment open** takes people (chapter 6) | |
| **O-9 · *Max participants* cannot be left empty**, whatever its placeholder says. From the code: an untouched number box submits an empty string, which the schema turns into 0 and refuses. The same trap is documented and fixed elsewhere in the frontend. Not browser-walked | |
| **O-5** · the list shows a refusal as *"No sessions"* | |

---

## 6. `/hr/orientation/sessions/[id]` — one run

### 📍 Where you are

**From:** any session link · `/hr/orientation/sessions/[id]` · as **hr.head** · **8 minutes**

### 📖 What it is

> *"One run of a programme, in full: when and where, who is delivering it and whether they have
> confirmed, who is on it, and the register of who actually came."*

### 👁 On the page

**Header:** the session title; *"OSN-2026-000N · New Employee Onboarding · Blended"*; back-link; and,
left to right, a **status badge**, a **Change status…** select, **Run again**, and a trash button
(**Delete session**).

- **Change status…** offers every status except the current one — **any status can follow any
  status** (**O-13**). The confirmation reads, for example, *"Change status to EnrollmentOpen?"* —
  the raw value — with one of three lines:
  - to *In progress* or *Completed*: *"The actual start or end time is stamped automatically if it has
    not been set."*;
  - to *Cancelled*: *"Participants keep their records, but this run will not go ahead."*;
  - to anything else: *"This changes who can enrol on the session."*
- 🚫 **Delete session** is `HR.Orientation.Admin`, and **403 for `hr.head`** (Rule 2, **O-10**).

**Four tiles:** **Starts** · **Ends** · **Enrolled** (*"N / max"*, with *"Full — new enrollments are
waitlisted"* or *"… are refused"* when full, amber) · **Enrollment closes**. Below them, when the
session has one, the venue and a **Joining link**.

**Four tabs:**

| Tab | Holds |
|---|---|
| **Overview** | the session form of chapter 5, **without** Programme (a session cannot change programme) and **with** Actual start / Actual end; **Cancel** · **Save changes** |
| **Facilitators (N)** | who delivers it — below |
| **Roster (N)** | every enrolment on the session, **any** status: **Participant** · **Enrollment** · **Completion** · **Progress** · **Enrolled**. Empty: *"Nobody enrolled yet — Enrol participants onto this session from the Enrollments screen."* with **Go to enrollments** |
| **Attendance** | the register — below; read-only once the session is Cancelled |

**The Facilitators tab.** A count, **Add facilitator**, and a table: **Facilitator** · **Role** ·
**Confirmed** (*Confirmed* / *Awaiting*) · **Notes**. Each row names where the facilitator comes from:
- *"Employee"*;
- *"From the training register · GIMPA · trainer to be confirmed"*;
- *"External"* for somebody typed in.

A register pick whose vendor has changed since shows an amber note, such as *"GIMPA has since been
blacklisted in the training vendor register."* The row menu offers **Edit**, **Mark confirmed** /
**Mark unconfirmed**, and 🚫 **Remove**, which is Admin-only (**O-10**).

The **Add facilitator** dialog asks first **"Who is it?"**, then shows that mode's fields, and always
**Role** and **Has confirmed**:

| Mode | Fields |
|---|---|
| **One of our employees** — *"Picked from the employee list."* | **Employee** *, a search |
| **From the training vendor register** — *"A vendor on file, and its trainer once known."* | **Vendor** * (active vendors, *"· preferred"* marked); **Trainer** — *"To be confirmed by the vendor"* or one of the vendor's active trainers |
| **Someone else** — *"Type their details — for a one-off speaker."* | **Name** *, **Organisation**, **Email** |

A register pick is **snapshotted**. The name, email and organisation are copied from the register
when the pick is made or changed, and kept: *"renaming the vendor later does not change this
session."* The dialog refuses a blacklisted or inactive vendor, a trainer who belongs to another
vendor, and one of your own internal trainers (*"add them as an employee"*), each with its own
sentence (round 4, lane M).

**The Attendance tab** — *"Attendance register"* — *"Seat-holding participants for one day of this
session. Saving the same day again corrects it rather than adding to it."*
- **Session day** (1, 2, …) chooses the day.
- **Save day N** saves it.
- The line under it reads *"N attending, N not, of N on the roll."* and *"Already marked: day 1."*
- One row per seat-holder: **Status** (*Present*, *Late*, *Partial*, *Excused*, *Absent*, *Not
  recorded*), **In** / **Out** times, and an **Absence reason** for *Absent* or *Excused*.
- Every row starts at **Present**: *"a register is quicker to correct than to fill from nothing."*
- Empty: *"Nobody to mark — … Enrol someone first."*

⚠ Times are recorded against **today's** date, so a register filled in after the day records the
wrong day's times (**O-17**).

### ▶ Walk it

**1 — You are on the session LIVE WRITE 1 made.** Read the header: *Draft*.

> *"A draft. Nobody can enrol on it yet, and nobody has been told about it — which is right, because
> the people delivering it have not agreed to the new date."*

**2 — Open *Facilitators*.** Two rows, both **Awaiting**: the Head of HR, an employee, and GIMPA
*"From the training register"*.

> *"The Head of HR leads it, and GIMPA co-facilitates. GIMPA is not typed in — it is picked
> from the training vendor register, the same register Training uses, so if GIMPA were blacklisted
> tomorrow this row would say so. And what it says about GIMPA today is kept: renaming the vendor
> later does not rewrite who delivered a past session."*

**3 — 🔴 LIVE WRITE 2 — ⋯ → *Mark confirmed* on both rows.** Each flips to *Confirmed*. There is no
toast; the badge is the confirmation.

**4 — 🔴 LIVE WRITE 3 — *Change status…* → *Enrollment Open*.** Read the confirmation line aloud —
*"This changes who can enrol on the session."* — and confirm.

> *"Open for enrolment. And the moment it went live, the Head of HR — an employee facilitator — was
> sent a notice: 'You are facilitating the Corporate Induction Day'. GIMPA is outside the organisation,
> so the system has nobody to tell there — that is a phone call."*

⚠ Only *Enrollment open* takes enrolments. *Published* does not, whatever the create dialog's
*"Publish it"* suggested (**O-8**).

**5 — Open the October session instead** *(the seeded one)* and its **Facilitators** tab: GIMPA with
*Dr. Efua Mensah-Bonsu*, confirmed. Then the **November** one: GIMPA, *"trainer to be confirmed"*.

> *"Two kinds of register pick. In October GIMPA has named who is coming, and that trainer has confirmed. In
> November GIMPA is booked and has not said who yet — which is how vendors actually work."*

**6 — The register.** Open **Q3 Product Launch — Live Briefing** → **Attendance**. One person on the
roll — the seeder's session booking — shown **Not recorded**, with *"Already marked: day 1."*,
because the seeder saved an empty mark for day 1.

> *"The register is per day, for the people holding a seat. A day nobody has marked starts everyone
> at Present, because a register is quicker to correct than to fill from nothing. Save the same day
> again and it corrects itself rather than adding a second line."*

🚫 **Do not save a register here** — the session has not happened, and times are recorded against
today. Show it; do not press **Save day 1**.

**7 — 🚫 DO NOT PRESS the trash button.** It is Admin-only (Rule 2). If a session is not going to
run, **Change status… → Cancelled** is the action, and it tells everybody on it.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The session | `GET api/orientation-sessions/{id}` | **none beyond signing in** — an enrolled participant can read when and where their session is |
| Roster, register rows | `GET api/employee-orientations/session/{id}` | `HR.Orientation.Read` |
| **Save changes** | `PUT api/orientation-sessions/{id}` | `HR.Orientation.Write` |
| **Change status** | `POST …/{id}/status` | `HR.Orientation.Write` |
| 🚫 **Delete** | `DELETE …/{id}` | **`HR.Orientation.Admin`** |
| Facilitators | `GET` / `POST …/{id}/facilitators` · `PUT …/facilitators/{id}` | Read / Write |
| 🚫 **Remove facilitator** | `DELETE …/facilitators/{id}` | **`HR.Orientation.Admin`** |
| The register | `GET` / `POST …/{id}/attendance` | Read / Write |
| Vendor and trainer lists | `GET api/training-vendors/active` · `GET api/trainers/vendor/{id}` | `HR.Training.Read` — the HR role holds it; a role without it is told so in the dialog |

Tables: `OrientationSessions`, `OrientationSessionFacilitators`, `OrientationAttendanceRecords`.

**What the server does on a status change:**
- *In progress* stamps the actual start, and *Completed* the actual end, if they are empty.
- A **Draft going live** tells its employee facilitators.
- A **live session cancelled or postponed** tells everybody on it.
- Saving a **live** session with a new time or place tells everybody on it, with *Was:* and *Now:*.
- No status change touches an enrolment.

**Delete** is refused while anybody holds a seat — and a *Completed* enrolment still holds one — so a
session that ran with people on it can never be deleted, whatever its status.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-10 · Every delete in the module is Admin-only and offered to the HR desk anyway.** Here: the trash button and *Remove facilitator*. Elsewhere: removing a programme, module, content item, question, prerequisite or rule (chapter 14), a category (chapter 15), a template or a template task (chapter 16). The shared table already has the switch to hide a remove that would 403; none of these screens uses it. Same finding as the company schedule's C-1. The one delete that is not Admin-only — a template's audience — is the one with no confirmation | |
| **O-11 · A session's *Requires approval* does nothing.** *"Enrollments start as pending confirmation."* — no server code reads the flag except to map and copy it, and new enrolments are always created *Confirmed*, or *Waitlisted* when the session is full | |
| **O-12 · The Overview form goes stale after a status change, and its next save can erase what the change stamped.** The form reads its values once, when it is first drawn. Moving to *In progress* or *Completed* stamps the actual start or end on the server and refreshes the page — but not the mounted form. The next **Save changes** sends the actuals blank, and the update writes them as sent. Switching tabs re-reads it. From the code, not browser-walked | |
| **O-13 · Session status has no rules, and shows raw values.** A *Completed* or *Cancelled* session can be put back to *Draft* or *Enrollment open*; the badge, the confirmation and the toast print *"EnrollmentOpen"*, *"InProgress"* | |
| **O-16 · *Run again* says the facilitators "are asked to confirm the new date"; nothing asks them.** The copy marks them unconfirmed and sends nothing. Employee facilitators hear when the draft goes live; an external one never does | |
| **O-17 · The register's times are hung on today's date**, so a register filled in after the day records wrong-dated check-ins. Out before In is not checked, and changing *Session day* drops unsaved edits without a word | |
| **O-18 · A failed attendance read can overwrite the day with "Present".** If the attendance read fails, every row is drawn as *Present* and **Save day N** stays live. Saving writes those values over whatever was recorded | |
| **O-5** · here: a refused roster reads *"Nobody enrolled yet"* and an *Enrolled* tile of 0; a refused register reads *"Nobody to mark"*; any failure of the page reads *"Session not found"* | |
| **O-14 · The logistics fields do not follow the delivery mode.** *Blended* is treated as in person only, so it gets a venue and no joining link, while *Printed material* gets both. A value typed into a field the form then hides is still sent | |

---

## 7. `/hr/orientation/enrollments` — who is on what

### 📍 Where you are

**Sidebar:** … → Orientation & Onboarding → **Enrollments** · `/hr/orientation/enrollments` · as
**hr.head** · **6 minutes**

### 📖 What it is

> *"The register of who is on which programme, how far through they are and what has fallen due —
> and the one place HR puts people on a programme by hand."*

### 👁 On the page

**Header:** *Orientation Enrollments* — *"Who is on which programme, how far through they are, and
what has fallen due."*, back-link, and **Enrol participants**.

**The filter card.** A **scope** dropdown decides what is loaded, and each scope is its own read:

| Scope | Then | Loads |
|---|---|---|
| **By programme** *(default)* | a **Programme** dropdown — *"Choose a programme…"* — listing **every** programme, drafts and retired included | that programme's enrolments, newest first |
| **By completion status** | a status dropdown, default *In Progress*: *Not Started* · *In Progress* · *Pending Assessment* · *Pending Acknowledgement* · *Completed* · *Failed* · *Overdue* · *Exempted* | every enrolment in that status |
| **Overdue** | — | past the due date and not completed or exempted, **whatever the enrolment status** (**O-2**), soonest first |
| **Due in 14 days** | — | due within a fortnight and not completed or exempted |

A **search box**, *"Search by name, number or programme…"*, filters the loaded rows in the browser.
It matches the participant's name and number and the programme's title.

**Four tiles**, counted over everything the scope loaded, not over what the search leaves:

| Tile | Counts |
|---|---|
| **Enrollments** | every row |
| **Holding a seat** | *Pending confirmation*, *Confirmed*, *Active* and *Completed*. The hint says *"Withdrawn and cancelled are excluded"*; waitlisted people and no-shows are excluded too |
| **Completed** | completion status *Completed*, in green, with *"N% of the list"* |
| **Overdue** | completion status **literally** *Overdue*, red when any (**O-24**) |

**The table.**
- **Participant** — the name, with the number beneath.
- **Programme** — the title, with the code beneath.
- **Session** — a link to it, or *"Self-paced"* when there is none.
- **Enrollment** and **Completion** — both raw values, e.g. *PendingConfirmation*.
- **Progress** — a bar and a percentage. Once a quiz has been sat it adds *"Scored N%"* with
  *Passed* or *Failed*.
- **Due**, then a **⋯** menu.

A row does not click.

**The row menu:**

| Item | Offered when |
|---|---|
| **View progress** | always. ⚠ It opens the participant's own player (**O-4**) |
| **Re-enrol** | on the person's latest row on the programme, when HR ended it: *Withdrawn*, *Cancelled* or *No show* |
| **Open the next cycle now** | on the person's latest row, when it is *Completed* on a **recurring** programme |
| **Issue certificate** / **Reissue certificate** | *Completed*, on a programme that issues certificates. It appears on any row, not only the latest |
| **Withdraw** (red) | any status but *Withdrawn* and *Cancelled*, so also *Completed* and *No show* (**O-25**) |

"Latest" means the newest of the rows **on screen** for that person and programme (**O-21**).

**The "Enrol participants" dialog.** Its description reads: *"Anyone already on the programme and
not finished is skipped, as is anyone who has completed a programme that does not recur. Someone
HR withdrew can be enrolled again, and on a recurring programme a completed person starts their
next cycle. If the chosen session is full and allows a waitlist, they are queued rather than
confirmed."*

| Field | |
|---|---|
| **Programme** | only programmes that take enrolments: *Active* and within their dates. The rest are counted underneath: *"N programmes that are a draft, retired or outside their effective dates are not offered — they take no enrolments."* |
| **Session** *(optional)* | The first option is *"Self-paced — no session"* on a self-paced, video or printed programme, otherwise *"No session (enrol without one)"*. Then come the programme's sessions, open ones first, e.g. *"Corporate Induction Day — … · 4 Oct 2026 (N seats left)"*, or *"(uncapped)"*. A closed session is listed greyed, with the reason, e.g. *"— not open: it is still a draft"* |
| *(the line under it)* | one of: *"This programme is self-paced, so it has no sessions — people work through it on their own."* · *"No sessions are scheduled for this programme yet. Schedule one, or enrol without a session."* The link opens chapter 5's dialog and loses these picks · *"None of its sessions is open for enrolment — the closed ones are listed for reference. …"* · a permission or load failure, each in its own words |
| **How they came to be enrolled** | *HR Assigned* (default) · *Manager Assigned* · *Self Enrollment* · *Automatic Rule* (**O-22**) |
| **Participants (N)** | an employee search, active employees only. Each pick becomes a chip with an ✕ |

The buttons are **Cancel** · **Enrol N**. The result is reported twice:
- a toast, e.g. *"1 of 2 enrolled"* with *"1 were skipped — already on the programme's current
  cycle, or they have completed a programme that does not recur."*;
- when anyone was skipped or waitlisted, an amber **"Not everyone was enrolled as requested"**
  banner above the tiles. It stays until the next enrolment.

The page then switches to that programme.

**Three confirmations from the row menu:**
- **Issue** / **Reissue certificate** — *"Issue Abdul-Rahman Aryee a certificate?"* … *"with the
  next serial and the programme's validity. They are told, in-app and by email."* A reissue names
  the certificate it replaces.
- **Re-enrol** / **Open the next cycle** — a new enrolment, **always without a session**. The earlier
  one *"stays on the record"*.
- **Withdraw from this programme** — *"… keeps their record, but their seat is freed and their
  enrollment closed."* A **Reason** is required. The buttons are **Keep them on** · **Withdraw**.

**Empty:** *"Nothing to show"*, with *"Choose a programme to see who is on it."*, *"Nothing is
overdue."*, *"Nothing falls due in the next fortnight."* or *"No enrollments match."* The same appears
when the search matches nothing, and **when the read is refused** (**O-5**).

### ▶ Walk it

**1 — Open the page.** Nothing loads until a programme is chosen: *"Choose a programme to see who
is on it."*

**2 — Choose *ORI-ONB-001 — New Employee Onboarding*.** On a clean rebuild, by the second day after
the build, there are **37 rows** (8 before the hire rule's first run, § 2.1):
- the six starters;
- Abdul-Rahman Aryee, *Completed*;
- Abena Lartey at 40%;
- **twenty-nine people the new-hire rule enrolled by itself**.

Tiles: **37 · 37 · 1 (3% of the list) · 0**.

> *"Everyone on the new-starter induction. Six of them HR put here by hand. Twenty-nine the
> new-hire rule put here by itself, a day or two after they joined — nobody pressed anything. One has
> finished; the rest are inside their thirty days."*

Point at **Kojo Ansah's** row: 0%, *"Scored 100% — Passed"*.

> *"Kojo has passed the knowledge check before opening a single module. The order is Kojo's to
> choose; what the system insists on is that all of it is done."*

⚠ **CAREFUL — do not say Kojo will be certified on finishing.** Kojo cannot finish. The programme
requires a signed declaration and no screen can create one, so the enrolment will stop at *Pending
acknowledgement* (**O-15**, Rule 3).

**3 — Scope → *Overdue*.** One row: the seeder's overdue person on **Anti-Harassment & Code of
Conduct**, due six days before the build. The *Overdue* tile reads **1**.

> *"One person past the deadline on the annual code-of-conduct course. The reminder sweep has already
> chased them; this is where HR sees who to call."*

⚠ A month after the build, the starters and then the twenty-nine fall due and join this list. The
*Overdue* tile stays at **1**, because it counts a status only the seeder writes (**O-24**).

**4 — 🔴 LIVE WRITE 4 — Enrol participants.**
- **Programme:** **ORI-CMP-001 — Anti-Harassment & Code of Conduct**.
- **Session:** the only option is *"Self-paced — no session"*, and the line reads *"This programme
  is self-paced, so it has no sessions…"*.
- **How they came to be enrolled:** leave **HR Assigned**.
- **Participants:** **Efua Seidu** (TDC/00017, the `staff` persona) and the overdue person from
  step 3.

Press **Enrol 2**. Expect *"1 of 2 enrolled"* and the amber banner: *"1 skipped — already on the
current cycle, or completed a programme that does not recur."* The page lands on the programme with
two rows.

> *"I asked for two and got one — and the screen says so rather than calling it a success. That person is
> already on this year's cycle and has not finished it, so enrolling them again would do nothing
> but reset the clock. Efua is on it, due in fourteen days, and has just been told: it is in Efua's
> notifications now."*

*Undo:* chapter 21.

**5 — Open Efua's row menu, and do not choose anything.** It offers *View progress* and *Withdraw*:
- no certificate item until Efua completes;
- no *Re-enrol* until HR has ended an enrolment.

🚫 **DO NOT PRESS *View progress*** — it opens Efua's own player with Efua's buttons live (**O-4**,
Rule 4). 🚫 Do not press **Reissue certificate** on Abdul-Rahman's row either: it replaces
OCERT-2026-00001 and writes to its holder.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Programme dropdowns, and the menu's recurring and certificate checks | `GET api/orientation-programs/all` | `HR.Orientation.Read` |
| *By programme* | `GET api/employee-orientations/program/{id}` | `HR.Orientation.Read` |
| *By completion status* | `GET …/completion-status/{status}` | `HR.Orientation.Read` |
| *Overdue* · *Due in 14 days* | `GET …/overdue` · `GET …/due-soon?daysAhead=14` | `HR.Orientation.Read` |
| The dialog's sessions | `GET api/orientation-sessions/program/{id}` | `HR.Orientation.Read` |
| **Enrol N** | `POST api/employee-orientations/bulk-enroll` | `HR.Orientation.Write` |
| **Re-enrol** · **Open the next cycle** | `POST api/employee-orientations` | `HR.Orientation.Write` |
| **Issue / Reissue certificate** | `POST …/{id}/certificates` | `HR.Orientation.Write` |
| **Withdraw** | `POST …/{id}/withdraw` | `HR.Orientation.Write` |

Tables: `EmployeeOrientations`, `OrientationCertificates`, `OrientationNotifications`.
- Every write needs the signed-in account linked to an employee.
- Withdraw, a certificate and *View progress* also pass the service's record check. It admits the
  **roles** SuperAdmin, HR and HR User, or the participant (**O-26**).

**Who may be enrolled** is judged on the person's **latest** enrolment on the programme (round 4,
lane I-b):
- none, or one HR ended (*Withdrawn*, *Cancelled*, *No show*) → **enrol**;
- *Completed* → enrol **only if the programme recurs**, which starts the next cycle;
- anything unfinished → **skip** in a bulk run, and refuse a single one: *"This person is already on
  this programme and has not completed it. Withdraw that enrolment first if they are to start
  again."*

The programme must take enrolments — *"Nobody can be enrolled on "…": it has been retired."* — and
so must the session: *"Session OSN-… cannot take enrolments: it is still a draft."*

**What an enrolment gets:**
- *Confirmed* and *Not started*;
- *Waitlisted*, with a position, when the session is full and waitlists;
- refused, for **the whole batch**, when the session is full and does not waitlist: *"The selected
  session is full and does not allow a waitlist."*

The due date is the enrolment date plus the programme's deadline. When the programme's **Send
reminders** is on, the person is told: *"Enrolled by HR."*

**What a withdrawal does:** it sets the enrolment status and stores the reason. Nothing else:
- the completion status stays as it was (**O-2**);
- nobody moves up from the waitlist;
- the person is not told (**O-25**).

**What a certificate does:** it takes the next `OCERT-{year}-NNNNN`, expiring after the programme's
validity. A reissue marks the old one *Reissued*. Since round 4 (lane K-b1) completion issues the
certificate itself, so this item is for completions from before that, and for reissues.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-15 · No enrolment made in the product can reach *Completed* on any of the three seeded programmes.** New Employee Onboarding and Anti-Harassment & Code of Conduct require a signed declaration. A declaration is a row created per enrolment, and only by an HR endpoint that **no screen calls**. The programme form has the switch *Requires an acknowledgement*, and no field for the text. So a participant finishes the content, passes the quiz and stops at *Pending acknowledgement*, with no Declarations tab to sign, while the sweep chases them to sign. The Q3 Product Launch Briefing has no content and no quiz. Only tracking content, submitting a quiz or signing a declaration ever evaluates completion; attendance is not read. **The one completed enrolment on the demo database is the seeder's.** Measured on UAT: 19 declarations in the tenant, the seeder's one and 18 harness fixtures | |
| **O-19 · A bulk enrolment can over-fill a capped session.** Each person's seat check counts the saved seats, and the batch is saved once at the end. So everybody in it sees the count from before the batch: with two seats left, five picks are all *Confirmed*. On a full session that waitlists, everybody in the batch gets the same position. *"(N seats left)"* is true only before you press | |
| **O-20 · Nobody already on a programme can be put on a session.** No screen can do it: the enrolment update that places a person on a session has no caller in the frontend; the dialog skips anyone already on the programme; *Re-enrol* never sends a session; and automatic enrolments are made without one. So the six starters, and everybody the hire rule enrols, can never be booked onto a **Corporate Induction Day**. The one route is to withdraw them and enrol them again with the session, which leaves a withdrawn row behind (**O-2**) | |
| **O-21 · "The latest row" is judged among the rows on screen.** In *By completion status*, *Overdue* and *Due in 14 days* a person's newer enrolment may not be loaded, so an older row is treated as the latest. *Open the next cycle now* is then offered on cycle 1 while cycle 2 is under way, and the server refuses. It is correct only in *By programme* | |
| **O-22 · A hand enrolment can be recorded as *Automatic Rule* or *Self Enrollment*.** The dialog offers both and the server stores what it is sent. The triggers screen then reports an automatic enrolment with no rule. *Re-enrol* and *Open the next cycle* also record no "enrolled by", where the dialog records the officer | |
| **O-23 · The employee record's *Open in Orientation* is ignored.** It links here with `?employeeId=`, which this page never reads, and there is no by-employee scope. It lands on *"Choose a programme to see who is on it."* | |
| **O-24 · The *Overdue* tile, and *By completion status → Overdue*, count a status only the demo seeder writes.** The *Overdue* scope is by date, so in that scope the tile can read 0 beside a list of overdue rows. *Pending Assessment* and *Exempted* are never written either, so those two filters only ever return nothing | |
| **O-25 · *Withdraw* is offered on completed and no-show enrolments, and the server has no status rule.** A withdrawn completion keeps *Completed* as its completion status and then offers *Re-enrol*; the triggers screen reports *"Ended by HR"*. A withdrawal frees a seat but promotes nobody from the waitlist, and the person is not told | |
| **O-26 · The record check is by role; the policies are by permission.** TenantAdmin, Admin, or any role given `HR.Orientation.Write` passes every policy on this screen. On **Withdraw** and **Issue certificate** it then gets 403 *"You do not have access to this orientation enrollment."*, and *View progress* shows it *"Orientation not found"*. Enrolling works, because it has no record check. The same check guards every participant action in the module | |
| **O-27 · The dialogs' own buttons do not reset them, and some labels mislead.** *Cancel* and *Keep them on* only close, so reopening shows the last programme, people or reason. Every sessionless enrolment reads *"Self-paced"*, including an in-person one. *"Holding a seat"* leaves out waitlisted people and no-shows without saying so. The badges print raw values, and the re-enrol confirmation reads *"The noshow enrolment stays on the record"* | |
| **O-5** · here: a refused or failed read shows *"Nothing to show"*, after three silent retries. The app's default retry does not recognise this client's 403 and 404 errors, so a refusal first shows *"Loading enrollments…"* for several seconds | |

---

## 8. `/hr/orientation/triggers` — why a rule did, or did not, fire

### 📍 Where you are

**Sidebar:** … → Orientation & Onboarding → **Enrollment Triggers** · `/hr/orientation/triggers` ·
as **hr.head** · **5 minutes**

### 📖 What it is

> *"The answer to 'do the triggers actually fire?' — for any one person: which of our rules reach
> them, whether each has fired or when it will, and which onboarding checklist they would get."*

### 👁 On the page

**Header:** *Enrollment triggers* — *"Which orientation rules reach a person, whether they have fired
or when they will — and which onboarding template their plan comes from."*, and a back-link. Holders
of `HR.Orientation.Admin` also see **Run tonight's sweep now**; the HR role does not.

**The Employee card** has an employee search (active employees only) and this hint: *"Hire, transfer
and promotion rules fire on the event and for 30 days after their date (plus any delay); scheduled
rules every night; manual and publish rules only when HR enrols the audience. Nobody is enrolled
twice — not even after a withdrawal."* ⚠ Two of its sentences are wrong (**O-28**).

Before a pick, the page reads *"Choose an employee — Their programmes, rules and onboarding template
will be explained here."*

**The summary card:**
- the name and number;
- a line of facts: *Unit* · *Level* · *Position* · *Location* · *Employment* (raw, e.g. *Permanent*)
  · *Hire date*. With no hire date it reads *"Hire date: none — no hire rule can fire until an
  employment date is set"*;
- **Populations:** badges *New hires* (employed 90 days or less), *Management* and *Contractors*, or
  *"none"*;
- a red *"Inactive — no rule reaches them"* for an inactive person;
- **Movements:** up to ten implemented promotions, transfers, lateral moves and secondments.

**"Orientation programmes."** There is one card for each programme that has an audience rule or that
the person is on, in code order. Each card shows:
- the title, a link to the programme's setup page;
- *"code · status"*;
- a **verdict pill** and the server's sentence;
- whatever the verdict, *"Mandatory prerequisites not yet completed: …"* when one is missing.

The verdicts:

| Pill | Means |
|---|---|
| **Enrolled** (green) | on the programme. The sentence says how: *"Enrolled automatically by "…""*, *"On the next cycle…"*, or *"Already on the programme (HrAssigned, Confirmed)"* |
| **Enrols tonight** (blue) | a rule is due or scheduled, and reaches them |
| **Next cycle opens tonight** · **Next cycle scheduled** | a recurring programme's renewal, due now or on a stated day |
| **Waiting for its date** (blue) | a dated rule reaches them and fires from a day still ahead |
| **Waiting on a prerequisite** (amber) | a rule would fire, but a mandatory prerequisite is not done |
| **Only when HR enrols** (amber) | only a *Manual* or *publish* rule reaches them |
| **Excluded** · **Ended by HR** · **Window lapsed** · **No triggering event** · **Not in the audience** · **Programme not active** (grey) | nothing will happen, and the sentence says why |

Beneath each card is a **rules table**:
- **Rule**, with *Enrols* or *Excludes*, and *Inactive*;
- **Targets** — *"Everyone"*, *"Organisation unit: … and the units beneath it"*, *"Position: …"*
  and so on, with *"New hires only"* and the like;
- **In target** and **In population**, each ✓ or ✗;
- **Trigger**, with *"+N day(s)"*;
- **Why** — one sentence per rule. For example, *"Its window closed on 3 Sep 2026 — dated rules
  fire for 30 days after their day, so they never reach back into history."* or *"Fires only when
  HR presses "Enrol audience now" on the programme."*

Empty: *"No programme has a rule — Add audience rules on a programme for anyone to be enrolled
automatically."*

**"Onboarding plan."**
- The card first says *"Has a plan from "…"."* with **Open it**, or *"No onboarding plan. A confirmed
  hire gets one automatically."*
- Then it says *"For their current placement the template would be **…** — "*, with the reason:
  *"Most specific match — …"*, *"The default template — no other template's audience matched."*, or
  *"There is no active onboarding plan template."* A tie adds *"⚠ "…" matched equally; chosen by
  name. Narrow one of them."*
- A **candidates** table follows: **Template** (*Default* marked) · **Matched on** · **Score** ·
  **Excluded**.

The score measures how specific the match is:

| Matched on | Score |
|---|---|
| a position | 100 |
| the person's own unit | 90, one less for each level above it |
| an organisation level | 40 |
| a location | 30 |
| everyone | 10 |
| the default template | 0 |

**The sweep preview** is for administrators. **Run tonight's sweep now** evaluates everything
**without writing**, and opens *"The sweep would enrol N"*: *"N rule(s) across N programme(s). … N
already enrolled, N excluded, N waiting on a prerequisite. Nothing has been written yet."*
- **Run it** then runs the real sweep, a fresh evaluation, and toasts *"Sweep ran: N enrolled"*.
- With nothing to enrol, the button reads **Close**, beside **Cancel**.

### ▶ Walk it

**1 — Pick Kojo Ansah.** Populations: **New hires**. A clean rebuild shows two cards; a UAT full of
harness programmes lists dozens.
- **Anti-Harassment & Code of Conduct** — **Only when HR enrols**: *""All employees annually"
  reaches them, but it fires only at publication or when HR presses "Enrol audience now"."*
- **New Employee Onboarding** — **Enrolled**: *"Already on the programme (HrAssigned, Confirmed). No
  rule enrols anyone twice."* Its rule, *All new hires on joining*, ticks both columns and reads **On
  Hire +1 day**. It says its window **closed**, about a fortnight before the build.

> *"This is the question the last demonstration asked: do the triggers fire? For Kojo, the new-hire
> rule reaches — Kojo is in the audience, and a new hire — and its window closed before we switched
> it on: Kojo joined seven weeks before the build, and a hire rule looks back thirty days. That is
> deliberate. Otherwise importing the whole workforce would enrol everybody on the new-starter
> induction. So HR enrolled Kojo by hand, and the screen says that too."*

**2 — Read Kojo's *Onboarding plan* card.** It says *"Has a plan from "TDC New Starter Checklist"."*.
Underneath: *"For their current placement the template would be **TDC Field Staff Starter** — Most
specific match — Organisation unit: Operations Directorate and the units beneath it."* Candidates:
**TDC Field Staff Starter · 88** and **TDC New Starter Checklist** *(Default)* **· 0**.

> *"This plan was made by hand, from the standard checklist. Hire Kojo today and the system would
> choose the field-staff checklist, because Kojo sits two levels under the Operations Directorate —
> and it shows its working: eighty-eight against zero."*

**3 — Pick Nana Legal (LA-LEG-001).** **New Employee Onboarding** — **Enrolled**: *"Enrolled
automatically by "All new hires on joining"; status Confirmed. No rule enrols anyone twice."*

> *"And this one the rule did by itself. Employed on the day we built this database; a day or two later
> the sweep enrolled them. Nobody pressed anything, and nobody will be enrolled twice."*

**4 — Pick Efua Seidu** *(after LIVE WRITE 4)*.
- **Anti-Harassment & Code of Conduct** — **Enrolled**: *"Already on the programme (HrAssigned,
  Confirmed). No rule enrols anyone twice. The programme recurs annually: the next cycle opens one
  period after this one is completed."*
- **New Employee Onboarding** — **Not in the audience**. The rule's *Why* begins *"Reaches their
  placement, but they are not in the new hires population."*

> *"The compliance course is set to renew itself: a year after a cycle is completed, the next one
> opens. The new-starter induction does not reach Efua at all, because Efua is not a new starter."*

🚫 **Run tonight's sweep now** is not on the screen for `hr.head`; it needs `HR.Orientation.Admin`.
As an administrator, the preview is safe because it writes nothing. **Run it** is not safe: it
enrols.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The diagnosis | `GET api/orientation-programs/triggers/diagnose/{employeeId}` | `HR.Orientation.Read` |
| **Run tonight's sweep now** — the preview | `POST api/orientation-programs/triggers/run?preview=true` | **`HR.Orientation.Admin`** |
| **Run it** | `POST …/triggers/run?preview=false` | **`HR.Orientation.Admin`** |

The diagnosis runs **the same rules the sweep runs**, for one person, and writes nothing. It reads:
- `OrientationPrograms`, `OrientationAudienceRules` and `OrientationPrerequisites`;
- `EmployeeOrientations`;
- the staff movements;
- the onboarding templates and their audiences.

**What the nightly sweep does**, for the organisation's live programmes (Active and within their
dates):
1. **Scheduled** rules, for everyone they reach;
2. **hire** rules, for people employed within the rule's delay plus 30 days;
3. **transfer** and **promotion** rules, for movements implemented in that window;
4. **publish** rules of a programme that came into effect in the last 30 days;
5. **renewals** of recurring programmes.

Automatic enrolments are:
- *Confirmed*;
- made with **no session** (**O-20**);
- sourced *AutoRule* or *Recurrence*;
- stamped with the rule that made them.

**The preview's counts:**
- *already enrolled* — anybody the rules reach who has **any** earlier enrolment on the programme,
  a withdrawal included;
- *excluded*;
- *waiting on a prerequisite*;
- *renewed*;
- for renewals, *no longer in its audience*.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-28 · The hint under the picker is wrong twice.** Publish rules also fire at publication, and on the sweep when a programme comes into effect; the page's own rule sentences say so. And *"Nobody is enrolled twice — not even after a withdrawal"* contradicts HR's *Re-enrol* and the renewals. The server's wording is narrower and right: *"No rule enrols anyone twice."* | |
| **O-29 · The onboarding card over-promises.** It reads the latest plan in **any** status, so a cancelled plan reads *"Has a plan"*, while the hire path treats a cancelled plan as none. *"A confirmed hire gets one automatically"* is true only of a hire confirmed through recruitment. Creating or importing an employee makes no plan: the twenty-nine rule-enrolled people have none | |
| **O-30 · The page's smaller slips.** `?employeeId=` preselects the diagnosis but leaves the picker empty. Inactive people cannot be picked, so the *Inactive* verdicts are reachable only by URL. The preview's first sentence counts a programme once per step of the sweep, so a recurring programme with one scheduled rule counts twice | |

---

## 9. `/hr/orientation/onboarding` — the plans

### 📍 Where you are

**Sidebar:** … → Orientation & Onboarding → **Onboarding Plans** · `/hr/orientation/onboarding` ·
as **hr.head** · **3 minutes**

### 📖 What it is

> *"Every new hire's first-weeks checklist, one row each — how many of their tasks are done, how
> many are late, and whether the plan has started."*

### 👁 On the page

**Header:** *Onboarding Plans* — *"A new hire's checklist — tasks, owners, due dates and the assets
they need on day one."*, back-link, and **New plan**.

**Four tiles**, counted over the open tab: **Plans** · **Tasks** · **Completed** (*"N% of tasks on
these plans"*) · **Overdue tasks** (red).

**Four tabs**, each its own read: **In progress** *(default)* · **Not started** · **Overdue** ·
**Completed**. There is no *Cancelled* tab and no "all plans" view. A search box, *"Search by name
or employee number…"*, filters the open tab.

**The table:**
- **New hire** — the name links to the plan, with the number beneath;
- **Starts** and **Target**;
- **Tasks** — a bar and, e.g., *"3/11"*;
- **Overdue** — a red count;
- **Status** — raw, e.g. *InProgress*.

Newest start comes first. There is no row menu.

**Empty:** *"No plans here — No onboarding plans are in progress."* (or the tab's own name), or
*"Try a different search."* A refused read looks the same (**O-5**).

**The "New onboarding plan" dialog.** Its description reads: *"Choosing a template copies its tasks
onto the plan, each due at the start date plus its own offset. The copy is taken now, so later edits
to the template leave this plan alone."*

| Field | |
|---|---|
| **New hire** * | an employee search; *"Choose the new hire"* if left empty |
| **Template** | *"No template — start empty"*, or an active template, e.g. *"TDC New Starter Checklist (default) — 9 tasks"*. Nothing is preselected, and nothing suggests the template the hire's placement would get |
| **Start date** * | today by default |
| **Target completion** | |
| **Buddy** · **Coordinator** | employee searches |
| **Notes** | up to 2,000 characters |

The buttons are **Cancel** · **Create plan**. On success the toast reads *"Plan created — The
template's tasks have been copied onto it, each due from the start date."*, or *"It has no tasks yet
— add them on the plan."*, and you land on the plan.

### ▶ Walk it

**1 — Open the register.** *In progress* holds one plan: **Kojo Ansah**. It starts about seven weeks
before the build and reads **3/11**, with **8** overdue. Tiles: **1 · 11 · 3 (27%) · 8**.

> *"Kojo's first month. Three of eleven things done, eight late — and late is honest: the plan
> started on the day Kojo joined, seven weeks before we built this, and nobody has worked it since."*

**2 — *Not started*.** The other five starters, each **0/11** with **11** overdue.

> *"Five plans nobody has started. That is what the reminder sweep chases once a day — the Head
> of HR's notifications hold a digest of forty-one of these right now."*

⚠ **CAREFUL — skip the *Overdue* tab.** It is empty, because it filters on a plan status nothing
sets. A plan with late tasks stays in *In progress* or *Not started*, and the lateness is counted per
task, in the column (**O-31**).

**3 — Open *New plan*, read it, and press *Cancel*.**

> *"A plan by hand: the new hire, a template, a start date. The tasks are copied now and dated from
> the start, so changing the template tomorrow does not rewrite anybody's plan in flight. And since
> round 4 nobody has to open this for a hire that recruitment confirms: the plan is made for them,
> from the template their placement calls for."*

🚫 **DO NOT PRESS *Create plan*.** A plan cannot be edited, cancelled or deleted once it is made
(Rule 5, **O-32**). Nothing stops a second plan for somebody who already has one (**O-33**).

⚠ *Cancel* does not clear the dialog. Reopening shows empty pickers that still hold the last picks
(**O-33**).

**4 — Click *Kojo Ansah*** and continue into chapter 10.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Each tab | `GET api/onboarding-plans/status/{status}` | `HR.Orientation.Read` |
| Template options | `GET api/onboarding-plan-templates/all` — active templates only | `HR.Orientation.Read` |
| **Create plan** | `POST api/onboarding-plans` | `HR.Orientation.Write`, and a linked employee |

Tables: `OnboardingPlans`, `OnboardingTasks`. The notices go to `OrientationNotifications`.

**What creating a plan does:**
- the plan starts *Not started*;
- each of the template's tasks is copied as *Pending*, due at the start date plus its offset, with its
  owning position;
- in the same save, three notices go out: a welcome to the new hire, *"You are coordinating…"* to the
  coordinator, and *"You are … onboarding buddy"* to the buddy.

**The two counts in each row:**
- *Tasks* counts only *Completed*.
- *Overdue* counts every task not *Completed* whose due date has passed, a waived one included.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-31 · Half the statuses have no writer.** Nothing but the demo seeder sets a plan *Overdue* or *Cancelled*, or a task *In progress*, *Blocked*, *Waived* or *Overdue*. So this screen's *Overdue* tab is empty on real data, the board's *In progress* column only ever holds seeded cards, and the portal's *"HR waived it."* is never earned. A plan with late tasks stays in *In progress* or *Not started*: the *Overdue* column counts tasks, while the *Overdue* tab filters plans | |
| **O-32 · A plan cannot be changed after it is made.** No control changes its coordinator, buddy, target date or notes, and none cancels or deletes it. The plan update endpoint has no caller in the frontend, and there is no delete endpoint at all. The code that creates a plan on hire says the opposite: the confirming officer becomes coordinator, and *"HR can hand the plan to someone else from the plan screen"*. The *"You are now coordinating…"* notice has nothing to send it | |
| **O-33 · The New plan dialog checks nothing a person would expect.** Nothing stops a second plan for somebody who already has one, though the hire path refuses that case; the portal then shows only the latest. *Cancel* keeps the picks: reopening shows empty pickers that still hold the last new hire, buddy and coordinator, so *Create plan* makes a plan for somebody the dialog does not show. Options read *"1 tasks"*, and badges print raw values | |
| **O-5** · here a refused read shows *"No plans here"* | |

---

## 10. `/hr/orientation/onboarding/[id]` — one plan

### 📍 Where you are

**From:** a name on the register, a queue's *Open plan*, the triggers screen's *Open it*, or a
coordinator's notice · `/hr/orientation/onboarding/[id]` · as **hr.head** · **6 minutes**

### 📖 What it is

> *"One new hire's first weeks: every task, who owns it and when it is due, what has been signed off,
> and the kit — the card, the laptop, the accounts — from ordered to handed over."*

### 👁 On the page

**Header:**
- the new hire's name;
- *"TDC/00063 · starts … · from "TDC New Starter Checklist""*;
- a back-link and the status badge;
- **Start** (only when *Not started*), or **Mark completed** (when *In progress*).

Nothing edits the plan (**O-32**).

**Four tiles:** **Tasks done** (*"3 of 11"*, *"27% complete"*) · **Overdue** · **Target completion**
· **Assets** (*"N issued"*).

**The summary card:**
- a progress bar;
- *"Buddy: …"* and *"Coordinator: …"*;
- the completion date, once completed;
- the notes;
- on a plan the system made on hire, *"**Created automatically.** Created on hire from "…". Most
  specific match — …"*;
- an amber line counting the mandatory tasks still outstanding.

**Three tabs.**

**Board** *(default)* has four columns:

| Column | Holds |
|---|---|
| **To do** | *Pending*, *Blocked*, *Overdue* |
| **In progress** | *In progress*, which nothing sets (**O-31**) |
| **Awaiting verification** | done, and waiting for a second person's sign-off |
| **Done** | *Completed*, *Waived* |

Each card shows:
- the task, with badges for its status, *Mandatory* and its category;
- *"Due …"*, with *" · overdue"* in red;
- who has it: *"Assigned to Kojo Ansah"*, *"Assigned to Human Resource"* (a unit), *"Owned by Head of
  MIS"* (a position), or *"Unassigned"*;
- *"Completed by … — needs a second signature"* while waiting, and *"Verified by … …"* once signed.

Its ⋯ menu offers:
- **Complete** — unless the task is done or waiting. The dialog reads *"This closes the task."*, or,
  for a task that needs verifying, *"…it will move to awaiting verification rather than closing — and
  you cannot verify it yourself."* It takes **Notes** and an **Evidence file path**, a typed reference
  (there is no upload). The button is **Complete**, or **Complete & send for verification**;
- **Verify** — only while the task is waiting. It acts at once, and the server refuses the person who
  completed it: *"A task cannot be verified by the person who completed it."*;
- **Comments** — the thread, with author and time, and **Add a comment**.

**All tasks (N)** is a table: **#** · **Task** (with its category, *"· mandatory"* and *"· needs
verifying"*) · **Due** · **Assigned** · **Status** · **Signed off** (the verifier, or *Awaiting*).
It has **Add task** and, per row, **Edit**; nothing removes a task. The task dialog is described as
*"An extra step for this hire, beyond whatever the template supplied."*:

| Field | |
|---|---|
| **Task** * · **Description** | |
| **Category** * | ⚠ ignored on edit (**O-35**) |
| **Due date** * | today by default |
| **Assigned to** | an employee search |
| **Display order** * | 0–9999 |
| **Requires verification** | *"A second person has to sign it off; whoever completes it cannot."* |
| **Mandatory** | *"Fixed once the task exists."* — on add only |

**Assets (N)** is a table: **Asset** (type · tag · serial) · **Needed by** · **Issued** ·
**Acknowledged** · **Status**. It has **Add asset** and **Edit**; nothing removes one. The dialog is
described as *"Kit, cards and accounts this hire needs. Status is tracked from ordered through to
acknowledged."* Its fields:
- **Type**, **Name**, **Description** (add only), **Asset tag**, **Serial number**, **Needed by** and
  **Notes**;
- on edit, **Status** (*Pending* · *Ordered* · *Ready* · *Issued* · *Acknowledged* · *Not required*)
  and **Acknowledged by the employee**.

A completed plan is read-only on every tab.

### ▶ Walk it

**1 — You are on Kojo Ansah's plan.** It reads **In progress**, **3 of 11**, with **8** overdue. The
buddy is a colleague from Kojo's unit; the coordinator is **Akpene Amoah**.

> *"Kojo's plan. It came from our standard checklist, so every task is the checklist's, dated from
> the first day. The Head of HR coordinates it, and a colleague from the same unit is the buddy."*

**2 — Read the Board.** *To do* holds eight. *Done* holds three: the personal file, the ID card and
the site safety induction.

> *"Each task belongs to somebody. The ID card is the Administrative Officer's; the email account and
> the laptop are the Head of MIS's. Two are queued to a section: the biometric clock sits with Human
> Resource until somebody there picks it up. Meeting the directorate is the one optional task."*

**3 — ⋯ → *Comments* on *Create the corporate email account*.** Two comments: *"Raised with MIS on
the day of resumption. …"* and *"Account created; the distribution list follows once the
establishment record is updated."*

> *"The conversation about a task stays on the task, so whoever picks it up next reads it there."*

**4 — 🔴 LIVE WRITE 5 — Complete *Create the corporate email account*.** Notes: *"Mailbox handed
over in person; distribution list added."* The dialog reads *"This closes the task."* The card moves
to **Done**: **4 of 11**.

**5 — 🔴 LIVE WRITE 6 — *All tasks* → *Add task*:**
- **Task:** *Return the signed code of conduct*;
- **Category:** *Policy Acknowledgement*;
- **Due date:** today;
- **Assigned to:** **Kojo Ansah**;
- **Display order:** 30;
- **Requires verification:** on;
- **Mandatory:** on.

The toast reads *"Saved — Task added."* Kojo is told at once: *"Your onboarding task: Return the
signed code of conduct"* appears in Kojo's notifications.

> *"Something the template did not have, for this one hire. It is Kojo's to do, and it needs a second
> signature — so when Kojo says it is done, it waits for the Head of HR. Kojo does that half from the
> portal in a few minutes."*

**6 — Open *Assets*.** Four items:
- the **staff identity card** and the **HP ProBook 450 G10**, *Issued* and acknowledged;
- the **email account**, *Ready*;
- the **desk keys**, *Pending*.

> *"The kit, from ordered to handed over. The card and the laptop have been handed over and signed
> for; the keys are still to come."*

⚠ **CAREFUL — do not edit an asset.** Saving the dialog blanks its issue, provisioning and
acknowledgement dates, and ignores a changed type or name (**O-34**). The card's *Issued* date would
vanish in front of you.

🚫 **DO NOT PRESS *Mark completed*.** It closes the plan with eight tasks still open, because nothing
checks, and a completed plan can never be reopened (**O-36**).

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The plan | `GET api/onboarding-plans/{id}/details` | `HR.Orientation.Read` |
| **Start** · **Mark completed** | `POST …/{id}/start` · `POST …/{id}/complete` | `HR.Orientation.Write` |
| Tasks (board and table) | `GET …/{planId}/tasks` | `HR.Orientation.Read` |
| **Add task** · **Edit** | `POST …/{planId}/tasks` · `PUT …/tasks/{taskId}` | `HR.Orientation.Write` |
| **Complete** · **Verify** | `POST …/tasks/{taskId}/complete` · `POST …/tasks/{taskId}/verify` | `HR.Orientation.Write` |
| Comments | `GET` / `POST …/tasks/{taskId}/comments` | Read / Write |
| Assets | `GET …/{planId}/assets` · `POST …/{planId}/assets` · `PUT …/assets/{id}` | Read / Write / Write |

Tables: `OnboardingPlans`, `OnboardingTasks`, `OnboardingTaskComments`, `OnboardingAssets`. The
completer, the verifier and the commenter are the signed-in officer.

**How a task moves:**

| From | Act | Who | Result |
|---|---|---|---|
| anything open | **Complete** (board) | `HR.Orientation.Write` | *Completed*, or *Awaiting verification* when it needs it. Nobody is told |
| anything open that is assigned to you | **Mark done** (the portal, chapter 19) | the assignee | the same, and the coordinator is told it is ready to sign |
| awaiting verification | **Verify** (board or queue) | `HR.Orientation.Write`, not the completer | *Completed*, with who and when |
| — | start, block, waive, send back, reopen | **nobody** | no endpoint exists |

**Adding a task with an assignee tells them** — *"Your onboarding task: …"*, or *"Onboarding task
for …: …"* for a colleague — and reassigning a task tells the new assignee.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-34 · Editing an asset erases its history.** Every save sends the provisioned, issued and acknowledgement dates blank, and the server writes them as sent. Nothing else in the product sets them, so *Issued* can only show a date the demo scenario wrote through the API. *Type* and *Name* are editable in the dialog and never sent. The description takes 1,000 characters in the form and 500 on the server | |
| **O-35 · Editing a task ignores some of what it shows.** *Category* is editable and never sent. *Assigned to* opens empty, although the task has an assignee. Turning *Requires verification* off on a task already awaiting sign-off strands it: *Verify* then refuses with *"This task does not require verification."*, and nothing else can close it | |
| **O-36 · Closing and opening a plan are unguarded, and say more than they do.** *Mark completed* checks nothing, neither status nor mandatory tasks, and the plan can never be reopened. *"…the plan can no longer be edited"* is enforced only by the screen: the server still accepts task, asset and comment writes on a completed plan. *Start* says *"its tasks become live work"*, but it changes only the plan's status; tasks on a plan not yet started are already reminded, and already markable done. *Mandatory* is a warning, nothing more | |
| **O-37 · What is written on a task is half hidden.** The Complete dialog's notes and evidence path are shown nowhere afterwards. Comments cannot be read on a completed plan, because the menu that opens them is hidden with the rest. HR's *Complete* on a task that needs verifying tells nobody, where the portal's *Mark done* tells the coordinator. The empty board says *"Add them below."*, but adding is on the *All tasks* tab | |
| **O-5** · here a refused plan reads *"Plan not found"*, and a failed task read *"No tasks on this plan"* | |

---

## 11. `/hr/orientation/onboarding/queues` — what is outstanding everywhere

### 📍 Where you are

**Sidebar:** … → Orientation & Onboarding → **Task Queues** · `/hr/orientation/onboarding/queues` ·
as **hr.head** · **3 minutes**

### 📖 What it is

> *"The same tasks, across every plan at once: everything late, everything waiting for a second
> signature, anybody's tasks, and the kit still to be provisioned — because those are what get lost
> when you only look one hire at a time."*

### 👁 On the page

**Header:** *Onboarding Task Queues* — *"What is outstanding across every open plan — overdue work,
sign-offs waiting on someone, and kit still to be provisioned."*, and a back-link.

**Four tiles**, always counted from the overdue and verification lists, whatever tab is open:
**Overdue tasks** · **Awaiting verification** (*"Done, but needing a second signature"*) · **Mandatory
& overdue** · **Unassigned & overdue** (*"Nobody owns these"*).

**Four tabs:**

| Tab | Holds |
|---|---|
| **Overdue** *(default)* | every task in the organisation not *Completed* and past its due date, on **any** plan, whatever its status (**O-38**) |
| **Verification** | every task awaiting sign-off |
| **By assignee** | *"Whose tasks?"*, an employee search: every task given to that person, done ones included |
| **Assets** | a status choice, default *Pending*: every asset in that status |

A **Search…** box matches the task name or the assignee, or the asset name on *Assets*. The new
hire's name is not searchable, and no row says whose onboarding it belongs to (**O-39**).

**Task rows:**
- **Task**, with *Mandatory* and *"Completed by … …"*;
- **Category**;
- **Due**, red when late;
- **Assigned to** — a person, a unit or a position, or *Unassigned*;
- **Status**;
- then **Verify** (only while the task awaits sign-off, with no confirmation) and **Open plan**.

**Asset rows:** **Asset** (with its notes) · **Tag / serial** · **Needed by** · **Status** · **Plan**.

**Empty:**
- task tabs: *"Nothing outstanding"* with *"No onboarding task is past its due date."*, *"Nothing is
  waiting on a sign-off."* or *"This person has no onboarding tasks assigned."*;
- *By assignee* before a pick: *"Choose someone"*;
- *Assets*: *"Nothing here — No assets are pending."*

⚠ A refused read gives the same all-clear (**O-5**).

### ▶ Walk it

**1 — Open the queues.** On a clean rebuild the *Overdue* tab holds the six starters' open tasks,
about **60**. *Awaiting verification* reads **0**.

> *"Everything late across every new hire, in one list, oldest first — and a tile for the ones
> nobody owns, because those are the ones that never happen by themselves."*

⚠ On UAT this list holds **6,727** tasks, **6,664** of them on 811 cancelled harness plans (Rule 1,
**O-38**).

**2 — *By assignee* → Kojo Ansah.** One task: *Return the signed code of conduct*, from LIVE
WRITE 6.

> *"Anybody's work, across every plan they are on. Kojo has one thing to do — and does it from the
> portal, not from here."*

**3 — *Assets* → *Pending*.** One item: Kojo's **desk pedestal and drawer keys**.

> *"And the kit nobody has provisioned yet. A laptop nobody ordered is invisible until the first
> day; here it is not."*

**4 — *Verification*.** It reads *"Nothing is waiting on a sign-off."* It fills in chapter 19, when
Kojo marks the task done, and you come back here to sign it off.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| *Overdue* | `GET api/onboarding-plans/tasks/overdue` | `HR.Orientation.Read` |
| *Verification* | `GET …/tasks/status/PendingVerification` | `HR.Orientation.Read` |
| *By assignee* | `GET …/tasks/assignee/{employeeId}` | `HR.Orientation.Read` |
| *Assets* | `GET …/assets/status/{status}` | `HR.Orientation.Read` |
| **Verify** | `POST …/tasks/{taskId}/verify` | `HR.Orientation.Write`, and not the completer |

The reminder sweep's unassigned items, and every sign-off chase, link here.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-38 · The Overdue queue ignores the plan's status.** It lists every task not *Completed* that is past its due date. So tasks left open on a plan HR marked completed stay overdue for ever, on a plan that is now read-only where they cannot be finished. *Waived* tasks and tasks awaiting verification count too. The reminder sweep and the portal use the opposite rules: open plans only, with waived and waiting tasks treated as closed. Measured on UAT: 6,664 of the queue's 6,727 tasks sit on 811 cancelled harness plans | |
| **O-39 · A queue row does not say whose onboarding it is.** No column names the new hire, and search cannot find them: the reads load the name and the rows drop it. *Unassigned & overdue* counts a task owned by a position as nobody's, while the row shows the position | |
| **O-40 · The sign-off chain assumes the coordinator is in HR.** Sign-off chases go to the plan's coordinator and link to this screen, and only a holder of `HR.Orientation.Write` can verify; the portal has no Verify. A coordinator from outside HR is asked to sign and cannot. Nothing sends a task back: a sign-off can only be given | |
| **O-5** · here the false all-clear is *"No onboarding task is past its due date."* | |

---

## 12. `/administration/hr/orientation` — the setup hub

### 📍 Where you are

**From:** the back-arrow on any setup page — *Programmes*, *Categories*, *Onboarding Templates*,
*Reminders & notices* · `/administration/hr/orientation` · as **hr.head** · **1 minute**

### 📖 What it is

> *"What the module is built from, decided once: the catalogue of programmes, how it is grouped, the
> checklists new hires are given, and the reminders."*

### 👁 On the page

**Header:** *Orientation & Onboarding Setup* — *"The induction catalogue and the reusable onboarding
checklists. Day-to-day delivery — sessions, enrollments and plans — lives under HR."* There is no
back-link and no button.

**Four cards:**

| Card | Goes to |
|---|---|
| **Programmes** — *"The induction catalogue — modules, content, prerequisites, audience rules and assessments."* | chapter 13 |
| **Categories** — *"Groups the catalogue — onboarding, compliance, health & safety."* | chapter 15 |
| **Onboarding Templates** — *"Reusable task checklists, copied onto each new hire's plan when it is created."* | chapter 16 |
| **Reminders & notices** — *"The daily sweep that tells people what is due, and every notice sent — enrolments, session changes, completions, certificates — with what its email did."* | chapter 17 |

### ▶ Walk it

**1 — Read the four cards.**

> *"Four things are decided here, not day to day: the programmes, how they are grouped, the
> checklists, and the reminders that chase both."*

**2 — Click *Programmes*.**

### ⚙ Behind the page

No API call. Every `/administration/hr/*` page needs the `admin.hr` gate, which the HR role holds.
Without it the browser is sent to `/dashboard`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-41 · *Reminders & notices* is on no menu.** For this group, the HR Setup hub and the Administration menu list *Programmes*, *Categories* and *Onboarding Templates* and stop there. The delivery landing (chapter 3) has no card for it either. This card is the only way in | |

---

## 13. `/administration/hr/orientation/programs` — the catalogue

### 📍 Where you are

**From:** the landing's *Programmes* card, or the setup hub ·
`/administration/hr/orientation/programs` and `…/programs/new` · as **hr.head** · **4 minutes**

### 📖 What it is

> *"The catalogue: every programme we run, what it asks of people, and how many are on it and
> through it."*

### 👁 On the page

**Header:** *Orientation Programmes* — *"The induction catalogue — each programme carries its
modules, content, prerequisites and assessment."*, back-link, and **New programme**.

**Filters** work in the browser over one read:
- a search box, *"Search by title, code or category…"*;
- a **Status** choice: *All statuses*, then *Draft*, *Pending Approval*, *Active*, *Suspended*,
  *Retired*, *Archived*;
- a **Type** choice: *All types*, then *Onboarding*, *Policy Awareness*, *Product Launch*,
  *Compliance*, *Health & Safety*, *Systems & Tools*, *Culture & Values*, *General*.

**The table**, ordered by title:
- **Code** and **Title**, both links to the programme. Under the title: *"Assessed"*,
  *"Certificated"* and *"N min"*;
- **Category** · **Type** · **Priority** (a badge; *Mandatory* and *Critical* in red);
- **Modules**;
- **Enrolled** — every enrolment ever made, withdrawn ones included (**O-42**);
- **Completed** — *"N (N%)"*;
- **Status** — raw, e.g. *PendingApproval*;
- a **Copy** button.

The rows look clickable, but only the code and the title are.

**Empty:** *"No programmes yet — Create an induction programme to start enrolling employees."* or *"No
matching programmes — Try a different search or clear the filters."* A refused read shows the first
(**O-5**).

**New programme** (`…/programs/new`) opens *New Orientation Programme*: *"Add an induction programme
to the catalogue. It starts as a draft — its modules, content and assessment are added next, and it
is published from there."*
- It is the programme form of chapter 14's *Overview*, with **Cancel** · **Create programme**.
- The server numbers it `ORI-{year}-NNNN`, never reusing a code, and always makes it a Draft: *"Programme
  created — ORI-2026-NNNN — add its modules and content next."*

**The Copy dialog** is titled *"Copy ORI-…"*. Its two paragraphs read:
- *"The copy gets the modules and their content, the quiz, the prerequisites and the rules for who
  takes it. Sessions and enrolments stay with the original."*
- *"It starts as a draft, so its rules enrol nobody yet. Once you publish it they enrol people just
  as the original's do — if the copy replaces the original, retire the original at the same time, or
  both will enrol the same people."*

Its fields are **Title of the copy** (*"… (copy)"*) and **Programme code** (*"Leave blank to number it
automatically"*), with **Cancel** · **Copy programme**. A refusal, such as a code already in use,
stays inside the dialog. On success the toast reads *"Programme copied — ORI-… is a draft. Publish it
when it is ready."*, and you land on the copy.

### ▶ Walk it

**1 — Open the catalogue.** A clean rebuild has three rows, all **Active** (UAT has 505 — Rule 1):

| Programme | Shape | Enrolled · Completed |
|---|---|---|
| **ORI-CMP-001 Anti-Harassment & Code of Conduct** | Assessed · Certificated · Compliance · *Mandatory* · 1 module | 2 · 0 (after LIVE WRITE 4) |
| **ORI-ONB-001 New Employee Onboarding** | Assessed · Certificated · Onboarding · *High* · 4 modules | 37 · 1 (3%) |
| **ORI-PRD-001 Q3 Product Launch Briefing** | Product Launch · *Medium* · 0 modules | 1 · 0 |

> *"The whole catalogue, and the three kinds of thing it holds: an induction everyone new goes
> through, a compliance course everyone renews every year, and a one-off briefing that is only its
> live session."*

**2 — 🔴 LIVE WRITE 7 — *Copy* on *Q3 Product Launch Briefing*.** Title of the copy: *Q4 Product
Launch Briefing*; leave the code blank. Press **Copy programme**. The toast reads *"Programme copied —
ORI-2026-NNNN is a draft. Publish it when it is ready."*, and you land on the copy, where chapter 14
continues.

> *"Next quarter's briefing is this quarter's with new slides. A copy takes everything but the people
> and the sessions, and it starts as a draft, so nobody is enrolled on it by accident."*

*Undo:* chapter 21. HR cannot delete it (Rule 2).

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The list | `GET api/orientation-programs/all` | `HR.Orientation.Read` |
| **Create programme** | `POST api/orientation-programs` | `HR.Orientation.Write`, and a linked employee |
| **Copy programme** | `POST api/orientation-programs/{id}/clone` | `HR.Orientation.Write`, and a linked employee |
| Category options (New) | `GET api/orientation-categories/lookup` — active categories only | `HR.Orientation.Read` |

**What a copy carries:**
- the modules and their content — retired ones stay retired, and the links are shared;
- the questions and their options;
- the prerequisites and the audience rules.

It is a Draft, whatever the original's status.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-42 · The list's numbers, and the copy's title.** *Enrolled* counts every enrolment ever made, withdrawn, cancelled and no-show ones included, so it does not mean "on it now". The Copy dialog accepts a 300-character title and the programme form refuses more than 200, so a copy with a long title cannot then be saved from its own Overview | |
| **O-5** · here a refused list reads *"No programmes yet"* | |

---

## 14. `/administration/hr/orientation/programs/[id]` — one programme

### 📍 Where you are

**From:** a code or title in the catalogue, or a card on the triggers screen ·
`/administration/hr/orientation/programs/[id]` · as **hr.head** · **10 minutes**

### 📖 What it is

> *"One programme, in full: what it asks of people, the content they work through, what has to come
> first, who is enrolled automatically and when, and the question paper."*

### 👁 On the page

**Header:**
- the title;
- *"ORI-… · category · vversion"* — seeded programmes read *"vv1.0"* (**O-50**);
- a back-link and the status badge;
- **Change status…** and **Copy**;
- a trash button, **Delete programme** — 🚫 Admin-only (Rule 2, **O-10**).

**Change status…** offers every status but the current one, with **no rules between them**
(**O-46**). The confirmation, *"Change status to PendingApproval?"* (the raw value), says:
- to *Active*: *"The programme becomes available for enrollment and its audience rules start
  firing."*;
- to *Retired* or *Archived*: *"Existing enrollments are kept, but nobody new can be enrolled."*;
- otherwise: *"Enrollment on this programme is paused until it is made active again."*

Making a programme Active, from any other status, runs its *On programme publish* rules.

**Four tiles:** **Modules** · **Sessions** · **Enrolled** · **Completed** (*"N% of those enrolled"*).
Once the programme is Active, an amber **"This programme is live but incomplete"** can list two
problems:
- *"It has no modules, so there is nothing for a participant to work through."*
- *"It requires an assessment but has no questions, so nobody enrolled on it can complete it."*

It appears only after publishing, never before (**O-46**).

**Five tabs.**

**Overview** is the programme form, with **Cancel** · **Save changes**:

| Card | Fields |
|---|---|
| **Programme** — *"Code … — issued on creation and fixed."* | **Title** * · **Description** · **Objectives** · **Category** (*"Uncategorised"*, or an active category) · **Type** * · **Default delivery mode** * · **Priority** * · **Audience scope** * — ⚠ a label only, which decides nothing (**O-50**) · **Estimated duration (minutes)** · **Owner** — *"Who is accountable for this programme?"*, shown empty even when set (**O-43**) |
| **Completion requirements** — *"What a participant has to do before the programme counts as finished."* | **Requires an assessment**, then **Passing score (%)** · **Requires an acknowledgement** — *"A declaration the participant signs — recorded with their IP and a tamper hash."* ⚠ There is no field for the declaration's text, and nothing creates one (**O-15**) · **Completion deadline (days from enrollment)** |
| **Certificate** | **Issues a certificate** — *"Issued automatically, with a serial, when an enrolment completes…"*, then **Validity (months)** — *"Leave empty for a certificate that does not expire"*, which cannot be left empty (**O-44**) |
| **Recurrence & lifecycle** | **Recurs** — *"Everyone who completes it is enrolled again one period after completing…"* — then **Frequency** (*Monthly* · *Quarterly* · *Semi-annually* · *Annually* · *Every two years*) · **Send reminders** · **Effective from** / **Effective to** — *"Outside these dates nothing enrols anyone — not the audience rules, not a renewal, not "Enrol audience now". …"* · **Version** · **Tags** |

**Modules & content (N)** starts with the modules table: **#** · **Module** (with *Optional*) · **Type**
· **Duration** · **Content** — *"N items"*, which opens that module's content beneath · **Status**.
The module dialog has:
- **Title** * and **Description**;
- **Type** *: *Information Content*, *Video Lesson*, *Interactive*, *Assessment*, *Acknowledgement*,
  *Survey*, *Live Session*;
- **Sequence** * and **Estimated duration**;
- the switches **Must be done in order**, **Optional** — *"Not counted towards completion."* (⚠ it is
  counted, **O-58**) — and **Active**.

The content table, *"Content in "…""*, has **#** · **Title** · **Type** · **Resource** (a link) ·
**Required** · **Status**. The item dialog has:
- **Title** * and **Description**;
- **Content type** *: *Video*, *Audio*, *PDF*, *Document*, *Presentation*, *External Link*, *Embedded
  Web Page*, *Image*, *Text*, *Quiz*;
- **Sequence** *;
- **Resource URL** — a link. **There is no upload**;
- **Display file name** and **Media length (seconds)**;
- **Required** — *"Counts towards completing the programme."* — and **Active**.

**Prerequisites (N)** has **Programme** · **Mandatory** (*Mandatory* / *Advisory*) · **Notes**.
- **Add prerequisite** takes another programme, **Mandatory** — *"An advisory prerequisite is recorded
  but does not block enrollment."* — and **Notes**.
- There is no Edit: *"a prerequisite is added or removed, not amended"*. Remove is Admin-only, so HR
  can add a prerequisite and never change it (**O-47**).

**Audience rules (N)** starts with a card:
- *"Rules fire by themselves — Hire, transfer and promotion rules fire on the event; scheduled rules
  every night. "Enrol audience now" runs the manual, publish and scheduled rules immediately."*;
- the recurrence line, when the programme recurs;
- the link *"Why did — or didn't — a rule reach someone?"* (chapter 8);
- **Enrol audience now**, disabled unless Active: *"Only an active programme enrols anyone."*

Then come the rules: **Rule** · **Targets** (with *"New hires only"* and the like) · **Reach today**
(red at 0) · **Trigger** · **Delay** (*"Immediately"*, *"1 day after"*) · **Effect** (*Enrols* /
*Excludes*) · **Status**.

The rule dialog:

| Field | |
|---|---|
| **Rule name** * · **Description** | |
| **Targets** | *Everyone* · *Organisation unit* (*"and everything beneath it"*) · *Organisation level* · *Position* · *Location* · *One person* — each a typed picker, not an id box |
| **Who, of the people there** * | *Anyone there* · *New hires* (*"Employed within the last 90 days."*) · *Management* · *Contractors*. The default is *New hires* |
| *(reach)* | *"Reaches **N** people today — …"*, updated live as you choose |
| **Trigger** * | each with what it does. *On Hire*: *"When an employee is created — the form, the import or a confirmed hire. …"*. *On Program Publish*: *"Once, when the programme is made Active. …"* (**O-46**). *Scheduled*: *"Every night: anyone the rule reaches who is not yet on the programme."*. *Manual*: *"Only when HR presses "Enrol audience now" on this programme."* |
| **Enrollment delay (days)** * | hire, transfer and promotion only — *"…A rule fires for 30 days after its day comes, and never reaches further back."* (**O-48**) |
| **Enrols** · **Active** | switching *Enrols* off makes the rule an exclusion: *"it keeps people out whatever the trigger"* |

**Questions (N)** shows an alert when the programme does not require an assessment. Then the table:
**#** · **Question** (*"N options · N correct"*) · **Type** (*Single Choice*, *Multi Select*, *True /
False*, *Free Text*) · **Points** · **Status**. The question dialog has:
- **Question**, **Type** * and **Points** *;
- the **Options**, each marked **Correct** or **Wrong** — only one correct, unless multi-select. The
  hint reads *"Saving replaces the whole option set — an option removed here is deleted."*;
- **Explanation** — *"Shown to the participant once their attempt has been graded."*;
- **Sequence** * and **Active** (**O-45**).

Every table's **Remove** — module, content item, prerequisite, rule, question — is 🚫 Admin-only
(Rule 2).

### ▶ Walk it

**1 — You are on the copy LIVE WRITE 7 made.** It reads *Draft*, with **Modules 0**. *Enrol audience
now* is greyed: *"Only an active programme enrols anyone."*

> *"A draft. Nothing in it reaches anybody until it is published — and publishing is a separate act
> from editing, on purpose."*

**2 — 🔴 LIVE WRITE 8 — *Modules & content* → *Add module*.** Title *Q4 range walkthrough* · Type
*Information Content* · Sequence 1. Then click *"0 items"* on its row.

**3 — 🔴 LIVE WRITE 9 — *Add content item*.** Title *Q4 range deck* · Content type *Presentation* ·
Sequence 1 · Resource URL: any `https://` link · **Required** on.

> *"Content is linked, not uploaded: the deck lives on the intranet or a drive, and the programme
> points at it. The participant opens it from their own page and marks it done."*

**4 — Open the catalogue and choose *New Employee Onboarding*.** Tiles: **4 modules**, **3
sessions**, **37 enrolled**, **1 completed**. Walk the tabs:
- **Modules & content** — *Welcome & Company Overview* (the CEO's welcome, and the company's history
  and values), *Policies & Compliance* (the handbook and the code of conduct), *Tools & Systems* and
  *Knowledge Check*: six items.
- **Questions** — three: where the leave policy lives, the core values (*Multi Select*), and *"New
  employees must complete onboarding within 30 days."* (*True / False*).
- **Audience rules** — *All new hires on joining*: *Everyone*, *New hires only*, **Reach today**
  about 30, *On Hire*, *1 day after*, *Enrols*.
- **Overview → Completion requirements** — the assessment at 70%, the acknowledgement and 30 days;
  a certificate valid for 24 months.

> *"This is what a new joiner is held to: four modules, a knowledge check at seventy per cent, a
> signed declaration, inside thirty days — and a certificate that lasts two years. And who is
> enrolled on it is not a list somebody keeps: it is one rule — everybody, as long as they are new —
> firing within a day or two of their joining."*

⚠ **CAREFUL — do not claim the declaration is authored here.** *Requires an acknowledgement* is a
switch with no text behind it, and nothing creates the declaration a participant would sign (**O-15**,
Rule 3).

⚠ **CAREFUL — do not press *Save changes* on a seeded programme.** It clears the programme's owning
unit without saying so (**O-43**).

**5 — Open *Anti-Harassment & Code of Conduct* → *Audience rules* → *Enrol audience now*.** Nothing is
written; a preview opens: *"Enrol N people?"* — every active employee not already on it, well over a
hundred on a clean rebuild. It continues *"1 rule(s). … already on the programme, 0 excluded, 0
waiting on a prerequisite. First: …"*.

> *"The annual refresher reaches everybody. Before anything is written it says how many and who
> first — and nobody already on this year's cycle is counted twice. This is the button HR presses on
> the first of January."*

🚫 **Press *Cancel*, not *Enrol them*.** It enrols the whole workforce, emails nobody (Rule 7), and
cannot be undone from a screen.

**6 — 🚫 DO NOT PRESS the trash button or any *Remove*.** They are Admin-only (Rule 2).

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The programme | `GET api/orientation-programs/{id}` | `HR.Orientation.Read` |
| **Save changes** | `PUT …/{id}` | `HR.Orientation.Write` |
| **Change status** | `POST …/{id}/status` | `HR.Orientation.Write` |
| 🚫 **Delete** | `DELETE …/{id}` | **`HR.Orientation.Admin`** |
| Modules · content items · questions | `GET` / `POST` / `PUT` under `…/{id}/modules`, `…/modules/{id}/content-items`, `…/{id}/questions` | Read / Write / Write |
| Prerequisites · audience rules | `GET` / `POST` under `…/{id}/prerequisites`, `…/{id}/audience-rules`; `PUT …/audience-rules/{id}` | Read / Write / Write |
| 🚫 Every **Remove** | `DELETE` on each | **`HR.Orientation.Admin`** |
| The reach line | `POST …/audience-rules/reach` | `HR.Orientation.Read` |
| **Enrol audience now** | `POST …/{id}/enrol-audience?preview=true`, then `…=false` | `HR.Orientation.Write` |

Tables: `OrientationPrograms`, `OrientationModules`, `OrientationContentItems`,
`OrientationAssessmentQuestions`, `OrientationAssessmentOptions`, `OrientationPrerequisites`,
`OrientationAudienceRules`.

**Enrol audience now** runs every active rule with no date to count from: *Manual*, *On programme
publish* and *Scheduled*. It keeps the triggers' own rules (§ 1.5):
- anybody with an earlier enrolment is left alone, a withdrawal included;
- exclusions apply;
- a mandatory prerequisite holds a person back.

It refuses a programme that is not live — *"Only an active programme enrols anyone. Publish it first
— publishing runs its publish rules by itself."* — and one outside its dates.

**Delete**, for an administrator, refuses an Active programme: *"An active program cannot be deleted.
Suspend or retire it first."* It checks nothing else — not enrolments, sessions or certificates.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-43 · Saving the Overview wipes the owning unit, and never shows the owner.** The form always sends the owning organisation unit blank and the server writes it through, so the first save of a seeded programme clears it. The owner employee is never loaded into the field, so a programme with an owner shows an empty search box — and the owner cannot be cleared | |
| **O-44 · *Validity (months)* cannot be left empty**, whatever its placeholder says. An empty number box is sent as 0 and refused with *"Too small: expected number to be >=1"*, so a certificate that does not expire cannot be made. Switch the certificate on and off again and the bad value stays in the hidden field, and *Save* then does nothing at all. Blank duration, deadline and media length are saved as 0. It is the same trap as **O-9** | |
| **O-45 · The question dialog blocks *Save* without a word.** The option-set messages, *"Give this question at least two options."* and *"Mark at least one option as correct…"*, are raised where the dialog never reads them, and *"Option text is required"* has no place to show at all. A **Free Text** question cannot be added or edited, because its two hidden, blank option rows fail. From the code and the installed libraries, not browser-walked | |
| **O-46 · Publishing is unguarded, and *Pending approval* has no approver.** Any status can follow any status. Nothing warns while a programme with no modules, or an assessment with no questions, is being published; the alert appears only once it is live. *Pending approval* is a label, with no workflow, no approver and no check. *On Program Publish* says *"Once"*, but it re-runs on every return to Active. The dialog and toast print raw values, e.g. *"Now PendingApproval."* | |
| **O-47 · Prerequisites are added once and never changed.** There is no update, and Remove is Admin-only, so HR cannot turn a mandatory prerequisite into an advisory one, or take one away. *Mandatory* holds back only automatic enrolments; HR's own enrolments never check it. Yet the screen says advisory ones *"do not block enrollment"*, as if mandatory ones did. A circle (A needs B, B needs A) is accepted, and would hold every automatic enrolment on both for ever | |
| **O-48 · A hidden delay can block saving a rule.** Type a delay under *On Hire*, then change the trigger to *Scheduled* or *Manual*. The delay field disappears but keeps its value, fails its own rule where it cannot be seen, and *Save* does nothing | |
| **O-49 · Client and server limits disagree across the module.** Pairs of form limit and server limit: the programme description 4,000 and 2,000 characters; the version 50 and 20; prerequisite notes 1,000 and 500; an asset description 1,000 and 500; a template task's instructions URL 1,000 and 500. Between the two limits the server answers *"One or more validation errors occurred."*, naming no field | |
| **O-50 · Small things that mislead.** *Audience scope* is required and decides nothing; the rules decide. The header prints *"vv1.0"*. The content panel's title and its *"in order"* line do not follow an edit to its module. A programme whose category was made inactive shows a blank *Category* | |
| **O-10** · here: **Delete programme** and every **Remove** (module, content item, prerequisite, rule, question) answer 403 to the HR role | |
| **O-5** · here a refused programme reads *"Programme not found"* | |

---

## 15. `/administration/hr/orientation/categories` — how the catalogue is grouped

### 📍 Where you are

**From:** the setup hub's *Categories* card · `/administration/hr/orientation/categories` · as
**hr.head** · **1 minute**

### 📖 What it is

> *"The shelves the catalogue sits on — onboarding, compliance, product — so a list of fifty
> programmes can be read."*

### 👁 On the page

**Header:** *Orientation Categories* — *"Groups the induction catalogue — onboarding, compliance,
health & safety, and so on."*, and a back-link.

**The table**, with **Add category**:
- **Name** · **Parent**;
- **Programmes** — every programme in it, retired ones included;
- **Sub-categories** · **Order** · **Status**;
- per row, **Edit** and 🚫 **Remove** (Admin-only).

**The dialog** — *"Leave the parent empty for a top-level category."*:
- **Name** — required, though not marked;
- **Description**;
- **Parent category** — *"None (top level)"* or an active category (⚠ **O-51**);
- **Display order**;
- **Active** — *"Inactive categories stay on existing programmes but are not offered for new ones."*

**Empty:** *"No categories yet"*, twice.

### ▶ Walk it

**1 — Open it.** A clean rebuild has three categories — *Onboarding*, *Compliance & Regulatory* and
*Product & Launches* — with one programme each.

> *"Three shelves for three programmes. They nest — Compliance, then Fire Safety under it — when the
> catalogue grows."*

⚠ **CAREFUL — do not edit a category's parent.** The parent list hides the wrong entry (**O-51**).

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The list · the parent options | `GET api/orientation-categories` · `GET …/lookup` (active only) | `HR.Orientation.Read` |
| **Add** · **Edit** | `POST` · `PUT …/{id}` | `HR.Orientation.Write`, and a linked employee |
| 🚫 **Remove** | `DELETE …/{id}` | **`HR.Orientation.Admin`** |

Table: `OrientationCategories`. For an administrator, Remove refuses a category that still holds
programmes (retired ones included) or sub-categories.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-51 · The parent picker hides the wrong category.** When editing, it removes the category's **current parent** from the list, not the category itself. So the parent looks blank; a newly chosen parent vanishes from the list; the category is offered as its own parent (and refused, 422); and its own descendants are offered. The server checks only direct self-parenting, so a circle can be saved | |
| **O-10** · here **Remove** answers 403 to the HR role | |

---

## 16. `/administration/hr/orientation/onboarding-templates` — the checklists, and who they are for

### 📍 Where you are

**From:** the landing's or the setup hub's *Onboarding Templates* card ·
`/administration/hr/orientation/onboarding-templates` and `…/onboarding-templates/[id]` · as
**hr.head** · **5 minutes**

### 📖 What it is

> *"The standard checklists a new hire is given, and who each one is for — so that the right
> checklist is chosen for a hire without anybody choosing it."*

### 👁 On the page

**The list.** *Onboarding Templates* — *"Standard task checklists. Creating a plan from a template
copies its tasks, so editing a template later does not rewrite plans already in flight."*, with a
back-link.

A table with **Add template**:
- **Template** — a link, with the description beneath;
- **Tasks** — a count;
- **Default** — *Default* or *"—"*;
- **Status** — which only ever reads *Active* (**O-52**).

The row menu offers **Edit**, **Copy** and 🚫 **Remove** (Admin-only).

The template dialog — *"Add the tasks themselves from the template's own page."*:
- **Name** * and **Description**;
- **Default template** — *"Used when a plan is created without one being chosen. Marking this
  clears the flag on whichever template holds it."* (**O-53**);
- **Active** — *"An inactive template is not offered for new plans, but plans already created from
  it are untouched."* (**O-52**).

**Copy** — *"The copy gets every task, with the same timing and owners."* / *"It is not the default,
and it is not given the original's audience — so no new hire receives it until you choose it by
hand or give it an audience of its own."*
- It asks for **Name of the copy**.
- A name already taken is refused inside the dialog: *"A template named "…" already exists. Choose
  another name for the copy."*
- On success: *"Template copied — N task(s) copied into "…"."*, and you land on the copy.

**One template.**

The header:
- the name and description;
- a *Default* badge on the default template;
- *Active* or *Inactive*;
- **Copy**.

Four tiles: **Tasks** · **Mandatory** · **Earliest task** · **Latest task**. The two timing tiles
read *"On the start date"*, *"N day(s) before starting"* or *"N day(s) after starting"* (**O-54**).

**"Who this template is for"** — *"When a hire's start is confirmed, their onboarding plan is created
from the most specific template that reaches them: a position beats a unit, the nearest unit beats
one further up, then level, location and everyone."*
- It adds either *"This is the default template — it is used when no other template reaches the
  hire."* or *"With no audience this template is only ever chosen by hand."*
- Each audience row shows its target, e.g. *"Organisation unit: Operations Directorate and the units
  beneath it"*, with *Applies* or *Never for*, and a trash button that removes it at once, with no
  confirmation. Removing an audience needs only Write.
- The add box has **Targets** (*Everyone*, *Organisation unit*, *Organisation level*, *Position*,
  *Location* — not one person, because *"this audience is decided before the person is an
  employee"*), a switch **Applies to them** / **Never for them (an exclusion)**, and **Add audience**.

**The task list**, with **Add task**:
- **#** · **Task** · **Category**;
- **Due** — the offset in words;
- **Owner** — a position, or *Unassigned*;
- **Mandatory** / *Optional*;
- per row, **Edit** and 🚫 **Remove** (Admin-only).

The task dialog — *"Due dates are offsets from the plan's start date, not fixed dates."*:

| Field | |
|---|---|
| **Task** * · **Description** · **Category** * | |
| **Due (days from start)** * | −90 to 365, *"Negative for before the start date"* — contracts and accounts are meant to be ready before the first day |
| **Owned by position** | a position, or *Unassigned*. The task on each plan is owned by whoever holds it |
| **Instructions URL** | ⚠ copied onto no plan (**O-54**) |
| **Display order** * | |
| **Mandatory** | *"An optional task does not hold up the plan."* (**O-53**) |

There is no *Requires verification* here. A task copied from a template never needs a sign-off,
unless somebody adds one on the plan (**O-54**).

### ▶ Walk it

**1 — Open the list.** A clean rebuild has two templates: **TDC New Starter Checklist**, with 9 tasks
and the *Default* badge, and **TDC Field Staff Starter**, with 5 tasks. UAT has 122, eleven of them
copies of the checklist (Rule 1).

**2 — Open *TDC Field Staff Starter*.** Five tasks, all mandatory: the site safety induction on day
1, then the field kit, a visit to Community 25, the personal file and the bank details. It is for
*"Organisation unit: Operations Directorate and the units beneath it"* — **Applies**.

> *"The field-staff checklist starts with the site and the kit, not the registry. It is for the
> Operations Directorate and everything under it: a hire confirmed into any unit there gets this one,
> and everybody else falls back to the default. That is the choice the triggers screen showed for
> Kojo."*

⚠ **CAREFUL — do not read the *Earliest task* tile aloud.** It says *"On the start date"*, because it
counts from zero; the first task is on day 1 (**O-54**).

**3 — Open *TDC New Starter Checklist*.** *Default*, nine tasks, with the line *"This is the default
template — it is used when no other template reaches the hire."* Each task is owned by a post: the
Administrative Officer, the Head of MIS, the Human Resource Officer, the Head of HR & Administration.

> *"Tasks are owned by posts, not people. When the Head of MIS changes, the checklist does not."*

🚫 **DO NOT PRESS *Remove*** on a template or a task. Both are Admin-only (Rule 2).

⚠ **CAREFUL — do not touch an audience row's trash button.** It is the one delete in the module
that HR can make, and it acts at once, with no confirmation. Removing *Operations Directorate*
from the Field Staff Starter changes which checklist every future hire there is given.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The list | `GET api/onboarding-plan-templates/all` — **active** templates only | `HR.Orientation.Read` |
| **Add** · **Edit** · **Copy** | `POST api/onboarding-plan-templates` · `PUT …/{id}` · `POST …/{id}/clone` | `HR.Orientation.Write` |
| 🚫 **Remove** a template | `DELETE …/{id}` | **`HR.Orientation.Admin`** |
| One template | `GET …/{id}/with-tasks` · `GET …/{id}/task-templates` | `HR.Orientation.Read` |
| Tasks | `POST …/{id}/task-templates` · `PUT …/task-templates/{id}` | `HR.Orientation.Write` |
| 🚫 **Remove** a task | `DELETE …/task-templates/{id}` | **`HR.Orientation.Admin`** |
| Audiences | `GET` / `POST …/{id}/audiences` · `DELETE …/{id}/audiences/{audienceId}` | Read / Write / **Write** |
| Position options | `GET api/EmployeePositions/active` | signing in |

Tables: `OnboardingPlanTemplates`, `OnboardingTaskTemplates`, `OnboardingPlanTemplateAudiences`.

**How a template is chosen on hire** (round 4, lane I). When recruitment confirms a hire's start,
the active template with the best-scoring inclusive audience wins:

| Match | Score |
|---|---|
| a position | 100 |
| the hire's own unit | 90, one less for each level up, never below 51 |
| an organisation level | 40 |
| a location | 30 |
| everyone | 10 |
| the default, when nothing else matches | 0 |

- Any matching exclusion removes a template.
- A tie goes to name order, and the reason says so.
- No plan is made for somebody who already has one that is not cancelled.
- The confirming officer becomes coordinator.

Only recruitment's confirm-start makes a plan (**O-29**).

**Removing the default**, for an administrator, is refused: *"The default onboarding template cannot
be deleted. Assign a new default first."*

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-52 · A deactivated template disappears for good.** The list shows active templates only, and the template page cannot change *Active*. So switching it off from the list is final from any screen, and the *Status* column can only ever read *Active*. A deactivated **default** also stops the on-hire fallback without a word | |
| **O-53 · Two switch descriptions promise the wrong thing.** *Default template* says it is *"Used when a plan is created without one being chosen"*; a plan made by hand without a template gets no tasks at all, and the default serves only the on-hire choice. *"An optional task does not hold up the plan"* implies a mandatory one does; nothing holds up *Mark completed* (**O-36**) | |
| **O-54 · Part of a template stops at the template.** The *Instructions URL* is never copied onto a plan's task, so no board, table or portal row can show it. A template task cannot require verification, so every copied task starts without a sign-off. *Earliest task* and *Latest task* are counted from zero, so a template whose tasks all fall after the start reads *"On the start date"* for its earliest | |
| **O-10** · here: **Remove** on a template and on a task answer 403 to the HR role | |
| **O-5** · here a failed audience read claims *"With no audience this template is only ever chosen by hand."* | |

---

## 17. `/administration/hr/orientation/reminders` — what the system told people

### 📍 Where you are

**From:** the setup hub's *Reminders & notices* card — the only way in (**O-41**) ·
`/administration/hr/orientation/reminders` · as **hr.head** · **5 minutes**

### 📖 What it is

> *"What the system has told people, and what it will tell them: the daily chase of everything due,
> and every notice — enrolled, a session moved, completed — with what became of its email."*

### 👁 On the page

**Header:** *Orientation & onboarding reminders and notices* — *"The daily reminder sweep, and every
notice the system sends — enrolments, session changes, completions, certificates — with what each
email did."*, and a back-link. There are two tabs.

**Reminder sweep** — *"The daily sweep: onboarding tasks due soon, overdue or awaiting sign-off;
orientations due, overdue or waiting on the participant; certificates expiring. Each person gets one
notification listing theirs, and the same by email. A programme whose "Send reminders" is off is
left out."*

- **Run now** — no confirmation. The toast reads *"Sweep complete"*, with *"N item(s) sent to N
  person(s)."* or *"Nothing new to send — everything due has already been reminded."*
- **"Reminders are going out in-app only"** appears when the last run had no mail server: *"No mail
  server is configured, so the last sweep could not email anyone. People still see their reminders
  under My Notifications. Set up email under email settings and the next sweep emails as well."*
  (**O-57**).
- **Last run from this screen** — after *Run now*, the counts: items, people notified in-app,
  emailed, not emailed, with nobody to send to, and a line per kind.
- **Preview** — an **As at** date and **Preview**: *"What a sweep would remind on that date (today if
  blank). Sends nothing and claims nothing, so it never takes a reminder away from the real sweep."*
  It shows **What** · **Reference** · **Due** · **Goes to** (red *"Nobody — no coordinator"* when
  unrouted), for the first 200 rows (**O-56**).
- **Recent sweeps** — **Started** · **Trigger** (*Manual* or *Scheduled*) · **Items** · **People
  notified** · **Emailed** · **Not emailed** (*"(no mail server)"*) · **Nobody to send to**.
- **Sent in the last 14 days** — **Sent** · **What** · **Reference** · **Due** · **Tier** · **To** ·
  **Email**.
- A footnote: *"Escalation: an overdue item is reminded when it falls due (tier 1), again after a
  week (tier 2) and after a fortnight (tier 3). Items more than 90 days overdue are treated as history
  and not reminded. The windows — how far ahead to remind, and how long to wait before chasing — are
  set under HR policy settings."*

**The eight kinds of reminder:**

| Kind | Goes to | When |
|---|---|---|
| **Onboarding task due soon** · **overdue** | the task's assignee, else the plan's coordinator | open tasks on open plans |
| **Onboarding task awaiting sign-off** | the coordinator | done and unsigned for the chase window |
| **Orientation due soon** · **overdue** | the participant | a live enrolment on a programme with *Send reminders* on |
| **Assessment not attempted** | the participant | ⚠ never fires (**O-55**) |
| **Acknowledgement not signed** | the participant | waiting to sign, for the chase window |
| **Certificate expiring** | the participant | an Active certificate inside the warning window |

**Notices** lists every notice the system sent. Its filters:
- **Sent in the last** — 7, 14 (default), 30 or 90 days;
- **What** — *Everything*, or one of thirteen kinds: *Enrolled*, *Session scheduled*, *Session
  moved*, *Session postponed*, *Session cancelled*, *Completed*, *Certificate issued*, *Onboarding —
  welcome*, *Onboarding — coordinator*, *Onboarding — buddy*, *Onboarding — task given*, *Onboarding
  — awaiting sign-off*, *Reminder (daily sweep)*;
- **Email** — *Any*, one outcome, or *In-app only*.

**Send queued emails now** sits beside the filters. Then summary badges, and the table: **Sent** ·
**What** · **To** (*read* / *unread*) · **Notice** · **Email** (*Queued*, *Emailed*, *Email failed*,
*Email timed out*, *No email address*, *No mail server*, *Too old to email*, or *In-app only*).

### ▶ Walk it

**1 — Open *Reminder sweep*.** The amber *"Reminders are going out in-app only"* is there (Rule 7).
*Recent sweeps* has a **Scheduled** row for each day the API has run, starting 19 minutes after it
started. On a clean rebuild the first row holds about forty items for two people: the Head of HR's
digest of the starters' tasks, and the overdue compliance participant's. It reads *Emailed* 0, *Not
emailed* 2, *"(no mail server)"*. Later rows hold only what fell due since.

> *"Once a day, one message per person listing everything of theirs that is due or late — not
> forty emails, one. The Head of HR got the new starters' overdue tasks; the person late on the code
> of conduct got theirs. And it is honest about delivery: this database has no mail server, so it
> says so, and people read their reminders in the portal."*

**2 — Preview, As at 25 days after the build.** The starters' and the rule-enrolled people's
orientations appear as *Orientation due soon*, each routed to its participant: inside the
seven-day window, and not yet overdue (Rule 8).

> *"And what it will say, to whom, on any date we choose — without sending anything."*

**3 — 🔴 LIVE WRITE 10 — *Run now*.** Expect a small count, such as Kojo's new task, due today, from
LIVE WRITE 6, or *"Nothing new to send — everything due has already been reminded."*

> *"Safe to press twice: everything is reminded once per date it falls due, and once per step it
> escalates."*

**4 — Open *Notices*.** The last 14 days list:
- the **Enrolled** notice to Efua Seidu (LIVE WRITE 4);
- **Onboarding — task given** to Kojo Ansah (LIVE WRITE 6);
- the session and facilitator notices of chapter 6.

Every email reads *No mail server*.

> *"Everything the system told anybody lately: who, what, whether they have read it, and what its
> email did. HR's answer to 'did they get it?'."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| *Recent sweeps* · *Sent in the last 14 days* | `GET api/orientation-reminders/runs?count=20` · `GET …/log?days=14` | `HR.Orientation.Read` |
| **Preview** | `GET …/preview[?asOf=]` | `HR.Orientation.Read` |
| **Run now** | `POST …/run` | `HR.Orientation.Write` — deliberately not Admin |
| The notices | `GET api/orientation-notifications/recent?days=&kind=&emailStatus=` | `HR.Orientation.Read` |
| **Send queued emails now** | `POST …/send-queued` | `HR.Orientation.Write` |

Tables: `OnboardingOrientationReminderRuns`, `OnboardingOrientationReminderDispatchLogs`,
`OrientationNotifications`.
- The daily host runs 19 minutes after the API starts and then every 24 hours, for every tenant. The
  email dispatcher runs every minute.
- Each item is sent once per due date and escalation tier; moving a due date re-arms it.
- A digest lists up to 25 lines, then *"…and N more."*
- The windows are four numbers on **HR policy settings**, card *"Orientation & onboarding —
  reminders"*:

| Window | Default | Range |
|---|---|---|
| remind about an onboarding task this many days before it is due | 3 | 0–90 |
| remind about an orientation | 7 | 0–90 |
| warn before a certificate expires | 30 | 0–365 |
| chase something waiting on a person | 3 | 1–90 |

Saving them needs `HR.Company.Admin` (chapter 20).

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-55 · Two reminders cannot behave as labelled.** *Assessment not attempted* needs the *Pending assessment* status, which nothing sets (**O-24**), so it never fires. An enrolment that has **Failed** its quiz is left out of every orientation reminder, due and overdue alike, although the participant still has something to do | |
| **O-56 · The screen claims more than was sent.** *Preview* lists items already reminded, so today's preview can list what *Run now* then calls *"Nothing new to send"*. The *Run now* toast counts items with nobody to send to as *"sent"*. The *Send queued* toast leaves out the ones too old to email, and is titled *"Queued emails sent"* even when nothing was. The notices list stops at 1,000 rows and the log at 2,000, without saying so | |
| **O-57 · The fixes it points to are out of HR's reach.** *"Set up email under email settings"* links to a page whose API only TenantAdmin and SuperAdmin may use. The four reminder windows are on **HR policy settings**, which HR can read but not save: *"Changing these requires an administrator. HR can view them but not change them."* | |
| **O-5** · here a failed read of the sweeps, the log or the notices reads *"No sweeps yet"*, *"Nothing sent"* or *"No notices"* | |

---

## 18. `/me/orientation` — the employee's programmes, and one programme

### 📍 Where you are

**Portal:** Learning → **My Orientations**, or the home tile *"My Orientations — Programmes you are
enrolled on"*, or any orientation notice · `/me/orientation` and `/me/orientation/[id]` · as
**new.hire** · **6 minutes**

### 📖 What it is

> *"The employee's side: what they have been put on, how far through they are, and the programme
> itself — the content, the quiz, the declaration, the feedback and the certificate."*

### 👁 On the page

**The list.** *My Orientation* — *"Programmes you have been enrolled on — work through the content,
sit the assessment, and sign off what needs signing."*, with a back-link to the portal home.
- **Four tiles:** **Assigned** · **Outstanding** · **Overdue** (**O-60**) · **Completed**.
- A red card, *"N of your programmes is past due"*, appears when any is overdue.
- **To do (N)**, then **Completed (N)**. Each programme is a card that links to it, showing:
  - the title and its status (raw);
  - *Waitlisted*, when it is;
  - the code, the session, *"Enrolled …"*, *"Due …"* and *"Completed …"*;
  - a bar, *"N% · scored N%"*.
- **Empty:** *"Nothing assigned to you — You have not been enrolled on any orientation programme. HR
  will assign one when there is something for you to do."*

**One programme.**

The header: the title, *"code · session"*, a back-link, and the status badge.

**Four tiles:**
- **Progress**;
- **Content done** — *"N of M"*, *"Required items only"* (**O-58**);
- **Assessment** — the last score with *Passed* or *Not passed*, *Not attempted*, or *None*;
- **Due**.

Under them, a progress card. It says *"You are on the waitlist at position N…"* when waitlisted, and
*"N declarations still need signing before this can be completed."* when any is unsigned.

**Tabs:**

| Tab | Shown | Holds |
|---|---|---|
| **Content (N)** | always | a card per module (*Optional*, *In order*, *~N min*, the description). Each item has a tick, a lock or a circle, its type, *"· optional"*, *"· N min"*, *"· finish the item above first"* and *"· done …"*. Its buttons are **Open** — the link, in a new tab — or **Start** when there is no link, and **Mark done**. *In order* is enforced by the screen only |
| **Assessment (N)** | when the paper has questions | the whole paper at once: *"Last attempt: 100% — passed — Attempt 1. Submitting again replaces this result."*, each question with its hint (*"Choose one · 1 points"*, *"Choose all that apply"*, *"Free text — recorded but not auto-marked"*), and the explanations once graded. **Submit assessment** confirms *"…Unanswered questions score zero, and submitting replaces any previous attempt."* The pass mark is never shown (**O-59**) |
| **Declarations (N)** | only when a declaration exists (**O-15**) | each with its text, *"Signing this is recorded with your IP address and a tamper hash."*, and **I agree** — *"Your name, the time, and your IP address are recorded against it. This cannot be undone."* — or **Decline**, with a required reason |
| **Feedback** | always | *"How was it?"* — **Overall**, **Content**, **Facilitator** and **Relevance to your job**, each 1 to 5 stars; **Comments**; **Send anonymously**; **Send feedback**. One response per orientation |
| **Certificate** | when one exists | *"Certificate OCERT-… — Issued …, expires …."*, each certificate's status, and **Verify** where it has a link. There is no download (**O-61**) |

### ▶ Walk it

**1 — Sign in as `new.hire` → *My Orientations*.** One card: **New Employee Onboarding** —
*InProgress*, due a month after the build, *"0% · scored 100%"*. Tiles: **1 · 1 · 0 · 0**.

> *"Kojo's own view. One programme, a month to finish it, and the knowledge check already passed."*

**2 — Open it.** Tiles: **Progress 0%** · **Content done 0 of 6** · **Assessment 100%, Passed** ·
**Due …**. Tabs: *Content (6)*, *Assessment (3)*, *Feedback* — and no *Declarations* tab (**O-15**).

**3 — *Content*.** Four module cards, in order. The first is *Welcome & Company Overview*, with the
*CEO Welcome Message* (*Video · 7 min*) and *Company History & Values*.

⚠ **CAREFUL — do not press *Open* on a seeded item.** The demo's content links point at files that
were never provided, so *Open* shows a page that does not exist (**O-62**).

**4 — 🔴 LIVE WRITE 11 — *Mark done* on the *CEO Welcome Message*.** The toast reads *"Marked done —
CEO Welcome Message"*. **Progress 17%**; **Content done 1 of 6**.

> *"The employee works through it in their own time, in any order unless a module says otherwise.
> HR sees the same percentage on the enrolments screen."*

**5 — *Assessment*.** *"Last attempt: 100% — passed"*, with each question's explanation.

> *"Marked the moment it is submitted, and explained afterwards, so that getting a question wrong
> teaches something."*

⚠ **CAREFUL — do not submit it again.** A retake replaces the passing attempt, and a lower score marks
the enrolment *Failed* — even on a completed programme (**O-59**).

**6 — *Feedback*.** Show the form and do not send it: there is one response per orientation, and an
empty one uses it up (**O-63**).

⚠ **CAREFUL — do not mark all six items done in front of people.** The enrolment would stop at
*Pending acknowledgement*, with nothing to sign (**O-15**, Rule 3). Say instead:

> *"The design is that, with the content done and the quiz passed, the participant signs a
> declaration, and signing it completes the programme and issues the certificate. The declaration
> is the part still to be built."*

### ⚙ Behind the page

Everything here goes through `api/employee-orientations`, with **no permission**: the service admits
the participant, or the roles SuperAdmin, HR and HR User (**O-4**, **O-26**).

| Element | Endpoint |
|---|---|
| The list | `GET …/mine` — the employee from the sign-in |
| The programme · its content · the progress rows | `GET …/{id}` · `GET …/{id}/content` · `GET …/{id}/content-progress` |
| **Open** · **Mark done** | `POST …/{id}/content-progress` |
| The paper · **Submit** | `GET …/{id}/assessment` · `POST …/{id}/assessment/submit` |
| Declarations · **I agree** / **Decline** | `GET …/{id}/acknowledgements` · `POST …/acknowledgements/sign` |
| Feedback | `GET …/feedback/mine` · `POST …/{id}/feedback` |
| Certificates | `GET …/{id}/certificates` |

Tables: `EmployeeOrientations`, `OrientationContentProgresses`, `OrientationAssessmentResponses`,
`OrientationAcknowledgements`, `OrientationFeedbacks`, `OrientationCertificates`.

**What is recorded:**
- *Mark done* stamps the item, credits the time since *Open*, and recomputes the percentage over every
  live item.
- *Submit* deletes the last attempt's answers, grades the new ones, and counts the attempt. Every
  question but a free-text one counts, blank or not.
- *I agree* stores the time, the IP address and a SHA-256 hash of the enrolment, the text and the
  time.
- *Feedback* can be sent anonymously, which hides the name from everybody but its author.

**Completion** is the rule in § 1.4. It is judged after each of these acts, and only then.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-58 · Optional content is counted towards completion.** The percentage divides by every live item on every live module, optional ones included, and completion needs 100%. So an optional item left undone holds the programme open. This contradicts HR's own *"Not counted towards completion."* and this page's *"Required items only"*. The count of done items also includes items since retired, and a progress call is not checked against the programme, so 100% can be reached with live items undone. The seeded programmes have no optional content; a programme authored in the demo can | |
| **O-59 · The quiz can be re-sat for ever, and after completion.** There is no attempt limit. A retake deletes the previous answers, including a passing set. On a completed programme, a lower score marks the completion *Failed* while the enrolment stays *Completed* and the certificate *Active*, and the list files it under *To do*. After the first attempt the paper's read returns which options are correct: the page does not show it, but the response carries it. The pass mark is never shown | |
| **O-60 · The portal reads statuses the HR screens do not use.** The *Overdue* tile, banner and red *Due* test a status only the demo seeder sets (**O-24**). Enrolments HR withdrew or cancelled, and no-shows, sit under *To do* with no mark, and stay fully workable: completing one turns its enrolment back to *Completed* | |
| **O-61 · Certificates cannot leave the screen, and never lapse.** There is no download or print. *Verify* appears only on the seeder's certificate, whose link is a placeholder. Nothing ever marks a certificate *Expired*, so a lapsed one still reads *Active* and *"expires"* a past date | |
| **O-62 · Content shows less than it holds.** An item's description is never displayed, so a *Text* item has nothing to read. *In order* is enforced only by the screen. The seeded items link to files that were never provided (`/orientation/onboarding/…`), so *Open* lands on a page that does not exist | |
| **O-63 · The small print.** An empty feedback form is accepted and uses up the one response. *"This cannot be undone"* is kept only by the screen: the API accepts a second signature, or a decline after signing. *"N declarations still need signing"* shows whenever one is unsigned, though one signature completes the gate. *"Every question counts towards the score"* excludes free-text questions. The title *My Orientation* sits under a menu item *My Orientations* | |
| **O-4** · the same player opens for HR, with the participant's buttons live | |
| **O-5** · here a failed list reads *"Nothing assigned to you"*; a failed programme, *"Orientation not found"*; and the *Assessment*, *Declarations* and *Certificate* tabs simply vanish | |

---

## 19. `/me/onboarding` — the employee's onboarding

### 📍 Where you are

**Portal:** Learning → **My Onboarding**, or the home tile *"My Onboarding — Your onboarding, and tasks
given to you"*, or any onboarding notice · `/me/onboarding` · as **new.hire**, then **hr.head** · **4
minutes**

### 📖 What it is

> *"Onboarding from the employee's side: my own plan if I am new, the tasks anybody's plan has given
> me, and the new colleagues I am buddy to."*

### 👁 On the page

**Header:** *My Onboarding* — *"Your own onboarding, the onboarding tasks you have been given, and the
new colleagues you are helping settle in."*, with a back-link to the portal home.

**"Your onboarding"** — only for somebody with a plan:
- *"From … to … · coordinated by … · your buddy is …"*, and the status in words (*In Progress*);
- *"N of M done"*;
- every task, with *Yours to do* on the ones assigned to the reader, *"{category} · due {date} ·
  {who}"*, and its status.

**"Tasks given to you"** — *"N to do"*. Each task shows:
- the task, and whose it is: *"Part of your own onboarding"*, or *"For {new hire}, starting …"*,
  with *"· coordinated by …"*;
- the description;
- *"Due …"*, or in red *"Overdue — was due …"*;
- *"Signed off by …"* when it needs a sign-off;
- *"Your note: …"* (**O-65**);
- the status;
- **Mark done**, or why it cannot be: *"This onboarding is closed."*, *"Done."*, *"HR waived it."*,
  *"Done — waiting for … to sign it off."*

Empty: *"No onboarding tasks have been given to you. Tasks you finish stay here for 30 days."*

**"New colleagues you are buddy to"** appears only when there are some: the new colleague, *"Starting
… · coordinated by …"*, and the plan's status.

**The Mark done dialog:**
- *"… signs it off after you do, and is told it is ready."*, or *"It is recorded as done by you,
  today."*;
- **A note (optional)** — *"What was done — a serial number, who it was handed to…"*;
- **Cancel** · **Mark done**.

### ▶ Walk it

**1 — As `new.hire`, open *My Onboarding*.** *"Your onboarding"* reads: *"From … to … · coordinated
by Akpene Amoah · your buddy is …"*, **In Progress**, **4 of 12 done**. The list has *Return the
signed code of conduct* marked **Yours to do**.

> *"Kojo sees the whole first month — every task, and who has it — and the one that is Kojo's to do."*

**2 — *Tasks given to you — 1 to do*.** *Return the signed code of conduct* — *"Part of your own
onboarding · coordinated by Akpene Amoah"*, due today, *"Signed off by Akpene Amoah"*.

**3 — 🔴 LIVE WRITE 12 — *Mark done*.** Note: *"Signed copy handed to the HR counter."* The dialog
reads *"Akpene Amoah signs it off after you do, and is told it is ready."* The toast reads *"Marked
done — waiting for sign-off — Akpene Amoah has been told it is ready to sign off."* The task now reads
*"Done — waiting for Akpene Amoah to sign it off."*

> *"Kojo says it is done. It is not closed yet: a second person signs it off, and the system has
> already told them."*

**4 — 🔴 LIVE WRITE 13 — sign in as `hr.head`.**
1. *My Notifications* has *"Return the signed code of conduct for Kojo Ansah is done — waiting for
   your sign-off"*.
2. Open **Task Queues → Verification**. One task.
3. Press **Verify**. The toast reads *"Verified"*.

> *"Kojo did it; the Head of HR signed it off; neither could have done both. The person who
> completes a task can never be the one who verifies it."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The page | `GET api/employee-portal/onboarding` | signing in — the employee from the token |
| **Mark done** | `POST api/employee-portal/onboarding/tasks/{taskId}/complete` | signing in, and being the task's assignee |

- Somebody else's task, or a missing one, answers the same 404 — *"Onboarding task with ID '…' not
  found."* — so the door never confirms that a colleague's task exists.
- A closed plan is refused: *"This onboarding is closed, so its tasks can no longer be marked done."*
- *"Your onboarding"* is the reader's latest plan that is not cancelled.
- *"Tasks given to you"* holds the open ones, plus those finished in the last 30 days.
- A task that needs a sign-off goes to *Awaiting verification*, and the coordinator is told.
- Evidence files are not taken here.

### ⚠ Known gaps

| Gap | |
|---|---|
| **O-64 · The sign-off promise can be false.** On a plan with no coordinator — the New plan dialog leaves it optional — the page still says *"HR signs it off…"* and *"HR has been told it is ready to sign off."* Nobody is told: the notice needs a coordinator, and the sweep's chase is logged *"Nobody to send to"*. Where there is a coordinator, they may not be able to sign (**O-40**) | |
| **O-65 · What the list keeps, and whose words it shows.** *"Your note:"* shows the task's completion note whoever wrote it, HR's included. A task left open on a plan HR closed stays in *Tasks given to you*, with *"This onboarding is closed."*, for ever | |
| **O-5** · here a failed read reads *"Nothing here"* | |

---

## 20. Where it shows up outside its menu

| Where | What it does there | Chapter |
|---|---|---|
| **Recruitment → confirming a hire's start** | makes the new hire's onboarding plan from the most specific template. The confirming officer becomes coordinator, and the reason is stored: *"Created on hire from "…". Most specific match — …"*. It also fires the **On hire** rules | 16, 8 |
| **The employee form, and the employee import** | fire the **On hire** rules. They make no plan (**O-29**) | 8 |
| **Staff movements → implementing** a transfer, lateral move, secondment or promotion | fires the **On transfer** or **On promotion** rules. A demotion, an acting appointment or a redesignation fires nothing | § 1.5 |
| **The employee record → Orientation tab** | *"Orientation programmes this employee was enrolled in, with progress and result. Enrolment and sessions are on the orientation screens."*, and *Open in Orientation*, which lands on an empty register (**O-23**) | 7 |
| **The portal → My Notifications** | every notice and every digest, whatever became of the email. Their buttons open the portal pages, or the HR plan and queues for a coordinator | 17 |
| **HR policy settings → "Orientation & onboarding — reminders"** | the four reminder windows. HR can read them; saving needs `HR.Company.Admin` (**O-57**) | 17 |
| **HR settings → Letter & Email Templates** | the thirteen emails — the digest, seven orientation notices and five onboarding ones — under the module *OnboardingOrientation*, each with its tokens. Until a tenant saves its own wording, the shipped default is used | 17 |
| **Training → the vendor register** | vendors and their trainers become session facilitators, snapshotted at the pick. A vendor blacklisted later is flagged on the session | 6 |

---

## 21. Resetting after a run

**The clean reset is a rebuild** (Rule 1). What follows undoes this guide's own writes, on a database
you mean to demonstrate on again.

| # | Wrote | Undo |
|---|---|---|
| 1–3 | a copy of the October induction day, its facilitators confirmed, opened for enrolment | as a **SuperAdmin**, the session's trash button; nobody holds a seat, so it is allowed. As HR: *Change status… → Cancelled*. The Head of HR's *"You are facilitating…"* notice stays |
| 4 | Efua Seidu on the compliance course, and the notice that told Efua | the SQL below. *Withdraw* is not an undo: it leaves a withdrawn row, which the triggers screen reports as *Ended by HR* and the overdue lists count (**O-2**) |
| 5 | *Create the corporate email account* completed | the SQL below; nothing reopens a task |
| 6, 12, 13 | *Return the signed code of conduct* — added, marked done, verified | the SQL below; nothing removes a task |
| 7–9 | *Q4 Product Launch Briefing*, its module and its content item | as a **SuperAdmin**, the programme's trash button; a Draft may be deleted |
| 10 | a reminder run | nothing: each item is sent once, and the log keeps it |
| 11 | *CEO Welcome Message* marked done for Kojo Ansah | the SQL below |

Run the SQL as a database administrator, against the demo database only:

```sql
-- Chapter 21 reset. Undoes LIVE WRITES 4, 5, 6 (with 12 and 13) and 11 on the demo database.
DECLARE @t uniqueidentifier = (SELECT TenantId FROM Employees WHERE EmployeeNumber = 'TDC/00063' AND IsDeleted = 0);
DECLARE @kojo uniqueidentifier = (SELECT Id FROM Employees WHERE TenantId = @t AND EmployeeNumber = 'TDC/00063' AND IsDeleted = 0);
DECLARE @efua uniqueidentifier = (SELECT Id FROM Employees WHERE TenantId = @t AND EmployeeNumber = 'TDC/00017' AND IsDeleted = 0);
DECLARE @plan uniqueidentifier = (SELECT TOP 1 Id FROM OnboardingPlans
  WHERE TenantId = @t AND EmployeeId = @kojo AND IsDeleted = 0 ORDER BY CreatedAt);

-- LIVE WRITE 4: Efua Seidu's enrolment on ORI-CMP-001, and the notice that told Efua.
DECLARE @efuaEnrolment uniqueidentifier = (SELECT eo.Id FROM EmployeeOrientations eo
  JOIN OrientationPrograms p ON p.Id = eo.ProgramId
  WHERE eo.TenantId = @t AND eo.EmployeeId = @efua AND p.ProgramCode = 'ORI-CMP-001' AND eo.IsDeleted = 0);
UPDATE OrientationNotifications SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME()
  WHERE EmployeeOrientationId = @efuaEnrolment AND IsDeleted = 0;
UPDATE EmployeeOrientations SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME()
  WHERE Id = @efuaEnrolment;

-- LIVE WRITE 5: reopen "Create the corporate email account" on Kojo Ansah's plan (1 = Pending).
UPDATE OnboardingTasks SET Status = 1, CompletedDate = NULL, CompletedById = NULL, CompletionNotes = NULL
  WHERE OnboardingPlanId = @plan AND TaskName = 'Create the corporate email account' AND IsDeleted = 0;

-- LIVE WRITES 6, 12 and 13: the task added to Kojo Ansah's plan.
UPDATE OnboardingTasks SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME()
  WHERE OnboardingPlanId = @plan AND TaskName = 'Return the signed code of conduct' AND IsDeleted = 0;

-- LIVE WRITE 11: the CEO Welcome Message marked done on Kojo Ansah's induction.
DECLARE @kojoEnrolment uniqueidentifier = (SELECT eo.Id FROM EmployeeOrientations eo
  JOIN OrientationPrograms p ON p.Id = eo.ProgramId
  WHERE eo.TenantId = @t AND eo.EmployeeId = @kojo AND p.ProgramCode = 'ORI-ONB-001' AND eo.IsDeleted = 0);
UPDATE cp SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME()
  FROM OrientationContentProgresses cp JOIN OrientationContentItems ci ON ci.Id = cp.ContentItemId
  WHERE cp.EmployeeOrientationId = @kojoEnrolment AND ci.Title = 'CEO Welcome Message' AND cp.IsDeleted = 0;
UPDATE EmployeeOrientations SET ProgressPercentage = 0 WHERE Id = @kojoEnrolment;
```

⚠ The script finds the rows by name: the task titles and the employee numbers TDC/00063 and
TDC/00017. If you typed a different title in LIVE WRITE 6, change it here.

⚠ It was **compiled** against UAT on 2026-09-24 under `SET NOEXEC ON`, so every table and column
exists, but it has **not been run**.

The notices that the onboarding writes sent — *"Your onboarding task: …"* and *"… is done — waiting
for your sign-off"* — stay in their readers' notifications.

---

## 22. The short path — fifteen minutes

For a room that has fifteen minutes for this module, as `hr.head` unless marked:

| # | Screen | The beat | Chapter |
|---|---|---|---|
| 1 | **Dashboard** | the six tiles; one person overdue, and already chased by the sweep | 4 |
| 2 | **Enrollments** | New Employee Onboarding: thirty-seven people, twenty-nine of them enrolled by the rule. **LIVE WRITE 4**: two asked for, one enrolled, one skipped | 7 |
| 3 | **Enrollment Triggers** | Kojo Ansah: the hire rule's window lapsed, so HR enrolled Kojo by hand; the template the placement would get, with its score. Nana Legal: the rule fired by itself | 8 |
| 4 | **Onboarding plan** | Kojo's plan: owners, the section queues, the comment thread. **LIVE WRITE 6**: a task that needs a second signature | 10 |
| 5 | **My Onboarding**, as `new.hire` | **LIVE WRITE 12**: Kojo marks it done | 19 |
| 6 | **Task Queues**, as `hr.head` | **LIVE WRITE 13**: the Head of HR verifies it | 19, 11 |

Leave the programme page (chapter 14) and the portal player (chapter 18) out of a short run. Both lead
towards a completion that cannot happen (Rule 3).

---

## 23. Findings

**65 in the code** (`O-…`, each in its chapter's gap block, except O-1 in § 1.1) and **four in the
demo data** (`D-…`).
None was fixed by this guide.

### Demo-breaking — know these before you present

| # | Finding | Chapter |
|---|---|---|
| **O-15** | No enrolment made in the product can reach *Completed* on any of the three seeded programmes: nothing creates a declaration, and a programme with no content has nothing that evaluates completion | 7, 14, 18 |
| **O-4** | HR can act inside a participant's own player — mark content done, sit the quiz, sign the declaration. The screens show nothing; the signature records HR's IP address, and only an audit column holds HR's user id | 4, 7, 18 |
| **O-20** | Nobody already on a programme can be put on a session, so the induction days can never be filled with the starters | 7 |
| **O-59** | The quiz can be re-sat for ever, even after completion, and a lower score turns a completed programme *Failed*; the answer key travels with the paper after the first attempt | 18 |

### Worth fixing first

| # | Finding | Chapter |
|---|---|---|
| **O-58** | Optional content is counted towards completion. The seeded programmes have none, so it bites the first programme somebody authors with an optional item | 18 |
| **O-2**, **O-24**, **O-60** | "Overdue" means three things: a date, a status only the seeder writes, and a portal tile. Withdrawn enrolments count as overdue | 4, 7, 18 |
| **O-19** | Bulk enrolment over-fills a capped session | 7 |
| **O-26** | The record check is by role and the policies by permission, so TenantAdmin passes one and fails the other | 7, 18 |
| **O-32** | A plan cannot be changed, cancelled or deleted after it is made | 9 |
| **O-34** | Editing an asset erases its issue and acknowledgement dates | 10 |
| **O-38** | The Overdue queue ignores the plan's status; on UAT, 6,664 of its 6,727 tasks sit on cancelled plans | 11 |
| **O-40**, **O-64** | The sign-off chain assumes the coordinator is in HR, and promises a notice that may go to nobody | 11, 19 |
| **O-10** | Every delete in the module but one is Admin-only, and offered to the HR desk | 6, 14–16 |
| **O-43** | Saving a programme's Overview wipes its owning unit | 14 |
| **O-9**, **O-44** | An empty number box is sent as 0: *Max participants* and certificate *Validity* cannot be left empty | 5, 14 |
| **O-45** | The question dialog blocks *Save* silently; a Free Text question cannot be added | 14 |
| **O-5** | Refusals and failures read as "empty" nearly everywhere — made worse by the app's default retry, which does not recognise this client's 4xx errors | 4 onwards |

### Every code finding

| # | Finding | Ch. |
|---|---|---|
| O-1 | The checklist's *"Attend the corporate induction"* task and the induction enrolment are not connected | § 1.1 |
| O-2 | Withdrawn enrolments are counted and listed as overdue | 4 |
| O-3 | The landing's Setup cards are offered to everyone who reaches it | 3 |
| O-4 | The participant link puts HR inside that person's own player, with its controls live | 4 |
| O-5 | Refusals and failures read as "empty" across the module | 4 |
| O-6 | The dashboard's windows and cut-offs are unstated | 4 |
| O-7 | There is no "all sessions" view | 5 |
| O-8 | *"Publish it"* is not the step that opens enrolment | 5 |
| O-9 | *Max participants* cannot be left empty | 5 |
| O-10 | Every delete but one is Admin-only, and offered to the HR desk | 6 |
| O-11 | A session's *Requires approval* does nothing | 6 |
| O-12 | The session's Overview goes stale after a status change, and can erase the stamped actuals | 6 |
| O-13 | Session status has no rules, and shows raw values | 6 |
| O-14 | The logistics fields do not follow the delivery mode | 6 |
| O-15 | No enrolment made in the product can reach *Completed* on the seeded programmes | 7 |
| O-16 | *Run again* says the facilitators are asked to confirm; nothing asks them | 6 |
| O-17 | The register's times are hung on today's date | 6 |
| O-18 | A failed attendance read can overwrite the day with *Present* | 6 |
| O-19 | A bulk enrolment can over-fill a capped session | 7 |
| O-20 | Nobody already on a programme can be put on a session | 7 |
| O-21 | "The latest row" is judged among the rows on screen | 7 |
| O-22 | A hand enrolment can be recorded as *Automatic Rule* or *Self Enrollment* | 7 |
| O-23 | The employee record's *Open in Orientation* is ignored | 7 |
| O-24 | The *Overdue* tile counts a status only the seeder writes; *Pending Assessment* and *Exempted* are never written | 7 |
| O-25 | *Withdraw* is offered on completed and no-show enrolments, with no status rule | 7 |
| O-26 | The record check is by role; the policies are by permission | 7 |
| O-27 | The dialogs' own buttons do not reset them, and some labels mislead | 7 |
| O-28 | The triggers screen's hint is wrong twice | 8 |
| O-29 | The onboarding card over-promises; only recruitment's confirm-start makes a plan | 8 |
| O-30 | The triggers screen's smaller slips | 8 |
| O-31 | Half the plan and task statuses have no writer | 9 |
| O-32 | A plan cannot be changed after it is made | 9 |
| O-33 | The New plan dialog checks nothing a person would expect | 9 |
| O-34 | Editing an asset erases its history | 10 |
| O-35 | Editing a task ignores some of what it shows | 10 |
| O-36 | Closing and opening a plan are unguarded, and say more than they do | 10 |
| O-37 | What is written on a task is half hidden | 10 |
| O-38 | The Overdue queue ignores the plan's status | 11 |
| O-39 | A queue row does not say whose onboarding it is | 11 |
| O-40 | The sign-off chain assumes the coordinator is in HR | 11 |
| O-41 | *Reminders & notices* is on no menu | 12 |
| O-42 | The catalogue's numbers, and the copy's title | 13 |
| O-43 | Saving the Overview wipes the owning unit, and never shows the owner | 14 |
| O-44 | *Validity (months)* cannot be left empty | 14 |
| O-45 | The question dialog blocks *Save* without a word | 14 |
| O-46 | Publishing is unguarded, and *Pending approval* has no approver | 14 |
| O-47 | Prerequisites are added once and never changed | 14 |
| O-48 | A hidden delay can block saving a rule | 14 |
| O-49 | Client and server limits disagree across the module | 14 |
| O-50 | Small things that mislead on the programme page | 14 |
| O-51 | The category parent picker hides the wrong category | 15 |
| O-52 | A deactivated template disappears for good | 16 |
| O-53 | Two template switch descriptions promise the wrong thing | 16 |
| O-54 | Part of a template stops at the template | 16 |
| O-55 | Two reminders cannot behave as labelled | 17 |
| O-56 | The reminders screen claims more than was sent | 17 |
| O-57 | The fixes the reminders screen points to are out of HR's reach | 17 |
| O-58 | Optional content is counted towards completion | 18 |
| O-59 | The quiz can be re-sat for ever, and after completion | 18 |
| O-60 | The portal reads statuses the HR screens do not use | 18 |
| O-61 | Certificates cannot leave the screen, and never lapse | 18 |
| O-62 | Content shows less than it holds | 18 |
| O-63 | The portal player's small print | 18 |
| O-64 | The sign-off promise can be false | 19 |
| O-65 | What *My Onboarding* keeps, and whose words it shows | 19 |

### In the demo data

Measured on `ErpSystemDB_UAT` on 2026-09-24:

| # | Finding |
|---|---|
| **D-1** | **The harness suites leave their fixtures live.** Measured: 505 programmes, 27 of them Active fixtures; 108 sessions; 1,523 enrolments, of which 187 are fixture auto-enrolments on New Employee Onboarding; 811 cancelled onboarding plans carrying 6,664 overdue tasks; 122 active templates; 14 tasks awaiting verification. A rebuild clears them (Rule 1) |
| **D-2** | **Scenario 145 makes a new *TDC New Starter Checklist* on every re-run**, and moves the *Default* flag to the newest copy. It looks for the template through `GET /onboarding-plan-templates`, which does not exist, and treats the error as "none yet". UAT holds eleven copies. The fix is in the harness: read `/onboarding-plan-templates/all` |
| **D-3** | **The other modules' personas are all employed on the build day**, so the new-hire rule enrols them — 26 legal and estates personas (`LA-…`) and 3 facilities ones (`FAC-…`), two days after the build on UAT. It reads as the rule working, and it is: the data says they are new |
| **D-4** | **There is no mail server** (`EmailSettings` is empty), so every email ends *No mail server* and every screen that reports delivery says so (Rule 7). Shared with the company schedule guide's R4-2.4 |

---

## Appendix A — the twenty screens and their files

| Screen | File, under `frontend/src/app/` |
|---|---|
| `/hr/orientation` | `hr/orientation/page.tsx` |
| `/hr/orientation/dashboard` | `hr/orientation/dashboard/page.tsx` |
| `/hr/orientation/sessions` | `hr/orientation/sessions/page.tsx` |
| `/hr/orientation/sessions/[id]` | `hr/orientation/sessions/[id]/page.tsx` |
| `/hr/orientation/enrollments` | `hr/orientation/enrollments/page.tsx` |
| `/hr/orientation/triggers` | `hr/orientation/triggers/page.tsx` |
| `/hr/orientation/onboarding` | `hr/orientation/onboarding/page.tsx` |
| `/hr/orientation/onboarding/[id]` | `hr/orientation/onboarding/[id]/page.tsx`, with `components/hr/orientation/OnboardingTaskBoard.tsx` |
| `/hr/orientation/onboarding/queues` | `hr/orientation/onboarding/queues/page.tsx` |
| `/administration/hr/orientation` | `administration/hr/orientation/page.tsx` |
| `/administration/hr/orientation/programs` | `administration/hr/orientation/programs/page.tsx` |
| `/administration/hr/orientation/programs/new` | `administration/hr/orientation/programs/new/page.tsx`, with `components/hr/orientation/OrientationProgramForm.tsx` |
| `/administration/hr/orientation/programs/[id]` | `administration/hr/orientation/programs/[id]/page.tsx`, with `ProgramModulesPanel.tsx` and `ProgramQuestionsPanel.tsx` |
| `/administration/hr/orientation/categories` | `administration/hr/orientation/categories/page.tsx` |
| `/administration/hr/orientation/onboarding-templates` | `administration/hr/orientation/onboarding-templates/page.tsx` |
| `/administration/hr/orientation/onboarding-templates/[id]` | `administration/hr/orientation/onboarding-templates/[id]/page.tsx`, with `OnboardingTemplateAudiencePanel.tsx` |
| `/administration/hr/orientation/reminders` | `administration/hr/orientation/reminders/page.tsx` |
| `/me/orientation` | `me/orientation/page.tsx` |
| `/me/orientation/[id]` | `me/orientation/[id]/page.tsx` |
| `/me/onboarding` | `me/onboarding/page.tsx` |

The three *"make another one like this"* dialogs — copy a programme, copy a template, run a session
again — are in `components/hr/orientation/CopyDialogs.tsx`.

---

## Appendix B — the permission map

Every controller also requires `InternalOnly`: signed in, and not an external user, candidate or
consultant client. **Read**, **Write** and **Admin** are `HR.Orientation.Read`, `.Write` and
`.Admin`, and each is satisfied by the ones above it. **Self** means no permission: the service
decides, by ownership.

| Controller | Read | Write | Admin | Self |
|---|---|---|---|---|
| `api/orientation-programs` | every read, the reach count and the trigger diagnosis | create, copy, update, status, and every module, content item, prerequisite, rule and question add or edit; **Enrol audience now** | **delete** — the programme, a module, a content item, a prerequisite, a rule, a question; **the trigger sweep's run** | — |
| `api/orientation-sessions` | every read but one | create, copy, update, status, facilitators, attendance | **delete** a session or a facilitator | `GET {id}`: when and where a session is |
| `api/employee-orientations` | the HR lists: by programme, session, status, overdue, due soon; expiring certificates | enrol, bulk-enrol, update (no caller), withdraw, **add a declaration** (no caller), issue and revoke certificates | **delete** an enrolment (no caller) | everything the participant does: the enrolment, its content and progress, the paper and its submission, declarations and signing, feedback, certificates |
| `api/orientation-categories` | every read | create, update | **delete** | — |
| `api/orientation-dashboard` | the dashboard | — | — | — |
| `api/orientation-notifications` | by recipient, by enrolment, recent | **send queued**, create | — | my notices, unread count, mark read |
| `api/onboarding-plans` | every read | create, update (no caller), start, complete, tasks (add, edit, complete, verify), comments, assets (add, edit) | — *(there is no delete)* | — |
| `api/onboarding-plan-templates` | every read, including *applicable* and *default* (no caller) | create, copy, update, task templates (add, edit), **audiences — add and remove** | **delete** a template or a task template | — |
| `api/orientation-reminders` | preview, runs, log | **run now** | — | — |
| `api/employee-portal/onboarding` | — | — | — | my onboarding; **Mark done** on a task given to me |

Who holds what:
- The `HR` role, and the legacy `HR User`, hold Read and Write.
- SuperAdmin, TenantAdmin and `Admin` hold all three.
- No other role holds any of them.
- The participant needs none.

⚠ The service's own record check for an enrolment addressed by id admits **roles**, not permissions:
SuperAdmin, HR and HR User, or the participant (**O-26**).

---

## Appendix C — related documents

| Document | For |
|---|---|
| [`HR-DEMO-FEEDBACK-ROUND-4-PLAN.md`](../../programme/HR-DEMO-FEEDBACK-ROUND-4-PLAN.md) | what round 4 built here — lanes I, I-b, J, K-a, K-b1, K-b2, L and M — and why |
| [`HR-RECRUITMENT-SYSTEM-GUIDE.md`](../recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md) | confirming a hire's start: where an onboarding plan is made on hire |
| [`HR-COMPANY-SCHEDULE-SYSTEM-GUIDE.md`](../company-schedule/HR-COMPANY-SCHEDULE-SYSTEM-GUIDE.md) | the same demo database, its missing mail server and its harness litter |
| [`HR-EMPLOYEES-SYSTEM-GUIDE.md`](../employees/HR-EMPLOYEES-SYSTEM-GUIDE.md) | the employee record, its Orientation tab, and the import that fires the hire rules |
| [`docs/HR/README.md`](../../README.md) | the index of the series |
