using System.ComponentModel.DataAnnotations;
using System.Data;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Procurement;

/// <summary>
/// API controller for managing purchase orders
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    // Avoid duplicate InventoryLocation upserts when multiple receipt lines hit the same (item, bin) before SaveChanges.
    private readonly Dictionary<(Guid InventoryItemId, Guid LocationId), InventoryLocation> _inventoryLocationCache = new();
    private readonly HashSet<(Guid InventoryItemId, Guid LocationId)> _newInventoryLocationKeys = new();
    // Avoid duplicate WarehouseQuantity/InventoryItem updates when multiple receipt lines hit the same targets before SaveChanges.
    private readonly Dictionary<(Guid InventoryItemId, Guid WarehouseId), WarehouseQuantity> _warehouseQuantityCache = new();
    private readonly HashSet<(Guid InventoryItemId, Guid WarehouseId)> _newWarehouseQuantityKeys = new();
    private readonly Dictionary<Guid, InventoryItem> _inventoryItemCache = new();
    private readonly Dictionary<Guid, bool> _warehouseConsignmentFlagCache = new();

    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseOrderItemRepository _purchaseOrderItemRepository;
    private readonly IPurchaseOrderReceiptRepository _purchaseOrderReceiptRepository;
    private readonly IPurchaseOrderReceiptItemRepository _purchaseOrderReceiptItemRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProcurementBudgetService _budgetService;
    private readonly IProcurementBudgetRepository _budgetRepository;
    private readonly IProcurementPlanItemRepository _planItemRepository;
    private readonly IInventoryValuationService _inventoryValuationService;
    private readonly IProjectService _projectService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserService;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IWorkflowService _workflowService;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly IProcurementPurchaseOrderSourceService _purchaseOrderSources;
    private readonly ILogger<PurchaseOrdersController> _logger;

    private const string SpreadToItemCost = "SpreadToItemCost";
    private const string GLExpense = "GLExpense";
    private const string BasisValue = "Value";
    private const string BasisWeight = "Weight";
    private const string BasisQuantity = "Quantity";

    public PurchaseOrdersController(
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseOrderItemRepository purchaseOrderItemRepository,
        IPurchaseOrderReceiptRepository purchaseOrderReceiptRepository,
        IPurchaseOrderReceiptItemRepository purchaseOrderReceiptItemRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        IInventoryItemRepository inventoryItemRepository,
        IWarehouseRepository warehouseRepository,
        IProcurementBudgetService budgetService,
        IProcurementBudgetRepository budgetRepository,
        IProcurementPlanItemRepository planItemRepository,
        IInventoryValuationService inventoryValuationService,
        IProjectService projectService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserService,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IWorkflowService workflowService,
        ISupplierValidationService supplierValidation,
        IProcurementPurchaseOrderSourceService purchaseOrderSources,
        ILogger<PurchaseOrdersController> logger)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseOrderItemRepository = purchaseOrderItemRepository;
        _purchaseOrderReceiptRepository = purchaseOrderReceiptRepository;
        _purchaseOrderReceiptItemRepository = purchaseOrderReceiptItemRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _warehouseRepository = warehouseRepository;
        _budgetService = budgetService;
        _budgetRepository = budgetRepository;
        _planItemRepository = planItemRepository;
        _inventoryValuationService = inventoryValuationService;
        _projectService = projectService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _workflowService = workflowService;
        _supplierValidation = supplierValidation;
        _purchaseOrderSources = purchaseOrderSources;
        _logger = logger;
    }

    [HttpGet("source-options")]
    public async Task<ActionResult<ProcurementPurchaseOrderSourceStatusDto>>
        GetSourceOptions([FromQuery] Guid? purchaseRequisitionId = null)
    {
        var correlationId = CorrelationId();
        try
        {
            return Ok(await _purchaseOrderSources.GetOptionsAsync(
                purchaseRequisitionId, correlationId, HttpContext.RequestAborted));
        }
        catch (ProcurementPurchaseOrderSourceAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "PO_SOURCE_FORBIDDEN",
                message = ex.Message,
                correlationId
            });
        }
        catch (ProcurementAccessAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "PO_SOURCE_FORBIDDEN",
                message = ex.Message,
                correlationId
            });
        }
    }

    /// <summary>
    /// Gets all purchase orders with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PurchaseOrderSummaryDto>>> GetPurchaseOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var purchaseOrders = await _purchaseOrderRepository.GetPurchaseOrdersAsync(
                page, pageSize, search, status, supplierId, startDate, endDate);

            var purchaseOrderDtos = purchaseOrders.Items.Select(po => new PurchaseOrderSummaryDto
            {
                Id = po.Id,
                OrderNumber = po.OrderNumber,
                OrderType = po.OrderType,
                SupplierId = po.BusinessPartnerId,
                SupplierName = po.BusinessPartner?.PartnerName ?? "",
                OrderDate = po.OrderDate,
                RequiredDate = po.RequiredDate,
                PromisedDate = po.PromisedDate,
                Status = po.Status,
                TotalAmount = po.TotalAmount,
                ItemCount = po.Items?.Count ?? 0,
                RequestedByName = po.RequestedBy?.FirstName + " " + po.RequestedBy?.LastName,
                ProcurementSourceType = po.ProcurementSourceType,
                ProcurementSourceReference = po.ProcurementSourceReference
            }).ToList();

            await PopulateCurrentStepNamesAsync(purchaseOrderDtos);

            var result = new PagedResult<PurchaseOrderSummaryDto>
            {
                Items = purchaseOrderDtos,
                TotalCount = purchaseOrders.TotalCount,
                Page = purchaseOrders.Page,
                PageSize = purchaseOrders.PageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase orders");
            return StatusCode(500, "An error occurred while retrieving purchase orders");
        }
    }

    /// <summary>
    /// Gets a purchase order by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PurchaseOrderDetailDto>> GetPurchaseOrder(Guid id)
    {
        try
        {
            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            var items = await _purchaseOrderItemRepository.GetItemsByPurchaseOrderIdAsync(id);
            var receipts = await _purchaseOrderReceiptRepository.GetReceiptsByPurchaseOrderIdAsync(id);

            var purchaseOrderDto = new PurchaseOrderDetailDto
            {
                Id = purchaseOrder.Id,
                OrderNumber = purchaseOrder.OrderNumber,
                OrderType = purchaseOrder.OrderType,
                SupplierId = purchaseOrder.BusinessPartnerId,
                SupplierName = purchaseOrder.BusinessPartner?.PartnerName ?? "",
                OrderDate = purchaseOrder.OrderDate,
                RequiredDate = purchaseOrder.RequiredDate,
                PromisedDate = purchaseOrder.PromisedDate,
                ReceivedDate = purchaseOrder.ReceivedDate,
                Status = purchaseOrder.Status,
                ApprovedByName = purchaseOrder.ApprovedBy?.FirstName + " " + purchaseOrder.ApprovedBy?.LastName,
                ApprovedAt = purchaseOrder.ApprovedAt,
                SubTotal = purchaseOrder.SubTotal,
                TaxAmount = purchaseOrder.TaxAmount,
                ShippingCost = purchaseOrder.ShippingCost,
                MiscellaneousCost = purchaseOrder.MiscellaneousCost,
                TotalAdditionalCost = purchaseOrder.TotalAdditionalCost,
                CostAllocationMethod = purchaseOrder.CostAllocationMethod,
                CostApportionmentBasis = purchaseOrder.CostApportionmentBasis,
                ExpenseGLAccount = purchaseOrder.ExpenseGLAccount,
                CostsAllocated = purchaseOrder.CostsAllocated,
                DiscountAmount = purchaseOrder.DiscountAmount,
                TotalAmount = purchaseOrder.TotalAmount,
                PaymentTerms = purchaseOrder.PaymentTerms,
                ShippingTerms = purchaseOrder.ShippingTerms,
                Terms = purchaseOrder.Terms,
                Notes = purchaseOrder.Notes,
                DeliveryWarehouseId = purchaseOrder.DeliveryWarehouseId,
                DeliveryAddress = purchaseOrder.DeliveryAddress,
                DeliveryInstructions = purchaseOrder.DeliveryInstructions,
                SupplierOrderNumber = purchaseOrder.BusinessPartnerOrderNumber,
                ReferenceNumber = purchaseOrder.ReferenceNumber,
                ItemCount = items.Count(),
                RequestedByName = purchaseOrder.RequestedBy?.FirstName + " " + purchaseOrder.RequestedBy?.LastName,
                SupplierPhone = purchaseOrder.BusinessPartner?.PrimaryPhone,
                SupplierEmail = purchaseOrder.BusinessPartner?.PrimaryEmail,
                SupplierAddress = $"{purchaseOrder.BusinessPartner?.PhysicalAddress}, {purchaseOrder.BusinessPartner?.PhysicalCity}, {purchaseOrder.BusinessPartner?.PhysicalState} {purchaseOrder.BusinessPartner?.PhysicalPostalCode}",
                ProcurementSourceType = purchaseOrder.ProcurementSourceType,
                ProcurementSourceId = purchaseOrder.ProcurementSourceId,
                ProcurementSourceReference = purchaseOrder.ProcurementSourceReference,
                SourceRequisitionId = purchaseOrder.SourceRequisitionId,
                SourceRequisitionNumber = purchaseOrder.SourceRequisitionNumber,
                SourcingReleaseId = purchaseOrder.SourcingReleaseId,
                SourcingCaseId = purchaseOrder.SourcingCaseId,
                AwardReadinessDecisionId = purchaseOrder.AwardReadinessDecisionId,
                SourceIntegrityHash = purchaseOrder.SourceIntegrityHash,
                SourceValidatedAtUtc = purchaseOrder.SourceValidatedAtUtc,
                
                // Tender/Contract Integration
                TenderAwardId = purchaseOrder.TenderAwardId,
                TenderNumber = purchaseOrder.TenderNumber,
                ContractId = purchaseOrder.ContractId,
                ContractNumber = purchaseOrder.ContractNumber,
                IsFromTender = purchaseOrder.TenderAwardId.HasValue,
                IsFromContract = purchaseOrder.ContractId.HasValue,
                
                // Contract Utilization
                ContractValue = purchaseOrder.ContractValue,
                ContractUsedValue = purchaseOrder.ContractUsedValue,
                ContractRemainingValue = purchaseOrder.ContractRemainingValue,
                ContractUtilizationPercent = purchaseOrder.ContractValue.HasValue && purchaseOrder.ContractValue.Value > 0
                    ? (purchaseOrder.ContractUsedValue ?? 0) / purchaseOrder.ContractValue.Value * 100
                    : null,
                Items = items.Select(item => new PurchaseOrderItemDto
                {
                    Id = item.Id,
                    PurchaseOrderId = item.PurchaseOrderId,
                    InventoryItemId = item.InventoryItemId ?? Guid.Empty,
                    SupplierItemCode = item.BusinessPartnerItemCode,
                    ItemDescription = item.ItemDescription,
                    OrderedQuantity = item.OrderedQuantity,
                    UnitOfMeasure = item.UnitOfMeasure,
                    ItemUnitOfMeasureId = item.ItemUnitOfMeasureId,
                    WarehouseId = item.WarehouseId,
                    WarehouseName = item.Warehouse?.Name,
                    ReceivedQuantity = item.ReceivedQuantity,
                    RemainingQuantity = item.OrderedQuantity - item.ReceivedQuantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal,
                    AllocatedAdditionalCost = item.AllocatedAdditionalCost,
                    AllocatedCostPerUnit = item.AllocatedCostPerUnit,
                    LandedUnitCost = item.LandedUnitCost,
                    ExpectedDeliveryDate = item.ExpectedDeliveryDate,
                    Notes = item.Notes,
                    ItemCode = item.InventoryItem?.ItemCode ?? "",
                    ItemName = item.InventoryItem?.Name ?? ""
                }).ToList(),
                Receipts = receipts.Select(receipt => new PurchaseOrderReceiptDto
                {
                    Id = receipt.Id,
                    PurchaseOrderId = receipt.PurchaseOrderId,
                    ReceiptNumber = receipt.ReceiptNumber,
                    ReceiptDate = receipt.ReceiptDate,
                    DeliveryNote = receipt.DeliveryNote,
                    CarrierName = receipt.CarrierName,
                    TrackingNumber = receipt.TrackingNumber,
                    Status = receipt.Status,
                    ReceivedByName = receipt.ReceivedBy?.FirstName + " " + receipt.ReceivedBy?.LastName,
                    InspectedByName = receipt.InspectedBy?.FirstName + " " + receipt.InspectedBy?.LastName,
                    Notes = receipt.Notes,
                    RequiresInspection = receipt.RequiresInspection,
                    InspectionDate = receipt.InspectionDate,
                    InspectionResult = receipt.InspectionResult,
                    InspectionNotes = receipt.InspectionNotes,
                    PurchaseOrderNumber = purchaseOrder.OrderNumber,
                    SupplierName = purchaseOrder.BusinessPartner?.PartnerName ?? ""
                }).ToList()
            };

            purchaseOrderDto.CurrentWorkflowStepName = await GetCurrentStepNameAsync(purchaseOrderDto.Id, purchaseOrderDto.Status);

            return Ok(purchaseOrderDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase order {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while retrieving the purchase order");
        }
    }

    /// <summary>
    /// Creates a new purchase order
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDetailDto>> CreatePurchaseOrder([FromBody] CreatePurchaseOrderDto createDto)
    {
        var ownsSourceClaimTransaction = false;
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get tenant ID from current user
            var tenantId = _currentUserService.TenantId;
            if (tenantId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Tenant not found");
            }

            var orderType = NormalizeOrderType(createDto.OrderType);
            if (string.Equals(
                    orderType, "FrameworkCallOff", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    code = "FRAMEWORK_CALL_OFF_DEDICATED_ROUTE_REQUIRED",
                    message = "Framework call-offs must be created through /api/procurement/framework-call-offs so agreement, price, demand, authority, approval, and balance controls cannot be bypassed."
                });
            }
            if (!createDto.SourceType.HasValue ||
                !createDto.SourceId.HasValue ||
                createDto.SourceId.Value == Guid.Empty)
            {
                return UnprocessableEntity(new
                {
                    code = "PO_SOURCE_REQUIRED",
                    message = "Select an approved sourcing, award, contract, or exception source before creating a purchase order."
                });
            }
            var correlationId = CorrelationId();
            var approvedSource = await _purchaseOrderSources.ResolveAsync(
                createDto.SourceType.Value,
                createDto.SourceId.Value,
                createDto.SupplierId,
                correlationId,
                HttpContext.RequestAborted);
            await _supplierValidation.EnforceEligibilityAsync(new SupplierEligibilityEvaluationRequest
            {
                BusinessPartnerId = createDto.SupplierId,
                Boundary = string.Equals(orderType, "Blanket", StringComparison.OrdinalIgnoreCase)
                    ? SupplierEligibilityBoundary.FrameworkCallOff
                    : SupplierEligibilityBoundary.ManualPurchaseOrder,
                RecordAudit = true,
                SourceType = "PurchaseOrder",
                SourceReference = string.IsNullOrWhiteSpace(createDto.ReferenceNumber)
                    ? orderType
                    : createDto.ReferenceNumber,
                CorrelationId = Request.Headers["X-Correlation-ID"].FirstOrDefault() ??
                    Guid.NewGuid().ToString("N")
            });

            if (string.Equals(orderType, "Consignment", StringComparison.OrdinalIgnoreCase))
            {
                if (!createDto.DeliveryWarehouseId.HasValue || createDto.DeliveryWarehouseId.Value == Guid.Empty)
                {
                    return BadRequest("DeliveryWarehouseId is required for consignment purchase orders.");
                }

                var consignmentWarehouse = await _warehouseRepository.GetByIdAsync(createDto.DeliveryWarehouseId.Value);
                if (consignmentWarehouse == null)
                {
                    return BadRequest($"Delivery warehouse with ID {createDto.DeliveryWarehouseId.Value} not found");
                }

                if (!consignmentWarehouse.IsConsignmentWarehouse)
                {
                    return BadRequest($"Delivery warehouse '{consignmentWarehouse.Name}' is not marked as a consignment warehouse.");
                }

                foreach (var item in createDto.Items)
                {
                    if (!item.WarehouseId.HasValue || item.WarehouseId.Value == Guid.Empty)
                    {
                        item.WarehouseId = createDto.DeliveryWarehouseId.Value;
                    }
                    else if (item.WarehouseId.Value != createDto.DeliveryWarehouseId.Value)
                    {
                        return BadRequest("All items in a consignment purchase order must use the selected consignment DeliveryWarehouseId.");
                    }
                }
            }

            // Generate purchase order number
            var orderNumber = await _purchaseOrderRepository.GenerateOrderNumberAsync();

            // Calculate totals
            var subtotal = createDto.Items.Sum(item => item.OrderedQuantity * item.UnitPrice);
            var taxAmount = createDto.TaxAmount ?? 0;
            var shippingCost = createDto.ShippingCost ?? 0;
            var miscellaneousCost = createDto.MiscellaneousCost ?? 0;
            var totalAdditionalCost = shippingCost + miscellaneousCost;
            var discountAmount = createDto.DiscountAmount ?? 0;
            var totalAmount = subtotal + taxAmount + totalAdditionalCost - discountAmount;
            var costAllocationMethod = NormalizeCostAllocationMethod(createDto.CostAllocationMethod);
            var costApportionmentBasis = NormalizeCostApportionmentBasis(createDto.CostApportionmentBasis);
            var proposedSourceLines = createDto.Items.Select(item =>
                new ProcurementPurchaseOrderSourceOrderLine
                {
                    InventoryItemId = item.InventoryItemId,
                    ItemDescription = item.ItemDescription,
                    OrderedQuantity = item.OrderedQuantity,
                    UnitOfMeasure = item.UnitOfMeasure,
                    UnitPrice = item.UnitPrice
                }).ToList();
            await _purchaseOrderSources.ValidateOrderAsync(
                approvedSource,
                proposedSourceLines,
                totalAmount,
                approvedSource.CurrencyCode,
                correlationId,
                HttpContext.RequestAborted);

            if (costAllocationMethod == GLExpense && string.IsNullOrWhiteSpace(createDto.ExpenseGLAccount))
            {
                return BadRequest("ExpenseGLAccount is required when CostAllocationMethod is GLExpense.");
            }

            var purchaseOrder = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = orderNumber,
                BusinessPartnerId = createDto.SupplierId,
                OrderDate = DateTime.UtcNow,
                RequiredDate = createDto.RequiredDate,
                PromisedDate = createDto.PromisedDate,
                Status = "Draft",
                SubTotal = subtotal,
                TaxAmount = taxAmount,
                ShippingCost = shippingCost,
                MiscellaneousCost = miscellaneousCost,
                TotalAdditionalCost = totalAdditionalCost,
                CostAllocationMethod = costAllocationMethod,
                CostApportionmentBasis = costApportionmentBasis,
                ExpenseGLAccount = costAllocationMethod == GLExpense ? createDto.ExpenseGLAccount?.Trim() : null,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                PaymentTerms = createDto.PaymentTerms,
                ShippingTerms = createDto.ShippingTerms,
                Terms = createDto.Terms,
                Notes = createDto.Notes,
                DeliveryWarehouseId = createDto.DeliveryWarehouseId,
                DeliveryAddress = createDto.DeliveryAddress,
                DeliveryInstructions = createDto.DeliveryInstructions,
                ReferenceNumber = createDto.ReferenceNumber,
                RequestedById = createDto.RequestedById,
                OrderType = orderType,
                TenantId = tenantId
            };
            _purchaseOrderSources.Apply(purchaseOrder, approvedSource);

            ownsSourceClaimTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsSourceClaimTransaction)
            {
                await _unitOfWork.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    HttpContext.RequestAborted);
            }
            await _purchaseOrderSources.ReserveAsync(
                approvedSource,
                proposedSourceLines,
                totalAmount,
                approvedSource.CurrencyCode,
                purchaseOrder.Id,
                correlationId,
                HttpContext.RequestAborted);

            await _purchaseOrderRepository.CreatePurchaseOrderAsync(purchaseOrder);

            var newItems = createDto.Items.Select(itemDto => new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = purchaseOrder.Id,
                InventoryItemId = itemDto.InventoryItemId,
                BusinessPartnerItemCode = itemDto.SupplierItemCode,
                ItemDescription = itemDto.ItemDescription,
                OrderedQuantity = itemDto.OrderedQuantity,
                ReceivedQuantity = 0,
                UnitOfMeasure = itemDto.UnitOfMeasure ?? "EA",
                ItemUnitOfMeasureId = itemDto.ItemUnitOfMeasureId,
                WarehouseId = itemDto.WarehouseId,
                UnitPrice = itemDto.UnitPrice,
                PriceListLineId = itemDto.PriceListLineId,
                LineTotal = itemDto.OrderedQuantity * itemDto.UnitPrice,
                ExpectedDeliveryDate = itemDto.ExpectedDeliveryDate,
                Notes = itemDto.Notes,
                TenantId = tenantId
            }).ToList();

            await ApplyPurchaseOrderCostAllocationAsync(
                purchaseOrder,
                newItems,
                totalAdditionalCost,
                costAllocationMethod,
                costApportionmentBasis);
            purchaseOrder.CostsAllocated = costAllocationMethod == SpreadToItemCost && totalAdditionalCost > 0;

            // Create purchase order items
            foreach (var item in newItems)
            {
                await _purchaseOrderItemRepository.CreateItemAsync(item);
            }

            // CRITICAL: Save changes to database
            await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
            if (approvedSource.SourceType ==
                ProcurementPurchaseOrderSourceType.TenderAward)
            {
                await _purchaseOrderSources.ClaimTenderAwardAsync(
                    approvedSource.SourceId,
                    purchaseOrder,
                    HttpContext.RequestAborted);
            }
            await _purchaseOrderSources.RecordBoundAsync(
                purchaseOrder,
                "PurchaseOrderCreated",
                correlationId,
                HttpContext.RequestAborted);
            if (ownsSourceClaimTransaction)
                await _unitOfWork.CommitAsync(HttpContext.RequestAborted);

            // Return the created purchase order
            var createdPurchaseOrder = await GetPurchaseOrderDetailDto(purchaseOrder.Id);
            return CreatedAtAction(nameof(GetPurchaseOrder), new { id = purchaseOrder.Id }, createdPurchaseOrder);
        }
        catch (SupplierEligibilityException ex)
        {
            _logger.LogWarning(ex,
                "Supplier eligibility denied purchase order creation for {SupplierId}", createDto.SupplierId);
            return UnprocessableEntity(new
            {
                code = ex.Code,
                message = ex.Message,
                eligibility = ex.Result
            });
        }
        catch (ProcurementPurchaseOrderSourceValidationException ex)
        {
            return UnprocessableEntity(new
            {
                code = ex.Code,
                message = ex.Message,
                correlationId = CorrelationId()
            });
        }
        catch (ProcurementPurchaseOrderSourceAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "PO_SOURCE_FORBIDDEN",
                message = ex.Message,
                correlationId = CorrelationId()
            });
        }
        catch (ProcurementAccessAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "PO_SOURCE_FORBIDDEN",
                message = ex.Message,
                correlationId = CorrelationId()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase order");
            return StatusCode(500, "An error occurred while creating the purchase order");
        }
        finally
        {
            if (ownsSourceClaimTransaction && _unitOfWork.HasActiveTransaction)
            {
                try
                {
                    await _unitOfWork.RollbackAsync(HttpContext.RequestAborted);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(
                        rollbackException,
                        "Failed to roll back purchase-order source claim");
                }
            }
        }
    }

    /// <summary>
    /// Updates an existing purchase order (only Draft status)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PurchaseOrderDetailDto>> UpdatePurchaseOrder(Guid id, [FromBody] CreatePurchaseOrderDto updateDto)
    {
        var ownsSourceCapacityTransaction = false;
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            if (await IsFrameworkCallOffAsync(id))
            {
                return Conflict(new
                {
                    code = "FRAMEWORK_CALL_OFF_DEDICATED_ROUTE_REQUIRED",
                    message = "A framework call-off PO can be changed only through its dedicated call-off lifecycle."
                });
            }

            // Only allow editing Draft purchase orders
            if (purchaseOrder.Status != "Draft")
            {
                return BadRequest($"Only draft purchase orders can be edited. Current status: {purchaseOrder.Status}");
            }
            if (updateDto.SourceType != purchaseOrder.ProcurementSourceType ||
                updateDto.SourceId != purchaseOrder.ProcurementSourceId)
            {
                return Conflict(new
                {
                    code = "PO_SOURCE_IMMUTABLE",
                    message = "The approved purchase-order source cannot be replaced through draft editing."
                });
            }
            if (updateDto.SupplierId != purchaseOrder.BusinessPartnerId)
            {
                return Conflict(new
                {
                    code = "PO_SOURCE_SUPPLIER_IMMUTABLE",
                    message = "The supplier is owned by the approved source and cannot be changed."
                });
            }
            var sourceCorrelationId = CorrelationId();
            var currentSource = await _purchaseOrderSources.RevalidateAsync(
                purchaseOrder,
                "Update",
                sourceCorrelationId,
                HttpContext.RequestAborted);

            // Get tenant ID from current user
            var tenantId = _currentUserService.TenantId;
            if (tenantId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Tenant not found");
            }

            // Calculate totals
            var subtotal = updateDto.Items.Sum(item => item.OrderedQuantity * item.UnitPrice);
            var taxAmount = updateDto.TaxAmount ?? 0;
            var shippingCost = updateDto.ShippingCost ?? 0;
            var miscellaneousCost = updateDto.MiscellaneousCost ?? 0;
            var totalAdditionalCost = shippingCost + miscellaneousCost;
            var discountAmount = updateDto.DiscountAmount ?? 0;
            var totalAmount = subtotal + taxAmount + totalAdditionalCost - discountAmount;
            var costAllocationMethod = NormalizeCostAllocationMethod(updateDto.CostAllocationMethod);
            var costApportionmentBasis = NormalizeCostApportionmentBasis(updateDto.CostApportionmentBasis);
            var proposedSourceLines = updateDto.Items.Select(item =>
                new ProcurementPurchaseOrderSourceOrderLine
                {
                    InventoryItemId = item.InventoryItemId,
                    ItemDescription = item.ItemDescription,
                    OrderedQuantity = item.OrderedQuantity,
                    UnitOfMeasure = item.UnitOfMeasure,
                    UnitPrice = item.UnitPrice
                }).ToList();
            await _purchaseOrderSources.ValidateOrderAsync(
                currentSource,
                proposedSourceLines,
                totalAmount,
                purchaseOrder.Currency,
                sourceCorrelationId,
                HttpContext.RequestAborted);

            if (costAllocationMethod == GLExpense && string.IsNullOrWhiteSpace(updateDto.ExpenseGLAccount))
            {
                return BadRequest("ExpenseGLAccount is required when CostAllocationMethod is GLExpense.");
            }

            // Update purchase order fields
            purchaseOrder.RequiredDate = updateDto.RequiredDate;
            purchaseOrder.PromisedDate = updateDto.PromisedDate;
            purchaseOrder.PaymentTerms = updateDto.PaymentTerms;
            purchaseOrder.ShippingTerms = updateDto.ShippingTerms;
            purchaseOrder.Terms = updateDto.Terms;
            purchaseOrder.Notes = updateDto.Notes;
            purchaseOrder.DeliveryWarehouseId = updateDto.DeliveryWarehouseId;
            purchaseOrder.DeliveryAddress = updateDto.DeliveryAddress;
            purchaseOrder.DeliveryInstructions = updateDto.DeliveryInstructions;
            purchaseOrder.ReferenceNumber = updateDto.ReferenceNumber;
            purchaseOrder.SubTotal = subtotal;
            purchaseOrder.TaxAmount = taxAmount;
            purchaseOrder.ShippingCost = shippingCost;
            purchaseOrder.MiscellaneousCost = miscellaneousCost;
            purchaseOrder.TotalAdditionalCost = totalAdditionalCost;
            purchaseOrder.CostAllocationMethod = costAllocationMethod;
            purchaseOrder.CostApportionmentBasis = costApportionmentBasis;
            purchaseOrder.ExpenseGLAccount = costAllocationMethod == GLExpense ? updateDto.ExpenseGLAccount?.Trim() : null;
            purchaseOrder.DiscountAmount = discountAmount;
            purchaseOrder.TotalAmount = totalAmount;

            var orderType = NormalizeOrderType(updateDto.OrderType ?? purchaseOrder.OrderType);
            if (string.Equals(orderType, "Consignment", StringComparison.OrdinalIgnoreCase))
            {
                if (!updateDto.DeliveryWarehouseId.HasValue || updateDto.DeliveryWarehouseId.Value == Guid.Empty)
                {
                    return BadRequest("DeliveryWarehouseId is required for consignment purchase orders.");
                }

                var consignmentWarehouse = await _warehouseRepository.GetByIdAsync(updateDto.DeliveryWarehouseId.Value);
                if (consignmentWarehouse == null)
                {
                    return BadRequest($"Delivery warehouse with ID {updateDto.DeliveryWarehouseId.Value} not found");
                }

                if (!consignmentWarehouse.IsConsignmentWarehouse)
                {
                    return BadRequest($"Delivery warehouse '{consignmentWarehouse.Name}' is not marked as a consignment warehouse.");
                }

                foreach (var item in updateDto.Items)
                {
                    if (!item.WarehouseId.HasValue || item.WarehouseId.Value == Guid.Empty)
                    {
                        item.WarehouseId = updateDto.DeliveryWarehouseId.Value;
                    }
                    else if (item.WarehouseId.Value != updateDto.DeliveryWarehouseId.Value)
                    {
                        return BadRequest("All items in a consignment purchase order must use the selected consignment DeliveryWarehouseId.");
                    }
                }
            }

            ownsSourceCapacityTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsSourceCapacityTransaction)
            {
                await _unitOfWork.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    HttpContext.RequestAborted);
            }
            await _purchaseOrderSources.ReserveAsync(
                currentSource,
                proposedSourceLines,
                totalAmount,
                purchaseOrder.Currency,
                purchaseOrder.Id,
                sourceCorrelationId,
                HttpContext.RequestAborted);

            purchaseOrder.OrderType = orderType;

            await _purchaseOrderRepository.UpdatePurchaseOrderAsync(purchaseOrder);

            // Delete existing items
            var existingItems = await _purchaseOrderItemRepository.GetItemsByPurchaseOrderIdAsync(id);
            foreach (var existingItem in existingItems)
            {
                await _purchaseOrderItemRepository.DeleteItemAsync(existingItem.Id);
            }

            // Create new items
            var recreatedItems = new List<PurchaseOrderItem>();
            foreach (var itemDto in updateDto.Items)
            {
                // Skip items with empty InventoryItemId
                if (itemDto.InventoryItemId == Guid.Empty)
                {
                    _logger.LogWarning("Skipping item with empty InventoryItemId in purchase order update");
                    continue;
                }
                
                recreatedItems.Add(new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = purchaseOrder.Id,
                    InventoryItemId = itemDto.InventoryItemId,
                    BusinessPartnerItemCode = itemDto.SupplierItemCode,
                    ItemDescription = itemDto.ItemDescription,
                    OrderedQuantity = itemDto.OrderedQuantity,
                    ReceivedQuantity = 0,
                    UnitOfMeasure = itemDto.UnitOfMeasure ?? "EA",
                    ItemUnitOfMeasureId = itemDto.ItemUnitOfMeasureId,
                    WarehouseId = itemDto.WarehouseId,
                    UnitPrice = itemDto.UnitPrice,
                    PriceListLineId = itemDto.PriceListLineId,
                    LineTotal = itemDto.OrderedQuantity * itemDto.UnitPrice,
                    ExpectedDeliveryDate = itemDto.ExpectedDeliveryDate,
                    Notes = itemDto.Notes,
                    TenantId = tenantId
                });
            }

            await ApplyPurchaseOrderCostAllocationAsync(
                purchaseOrder,
                recreatedItems,
                totalAdditionalCost,
                costAllocationMethod,
                costApportionmentBasis);
            purchaseOrder.CostsAllocated = costAllocationMethod == SpreadToItemCost && totalAdditionalCost > 0;

            foreach (var item in recreatedItems)
            {
                await _purchaseOrderItemRepository.CreateItemAsync(item);
            }

            // CRITICAL: Save changes to database
            await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
            if (ownsSourceCapacityTransaction)
                await _unitOfWork.CommitAsync(HttpContext.RequestAborted);

            // Return the updated purchase order
            var updatedPurchaseOrder = await GetPurchaseOrderDetailDto(purchaseOrder.Id);
            return Ok(updatedPurchaseOrder);
        }
        catch (ProcurementPurchaseOrderSourceValidationException ex)
        {
            return UnprocessableEntity(new
            {
                code = ex.Code,
                message = ex.Message,
                correlationId = CorrelationId()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating purchase order {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while updating the purchase order");
        }
        finally
        {
            if (ownsSourceCapacityTransaction && _unitOfWork.HasActiveTransaction)
            {
                try
                {
                    await _unitOfWork.RollbackAsync(HttpContext.RequestAborted);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(
                        rollbackException,
                        "Failed to roll back purchase-order source capacity reservation");
                }
            }
        }
    }

    /// <summary>
    /// Updates purchase order status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdatePurchaseOrderStatus(Guid id, [FromBody] UpdateStatusDto statusDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            if (await IsFrameworkCallOffAsync(id))
            {
                return Conflict(new
                {
                    code = "FRAMEWORK_CALL_OFF_DEDICATED_ROUTE_REQUIRED",
                    message = "A framework call-off PO status can be changed only through its dedicated call-off lifecycle."
                });
            }
            if (!string.Equals(statusDto.Status, "Draft", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(statusDto.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(statusDto.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                await _purchaseOrderSources.RevalidateAsync(
                    purchaseOrder,
                    $"Status{statusDto.Status}",
                    CorrelationId(),
                    HttpContext.RequestAborted);
            }

            await _purchaseOrderRepository.UpdateStatusAsync(id, statusDto.Status);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (ProcurementPurchaseOrderSourceValidationException ex)
        {
            return UnprocessableEntity(new
            {
                code = ex.Code,
                message = ex.Message,
                correlationId = CorrelationId()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating purchase order status {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while updating purchase order status");
        }
    }

    /// <summary>
    /// Approves a purchase order
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApprovePurchaseOrder(Guid id, [FromBody] ApprovalDto approvalDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!_currentUserService.IsAuthenticated)
            {
                return Unauthorized("User is not authenticated");
            }

            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            if (await IsFrameworkCallOffAsync(id))
            {
                return Conflict(new
                {
                    code = "FRAMEWORK_CALL_OFF_DEDICATED_ROUTE_REQUIRED",
                    message = "Approve or reject this framework call-off through /api/procurement/framework-call-offs/{id}/decision."
                });
            }

            if (purchaseOrder.Status != "Pending Approval" && purchaseOrder.Status != "Draft")
            {
                return BadRequest($"Purchase order cannot be approved in current status: {purchaseOrder.Status}");
            }
            if (approvalDto.Approved)
            {
                await _purchaseOrderSources.RevalidateAsync(
                    purchaseOrder,
                    "Approve",
                    CorrelationId(),
                    HttpContext.RequestAborted);
            }

            var userId = _currentUserService.UserId;
            if (userId == Guid.Empty)
            {
                return Unauthorized("User identifier claim is missing or invalid");
            }

            var canApprove = await _workflowIntegrationService.CanUserApproveAsync("PurchaseOrder", id, userId);
            if (!canApprove)
            {
                return StatusCode(403, "You are not assigned as an approver for the current workflow step");
            }

            var action = approvalDto.Approved ? "approve" : "reject";
            var comments = approvalDto.Comments;
            if (!approvalDto.Approved && string.IsNullOrWhiteSpace(comments))
            {
                comments = approvalDto.RejectionReason;
            }

            if (!approvalDto.Approved && string.IsNullOrWhiteSpace(comments))
            {
                return BadRequest("Rejection comment is required");
            }

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                "PurchaseOrder",
                id,
                userId,
                action,
                comments);

            var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("PurchaseOrder");
            statusAdapter.ApplyApprovalOutcome(purchaseOrder, workflowResult.Outcome, userId);

            purchaseOrder.UpdatedAt = DateTime.UtcNow;
            await _purchaseOrderRepository.UpdatePurchaseOrderAsync(purchaseOrder);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Purchase order submitted successfully",
                purchaseOrderId = purchaseOrder.Id,
                purchaseOrderNumber = purchaseOrder.OrderNumber,
                purchaseOrderStatus = purchaseOrder.Status,
                workflowOutcome = workflowResult.Outcome.ToString(),
                workflowStatus = workflowResult.ExecutionResult.Status.ToString(),
                workflowInstanceId = workflowResult.ExecutionResult.WorkflowInstanceId,
                currentStepId = workflowResult.ExecutionResult.CurrentStepId
            });
        }
        catch (ProcurementPurchaseOrderSourceValidationException ex)
        {
            return UnprocessableEntity(new
            {
                code = ex.Code,
                message = ex.Message,
                correlationId = CorrelationId()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving purchase order {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while approving the purchase order");
        }
    }

    /// <summary>
    /// Submits a purchase order for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitPurchaseOrder(Guid id)
    {
        try
        {
            if (!_currentUserService.IsAuthenticated)
            {
                return Unauthorized();
            }

            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            if (await IsFrameworkCallOffAsync(id))
            {
                return Conflict(new
                {
                    code = "FRAMEWORK_CALL_OFF_DEDICATED_ROUTE_REQUIRED",
                    message = "Submit this framework call-off through /api/procurement/framework-call-offs/{id}/submit."
                });
            }

            if (purchaseOrder.Status != "Draft")
            {
                return BadRequest($"Purchase order cannot be submitted in current status: {purchaseOrder.Status}");
            }
            await _purchaseOrderSources.RevalidateAsync(
                purchaseOrder,
                "Submit",
                CorrelationId(),
                HttpContext.RequestAborted);

            WorkflowIntegrationResult workflowResult;
            try
            {
                workflowResult = await _workflowIntegrationService.SubmitAsync("PurchaseOrder", id);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("PurchaseOrder");
            statusAdapter.ApplySubmitOutcome(purchaseOrder, workflowResult.Outcome, _currentUserService.UserId);

            if (!purchaseOrder.RequestedById.HasValue)
            {
                purchaseOrder.RequestedById = _currentUserService.UserId;
            }
            purchaseOrder.UpdatedAt = DateTime.UtcNow;

            await _purchaseOrderRepository.UpdatePurchaseOrderAsync(purchaseOrder);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (ProcurementPurchaseOrderSourceValidationException ex)
        {
            return UnprocessableEntity(new
            {
                code = ex.Code,
                message = ex.Message,
                correlationId = CorrelationId()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting purchase order {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while submitting the purchase order");
        }
    }

    /// <summary>
    /// Receives a purchase order (creates receipt)
    /// </summary>
    [HttpPost("{id}/receive")]
    public async Task<ActionResult<PurchaseOrderReceiptDto>> ReceivePurchaseOrder(
        Guid id,
        [FromBody] ReceivePurchaseOrderDto receiveDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            // Allow multiple receipts until fully received/closed.
            // "Partially Received" should still be receiptable.
            if (!string.Equals(purchaseOrder.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(purchaseOrder.Status, "Partially Received", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Purchase order cannot be received in current status: {purchaseOrder.Status}");
            }

            // Generate receipt number
            var receiptNumber = await _purchaseOrderReceiptRepository.GenerateReceiptNumberAsync();

            // Create receipt
            var receipt = new PurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = id,
                ReceiptNumber = receiptNumber,
                ReceiptDate = DateTime.UtcNow,
                DeliveryNote = receiveDto.DeliveryNote,
                CarrierName = receiveDto.CarrierName,
                TrackingNumber = receiveDto.TrackingNumber,
                Status = receiveDto.RequiresInspection ? "Pending Inspection" : "Received",
                ReceivedById = receiveDto.ReceivedById,
                InspectedById = receiveDto.InspectedById,
                Notes = receiveDto.Notes,
                RequiresInspection = receiveDto.RequiresInspection,
                TenantId = purchaseOrder.TenantId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _purchaseOrderReceiptRepository.CreateReceiptAsync(receipt);

            // Update purchase order items with received quantities and process inventory
            foreach (var itemDto in receiveDto.Items)
            {
                if (itemDto.ReceivedQuantity <= 0)
                {
                    continue;
                }

                if (itemDto.AcceptedQuantity < 0 || itemDto.RejectedQuantity < 0)
                {
                    return BadRequest("AcceptedQuantity and RejectedQuantity cannot be negative.");
                }

                if (itemDto.AcceptedQuantity + itemDto.RejectedQuantity > itemDto.ReceivedQuantity)
                {
                    return BadRequest("AcceptedQuantity + RejectedQuantity cannot exceed ReceivedQuantity.");
                }

                if (itemDto.RejectedQuantity > 0 && string.IsNullOrWhiteSpace(itemDto.RejectionReason))
                {
                    return BadRequest("RejectionReason is required when RejectedQuantity is greater than 0.");
                }

                var poItem = await _purchaseOrderItemRepository.GetItemByIdAsync(itemDto.PurchaseOrderItemId);
                if (poItem != null)
                {
                    // PO "ReceivedQuantity" is treated as the quantity actually receipted/accepted against the PO.
                    // If AcceptedQuantity < ReceivedQuantity (e.g., short-shipped or pending check), we do NOT
                    // automatically treat the difference as rejected.
                    var remainingToReceipt = poItem.OrderedQuantity - poItem.ReceivedQuantity;

                    if (remainingToReceipt < 0)
                    {
                        remainingToReceipt = 0;
                    }

                    if (itemDto.ReceivedQuantity > remainingToReceipt)
                    {
                        return BadRequest($"ReceivedQuantity ({itemDto.ReceivedQuantity}) cannot exceed remaining quantity to receipt ({remainingToReceipt}).");
                    }

                    if (itemDto.AcceptedQuantity > remainingToReceipt)
                    {
                        return BadRequest($"AcceptedQuantity ({itemDto.AcceptedQuantity}) cannot exceed remaining quantity to receipt ({remainingToReceipt}).");
                    }

                    // Only accepted quantity reduces PO remaining (supports partial receipts without implying rejection).
                    poItem.ReceivedQuantity += itemDto.AcceptedQuantity;
                    poItem.UpdatedAt = DateTime.UtcNow;
                    await _purchaseOrderItemRepository.UpdateItemAsync(poItem);

                    // Create receipt item
                    var effectiveWarehouseId = itemDto.WarehouseId ?? poItem.WarehouseId ?? purchaseOrder.DeliveryWarehouseId;
                    var effectiveLocationId = itemDto.LocationId;

                    // Storage/put-away location should come from the UI (per-line selection).
                    // If provided, use it to derive the warehouse, since receiving can override the PO-line warehouse.
                    if (effectiveLocationId.HasValue && effectiveLocationId.Value != Guid.Empty)
                    {
                        var location = await _unitOfWork.Repository<WarehouseLocation>()
                            .FirstOrDefaultAsync(l => l.Id == effectiveLocationId.Value && l.TenantId == purchaseOrder.TenantId);

                        if (location == null)
                        {
                            return BadRequest($"Invalid LocationId ({effectiveLocationId}). Please select a valid warehouse location.");
                        }

                        effectiveWarehouseId = location.InventoryWarehouseId;
                    }

                    // Enforce a put-away location for any non-zero receipt lines (so we can report accurately).
                    if (!effectiveLocationId.HasValue || effectiveLocationId.Value == Guid.Empty)
                    {
                        return BadRequest("LocationId is required for received items. Please select a storage location for each line item.");
                    }

                    if (string.Equals(purchaseOrder.OrderType, "Consignment", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!purchaseOrder.DeliveryWarehouseId.HasValue || purchaseOrder.DeliveryWarehouseId.Value == Guid.Empty)
                        {
                            return BadRequest("Consignment purchase orders must have DeliveryWarehouseId set to a consignment warehouse.");
                        }

                        if (!effectiveWarehouseId.HasValue || effectiveWarehouseId.Value == Guid.Empty)
                        {
                            return BadRequest("Warehouse could not be resolved for the selected LocationId.");
                        }

                        if (effectiveWarehouseId.Value != purchaseOrder.DeliveryWarehouseId.Value)
                        {
                            return BadRequest("Consignment purchase order receipts must be posted to the PO DeliveryWarehouseId (dedicated consignment warehouse).");
                        }

                        var wh = await _warehouseRepository.GetByIdAsync(effectiveWarehouseId.Value);
                        if (wh?.IsConsignmentWarehouse != true)
                        {
                            return BadRequest("Selected warehouse is not marked as a consignment warehouse.");
                        }
                    }
                    else
                    {
                        // Prevent accidentally receiving owned stock into consignment inventory bins/warehouses.
                        if (effectiveWarehouseId.HasValue && effectiveWarehouseId.Value != Guid.Empty)
                        {
                            var isConsignment = await IsConsignmentWarehouseAsync(effectiveWarehouseId.Value);
                            if (isConsignment)
                            {
                                return BadRequest("This receipt line resolves to a consignment warehouse/bin. Use a Consignment purchase order type to receive consignment stock.");
                            }
                        }
                    }

                    var receiptItem = new PurchaseOrderReceiptItem
                    {
                        Id = Guid.NewGuid(),
                        ReceiptId = receipt.Id,
                        PurchaseOrderItemId = itemDto.PurchaseOrderItemId,
                        ReceivedQuantity = itemDto.ReceivedQuantity,
                        AcceptedQuantity = itemDto.AcceptedQuantity,
                        RejectedQuantity = itemDto.RejectedQuantity,
                        UnitOfMeasure = poItem.UnitOfMeasure,
                        ItemUnitOfMeasureId = poItem.ItemUnitOfMeasureId,
                        LocationId = effectiveLocationId,
                        SerialNumber = itemDto.SerialNumber,
                        LotNumber = itemDto.LotNumber,
                        ExpirationDate = itemDto.ExpirationDate,
                        Notes = itemDto.Notes,
                        RejectionReason = itemDto.RejectionReason,
                        QualityStatus = itemDto.QualityStatus,
                        QualityNotes = itemDto.QualityNotes,
                        TenantId = purchaseOrder.TenantId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _purchaseOrderReceiptRepository.CreateReceiptItemAsync(receiptItem);

                    // Process inventory receipt if not requiring inspection or if already accepted
                    if (!receiveDto.RequiresInspection || string.Equals(itemDto.QualityStatus, "Passed", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only post accepted quantity to inventory; the remainder can be pending (uninspected) without being rejected.
                        if (itemDto.AcceptedQuantity > 0)
                        {
                            await ProcessInventoryReceiptAsync(
                                poItem,
                                itemDto.AcceptedQuantity,
                                effectiveWarehouseId ?? Guid.Empty,
                                effectiveLocationId,
                                receiptNumber,
                                receipt.Id,
                                itemDto.LotNumber,
                                itemDto.SerialNumber,
                                itemDto.ExpirationDate);
                        }
                    }
                }
            }

            // Check if purchase order is fully received
            var items = await _purchaseOrderItemRepository.GetItemsByPurchaseOrderIdAsync(id);
            var fullyReceived = items.All(item => item.ReceivedQuantity >= item.OrderedQuantity);

            if (fullyReceived)
            {
                await _purchaseOrderRepository.UpdateStatusAsync(id, "Received");
                purchaseOrder.ReceivedDate = DateTime.UtcNow;
                await _purchaseOrderRepository.UpdatePurchaseOrderAsync(purchaseOrder);

                // Move committed budget to utilized when PO is fully received
                await UtilizeBudgetForPurchaseOrderAsync(purchaseOrder);
            }
            else
            {
                await _purchaseOrderRepository.UpdateStatusAsync(id, "Partially Received");
            }

            // Persist receipt, item quantity updates, and PO status transitions.
            await _unitOfWork.SaveChangesAsync();
            await _projectService.SyncPurchaseReceiptMaterialCostAsync(receipt.Id);

            // Return the created receipt
            var receiptDto = new PurchaseOrderReceiptDto
            {
                Id = receipt.Id,
                PurchaseOrderId = receipt.PurchaseOrderId,
                ReceiptNumber = receipt.ReceiptNumber,
                ReceiptDate = receipt.ReceiptDate,
                DeliveryNote = receipt.DeliveryNote,
                CarrierName = receipt.CarrierName,
                TrackingNumber = receipt.TrackingNumber,
                Status = receipt.Status,
                Notes = receipt.Notes,
                RequiresInspection = receipt.RequiresInspection,
                PurchaseOrderNumber = purchaseOrder.OrderNumber,
                SupplierName = purchaseOrder.BusinessPartner?.PartnerName ?? ""
            };

            return CreatedAtAction(nameof(GetPurchaseOrder), new { id = purchaseOrder.Id }, receiptDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving purchase order {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while receiving the purchase order");
        }
    }

    /// <summary>
    /// Gets purchase orders by supplier
    /// </summary>
    [HttpGet("by-supplier/{supplierId}")]
    public async Task<ActionResult<List<PurchaseOrderSummaryDto>>> GetPurchaseOrdersBySupplier(Guid supplierId)
    {
        try
        {
            var purchaseOrders = await _purchaseOrderRepository.GetPurchaseOrdersBySupplierId(supplierId);

            var purchaseOrderDtos = purchaseOrders.Select(po => new PurchaseOrderSummaryDto
            {
                Id = po.Id,
                OrderNumber = po.OrderNumber,
                OrderType = po.OrderType,
                SupplierId = po.BusinessPartnerId,
                SupplierName = po.BusinessPartner?.PartnerName ?? "",
                OrderDate = po.OrderDate,
                RequiredDate = po.RequiredDate,
                PromisedDate = po.PromisedDate,
                Status = po.Status,
                TotalAmount = po.TotalAmount,
                ItemCount = po.Items?.Count ?? 0,
                RequestedByName = po.RequestedBy?.FirstName + " " + po.RequestedBy?.LastName,
                ProcurementSourceType = po.ProcurementSourceType,
                ProcurementSourceReference = po.ProcurementSourceReference
            }).ToList();

            return Ok(purchaseOrderDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase orders for supplier {SupplierId}", supplierId);
            return StatusCode(500, "An error occurred while retrieving purchase orders");
        }
    }

    /// <summary>
    /// Gets purchase orders by status
    /// </summary>
    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<List<PurchaseOrderSummaryDto>>> GetPurchaseOrdersByStatus(string status)
    {
        try
        {
            var purchaseOrders = await _purchaseOrderRepository.GetPurchaseOrdersByStatus(status);

            var purchaseOrderDtos = purchaseOrders.Select(po => new PurchaseOrderSummaryDto
            {
                Id = po.Id,
                OrderNumber = po.OrderNumber,
                OrderType = po.OrderType,
                SupplierId = po.BusinessPartnerId,
                SupplierName = po.BusinessPartner?.PartnerName ?? "",
                OrderDate = po.OrderDate,
                RequiredDate = po.RequiredDate,
                PromisedDate = po.PromisedDate,
                Status = po.Status,
                TotalAmount = po.TotalAmount,
                ItemCount = po.Items?.Count ?? 0,
                RequestedByName = po.RequestedBy?.FirstName + " " + po.RequestedBy?.LastName,
                ProcurementSourceType = po.ProcurementSourceType,
                ProcurementSourceReference = po.ProcurementSourceReference
            }).ToList();

            return Ok(purchaseOrderDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase orders by status {Status}", status);
            return StatusCode(500, "An error occurred while retrieving purchase orders");
        }
    }

    /// <summary>
    /// Completes inspection and posts receipt to inventory
    /// </summary>
    [HttpPost("receipts/{receiptId}/complete-inspection")]
    public async Task<IActionResult> CompleteInspectionAndPostToInventory(Guid receiptId)
    {
        try
        {
            var receipt = await _purchaseOrderReceiptRepository.GetByIdAsync(receiptId);
            if (receipt == null)
            {
                return NotFound($"Receipt with ID {receiptId} not found");
            }

            if (receipt.Status != "Pending Inspection" && receipt.Status != "Inspected")
            {
                return BadRequest($"Receipt cannot be completed in current status: {receipt.Status}");
            }

            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(receipt.PurchaseOrderId);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order not found for receipt {receiptId}");
            }

            // Get receipt items using the receipt item repository
            var allReceiptItems = await _purchaseOrderReceiptItemRepository.GetAllAsync();
            var receiptItems = allReceiptItems.Where(ri => ri.ReceiptId == receiptId).ToList();

            // Process inventory for accepted items
            foreach (var receiptItem in receiptItems)
            {
                if (receiptItem.AcceptedQuantity > 0 && receiptItem.QualityStatus == "Passed")
                {
                    var poItem = await _purchaseOrderItemRepository.GetItemByIdAsync(receiptItem.PurchaseOrderItemId);
                    if (poItem != null)
                    {
                        await ProcessInventoryReceiptAsync(
                            poItem,
                            receiptItem.AcceptedQuantity,
                            poItem.WarehouseId ?? purchaseOrder.DeliveryWarehouseId ?? Guid.Empty,
                            receiptItem.LocationId,
                            receipt.ReceiptNumber,
                            receipt.Id,
                            receiptItem.LotNumber,
                            receiptItem.SerialNumber,
                            receiptItem.ExpirationDate);
                    }
                }
            }

            // Update receipt status
            receipt.Status = "Accepted";
            receipt.InspectionDate = DateTime.UtcNow;
            receipt.UpdatedAt = DateTime.UtcNow;
            await _purchaseOrderReceiptRepository.UpdateAsync(receipt);
            await _unitOfWork.SaveChangesAsync();
            await _projectService.SyncPurchaseReceiptMaterialCostAsync(receipt.Id);

            _logger.LogInformation("Completed inspection and posted receipt {ReceiptNumber} to inventory", receipt.ReceiptNumber);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing inspection for receipt {ReceiptId}", receiptId);
            return StatusCode(500, "An error occurred while completing the inspection");
        }
    }

    #region Private Helper Methods

    private async Task<PurchaseOrderDetailDto> GetPurchaseOrderDetailDto(Guid purchaseOrderId)
    {
        var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(purchaseOrderId);
        if (purchaseOrder == null)
        {
            return null!;
        }

        var items = await _purchaseOrderItemRepository.GetItemsByPurchaseOrderIdAsync(purchaseOrderId);
        var receipts = await _purchaseOrderReceiptRepository.GetReceiptsByPurchaseOrderIdAsync(purchaseOrderId);

        return new PurchaseOrderDetailDto
        {
            Id = purchaseOrder.Id,
            OrderNumber = purchaseOrder.OrderNumber,
            OrderType = purchaseOrder.OrderType,
            SupplierId = purchaseOrder.BusinessPartnerId,
            SupplierName = purchaseOrder.BusinessPartner?.PartnerName ?? "",
            OrderDate = purchaseOrder.OrderDate,
            RequiredDate = purchaseOrder.RequiredDate,
            PromisedDate = purchaseOrder.PromisedDate,
            ReceivedDate = purchaseOrder.ReceivedDate,
            Status = purchaseOrder.Status,
            ApprovedByName = purchaseOrder.ApprovedBy?.FirstName + " " + purchaseOrder.ApprovedBy?.LastName,
            ApprovedAt = purchaseOrder.ApprovedAt,
            SubTotal = purchaseOrder.SubTotal,
            TaxAmount = purchaseOrder.TaxAmount,
            ShippingCost = purchaseOrder.ShippingCost,
            MiscellaneousCost = purchaseOrder.MiscellaneousCost,
            TotalAdditionalCost = purchaseOrder.TotalAdditionalCost,
            CostAllocationMethod = purchaseOrder.CostAllocationMethod,
            CostApportionmentBasis = purchaseOrder.CostApportionmentBasis,
            ExpenseGLAccount = purchaseOrder.ExpenseGLAccount,
            CostsAllocated = purchaseOrder.CostsAllocated,
            DiscountAmount = purchaseOrder.DiscountAmount,
            TotalAmount = purchaseOrder.TotalAmount,
            PaymentTerms = purchaseOrder.PaymentTerms,
            ShippingTerms = purchaseOrder.ShippingTerms,
            Terms = purchaseOrder.Terms,
            Notes = purchaseOrder.Notes,
            DeliveryWarehouseId = purchaseOrder.DeliveryWarehouseId,
            DeliveryAddress = purchaseOrder.DeliveryAddress,
            DeliveryInstructions = purchaseOrder.DeliveryInstructions,
            SupplierOrderNumber = purchaseOrder.BusinessPartnerOrderNumber,
            ReferenceNumber = purchaseOrder.ReferenceNumber,
            ItemCount = items.Count(),
            RequestedByName = purchaseOrder.RequestedBy?.FirstName + " " + purchaseOrder.RequestedBy?.LastName,
            SupplierPhone = purchaseOrder.BusinessPartner?.PrimaryPhone,
            SupplierEmail = purchaseOrder.BusinessPartner?.PrimaryEmail,
            SupplierAddress = $"{purchaseOrder.BusinessPartner?.PhysicalAddress}, {purchaseOrder.BusinessPartner?.PhysicalCity}, {purchaseOrder.BusinessPartner?.PhysicalState} {purchaseOrder.BusinessPartner?.PhysicalPostalCode}",
            ProcurementSourceType = purchaseOrder.ProcurementSourceType,
            ProcurementSourceId = purchaseOrder.ProcurementSourceId,
            ProcurementSourceReference = purchaseOrder.ProcurementSourceReference,
            SourceRequisitionId = purchaseOrder.SourceRequisitionId,
            SourceRequisitionNumber = purchaseOrder.SourceRequisitionNumber,
            SourcingReleaseId = purchaseOrder.SourcingReleaseId,
            SourcingCaseId = purchaseOrder.SourcingCaseId,
            AwardReadinessDecisionId = purchaseOrder.AwardReadinessDecisionId,
            SourceIntegrityHash = purchaseOrder.SourceIntegrityHash,
            SourceValidatedAtUtc = purchaseOrder.SourceValidatedAtUtc,
            
            // Tender/Contract Integration
            TenderAwardId = purchaseOrder.TenderAwardId,
            TenderNumber = purchaseOrder.TenderNumber,
            ContractId = purchaseOrder.ContractId,
            ContractNumber = purchaseOrder.ContractNumber,
            IsFromTender = purchaseOrder.TenderAwardId.HasValue,
            IsFromContract = purchaseOrder.ContractId.HasValue,
            
            // Contract Utilization
            ContractValue = purchaseOrder.ContractValue,
            ContractUsedValue = purchaseOrder.ContractUsedValue,
            ContractRemainingValue = purchaseOrder.ContractRemainingValue,
            ContractUtilizationPercent = purchaseOrder.ContractValue.HasValue && purchaseOrder.ContractValue.Value > 0
                ? (purchaseOrder.ContractUsedValue ?? 0) / purchaseOrder.ContractValue.Value * 100
                : null,
            Items = items.Select(item => new PurchaseOrderItemDto
            {
                Id = item.Id,
                PurchaseOrderId = item.PurchaseOrderId,
                InventoryItemId = item.InventoryItemId ?? Guid.Empty,
                SupplierItemCode = item.BusinessPartnerItemCode,
                ItemDescription = item.ItemDescription,
                OrderedQuantity = item.OrderedQuantity,
                UnitOfMeasure = item.UnitOfMeasure,
                ItemUnitOfMeasureId = item.ItemUnitOfMeasureId,
                WarehouseId = item.WarehouseId,
                WarehouseName = item.Warehouse?.Name,
                ReceivedQuantity = item.ReceivedQuantity,
                RemainingQuantity = item.OrderedQuantity - item.ReceivedQuantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal,
                AllocatedAdditionalCost = item.AllocatedAdditionalCost,
                AllocatedCostPerUnit = item.AllocatedCostPerUnit,
                LandedUnitCost = item.LandedUnitCost,
                ExpectedDeliveryDate = item.ExpectedDeliveryDate,
                Notes = item.Notes,
                ItemCode = item.InventoryItem?.ItemCode ?? "",
                ItemName = item.InventoryItem?.Name ?? ""
            }).ToList(),
            Receipts = receipts.Select(receipt => new PurchaseOrderReceiptDto
            {
                Id = receipt.Id,
                PurchaseOrderId = receipt.PurchaseOrderId,
                ReceiptNumber = receipt.ReceiptNumber,
                ReceiptDate = receipt.ReceiptDate,
                DeliveryNote = receipt.DeliveryNote,
                CarrierName = receipt.CarrierName,
                TrackingNumber = receipt.TrackingNumber,
                Status = receipt.Status,
                ReceivedByName = receipt.ReceivedBy?.FirstName + " " + receipt.ReceivedBy?.LastName,
                InspectedByName = receipt.InspectedBy?.FirstName + " " + receipt.InspectedBy?.LastName,
                Notes = receipt.Notes,
                RequiresInspection = receipt.RequiresInspection,
                InspectionDate = receipt.InspectionDate,
                InspectionResult = receipt.InspectionResult,
                InspectionNotes = receipt.InspectionNotes,
                PurchaseOrderNumber = purchaseOrder.OrderNumber,
                SupplierName = purchaseOrder.BusinessPartner?.PartnerName ?? ""
            }).ToList()
        };
    }

    #endregion

    #region Private Helper Methods

    private static string NormalizeCostAllocationMethod(string? method)
    {
        return string.Equals(method, GLExpense, StringComparison.OrdinalIgnoreCase)
            ? GLExpense
            : SpreadToItemCost;
    }

    private static string NormalizeCostApportionmentBasis(string? basis)
    {
        if (string.Equals(basis, BasisWeight, StringComparison.OrdinalIgnoreCase))
        {
            return BasisWeight;
        }

        if (string.Equals(basis, BasisQuantity, StringComparison.OrdinalIgnoreCase))
        {
            return BasisQuantity;
        }

        return BasisValue;
    }

    private static string NormalizeOrderType(string? orderType)
    {
        if (string.IsNullOrWhiteSpace(orderType))
        {
            return "Standard";
        }

        var normalized = orderType.Trim();

        if (string.Equals(normalized, "Consignment", StringComparison.OrdinalIgnoreCase))
        {
            return "Consignment";
        }

        if (string.Equals(normalized, "DropShip", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Drop Ship", StringComparison.OrdinalIgnoreCase))
        {
            return "DropShip";
        }

        if (string.Equals(normalized, "Blanket", StringComparison.OrdinalIgnoreCase))
        {
            return "Blanket";
        }

        if (string.Equals(normalized, "Contract", StringComparison.OrdinalIgnoreCase))
        {
            return "Contract";
        }

        return normalized;
    }

    private async Task ApplyPurchaseOrderCostAllocationAsync(
        PurchaseOrder purchaseOrder,
        List<PurchaseOrderItem> items,
        decimal totalAdditionalCost,
        string costAllocationMethod,
        string costApportionmentBasis)
    {
        // Reset first, so updates always stay deterministic.
        foreach (var item in items)
        {
            item.AllocatedAdditionalCost = 0;
            item.AllocatedCostPerUnit = 0;
            item.LandedUnitCost = item.UnitPrice;
        }

        if (totalAdditionalCost <= 0 || costAllocationMethod != SpreadToItemCost || !items.Any())
        {
            return;
        }

        var basisByItem = new Dictionary<Guid, decimal>();

        if (costApportionmentBasis == BasisWeight)
        {
            foreach (var item in items)
            {
                var perUnitWeight = 0m;
                if (item.InventoryItemId.HasValue && item.InventoryItemId.Value != Guid.Empty)
                {
                    var inventoryItem = await _inventoryItemRepository.GetByIdAsync(item.InventoryItemId.Value);
                    perUnitWeight = inventoryItem?.ShippingWeight > 0
                        ? inventoryItem.ShippingWeight
                        : inventoryItem?.Weight ?? 0;
                }

                basisByItem[item.Id] = perUnitWeight > 0 ? perUnitWeight * item.OrderedQuantity : 0;
            }
        }
        else if (costApportionmentBasis == BasisQuantity)
        {
            foreach (var item in items)
            {
                basisByItem[item.Id] = item.OrderedQuantity;
            }
        }
        else
        {
            foreach (var item in items)
            {
                basisByItem[item.Id] = item.OrderedQuantity * item.UnitPrice;
            }
        }

        var totalBasis = basisByItem.Values.Sum();
        if (totalBasis <= 0)
        {
            // Fallback to line value if selected basis cannot be resolved (e.g. no weights).
            basisByItem = items.ToDictionary(i => i.Id, i => i.OrderedQuantity * i.UnitPrice);
            totalBasis = basisByItem.Values.Sum();
        }

        if (totalBasis <= 0)
        {
            // Final fallback for zero-value lines.
            basisByItem = items.ToDictionary(i => i.Id, i => i.OrderedQuantity);
            totalBasis = basisByItem.Values.Sum();
        }

        if (totalBasis <= 0)
        {
            return;
        }

        foreach (var item in items)
        {
            var ratio = basisByItem[item.Id] / totalBasis;
            var allocated = Math.Round(totalAdditionalCost * ratio, 2, MidpointRounding.AwayFromZero);

            item.AllocatedAdditionalCost = allocated;
            item.AllocatedCostPerUnit = item.OrderedQuantity > 0
                ? Math.Round(allocated / item.OrderedQuantity, 4, MidpointRounding.AwayFromZero)
                : 0;
            item.LandedUnitCost = Math.Round(item.UnitPrice + item.AllocatedCostPerUnit, 4, MidpointRounding.AwayFromZero);
        }

        // Keep total allocated aligned to header shipping cost by adjusting the largest line.
        var roundedTotal = items.Sum(i => i.AllocatedAdditionalCost);
        var roundingDifference = Math.Round(totalAdditionalCost - roundedTotal, 2, MidpointRounding.AwayFromZero);
        if (roundingDifference != 0)
        {
            var largestLine = items.OrderByDescending(i => i.AllocatedAdditionalCost).First();
            largestLine.AllocatedAdditionalCost = Math.Round(largestLine.AllocatedAdditionalCost + roundingDifference, 2, MidpointRounding.AwayFromZero);
            largestLine.AllocatedCostPerUnit = largestLine.OrderedQuantity > 0
                ? Math.Round(largestLine.AllocatedAdditionalCost / largestLine.OrderedQuantity, 4, MidpointRounding.AwayFromZero)
                : 0;
            largestLine.LandedUnitCost = Math.Round(largestLine.UnitPrice + largestLine.AllocatedCostPerUnit, 4, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>
    /// Processes inventory receipt by updating stock levels and creating movements
    /// </summary>
    private async Task ProcessInventoryReceiptAsync(
        PurchaseOrderItem poItem,
        decimal quantity,
        Guid warehouseId,
        Guid? locationId,
        string receiptNumber,
        Guid receiptId,
        string? lotNumber,
        string? serialNumber,
        DateTime? expirationDate)
    {
        try
        {
            if (quantity <= 0)
            {
                _logger.LogWarning("Skipping inventory receipt for zero or negative quantity. PO Item: {ItemId}", poItem.Id);
                return;
            }

            if (warehouseId == Guid.Empty)
            {
                _logger.LogWarning("No warehouse specified for inventory receipt. PO Item: {ItemId}", poItem.Id);
                return;
            }

            // Skip inventory processing if no inventory item is linked (e.g., from tender)
            if (!poItem.InventoryItemId.HasValue || poItem.InventoryItemId.Value == Guid.Empty)
            {
                _logger.LogWarning("Skipping inventory receipt for PO item {ItemId} - no inventory item linked", poItem.Id);
                return;
            }

            // Convert PO line quantities/costs to the item's base unit so valuation/stock are consistent.
            // The PO line quantity/cost are in poItem.UnitOfMeasure (often a purchase UOM).
            var conversionToBase = 1m;
            if (poItem.ItemUnitOfMeasureId.HasValue && poItem.ItemUnitOfMeasureId.Value != Guid.Empty)
            {
                var poUom = await _unitOfWork.Repository<ItemUnitOfMeasure>().GetByIdAsync(poItem.ItemUnitOfMeasureId.Value);
                if (poUom != null && poUom.ConversionToBase > 0)
                {
                    conversionToBase = poUom.ConversionToBase;
                }
            }
            else if (!string.IsNullOrWhiteSpace(poItem.UnitOfMeasure))
            {
                // Fallback: try to resolve conversion from the item UOM schedule by matching the PO's UOM code.
                var match = await _unitOfWork.Repository<ItemUnitOfMeasure>()
                    .GetQueryable(u => u.InventoryItemId == poItem.InventoryItemId.Value && u.IsActive)
                    .AsNoTracking()
                    .Include(u => u.UnitOfMeasure)
                    .FirstOrDefaultAsync(u => u.UnitOfMeasure.Code == poItem.UnitOfMeasure);

                if (match != null && match.ConversionToBase > 0)
                {
                    conversionToBase = match.ConversionToBase;
                }
            }

            if (conversionToBase <= 0)
            {
                conversionToBase = 1m;
            }

            var unitCostPerPurchaseUom = poItem.LandedUnitCost > 0 ? poItem.LandedUnitCost : poItem.UnitPrice;
            var baseQuantity = quantity * conversionToBase;
            var baseUnitCost = conversionToBase != 0 ? unitCostPerPurchaseUom / conversionToBase : unitCostPerPurchaseUom;

            // Process receipt using the valuation service
            // This will:
            // 1. Create appropriate cost layers (FIFO) or update balances (WAC/Standard)
            // 2. Create stock movement records
            // 3. Update warehouse quantities
            var variance = await _inventoryValuationService.ProcessReceiptAsync(
                inventoryItemId: poItem.InventoryItemId.Value,
                warehouseId: warehouseId,
                locationId: locationId,
                quantity: baseQuantity,
                unitCost: baseUnitCost,
                referenceType: ReferenceType.PO,
                referenceNumber: receiptNumber,
                referenceId: receiptId,
                lotNumber: lotNumber,
                serialNumber: serialNumber,
                expirationDate: expirationDate);

            // Update operational bin balances so the Bin Stock page reflects PO receipts.
            if (locationId.HasValue && locationId.Value != Guid.Empty && baseQuantity != 0)
            {
                // Prefer the valuation cache average cost after posting.
                var tenantId = _currentUserService.TenantId;
                var balance = await _unitOfWork.Repository<InventoryBalance>()
                    .FirstOrDefaultAsync(b => b.TenantId == tenantId &&
                                             b.InventoryItemId == poItem.InventoryItemId.Value &&
                                             b.WarehouseId == warehouseId &&
                                             b.LocationId == locationId);

                var averageCost = balance?.AverageUnitCost > 0 ? balance.AverageUnitCost : baseUnitCost;
                await AdjustInventoryLocationQuantityAsync(locationId.Value, poItem.InventoryItemId.Value, baseQuantity, averageCost);

                // Keep item/warehouse totals in sync with operational balances so inventory lists reflect receipts.
                await AdjustWarehouseAndItemTotalsAsync(poItem.InventoryItemId.Value, warehouseId, baseQuantity, warehouseAverageCost: averageCost, lastPurchaseCost: baseUnitCost);
            }
            else
            {
                // No bin tracking: still update item/warehouse totals.
                await AdjustWarehouseAndItemTotalsAsync(poItem.InventoryItemId.Value, warehouseId, baseQuantity, warehouseAverageCost: baseUnitCost, lastPurchaseCost: baseUnitCost);
            }

            _logger.LogInformation(
                "Processed inventory receipt for item {ItemId}, {Qty} {Uom} (x{Conv} => {BaseQty} base) at landed cost {UnitCostPerUom} per PO UOM (=> {BaseUnitCost} per base). Variance: {Variance}",
                poItem.InventoryItemId, quantity, poItem.UnitOfMeasure, conversionToBase, baseQuantity, unitCostPerPurchaseUom, baseUnitCost, variance);

            // If using standard costing, the variance should be posted to a variance account
            // This would typically be handled by the accounting module
            if (variance != 0)
            {
                _logger.LogInformation(
                    "Purchase price variance detected: {Variance} for item {ItemId}",
                    variance, poItem.InventoryItemId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process inventory receipt for PO item {ItemId}: {ErrorMessage}",
                poItem.Id, ex.Message);
            // Don't fail the entire receipt if inventory update fails
            // This allows manual correction later
            throw; // Re-throw to rollback the transaction
        }
    }

    private async Task AdjustWarehouseAndItemTotalsAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        decimal deltaQuantity,
        decimal? warehouseAverageCost,
        decimal? lastPurchaseCost)
    {
        if (inventoryItemId == Guid.Empty || warehouseId == Guid.Empty || deltaQuantity == 0)
        {
            return;
        }

        var tenantId = _currentUserService.TenantId;
        var isConsignmentWarehouse = await IsConsignmentWarehouseAsync(warehouseId);

        // ---- Warehouse totals ----
        var wqKey = (inventoryItemId, warehouseId);
        if (!_warehouseQuantityCache.TryGetValue(wqKey, out var warehouseQty))
        {
            warehouseQty = await _unitOfWork.Repository<WarehouseQuantity>()
                .FirstOrDefaultAsync(q => q.TenantId == tenantId &&
                                         q.InventoryItemId == inventoryItemId &&
                                         q.WarehouseId == warehouseId);

            if (warehouseQty == null)
            {
                var initialQty = deltaQuantity;
                if (initialQty < 0)
                {
                    throw new InvalidOperationException("Insufficient warehouse stock.");
                }

                warehouseQty = new WarehouseQuantity
                {
                    TenantId = tenantId,
                    InventoryItemId = inventoryItemId,
                    WarehouseId = warehouseId,
                    CurrentStock = 0,
                    AvailableStock = 0,
                    AllocatedStock = 0,
                    AverageCost = warehouseAverageCost.HasValue && warehouseAverageCost.Value > 0 ? warehouseAverageCost.Value : 0,
                    LastMovementDate = DateTime.UtcNow,
                    CreatedById = _currentUserService.UserId
                };

                await _unitOfWork.Repository<WarehouseQuantity>().AddAsync(warehouseQty);
                _warehouseQuantityCache[wqKey] = warehouseQty;
                _newWarehouseQuantityKeys.Add(wqKey);
            }
            else
            {
                _warehouseQuantityCache[wqKey] = warehouseQty;
            }
        }

        if (warehouseQty.CurrentStock + deltaQuantity < 0)
        {
            throw new InvalidOperationException("Insufficient warehouse stock.");
        }

        warehouseQty.CurrentStock += deltaQuantity;
        warehouseQty.AvailableStock += deltaQuantity;
        warehouseQty.LastMovementDate = DateTime.UtcNow;
        if (warehouseAverageCost.HasValue && warehouseAverageCost.Value > 0)
        {
            warehouseQty.AverageCost = warehouseAverageCost.Value;
        }
        warehouseQty.UpdatedAt = DateTime.UtcNow;
        warehouseQty.LastModifiedById = _currentUserService.UserId;

        if (!_newWarehouseQuantityKeys.Contains(wqKey))
        {
            await _unitOfWork.Repository<WarehouseQuantity>().UpdateAsync(warehouseQty);
        }

        // Consignment stock is not part of owned/main inventory totals.
        if (isConsignmentWarehouse)
        {
            return;
        }

        // ---- Item totals ----
        if (!_inventoryItemCache.TryGetValue(inventoryItemId, out var invItem))
        {
            invItem = await _inventoryItemRepository.GetByIdAsync(inventoryItemId)
                     ?? await _unitOfWork.Repository<InventoryItem>().GetByIdAsync(inventoryItemId);

            if (invItem == null)
            {
                throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");
            }

            _inventoryItemCache[inventoryItemId] = invItem;
        }

        if (invItem.CurrentStock + deltaQuantity < 0)
        {
            throw new InvalidOperationException("Insufficient item stock.");
        }

        invItem.CurrentStock += deltaQuantity;
        invItem.AvailableStock += deltaQuantity;
        invItem.LastPurchaseDate = DateTime.UtcNow;
        if (lastPurchaseCost.HasValue && lastPurchaseCost.Value > 0)
        {
            invItem.LastPurchaseCost = lastPurchaseCost.Value;
        }

        // Keep average cost aligned with valuation balances (best-effort).
        var balances = await _unitOfWork.Repository<InventoryBalance>()
            .FindAsync(b => b.TenantId == tenantId && b.InventoryItemId == inventoryItemId);
        var totalQty = balances.Sum(b => b.QuantityOnHand);
        var totalValue = balances.Sum(b => b.TotalValue);
        var avg = totalQty > 0 ? totalValue / totalQty : 0;
        if (avg > 0)
        {
            invItem.AverageCost = avg;
        }

        invItem.UpdatedAt = DateTime.UtcNow;
        invItem.LastModifiedById = _currentUserService.UserId;
        await _unitOfWork.Repository<InventoryItem>().UpdateAsync(invItem);
    }

    private async Task<bool> IsConsignmentWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
        {
            return false;
        }

        if (_warehouseConsignmentFlagCache.TryGetValue(warehouseId, out var cached))
        {
            return cached;
        }

        var wh = await _warehouseRepository.GetByIdAsync(warehouseId);
        var isConsignment = wh?.IsConsignmentWarehouse == true;
        _warehouseConsignmentFlagCache[warehouseId] = isConsignment;
        return isConsignment;
    }

    private async Task AdjustInventoryLocationQuantityAsync(Guid locationId, Guid inventoryItemId, decimal deltaQuantity, decimal averageCost)
    {
        if (locationId == Guid.Empty || inventoryItemId == Guid.Empty || deltaQuantity == 0)
        {
            return;
        }

        var key = (inventoryItemId, locationId);
        if (!_inventoryLocationCache.TryGetValue(key, out var invLoc))
        {
            var tenantId = _currentUserService.TenantId;
            invLoc = await _unitOfWork.Repository<InventoryLocation>()
                .FirstOrDefaultAsync(il => il.TenantId == tenantId &&
                                          il.InventoryItemId == inventoryItemId &&
                                          il.LocationId == locationId);

            if (invLoc == null)
            {
                var initialQty = deltaQuantity;
                if (initialQty < 0)
                {
                    throw new InvalidOperationException("Insufficient bin stock.");
                }

                invLoc = new InventoryLocation
                {
                    TenantId = tenantId,
                    InventoryItemId = inventoryItemId,
                    LocationId = locationId,
                    Quantity = initialQty,
                    AllocatedQuantity = 0,
                    AvailableQuantity = initialQty,
                    AverageCost = averageCost > 0 ? averageCost : 0,
                    LastMovementDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = _currentUserService.UserId
                };

                await _unitOfWork.Repository<InventoryLocation>().AddAsync(invLoc);
                _inventoryLocationCache[key] = invLoc;
                _newInventoryLocationKeys.Add(key);
                return; // Keep entity in Added state; no UpdateAsync call (prevents concurrency errors).
            }

            _inventoryLocationCache[key] = invLoc;
        }

        var newQty = invLoc.Quantity + deltaQuantity;
        if (newQty < 0)
        {
            throw new InvalidOperationException("Insufficient bin stock.");
        }

        invLoc.Quantity = newQty;
        invLoc.AvailableQuantity = invLoc.Quantity - invLoc.AllocatedQuantity;

        if (averageCost > 0)
        {
            invLoc.AverageCost = averageCost;
        }

        invLoc.LastMovementDate = DateTime.UtcNow;
        invLoc.UpdatedAt = DateTime.UtcNow;
        invLoc.LastModifiedById = _currentUserService.UserId;

        if (!_newInventoryLocationKeys.Contains(key))
        {
            await _unitOfWork.Repository<InventoryLocation>().UpdateAsync(invLoc);
        }
    }

    /// <summary>
    /// Moves committed budget to utilized when PO is fully received
    /// </summary>
    private async Task UtilizeBudgetForPurchaseOrderAsync(PurchaseOrder purchaseOrder)
    {
        try
        {
            // Find the plan item linked to this PO to get category and plan info
            var planItems = await _planItemRepository.GetAllAsync();
            var planItem = planItems.FirstOrDefault(pi => pi.PurchaseOrderId == purchaseOrder.Id);

            if (planItem == null)
            {
                _logger.LogWarning("No plan item found for PO {PONumber}, skipping budget utilization", purchaseOrder.OrderNumber);
                return;
            }

            // Find budget linked to the plan
            var budgets = await _budgetRepository.GetByPlanIdAsync(planItem.ProcurementPlanId);
            var budget = budgets.FirstOrDefault(b => b.Status == "Active" || b.Status == "Approved");

            if (budget == null)
            {
                _logger.LogWarning("No budget found for plan {PlanId}, skipping budget utilization", planItem.ProcurementPlanId);
                return;
            }

            // Move from committed to utilized
            await _budgetService.UtilizeCommittedBudgetAsync(budget.Id, purchaseOrder.TotalAmount, planItem.ItemCategory);
            _logger.LogInformation("Utilized {Amount} from budget {BudgetCode} for PO {PONumber}, category: {Category}",
                purchaseOrder.TotalAmount, budget.BudgetCode, purchaseOrder.OrderNumber, planItem.ItemCategory ?? "N/A");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to utilize budget for PO {PONumber}: {ErrorMessage}", purchaseOrder.OrderNumber, ex.Message);
            // Don't fail the PO receiving if budget utilization fails
        }
    }

    private async Task PopulateCurrentStepNamesAsync(List<PurchaseOrderSummaryDto> purchaseOrderDtos)
    {
        // Only populate for records that are likely to be in an approval workflow.
        var idsToCheck = purchaseOrderDtos
            .Where(po =>
                string.Equals(po.Status, "Pending Approval", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(po.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
            .Select(po => po.Id)
            .ToList();

        if (idsToCheck.Count == 0)
        {
            return;
        }

        var map = new Dictionary<Guid, string?>();
        foreach (var id in idsToCheck)
        {
            try
            {
                var step = await _workflowService.GetCurrentWorkflowStepAsync("PurchaseOrder", id);
                map[id] = step?.StepName;
            }
            catch
            {
                map[id] = null;
            }
        }

        foreach (var dto in purchaseOrderDtos)
        {
            if (map.TryGetValue(dto.Id, out var name))
            {
                dto.CurrentWorkflowStepName = name;
            }
        }
    }

    private async Task<string?> GetCurrentStepNameAsync(Guid purchaseOrderId, string status)
    {
        if (!string.Equals(status, "Pending Approval", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var step = await _workflowService.GetCurrentWorkflowStepAsync("PurchaseOrder", purchaseOrderId);
            return step?.StepName;
        }
        catch
        {
            return null;
        }
    }

    private Task<bool> IsFrameworkCallOffAsync(Guid purchaseOrderId) =>
        _unitOfWork.Repository<ProcurementFrameworkCallOff>()
            .GetQueryable(item =>
                item.TenantId == _currentUserService.TenantId &&
                item.PurchaseOrderId == purchaseOrderId &&
                !item.IsDeleted)
            .AnyAsync(HttpContext.RequestAborted);

    private string CorrelationId()
    {
        var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(supplied))
            return HttpContext.TraceIdentifier.Length <= 100
                ? HttpContext.TraceIdentifier
                : HttpContext.TraceIdentifier[..100];
        supplied = supplied.Trim();
        return supplied.Length <= 100 ? supplied : supplied[..100];
    }

    #endregion
}
