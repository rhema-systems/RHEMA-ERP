using System.Security.Claims;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/purchase-orders/{purchaseOrderId}/landed-cost-plan")]
[Authorize]
public class PurchaseOrderLandedCostPlansController : ControllerBase
{
    private static readonly HashSet<string> AllowedAllocationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "ByValue",
        "ByQuantity",
        "ByWeight",
        "ByVolume",
        "Equal",
        "Manual"
    };

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

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
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

            var userId = GetCurrentUserId();
            _ = userId;

            var po = await _purchaseOrderRepository.GetByIdAsync(purchaseOrderId);
            if (po == null || po.TenantId != tenantId)
                return NotFound($"Purchase order {purchaseOrderId} not found");

            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest("At least one landed cost plan line is required");

            var plan = await _planRepository.GetWithItemsByPurchaseOrderIdAsync(purchaseOrderId);
            var isNewPlan = false;
            if (plan == null)
            {
                isNewPlan = true;
                plan = new PurchaseOrderLandedCostPlan
                {
                    PurchaseOrderId = purchaseOrderId,
                    Currency = string.IsNullOrWhiteSpace(dto.Currency) ? po.Currency : dto.Currency,
                    Status = "Draft",
                    Notes = dto.Notes,
                    TenantId = tenantId
                };

                await _planRepository.AddAsync(plan);
            }
            else
            {
                if (plan.TenantId != tenantId)
                    return NotFound($"Landed cost plan for purchase order {purchaseOrderId} not found");

                plan.Currency = string.IsNullOrWhiteSpace(dto.Currency) ? plan.Currency : dto.Currency;
                plan.Notes = dto.Notes;
                await _planRepository.UpdateAsync(plan);

                var existingItems = plan.Items.Where(i => !i.IsDeleted).ToList();
                await _planItemRepository.DeleteRangeAsync(existingItems);
            }

            decimal total = 0;
            foreach (var line in dto.Items)
            {
                EnsureAllowedAllocationMethod(line.AllocationMethod);

                var supplierName = await ResolveSupplierNameAsync(line.SupplierId);
                var amountInPlanCurrency = RoundMoney(line.Amount * line.ExchangeRate);
                total += amountInPlanCurrency;

                var planItem = new PurchaseOrderLandedCostPlanItem
                {
                    PurchaseOrderLandedCostPlanId = plan.Id,
                    CostType = line.CostType,
                    Description = line.Description,
                    Amount = line.Amount,
                    Currency = line.Currency,
                    ExchangeRate = line.ExchangeRate,
                    AmountInPlanCurrency = amountInPlanCurrency,
                    AllocationMethod = line.AllocationMethod,
                    SupplierId = line.SupplierId,
                    SupplierName = supplierName,
                    ReferenceNumber = line.ReferenceNumber,
                    Notes = line.Notes,
                    TenantId = tenantId
                };

                await _planItemRepository.AddAsync(planItem);
            }

            plan.TotalPlannedCost = RoundMoney(total);
            // IMPORTANT: Do not call Update() on a newly-added entity; GenericRepository.UpdateAsync uses DbSet.Update,
            // which would flip EntityState from Added -> Modified and prevent the INSERT, causing FK failures on item inserts.
            if (!isNewPlan)
                await _planRepository.UpdateAsync(plan);

            await _unitOfWork.SaveChangesAsync();

            var reloaded = await _planRepository.GetWithItemsByPurchaseOrderIdAsync(purchaseOrderId)
                ?? throw new InvalidOperationException("Failed to reload landed cost plan after save");

            return Ok(MapToDto(reloaded));
        }
        catch (ArgumentException ex)
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

    private async Task<string?> ResolveSupplierNameAsync(Guid? supplierId)
    {
        if (supplierId == null || supplierId == Guid.Empty) return null;
        var partner = await _businessPartnerRepository.GetByIdAsync(supplierId.Value);
        return partner?.PartnerName;
    }

    private static void EnsureAllowedAllocationMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("AllocationMethod is required");

        if (!AllowedAllocationMethods.Contains(method))
            throw new ArgumentException($"Unsupported AllocationMethod '{method}'. Allowed: {string.Join(", ", AllowedAllocationMethods.OrderBy(x => x))}");
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

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
