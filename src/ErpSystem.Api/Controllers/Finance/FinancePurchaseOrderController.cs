using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/ap/purchase-orders")]
public class FinancePurchaseOrderController : ControllerBase
{
    private const int Draft = 1;
    private const int Approved = 2;
    private const int PartiallyReceived = 3;
    private const int Received = 4;
    private const int PendingApproval = 9;
    private const int Rejected = 10;
    private const string ApproverRoles = "SuperAdmin,TenantAdmin,Manager,Accounts Officer,Senior Accountant,Finance Manager,Financial Controller";
    private static readonly string[] ApproverRoleNames =
    {
        "SuperAdmin",
        "TenantAdmin",
        "Manager",
        "Accounts Officer",
        "Senior Accountant",
        "Finance Manager",
        "Financial Controller"
    };
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IWorkflowService _workflowService;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly ILogger<FinancePurchaseOrderController> _logger;

    public FinancePurchaseOrderController(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IWorkflowService workflowService,
        IDocumentNumberingService documentNumberingService,
        ILogger<FinancePurchaseOrderController> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _workflowService = workflowService;
        _documentNumberingService = documentNumberingService;
        _logger = logger;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FinancePurchaseOrderDto>>> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] int? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var query = BasePurchaseOrderQuery(tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(po =>
                po.OrderNumber.ToLower().Contains(term) ||
                (po.Remarks != null && po.Remarks.ToLower().Contains(term)) ||
                po.Vendor.PartnerName.ToLower().Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(po => po.Status == status.Value);
        }

        var purchaseOrders = await query
            .OrderByDescending(po => po.OrderDate)
            .ThenByDescending(po => po.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return Ok(purchaseOrders.Select(MapPurchaseOrder));
    }

    [HttpGet("pending-approvals")]
    [Authorize(Roles = ApproverRoles)]
    public async Task<ActionResult<IEnumerable<FinancePurchaseOrderDto>>> GetPendingApprovals(
        CancellationToken cancellationToken = default)
    {
        var currentUserId = TryGetCurrentUserId();
        var purchaseOrders = await BasePurchaseOrderQuery(TenantId)
            .Where(po => po.Status == PendingApproval)
            .Where(po => !currentUserId.HasValue || !po.CreatedById.HasValue || po.CreatedById.Value != currentUserId.Value)
            .OrderBy(po => po.OrderDate)
            .ThenBy(po => po.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        if (currentUserId.HasValue)
        {
            var assignedPurchaseOrders = new List<FinancePurchaseOrder>();
            foreach (var purchaseOrder in purchaseOrders)
            {
                if (await _workflowService.CanUserApproveAsync("FinancePurchaseOrder", purchaseOrder.Id, currentUserId.Value))
                {
                    assignedPurchaseOrders.Add(purchaseOrder);
                }
            }

            purchaseOrders = assignedPurchaseOrders;
        }

        return Ok(purchaseOrders.Select(MapPurchaseOrder));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FinancePurchaseOrderDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await BasePurchaseOrderQuery(TenantId)
            .AsSplitQuery()
            .FirstOrDefaultAsync(po => po.Id == id, cancellationToken);

        return purchaseOrder == null ? NotFound() : Ok(MapPurchaseOrder(purchaseOrder));
    }

    [HttpPost]
    public async Task<ActionResult<FinancePurchaseOrderDto>> Create(
        [FromBody] CreateFinancePurchaseOrderDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.Items.Count == 0)
        {
            return BadRequest("At least one purchase order line is required.");
        }

        var tenantId = TenantId;
        var vendor = await _dbContext.BusinessPartners
            .FirstOrDefaultAsync(v => v.Id == dto.VendorId && v.TenantId == tenantId && !v.IsDeleted, cancellationToken);

        if (vendor == null)
        {
            return BadRequest($"Vendor with ID {dto.VendorId} was not found.");
        }

        var orderDate = dto.OrderDate ?? DateTime.UtcNow;
        var orderNumber = await ResolvePurchaseOrderNumberAsync(tenantId, orderDate, dto.OrderNumber, cancellationToken);

        var orderNumberExists = await _dbContext.FinancePurchaseOrders
            .AnyAsync(po => po.TenantId == tenantId && !po.IsDeleted && po.OrderNumber == orderNumber, cancellationToken);

        if (orderNumberExists)
        {
            return BadRequest($"Finance purchase order '{orderNumber}' already exists.");
        }

        var currencyCode = NormalizeCurrency(dto.CurrencyCode);
        var exchangeRate = dto.ExchangeRate.GetValueOrDefault(1m);
        if (exchangeRate <= 0)
        {
            return BadRequest("Exchange rate must be greater than zero.");
        }

        PaymentTerm? paymentTerm;
        try
        {
            paymentTerm = await ResolvePaymentTermAsync(dto.PaymentTermId ?? vendor.PaymentTermId, tenantId, "Supplier", cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var purchaseOrder = new FinancePurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = orderNumber,
            VendorId = dto.VendorId,
            OrderDate = orderDate,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            PaymentTermId = paymentTerm?.Id,
            Status = Draft,
            CurrencyCode = currencyCode,
            ExchangeRate = exchangeRate,
            DiscountAmount = dto.DiscountAmount.GetValueOrDefault(),
            TaxGroupId = NormalizeGuid(dto.TaxGroupId),
            Remarks = dto.Remarks,
            CreatedBy = _currentUserService.UserName,
            CreatedById = TryGetCurrentUserId()
        };

        foreach (var lineDto in dto.Items)
        {
            var validationError = await ValidateLineAsync(lineDto, tenantId, cancellationToken);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var quantity = lineDto.OrderedQuantity;
            var unitPrice = lineDto.UnitPrice;
            var lineBaseAmount = quantity * unitPrice;
            var taxAmount = lineDto.TaxAmount.GetValueOrDefault();
            var discountAmount = lineDto.DiscountAmount.GetValueOrDefault();
            var lineTotal = lineDto.LineTotal ?? (lineBaseAmount + taxAmount - discountAmount);

            purchaseOrder.Items.Add(new FinancePurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LineType = NormalizeLineType(lineDto.LineType),
                InventoryItemId = NormalizeGuid(lineDto.InventoryItemId),
                WarehouseId = NormalizeGuid(lineDto.WarehouseId),
                GlAccountId = NormalizeGuid(lineDto.GlAccountId),
                Description = lineDto.Description.Trim(),
                OrderedQuantity = quantity,
                ReceivedQuantity = 0m,
                InvoicedQuantity = 0m,
                CancelledQuantity = 0m,
                UnitPrice = unitPrice,
                CurrencyCode = string.IsNullOrWhiteSpace(lineDto.CurrencyCode)
                    ? currencyCode
                    : NormalizeCurrency(lineDto.CurrencyCode),
                ExchangeRate = lineDto.ExchangeRate.GetValueOrDefault(exchangeRate),
                TaxCode = lineDto.TaxCode,
                TaxRate = lineDto.TaxRate.GetValueOrDefault(),
                TaxAmount = taxAmount,
                TaxGroupId = NormalizeGuid(lineDto.TaxGroupId),
                DiscountPercentage = lineDto.DiscountPercentage.GetValueOrDefault(),
                DiscountAmount = discountAmount,
                LineTotal = lineTotal
            });
        }

        purchaseOrder.TotalAmount = dto.TotalAmount ?? purchaseOrder.Items.Sum(i => i.LineTotal);

        _dbContext.FinancePurchaseOrders.Add(purchaseOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var saved = await BasePurchaseOrderQuery(tenantId)
            .AsSplitQuery()
            .FirstAsync(po => po.Id == purchaseOrder.Id, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = purchaseOrder.Id }, MapPurchaseOrder(saved));
    }

    [HttpPost("{id:guid}/submit-for-approval")]
    public async Task<ActionResult<FinancePurchaseOrderDto>> SubmitForApproval(Guid id, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await _dbContext.FinancePurchaseOrders
            .Include(po => po.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(po => po.Id == id && po.TenantId == TenantId && !po.IsDeleted, cancellationToken);

        if (purchaseOrder == null)
        {
            return NotFound();
        }

        if (purchaseOrder.Status != Draft && purchaseOrder.Status != Rejected)
        {
            return BadRequest("Only draft or rejected finance purchase orders can be submitted for approval.");
        }

        if (!purchaseOrder.Items.Any())
        {
            return BadRequest("At least one purchase order line is required before submitting for approval.");
        }

        var previousStatus = purchaseOrder.Status;
        purchaseOrder.Status = PendingApproval;
        purchaseOrder.UpdatedAt = DateTime.UtcNow;
        purchaseOrder.UpdatedBy = _currentUserService.UserName;
        purchaseOrder.LastModifiedById = TryGetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        var workflowResult = await _workflowService.StartApprovalWorkflowAsync("FinancePurchaseOrder", id);
        if (!workflowResult.Success)
        {
            purchaseOrder.Status = previousStatus;
            purchaseOrder.UpdatedAt = DateTime.UtcNow;
            purchaseOrder.UpdatedBy = _currentUserService.UserName;
            purchaseOrder.LastModifiedById = TryGetCurrentUserId();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return BadRequest(workflowResult.Message ?? "Unable to start finance purchase order approval workflow.");
        }

        var saved = await BasePurchaseOrderQuery(TenantId)
            .AsSplitQuery()
            .FirstAsync(po => po.Id == id, cancellationToken);

        return Ok(MapPurchaseOrder(saved));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = ApproverRoles)]
    public async Task<ActionResult<FinancePurchaseOrderDto>> Approve(Guid id, [FromBody] ApprovalActionDto? request = null, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await _dbContext.FinancePurchaseOrders
            .FirstOrDefaultAsync(po => po.Id == id && po.TenantId == TenantId && !po.IsDeleted, cancellationToken);

        if (purchaseOrder == null)
        {
            return NotFound();
        }

        if (purchaseOrder.Status != PendingApproval)
        {
            return BadRequest("Only finance purchase orders pending approval can be approved.");
        }

        var currentUserId = TryGetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return StatusCode(403, "Unable to resolve the current approver.");
        }

        if (currentUserId.HasValue && purchaseOrder.CreatedById.HasValue && purchaseOrder.CreatedById.Value == currentUserId.Value)
        {
            return StatusCode(403, "You cannot approve a finance purchase order that you created.");
        }

        if (!await _workflowService.CanUserApproveAsync("FinancePurchaseOrder", id, currentUserId.Value))
        {
            return StatusCode(403, "This finance purchase order is assigned to another workflow approver.");
        }

        var workflowResult = await _workflowService.ProcessApprovalStepAsync(
            "FinancePurchaseOrder",
            id,
            currentUserId.Value,
            "Approve",
            request?.Comments);

        if (!workflowResult.Success)
        {
            return BadRequest(workflowResult.Message ?? "Unable to process finance purchase order approval.");
        }

        if (workflowResult.Status == WorkflowInstanceStatus.Completed)
        {
            purchaseOrder.Status = Approved;
        }

        purchaseOrder.UpdatedAt = DateTime.UtcNow;
        purchaseOrder.UpdatedBy = _currentUserService.UserName;
        purchaseOrder.LastModifiedById = currentUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var saved = await BasePurchaseOrderQuery(TenantId)
            .AsSplitQuery()
            .FirstAsync(po => po.Id == id, cancellationToken);

        if (workflowResult.Status == WorkflowInstanceStatus.Completed)
        {
            await NotifyFinancePurchaseOrderOwnerAsync(
                saved,
                "Finance PO approved",
                $"{saved.OrderNumber} has been approved.",
                "FinancePurchaseOrderApproved",
                cancellationToken);
        }

        return Ok(MapPurchaseOrder(saved));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = ApproverRoles)]
    public async Task<ActionResult<FinancePurchaseOrderDto>> Reject(Guid id, [FromBody] ApprovalActionDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return BadRequest("A rejection reason is required.");
        }

        var purchaseOrder = await _dbContext.FinancePurchaseOrders
            .FirstOrDefaultAsync(po => po.Id == id && po.TenantId == TenantId && !po.IsDeleted, cancellationToken);

        if (purchaseOrder == null)
        {
            return NotFound();
        }

        if (purchaseOrder.Status != PendingApproval)
        {
            return BadRequest("Only finance purchase orders pending approval can be rejected.");
        }

        var currentUserId = TryGetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return StatusCode(403, "Unable to resolve the current approver.");
        }

        if (currentUserId.HasValue && purchaseOrder.CreatedById.HasValue && purchaseOrder.CreatedById.Value == currentUserId.Value)
        {
            return StatusCode(403, "You cannot reject a finance purchase order that you created.");
        }

        if (!await _workflowService.CanUserApproveAsync("FinancePurchaseOrder", id, currentUserId.Value))
        {
            return StatusCode(403, "This finance purchase order is assigned to another workflow approver.");
        }

        var workflowResult = await _workflowService.ProcessApprovalStepAsync(
            "FinancePurchaseOrder",
            id,
            currentUserId.Value,
            "Reject",
            request.Reason);

        if (!workflowResult.Success)
        {
            return BadRequest(workflowResult.Message ?? "Unable to process finance purchase order rejection.");
        }

        if (workflowResult.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
        {
            purchaseOrder.Status = Rejected;
            purchaseOrder.Remarks = string.IsNullOrWhiteSpace(purchaseOrder.Remarks)
                ? $"Rejected: {request.Reason.Trim()}"
                : $"{purchaseOrder.Remarks}{Environment.NewLine}Rejected: {request.Reason.Trim()}";
        }

        purchaseOrder.UpdatedAt = DateTime.UtcNow;
        purchaseOrder.UpdatedBy = _currentUserService.UserName;
        purchaseOrder.LastModifiedById = currentUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var saved = await BasePurchaseOrderQuery(TenantId)
            .AsSplitQuery()
            .FirstAsync(po => po.Id == id, cancellationToken);

        if (workflowResult.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
        {
            await NotifyFinancePurchaseOrderOwnerAsync(
                saved,
                "Finance PO rejected",
                $"{saved.OrderNumber} was rejected. {request.Reason.Trim()}",
                "FinancePurchaseOrderRejected",
                cancellationToken);
        }

        return Ok(MapPurchaseOrder(saved));
    }

    private IQueryable<FinancePurchaseOrder> BasePurchaseOrderQuery(Guid tenantId)
    {
        return _dbContext.FinancePurchaseOrders
            .Include(po => po.Vendor)
            .Include(po => po.PaymentTerm)
            .Include(po => po.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.InventoryItem)
            .Include(po => po.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.GlAccount)
            .Where(po => po.TenantId == tenantId && !po.IsDeleted);
    }

    private static FinancePurchaseOrderDto MapPurchaseOrder(FinancePurchaseOrder purchaseOrder)
    {
        return new FinancePurchaseOrderDto
        {
            Id = purchaseOrder.Id,
            TenantId = purchaseOrder.TenantId,
            OrderNumber = purchaseOrder.OrderNumber,
            VendorId = purchaseOrder.VendorId,
            SupplierId = purchaseOrder.VendorId,
            VendorName = purchaseOrder.Vendor?.PartnerName,
            SupplierName = purchaseOrder.Vendor?.PartnerName,
            OrderDate = purchaseOrder.OrderDate,
            ExpectedDeliveryDate = purchaseOrder.ExpectedDeliveryDate,
            PaymentTermId = purchaseOrder.PaymentTermId,
            PaymentTermsDays = purchaseOrder.PaymentTerm?.DueDays,
            EarlyPaymentDiscountPercentage = purchaseOrder.PaymentTerm?.DiscountPercent,
            EarlyPaymentDiscountDueDate = purchaseOrder.PaymentTerm != null && purchaseOrder.PaymentTerm.DiscountPercent > 0 && purchaseOrder.PaymentTerm.DiscountDays > 0
                ? purchaseOrder.OrderDate.AddDays(purchaseOrder.PaymentTerm.DiscountDays)
                : null,
            Status = purchaseOrder.Status,
            CurrencyCode = purchaseOrder.CurrencyCode,
            ExchangeRate = purchaseOrder.ExchangeRate,
            TotalAmount = purchaseOrder.TotalAmount,
            DiscountAmount = purchaseOrder.DiscountAmount,
            TaxGroupId = purchaseOrder.TaxGroupId,
            Remarks = purchaseOrder.Remarks,
            Items = purchaseOrder.Items
                .Where(item => !item.IsDeleted)
                .OrderBy(item => item.CreatedAt)
                .Select(MapItem)
                .ToList()
        };
    }

    private static FinancePurchaseOrderItemDto MapItem(FinancePurchaseOrderItem item)
    {
        return new FinancePurchaseOrderItemDto
        {
            Id = item.Id,
            FinancePurchaseOrderId = item.FinancePurchaseOrderId,
            LineType = item.LineType,
            InventoryItemId = item.InventoryItemId,
            WarehouseId = item.WarehouseId,
            GlAccountId = item.GlAccountId,
            Description = item.Description,
            OrderedQuantity = item.OrderedQuantity,
            ReceivedQuantity = item.ReceivedQuantity,
            InvoicedQuantity = item.InvoicedQuantity,
            CancelledQuantity = item.CancelledQuantity,
            UnitPrice = item.UnitPrice,
            CurrencyCode = item.CurrencyCode,
            ExchangeRate = item.ExchangeRate,
            TaxCode = item.TaxCode,
            TaxRate = item.TaxRate,
            TaxAmount = item.TaxAmount,
            TaxGroupId = item.TaxGroupId,
            DiscountPercentage = item.DiscountPercentage,
            DiscountAmount = item.DiscountAmount,
            LineTotal = item.LineTotal,
            InventoryItemName = item.InventoryItem?.Name,
            InventoryItemCode = item.InventoryItem?.ItemCode,
            GlAccountName = item.GlAccount?.AccountName,
            GlAccountCode = item.GlAccount?.AccountNumber ?? item.GlAccount?.AccountCode
        };
    }

    private async Task<string?> ValidateLineAsync(
        CreateFinancePurchaseOrderItemDto line,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (line.OrderedQuantity <= 0)
        {
            return "Ordered quantity must be greater than zero.";
        }

        if (line.UnitPrice < 0)
        {
            return "Unit price cannot be negative.";
        }

        if (string.IsNullOrWhiteSpace(line.Description))
        {
            return "Line description is required.";
        }

        var lineType = NormalizeLineType(line.LineType);
        if (lineType == 1)
        {
            var inventoryItemId = NormalizeGuid(line.InventoryItemId);
            if (!inventoryItemId.HasValue)
            {
                return "Inventory lines require an inventory item.";
            }

            var exists = await _dbContext.InventoryItems
                .AnyAsync(item => item.Id == inventoryItemId.Value && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
            return exists ? null : $"Inventory item with ID {inventoryItemId.Value} was not found.";
        }

        var glAccountId = NormalizeGuid(line.GlAccountId);
        if (!glAccountId.HasValue)
        {
            return "GL account lines require a GL account.";
        }

        var accountExists = await _dbContext.Accounts
            .AnyAsync(account => account.Id == glAccountId.Value && account.TenantId == tenantId && !account.IsDeleted, cancellationToken);

        return accountExists ? null : $"GL account with ID {glAccountId.Value} was not found.";
    }

    private async Task<string> GeneratePurchaseOrderNumberAsync(Guid tenantId, DateTime orderDate, CancellationToken cancellationToken)
    {
        // Generated finance PO numbers use the central reservation service so
        // concurrent creates cannot both derive the same number from row counts.
        return await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            FinanceDocumentTypes.FinancePurchaseOrder,
            tenantId,
            orderDate,
            nameof(FinancePurchaseOrder),
            cancellationToken: cancellationToken);
    }

    private async Task<string> ResolvePurchaseOrderNumberAsync(
        Guid tenantId,
        DateTime orderDate,
        string? requestedOrderNumber,
        CancellationToken cancellationToken)
    {
        var manualOrderNumber = requestedOrderNumber?.Trim();
        if (string.IsNullOrWhiteSpace(manualOrderNumber))
        {
            return await GeneratePurchaseOrderNumberAsync(tenantId, orderDate, cancellationToken);
        }

        var definitions = await _documentNumberingService.GetDefinitionsAsync(
            DocumentNumberingModules.Finance,
            tenantId,
            cancellationToken);

        var definition = definitions
            .Where(d => d.DocumentType == FinanceDocumentTypes.FinancePurchaseOrder
                && d.IsActive
                && d.IsDefault
                && (d.EffectiveFrom == null || d.EffectiveFrom <= orderDate)
                && (d.EffectiveTo == null || d.EffectiveTo >= orderDate))
            .OrderByDescending(d => d.EffectiveFrom ?? DateTime.MinValue)
            .FirstOrDefault();

        if (definition?.AllowManualEntry == true)
        {
            return manualOrderNumber;
        }

        // Finance PO numbering is centrally controlled by default. Ignore client-supplied
        // numbers unless the tenant sequence explicitly allows manual entry.
        return await GeneratePurchaseOrderNumberAsync(tenantId, orderDate, cancellationToken);
    }

    private static int NormalizeLineType(int lineType) => lineType == 2 ? 2 : 1;

    private static Guid? NormalizeGuid(Guid? value)
    {
        return value.HasValue && value.Value != Guid.Empty ? value.Value : null;
    }

    private static string NormalizeCurrency(string? currencyCode)
    {
        var normalized = string.IsNullOrWhiteSpace(currencyCode) ? "GHS" : currencyCode.Trim().ToUpperInvariant();
        return normalized.Length > 3 ? normalized[..3] : normalized;
    }

    private async Task<PaymentTerm?> ResolvePaymentTermAsync(Guid? paymentTermId, Guid tenantId, string applicableTo, CancellationToken cancellationToken)
    {
        if (!paymentTermId.HasValue || paymentTermId.Value == Guid.Empty)
        {
            return await _dbContext.PaymentTerms
                .Where(t =>
                    t.TenantId == tenantId && !t.IsDeleted && t.IsActive && t.IsDefault &&
                    (t.ApplicableTo == "All" || t.ApplicableTo == applicableTo || t.ApplicableTo == "Vendor"))
                .OrderBy(t => t.ApplicableTo == applicableTo ? 0 : 1)
                .ThenBy(t => t.DisplayOrder)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var paymentTerm = await _dbContext.PaymentTerms
            .FirstOrDefaultAsync(t => t.Id == paymentTermId.Value && t.TenantId == tenantId && !t.IsDeleted && t.IsActive, cancellationToken);

        if (paymentTerm == null)
        {
            throw new InvalidOperationException($"Active payment term with Id '{paymentTermId.Value}' was not found.");
        }

        if (!string.Equals(paymentTerm.ApplicableTo, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(paymentTerm.ApplicableTo, applicableTo, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Payment term '{paymentTerm.Code}' is not applicable to {applicableTo} transactions.");
        }

        return paymentTerm;
    }

    private Guid? TryGetCurrentUserId()
    {
        return Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;
    }

    private async Task NotifyFinancePurchaseOrderApproversAsync(FinancePurchaseOrder purchaseOrder, CancellationToken cancellationToken)
    {
        try
        {
            var excludedUserIds = new HashSet<Guid>();
            var currentUserId = TryGetCurrentUserId();
            if (currentUserId.HasValue) excludedUserIds.Add(currentUserId.Value);
            if (purchaseOrder.CreatedById.HasValue) excludedUserIds.Add(purchaseOrder.CreatedById.Value);

            var approverIds = await GetFinancePurchaseOrderApproverUserIdsAsync(purchaseOrder.TenantId, excludedUserIds, cancellationToken);
            foreach (var approverId in approverIds)
            {
                await _notificationService.CreateInAppNotificationAsync(
                    approverId,
                    "Finance PO awaiting approval",
                    $"{purchaseOrder.OrderNumber} requires your approval.",
                    "FinancePurchaseOrderApproval",
                    BuildPurchaseOrderNotificationData(purchaseOrder, "ApprovalRequested"),
                    purchaseOrder.TenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify finance PO approvers for {PurchaseOrderId}", purchaseOrder.Id);
        }
    }

    private async Task NotifyFinancePurchaseOrderOwnerAsync(
        FinancePurchaseOrder purchaseOrder,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!purchaseOrder.CreatedById.HasValue || purchaseOrder.CreatedById.Value == Guid.Empty)
            {
                return;
            }

            var currentUserId = TryGetCurrentUserId();
            if (currentUserId.HasValue && currentUserId.Value == purchaseOrder.CreatedById.Value)
            {
                return;
            }

            await _notificationService.CreateInAppNotificationAsync(
                purchaseOrder.CreatedById.Value,
                title,
                message,
                type,
                BuildPurchaseOrderNotificationData(purchaseOrder, type),
                purchaseOrder.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify finance PO owner for {PurchaseOrderId}", purchaseOrder.Id);
        }

        await Task.CompletedTask;
    }

    private async Task<List<Guid>> GetFinancePurchaseOrderApproverUserIdsAsync(
        Guid tenantId,
        HashSet<Guid> excludedUserIds,
        CancellationToken cancellationToken)
    {
        return await _dbContext.UserTenants
            .Where(ut => ut.TenantId == tenantId
                         && !ut.IsDeleted
                         && ut.Status == UserTenantStatus.Active
                         && (ut.ExpiresAt == null || ut.ExpiresAt > DateTime.UtcNow)
                         && ut.User.IsActive
                         && !excludedUserIds.Contains(ut.UserId)
                         && ut.User.UserRoles.Any(ur => ApproverRoleNames.Contains(ur.Role.Name)))
            .Select(ut => ut.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static Dictionary<string, object> BuildPurchaseOrderNotificationData(FinancePurchaseOrder purchaseOrder, string eventType)
    {
        return new Dictionary<string, object>
        {
            ["EntityType"] = "FinancePurchaseOrder",
            ["EntityId"] = purchaseOrder.Id,
            ["ActionUrl"] = $"/finance/ap/purchase-orders/{purchaseOrder.Id}",
            ["purchaseOrderId"] = purchaseOrder.Id,
            ["orderNumber"] = purchaseOrder.OrderNumber,
            ["eventType"] = eventType,
            ["status"] = purchaseOrder.Status,
            ["totalAmount"] = purchaseOrder.TotalAmount,
            ["currencyCode"] = purchaseOrder.CurrencyCode,
            ["vendorName"] = purchaseOrder.Vendor?.PartnerName ?? string.Empty
        };
    }
}

[Authorize]
[ApiController]
[Route("api/finance/ap/purchase-receipts")]
public class FinancePurchaseOrderReceiptController : ControllerBase
{
    private const int Approved = 2;
    private const int PartiallyReceived = 3;
    private const int Received = 4;
    private const int PartiallyInvoiced = 5;
    private const int Invoiced = 6;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly IWorkflowService _workflowService;
    private readonly FinancePurchaseOrderReceiptPostingService _receiptPostingService;
    private readonly ILogger<FinancePurchaseOrderReceiptController> _logger;

    public FinancePurchaseOrderReceiptController(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IDocumentNumberingService documentNumberingService,
        IWorkflowService workflowService,
        FinancePurchaseOrderReceiptPostingService receiptPostingService,
        ILogger<FinancePurchaseOrderReceiptController> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _documentNumberingService = documentNumberingService;
        _workflowService = workflowService;
        _receiptPostingService = receiptPostingService;
        _logger = logger;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FinancePurchaseOrderReceiptDto>>> GetAll(CancellationToken cancellationToken = default)
    {
        var receipts = await BaseReceiptQuery(TenantId)
            .OrderByDescending(receipt => receipt.ReceiptDate)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return Ok(receipts.Select(MapReceipt));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FinancePurchaseOrderReceiptDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await BaseReceiptQuery(TenantId)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return receipt == null ? NotFound() : Ok(MapReceipt(receipt));
    }

    [HttpGet("by-purchase-order/{purchaseOrderId:guid}")]
    public async Task<ActionResult<IEnumerable<FinancePurchaseOrderReceiptDto>>> GetByPurchaseOrder(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var receipts = await BaseReceiptQuery(TenantId)
            .Where(r => r.FinancePurchaseOrderId == purchaseOrderId)
            .OrderByDescending(r => r.ReceiptDate)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return Ok(receipts.Select(MapReceipt));
    }

    [HttpPost]
    public async Task<ActionResult<FinancePurchaseOrderReceiptDto>> Create(
        [FromBody] CreateFinancePurchaseOrderReceiptDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tenantId = TenantId;
        var receiptLines = dto.Lines.Count > 0 ? dto.Lines : dto.Items;
        if (receiptLines.Count == 0)
        {
            return BadRequest("At least one receipt line is required.");
        }

        // Re-read the PO and update received quantities under serializable isolation so concurrent receipts cannot over-receive the same line.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var receiptDate = dto.ReceiptDate ?? DateTime.UtcNow;
        var receiptNumber = await ResolveReceiptNumberAsync(tenantId, receiptDate, dto.ReceiptNumber, cancellationToken);

        var receiptNumberExists = await _dbContext.FinancePurchaseOrderReceipts
            .AnyAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.ReceiptNumber == receiptNumber, cancellationToken);
        if (receiptNumberExists)
        {
            return BadRequest($"Finance purchase receipt '{receiptNumber}' already exists.");
        }

        var purchaseOrder = await _dbContext.FinancePurchaseOrders
            .Include(po => po.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(po => po.Id == dto.FinancePurchaseOrderId && po.TenantId == tenantId && !po.IsDeleted, cancellationToken);

        if (purchaseOrder == null)
        {
            return BadRequest($"Finance purchase order with ID {dto.FinancePurchaseOrderId} was not found.");
        }

        if (purchaseOrder.Status != Approved && purchaseOrder.Status != PartiallyReceived)
        {
            return BadRequest("Only approved or partially received finance purchase orders can be received.");
        }

        var receipt = new FinancePurchaseOrderReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinancePurchaseOrderId = purchaseOrder.Id,
            ReceiptNumber = receiptNumber,
            ReceiptDate = receiptDate,
            Status = FinancePurchaseOrderReceiptStatus.Draft,
            Remarks = dto.Remarks
        };

        foreach (var lineDto in receiptLines)
        {
            var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == lineDto.FinancePurchaseOrderItemId && !i.IsDeleted);
            if (item == null)
            {
                return BadRequest($"Purchase order line with ID {lineDto.FinancePurchaseOrderItemId} was not found on this PO.");
            }

            if (lineDto.QuantityReceived <= 0)
            {
                return BadRequest("Receipt quantity must be greater than zero.");
            }

            var remainingQuantity = item.OrderedQuantity - item.ReceivedQuantity - item.CancelledQuantity;
            if (lineDto.QuantityReceived > remainingQuantity)
            {
                return BadRequest($"Receipt quantity for line '{item.Description}' exceeds the remaining quantity.");
            }

            receipt.Items.Add(new FinancePurchaseOrderReceiptItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FinancePurchaseOrderReceiptId = receipt.Id,
                FinancePurchaseOrderItemId = item.Id,
                QuantityReceived = lineDto.QuantityReceived,
                InvoicedQuantity = 0m,
                DiscountPercentage = lineDto.DiscountPercentage.GetValueOrDefault(),
                DiscountAmount = lineDto.DiscountAmount.GetValueOrDefault()
            });

            item.ReceivedQuantity += lineDto.QuantityReceived;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = _currentUserService.UserName;
        }

        purchaseOrder.Status = purchaseOrder.Items.All(i => i.OrderedQuantity - i.ReceivedQuantity - i.CancelledQuantity <= 0)
            ? Received
            : PartiallyReceived;
        purchaseOrder.UpdatedAt = DateTime.UtcNow;
        purchaseOrder.UpdatedBy = _currentUserService.UserName;

        _dbContext.FinancePurchaseOrderReceipts.Add(receipt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var saved = await BaseReceiptQuery(tenantId)
            .AsSplitQuery()
            .FirstAsync(r => r.Id == receipt.Id, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, MapReceipt(saved));
    }

    [HttpPost("{id:guid}/submit-for-approval")]
    public async Task<ActionResult<FinancePurchaseOrderReceiptDto>> SubmitForApproval(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var receipt = await _dbContext.FinancePurchaseOrderReceipts
            .Include(r => r.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted, cancellationToken);

        if (receipt == null)
        {
            return NotFound();
        }

        if (receipt.Status != FinancePurchaseOrderReceiptStatus.Draft)
        {
            return BadRequest("Only draft finance GRVs can be submitted for approval.");
        }

        if (!receipt.Items.Any())
        {
            return BadRequest("At least one receipt line is required before submitting for approval.");
        }

        var userId = TryGetCurrentUserId();
        var now = DateTime.UtcNow;
        receipt.Status = FinancePurchaseOrderReceiptStatus.PendingApproval;
        receipt.SubmittedAt = now;
        receipt.SubmittedById = userId;
        receipt.UpdatedAt = now;
        receipt.UpdatedBy = _currentUserService.UserName;
        receipt.RejectedAt = null;
        receipt.RejectedById = null;
        receipt.RejectionReason = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var workflowResult = await _workflowService.StartApprovalWorkflowAsync("FinancePurchaseOrderReceipt", id);
        if (!workflowResult.Success)
        {
            receipt.Status = FinancePurchaseOrderReceiptStatus.Draft;
            receipt.SubmittedAt = null;
            receipt.SubmittedById = null;
            receipt.UpdatedAt = DateTime.UtcNow;
            receipt.UpdatedBy = _currentUserService.UserName;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return BadRequest(workflowResult.Message ?? "Unable to start finance GRV approval workflow.");
        }

        receipt.WorkflowInstanceId = workflowResult.WorkflowInstanceId ?? receipt.WorkflowInstanceId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (workflowResult.Status == WorkflowInstanceStatus.Completed && userId.HasValue)
        {
            await _receiptPostingService.ApproveAndPostAsync(
                tenantId,
                receipt.Id,
                userId.Value,
                _currentUserService.UserName,
                null,
                cancellationToken);
        }

        var saved = await BaseReceiptQuery(tenantId)
            .AsSplitQuery()
            .FirstAsync(r => r.Id == receipt.Id, cancellationToken);

        return Ok(MapReceipt(saved));
    }

    [HttpPost("{id:guid}/convert-to-vendor-invoice")]
    public async Task<ActionResult<VendorInvoiceDto>> ConvertToVendorInvoice(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;

        // Read the receipt's invoice link, create the draft invoice, and write the link back under serializable
        // isolation so two concurrent conversion requests cannot both observe VendorInvoiceId as null and create
        // duplicate vendor invoices for the same receipt lines.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var receipt = await _dbContext.FinancePurchaseOrderReceipts
            .Include(r => r.FinancePurchaseOrder)
                .ThenInclude(po => po.Vendor)
            .Include(r => r.FinancePurchaseOrder)
                .ThenInclude(po => po.PaymentTerm)
            .Include(r => r.FinancePurchaseOrder)
                .ThenInclude(po => po.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.InventoryItem)
            .Include(r => r.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.FinancePurchaseOrderItem)
                    .ThenInclude(i => i.GlAccount)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted, cancellationToken);

        if (receipt == null)
        {
            return NotFound();
        }

        if (receipt.VendorInvoiceId.HasValue)
        {
            var existingInvoice = await _dbContext.VendorInvoices
                .Include(i => i.Supplier)
                .Include(i => i.LineItems)
                    .ThenInclude(li => li.GLAccount)
                .Include(i => i.PaymentAllocations)
                .FirstOrDefaultAsync(i => i.Id == receipt.VendorInvoiceId.Value && i.TenantId == tenantId && !i.IsDeleted, cancellationToken);

            return existingInvoice == null
                ? BadRequest("This receipt is linked to a vendor invoice that no longer exists.")
                : Ok(MapVendorInvoice(existingInvoice));
        }

        if (receipt.Status != FinancePurchaseOrderReceiptStatus.Approved)
        {
            return BadRequest("Only approved finance GRVs can be converted to vendor invoices.");
        }

        var billableLines = receipt.Items
            .Where(item => !item.IsDeleted && item.QuantityReceived - item.InvoicedQuantity > 0)
            .ToList();

        if (billableLines.Count == 0)
        {
            return BadRequest("This receipt has no uninvoiced quantities to convert.");
        }

        var purchaseOrder = receipt.FinancePurchaseOrder;
        var vendor = purchaseOrder.Vendor;
        if (vendor == null)
        {
            return BadRequest("The receipt's finance purchase order is missing a vendor.");
        }

        var supplier = await EnsureSupplierForVendorAsync(vendor, tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var invoiceNumber = await GenerateVendorInvoiceNumberAsync(tenantId, now, cancellationToken);
        var paymentTerm = purchaseOrder.PaymentTerm;
        if (paymentTerm == null && vendor.PaymentTermId.HasValue)
        {
            paymentTerm = await _dbContext.PaymentTerms
                .FirstOrDefaultAsync(t => t.Id == vendor.PaymentTermId.Value && t.TenantId == tenantId && !t.IsDeleted && t.IsActive, cancellationToken);
        }

        var paymentTermsDays = paymentTerm?.DueDays ?? 30;
        var earlyPaymentDiscountPercentage = paymentTerm?.DiscountPercent ?? 0m;
        DateTime? earlyPaymentDiscountDueDate = paymentTerm != null && paymentTerm.DiscountPercent > 0 && paymentTerm.DiscountDays > 0
            ? receipt.ReceiptDate.AddDays(paymentTerm.DiscountDays)
            : null;

        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            SupplierInvoiceNumber = receipt.ReceiptNumber,
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            InvoiceDate = receipt.ReceiptDate,
            ReceivedDate = now,
            DueDate = receipt.ReceiptDate.AddDays(paymentTermsDays),
            CurrencyCode = purchaseOrder.CurrencyCode,
            ExchangeRate = purchaseOrder.ExchangeRate <= 0 ? 1m : purchaseOrder.ExchangeRate,
            PaymentTermsDays = paymentTermsDays,
            PaymentTermId = paymentTerm?.Id,
            EarlyPaymentDiscountPercentage = earlyPaymentDiscountPercentage,
            EarlyPaymentDiscountDueDate = earlyPaymentDiscountDueDate,
            MatchingType = InvoiceMatchingType.ThreeWay,
            MatchingStatus = InvoiceMatchingStatus.ThreeWayMatched,
            MatchingNotes = $"Created from finance purchase receipt {receipt.ReceiptNumber} for PO {purchaseOrder.OrderNumber}.",
            Status = VendorInvoiceStatus.Draft,
            ApprovalStatus = "Draft",
            Notes = receipt.Remarks,
            Reference = $"{purchaseOrder.OrderNumber}/{receipt.ReceiptNumber}",
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "system"
        };

        decimal subtotal = 0m;
        decimal totalTax = 0m;
        decimal totalDiscount = 0m;

        foreach (var receiptItem in billableLines)
        {
            var poItem = receiptItem.FinancePurchaseOrderItem;
            if (poItem == null)
            {
                return BadRequest($"Receipt line '{receiptItem.Id}' is missing its purchase order line.");
            }

            var quantityToInvoice = receiptItem.QuantityReceived - receiptItem.InvoicedQuantity;
            var discountPercentage = receiptItem.DiscountPercentage > 0m
                ? receiptItem.DiscountPercentage
                : poItem.DiscountPercentage;
            var lineGross = quantityToInvoice * poItem.UnitPrice;
            var lineDiscount = receiptItem.DiscountAmount > 0m
                ? receiptItem.DiscountAmount
                : lineGross * (discountPercentage / 100m);
            var lineNet = lineGross - lineDiscount;
            var lineTax = lineNet * (poItem.TaxRate / 100m);

            invoice.LineItems.Add(new VendorInvoiceLineItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VendorInvoiceId = invoice.Id,
                LineItemType = poItem.LineType == 1 ? "Product" : "Expense",
                GLAccountId = poItem.GlAccountId,
                // Finance PO lines are not legacy PurchaseOrderItem rows; keep the legacy FK null to avoid cross-model FK violations.
                PurchaseOrderItemId = null,
                InventoryItemId = poItem.InventoryItemId,
                WarehouseId = poItem.WarehouseId,
                Description = poItem.Description,
                Quantity = quantityToInvoice,
                UnitPrice = poItem.UnitPrice,
                TaxRate = poItem.TaxRate,
                TaxAmount = lineTax,
                TaxCode = poItem.TaxCode,
                DiscountPercentage = discountPercentage,
                DiscountAmount = lineDiscount,
                Unit = "Each",
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "system"
            });

            receiptItem.InvoicedQuantity += quantityToInvoice;
            receiptItem.UpdatedAt = now;
            receiptItem.UpdatedBy = _currentUserService.UserName;

            poItem.InvoicedQuantity += quantityToInvoice;
            poItem.UpdatedAt = now;
            poItem.UpdatedBy = _currentUserService.UserName;

            subtotal += lineNet;
            totalTax += lineTax;
            totalDiscount += lineDiscount;
        }

        invoice.SubTotal = subtotal;
        invoice.TaxAmount = totalTax;
        invoice.DiscountAmount = totalDiscount;
        invoice.TotalAmount = subtotal + totalTax;
        invoice.PaidAmount = 0m;
        invoice.BaseCurrencyAmount = invoice.TotalAmount * invoice.ExchangeRate;

        receipt.VendorInvoiceId = invoice.Id;
        receipt.UpdatedAt = now;
        receipt.UpdatedBy = _currentUserService.UserName;

        purchaseOrder.Status = purchaseOrder.Items.All(i => i.InvoicedQuantity >= i.OrderedQuantity - i.CancelledQuantity)
            ? Invoiced
            : PartiallyInvoiced;
        purchaseOrder.UpdatedAt = now;
        purchaseOrder.UpdatedBy = _currentUserService.UserName;

        _dbContext.VendorInvoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var savedInvoice = await _dbContext.VendorInvoices
            .Include(i => i.Supplier)
            .Include(i => i.LineItems)
                .ThenInclude(li => li.GLAccount)
            .Include(i => i.PaymentAllocations)
            .FirstAsync(i => i.Id == invoice.Id, cancellationToken);

        return Ok(MapVendorInvoice(savedInvoice));
    }

    private IQueryable<FinancePurchaseOrderReceipt> BaseReceiptQuery(Guid tenantId)
    {
        return _dbContext.FinancePurchaseOrderReceipts
            .Include(r => r.FinancePurchaseOrder)
                .ThenInclude(po => po.Vendor)
            .Include(r => r.FinancePurchaseOrder)
                .ThenInclude(po => po.PaymentTerm)
            .Include(r => r.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.FinancePurchaseOrderItem)
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);
    }

    private static FinancePurchaseOrderReceiptDto MapReceipt(FinancePurchaseOrderReceipt receipt)
    {
        return new FinancePurchaseOrderReceiptDto
        {
            Id = receipt.Id,
            FinancePurchaseOrderId = receipt.FinancePurchaseOrderId,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate,
            Remarks = receipt.Remarks,
            Status = (int)receipt.Status,
            StatusName = receipt.Status.ToString(),
            WorkflowInstanceId = receipt.WorkflowInstanceId,
            VendorInvoiceId = receipt.VendorInvoiceId,
            SubmittedAt = receipt.SubmittedAt,
            ApprovedAt = receipt.ApprovedAt,
            RejectedAt = receipt.RejectedAt,
            RejectionReason = receipt.RejectionReason,
            OrderNumber = receipt.FinancePurchaseOrder?.OrderNumber,
            VendorName = receipt.FinancePurchaseOrder?.Vendor?.PartnerName,
            Items = receipt.Items
                .Where(item => !item.IsDeleted)
                .OrderBy(item => item.CreatedAt)
                .Select(item =>
                {
                    var poItem = item.FinancePurchaseOrderItem;
                    return new FinancePurchaseOrderReceiptItemDto
                    {
                        Id = item.Id,
                        FinancePurchaseOrderReceiptId = item.FinancePurchaseOrderReceiptId,
                        FinancePurchaseOrderItemId = item.FinancePurchaseOrderItemId,
                        QuantityReceived = item.QuantityReceived,
                        InvoicedQuantity = item.InvoicedQuantity,
                        RemainingToInvoice = item.QuantityReceived - item.InvoicedQuantity,
                        DiscountPercentage = item.DiscountPercentage,
                        DiscountAmount = item.DiscountAmount,
                        Description = poItem?.Description,
                        OrderedQuantity = poItem?.OrderedQuantity ?? 0m,
                        PreviouslyReceived = Math.Max(0m, (poItem?.ReceivedQuantity ?? 0m) - item.QuantityReceived)
                    };
                })
                .ToList()
        };
    }

    private Guid? TryGetCurrentUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private async Task<string> GenerateReceiptNumberAsync(Guid tenantId, DateTime receiptDate, CancellationToken cancellationToken)
    {
        // Receipt creation already holds a serializable transaction; document
        // numbering joins it so the reservation and GRV insert commit together.
        return await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            FinanceDocumentTypes.FinancePurchaseOrderReceipt,
            tenantId,
            receiptDate,
            nameof(FinancePurchaseOrderReceipt),
            cancellationToken: cancellationToken);
    }

    private async Task<string> ResolveReceiptNumberAsync(
        Guid tenantId,
        DateTime receiptDate,
        string? requestedReceiptNumber,
        CancellationToken cancellationToken)
    {
        var manualReceiptNumber = requestedReceiptNumber?.Trim();
        if (string.IsNullOrWhiteSpace(manualReceiptNumber))
        {
            return await GenerateReceiptNumberAsync(tenantId, receiptDate, cancellationToken);
        }

        var definitions = await _documentNumberingService.GetDefinitionsAsync(
            DocumentNumberingModules.Finance,
            tenantId,
            cancellationToken);

        var definition = definitions
            .Where(d => d.DocumentType == FinanceDocumentTypes.FinancePurchaseOrderReceipt
                && d.IsActive
                && d.IsDefault
                && (d.EffectiveFrom == null || d.EffectiveFrom <= receiptDate)
                && (d.EffectiveTo == null || d.EffectiveTo >= receiptDate))
            .OrderByDescending(d => d.EffectiveFrom ?? DateTime.MinValue)
            .FirstOrDefault();

        if (definition?.AllowManualEntry == true)
        {
            return manualReceiptNumber;
        }

        // Finance GRV numbering is centrally controlled by default. Ignore client-supplied
        // numbers unless the tenant sequence explicitly allows manual entry.
        return await GenerateReceiptNumberAsync(tenantId, receiptDate, cancellationToken);
    }

    private async Task<Supplier> EnsureSupplierForVendorAsync(
        BusinessPartner vendor,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                !s.IsDeleted &&
                (s.Id == vendor.Id ||
                 s.SupplierCode == vendor.PartnerCode ||
                 s.Name == vendor.PartnerName),
                cancellationToken);

        if (supplier != null)
        {
            return supplier;
        }

        supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = string.IsNullOrWhiteSpace(vendor.PartnerCode)
                ? $"BP-{vendor.Id.ToString("N")[..8].ToUpperInvariant()}"
                : vendor.PartnerCode,
            Name = vendor.PartnerName,
            SupplierType = vendor.PartnerType.Contains("Manufacturer", StringComparison.OrdinalIgnoreCase)
                ? "Manufacturer"
                : "Vendor",
            Address = vendor.PhysicalAddress ?? vendor.MailingAddress,
            City = vendor.PhysicalCity ?? vendor.MailingCity,
            State = vendor.PhysicalState ?? vendor.MailingState,
            Country = vendor.PhysicalCountry ?? vendor.MailingCountry,
            ZipCode = vendor.PhysicalPostalCode ?? vendor.MailingPostalCode,
            Phone = vendor.PrimaryPhone,
            Email = vendor.PrimaryEmail,
            Website = vendor.Website,
            PrimaryContactName = vendor.PrimaryContactName,
            PrimaryContactTitle = vendor.PrimaryContactTitle,
            PrimaryContactPhone = vendor.PrimaryPhone,
            PrimaryContactEmail = vendor.PrimaryEmail,
            TaxId = vendor.TaxIdentificationNumber ?? vendor.VATNumber,
            PaymentTerms = vendor.PaymentTerms ?? "Net 30",
            PaymentTermId = vendor.PaymentTermId,
            IsActive = vendor.IsActive,
            IsPreferred = vendor.IsPreferred,
            Status = vendor.IsActive ? "Active" : "Inactive",
            Notes = $"Auto-created from business partner {vendor.PartnerCode} for Finance AP invoice conversion.",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "system"
        };

        _dbContext.Suppliers.Add(supplier);
        return supplier;
    }

    private async Task<string> GenerateVendorInvoiceNumberAsync(
        Guid tenantId,
        DateTime documentDate,
        CancellationToken cancellationToken)
    {
        return await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            FinanceDocumentTypes.APInvoice,
            tenantId,
            documentDate,
            nameof(VendorInvoice),
            cancellationToken: cancellationToken);
    }

    private static VendorInvoiceDto MapVendorInvoice(VendorInvoice invoice)
    {
        return new VendorInvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            SupplierInvoiceNumber = invoice.SupplierInvoiceNumber,
            SupplierId = invoice.SupplierId,
            SupplierName = invoice.SupplierName,
            PurchaseOrderId = invoice.PurchaseOrderId,
            PurchaseOrderNumber = invoice.PurchaseOrder?.OrderNumber,
            InvoiceDate = invoice.InvoiceDate,
            ReceivedDate = invoice.ReceivedDate,
            DueDate = invoice.DueDate,
            SubTotal = invoice.SubTotal,
            TaxAmount = invoice.TaxAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = invoice.PaidAmount,
            BalanceAmount = invoice.BalanceAmount,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            BaseCurrencyAmount = invoice.BaseCurrencyAmount,
            PaymentTermsDays = invoice.PaymentTermsDays,
            EarlyPaymentDiscountPercentage = invoice.EarlyPaymentDiscountPercentage,
            EarlyPaymentDiscountDueDate = invoice.EarlyPaymentDiscountDueDate,
            EarlyPaymentDiscountAmount = invoice.EarlyPaymentDiscountAmount,
            WithholdingTaxRate = invoice.WithholdingTaxRate,
            WithholdingTaxAmount = invoice.WithholdingTaxAmount,
            MatchingType = invoice.MatchingType,
            MatchingStatus = invoice.MatchingStatus,
            MatchingNotes = invoice.MatchingNotes,
            Status = invoice.Status,
            ApprovalStatus = invoice.ApprovalStatus,
            ExpenseAccountId = invoice.ExpenseAccountId,
            ExpenseAccountName = invoice.ExpenseAccount?.AccountName,
            ApAccountId = invoice.ApAccountId,
            ApAccountName = invoice.ApAccount?.AccountName,
            Notes = invoice.Notes,
            Reference = invoice.Reference,
            LineItems = invoice.LineItems
                .Where(li => !li.IsDeleted)
                .Select(li => new VendorInvoiceLineItemDto
                {
                    Id = li.Id,
                    VendorInvoiceId = li.VendorInvoiceId,
                    LineItemType = li.LineItemType,
                    GLAccountId = li.GLAccountId,
                    GLAccountName = li.GLAccount?.AccountName,
                    PurchaseOrderItemId = li.PurchaseOrderItemId,
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice,
                    LineTotal = li.Quantity * li.UnitPrice,
                    TaxRate = li.TaxRate,
                    TaxAmount = li.TaxAmount,
                    TaxCode = li.TaxCode,
                    DiscountPercentage = li.DiscountPercentage,
                    DiscountAmount = li.DiscountAmount,
                    Unit = li.Unit
                })
                .ToList(),
            PaymentAllocations = invoice.PaymentAllocations?
                .Where(a => !a.IsDeleted)
                .Select(a => new VendorPaymentAllocationDto
                {
                    Id = a.Id,
                    VendorPaymentId = a.VendorPaymentId,
                    VendorInvoiceId = a.VendorInvoiceId,
                    AllocatedAmount = a.AllocatedAmount,
                    DiscountAmount = a.DiscountAmount,
                    WithholdingTaxAmount = a.WithholdingTaxAmount,
                    AllocationDate = a.AllocationDate,
                    Notes = a.Notes,
                    IsReversal = a.IsReversal
                })
                .ToList() ?? new List<VendorPaymentAllocationDto>(),
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }
}
