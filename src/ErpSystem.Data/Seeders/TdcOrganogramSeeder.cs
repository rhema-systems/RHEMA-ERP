using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the TDC organisation structure into the <b>DEFAULT tenant</b> (the tenant the <c>admin</c>
/// user signs in to), reconciled from three client documents:
///   • <b>TDC organogram</b>            — authoritative for units and reporting lines.
///   • <b>Staff position extracts</b>   — authoritative for which positions actually exist.
///   • <b>HR workflow questionnaire</b> — authoritative for grades, staff categories and policy.
///
/// MODEL (important): the organogram's boxes are <b>positions</b>, not organisation units.
///   • <see cref="OrganizationUnit"/>  = the *places*: directorates, departments, units, sections.
///   • <see cref="EmployeePosition"/>  = the *jobs*: "Head of Development", "Secretary", "Cashier".
///     Each is anchored to a unit; the organogram's connecting lines become
///     <see cref="EmployeePosition.ReportsToPositionId"/>.
///
/// Position titles are stored in the <b>singular</b> ("Draughtsman", not "Draughtsmen") because a
/// position is one job — plurality is expressed by <see cref="EmployeePosition.ExpectedHeadcount"/>.
///
/// SAFE BY CONSTRUCTION:
///   1. Runs only when invoked explicitly via a seed CLI argument; never on normal startup.
///   2. Idempotent: returns immediately if the "TDC" structure already exists for the tenant.
///   3. Creates structure / levels / grades / units / positions only — no employees.
/// </summary>
public class TdcOrganogramSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcOrganogramSeeder> _logger;

    public TdcOrganogramSeeder(ApplicationDbContext context, ILogger<TdcOrganogramSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // Target the DEFAULT tenant (the one the admin user logs in to). Resolved by code rather
        // than a hard-coded id, matching the convention used by the other seeders.
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT");
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed the TDC organisation structure.");
            return;
        }

        var tenantId = tenant.Id;

        // Idempotency guard — never duplicate, never touch a previous run.
        if (await _context.Set<OrganizationStructure>().AnyAsync(s => s.Code == "TDC" && s.TenantId == tenantId))
        {
            _logger.LogInformation("TDC organisation structure already seeded for the DEFAULT tenant. Skipping.");
            return;
        }

        var now = DateTime.UtcNow;
        const string by = "TdcOrganogramSeeder";

        // ── 1) Structure ────────────────────────────────────────────────────────────────────────
        var structure = new OrganizationStructure
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "TDC Organisational Structure",
            Code = "TDC",
            IsDefault = true,
            IsActive = true,
            Description = "Tema Development Corporation organisation structure.",
            CreatedAt = now,
            CreatedBy = by
        };
        _context.Add(structure);

        // ── 2) Organisation levels (1 = highest) ────────────────────────────────────────────────
        var levelDefs = new (string Code, string Name, int Number)[]
        {
            ("GOV",  "Governance",        1),
            ("EXEC", "Executive",         2),
            ("DIR",  "Directorate",       3),
            ("DEPT", "Department / Unit", 4),
            ("SEC",  "Section",           5),
        };

        var levelIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var levelNumberByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name, number) in levelDefs)
        {
            var level = new OrganizationLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StructureId = structure.Id,
                Name = name,
                Code = code,
                LevelNumber = number,
                RequiresHead = number <= 4,
                AllowsDirectEmployees = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = by
            };
            levelIdByCode[code] = level.Id;
            levelNumberByCode[code] = number;
            _context.Add(level);
        }

        // ── 3) Staff categories ─────────────────────────────────────────────────────────────────
        // The three bands named in the HR questionnaire: Management, Senior Staff (Senior Staff
        // Association) and Junior Staff (unionised). Derived from a position's grade — see GradeDefs.
        var staffLevelIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name, rank) in new[]
                 {
                     ("MGT", "Management Staff", 1),
                     ("SNR", "Senior Staff",     2),
                     ("JNR", "Junior Staff",     3),
                 })
        {
            var sl = new StaffLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Code = code,
                Rank = rank,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = by
            };
            staffLevelIdByCode[code] = sl.Id;
            _context.Add(sl);
        }

        // ── 4) Salary grades — TDC's 8-grade scheme (M1–M5, S1–S3) ──────────────────────────────
        // Salary bands are intentionally left at zero: the Scheme of Service figures were not
        // supplied, and inventing them would corrupt payroll calculations downstream.
        var gradeIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name, staffLevel, _) in GradeDefs)
        {
            var band = staffLevel switch
            {
                "MGT" => "Management Staff",
                "SNR" => "Senior Staff",
                _ => "Junior Staff"
            };

            var grade = new SalaryGrade
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                Description = $"{code} — {name} ({band}).",
                IsActive = true,
                EffectiveDate = now,
                CreatedAt = now,
                CreatedBy = by
            };
            gradeIdByCode[code] = grade.Id;
            _context.Add(grade);
        }

        // ── 5) Organisation units ───────────────────────────────────────────────────────────────
        var unitDefs = BuildUnitDefinitions();
        var unitIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var unitLevelByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sequenceByParent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in unitDefs)
        {
            var id = Guid.NewGuid();
            unitIdByCode[def.Code] = id;
            unitLevelByCode[def.Code] = def.LevelCode;

            Guid? parentId = null;
            if (def.ParentCode is not null)
            {
                if (!unitIdByCode.TryGetValue(def.ParentCode, out var pid))
                    throw new InvalidOperationException(
                        $"TDC seeder: unit '{def.Code}' references unknown parent '{def.ParentCode}'. Define parents first.");
                parentId = pid;
            }

            var parentKey = def.ParentCode ?? "<root>";
            var seq = sequenceByParent.TryGetValue(parentKey, out var s) ? s + 1 : 1;
            sequenceByParent[parentKey] = seq;

            _context.Add(new OrganizationUnit
            {
                Id = id,
                TenantId = tenantId,
                OrganizationLevelId = levelIdByCode[def.LevelCode],
                ParentUnitId = parentId,
                Name = def.Name,
                Code = def.Code,
                Description = def.Classification,
                Sequence = seq,
                Path = string.Empty,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = by
            });
        }

        // ── 6) Positions (the organogram boxes) ─────────────────────────────────────────────────
        var positionDefs = BuildPositionDefinitions();
        var positionIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var gradeMeta = GradeDefs.ToDictionary(g => g.Code, g => (g.StaffLevel, g.ProbationMonths),
                                               StringComparer.OrdinalIgnoreCase);

        foreach (var def in positionDefs)
        {
            var id = Guid.NewGuid();
            positionIdByCode[def.Code] = id;

            if (!unitIdByCode.TryGetValue(def.UnitCode, out var unitId))
                throw new InvalidOperationException(
                    $"TDC seeder: position '{def.Code}' references unknown unit '{def.UnitCode}'.");

            Guid? reportsToId = null;
            if (def.ReportsToCode is not null)
            {
                if (!positionIdByCode.TryGetValue(def.ReportsToCode, out var rid))
                    throw new InvalidOperationException(
                        $"TDC seeder: position '{def.Code}' reports to unknown position '{def.ReportsToCode}'. Define superiors first.");
                reportsToId = rid;
            }

            // The Managing Director sits above the M1–S3 scheme, so carries no salary grade.
            var hasGrade = gradeMeta.TryGetValue(def.Grade, out var meta);
            var staffLevelCode = hasGrade ? meta.StaffLevel : "MGT";
            var levelCode = unitLevelByCode[def.UnitCode];

            _context.Add(new EmployeePosition
            {
                Id = id,
                TenantId = tenantId,
                Title = def.Title,
                Code = def.Code,
                OrganizationUnitId = unitId,
                OrganizationLevelId = levelIdByCode[levelCode],
                ReportsToPositionId = reportsToId,
                StaffLevelId = staffLevelIdByCode[staffLevelCode],
                SalaryGradeId = hasGrade ? gradeIdByCode[def.Grade] : null,
                Level = levelNumberByCode[levelCode],
                // Probation: 6 months for senior/management staff, 3 for junior (HR questionnaire).
                ProbationPeriodMonths = hasGrade ? meta.ProbationMonths : 6,
                // Headcount is filled in from the staff extracts in a later pass; 1 is the safe default.
                ExpectedHeadcount = 1,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = by
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation(
            "TDC structure seeded into the DEFAULT tenant: {Levels} levels, {Grades} grades, {Units} units, {Positions} positions.",
            levelDefs.Length, GradeDefs.Length, unitDefs.Count, positionDefs.Count);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// TDC's grading scheme (HR questionnaire §3): 8 grades. The staff band and probation period are
    /// derived from the grade rather than stored per position, so the two cannot drift apart.
    /// </summary>
    private static readonly (string Code, string Name, string StaffLevel, int ProbationMonths)[] GradeDefs =
    {
        ("M1", "General Managers",                                        "MGT", 6),
        ("M2", "Heads of Department",                                     "MGT", 6),
        ("M3", "Unit and Sectional Managers",                             "SNR", 6),
        ("M4", "Supervisors and Technical/Administrative Professionals",  "SNR", 6),
        ("M5", "First Level Officers and Management Trainees",            "SNR", 6),
        ("S1", "Technical Assistants",                                    "JNR", 3),
        ("S2", "Artisans and Drivers",                                    "JNR", 3),
        ("S3", "Utility Men",                                             "JNR", 3),
    };

    private sealed record UnitDef(string Code, string Name, string? ParentCode, string LevelCode,
                                  string? Classification = null);

    /// <summary>
    /// Units = the organogram page headers + the Department column of the staff extracts. The
    /// organogram's own page headers already distinguish DEPARTMENT from UNIT, and that split matches
    /// the HR questionnaire's "7 core departments + 4 business units" exactly.
    /// Sections come from the contract-staff extract and the questionnaire's section lists.
    /// </summary>
    private static List<UnitDef> BuildUnitDefinitions() => new()
    {
        // Governance / Executive
        new("BOARD",     "Board of Directors",                   null,    "GOV"),
        new("MDO",       "Managing Director's Office",           "BOARD", "EXEC"),

        // Directorates
        new("DIR-OPS",   "Operations Directorate",               "MDO",   "DIR"),
        new("DIR-FA",    "Finance & Administration Directorate", "MDO",   "DIR"),

        // Reports to the Board (audit independence)
        new("DEPT-IA",   "Internal Audit Department",            "BOARD", "DEPT", "Core department"),

        // Report directly to the Managing Director
        new("DEPT-LEG",  "Legal Department",                               "MDO", "DEPT", "Core department"),
        new("DEPT-CPC",  "Corporate Planning & Communications Department", "MDO", "DEPT", "Core department"),
        new("UNIT-PROC", "Procurement Unit",                               "MDO", "DEPT", "Business unit"),
        // MERC sits under the MD on the organogram; its staff are grouped under Audit for reporting.
        new("UNIT-MERC", "MERC Unit",                                      "MDO", "DEPT", "Business unit"),

        // Under the Operations Directorate
        new("DEPT-DEV",  "Development Department",   "DIR-OPS", "DEPT", "Core department"),
        new("UNIT-DC",   "Development Control Unit", "DIR-OPS", "DEPT", "Unit"),
        new("DEPT-EST",  "Estates Department",       "DIR-OPS", "DEPT", "Core department"),
        new("UNIT-MKT",  "Marketing Unit",           "DIR-OPS", "DEPT", "Business unit"),
        // Task Force appears only in the staff records — not on the organogram or in the questionnaire.
        new("UNIT-TF",   "Task Force",               "DIR-OPS", "DEPT", "Unit"),

        // Under the Finance & Administration Directorate
        new("DEPT-HRA",  "HR / Administration Department", "DIR-FA", "DEPT", "Core department"),
        new("DEPT-FIN",  "Finance Department",             "DIR-FA", "DEPT", "Core department"),
        new("UNIT-MIS",  "MIS Unit",                       "DIR-FA", "DEPT", "Business unit"),

        // Sections — Development
        new("SEC-DEV-QS",  "Quantity Survey",       "DEPT-DEV", "SEC"),
        new("SEC-DEV-ARC", "Architecture",          "DEPT-DEV", "SEC"),
        new("SEC-DEV-SUR", "Survey / Geodetic",     "DEPT-DEV", "SEC"),
        new("SEC-DEV-PLN", "Planning",              "DEPT-DEV", "SEC"),
        new("SEC-DEV-BM",  "Building Maintenance",  "DEPT-DEV", "SEC"),

        // Sections — Development Control
        new("SEC-DC-BIS",  "Building Inspectorate", "UNIT-DC",  "SEC"),

        // Sections — Estates
        new("SEC-EST-LND", "Lands",                 "DEPT-EST", "SEC"),
        new("SEC-EST-HSG", "Housing",               "DEPT-EST", "SEC"),
        new("SEC-EST-FM",  "Facilities Management", "DEPT-EST", "SEC"),
        new("SEC-EST-REC", "Records",               "DEPT-EST", "SEC"),

        // Sections — HR / Administration (questionnaire: HR, Admin, SHE, Transport, Archival/Registry)
        new("SEC-HRA-HR",  "Human Resource",               "DEPT-HRA", "SEC"),
        new("SEC-HRA-ADM", "Administration",               "DEPT-HRA", "SEC"),
        new("SEC-HRA-SHE", "Safety, Health & Environment", "DEPT-HRA", "SEC"),
        new("SEC-HRA-TRP", "Transport",                    "DEPT-HRA", "SEC"),
        new("SEC-HRA-REG", "Archival / Registry",          "DEPT-HRA", "SEC"),

        // Sections — Finance
        new("SEC-FIN-MA",  "Management Accounts",   "DEPT-FIN", "SEC"),
        new("SEC-FIN-FA",  "Financial Accounts",    "DEPT-FIN", "SEC"),
        new("SEC-FIN-REV", "Revenue",               "DEPT-FIN", "SEC"),
        new("SEC-FIN-CB",  "Cash & Banks",          "DEPT-FIN", "SEC"),
        new("SEC-FIN-STR", "Stores & Fixed Assets", "DEPT-FIN", "SEC"),

        // Sections — Corporate Planning & Communications
        new("SEC-CPC-CP",  "Corporate Planning",    "DEPT-CPC", "SEC"),
        new("SEC-CPC-COM", "Communications",        "DEPT-CPC", "SEC"),
        new("SEC-CPC-CR",  "Client Relations",      "DEPT-CPC", "SEC"),
    };

    // ════════════════════════════════════════════════════════════════════════════════════════════
    private sealed record PositionDef(
        string Code, string Title, string UnitCode, string? ReportsToCode, string Grade);

    /// <summary>
    /// Positions = the organogram boxes. Superiors must be declared before their subordinates.
    /// Grades follow the questionnaire's own definitions (M1 GMs, M2 HODs, M3 managers, M4
    /// supervisors and professionals, M5 first-level officers, S1 technical assistants, S2
    /// artisans/drivers, S3 utility). A few field roles (guards, workmen) are inferred by band.
    /// </summary>
    private static List<PositionDef> BuildPositionDefinitions() => new()
    {
        // ── Executive ───────────────────────────────────────────────────────────────────────────
        // "EXEC" is not a grade: the MD sits above the M1–S3 scheme and so carries no salary grade.
        new("MD",    "Managing Director",                          "MDO",     null, "EXEC"),
        // Chief Internal Auditor reports to the Board (dotted line to the MD = administrative only).
        new("CIA",   "Chief Internal Auditor",                     "DEPT-IA", null, "M2"),
        new("GMO",   "General Manager - Operations",               "DIR-OPS", "MD", "M1"),
        new("GMF",   "General Manager - Finance & Administration", "DIR-FA",  "MD", "M1"),

        // ── Heads reporting directly to the Managing Director ────────────────────────────────────
        new("HLEG",  "Head of Legal",                              "DEPT-LEG",  "MD", "M2"),
        new("HCPC",  "Head of Corporate Planning & Communications","DEPT-CPC",  "MD", "M2"),
        new("HPRO",  "Head of Procurement",                        "UNIT-PROC", "MD", "M2"),
        new("HMERC", "Head of MERC",                               "UNIT-MERC", "MD", "M2"),

        // ── Heads under the Operations Directorate ──────────────────────────────────────────────
        new("HDEV",  "Head of Development",                        "DEPT-DEV", "GMO", "M2"),
        new("HEST",  "Head of Estates",                            "DEPT-EST", "GMO", "M2"),
        new("HSM",   "Head of Sales & Marketing",                  "UNIT-MKT", "GMO", "M2"),
        new("HDC",   "Head of Development Control",                "UNIT-DC",  "GMO", "M2"),
        new("HTF",   "Head of Task Force",                         "UNIT-TF",  "GMO", "M2"),

        // ── Heads under the Finance & Administration Directorate ────────────────────────────────
        new("HHR",   "Head of HR & Administration",                "DEPT-HRA", "GMF", "M2"),
        new("HFIN",  "Head of Finance",                            "DEPT-FIN", "GMF", "M2"),
        new("HMIS",  "Head of MIS",                                "UNIT-MIS", "GMF", "M2"),

        // ── Internal Audit ──────────────────────────────────────────────────────────────────────
        new("IA-SEC", "Secretary",                    "DEPT-IA", "CIA",   "S1"),
        new("IA-AM",  "Audit Manager",                "DEPT-IA", "CIA",   "M3"),
        new("IA-AS",  "Audit Supervisor",             "DEPT-IA", "IA-AM", "M4"),
        new("IA-CO",  "Compliance Officer",           "DEPT-IA", "IA-AM", "M4"),
        // The chart shows one "Audit Assistants" box serving both supervisors; modelled as two roles.
        new("IA-AAS", "Audit Assistant (Audit)",      "DEPT-IA", "IA-AS", "S1"),
        new("IA-AAC", "Audit Assistant (Compliance)", "DEPT-IA", "IA-CO", "S1"),

        // ── MERC ────────────────────────────────────────────────────────────────────────────────
        new("MC-SUP", "MERC Supervisor",     "UNIT-MERC", "HMERC",  "M4"),
        new("MC-SMO", "Senior MERC Officer", "UNIT-MERC", "MC-SUP", "M4"),
        new("MC-AST", "MERC Assistant",      "UNIT-MERC", "MC-SMO", "S1"),

        // ── Development ─────────────────────────────────────────────────────────────────────────
        new("DV-SEC", "Secretary",                       "DEPT-DEV",    "HDEV",   "S1"),
        new("DV-PC",  "Project Coordinator",             "DEPT-DEV",    "HDEV",   "M4"),
        new("DV-SD",  "Senior Draftsman",                "DEPT-DEV",    "HDEV",   "M4"),
        new("DV-DR",  "Draughtsman",                     "DEPT-DEV",    "DV-SD",  "S1"),
        new("DV-SCE", "Supervising Civil Engineer",      "DEPT-DEV",    "HDEV",   "M4"),
        new("DV-CE",  "Civil Engineer",                  "DEPT-DEV",    "DV-SCE", "M4"),
        new("DV-SQS", "Supervising Quantity Surveyor",   "SEC-DEV-QS",  "HDEV",   "M4"),
        new("DV-QS",  "Quantity Surveyor",               "SEC-DEV-QS",  "DV-SQS", "M4"),
        new("DV-AQS", "Assistant Quantity Surveyor",     "SEC-DEV-QS",  "DV-QS",  "M5"),
        new("DV-SA",  "Supervising Architect",           "SEC-DEV-ARC", "HDEV",   "M4"),
        new("DV-ARC", "Architect",                       "SEC-DEV-ARC", "DV-SA",  "M4"),
        new("DV-SGE", "Supervising Geodetic Engineer",   "SEC-DEV-SUR", "HDEV",   "M4"),
        new("DV-GE",  "Geodetic Engineer",               "SEC-DEV-SUR", "DV-SGE", "M4"),
        new("DV-GT",  "Geodetic Technician",             "SEC-DEV-SUR", "DV-GE",  "S1"),
        new("DV-SW",  "Survey Workman",                  "SEC-DEV-SUR", "DV-GT",  "S3"),
        new("DV-STP", "Supervising Town Planner",        "SEC-DEV-PLN", "HDEV",   "M4"),
        new("DV-PP",  "Physical Planner",                "SEC-DEV-PLN", "DV-STP", "M4"),
        new("DV-PO",  "Printing Officer",                "SEC-DEV-PLN", "DV-PP",  "S1"),
        new("DV-BMS", "Building Maintenance Supervisor", "SEC-DEV-BM",  "DV-SCE", "M4"),
        new("DV-ART", "Artisan",                         "SEC-DEV-BM",  "DV-BMS", "S2"),

        // ── Development Control ─────────────────────────────────────────────────────────────────
        new("DC-SEC", "Secretary",                        "UNIT-DC",    "HDC",    "S1"),
        new("DC-GS",  "Guardsmen Supervisor",             "UNIT-DC",    "HDC",    "M4"),
        new("DC-GM",  "Guardsman",                        "UNIT-DC",    "DC-GS",  "S2"),
        new("DC-BIS", "Building Inspectorate Supervisor", "SEC-DC-BIS", "HDC",    "M4"),
        new("DC-BI",  "Building Inspector",               "SEC-DC-BIS", "DC-BIS", "S1"),

        // ── Task Force (kept separate from the Development Control guardsmen) ───────────────────
        new("TF-SUP", "Task Force Supervisor", "UNIT-TF", "HTF",    "M4"),
        new("TF-SEC", "Security Supervisor",   "UNIT-TF", "HTF",    "M4"),
        new("TF-G",   "Task Force Guard",      "UNIT-TF", "TF-SUP", "S2"),

        // ── Estates ─────────────────────────────────────────────────────────────────────────────
        new("ES-SEC", "Secretary",                   "DEPT-EST",    "HEST",   "S1"),
        new("ES-EML", "Estates Manager - Lands",     "SEC-EST-LND", "HEST",   "M3"),
        new("ES-EOL", "Estates Officer - Lands",     "SEC-EST-LND", "ES-EML", "M5"),
        new("ES-EAL", "Estates Assistant - Lands",   "SEC-EST-LND", "ES-EOL", "S1"),
        new("ES-EMH", "Estates Manager - Housing",   "SEC-EST-HSG", "HEST",   "M3"),
        new("ES-EOH", "Estates Officer - Housing",   "SEC-EST-HSG", "ES-EMH", "M5"),
        new("ES-EAH", "Estates Assistant - Housing", "SEC-EST-HSG", "ES-EOH", "S1"),
        new("ES-FM",  "Facilities Manager",          "SEC-EST-FM",  "HEST",   "M3"),
        new("ES-FO",  "Facilities Officer",          "SEC-EST-FM",  "ES-FM",  "M5"),
        new("ES-UC",  "Utility Attendant / Cleaner", "SEC-EST-FM",  "ES-FO",  "S3"),
        new("ES-RO",  "Records Officer",             "SEC-EST-REC", "ES-FM",  "M5"),
        new("ES-TY",  "Typist",                      "SEC-EST-REC", "ES-RO",  "S1"),
        new("ES-RA",  "Records Assistant",           "SEC-EST-REC", "ES-RO",  "S1"),

        // ── HR / Administration ─────────────────────────────────────────────────────────────────
        new("HR-SEC", "Secretary",              "DEPT-HRA",    "HHR",    "S1"),
        new("HR-HRM", "Human Resource Manager", "SEC-HRA-HR",  "HHR",    "M3"),
        new("HR-HRO", "Human Resource Officer", "SEC-HRA-HR",  "HR-HRM", "M5"),
        new("HR-AC",  "HR Assistant",           "SEC-HRA-HR",  "HR-HRO", "S1"),
        new("HR-AO",  "Administrative Officer", "SEC-HRA-ADM", "HHR",    "M5"),
        new("HR-HSE", "HSE Supervisor",         "SEC-HRA-SHE", "HHR",    "M4"),
        new("HR-HSA", "HSE Assistant",          "SEC-HRA-SHE", "HR-HSE", "S1"),
        new("HR-ENV", "Environmental Officer",  "SEC-HRA-SHE", "HR-HSE", "M5"),
        new("HR-TS",  "Transport Supervisor",   "SEC-HRA-TRP", "HHR",    "M4"),
        new("HR-DRV", "Driver",                 "SEC-HRA-TRP", "HR-TS",  "S2"),
        new("HR-ARC", "Archival Clerk",         "SEC-HRA-REG", "HHR",    "S1"),
        new("HR-RGA", "Registry Assistant",     "SEC-HRA-REG", "HR-ARC", "S1"),

        // ── Corporate Planning & Communications ─────────────────────────────────────────────────
        new("CP-SEC", "Secretary",                                "DEPT-CPC",    "HCPC",   "S1"),
        new("CP-CPM", "Corporate Planning Manager",               "SEC-CPC-CP",  "HCPC",   "M3"),
        new("CP-CPO", "Corporate Planning Officer",               "SEC-CPC-CP",  "CP-CPM", "M5"),
        new("CP-CM",  "Communications Manager",                   "SEC-CPC-COM", "HCPC",   "M3"),
        new("CP-CO",  "Communications Officer",                   "SEC-CPC-COM", "CP-CM",  "M5"),
        new("CP-CRS", "Client Relations Supervisor",              "SEC-CPC-CR",  "HCPC",   "M4"),
        new("CP-CRV", "Client Relations Assistant - VIP",         "SEC-CPC-CR",  "CP-CRS", "S1"),
        new("CP-CRF", "Client Relations Assistant - Front Desk",  "SEC-CPC-CR",  "CP-CRS", "S1"),
        new("CP-CRC", "Client Relations Assistant - Call Centre", "SEC-CPC-CR",  "CP-CRS", "S1"),
        new("CP-CRR", "Client Relations Assistant - Complaint Desk / Registry", "SEC-CPC-CR", "CP-CRS", "S1"),

        // ── Legal ───────────────────────────────────────────────────────────────────────────────
        new("LG-SEC", "Secretary",                "DEPT-LEG", "HLEG",   "S1"),
        new("LG-SLO", "Senior Legal Officer",     "DEPT-LEG", "HLEG",   "M4"),
        new("LG-LO",  "Legal Officer",            "DEPT-LEG", "LG-SLO", "M5"),
        new("LG-AA",  "Administrative Assistant", "DEPT-LEG", "LG-LO",  "S1"),

        // ── Finance ─────────────────────────────────────────────────────────────────────────────
        new("FN-SEC",  "Secretary",                               "DEPT-FIN",    "HFIN",   "S1"),
        new("FN-MA",   "Management Accountant",                   "SEC-FIN-MA",  "HFIN",   "M3"),
        new("FN-MAS",  "Management Accounts Supervisor",          "SEC-FIN-MA",  "FN-MA",  "M4"),
        new("FN-SFA",  "Stores & Fixed Assets Supervisor",        "SEC-FIN-STR", "FN-MA",  "M4"),
        new("FN-SAS",  "Stores Assistant",                        "SEC-FIN-STR", "FN-SFA", "S1"),
        new("FN-FA",   "Financial Accountant",                    "SEC-FIN-FA",  "HFIN",   "M3"),
        new("FN-FAS",  "Final Accounts Supervisor",               "SEC-FIN-FA",  "FN-FA",  "M4"),
        new("FN-DEC",  "Data Entry Clerk",                        "SEC-FIN-FA",  "FN-FAS", "S1"),
        new("FN-EAS",  "Expenditure Accounts Supervisor",         "SEC-FIN-FA",  "FN-FA",  "M4"),
        new("FN-PO",   "Pay Officer",                             "SEC-FIN-FA",  "FN-EAS", "M5"),
        new("FN-PC",   "Paying Cashier",                          "SEC-FIN-FA",  "FN-EAS", "S1"),
        new("FN-AO",   "Accounts Officer",                        "SEC-FIN-FA",  "FN-FA",  "M5"),
        new("FN-RAS",  "Revenue Accounts Supervisor",             "SEC-FIN-REV", "FN-FA",  "M4"),
        new("FN-ARA1", "Accounts Receivable Assistant (Revenue)", "SEC-FIN-REV", "FN-RAS", "S1"),
        new("FN-CBS",  "Cash & Banks Supervisor",                 "SEC-FIN-CB",  "FN-FA",  "M4"),
        new("FN-CSH",  "Cashier",                                 "SEC-FIN-CB",  "FN-CBS", "S1"),
        new("FN-ARA2", "Accounts Receivable Assistant (Cash & Banks)", "SEC-FIN-CB", "FN-CBS", "S1"),

        // ── Marketing ───────────────────────────────────────────────────────────────────────────
        new("MK-SEC", "Secretary",                   "UNIT-MKT", "HSM",    "S1"),
        new("MK-SMO", "Sales & Marketing Officer",   "UNIT-MKT", "HSM",    "M5"),
        new("MK-SMA", "Sales & Marketing Assistant", "UNIT-MKT", "MK-SMO", "S1"),

        // ── Procurement ─────────────────────────────────────────────────────────────────────────
        new("PR-SEC", "Secretary",                  "UNIT-PROC", "HPRO",   "S1"),
        new("PR-SPO", "Senior Procurement Officer", "UNIT-PROC", "HPRO",   "M4"),
        new("PR-PO",  "Procurement Officer",        "UNIT-PROC", "PR-SPO", "M5"),
        new("PR-PA",  "Procurement Assistant",      "UNIT-PROC", "PR-PO",  "S1"),

        // ── MIS ─────────────────────────────────────────────────────────────────────────────────
        new("MS-ITS", "IT Security & Installation Officer",   "UNIT-MIS", "HMIS",   "M5"),
        new("MS-ITA", "IT Security & Installation Assistant", "UNIT-MIS", "MS-ITS", "S1"),
        new("MS-DBA", "Database Administrator / DPS",         "UNIT-MIS", "HMIS",   "M4"),
        new("MS-SA",  "Systems Administrator",                "UNIT-MIS", "HMIS",   "M4"),
        new("MS-CA",  "Computer Assistant",                   "UNIT-MIS", "MS-SA",  "S1"),
        new("MS-CT",  "Computer Technician",                  "UNIT-MIS", "MS-SA",  "S1"),
        new("MS-WD",  "Website Developer",                    "UNIT-MIS", "HMIS",   "M5"),
    };
}
