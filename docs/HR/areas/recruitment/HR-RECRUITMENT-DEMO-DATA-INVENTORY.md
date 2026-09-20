# HR Recruitment — demo data inventory

**Purpose.** Before regenerating demo data for a stakeholder walkthrough of the recruitment module,
establish what the module actually contains, what is in the demo database today, and where the two
disagree. This document is the inventory half; the seeding plan is agreed from it.

**Date:** 2026-09-14 · **Branch:** `hrdev` · **Target database:** `ErpSystemDB_UAT`

## How this was established

| Question | Source |
| --- | --- |
| What entities exist | `src/ErpSystem.Core/Entities/HR/RecruitmentEntities.cs` (62 classes), `StaffRequisitionEntities.cs` (5), `PositionVacancyEntities.cs` (1), `SuccessionPlanningEntities.cs` (talent pool, 3) |
| What tables they map to | `DbSet<>` declarations in `src/ErpSystem.Data/ApplicationDbContext*.cs` |
| What is in the demo database | `sys.partitions` row counts + per-column `GROUP BY` over `ErpSystemDB_UAT` |
| What a stakeholder sees | 44 Next.js routes under `hr/recruitment`, `administration/hr/recruitment`, `careers` |
| What the numbers on screen are built from | `RecruitmentDashboardController.GetDashboard`, `RecruitmentAnalyticsService.GetAnalyticsAsync` |
| How the data is seeded today | `dev-harness/hr-demo-smoke/scenarios/050-recruitment.mjs` (673 lines, API-driven, "ensure" style) |

---

## 1. Headline findings

**F1 — The demo database is four migrations and several features behind the branch.**
Last migration applied to `ErpSystemDB_UAT` is `20260911091344_AddEmployeeSalaryChangeRequest`.
The branch carries seven more, through `20260914063456_AddTalentSegmentOwnership`. Three tables the
recruitment screens read **do not exist in the demo database at all**: `Languages`,
`JobShortlistingCriteriaValues`, `PreEmploymentCheckProviderServices` (also `DisabilityTypes`).
No amount of seeding against the current database fixes this — it has to be rebuilt first.

**F2 — Coverage was the goal, and coverage is what we got.** The existing manifest asks for
*≥ 1 row per required table*, and 71 of 73 tables satisfy it. That is why the tables looked fine and
the demo did not: one row proves a table is wired, it does not fill a screen. The whole module is
carried by **2 vacancies, 7 candidates, 8 applications, 2 interviews, 2 offers, 1 hire**.

**F3 — Almost every status dimension is collapsed onto one or two values.** A recruitment module is
demonstrated through its *states*, and the demo data has none of the spread (§3). Both vacancies are
Published; every requisition is `Replacement`; every application came from `CompanyWebsite`; both
interviews are round 1 Panel; no offer is in `Sent`; every probation period is `Active`.

**F4 — The recruitment dashboard and analytics screens are structurally empty, not just thin.**
They aggregate on exactly the dimensions that are collapsed:

| Tile / chart | What it reads | What it finds today |
| --- | --- | --- |
| Offers Made | offers with status **`Sent`** | **0** — the two offers are Draft and Accepted |
| Expiring Offers | `Sent` offers expiring in 7 days | **empty** |
| Interviews Scheduled | interviews dated **today** | **0** |
| SLA Alerts | active vacancies past their deadline | **0** — both deadlines are future and identical |
| Time-to-fill / time-to-hire | hires with an **actual** start date this year | **empty** — the one hire has `ActualStartDate = NULL` |
| Time-to-shortlist | vacancies with `ShortlistCompletedAt` | **empty** — both are NULL |
| Source effectiveness | applications grouped by source | **one bar** — all 8 are CompanyWebsite |
| Offer outcomes | accepted vs declined | 1 vs 0 — no declined offer exists |
| Recruiter load / vacancy ageing | vacancies grouped by `RecruiterId` | **both vacancies have no recruiter assigned** |

**F5 — Two reference lookups a recruitment form depends on are empty or absent.**
`RelationshipTypes` has **0 rows**, so the referee "relationship" dropdown on the candidate dossier
is empty (`JobCandidateReferee` implements `IRelationshipTypeConsumer`). `Languages` is absent, so
`GET /public-recruitment/catalogue/languages` — served to the careers site application form — has
nothing to return.

**F6 — Visible untidiness in the current data.** `REQ-2026-00005` and `REQ-2026-00006` are Draft
duplicates left by a re-run, titled `Replacement — Records Assistant` / `Replacement — Geodetic
Engineer` where the real ones read `Geodetic Engineer — Survey / Geodetic`. Both vacancies share the
same deadline timestamp to the millisecond (`2026-09-28 09:39:40.698`). The interview dated
2026-09-07 is now in the past because the database was built a week ago and all dates are relative
to build day.

---

## 2. Entity inventory

73 tables, grouped by the funnel stage they belong to. ⚠ **This list missed
`EmployeeOathsOfSecrecy`** — see §10.2 for the table, and for why two independent, name-keyed
filters were both blind to it. **Rows** = current `ErpSystemDB_UAT`.
**Door** = how a row can be created (API = a controller endpoint exists; *system* = written as a
side effect; *EF* = seeder only). **Verdict**: ✅ adequate · ⚠ present but too thin to demo ·
🔴 blocking (empty, absent, or collapsed onto one state).

### A. Establishment and demand — the seat and the request to fill it

| Table | Rows | Door | Verdict |
| --- | ---: | --- | --- |
| `PositionVacancies` | 38 | API (`/hr/recruitment/establishment`) | ✅ 36 Open, 2 Filled — the best-populated table in the module |
| `StaffRequisitions` | 6 | `StaffRequisitionsController` | 🔴 all one type; 2 are duplicate drafts; no rejected / on-hold / fulfilled |
| `StaffRequisitionCosts` | 3 | API | ⚠ only one requisition carries costs — cost-per-hire has one input |
| `StaffRequisitionAttachments` | 1 | API (scan-gated upload) | ⚠ |
| `StaffRequisitionComments` | 2 | API | ⚠ |
| `StaffRequisitionHistories` | 6 | *system* | ✅ follows the writes |

### B. Vacancy, advertising and the screening set-up

| Table | Rows | Door | Verdict |
| --- | ---: | --- | --- |
| `JobVacancies` | 2 | `JobVacancyController` | 🔴 both Published, both unassigned, identical deadline, no shortlist date |
| `JobVacancyAttachments` | 2 | API | ⚠ |
| `JobVacancyStatusHistories` | 6 | *system* | ✅ |
| `JobPostings` | 4 | `JobPostingController` | ⚠ 2 channels of 9; none expired or closed |
| `JobPostingAttachments` | 1 | API | ⚠ |
| `RecruitmentPipelines` | 1 | `RecruitmentPipelineController` | ⚠ one pipeline ("TDC Standard Recruitment") |
| `RecruitmentPipelineStages` | 7 | API | ✅ |
| `VacancyPipelineStageAssignments` | 4 | API | ⚠ 4 of 14 possible (7 stages × 2 vacancies); all one status |
| `JobShortlistingCriterias` | 8 | API (`/vacancies/[id]/screening`) | ⚠ |
| `JobShortlistingCriteriaValues` | **absent** | derived when a list criterion is saved | 🔴 table not in the database (migration `AddCriteriaCatalogueValues` unapplied) |

### C. Candidates — the CRM side

| Table | Rows | Door | Verdict |
| --- | ---: | --- | --- |
| `JobCandidates` | 7 | `JobCandidateController` | 🔴 all `TalentPoolStatus = Active`; no passive / dormant / converted |
| `JobCandidateQualifications` | 11 | API | ⚠ |
| `JobCandidateWorkHistories` | 10 | API | ⚠ |
| `JobCandidateReferees` | 10 | API | 🔴 relationship dropdown has no options (`RelationshipTypes` = 0) |
| `JobCandidateSkills` | 16 | API | ⚠ |
| `JobCandidateLanguages` | 4 | API | 🔴 `Languages` catalogue absent |
| `JobCandidateInterests` | 7 | API | ⚠ |
| `JobCandidateDocuments` | 6 | API (scan-gated) | ⚠ |
| `JobCandidateNotes` | 6 | API | ⚠ |
| `CandidateTalentSegments` | 3 | `TalentPoolController` (singular) | ⚠ ownership columns added 2026-09-14, unapplied here |
| `CandidateSegmentMemberships` | 3 | API | ⚠ 3 memberships across 3 segments and 7 candidates |
| `CandidateEngagementEvents` | 4 | API | ⚠ |

### D. Applications and screening decisions

| Table | Rows | Door | Verdict |
| --- | ---: | --- | --- |
| `JobApplications` | 8 | `JobApplicationController` | 🔴 3 of 16 statuses; one source; all on one vacancy |
| `JobApplicationStageHistories` | 0 | *system* | 🔴 empty — the stage timeline on an application detail page has nothing |
| `JobApplicantTestResults` | 3 | API | ⚠ |
| `JobApplicantCommunications` | 15 | API | ✅ |
| `ShortlistReviews` | 6 | API | ⚠ neither vacancy's shortlist was ever submitted for approval |
| `ShortlistDecisionLogs` | 6 | *system* | ✅ |

### E. Interviews

| Table | Rows | Door | Verdict |
| --- | ---: | --- | --- |
| `JobInterviewQuestionTypes` | 4 | `InterviewQuestionBankController` | ⚠ |
| `JobInterviewQuestionDetails` | 17 | API | ⚠ 17 questions across 4 types |
| `InterviewQuestionPresets` | 1 | `InterviewQuestionPresetController` | ⚠ |
| `InterviewQuestionPresetItems` | 4 | API | ⚠ |
| `JobInterviews` | 2 | `JobInterviewController` | 🔴 both round 1, both Panel, one now in the past |
| `JobInterviewPanelists` | 6 | API | ⚠ |
| `JobInterviewExternalPanelists` | 2 | API | ⚠ |
| `JobInterviewees` | 5 | API | ⚠ |
| `JobInterviewQuestions` | 8 | API | ⚠ |
| `JobInterviewSelectedQuestions` | 14 | API | ⚠ |
| `JobInterviewScoreSummaries` | 6 | API | ⚠ |
| `JobInterviewScoreEntries` | 42 | API | ✅ |
| `JobInterviewScoreDrafts` | 0 | *system* (scoring-screen autosave) | — excluded by design |

### F. Offers

| Table | Rows | Door | Verdict |
| --- | ---: | --- | --- |
| `JobOffers` | 2 | `JobOfferController` | 🔴 Draft + Accepted only; 11 other statuses unrepresented; **no `Sent` offer** |
| `JobOfferBenefits` | 7 | API | ⚠ |
| `JobOfferNotes` | 3 | API | ⚠ |
| `OfferCandidateTokens` | 1 | *system* (on send) | ⚠ the candidate-facing offer-response page has one live token |

### G. Pre-employment checks and hire

| Table | Rows | Door | Verdict |
| --- | ---: | --- | --- |
| `PreEmploymentCheckTemplates` | 1 | `administration/hr/recruitment/check-templates` | ⚠ |
| `PreEmploymentCheckTemplateItems` | 5 | API | ⚠ |
| `PreEmploymentChecks` | 1 | `JobHireController` | 🔴 one check, status Pending — no cleared, cautioned, failed or waived case |
| `PreEmploymentCheckItems` | 6 | API | ⚠ |
| `PreEmploymentCheckProviderServices` | **absent** | check-templates page | 🔴 table not in the database (migration `AddPreEmploymentCheckProviders` unapplied) |
| `ReferenceCheckResponses` | 2 | API | ⚠ |
| `JobHireRecords` | 1 | `JobHireController` | 🔴 one record, no actual start date — every speed metric is blank |

### H. Onboarding (adjacent — owned by orientation, entered from a hire)

| Table | Rows | Door | Verdict |
| --- | ---: | --- | --- |
| `OnboardingPlanTemplates` | 1 | `administration/hr/orientation/onboarding-templates` | ⚠ |
| `OnboardingTaskTemplates` | 9 | API | ⚠ |
| `OnboardingPlans` | 6 | `OnboardingPlanController` | ⚠ 5 NotStarted, 1 InProgress — none completed or overdue |
| `OnboardingTasks` | 66 | API | ✅ volume is there; status spread is not |
| `OnboardingTaskComments` | 2 | API | ⚠ |
| `OnboardingAssets` | 4 | API | ⚠ |

### I. Probation (adjacent — where a hire lands)

| Table | Rows | Door | Verdict |
| --- | ---: | --- | --- |
| `ProbationPeriods` | 6 | probation controllers | 🔴 all `Active` — no confirmation, extension outcome or termination on show |
| `ProbationReviews` | 6 | API | ⚠ |
| `ProbationExtensions` | 1 | API | ⚠ |
| `ProbationConfirmingAuthorities` | 5 | admin API | ✅ |
| `ProbationReminderRuns` | 17 | *system* (nightly sweep) | ✅ |
| `ProbationReminderDispatchLogs` | 5 | *system* | ✅ |

### J. Talent pools (succession's, not recruitment's — the naming collision)

`TalentPools` 1 · `TalentPoolTypeDefinitions` 6 · `TalentPoolMembers` 5. These belong to succession
planning and are reached from `/hr/succession`. Recruitment's own CRM is `CandidateTalentSegments`
in group C. **Do not conflate them** — `api/TalentPool` (singular) and `api/talent-pools` (plural)
are different controllers.

### K. Supporting lookups the recruitment forms read

| Lookup | Rows | Consequence |
| --- | ---: | --- |
| `Countries` | 196 | ✅ |
| `Qualifications` | 186 | ✅ catalogue for candidate qualifications |
| `JobDescriptions` | 47 | ✅ vacancies can cite a real JD |
| `JobFamilies` / `JobLevels` | 11 / 8 | ✅ |
| `SalaryGrades` | 8 | ✅ offers can cite a grade |
| `Locations` | 11 | ✅ |
| `RelationshipTypes` | **0** | 🔴 referee relationship dropdown empty |
| `Languages` | **absent** | 🔴 careers-site language picker empty |
| `DisabilityTypes` | **absent** | 🔴 candidate disability field has no list |

---

## 3. State coverage — where the module's vocabulary is unused

Recruitment is demonstrated by moving records between states. This is the current spread, decoded
from the enums in `src/ErpSystem.Core/Enums/`:

| Dimension | Values in the model | Present in demo data | Missing |
| --- | ---: | --- | --- |
| `StaffRequisitionStatus` | 9 | Draft, Submitted, Approved | UnderReview, Rejected, OnHold, Cancelled, PartiallyFulfilled, Fulfilled |
| `StaffRequisitionType` | 6 | Replacement | NewPosition, Contract, Internship, Backfill, Other |
| `JobVacancyStatus` | 12 | Published | Draft, PendingApproval, Approved, Rejected, ClosedForApplications, Shortlisting, Interviewing, OfferStage, Filled, Cancelled, OnHold |
| `ShortlistApprovalStatus` | 4 | NotSubmitted | PendingApproval, Approved, Rejected |
| `JobPostingChannel` | 9 | InternalPortal, CompanyWebsite | LinkedIn, JobBoard, Agency, Indeed, Glassdoor, Newspaper, Other |
| `JobPostingStatus` | 5 | Published | Draft, Expired, Closed, Removed |
| `ApplicationStatus` | 16 | New, Shortlisted, Rejected | Submitted, UnderReview, InterviewScheduled, InterviewCompleted, AssessmentPending, PreEmploymentCheck, OfferExtended, OfferAccepted, OfferDeclined, Withdrawn, Hired, Waitlisted |
| `ApplicationSource` | 11 | CompanyWebsite | the other 10 — **this is the source-effectiveness chart** |
| `JobInterviewType` | 10 | Panel | Screening, OneOnOne, Technical, CompetencyBased, CaseStudy, Presentation, GroupAssessment, Final, Other |
| `JobInterviewStatus` | 6 | Scheduled, Completed | Rescheduled, InProgress, NoShow, Cancelled |
| `JobOfferStatus` | 13 | Draft, Accepted | **Sent**, PendingApproval, Approved, Negotiating, Declined, Withdrawn, Expired, OnHold, ConditionallyAccepted, ChecksCleared, Rejected |
| `PreEmploymentCheckStatus` | 6 | Pending | InProgress, Completed, CompletedWithCaution, Failed, Waived |
| `JobHireStatus` | 5 | OnboardingInProgress | PendingOnboarding, OnboardingCompleted, Active, Cancelled |
| `OnboardingStatus` | 5 | NotStarted, InProgress | Completed, Overdue, Cancelled |
| `TalentPoolCandidateStatus` | 6 | Active | Passive, Dormant, Expired, Converted, OnHold |
| `VacancyStageAssignmentStatus` | 6 | one value | NotStarted/InProgress/Completed/Overdue/Escalated/Skipped spread |
| `ProbationStatus` | 5 | Active | Completed, Terminated, PendingConfirmation, ConfirmationApproved — **but see §10.3: six people currently on probation is the intended story, not a gap** |

**17 of 17 dimensions are under-represented; 9 of them sit on a single value.**

---

## 4. Screen-by-screen exposure

44 routes. Grouped by what a stakeholder would experience today.

**Opens empty or near-empty (🔴):**
`hr/recruitment/dashboard` (four tiles read zero) · `hr/recruitment/hires` (one row) ·
`hr/recruitment/pre-employment-checks` (one pending row) · `hr/recruitment/offers` (two rows, one a
draft) · `careers` and `careers/[id]` (two adverts, both closing the same day) ·
`hr/recruitment/vacancies/[id]/screening` (criteria exist, no submitted shortlist) ·
`administration/hr/recruitment/check-templates/[id]` (providers tab has no table behind it).

**Opens with a single specimen (⚠):** `talent-pool`, `postings`, `interviews`,
`administration/hr/recruitment/pipelines`, `question-presets`, `hr/orientation/onboarding`.

**Opens well (✅):** `establishment` (38 seats), `candidates` (7 dossiers, genuinely detailed),
`applications`, `interviews/[id]/score/[intervieweeId]` (42 score entries), `hr/job-descriptions`
(47), `hr/probation` (6 periods).

---

## 5. What has to happen before seeding

1. **Rebuild `ErpSystemDB_UAT` from the current branch.** Three tables the plan needs do not exist
   yet. `scripts/New-UatDatabase.ps1` is the only path (no migration creates `Employees`). This is a
   build, so it is yours to run.
2. **Seed the two empty lookups** — `RelationshipTypes` and `Languages` — in whichever seeder owns
   them, so the candidate forms have their dropdowns. (`Languages` is already claimed by
   `seed-hr-all` per the manifest; confirm it runs.)
3. Then regenerate the recruitment demo data against it.

---

## 6. Proposed shape of the seed (for agreement, not yet built)

The principle: **one headline story the runbook walks, plus a populated background that makes every
screen and every chart look like a real recruitment function mid-year.**

- **Backfill a year of history** — roughly 12 closed vacancies across the 2026 calendar, each with
  applications, an outcome, and a hire with a real `ActualStartDate`, so time-to-fill, time-to-hire,
  cost-per-hire, source effectiveness and offer outcomes all have curves rather than points.
- **Spread the sources** — applications distributed across all 11 `ApplicationSource` values,
  weighted realistically (website and referral heavy, career fair light).
- **Live pipeline in every state** — 6–8 open vacancies, deliberately staggered: one in Draft, one
  PendingApproval, one just Published, two Shortlisting, one Interviewing, one at OfferStage, one
  OnHold, one past its deadline (to light the SLA alert).
- **~60–80 candidates** with dossiers of varying completeness, spread across all six
  `TalentPoolCandidateStatus` values and across the talent segments.
- **Offers in every meaningful state** — at least one `Sent` and expiring inside 7 days (the
  dashboard tile), one Negotiating, one Declined, one ConditionallyAccepted, one Accepted.
- **Checks and hires** — checks Pending / InProgress / Completed / CompletedWithCaution / Waived;
  hires across all five statuses with three carrying actual start dates.
- **Assign recruiters** to every vacancy so ageing and recruiter-load charts populate.
- **Dates anchored relative to build day**, as the pack already does, and the headline records keep
  their existing numbers (`REQ-2026-00001`, `VAC-000001`, `OFR-000001`) because the runbook quotes
  them.

Delivery would extend `scenarios/050-recruitment.mjs` in the existing "ensure" style, plus a new
history module for the backfilled year, and update `runbook-counts.json` for every number the books
state.

---

## 7. Decisions needed

1. **Scope of "the recruitment module"** — groups A–G are unambiguously recruitment. Do onboarding
   (H), probation (I) and the succession talent pools (J) come in too? They are adjacent and a
   stakeholder following a hire will walk straight into them.
2. **Volume** — is ~12 historical vacancies / ~70 candidates the right density for TDC, or should it
   read as a bigger or smaller organisation?
3. **The backfilled year** — creating dated history through the API means writing `CreatedAt` values
   the API does not accept. Either the history module writes directly via EF/SQL, or the analytics
   charts stay shallow. Direct writes are the pragmatic answer; confirm that is acceptable.
4. **Tidy-up** — delete the two duplicate draft requisitions (`REQ-2026-00005/6`), or leave them as
   evidence that drafts exist?

---

## 8. What was built (2026-09-14)

> ⚠ **Read §9 with this section.** §8 describes the spine and overstates the coverage — the
> first cut of the seeder wrote 12 of the module's 67 tables and left every candidate dossier,
> scorecard and screening record empty. §9 is the table-by-table accounting and the correction.

Decisions taken: scope = **groups A–I** (recruitment plus onboarding and probation); volume =
**~12 historical vacancies, ~70 candidates**; backfill = **written directly via EF**, with the live
pipeline still going through the API.

### 8.1 `TdcDemoRecruitmentHistorySeeder` — the closed 2026 year

`src/ErpSystem.Data/Seeders/TdcDemoRecruitmentHistorySeeder.cs`, wired into
`HrDemoSeedOrchestrator`'s **second pass** (after the API scenarios).

Twelve cycles dated January to July: ten carried to a hire, one closed with no suitable candidate,
one withdrawn under a hiring freeze. Each carries a requisition (with costs), a vacancy, one or two
adverts, four to eight candidates with applications, one or two interview rounds with scored
attendees, an offer, pre-employment checks and — for the ten — a hire record with a **real
`ActualStartDate`**. Two cycles have the first offer declined and the runner-up appointed. Plus three
requisitions that never became a vacancy, covering Under Review, Rejected and Partially Fulfilled.

Why it is in the second pass rather than the first:

- The live records the runbook quotes by number (`REQ-2026-00001…4`, `VAC-000001`, `VAC-000002`)
  must be minted by the scenario first. **History therefore carries numbers above the live records
  despite being older** — a deliberate trade against renumbering Book 1 and the cheat sheet.
- It also repairs what the scenario cannot: it assigns a recruiter to every vacancy that has none
  (the ageing and recruiter-load charts group by exactly that), and pushes one published vacancy six
  days past its deadline, which is the only way the SLA panel gets a row — a deadline is only ever
  set forward through the door.

It advances every number sequence it consumes (REQ, VAC, CAND, APP, OFR, HIR, INT), so a candidate
created live on stage cannot collide with a seeded one. It is deterministic (fixed-seed LCG and
index arithmetic, never `Random`) and idempotent (skipped once any vacancy is Filled).

### 8.2 `scenarios/050-recruitment.mjs` — the live pipeline, sections 16–20

Ten further vacancies driven through the real API and left standing at **Draft, PendingApproval,
Approved, Rejected, Published (×2), ClosedForApplications, Shortlisting, Interviewing and
OfferStage**; ten applicants across the three that are past the advert; four interviews including one
**dated today** (the dashboard tile counts only today) and one each Technical, Screening and
Competency-Based; offers driven to **Sent** (expiring in five days), **Negotiating**,
**ConditionallyAccepted** and **Declined**; and a hire whose start date falls inside the fortnight the
dashboard watches.

⚠ **`OnHold` is unreachable.** `JobVacancyService.AllowedTransitions` has no path to it from any
state, so `change-status` refuses it. The status exists in the enum and cannot be reached through the
door. Recorded here rather than worked around.

### 8.3 Runbook

- Book 1 §5.1–5.4 rewritten to describe a register with states rather than four rows and two
  vacancies, keeping every record number it already quotes.
- **New §5.5 walks `/hr/recruitment/dashboard` and its Analytics tab** — the two screens the whole
  backfill exists for, and which the runbook never opened.
- `/hr/recruitment/adverts` corrected to `/hr/recruitment/postings` in Book 1 and
  `runbook-claims.json` — **the old path is a dead route** and would have 404'd on stage.
- `runbook-counts.json`: the "four requisitions" and "two published vacancies" claims now assert the
  named records rather than whole-table counts, and four claims were added — ten filled vacancies,
  ten hires with an actual start date, exactly one overdue advert, and at least eight application
  sources.

### 8.4 Not changed, and why

- **The two empty lookups need no code.** `RelationshipTypes`, `Languages` and `DisabilityTypes` are
  already seeded by `seed-hr-all` (`RelationshipTypeSeeder`, `LanguageSeeder`, `DisabilityTypeSeeder`
  are wired into `HrSeedOrchestrator`). They are absent from the demo database only because it
  predates them. The rebuild fixes this — **verify it rather than assuming it**.
- `JobApplicationStageHistories` stays `excluded` in the manifest, so the stage timeline on an
  application will still be empty. Reopen that decision separately if the timeline is demonstrated.
- The two duplicate draft requisitions (§1, F6) are scenario artefacts and disappear on a clean
  rebuild; nothing was written to delete them.

### 8.5 Order of operations

1. **Build** (yours to run). The API on port 5000 was stopped so its DLLs are not locked.
2. `scripts/New-UatDatabase.ps1` — rebuilds `ErpSystemDB_UAT` from the current branch, which is what
   brings in the four missing migrations, then runs the seeders, the scenarios, the second seed pass
   and both verifiers. Allow 45–60 minutes.
3. Check the verdict lines: SCENARIOS, REQUIRED, COUNTS, RUNBOOK. The four new count claims are the
   ones that prove the analytics has inputs.
4. Then open `/hr/recruitment/dashboard` and its Analytics tab and confirm no tile reads zero.

**Nothing here has been run yet.** The seeder has not been compiled and the scenario additions have
not been executed — only the JavaScript syntax was checked and the C# reviewed against the entity
definitions.

---

## 9. Table-by-table accounting — who writes what

§8 described the spine and overstated it. Asked directly whether *every* recruitment entity is
seeded, the first version of `TdcDemoRecruitmentHistorySeeder` wrote **12 of the 67** recruitment
tables: it built requisition → vacancy → candidate → application → interview → offer → check → hire
and nothing hanging off them. The sixty-six historical candidates had a name, a phone number and an
employer and **no qualifications, work history, referees, skills, languages, interests or notes**;
the historical interviews had **no panel and no scorecards**; no historical vacancy had **advertised
criteria, shortlist reviews, a decision log or a written assessment**; and the live applicants the
API scenario creates were equally bare.

That is now fixed. The seeder writes **40 tables**, updates **8 more**, and the remaining 19 are
written by the API scenarios where they belong.

### 9.1 Created by the EF history seeder (40)

**Demand** — `StaffRequisitions`, `StaffRequisitionCosts`, `StaffRequisitionHistories`
**Vacancy and advert** — `JobVacancies`, `JobVacancyStatusHistories`, `JobPostings`,
`VacancyPipelineStageAssignments`, `JobShortlistingCriterias`, `JobShortlistingCriteriaValues`
**Candidate dossier** — `JobCandidates`, `JobCandidateQualifications`, `JobCandidateWorkHistories`,
`JobCandidateReferees`, `JobCandidateSkills`, `JobCandidateLanguages`, `JobCandidateInterests`,
`JobCandidateNotes`
**Talent CRM** — `CandidateSegmentMemberships`, `CandidateEngagementEvents`
**Application and screening** — `JobApplications`, `JobApplicationStageHistories`,
`JobApplicantTestResults`, `JobApplicantCommunications`, `ShortlistReviews`, `ShortlistDecisionLogs`
**Interview** — `JobInterviews`, `JobInterviewees`, `JobInterviewPanelists`,
`JobInterviewExternalPanelists`, `JobInterviewQuestions`, `JobInterviewSelectedQuestions`,
`JobInterviewScoreSummaries`, `JobInterviewScoreEntries`
**Offer and hire** — `JobOffers`, `JobOfferBenefits`, `JobOfferNotes`, `JobHireRecords`,
`PreEmploymentChecks`, `PreEmploymentCheckItems`, `ReferenceCheckResponses`

Each of the twelve posts has its own **candidate profile**: the degree, the universities, the prior
job titles, the employers and the skills a credible applicant for *that* post would carry, so two
candidates opened side by side do not read identically. Referees carry a `RelationshipTypeId` from
the catalogue as well as the free text, and languages carry a `LanguageId`.

### 9.2 Updated by the EF history seeder (6)

`RecruitmentPipelines`, `RecruitmentPipelineStages`, `CandidateTalentSegments`,
`JobInterviewQuestionDetails` — read, not written; the API scenario owns them.

`OnboardingPlans`, `OnboardingTasks` — **states spread**, not rows created. See §9.4 for why.
`ProbationPeriods` and `ProbationReviews` were going to be spread the same way and are now left
untouched — §10.3.

Plus a backfill pass that gives a dossier to **any candidate who has none**, which is how the live
applicants created by scenario sections 17–20 stop being bare.

### 9.3 Written by the API scenarios, not by the seeder (19)

`PositionVacancies` (reconciled from live headcount) · `JobVacancyAttachments`,
`JobPostingAttachments`, `StaffRequisitionAttachments`, `JobCandidateDocuments` (real files through
the virus-scan gate) · `StaffRequisitionComments` · `JobInterviewQuestionTypes`,
`InterviewQuestionPresets`, `InterviewQuestionPresetItems` · `PreEmploymentCheckTemplates`,
`PreEmploymentCheckTemplateItems` · `OnboardingPlanTemplates`, `OnboardingTaskTemplates`,
`OnboardingTaskComments`, `OnboardingAssets` · `OfferCandidateTokens` (minted when an offer is
issued) · `ProbationPeriods`, `ProbationReviews` (`065-probation`) · `ProbationExtensions`
(`TdcDemoProbationExtensionSeeder`).

⚠ The four attachment tables are deliberately **not** given historical rows. An attachment row whose
file was never uploaded through the scan gate is a download link that 404s in front of an audience —
worse than a tab with one document in it. The live records carry real uploaded files; the historical
ones carry none.

### 9.4 Onboarding and probation — why rows were not created

> ⚠ **The probation half of this section is withdrawn — see §10.3.** Onboarding is still
> spread; probation is now left entirely alone, because confirmation writes to the employee
> record and two books rest on all six starters still serving.

Both were in the agreed scope, and both turned out to be structurally impossible to hang off a
historical hire:

- `OnboardingPlan.EmployeeId` is non-nullable.
- `ProbationPeriod` needs an `EmployeeId` **and** a `ContractDetailId`.

The appointees invented by this seeder are candidates, not staff — no employee record, no contract.
Creating ten employees would add ten people to a register already staffed exactly to the
establishment, so the organogram, the headcount and the salary-grade counts would all disagree with
the org chart. Linking the hires to the staff who really hold those posts is no better: those people
were given years of service reaching back years by `TdcDemoWorkforceSeeder`, and a probation period
starting in March 2026 for someone employed in 2019 is a record that contradicts itself.

So both registers stay attached to the real employees the `145-onboarding` and `065-probation`
scenarios create. What was actually wrong with them was never the row count — every plan sat at
`NotStarted` and every probation at `Active`. The seeder now spreads them: onboarding across
Completed / Overdue / InProgress / NotStarted with tasks in Waived, Blocked, PendingVerification and
Overdue; probation across Completed, ConfirmationApproved and PendingConfirmation with reviews
carrying ratings, recommendations and HR sign-off.

⚠ **Two probation periods are deliberately left `Active`:** TDC/00063 (Kojo Ansah), whose first
review Book 1 §7 walks live, and TDC/00018 (Patrick Appiah), because `TdcDemoProbationExtensionSeeder`
runs *after* this step and looks for an active probation — move him and that seeder silently writes
nothing.

### 9.5 The one table that stays empty, and why

| Table | Why |
| --- | --- |
| `JobInterviewScoreDrafts` | The scoring screen's autosave. A draft only exists while a panellist is part-way through a scorecard; every historical scorecard is finalised. Manifest-`excluded`, correctly. |

Everything else in the recruitment module has rows.

### 9.6 Pre-employment check providers — built on request

`PreEmploymentCheckProviderServices` maps a Procurement **supplier** to the checks it performs, and
the check-type → provider cascade on the check and check-template screens reads it. It was the one
recruitment table with no writer anywhere in the demo pack. The obstacle was never the table: it was
that the demo's three suppliers are an engineering firm, an office-supplies firm and an
infrastructure firm, and mapping one of those to a medical examination reads worse to a stakeholder
than an empty tab.

Built so the providers are real, and **registered through the real doors** rather than written into
another module's tables behind its back:

| Provider | Registered as | Performs |
| --- | --- | --- |
| Tema Diagnostic & Occupational Health Centre | `SUP-MED-001`, Service Provider | Medical examination, drug test |
| Sentinel Verification Services Ltd | `SUP-VER-001`, Service Provider | Background check, academic verification, professional licence verification, reference check, credit check |
| Ghana Police Service — Criminal Investigations Department | `SUP-PCC-001`, Government Agency | Police clearance |

Between them they cover **eight of the nine** `PreEmploymentCheckType` values; only `Other` is
unmapped, which is correct — it is the escape hatch for a check with no standing provider.

- **Scenario `050-recruitment.mjs` §21** creates each supplier through `POST /api/Suppliers` and maps
  it through `POST /api/pre-employment-checks/providers`. Both are the doors a user would use, so the
  registration path is exercised rather than bypassed, and Procurement's master data is created by
  Procurement's own endpoint.
- **`TdcDemoCheckProviderLinkSeeder`** then stamps the link onto the rows that already exist — the
  check items on every historical offer, and the check template every future check is raised from —
  keeping the free-text provider name in step with the link. It is a separate orchestrator step with
  its own probe ("does any check item name a supplier?") because it cannot run until both the checks
  and the providers exist, by which time the history seeder's own guard has closed.

⚠ `POST /api/Suppliers` is behind `GuardDirectMutationAsync`. If an **Active** maker-checker policy
covers the Supplier resource it answers **409** and demands a staged change request instead; with no
such policy — the demo's state — it is allowed. Both the scenario and the seeder say so in their
output if it happens, so a 409 is diagnosable rather than a silent empty tab.

---

## 10. The module boundary, checked against the source files

The entity files that define the recruitment module, and what each contributes:

| File | Classes | Status |
| --- | ---: | --- |
| `RecruitmentEntities.cs` | 61 | Covered in §2 and §9. |
| `StaffRequisitionEntities.cs` | 5 | Covered in §2 and §9. |
| `PositionVacancyEntities.cs` | 1 | Covered in §2 and §9. |
| `ProbationConfirmingAuthority.cs` | 1 | Covered in §2 group I. See §10.1. |
| `ProbationReminderEntities.cs` | 2 | Covered in §2 group I. See §10.1. |
| `EmployeeOathOfSecrecy.cs` | 1 | **Missed by the original inventory.** See §10.2. |

**70 entity classes in total.** The inventory in §2 listed 73 tables, which is these 70 minus
`EmployeeOathOfSecrecy`, plus the three succession-owned talent-pool tables (§2 group J) that sit
next to recruitment in the menu but belong to another area.

### 10.1 Probation confirming authorities and the reminder sweep — already sound

| Table | Rows | Spread |
| --- | ---: | --- |
| `ProbationConfirmingAuthorities` | 5 | All three scopes represented — one global, three by staff level, one by organisation unit |
| `ProbationReminderRuns` | 17 | All `Scheduled` (the only trigger the nightly sweep uses) |
| `ProbationReminderDispatchLogs` | 5 | Three kinds — ProbationEndingSoon, ReviewOverdue, ConfirmationFormDue — across three escalation tiers |

Both reminder tables are manifest-`excluded` as genuine system logs, and they are non-empty because
the sweep has really run. Nothing was added; nothing needed to be.

### 10.2 The oath of secrecy — the one file the inventory missed

`EmployeeOathsOfSecrecy` was absent from §2 entirely. The name-pattern search that built the table
list looked for *job / candidate / offer / interview / talent / onboard / requisition / recruit*, and
"oath of secrecy" matches none of them. It is also filed in the coverage manifest under
**`policies-letters`**, not `recruitment`, so the area-scoped read of the manifest did not surface it
either. Two independent filters, both keyed on naming, both blind to the same row.

Checked directly, it needs nothing:

| Measure | Value |
| --- | --- |
| Rows | 6 — one per new starter |
| `Method` | Both values present: 1 Affirmed (by the employee), 5 Administered (sworn on paper, recorded by HR) |
| Witnessed | 5 of 6 carry a witness |
| Signed document attached | 1 of 6 (a real uploaded file) |

Written by scenarios `011-letters-and-profile-changes` and `145-onboarding`, and demonstrated **live**
in Book 1 §7 step 4, where `new.hire` affirms his own oath at `/me/oath` and the date, IP address and
text version are recorded — with HR's register at `/hr/probation/oaths` behind it. Nothing was
changed.

### 10.3 Correction to §9.4 — probation is left entirely alone

§9.4 said the probation register's states would be spread because every period sat at `Active`. That
was written from the schema and is **withdrawn**. Reading the books afterwards showed three reasons
not to:

1. **Confirmation is not a status.** `ProbationService` writes a confirmation date onto the
   **employee** and releases the benefits withheld until then. Setting the status directly would leave
   a period reading "confirmed" beside an employee record that disagrees.
2. **Book 1 §7's aside and Book 2 §6 both rest on all six starters showing "withheld"** on the
   benefits screen. That refusal is a live demo moment, and confirming three of them removes it.
3. **Book 1 §7 opens with "six probation periods, each with a *scheduled* first review"**, and step 2
   has `head.dev` conduct one on stage.

Six people currently serving probation is not a collapsed status dimension — it is the story the
books tell, and the register's other states are demonstrated by conducting a review live rather than
by pre-seeding an outcome. The corresponding row in §3's state-coverage table should be read the same
way. **Onboarding is still spread** (Completed / Overdue / InProgress, with tasks in Waived, Blocked
and PendingVerification): no book states an onboarding status, and a queue showing work in every
condition is what those screens are for.

### 10.4 Where that leaves the module

Of the 70 classes across the six files: **64 carry rows this seeder or the API scenarios create**,
**4 are system logs or configuration already populated** (the two reminder tables, confirming
authorities, oaths), and **1 is deliberately empty** for the reason in §9.5
(`JobInterviewScoreDrafts`). `PreEmploymentCheckProviderServices` was the second of those until it
was built on request — §9.6.

---

## 11. What running it actually found (2026-09-14)

§8 and §9 described work that had been written but never executed. Running it end to end took four
attempts and found more than the build ever could. Everything below was measured, not reasoned.

### 11.1 The demo data had fallen behind two product rules, and both failed silently

Neither shows up as a low row count. Both stop rows being created at all.

**Lane R5 — the requisition submit gate.** A requisition whose position is not covered by an
approved manpower budget line cannot be submitted without an `exceptionJustification`. Module
`150-manpower` runs *after* `050-recruitment`, so on a fresh database nothing is covered and **every
submit answered 422**. Nothing downstream exists without an approved requisition, so the whole spine
collapsed: no vacancy, no candidates, no applications, no interviews, no offers — a 26-table
"still empty" list from one refused call. Fixed by setting the justification on every requisition
(which is also the honest record) plus an `ensureSubmitted` helper that repairs drafts left by an
earlier run — those were created before the fix and can never otherwise be submitted.

**Lane R4a — the establishment gate.** A position vacancy is opened only on a post whose headcount
was authorised: `IsEstablished = EstablishmentApprovedOn != null`. **Nothing in the codebase ever set
that column.** Measured: 142 positions, **0 established**, 38 genuinely below headcount,
`PositionVacancies` = **0**. So `/hr/recruitment/establishment` — the screen the walkthrough opens
on — was blank, and reconcile reported "scanned 142, opened 0". `TdcDemoEstablishmentApprovalSeeder`
now approves the 123 TDC posts and leaves the 19 estate fixtures under the unit named "General"
alone; they carry 18 of the 38 gaps and would fill the screen with another module's test data. The
gaps themselves are still opened by the product's own reconcile.

### 11.2 Three EF tracked-graph traps, each of which cost a run

1. **"The association between entity types 'X' and 'Y' has been severed."** Adding a row whose
   *required* foreign key points at a principal that is itself still `Added` makes EF's
   `NavigationFixer` null the key and throw. Hit on `PreEmploymentCheckItem → ReferenceCheckResponse`
   and again on `JobCandidate → CandidateEngagementEvent`. **Fixed by a two-phase save**: those
   second-level rows are collected in a `List<Action>` and run after the first `SaveChangesAsync`,
   when every principal is `Unchanged`.
2. **"A circular dependency was detected."** `JobVacancy.StaffRequisitionId` and
   `StaffRequisition.JobVacancyId` point at each other and both were `Added`; EF cannot order the two
   INSERTs. The vacancy's link is the one required at insert time, so the requisition's back-pointer
   moved to the second phase where it is a plain UPDATE. **Grep a new seeder for
   `x.SomethingId = y.Id;` before running it** — that shape is the whole tell.
3. **`DiscardPendingChanges` was itself unsafe.** It detached entries one at a time, which severs
   relationships as it goes: the next step's save then threw an *unhandled* exception that killed the
   entire seed run rather than failing one isolated step. Replaced with `ChangeTracker.Clear()`.

### 11.3 Validation rules the seed data had to learn

| Refusal | Rule |
| --- | --- |
| `Interview cannot be scheduled in the past.` | The API refuses a back-dated interview. Completed interviews with panels and scorecards come from the EF seeder, which does not use that door. |
| `Proposed base salary … is outside the salary band (MIN – MAX) for this position.` | Base salary is validated against the position's grade band, and the refusal quotes it. Don't hard-code a figure — read the band out of the 422 and retry inside it, so the harness survives a re-seeded salary scale. |
| `Say who was paid: choose a supplier, or name the payee.` | A requisition cost line needs an active supplier or a `PayeeName`. Set in the scenario *and* in the EF seeder — an unattributable cost row makes cost-per-hire meaningless. |

### 11.4 Cross-module defect 26 was misdiagnosed, and is now fixed

The entry logged on 2026-09-10 blamed a paging fault inside `SupplierRepository.GetSuppliersAsync`.
The stack trace says otherwise:

```
Unable to resolve service for type 'ISupplierContactRepository'
while attempting to activate 'SuppliersController'
```

`SuppliersController` takes three repositories; **only `ISupplierRepository` was ever registered**,
though both missing interfaces and both implementations already existed. It fails during *controller
activation*, before any action method runs — which is why every endpoint failed identically and why
the query string made no difference. Both registrations were added and the defect entry rewritten
with the proven cause. HR's own narrow read door, `GET api/hr/suppliers`, stays.

### 11.5 Two operational traps worth keeping

- **A frontend build and a rebuild cannot share this machine.** `next build` runs with an 8 GB heap
  and took 10.4 GB; free memory fell to 1.1 GB of 23.4 GB, SQL Server stopped completing logins, and
  the rebuild died mid-run with `Sqlcmd: Timeout error [258]` and a wall of 500s. **A SQL "timeout
  during the post-login phase" in this project means memory starvation, not a bad query.** Run
  `dotnet build-server shutdown` before a long job — the Roslyn server alone held 2.58 GB.
- **`Start-Demo.ps1` reports "the web app did not start" when it has.** Its readiness probe hits `/`,
  which redirects into the 14-query enterprise dashboard and times out. `/login` and `/careers` both
  answered 200 while the script called it a failure. Check a real route before believing it.
