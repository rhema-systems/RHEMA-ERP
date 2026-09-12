using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the three doorless HR reference tables for the <b>DEFAULT tenant</b>: contract types,
/// divisions and work stations.
///
/// <para><b>Why a seeder and not a scenario.</b> These three have a <c>DbSet</c>, a table and a
/// migration, and nothing else — no repository, no service, no controller, no route in
/// <c>fe_calls.json</c>. There is literally no door to post through, so the demo-coverage rule
/// ("every entity a user can create holds at least one row") is satisfied here the only way it can
/// be. The one thing that would be worse than an empty table is a fabricated one, so every row
/// below is a statement about TDC that is true elsewhere in the same database.</para>
///
/// <para><b>What this deliberately does NOT seed, and why.</b> Three sibling tables were left
/// empty on purpose:</para>
/// <list type="bullet">
/// <item><description><c>Sections</c> and <c>Units</c> — both require a <c>DepartmentId</c>, and
/// the <c>Departments</c> table in this database holds the estate/land-acquisition module's own six
/// fixtures (LA-EXE, LA-LEG, LA-SUR, LA-FIN, LA-EST, LA-REG), not TDC's departments. TDC's real
/// sections and units are <c>OrganizationUnits</c> rows (SEC-*, UNIT-*) under the 41-unit tree the
/// organogram draws. Hanging a second, parallel section tree off another module's fixtures would
/// put two different answers to "what are TDC's sections?" in one database.</description></item>
/// <item><description><c>OrganizationChartNodes</c> — <c>OrganogramController</c> builds every one
/// of its five dimensions live from units, positions, people and locations. A stored node table is
/// a second copy of the chart that nothing updates, and it would be wrong the first time anybody
/// moved a unit.</description></item>
/// </list>
///
/// <para>Idempotent per table: each block is skipped when its table already holds a live row for
/// the tenant, so a run that added one block and then failed can be repeated.</para>
/// </summary>
public class TdcDemoLegacyOrgSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoLegacyOrgSeeder> _logger;

    private const string By = "TdcDemoLegacyOrgSeeder";

    public TdcDemoLegacyOrgSeeder(ApplicationDbContext context, ILogger<TdcDemoLegacyOrgSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed contract types, divisions or work stations.");
            return;
        }

        var tenantId = tenant.Id;

        // Foreign keys are resolved by querying, never hardcoded: staff numbers and the ISO country
        // code are stable across rebuilds, the GUIDs behind them are not.
        var employees = await _context.Set<Employee>().IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.EmployeeNumber.StartsWith("TDC/"))
            .Select(e => new { e.Id, e.EmployeeNumber })
            .ToListAsync(ct);
        var byNumber = employees.ToDictionary(e => e.EmployeeNumber, e => e.Id);

        Guid? Staff(string number) => byNumber.TryGetValue(number, out var id) ? (Guid?)id : null;

        var ghana = await _context.Set<Country>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted
                                   && (c.Code == "GHA" || c.Alpha2Code == "GH" || c.Name == "Ghana"), ct);

        await SeedContractTypesAsync(tenantId, ct);
        await SeedDivisionsAsync(tenantId, Staff, ct);

        if (ghana is null)
        {
            _logger.LogWarning("Ghana not found in Countries — work stations skipped (CountryId is required).");
        }
        else
        {
            await SeedWorkStationsAsync(tenantId, ghana.Id, Staff, ct);
        }

        await _context.SaveChangesAsync(ct);
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Contract types — the vocabulary the appointment letter picks from
    // ────────────────────────────────────────────────────────────────────────────────────────────
    //
    // ⚠ Distinct from the EmploymentType ENUM carried on EmployeeContractDetail. The enum is the
    // system's fixed set; this table is TDC's own list, with the duration each kind of engagement
    // normally runs for. Duration is in MONTHS and zero means open-ended.

    private async Task SeedContractTypesAsync(Guid tenantId, CancellationToken ct)
    {
        if (await _context.Set<EmployeeContractType>().IgnoreQueryFilters()
                .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct))
        {
            _logger.LogInformation("Employee contract types already present for the DEFAULT tenant. Skipping.");
            return;
        }

        var now = DateTime.UtcNow;

        var types = new (string Code, string Name, string Description, int Duration)[]
        {
            ("PERM", "Permanent",
                "Permanent and pensionable appointment on the TDC Conditions of Service, open-ended and subject to the retiring age of sixty.", 0),
            ("CONT", "Contract",
                "Fixed-term contract appointment, renewable on the recommendation of the head of department.", 24),
            ("FIXED", "Fixed Term",
                "Engagement for a defined project or period, ending on the stated date without further notice.", 12),
            ("NSS", "National Service",
                "National service personnel posted to the Corporation for the statutory service year.", 12),
            ("INTERN", "Internship",
                "Student attachment or graduate internship, unpaid or on a stipend outside the payroll run.", 3),
            ("CASUAL", "Casual",
                "Day-rated engagement for short-term operational work, under Labour Act 651 Part V.", 1),
            ("CONSULT", "Consultancy",
                "Professional services engagement paid against invoices rather than through the payroll run.", 6),
        };

        foreach (var (code, name, description, duration) in types)
        {
            _context.Set<EmployeeContractType>().Add(new EmployeeContractType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                Description = description,
                Duration = duration,
                CreatedAt = now,
                CreatedBy = By,
            });
        }

        _logger.LogInformation("Seeded {Count} employee contract types.", types.Length);
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Divisions — the two directorates above the departments
    // ────────────────────────────────────────────────────────────────────────────────────────────
    //
    // The same two directorates the organogram already shows as OrganizationUnits DIR-FA and
    // DIR-OPS, headed by the two General Managers. Department.DivisionId is left alone: the six
    // Departments rows belong to the estate module's fixtures, and pointing them at these would be
    // asserting a reporting line that module never claimed.

    private async Task SeedDivisionsAsync(Guid tenantId, Func<string, Guid?> staff, CancellationToken ct)
    {
        if (await _context.Set<Division>().IgnoreQueryFilters()
                .AnyAsync(d => d.TenantId == tenantId && !d.IsDeleted, ct))
        {
            _logger.LogInformation("Divisions already present for the DEFAULT tenant. Skipping.");
            return;
        }

        var now = DateTime.UtcNow;

        var divisions = new (string Code, string Name, string AccountCode, string Description, string HeadNumber)[]
        {
            ("DIR-FA", "Finance & Administration Directorate", "2000",
                "Finance, HR and Administration, MIS, Procurement and the Task Force.", "TDC/00002"),
            ("DIR-OPS", "Operations Directorate", "3000",
                "Development, Estates, Development Control and Corporate Planning & Communications.", "TDC/00003"),
        };

        foreach (var (code, name, accountCode, description, headNumber) in divisions)
        {
            _context.Set<Division>().Add(new Division
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                AccountCode = accountCode,
                Description = description,
                DivisionHeadId = staff(headNumber),
                IsActive = true,
                CreatedAt = now,
                CreatedBy = By,
            });
        }

        _logger.LogInformation("Seeded {Count} divisions.", divisions.Length);
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Work stations — the physical places people report to
    // ────────────────────────────────────────────────────────────────────────────────────────────
    //
    // The same five sites as the Locations tree, with the officer who actually sits at each. They
    // are not duplicates in the harmful sense: Location is the geography a person is POSTED to and
    // carries the attendance geofence; WorkStation is the building, its telephone and the person
    // who answers it.

    private async Task SeedWorkStationsAsync(Guid tenantId, Guid ghanaId, Func<string, Guid?> staff, CancellationToken ct)
    {
        if (await _context.Set<WorkStation>().IgnoreQueryFilters()
                .AnyAsync(w => w.TenantId == tenantId && !w.IsDeleted, ct))
        {
            _logger.LogInformation("Work stations already present for the DEFAULT tenant. Skipping.");
            return;
        }

        var now = DateTime.UtcNow;

        var stations = new (string Code, string Name, string Description, string Location, string Digital, string Phone, string Email, string ContactNumber)[]
        {
            ("WS-HQ", "Tema Head Office",
                "The Corporation's head office: executive, finance, HR and administration, MIS and procurement.",
                "TDC House, Community 1, Tema", "GT-081-0001", "+233303202401", "headoffice@tdc.com.gh", "TDC/00009"),
            ("WS-C2", "Community 2 Estate Office",
                "Estate office for the Community 2 (Towers) housing stock.",
                "Towers Block, Community 2, Tema", "GT-082-0114", "+233303202402", "community2@tdc.com.gh", "TDC/00052"),
            ("WS-ASH", "Ashaiman Market Office",
                "Development-control and revenue office at the Ashaiman market.",
                "Ashaiman Main Market, Ashaiman", "GA-115-0220", "+233303202403", "ashaiman@tdc.com.gh", "TDC/00058"),
            ("WS-SEB", "Sebrepor Lands Office",
                "Lands and site office for the Sebrepor development area.",
                "Sebrepor Site Office, Sebrepor", "GS-021-4410", "+233303202404", "sebrepor@tdc.com.gh", "TDC/00076"),
            ("WS-HO", "Ho Project Office",
                "Project office for the Ho (Oxygen City) development in the Volta Region.",
                "Oxygen City Site Office, Ho", "VH-001-8802", "+233303202405", "ho@tdc.com.gh", "TDC/00053"),
        };

        foreach (var (code, name, description, location, digital, phone, email, contactNumber) in stations)
        {
            _context.Set<WorkStation>().Add(new WorkStation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                Description = description,
                Location = location,
                DigitalAddress = digital,
                CountryId = ghanaId,
                Phone = phone,
                Email = email,
                ContactPersonId = staff(contactNumber),
                IsActive = true,
                CreatedAt = now,
                CreatedBy = By,
            });
        }

        _logger.LogInformation("Seeded {Count} work stations.", stations.Length);
    }
}
