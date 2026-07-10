using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class SalesOrderService : ISalesOrderService
{
    private const string WorkflowEntityType = "SalesOrder";

    private readonly IGenericRepository<SalesOrder> _salesOrderRepo;
    private readonly IGenericRepository<SalesOrderLine> _lineRepo;
    private readonly IGenericRepository<SalesOrderStatusHistory> _historyRepo;
    private readonly IGenericRepository<BusinessPartner> _bpRepo;
    private readonly IGenericRepository<Quote> _quoteRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ILogger<SalesOrderService> _logger;
    private readonly IDocumentNumberingService _documentNumberingService;

    public SalesOrderService(
        IGenericRepository<SalesOrder> salesOrderRepo,
        IGenericRepository<SalesOrderLine> lineRepo,
        IGenericRepository<SalesOrderStatusHistory> historyRepo,
        IGenericRepository<BusinessPartner> bpRepo,
        IGenericRepository<Quote> quoteRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IDocumentNumberingService documentNumberingService,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ILogger<SalesOrderService> logger)
    {
        _salesOrderRepo = salesOrderRepo;
        _lineRepo = lineRepo;
        _historyRepo = historyRepo;
        _bpRepo = bpRepo;
        _quoteRepo = quoteRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _logger = logger;
        // Document numbering is kept with workflow governance so merged Sales orders remain traceable and approval-controlled.
        _documentNumberingService = documentNumberingService;
    }

    #region CRUD

    public async Task<SalesOrderDetailDto> CreateSalesOrderAsync(CreateSalesOrderDto dto)
    {
        try
        {
            var bp = await _bpRepo.GetByIdAsync(dto.BusinessPartnerId)
                ?? throw new InvalidOperationException($"Business Partner {dto.BusinessPartnerId} not found");

            var salesOrder = new SalesOrder
            {
                DocumentNumber = await GenerateOrderNumberAsync(),
                DocumentDate = DateTime.UtcNow,
                BusinessPartnerId = dto.BusinessPartnerId,
                CustomerName = bp.PartnerName,
                OrderType = dto.OrderType,
                OrderStatus = SalesOrderStatus.Draft,
                ApprovalStatus = "Draft",
                OrderPriority = dto.OrderPriority ?? "Normal",
                SalesRepId = dto.SalesRepId,
                Currency = dto.Currency ?? "GHS",
                DiscountAmount = dto.DiscountAmount ?? 0,
                DiscountPercentage = dto.DiscountPercentage ?? 0,
                ShippingAmount = dto.ShippingAmount ?? 0,
                PaymentTermId = dto.PaymentTermId ?? bp.PaymentTermId,
                PaymentTermsDays = bp.PaymentTerms != null ? 30 : 30, // Default
                RequestedDeliveryDate = dto.RequestedDeliveryDate,
                PromisedDeliveryDate = dto.PromisedDeliveryDate,
                ShipmentMethod = dto.ShipmentMethod,
                ShippingAddress = dto.ShippingAddress ?? bp.PhysicalAddress,
                BillingAddress = dto.BillingAddress ?? bp.MailingAddress ?? bp.PhysicalAddress,
                DeliveryInstructions = dto.DeliveryInstructions,
                WarehouseId = dto.WarehouseId,
                QuoteId = dto.QuoteId,
                OpportunityId = dto.OpportunityId,
                PropertyReference = dto.PropertyReference,
                PropertyType = dto.PropertyType,
                Terms = dto.Terms,
                InternalNotes = dto.InternalNotes,
                ExternalNotes = dto.ExternalNotes,
                ReferenceNumber = dto.ReferenceNumber ?? string.Empty,
                TenantId = bp.TenantId
            };

            // Calculate totals from line items
            decimal subTotal = 0;
            decimal totalTax = 0;
            int lineNumber = 1;

            await _salesOrderRepo.AddAsync(salesOrder);

            foreach (var lineDto in dto.Lines)
            {
                var line = new SalesOrderLine
                {
                    SalesOrderId = salesOrder.Id,
                    LineNumber = lineNumber++,
                    ProductId = lineDto.ProductId,
                    InventoryItemId = lineDto.InventoryItemId,
                    Description = lineDto.Description,
                    ProductCode = lineDto.ProductCode,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    DiscountPercentage = lineDto.DiscountPercentage ?? 0,
                    DiscountAmount = lineDto.DiscountAmount ?? 0,
                    TaxRate = lineDto.TaxRate ?? 0,
                    TaxCode = lineDto.TaxCode,
                    Unit = lineDto.Unit,
                    WarehouseId = lineDto.WarehouseId ?? dto.WarehouseId,
                    LocationId = lineDto.LocationId,
                    GLAccountId = lineDto.GLAccountId,
                    SerialNumber = lineDto.SerialNumber,
                    LotNumber = lineDto.LotNumber,
                    ExpirationDate = lineDto.ExpirationDate,
                    Notes = lineDto.Notes,
                    TenantId = bp.TenantId
                };

                line.TaxAmount = line.LineTotal * (line.TaxRate / 100);
                subTotal += line.LineTotal - line.DiscountAmount;
                totalTax += line.TaxAmount;

                await _lineRepo.AddAsync(line);
            }

            salesOrder.SubTotal = subTotal;
            salesOrder.TaxAmount = dto.TaxAmount ?? totalTax;
            salesOrder.TotalAmount = subTotal + salesOrder.TaxAmount + salesOrder.ShippingAmount - salesOrder.DiscountAmount;

            // Record initial status
            await RecordStatusChangeAsync(salesOrder.Id, null, SalesOrderStatus.Draft, "Sales Order created", bp.TenantId);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created Sales Order {OrderNumber} for customer {CustomerName}",
                salesOrder.DocumentNumber, salesOrder.CustomerName);

            return await GetSalesOrderByIdAsync(salesOrder.Id)
                ?? throw new InvalidOperationException("Failed to retrieve created Sales Order");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Sales Order");
            throw;
        }
    }

    public async Task<SalesOrderDetailDto> UpdateSalesOrderAsync(Guid id, UpdateSalesOrderDto dto)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");

            if (so.OrderStatus != SalesOrderStatus.Draft)
                throw new InvalidOperationException($"Cannot edit Sales Order in {so.OrderStatus} status");

            if (dto.OrderPriority != null) so.OrderPriority = dto.OrderPriority;
            if (dto.SalesRepId.HasValue) so.SalesRepId = dto.SalesRepId;
            if (dto.RequestedDeliveryDate.HasValue) so.RequestedDeliveryDate = dto.RequestedDeliveryDate;
            if (dto.PromisedDeliveryDate.HasValue) so.PromisedDeliveryDate = dto.PromisedDeliveryDate;
            if (dto.DiscountAmount.HasValue) so.DiscountAmount = dto.DiscountAmount.Value;
            if (dto.DiscountPercentage.HasValue) so.DiscountPercentage = dto.DiscountPercentage.Value;
            if (dto.ShippingAmount.HasValue) so.ShippingAmount = dto.ShippingAmount.Value;
            if (dto.PaymentTermId.HasValue) so.PaymentTermId = dto.PaymentTermId;
            if (dto.ShipmentMethod.HasValue) so.ShipmentMethod = dto.ShipmentMethod;
            if (dto.ShippingAddress != null) so.ShippingAddress = dto.ShippingAddress;
            if (dto.BillingAddress != null) so.BillingAddress = dto.BillingAddress;
            if (dto.DeliveryInstructions != null) so.DeliveryInstructions = dto.DeliveryInstructions;
            if (dto.WarehouseId.HasValue) so.WarehouseId = dto.WarehouseId;
            if (dto.PropertyReference != null) so.PropertyReference = dto.PropertyReference;
            if (dto.PropertyType.HasValue) so.PropertyType = dto.PropertyType;
            if (dto.Terms != null) so.Terms = dto.Terms;
            if (dto.InternalNotes != null) so.InternalNotes = dto.InternalNotes;
            if (dto.ExternalNotes != null) so.ExternalNotes = dto.ExternalNotes;
            if (dto.ReferenceNumber != null) so.ReferenceNumber = dto.ReferenceNumber;

            // If lines are provided, replace them
            if (dto.Lines != null)
            {
                var existingLines = await _lineRepo.FindAsync(l => l.SalesOrderId == id);
                foreach (var line in existingLines)
                    await _lineRepo.DeleteAsync(line);

                decimal subTotal = 0;
                decimal totalTax = 0;
                int lineNumber = 1;

                foreach (var lineDto in dto.Lines)
                {
                    var line = new SalesOrderLine
                    {
                        SalesOrderId = id,
                        LineNumber = lineNumber++,
                        ProductId = lineDto.ProductId,
                        InventoryItemId = lineDto.InventoryItemId,
                        Description = lineDto.Description,
                        ProductCode = lineDto.ProductCode,
                        Quantity = lineDto.Quantity,
                        UnitPrice = lineDto.UnitPrice,
                        DiscountPercentage = lineDto.DiscountPercentage ?? 0,
                        DiscountAmount = lineDto.DiscountAmount ?? 0,
                        TaxRate = lineDto.TaxRate ?? 0,
                        TaxCode = lineDto.TaxCode,
                        Unit = lineDto.Unit,
                        WarehouseId = lineDto.WarehouseId ?? so.WarehouseId,
                        LocationId = lineDto.LocationId,
                        GLAccountId = lineDto.GLAccountId,
                        Notes = lineDto.Notes,
                        TenantId = so.TenantId
                    };

                    line.TaxAmount = line.LineTotal * (line.TaxRate / 100);
                    subTotal += line.LineTotal - line.DiscountAmount;
                    totalTax += line.TaxAmount;

                    await _lineRepo.AddAsync(line);
                }

                so.SubTotal = subTotal;
                so.TaxAmount = totalTax;
                so.TotalAmount = subTotal + so.TaxAmount + so.ShippingAmount - so.DiscountAmount;
            }

            await _salesOrderRepo.UpdateAsync(so);
            await _unitOfWork.SaveChangesAsync();

            return await GetSalesOrderByIdAsync(id)
                ?? throw new InvalidOperationException("Failed to retrieve updated Sales Order");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Sales Order {SalesOrderId}", id);
            throw;
        }
    }

    public async Task<SalesOrderDetailDto?> GetSalesOrderByIdAsync(Guid id)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id,
                s => s.BusinessPartner,
                s => s.SalesRep!,
                s => s.Lines,
                s => s.StatusHistory,
                s => s.DeliveryNotes);

            if (so == null) return null;

            var projectUnitContext = await GetProjectUnitContextAsync(so.Id, so.TenantId);
            return MapToDetailDto(so, projectUnitContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Sales Order {SalesOrderId}", id);
            throw;
        }
    }

    public async Task<PagedResult<SalesOrderSummaryDto>> GetSalesOrdersAsync(
        int page, int pageSize,
        string? search = null,
        SalesOrderStatus? status = null,
        Guid? customerId = null,
        Guid? salesRepId = null,
        SalesOrderType? orderType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? orderPriority = null,
        bool projectLinkedOnly = false,
        bool releasedUnitsOnly = false)
    {
        try
        {
            var query = _salesOrderRepo.GetQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var normalizedSearch = search.ToLower();
                query = query.Where(s =>
                    s.DocumentNumber.ToLower().Contains(normalizedSearch)
                    || s.CustomerName.ToLower().Contains(normalizedSearch)
                    || (s.PropertyReference != null && s.PropertyReference.ToLower().Contains(normalizedSearch))
                    || _unitOfWork.Repository<ProjectUnit>().GetQueryable().Any(unit =>
                        unit.TenantId == s.TenantId
                        && unit.SalesOrderId == s.Id
                        && !unit.IsDeleted
                        && (
                            (unit.Code != null && unit.Code.ToLower().Contains(normalizedSearch))
                            || unit.Name.ToLower().Contains(normalizedSearch)
                            || unit.Project.ProjectCode.ToLower().Contains(normalizedSearch)
                            || unit.Project.Title.ToLower().Contains(normalizedSearch))));
            }
            if (status.HasValue)
                query = query.Where(s => s.OrderStatus == status.Value);
            if (customerId.HasValue)
                query = query.Where(s => s.BusinessPartnerId == customerId.Value);
            if (salesRepId.HasValue)
                query = query.Where(s => s.SalesRepId == salesRepId.Value);
            if (orderType.HasValue)
                query = query.Where(s => s.OrderType == orderType.Value);
            if (startDate.HasValue)
                query = query.Where(s => s.DocumentDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(s => s.DocumentDate <= endDate.Value);
            if (!string.IsNullOrEmpty(orderPriority))
                query = query.Where(s => s.OrderPriority == orderPriority);
            if (projectLinkedOnly)
                query = query.Where(s => _unitOfWork.Repository<ProjectUnit>().GetQueryable().Any(unit =>
                    unit.TenantId == s.TenantId
                    && unit.SalesOrderId == s.Id
                    && !unit.IsDeleted));
            if (releasedUnitsOnly)
                query = query.Where(s => _unitOfWork.Repository<ProjectUnit>().GetQueryable().Any(unit =>
                    unit.TenantId == s.TenantId
                    && unit.SalesOrderId == s.Id
                    && unit.IsReleasedForMarket
                    && !unit.IsDeleted));

            var totalCount = await query.CountAsync();
            var items = await query
                .Include(s => s.BusinessPartner)
                .Include(s => s.SalesRep)
                .Include(s => s.Lines)
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var projectUnitContextLookup = await GetProjectUnitContextsBySalesOrderIdsAsync(
                items.Select(item => item.Id),
                items.FirstOrDefault()?.TenantId);

            return new PagedResult<SalesOrderSummaryDto>
            {
                Items = items.Select(item => MapToSummaryDto(
                    item,
                    projectUnitContextLookup.TryGetValue(item.Id, out var context) ? context : null)).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Sales Orders");
            throw;
        }
    }

    #endregion

    #region Lifecycle Actions

    public async Task<SalesOrderDetailDto> SubmitForApprovalAsync(Guid id)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");

            if (so.OrderStatus != SalesOrderStatus.Draft)
                throw new InvalidOperationException($"Cannot submit Sales Order in {so.OrderStatus} status");

            var userId = _currentUserProvider.UserId;
            if (userId == Guid.Empty)
                throw new UnauthorizedAccessException("User is not authenticated");

            var previousStatus = so.OrderStatus;
            var workflowResult = await _workflowIntegrationService.SubmitAsync(WorkflowEntityType, id);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start workflow");

            var adapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
            adapter.ApplySubmitOutcome(so, workflowResult.Outcome, userId);

            await _salesOrderRepo.UpdateAsync(so);
            if (previousStatus != so.OrderStatus)
            {
                await RecordStatusChangeAsync(so.Id, previousStatus, so.OrderStatus, "Submitted for approval", so.TenantId);
            }

            await SyncLinkedProjectUnitsForSalesOrderAsync(so);
            await _unitOfWork.SaveChangesAsync();

            var workflowResult = await _workflowIntegrationService.SubmitAsync("SalesOrder", id);
            if (!workflowResult.ExecutionResult.Success)
            {
                so.OrderStatus = SalesOrderStatus.Draft;
                so.ApprovalStatus = "Draft";
                so.SubmittedById = null;
                so.SubmittedDate = null;
                await _salesOrderRepo.UpdateAsync(so);
                await _unitOfWork.SaveChangesAsync();

                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Unable to start sales order approval workflow.");
            }

            _logger.LogInformation("Sales Order {OrderNumber} submitted for approval", so.DocumentNumber);
            return await GetSalesOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting Sales Order {SalesOrderId} for approval", id);
            throw;
        }
    }

    public async Task<SalesOrderDetailDto> ProcessApprovalAsync(Guid id, SalesOrderApprovalDto dto)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");

            if (so.OrderStatus != SalesOrderStatus.PendingApproval)
                throw new InvalidOperationException($"Sales Order is not pending approval");

            var userId = _currentUserProvider.UserId;
            if (userId == Guid.Empty)
                throw new UnauthorizedAccessException("User is not authenticated");

            // Keep the workflow assignment guard before applying Sales/Finance state changes.
            var canApprove = await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, id, userId);
            if (!canApprove)
                throw new UnauthorizedAccessException("You are not assigned to approve the current workflow step");

            var previousStatus = so.OrderStatus;
            var comments = dto.Approved
                ? dto.Comments
                : dto.RejectionReason ?? dto.Comments;

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                WorkflowEntityType,
                id,
                userId,
                dto.Approved ? "Approve" : "Reject",
                comments);

            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval");

            var adapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
            adapter.ApplyApprovalOutcome(so, workflowResult.Outcome, userId, comments);

            if (previousStatus != so.OrderStatus)
            {
                var historyNote = workflowResult.Outcome switch
                {
                    WorkflowOutcome.Approved => comments ?? "Approved",
                    WorkflowOutcome.Rejected => comments ?? "Rejected",
                    _ => comments ?? "Workflow step processed"
                };

                await RecordStatusChangeAsync(so.Id, previousStatus, so.OrderStatus, historyNote, so.TenantId);
            }

            if (workflowResult.Outcome == WorkflowOutcome.Approved)
            {
                _logger.LogInformation("Sales Order {OrderNumber} approved", so.DocumentNumber);
            }
            else if (workflowResult.Outcome == WorkflowOutcome.Rejected)
            {
                _logger.LogInformation("Sales Order {OrderNumber} rejected: {Reason}", so.DocumentNumber, comments);
            }

            await _salesOrderRepo.UpdateAsync(so);
            await SyncLinkedProjectUnitsForSalesOrderAsync(so);
            await _unitOfWork.SaveChangesAsync();

            return await GetSalesOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing approval for Sales Order {SalesOrderId}", id);
            throw;
        }
    }

    public async Task<SalesOrderDetailDto> ConfirmSalesOrderAsync(Guid id)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id, s => s.Lines, s => s.BusinessPartner)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");

            // Allow confirming from PendingApproval if using direct confirm (no approval workflow)
            if (so.OrderStatus != SalesOrderStatus.PendingApproval && so.OrderStatus != SalesOrderStatus.Draft)
                throw new InvalidOperationException($"Cannot confirm Sales Order in {so.OrderStatus} status");

            // Validate credit limit
            if (!await ValidateCreditLimitAsync(so.BusinessPartnerId, so.TotalAmount))
                throw new InvalidOperationException("Order exceeds customer's available credit limit");

            var previousStatus = so.OrderStatus;
            so.OrderStatus = SalesOrderStatus.Confirmed;
            so.ApprovalStatus = "Approved";
            so.ApprovedById = _currentUserProvider.UserId;
            so.ApprovedDate = DateTime.UtcNow;

            // TODO: Reserve stock for each line item via Inventory service
            // This will be wired up when integrating with the Inventory module

            await _salesOrderRepo.UpdateAsync(so);
            await RecordStatusChangeAsync(so.Id, previousStatus, SalesOrderStatus.Confirmed, "Sales Order confirmed — stock reserved", so.TenantId);
            await SyncLinkedProjectUnitsForSalesOrderAsync(so);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Sales Order {OrderNumber} confirmed", so.DocumentNumber);
            return await GetSalesOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming Sales Order {SalesOrderId}", id);
            throw;
        }
    }

    public async Task<SalesOrderDetailDto> CancelSalesOrderAsync(Guid id, CancelSalesOrderDto dto)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");

            if (so.OrderStatus == SalesOrderStatus.Closed || so.OrderStatus == SalesOrderStatus.Cancelled)
                throw new InvalidOperationException($"Sales Order is already {so.OrderStatus}");

            var previousStatus = so.OrderStatus;
            so.OrderStatus = SalesOrderStatus.Cancelled;

            // TODO: Release reserved stock via Inventory service

            await _salesOrderRepo.UpdateAsync(so);
            await RecordStatusChangeAsync(so.Id, previousStatus, SalesOrderStatus.Cancelled, dto.Reason, so.TenantId);
            await SyncLinkedProjectUnitsForSalesOrderAsync(so);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Sales Order {OrderNumber} cancelled: {Reason}", so.DocumentNumber, dto.Reason);
            return await GetSalesOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Sales Order {SalesOrderId}", id);
            throw;
        }
    }

    public async Task<SalesOrderDetailDto> PutOnHoldAsync(Guid id, string? reason = null)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");

            var previousStatus = so.OrderStatus;
            so.OrderStatus = SalesOrderStatus.OnHold;

            await _salesOrderRepo.UpdateAsync(so);
            await RecordStatusChangeAsync(so.Id, previousStatus, SalesOrderStatus.OnHold, reason ?? "Put on hold", so.TenantId);
            await SyncLinkedProjectUnitsForSalesOrderAsync(so);
            await _unitOfWork.SaveChangesAsync();

            return await GetSalesOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error putting Sales Order {SalesOrderId} on hold", id);
            throw;
        }
    }

    public async Task<SalesOrderDetailDto> ReleaseFromHoldAsync(Guid id)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");

            if (so.OrderStatus != SalesOrderStatus.OnHold)
                throw new InvalidOperationException("Sales Order is not on hold");

            so.OrderStatus = SalesOrderStatus.Confirmed;
            await _salesOrderRepo.UpdateAsync(so);
            await RecordStatusChangeAsync(so.Id, SalesOrderStatus.OnHold, SalesOrderStatus.Confirmed, "Released from hold", so.TenantId);
            await SyncLinkedProjectUnitsForSalesOrderAsync(so);
            await _unitOfWork.SaveChangesAsync();

            return await GetSalesOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing Sales Order {SalesOrderId} from hold", id);
            throw;
        }
    }

    public async Task<SalesOrderDetailDto> CloseSalesOrderAsync(Guid id)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");

            if (so.OrderStatus != SalesOrderStatus.Delivered && so.OrderStatus != SalesOrderStatus.Invoiced)
                throw new InvalidOperationException($"Cannot close Sales Order in {so.OrderStatus} status");

            var previousStatus = so.OrderStatus;
            so.OrderStatus = SalesOrderStatus.Closed;
            await _salesOrderRepo.UpdateAsync(so);
            await RecordStatusChangeAsync(so.Id, previousStatus, SalesOrderStatus.Closed, "Sales Order closed", so.TenantId);
            await SyncLinkedProjectUnitsForSalesOrderAsync(so);
            await _unitOfWork.SaveChangesAsync();

            return await GetSalesOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing Sales Order {SalesOrderId}", id);
            throw;
        }
    }

    #endregion

    #region Conversion

    public async Task<SalesOrderDetailDto> ConvertQuoteToSalesOrderAsync(Guid quoteId)
    {
        try
        {
            var quote = await _quoteRepo.GetByIdAsync(quoteId, q => q.LineItems)
                ?? throw new InvalidOperationException($"Quote {quoteId} not found");

            if (quote.QuoteStatus != "Accepted")
                throw new InvalidOperationException("Only accepted quotes can be converted to Sales Orders");

            var existingOrder = (await _salesOrderRepo.FindAsync(o =>
                    o.TenantId == quote.TenantId
                    && o.QuoteId == quoteId))
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();
            if (existingOrder != null)
            {
                return await GetSalesOrderByIdAsync(existingOrder.Id)
                    ?? throw new InvalidOperationException($"Sales Order {existingOrder.Id} could not be loaded");
            }

            var createDto = new CreateSalesOrderDto
            {
                BusinessPartnerId = quote.CustomerId ?? throw new InvalidOperationException("Quote has no customer"),
                QuoteId = quoteId,
                OpportunityId = quote.OpportunityId,
                Currency = quote.Currency,
                DiscountAmount = quote.DiscountAmount,
                ShippingAmount = quote.ShippingAmount,
                TaxAmount = quote.LineItems.Sum(li => li.TaxAmount),
                ReferenceNumber = quote.DocumentNumber,
                Terms = quote.Proposal,
                ExternalNotes = $"Converted from CRM Quote {quote.DocumentNumber}",
                Lines = quote.LineItems.Select(li => new CreateSalesOrderLineDto
                {
                    ProductCode = li.ProductCode,
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice,
                    DiscountPercentage = li.DiscountPercentage,
                    DiscountAmount = li.DiscountAmount,
                    TaxCode = li.TaxCode,
                    Unit = li.Unit
                }).ToList()
            };

            var result = await CreateSalesOrderAsync(createDto);

            _logger.LogInformation("Converted Quote {QuoteId} to Sales Order {OrderNumber}", quoteId, result.DocumentNumber);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting Quote {QuoteId} to Sales Order", quoteId);
            throw;
        }
    }

    public async Task<Guid> GenerateInvoiceAsync(Guid salesOrderId)
    {
        // TODO: Integrate with Finance module's Invoice creation service
        // This will create an Invoice from the Sales Order lines and link it back
        _logger.LogWarning("GenerateInvoiceAsync not yet integrated with Finance module for SO {SalesOrderId}", salesOrderId);
        throw new NotImplementedException("Invoice generation will be implemented during Finance module integration");
    }

    #endregion

    #region Utilities

    public async Task<string> GenerateOrderNumberAsync()
    {
        return await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Sales,
            SalesDocumentTypes.SalesOrder,
            _currentUserProvider.TenantId,
            DateTime.UtcNow,
            nameof(SalesOrder));
    }

    public async Task<bool> ValidateCreditLimitAsync(Guid businessPartnerId, decimal orderAmount)
    {
        try
        {
            var bp = await _bpRepo.GetByIdAsync(businessPartnerId);
            if (bp == null) return false;

            // If no credit limit is set, allow the order
            if (!bp.CreditLimit.HasValue || bp.CreditLimit.Value == 0)
                return true;

            // Check if customer is on credit hold
            if (bp.IsOnCreditHold)
                return false;

            var outstandingBalance = await GetCustomerOutstandingBalanceAsync(businessPartnerId);
            var availableCredit = bp.CreditLimit.Value - outstandingBalance;

            return orderAmount <= availableCredit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credit limit for BP {BusinessPartnerId}", businessPartnerId);
            return false;
        }
    }

    public async Task<decimal> GetCustomerOutstandingBalanceAsync(Guid businessPartnerId)
    {
        try
        {
            var bp = await _bpRepo.GetByIdAsync(businessPartnerId);
            return bp?.OutstandingBalance ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting outstanding balance for BP {BusinessPartnerId}", businessPartnerId);
            return 0;
        }
    }

    #endregion

    #region Private Helpers

    private async Task SyncLinkedProjectUnitsForSalesOrderAsync(SalesOrder salesOrder)
    {
        var projectUnitRepository = _unitOfWork.Repository<ProjectUnit>();
        if (projectUnitRepository == null)
        {
            return;
        }

        var linkedUnits = (await projectUnitRepository.FindAsync(unit =>
                unit.TenantId == salesOrder.TenantId
                && unit.SalesOrderId == salesOrder.Id
                && !unit.IsDeleted))
            ?.ToList() ?? [];

        if (linkedUnits.Count == 0)
        {
            return;
        }

        var agreementIds = linkedUnits
            .Where(unit => unit.SalesAgreementId.HasValue)
            .Select(unit => unit.SalesAgreementId!.Value)
            .Distinct()
            .ToList();
        var salesAgreementRepository = _unitOfWork.Repository<SalesAgreement>();

        var linkedAgreementLookup = agreementIds.Count == 0
            || salesAgreementRepository == null
            ? new Dictionary<Guid, SalesAgreement>()
            : ((await salesAgreementRepository.FindAsync(agreement =>
                    agreement.TenantId == salesOrder.TenantId
                    && agreementIds.Contains(agreement.Id)))
                ?? Enumerable.Empty<SalesAgreement>())
                .ToDictionary(agreement => agreement.Id);

        foreach (var unit in linkedUnits)
        {
            linkedAgreementLookup.TryGetValue(unit.SalesAgreementId ?? Guid.Empty, out var linkedAgreement);
            var nextStatus = ProjectUnitSalesSyncRules.ResolveStatusFromOrder(
                unit.Status,
                unit.IsReleasedForMarket,
                salesOrder.OrderType,
                salesOrder.OrderStatus,
                linkedAgreement?.AgreementType,
                linkedAgreement?.AgreementStatus);

            if (!string.Equals(unit.Status, nextStatus, StringComparison.OrdinalIgnoreCase))
            {
                unit.Status = nextStatus;
                await projectUnitRepository.UpdateAsync(unit);
            }
        }
    }

    private async Task RecordStatusChangeAsync(Guid salesOrderId, SalesOrderStatus? from, SalesOrderStatus to, string? notes, Guid tenantId)
    {
        var history = new SalesOrderStatusHistory
        {
            SalesOrderId = salesOrderId,
            FromStatus = from,
            ToStatus = to,
            ChangedById = _currentUserProvider.UserId,
            ChangedAt = DateTime.UtcNow,
            Notes = notes,
            TenantId = tenantId
        };
        await _historyRepo.AddAsync(history);
    }

    private async Task<SalesLinkedProjectUnitContextDto?> GetProjectUnitContextAsync(Guid salesOrderId, Guid tenantId)
    {
        var projectUnitRepository = _unitOfWork.Repository<ProjectUnit>();
        if (projectUnitRepository == null)
        {
            return null;
        }

        var linkedUnit = await projectUnitRepository.GetQueryable()
            .Include(unit => unit.Project)
            .Include(unit => unit.SalesAgreement)
            .Include(unit => unit.SalesOrder)
            .FirstOrDefaultAsync(unit =>
                unit.TenantId == tenantId
                && unit.SalesOrderId == salesOrderId
                && !unit.IsDeleted);

        return linkedUnit == null
            ? null
            : ProjectUnitPresentationRules.BuildSalesLinkedProjectUnitContext(linkedUnit);
    }

    private async Task<Dictionary<Guid, SalesLinkedProjectUnitContextDto>> GetProjectUnitContextsBySalesOrderIdsAsync(
        IEnumerable<Guid> salesOrderIds,
        Guid? tenantId)
    {
        var distinctIds = salesOrderIds.Distinct().ToList();
        if (distinctIds.Count == 0 || !tenantId.HasValue)
        {
            return new Dictionary<Guid, SalesLinkedProjectUnitContextDto>();
        }

        var projectUnitRepository = _unitOfWork.Repository<ProjectUnit>();
        if (projectUnitRepository == null)
        {
            return new Dictionary<Guid, SalesLinkedProjectUnitContextDto>();
        }

        var linkedUnits = await projectUnitRepository.GetQueryable()
            .Include(unit => unit.Project)
            .Include(unit => unit.SalesAgreement)
            .Include(unit => unit.SalesOrder)
            .Where(unit =>
                unit.TenantId == tenantId.Value
                && unit.SalesOrderId.HasValue
                && distinctIds.Contains(unit.SalesOrderId.Value)
                && !unit.IsDeleted)
            .ToListAsync();

        return linkedUnits
            .GroupBy(unit => unit.SalesOrderId!.Value)
            .ToDictionary(
                group => group.Key,
                group => ProjectUnitPresentationRules.BuildSalesLinkedProjectUnitContext(group.First()));
    }

    private SalesOrderSummaryDto MapToSummaryDto(SalesOrder so, SalesLinkedProjectUnitContextDto? projectUnitContext = null) => new()
    {
        Id = so.Id,
        DocumentNumber = so.DocumentNumber,
        DocumentDate = so.DocumentDate,
        OrderType = so.OrderType,
        OrderStatus = so.OrderStatus,
        BusinessPartnerId = so.BusinessPartnerId,
        CustomerName = so.CustomerName,
        RequestedDeliveryDate = so.RequestedDeliveryDate,
        TotalAmount = so.TotalAmount,
        TaxAmount = so.TaxAmount,
        Currency = so.Currency,
        SalesRepName = so.SalesRep?.UserName,
        OrderPriority = so.OrderPriority,
        ApprovalStatus = so.ApprovalStatus,
        LineCount = so.Lines?.Count ?? 0,
        PropertyReference = so.PropertyReference,
        PropertyType = so.PropertyType,
        ProjectUnitContext = projectUnitContext
    };

    private SalesOrderDetailDto MapToDetailDto(SalesOrder so, SalesLinkedProjectUnitContextDto? projectUnitContext = null) => new()
    {
        Id = so.Id,
        DocumentNumber = so.DocumentNumber,
        DocumentDate = so.DocumentDate,
        OrderType = so.OrderType,
        OrderStatus = so.OrderStatus,
        BusinessPartnerId = so.BusinessPartnerId,
        CustomerName = so.CustomerName,
        RequestedDeliveryDate = so.RequestedDeliveryDate,
        PromisedDeliveryDate = so.PromisedDeliveryDate,
        ActualDeliveryDate = so.ActualDeliveryDate,
        TotalAmount = so.TotalAmount,
        TaxAmount = so.TaxAmount,
        Currency = so.Currency,
        ExchangeRate = so.ExchangeRate,
        SubTotal = so.SubTotal,
        DiscountAmount = so.DiscountAmount,
        DiscountPercentage = so.DiscountPercentage,
        ShippingAmount = so.ShippingAmount,
        ShipmentMethod = so.ShipmentMethod,
        ShippingAddress = so.ShippingAddress,
        BillingAddress = so.BillingAddress,
        DeliveryInstructions = so.DeliveryInstructions,
        PaymentTermId = so.PaymentTermId,
        PaymentTermsDays = so.PaymentTermsDays,
        WarehouseId = so.WarehouseId,
        QuoteId = so.QuoteId,
        OpportunityId = so.OpportunityId,
        InvoiceId = so.InvoiceId,
        SalesRepId = so.SalesRepId,
        SalesRepName = so.SalesRep?.UserName,
        OrderPriority = so.OrderPriority,
        ApprovalStatus = so.ApprovalStatus,
        Terms = so.Terms,
        InternalNotes = so.InternalNotes,
        ExternalNotes = so.ExternalNotes,
        ReferenceNumber = so.ReferenceNumber,
        PropertyReference = so.PropertyReference,
        PropertyType = so.PropertyType,
        SubmittedById = so.SubmittedById,
        SubmittedDate = so.SubmittedDate,
        ApprovedById = so.ApprovedById,
        ApprovedDate = so.ApprovedDate,
        ApprovalComments = so.ApprovalComments,
        LineCount = so.Lines?.Count ?? 0,

        // Customer details from BP
        CustomerPhone = so.BusinessPartner?.PrimaryPhone,
        CustomerEmail = so.BusinessPartner?.PrimaryEmail,
        CustomerAddress = so.BusinessPartner?.PhysicalAddress,

        Lines = so.Lines?.Select(l => new SalesOrderLineDto
        {
            Id = l.Id,
            SalesOrderId = l.SalesOrderId,
            LineNumber = l.LineNumber,
            ProductId = l.ProductId,
            InventoryItemId = l.InventoryItemId,
            Description = l.Description,
            ProductCode = l.ProductCode,
            Quantity = l.Quantity,
            DeliveredQuantity = l.DeliveredQuantity,
            InvoicedQuantity = l.InvoicedQuantity,
            RemainingQuantity = l.RemainingQuantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
            DiscountPercentage = l.DiscountPercentage,
            DiscountAmount = l.DiscountAmount,
            TaxRate = l.TaxRate,
            TaxAmount = l.TaxAmount,
            TaxCode = l.TaxCode,
            Unit = l.Unit,
            WarehouseId = l.WarehouseId,
            LocationId = l.LocationId,
            SerialNumber = l.SerialNumber,
            LotNumber = l.LotNumber,
            IsStockReserved = l.IsStockReserved,
            ReservedQuantity = l.ReservedQuantity,
            UnitCost = l.UnitCost,
            Notes = l.Notes
        }).ToList() ?? new(),

        StatusHistory = so.StatusHistory?.OrderByDescending(h => h.ChangedAt).Select(h => new SalesOrderStatusHistoryDto
        {
            Id = h.Id,
            FromStatus = h.FromStatus,
            ToStatus = h.ToStatus,
            ChangedByName = h.ChangedBy?.UserName,
            ChangedAt = h.ChangedAt,
            Notes = h.Notes,
            Reason = h.Reason
        }).ToList() ?? new(),

        DeliveryNotes = so.DeliveryNotes?.Select(d => new DeliveryNoteSummaryDto
        {
            Id = d.Id,
            DocumentNumber = d.DocumentNumber,
            DocumentDate = d.DocumentDate,
            DeliveryStatus = d.DeliveryStatus,
            SalesOrderId = d.SalesOrderId,
            CustomerName = d.CustomerName,
            ShipmentMethod = d.ShipmentMethod,
            ShippedDate = d.ShippedDate,
            DeliveredDate = d.DeliveredDate,
            CarrierName = d.CarrierName,
            TrackingNumber = d.TrackingNumber,
            LineCount = d.Lines?.Count ?? 0
        }).ToList() ?? new(),
        ProjectUnitContext = projectUnitContext
    };

    #endregion
}
