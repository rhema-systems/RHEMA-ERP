using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public partial class EmergencyProcurementPlanService : IEmergencyProcurementPlanService
{
    private readonly IEmergencyProcurementPlanRepository _planRepository;
    private readonly IEmergencyProcurementItemRepository _itemRepository;
    private readonly IEmergencySupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<EmergencyProcurementPlanService> _logger;

    public EmergencyProcurementPlanService(
        IEmergencyProcurementPlanRepository planRepository,
        IEmergencyProcurementItemRepository itemRepository,
        IEmergencySupplierRepository supplierRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        IProcurementSodGuardService sodGuard,
        IWorkflowService workflowService,
        ILogger<EmergencyProcurementPlanService> logger)
    {
        _planRepository = planRepository;
        _itemRepository = itemRepository;
        _supplierRepository = supplierRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _sodGuard = sodGuard;
        _workflowService = workflowService;
        _logger = logger;
    }

    public async Task<EmergencyProcurementPlanDetailDto?> GetByIdAsync(Guid id)
    {
        await EnsureEmergencyCapabilityAsync(ReadPermission, id.ToString("N"), NewCorrelation(), CancellationToken.None);
        var plan = await _planRepository.GetWithFullDetailsAsync(id);
        return plan == null ? null : MapToDetailDto(plan);
    }

    public async Task<EmergencyProcurementPlanDto?> GetByPlanNumberAsync(string planNumber)
    {
        await EnsureEmergencyCapabilityAsync(ReadPermission, planNumber, NewCorrelation(), CancellationToken.None);
        var plan = await _planRepository.GetByPlanCodeAsync(planNumber);
        return plan == null ? null : MapToDto(plan);
    }

    public async Task<PagedResult<EmergencyProcurementPlanDto>> GetPlansAsync(
        int page, int pageSize, string? search = null, string? status = null, string? emergencyType = null)
    {
        await EnsureEmergencyCapabilityAsync(ReadPermission, "list", NewCorrelation(), CancellationToken.None);
        var result = await _planRepository.GetPlansAsync(page, pageSize, search, status, null, emergencyType);
        return new PagedResult<EmergencyProcurementPlanDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IEnumerable<EmergencyProcurementPlanDto>> GetByEmergencyTypeAsync(string emergencyType)
    {
        await EnsureEmergencyCapabilityAsync(ReadPermission, emergencyType, NewCorrelation(), CancellationToken.None);
        var plans = await _planRepository.GetByEmergencyTypeAsync(emergencyType);
        return plans.Select(MapToDto);
    }

    public async Task<IEnumerable<EmergencyProcurementPlanDto>> GetActivePlansAsync()
    {
        await EnsureEmergencyCapabilityAsync(ReadPermission, "active", NewCorrelation(), CancellationToken.None);
        var plans = await _planRepository.GetActivePlansAsync();
        return plans.Select(MapToDto);
    }

    public async Task<IEnumerable<EmergencyProcurementPlanDto>> GetTriggeredPlansAsync()
    {
        await EnsureEmergencyCapabilityAsync(ReadPermission, "triggered", NewCorrelation(), CancellationToken.None);
        var result = await _planRepository.GetPlansAsync(1, 1000, null, "Triggered", null, null);
        return result.Items.Select(MapToDto);
    }

    public async Task<EmergencyProcurementPlanDetailDto> CreateAsync(CreateEmergencyProcurementPlanDto dto)
    {
        await EnsureEmergencyCapabilityAsync(ManagePermission, "new", NewCorrelation(), CancellationToken.None);
        var planCode = await _planRepository.GeneratePlanCodeAsync();
        var plan = new EmergencyProcurementPlan
        {
            PlanCode = planCode,
            Title = dto.Title,
            Description = dto.Description,
            DepartmentId = dto.DepartmentId,
            EmergencyType = dto.EmergencyType,
            CriticalityLevel = dto.CriticalityLevel,
            BudgetReserve = dto.BudgetReserve,
            Currency = dto.Currency,
            MaxApprovalLimit = dto.MaxApprovalLimit,
            RapidProcurementProcess = dto.RapidProcurementProcess,
            EscalationContacts = dto.EscalationContacts,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            NextReviewDate = dto.NextReviewDate,
            Notes = dto.Notes,
            Status = "Draft",
            TenantId = _currentUserProvider.TenantId
        };

        await _planRepository.AddAsync(plan);

        foreach (var itemDto in dto.CriticalItems)
        {
            var item = new EmergencyProcurementItem
            {
                EmergencyProcurementPlanId = plan.Id,
                ItemDescription = itemDto.ItemDescription,
                Specifications = itemDto.Specifications,
                ItemCategory = itemDto.ItemCategory,
                MinimumStockLevel = itemDto.MinimumStockLevel,
                CurrentStockLevel = itemDto.CurrentStockLevel,
                EmergencyOrderQuantity = itemDto.EmergencyOrderQuantity,
                UnitOfMeasure = itemDto.UnitOfMeasure,
                MaxLeadTimeDays = itemDto.MaxLeadTimeDays,
                CriticalityLevel = itemDto.CriticalityLevel,
                AlternativeItems = itemDto.AlternativeItems,
                Notes = itemDto.Notes,
                TenantId = _currentUserProvider.TenantId
            };
            await _itemRepository.AddAsync(item);
        }

        foreach (var supplierDto in dto.EmergencySuppliers)
        {
            var supplier = new EmergencySupplier
            {
                EmergencyProcurementPlanId = plan.Id,
                SupplierId = supplierDto.SupplierId,
                SupplierName = supplierDto.SupplierName,
                ContactPerson = supplierDto.ContactPerson,
                ContactPhone = supplierDto.ContactPhone,
                ContactEmail = supplierDto.ContactEmail,
                Address = supplierDto.Address,
                ItemsProvided = supplierDto.ItemsProvided,
                ResponseTimeHours = supplierDto.ResponseTimeHours,
                Priority = supplierDto.Priority,
                HasEmergencyContract = supplierDto.HasEmergencyContract,
                ContractExpiryDate = supplierDto.ContractExpiryDate,
                PaymentTerms = supplierDto.PaymentTerms,
                Notes = supplierDto.Notes,
                IsActive = true,
                TenantId = _currentUserProvider.TenantId
            };
            await _supplierRepository.AddAsync(supplier);
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Created emergency procurement plan {PlanCode}", planCode);

        return await GetByIdAsync(plan.Id) ?? throw new InvalidOperationException("Failed to retrieve created plan");
    }

    public async Task<EmergencyProcurementPlanDetailDto> UpdateAsync(Guid id, CreateEmergencyProcurementPlanDto dto)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan == null) throw new KeyNotFoundException($"Emergency plan with ID {id} not found");
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        EnsureDraft(plan);

        plan.Title = dto.Title;
        plan.Description = dto.Description;
        plan.DepartmentId = dto.DepartmentId;
        plan.EmergencyType = dto.EmergencyType;
        plan.CriticalityLevel = dto.CriticalityLevel;
        plan.BudgetReserve = dto.BudgetReserve;
        plan.Currency = dto.Currency;
        plan.MaxApprovalLimit = dto.MaxApprovalLimit;
        plan.RapidProcurementProcess = dto.RapidProcurementProcess;
        plan.EscalationContacts = dto.EscalationContacts;
        plan.EffectiveDate = dto.EffectiveDate;
        plan.ExpiryDate = dto.ExpiryDate;
        plan.NextReviewDate = dto.NextReviewDate;
        plan.Notes = dto.Notes;
        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated plan");
    }

    public async Task DeleteAsync(Guid id)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan == null) throw new KeyNotFoundException($"Emergency plan with ID {id} not found");
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        EnsureDraft(plan);

        plan.IsDeleted = true;
        plan.UpdatedAt = DateTime.UtcNow;
        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<EmergencyProcurementPlanDetailDto> ActivateAsync(Guid id)
    {
        var plan = await RequirePlanAsync(id, CancellationToken.None);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        return await RejectDirectLifecycleAsync(plan, "EmergencyDirectActivationDenied",
            "EMERGENCY_DIRECT_ACTIVATION_DISABLED",
            "Direct activation is disabled. Prepare the requisition exception, obtain Internal Audit vouching, and complete the configured MD/Board workflow.");
    }

    public async Task<EmergencyProcurementPlanDetailDto> TriggerAsync(Guid id)
    {
        var plan = await RequirePlanAsync(id, CancellationToken.None);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        return await RejectDirectLifecycleAsync(plan, "EmergencyDirectTriggerDenied",
            "EMERGENCY_DIRECT_TRIGGER_DISABLED",
            "Direct triggering is disabled. Only an approved governed emergency-purchase exception can be triggered.");
    }

    public async Task<EmergencyProcurementPlanDetailDto> DeactivateAsync(Guid id)
    {
        var plan = await RequirePlanAsync(id, CancellationToken.None);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        return await RejectDirectLifecycleAsync(plan, "EmergencyDirectDeactivationDenied",
            "EMERGENCY_DIRECT_DEACTIVATION_DISABLED",
            "Direct deactivation is disabled because approved emergency-purchase lineage is immutable.");
    }

    public async Task<EmergencyProcurementItemDto> AddItemAsync(Guid planId, CreateEmergencyProcurementItemDto dto)
    {
        var plan = await RequirePlanAsync(planId, CancellationToken.None);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        EnsureDraft(plan);
        var item = new EmergencyProcurementItem
        {
            EmergencyProcurementPlanId = planId,
            ItemDescription = dto.ItemDescription,
            Specifications = dto.Specifications,
            ItemCategory = dto.ItemCategory,
            MinimumStockLevel = dto.MinimumStockLevel,
            CurrentStockLevel = dto.CurrentStockLevel,
            EmergencyOrderQuantity = dto.EmergencyOrderQuantity,
            UnitOfMeasure = dto.UnitOfMeasure,
            MaxLeadTimeDays = dto.MaxLeadTimeDays,
            CriticalityLevel = dto.CriticalityLevel,
            AlternativeItems = dto.AlternativeItems,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };
        await _itemRepository.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();
        return MapToItemDto(item);
    }

    public async Task<EmergencyProcurementItemDto> UpdateItemAsync(Guid itemId, CreateEmergencyProcurementItemDto dto)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item == null) throw new KeyNotFoundException($"Item with ID {itemId} not found");
        var plan = await RequirePlanAsync(item.EmergencyProcurementPlanId, CancellationToken.None);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        EnsureDraft(plan);

        item.ItemDescription = dto.ItemDescription;
        item.Specifications = dto.Specifications;
        item.ItemCategory = dto.ItemCategory;
        item.MinimumStockLevel = dto.MinimumStockLevel;
        item.CurrentStockLevel = dto.CurrentStockLevel;
        item.EmergencyOrderQuantity = dto.EmergencyOrderQuantity;
        item.UnitOfMeasure = dto.UnitOfMeasure;
        item.MaxLeadTimeDays = dto.MaxLeadTimeDays;
        item.CriticalityLevel = dto.CriticalityLevel;
        item.AlternativeItems = dto.AlternativeItems;
        item.Notes = dto.Notes;
        item.UpdatedAt = DateTime.UtcNow;

        await _itemRepository.UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync();
        return MapToItemDto(item);
    }

    public async Task DeleteItemAsync(Guid itemId)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item == null) throw new KeyNotFoundException($"Item with ID {itemId} not found");
        var plan = await RequirePlanAsync(item.EmergencyProcurementPlanId, CancellationToken.None);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        EnsureDraft(plan);

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _itemRepository.UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<EmergencyProcurementItemDto>> GetCriticalItemsAsync(Guid planId)
    {
        await EnsureEmergencyCapabilityAsync(ReadPermission, planId.ToString("N"), NewCorrelation(), CancellationToken.None);
        var items = await _itemRepository.GetCriticalItemsAsync(planId);
        return items.Select(MapToItemDto);
    }

    public async Task<EmergencySupplierDto> AddSupplierAsync(Guid planId, CreateEmergencySupplierDto dto)
    {
        var plan = await RequirePlanAsync(planId, CancellationToken.None);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        EnsureDraft(plan);
        var supplier = new EmergencySupplier
        {
            EmergencyProcurementPlanId = planId,
            SupplierId = dto.SupplierId,
            SupplierName = dto.SupplierName,
            ContactPerson = dto.ContactPerson,
            ContactPhone = dto.ContactPhone,
            ContactEmail = dto.ContactEmail,
            Address = dto.Address,
            ItemsProvided = dto.ItemsProvided,
            ResponseTimeHours = dto.ResponseTimeHours,
            Priority = dto.Priority,
            HasEmergencyContract = dto.HasEmergencyContract,
            ContractExpiryDate = dto.ContractExpiryDate,
            PaymentTerms = dto.PaymentTerms,
            Notes = dto.Notes,
            IsActive = true,
            TenantId = _currentUserProvider.TenantId
        };
        await _supplierRepository.AddAsync(supplier);
        await _unitOfWork.SaveChangesAsync();
        return MapToSupplierDto(supplier);
    }

    public async Task<EmergencySupplierDto> UpdateSupplierAsync(Guid supplierId, CreateEmergencySupplierDto dto)
    {
        var supplier = await _supplierRepository.GetByIdAsync(supplierId);
        if (supplier == null) throw new KeyNotFoundException($"Supplier with ID {supplierId} not found");
        var plan = await RequirePlanAsync(supplier.EmergencyProcurementPlanId, CancellationToken.None);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        EnsureDraft(plan);

        supplier.SupplierId = dto.SupplierId;
        supplier.SupplierName = dto.SupplierName;
        supplier.ContactPerson = dto.ContactPerson;
        supplier.ContactPhone = dto.ContactPhone;
        supplier.ContactEmail = dto.ContactEmail;
        supplier.Address = dto.Address;
        supplier.ItemsProvided = dto.ItemsProvided;
        supplier.ResponseTimeHours = dto.ResponseTimeHours;
        supplier.Priority = dto.Priority;
        supplier.HasEmergencyContract = dto.HasEmergencyContract;
        supplier.ContractExpiryDate = dto.ContractExpiryDate;
        supplier.PaymentTerms = dto.PaymentTerms;
        supplier.Notes = dto.Notes;
        supplier.UpdatedAt = DateTime.UtcNow;

        await _supplierRepository.UpdateAsync(supplier);
        await _unitOfWork.SaveChangesAsync();
        return MapToSupplierDto(supplier);
    }

    public async Task DeleteSupplierAsync(Guid supplierId)
    {
        var supplier = await _supplierRepository.GetByIdAsync(supplierId);
        if (supplier == null) throw new KeyNotFoundException($"Supplier with ID {supplierId} not found");
        var plan = await RequirePlanAsync(supplier.EmergencyProcurementPlanId, CancellationToken.None);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, NewCorrelation(), CancellationToken.None);
        EnsureDraft(plan);

        supplier.IsDeleted = true;
        supplier.UpdatedAt = DateTime.UtcNow;
        await _supplierRepository.UpdateAsync(supplier);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<EmergencySupplierDto>> GetActiveSuppliersAsync(Guid planId)
    {
        await EnsureEmergencyCapabilityAsync(ReadPermission, planId.ToString("N"), NewCorrelation(), CancellationToken.None);
        var suppliers = await _supplierRepository.GetActiveSuppliersByPlanIdAsync(planId);
        return suppliers.Select(MapToSupplierDto);
    }

    public async Task<IEnumerable<EmergencySupplierDto>> GetSuppliersByCategoryAsync(string category)
    {
        await EnsureEmergencyCapabilityAsync(ReadPermission, category, NewCorrelation(), CancellationToken.None);
        // Get all suppliers and filter by items provided
        var allPlans = await _planRepository.GetActivePlansAsync();
        var allSuppliers = new List<EmergencySupplier>();
        foreach (var plan in allPlans)
        {
            var suppliers = await _supplierRepository.GetByPlanIdAsync(plan.Id);
            allSuppliers.AddRange(suppliers.Where(s => s.ItemsProvided?.Contains(category) == true));
        }
        return allSuppliers.Select(MapToSupplierDto);
    }

    #region Mapping Methods

    private static EmergencyProcurementPlanDto MapToDto(EmergencyProcurementPlan plan)
    {
        return new EmergencyProcurementPlanDto
        {
            Id = plan.Id,
            PlanCode = plan.PlanCode,
            Title = plan.Title,
            Description = plan.Description,
            DepartmentId = plan.DepartmentId,
            DepartmentName = plan.Department?.Name,
            EmergencyType = plan.EmergencyType,
            CriticalityLevel = plan.CriticalityLevel,
            BudgetReserve = plan.BudgetReserve,
            UtilizedReserve = plan.UtilizedReserve,
            RemainingReserve = plan.BudgetReserve - plan.UtilizedReserve,
            Currency = plan.Currency,
            MaxApprovalLimit = plan.MaxApprovalLimit,
            EffectiveDate = plan.EffectiveDate,
            ExpiryDate = plan.ExpiryDate,
            LastReviewDate = plan.LastReviewDate,
            NextReviewDate = plan.NextReviewDate,
            ApprovedByName = plan.ApprovedBy?.FullName,
            ApprovedDate = plan.ApprovedDate,
            Status = plan.Status,
            CriticalItemCount = plan.CriticalItems?.Count(i => !i.IsDeleted) ?? 0,
            EmergencySupplierCount = plan.EmergencySuppliers?.Count(s => !s.IsDeleted) ?? 0,
            CreatedAt = plan.CreatedAt
            ,PurchaseRequisitionId = plan.PurchaseRequisitionId
            ,PurchaseRequisitionNumber = plan.PurchaseRequisition?.RequisitionNumber
            ,ExceptionRuleId = plan.ExceptionRuleId
            ,ExceptionRuleCode = plan.ExceptionRule?.RuleCode
            ,ExceptionRuleName = plan.ExceptionRule?.ExceptionName
            ,WorkflowInstanceId = plan.WorkflowInstanceId
            ,CentralDocumentVersionId = plan.CentralDocumentVersionId
            ,EvidenceReference = plan.EvidenceReference
            ,ApprovalAuthority = plan.ApprovalAuthority
            ,ApprovalReference = plan.ApprovalReference
            ,InternalAuditVouchedAtUtc = plan.InternalAuditVouchedAtUtc
            ,SubmittedForApprovalAtUtc = plan.SubmittedForApprovalAtUtc
            ,ExceptionalSourcingTenderId = plan.ExceptionalSourcingTenderId
            ,FiledAtUtc = plan.FiledAtUtc
            ,RowVersion = Convert.ToBase64String(plan.RowVersion)
        };
    }

    private static EmergencyProcurementPlanDetailDto MapToDetailDto(EmergencyProcurementPlan plan)
    {
        return new EmergencyProcurementPlanDetailDto
        {
            Id = plan.Id,
            PlanCode = plan.PlanCode,
            Title = plan.Title,
            Description = plan.Description,
            DepartmentId = plan.DepartmentId,
            DepartmentName = plan.Department?.Name,
            EmergencyType = plan.EmergencyType,
            CriticalityLevel = plan.CriticalityLevel,
            BudgetReserve = plan.BudgetReserve,
            UtilizedReserve = plan.UtilizedReserve,
            RemainingReserve = plan.BudgetReserve - plan.UtilizedReserve,
            Currency = plan.Currency,
            MaxApprovalLimit = plan.MaxApprovalLimit,
            EffectiveDate = plan.EffectiveDate,
            ExpiryDate = plan.ExpiryDate,
            LastReviewDate = plan.LastReviewDate,
            NextReviewDate = plan.NextReviewDate,
            ApprovedByName = plan.ApprovedBy?.FullName,
            ApprovedDate = plan.ApprovedDate,
            Status = plan.Status,
            CriticalItemCount = plan.CriticalItems?.Count(i => !i.IsDeleted) ?? 0,
            EmergencySupplierCount = plan.EmergencySuppliers?.Count(s => !s.IsDeleted) ?? 0,
            CreatedAt = plan.CreatedAt,
            ApprovedById = plan.ApprovedById,
            RapidProcurementProcess = plan.RapidProcurementProcess,
            EscalationContacts = plan.EscalationContacts,
            Notes = plan.Notes,
            PurchaseRequisitionId = plan.PurchaseRequisitionId,
            PurchaseRequisitionNumber = plan.PurchaseRequisition?.RequisitionNumber,
            ExceptionRuleId = plan.ExceptionRuleId,
            ExceptionRuleCode = plan.ExceptionRule?.RuleCode,
            ExceptionRuleName = plan.ExceptionRule?.ExceptionName,
            WorkflowInstanceId = plan.WorkflowInstanceId,
            CentralDocumentVersionId = plan.CentralDocumentVersionId,
            EvidenceReference = plan.EvidenceReference,
            ApprovalAuthority = plan.ApprovalAuthority,
            ApprovalReference = plan.ApprovalReference,
            InternalAuditVouchedAtUtc = plan.InternalAuditVouchedAtUtc,
            SubmittedForApprovalAtUtc = plan.SubmittedForApprovalAtUtc,
            ExceptionalSourcingTenderId = plan.ExceptionalSourcingTenderId,
            FiledAtUtc = plan.FiledAtUtc,
            RowVersion = Convert.ToBase64String(plan.RowVersion),
            ExceptionJustification = plan.ExceptionJustification,
            InternalAuditVouchNote = plan.InternalAuditVouchNote,
            PostAwardJustification = plan.PostAwardJustification,
            PostAwardCentralDocumentVersionId = plan.PostAwardCentralDocumentVersionId,
            PostAwardEvidenceReference = plan.PostAwardEvidenceReference,
            CriticalItems = plan.CriticalItems?.Where(i => !i.IsDeleted).Select(MapToItemDto).ToList() ?? new(),
            EmergencySuppliers = plan.EmergencySuppliers?.Where(s => !s.IsDeleted).Select(MapToSupplierDto).ToList() ?? new()
        };
    }

    private static EmergencyProcurementItemDto MapToItemDto(EmergencyProcurementItem item)
    {
        return new EmergencyProcurementItemDto
        {
            Id = item.Id,
            EmergencyProcurementPlanId = item.EmergencyProcurementPlanId,
            ItemDescription = item.ItemDescription,
            Specifications = item.Specifications,
            ItemCategory = item.ItemCategory,
            MinimumStockLevel = item.MinimumStockLevel,
            CurrentStockLevel = item.CurrentStockLevel,
            EmergencyOrderQuantity = item.EmergencyOrderQuantity,
            UnitOfMeasure = item.UnitOfMeasure,
            MaxLeadTimeDays = item.MaxLeadTimeDays,
            CriticalityLevel = item.CriticalityLevel,
            AlternativeItems = item.AlternativeItems,
            Notes = item.Notes,
            IsStockLow = item.CurrentStockLevel < item.MinimumStockLevel
        };
    }

    private static EmergencySupplierDto MapToSupplierDto(EmergencySupplier supplier)
    {
        return new EmergencySupplierDto
        {
            Id = supplier.Id,
            EmergencyProcurementPlanId = supplier.EmergencyProcurementPlanId,
            SupplierId = supplier.SupplierId,
            SupplierName = supplier.SupplierName,
            ContactPerson = supplier.ContactPerson,
            ContactPhone = supplier.ContactPhone,
            ContactEmail = supplier.ContactEmail,
            Address = supplier.Address,
            ItemsProvided = supplier.ItemsProvided,
            ResponseTimeHours = supplier.ResponseTimeHours,
            Priority = supplier.Priority,
            HasEmergencyContract = supplier.HasEmergencyContract,
            ContractExpiryDate = supplier.ContractExpiryDate,
            PaymentTerms = supplier.PaymentTerms,
            LastVerifiedDate = supplier.LastVerifiedDate,
            IsActive = supplier.IsActive,
            Notes = supplier.Notes
        };
    }

    #endregion
}
