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
    public const string CategoryMedical = "HR - Occupational Health";
    public const string CategoryTravel = "HR - Staff Travel";
    public const string CategorySuccession = "HR - Succession & Talent";
    public const string CategoryProbation = "HR - Probation & Confirmation";
    public const string CategoryJobArchitecture = "HR - Job Architecture";
    public const string CategoryCompetency = "HR - Competency";
    public const string CategoryManpowerBudget = "HR - Manpower Budget & Establishment";

    /// <summary>Prefix identifying HR permissions, used by the role-fallback handler.</summary>
    public const string Prefix = "HR.";

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

    public static readonly HrPermissionDefinition[] All =
    {
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
            "Delete travel records and administer travel policies, per-diem rates, vendors and approval templates.",
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
            CategoryManpowerBudget)
    };

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();

    /// <summary>
    /// What HR staff hold: they maintain records but do not administer them, so deleting a
    /// medical record or a paid travel claim stays with tenant administrators.
    /// </summary>
    /// <remarks>
    /// <para>Travel follows medical deliberately. HR raises travel on behalf of staff and processes
    /// advances and claims (Write), but deleting travel records and setting the policies,
    /// per-diem rates and approval templates that govern their own spending authority is
    /// administration (Admin) — the same separation that stopped an HR-role user deleting a paid
    /// medical claim.</para>
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
        ViewMedicalRecords, MaintainMedicalRecords,
        ViewTravel, MaintainTravel,
        ViewSuccession, MaintainSuccession,
        ViewProbation, MaintainProbation,
        ViewJobArchitecture, MaintainJobArchitecture,
        ViewCompetency, MaintainCompetency,
        ViewManpowerBudget, MaintainManpowerBudget
    };

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
            [Constants.Roles.LegacyHrUser] = HrStaffGrants
        };

    /// <summary>
    /// The HR permissions granted to <paramref name="roleName"/>, or an empty array when the role
    /// receives none. Callers can concatenate this with their own module's grants.
    /// </summary>
    public static string[] GrantsFor(string roleName)
        => RoleGrants.TryGetValue(roleName, out var permissions)
            ? permissions
            : Array.Empty<string>();
}
