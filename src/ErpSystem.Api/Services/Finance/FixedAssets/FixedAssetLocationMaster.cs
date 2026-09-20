using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

/// <summary>
/// Finance-owned, read-only adapter over the HR/Payroll organization location master.
/// The adapter keeps Finance authorization and tenant isolation at the Finance boundary.
/// </summary>
internal static class FixedAssetLocationMaster
{
    public static async Task<IReadOnlyList<FixedAssetLocationOptionDto>> GetOptionsAsync(
        ApplicationDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var locations = await context.Locations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(location => location.LocationLevel)
            .Where(location => location.TenantId == tenantId &&
                location.IsActive && !location.IsDeleted &&
                location.LocationLevel.TenantId == tenantId &&
                location.LocationLevel.IsActive && !location.LocationLevel.IsDeleted &&
                location.LocationLevel.Structure.TenantId == tenantId &&
                location.LocationLevel.Structure.IsActive && !location.LocationLevel.Structure.IsDeleted)
            .OrderBy(location => location.Sequence)
            .ThenBy(location => location.Name)
            .ToListAsync(cancellationToken);

        var byId = locations.ToDictionary(location => location.Id);
        var parentIds = locations
            .Where(location => location.ParentLocationId.HasValue)
            .Select(location => location.ParentLocationId!.Value)
            .ToHashSet();

        return locations
            .Select(location => new FixedAssetLocationOptionDto
            {
                Id = location.Id,
                LocationLevelId = location.LocationLevelId,
                Code = location.Code,
                Name = location.Name,
                DisplayName = BuildDisplayName(location, byId),
                LevelName = location.LocationLevel?.Name,
                ParentLocationId = location.ParentLocationId,
                IsLeaf = !parentIds.Contains(location.Id)
            })
            .OrderBy(option => option.DisplayName)
            .ToList();
    }

    private static string BuildDisplayName(Location location, IReadOnlyDictionary<Guid, Location> byId)
    {
        var names = new List<string>();
        var visited = new HashSet<Guid>();
        Location? current = location;

        while (current != null && visited.Add(current.Id))
        {
            names.Add(current.Name);
            current = current.ParentLocationId.HasValue &&
                byId.TryGetValue(current.ParentLocationId.Value, out var parent)
                    ? parent
                    : null;
        }

        names.Reverse();
        var display = string.Join(" / ", names);
        return display.Length <= 500 ? display : display[..500];
    }
}
