using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Shared helper for seeders that create <see cref="EmployeePosition"/> rows. The HR module port removed
/// <c>DepartmentId</c> from positions and now REQUIRES both <c>OrganizationUnitId</c> and
/// <c>OrganizationLevelId</c> (the org-structure anchoring). A seeder that leaves those unset saves
/// <see cref="System.Guid.Empty"/>, which violates the org foreign keys and fails <c>SaveChangesAsync</c>.
/// This helper guarantees a usable organization unit/level exists for a tenant so those seeders can set both.
/// </summary>
public static class SeederOrgDefaults
{
    /// <summary>
    /// Returns an existing organization unit for the tenant, or creates a minimal
    /// Structure → Level → Unit placeholder. Returns the unit id and its level id — both required by
    /// <see cref="EmployeePosition"/>. When it creates rows it persists them (so the returned ids are
    /// valid FKs) unless <paramref name="save"/> is false, in which case the caller must save before
    /// the ids are used as foreign keys.
    /// </summary>
    public static async Task<(Guid UnitId, Guid LevelId)> EnsureDefaultUnitAsync(
        ApplicationDbContext context, Guid tenantId, bool save = true, CancellationToken ct = default)
    {
        var existing = await context.Set<OrganizationUnit>()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .OrderBy(u => u.Sequence)
            .FirstOrDefaultAsync(ct);
        if (existing != null)
            return (existing.Id, existing.OrganizationLevelId);

        var structure = new OrganizationStructure
        {
            TenantId = tenantId,
            Name = "Default Structure",
            Code = "DEFAULT",
            IsDefault = true,
            IsActive = true,
            CreatedBy = "System"
        };
        var level = new OrganizationLevel
        {
            TenantId = tenantId,
            Name = "Organization",
            Code = "ORG",
            LevelNumber = 1,
            RequiresHead = false,
            AllowsDirectEmployees = true,
            IsActive = true,
            StructureId = structure.Id,
            CreatedBy = "System"
        };
        var unit = new OrganizationUnit
        {
            TenantId = tenantId,
            Name = "General",
            Code = "GEN",
            OrganizationLevelId = level.Id,
            Sequence = 1,
            Path = "/GEN",
            IsActive = true,
            CreatedBy = "System"
        };

        context.Add(structure);
        context.Add(level);
        context.Add(unit);
        if (save)
            await context.SaveChangesAsync(ct);

        return (unit.Id, level.Id);
    }
}
