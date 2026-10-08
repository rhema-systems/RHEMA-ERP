using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Core.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services.Sales;

public class SalesOrderService : ISalesOrderService
{
    private const string WorkflowEntityType = "SalesOrder";

    private readonly IGenericRepository<SalesOrder> _salesOrderRepo;
    private readonly IGenericRepository<SalesOrderLine> _lineRepo;
    private readonly IGenericRepository<SalesOrderStatusHistory> _historyRepo;
    private readonly IGenericRepository<BusinessPartner> _bpRepo;
    private readonly IGenericRepository<Quote> _quoteRepo;
    private readonly IGenericRepository<PaymentTerm> _paymentTermRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ILogger<SalesOrderService> _logger;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly ISalesOrderInvoiceService? _invoiceGenerator;
    private readonly ITaxCalculationEngine? _taxCalculationEngine;
    private readonly ICommercialQuantityPolicyValidator? _commercialQuantityValidator;

    public SalesOrderService(
        IGenericRepository<SalesOrder> salesOrderRepo,
        IGenericRepository<SalesOrderLine> lineRepo,
        IGenericRepository<SalesOrderStatusHistory> historyRepo,
        IGenericRepository<BusinessPartner> bpRepo,
        IGenericRepository<Quote> quoteRepo,
        IGenericRepository<PaymentTerm> paymentTermRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IDocumentNumberingService documentNumberingService,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ILogger<SalesOrderService> logger,
        ISalesOrderInvoiceService? invoiceGenerator = null,
        ITaxCalculationEngine? taxCalculationEngine = null,
        ICommercialQuantityPolicyValidator? commercialQuantityValidator = null)
    {
        _salesOrderRepo = salesOrderRepo;
        _lineRepo = lineRepo;
        _historyRepo = historyRepo;
        _bpRepo = bpRepo;
        _quoteRepo = quoteRepo;
        _paymentTermRepo = paymentTermRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _logger = logger;
        // Document numbering is kept with workflow governance so merged Sales orders remain traceable and approval-controlled.
        _documentNumberingService = documentNumberingService;
        _invoiceGenerator = invoiceGenerator;
        _taxCalculationEngine = taxCalculationEngine;
        _commercialQuantityValidator = commercialQuantityValidator;
    }

    #region CRUD

    public async Task<SalesOrderDetailDto> CreateSalesOrderAsync(CreateSalesOrderDto dto)
    {
        try
        {
            var bp = await RequireApprovedCustomerAsync(dto.BusinessPartnerId);
            var paymentTerm = await ResolvePaymentTermAsync(dto.PaymentTermId ?? bp.PaymentTermId, bp.TenantId);
            SalesAllocation? reservedAllocation = null;
            if (dto.SalesAllocationId.HasValue)
            {
                reservedAllocation = await _unitOfWork.Repository<SalesAllocation>().FirstOrDefaultAsync(allocation =>
                        allocation.Id == dto.SalesAllocationId.Value
                        && allocation.TenantId == bp.TenantId
                        && !allocation.IsDeleted)
                    ?? throw new InvalidOperationException("The selected property reservation was not found.");

                if (!string.Equals(reservedAllocation.Status, "Reserved", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Only an active Reserved allocation can be used for a new Sales Order.");
                if (reservedAllocation.SalesOrderId.HasValue)
                    throw new InvalidOperationException("This reservation is already linked to another Sales Order.");
                if (reservedAllocation.OpportunityId.HasValue && reservedAllocation.OpportunityId != dto.OpportunityId)
                    throw new InvalidOperationException("The Sales Order must belong to the Opportunity that reserved this item.");
                if (reservedAllocation.BusinessPartnerId.HasValue && reservedAllocation.BusinessPartnerId != dto.BusinessPartnerId)
                    throw new InvalidOperationException("The Sales Order customer must match the customer on this reservation.");
            }

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
                PaymentTermId = paymentTerm?.Id,
                PaymentTermsDays = paymentTerm?.DueDays ?? 0,
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
                TaxGroupId = dto.TaxGroupId,
                TenantId = bp.TenantId
            };

            // Calculate totals from line items
            decimal subTotal = 0;
            decimal totalTax = 0;
            int lineNumber = 1;

            await _salesOrderRepo.AddAsync(salesOrder);

            foreach (var lineDto in dto.Lines)
            {
                var tax = await CalculateLineTaxAsync(lineDto, dto.TaxGroupId, dto.BusinessPartnerId);
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
                    DiscountAmount = tax.DiscountAmount,
                    TaxRate = tax.EffectiveRate,
                    TaxAmount = tax.TaxAmount,
                    TaxCode = tax.TaxCode,
                    TaxGroupId = tax.TaxGroupId,
                    Unit = lineDto.Unit,
                    UnitOfMeasureId = lineDto.UnitOfMeasureId,
                    UnitOfMeasureCodeSnapshot = lineDto.UnitOfMeasureCodeSnapshot,
                    UnitOfMeasureDecimalPlacesSnapshot = lineDto.UnitOfMeasureDecimalPlacesSnapshot,
                    UnitOfMeasureRoundingIncrementSnapshot = lineDto.UnitOfMeasureRoundingIncrementSnapshot,
                    WarehouseId = lineDto.WarehouseId ?? dto.WarehouseId,
                    LocationId = lineDto.LocationId,
                    GLAccountId = lineDto.GLAccountId,
                    SerialNumber = lineDto.SerialNumber,
                    LotNumber = lineDto.LotNumber,
                    ExpirationDate = lineDto.ExpirationDate,
                    Notes = lineDto.Notes,
                    TenantId = bp.TenantId
                };

                await ValidateLineQuantityAsync(line, "Sales order create");

                subTotal += line.LineTotal - line.DiscountAmount;
                totalTax += line.TaxAmount;

                await _lineRepo.AddAsync(line);
            }

            salesOrder.SubTotal = subTotal;
            salesOrder.TaxAmount = dto.TaxGroupId.HasValue || dto.Lines.Any(line => line.TaxGroupId.HasValue)
                ? totalTax
                : dto.TaxAmount ?? totalTax;
            salesOrder.TotalAmount = subTotal + salesOrder.TaxAmount + salesOrder.ShippingAmount - salesOrder.DiscountAmount;

            if (reservedAllocation is not null)
            {
                reservedAllocation.SalesOrderId = salesOrder.Id;
                reservedAllocation.AgreedValue = salesOrder.TotalAmount;
                reservedAllocation.UpdatedAt = DateTime.UtcNow;
                reservedAllocation.UpdatedBy = _currentUserProvider.Username;
                reservedAllocation.LastModifiedById = _currentUserProvider.UserId;
                await _unitOfWork.Repository<SalesAllocation>().UpdateAsync(reservedAllocation);
                await _unitOfWork.Repository<SalesAllocationHistory>().AddAsync(new SalesAllocationHistory
                {
                    TenantId = reservedAllocation.TenantId,
                    SalesAllocationId = reservedAllocation.Id,
                    Action = "SalesOrderLinked",
                    FromStatus = reservedAllocation.Status,
                    ToStatus = reservedAllocation.Status,
                    PerformedById = _currentUserProvider.UserId == Guid.Empty ? null : _currentUserProvider.UserId,
                    PerformedByName = _currentUserProvider.FullName,
                    PerformedAt = DateTime.UtcNow,
                    Notes = $"Linked reservation to Sales Order {salesOrder.DocumentNumber}.",
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                });
            }

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
            if (dto.PaymentTermId.HasValue)
            {
                var paymentTerm = await ResolvePaymentTermAsync(dto.PaymentTermId, so.TenantId, useDefaultWhenMissing: false);
                so.PaymentTermId = paymentTerm!.Id;
                so.PaymentTermsDays = paymentTerm.DueDays;
            }
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
            if (dto.TaxGroupId.HasValue) so.TaxGroupId = dto.TaxGroupId;

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
                    var tax = await CalculateLineTaxAsync(lineDto, dto.TaxGroupId ?? so.TaxGroupId, so.BusinessPartnerId);
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
                        DiscountAmount = tax.DiscountAmount,
                        TaxRate = tax.EffectiveRate,
                        TaxAmount = tax.TaxAmount,
                        TaxCode = tax.TaxCode,
                        TaxGroupId = tax.TaxGroupId,
                        Unit = lineDto.Unit,
                        UnitOfMeasureId = lineDto.UnitOfMeasureId,
                        UnitOfMeasureCodeSnapshot = lineDto.UnitOfMeasureCodeSnapshot,
                        UnitOfMeasureDecimalPlacesSnapshot = lineDto.UnitOfMeasureDecimalPlacesSnapshot,
                        UnitOfMeasureRoundingIncrementSnapshot = lineDto.UnitOfMeasureRoundingIncrementSnapshot,
                        WarehouseId = lineDto.WarehouseId ?? so.WarehouseId,
                        LocationId = lineDto.LocationId,
                        GLAccountId = lineDto.GLAccountId,
                        Notes = lineDto.Notes,
                        TenantId = so.TenantId
                    };

                    await ValidateLineQuantityAsync(line, "Sales order update");

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

    private async Task<PaymentTerm?> ResolvePaymentTermAsync(
        Guid? paymentTermId,
        Guid tenantId,
        bool useDefaultWhenMissing = true)
    {
        PaymentTerm? term;
        if (paymentTermId.HasValue)
        {
            term = await _paymentTermRepo.GetByIdAsync(paymentTermId.Value);
            if (term == null || term.TenantId != tenantId || term.IsDeleted)
            {
                throw new InvalidOperationException("The selected payment term was not found for this tenant.");
            }
        }
        else if (useDefaultWhenMissing)
        {
            term = (await _paymentTermRepo.FindAsync(candidate =>
                    candidate.TenantId == tenantId &&
                    !candidate.IsDeleted &&
                    candidate.IsActive &&
                    candidate.IsDefault &&
                    (candidate.ApplicableTo == "All" || candidate.ApplicableTo == "Customer" || candidate.ApplicableTo == "Client")))
                .OrderBy(candidate => candidate.ApplicableTo == "Customer" ? 0 : 1)
                .ThenBy(candidate => candidate.DisplayOrder)
                .FirstOrDefault();
        }
        else
        {
            term = null;
        }

        if (term != null && (!term.IsActive ||
            !(term.ApplicableTo.Equals("All", StringComparison.OrdinalIgnoreCase) ||
              term.ApplicableTo.Equals("Customer", StringComparison.OrdinalIgnoreCase) ||
              term.ApplicableTo.Equals("Client", StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException($"Payment term '{term.Code}' is not active and applicable to customers.");
        }

        return term;
    }

    #region Lifecycle Actions

    public async Task<SalesOrderDetailDto> SubmitForApprovalAsync(Guid id)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(id, s => s.Lines)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");
            EnsureLifecycleTenant(so);

            if (so.OrderStatus != SalesOrderStatus.Draft)
                throw new InvalidOperationException($"Cannot submit Sales Order in {so.OrderStatus} status");

            await ValidateOrderQuantitiesAsync(so, "Sales order submit");

            var userId = _currentUserProvider.UserId;
            if (userId == Guid.Empty)
                throw new UnauthorizedAccessException("User is not authenticated");

            // Both direct completion and an approval-backed submission retain the customer credit gate.
            if (!await ValidateCreditLimitAsync(so.BusinessPartnerId, so.TotalAmount))
                throw new InvalidOperationException("Order exceeds customer's available credit limit or the customer is on credit hold");

            var previousStatus = so.OrderStatus;
            var workflowResult = await _workflowIntegrationService.SubmitAsync(WorkflowEntityType, id);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start workflow");

            var adapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
            adapter.ApplySubmitOutcome(so, workflowResult, userId);

            await _salesOrderRepo.UpdateAsync(so);
            if (previousStatus != so.OrderStatus)
            {
                await RecordStatusChangeAsync(so.Id, previousStatus, so.OrderStatus,
                    workflowResult.ApprovalRequired ? "Submitted for approval" : "Confirmed — approval not required", so.TenantId);
            }

            await SyncLinkedProjectUnitsForSalesOrderAsync(so);
            await _unitOfWork.SaveChangesAsync();

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
            var so = await _salesOrderRepo.GetByIdAsync(id, s => s.Lines)
                ?? throw new InvalidOperationException($"Sales Order {id} not found");
            EnsureLifecycleTenant(so);

            if (so.OrderStatus != SalesOrderStatus.PendingApproval)
                throw new InvalidOperationException($"Sales Order is not pending approval");

            var userId = _currentUserProvider.UserId;
            if (userId == Guid.Empty)
                throw new UnauthorizedAccessException("User is not authenticated");

            // Keep the workflow assignment guard before applying Sales/Finance state changes.
            var canApprove = await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, id, userId);
            if (!canApprove)
                throw new UnauthorizedAccessException("You are not assigned to approve the current workflow step");

            if (dto.Approved && !await ValidateCreditLimitAsync(so.BusinessPartnerId, so.TotalAmount))
                throw new InvalidOperationException("Order exceeds customer's available credit limit or the customer is on credit hold");

            if (dto.Approved)
                await ValidateOrderQuantitiesAsync(so, "Sales order confirm");

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
            EnsureLifecycleTenant(so);

            // Keep the legacy route, but never let it bypass a configured or in-flight approval.
            // Central submission confirms directly only when approval is genuinely not required.
            if (so.OrderStatus == SalesOrderStatus.Draft)
                return await SubmitForApprovalAsync(id);

            throw new InvalidOperationException($"Cannot confirm Sales Order in {so.OrderStatus} status. Complete the existing approval process first.");
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

            if (quote.TenantId != _currentUserProvider.TenantId)
                throw new UnauthorizedAccessException("The Quote does not belong to the current tenant.");

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

            Guid? salesAllocationId = null;
            string? propertyReference = null;
            if (quote.PropertyEnquiryTicketId.HasValue)
            {
                var prospect = await _unitOfWork.Repository<EhcPropertyEnquiryProspect>()
                    .GetQueryable(item => item.TenantId == quote.TenantId
                        && item.TicketId == quote.PropertyEnquiryTicketId.Value
                        && !item.IsDeleted)
                    .Include(item => item.Ticket)
                    .AsNoTracking()
                    .SingleOrDefaultAsync()
                    ?? throw new InvalidOperationException("The property enquiry linked to this Quote could not be resolved.");
                salesAllocationId = prospect.SalesAllocationId;
                try
                {
                    propertyReference = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(
                        prospect.Ticket.PropertyListingContextJson ?? string.Empty)?.ListingReference;
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException("The property enquiry context is invalid and must be corrected before conversion.");
                }
            }

            var createDto = new CreateSalesOrderDto
            {
                BusinessPartnerId = quote.CustomerId ?? throw new InvalidOperationException("Quote has no customer"),
                QuoteId = quoteId,
                OpportunityId = quote.OpportunityId,
                SalesAllocationId = salesAllocationId,
                PropertyReference = propertyReference,
                Currency = quote.Currency,
                DiscountAmount = quote.DiscountAmount,
                ShippingAmount = quote.ShippingAmount,
                TaxAmount = quote.LineItems.Sum(li => li.TaxAmount),
                ReferenceNumber = quote.DocumentNumber,
                Terms = quote.Proposal,
                ExternalNotes = $"Converted from CRM Quote {quote.DocumentNumber}",
                Lines = quote.LineItems.Select(ToSalesOrderLineRequest).ToList()
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

    private async Task<BusinessPartner> RequireApprovedCustomerAsync(Guid businessPartnerId)
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A tenant context is required to create a Sales Order.");

        var partner = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item =>
                item.Id == businessPartnerId &&
                item.TenantId == tenantId &&
                !item.IsDeleted)
            .Include(item => item.Roles)
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("The selected Customer Business Partner was not found in the current tenant.");

        var hasActiveCustomerRole = partner.Roles.Any(role =>
            !role.IsDeleted &&
            role.RoleType == BusinessPartnerRoleType.Customer &&
            role.Status == BusinessPartnerRoleStatus.Active &&
            role.ActiveFromUtc <= DateTime.UtcNow &&
            (!role.InactiveFromUtc.HasValue || role.InactiveFromUtc.Value > DateTime.UtcNow));

        if (!partner.IsActive ||
            !string.Equals(partner.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
            !hasActiveCustomerRole)
        {
            throw new InvalidOperationException(
                "Sales Orders require an active, approved Business Partner with an active Customer role.");
        }

        return partner;
    }

    public async Task<Guid> GenerateInvoiceAsync(Guid salesOrderId, GenerateSalesOrderInvoiceRequest request)
    {
        var generator = _invoiceGenerator ?? throw new InvalidOperationException("The Sales invoice adapter is not configured.");
        return (await generator.GenerateAsync(salesOrderId, request)).Invoice.Id;
    }

    #endregion

    #region Utilities

    private static CreateSalesOrderLineDto ToSalesOrderLineRequest(QuoteLineItem line)
    {
        var request = new CreateSalesOrderLineDto
        {
            ProductCode = line.ProductCode,
            Description = line.Description,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            DiscountPercentage = line.DiscountPercentage,
            DiscountAmount = line.DiscountAmount,
            TaxCode = line.TaxCode,
            Unit = line.Unit
        };
        SalesCommercialQuantityEvidence.CopyToSalesOrderRequest(line, request);
        return request;
    }

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
            if (bp == null || bp.IsDeleted || bp.TenantId != _currentUserProvider.TenantId) return false;

            // A hold still applies when no numeric credit limit has been configured.
            if (bp.IsOnCreditHold)
                return false;

            var role = (await _unitOfWork.Repository<BusinessPartnerRole>().FindAsync(item =>
                    item.TenantId == _currentUserProvider.TenantId &&
                    item.BusinessPartnerId == businessPartnerId &&
                    item.RoleType == BusinessPartnerRoleType.Customer))
                .SingleOrDefault();
            if (role is null)
                return false;
            var profiles = await _unitOfWork.Repository<BusinessPartnerArProfileVersion>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId &&
                item.BusinessPartnerRoleId == role.Id);
            var readiness = BusinessPartnerFinanceProfilePolicy.ResolveAr(
                bp, role, profiles, DateTime.UtcNow.Date);
            if (!readiness.IsReady)
                return false;

            // The governed AR profile is the only credit-limit authority. The similarly named
            // BusinessPartner column is retained for historical compatibility and is ignored.
            var creditLimit = readiness.ArProfile!.CreditLimit;
            if (!creditLimit.HasValue || creditLimit.Value == 0)
                return true;

            var outstandingBalance = await GetCustomerOutstandingBalanceAsync(businessPartnerId);
            var availableCredit = creditLimit.Value - outstandingBalance;

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

    private void EnsureLifecycleTenant(SalesOrder order)
    {
        if (_currentUserProvider.TenantId == Guid.Empty || order.IsDeleted || order.TenantId != _currentUserProvider.TenantId)
            throw new KeyNotFoundException("The selected sales order was not found in the current tenant.");
    }

    private async Task ValidateOrderQuantitiesAsync(SalesOrder order, string boundary)
    {
        foreach (var line in order.Lines.Where(line => !line.IsDeleted))
            await ValidateLineQuantityAsync(line, boundary);
    }

    private Task ValidateLineQuantityAsync(SalesOrderLine line, string boundary)
    {
        var validator = _commercialQuantityValidator
            ?? throw new InvalidOperationException("Commercial quantity policy validation is not configured for Sales.");
        return SalesCommercialQuantityEvidence.ValidateAndFreezeAsync(
            validator, line, line.Unit, line.Quantity, $"{boundary} line {line.LineNumber}");
    }

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

    private async Task<SalesOrderLineTax> CalculateLineTaxAsync(
        CreateSalesOrderLineDto line,
        Guid? documentTaxGroupId,
        Guid businessPartnerId)
    {
        var lineAmount = line.Quantity * line.UnitPrice;
        var discountAmount = line.DiscountAmount
            ?? Math.Round(
                lineAmount * ((line.DiscountPercentage ?? 0m) / 100m),
                2,
                MidpointRounding.AwayFromZero);
        var taxableAmount = Math.Max(0m, lineAmount - discountAmount);
        // The document selection is authoritative. Line-level groups are retained only for
        // backwards-compatible API callers that do not provide a document tax group.
        var taxGroupId = documentTaxGroupId ?? line.TaxGroupId;

        if (!taxGroupId.HasValue)
        {
            var rate = line.TaxRate ?? 0m;
            return new SalesOrderLineTax(
                discountAmount,
                Math.Round(taxableAmount * rate / 100m, 2, MidpointRounding.AwayFromZero),
                rate,
                line.TaxCode,
                null);
        }

        if (_taxCalculationEngine == null)
        {
            throw new InvalidOperationException(
                "The Finance tax calculation service is required when a sales-order tax group is selected.");
        }

        var result = await _taxCalculationEngine.CalculateTaxesAsync(new TaxCalculationRequestDto
        {
            BaseAmount = taxableAmount,
            TaxGroupId = taxGroupId,
            TransactionDate = DateTime.UtcNow,
            TransactionType = TaxTransactionType.SaleOfGoods,
            BusinessPartnerId = businessPartnerId,
            BusinessPartnerRole = BusinessPartnerRoleType.Customer
        });
        var taxCode = string.Join("+", result.TaxBreakdowns.Select(item => item.TaxCode));

        return new SalesOrderLineTax(
            discountAmount,
            result.TotalTaxAmount,
            result.EffectiveTaxRate,
            taxCode.Length <= 50 ? taxCode : taxCode[..50],
            result.TaxGroupId ?? taxGroupId);
    }

    private sealed record SalesOrderLineTax(
        decimal DiscountAmount,
        decimal TaxAmount,
        decimal EffectiveRate,
        string? TaxCode,
        Guid? TaxGroupId);

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
        RowVersion = Convert.ToBase64String(so.RowVersion),
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
        TaxGroupId = so.TaxGroupId,
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
            TaxGroupId = l.TaxGroupId,
            Unit = l.Unit,
            UnitOfMeasureId = l.UnitOfMeasureId,
            UnitOfMeasureCodeSnapshot = l.UnitOfMeasureCodeSnapshot,
            UnitOfMeasureDecimalPlacesSnapshot = l.UnitOfMeasureDecimalPlacesSnapshot,
            UnitOfMeasureRoundingIncrementSnapshot = l.UnitOfMeasureRoundingIncrementSnapshot,
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
