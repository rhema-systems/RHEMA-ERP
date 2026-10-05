# HR — Appraisal Settings Audit

**What this is:** a field-by-field check of whether every value on the Appraisal Settings profile
actually drives behaviour. Companion to `HR-PERFORMANCE-SYSTEM-GUIDE.md` § 1.6, which lists the same
settings as a policy reference; this document says which of them the code obeys.

**Audited:** 2026-09-17 against the working tree. Every claim carries a `file:line`.

> **Closure status (2026-09-29) — read before the body.** The performance closure
> (`HR-PERFORMANCE-FINAL-CLOSURE-PLAN.md`) is working through this audit; its decision 1 implements
> twelve of the fourteen non-enforced settings and removes two. So far:
> - **Migration batch 1** removed `IsManagerAuthoritative` (#14) and `RequireDevelopmentPlanUpdate`
>   (ghost 3) from the entity, the DTOs and the form — the profile now carries **48** fields.
> - **Lane A** (scoring and the settle path) changed what three *enforced* settings do — marked
>   *"Since lane A"* in section A: `HRCanModifyScores`, `RequireCalibration`,
>   `RequireEmployeeAcknowledgment`. No verdict moved between A/B/C/D.
> - **Lane P** (privacy and access) changed what four *enforced* settings do and touched one
>   presentation setting — marked *"Since lane P"*: `PeerNominationMode` (now decides who may
>   nominate), `PeerReviewsAnonymous` (holds for an HR officer who is the appraisee),
>   `RequireCalibration` and `RequireHRReview` (now also decide when the outcome is released to the
>   employee); `ShowSelfScoreToManager` is honoured on one more read, but its verdict stands — the
>   manager's own form, the path this audit describes, is lane B2's. No verdict moved.
> - **Lane B1** (one gate evaluator) **moved nine verdicts**. The two pipeline resolvers of
>   Group 1 are one (`AppraisalGates`), and every write path — self, peer and manager submissions,
>   the calibration commit, HR's sign-off, the acknowledgment, the appeal and HR's advance — is held
>   to the step it puts the appraisal at. So the eight Group 1–3 settings below (#6–#13) and ghost 2
>   (`AllowAcknowledgmentWithoutConversation`) now **refuse something**: all nine are *enforced*.
>   Marked *"Since lane B1"*, and the section A rows whose mechanism changed are too. **The profile
>   now: 45 enforced, 0 advisory, 2 client-side only, 1 ghost** (`ShowPeerScoresToManager`) — of 48.
>   The client-side pair and the ghost are lane B2's.
> - **Lane L-a** (the goal set's governance) changed what three *enforced* settings do — marked
>   *"Since lane L-a"*: `MinGoalsPerEmployee` and `MaxGoalsPerEmployee` now also hold the manager's
>   *lock goal set* (with the weights adding to 100), and `AutoLockOnDeadline`'s advance past goal
>   setting locks the agreed set. No verdict moved.
> - **Lane L-b** (goal rows and scoring) widened two *enforced* settings — marked *"Since lane L-b"*:
>   `AllowPeerKpiEvaluation` now also governs the employee's **goal rows** (read-only on the peer form,
>   refused when a peer saves one, required at submission only when it is on), and a
>   `HRCanModifyScores` restatement can name a goal row. The goal-set lock (`RequireGoalSetting` with
>   a goals section) now also builds the appraisal's goals section. No verdict moved.
> - **Lane L-c** (the screens) moved no verdict either — marked *"Since lane L-c"*. The peer form
>   takes its read-only rows from the server's own per-row `isScoreable`, where it re-derived them
>   from the KPI id (which no goal row carries), so `AllowPeerKpiEvaluation`'s goal rows are read-only
>   on screen too; and HR's appeal screen can send a `HRCanModifyScores` restatement for a goal row.
> - **Lane B2, slice B-v** (one visibility rule, `AppraisalVisibility`) **moved three verdicts**: the
>   two client-side-only settings (#4 `ShowSelfScoreToManager`, #5 `ShowScoreBreakdownToEmployee`)
>   and the last ghost (#1 `ShowPeerScoresToManager`) are enforced by the server on every read of an
>   evaluation — marked *"Since lane B2"*. **The profile now: 48 enforced, 0 advisory, 0 client-side
>   only, 0 ghosts — of 48.** (`KpiDefinition.TolerancePercent`, a KPI field rather than a profile
>   field, moved to migration batch 2 — closure plan D-32 — and **enforced since slice F-b (2026-10-01)**: kept with
>   the KPI's target in the criterion snapshot at generation, and an actual within it of the target scores as met.
>   Forms generated before then carry none and score as before.)
> - **Lane B2, slice B-w** (the write paths, B6, B8) moved no verdict — all 48 were enforced — but
>   changed what seven do, marked *"Since lane B2 (B-w)"*: `RequireManagerGoalApproval` (off, a
>   submitted goal lands Approved and needs no manager; a draft holds goal setting either way),
>   `RequireMidYearConversation` (holds the manager's submission, not the self-evaluation),
>   `AllowPeerKpiEvaluation` (the draft save refuses a template KPI row too, and the submit drops one a
>   draft holds), `AllowSelfSoftSkillRating` (the label says what it does), `MinGoalsPerEmployee` and
>   `MaxGoalsPerEmployee` (the manager's team desk reads the pair) and the deadline-risk bands (a
>   profile out of order is refused, as is a peer or goal minimum above its maximum). The profile has a
>   **default** now — a flag HR sets, where it was "the newest profile". `AppraisalCompetency.RequireEvidence`,
>   a criterion field, is enforced on all three submissions and has a door on the criteria page.
>   **Every item of "What to fix" is done.**
> - **Lane C, slice C-a** (the appeal machine) moved no verdict but changed what two do, marked
>   *"Since lane C (C-a)"*: `HRCanModifyScores` (changes go only with an upheld appeal) and
>   `AppealReevaluationWindowDays` (a remand now reopens the manager's evaluation until it, and HR can
>   extend it).
> - **Lane C, slice C-b** (the appeal reads) moved no verdict; it narrowed what one allows, marked
>   *"Since lane C (C-b)"*: `HRCanModifyScores` (a change only on a criterion the appeal contests).
>   `ShowScoreBreakdownToEmployee` reads as before; the appeal status now also withholds the scores as
>   they stand while a remand is open — that is the release rule, not the switch.
> - **Lane D** (peer nomination and evaluation integrity, 2026-09-30) moved no verdict but changed what
>   five do, marked *"Since lane D"*: `MinPeerEvaluators` and `MaxPeerEvaluators` (every count leaves a
>   rejected nomination out, so a replacement can be nominated), `PeerNominationMode` (in Manager mode
>   a nomination is approved as it is made; the window follows the mode everywhere),
>   `PeerReviewsAnonymous` (in Manager mode the appraisee's nomination reads carry counts, not peers —
>   D-40) and `AllowPeerKpiEvaluation` (the manager's peer review lists the KPI and goal rows a peer
>   scored).
> - **Lane E, slice E-a** (the appraisal routes, 2026-09-30) moved no verdict and changed what one does,
>   marked *"Since lane E-a"*: `RequireCalibration` — HR's return to the manager takes a calibration with
>   it, so a returned appraisal is calibrated again on the manager's new evaluation.
> - **Lane E, slice E-b** (calibration, 2026-09-30) moved no verdict and changed what one does, marked
>   *"Since lane E-b"*: `RequireCalibration` — a session commits each appraisal once, and only the
>   evaluation its panel sat over, so a commit run again undoes neither an upheld appeal nor a return;
>   the grid says what a commit would take; a cancelled or deleted session releases its appraisals.
> - **Lane E, slice E-c** (cycle rules, 2026-09-30) moved no verdict and changed no setting. What it
>   changed around them: **a cycle's settings profile is fixed once the cycle is opened or has
>   appraisals** — an edit could swap the rulebook under running appraisals — and the deadline reminders
>   (`DeadlineRisk*` bands) now reach the cycle's scope as generation reads it, not leavers, inactive
>   targets or anyone holding a post a template names.
> - **Lane E, slice E-d1** (Withdrawn, 2026-09-30) moved no verdict and changed what three do, marked
>   *"Since lane E-d1"*: `RequireCalibration` — with HR's sign-off it decides when an appraisal is
>   **final**, and a final one is not withdrawn (a leaver's stands); `PeerNominationMode` — in Manager
>   mode a withdrawn appraisal takes no more nominations, and in either mode a peer who has left is
>   refused; `AllowEmployeeResponse` — HR's response route answers its refusal with 422, not 500. The
>   deadline reminders and the open notice no longer reach anyone whose appraisal was withdrawn.
> - **Lane E, slice E-d2a** (the cycle's own rules, 2026-09-30) moved no verdict and changed what two
>   do, marked *"Since lane E-d2a"*: `EnableAppeals` and `AppealWindowDays` — a cycle is not closed while
>   any Completed appraisal in it could still appeal. The deadline reminders (`DeadlineRisk*` bands) go
>   out for an Open cycle only; a Closed one was reminded.
> - **Lane E, slice E-d2b** (the live cycle, 2026-09-30) moved no verdict and changed what one does, marked
>   *"Since lane E-d2b"*: `AutoLockOnDeadline` — the sweep runs on an open cycle only and leaves the
>   officer's own appraisal alone.
> - **Lane E, slice E-e** (templates and settings, 2026-10-01) moved no verdict; it changed **when a
>   profile's settings can change at all** (D-67). Every appraisal reads its profile live through its
>   cycle — finished ones still read `ShowScoreBreakdownToEmployee`, `PeerReviewsAnonymous`,
>   `PeerNominationMode`, `EnableAppeals`/`AppealWindowDays` (Completed ones), `AllowEmployeeResponse`, and
>   `ProbationExtensionMonths` and the two succession settings whenever an outcome is approved — and only
>   the three evaluator weights (stamped on each evaluation), the interim-review pair (the review events)
>   and `AppealReevaluationWindowDays` (the remand deadline) are copied per appraisal. So **once any
>   appraisal sits on a cycle running under a profile, its rules are frozen** (`PUT` answers 409, naming
>   the fields); `SettingsName`, the three `DeadlineRisk*` bands, `ManagerWorkloadThreshold` and
>   `DefaultHRReviewerId` stay editable, and `POST …/{id}/clone` copies a profile for new rules. The save
>   now also refuses a weight outside 0–1 (the `[Range(0, 1)]` attributes have int bounds and let −0.4
>   and 1.4 through), a `SuccessionPoolName` over 100 characters (a 500), an enum value outside its type,
>   and a `DefaultHRReviewerId` that is not an employee of the tenant.
>
> Line numbers below are as of 2026-09-17; lanes A and B1 rewrote much of `PerformanceAppraisalService.cs`,
> and `AppraisalAdvanceHelpers.cs` is gone — its resolver is `AppraisalGates.cs`.

---


**Question asked:** the Appraisal Settings profile carries a lot of values. Are they all driving
behaviour, or are some of them ghosts?

**Answer:** of the **50** configurable fields, **36 are genuinely enforced**, **9 are advisory** (read,
but into something nothing acts on), **2 are client-side only** (the browser hides something the server
still sends), and **3 are complete ghosts** (no reader anywhere in the codebase).

**Audited:** 2026-09-17, against the working tree. Every claim below carries a `file:line`.

---

## Method — so this is reproducible

1. The field list is the **entity**, `AppraisalSettings` in
   `src/ErpSystem.Core/Entities/HR/PerformanceEntities.cs`, not the DTO or the form — 50 fields plus
   `SettingsName`.
2. One regex sweep for all 50 names across `src/**/*.cs` (8,483 raw hits), then **noise excluded**:
   the entity itself, `DTOs/`, `AppraisalMappingExtensions.cs`, `Migrations/`, `Seeders/`,
   `HREnums.cs`, `ApplicationDbContext.HR.cs`, and the settings service and controller — none of which
   is behaviour, all of which is CRUD, persistence or projection.
3. Every remaining hit was **read in context** and classified as enforcement, projection or comment.
4. Where a field reached a DTO, the **frontend** was searched for a consumer, excluding the settings
   form itself (which of course binds all 50).
5. Run twice with different exclusion lists. Both runs agree on the zero-reader set.

---

## The verdict, in one table

| | Count | Meaning |
|---|---|---|
| **A · Enforced** | **36** | The server refuses something, or the outcome changes, because of this value |
| **B · Advisory** | **9** | Read — but only into an analytics figure, a notification's wording, or a DTO field nothing consumes. Turning it off changes no rule |
| **C · Client-side only** | **2** | The server ignores it entirely; the React page hides something, but the data is still in the response payload |
| **D · Ghost** | **3** | No reader anywhere — backend, frontend, tests |

---

## D · The three ghosts

Zero references outside the entity, the DTOs, the mapping extensions, the migrations and the settings
form. Setting them has no effect of any kind.

### 1. `ShowPeerScoresToManager`

> *"If true, aggregated peer evaluation scores are visible to the manager while completing their
> evaluation. If false, peer scores are only revealed after the manager submits their own assessment."*

Nothing reads it. The manager's **Peer feedback** tab calls
`GET api/PerformanceAppraisals/{id}/manager-peer-evaluations`, which returns every submitted peer's
scores and comments unconditionally
(`PerformanceAppraisalService.cs:2670` onwards — the only setting consulted there is
`PeerReviewsAnonymous`, and that governs the *appraisee's* view).

**Effect of switching it off: none.** The anchoring-bias control it describes does not exist.

*Since lane B2 (slice B-v, 2026-09-29) — enforced.* Off, the line manager reads no peer's scores or
comments — on the peer review or the HR review — until they have submitted their own evaluation; who
the peers are and how many have submitted stay listed, so a missing response can still be chased. A
peer's draft reaches nobody, whatever the switch; the desk reads every submitted peer throughout
(`AppraisalVisibility.ForManager`; `run-final-settings.mjs`, stories m and s on both profiles).

### 2. `AllowAcknowledgmentWithoutConversation`

> *"If false, the employee cannot acknowledge the appraisal until the AppraisalConversation of type
> FinalReview is marked as completed."*

Nothing reads it. `AcknowledgeAppraisalAsync` (`PerformanceAppraisalService.cs:2753`) checks exactly
three things: the appraisal is in `Governance`, the caller is the appraisee, and — when
`RequireHRReview` is on — that HR has signed off. **No conversation check exists at all.**

There is even a stranded XML comment referring to it on a different property
(`PerformanceEntities.cs:574`), which is a documentation promise the code never kept.

**Effect of switching it off: none.** The employee can always acknowledge.

*Since lane B1 — enforced.* The final conversation is a step in the pipeline (`AppraisalGates`), and
this switch decides whether it stands before the acknowledgment: **off**, the acknowledgment is refused
(422, *"…is at Final Conversation — the final review conversation has not been held"*) until a
FinalReview conversation is held; **on**, the employee may acknowledge first. With no acknowledgment
step the switch has nothing to relax, so a required final conversation holds completion either way
(`run-final-gates.mjs`, e1 and e2 both ways, e3).

### 3. `RequireDevelopmentPlanUpdate`

> *"If true, the employee's year-end self-appraisal form includes a mandatory development plan section
> and cannot be submitted until all objectives are updated."*

Nothing reads it. `SaveSelfEvaluationAsync` has no development-plan branch, and the self-evaluation
context (`PerformanceAppraisalService.cs:1380`) never projects it. The development plan and the
appraisal are entirely separate records with no submit-time link.

**Effect of switching it off: none.** There is no development-plan section on the self-appraisal form.

---

## C · The two client-side-only settings

These are described in the entity as controls. They are **presentation**: the React page declines to
render something the API has already sent. Anyone with the browser's network tab sees the hidden value.

### 4. `ShowSelfScoreToManager`

> *"If false, self-scores are hidden from the manager until after they submit (prevents anchoring
> bias)."*

* **Server:** `MapManagerEvaluationItem` populates `EmployeeSelfNumericScore`,
  `EmployeeSelfActualValue` and `EmployeeSelfNotes` **unconditionally**
  (`PerformanceAppraisalService.cs:4845-4847`). The setting is never consulted on that path.
* **Client:** `team-appraisals/[id]/page.tsx:157` reads
  `context?.settings?.showSelfScoreToManager !== false` and drops the comparison aside.

**Effect of switching it off:** the manager's screen stops *showing* the self-score. The payload still
contains it. If anchoring bias is the reason for the setting, it is not prevented — it is hidden.

*Since lane P (P12):* the employee's own form (`GET …/{id}/self-evaluation-context`), which the
manager and HR could also read, now withholds the entries from everyone but the employee until the
self-evaluation is submitted, and from the manager afterwards when this setting is off. That is a
second path, not the manager's form: `MapManagerEvaluationItem` still sends the self scores
unconditionally, so the verdict above stands until lane B2.

*Since lane B2 (slice B-v, 2026-09-29) — enforced.* One rule (`AppraisalVisibility`) on every read
that carries the self-evaluation to the line manager: the manager's form, the submitted
self-evaluation, the self-evaluation context, the HR review and the goal assessments. Off, the manager
reads the employee's entries only once they have submitted their own evaluation — the switch's own
words; P12's first cut hid them from the manager for good — and a self-evaluation draft is on none of
them, for the manager or the desk (the manager's form used to show a draft's scores). The page takes its
cue from the server's `selfScoresWithheld` and shows the comparison once the manager has submitted
(`run-final-settings.mjs`, stories d, m and s).

### 5. `ShowScoreBreakdownToEmployee`

> *"If false, only the overall score/grade and narrative comments are shown to the employee."*

* **Server:** `GetHRReviewAsync` redacts on one condition only —
  `shouldHidePeerDetails = isAppraiseeViewing && settings.PeerReviewsAnonymous`
  (`PerformanceAppraisalService.cs:4066`). The criterion-by-criterion breakdown is returned in full
  regardless of this setting.
* **Client:** `me/performance/appraisals/[id]/page.tsx:305,446` hides the breakdown card.

**Effect of switching it off:** the employee's screen hides the breakdown. The API still returns it to
their own browser.

*Since lane B2 (slice B-v, 2026-09-29) — enforced.* The appraisee's copy of the HR review, the goal
assessments, the appeal page, the appeal status and the appeal outcome carry the manager's and the
peers' criteria only when this switch is on; off, the overall, the grade and the manager's narrative.
Whatever the switch, none of them carries the manager's leg before HR's sign-off (lane P's release
rule) — the appeal page and the goal assessments did, the latter as a draft
(`run-final-settings.mjs`, stories r and b).

> **Note the contrast.** `PeerReviewsAnonymous`, sitting two switches away on the same form, *is*
> enforced server-side. So on this screen two "visibility controls" are real security and two are CSS.
> A customer cannot tell them apart.

---

## B · The nine advisory settings

Each of these is read — so a grep says "it is used" — but the read lands somewhere that changes no
rule. They fall into three groups.

### Group 1 — the settings only the *other* pipeline resolver honours

**This is the structural finding of the audit, and it explains four of the nine.**

The module has **two** pipeline resolvers, with **different rule sets**:

| | `AppraisalWorkflowService.GetCurrentPhase` | `AppraisalSubStatusResolver.Resolve` |
|---|---|---|
| Where | `AppraisalWorkflowService.cs:142` | `AppraisalAdvanceHelpers.cs:15` |
| Used by | the phase rail on every appraisal screen, `IsEditableByRole`, and — with the service-level checks — everything that actually refuses an action | the analytics dashboard's stage counts and attention list, and the deadline-enforcement advance path |
| Honours goal **count** | ✗ | ✓ `MinGoalsPerEmployee` |
| Honours goal **approval** | ✗ *(hard-codes "not Draft/Pending/Rejected")* | ✓ `RequireManagerGoalApproval` |
| Honours **peer nomination** gate | ✗ | ✓ |
| Honours **kick-off / mid-year conversations** | ✗ | ✓ |
| Honours **final conversation** | ✗ | ✓ |

So the four conversation- and goal-governance settings below take effect **only** on the analytics
dashboard and the manual-advance path — neither of which refuses anybody anything.

> *Since lane B1:* **one resolver.** `AppraisalGates.Resolve` (`AppraisalGates.cs`) carries every gate
> of the right-hand column, and the phase endpoint, the dashboard, the lists and every write path read
> it through `AppraisalLifecycleService`. It counts goals by **employee and cycle** — the left column's
> source, `appraisal.Goals`, missed any goal agreed before its appraisal was generated.

#### 6. `RequireManagerGoalApproval`
Single read: `AppraisalAdvanceHelpers.cs:50`. Neither `EmployeeGoalService` nor
`GoalWorkflowCommandService` reads it (verified: zero occurrences in both files). A goal's lifecycle is
identical whether this is on or off — and `GetCurrentPhase:149` already treats Draft / PendingApproval /
Rejected goals as not-ready **regardless**, so turning it *off* does not let unapproved goals through
the goal-setting phase either.

*Since lane B1 — enforced.* **On**, the self-evaluation submit is refused while any of the employee's
goals in the cycle is still a draft or waits for the manager's approval (*"…is at Goal Setting — one
goal still waits for the manager's approval"*); **off**, every live goal counts towards the minimum as
it stands. The goal's own lifecycle still ignores the switch — landing a submitted goal straight on
Approved when it is off is lane B2's.

*Since lane B2 (B-w):* the goal's lifecycle reads it too. **Off**, a submitted goal lands
**Approved** on submission (`GoalWorkflowCommandService.SubmitGoalAsync`), and an employee with no line
manager may submit — the manager is needed only to route an approval. A **draft** holds goal setting
either way (*"…one goal is still a draft, not yet submitted"*): with the switch off it used to count
toward the minimum.

#### 7. `RequireKickOffConversation` · 8. `RequireMidYearConversation`
Single read each — `AppraisalAdvanceHelpers.cs:64` and `:68`. Nothing refuses an evaluation because a
required conversation was never held.

*Since lane B1 — enforced.* Both belong to the goal-setting step, where the old resolver kept them:
the self-evaluation submit is refused until the required conversation is held, and the refusal names
it (*"…the kick-off conversation has not been held"*). Lane B2 moves the mid-year one to hold the
manager's submission instead of the employee's.

*Since lane B2 (B-w):* **the mid-year holds the manager's submission** (`AppraisalGates.EnsureManagerMaySubmit`)
and nothing before it: the appraisal waits at Manager Evaluation, whose blocker names the mid-year while
it is missing, and the manager's drafts are not held. A profile that requires only the mid-year has no
goal-setting step. A conversation must now name its type — the create DTO defaulted to `KickOff`, so a
body without one booked the conversation this gate counts.

#### 9. `RequireFinalConversation`
Read at `AppraisalAdvanceHelpers.cs:153` and `:209` (the governance-status helper), plus the dashboard's
deadline row (`HRCycleDashboardQueryService.cs:502`). **Acknowledgment does not check it** — which is
also why its intended companion, `AllowAcknowledgmentWithoutConversation`, has nothing to switch.

*Since lane B1 — enforced.* A step between HR's sign-off and the acknowledgment: the acknowledgment is
refused until the FinalReview conversation is held (unless ghost 2 lets it go first), and on a cycle
with no acknowledgment the appraisal waits in Governance until the conversation is held — holding it
completes the appraisal and settles its score. HR's sign-off used to complete such an appraisal on the
spot, conversation or not.

#### 10. `MinGoalsPerEmployee`
Reads: `AppraisalAdvanceHelpers.cs:45`, and `EmployeeGoalService.cs:550` where it computes
`MeetsMinGoalCount` on the goal-summary DTO. **The frontend never reads `meetsMinGoalCount`** (zero
references). Nothing refuses a self-evaluation submit, or a goal-set submission, for having too few
goals. Its sibling `MaxGoalsPerEmployee` **is** hard-enforced (`EmployeeGoalService.cs:205-216`), so the
pair is asymmetric.

*Since lane B1 — enforced.* The self-evaluation submit is refused below the minimum (*"…1 of the 2 goals
this cycle requires are set"*), counting the employee's live goals in the cycle whether or not they are
linked to the appraisal. Surfacing `meetsMinGoalCount` in the manager's governance view is lane B2's.

*Since lane B2 (B-w):* the manager's **team desk** reads the pair: its verdict is *Below minimum* or
*Above maximum* before it looks at the weights (one goal at weight 100 on a three-goal profile read
*Structurally complete* and offered a *Lock set* the lock refused), and the row carries
`meetsMinGoalCount`, `withinMaxGoalCount` and the bounds.

*Since lane L-a:* the manager's **lock goal set** reads the pair too — it is refused below the minimum
and **above the maximum**, and unless the live goals' weights add to 100 (`GoalSetRules.LockBlocker`).
That closes the maximum's one gap: rejecting a goal, adding another and resubmitting the first left
four live goals under a ceiling of three, because the ceiling is checked only at creation.

### Group 2 — appeals

#### 11. `EnableAppeals` · 12. `AppealWindowDays`
Both are read in exactly one place: `GetMyAppraisalsAsync` (`PerformanceAppraisalService.cs:1290-1294`),
where they compute a `CanFileAppeal` flag on the *My Appraisals list row* DTO.

Two problems:

* **`SubmitAppealAsync` does not check either of them.** Its guards are ownership, status `Completed`,
  no existing appeal, and at least one appealed item — nothing else. So an appeal can be filed on a
  cycle whose policy says appeals are off.
* **The frontend never reads `canFileAppeal`.** The only matches in the whole frontend are the type
  definition and an unrelated procurement component. `me/performance/appraisals/[id]` offers the
  **File an appeal** button on `status === 'Completed'` alone.

Also worth noting: the window is computed from **`cycle.EndDate + AppealWindowDays`**, while the
entity's own documentation says *"days after employee acknowledgment"*. Even the advisory figure is
anchored to the wrong date.

*Since lane B1 — both enforced.* One rule, `AppraisalGates.CanFileAppeal`, for the submit, the appeal
page and the list row: Completed, no appeal yet, appeals on, and inside the window, which now runs
from the **acknowledgment** (else HR's sign-off, else the last submission). `SubmitAppealAsync` refuses
with a 422 that says which. The portal's button still keys off the status alone — lane I.

*Since lane E-d2a — the window also holds the cycle open.* The cycle's close asks the same
`CanFileAppeal` of every Completed appraisal and is refused (422, naming the day the last window
closes) while any could still appeal; a close used to cut every open window.

### Group 3 — read, but only into wording or sort order

#### 13. `PeerEvaluationOpenMode`
Genuinely does two things, neither of which is a gate:

* It changes the **notification text** a nominated peer receives — *"Your peer feedback form is open"*
  vs *"…opens once they submit their self-evaluation"* (`PeerNominationService.cs:569`), and triggers a
  second "now open" notification on self-evaluation submit
  (`PerformanceAppraisalService.cs:1757`). That is real, user-visible behaviour.
* It is consulted by `IsEditableByRole` (`AppraisalWorkflowService.cs:295`) — **but
  `IsEditableByRole` is called by nothing except its own read-only endpoint**
  (`AppraisalWorkflowController.cs:91`). `PeerEvaluationService.SaveDraftAsync` and `SubmitAsync` never
  consult the phase or this setting.

**So in `AfterSelfEval` mode a peer is told to wait, and is not actually prevented from submitting.**

*Since lane B1 — enforced.* The peer's draft and submission are held to the peer window
(`AppraisalGates.PeerWindow`): from the self-evaluation step in `WithSelfEval`, from the peer step in
`AfterSelfEval`, until the manager submits. A peer writing early in `AfterSelfEval` is refused with a
422 naming the step; `IsEditableByRole` answers from the same window.

#### 14. `IsManagerAuthoritative`
Copied onto the manager's `EvaluatorEvaluation.IsAuthoritative` at generation
(`AppraisalCycleService.cs:734`). That column is read in exactly one place —
`.OrderByDescending(e => e.IsAuthoritative)` in `GetEvaluatorEvaluationsAsync`
(`PerformanceAppraisalService.cs:642`), i.e. **list sort order**.

It has **no effect on scoring**. `AppraisalScoring` aggregates by evaluator *role* at the configured
role weights; nothing anywhere treats an authoritative evaluation as overriding.

---

## A · The 36 that are genuinely enforced

Grouped by what they actually do, with the strongest evidence line for each.

### Scoring and generation
| Setting | What it really does |
|---|---|
| `SelfEvaluationWeight` · `PeerEvaluationWeight` · `ManagerEvaluationWeight` | Written onto each `EvaluatorEvaluation.EvaluatorWeight` at generation (`AppraisalCycleService.cs:729,734`) and used as the role weights in the overall weighted mean |
| `RequireSelfEvaluation` | Phase gate (`AppraisalWorkflowService.cs:163`); whether generation creates the self evaluator record (`AppraisalCycleService.cs:728`); **finalise refuses without it** (`PerformanceAppraisalService.cs:4363`). *Since lane B1:* a pipeline step the **manager's submission waits for** (decision 5 — HR's audited advance can waive it, leaving the employee's draft a draft); sign-off demands it only when this is on (it demanded it regardless) |
| `RequireManagerEvaluation` | Phase gate (`:183`); generation (`AppraisalCycleService.cs:731`); **finalise refuses** (`:4366`); blocks progress-to-HR (`:3924`). *Since lane B1:* off, nothing waits for a manager — the submission that completes the last step before governance moves the appraisal on, a peer's included |
| `RequirePeerReviews` | Phase gate (`:173`); nomination ceiling check (`PeerNominationService.cs:211`); **self-eval submit rule** (`PerformanceAppraisalService.cs:1446`). *Since lane B1:* two pipeline steps, **nominations** before the self-evaluation and **evaluations** before the manager's submission; sign-off demands the minimum only when this is on |
| `MinPeerEvaluators` | **Self-eval submit refused** outside the range (`:1449`); phase gate (`:178`); **finalise refused** — *"At least N peer reviews must be completed"* (`:4370`). *Since lane B1:* the nomination step counts **live** nominations — pending ones included, rejected ones not — and the evaluation step submitted ones; the manager's submission waits for the second. *Since lane D:* the self-evaluation's own count and the summary's `CanSubmit` leave a rejected nomination out too — they counted it against the minimum and the maximum, though the gate before them did not |
| `MaxPeerEvaluators` | **Nomination refused** singly (`PeerNominationService.cs:211`) and in batch (`:399`). *Since lane D:* on the live count — pending and approved — so a rejected nomination leaves room for its replacement (with one peer allowed, a rejection stranded the employee at "the maximum of 1") |
| `PeerNominationMode` | Who may nominate (`PeerNominationService.cs:100`); who the approval is routed to (`:473`); whether the self-eval submit rule applies (`PerformanceAppraisalService.cs:1446`). *Since lane P:* line 100 was only ever the editing window; the mode now decides **who** — in Manager mode the appraisee is refused both nominate routes (`EnsureMayNominate`) — and every nomination records the login that made it (the batch recorded the appraisee whoever sent it). *Since lane D:* in Manager mode a nomination is **approved as it is made** — on both routes the peer gets their evaluation and is told, and nobody is asked to approve (the manager's own nominations sat Pending for their approval, and nobody was told). The window follows the mode on the summary's `CanEdit` too (Employee mode while Draft or Active, Manager mode until completed or closed), and the manager's approve and reject are held to it. *Since lane E-d1:* in Manager mode the window also closes on a **withdrawn** appraisal (it let one through, so an approval asked a peer to evaluate an appraisal no one would finish); in either mode a peer who has left is refused at the nomination and at the approval |
| `AllowPeerKpiEvaluation` | Which items the peer form renders (`PeerEvaluationService.cs:199`) and **peer submit completeness** (`:321`). *Since lane L-b:* the employee's **goal rows** follow it too — read-only on the form, **a peer's score on one refused when saved**, required at submission only when it is on. A KPI row was then still caught only at submission — B-w closed that, below. *Since lane L-c:* the form takes which rows are read-only from the server's per-row `isScoreable` and sends only the rows the peer may score. *Since lane B2 (B-w):* off, **the draft save refuses a template KPI row** as well (it counted in the peer's total, so in the overall), and the submit drops any barred row a draft still holds before it totals. *Since lane D:* on, **the manager's peer review lists the KPI and goal rows** a peer scored, each with its weight (it listed competency rows only, at weight 0) |
| `AllowSelfSoftSkillRating` | **Self-eval submit requires every competency scored when ON** (`PerformanceAppraisalService.cs:1461`) — see the caveat below. *Since lane B2 (B-w):* labelled for what it does |

### The gates
| Setting | What it really does |
|---|---|
| `RequireCalibration` | Phase gate (`AppraisalWorkflowService.cs:206`) **and the finalise refusal** (`PerformanceAppraisalService.cs:4378`). *Since lane A:* a commit lifts the gate only on appraisals at the calibration step (manager submitted); it used to stamp everyone in the session's scope, so an appraisal could pass the gate before any panel saw its score. *Since lane P:* also one of the two conditions of the employee's **outcome release** (see `RequireHRReview`). *Since lane B1:* the calibration step — the commit calibrates an appraisal **at** it (or one it already calibrated and this session restates, or a final one it adjusts) and skips the rest naming the step each is at, releasing them from the session; a commit that is the last step **completes** the appraisal with its score (it stayed in Governance for good); a cycle with this off has no calibration step, and a commit skips its appraisals. *Since lane E-a:* HR's return to the manager — made before the sign-off, never while a panel sits — takes the calibration with it (the flag, the seat, the restated overall and the manager's captured number), so the step is asked again on the new evaluation; it kept all four, and the returned appraisal went straight back to HR's review with the old panel's number. *Since lane E-b:* a session commits each appraisal **once** — run again it skips what it calibrated (it re-applied its decisions over an upheld appeal) — and, once closed, **only the evaluation its panel sat over**: an appraisal whose manager submitted after the panel closed (after a return or a remand, or a late first submission) waits for the next panel; opening links only appraisals waiting for calibration, HR's advance past the step drops the link, and cancelling (new) or deleting a session releases the appraisals it holds. *Since lane E-d1:* with HR's sign-off it decides when an appraisal is **final** — signed off, and committed by the panel when this is on — and a final appraisal is **not withdrawn**: HR's withdraw refuses it, and a leaver's exit leaves it standing (logged) |
| `RequireHRReview` | Phase gate; assigns the HR reviewer on manager submit (`:2604`); **refuses acknowledgment before sign-off** (`:2753`); short-circuits progress-to-HR (`:3907`). *Since lane P:* with `RequireCalibration`, decides when an outcome is **released to the employee** (`AppraisalRelease.IsReleased`) — until then their own copy of the appraisal, lists, trend and HR review carries no score, grade, recommendations or narrative. *Since lane B1:* the sign-off is refused unless the appraisal is **at** the HR-review step, and the acknowledgment reads the sign-off record (`AppraisalHRReview`), which HR's advance writes too |
| `HRReviewTiming` | Swaps the calibration / HR-review order in **both** resolvers (`AppraisalWorkflowService.cs:196`, `AppraisalAdvanceHelpers.cs:113`). *Since lane B1:* real on the **write** paths. With `BeforeCalibration`, HR signs off first (the sign-off used to demand calibration whatever the timing, so such a cycle could never be finalised), a panel before the sign-off skips the appraisal, and nothing is published to the talent pools until the panel has committed |
| `RequireEmployeeAcknowledgment` | **Decides the status finalisation lands on** — `Governance` vs `Completed` (`PerformanceAppraisalService.cs:4388`); phase gate (`:224`). *Since lane A:* the acknowledgment settles the score and publishes it; with calibration and HR review off, the manager's submission settles it first so the employee acknowledges a score, not a blank (it used to finish with none). *Since lane B1:* the last step of the pipeline; where a write leaves the appraisal follows the gates, not this switch alone (a required final conversation used to be skipped when this was off) |
| `RequireGoalSetting` | Phase gate (`AppraisalWorkflowService.cs:150`). *Since lane B1:* **refuses the self-evaluation submit** until the goals are set — the minimum, the manager's approval and, with a goals section on the template, the goal set's lock — counting the employee's goals in the cycle, linked to the appraisal or not |
| `HRCanModifyScores` | **Refuses score modifications on appeal resolution** (`PerformanceAppraisalService.cs:3368`). *Since lane A:* the modifications it allows **reach the score** (checked against the item's own scale; a KPI's is a restated achievement %). Before, they were written and the overall re-summed stale weighted scores, so an upheld appeal never moved the result. *Since lane L-b:* a restatement may name a **goal row** by its snapshot row; one naming a criterion that is not the appraisal's is refused. *Since lane L-c:* HR's appeal screen sends it, by the row's template item or snapshot row. *Since lane C (C-a):* only an **upheld** appeal carries changes — a rejection or a remand carrying one is refused, whatever this switch (a rejection applied them, so a rejected appeal could move the score). *Since lane C (C-b):* only on a **criterion the appeal contests** (422 otherwise) — each contested item keeps its score at the filing, and the outcome says what moved from it; HR's screen offers a KPI's change as an achievement %, bounded by each row's scale |
| `DefaultHRReviewerId` | Chosen as reviewer when it points at an active employee, else least-loaded fallback (`:3984`) |
| `AllowEmployeeResponse` | **Refuses the employee's written response** when off (`:1003`). *Since lane E-d1:* HR's response route answers this refusal (and a withdrawn appraisal's) with 422 — it answered 500 |
| `AppealReevaluationWindowDays` | Sets `AppealRemandDeadline` (`:3454`) — and **the post-remand re-evaluation is refused after it** (`:2361`). *Since lane C (C-a):* reachable at last — the remand reopens the manager's evaluation until this deadline (it reopened nothing, P-71). HR can extend the deadline, and once it passes can decide the appeal on the original scores. The DTOs hold it to 1–30 days |
| `MaxGoalsPerEmployee` | **Refuses goal creation** past the ceiling (`EmployeeGoalService.cs:205`) — and, *since lane L-a*, **refuses the goal-set lock** above it, which catches the set that grew past the ceiling by resubmitting a rejected goal |
| `EnableCheckIns` | **Refuses check-in creation** (`CheckInService.cs:145`) |
| `EnablePrivateJournal` | **Refuses a private journal entry** (`PerformanceJournalService.cs:159`) |
| `PeerReviewsAnonymous` | **Server-side redaction** of peer identity from the appraisee (`PerformanceAppraisalService.cs:4066`). *Since lane P:* an HR officer who is the appraisee is the appraisee — the manager's peer detail, which names each peer, used to open to them through the desk exemption; lane P's suite asserts no peer name in seven of the appraisee's payloads. *Since lane D:* in **Manager** mode the appraisee's nomination reads carry counts, not peers — the summary lists none (`PeersWithheld`), and the list and a nomination by id refuse them (D-40); in Employee mode, where the appraisee chose the peers, nothing changes |

### Interim reviews
| Setting | What it really does |
|---|---|
| `ReviewFrequency` | How many review events generation creates, and whether any are created at all (`AppraisalCycleService.cs:746`) |
| `InterimReviewDepth` | Whether each generated event is `IsFullAppraisal` (`AppraisalCycleService.cs:894`) |
| `RequireMidYearSelfAssessment` | **Refuses the manager's completion** until the employee has submitted (`AppraisalReviewEventService.cs:487`) |
| `RequireGoalProgressUpdateAtReview` | **Refuses both submit and complete** until every live goal has an entry (`:462`, `:489`) |

### Operations
| Setting | What it really does |
|---|---|
| `AutoLockOnDeadline` | The sweep honours it and reports `AutoLockEnabled` (`AppraisalWorkflowService.cs:699`). *Since lane B1:* each advance is past the step the gates put the appraisal at — a step before the manager's evaluation is **waived** by the advance's own log row. *Since lane L-a:* waiving goal setting **locks the agreed set** — submitted goals approved on the recorded reason, every approved or running goal locked, drafts and rejected goals left out (it used to approve all three and lock nothing). The lock is the goal's flag; its year runs on (D-29). *Since lane E-d2b (2026-09-30):* the sweep runs on an **open** cycle only (a 422 naming the cycle — every refusal answered 500), and leaves the appraisal of the HR officer running it alone, saying so in its messages (the two-actor rule, D-62; the explicit advance refuses its appraisee 403). Proven on a profile with the setting on (the lifecycle suite's LD) — the two-position test's *on* half only: no suite runs the sweep with it off |
| `ProbationExtensionMonths` | The extension actually applied by the probation handler (`ProbationHandlers.cs:157`) |
| `ManagerWorkloadThreshold` | The "managers over workload" figure (`AppraisalCycleService.cs:1066`) |
| `DeadlineRiskHighDays` · `MediumDays` · `LowDays` | The risk banding on the cycle progress dashboard (`AppraisalCycleService.cs:516,1070-1072`). *Since lane B2 (B-w):* a profile whose bands are out of order (high ≤ medium ≤ low) or negative is refused, as is a peer or goal minimum above its maximum |
| `SuccessionPoolName` · `SuccessionDefaultReadiness` | The pool and readiness a succession nomination writes (`SuccessionNominationHandler.cs:90-91`) |

> **Caveat on `AllowSelfSoftSkillRating`.** It is enforced, but it does the **opposite of what its
> label says**. The admin screen reads *"Employees may rate their own soft skills"* with the hint
> *"Off by default — most policies keep behavioural criteria for the manager."* In fact
> `BuildSelfEvaluationSections` never filters competency items, and the frontend never reads the flag —
> so the employee always sees and can always score the behavioural criteria. What the setting really
> controls is a **completeness check**: when ON, the submit is refused unless every competency is
> scored. "May rate" is really "must rate". There is also a dead local at
> `PerformanceAppraisalService.cs:1533` — assigned from the setting and never used.
>
> *Since lane B2 (B-w):* the form reads *"Employees must score every behavioural criterion before
> submitting"*, the self-evaluation page tells the employee when it is on, and the dead local is gone.
> The behaviour is unchanged.

---

## What to fix, in priority order

> **Status 2026-09-29 (closure lane B1).** Items **3, 4 and 5 are done**; item 1 is down to
> `ShowPeerScoresToManager` (ghost 2 is enforced, ghost 3 removed in batch 1); item 7's refusal is
> done and its governance-view half is lane B2's; items 2 and 6 are lane B2's.
> **Then lane B2, slice B-v (2026-09-29): items 1 and 2 are done** — the last ghost and both
> visibility controls are enforced in the server's one visibility rule. Item 6 and item 7's
> governance view are slice B-w's.
> **Then slice B-w (2026-09-29): items 6 and 7 are done** — the label and the dead local; the
> minimum and maximum on the manager's team desk. All seven items are closed.

### 1. Delete or implement the three ghosts *(half a day either way)*
`ShowPeerScoresToManager`, `AllowAcknowledgmentWithoutConversation`, `RequireDevelopmentPlanUpdate`.
A switch on a policy screen that does nothing is worse than a missing feature: somebody will configure
it, believe it, and be wrong. Implementing them is small — the peer one is a projection filter, the
acknowledgment one is a three-line check next to the existing `RequireHRReview` guard, the development
one is a submit-time completeness check.

### 2. Make the two visibility controls real *(one day)*
`ShowSelfScoreToManager` and `ShowScoreBreakdownToEmployee` should be enforced where
`PeerReviewsAnonymous` already is — in the mapper, not in React. Until then, do not describe them as
controls to a customer with an audit function.

### 3. Close the appeal gate *(an hour)*
`SubmitAppealAsync` should honour `EnableAppeals` and the window, and the window should be anchored to
the acknowledgment date the entity documents rather than the cycle end date.

### 4. Reconcile the two pipeline resolvers *(the real work — a week)*
`GetCurrentPhase` and `AppraisalSubStatusResolver.Resolve` answer the same question with different
rules, and the one that enforces is the one with fewer rules. Either fold the sub-status resolver's
extra gates into the phase machine — which is what makes the four conversation and goal-governance
settings real — or delete them from the settings form. Today the analytics dashboard reports a stricter
pipeline than the system enforces, so a cycle can look blocked on the dashboard while everybody carries
on working.

### 5. Enforce `PeerEvaluationOpenMode`, or stop promising it *(half a day)*
`IsEditableByRole` is a read-only query nothing calls. Either the peer draft/submit path consults it, or
`AfterSelfEval` is a notification wording option and should be labelled as one.

### 6. Fix the `AllowSelfSoftSkillRating` label, and delete the dead local *(fifteen minutes)*
Rename it on the form to what it does — *"Employees must rate every behavioural criterion before they
can submit"* — or filter the sections when it is off, which is what the label implies.

### 7. Make `MinGoalsPerEmployee` symmetric with `MaxGoalsPerEmployee` *(an hour)*
The maximum refuses a create; the minimum computes a flag nothing reads. Either refuse the
self-evaluation submit below the minimum, or surface `meetsMinGoalCount` in the manager's governance
view, where it belongs.
