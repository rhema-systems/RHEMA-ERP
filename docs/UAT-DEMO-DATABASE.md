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

**A4.** Wait. It runs four steps and takes a few minutes. You should see, in order:

```
  -> Rebuilding schema from the EF model and running base seeders
  -> Seeding workflow definitions (approvals refuse to submit without these)
  -> Seeding HR reference data and TDC organisation structure
  -> Seeding the DEMO workforce, leave calendar and HR/SHE sample data
```

**A5.** It then prints what it built. It should look like this:

```
    Employees             = 127
    Leave types           = 9
    Public holidays       = 26
    Positions             = 137
    Organisation units    = 41
    Units with a head     = 38
    Active workflow defs  = 90
    Roles                 = 48
    -- demo dataset --
    Demo staff (TDC/...)  = 103
    Demo logins (linked)  = 8
```

Then run the scenarios (section F) to load the transactions.

> The script refuses to touch `ErpSystemDB`. If you typo the name into the dev database it stops
> with `REFUSED:` and does nothing.

---

## B. Switch to UAT and run the app

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
Invoke-RestMethod -Method Post -Uri 'http://localhost:5000/api/auth/login' `
  -ContentType 'application/json' `
  -Body '{"username":"admin","password":"Admin123!"}' |
  ForEach-Object { Invoke-RestMethod -Method Post `
    -Uri 'http://localhost:5000/api/hr/Employees/paged?page=1&pageSize=1' `
    -ContentType 'application/json' -Body '{}' `
    -Headers @{ Authorization = "Bearer $($_.token)" } } |
  Select-Object -ExpandProperty totalCount
```

| Result | You are on |
| --- | --- |
| **127** | UAT — the demo database (103 seeded staff + 24 estate fixtures) |
| **~1750** | Development — full of test fixtures |

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
| Nine persona logins (`hr.head`, `she.officer`, `she.manager`, `head.dev`, `staff`, `new.hire`, `gm.ops`, `md.tdc`, `auditor`), password `Demo123!`, each linked to a seeded employee. Since 2026-09-03 the safety function is its own role pair (DR-10): `she.officer` is the **Safety Officer** on the Environmental Officer post (fallback HSE Assistant) and works the SHE desk; `she.manager` is the **SHE Manager** on the HSE Supervisor post and also holds the Safety (SHE) settings tree (`admin.she`) and deletion. HR is read-only in SHE; the Safety (SHE) menu gates on `she.access`. On a database seeded before that date the seeder re-binds `she.officer`, removes HR from it, creates `she.manager`, and revokes `HR.She.Write` from the HR role on the next start — but the clean path is a rebuild (section E) | `TdcDemoPersonaSeeder`, `DatabaseSeedingService` |
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

**Then run the transactions** — leave requests, travel, requisitions, cases, appraisals, everything
that sits in an approval queue — through the API as the personas:

```powershell
cd "D:\Rhema\TDC ERPS\dev-harness\hr-demo-smoke"
node scenarios.mjs        # 21 scenarios; expect "21 ok, 0 failed"
node personas.mjs         # proves the eight logins
node smoke.mjs --user hr.head   # optional: every screen-opening read, expect "0 crash"
```

The demo runbook that uses all of this is in `docs/demo-runbook/` (Books 0–4 and a cheat sheet).

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

**2. A rebuilt database can never be migrated forward.** `rebuild-db` builds tables from the EF
model and then stamps all migrations as applied, so anything that exists only inside migration SQL
is missing while the database claims to be current. `dotnet ef database update` will fail on it
afterwards. Fine for a demo box you rebuild on demand; **not fine for go-live.** Details in
`CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` section 21.

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
