using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// The starter relationship vocabulary — the fourteen familial ties the dependant enum already
/// names, plus the professional and other ones a referee, guarantor or next of kin actually is.
/// </summary>
/// <remarks>
/// <para>Round 2, lane D2 (plan § 6.9). ⚠ A lookup that ships empty is a lookup nobody uses: the
/// four screens would offer an empty dropdown beside the free-text box they were meant to replace,
/// and everyone would keep typing. The list below is deliberately opinionated and complete enough
/// to be usable on day one; a tenant edits, retires and adds to it from the setup screen.</para>
///
/// <para><b>⚠ The familial rows carry <c>MapsToDependentRelationship</c>.</b> They are the SAME
/// fourteen ties <c>DependentRelationship</c> holds, and the mapping is what lets a screen line the
/// two up. It is a read-only bridge — nothing derives a dependant's enum from a catalogue row, and
/// the dependant screen still writes the enum directly.</para>
///
/// <para><b>The skip probe asks for a row THIS seed creates</b>, not for a non-empty table. A
/// tenant that had already added one relationship of its own would otherwise silently never
/// receive the starter list — the trap the job-architecture step records in the orchestrator.</para>
/// </remarks>
public class RelationshipTypeSeeder
{
    /// <summary>The row the orchestrator's skip probe looks for.</summary>
    public const string ProbeCode = "SPOUSE";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<RelationshipTypeSeeder> _logger;

    public RelationshipTypeSeeder(ApplicationDbContext context, ILogger<RelationshipTypeSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId, CancellationToken ct = default)
    {
        var set = _context.Set<RelationshipType>();

        // Codes already present are left exactly as they are — a tenant may have renamed "Wife" to
        // "Spouse" or retired one, and re-running the seed must not undo that.
        var existingCodes = await set
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.Code)
            .ToListAsync(ct);
        var have = new HashSet<string>(
            existingCodes.Where(c => c != null)!, StringComparer.OrdinalIgnoreCase);

        var rows = Rows(tenantId).Where(r => !have.Contains(r.Code!)).ToList();
        if (rows.Count == 0)
        {
            _logger.LogInformation("Relationship types already seeded for tenant {TenantId}. Skipping.", tenantId);
            return;
        }

        await set.AddRangeAsync(rows, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded {Count} relationship types for tenant {TenantId}.", rows.Count, tenantId);
    }

    private static List<RelationshipType> Rows(Guid tenantId)
    {
        var rows = new List<RelationshipType>();
        var order = 0;

        void Add(string code, string name, RelationshipCategory category,
                 DependentRelationship? maps = null, string? description = null)
            => rows.Add(new RelationshipType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                Category = category,
                MapsToDependentRelationship = maps,
                Description = description,
                SortOrder = ++order,
                IsActive = true,
            });

        // ── Familial: the fourteen the dependant enum already names ──────────────────────────
        Add("SPOUSE", "Spouse", RelationshipCategory.Familial, DependentRelationship.Spouse);
        Add("SON", "Son", RelationshipCategory.Familial, DependentRelationship.Son);
        Add("DAUGHTER", "Daughter", RelationshipCategory.Familial, DependentRelationship.Daughter);
        Add("MOTHER", "Mother", RelationshipCategory.Familial, DependentRelationship.Mother);
        Add("FATHER", "Father", RelationshipCategory.Familial, DependentRelationship.Father);
        Add("BROTHER", "Brother", RelationshipCategory.Familial, DependentRelationship.Brother);
        Add("SISTER", "Sister", RelationshipCategory.Familial, DependentRelationship.Sister);
        Add("UNCLE", "Uncle", RelationshipCategory.Familial, DependentRelationship.Uncle);
        Add("AUNT", "Aunt", RelationshipCategory.Familial, DependentRelationship.Aunt);
        Add("NEPHEW", "Nephew", RelationshipCategory.Familial, DependentRelationship.Nephew);
        Add("NIECE", "Niece", RelationshipCategory.Familial, DependentRelationship.Niece);
        Add("GRANDFATHER", "Grandfather", RelationshipCategory.Familial, DependentRelationship.Grandfather);
        Add("GRANDMOTHER", "Grandmother", RelationshipCategory.Familial, DependentRelationship.Grandmother);
        Add("OTHER_RELATIVE", "Other relative", RelationshipCategory.Familial, DependentRelationship.Other,
            "A blood or marital tie the list above does not name — cousin, in-law, ward.");

        // ── Professional: who a referee actually is ──────────────────────────────────────────
        Add("FORMER_MANAGER", "Former manager", RelationshipCategory.Professional,
            description: "Supervised the person at a previous employer. The commonest professional referee.");
        Add("CURRENT_MANAGER", "Current manager", RelationshipCategory.Professional);
        Add("COLLEAGUE", "Colleague", RelationshipCategory.Professional);
        Add("SUBORDINATE", "Subordinate", RelationshipCategory.Professional);
        Add("CLIENT", "Client", RelationshipCategory.Professional);
        Add("BUSINESS_PARTNER", "Business partner", RelationshipCategory.Professional);
        Add("ACADEMIC_SUPERVISOR", "Academic supervisor", RelationshipCategory.Professional,
            description: "Supervised a thesis or a project. The academic referee's usual tie.");
        Add("LECTURER", "Lecturer", RelationshipCategory.Professional);

        // ── Other: neither blood nor work, and both the next-of-kin and referee screens take them ──
        Add("PASTOR", "Pastor / religious leader", RelationshipCategory.Other);
        Add("COMMUNITY_LEADER", "Community leader", RelationshipCategory.Other,
            description: "A chief, an assemblyman, an opinion leader — often the referee a rural applicant has.");
        Add("FAMILY_FRIEND", "Family friend", RelationshipCategory.Other);
        Add("FRIEND", "Friend", RelationshipCategory.Other);
        Add("LANDLORD", "Landlord", RelationshipCategory.Other);

        return rows;
    }
}
