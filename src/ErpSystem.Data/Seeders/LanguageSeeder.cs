using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// The language catalogue (round 3, lane C1) — Ghana's working languages first, then the ones a
/// Ghanaian employer meets in a CV. Codes already present are left exactly as they are, so a
/// tenant's rename or retirement survives a re-run. Runs under <c>seed-hr-all</c>.
/// </summary>
public class LanguageSeeder
{
    public const string ProbeCode = "EN";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<LanguageSeeder> _logger;

    public LanguageSeeder(ApplicationDbContext context, ILogger<LanguageSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId, CancellationToken ct = default)
    {
        var set = _context.Set<Language>();
        var existingCodes = await set.Where(l => l.TenantId == tenantId).Select(l => l.Code).ToListAsync(ct);
        var have = new HashSet<string>(existingCodes.Where(c => c != null)!, StringComparer.OrdinalIgnoreCase);
        var rows = Rows(tenantId).Where(r => !have.Contains(r.Code!)).ToList();
        if (rows.Count == 0)
        {
            _logger.LogInformation("Languages already seeded for tenant {TenantId}. Skipping.", tenantId);
            return;
        }
        await set.AddRangeAsync(rows, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} languages for tenant {TenantId}.", rows.Count, tenantId);
    }

    private static List<Language> Rows(Guid tenantId)
    {
        var rows = new List<Language>();
        var order = 0;
        void Add(string code, string name, string? description = null)
            => rows.Add(new Language { TenantId = tenantId, Code = code, Name = name, Description = description, SortOrder = order += 10, IsActive = true, CreatedBy = "System" });

        // Ghana — official and the major working languages
        Add("EN", "English", "Official language");
        Add("AK", "Akan (Twi)", "Asante and Akuapem Twi");
        Add("FAT", "Fante");
        Add("GAA", "Ga");
        Add("EE", "Ewe");
        Add("DAG", "Dagbani");
        Add("DGA", "Dagaare");
        Add("GUR", "Frafra (Gurene)");
        Add("HA", "Hausa");
        Add("NZI", "Nzema");
        Add("GJN", "Gonja");
        Add("XSM", "Kasem");
        Add("MAW", "Mampruli");
        // Regional and international
        Add("FR", "French");
        Add("AR", "Arabic");
        Add("ES", "Spanish");
        Add("PT", "Portuguese");
        Add("DE", "German");
        Add("ZH", "Mandarin Chinese");
        Add("HI", "Hindi");
        Add("SW", "Swahili");
        Add("YO", "Yoruba");
        Add("IG", "Igbo");
        return rows;
    }
}
