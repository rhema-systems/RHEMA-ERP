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
        ("BUILD", "Recruitment candidate CRM. Controller remarks already say 'bare since the port, no screen calling it'. Fold into the existing candidate screens."),
    "EmployeeBanksController.cs":
        ("BUILD", "Banks and branches reference data has no maintenance screen."),
    "LocationContactController.cs":
        ("BUILD", "No screen."),
    "EmployeeCareerPathController.cs":
        ("BUILD", "Decided 2026-08-28: career paths are NOT server-write-only and should have a UI."),
    "SalaryNotchesController.cs":
        ("INTENTIONAL", "Decided 2026-08-28: read-only by intent — payroll owns the grade master."),
    "SalaryLevelsController.cs":
        ("INTENTIONAL", "Decided 2026-08-28: read-only by intent — payroll owns the grade master."),
    "SalaryGradesController.cs":
        ("INTENTIONAL", "Decided 2026-08-28: read-only by intent — payroll owns the grade master."),
    "PayrollController.cs":
        ("INTENTIONAL", "Another team's module; HR integrates read-only."),
    "CandidatePortalController.cs":
        ("BUILD", "Candidate-facing recruitment portal. Reuses the external portal per the standing decision."),
    "CandidatePortalAuthController.cs":
        ("BUILD", "Candidate portal authentication."),
    "PublicRecruitmentController.cs":
        ("BUILD", "Public job board / anonymous apply."),
    "OfferResponseController.cs":
        ("BUILD", "Candidate offer response."),
    "ClientTimesheetConfirmationController.cs":
        ("BUILD", "Anonymous client confirmation link. Without it consultant billing has no client step."),
    "ConsultantClientPortalController.cs":
        ("BUILD", "Client portal for consultant engagements."),
    "ConsultantClientPortalAuthController.cs":
        ("BUILD", "Client portal authentication."),
    "HrLegacyFileMigrationController.cs":
        ("INTENTIONAL", "One-off ops tool, invoked by script."),
    "EmployeesController.cs":
        ("FALSE", "Fully wired through the path-builder helper employeeService.sub(id, 'contacts'). Instrument 01 cannot resolve a method call."),
    "PerformanceImprovementPlansController.cs":
        ("FALSE", "Classified 2026-08-29: nothing here is real. 18 flags are the api/PerformanceImprovementPlans "
                  "alias of api/Pip and 1 is the DocumentUploadField artefact; complete duplicates outcome, and "
                  "the two review-meeting writes duplicate api/PipMeeting. See D2."),
    "JobAnalysisController.cs":
        ("BUILD", "The reference case. Eleven child collections with full CRUD and a read-only UI."),
    "StaffDisciplineSupportController.cs":
        ("BUILD", "Action steps and legal reviews are displayed but can never be recorded."),
    "StaffDisciplineSubEntityController.cs":
        ("BUILD", "Corrective action items cannot be edited or removed."),
    "MedicalInsuranceController.cs":
        ("BUILD", "Network facilities, premium records, provider documents, insurance claims."),
    "JobVacancyController.cs":
        ("BUILD", "Stage assignments — create, edit, skip, delete."),
    "AwardsController.cs":
        ("BUILD", "Nomination attachments — edit and delete."),
    "EmployeeCompetencyController.cs":
        ("BUILD", "Batch assessment."),
    "SeparationsController.cs":
        ("BUILD", "Clearance — refresh assets."),
    "AppraisalWorkflowController.cs":
        ("INTENTIONAL", "Checked 2026-08-29: they do. The appraisal screens move status through the named "
                        "transitions on api/PerformanceAppraisals, each with its own preconditions."),
    "AssetsController.cs":
        ("BUILD", "Classified 2026-08-29: 8 real. Every asset child wires create and delete and none wires the edit; surcharges wire neither, and have no recall while requisitions and transfers both do. The other 4 are 2 upload artefacts and the 2 employee-portal duplicates. See D2."),
    "SuccessionPlanController.cs":
        ("BUILD", "Classified 2026-08-29: all 8 real. Competency requirements and actions are read-only in the UI; the 2 document endpoints are blocked on D-14/D-15."),
    "MedicalClinicalController.cs":
        ("DONE", "Built 2026-08-29 (slice 6). All three clinical entities are now correctable and removable from the clinical screen; the one remaining flag is the free status set, which the screen deliberately does not call. D-16 was cleared first."),
    "MedicalExpenseClaimsController.cs":
        ("DONE", "Built 2026-08-29 (slice 6). The claim, its lines and its documents are all correctable and removable; the one remaining flag is the upload artefact. The edit form also closes section E's AdmissionStart/AdmissionEnd pair."),
    "SuccessionCandidatesController.cs":
        ("BUILD", "Classified 2026-08-29: 2 real (documents, blocked on D-14/D-15). The development-activity trio duplicates api/succession-development."),
    "StaffTravelRequestsController.cs":
        ("BUILD", "Classified 2026-08-29: 3 real - a travel group cannot be edited, deleted, or have a participant removed."),
    "TalentPoolsController.cs":
        ("BUILD", "Classified 2026-08-29: 2 real (member documents, blocked on D-14/D-15); the development-activity route duplicates api/succession-development."),
    "TrainingServiceBondsController.cs":
        ("BUILD", "Classified 2026-08-29: all 3 real. Bonds are minted server-side on nomination submit, so a bond with the wrong amount copied off its program has no correction path - and it is money."),
    "PositionVacanciesController.cs":
        ("BUILD", "Classified 2026-08-29: all 3 real. The establishment screen wires reconcile and raise-requisition only; a vacancy cannot be closed by hand, annotated, or have its status set."),
    "CheckInsController.cs":
        ("BUILD", "Classified 2026-08-29: both real. Goals and review events wire an attachment panel; check-ins do not."),
    "NHISClaimsController.cs":
        ("BUILD", "Classified 2026-08-29: both real and both blocked on D-14 - the fifth caller-supplied file path in the medical module."),
    "StaffDemotionsController.cs":
        ("BUILD", "Classified 2026-08-29: both real. No delete, and the employee's own response has no surface at all, so HR's wired pending-appeals queue cannot fill."),
    "StaffTravelPoliciesController.cs":
        ("BUILD", "Classified 2026-08-29: both real. A rule cannot be edited, and nothing raises the policy exception the wired decide queue exists to rule on."),
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
        ("BUILD", "Classified 2026-08-29: 1 real - a candidate's expressed interest can be added and removed but not edited."),
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
        ("BUILD", "Classified 2026-08-29: real - an external panellist's own details cannot be corrected."),
    "LeaveTypesController.cs":
        ("BUILD", "Classified 2026-08-29: real - a leave type can never be retired, and there is no delete either."),
    "StaffActingAppointmentsController.cs":
        ("BUILD", "Classified 2026-08-29: real - the one movement sub-type where the edit rather than the delete is missing."),
    "StaffPromotionsController.cs":
        ("BUILD", "Classified 2026-08-29: real - no movement sub-type wires its Admin delete."),
    "StaffTransfersController.cs":
        ("BUILD", "Classified 2026-08-29: real - no movement sub-type wires its Admin delete."),
    "StaffSecondmentsController.cs":
        ("BUILD", "Classified 2026-08-29: real - no movement sub-type wires its Admin delete."),
    "StaffTravelComplianceController.cs":
        ("BUILD", "Classified 2026-08-29: real - nothing raises a compliance alert, so the wired acknowledge action has nothing to acknowledge."),
    "TrainingCompletionsController.cs":
        ("BUILD", "Classified 2026-08-29: real, and already in section F - bulk completion has no UI."),
    "TrainingNominationsController.cs":
        ("BUILD", "Classified 2026-08-29: real, and already in section F - the availability check is never shown."),
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


# ── Assets — 12 flagged: 2 artefacts, 2 portal duplicates, 8 real ────────────
_ASSET_EDIT = ("BUILD", "Create and delete are wired; the edit is not. The record can be raised and "
                        "destroyed but never corrected.")
_d("AssetsController.cs", [
    ("POST", "api/Assets/{}/attachments", _HELPER),
    ("POST", "api/Assets/{}/images", _HELPER),
    ("POST", "api/Assets/assignments/{}/acknowledge",
     ("INTENTIONAL", "The assignee's own signature, and `InternalOnly` refuses HR by name "
                     "(AssetActor.EnsureIsSubject). It is served by the employee's own route, "
                     "`POST api/employee-portal/assets/{}/acknowledge`, which /me/assets calls — "
                     "a portal route takes no employee id, so there is nothing to get wrong.")),
    ("POST", "api/Assets/surcharges/{}/respond",
     ("INTENTIONAL", "Same shape and same reason as acknowledge: the employee's right of reply, "
                     "refused for HR. Served by `POST api/employee-portal/asset-surcharges/{}/respond`, "
                     "wired from /me/assets.")),
    ("PUT", "api/Assets/assignments/{}", _ASSET_EDIT),
    ("PUT", "api/Assets/attributes/{}", _ASSET_EDIT),
    ("PUT", "api/Assets/maintenance/{}", _ASSET_EDIT),
    ("PUT", "api/Assets/requisitions/{}",
     ("BUILD", "The requester may correct their own undecided request and HR may correct anyone's — "
               "the controller says so — and no screen offers it.")),
    ("PUT", "api/Assets/surcharges/{}",
     ("BUILD", "Surcharges have neither an edit nor a delete: a charge raised for the wrong amount "
               "can only be waived or cancelled, which is a different fact about the employee.")),
    ("DELETE", "api/Assets/surcharges/{}",
     ("BUILD", "The only asset child with no delete wired; attributes, assignments, maintenance, "
               "requisitions and transfers all have one.")),
    ("POST", "api/Assets/surcharges/{}/recall",
     ("BUILD", "Requisitions and transfers both wire recall. The surcharge ladder wires submit but "
               "not the way back, so a charge sent for approval in error is stuck there.")),
    ("PUT", "api/Assets/transfers/{}", _ASSET_EDIT),
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

_SUCC_DOC_DELETE = ("BUILD", "Unblocked 2026-08-29 (D-14, D-15). The upload, the download and the "
                             "actor stamping all exist now; what is still missing is a screen. "
                             "Removing a document attached in error has no caller.")
_d("SuccessionPlanController.cs", [
    ("POST", "api/succession-plans/{}/competency-requirements",
     ("BUILD", "The read is wired and the panel renders; nothing can author a row.")),
    ("PUT", "api/succession-plans/competency-requirements/{}", ("BUILD", "Same collection.")),
    ("DELETE", "api/succession-plans/competency-requirements/{}", ("BUILD", "Same collection.")),
    ("POST", "api/succession-plans/{}/actions",
     ("BUILD", "`getActions` is wired; the plan's action list is read-only in the UI.")),
    ("PUT", "api/succession-plans/actions/{}", ("BUILD", "Same collection.")),
    ("DELETE", "api/succession-plans/actions/{}", ("BUILD", "Same collection.")),
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

# ── MedicalClinical — 7 flagged, all real ────────────────────────────────────
_CLIN = ("BUILD", "Create and the lifecycle transitions are wired; edit and delete are not. Every "
                  "one of the three clinical entities has the same hole, so a pre-authorization, "
                  "referral or appointment entered wrongly can only be cancelled, never corrected. "
                  "The per-record reads already return the full DTO (the list reads are the "
                  "summaries), so an edit panel has the fields it needs — but see D-16 before "
                  "building: the status transitions stamp no actor at all.")
_d("MedicalClinicalController.cs", [
    ("PUT", "api/medical-clinical/pre-authorizations/{}", _CLIN),
    ("DELETE", "api/medical-clinical/pre-authorizations/{}", _CLIN),
    ("PUT", "api/medical-clinical/referrals/{}", _CLIN),
    ("DELETE", "api/medical-clinical/referrals/{}", _CLIN),
    ("PUT", "api/medical-clinical/appointments/{}", _CLIN),
    ("PUT", "api/medical-clinical/appointments/{}/status",
     ("BUILD", "A free status set, distinct from cancel / check-in / check-out. See D-16: it takes "
               "no actor and the service records none, not even `UpdatedBy`.")),
    ("DELETE", "api/medical-clinical/appointments/{}", _CLIN),
])

# ── MedicalExpenseClaims — 6 flagged: 1 artefact, 5 real ────────────────────
_MEC = ("BUILD", "The claim ladder wires create, approval, payment, flag, unflag, items and notes — "
                 "but neither the claim nor its items can be edited or removed. A claim captured "
                 "with the wrong amount has no correction path short of approving and paying it.")
_d("MedicalExpenseClaimsController.cs", [
    ("POST", "api/medical-expense-claims/{}/documents", _HELPER),
    ("PUT", "api/medical-expense-claims/{}", _MEC),
    ("DELETE", "api/medical-expense-claims/{}", _MEC),
    ("PUT", "api/medical-expense-claims/items/{}", _MEC),
    ("DELETE", "api/medical-expense-claims/items/{}", _MEC),
    ("DELETE", "api/medical-expense-claims/documents/{}",
     ("BUILD", "The upload is wired and the download is wired; removing a receipt attached in "
               "error is not.")),
])

# ── NHISClaims — 2 flagged, both real and both blocked ──────────────────────
_NHIS_META = ("INTENTIONAL", "The metadata-only route, deliberately unwired since D-14 was cleared "
                             "on 2026-08-29. It refuses `FilePath` and the three DMS ids outright, "
                             "so through the API it can only mint a row naming no file; it survives "
                             "for the legacy migration utility. "
                             "`POST nhis-claims/documents/upload` is the supported way in.")

_NHIS_DOC = ("BUILD", "Unblocked 2026-08-29 (D-14). The gated upload and the token-bearing download "
                      "both exist; NHIS claim documents still have no screen.")
_d("NHISClaimsController.cs", [
    ("POST", "api/nhis-claims/documents", _NHIS_META),
    ("POST", "api/nhis-claims/documents/upload", _NHIS_DOC),
    ("DELETE", "api/nhis-claims/documents/{}", _NHIS_DOC),
])

# ── The transport built by the D-14 slice. Three owners, one pair of routes ──
_d("SuccessionDocumentsController.cs", [
    ("POST", "api/succession-documents/upload",
     ("BUILD", "Built 2026-08-29 to clear D-14, and waiting for a caller. The one gated upload for "
               "all three owners — succession plan, plan candidate and talent-pool member — since "
               "one table and one DTO serve all three. Its download sibling is a GET and so is "
               "invisible to instrument 01.")),
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
_d("StaffTravelPoliciesController.cs", [
    ("PUT", "api/staff-travel/policies/rules/{}",
     ("BUILD", "Rules can be added and deleted but not edited — a threshold correction means "
               "destroying the rule and its history and re-typing it.")),
    ("POST", "api/staff-travel/policies/exceptions",
     ("BUILD", "Nothing raises a policy exception. `CreateExceptionAsync` has exactly one caller — "
               "this endpoint — and no compliance sweep creates one, so the wired "
               "`exceptions/pending` queue and its wired `exceptions/{}/decide` action can never "
               "have anything to work on.")),
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
     ("BUILD", "The employee's acceptance of, or appeal against, a demotion notice — the service "
               "refuses anyone but the demoted employee, so HR cannot file it for them. HR's side "
               "is wired (`pending-appeals` reads the queue); the employee has no surface at all, "
               "so the queue can only ever be empty. Unlike the asset acknowledge/respond pair "
               "there is no employee-portal route to fall back on: this one needs building on /me.")),
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
_d("CompetencyController.cs", [
    ("POST", "api/competency-skill-indicators", _ALIAS("`api/competencies`")),
    ("POST", "api/competency-skill-indicators/{}/skill-indicators",
     _ALIAS("`api/competencies/{}/skill-indicators`")),
])

# ── Single real gaps ────────────────────────────────────────────────────────
_d("JobCandidateController.cs", [
    ("POST", "api/job-candidates/{}/documents", _HELPER),
    ("PUT", "api/job-candidates/{}/interests/{}",
     ("BUILD", "A candidate's expressed interest can be added and removed but not edited.")),
])
_d("AwardsMeController.cs", [
    ("PUT", "api/awards/me/reviews/{}",
     ("BUILD", "Scoring a nomination is wired; revising a score the caller themselves gave is not, "
               "so a mistyped score is final.")),
])
_d("JobInterviewController.cs", [
    ("PUT", "api/job-interviews/external-panelists/{}",
     ("BUILD", "Add, remove, attendance and scores are all wired; correcting an external "
               "panellist's own details is not.")),
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
    ("Platform", "No AddHostedService registration for HR", "BUILD", "Forfeiture, carry-over expiry, vacancy and retirement alerts, all reminder sweeps"),
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
    ("D-03", "A child write on an approved job description is accepted by the API", "OPEN",
     "None of the twelve child-collection writes checks status. `AddPhysicalDemandAsync` and its "
     "eleven siblings call `GetOwnedJobDescriptionAsync`, which verifies the tenant and stops — so "
     "the duties of an approved, in-force job description can be rewritten with no new version and "
     "no trace. Proven against the running API by slice 13, which records it as a passing "
     "assertion so the day it starts failing is the day the server grew a gate. The authoring "
     "panels refuse it client-side (`AUTHORABLE_JOB_DESCRIPTION_STATUSES`) and that is the only "
     "thing stopping it. A server-side guard belongs on the service, not the screen.",
     "Nothing — the UI compensates. Raised so the compensation is not mistaken for a rule."),
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
    ("D-17", "`POST api/talent-pools` 500s when ownerId is omitted", "OPEN",
     "`CreateTalentPoolDto.OwnerId` is `[Required]` but typed as a non-nullable `Guid`, and "
     "`[Required]` does not reject `Guid.Empty` — so an omitted owner passes model validation "
     "intact and dies at the database on `FK_TalentPools_Employees_OwnerId` with error 547, "
     "surfacing as the generic handler's 500 that names neither the field nor the constraint. "
     "The same shape as D-04, where a wrong-catalogue qualification id 500'd naming nothing. Found "
     "by slice 13 tripping over it while building a talent-pool fixture, not by looking for it. "
     "The fix is a validation guard that rejects `Guid.Empty` with a message naming the field; "
     "sending the id is the workaround, not the fix. Worth a sweep rather than a one-line patch — "
     "`[Required]` on a non-nullable `Guid` is inert everywhere it appears, and this DTO family "
     "uses it heavily.",
     "Blocks nothing built so far. Recorded because a 500 that names nothing costs someone an hour "
     "the next time"),
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
w("| 2026-08-29 | **The medical missing-edit family is built** (slice 6, 41 assertions). Pre-authorisations, referrals, appointments, expense claims and claim lines can all be corrected and removed; eleven endpoints left the coverage queue on their own, which is the check that the wiring is real. The claim edit also closes section E's `AdmissionStart`/`AdmissionEnd`. |")
w("| 2026-08-29 | **An edit dialog loads its record by id — never from the list row.** The appointment row carries 7 fields against the record's 32; the pre-authorisation row 9 against 34. Neither carries `purpose`, `serviceType`, `diagnosis` or `proposedTreatment`, all of which the update writes, so a row-bound dialog would have rendered them blank and blanked them on save — D-09 and D-12 one layer further out. Proved by `probe-clinical-byid.mjs` before any TypeScript was written, and now held by a standing assertion. |")
w("| 2026-08-29 | **D-16's sweep finished: eleven more transitions stamp their actor.** The blocker named the eight clinical ones; checking every `ApplyTo` helper in the medical mappers found eleven more with no actor at all — four of them moving money, and payment, flag and unflag wired and shipped. A claim being paid recorded the amount, the method and the reference, but not who did it. |")
w("| 2026-08-29 | **The D-14/D-15/D-16 slice is harness-verified, not merely built.** `hr-succession/run-slice13.mjs` (36 assertions) and `hr-medical/run-slice5-actors-and-nhis-documents.mjs` (34) are both green. The run found one defect a code read had missed — a blank `uploadedByName` on the create response — which is the reason the harness runs before the hand-over rather than after it. |")
w("| 2026-08-29 | **The succession, talent-pool and NHIS document families now go through the controlled upload gate** (D-14). The succession half is served by one new controller, `api/succession-documents`, rather than three copies of the same transport: one table and one DTO serve the plan, the candidate and the pool member, so the upload takes the owner as a parameter — the same shape `api/succession-development` already uses for development activities. All four metadata routes survive for the legacy migration utility and refuse every file-location field. |")
w("| 2026-08-29 | **Succession document uploaders are stamped from the token** (D-15). `UploadedById` is an explicit service parameter, not a DTO field, so it cannot be asserted by a caller. Fifth instance of the D-05 shape. |")
w("| 2026-08-29 | **Medical clinical transitions stamp an actor** (D-16). Eight helpers now set `UpdatedAt`/`UpdatedBy`. Not just a blocker — cancel, check-in and check-out were wired and shipped, so the defect was live. Neither the appointment nor the referral carries a domain actor FK, so the audit column is the only place an actor can go without a schema change. |")
w("| 2026-08-29 | **The section D hand-review queue is classified per endpoint, not per controller.** A controller is rarely one verdict: `Assets` is two path-builder artefacts, two employee-portal duplicates and eight genuine gaps. `ENDPOINT_DISPOSITIONS` in the generator keys on `(file, VERB, route)` so the mix survives a regeneration, and section D2 carries the reason for every line. |")
w("| 2026-08-29 | **Of the 149 endpoints that were `REVIEW`, 65 are real.** 46 are instrument artefacts — the endpoint is wired and 01 could not see it (31 through `hrDocumentService.upload`, 4 through `<DocumentUploadField>`, 20 through a second `[Route]` alias, 1 through an interpolated query string). 38 are `INTENTIONAL`: a duplicate route onto an operation that is already reachable, a raw-CRUD escape hatch superseded by a workflow, a replace-set parent that owns its children, or a boundary held on purpose. The remaining 65 are gaps with a screen to build. |")
w("| 2026-08-29 | **The dominant real gap is the missing edit.** Assets, medical clinical records, medical expense claims and their items, travel policy rules, travel groups, and the four movement sub-types all wire create and (mostly) delete, and not the correction. A record raised wrongly can be destroyed but not fixed — which is the worse of the two on anything a person is charged, paid or moved by. |")
w("| 2026-08-29 | **Two wired approval queues can never have anything in them.** `CreateExceptionAsync` (travel policy exceptions) and `CreateAlertNotificationAsync` (travel compliance alerts) each have exactly one caller — their own endpoint — and no screen calls either, while the pending-queue read and the decide/acknowledge action on both are wired. Nothing raises the thing the queue exists to work through. |")
w("| 2026-08-29 | **The employee-portal principle decides four of the queue rows.** `POST api/Assets/assignments/{}/acknowledge`, `POST api/Assets/surcharges/{}/respond` and `POST api/AppraisalNotifications/mark-all-read/{employeeId}` stay unwired because the same operation is served by a route that takes the employee from the token instead of the URL. `POST api/staff-demotions/{}/respond` is the exception that proves it: no portal route exists, so HR's wired `pending-appeals` queue is unfillable and the employee surface has to be built. |")
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
