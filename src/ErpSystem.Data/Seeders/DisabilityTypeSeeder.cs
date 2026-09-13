using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// The disability catalogue a tenant starts from (round 3, lane P2; register row E-5): the
/// groupings Ghana's population census and Persons with Disability Act (Act 715) practice use,
/// spelt the way an HR officer would pick them. Idempotent by code; a renamed or retired row is
/// left exactly as the tenant left it.
/// </summary>
public class DisabilityTypeSeeder
{
    public const string ProbeCode = "VISUAL";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<DisabilityTypeSeeder> _logger;

    public DisabilityTypeSeeder(ApplicationDbContext context, ILogger<DisabilityTypeSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId, CancellationToken ct = default)
    {
        var set = _context.Set<DisabilityType>();
        var existingCodes = await set.Where(t => t.TenantId == tenantId).Select(t => t.Code).ToListAsync(ct);
        var have = new HashSet<string>(existingCodes.Where(c => c != null)!, StringComparer.OrdinalIgnoreCase);

        var rows = Rows(tenantId).Where(r => !have.Contains(r.Code!)).ToList();
        if (rows.Count == 0)
        {
            _logger.LogInformation("Disability types already seeded for tenant {TenantId}. Skipping.", tenantId);
            return;
        }
        await set.AddRangeAsync(rows, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} disability types for tenant {TenantId}.", rows.Count, tenantId);
    }

    private static List<DisabilityType> Rows(Guid tenantId)
    {
        var rows = new List<DisabilityType>();
        var order = 0;
        void Add(string code, string name, DisabilityCategory category, string? description = null)
            => rows.Add(new DisabilityType
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = name, Category = category,
                Description = description, SortOrder = ++order, IsActive = true,
            });

        Add("VISUAL", "Visual impairment", DisabilityCategory.Visual, "Blindness or low vision not corrected by glasses.");
        Add("HEARING", "Hearing impairment", DisabilityCategory.Hearing, "Deafness or hard of hearing.");
        Add("SPEECH", "Speech impairment", DisabilityCategory.Speech);
        Add("PHYSICAL", "Physical / mobility impairment", DisabilityCategory.Physical, "Difficulty walking, climbing or using the hands.");
        Add("AMPUTATION", "Amputation / limb loss", DisabilityCategory.Physical);
        Add("SPINAL", "Spinal cord injury", DisabilityCategory.Physical);
        Add("INTELLECTUAL", "Intellectual disability", DisabilityCategory.Intellectual);
        Add("LEARNING", "Specific learning disability", DisabilityCategory.Intellectual, "Dyslexia, dyscalculia and the like.");
        Add("PSYCHOSOCIAL", "Psychosocial / mental health condition", DisabilityCategory.Psychosocial);
        Add("AUTISM", "Autism spectrum", DisabilityCategory.Neurological);
        Add("EPILEPSY", "Epilepsy", DisabilityCategory.Neurological);
        Add("CEREBRAL_PALSY", "Cerebral palsy", DisabilityCategory.Neurological);
        Add("CHRONIC", "Chronic health condition", DisabilityCategory.ChronicHealth, "A long-term condition that limits work — sickle cell, kidney disease, diabetes with complications.");
        Add("ALBINISM", "Albinism", DisabilityCategory.Other);
        Add("MULTIPLE", "Multiple disabilities", DisabilityCategory.Multiple);
        Add("OTHER", "Other", DisabilityCategory.Other, "A condition the list does not name — describe it in the notes.");
        return rows;
    }
}
