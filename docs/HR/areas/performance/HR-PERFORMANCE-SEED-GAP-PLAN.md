# HR Performance — seed-data gaps, and the order to do the work in

**Question asked:** are there gaps in the performance seed data to close, and should the known defects
be fixed before or after that work?

**Short answer:** **table coverage is already complete** — all 44 required performance tables carry
rows. What is missing is **shape**: seventeen states the data never reaches, which is why a dozen
screens open empty or read as formalities. And the sequencing is **not** "all defects first" — it is
**three specific defects first, then the seed work, then everything else**, because exactly three
defects would cause you to seed data that lands wrong.

**Measured:** 2026-09-17 against `ErpSystemDB_UAT`.

---

## 1. Table coverage: complete

| | |
|---|---|
| Required performance tables in `demo-coverage-manifest.csv` | **44** |
| …of those, holding at least one row | **44** ✅ |
| Excluded by the manifest as system-generated | 5 — the three snapshot tables, `AppraisalNotifications`, `AppraisalManualAdvanceLogs` |
| Tables sitting at zero | **1** — `AppraisalManualAdvanceLogs`, which is one of the five excluded |

So `verify-tables.mjs` passes and will keep passing. **The instrument cannot see the real gaps**,
because it asks "does this table have a row?" and every one of them does.

> **One small manifest gap:** `GoalRiskSetting` is not listed in the manifest at all. It happens to have
> a row, so nothing is broken — but it is unmonitored. Add it as `required`.

---

## 2. What is actually missing — seventeen shape gaps

Ordered by how much of a screen they unblock.

### Tier 1 — these leave a screen blank or meaningless

| # | Gap | Today | What it costs the demo |
|---|---|---|---|
| **S-1** | **No employee goal is aligned to a unit goal** | 0 of 22 to a unit, 5 to a company goal — and all 5 are non-TDC fixtures | Every unit goal's *"Employee goals cascaded from this goal"* table is **empty**. The cascade — the module's headline story — cannot be shown end to end |
| **S-2** | **No uncompleted conversation** | all 8 are `IsCompleted = 1` | `/hr/performance/conversations` → **My diary** is **empty for every persona** |
| **S-3** | **No open calibration session** | both are Completed and committed | Calibration's **Open** tab is empty; the Pending → InProgress → Completed → Commit lifecycle cannot be shown without creating one live |
| **S-4** | **Every recommendation is Actioned** | 7 of 7 | *Proposed* and **Needs dispatch** tabs are both empty. Needs-dispatch is the most interesting screen in Part VI and is unreachable |
| **S-5** | **Only one appraisal template, unscoped** | 1, Global | The cycle's **Coverage** tab — the best screen in Part II — always reads 100% / 0 without a template / 0 conflicts. Scope resolution, priority tie-breaks, a `Conflict` row and a `NoTemplate` row can never be demonstrated |
| **S-6** | **Only one appraisal cycle** | 1 | No cycle lifecycle (Draft → Open), no *Competing cycles* panel, and **every employee trend chart plots a single point** |

### Tier 2 — these make a screen read as an artefact rather than a feature

| # | Gap | Today |
|---|---|---|
| **S-7** | **Nine goals sit at 0% progress** | only Efua Seidu has progress entries — so the at-risk report is mostly "nobody has touched these", not "these are drifting" |
| **S-8** | **No goal in Draft / PendingApproval / Rejected / Locked / Completed** | statuses are Approved ×19, InProgress, AtRisk, OnTrack. A **Rejected** goal is the valuable one: it is the only thing that lights the *"Returned by the manager"* banner and the manager-feedback quote box |
| **S-9** | **All 102 live interim reviews sit at Pending** | *Awaiting manager* reads 0; every detail page opens with an empty self-assessment |
| **S-10** | **No interim review is a full appraisal** | all light-touch, 0 with a period score — so the **Scoring** tab and `overallPeriodScore` are dead surface |
| **S-11** | **All four proposals sit at Proposed with no figure** | *Out for approval* and *Approved, not yet done* tiles both read 0 |
| **S-12** | **Every peer nomination is Approved and invited** | 10 of 10 — the manager's approve/reject nomination panel has nothing to act on |
| **S-13** | **One check-in, already held** | *Check-ins I run* has nothing outstanding; *Check-ins about me* is empty for everyone but Efua |
| **S-14** | **All three development plans are Active** | the *"Still a draft — the employee has not been told"* banner never renders |
| **S-15** | **One PIP, Active** | no Completed / Unsuccessful / Extended outcome, so the outcome panel and the "extending is not closing" rule are unshowable. Also carries **8 review meetings where it should carry 2** (P-56) |
| **S-16** | **No goal was created from the goal library** | 0 of 22 — every usage figure on the library detail page reads zero (P-10) |
| **S-17** | **Only one settings profile, and no Bonus proposal** | a second profile (e.g. a probation policy with no peer reviews) is what makes "a cycle names exactly one profile" mean something, and what lets the phase rail be seen **dropping** a step. No `Bonus` proposal means the *Bonus amount* field is never live |

---

## 3. The sequencing answer

**Not "fix everything first".** Most of the 70 recorded findings are cosmetic, advisory, or the
settings ghosts — none of them touches the data, and blocking the dataset on them would stall the work
with the most demo value for weeks.

**Not "seed first" either.** Three specific defects will make you seed data that lands wrong, and you
will spend the time twice.

### Wave 0 — fix these three before touching the seed pack *(about a day)*

| Defect | Why it blocks seeding |
|---|---|
| **P-55** — PIP goal `status` and `progressPercent` supplied at create are not honoured | S-15 needs PIP goals with progress. Post them today and they land as *Not started* with a blank percentage. You would write the data, see it wrong, and suspect the seeder |
| **P-28** — journal `entryDate` is ignored; the server stamps the creation date | Any attempt to seed a "written in May, read in November" journal is silently collapsed to the build date. All four existing entries already prove it |
| **P-40** — a per-criterion calibration adjustment on a **KPI** item writes `NumericScore`, which a KPI is not scored from | S-3 and any richer calibration data. Seed an adjustment on a KPI item today and it is recorded and does nothing — you are seeding a dataset that documents the bug |

**And one decision, not a fix: P-39** — the panel's *overall* restatement is overwritten by HR sign-off.
That is as much a design question as a defect (should the panel's number win over the recomputed one?).
**Decide it before seeding**, because the answer changes what a realistic calibration dataset looks
like. The current data already shows the conflict: Cynthia Sarpong's panel figure is 87.0 and her
appraisal reads 88.74.

### Wave 1 — the seed work, in this order

1. **S-1** goal alignment — *the single highest-value change in this list*. `CreateEmployeeGoalDto`
   already accepts `UnitGoalId`, `CompanyGoalId` and `GoalLibraryId`, so this is a few lines in
   `scenarios/060-performance.mjs`: pass the matching unit goal when creating each person's goals. It
   also closes **S-16** at the same time if two of them are created from library templates.
2. **S-2** an unheld conversation, and one overdue — unblocks a screen that is blank today.
3. **S-4** a recommendation left at *Proposed*, and one deliberately at *Approved-but-not-Actioned*.
4. **S-11** drive one salary proposal through: set a figure → submit → approve as `md.tdc` → mark
   applied; leave a second at *Pending approval*. Add one `Bonus` proposal (S-17).
5. **S-3** a third calibration session left at *Pending*, scoped to **Corporate Planning &
   Communications** (9 uncalibrated appraisals; everything under the two directorates is already
   committed).
6. **S-7 / S-8** progress entries for `gm.ops`, `head.dev` and `she.officer`, plus one goal rejected
   with feedback and one locked.
7. **S-9** submit 3–4 interim reviews as the appraisees so *Awaiting manager* is non-zero.
8. **S-13** a scheduled future check-in · **S-14** a Draft development plan · **S-12** one nomination
   left Pending and one Rejected.
9. **S-15** a second PIP, closed with *Performance improved*, and a third *Extended*. Dedupe the 8
   meetings down to 2.
10. **S-5 / S-6 / S-17** the structural ones — a second scoped template, a second settings profile, a
    Draft 2027 cycle and a Closed 2025 cycle. These are the largest and the ones to do last, because
    they change what generation produces and will want a rebuild to verify.
11. **S-10** requires a cycle whose profile sets `InterimReviewDepth = FullAppraisal` — so it naturally
    falls out of S-17's second profile rather than being its own task.

> **Keep to the harness's own rule:** build all of this **through the API as the personas**, in
> `scenarios/060-performance.mjs` and `061-appraisal-evaluations.mjs`, written as *ensure* steps. A row
> written around the service is a row the screens then read wrongly — which is exactly how the five
> snapshot-less appraisals of Rule 8 got there.

### Wave 2 — everything else

The settings audit's fix list (ghosts, the two client-side visibility controls, the appeal gate), then
the resolver reconciliation, then the remaining cosmetic findings. **None of these is blocked by, or
blocks, the seed work** — no amount of seed data can make a ghost setting do something, and no seed gap
is caused by one.

---

## 4. Also worth fixing while in there

* Add **`GoalRiskSetting`** to `demo-coverage-manifest.csv` as `required`.
* **Strengthen the instrument.** `verify-tables.mjs` asks "has this table a row?", which every
  performance table now passes while a dozen screens read empty. The cheap improvement is a second
  check with a handful of **shape assertions** — e.g. *at least one employee goal has a `UnitGoalId`*,
  *at least one conversation has `IsCompleted = 0`*, *at least one calibration session is not
  Completed*, *at least one recommendation is `Proposed`*. Ten lines, and it would have caught every
  Tier-1 gap above.
