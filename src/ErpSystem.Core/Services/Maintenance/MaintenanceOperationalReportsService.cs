using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance;

public sealed class MaintenanceOperationalReportsService : IMaintenanceOperationalReportsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public MaintenanceOperationalReportsService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<MaintenanceOperationalReportsDto> GetOperationalReportsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        Guid? assetId = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required for maintenance reports.");
        }

        var to = NormalizeUtc(toUtc ?? DateTime.UtcNow);
        var from = NormalizeUtc(fromUtc ?? to.AddYears(-1));
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var movements = await LoadAssetMovementsAsync(tenantId, from, to, assetId);
        var inspections = await LoadInspectionServiceHistoryAsync(tenantId, from, to, assetId);
        var workOrders = await LoadWorkOrdersAsync(tenantId, from, to, assetId);
        var partsIssued = await LoadPartsIssuedAsync(tenantId, from, to, assetId);
        var (workOrderCosts, assetCosts) = await LoadCostsAsync(tenantId, from, to, assetId);

        return new MaintenanceOperationalReportsDto
        {
            FromUtc = from,
            ToUtc = to,
            AssetMovements = movements,
            InspectionServiceHistory = inspections,
            WorkOrderStatus = workOrders,
            PartsIssued = partsIssued,
            CostsByWorkOrder = workOrderCosts,
            CostsByAsset = assetCosts
        };
    }

    private async Task<List<MaintenanceAssetMovementReportRowDto>> LoadAssetMovementsAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        Guid? assetId)
    {
        var query = _unitOfWork.Repository<MaintenanceAssetMovement>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.EffectiveDate >= from && x.EffectiveDate <= to)
            .AsNoTracking()
            .Include(x => x.Asset)
            .AsQueryable();

        if (assetId.HasValue && assetId.Value != Guid.Empty)
        {
            query = query.Where(x => x.AssetId == assetId.Value);
        }

        return await query
            .OrderByDescending(x => x.EffectiveDate)
            .Select(x => new MaintenanceAssetMovementReportRowDto
            {
                MovementId = x.Id,
                AssetId = x.AssetId,
                AssetNumber = x.Asset.AssetNumber,
                AssetName = x.Asset.Name,
                EffectiveAtUtc = x.EffectiveDate,
                FromProject = x.FromProjectName ?? string.Empty,
                FromSite = x.FromSiteLocationName ?? x.FromLocation ?? string.Empty,
                ToProject = x.ToProjectName ?? string.Empty,
                ToSite = x.ToSiteLocationName ?? x.ToLocation ?? string.Empty,
                MovementType = x.MovementType,
                Reason = x.Reason,
                Notes = x.Notes
            })
            .ToListAsync();
    }

    private async Task<List<MaintenanceInspectionServiceReportRowDto>> LoadInspectionServiceHistoryAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        Guid? assetId)
    {
        var legacyQuery = _unitOfWork.Repository<AssetInspection>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.InspectionDate >= from && x.InspectionDate <= to)
            .AsNoTracking()
            .Include(x => x.Asset)
            .Include(x => x.InspectionTemplate)
            .Include(x => x.Inspector)
            .AsQueryable();

        var mobileQuery = _unitOfWork.Repository<FleetTripInspection>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.StartedAtUtc >= from && x.StartedAtUtc <= to)
            .AsNoTracking()
            .Include(x => x.VehicleAsset)
            .Include(x => x.InspectionTemplate)
            .Include(x => x.InspectorEmployee)
            .AsQueryable();

        if (assetId.HasValue && assetId.Value != Guid.Empty)
        {
            legacyQuery = legacyQuery.Where(x => x.AssetId == assetId.Value);
            mobileQuery = mobileQuery.Where(x => x.VehicleAssetId == assetId.Value);
        }

        var legacy = await legacyQuery
            .Select(x => new MaintenanceInspectionServiceReportRowDto
            {
                RecordId = x.Id,
                AssetId = x.AssetId,
                AssetNumber = x.Asset.AssetNumber,
                AssetName = x.Asset.Name,
                RecordType = x.InspectionTemplate.SheetType == "ServiceSheet" ? "Service" : "Inspection",
                SheetType = x.InspectionTemplate.SheetType,
                TemplateName = x.InspectionTemplate.Name,
                InspectionKind = x.InspectionTemplate.InspectionType,
                PerformedAtUtc = x.InspectionDate,
                Status = x.Status,
                Result = x.OverallResult,
                InspectorName = (x.Inspector.FirstName + " " + x.Inspector.LastName).Trim(),
                Source = "Asset inspection",
                Notes = x.Notes
            })
            .ToListAsync();

        var mobile = await mobileQuery
            .Select(x => new MaintenanceInspectionServiceReportRowDto
            {
                RecordId = x.Id,
                AssetId = x.VehicleAssetId,
                AssetNumber = x.VehicleAsset.AssetNumber,
                AssetName = x.VehicleAsset.Name,
                RecordType = x.InspectionTemplate.SheetType == "ServiceSheet" ? "Service" : "Inspection",
                SheetType = x.InspectionTemplate.SheetType,
                TemplateName = x.InspectionTemplate.Name,
                InspectionKind = x.InspectionKind,
                PerformedAtUtc = x.CompletedAtUtc ?? x.StartedAtUtc,
                Status = x.Status,
                Result = x.OverallResult,
                InspectorName = x.InspectorEmployee == null
                    ? string.Empty
                    : (x.InspectorEmployee.FirstName + " " + x.InspectorEmployee.LastName).Trim(),
                Source = x.CapturedOfflineAtUtc.HasValue ? "Mobile offline" : "Mobile/Trip",
                Notes = x.Notes
            })
            .ToListAsync();

        return legacy
            .Concat(mobile)
            .OrderByDescending(x => x.PerformedAtUtc)
            .ToList();
    }

    private async Task<List<MaintenanceWorkOrderStatusReportRowDto>> LoadWorkOrdersAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        Guid? assetId)
    {
        var query = _unitOfWork.Repository<WorkOrder>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.CreatedAt >= from && x.CreatedAt <= to)
            .AsNoTracking()
            .Include(x => x.Asset)
            .Include(x => x.WorkOrderType)
            .Include(x => x.MaintenanceType)
            .Include(x => x.PriorityLevel)
            .Include(x => x.AssignedTechnician)
            .AsQueryable();

        if (assetId.HasValue && assetId.Value != Guid.Empty)
        {
            query = query.Where(x => x.AssetId == assetId.Value);
        }

        var now = DateTime.UtcNow;
        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MaintenanceWorkOrderStatusReportRowDto
            {
                WorkOrderId = x.Id,
                WorkOrderNumber = x.WorkOrderNumber,
                AssetId = x.AssetId,
                AssetNumber = x.Asset.AssetNumber,
                AssetName = x.Asset.Name,
                Title = x.Title,
                WorkOrderType = x.WorkOrderType.Name,
                MaintenanceType = x.MaintenanceType.Name,
                Priority = x.PriorityLevel.Name,
                Status = x.Status,
                AssignedTechnician = x.AssignedTechnician == null
                    ? string.Empty
                    : (x.AssignedTechnician.FirstName + " " + x.AssignedTechnician.LastName).Trim(),
                CreatedAtUtc = x.CreatedAt,
                RequestedCompletionAtUtc = x.RequestedCompletionDate,
                CompletedAtUtc = x.ActualCompletionDate,
                IsOverdue = x.RequestedCompletionDate.HasValue
                    && x.RequestedCompletionDate.Value < now
                    && x.Status != "Completed"
                    && x.Status != "Closed"
                    && x.Status != "Cancelled"
            })
            .ToListAsync();
    }

    private async Task<List<MaintenancePartIssuedReportRowDto>> LoadPartsIssuedAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        Guid? assetId)
    {
        var query = _unitOfWork.Repository<WorkOrderPart>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted)
            .AsNoTracking()
            .Include(x => x.WorkOrder)
                .ThenInclude(x => x.Asset)
            .AsQueryable();

        query = query.Where(x =>
            (x.UsedAt ?? x.PickedAt ?? x.AllocatedAt ?? x.CreatedAt) >= from
            && (x.UsedAt ?? x.PickedAt ?? x.AllocatedAt ?? x.CreatedAt) <= to);

        if (assetId.HasValue && assetId.Value != Guid.Empty)
        {
            query = query.Where(x => x.WorkOrder.AssetId == assetId.Value);
        }

        var parts = await query
            .OrderByDescending(x => x.UsedAt ?? x.PickedAt ?? x.AllocatedAt ?? x.CreatedAt)
            .ToListAsync();

        return parts
            .Select(x =>
            {
                var issuedQuantity = x.QuantityUsed + x.QuantityReturned;
                if (issuedQuantity <= 0 && x.PickedAt.HasValue)
                {
                    issuedQuantity = x.QuantityAllocated;
                }

                var totalCost = x.TotalCost > 0
                    ? x.TotalCost
                    : Math.Round(x.UnitCost * Math.Max(x.QuantityUsed, issuedQuantity), 4);

                return new MaintenancePartIssuedReportRowDto
                {
                    WorkOrderPartId = x.Id,
                    WorkOrderId = x.WorkOrderId,
                    WorkOrderNumber = x.WorkOrder.WorkOrderNumber,
                    AssetId = x.WorkOrder.AssetId,
                    AssetNumber = x.WorkOrder.Asset.AssetNumber,
                    AssetName = x.WorkOrder.Asset.Name,
                    ItemCode = x.ItemCode,
                    ItemName = x.ItemName,
                    QuantityIssued = issuedQuantity,
                    QuantityUsed = x.QuantityUsed,
                    QuantityReturned = x.QuantityReturned,
                    UnitCost = x.UnitCost,
                    TotalCost = totalCost,
                    Status = x.Status,
                    IssuedAtUtc = x.UsedAt ?? x.PickedAt ?? x.AllocatedAt ?? x.CreatedAt
                };
            })
            .Where(x => x.QuantityIssued > 0)
            .ToList();
    }

    private async Task<(List<MaintenanceWorkOrderCostReportRowDto> WorkOrders, List<MaintenanceAssetCostReportRowDto> Assets)> LoadCostsAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        Guid? assetId)
    {
        var workOrderQuery = _unitOfWork.Repository<WorkOrder>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.CreatedAt >= from && x.CreatedAt <= to)
            .AsNoTracking()
            .Include(x => x.Asset)
            .AsQueryable();

        if (assetId.HasValue && assetId.Value != Guid.Empty)
        {
            workOrderQuery = workOrderQuery.Where(x => x.AssetId == assetId.Value);
        }

        var workOrders = await workOrderQuery.OrderByDescending(x => x.CreatedAt).ToListAsync();
        var workOrderIds = workOrders.Select(x => x.Id).ToList();
        if (workOrderIds.Count == 0)
        {
            return (new List<MaintenanceWorkOrderCostReportRowDto>(), new List<MaintenanceAssetCostReportRowDto>());
        }

        var partCosts = await _unitOfWork.Repository<WorkOrderPart>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && workOrderIds.Contains(x.WorkOrderId))
            .AsNoTracking()
            .GroupBy(x => x.WorkOrderId)
            .Select(x => new
            {
                WorkOrderId = x.Key,
                Total = x.Sum(p => p.TotalCost > 0 ? p.TotalCost : p.UnitCost * p.QuantityUsed)
            })
            .ToDictionaryAsync(x => x.WorkOrderId, x => x.Total);

        var laborCosts = await _unitOfWork.Repository<WorkOrderLabor>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && workOrderIds.Contains(x.WorkOrderId))
            .AsNoTracking()
            .GroupBy(x => x.WorkOrderId)
            .Select(x => new { WorkOrderId = x.Key, Total = x.Sum(l => l.TotalCost) })
            .ToDictionaryAsync(x => x.WorkOrderId, x => x.Total);

        var rows = workOrders.Select(x =>
        {
            var partsCost = partCosts.GetValueOrDefault(x.Id);
            var laborCost = laborCosts.GetValueOrDefault(x.Id);
            var capturedTotal = Math.Max(x.ActualCost, partsCost + laborCost);
            var otherCost = Math.Max(0, capturedTotal - partsCost - laborCost);

            return new MaintenanceWorkOrderCostReportRowDto
            {
                WorkOrderId = x.Id,
                WorkOrderNumber = x.WorkOrderNumber,
                AssetId = x.AssetId,
                AssetNumber = x.Asset.AssetNumber,
                AssetName = x.Asset.Name,
                Title = x.Title,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAt,
                PartsCost = Math.Round(partsCost, 2),
                LaborCost = Math.Round(laborCost, 2),
                OtherCost = Math.Round(otherCost, 2),
                TotalCost = Math.Round(capturedTotal, 2)
            };
        }).ToList();

        var assets = rows
            .GroupBy(x => new { x.AssetId, x.AssetNumber, x.AssetName })
            .Select(x => new MaintenanceAssetCostReportRowDto
            {
                AssetId = x.Key.AssetId,
                AssetNumber = x.Key.AssetNumber,
                AssetName = x.Key.AssetName,
                WorkOrderCount = x.Count(),
                PartsCost = x.Sum(r => r.PartsCost),
                LaborCost = x.Sum(r => r.LaborCost),
                OtherCost = x.Sum(r => r.OtherCost),
                TotalCost = x.Sum(r => r.TotalCost)
            })
            .OrderByDescending(x => x.TotalCost)
            .ToList();

        return (rows, assets);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
