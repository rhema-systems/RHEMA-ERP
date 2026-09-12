using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/purchase-orders/{purchaseOrderId}/landed-cost-plan")]
[Authorize]
public class PurchaseOrderLandedCostPlansController : ControllerBase
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseOrderLandedCostPlanRepository _planRepository;
    private readonly IPurchaseOrderLandedCostPlanItemRepository _planItemRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<PurchaseOrderLandedCostPlansController> _logger;

    public PurchaseOrderLandedCostPlansController(
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseOrderLandedCostPlanRepository planRepository,
        IPurchaseOrderLandedCostPlanItemRepository planItemRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<PurchaseOrderLandedCostPlansController> logger)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _planRepository = planRepository;
        _planItemRepository = planItemRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PurchaseOrderLandedCostPlanDto?>> Get(Guid purchaseOrderId)
    {
        try
        {
            var tenantId = _currentUser.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant not found");

            var po = await _purchaseOrderRepository.GetByIdAsync(purchaseOrderId);
            if (po == null || po.TenantId != tenantId)
                return NotFound($"Purchase order {purchaseOrderId} not found");

            var plan = await _planRepository.GetWithItemsByPurchaseOrderIdAsync(purchaseOrderId);
            if (plan != null && plan.TenantId != tenantId)
                return NotFound($"Landed cost plan for purchase order {purchaseOrderId} not found");

            return Ok(plan == null ? null : MapToDto(plan));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PO landed cost plan for PO {PurchaseOrderId}", purchaseOrderId);
            return StatusCode(500, "An error occurred while retrieving landed cost plan");
        }
    }

    [HttpPut]
    public async Task<ActionResult<PurchaseOrderLandedCostPlanDto>> Upsert(Guid purchaseOrderId, [FromBody] UpsertPurchaseOrderLandedCostPlanDto dto)
    {
        try
        {
            var tenantId = _currentUser.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant not found");

            var po = await _purchaseOrderRepository.GetByIdAsync(purchaseOrderId);
            if (po == null || po.TenantId != tenantId)
                return NotFound($"Purchase order {purchaseOrderId} not found");

            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                var ownsTransaction = !_unitOfWork.HasActiveTransaction;
                if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var currentPo = await _unitOfWork.Repository<PurchaseOrder>().GetQueryable(
                        p => p.Id == purchaseOrderId && p.TenantId == tenantId && !p.IsDeleted).AsNoTracking().SingleOrDefaultAsync()
                        ?? throw new ArgumentException("The purchase order is no longer available.");
                    var lines = (await _unitOfWork.Repository<PurchaseOrderItem>().FindAsync(
                        i => i.PurchaseOrderId == purchaseOrderId && i.TenantId == tenantId && !i.IsDeleted)).ToList();
                    await new ErpSystem.Core.Services.Procurement.PurchaseOrderLandedCostPlanService(
                        _planRepository, _planItemRepository, _businessPartnerRepository).StageAsync(currentPo, lines, dto);
                    await _unitOfWork.SaveChangesAsync();
                    if (ownsTransaction) await _unitOfWork.CommitAsync();
                    return true;
                }
                catch
                {
                    if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                    if (ownsTransaction) _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            });
            var reloaded = await _planRepository.GetWithItemsByPurchaseOrderIdAsync(purchaseOrderId)
                ?? throw new InvalidOperationException("Failed to reload landed cost plan after save");

            return Ok(MapToDto(reloaded));
        }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.DataAnnotations.ValidationException)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting PO landed cost plan for PO {PurchaseOrderId}", purchaseOrderId);
            return StatusCode(500, "An error occurred while saving landed cost plan");
        }
    }

    private static PurchaseOrderLandedCostPlanDto MapToDto(PurchaseOrderLandedCostPlan plan)
    {
        return new PurchaseOrderLandedCostPlanDto
        {
            Id = plan.Id,
            PurchaseOrderId = plan.PurchaseOrderId,
            Currency = plan.Currency,
            Status = plan.Status,
            TotalPlannedCost = plan.TotalPlannedCost,
            Notes = plan.Notes,
            Items = plan.Items
                .Where(i => !i.IsDeleted)
                .Select(i => new PurchaseOrderLandedCostPlanItemDto
                {
                    Id = i.Id,
                    PurchaseOrderItemId = i.PurchaseOrderItemId,
                    CostType = i.CostType,
                    Description = i.Description,
                    Amount = i.Amount,
                    Currency = i.Currency,
                    ExchangeRate = i.ExchangeRate,
                    AmountInPlanCurrency = i.AmountInPlanCurrency,
                    AllocationMethod = i.AllocationMethod,
                    SupplierId = i.SupplierId,
                    SupplierName = i.SupplierName,
                    ReferenceNumber = i.ReferenceNumber,
                    Notes = i.Notes
                })
                .ToList()
        };
    }
}
