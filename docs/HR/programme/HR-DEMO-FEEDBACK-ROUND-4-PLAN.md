# HR demo feedback, round 4 — Recruitment, Onboarding/Orientation, Miscellaneous

> **Status 2026-09-23 — A–O done (with I-b; K as K-a, K-b1, K-b2; N-b as N-b1, N-b2), and P2a, the
> recruitment guide. What remains is P: P2b and P2c (the company schedule and orientation guides) and
> the six walks in § 5.**
>
> | Lane | State |
> |---|---|
> | **A** | **DONE** — 45 ×2, `run-round4-a.mjs`, committed `10bb1d4f` |
> | **C** | **DONE** — 46 ×2, `run-round4-c.mjs` |
> | **F** | **DONE** — 103 ×2, `run-round4-f.mjs`; both migrations applied to UAT |
> | **G** | **DONE** — 41 ×2, `run-round4-g.mjs`; committed |
> | **H** | **DONE** — 36 ×2, `run-round4-h.mjs`; **37** since 2026-09-24, when the suite began switching its template off again and asserting it (P2a's repair, § 8) |
> | **B** | **DONE** — 96 ×2, `run-round4-b.mjs`; **no migration**; all 16 neighbouring suites at baseline |
> | **D-1** | **DONE** — 58 ×2, `run-round4-d.mjs`; migration `AddInterviewPanelClashOverrideAndRoomBooking` applied to UAT. D1–D4 + D8, the recruitment half |
> | **D-2** | **DONE** — 39 ×2, `hr-company-schedule/run-round4-d.mjs`; migration `AddCompanyEventOriginalWindowAndUniqueNumbers` applied to UAT. D5–D7 |
> | **E-a** | **DONE** — 141 ×2, `run-round4-e.mjs`; migration `AddRecruitmentTestEngine` applied to UAT; all 18 neighbouring suites at baseline. E1–E5. **The harness found two defects in the lane's own code** — § 8 |
> | **E-b** | **DONE** — 86 ×2, `run-round4-e6.mjs`; migration `AddRecruitmentTestSittingMode` applied to UAT; E-a still 141 and all 18 neighbours at baseline; demo scenario `052-recruitment-tests` idempotent, `verify-tables` recruitment 74/74. E6 + E7 — § 8 |
> | *email links* | **DONE** — every candidate-facing CTA in the recruitment catalogue pointed at a non-existent page since 2026-08-31; six repointed, **two confirmation pages built**. § 8 |
> | **I** | **DONE** — 100 ×2, `hr-orientation/run-round4-i.mjs`; migration `AddOrientationTriggers` applied to UAT. I1–I5: audience rules on the shared HR axis + populations, typed picker + reach, triggers that fire (hire, movement, publish, nightly), onboarding template applicability + a plan on hire, the "why" diagnostic. **Two demo-data incidents of my own, both reversed** — § 8 |
> | **I-b** | **DONE** — 152 ×2 (lane I's 100 + 52), same harness; **no migration**. Recurring programmes renew one period after completion, opened early by the deadline so it falls on the anniversary; the effective dates now bind (a lane I gap); **HR may re-enrol after a withdrawal and open a next cycle early by hand** (the user's call); a renewal can no longer open over a completed-then-withdrawn cycle; ORI-CMP-001 recurs annually on UAT and in the seeder — § 8 |
> | **L** | **DONE** — block S, 29 assertions, in the same harness: 181 ×2; **no migration**. L1 reproduced first: the endpoint was right — onboarding (blended) had never had a session scheduled, compliance is self-paced, and the dialog drew a 403 like "none". The dialog now says which; only programmes that take enrolments are offered; closed sessions are marked; **the enrol path refuses a retired/draft programme and a closed or other programme's session** (it checked neither); two induction days seeded. ⚠ A third demo-data incident, reversed — § 8 |
> | **J** | **DONE** — 94 ×2, `hr-orientation/run-round4-j.mjs`; **no migration**. Copy an onboarding template (never the default, **not** its audience), a programme (everything it is made of, as a Draft) and a session (*Run again* on a new date); a Copy / Run again button on each list **and each record's page** — a finished session is on no list. **The harness found two defects older than the lane, both closed:** editing a quiz question's options had never once saved, and a task due before the start date was refused though the screen offered it — § 8 |
> | **M** | **DONE** — 54 ×2, `hr-orientation/run-round4-m.mjs`; migration `AddOrientationFacilitatorRegisterPick` applied to UAT. An external facilitator can be picked from the **training vendor register** (a vendor, and its trainer once named): the name, email and organisation are a snapshot taken when the pick is made or changed; a blacklisted/inactive vendor or inactive trainer cannot be picked, and one that becomes so later is flagged on the session instead of blocking its edits; a vendor or trainer booked for a session that has not happened cannot be deleted (M3); *Run again* keeps the picks (M4). Demo: GIMPA on both induction days — § 8 |
> | **K-a** | **DONE** — 53 ×2, `hr-orientation/run-round4-k.mjs`; migration `AddOnboardingOrientationReminders` applied to UAT. **The first HR sweep that delivers**: eight rules (onboarding tasks due / overdue / awaiting sign-off; orientations due / overdue / assessment unattempted / acknowledgement unsigned; certificates expiring), **one digest per person per run**, in-app in the same save as the claims and emailed after — each email's outcome recorded; four lead-day settings, each enforced in both positions; run-now / preview / runs / log + a Reminders screen; the host registered with it and its scheduled run seen. Proved by a local SMTP sink: the email **arrives**. ⚠ 768 test-fixture onboarding plans cancelled first (the user's call) — § 8. ⚠ **The scheduled run found a lane I4 gap:** a plan created on hire had no coordinator, so its reminders reached nobody — the confirming officer now coordinates it (lane I 183 ×2, H6b/H6c) |
> | **K-b1** | **DONE** — 84 ×2, `hr-orientation/run-round4-kb.mjs`; migration `AddOrientationNoticeEmailOutbox` applied to UAT. **The events tell people now**: enrolled (HR, bulk, a rule; a renewal too, wired but not exercised — § 8), a session placed / moved (saying where from) / postponed / cancelled — participants and employee facilitators — completed, certificate issued; each notice written in the same save as its event, its email queued on it and sent within a minute by a dispatcher. **Certificates are issued at completion** for a programme that issues them (nothing issued one before — the user's call), plus an HR **Issue / Reissue certificate** button. The programme's **"Send reminders"** switch made real — it was read by nothing, K-a included. A **Notices** tab: who was told what, and what each email did — § 8 |
> | **K-b2** | **DONE** — 54 ×2, `hr-orientation/run-round4-kb2.mjs`; **no migration**. Onboarding tells people: a plan made **welcomes the new hire** and tells its coordinator and buddy (on hire too — lane I 184 ×2, H6d); a plan changing hands tells only the new holders; a task given tells its assignee; marking a task done that needs signing off tells the coordinator at once. And the page they lead to — the user's call — **"My onboarding"** in the portal: the new hire's plan and who owes each task, the tasks given to the reader with **Mark done**, the new colleagues they are buddy to. K-a's reminders for a task given to somebody now open it, not HR's queues — § 8 |
> | **N** | **DONE** — 115 ×2, a new suite `hr-templates/run-lane-n.mjs`; **no migration**. **Letter & Email Templates** under HR Settings covers all 44 templates in the eight catalogues (32 emails, 12 printed documents). It has a token palette, a live preview that lists every problem a save would be refused for, save, reset and a test send to the officer's own address. The list comes from the catalogues and a row is stored only when a tenant edits; N2's startup seeding was rejected because a seeded row would freeze its day's wording. **The sender used any tenant's stored wording**, and would still have taken an untouched seeded copy for an edit; both are fixed. Background emails now carry the tenant's legal name, where they printed "Our Company"; so does the careers activation email, where it read "Default Tenant". Candidates are greeted by name, where "Application Received" used their email address. The register § 2.7 lists all 44, checked against the screen both ways. The survey found **four sender behaviours outside the lane**, which become **N-b** — § 8 |
> | **N-b1** | **DONE** — 32 ×2, `hr-templates/run-lane-nb1.mjs`; **no migration**. **The offer letter goes with the Offer Issued email as a PDF** (the user's call), rendered by the Syncfusion engine the document module already uses; the email says it is attached only when it is. **Under Review** is sent once, on the first move a person makes into a review stage, from either door; the older door had sent it after every move. **Application Withdrawn** is sent from all three live withdrawals; it was unreachable. **Offer Accepted** is sent however the acceptance arrives, and an acceptance by the anonymous link carries the offer tenant's wording. The portal's withdrawal now closes the application's pipeline stage, as HR's always did. ⚠ The first PDF had **empty tables**, because the importer does not read `rem`. It was found by opening the file, then fixed, and the suite now checks the tables' words — § 8 |
> | **N-b2** | **DONE** — 36 ×2, `hr-templates/run-lane-nb2.mjs`; migration `AddCompanyScheduleReminderSweep` applied to UAT. **Company-schedule reminders send themselves.** An hourly sweep sends each live event's reminder the days before its form asks for, and chases unanswered invitations ahead of the RSVP deadline. Each goes **once**, stamped on the event, in the tenant's wording and under its legal name. The form's **Send reminders / days before** had been ghosts. The RSVP-chase lead is a new tenant setting (2 days, on the policy page). A moved date is reminded again. The manual endpoints, which no screen called, are now buttons on a Reminders card and count as the send. Register § 2.8 — § 8 |
> | **O** | **DONE** — 101 ×2, `hr-jobarch/run-round4-o.mjs`; migration `AddTechnicianRoleFlag` applied to UAT. **One answer to "is this person a technician?"**: the stored `CanBeAssignedToMaintenance`, which Maintenance's work-order, labour and schedule gates already read. It follows a position's new **Technician role** flag unless HR sets the person *Include* or *Exclude* by hand, and the DbContext re-establishes that on every save. ⚠ The column had **no writer at all**: 0 of UAT's 2,089 staff, an empty door, every assignment refused. The door carries what HR knows (skills with their certification dates, unit, location, trade) and **null, not 0**, for what it does not. Maintenance's dropdowns now read it, and the every-employee fallback and the root endpoint are gone. **Probationers count as available** (the user's call). The plan's defect 21 was wrong, and so was its O2 formula. Lane H's check-set picker never saved, and cleared the set on every edit; fixed. Hand-off: cross-module defect #29 — § 8 |
> | **Q** | **IN PROGRESS** (added 2026-09-24) — education level on the ladder, the proper fix for R4-5.2: a Level on each candidate qualification, an *Education level at least …* criterion that compares ranks, the catalogue mapped onto the ladder. Four decisions, all the user's — § 4 lane Q |
> | **P2a** | **DONE** — the recruitment guide rewritten for round 4, from the code, this log and UAT: § 5.8 scoring, a new chapter **8A** for recruitment tests, § 9 interviews, § 10 offers, § 13 talent pool. **Docs, then a data repair.** It records 18 `R4-…` findings, four of them walks not yet done. **Two blocked the demo, both in the data:** the seeded test blend could not show, because every candidate who sat the paper failed the vacancy's mandatory degree (R4-5.1); and no demo offer got a checklist, because UAT held 44 active check templates and the fallback wants one (R4-10.1). **Both repaired 2026-09-24** (the user's call): three first degrees via scenario 052, giving 81 / 67.5 / 66; 43 fixture templates switched off, and the suites that made them now switch theirs off. The repair found two more: the seeded degree criterion cannot recognise a real degree (R4-5.2, now lane Q), and a rebuild leaves the blend unscored (R4-5.3, fixed in the rebuild script). The four live demo offers raised before the tidy now have TDC's checklist — § 8 |
>
> ⚠ **Lane D is split in two.** As specified it is eight slices across two modules, roughly four
> times lane B. It splits at the seam the plan already implies: the clash check (recruitment) and
> the organizer + company-schedule defects. That is sequencing, not narrowing — D-2 follows
> immediately, and the lane is not done until it lands.
>
> **Lane E is complete** (E-a + E-b). **Lane I is complete, with I-b. Lanes L, J, M and N are complete, and so is K** (K-a, K-b1, K-b2). **N-b is complete** (N-b1, the recruitment senders and the offer letter attached; N-b2, the company-schedule reminders). **O is complete** (the technician-role flag; Maintenance's side is a hand-off, defect #29). **P2a is complete** (the recruitment guide). What remains is **P**: P2b, P2c and the walks.
>
> ⚠ **Nothing in round 4 has been browser-walked.** § 5 names four walks a harness cannot replace;
> all four are still outstanding.
>
> ⚠ **Lane B moved the scoring engine.** `ScoringCandidateView` and `EvaluateCriterion` were private
> members of `JobApplicationService`; they are now `ShortlistingEvaluator` in
> `Services/HR/Recruitment/`, and the criterion-value resolver is `ShortlistingCriteriaResolver`.
> Anything that scores an application or validates a criterion goes through those two files now.
>
> **Neighbouring suites after lane F**, all at baseline:
>
> | Suite | Result | Baseline | |
> |---|---|---|---|
> | `run-c1` · `run-c2` | 96/96 · 89/89 | same | ✅ |
> | `run-round4-a` · `run-round4-c` | 45/45 · 46/46 | same | ✅ |
> | `run-k` · `run-v` · `run-a` · `run-candidate-country` | 81 · 72 · 44 · 43 | same | ✅ |
> | `slice-b` · `slice-c` · `slice-e` · `slice-f` | 174/176 · 188/190 · 101/105 · 69/69 | same | ✅ |
> | `run-lane5b` | 32/34 | 32/34 | ✅ the recorded stale admin-gate |
>
> ⚠ `slice-b` came back **173/176 before the second name fix** — one below baseline. Its failing
> assertion **expected the double space** (`expected "E2EA  Cand507766"`), so a stale assertion had
> encoded defect 29 as the contract and would have defended it indefinitely. It was left untouched
> and passed on its own once both name paths agreed, which is the proof that wanted having. That one
> assertion is the entire argument for *"fewer assertions with zero failures is a regression
> signal"*.
>
> **Verified on `ErpSystemDB_UAT`, not the dev database** — see § 9. The dev database is on the
> pre-baseline chain (562 history rows, last `AddLeaveYearStartMonth`) and cannot take the current
> migration chain; UAT carries `DisposableDevelopmentCurrentModelBaseline` and the demo dataset.
>
> **Neighbouring suites after lane A**, all at or explained against baseline:
>
> | Suite | Result | Baseline | |
> |---|---|---|---|
> | `run-k` (criteria values, scoring fixes, D-7) | **81/81** | 81 | ✅ the scoring suite |
> | `run-v` (talent segments) | **72/72** | 72 | ✅ |
> | `run-c1` / `run-c2` / `run-a` / `run-candidate-country` | **96 / 89 / 44 / 43** | same | ✅ after the § 9.1 harness fix |
> | `slice-f` | **69/69** | 69 | ✅ after the same fix |
> | `slice-b` / `slice-c` / `slice-e` | 174/176 · 188/190 · 101/105 | 175/176 · 190 · 105 | stale assertions, § 9.2 |
> | `run-lane5b` | 32/34 | 34 | stale admin-gate, § 9.2 |
> | `run.mjs` / `slice-d` / `run-g` | blocked | — | § 9.3, parked |
>
> ⚠ **A "stale assertion" here is always named and explained in § 9.2.** The rule this module runs
> on is that *fewer assertions with zero failures is a regression signal*, so a lower count is never
> waved through.

## Context

**Source.** `RECRUITMENT & OTHER CHANGES.pdf` (2 pages), brought 2026-09-21 after the HR demo.
Seventeen bullets across three sections. This plan accounts for **every** bullet — as a build item,
a decision, a defect, or a record. If a bullet is missing below, that is an error in this document.

**Why now.** Recruitment and orientation are functionally complete (all 70 recruitment guide gaps
closed 2026-09-16; area 15 closed 2026-08-18), but the feedback is about the *next* layer: the
criteria that decide a shortlist must be trustworthy; the talent pool must be usable to fill a
vacancy; interviews must be schedulable at volume; tests must exist as more than a typed-in mark;
and the templates HR signs its name to must be editable without a deployment.

**Vetting block.** Every "what exists" claim was verified against the working tree on 2026-09-21
(branch `hrdev`, head `7a0c78d8`) by three read-only surveys plus direct reads of the cited code.
Line numbers drift — **resolve every reference by NAME before acting on it.**

**Two findings that change the shape of the work, both confirmed by direct read:**

1. **The "Meetings/schedules module" already exists.** `CompanyEvent` (category `Meeting`),
   `EventParticipant` with RSVP, `EventTask`, `EventAttendance`, `MeetingRoom`, `RoomBooking` —
   `src/ErpSystem.Core/Entities/HR/CompanyScheduleEntities.cs`, with a full service, controller and
   screens. `RoomBooking` even has a *blocking* double-booking check. Lane D extends it; it does
   not build a second calendar.
2. **"HR should be able to upload pictures for the candidate" is already built** (round 3, lane C2).
   `POST/GET api/job-candidates/{id}/photo` + `GatedPhoto`/`PhotoDialog` on the candidate list and
   detail. The only residue is the **application** ("applicant") screens, which never render it.

**The ported-from solution is on this machine and was consulted.** `D:\ERP Demo\HRApi` — the
standalone Blazor Server + API HR product named in `HR_MODULE_PORT_PLAN.md`, on `main` at `6d2b8a7`,
the commit the HR Core layer is synced to. Four things came out of reading it:

| | Finding |
|---|---|
| **Lost in the port** | The interview scorecard printout the feedback asks for **exists there and was never brought across** — `ErpSystem.BlazorServer\Pages\Recruitment\InterviewScorecard.razor`, 516 lines. Lane F ports it and fixes its seven gaps. |
| **Confirms lane N's design** | `Pages\Recruitment\RecruitmentEmailTemplates.razor` (679 lines) is the template screen the feedback says is missing: an **event-key selector** *(“links this template to an automated recruitment email — determines the available merge fields”)*, a clickable **merge-field palette** that inserts `{{Token}}` at the cursor, **preview with sample data** in an iframe, and **test send**. Exactly the API lane N adds. |
| **Never existed anywhere** | No aptitude/exam engine, no slot apportionment (slots are typed by hand there too), no talent-pool criteria query, and orientation audience rules are the same CRUD-with-no-resolver. **The port did not lose these — they are new capability**, which is why the feedback asks for them. |
| **Use it as reference, not as a source** | It is a different stack (Blazor + inline CSS + `HttpClient` API services). Read it for *what the document said and how the sheet was laid out*; build to this repo's patterns. |

**House rules that bind every slice.** The user runs `dotnet build` and scaffolds migrations (the
agent edits them); the agent stages, the user commits. Every slice's harness
(`D:\Rhema\TDC ERPS\dev-harness\`) is green **twice** in Staging with the JWT key passed in. Every
new column a user can fill gets a `demo-coverage-manifest.csv` row with seeding. Migrations use the
**guarded-SQL idiom** (`IF COL_LENGTH(...) IS NULL ALTER TABLE ... ADD`), never a bare scaffolded
`AddColumn` — `rebuild-db` builds the schema from the EF model, so a rebuilt database already has
the column. Template: `src/ErpSystem.Data/Migrations/20260918042423_AddLeaveYearStartMonth.cs`.

---

## 1. Decisions

| # | Decision | Effect |
|---|---|---|
| **D-1** | **Extend HR Company Schedule; no new Meetings module.** | Lane D adds a personal diary, team scheduling by a head, invite/RSVP/reminder notifications, and makes the interviewer clash check read events, room bookings, training and closures. Fixes the Company Schedule defects this makes load-bearing. |
| **D-2** | **The recruitment exam engine mirrors Orientation, and is sat online with auto-marking.** | Orientation already has `OrientationAssessmentQuestion/Option/Response`, server-side grading and a working test-taking UI. Lane E copies that shape (including its denominator bug fix), adds sections/duration/attempts/windows, delivers in the careers portal, and writes results back into the existing `JobApplicantTestResult` so `TestScoreWeight` finally does something. A printable paper covers offline sittings. |
| **D-3** | **Location goes onto the geography tree on BOTH sides.** | `JobCandidate` gains `GeoAreaId`; the criterion's values become `GeoArea` references; matching is **tree containment** (an accepted area matches candidates in it and anywhere beneath it), with the free-text city kept as the fallback for rows predating the tree. |
| **D-4** | **A new HR-scoped template screen, plus the missing token-catalogue API.** | `/administration/hr/letter-templates`, gated on HR permissions, covering the HR modules only. `/administration/settings/email` (TenantAdmin) is left alone. |

**Defaults taken** (overridable — each is also in § 6):

- **D-5** A clash **refuses** where the conflicting commitment is hard and confirmed (another
  interview, a Confirmed room booking, a Confirmed event the panelist accepted) and **warns** where
  it is soft (Tentative booking, training nomination, day-granular leave/travel). A refusal is
  overridable with a recorded reason, following the requisition-budget precedent.
- **D-6** Interview breaks are **explicit rows on the interview** (start/end per break), not read
  from the work schedule. A break is a property of the sitting, not of the panel's contract.
- **D-7** The talent-pool criteria score is shown **beside** the 40/30/20 fit score, not merged into
  it. `MatchScoreMax` stays 90; a second column carries the criteria score out of 100.
- **D-8** External facilitators come from the **existing `TrainingVendor` + `TrainerProfile`**
  register, not a new one. Orientation and training buy from the same market.
- **D-9** `OrientationAudienceScope` migrates onto the shared `HrAudienceTargetType` +
  `IHrAudienceResolver`. `HREnums.cs:10143` asks for exactly this in a comment.
- **D-10** Offer validity becomes a policy setting `OfferValidityDays` (default 14), enforced, and
  registered in `HR-CONFIGURATION-REGISTER.md`.

---

## 2. The register — every PDF bullet

### RECRUITMENT

| # | Bullet | What exists today | Gap | Lane |
|---|---|---|---|---|
| 1 | Location criterion should use geo scheme values | `JobShortlistingCriteriaType.Location` matched by ordinal substring against `JobCandidate.City` (free text, lower-cased). `ShortlistingCriteriaShapes.cs` Location row is `ValueKind = Text`. Frontend is a text box, *"A city, e.g. Kumasi"*. `Employee` already carries `GeoAreaId`; `AddressFields` cascade exists. **`JobCandidate` does not.** | Whole geo path, both sides | **A** |
| 2 | Are the auto-shortlisting logics/scoring correct? | A largely sound engine (`JobApplicationService.EvaluateCriterion` and helpers), already repaired twice. **Eight real defects found — see § 3.** | Audit + fixes + a written record | **A** |
| 3 | Apply selection criteria to the talent pool, then schedule for interview | Pool is columns on `JobCandidate` + `CandidateTalentSegment`. `TalentPoolFilterDto` has 10 server-side filters; **the screen wires 5** and there is no location filter. Matching is a blind 40/30/20 rubric over experience/work-mode/availability. **No path from a pool member to an application or an interview** — `JobInterviewee` requires a `JobApplicationId`. | Screening by criteria, then bulk act | **B** |
| 4 | HR should upload candidate/applicant pictures | **Built** (round 3 lane C2): `POST/GET api/job-candidates/{id}/photo`, `GatedPhoto` on the candidate list and detail. | Application ("applicant") screens never render it | **B5** |
| 5 | Auto-apportion interview slots; breaks; feasibility for the day | `JobInterviewee.SlotStartTime/SlotEndTime` **already exist**, `ApplicationSlotEntry` on create, `PATCH interviewees/{id}/slot`. **Nothing computes a slot**, nothing validates one, no breaks, no feasibility. | The apportioner + validation + UI | **C** |
| 6 | HR sets up tests/exams/aptitude tests | `JobApplicantTestResult` is a **result ledger only** — name, date, venue, score, max, invigilator. No questions, no answers, no delivery. Training has no exam engine; **Orientation does**. | The whole engine | **E** |
| 7 | Clash check beyond the current 3; a meetings/organizer module | `CheckPanelistAvailabilityAsync` checks exactly 3 — other interviews, leave, travel — is **advisory only** and is **never called from create/update/reschedule**. Externals get 1 of 3. Compares the session window, never the candidate slot. N+1 on the panelist read. Company Schedule exists and is unread by recruitment. | Widen, make it bind, add the organizer | **D** |
| 8 | Print interview questions for offline scoring, then input | **The ported original exists and was read** — `D:\ERP Demo\HRApi\…\Recruitment\InterviewScorecard.razor`, 516 lines — and **was never brought across**. Zero print/export anywhere in recruitment today. `GetWithFullDetailsAsync` already loads the panel, candidates and questions in one read. | Port it, and fix its seven gaps | **F** |
| 9 | Auto-populate offer details; currency from a dropdown | Role fields are already server-authoritative. **Currency is a hand-typed 10-char box** on `offers/new`, `offers/[id]/edit` and each benefit line, with no server validation — while `HrCurrenciesController`, `CurrencyPicker` and `HrCurrencyBridge` all exist. `locationId` sits in form state **bound to no control**; `locationLevelId`, `salaryLevelId`, `salaryNotchId` have no UI at all. | Defaults endpoint + the currency swap + the missing controls | **G** |
| 10 | Pre-employment checklist should be part of the offer letter | `OfferLetterService.BuildConditionsList` already lists check items — but **only when `IsConditional`**, names-only, and **falls back to five invented conditions** the system does not track. Nothing seeds a check set at offer creation. | Seed it, render it properly, always | **H** |

### ONBOARDING AND ORIENTATION

| # | Bullet | What exists today | Gap | Lane |
|---|---|---|---|---|
| 11 | How onboardings trigger, and do the triggers fire | **They do not.** `OrientationEnrollmentTrigger` is written by the seeder and **read by nothing** (grep: 2 hits, both in the seeder). `OrientationAudienceRule` has no resolver — `HREnums.cs:10143` says so. `OnboardingPlan` is created **only** by a human pressing a button; the entity comment claiming hire-confirm generation is false. | Resolver + trigger service + a diagnostic | **I** |
| 12 | "Target id" should be a dropdown | Free-text GUID box (`programs/[id]/page.tsx`, "Audience rules" tab). `TargetEntityName` is on the DTO and **never populated** by the mapper. | Typed picker + reach preview | **I2** |
| 13 | Copy onboarding/orientation templates | **No clone anywhere** in onboarding or orientation. Three good precedents exist (`AppraisalTemplateService.CloneAsync`, `JobPostingPipelineService.ClonePipelineAsync`, job-description clone). | Clone endpoints + UI | **J** |
| 14 | Notifications should fire, e.g. overdue | Overdue queries exist (`GetOverdueTasksAsync`, `GetOverdueAsync`, `GetDueSoonAsync`) and are **page-only**. Neither module is in the 14-strong hosted-service roster. `OrientationNotification` is written **only** by the manual POST and the seeder. ⚠ **None of the 14 existing HR sweeps actually sends anything** — they log intent into a dispatch table. | A sweep that logs **and delivers** | **K** |
| 15 | Sessions dropdown empty for the selected program | Endpoint, service and repository are **correct and unfiltered**. The seeder creates **exactly one session**, bound to `ORI-PRD-001` only — the two programmes a user picks first have none. `const { data: sessions = [] }` renders a rejected query identically to an empty one. | Reproduce, then make the screen stop lying + seed sessions | **L** |
| 16 | External facilitator from a setup, like the training vendor | Three free-text columns on `OrientationSessionFacilitator`, no FK. `TrainingVendor` + `TrainerProfile` is a complete register with admin screens and `GET api/training-vendors/active`. | Reference the register | **M** |

### MISCELLANEOUS

| # | Bullet | What exists today | Gap | Lane |
|---|---|---|---|---|
| 17 | Wire HR letter/notification templates to the UI | 18 templates declared in 4 `IEmailEventCatalog` implementations, rendered by a custom `{{token}}`/`{{#if}}` engine, seeded as editable rows. `ITemplatedEmailService.RenderInline` exists **for the preview that was never built**. The designer at `/administration/settings/email` is gated to TenantAdmin/SuperAdmin/Manager — **HR cannot open it** — shows no `eventKey`, has no token palette, and **the token-catalogue API does not exist**. The catalogue seeder runs only under the demo seeder. | The API + an HR screen + seeding on normal startup | **N** |
| 18 | Position flag: is this a technician role | `EmployeePosition` has `RequiresCertification`, `RequiresGuarantor`, `RequiresLicense`, `IsActive` — the pattern to mirror. **No boolean is a list column today.** ⚠ The consumer already exists: `Employee` carries a Maintenance block (`CanBeAssignedToMaintenance`, `Specialization`, `CertificationLevel`, `ExperienceLevel`, `CurrentWorkload`, `MaxWorkload`, `LastSyncDate`) and HR already serves Maintenance at `api/hr/employees/technicians*`. What is missing is the **role-level** statement — see § 4, lane O. | The flag, and the door it feeds | **O** |

---

## 3. Defects found by the survey that the PDF did not name

Each is fixed inside the lane named. The first eight answer bullet 2 directly.

| # | Defect | Where |
|---|---|---|
| 1 | The **Location** arm ignores `NotEquals` and `In` — every operator that is not `Equals` falls through to substring. A *"not Accra"* criterion passes Accra candidates. | A3 |
| 2 | Location matching is **one-directional** (`city.Contains(accepted)`), unlike `MatchesValue`, which is bidirectional. *"Accra"* matches *"Greater Accra"*; *"Greater Accra"* does not match *"Accra"*. | A3 |
| 3 | Location ignores `MatchMode` (`AllRequired`) and `MatchStrategy` (`Exact`/`Contains`/`Fuzzy`) — both are honoured for every other list criterion. | A3 |
| 4 | Location scores **binary 1/0**; every other list criterion gives fractional partial credit. | A3 |
| 5 | An **empty criterion scores full marks** (`"No required X specified; defaulting to pass"`, `rawScore = 1m`) in Location, Gender and `EvaluateListCriterion` — lifting everybody's percentage. Same shape as the G-13.2 finding the talent pool already fixed. | A3 |
| 6 | `EvaluateNumericCriterion`: `LessThan`/`LessThanOrEqual` compare against `max`, which defaults to **`decimal.MaxValue`** when `MaxValue` is null — so a "less than" criterion with no ceiling **passes everyone**. | A3 |
| 7 | `Age` computes from `ScoringCandidateView.DateOfBirth`, a **non-nullable `DateTime`**. A candidate HR typed in without a DOB is scored as **~2026 years old** — passing every minimum and failing every maximum. | A3 |
| 8 | The **test blend and internal boost never execute** (guide G-5.2). The blend needs test results that nothing produces (lane E fixes the cause); the boost needs `IsInternalCandidate`, which must be verified as actually set. | A3 + E5 |
| 9 | The clash check compares against `JobInterview.StartTime/EndTime` and **ignores the per-candidate slots** it is supposed to protect. | D2 |
| 10 | `GetByEmployeeIdAsync` is called **inside the per-employee loop** — an N+1, unlike leave and travel which are batch-loaded. | D1 |
| 11 | Per-candidate slots are **never validated**: not against the session window, not against each other, not against breaks. | C3 |
| 12 | `OfferLetterService.BuildConditionsList` **invents five conditions** when no check set exists — the letter promises checks the system does not track. | H2 |
| 13 | The offer form's `locationId` is in state and **bound to nothing**, while the letter prints `{{LocationName}}`. | G2 |
| 14 | Offer and benefit `CurrencyCode` are **never validated** — `HrCurrencyBridge.RequireKnownCurrencyAsync` exists and is used by travel, guarantors and succession, but not by the offer path. | G3 |
| 15 | `OrientationAudienceRuleDto.TargetEntityName` is declared and **never mapped**, so the rules list can never show a name. | I2 |
| 16 | Company Schedule C-1…C-6 (Admin-gated red Delete, reschedule overwriting history, approval that is a flag, unenforced room rules, closures reaching nothing, repeating event/booking numbers) — C-2, C-4, C-5 and C-6 become load-bearing once the clash check reads this module. | D7 |
| 17 | The HR→Maintenance technician door returns **hardcoded zeros** — `CurrentWorkOrders = 0`, `WorkloadScore = 0`, `ShiftName = null` in `MapToMaintenanceTechnicianDto`, and the same two zeros again in `GetTechnicianAvailabilityAsync`. A zero here reads as *"this technician is free"*. Compounded by defect 21: the HR columns that look like the fix are themselves dead. | O5 |
| 18 | The nested `UserTechnicianSkillDto` is filled with only `SkillName`/`Level`/`IsCertified`; `SkillId`, `ProficiencyLevel`, `CertificationDate` and `CertificationExpiry` go back as defaults, so a consumer cannot tell a lapsed certification from a current one. | O5 |
| 19 | `Employee.CanBeAssignedToMaintenance` and the five technical fields have **zero frontend references** — HR cannot see or set any of them, which is why `TechnicianService` sets the flag for itself. | O4 |
| 20 | `JobInterviewQuestionDetail` has no scoring guidance, so a paper sheet cannot tell a panelist what a good answer looks like. | F1c |
| 21 | `Employee.CurrentWorkload` and `MaxWorkload` are **written and read by nothing** — Maintenance keeps its own copies on `Technician` and computes real utilisation from work orders. Two dead columns that look exactly like the fix for defect 17. ⚠ **Wrong, measured in lane O:** `CurrentWorkload` is written by nothing but READ by Maintenance's `TechnicianService`, which therefore reports every technician free. `MaxWorkload` is Maintenance's, and its only writer is unreachable. The conclusion stands (do not fill the door from them); register § 2.9, defect #29. | O5 |
| 22 | The application profile snapshot has **three** write sites; two call the shared `BuildSnapshot`, the third builds it inline. Any field added to the snapshot lands on two paths out of three unless the inline one is fixed too. | A1 |
| 23 | **Rescheduling an interview moves the session window and leaves every candidate's slot behind — then emails them the stale time.** `RescheduleAsync` rewrites `ScheduledDate`/`StartTime`/`EndTime`, rotates each confirmation token, and sends the reschedule notice with `slotStart: ie.SlotStartTime` — the *original* slot. Move a 09:00–11:00 session to 14:00–16:00 and every candidate is told to arrive at 09:20 on the new date, with a fresh token confirming it. Demo-visible. | C4 |
| 24 | **"Is this person a technician?" has four disagreeing answers**, two of them magic strings over two different org models (`Department.Code == "MAINT"` and `OrganizationUnit.Name.Contains("Maintenance")`). Unrecorded in the cross-module defects document. | O2 |
| 25 | **The technician dropdown that actually ships falls back to every employee in the tenant.** `maintenanceApiService` calls `/employees/maintenance-available` (full `EmployeeDto`, root controller) and on error retries `/employees?pageSize=1000&isActive=true`. Neither narrow technician endpoint is used by any screen. | O3 |
| 26 | **The HR→Maintenance technician sync is dead code.** `TechnicianRepository.GetFromHRModuleAsync` carries `// TODO: Implement actual HR module integration` and returns an empty list unconditionally, so `SyncTechniciansFromHRAsync` and `POST technical-skills/sync-from-hr` both iterate nothing. The integration map records this as "not verified to run"; it does not run. | O6 |
| 27 | `EmployeeSearchDto.MaintenanceTechniciansOnly` has **two references in the whole solution** — its declaration and its one `Where` clause. No caller, front or back. | O2 |

---

## 4. The build plan

Sixteen lanes. Sequenced: A→B and C→D→E share code, so order matters within a chain; chains are
independent of each other.

### Lane A — Shortlisting: geography, and the scoring audit

**A1 — `JobCandidate` joins the geography tree.**
`GeoAreaId : Guid?` + nav on `JobCandidate` (`RecruitmentEntities.cs`), and `GeoAreaId` + `GeoAreaPath`
on `ApplicationCandidateSnapshot` (a `[NotMapped]` JSON POCO — **no migration**, and old JSON
deserialises with nulls). Register a `JobCandidateGeoAreaConsumer : IGeoAreaConsumer` alongside the
eight existing probes in `GeoAreaConsumers.cs` and at `ServiceCollectionExtensions.cs:1174+` —
geography deletes are **soft**, so the FK never fires and the probe is the only thing standing
between an area and a silent orphan. Call `GeoAddressSnapshot.ApplyAsync` from all three write
paths — `JobCandidateService.CreateAsync` and `.UpdateAsync`, and
`CandidatePortalService.MapDtoToCandidate` — so `City` becomes a display snapshot, as it already is
on `Employee`.

⚠ **The snapshot has three write sites, and only two share a builder.**
`CandidatePortalService:600` and `JobApplicationService:2493` both call
`_snapshotService.BuildSnapshot(...)`; `JobApplicationService:3164` **builds the snapshot inline**
with its own collection variables. Adding the field to the shared builder covers two of three and
leaves the third writing rows with no geography — scored against the text fallback forever, with
nothing to indicate why. Fix the inline one in the same slice, or fold it into the shared builder.

Frontend: swap the plain `city` `TextField` in
`components/hr/recruitment/CandidateFormFields.tsx` for `AddressCascadeField`
(`components/hr/employee/tabs/address-fields.tsx`), and put `AddressFields` on the careers profile.
Both render the free-text city and region **disabled** with *"Set from the address above"* once a
scheme loads, and fall back to plain inputs for a country with no scheme — which is most of the
world, and a normal state rather than an error.

**A2 — the Location criterion references areas.**
Add `GeoArea` to `ShortlistingValueKind`; change the Location row in
`Recruitment/ShortlistingCriteriaShapes.cs` to `ValueKind = GeoArea` with the full operator set;
resolve and mirror area names in `JobVacancyService.ResolveCriterionValuesAsync` (the same
id-resolve-and-mirror it already does for skills and qualifications). Matching becomes **`GeoArea.Path`
containment** — an accepted area matches the candidate's area and every descendant — falling back to
the repaired text match when the candidate has no `GeoAreaId`. Frontend: the Location value editor in
`VacancyCriteriaPanel.tsx` becomes the `AddressFields` cascade.

**A3 — the audit.** Fix defects 1–8 of § 3. Route Location through `EvaluateListCriterion` so
`MatchMode`, `MatchStrategy` and fractional credit apply uniformly. Treat an **empty** criterion the
way `Other` is already treated — excluded from both `earnedScore` and `totalWeight`, so it neither
lifts nor lowers anybody — rather than awarding full marks. Guard the unset DOB and the unbounded
`LessThan`. Publish the audit as a new appendix in `HR-RECRUITMENT-SYSTEM-GUIDE.md` and re-score the
demo applications.

**Harness:** `hr-recruitment/run-round4-a.mjs`. Must assert **exact** scores, not `ok(a < b)` — an
inequality passes at zero.

### Lane B — Talent pool: screen, then act

- **B1** `POST /api/talent-pool/screen/{vacancyId}` runs the vacancy's live `JobShortlistingCriteria`
  over pool members through the **same** `EvaluateCriterion`, returning per-member score, pass/fail
  per criterion and the breakdown. A `ScoringCandidateView.FromCandidate(JobCandidate)` factory is
  added beside the existing `FromEntity`/`FromSnapshot` (a pool member has no application).
  Also `POST /api/talent-pool/screen` taking an ad-hoc criteria set, for "who do we have?" with no
  vacancy open.
- **B2** Wire the five unwired server-side filters into `talent-pool/page.tsx`
  (min/max experience, available-before, dormant-days, work arrangement, sort), and add a
  **location filter** over `GeoAreaId` subtree — the repository query gains one predicate.
- **B3** Bulk act on a screening result: **Invite to apply** creates `JobApplication` rows
  (`ApplicationSource = TalentPool`) through the existing create path, logs a
  `CandidateEngagementEvent` of type `InvitedToApply`, and sends a new `TalentPoolInvitation`
  template; **Book for interview** then adds them to a session as interviewees (the existing
  `AddIntervieweeAsync` auto-advances the pipeline stage). Partial results with per-row reasons,
  the convention this screen already uses.
- **B4** Show the criteria score as a second column on `TalentPoolMatchesPanel` (D-7 — not merged
  into the 90-point rubric), with `MatchScoreMax` rendered so a bare number has a denominator.
- **B5** Render the candidate photograph on the **application** screens — detail header, the
  pipeline board card, and the screening rows — reusing `GatedPhoto` with
  `jobCandidateService.photoUrl(candidateId)`. No backend work.

**Harness:** `hr-recruitment/run-round4-b.mjs`.

### Lane C — Interview slot apportionment

The slot *columns* were ported; nothing ever computed them, in this solution or the one it came
from. This is new capability, not a restoration.

- **C1** A pure apportioner: given `{ date, startTime, endTime, slotMinutes, bufferMinutes,
  breaks[], applicationIds[] }`, produce a timetable and a **feasibility verdict** — all N fit, or
  *"M fit today; K must move"* with a suggested continuation window.
  `POST /api/job-interviews/apportion-preview` returns it without writing.
- **C2** `POST /api/job-interviews/{id}/apportion` applies a plan onto the session's interviewees.
- **C3** Server-side slot validation on **every** write path (create, add interviewee, `PATCH slot`,
  apportion): inside the session window, no overlap with another candidate, not inside a break.
  None of this is checked today (§ 3 defect 11).
- **C4** Persist the parameters on `JobInterview` — `SlotMinutes`, `SlotBufferMinutes`, `BreaksJson`
  — and **re-apportion on reschedule**. This closes § 3 defect 23: today a reschedule moves the
  window, leaves the slots where they were, and emails each candidate their stale time against the
  new date. Where the session was apportioned, shift the slots by the same rule; where it was not,
  clear them rather than send a time that no longer sits inside the session.
- **C5** Frontend: an "Apportion slots" panel on `interviews/new` and the detail Candidates tab —
  interval, buffer, break rows, Preview showing the timetable and the feasibility line, Apply. Add
  the slot column and inline edit to `InterviewCandidatesPanel.tsx` (it currently prints the literal
  `'Session time'` when a slot is null).

**Harness:** `hr-recruitment/run-round4-c.mjs` — assert the timetable arithmetic with exact times,
and assert each refusal.

### Lane D — Clash check widened; the organizer

- **D1** Introduce `IPanelistCommitmentSource` — one interface, N registered implementations:
  interviews, leave, travel (the existing three), plus **company events via `EventParticipant`**
  (not just the organizer, which is all `HasConflictingEventAsync` checks), **room bookings**,
  **training nominations → `TrainingSchedule`/`TrainingSession` times**, and **business closures +
  public holidays** (`GetInRangeAsync` is the cleanest range API in HR). Rewrite
  `CheckPanelistAvailabilityAsync` to fan out. Fix the N+1 (§ 3 defect 10). Externals get every
  source that can apply to them.
- **D2** Make the check time-precise where the source is, and compare against the **candidate's
  slot** where one exists (§ 3 defect 9).
- **D3** Make it bind: create/update/reschedule call it and **refuse a hard clash unless an override
  reason is supplied**, recorded on the interview (D-5). Soft clashes warn. This is the
  `RoomBooking` rule, which already refuses, applied to panels.
- **D4** `GET /api/job-interviews/suggest-slots?...` — clash resolution: the next windows in a date
  range where the whole panel is free.
- **D5** The organizer: `/hr/company-schedule/my-schedule` (one diary: my events, my room bookings,
  my interviews as a panelist, my training, my leave and travel) and `/hr/company-schedule/team`,
  where a head schedules an event for their unit — participants pre-filled from the org subtree via
  `IHrAudienceResolver.UnitSubtreeAsync`, with a per-participant clash flag **at the point of
  selection**.
- **D6** Event notifications: invite, RSVP chase, reminder, reschedule, cancellation — through
  `ITemplatedEmailService` with a new `CompanySchedule` catalogue, plus in-app.
- **D7** Fix the Company Schedule defects this makes load-bearing: **C-6** repeating event/booking
  numbers (count-based generators over soft-deleted rows, with non-unique indexes — every other HR
  generator was hardened against exactly this); **C-2** reschedule overwriting the original dates
  while the dialog claims they are kept; **C-4** `MaxBookingDurationHours`/`AdvanceBookingDays`/
  capacity recorded and never enforced; **C-1** the red Admin-gated Delete the HR role cannot use.
  **C-5** (closures reach nothing) becomes partly true here, since the clash check now reads them.
- **D8** Give `JobInterview` an optional `RoomBookingId` so an interview actually **holds** the room
  rather than naming it in `LocationOrLink` free text.

**Harnesses:** `hr-recruitment/run-round4-d.mjs` + `hr-company-schedule/run-round4-d.mjs`.

### Lane E — Recruitment tests / aptitude exams

- **E1 Entities**, mirroring Orientation §4: `RecruitmentTest` (code, name, type, instructions,
  `DurationMinutes`, `PassMarkPercent`, `MaxAttempts`, `ShuffleQuestions`, `ShuffleOptions`,
  `IsActive`), `RecruitmentTestSection`, `RecruitmentTestQuestion`
  (`RecruitmentQuestionType { SingleChoice, MultiSelect, TrueFalse, FreeText, Numeric }`, marks,
  explanation, order), `RecruitmentTestQuestionOption` (`IsCorrect`), `RecruitmentTestAssignment`
  (test ↔ vacancy or ↔ application, window, required), `RecruitmentTestSitting` (attempt no, access
  token, started/submitted, auto score, manual score, final, passed), `RecruitmentTestAnswer`.
- **E2 Authoring**: service + `/administration/hr/recruitment/tests` — list, builder
  (sections → questions → options), preview as a candidate sees it, activate.
- **E3 Assignment + invitation**: assign to a vacancy or to named applications, open a window, invite.
  The `AssessmentPending` template **already exists** in `RecruitmentEmailCatalog`.
- **E4 Delivery**: the candidate sits it at `/careers/portal/assessments/[id]` — timer, resume on
  refresh, one submit. The participant read must **never** expose `IsCorrect`, the guard
  `me/orientation/[id]/page.tsx:65` already documents.
- **E5 Marking**: auto-mark closed questions **exactly** as `EmployeeOrientationService.SubmitAssessmentAsync`
  does, carrying its hard-won fix — *the denominator is every gradable question on the paper, not
  just the ones that came back*; multi-select is exact set equality. Free-text gets a manual marking
  screen. Finalising writes a `JobApplicantTestResult` row, then re-scores the application so the
  existing `TestScoreWeight` blend runs (§ 3 defect 8).
- **E6 Offline**: printable paper + marking key (HTML + `window.print()`), and an offline-results
  entry screen.
- **E7** Seed a demo aptitude test; coverage-manifest rows.

**Harness:** new `dev-harness/hr-tests/`. Assert the grading arithmetic against a paper where some
questions are unanswered — that is the case the orientation bug hid in.

### Lane F — The interview paper, printed

**✅ The ported original was found and read.**
`D:\ERP Demo\HRApi\ErpSystem.BlazorServer\Pages\Recruitment\InterviewScorecard.razor` (516 lines),
route `/recruitment/interviews/scorecard/{interviewId}?employeeId=&candidates=`, on a bare
`PrintLayout`, auto-firing `window.print()` once loaded. **It was never ported** — the repo has the
online scorecard and `/me/panel` but no print route at all, and the recruitment guide's § 9.6 does
not mention one. This lane ports it and raises it.

**What the original does, and we keep:** one sheet per candidate with `page-break-after: always`;
a 2px-ruled header with a *"Interview Evaluation Scorecard · Confidential"* kicker, the job title,
and a three-column meta grid (interview #, date, time range, vacancy #, round, candidate); a panelist
band with a role chip highlighted for the Chair, falling back to a blank rule when no panelist is
named; per-question-type sections, each a `# · Question · Scale · Score · Remarks` table with an
empty bordered score box and two ruled remark lines, closed by a **Section Total** row; the five
recommendation tick boxes (Strong Hire → Strong No Hire); four ruled comment lines; and a
signature/date block. Also the candidate-picker modal on the entry point (all candidates, or a
subset) and the blank-placeholder sheet when a panel has no candidates yet.

**Seven things the original got wrong or left out, which this build fixes:**

| | In the original | Here |
|---|---|---|
| 1 | `Weight` is **loaded and never printed**, and the Section Total box has no denominator — an offline marker cannot weight anything | Print the weight per question and the achievable weighted total as the denominator (see F1b) |
| 2 | Only the *current* panelist prints. `_panelists` is loaded and used only to find that one person — **the panel roster is never rendered** | The full panel on a cover page, and the individual panelist on each sheet |
| 3 | **External panelists are absent entirely** — it reads `iv.Panelists` only | Both, externals with their organisation |
| 4 | **No letterhead.** The only branding is the "Confidential" kicker | Company profile, logo and seal, as every other HR document has |
| 5 | Candidate context is the **name alone** | Name, application number, slot time, current employer, years of experience |
| 6 | Styles and every word are **inline in the page** — a client cannot reword it without a deployment | An HR-editable template (see below) |
| 7 | No guidance on what a good answer looks like | `ScoringGuide` on the question (F1c) |

**Everything needed is already loaded.** `IJobInterviewRepository.GetWithFullDetailsAsync` includes
vacancy → position, requisition → job description (the `JobTitle` chain), interviewees → application
→ candidate, panelists → employee → position, and the question plans with their drawn questions.
Only external panelists need an extra include.

**The format.** Server-rendered HTML from an **HR-editable template**, printed by the browser —
**no QuestPDF**. That is HR's house pattern and it was reached by reversing the opposite decision:
`AssetTermsLetterService.cs:20` records why a QuestPDF builder was replaced — it read company details
from `IConfiguration` instead of the per-tenant company profile, and hard-coded the wording in C#
where nobody but a developer could change it. QuestPDF stays available (payslips, procurement use it)
and is not needed here: a scoring sheet is signed on paper, so browser print-to-PDF is the target
anyway, and fix #6 above requires the wording to belong to the client.

- **F1 The service.** `InterviewPaperService`, mirroring `ProbationLetterService` /
  `OfferLetterService` exactly: load, build the token set, render through
  `ITemplatedEmailService.RenderAsync` against a new `Interviews` catalogue, return
  `{ Subject, HtmlBody }`. **One template renders ONE sheet** — the service renders it per
  (candidate × panelist) and concatenates with page breaks, so HR can reword the sheet without
  editing a repeated blob. Tokens: the letterhead and seal (via `ICompanyProfileProvider` +
  `ICompanySealAssetService`, as the offer letter does), interview number, round, type, mode, date,
  start–end, venue or link, vacancy number and job title, department and grade, the candidate's name
  / application number / slot time / current employer / years of experience, the **full panel by
  name, position title and role** (internal and external, externals with their organisation), the
  question table, the recommendation scale as tick boxes, a totals row, and a signature block.
- **F1b The question table is the point.** One row per drawn question grouped by question type,
  carrying **the question, its weight, and its score band** — because the server scores
  `(raw ÷ MaxScore) × Weight`, so a question marked out of 5 with weight 20 counts exactly as much as
  one marked out of 10 with weight 20. A sheet that omits the band cannot be marked correctly
  offline. Print the band as the box label (`___ / 10`), print the weight beside it, and print the
  achievable weighted total as the denominator of the totals row.
- **F1c Add `ScoringGuide` to `JobInterviewQuestionDetail`** (nullable, 2000). The bank holds a
  question, a weight and a band — and **no note on what a good answer looks like**. On screen that
  lives in the interviewer's head; on paper, handed to a panelist who did not write the question, it
  has to be printed. Optional per question, shown on the sheet only when set, and editable in the
  existing question-bank dialog.
- **F2 Three variants**, one endpoint —
  `GET /api/job-interviews/{id}/paper?variant=scoresheet|questions|pack&panelistId=&candidateIds=`:
  **scoresheet** (one per candidate per panelist, with boxes), **questions** (the question list
  alone, no boxes — for the panel's own preparation), **pack** (the cover page naming the **whole
  panel** and the day's timetable with each candidate's slot, then every scoresheet). The
  `panelistId`/`candidateIds` filters are the original's query parameters, kept.
  ⚠ The original gated its print button behind *"blind scoring"* until the panelist had submitted.
  A **blank** sheet leaks nothing, so no gate there; the **questions** variant does reveal the drawn
  set, so it stays behind `EnsureCanReadInterviewAsync` like everything else on this controller.
- **F3 The print route.** `/hr/recruitment/interviews/[id]/paper` renders the server HTML the way
  `me/letters/[id]` does — `printing-hr-letter`'s sibling body class `printing-hr-interview-paper`
  plus a `.hr-interview-paper-print-root` rule in `globals.css`, `dark:bg-white` because a sheet is
  a piece of paper, and `page-break-after` between sheets. Follow the letter family's *flowing*
  model, not the payslip's fixed-A4 box — a paper is multi-page by construction. Buttons on the
  interview detail **Questions** tab and on `/me/panel` (which already carries
  `slot.jobInterviewId`), gated on `EnsureCanReadInterviewAsync` — *HR, or a panelist on this
  interview* — rather than `EnsureHr`. The panelist who needs the sheet is not the person who
  manages the session.
- **F4 Offline score entry.** A "from a paper sheet" mode on the scorecard screen letting HR file a
  panelist's card on their behalf. `EnsureCanScoreAsAsync` already permits this for an **external**
  assessor; extend it to internal panelists and record it — new `FiledByHrOnBehalfOfUserId` and
  `ScoreSource { Online, PaperSheet }` on `JobInterviewScoreSummary`, so the audit trail never
  silently claims the panelist typed it. The existing per-question range guard
  (*"Score 12 for … is outside its 1–10 range"*) and `RefreshTotals()` are reused unchanged.

**Harness:** `hr-recruitment/run-round4-f.mjs` — assert each variant's HTML contains every panelist's
name, every drawn question, and the correct weighted denominator; assert an out-of-band offline score
is refused.

### Lane G — Offer form: defaults and currency

- **G1** `GET /api/job-offers/defaults?applicationId=` — a labelled proposal, every value editable
  and carrying its source: probation/notice months from the position; annual leave days from the
  leave entitlement for the grade/employment type; weekly hours from the work schedule; contract
  duration from the employment type; `LocationLevelId`/`LocationId` from the vacancy/requisition
  unit; `SalaryGradeId/LevelId/NotchId` and `BaseSalary` from the position's grade (notch → level
  midpoint → grade minimum, the same ladder `ManpowerBudgetLine` already uses); `CurrencyCode` from
  the company profile; `ExpiryDate` from `OfferValidityDays` (D-10); `ProposedStartDate` from the
  requisition's required-by date; `NdaRequired`/`IsConditional` from the position.
- **G2** `offers/new` seeds from it and shows the source hints. Bind the orphaned `locationId`
  (§ 3 defect 13) and add the missing `locationLevelId`, `salaryLevelId`, `salaryNotchId` controls.
- **G3** Currency becomes `CurrencyPicker` on `offers/new`, `offers/[id]/edit` and
  `OfferBenefitsPanel`; the server validates through `HrCurrencyBridge.RequireKnownCurrencyAsync` on
  create, update and add-benefit (§ 3 defect 14).

**Harness:** `hr-recruitment/run-round4-g.mjs`.

### Lane H — The checklist in the offer letter

- **H1** Seed the check set when an offer is created, from the position's default
  `PreEmploymentCheckTemplate` (a new `PreEmploymentCheckTemplateId` on `EmployeePosition`, falling
  back to the single active default). Editable and deletable afterwards. This also removes the trap
  where `AcceptConditionallyAsync` refuses because nobody created the set by hand.
- **H2** Delete the five invented fallback conditions (§ 3 defect 12) and render the real items with
  what the candidate must produce: `Name`, `Instructions`, a mandatory/blocking marker, `ExpectedDays`.
- **H3** Render the checklist for **every** offer — *"Pre-employment requirements"* on a
  non-conditional one, *"Conditions of this offer"* on a conditional one. New tokens
  `{{PreEmploymentChecklist}}` and `{{HasPreEmploymentChecks}}`; `{{ConditionsList}}` keeps working.
- **H4** Update the shipped `OfferLetter` default body in `RecruitmentEmailCatalog`.

**Harness:** `hr-recruitment/run-round4-h.mjs` — assert the rendered HTML contains the seeded item
names and **does not** contain the invented fallback strings.

### Lane I — Triggers that fire

- **I1** `OrientationAudienceScope` migrates onto `HrAudienceTargetType` + `IHrAudienceResolver`
  (D-9), keeping `NewHires`, `Management` and `Contractors` as derived predicates layered on top.
- **I2** `TargetEntityId` becomes a **typed picker** driven by `TargetType` (unit / position / level
  / location / employee), `TargetEntityName` is finally populated in
  `OrientationMappingExtensions.ToDto` (§ 3 defect 15), and the form gains a **reach preview**
  (*"this rule reaches 412 people"*) using `CountAsync` — the announcements form already does this.
- **I3** `OrientationEnrollmentTriggerService` evaluates active rules and creates `EmployeeOrientation`
  rows: on hire (from `ConfirmStartAsync`, employee create and the import commit), on transfer, on
  promotion, on program publish, and on a `Scheduled` sweep — honouring `EnrollmentDelayDays` and
  `IsInclusive`, and deduped so a re-run cannot double-enrol.
- **I4** Onboarding template applicability — the shape two code comments already specify:
  `OnboardingPlanTemplateAudience` (TargetType / TargetEntityId / IsInclusive),
  `GET /api/onboarding-plan-templates/applicable?positionId=&orgUnitId=&gradeId=&locationId=` with an
  explicit precedence rule, and **automatic plan creation when a hire's start is confirmed** —
  making true the claim `RecruitmentEntities.cs` has always made.
- **I5** A diagnostic: *"which rules would fire for this employee, and why"*, so the PDF's question
  is answerable inside the product rather than by reading code.

**Harness:** `hr-orientation/run-round4-i.mjs`.

### Lane J — Copy a template

- **J1** `POST /api/onboarding-plan-templates/{id}/clone` `{ newName }` — copies task templates,
  forces `IsDefault = false`, refuses a duplicate name. Mirrors `JobPostingPipelineService.ClonePipelineAsync`.
- **J2** `POST /api/orientation-programs/{id}/clone` `{ newName, newCode }` — copies modules,
  content items, prerequisites, assessment questions **and their options**, and audience rules;
  forces `Status = Draft`. Mirrors `AppraisalTemplateService.CloneAsync` (nested rebuild with
  `TenantId` stamped at every level, one `SaveChangesAsync`, re-read for navigations).
- **J3** `POST /api/orientation-sessions/{id}/clone` with a new date — re-running the same briefing
  is the common case.
- **J4** "Duplicate" action on both admin lists and the session list.

**Harness:** `hr-orientation/run-round4-j.mjs` — assert child counts on the copy, and that editing
the copy does not touch the original.

### Lane K — Notifications that actually send

- **K1** `OnboardingOrientationReminderService` + its `BackgroundService`, following
  `ProbationReminderService` exactly: run header, dispatch log, dedupe key **including the due date**
  (so moving a deadline re-arms the reminder), distributed lock, per-tenant loop with per-tenant
  failure isolation, staggered start. Rules: onboarding task due soon / overdue / awaiting
  verification; orientation enrolment due soon / overdue / assessment not attempted /
  acknowledgement outstanding / certificate expiring.
- **K2** ⚠ **This sweep must deliver, not just log.** None of the 14 existing HR sweeps sends
  anything. This one writes an `OrientationNotification` (in-app) **and** sends through
  `ITemplatedEmailService` with a new Onboarding/Orientation catalogue. Record the wider finding in
  `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`.
- **K3** Lifecycle notifications from the events themselves: enrolled, session scheduled /
  rescheduled / cancelled, completed, certificate issued, plan assigned, task assigned.
- **K4** Admin surface — `POST /reminders/run`, `GET preview?asOf=`, `GET runs`, `GET log?days=` —
  and lead-day settings on `CompanyHrPolicySettings`, entered in `HR-CONFIGURATION-REGISTER.md` with
  their enforcement status.
- **K5** ⚠ **Register the hosted service in the same commit.** Two HR sweeps in this codebase turned
  out never to have run because the `AddHostedService` line was missing —
  `TeamReminderBackgroundService`'s own doc comment says so. Verify the run table has rows before
  calling this done.

**Harness:** `hr-orientation/run-round4-k.mjs` — assert rows in the run table **and** a delivered
notification, not merely a 200 from the trigger endpoint.

> **Split, like D and E** (sequencing, not narrowing): **K-a** = K1, K2, K4, K5 — the sweep that
> delivers, its settings, its admin surface and its host — **done 2026-09-23**, see § 8. **K-b** =
> K3, the lifecycle notifications, on K-a's delivery code.
>
> **K-b's survey changed its shape — two decisions, the user's, 2026-09-23.** (1) **No screen could
> issue an orientation certificate** — the programme's "Issues a certificate" switch promised one
> "when HR certifies a passed enrolment", and HR had no button; the only certificate on UAT is the
> seeder's. Decided: issued **automatically at completion** for a programme set to issue one, **and**
> an HR **Issue certificate** button for completions from before and for reissues. (2) **New hires,
> buddies and non-HR task assignees can open no onboarding page** (the plan controller defers their
> views to the portal). Decided: K-b **builds them** — "My onboarding" and "Tasks assigned to me" with
> Mark done — so every notice leads somewhere. Also found: the programme's **"Send reminders"**
> switch ("enrolment, deadline and overdue notices") is read by nothing, so K-a reminds on a
> programme where it is off; K-b makes it govern exactly what it says. Sequenced as **K-b1**
> (orientation notices, an email outbox — a publish can enrol hundreds in one save, so emails cannot
> go inside the request — certificates, the switch) and **K-b2** (onboarding notices + the portal
> views).

### Lane L — The sessions dropdown

- **L1 Reproduce first.** Probe `GET /orientation-sessions/program/{id}` for every seeded programme
  against the UAT database and record the answer. Expected finding: only `ORI-PRD-001` has a
  session, so the others are legitimately empty — a data gap, not a code bug. Do not ship a fix for
  a cause that was not confirmed.
- **L2** Make the screen stop lying whatever the cause: distinguish *loading* / *this programme has
  no scheduled sessions* (with a link to schedule one) / *this programme is self-paced* / *you do
  not have permission* — today `const { data: sessions = [] }` renders a **403 identically to an
  empty list**.
- **L3** The enrolment dialog offers every programme via `getAll()` while the session screen only
  lets you schedule against **Active** ones. Align them, or mark the status in the picker.
- **L4** Mark or hide Cancelled and Completed sessions in the enrolment dropdown — it renders only
  the title and seat count today.
- **L5** Seed sessions for the onboarding and compliance programmes.

**Harness:** folded into `run-round4-i.mjs`.

### Lane M — External facilitators from a register

- **M1** `OrientationSessionFacilitator` gains `ExternalFacilitatorVendorId` and
  `ExternalFacilitatorTrainerProfileId` (both nullable). The three free-text columns stay as the
  **snapshot**, mirrored from the picked vendor/trainer at save — the same
  reference-plus-snapshot pattern `PreEmploymentCheckItem.ServiceProviderName` already uses, so
  renaming a vendor later does not rewrite what an old session says.
- **M2** The facilitator dialog gets three modes: employee / vendor + trainer from the setup / free
  text, fed by `GET /api/training-vendors/active` and that vendor's `TrainerProfile` list.
- **M3** Check `TrainingVendorService.DeleteAsync` does not orphan a facilitator.
- **M4** *(added by lane J)* **Run again copies facilitators field by field** —
  `OrientationSessionService.CloneAsync`. The two new columns must be copied there too, and added to
  the `copied` list for `OrientationSessionFacilitators` in `run-round4-j.mjs`. That suite's block Z
  **fails on any column nobody has decided about**, so M's migration turns it red until this is done
  — on purpose. **Done in lane M** — both columns copied, block Z 94/94 after the migration.

**Harness:** `hr-orientation/run-round4-m.mjs`.

### Lane N — HR letter and notification templates

**The ported solution had this screen** — `RecruitmentEmailTemplates.razor`, with an event-key
selector, a click-to-insert merge-field palette, preview-with-sample-data in an iframe, and test
send. Build to that shape; the backend already supports all of it (`RenderInline` exists and has no
caller) and the only missing piece is the API that exposes the token catalogue.

- **N1** The missing API, gated on an HR permission and scoped to HR modules:
  `GET /api/hr/letter-templates/events` (module, event, name, category, description, tokens with
  descriptions and sample values, whether a stored row exists and whether it differs from the
  shipped default), `GET`/`PUT /{module}/{eventKey}`, `POST /{module}/{eventKey}/preview`
  (`RenderInline` — **the method exists for exactly this and has no caller**),
  `POST .../test-send`, `POST .../reset`.
- **N2** Seed the catalogue rows on **normal** tenant startup. `EmailTemplateCatalogSeeder` runs
  only under `HrDemoSeedOrchestrator` today, so a fresh tenant has no editable rows at all — which
  is precisely the *"only visible in the code"* the feedback describes.
- **N3** `/administration/hr/letter-templates` — grouped list, editor with an insertable token
  palette, live preview, test send, reset to default, and an *edited / shipped default* badge. Add
  it to the HR administration sidebar group.
- **N4** Add the catalogues the other lanes need so the screen covers HR end to end: Onboarding and
  Orientation (K), Company Schedule (D), the interview question paper (F), the talent-pool
  invitation (B3). Recruitment's `AssessmentPending` already covers lane E's invitation.
- **N5** Enter every template in `HR-CONFIGURATION-REGISTER.md`.

⚠ **Scope boundary.** There are **four** unrelated template systems in this solution:
`EmailTemplate` + `IEmailEventCatalog` (what this lane covers), `NotificationTemplate` on
`INotificationService` (in-app/push), `MaintenanceNotificationTemplateService`, and the DB-field
designer at `/administration/settings/email`. This lane touches **only the first**, and the screen
says so, so nobody edits here expecting an in-app notification to change.

**Harness:** new `dev-harness/hr-templates/`.

### Lane N-b — The senders: four emails that go wrong, or never go

**Added 2026-09-23, after lane N.** Lane N's survey of every sender (register § 2.7) found four
emails that the catalogue promises and the code does not deliver as described. Every one is an email
a candidate or a participant reads. The user asked for them to be fixed, so this lane runs before O.

- **Nb1 — Application Withdrawn, made reachable.** Its only sender, a token-link withdrawal, has no
  caller. The live withdrawals send nothing: the candidate on the portal, an employee applicant
  withdrawing their own, and HR withdrawing on a candidate's behalf. Send it from all three; the
  tenant is the signed-in caller's.
- **Nb2 — Under Review, once, on entering review, from either door.** The older stage-move door
  (`JobApplicationController` → `MoveToStageAsync`) sends it after **every** move, whatever the stage.
  A move into an assessment stage therefore sends it together with Assessment Pending, and a move to
  an interview or an offer sends "Good news — your application is now being actively reviewed". The
  pipeline board's door never sends it at all. Fix it with one rule in the one writer
  (`ApplicationPipelineService`): send Under Review when an application first moves from Submitted
  into a review stage (application review, screening or hiring-manager review), and never otherwise.
  Remove the older door's own send, so that both doors send the same emails.
- **Nb3 — Offer Accepted, when the candidate accepts it themselves.** Today only HR's recording of
  an acceptance sends it. Send it from the token link and from the portal too. ⚠ The token link is
  **anonymous**: it must name the offer's tenant (`SendForTenantAsync`, lane N), or the tenant's own
  wording is skipped and the email carries the fallback company name.
- **Nb4 — Company-schedule reminders that send themselves.** The event form's **Send reminders** and
  **days before** fields are saved and read by nothing, and `ReminderSentDate` is never written, so
  both are ghosts. The RSVP chase exists only as a button.
  - A daily sweep sends each event's reminder that many days before the event, **once**, and stamps
    `ReminderSentDate`.
  - The same sweep chases unanswered invitations ahead of the RSVP deadline, **once**. This needs a
    new per-event marker, so the lane has **a migration**.
  - The chase's lead time is a tenant setting (default 2 days), proved in both positions and entered
    in the register.
  - The manual buttons stamp the same markers, so nobody is told twice.
  - Background sends name the event's tenant, which is lane N's lesson.

- **Nb5 — The offer letter, attached to Offer Issued (the user's call, 2026-09-23).** The survey
  found the letter was never emailed, although two comments said it was. Asked whether to correct
  the comments or attach the letter, the user chose to attach it as a PDF.

**Split, like lane K: N-b1** (Nb1, Nb2, Nb3, Nb5, the recruitment senders, no migration) and **N-b2**
(Nb4, the company schedule, with the migration).

**Harness:** `dev-harness/hr-templates/run-lane-nb1.mjs` and `run-lane-nb2.mjs`, through the local
SMTP sink. Every email is asserted to **arrive**, and to arrive **once**.

### Lane O — The technician-role flag, and the Maintenance door it feeds

> ⚠ **Executed differently in three places — read § 8's lane O entry before this section.**
> 1. The one predicate is the **stored** `CanBeAssignedToMaintenance`, maintained at save time, not
>    O2's computed OR: five Maintenance gates read the column.
> 2. The exception works **both** ways (Include / Exclude). O2's OR could not exclude anyone.
> 3. **Defect 21 was wrong.** Maintenance reads both workload columns, so they are recorded rather
>    than surfaced (O4, O5).

**The lane is not "add a flag and find it a reader". It is "give four disagreeing definitions one
answer."** Maintenance already pulls technicians from HR constantly — but *"is this person a
technician?"* is decided four different ways, none of them a role, two of them by magic string, and
one of them by a Maintenance service **writing HR's `Employee` row**:

| Predicate | Where |
|---|---|
| `e.CanBeAssignedToMaintenance \|\| e.Department.Code == "MAINT"` | `EmployeeRepository.GetEmployeesForMaintenanceAsync` — backs HR's own technician door |
| `e.OrganizationUnit.Name.Contains("Maintenance")` | `TechnicianRepository.GetTechniciansAsync` (+8 siblings) — a **different org model** from the line above |
| `e.CanBeAssignedToMaintenance` | `EmployeeService.cs:1231`, behind `EmployeeSearchDto.MaintenanceTechniciansOnly` — **a filter with no caller anywhere** |
| org-unit name again, **then `employee.CanBeAssignedToMaintenance = true`** | `TechnicianService.CreateTechnicianAsync` — Maintenance writing an HR column, the inverse of a read door |

Three more things the survey established, each of which changes what this lane should do:

- **`Employee.PositionId` is `[Required]` and non-nullable.** Every employee has a post, so a
  role-level flag can be the *primary* definition without losing anybody.
- **Maintenance's `Technician` table is dead.** `ITechnicianRepository` is
  `IGenericRepository<Employee>`; the `Technicians` table is written only by two seeders and read by
  no service. There is no snapshot to refresh — **do not build one.** Same for `TechnicalSkill`,
  whose repository reads HR's `Skills` table.
- **The HR→Maintenance sync does not run, and never has.** `TechnicianRepository.GetFromHRModuleAsync`
  is `// TODO: Implement actual HR module integration` returning `new List<Employee>()`
  unconditionally, so `SyncTechniciansFromHRAsync` and `POST technical-skills/sync-from-hr` iterate
  nothing. This **answers the open question** the integration map records as *"sync not verified to
  run"* — it should read 🔴, not 🟡.

- **O1** `EmployeePosition.IsTechnicianRole : bool` (default false), mirroring `RequiresLicense`
  through every rung: entity → guarded-SQL migration → the three DTOs → the three service mappings →
  frontend type → the form switch beside `RequiresLicense` (⚠ this form uses the raw shadcn
  `<Switch>` + `form.setValue`, **not** the `SwitchField` helper used elsewhere in HR — match the
  file) → the list column and a list filter. The list is simpler than it looks: the grid calls
  `employeePositionService.getAll()`, which returns the **full** `EmployeePositionDto`, so no new
  projection is needed. Add it to `EmployeePositionLookupDto` too, for pickers. It sits naturally
  beside `PositionSkillRequirement`, the position-level skill model that already exists.
- **O2 One predicate, in one place.** A single shared specification —
  **`position.IsTechnicianRole || employee.CanBeAssignedToMaintenance`** — where the flag states the
  rule and the per-person bool is the **exception** (someone seconded in, or a technician on
  long-term light duties opted out). Apply it at all four sites above and **retire both magic
  strings**: `Department.Code == "MAINT"` and `OrganizationUnit.Name.Contains("Maintenance")`. Give
  `MaintenanceTechniciansOnly` its first caller, or remove it.
- **O3 Repoint the dropdown that actually ships.** Neither technician endpoint is what the Maintenance
  screens call. `maintenanceDataService.getTechnicians()` and `maintenanceApiService` both hit
  **`GET /employees/maintenance-available`** on the *root* `EmployeesController`, which returns the
  **full `EmployeeDto`** — and one of them **falls back to `/employees?pageSize=1000` — every employee
  in the tenant — when it errors.** Until that call is moved onto the narrow door, the flag changes
  nothing a user sees. Point both at `GET api/hr/employees/technicians`, delete the
  every-employee fallback, and retire the root duplicate.
- **O4** Placing an employee into a technician position **defaults** `CanBeAssignedToMaintenance`
  true; leaving one clears it unless it was set by hand. And **surface it, with the five technical
  fields, in the HR employee form** — grep returns **zero frontend references** to any of them, which
  is exactly why Maintenance ended up writing the column itself (O2's fourth row).
- **O5** Fix the door's projection — but **fill only what HR actually knows**.
  - **Add** `IsTechnicianRole`, `Specialization`, `CertificationLevel`, `ExperienceLevel` and the
    department to `MaintenanceTechnicianDto`.
  - **Fill** the nested `UserTechnicianSkillDto` properly — `SkillId`, `ProficiencyLevel`,
    `CertificationDate` and `CertificationExpiry` currently go back as defaults, so a consumer cannot
    tell a lapsed certification from a current one. HR holds all four.
  - **Do not fill** `CurrentWorkOrders`, `WorkloadScore` or `ShiftName`. ⚠ The tempting fix is to read
    them from `Employee.CurrentWorkload`/`MaxWorkload` — **do not**. Those two columns are written and
    read by **nothing**; Maintenance computes real utilisation from work orders in
    `TechnicianSchedulingService`. Return them as explicitly not-supplied rather than shipping a zero
    that reads as *"this technician is free"*, and record the two HR columns as **dead** in the
    configuration register under its own rule.
- **O6** Record it: correct rows 4 and 5 of `HR-MODULE-INTEGRATION-MAP.md` (the sync is dead, not
  unverified), add the competing-predicate finding to
  `CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` — **it is unrecorded ground today** — and enter the flag
  in the configuration register. **Do not build Maintenance's side**: retiring its org-unit-name gate
  and its write into `Employee` is Maintenance's to do, and goes out as a hand-off.

**Harness:** `hr-jobarch/run-round4-o.mjs` — assert the flag round-trips; that an employee in a
technician position appears in `technicians` **without** being individually ticked; that an employee
in a unit merely *named* "Maintenance" no longer appears by that fact alone; and that the skill rows
carry their certification dates.

### Lane Q — Education level on the ladder (added 2026-09-24: the proper fix for R4-5.2)

**The lane exists because P2a's data repair found that no real degree can pass the seeded
"A relevant first degree" criterion.** That criterion matches the word "degree" in a qualification's
name. Four facts make the fix larger than a new value:

| Fact | Consequence |
|---|---|
| `EducationLevel` is shaped *"Scored exactly like Qualification"*: a name match | The type exists in name only; nothing compares levels |
| A ranked ladder exists, 11 `QualificationLevels` from BECE (10) to Doctorate (90), with HND and Bachelor's tied at 50 as equivalents | It is created by scenario 005, and nothing reads it |
| None of the 186 catalogue qualifications has a `QualificationLevelId` | A catalogue pick carries no level either |
| 211 of 237 candidate qualifications are typed text, with no catalogue link | Most candidates have no route to a level at all |

**Decisions, the user's (2026-09-24):**

| # | Decision |
|---|---|
| Q-D1 | **A Level field on each candidate qualification**, from the ladder. It is pre-filled from the catalogue entry when one is picked, **required for Education**, and optional for licences, memberships and the rest. |
| Q-D2 | **Existing rows are filled once from their names** (BSc, BA or Bachelor → Bachelor's; MSc, MBA or Master → Master's; HND; Diploma; PhD; WASSCE…). A name that cannot be read stays empty, and is reported. |
| Q-D3 | **Below the required level fails the criterion**: 0, and disqualified if mandatory. No partial credit. |
| Q-D4 | **No qualification with a level counts as a miss** (0), like Location's no-area rule. |

- **Q1 Schema.**
  - Add `JobCandidateQualification.QualificationLevelId`: nullable, a foreign key to the ladder.
  - The *effective* level is the qualification's own, or else its catalogue entry's.
  - One migration, which the user scaffolds and I rewrite as guarded SQL, does three things: adds
    the column; runs the Q-D2 backfill on typed rows, filling empty ones only; and maps the 186
    catalogue entries onto the ladder by name, also filling empty ones only, so a level HR later
    sets is never overwritten.
- **Q2 Doors.**
  - The candidate-qualification create, update and read DTOs carry the level.
  - Both doors refuse an Education row with no level, and a level that is not an active rung of the
    tenant's ladder. The doors are HR's candidate tab and the careers profile.
  - The catalogue read carries each entry's level, so both forms can pre-fill it.
  - The careers portal reads the ladder from its public catalogue.
- **Q3 Engine.**
  - `EducationLevel` takes a new value kind, `QualificationLevel`, appended to the enum and never
    renumbered: one value, the minimum rung. The resolver validates it.
  - The evaluator compares **ranks**: at or above passes (1), below fails (0), no levelled
    qualification is a miss (0). Its notes name the best rung found and the one required.
  - The ladder is read once per scoring run by each caller: an application, *Score all* and the
    talent-pool screen.
  - Snapshots gain the effective level. An older snapshot back-fills from the catalogue and the live
    profile, as `GeoAreaPath` does.
- **Q4 Screens.**
  - The criteria panel gets a level picker for *Education level*, which the talent-pool screen
    shares.
  - Both qualification forms get a Level select.
- **Q5 Demo.**
  - The backfill seeder writes the first-degree criterion as *Education level at least Bachelor's*.
  - Scenario 005 maps the catalogue onto the ladder it creates.
  - Scenario 050 sends a level with every qualification it records, Elikem's included.
  - Scenario 052's three degrees become plain *BSc …* rows at Bachelor's level.
  - UAT's six live-pipeline criteria are converted through the API, and VAC-000021's three scripts
    are re-scored one at a time.
- **Q6 Harness and docs.**
  - A new suite, `hr-recruitment/run-round4-q.mjs`, plus the neighbours re-run on count.
  - Guide § 5.8's criterion table, R4-5.2 closed, and the configuration register.

### Lane P — Documentation, harnesses, demo data

- **P1** `docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-4-PLAN.md` — this plan, in the house format, with
  a per-slice log.
- **P2** Rewrite the affected guide sections: recruitment §5.8 (scoring), §9 (interviews), §10
  (offers), §13 (talent pool); the company schedule guide's six rules; a new orientation guide.
  Split into **P2a** (the recruitment guide, with a new chapter 8A for lane E: DONE 2026-09-23),
  **P2b** (company schedule) and **P2c** (orientation).
- **P3** Harnesses per lane, each green **twice**, plus the existing suites re-run on count
  (`hr-recruitment` slices A–F, `hr-orientation`, `hr-company-schedule`, `hr-jobarch`).
- **P4** `demo-coverage-manifest.csv` rows and seeding for every new fillable column.

---

## 5. Verification

**Environment** (`docs/HR/operations/HR-VERIFICATION-HARNESS-GUIDE.md`): API in **Staging** with the
JWT key passed in, `--no-launch-profile` (the launch profile forces Development, where the developer
exception page turns every refusal into a 500 with a stack trace and every status assertion lies).
Confirm `Hosting environment: Staging` in the startup log. Run `clamd-stub.mjs` for the upload
assertions.

**Per lane:** its harness green twice, plus the neighbouring suites re-run on count — *fewer
assertions with zero failures is a regression signal, not a pass.*

**Things a harness cannot prove, so they are walked in the browser** (three planned; the fourth added
by lane K-b2):

1. The apportionment panel and the timetable it draws (lane C).
2. The candidate sitting a test end to end in the careers portal, including refresh mid-test (E4).
3. The template editor (N3): reword an email with the token palette (the insert lands at the
   cursor, in the field last used), watch the preview and its problems follow the typing, save, send a
   test to yourself, and reset. Also open one template directly with `?t=Module/EventKey`.
4. **My onboarding**, signed in as three people — the new hire (their plan, who owes what), an IT
   assignee (Mark done, then waiting on the sign-off) and a buddy — each seeing only their own; and a
   notice's link landing each of them on it (K-b2).
5. **An event's Reminders card** (N-b2): what went and when, or when it will. Press **Send reminder
   now** and see the card say it went. Also the policy page's **Company schedule reminders** card, and
   the event form's explained **Send reminders** switch.
6. **The technician role, end to end** (lane O).
   1. Switch on **Technician role** for Artisan and see the positions list's badge and filter.
   2. On an employee's edit form, the **Maintenance** section should say *"Available: … is a technician
      role"*. Change the position on the form, before saving, and watch that sentence change.
   3. Save *Include* for a Facilities Officer.
   4. Open **Maintenance → Work orders** and see both people in the technician dropdown.
   5. Assign one of them to a work order. The assignment gate reads the same column, but the harness
      could not exercise it, because UAT has no work orders.
   6. Re-open a post's edit page and save it without touching anything: its **Pre-employment checks**
      choice must survive (the lane H residue).

**Live probes before building, not after:**

- Lane L1 — the sessions dropdown, against the UAT database, before writing any fix.
- Lane A3 — re-score the demo applications and diff the scores; a criterion that stops awarding free
  marks **will** move numbers, and the movement is the proof.
- Lane K5 — `SELECT COUNT(*)` on the new run table after the first scheduled fire.

---

## 6. Open questions, each with a default I will proceed on

| # | Question | Default |
|---|---|---|
| Q1 | ~~What is the technician-role flag for?~~ **Answered: Maintenance, to pull employees holding technical roles.** Remaining: should the flag **replace** `Employee.CanBeAssignedToMaintenance`, or sit above it? | Sit above it — the role states the rule, the per-person bool is the exception (O2). Replacing it outright would strand secondees and technicians on long-term light duties, both real cases. |
| Q1b | Retire `GET /employees/maintenance-available` once the screens are repointed, or keep it as a deprecated alias? | Retire it. It returns the full `EmployeeDto` from the root controller — an over-broad read surface that exists only because the narrow door was never wired up. Removing it is what makes O3 stick. |
| Q2 | Should the talent-pool criteria screen be able to **auto-shortlist**, or only invite to apply? | Invite only. Shortlisting belongs to an application against a vacancy, and short-circuiting that loses the audit trail. |
| Q3 | Does a failed aptitude test **reject** the application automatically? | No. It records the mark and feeds the blended score; rejection stays a human act carrying a reason, as `CompleteAsync` already does for interviews. |
| Q4 | Can a candidate **retake** a test, and who authorises it? | `MaxAttempts` on the test (default 1); HR can grant one extra attempt with a reason recorded. |
| Q5 | Should a hard interview clash be **refusable at all**, or always overridable? | Always overridable with a recorded reason. A recruiter who knows the panelist swapped a meeting should not be blocked. |
| Q6 | Does the offer letter's checklist show **every** item, or only the candidate-facing ones? | Every mandatory or blocking item. An internal-only check is not a condition the candidate can act on — add an `IsCandidateVisible` flag if TDC wants the distinction. |

---

## 7. Sequencing

```
A ──► B ──► (B5 is independent, can land any time)
C ──► D ──► E4 (the portal sitting reuses D's window/notification plumbing)
       └──► F
G ──► H
I ──► L (L's fix depends on I's trigger work for the "self-paced" distinction)
J, K, M, N, O — independent
N ──► N-b (the senders N's survey found; added 2026-09-23)
P — continuous
```

Recommended order: **A, C, F, G, H, B, D, E, I, L, J, M, K, N, N-b, O**, with P alongside.

- **A and C first** — the scoring audit and the slot apportioner are the two the demo audience will
  look at hardest, and A's geography change ripples into B.
- **F early, right after C** — it is self-contained, the original to port has been read, and a
  printable scoring sheet is the most visible thing in this document per hour spent.
- **D and E late in their chains** — the largest builds, and both benefit from C landing first.
- **O last but not least** — it is independent, but it is the one lane that ends in a hand-off to
  another module's owner, so starting it early only means the hand-off waits longer. It also carries
  the most unrecorded findings, so budget documentation time (O6) alongside the code.

---

## 8. Execution log

### Lane A — DONE 2026-09-21 · 45 assertions ×2 · commit `10bb1d4f`

Harness: `dev-harness/hr-recruitment/run-round4-a.mjs`. Migration:
`20260921152937_AddRecruitmentGeography` (guarded SQL; applied to `ErpSystemDB_UAT`).

**Built as planned** — `JobCandidate.GeoAreaId` + derived `Region`, `City` demoted to a display
snapshot, the shared `GeoAddressSnapshot` helper on all three write paths, two `IGeoAreaConsumer`
probes, `ShortlistingValueKind.GeoArea`, containment matching, the `AddressFields` cascade on the HR
candidate form and the careers profile, and the scoring audit.

**Changed during the build, with reasons:**

| | Planned | Built | Why |
|---|---|---|---|
| Candidate with no area, area-only criterion | excluded from the score | **scored as a miss (0)** | A `GeoArea` value carries the area's name mirrored onto it, so there IS something to compare: typed "Kumasi" against "Greater Accra" is a demonstrable non-match, not an unanswerable question. Excluding it would let a candidate with no address outrank one who genuinely does not match — the "rewards missing data" fault G-13.2 removed from the talent pool. The harness asserts **both** halves: the miss, and a typed city that hits. |
| Empty-criterion exclusion | asserted through scoring | **asserted as a write-path refusal** | Only `Other` accepts no values, and `Other` is already excluded from scoring by design — so an empty auto-evaluated criterion **cannot be created through the API**. The evaluator fix is defence-in-depth for legacy rows; the reachable guarantee is the 422. A scoring assertion there could never reach its own assertion. |
| — | not planned | **`totalWeight == 0` implies a null score** | Found writing the harness. When every criterion was unevaluable the score fell through to **100** — the same inflation round 3 lane K removed one level up, reached from below. It barely mattered while `Other` was the only route in; this slice added three more exclusion paths, and one is ordinary: a vacancy screening on Greater Accra, scored against public-form applicants with no area, would have handed **every one of them 100**. |
| — | not planned | **`JobVacancy.isBlindScreeningEnabled` on the read type** | A live TypeScript error: the vacancy edit page reads it off `JobVacancy`, only `CreateJobVacancy` declared it, and the server DTO has carried it since blind screening was built. |
| — | not planned | **three anonymous geography reads** on `PublicRecruitmentController` | `api/reference/geo` is `InternalOnly`, a **blocklist** excluding `Candidate` — so the careers cascade would have been refused for every candidate, and refused **invisibly**, because react-query's empty-array default renders as a dropdown reading *"this country has no scheme"*. |

### Careers registration — DONE 2026-09-21 (amended into `10bb1d4f`)

Not a lane; a live defect found while re-running the neighbouring suites.

**A self-registered candidate had no route to an active account.** `RegisterCandidate` creates the
user `IsActive = false` and sends only an **SMS OTP**, deliberately best-effort *"so registration
does not fail if SMS fails"*. On any deployment without an SMS provider — including this one, where
the log reads *"Twilio SMS is not enabled / GhanaGateway SMS is not enabled / All fallback SMS
providers failed"* — registration returns success, the code never arrives, and login answers
`IDENTITY_USER_INACTIVE` forever.

Email confirmation was not a way out either: `api/candidate/confirm-email` sits behind
`[Authorize(Policy = "CandidateOnly")]`, requiring the login the inactive account cannot obtain, and
set `EmailConfirmed` **without ever touching `IsActive`**. A closed loop.

Built: registration also emails an activation link; anonymous `POST api/auth/candidate/activate`
confirms the address **and** activates; a companion `activate/resend` returns one neutral message
whichever address it is given. Anonymous is forced by the situation and is not unguarded — the
Identity token is single-use, expiring, bound to one user and purpose, and the endpoint refuses any
account not in the `Candidate` role. Wording lives in `RecruitmentEmailCatalog`, not the controller,
so it carries the branded shell and HR can reword it without a deployment.

⚠ **The happy path is not harness-proven and cannot be**: the token exists only in the mailbox. The
refusal paths are (bogus token 400, missing token 400, resend neutral for an unknown address **and**
for a non-candidate). A browser walk against a deployment with SMTP configured is owed.

---

### Lane F — the printed interview paper · DONE 2026-09-22 · 103 assertions ×2

Harness: `dev-harness/hr-recruitment/run-round4-f.mjs` (103 assertions). Migrations:
`20260921224126_AddInterviewQuestionScoringGuide` and `20260921231628_AddInterviewScoreProvenance`
(both guarded SQL; both applied to `ErpSystemDB_UAT`).

**Built as planned** — F1/F1b/F1c/F2/F3/F4: the paper service rendering three variants from an
HR-editable `Interviews` catalogue, the question table carrying weight *and* band *and* the
achievable weighted total, `ScoringGuide` on the question bank, the print route with its own body
class, and offline score entry recorded rather than silently attributed.

**Changed during the build, with reasons:**

| | Planned | Built | Why |
|---|---|---|---|
| `FiledByHrOnBehalfOfUserId` | a user id | **`FiledByHrOnBehalfOfEmployeeId`**, an Employee FK with a navigation | The value reaching the service is `_currentUser.EmployeeId`, passed into a parameter the controller family calls `createdByUserId`. A `...UserId` column holding an employee id is a trap that surfaces the day somebody joins it to `AspNetUsers`. HR's actor columns are employee references throughout. |
| `ScoreSource` on the create DTO | client-supplied | **no DTO field at all; derived in `EnsureCanScoreAsAsync`** | A provenance the caller can assert is worth nothing in an audit, and a DTO field the server ignores reads as a control that exists when it does not. The gate already knows the only fact that settles it — whether the caller *is* the panelist. |
| — | not planned | **the pack was double-wrapping its sheets** | `RenderScoreSheetsAsync` returned already-joined HTML and the pack joined it again: six sheets nested inside one section, the page break in the wrong place, `SheetCount` reading 8. The question list was not wrapped at all, so it reported 0. Both fixed by joining exactly once, in the caller. |
| — | not planned | **a false claim in my own comment** | I wrote that `GetWithFullDetailsAsync` omits external panelists. It does not — `WithSummaryNavigations` includes them. Dropped the redundant repository round-trip; kept the soft-delete filter, which an EF `Include` does not apply. |

**Lane F5 — the panelist's own scorecard, added after review.** Not in the plan. A panelist's only
route to a scorecard ran through HR's interview desk and its Candidates tab — three clicks and a tab
— while `/me/panel` listed interview *numbers* and described itself as *"the scorecards you owe"*.
Built: `GET api/job-interviews/me/scorecard-worklist`, a worklist `/me/panel`, and
`/me/panel/[interviewId]/score/[intervieweeId]`.

⚠ **The scorecard form was extracted into one shared component before the second route was added.**
The earlier decision against a portal scorecard (recorded on `/me/panel`) warned it would mean *"two
scorecard forms against one upsert endpoint, which is how a scorecard gets silently replaced"* — an
objection to two *implementations*, not two *routes*. `InterviewScorecardForm` says so; do not fork it.

**Blind scoring — decided, then built.** Every score read was gated on *read* access, so any panelist
could read a colleague's totals, recommendation and private comments before filing their own. Now
blinded until they file, per **candidate** rather than per interview. ⚠ **Four** read paths leaked,
not one: the list, the card by id, its `/entries`, and `finalized-scores` — blinding only the list
would have been theatre, since the ids are in the DOM of any screen that ever showed it. HR is never
blinded, because HR files on a panelist's behalf and must see what is already recorded. The HR desk's
Scores tab now says the view is narrowed instead of claiming *"The panel has not scored this
candidate"*, which for a blinded panelist may be flatly false.

**⚠ Defect 28 — found while verifying, pre-existing, not lane F's.**
`JobApplicationRepository.GetWithFullDetailsAsync` loads **eight collection navigations** under EF's
default `SingleQuery`, so they are LEFT JOINed into one result set whose row count is their product.
On `ErpSystemDB_UAT` every `POST /api/job-applications` returned **500 after exactly 30,287ms** — the
command timeout — on the post-create re-read, *with the row already written*. Live session
inspection settled it: `status=running`, **no wait type, no blocker**; pure query-processor CPU, not
contention. EF logs `MultipleCollectionIncludeWarning` on the line above the failure and names the
fix. `.AsSplitQuery()` applied there and to `GetAllWithFullDetailsByVacancyIdAsync`, which is worse —
it is the **scoring engine's** read, so the product is multiplied again by the vacancy's application
count, and a timeout there would read as *"shortlisting is broken"* rather than as a slow query.
Already house practice in 15 repository files, one of which records the same lesson: *"four
collection includes is the 8060-byte single-query shape that 500'd"*.

**⚠ Defect 29 — `JobCandidate.FullName` printed a double space.** It was
`$"{FirstName} {MiddleName ?? ""} {LastName}".Trim()`, and `.Trim()` strips the *ends*, not the gap
left in the middle — so every candidate without a middle name rendered as *"Yaaba&#160;&#160;Nkrumah"*
on every screen, and on the printed scoring sheet a panel signs. `Employee.FullName` has always
branched on the middle name correctly; `JobCandidate` was the odd one out of the five `FullName`
definitions in the entity layer, and now matches.

Found by the lane F harness at 99/101 on its first pass. It also exposed a **weak test**: the
harness's `has()` collapsed whitespace in the haystack but not in the needle, so the comparison
could only ever pass while the data happened to be clean. Fixed at the helper, not at the call site.

⚠ **Fixing the entity alone was not enough, and the neighbour sweep is what proved it.**
`JobCandidateDto.FullName` carried a **second copy** of the same expression, so the paged
application read (which goes through the entity) and the candidate read (which goes through the DTO)
then disagreed about the same person's name — which is precisely what `slice-b` compares. A sweep
for the pattern found a third: `PhysicianDto.FullName` in Medical, worse again at
`{Title} {First} {Middle} {Last}`, leaving *two* gaps for a physician with neither. All three are
fixed and the pattern now returns zero matches across the solution.

The other first-pass failure was in the harness, not the product, and is worth recording because it
is the exact confusion the column rename exists to prevent: the assertion compared
`filedByHrOnBehalfOfEmployeeId` against the **user** id. It now asserts both directions — equal to
the employee id, *and not* equal to the user id — so a future change that swaps them cannot pass
quietly.

**Two wrong diagnoses on the way, recorded because the reasoning is the reusable part.** I first read
the log as showing the notification backlog had drained — it had not; the log was quiet *between*
30-second cycles. I then blamed the backlog for starving the query, narrowed the dispatcher with
`Notifications__PropertyEnquiriesOnly=true`, got zero email failures, and watched the timeout
reproduce identically. Neither guess survived contact with `sys.dm_exec_requests`. **Look at what the
session is waiting on before theorising about what else is running.**

**Environment notes.** ⚠ Staging has no user secrets, so `JwtSettings__SecretKey` must be passed in
or every login 400s — documented in `../operations/HR-VERIFICATION-HARNESS-GUIDE.md` §2.1, and hit
anyway by extracting the key from `appsettings.json`, where it is an empty string. Separately, UAT
carries **930 notifications still in the retry cycle** with no SMTP configured, cycling every 30
seconds: noisy in the log, harmless to correctness, and not the cause of anything above.

---

### Lane G — offer defaults and currency · BUILT 2026-09-22, verification in progress

Harness: `dev-harness/hr-recruitment/run-round4-g.mjs`. Migration:
`20260922084439_AddOfferValidityDays` (guarded SQL; applied to `ErpSystemDB_UAT`).

**Built as planned** — G1 the defaults endpoint, G2 the form seeded from it with the source of
every value shown, G3 the currency picker and server-side validation, and D-10 `OfferValidityDays`.

**Changed during the build, with reasons:**

| | Planned | Built | Why |
|---|---|---|---|
| Annual leave from "the entitlement for the grade/employment type" | a grade-keyed rule | **the annual leave type’s standard days** | There is no entitlement keyed on grade or employment type in this system. Inventing one would put a number on the offer letter that nothing downstream honours. The source line says which leave type it came from, rather than implying a grade rule that does not exist. |
| `NdaRequired`/`IsConditional` from the position | both | **`IsConditional` only, derived** | The position has neither flag. `IsConditional` is derived from the post requiring a licence, certification or guarantor — an offer conditional on producing them — and the source line explains why the box arrived ticked. `NdaRequired` has no source at all, so it is left alone rather than guessed. |
| Currency validation on "create, update and add-benefit" | three paths | **four** | `UpdateBenefitAsync` takes a currency too. Three guarded paths and one unguarded one is not three quarters of a control; it is a control with a door next to it. On that path the check runs BEFORE the assignment, because the entity is tracked. |
| — | not planned | **`Sources` omits unresolved values entirely** | A caption under an empty box is the same fault as an empty criterion scoring full marks — an unanswerable question dressed as an answer. Unresolved fields are named in `Unresolved` instead, and the form lists them as *"You will need to supply these"*. |

**⚠ Defect 31 — publishing a vacancy through the API erased its employment type and work mode.**
`TransitionJobVacancyDto.EmploymentType` and `WorkMode` were non-nullable **with no initialiser**,
unlike `CreateJobVacancyDto` which defaults them. A transition payload that did not mention them
bound to `default` = **0** — a value outside BOTH enums, since `EmploymentType.Permanent` is 1 and
`WorkMode.OnSite` is 1 — and the mapper wrote it unconditionally. `JobOfferService.CreateAsync`
then copies `vacancy.EmploymentType` onto every offer raised from that vacancy, where it drives
weekly hours and the contract-duration prompt.

Both fields are now nullable on the transition payload and applied only when supplied, so *"not
mentioned"* stays distinguishable from *"set to zero"*. The UI never hit this because it posts a
full update payload; a harness sending only what it meant to change did.

⚠ **The weekly-hours VALUE was right — 40 — so a value-only assertion would have passed.** What
failed was the source line: *"Standard hours for a **0** contract"*. That is the argument for
making a defaults endpoint state its provenance rather than just its numbers: the provenance is
checkable, and it caught a data-corruption bug two lanes away from anything this lane touched.

---

### Lane H — the pre-employment checklist in the offer letter · BUILT 2026-09-22

Harness: `dev-harness/hr-recruitment/run-round4-h.mjs`. Migration:
`20260922094205_AddPositionPreEmploymentCheckTemplate` (guarded SQL; applied to `ErpSystemDB_UAT`).

**Built as planned** — H1 seeds the check set at offer creation from the post’s template, H2 deletes
the five invented conditions and renders the real items with what the candidate must produce, H3
renders the list for every offer under a heading that changes with `IsConditional`, H4 updates the
shipped `OfferLetter` body.

**⚠ The assertion this lane turns on is a NEGATIVE one.** The letter used to print five conditions
whenever no check set existed — *"Satisfactory employment references"*, *"Verification of stated
qualifications"*, *"Confirmation of the right to work"*, *"Satisfactory background / criminal-record
check"*, *"Medical fitness assessment"* — naming things the system does not track, cannot chase and
will never mark complete. A candidate was told the offer depended on five requirements that existed
nowhere but in that paragraph. The harness asserts all five ABSENT from the non-conditional letter,
the conditional letter, and the no-check-set case, because a letter printing real items looks
identical whether or not the fallback still lurks until you go looking for what it used to say.

**Changed during the build, with reasons:**

| | Planned | Built | Why |
|---|---|---|---|
| "falling back to the single active default" | a default template | **literally the single active one** | `PreEmploymentCheckTemplate` has no `IsDefault`, only `IsActive`. With several active it seeds NOTHING rather than picking: an offer letter commits the company, in writing, to what a candidate must produce, and HR would only discover a guess after the letter was sent. |
| — | not planned | **a post pointing at a retired template falls through to the tenant rule** | Rather than silently seeding nothing, which would look identical to "this post needs no checks". |
| — | not planned | **the harness creates its own position** | ⚠ The position update payload is a replace-set: skills, benefits and certifications are collections, so a PUT that omits them deletes every row. Hanging one field on a demo position would have stripped it. |

**⚠ Defect 32 — self-inflicted, and of a shape this plan already names.** `PreEmploymentCheckTemplateName`
was added to `EmployeePositionDto` and the mapper read `position.PreEmploymentCheckTemplate?.Name`,
but the navigation was in none of the repository’s seven reads — so the field was **declared and
never populated**. That is exactly § 3 defect 15 (`OrientationAudienceRuleDto.TargetEntityName`,
*"declared and never mapped, so the rules list can never show a name"*), reproduced while the plan
describing it was open. Caught at 35/36 on the first pass. The `Include` went onto **all seven**
reads, not the one the harness touched: a field that resolves on the detail read and returns null
on the list read is the same defect wearing a hat.

**A correction to the template work.** The shipped letter block first used `{{#if IsNotConditional}}`,
a token that does not exist. Reading `EmailTemplateRenderer` showed the engine supports nesting AND
`{{else}}`, so the block uses `{{else}}` and needs no new token. `{{ConditionsList}}` keeps its old
meaning — conditional offers only — so a template a client has already reworded goes on behaving as
it did.

**Parked blocker #1 is cleared.** `AcceptConditionallyAsync` refuses an offer with no check set,
correctly, because the condition in *"conditionally accepted"* IS the check set. Seeding one at
creation is what stops the refusal being a dead end; the harness proves a conditional acceptance now
succeeds with nobody hand-building anything.

---

### Neighbouring suites after lane H — and three things they said

Recruitment, all at baseline: `run-round4-f` 103, `-g` 41, `-a` 45, `-c` 46, `-h` 36 ×2; `run-k` 81,
`run-v` 72, `run-a` 44, `run-c1` 96, `run-c2` 89, `run-candidate-country` 43; `slice-b` 174/176,
`slice-c` 188/190, `slice-e` 101/105, `slice-f` 69/69; `run-lane5b` 32/34.

`hr-jobarch` was run too, because lane H’s fix for defect 32 put an `Include` on **all seven**
position reads and that is the suite which exercises them. Twenty of its runners pass with zero
failures, including the heavy ones (`slice13` 229, `slice14` 114, `slice15` 83).

**⚠ A stale assertion that INVERTED.** `run-r7` failed on
`ok(procurementDoor !== 200, "Procurement’s own supplier list is not usable")` — an assertion that
**pinned cross-module defect #1**, Procurement’s `SuppliersController` failing to activate.
Procurement repaired it; `GET /api/Suppliers?page=1&pageSize=1` now answers **200**, verified
directly with an admin token. So the suite reported a regression on the day somebody fixed
something.

That is the failure mode of encoding another team’s bug as your own expectation: it inverts the
moment they repair it, and the signal arrives labelled backwards. The assertion now asserts 200
(`run-r7` 42/0) and defect #1 is marked **RESOLVED 2026-09-22** in
`../integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`, with the original account kept as the
evidence the fix was needed. **Assert what should be true; record what is broken in the register.**

**⚠ Seven `hr-jobarch` runners CRASH, for at least two distinct causes, and are NOT chased here.**

| Runner(s) | Symptom | What is known |
|---|---|---|
| `run-slice0`, and probably its siblings | **409** — *"This tenant approves job descriptions through the workflow engine. Approve it from the workflow queue instead."* | The `Job Description Approval` definition was published **2026-09-20 16:00**, two days before lane H. The suite calls the direct-approve endpoint the product now correctly refuses. The product is right; the suite predates the wiring. |
| `run-r3` | `TypeError: Cannot read properties of undefined (reading ‘id’)` | A fixture assumption, a different cause entirely. Not diagnosed. |
| `run-r4b`, `run-slice1`, `-2`, `-3`, `-9` | crash | Cause not established individually. |

⚠ **Honest limit on this claim:** these were not run BEFORE lane H, so it cannot be said by
measurement that they predate it. What can be said is that lane H’s diff touches no `JobAnalysis`,
`JobDescription` or `Workflow` file — the twelve changed files are offers, positions, the letter and
docs — and that the 409’s cause is dated two days earlier. Recorded rather than assumed away.

**Owed:** route those seven through the workflow queue, or retire the direct-approve path from
them. That is a `hr-jobarch` job, not a round 4 lane.

### Lane B — the talent pool, screened and acted on · DONE 2026-09-22 · 95 assertions ×2

Harness: `dev-harness/hr-recruitment/run-round4-b.mjs`. **No migration** — the only
schema-adjacent change is `ApplicationSource.TalentPool = 12`, a new member of an enum stored as an
int. Verified on `ErpSystemDB_UAT`.

**Built as planned** — `POST talent-pool/screen/{vacancyId}` and `POST talent-pool/screen`, the five
unwired filters plus a geography-subtree location filter, `invite-to-apply` and `book-interview`,
the criteria score beside the fit score, and the candidate photograph on the application screens.

**The structural move, which is the lane's real content.** B1 says the screen must run "the **same**
`EvaluateCriterion`". That was only achievable by making it structurally true:

| Moved | From | To |
|---|---|---|
| `ScoringCandidateView`, `EvaluateCriterion` and their nine helpers | private members of `JobApplicationService` | `Services/HR/Recruitment/ShortlistingEvaluator.cs` |
| the aggregation loop — evaluate-all, a mandatory miss scores 0, nothing measurable scores **null** | inline in `EvaluateLoadedApplicationScoreAsync` | `ShortlistingEvaluator.Score` |
| `ResolveCriterionValuesAsync`, `MirrorLabels`, `RequireScorableCriterion` | private members of `JobVacancyService` | `Services/HR/Recruitment/ShortlistingCriteriaResolver.cs` |

Every rule, comment and lane A repair moved verbatim. The harness asserts the claim directly: the
same pool member is given an application against the same vacancy, scored through
`JobApplicationService`, and the two numbers must be equal. That single assertion is what would
catch the two paths drifting; the other 94 would not.

The resolver move is what makes the ad-hoc screen refuse what a vacancy refuses — a mandatory
Gender, a numeric criterion with no bound, a list criterion with no values, an area off the tree.
Four assertions, one per refusal, all 422.

**Changed during the build, with reasons:**

| | Planned | Built | Why |
|---|---|---|---|
| B5 | "No backend work" | **`CandidateHasPhoto` added to three read projections** | `GatedPhoto` takes an `enabled` flag precisely so a list does not fire one request per row that will 404. Without the flag a 30-row pipeline board issues 30 doomed requests. The pipeline board is a *different* projection from the application DTOs, so the flag had to go on all three or the board falls back to initials for everybody. |
| — | not planned | **the professional profile becomes HR-writable** | `TotalYearsExperience`, `PreferredWorkArrangement`, `AvailableFrom`, `Headline` and three siblings were writable **only through the candidate's own portal profile** — while the pool's match rubric scores on three of them and the pool list has a column for two. A career fair, a referral and an unsolicited CV all reach the pool by HR typing them in, so **the people HR knew most about could never rank above the "nothing on file" tier**, and there was no box to put it in. This is G-13.2 seen from the other side: the rubric was corrected so silence stops scoring like a match, and then the only people who could break the silence were the candidates. Added to HR's create/update DTOs, both mappers and the candidate form. Expected salary and work-authorization status stay candidate-only — the DTOs do not carry them and an input would discard what was typed. |
| screen with no criteria | not specified | **refused, 422** | The alternative is a full table of "Not measurable" against every pool member, which reads as *"the pool is useless"* rather than *"this vacancy has not said what it wants yet"*. The refusal names the tab that fixes it. |
| `invite-to-apply` on a closed vacancy | not specified | **refused for the whole request, 422** | `CreateAsync` deliberately does not check vacancy status — it is the door a late walk-in is recorded through, and that is a judgement call. An **invitation** is not: writing to somebody asking them to apply for a filled post is the organisation embarrassing itself in the candidate's inbox. A property of the vacancy, so the request fails rather than each row. |
| `book-interview` for a candidate who has not applied | "requires an application" | **skipped per row, with a reason naming the button above it** | Decision Q2. Manufacturing an application would lose the record of who decided this person should be considered. |
| criteria score `null` | not specified | **rendered as "Not measurable", never as 0** | Three states, not two: 0 is *measured and missed everything*; null is *the criteria asked questions this record cannot answer*. A `?? 0` anywhere in the chain puts an unknown beside a demonstrated miss. Carried through the DTO, the TS type, both panels and the ranking, which sorts null last. |

**Two product findings, recorded not fixed:**

1. **`TransitionJobVacancyDto` clears `CustomAdvertTitle` on a partial payload.** The DTO mirrors
   the update payload — it is a full replace — and its own doc comment already records this trap for
   `EmploymentType` and `WorkMode`, **which were made nullable so that an omitted value means "leave
   it"**. `CustomAdvertTitle` is `string?` where null genuinely means "clear it", so a caller has no
   way to say "leave it" and publishing a vacancy without restating the title erases it. The vacancy
   then has no title at all, and the invitation email and engagement-event subject read
   *"Invited to apply:  (VAC-000131)"* — a blank where the role should be. The UI posts a full
   payload, so it never shows there. **This is the same defect one field over, left behind by the fix
   that named it.** Found because lane B is the first suite to assert on a vacancy's title; lane A's
   fixture has been silently clearing it too. Not fixed here — making null mean "leave it" removes
   the ability to clear a title, which is a contract change other suites depend on.

2. **Parked blocker 1 survived lane H.** `run-g` still dies on `accept-conditionally`, and the cause
   is now exact: *"A conditional acceptance needs the checks it is conditional on."* Lane H seeds the
   check set at offer creation **from the position's check template**, so a position with no template
   still produces an offer that cannot be conditionally accepted. Whether that is the intended design
   or a gap is lane H's call. `slice-d` aborts at 75/98 on the same wall, and `run.mjs` is still on
   blocker 2 (the Staff Requisition workflow not routing multi-step) — unchanged, lane D's
   neighbourhood.

**A harness defect, and it is § 9.1's trap arriving by a different road.**

`setup.mjs` — shared by **every** suite in this folder — asked for
`/api/Workflow/definitions?pageSize=500`. The listing caps `pageSize` at **100** server-side and
pages on **`page`**, not `pageNumber` (which is silently ignored and returns page 1 again). This
tenant now carries **127** definitions, and `Staff Requisition Approval` sits at row 101. So setup
read the first 100, found nothing, and printed *"expected exactly one active StaffRequisition
definition, found 0 — run publish-requisition-definition.mjs"*.

That is the **same message and the same wrong remedy** as § 9.1, from a different cause: publishing a
second definition would have left the tenant with two active ones and the engine's choice between
them unspecified. § 9.1's fix normalised the entity-type key; **a truncated read defeats any amount
of normalising.** Setup now walks every page against `metadata.totalCount`, and the failure message
carries *how many definitions it actually read* — without that number a missing definition and a
truncated listing are indistinguishable, and only one of them has that remedy.

**Three harness assertions were wrong, and saying which matters:**

- six refusals expected **400**; the area's `[RecruitmentBusinessRules]` filter maps
  `InvalidOperationException` to **422** throughout, which lane A already asserted for its country
  clash. My expectation, not the product's contract.
- `alreadyApplied === false` was asserted on the candidate the B1 cross-check had just given an
  application to. True is correct; the assertion was testing the fixture's memory.
- the pipeline auto-advance was asserted on a fresh application. `TDC Standard Recruitment` marks its
  stages **non-skippable**, so `MoveApplicationToStageAsync` throws *"Stage 'Application Review'
  cannot be skipped"*, `AutoAdvanceToStageTypeAsync` swallows it into a warning, and the booking
  still succeeds. ⚠ **That swallow is right** — a candidate booked into a session should not be
  un-booked because the pipeline has an opinion about ordering — but it makes the advance
  **best effort**, and asserting the landing without walking the earlier stages asserts something the
  product never promised. The fixture now attaches the default pipeline, walks the application
  through every stage before Interview, re-books, and asserts the landing **by name**.

**Neighbouring suites — all sixteen at baseline.** The engine moved, so everything that scores an
application is a regression candidate; `run-k` and `slice-e` are the two that exercise it hardest.

| Suite | Result | Baseline | |
|---|---|---|---|
| `run-round4-a` · `-c` · `-f` · `-g` · `-h` | 45 · 46 · 103 · 41 · 36 | same | ✅ |
| `run-k` (scoring) · `run-v` (segments) | **81/81** · **72/72** | same | ✅ the two that exercise the moved engine |
| `run-a` · `run-c1` · `run-c2` · `run-candidate-country` | 44 · 96 · 89 · 43 | same | ✅ |
| `slice-b` · `slice-c` · `slice-e` · `slice-f` | 174/176 · 188/190 · 101/105 · 69/69 | same | ✅ the § 9.2 stale assertions |
| `run-lane5b` | 32/34 | 32/34 | ✅ the recorded stale admin-gate |

**Frontend:** type-checked against a scoped `tsconfig` over the 13 touched files (the project-wide
`tsc` still crashes — HR-CLOSURE-LEDGER 2026-08-30). Four real errors found and fixed:
`TextField`'s `type` union is `text|email|tel` and will not take `number` (`NumberField` exists);
the interviews service exports `jobInterviewService`, not `interviewsService`.

**Not walked in a browser.** The Screen tab, the ad-hoc criteria builder and the two bulk actions
have no browser walk — the same standing gap this module carries elsewhere.

---

### Lane D-1 — the clash check made real · DONE 2026-09-22 · 58 assertions ×2

Harness: `dev-harness/hr-recruitment/run-round4-d.mjs`. Migration:
`20260922122130_AddInterviewPanelClashOverrideAndRoomBooking` (guarded SQL; applied to
`ErpSystemDB_UAT`). Covers **D1, D2, D3, D4 and D8**; D5–D7 are lane D-2.

**Built as planned.** `IPanelistCommitmentSource` with seven registered implementations, the check
made binding on all three write paths with a recorded override, `GET suggest-slots`, and
`JobInterview.RoomBookingId`.

**The four new sources are the point.** The old check knew about other interviews, leave and travel.
It now also reads **meetings the panelist PARTICIPATES in** — the company-schedule module's own
`HasConflictingEventAsync` checks the *organizer* only, and a board meeting has one organiser and
twelve attendees, every one of whom read as free — **room bookings**, **training nominations**, and
**closures + public holidays**.

**Hard vs soft is the whole design.** Hard means confirmed **and** time-precise: another interview, a
Confirmed room booking, a Confirmed meeting this person accepted. Those refuse. Everything else
warns, and the reason is not squeamishness — leave and travel are recorded by the **DAY** and cannot
answer "is the 09:00 hour free?". A check that refused on them would have the system overruling
somebody about their own time on evidence that does not reach the question. The harness's most
important assertion is therefore a **negative** one: *"a SOFT clash does NOT refuse the schedule"*.
It is easy to write a check that blocks everything and call it strict.

**Changed during the build, with reasons:**

| | Planned | Built | Why |
|---|---|---|---|
| the availability DTO | three typed lists (`interviewConflicts`, `leaveConflicts`, `travelConflicts`) | **one `commitments` list** | Seven sources now and more later. A named list per source means editing the DTO, the TS type and every screen each time the organisation learns to track something else. `slice-c` asserts only the row count and the 403, so nothing moved. |
| — | not planned | **`sourcesConsulted` on the result** | A source that is written and never registered in DI contributes nothing, and the check then answers **"free"** — the exact failure the interface exists to stop, reappearing as a DI omission. It is invisible unless the result says which sources answered. The harness asserts the count **and** the seven names; that assertion is the only thing standing between a forgotten `AddScoped` and a silent wrong answer. |
| — | not planned | **a row for every panelist, including the clear ones** | The external half used to emit a row only where there was a conflict, so a panel of three externals with one clash rendered as one row. "Checked and clear" and "not checked" must not look the same. |
| — | not planned | **one interview per room booking** | D8 gives an interview a booking to hold. Two interviews pointing at the same hold would be the double-booking the hold exists to prevent, arriving from inside recruitment. Refused, naming the interview that already holds it. |
| a hard clash refuses | — | **and a reason given where there is NO clash is discarded** | A record saying somebody overrode a clash that never existed is worse than no record. Asserted. |

**A defect of my own, caught by the harness asserting the right thing.** The interview mapper reads
`RoomBooking?.Room?.RoomName`, and I never added the `Include` — so `roomName` and
`roomBookingNumber` came back **null on every read** while `roomBookingId` was set. That is the
"declared and populated by nothing" shape this round keeps recording, produced while writing the
lane that records it. It survived only because the first draft of the assertion checked the id;
asserting the **name** found it immediately. Fixed by including `RoomBooking.Room` on
`WithSummaryNavigations`.

**What the harness got wrong, and what each mistake was:**

- **The reschedule test moved the wrong interview.** It moved a session whose panel was B onto a
  window occupied by A, and the create succeeded — correctly, B was free. *"A reschedule onto an
  occupied window is refused"* is only true of a window occupied **for that panel**, and the fixture
  has to arrange that. The first version asserted the product was broken when it was right.
- **Event creation forces its own state.** `CompanyScheduleService` sets `Status = Scheduled` and
  `InvitationStatus = Sent` whatever the payload says. The fixture set `Confirmed`/`Accepted` in the
  create body and then asserted a hard clash — asserting its own hopes. It now goes through
  `approve` and `participants/respond`, which is also what proves the hardness rule reads live state
  rather than what was posted.
- **Several leave types require a reliever**, and the create is refused. A real product rule, so the
  fixture picks a type it does not apply to rather than working around it by naming an arbitrary
  reliever.
- **The room fixture was not repeatable.** It booked a fixed window on the tenant's first bookable
  room; the second run hit *"There is a conflicting booking for this time slot"* — the room module's
  blocking check doing its job on the first run's leftovers. The fixture now creates a room of its
  own per run, which also removes the dependence on the tenant having a bookable room at all.

⚠ The assertion count went **48 → 58** on that last fix, because the failing room booking had been
short-circuiting the whole D8 block. *Fewer assertions with zero failures is a regression signal* —
and so is a suite that reports 48 when it should report 58.

**`run-round4-c` broke, correctly, and was repaired rather than overridden.** Lane C creates several
interviews on one day with the same panelist. That was harmless while the check was advisory; D3
made it bind, so the second session was refused and the suite died. **The product is right and the
suite predates the rule.** Each session now gets its own day — deliberately **not** an override
reason, which would have worked but would have meant lane C could no longer notice if the clash
check broke, and would have written a false *"somebody decided to double-book"* record onto fixtures
that are really about slot arithmetic. Back to 46/46.

**Neighbouring suites — all at baseline.**

| Suite | Result | Baseline | |
|---|---|---|---|
| `run-round4-a` · `-b` · `-c` · `-f` · `-g` · `-h` | 45 · **96** · 46 · 103 · 41 · 36 | same (b +1, see below) | ✅ |
| `run-k` · `run-v` | 81 · 72 | same | ✅ |
| `run-a` · `run-c1` · `run-c2` · `run-candidate-country` | 44 · 96 · 89 · 43 | same | ✅ |
| `slice-b` · `slice-c` · `slice-e` · `slice-f` | 174/176 · 188/190 · **101/105** · 69/69 | same | ✅ |
| `run-lane5b` | 32/34 | 32/34 | ✅ recorded stale admin-gate |

**⚠ `slice-e` came back 99/105 — two below baseline — and it was MY fixture litter, not lane D.**

`run-round4-b` mints four talent-pool members per run and left every one of them in the pool. Six
runs, twenty-four squatters, all with experience on file — and `slice-e` asks the vacancy-match
endpoint for its top 50 and looks for its own candidate in the answer. It was pushed out.

slice-e's assertion is **not** at fault: it already asks for 50 and searches by id. The fault is a
suite that creates pool members and does not take them out again. `run-round4-b` now removes its
four at the end (96 assertions, up from 95), the twenty-four already left behind were removed
through the real door, and slice-e is back to 101/105 exactly.

⚠ The candidates themselves are kept — they carry applications, engagement events and an interview
booking, and deleting them would delete the evidence the suite just created. What must not persist
is the **pool membership**, which is what every pool read ranks. ⚠ The removal needs an
**employee-linked** user: `admin` is not one, and the pool doors answer *"Tenant context could not be
resolved"* for it.

**Not walked in a browser:** the rewritten availability panel and the "when is everyone free?"
suggestions.

---

### Lane D-2 — the organizer, the notifications, the four defects · DONE 2026-09-22 · 39 assertions ×2

Harness: `dev-harness/hr-company-schedule/run-round4-d.mjs`. Migration:
`20260922132728_AddCompanyEventOriginalWindowAndUniqueNumbers` (guarded SQL; applied to
`ErpSystemDB_UAT`). Covers **D5, D6 and D7**, completing lane D.

**D7 — the four defects this lane made load-bearing.**

| | What was wrong | What it is now |
|---|---|---|
| **C-6** | Three generators issued `COUNT(*) + 1` over **live** rows. Soft-deleted rows are excluded from that count, so deleting an event FREED its number and the next create took it — and the indexes were not unique, so nothing complained and two rows quietly shared a reference. | Issuing moved into the repositories on the shared sequence, which probes with `IgnoreQueryFilters` so a deleted row still holds its number. The three indexes are UNIQUE per tenant. |
| **C-2** | `RescheduleEventAsync` set `RescheduledDate = DateTime.UtcNow` — *when somebody pressed the button*, not what the event moved from — then overwrote `StartDate`/`EndDate`. Nothing remembered the original, while the dialog told the user it was kept. | Four nullable columns hold it, set on the **first** move only. |
| **C-4** | `MaxBookingDurationHours`, `AdvanceBookingDays` and `Capacity` were settable on the room's admin screen and read by **nothing**. A room configured "2 hours maximum, seats 8" could be booked for a day, for forty people. | Enforced on create **and** edit. |
| **C-1** | Every Delete is gated on `HR.Company.Admin`; the HR role holds Read, Write and Approve and **not** Admin — verified against UAT. The button was rendered, in destructive red, for the people it refuses. | The **button is hidden**; the endpoint is unchanged. |

⚠ **C-1 was fixed by hiding the button, not by loosening the policy.** Whether HR may delete a
company event is a permission decision for TDC to make in role setup; the defect is that the screen
offered what it could not do. The harness asserts the 403 is *still* there, which is the assertion
that records the choice.

**⚠ How C-6 is tested, because the obvious test proves nothing.** A repeating number generator is
invisible until it repeats. Asserting that two fresh creates differ passes under the old code too.
The suite has to MAKE it repeat: create, **delete**, create again, and assert the third did not take
the dead row's number. The deletes go through `admin`, because the HR actor cannot delete — which is
C-1, asserted three sections later.

**D6 — the module told nobody.** It could invite, take an RSVP, reschedule and cancel, and sent
nothing: `EventParticipant.InvitationSentDate` was stamped by the participant-create and meant
nothing, so an event with an RSVP deadline was a deadline the invitee had never heard of. A
`CompanySchedule` catalogue with five templates now backs real sends on invite, reschedule and
cancel. The reschedule notice carries **what it moved from**, which is only possible because C-2
keeps the original.

⚠ **The RSVP chase and the reminder are ENDPOINTS, not sweeps**, and that is a deliberate scoping
call. All fourteen existing HR scheduled sweeps log intent into a dispatch table and deliver
nothing; making these the fifteenth would have added to that pile rather than to the product. **Lane
K owns** turning sweeps into things that actually send, and when it lands these are the methods it
calls. Until then the organiser chases from the event screen, which is where they already are when
they notice. The harness asserts the filters that make them worth having: a chase stops once
somebody answers, and a reminder never reaches somebody who declined — *ignoring the answer it asked
for* is the failure mode a naive loop produces.

**D5 — the diary, and why D1's interface earned its keep.**

"What is this person committed to?" is the same question the clash check asks, over a fortnight
instead of an hour. So `/my-schedule` and `/team-schedule` fan out over the **same seven registered
commitment sources** rather than re-reading six modules. A diary written separately would have
started identical and drifted, and the day somebody adds an eighth kind of commitment only one of
them would learn about it.

That required the sources to honour a date **range** — they filtered on a single day, so a
fortnight's diary would have shown the first day's entries. Six of the seven changed. The clash check
passes a same-day range and is unaffected: `run-round4-d` at **58/58** is what proves it, and that
assertion is the whole reason the recruitment suite is in the neighbour list for a company-schedule
lane.

⚠ `my-schedule` takes the employee **from the token**, never a query parameter. One there would let
any signed-in user read a colleague's leave and travel — exactly what the panel-availability
endpoint is HR-gated to prevent. Reading somebody else's goes through `team-schedule`, which is
gated on Company **Write** rather than Read for the same reason, and the harness asserts an ordinary
employee gets their own diary and a 403 on the team's.

**Two defects of my own, both caught by the harness:**

1. **`UnitSubtreeAsync` returns UNIT ids, not employee ids** — its own remarks say so, because the
   staff directory wanted the walk as a SQL predicate rather than a resolved employee set. I read
   them as employee ids, so nothing matched and the team read answered **"0 members" for a populated
   directorate**. Silently: no error, an empty list. Fixed to match on `OrganizationUnitId`, and
   filtered to active employees to match the resolver's own population — a leaver should not appear
   in next week's team diary.
2. **The `hh\:mm` escape, for the second time in one lane.** A `TimeSpan` format needs `hh\:mm`, and
   `\:` only parses inside a **verbatim** interpolated string. `DateTime` needs no escape at all,
   which is why identical-looking code elsewhere compiles. Recorded here because repeating it inside
   one lane means reading the D-1 note was not enough.

**⚠ A harness defect that had broken the whole area, silently.** `hr-company-schedule/setup.mjs`
still sent `employeeNumber`, which the register has refused since 2026-09-10 (*"Staff numbers for
permanent staff are issued by the system"*). It is the **shared** setup, so all four company-schedule
slices had been failing at their first fixture since then — the same repair `hr-recruitment`,
`hr-employee-docs` and `hr-payroll-membership` each had to make. Removing it revived them:

| Suite | Result |
|---|---|
| `run-round4-d` (new) | **39/39 ×2** |
| `run-slice0` · `-1` · `-2` · `-3` | **24 · 32 · 62 · 44**, all zero failures — first green since 2026-09-10 |

**Neighbouring suites — recruitment, all at baseline** (the range refactor touched the shared
sources, so the whole recruitment set is a neighbour here):

| Suite | Result | Baseline | |
|---|---|---|---|
| `run-round4-d` | **58/58** | 58 | ✅ the range refactor did not move the clash check |
| `run-round4-a` · `-b` · `-c` · `-f` · `-g` · `-h` | 45 · 96 · 46 · 103 · 41 · 36 | same | ✅ |
| `run-k` · `run-v` · `run-a` · `run-c1` · `run-c2` · `run-candidate-country` | 81 · 72 · 44 · 96 · 89 · 43 | same | ✅ |
| `slice-b` · `slice-c` · `slice-e` · `slice-f` | 174/176 · 188/190 · 101/105 · 69/69 | same | ✅ |
| `run-lane5b` | 32/34 | 32/34 | ✅ recorded stale admin-gate |

**On the migration, and a question worth recording.** The first draft **renumbered** duplicate
references automatically so the unique index could be built. It was idempotent and it was the wrong
trade: these references are printed on agendas and quoted in emails, and forty lines of untested SQL
whose failure mode is *corrupting* them is a poor risk for a case that does not currently exist —
UAT has zero duplicates, measured. It now **refuses** with the table, the column and the shared
values, and says the decision is the operator's. Compare the two failures: SQL Server's own names the
index and not the cause.

**Not walked in a browser:** `my-schedule`, `team`, and the hidden Delete buttons.

---

---

### Lane E-a — the recruitment test engine · DONE 2026-09-22 · 141 assertions ×2

Harness: `dev-harness/hr-recruitment/run-round4-e.mjs`, blocks A–O. **Not** a new `hr-tests/` as
this plan first said: a sitting hangs off an application, which hangs off a candidate and a
published vacancy — slice-F's whole fixture. A fresh directory would have rebuilt that plumbing to
test a feature whose own endpoints are `api/hr/recruitment/tests`.

**Neighbouring suites, all at baseline:** `run-round4-a` 45 · `-b` 96 · `-c` 46 · `-d` 58 · `-f`
103 · `-g` 41 · `-h` 36 · `run-a` 44 · `run-c1` 96 · `run-c2` 89 · `run-k` 81 · `run-v` 72 ·
`run-candidate-country` 43 · `slice-b` 174/176 · `slice-c` 188/190 · `slice-e` 101/105 · `slice-f`
69 · `run-lane5b` 32/34 (§ 9.2's stale admin-gate) · `hr-company-schedule/run-round4-d` 39.

Migration `20260922152618_AddRecruitmentTestEngine` (guarded SQL; applied to `ErpSystemDB_UAT`,
all 7 tables and 23 indexes verified present). No frontend route has been browser-walked; **E4's
walk is already listed in § 5** as one of the three things a harness cannot prove.

**What was built (E1–E5).** Seven entities; `RecruitmentTestService` + `IRecruitmentTestService`;
`RecruitmentTestController` (`api/hr/recruitment/tests`, `InternalOnly` + `HR.Recruitment.*`) and
five candidate actions on `CandidateController`; authoring, assignment, delivery, auto-marking,
manual marking and finalisation; six screens.

**Three decisions taken inside the lane, each reversible and each worth a second opinion.**

| | Decision | Why, and what the alternative cost |
|---|---|---|
| 1 | A paper the machine can settle outright **finalises itself at submit** — ledger row written, application re-scored, result released. A paper with written answers waits for a human and shows the candidate nothing. | This is what finally gives `TestScoreWeight` an input (§ 3 defect 8) without a second click. The alternative leaves every aptitude test in a queue waiting for a human to press a button that has nothing to decide. |
| 2 | Past the deadline the **late payload is ignored** and the sitting is marked on what was *saved* before it, status `Expired`, with a 2-minute grace for clock skew. It does not refuse. | Refusing loses work the candidate did in time; accepting late answers makes the clock decorative. Requires server-side autosave, which is why progress is stored rather than kept in the tab. |
| 3 | The sitting's access token **binds one live window per attempt** — reopening re-issues it and the stale tab's autosave is refused. | Without it a second tab's twenty-minute-old answers silently overwrite the fresh ones. It also gives `AccessTokenHash`/`Last4`/`ExpiresAt` a job; the alternative was three columns nothing populated, which is the defect shape this round keeps finding. |

**⚠ Deviation from the plan, for the record.** The plan said to reuse `AssessmentPending` for the
invitation. A new `TestInvitation` template was added instead: `AssessmentPending` is the *pipeline
stage* notice — it carries no test name, duration, deadline or attempt count and points at the
dashboard, so a candidate would open a timed paper on a phone with ten minutes to spare. Both are
kept.

**§ 9.4's token recommendation is now half taken.** The sitting token is SHA-256 hashed at rest
with only the last four characters in clear, following procurement's design as § 9.4 asked.
**The offer-response and interview-confirmation tokens still store in clear** — unchanged, still
owed.

#### What verifying it found — two defects in the lane's own code

| | Defect | How it was found | Fix |
|---|---|---|---|
| 1 | **A re-sit was AVERAGED with the attempt it replaced.** The ledger row was keyed off the *sitting*, so attempt 2 minted a second `JobApplicantTestResult` — and the shortlisting blend averages every scored row. The commonest reason to grant a re-sit (Q4) is that the first went wrong; averaging penalised the candidate for the power cut HR granted the re-sit over. | **Designing** block K, before it ever ran | One ledger row per test per candidate, owned by the latest finalised attempt, `TestDate` moving with it. Attempt history lives in the sittings. K5 asserts one row carrying 15/15, not an average of 6 and 15 |
| 2 | **The countdown was wrong by the viewer's UTC offset after a refresh.** `datetime2` carries no offset, so EF reads every value back as `Kind=Unspecified` and JSON drops the `Z`: `MustSubmitBy` left the API as `…Z` on the start and without it on the next read. A browser parses the second as **local** time. Invisible at UTC+0; an hour east, the timer reads an hour long and the server cuts the candidate off while their clock still shows time. | Block E3, comparing the deadline across two responses | `AsUtc` on every instant the service serves — 15 sites |

⚠ **Defect 2 is not lane E's alone.** Every HR DTO that serialises a `DateTime` read back from a
`datetime2` column has the same property; lane E fixed its own because a countdown is the one field
where an offset-sized error changes the outcome. Recorded here, not swept.

**Block O exists because nothing else runs the sweep.** No harness in the repo exercised the
recruitment lifecycle sweep before this one, so `ExpireOverdueSittingsAsync` had never executed —
this repo has twice found HR sweeps that never ran in production. O asserts an abandoned sitting is
expired and marked on the **2 marks it saved of the paper's 15**, with the denominator still the
whole paper.

**Three things the harness taught about itself, for whoever runs it next:**

- **`start` and `submit` carry `SensitivePolicy`, 5 a minute.** It partitions by `CallerKey`, which
  prefers the authenticated **user id** — so an exam hall behind one NAT does not lock itself out
  (block N asserts it). A real candidate makes two of these calls; the suite makes a dozen, so it
  spreads them across four candidates and waits out the window once before the re-sit.
- **`[Required]` on a string trims before deciding**, so a whitespace-only reason is a **400** from
  model validation — the service's own 422 guard is unreachable over HTTP.
- ⚠ **`appsettings.json` points at `ErpSystemDB`, but `slice-f/setup.mjs` defaults to
  `ErpSystemDB_UAT`** and claims that default "matches the API's own default". It no longer does.
  Run the API with `ConnectionStrings__DefaultConnection` overridden to UAT, or every fixture fails
  on *"the statement matched NO rows in ErpSystemDB_UAT"*. And the host machine ran out of RAM
  mid-verification (1.1 GB free of 23.4; SQL trimmed to a 385 MB working set; every query at its
  35 s timeout) — two runs died in unrelated places before that was diagnosed.

#### What lane E still owes

| | Item | State |
|---|---|---|
| **E-b** | E6 — printable paper + marking key + offline results entry | **done** — see the E-b entry below |
| | E7 — demo aptitude test seed + coverage-manifest rows | **done** — see the E-b entry below |
| **Walk** | E4 end to end in the careers portal, including refresh mid-test (§ 5, item 2) | not done — the demo seed leaves Elikem Attipoe's paper unsat for exactly this |

---

### Lane E-b — the printed paper, the paper sitting, the demo seed · DONE 2026-09-22 · 86 assertions ×2

Harness: `dev-harness/hr-recruitment/run-round4-e6.mjs` (blocks P–T). Migration:
`20260922212045_AddRecruitmentTestSittingMode` (guarded SQL, named `DEFAULT 1`; applied to
`ErpSystemDB_UAT` — every one of the 44 existing sittings reads Online). Lane E-a's 141 unchanged; all
18 neighbouring suites at the baselines listed in the E-a entry.

**E6 — offline, as built.**

- **The printed paper and the marking key** follow lane F's house pattern exactly: server-composed
  HTML from an HR-editable `RecruitmentTests` template catalogue, printed by the browser, **reusing lane
  F's print stylesheet** rather than copying it (two print stylesheets drift). The question paper prints
  blank, or one named paper per candidate an assignment reaches, in **surname order** for the sign-in
  desk. The key carries the correct choices, expected numbers, marking notes, and the rules the server
  applies online.
- **A paper sitting** — `POST api/hr/recruitment/tests/sittings/paper` — takes what the candidate
  **ticked**, never a mark, and the server marks it with the **same marker** the portal uses, against the
  whole paper. Written answers take the marker's marks, and the sitting finalises in the same call: the
  ledger row says *"Sat on paper … entered by HR"* and carries the venue and the invigilator (an
  employee id — the ledger's `InvigilatedById` is an Employee FK).
- **Who an assignment reaches** — `GET …/assignments/{id}/candidates` — with each candidate's attempts,
  latest result and, when a paper sitting cannot be recorded, why.
- Screens: `/hr/recruitment/assessments/paper` and `/record`, reached from the builder, from every
  assignment row, and from a mode badge on each sitting.

**Decisions taken inside the lane.**

| | Decision | Why |
|---|---|---|
| 1 | **How a sitting was sat is a COLUMN** (`Mode`), not inferred | A candidate's own submission and a script typed in by HR are different kinds of evidence, and a recruitment decision can be challenged. "No access token" would have worked as a signal and nobody should have to know it. |
| 2 | **A printed paper is never shuffled**, even when the paper shuffles online | One marking key has to fit every script in the pile; per-candidate shuffles would each need their own key. |
| 3 | **Double ticks and words-in-a-number are RECORDED**, and marked wrong by the key | A candidate can do both on paper. Refusing them would force HR to "correct" the script on the way in. |
| 4 | **Every written answer must carry its mark** on entry | The script has been marked by hand before it is typed in; an unmarked essay would publish a score missing the essay. |
| 5 | **The window is judged by the date it was SAT**, not the day it is entered | A paper sat on the last day and typed in the next week is on time. |
| 6 | **Refused over a running online attempt** | Two live attempts would finalise in either order, and the ledger row belongs to the last one finalised. |
| 7 | **The entry screen shows the paper WITHOUT the answers** (the candidate projection) | Whoever types a script in should record what was ticked, not look at the key while doing it. |
| 8 | **One reach rule, extracted** — `RecruitmentTestReach` | The printer and the portal both ask "which applications does this assignment reach"; two copies of the live-status list is how a withdrawn candidate gets printed a paper the portal would refuse. Lane B's extraction, for the same reason. |

**E7 — the demo seed.** `hr-demo-smoke/scenarios/052-recruitment-tests.mjs`, after 050. A TDC
*"Numerical and Verbal Reasoning"* paper (8 questions, 20 marks, three sections) on **VAC-000021,
Estates Officer — Housing** — the live-pipeline vacancy at Shortlisting, which has criteria and is not
walked by a runbook. Weighted at **30%**. Three scripts entered as sat on paper: **95%**, **50% — a pass
exactly on the mark**, and **45%**, whose blank question still counts towards the total. **Elikem Attipoe**,
the demo careers account, is on the vacancy with the paper **unsat**, so a presenter can sit it live and
watch it land in the marking queue. Idempotent: a second run created nothing (1 paper, 1 assignment,
3 sittings, 3 ledger rows). Seven manifest rows added; the 634 existing rows unchanged;
`verify-tables --area recruitment` reads **74 of 74** required tables holding data.
⚠ **No invitation email is sent** — the demo candidates carry real-looking gmail.com addresses.

**Found on the way.**

- ⚠ **`PUT /job-vacancies` writes all 25 of its fields** — an omitted one is nulled. The demo seed and
  both lane E harnesses now round-trip every field and the seed refuses to PUT if the read lacks one.
  Lane E-a's harness had been sending eleven and nulling the rest on its fixture vacancy; harmless there,
  but the pattern gets copied.
- **`register-candidate` carries `SensitivePolicy`** — 5 a minute, by IP for an anonymous caller. Two
  back-to-back runs of a suite that signs up four candidates collide; both lane E suites now wait the
  window out on a 429.

**Owed, and recorded rather than built:**

- **An untimed paper opened online and never submitted blocks a paper sitting for good.** A timed one
  expires on the sweep; an untimed one has no clock, and there is no HR action to cancel an attempt. The
  roster says so in words (*"ask the candidate to submit it"*). A cancel-attempt action is the fix if it
  ever bites.
- **E4's browser walk** (§ 5 item 2) — still not done; the seed is ready for it.
- **A runbook page for the test engine** — lane P's.

---

### Candidate-facing email links — a sweep, DONE 2026-09-22

Found while wiring lane E's invitation. **Every call-to-action button in the recruitment email
catalogue pointed at a page that did not exist**, and had done since the standalone candidate portal
was retired on 2026-08-31 — three weeks of live email.

**Why it was cheap to fix, and the check that established it.** These are `DefaultHtmlBody` values,
used only when no row exists in `EmailTemplates` for that module+event. Both databases held 11
stored templates, **none of them Recruitment and none carrying the old path**, so the C# default
*was* the live text and no data migration was needed. Run that query first, always:
`SELECT COUNT(*), SUM(CASE WHEN HtmlBody LIKE '%<path>%' THEN 1 ELSE 0 END) FROM EmailTemplates`.

| | Was | Now |
|---|---|---|
| 6 buttons — ApplicationReceived, ApplicationUnderReview, ApplicationShortlisted, AssessmentPending, OfferAccepted, TalentPoolInvitation | `/careers/portal/dashboard` — no such page | `/external-portal/careers` |
| InterviewInvitation + InterviewRescheduled (shared `interviewDetailsTable`) | `/careers/portal/confirm-interview/{token}` — **page never built** | page built at `/careers/confirm-interview/[token]`; template repointed |
| InterviewPanelistAssignment | `/interviews/confirm-panelist/{token}` — **page never built** | page built at that exact route; template unchanged |
| `JobOfferHireService` non-token `RespondUrl` fallback | `/careers/portal/login?returnUrl=/careers/portal/offer/{id}` — wrong path, wrong parameter name, non-existent target | `/login?redirect=%2Fexternal-portal%2Fcareers` |
| `RespondUrl` sample value, and a stale example in a `ServiceCollectionExtensions` comment | advertised routes that never existed | corrected |

**Verified mechanically, not by eye.** Every link the HR email layer emits was extracted and checked
against a real page; all eight resolve. `/careers/portal/offer-response` is the only survivor of the
retired namespace and genuinely exists.

**⚠ Two confirmation pages, not one.** The panelist link was found while checking the candidate one
and was equally dead, with an equally live API half. Placement is load-bearing in both cases: the
candidate page sits under `/careers` (that layout carries no AuthGuard); the panelist page is
top-level with no shell, because a panelist may be an **external associate with no account at all**
— `/external-portal/*` would lock them out and the careers header would greet a director with
"Create an account".

**⚠ Both confirm endpoints are a GET that CONFIRMS AND SPENDS the token.** Two consequences, handled
in the pages and recorded here rather than designed away:

- React StrictMode double-invokes effects, so a naive fetch reports *"invalid or expired"* over a
  confirmation that had just succeeded. Both pages use React Query with `retry: false` and every
  refetch disabled.
- **A mail scanner that pre-fetches links spends the token before the human clicks.** The attendance
  is still recorded correctly — the scanner's GET is what records it — but the person then sees the
  expired message. That is a property of the token design, not of these pages, which is why
  *"already confirmed"* is presented as a **success**. Changing it means making the confirm a POST
  behind a landing page, or not spending the token on read. **Deliberately not done here** — it is
  a behaviour change to a live flow, and it belongs with § 9.4's token rework.

#### Recorded, not fixed

**`JobApplicationService.AddTestResultAsync` stamps an employee id into a user column.** Its
parameter is named `createdByUserId`, its only caller (`JobApplicationController:293`) passes
`employeeId.Value`, and `ToEntity` writes it to `CreatedBy`. So every offline test result records
its author as an employee id where every other row records a user id. Harmless to behaviour, wrong
in the audit column, and the same shape this repo has recorded before — *a column named
`...UserId` holding an employee id*. Lane E's own ledger writer does not go through that door and
stamps both correctly (`CreatedById` = user, `MarkedById` = employee). Not fixed: it is another
lane's door, and changing it touches the controller, the parameter name and existing rows.

---

### Lane I — triggers that fire · DONE 2026-09-22 · 100 assertions ×2

Harness: `dev-harness/hr-orientation/run-round4-i.mjs`, blocks A–H. Migration
`20260922225423_AddOrientationTriggers` (guarded SQL; applied to `ErpSystemDB_UAT`; history row,
table, five columns and the rule remap verified in SQL). The PDF's question — *do the triggers
fire?* — had the answer **no**: `OrientationEnrollmentTrigger` was written by the seeder and read by
nothing, audience rules had no resolver, and an onboarding plan was created only by a person.

**What was built.**

| | |
|---|---|
| **I1** | Rules target the shared `HrAudienceTargetType` through `IHrAudienceResolver` (a unit includes the units beneath it), narrowed by a new `Population` — new hires (employed ≤ 90 days), management (heads a unit or manages someone), contractors (contract, fixed-term, consultant, freelance). "New hires in Operations" was not expressible before. `OrientationAudienceScope` survives only as the programme's descriptive label. |
| **I2** | A typed target picker (the level→unit and level→location cascades, levels, positions, the employee search) replacing the free-text GUID box; targets validated server-side (a position's id under "unit" is now a 422); `TargetEntityName` filled at last (§ 3 defect 15); a live reach line on the form and a *Reach today* column, both proven against an independent SQL count. |
| **I3** | `OrientationEnrollmentTriggerService`: **OnHire** from `EmployeeService.CreateEmployeeAsync` (the form and the import) and `ConfirmStartAsync`; **OnTransfer/OnPromotion** from `StaffMovementService.ImplementAsync`; **OnProgramPublish** from the status change; **Scheduled** plus a catch-up of every dated rule in a nightly `OrientationTriggerBackgroundService`, **registered in the same change**; **Manual** only from HR's new *Enrol audience now* (preview first). Each automatic enrollment records its rule, event and date (`AudienceRuleId`, `TriggerEvent`, `TriggerDate`). |
| **I4** | `OnboardingPlanTemplateAudience` + `OnboardingTemplateApplicabilityService`: most specific wins (position 100 › own unit 90, one less per level up › level 40 › location 30 › everyone 10 › the default template 0), exclusions veto, ties flagged `IsAmbiguous`; `GET …/applicable`; and **a plan created automatically when a hire's start is confirmed**, with `TemplateSelectionReason` — making true what `RecruitmentEntities.cs` had always claimed. ⚠ *Corrected in lane K-a:* that plan had no coordinator, so its reminders reached nobody; the confirming officer now coordinates it (H6b, H6c). |
| **I5** | *Orientation → Enrollment Triggers*: for one employee, per programme, the verdict (enrolled, enrols tonight, waiting for its date, waiting on a prerequisite, excluded, window lapsed, only when HR enrols, not in the audience…) with the reason, every rule's match and window, and which onboarding template their plan would come from. |

**Rules the triggers run on — each a decision taken inside the lane.**

| | Rule | Why |
|---|---|---|
| 1 | A dated trigger fires on or after its date + delay, and **not more than 30 days later** | Catches a late-entered hire, a failed hook and a rule added after the event — and stops an import of the existing workforce, or a brand-new rule, reaching back into history (asserted: a 2015 hire fires nothing). |
| 2 | **Hire rules count from the employment date only** — no fallback | The record's creation date was the first stand-in; on UAT every one of the 221 active employees without an employment date was a harness fixture from the last three days, and it would have enrolled 250 people instead of 29. The diagnostic tells HR to set the date. |
| 3 | **Any earlier enrollment blocks a rule — including one HR withdrew** | A rule must never put back somebody a person took off; it is also what makes every re-run harmless (asserted twice). |
| 4 | Exclusions apply **whatever trigger they were written against** | "Never auto-enrol contractors" should not need repeating per trigger. |
| 5 | A **mandatory** prerequisite holds back an automatic enrollment; a manual one is not gated | Automation is not a judgement. The sweep enrols them the first night after they complete it (asserted: the gate lifts). |
| 6 | Movement mapping: transfer, lateral move, secondment → OnTransfer; promotion → OnPromotion; demotion, acting, redesignation fire nothing | A transfer orientation is about arriving somewhere new. |
| 7 | The onboarding plan on hire is created **only for an employee the hire created** | A linked internal hire still carries their old placement at that moment, and would be handed the plan for the job they are leaving. |
| 8 | Templates target **organisation level, not grade** (the plan said `gradeId`) | Grade is payroll's axis and not one the shared audience model has. |

**⚠ Two incidents on the UAT demo data — both mine, both reversed with the user's agreement.**

1. **The seeded "All employees annually" rule enrolled all 1,135 active employees** into
   ORI-CMP-001 on the harness's first tenant sweep. I had said its onboarding prerequisite would hold
   them back; the seeder made that prerequisite **advisory**, and I had not checked the flag. The
   1,135 rows (one transaction, count-checked, no dependants) were removed and the rule — on UAT and
   in `OrientationDataSeeder` — is now **Manual**: HR enrols that audience deliberately, with the
   preview. *A scheduled rule on "everyone" is a tenant-wide enrolment; that is what it says.*
2. **Scenario 145 created 445 onboarding plans (4,005 tasks) for harness fixtures.** It had died on
   "Invalid time value" since fixtures began receiving `TDC/` numbers (sqlcmd prints a null date as
   the *text* `NULL`); my first repair — "require a real date" — admitted 791 dated fixtures on
   probation. Stopped within two minutes, the rows removed, and the scenario now takes the six
   starters the books name by the marker the demo workforce seeder stamps
   (`CreatedBy = 'TdcDemoWorkforceSeeder'`, exactly six on UAT).

**What verifying it found in the lane's own code.** The diagnostic told a person held back by a
mandatory prerequisite under a **manual** rule that they would be enrolled "when HR enrols" — sending
HR to a button the same run proved enrols nobody (E12). The prerequisite is now reported as the
blocker whatever the trigger (E14/E14b). The harness also polluted itself between runs (earlier
runs' fixture templates tied with the current one — the product flagged the tie correctly); it now
clears its fixture template audiences before and after.

**Also for the record.**

- **The migration remaps the two existing rules exactly once**, inside the guard that adds
  `Population`; job-grade and custom rules (none on UAT) are switched off with a plain-language
  note. Down maps the targets back: a scratch Down→Up without it turned a unit rule into
  "everyone".
- **HR holds orientation Read and Write but not Admin**, so *Run tonight's sweep now* is gated to
  admins and hidden from HR.
- **UAT side effect:** "New Employee Onboarding" now carries 39 automatic hire enrolments — the 29
  demo people employed on 2026-09-20, plus the harness's backdated fixture hires.

**Neighbouring suites.** All 20 recruitment suites and `hr-company-schedule/run-round4-d` exactly at
their recorded baselines. `hr-orientation/run.mjs` **107/107** and `run-lane6-feedback` **20/20**
— after repairing their fixture, which had supplied a staff number since the 2026-09-10 register
change and so could not mint one actor. Three below baseline, each explained and pre-existing:
`hr-movements/run-h` 32/33 (§ 9.1's "Accounts Officer" — established for 1, now 616 in post);
`hr-employee-import` 69/76 and 48/52 (its 2026-09-03 assertions predate the register's
staff-number format warning and round 3's salary-change approval rule; the commit runs clean and the
API log carries no trigger failure).

**Owed.**

| | Item | State |
|---|---|---|
| **I-b** | Re-enrolment of recurring programmes (`IsRecurring`/`RecurrenceFrequency`): today "any earlier enrollment" blocks last year's refresher | **done 2026-09-23 — see the I-b entry below** |
| | Browser walk of the rule form (typed picker, reach line), the template audience panel and the diagnostic | not done — no browser automation |
| | A concurrent hook and sweep could in principle both enrol one person (no unique index on programme + employee; existing data may hold duplicates) | recorded, not built |
| | `EnrollAsync`/`BulkEnrollAsync` stamp a **user id** into `EnrolledByEmployeeId` | recorded — the "…Id holding the wrong kind of id" shape again; not this lane's door |

### Lane I-b — recurring programmes renew; the effective dates bind; HR enrols again by hand · DONE 2026-09-23 · 152 assertions ×2

Harness: `dev-harness/hr-orientation/run-round4-i.mjs`, new blocks **R** (recurrence), **W**
(effective window) and **X** (HR enrols again by hand) — lane I's 100 unchanged, 52 added. **No
migration**: the one new value is an enum member (`OrientationEnrollmentSource.Recurrence = 5`) in
an existing int column. Built at the user's request after lane I was verified; block X at the
user's request before I-b was committed.

**Why it was needed.** `IsRecurring` and `RecurrenceFrequency` were stored, shown on the programme
form ("Recurs — compliance refreshers people must retake on a cycle") and **read by nothing**; and
lane I's own rule — any earlier enrolment blocks a rule — would have blocked every annual refresher
after its first year.

**What renews, and when — decisions taken inside the lane.**

| | Rule | Why |
|---|---|---|
| 1 | The nightly sweep opens a person's next cycle when their **latest** enrolment is **Completed** and one period has passed | The latest enrolment decides: an open cycle is still the current one; a withdrawal or cancellation is HR's act and is never renewed over; a Failed attempt is not a completion. |
| 2 | The cycle **opens early by the programme's completion deadline** — an annual programme with a 14-day deadline reopens 351 days after completion | So the ordinary due date (enrolment + deadline) falls **on the anniversary**, and a 12-month certificate never lapses. A cycle that opens late gets a full deadline from the day it opens — never a date already past (R14). No deadline set → it opens on the anniversary with no due date (R23). |
| 3 | Only people the programme is **still for** renew: one of its active inclusive rules — whatever the trigger — must still reach them; a programme with no rules is managed by hand and renews everyone who completed it; exclusions apply either way | A site-safety refresher does not follow somebody who moved to Finance (R6, R19); a programme "for new hires" never renews, because a year on they are not new hires. |
| 4 | No catch-up window, unlike the dated triggers | A renewal creates exactly one open cycle, after which the latest enrolment is no longer Completed — so a completion from years ago renews once, not once per missed period (R25). |
| 5 | Not gated by prerequisites | They were met for the first cycle. |
| 6 | Recorded as source **Recurrence**, no rule id, trigger date = the completion it renews | The diagnostic and the run summary can tell a renewal from a rule. |

**The effective dates now bind — a gap in lane I, found while building this.** `EffectiveFrom` and
`EffectiveTo` were stored and read by nothing, so lane I's triggers would have gone on enrolling
people into a programme whose effective period had ended. "Active" now means Active **and** in its
dates, for every route — the triggers, renewals, and "Enrol audience now", which refuses with the
date (W1). A programme published **ahead** of its effective-from runs its publish rules on the
nightly sweep once it comes into effect (W3, W6), and a hire the hook skipped is caught up the same
night (W4, W8).

**Two refusals the API lacked.** A recurring programme with no frequency (the form required one; the
API accepted it and the flag then renewed nothing) and an effective-to before the effective-from
are now 422s (R0, R0b).

**ORI-CMP-001 recurs annually** — on UAT (two columns, one row, count-checked) and in
`OrientationDataSeeder` for rebuilt databases — at the user's request. It renews nobody today: its
one enrolment is overdue, not completed; its rule is the Manual "All employees annually", so every
future completer stays in its audience.

**Screens.** The *Recurs* switch and the effective dates say what they now do; the rules tab says
when a programme recurs; the diagnostic gains **Next cycle scheduled** / **Next cycle opens tonight**
with the completion, opening and due dates; the sweep preview counts renewals and anyone due but no
longer in the audience. *Renewed* is deliberately **not** offered in the manual-enrolment source
picker — HR enrolling somebody by hand is not a renewal.

**HR enrols again by hand — decided with the user, 2026-09-23.** Manual enrolment (single and
bulk) refused anybody with ANY enrolment on the programme, ever — so HR could neither open a
recurring programme's next cycle early nor put back somebody it had withdrawn by mistake. It now
reads the person's **latest** enrolment, as renewal does (`EmployeeOrientationService.WhyCannotEnrolAsync`):

| Latest enrolment | Manual enrolment |
|---|---|
| none | allowed, as before |
| **withdrawn, cancelled or a no-show** | **allowed** — HR's ending, HR's to undo (X1–X4, X13) |
| **completed, programme recurs** | **allowed** — the next cycle, opened early (X9–X12) |
| completed, programme does not recur | refused: *"…completed this programme, and it does not recur…"* (X7, X8) |
| still open | refused: *"…already on the current cycle…"* (X5, X6) |

Bulk applies the same rule and skips rather than refuses (X11, X12). Both doors stay HR-only
(orientation Write, X16), so this is never a way for an employee to undo a withdrawal — and the
**automation still never re-enrols anybody**: rules treat any earlier enrolment as final, renewals
never open over an ending.

⚠ **Found while doing it:** a completed cycle HR then **withdrew** still reads *Completed* — only
the enrolment status says HR ended it — so the renewal would have opened a new cycle straight over
the withdrawal. `RenewAsync` now checks the enrolment status as well (X14). The diagnostic gains an
**Ended by HR** verdict instead of calling a withdrawn person "Enrolled", and tells HR it can
re-enrol them (R7, X15).

**Screens for it:** the enrolments list's row menu offers **Re-enrol** (withdrawn / cancelled /
no-show) and **Open the next cycle now** (a completion on a recurring programme), each behind a
confirmation and only on a person's latest row for that programme — the programme summary now
carries `IsRecurring` for that. The enrol dialog and its "skipped" note give the real reason instead
of "already enrolled".

**Neighbouring suites:** `hr-orientation/run.mjs` 107/107 and `run-lane6-feedback` 20/20 — the ones
that create and update programmes and enrol people, which is where the new rules could bite. The demo
scenario that touches programmes (140) only reads them.

### Lane L — the sessions dropdown · DONE 2026-09-23 · 29 assertions (block S) · 181 ×2 with lane I

Harness: block **S** of `dev-harness/hr-orientation/run-round4-i.mjs`, as the plan asked. **No
migration.**

**L1 — reproduced before anything was built**, against `ErpSystemDB_UAT`, as `hr.head`:

| Programme | Delivery | `GET orientation-sessions/program/{id}` | Verdict |
|---|---|---|---|
| ORI-ONB-001 New Employee Onboarding | **Blended** | 200, `[]` | a data gap — it has a classroom day and nobody had scheduled one |
| ORI-CMP-001 Anti-Harassment & Code of Conduct | **Self-paced online** | 200, `[]` | legitimately empty |
| ORI-PRD-001 Q3 Product Launch | Virtual | 200, one session, open | fine |
| *(any of them, as `staff`)* | | **403** | drawn by the dialog exactly like "no sessions" |

So the endpoint was right, and "the dropdown is empty" was two different truths plus a lie the
screen told about a third. The probe also showed the dialog offering **all 111 programmes, 87
retired and 12 drafts.**

**What was built.**

| | |
|---|---|
| **L2** | Under the session dropdown, one line tells five states apart: loading; no permission (403); failed; self-paced ("people work through it on their own"); nothing scheduled — with a **Schedule one** link that opens the sessions screen's form with the programme chosen (`?schedule=`). A 403 is not retried. |
| **L3** | The dialog offers only programmes that take enrolments — Active and in their effective dates — and says how many it hides and why. The programme summary carries `AcceptsEnrolment` / `ClosedBecause` for it. |
| **L4** | Sessions are listed open-first; a closed one stays visible but cannot be chosen, and says why ("not open: it was cancelled"). The session summary carries `AcceptsEnrolment` / `ClosedBecause` / `EnrollmentDeadlineAt`. |
| **L5** | Two **Corporate Induction Days** for ORI-ONB-001, open for enrolment, a fortnight and six weeks out (scenario 140; it keeps two upcoming, whatever month it runs). **Deliberately none for compliance** — a deviation from the plan, which said "seed sessions for the onboarding and compliance programmes": compliance is self-paced online, so the fix there is the dialog saying so, not a session nobody would attend. |

**Defects found and closed — the server had checked none of this.**

| | Was | Now |
|---|---|---|
| 1 | HR could enrol people onto a **retired or draft** programme — while the programme page said a retired one "enrols nobody new" | refused, with the reason; one definition (`OrientationProgramEnrolment`) — Active and in its effective dates, the same the triggers use |
| 2 | The enrol path **never looked at the session**: a cancelled one, a finished one, one past its enrolment deadline, **another programme's** | refused, with the reason; one definition (`OrientationSessionEnrolment`) shared with the "open for enrolment" list and the dropdown's flag (S8 asserts the list and the flags agree) |
| 3 | Moving an enrolment into a session (the edit door) had the same gap | the same check, on a change of session only — editing an enrolment whose session has since closed does not start failing |
| 4 | The sessions screen's code said "the server would refuse" scheduling against a draft or retired programme — it did not | it does now (S1) |

**⚠ A third demo-data incident this round — mine, reversed with the user's agreement.** Running
scenario 140 to seed the induction days also ran its benefits half, whose "already done?" guard
reads `GET /hr/employee-benefit-enrollments` — **an endpoint that does not exist (405)** — through
`.catch(() => [])`. The failure read as "no enrolments yet", so the guard had **never** held and every
run re-created the same 45 benefit enrolments. I had predicted the run safe from the table count (48)
without checking what the guard itself read; two runs added **90 duplicates**. Removed (one
transaction, count-checked, nothing referenced them; every employee–policy pair back to its one
original row), and the guard now counts in the database, as the orientation half of the same
scenario always has. Two further runs added nothing. Before that, the scenario was also made safe
the way scenario 145 was on 2026-09-22: it takes the six starters by the demo seeder's stamp (not
every probationer — ~800 of them are harness fixtures now), and finds the programme by its code
rather than page one of 111, whose fallback was usually a retired fixture.

**Neighbouring suites:** `hr-orientation/run.mjs` 107/107; `run-lane6-feedback` 20/20 — after its
fixture was repaired: it enrolled onto a programme it never published, i.e. a **draft**, which the new
rule refuses as the product's own status text says it should ("not yet available"). It now publishes
first and retires before deleting. `verify-tables --area orientation`: 23 of 23.

**Not browser-walked** — the dropdown's five states are the next thing a screen walk should look at.

### Lane J — copy a template, a programme, a session · DONE 2026-09-23 · 94 assertions ×2

Harness: `dev-harness/hr-orientation/run-round4-j.mjs`, **94 ×2**. **No migration.**

**What was built.**

| | |
|---|---|
| **J1** | `POST /api/onboarding-plan-templates/{id}/clone { newName }` — the template and its tasks, every field (timing, mandatory, order, instructions, owning position). **Never the default** — a copy must not take over the fallback. A name already in use is a 422. |
| **J2** | `POST /api/orientation-programs/{id}/clone { newName, newCode? }` — every setting, the modules and their content, the prerequisites, the quiz **with which option is right**, and the audience rules, as a **Draft**. The code is numbered for you or given; a code in use is a 422 — **including a deleted programme's**, which still holds it in the unique index (a duplicate-key 500 otherwise). Retired modules, items and questions come across still retired; deleted ones do not come at all. A content item's file or link is shared, not duplicated. Sessions and enrolments are deliveries of the original and stay with it. |
| **J3** | `POST /api/orientation-sessions/{id}/clone { scheduledStartAt, scheduledEndAt?, title? }` — *Run again*. The new run keeps the original's **length** unless an end is given, and its enrolment deadline keeps the **same notice** before the start. A Draft with a new code and nobody enrolled; the same programme, venue, link, capacity, waitlist and approval rules; the facilitators come across **unconfirmed** — they agreed to the old date. The recording and the actual times are the old run's and are not copied. Scheduling rules apply as to any new session: the programme must be active. |
| **J4** | **Copy** on the templates list and each template's page, **Copy** on the programmes list and each programme's page, **Run again** on the sessions list and each session's page. The dialog keeps the server's refusal in place so it can be corrected, and lands on the copy. |

**Three decisions the plan did not make — each one this entry's to defend.**

| | Decided | Why |
|---|---|---|
| 1 | **A template's audience is not copied** | Audiences arrived with lane I4, after this plan was written. A hire gets the most specific template whose audience reaches them, and a tie is settled **by name** — a copy carrying the original's audience would tie with it, and could start being handed to real hires by accident of the alphabet. Uncopied, the copy is chosen by hand until it is given an audience of its own, which is what a copy is usually made for. The dialog says so. |
| 2 | **A programme's audience rules ARE copied** | A Draft never fires, so they are inert until somebody publishes the copy — and then they enrol people just as the original's do. The dialog says that too, and says to retire the original at the same time if the copy replaces it, or both will enrol the same people. |
| 3 | **The actions are on the record pages as well as the lists** (the plan said lists) | The sessions list shows upcoming, open and published runs — **a finished session is on none of them**, and it is the one most often run again. |

**Built the pipeline clone's way, not the appraisal clone's.** The plan named
`AppraisalTemplateService.CloneAsync`'s nested rebuild. Each child is instead added through its own
repository with its key set (`JobPostingPipelineService.ClonePipelineAsync`'s way): a child reached
through the navigation of a parent EF already tracks is taken for an UPDATE of a row never inserted.
The source's children are read **untracked**, so no loaded entity can end up in the new graph.

**How it is proved — by rows, SQL as the oracle.** Copied rows equal the source's, field for field,
by `EXCEPT` both ways, with exact counts beside them so empty-on-both-sides can never pass. A
**SHA-256 fingerprint of every source row** — deleted ones included, every column including the
audit stamps — is taken before the copy and again after the copy has been edited in every child
collection (tasks renamed and deleted, options replaced, a content item and a rule changed, a module
and a prerequisite deleted, a session's venue and capacity, a facilitator's confirmation): unchanged
in all three blocks, so no source row was written at all. Refusals: a name or code in use, a blank
one, a missing source (404), an ordinary employee (403), an end before the start, no start (400), a
retired programme. A copy of the tenant's default template is not the default, and the tenant still
has exactly one.

**Block Z accounts for every column** of the eleven tables a copy writes — each is copied, set by
the copy on purpose, or audit. A column in none of those lists **fails the suite**, so a column added
later has to be decided rather than silently left behind by every copy. Lane M adds two to the
facilitators; its section now says so (M4). Negative-controlled: a column left out of the account is
named, and so is one the account names that the database no longer has.

**Two defects older than the lane — the harness's first run found both, and both are closed.**

| | Was | Now |
|---|---|---|
| 1 | **Editing a quiz question's answer options had never once saved.** `UpdateQuestionAsync` soft-deleted the old options and then also removed them from the tracked question's `Options` collection — to keep fixup from putting them back into the response. But Question → Options is a required relationship with `DeleteBehavior.Restrict`, so removing a child **severs** it, and EF refused the whole save. Every edit of a question with options, from the programme screen's question panel, failed that way from the area 15 sweep (`21ed54cf`, 2026-08-13) until now: no harness had ever sent one. | The collection is left alone; the response's options come from an **untracked** read, which fixup cannot reach. P28 asserts the response carries the new options and not the replaced ones — the concern the removal existed for. |
| 2 | **A task due before the start date could not be saved.** The template screen offers −90…365 days, with the hint *"Negative for before the start date"*; the server's range was 0…365, carried over unexamined in the original port. The contract and the system accounts — the tasks most often due before day one — were a 400. | Both DTOs take −90…365. Plan creation already computed `StartDate.AddDays(n)`. No negative offset existed on UAT: the server had never allowed one. |

The first build ran **78/83 — precisely the five assertions of those two defects**; after the rebuild,
83/83 twice; with block Z, **94/94 twice**.

**Neighbouring suites:** `hr-orientation/run.mjs` 107/107; `run-lane6-feedback` 20/20;
`run-round4-i` 181/181 — all at baseline. Not run, and why: `hr-w3-permissions/run-slice13` tests
the gates on existing endpoints, and this lane's controller changes are additive only (the clone
endpoints' 403 is asserted in the lane J suite); demo-smoke scenarios 140/145 are seeders, not tests,
and set no negative offset.

**Hygiene:** the suite deletes what it made — the template's audience first, the sessions after
withdrawing the fixture enrolment, the programmes after retiring the active one. Checked in SQL after
the final runs: no live `R4J` template, programme or session, no fixture enrolment left open, and one
default template. What stays is what every suite leaves: two fixture people per run, hired in 2019
so no new-hire rule reaches them.

**Frontend:** a scoped type-check (`tsconfig.round4-lane-j.json`) clean, with a negative control (a
planted error was caught); eslint clean. **Not browser-walked.**

### Lane M — external facilitators from the training vendor register · DONE 2026-09-23 · 54 assertions ×2

Harness: `dev-harness/hr-orientation/run-round4-m.mjs`, **54 ×2**. Migration
`20260923091928_AddOrientationFacilitatorRegisterPick` — scaffolded, rewritten as guarded SQL,
proved on a scratch database (Up twice, the keys refusing unknown ids, Down twice, Up again), then
applied to UAT and checked there (history row, both columns, both keys, both indexes, all 32 existing
facilitators intact).

**What was built.**

| | |
|---|---|
| **M1** | `OrientationSessionFacilitator` gains `ExternalFacilitatorVendorId` → `TrainingVendors` and `ExternalFacilitatorTrainerProfileId` → `TrainerProfiles`, both nullable, indexed, keyed. The three text columns stay, as the **snapshot**: the vendor's name as the organisation, the trainer's name, and the trainer's own address when their contact is one — else the vendor's contact address. |
| **M2** | The facilitator dialog asks **who it is** — one of our employees / from the training vendor register / someone else — and shows only that mode's fields. Register mode offers the active vendors (`GET /api/training-vendors/active`) and the vendor's active trainers, with *To be confirmed by the vendor* for a vendor that has not named anyone. A 403 on the register is said, not drawn as "no vendors" (lane L's lesson). The list shows *From the training register*, and *trainer to be confirmed* where it applies. |
| **M3** | A vendor or trainer **booked for a session that has not happened yet** cannot be deleted from the register: a 422 naming the sessions, and suggesting *inactive* instead. A soft delete never trips the foreign key, so this is the only thing that keeps a booked vendor on file. One named only on a finished session goes, and that session keeps saying who delivered it. |
| **M4** | *Run again* (lane J) copies the pick with its snapshot. Lane J's block Z accounts for both new columns and passes (94/94). |

**Decisions this lane made — each one this entry's to defend.**

| | Decided | Why |
|---|---|---|
| 1 | **The snapshot is taken when the pick is made or CHANGED, never otherwise** | The precedent (`PreEmploymentCheckService.ResolveProviderNameAsync`) re-mirrors the supplier's name on every save. For a session that would rewrite history on the first confirmation or note after a rename — and "renaming a vendor later does not rewrite what an old session says" was the plan's own requirement. |
| 2 | **Availability is checked at the same moment and only then** | A vendor blacklisted after it was booked must not make that booking uneditable (confirming it, adding "checking whether to keep them"). The same rule lane L applied to an enrolment's session. The register's current state is reported instead — `RegisterNote` on every read: *has since been blacklisted*, *no longer active*, *has since been removed*, *no longer listed with the vendor*. |
| 3 | **A vendor alone is a valid pick; a trainer alone implies its vendor** | A vendor is often booked before it says who will come. The display falls back to the vendor. |
| 4 | **Typed values beside a register pick are ignored** | The register decides the snapshot; otherwise the screen could send a name the register does not hold and the "reference" would mean nothing. |
| 5 | **Refused picks:** a blacklisted vendor (with the register's reason), an inactive vendor or trainer, a trainer of another vendor, an internal trainer (*add them as an employee*), an employee **and** a vendor, an unknown id | Each is a 422 that says which. |

**Two gaps closed on the way.** (1) The **edit** path of a facilitator had no check at all — the add
path refused a facilitator with neither an employee nor a name; the edit path saved one. Both paths
now run one resolver. (2) Reopening an **employee** facilitator's dialog showed an empty search box
rather than the employee — the picker was never given the name.

**Permissions checked before reusing the register's endpoints.** The vendor and trainer reads sit on
`HR.Training.Read`, the dialog on `HR.Orientation.Write`. On UAT every role holding the second holds
the first (HR, SuperAdmin, TenantAdmin), so no narrower door was built; the dialog handles a 403 for
the day a role differs.

**Demo (scenario 140).** Both Corporate Induction Days had **no facilitator at all**. Now the Head of
HR leads both, and **GIMPA** co-facilitates from the register — named (Dr. Efua Mensah-Bonsu,
confirmed) in October, *trainer to be confirmed* in November — so the demo shows both kinds of pick.
Every guard in the scenario was proved against UAT first (starters 6/6 enrolled, the assessment
already sat, 48 benefit enrolments, two upcoming days with matching titles, already open); the run
added exactly four facilitators and changed nothing else, and a second run added none.
`verify-tables --area orientation` 23 of 23.

**Neighbouring suites:** `run-round4-j` 94/94 (block Z with the new columns); `run-round4-i`
181/181; `run.mjs` 107/107; `run-lane6-feedback` 20/20.
⚠ **`hr-training/run.mjs` has not run to completion since W3 (2026-08-25)** — nothing to do with this
lane. Its fixture supplied a staff number, which the register now refuses (fixed in its `setup.mjs`;
the suites compare against the number the response returns), and then it approves a training budget
as plain HR, which needs `HR.Training.Admin` since W3. It reached 47 assertions, 0 failed — sections
1–5, the vendor and trainer lists the facilitator dialog reads. The W3 two-actor treatment it needs
belongs to area 7; recorded, not done here.

**Frontend:** scoped type-check (`tsconfig.round4-lane-m.json`) clean with a negative control; eslint
clean. **Not browser-walked.**

### Lane K-a — the reminder sweep that delivers · DONE 2026-09-23 · 53 assertions ×2

Harness: `dev-harness/hr-orientation/run-round4-k.mjs`, **53 ×2**. Migration
`20260923110909_AddOnboardingOrientationReminders` — scaffolded, rewritten as guarded SQL, proved on
a scratch database (Up twice; the four windows filled on **both** tenants' settings rows, not only a
seeded one; a duplicate claim refused by the unique index; deleting a run cascading to its log; Down
twice; Up again), applied to UAT and checked there.

**The survey said what the plan assumed, and it held.** All eleven HR reminder sweeps (asset,
certification, discipline, identification, leave, probation, separation, SHE, movement, travel,
team) have **no call** to any notification or email service, and nothing reads their dispatch logs
but their own admin screens — no outbox. They count what they would have said. That debt is HR's
own, so it is recorded as **lane 10 of `HR-FINISH-PLAN.md`**, not in the cross-module defects file
this plan named (that file is for other teams' modules).

**What was built.**

| | |
|---|---|
| **K1** | `OnboardingOrientationReminderService` + its host, on the probation engine's pattern — run header, dispatch log, a dedupe key of kind, item, **due date** and tier (moving a date re-arms), a lock, per-tenant isolation, a staggered start (19 minutes; the other sweeps take 3–29). **Eight rules:** an onboarding task due soon, overdue (three rungs: at due, a week, a fortnight) or completed and awaiting sign-off; an orientation due soon or overdue; an assessment not attempted and an acknowledgement not signed after the chase window; a certificate expiring. Onboarding tasks route to their assignee, else the plan's coordinator; the rest to the participant. A **90-day backlog horizon** keeps history out. |
| **K2** | **Delivery.** One `OrientationNotification` **per person per run** — their items listed — saved in the **same transaction** as the claims (a crash cannot claim a reminder nobody received); then one email per person through the templated email service and a new Orientation & Onboarding catalogue (`OrientationReminderDigest`), after the commit, raced against a 10-second timeout. The outcome is written back **per item**: `Sent`, `Failed`, `TimedOut`, `NoAddress`, `NoMailServer` (checked the way the sender checks), `NotRouted`. |
| **K4** | `POST /api/orientation-reminders/run` (HR.Orientation.**Write** — the HR role holds no Admin, and a button HR cannot press is the company-schedule finding again), `GET preview?asOf=`, `GET runs`, `GET log?days=` (Read). A **Reminders** screen under Orientation setup: run now with what reached people, preview as at a date, recent sweeps with their delivery columns, and the log with who each item went to and what its email did. **Four settings** on `CompanyHrPolicySettings` — `OnboardingTaskDueLeadDays` 3, `OrientationDueLeadDays` 7, `OrientationCertificateExpiryLeadDays` 30, `OrientationChaseAfterDays` 3 — on the HR policy settings screen, and in the configuration register (§ 2.5) as **Enforced**, each proved in both positions. |
| **K5** | Registered with `AddHostedService` in the same change; the host logged its start. **The first scheduled run fired at 11:41:21, 19 minutes after the start as designed**, and wrote its header: queued 2, people 0, **unrouted 2**, mail 0, completed. Both items were lane I's own hire-test tasks, from a plan the hire hook had created at 11:26 — and the reason nobody received them is a lane I4 gap, below. After the fix and a rebuild the host fired again on time (13:02:57): queued 0 — the suites' tenant-wide runs had already claimed what was due — and **unrouted 0**. |

**Decisions this lane made — each one this entry's to defend.**

| | Decided | Why |
|---|---|---|
| 1 | **One digest per person per run, not one message per item** | Measured before building: every one of the 755 coordinated onboarding plans had the same coordinator. One notification per task would have handed her hundreds on the first night. Each item is still claimed and logged on its own. |
| 2 | **Onboarding tasks route to the plan's coordinator when unassigned — not to the holders of the owning position** | On UAT **0 of 6,857** tasks had an assignee; 6,040 were owned by a position. A position can have many holders, and none of them can see an onboarding task anywhere in the portal — so the coordinator, whose queue it is, is told. |
| 3 | **In-app through `OrientationNotification`**, not the platform notification table | It is already merged into the employee's **My Notifications** feed and unread count (area 25), and it is keyed by employee, so a person without a login still has a record. |
| 4 | **The email outcome is recorded, never assumed** | The templated email service returns only true/false and never throws. On a database with no mail server the in-app notification is the delivery and the log says `NoMailServer` — which UAT's first real run shows. |

**How it is proved.** The preview (which claims nothing) shows each rule, tier and recipient; each
setting is flipped both ways and the item appears and disappears; a run with **no mail server** —
UAT's true state — claims the fixtures, writes one notification per person, records `NoMailServer`,
and the participant **sees it in their own My Notifications feed**, unread and counted. Then the suite
starts a **local SMTP sink**, adds a mail-settings row pointing at it, re-arms one item per person by
moving its date, and runs again: both emails **arrive**, to the right addresses, with the task or
programme and "due tomorrow" / "due in 2 days" in the subject, the greeting, the link to the queues or
that very enrolment, and the list kept as text. The row is deleted and UAT is back to no mail server.
A re-run claims nothing; the overdue task climbs to the third rung five days on; the admin reads carry
the delivery counts and names; an ordinary employee is refused both.

**⚠ Demo data — the user's call, made before the first sweep.** UAT held **774** open onboarding
plans: the 6 real starters' and **768 test fixtures** — 749 from the scenario 145 burst during lane I
(its cleanup had removed 445 and missed these; nothing has created any since) and 19 from lane I's
hire tests. All named the Head of HR as coordinator or no one. Asked, the user chose to **cancel** them:
one transaction, count-checked at 768, marked `UpdatedBy = round4-laneK-fixture-tidy-2026-09-23`, with
every plan's previous status saved to `dev-harness/hr-orientation/fixture-plans-cancelled-2026-09-23.csv`.
**The first real run then sent the Head of HR one notification — "41 onboarding tasks need your
attention"**, the six starters' overdue tasks (63–79 days late in the demo data), 25 listed and "…and
16 more", linking to the onboarding queues — and one to a demo employee for her overdue orientation.

**⚠ The scheduled run found a gap in lane I4: a plan created on hire had no coordinator.**
`CreatePlanOnHireAsync` passed the employee, the template and the start date, and nothing else. Template
tasks name a position, not a person — on UAT not one onboarding task has an assignee — so an
unassigned task is reminded to the plan's coordinator (decision 2), and on a plan the hire created
there was none: every one of its reminders would be logged `NotRouted`, for every real hire, from the
first night. Lane I's suite could not see it; it asserted the plan existed, not that anyone owned it.
**Fixed:** the officer who confirms the start becomes the coordinator — HR, the person who knows the
hire has arrived, and the one name the system has at that moment. The id is checked as an employee
of the tenant first (otherwise the plan is created uncoordinated and a warning logged — the hook
stays best-effort), and HR can hand the plan to someone else on the plan screen. Lane I block H now
asserts the coordinator (**H6b**) and, through the reminder preview — which lists before the claim
filter, so it holds whenever it runs — that the plan's tasks are routed to that officer (**H6c**; the
11:41 run is its negative: the same items, routed to nobody). Its tidy-up cancels the plan its hire
created, so the nightly sweep does not remind a fixture officer about fixture tasks for 90 days. UAT held exactly **one** open plan created
on hire — that same fixture — so nothing needed backfilling; it was cancelled like the 768 (same
marker, row 769 of the CSV).

**Neighbouring suites:** `run-round4-i` 181/181 before the coordinator fix and **183/183 twice** after
it (H6b, H6c; the only suite that drives the hire hook — `hr-w3-permissions/run-slice9` only checks
that an employee is refused `confirm-start`, and recruitment `slice-d` is parked on its own § 9.3 wall;
`run-round4-k` 53/53 twice again on the rebuilt API); `run-round4-j` 94/94; `run-round4-m` 54/54;
`hr-orientation/run.mjs` 107/107; `run-lane6-feedback` 20/20. The settings suites that save the
policy all read-then-spread it, so the new windows round-trip; `hr-leave/run-slice6-settings` saved and
restored the settings cleanly, then stopped on its own stale fixture (its fixed employee already has
leave for the dates it books — the recorded leave-harness litter trap), not on this lane.

**Frontend:** scoped type-check (`tsconfig.round4-lane-k.json`, now including the settings screens)
clean with a negative control; eslint clean. **Not browser-walked.**

### Lane K-b1 — the events tell people, and certificates are issued · DONE 2026-09-23 · 84 assertions ×2

Harness: `dev-harness/hr-orientation/run-round4-kb.mjs`. Migration
`20260923140503_AddOrientationNoticeEmailOutbox` — scaffolded, rewritten as guarded SQL, proved on a
scratch database built from UAT's own notification and dispatch-log rows (Up twice, the second
changing nothing; the 20 K-a digests backfilled with the outcome their dispatch log held — 12
`NoMailServer`, 8 `Sent` — and nothing else touched; a queued row written through the filtered index;
Down twice; Up again), then applied to UAT.

**The survey changed the lane** — two decisions, the user's, recorded under lane K above: no screen
could issue an orientation certificate, and non-HR onboarding recipients have no page to be sent to
(K-b2 builds them). It also found the programme's "Send reminders" switch read by nothing.

**What was built.**

| | |
|---|---|
| **Notices** | `OnboardingOrientationNoticeService` **stages** each notice on the caller's unit of work; the caller's own save commits it with the event, so a notice exists exactly when the thing it reports does (A12: a refused enrolment told nobody). It never throws — a notice that cannot be composed is logged and skipped. The events: enrolled (HR single and bulk, onto a session or its waiting list; an audience rule; a renewal); placed on or moved into a session; an employee facilitating a session told when it goes live, or when added to a live one; a live session moved (**saying where from** — the snapshot is taken before the edit), postponed, cancelled; the first completion; a certificate issued by HR. |
| **The outbox** | Five columns on `OrientationNotification` — the catalogue event, its tokens as they stood at the event, what the email did, attempts, the last attempt — and `OrientationNoticeEmailDispatcher` with a host every minute. Emails never go inside the request. `Sent`; `NoMailServer` and `NoAddress` final at once; `Failed` / `TimedOut` retried twice, five minutes apart; `Stale` for anything still queued three days on. Saved row by row; a timeout ends the pass. The host and HR's "Send queued emails now" share one lock. K-a's digests now record their own outcome too. |
| **Certificates** | A programme that issues certificates issues one **at the first completion** — the serial off the shared number sequence (the old highest-plus-one raced, and completions bunch at a deadline), the expiry from its validity, issued by nobody but the completion. HR's **Issue / Reissue certificate** on the enrolment list: refused for an unfinished enrolment and for a programme that issues none (neither was asked before), a second live certificate refused, a reissue marking the first `Reissued`; the issuer is the signed-in officer, and the request's `IssuedByEmployeeId` is ignored (D6). |
| **The switch** | `EnableReminders` now governs enrolment notices and all five orientation reminder kinds in K-a's sweep — exactly what its description says. Session changes, completions and certificates go either way. Register § 2.6. |
| **Screens** | A **Notices** tab on the Reminders screen (who was told what, the programme, the email's outcome; filters; the send button); **Issue certificate** / **Reissue certificate** on the enrolment list; the programme form's two switches say what they now do. |

**Decisions — each one this entry's to defend.**

| | Decided | Why |
|---|---|---|
| 1 | **An outbox, not sends inside the request** | D-2 sends in the request — right for a meeting's handful of invitees. Here one publish can enrol a population in a single save (lane I's seeded rule reached 1,135 people); that many sends inline would hold the request for minutes. |
| 2 | **The notice row is the outbox** | One notice is one message to one person, so its email's outcome belongs on it. A separate outbox table would be a second record of the same fact. |
| 3 | **`NoMailServer`, `NoAddress` and `Stale` are final** | The in-app notice was the delivery. Nobody wants last month's enrolment emailed the day a mail server is configured. |
| 4 | **A completion's certificate travels with the completion notice** | Issued at the same moment, one message says both. A certificate HR issues later has its own. |
| 5 | **A facilitator's notice carries no link** | The session page is HR's, and a facilitator often is not HR — a link that 403s is the company-schedule finding again. |
| 6 | **"Send reminders" governs what it says and no more** | "Enrolment, deadline and overdue notices". A session called off is not a reminder; suppressing it would leave somebody at a closed door. |

**How it is proved.** Each notice is read straight from the table — recipient, tenant, kind, words,
link, email state — and once as the person sees it in My Notifications (A7). Every one of the five
people on a session heard exactly what happened to them, in order (B23), and an edit that moves
nothing, or a draft session, tells nobody (B15, B16). Completion is driven by the participant's own
content tracking, and a second pass issues nothing (C8). The outbox block drains the tenant's queue to
`NoMailServer` before opening the local SMTP sink; the email **arrives**, in the notice's words, with a
link to that very enrolment (E6–E9); a bouncing address is tried three times and never reaches the
sink (E12, E13 — and the API log shows exactly three SMTP attempts per run).

**⚠ The suite's own reader lied first.** Run 1 was 75/84 — nine failures, every one a "—" or "–" read
back as "-". The table held the right characters (`CHARINDEX(NCHAR(8211), Message)` = 34): `sqlcmd`
writes to a console code page and best-fits what it cannot show. Loosening the expectations would
have stopped them proving the words, so the reader now returns text as the hex of its UTF-16 bytes.
**Any harness that compares non-ASCII text through `sqlcmd` has this blind spot.**

**Not exercised: renewal notices.** They go through the same `EnrolledAsync` with a different reason,
but only the tenant-wide sweep renews, and this suite stays out of it (lane I's lesson); lane I's
recurring fixtures keep "Send reminders" off. Nothing else here is claimed without a run.

**Neighbouring suites:** `run-round4-k` 55/55 twice — 53 plus two new (the digest records its own
outcome, C16 and D10b); its sink check now picks the digest by subject, because the dispatcher can put
an enrolment email in the same mailbox during its block D. `run-round4-i` 183/183; `run-round4-j`
94/94; `run-round4-m` 54/54; `hr-orientation/run.mjs` 107/107; `run-lane6-feedback` 20/20.
`hr-portal/run-slice11` (the unified feed) did not reach its assertions: its fixture supplied a staff
number the system now issues (fixed in `hr-portal/setup.mjs`), then approves a staff movement it
requested itself, which the movements separation-of-duties rule now refuses — stale since before this
lane, recorded rather than chased; A7 proves the feed carries the new notices. The API log: the 45
payroll-profile inserts (defect #23, one per employee created), the bounce's nine SMTP refusals, and
nothing from the new code.

**Frontend:** scoped type-check (`tsconfig.round4-lane-kb.json`, which already includes `src/app/me`
for K-b2) clean with a negative control; eslint clean. **Not browser-walked.**

### Lane K-b2 — onboarding tells people, and the page they are sent to · DONE 2026-09-23 · 54 assertions ×2

Harness: `dev-harness/hr-orientation/run-round4-kb2.mjs`. **No migration** — K-b1's outbox columns
carry these notices too. Lane K is complete.

**What was built.**

| | |
|---|---|
| **Notices** | On K-b1's staging and outbox. A plan made — by HR, or by the hire hook — **welcomes the new hire** (who coordinates, their buddy, how many tasks) and tells the **coordinator** (the plan on HR's screen: when it starts, its tasks, the first due) and the **buddy**. A plan changing hands tells only the NEW coordinator or buddy. A task added for somebody, or passed to somebody new, tells them — as their own when it is the new hire's. A task marked done that needs signing off tells the **coordinator at once**. Five new catalogue events, one per reader: `OnboardingWelcome`, `OnboardingCoordinatorAssigned`, `OnboardingBuddyAssigned`, `OnboardingTaskAssigned`, `OnboardingTaskDone`. |
| **My onboarding** (the user's call) | `MyOnboardingController` at `api/employee-portal/onboarding`: one read and **Mark done**, the employee always the caller from the token. The new hire sees their plan — coordinator, buddy, progress, and **who owes each task** (a person, else the post, else the coordinator); anyone sees the tasks given to them on anybody's plan (open, or done within 30 days) with Mark done — done, or waiting on the sign-off, with a note; a buddy sees whom they are helping. Narrower than HR's read: no comments, no evidence, nobody's notes but the reader's own. A portal page, a nav link and a home tile; the "My Orientations" tile's hint had promised "Onboarding checklists" it never held. |
| **The sweep's links** | K-a's reminder about a task given to somebody opens **My onboarding** — HR's queues refuse anybody without `HR.Orientation.Read`, so every assignee who is not HR had been sent to a 403. The coordinator's reminders, and every sign-off chase, keep the queues. |

**Decisions — each one this entry's to defend.**

| | Decided | Why |
|---|---|---|
| 1 | **Somebody else's task is "not found"** | The door never confirms a colleague's task exists — the house rule every self-service door follows. |
| 2 | **Mark done keeps HR's outcome** | A task needing sign-off waits for it (`PendingVerification`), exactly as HR's own complete does; only the rules a self-service door needs are added — the person it was given to, an open plan, once. |
| 3 | **The coordinator is told the moment a sign-off is waiting** | Otherwise the first they hear is K-a's chase, three days on — and the new hire's laptop sits unsigned-for. |
| 4 | **No evidence upload on Mark done** | Evidence files go through HR's upload gate; a note is what the portal takes. |
| 5 | **A task nobody was given shows the coordinator as its owner** | On the new hire's page, "nobody" reads as "nothing is happening"; the coordinator is who is answerable. |

**How it is proved.** Every notice is read from the table — recipient, kind, words, link — and every
reader's page as that reader: the assignee sees their task and nothing of anybody else's, the new hire
their plan with who owes what, the new buddy the new hire and the old buddy nobody. Mark done is driven
through the portal door as the assignee; the table then says done by them, today, with their note, and
the coordinator's notice says who and what they wrote. A colleague gets "not found", a second Mark done
and a cancelled plan's task are refused, and HR's own sign-off still works — by somebody else. Each of
the five new kinds is emailed through the local sink and **arrives**, the welcome opening the new
hire's own page.

**⚠ The suite's first run: 51/54 — its own window, again.** "This block's notices" were picked by a
two-second lookback, and the whole suite runs in eleven seconds, so the window caught the previous
block's notices (already settled with no mail server) and the arrival check compared the wrong
subjects. They are picked by exact ids now — a before-and-after set. A time window is not a fixture.

**Neighbouring suites:** `run-round4-kb` 84/84; `run-round4-k` 55/55; `run-round4-i` 184/184 twice
(183 plus H6d — a plan made on hire welcomes the new hire and tells the confirming officer, keyed to
that plan's own link because the officer is the same fixture on every run); `run-round4-j` 94/94;
`run-round4-m` 54/54; `hr-orientation/run.mjs` 107/107; `run-lane6-feedback` 20/20. The API log: the
27 payroll-profile inserts (defect #23, one per employee created) and nothing from the new code.

**Frontend:** scoped type-check (`tsconfig.round4-lane-kb.json`, now including `src/components/me`)
clean with a negative control in the new page's folder; eslint clean. **Not browser-walked** — and
this lane adds a portal page to the walk list: a new hire, an IT assignee and a buddy, each signing in
to see their own.

### Lane N — HR letter and email templates · DONE 2026-09-23 · 115 assertions ×2

Harness: `dev-harness/hr-templates/run-lane-n.mjs`, a new suite with a README alongside. **No
migration.**

**What was built.**

| | |
|---|---|
| **N1: the API** | `api/hr/letter-templates` serves the list at the collection root, which the plan called `/events`. It also serves one template with its tokens and shipped defaults, save, preview, test send and reset. Reading and previewing need HR Company **Read**; saving, resetting and a test send need **Write**, which the HR role holds (Admin would be a door HR cannot open). Its own business-rules filter makes an unknown template a 404 and a refused save a 422 that gives every reason. |
| **N3: the screen** | **Administration → HR Settings → Letter & Email Templates** (`/administration/hr/settings/letter-templates`), beside Company Profile and Policy Settings and on the HR Settings landing page. The plan said `/administration/hr/letter-templates`, but the settings group is where HR's tenant-wide configuration lives. The screen has: a grouped list with search and an Edited badge; a token palette that inserts at the cursor, in whichever field was last used (`{{{…}}}` only for ready-made HTML); a live preview with sample values in a sandboxed frame, half a second behind the typing, listing every problem a save would be refused for; and **Save**, **Undo changes**, **Start from the shipped wording**, **Send a test to me** and **Reset to shipped** (confirmed). `?t=Module/EventKey` opens one template directly. The screen says that in-app notifications and the platform's email designer are not edited here. |
| **N2: replaced** | See decision 1. |
| **N4: nothing to add** | All eight catalogues already existed: 44 templates, 32 emails and 12 printed documents. |
| **N5** | `HR-CONFIGURATION-REGISTER.md` § 2.7 lists all 44, each with what sends it, on which road, and its status. The harness checks it against the screen in both directions [A6–A8]. |

**Decisions — each one this entry's to defend.**

| | Decided | Why |
|---|---|---|
| 1 | **The list comes from the catalogues, and a row is stored only when a tenant edits. N2's startup seeding was not built** | The sender prefers a stored row, so a seeded copy would freeze its day's wording; lane K-b alone rewrote twelve defaults in one day. Reset sets the tenant's row aside, and the shipped wording goes out again. |
| 2 | **A save is checked; a send is not** | The renderer forgives everything at send time. An unknown token prints nothing, and an unclosed `{{#if}}` swallows the rest of the letter whenever its condition is empty. That is right when a candidate is waiting and wrong while an author is typing. A save is refused, with every reason, for a broken structure, a token the email does not supply, an empty or over-long subject, or `{{{…}}}` on anything but ready-made HTML. |
| 3 | **Raw placement is a catalogue mark (`IsHtml`), not "wherever the default places it"** | The first rule refused HR the offer letter's `ConditionsList` and the score sheet's `PanelTable`. Both are HTML on every render, but no default places them. Twelve tokens across six templates now carry the mark, and the harness pins them template by template [B7]. |
| 4 | **A test goes only to the officer's own address** | The address is the one on their employee record, or else their account's. The request cannot name a recipient [L6]. The result is an outcome, not a boolean: Sent, NoMailServer, NoAddress, Failed, or TimedOut after 10 s [D1, D4, L1, L7, L9]. |
| 5 | **Reset soft-deletes, and an edit after it revives the same row** | `(TenantId, Name)` is unique whether or not a row is deleted [I4–I5]. |

**Five defects found and fixed. The first two would have shipped with the screen.**

| | Found | Fixed |
|---|---|---|
| 1 | **The sender used ANY tenant's wording.** `TemplatedEmailService` took the first active row for the module and event. Its comment claimed the tenant filter scoped it, but that filter is inert on this context. One tenant's edited letter would have gone out under every tenant's name the day a second tenant existed. | The sender now uses its own tenant: the signed-in user's, or the one a background or anonymous sender names through the new `SendForTenantAsync` (the dispatcher, the K-a digest, the careers activation). When no tenant is known it uses the shipped wording, never another tenant's [F1–F4, J3]. |
| 2 | **An untouched seeded copy could never be recognised.** The first cut defined one as `IsSystemDefault && UpdatedAt == null`. But `SaveChanges` stamps `UpdatedAt` on every INSERT: on UAT's 11 stored rows it sits within a microsecond of `CreatedAt`. So every seeded row would have counted as the tenant's choice. This was found by reading the insert path before the harness ran. | "Edited" is now read from `UpdatedBy`, which only an edit writes [G1–G6]. The suite inserts its seeded row with `UpdatedAt = CreatedAt`, exactly as the insert does. |
| 3 | **Every background email said "Our Company".** The company name came from the signed-in user's profile. A background send has no user, so the lookup threw, and UAT's configuration has neither fallback key. | `ICompanyProfileProvider.GetForTenantAsync` now gives the sender's tenant's legal name [H6]. |
| 4 | **The careers activation email read "Activate your Default Tenant careers account".** The controller overrode the company name with the tenant record's label. | The override is removed, and the email uses the tenant's legal name [J3b]. |
| 5 | **"Application Received" greeted candidates by their email address.** The portal passed the address as the name; the sender survey found it. | It now uses the candidate's name [K3]. |

**How it is proved.** The suite covers every road an email takes, through a local SMTP sink, in both
positions:

- **Signed in:** HR's preview of an employee's letter shows the shipped wording, then HR's, then the
  shipped wording again [E]; a candidate's own application [K].
- **Background:** the orientation dispatcher [F–I].
- **Anonymous:** a careers registration [J].

It also proves that:

- another tenant's edit of the same email never goes out [F, J3];
- a seeded copy nobody touched is ignored, and an edit made in the platform designer is honoured [G];
- reset and revive both work [I];
- every shipped default passes every check, so HR can always save the shipped wording back [C1];
- each fault is reported word for word [C];
- the gates hold [D];
- a test send reports all five of its outcomes [D, L].

The suite refuses to start if the tenant already has wording of its own for any template it edits,
and it deletes every row it wrote outright.

**The sender survey: four behaviours outside this lane, recorded in register § 2.7 rather than
fixed here.**

- `ApplicationWithdrawn` is Unreachable.
- The older stage-move door sends "Under Review" after every move.
- A candidate who accepts an offer themselves gets no email.
- Company-schedule reminders go out only when HR presses the button, and the event form's own "Send
  reminders / days before" fields are read by nothing.

The user asked for all four to be fixed, so they are **lane N-b** (§ 4), next.

**Neighbouring suites, every one that sends or renders through the changed sender.**

- **Orientation:** K-a 55/55; K-b1 84/84; K-b2 54/54; I 184/184; J 94/94; M 54/54.
- **Recruitment:** B 96/96 (the talent-pool invitation); E 141/141 (the test invitation and paper);
  F 103/103 (the interview paper); G 41/41; H 36/36 (the offer letter's raw checklist);
  candidate country 43/43 and run-a 44/44 (registrations and portal applications).
- **Other documents:** the portal's HR letters (`hr-portal/run-slice12b`) 80/80; the probation
  confirmation letter (`hr-probation/run-d1`) 79 passed, none failed.
- **`hr-assets/run-slice5`** (the asset terms document) **died before its first assertion.** Its
  fixture still supplied a staff number, which the numbering register has refused since round 2b.
  The fixture is fixed (harness-only). Now **46/49**, and the three failures are older than this lane:
  - two expect the refusal to say "employee concerned" and get the global handler's generic 403 text,
    which is master's 403 masking, already recorded;
  - its own cleanup DELETEs are refused with 403.

  Every assertion about the rendered document passes.

**The API log, classified.** None of it comes from this lane's code:

- 111 failed saves, every one the payroll-profile foreign key (defect #23), one per employee the
  suites created.
- About 6,200 "Error processing notification" lines. The unified notification processor tries to
  email every pending notification, UAT has no mail server, and it logs one error per notification
  per pass. It runs at the same rate in every earlier lane's log: 30,419 in K-b2's.
- Four "responded 500" request-log lines, which are not 500s. They are the asset suite's 403 and 404
  refusals. The global exception middleware answered them, and the request logger sits inside that
  middleware, so it saw the exception pass and recorded 500.

⚠ An error count of zero was first reported here from a grep for `[ERR]`. That pattern can never
match, because this log writes `[17:31:58 ERR]`. Grep for ` ERR]`.

**Frontend:** scoped type-check (`tsconfig.round4-lane-n.json`) clean, with a negative control in the
new page; eslint clean. **Not browser-walked.** The template editor is walk 3 in § 5.

### Lane N-b1 — the recruitment senders, and the offer letter attached · DONE 2026-09-23 · 32 assertions ×2

Harness: `dev-harness/hr-templates/run-lane-nb1.mjs`, through the local SMTP sink. **No migration.**

**What was built.**

| | |
|---|---|
| **Nb5: the offer letter, attached** (the user's call) | The Offer Issued email carries the letter as `Offer letter <number>.pdf`. `IHtmlToPdfRenderer`, implemented in the API project as `HtmlToPdfRenderer`, imports the HTML into a Syncfusion Word document and renders that to PDF, using the engine the document-management renditions already use. The email's new `{{#if LetterAttached}}` sentence appears only when the attachment really went. A letter that cannot be rendered or converted costs the attachment, never the email. |
| **Nb2: Under Review, once** | It moved into the pipeline service, which both stage-move doors go through. It is sent on the **first move a person makes into a review stage**. The older door sent it after every move: into an interview, into an offer, beside Assessment Pending. The board's door never sent it. |
| **Nb1: Application Withdrawn, reachable** | It is sent from HR's withdrawal on a candidate's behalf, which the employee applicant's own door also goes through, and from the careers portal's. Its only sender had been a token-link withdrawal that nothing called. The portal's withdrawal also **closes the application's open pipeline stage**, as HR's always did. The portal's left the application sitting in its stage. |
| **Nb3: Offer Accepted, however it arrives** | It is sent for HR's record, the candidate's emailed link and the portal; before, only HR's record sent it. The link is anonymous, so the send names the offer's tenant (lane N's `SendForTenantAsync`). Otherwise the tenant's wording would be skipped and the company name would fall back to configuration. |

**Decisions.**

| | Decided | Why |
|---|---|---|
| 1 | **"First move into review" is marked by the stage history's mover, not by the application leaving Submitted** | A submission is placed in the pipeline's first stage at once, and that is usually a review stage, so the application is "under review" before anyone has looked at it. The placement row records no mover; a person's move always does. So the rule needs no new column. |
| 2 | **The letter is attached as a PDF, not as HTML** | An `.html` attachment is what phishing uses, and mail filters treat it so. PDF is what a candidate expects to keep and print. |
| 3 | **The email never promises an attachment it does not carry** | The sentence is a token switched on by the attachment itself, so a failed conversion degrades to the pre-N-b1 email, not to a false claim. |
| 4 | **HR's withdrawal on a candidate's behalf tells the candidate too** | It is still their application, and the shipped wording ("has been withdrawn") reads correctly whoever withdrew it. |

**⚠ The first PDF looked right to the suite and wrong to a reader.** The first build passed 30/30: a
real PDF, the right size, the email saying so. Opened by hand, it showed the letter with **both its
tables empty**: the position-and-terms table, and the salary table. The rows' height survived, the
columns collapsed to nothing, and the text went with them. The cause was found **without a rebuild**,
through lane N's own screen. Eight variants of the letter were saved as the tenant's wording, an offer
was issued against each, and each attached PDF was read. Converting `rem` to `px` alone restored the
tables: Syncfusion's HTML import does not know the unit, and every table in HR's letters is styled in
it. The renderer now converts it. The suite now looks inside the PDF for the employment type and the
salary; the cells print in a standard font, so their words are literal in the page streams [E4b–E4c].
**Negative control:** against the build without the fix, exactly those two assertions failed. A
harness can prove a file is a PDF, but not that a person can read it, so the suite saves the last
PDF as `offer-letter-from-last-run.pdf` for a person to open.

**How it is proved.**

- **Under Review:** exactly one per application, through each door. None for an application moved
  straight into an interview, and none beside Assessment Pending [B, C].
- **Application Withdrawn:** exactly one per withdrawal, through all three doors. The portal
  application's stage is closed as Withdrawn [D].
- **Offer Issued:** the PDF is real (header, page, end marker, a letter's size, both tables' words),
  and the email says so. HR's own letter wording, badly nested, costs the attachment but not the
  email, and that email then promises nothing [E].
- **Offer Accepted:** exactly one per acceptance, however it arrives. By the link, it carries the
  tenant's own wording. A decline sends none [F].

**Found on the way, not a product defect:** publishing a vacancy through `POST …/transition` with a
partial payload **cleared its pipeline** as well as its advert title. This is the recorded "null means
clear it" behaviour of that DTO; the UI sends the full edit form, so its screens keep both. The
suite's first run died on it, and the suite now publishes with `change-status`.

**Neighbouring suites.** Every suite that moves, withdraws, offers or applies was run, along with
lane N's, which checks the new `LetterAttached` token against the register. All are at baseline:

| Suite | Result |
|---|---|
| `hr-templates/run-lane-n` | 115/115 |
| `slice-b` | 174/176 |
| `slice-c` | 188/190 |
| `slice-e` | 101/105 |
| `slice-f` | 69/69 |
| lanes A, B, C, D | 45, 96, 46, 58 |
| lanes E, E6, G, H | 141, 86, 41, 36 |
| `run-a`, `run-c1`, `run-c2` | 44, 96, 89 |
| `run-k`, `run-v` | 81, 72 |
| candidate country | 43 |
| `run-lane5b` | 32/34 |

Every failure among them is one already named in § 9.2. The API log holds only the known noise:
- the notification processor with no mail server;
- one payroll-profile failure (defect #23) per employee the suites made;
- one timed-out clean-up of expired notifications, which belongs to that processor.

### Lane N-b2 — company-schedule reminders that send themselves · DONE 2026-09-23 · 36 assertions ×2

Harness: `dev-harness/hr-templates/run-lane-nb2.mjs`, through the local SMTP sink. Migration
`20260923200051_AddCompanyScheduleReminderSweep`, applied to UAT. It is guarded SQL; the scaffold's
`defaultValue: 0` plus an `UpdateData` against one hard-coded seed-row id became `NOT NULL DEFAULT
(2)`, which fills every tenant's row. It was proved twice up and twice down on a scratch database with
two settings rows, and verified on UAT (history row, both columns, the named default, the value 2).

**What was built.**

| | |
|---|---|
| **The sweep** | `ICompanyEventService.SendDueRemindersAsync(tenantId, now)` runs hourly in `CompanyScheduleReminderBackgroundService`, which is registered in the same change; its start line is in the log. HR's `POST api/CompanySchedule/reminders/run` runs the same code now. It sends each live event's reminder `ReminderDaysBefore` before it, where **Send reminders** is on, to everybody who has not declined. It chases unanswered invitations `CompanyEventRsvpChaseLeadDays` before the RSVP deadline, to nobody who has answered. Each is sent **once** and stamped on the event, and each event is saved as it is sent, so a failed pass costs only what it had not sent. It is tenant-explicit throughout (`SendForTenantAsync`, lane N's lesson), so the sweep's email carries the tenant's wording and legal name. |
| **Which events** | Only live ones: scheduled, confirmed or rescheduled, not cancelled, and approved where approval is required. A postponed event has no date to be reminded of. |
| **Moved dates** | A reschedule clears the reminder stamp. An edit clears whichever stamp its changed date invalidates, the start or the RSVP deadline. |
| **The Reminders card** (event page) | The manual reminder and RSVP-chase endpoints, from round 4's D6, **had no control on any screen**. Their service methods sat on the personal-schedule service, called by nothing. They are now **Send reminder now** and **Chase unanswered now** on a Reminders card. The card says what went and when, or when it will, and pressing a button counts as the send. |
| **The setting** | `CompanyHrPolicySettings.CompanyEventRsvpChaseLeadDays`, 2 by default, is on the HR policy page's new **Company schedule reminders** card. The event form's **Send reminders** switch now says what it does. Register § 2.8. |

**Decisions.**

| | Decided | Why |
|---|---|---|
| 1 | **Hourly, where lane K's sweep is daily** | An event created this morning for tomorrow, reminding a day before, is due today. A daily pass could fall before the event existed and come round again after it. The stamps make cadence a matter of latency, never of duplicates. |
| 2 | **The RSVP-chase lead is a tenant setting, not a field per event** | The form already asks for a deadline. A second date to reason about per event is one more thing for an organiser to get wrong. |
| 3 | **A manual send counts as the send** | Otherwise a person HR reminded yesterday is reminded again by the sweep today. Pressing the button again still sends, because HR asked. |
| 4 | **A reminder attempted with no mail server is stamped** | Consistent with the K-b1 outbox's `NoMailServer` being final. The sweep does not hold reminders back waiting for a mail server to be configured. |
| 5 | **The sweep's reach was measured before the suite was written** | UAT's demo events: none due for a reminder, and none with an RSVP deadline ahead. The suite re-measures before its first run (at the widest lead it sets) and refuses to run if any demo event would be swept. It cancels every fixture event at the end, so the hourly host never reminds anyone about one. After both runs, no demo event carries a stamp. |

**How it is proved.** Every setting is tested in both positions, with the observed outcome stated
(register § 2.8). The suite also proves:
- send-once, down to "not one email" on a second pass;
- never for a cancelled or unapproved event, and at once on approval;
- the manual send counted as the send;
- both kinds of moved date reminded again;
- the tenant's wording under its legal name;
- the run-now door refusing an employee and asking a stranger to sign in.

The first run stopped at the approval step. The suite had approved as admin, and approving needs a
user linked to an employee, so the fixture now uses a second HR officer.

**Neighbouring suites.** The company-schedule suites, whose event, participant, reminder,
reschedule and edit paths all changed, are all at baseline:

| Suite | Result |
|---|---|
| `hr-company-schedule` slices 0–3 | 24, 32, 62, 44 (the area's 162) |
| lane D-2 | 39 |
| lane K-a | 55 (it saves the policy settings) |
| lane N | 115 |

**Two leave suites failed. Neither failure is this lane's; both are older harness problems.**

- **`hr-leave/run-slice13-leaveyear` (10/14).** Its first block passed, including *saving the
  settings without moving the leave year*, which exercises the new field. Its second block moves the
  leave year **by SQL**, and the hr-leave SQL helper is hard-wired to `ErpSystemDB` while the API runs
  on `ErpSystemDB_UAT`. The move landed in the other database: the plan read back January's answer,
  and the stored year read back empty. The suite's own `exit` handler put the development database's
  month back to January.
- **`hr-leave/run-slice6-settings`.** It saved and restored the settings, then died raising a leave
  request that its long-lived fixture employee already holds for that period, which is litter from
  earlier runs.

Both suites need their SQL target made configurable before they can prove anything on UAT; that is
recorded here, not fixed. The API log holds only the known noise: the notification processor, and
42 failed saves, every one the payroll-profile foreign key (defect #23). Nothing was logged from the
sweep.

**Frontend:** scoped type-check (`tsconfig.round4-lane-nb2.json`) clean, with a negative control in
the event page; eslint clean. **Not browser-walked**: the Reminders card and the policy page's new
card join the walk list.

### Lane O — the technician-role flag, and the Maintenance door it feeds · DONE 2026-09-23 · 101 assertions ×2

Harness: `dev-harness/hr-jobarch/run-round4-o.mjs`. Migration `20260923223409_AddTechnicianRoleFlag`,
applied to UAT. It is guarded SQL: two `bit NOT NULL` columns with named `DEFAULT (0)`s, and a
value-keyed backfill in its own batch. It was proved twice up and twice down on two scratch databases:
- a migrated shape, with a fixture for each backfill rule;
- a model-built shape, with the columns already present and a post already flagged.

On UAT it was verified directly: the history row, both columns, both named defaults, and 0 flagged, 0
set by hand, 0 drift.

**What the survey found first — and why the plan's O2 formula could not stand.** The plan's single
predicate was `position.IsTechnicianRole || employee.CanBeAssignedToMaintenance`, computed on read.
The survey found **five Maintenance gates reading the stored column directly**:
- `WorkOrderService` (assign a technician);
- `WorkOrderLaborService` (log labour);
- `MaintenanceStaffScheduleService`, create and update;
- `QualityControlService` (inspection officers).

A computed rule would have let HR's door offer an artisan whom the work-order assignment then
refused as "not qualified for maintenance assignments". So the one predicate is **the stored
column**, and HR keeps it: it follows the position's flag unless HR set it by hand. And the column
had **no writer at all**. Measured on UAT before the lane:
- 0 of 2,089 employees held it;
- HR's door listed nobody;
- Maintenance would have refused every assignment.

The Maintenance module is empty on UAT (0 work orders, 0 schedules), which is why nothing had shown it.

**What was built.**

| | |
|---|---|
| **The rule — `EmployeePosition.IsTechnicianRole`** | "Technician role" on the position form, beside Requires License, with a line saying what it does. The positions list has a badge and a Technician-roles filter. The update DTO takes it as `bool?`, so an update that omits it leaves it alone: turning it off moves every holder out of the pool, which must never happen by omission, and scenario 006 omits it. |
| **The exception — `Employee.MaintenanceAssignmentSetByHand`** | The employee form's new **Maintenance** section: *Follow the position* / *Include* / *Exclude*, then the trade, certification level and experience level. Under the choice it says what "follow the position" answers today, for the position chosen above, live: *"Available: Artisan is a technician role…"*. |
| **One writer — at save time** | `ApplicationDbContext.HrTechnicianRole.cs`, called in both save paths before the audit pass. (1) A write of the column that did not come from the rule becomes a by-hand exception. (2) Every employee saved and not set by hand takes their post's flag. (3) Every position saved takes its holders not set by hand with it, in the same save, audit-stamped. It asks "does the row agree with its post?", never "what changed?" |
| **The door** | One predicate, the column. `Department.Code == "MAINT"` is retired; the migration first made anyone it matched a by-hand inclusion, so it dropped nobody. One include set. **What HR knows**: first and last names, position and its flag, why the person is in the pool, organisation unit (the department), location (the gates match it to the asset), trade, both levels, status, and skills — id, skill id, proficiency, the row's real `IsCertified`, and certification date, expiry, number and body, where a linked credential's win. **What HR does not know** — work orders, workload, shift — is **null**, where it was a hardcoded 0 that read "free". |
| **"Available" (the user's call, 2026-09-23)** | Active **or on probation**. It was Active only, and every hire starts on probation, so a new artisan read "unavailable: Probation" in the pool that listed them for three to six months, and Maintenance's resource allocation could not propose them. Suspended, inactive and terminated staff stay out. |
| **O3 — the dropdown that ships** | `maintenanceDataService.getTechnicians()`, behind the work-order, emergency, scheduled and job-card screens, now calls HR's door. `maintenanceApiService.getTechnicians()`, the copy with the every-employee fallback, is removed; it had no caller. The root `GET api/Employees/maintenance-available` is **retired** (Q1b). |
| **The HR employee list** | A *Maintenance technicians* filter, the first caller `EmployeeSearchDto.MaintenanceTechniciansOnly` ever had, and a *Technician* badge on each row. |
| **Seeds and demo** | `TdcOrganogramSeeder` flags TDC's three maintenance trades: `DV-BMS` (Building Maintenance Supervisor), `DV-ART` (Artisan) and `MS-CT` (Computer Technician). `EmployeePositionDataSeeder` flags `POS-HVAC`. New scenario **`007-maintenance-technicians`** does the same on UAT: Grace Danquah follows her post, and Kwabena Owusu (Facilities Officer) is included by hand. |

**Decisions.**

| | Decided | Why |
|---|---|---|
| 1 | **The one predicate is the stored column, maintained, not a computed rule** | Maintenance's five gates read it. |
| 2 | **Enforced at save time in the DbContext, not in each service** | A person changes post through at least six paths: HR's create and edit, a hire from an offer, a movement and its reversal, the import, and the seeders. A rule each path had to remember to call is how the question came to have four answers. The inventory cost projection is the precedent. |
| 3 | **The exception works both ways** | The plan's OR could only add people; Q1's own rationale names both cases, a secondee and a technician on long-term light duties. |
| 4 | **A write from outside the rule counts as by hand** | Another module's decision then sticks, and HR sees it on the form. Undoing it on the next save would be a silent no-op for them. |
| 5 | **The workload columns are neither surfaced nor filled** | Nothing writes `CurrentWorkload`. `MaxWorkload` is Maintenance's own, and its only writer is unreachable. |
| 6 | **`ExperienceLevel` held to the four words the entity has always documented** | Maintenance's list filters and groups on the text. |
| 7 | **`ShiftName` left null, although HR holds shift assignments** (40 on UAT) | The plan's scope. Offered as a follow-up, with leave-aware availability. |
| 8 | **Nothing added to `EmployeePositionLookupDto`** | It is constructed by nothing. A field on it would be a field populated by nothing. |

**What the plan got wrong, measured before building:**
- **Defect 21 was wrong on both halves.** `CurrentWorkload` is never written, but Maintenance *reads*
  it, and its technician screen therefore reports everyone free. `MaxWorkload` is Maintenance's.
  Register § 2.9.
- **O2 listed four predicate sites, and missed all five gates above.** Those gates read the stored
  column, and that is the fact that decided the design. O2's fourth row, Maintenance writing HR's
  column, is real code with **no controller**.
- **O3's every-employee fallback lived in a method no screen calls.** The shipped dropdown had none.
  The root endpoint it called is what returned full employee records.
- **The harness spec's "a unit merely named Maintenance"** — HR's door never read a unit name; it read
  a department *code*. Both are now asserted [F1–F5].

**Found on the way, fixed:**
- **Lane H's check-set picker never saved.** It was on the position form, and neither page sent it.
  The update applies the field unconditionally, so **every save from the edit page cleared the post's
  check set**. Eleven posts had one, all lane H fixtures. It was also a latent type error that the
  crashing full type-check never reported.
- **The door's list reads never loaded skills**, so `technicians` and `technicians/available` sent
  every technician with an empty skills list.
- **`IsCertified` was `CertificationDate.HasValue`**, not the row's flag.
- **A removed skill row still qualified its holder** in the skill door. The query now also runs in
  SQL, where it loaded every holder of the skill.
- **The employee list's empty state** said "No employees yet" under any filter.

**How it is proved** — `run-round4-o.mjs`, 101 ×2. Blocks:
- **A** — the flag round-trips, and an update that omits it leaves it.
- **B** — a holder is in the door untouched.
- **C** — the post takes its people with it both ways, in the same save, audit-stamped.
- **D** — moving in and out of a technician post.
- **E** — Include and Exclude both stick, through moves and through the post switched off and on;
  Follow the position hands the answer back.
- **F** — a `MAINT` department and a "…Maintenance…" unit each admit nobody. Maintenance's own list
  still admits by unit name; this is printed, not asserted.
- **G** — the whole projection, including a lapsed certification, a dateless certified skill, a linked
  credential's dates, and null for the three unknowns.
- **H** — the list filter returns exactly the door's answer.
- **I** — the root endpoint is gone.
- **J** — Maintenance's resource allocation, reading the door in-process, agrees.
- **K** — HR may write, an employee may not, and the door is open to Maintenance staff.
- **M** — "available" in both positions: a probationer yes, a deactivated technician no, saying why.
- **L** — afterwards: no employee anywhere in the tenant disagrees with their post; the suite leaves
  none of its fixtures in the pool; the real pool is exactly as it was.

The first run was 92/98, and all six failures were probationers read as unavailable. That became the
user's call above. Fixtures are "ZZZ R4O" posts and "R4O Tech…" people. Each run switches its posts off,
deactivates them so the organogram does not draw them, and deactivates its people.

**Neighbouring suites** — chosen by code path: everything that saves a position, the hire and movement
paths the save rule now runs on, and offers read from a position.

| Suite | Result | Baseline |
|---|---|---|
| `hr-jobarch/run-c1` · `c2` · `c3` · `c3b` (position requirements, all four PUT positions) | 36 · 143 · 74 · 33 | same ✅ |
| `hr-recruitment/run-round4-h` · `run-round4-g` (offers from a post) | 36 · 41 | same ✅ |
| `hr-movements/run-h` (movement implement and temporary return) | **32/33** | 33 — see below |
| `hr-orientation/run-round4-i` (a hire from an offer, an implemented movement — both through the save rule) | 184 | same ✅ |
| `hr-employee-import/run-smoke` · `run-update` | **69/76 · 48/52** | stale — see below |

⚠ **`run-h` 32/33 is not this lane's.** Its promotion's implement and its temporary assignment's
return passed, and those are the two movement paths that write a position. The missing assertion is
its last transfer's submit, refused by the establishment gate. The target is `positionList[0]`,
**Accounts Officer**, established for 1 on 2026-01-01, holding **1,077** active employees, **441 of
them added today** by the suites' own actors. It is § 9.1's lesson, now in `hr-movements/setup.mjs`,
which needs the same "require headroom" fix. Recorded, not fixed.

⚠ **The two import suites are stale, and have one cause.** They were written on 2026-09-03, before
UAT's Permanent register took the `TDC/00001` format. Every fixture row's staff number (`EIMP…-001`)
now draws the warning *"does not match the Permanent register's format … accepted as given"*.
Read from the session row, that warning is R1's **only** finding on first upload. So rows expected
Ready read Warning, commits read "with issues", and the counts shift by exactly those rows. Nothing
in any finding touches positions or maintenance. Recorded, not fixed.

**The demo, after the suites.** Scenario 007 marked the three posts, 3/3, and wrote both profiles.
Its second run wrote nothing, so the guards hold. The tenant now has exactly **two** technicians:
- Grace Danquah, by her post, *Building maintenance*, Level 3, Senior;
- Kwabena Owusu, by hand, *Plumbing and general repairs*, Level 2, Intermediate.

Drift is 0. The suite was then run a third time, green, with that real pool of two present, and its
L3 check confirmed it left them untouched.

**The API log** holds only the known noise: 2,650 notification-processor lines (no SMTP), and 46
failed saves, every one an `INSERT INTO [PayrollEmployeeProfiles]` refused by the payment-method
foreign key (defect #23), one per employee the suites' actors and hires created. This suite's own
fixtures are off payroll. Nothing came from the technician rule.

**The hand-off** — cross-module defect **#29**:
- Maintenance's own technician list still decides by `OrganizationUnit.Name.Contains("Maintenance")`,
  at nine sites.
- Its availability reads the workload nothing writes.
- Its sync reads an HR stub that returns nothing.
- Its create/update-technician methods write HR's row, and nothing calls them.
- It resolves past schedules' names through the technician door, which now answers only for current
  technicians.

It also records the two Maintenance frontend files HR changed for O3. Integration map rows 4 and 5
are corrected: the sync is 🔴, not "unverified". Register § 2.9 holds the flag, the exception, the
vocabulary, the "available" rule in both positions, and the two workload columns under the
register's own rule.

**Frontend:** the scoped type-check (`tsconfig.round4-lane-o.json`) is clean. Its negative control
passed: three probes, in a types glob and a page glob, and all three were caught. Eslint is clean
apart from one pre-existing warning. **Not browser-walked** — walk 6 in § 5.

### P2a — the recruitment guide, rewritten for round 4 · DONE 2026-09-23 · docs; its two demo findings repaired in the data 2026-09-24

`docs/HR/areas/recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md`, written from the code, this log and
`ErpSystemDB_UAT`. It keeps the guide's layers: what it is, on the page, who can use it, behind the
page, known gaps.

| Chapter | Now covers |
|---|---|
| § 5.8 scoring | The engine as it stands: no score versus 0 versus a score; what is left out when it cannot be evaluated; how each kind of criterion answers; Location on the geography tree, with the deliberate "no area is a miss" rule; the snapshot; the blend's input |
| **8A** *(new)* | Recruitment tests: writing a paper, giving it, sitting it online, marking, paper sittings, the demo, and the endpoints and enums |
| § 9 interviews | Apportioned slots; the clash check binding across seven sources, with its override; suggest-slots and room booking; blind scoring; the panelist's worklist; the printed paper |
| § 10 offers | Each default and where it comes from; the 14 statuses and the nightly expiry; what issuing does, the PDF included; acceptance emails from all three paths; the letter's checklist headings as the template writes them |
| § 13 talent pool | The five filters; the Screen tab (new § 13.5); the three-tier fit score (§ 13.6). The gaps move to § 13.7, and nothing outside the guide cited the old numbers |

Also updated: the guide's status block; its "How to read" table; § 1.3's screen table, which gains an
Assessments row outside the 32 walked; and § 8.4's tiles, where the panel tile is now "Shortlisting
panel score", as G-9.5 relabelled it on screen.

**The findings.** The rewrite recorded 16, marked `R4-…` above each chapter's closed history, and
fixed none. The data repair on 2026-09-24, below, fixed the two demo blockers and added R4-5.2 and
R4-5.3, making 18.

**Two blocked the demo, both in the data.** Both were re-measured on UAT before staging:
- **R4-5.1: the test blend cannot be shown.** VAC-000021 carries the seeded paper at 30%. Its three
  marked candidates scored 19/20, 10/20 and 9/20, and each has an auto score of **0** and **no
  qualification on file**. The mandatory *A relevant first degree* fails at step 3, so the blend at
  step 5 never runs.
- **R4-10.1: no demo offer gets a checklist.** UAT holds **44** active check templates: TDC's own,
  12 *"R4H Standard checks …"* from lane H's suite, and 31 *"E2E RecD Template …"* from slice-d. The
  fallback wants exactly one, so it seeds nothing. The 16 posts that name a template are all
  fixtures. So no demo offer prints a checklist, and *Accept conditionally* is refused.

**One more in the data.** R4-10.2: 21 of UAT's 28 `Sent` offers have no expiry date, so the sweep
never expires them.

**Eight in the code**, none blocking:
- R4-8A.1: an untimed online attempt blocks a paper sitting, with no cancel action.
- R4-8A.4: the "already sat" refusal points at a copy action tests do not have.
- R4-9.1: a reschedule drops the candidates who no longer fit, and only the log says so.
- R4-9.2 and R4-10.4: the interview confirmation and offer-response tokens are stored in clear.
- R4-10.3: the leave default comes from the leave type, and `NdaRequired` has no source.
- R4-13.1: publishing through the API without restating the title erases it.
- R4-13.2: booking advances the pipeline best effort, and only the log shows a refusal.

**Four are walks not yet done**: R4-8A.2, R4-9.3, R4-10.5 and R4-13.3. Only the first, and part of
R4-9.3, match walks in § 5 (walks 2 and 1). The offer form and the talent pool's Screen tab were
proved by harness only. R4-8A.3 points at R4-5.1.

**Repaired in the data on 2026-09-24 (the user's call: a first degree for each, not a non-mandatory
criterion).**

*R4-5.1.* Scenario 052 gained step 3b, which records a first degree for Comfort Asiedu, Ishmael
Tetteh-Okine and Gifty Mensah, and step 6b, which re-scores those three one at a time. It does not
use *score-all*, which would also have scored Ebenezer Okyere and Elikem Attipoe.
- **Guards:** before the run, every guard in 052 was proved to answer correctly on UAT: one paper
  among 69, one assignment, the weight already 30, Elikem already on the vacancy, all three scripts
  entered.
- **Run 1:** qualifications went from 234 to 237 and nothing else moved. The scores went from 0 to
  exactly **81, 67.5 and 66**. In each breakdown the degree now passes (40), experience passes (35)
  and report writing fails (0 of 25); 75 on the criteria, blended at 30% with 95%, 50% and 45%.
- **Run 2:** wrote nothing. The state, timestamps included, is identical.

*The repair found two more, both recorded in the guide and not fixed:*
- **R4-5.2: the criterion cannot recognise a first degree.** `TdcDemoLivePipelineBackfillSeeder`
  writes *A relevant first degree* as the literal word "Degree", matched by containment.
  - Only a qualification whose name contains that word passes. Of the qualification catalogue's 186
    entries only *Associate Degree* does, and before the repair none of the tenant's 234 candidate
    qualifications did.
  - So the degrees had to be worded *"Bachelor's degree in …"*.
  - The criterion is mandatory on six live-pipeline vacancies, VAC-000019 to 000024.
  - Elikem Attipoe's live sitting will still score 0, because a *BSc Quantity Surveying* does not
    contain the word.
  - The root cause is in the engine: no criterion type compares qualification *levels*.
- **R4-5.3: a rebuild leaves the blend unscored.** Scenario 052 scores before the second seeder pass
  gives the vacancy its criteria. A second run of 052, or *Score all*, fixes it.

*R4-10.1.* The tidy had four steps:
1. **Backup.** The 43 fixture templates were saved to
   `dev-harness/hr-recruitment/fixture-check-templates-retired-2026-09-24.csv`: 12 from lane H, 31
   from slice-d's fixture, and 16 fixture posts naming them.
2. **Switch-off.** One transaction switched them off. It would have rolled back unless it matched
   exactly 43 rows, and they are marked `UpdatedBy = round4-P2a-fixture-tidy-2026-09-24`.
3. **Check.** TDC's *Standard Pre-Employment Checks* is now the only active template.
4. **Suites.** Every script that made one now switches its own off:
   - lane H's suite, with an asserted switch-off, so it counts **37** from now on (36 + 1);
   - slice-d's `run.mjs` and `verify-benefits.mjs`;
   - `run-g`;
   - lane I's suite, which builds slice-d's fixture in its block H.

   Each switches off at the end, and again on a fatal error, since Node hands a failed top-level
   `await` to an `uncaughtException` handler and waits for the PUT. slice-d's `build()` and lane H's
   suite also sweep up their own leftovers first, for a run killed outright.

⚠ **The suites could not be the proof of the tidy.** `run-g` still dies at § 9.3 #1's wall, and in
the first batch it did so before its switch-off existed on the fatal path. That left one fixture
template active while lane G, N-b1, lane O and W3 slice 9 ran, so those runs said nothing about the
single-template path. They were re-run with exactly one active template.

| Suite | Batch 1 (a leftover active from run-g onward) | Batch 2 (one active) | Baseline |
|---|---|---|---|
| lane H `run-round4-h` ×2 | **37/37** · **37/37** | — | 36, and 37 with the tidy assertion ✅ |
| slice-d `run.mjs` ×2 | stops at 75/98 · 75/98 | — | stops at 75/98 on § 9.3 #1 ✅ |
| slice-d `verify-benefits.mjs` | **23/23** | — | not recorded before |
| `run-g` | stops at the § 9.3 #1 422, template left **active** | stops at the same line, template **switched off** on the way out | stops at § 9.3 #1 ✅ |
| lane G `run-round4-g` | 41/41 | **41/41** | 41 ✅ |
| N-b1 `run-lane-nb1` | 32/32 | **32/32** | 32 ✅ |
| lane O `run-round4-o` | 101/101 | **101/101** | 101 ✅ |
| lane I `run-round4-i` | **184/184**, sweeping run-g's leftover | — | 184 ✅ |
| W3 `run-slice9-recruitment` | cannot run on UAT: its `w3.*` users do not exist there (login 401) | — | — |

After every run in batch 2, TDC's was the only active template. **The proof is in the offers, not
the counts.** Every offer that batch 2 raised on a post naming no template now starts from TDC's
five-item checklist: lane G's OFR-000178, and N-b1's OFR-000179 to 000183. The one exception is
run-g's OFR-000177. It was raised while run-g's own fixture template was also active, and with two
active the fallback correctly seeds nothing.

**The API log** gained about 4,000 error lines across both batches, and none touches templates,
qualifications or scoring:
- the notification processor, as always;
- 68 refused `PayrollEmployeeProfiles` inserts (defect #23), one per fixture employee;
- three bulk updates of `Notifications` that failed while N-b1's SMTP sink was up.

**The four live demo offers raised before the tidy.** The checklist is seeded when an offer is
*created*, so these had none: OFR-000013 (Draft), OFR-000015 (Sent), OFR-000016 (Negotiating) and
OFR-000017 (Sent).
- **Repaired, the user's call.** `dev-harness/hr-recruitment/repair-demo-offer-checklists.mjs`,
  working as the HR Head through the real doors, gave each one check set with the note a seeded set
  carries, and TDC's five items.
- **Verified.** Sets went from 52 to 56 and items from 166 to 186. The offers' own status and
  timestamps are untouched, and a second run wrote nothing. OFR-000017's letter now prints
  *Conditions Precedent* with the five checks.
- ⚠ **Not done, awaiting the user's confirmation.** OFR-000017 is Rita Amponsah's, the offer
  scenario 050 meant to be *Conditionally Accepted*. That acceptance was refused for want of a
  checklist, and would now succeed. Recording it is a separate status change.

A rebuilt database has no fixtures, so there the fallback works from the first offer.

**R4-5.3, fixed the same day (the user's call).** `scripts/Invoke-UatDemoScenarios.ps1` runs scenario
052 once more after the second seeder pass (its new step 4b). The script parses; no full rebuild has
exercised it yet. **R4-5.2 is fixed properly by lane Q** (§ 4), the user's choice over a cheaper
repair.

**A lesson from writing it.** Three of my draft claims were wrong and were corrected against the code
before staging:
- what can still be done to a paper once somebody has sat it;
- the `ScoreSource` names;
- the letter's headings, which are *"Pre-employment Requirements"* and *"Conditions Precedent"*.

Each had come from a log's paraphrase, not the source. Read the template, not the account of it.

⚠ The rest of the guide still describes the 2026-09-15 walk, and none of round 4's screens has been
walked in a browser.

---

## 9. What the neighbouring suites actually said

### 9.1 Three harness defects, two of them expensive

**The `SQL` helper wrote to the wrong database.** `slice-f/setup.mjs` hardcoded
`-d ErpSystemDB` while the API ran against `ErpSystemDB_UAT`. The suites activate their test
accounts with a direct `UPDATE Users SET IsActive = 1`; that statement matched **zero rows** in a
database that did not contain the user, sqlcmd exited 0, and the next login returned
`IDENTITY_USER_INACTIVE` — a message pointing convincingly at account activation rather than at the
fixture writing to the wrong place. Five suites were blocked by this and were initially misreported
as blocked by the product defect above.

Fixed: the helper follows the API (`HARNESS_DB`, defaulting to `ErpSystemDB_UAT`), and a new
`SQL_MUST_AFFECT` makes an UPDATE whose purpose is to change a row **fail loudly when it matches
nothing**, naming the wrong-database cause.

⚠ **The harness was masking the very defect it appeared to cover.** Those suites prove careers
registration works by activating the account in SQL — a statement no real candidate has. They would
have gone on passing while registration was unusable in production.

**`setup.mjs` chose the worst possible position.** It took the first position carrying an
organisation unit — "Accounts Officer", established for **one** post with **178 filled**. Each run
mints five more employees onto it, so the establishment gate eventually refuses the requisition for
every suite sharing the setup. Only 65 of those 178 were from the current session, so the pile-up
long predates any one run. Fixed: it now requires genuine headroom.

⚠ Do **not** filter positions on `establishmentApprovedOn`: it is on the entity and drives the gate
but is **absent from the position read DTO**, so every position looks unconstrained and `find`
returns the same over-subscribed row. `expectedHeadcount` and `employeeCount` are exposed and
honest. *(That the DTO omits the very column added to answer "is this establishment authorised, or
still the column default?" is itself a finding — recorded for lane O's neighbourhood.)*

**`setup.mjs` matched the workflow definition by exact string.** It compared
`entityType === 'StaffRequisition'` while the API returns the display name `'Staff Requisition'`, so
it reported *"found 0"* and told the reader to publish a second definition — which would have left
the tenant with two active ones and the engine's choice between them unspecified. It now normalises
the way `NormalizeEntityTypeKey` does.

### 9.2 Stale assertions — named, not waved through

| Suite | Assertion | Why it is stale |
|---|---|---|
| `slice-c` | panelist records a verdict, expects 200 | **G-9.3 deliberately tightened this to HR-only.** 403 is now correct. |
| `slice-e` | talent-pool match scores 90 | **G-13.2 replaced the rubric with three tiers** precisely so an empty profile no longer tops the list. 35 is the intended answer. |
| `slice-e`, `slice-b` | flat talent-pool add/remove, expects 200 | the old flat door is retired; 404 is correct. |
| `run-lane5b` | HR cannot delete a criterion, expects 403 | the gap-closure **granted `RecruitmentAdmin` to the HR role**; the delete succeeding is correct, and the follow-on 404 is the row already being gone. |

None touch geography, Location criteria or scoring. The two suites that do exercise the changed
engine — `run-k` and lane V — match their baselines exactly.

### 9.3 Parked blockers — real, pre-existing, owned by later lanes

| # | Blocker | Owner |
|---|---|---|
| 1 | `accept-conditionally` refuses without a pre-employment check set, blocking `run-g` and `slice-d`. The rule is correct and documented; the suites predate it. | ~~**Lane H** fixes the cause by seeding the check set at offer creation.~~ ⚠ **Re-measured 2026-09-22 after lane B: still blocked.** `run-g` dies on the same 422 and `slice-d` aborts at 75/98. Lane H seeds the set **from the position's check template**, so a position without one still produces an offer that cannot be conditionally accepted. Whether that is the design or a gap is **lane H's** call. |
| 2 | **The Staff Requisition workflow is not routing multi-step.** The definition is `Published`, active, three steps — yet submit creates no instance and HR's single approval completes it, so `run.mjs`'s TRAP-1 assertion fires. This is the *"the definitions listing asks a different question from the engine"* trap. | **Lane D's neighbourhood.** Not to be patched blind — read the engine's lookup first. |

Neither is in lane A's diff: nothing there touches requisitions, the workflow engine, offers or
identity.

### 9.4 Raised with another module's owner

**Procurement supplier registration does not verify the applicant's email address**, while telling
the reader it does. `BusinessPartnerRegistrationService` refuses submission with *"A verified email
address or phone number is required"*, but the check behind that message only tests that the field
is non-empty and syntactically valid — there is no OTP, no confirmation token and no ownership check
anywhere in the procurement area. Supplier portal credentials and a one-time password are later
emailed to what the code calls *"the verified application contact"*.

Staff review and document verification sit in front of account provisioning, which contains the
risk, and their `ProcurementSupplierOnboardingToken` is better engineered than anything on the HR
side — **SHA-256 hashed at rest** with only the last four characters kept, plus `Generation`,
`Status`, issued/activated/expired timestamps and an `ExpiryReason`. Two observations, then:

- the *"verified"* wording should be corrected, or the check built;
- **HR should borrow their token design** for the offer-response and interview-confirmation tokens,
  which today store their tokens in clear.

Recorded for the procurement owner in `../integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`.
