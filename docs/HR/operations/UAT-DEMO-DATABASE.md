# UAT / demo database — runbook

Follow this top to bottom. Every command is copy-paste, and every step says what you should see.

## How it works, in three lines

There are two databases on the same SQL Server: `ErpSystemDB` for development and
`ErpSystemDB_UAT` for demos. **You run one API at a time, always on port 5000, and the API decides
which database is live.** The frontend always points at port 5000, so you never touch the frontend
when you switch.

---

## A. One-time: build the UAT database

Do this once. Repeat it later only when you want to wipe UAT and start clean.

**A1.** Build the solution first if you have not already. You run the build; nothing here does it
for you.

**A2.** Open PowerShell in the repository root and run:

```powershell
powershell -File .\scripts\New-UatDatabase.ps1
```

**A3.** It asks you to type the database name to confirm. Type exactly:

```
ErpSystemDB_UAT
```

**A4.** Wait. It runs seven steps and takes **45–60 minutes** (the last is the long one). You should
see, in order:

```
  -> Dropping and recreating 'ErpSystemDB_UAT' empty
  -> Building the schema through the migration chain (~2 min)
  -> Running the core seeders (roles, tenant, modules)
  -> Seeding workflow definitions (approvals refuse to submit without these)
  -> Seeding HR reference data and TDC organisation structure
  -> Seeding the DEMO workforce, leave calendar and HR/SHE sample data
  -> Building the transactional layer through the API, then checking it
```

> ⚠ **Changed 2026-09-27 (merge #11).** The first step used to be `rebuild-db`, which builds the
> schema from the EF model. It now builds through the migration chain, because only the chain
> creates master's 500 guard triggers. See rule G.2.

The last step starts the scanner stub and the API itself (on port 5000 — nothing else may be on
it, see B1), runs every demo scenario as the personas, checks the result, and stops what it
started. It prints one `▶` line per scenario with a `✓` or `✗` under it.

**A5.** It then prints three verdicts. All three must be green:

```
  SCENARIOS: <n> ok, 0 failed
  REQUIRED: <n> of <n> tables hold data; 0 still empty; 48 excluded (logs)
  RUNBOOK: <n> of <n> required names found; 0 unmet
  DEMO DATASET COMPLETE: scenarios ok, every required table holds data, runbooks consistent.
```

and finally the row counts (`Demo staff (TDC/...) = 103`, `Demo logins (linked) = 9`, …; the plain
`Employees` total also includes other modules' fixtures and moves when master is merged). **If the last banner is red, the database is not fit for a demo.** Read the `✗`
lines, fix the cause, and re-run just the fifth step — it is safe to repeat:

```powershell
powershell -File .\scripts\Invoke-UatDemoScenarios.ps1
```

> The script refuses to touch `ErpSystemDB`. If you typo the name into the dev database it stops
> with `REFUSED:` and does nothing.

**Where the database password comes from.** None of the scripts carries one. Each takes it from
`-Password`, else `$env:ERP_DB_PASSWORD`, else the `DefaultConnection` string in
`src/ErpSystem.Api/appsettings.json` — which is gitignored and is the file the API itself reads. So
a rotated password is changed in one place and everything follows it, and no credential is ever
staged. See `scripts/ErpDbCredential.ps1`.

---

## B. Switch to UAT and run the app

**For a demonstration, use the one command** — it does everything in this section, proves the API
is on the demo database, and says READY or tells you what is wrong:

```powershell
powershell -File .\scripts\Start-Demo.ps1          # up
powershell -File .\scripts\Start-Demo.ps1 -Stop    # down afterwards
```

The steps below are the same thing by hand, and are what you want for ordinary development.


**B1. Stop any API that is already running.** In the window where it is running press `Ctrl+C`.
If you cannot find the window:

```powershell
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
  Where-Object { $_.CommandLine -like '*ErpSystem.Api.dll*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
```

**B2. Start the API against UAT.** From the repository root:

```powershell
powershell -File .\scripts\Start-ErpApi.ps1 -Database Uat
```

You should see:

```
  Database    : ErpSystemDB_UAT
  Environment : Staging
  Listening   : http://localhost:5000
```

**Leave this window open.** The API runs in it.

**B3. Start the frontend.** In a *second* PowerShell window:

```powershell
cd "D:\Rhema\TDC ERPS\RHEMA-ERP\frontend"
npm run dev
```

**B4. Open the app** at http://localhost:3000 and sign in:

| Username | Password | Role |
| --- | --- | --- |
| `admin` | `Admin123!` | SuperAdmin — use this for the demo |
| `employee` | `Employee123!` | Employee — the self-service view |

---

## C. Switch back to development

Exactly the same, with one word changed.

**C1.** Stop the API (`Ctrl+C`, or the command in B1).

**C2.**

```powershell
powershell -File .\scripts\Start-ErpApi.ps1 -Database Dev
```

The frontend does not need restarting. Refresh the browser.

---

## D. Check which database you are on

Do this whenever you are unsure. **This is the check that stops you demoing the wrong data.**

```powershell
powershell -File .\scripts\Test-ErpApiDatabase.ps1
```

```
  YES  The API on port 5000 is serving ErpSystemDB_UAT.
```

or `NO`, with the command to fix it. It works by reading one employee id out of the demo database
and asking the API for that employee: ids are minted on every rebuild, so only the right database
answers. **Do not judge by row counts any more.** The development database also carries TDC staff
numbers, and other teams' fixtures move the totals every time master is merged, so "127 means
UAT" stopped being true on 2026-09-05. On 2026-09-07 a development API left running on port
5000 was mistaken for the demo one and an entire rebuild's transactions went into the wrong
database; this check, and the same probe inside the rebuild script, exist so that cannot recur.

---

## E. Reset UAT to clean

Re-run section A. It drops and rebuilds. Do this if a demo rehearsal left junk behind, or if
somebody pointed a harness at it.

---

## F. The demonstration dataset — what section A now installs

`New-UatDatabase.ps1` runs `seed-hr-demo` after `seed-hr-all`. That installs a **synthetic** workforce on
TDC's real establishment and everything the HR and SHE screens need to show:

| What | Where it comes from |
| --- | --- |
| 103 staff on the 123 TDC established posts, ~20 left vacant, MD = TDC/00001, every unit headed, every employee with a line manager | `TdcDemoWorkforceSeeder` — deterministic, so names and numbers are the same on every rebuild |
| Payroll membership: 99 staff on payroll, each with a Payroll employee profile, salary basis and default bank method; 4 off payroll (three national service personnel on an allowance, one contractor on invoice — the four most junior posts, TDC/00100–00103) | `TdcDemoWorkforceSeeder` — HR's `IsOnPayroll` flag AND payroll's profile rows, so the payroll reconciliation screen opens clean |
| 9 leave types, Ghana statutory holidays for two years | `TdcDemoLeaveCalendarSeeder` |
| Awards, benefits, emoluments, medical, orientation, appraisal, SHE, external associates | the nine seeders that were previously deferred |
| Nine persona logins (`hr.head`, `she.officer`, `she.manager`, `head.dev`, `staff`, `new.hire`, `gm.ops`, `md.tdc`, `auditor`), password `Demo123!`, each linked to a seeded employee. Since 2026-09-03 the safety function is its own role pair (DR-10): `she.officer` is the **Safety Officer** on the Environmental Officer post (Cynthia Sarpong, TDC/00081; fallback HSE Assistant) and works the SHE desk; `she.manager` is the **SHE Manager** on the HSE Supervisor post (Josephine Appiah, TDC/00071) and also holds the Safety (SHE) settings tree (`admin.she`) and deletion. HR is read-only in SHE; the Safety (SHE) menu gates on `she.access`. On a database seeded before that date the seeder re-binds `she.officer`, removes HR from it, creates `she.manager`, and revokes `HR.She.Write` from the HR role on the next start — but the clean path is a rebuild (section E) | `TdcDemoPersonaSeeder`, `DatabaseSeedingService` |
| 25 HR workflow definitions (leave, travel, requisition, separation, movement, discipline …) | `seed-workflows` (`EnsureHrWorkflowsSeededAsync`) |

**Document uploads need a scanner running.** A clean malware scan is mandatory for all 29 HR upload
categories and no tenant policy can opt out, so on a machine with no ClamAV every upload is refused
with `FILE_VIRUS_SCAN_INCOMPLETE`. For a demo laptop:

```powershell
powershell -File .\scripts\Start-DemoVirusScanner.ps1     # -Stop to shut it down
```

⚠ That is a **stub**: it reports every file as clean and scans nothing. Demo laptops only — never on
a shared or networked machine, and never tell an audience that files are being scanned while it is
running. A real deployment runs clamd on 127.0.0.1:3310 and needs none of this.

**The transactional layer** — leave requests, travel, requisitions, cases, claims, appraisals,
everything that sits in an approval queue or carries a number from a sequence — is built **through
the API as the personas**, not by an EF seeder, because a row written around the service is a row
the screens then read wrongly. Section A's fifth step does this for you by calling
`scripts/Invoke-UatDemoScenarios.ps1`, which runs `dev-harness/hr-demo-smoke/scenarios.mjs`: one
module per area under `scenarios/`, each written as an "ensure" step, so re-running completes
whatever an earlier run left half-done and never duplicates.

**The coverage rule (agreed 2026-09-04).** Every screen in the five books opens on real data, and
**every HR/SHE entity a user can create has at least one seeded row.** Two checks enforce it and
both run inside section A:

| Check | What it proves | Input |
| --- | --- | --- |
| `verify-tables.mjs` | every table marked `required` in `demo-coverage-manifest.csv` holds a live row. 42 system-generated tables (reminder runs, dispatch logs, import batches, snapshots) are `excluded` — filling them would be inventing audit history | the manifest, next to the script |
| `verify-runbook.mjs` | every staff number, person, record number and name the six books cite exists, **and** every headline number they state | `runbook-claims.json`, extracted from `dev-harness/hr-demo-smoke/runbook/*.html` — regenerate it when a book changes — plus `runbook-counts.json`, one query per stated number |

To run them by hand, or to re-run one area while the API is up against UAT:

```powershell
cd "D:\Rhema\TDC ERPS\dev-harness\hr-demo-smoke"
node scenarios.mjs --list           # the modules, in run order
node scenarios.mjs --only 050       # one module (prefix or a word from its name)
node verify-tables.mjs --area she   # one area, every table listed with its count
node verify-tables.mjs --missing    # every required table still empty
node verify-runbook.mjs --book 3    # one book
node personas.mjs                   # proves the nine logins
node smoke.mjs --user hr.head       # optional: every screen-opening read, expect "0 crash"
```

`DEMO_API` and `DEMO_DB` override the API base URL and database name when the demo database is
not `ErpSystemDB_UAT` on port 5000.

The demo runbook that uses all of this — Books 0 to 4 and the cheat sheet — lives with the demo
pack in `dev-harness/hr-demo-smoke/runbook/`, **outside this repository**. It is a printed
deliverable that changes with every rehearsal, and it belongs beside `runbook-claims.json`, which is
extracted from it and is what `verify-runbook.mjs` checks the database against.

**Still deliberately synthetic:** the people, the salaries, the grade bands, who is and is not on
payroll, the payroll profiles, the incidents and claims. Replace from the real employee file and the
HR questionnaire before this database is used for anything but a demonstration.

⚠ **Payroll's own "new employee profile" cannot be demonstrated from the Payroll screen** on any
database: its upsert fails to create a profile (cross-module defect #23, recorded for the payroll
owner). The seeder writes the profiles directly, and HR-driven enrolment falls back to a direct
insert, so everything HR-side works; only the Payroll → Employee Profiles → *create* path does not.

## G. Two rules

**1. Never run the dev-harness suites against UAT.** They create fixtures on every run — employees
named `Actor…`, leave types named `Lane4 Leave 194151`. That is exactly how the development
database became 91% robots. Always switch to Dev (section C) before running anything in
`dev-harness/`.

**2. Never build UAT with `rebuild-db`.** `rebuild-db` builds tables from the EF model and then
stamps all migrations as applied, so anything that exists only inside migration SQL is missing
while the database claims to be current. Since master's disposable baseline that is most of the
platform's business rules: the chain creates **500 guard triggers**, 7 functions, a view and 65
more default constraints, and `rebuild-db` creates none of them. Such a database also cannot be
migrated forward — several of master's migrations patch a guard that must already exist, and
refuse. That is how the previous UAT stalled at merge #11 (2026-09-27). `New-UatDatabase.ps1`
therefore builds through the chain (section A). Details in
`CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` sections 21 and 31.

---

## H. When something looks wrong

| Symptom | Cause | Fix |
| --- | --- | --- |
| `Database 'X' does not exist` | Typo, or UAT never built | Run section A |
| `REFUSED: 'ErpSystemDB' is the development database` | You aimed the build script at dev | Working as intended. Use the default name. |
| Every login returns 400, `IDX10703: key length is zero` | JWT key not picked up | The launcher reads it from user-secrets. If it cannot, set `$env:JwtSettings__SecretKey` yourself. |
| API starts, every request 500s | Wrong or half-built database | Run section D, then rebuild UAT |
| Port 5000 already in use | An API is still running | Section B1 |
| Frontend shows old data after switching | Browser cache | Hard refresh, or sign out and in |
| Refusals appear as 500 with a stack trace | Running in `Development` | Restart without `-Environment Development`; Staging is the default and is correct |

---

## I. Advanced: both databases at once

Only if you want to compare side by side.

```powershell
powershell -File .\scripts\Start-ErpApi.ps1 -Database Dev -Port 5000
powershell -File .\scripts\Start-ErpApi.ps1 -Database Uat -Port 5010
```

The frontend follows `NEXT_PUBLIC_API_URL` in `frontend/.env.local`, which is
`http://localhost:5000/api`. To point the app at the UAT one instead, change the port there and
restart `npm run dev` — Next.js reads that file only at startup.
