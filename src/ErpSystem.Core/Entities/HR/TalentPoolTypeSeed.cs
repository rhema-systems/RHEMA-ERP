using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// Fixed seed identities for the built-in <see cref="TalentPoolTypeDefinition"/> rows, derived
/// from the original <see cref="TalentPoolType"/> enum. Shared by the DbContext seed (HasData)
/// and the enum→FK data migration so both reference the same GUIDs. Scoped to the system
/// default tenant (…-0001).
/// </summary>
public static class TalentPoolTypeSeed
{
    public static readonly Guid DefaultTenantId = new("00000000-0000-0000-0000-000000000001");

    public sealed record SeedRow(Guid Id, TalentPoolType LegacyValue, string Code, string Name, string ColorHex, int SortOrder);

    /// <summary>The six built-in types, in display order. GUIDs are fixed constants.</summary>
    public static readonly IReadOnlyList<SeedRow> Rows = new List<SeedRow>
    {
        new(new("c3d4e5f6-0000-0000-0000-000000000001"), TalentPoolType.HighPotential,      "HighPotential",      "High Potential",      "#2563EB", 1),
        new(new("c3d4e5f6-0000-0000-0000-000000000002"), TalentPoolType.LeadershipPipeline, "LeadershipPipeline", "Leadership Pipeline", "#7C3AED", 2),
        new(new("c3d4e5f6-0000-0000-0000-000000000003"), TalentPoolType.TechnicalExperts,   "TechnicalExperts",   "Technical Experts",   "#0891B2", 3),
        new(new("c3d4e5f6-0000-0000-0000-000000000004"), TalentPoolType.Specialist,         "Specialist",         "Specialist",          "#059669", 4),
        new(new("c3d4e5f6-0000-0000-0000-000000000005"), TalentPoolType.EmergencyPool,      "EmergencyPool",      "Emergency Pool",      "#DC2626", 5),
        new(new("c3d4e5f6-0000-0000-0000-000000000006"), TalentPoolType.Other,              "Other",              "Other",               "#6B7280", 6),
    };

    /// <summary>Maps a legacy enum value to its seeded definition GUID (for data migration).</summary>
    public static Guid IdFor(TalentPoolType legacy) =>
        Rows.First(r => r.LegacyValue == legacy).Id;

    /// <summary>The GUID used as a safe default when a legacy value is missing/unknown.</summary>
    public static Guid HighPotentialId => IdFor(TalentPoolType.HighPotential);
}
