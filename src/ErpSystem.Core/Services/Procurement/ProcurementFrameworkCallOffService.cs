using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementFrameworkCallOffService :
    IProcurementFrameworkCallOffService
{
    private const string SourceTypeName = "ProcurementFrameworkCallOff";
    private const string EventType = "ProcurementFrameworkCallOffControl";
    private const string PurchaseOrderWorkflowEntity = "PurchaseOrder";
    private const string PurchaseOrderWorkflowName = "TDC Purchase Order Approval";
    private const string ManagePermission = "procurement.purchase-order.create";
    private const string ApprovePermission = "procurement.purchase-order.approve";
    private const int ExpiryAlertDays = 30;
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(number => $"DEC-{number:000}").ToArray();
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly IDocumentNumberingService _documentNumbering;
    private readonly IProcurementPurchaseOrderSourceService _purchaseOrderSources;
    private readonly IProcurementPurchaseOrderComplianceService _purchaseOrderCompliance;
    private readonly IProcurementPurchaseOrderSodService _purchaseOrderSod;
    private readonly IProcurementBudgetCommitmentLifecycleService _budgetCommitments;
    private readonly ILogger<ProcurementFrameworkCallOffService> _logger;

    public ProcurementFrameworkCallOffService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        ISupplierValidationService supplierValidation,
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notificationTopics,
        IDocumentNumberingService documentNumbering,
        IProcurementPurchaseOrderSourceService purchaseOrderSources,
        IProcurementPurchaseOrderComplianceService purchaseOrderCompliance,
        IProcurementPurchaseOrderSodService purchaseOrderSod,
        IProcurementBudgetCommitmentLifecycleService budgetCommitments,
        ILogger<ProcurementFrameworkCallOffService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _supplierValidation = supplierValidation;
        _workflow = workflow;
        _workflowAdapters = workflowAdapters;
        _controlEvents = controlEvents;
        _notificationTopics = notificationTopics;
        _documentNumbering = documentNumbering;
        _purchaseOrderSources = purchaseOrderSources;
        _purchaseOrderCompliance = purchaseOrderCompliance;
        _purchaseOrderSod = purchaseOrderSod;
        _budgetCommitments = budgetCommitments;
        _logger = logger;
    }

    private IGenericRepository<ProcurementFrameworkCallOff> CallOffs =>
        _unitOfWork.Repository<ProcurementFrameworkCallOff>();
    private IGenericRepository<ProcurementFrameworkCallOffLine> CallOffLines =>
        _unitOfWork.Repository<ProcurementFrameworkCallOffLine>();
    private IGenericRepository<ProcurementFrameworkAgreementBalance> Balances =>
        _unitOfWork.Repository<ProcurementFrameworkAgreementBalance>();
    private IGenericRepository<ProcurementFrameworkBalanceMovement> BalanceMovements =>
        _unitOfWork.Repository<ProcurementFrameworkBalanceMovement>();
    private IGenericRepository<PurchaseOrder> PurchaseOrders =>
        _unitOfWork.Repository<PurchaseOrder>();
    private IGenericRepository<PurchaseOrderItem> PurchaseOrderItems =>
        _unitOfWork.Repository<PurchaseOrderItem>();
    private IGenericRepository<PurchaseRequisition> Requisitions =>
        _unitOfWork.Repository<PurchaseRequisition>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions =>
        _unitOfWork.Repository<WorkflowDefinition>();

    public async Task<ProcurementFrameworkCallOffSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var rows = await CallOffs.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        var agreements = await _unitOfWork
            .Repository<ProcurementFrameworkAgreement>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Extensions.Where(child => !child.IsDeleted))
            .AsNoTracking().ToListAsync(cancellationToken);
        var movements = await BalanceMovements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        var familySummaries =
            ProcurementFrameworkCallOffCommercialRules.SummarizeFamilies(
                agreements.Select(ToAgreementRevisionState),
                movements.Select(item =>
                    new ProcurementFrameworkCallOffCommercialRules.MovementState(
                        item.AgreementId,
                        item.MovementType,
                        item.Amount)),
                now);
        var committed = familySummaries.GroupBy(item => item.CurrencyCode)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.CommittedAmount));
        var issued = familySummaries.GroupBy(item => item.CurrencyCode)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.IssuedAmount));
        var available = familySummaries.GroupBy(item => item.CurrencyCode)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.AvailableAmount));
        return new ProcurementFrameworkCallOffSummaryDto
        {
            TotalCount = rows.Count,
            DraftCount = rows.Count(item => item.Status == ProcurementFrameworkCallOffStatus.Draft),
            PendingApprovalCount = rows.Count(item =>
                item.Status == ProcurementFrameworkCallOffStatus.PendingApproval),
            ApprovedCount = rows.Count(item =>
                item.Status == ProcurementFrameworkCallOffStatus.Approved),
            IssuedCount = rows.Count(item =>
                item.Status == ProcurementFrameworkCallOffStatus.Issued),
            ExpiringAgreementCount = familySummaries.Count(item =>
                item.AvailableAmount > 0 &&
                item.IsEffective &&
                item.EffectiveEndUtc.HasValue &&
                item.EffectiveEndUtc.Value <=
                now.AddDays(ExpiryAlertDays)),
            TotalCommittedAmount = SingleCurrencyTotal(committed),
            TotalIssuedAmount = SingleCurrencyTotal(issued),
            TotalAvailableAmount = SingleCurrencyTotal(available),
            CommittedByCurrency = committed,
            IssuedByCurrency = issued,
            AvailableByCurrency = available
        };
    }

    public async Task<ProcurementFrameworkCallOffPageDto> SearchAsync(
        ProcurementFrameworkCallOffSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = CallOffQuery().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.CallOffNumber.Contains(search) ||
                item.AgreementNumber.Contains(search) ||
                item.PurchaseOrder.OrderNumber.Contains(search) ||
                item.SourceRequisition.RequisitionNumber.Contains(search) ||
                item.BusinessPartner.PartnerCode.Contains(search) ||
                item.BusinessPartner.PartnerName.Contains(search));
        }
        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);
        if (request.AgreementId.HasValue)
            query = query.Where(item => item.AgreementId == request.AgreementId.Value);
        if (request.BusinessPartnerId.HasValue)
            query = query.Where(item => item.BusinessPartnerId == request.BusinessPartnerId.Value);
        if (request.SourceRequisitionId.HasValue)
            query = query.Where(item =>
                item.SourceRequisitionId == request.SourceRequisitionId.Value);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.CallOffNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var familySummaries = await ResolveRevisionFamilySummariesAsync(
            rows.Select(item => item.Agreement),
            now,
            cancellationToken);
        return new ProcurementFrameworkCallOffPageDto
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = rows.Select(item => MapList(
                    item,
                    now,
                    familySummaries.GetValueOrDefault(item.AgreementId)))
                .ToList()
        };
    }

    public async Task<ProcurementFrameworkCallOffDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await MapAsync(
            await LoadAsync(id, false, cancellationToken),
            cancellationToken);
    }

    public async Task<ProcurementFrameworkCallOffOptionsDto> GetOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var agreements = await AgreementQuery(false)
            .Where(item => item.Status == ProcurementFrameworkAgreementStatus.Published &&
                           item.EffectiveFromUtc <= now)
            .OrderBy(item => item.AgreementNumber)
            .ToListAsync(cancellationToken);
        var currentAgreementIds =
            ProcurementFrameworkCallOffCommercialRules
                .SelectCurrentEffectiveRevisions(
                    agreements.Select(ToAgreementRevisionState),
                    now)
                .Select(item => item.AgreementId)
                .ToHashSet();
        agreements = agreements.Where(item =>
                currentAgreementIds.Contains(item.Id))
            .ToList();

        var agreementOptions = new List<ProcurementFrameworkCallOffAgreementOptionDto>();
        foreach (var agreement in agreements)
        {
            var authority = await ResolveAuthorityAsync(
                agreement, null, now, false, cancellationToken);
            var familyCapacity = await EvaluateAgreementFamilyCapacityAsync(
                agreement,
                0m,
                cancellationToken);
            var end = ProcurementFrameworkAgreementRules.EffectiveEnd(agreement);
            agreementOptions.Add(new ProcurementFrameworkCallOffAgreementOptionDto
            {
                AgreementId = agreement.Id,
                AgreementNumber = agreement.AgreementNumber,
                Title = agreement.Title,
                Version = agreement.Version,
                BusinessPartnerId = agreement.BusinessPartnerId,
                SupplierCode = agreement.BusinessPartner.PartnerCode,
                SupplierName = agreement.BusinessPartner.PartnerName,
                CurrencyCode = agreement.CurrencyCode,
                CeilingAmount = agreement.CeilingAmount,
                CommittedAmount = familyCapacity.CommittedAmount,
                AvailableAmount = familyCapacity.AvailableAmount,
                EffectiveFromUtc = agreement.EffectiveFromUtc,
                EffectiveEndUtc = end,
                DaysToExpiry = DaysToExpiry(end, now),
                CurrentActorIsAuthorized = authority is not null,
                CurrentActorMaximumCallOffAmount = authority?.MaximumCallOffAmount,
                PriceLines = agreement.PriceLines.Where(item => !item.IsDeleted)
                    .OrderBy(item => item.ItemCode)
                    .Select(item => new ProcurementFrameworkCallOffPriceOptionDto
                    {
                        AgreementPriceLineId = item.Id,
                        InventoryItemId = item.InventoryItemId,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        UnitOfMeasure = item.UnitOfMeasure,
                        UnitPrice = item.UnitPrice,
                        MinimumQuantity = item.MinimumQuantity,
                        MaximumQuantity = item.MaximumQuantity,
                        LeadTimeDays = item.LeadTimeDays
                    }).ToList()
            });
        }

        var requisitions = await Requisitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.Status == "Approved")
            .Include(item => item.Items.Where(line => !line.IsDeleted))
            .AsNoTracking().OrderByDescending(item => item.RequisitionDate)
            .ToListAsync(cancellationToken);
        var demandIds = requisitions.SelectMany(item => item.Items)
            .Select(item => item.Id).ToArray();
        var allocated = demandIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await CallOffLines.GetQueryable(line =>
                    line.TenantId == _currentUser.TenantId && !line.IsDeleted &&
                    demandIds.Contains(line.PurchaseRequisitionItemId) &&
                    !line.CallOff.IsDeleted &&
                    ActiveDemandStatuses.Contains(line.CallOff.Status))
                .GroupBy(line => line.PurchaseRequisitionItemId)
                .Select(group => new { Id = group.Key, Quantity = group.Sum(line => line.Quantity) })
                .ToDictionaryAsync(item => item.Id, item => item.Quantity, cancellationToken);
        var requisitionOptions = requisitions.Select(item =>
            {
                var lines = item.Items.Where(line =>
                        line.InventoryItemId.HasValue &&
                        line.Quantity - allocated.GetValueOrDefault(line.Id) > 0)
                    .Select(line => new ProcurementFrameworkCallOffDemandOptionDto
                    {
                        PurchaseRequisitionItemId = line.Id,
                        InventoryItemId = line.InventoryItemId!.Value,
                        ItemDescription = line.ItemDescription,
                        UnitOfMeasure = line.UnitOfMeasure,
                        DemandQuantity = line.Quantity,
                        AllocatedQuantity = allocated.GetValueOrDefault(line.Id),
                        RemainingQuantity = Math.Max(
                            0m, line.Quantity - allocated.GetValueOrDefault(line.Id))
                    }).ToList();
                return new ProcurementFrameworkCallOffRequisitionOptionDto
                {
                    RequisitionId = item.Id,
                    RequisitionNumber = item.RequisitionNumber,
                    Status = item.Status,
                    RequiredDate = item.RequiredDate,
                    DeliveryWarehouseId = item.DeliveryWarehouseId,
                    DeliveryAddress = item.DeliveryAddress,
                    Lines = lines
                };
            })
            .Where(item => item.Lines.Count > 0)
            .ToList();

        var warehouses = await _unitOfWork.Repository<Warehouse>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  !item.IsDeleted && item.IsActive)
            .AsNoTracking().OrderBy(item => item.Code)
            .Select(item => new ProcurementFrameworkCallOffWarehouseOptionDto
            {
                WarehouseId = item.Id,
                Code = item.Code,
                Name = item.Name
            }).ToListAsync(cancellationToken);
        var workflow = await ResolvePurchaseOrderWorkflowAsync(cancellationToken);
        return new ProcurementFrameworkCallOffOptionsDto
        {
            Agreements = agreementOptions,
            Requisitions = requisitionOptions,
            Warehouses = warehouses,
            PurchaseOrderWorkflowReady = workflow is not null,
            WorkflowReadinessMessage = workflow is null
                ? "Publish and activate the TDC Purchase Order Approval workflow before submitting a call-off."
                : null
        };
    }

    public async Task<ProcurementFrameworkCallOffDto> CreateAsync(
        CreateProcurementFrameworkCallOffRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            ManagePermission, request.AgreementId.ToString(), correlation, cancellationToken);
        ProcurementFrameworkCallOff? result = null;
        var created = false;
        await ExecuteAsync(async () =>
        {
            var replay = await CallOffs.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.CreationCorrelationId == correlation && !item.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (replay is not null)
            {
                result = replay;
                return;
            }

            ValidateCreateRequest(request);
            var now = DateTime.UtcNow;
            var agreement = await LoadAgreementAsync(
                request.AgreementId, true, cancellationToken);
            EnsureEffectiveAgreement(agreement, now, request.RequiredDateUtc);
            await EnsureCurrentAgreementRevisionAsync(
                agreement,
                now,
                cancellationToken);
            var requisition = await Requisitions.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == request.SourceRequisitionId && !item.IsDeleted)
                .Include(item => item.Items.Where(line => !line.IsDeleted))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("FRAMEWORK_CALL_OFF_REQUISITION_NOT_FOUND",
                    "The source requisition was not found in the current tenant.");
            if (!string.Equals(requisition.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                throw Conflict("FRAMEWORK_CALL_OFF_REQUISITION_NOT_APPROVED",
                    "Only an approved requisition can supply call-off demand.");
            var warehouseId = request.DeliveryWarehouseId ?? requisition.DeliveryWarehouseId;
            if (warehouseId.HasValue)
                await EnsureWarehouseAsync(warehouseId.Value, cancellationToken);

            var resolvedLines = await ResolveLinesAsync(
                agreement, requisition, request.Lines, null, cancellationToken);
            var total = RoundMoney(resolvedLines.Sum(item => item.LineTotal));
            var authority = await ResolveAuthorityAsync(
                    agreement, total, now, true, cancellationToken)
                ?? throw Authorization(
                    "The current actor has no effective call-off authority for this agreement and amount.");
            var eligibility = await EnforceSupplierEligibilityAsync(
                agreement, correlation, cancellationToken);
            await EnsureBalanceAsync(agreement, now, cancellationToken);
            var familyCapacity = await EvaluateAgreementFamilyCapacityAsync(
                agreement,
                total,
                cancellationToken);
            if (!familyCapacity.CanReserve)
                throw Conflict("FRAMEWORK_CALL_OFF_BALANCE_INSUFFICIENT",
                    $"The call-off total {total:0.00} exceeds the available agreement-family balance {familyCapacity.AvailableAmount:0.00} {agreement.CurrencyCode}.");

            var callOffId = Guid.NewGuid();
            var number = await _documentNumbering.GenerateConfiguredAsync(
                DocumentNumberingModules.Procurement,
                "FrameworkCallOff",
                "Framework Call-off",
                $"FCO-{_currentUser.TenantId:N}"[..12] + "-{YYYY}-{#####}",
                "Yearly",
                _currentUser.TenantId,
                now,
                SourceTypeName,
                callOffId,
                cancellationToken);
            var purchaseOrder = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                OrderNumber = number,
                BusinessPartnerId = agreement.BusinessPartnerId,
                OrderDate = now,
                RequiredDate = EnsureUtc(request.RequiredDateUtc),
                Status = "Draft",
                RequestedById = requisition.RequestedById,
                SubTotal = total,
                TaxAmount = 0m,
                ShippingCost = 0m,
                MiscellaneousCost = 0m,
                TotalAdditionalCost = 0m,
                CostAllocationMethod = "SpreadToItemCost",
                CostApportionmentBasis = "Value",
                CostsAllocated = false,
                DiscountAmount = 0m,
                TotalAmount = total,
                Notes = Trim(request.Notes, 1000),
                DeliveryWarehouseId = warehouseId,
                DeliveryAddress = Trim(request.DeliveryAddress, 500) ??
                                  requisition.DeliveryAddress,
                DeliveryInstructions = requisition.DeliveryInstructions,
                ReferenceNumber = number,
                OrderType = "FrameworkCallOff",
                ContractStartDate = agreement.EffectiveFromUtc,
                ContractEndDate = ProcurementFrameworkAgreementRules.EffectiveEnd(agreement),
                ContractValue = agreement.CeilingAmount,
                ContractUsedValue = familyCapacity.CommittedAmount,
                ContractRemainingValue = familyCapacity.AvailableAmount,
                SourceRequisitionId = requisition.Id,
                SourceRequisitionNumber = requisition.RequisitionNumber,
                Currency = agreement.CurrencyCode,
                ExchangeRate = 1m,
                BudgetId = requisition.BudgetId,
                BudgetCode = requisition.BudgetCode,
                BudgetValidated = requisition.BudgetValidated,
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            await PurchaseOrders.AddAsync(purchaseOrder);

            var callOff = new ProcurementFrameworkCallOff
            {
                Id = callOffId,
                TenantId = _currentUser.TenantId,
                CallOffNumber = number,
                Status = ProcurementFrameworkCallOffStatus.Draft,
                AgreementId = agreement.Id,
                PurchaseOrderId = purchaseOrder.Id,
                SourceRequisitionId = requisition.Id,
                BusinessPartnerId = agreement.BusinessPartnerId,
                AuthorityId = authority.Id,
                AuthorityKind = authority.AuthorityKind,
                AuthorityValue = authority.AuthorityValue,
                AuthorityThreshold = authority.MaximumCallOffAmount,
                CurrencyCode = agreement.CurrencyCode,
                TotalAmount = total,
                RequiredDateUtc = EnsureUtc(request.RequiredDateUtc),
                DeliveryWarehouseId = warehouseId,
                DeliveryAddress = purchaseOrder.DeliveryAddress,
                Notes = Trim(request.Notes, 1000),
                AgreementNumber = agreement.AgreementNumber,
                AgreementVersion = agreement.Version,
                AgreementIntegrityHash = agreement.IntegrityHash,
                AwardReadinessIntegrityHash = agreement.SourceIntegrityHash,
                SupplierEligibilityDecisionHash = eligibility.DecisionHash,
                PriceListReference = agreement.PriceListReference,
                PriceListVersion = agreement.PriceListVersion,
                AgreementEffectiveFromUtc = agreement.EffectiveFromUtc,
                AgreementEffectiveEndUtc =
                    ProcurementFrameworkAgreementRules.EffectiveEnd(agreement),
                CreatedByUserId = _currentUser.UserId,
                CreatedByName = ActorName,
                CreationCorrelationId = correlation,
                LastOperationCorrelationId = correlation,
                LastOperation = "Created",
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            await CallOffs.AddAsync(callOff);
            var approvedSource =
                await _purchaseOrderSources.ResolveFrameworkCallOffAsync(
                    callOff, agreement, correlation, cancellationToken);
            _purchaseOrderSources.Apply(purchaseOrder, approvedSource);
            foreach (var resolved in resolvedLines)
            {
                var poLine = new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUser.TenantId,
                    PurchaseOrderId = purchaseOrder.Id,
                    InventoryItemId = resolved.PriceLine.InventoryItemId,
                    ItemDescription = resolved.PriceLine.ItemName,
                    OrderedQuantity = resolved.Quantity,
                    ReceivedQuantity = 0m,
                    RemainingQuantity = resolved.Quantity,
                    UnitOfMeasure = resolved.PriceLine.UnitOfMeasure,
                    WarehouseId = warehouseId,
                    UnitPrice = resolved.PriceLine.UnitPrice,
                    LineTotal = resolved.LineTotal,
                    LandedUnitCost = resolved.PriceLine.UnitPrice,
                    ExpectedDeliveryDate = EnsureUtc(request.RequiredDateUtc),
                    CreatedAt = now,
                    CreatedBy = ActorName,
                    CreatedById = _currentUser.UserId
                };
                await PurchaseOrderItems.AddAsync(poLine);
                var line = new ProcurementFrameworkCallOffLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUser.TenantId,
                    CallOffId = callOff.Id,
                    AgreementPriceLineId = resolved.PriceLine.Id,
                    PurchaseRequisitionItemId = resolved.DemandLine.Id,
                    PurchaseOrderItemId = poLine.Id,
                    InventoryItemId = resolved.PriceLine.InventoryItemId,
                    ItemCode = resolved.PriceLine.ItemCode,
                    ItemName = resolved.PriceLine.ItemName,
                    UnitOfMeasure = resolved.PriceLine.UnitOfMeasure,
                    Quantity = resolved.Quantity,
                    UnitPrice = resolved.PriceLine.UnitPrice,
                    LineTotal = resolved.LineTotal,
                    SourceDemandQuantity = resolved.DemandLine.Quantity,
                    PriceIntegrityHash = resolved.PriceLine.IntegrityHash,
                    CreatedAt = now,
                    CreatedBy = ActorName,
                    CreatedById = _currentUser.UserId
                };
                Capture(line);
                await CallOffLines.AddAsync(line);
                callOff.Lines.Add(line);
            }
            Capture(callOff);
            await _purchaseOrderSources.RecordBoundAsync(
                purchaseOrder,
                "FrameworkCallOffPurchaseOrderCreated",
                correlation,
                cancellationToken);
            await RecordEventAsync(
                callOff, agreement, "Created", ProcurementControlEventResult.Succeeded,
                null, Snapshot(callOff), "Server-derived call-off draft created.",
                [], correlation, now, cancellationToken);
            result = callOff;
            created = true;
        }, cancellationToken);
        if (created)
        {
            await PublishNotificationAsync(
                "procurement.framework-call-off.created", result!, cancellationToken);
        }
        return await GetInternalAsync(result!.Id, cancellationToken);
    }

    public async Task<ProcurementFrameworkCallOffDto> SubmitAsync(
        Guid id,
        ProcurementFrameworkCallOffLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            ManagePermission, id.ToString(), correlation, cancellationToken);
        EnsureEvidence(request.Evidence);
        ProcurementFrameworkCallOff? result = null;
        PurchaseOrder? automaticApprovalPurchaseOrder = null;
        var submitted = false;
        try
        {
            await ExecuteAsync(async () =>
            {
                var callOff = await LoadAsync(id, true, cancellationToken);
                if (IsReplay(callOff, "Submitted", correlation) ||
                    IsReplay(callOff, "ApprovedOnSubmit", correlation))
                {
                    result = callOff;
                    return;
                }
                EnsureStatus(callOff, ProcurementFrameworkCallOffStatus.Draft,
                    "Only a Draft call-off can be submitted.");
                EnsureRowVersion(callOff.RowVersion, request.RowVersion);
                var agreement = await RevalidateAsync(
                    callOff, true, correlation, cancellationToken);
                await EnforcePurchaseOrderComplianceAsync(
                    callOff.PurchaseOrder,
                    "Submit",
                    correlation,
                    cancellationToken);
                var definition = await ResolvePurchaseOrderWorkflowAsync(cancellationToken)
                    ?? throw Conflict("FRAMEWORK_CALL_OFF_WORKFLOW_REQUIRED",
                        "A Published and active TDC Purchase Order Approval workflow is required.");
                var workflowResult = await _workflow.SubmitAsync(
                    PurchaseOrderWorkflowEntity, callOff.PurchaseOrderId, definition.Id);
                if (!workflowResult.ExecutionResult.Success)
                    throw Conflict("FRAMEWORK_CALL_OFF_WORKFLOW_START_FAILED",
                        workflowResult.ExecutionResult.Message ??
                        "The shared purchase-order workflow could not start.");
                if (ProcurementPurchaseOrderSodRules.IsAutomaticApproval(
                        workflowResult.Outcome))
                {
                    automaticApprovalPurchaseOrder = callOff.PurchaseOrder;
                    throw new AutomaticApprovalDetectedException();
                }
                if (workflowResult.Outcome == WorkflowOutcome.Rejected)
                {
                    throw Conflict("FRAMEWORK_CALL_OFF_WORKFLOW_START_REJECTED",
                        "The shared workflow rejected or failed while starting; the call-off remains Draft.");
                }

                var now = DateTime.UtcNow;
                callOff.WorkflowDefinitionId = definition.Id;
                callOff.WorkflowInstanceId = workflowResult.ExecutionResult.WorkflowInstanceId;
                callOff.SubmittedById = _currentUser.UserId;
                callOff.SubmittedByName = ActorName;
                callOff.SubmittedAtUtc = now;
                callOff.DecisionComment = Trim(request.Comment, 1000);
                callOff.Status = ProcurementFrameworkCallOffStatus.PendingApproval;
                callOff.LastOperation = "Submitted";
                callOff.LastOperationCorrelationId = correlation;
                callOff.UpdatedAt = now;
                callOff.UpdatedBy = ActorName;
                callOff.LastModifiedById = _currentUser.UserId;
                Capture(callOff);
                await CallOffs.UpdateAsync(callOff);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var purchaseOrder = callOff.PurchaseOrder;
                _workflowAdapters.GetAdapter(PurchaseOrderWorkflowEntity)
                    .ApplySubmitOutcome(
                        purchaseOrder, workflowResult.Outcome, _currentUser.UserId);
                purchaseOrder.UpdatedAt = now;
                purchaseOrder.UpdatedBy = ActorName;
                purchaseOrder.LastModifiedById = _currentUser.UserId;
                await PurchaseOrders.UpdateAsync(purchaseOrder);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await RecordEventAsync(
                    callOff, agreement, "Submitted",
                    ProcurementControlEventResult.ReviewRequired,
                    null, Snapshot(callOff), request.Comment, request.Evidence,
                    correlation, now, cancellationToken);
                result = callOff;
                submitted = true;
            }, cancellationToken);
        }
        catch (AutomaticApprovalDetectedException)
        {
            await _purchaseOrderSod.RejectApprovalBypassAsync(
                automaticApprovalPurchaseOrder!,
                "FrameworkWorkflowAutoApprove",
                correlation,
                cancellationToken);
            throw;
        }
        if (submitted)
        {
            await PublishNotificationAsync(
                "procurement.framework-call-off.submitted",
                result!, cancellationToken);
        }
        return await GetInternalAsync(result.Id, cancellationToken);
    }

    public async Task<ProcurementFrameworkCallOffDto> DecideAsync(
        Guid id,
        ProcurementFrameworkCallOffDecisionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            ApprovePermission, id.ToString(), correlation, cancellationToken);
        EnsureEvidence(request.Evidence);
        ProcurementFrameworkCallOff? result = null;
        var decided = false;
        await ExecuteAsync(async () =>
        {
            var callOff = await LoadAsync(id, true, cancellationToken);
            var operation = request.Approved ? "Approved" : "Rejected";
            if (IsReplay(callOff, operation, correlation) ||
                IsReplay(callOff, "ApprovalProgressed", correlation))
            {
                result = callOff;
                return;
            }
            EnsureStatus(callOff, ProcurementFrameworkCallOffStatus.PendingApproval,
                "Only a Pending Approval call-off can be decided.");
            EnsureRowVersion(callOff.RowVersion, request.RowVersion);
            if (!callOff.WorkflowInstanceId.HasValue ||
                !callOff.WorkflowDefinitionId.HasValue)
                throw Conflict("FRAMEWORK_CALL_OFF_WORKFLOW_BINDING_REQUIRED",
                    "The call-off has no exact shared workflow binding.");
            var canApprove = await _workflow.CanUserApproveAsync(
                PurchaseOrderWorkflowEntity, callOff.PurchaseOrderId,
                _currentUser.UserId);
            if (!canApprove)
                throw Authorization(
                    "The current actor is not assigned to the active purchase-order workflow step.");

            // LoadAsync includes the retained agreement graph. Rejection skips
            // commercial revalidation by design, but its control event still
            // needs the immutable agreement rule id/hash.
            ProcurementFrameworkAgreement agreement = callOff.Agreement;
            if (ProcurementFrameworkCallOffCommercialRules
                    .RequiresCommercialRevalidationOnDecision(request.Approved))
            {
                agreement = await RevalidateAsync(
                    callOff, false, correlation, cancellationToken);
                await _purchaseOrderSod.EnforceApprovalAsync(
                    callOff.PurchaseOrder,
                    correlation,
                    cancellationToken);
                await EnforcePurchaseOrderComplianceAsync(
                    callOff.PurchaseOrder,
                    "Approve",
                    correlation,
                    cancellationToken);
            }
            var workflowResult = await _workflow.ProcessApprovalAsync(
                PurchaseOrderWorkflowEntity,
                callOff.PurchaseOrderId,
                _currentUser.UserId,
                request.Approved ? "approve" : "reject",
                request.Comment);
            if (!workflowResult.ExecutionResult.Success)
                throw Conflict("FRAMEWORK_CALL_OFF_WORKFLOW_DECISION_FAILED",
                    workflowResult.ExecutionResult.Message ??
                    "The shared workflow decision failed.");
            if (request.Approved && workflowResult.Outcome == WorkflowOutcome.Rejected)
                throw Conflict("FRAMEWORK_CALL_OFF_WORKFLOW_APPROVAL_CONTRADICTED",
                    "The shared workflow did not approve this call-off.");
            if (!request.Approved && workflowResult.Outcome == WorkflowOutcome.Approved)
                throw Conflict("FRAMEWORK_CALL_OFF_WORKFLOW_REJECTION_CONTRADICTED",
                    "A Completed shared workflow cannot be recorded as a rejected call-off.");

            var now = DateTime.UtcNow;
            if (workflowResult.Outcome == WorkflowOutcome.Approved)
            {
                await CommitBalanceAsync(
                    callOff,
                    agreement!,
                    correlation,
                    now,
                    cancellationToken);
                await _purchaseOrderSources.EnsureBudgetCommitmentForIssueAsync(
                    callOff.PurchaseOrder,
                    correlation,
                    cancellationToken);
                await _budgetCommitments.CommitPurchaseOrderAsync(
                    callOff.PurchaseOrder,
                    correlation,
                    cancellationToken);
                // The immutable Finance exposure must be visible before the
                // governed PO enters Approved and invokes the SQL hard stop.
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                callOff.Status = ProcurementFrameworkCallOffStatus.Approved;
                callOff.ApprovedById = _currentUser.UserId;
                callOff.ApprovedByName = ActorName;
                callOff.ApprovedAtUtc = now;
                callOff.LastOperation = "Approved";
            }
            else if (workflowResult.Outcome == WorkflowOutcome.Rejected)
            {
                callOff.Status = ProcurementFrameworkCallOffStatus.Rejected;
                callOff.RejectedById = _currentUser.UserId;
                callOff.RejectedByName = ActorName;
                callOff.RejectedAtUtc = now;
                callOff.LastOperation = "Rejected";
            }
            else
            {
                callOff.LastOperation = "ApprovalProgressed";
            }
            callOff.DecisionComment = Trim(request.Comment, 1000);
            callOff.LastOperationCorrelationId = correlation;
            callOff.UpdatedAt = now;
            callOff.UpdatedBy = ActorName;
            callOff.LastModifiedById = _currentUser.UserId;
            Capture(callOff);
            await CallOffs.UpdateAsync(callOff);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var purchaseOrder = callOff.PurchaseOrder;
            _workflowAdapters.GetAdapter(PurchaseOrderWorkflowEntity)
                .ApplyApprovalOutcome(
                    purchaseOrder, workflowResult.Outcome,
                    _currentUser.UserId, request.Comment);
            purchaseOrder.UpdatedAt = now;
            purchaseOrder.UpdatedBy = ActorName;
            purchaseOrder.LastModifiedById = _currentUser.UserId;
            await PurchaseOrders.UpdateAsync(purchaseOrder);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await RecordEventAsync(
                callOff, agreement, callOff.LastOperation,
                workflowResult.Outcome switch
                {
                    WorkflowOutcome.Approved =>
                        ProcurementControlEventResult.Succeeded,
                    WorkflowOutcome.Rejected =>
                        ProcurementControlEventResult.Rejected,
                    _ => ProcurementControlEventResult.ReviewRequired
                },
                null, Snapshot(callOff), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
            result = callOff;
            decided = true;
        }, cancellationToken);
        if (decided)
        {
            await PublishNotificationAsync(
                result!.Status switch
                {
                    ProcurementFrameworkCallOffStatus.Approved =>
                        "procurement.framework-call-off.approved",
                    ProcurementFrameworkCallOffStatus.Rejected =>
                        "procurement.framework-call-off.rejected",
                    _ => "procurement.framework-call-off.approval-progressed"
                },
                result, cancellationToken);
        }
        return await GetInternalAsync(result.Id, cancellationToken);
    }

    public async Task<ProcurementFrameworkCallOffDto> IssueAsync(
        Guid id,
        ProcurementFrameworkCallOffLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            ManagePermission, id.ToString(), correlation, cancellationToken);
        EnsureEvidence(request.Evidence);
        ProcurementFrameworkCallOff? result = null;
        var issued = false;
        await ExecuteAsync(async () =>
        {
            var callOff = await LoadAsync(id, true, cancellationToken);
            if (IsReplay(callOff, "Issued", correlation))
            {
                result = callOff;
                return;
            }
            EnsureStatus(callOff, ProcurementFrameworkCallOffStatus.Approved,
                "Only an Approved call-off can be issued.");
            EnsureRowVersion(callOff.RowVersion, request.RowVersion);
            var agreement = await RevalidateAsync(
                callOff, true, correlation, cancellationToken);
            if (!callOff.BalanceDeductedAtUtc.HasValue)
                throw Conflict("FRAMEWORK_CALL_OFF_BALANCE_NOT_DEDUCTED",
                    "The agreement balance was not committed by final approval.");
            var balance = await Balances.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.AgreementId == callOff.AgreementId && !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw Conflict("FRAMEWORK_CALL_OFF_BALANCE_NOT_FOUND",
                    "The agreement balance ledger is unavailable.");
            var commitmentExists = await BalanceMovements.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.CallOffId == callOff.Id &&
                    item.MovementType == ProcurementFrameworkBalanceMovementType.Commitment &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!commitmentExists)
                throw Conflict("FRAMEWORK_CALL_OFF_COMMITMENT_NOT_FOUND",
                    "The approval commitment movement is unavailable.");

            var now = DateTime.UtcNow;
            var before = balance.AvailableAmount;
            var issuedAmount = RoundMoney(balance.IssuedAmount + callOff.TotalAmount);
            var movement = CreateMovement(
                balance, callOff, ProcurementFrameworkBalanceMovementType.Issue,
                callOff.TotalAmount, before, before, correlation, now);
            await BalanceMovements.AddAsync(movement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            balance.IssuedAmount = issuedAmount;
            balance.LastMovementAtUtc = now;
            balance.LastMovementId = movement.Id;
            Capture(balance);
            await Balances.UpdateAsync(balance);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            callOff.Status = ProcurementFrameworkCallOffStatus.Issued;
            callOff.IssuedById = _currentUser.UserId;
            callOff.IssuedByName = ActorName;
            callOff.IssuedAtUtc = now;
            callOff.DecisionComment = Trim(request.Comment, 1000);
            callOff.LastOperation = "Issued";
            callOff.LastOperationCorrelationId = correlation;
            callOff.UpdatedAt = now;
            callOff.UpdatedBy = ActorName;
            callOff.LastModifiedById = _currentUser.UserId;
            Capture(callOff);
            await CallOffs.UpdateAsync(callOff);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            callOff.PurchaseOrder.Status = "Sent";
            callOff.PurchaseOrder.UpdatedAt = now;
            callOff.PurchaseOrder.UpdatedBy = ActorName;
            callOff.PurchaseOrder.LastModifiedById = _currentUser.UserId;
            await PurchaseOrders.UpdateAsync(callOff.PurchaseOrder);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await RecordEventAsync(
                callOff, agreement, "Issued", ProcurementControlEventResult.Succeeded,
                null, Snapshot(callOff), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
            result = callOff;
            issued = true;
        }, cancellationToken);
        if (issued)
        {
            await PublishNotificationAsync(
                "procurement.framework-call-off.issued", result!, cancellationToken);
        }
        return await GetInternalAsync(result!.Id, cancellationToken);
    }

    public async Task<ProcurementFrameworkCallOffDto> CancelAsync(
        Guid id,
        ProcurementFrameworkCallOffLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            ManagePermission, id.ToString(), correlation, cancellationToken);
        EnsureEvidence(request.Evidence);
        ProcurementFrameworkCallOff? result = null;
        var cancelled = false;
        await ExecuteAsync(async () =>
        {
            var callOff = await LoadAsync(id, true, cancellationToken);
            if (IsReplay(callOff, "Cancelled", correlation))
            {
                result = callOff;
                return;
            }
            if (callOff.Status is not (
                    ProcurementFrameworkCallOffStatus.Draft or
                    ProcurementFrameworkCallOffStatus.PendingApproval or
                    ProcurementFrameworkCallOffStatus.Approved))
                throw Conflict("FRAMEWORK_CALL_OFF_CANCELLATION_NOT_ALLOWED",
                    "Only a Draft, Pending Approval, or not-yet-issued Approved call-off can be cancelled.");
            EnsureRowVersion(callOff.RowVersion, request.RowVersion);
            if (callOff.Status == ProcurementFrameworkCallOffStatus.Approved)
                throw Conflict(
                    "FRAMEWORK_CALL_OFF_COMMITMENT_REVERSAL_REQUIRED",
                    "An Approved framework call-off cannot be cancelled until a dedicated serializable reversal atomically releases its formal budget exposure and agreement balance.");
            var agreement = callOff.Agreement;
            var now = DateTime.UtcNow;
            if (callOff.Status == ProcurementFrameworkCallOffStatus.PendingApproval)
            {
                var cancelled = await _workflow.CancelWorkflowAsync(
                    PurchaseOrderWorkflowEntity, callOff.PurchaseOrderId,
                    request.Comment);
                if (!cancelled.Success)
                    throw Conflict("FRAMEWORK_CALL_OFF_WORKFLOW_CANCEL_FAILED",
                        cancelled.Message ?? "The shared workflow could not be cancelled.");
            }
            callOff.Status = ProcurementFrameworkCallOffStatus.Cancelled;
            callOff.CancelledById = _currentUser.UserId;
            callOff.CancelledByName = ActorName;
            callOff.CancelledAtUtc = now;
            callOff.DecisionComment = Trim(request.Comment, 1000);
            callOff.LastOperation = "Cancelled";
            callOff.LastOperationCorrelationId = correlation;
            callOff.UpdatedAt = now;
            callOff.UpdatedBy = ActorName;
            callOff.LastModifiedById = _currentUser.UserId;
            Capture(callOff);
            await CallOffs.UpdateAsync(callOff);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            callOff.PurchaseOrder.Status = "Cancelled";
            callOff.PurchaseOrder.CancelledAtUtc = now;
            callOff.PurchaseOrder.UpdatedAt = now;
            callOff.PurchaseOrder.UpdatedBy = ActorName;
            callOff.PurchaseOrder.LastModifiedById = _currentUser.UserId;
            await PurchaseOrders.UpdateAsync(callOff.PurchaseOrder);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await RecordEventAsync(
                callOff, agreement, "Cancelled",
                ProcurementControlEventResult.Succeeded,
                null, Snapshot(callOff), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
            result = callOff;
            cancelled = true;
        }, cancellationToken);
        if (cancelled)
        {
            await PublishNotificationAsync(
                "procurement.framework-call-off.cancelled", result!, cancellationToken);
        }
        return await GetInternalAsync(result!.Id, cancellationToken);
    }

    public async Task<int> ProcessExpiryAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(
            $"framework-call-off-expiry-{DateTime.UtcNow:yyyyMMddHH}");
        await EnsureCapabilityAsync(
            ManagePermission, "expiry-alerts", correlation, cancellationToken);
        var alerted = new List<(
            ProcurementFrameworkAgreementBalance Balance,
            ProcurementFrameworkCallOffCommercialRules.FamilySummary Summary)>();
        await ExecuteAsync(async () =>
        {
            var now = DateTime.UtcNow;
            var publishedAgreements = await _unitOfWork
                .Repository<ProcurementFrameworkAgreement>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Status == ProcurementFrameworkAgreementStatus.Published &&
                    item.EffectiveFromUtc <= now &&
                    !item.IsDeleted)
                .Include(item => item.Extensions.Where(child => !child.IsDeleted))
                .ToListAsync(cancellationToken);
            var currentAgreementIds =
                ProcurementFrameworkCallOffCommercialRules
                    .SelectCurrentEffectiveRevisions(
                        publishedAgreements.Select(ToAgreementRevisionState),
                        now)
                    .Select(item => item.AgreementId)
                    .ToHashSet();
            var currentAgreements = publishedAgreements
                .Where(item => currentAgreementIds.Contains(item.Id))
                .ToList();
            var balances = await Balances.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    currentAgreementIds.Contains(item.AgreementId) &&
                    !item.IsDeleted)
                .ToDictionaryAsync(item => item.AgreementId, cancellationToken);
            var createdBalance = false;
            foreach (var agreement in currentAgreements)
            {
                if (balances.TryGetValue(agreement.Id, out var balance))
                {
                    balance.Agreement = agreement;
                    agreement.Balance = balance;
                    continue;
                }

                balance = CreateBalance(agreement, now);
                await Balances.AddAsync(balance);
                balances.Add(agreement.Id, balance);
                createdBalance = true;
            }
            if (createdBalance)
                await _unitOfWork.SaveChangesAsync(cancellationToken);

            var familySummaries = await ResolveRevisionFamilySummariesAsync(
                currentAgreements,
                now,
                cancellationToken);
            foreach (var agreement in currentAgreements)
            {
                var balance = balances[agreement.Id];
                if (!familySummaries.TryGetValue(
                        balance.AgreementId,
                        out var familySummary) ||
                    !ProcurementFrameworkCallOffCommercialRules
                        .ShouldEmitExpiryAlert(
                            familySummary,
                            balance.AgreementId,
                            balance.LastExpiryAlertAtUtc,
                            now,
                            ExpiryAlertDays))
                {
                    continue;
                }

                balance.LastExpiryAlertAtUtc = now;
                balance.UpdatedAt = now;
                balance.UpdatedBy = ActorName;
                balance.LastModifiedById = _currentUser.UserId;
                Capture(balance);
                await Balances.UpdateAsync(balance);
                await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
                {
                    EventKey = ProcurementControlEventKey.Create(
                        "framework-call-off-expiry", balance.TenantId,
                        balance.AgreementId, now.ToString("yyyyMMdd")),
                    EventType = EventType,
                    Action = "AgreementExpiryAlerted",
                    Result = ProcurementControlEventResult.Warning,
                    RuleCode = "FRAMEWORK_EXPIRY_30_DAYS",
                    RuleId = balance.AgreementId,
                    RuleVersion = balance.Agreement.IntegrityHash,
                    DecisionKeys = DecisionKeys.ToList(),
                    SourceType = SourceTypeName,
                    SourceId = balance.AgreementId,
                    SourceReference = balance.Agreement.AgreementNumber,
                    Reason = "Effective framework has remaining balance and expires within 30 days.",
                    ResultValues = new
                    {
                        familySummary.AvailableAmount,
                        familySummary.CurrencyCode,
                        familySummary.EffectiveEndUtc
                    },
                    CorrelationId = correlation,
                    CausationId = correlation,
                    OccurredAtUtc = now
                }, cancellationToken);
                alerted.Add((balance, familySummary));
            }
        }, cancellationToken);
        foreach (var (balance, familySummary) in alerted)
        {
            await PublishExpiryNotificationAsync(
                balance,
                familySummary,
                cancellationToken);
        }
        return alerted.Count;
    }

    public Task<bool> IsFrameworkCallOffPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        return CallOffs.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderId == purchaseOrderId && !item.IsDeleted)
            .AnyAsync(cancellationToken);
    }

    private IQueryable<ProcurementFrameworkCallOff> CallOffQuery() =>
        CallOffs.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Agreement).ThenInclude(item => item.Extensions)
            .Include(item => item.Agreement).ThenInclude(item => item.Balance)
            .Include(item => item.PurchaseOrder)
            .Include(item => item.SourceRequisition)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Authority)
            .Include(item => item.Lines.Where(line => !line.IsDeleted))
            .Include(item => item.BalanceMovements.Where(movement => !movement.IsDeleted));

    private async Task<ProcurementFrameworkCallOff> LoadAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = CallOffQuery();
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(
                   item => item.Id == id, cancellationToken)
               ?? throw NotFound("FRAMEWORK_CALL_OFF_NOT_FOUND",
                   "The call-off was not found in the current tenant.");
    }

    private async Task<ProcurementFrameworkCallOffDto> GetInternalAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await MapAsync(await LoadAsync(id, false, cancellationToken), cancellationToken);

    private IQueryable<ProcurementFrameworkAgreement> AgreementQuery(bool tracked)
    {
        var query = _unitOfWork.Repository<ProcurementFrameworkAgreement>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Categories.Where(child => !child.IsDeleted))
            .Include(item => item.PriceLines.Where(child => !child.IsDeleted))
            .Include(item => item.CallOffAuthorities.Where(child => !child.IsDeleted))
            .Include(item => item.Extensions.Where(child => !child.IsDeleted))
            .Include(item => item.Balance);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<ProcurementFrameworkAgreement> LoadAgreementAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken) =>
        await AgreementQuery(tracked).SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken)
        ?? throw NotFound("FRAMEWORK_CALL_OFF_AGREEMENT_NOT_FOUND",
            "The framework agreement was not found in the current tenant.");

    private async Task EnsureCurrentAgreementRevisionAsync(
        ProcurementFrameworkAgreement agreement,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        var family = await _unitOfWork
            .Repository<ProcurementFrameworkAgreement>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.AgreementKey == agreement.AgreementKey &&
                !item.IsDeleted)
            .Include(item => item.Extensions.Where(child => !child.IsDeleted))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (!ProcurementFrameworkCallOffCommercialRules
                .IsCurrentEffectiveRevision(
                    ToAgreementRevisionState(agreement),
                    family.Select(ToAgreementRevisionState),
                    atUtc))
        {
            throw Conflict(
                "FRAMEWORK_CALL_OFF_AGREEMENT_REVISION_SUPERSEDED",
                "A newer effective framework revision now governs this agreement family.");
        }
    }

    private async Task<ProcurementFrameworkAgreement> RevalidateAsync(
        ProcurementFrameworkCallOff callOff,
        bool requireCurrentActorAuthority,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var agreement = await LoadAgreementAsync(
            callOff.AgreementId, true, cancellationToken);
        EnsureEffectiveAgreement(agreement, now, callOff.RequiredDateUtc);
        await EnsureCurrentAgreementRevisionAsync(
            agreement,
            now,
            cancellationToken);
        if (agreement.BusinessPartnerId != callOff.BusinessPartnerId ||
            agreement.AgreementNumber != callOff.AgreementNumber ||
            agreement.Version != callOff.AgreementVersion ||
            agreement.IntegrityHash != callOff.AgreementIntegrityHash ||
            agreement.SourceIntegrityHash != callOff.AwardReadinessIntegrityHash ||
            agreement.PriceListReference != callOff.PriceListReference ||
            agreement.PriceListVersion != callOff.PriceListVersion ||
            !string.Equals(agreement.CurrencyCode, callOff.CurrencyCode,
                StringComparison.OrdinalIgnoreCase))
            throw Conflict("FRAMEWORK_CALL_OFF_AGREEMENT_LINEAGE_CHANGED",
                "The call-off no longer matches the immutable framework version and must not proceed.");

        var selectedAuthority = agreement.CallOffAuthorities.SingleOrDefault(item =>
            item.Id == callOff.AuthorityId && !item.IsDeleted && item.IsActive);
        if (selectedAuthority is null ||
            selectedAuthority.ValidFromUtc > now ||
            EffectiveAuthorityEnd(agreement, selectedAuthority) < now ||
            selectedAuthority.MaximumCallOffAmount.HasValue &&
            callOff.TotalAmount > selectedAuthority.MaximumCallOffAmount.Value)
            throw Conflict("FRAMEWORK_CALL_OFF_AUTHORITY_EXPIRED",
                "The selected call-off authority is no longer effective for this amount.");
        if (requireCurrentActorAuthority &&
            !await AuthorityMatchesCurrentActorAsync(
                selectedAuthority, false, cancellationToken))
            throw Authorization(
                "The current actor is not the effective agreement authority for this call-off.");

        var requisition = await Requisitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == callOff.SourceRequisitionId && !item.IsDeleted)
            .Include(item => item.Items.Where(line => !line.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("FRAMEWORK_CALL_OFF_REQUISITION_NOT_FOUND",
                "The source requisition is no longer available.");
        if (!string.Equals(requisition.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            throw Conflict("FRAMEWORK_CALL_OFF_REQUISITION_NOT_APPROVED",
                "The source requisition is no longer Approved.");
        var lineRequests = callOff.Lines.Select(line =>
            new CreateProcurementFrameworkCallOffLineRequest
            {
                PurchaseRequisitionItemId = line.PurchaseRequisitionItemId,
                AgreementPriceLineId = line.AgreementPriceLineId,
                Quantity = line.Quantity
            }).ToList();
        var resolved = await ResolveLinesAsync(
            agreement, requisition, lineRequests, callOff.Id, cancellationToken);
        if (RoundMoney(resolved.Sum(item => item.LineTotal)) != callOff.TotalAmount ||
            resolved.Any(item =>
                !callOff.Lines.Any(line =>
                    line.AgreementPriceLineId == item.PriceLine.Id &&
                    line.PurchaseRequisitionItemId == item.DemandLine.Id &&
                    line.PriceIntegrityHash == item.PriceLine.IntegrityHash &&
                    line.UnitPrice == item.PriceLine.UnitPrice &&
                    line.Quantity == item.Quantity)))
            throw Conflict("FRAMEWORK_CALL_OFF_PRICE_OR_DEMAND_CHANGED",
                "The governed price or source-demand lineage no longer matches the call-off.");
        var eligibility = await EnforceSupplierEligibilityAsync(
            agreement, correlationId, cancellationToken);
        callOff.SupplierEligibilityDecisionHash = eligibility.DecisionHash;
        await EnsureBalanceAsync(agreement, now, cancellationToken);
        if (callOff.Status is ProcurementFrameworkCallOffStatus.Draft or
            ProcurementFrameworkCallOffStatus.PendingApproval)
        {
            var familyCapacity = await EvaluateAgreementFamilyCapacityAsync(
                agreement,
                callOff.TotalAmount,
                cancellationToken);
            if (!familyCapacity.CanReserve)
            {
                throw Conflict(
                    "FRAMEWORK_CALL_OFF_BALANCE_INSUFFICIENT",
                    $"The call-off total exceeds the available agreement-family balance of {familyCapacity.AvailableAmount:0.00} {agreement.CurrencyCode}.");
            }
        }
        return agreement;
    }

    private async Task<List<ResolvedLine>> ResolveLinesAsync(
        ProcurementFrameworkAgreement agreement,
        PurchaseRequisition requisition,
        IReadOnlyCollection<CreateProcurementFrameworkCallOffLineRequest> requests,
        Guid? currentCallOffId,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            throw Validation("FRAMEWORK_CALL_OFF_LINES_REQUIRED",
                "At least one source-demand line is required.");
        if (requests.Any(item =>
                item.PurchaseRequisitionItemId == Guid.Empty ||
                item.AgreementPriceLineId == Guid.Empty ||
                item.Quantity <= 0))
            throw Validation("FRAMEWORK_CALL_OFF_LINE_INVALID",
                "Every call-off line requires source demand, governed price, and a positive quantity.");
        if (!ProcurementFrameworkCallOffCommercialRules.HasDistinctDemandLineage(
                requests.Select(item => (
                    item.PurchaseRequisitionItemId,
                    item.AgreementPriceLineId))))
            throw Validation("FRAMEWORK_CALL_OFF_DEMAND_LINE_DUPLICATE",
                "A requisition line can appear only once in a call-off.");

        var demand = requisition.Items.ToDictionary(item => item.Id);
        var prices = agreement.PriceLines.Where(item => !item.IsDeleted)
            .ToDictionary(item => item.Id);
        var demandIds = requests.Select(item => item.PurchaseRequisitionItemId).ToArray();
        var allocated = await CallOffLines.GetQueryable(line =>
                line.TenantId == _currentUser.TenantId && !line.IsDeleted &&
                demandIds.Contains(line.PurchaseRequisitionItemId) &&
                (!currentCallOffId.HasValue || line.CallOffId != currentCallOffId.Value) &&
                !line.CallOff.IsDeleted &&
                ActiveDemandStatuses.Contains(line.CallOff.Status))
            .GroupBy(line => line.PurchaseRequisitionItemId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToDictionaryAsync(item => item.Id, item => item.Quantity, cancellationToken);
        var result = new List<ResolvedLine>();
        foreach (var request in requests)
        {
            if (!demand.TryGetValue(request.PurchaseRequisitionItemId, out var demandLine))
                throw Validation("FRAMEWORK_CALL_OFF_DEMAND_LINE_NOT_FOUND",
                    "A selected demand line does not belong to the approved requisition.");
            if (!demandLine.InventoryItemId.HasValue)
                throw Validation("FRAMEWORK_CALL_OFF_INVENTORY_ITEM_REQUIRED",
                    "Framework call-offs require inventory-mapped requisition lines.");
            if (!prices.TryGetValue(request.AgreementPriceLineId, out var priceLine))
                throw Validation("FRAMEWORK_CALL_OFF_PRICE_LINE_NOT_FOUND",
                    "A selected price line does not belong to the framework version.");
            var normalized =
                ProcurementFrameworkCallOffCommercialRules.NormalizeLine(
                    request.Quantity,
                    priceLine.UnitPrice);
            if (normalized.Quantity <= 0)
                throw Validation("FRAMEWORK_CALL_OFF_LINE_INVALID",
                    "Every call-off line requires source demand, governed price, and a positive quantity.");
            if (demandLine.InventoryItemId.Value != priceLine.InventoryItemId ||
                !string.Equals(
                    NormalizeUnit(demandLine.UnitOfMeasure),
                    NormalizeUnit(priceLine.UnitOfMeasure),
                    StringComparison.OrdinalIgnoreCase))
                throw Validation("FRAMEWORK_CALL_OFF_ITEM_UOM_MISMATCH",
                    "The governed price item and unit must exactly match source demand.");
            if (normalized.Quantity < priceLine.MinimumQuantity ||
                priceLine.MaximumQuantity.HasValue &&
                normalized.Quantity > priceLine.MaximumQuantity.Value)
                throw Validation("FRAMEWORK_CALL_OFF_QUANTITY_OUTSIDE_PRICE_BAND",
                    $"Quantity for {priceLine.ItemCode} must be between {priceLine.MinimumQuantity:0.####} and {(priceLine.MaximumQuantity.HasValue ? priceLine.MaximumQuantity.Value.ToString("0.####") : "the remaining demand")}.");
            var otherAllocation = allocated.GetValueOrDefault(demandLine.Id);
            if (otherAllocation + normalized.Quantity > demandLine.Quantity)
                throw Conflict("FRAMEWORK_CALL_OFF_SOURCE_DEMAND_EXCEEDED",
                    $"Call-offs would allocate {otherAllocation + normalized.Quantity:0.####} against source demand of {demandLine.Quantity:0.####} for {demandLine.ItemDescription}.");
            result.Add(new ResolvedLine(
                demandLine,
                priceLine,
                normalized.Quantity,
                normalized.LineTotal));
        }
        return result;
    }

    private async Task<ProcurementFrameworkCallOffAuthority?> ResolveAuthorityAsync(
        ProcurementFrameworkAgreement agreement,
        decimal? amount,
        DateTime atUtc,
        bool throwOnPermissionFailure,
        CancellationToken cancellationToken)
    {
        var candidates = agreement.CallOffAuthorities.Where(item =>
                !item.IsDeleted && item.IsActive &&
                item.ValidFromUtc <= atUtc &&
                EffectiveAuthorityEnd(agreement, item) > atUtc &&
                (!amount.HasValue || !item.MaximumCallOffAmount.HasValue ||
                 amount.Value <= item.MaximumCallOffAmount.Value))
            .OrderBy(item => item.MaximumCallOffAmount ?? decimal.MaxValue)
            .ThenBy(item => item.AuthorityKind)
            .ThenBy(item => item.AuthorityValue)
            .ToList();
        foreach (var candidate in candidates)
        {
            if (await AuthorityMatchesCurrentActorAsync(
                    candidate, throwOnPermissionFailure, cancellationToken))
                return candidate;
        }
        return null;
    }

    private async Task<bool> AuthorityMatchesCurrentActorAsync(
        ProcurementFrameworkCallOffAuthority authority,
        bool throwOnPermissionFailure,
        CancellationToken cancellationToken)
    {
        switch (authority.AuthorityKind)
        {
            case ProcurementFrameworkAuthorityKind.User:
                return authority.AuthorityUserId == _currentUser.UserId;
            case ProcurementFrameworkAuthorityKind.Role:
                return _currentUser.Roles.Any(role =>
                    RoleAuthorityMatches(role, authority.AuthorityValue));
            case ProcurementFrameworkAuthorityKind.OrganizationalUnit:
                return _currentUser.Claims.Any(claim =>
                    OrganizationalUnitClaimKeys.Contains(claim.Key) &&
                    string.Equals(claim.Value.Trim(), authority.AuthorityValue.Trim(),
                        StringComparison.OrdinalIgnoreCase));
            case ProcurementFrameworkAuthorityKind.Permission:
            {
                var request = new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = authority.AuthorityValue,
                    SourceType = SourceTypeName,
                    SourceReference = authority.AgreementId.ToString()
                };
                var decision = throwOnPermissionFailure
                    ? await _accessControl.EnforceCapabilityAsync(
                        request,
                        $"framework-authority-{authority.Id:N}",
                        cancellationToken)
                    : await _accessControl.CheckCapabilityAsync(
                        request,
                        $"framework-authority-check-{authority.Id:N}",
                        cancellationToken);
                return decision.Allowed;
            }
            default:
                return false;
        }
    }

    private async Task<SupplierValidationResult> EnforceSupplierEligibilityAsync(
        ProcurementFrameworkAgreement agreement,
        string correlationId,
        CancellationToken cancellationToken) =>
        await _supplierValidation.EnforceEligibilityAsync(
            new SupplierEligibilityEvaluationRequest
            {
                BusinessPartnerId = agreement.BusinessPartnerId,
                Boundary = SupplierEligibilityBoundary.FrameworkCallOff,
                CategoryIds = agreement.Categories
                    .Where(item => !item.IsDeleted)
                    .Select(item => item.PartnerCategoryId)
                    .Distinct().ToList(),
                RecordAudit = true,
                SourceType = SourceTypeName,
                SourceId = agreement.Id,
                SourceReference = $"{agreement.AgreementNumber}/v{agreement.Version}",
                CorrelationId = correlationId
            }, cancellationToken);

    private async Task<ProcurementFrameworkAgreementBalance> EnsureBalanceAsync(
        ProcurementFrameworkAgreement agreement,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var balance = await Balances.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.AgreementId == agreement.Id && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);
        if (balance is null)
        {
            balance = CreateBalance(agreement, now);
            await Balances.AddAsync(balance);
        }
        else if (balance.CeilingAmount != agreement.CeilingAmount ||
                 !string.Equals(balance.CurrencyCode, agreement.CurrencyCode,
                     StringComparison.OrdinalIgnoreCase))
        {
            throw Conflict("FRAMEWORK_CALL_OFF_BALANCE_LINEAGE_INVALID",
                "The agreement balance does not match the immutable ceiling and currency.");
        }
        return balance;
    }

    private ProcurementFrameworkAgreementBalance CreateBalance(
        ProcurementFrameworkAgreement agreement,
        DateTime now)
    {
        var balance = new ProcurementFrameworkAgreementBalance
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            AgreementId = agreement.Id,
            Agreement = agreement,
            CurrencyCode = agreement.CurrencyCode,
            CeilingAmount = agreement.CeilingAmount,
            CommittedAmount = 0m,
            IssuedAmount = 0m,
            AvailableAmount = agreement.CeilingAmount,
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId
        };
        Capture(balance);
        agreement.Balance = balance;
        return balance;
    }

    private async Task<ProcurementFrameworkCallOffCommercialRules.FamilyCapacity>
        EvaluateAgreementFamilyCapacityAsync(
            ProcurementFrameworkAgreement agreement,
            decimal proposedAmount,
            CancellationToken cancellationToken)
    {
        var familyAgreementIds = await _unitOfWork
            .Repository<ProcurementFrameworkAgreement>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.AgreementKey == agreement.AgreementKey &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (familyAgreementIds.Count == 0)
        {
            throw Conflict(
                "FRAMEWORK_CALL_OFF_BALANCE_LINEAGE_INVALID",
                "The framework agreement family is unavailable.");
        }

        var committedAmount = await BalanceMovements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                familyAgreementIds.Contains(item.AgreementId) &&
                !item.IsDeleted &&
                (item.MovementType ==
                    ProcurementFrameworkBalanceMovementType.Commitment ||
                 item.MovementType ==
                    ProcurementFrameworkBalanceMovementType.Release))
            .SumAsync(item =>
                    item.MovementType ==
                    ProcurementFrameworkBalanceMovementType.Commitment
                        ? item.Amount
                        : -item.Amount,
                cancellationToken);
        if (committedAmount < 0m)
        {
            throw Conflict(
                "FRAMEWORK_CALL_OFF_BALANCE_LINEAGE_INVALID",
                "The framework agreement-family commitment ledger is negative.");
        }

        return ProcurementFrameworkCallOffCommercialRules
            .EvaluateFamilyCapacity(
                agreement.CeilingAmount,
                committedAmount,
                proposedAmount);
    }

    private async Task<IReadOnlyDictionary<Guid,
        ProcurementFrameworkCallOffCommercialRules.FamilySummary>>
        ResolveRevisionFamilySummariesAsync(
            IEnumerable<ProcurementFrameworkAgreement> agreements,
            DateTime atUtc,
            CancellationToken cancellationToken)
    {
        var familyKeys = agreements
            .Select(item => item.AgreementKey)
            .Distinct()
            .ToArray();
        if (familyKeys.Length == 0)
        {
            return new Dictionary<Guid,
                ProcurementFrameworkCallOffCommercialRules.FamilySummary>();
        }

        var revisions = await _unitOfWork
            .Repository<ProcurementFrameworkAgreement>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                familyKeys.Contains(item.AgreementKey) &&
                !item.IsDeleted)
            .Include(item => item.Extensions.Where(child => !child.IsDeleted))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var agreementIds = revisions
            .Select(item => item.Id)
            .ToArray();
        var movements = await BalanceMovements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                agreementIds.Contains(item.AgreementId) &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item =>
                new ProcurementFrameworkCallOffCommercialRules.MovementState(
                    item.AgreementId,
                    item.MovementType,
                    item.Amount))
            .ToListAsync(cancellationToken);

        return ProcurementFrameworkCallOffCommercialRules
            .SummarizeRevisionFamilies(
                revisions.Select(ToAgreementRevisionState),
                movements,
                atUtc);
    }

    private async Task CommitBalanceAsync(
        ProcurementFrameworkCallOff callOff,
        ProcurementFrameworkAgreement agreement,
        string correlationId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (callOff.BalanceDeductedAtUtc.HasValue)
            return;
        var existing = await BalanceMovements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.CallOffId == callOff.Id &&
                item.MovementType == ProcurementFrameworkBalanceMovementType.Commitment &&
                !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            callOff.BalanceDeductedAtUtc = existing.OccurredAtUtc;
            return;
        }
        var balance = await EnsureBalanceAsync(agreement, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var familyCapacity = await EvaluateAgreementFamilyCapacityAsync(
            agreement,
            callOff.TotalAmount,
            cancellationToken);
        if (!familyCapacity.CanReserve)
            throw Conflict("FRAMEWORK_CALL_OFF_BALANCE_INSUFFICIENT",
                $"Final approval would exceed the available agreement-family balance of {familyCapacity.AvailableAmount:0.00} {balance.CurrencyCode}.");
        var before = balance.AvailableAmount;
        var committedAmount = RoundMoney(
            balance.CommittedAmount + callOff.TotalAmount);
        var availableAmount = RoundMoney(
            balance.CeilingAmount - committedAmount);
        var movement = CreateMovement(
            balance, callOff, ProcurementFrameworkBalanceMovementType.Commitment,
            callOff.TotalAmount, before, availableAmount,
            correlationId, now);
        await BalanceMovements.AddAsync(movement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        balance.CommittedAmount = committedAmount;
        balance.AvailableAmount = availableAmount;
        balance.LastMovementAtUtc = now;
        balance.UpdatedAt = now;
        balance.UpdatedBy = ActorName;
        balance.LastModifiedById = _currentUser.UserId;
        balance.LastMovementId = movement.Id;
        Capture(balance);
        await Balances.UpdateAsync(balance);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        callOff.BalanceDeductedAtUtc = now;
    }

    private async Task ReleaseBalanceAsync(
        ProcurementFrameworkCallOff callOff,
        string correlationId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await BalanceMovements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.CallOffId == callOff.Id &&
                item.MovementType == ProcurementFrameworkBalanceMovementType.Release &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (existing) return;
        var balance = await Balances.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.AgreementId == callOff.AgreementId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Conflict("FRAMEWORK_CALL_OFF_BALANCE_NOT_FOUND",
                "The agreement balance ledger is unavailable.");
        if (balance.CommittedAmount < callOff.TotalAmount)
            throw Conflict("FRAMEWORK_CALL_OFF_RELEASE_INVALID",
                "The call-off commitment cannot be released from the current balance.");
        var before = balance.AvailableAmount;
        var committedAmount = RoundMoney(
            balance.CommittedAmount - callOff.TotalAmount);
        var availableAmount = RoundMoney(
            balance.CeilingAmount - committedAmount);
        var movement = CreateMovement(
            balance, callOff, ProcurementFrameworkBalanceMovementType.Release,
            callOff.TotalAmount, before, availableAmount,
            correlationId, now);
        await BalanceMovements.AddAsync(movement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        balance.CommittedAmount = committedAmount;
        balance.AvailableAmount = availableAmount;
        balance.LastMovementAtUtc = now;
        balance.UpdatedAt = now;
        balance.UpdatedBy = ActorName;
        balance.LastModifiedById = _currentUser.UserId;
        balance.LastMovementId = movement.Id;
        Capture(balance);
        await Balances.UpdateAsync(balance);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private ProcurementFrameworkBalanceMovement CreateMovement(
        ProcurementFrameworkAgreementBalance balance,
        ProcurementFrameworkCallOff callOff,
        ProcurementFrameworkBalanceMovementType type,
        decimal amount,
        decimal before,
        decimal after,
        string correlationId,
        DateTime now)
    {
        var movement = new ProcurementFrameworkBalanceMovement
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            AgreementId = callOff.AgreementId,
            AgreementBalanceId = balance.Id,
            CallOffId = callOff.Id,
            MovementType = type,
            Amount = amount,
            BalanceBefore = before,
            BalanceAfter = after,
            IdempotencyKey = $"{callOff.Id:N}:{type}",
            CorrelationId = correlationId,
            ActorUserId = _currentUser.UserId,
            ActorName = ActorName,
            OccurredAtUtc = now,
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId
        };
        movement.IntegrityHash = Hash(Serialize(new
        {
            movement.TenantId,
            movement.AgreementId,
            movement.AgreementBalanceId,
            movement.CallOffId,
            movement.MovementType,
            movement.Amount,
            movement.BalanceBefore,
            movement.BalanceAfter,
            movement.IdempotencyKey,
            movement.CorrelationId,
            movement.ActorUserId,
            movement.OccurredAtUtc
        }));
        return movement;
    }

    private async Task<WorkflowDefinition?> ResolvePurchaseOrderWorkflowAsync(
        CancellationToken cancellationToken) =>
        await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                item.Name == PurchaseOrderWorkflowName &&
                item.EntityType.IsActive &&
                (item.EntityType.Code == "PURCHASE_ORDER" ||
                 item.EntityType.Name == "PurchaseOrder" ||
                 item.EntityType.Name == "Purchase Order"))
            .Include(item => item.EntityType)
            .AsNoTracking()
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task EnsureWarehouseAsync(
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var exists = await _unitOfWork.Repository<Warehouse>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == warehouseId &&
                                  !item.IsDeleted && item.IsActive)
            .AnyAsync(cancellationToken);
        if (!exists)
            throw NotFound("FRAMEWORK_CALL_OFF_WAREHOUSE_NOT_FOUND",
                "The delivery warehouse is not active in the current tenant.");
    }

    private static ProcurementFrameworkCallOffCommercialRules
        .AgreementRevisionState ToAgreementRevisionState(
            ProcurementFrameworkAgreement agreement) =>
        new(
            agreement.Id,
            agreement.AgreementKey,
            agreement.Version,
            agreement.CurrencyCode,
            agreement.CeilingAmount,
            agreement.EffectiveFromUtc,
            ProcurementFrameworkAgreementRules.EffectiveEnd(agreement),
            agreement.Status == ProcurementFrameworkAgreementStatus.Published);

    private static void EnsureEffectiveAgreement(
        ProcurementFrameworkAgreement agreement,
        DateTime now,
        DateTime requiredDate)
    {
        if (!ProcurementFrameworkAgreementRules.IsEffective(agreement, now))
            throw Conflict("FRAMEWORK_CALL_OFF_AGREEMENT_NOT_EFFECTIVE",
                "Only the Published framework version effective now can be used.");
        var required = EnsureUtc(requiredDate);
        var end = ProcurementFrameworkAgreementRules.EffectiveEnd(agreement);
        if (required < agreement.EffectiveFromUtc || required > end)
            throw Validation("FRAMEWORK_CALL_OFF_REQUIRED_DATE_OUTSIDE_AGREEMENT",
                "The required delivery date must fall within the current agreement period.");
    }

    private static DateTime EffectiveAuthorityEnd(
        ProcurementFrameworkAgreement agreement,
        ProcurementFrameworkCallOffAuthority authority) =>
        authority.ValidToUtc == agreement.EffectiveToUtc
            ? ProcurementFrameworkAgreementRules.EffectiveEnd(agreement)
            : authority.ValidToUtc;

    private static void ValidateCreateRequest(
        CreateProcurementFrameworkCallOffRequest request)
    {
        if (request.AgreementId == Guid.Empty ||
            request.SourceRequisitionId == Guid.Empty)
            throw Validation("FRAMEWORK_CALL_OFF_SOURCE_REQUIRED",
                "An effective framework agreement and approved source requisition are required.");
        if (EnsureUtc(request.RequiredDateUtc) < DateTime.UtcNow.Date)
            throw Validation("FRAMEWORK_CALL_OFF_REQUIRED_DATE_INVALID",
                "The required delivery date cannot be in the past.");
        if (request.Lines.Count == 0)
            throw Validation("FRAMEWORK_CALL_OFF_LINES_REQUIRED",
                "At least one call-off line is required.");
    }

    private async Task RecordEventAsync(
        ProcurementFrameworkCallOff callOff,
        ProcurementFrameworkAgreement agreement,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "framework-call-off", callOff.TenantId, callOff.Id,
                $"{action}-{correlationId}"),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = "FRAMEWORK_CALL_OFF",
            RuleId = agreement.Id,
            RuleVersion = agreement.IntegrityHash,
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceTypeName,
            SourceId = callOff.Id,
            SourceReference = callOff.CallOffNumber,
            Reason = Trim(reason, 1000),
            InputValues = new
            {
                callOff.AgreementId,
                callOff.SourceRequisitionId,
                callOff.BusinessPartnerId,
                callOff.AuthorityId,
                callOff.WorkflowDefinitionId,
                callOff.WorkflowInstanceId
            },
            ResultValues = new
            {
                callOff.Status,
                callOff.TotalAmount,
                callOff.CurrencyCode,
                callOff.PurchaseOrderId,
                callOff.RequiredDateUtc,
                callOff.SupplierEligibilityDecisionHash,
                callOff.IntegrityHash
            },
            Before = before,
            After = after,
            CorrelationId = correlationId,
            CausationId = correlationId,
            OccurredAtUtc = occurredAtUtc,
            Evidence = evidence.Select(item =>
                new ProcurementControlEventEvidenceReference
                {
                    ReferenceKind = item.ReferenceKind,
                    ReferenceId = item.ReferenceId,
                    Reference = item.Reference,
                    Label = item.Label,
                    RequirementKey = item.RequirementKey
                }).ToList()
        }, cancellationToken);

    private async Task PublishNotificationAsync(
        string topic,
        ProcurementFrameworkCallOff callOff,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = callOff.TenantId,
                TopicKey = topic,
                NotificationType = "ProcurementFrameworkCallOffControl",
                EntityType = SourceTypeName,
                EntityId = callOff.Id,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["callOffNumber"] = callOff.CallOffNumber,
                    ["agreementNumber"] = callOff.AgreementNumber,
                    ["purchaseOrderId"] = callOff.PurchaseOrderId,
                    ["supplierId"] = callOff.BusinessPartnerId,
                    ["status"] = callOff.Status.ToString(),
                    ["totalAmount"] = callOff.TotalAmount,
                    ["currencyCode"] = callOff.CurrencyCode,
                    ["requiredDateUtc"] = callOff.RequiredDateUtc
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish call-off notification {Topic} for {CallOffId}",
                topic, callOff.Id);
        }
    }

    private async Task PublishExpiryNotificationAsync(
        ProcurementFrameworkAgreementBalance balance,
        ProcurementFrameworkCallOffCommercialRules.FamilySummary familySummary,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = balance.TenantId,
                TopicKey = "procurement.framework-call-off.expiry-warning",
                NotificationType = "ProcurementFrameworkCallOffExpiry",
                EntityType = "ProcurementFrameworkAgreement",
                EntityId = balance.AgreementId,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["agreementNumber"] = balance.Agreement.AgreementNumber,
                    ["availableAmount"] = familySummary.AvailableAmount,
                    ["currencyCode"] = familySummary.CurrencyCode,
                    ["effectiveEndUtc"] = familySummary.EffectiveEndUtc!.Value,
                    ["daysToExpiry"] = DaysToExpiry(
                        familySummary.EffectiveEndUtc.Value,
                        DateTime.UtcNow)
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish framework expiry notification for {AgreementId}",
                balance.AgreementId);
        }
    }

    private static ProcurementFrameworkCallOffListItemDto MapList(
        ProcurementFrameworkCallOff item,
        DateTime now,
        ProcurementFrameworkCallOffCommercialRules.FamilySummary?
            familySummary = null)
    {
        var end = ProcurementFrameworkAgreementRules.EffectiveEnd(item.Agreement);
        var balance = item.Agreement.Balance;
        return new ProcurementFrameworkCallOffListItemDto
        {
            Id = item.Id,
            CallOffNumber = item.CallOffNumber,
            Status = item.Status,
            AgreementId = item.AgreementId,
            AgreementNumber = item.AgreementNumber,
            AgreementVersion = item.AgreementVersion,
            PurchaseOrderId = item.PurchaseOrderId,
            PurchaseOrderNumber = item.PurchaseOrder.OrderNumber,
            PurchaseOrderStatus = item.PurchaseOrder.Status,
            SourceRequisitionId = item.SourceRequisitionId,
            SourceRequisitionNumber = item.SourceRequisition.RequisitionNumber,
            BusinessPartnerId = item.BusinessPartnerId,
            SupplierCode = item.BusinessPartner.PartnerCode,
            SupplierName = item.BusinessPartner.PartnerName,
            CurrencyCode = item.CurrencyCode,
            TotalAmount = item.TotalAmount,
            AgreementCeilingAmount = familySummary?.CeilingAmount ??
                                     balance?.CeilingAmount ??
                                     item.Agreement.CeilingAmount,
            AgreementCommittedAmount = familySummary?.CommittedAmount ??
                                       balance?.CommittedAmount ?? 0m,
            AgreementIssuedAmount = familySummary?.IssuedAmount ??
                                    balance?.IssuedAmount ?? 0m,
            AgreementAvailableAmount = familySummary?.AvailableAmount ??
                                       balance?.AvailableAmount ??
                                       item.Agreement.CeilingAmount,
            RequiredDateUtc = item.RequiredDateUtc,
            AgreementEffectiveEndUtc = end,
            AgreementIsEffective =
                ProcurementFrameworkAgreementRules.IsEffective(item.Agreement, now),
            DaysToExpiry = DaysToExpiry(end, now),
            LineCount = item.Lines.Count(line => !line.IsDeleted),
            CreatedAtUtc = item.CreatedAt,
            SubmittedAtUtc = item.SubmittedAtUtc,
            ApprovedAtUtc = item.ApprovedAtUtc,
            IssuedAtUtc = item.IssuedAtUtc,
            AllowedActions = ProcurementFrameworkCallOffRules.AllowedActions(item),
            RowVersion = Convert.ToBase64String(item.RowVersion ?? Array.Empty<byte>())
        };
    }

    private async Task<ProcurementFrameworkCallOffDto> MapAsync(
        ProcurementFrameworkCallOff item,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var familySummaries = await ResolveRevisionFamilySummariesAsync(
            [item.Agreement],
            now,
            cancellationToken);
        var list = MapList(
            item,
            now,
            familySummaries.GetValueOrDefault(item.AgreementId));
        var ids = item.Lines.Select(line => line.PurchaseRequisitionItemId).ToArray();
        var allocated = ids.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await CallOffLines.GetQueryable(line =>
                    line.TenantId == _currentUser.TenantId && !line.IsDeleted &&
                    ids.Contains(line.PurchaseRequisitionItemId) &&
                    !line.CallOff.IsDeleted &&
                    ActiveDemandStatuses.Contains(line.CallOff.Status))
                .GroupBy(line => line.PurchaseRequisitionItemId)
                .Select(group => new { Id = group.Key, Quantity = group.Sum(line => line.Quantity) })
                .ToDictionaryAsync(value => value.Id, value => value.Quantity, cancellationToken);
        return new ProcurementFrameworkCallOffDto
        {
            Id = list.Id,
            CallOffNumber = list.CallOffNumber,
            Status = list.Status,
            AgreementId = list.AgreementId,
            AgreementNumber = list.AgreementNumber,
            AgreementVersion = list.AgreementVersion,
            PurchaseOrderId = list.PurchaseOrderId,
            PurchaseOrderNumber = list.PurchaseOrderNumber,
            PurchaseOrderStatus = list.PurchaseOrderStatus,
            SourceRequisitionId = list.SourceRequisitionId,
            SourceRequisitionNumber = list.SourceRequisitionNumber,
            BusinessPartnerId = list.BusinessPartnerId,
            SupplierCode = list.SupplierCode,
            SupplierName = list.SupplierName,
            CurrencyCode = list.CurrencyCode,
            TotalAmount = list.TotalAmount,
            AgreementCeilingAmount = list.AgreementCeilingAmount,
            AgreementCommittedAmount = list.AgreementCommittedAmount,
            AgreementIssuedAmount = list.AgreementIssuedAmount,
            AgreementAvailableAmount = list.AgreementAvailableAmount,
            RequiredDateUtc = list.RequiredDateUtc,
            AgreementEffectiveEndUtc = list.AgreementEffectiveEndUtc,
            AgreementIsEffective = list.AgreementIsEffective,
            DaysToExpiry = list.DaysToExpiry,
            LineCount = list.LineCount,
            CreatedAtUtc = list.CreatedAtUtc,
            SubmittedAtUtc = list.SubmittedAtUtc,
            ApprovedAtUtc = list.ApprovedAtUtc,
            IssuedAtUtc = list.IssuedAtUtc,
            AllowedActions = list.AllowedActions,
            RowVersion = list.RowVersion,
            AuthorityId = item.AuthorityId,
            AuthorityKind = item.AuthorityKind,
            AuthorityValue = item.AuthorityValue,
            AuthorityThreshold = item.AuthorityThreshold,
            DeliveryWarehouseId = item.DeliveryWarehouseId,
            DeliveryAddress = item.DeliveryAddress,
            Notes = item.Notes,
            PriceListReference = item.PriceListReference,
            PriceListVersion = item.PriceListVersion,
            AgreementEffectiveFromUtc = item.AgreementEffectiveFromUtc,
            AgreementIntegrityHash = item.AgreementIntegrityHash,
            AwardReadinessIntegrityHash = item.AwardReadinessIntegrityHash,
            SupplierEligibilityDecisionHash = item.SupplierEligibilityDecisionHash,
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowInstanceId = item.WorkflowInstanceId,
            CreatedByUserId = item.CreatedByUserId,
            CreatedByName = item.CreatedByName,
            SubmittedById = item.SubmittedById,
            SubmittedByName = item.SubmittedByName,
            ApprovedById = item.ApprovedById,
            ApprovedByName = item.ApprovedByName,
            IssuedById = item.IssuedById,
            IssuedByName = item.IssuedByName,
            RejectedById = item.RejectedById,
            RejectedByName = item.RejectedByName,
            RejectedAtUtc = item.RejectedAtUtc,
            CancelledById = item.CancelledById,
            CancelledByName = item.CancelledByName,
            CancelledAtUtc = item.CancelledAtUtc,
            DecisionComment = item.DecisionComment,
            IntegrityHash = item.IntegrityHash,
            Lines = item.Lines.Where(line => !line.IsDeleted)
                .OrderBy(line => line.ItemCode)
                .Select(line =>
                {
                    var totalAllocated = allocated.GetValueOrDefault(
                        line.PurchaseRequisitionItemId);
                    return new ProcurementFrameworkCallOffLineDto
                    {
                        Id = line.Id,
                        AgreementPriceLineId = line.AgreementPriceLineId,
                        PurchaseRequisitionItemId = line.PurchaseRequisitionItemId,
                        PurchaseOrderItemId = line.PurchaseOrderItemId,
                        InventoryItemId = line.InventoryItemId,
                        ItemCode = line.ItemCode,
                        ItemName = line.ItemName,
                        UnitOfMeasure = line.UnitOfMeasure,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        LineTotal = line.LineTotal,
                        SourceDemandQuantity = line.SourceDemandQuantity,
                        SourceDemandAllocatedQuantity = totalAllocated,
                        SourceDemandRemainingQuantity = Math.Max(
                            0m, line.SourceDemandQuantity - totalAllocated),
                        PriceIntegrityHash = line.PriceIntegrityHash,
                        IntegrityHash = line.IntegrityHash
                    };
                }).ToList(),
            BalanceMovements = item.BalanceMovements.Where(movement => !movement.IsDeleted)
                .OrderBy(movement => movement.OccurredAtUtc)
                .Select(movement => new ProcurementFrameworkBalanceMovementDto
                {
                    Id = movement.Id,
                    MovementType = movement.MovementType,
                    Amount = movement.Amount,
                    BalanceBefore = movement.BalanceBefore,
                    BalanceAfter = movement.BalanceAfter,
                    ActorUserId = movement.ActorUserId,
                    ActorName = movement.ActorName,
                    OccurredAtUtc = movement.OccurredAtUtc,
                    CorrelationId = movement.CorrelationId,
                    IntegrityHash = movement.IntegrityHash
                }).ToList()
        };
    }

    private static object Snapshot(ProcurementFrameworkCallOff item) => new
    {
        item.Id,
        item.TenantId,
        item.CallOffNumber,
        item.Status,
        item.AgreementId,
        item.PurchaseOrderId,
        item.SourceRequisitionId,
        item.BusinessPartnerId,
        item.AuthorityId,
        item.AuthorityKind,
        item.AuthorityValue,
        item.AuthorityThreshold,
        item.CurrencyCode,
        item.TotalAmount,
        item.RequiredDateUtc,
        item.DeliveryWarehouseId,
        item.AgreementNumber,
        item.AgreementVersion,
        item.AgreementIntegrityHash,
        item.AwardReadinessIntegrityHash,
        item.SupplierEligibilityDecisionHash,
        item.PriceListReference,
        item.PriceListVersion,
        item.AgreementEffectiveFromUtc,
        item.AgreementEffectiveEndUtc,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.CreatedByUserId,
        item.SubmittedById,
        item.ApprovedById,
        item.BalanceDeductedAtUtc,
        item.IssuedById,
        item.RejectedById,
        item.CancelledById,
        Lines = item.Lines.Where(line => !line.IsDeleted)
            .OrderBy(line => line.ItemCode)
            .Select(line => new
            {
                line.Id,
                line.AgreementPriceLineId,
                line.PurchaseRequisitionItemId,
                line.PurchaseOrderItemId,
                line.InventoryItemId,
                line.ItemCode,
                line.UnitOfMeasure,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal,
                line.SourceDemandQuantity,
                line.PriceIntegrityHash,
                line.IntegrityHash
            })
    };

    private async Task EnforcePurchaseOrderComplianceAsync(
        PurchaseOrder purchaseOrder,
        string action,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _purchaseOrderCompliance.EnforceAsync(
                purchaseOrder,
                action,
                correlationId,
                cancellationToken);
        }
        catch (ProcurementPurchaseOrderComplianceBlockedException exception)
        {
            throw Conflict(exception.Code, exception.Message);
        }
        catch (ProcurementPurchaseOrderComplianceAuthorizationException exception)
        {
            throw Authorization(exception.Message);
        }
    }

    private static void Capture(ProcurementFrameworkCallOff item)
    {
        item.SnapshotJson = Serialize(Snapshot(item));
        item.IntegrityHash = Hash(item.SnapshotJson);
    }

    private static void Capture(ProcurementFrameworkCallOffLine item)
    {
        item.IntegrityHash = Hash(Serialize(new
        {
            item.TenantId,
            item.CallOffId,
            item.AgreementPriceLineId,
            item.PurchaseRequisitionItemId,
            item.PurchaseOrderItemId,
            item.InventoryItemId,
            item.ItemCode,
            item.ItemName,
            item.UnitOfMeasure,
            item.Quantity,
            item.UnitPrice,
            item.LineTotal,
            item.SourceDemandQuantity,
            item.PriceIntegrityHash
        }));
    }

    private static void Capture(ProcurementFrameworkAgreementBalance item)
    {
        item.IntegrityHash = Hash(Serialize(new
        {
            item.TenantId,
            item.AgreementId,
            item.CurrencyCode,
            item.CeilingAmount,
            item.CommittedAmount,
            item.IssuedAmount,
            item.AvailableAmount,
            item.LastMovementId,
            item.LastMovementAtUtc,
            item.LastExpiryAlertAtUtc
        }));
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw Authorization(
                "External portal users cannot administer framework call-offs.");
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = SourceTypeName,
                SourceReference = sourceReference
            }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw Authorization(decision.Message);
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw Authorization(
                "External portal users cannot access framework call-offs.");
        if (IsAdministrator() ||
            _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role =>
                ProcurementAccessControlRegistry.FindRole(role) is not null))
            return;
        throw Authorization("A TDC procurement or internal-audit role is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw Authorization("An authenticated tenant context is required.");
    }

    private bool IsAdministrator() =>
        _currentUser.HasRole("Admin") ||
        _currentUser.HasRole("Administrator") ||
        _currentUser.HasRole("SuperAdmin") ||
        _currentUser.HasRole("TenantAdmin");

    private async Task ExecuteAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            try
            {
                await action();
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private static void EnsureStatus(
        ProcurementFrameworkCallOff item,
        ProcurementFrameworkCallOffStatus expected,
        string message)
    {
        if (item.Status != expected)
            throw Conflict("FRAMEWORK_CALL_OFF_STATUS_INVALID", message);
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException)
        {
            throw Validation("FRAMEWORK_CALL_OFF_ROW_VERSION_INVALID",
                "The row version is invalid.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict("FRAMEWORK_CALL_OFF_CONCURRENCY_CONFLICT",
                "The call-off changed; reload it before continuing.");
    }

    private static void EnsureEvidence(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence)
    {
        if (evidence.Count == 0 || evidence.Any(item =>
                string.IsNullOrWhiteSpace(item.Reference) &&
                (!item.ReferenceId.HasValue || item.ReferenceId == Guid.Empty)))
            throw Validation("FRAMEWORK_CALL_OFF_EVIDENCE_REQUIRED",
                "Submission, decision, issue, and cancellation require retained shared evidence.");
    }

    private static bool IsReplay(
        ProcurementFrameworkCallOff item,
        string operation,
        string correlationId) =>
        string.Equals(item.LastOperation, operation, StringComparison.Ordinal) &&
        string.Equals(
            item.LastOperationCorrelationId, correlationId, StringComparison.Ordinal);

    private static bool RoleAuthorityMatches(string role, string authorityValue)
    {
        if (string.Equals(role.Trim(), authorityValue.Trim(),
                StringComparison.OrdinalIgnoreCase))
            return true;
        var definition = ProcurementAccessControlRegistry.FindRole(role);
        if (definition is null) return false;
        var authority = NormalizeAuthorityLabel(authorityValue);
        return string.Equals(
                   NormalizeAuthorityLabel(definition.Code), authority,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   NormalizeAuthorityLabel(definition.Name), authority,
                   StringComparison.OrdinalIgnoreCase) ||
               NormalizeAuthorityLabel(definition.Name)
                   .EndsWith(authority, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAuthorityLabel(string value)
    {
        var normalized = new string(value.Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant).ToArray());
        return normalized.StartsWith("TDC", StringComparison.Ordinal)
            ? normalized[3..]
            : normalized;
    }

    private string ActorName => string.IsNullOrWhiteSpace(_currentUser.FullName)
        ? _currentUser.Username
        : _currentUser.FullName;

    private static string NormalizeCorrelation(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw Validation("FRAMEWORK_CALL_OFF_CORRELATION_REQUIRED",
                "X-Correlation-ID is required.");
        return correlationId.Trim().Length <= 100
            ? correlationId.Trim()
            : Hash(correlationId);
    }

    private static string NormalizeUnit(string value) =>
        value.Trim().Replace(" ", string.Empty).ToUpperInvariant();
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal SingleCurrencyTotal(
        IReadOnlyDictionary<string, decimal> values) =>
        values.Count == 1 ? values.Values.Single() : 0m;
    private static int DaysToExpiry(DateTime end, DateTime now) =>
        Math.Max(0, (int)Math.Ceiling((end - now).TotalDays));
    private static string Serialize(object value) =>
        JsonSerializer.Serialize(value, JsonOptions);
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Trim(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value) ? null :
        value.Trim().Length <= maximum ? value.Trim() : value.Trim()[..maximum];
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private static ProcurementFrameworkCallOffNotFoundException NotFound(
        string code,
        string message) => new(code, message);
    private static ProcurementFrameworkCallOffValidationException Validation(
        string code,
        string message) => new(code, message);
    private static ProcurementFrameworkCallOffConflictException Conflict(
        string code,
        string message) => new(code, message);
    private static ProcurementFrameworkCallOffAuthorizationException Authorization(
        string message) => new(message);

    private static readonly ProcurementFrameworkCallOffStatus[] ActiveDemandStatuses =
    [
        ProcurementFrameworkCallOffStatus.Draft,
        ProcurementFrameworkCallOffStatus.PendingApproval,
        ProcurementFrameworkCallOffStatus.Approved,
        ProcurementFrameworkCallOffStatus.Issued
    ];

    private static readonly HashSet<string> OrganizationalUnitClaimKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "organizational_unit",
            "organization_unit",
            "org_unit",
            "orgUnit",
            "department",
            "department_id"
        };

    private sealed class AutomaticApprovalDetectedException : Exception
    {
    }

    private sealed record ResolvedLine(
        PurchaseRequisitionItem DemandLine,
        ProcurementFrameworkPriceListLine PriceLine,
        decimal Quantity,
        decimal LineTotal);
}

public static class ProcurementFrameworkCallOffRules
{
    public static IReadOnlyList<string> AllowedActions(
        ProcurementFrameworkCallOff item) =>
        item.Status switch
        {
            ProcurementFrameworkCallOffStatus.Draft =>
                ["submit", "cancel"],
            ProcurementFrameworkCallOffStatus.PendingApproval =>
                ["approve", "reject", "cancel"],
            ProcurementFrameworkCallOffStatus.Approved =>
                ["issue", "cancel"],
            _ => Array.Empty<string>()
        };
}
