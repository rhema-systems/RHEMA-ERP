"""
Builds docs/HR-CLOSURE-LEDGER.md — the checkable master list for the HR Closure Programme.

Machine-derived sections come from the instrument output so nothing is hand-missed; the
disposition column is authored here (DISPOSITIONS below) because only a human can say whether
an unreachable endpoint is a gap or a deliberate boundary.

Run 01, 02 and 03 first, then this.
"""
import os, re, json, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "out")
LEDGER = os.path.join(ROOT, "docs", "HR-CLOSURE-LEDGER.md")

routes = json.load(open(os.path.join(OUT, "hr_routes.json")))
unwired = json.load(open(os.path.join(OUT, "unwired.json")))
inst2 = json.load(open(os.path.join(OUT, "instrument2.json")))
dto_gaps = json.load(open(os.path.join(OUT, "dto_gaps.json")))

UNWIRED_KEYS = {(u["file"], u["verb"], u["route"]) for u in unwired}
HIGH = [r for r in inst2 if r["leaf_absent"] and (r["file"], r["verb"], r["route"]) in UNWIRED_KEYS]
HIGH_KEYS = {(r["file"], r["verb"], r["route"]) for r in HIGH}

# ---------------------------------------------------------------------------
# Authored dispositions. Anything not listed defaults to REVIEW.
#   BUILD       - real gap, needs a UI
#   INTENTIONAL - correct as-is; never report again
#   FALSE       - instrument artefact, not a real gap
#   DECIDE      - needs a call from the user or TDC
# ---------------------------------------------------------------------------
DISPOSITIONS = {
    "CompanyScheduleController.cs":
        ("DONE", "Built 2026-08-28. All 40 writes wired; 8 screens under /hr/company-schedule and "
                 "/administration/hr/company-schedule. Actor fix D-01 cleared first, and the three "
                 "StationId FKs repointed from the vestigial WorkStation to the live Location tree."),
    "StaffDisciplineLookupController.cs":
        ("BUILD", "Offence catalogue is GET-only in the UI; clients cannot maintain their own offence library."),
    "TalentPoolController.cs":
        ("DONE", "Built 2026-08-30 (recruitment closure, slice 1). The pool CRM lives at /hr/recruitment/talent-pool with pool/engagement tabs on candidate detail and stage-owner/pool-match tabs on vacancy detail; all 12 writes left the queue on their own. Verified by hr-recruitment/slice-e, 102 assertions ×2. Ten backend defects cleared first — the 21-of-40-field pool read, the tenantless paging query, the dead SegmentIds, the never-populated BySegment, the bulk false successes, and the missing [RecruitmentBusinessRules] among them."),
    "EmployeeBanksController.cs":
        ("DONE", "Built 2026-08-31 (lane 2). /administration/hr/banks and banks/[id] cover all TEN "
                 "writes — bank add/edit/retire/restore/delete and the same five for branches "
                 "— with a nav entry. The queue counted 4 because only those agreed across both "
                 "instruments; the family had no screen of any kind. One backend defect cleared "
                 "first: BranchCount was hardcoded ToDto(0) on four of the five reads, so a list "
                 "would have shown every bank with zero branches."),
    "LocationContactController.cs":
        ("DONE", "Built 2026-08-31 (lane 2). A contacts panel on the location edit screen. "
                 "⚠ The create had NEVER ONCE SUCCEEDED: the service did not stamp TenantId, so "
                 "every insert died on FK_LocationContacts_Tenants_TenantId and the controller "
                 "returned a bare 500. Found by the payload probe running it for the first time — "
                 "a dead path cannot fail visibly."),
    "EmployeeCareerPathController.cs":
        ("DONE", "Built 2026-08-31 (lane 2). The timeline on /hr/movements/career-paths/[employeeId] "
                 "can be annotated, corrected and added to. The edit offers only the four fields the "
                 "server takes — end date, current flag, achievements, key projects — because the "
                 "position, unit and salary are what the movement engine wrote and are corrected by "
                 "correcting the movement."),
    "SalaryNotchesController.cs":
        ("INTENTIONAL", "Decided 2026-08-28: read-only by intent — payroll owns the grade master."),
    "SalaryLevelsController.cs":
        ("INTENTIONAL", "Decided 2026-08-28: read-only by intent — payroll owns the grade master."),
    "SalaryGradesController.cs":
        ("INTENTIONAL", "Decided 2026-08-28: read-only by intent — payroll owns the grade master."),
    "PayrollController.cs":
        ("INTENTIONAL", "Another team's module; HR integrates read-only."),
    # CandidatePortalController.cs / CandidatePortalAuthController.cs were DELETED 2026-08-30 —
    # the candidate portal (own PortalBearer auth) is retired; candidates self-register on the
    # main JWT scheme with the Candidate role and are served by the main-scheme candidate surface.
    "PublicRecruitmentController.cs":
        ("BUILD", "Public job board / anonymous apply."),
    "OfferResponseController.cs":
        ("DONE", "Built 2026-08-31 (recruitment closure). Kept by decision — offer emails link to it — and the page now exists at the exact path those emails carry, /careers/portal/offer-response?token=…: validate, respond once, token consumed. A registered candidate sees the same offer in their portal; this is the door for the one who has not signed up."),
    "ClientTimesheetConfirmationController.cs":
        ("DONE", "Built 2026-08-31 (consultant-client closure). The page now exists at the exact path the confirmation emails have always carried, {PortalUrl}/client-timesheet/confirm/{token}: validate (stamps Viewed), review the entries, confirm or reject with notes; expired and already-responded links render read-only. Kept by decision alongside the logged-in portal — the door for the contact who has not completed an invite. Verified by dev-harness/hr-consulting (78 assertions ×2)."),
    "ConsultantClientPortalController.cs":
        ("DONE", "Rebuilt 2026-08-31 on the main JWT scheme (consultant-client closure): the ConsultantClient role, invite-only, fenced by ConsultantClientAccessMiddleware + the ConsultantClientOnly policy, authorised inside by ConsultantClientContact rows. Dashboard is multi-client (one section per client the contact serves); confirm/reject advance the timesheet to ClientConfirmed/ClientRejected. Surfaced at /external-portal/client-timesheets inside the external-portal shell. Verified by dev-harness/hr-consulting (78 assertions ×2)."),
    # ConsultantClientPortalAuthController.cs was DELETED 2026-08-31 — the consultant-client
    # portal was the PortalBearer scheme's LAST tenant, and both retired together. Contacts are
    # invited by HR (ConsultantClientContactService) onto main-scheme Identity accounts; setup
    # completes via api/auth/complete-client-setup (the emailed token is the mailbox proof); the
    # anonymous register-with-client-code flow retired unbuilt by decision (invite-only).
    "HrLegacyFileMigrationController.cs":
        ("INTENTIONAL", "One-off ops tool, invoked by script."),
    "EmployeesController.cs":
        ("FALSE", "Fully wired through the path-builder helper employeeService.sub(id, 'contacts'). Instrument 01 cannot resolve a method call."),
    "PerformanceImprovementPlansController.cs":
        ("FALSE", "Classified 2026-08-29: nothing here is real. 18 flags are the api/PerformanceImprovementPlans "
                  "alias of api/Pip and 1 is the DocumentUploadField artefact; complete duplicates outcome, and "
                  "the two review-meeting writes duplicate api/PipMeeting. See D2."),
    "JobAnalysisController.cs":
        ("DONE", "The child collections were built 2026-08-29 (slice 13) and the manpower budget "
                 "edit/delete plus line edit/delete on 2026-08-31 (lane 2). ⚠ The budget update is "
                 "a REPLACE — the DTO names every figure — so the dialog seeds from the budget and "
                 "sends the whole set; omitting one writes a zero over it. Edits are Write-tier and "
                 "both deletes are HR.ManpowerBudget.Admin, established by a 403 rather than "
                 "assumed."),
    "StaffDisciplineSupportController.cs":
        ("BUILD", "Action steps and legal reviews are displayed but can never be recorded."),
    "StaffDisciplineSubEntityController.cs":
        ("BUILD", "The corrective-action note here was stale \u2014 those were built in slice 9. The "
                  "investigation and hearing were built in slice 11 (2026-08-30) and have left the "
                  "queue. The 10 that remain are the sanctions, blocked on D-18."),
    "MedicalInsuranceController.cs":
        ("INTENTIONAL", "⚠ This row read `BUILD` with a note naming four collections that slice 4 built on 2026-08-29; the note was never updated and would have sent someone to build them twice. Corrected 2026-08-30 by reading the 9 flags rather than the note: SEVEN are the employee-policy and dependent family, which is D-13 — deferred by decision, not a coverage gap — and the other two are the provider-document pair, one the deliberately-unwired metadata route and one the hrDocumentService.upload artefact. There is no work here."),
    "JobVacancyController.cs":
        ("FALSE", "Stage assignments were built 2026-08-30 (recruitment closure, slice 1 — the Stage owners tab on vacancy detail; the eighth soft-delete/unique-index face was fixed with revive-on-upsert first) and left the queue on their own. The one remaining flag is the attachments POST, wired through hrDocumentService.upload — the helper artefact instrument 01 cannot resolve."),
    "AwardsController.cs":
        ("REVIEW", "⚠ The note here read \"nomination attachments — edit and delete\", which the 15 flagged routes contradict: they include the nomination create, update and submit, the target, team-nominee, contribution and committee-member edits, and the long-service create, update and sweep. Area 14 shipped 17 screens and 618 assertions, so most of these are probably helper-upload artefacts or wired through a path builder — but that is a guess, and a controller is rarely one verdict. **Classify endpoint by endpoint before treating this as a build block.**"),
    "EmployeeCompetencyController.cs":
        ("BUILD", "Batch assessment."),
    "SeparationsController.cs":
        ("BUILD", "Clearance — refresh assets."),
    "AppraisalWorkflowController.cs":
        ("INTENTIONAL", "Checked 2026-08-29: they do. The appraisal screens move status through the named "
                        "transitions on api/PerformanceAppraisals, each with its own preconditions."),
    "AssetsController.cs":
        ("DONE", "Built 2026-08-30 (slice 18, 43 assertions). Assignments, attribute definitions, maintenance records, requisitions, transfers and surcharges are all correctable now, and the surcharge gained the delete and the recall it never had. No backend change was needed: every one of the eight already stamped its actor and every screen already fetched by id, so both standing checks passed before any UI. The 4 remaining flags are 2 upload artefacts and the 2 employee-portal duplicates."),
    "SuccessionDocumentsController.cs":
        ("DONE", "Built 2026-08-29 to clear D-14; wired 2026-08-30 by slice 19. It still reads as flagged because the client calls it through hrDocumentService.upload(endpoint, file, fields) - the helper-indirection artefact, the single largest false-positive source in this queue - and its download sibling is a GET, which instrument 01 skips outright."),
    "SuccessionPlanController.cs":
        ("DONE", "Built 2026-08-30 (slice 19). The plan's actions and competency requirements are authorable from the detail screen, and the documents tab drives the gated upload, the token-bearing download and the Admin-tier delete. Three backend defects had to clear first, all found by the standing checks and none by a probe failing: the per-plan actions read was a summary missing nine of the update payload's thirteen fields (D-19), the assigner was assertable by the request body (D-20), and removing a competency requirement made it permanently unrequirable (D-21). The one remaining flag is the metadata-only document POST, deliberately unwired."),
    "MedicalClinicalController.cs":
        ("DONE", "Built 2026-08-29 (slice 6). All three clinical entities are now correctable and removable from the clinical screen; the one remaining flag is the free status set, which the screen deliberately does not call. D-16 was cleared first."),
    "MedicalExpenseClaimsController.cs":
        ("DONE", "Built 2026-08-29 (slice 6). The claim, its lines and its documents are all correctable and removable; the one remaining flag is the upload artefact. The edit form also closes section E's AdmissionStart/AdmissionEnd pair."),
    "SuccessionCandidatesController.cs":
        ("DONE", "Built 2026-08-30 (slice 19). A candidate's own files hang off a Documents dialog on the successors tab, sharing the one panel the plan and the talent-pool member use — one table and one DTO serve all three owners, so one component does. The remaining flags are the metadata-only POST and the development-activity trio that duplicates api/succession-development."),
    "StaffTravelRequestsController.cs":
        ("DONE", "Built 2026-08-30 (slice 21). ⚠ The queue said a group 'cannot be edited, deleted, or have a participant removed'; in fact group travel had NO screen of any kind - it could not be created, listed or opened either, and the reads are invisible to instrument 01 while the writes had client methods. /hr/travel/groups and /[id] exist now. One defect cleared first: UpdateGroupTravelAsync mapped an include-less entity, so the edit response reported ZERO participants on a group that has them."),
    "TalentPoolsController.cs":
        ("DONE", "Built 2026-08-30 (slice 19). A pool member's documents open from the members table on /hr/succession/pools/{id}, on the shared succession-document panel. The remaining flags are the metadata-only POST and the development-activity duplicate."),
    "TrainingServiceBondsController.cs":
        ("DONE", "Built 2026-08-31 (lane 2). Raise, correct and delete on /hr/service-bonds. The "
                 "correction is the one that mattered: amount and duration are copied from the "
                 "programme when the server mints the bond, so a programme priced wrongly mints "
                 "every bond wrongly — and the only remedy was to delete and re-raise, throwing "
                 "away the acceptance the employee had already signed. The delete is offered only "
                 "before acceptance; an accepted bond is waived instead."),
    "PositionVacanciesController.cs":
        ("DONE", "Built 2026-08-31 (lane 2). Close, annotate and set-status on the establishment "
                 "screen, each dialog saying what it is FOR, because reconcile normally owns all "
                 "three: closing is for a post the organisation has decided not to fill, which "
                 "reconcile cannot see, so it sat open for ever. ⚠ The notes dialog fetches the "
                 "BY-ID read — PositionVacancySummaryDto has no `notes`, so seeding from the row "
                 "would open empty and save a blank over whatever was written."),
    "CheckInsController.cs":
        ("DONE", "Built 2026-08-31 (lane 2). An attachments card on the check-in detail, sharing "
                 "one panel with the appraisal family — the two are the same four routes with a "
                 "different prefix. No backend change: the endpoint was already IFormFile through "
                 "the controlled gate, so the FilePath DTO beside it is a leftover."),
    "NHISClaimsController.cs":
        ("DONE", "Built 2026-08-30 (slice 19). The claims list gained a Documents dialog at every status - the scheme's rejection letter arrives after the decision and the attendance record before it - driving the gated upload, the download and the Admin-tier delete. No backend change was needed: D-14 had already built and harness-verified the whole transport in the medical slice-5 run, and it had simply never had a caller. The one remaining flag is the metadata-only POST."),
    "StaffDemotionsController.cs":
        ("DONE", "Built 2026-08-30 (slice 22). The Admin-tier delete is on the sub-type panel, and the employee's own response to a demotion notice is on /me/movements/{id} — which is what lets a demotion ever LEAVE HR's pending-appeals queue (that queue lists demotions awaiting an answer, not appeals filed; see D-37). ⚠ The respond endpoint needed no change: it holds no policy beyond InternalOnly by design and the service refuses anyone but the demoted employee, so unlike the travel acknowledgement its gate and its rule already agreed. What was missing was a screen for the one person allowed to use it."),
    "StaffTravelPoliciesController.cs":
        ("DONE", "Built 2026-08-30 (slice 20). ⚠ The queue flagged ONE endpoint here and the gap was the whole subsystem: policy create, edit and delete, the entire rules register and both halves of the exception flow had client methods and no screen calling any of them, so instrument 01 counted them wired. A service method is not a user being able to reach anything — the area-16 lesson, and the reason the earlier note was wrong to call the decide queue 'wired'. **Delivered: policy authoring** — /new and /[id] exist (the two routes the register linked to since it shipped, both 404s that slice 10 removed), and a policy is drafted, corrected, approved, withdrawn and deleted-while-draft from them. **Deliberately NOT delivered: rule authoring and the whole exception flow** — see D-29. Four backend defects cleared first: D-23, D-24, D-25, D-26."),
    "AppraisalCycleTargetController.cs":
        ("INTENTIONAL", "Classified 2026-08-29: all 3 duplicate the cycle-nested api/AppraisalCycle/{}/targets routes, which are wired."),
    "InterviewQuestionPresetController.cs":
        ("INTENTIONAL", "Classified 2026-08-29: the preset PUT is a replace-set over its items, so the per-item routes are a second writer over the same rows."),
    "PositionCompetencyController.cs":
        ("INTENTIONAL", "Classified 2026-08-29: superseded by the wired position/{}/bulk-set replace-set."),
    "ConsultantClientsController.cs":
        ("INTENTIONAL", "Classified 2026-08-29: api/client-engagements is the flat controller for the same entity and its PUT and DELETE are wired."),
    "CompetencyController.cs":
        ("FALSE", "Classified 2026-08-29: both flags are the api/competency-skill-indicators alias of api/competencies routes that are wired."),
    "PeerNominationController.cs":
        ("INTENTIONAL", "Classified 2026-08-29: batch nomination lives on the appraisal and the single-row client deliberately offers only read, remove and send-invitation."),
    "JobCandidateController.cs":
        ("FALSE", "The interest edit was built 2026-08-30 (recruitment closure, slice 1 — the UI comment claiming 'no update endpoint' was false) and left the queue. The remaining flag is the documents POST, the hrDocumentService.upload artefact."),
    "LeavesController.cs":
        ("INTENTIONAL", "Classified 2026-08-29: the attachment POST is the helper artefact and the balance-scoped adjustment is superseded by the flat standalone route."),
    "AppraisalNotificationsController.cs":
        ("INTENTIONAL", "Classified 2026-08-29: /me wires the token-scoped mark-all-read; this is the employee-id-keyed variant."),
    "LocationController.cs":
        ("INTENTIONAL", "Classified 2026-08-29: the wired PUT reparents with the identical guards, verified line by line against MoveLocationAsync."),
    "PayComponentsController.cs":
        ("INTENTIONAL", "The endpoint's own summary says it: create the component in Payroll, then sync."),
    "AwardsMeController.cs":
        ("BUILD", "Classified 2026-08-29: real - a score the caller gave cannot be revised."),
    "JobInterviewController.cs":
        ("DONE", "Built 2026-08-30 (recruitment closure, slice 1): one edit dialog on the interview panel serves internal and external rows — the internal updatePanelist had a client method and no caller, so both were dark. The service also gained the UpdatedAt/UpdatedBy stamps it silently lacked."),
    "LeaveTypesController.cs":
        ("DONE", "Built 2026-08-31 (lane 2). A retire action on the leave-types list, with a "
                 "confirmation that says plainly there is no delete — 405 by design, because a "
                 "leave type is referenced by every request ever made against it."),
    "StaffActingAppointmentsController.cs":
        ("DONE", "Built 2026-08-30 (slice 22). The edit is on /hr/movements/acting, offered on anything not yet Completed because the API refuses it after that. It carries allowanceCalculation, which section E listed as settable by no form — the TypeScript type had only the resolved NAME, so a control had nothing to bind to."),
    "StaffPromotionsController.cs":
        ("DONE", "Built 2026-08-30 (slice 22). The Admin-tier delete is on the movement's sub-type panel, so a detail recorded against the wrong movement is correctable at last. Two defects cleared first: D-34, where deleting a detail made its movement permanently unable to carry one again, and a write response that named neither the movement nor the employee."),
    "StaffTransfersController.cs":
        ("DONE", "Built 2026-08-30 (slice 22). The Admin-tier delete is on the movement's sub-type panel, so a detail recorded against the wrong movement is correctable at last. Two defects cleared first: D-34, where deleting a detail made its movement permanently unable to carry one again, and a write response that named neither the movement nor the employee."),
    "StaffSecondmentsController.cs":
        ("DONE", "Built 2026-08-30 (slice 22). The Admin-tier delete is on the movement's sub-type panel, so a detail recorded against the wrong movement is correctable at last. Two defects cleared first: D-34, where deleting a detail made its movement permanently unable to carry one again, and a write response that named neither the movement nor the employee."),
    "StaffTravelComplianceController.cs":
        ("DONE", "Built 2026-08-30 (slice 21). The destination-alert feed is authorable at /administration/hr/travel/alerts, alerts are sent to a named traveller from the trip's compliance tab, and the traveller reads and acknowledges them on /me/travel. Three defects cleared first - D-31, D-32 and the notification create response, which resolved none of its three names."),
    "TrainingCompletionsController.cs":
        ("DONE", "Built 2026-08-31 (lane 2). A Completion tab on the schedule: tick who finished, "
                 "set the date and outcome, record in one pass. ⚠ The server checks only for a "
                 "completion that already exists — not that the nomination belongs to this "
                 "schedule, nor that anyone was ever confirmed (the probe recorded a completion "
                 "against a DRAFT nomination without complaint) — so the screen is the constraint, "
                 "and it shows every skipped row with its reason rather than reporting success for "
                 "work it did not do."),
    "TrainingNominationsController.cs":
        ("DONE", "Built 2026-08-31 (lane 2). A 'Check availability' button in the nominate dialog, "
                 "showing leave, travel and other live nominations over the schedule's dates. "
                 "Advisory rather than a gate — leave gets cancelled, travel moves — but it "
                 "distinguishes 'checked and clear' from 'not checked', which is the whole value. "
                 "⚠ Proven both ways: a DRAFT nomination elsewhere is correctly not a clash, the "
                 "same one live is. An empty list would otherwise have been a vacuous green."),
    "CalibrationSessionsController.cs":
        ("FALSE", "hrDocumentService.upload artefact."),
    "UnitGoalsController.cs":
        ("FALSE", "hrDocumentService.upload artefact."),
    "AppraisalReviewEventsController.cs":
        ("FALSE", "hrDocumentService.upload artefact."),
    "MedicalSelfServiceController.cs":
        ("FALSE", "hrDocumentService.upload artefact."),
    "EmployeeHealthController.cs":
        ("FALSE", "hrDocumentService.upload artefact."),
    "JobOfferController.cs":
        ("FALSE", "hrDocumentService.upload artefact (both letter uploads)."),
    "PreEmploymentCheckController.cs":
        ("FALSE", "hrDocumentService.upload artefact (both document routes)."),
    "StaffRequisitionsController.cs":
        ("FALSE", "hrDocumentService.upload artefact."),
    "JobPostingController.cs":
        ("FALSE", "hrDocumentService.upload artefact."),
    "SheControlledDocumentController.cs":
        ("FALSE", "hrDocumentService.upload artefact."),
    "SheEnvironmentalComplianceController.cs":
        ("FALSE", "hrDocumentService.upload artefact."),
    "HrAnnouncementsController.cs":
        ("FALSE", "DocumentUploadField artefact."),
    "HrLetterRequestsController.cs":
        ("FALSE", "DocumentUploadField artefact."),
    "HrPoliciesController.cs":
        ("FALSE", "DocumentUploadField artefact."),
    "MyProfileController.cs":
        ("FALSE", "DocumentUploadField artefact."),
    "StaffMovementsController.cs":
        ("INTENTIONAL", "Classified 2026-08-29: the upload route is wired through hrDocumentService and the "
                        "metadata route beside it deliberately refuses every file-location field. Neither is a gap."),
    "PerformanceAppraisalsController.cs":
        ("BUILD", "Classified 2026-08-29: 10 of 15 are raw model CRUD superseded by the workflow routes the "
                  "screens drive. The 5 real ones are the appraisal header edit and delete, the attachment pair "
                  "(the gated upload was purpose-built here and nothing calls it) and the employee response, "
                  "whose cycle setting therefore does nothing. See D2."),
}

# ---------------------------------------------------------------------------
# Endpoint-level dispositions for the section D hand-review queue.
#
# A controller is rarely one verdict. `AssetsController` mixes two path-builder
# artefacts, two employee-portal duplicates and eight genuine gaps; `PerformanceAppraisals`
# mixes ten superseded raw-CRUD routes with five real ones. So the queue is classified per
# endpoint, keyed `(controller file, VERB, route with {} for every parameter)` — the same
# normalisation the checklists use.
#
# Verdicts:
#   BUILD       - real gap, needs a UI
#   INTENTIONAL - reachable another way, or a deliberate boundary; never report again
#   FALSE       - instrument artefact; the endpoint IS wired
#
# Every entry below was hand-verified in source on 2026-08-29 by reading the controller,
# the DTO and the frontend service that would call it. Anything not listed defaults to the
# controller-level DISPOSITIONS entry, then to REVIEW.
# ---------------------------------------------------------------------------

# The three artefact classes, named once so the notes stay short.
_HELPER = ("FALSE", "Wired through hrDocumentService.upload(endpoint, file, fields); "
                    "instrument 01 cannot resolve a path passed to a helper.")
_FIELD = ("FALSE", "Wired through the shared `DocumentUploadField` component's `endpoint` prop; "
                   "the apiService.post call is inside the component, not at the call site.")
_ALIAS = lambda real: ("FALSE", "Alias route. The controller carries two `[Route]` attributes, so every "
                                "action yields two rows; the frontend calls %s." % real)

ENDPOINT_DISPOSITIONS = {}


def _d(file, rows):
    for verb, route, verdict in rows:
        ENDPOINT_DISPOSITIONS[(file, verb, route)] = verdict


# ── Awards — 15 flagged, classified 2026-08-31 (lane 2 triage) ───────────────
# The last controller-level `REVIEW` row in the queue, and the note beside it guessed wrong: it
# supposed "most of these are probably helper artefacts", and only ONE is. Eleven are real.
_d("AwardsController.cs", [
    ("POST", "api/Awards/types/{}/long-service/sweep",
     ("FALSE", "Wired as `runLongServiceSweep`, which appends `?asOf=` — the interpolated "
               "query-string artefact: instrument 01 folds the `${query}` into the path segment "
               "instead of dropping it. The preview beside it is a GET, which 01 skips outright.")),

    # The desk's own nomination routes. Area 14 decided the nominator acts from their own surface,
    # where entitlement is read off the record rather than asserted by a caller.
    ("POST", "api/Awards/nominations",
     ("INTENTIONAL", "Nominating is the nominator's act, not the desk's: the frontend calls "
                     "`POST api/awards/me/nominations`, which takes no employee id and so cannot "
                     "nominate in someone else's name. Same shape as the employee-portal principle "
                     "recorded for Assets.")),
    ("PUT", "api/Awards/nominations/{}",
     ("INTENTIONAL", "Served by `PUT api/awards/me/nominations/{}` for the same reason.")),
    ("POST", "api/Awards/nominations/{}/submit",
     ("INTENTIONAL", "Served by `POST api/awards/me/nominations/{}/submit` for the same reason.")),

    # The missing edit, four times — the queue's dominant real shape. Create and delete are wired
    # in every one of these; only the correction is absent, so a mistake must be deleted and redone,
    # which changes what the record says happened.
    ("PUT", "api/Awards/targets/{}",
     ("DONE", "Built 2026-08-31 (lane 2). The scope dialog now edits as well as adds, on BOTH tables — the electorate table had no actions column at all, so a rule about who may vote could be added and then neither corrected nor removed. ⚠ The edit has teeth: flipping isExclusion made the probe's own nominee ineligible and the nomination was refused quoting the reason text back. ORIGINAL: A cycle target can be created (`POST targets`) and deleted (`DELETE targets/{}`) "
               "and not corrected.")),
    ("PUT", "api/Awards/team-nominees/{}",
     ("DONE", "Built 2026-08-31 (lane 2). A team member's role, contribution summary and share are correctable. The share decides what each member is PAID. ORIGINAL: A team member's role, contribution summary and reward percentage cannot be "
               "corrected once named; create and delete are both wired.")),
    ("PUT", "api/Awards/contributions/{}",
     ("DONE", "Built 2026-08-31 (lane 2). Reworded in place on the nomination screen rather than deleted and retyped, which would lose who recorded it and when. ORIGINAL: A contribution can be added and removed, never reworded.")),
    ("PUT", "api/Awards/committee-members/{}",
     ("DONE", "Built 2026-08-31 (lane 2). Correcting a member is not deactivating them: deactivation ends their entitlement to score and keeps their scores attributable; this fixes what the record says. ORIGINAL: A committee member can be added, deactivated and deleted, but their role on the "
               "committee cannot be corrected.")),

    ("POST", "api/Awards/long-service",
     ("DONE", "Built 2026-08-31 (lane 2). By hand, for the award the sweep cannot see — a missing service date, or one agreed separately. The sweep remains the normal path. ORIGINAL: A long-service award can only come into being through the sweep. One granted with "
               "the wrong value has no manual counterpart to correct it against — and it is money, "
               "the same shape as TrainingServiceBonds.")),
    ("PUT", "api/Awards/long-service/{}",
     ("DONE", "Built 2026-08-31 (lane 2). ⚠ Seeded from the BY-ID read, never the row: the summary carries five fields and the update takes eight, so a row-seeded form would blank the description, leave bonus and benefits on save. isProcessed/presentationDate/presentationNotes are carried through unchanged, or a correction would un-present an award somebody had already presented. ORIGINAL: The correction itself. `DELETE long-service/{}` and `POST long-service/{}/process` "
               "are wired; the edit is not.")),

    # The attachment family: five routes, no upload surface anywhere in the area, AND a backend
    # blocker in front of them. See D-39.
    ("POST", "api/Awards/{}/attachments",
     ("DONE", "Built 2026-08-31 (lane 2), D-39 cleared first. WAS: Blocked on D-39: the DTO takes `FileName` and `FilePath` as JSON, so a screen over "
               "it would ship the caller-supplied path sink area 16 replaced.")),
    ("DELETE", "api/Awards/attachments/{}",
     ("DONE", "Built 2026-08-31 (lane 2), D-39 cleared first. WAS: Blocked on D-39, with the POST it belongs to.")),
    ("POST", "api/Awards/nominations/{}/attachments",
     ("DONE", "Built 2026-08-31 (lane 2), D-39 cleared first. WAS: Blocked on D-39. `grep -ri attachment` over the awards screens and service returns "
               "nothing at all: the evidence a nomination is supposed to carry has never been "
               "uploadable.")),
    ("PUT", "api/Awards/nomination-attachments/{}",
     ("BUILD", "Still open, and deliberately: D-39 gave the family an upload, a list, a download "
               "and a delete, but the panel offers no way to change an attachment's TYPE or "
               "description after the fact. Re-upload and delete is the workaround; a small edit "
               "dialog would close it.")),
    ("DELETE", "api/Awards/nomination-attachments/{}",
     ("DONE", "Built 2026-08-31 (lane 2), D-39 cleared first. WAS: Blocked on D-39.")),
])

# ── Candidate — 3 flagged, classified 2026-08-31: all wired ──────────────────
_d("CandidateController.cs", [
    ("POST", "api/candidate/profile/photo", _HELPER),
    ("POST", "api/candidate/documents", _HELPER),
])
_d("JobCandidatesController.cs", [
    ("POST", "api/job-candidates/{}/documents", _HELPER),
])


# ── Assets — 12 flagged: 2 artefacts, 2 portal duplicates, 8 real ────────────
# Built 2026-08-30 (slice 18): the five edits, the surcharge delete and its recall all resolve to
# a caller now and have left this queue. What remains is the two upload artefacts and the two
# employee-portal duplicates.
_d("AssetsController.cs", [
    ("POST", "api/Assets/{}/attachments", _HELPER),
    ("POST", "api/Assets/{}/images", _HELPER),
    ("POST", "api/Assets/assignments/{}/acknowledge",
     ("INTENTIONAL", "The assignee's own signature, and `InternalOnly` refuses HR by name "
                     "(AssetActor.EnsureIsSubject). It is served by the employee's own route, "
                     "`POST api/employee-portal/assets/{}/acknowledge`, which /me/assets calls - "
                     "a portal route takes no employee id, so there is nothing to get wrong.")),
    ("POST", "api/Assets/surcharges/{}/respond",
     ("INTENTIONAL", "Same shape and same reason as acknowledge: the employee's right of reply, "
                     "refused for HR. Served by `POST api/employee-portal/asset-surcharges/{}/respond`, "
                     "wired from /me/assets.")),
])

# ── PerformanceImprovementPlans — 22 flagged, none real ───────────────────────
_PIP_ALIAS = _ALIAS("`api/Pip`")
_d("PerformanceImprovementPlansController.cs", [
    ("POST", "api/PerformanceImprovementPlans", _PIP_ALIAS),
    ("PUT", "api/PerformanceImprovementPlans/{}", _PIP_ALIAS),
    ("DELETE", "api/PerformanceImprovementPlans/{}", _PIP_ALIAS),
    ("PATCH", "api/PerformanceImprovementPlans/{}/status", _PIP_ALIAS),
    ("POST", "api/PerformanceImprovementPlans/{}/submit", _PIP_ALIAS),
    ("POST", "api/PerformanceImprovementPlans/{}/approve", _PIP_ALIAS),
    ("POST", "api/PerformanceImprovementPlans/{}/reject", _PIP_ALIAS),
    ("POST", "api/PerformanceImprovementPlans/{}/recall", _PIP_ALIAS),
    ("POST", "api/PerformanceImprovementPlans/{}/complete", _PIP_ALIAS),
    ("POST", "api/PerformanceImprovementPlans/{}/outcome", _PIP_ALIAS),
    ("POST", "api/PerformanceImprovementPlans/{}/goals", _PIP_ALIAS),
    ("PUT", "api/PerformanceImprovementPlans/goals/{}", _PIP_ALIAS),
    ("DELETE", "api/PerformanceImprovementPlans/goals/{}", _PIP_ALIAS),
    ("POST", "api/PerformanceImprovementPlans/{}/attachments", _PIP_ALIAS),
    ("DELETE", "api/PerformanceImprovementPlans/attachments/{}", _PIP_ALIAS),
    ("POST", "api/PerformanceImprovementPlans/{}/review-meetings", _PIP_ALIAS),
    ("PUT", "api/PerformanceImprovementPlans/{}/review-meetings/{}", _PIP_ALIAS),
    ("DELETE", "api/PerformanceImprovementPlans/{}/review-meetings/{}", _PIP_ALIAS),
    ("POST", "api/Pip/{}/attachments", _FIELD),
    ("POST", "api/Pip/{}/complete",
     ("INTENTIONAL", "`POST {}/outcome` is wired and both call the same "
                     "`CompletePipAsync(CompletePipDto)` — the outcome route just builds the DTO "
                     "from its own request shape. One operation, two doors.")),
    ("POST", "api/Pip/{}/review-meetings",
     ("INTENTIONAL", "`POST api/PipMeeting` is wired and calls the same "
                     "`AddReviewMeetingAsync`, plus it applies the form's goal updates. The "
                     "meeting form is the supported way in.")),
    ("PUT", "api/Pip/{}/review-meetings/{}",
     ("INTENTIONAL", "`PUT api/PipMeeting/{}` is wired onto the same `UpdateReviewMeetingAsync`.")),
])

# ── PerformanceAppraisals — 15 flagged: 10 superseded, 5 real ────────────────
_APPR_RAW = ("INTENTIONAL", "Raw model CRUD, superseded by the workflow routes the screens drive: "
                            "self-evaluation, manager-evaluation, the peer draft/submit pair, "
                            "progress-to-hr-review, approve, return-to-manager and acknowledge. "
                            "Writing an evaluation or a score directly would bypass every one of "
                            "the cycle's gates.")
_d("PerformanceAppraisalsController.cs", [
    ("POST", "api/PerformanceAppraisals",
     ("INTENTIONAL", "Appraisals are generated in bulk from the cycle — "
                     "`POST api/AppraisalCycle/{}/generate-appraisals` is wired and is how every "
                     "appraisal in the model comes to exist. A hand-made appraisal outside a cycle "
                     "has no template, no settings profile and no roll-up.")),
    ("PATCH", "api/PerformanceAppraisals/{}/status",
     ("INTENTIONAL", "Raw status set. Status is moved by the named workflow transitions, each of "
                     "which enforces its own preconditions.")),
    ("POST", "api/PerformanceAppraisals/{}/appeal",
     ("INTENTIONAL", "Superseded by `POST {}/submit-appeal`, which is wired and enforces the "
                     "at-least-one-appealed-item rule.")),
    ("POST", "api/PerformanceAppraisals/appeal/{}/resolve",
     ("INTENTIONAL", "Superseded by `POST {}/resolve-appeal`, wired, which also handles Remanded "
                     "(snapshot, rollback, re-evaluation deadline) and the score-modification gate.")),
    ("POST", "api/PerformanceAppraisals/{}/evaluations", _APPR_RAW),
    ("PUT", "api/PerformanceAppraisals/{}/evaluations/{}", _APPR_RAW),
    ("DELETE", "api/PerformanceAppraisals/{}/evaluations/{}", _APPR_RAW),
    ("POST", "api/PerformanceAppraisals/evaluations/{}/scores", _APPR_RAW),
    ("PUT", "api/PerformanceAppraisals/evaluations/{}/scores/{}", _APPR_RAW),
    ("DELETE", "api/PerformanceAppraisals/evaluations/{}/scores/{}", _APPR_RAW),
    ("PUT", "api/PerformanceAppraisals/{}",
     ("BUILD", "A generated appraisal's header — dates, evaluator, template — has no correction "
               "path. Regenerating the cycle is not one: it would not touch an appraisal that "
               "already exists.")),
    ("DELETE", "api/PerformanceAppraisals/{}",
     ("BUILD", "Admin-tier cleanup for an appraisal generated against someone who should not have "
               "been in scope. No other route removes one.")),
    ("POST", "api/PerformanceAppraisals/{}/attachments",
     ("BUILD", "The gated multipart upload exists and was purpose-built here — the controller's own "
               "remarks record replacing a caller-supplied filePath route and adding the "
               "entitlement test — and nothing calls it. An appraisal cannot carry evidence.")),
    ("DELETE", "api/PerformanceAppraisals/{}/attachments/{}",
     ("BUILD", "The other half of the same gap.")),
    ("POST", "api/PerformanceAppraisals/{}/responses",
     ("BUILD", "The employee's written answer to their appraisal, gated on the cycle's "
               "`AllowEmployeeResponse` setting. The read is there, the write has no screen, and "
               "the setting therefore does nothing. Note before building: the create DTO carries "
               "no author and the service stamps none — the response is keyed to the appraisal "
               "alone — and it sits on the HR-desk write policy, so as it stands 'the employee's "
               "response' is whatever HR types.")),
])

# ── SuccessionPlan — 8 flagged, all real; 2 blocked on D-14/D-15 ─────────────
# The metadata route now refuses every file-location field and the upload route beside it is the
# supported way in — the same split the discipline and staff-movement document routes already have.
_SUCC_META = ("INTENTIONAL", "The metadata-only route, deliberately unwired since D-14 was cleared "
                             "on 2026-08-29. It refuses `DocumentUrl` and the three DMS ids "
                             "outright and says so in its 400, so through the API it can only mint "
                             "a row naming no file; it survives for the legacy migration utility. "
                             "`POST api/succession-documents/upload` is the supported way in.")

_SUCC_DOC_DELETE = ("DONE", "Built 2026-08-30 (slice 19). D-14 and D-15 had already built the "
                             "upload, the download and the actor stamping; what was missing was a "
                             "screen, and the shared succession-document panel now supplies one for "
                             "each of the three owners.")
_COMPETENCY_REQ = ("DONE", "Built 2026-08-30 (slice 19). The competencies tab authors the "
                           "requirement set. The level input is bounded by the chosen "
                           "competency's own `proficiencyScaleMax`, which is not always 5, and "
                           "the delete is Admin-tier like every other delete in the area.")
_PLAN_ACTION = ("DONE", "Built 2026-08-30 (slice 19). The actions tab authors the plan's work. "
                        "The panel reads `GET plans/{}/actions` rather than the plan detail's "
                        "nested summary, because the summary carries seven of the update "
                        "payload's thirteen fields — see D-19.")
_d("SuccessionPlanController.cs", [
    ("POST", "api/succession-plans/{}/competency-requirements", _COMPETENCY_REQ),
    ("PUT", "api/succession-plans/competency-requirements/{}", _COMPETENCY_REQ),
    ("DELETE", "api/succession-plans/competency-requirements/{}", _COMPETENCY_REQ),
    ("POST", "api/succession-plans/{}/actions", _PLAN_ACTION),
    ("PUT", "api/succession-plans/actions/{}", _PLAN_ACTION),
    ("DELETE", "api/succession-plans/actions/{}", _PLAN_ACTION),
    ("POST", "api/succession-plans/{}/documents", _SUCC_META),
    ("DELETE", "api/succession-plans/documents/{}", _SUCC_DOC_DELETE),
])

# ── SuccessionCandidates — 5 flagged: 3 duplicates, 2 real (blocked) ─────────
_SUCC_DEV = ("INTENTIONAL", "Duplicate of `api/succession-development`, which is wired for create, "
                            "update and delete and takes the same "
                            "`Create/UpdateSuccessionDevelopmentActivityDto`. The candidate-scoped "
                            "trio is a second door onto the same entity.")
_d("SuccessionCandidatesController.cs", [
    ("POST", "api/succession-candidates/{}/development-activities", _SUCC_DEV),
    ("PUT", "api/succession-candidates/development-activities/{}", _SUCC_DEV),
    ("DELETE", "api/succession-candidates/development-activities/{}", _SUCC_DEV),
    ("POST", "api/succession-candidates/{}/documents", _SUCC_META),
    ("DELETE", "api/succession-candidates/documents/{}", _SUCC_DOC_DELETE),
])

# ── TalentPools — 3 flagged: 1 duplicate, 2 real (blocked) ───────────────────
_d("TalentPoolsController.cs", [
    ("POST", "api/talent-pools/members/{}/development-activities", _SUCC_DEV),
    ("POST", "api/talent-pools/members/{}/documents", _SUCC_META),
    ("DELETE", "api/talent-pools/documents/{}", _SUCC_DOC_DELETE),
])

# ── MedicalClinical — built 2026-08-29 (slice 6) ────────────────────────────
# The six edit/delete endpoints resolve to a caller now and have left the queue by themselves.
# What remains is the one route the screen deliberately does not call.
_d("MedicalClinicalController.cs", [
    ("PUT", "api/medical-clinical/appointments/{}/status",
     ("INTENTIONAL", "The free status set, deliberately unwired. The screen moves an appointment "
                     "through check-in, check-out and cancel — each carrying its own preconditions "
                     "and its own fields — and a control assigning any status at will would let a "
                     "visit be marked Completed with no check-out time. Same call as "
                     "`PATCH PerformanceAppraisals/{}/status`.")),
])

# ── MedicalExpenseClaims — built 2026-08-29 (slice 6) ───────────────────────
# The claim edit/delete, the line edit/delete and the document delete all resolve to a caller now.
_d("MedicalExpenseClaimsController.cs", [
    ("POST", "api/medical-expense-claims/{}/documents", _HELPER),
])

# ── NHISClaims — 2 flagged, both real and both blocked ──────────────────────
_NHIS_META = ("INTENTIONAL", "The metadata-only route, deliberately unwired since D-14 was cleared "
                             "on 2026-08-29. It refuses `FilePath` and the three DMS ids outright, "
                             "so through the API it can only mint a row naming no file; it survives "
                             "for the legacy migration utility. "
                             "`POST nhis-claims/documents/upload` is the supported way in.")

_NHIS_DOC = ("DONE", "Built 2026-08-30 (slice 19). D-14 had already built the gated upload and the "
                     "token-bearing download and hr-medical slice 5 had verified both against the "
                     "running API; the collection simply had no screen. It has one now, on the "
                     "NHIS claims list.")
_d("NHISClaimsController.cs", [
    ("POST", "api/nhis-claims/documents", _NHIS_META),
    ("POST", "api/nhis-claims/documents/upload", _NHIS_DOC),
    ("DELETE", "api/nhis-claims/documents/{}", _NHIS_DOC),
])

# ── The transport built by the D-14 slice. Three owners, one pair of routes ──
_d("SuccessionDocumentsController.cs", [
    ("POST", "api/succession-documents/upload",
     ("DONE", "Built 2026-08-29 to clear D-14; wired 2026-08-30 by slice 19. The one gated upload "
              "for all three owners — succession plan, plan candidate and talent-pool member — "
              "since one table and one DTO serve all three, and for the same reason one frontend "
              "panel drives it from three places rather than three panels being written. Its "
              "download sibling is a GET and so is invisible to instrument 01.")),
])

# ── StaffTravelRequests — 5 flagged: 2 artefacts, 3 real ────────────────────
_GROUP = ("BUILD", "Group travel can be created and given participants, and then nothing: the group "
                   "cannot be edited, cannot be deleted, and a participant cannot be taken off it.")
_d("StaffTravelRequestsController.cs", [
    ("POST", "api/staff-travel/requests/{}/attachments", _HELPER),
    ("POST", "api/staff-travel/requests/{}/reject",
     ("FALSE", "Wired at travel.service.ts `reject()`. The reason is a query parameter, so the "
               "call site reads `${baseUrl}/${id}/reject${query}` and instrument 01 folds the "
               "interpolation into the path segment instead of dropping it.")),
    ("PUT", "api/staff-travel/requests/groups/{}", _GROUP),
    ("DELETE", "api/staff-travel/requests/groups/{}", _GROUP),
    ("DELETE", "api/staff-travel/requests/groups/{}/participants/{}", _GROUP),
])

# ── StaffTravelPolicies — 2 flagged, both real ──────────────────────────────
_WITHHELD = ("INTENTIONAL", "Withheld from the UI on purpose — see D-29. Implemented, "
                            "harness-covered by `run-slice12-policy-authoring.mjs`, and waiting on "
                            "rule ENFORCEMENT rather than on a screen: `StaffTravelPolicyGuard` "
                            "refuses bookings on the policy's own scalar caps and never reads a "
                            "rule, so an editable rules register would be a control that enforces "
                            "nothing, and a hand-raised exception would be an audit record "
                            "implying a control was in force and waived.")
_d("StaffTravelPoliciesController.cs", [
    ("PUT", "api/staff-travel/policies/rules/{}", _WITHHELD),
    ("POST", "api/staff-travel/policies/{}/rules", _WITHHELD),
    ("DELETE", "api/staff-travel/policies/rules/{}", _WITHHELD),
    ("POST", "api/staff-travel/policies/exceptions/{}/decide", _WITHHELD),
    ("POST", "api/staff-travel/policies/exceptions", _WITHHELD),
])

# ── StaffTravelCompliance — 1 flagged, real ─────────────────────────────────
_d("StaffTravelComplianceController.cs", [
    ("POST", "api/staff-travel/compliance/alert-notifications",
     ("BUILD", "Same shape as the policy exception above: the alert list and its acknowledge action "
               "are wired, and `CreateAlertNotificationAsync` has no caller but this endpoint, so "
               "no alert can ever exist to acknowledge.")),
])

# ── TrainingServiceBonds — 3 flagged, all real ──────────────────────────────
_d("TrainingServiceBondsController.cs", [
    ("POST", "api/training-service-bonds",
     ("BUILD", "Bonds are minted server-side by `EnsureBondForNominationAsync` when a nomination is "
               "submitted and its program has `RequiresServiceBond`. That leaves no way to raise "
               "one for a nomination whose program was flagged afterwards.")),
    ("PUT", "api/training-service-bonds/{}",
     ("BUILD", "The duration, amount and currency are copied off the program at mint time. If the "
               "program's figures were wrong, the bond is wrong, and this is the only route that "
               "corrects it — a financial obligation with no correction path.")),
    ("DELETE", "api/training-service-bonds/{}",
     ("BUILD", "Admin-tier removal of a bond minted in error. Nothing else removes one.")),
])

# ── PositionVacancies — 3 flagged, all real ─────────────────────────────────
_d("PositionVacanciesController.cs", [
    ("PATCH", "api/position-vacancies/{}/status",
     ("BUILD", "The establishment screen wires reconcile and raise-requisition only. A vacancy's "
               "status cannot be set by hand.")),
    ("PUT", "api/position-vacancies/{}/notes",
     ("BUILD", "No way to annotate a vacancy.")),
    ("POST", "api/position-vacancies/{}/close",
     ("BUILD", "`reconcile` closes vacancies arithmetically, when the position is no longer below "
               "establishment. Closing one deliberately — with a reason — has no screen.")),
])

# ── CheckIns — 2 flagged, both real ─────────────────────────────────────────
_CHECKIN = ("BUILD", "Goals and appraisal review events both wire their attachment panel; check-ins "
                     "do not, so a check-in cannot carry evidence.")
_d("CheckInsController.cs", [
    ("POST", "api/CheckIns/{}/attachments", _CHECKIN),
    ("DELETE", "api/CheckIns/{}/attachments/{}", _CHECKIN),
])

# ── Movement sub-types — the same hole four times ───────────────────────────
_MOVE_DEL = ("BUILD", "Every movement sub-type wires create and update and none wires the "
                      "Admin-tier delete. A promotion, transfer, demotion or secondment detail "
                      "recorded against the wrong movement is permanent.")
_d("StaffPromotionsController.cs", [("DELETE", "api/staff-promotions/{}", _MOVE_DEL)])
_d("StaffTransfersController.cs", [("DELETE", "api/staff-transfers/{}", _MOVE_DEL)])
_d("StaffSecondmentsController.cs", [("DELETE", "api/staff-secondments/{}", _MOVE_DEL)])
_d("StaffDemotionsController.cs", [
    ("DELETE", "api/staff-demotions/{}", _MOVE_DEL),
    ("POST", "api/staff-demotions/{}/respond",
     ("DONE", "Built 2026-08-30 (slice 22) on /me/movements/{id}. The employee's acceptance of, "
              "or appeal against, a demotion notice — the service refuses anyone but the demoted "
              "employee, so HR cannot file it for them, and unlike the asset acknowledge/respond "
              "pair there was no employee-portal route to fall back on.\n\n"
              "⚠ The old note here said HR's `pending-appeals` queue \"can only ever be empty\" "
              "for want of this surface. That was backwards: the queue filters "
              "`EmployeeResponse == null`, so it lists demotions AWAITING an answer and filled "
              "with every demotion granting a right of appeal — what could never happen was a "
              "demotion LEAVING it. Establishing that is what turned up D-37, the absence of any "
              "read of appeals actually filed.")),
])
_d("StaffActingAppointmentsController.cs", [
    ("PUT", "api/staff-acting-appointments/{}",
     ("BUILD", "Create, complete, extend, convert and delete are wired; the plain edit is not. The "
               "one sub-type where update rather than delete is the missing half.")),
])

# ── StaffMovements — 2 flagged: 1 artefact, 1 deliberate ────────────────────
_d("StaffMovementsController.cs", [
    ("POST", "api/staff-movements/{}/attachments/upload", _HELPER),
    ("POST", "api/staff-movements/{}/attachments",
     ("INTENTIONAL", "The metadata-only route, deliberately unwired. It rejects FilePath, "
                     "FileUploadRecordId, DocumentRecordId and DocumentVersionId outright and says "
                     "so in its 400, exactly as the discipline document route does, so through the "
                     "API it can only mint a row naming no file. The upload route beside it is the "
                     "supported way in.")),
])

# ── Replace-set parents: the per-child routes are a second door ─────────────
_PRESET = ("INTENTIONAL", "`PUT api/interview-question-presets/{}` is wired and its `items` is a "
                          "replace-set: the service deletes every existing item absent from the "
                          "payload, updates the ones present and adds the new ones. The preset "
                          "form authors the whole list, so a per-item route would be a second "
                          "writer over the same rows.")
_d("InterviewQuestionPresetController.cs", [
    ("POST", "api/interview-question-presets/{}/items", _PRESET),
    ("PUT", "api/interview-question-presets/{}/items/{}", _PRESET),
    ("DELETE", "api/interview-question-presets/items/{}", _PRESET),
])
_POSCOMP = ("INTENTIONAL", "`PUT api/position-competencies/position/{}/bulk-set` is wired and calls "
                           "`BulkReplaceForPositionAsync` — the position's whole competency set is "
                           "authored in one write. Single-row CRUD over the same rows would race it.")
_d("PositionCompetencyController.cs", [
    ("POST", "api/position-competencies", _POSCOMP),
    ("PUT", "api/position-competencies/{}", _POSCOMP),
    ("DELETE", "api/position-competencies/{}", _POSCOMP),
])

# ── Flat duplicates of a nested route that is wired ─────────────────────────
_CYCLE_TARGET = ("INTENTIONAL", "Duplicate. Target CRUD goes through the cycle-nested "
                                "`api/AppraisalCycle/{}/targets` routes, which are wired and "
                                "validate the cycle with the target; the frontend service says so "
                                "at the point of use. Only this controller's exclusion routes are "
                                "called.")
_d("AppraisalCycleTargetController.cs", [
    ("POST", "api/AppraisalCycleTarget", _CYCLE_TARGET),
    ("PUT", "api/AppraisalCycleTarget/{}", _CYCLE_TARGET),
    ("DELETE", "api/AppraisalCycleTarget/{}", _CYCLE_TARGET),
])
_ENGAGEMENT = ("INTENTIONAL", "Duplicate. `api/client-engagements/{}` is the flat controller for the "
                              "same entity and its PUT and DELETE are both wired.")
_d("ConsultantClientsController.cs", [
    ("PUT", "api/consultant-clients/engagements/{}", _ENGAGEMENT),
    ("DELETE", "api/consultant-clients/engagements/{}", _ENGAGEMENT),
])
_d("LeavesController.cs", [
    ("POST", "api/Leaves/{}/attachments", _HELPER),
    ("POST", "api/Leaves/balances/{}/adjustments",
     ("INTENTIONAL", "Superseded by the flat `POST api/Leaves/adjustments`, which is wired and looks "
                     "the balance up or creates it, so the caller does not need a balance id it "
                     "may not have.")),
])
_d("LocationController.cs", [
    ("POST", "api/Location/{}/move",
     ("INTENTIONAL", "`PUT api/Location/{}` is wired, carries `ParentLocationId`, and applies the "
                     "identical guards — self-parent, cycle, same structure, exactly-one-level-"
                     "below, single root. Verified line by line against `MoveLocationAsync`.")),
])
_d("AppraisalNotificationsController.cs", [
    ("POST", "api/AppraisalNotifications/mark-all-read/{}",
     ("INTENTIONAL", "The employee-id-keyed variant. /me wires `POST "
                     "api/AppraisalNotifications/me/mark-all-read`, which takes the employee from "
                     "the token — the same principle the asset portal is built on, and the reason "
                     "not to wire this one.")),
])
_d("AppraisalWorkflowController.cs", [
    ("POST", "api/AppraisalWorkflow/{}/transition",
     ("INTENTIONAL", "The generic transition. The appraisal screens drive status through the named "
                     "transitions on `api/PerformanceAppraisals`, each carrying its own "
                     "preconditions and its own actor rule.")),
])
_d("PeerNominationController.cs", [
    ("POST", "api/PeerNomination",
     ("INTENTIONAL", "Batch nomination lives on the appraisal — `POST "
                     "api/PerformanceAppraisals/{}/peer-nominations` is wired — and nominating peers "
                     "one at a time is not how the screen works.")),
    ("PUT", "api/PeerNomination/{}",
     ("INTENTIONAL", "The client deliberately exposes only read, remove and send-invitation for "
                     "single rows; changing who a peer is means removing the nomination and "
                     "nominating again, and remove is refused once the invitation has gone out.")),
])
_d("PayComponentsController.cs", [
    ("POST", "api/hr/pay-components",
     ("INTENTIONAL", "The endpoint's own summary reads 'Not supported — create the component in "
                     "Payroll, then sync.' Payroll owns the component master.")),
])
# ── Competency — nothing left to classify ───────────────────────────────────
# Its two flags were phantoms: `CompetencyController.cs` holds two routed controllers, and
# instrument 01 used to cross every [Route] in a FILE with every action in it. Fixed at the
# instrument on 2026-08-30, so those rows no longer exist and need no disposition.

_SANCTION = ("BUILD", "Blocked — see D-18, which RESTATES the original block rather than lifting "
                      "it. The FR-HR-080 authority rule does exist in `RecordDecision`, but no "
                      "actor reaches it, and a warning can be recorded against a case nobody has "
                      "decided — so an editable sanction today would let a penalty be written with "
                      "no authority rule in force. `probe-authority-gate.mjs` holds both findings "
                      "as passing assertions.")

_d("StaffDisciplineSubEntityController.cs", [
    ("POST", "api/discipline/cases/{}/warning", _SANCTION),
    ("PUT", "api/discipline/cases/{}/warning", _SANCTION),
    ("POST", "api/discipline/cases/{}/suspension", _SANCTION),
    ("PUT", "api/discipline/cases/{}/suspension", _SANCTION),
    ("POST", "api/discipline/cases/{}/fine", _SANCTION),
    ("POST", "api/discipline/cases/{}/fine/payment", _SANCTION),
    ("POST", "api/discipline/cases/{}/termination", _SANCTION),
    ("PUT", "api/discipline/cases/{}/termination", _SANCTION),
    ("POST", "api/discipline/cases/{}/separation", _SANCTION),
    ("PUT", "api/discipline/cases/{}/separation", _SANCTION),
])

# ── Single real gaps ────────────────────────────────────────────────────────
_d("JobCandidateController.cs", [
    ("POST", "api/job-candidates/{}/documents", _HELPER),
    ("PUT", "api/job-candidates/{}/interests/{}",
     ("DONE", "Built 2026-08-30 (recruitment closure, slice 1): the interests tab edits in place; "
              "the component comment claiming no update endpoint existed was deleted with it.")),
])
_d("AwardsMeController.cs", [
    ("PUT", "api/awards/me/reviews/{}",
     ("BUILD", "Scoring a nomination is wired; revising a score the caller themselves gave is not, "
               "so a mistyped score is final.")),
])
_d("JobInterviewController.cs", [
    ("PUT", "api/job-interviews/external-panelists/{}",
     ("DONE", "Built 2026-08-30 (recruitment closure, slice 1): the panel's edit dialog, shared "
              "with the internal rows.")),
])
_d("LeaveTypesController.cs", [
    ("PATCH", "api/hr/leave-types/{}/deactivate",
     ("BUILD", "Leave types can be created and edited but never retired — and there is no delete "
               "either, so a type introduced by mistake stays on every picker forever.")),
])
_d("TrainingCompletionsController.cs", [
    ("POST", "api/training-completions/bulk",
     ("BUILD", "Already carried in section F from the demo feedback: bulk completion has no UI and "
               "no client method. Confirmed here against the route.")),
])
_d("TrainingNominationsController.cs", [
    ("POST", "api/training-nominations/availability-check",
     ("BUILD", "Already carried in section F: the availability check is never shown, so a nominee "
               "is scheduled against a clash the server would have reported.")),
])

# ── Pure artefacts, one per controller ──────────────────────────────────────
for _f, _v, _r, _k in [
    ("CalibrationSessionsController.cs", "POST", "api/CalibrationSessions/{}/attachments", _HELPER),
    ("UnitGoalsController.cs", "POST", "api/UnitGoals/{}/attachments", _HELPER),
    ("AppraisalReviewEventsController.cs", "POST", "api/AppraisalReviewEvents/{}/attachments", _HELPER),
    ("MedicalSelfServiceController.cs", "POST", "api/medical/me/expense-claims/{}/documents", _HELPER),
    ("EmployeeHealthController.cs", "POST", "api/employee-health/exam-documents", _HELPER),
    ("JobOfferController.cs", "POST", "api/job-offers/{}/upload-letter", _HELPER),
    ("JobOfferController.cs", "POST", "api/job-offers/{}/upload-signed-letter", _HELPER),
    ("PreEmploymentCheckController.cs", "POST", "api/pre-employment-checks/items/{}/document", _HELPER),
    ("PreEmploymentCheckController.cs", "POST",
     "api/pre-employment-checks/reference-responses/{}/document", _HELPER),
    ("StaffRequisitionsController.cs", "POST", "api/StaffRequisitions/{}/attachments", _HELPER),
    ("JobPostingController.cs", "POST", "api/job-postings/{}/attachments", _HELPER),
    ("SheControlledDocumentController.cs", "POST", "api/safety/documents/{}/versions", _HELPER),
    ("SheEnvironmentalComplianceController.cs", "POST",
     "api/safety/environmental/permits/{}/document", _HELPER),
    ("HrAnnouncementsController.cs", "POST", "api/hr/announcements/{}/attachment", _FIELD),
    ("HrLetterRequestsController.cs", "POST", "api/hr/letter-requests/{}/upload", _FIELD),
    ("HrPoliciesController.cs", "POST", "api/hr/policies/{}/document", _FIELD),
    ("MyProfileController.cs", "POST", "api/employee-portal/profile/change-requests/{}/evidence", _FIELD),
]:
    ENDPOINT_DISPOSITIONS[(_f, _v, _r)] = _k


# Curated items that no static instrument can see — the demo-feedback backlog and the scheduler.
DEMO_FEEDBACK = [
    ("Employee Master", "Disability tick and description sit on EmployeeDependent, not Employee", "BUILD", "Wrong entity, not merely absent"),
    ("Employee Master", "Probation dates stay editable after confirmation", "BUILD", "No confirmed-state guard on contract update"),
    ("Employee Master", "Manager picker ignores ReportsToPosition", "BUILD", "Field exists and is populated; picker does not use it"),
    ("Employee Master", "Gender 'Other' has no description field", "BUILD", ""),
    ("Employee Master", "Hometown absent from the employee record", "BUILD", ""),
    ("Employee Master", "Guarantor has no guaranteed amount and no photograph", "BUILD", ""),
    ("Employee Master", "Referees cannot carry a reference letter", "BUILD", ""),
    ("Employee Master", "Expatriate: no issue dates, no resident permit, no family members", "BUILD", "FamilyAccompanying is a bare bool"),
    ("Employee Master", "IdentificationType has no expiry notification lead days", "BUILD", "Per-type setting"),
    ("Employee Master", "Certification bodies are free text, not a lookup", "BUILD", "EmployeeSkill.CertifyingBody"),
    ("Employee Master", "No mandatory documents against a position", "BUILD", ""),
    ("Employee Master", "No employee document attachments", "BUILD", "Controlled upload gate already exists"),
    ("Employee Master", "Qualification level is not a dimension", "BUILD", "QualificationType is a category, not an academic level"),
    ("Employee Master", "Staff number auto/manual is behaviour, not configuration", "BUILD", "No mode flag, no prefix/format"),
    ("Employee Master", "Exit interview questions are fixed fields", "BUILD", "Not a configurable question set; no attachments"),
    ("Employee Master", "No appointment letter templates", "BUILD", "Only email templates exist"),
    ("Employee Master", "No labour-law checklist", "BUILD", ""),
    ("Employee Master", "No mass application of benefits to dependents", "BUILD", ""),
    ("Employee Master", "Workflow step checklists unused by any HR process", "DECIDE", "Candidates: onboarding, separation clearance, probation"),
    ("Leave", "Compassionate leave cannot be set off against annual leave", "DECIDE", "No offset/advance concept anywhere; needs a design call"),
    ("Leave", "Adjustment form does not show the employee's balance", "BUILD", ""),
    ("Leave", "Reliever clashes are not visible on the plan", "BUILD", ""),
    ("Leave", "Free-text field still labelled 'Reason', not 'Remarks'", "BUILD", "Cosmetic"),
    ("Leave", "Leave request numbering still uses a max+1 scan", "BUILD", "Move to NumberSequence as Training already did"),
    ("Succession", "Criteria candidate search has no screen", "BUILD", "successionSearchService.searchCandidates has no caller"),
    ("Succession", "Candidate age and service-years-left not displayed", "BUILD", "API returns both"),
    ("Training", "Bulk nomination has no UI", "BUILD", "setBulkResult in NomineesPanel.tsx is dead code"),
    ("Training", "Bulk completion has no UI and no client method", "BUILD", ""),
    ("Training", "Nominee availability check never shown", "BUILD", "availability-check endpoint has no caller"),
    ("Training", "No 'Training Activities' grouped screen", "BUILD", ""),
    ("Training", "Mentoring still inside the Training menu", "BUILD", "Wants its own nav section"),
    ("Training", "Certificate does not gate completion", "BUILD", "Per-program flag"),
    ("Training", "Menu order differs from TDC's suggestion", "DECIDE", "Setup/operations split may be deliberate"),
    ("Recruitment", "Menu still reads 'Manpower Budgets'", "BUILD", "Rename to Manpower Recruitment Budget"),
    ("Appraisal", "Check-in link to company objectives", "DECIDE", "Confirm with TDC that it matches intent"),
    ("Platform", "Scheduled HR sweeps: two failed nightly, two were never hosted", "DONE 2026-08-31", "⚠ This row read \"No AddHostedService registration for HR\", which had been false for weeks — six HR engines were hosted. What was true was worse and invisible: see D-38. Retirement and contract-expiry alerts (FR-HR-093) now run on a timer. Leave year-end is deliberately NOT scheduled: carry-over and forfeiture move balances rather than raise reminders, so automating them is a policy decision for TDC."),
]

# Controllers we have committed to building. For these the ledger enumerates EVERY write
# endpoint as a checkbox, not just the ones the instruments flagged — a build checklist has to
# be complete to be checkable.
BUILD_CHECKLISTS = {
    "CompanyScheduleController.cs": "Company Schedule",
    "JobAnalysisController.cs": "Job Analysis",
    "StaffDisciplineSupportController.cs": "Discipline — case file support",
    "StaffDisciplineSubEntityController.cs": "Discipline — case sub-entities",
    "MedicalInsuranceController.cs": "Medical insurance",
    "EmployeeCareerPathController.cs": "Employee career paths",
}

BLOCKERS = [
    ("D-01", "Company Schedule actor ids are client-supplied", "DONE 2026-08-28",
     "organizerId, approvedById, markedById, bookedById and announcedById arrived as query "
     "parameters. Opening a UI over them unchanged would have shipped an act-as-anyone surface — "
     "the controller's own W3 remarks said so. All six now derive the actor from the token via "
     "HrControllerBase.TryGetEmployeeWriteContext and no longer accept it from the client. "
     "Safe to change signatures because no caller existed. **Needs a backend rebuild.**",
     "Was blocking the Company Schedule build — cleared"),
    ("D-03", "A child write on an approved job description is accepted by the API", "DONE 2026-08-31",
     "None of the twelve child-collection writes checks status. `AddPhysicalDemandAsync` and its "
     "eleven siblings call `GetOwnedJobDescriptionAsync`, which verifies the tenant and stops — so "
     "the duties of an approved, in-force job description can be rewritten with no new version and "
     "no trace. Proven against the running API by slice 13, which records it as a passing "
     "assertion so the day it starts failing is the day the server grew a gate. The authoring "
     "panels refuse it client-side (`AUTHORABLE_JOB_DESCRIPTION_STATUSES`) and that is the only "
     "thing stopping it. A server-side guard belongs on the service, not the screen.\n\n"
     "  **Fixed 2026-08-31 (closure lane 1).** `RequireAuthorableJobDescriptionAsync` gates all "
     "twelve collections — 36 write paths, since the updates and deletes never called "
     "`GetOwnedJobDescriptionAsync` at all: they loaded the child by id, checked its tenant and "
     "wrote. Two of the twelve reach their description through a parent (a KPI through its "
     "responsibility, equipment training through its tool) and have their own helper. Deletes are "
     "gated too: removing a duty from a signed document is the same act as rewriting one, and the "
     "message names the way forward — raise a new version — rather than just "
     "refusing. `run-slice7-closure-lane1.mjs` §2 proves the draft surface still works "
     "first, then the refusals, then that the record is unchanged by them.",
     "Cleared. The UI's client-side refusal now compensates for nothing, which is the "
     "correct state for it"),
    ("D-04", "`JobEquipmentTool.LinkedQualificationId` points at a JobQualification, not the catalogue", "DONE 2026-08-29",
     "The field is called `LinkedQualificationId`, the DTO types it `Guid?`, and the obvious "
     "reading — the `Qualifications` reference catalogue — is wrong. The constraint is "
     "`FK_JobEquipmentTools_JobQualifications_LinkedQualificationId`: it targets one of the SAME "
     "job description's qualification rows. A catalogue id fails the FK and the request 500s with "
     "the generic handler's message, naming nothing. The equipment panel was built on the wrong "
     "reading and slice 13 caught it; the picker now reads the job description's own "
     "qualifications and hides itself until there are some.",
     "Was breaking every equipment-tool save — cleared"),
    ("D-05", "Legal-review referrer was assertable by the request body", "DONE 2026-08-29",
     "`CreateStaffDisciplineLegalReviewDto.ReferredById` was accepted from the client and copied "
     "straight onto the entity by the mapper, while the token's employee id went only to "
     "`CreatedBy`. So any HR user could record a colleague as the person who referred a case to "
     "legal — a falsifiable audit record on exactly the kind of document a case turns on later. "
     "The same defect class as D-01, and cleared the same way: the field is gone from the create "
     "DTO and `ReferAsync` stamps the actor. Nothing had ever sent it (it was in section E's "
     "\"no form can set\" table), so no caller broke. **Needs a backend rebuild.**",
     "Was blocking the discipline legal-review build — cleared"),
    ("D-07", "Action-step complete and skip never stamped the step's actor", "DONE 2026-08-29",
     "`CompleteStepAsync` and `SkipStepAsync` set only `UpdatedBy` — the string audit column — and "
     "never `ActionedById`, the Employee FK the DTO exposes and the case screen renders as \"By\". "
     "So that column was permanently blank for every step closed through the supported route, and "
     "the ONLY path that ever filled it was the plain update, which takes the id from the request "
     "body. Found by running the new panel's own payloads, not by reading the code: three "
     "assertions failed on the first run of slice 9. Both transitions now stamp the actor; skip "
     "deliberately still sets no completion date. **Needs a backend rebuild.** "
     "⚠ Residue: `UpdateActionStepDto.ActionedById` is still client-supplied. Left alone rather "
     "than removed, unlike the legal-review referrer, because correcting a mis-attributed step is "
     "a legitimate HR act and no screen exposes it — but it is an act-as-anyone vector on paper.",
     "Was making the panel's \"By\" column permanently empty — cleared"),
    ("D-10", "Provider documents took a caller-supplied file path with no gate and no download", "DONE 2026-08-29",
     "`POST provider-documents` REQUIRED a `FilePath` and stored it verbatim, so any HR user could "
     "point a document row at arbitrary bytes on disk. There was no upload endpoint and no download "
     "route either, so the row was unreadable even when the path was honest. This was the **third** "
     "instance of that defect in the medical module — `MedicalExpenseDocument` and "
     "`EmployeeMedicalExamDocument` were each fixed for it, and provider documents were missed. "
     "There was no safe way to build a UI over it, which is why the collection stayed unreachable. "
     "Now: three nullable DMS columns (migration `AddMedicalInsuranceProviderDocumentDmsColumns`, "
     "hand-guarded on COL_LENGTH because rebuild-db builds from the EF model), a multipart upload "
     "through the scanning + DMS gate, a token-bearing download, and the metadata route refusing "
     "every file-location field. **Needs a backend rebuild.**",
     "Was blocking the provider-documents build — cleared"),
    ("D-11", "A declared upload category is not a registered one", "DONE 2026-08-29",
     "Adding `HrMedicalInsuranceProviderDocuments` to `ControlledFileUploadCategories` was only "
     "half the job. Membership of `SystemCleanScanRequired` is the ONLY thing that turns scanning "
     "on — a category absent from it is silently SKIPPED, and "
     "`CentralDocumentRepositoryFileService.RegisterAsync` then rejects `Skipped` as firmly as "
     "`Infected`. The upload passed the gate and failed one layer later with an "
     "`InvalidOperationException` naming neither the category nor the scan. The property's own "
     "remarks predict this exactly; it was still missed. Registered, with a comment at the point "
     "of use. **Any future upload category must be added in both places.**",
     "Was making every provider-document upload fail — cleared"),
    ("D-12", "The per-provider premium read returned a summary its panel could not render", "DONE 2026-08-29",
     "`providers/{id}/premium-records` returned `MedicalInsurancePremiumRecordSummaryDto` — total "
     "and status only, nothing about contributions, covered lives, due date or payment. Nine probe "
     "assertions failed and the panel would have shown blank columns. **Third occurrence of the "
     "D-09 shape**, and again the pattern already existed beside it: network facilities and "
     "provider documents on the SAME controller returned full DTOs. Converted; "
     "`premium-records/overdue` stays a summary. The rule, now stated three times: a per-parent "
     "read feeds a panel that must show detail; a cross-record read feeds a list.",
     "Was making the premium panel render blank columns — cleared"),
    ("D-13", "HR cannot administer employee insurance policies", "OPEN",
     "`policies` (create, update, cancel, delete) and `dependents` (add, update, remove) — seven "
     "write endpoints — have no HR screen at all; only the employee's own self-service surface "
     "reads policies. Deferred by decision on 2026-08-29 rather than overlooked: whether HR "
     "administers enrolment, or it arrives from payroll or the insurer, is a product call and not "
     "a coverage gap. Recorded because slice 4 made the consequence concrete — its insurance-claims "
     "section cannot execute at all, since no policy exists to claim against.",
     "Blocks nothing built so far, and blocks any test of insurance claims"),
    ("D-06", "Four discipline case-file collections were displayed but unrecordable", "DONE 2026-08-29",
     "`StaffDisciplineCaseDetailDto` carries six collections; only two were built out in the first "
     "pass. Witnesses, documents, notes and notifications are now authorable too, so the case file "
     "is complete. Documents go through the controlled upload gate only — the metadata-only route "
     "is left unwired because it rejects every file-location field and can therefore only mint a "
     "row naming a file that does not exist.",
     "Was leaving the case file half-authorable — cleared"),
    ("D-09", "Four per-case reads returned summaries a panel could not edit from", "DONE 2026-08-29",
     "`cases/{id}/witnesses`, `/notes`, `/documents` and `/notifications` returned `...SummaryDto` "
     "projections. The witness summary has no `ContactInfo`; the note summary carries a "
     "**100-character excerpt** instead of the note; documents drop `Description` and "
     "`ActionStepName`; notifications drop `SentByName` and `FollowupDate`. Every panel built on "
     "them rendered permanently blank columns, and the witness edit form would have wiped "
     "`ContactInfo` on every save. Legal reviews already returned the full DTO from their per-case "
     "read — the pattern existed and four collections had not followed it. The eight per-case "
     "reads now return the record; the cross-case reads (by-author, pending-followup, by-scope, "
     "by-category, by-employee) stay on summaries because they feed lists and the reminder sweep. "
     "The rule: **a per-case read feeds a panel that must edit; a cross-case read feeds a list.** "
     "**Needs a backend rebuild.**",
     "Was making four new panels render blank columns — cleared"),
    ("D-08", "Note author and document uploader were assertable by the request body", "DONE 2026-08-29",
     "Third and fourth instances of the D-05 shape, found by checking every create mapper in the "
     "family rather than waiting for a probe to fail. `StaffDisciplineNote.CreatedByEmployeeId` "
     "and `StaffDisciplineDocument.UploadedById` were copied from the DTO while the token's "
     "employee id went only to `CreatedBy`. Notifications were **already correct** — `SentById` is "
     "server-stamped and the DTO says why — which is what showed the other two were not. Both "
     "fields are now stamped from the token and removed from their create DTOs.",
     "Was letting a case note be attributed to a colleague — cleared"),
    ("D-14", "The succession, talent-pool and NHIS document families were on a caller-supplied file path", "DONE 2026-08-29",
     "The fourth and fifth instances of the sink D-10 closed for provider documents. "
     "`CreateSuccessionDocumentDto.DocumentUrl` is `[Required]`, 1000 characters, and copied "
     "verbatim onto the entity by the mapper; `CreateNHISClaimDocumentDto.FilePath` is the same "
     "shape. Neither family has an upload route, a download route or a single DMS column, so a "
     "row can only ever name a file the server never received — unreadable even when the path is "
     "honest. One DTO serves all three succession doors (`succession-plans/{}/documents`, "
     "`succession-candidates/{}/documents`, `talent-pools/members/{}/documents`), which is why "
     "eight endpoints are blocked by one defect. The fix is the D-10 recipe: nullable DMS columns "
     "behind a COL_LENGTH-guarded migration, a multipart upload through the scanning + DMS gate, "
     "a token-bearing download, and the metadata route refusing every file-location field. "
     "⚠ Register the new upload categories in **both** `ControlledFileUploadCategories` **and** "
     "`SystemCleanScanRequired` — that is D-11, and it is the step that gets missed.\n\n"
     "  **Done 2026-08-29.** Six nullable DMS columns across the two tables (migration "
     "`AddSuccessionAndNhisDocumentDmsColumns`, hand-guarded on `COL_LENGTH` and listed in "
     "`FastBuildMigrationMetadata`), `HrSuccessionDocuments` declared **and** registered as "
     "scan-mandatory in the same edit, and all four metadata routes now refuse every "
     "file-location field. The succession transport went into ONE new controller, "
     "`api/succession-documents`, rather than three copies: one table and one DTO serve all "
     "three owners, so the upload takes the owner as a parameter and refuses anything but "
     "exactly one of them — none writes a row invisible to every read, two puts it in two "
     "collections. NHIS reuses `HrMedicalClaimDocuments`: same artefact, same person, same "
     "permission family. Two things the work turned up on the way — the confidential-document "
     "download is gated to Succession.Admin, because the confidential list is a separate read "
     "rather than a filter and a blind download would hand back through one door what the other "
     "withholds; and the NHIS upload checks the claim's tenant before storing bytes, because "
     "`AddClaimDocumentAsync` checks the tenant but never the claim, so the FK alone was "
     "deciding.\n\n"
     "  **Verified against the running API, 2026-08-29** — `hr-succession/run-slice13.mjs` (36 "
     "assertions) and `hr-medical/run-slice5-actors-and-nhis-documents.mjs` (34), both green. The "
     "run earned its keep: **`uploadedByName` came back empty on the create response** while every "
     "per-parent read resolved it. All three writers mapped a freshly-added entity whose "
     "`UploadedBy` navigation had never been loaded — the stale-nav-on-a-write-response shape — so "
     "a panel binding \"Uploaded by\" would have shown blank on the row it had just created and "
     "correct after a refetch. Reading the code did not find it; the assertion did. Fixed with "
     "`ISuccessionDocumentRepository.GetByIdWithUploaderAsync`, re-read in all three writers "
     "before mapping.",
     "Was blocking 8 of the queue's BUILD endpoints — cleared"),
    ("D-15", "Succession document uploader was assertable by the request body", "DONE 2026-08-29",
     "The fifth instance of the D-05 shape, and it travels with D-14. "
     "`CreateSuccessionDocumentDto.UploadedById` is `[Required]` and the mapper writes "
     "`UploadedById = dto.UploadedById` while the token's employee id goes only to `CreatedBy` — "
     "so on a succession plan, a candidate file or a talent-pool member's file, the person "
     "recorded as having produced a document is whoever the client says. Both controllers already "
     "resolve `_currentUser.EmployeeId` and hand it to the service; it simply never reaches the "
     "field. Fix it in the same commit as D-14: nothing has ever sent the field, so no caller "
     "breaks.\n\n"
     "  **Done 2026-08-29.** The field is gone from the create DTO and the uploader is now an "
     "explicit `uploadedByEmployeeId` parameter on all three writers — a parameter rather than a "
     "DTO field precisely so it cannot be asserted by a caller. Nothing had ever sent it, and no "
     "caller broke. **Needs a backend rebuild.**",
     "Was blocking the same 8 endpoints as D-14 — cleared"),
    ("D-16", "Medical clinical status transitions recorded no actor at all", "DONE 2026-08-29",
     "`UpdateAppointmentStatusAsync`, `CancelAppointmentAsync`, `CheckIn`, `CheckOut`, "
     "`UpdateReferralStatusAsync`, `CompleteReferralAsync` and `RejectPreAuthorizationAsync` are "
     "declared without a `userId` parameter, their DTOs carry no actor field, and the service sets "
     "no `UpdatedBy` — so the row after the transition is indistinguishable from the row before it "
     "as to who moved it. Worse than D-07, where the audit string at least survived: here there is "
     "no actor anywhere on the path to stamp. This is **not** only a blocker for the new work — "
     "cancel, check-in and check-out are wired today and shipped, so the defect is live. The "
     "sibling writes on the same controller (`CreatePreAuthorization`, `UpdateReferral`, "
     "`UpdateAppointment`) all take a userId through `TryGetWriteContext`; the pattern exists "
     "beside them and the transitions did not follow it.\n\n"
     "  **Done 2026-08-29.** All eight transition helpers take the actor and stamp "
     "`UpdatedAt`/`UpdatedBy`, the same two lines their `UpdateEntity` siblings in the same file "
     "already used. Two calls recorded rather than assumed: **neither `MedicalAppointment` nor "
     "`MedicalReferral` carries a domain actor FK at all** — no `CancelledById`, no "
     "`CheckedInById` — so `UpdatedBy` is the only place an actor can go without a schema change; "
     "and pre-authorization **reject** sets `UpdatedBy` but deliberately NOT `ApprovedBy`, "
     "because writing the approver's field on a rejection would make a rejected authorization "
     "read as approved-by-that-person on every screen that binds it. A dedicated rejector column "
     "needs a migration and belongs with the pre-authorization edit build. "
     "⚠ Residue: `CancelPolicyAsync`, `UpdateInsuranceClaimStatusAsync` and NHIS "
     "`UpdateClaimStatusAsync` have the identical defect — same file, different families, all "
     "three wired and shipped. Left alone rather than widening a scoped slice; three lines when "
     "someone wants them.\n\n"
     "  **Verified against the running API, 2026-08-29** by "
     "`hr-medical/run-slice5-actors-and-nhis-documents.mjs`. ⚠ Note how the actor is pinned, "
     "because \"UpdatedBy is non-blank\" would also pass on a hardcoded constant: each transition "
     "is compared against the `CreatedBy` of a record the SAME actor created, and then a SECOND "
     "actor moves the row and `UpdatedBy` is asserted to have changed to theirs. Non-blank proves "
     "the line runs; changing with the caller proves it is the caller.",
     "Was blocking the MedicalClinical edit/delete build, and was already wrong on three shipped "
     "actions — cleared"),
    ("D-17", "`POST api/talent-pools` 500s when ownerId is omitted", "DONE 2026-08-31",
     "`CreateTalentPoolDto.OwnerId` is `[Required]` but typed as a non-nullable `Guid`, and "
     "`[Required]` does not reject `Guid.Empty` — so an omitted owner passes model validation "
     "intact and dies at the database on `FK_TalentPools_Employees_OwnerId` with error 547, "
     "surfacing as the generic handler's 500 that names neither the field nor the constraint. "
     "The same shape as D-04, where a wrong-catalogue qualification id 500'd naming nothing. Found "
     "by slice 13 tripping over it while building a talent-pool fixture, not by looking for it. "
     "The fix is a validation guard that rejects `Guid.Empty` with a message naming the field; "
     "sending the id is the workaround, not the fix. Worth a sweep rather than a one-line patch — "
     "`[Required]` on a non-nullable `Guid` is inert everywhere it appears, and this DTO family "
     "uses it heavily.\n\n"
     "  **Fixed 2026-08-31 (closure lane 1), and the sweep it was supposed to be is NOT what "
     "shipped.** `HrRequiredGuidActionFilter` rejects an empty `Guid` on a `[Required]` property "
     "with a message naming the field, but only for a DTO carrying "
     "`[CallerSuppliesIdentifiers]`. Opt-in, after the blanket version over the "
     "`ErpSystem.Core.DTOs.HR` namespace was written and then withdrawn.\n\n"
     "  ⚠ **Twice it refused correct requests, and neither case is visible from the "
     "DTO.** An empty required Guid at validation time is often a field the CONTROLLER is about "
     "to fill: from the route (`dto.JobDescriptionId = jobDescriptionId` on "
     "`POST descriptions/{}/duty-items`) or from the token (`dto.ReportedById`, "
     "`dto.EmployeeId`, `dto.InitiatedById`, deliberately, so a caller cannot assert who acted). "
     "Matching the property against the route key does not rescue it either: "
     "`POST responsibilities/{responsibilityId}/kpis` fills `JobResponsibilityId`, and the only "
     "thing connecting those names is an assignment statement no reflection can see. Both were "
     "found by `hr-jobarch/run-slice13` refusing its own correct payloads, one after the "
     "other — not by reading the code.\n\n"
     "  So breadth is now a per-DTO claim rather than a namespace rule: two DTOs carry the "
     "attribute today (`CreateTalentPoolDto`, the reported instance, and "
     "`CreateJobDescriptionDto`), each checked to confirm its controller fills nothing in. "
     "**The remaining ~900 properties are unchanged and an omitted foreign key still 500s "
     "there**; adding a DTO is one line plus that check. Recorded plainly because the entry above "
     "asked for a sweep and this is narrower than it sounds.\n\n"
     "  Worth knowing: it began as an `IValidationMetadataProvider`, the tidier hook, which did "
     "not compile — the namespace resolves but `IValidationMetadataProvider` and "
     "`ValidationMetadataProviderContext` do not, in a project whose controllers use "
     "`Microsoft.AspNetCore.Mvc.Filters` happily. An action filter reaches the same result "
     "through an API this solution already builds against.",
     "Cleared for the endpoint that exposed it. The class is addressable now, not addressed"),
    ("D-18", "The sanctions' block stands, but not for the reason recorded", "OPEN",
     "The case screen has said since slice 1 that the sanctions must stay read-only \u2014 warning, "
     "suspension, fine, termination, separation \u2014 \"until the issuing-authority rule is in "
     "place\". Slice 10 made `MinimumAuthority` maintainable per action type and "
     "`RecordDecision` does contain the rule, so the condition looked met. It is not, and "
     "`probe-authority-gate.mjs` establishes why with seven passing assertions.\n\n"
     "  **No actor reaches the rule.** `HR.Discipline.Write` is granted only to HR, LegacyHrUser, "
     "SuperAdmin, TenantAdmin and Admin, and the check excludes HR, SuperAdmin and Admin **by "
     "name**. A head of department \u2014 the actor the rule was written for \u2014 holds none of "
     "those permissions and is refused at the endpoint gate long before the service check runs. "
     "There is no head-of-department role at all, which is the deferred org-authority model "
     "showing through: 0 of 41 org units have a head recorded.\n\n"
     "  **And the sanctions are not uniformly gated by the decision.** A termination is refused "
     "until the decision is confirmed (`EnsureTerminationIsFoundedAsync`); a **warning is not**, "
     "and can be recorded against a case nobody has decided. So even a working authority rule on "
     "the decision would not govern an editable warning \u2014 the sanction can be written with no "
     "decision behind it at all.\n\n"
     "  Two things would clear this: a role that actually holds `HR.Discipline.Write` without "
     "being HR (which is the org-authority model), and a founded-ness guard on the remaining "
     "sanctions matching the one termination already has. Until then an editable sanction would "
     "ship exactly the hole the original note warned about.",
     "Blocks the 10 sanction endpoints on StaffDisciplineSubEntity"),
    ("D-19", "The per-plan actions read was a summary its panel could not edit from", "DONE 2026-08-30",
     "`GET succession-plans/{id}/actions` returned `SuccessionActionSummaryDto` — id, description, "
     "type, priority, status, due date and the responsible person's NAME. "
     "`UpdateSuccessionActionDto` sends thirteen fields, so an edit form built on that read would "
     "have blanked the candidate, the assigner, the start and completion dates, both note fields, "
     "the dependency and the success flag on every save — and the plan's own detail read nests the "
     "same summary, so there was nowhere else to get them. **Sixth occurrence of the D-09 shape** "
     "and the third in this module after the four discipline reads and the per-provider premium "
     "read. The rule holds and is now stated in the interface: a per-parent read feeds a panel "
     "that must edit; a cross-record read (by status, by priority, overdue) feeds a list and stays "
     "a summary. Converted, with `DependsOnAction` and `SuccessionPlan` added to the repository's "
     "includes so `dependsOnActionDescription` and `planNumber` are not blank on the new shape. "
     "**Needs a backend rebuild.**\n\n"
     "  ⚠ Two write responses were wrong the same way and for a different reason: `AddActionAsync` "
     "and `AddCompetencyRequirementAsync` mapped a freshly-added entity whose navigations had "
     "never been loaded, so `planNumber`, `candidateEmployeeName`, `responsiblePersonName`, "
     "`assignedByName` and the competency's own code, name, category and scale maximum all came "
     "back empty on the row the panel had just created and correct after a refetch. Identical to "
     "the `uploadedByName` defect the slice-13 RUN found and a code read had missed. Both writers "
     "now re-read by id with their includes.\n\n"
     "  **Verified against the running API, 2026-08-30** — `hr-succession/run-slice14.mjs`, "
     "74 assertions, green. The read assertion is written as *every field the update payload "
     "sends comes back on the read*, field by field, rather than \"the read is not empty\" — the "
     "second would have passed on the summary, which is how the defect survived a whole area "
     "being marked complete.",
     "Was going to make the actions panel wipe nine fields per save — cleared"),
    ("D-20", "The succession action's assigner was assertable by the request body", "DONE 2026-08-30",
     "`CreateSuccessionActionDto.AssignedById` and its update twin were copied straight onto the "
     "entity's `Employee` FK while the token's id went only to `CreatedBy`, so any HR user could "
     "record a colleague as the person who assigned an action. **Sixth instance of the D-05 "
     "shape** — after the legal-review referrer, the case note author, the document uploader, the "
     "succession document uploader and the pool nominator. It survived area 13's own actor sweep "
     "because that sweep worked from the fields screens were binding, and no screen touched this "
     "one. Raising an action IS the caller assigning it, so it is stamped from the token; the "
     "field is gone from both DTOs and the update deliberately does not touch it, so the original "
     "assigner survives every later edit. Nothing had ever sent it, so no caller broke. "
     "**Needs a backend rebuild.**\n\n"
     "  **Verified against the running API, 2026-08-30.** The forged id goes over the wire under "
     "the name the DTO used to expose and is asserted to land in no field at all; then a SECOND "
     "actor raises their own action and the stamp is asserted to have followed the caller. "
     "Non-blank proves the line runs; changing with the caller proves it is the caller.",
     "Was an act-as-anyone field on the panel about to be built — cleared"),
    ("D-21", "Removing a competency requirement made it permanently unrequirable", "DONE 2026-08-30",
     "`IX_SuccessionCompetencyReq_Tenant_Plan_Competency` is unique on "
     "(TenantId, SuccessionPlanId, CompetencyId) **with no filter**, and "
     "`DeleteCompetencyRequirementAsync` goes through the generic soft delete. So a removed row "
     "kept occupying the slot and requiring the same competency again hit the index and 500'd "
     "naming nothing: **once a competency was removed from a plan it could never be required "
     "again.** Unreachable before this slice because nothing could delete one; the delete "
     "affordance is what turns it into a one-click path, which is the recurring lesson that giving "
     "a dormant field teeth turns its neighbours into defects.\n\n"
     "  **Sixth face of the area-13 soft-delete/unique-index defect**, and fixed the way the fifth "
     "was rather than with a migration: re-requiring a competency revives the removed row, exactly "
     "as re-joining a talent pool revives a membership. `AddCompetencyRequirementAsync` looks the "
     "row up with `IgnoreQueryFilters` (re-applying the tenant by hand, because that call drops "
     "the tenant filter too), revives it at the level given on the re-add, and refuses a duplicate "
     "of a LIVE row with a 400 rather than merging it silently. Reviving is also the better record "
     "— it keeps the original `CreatedAt` instead of pretending this is the first time. "
     "**Needs a backend rebuild, but no migration.**\n\n"
     "  **Verified against the running API, 2026-08-30**: remove, re-require, and the row that "
     "comes back carries the SAME id at the NEW level, with exactly one row for that competency "
     "on the plan rather than two.\n\n"
     "  ⚠ The run also turned up a stale assertion three files away. "
     "`audit-content.mjs` asserted the competency lookup was **empty**, \"until area 17 lands\" "
     "— and area 17 landed on 2026-08-19, so the assertion was demanding the wrong thing and went "
     "red the moment anything created a competency. Replaced with a shape assertion (every row "
     "carries a code and its own `proficiencyScaleMax`), and the slice now deletes the catalogue "
     "rows it mints, because a competency is shared reference data rather than one run's fixture.",
     "Was going to make the competency panel's own delete/re-add path 500 — cleared"),
    ("D-22", "`MedicalExpenseClaim`'s TypeScript type is five fields short of what the API returns", "DONE 2026-08-31",
     "`ClaimEditDialogs.tsx` binds `claim.admissionStart`, `admissionEnd`, `preAuthorizationId`, "
     "`referralId` and `leaveRequestId`; the `MedicalExpenseClaim` interface declares none of "
     "them, though `MedicalExpenseClaimDto` returns all five and `MedicalExpenseClaimUpdateRequest` "
     "sends all five. So the dialog works at runtime and fails `tsc` — five of the 36 type errors "
     "the HR subtree currently carries. Found in passing while type-checking slice 19, not by "
     "looking for it; recorded rather than fixed because fixing five of 36 in another slice's area "
     "is arbitrary.\n\n"
     "  ⚠ Worth knowing separately: **`tsc` over the whole frontend does not merely fail, it "
     "crashes** — \"Debug Failure. No error for last overload signature\" out of "
     "`resolveJsxOpeningLikeElement` — on the clean tree as well as a dirty one. So "
     "`npm run type-check` reports nothing at all and every type error in the repo is currently "
     "invisible. A scoped `tsconfig` over the HR subtree gets round it and is how this slice was "
     "checked.\n\n"
     "  **Fixed 2026-08-31 (closure lane 1).** All five are declared, plus the three display "
     "companions the DTO returns beside them (`preAuthorizationNumber`, `referralNumber`, "
     "`leaveRequestNumber`) so a panel can name what it links to rather than showing a Guid. "
     "Checked with a scoped `tsconfig.hr-slice.json`: the touched files are clean and the 31 "
     "errors that remain in that scope are all in `ClinicalRecordActions.tsx`, which this slice "
     "did not touch and which does not reference the claim type.\n\n"
     "  ⚠ The crash below is unchanged and still hides every other type error in the "
     "repo. It belongs to shared reporting (cross-module #20), and the decision on 2026-08-31 was "
     "to keep working around it rather than edit another team's file.",
     "Cleared for this type. The project-wide type-check is still silently dead"),
    ("D-23", "A travel policy could be DELETED after approval, though it could not be edited", "DONE 2026-08-30",
     "`UpdatePolicyAsync` refuses to edit an approved policy, and says why: it would change what "
     "everyone may spend with nobody approving the change. `DeletePolicyAsync` did the same thing "
     "more completely and was unguarded — and it also erases the record of a rule that really did "
     "govern spending for a period, which is the opposite of what an audit trail is for. "
     "`WithdrawPolicyAsync` already existed as the correct verb for standing a policy down. The "
     "guard was on the sibling and not on this one, which is the recurring shape: **audit the "
     "neighbourhood, not just the field.** Delete now refuses an approved policy and names "
     "withdraw in the message. ⚠ It reads the APPROVAL, not the in-force flag, so a withdrawn "
     "policy is still undeletable — withdrawing does not turn it back into a draft.",
     "Was letting an Admin silently un-cap everyone's travel — cleared"),
    ("D-24", "A travel policy rule code could be used once per policy, ever", "DONE 2026-08-30",
     "`IX_StaffTravelPolicyRules_TenantId_PolicyId_RuleCode` is unique with no `IsDeleted` filter "
     "while `DeleteRuleAsync` is a soft delete, so a removed rule went on occupying its code and "
     "re-adding it hit the index and 500'd naming nothing. **Seventh face** of the soft-delete/"
     "unique-index defect across HR, one week after the sixth (D-21, competency requirements). "
     "Unreachable until a screen could delete a rule — the recurring lesson that giving a dormant "
     "path teeth turns its neighbours into defects. Fixed the way the fifth and sixth were: "
     "re-using a removed code revives that row and overwrites its fields, while re-using a LIVE "
     "code is refused with a message naming the code.",
     "Was going to make the rules panel's own delete/re-add path 500 — cleared"),
    ("D-25", "A travel policy exception validated neither of its parents", "DONE 2026-08-30",
     "`CreateExceptionAsync` copied `StaffTravelRequestId` and `PolicyRuleId` from the body onto "
     "the row without checking either existed or belonged to the tenant, so an exception could be "
     "attached to **another tenant's travel request** — the foreign key accepts the id because it "
     "is perfectly valid, it simply is not yours — and it would then appear on this tenant's "
     "pending queue. `CreateAlertNotificationAsync` in the sibling service checks both of its "
     "parents and carries a comment saying why; this one had not followed it. Both are checked "
     "now and answer 404 naming the id. The create response also re-reads the rule, because the "
     "panel binds `policyRuleName` and a freshly added entity has no `PolicyRule` loaded.",
     "Was a cross-tenant write on the endpoint this slice was about to give a caller — cleared"),
    ("D-26", "The exception decision's reasoning was discarded by the model binder", "DONE 2026-08-30",
     "`decidePolicyException` posted a `notes` field for as long as it has existed and "
     "`DecideStaffTravelPolicyExceptionDto` had no such property, so every explanation was dropped "
     "while the screen reported success. Granting an exception authorises spend above a cap — an "
     "authority HR deliberately does not hold — so *why* is exactly what an auditor asks, and the "
     "row could not answer. **The shape a matched route cannot reveal**: the path resolves either "
     "way, instrument 01 counts the endpoint wired, and only reading the DTO or running the call "
     "finds it. `DecisionNotes` added (migration `AddTravelPolicyExceptionDecisionNotes`, "
     "COL_LENGTH-guarded and listed in `FastBuildMigrationMetadata`), and the client now sends "
     "`decisionNotes`.",
     "Was losing the justification for every travel-policy exception ever granted — cleared"),
    ("D-27", "Deleting one travel request breaks every later create, permanently", "DONE 2026-08-30",
     "`GenerateRequestNumberAsync` counted LIVE rows and formatted `TR-{year}-{count+1:D5}`, while "
     "`IX_StaffTravelRequests_TenantId_RequestNumber` is unique with **no `IsDeleted` filter** and "
     "the delete is soft. So deleting `TR-2026-00001` drops the live count to zero, the next "
     "create mints `TR-2026-00001` again, and it collides with the row still sitting there — every "
     "subsequent travel request in that tenant fails with the generic handler's 500, naming "
     "nothing.\n\n"
     "  **The first face of this defect found LIVE on a shipped path** — and by no means the eighth, which is what this entry first claimed. D-28 establishes that HR had already fixed it at least seven times (overtime, letter requests, SHE, movements, grievance, discipline, profile changes) with the same idiom. This is "
     "the same shape area 13 recorded as its worst instance — a document-number generator counting "
     "rows the schema does not agree are gone. Any tenant that has ever deleted a travel request "
     "is already in this state. Found on 2026-08-30 because a harness cleanup deleted its own "
     "fixture and the next run could not create one; confirmed in SQL (one row, `IsDeleted=1`, "
     "live count 0, unfiltered unique index) rather than inferred.\n\n"
     "  Fixed by reading the highest number ever issued, deleted rows included, instead of "
     "counting — a count is also wrong the moment the sequence has a gap, and the maximum is the "
     "only value the unique index cares about. ⚠ Still not atomic under concurrent creates; see "
     "D-28 for the mechanism that is.",
     "Was breaking travel request creation outright for any tenant that had deleted one — cleared"),
    ("D-28", "Count-based document numbers: three more fixed, and my own first list was wrong", "DONE 2026-08-30",
     "D-27 was not a one-off. **Twelve tables carry an unfiltered unique index on a number column "
     "AND a soft delete**, established from `sys.indexes` rather than by grep.\n\n"
     "  ⚠ **The first pass at classifying them was wrong, and wrong in the predicted way.** I "
     "sorted the generators by whether their FILE imports `INumberSequenceService` and named five "
     "HR tables as latent. Reading each generator instead: `StaffOvertimeRequests` and "
     "`HrLetterRequests` were **already fixed** — and their own remarks name the SHE, movement, "
     "grievance, disciplinary and profile-change generators as earlier fixes for the identical "
     "shape, with `GetQueryableIncludingDeleted` and a prefix scan. So this defect has been found "
     "and fixed at least seven times across HR before travel, the established idiom was already in "
     "the codebase, and the count of \"faces\" in D-27 understated it. **Every static instrument "
     "in this programme has cried wolf on its first pass, including this one, including when the "
     "instrument was my own reasoning.**\n\n"
     "  **Three were genuinely live, and all three are fixed 2026-08-30**: "
     "`RemoteWorkRequests` (`RWR-`), `ConsultantTimesheets` (`TS-`) and `TimesheetInvoices` "
     "(`INV-`) — each counted live rows against an unfiltered unique index, so one delete would "
     "have broken every later create in that tenant, silently and permanently. All three now read "
     "the highest number issued, deleted rows included.\n\n"
     "  **Confirmed harmless, so a future sweep does not spend time on them**: the appraisal "
     "`APR-`, company-schedule `EVT-` and `BK-`, job-analysis `MPB-` and consultant-engagement "
     "`ENG-` generators all count live rows, and none of their number columns carries a unique "
     "index. Counting is only a defect where the schema disagrees.\n\n"
     "  **Not ours**: `ProcurementMasterDataChangeRequests`, `ProjectInvoiceRequests` and "
     "`QuantitySurveyJointMeasurementRequests` have the index shape; whether their generators "
     "count was not checked, because that is the owning teams' code. Recorded as cross-module #22.\n\n"
     "  ⚠ **None of these is atomic under concurrent creates**, including the fixes. "
     "`INumberSequenceService` is the platform's answer and the training area uses it; moving the "
     "rest across needs each sequence seeded from its table's current maximum so it keeps issuing "
     "after the numbers already in the wild. That is the real end state, and it is not done.",
     "Was three silent, permanent breakages waiting on the first delete — cleared"),
    ("D-29", "Travel policy rules are enforced by nothing, so their editor and the exception flow are withheld", "OPEN",
     "`StaffTravelPolicyService` is the only consumer of the rules table anywhere in the codebase. "
     "`StaffTravelPolicyGuard` refuses a booking on the policy's own scalar caps — "
     "`MaxFlightClass*`, `MaxHotelRate*` — and never reads a rule, so `RuleType`, `LimitValue`, "
     "`ViolationAction`, `ExceptionAllowed` and `ExceptionRequiresApproval` describe a mechanism "
     "that does not run. **And that is why nothing raises a policy exception**: its producer was "
     "never built, so the queue was unreachable and unfillable at the same time.\n\n"
     "  **Decided 2026-08-30, after first building the editor and then withdrawing it.** The rules "
     "register ships **read-only** and the exception flow does **not ship**. An editable control "
     "that enforces nothing creates false assurance, and that is worse than no control: a rule set "
     "to `Block` is a promise to the person configuring it, a warning banner is a weak defence to "
     "an auditor looking at a screenshot, and hand-raised exceptions would manufacture audit "
     "records implying a control was in force and consciously waived. Checked before deciding: "
     "**every rule and exception row in the database was created by the harness** — there is no "
     "ported or seeded data, so a read-only register has no existing data to expose either.\n\n"
     "  `POST policies/{}/rules`, `PUT policies/rules/{}`, `DELETE policies/rules/{}`, "
     "`POST policies/exceptions` and `POST policies/exceptions/{}/decide` are therefore "
     "**INTENTIONAL, not gaps** — all five are implemented, harness-covered by "
     "`run-slice12-policy-authoring.mjs`, and waiting on enforcement rather than on a screen.\n\n"
     "  ⚠ **The queue cannot show this, and that is the blind spot itself.** The client methods stay in `travel-compliance.service.ts` so enforcement is a screen change — and instrument 01 matches the service layer, so it now counts all five as *wired to a screen* and `StaffTravelPolicies` has dropped out of section D altogether. The Position table above therefore over-counts by at least these five. This entry is where they are recorded, because the machine-derived sections structurally cannot hold them.\n\n"
     "  **Three things belong in the enforcement change**, recorded so they are not rediscovered: "
     "rule writes need the approval guard `UpdatePolicyAsync` has, because an approved policy's "
     "rules are part of the approved document; `(TenantId, PolicyId, RuleCode)` uniqueness should "
     "become a filtered index rather than the revive-on-re-add the service does today, because a "
     "rule code is a label rather than an identity and reviving keeps the previous rule's "
     "`CreatedAt`/`CreatedBy`; and mapping `RuleType` × `ExpenseCategory` onto real booking and "
     "claim fields is a product decision, not a coding one — what exactly does "
     "\"Meals / HardLimit / 50 per day\" compare against?",
     "Blocks nothing. It withholds 5 endpoints from the UI on purpose, and they will not re-read "
     "as gaps"),
    ("D-30", "Authorising a booking above a travel cap records no reason", "FALSE — corrected 2026-08-31",
     "Found while deciding D-29, and it is the real version of the gap the exception flow was "
     "pretending to fill. A booking that breaches the policy's cap is refused unless the caller "
     "holds `HR.Travel.Admin` and sets the exception flag — which makes Travel.Admin a financial "
     "authority — and **nothing anywhere records why they allowed it**. The booking carries the "
     "flag and no justification field. So the one spend-authorisation that genuinely happens in "
     "this module is the one with no audit reasoning, while the elaborate exception entity that "
     "does have `ExceptionReason` and now `DecisionNotes` governs a mechanism nobody runs.\n\n"
     "  A reason column on the breach flag would be a small change and worth more than the whole "
     "rules subsystem in its current state.\n\n"
     "  ⚠ **This entry was wrong, and it was checked before it was acted on.** The "
     "reason column exists and always has: `ClassExceptionReason` on `StaffTravelFlightBooking` "
     "and `RateExceptionReason` on `StaffTravelHotelBooking`, both on the create and update DTOs, "
     "both mapped in and out — and `StaffTravelBookingService` **refuses** an approved "
     "exception without one (\"A booking above the policy cap must record why the exception "
     "was granted\"), a guard that landed with the caps themselves in area 12 slice 8. "
     "`TravelBookingsPanel.tsx` carries both fields in its form schema. So the audit reasoning "
     "this entry called absent is recorded, enforced and enterable.\n\n"
     "  The lesson is the entry itself: it was written while deciding D-29, from the shape of the "
     "exception entity rather than from the booking service, and it read plausibly enough to be "
     "scheduled as work. **A ledger entry is a claim, not a finding, until something has run.**",
     "Nothing. Every cap breach ever authorised carries a reason — the entry was "
     "mistaken"),
    ("D-31", "Every traveller has been shown a destination alert with no text", "DONE 2026-08-30",
     "`alerts/country/{id}/current` returned `StaffTravelAlertSummaryDto`, which has no `Body` — "
     "and **the body is the alert**. The travel request's compliance strip renders the title, the "
     "severity and then `{a.body && …}`, so since the day it shipped a traveller has seen "
     "\"Civil unrest · High\" and never a word about what is happening, where, or what to do. A "
     "severity with no text is not a security briefing.\n\n"
     "  **TypeScript said this was fine**, because the client typed all three alert list reads as "
     "the full record while the API returned summaries — the same fiction-that-type-checks shape "
     "as the travel-type union and the group-travel summary. Only reading the DTO or running the "
     "call finds it.\n\n"
     "  The read now returns the full record and the repository includes the country. **The D-09 "
     "rule is unchanged and this is not an exception to it**: `alerts/active` and "
     "`alerts/country/{id}` are cross-record lists and stay summaries, asserted as such by the "
     "harness so a later \"consistency\" change does not quietly widen them. This read is not a "
     "list — its only job is to carry an alert's content to the person going there. "
     "**Needs a backend rebuild.**",
     "Was the whole point of the destination-alert feature, silently missing — cleared"),
    ("D-32", "The travel-alert acknowledgement was unreachable by anyone", "DONE 2026-08-30",
     "`POST compliance/alert-notifications/{id}/acknowledge` sits on `HR.Travel.Write`, and "
     "`AcknowledgeNotificationAsync` refuses anyone but the employee the alert was addressed to. "
     "Those two rules do not overlap: `HrStaffGrants` gives Travel.Write to HR staff and **never "
     "to the `Employee` role** — the map's own remarks state the rule — so a traveller is refused "
     "at the endpoint gate and an HR officer who passes it is refused by the service. The only "
     "actor who could ever use it was a travel-desk officer acknowledging an alert about their "
     "own trip.\n\n"
     "  **The same shape as D-18**, where no actor reaches the discipline authority rule: a rule "
     "nobody can reach is not a rule. Building the desk's \"send this alert to the traveller\" "
     "action without fixing it would have shipped another queue that can never be worked.\n\n"
     "  Cleared with three token-scoped routes on `StaffTravelMeController` — read, unread and "
     "acknowledge — which is the answer this codebase already uses for the asset acknowledgement "
     "and the probation review: no employee id is accepted anywhere, so there is nothing to "
     "forge. The desk route is kept and documented as deliberately unwired. **Needs a backend "
     "rebuild.**\n\n"
     "  ⚠ Proven both ways by `hr-travel/run-slice13-groups-and-alerts.mjs`: the HR officer is "
     "refused (403), the traveller is refused on the desk route (403), the traveller succeeds on "
     "`/me`, and a traveller acknowledging someone ELSE's notification gets 404 — not 403, which "
     "would confirm the notification exists.",
     "Was making 'confirm you have read this security briefing' impossible for anyone — cleared"),
    ("D-33", "The travel content audit was reporting six findings from a dead fixture id", "DONE 2026-08-30",
     "`audit-content.mjs` hard-coded a Procurement `Supplier` id seeded in SQL during area 12. The "
     "row is gone — the database has been rebuilt since — so all three booking creates failed the "
     "foreign key with the generic 500, and the three booking reads then reported \"0 rows despite "
     "a fixture\". **Six findings that read as a travel regression and were a stale fixture**, "
     "which is worse than none: red findings nobody trusts are how a real one gets missed.\n\n"
     "  It cannot be resolved at runtime either, because `api/Suppliers` answers 400 — cross-module "
     "defect #1, Procurement's dead `SuppliersController`. `VendorId` is nullable on all three "
     "booking DTOs, so the audit now sends none and drops the two `vendorName` assertions that "
     "depended on it. **38 fields resolved, 0 findings.** Restore the vendor the day Procurement's "
     "supplier list works.\n\n"
     "  The transferable point: a harness fixture pinned to an id from another module ages badly, "
     "and it fails in a direction that blames the module under test.",
     "Was hiding whether travel bookings work behind six false findings — cleared"),
    ("D-34", "Deleting a movement's detail made that movement unable to carry one ever again", "DONE 2026-08-30",
     "All four movement sub-types — `StaffPromotion`, `StaffTransfer`, `StaffDemotion`, "
     "`StaffSecondment` — carry `HasIndex(MovementId).IsUnique()` with **no `IsDeleted` filter**, "
     "while the delete is the generic soft delete. So a removed detail kept its movement's slot "
     "and re-adding one violated the index, 500ing with a message naming neither the column nor "
     "the constraint.\n\n"
     "  ⚠ **The create guard knew about the case and handled it backwards.** "
     "`GetOwnedParentMovementAsync(..., requireNoExistingDetail: true)` checks "
     "`existing != null && !existing.IsDeleted` — it explicitly TOLERATES a tombstone and lets the "
     "insert proceed into an index that counts it. Tolerating it is precisely what the index does "
     "not do.\n\n"
     "  Faces nine through twelve of this defect, all unreachable until this slice added the "
     "delete affordance — the recurring lesson that giving a dormant path teeth turns its "
     "neighbours into defects.\n\n"
     "  **Fixed by HARD-deleting the tombstone on re-create, not by reviving it**, and the "
     "divergence from D-21 and D-24 is deliberate. Those keep an identity across the gap — the "
     "plan still requires that competency. A movement detail is 1:1 with its movement, invisible "
     "to every read once soft-deleted, referenced by no foreign key anywhere, and removed *because "
     "it was recorded in error*; the replacement is a different assertion and should not inherit "
     "the old row's id or `CreatedAt`. The movement's own status history is the audit trail.\n\n"
     "  ⚠ **The first attempt at the fix was itself broken, and only the run found it.** "
     "`HardDeleteAsync` removes the row with raw SQL and does NOT detach the tracked entity, so "
     "the tombstone stayed in the change tracker, the insert gave the movement a second detail as "
     "far as EF was concerned, and — the relationship being a required 1:1 — `SaveChanges` threw "
     "*\"the association has been severed\"* rather than anything about an index. `AsNoTracking()` "
     "on the tombstone lookup; `HardDeleteAsync` only ever reads the id. **Needs a backend "
     "rebuild.**",
     "Was making the delete this slice adds a one-way door — cleared"),
    ("D-35", "Four movement sub-type write responses named neither the movement nor the employee", "DONE 2026-08-30",
     "`StaffTransfer`, `StaffSecondment`, `StaffDemotion` and `StaffActingAppointment` all mapped "
     "the entity returned by their include-less `GetOwnedAsync`, so `movementNumber`, "
     "`employeeName` and the position titles came back empty on both create and update — while "
     "`GetByMovementIdAsync` (and `GetWithDetailsAsync` for acting) sat on the same repositories "
     "with the full include graph, and the **promotion service two methods away already re-read "
     "for exactly this reason**. Seventh occurrence of the stale-nav-on-a-write-response shape.\n\n"
     "  Fixed in all four, on the creates as well as the updates, since those are the same "
     "methods. **Needs a backend rebuild.**",
     "Was going to blank three columns on every panel this slice touches — cleared"),
    ("D-36", "A movement needing employee acceptance never records who authorised it", "DONE 2026-08-30",
     "`ApproveAsync` stamps `AuthorizedById` inside `if (entity.Status == Approved)`. A movement "
     "with `RequiresEmployeeAcceptance` does not reach `Approved` at final approval — the workflow "
     "adapter puts it in `EmployeeAcceptancePending` — and `RespondAsync`, which later moves it to "
     "`Approved`, stamps the acceptance flags and the status and **not this field**. So for that "
     "entire class of movement the authoriser stayed null forever, on a field the DTO exposes and "
     "the screens render.\n\n"
     "  The condition now covers both statuses. ⚠ It stamps the approver who cleared the last "
     "step, **not** the employee who subsequently accepted: accepting is not authorising, and "
     "stamping the subject would make the record say the person being moved approved their own "
     "move. **Needs a backend rebuild.**\n\n"
     "  ⚠ **How it was nearly missed, which is the transferable part.** It surfaced as a failing "
     "assertion in `hr-movements/run-slice2.mjs` during a no-regression run — alongside three "
     "OTHER failures in the same suite that were all genuinely stale (deletes tightened to "
     "`HR.Movements.Admin` by the W3 sweep after the file was written, and a refusal message "
     "reworded). Three stale assertions in a row is exactly the conditioning that makes the fourth "
     "look like more of the same. It was checked against the service rather than adjusted to "
     "match observed behaviour, which is the only reason it was found.",
     "Was leaving the authoriser blank on every movement that needs accepting — cleared"),
    ("D-37", "A filed demotion appeal appears in no list", "DONE 2026-08-31",
     "`staff-demotions/pending-appeals` filters `EmployeeResponse == null` — it lists demotions "
     "still AWAITING an answer, not ones that have been appealed. So responding REMOVES a demotion "
     "from it, and the controller's other four reads are by id, by movement, disciplinary and "
     "performance-related. **There is no aggregated read of demotions that have actually been "
     "appealed.** HR sees an appeal only by opening that demotion's own movement, where the "
     "sub-type panel does render `employeeResponse`.\n\n"
     "  ⚠ **This corrects the queue's entry in D2**, which said HR's queue \"can only ever be "
     "empty\" for want of an employee surface. The opposite was true: it filled automatically with "
     "every demotion granting a right of appeal, and nothing could ever leave it. Giving employees "
     "a surface (slice 22) is what lets demotions leave — and is also what makes this gap start to "
     "matter, because appeals can now actually be filed.\n\n"
     "  A `filed-appeals` read would be a small addition. Not built here because it is a new "
     "endpoint rather than a caller for an existing one, and the appeal is visible on the record "
     "meanwhile.\n\n"
     "  **Built 2026-08-31 (closure lane 1)** — repository, service, "
     "`GET api/staff-demotions/filed-appeals`, a client method and a screen. ⚠ The "
     "screen was not optional: `getPendingAppeals` **had no caller either**, so HR had no appeals "
     "surface at all, and a second uncalled client method would have been one more of exactly "
     "what the coverage instruments keep finding. `/hr/movements/appeals` carries both queues "
     "side by side with their counts, because they are disjoint and neither alone is the "
     "picture. `StaffDemotionDto` gained `EmployeeName` and `EmployeeNumber` at the same time "
     "— a worklist keyed by movement number is not a worklist of people.",
     "Cleared. Both queues are reachable, which neither was"),
    ("D-38", "Two nightly HR sweeps failed every night, and two were never scheduled at all", "DONE 2026-08-31",
     "Found while acting on the demo-feedback row above, which claimed HR had no hosted services "
     "at all. It had six. **Two of them threw on every scheduled run.**\n\n"
     "  `ProbationReminderService.BuildCandidatesAsync` and "
     "`DisciplineReminderService.CollectPendingAsync` read the tenant's policy settings "
     "through `ICompanyHrPolicyProvider.GetAsync()` and "
     "`ICompanyHrPolicySettingsService.GetAsync()`, both of which resolve the tenant **from the "
     "current user**. A background service has no current user, so the read threw "
     "\"No tenant is associated with the current user\" and the sweep aborted — while "
     "the run-now button worked perfectly, because a button always has a token behind it. **The "
     "condition that triggers the bug is the absence of the thing an HTTP harness always "
     "supplies**, which is why 274 movement assertions and 415 discipline ones never saw "
     "it.\n\n"
     "  ⚠ **Proven in the API's own log, not inferred.** From one uptime window on "
     "2026-08-31: SHE swept at 03:04, movements at 03:06, travel at 03:12, assets at 03:14 — "
     "and at 03:08 both `Probation reminder sweep failed for tenant` "
     "(`ProbationReminderService.cs:152`) and `Discipline reminder sweep failed for tenant` "
     "(`DisciplineReminderService.cs:238`): the exact two lines that read policy. So "
     "FR-HR-032's confirmation reminders, FR-HR-140's expiry notices, the 48-hour "
     "written query, the four-week investigation, both appeal windows and every grievance clock "
     "had never once fired on the timer.\n\n"
     "  **Fixed** by giving both providers a `GetForTenantAsync(tenantId)` and pointing the two "
     "collectors at it — the tenant was already a parameter of both methods; only the "
     "settings read reached round it. `SeparationReminderService` gained the "
     "`RunSweepForTenantAsync(tenantId, trigger, userId)` the other five engines already had, "
     "along with tenant-explicit retirement and contract-expiry reads, and "
     "`SeparationReminderBackgroundService` hosts it at a 17-minute stagger. "
     "`check-reminder-hosts.mjs` reads the log and asserts seven hosts, seven scheduled sweeps "
     "and no failures.\n\n"
     "  **`LeaveYearEndService` is deliberately NOT hosted.** Carry-over and forfeiture move "
     "people's balances; the other seven engines only raise reminders. Putting them on a "
     "timer is a policy decision for TDC, not a defect to fix.",
     "Cleared. Two engines that had never run now do, and a third is scheduled for the first "
     "time"),
    ("D-39", "The awards attachment family is on a caller-supplied file path", "DONE 2026-08-31",
     "Found by the lane-2 triage of the `Awards` queue rows, 2026-08-31. `CreateAwardAttachmentDto` "
     "and `CreateAwardNominationAttachmentDto` take **`FileName` and `FilePath` as JSON** and store "
     "them: the sixth instance of the path sink, and the exact shape area 16 replaced — where "
     "the \"attachment\" is a string somebody typed and the list renders it beautifully.\n\n"
     "  ⚠ It is untouched rather than in use: `grep -ri attachment` over the awards screens "
     "and the awards service returns **nothing at all**. So five endpoints have no caller because "
     "the area has no upload surface, and building one over the DTO as it stands would ship the "
     "sink rather than close it. The evidence a nomination is meant to carry — the citation, "
     "the supporting letter — has never been attachable.\n\n"
     "  The fix is the established one and is mechanical: `HrAttachmentUpload.ExecuteAsync` on the "
     "way in, `HrDocumentDownload.ServeAsync` on the way out, a category constant registered in "
     "`SystemCleanScanRequired` (a declared category is not a registered one — D-11), then "
     "`DocumentUploadField` and `hrDocumentService` on the screens.\n\n"
     "  **Done 2026-08-31.** Migration `20260831135847_AddAwardAttachmentDmsColumns` (eight "
     "nullable columns, guarded on `COL_LENGTH` like its three siblings, because `rebuild-db` "
     "builds from the model and a bare AddColumn fails on a database that already has them). "
     "`FileName` and `FilePath` are GONE from both create DTOs: the file arrives as multipart "
     "through `HrAttachmentUpload`, and the gate's facts reach the service as PARAMETERS rather "
     "than DTO fields, so a caller may describe what a file is and may not say where it lives. "
     "Both families gained a token-bearing download through `HrDocumentDownload`. Category "
     "`hr-award-attachments` declared AND registered in `SystemCleanScanRequired` in the same "
     "commit, so the D-11 half-job cannot recur.\n\n"
     "  ⚠ **Proven by `hr-awards/probe-d39-attachments.mjs`, 16 assertions — the first "
     "time an award attachment has ever existed.** Upload, then: the stored name is the FILE's "
     "and the size is what the gate measured (not what a caller claimed); the download returns "
     "the same bytes, which a caller-supplied path could never promise; a JSON body naming "
     "`C:\\Windows\\System32\\config\\SAM` is refused with 415; and the delete is "
     "Admin-tier while the desk gets 403.\n\n"
     "  Two rules refused shortcuts on the way and named themselves: a CommitteeScore award "
     "cannot be conferred directly (AWD-07) and a draft nomination cannot be conferred at all.",
     "Cleared. Four of the five endpoints are wired; the attachment EDIT is still open (see D2)"),
    ("D-02", "Self-service invitation response still act-as-anyone", "OPEN",
     "events/{id}/participants/respond takes a ParticipantId and sits on the HR-desk Write "
     "policy, so today it means 'HR records the response'. That is correct for the HR screens "
     "being built now. Before any /me surface offers 'accept this invitation', it needs a "
     "self-or-permission check against the participant's own employee id.",
     "Blocks a future self-service calendar only"),
]


def rows_for(files):
    out = collections.defaultdict(list)
    for u in unwired:
        if u["file"] in files:
            out[u["file"]].append(u)
    return out


def esc(s):
    return s.replace("|", "\\|")


lines = []
w = lines.append

w("# HR Closure Programme — master ledger")
w("")
w("> Generated by `scripts/hr-coverage/04_build_ledger.py`. Re-run the four scripts in order to")
w("> refresh the machine-derived sections. Dispositions live in the generator, not in this file —")
w("> edit `DISPOSITIONS` / `DEMO_FEEDBACK` there so a regeneration never loses a decision.")
w("")
w("**Disposition key** — `BUILD` real gap · `DONE` built and verified · `INTENTIONAL` correct")
w("as-is, never report again · `FALSE` instrument artefact · `DECIDE` needs a call · `REVIEW` not")
w("yet classified.")
w("")

total_w = len([r for r in routes if r["verb"] != "Get"])
w("## Position")
w("")
w("| Measure | Count |")
w("| --- | ---: |")
w("| HR write endpoints | %d |" % total_w)
w("| Wired to a screen | %d |" % (total_w - len(unwired)))
w("| No caller found (instrument 01) | %d |" % len(unwired))
w("| Confirmed unreachable (01 ∩ 02) | %d |" % len(HIGH))
w("| Write-DTO fields no form can set | %d across %d DTOs |"
  % (sum(len(d["missing"]) for d in dto_gaps), len(dto_gaps)))
w("")

w("## A. Decisions taken")
w("")
w("| Date | Decision |")
w("| --- | --- |")
w("| 2026-08-28 | Build the Company Schedule UI in full. |")
w("| 2026-08-28 | Salary grades, levels and notches are read-only **by intent** — payroll owns the grade master. Closed; do not re-raise. |")
w("| 2026-08-28 | Employee career paths are **not** server-write-only and should get a UI. |")
w("| 2026-08-28 | Company Schedule station FKs repointed from `WorkStation` to `Location`. `WorkStation` has an empty table, no repository implementation and no endpoint, so a required station made the meeting-room form unfillable. **Needs a migration.** |")
w("| 2026-08-29 | Job Analysis: the twelve child collections are now authored from the job-description detail screen. Panels go read-only outside `Draft`/`UnderRevision`, and the delete affordance is hidden below Admin — both mirror what the API does, except the status rule, which the API does **not** enforce (see D-03). |")
w("| 2026-08-29 | Four TypeScript enum unions in `job-architecture.ts` were fiction and are corrected: `PhysicalDemandFrequency` ended in `Constantly` (it is `Continuously`), `WorkEnvironmentType` carried `Warehouse` and `Site` (neither exists) and lacked `Hybrid`/`FieldBased`/`Other`, `CompetencyType` carried `Functional` (that is `CompetencyCategory`, a different enum), and `QualificationType` was missing `TechnicalSkills` and `Language`. Each is now proven against the running API. |")
w("| 2026-08-29 | Discipline: the procedure steps, legal reviews and corrective action plan are now authorable from the case screen. The **sanctions stay read-only** — warning, suspension, fine and termination are blocked on FR-HR-080's issuing-authority rule, which is the original and still-valid reason. Investigation and hearing are read-only only because nobody has built their editors. |")
w("| 2026-08-29 | Legal-review `referredById` is now stamped from the token and removed from the create DTO (D-05). |")
w("| 2026-08-29 | The discipline case file is complete: witnesses, documents, notes and notices are authorable alongside the procedure steps, legal reviews and corrective action plan. Documents go through the controlled upload gate only. |")
w("| 2026-08-29 | **A per-case read returns the record; a cross-case read returns a summary.** Four per-case reads were returning projections that dropped the very fields their panels had to edit — the note one truncated at 100 characters. Eight reads converted (D-09). |")
w("| 2026-08-29 | Notices are issue-and-chase only, and carry **no acknowledge control on the HR screen**: the API refuses anyone but the employee the notice was issued to. |")
w("| 2026-08-29 | Two Discipline boxes stay unticked **by explanation, not omission**. `POST cases/{}/documents` is deliberately unwired — it rejects every file-location field, so through the API it can only mint a row naming a file that does not exist; it survives for the legacy migration utility. `POST cases/{}/documents/upload` **is** wired, through `hrDocumentService.upload(endpoint, file, fields)` — instrument 01 cannot resolve a path passed to a helper, the same artefact that makes EmployeesController read 73/81. |")
w("| 2026-08-29 | Medical insurance: network facilities, provider documents and premium records are administered from the provider screen; insurance claims are filed from the medical expense claim they belong to. |")
w("| 2026-08-29 | Provider documents now go through the controlled upload gate (D-10). The metadata route survives for the legacy migration utility and refuses every file-location field. |")
w("| 2026-08-29 | **HR still has no employee-policy screen.** `policies`, `dependents` and their seven write endpoints have no UI at all — only the employee self-service surface reads them. Deferred by decision, not overlooked: whether HR administers enrolment is a product call. Slice 4 demonstrates the consequence rather than arguing it — its insurance-claims section cannot run, because no policy exists to claim against. |")
w("| 2026-08-29 | Medical insurance reads 15 of 24, and the nine remaining are **accounted for, not outstanding**: seven are the deferred policy/dependent family (D-13); `POST provider-documents` is deliberately unwired (it refuses every file-location field, so it can only mint a row naming a file that does not exist); and `POST provider-documents/upload` **is** wired, through `hrDocumentService.upload(endpoint, file, fields)` — the path-builder artefact instrument 01 cannot resolve. |")
w("| 2026-08-30 | **The investigation and the hearing are recordable** (area 9 slice 11, 29 assertions). Both were read-only for no reason beyond nobody having built the editors \u2014 neither imposes a penalty, so FR-HR-080 never governed them. Five endpoints, and the panel is built around three traps: `complete` takes a bare JSON string, recording findings does NOT complete the investigation, and clearing the accompaniment must clear every representative field with it. |")
w("| 2026-08-30 | **A control that cannot enforce does not get an editor.** Travel policy RULES are read by nothing — the booking guard uses the policy's own scalar caps — so the rules register ships read-only and the policy-exception flow does not ship at all. An editable control that enforces nothing creates false assurance, which is worse than no control: a rule set to `Block` is a promise to whoever configures it, and hand-raised exceptions would manufacture audit records implying a control was in force and waived. The write paths exist and stay harness-covered, so enforcement is a screen change. |")
w("| 2026-08-30 | **Succession documents get one panel, not three.** A `SuccessionDocument` hangs off a plan, a plan candidate or a talent-pool member, and one table, one DTO and one upload route already served all three — so one frontend component takes the owner as a discriminated union and is mounted from the plan detail, the successors tab and the pool members table. The alternative was three near-identical panels drifting apart. |")
w("| 2026-08-30 | **The sanctions stay read-only, and D-18 restates why.** The recorded reason (\"blocked on FR-HR-080\") was imprecise \u2014 the rule exists \u2014 but the substance holds: no actor reaches the rule, and a warning can be recorded against a case nobody has decided. Tested rather than assumed; `probe-authority-gate.mjs` keeps both findings as passing assertions, so the day one fails is the day the gap closed. |")
w("| 2026-08-30 | **Stale-navigation-on-a-write-response, fourth instance.** `investigatorName`, `hearingOfficerName` and `representativeEmployeeName` all came back null from the create and update responses while the detail and queue reads resolved them. Four writers now re-read before mapping, the same fix the succession document uploader got. Every instance so far has been found by a harness assertion, never by reading the code. |")
w("| 2026-08-30 | **The discipline catalogue is authorable** (area 9 slice 10, 43 assertions). Offences, their procedure ladders and the sanction catalogue can all be created, corrected, reordered and retired from `/administration/hr/discipline/catalogue`. A client was previously stuck with whatever the seed shipped — unable to name an offence it had not anticipated, or fix a typo in one it had. |")
w("| 2026-08-30 | ⚠ **Instrument 01 invented a phantom route for every real one in a file holding two controllers.** It crossed every `[Route]` in a FILE with every `[Http*]` in it, so `StaffDisciplineLookup` read as 20 writes when it has 10, and `Competency` as 12 when it has 6. `api/discipline/action-types/{}/procedures` was reported for months and answers 404 — slice 10 proves it. **Fixed at the instrument**: routes are now attributed per class. 16 phantom endpoints left the backend count (2151 → 2135). |")
w("| 2026-08-30 | **A TypeScript type can be fiction nothing has caught yet.** `StaffOffense.offenseProcedures` never existed — the DTO field is `Procedures` — but no screen had read it, so nothing failed. The first screen to use it would have rendered an empty ladder with no error and no clue. Corrected, and the harness now pins the real name. Fourth instance of this shape. |")
w("| 2026-08-30 | **The Assets missing-edit family is built** (slice 18, 43 assertions). All eight endpoints wired; eight left the coverage queue on their own. Unlike medical this needed **no backend change** — every endpoint already stamped its actor (five through `UpdateEntity(dto, userId)`, surcharges through a `Stamp(entity)` helper) and every screen already fetched its record by id, so both standing checks passed before any UI was written. |")
w("| 2026-08-30 | **A wired endpoint can still be unreachable through a misspelled payload key.** `submitSurcharge` sent `proceededWithoutResponseReason` while the DTO declares `ProceedWithoutResponseReason` — \"proceeded\" against \"proceed\" — so a submit without an employee response was refused however carefully the reason was typed. Instrument 01 counts the endpoint as wired, because it is: the ROUTE matched and the BODY did not. Slice 18 asserts both spellings. |")
w("| 2026-08-30 | ⚠ **A full-project `tsc --noEmit` crashes on this repo** (TypeScript 5.9.2, \"Debug Failure. No error for last overload signature\"), and `incremental: true` with a stale `tsconfig.tsbuildinfo` had been hiding it — earlier clean runs were partial, checking only changed files. Reproduced on a clean tree with no local changes, so it predates this work. Slices are type-checked against a scoped `tsconfig` until someone finds the offending file. |")
w("| 2026-08-30 | **The recruitment block's portal decisions, all three settled by the user at kickoff.** The candidate portal (own `PortalBearer` auth) is retired — code AND schema, the table held zero rows; candidates self-register on the main JWT scheme under a **separate `Candidate` role**, never `ExternalUser`, because that role's middleware allowlist carries `/api/procurement` wholesale (live cross-module hole #11a) plus the projects/estate/support portals; and the flow is **browse public, apply logged-in** — the anonymous apply/track/cv-upload endpoints retired with the portal while the board and catalogues stay anonymous. |")
w("| 2026-08-30 | **`InternalOnly` is a blocklist, not an allowlist.** The policy refused exactly one role by name, so a new self-registered public role would have satisfied it on every internal endpoint it guards. `Candidate` is now named alongside `ExternalUser`, and the policy's comment says the rule out loud so the next public role names itself too. `CandidateAccessMiddleware` is a deliberate SIBLING of the ExternalUser fence — strict-subset allowlist, no admin bypass. |")
w("| 2026-08-30 | **Adopting an existing candidate row requires a confirmed mailbox.** Registration activates by SMS OTP, but linking an account to a pre-existing `JobCandidate` with the same email hands over that candidate's application history — and anyone can type someone else's address at registration. `SaveProfileAsync` refuses adoption until Identity's `EmailConfirmed` is true (`api/candidate/confirm-email` pair); a fresh email just creates a fresh candidate. |")
w("| 2026-08-31 | **EF relationship fixup resurrects soft-deleted children on a same-context response read.** The profile's replace-set save soft-deleted an omitted interest and the response served it straight back: `GetWithFullDetailsAsync` gained filtered includes (it was echoing deleted children to HR's `/details` too), but the save builds its response in the SAME DbContext, and fixup re-attaches the tracked, just-deleted rows regardless of the SQL. The mapper now filters `IsDeleted` as well, with a comment so the 'redundant' second filter survives review. Found by slice-f's replace-set assertion, not by reading. |")
w("| 2026-08-31 | **The tokenised offer-response page lives at the path already in people's inboxes.** Offer emails link to `{PortalUrl}/careers/portal/offer-response?token=…`; the retired Blazor page was the old target, so the Next page was built at that exact route rather than a cleaner one — changing the URL format would have dead-linked every offer already sent. |")
w("| 2026-08-29 | **The medical missing-edit family is built** (slice 6, 41 assertions). Pre-authorisations, referrals, appointments, expense claims and claim lines can all be corrected and removed; eleven endpoints left the coverage queue on their own, which is the check that the wiring is real. The claim edit also closes section E's `AdmissionStart`/`AdmissionEnd`. |")
w("| 2026-08-29 | **An edit dialog loads its record by id — never from the list row.** The appointment row carries 7 fields against the record's 32; the pre-authorisation row 9 against 34. Neither carries `purpose`, `serviceType`, `diagnosis` or `proposedTreatment`, all of which the update writes, so a row-bound dialog would have rendered them blank and blanked them on save — D-09 and D-12 one layer further out. Proved by `probe-clinical-byid.mjs` before any TypeScript was written, and now held by a standing assertion. |")
w("| 2026-08-29 | **D-16's sweep finished: eleven more transitions stamp their actor.** The blocker named the eight clinical ones; checking every `ApplyTo` helper in the medical mappers found eleven more with no actor at all — four of them moving money, and payment, flag and unflag wired and shipped. A claim being paid recorded the amount, the method and the reference, but not who did it. |")
w("| 2026-08-29 | **The D-14/D-15/D-16 slice is harness-verified, not merely built.** `hr-succession/run-slice13.mjs` (36 assertions) and `hr-medical/run-slice5-actors-and-nhis-documents.mjs` (34) are both green. The run found one defect a code read had missed — a blank `uploadedByName` on the create response — which is the reason the harness runs before the hand-over rather than after it. |")
w("| 2026-08-29 | **The succession, talent-pool and NHIS document families now go through the controlled upload gate** (D-14). The succession half is served by one new controller, `api/succession-documents`, rather than three copies of the same transport: one table and one DTO serve the plan, the candidate and the pool member, so the upload takes the owner as a parameter — the same shape `api/succession-development` already uses for development activities. All four metadata routes survive for the legacy migration utility and refuse every file-location field. |")
w("| 2026-08-29 | **Succession document uploaders are stamped from the token** (D-15). `UploadedById` is an explicit service parameter, not a DTO field, so it cannot be asserted by a caller. Fifth instance of the D-05 shape. |")
w("| 2026-08-29 | **Medical clinical transitions stamp an actor** (D-16). Eight helpers now set `UpdatedAt`/`UpdatedBy`. Not just a blocker — cancel, check-in and check-out were wired and shipped, so the defect was live. Neither the appointment nor the referral carries a domain actor FK, so the audit column is the only place an actor can go without a schema change. |")
w("| 2026-08-29 | **The section D hand-review queue is classified per endpoint, not per controller.** A controller is rarely one verdict: `Assets` is two path-builder artefacts, two employee-portal duplicates and eight genuine gaps. `ENDPOINT_DISPOSITIONS` in the generator keys on `(file, VERB, route)` so the mix survives a regeneration, and section D2 carries the reason for every line. |")
w("| 2026-08-29 | **Of the 149 endpoints that were `REVIEW`, 65 are real.** 46 are instrument artefacts — the endpoint is wired and 01 could not see it (31 through `hrDocumentService.upload`, 4 through `DocumentUploadField`, 18 through the `api/Pip` second-`[Route]` alias, 2 through the two-controllers-in-one-file bug fixed on 2026-08-30, 1 through an interpolated query string). 38 are `INTENTIONAL`: a duplicate route onto an operation that is already reachable, a raw-CRUD escape hatch superseded by a workflow, a replace-set parent that owns its children, or a boundary held on purpose. The remaining 65 are gaps with a screen to build. |")
w("| 2026-08-29 | **The dominant real gap is the missing edit.** Assets, medical clinical records, medical expense claims and their items, travel policy rules, travel groups, and the four movement sub-types all wire create and (mostly) delete, and not the correction. A record raised wrongly can be destroyed but not fixed — which is the worse of the two on anything a person is charged, paid or moved by. |")
w("| 2026-08-29 | **Two wired approval queues can never have anything in them.** `CreateExceptionAsync` (travel policy exceptions) and `CreateAlertNotificationAsync` (travel compliance alerts) each have exactly one caller — their own endpoint — and no screen calls either, while the pending-queue read and the decide/acknowledge action on both are wired. Nothing raises the thing the queue exists to work through. |")
w("| 2026-08-29 | **The employee-portal principle decides four of the queue rows.** `POST api/Assets/assignments/{}/acknowledge`, `POST api/Assets/surcharges/{}/respond` and `POST api/AppraisalNotifications/mark-all-read/{employeeId}` stay unwired because the same operation is served by a route that takes the employee from the token instead of the URL. `POST api/staff-demotions/{}/respond` is the exception that proves it: no portal route exists, so the employee surface has to be built. ⚠ The reason given here — that HR's `pending-appeals` queue was unfillable — was wrong, and corrected on 2026-08-30: that queue lists demotions awaiting an answer, so it was always full and nothing could leave it. The conclusion stands; the reasoning did not. |")
w("| 2026-08-29 | **Manpower budgets can be created and approved but not edited or deleted.** `PUT`/`DELETE api/JobAnalysis/budgets/{}` and `PUT`/`DELETE api/JobAnalysis/lines/{}` have no caller — surfaced when the whole JobAnalysis controller was enumerated, and outside that slice's scope (its disposition named the twelve job-description child collections). Area 18, unscheduled. |")
w("")

w("## B. Blockers — must clear before the dependent build starts")
w("")
for bid, title, state, detail, blocks in BLOCKERS:
    box = "x" if state.startswith("DONE") else " "
    w("- [%s] **%s — %s** · `%s`" % (box, bid, title, state))
    w("")
    w("  %s" % detail)
    w("")
    w("  _%s_" % blocks)
    w("")

w("## C. Confirmed unreachable endpoints (01 ∩ 02)")
w("")
w("Both instruments agree, and each was hand-verified in source. This is the trustworthy list.")
w("")
by_file = collections.defaultdict(list)
for r in HIGH:
    by_file[r["file"]].append(r)
for f in sorted(by_file, key=lambda x: -len(by_file[x])):
    disp, note = DISPOSITIONS.get(f, ("REVIEW", ""))
    w("### %s — `%s`" % (f.replace("Controller.cs", ""), disp))
    if note:
        w("")
        w("%s" % note)
    w("")
    seen = set()
    for r in sorted(by_file[f], key=lambda x: x["route"]):
        rt = re.sub(r"\{[^}]*\}", "{}", r["route"])
        if (r["verb"], rt) in seen:
            continue
        seen.add((r["verb"], rt))
        w("- [ ] `%-6s %s`" % (r["verb"].upper(), rt))
    w("")

w("## C2. Build checklists — committed work, every endpoint enumerated")
w("")
w("**Write boxes tick themselves.** A write box is checked when instrument 01 finds a frontend")
w("call for that exact verb and path, so the write lists cannot drift from the code.")
w("")
w("⚠ **READ boxes never tick, and an unticked GET means nothing.** Instrument 01 skips GETs")
w("outright (`if r[\"verb\"] == \"Get\": continue`), so every read below is unchecked whether or")
w("not a screen calls it — Company Schedule shows 40 of 40 writes wired and zero reads. Reads are")
w("listed only so the surface is visible. Do not read an unticked GET as a gap; this line used to")
w("claim reads were \"checked the same way\", which was false and would have sent someone hunting")
w("for hundreds of gaps that do not exist.")
w("")
UNWIRED_ALL = {(u["file"], u["verb"], u["route"]) for u in unwired}
for cfile, label in BUILD_CHECKLISTS.items():
    rs = [r for r in routes if r["file"] == cfile]
    wr = [r for r in rs if r["verb"] != "Get"]
    gt = [r for r in rs if r["verb"] == "Get"]
    done_w = sum(1 for r in wr if (r["file"], r["verb"], r["route"]) not in UNWIRED_ALL)
    w("### %s — %d of %d writes wired" % (label, done_w, len(wr)))
    w("")
    for title, group in (("Writes", wr), ("Reads", gt)):
        w("**%s**" % title)
        w("")
        seen = set()
        for r in sorted(group, key=lambda x: (x["route"], x["verb"])):
            rt = re.sub(r"\{[^}]*\}", "{}", r["route"])
            if (r["verb"], rt) in seen:
                continue
            seen.add((r["verb"], rt))
            # Reads are not tracked by instrument 01's unwired set, so only writes self-tick.
            if r["verb"] == "Get":
                box = " "
            else:
                box = " " if (r["file"], r["verb"], r["route"]) in UNWIRED_ALL else "x"
            w("- [%s] `%-6s %s`" % (box, r["verb"].upper(), rt))
        w("")

w("## D. Hand-review queue (flagged by 01 only)")
w("")
w("Instrument 01 found no caller but instrument 02 still sees the path segment in the frontend, so")
w("each of these is *either* a real gap *or* one of the three documented artefacts.")
w("")
w("**A controller is rarely one verdict.** `Assets` mixes two path-builder artefacts, two")
w("employee-portal duplicates and eight genuine gaps. So the queue is classified per endpoint in")
w("`ENDPOINT_DISPOSITIONS`, and the Disposition column below is the *mix* — the per-endpoint")
w("verdicts and their reasons are in **D2**. A row still reading plain `REVIEW` has not been")
w("looked at; a row with a mix has been looked at endpoint by endpoint.")
w("")
w("| Controller | Flagged | Total writes | Disposition | Note |")
w("| --- | ---: | ---: | --- | --- |")
totals = collections.Counter(r["file"] for r in routes if r["verb"] != "Get")
queue = [u for u in unwired if (u["file"], u["verb"], u["route"]) not in HIGH_KEYS]
counts = collections.Counter(u["file"] for u in queue)


def ekey(u):
    return (u["file"], u["verb"].upper(), re.sub(r"\{[^}]*\}", "{}", u["route"]))


def verdict_of(u):
    """Endpoint verdict, falling back to the controller's, then to REVIEW."""
    hit = ENDPOINT_DISPOSITIONS.get(ekey(u))
    if hit:
        return hit
    return DISPOSITIONS.get(u["file"], ("REVIEW", ""))


ORDER = ["BUILD", "DECIDE", "REVIEW", "INTENTIONAL", "FALSE", "DONE"]
by_ctrl = collections.defaultdict(list)
for u in queue:
    by_ctrl[u["file"]].append(u)
for f, n in counts.most_common():
    mix = collections.Counter(verdict_of(u)[0] for u in by_ctrl[f])
    if len(mix) == 1:
        cell = "`%s`" % next(iter(mix))
    else:
        cell = " · ".join("`%s` %d" % (k, mix[k]) for k in ORDER if mix.get(k))
    _, note = DISPOSITIONS.get(f, ("REVIEW", ""))
    w("| %s | %d | %d | %s | %s |" % (esc(f.replace("Controller.cs", "")), n, totals[f], cell, esc(note)))
w("")

w("## D2. Hand-review dispositions, endpoint by endpoint")
w("")
w("Each line was hand-verified in source: the controller, the write DTO and the frontend service")
w("that would call it. `FALSE` means the endpoint **is** wired and the instrument could not see it;")
w("`INTENTIONAL` means the operation is reachable another way, or is a boundary we hold on purpose;")
w("`BUILD` means nothing reaches it and something should.")
w("")
classified = [u for u in queue if ekey(u) in ENDPOINT_DISPOSITIONS]
tally = collections.Counter(verdict_of(u)[0] for u in classified)
w("**%d of the %d queued endpoints are classified here — %s.** The remaining %d were already "
  "carried by a controller-level disposition in section C's map and are not re-argued."
  % (len(classified), len(queue),
     ", ".join("%d %s" % (tally[k], k) for k in ORDER if tally.get(k)),
     len(queue) - len(classified)))
w("")
seen_e = set()
for f in sorted(by_ctrl, key=lambda x: (-len(by_ctrl[x]), x)):
    rows = [u for u in by_ctrl[f] if ekey(u) in ENDPOINT_DISPOSITIONS]
    if not rows:
        continue
    mix = collections.Counter(verdict_of(u)[0] for u in rows)
    w("### %s — %s" % (f.replace("Controller.cs", ""),
                       " · ".join("%d %s" % (mix[k], k) for k in ORDER if mix.get(k))))
    w("")
    said = {}
    for u in sorted(rows, key=lambda x: (verdict_of(x)[0], x["route"], x["verb"])):
        k = ekey(u)
        if k in seen_e:
            continue
        seen_e.add(k)
        disp, note = ENDPOINT_DISPOSITIONS[k]
        w("- `%-6s %s` — **%s**" % (k[1], k[2], disp))
        # A note repeated verbatim in one section is stated once and referred back to; 18
        # identical alias lines are noise, and the reader needs the route list, not the sentence.
        if note in said:
            w("  <br>_As `%s`._" % said[note])
        else:
            said[note] = "%s %s" % (k[1], k[2])
            w("  <br>%s" % note)
    w("")

w("## E. Fields no form can set (instrument 03)")
w("")
w("These endpoints *are* wired. The form omits fields, so the feature is degraded rather than")
w("missing — the class an endpoint audit cannot see.")
w("")
w("| DTO | Unreachable | Fields |")
w("| --- | ---: | --- |")
for d in dto_gaps:
    fields = ", ".join(d["missing"][:12]) + (" …" if len(d["missing"]) > 12 else "")
    w("| `%s` | %d of %d | %s |" % (d["dto"], len(d["missing"]), d["total"], esc(fields)))
w("")

w("## F. Demo-feedback backlog")
w("")
w("From the five pre-port feedback documents. No static instrument can find these — they are")
w("things absent from *both* sides, or present but wrong.")
w("")
w("| Area | Item | Disposition | Note |")
w("| --- | --- | --- | --- |")
for area, item, disp, note in DEMO_FEEDBACK:
    w("| %s | %s | `%s` | %s |" % (esc(area), esc(item), disp, esc(note)))
w("")

w("## G. Out of scope")
w("")
w("Closed decisions. Listed so a future sweep does not re-open them.")
w("")
for f, (disp, note) in sorted(DISPOSITIONS.items()):
    if disp == "INTENTIONAL":
        w("- **%s** — %s" % (f.replace("Controller.cs", ""), note))
w("")

os.makedirs(os.path.dirname(LEDGER), exist_ok=True)
open(LEDGER, "w", encoding="utf-8").write("\n".join(lines))
print("wrote %s (%d lines)" % (LEDGER, len(lines)))
print("  confirmed unreachable: %d across %d controllers" % (len(HIGH), len(by_file)))
print("  hand-review queue:     %d across %d controllers" % (sum(counts.values()), len(counts)))
qmix = collections.Counter(verdict_of(u)[0] for u in queue)
print("    verdicts:            %s" % ", ".join("%d %s" % (qmix[k], k) for k in ORDER if qmix.get(k)))
print("  DTO field gaps:        %d across %d DTOs" % (sum(len(d['missing']) for d in dto_gaps), len(dto_gaps)))
print("  demo-feedback rows:    %d" % len(DEMO_FEEDBACK))
