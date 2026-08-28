using System.Data;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Maintenance facade. All reservation and stock mutations are delegated to the authoritative
/// Inventory work-order reservation service.
/// </summary>
public sealed class WorkOrderPartService : IWorkOrderPartService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserProvider currentUser;
    private readonly IInventoryWorkOrderReservationService reservations;
    private readonly IProcurementAccessControlService access;

    public WorkOrderPartService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUser,
        IInventoryWorkOrderReservationService reservations, IProcurementAccessControlService access)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.reservations = reservations;
        this.access = access;
    }

    public Task<WorkOrderPartDto> AddPartAsync(CreateWorkOrderPartDto createDto,
        string? idempotencyKey = null, string? correlationId = null) =>
        reservations.CreateAndReserveAsync(createDto, Key(idempotencyKey, "create", createDto.WorkOrderId),
            Correlation(correlationId));

    public Task<WorkOrderPartDto> UpdatePartAsync(Guid id, UpdateWorkOrderPartDto updateDto,
        string? idempotencyKey = null, string? correlationId = null) =>
        reservations.UpdateAsync(id, updateDto, Key(idempotencyKey, "update", id), Correlation(correlationId));

    public Task DeletePartAsync(Guid id, string? idempotencyKey = null, string? correlationId = null) =>
        reservations.DeleteAndReleaseAsync(id, "Unused work-order part removed.",
            Key(idempotencyKey, "delete", id), Correlation(correlationId));

    public async Task<IEnumerable<WorkOrderPartDto>> GetPartsByWorkOrderAsync(Guid workOrderId)
    {
        EnsureActor();
        var parts = await FullQuery().Where(value => value.WorkOrderId == workOrderId)
            .OrderBy(value => value.CreatedAt).AsNoTracking().ToListAsync();
        var result = new List<WorkOrderPartDto>(parts.Count);
        foreach (var part in parts)
        {
            if (part.Allocation is not null)
            {
                var decision = await access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = "procurement.inventory.read", WarehouseId = part.Allocation.WarehouseId,
                    LocationId = part.Allocation.LocationId, RequireLocationScope = true,
                    SourceType = "MaintenanceWorkOrder", SourceReference = part.Allocation.ReferenceNumber ?? workOrderId.ToString("N")
                }, $"work-order-part-read:{part.Id:N}");
                if (!decision.Allowed) continue;
            }
            result.Add(Map(part));
        }
        return result;
    }

    public async Task<WorkOrderPartDto> UpdatePartStatusAsync(Guid id, string status, int? quantityUsed = null)
    {
        var part = await FullQuery().AsNoTracking().SingleOrDefaultAsync(value => value.Id == id)
            ?? throw new InventoryWorkOrderReservationNotFoundException("The work-order part was not found.");
        if (string.Equals(status, "Returned", StringComparison.OrdinalIgnoreCase))
            return await ReturnUnusedPartsAsync(id);
        return await reservations.UpdateAsync(id, new UpdateWorkOrderPartDto
        {
            QuantityRequired = part.QuantityRequired,
            QuantityUsed = quantityUsed ?? part.QuantityUsed,
            QuantityReturned = part.QuantityReturned,
            UnitCost = part.UnitCost,
            WarehouseLocationId = part.WarehouseLocationId,
            SerialNumber = part.SerialNumber,
            LotNumber = part.LotNumber,
            Status = status,
            Notes = part.Notes
        }, Key(null, "status", id), Correlation(null));
    }

    public Task<WorkOrderPartDto> ReturnUnusedPartsAsync(Guid partId, string? idempotencyKey = null,
        string? correlationId = null) => reservations.ReturnUnusedAsync(partId,
        Key(idempotencyKey, "return", partId), Correlation(correlationId));

    public Task<WorkOrderPartDto> RetryReservationAsync(Guid partId, string? idempotencyKey = null,
        string? correlationId = null) => reservations.RetryAsync(partId,
        Key(idempotencyKey, "retry", partId), Correlation(correlationId));

    public Task<IReadOnlyList<InventoryWorkOrderReservationActionDto>> GetReservationActionsAsync(Guid partId) =>
        reservations.GetActionsAsync(partId);

    public async Task<decimal> GetTotalPartsCostAsync(Guid workOrderId)
    {
        EnsureActor();
        return await unitOfWork.Repository<WorkOrderPart>().GetQueryable(value =>
                value.TenantId == currentUser.TenantId && value.WorkOrderId == workOrderId && !value.IsDeleted)
            .SumAsync(value => value.TotalCost);
    }

    public async Task<IEnumerable<WorkOrderPartDto>> GetPartsRequiringOrderAsync()
    {
        EnsureActor();
        return (await FullQuery().Where(value => !value.AllocationId.HasValue || value.QuantityAllocated < value.QuantityRequired)
            .OrderBy(value => value.CreatedAt).AsNoTracking().ToListAsync()).Select(Map).ToList();
    }

    public async Task<IEnumerable<WorkOrderPartDto>> AddPartsBulkAsync(IEnumerable<CreateWorkOrderPartDto> createDtos,
        string? idempotencyKey = null, string? correlationId = null)
    {
        var requests = createDtos.ToList();
        if (requests.Count == 0) return [];
        var result = new List<WorkOrderPartDto>(requests.Count);
        var prefix = Key(idempotencyKey, "bulk-create", requests[0].WorkOrderId);
        var correlation = Correlation(correlationId);
        await unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                for (var i = 0; i < requests.Count; i++)
                    result.Add(await reservations.CreateAndReserveAsync(requests[i], $"{prefix}:{i}", correlation));
                await unitOfWork.CommitAsync();
            }
            catch
            {
                if (unitOfWork.HasActiveTransaction) await unitOfWork.RollbackAsync();
                throw;
            }
        });
        return result;
    }

    public async Task DeletePartsBulkAsync(IEnumerable<Guid> ids, string? idempotencyKey = null,
        string? correlationId = null)
    {
        var values = ids.Distinct().ToList();
        if (values.Count == 0) return;
        var prefix = Key(idempotencyKey, "bulk-delete", values[0]);
        var correlation = Correlation(correlationId);
        await unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                for (var i = 0; i < values.Count; i++)
                    await reservations.DeleteAndReleaseAsync(values[i], "Unused work-order part removed in bulk.",
                        $"{prefix}:{i}", correlation);
                await unitOfWork.CommitAsync();
            }
            catch
            {
                if (unitOfWork.HasActiveTransaction) await unitOfWork.RollbackAsync();
                throw;
            }
        });
    }

    private IQueryable<WorkOrderPart> FullQuery() => unitOfWork.Repository<WorkOrderPart>().GetQueryable(value =>
            value.TenantId == currentUser.TenantId && !value.IsDeleted)
        .Include(value => value.InventoryItem).Include(value => value.WarehouseLocation)
        .Include(value => value.Allocation).ThenInclude(value => value!.Warehouse);

    private static WorkOrderPartDto Map(WorkOrderPart part) => new()
    {
        Id = part.Id, WorkOrderId = part.WorkOrderId, InventoryItemId = part.InventoryItemId,
        ItemCode = part.ItemCode, ItemName = part.ItemName, Description = part.Description ?? part.InventoryItem?.Description,
        QuantityRequired = part.QuantityRequired, QuantityAllocated = part.QuantityAllocated,
        QuantityUsed = part.QuantityUsed, QuantityReturned = part.QuantityReturned,
        UnitCost = part.UnitCost, TotalCost = part.TotalCost, WarehouseId = part.Allocation?.WarehouseId,
        WarehouseName = part.Allocation?.Warehouse?.Name, WarehouseLocationId = part.WarehouseLocationId,
        SerialNumber = part.SerialNumber,
        LotNumber = part.LotNumber, WarehouseLocationCode = part.WarehouseLocation?.LocationCode,
        WarehouseLocationName = part.WarehouseLocation?.Name, Status = part.Status,
        AllocationId = part.AllocationId, AllocatedAt = part.AllocatedAt, PickedAt = part.PickedAt,
        UsedAt = part.UsedAt, Notes = part.Notes, CreatedAt = part.CreatedAt, UpdatedAt = part.UpdatedAt
    };

    private void EnsureActor()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty || currentUser.TenantId == Guid.Empty ||
            currentUser.IsExternalUser)
            throw new InventoryWorkOrderReservationAuthorizationException("An authenticated internal tenant user is required.");
    }

    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value)
        ? $"inventory-work-order-reservation-{Guid.NewGuid():N}" : value.Trim();
    private static string Key(string? value, string operation, Guid id)
    {
        var result = string.IsNullOrWhiteSpace(value)
            ? $"{operation}:{id:N}:{Guid.NewGuid():N}" : value.Trim();
        return result[..Math.Min(result.Length, 60)];
    }
}
