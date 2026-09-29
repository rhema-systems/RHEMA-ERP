# HR test data for developers

If you work on another module and need HR data (an organisation, positions, employees with line
managers, and logins linked to employees), seed it from the UI. You don't need the command line.

**Administration → HR → HR Settings → Developer Test Data** (`/administration/hr/settings/test-data`)

## Before you start

- Build your database through the migration chain, then run the core seed (`seed-db`). That gives you
  the DEFAULT tenant and the `admin` login. Don't use `rebuild-db`: it skips the platform's guard
  triggers (see `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` § 31).
- Sign in as `admin` (SuperAdmin), or as a TenantAdmin of the DEFAULT tenant.

## The three buttons

Each button includes the ones before it. Every step checks whether it has already run, so pressing a
button twice adds nothing.

| Button | What you get |
| --- | --- |
| **Seed foundation** | Reference data (countries, Ghana's geography, qualifications, languages, job architecture); the TDC organisation (levels, grades, 42 units, 142 positions); its locations; HR's approval workflows. **No people.** Same as `seed-hr-all`. |
| **Seed foundation + workforce** | Adds leave types, the Ghana holiday calendar, about 100 **synthetic** staff on the TDC establishment with their line managers, the approved establishment, contract types and divisions, and the 2026 salary scale. |
| **Seed everything, with logins** | Adds the demo personas (`hr.head`, `head.dev`, `staff`, `she.officer`, `md.tdc`, …), each linked to one of those employees. The page lists every login, the post behind it, and the shared password. |

A seed runs in the background and takes a few minutes. The page shows each step as it finishes.
Only one seed can run at a time.

## What it will not do

- **Run in Production.** The page is disabled when the API runs as Production, unless
  `HrTestData:AllowInProduction` is set to `true`. It works in Development and Staging.
- **Add synthetic staff or logins beside real people.** The workforce and logins buttons are refused
  while the DEFAULT tenant holds any `TDC/` staff record the demo seeder didn't create (imported, or
  entered by hand). Otherwise a demo login with a published password would be linked to a real
  employee. The foundation button stays available.
- **Seed the demo walkthrough.** The awards, medical, SHE, orientation and appraisal sample data, and
  the transactional scenarios the runbooks rely on, are UAT-only. They come from
  `scripts/New-UatDatabase.ps1` (see `UAT-DEMO-DATABASE.md`).

## Behind the page

`GET /api/administration/hr-test-data` returns what is present. `POST
/api/administration/hr-test-data/{Foundation|Workforce|Logins}` starts a run and answers 202; it
answers 403 when disabled or not allowed, 409 when a run is in progress, and 422 when real staff
block it. The service is `HrTestDataSeedService`. It runs the same orchestrators as `seed-hr-all`
and `seed-hr-demo`, so the command line and the page cannot drift apart.
