using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Staffs the TDC establishment with a demo workforce, for the <b>DEFAULT tenant</b>.
///
/// <para><b>Why this exists.</b> <see cref="TdcOrganogramSeeder"/> creates the establishment — 41
/// organisation units and 137 positions carrying 179 established posts — and deliberately creates no
/// employees, because employee seeding was reserved for the real TDC employee-details file. Until
/// that file arrives the register is empty, and an empty register makes most of the HR module
/// undemonstrable: no org chart, no appraisal, no leave balance, no succession bench, and every
/// downstream seeder that resolves employees by query finds nothing to attach to. This seeder fills
/// that gap with synthetic staff so the module can be shown.
/// </para>
///
/// <para><b>The establishment is the source of truth, not this file.</b> Who exists is derived from
/// <see cref="EmployeePosition"/>: the unit comes from the position, the grade comes from the
/// position, and the reporting line comes from <see cref="EmployeePosition.ReportsToPositionId"/>.
/// Nothing here invents an organisational fact. That is what keeps the seeded org chart identical to
/// the organogram the client supplied, rather than a second structure that merely resembles it.
/// </para>
///
/// <para><b>Deterministic on purpose.</b> Names, dates and salaries come from a fixed-seed generator
/// implemented in this file rather than <see cref="Random"/>, whose sequence is not contractually
/// stable across .NET versions. A rebuild therefore produces the same people with the same staff
/// numbers, which is the only way a written demo script can say "open Akosua Mensah, TDC/00007" and
/// still be correct next week.</para>
///
/// <para><b>Posts are left deliberately vacant.</b> Filling all 179 would leave recruitment with
/// nothing to recruit and the establishment report showing no gap. Roughly one post in six is left
/// open, spread across the structure, and the vacancies are logged at the end so a demo script can
/// name a real one.</para>
///
/// <para><b>SAFE BY CONSTRUCTION.</b> Runs only from an explicit seed command, never on startup.
/// Idempotent: returns immediately if any employee numbered by this register already exists.
/// Creates employees, salary-grade bands, a staff-number register and unit-head assignments; it does
/// not modify the establishment itself.</para>
/// </summary>
public class TdcDemoWorkforceSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoWorkforceSeeder> _logger;

    private const string By = "TdcDemoWorkforceSeeder";

    /// <summary>Prefix of the staff-number register this seeder issues into.</summary>
    private const string StaffNumberPrefix = "TDC";

    /// <summary>
    /// The number-sequence key for staff numbers, matching <see cref="StaffNumberFormat.SequenceKey"/>.
    /// </summary>
    private const string StaffSequenceKey = "EMP";

    /// <summary>
    /// Leave every Nth established post open. 6 yields roughly 29 vacancies against 179 posts —
    /// enough for recruitment, establishment-gap and manpower-budget screens to show real numbers.
    /// </summary>
    private const int VacancyEveryNthPost = 6;

    public TdcDemoWorkforceSeeder(ApplicationDbContext context, ILogger<TdcDemoWorkforceSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed the demo workforce.");
            return;
        }

        var tenantId = tenant.Id;

        // The guard asks for a row THIS seeder creates, not for a non-empty Employees table. The
        // table is NOT empty on a fresh UAT build — the estate module contributes 24 fixtures — so
        // "any employee exists?" would skip this step for ever. That is HrSeedOrchestrator's
        // job-architecture lesson met again: a starter-data seed and a create-the-baseline seed need
        // different questions.
        if (await _context.Employees.IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId
                            && e.EmployeeNumber.StartsWith(StaffNumberPrefix + "/"), ct))
        {
            _logger.LogInformation("Demo workforce already seeded for the DEFAULT tenant. Skipping.");
            return;
        }

        // ⚠ ONLY the TDC establishment, resolved through the organisation STRUCTURE.
        //
        // This database holds two organisation structures. Besides TDC's, the estate module has its
        // own: fourteen positions ("Estate Officer", "Acquisition Committee", "Executive Approver",
        // "Legal Manager") anchored to a catch-all unit called GEN, sitting on a separate level
        // scheme, and already staffed by that module's own 24 fixtures. A first run staffed both,
        // which put 46 invented people into another module's jobs and made HR registers list an
        // "Acquisition Committee" — precisely the kind of row this seeder exists to remove.
        //
        // The filter walks position -> level -> structure and keeps structure "TDC", rather than
        // excluding the estate positions by a code prefix. A prefix is a description of today's
        // data; the structure is the actual boundary, and a third module arriving next month is
        // handled without anyone remembering to update a pattern.
        var levelsInTdc = await _context.Set<OrganizationLevel>()
            .IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted
                     && _context.Set<OrganizationStructure>()
                                .Any(s => s.Id == l.StructureId && s.Code == "TDC"))
            .ToListAsync(ct);

        var levelNumberById = levelsInTdc.ToDictionary(l => l.Id, l => l.LevelNumber);

        var positions = await _context.Set<EmployeePosition>()
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsActive)
            .ToListAsync(ct);

        positions = positions
            .Where(p => levelNumberById.ContainsKey(p.OrganizationLevelId))
            .ToList();

        if (positions.Count == 0)
        {
            _logger.LogError(
                "No TDC positions found — run 'seed-hr-all' first so the TDC establishment exists. " +
                "This seeder staffs an establishment; it does not create one.");
            return;
        }

        var units = await _context.Set<OrganizationUnit>()
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .ToListAsync(ct);

        var grades = await _context.Set<SalaryGrade>()
            .IgnoreQueryFilters()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .ToListAsync(ct);

        // Country.Code is ISO 3166-1 alpha-3, so the country is "GHA" and the alpha-2 "GH" lives on
        // its own column. Matching only "GH" against Code silently finds nothing and leaves every
        // employee without a nationality.
        var ghana = await _context.Set<Country>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId
                                   && (c.Code == "GHA" || c.Alpha2Code == "GH" || c.Name == "Ghana"), ct);

        var headOffice = await _context.Set<Location>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && !l.IsDeleted && l.Code == "TEMA-HQ", ct);

        EnsureGradeBands(grades);
        await EnsureStaffNumberRegisterAsync(tenantId, ct);

        var gradeById = grades.ToDictionary(g => g.Id);
        var unitById = units.ToDictionary(u => u.Id);

        var rng = new DeterministicRng(20260901);
        var created = new List<SeededEmployee>();
        var vacancies = new List<string>();
        // Seeded WITH the addresses already in the register, not empty. (TenantId, EmailAddress) is a
        // unique index and this seeder is not the only writer into it — the estate module contributes
        // 24 employees of its own. A set that only remembers what this run generated would collide
        // with one of theirs, and the whole insert would fail on a name that merely happened to
        // repeat.
        var usedEmails = (await _context.Employees.IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.EmailAddress)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var postCounter = 0;
        var staffNumber = 0;

        // Senior posts first, so a manager's staff number is always lower than the numbers of the
        // people reporting to them. Not cosmetic: it makes the register read top-down on screen
        // without anybody having to sort it, and the org chart opens on the Managing Director rather
        // than on a driver.
        //
        // ⚠ ORGANISATION LEVEL LEADS, GRADE ONLY BREAKS THE TIE. Ordering by grade alone put the
        // Managing Director at TDC/00109, below every Head of Department, because the MD position
        // carries no salary grade at all and ungraded positions sort mid-table by design. Seniority
        // in an organogram is a property of where the box sits, not of whether somebody remembered
        // to attach a pay grade to it.
        var ordered = positions
            .OrderBy(p => levelNumberById.TryGetValue(p.OrganizationLevelId, out var n) ? n : int.MaxValue)
            .ThenBy(p => SeniorityOf(p, gradeById))
            .ThenBy(p => p.Code, StringComparer.Ordinal)
            .ToList();

        foreach (var position in ordered)
        {
            var establishedPosts = Math.Max(1, position.ExpectedHeadcount);
            var grade = position.SalaryGradeId is { } gid && gradeById.TryGetValue(gid, out var g) ? g : null;
            var seniority = SeniorityOf(position, gradeById);
            var levelNumber = levelNumberById.TryGetValue(position.OrganizationLevelId, out var lvl)
                ? lvl
                : int.MaxValue;

            for (var post = 0; post < establishedPosts; post++)
            {
                postCounter++;

                // Never leave the top of the house vacant — an org chart missing its Managing
                // Director or a directorate with no General Manager is the first thing anyone
                // notices. Levels 1-3 are Governance, Executive and Directorate.
                if (levelNumber > 3 && postCounter % VacancyEveryNthPost == 0)
                {
                    vacancies.Add($"{position.Code} — {position.Title}");
                    continue;
                }

                staffNumber++;
                var employee = BuildEmployee(
                    tenantId, position, grade, seniority, unitById,
                    ghana?.Id, headOffice?.Id, staffNumber, rng, usedEmails);

                _context.Employees.Add(employee);
                created.Add(new SeededEmployee(employee, position.Id, position.ReportsToPositionId));
            }
        }

        // ── The off-payroll cohort ──────────────────────────────────────────────────────────────
        // Not every employee is paid through the payroll run, and a workforce where everyone is
        // would leave the "not on payroll" path — the flag, the reason, the hidden salary block,
        // the reconciliation screen — with nothing to show. The four most junior posts are filled
        // by national service personnel on an allowance and one contractor on invoice. Junior on
        // purpose: the runbook names senior people by staff number, and those must stay as they are.
        var offPayroll = MakeOffPayrollCohort(created);

        await _context.SaveChangesAsync(ct);

        // ── Second pass: reporting lines ────────────────────────────────────────────────────────
        // Deferred because a manager must already have a row before anyone can point at it, and the
        // establishment's reporting lines are not ordered: a position may report to one seeded later
        // in the same run.
        var holderByPosition = created
            .GroupBy(c => c.PositionId)
            .ToDictionary(grp => grp.Key, grp => grp.First().Employee);

        var linked = 0;
        foreach (var seeded in created)
        {
            if (seeded.ReportsToPositionId is not { } reportsTo) continue;
            if (!holderByPosition.TryGetValue(reportsTo, out var manager)) continue;
            if (manager.Id == seeded.Employee.Id) continue;

            seeded.Employee.ManagerId = manager.Id;
            linked++;
        }

        // ── Unit heads ──────────────────────────────────────────────────────────────────────────
        // The most senior filled position anchored to a unit becomes that unit's head. Done here
        // rather than left to 'seed-hr-org-authority', which assigns heads from the estate fixtures
        // and so points units at people named "Michael Executive".
        var headsAssigned = 0;
        var headByUnit = new Dictionary<Guid, Employee>();
        foreach (var unit in units)
        {
            var head = created
                .Where(c => c.Employee.OrganizationUnitId == unit.Id)
                .OrderBy(c => c.Employee.EmployeeNumber, StringComparer.Ordinal)
                .Select(c => c.Employee)
                .FirstOrDefault();

            if (head is null) continue;

            unit.HeadEmployeeId = head.Id;
            unit.UpdatedAt = DateTime.UtcNow;
            unit.UpdatedBy = By;
            headsAssigned++;

            headByUnit[unit.Id] = head;
        }

        // ── Third pass: the reporting lines the establishment does not state ────────────────────
        //
        // Sixteen of the positions carry no ReportsToPositionId, so their holders finish the second
        // pass with no manager. That matters beyond a blank field: FR-HR-080's issuing authority,
        // FR-HR-181's grievance ladder and the responder matrix all resolve UP the management line,
        // and an employee without one falls out of every rule that walks it.
        //
        // 'seed-hr-org-authority' exists to fill exactly this gap, and on the first run it filled it
        // from the estate fixtures — it made "Ama Estate" the manager of the Chief Internal Auditor.
        // Deriving the answer from the organisation tree instead keeps it inside the real structure:
        // report to your unit's head, and if you ARE the head, to the head of the parent unit.
        // Only the Managing Director ends with no manager, which is correct.
        var derived = 0;
        foreach (var seeded in created)
        {
            var employee = seeded.Employee;
            if (employee.ManagerId is not null) continue;
            if (employee.OrganizationUnitId is not { } unitId) continue;

            var manager = FindManagerUpTheTree(employee, unitId, unitById, headByUnit);
            if (manager is null) continue;

            employee.ManagerId = manager.Id;
            derived++;
        }

        // ── Advance the staff-number counter ────────────────────────────────────────────────────
        // NOTHING ELSE DOES THIS. NumberSequenceSeeder reconciles REQ, APP, CAND and VAC and has no
        // EMP case, so without this line the register holds TDC/00001..TDC/0015x while the counter
        // still stands at zero — and the first employee HR creates after seeding is issued a number
        // that is already taken. It surfaces as a unique-index violation on the create, which is to
        // say: on stage, in front of the room, on the one screen everybody recognises.
        EnsureSequenceAtLeast(tenantId, StaffSequenceKey, 0, staffNumber);

        await _context.SaveChangesAsync(ct);

        // ── Payroll's side of the membership ────────────────────────────────────────────────────
        // HR's IsOnPayroll says who SHOULD be paid through the run; payroll's own employee profile
        // is what a run actually selects on. Seeding the profile for everyone HR puts on payroll
        // keeps the reconciliation screen honest on demo day (it lists disagreements, and a
        // workforce with a hundred "awaiting payroll setup" rows would be noise, not a finding) and
        // gives the payroll run people to pay. Written directly, as demo data, in the shape payroll's
        // own upsert produces for a new profile: profile + salary basis + one default bank method.
        var profiles = await SeedPayrollProfilesAsync(tenantId, created, ct);

        _logger.LogInformation(
            "Payroll membership: {On} on payroll (with {Profiles} payroll profiles created), "
            + "{Off} not on payroll: {OffList}.",
            created.Count(c => c.Employee.IsOnPayroll), profiles,
            offPayroll.Count,
            string.Join("; ", offPayroll.Select(e =>
                $"{e.EmployeeNumber} {e.FirstName} {e.LastName} ({e.EmploymentType}, {e.OffPayrollReason})")));

        _logger.LogInformation(
            "Demo workforce seeded: {Created} employees, {Heads} unit heads, {Vacant} posts left "
            + "vacant. Reporting lines: {Linked} from the establishment, {Derived} derived from the "
            + "organisation tree, {Unmanaged} left with none. Staff numbers {First}..{Last}; "
            + "'{Key}' counter advanced to {Counter}.",
            created.Count, headsAssigned, vacancies.Count,
            linked, derived, created.Count(c => c.Employee.ManagerId is null),
            $"{StaffNumberPrefix}/00001", $"{StaffNumberPrefix}/{staffNumber:D5}",
            StaffSequenceKey, staffNumber);

        // The cohorts, counted from what was actually written rather than from the intent. Each one
        // switches an area of the module on, so a zero here is a silently disabled demo, not a
        // cosmetic detail — which is exactly how the first run shipped an empty probation register.
        var todayOnly = DateOnly.FromDateTime(DateTime.Today);
        _logger.LogInformation(
            "Cohorts: {Probation} still serving probation, {Retiring} within 3 years of retirement, "
            + "{LongService} with 20+ years' service, {Vacant} vacant posts.",
            created.Count(c => c.Employee.StaffStatus == StaffStatus.Probation),
            created.Count(c => c.Employee.RetirementDate is { } r && r <= todayOnly.AddYears(3)),
            created.Count(c => c.Employee.DateEmployed is { } d && d <= todayOnly.AddYears(-20)),
            vacancies.Count);

        if (vacancies.Count > 0)
        {
            _logger.LogInformation(
                "Vacant established posts a demo can recruit into ({Count}): {Vacancies}",
                vacancies.Count, string.Join("; ", vacancies));
        }
    }

    private sealed record SeededEmployee(Employee Employee, Guid PositionId, Guid? ReportsToPositionId);

    /// <summary>
    /// Turns the four most junior seeded staff into off-payroll people: three national service
    /// personnel on an allowance and one contractor paid by invoice. Their pay figures and switches
    /// are cleared, as the employee service would clear them, so the record is exactly what the
    /// "not on payroll" rule produces rather than a flag stuck on a payroll-shaped row.
    /// </summary>
    private static List<Employee> MakeOffPayrollCohort(List<SeededEmployee> created)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var juniors = created
            .OrderByDescending(c => c.Employee.EmployeeNumber, StringComparer.Ordinal)
            .Take(4)
            .Select(c => c.Employee)
            .ToList();

        for (var i = 0; i < juniors.Count; i++)
        {
            var e = juniors[i];
            var contractor = i == juniors.Count - 1;

            e.IsOnPayroll = false;
            e.Salary = null;
            e.PayTax = false;
            e.SSFund = false;
            e.GrossUp = false;
            e.Tier2Only = false;
            e.Overtime = false;

            if (contractor)
            {
                e.EmploymentType = EmploymentType.Consultant;
                e.IsFullTime = false;
                e.OffPayrollReason = OffPayrollReason.PaidByInvoice;
                e.OffPayrollNote = "Engaged on a 12-month service contract; invoices monthly through Procurement.";
                e.DateEmployed = today.AddMonths(-5);
            }
            else
            {
                e.EmploymentType = EmploymentType.Internship;
                e.OffPayrollReason = OffPayrollReason.Allowance;
                e.OffPayrollNote = "National Service personnel; allowance paid by the National Service Scheme.";
                // The service year starts in September/October; place them inside the current one.
                e.DateEmployed = today.AddMonths(-(2 + i));
            }

            // No probation for either: the engagement is fixed-term by nature.
            e.StaffStatus = StaffStatus.Active;
            e.ConfirmationDate = null;
            e.ProbationPeriodDays = 0;
        }

        return juniors;
    }

    /// <summary>
    /// Payroll's employee profile for every seeded employee HR puts on payroll, in the shape
    /// payroll's own upsert produces for a new profile. Skips anyone payroll already knows.
    /// </summary>
    private async Task<int> SeedPayrollProfilesAsync(Guid tenantId, List<SeededEmployee> created, CancellationToken ct)
    {
        var known = (await _context.Set<PayrollEmployeeProfile>()
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                .Select(p => p.EmployeeId)
                .ToListAsync(ct))
            .ToHashSet();

        var written = 0;
        foreach (var seeded in created)
        {
            var e = seeded.Employee;
            if (!e.IsOnPayroll || known.Contains(e.Id)) continue;

            var profile = new PayrollEmployeeProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = e.Id,
                EmployeeNumber = e.EmployeeNumber,
                PayrollActive = true,
                PayTax = e.PayTax,
                SsfApplicable = e.SSFund,
                GrossUp = e.GrossUp,
                Tier2Only = e.Tier2Only,
                OvertimeEligible = e.Overtime,
                SsfNumber = e.SocialSecurityNumber,
                TinNumber = e.TINNumber,
                CurrencyCode = "GHS",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = By,
            };
            _context.Set<PayrollEmployeeProfile>().Add(profile);

            if (e.Salary is > 0)
            {
                _context.Set<PayrollSalaryBasis>().Add(new PayrollSalaryBasis
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EmployeeProfileId = profile.Id,
                    MonthlyBasicSalary = e.Salary.Value,
                    AnnualBasicSalary = e.Salary.Value * 12,
                    CurrencyCode = "GHS",
                    EffectiveFrom = (e.DateEmployed ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = By,
                });
            }

            _context.Set<PayrollPaymentMethod>().Add(new PayrollPaymentMethod
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeProfileId = profile.Id,
                PaymentType = "Bank",
                PaymentMode = "Percentage",
                PaymentPercent = 100m,
                CurrencyCode = "GHS",
                SequenceNo = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = By,
            });

            written++;
        }

        if (written > 0) await _context.SaveChangesAsync(ct);
        return written;
    }

    /// <summary>
    /// The nearest head above <paramref name="employee"/>: their own unit's head, or — when they are
    /// that head — the head of the nearest ancestor unit that has a different one.
    /// </summary>
    /// <remarks>
    /// The walk is bounded by the number of units rather than by "until the root", because a cycle
    /// in ParentUnitId would otherwise hang the seeder. Organisation trees are hand-maintained and a
    /// cycle is a data error somebody will eventually make; a seeder is a bad place to discover it.
    /// </remarks>
    private static Employee? FindManagerUpTheTree(
        Employee employee,
        Guid startUnitId,
        IReadOnlyDictionary<Guid, OrganizationUnit> unitById,
        IReadOnlyDictionary<Guid, Employee> headByUnit)
    {
        var currentUnitId = (Guid?)startUnitId;

        for (var hops = 0; hops <= unitById.Count && currentUnitId is { } id; hops++)
        {
            if (headByUnit.TryGetValue(id, out var head) && head.Id != employee.Id)
            {
                return head;
            }

            currentUnitId = unitById.TryGetValue(id, out var unit) ? unit.ParentUnitId : null;
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Building one employee
    // ────────────────────────────────────────────────────────────────────────────────────────────

    private Employee BuildEmployee(
        Guid tenantId,
        EmployeePosition position,
        SalaryGrade? grade,
        int seniority,
        IReadOnlyDictionary<Guid, OrganizationUnit> unitById,
        Guid? countryId,
        Guid? locationId,
        int staffNumber,
        DeterministicRng rng,
        HashSet<string> usedEmails)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Roughly 40% female. Names are drawn gender-consistently so the register does not read as
        // machine output the moment somebody scans it.
        var isFemale = rng.Next(100) < 40;
        var firstName = isFemale ? Pick(FemaleFirstNames, rng) : Pick(MaleFirstNames, rng);
        var middleName = rng.Next(100) < 55
            ? (isFemale ? Pick(FemaleFirstNames, rng) : Pick(MaleFirstNames, rng))
            : null;
        var lastName = Pick(Surnames, rng);

        // ── Three deliberate cohorts ────────────────────────────────────────────────────────────
        //
        // A workforce drawn purely from a plausible distribution silently switches whole areas of the
        // module off, and the first version of this seeder did exactly that: nobody had been hired in
        // the last twelve months, so the probation register (twelve slices of built functionality)
        // and the onboarding queues were EMPTY; one person was within three years of retirement, so
        // succession planning had nothing urgent to plan for; and five people had twenty years in, so
        // the long-service award had almost nobody to give.
        //
        // These cohorts are carved by staff number rather than drawn at random, so they are stable
        // across rebuilds and a demo script can rely on them existing. The proportions are ordinary:
        // roughly one in nine recently hired, one in thirteen approaching retirement.
        var isNewHire = seniority >= 4 && staffNumber % 9 == 0;
        var isNearRetirement = !isNewHire && seniority <= 4 && staffNumber % 13 == 0;
        var isLongServer = !isNewHire && !isNearRetirement && seniority <= 3 && staffNumber % 7 == 0;

        // Service length otherwise tracks seniority: a Head of Department who joined last year is not
        // impossible, but a register full of them is not credible either.
        var yearsOfService = isNewHire
            ? 0
            : isLongServer
                ? 20 + rng.Next(12)
                : seniority switch
                {
                    1 => 18 + rng.Next(10),   // General Managers
                    2 => 14 + rng.Next(9),    // Heads of Department
                    3 => 9 + rng.Next(9),     // Unit and Sectional Managers
                    4 => 5 + rng.Next(9),     // Supervisors and professionals
                    5 => 1 + rng.Next(7),     // First-level officers
                    _ => 1 + rng.Next(14)     // Technical assistants, artisans, utility
                };

        // A new hire needs a date INSIDE the probation window, not merely a recent one. 30-150 days
        // ago against a 180-day probation leaves every one of them still serving it, with a spread of
        // review dates rather than a single cliff.
        var dateEmployed = isNewHire
            ? today.AddDays(-(30 + rng.Next(120)))
            : today.AddYears(-yearsOfService).AddDays(-rng.Next(360));

        // Age at hire is plausible for the grade, so nobody appears to have run a directorate at 24.
        // For the near-retirement cohort it is worked backwards from the age they are TODAY, since
        // that — not the age they joined at — is what puts them in front of a succession plan.
        var ageAtHire = isNearRetirement
            ? Math.Max(20, (57 + rng.Next(3)) - yearsOfService)
            : seniority switch
            {
                1 => 34 + rng.Next(8),
                2 => 31 + rng.Next(8),
                3 => 28 + rng.Next(8),
                4 => 25 + rng.Next(8),
                _ => 21 + rng.Next(9)
            };

        var dateOfBirth = dateEmployed.AddYears(-ageAtHire).AddDays(-rng.Next(360));

        // Probation: anyone within their probation window is still on it, which is what gives the
        // probation register live rows rather than an empty list.
        var probationDays = position.ProbationPeriodMonths is > 0
            ? position.ProbationPeriodMonths.Value * 30
            : 180;
        var probationEnds = dateEmployed.AddDays(probationDays);
        var onProbation = probationEnds > today;

        var unit = unitById.TryGetValue(position.OrganizationUnitId, out var u) ? u : null;

        var email = UniqueEmail(firstName, lastName, usedEmails);

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = $"{StaffNumberPrefix}/{staffNumber:D5}",

            FirstName = firstName,
            MiddleName = middleName,
            LastName = lastName,
            Title = TitleFor(isFemale, seniority, rng),
            Gender = isFemale ? Gender.Female : Gender.Male,
            DateOfBirth = dateOfBirth,
            MaritalStatus = MaritalStatusFor(today.Year - dateOfBirth.Year, rng),
            Religion = Pick(Religions, rng),
            Hometown = Pick(Hometowns, rng),

            // A small, believable minority, so the disability field is not empty on every record
            // and the equal-opportunity reporting has something to count.
            HasDisability = rng.Next(100) < 3,

            IsFullTime = true,
            DateEmployed = dateEmployed,

            Address = $"{Pick(Streets, rng)}, {Pick(TemaCommunities, rng)}, Tema",
            City = "Tema",
            State = "Greater Accra",
            DigitalAddress = $"GT-{rng.Next(100, 999)}-{rng.Next(1000, 9999)}",
            CountryId = countryId,

            EmailAddress = email,
            MobileNumber = $"+2332{rng.Next(10000000, 99999999)}",
            TelephoneNumber = $"+2330{rng.Next(30000000, 39999999)}",

            EmploymentType = EmploymentType.Permanent,
            ProbationPeriodDays = probationDays,
            ConfirmationDate = onProbation ? null : probationEnds,
            RetirementDate = dateOfBirth.AddYears(60),

            OrganizationUnitId = unit?.Id,

            // ⚠ SectionId is deliberately NOT set from the organisation unit, and the two are not
            // the same thing. Employee.SectionId is a foreign key to the legacy `Sections` TABLE — a
            // separate org dimension alongside Divisions, Units and WorkStations — and all four of
            // those tables are empty in this database. Assigning an OrganizationUnit id here because
            // the unit's code begins "SEC-" earns a foreign-key violation (error 547) on every
            // insert; it was the one failure of this seeder's first run. The section an employee
            // belongs to IS the organisation unit above, and that is where every HR screen reads it.
            OrganizationLevelId = position.OrganizationLevelId,
            PositionId = position.Id,
            LocationId = locationId,

            StaffStatus = onProbation ? StaffStatus.Probation : StaffStatus.Active,
            IsActive = true,
            IsExpatriate = false,

            SocialSecurityNumber = $"C{rng.Next(100000000, 999999999)}",
            TINNumber = $"P00{rng.Next(10000000, 99999999)}",
            BloodType = (BloodType)(1 + rng.Next(8)),

            Salary = SalaryFor(grade, yearsOfService, rng),
            PayTax = true,
            SSFund = true,
            GrossUp = false,
            Tier2Only = false,
            Overtime = seniority >= 6,
            // Everyone on the establishment is paid through the run; the off-payroll cohort is
            // carved out afterwards (see MakeOffPayrollCohort), so the exception is visible in one
            // place rather than scattered through the cohort logic.
            IsOnPayroll = true,

            BadgeNumber = $"B{staffNumber:D4}",

            CreatedAt = DateTime.UtcNow,
            CreatedBy = By
        };

        return employee;
    }

    private static string TitleFor(bool isFemale, int seniority, DeterministicRng rng)
    {
        // A couple of doctorates at the top of the house, which is normal for a corporation of this
        // size and stops the title column being one repeated value.
        if (seniority <= 2 && rng.Next(100) < 20) return "Dr";
        if (!isFemale) return "Mr";
        return rng.Next(100) < 65 ? "Mrs" : "Ms";
    }

    private static MaritalStatus MaritalStatusFor(int age, DeterministicRng rng)
    {
        if (age < 26) return rng.Next(100) < 85 ? MaritalStatus.Single : MaritalStatus.Married;
        if (age < 35) return rng.Next(100) < 45 ? MaritalStatus.Single : MaritalStatus.Married;
        var roll = rng.Next(100);
        if (roll < 72) return MaritalStatus.Married;
        if (roll < 84) return MaritalStatus.Single;
        return MaritalStatus.Divorced;
    }

    /// <summary>
    /// A salary inside the grade band, placed by length of service rather than at random, so the
    /// emoluments screens show a progression instead of noise.
    /// </summary>
    private static decimal SalaryFor(SalaryGrade? grade, int yearsOfService, DeterministicRng rng)
    {
        var (min, max) = grade is null || grade.MaxSalary <= 0
            ? (3000m, 6000m)
            : (grade.MinSalary, grade.MaxSalary);

        if (max <= min) return Math.Round(min, 2);

        // 25 years of service walks the band end to end; the jitter stops every peer being identical.
        var progression = Math.Clamp(yearsOfService / 25m, 0m, 1m);
        var jitter = (rng.Next(0, 120) - 60) / 1000m;
        var placement = Math.Clamp(progression + jitter, 0m, 1m);

        var salary = min + (max - min) * placement;
        return Math.Round(salary / 10m, 0) * 10m;
    }

    private static string UniqueEmail(string firstName, string lastName, HashSet<string> used)
    {
        var baseName = $"{firstName}.{lastName}".ToLowerInvariant().Replace(" ", "");
        var candidate = $"{baseName}@tdc.com.gh";

        var suffix = 1;
        while (!used.Add(candidate))
        {
            suffix++;
            candidate = $"{baseName}{suffix}@tdc.com.gh";
        }

        return candidate;
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Supporting rows
    // ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills in salary-grade bands where the organogram seeder left them at zero.
    /// </summary>
    /// <remarks>
    /// All eight grades ship with MinSalary and MaxSalary of 0, which makes every emolument, payroll
    /// and salary-review screen read zero and makes a grade band impossible to demonstrate. These are
    /// plausible monthly GHS figures for a Ghanaian state corporation and are DEMO VALUES — they must
    /// be replaced with the real salary structure before anything but a demonstration.
    /// Existing non-zero bands are never overwritten.
    /// </remarks>
    private void EnsureGradeBands(List<SalaryGrade> grades)
    {
        var bands = new Dictionary<string, (decimal Min, decimal Max)>(StringComparer.OrdinalIgnoreCase)
        {
            ["M1"] = (18000m, 28000m),  // General Managers
            ["M2"] = (12500m, 19000m),  // Heads of Department
            ["M3"] = (8500m, 13000m),   // Unit and Sectional Managers
            ["M4"] = (5800m, 9000m),    // Supervisors, technical and administrative professionals
            ["M5"] = (4000m, 6200m),    // First-level officers and management trainees
            ["S1"] = (2700m, 4300m),    // Technical assistants
            ["S2"] = (1900m, 3000m),    // Artisans and drivers
            ["S3"] = (1450m, 2200m)     // Utility
        };

        var filled = 0;
        foreach (var grade in grades)
        {
            if (grade.MinSalary > 0 || grade.MaxSalary > 0) continue;
            if (!bands.TryGetValue(grade.Code, out var band)) continue;

            grade.MinSalary = band.Min;
            grade.MaxSalary = band.Max;
            grade.UpdatedAt = DateTime.UtcNow;
            grade.UpdatedBy = By;
            filled++;
        }

        if (filled > 0)
        {
            _logger.LogInformation(
                "Filled {Count} salary-grade band(s) with DEMO figures. Replace with the real salary " +
                "structure before this database is used for anything but a demonstration.", filled);
        }
    }

    /// <summary>
    /// Registers the staff-number format so HR can create an employee after seeding and receive a
    /// number in the same shape as the seeded ones.
    /// </summary>
    /// <remarks>
    /// With no <see cref="StaffNumberFormat"/> row a tenant is in manual mode, which is the correct
    /// default but the wrong demo: the create-employee screen asks the presenter to invent a number
    /// on stage, and whatever they type is inconsistent with every row already on screen.
    /// </remarks>
    private async Task EnsureStaffNumberRegisterAsync(Guid tenantId, CancellationToken ct)
    {
        var exists = await _context.Set<StaffNumberFormat>()
            .IgnoreQueryFilters()
            .AnyAsync(f => f.TenantId == tenantId && !f.IsDeleted, ct);

        if (exists) return;

        _context.Set<StaffNumberFormat>().Add(new StaffNumberFormat
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Permanent staff",

            // The tenant default: null catches every employment type without a rule of its own, so
            // one row is enough rather than nine.
            AppliesToEmploymentType = null,

            Prefix = StaffNumberPrefix,
            Separator = "/",

            // No year in the number, and therefore no annual reset. A demo database rebuilt in
            // January must not renumber the workforce it had in December.
            IncludeYear = false,
            SequenceDigits = 5,
            Suffix = "",

            AutoGenerate = true,
            SequenceKey = StaffSequenceKey,
            IsActive = true,

            CreatedAt = DateTime.UtcNow,
            CreatedBy = By
        });

        _logger.LogInformation("Registered the '{Prefix}/00001' staff-number format.", StaffNumberPrefix);
    }

    /// <summary>Raises a number sequence to at least <paramref name="value"/>; never lowers it.</summary>
    private void EnsureSequenceAtLeast(Guid tenantId, string key, int year, long value)
    {
        if (value <= 0) return;

        var existing = _context.Set<NumberSequence>().Local
            .FirstOrDefault(s => s.TenantId == tenantId && s.SequenceKey == key && s.Year == year)
            ?? _context.Set<NumberSequence>().IgnoreQueryFilters()
                .FirstOrDefault(s => s.TenantId == tenantId && s.SequenceKey == key && s.Year == year);

        if (existing is null)
        {
            _context.Set<NumberSequence>().Add(new NumberSequence
            {
                TenantId = tenantId,
                SequenceKey = key,
                Year = year,
                NextValue = value
            });
        }
        else if (existing.NextValue < value)
        {
            existing.NextValue = value;
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Seniority
    // ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 1 (most senior) to 8, read from the position's salary grade. Positions with no grade sort to
    /// the middle rather than the top: an ungraded position is unknown, not senior.
    /// </summary>
    private static int SeniorityOf(EmployeePosition position, IReadOnlyDictionary<Guid, SalaryGrade> grades)
    {
        if (position.SalaryGradeId is not { } id || !grades.TryGetValue(id, out var grade)) return 5;
        return GradeRank(grade.Code);
    }

    /// <summary>M1..M5 rank 1..5; S1..S3 rank 6..8. Anything unrecognised sorts mid-table.</summary>
    private static int GradeRank(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 2) return 5;
        if (!int.TryParse(code.AsSpan(1), out var n)) return 5;

        return code[0] switch
        {
            'M' or 'm' => Math.Clamp(n, 1, 5),
            'S' or 's' => Math.Clamp(n, 1, 3) + 5,
            _ => 5
        };
    }

    private static string Pick(string[] pool, DeterministicRng rng) => pool[rng.Next(pool.Length)];

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Name and place pools — Ghanaian, because the corporation is
    // ────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] MaleFirstNames =
    {
        "Kwame", "Kofi", "Yaw", "Kwabena", "Kwaku", "Kojo", "Kwasi", "Nana", "Fiifi", "Ebo",
        "Emmanuel", "Samuel", "Daniel", "Isaac", "Joseph", "Michael", "Richard", "Francis",
        "Prince", "Bright", "Godfred", "Eric", "Stephen", "Alfred", "Patrick", "Benjamin",
        "Nii", "Tetteh", "Ayitey", "Kwadwo", "Selorm", "Elikem", "Mawuli", "Senyo",
        "Abdul-Rahman", "Iddrisu", "Mohammed", "Yakubu", "Alhassan", "Sulemana"
    };

    private static readonly string[] FemaleFirstNames =
    {
        "Akosua", "Ama", "Abena", "Adwoa", "Afua", "Yaa", "Esi", "Efua", "Araba", "Mansa",
        "Grace", "Comfort", "Mercy", "Gifty", "Vida", "Patience", "Cynthia", "Priscilla",
        "Naa", "Dede", "Adukwei", "Korkor", "Lariba", "Fati", "Zeinab", "Hawa",
        "Elikplim", "Dzifa", "Sena", "Akpene", "Belinda", "Josephine", "Rebecca", "Doris",
        "Millicent", "Linda", "Sandra", "Emelia", "Regina", "Beatrice"
    };

    private static readonly string[] Surnames =
    {
        "Mensah", "Owusu", "Boateng", "Asante", "Agyemang", "Osei", "Appiah", "Frimpong",
        "Amoah", "Darko", "Ofori", "Adjei", "Ansah", "Baah", "Gyasi", "Nkrumah",
        "Quartey", "Lartey", "Tetteh", "Nortey", "Ayikwei", "Odoi", "Ashong", "Aryee",
        "Agbeko", "Dzomeku", "Fiadzo", "Akakpo", "Gbedemah", "Nyaho",
        "Abubakari", "Mahama", "Seidu", "Zakaria", "Musah",
        "Acheampong", "Bediako", "Danquah", "Essien", "Koomson", "Sarpong", "Yeboah",
        "Otoo", "Bruce-Quaye", "Hammond", "Reindorf", "Sowah", "Tagoe", "Vanderpuye"
    };

    private static readonly string[] Religions =
    {
        "Christianity", "Christianity", "Christianity", "Christianity", "Islam", "Islam",
        "Traditional", "None"
    };

    private static readonly string[] Hometowns =
    {
        "Tema", "Accra", "Kumasi", "Cape Coast", "Koforidua", "Ho", "Takoradi", "Sunyani",
        "Tamale", "Bolgatanga", "Wa", "Winneba", "Nsawam", "Akosombo", "Elmina", "Aburi",
        "Prampram", "Ada", "Somanya", "Techiman"
    };

    private static readonly string[] TemaCommunities =
    {
        "Community 1", "Community 2", "Community 4", "Community 5", "Community 6",
        "Community 8", "Community 9", "Community 11", "Community 18", "Community 22",
        "Community 25", "Sakumono", "Ashaiman", "Lashibi", "Nungua", "Spintex"
    };

    private static readonly string[] Streets =
    {
        "Hospital Road", "Meridian Drive", "Harbour Road", "Fishing Harbour Lane", "Nii Adjei Street",
        "Kaiser Avenue", "Liberation Link", "Republic Road", "Independence Avenue", "Ring Road",
        "Volta Street", "Akosombo Close", "Tetteh Quarshie Street", "Osu Badu Link"
    };

    // ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A small fixed-sequence generator, so the demo workforce is byte-for-byte the same on every
    /// rebuild.
    /// </summary>
    /// <remarks>
    /// <see cref="Random"/> is explicitly documented as not guaranteeing the same sequence across
    /// .NET versions, and it changed once already between .NET Framework and .NET Core. A demo
    /// script that names an employee cannot depend on that. This is the classic 32-bit
    /// linear congruential generator from Numerical Recipes; it is not remotely suitable for
    /// anything but placeholder data, which is all it is asked to produce.
    /// </remarks>
    private sealed class DeterministicRng
    {
        private uint _state;

        public DeterministicRng(uint seed) => _state = seed == 0 ? 1u : seed;

        private uint NextUInt()
        {
            _state = unchecked(1664525u * _state + 1013904223u);
            return _state;
        }

        /// <summary>A non-negative value below <paramref name="exclusiveUpperBound"/>.</summary>
        public int Next(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0) return 0;
            return (int)(NextUInt() % (uint)exclusiveUpperBound);
        }

        /// <summary>A value in [inclusiveLower, exclusiveUpper).</summary>
        public int Next(int inclusiveLower, int exclusiveUpper)
        {
            if (exclusiveUpper <= inclusiveLower) return inclusiveLower;
            return inclusiveLower + Next(exclusiveUpper - inclusiveLower);
        }
    }
}
