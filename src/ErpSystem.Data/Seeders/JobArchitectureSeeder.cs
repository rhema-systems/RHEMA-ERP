using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds a starter job-family / sub-family / career-level vocabulary (area 17, decision D-5).
/// </summary>
/// <remarks>
/// <para><b>Why seed at all.</b> The three tables held zero rows, and a job description's family,
/// sub-family and level are optional — so an empty vocabulary stays empty: nobody classifies a job
/// description against a dropdown with nothing in it, and the classification screens demo blank.
/// That is the <c>ExpectedHeadcount</c> shape (a field nobody maintains becomes a field nobody can
/// rely on), and this is the cheap point to avoid it.</para>
///
/// <para><b>Why only this much.</b> The families below are TDC's own functions, read off the
/// organisation units <c>TdcOrganogramSeeder</c> already creates — Architecture, Quantity Survey,
/// Development Control, Financial Accounts, Records, SHE and the rest. Nothing here is invented
/// industry taxonomy. The ladder is the eight rungs TDC's own grade structure implies, from junior
/// officer to managing director.</para>
///
/// <para>⚠ <b><see cref="CareerLevel.SalaryGradeId"/> is deliberately left null.</b> Mapping a
/// career ladder onto pay grades is a TDC decision with money attached, and payroll owns the grade
/// store. A guess here would be exactly the invented reference data that is harder to remove than
/// absent data. HR links them on the levels screen; it is one dropdown.</para>
///
/// <para>Idempotent by code: an entry whose <c>Code</c> already exists for the tenant is skipped,
/// so re-running picks up additions without duplicating anything.</para>
/// </remarks>
public class JobArchitectureSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<JobArchitectureSeeder> _logger;

    public JobArchitectureSeeder(ApplicationDbContext context, ILogger<JobArchitectureSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Family code, name, and the sub-families under it as (code, name).</summary>
    private static readonly (string Code, string Name, string Description, (string Code, string Name)[] SubFamilies)[] Families =
    {
        ("EXE", "Executive & Corporate Management",
            "Managing Director's Office, directorates and corporate planning.",
            new[] { ("EXE-MD", "Executive Office"), ("EXE-CP", "Corporate Planning"), ("EXE-COM", "Communications") }),

        ("PLN", "Planning & Development",
            "Town planning, development control and the schemes that come out of them.",
            new[] { ("PLN-TP", "Town Planning"), ("PLN-DC", "Development Control"), ("PLN-DEV", "Development") }),

        ("EST", "Estates & Lands",
            "Land administration, estates, housing and allocations.",
            new[] { ("EST-LND", "Lands"), ("EST-HSG", "Housing"), ("EST-EST", "Estates") }),

        ("ENG", "Engineering & Technical",
            "Architecture, surveying and the technical inspection of works.",
            new[] { ("ENG-ARC", "Architecture"), ("ENG-QS", "Quantity Survey"),
                    ("ENG-SUR", "Survey & Geodetic"), ("ENG-INS", "Building Inspectorate") }),

        ("FAC", "Facilities & Maintenance",
            "Building maintenance, facilities management and transport.",
            new[] { ("FAC-BLD", "Building Maintenance"), ("FAC-FM", "Facilities Management"),
                    ("FAC-TRN", "Transport") }),

        ("FIN", "Finance & Accounting",
            "Financial and management accounts, revenue, treasury.",
            new[] { ("FIN-FA", "Financial Accounts"), ("FIN-MA", "Management Accounts"),
                    ("FIN-REV", "Revenue"), ("FIN-CB", "Cash & Banks") }),

        ("HRA", "Human Resource & Administration",
            "Human resource management, general administration, records and registry.",
            new[] { ("HRA-HR", "Human Resource"), ("HRA-ADM", "Administration"),
                    ("HRA-REC", "Records & Registry") }),

        ("LEG", "Legal & Assurance",
            "Legal services, internal audit and compliance.",
            new[] { ("LEG-LEG", "Legal"), ("LEG-AUD", "Internal Audit") }),

        ("SCM", "Commercial & Supply Chain",
            "Procurement, stores and fixed assets, marketing and client relations.",
            new[] { ("SCM-PRC", "Procurement"), ("SCM-STR", "Stores & Fixed Assets"),
                    ("SCM-MKT", "Marketing & Client Relations") }),

        ("ICT", "Information & Communications Technology",
            "Management information systems and technical support.",
            new[] { ("ICT-MIS", "Management Information Systems") }),

        ("SHE", "Safety, Health & Environment",
            "Occupational safety, health and environmental management.",
            new[] { ("SHE-SHE", "Safety, Health & Environment") }),
    };

    /// <summary>The career ladder: rank 1 is the most junior.</summary>
    private static readonly (string Code, string Name, int Rank, string Description)[] Levels =
    {
        ("L1", "Junior Officer", 1, "Entry level; works to set procedures under close supervision."),
        ("L2", "Officer", 2, "Delivers defined work with routine supervision."),
        ("L3", "Senior Officer", 3, "Handles complex cases and guides junior colleagues."),
        ("L4", "Principal Officer", 4, "Owns a work area; sets how objectives are met within it."),
        ("L5", "Assistant Manager / Head of Unit", 5, "Leads a unit and its people."),
        ("L6", "Manager / Head of Department", 6, "Accountable for a department and its budget."),
        ("L7", "General Manager / Director", 7, "Accountable for a directorate."),
        ("L8", "Managing Director", 8, "Accountable to the Board for the organisation."),
    };

    public async Task SeedAsync(Guid tenantId, CancellationToken ct = default)
    {
        _logger.LogInformation("Seeding job architecture for tenant {TenantId}", tenantId);

        var existingFamilyCodes = (await _context.Set<JobFamily>()
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.Code)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newFamilies = new List<JobFamily>();
        var newSubFamilies = new List<JobSubFamily>();

        foreach (var (code, name, description, subFamilies) in Families)
        {
            if (existingFamilyCodes.Contains(code)) continue;

            var family = new JobFamily
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                Description = description,
                IsActive = true,
            };
            newFamilies.Add(family);

            foreach (var (subCode, subName) in subFamilies)
            {
                newSubFamilies.Add(new JobSubFamily
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    JobFamilyId = family.Id,
                    Code = subCode,
                    Name = subName,
                    IsActive = true,
                });
            }
        }

        var existingLevelCodes = (await _context.Set<CareerLevel>()
            .Where(l => l.TenantId == tenantId)
            .Select(l => l.Code)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newLevels = Levels
            .Where(l => !existingLevelCodes.Contains(l.Code))
            .Select(l => new CareerLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = l.Code,
                Name = l.Name,
                Rank = l.Rank,
                Description = l.Description,
                // SalaryGradeId deliberately null — see the remarks on this class.
                IsActive = true,
            })
            .ToList();

        if (newFamilies.Count == 0 && newLevels.Count == 0)
        {
            _logger.LogInformation("Job architecture already present for tenant {TenantId}. Nothing to insert.", tenantId);
            return;
        }

        if (newFamilies.Count > 0) await _context.Set<JobFamily>().AddRangeAsync(newFamilies, ct);
        if (newSubFamilies.Count > 0) await _context.Set<JobSubFamily>().AddRangeAsync(newSubFamilies, ct);
        if (newLevels.Count > 0) await _context.Set<CareerLevel>().AddRangeAsync(newLevels, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded {Families} job families, {SubFamilies} sub-families and {Levels} career levels.",
            newFamilies.Count, newSubFamilies.Count, newLevels.Count);
    }
}
