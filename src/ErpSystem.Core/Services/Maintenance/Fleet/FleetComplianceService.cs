using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public class FleetComplianceService : IFleetComplianceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IMaintenanceSettingsRepository _maintenanceSettingsRepository;

    public FleetComplianceService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IMaintenanceSettingsRepository maintenanceSettingsRepository)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _maintenanceSettingsRepository = maintenanceSettingsRepository;
    }

    public async Task<PagedResult<FleetComplianceItemDto>> GetComplianceItemsPagedAsync(Guid vehicleAssetId, int page, int pageSize)
    {
        if (vehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetComplianceItem>();

        var q = repo.GetQueryable(i => i.TenantId == tenantId && i.VehicleAssetId == vehicleAssetId)
            .Include(i => i.VehicleAsset);

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(i => i.ExpiryDate ?? DateTime.MaxValue)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new FleetComplianceItemDto
            {
                Id = i.Id,
                VehicleAssetId = i.VehicleAssetId,
                VehicleName = i.VehicleAsset != null ? i.VehicleAsset.Name : string.Empty,
                ComplianceType = i.ComplianceType,
                ReferenceNumber = i.ReferenceNumber,
                IssueDate = i.IssueDate,
                ExpiryDate = i.ExpiryDate,
                IsCritical = i.IsCritical,
                Notes = i.Notes,
                DocumentLinks = i.DocumentLinks,
                LastDueSoonReminderSentAt = i.LastDueSoonReminderSentAt,
                LastOverdueReminderSentAt = i.LastOverdueReminderSentAt,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<FleetComplianceItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FleetComplianceItemDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetComplianceItem>();

        var item = await repo.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, i => i.VehicleAsset);
        return item == null
            ? null
            : new FleetComplianceItemDto
            {
                Id = item.Id,
                VehicleAssetId = item.VehicleAssetId,
                VehicleName = item.VehicleAsset != null ? item.VehicleAsset.Name : string.Empty,
                ComplianceType = item.ComplianceType,
                ReferenceNumber = item.ReferenceNumber,
                IssueDate = item.IssueDate,
                ExpiryDate = item.ExpiryDate,
                IsCritical = item.IsCritical,
                Notes = item.Notes,
                DocumentLinks = item.DocumentLinks,
                LastDueSoonReminderSentAt = item.LastDueSoonReminderSentAt,
                LastOverdueReminderSentAt = item.LastOverdueReminderSentAt,
                CreatedAt = item.CreatedAt
            };
    }

    public async Task<FleetComplianceItemDto> CreateAsync(CreateFleetComplianceItemDto dto)
    {
        dto ??= new CreateFleetComplianceItemDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (string.IsNullOrWhiteSpace(dto.ComplianceType)) throw new ArgumentException("ComplianceType is required.");

        var tenantId = _currentUserProvider.TenantId;
        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.Id == dto.VehicleAssetId && a.TenantId == tenantId, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selected asset is not a vehicle.");

        var repo = _unitOfWork.Repository<FleetComplianceItem>();

        var entity = new FleetComplianceItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            ComplianceType = dto.ComplianceType.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(dto.ReferenceNumber) ? null : dto.ReferenceNumber.Trim(),
            IssueDate = dto.IssueDate,
            ExpiryDate = dto.ExpiryDate,
            IsCritical = dto.IsCritical,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            DocumentLinks = string.IsNullOrWhiteSpace(dto.DocumentLinks) ? null : dto.DocumentLinks.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetComplianceItemDto> UpdateAsync(Guid id, UpdateFleetComplianceItemDto dto)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.");
        dto ??= new UpdateFleetComplianceItemDto();

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetComplianceItem>();

        var entity = await repo.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId)
            ?? throw new ArgumentException($"Compliance item with ID {id} not found.");

        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");

        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.Id == dto.VehicleAssetId && a.TenantId == tenantId, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selected asset is not a vehicle.");

        entity.VehicleAssetId = dto.VehicleAssetId;
        entity.ComplianceType = (dto.ComplianceType ?? string.Empty).Trim();
        entity.ReferenceNumber = string.IsNullOrWhiteSpace(dto.ReferenceNumber) ? null : dto.ReferenceNumber.Trim();
        entity.IssueDate = dto.IssueDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.IsCritical = dto.IsCritical;
        entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        entity.DocumentLinks = string.IsNullOrWhiteSpace(dto.DocumentLinks) ? null : dto.DocumentLinks.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty) return false;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetComplianceItem>();

        var entity = await repo.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<FleetComplianceItemDto>> GetDispatchBlockingItemsAsync(Guid vehicleAssetId, DateTime? asAtUtc = null)
    {
        if (vehicleAssetId == Guid.Empty) return Array.Empty<FleetComplianceItemDto>();

        var now = (asAtUtc ?? DateTime.UtcNow).ToUniversalTime();
        var tenantId = _currentUserProvider.TenantId;

        var settings = await _maintenanceSettingsRepository.GetByTenantIdAsync(tenantId);
        var dueSoonDays = settings?.FleetComplianceDueSoonDays ?? 7;
        var blockDueSoon = settings?.BlockFleetDispatchWhenComplianceDueSoon ?? true;

        var dueSoonCutoff = now.Date.AddDays(Math.Max(0, dueSoonDays));

        var repo = _unitOfWork.Repository<FleetComplianceItem>();
        IQueryable<FleetComplianceItem> q = repo.GetQueryable(i => i.TenantId == tenantId && i.VehicleAssetId == vehicleAssetId && i.IsCritical)
            .Include(i => i.VehicleAsset);

        // Always block expired. Optionally block "due soon".
        if (blockDueSoon)
        {
            q = q.Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value.Date <= dueSoonCutoff);
        }
        else
        {
            q = q.Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value.Date < now.Date);
        }

        var items = await q
            .OrderBy(i => i.ExpiryDate ?? DateTime.MaxValue)
            .Select(i => new FleetComplianceItemDto
            {
                Id = i.Id,
                VehicleAssetId = i.VehicleAssetId,
                VehicleName = i.VehicleAsset != null ? i.VehicleAsset.Name : string.Empty,
                ComplianceType = i.ComplianceType,
                ReferenceNumber = i.ReferenceNumber,
                IssueDate = i.IssueDate,
                ExpiryDate = i.ExpiryDate,
                IsCritical = i.IsCritical,
                Notes = i.Notes,
                DocumentLinks = i.DocumentLinks,
                LastDueSoonReminderSentAt = i.LastDueSoonReminderSentAt,
                LastOverdueReminderSentAt = i.LastOverdueReminderSentAt,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        return items;
    }
}
