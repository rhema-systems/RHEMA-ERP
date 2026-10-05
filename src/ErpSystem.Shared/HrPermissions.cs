namespace ErpSystem.Shared;

public sealed record HrPermissionDefinition(
    string Name,
    string DisplayName,
    string Description,
    string Category);

/// <summary>
/// Named HR permissions and the policies built from them, mirroring
/// <see cref="FinancePermissions"/>.
/// </summary>
/// <remarks>
/// <para>Only the occupational-health slice is populated for now. It exists because the medical
/// controllers previously carried a bare <c>[Authorize]</c>, which meant any authenticated user
/// — every employee with an ERP login — could list health profiles, read conditions, allergies
/// and exam documents, and delete them. Role strings would have closed that, but medical data
/// warrants the same explicit, greppable permission model Finance already uses.</para>
///
/// <para>This class is the home for future HR permissions; the rest of the HR module still uses
/// role strings and is not in scope here.</para>
/// </remarks>
public static class HrPermissions
{
    public const string CategoryLeave = "HR - Leave";
    public const string CategoryAttendance = "HR - Attendance & Time";
    public const string CategoryCompensation = "HR - Compensation & Benefits";
    public const string CategoryTraining = "HR - Training & Learning";
    public const string CategoryRecruitment = "HR - Recruitment";
    public const string CategoryMedical = "HR - Occupational Health";
    public const string CategoryTravel = "HR - Staff Travel";
    public const string CategorySuccession = "HR - Succession & Talent";
    public const string CategoryProbation = "HR - Probation & Confirmation";
    public const string CategoryJobArchitecture = "HR - Job Architecture";
    public const string CategoryCompetency = "HR - Competency";
    public const string CategoryManpowerBudget = "HR - Manpower Budget & Establishment";
    public const string CategorySeparation = "HR - Separation, Clearance & Exit";
    public const string CategoryPayValuation = "HR - Pay Valuation (Finance)";
    public const string CategoryAwards = "HR - Staff Awards & Recognition";
    public const string CategoryPerformance = "HR - Performance";
    public const string CategoryEmployee = "HR - Employee Records & Foundation";
    /// <summary>
    /// Renamed 2026-09-03 from "HR - Safety, Health &amp; Environment": the roles screen groups
    /// permissions by this label, and SHE is its own function with its own roles (DR-10). The
    /// permission NAMES stay <c>HR.She.*</c> — 314 attributes, seeded rows and the harness ride
    /// on them. The seeder re-labels existing rows in place.
    /// </summary>
    public const string CategoryShe = "Safety (SHE)";
    public const string CategoryOrientation = "HR - Orientation & Onboarding";
    public const string CategoryAssets = "HR - Staff Assets";
    public const string CategoryMovements = "HR - Staff Movements";
    public const string CategoryDiscipline = "HR - Discipline & Grievance";
    public const string CategoryCompany = "HR - Company & Administration";

    /// <summary>Prefix identifying HR permissions, used by the role-fallback handler.</summary>
    public const string Prefix = "HR.";

    public const string ViewLeave = "HR.Leave.Read";
    public const string MaintainLeave = "HR.Leave.Write";
    public const string AdministerLeave = "HR.Leave.Admin";

    public const string LeaveReadPolicy = "HR.Policy.LeaveRead";
    public const string LeaveWritePolicy = "HR.Policy.LeaveWrite";
    public const string LeaveAdminPolicy = "HR.Policy.LeaveAdmin";

    public const string ViewAttendance = "HR.Attendance.Read";
    public const string MaintainAttendance = "HR.Attendance.Write";
    public const string AdministerAttendance = "HR.Attendance.Admin";

    public const string AttendanceReadPolicy = "HR.Policy.AttendanceRead";
    public const string AttendanceWritePolicy = "HR.Policy.AttendanceWrite";
    public const string AttendanceAdminPolicy = "HR.Policy.AttendanceAdmin";

    public const string ViewCompensation = "HR.Compensation.Read";
    public const string MaintainCompensation = "HR.Compensation.Write";
    public const string AdministerCompensation = "HR.Compensation.Admin";

    public const string CompensationReadPolicy = "HR.Policy.CompensationRead";
    public const string CompensationWritePolicy = "HR.Policy.CompensationWrite";
    public const string CompensationAdminPolicy = "HR.Policy.CompensationAdmin";

    public const string ViewTraining = "HR.Training.Read";
    public const string MaintainTraining = "HR.Training.Write";
    public const string AdministerTraining = "HR.Training.Admin";

    public const string TrainingReadPolicy = "HR.Policy.TrainingRead";
    public const string TrainingWritePolicy = "HR.Policy.TrainingWrite";
    public const string TrainingAdminPolicy = "HR.Policy.TrainingAdmin";

    public const string ViewRecruitment = "HR.Recruitment.Read";
    public const string MaintainRecruitment = "HR.Recruitment.Write";
    public const string AdministerRecruitment = "HR.Recruitment.Admin";

    public const string RecruitmentReadPolicy = "HR.Policy.RecruitmentRead";
    public const string RecruitmentWritePolicy = "HR.Policy.RecruitmentWrite";
    public const string RecruitmentAdminPolicy = "HR.Policy.RecruitmentAdmin";

    public const string ViewMedicalRecords = "HR.Medical.Read";
    public const string MaintainMedicalRecords = "HR.Medical.Write";
    public const string AdministerMedical = "HR.Medical.Admin";

    public const string MedicalReadPolicy = "HR.Policy.MedicalRead";
    public const string MedicalWritePolicy = "HR.Policy.MedicalWrite";
    public const string MedicalAdminPolicy = "HR.Policy.MedicalAdmin";

    public const string ViewTravel = "HR.Travel.Read";
    public const string MaintainTravel = "HR.Travel.Write";
    public const string AdministerTravel = "HR.Travel.Admin";

    public const string TravelReadPolicy = "HR.Policy.TravelRead";
    public const string TravelWritePolicy = "HR.Policy.TravelWrite";
    public const string TravelAdminPolicy = "HR.Policy.TravelAdmin";

    public const string ViewSuccession = "HR.Succession.Read";
    public const string MaintainSuccession = "HR.Succession.Write";
    public const string AdministerSuccession = "HR.Succession.Admin";

    public const string SuccessionReadPolicy = "HR.Policy.SuccessionRead";
    public const string SuccessionWritePolicy = "HR.Policy.SuccessionWrite";
    public const string SuccessionAdminPolicy = "HR.Policy.SuccessionAdmin";

    public const string ViewProbation = "HR.Probation.Read";
    public const string MaintainProbation = "HR.Probation.Write";
    public const string AdministerProbation = "HR.Probation.Admin";

    public const string ProbationReadPolicy = "HR.Policy.ProbationRead";
    public const string ProbationWritePolicy = "HR.Policy.ProbationWrite";
    public const string ProbationAdminPolicy = "HR.Policy.ProbationAdmin";

    public const string ViewJobArchitecture = "HR.JobArchitecture.Read";
    public const string MaintainJobArchitecture = "HR.JobArchitecture.Write";
    public const string AdministerJobArchitecture = "HR.JobArchitecture.Admin";

    public const string JobArchitectureReadPolicy = "HR.Policy.JobArchitectureRead";
    public const string JobArchitectureWritePolicy = "HR.Policy.JobArchitectureWrite";
    public const string JobArchitectureAdminPolicy = "HR.Policy.JobArchitectureAdmin";

    public const string ViewCompetency = "HR.Competency.Read";
    public const string MaintainCompetency = "HR.Competency.Write";
    public const string AdministerCompetency = "HR.Competency.Admin";

    public const string CompetencyReadPolicy = "HR.Policy.CompetencyRead";
    public const string CompetencyWritePolicy = "HR.Policy.CompetencyWrite";
    public const string CompetencyAdminPolicy = "HR.Policy.CompetencyAdmin";

    public const string ViewManpowerBudget = "HR.ManpowerBudget.Read";
    public const string MaintainManpowerBudget = "HR.ManpowerBudget.Write";
    public const string AdministerManpowerBudget = "HR.ManpowerBudget.Admin";

    public const string ManpowerBudgetReadPolicy = "HR.Policy.ManpowerBudgetRead";
    public const string ManpowerBudgetWritePolicy = "HR.Policy.ManpowerBudgetWrite";
    public const string ManpowerBudgetAdminPolicy = "HR.Policy.ManpowerBudgetAdmin";

    public const string ViewSeparation = "HR.Separation.Read";
    public const string MaintainSeparation = "HR.Separation.Write";
    public const string AdministerSeparation = "HR.Separation.Admin";

    public const string SeparationReadPolicy = "HR.Policy.SeparationRead";
    public const string SeparationWritePolicy = "HR.Policy.SeparationWrite";
    public const string SeparationAdminPolicy = "HR.Policy.SeparationAdmin";

    /// <summary>
    /// Finance's valuation of the pay HR records (leave settings audit 2, decision P2): a leaver's
    /// settlement pay lines and leave cashed in while employed. HR records the days; the holder of
    /// this puts the money on them.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately NOT in <see cref="HrStaffGrants"/>: the one who counts the days is not the one
    /// who prices them. The stakeholders asked for HR to leave the calculation to Finance, and until
    /// this existed the "indicative" figure HR worked out went into Finance's books on release.
    /// </remarks>
    public const string ValueHrPay = "HR.Pay.Value";
    public const string PayValuePolicy = "HR.Policy.PayValue";

    public const string ViewPerformance = "HR.Performance.Read";
    public const string MaintainPerformance = "HR.Performance.Write";
    public const string AdministerPerformance = "HR.Performance.Admin";

    /// <summary>
    /// Read the pay and employment proposals an appraisal raised, and nothing else of performance (F3, D-94) — what
    /// the Managing Director needs to decide the proposals the record says are theirs.
    /// </summary>
    public const string ViewPerformanceProposals = "HR.Performance.Proposals.Read";

    public const string PerformanceReadPolicy = "HR.Policy.PerformanceRead";
    /// <summary>The proposals' reads: the performance desk, or a proposal decider (<see cref="ViewPerformanceProposals"/>).</summary>
    public const string PerformanceProposalsReadPolicy = "HR.Policy.PerformanceProposalsRead";
    public const string PerformanceWritePolicy = "HR.Policy.PerformanceWrite";
    public const string PerformanceAdminPolicy = "HR.Policy.PerformanceAdmin";

    public const string ViewEmployees = "HR.Employee.Read";
    public const string MaintainEmployees = "HR.Employee.Write";
    public const string AdministerEmployees = "HR.Employee.Admin";

    public const string EmployeeReadPolicy = "HR.Policy.EmployeeRead";
    public const string EmployeeWritePolicy = "HR.Policy.EmployeeWrite";
    public const string EmployeeAdminPolicy = "HR.Policy.EmployeeAdmin";

    public const string ViewShe = "HR.She.Read";
    public const string MaintainShe = "HR.She.Write";
    public const string AdministerShe = "HR.She.Admin";

    public const string SheReadPolicy = "HR.Policy.SheRead";
    public const string SheWritePolicy = "HR.Policy.SheWrite";
    public const string SheAdminPolicy = "HR.Policy.SheAdmin";

    public const string ViewOrientation = "HR.Orientation.Read";
    public const string MaintainOrientation = "HR.Orientation.Write";
    public const string AdministerOrientation = "HR.Orientation.Admin";

    public const string OrientationReadPolicy = "HR.Policy.OrientationRead";
    public const string OrientationWritePolicy = "HR.Policy.OrientationWrite";
    public const string OrientationAdminPolicy = "HR.Policy.OrientationAdmin";

    public const string ViewAssets = "HR.Assets.Read";
    public const string MaintainAssets = "HR.Assets.Write";
    public const string AdministerAssets = "HR.Assets.Admin";

    public const string AssetsReadPolicy = "HR.Policy.AssetsRead";
    public const string AssetsWritePolicy = "HR.Policy.AssetsWrite";
    public const string AssetsAdminPolicy = "HR.Policy.AssetsAdmin";

    public const string ViewMovements = "HR.Movements.Read";
    public const string MaintainMovements = "HR.Movements.Write";
    public const string AdministerMovements = "HR.Movements.Admin";

    public const string MovementsReadPolicy = "HR.Policy.MovementsRead";
    public const string MovementsWritePolicy = "HR.Policy.MovementsWrite";
    public const string MovementsAdminPolicy = "HR.Policy.MovementsAdmin";

    public const string ViewDiscipline = "HR.Discipline.Read";
    public const string MaintainDiscipline = "HR.Discipline.Write";
    public const string AdministerDiscipline = "HR.Discipline.Admin";

    public const string DisciplineReadPolicy = "HR.Policy.DisciplineRead";
    public const string DisciplineWritePolicy = "HR.Policy.DisciplineWrite";
    public const string DisciplineAdminPolicy = "HR.Policy.DisciplineAdmin";

    public const string ViewAwards = "HR.Awards.Read";
    public const string MaintainAwards = "HR.Awards.Write";
    public const string AdministerAwards = "HR.Awards.Admin";

    public const string AwardsReadPolicy = "HR.Policy.AwardsRead";
    public const string AwardsWritePolicy = "HR.Policy.AwardsWrite";
    public const string AwardsAdminPolicy = "HR.Policy.AwardsAdmin";

    public const string ViewCompany = "HR.Company.Read";
    public const string MaintainCompany = "HR.Company.Write";
    public const string AdministerCompany = "HR.Company.Admin";

    public const string CompanyReadPolicy = "HR.Policy.CompanyRead";
    public const string CompanyWritePolicy = "HR.Policy.CompanyWrite";
    public const string CompanyAdminPolicy = "HR.Policy.CompanyAdmin";

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // THE APPROVE TIER — interim authority where no workflow definition is published
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //
    // ⚠ Added 2026-09-16. Read this before using, extending or removing any of them.
    //
    // WHAT THEY MEAN. "May rule on a record of this kind **when no workflow definition is
    // published for its entity type**." Nothing more. Once a definition exists, the engine names
    // the approver per step and none of these are consulted — see HrWorkflowFallbackAuthority.
    //
    // WHY THEY EXIST. WorkflowIntegrationService.SubmitAsync returns WorkflowOutcome.Approved
    // whenever no active definition exists, and every HR status adapter maps that to its own
    // approved status. So where no definition is published, pressing Submit *approves the record*
    // — no approver, no segregation of duties. Making submit stop at a pending status needs
    // somewhere for the authority to come from, and there was nowhere: the permission model
    // deliberately has no approver concept, because approval authority is meant to come from the
    // definition.
    //
    // ⚠ Corrected 2026-09-16: an earlier version said "No HR definition is seeded anywhere in the
    // solution". EnsureHrWorkflowsSeededAsync seeds 28 of them, published and active, so on a
    // seeded tenant the fallback never runs and these permissions are inert. They exist for the
    // unseeded tenant — a provisioning that did not complete, a definition someone unpublished, a
    // tenant older than the seeder. Inert-by-default is the intended resting state.
    //
    // ⚠ WHY NOT THE ADMIN TIER. It was the obvious candidate and it is wrong. The Admin tiers are
    // DESTRUCTIVE-OPERATIONS permissions, not approval permissions, and they say so themselves —
    // AdministerLeave's description reads "Approving leave is NOT this permission — approval
    // belongs to the workflow assignee and is validated per request by the workflow engine";
    // AdministerDiscipline says the natural-justice rules are enforced on the record, "not granted
    // here"; AdministerCompensation is about deleting grades and deactivating pay components.
    // Pinning approval to them would misuse them, and granting them to the HR desk so it could
    // approve would widen HR's destructive reach as a side effect.
    //
    // ⚠ FIVE ARE DELIBERATELY NOT GRANTED TO THE HR DESK — see HrStaffGrants. Separation,
    // Probation, Succession, JobArchitecture and ManpowerBudget each carry a requirement-driven
    // objection to HR approving at all. They are defined here so the mechanism is uniform; the
    // grant is where the decision lives, which is the right place for it to be reversed.
    //
    // WHEN TO DELETE THEM. When every HR entity type has a published definition, these do nothing
    // and should go. They are in one block, and each is named .Approve, so both are easy to find.

    public const string ApproveLeave = "HR.Leave.Approve";
    public const string ApproveAttendance = "HR.Attendance.Approve";
    public const string ApproveCompensation = "HR.Compensation.Approve";
    public const string ApproveTraining = "HR.Training.Approve";
    public const string ApproveRecruitment = "HR.Recruitment.Approve";
    public const string ApproveTravel = "HR.Travel.Approve";
    public const string ApprovePerformance = "HR.Performance.Approve";
    public const string ApproveAssets = "HR.Assets.Approve";
    public const string ApproveMovements = "HR.Movements.Approve";
    public const string ApproveDiscipline = "HR.Discipline.Approve";
    public const string ApproveCompany = "HR.Company.Approve";

    // ── The four the HR desk is NOT granted ──────────────────────────────────────────────────
    //
    // ⚠ There is deliberately NO ApproveSeparation. It was declared and then removed on the same
    // day, once SeparationService was read properly: RequireDecisionAuthority already enforces
    // FR-HR-092 on the record — the Managing Director signs any exit, HR only a procedural one —
    // and its own comment notes that it holds "even if the definition is missing or wrong". That
    // is stronger than a permission and it runs first.
    //
    // Adding a permission gate on top would have BLOCKED THE MANAGING DIRECTOR, who holds only
    // ViewSeparation by design; see ApprovalReaderGrants, which says the authority to decide "is
    // not a permission at all, it is read off the record". A second gate there would refuse the
    // one person the requirement names. Separation's no-workflow branch therefore carries no
    // permission check, and the record rule is the whole authority.
    public const string ApproveProbation = "HR.Probation.Approve";
    public const string ApproveSuccession = "HR.Succession.Approve";
    public const string ApproveJobArchitecture = "HR.JobArchitecture.Approve";
    public const string ApproveManpowerBudget = "HR.ManpowerBudget.Approve";

    public static readonly HrPermissionDefinition[] All =
    {
        new(ViewLeave, "View Leave",
            "View all leave requests, balances and their adjustments, leave plans, encashments and mandatory-leave compliance across the organisation. Employees do not need this to see their own leave — self access is an ownership check on the endpoint, not a permission.",
            CategoryLeave),
        new(MaintainLeave, "Maintain Leave",
            "Raise and amend leave on behalf of staff, record balance adjustments, close completed leave, recalculate balances, process encashment payments, and maintain the leave-type catalogue (sub-types, allocations, eligibility, accrual policies).",
            CategoryLeave),
        new(AdministerLeave, "Administer Leave",
            "Run the year-end carry-over and forfeiture jobs, deactivate leave types, and delete adjustments and leave-type configuration. Approving leave is NOT this permission — approval belongs to the workflow assignee and is validated per request by the workflow engine.",
            CategoryLeave),

        new(ViewAttendance, "View Attendance & Time",
            "View everyone's attendance records, raw device logs, regularizations, remote-work requests, alerts, biometric enrolment, payroll exports, and the consultant/timesheet/invoicing registers. Employees do not need this for their own records — self access is an ownership check on the endpoint.",
            CategoryAttendance),
        new(MaintainAttendance, "Maintain Attendance & Time",
            "Correct attendance records, capture and process device logs, apply approved regularizations, maintain shift/schedule/holiday/pay-period/geofence/device configuration, manage alerts, generate payroll exports, and run consultant, engagement and timesheet-invoice administration.",
            CategoryAttendance),
        new(AdministerAttendance, "Administer Attendance & Time",
            "Delete attendance data — records, logs, alerts, biometric enrolment, configuration rows, engagements and invoices. Approving a regularization, remote-work request or consultant timesheet is NOT this permission — approval belongs to the workflow assignee, validated per request by the workflow engine.",
            CategoryAttendance),

        new(ViewCompensation, "View Compensation & Benefits",
            "View position and employee emoluments, the pay-component master and its HR attributes, the salary grade/level/notch structure, benefit grade-values, enrollment registers and payroll lines. Employees do not need this for their own pay makeup, benefits or beneficiaries — self access is an ownership check on the endpoint.",
            CategoryCompensation),
        new(MaintainCompensation, "Maintain Compensation & Benefits",
            "Assign and amend position/employee pay components, maintain the benefit-policy catalogue and its grade values, enroll employees and manage enrollment status, decide utilization claims, reconcile, sync the pay-component and salary-structure mirrors from payroll, and maintain HR pay attributes.",
            CategoryCompensation),
        new(AdministerCompensation, "Administer Compensation & Benefits",
            "Delete emolument assignments, benefit policies and their grade values, and salary grades/levels/notches, and deactivate pay components and benefit policies.",
            CategoryCompensation),

        new(ViewTraining, "View Training & Learning",
            "View the organisation-wide training surface: the nomination and request registers, completions, needs assessments, budgets, plans, vendors, trainers, enrollments, mentoring pairs, service bonds and the dashboard. Employees do not need this for their own training record — self access is an ownership check on the endpoint.",
            CategoryTraining),
        new(MaintainTraining, "Maintain Training & Learning",
            "Run the training desk: maintain programs, plans, schedules, nominate and enroll on behalf of staff, decide training requests, record and verify completions, issue certificates, mark attendance, and maintain needs assessments, budgets, vendors, trainers, learning paths, mentoring and compliance assignments. Employee self-acts (self-nomination, requests, waitlist, bond acceptance, mentoring sessions, learning-path steps) are ownership checks on the endpoint, not this permission.",
            CategoryTraining),
        new(AdministerTraining, "Administer Training & Learning",
            "Approve training budgets and annual training plans, revoke certificates, blacklist vendors, waive service bonds, and delete training records and catalogue configuration. Approving a nomination is NOT this permission — that belongs to the workflow assignee, validated per request by the workflow engine.",
            CategoryTraining),

        new(ViewRecruitment, "View Recruitment",
            "View the recruitment surface: requisitions, vacancies, adverts and postings, the candidate register and applications with their documents, interview schedules and scores, offers, hires and pre-employment checks. A panelist does not need this for their own panel — panel access is a membership check on the endpoint.",
            CategoryRecruitment),
        new(MaintainRecruitment, "Maintain Recruitment",
            "Run the recruitment desk: hold, cancel and fulfill requisitions, maintain vacancies, adverts and postings, register candidates, progress applications through the pipeline, extend and issue offers, record hires and start dates, run pre-employment checks, and maintain the question bank, presets, pipeline definitions and check templates. Raising a requisition and approving one are NOT this permission — any manager raises their own, and approval belongs to the workflow assignee.",
            CategoryRecruitment),
        new(AdministerRecruitment, "Administer Recruitment",
            "Delete recruitment records — requisitions, vacancies, candidates and their documents, applications and test results, offers and their benefits, question-bank entries, pipeline stages and check templates — and reconcile the establishment vacancy register.",
            CategoryRecruitment),

        new(ViewMedicalRecords, "View Medical Records",
            "View employee health profiles, conditions, allergies, exams, claims, and medical documents.",
            CategoryMedical),
        new(MaintainMedicalRecords, "Maintain Medical Records",
            "Create and update employee health records, exams, and medical claims.",
            CategoryMedical),
        new(AdministerMedical, "Administer Medical Records",
            "Delete medical records and administer occupational-health configuration.",
            CategoryMedical),

        new(ViewTravel, "View Staff Travel",
            "View travel requests, itineraries, bookings, advances, expense claims, travel documents and policies.",
            CategoryTravel),
        new(MaintainTravel, "Maintain Staff Travel",
            "Raise and amend travel requests on behalf of staff, make bookings, and process advances and expense claims.",
            CategoryTravel),
        new(AdministerTravel, "Administer Staff Travel",
            "Delete travel records; approve and withdraw travel policies; authorise or refuse a booking above the policy; decide policy exceptions; approve trip budgets; write off advances and void claim payments; run the travel reminders. Each act is refused to the person it concerns — a policy's author, a booking's booker, a budget's setter, a payment's payer.",
            CategoryTravel),

        new(ViewSuccession, "View Succession & Talent",
            "View succession plans, candidate readiness and retention risk, talent pools, talent reviews and nine-box placements.",
            CategorySuccession),
        new(MaintainSuccession, "Maintain Succession & Talent",
            "Author succession plans, nominate and assess candidates, record development activities, and run talent reviews.",
            CategorySuccession),
        new(AdministerSuccession, "Administer Succession & Talent",
            "Approve succession plans, finalize calibration, read confidential succession documents, delete records, and administer talent pool types.",
            CategorySuccession),

        new(ViewProbation, "View Probation & Confirmation",
            "View probation periods, review schedules and ratings, extension history and confirmation outcomes.",
            CategoryProbation),
        new(MaintainProbation, "Maintain Probation & Confirmation",
            "Open probation periods, schedule and record probation reviews, and issue confirmation letters.",
            CategoryProbation),
        new(AdministerProbation, "Administer Probation & Confirmation",
            "Decide the probation outcome — confirm, extend or terminate — and delete probation records.",
            CategoryProbation),

        new(ViewJobArchitecture, "View Job Architecture",
            "View job descriptions and their responsibilities, qualifications, competencies, working conditions and valuation, plus job families, sub-families and levels.",
            CategoryJobArchitecture),
        new(MaintainJobArchitecture, "Maintain Job Architecture",
            "Author job descriptions and their content, raise new versions, submit and review them, and maintain the job family, sub-family and level taxonomy.",
            CategoryJobArchitecture),
        new(AdministerJobArchitecture, "Administer Job Architecture",
            "Approve job descriptions against positions (FR-HR-134), set the job valuation and suggested salary grade, and delete job descriptions.",
            CategoryJobArchitecture),

        new(ViewCompetency, "View Competency",
            "View the competency framework and skill indicators, position competency requirements, employee competency profiles and gap analysis.",
            CategoryCompetency),
        new(MaintainCompetency, "Maintain Competency",
            "Define competencies and their skill indicators, set position competency requirements, and record employee competency assessments.",
            CategoryCompetency),
        new(AdministerCompetency, "Administer Competency",
            "Delete competencies, position requirements and employee assessments, and administer the framework taxonomy.",
            CategoryCompetency),

        new(ViewManpowerBudget, "View Manpower Budget & Establishment",
            "View manpower budgets, budget lines, critical positions, budget variance and the approved establishment.",
            CategoryManpowerBudget),
        new(MaintainManpowerBudget, "Maintain Manpower Budget & Establishment",
            "Draft manpower budgets and their lines and submit them for approval.",
            CategoryManpowerBudget),
        new(AdministerManpowerBudget, "Administer Manpower Budget & Establishment",
            "Approve or reject a manpower budget — which sets the approved establishment that gates vacancy approval (FR-HR-136) — and delete budgets.",
            CategoryManpowerBudget),

        new(ViewSeparation, "View Separation, Clearance & Exit",
            "View the exit register, separation records of every type, clearance progress, exit interviews and final settlement statements.",
            CategorySeparation),
        new(MaintainSeparation, "Maintain Separation, Clearance & Exit",
            "Raise separations of any type, record resignations and notice, run the clearance checklist, conduct exit interviews and prepare final settlement statements.",
            CategorySeparation),
        new(AdministerSeparation, "Administer Separation, Clearance & Exit",
            "Delete separation records and administer the clearance-item catalogue and separation authorities. Signing a termination (FR-HR-092) and reviewing a settlement (FR-HR-185) are NOT this permission — those are read off the record, see the remarks on RoleGrants.",
            CategorySeparation),

        new(ValueHrPay, "Value HR pay (Finance)",
            "Put the amounts on the pay HR records in days — a leaver's settlement pay lines (unpaid salary, notice pay, leave owed, gratuity, pension, tax) and leave cashed in while employed — and mark cashed-in leave as paid. Finance's step: HR records the days and cannot price them.",
            CategoryPayValuation),

        new(ViewAwards, "View Staff Awards & Recognition",
            "View the award catalogue and its levels, budgets and eligibility rules, the nomination register, award committees and their scoring, conferred awards and long-service milestones.",
            CategoryAwards),
        new(MaintainAwards, "Maintain Staff Awards & Recognition",
            "Raise and submit nominations, score nominations as a committee member, confer awards, schedule presentations, record award payments and process long-service milestones.",
            CategoryAwards),
        new(AdministerAwards, "Administer Staff Awards & Recognition",
            "Administer the award-type catalogue, levels, eligibility targets, budgets and committees, and delete award records. Nominating and voting are NOT this permission — every employee may do both from their own self-service surface.",
            CategoryAwards),

        new(ViewEmployees, "View Employee Records",
            "View any employee's full record — personal details, home address, tax and social-security numbers, salary, contacts, dependents, qualifications, skills, identification, work and position history, contracts, referees, guarantors and bank details. The lean directory reads (the shared name picker, org lookups) stay open to internal staff and do not need this.",
            CategoryEmployee),
        new(MaintainEmployees, "Maintain Employee Records",
            "Create and amend employee records and every sub-record, verify qualifications, skills, identification, guarantors and bank details, and maintain the foundation registers — organization and location structures, positions, teams, unions, staff levels, banks, and the skill, qualification, identification-type, reason-code and country catalogues.",
            CategoryEmployee),
        new(AdministerEmployees, "Administer Employee Records",
            "Delete employees and their sub-records, decide the legacy lifecycle acts (activate, deactivate, terminate, reinstate — the separation module is the governed exit path), and delete foundation reference data — organization and location nodes, positions, teams, unions and lookup entries.",
            CategoryEmployee),

        new(ViewOrientation, "View Orientation & Onboarding",
            "View the orientation surface: programs and their content, categories, session schedules and attendance, employee orientation records and progress, notifications and the dashboard. A new joiner does not need this for their own orientation — self access is an ownership check on the endpoint.",
            CategoryOrientation),
        new(MaintainOrientation, "Maintain Orientation & Onboarding",
            "Run the orientation desk: maintain programs, categories and sessions, enroll and progress employees through orientation, record attendance and completion, and send notifications.",
            CategoryOrientation),
        new(AdministerOrientation, "Administer Orientation & Onboarding",
            "Delete orientation records, programs, categories and sessions.",
            CategoryOrientation),

        new(ViewAssets, "View Staff Assets",
            "View the staff asset surface: the asset register and assignments, requisitions, returns, surcharges, maintenance and disposal records, asset types and the reminders queue. Employees do not need this for their own assets — the employee portal is token-scoped.",
            CategoryAssets),
        new(MaintainAssets, "Maintain Staff Assets",
            "Run the asset desk: register and assign assets, process requisitions, returns, transfers, maintenance, surcharges and disposals, and maintain asset types and configuration.",
            CategoryAssets),
        new(AdministerAssets, "Administer Staff Assets",
            "Delete asset records, assignments, requisitions and configuration.",
            CategoryAssets),

        new(ViewMovements, "View Staff Movements",
            "View the staff movement surface: movements of every type, promotions, transfers, secondments, acting appointments, demotions, career paths and the reminders queue. An employee does not need this for their own movements — the employee portal is token-scoped.",
            CategoryMovements),
        new(MaintainMovements, "Maintain Staff Movements",
            "Run the movements desk: raise and progress movements of every type, implement approved movements, maintain career paths, and process promotions, transfers, secondments, acting appointments and demotions.",
            CategoryMovements),
        new(AdministerMovements, "Administer Staff Movements",
            "Delete movement records and career paths. Approving a movement is NOT this permission — approval belongs to the workflow assignee, validated per movement by the workflow engine.",
            CategoryMovements),

        new(ViewDiscipline, "View Discipline & Grievance",
            "View the discipline surface: cases and their charges, hearings, evidence and outcomes, appeals, suspensions, the grievance register, the offense and action-type catalogues and the reminders queue. An accused or aggrieved employee does not need this for their own case — self access is an ownership check on the endpoint.",
            CategoryDiscipline),
        new(MaintainDiscipline, "Maintain Discipline & Grievance",
            "Run the discipline desk: open and progress cases, record charges, hearings, evidence and outcomes, process appeals and suspensions, handle grievances, and maintain the offense and disciplinary-action catalogues.",
            CategoryDiscipline),
        new(AdministerDiscipline, "Administer Discipline & Grievance",
            "Delete discipline and grievance records and catalogue entries. The natural-justice rules (who may decide a case) are enforced on the record by the service, not granted here.",
            CategoryDiscipline),

        new(ViewShe, "View Safety, Health & Environment",
            "View the SHE surface: incident, hazard, stop-work and risk-assessment registers, audits, inspections, committees, safety equipment and signage, PPE, permits to work, contractors, emergency preparedness, environmental compliance and reviews, waste, controlled documents, reference data and the dashboard. Reporting an incident or hazard, raising a stop-work order and reading one's own reports never need this — every internal employee may do those by design.",
            CategoryShe),
        new(MaintainShe, "Maintain Safety, Health & Environment",
            "Run the SHE desk: maintain every SHE register, investigate and close incidents, decide permits to work, resolve and clear stop-work orders, run audits, inspections and environmental reviews (including clearance and commencement approvals), record statutory submissions, and maintain PPE, contractors, emergency plans, waste, controlled documents and reference data.",
            CategoryShe),
        new(AdministerShe, "Administer Safety, Health & Environment",
            "Delete SHE records — incidents and their sub-records, hazards, assessments, audits, permits, contractors, equipment, documents and reference data. Deletion is the only act above the desk: every SHE decision (close, approve, clear) is desk work and stays with Maintain.",
            CategoryShe),

        new(ViewPerformance, "View Performance",
            "View the org-wide performance surface: appraisal and appeal registers, cycle progress and coverage, the HR cycle dashboard, org-wide at-risk goals, development-plan and improvement-plan registers, review-event and check-in registers, rating analytics and goal-library usage. Employees, managers and peers do not need this for their own appraisal work — self access is an ownership check on the endpoint.",
            CategoryPerformance),
        new(MaintainPerformance, "Maintain Performance",
            "Run the performance desk: maintain cycles, templates, criteria, grades, settings and targets, generate appraisals, run HR review and appeals, run calibration sessions, decide outcome recommendations, enforce deadlines, and maintain goals, check-ins, development plans and improvement plans on behalf of staff. Employee, manager and peer self-acts stay ownership checks on the endpoint, not this permission.",
            CategoryPerformance),
        new(AdministerPerformance, "Administer Performance",
            "Delete performance records — appraisals, cycles, templates, grade and KPI definitions, goal-library items, calibration sessions, review events and improvement plans — and reset goal-risk thresholds. Approving a goal is NOT this permission — that belongs to the goal's direct manager, validated per goal by the goal workflow.",
            CategoryPerformance),
        new(ViewPerformanceProposals, "View Pay & Employment Proposals",
            "Read the salary review and employment action proposals raised from appraisals — the figure, the action, the history — and nothing else of performance. Held by the Managing Director, who decides them; the authority to decide is read off the record, not granted here.",
            CategoryPerformance),

        new(ViewCompany, "View Company & Administration",
            "View the company-level administration surface: the company profile with its statutory numbers, the HR policy settings, the external-associate register with its contact details, and the company schedule — events, meeting rooms and bookings, milestones, business closures and fiscal years.",
            CategoryCompany),
        new(MaintainCompany, "Maintain Company & Administration",
            "Maintain the company profile, keep the external-associate register, and run the company schedule — events with their participants, attendance and tasks, meeting rooms and bookings, milestones, business closures, and fiscal years with their periods. Changing the HR policy settings is NOT this permission — those knobs move trust boundaries (FR-HR-092, FR-HR-136) and sit with Administer.",
            CategoryCompany),
        new(AdministerCompany, "Administer Company & Administration",
            "Change the HR policy settings — the procedural-absence threshold (FR-HR-092) and the budget and establishment enforcement modes (FR-HR-136) among them — and delete company-schedule records, meeting rooms, milestones, closures, fiscal years and external associates.",
            CategoryCompany),

        // ── The Approve tier (2026-09-16) ────────────────────────────────────────────────────
        // Every description says the same thing on purpose: this is an INTERIM authority, it only
        // applies where no workflow definition is published, and publishing one supersedes it.
        // Whoever reads these on the roles screen should be able to tell that from the text alone.

        new(ApproveLeave, "Approve Leave (unconfigured)",
            "Approve or reject leave requests, plans and encashments WHERE NO LEAVE APPROVAL WORKFLOW IS PUBLISHED. Interim: once a workflow definition names the approver, that definition decides and this permission does nothing. It does not let you approve your own request.",
            CategoryLeave),
        new(ApproveAttendance, "Approve Attendance & Time (unconfigured)",
            "Approve or reject attendance regularisations, remote-work requests, overtime and consultant timesheets WHERE NO APPROVAL WORKFLOW IS PUBLISHED for them. Interim, superseded by a published definition. Consultant timesheets sit here because ConsultantTimesheetsController gates every other action on them against the attendance policies.",
            CategoryAttendance),
        new(ApproveCompensation, "Approve Compensation changes (unconfigured)",
            "Approve or reject salary change requests and salary review proposals WHERE NO APPROVAL WORKFLOW IS PUBLISHED. Interim, superseded by a published definition. ⚠ This authorises a change to someone's pay — grant it deliberately.",
            CategoryCompensation),
        new(ApproveTraining, "Approve Training nominations (unconfigured)",
            "Approve or reject training nominations WHERE NO APPROVAL WORKFLOW IS PUBLISHED. Interim, superseded by a published definition.",
            CategoryTraining),
        new(ApproveRecruitment, "Approve Recruitment records (unconfigured)",
            "Approve or reject staff requisitions and job offers WHERE NO APPROVAL WORKFLOW IS PUBLISHED. Interim, superseded by a published definition. ⚠ Approving an offer authorises binding terms to someone outside the organisation. It does not let you approve a requisition you raised or an offer you prepared.",
            CategoryRecruitment),
        new(ApproveTravel, "Approve Staff Travel (unconfigured)",
            "Approve or reject travel requests WHERE NO APPROVAL WORKFLOW IS PUBLISHED. Interim, superseded by a published definition.",
            CategoryTravel),
        new(ApprovePerformance, "Approve Performance records (unconfigured)",
            "Approve or reject appraisal templates, improvement plans and employment-action proposals WHERE NO APPROVAL WORKFLOW IS PUBLISHED. Interim, superseded by a published definition. ⚠ An improvement plan becomes binding on the employee when approved.",
            CategoryPerformance),
        new(ApproveAssets, "Approve Asset requests (unconfigured)",
            "Approve or reject asset requisitions, transfers and surcharges WHERE NO APPROVAL WORKFLOW IS PUBLISHED. Interim, superseded by a published definition.",
            CategoryAssets),
        new(ApproveMovements, "Approve Staff Movements (unconfigured)",
            "Approve or reject promotions and transfers WHERE NO APPROVAL WORKFLOW IS PUBLISHED. Interim, superseded by a published definition. It does not let you approve a movement you requested.",
            CategoryMovements),
        new(ApproveDiscipline, "Approve Disciplinary actions (unconfigured)",
            "Approve or reject disciplinary actions WHERE NO APPROVAL WORKFLOW IS PUBLISHED. Interim, superseded by a published definition. ⚠ The natural-justice rules — who may decide a case, and that the employee was queried and heard — are enforced on the record by the service and are NOT granted here.",
            CategoryDiscipline),
        new(ApproveCompany, "Approve Team records and company events (unconfigured)",
            "Approve or reject team objectives, terms of reference and company events that need approval WHERE NO APPROVAL WORKFLOW IS PUBLISHED. Interim, superseded by a published definition. It does not let you approve an event you organise.",
            CategoryCompany),

        // ── Defined, but deliberately NOT granted to the HR desk ─────────────────────────────
        // Each carries a requirement-driven objection to HR approving. See HrStaffGrants for the
        // reasoning; the grant is where the decision lives, so that is where to reverse it.

        // ⚠ No separation entry. See the note where these constants are declared: FR-HR-092 lives
        // on the record in SeparationService.RequireDecisionAuthority, and a permission gate there
        // would refuse the Managing Director.
        new(ApproveProbation, "Approve Probation outcomes (unconfigured)",
            "Approve or reject a probation outcome WHERE NO APPROVAL WORKFLOW IS PUBLISHED. ⚠ NOT granted to the HR desk: confirming, extending or terminating probation decides whether someone's employment becomes permanent. FR-HR-032 names the chain as system → head confirms → HR issues the letter.",
            CategoryProbation),
        new(ApproveSuccession, "Approve Succession plans (unconfigured)",
            "Approve a succession plan or finalise calibration WHERE NO APPROVAL WORKFLOW IS PUBLISHED. ⚠ NOT granted to the HR desk: approving names a person as the intended successor to a post, and a finalised nine-box placement feeds promotion and movement decisions. Management acts, not record-keeping.",
            CategorySuccession),
        new(ApproveJobArchitecture, "Approve Job descriptions (unconfigured)",
            "Approve or reject a job description WHERE NO APPROVAL WORKFLOW IS PUBLISHED. ⚠ NOT granted to the HR desk: FR-HR-134 — an approved job description is what a position is measured against, and it carries the job valuation and the suggested salary grade.",
            CategoryJobArchitecture),
        new(ApproveManpowerBudget, "Approve Manpower budgets (unconfigured)",
            "Approve or reject a manpower budget WHERE NO APPROVAL WORKFLOW IS PUBLISHED. ⚠ NOT granted to the HR desk: approving one sets the approved establishment, which gates whether a vacancy may be approved at all under FR-HR-136.",
            CategoryManpowerBudget)
    };

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();

    /// <summary>
    /// What HR staff hold: they maintain records but do not administer them, so deleting a
    /// medical record stays with tenant administrators. Travel is the exception since its final closure's
    /// lane 4 (D-3): HR administers it, narrowed per act in the travel services.
    /// </summary>
    /// <remarks>
    /// <para>Travel followed medical until the travel final closure's lane 4 (decision D-3, 2026-10-02). HR raises
    /// travel on behalf of staff and processes advances and claims (Write); deleting travel records and setting the
    /// policies, per-diem rates and approval templates that govern their own spending authority was held back as
    /// administration (Admin) — and no admin login had the employee link that Admin's acts require, so in practice
    /// nobody could approve a policy, authorise a breach, write off an advance or open the reminders screen
    /// (T-1, T-2, T-52). <b>HR now holds <c>AdministerTravel</c></b>, and the narrowing moved into the travel
    /// services, where it can be stated per act: a policy's author does not approve it; nobody decides, pays out,
    /// writes off or voids on their own claim or advance, and the payer does not void the payment (D-2, T-39);
    /// a booking's breach is authorised by an administrator who neither booked it nor asked (D-8); the officer
    /// who set a trip's budget does not approve it (D-19); a policy exception is not decided by whoever raised it
    /// (C4). Status guards on the remaining deletes are lane 7's.</para>
    ///
    /// <para>Succession follows the same ladder for a different reason. HR runs succession
    /// planning — authoring plans, nominating candidates, recording development, facilitating
    /// talent reviews — so Read and Write are theirs. But <b>approving</b> a plan names a person as
    /// the intended successor to a post, and finalizing calibration fixes a nine-box placement that
    /// then feeds promotion and movement decisions. Those are management acts, not record-keeping,
    /// so they sit with Admin alongside deletion and the confidential document tier.</para>
    ///
    /// <para>Probation splits on the same line, and here the requirement draws it for us. FRD
    /// FR-HR-032 and the notification matrix state the chain as <i>system (month 5) → head
    /// confirms → HR issues the confirmation letter</i>. So opening a probation period, scheduling
    /// reviews, recording their ratings and issuing the letter are HR record-keeping (Write),
    /// while the three <b>outcomes</b> — confirm, extend, terminate — decide whether someone's
    /// employment becomes permanent and are management acts (Admin), alongside deletion.</para>
    ///
    /// <para>⚠ Admin is the <i>interim</i> home for those three. Slice 8 of
    /// <c>plans/HR-Area-15b-Probation-Confirmation-Build-Plan.md</c> moves the real check onto the
    /// workflow engine, where the approver is the confirming authority named for the employee's
    /// organisation unit (decision D-2, taken 2026-08-18 because 0 of 41 units carry a
    /// <c>HeadEmployeeId</c> and only 7% of employees a <c>ManagerId</c>). When that lands, these
    /// three may relax to Write with the instance-level authority check doing the real work — do
    /// not relax them before it, or the outcome becomes reachable by anyone HR-shaped.</para>
    ///
    /// <para>Area 17/18 adds three families at once, because one area spans three audiences.
    /// <b>Job architecture</b> follows succession's line: HR authors job descriptions, adds their
    /// responsibilities, qualifications and competencies, raises versions and submits them for
    /// review (Write), but <b>approving</b> one is the act FR-HR-134 actually asks for — an
    /// approved job description is what a position is measured against, and it carries the job
    /// valuation and the suggested salary grade — so approval and deletion are Admin.
    /// <b>Competency</b> keeps the framework, the position requirements and the assessments in
    /// Write, because assessing is record-keeping, with deletion at Admin.
    /// <b>Manpower budget</b> draws the sharpest line: drafting a budget and its lines is Write,
    /// but <b>approving</b> one sets the approved establishment (decision D-2), and the
    /// establishment is what gates whether a vacancy may be approved at all under FR-HR-136. An
    /// approver there is authorising headcount, not filing a record, so approve, reject and
    /// delete are Admin.</para>
    ///
    /// <para>⚠ Admin is again the <i>interim</i> home for the two approvals. Slices 3 and 7 of
    /// <c>plans/HR-Area-17-Job-Architecture-Competency-Establishment-Build-Plan.md</c> move both
    /// onto the workflow engine — the manpower budget onto FR-HR-135's named chain, Department
    /// Head → HR → Managing Director. As with probation, they may relax to Write once the
    /// instance-level check does the real work, and not before.</para>
    ///
    /// <para><b>Recruitment is the exception to the Admin line above</b>, added 2026-09-15 to close
    /// G-3.1 and G-4.8 of <c>docs/HR/areas/recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md</c>. Everywhere else Admin is
    /// the home for a management <i>decision</i>; in recruitment it had drifted onto ordinary
    /// record-keeping, and the result was that the HR function could not run its own module.
    /// <c>POST /position-vacancies/reconcile</c> is the <b>only</b> writer of the vacancy register
    /// anywhere in the solution, and it sits on Admin — so an HR user saw the Reconcile button,
    /// pressed it and got a 403, and the establishment register, the "seats standing empty" tile
    /// and every downstream count stayed empty for ever. The four deletes behind the same policy
    /// (requisition, cost, comment, attachment) are equally routine: removing a draft raised in
    /// error, or an attachment added to the wrong row. None of the 36 endpoints on
    /// <c>RecruitmentAdminPolicy</c> decides anything about a person the way confirming a
    /// probation or approving a succession plan does — the decisions in this module (approve a
    /// requisition, approve an offer) are gated by the workflow adapters instead, and as of the
    /// same date they no longer auto-approve. So <c>AdministerRecruitment</c> joins the HR desk's
    /// grants.</para>
    ///
    /// <para>⚠ Note what is <b>absent</b>: no grant reaches the <c>Employee</c> role. Succession
    /// deliberately inverts the self-service rule the rest of HR follows. A candidate's readiness
    /// level, retention-risk flag and nine-box placement are assessments made about them, not
    /// records belonging to them, so there is no self tier here at all — see decision D-2 in
    /// <c>plans/HR-Area-13-Succession-Build-Plan.md</c>. Any future "my development plan" screen
    /// must be fed by a separate, deliberately narrowed projection, never by relaxing this map.</para>
    ///
    /// <para>Probation does not contradict that. It needs one employee-facing action — signing
    /// "I have seen this review" (<c>ProbationReview.EmployeeAcknowledged</c>) — and that arrives
    /// as a self-or-HR check on the ownership helper, the way discipline's acknowledgement does,
    /// <b>not</b> as a grant here. The rule holds: this map never reaches <c>Employee</c>.</para>
    /// </remarks>
    private static readonly string[] HrStaffGrants =
    {
        ViewLeave, MaintainLeave,
        ViewAttendance, MaintainAttendance,
        ViewCompensation, MaintainCompensation,
        ViewTraining, MaintainTraining,
        ViewRecruitment, MaintainRecruitment, AdministerRecruitment,
        ViewMedicalRecords, MaintainMedicalRecords,
        // AdministerTravel: travel final closure lane 4, D-3 — see the remarks above for where the narrowing lives now.
        ViewTravel, MaintainTravel, AdministerTravel,
        ViewSuccession, MaintainSuccession,
        ViewProbation, MaintainProbation,
        ViewJobArchitecture, MaintainJobArchitecture,
        ViewCompetency, MaintainCompetency,
        ViewManpowerBudget, MaintainManpowerBudget,
        ViewSeparation, MaintainSeparation,
        ViewAwards, MaintainAwards,
        ViewPerformance, MaintainPerformance,
        ViewEmployees, MaintainEmployees,
        // SHE: READ ONLY since 2026-09-03 (DR-10). The safety function has its own roles below;
        // HR sees the registers but no longer investigates, closes, decides or edits in them.
        // RoleRevocations removes the Write grant from tenants seeded before this change.
        ViewShe,
        ViewOrientation, MaintainOrientation,
        ViewAssets, MaintainAssets,
        ViewMovements, MaintainMovements,
        ViewDiscipline, MaintainDiscipline,
        ViewCompany, MaintainCompany,

        // ── The Approve tier (2026-09-16) ────────────────────────────────────────────────────
        // Interim authority to rule on a record whose entity type has NO published workflow
        // definition. See the block where these are declared for why this is a new tier rather
        // than the Admin one. The HR desk gets eleven of the sixteen.
        //
        // ⚠ These are the reason a submitted record does not sit unapprovable. Before them,
        // Submit approved the record outright (no approver at all); the fix stops that, and
        // without somewhere for the authority to come from it would simply strand every record
        // instead — which is the worse of the two failures.
        ApproveLeave,
        ApproveAttendance,
        ApproveCompensation,
        ApproveTraining,
        ApproveRecruitment,
        ApproveTravel,
        ApprovePerformance,
        ApproveAssets,
        ApproveMovements,
        ApproveDiscipline,
        ApproveCompany,

        // ⚠ FOUR ARE ABSENT AND THAT IS THE POINT. ApproveProbation, ApproveSuccession,
        // ApproveJobArchitecture and ApproveManpowerBudget are deliberately NOT here, each for a
        // reason this map already argues elsewhere. (Separation has no Approve permission at all —
        // its authority is FR-HR-092 on the record; see the declaration block.)
        //
        //   • Probation       — confirm / extend / terminate decide whether employment becomes
        //                       permanent. The remarks above warn against making the outcome
        //                       "reachable by anyone HR-shaped".
        //   • Succession      — approving names the intended successor to a post; calibration
        //                       fixes a nine-box placement that feeds promotion decisions.
        //   • JobArchitecture — FR-HR-134: an approved JD carries the job valuation and the
        //                       suggested salary grade.
        //   • ManpowerBudget  — approving sets the approved establishment, which gates whether a
        //                       vacancy may be approved at all under FR-HR-136.
        //
        // The effect: those five stop auto-approving like everything else, but their approval
        // stalls until SuperAdmin/TenantAdmin/Admin acts or — the real answer — a workflow
        // definition names the authority. A stalled approval is the documented intent.
        //
        // If a tenant needs HR to approve one of these, publish a definition naming the right
        // approver. Adding the grant here is the wrong lever and undoes a requirement.
    };

    /// <summary>
    /// The Safety, Health &amp; Environment desk (DR-10). Every SHE register at Read + Write, plus
    /// the occupational-health pair because the surveillance, first-aid, wellness and
    /// return-to-work registers SHE owns are gated on <c>HR.Medical.*</c> (area-11 boundary
    /// decision, 2026-08-14). No employee-master PII, no other HR family: the lean directory and
    /// the employee picker are open reads, which is all the SHE screens need.
    /// </summary>
    private static readonly string[] SafetyOfficerGrants =
    {
        ViewShe, MaintainShe,
        ViewMedicalRecords, MaintainMedicalRecords
    };

    /// <summary>Everything the officer holds, plus deletion — the one act above the SHE desk.</summary>
    private static readonly string[] SheManagerGrants =
    {
        ViewShe, MaintainShe, AdministerShe,
        ViewMedicalRecords, MaintainMedicalRecords
    };

    /// <summary>
    /// Grants a role must NOT hold, applied by the seeder AFTER <see cref="RoleGrants"/>. The
    /// grant loop is add-only (it never removes a row it did not just add), so shrinking a role in
    /// <see cref="RoleGrants"/> alone changes nothing on a tenant seeded before the shrink. List
    /// the revocation here and the seeder deletes the row on every startup. The fallback handler
    /// reads <see cref="RoleGrants"/>, which no longer contains the grant, so both sides agree.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> RoleRevocations =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // 2026-09-03: HR drops SHE Write; the safety function is its own desk (DR-10).
            [Constants.Roles.Hr] = new[] { MaintainShe },
            [Constants.Roles.LegacyHrUser] = new[] { MaintainShe }
        };

    /// <summary>
    /// What the two approval authorities hold: the ability to <b>see</b> a separation, and nothing
    /// else. The Managing Director signs one (FR-HR-092) and Internal Audit reviews its settlement
    /// (FR-HR-185); neither raises, edits or administers separations, and their authority to decide
    /// is read off the record rather than granted here.
    /// </summary>
    private static readonly string[] ApprovalReaderGrants =
    {
        ViewSeparation
    };

    /// <summary>
    /// The Managing Director's: a separation to sign, and the pay and employment proposals to decide (F3, D-94) —
    /// read only, as <see cref="ApprovalReaderGrants"/>, which Internal Audit shares and which stays as it is.
    /// </summary>
    private static readonly string[] ManagingDirectorGrants =
    {
        ViewSeparation, ViewPerformanceProposals
    };

    /// <summary>
    /// What Finance holds in HR (leave settings audit 2, decision P2): the valuation step, and
    /// nothing else. Finance works from its own queue of what awaits a figure, so it needs no read
    /// of HR's records at large.
    /// </summary>
    /// <remarks>
    /// Granted by default to the three roles below, which TDC is asked to confirm or replace
    /// (HR-OPEN-QUESTIONS-FOR-TDC.md, "Who in Finance values a leaver's final pay").
    /// </remarks>
    private static readonly string[] FinancePayValuerGrants =
    {
        ValueHrPay
    };

    /// <summary>The Finance roles that value HR's pay by default. Bare literals, as the seeder names them.</summary>
    public static readonly string[] FinancePayValuerRoles = { "Finance Officer", "Senior Accountant", "Chief Accountant" };

    /// <summary>
    /// Per-role HR permission grants. This is the single source for both the database seed
    /// (<c>DatabaseSeedingService</c>) and the role fallback
    /// (<c>HrPermissionRoleFallbackAuthorizationHandler</c>).
    /// </summary>
    /// <remarks>
    /// <para><b>The fallback exists to stand in for the seed, so it must grant exactly what the
    /// seed grants.</b> Both read this map for that reason. The previous implementation matched
    /// on the <c>HR.</c> prefix alone and never inspected the verb, so an HR-role user — seeded
    /// with Read and Write only — satisfied <c>MedicalAdminPolicy</c> as well and could delete
    /// medical records, including paid expense claims. Verb-blind fallback is a privilege
    /// escalation; keep the two sides reading one map so they cannot drift apart again.</para>
    ///
    /// <para>Permissions resolve from the database, so without a fallback every HR endpoint would
    /// 403 for everyone but SuperAdmin on a tenant provisioned before the seeder ran.</para>
    ///
    /// <para>"HR User" was the seeded name before the rename to "HR"
    /// (<c>DatabaseSeedingService.MigrateLegacyHrRoleNameAsync</c>). It is listed so a tenant that
    /// has not yet run the renaming startup keeps access; the seeder does not seed it, which is
    /// precisely what the fallback is for. Drop it once every environment is known to be migrated.</para>
    ///
    /// <para>⚠ "Admin" is a bare literal with no <c>Constants.Roles</c> member, and no such role
    /// exists in the reference database (checked 2026-08-17). It is retained because removing it
    /// would silently revoke medical access in any environment that does have one. Confirm whether
    /// any environment uses it, then either promote it to a constant or delete it.</para>
    ///
    /// <para>Separation (area 9b) follows the same ladder, and deliberately stops short of two
    /// things this map cannot express. FR-HR-092 puts the <b>MD's signature</b> on every
    /// non-procedural termination and FR-HR-185 puts <b>Internal Audit's review</b> before the
    /// settlement is paid. Neither is an HR permission: the entitled party is named on the record
    /// and anchored on the <c>Managing Director</c> / <c>TDC_MANAGING_DIRECTOR</c> and
    /// <c>TDC_INTERNAL_AUDIT</c> roles, so granting <c>HR.Separation.Admin</c> must never confer
    /// them. Gating those two actions on a permission family HR holds would let HR sign off its
    /// own terminations and release its own payments — and gating them on a permission nobody
    /// holds would make them reachable by nobody, the area-15b mistake.</para>
    ///
    /// <para>When extending this to other HR areas (the W3 permission sweep), add the area's
    /// permissions here per role rather than widening the match — the whole point of this map is
    /// that a role's grants are stated, not inferred from a name.</para>
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string[]> RoleGrants =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Constants.Roles.SuperAdmin] = AllNames,
            [Constants.Roles.TenantAdmin] = AllNames,
            ["Admin"] = AllNames,
            [Constants.Roles.Hr] = HrStaffGrants,
            [Constants.Roles.LegacyHrUser] = HrStaffGrants,

            // DR-10 (2026-09-03): the safety function's own roles. Neither holds the HR role, and
            // the HR role no longer holds SHE Write — see RoleRevocations.
            [Constants.Roles.SafetyOfficer] = SafetyOfficerGrants,
            [Constants.Roles.SheManager] = SheManagerGrants,

            // The Managing Director signs separations (FR-HR-092) and Internal Audit reviews their
            // settlements (FR-HR-185). Both need to READ the record they are deciding on — and
            // nothing more. Without this the approve endpoint would admit the MD while every read
            // endpoint refused them, which is a signature on something they cannot see.
            //
            // Deliberately Read only: the authority to decide is not a permission at all, it is
            // read off the record by SeparationService. Granting Write here would let the MD edit
            // what they are about to sign.
            //
            // The MD also decides the pay and employment proposals an appraisal raises (F3, D-12), so reads those too.
            [Constants.Roles.ManagingDirector] = ManagingDirectorGrants,
            [Constants.Roles.TdcManagingDirector] = ManagingDirectorGrants,
            [Constants.Roles.InternalAudit] = ApprovalReaderGrants,

            // Finance values the pay HR records in days (leave settings audit 2, P2).
            [FinancePayValuerRoles[0]] = FinancePayValuerGrants,
            [FinancePayValuerRoles[1]] = FinancePayValuerGrants,
            [FinancePayValuerRoles[2]] = FinancePayValuerGrants
        };

    /// <summary>
    /// The HR permissions granted to <paramref name="roleName"/>, or an empty array when the role
    /// receives none. Callers can concatenate this with their own module's grants.
    /// </summary>
    public static string[] GrantsFor(string roleName)
        => RoleGrants.TryGetValue(roleName, out var permissions)
            ? permissions
            : Array.Empty<string>();

    /// <summary>
    /// True when any of <paramref name="roleNames"/> is granted any of
    /// <paramref name="permissions"/> by <see cref="RoleGrants"/>.
    /// </summary>
    /// <remarks>
    /// <para>For <b>service-layer</b> checks that need to ask "does this caller hold a permission"
    /// where only the role list is to hand. Added 2026-09-15 for G-9.1: the interview service
    /// asked <c>HasRole("HR") || HasRole("SuperAdmin")</c> while every other recruitment surface
    /// authorised on <c>HR.Recruitment.*</c>, so <c>TenantAdmin</c>, <c>Admin</c> and the legacy
    /// <c>HR User</c> role could raise a requisition and reject a candidate but could not schedule
    /// an interview — or read one, unless they happened to sit on the panel.</para>
    ///
    /// <para>⚠ This resolves from the role map, <b>not</b> from the database, so a permission
    /// granted directly to a custom role is not seen here. It is a second layer behind the
    /// controller's <c>[Authorize(Policy = …)]</c>, which does run the full database check — the
    /// same relationship <see cref="RoleGrants"/> already has with
    /// <c>HrPermissionRoleFallbackAuthorizationHandler</c>. Do not use it as a sole gate on an
    /// endpoint.</para>
    /// </remarks>
    public static bool RolesGrantAny(IEnumerable<string>? roleNames, params string[] permissions)
    {
        if (roleNames is null || permissions.Length == 0) return false;

        foreach (var roleName in roleNames)
        {
            if (string.IsNullOrWhiteSpace(roleName)) continue;

            var granted = GrantsFor(roleName);
            if (granted.Length == 0) continue;

            foreach (var permission in permissions)
            {
                if (granted.Contains(permission, StringComparer.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }
}
