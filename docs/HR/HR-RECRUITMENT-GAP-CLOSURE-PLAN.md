# HR Recruitment — gap closure plan

**Source:** every gap in Appendix C of `HR-RECRUITMENT-SYSTEM-GUIDE.md` (70 findings, recorded
2026-09-14 → 2026-09-15 across all 32 recruitment screens).

**Scope decision, 2026-09-15:** close all of them.

**START HERE.** Work the lanes in order. Each lane is a slice: backend edits, frontend edits, then
a harness run (×2, per `HR-VERIFICATION-HARNESS-GUIDE.md`) before it is marked done. A lane is not
done because its code compiles — it is done when its assertions are green twice and the guide's
gap entry has been rewritten to say what the code now does.

**Four decisions taken before any code was written** (2026-09-15, with the user):

| Question | Decision |
|---|---|
| The auto-approve fallback (G-4.1, G-10.1, G-5.4) | **Refuse locally in the HR adapters.** `WorkflowIntegrationService.SubmitAsync` is shared by 9 modules and is not touched. Recruitment's own submit paths ask `HasActiveApprovalWorkflowAsync` first and, with no definition, land at Submitted / PendingApproval so the bespoke `ApproveAsync` — and its segregation-of-duties check — actually runs. |
| The `HR.Recruitment.Admin` gate (G-3.1, G-4.8) | **Grant it to the HR role.** `AdministerRecruitment` joins `HrStaffGrants`. |
| Date-driven statuses (G-2.4, G-6.2, G-14.1, G-15.2) | **A nightly recruitment sweep**, following the existing HR background-service pattern (`ProbationReminderBackgroundService`, `AssetReminderBackgroundService`, …). |
| The vacancy register's missing writer (G-3.2) | **Hook the employee write paths** — terminate, retire, promote, transfer — rather than building the documented interceptor. Explicit call sites carry the real reason, the real vacating employee and the real date. |

---

## Lane status

| Lane | Gaps | State |
|---|---|---|
| A — permissions and authorization | G-3.1, G-4.8, G-2.3, G-3.10, G-9.1, G-15.5, G-3.3, G-8.2, G-7.5 | **built** |
| B — approval integrity | G-4.1, G-10.1, G-5.4 | **built** (also took G-5.5 early; folded in StaffMovement, PIP, EmploymentActionProposal) |
| C — the establishment register | G-3.2, G-3.4, G-3.5, G-3.6, G-3.7, G-3.8, G-3.9, G-4.4 | **built** |
| D — expiry and the nightly sweep | G-2.4, G-6.2, G-2.2, G-14.1, G-15.2, G-10.2, G-6.3, G-11.4 | **built** |
| E — the vacancy is editable | G-5.1, G-5.2, G-5.3, G-5.5, G-5.6, G-5.7, G-4.7, G-4.6, G-5.8, G-10.3 | **built** |
| F — the candidate register | G-7.1, G-7.2, G-7.3, G-7.4, G-7.6, G-8.5, G-11.3, G-12.4 | **built** (G-7.1 + G-12.4 were already closed; G-7.2 index deferred) |
| G — recall reaches the browser | G-4.3, G-10.4 | **built** |
| H — steps hand over to the next | G-12.1, G-12.2, G-9.4, G-9.2, G-9.3, G-8.1, G-8.3, G-8.4, G-4.5, G-13.1, G-13.3 | **built** (G-8.4 narrowed deliberately) |
| I — queues, counters and tiles | G-11.1, G-11.2, G-6.1, G-2.1, G-2.5 | **built** |
| J — reporting and the rest | G-15.1, G-15.3, G-15.4, G-14.3, G-14.4, G-13.2, G-13.4, G-9.5, G-9.6, G-10.5, G-6.4, G-12.3 | **built** |
| — the guide rewritten | all 70 | **done 2026-09-16** |

**All ten lanes are built and `HR-RECRUITMENT-SYSTEM-GUIDE.md` has been rewritten to match.** Every
finding there now carries what the code does today; the diagnoses are kept, in the past tense, as
the record. What remains open is the two items below and nothing else from the original 70.

---

## The auto-approve finding is HR-wide, not a recruitment defect

Recorded 2026-09-15, while closing G-4.1 and G-10.1.

`WorkflowIntegrationService.SubmitAsync` returns `WorkflowOutcome.Approved` whenever no active
definition exists for the entity type. **Every HR status adapter maps `Approved` to its own
approved status.** No HR workflow definition is seeded anywhere in the solution. So on every
tenant, for every HR record wired to the engine, Submit was Approve.

Appendix C called this "systemic, not local" on the strength of two instances. It is wider than
that: **27 submit call sites across 22 HR services**, and before this work
`HasActiveApprovalWorkflowAsync` was called by **none** of them.

**The fix is four parts, and part 1 alone makes things worse.** `CanUserApproveAsync` returns
`false` with no instance and `RecallWorkflowAsync` answers *"No active workflow found"* — so a
record that stops at a pending status becomes unapprovable, unrejectable and unrecallable, with
cancellation its only exit. That is G-4.2's shape. Submit, approve, reject and recall must be done
together. `HrWorkflowFallbackAuthority` carries the reasoning and the per-module permission
binding.

### Closed

| Entity type | Service | What Submit used to do |
|---|---|---|
| `StaffRequisition` | `StaffRequisitionService` | Draft → Approved, no approver, no segregation of duties |
| `JobOffer` | `JobOfferHireService` | Draft → Approved **+ `ApprovedDate`**, unlocking *Issue to candidate* |
| `StaffMovement` | `StaffMovementService` | Draft → Approved, **and stamped the submitter as `AuthorizedById`** — a false audit fact, not just a missing gate |
| `PerformanceImprovementPlan` | `PerformanceImprovementPlanService` | Draft → **Active**, putting an unreviewed plan in force and notifying the employee, supervisor and HR owner that it bound them |
| `EmploymentActionProposal` | `EmploymentActionProposalService` | Proposed → Approved on a proposal to promote, discipline or terminate |

Segregation of duties was added where the entity carries an unambiguous requester in the same id
space as the approver: the offer (`PreparedById`), the movement (`RequestedById`) and the vacancy
(`CreatedById`, G-5.4). The requisition already had one. PIP and `EmploymentActionProposal` have no
such field, so none was invented — noted at both call sites instead.

### Still open — the same mechanism, not yet touched

`SalaryReviewProposal`, `AppraisalTemplate`, asset requests (×2), `AssetSurcharge`, attendance
(×2), consultants, `EmployeeSalaryChangeRequest`, job analysis (×2), leave encashment, leave plans
(×2), leave requests, overtime, probation, separation, discipline, travel, succession, team
activities (×2), training nominations.

Roughly seventeen services. Each needs the same four-part change and a decision about which
permission is its approval tier. **This is a programme in its own right and is not part of the
recruitment gap closure** — it is recorded here because this is where it was found. Two of them
deserve to go first on impact alone: `EmployeeSalaryChangeRequest` (auto-approves a pay change)
and `Separation` (auto-approves a termination, which FR-HR-092 says the MD signs).

**Greppable progress check:** `grep -rl HasActiveApprovalWorkflowAsync src/ErpSystem.Core/Services/HR/`
lists the services that have been through this.

---

## Lane A — permissions and authorization

Nine findings, one shape: the page and the API disagree about who may act, or a policy is missing
altogether. Lane A is first because Lane C's reconcile work is unreachable until G-3.1 is closed.

| Gap | Change |
|---|---|
| G-3.1 | `AdministerRecruitment` joins `HrStaffGrants` in `HrPermissions.cs`. Unblocks Reconcile for the HR role — and with it G-14.2, which is only as good as the last reconcile. |
| G-4.8 | Follows from G-3.1: delete requisition / cost / comment / attachment become reachable for HR. |
| G-2.3 | `/hr/recruitment` gates its HR cards and counters on `hasAnyPermission([ViewRecruitment, …])`, not `hasAnyRole(['SuperAdmin','HR'])`. |
| G-3.10 | Same change on `/hr/recruitment/establishment` — header buttons and row actions. |
| G-9.1 | The interview area's `IsHr` becomes a permission check (`HR.Recruitment.Write`/`Admin`), with the HR role still satisfying it. `TenantAdmin`, `Admin` and `HR User` stop being refused. |
| G-15.5 | `[Authorize(Policy = "InternalOnly")]` added to the dashboard controller, matching every other recruitment controller. |
| G-3.3 | `raise-requisition` gets `HR.Policy.RecruitmentWrite`. The ungated *create* endpoint stays ungated — that is deliberate, line managers raise requisitions — but pinning a vacancy to `RequisitionRaised` is not the same act. |
| G-8.2 | The application decision bar renders behind `hasAnyPermission`, as *Record an application* on the same page already does. |
| G-7.5 | The candidate page's "HR-only" comment corrected to describe the permission gate that exists. |

## Lane B — approval integrity

| Gap | Change |
|---|---|
| G-4.1 | Requisition submit: when no `StaffRequisition` definition is active, land at `Submitted` and leave approval to `ApproveAsync`, which carries the "you cannot approve what you raised" rule. |
| G-10.1 | Offer submit: the same, landing at `PendingApproval`. This is the step that authorises binding terms to an outsider. |
| G-5.4 | Vacancy approve gets the segregation-of-duties check it has never had, and `PendingApproval` becomes reachable (with G-5.3). |
| — | The three screen comments claiming Submit is "inoperable until a definition is published" are rewritten. Per Appendix C's first pattern, the prose was the confident half. |

## Lane C — the establishment register

| Gap | Change |
|---|---|
| G-3.2 | The employee termination / retirement / promotion / transfer paths log a `PositionVacancy`. |
| G-3.4 | Those call sites carry the real `Reason`, `VacatedByEmployeeId` and `VacatedDate` — so "Since" means when the post fell empty, not when a button was pressed. |
| G-3.5 | `Classification` computed three ways against establishment, so `NoShortfall` and `OverEstablishment` can occur and `noShortfallOrOver` stops being 0. |
| G-3.6 | Anticipated vacancies detected from a notice/last-working date; `ExpectedVacancyDate` written. |
| G-3.7 | The status dropdown stops offering `RequisitionRaised` and `Filled`, which the service always refuses. |
| G-3.8 | The override dialog requires a reason when the target status is `Closed`, as the dedicated close path does. |
| G-3.9, G-4.4 | All three headcount call sites move onto `HrServingEmployees.Predicate`. Three definitions of "filled" become one. |

## Lane D — expiry and the nightly sweep

| Gap | Change |
|---|---|
| G-2.4 | `RecruitmentLifecycleSweepBackgroundService` marks lapsed `Sent` offers `Expired`. |
| G-6.2 | The same sweep expires adverts past `ExpiryDate`. |
| G-2.2 | The landing tile's "live adverts" query respects `ExpiryDate`. |
| G-14.1 | Follows: the analytics Expired bucket stops being structurally zero and Pending stops absorbing lapsed offers. |
| G-15.2 | The dashboard's "offers pending response" filters on `IsLatestVersion` and bounds expiry, as analytics already does. |
| G-10.2 | `ReviseOfferAsync` moves the superseded version out of a live status. |
| G-6.3 | The overdue-adverts view gets an Expire action — the cure for the thing it exists to diagnose. |
| G-11.4 | The clearance queue gets an action on the rows it finds. |

## Lane E — the vacancy is editable

| Gap | Change |
|---|---|
| G-5.1 | `/hr/recruitment/vacancies/[id]/edit`, calling the `PUT` that has always existed. Deadline, pipeline and blind screening become changeable, and the three screens that tell users to change them stop lying. |
| G-5.2 | `TestScoreWeight` and `InternalCandidateBoostPoints` added to the create and update DTOs and the form. Two dead branches of the scoring algorithm come alive. |
| G-5.3 | `OnHold`, `PendingApproval` and `Rejected` become reachable. |
| G-5.5 | `CloseAsync` goes through `GuardTransition` so a Filled vacancy cannot be cancelled. |
| G-5.6 | Auto-created adverts get a composed body rather than an empty one under a reference number. |
| G-5.7 | The "Advance to…" dropdown offers only legal transitions. |
| G-4.7 | The requisition edit page gets the `numberOfPositions >= 1` check its sibling has. |
| G-4.6, G-5.8, G-10.3 | Status-filtered reads are paged. The offers list stops returning every offer in the tenant by default. |

## Lane F — the candidate register

| Gap | Change |
|---|---|
| G-7.1 | Eighteen write endpoints wired into the six read-only tabs: qualifications, work history, referees, skills, languages, interests. |
| G-7.2 | Email uniqueness enforced on update, and `IX_JobCandidate_Email` made unique (migration). |
| G-7.3 | `Nationality` added to the create and update DTOs and the form. |
| G-7.4 | The two superseded talent-pool endpoints retired. |
| G-7.6 | Name search on the register, reusing the talent-pool screen's server-side multi-field search. |
| G-8.5 | Inline candidate create in the walk-in dialog. |
| G-11.3 | A referee picker on *Record a reference*, writing `RefereeId` beside the snapshot fields. |
| G-12.4 | Follows from G-7.1: a hand-typed candidate can carry referees into employment. |

### Lane F findings — two gaps were already closed, one is deferred

**G-7.1 and G-12.4 were stale.** The guide records `CandidateSubResourceTabs.tsx` as *"680 lines
containing `useQuery` and nothing else — no `useMutation`, no `mutationFn`, no buttons"*, with all
six tabs display-only and eighteen write endpoints unreachable. **That is not what the file
contains.** Every one of the six — qualifications, work history, referees, skills, languages,
interests — rides `ResourceCollectionTab` with `create`, `update` and `remove` wired to the
matching service method. The work landed in round 3, lane C2, which closed days before the guide
walk; the walk appears to have read a stale checkout.

G-12.4 was recorded as a downstream consequence of G-7.1 ("HR could never add a referee, so a
hand-typed candidate arrives with none"), so it goes with it.

*The lesson is the guide's own*: where a gap rests on "X does not exist", read the file before
acting on it. Two of seventy findings were already corrected in-document for exactly this reason;
these are the third and fourth.

**G-7.2 is closed in the writer, deferred in the schema.** `UpdateAsync` now refuses a duplicate
email, which is what stops new ones. **The unique index was deliberately not added.** Soft delete
leaves rows in place and they still occupy a unique index, so a tenant carrying historical
duplicates — or reusing a deleted candidate's address — would fail the migration. Before adding it:

```sql
SELECT Email, COUNT(*) FROM JobCandidates
WHERE TenantId = @tenant GROUP BY Email HAVING COUNT(*) > 1;
```

Run that on the live tenant, resolve what it returns, then scaffold a filtered unique index
(`WHERE IsDeleted = 0` still does not help — deleted rows are the risk). Until then the database
does not enforce it and the service does.

## Lane G — recall reaches the browser

| Gap | Change |
|---|---|
| G-4.3, G-10.4 | `useWorkflowRecord` gains a recall command; the requisition and offer pages gain the button. Both server halves are already built and enforce requester-only. |

## Lane H — steps hand over to the next

Appendix C's sixth pattern, and the largest lane.

| Gap | Change |
|---|---|
| G-12.1 | `ConfirmStartAsync` marks the application `Hired` and moves `JobVacancy.HireCount`. |
| G-12.2 | A `Cancelled` hire can no longer be confirmed — server guard and client guard. |
| G-9.4 | Closing an interview advances its applications rather than leaving them at `InterviewScheduled`. |
| G-9.2 | `JobInterviewee.Outcome` acquires a reader. |
| G-9.3 | Recording the panel's verdict requires more than read access. |
| G-8.1 | One definition of `IsShortlisted`, so HR and the candidate are told the same thing. |
| G-8.3 | A stage move reconciles with the decision status instead of silently overwriting it. |
| G-8.4 | Offer creation gates on the step before it. |
| G-4.5 | The two `PositionsFilled` writers reconciled, so a manual entry plus a confirmed start stops double-counting. |
| G-13.1 | `Converted` set when a pooled candidate is hired, dated by a real `ConvertedDate` rather than `UpdatedAt`. |
| G-13.3 | A converted candidate leaves the pool without the conversion being overwritten by `Expired`. |

## Lane I — queues, counters and tiles

| Gap | Change |
|---|---|
| G-11.1 | The clearance queue's live view shows what it means to show — a check set started and not finished — rather than defaulting to a status nothing writes. |
| G-11.2 | Accepting conditionally with no check set stops being a silent dead end. |
| G-6.1 | `JobPosting.ApplicationCount` derived from the link that already exists. |
| G-2.1 | The landing tiles distinguish a failed query from a pending one. |
| G-2.5 | The five tiles carry the `href` `MetricTiles` has always supported. |

## Lane J — reporting and the rest

| Gap | Change |
|---|---|
| G-15.1 | The pipeline card counts people in all four bars, on one window. |
| G-15.3 | `CandidateName` stops carrying the literal text "Round N". |
| G-15.4 | The shortlisting SLA card fires on `ShortlistingDeadline`. |
| G-14.3 | The two definitions of "an open seat" reconciled or labelled. |
| G-14.4 | The funnel's form stops contradicting its own caption. |
| G-13.2 | The match rubric stops awarding points for missing data. |
| G-13.4 | Segment metadata merged rather than replaced. |
| G-9.5 | "Panel score" says which panel. |
| G-9.6 | One screen showing "interviews I am involved in". |
| G-10.5 | The salary-band check says when it did not run. |
| G-6.4 | The print-advert composition block gets the UI it was typed all the way to the browser for. |
| G-12.3 | The confirmed-hire recovery path documented on the screen. |

---

## Working rules for this programme

1. **Verify before fixing.** Two findings in the guide were recorded wrongly (G-4.5, G-10.2), both
   because a search stood in for reading the code. Where a gap rests on *"X is never written"*,
   read the writer.
2. **Read the readers.** A defect in a writer does not make every reader wrong — the analytics
   service is already immune to four of these findings. Check each reader before changing it.
3. **The guide is the deliverable too.** Every closed gap is rewritten in
   `HR-RECRUITMENT-SYSTEM-GUIDE.md` — in its chapter and in Appendix C — to describe the code as
   it then stands. A gap list that outlives its gaps is worse than none.
4. **Harness each lane twice.** Per the area surveys: fewer assertions with zero failures is a
   regression signal, not a pass.
