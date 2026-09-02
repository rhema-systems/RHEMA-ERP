# HR Verification Harnesses — Where They Are, How to Run Them, What Green Proves

**Created:** 2026-09-02. **Why:** `HR-DOCUMENTATION-CATALOGUE.md` listed a testing guide as
missing; the practice existed only in 25 README files outside the repo and in build-plan
footnotes. This page is the guide. It records conventions, not results — per-area assertion
counts live in the build plans and `docs/HR-FINISH-PLAN.md`.

---

## 1. Where

`D:\Rhema\TDC ERPS\dev-harness\hr-*` — **outside the repo, deliberately** (no fixtures or probe
output in git). 25 directories as of 2026-09-02: `hr-assets`, `hr-awards`, `hr-company-schedule`,
`hr-consulting`, `hr-demo-smoke`, `hr-discipline`, `hr-employee-docs`, `hr-employee-relations`,
`hr-finish-lane4`, `hr-jobarch`, `hr-medical`, `hr-movements`, `hr-orientation`,
`hr-payroll-membership`, `hr-performance`, `hr-portal`, `hr-probation`, `hr-recruitment`,
`hr-safety`, `hr-separation`, `hr-succession`, `hr-tierb-tail`, `hr-training`, `hr-travel`,
`hr-w3-permissions`. Each is Node (`.mjs`), cloned from its predecessor: `api.mjs` (login +
request helpers), `setup.mjs`/`fixtures.json` (fixture creation), `run-slice<N>.mjs` per slice, a
`README.md` with the recipe and per-slice notes. Some carry `clamd-stub.mjs` (a fake virus
scanner) — the repo now also has `scripts/demo-virus-scanner-stub.mjs` / `Start-DemoVirusScanner.ps1`.

The harnesses are **not** `dotnet test`. `tests/ErpSystem.Api.Tests/` holds the unit/contract
tests other modules use (e.g. `Services/Finance/FinanceConsumerContractAssertions.cs`, which any
HR→Finance integration must ship with per the Finance owner's rule). HR's verification is
end-to-end against a running API and the real database.

## 2. How — the environment, and the three ways it silently lies

### 2.1 Start the API in Staging with the JWT key passed in

```powershell
cd 'D:\Rhema\TDC ERPS\RHEMA-ERP\src\ErpSystem.Api'
$line = (dotnet user-secrets list | Select-String '^JwtSettings:SecretKey = ').ToString()
$env:JwtSettings__SecretKey = $line.Substring($line.IndexOf(' = ') + 3)
$env:ASPNETCORE_ENVIRONMENT = 'Staging'; $env:ASPNETCORE_URLS = 'http://localhost:5000'
dotnet bin\Debug\net8.0\ErpSystem.Api.dll
```

(`scripts/Start-ErpApi.ps1` wraps this for the demo.) Then, from the harness folder:
`node run-slice<N>.mjs`.

**Why Staging.** In Development, `app.UseDeveloperExceptionPage()` (`Program.cs:402`) is
registered *after* `GlobalExceptionHandlingMiddleware` (`:388`), so it catches first and **every
refusal is a 500 with a stack trace**; the exception mapping never runs. Status-code assertions
fail for that reason alone and look like authorization defects. Affects every module; swapping the
two lines is a one-liner that changes every module's dev behaviour, so it has been left alone.

**Why the key.** Staging has no user-secrets. Without the key, every login 400s with
`IDX10703: key length is zero`. ⚠ **The symptom is not a dead API — it is a live one that cannot
authenticate.** `/health` answers 200, the log says `Now listening` and `Hosting environment:
Staging`, the sweeps run, and every authenticated request throws from `AuthenticationMiddleware`.
**Log in once before starting a suite; a health check is not proof.** There is one
`appsettings.json`, so Staging uses the same database — no other drift.

**Why the built DLL, not `dotnet run`.** `Properties/launchSettings.json` sets `Development` and
wins over the environment variable; `ASPNETCORE_ENVIRONMENT=Staging dotnet run` starts in
Development. Either run the DLL as above or pass `--no-launch-profile`. Confirm `Hosting
environment: Staging` in the startup log before trusting a run. This has been re-learned three
times; every harness README carries the warning.

### 2.2 The user builds; you stop the API first

Never run `dotnet build`. Kill the running `ErpSystem.Api` process (it locks its own output
DLLs), then stop and ask the user to build, and continue from their result. A "fast EF build"
exists that hides `hrdev` migrations — a migration is inert until listed in
`FastBuildMigrationMetadata`.

### 2.3 Other environment facts

- SQL Server (`MSSQLSERVER`) is often stopped after a reboot despite Automatic; starting it needs
  elevation — ask the user.
- Rebuilding the database (`rebuild-db`, `New-UatDatabase.ps1`) builds from the **EF model**, not
  migrations; migration-only objects (a Procurement check constraint) are lost. Never for go-live.
- Two databases exist (`ErpSystemDB`, `ErpSystemDB_UAT`); one API on port 5000 serves whichever
  the connection string names — check before asserting counts.
- Permission grants seed via the **`seed-db` command**, not API startup. A post-build green
  harness may be riding `HrPermissionRoleFallbackAuthorizationHandler` (which grants any `HR.*`
  requirement to SuperAdmin/TenantAdmin/Admin/HR/legacy-HR roles) rather than seeded rows. Run
  `seed-db`, verify the grant rows in the DB, then re-run before calling a permissions slice green.
- `Permissions.Description` is `nvarchar(500)`; an over-long description fails the whole
  permission save.
- Uploads through the controlled gate need a scanner for categories with
  `SystemCleanScanRequired` (SHE documents, medical documents): run `node clamd-stub.mjs`
  alongside, or every upload 422s.
- `SensitivePolicy`-rate-limited endpoints allow **5 requests per minute** — a suite that hits one
  in a loop trips it.

## 3. Fixtures — conventions

- **Namespace every fixture** by area and slice (`a15v_*`, `a10v_*`, `lane4.hr`, `w3.employee`)
  and create idempotently (probe by login first — a duplicate `CreateUser` returns a bare 500).
- **Pass the admin token's `tenant_id` on user creation** (FK 547 otherwise).
- **Link harness users to employees** (`POST /api/UserEmployeeLink/link-user-to-employee`) before
  any self-service path is exercisable. ⚠ Linking an already-linked employee fails silently and
  the self reads go 403 — mint a fresh employee (`W3EMP`) rather than reusing one.
- **Two actors per role minimum** wherever `preventInitiatorApproval` is true, or the submitter
  hits their own gate and Approved is unreachable.
- **Two actors per gate**: an HR-desk user *and* a TenantAdmin (`lane4.hradmin`) — Admin-tier
  actions (`HR.<Area>.Admin`) are held only by SuperAdmin/TenantAdmin, and some also want an
  employee-linked actor, so an unlinked admin is refused too.
- **The two-actor rule for guards**: HR bypasses its own guards. A refusal that only HR could
  trigger tests nothing; test with the employee actor.
- **Roles that do not exist in the dev database:** `TDC_INTERNAL_AUDIT` and
  `TDC_MANAGING_DIRECTOR` (47 roles, neither present). `hr-separation` `run-slice8` and
  `run-slice10` cannot pass there; the demo seeder creates the auditor role on the UAT database.
- **Fixture rows that must be derived from the stamp**: anything under a period+location or
  number uniqueness (SHE performance snapshots, monthly reports) collides with the previous run's
  rows otherwise.
- **Date-sensitive rows**: a grade placement must be posted at midnight today or an as-of-date
  read misses it until tomorrow; a `for-acknowledgement` list shows only Approved/Active risk
  assessments — approve the fixture before reading.
- **Commit ordered fixtures out of order** so a correct-ordering assertion cannot pass by
  insertion luck.
- **Cleanup steps need assertions too**; one that fails silently for a whole slice leaves the
  next run colliding.

## 4. What green proves — and what it doesn't

1. **Status-code harnesses prove the GATE, not the FEATURE.** Area 11's post-hoc content audit
   failed 20 of 37 after every status assertion was green; ~24 endpoints had never executed.
   Assert **content** on every read (resolved names, counts, the field the panel edits), and run
   the happy path before building UI on it.
2. **The positive assertion is the one that catches a dead feature.** A file full of refusals
   passes while the entitled party cannot reach the action (area 15b: a permission gate admitted
   only HR, the service required the named reviewer, the intersection was empty). Assert that the
   person who is supposed to do the thing *can*.
3. **Fewer assertions with zero failures is a regression signal**, not a clean run — an assertion
   that stopped executing is one that stopped protecting.
4. **A UI-payload probe is not optional.** Probe the WRITES with the shape the screen will send
   before any TypeScript is written — a type written from an endpoint name is fiction that
   type-checks (four such unions in travel, four in job architecture). An empty read gives no
   shape.
5. **Read the DTOs for writes; probe the reads.** And grep for the **constant**, not just the
   literal (three misses in one slice).
6. **The workflow assertion pair** (`HR-WORKFLOW-ENGINE-INTEGRATION.md` §7): submit refused with
   no definition; engine refuses an approve the role gate admitted.
7. **Frontend "verification" is `tsc` + lint + route-file resolution**, on a scoped tsconfig
   against a stashed baseline — `npm run type-check` crashes on the clean tree, and there is no
   browser-automation tool. No screen has been rendered by a harness. Say so in the slice notes.
8. **ModelState runs before the controller**: a refusal probe's body must satisfy every
   `[Required]`/`[MinLength]` or the 400 answers before the code under test. `[Required]` on a
   value type always passes. `answers: []` is rejected by validation and proves nothing about
   entitlement.
9. **Distinguish the actor guard from the permission gate** by the body: `SheApiControllerBase`
   and `AttendanceControllerBase` answer 403 "not linked to an employee record" for an unlinked
   user, and an actor-guarded desk op can answer **401** for the unlinked HR fixture after the
   gate admitted it — assert *not-403*, not 200.
10. **Three stale assertions will condition you to dismiss a real one.** When a suite that was
    green fails after someone else's commit, re-read each failure against the code before deciding
    it is drift (`rejects` is stale in eleven harnesses; the `hr-w3-permissions` slice-11 employee
    slot drifted to an estate fixture).

## 5. The static instruments, and why they cry wolf

`scripts/hr-coverage/` (01 endpoint census, 02 frontend callers, 03 DTO gaps, 04 ledger
companion) finds backend features with no UI. Trust only the **01 ∩ 02 intersection**; of 149
REVIEW endpoints only 65 were real, a controller is rarely one verdict, and every scan run to
date flagged far more than it found (19 controllers flagged where one was real). **Hand-verify
before acting.** The inventory must know the area base classes (`SheApiControllerBase`,
`AttendanceControllerBase`, `MedicalControllerBase`, `HrControllerBase`) or it under-counts by 76
controllers. ⚠ `04_build_ledger.py` now writes `scripts/hr-coverage/out/HR-CLOSURE-LEDGER.generated.md`
(gitignored) and must never overwrite `docs/HR-CLOSURE-LEDGER.md` — it once deleted 697 hand-curated
lines. `git checkout -- <file>` restores from the **index**, not HEAD; copy before regenerating.

## 6. Recording a run

In the area's README: slice, assertion count, "green twice against the rebuilt API", which
earlier suites were re-run as the no-regression gate (W3 re-runs the whole ladder:
39/23/47/27/90/66/167/142/82/100), the fixtures it needed, and any trap it hit. In
`docs/HR-FINISH-PLAN.md`: the lane checkbox with the counts. In the closure ledger: nothing by
hand unless a decision changed — and re-verify the ledger's section F **after** building, not
only before (lane 3 closed 14 rows the table still said BUILD for).

See also `HR-WORKFLOW-ENGINE-INTEGRATION.md` §7, `HR-SHE-INTEGRATION-AND-BOUNDARIES.md` §6,
`docs/UAT-DEMO-DATABASE.md`.
