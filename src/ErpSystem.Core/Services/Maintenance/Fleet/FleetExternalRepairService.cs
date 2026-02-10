using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetExternalRepairService : IFleetExternalRepairService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetExternalRepairService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PagedResult<FleetExternalRepairDto>> GetPagedAsync(int page, int pageSize, Guid? vehicleAssetId = null, string? status = null)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetExternalRepair>();

        IQueryable<FleetExternalRepair> q = repo.GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.VehicleAsset)
            .Include(r => r.VendorBusinessPartner);

        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            q = q.Where(r => r.VehicleAssetId == vehicleAssetId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(r => r.Status == status);

        var total = await q.CountAsync();

        var items = await q.OrderByDescending(r => r.RequestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new FleetExternalRepairDto
            {
                Id = r.Id,
                VehicleAssetId = r.VehicleAssetId,
                VehicleName = r.VehicleAsset.Name,
                VendorBusinessPartnerId = r.VendorBusinessPartnerId,
                VendorBusinessPartnerName = r.VendorBusinessPartner != null ? r.VendorBusinessPartner.PartnerName : null,
                Title = r.Title,
                Description = r.Description,
                Status = r.Status,
                EstimatedCost = r.EstimatedCost,
                ActualCost = r.ActualCost,
                CurrencyCode = r.CurrencyCode,
                RequestedAtUtc = r.RequestedAtUtc,
                ApprovedAtUtc = r.ApprovedAtUtc,
                CompletedAtUtc = r.CompletedAtUtc,
                InvoicedAtUtc = r.InvoicedAtUtc,
                WorkOrderId = r.WorkOrderId
            })
            .ToListAsync();

        return new PagedResult<FleetExternalRepairDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<FleetExternalRepairDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetExternalRepair>();
        var r = await repo.GetQueryable(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted)
            .Include(x => x.VehicleAsset)
            .Include(x => x.VendorBusinessPartner)
            .FirstOrDefaultAsync();

        if (r == null) return null;

        return new FleetExternalRepairDto
        {
            Id = r.Id,
            VehicleAssetId = r.VehicleAssetId,
            VehicleName = r.VehicleAsset.Name,
            VendorBusinessPartnerId = r.VendorBusinessPartnerId,
            VendorBusinessPartnerName = r.VendorBusinessPartner != null ? r.VendorBusinessPartner.PartnerName : null,
            Title = r.Title,
            Description = r.Description,
            Status = r.Status,
            EstimatedCost = r.EstimatedCost,
            ActualCost = r.ActualCost,
            CurrencyCode = r.CurrencyCode,
            RequestedAtUtc = r.RequestedAtUtc,
            ApprovedAtUtc = r.ApprovedAtUtc,
            CompletedAtUtc = r.CompletedAtUtc,
            InvoicedAtUtc = r.InvoicedAtUtc,
            WorkOrderId = r.WorkOrderId
        };
    }

    public async Task<FleetExternalRepairDto> CreateAsync(CreateFleetExternalRepairDto dto)
    {
        dto ??= new CreateFleetExternalRepairDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ArgumentException("Title is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        // Validate vendor if provided
        if (dto.VendorBusinessPartnerId.HasValue && dto.VendorBusinessPartnerId.Value != Guid.Empty)
        {
            var vendor = await _unitOfWork.Repository<BusinessPartner>()
                .FirstOrDefaultAsync(bp => bp.TenantId == tenantId && bp.Id == dto.VendorBusinessPartnerId.Value && !bp.IsDeleted);
            if (vendor == null) throw new ArgumentException("Vendor business partner not found.");
        }

        var entity = new FleetExternalRepair
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            VendorBusinessPartnerId = dto.VendorBusinessPartnerId,
            Title = dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Status = "Requested",
            EstimatedCost = dto.EstimatedCost,
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? null : dto.CurrencyCode.Trim(),
            RequestedAtUtc = now,
            CreatedAt = now,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetExternalRepair>().AddAsync(entity);

        // Auto-add cost entry (best-effort, can be updated later when invoiced).
        if (entity.EstimatedCost.HasValue && entity.EstimatedCost.Value > 0)
        {
            await _unitOfWork.Repository<FleetCostEntry>().AddAsync(new FleetCostEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VehicleAssetId = entity.VehicleAssetId,
                FleetExternalRepairId = entity.Id,
                CostDateUtc = now,
                CostType = "ExternalRepair",
                Amount = entity.EstimatedCost.Value,
                CurrencyCode = entity.CurrencyCode,
                Notes = "Estimated external repair cost",
                CreatedAt = now,
                CreatedById = userId
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetExternalRepairDto> UpdateStatusAsync(Guid id, UpdateFleetExternalRepairStatusDto dto)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.");
        dto ??= new UpdateFleetExternalRepairStatusDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetExternalRepair>();
        var entity = await repo.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted)
            ?? throw new ArgumentException("External repair not found.");

        entity.Status = dto.Status.Trim();

        if (dto.ActualCost.HasValue && dto.ActualCost.Value > 0)
        {
            entity.ActualCost = Math.Round(dto.ActualCost.Value, 2, MidpointRounding.AwayFromZero);
        }

        if (!string.IsNullOrWhiteSpace(dto.CurrencyCode))
        {
            entity.CurrencyCode = dto.CurrencyCode.Trim();
        }

        if (string.Equals(entity.Status, "Approved", StringComparison.OrdinalIgnoreCase) && !entity.ApprovedAtUtc.HasValue)
            entity.ApprovedAtUtc = now;
        if (string.Equals(entity.Status, "Completed", StringComparison.OrdinalIgnoreCase) && !entity.CompletedAtUtc.HasValue)
            entity.CompletedAtUtc = now;
        if (string.Equals(entity.Status, "Invoiced", StringComparison.OrdinalIgnoreCase) && !entity.InvoicedAtUtc.HasValue)
            entity.InvoicedAtUtc = now;

        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        await repo.UpdateAsync(entity);

        // Reconcile external repair cost entry (estimated -> actual) when actual cost is provided.
        var amount = entity.ActualCost ?? entity.EstimatedCost;
        if (amount.HasValue && amount.Value > 0)
        {
            var costRepo = _unitOfWork.Repository<FleetCostEntry>();
            var cost = await costRepo.FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.FleetExternalRepairId == entity.Id &&
                c.CostType == "ExternalRepair" &&
                !c.IsDeleted);

            if (cost == null)
            {
                await costRepo.AddAsync(new FleetCostEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    VehicleAssetId = entity.VehicleAssetId,
                    FleetExternalRepairId = entity.Id,
                    CostDateUtc = now,
                    CostType = "ExternalRepair",
                    Amount = Math.Round(amount.Value, 2, MidpointRounding.AwayFromZero),
                    CurrencyCode = entity.CurrencyCode,
                    Notes = entity.ActualCost.HasValue ? "Actual external repair cost" : "Estimated external repair cost",
                    CreatedAt = now,
                    CreatedById = userId
                });
            }
            else
            {
                cost.VehicleAssetId = entity.VehicleAssetId;
                cost.CostDateUtc = now;
                cost.Amount = Math.Round(amount.Value, 2, MidpointRounding.AwayFromZero);
                cost.CurrencyCode = entity.CurrencyCode;
                cost.Notes = entity.ActualCost.HasValue ? "Actual external repair cost" : "Estimated external repair cost";
                cost.UpdatedAt = now;
                cost.LastModifiedById = userId;
                await costRepo.UpdateAsync(cost);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }
}
