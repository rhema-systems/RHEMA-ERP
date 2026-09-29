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
>
> Line numbers below are as of 2026-09-17; lane A rewrote much of `PerformanceAppraisalService.cs`.

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

#### 6. `RequireManagerGoalApproval`
Single read: `AppraisalAdvanceHelpers.cs:50`. Neither `EmployeeGoalService` nor
`GoalWorkflowCommandService` reads it (verified: zero occurrences in both files). A goal's lifecycle is
identical whether this is on or off — and `GetCurrentPhase:149` already treats Draft / PendingApproval /
Rejected goals as not-ready **regardless**, so turning it *off* does not let unapproved goals through
the goal-setting phase either.

#### 7. `RequireKickOffConversation` · 8. `RequireMidYearConversation`
Single read each — `AppraisalAdvanceHelpers.cs:64` and `:68`. Nothing refuses an evaluation because a
required conversation was never held.

#### 9. `RequireFinalConversation`
Read at `AppraisalAdvanceHelpers.cs:153` and `:209` (the governance-status helper), plus the dashboard's
deadline row (`HRCycleDashboardQueryService.cs:502`). **Acknowledgment does not check it** — which is
also why its intended companion, `AllowAcknowledgmentWithoutConversation`, has nothing to switch.

#### 10. `MinGoalsPerEmployee`
Reads: `AppraisalAdvanceHelpers.cs:45`, and `EmployeeGoalService.cs:550` where it computes
`MeetsMinGoalCount` on the goal-summary DTO. **The frontend never reads `meetsMinGoalCount`** (zero
references). Nothing refuses a self-evaluation submit, or a goal-set submission, for having too few
goals. Its sibling `MaxGoalsPerEmployee` **is** hard-enforced (`EmployeeGoalService.cs:205-216`), so the
pair is asymmetric.

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
| `RequireSelfEvaluation` | Phase gate (`AppraisalWorkflowService.cs:163`); whether generation creates the self evaluator record (`AppraisalCycleService.cs:728`); **finalise refuses without it** (`PerformanceAppraisalService.cs:4363`) |
| `RequireManagerEvaluation` | Phase gate (`:183`); generation (`AppraisalCycleService.cs:731`); **finalise refuses** (`:4366`); blocks progress-to-HR (`:3924`) |
| `RequirePeerReviews` | Phase gate (`:173`); nomination ceiling check (`PeerNominationService.cs:211`); **self-eval submit rule** (`PerformanceAppraisalService.cs:1446`) |
| `MinPeerEvaluators` | **Self-eval submit refused** outside the range (`:1449`); phase gate (`:178`); **finalise refused** — *"At least N peer reviews must be completed"* (`:4370`) |
| `MaxPeerEvaluators` | **Nomination refused** singly (`PeerNominationService.cs:211`) and in batch (`:399`) |
| `PeerNominationMode` | Who may nominate (`PeerNominationService.cs:100`); who the approval is routed to (`:473`); whether the self-eval submit rule applies (`PerformanceAppraisalService.cs:1446`) |
| `AllowPeerKpiEvaluation` | Which items the peer form renders (`PeerEvaluationService.cs:199`) and **peer submit completeness** (`:321`) |
| `AllowSelfSoftSkillRating` | **Self-eval submit requires every competency scored when ON** (`PerformanceAppraisalService.cs:1461`) — see the caveat below |

### The gates
| Setting | What it really does |
|---|---|
| `RequireCalibration` | Phase gate (`AppraisalWorkflowService.cs:206`) **and the finalise refusal** (`PerformanceAppraisalService.cs:4378`). *Since lane A:* a commit lifts the gate only on appraisals at the calibration step (manager submitted); it used to stamp everyone in the session's scope, so an appraisal could pass the gate before any panel saw its score |
| `RequireHRReview` | Phase gate; assigns the HR reviewer on manager submit (`:2604`); **refuses acknowledgment before sign-off** (`:2753`); short-circuits progress-to-HR (`:3907`) |
| `HRReviewTiming` | Swaps the calibration / HR-review order in **both** resolvers (`AppraisalWorkflowService.cs:196`, `AppraisalAdvanceHelpers.cs:113`) |
| `RequireEmployeeAcknowledgment` | **Decides the status finalisation lands on** — `Governance` vs `Completed` (`PerformanceAppraisalService.cs:4388`); phase gate (`:224`). *Since lane A:* the acknowledgment settles the score and publishes it; with calibration and HR review off, the manager's submission settles it first so the employee acknowledges a score, not a blank (it used to finish with none) |
| `RequireGoalSetting` | Phase gate (`AppraisalWorkflowService.cs:150`) |
| `HRCanModifyScores` | **Refuses score modifications on appeal resolution** (`PerformanceAppraisalService.cs:3368`). *Since lane A:* the modifications it allows **reach the score** (checked against the item's own scale; a KPI's is a restated achievement %). Before, they were written and the overall re-summed stale weighted scores, so an upheld appeal never moved the result |
| `DefaultHRReviewerId` | Chosen as reviewer when it points at an active employee, else least-loaded fallback (`:3984`) |
| `AllowEmployeeResponse` | **Refuses the employee's written response** when off (`:1003`) |
| `AppealReevaluationWindowDays` | Sets `AppealRemandDeadline` (`:3454`) — and **the post-remand re-evaluation is refused after it** (`:2361`) |
| `MaxGoalsPerEmployee` | **Refuses goal creation** past the ceiling (`EmployeeGoalService.cs:205`) |
| `EnableCheckIns` | **Refuses check-in creation** (`CheckInService.cs:145`) |
| `EnablePrivateJournal` | **Refuses a private journal entry** (`PerformanceJournalService.cs:159`) |
| `PeerReviewsAnonymous` | **Server-side redaction** of peer identity from the appraisee (`PerformanceAppraisalService.cs:4066`) |

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
| `AutoLockOnDeadline` | The sweep honours it and reports `AutoLockEnabled` (`AppraisalWorkflowService.cs:699`) |
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
