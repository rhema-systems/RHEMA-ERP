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

### 5. `ShowScoreBreakdownToEmployee`

> *"If false, only the overall score/grade and narrative comments are shown to the employee."*

* **Server:** `GetHRReviewAsync` redacts on one condition only —
  `shouldHidePeerDetails = isAppraiseeViewing && settings.PeerReviewsAnonymous`
  (`PerformanceAppraisalService.cs:4066`). The criterion-by-criterion breakdown is returned in full
  regardless of this setting.
* **Client:** `me/performance/appraisals/[id]/page.tsx:305,446` hides the breakdown card.

**Effect of switching it off:** the employee's screen hides the breakdown. The API still returns it to
their own browser.

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

#### 7. `RequireKickOffConversation` · 8. `RequireMidYearConversation`
Single read each — `AppraisalAdvanceHelpers.cs:64` and `:68`. Nothing refuses an evaluation because a
required conversation was never held.

*Since lane B1 — enforced.* Both belong to the goal-setting step, where the old resolver kept them:
the self-evaluation submit is refused until the required conversation is held, and the refusal names
it (*"…the kick-off conversation has not been held"*). Lane B2 moves the mid-year one to hold the
manager's submission instead of the employee's.

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
| `MinPeerEvaluators` | **Self-eval submit refused** outside the range (`:1449`); phase gate (`:178`); **finalise refused** — *"At least N peer reviews must be completed"* (`:4370`). *Since lane B1:* the nomination step counts **live** nominations — pending ones included, rejected ones not — and the evaluation step submitted ones; the manager's submission waits for the second |
| `MaxPeerEvaluators` | **Nomination refused** singly (`PeerNominationService.cs:211`) and in batch (`:399`) |
| `PeerNominationMode` | Who may nominate (`PeerNominationService.cs:100`); who the approval is routed to (`:473`); whether the self-eval submit rule applies (`PerformanceAppraisalService.cs:1446`). *Since lane P:* line 100 was only ever the editing window; the mode now decides **who** — in Manager mode the appraisee is refused both nominate routes (`EnsureMayNominate`) — and every nomination records the login that made it (the batch recorded the appraisee whoever sent it) |
| `AllowPeerKpiEvaluation` | Which items the peer form renders (`PeerEvaluationService.cs:199`) and **peer submit completeness** (`:321`). *Since lane L-b:* the employee's **goal rows** follow it too — read-only on the form, **a peer's score on one refused when saved**, required at submission only when it is on. A KPI row is still caught only at submission (lane D). *Since lane L-c:* the form takes which rows are read-only from the server's per-row `isScoreable` and sends only the rows the peer may score |
| `AllowSelfSoftSkillRating` | **Self-eval submit requires every competency scored when ON** (`PerformanceAppraisalService.cs:1461`) — see the caveat below |

### The gates
| Setting | What it really does |
|---|---|
| `RequireCalibration` | Phase gate (`AppraisalWorkflowService.cs:206`) **and the finalise refusal** (`PerformanceAppraisalService.cs:4378`). *Since lane A:* a commit lifts the gate only on appraisals at the calibration step (manager submitted); it used to stamp everyone in the session's scope, so an appraisal could pass the gate before any panel saw its score. *Since lane P:* also one of the two conditions of the employee's **outcome release** (see `RequireHRReview`). *Since lane B1:* the calibration step — the commit calibrates an appraisal **at** it (or one it already calibrated and this session restates, or a final one it adjusts) and skips the rest naming the step each is at, releasing them from the session; a commit that is the last step **completes** the appraisal with its score (it stayed in Governance for good); a cycle with this off has no calibration step, and a commit skips its appraisals |
| `RequireHRReview` | Phase gate; assigns the HR reviewer on manager submit (`:2604`); **refuses acknowledgment before sign-off** (`:2753`); short-circuits progress-to-HR (`:3907`). *Since lane P:* with `RequireCalibration`, decides when an outcome is **released to the employee** (`AppraisalRelease.IsReleased`) — until then their own copy of the appraisal, lists, trend and HR review carries no score, grade, recommendations or narrative. *Since lane B1:* the sign-off is refused unless the appraisal is **at** the HR-review step, and the acknowledgment reads the sign-off record (`AppraisalHRReview`), which HR's advance writes too |
| `HRReviewTiming` | Swaps the calibration / HR-review order in **both** resolvers (`AppraisalWorkflowService.cs:196`, `AppraisalAdvanceHelpers.cs:113`). *Since lane B1:* real on the **write** paths. With `BeforeCalibration`, HR signs off first (the sign-off used to demand calibration whatever the timing, so such a cycle could never be finalised), a panel before the sign-off skips the appraisal, and nothing is published to the talent pools until the panel has committed |
| `RequireEmployeeAcknowledgment` | **Decides the status finalisation lands on** — `Governance` vs `Completed` (`PerformanceAppraisalService.cs:4388`); phase gate (`:224`). *Since lane A:* the acknowledgment settles the score and publishes it; with calibration and HR review off, the manager's submission settles it first so the employee acknowledges a score, not a blank (it used to finish with none). *Since lane B1:* the last step of the pipeline; where a write leaves the appraisal follows the gates, not this switch alone (a required final conversation used to be skipped when this was off) |
| `RequireGoalSetting` | Phase gate (`AppraisalWorkflowService.cs:150`). *Since lane B1:* **refuses the self-evaluation submit** until the goals are set — the minimum, the manager's approval and, with a goals section on the template, the goal set's lock — counting the employee's goals in the cycle, linked to the appraisal or not |
| `HRCanModifyScores` | **Refuses score modifications on appeal resolution** (`PerformanceAppraisalService.cs:3368`). *Since lane A:* the modifications it allows **reach the score** (checked against the item's own scale; a KPI's is a restated achievement %). Before, they were written and the overall re-summed stale weighted scores, so an upheld appeal never moved the result. *Since lane L-b:* a restatement may name a **goal row** by its snapshot row; one naming a criterion that is not the appraisal's is refused. *Since lane L-c:* HR's appeal screen sends it, by the row's template item or snapshot row |
| `DefaultHRReviewerId` | Chosen as reviewer when it points at an active employee, else least-loaded fallback (`:3984`) |
| `AllowEmployeeResponse` | **Refuses the employee's written response** when off (`:1003`) |
| `AppealReevaluationWindowDays` | Sets `AppealRemandDeadline` (`:3454`) — and **the post-remand re-evaluation is refused after it** (`:2361`) |
| `MaxGoalsPerEmployee` | **Refuses goal creation** past the ceiling (`EmployeeGoalService.cs:205`) — and, *since lane L-a*, **refuses the goal-set lock** above it, which catches the set that grew past the ceiling by resubmitting a rejected goal |
| `EnableCheckIns` | **Refuses check-in creation** (`CheckInService.cs:145`) |
| `EnablePrivateJournal` | **Refuses a private journal entry** (`PerformanceJournalService.cs:159`) |
| `PeerReviewsAnonymous` | **Server-side redaction** of peer identity from the appraisee (`PerformanceAppraisalService.cs:4066`). *Since lane P:* an HR officer who is the appraisee is the appraisee — the manager's peer detail, which names each peer, used to open to them through the desk exemption; lane P's suite asserts no peer name in seven of the appraisee's payloads |

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
| `AutoLockOnDeadline` | The sweep honours it and reports `AutoLockEnabled` (`AppraisalWorkflowService.cs:699`). *Since lane B1:* each advance is past the step the gates put the appraisal at — a step before the manager's evaluation is **waived** by the advance's own log row. *Since lane L-a:* waiving goal setting **locks the agreed set** — submitted goals approved on the recorded reason, every approved or running goal locked, drafts and rejected goals left out (it used to approve all three and lock nothing). The lock is the goal's flag; its year runs on (D-29) |
| `ProbationExtensionMonths` | The extension actually applied by the probation handler (`ProbationHandlers.cs:157`) |
| `ManagerWorkloadThreshold` | The "managers over workload" figure (`AppraisalCycleService.cs:1066`) |
| `DeadlineRiskHighDays` · `MediumDays` · `LowDays` | The risk banding on the cycle progress dashboard (`AppraisalCycleService.cs:516,1070-1072`) |
| `SuccessionPoolName` · `SuccessionDefaultReadiness` | The pool and readiness a succession nomination writes (`SuccessionNominationHandler.cs:90-91`) |

> **Caveat on `AllowSelfSoftSkillRating`.** It is enforced, but it does the **opposite of what its
> label says**. The admin screen reads *"Employees may rate their own soft skills"* with the hint
> *"Off by default — most policies keep behavioural criteria for the manager."* In fact
> `BuildSelfEvaluationSections` never filters competency items, and the frontend never reads the flag —
> so the employee always sees and can always score the behavioural criteria. What the setting really
> controls is a **completeness check**: when ON, the submit is refused unless every competency is
> scored. "May rate" is really "must rate". There is also a dead local at
> `PerformanceAppraisalService.cs:1533` — assigned from the setting and never used.

---

## What to fix, in priority order

> **Status 2026-09-29 (closure lane B1).** Items **3, 4 and 5 are done**; item 1 is down to
> `ShowPeerScoresToManager` (ghost 2 is enforced, ghost 3 removed in batch 1); item 7's refusal is
> done and its governance-view half is lane B2's; items 2 and 6 are lane B2's.

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
