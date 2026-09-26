using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class LandedCostService
{
    public Task<LandedCostDetailDto> SetReceiptWeightAsync(Guid id, Guid receiptItemId,
        SetLandedCostReceiptWeightDto dto, Guid userId) => SaveInTransactionAsync(async () =>
    {
        if (userId == Guid.Empty) throw new ArgumentException("A valid actor is required.");
        if (ReceiptItemWeight.Validate(dto.UnitWeightKg) <= 0 || string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 500)
            throw new ArgumentException("Enter a positive weight and a reason of at most 500 characters.");
        await _unitOfWork.AcquireTransactionLockAsync($"InventoryLandedCost:{id:N}");
        var cost = await _landedCostRepository.GetWithDetailsAsync(id)
            ?? throw new ArgumentException("Landed cost voucher not found.");
        if (cost.Status != "Draft")
            throw new InvalidOperationException("Receipt weights can only be declared on a Draft landed-cost voucher.");
        var receipt = await _grnRepository.GetWithItemsAsync(cost.GoodsReceiptNoteId)
            ?? throw new ArgumentException("Receipt not found.");
        var item = receipt.Items.SingleOrDefault(i => i.Id == receiptItemId && !i.IsDeleted && i.TenantId == cost.TenantId);
        if (receipt.TenantId != cost.TenantId || item == null || string.IsNullOrWhiteSpace(item.UnitOfMeasure))
            throw new ArgumentException("Select a stock line belonging to this voucher's receipt.");
        if (dto.EditToken != CostEditToken(cost))
            throw new InvalidOperationException("The voucher has changed. Refresh before declaring its weight.");
        var repository = _unitOfWork.Repository<LandedCostReceiptWeight>();
        var weight = await repository.GetQueryable(w => w.TenantId == cost.TenantId &&
            w.LandedCostId == id && w.GoodsReceiptNoteItemId == receiptItemId && !w.IsDeleted).SingleOrDefaultAsync();
        var before = weight == null ? null : JsonSerializer.Serialize(new { weight.UnitWeightKg, weight.StockUom, weight.Reason });
        if (weight == null)
        {
            weight = new LandedCostReceiptWeight { TenantId = cost.TenantId, LandedCostId = id,
                GoodsReceiptNoteItemId = receiptItemId, CreatedById = userId };
            await repository.AddAsync(weight);
        }
        weight.UnitWeightKg = dto.UnitWeightKg;
        weight.StockUom = item.UnitOfMeasure;
        weight.Reason = dto.Reason.Trim();
        weight.UpdatedAt = DateTime.UtcNow;
        weight.LastModifiedById = userId;
        cost.UpdatedAt = DateTime.UtcNow;
        cost.LastModifiedById = userId;
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = cost.TenantId, UserId = userId, Username = userId.ToString(),
            Action = "LANDED_COST_WEIGHT_DECLARED", Resource = nameof(LandedCost), ResourceId = id.ToString(),
            OldValues = before, NewValues = JsonSerializer.Serialize(new { receiptItemId, weight.UnitWeightKg, weight.StockUom, weight.Reason }),
            Timestamp = DateTime.UtcNow, IpAddress = "Unknown", CreatedById = userId
        });
        await _unitOfWork.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    });

    private async Task PopulateReceiptWeightsAsync(LandedCost cost, GoodsReceiptNote? receipt, LandedCostDetailDto dto)
    {
        if (receipt == null) return;
        var weights = await _unitOfWork.Repository<LandedCostReceiptWeight>().GetQueryable(w =>
            w.TenantId == cost.TenantId && w.LandedCostId == cost.Id && !w.IsDeleted).AsNoTracking().ToListAsync();
        dto.ReceiptWeights = receipt.Items.Where(i => !i.IsDeleted).Select(item =>
        {
            var declaration = weights.SingleOrDefault(w => w.GoodsReceiptNoteItemId == item.Id);
            return new LandedCostReceiptWeightDto { GoodsReceiptNoteItemId = item.Id, ItemCode = item.ItemCode,
                ItemName = item.ItemName, StockUom = item.UnitOfMeasure,
                ReceiptUnitWeightKg = item.UnitWeightKg,
                UnitWeightKg = declaration?.UnitWeightKg ?? item.UnitWeightKg,
                IsOverridden = declaration != null, Reason = declaration?.Reason };
        }).ToList();
    }
}
