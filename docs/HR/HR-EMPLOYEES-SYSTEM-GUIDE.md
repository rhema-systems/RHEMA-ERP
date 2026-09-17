# HR Employees — System Guide and Demonstration Workbook

**Status:** written 2026-09-16 from the source — page components, services, controllers, entities,
the EF model and the demo seeders. It describes what the code is built to do. Where the demo
database will not show what the code can do, that is said in the step rather than smoothed over.

**Scope:** the whole **Employees** group of the HR sidebar — all eight menu items — plus the two
screens that hang off the register without a menu entry, and the three self-service screens that
feed its queues.

| # | Menu item | Route | Chapter |
|---|---|---|---|
| 1 | Employees | `/hr/employees` | 3 |
| — | *(no menu entry)* New employee | `/hr/employees/new` | 4 |
| — | *(no menu entry)* The profile | `/hr/employees/[id]` | 5 |
| — | *(no menu entry)* Edit | `/hr/employees/[id]/edit` | 6 |
| 2 | Change Requests | `/hr/employees/change-requests` | 7 |
| 3 | Letter Requests | `/hr/employees/letter-requests` | 8 |
| 4 | ID Expiry | `/hr/employees/identification-expiry` | 9 |
| 5 | Import | `/hr/employees/import` → `/new` → `/[id]` | 10 |
| — | *(no menu entry)* Payroll reconciliation | `/hr/employees/payroll-reconciliation` | 11 |
| 6 | Organogram | `/hr/organogram` | 12 |
| 7 | Announcements | `/hr/announcements` | 13 |
| 8 | Policy Library | `/hr/policies` | 14 |

Fourteen screens, plus a profile carrying **35 tabs in 8 groups**, plus three portal screens.

---

## This document is two things at once

The recruitment guide was a reference. This one is a reference **and a script you can perform**.
Every chapter has the same five parts, and you can read only the ones you need:

| Part | Marked | Use it for |
|---|---|---|
| **Where you are** | 📍 | the sidebar path, the URL, which persona, how long |
| **What it is** | 📖 | one paragraph you could say to a non-technical room |
| **On the page** | 👁 | every control on the screen, exhaustively — nothing omitted |
| **Walk it** | ▶ | numbered steps: click this, expect that, say this |
| **Behind the page** | ⚙ | endpoint → service → table, and the permission that gates it |

Two more markers appear inside the walks:

- **🔴 LIVE WRITE** — this step changes real data. Every one is numbered (`LIVE WRITE 1` … `9`),
  and chapter 15 tells you how to undo each.
- **⚠ CAREFUL** — a way this step goes wrong in front of people, and what to do instead.

**Say-lines are in quotation marks and indented.** They are written to be read aloud more or
less as they stand. Change the names, keep the order of the ideas — the order is doing the work.

---

## Before anything else: read chapter 2

**The demo database does not populate most of the employee profile.** Fifteen of the 35 tabs are
empty on a freshly built `ErpSystemDB_UAT`, because the workforce seeder creates employees and no
sub-records, and four of the eight menu items open onto empty queues for the same reason.

That is not a fault — those queues are *meant* to fill from the portal, which is exactly what this
demonstration does. But it means **you cannot open this book cold and start clicking.** Chapter 2
is a prep checklist that takes 35–45 minutes the evening before and turns the demo from a tour of
empty tables into an end-to-end story. Do it once; it survives until the database is rebuilt.

---

## Conventions

**Routes.** `/hr/employees/[id]` is the file
`frontend/src/app/hr/employees/[id]/page.tsx`. A segment in square brackets is a parameter.

**Table names.** There is no `HR_` prefix and no `ToTable()` mapping in the solution. A table is
named after its `DbSet<>` property in `ApplicationDbContext.HR.cs` — the `Employee` entity lives
in `Employees`, `EmployeeDependent` in `EmployeeDependents`.

**Columns every table here carries.** Every entity in this chapter derives from `TenantEntity`:

| Column | Meaning |
|---|---|
| `Id` | `Guid` primary key |
| `TenantId` | the company the row belongs to; every query is filtered by it |
| `CreatedAt`, `CreatedBy`, `CreatedById` | when and by whom |
| `UpdatedAt`, `UpdatedBy`, `LastModifiedById` | last change |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | soft delete — rows are hidden, never removed |

**The permission ladder.** Three permissions gate this whole module, and they stack:

| Permission | Grants | Held by the `HR` role? |
|---|---|---|
| `HR.Employee.Read` | every read that carries PII — the profile, every sub-record, the queues | **yes** |
| `HR.Employee.Write` | create, amend, verify, approve a change request, issue a letter, run the import | **yes** |
| `HR.Employee.Admin` | **delete** anything, and **activate / deactivate / terminate / reinstate** | **no** |

That last row is the single most important operational fact in this guide and it catches people
out. `hr.head` holds the HR role, so **`hr.head` cannot terminate, deactivate, activate, reinstate
or delete an employee, and cannot delete any sub-record.** The buttons are on the screen; the
server answers 403. Chapter 5.9 says what to do about it in a demo.

Announcements and the policy library are a different family — `HR.Company.Read` / `.Write` —
which the HR role also holds. A policy or a notice is a communication from the organisation, not
an act on a person's record, which is why they are gated separately and why they sit in this menu
group rather than under Administration.

---

## 1. How the employees module hangs together

### 1.1 One record, thirty-five windows onto it

Recruitment is a chain: each record hands the next one its contents. **Employees is not a chain.
It is a hub.** One `Employee` row sits in the middle, and everything else in HR points at it.

```
                      ┌──── owned BY the employee record ────┐
                      │  addresses · emergency contacts       │
                      │  dependents · identification          │
   PORTAL             │  qualifications · skills              │        OTHER MODULES
   /me/profile ──────▶│  certifications · work history        │◀────── movements · probation
   change requests    │  contracts · position history         │        separation · leave
   letter requests    │  salary placement · bank accounts     │        attendance · benefits
   policy signing     │  referees · guarantors · documents    │        training · appraisals
                      │  expatriate assignment · relievers    │        discipline · awards
                      │                                       │        medical · travel · assets
                      │           E M P L O Y E E             │        orientation · succession
                      └───────────────────────────────────────┘
                                       ▲
                                       │
                          ESTABLISHMENT decides the shape
                    position → unit → level → reports-to → grade
```

Three things follow from that picture, and they are the three points worth landing in a room:

**1. The establishment decides, the employee record follows.** You do not type somebody's
department. You choose their **position**, and the position already knows its unit, its
organisation level, its grade, its probation term and which position it reports to. The create
form greys out the unit box to make the point physically.

**2. The profile is read-only wherever another module owns the fact.** Sixteen of the 35 tabs are
summaries over another module's data with a door back to it and no write button at all. A movement
is implemented on the movements screen because that is where its approvals, salary placement and
return date live. A second button here would be a second door onto the same rule — and the second
door is where rules drift.

**3. Staff do not edit their own record. They ask.** A contact number they change themselves; a
name, a date of birth, a statutory number or a bank account opens a request with a reason and a
document attached, and HR approves it — which writes the value onto the record in the same
transaction. That asymmetry is deliberate and it is chapter 7.

### 1.2 The tables

| What | Entity | Table | Written from |
|---|---|---|---|
| The person | `Employee` | `Employees` | create form, edit form, import, change-request approval |
| Addresses | `EmployeeContact` | `EmployeeContacts` | Addresses tab |
| Next of kin | `EmployeeEmergencyContact` | `EmployeeEmergencyContacts` | Emergency contacts tab |
| Dependents | `EmployeeDependent` | `EmployeeDependents` | Dependents tab |
| ID cards | `EmployeeIdentificationCard` | `EmployeeIdentificationCards` | Identification tab |
| Degrees | `EmployeeQualification` | `EmployeeQualifications` | Qualifications tab |
| Skills | `EmployeeSkill` | `EmployeeSkills` | Skills tab |
| Credentials | `EmployeeCertification` | `EmployeeCertifications` | Certifications tab |
| Previous jobs | `EmployeeWorkHistory` | `EmployeeWorkHistories` | Work history tab |
| Contracts | `EmployeeContractDetail` | `EmployeeContractDetails` | Contracts tab |
| Post history | `EmployeePositionHistory` | `EmployeePositionHistories` | Position history tab |
| Grade placement | `EmployeeSalaryAssignment` | `EmployeeSalaryAssignments` | Salary tab |
| Pay change | `EmployeeSalaryChangeRequest` | `EmployeeSalaryChangeRequests` | Salary tab, approved on the engine |
| Bank | `EmployeeBankDetail` | `EmployeeBankDetails` | Bank tab |
| Referees | `EmployeeReferee` | `EmployeeReferees` | Referees tab |
| Guarantors | `EmployeeGuarantor` | `EmployeeGuarantors` | Guarantors tab |
| Files | `EmployeeDocument` | `EmployeeDocuments` | Documents tab, through the upload gate |
| Expatriate | `ExpatriateAssignment` | `ExpatriateAssignments` | Expatriate tab |
| Cover | `EmployeeReliever` | `EmployeeRelievers` | Relievers tab |
| Correction asked for | `EmployeeProfileChangeRequest` | `EmployeeProfileChangeRequests` | the portal |
| Letter asked for | `HrLetterRequest` | `HrLetterRequests` | the portal |
| Notice | `HrAnnouncement` | `HrAnnouncements` | Announcements |
| Policy | `HrPolicy` | `HrPolicies` | Policy Library |
| Signature | `HrPolicyAcknowledgement` | `HrPolicyAcknowledgements` | the portal |
| Bulk load | `EmployeeImportSession` + rows | `EmployeeImportSessions`, `…Rows` | the import wizard |

### 1.3 The four vocabularies worth memorising

**`Employees.StaffStatus`** — what the person *is*:

`Active` · `Inactive` · `Probation` · `Suspended` · `Terminated` · `Retired` · `OnLeave`

**`Employees.EmploymentType`** — the system's fixed set of engagement kinds:

`Permanent` · `Contract` · `FixedTerm` · `Internship` · `Casual` · `PartTime` · `Temporary` ·
`Consultant` · `Freelance`

Note that **`EmployeeContractType` is a different thing** — TDC's own vocabulary for what the
appointment letter calls the engagement, with the duration each kind normally runs for. The enum
is the system's; the table is the organisation's.

**`Employees.OffPayrollReason`** — why somebody is not paid through the run:

| Value | Reads as |
|---|---|
| `PaidByInvoice` | Paid by invoice (consultant, contractor) |
| `Allowance` | Allowance or stipend (intern, national service) |
| `PaidByParentOrganisation` | Paid by parent organisation (secondee) |
| `Unpaid` | Unpaid (volunteer, honorary) |
| `BoardOrCommittee` | Board or committee member (sitting allowance) |
| `Other` | Other (say what in the note) |

**`Employees.PayBasis`** — how the basic pay figure is *arrived at*, which is independent of both
of the above:

| Value | Meaning |
|---|---|
| `SalaryScale` | basic pay is the amount of the notch the person is placed on |
| `Negotiated` | basic pay is an amount agreed for this person; placement on the scale is refused while this stands |

A permanent employee can be negotiated. A contractor can be on the scale. A person can be on the
scale and not on payroll. These are three independent axes and the demo should say so once,
because a room will assume they are one.

### 1.4 The eight profile groups

The rail on the left of the profile is generated from one table,
`frontend/src/components/hr/employee/profileTabGroups.ts`. Every surface — the rail, the phone
dropdown, the `?tab=` deep link — reads it, so the groups below are exactly what you will see:

| Group | Tabs |
|---|---|
| **Personal** | Overview · Addresses · Emergency contacts · Dependents · Identification · *Expatriate* |
| **Employment** | Contracts · Position history · Movements · Probation · Teams · Relievers · Separation |
| **Pay & benefits** | Salary · Salary changes · Bank · Benefits |
| **Capability** | Qualifications · Skills · Certifications · Work history · Training |
| **Performance & conduct** | Appraisals & goals · Discipline · Awards |
| **Time & leave** | Leave · Attendance |
| **Welfare & travel** | Medical · Travel · Assets |
| **Records** | Documents · Referees · Guarantors · Orientation · Succession |

*Expatriate* is the one conditional tab — it appears only when `IsExpatriate` is ticked. Hidden is
not forbidden: the tab's own reads are still gated on the server.

The tab key is in the URL. `?tab=salary` opens the Salary tab directly, and other screens link
straight in — an approved salary-review proposal lands on `?tab=salary`. **Renaming a key breaks
those links**, which is why the file carries a warning about it.

Radix mounts only the active tab's content, so **a tab's data is fetched when you open it, not
when the page loads.** That is why the profile paints fast and why each tab has a beat of delay
the first time. In a demo that beat is your cue to say what the tab is for.

### 1.5 Which tabs can be written from here, and which cannot

This distinction runs through the whole chapter 5 and is worth having in front of you:

| Writeable here (17) | Read-only here (18) |
|---|---|
| Addresses, Emergency contacts, Dependents, Identification, Qualifications, Skills, Certifications, Work history, Contracts, Position history, Salary *(placement, basis, payroll profile, components)*, Bank, Referees, Guarantors, Documents, Expatriate, Relievers | Overview *(edit form)*, Movements, Probation, Separation, Salary changes *(raise only)*, Benefits, Leave, Attendance, Training, Appraisals & goals, Discipline, Awards, Medical, Travel, Assets, Orientation, Succession, Teams |

Teams is the odd one: it is read-only and its owner is not an HR module at all but
**Administration → Organization → Teams**. Membership is written from the team's side, deliberately.

---

## 2. Before the room fills — the prep that makes this demo possible

**Time: 35–45 minutes, the evening before, alone.** Do it in order. Every step is a one-off
that survives until somebody rebuilds `ErpSystemDB_UAT`.

### 2.1 Why this chapter exists

`TdcDemoWorkforceSeeder` creates the workforce — names, staff numbers, positions, units, reporting
lines, salaries — and deliberately creates **no employee sub-records**. It says so in its own
header: the sub-records were reserved for the real TDC employee-details file. The consequence,
checked against the seeders on 2026-09-16:

| Tab / screen | State on a fresh UAT database |
|---|---|
| Overview | **populated** — every field |
| Dependents | **populated** for a handful of people, by `MedicalDataSeeder` |
| Emergency contacts | **populated** for a handful, by `MedicalDataSeeder` and `SheDataSeeder` |
| Movements, Probation, Leave, Attendance, Training, Appraisals, Discipline, Awards, Medical, Travel, Assets, Orientation, Succession, Benefits, Separation | **populated, but only for the people their own module's seeder picked.** Do not assume your showcase employee has rows on all of them — check in §2.9 while you pre-open the tabs, and pick a different anchor if too many come up empty |
| Addresses, Identification, Qualifications, Skills, Certifications, Work history, Contracts, Position history, Salary placement, Bank, Referees, Guarantors, Documents, Expatriate, Relievers | **EMPTY for everyone** |
| Change Requests | **empty** — no seeder |
| Letter Requests | **empty** — no seeder |
| ID Expiry → Due | **empty**, twice over: no identification cards exist, and no identification type carries a warning time |
| Announcements | **empty** — no seeder |
| Policy Library | **empty** — no seeder |
| Payroll reconciliation | reads **nothing to reconcile** |

> **Say nothing about this in the room.** The fix is prep, not commentary. A tab that is empty
> because nobody has filed anything is a true state of the system; it just makes a poor demo.

### 2.2 Pick your two anchor employees

Everything in chapters 5 and 6 is performed against one person. Pick them now and write them in.

| Role in the demo | Who | Write it here |
|---|---|---|
| **The showcase profile** — you fill their tabs in §2.3 | Efua Seidu, the `staff` persona, so she can also raise the portal requests in chapters 7, 8 and 14 | `TDC/________` |
| **The throwaway** — created live in chapter 4, terminated in chapter 5.9, deleted in chapter 15 | created on the day; nothing to do now | — |

Open `/hr/employees`, search `Seidu`, open the row and **copy the URL**. The `Guid` in it is what
you will paste into `?tab=` links if you want to jump straight to a tab:

```
Showcase profile URL: ______________________________________________
```

### 2.3 Fill the showcase profile — the long step

Sign in as **hr.head**, open the showcase profile, and add **one row to each of these tabs**. One
row is enough: the demo is about the shape of the record, not its volume. Suggested contents are
given so you are not inventing data at 11pm.

| # | Tab | Add | Suggested |
|---|---|---|---|
| 1 | Personal → **Addresses** | one address | Residential · Ghana → Greater Accra → Tema → Community 9 · digital address `GT-123-4567` · tick **primary** |
| 2 | Personal → **Identification** | two cards | **Ghana Card** `GHA-000123456-7`, issued 2021-04-02, no expiry · **Ghana Passport** `G1234567`, issued 2019-08-11, **expires 60 days from your demo date** — see §2.4 |
| 3 | Capability → **Qualifications** | one | BSc Building Technology · KNUST · completed 2009 · Second Class Upper · then **Mark verified** |
| 4 | Capability → **Skills** | two | *AutoCAD* — Advanced · *Project Management* — Intermediate |
| 5 | Capability → **Certifications** | one | any certification your tenant lists, with an expiry **within 60 days** so the compliance strip shows amber |
| 6 | Capability → **Work history** | one | Ghana Highway Authority · Assistant Engineer · 2010-01 → 2014-08 · reason *career progression* · reference **may be approached** |
| 7 | Employment → **Contracts** | one | contract kind *Permanent appointment* · in force from their date employed · open-ended |
| 8 | Employment → **Relievers** | one | any colleague, order 1 |
| 9 | Pay → **Bank** | one | a bank from the list · branch · account name · account number · allocation **100 %** · tick **primary** |
| 10 | Records → **Referees** | one | professional referee, with a relationship from the catalogue |
| 11 | Records → **Guarantors** | one | a guarantor with a surety amount in **GHS** |
| 12 | Records → **Documents** | two files | one **Contract** (any PDF) and one **Certificate**. This needs the scanner stub running — see §2.5 |

⚠ **Do not fill the Salary tab's grade placement yet.** Chapter 5.5 raises a salary change live,
and a person with no placement makes that story clearer, not worse.

### 2.4 Make the ID Expiry screen show something

Two settings, both one-off. Without both, chapter 9 opens onto an empty table and its own empty
state tells the room the feature raises nothing.

1. **Administration → HR → Identification Types** (`/administration/hr/identification-types`).
   Open **Ghana Passport** and set its warning time to **90 days**. Open **Ghana Driver's
   License** and set its warning time to **30 days** — two different figures, because chapter 9's
   script points at the difference. Leave Ghana Card, Voter ID and SSNIT alone: they do not expire,
   and a type with no warning time raising nothing is exactly the behaviour chapter 9 explains.
2. Back on the showcase profile's **Identification** tab, confirm the passport you added in §2.3
   expires inside that 90-day window. Add a second, **already-expired** card on a *different*
   employee (any colleague, a driver's licence that lapsed last month) so the screen shows both
   escalation tiers — *the holder* and *HR*.
3. Open `/hr/employees/identification-expiry` and confirm the **Due** tab now has at least two
   rows and the counts above it are non-zero. Write them down:

```
Due: ______   approaching renewal: ______   already lapsed: ______
```

### 2.5 The scanner stub

Every HR upload is scan-mandatory and a tenant administrator cannot turn it off. Without a scanner
answering, **every file upload in this demo returns 422** — the photograph, the documents, the
evidence on a change request, the signed letter, the policy PDF, the announcement attachment.

```
powershell -File .\scripts\Start-Demo.ps1
```

brings the stub up with everything else. If you are starting pieces by hand,
`scripts\Start-DemoVirusScanner.ps1` is the one you need, on port 3310.

> **What to say if somebody asks whether files are being scanned:** "Every upload goes through one
> controlled gate that scans, types and audits the file, and a file with no clean result is
> refused. On this laptop the scanner is a local stub so we can show you the flow."
> Do **not** say files are being scanned here. They are not.

### 2.6 Build the queues you will approve

These three are the ones that make the demo an end-to-end story rather than a tour. Sign in as
**staff** in a second window and raise them now — the demo *approves* them, it does not raise them
(except chapter 14, which raises one live to show both sides at once).

| # | As | Do | So that |
|---|---|---|---|
| 1 | **staff** | `/me/profile` → **Bank & statutory** → **Request a change** on **Bank account number**. New value `0123456789012`, reason *"I have changed banks — letter attached"*, attach any PDF | Chapter 7 has a pending request with evidence to approve |
| 2 | **staff** | `/me/profile` → **Personal** → **Request a change** on **Surname**. New value anything, reason left deliberately thin — *"please change it"* — and **no evidence** | Chapter 7 has a second request worth **refusing**, which is the more interesting half |
| 3 | **staff** | `/me/letters` → **Request a letter** → *Employment and salary confirmation* → purpose *"a mortgage application with Ghana Commercial Bank"* | Chapter 8 has a letter to preview and issue |

### 2.7 Seed the announcement and the policy

Chapter 13 publishes a notice live and chapter 14 publishes a policy live, so you need **one of
each already published** to have a list to show first.

| As | Do |
|---|---|
| **hr.head** | `/hr/announcements` → **New announcement** → title *"Office closure — Founders' Day"*, category **Event**, a one-line summary, a short body, audience **Everyone** → save → **Publish** |
| **hr.head** | `/hr/policies` → **New policy** → title *"Code of Conduct"*, version `v1.0`, category anything, **tick signature required**, audience **Everyone** → save → **Attach document** (any PDF) → **Publish** |
| **staff** | `/me/policies` → open *Code of Conduct* → **sign it**, so chapter 14's compliance drawer shows one signature against a real audience rather than zeros |

### 2.8 The import file

Chapter 10 commits a real import. The workbook is made by a script against the running API:

```
cd "D:\Rhema\TDC ERPS\dev-harness\hr-demo-smoke"
node make-employee-import-file.mjs
```

It writes `out\demo-employee-import.xlsx` — six rows numbered `DEMO-001` … `DEMO-006`: one clean,
three with warnings, two with errors. Confirm the file exists and note its path:

```
Import file: ______________________________________________
```

### 2.9 Pre-open every screen

The web app compiles a route the first time it is opened. On a production build that is quick, but
the *first* open of a heavy screen still costs a second or two, and the organogram fetches about
2.3 MB before it draws anything. Open these now, one at a time, waiting for each to paint, then
leave the tabs open.

**Window A — hr.head** (your main window, where you spend most of the demo):

`/hr/employees` · `/hr/employees/new` · the showcase profile · `/hr/employees/change-requests` ·
`/hr/employees/letter-requests` · `/hr/employees/identification-expiry` · `/hr/employees/import` ·
`/hr/employees/payroll-reconciliation` · `/hr/organogram` · `/hr/announcements` · `/hr/policies` ·
`/administration/hr/settings/staff-numbering`

On the showcase profile, **click through all eight groups in the rail** so each tab's query is
warm. This is the single most valuable minute of prep: the profile is the screen the room stares
at longest.

**Window B — staff** (Efua Seidu): `/me/profile` · `/me/letters` · `/me/policies` ·
`/me/profile/change-requests`

**Window C — admin** (`Admin123!`): `/hr/employees` — held in reserve for chapter 5.9, where
`hr.head` is refused.

### 2.10 The numbers to write in

Read these off the screen tonight and write them here. A demonstrator who quotes a number that has
moved loses the room for a minute, and these move whenever another team seeds data.

```
Employees in the register (bottom of the list)       : ____________
Of those, "Not on payroll"                           : ____________
Organogram → Units → "Units" tile                    : ____________
Organogram → Units → "Staff placed"                  : ____________
Organogram → Positions → "Vacant posts"              : ____________
Payroll reconciliation → "On payroll (HR)"           : ____________
Payroll reconciliation → any non-zero issue tile     : ____________
Next staff number (Administration → Staff Numbering) : ____________
```

### 2.11 Prep checklist

```
[ ] 2.2  showcase employee chosen, URL written down
[ ] 2.3  twelve tabs filled on the showcase profile
[ ] 2.4  two identification types given a warning time; ID expiry shows rows
[ ] 2.5  scanner stub running; one test upload succeeded
[ ] 2.6  two change requests and one letter request pending
[ ] 2.7  one announcement published, one policy published and signed once
[ ] 2.8  demo-employee-import.xlsx built
[ ] 2.9  every screen pre-opened in the right window
[ ] 2.10 the numbers written in
[ ] Window A back on /hr/employees — your opening screen
```

⚠ **Do not refresh the browser during the demo unless a step tells you to.** Every screen fetches
on open; a refresh costs you the pause and gains you nothing.

---

## 3. `/hr/employees` — the register

### 📍 Where you are

**Sidebar:** Human Resources → **Employees** → **Employees** · `/hr/employees` · as **hr.head** ·
**4 minutes**

### 📖 What it is

Every person the organisation employs, once. One row each, whatever their contract, whatever their
status, whether or not payroll pays them. This is the screen the rest of HR reaches through — if a
person is not here, they are nowhere.

### 👁 On the page

**Header.** Title *Employees*, subtitle *Manage your organization's employee records*, and two
buttons on the right:

| Button | Goes to |
|---|---|
| **Payroll reconciliation** *(outline)* | `/hr/employees/payroll-reconciliation` — chapter 11 |
| **New Employee** *(solid)* | `/hr/employees/new` — chapter 4 |

**Card header.** Title *Employee List*, and on the right:

| Control | Values | Notes |
|---|---|---|
| Payroll filter *(dropdown, 176 px)* | **All staff** · **On payroll** · **Not on payroll** | resets to page 1 on change |
| Search box *(288 px, magnifier icon)* | placeholder *Search by name, number, email…* | **debounced 400 ms**; resets to page 1 |

**The table.** Five columns:

| Column | Shows |
|---|---|
| **Employee** | full name in bold; beneath it, the staff number and, if present, ` · ` and the email |
| **Position** | position title, or `—` |
| **Organization Unit** | unit name, or `—` |
| **Status** | the `StaffStatus` badge, plus a grey **Not on payroll** pill where that applies |
| *(unlabelled, 70 px)* | the `⋯` row menu |

**The row itself is a link** — clicking anywhere but the `⋯` opens the profile.

**The `⋯` menu.** Header *Actions*, then:

| Item | Does |
|---|---|
| **View** | opens the profile |
| **Edit** | opens the edit form |
| *(separator)* | |
| **Deactivate** *(or* **Activate** *if inactive)* | confirmation dialog, then the lifecycle call |
| **Delete** *(red)* | confirmation dialog, then a soft delete |

**Paging.** 20 rows a page. **Previous** / **Next** with *Page N of M* between them, shown only
when there is more than one page.

**Empty states.** With a search term: *No matching employees — Try a different search.* Without
one: *No employees yet — Add your first employee*, with a **New Employee** button.

### ▶ Walk it

**1 — Read the shape of the screen before touching it.**

> "Every person in the Corporation is here once. The staff number is issued by the system — TDC,
> slash, a five-digit counter — and it never changes, even when the person moves from contract to
> permanent, changes department, or is promoted three times. It is the handle everything else in
> HR holds them by."

Point at the count at the bottom of the list. Say the number you wrote in §2.10.

**2 — The payroll filter, which is the first surprising thing on the screen.**

Set it to **Not on payroll**. A short list appears, each row carrying a grey pill.

> "These people are employees and they are not paid through the payroll run. National service
> personnel on an allowance, a consultant who invoices us, somebody seconded from a parent
> organisation. They have a staff number, a position, a manager, leave, appraisals, assets and an
> ID card — everything except a payslip. Most systems make you choose between putting them in the
> register with a fictional salary or leaving them out of it altogether. Neither is true, so we
> made it a fact about the person."

Set the filter back to **All staff**.

**3 — Search.**

Type `Seidu`. One row: your showcase employee, with staff number, position and unit.

> "Search reads the name, the staff number and the email address. It waits until you stop typing
> rather than firing on every keystroke — which matters on a register of this size."

**4 — Open the row.** Click it. You are now in chapter 5. *(If you are running the short path, go
to chapter 16 instead.)*

⚠ **CAREFUL — do not open the `⋯` menu in front of the room yet.** Two of its four items are
`HR.Employee.Admin`, which `hr.head` does not hold, and a red **Delete** sitting open on screen
invites a question you would rather answer in chapter 5.9 on your own terms.

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| The list | `POST /api/hr/Employees/paged?page=&pageSize=` | `EmployeeService.GetEmployeesPagedAsync` | **not gated** — the summary DTO carries no DOB, pay or identifiers |
| Deactivate | `POST /api/hr/Employees/{id}/deactivate` | `EmployeeService` | `HR.Employee.Admin` |
| Activate | `POST /api/hr/Employees/{id}/activate` | | `HR.Employee.Admin` |
| Delete | `DELETE /api/hr/Employees/{id}` | | `HR.Employee.Admin` |

Rows come from `Employees`, ordered by **`LastName` then `FirstName`, ascending**. There is no sort
control and no column header is clickable.

The list read is deliberately open to any internal user: the same paged endpoint feeds the shared
employee picker used by every module in the product, and gating it would break pickers across
Finance, Procurement and Maintenance. The line is drawn at the **detail** read — `{id}/details`
and `{id}/profile` carry salary, tax and social-security numbers, home address and bank data, and
those are `HR.Employee.Read`.

### ⚠ Known gaps

| | |
|---|---|
| **E-1 · Two filters on a screen whose API offers fifteen.** `EmployeeSearchDto` accepts `DepartmentId`, `SectionId`, `PositionId`, `StaffStatus`, `EmploymentType`, `IsActive`, `IsFullTime`, `HiredAfter`, `HiredBefore`, `MinYearsOfService`, `MaxYearsOfService` and more. The register offers a search box and a payroll toggle. "Show me everyone on contract in Development" cannot be asked from this screen. | |
| **E-2 · No sorting.** The order is surname ascending, fixed. A register cannot be read by staff number, by date employed or by unit. | |
| **E-3 · No export.** There is no way to get the register out of this screen. |
| **E-4 · Book 1 §1 is stale about this screen** and should be corrected: it claims "filters by unit and status" (there are none) and that "sorting by staff number puts TDC/00001 at the top" (there is no sort, and the default is by surname). |

---

## 4. `/hr/employees/new` — creating an employee

### 📍 Where you are

**From:** `/hr/employees` → **New Employee** · `/hr/employees/new` · as **hr.head** · **7 minutes**

### 📖 What it is

One form, four sections, that creates an employee and enrols them in payroll. The interesting part
is how much of it the form refuses to let you decide — because the establishment has already
decided it.

### 👁 On the page

Header *New Employee*, subtitle *Create an employee record, or record one who already has a staff
number*, a back arrow to the register. Then one card, **Employee Details**, with four sections.

#### Section 1 — Identity & Personal

| Field | Type | Notes |
|---|---|---|
| First Name | text | **required**, max 100 |
| Middle Name | text | |
| Last Name | text | **required**, max 100 |
| **Employee Number** | text | see the numbering hint below — may be **disabled** |
| ↳ *checkbox* | **This person already has a staff number (recording an existing employee, not a new hire)** | changes the submit button to **Record Employee** and the endpoint to the import door |
| Title | text | placeholder *Mr / Ms / Dr* |
| Gender | select | Male · Female · Other · Prefer not to say · *Not set* |
| ↳ Describe gender | text | appears only when Gender is *Other* |
| Date of Birth | date | |
| Marital Status | select | Single · Married · Divorced · Widowed · Separated · Other |
| Blood Type | select | A+ A− B+ B− AB+ AB− O+ O− Unknown |
| Religion | text | |
| Hometown | text | |
| **Has a disability** | checkbox | opens two more fields |
| ↳ Disability type | select | from the live catalogue, plus *Not specified* |
| ↳ Notes | textarea | *"In their words where possible — the accommodation needed, or a condition the list does not name"* |
| Expatriate | switch | *(rendered with Full-time in the Employment section)* |

**The Employee Number hint changes with the register.** This is the part worth showing:

| Situation | Hint |
|---|---|
| Rule is set to auto-generate | *Issued automatically from "NAME", e.g. TDC/00104. A number cannot be supplied while that rule is on.* — **and the box is disabled** |
| Rule exists but is manual | *Required — "NAME" is set to be entered by hand.* |
| No rule for this employment type | *Required — no numbering rule is configured for permanent staff, so the system does not issue one.* |
| Import checkbox ticked | *Required — the number this employee already has. It is kept exactly as given, and the register's counter is moved past it so the next hire does not collide with it.* |

The rule is resolved the same way the server resolves it: the **active** rule for this employment
type first, then the **active** tenant default. Change **Employment Type** and the hint changes
under you.

#### Section 2 — Contact

| Field | Notes |
|---|---|
| Email | optional since 2026-09-03 — a register never has an address for everyone — but format-checked when given |
| Mobile | |
| Telephone | |
| **Country** | drives everything below it |
| **The address cascade** | the dropdown *labels* come from the country's geography scheme — for Ghana: **Region → District → Town → Community** |
| Address | free text, the street line |
| City / Town | **read-only when a scheme is loaded**, with the hint *Set from the address above* |
| State / Region | same |
| Postal Code | |
| Digital Address | |

#### Section 3 — Employment

| Field | Type | Notes |
|---|---|---|
| **Position** | select, sorted by title | **required** — and it decides the next two |
| Organization Level | **disabled** | derived from the position |
| Organization Unit | **disabled** | derived from the position; the server enforces the match |
| Location level | select | a cascade step, not submitted |
| Location | select | **required**, filtered to the chosen level; placeholder *Select a level first* until one is picked |
| **Manager** | employee picker | free search, plus the suggestion panel below |
| ↳ *suggestion panel* | appears when the position has a `ReportsToPositionId` | *"This position reports to **Head of Development**."* then a button per person holding that post — or, if it is vacant, the amber line *"Nobody currently holds that position, so there is no one to suggest."* |
| Employment Type | select | the nine values in §1.3 |
| Staff Status | select | the seven values in §1.3 |
| Date Employed | date | |
| **Contract kind** | select, **create only** | TDC's own vocabulary, each shown with its duration — *"Permanent appointment — open-ended"*, *"Fixed term — 24 months"*. Hint: *What the appointment letter calls this engagement. Its duration dates the first contract.* |
| **Confirmation date** | date, **import mode only** | *Only for staff already confirmed before this system. Leave blank if they are still on probation.* |
| **Probation (days)** | number | **read-only and disabled when the position states a term** |
| ↳ *hint, position states a term* | *"6 month(s), from the position — expected confirmation 2027-03-16. Change it on the position, not here."* |
| ↳ *hint, position silent* | *"This position states no probation period, so the company default of 3 month(s) applies. Change it here only for this employee."* |
| Full-time | switch | default on |
| Expatriate | switch | default off — ticking it is what makes the Expatriate tab appear on the profile |

#### Section 4 — Compensation & Tax

| Field | Notes |
|---|---|
| **On payroll — paid through the payroll run** | switch, default **on** |
| ↳ *when on* | a grey line: *The basic salary, payroll switches and grade placement are recorded on the Salary tab after saving. Until then the payroll reconciliation lists this person as having no pay basis.* |
| ↳ *when off* | **How are they paid instead?** *(required — the form will not submit without it)* and **Note (who pays, under what arrangement)**, max 500 |
| Tax Number | |
| SSNIT Number | |
| TIN | |
| Notes | textarea, 3 rows |

**Footer.** **Cancel** and **Create Employee** *(or* **Record Employee** *in import mode)*.

### ▶ Walk it

**1 — Leave the staff number alone and say why.**

> "Notice I am not typing a staff number. Look at the hint under the box — the register tells me
> what it is going to do. On this tenant permanent staff are numbered automatically, so the field
> is greyed out entirely: I *cannot* supply one. That is not a nicety. If I could type a number, I
> could type one the counter is about to issue, and two people would end up sharing it a fortnight
> later on a screen that has nothing to do with either of them."

Fill: first name **Kwabena**, last name **Ofori**, title **Mr**, gender **Male**, date of birth
any, marital status **Single**.

**2 — The disability tick.** Tick **Has a disability**, let the two fields appear, then untick it.

> "Recorded from a catalogue rather than as free text, so it can be reported on — and with a notes
> box beside it, in the employee's own words where possible, because a catalogue never covers
> everybody."

**3 — The address cascade.** Choose country **Ghana**, then work down: **Greater Accra → Tema
Metropolitan → Tema → Community 9**.

> "Watch the labels on the dropdowns. Region, District, Town, Community — those are Ghana's
> administrative tiers, and they come from the country, not from the form. Pick a different
> country and you get that country's tiers. There is no Ghana-specific code anywhere in this
> screen."

Point at **City** and **Region**, now greyed.

> "And these two have gone read-only, because the address above already answers them. A box you
> can type in that the server then overwrites is worse than no box at all."

**4 — The establishment doing the work.** This is the moment of the chapter. Choose position
**Administrative Officer**.

Three things happen at once. Say them as they happen:

> "Organisation level — filled in. Organisation unit — filled in, and greyed, because an employee
> sits in their position's unit and nowhere else; the server enforces it. And underneath the
> manager box: *this position reports to the Head of Administration*, with the name of whoever
> currently holds that post, as a button."

Click the suggested manager.

> "That reporting line is on the establishment. It is what draws the organogram. Before this
> panel existed, this box was a free search over every employee in the register — so the org chart
> and the manager on the employee record could disagree, for years, with nothing to notice it.
> It is still a free search, because acting arrangements and matrix reporting are real; it is now
> a free search with the right answer already offered."

**5 — Probation, and where numbers come from.** Point at **Probation (days)** — read-only, with
its hint naming the position and the expected confirmation date.

> "Ninety days is nobody's probation at TDC. Junior posts run three months, senior and management
> six, and the term is maintained on a hundred and twenty-three of a hundred and forty-six posts.
> This field used to be a free number defaulting to ninety, which meant every single hire quietly
> overwrote a maintained figure with a form default. Now it shows what the post says, it will not
> let me change it, and it tells me the confirmation date that follows — before I save, not a
> quarter later."

**6 — The payroll switch.** Flick **On payroll** off. The reason dropdown and note appear. Flick
it back on.

> "That switch decides whether Payroll is told about this person at all. Off, and it asks how they
> are paid instead, and will not let me save until I answer. On, and look what it says: the salary
> is not on this form. It is on the Salary tab, after saving."

Pause on that.

> "That is deliberate and it was a change we made after the last demonstration. Pay is not an
> attribute you type once while filling in somebody's date of birth. It is a decision with a
> basis, a grade placement, an effective date and — as we will see — an approval. Putting a salary
> box on the hiring form invited people to treat it as a detail."

**7 — 🔴 LIVE WRITE 1. Save.** Click **Create Employee**.

**Expect:** a green toast *Success — Employee created*, and the browser returns to
`/hr/employees`. Search for **Ofori** and open the record.

> "There is the number the register issued. And look at the status — **Probation** — set
> automatically from the position's probation period. Nobody chose that."

⚠ **CAREFUL — the import checkbox.** Do not tick it live. `POST /api/hr/Employees/import` is
gated on **`HR.Employee.Admin`**, which `hr.head` does not hold, so ticking it and saving gives
you a 403 in front of the room. Describe it instead:

> "There is a second door here, for recording somebody who already has a staff number — a person
> transferring in from another entity, or a register being loaded by hand. That path keeps the
> number they came with and moves our counter past it, so the next real hire is not handed a
> number somebody is already using. It is a different privilege from adding a new hire, and it is
> granted separately."

### ⚙ Behind the page

| Action | Call | Gate |
|---|---|---|
| Create | `POST /api/hr/Employees` | `HR.Employee.Write` |
| Record existing | `POST /api/hr/Employees/import` | **`HR.Employee.Admin`** |
| Position list | `GET /api/hr/EmployeePositions/active` | not gated |
| Numbering rules | `GET` staff number formats | not gated |
| Probation default | `GET` HR policy settings | not gated |
| Contract kinds | `GET` contract types (active) | not gated |
| Disability catalogue | `GET` disability types (active) | not gated |

**The probation value is not always sent.** When the position states a term, the form holds the
position's figure on screen but sends `null`, because the server settles the term against the post
and **refuses a value that contradicts it**. The `probationIsDerived` flag on the submit handler
is what carries that decision from the form to the mapper.

**Where the derived fields land.** `Employees.OrganizationUnitId` is set from the position and
validated server-side. `Employees.ProbationPeriodDays` is settled by the service, and
`ProbationSource` records which of *Position* / *PolicyDefault* / *Override* it came from — that
is the *"— from the position"* suffix you see on the profile.

**City and State are snapshots.** When `GeoAreaId` is set, the server overwrites `City` and
`State` from the geography tree. What is typed in those boxes survives only for a country with no
scheme loaded.

### ⚠ Known gaps

| | |
|---|---|
| **E-5 · The import checkbox is on a form its own persona cannot submit.** The sidebar gates the bulk import wizard on Write and the HR role holds it, but the single-record import door beside it is Admin. An HR officer sees the checkbox, ticks it, fills the form and is refused on save. The gate is correct and deliberate; the affordance should match it, the way the Documents tab hides its delete button from non-admins. |
| **E-6 · Confirmation date is shown on edit and never sent.** The edit form binds `confirmationDate` from the record, renders nothing for it, and drops it — correct, since the server refuses a confirmation date from the ordinary edit, but it means the field round-trips invisibly. |

---

## 5. `/hr/employees/[id]` — the profile

### 📍 Where you are

**From:** any row in the register · `/hr/employees/[id]` · as **hr.head** · **20 minutes** for the
whole chapter, **8** for the short path

### 📖 What it is

Everything known about one person, in eight groups. Thirty-five tabs: seventeen are maintained
right here, sixteen are windows onto another HR module, one — Teams — belongs to Administration,
and the Overview is the record itself, edited through the Edit form. It is the screen the room
will stare at longest, so it is worth slowing down on the frame before opening any tab.

### 👁 On the page — the frame

**Header.** The person's full name as the title; beneath it the staff number and, if present,
` · ` and the position title. A back arrow to the register. On the right, in order:

| Control | Behaviour |
|---|---|
| **The photograph** | a round avatar, clickable. Tooltip *View or replace the photograph*, or *Add a photograph* when there is none. Opens a dialog described as *Shown on the profile, the ID card and the organogram* |
| **Status badge** | the `StaffStatus` |
| **Edit** *(outline, pencil)* | `/hr/employees/[id]/edit` |
| **Deactivate** *or* **Activate** *(outline)* | confirmation dialog — hidden when terminated |
| **Terminate** *(red)* | a dialog with **Termination Date** and a **Reason** textarea; the reason is required client-side |
| **Reinstate** *(outline)* | replaces the three above when the status is `Terminated` |

**Below the header, the body splits in two:** the tab rail on the left (224 px, wide screens only)
and the tab content on the right. Below `lg` the rail becomes a native grouped dropdown with an
`optgroup` per group — worth showing if anybody asks about phones.

**The rail** is a real tab list: arrow keys move between tabs and the active one is announced. Each
group carries a small uppercase heading. **One tab can carry a number** — Discipline shows an amber
count of open cases, because that is the one count cheap enough to fetch without loading the tab.

### ▶ Walk the frame

**1 — Say what you are looking at before opening anything.**

> "One person. Eight groups down the left. Thirty-five sections behind them. Before I open any of
> them, the thing to notice is the grouping — Personal, Employment, Pay and benefits, Capability,
> Performance and conduct, Time and leave, Welfare and travel, Records. At the last demonstration
> this was nineteen tabs in one strip that ran off the side of the screen, and the feedback was
> that nobody could find anything. These are eight headings the eye can scan."

**2 — The photograph.** Click the avatar. The dialog opens.

> "One photograph, one place, three uses — this profile, the ID card and the organogram. And it
> goes through the same controlled upload gate as every other file in HR: scanned, typed,
> versioned and audited. There is no field anywhere in this system where you type a path to a file
> on somebody's desktop."

Close the dialog without uploading.

**3 — The Discipline count, if there is one.** If your showcase employee has an open disciplinary
case, the rail shows an amber number beside **Discipline**.

> "That number is the count of open disciplinary cases, and it is the only number on this rail.
> Everything else would cost a full list read to count — which would mean loading twenty-eight
> tabs to draw a menu. A tab's data is fetched when you open it, not before."

### 5.1 Personal group

#### Overview *(the default tab, `?tab=overview`)*

**Six cards, in order.** A certification compliance strip appears above them when there is
anything to say.

| Card | Rows |
|---|---|
| *(compliance strip)* | one badge per certification the position requires — **held** / **expires in Nd** / **expired** / **revoked** / **not held**. Renders nothing at all when the position requires nothing and nothing is expiring |
| **Personal** | Title · Full Name · Gender · Date of Birth · Marital Status · Religion · Blood Type · Expatriate · **Disability** *(the catalogue row and the notes joined with an em dash, or* None recorded*)* |
| **Contact** | Email · Mobile · Telephone · Address · City · Digital Address |
| **Employment** | Position · Organization Unit · Organization Level · Location · Manager · Employment Type · Staff Status · Date Employed · **Probation (days)** *with its source* · Full-time · On Probation · **Expected Confirmation** *(shown only while unconfirmed)* · **Confirmed On** |
| **Compensation & Tax** | **Payroll** *(green* On payroll *or grey* Not on payroll *pill)* · **In Payroll module** *(Set up · active / Set up · switched off / Not set up)* · **Attention** *(amber, only when the two disagree)* · then either **Monthly basic pay** *(on payroll)* or **Paid instead by** + **Arrangement** *(off payroll)* · Tax Number · SSNIT Number · TIN |
| **Termination** | Date · Reason · Notes — **only when the status is Terminated** |

**▶ Walk the Overview**

**1 — The probation row.** Read the suffix off the screen — it says *from the position*, *the
company default*, or *set for this employee*.

> "Probation — and beside the number, where it came from. *From the position.* Not a form default,
> not somebody's memory. A number with no provenance was the single most common complaint about the
> last build, so every derived figure on this screen now says where it came from."

**2 — The two confirmation rows.**

> "And beside it, two different facts kept apart on purpose. **Expected confirmation** is when
> probation is due to end. **Confirmed on** is when somebody actually signed it off. The second is
> read-only here and everywhere — it is written by confirming the probation record, which is the
> act that issues the letter. You cannot type a confirmation date onto an employee. If you could,
> the letter and the record would disagree."

**3 — The compensation card, and the point of the whole chapter.**

> "Two answers, side by side, from two different owners. **HR says** this person is paid through
> the run. **Payroll has** a profile for them, and it is active. When those two disagree — and
> they do disagree, in every organisation, every month — the mismatch is named here in amber and
> listed on a reconciliation screen we will open in a few minutes."

If the **Attention** row is showing, read it out. If it is not:

> "On this record they agree, so there is nothing in amber. I will show you the screen that finds
> the ones that do not."

**4 — The basic pay row.** Point at it.

> "One figure, and underneath it in the Salary tab, its source in words — which notch on which
> grade, or Payroll's own basis where the pay was negotiated. This record's own flat salary field
> is the fallback, not the source."

#### Addresses *(`?tab=contacts`)*

**Columns:** Type · Address · City · Region · Digital address · **Primary**.
**Row actions:** Edit · **Set as primary** · Delete.
**The form** carries the same country → region → district → town → community cascade as the create
form, with the same read-only City and Region under a loaded scheme.

> "The address on the Overview is the main one. This is the register behind it — a residential
> address, a postal address, a home-town address. One of them is primary and that is the one the
> record quotes."

#### Emergency contacts *(`?tab=emergency`)*

**Columns:** Name · Relationship · Type · Phone · Email · Status · **Primary**.
**Row actions:** Edit · Set as primary · Activate/Deactivate · Delete.

⚠ The relationship dropdown offers **familial and other relationships only** — deliberately. A
next of kin is not a former line manager, and a dropdown that offers one invites the mistake.
**When a relationship is chosen from the catalogue the server overwrites the free-text field with
the catalogue row's name**, so the two can never drift.

#### Dependents *(`?tab=dependents`)*

**Columns:** Name · Relationship · Date of birth · **Age** *(computed)* · Gender · Status.
**Row actions:** Edit · **Benefits…** · **Photograph…** / **Add photograph…** · Delete.

> "Recorded for benefit eligibility, which is why each row has a **Benefits** button. That opens
> the dependent's own enrolments — which of the company's benefit policies cover this child, from
> when, and what is left of the limit."

Open **Benefits…** on one row if your demo database has medical data seeded, which it will.

#### Identification *(`?tab=identification`)*

**Columns:** Type · Number *(masked)* · Issued · **Expires** · Issuing authority · Status.
**Row actions:** Edit · **Mark verified** / **Mark unverified** · Delete.

> "Ghana Card, passport, driver's licence, SSNIT card. The number is masked in the list — it is
> shown in full only when you open the row. Each one can be marked verified, by name and date, so
> 'we have seen the original' is a fact on the record rather than an assumption."

Point at the passport you set up in §2.4.

> "And this one expires inside ninety days. Hold that thought — there is a screen in this menu
> that does nothing but find these, and we will open it shortly."

#### Expatriate *(`?tab=expatriate`, conditional)*

**Appears only when the employee is flagged expatriate.** Columns: Home country · From · To ·
Visa · Visa expiry · **Residence permit** · **Work permit expiry** · **Family**.

The **Family** column opens a panel of accompanying family members — their own records, on their
own endpoints, hanging off the assignment row rather than the form.

⚠ The **work permit** and the **residence permit** are shown side by side deliberately: they are
different instruments, issued by different authorities, on different clocks. Every permit carries
both an issue date and an expiry, because before that a record could not answer *"when was this
one granted?"*.

### 5.2 Employment group

#### Contracts *(`?tab=contracts`)*

**Columns:** Contract *(number)* · Type · **In force from** · Until · **Salary (as recorded)** ·
Status.
**Row actions:** Edit · Activate/Deactivate · **Terminate** · Delete.

Three things to point out:

> "**In force from** is the effective date, not the date somebody typed the row in. Adding a
> contract closes the one in force the day before — supersession is automatic, so there is never a
> gap and never an overlap."

> "The salary column says **as recorded** and that wording is doing work. It is the figure this
> contract was written with, kept as history. It is not what the person is paid — that lives on
> the Salary tab, and the two are allowed to differ, because a pay review does not rewrite an old
> contract."

> "And there are no leave figures on a contract. Entitlement is the leave module's answer, and it
> changes with service length and policy; freezing it onto a contract would mean two answers to
> one question."

#### Position history *(`?tab=position-history`)*

**Columns:** Position · **Reason** · From · To · **Current**.

> "Every post this person has held, why they moved, and when. The reason comes from a maintained
> list of reason codes, not free text — so 'how many people left Development for Estate last year,
> and why' is a question with an answer."

#### Movements · Probation · Separation *(read-only)*

Three summaries over the modules that own them. Each has a **table**, an **Open in …** button top
right, and **no write control at all**.

| Tab | Columns | Door |
|---|---|---|
| **Movements** | Movement *(number)* · Type *(+ · temporary)* · **From → to** *(position, with the unit beneath when it changes)* · Effective · Status | **Open in Movements** |
| **Probation** | Period *(start — current end)* · Months · **Extensions** · **Reviews** · Status | **Open in Probation** |
| **Separation** | Separation *(number)* · Type · Initiated · **Last working day** · Status | **Open in Separations** |

> "Promotions, transfers, acting appointments, secondments — all here, newest first, and all
> read-only. The button top right takes me to the movements screen, and that is where a movement
> is raised, approved and implemented. There is no second door onto that rule from here, and that
> is a decision rather than something we ran out of time for: a movement implements a position
> change, a salary placement and — if it is temporary — a return date. A shortcut button on this
> page would be a second implementation of all three."

Point at the **Extensions** and **Reviews** counts on Probation.

> "Two extensions and three reviews. Confirming probation is what issues the confirmation letter,
> and it happens over there, not here."

#### Teams *(`?tab=teams`)*

**Read-only, and its owner is not an HR module.** The empty state says so: *Teams are made up from
the team's own screen, under Administration › Organization › Teams.*

The column worth showing is **allocation**.

> "A person's time can be split across teams, and the percentages are the point of this tab. If
> they add up to more than a hundred, somebody has been committed twice — and this is where you
> see it."

#### Relievers *(`?tab=relievers`)*

**Columns:** **Order** · Reliever · Position · Unit · Status.

> "Who covers this person when they are away, and in what order. First choice, second choice. It
> is read when leave is approved — a leave request with no cover arranged is a different
> conversation from one with two names against it."

### 5.3 Pay & benefits group — the Salary tab

This is the densest tab in the module and it deserves its own beat. `?tab=salary` opens **six
stacked cards** — five when the pay basis is Negotiated — in this order.

#### Card 1 — Salary changes

| Element | |
|---|---|
| Header | *Salary changes* + **Raise a change** button *(when you may)* |
| The one **live** request | shown in full, with the workflow engine's own action buttons beside it — Submit / Approve / Reject / Recall — plus **Recall**, **Retry** and a **remove** for a draft |
| **History table** | Change · Effective · Status · Raised by · **Result** |

**The raise dialog:** a two-button choice — **Onto the salary scale** or **A negotiated amount** —
then either a grade → level → notch picker or an amount and currency; an effective date; and a
**reason** textarea with the placeholder *The approver reads this before the figure.*

#### Card 2 — Pay basis

Three columns: **Basis** *(a badge: Salary scale or Negotiated, with the note beneath)* ·
**Monthly basic pay** *(the figure, and under it its source in words)* · **Payroll
reconciliation** *(green* HR and Payroll agree*, or the issue in amber)*.

A **Change** button opens a radio dialog with the two bases and their descriptions.

#### Card 3 — Grade placement

**Hidden entirely when the basis is Negotiated**, because a placement contradicts a negotiated
amount and the server refuses one. The history is still there the day they return to the scale.

**Columns:** Grade · Level · Notch · Amount · Effective · Until · **Status** *(four states, not
two — a withdrawn placement used to be indistinguishable from a superseded one)*.

#### Card 4 — Payroll profile

**Payroll's own window, hosted inside HR's page — the same component Payroll → Employee Profiles
uses.** Read through HR's door, saved through Payroll's.

- When the person is **not on payroll**: an amber bar — *This employee is not on payroll, so the
  profile is shown read-only. Put them on payroll (Edit → Compensation & Tax) to maintain it.*
- When Payroll has **no profile**: *Payroll has no profile for this person yet.* plus, if you may
  write, *Fill in the salary and a payment method below and save to create one.*

#### Card 5 — Allowances & deductions

This employee's own component exceptions — the allowances and deductions that differ from the
standard set for their grade. Saved one row at a time through Payroll's bulk endpoint, which
touches only the lines it is sent.

#### Card 6 — Payroll items

Loans, advances and tax reliefs against this staff number, read from Payroll's own lists.

**▶ Walk the Salary tab**

**1 — Start at the bottom and work up, verbally.**

> "Six cards, and they are several different owners. At the bottom, Payroll's own records — loans,
> advances, allowances and deductions, and Payroll's employee profile, which is literally the same
> window Payroll uses, embedded here so an HR officer does not have to go hunting. Above it, the
> grade placement — which notch on which scale, which is HR's. Above that, the pay basis — scale
> or negotiated. And at the top, the change requests, which is how any of it moves."

**2 — The basis card.**

> "Scale or negotiated, and it is independent of everything else. A permanent employee can be
> negotiated. A consultant can be on the scale. Look at the figure in the middle, and the line
> underneath it — that is the provenance. *Grade M4, level 3, notch 6.* Not a number somebody
> typed."

**3 — 🔴 LIVE WRITE 2. Raise a salary change.** Click **Raise a change**.

Choose **Onto the salary scale**, pick a grade, level and notch one step above where they are.
Effective date: the first of next month. Reason: *"Annual review — moved one notch on the
recommendation of the Head of Development."*

Click **Raise**.

**Expect:** the live request appears at the top of the card with status **Draft** and the
engine's action buttons beside it.

**4 — Submit it.** Click **Submit**.

**Expect:** status moves to **Pending approval**.

> "And there is the thing worth watching. That request is now on the workflow engine, and the
> engine decides who approves it — not this screen. When it is approved, the server applies it:
> the placement, the pay basis, the figure on the record and Payroll's monthly basic, all four, in
> one transaction. That is why the direct fields lower down are locked on this tenant. There is
> one door to changing pay, and it has an approval on it."

⚠ **CAREFUL.** If the tenant policy `salaryChangeRequiresApproval` is **off**, the lower cards are
not locked and this speech is wrong. Check it in prep: if **Grade placement** shows an editable
**Add** button, the policy is off. Either turn it on at
`/administration/hr/settings/policy` or change the line to *"on a tenant that requires approval,
those lower fields lock and this becomes the only door."*

⚠ Do **not** approve it in the same window. `hr.head` raised it; segregation of duties is the
point. If you want the approval shown, do it from a second persona — or leave it pending and say:

> "It sits with the approver now. I am the person who raised it, so I am not the person who
> decides it."

#### Bank *(`?tab=bank`)*

**Columns:** Bank *(with a* Not in list *badge when it was typed rather than chosen)* · Branch ·
Account name · Account number · Type · **Allocation %** · Status.
**Row actions:** Edit · Set as primary · **Mark verified** / unverified · Activate/Deactivate ·
Delete.

> "More than one account, each with an allocation percentage, because people split their net pay.
> And each one can be marked verified — somebody has seen the bank letter. Remember that: in a
> moment we will look at what happens when an employee asks to *change* one of these."

#### Benefits *(`?tab=benefits`, read-only)*

**Columns:** Policy · Source · Assessed value · **Limit used** · From · Status. Door: **Open in
Benefits**.

### 5.4 Capability group

| Tab | Columns | Row actions |
|---|---|---|
| **Qualifications** | Qualification · Institution · Field of study · Completed · Grade · **Verified** | Edit · Mark verified/unverified · Delete |
| **Skills** | Skill · Category · **Level** · **Evidence** · **Certification expiry** · Status | Edit · Mark verified/unverified · Delete |
| **Certifications** | Certification · Number · Issued · **Expires** · Status · **Evidence** | Edit · **Evidence…** / Attach evidence… · Mark verified · **Revoke…** · Delete |
| **Work history** | Company · Where · Job title · From · To *(or* Present*)* · Reason for leaving · **Reference** | Edit · Delete |
| **Training** *(read-only)* | Programme · Completed · Score · Passed · **Verified** · Status | door: **Open in Training** |

**▶ The two things worth saying here**

**1 — Skills carry evidence.**

> "A skill has a level, and beside it an **evidence** column — which certification on the next tab
> proves it. An 'Advanced' that nobody can point at is an opinion; an 'Advanced' backed by a
> current credential is a fact. And if that credential is expiring, this row says so."

**2 — The certification compliance strip.** Scroll to the top of the **Certifications** tab.

> "Required by the position, against held by the person. Green means on file and current. Amber
> means it expires within sixty days. Red means expired, revoked, or never held. That strip is
> computed on the server, not worked out in the browser — and it is also on the Overview, so an
> officer opening the record sees the gap before they go looking for it."

### 5.5 Performance & conduct group *(all three read-only)*

| Tab | Columns | Door |
|---|---|---|
| **Appraisals & goals** | open goals above; then Appraisal *(number · cycle)* · Period · **Score** · **Rank in unit** · Status | **Open in Performance** |
| **Discipline** | Case *(number)* · Offence *(· severity)* · Incident · **Outcome** · Status | **Open in Discipline** |
| **Awards** | Award *(number)* · Type *(· level)* · Awarded · Amount · **Presented** | **Open in Awards** |

> "Appraisal score, and beside it rank in unit. The score is a weighted mean across three levels —
> self, peer and manager — and the weights are configuration, not code. Discipline carries an
> amber count on the rail because open cases are the one thing an HR officer opening a record
> needs to know before they do anything else."

### 5.6 Time & leave group *(read-only)*

| Tab | Above the table | Columns |
|---|---|---|
| **Leave** | this year's **balances strip** | Request *(number)* · Type *(· sub-type)* · Period · Days · Status |
| **Attendance** | | Month · **Present** *(n / total working days)* · Absent · **On leave** · Late · **Attendance %** · **Finalised** |

> "The attendance tab is the monthly roll-up — the one payroll actually reads. Days present out of
> working days, absences, leave, lateness, and whether the month has been finalised. Daily records
> and regularisations live on the attendance screens; this is the summary that leaves the module."

### 5.7 Welfare & travel group *(read-only)*

| Tab | Columns | Note |
|---|---|---|
| **Medical** | the health profile in brief, then Claim *(number, · for dependent)* · Expense *(· facility)* · Service · **Requested / approved** · Status | **clinical records stay on the medical screens, where access is narrower than this page's** |
| **Travel** | Request *(number)* · Destination *(· international)* · Dates · Estimate · Status | |
| **Assets** | Asset *(name · number)* · Type *(· benefit in kind)* · Since · **Return due** · **Acknowledged** · Status | |

> "Medical is worth a word. What you see here is the profile in brief and the expense claims —
> money, essentially. The clinical record is behind a narrower gate than this page, because an HR
> officer who can see somebody's bank details has no business reading their diagnosis. Two
> different permissions, deliberately."

> "And assets — what the company has in this person's hands, whether they acknowledged receiving
> it, and when it is due back. On the day somebody resigns, that list is the clearance checklist."

### 5.8 Records group

| Tab | Columns | Row actions |
|---|---|---|
| **Documents** | Document *(the title, with the file name and size beneath it)* · Kind · Issued · Expires · Filed by | Edit · Download · **Delete** *(Admin only — the button is hidden from non-admins)* |
| **Referees** | Name · Type · Organization · Relationship · Phone · **Letter** · Status | Edit · **Reference letter…** · Set as primary · Activate/Deactivate · Delete |
| **Guarantors** | Name · Relationship · Phone · ID type · **Files** · Status | Edit · **Files…** · Set as primary · Mark verified · Activate/Deactivate · Delete |
| **Orientation** *(read-only)* | Programme *(· session)* · Enrolled · **Progress %** · Result · Status |
| **Succession** *(read-only)* | the bench this person sits on | |

**▶ The Documents tab is the one to dwell on**

At the top sits a card titled **Required for *(position title)*** with one badge per required
document type.

**1 — Read the compliance card out**, naming the actual badges on your screen.

> "Required for a Project Coordinator — and here they are, one badge each. Green ones are on file.
> An amber one is *lapsed* — we have it, it has expired. A red one has never been filed at all.
> Those are two different conversations and the system keeps them apart; a lot of systems collapse
> both into 'missing' and lose the distinction."

**2 — The guarantor line, if the position requires one.**

> "And underneath, the guarantor requirement — how much surety the post requires, how much is
> actually pledged, how many guarantors, and the shortfall. Note the small print: guarantors
> pledged in another currency are counted separately and *not* added in, because converting them
> would make a compliance verdict move with the exchange rate."

**3 — 🔴 LIVE WRITE 3 (optional). File a document.** Click **File a document**, choose a kind,
attach a PDF, set an issue date, save.

**Expect:** a toast *Document filed*, the row appears, and if that kind was one of the red badges
above, **the badge turns green as the compliance strip refetches.**

> "Watch the badge. That is the compliance card re-reading from the server — not the browser
> ticking a box it drew itself."

⚠ Needs the scanner stub. Without it: 422, and the toast reads *Not filed*.

### 5.9 The header actions — and the one that will refuse you

**▶ Walk it**

**1 — Say what these four buttons are.**

> "Four acts at the top of this record, and they are the legacy lifecycle: deactivate, activate,
> terminate, reinstate. Deactivate is administrative — somebody who should not appear in pickers
> and should not be paid, without any statement about why. Terminate is an ending, with a date and
> a reason."

**2 — Then say what they are *not*.**

> "And I want to be careful about this one, because it is the question the room should ask.
> Terminating from here is not how somebody leaves this organisation. The governed exit path is
> the Separations module: notice, clearance across every department holding company property, an
> exit interview, a final settlement reviewed by Internal Audit, and the Managing Director's
> signature on the termination itself. This button is the blunt instrument for a record that was
> created in error, or a legacy row being tidied. They are deliberately different acts, and they
> need different permissions."

**3 — 🔴 LIVE WRITE 4. Terminate the throwaway record.** Navigate to **Kwabena Ofori**, created in
chapter 4. Click **Terminate**. Fill the date *(today)* and reason *"Created during a system
demonstration."*. Confirm.

⚠ **CAREFUL — this is where `hr.head` is refused.** Terminate is `HR.Employee.Admin`, and the HR
role does not hold it. **Expect a red toast.** That is not a failure of the demo — it is the demo.
Say:

> "And there it is — refused. I am the Head of HR in this system and I cannot terminate an
> employee record, because that permission is granted separately from maintaining one. Ending
> somebody's employment is not a thing the person who maintains the register does on their own."

Then switch to **Window C (admin)**, open the same record and terminate it there.

**Expect:** the badge changes to **Terminated**, the three buttons collapse to **Reinstate**, and
a new **Termination** card appears on the Overview with the date, reason and notes.

> "Same record, different authority. And the record now carries the ending — date, reason, and who
> did it — as part of itself."

**4 — Reinstate, to show it is reversible.** Click **Reinstate** in the admin window.

**Expect:** the status returns and the Termination card disappears from the Overview.

### ⚙ Behind the profile

| Read | Call | Gate |
|---|---|---|
| The record | `GET /api/hr/Employees/{id}/details` | `HR.Employee.Read` |
| Manager's name | `GET /api/hr/Employees/{managerId}` | not gated *(summary DTO)* |
| Payroll status | `GET /api/hr/Employees/{id}/payroll-status` | `HR.Employee.Read` |
| Open discipline count | discipline service, by employee | `HR.Discipline.Read` — a 403 is simply no badge |
| Every sub-record list | `GET /api/hr/Employees/{id}/{resource}` | `HR.Employee.Read` |

| Write | Gate |
|---|---|
| Add / edit any sub-record | `HR.Employee.Write` |
| Verify a qualification, skill, ID, guarantor or bank account | `HR.Employee.Write` |
| **Delete** any sub-record | `HR.Employee.Admin` |
| Deactivate / Activate / Terminate / Reinstate | `HR.Employee.Admin` |
| Salary tab writes | `HR.Compensation.Write` |
| Documents delete | `HR.Employee.Admin` — **the button is hidden when you lack it**, deliberately: a desk offered a button that 403s learns nothing from it |

**The `?tab=` deep link.** `resolveProfileTab` accepts a tab key only if the table knows it *and*
it is visible for this employee; anything else falls back to the Overview. Tab changes use
`router.replace`, not `push` — switching tabs is not worth a history entry each, but the URL is
always shareable.

### ⚠ Known gaps

| | |
|---|---|
| **E-7 · Retirement date is not on the profile.** `Employees.RetirementDate` exists and is read by the separation reminders screen and the succession candidate search, but no card on this page shows it. A profile that shows date of birth, date employed and confirmation date, and cannot answer *"when do they retire?"*, is missing the one date HR planning needs most. |
| **E-8 · Lifecycle buttons are shown to personas that cannot use them.** Deactivate, Activate, Terminate and Reinstate are rendered unconditionally and gated only on the server. The Documents tab already models the fix — it checks the permission and hides the delete. |
| **E-9 · Teams allocation is not summed.** The tab shows a percentage per team and never totals them, so the over-allocation it exists to reveal still has to be added up by eye. |

---

## 6. `/hr/employees/[id]/edit`

### 📍 Where you are

**From:** the profile → **Edit** · `/hr/employees/[id]/edit` · as **hr.head** · **2 minutes**

### 📖 What it is

The create form again, bound to an existing record, with three deliberate differences.

### 👁 On the page

Identical to chapter 4 with these exceptions:

| Difference | Why |
|---|---|
| **Employee Number carries no hint at all** | the number already exists and the numbering rule has nothing to say about it. A hint here would be a claim that is false |
| **No import checkbox** | recording an existing employee is a create-time act |
| **No Contract kind field** | the kind belongs to the *contract*, and is edited on the Contracts tab. Blank here so an edit cannot silently retype the first contract's kind |
| **No Confirmation date field** | the server refuses a confirmation-date change from this path; only the import door may record a pre-system confirmation |
| Submit button reads **Save Changes** | |

The manager picker and the location cascade are both **seeded from the saved record** — the
manager's name is fetched to label the picker, and the location level is read from the employee's
saved location so the cascade opens on the right rung.

### ▶ Walk it

**1 — Open it and say what has gone.**

> "Same form, one record. Notice what is *not* here. There is no contract kind, because that
> belongs to the contract and is edited on the contract. There is no confirmation date, because
> confirming somebody is an act with a letter behind it, not a field. The server refuses both from
> this door even if something sent them."

**2 — Change the mobile number, save.**

**Expect:** toast *Employee updated*, and you land back on the profile.

**3 — Then make the contrast that sets up chapter 7.**

> "That is HR editing the record. Now — the employee themselves cannot do that. Not this form, not
> any form. Let me show you what they get instead."

### ⚙ Behind the page

`PUT /api/hr/Employees/{id}` · `HR.Employee.Write` · writes `Employees`.

⚠ The form binds to **`/details`**, never the summary read. `GET /api/hr/Employees/{id}` is
`GetEmployeeSummaryByIdAsync` and carries none of gender description, hometown, disability, blood
type or the statutory numbers — binding the form to it would render every one of those blank and
then **blank them on save**.

---

## 7. `/hr/employees/change-requests` — corrections staff have asked for

### 📍 Where you are

**Sidebar:** Human Resources → Employees → **Change Requests** ·
`/hr/employees/change-requests` · as **hr.head**, with **Window B (staff)** for the other side ·
**6 minutes**

### 📖 What it is

Staff cannot edit their own record. They ask, with a reason and a document, and HR decides.
Approving does not send a message to somebody — **it writes the value onto the employee record in
the same transaction**, re-running the same uniqueness rules the edit screen enforces.

### 👁 On the page

Header *Personal Data Change Requests*, subtitle *Corrections employees have asked for to their
name, address, statutory numbers or bank details. Approving writes the change onto the employee
record.*, back arrow to the register.

**Two tabs:** **Waiting on HR** *(the default)* · **All**.

**One card per request**, each carrying:

| Element | |
|---|---|
| Top row | employee name · staff number *(mono)* · **status badge** *(amber Pending / green Approved / red Rejected / grey Cancelled)* · a **masked bank account** badge when the request touches bank details · the request number, right-aligned |
| **Reason given** | the employee's own words |
| **The change itself** | one grey strip per field: **field label** · old value *(struck through, or* not set*)* · **→** · new value in bold |
| **Review comment** | a quoted block with the reviewer's name and timestamp, when one exists |
| Buttons | **Evidence** *(download, only when attached — shows the file name)* · **Approve & apply** · **Refuse** |
| Footer | *filed <date>* · *applied <date>* when applied |

**The refuse dialog** is the part worth showing. Title *Refuse REQ-…*, body: *<name> will read
this on their profile, so say what is missing or wrong — a refusal without a reason leaves them
with nothing to correct.* A **Reason** textarea with the placeholder *e.g. The bank letter does not
show the account name — please attach one that does.* **The Refuse button stays disabled until the
textarea is non-empty.**

**Requestable fields** — twenty-three, in two families:

| Family | Fields |
|---|---|
| On the employee record | FirstName · MiddleName · LastName · Title · DateOfBirth · Gender · MaritalStatus · EmailAddress · Address · City · State · PostalCode · DigitalAddress · CountryId · SocialSecurityNumber · TINNumber · TaxNumber |
| On the bank detail row | BankName · BankBranchName · BankAccountNumber · BankAccountName · BankAccountType · MobileMoneyNumber |

### ▶ Walk it

**1 — Show the employee's side first.** Switch to **Window B (staff)** and open `/me/profile`.

Five tabs: **Personal · Contact · Bank & statutory · Employment · My people**.

Go to **Contact**. Point at the four boxes — mobile, telephone, business number, extension — and
the **Save** button beneath them.

> "Efua can change her own phone number. She types it, she saves it, done. Nobody approves a
> phone number."

Go to **Personal**. Point at the **Request a change** button beside each row.

> "Now look at her surname, her date of birth. No box to type in. A button that says *request a
> change*. And on the next tab —"

Go to **Bank & statutory**.

> "— her SSNIT number, her TIN, and her bank account. Same thing: a request, not an edit. And
> notice the account number is masked even to her, because the server never sends it complete."

**2 — Make the argument.**

> "That split is the whole design. A wrong phone number costs somebody a missed call. A wrong bank
> account is a payroll fraud vector — you change the account and the salary goes somewhere else. A
> wrong date of birth moves a retirement date by years. Those are not corrections, they are
> changes to a statement the organisation has made about a person, and somebody has to look at
> the evidence."

**3 — Cross to the HR side.** Back to **Window A**, `/hr/employees/change-requests`.

Two cards are waiting — the ones you raised in §2.6.

**4 — Read the bank one across.**

> "Efua Seidu, staff number TDC/00017. She has asked to change her bank account number. Here is
> what it is now, here is what she wants it to be, here is her reason in her own words, and here
> —" *(click the evidence button)* "— is the bank letter she attached."

The file downloads.

**5 — 🔴 LIVE WRITE 5. Approve it.** Click **Approve & apply**.

**Expect:** a toast *REQ-… approved — The employee record has been updated*, and the card leaves
the **Waiting on HR** tab.

**6 — Prove it landed.** Open the showcase profile → **Bank** tab. The account number is the new
one.

> "That is not a notification to somebody who then types it in. The approval *is* the write — same
> transaction, same validation rules as the edit screen, and the old value is kept."

**7 — Now refuse the second one, which is the more interesting half.** Back on the queue, the
surname request has a thin reason and no evidence. Click **Refuse**.

Point at the disabled button.

> "I cannot refuse this without typing something. Watch."

Type: *"A change of surname needs the marriage certificate or the deed poll — please attach one
and ask again."*

The button enables. Click **Refuse request**.

**Expect:** toast *REQ-… refused — The employee can read your reason on their profile.*

**8 — Close the loop.** Switch to **Window B**, `/me/profile/change-requests`.

> "And there it is on her side — refused, with the reason, in full. That is why the server makes
> the comment mandatory: this page has to have something honest to show her. A refusal with no
> reason leaves an employee with nothing to correct and a grievance to file."

### ⚙ Behind the page

| Action | Call | Gate |
|---|---|---|
| The queue | `GET /api/hr/profile-change-requests?status=` | `HR.Employee.Read` |
| Approve | `POST /api/hr/profile-change-requests/{id}/approve` | `HR.Employee.Write` |
| Refuse | `POST /api/hr/profile-change-requests/{id}/reject` | `HR.Employee.Write` |
| Evidence | `GET …/{id}/evidence` | `HR.Employee.Read` |
| Employee side | `api/employee-portal/profile` | token-scoped |

Tables: `EmployeeProfileChangeRequests` + `EmployeeProfileChangeRequestItems`. An approval writes
`Employees` or `EmployeeBankDetails` depending on the field family, in the same transaction that
stamps the request Approved.

---

## 8. `/hr/employees/letter-requests` — letters staff have asked for

### 📍 Where you are

**Sidebar:** Human Resources → Employees → **Letter Requests** ·
`/hr/employees/letter-requests` · as **hr.head** · **5 minutes**

### 📖 What it is

Staff ask for the letters organisations ask them for. HR fulfils each one of two ways, and the
screen leads with the cheaper: **render it from a template and freeze it**, or **attach a signed
scan** for a letter that needs a wet signature or somebody else's form.

### 👁 On the page

Header *Employee Letter Requests*, subtitle *Letters staff have asked for. Read one before issuing
it — issuing freezes the document and the employee can hand it to a bank.*

**Two tabs:** **Waiting on HR** · **All**.

**One card per request:**

| Element | |
|---|---|
| Top row | employee name · staff number · **letter type** badge · **status** badge · the **letter number** once issued · request number, right |
| **For:** | the employee's stated purpose, and *· addressed to:* when they named a recipient |
| Decision comment | quoted, when refused |
| Buttons | **Preview** *(or* **View letter** *once issued)* · **Upload signed** · **Refuse** |
| Footer | *asked <date>* · *issued <date>* |

**The four letter types:**

| Type | What the employee is told it is |
|---|---|
| Confirmation of employment | *Confirms that you work here, since when, and in what role. The usual one.* |
| Letter of introduction | *Introduces you to a named organisation — say who it should be addressed to.* |
| Certificate of service | *States the period and capacity in which you have served.* |
| Employment and salary confirmation | *Confirms your employment and states your salary — for a bank or a landlord.* |

**The preview dialog** renders the letter as HTML on a white page *(white in dark mode too — it is
a letter)*, with **Print**, **Close** and **Issue this letter**. Printing swaps the page into a
print-only stylesheet so the dialog chrome does not come out on the paper.

**The upload dialog:** *For a letter that needs a wet signature, a stamp, or another
organisation's own form. Uploading fulfils the request — <name> can download it straight away.*

**The refuse dialog** works exactly like chapter 7's: mandatory reason, disabled until typed,
placeholder *e.g. Salary letters are issued after confirmation — please ask again in March.*

### ▶ Walk it

**1 — Read the waiting request.**

> "Efua has asked for an employment and salary confirmation, and she has said what for — a
> mortgage application with Ghana Commercial Bank. That matters, because the letter is going to
> state her salary to a bank."

**2 — Preview it.** Click **Preview**.

**Expect:** the dialog opens with the rendered letter — letterhead, date, the employee's name,
position, date employed, salary, and the company seal and signature.

**3 — Say why preview is not decoration.**

> "Read it before you issue it. This is not a nicety — issuing freezes this document and she can
> hand it to a bank the same afternoon. Reading it first is the step that catches a stale position
> or a salary that moved last week. The template is HR-editable, and the seal and the signature
> are versioned instruments held under HR settings, not images somebody pasted in."

**4 — 🔴 LIVE WRITE 6. Issue it.** Click **Issue this letter**.

**Expect:** toast *Letter LTR-… issued — Efua Seidu can collect it from their portal now.* The
card now carries a letter number, status **Issued**, and the button reads **View letter**.

**5 — Show the other route.** Click **Upload signed** on any pending request, then close it.

> "And that is the second route. Some letters need a wet signature, a stamp, or they have to go on
> the receiving organisation's own form. So HR attaches the scan and that fulfils the request — the
> employee gets a file rather than a rendered page, and the system knows which it is, so it never
> offers her a button that leads to a 404."

**6 — Close the loop.** Window B → `/me/letters`. The issued letter is there to open and print.

### ⚙ Behind the page

| Action | Call | Gate |
|---|---|---|
| Queue | `GET /api/hr/letter-requests?status=` | `HR.Employee.Read` |
| Preview | `POST …/{id}/preview` | `HR.Employee.Read` |
| Issue | `POST …/{id}/issue` | `HR.Employee.Write` |
| Upload signed | `POST …/{id}/upload` | `HR.Employee.Write` |
| Refuse | `POST …/{id}/reject` | `HR.Employee.Write` |

Table `HrLetterRequests`. `Fulfilment` records **Generated** or **Uploaded** — the employee's page
branches on it, which is why the row offers exactly one of *open the document* or *download the
file* rather than both.

---

## 9. `/hr/employees/identification-expiry` — the sweep

### 📍 Where you are

**Sidebar:** Human Resources → Employees → **ID Expiry** ·
`/hr/employees/identification-expiry` · as **hr.head** · **4 minutes**

### 📖 What it is

Company IDs, passports, permits and licences coming up for renewal, and those already lapsed. It
runs nightly on its own; the button is for when somebody wants it now.

### 👁 On the page

Header *Identification expiry*, subtitle *Company IDs, passports and permits coming up for renewal
— and those already lapsed.*, and a **Run a sweep now** button.

**Three tabs**, each with its live count in the label:

#### Tab 1 — **Due (N)**

A summary card first: *"**N** approaching renewal and **M** already lapsed. **K** would be newly
raised by a sweep now; the rest were claimed by an earlier one and will not be repeated."* plus a
link to **identification types** and the line: *A type with no warning time raises nothing — the
correct reading for an ID that does not expire.*

Then the table:

| Column | Shows |
|---|---|
| Employee | name *(a link to the profile)* with the staff number beneath |
| Document | the identification type |
| Number | the document number, mono |
| Expires | the date |
| **When** | **in N days** · **today** *(amber)* · **N days over** *(red)* |
| **Lead** | the type's warning time, right-aligned — *why this row surfaced when it did* |
| **Now with** | **The holder** *(outline)* at tier 1 · **HR** *(red)* at tier 2 |
| State | *Expiring* / *Expired*, or **Already raised** |

#### Tab 2 — **Sweeps (N)**

Started · Finished · **Trigger** *(**Nightly** badge for the scheduled service, outline for a
manual run)* · Raised.

Empty state: *No sweep has ever run — Neither the nightly service nor anybody here has run one. A
sweep that finds nothing still records a row, so an empty list here means it genuinely has not
run.*

#### Tab 3 — **Raised (N)**

What actually went out over the last 30 days: Raised · Employee · Document · Expired · **Was** ·
Owner.

### ▶ Walk it

**1 — Read the Due tab**, naming two actual rows — one in each tier. The passport and the lapsed
licence you set up in §2.4 are the two to use.

> "Every identification document in the register that is inside its warning window or past its
> date. Here — Efua Seidu's passport, expiring in *(read the* When *cell)*. And down here, a
> driver's licence, *(read it)* days over."

**2 — The two-tier column, which is the design.**

> "Look at *Now with*. Inside the warning window it says **the holder** — it is Efua's passport,
> she renews it, we remind her. Once the date has actually passed it says **HR**, in red, because
> it has stopped being a personal errand and become a compliance gap the organisation owns."

> "That is the whole idea of the screen: the tiers move *ownership*, not volume. One row per card
> per deadline either way. A second row addressed to HR while the holder is still in time would
> double the log and make 'how many cards are expiring' a question with two answers."

**3 — The Lead column.**

> "And this column says *why* a row is here today rather than last month. Ninety days' warning on
> a passport, thirty on a licence. That is set per document type, not per person — a Ghana Card
> renewal is not a passport renewal. A type with no warning time raises nothing at all, which is
> exactly right for a card that does not expire."

**4 — The Sweeps tab.**

> "Every run, scheduled or manual, with what it raised. A sweep that finds nothing still writes a
> row — so an empty list here means it genuinely has not run, rather than that there was nothing
> to find. That distinction is the difference between a working reminder and a silent one."

**5 — Run one.** Click **Run a sweep now**.

**Expect:** toast *Sweep finished — N card(s) due, M new reminder(s), K already raised.* A new row
appears on **Sweeps** with the trigger showing as a manual run, and rows on **Due** flip to
*Already raised*.

> "Nothing new went out, because the nightly service already claimed these. It will not remind the
> same person about the same deadline twice."

**6 — The leavers point, if anybody asks why a terminated name is in the list.**

> "Leavers are in here by design. An unreturned company ID belonging to somebody who has left is
> precisely the case this screen exists to surface."

### ⚙ Behind the page

| Action | Call | Gate |
|---|---|---|
| Preview | `GET /api/hr/identification-expiry/preview` | `HR.Employee.Read` |
| Sweeps | `GET …/runs?take=20` | `HR.Employee.Read` |
| Log | `GET …/log?days=30` | `HR.Employee.Read` |
| **Run** | `POST …/run` | `HR.Employee.Write` |

The screen is **read-gated, not write-gated** — seeing what is about to lapse is the point, and
only the run button carries Write.

**Lead days live on `IdentificationTypes.ExpiryNotificationLeadDays`**, which is nullable. Before
2026-09-01 nothing swept `EmployeeIdentificationCards.ExpiryDate` at all; the column and the
background service were added together, because a lead time nothing acts on is a setting that only
looks like a feature.

### ⚠ Known gaps

| | |
|---|---|
| **E-10 · No seeded warning times.** `IdentificationTypeSeeder` sets `HasExpiryDate` and leaves `ExpiryNotificationLeadDays` null on every type, so on a fresh database the sweep raises nothing and this screen is empty. §2.4 works around it by hand. The seeder should set a sensible default on the types that expire. |
| **E-11 · Renewing a document does not close its reminder.** By design — a renewal raises a *fresh* reminder against the new deadline rather than rewriting the old row — but the Raised tab has no way to see that the old one was resolved. |

---

## 10. `/hr/employees/import` — loading a register

### 📍 Where you are

**Sidebar:** Human Resources → Employees → **Import** · `/hr/employees/import` →
`/import/new` → `/import/[id]` · as **hr.head** · **8 minutes**

### 📖 What it is

A five-step wizard — **Template → Upload → Review → Commit → Result** — that loads many employees
from a spreadsheet the *system* issues, checks every row before writing anything, and hands you a
follow-up list afterwards.

### 👁 Screen 1 — `/hr/employees/import`, the history

Header *Employee Import*, subtitle *Load many employees from the system's own Excel template. Every
row is checked before anything is written.* Buttons: **Download template** and **New import**.

A card titled *N imports* over a table:

**Reference · File · Uploaded** *(date, with the uploader beneath)* **· Rows · Ready · Warnings ·
Errors · Created · Status**.

Empty: *No imports yet — Download the template, fill it in, and upload it here. Nothing is written
until you confirm.*

### 👁 Screen 2 — `/hr/employees/import/new`, steps 1 and 2

A wizard strip at the top, then **two cards side by side**.

**Card 1 · The template.** Description: *Generated now, with this organisation's departments,
sections, positions, locations, salary structure and identification types as reference sheets.
Download a fresh one whenever those change.* A **Download template** button, then five rules:

- One employee per row on the **Employees** sheet. Do not add, rename or reorder columns.
- Red headers are required; everything else can be completed later in the application.
- Dates must be real date cells. Staff numbers, phone and ID numbers are text so leading zeros survive.
- Department, Section, Position, Location and Salary Level must match a code or name on the reference sheets.
- Delete or overwrite the example row (Staff Number `EXAMPLE-001`); it is ignored either way.

Then a collapsible **Column guide (N columns)** — every column, its type, whether it is required,
and a help line.

**Card 2 · Upload the filled template.** A drag-and-drop zone *(Drop the .xlsx here, or click to
choose · Up to 15 MB. Only the template's format is accepted.)*, then the mode radio:

| Mode | Help text |
|---|---|
| **Create new employees** | *Every row is a new person. A staff number already in the register is an error.* |
| **Update existing employees** | *Rows are matched by staff number. Only the cells you filled change; a blank cell leaves the record as it is. A number not in the register is an error.* |
| **Create or update** | *A known staff number updates that person; an unknown one creates a new employee.* |

Then **Upload and check**, which reads *Checking every row…* while it works.

**A workbook the checker cannot use at all** — somebody's own spreadsheet, a template with a
column deleted — is refused right here with a red alert listing the reasons, and **no session is
created.**

### 👁 Screen 3 — `/hr/employees/import/[id]`, steps 3, 4 and 5

One page that changes with the session's status.

**Header:** the reference as the title; beneath it *file · mode · uploaded <date> by <person>*. On
the right, the status badge and a **Checked file** download.

**Tiles — before commit:** Rows · Ready *(green)* · Warnings *(amber)* · Errors *(red)* · Skipped.
**After:** Rows · Created · Updated · Failed at write · Not imported (errors) · Skipped.

**The commit card** *(status Validated only)*:

- A sentence: *"N rows will be written (X new, Y updated); Z with errors will be left behind; S
  skipped. Rows with warnings import and are listed for follow-up."*
- Two policies: **Commit valid rows only** *(errors stay; fix them in the checked file and upload
  again)* and **All or nothing** *(**disabled while any row has an error**, and it says how many)*
- Buttons: **Commit N rows** · **Upload a corrected file instead** · **Cancel this import**

**The rows table.** Filter buttons carrying counts — before commit *All · Ready · Warnings ·
Errors*; after, *All · Created · Created with issues · Failed · Not imported (errors) · Skipped* —
plus a search box for a staff number or name, and 50 rows a page.

| Column | |
|---|---|
| Row · Staff No. · Name · Type · Department · Position | as read from the sheet |
| **Action** *(update modes only)* | **Create** or **Update · N changes** |
| Result | the outcome badge |
| Findings | red *N errors* and/or amber *N warnings* |
| **Skip** *(Validated only)* | a switch |

**Clicking a row expands it** into three panels:

1. **What changes** *(update rows)* — a field-by-field `from → to` table, plus **Open the current profile**
2. **Findings** — each with its **cell reference** *(`L7`)*, the message, and *Did you mean: …?* suggestions
3. **As read from the sheet** — every non-empty value, verbatim

**While committing:** a progress bar and *"N of M written · X created · Y failed"*, with
*"You can leave this page; the import continues."*

**When done:** a result card with the completion time, **the staff-number counter reconciliation**
*(`TDC 103 → 109`)*, a **Profiles to complete (N)** download, a **Fix the checked file and upload
again** button when anything failed, and a table of the warned people with a link into each new
profile.

### ▶ Walk it

**1 — Set the problem up before opening anything.**

> "TDC's register today is a printed layout: two sheets, department banners between the rows,
> dates typed as text, and the word 'Grade' meaning a salary level on one page and a job title on
> the other. So the system does not accept anybody's spreadsheet. It *issues* one — and the one it
> issues already knows your departments, your positions, your locations and your salary
> structure."

**2 — Download the template and open it.** Show the **Employees** sheet: red required headers,
the example row, the reference sheets behind it.

> "Red headers are required. Everything else can be completed later in the application. And these
> tabs behind — Departments, Positions, Locations, Salary Structure — those are your live
> registers, generated into the file at the moment you downloaded it. The dropdowns on the list
> columns read from them."

**3 — Upload the prepared file.** Drop `demo-employee-import.xlsx`. Leave the mode on **Create new
employees**. Click **Upload and check**.

**Expect:** a toast naming the counts, and the page moves to the session with tiles reading
**6 rows · 1 ready · 3 warnings · 2 errors**.

**4 — Open an error row.** Click **DEMO-004, Esi Quaye**.

**Expect:** the findings panel names the **cell** — *`L7` — no department matches "Estate
Managemen"* — with *Did you mean: Estate Management?* beside it, and the sheet's values as read on
the right.

> "It tells you the cell. Not 'row seven has a problem' — column L, row seven, what is wrong with
> it, and what it thinks you meant."

**5 — Make the distinction that matters.**

> "An **error** means the row will not be written until the cell is fixed. A **warning** — no
> email address, no date of birth, a qualification with no institution — still imports, and the
> person comes out on a follow-up list so somebody completes the profile afterwards. The rules
> being applied are the same ones the create form applies one at a time. The import just checks
> them all at once."

**6 — The checked file.** Click **Checked file** and open it.

> "Every row gets a Result column, and every problem cell is coloured and commented in place. Fix
> it in Excel, upload it again — same checks, same counts, until it is clean. That is the loop."

**7 — Skip a row.** Flick the **Skip** switch on one warning row.

> "Skipping is for the person who should not be loaded at all — the duplicate, the leaver
> somebody added by mistake. It is not for the one whose department is misspelt; that one gets
> fixed."

**8 — Point at the greyed policy.**

> "**All or nothing** is greyed out, and it says why: two rows have errors. You cannot ask for an
> all-or-nothing commit while the file is known to be broken."

**9 — 🔴 LIVE WRITE 7. Commit.** Choose **Commit valid rows only** → **Commit N rows** → read the
confirmation dialog aloud, particularly its last line:

> "*New staff numbers are accepted exactly as given and the register's counter is advanced past
> them. This cannot be reversed from here.*"

Confirm.

**Expect:** a progress bar filling as the background committer writes each row through the same
service the create form uses.

**10 — The result.**

**Expect:** tiles flip to Created / Failed at write / Not imported / Skipped, and the counter
reconciliation line appears.

**Read the counter line off the screen** — it has the form *TDC 103 → 109*.

> "There is the counter line. The register was at that first number, these staff numbers were
> accepted exactly as the spreadsheet gave them, and the counter has been moved past them. So the
> next real hire is not handed a number somebody on this list already holds. That single line is
> the difference between a bulk load that works and one that detonates three weeks later on an
> unrelated screen."

**11 — The follow-up list.** Click into **Profiles to complete (N)**.

> "The warned people, with what each one is missing, a link straight into their new profile, and a
> workbook you can hand to whoever is going to chase it. The output of an import is not a number.
> It is a list of work."

⚠ **CAREFUL.** The demo pack's cheat sheet says *Employees → Import (show only)* and it is right
to be cautious: committing creates real employee rows. Commit only on `ErpSystemDB_UAT`, only with
the `DEMO-` file, and follow chapter 15 afterwards. If you are demonstrating against anything
else, stop at step 8 and say *"and from here it commits"*.

### ⚙ Behind the page

Every route is under `/api/hr/employees/import-sessions` and the **whole controller is gated on
`HR.Employee.Write`** — one attribute at class level.

| Step | Call |
|---|---|
| Template | `GET …/template` |
| Column guide | `GET …/columns` |
| Upload | `POST …` *(multipart, mode in the form)* |
| Rows | `GET …/{id}/rows?outcome=&search=&page=&pageSize=50` |
| Skip | `PATCH …/{id}/rows/{rowId}/skip` |
| Commit | `POST …/{id}/commit` *(policy in the body)* |
| Progress | `GET …/{id}/progress` — polled every 2 s while running |
| Cancel | `POST …/{id}/cancel` |
| Follow-up | `GET …/{id}/follow-up[?format=xlsx]` |
| Checked copy | `GET …/{id}/report` |

Tables `EmployeeImportSessions` and `EmployeeImportSessionRows`. The commit runs in a **background
committer** — the page polls and you may navigate away. Every row goes through the same service as
the create form, which is why an import cannot produce a record the form would have refused.

The uploaded workbook is kept in the document store against the session. Managers named by staff
number *inside the same file* are linked once both rows exist.

---

## 11. `/hr/employees/payroll-reconciliation`

### 📍 Where you are

**From:** `/hr/employees` → **Payroll reconciliation** *(header button — there is no menu entry)* ·
as **hr.head** · **3 minutes**

### 📖 What it is

HR's statement that somebody is paid through the run, beside Payroll's own employee profile. The
two are different owners' facts and are deliberately not collapsed into one column. This screen
lists where they disagree.

### 👁 On the page

Header *Payroll reconciliation*, subtitle *Where HR's on-payroll statement and Payroll's employee
profiles disagree.*, and a **Refresh** button.

**Seven tiles.** The first two are counts; the other five are **clickable filters** and turn amber
when non-zero:

| Tile | What it means |
|---|---|
| On payroll (HR) | count |
| Not on payroll (HR) | count |
| **Still active in Payroll** | *HR took these people off payroll but Payroll still has them active — the next run will pay them. Payroll switches them off; HR does not reach into Payroll to do it.* |
| **Awaiting payroll setup** | *HR says they are paid through the run but Payroll has no profile. Saving the employee again with "On payroll" ticked creates one; otherwise set them up in Payroll → Employee profiles.* |
| **Inactive in Payroll** | *HR says on payroll; Payroll has switched their profile off. One side is stale — agree which.* |
| **Basic pay mismatch** | *On the salary scale and placed on a notch, but Payroll's active basis is a different amount — the run pays Payroll's figure while HR's placement says another, every month until one side is corrected. Not raised for negotiated pay, where Payroll's figure is the basis.* |
| **No pay basis** | *On payroll with no salary and no graded notch on either side. A run skips a zero basis silently, so this is the case nobody notices until payday.* |

Selecting a tile shows its explanation in a grey bar and filters the table.

**The table:** Employee *(name, then staff number · employment type · staff status)* · Position
*(with the unit beneath)* · **HR says** · **Payroll has** *(No profile / Profile · active /
Profile · switched off)* · **HR basic pay** · **Issue**. Rows link to the profile.

Empty: *Nothing to reconcile — Every live employee's payroll membership agrees with Payroll's
profile.*

### ▶ Walk it

**1 — Frame it.**

> "HR says who *should* be paid through the run. Payroll decides who *is*. In every organisation
> those two drift, and the drift is invisible until somebody is paid who left in March, or
> somebody is not paid at all. This screen is where the two statements are held up against each
> other."

**2 — Read the tiles.** Click any that is non-zero. If they are all zero:

> "On this database they agree, so there is nothing to show — which is the state you want, and not
> the state you will be in on a Monday after a busy month. Let me read you what it looks for."

**3 — Read out the worst one.**

> "**No pay basis.** On payroll, and no salary on either side. The payroll run skips a zero basis
> silently — it does not fail, it does not warn, the person simply is not paid. That is the case
> nobody notices until payday, and it is the reason this screen exists."

**4 — And the second worst.**

> "**Basic pay mismatch.** HR has placed them on a notch worth one figure and Payroll's active
> basis is a different figure. The run pays Payroll's. Every month. Until somebody notices."

**5 — Name the boundary.**

> "And notice what this screen does *not* do. There is no fix button. HR does not reach into
> Payroll and switch somebody off — the text says so explicitly. This finds the disagreement and
> names whose job each side of it is. Collapsing two owners' facts into one column is how you get
> a system that is confidently wrong."

### ⚙ Behind the page

`GET /api/hr/Employees/payroll-reconciliation` · `HR.Employee.Read` · joins `Employees` against
Payroll's own employee-profile store and classifies each disagreement into one of the five issues.

---

## 12. `/hr/organogram`

### 📍 Where you are

**Sidebar:** Human Resources → Employees → **Organogram** · `/hr/organogram` · as **hr.head** ·
**6 minutes**

### 📖 What it is

The organisation drawn from its own records. Five different charts of the same organisation, a
strip of numbers that says how much of it has actually been entered, and an export.

### 👁 On the page

Header *Organogram*, subtitle *The organisation drawn from its own records — units, posts,
reporting lines, teams and sites.*, an *As at <timestamp>* stamp and a **Refresh**.

**Five dimension tabs**, each with its own one-line description beside them:

| Dimension | Description |
|---|---|
| **Units** | *The org backbone — each unit sits under its parent unit.* |
| **Positions** | *Establishment posts — each post reports to another post.* |
| **People** | *Reporting lines — each person sits under their recorded manager.* |
| **Teams** | *Working groups — each sub-team sits under its parent team.* |
| **Locations** | *Geography — each site sits under its parent location.* — and only this one needs a **location structure** dropdown |

**The coverage strip** — four tiles always, plus two or three per dimension:

| Always | Units adds | Positions adds | Teams adds |
|---|---|---|---|
| *(dimension name)* · **Levels deep** · **Widest branch** · **Placed in the hierarchy %** | **Staff placed** · **Units without a head** | **Holders** · **Vacant posts** | **Teams without a lead** |

*Inactive* appears as a further tile when there are any.

**When half or more of the nodes have no parent**, an alert appears: *Most <things> are not placed
in the hierarchy — N of M (X%) sit at the top because no parent is recorded against them… This
chart is a picture of what has been entered, not of a flat organisation.*

**The chart toolbar:**

| Control | |
|---|---|
| **Search** | *Search by name, title or code… ( / )* with **previous / next match** arrows and a clear |
| **Focus breadcrumbs** | appear when you focus a branch; a home button returns to the whole chart |
| **View** menu | **Orientation** — Top down / Left to right · **Card size** — Compact / Comfortable / Detailed · **Colour by** — the heat modes with a description each · **Show** — Stack wide rows into columns · Vacancies only · Hide inactive · Search shows matches only · Legend · Overview map |
| **Levels** | a dropdown for how many levels open, plus **expand everything** and **collapse to the top level** |
| **Zoom** | out / a percentage that resets on click / in / **fit to screen** |
| **Full screen** | |
| **Export** | opens the print dialog |

**The export dialog** — *Print or export the organogram*: a **Format** choice, a **What to
include** choice *(**What is on screen** — N boxes as currently expanded · **This branch** ·
**The whole chart** — N boxes fully expanded)*, and for print formats a **Page** choice of *A4
landscape, fitted* · *A3 landscape, fitted* · *Actual size, one page*.

**Clicking a node** opens a detail panel on the right.

### ▶ Walk it

**1 — Start on Units.**

> "This is TDC's organogram as supplied — Board of Directors, the Managing Director's office, the
> two directorates, the departments beneath them, the sections beneath those."

Expand **Operations Directorate → Development Department**.

> "Head: Kwasi Danquah. Sections: Architecture, Building Maintenance, Planning, Quantity Survey,
> Survey and Geodetic."

**2 — The governance point, which is the best thirty seconds on this screen.**

Expand **Board of Directors → Internal Audit Department**.

> "Internal Audit sits under the Board, not under management. So the Chief Internal Auditor has no
> line manager anywhere in this system — and nobody configured that. It fell out of the structure.
> Which is the test of whether an org chart is real or drawn."

**3 — Switch to Positions.**

> "Same organisation, different question. Not *which department* but *which post reports to which
> post*. The boxes on your original chart are positions, and that is how it is modelled: units are
> the places, positions are the jobs, and each post knows the post it answers to. Which is why
> the reporting line on an employee record is offered from the establishment rather than typed."

Point at **Vacant posts** in the coverage strip and **read the number off the screen** — you wrote
it down in §2.10.

> "That many vacant posts. It is not a report somebody compiles — it is the difference between the
> establishment and the register, live. And it is where recruitment starts."

Open the **View** menu and tick **Vacancies only**.

> "There they are on their own."

Untick it.

**4 — Switch to People — and say the thing the coverage strip is for.**

> "And this is the reporting-line chart. Now look at the strip across the top, because it is doing
> something most charts do not."

Read the **Placed in the hierarchy** tile out loud.

> "A tree where every node is a root is not a hierarchy. Without that number, an organisation whose
> reporting lines were never entered looks exactly like an organisation that is genuinely flat.
> This chart tells you which it is looking at, every single time — and if most nodes are unplaced
> it says so in a banner rather than quietly drawing you a lie."

**5 — The restricted view, if you can show it.** The People chart is limited to HR and tenant
administrators — it is the personnel register with contact details. Another persona sees an alert
saying so, and the other four views stay open to them.

**6 — Make the picture usable.** Press **Full screen**. Set **Levels** to 3. Try **Left to right**.
Set **Colour by** to a heat mode. Search for a name and step through the matches.

> "Every one of those choices is in the URL. Which means the view I am looking at right now is a
> link I can send to somebody, and they open exactly this."

**7 — Export.** Open the export dialog and read the scope choices without printing.

> "Three scopes, and the counts are in the labels — what is on screen as currently expanded, just
> this branch, or the whole chart fully expanded. A4 or A3, fitted, or actual size on one page.
> For the wall."

### ⚙ Behind the page

`GET` organogram by dimension *(with a structure id for locations)*. The people payload is roughly
**2.3 MB**, cached for five minutes — re-fetching it on every tab switch is the difference between
a snappy screen and a sluggish one, and the structure does not change by the minute.

`buildTree` assembles the hierarchy in the browser, `measureHealth` computes the coverage strip.
The whole view state — dimension, focus, selection, depth, orientation, density, colouring, search,
filters — is serialised into the query string, debounced 250 ms so typing does not spam history.

**The People dimension is restricted to HR and tenant administrators**; a 403 renders the
explanatory alert rather than an error.

---

## 13. `/hr/announcements`

### 📍 Where you are

**Sidebar:** Human Resources → Employees → **Announcements** · `/hr/announcements` · as
**hr.head** · **4 minutes**

### 📖 What it is

Notices published to staff. A draft is invisible until published; a published notice is **archived
rather than edited**, so what people were actually told stays findable.

### 👁 On the page

Header *Staff Announcements*, with a **New announcement** button.
**Four tabs:** Drafts · Published · Archived · All.

**One card per announcement**, carrying the title, category, status badge, a pinned marker, the
audience rules as badges, and its buttons:

| Status | Buttons |
|---|---|
| **Draft** | **Publish** · Edit · **Attach a file** · Delete |
| **Published** | **Archive** |

**The editor dialog:**

| Field | |
|---|---|
| **Title** | |
| **Category** | General · Policy · Benefits · Safety · Event · Urgent |
| **Stops showing (optional)** | a date |
| **One-line summary** | *(shown on the dashboard)* |
| **Announcement** | textarea, 6 rows, *Plain text. Line breaks are kept.* |
| **Keep at the top of the list** | checkbox |
| **Who gets this?** | the audience builder |

**The audience builder** is the part worth showing. It is a list of rules, each a dropdown plus
optionally a target plus an **except** checkbox and a remove:

| Rule | Needs a target? | Means |
|---|---|---|
| **Everyone** | no | every active employee |
| **Organisation unit** | yes | the unit **and everything beneath it** |
| **Organisation level** | yes | a tier of the org chart |
| **Position** | yes | everyone holding this job |
| **Location** | yes | everyone based at this site |

**And beside the heading, live, a recipient count** — *"412 recipients"* — recomputed as you edit
the rules, in red when it is zero. With no rules at all: *Nobody yet — add a rule below. An
announcement addressed to nobody cannot be published.*

### ▶ Walk it

**1 — Show the list and the states.**

> "Drafts, published, archived. A draft is invisible to staff. And a published notice is never
> edited — it is archived and replaced. What people were told on the day stays exactly as it was,
> which matters the first time somebody says 'that is not what the notice said'."

**2 — 🔴 LIVE WRITE 8. New announcement.** Click **New announcement**.

Title: *"Annual medical screening — Tema Head Office"*. Category **Benefits**. Summary: *"Screening
runs 12–16 October in the clinic. Book a slot with your unit head."* Body: two or three lines.

**3 — Now build the audience slowly, watching the number.** **Read each count off the screen as it
changes** — that is the whole point of the step, so do not quote a number from this page.

Add a rule → leave it on **Everyone**.

> "Everyone. That many people."

Change it to **Location → Tema Head Office**.

> "Now just this site. Watch the number move."

Add a second rule → **Organisation unit → Development Department** → tick **except**.

> "And now, everyone at Tema Head Office *except* the Development Department, who are on site at
> Community 26 that week. Watch the number again."

**4 — Land the point.**

> "That count is the reason this builder exists. The way announcements go wrong is not that the
> text is bad — it is that a notice meant for four hundred people went to four, or a notice meant
> for one department went to eight thousand, and nobody found out until afterwards. So the reach
> is on the screen while you are still writing, and a notice addressed to nobody cannot be
> published at all."

**5 — Save, then attach a file** if you have a PDF. Then **Publish**.

**Expect:** the card moves to the **Published** tab and picks up an **Archive** button.

**6 — Show the employee's side.** Window B → `/me/announcements`.

⚠ The **one person** audience rule exists in the API but is deliberately **not offered here** —
choosing a person needs an employee search gated on `HR.Employee`, which an `HR.Company` user need
not hold.

### ⚙ Behind the page

`/api/hr/announcements` — reads `HR.Company.Read`, writes `HR.Company.Write`. Tables
`HrAnnouncements` + `HrAnnouncementAudiences`. The reach count is a live server call against the
unsaved rule set, which is why it updates as you type.

---

## 14. `/hr/policies` — the policy library

### 📍 Where you are

**Sidebar:** Human Resources → Employees → **Policy Library** · `/hr/policies` · as **hr.head**,
with Window B for the signature · **5 minutes**

### 📖 What it is

The policies staff can read, and the ones they are asked to sign. **A published policy is
superseded rather than edited, so the signatures already collected keep their meaning.**

### 👁 On the page

Header *Policy Library*, subtitle as above, **New policy** button.
**Four tabs:** Published · Drafts · **Withdrawn** · All.

**One card per policy:**

| Element | |
|---|---|
| Top row | title · category · **version** *(mono badge)* · status · **Signature required** · **No document** *(red, when none is attached)* · policy number, right |
| Summary | |
| Supersedes | *Supersedes: <previous title>* when one is named |
| Audience badges | one per rule, red for an exclusion |
| Signature line | *"N of M signed · K declined"*, for a published policy that requires signature |

| Status | Buttons |
|---|---|
| **Draft** | **Publish** · Edit · **Attach / Replace document** · Delete |
| **Published** | **Who has signed** *(when signature is required)* · **Withdraw** |

**Publish is refused** without a document, without a declaration where one is asked for, or with
an audience that reaches nobody. **The document of a published policy cannot be replaced** — you
publish a new version that supersedes it.

**The "Who has signed" drawer** — four totals across the whole audience, then four tabs:
**Outstanding** *(the default)* · **Signed** · **Declined** · **Everyone**, over a paged table of
Employee · Unit · Outcome.

⚠ The audience builder here is **deliberately simpler** than the announcements one — everyone, or
one organisation unit. A policy addressed by a complicated rule set is a policy nobody can explain
the scope of, which is a poor property for something people sign.

### ▶ Walk it

**1 — The library.**

> "Every policy, its version, its category, and whether it needs a signature. Note the version
> badge — v1.0 — and that this one *supersedes* an earlier document. A published policy is never
> edited. You publish a new version, it supersedes the old one, and the signatures already
> collected against the old one still mean what they meant: that person read *that* document."

**2 — The compliance drawer.** Click **Who has signed** on the Code of Conduct.

**Expect:** four totals, and the **Outstanding** tab open first.

> "It opens on outstanding, because that is the work. Signed is a number; outstanding is a list of
> people to chase."

Click through **Signed** and **Declined**.

**3 — The declined point.**

> "And declined is its own outcome, not a missing signature. Somebody read it and refused. That is
> an answer to HR and it is recorded as one — it does not clear the obligation, and it does not
> disappear from the employee's own list either, because a person should not be able to believe a
> matter is closed when it is not."

**4 — 🔴 LIVE WRITE 9. Publish one.** **New policy** → title *"Remote Working Policy"*, version
`v1.0`, category **General**, tick **signature required**, audience **Everyone**, save → **Attach
document** → **Publish**.

**5 — Sign it from the other side.** Window B → `/me/policies`.

> "Outstanding first, again, for the same reason."

Open *Remote Working Policy*, read the declaration, sign it.

**6 — Back to Window A**, reopen **Who has signed**.

**Expect:** the signature count has moved by one and Efua has left the **Outstanding** tab.

> "One signature, against a named audience, with a timestamp — which is exactly what an auditor
> asks for and exactly what a shared network folder can never produce."

### ⚙ Behind the page

`/api/hr/policies` — reads `HR.Company.Read`, writes `HR.Company.Write`. Tables `HrPolicies`,
`HrPolicyAudiences`, `HrPolicyAcknowledgements`.

The compliance read is **computed at request time from the current audience** — so somebody who
joins the unit tomorrow appears as outstanding tomorrow, without the policy being republished. The
drawer pages its rows deliberately: the four totals always cover the whole audience, but a
tenant-wide policy has thousands of rows and returning them all measured 2.3 MB.

---

## 15. Reset — putting the database back

Do this after the room empties. Everything below is reversible; nothing needs a rebuild.

| # | What you changed | Undo |
|---|---|---|
| 1 | **Kwabena Ofori** created *(ch. 4)* | Window C (admin) → register → `⋯` → **Delete**. Soft delete: the row is hidden, not removed. ⚠ **his email address stays occupied** — reuse a different one next time |
| 2 | **Salary change request** raised *(ch. 5.3)* | If still Draft or Pending, use the card's **remove** / **Recall**. If it was approved and applied, raise a second request back to the original notch — an applied change is not undone, it is superseded |
| 3 | **Document filed** *(ch. 5.8)* | Documents tab → the row → **Delete** *(admin only)* |
| 4 | **Ofori terminated / reinstated** *(ch. 5.9)* | Already reinstated in the walk. Then delete him per row 1 |
| 5 | **Change requests approved and refused** *(ch. 7)* | The approved one wrote a new bank account number onto the record — **edit the Bank tab back** to the original. Re-raise both from `/me/profile` for the next run |
| 6 | **Letter issued** *(ch. 8)* | An issued letter is frozen by design and cannot be withdrawn. Leave it; re-raise a fresh request from `/me/letters` for the next run |
| 7 | **Sweep run** *(ch. 9)* | Nothing to undo — a run is a log entry. It does mean some Due rows now read *Already raised*; they reset when the deadline moves |
| 8 | **Import committed** *(ch. 10)* | **The important one.** Delete each `DEMO-00n` employee that was created, as admin, from the register. The **staff-number counter cannot be moved back from the UI** — use **Administration → HR → Settings → Staff Numbering → reconcile** on the affected register, which sets it to the highest number actually in use |
| 9 | **Announcement published** *(ch. 13)* | **Archive** it |
| 10 | **Policy published and signed** *(ch. 14)* | **Withdraw** it. The signature stays against the withdrawn version, correctly |

**The clean option.** If any of that looks fiddly, the whole database rebuilds in 45–60 minutes:

```
# stop every API first — a stray one ruins the rebuild
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
  Where-Object { $_.CommandLine -like '*ErpSystem.Api*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

powershell -File .\scripts\New-UatDatabase.ps1
```

⚠ A rebuild **wipes chapter 2's prep** — all twelve tabs, both warning times, the queues, the
announcement and the policy. Budget the 45 minutes to redo it.

---

## 16. The short path — 25 minutes

When the slot shrinks. Six screens, in this order, and the story still lands.

| # | Screen | Minutes | The one thing |
|---|---|---|---|
| 1 | `/hr/employees` | 2 | one person, once; the **Not on payroll** filter |
| 2 | `/hr/employees/new` | 5 | pick a position and watch unit, level, manager and probation fill themselves in. **Create it.** |
| 3 | The profile — Overview, then Documents | 6 | eight groups; the provenance line on probation; the two payroll answers side by side; **the compliance card** |
| 4 | `/hr/employees/change-requests` | 5 | the portal side first, then **approve one and refuse one**; show the refusal on the employee's own page |
| 5 | `/hr/organogram` → Positions | 4 | vacant posts are the difference between establishment and register, live; then the **coverage strip** |
| 6 | `/hr/employees/payroll-reconciliation` | 3 | two owners, one question, no fix button |

Cut, in this order if you must: the organogram *(Book 1 §2 covers it)*, then the import, then the
letters.

---

## 17. What this walk found

Eleven findings. None blocks the demonstration; four are worth fixing before the module is
considered finished, and two contradict the existing demo book.

| # | Where | Finding | Severity |
|---|---|---|---|
| **E-1** | ch. 3 | Register exposes 2 of the 15 filters its API accepts — no unit, status, employment-type or date-employed filter | **medium** |
| **E-2** | ch. 3 | No sorting at all; fixed surname-then-forename order | low |
| **E-3** | ch. 3 | No export from the register | low |
| **E-4** | ch. 3 | **Book 1 §1 is stale** — claims unit and status filters that do not exist, and a staff-number sort that does not exist | **correct the book** |
| **E-5** | ch. 4 | The import checkbox is offered on a form whose persona cannot submit it (`HR.Employee.Admin`); the Documents tab already models the right pattern | **medium** |
| **E-6** | ch. 6 | Confirmation date round-trips invisibly through the edit form | low |
| **E-7** | ch. 5 | **Retirement date is nowhere on the profile**, though the field exists and two other screens read it | **medium** |
| **E-8** | ch. 5.9 | Lifecycle buttons rendered unconditionally and gated only server-side; `hr.head` gets a 403 from a button they can see | **medium** |
| **E-9** | ch. 5.2 | Teams allocation percentages are never totalled, so the over-allocation the tab exists to reveal must be added by eye | low |
| **E-10** | ch. 9 | `IdentificationTypeSeeder` leaves every warning time null, so the expiry sweep raises nothing on a fresh database | **medium** |
| **E-11** | ch. 9 | A renewed document's old reminder cannot be seen to have been resolved | low |
| **E-12** | ch. 2 | **The demo database populates none of the 15 owned sub-record tabs**, and four of the eight menu items open empty. Not a defect — but a demo-readiness gap that costs 45 minutes of manual prep before every demonstration, and is lost on every rebuild | **medium** |

**E-12 is the one worth acting on.** A `TdcDemoEmployeeProfileSeeder` — one address, two ID cards,
a qualification, two skills, a certification, a work-history row, a contract, a bank account, a
referee, a guarantor for a dozen named employees, plus two pending change requests, one letter
request, warning times on the expiring identification types, one published announcement and one
signed policy — would remove chapter 2 entirely and make every future employees demonstration a
cold open. It is the same shape as `TdcDemoRecruitmentHistorySeeder`, which did exactly this for
recruitment.

---

## Appendix A — every route, in demo order

| # | Route | Persona | Writes |
|---|---|---|---|
| 1 | `/hr/employees` | hr.head | — |
| 2 | `/hr/employees/new` | hr.head | **LIVE 1** — creates an employee |
| 3 | `/hr/employees/[id]` | hr.head | — |
| 4 | `/hr/employees/[id]?tab=salary` | hr.head | **LIVE 2** — raises a salary change |
| 5 | `/hr/employees/[id]?tab=documents` | hr.head | **LIVE 3** — files a document |
| 6 | `/hr/employees/[id]` header | admin | **LIVE 4** — terminate, then reinstate |
| 7 | `/hr/employees/[id]/edit` | hr.head | amends the record |
| 8 | `/me/profile` | staff | — *(shown, not written)* |
| 9 | `/hr/employees/change-requests` | hr.head | **LIVE 5** — approve one, refuse one |
| 10 | `/me/profile/change-requests` | staff | — |
| 11 | `/hr/employees/letter-requests` | hr.head | **LIVE 6** — issues a letter |
| 12 | `/me/letters` | staff | — |
| 13 | `/hr/employees/identification-expiry` | hr.head | runs a sweep *(log only)* |
| 14 | `/hr/employees/import` → `/new` → `/[id]` | hr.head | **LIVE 7** — commits an import |
| 15 | `/hr/employees/payroll-reconciliation` | hr.head | — |
| 16 | `/hr/organogram` | hr.head | — |
| 17 | `/hr/announcements` | hr.head | **LIVE 8** — publishes a notice |
| 18 | `/hr/policies` | hr.head | **LIVE 9** — publishes a policy |
| 19 | `/me/policies` | staff | signs it |

## Appendix B — the permission map, in one table

| Act | Permission | `hr.head`? |
|---|---|---|
| Open the register, use the picker | *(none)* | ✔ |
| Open a profile, any tab, any queue | `HR.Employee.Read` | ✔ |
| Create an employee | `HR.Employee.Write` | ✔ |
| Amend an employee or any sub-record | `HR.Employee.Write` | ✔ |
| Verify a qualification, skill, ID, guarantor, bank account | `HR.Employee.Write` | ✔ |
| Approve or refuse a change request | `HR.Employee.Write` | ✔ |
| Preview, issue, upload or refuse a letter | `HR.Employee.Write` | ✔ |
| Run the identification sweep | `HR.Employee.Write` | ✔ |
| The whole bulk-import wizard | `HR.Employee.Write` | ✔ |
| **Record an employee who already has a number** | `HR.Employee.Admin` | **✘** |
| **Delete an employee or any sub-record** | `HR.Employee.Admin` | **✘** |
| **Deactivate / Activate / Terminate / Reinstate** | `HR.Employee.Admin` | **✘** |
| Salary tab writes | `HR.Compensation.Write` | ✔ |
| Announcements, policy library | `HR.Company.Read` / `.Write` | ✔ |
| The **People** organogram | HR or tenant admin | ✔ |

## Appendix C — related documents

| Document | For |
|---|---|
| `docs/HR/HR-RECRUITMENT-SYSTEM-GUIDE.md` | the same treatment for recruitment; chapter 1 there explains the establishment this module inherits |
| `docs/HR/HR-EMPLOYEE-IMPORT-DESIGN.md` | the import's design, its template contract and its checker |
| `docs/HR/HR-PAYROLL-BOUNDARY.md` | why chapter 11 exists and where the ownership line runs |
| `docs/HR/HR-WORKFLOW-ENGINE-INTEGRATION.md` | what happens to the salary change request in chapter 5.3 after Submit |
| `docs/HR/HR-ORGANOGRAM-REDESIGN.md` | the 2026-09-08 rebuild behind chapter 12 |
| `docs/UAT-DEMO-DATABASE.md` | building `ErpSystemDB_UAT`, and what each seeder does |
| `dev-harness/hr-demo-smoke/runbook/` | Books 0–4 and Book R — the printed demo pack this guide's chapter 2 supplements |
