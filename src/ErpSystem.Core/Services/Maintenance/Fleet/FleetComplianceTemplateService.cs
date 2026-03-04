using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetComplianceTemplateService : IFleetComplianceTemplateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetComplianceTemplateService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<IReadOnlyList<FleetComplianceTemplateDto>> GetTemplatesAsync(bool includeInactive = false)
    {
        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetComplianceTemplate>();

        var q = repo.GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted);
        if (!includeInactive) q = q.Where(t => t.IsActive);

        var templates = await q
            .OrderBy(t => t.Name)
            .Select(t => new FleetComplianceTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                IsActive = t.IsActive
            })
            .ToListAsync();

        return templates;
    }

    public async Task<FleetComplianceTemplateDto?> GetTemplateByIdAsync(Guid templateId)
    {
        if (templateId == Guid.Empty) return null;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetComplianceTemplate>();

        var template = await repo.FirstOrDefaultAsync(
            t => t.Id == templateId && t.TenantId == tenantId && !t.IsDeleted,
            t => t.Items);

        if (template == null) return null;

        return new FleetComplianceTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IsActive = template.IsActive,
            Items = template.Items
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.ComplianceType)
                .Select(i => new FleetComplianceTemplateItemDto
                {
                    Id = i.Id,
                    TemplateId = i.TemplateId,
                    ComplianceType = i.ComplianceType,
                    IsCritical = i.IsCritical,
                    SortOrder = i.SortOrder
                })
                .ToList()
        };
    }

    public async Task<FleetComplianceTemplateDto> CreateTemplateAsync(CreateFleetComplianceTemplateDto dto)
    {
        dto ??= new CreateFleetComplianceTemplateDto();
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Template name is required.");

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetComplianceTemplate>();

        var template = new FleetComplianceTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId
        };

        await repo.AddAsync(template);

        if (dto.Items != null && dto.Items.Count > 0)
        {
            var itemsRepo = _unitOfWork.Repository<FleetComplianceTemplateItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in dto.Items)
            {
                if (item == null) continue;
                var type = (item.ComplianceType ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(type)) continue;
                if (!seen.Add(type)) continue;

                await itemsRepo.AddAsync(new FleetComplianceTemplateItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TemplateId = template.Id,
                    ComplianceType = type,
                    IsCritical = item.IsCritical,
                    SortOrder = item.SortOrder,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = _currentUserProvider.UserId
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return (await GetTemplateByIdAsync(template.Id))!;
    }

    public async Task<FleetComplianceTemplateDto> UpdateTemplateAsync(Guid templateId, UpdateFleetComplianceTemplateDto dto)
    {
        if (templateId == Guid.Empty) throw new ArgumentException("TemplateId is required.");
        dto ??= new UpdateFleetComplianceTemplateDto();
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Template name is required.");

        var tenantId = _currentUserProvider.TenantId;
        var templateRepo = _unitOfWork.Repository<FleetComplianceTemplate>();
        var itemsRepo = _unitOfWork.Repository<FleetComplianceTemplateItem>();

        var template = await templateRepo.FirstOrDefaultAsync(
            t => t.Id == templateId && t.TenantId == tenantId && !t.IsDeleted,
            t => t.Items)
            ?? throw new ArgumentException("Template not found.");

        template.Name = dto.Name.Trim();
        template.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        template.IsActive = dto.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        template.LastModifiedById = _currentUserProvider.UserId;

        var desired = (dto.Items ?? new List<CreateFleetComplianceTemplateItemDto>())
            .Where(i => i != null)
            .Select(i => new
            {
                ComplianceType = (i!.ComplianceType ?? string.Empty).Trim(),
                i!.IsCritical,
                i!.SortOrder
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.ComplianceType))
            .GroupBy(i => i.ComplianceType, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var existingItems = template.Items.Where(i => !i.IsDeleted).ToList();

        foreach (var d in desired)
        {
            var existing = existingItems.FirstOrDefault(i => string.Equals(i.ComplianceType, d.ComplianceType, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.IsCritical = d.IsCritical;
                existing.SortOrder = d.SortOrder;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.LastModifiedById = _currentUserProvider.UserId;
                await itemsRepo.UpdateAsync(existing);
                continue;
            }

            await itemsRepo.AddAsync(new FleetComplianceTemplateItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TemplateId = template.Id,
                ComplianceType = d.ComplianceType,
                IsCritical = d.IsCritical,
                SortOrder = d.SortOrder,
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUserProvider.UserId
            });
        }

        // Soft-delete removed items (keep their IDs stable for already-applied compliance rows)
        foreach (var existing in existingItems)
        {
            if (desired.Any(d => string.Equals(d.ComplianceType, existing.ComplianceType, StringComparison.OrdinalIgnoreCase)))
                continue;

            existing.IsDeleted = true;
            existing.DeletedAt = DateTime.UtcNow;
            existing.DeletedBy = _currentUserProvider.UserId.ToString();
            await itemsRepo.UpdateAsync(existing);
        }

        await templateRepo.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return (await GetTemplateByIdAsync(template.Id))!;
    }

    public async Task<bool> DeleteTemplateAsync(Guid templateId)
    {
        if (templateId == Guid.Empty) return false;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetComplianceTemplate>();
        var template = await repo.FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId && !t.IsDeleted);
        if (template == null) return false;

        template.IsDeleted = true;
        template.DeletedAt = DateTime.UtcNow;
        template.DeletedBy = _currentUserProvider.UserId.ToString();

        await repo.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<FleetVehicleComplianceTemplateDto?> GetVehicleTemplateAsync(Guid vehicleAssetId)
    {
        if (vehicleAssetId == Guid.Empty) return null;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetVehicleComplianceTemplate>();

        var assignment = await repo.GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && a.VehicleAssetId == vehicleAssetId)
            .Include(a => a.Template)
            .FirstOrDefaultAsync();

        if (assignment?.Template == null) return null;

        return new FleetVehicleComplianceTemplateDto
        {
            VehicleAssetId = assignment.VehicleAssetId,
            TemplateId = assignment.TemplateId,
            TemplateName = assignment.Template.Name,
            AppliedAt = assignment.AppliedAt
        };
    }

    public async Task<FleetVehicleComplianceTemplateDto> ApplyTemplateToVehicleAsync(Guid vehicleAssetId, Guid templateId)
    {
        if (vehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (templateId == Guid.Empty) throw new ArgumentException("TemplateId is required.");

        var tenantId = _currentUserProvider.TenantId;

        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.Id == vehicleAssetId && a.TenantId == tenantId, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selected asset is not a vehicle.");

        var templateRepo = _unitOfWork.Repository<FleetComplianceTemplate>();
        var template = await templateRepo.FirstOrDefaultAsync(
            t => t.Id == templateId && t.TenantId == tenantId && !t.IsDeleted,
            t => t.Items)
            ?? throw new ArgumentException("Template not found.");

        if (!template.IsActive) throw new ArgumentException("Template is inactive.");

        var assignmentRepo = _unitOfWork.Repository<FleetVehicleComplianceTemplate>();
        var existingAssignment = await assignmentRepo.FirstOrDefaultAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.VehicleAssetId == vehicleAssetId);

        if (existingAssignment == null)
        {
            existingAssignment = new FleetVehicleComplianceTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VehicleAssetId = vehicleAssetId,
                TemplateId = templateId,
                AppliedAt = DateTime.UtcNow,
                AppliedByUserId = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUserProvider.UserId
            };
            await assignmentRepo.AddAsync(existingAssignment);
        }
        else
        {
            existingAssignment.TemplateId = templateId;
            existingAssignment.AppliedAt = DateTime.UtcNow;
            existingAssignment.AppliedByUserId = _currentUserProvider.UserId;
            existingAssignment.UpdatedAt = DateTime.UtcNow;
            existingAssignment.LastModifiedById = _currentUserProvider.UserId;
            await assignmentRepo.UpdateAsync(existingAssignment);
        }

        var complianceRepo = _unitOfWork.Repository<FleetComplianceItem>();

        var existingCompliance = await complianceRepo.GetQueryable(i =>
                i.TenantId == tenantId &&
                !i.IsDeleted &&
                i.VehicleAssetId == vehicleAssetId)
            .ToListAsync();

        var byTemplateItemId = existingCompliance
            .Where(i => i.TemplateItemId.HasValue)
            .ToDictionary(i => i.TemplateItemId!.Value, i => i);

        foreach (var templateItem in template.Items.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).ThenBy(i => i.ComplianceType))
        {
            if (byTemplateItemId.ContainsKey(templateItem.Id)) continue;

            // Try attach an existing item (manual) by compliance type to avoid duplicates.
            var match = existingCompliance.FirstOrDefault(i =>
                i.TemplateItemId == null &&
                string.Equals(i.ComplianceType, templateItem.ComplianceType, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                match.TemplateItemId = templateItem.Id;
                match.IsCritical = templateItem.IsCritical;
                match.UpdatedAt = DateTime.UtcNow;
                match.LastModifiedById = _currentUserProvider.UserId;
                await complianceRepo.UpdateAsync(match);
                continue;
            }

            await complianceRepo.AddAsync(new FleetComplianceItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VehicleAssetId = vehicleAssetId,
                TemplateItemId = templateItem.Id,
                ComplianceType = templateItem.ComplianceType,
                IsCritical = templateItem.IsCritical,
                IssueDate = null,
                ExpiryDate = null,
                ReferenceNumber = null,
                Notes = null,
                DocumentLinks = null,
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUserProvider.UserId
            });
        }

        await _unitOfWork.SaveChangesAsync();

        return new FleetVehicleComplianceTemplateDto
        {
            VehicleAssetId = vehicleAssetId,
            TemplateId = templateId,
            TemplateName = template.Name,
            AppliedAt = existingAssignment.AppliedAt
        };
    }
}
