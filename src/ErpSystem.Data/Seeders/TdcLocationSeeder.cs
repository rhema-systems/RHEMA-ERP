using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds TDC's duty-station structure into the <b>DEFAULT tenant</b>.
///
/// Sites are taken verbatim from the client's HR questionnaire (§3, Organization Structure):
/// <i>"Headquartered in Tema, with sub offices at Lebanon, Ashaiman Market, Sebrepor, Community 24,
/// Community 26, Community 2 (Towers) and HO (Oxygen City)."</i>
///
/// These are <b>work locations</b> — where staff are posted and clock in — not TDC's land or property
/// portfolio. <see cref="Location"/> feeds attendance/geofencing, hence the address and coordinate
/// fields on the site level.
///
/// Street addresses and Ghana Post digital addresses were not supplied and are left null rather
/// than invented. Coordinates ARE set (since 2026-09-03): they are approximate town-centre pins for
/// each site, good enough for the map and for a soft-enforced attendance zone, and they must be
/// confirmed on site before any zone is switched to hard enforcement. A location's pin can be moved
/// on its edit screen without touching this seeder.
///
/// SAFE BY CONSTRUCTION: runs only when invoked explicitly; idempotent on the "TDC-LOC" structure.
/// On a database seeded before the coordinates existed, it backfills the pins and nothing else.
/// </summary>
public class TdcLocationSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcLocationSeeder> _logger;

    public TdcLocationSeeder(ApplicationDbContext context, ILogger<TdcLocationSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT");
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed TDC locations.");
            return;
        }

        var tenantId = tenant.Id;

        if (await _context.Set<LocationStructure>().AnyAsync(s => s.Code == "TDC-LOC" && s.TenantId == tenantId))
        {
            await BackfillCoordinatesAsync(tenantId);
            _logger.LogInformation("TDC location structure already seeded for the DEFAULT tenant. Skipping.");
            return;
        }

        var now = DateTime.UtcNow;
        const string by = "TdcLocationSeeder";

        // Link to Ghana if the country reference data has been seeded; harmless when it has not.
        var ghanaId = await _context.Set<Country>()
            .Where(c => c.Code == "GHA" || c.Name == "Ghana")
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        // ── 1) Structure ────────────────────────────────────────────────────────────────────────
        var structure = new LocationStructure
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "TDC Locations",
            Code = "TDC-LOC",
            IsDefault = true,
            IsActive = true,
            Description = "Tema Development Corporation work locations (head office and sub offices).",
            CreatedAt = now,
            CreatedBy = by
        };
        _context.Add(structure);

        // ── 2) Levels ───────────────────────────────────────────────────────────────────────────
        // Only the site level takes an address, a contact and staff postings — country and region
        // are purely for grouping and reporting.
        var levelDefs = new (string Code, string Name, int Number, bool IsSite)[]
        {
            ("CTRY", "Country",      1, false),
            ("RGN",  "Region",       2, false),
            ("SITE", "Site / Office", 3, true),
        };

        var levelIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name, number, isSite) in levelDefs)
        {
            var level = new LocationLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StructureId = structure.Id,
                Name = name,
                Code = code,
                LevelNumber = number,
                RequiresAddress = isSite,
                RequiresContactInfo = isSite,
                AllowsEmployeeAssignment = isSite,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = by
            };
            levelIdByCode[code] = level.Id;
            _context.Add(level);
        }

        // ── 3) Locations ────────────────────────────────────────────────────────────────────────
        var locationIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in LocationDefs)
        {
            var id = Guid.NewGuid();
            locationIdByCode[def.Code] = id;

            Guid? parentId = null;
            if (def.ParentCode is not null)
            {
                if (!locationIdByCode.TryGetValue(def.ParentCode, out var pid))
                    throw new InvalidOperationException(
                        $"TDC location seeder: '{def.Code}' references unknown parent '{def.ParentCode}'. Define parents first.");
                parentId = pid;
            }

            _context.Add(new Location
            {
                Id = id,
                TenantId = tenantId,
                StructureId = structure.Id,
                LocationLevelId = levelIdByCode[def.LevelCode],
                ParentLocationId = parentId,
                Name = def.Name,
                Code = def.Code,
                Description = def.Description,
                City = def.City,
                // Only set on the country node; sites inherit their country through the hierarchy.
                CountryId = def.LevelCode == "CTRY" ? ghanaId : null,
                Latitude = def.Latitude,
                Longitude = def.Longitude,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = by
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation(
            "TDC locations seeded into the DEFAULT tenant: {Levels} levels, {Locations} locations.",
            levelDefs.Length, LocationDefs.Length);
    }

    /// <summary>
    /// Sites seeded before 2026-09-03 have no pin. Sets the seeder's coordinates on any site that
    /// still has none, and leaves a pin that someone has since placed by hand alone.
    /// </summary>
    private async Task BackfillCoordinatesAsync(Guid tenantId)
    {
        var pinned = LocationDefs.Where(d => d.Latitude.HasValue).ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);
        var codes = pinned.Keys.ToList();

        var unpinned = await _context.Set<Location>()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.Latitude == null && codes.Contains(l.Code))
            .ToListAsync();

        if (unpinned.Count == 0) return;

        foreach (var location in unpinned)
        {
            var def = pinned[location.Code];
            location.Latitude = def.Latitude;
            location.Longitude = def.Longitude;
            location.UpdatedAt = DateTime.UtcNow;
            location.UpdatedBy = "TdcLocationSeeder";
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("TDC locations: backfilled coordinates on {Count} site(s).", unpinned.Count);
    }

    private sealed record LocationDef(string Code, string Name, string? ParentCode, string LevelCode,
                                      string? City = null, string? Description = null,
                                      double? Latitude = null, double? Longitude = null);

    /// <summary>
    /// Ghana → region → site. Parents must precede their children. Coordinates are approximate
    /// town-centre pins (WGS-84, to four decimals ≈ 10 m), not surveyed positions.
    /// </summary>
    private static readonly LocationDef[] LocationDefs =
    {
        new("GH", "Ghana", null, "CTRY"),

        // Greater Accra — the head office and the Tema-area sub offices.
        new("GH-GA", "Greater Accra Region", "GH", "RGN"),
        new("TEMA-HQ",  "Tema Head Office",       "GH-GA", "SITE", "Tema", "Corporate head office.", 5.6698, -0.0166),
        new("LEBANON",  "Lebanon",                "GH-GA", "SITE", null,   "Sub office.",            5.6570, -0.0080),
        new("ASHAIMAN", "Ashaiman Market",        "GH-GA", "SITE", null,   "Sub office.",            5.6948, -0.0367),
        new("SEBREPOR", "Sebrepor",               "GH-GA", "SITE", null,   "Sub office.",            5.7070,  0.0180),
        new("COMM2",    "Community 2 (Towers)",   "GH-GA", "SITE", "Tema", "Sub office.",            5.6733, -0.0075),
        new("COMM24",   "Community 24",           "GH-GA", "SITE", "Tema", "Sub office.",            5.7215, -0.0405),
        new("COMM26",   "Community 26",           "GH-GA", "SITE", "Tema", "Sub office.",            5.7000,  0.0130),

        // Volta — "HO (Oxygen City)" in the questionnaire is Ho, the Volta regional capital.
        new("GH-VR", "Volta Region", "GH", "RGN"),
        new("HO", "Ho (Oxygen City)", "GH-VR", "SITE", "Ho", "Regional sub office.", 6.6013, 0.4713),
    };
}
