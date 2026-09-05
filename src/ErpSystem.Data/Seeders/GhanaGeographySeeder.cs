using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Reference;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Ghana's administrative geography as a four-tier division scheme:
/// <b>Region → District → Town → Community</b>.
/// Phase 1 of <c>docs/GEOGRAPHY-REFERENCE-DESIGN.md</c>.
/// </summary>
/// <remarks>
/// <para><b>Four tiers, decided 2026-09-03.</b> Three would have been the textbook Ghanaian
/// hierarchy (Region → MMDA → Town), but TDC's own addresses are Tema community numbers — Community
/// 2, Community 24, Community 26 are sub-offices in <c>TdcLocationSeeder</c> — and a community is a
/// level below a town. A tier that exists in the addresses but not in the model would push those
/// back into free text, which is the thing this module was built to stop.</para>
///
/// <para><b>⚠ IDEMPOTENT ON <c>Code</c>, and that is the whole contract.</b> Every row is matched by
/// its code within the scheme, so a second run inserts nothing and a partial run completes itself.
/// This is exactly why codes must never be re-spelled once seeded: change one and the next run
/// creates a duplicate rather than recognising the row it already made.</para>
///
/// <para><b>What is authoritative here and what is not — read before extending.</b></para>
/// <list type="bullet">
///   <item><description><b>Regions: authoritative.</b> All 16, coded with their ISO 3166-2:GH
///   subdivision codes. Brong Ahafo is seeded too, end-dated and pointed at Bono — see below.</description></item>
///   <item><description><b>Districts: names authoritative, codes derived.</b> The 29 Greater Accra
///   MMDAs plus Ho Municipal, which is where TDC's two regions of operation are. The codes are the
///   assemblies' own well-known acronyms (TMA, AMA, KKMA…) namespaced under the region, NOT Ghana
///   Statistical Service district codes — those were not to hand. If TDC supplies the GSS list,
///   reconcile by UPDATING these codes in place; do not re-seed under new ones.</description></item>
///   <item><description><b>The other 15 regions have no districts.</b> Roughly 232 MMDAs are
///   missing. They are omitted rather than guessed: this module's own rule is that an invented code
///   poisons the idempotency key, and the same discipline that left TDC's site addresses null rather
///   than inventing them applies here. Add them through Administration → Reference Data → Geography,
///   or extend <see cref="Districts"/> when the GSS list arrives.</description></item>
///   <item><description><b>Towns and communities: TDC's operating footprint only.</b> Seeded so the
///   sites in <c>TdcLocationSeeder</c> have somewhere to point in phase 4. Coordinates are reused
///   verbatim from that seeder — the pins the user already vetted — and are null everywhere
///   else rather than approximated.</description></item>
///   <item><description><b>⚠ Two community placements are marked provisional in their Notes.</b>
///   Whether Community 24 and Community 26 fall under Tema Metropolitan or Tema West, and whether
///   Sebrepor sits under Kpone, is a question for TDC. They are seeded at a best placement, flagged
///   in the record itself, and can be re-parented on the areas screen without touching this file —
///   the same treatment <c>TdcLocationSeeder</c> gave its approximate coordinates.</description></item>
/// </list>
///
/// <para><b>⚠ Brong Ahafo is the point of the effective dating, not a curiosity.</b> It was
/// dissolved in February 2019 into Bono, Bono East and Ahafo. It is seeded as a real row, end-dated
/// and superseded by Bono, so a TDC record created before 2019 still resolves to the region that
/// existed when it was written, and an old spreadsheet saying "Brong Ahafo" imports instead of
/// failing. Renaming it to Bono — the tempting shortcut — would silently relocate every pre-2019
/// record into a region that did not exist yet.</para>
/// </remarks>
public class GhanaGeographySeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GhanaGeographySeeder> _logger;

    public GhanaGeographySeeder(ApplicationDbContext context, ILogger<GhanaGeographySeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public const string SchemeCode = "GH-ADMIN";

    private const string LevelRegion = "REGION";
    private const string LevelDistrict = "DISTRICT";
    private const string LevelTown = "TOWN";
    private const string LevelCommunity = "COMMUNITY";

    /// <summary>
    /// The Constitutional Instruments creating the six new regions were signed on 12 February 2019,
    /// following the December 2018 referendum. Brong Ahafo ends the day before.
    /// </summary>
    private static readonly DateOnly NewRegionsFrom = new(2019, 2, 12);
    private static readonly DateOnly BrongAhafoUntil = new(2019, 2, 11);

    public async Task SeedAsync(Guid tenantId, CancellationToken ct = default)
    {
        var ghana = await _context.Set<Country>()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && (c.Code == "GHA" || c.Name == "Ghana"))
            .FirstOrDefaultAsync(ct);

        if (ghana is null)
        {
            _logger.LogError(
                "Ghana not found in Countries for tenant {TenantId} — run the Countries seed first. "
                + "Nothing was written.", tenantId);
            return;
        }

        var now = DateTime.UtcNow;
        const string by = nameof(GhanaGeographySeeder);

        // ── 1) Scheme ───────────────────────────────────────────────────────────────────────────
        var scheme = await _context.Set<GeoScheme>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Code == SchemeCode && !s.IsDeleted, ct);

        if (scheme is null)
        {
            scheme = new GeoScheme
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CountryId = ghana.Id,
                Name = "Ghana Administrative Divisions",
                Code = SchemeCode,
                Description = "Region → District (MMDA) → Town → Community.",
                IsDefault = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = by,
            };
            _context.Add(scheme);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Created division scheme {Code} for Ghana.", SchemeCode);
        }

        // ── 2) Tiers ────────────────────────────────────────────────────────────────────────────
        var levelByCode = await _context.Set<GeoLevel>()
            .Where(l => l.TenantId == tenantId && l.SchemeId == scheme.Id && !l.IsDeleted)
            .ToDictionaryAsync(l => l.Code, l => l, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var def in Levels)
        {
            if (levelByCode.ContainsKey(def.Code)) continue;

            var level = new GeoLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SchemeId = scheme.Id,
                Name = def.Name,
                Code = def.Code,
                Description = def.Description,
                LevelNumber = def.LevelNumber,
                IsRequiredInAddress = def.Required,
                AllowsAddressAssignment = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = by,
            };
            _context.Add(level);
            levelByCode[def.Code] = level;
        }

        await _context.SaveChangesAsync(ct);

        // ── 3) Areas ────────────────────────────────────────────────────────────────────────────
        // Loaded once and matched by code, so a partial previous run completes rather than duplicates.
        var areaByCode = await _context.Set<GeoArea>()
            .Where(a => a.TenantId == tenantId && a.SchemeId == scheme.Id && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Code, a => a, StringComparer.OrdinalIgnoreCase, ct);

        var definitions = BuildAreaDefinitions().ToList();
        var created = 0;

        // Ordered by tier so a parent always exists before the child that names it.
        foreach (var def in definitions.OrderBy(d => LevelNumberOf(d.LevelCode)))
        {
            if (areaByCode.ContainsKey(def.Code)) continue;

            if (!levelByCode.TryGetValue(def.LevelCode, out var level))
            {
                _logger.LogWarning("Tier {Level} missing — skipped area {Code}.", def.LevelCode, def.Code);
                continue;
            }

            GeoArea? parent = null;
            if (def.ParentCode is not null && !areaByCode.TryGetValue(def.ParentCode, out parent))
            {
                _logger.LogWarning(
                    "Parent {Parent} not found — skipped area {Code}.", def.ParentCode, def.Code);
                continue;
            }

            var area = new GeoArea
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SchemeId = scheme.Id,
                GeoLevelId = level.Id,
                ParentAreaId = parent?.Id,
                Name = def.Name,
                Code = def.Code,
                Latitude = def.Latitude,
                Longitude = def.Longitude,
                EffectiveFrom = def.EffectiveFrom,
                EffectiveTo = def.EffectiveTo,
                IsActive = def.EffectiveTo is null,
                Notes = def.Notes,
                CreatedAt = now,
                CreatedBy = by,
            };

            // Path needs the parent's, which is why the loop is tier-ordered rather than declaration-ordered.
            area.Path = parent is null ? $"/{area.Id}" : $"{parent.Path}/{area.Id}";

            _context.Add(area);
            areaByCode[def.Code] = area;
            created++;
        }

        await _context.SaveChangesAsync(ct);

        // ── 4) Succession links ─────────────────────────────────────────────────────────────────
        // A second pass: a successor is an area like any other, so it has to exist before it can be
        // pointed at.
        foreach (var def in definitions.Where(d => d.SupersededByCode is not null))
        {
            if (!areaByCode.TryGetValue(def.Code, out var area)) continue;
            if (area.SupersededByGeoAreaId is not null) continue;
            if (!areaByCode.TryGetValue(def.SupersededByCode!, out var successor)) continue;

            area.SupersededByGeoAreaId = successor.Id;
            area.UpdatedAt = now;
            area.UpdatedBy = by;
        }

        await _context.SaveChangesAsync(ct);

        // ── 5) Alternate names ──────────────────────────────────────────────────────────────────
        var aliasCount = 0;
        foreach (var (areaCode, alias, kind) in Aliases)
        {
            if (!areaByCode.TryGetValue(areaCode, out var area)) continue;

            var exists = await _context.Set<GeoAreaAlias>()
                .AnyAsync(x => x.TenantId == tenantId && x.GeoAreaId == area.Id
                            && x.Alias == alias && !x.IsDeleted, ct);
            if (exists) continue;

            _context.Add(new GeoAreaAlias
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GeoAreaId = area.Id,
                Alias = alias,
                Kind = kind,
                CreatedAt = now,
                CreatedBy = by,
            });
            aliasCount++;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ghana geography: {Created} area(s) and {Aliases} alternate name(s) added ({Total} areas now). "
            + "Districts outside Greater Accra and Ho are deliberately not seeded — see the class remarks.",
            created, aliasCount, areaByCode.Count);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  Tier definitions
    // ════════════════════════════════════════════════════════════════════════════════════════════

    private sealed record LevelDef(string Code, string Name, int LevelNumber, bool Required, string Description);

    private static readonly LevelDef[] Levels =
    {
        new(LevelRegion, "Region", 1, true,
            "Ghana's 16 regions. The tier almost every report groups by."),
        new(LevelDistrict, "District", 2, false,
            "Metropolitan, Municipal and District Assemblies (MMDAs) — the tier Ghanaian addresses "
            + "and statutory returns actually name."),
        new(LevelTown, "Town", 3, false,
            "Towns and localities within a district."),
        new(LevelCommunity, "Community", 4, false,
            "Numbered communities, suburbs and named neighbourhoods. Present because TDC's own "
            + "addresses are Tema community numbers."),
    };

    private static int LevelNumberOf(string levelCode) =>
        Levels.First(l => string.Equals(l.Code, levelCode, StringComparison.OrdinalIgnoreCase)).LevelNumber;

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  Area definitions
    // ════════════════════════════════════════════════════════════════════════════════════════════

    private sealed record AreaDef(
        string Code,
        string Name,
        string LevelCode,
        string? ParentCode,
        double? Latitude = null,
        double? Longitude = null,
        DateOnly? EffectiveFrom = null,
        DateOnly? EffectiveTo = null,
        string? SupersededByCode = null,
        string? Notes = null);

    private static IEnumerable<AreaDef> BuildAreaDefinitions()
    {
        foreach (var region in Regions) yield return region;
        foreach (var district in Districts) yield return district;
        foreach (var place in TownsAndCommunities) yield return place;
    }

    /// <summary>
    /// All 16 regions, coded with their ISO 3166-2:GH subdivision codes, plus the dissolved Brong
    /// Ahafo. The seven created in 2019 carry an <c>EffectiveFrom</c> so a pre-2019 record cannot
    /// silently claim one of them.
    /// </summary>
    private static readonly AreaDef[] Regions =
    {
        new("GH-AA", "Greater Accra", LevelRegion, null),
        new("GH-AH", "Ashanti", LevelRegion, null),
        new("GH-CP", "Central", LevelRegion, null),
        new("GH-EP", "Eastern", LevelRegion, null),
        new("GH-NP", "Northern", LevelRegion, null),
        new("GH-TV", "Volta", LevelRegion, null),
        new("GH-UE", "Upper East", LevelRegion, null),
        new("GH-UW", "Upper West", LevelRegion, null),
        new("GH-WP", "Western", LevelRegion, null),

        // Created 12 February 2019.
        new("GH-AF", "Ahafo", LevelRegion, null, EffectiveFrom: NewRegionsFrom),
        new("GH-BO", "Bono", LevelRegion, null, EffectiveFrom: NewRegionsFrom),
        new("GH-BE", "Bono East", LevelRegion, null, EffectiveFrom: NewRegionsFrom),
        new("GH-NE", "North East", LevelRegion, null, EffectiveFrom: NewRegionsFrom),
        new("GH-OT", "Oti", LevelRegion, null, EffectiveFrom: NewRegionsFrom),
        new("GH-SV", "Savannah", LevelRegion, null, EffectiveFrom: NewRegionsFrom),
        new("GH-WN", "Western North", LevelRegion, null, EffectiveFrom: NewRegionsFrom),

        // ⚠ Dissolved, not renamed. Kept so pre-2019 records still resolve to the region that
        // existed when they were written, and so an old spreadsheet saying "Brong Ahafo" imports.
        // Bono is named as the successor because it kept the capital, Sunyani; a record needing the
        // Bono East or Ahafo portion has to be re-stated by someone who knows which.
        new("GH-BA", "Brong Ahafo", LevelRegion, null,
            EffectiveTo: BrongAhafoUntil,
            SupersededByCode: "GH-BO",
            Notes: "Dissolved 2019 into Bono, Bono East and Ahafo. Successor recorded as Bono, which "
                 + "kept the capital — confirm per record before relying on it for the other two."),
    };

    /// <summary>
    /// Greater Accra's 29 MMDAs and Ho Municipal — TDC's two regions of operation. Codes are the
    /// assemblies' own acronyms namespaced under the region; see the class remarks on why these are
    /// not GSS codes and how to reconcile if TDC supplies them.
    /// </summary>
    private static readonly AreaDef[] Districts =
    {
        new("GH-AA-AMA", "Accra Metropolitan", LevelDistrict, "GH-AA"),
        new("GH-AA-ABCMA", "Ablekuma Central Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-ABNMA", "Ablekuma North Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-ABWMA", "Ablekuma West Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-ADEDA", "Ada East District", LevelDistrict, "GH-AA"),
        new("GH-AA-ADWDA", "Ada West District", LevelDistrict, "GH-AA"),
        new("GH-AA-ADMA", "Adentan Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-ASHMA", "Ashaiman Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-AYCMA", "Ayawaso Central Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-AYEMA", "Ayawaso East Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-AYNMA", "Ayawaso North Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-AYWMA", "Ayawaso West Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-GCMA", "Ga Central Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-GEMA", "Ga East Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-GNMA", "Ga North Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-GSMA", "Ga South Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-GWMA", "Ga West Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-KOKMA", "Korle Klottey Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-KKMA", "Kpone-Katamanso Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-KROMA", "Krowor Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-LADMA", "La Dade-Kotopon Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-LANMMA", "La Nkwantanang-Madina Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-LEKMA", "Ledzokuku Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-NPDA", "Ningo-Prampram District", LevelDistrict, "GH-AA"),
        new("GH-AA-OKNMA", "Okaikwei North Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-SODA", "Shai-Osudoku District", LevelDistrict, "GH-AA"),
        new("GH-AA-TMA", "Tema Metropolitan", LevelDistrict, "GH-AA"),
        new("GH-AA-TWMA", "Tema West Municipal", LevelDistrict, "GH-AA"),
        new("GH-AA-WEGMA", "Weija-Gbawe Municipal", LevelDistrict, "GH-AA"),

        // Volta: TDC's regional sub office is in Ho.
        new("GH-TV-HMA", "Ho Municipal", LevelDistrict, "GH-TV"),
    };

    /// <summary>
    /// TDC's operating footprint at the town and community tiers, so phase 4 has somewhere to point
    /// <c>Location.GeoAreaId</c>. Coordinates are the pins already vetted in
    /// <c>TdcLocationSeeder</c>, reused verbatim rather than re-approximated; every other place is
    /// left unpinned rather than guessed.
    /// </summary>
    private static readonly AreaDef[] TownsAndCommunities =
    {
        // ── Towns ───────────────────────────────────────────────────────────────────────────────
        new("GH-AA-TMA-TEMA", "Tema", LevelTown, "GH-AA-TMA", 5.6698, -0.0166),
        new("GH-AA-TWMA-TEMAW", "Tema West", LevelTown, "GH-AA-TWMA"),
        new("GH-AA-ASHMA-ASH", "Ashaiman", LevelTown, "GH-AA-ASHMA", 5.6948, -0.0367),
        new("GH-AA-KKMA-KPONE", "Kpone", LevelTown, "GH-AA-KKMA"),
        new("GH-AA-NPDA-PRAM", "Prampram", LevelTown, "GH-AA-NPDA"),
        new("GH-AA-AMA-ACCRA", "Accra", LevelTown, "GH-AA-AMA"),
        new("GH-TV-HMA-HO", "Ho", LevelTown, "GH-TV-HMA", 6.6013, 0.4713),

        // ── Communities ─────────────────────────────────────────────────────────────────────────
        // The four Tema communities TDC actually occupies, plus the two named sub-office
        // neighbourhoods. Nothing else in Tema's numbering is seeded — the rest would be invention.
        new("GH-AA-TMA-TEMA-C1", "Community 1", LevelCommunity, "GH-AA-TMA-TEMA"),
        new("GH-AA-TMA-TEMA-C2", "Community 2", LevelCommunity, "GH-AA-TMA-TEMA", 5.6733, -0.0075,
            Notes: "TDC sub office (Towers)."),

        // ⚠ Provisional placement. Tema's higher-numbered communities straddle Tema Metropolitan
        // and Tema West, and which assembly each falls under was not confirmed with TDC. Placed
        // under Tema West; re-parent on the areas screen once TDC confirms — no code change needed.
        new("GH-AA-TWMA-TEMAW-C24", "Community 24", LevelCommunity, "GH-AA-TWMA-TEMAW", 5.7215, -0.0405,
            Notes: "TDC sub office. ⚠ Assembly placement provisional — confirm Tema West vs Tema "
                 + "Metropolitan with TDC."),
        new("GH-AA-TWMA-TEMAW-C26", "Community 26", LevelCommunity, "GH-AA-TWMA-TEMAW", 5.7000, 0.0130,
            Notes: "TDC sub office. ⚠ Assembly placement provisional — confirm Tema West vs "
                 + "Kpone-Katamanso with TDC."),

        new("GH-AA-ASHMA-ASH-LEB", "Lebanon", LevelCommunity, "GH-AA-ASHMA-ASH", 5.6570, -0.0080,
            Notes: "TDC sub office."),

        new("GH-AA-KKMA-KPONE-SEB", "Sebrepor", LevelCommunity, "GH-AA-KKMA-KPONE", 5.7070, 0.0180,
            Notes: "TDC sub office. ⚠ Assembly placement provisional — confirm Kpone-Katamanso with TDC."),
    };

    /// <summary>
    /// Alternate names worth resolving on. Kept small and real: these are the spellings and
    /// abbreviations that actually turn up in submitted documents and spreadsheets, not a
    /// thesaurus.
    /// </summary>
    private static readonly (string AreaCode, string Alias, GeoAreaAliasKind Kind)[] Aliases =
    {
        ("GH-AA", "GAR", GeoAreaAliasKind.Abbreviation),
        ("GH-AA", "Accra Region", GeoAreaAliasKind.Spelling),
        ("GH-AH", "Asante", GeoAreaAliasKind.Vernacular),
        ("GH-UE", "U/E", GeoAreaAliasKind.Abbreviation),
        ("GH-UW", "U/W", GeoAreaAliasKind.Abbreviation),
        ("GH-WN", "Western-North", GeoAreaAliasKind.Spelling),

        // The one that earns its keep: every pre-2019 TDC record and spreadsheet says this.
        ("GH-BA", "Brong-Ahafo", GeoAreaAliasKind.Spelling),
        ("GH-BA", "B/A", GeoAreaAliasKind.Abbreviation),

        ("GH-AA-TMA", "TMA", GeoAreaAliasKind.Abbreviation),
        ("GH-AA-TMA", "Tema Metropolitan Assembly", GeoAreaAliasKind.Spelling),
        ("GH-AA-AMA", "AMA", GeoAreaAliasKind.Abbreviation),
        ("GH-AA-ASHMA", "ASHMA", GeoAreaAliasKind.Abbreviation),
        ("GH-AA-KKMA", "KKMA", GeoAreaAliasKind.Abbreviation),
        // Hyphen-less is how it is typed at least as often as not.
        ("GH-AA-KKMA", "Kpone Katamanso Municipal", GeoAreaAliasKind.Spelling),
        ("GH-AA-LEKMA", "LEKMA", GeoAreaAliasKind.Abbreviation),
    };
}
