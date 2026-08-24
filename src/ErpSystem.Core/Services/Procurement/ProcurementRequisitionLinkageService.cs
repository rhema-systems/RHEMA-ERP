using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementRequisitionLinkageService : IProcurementRequisitionLinkageService
{
    private const string SourceType = "PurchaseRequisition";
    private const string EventType = "PurchaseRequisitionLinkage";
    private const string CreatePermission = "procurement.requisition.create";
    private const string ExportPermission = "procurement.reports.export";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;

    public ProcurementRequisitionLinkageService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
    }

    private IGenericRepository<PurchaseRequisition> Requisitions => _unitOfWork.Repository<PurchaseRequisition>();
    private IGenericRepository<ProcurementPlanItem> PlanItems => _unitOfWork.Repository<ProcurementPlanItem>();
    private IGenericRepository<ProcurementBudget> Budgets => _unitOfWork.Repository<ProcurementBudget>();
    private IGenericRepository<Project> Projects => _unitOfWork.Repository<Project>();
    private IGenericRepository<ProcurementSpecificationTemplate> Templates => _unitOfWork.Repository<ProcurementSpecificationTemplate>();
    private IGenericRepository<ProcurementPolicyExceptionRule> ExceptionRules => _unitOfWork.Repository<ProcurementPolicyExceptionRule>();
    private IGenericRepository<WorkflowInstance> WorkflowInstances => _unitOfWork.Repository<WorkflowInstance>();
    private IGenericRepository<WorkflowActivityLog> WorkflowActivities => _unitOfWork.Repository<WorkflowActivityLog>();
    private IGenericRepository<ProcurementControlEvent> Events => _unitOfWork.Repository<ProcurementControlEvent>();

    public async Task<PurchaseRequisitionLinkageOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var tenantId = _currentUser.TenantId;
        var now = DateTime.UtcNow;

        var planItemEntities = await PlanItems.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                (item.Status == "Approved" || item.Status == "Planned") && !item.ProcurementPlan.IsDeleted &&
                (item.ProcurementPlan.Status == "Approved" || item.ProcurementPlan.Status == "Active"))
            .Include(item => item.ProcurementBudget)
            .Include(item => item.ProcurementPlan).ThenInclude(plan => plan.Department)
            .Include(item => item.ProcurementPlan).ThenInclude(plan => plan.Budget)
            .AsNoTracking()
            .OrderByDescending(item => item.ProcurementPlan.FiscalYear).ThenBy(item => item.ProcurementPlan.PlanNumber)
            .ThenBy(item => item.ItemDescription).Take(500)
            .ToListAsync(cancellationToken);
        var planItems = planItemEntities
            .Where(item => item.ProcurementBudgetId.HasValue || item.ProcurementPlan.BudgetId.HasValue)
            .Select(item => new PurchaseRequisitionLinkageOptionDto
            {
                Id = item.Id,
                Code = item.ProcurementPlan.PlanNumber,
                Name = item.ItemDescription,
                Status = $"{item.ProcurementPlan.Status}/{item.Status}",
                ParentId = item.ProcurementPlanId,
                ParentReference = item.ProcurementPlan.Title,
                Category = item.ItemCategory,
                Amount = item.ApprovedBudgetAmount ?? item.EstimatedTotalCost,
                // The approved budget is the accounting authority for a planned
                // requisition. Do not expose a stale plan-item default such as USD.
                Currency = item.ProcurementBudget?.Currency ?? item.ProcurementPlan.Budget?.Currency,
                BudgetId = item.ProcurementBudgetId ?? item.ProcurementPlan.BudgetId,
                BudgetCode = item.ProcurementBudget?.BudgetCode ?? item.ProcurementPlan.Budget?.BudgetCode,
                DepartmentId = item.ProcurementPlan.DepartmentId,
                DepartmentName = item.ProcurementPlan.Department.Name,
                InventoryItemId = item.InventoryItemId,
                Quantity = item.EstimatedQuantity,
                UnitOfMeasure = item.UnitOfMeasure,
                UnitPrice = item.EstimatedUnitPrice,
                RequiredDate = item.RequiredDate,
                Specifications = item.Specifications,
                PreferredSupplierId = item.PreferredSupplierId
            }).ToList();

        var budgets = await Budgets.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted && item.Status != "Closed")
            .AsNoTracking().OrderByDescending(item => item.FiscalYear).ThenBy(item => item.BudgetCode).Take(500)
            .Select(item => new PurchaseRequisitionLinkageOptionDto
            {
                Id = item.Id,
                Code = item.BudgetCode,
                Name = item.Title,
                Status = item.Status,
                ParentId = item.ProcurementPlanId,
                Amount = item.RemainingAmount,
                Currency = item.Currency
            }).ToListAsync(cancellationToken);

        var projects = await Projects.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
            .AsNoTracking().OrderBy(item => item.ProjectCode).Take(500)
            .Select(item => new PurchaseRequisitionLinkageOptionDto
            {
                Id = item.Id,
                Code = item.ProjectCode,
                Name = item.Title,
                Status = item.Status,
                Amount = item.ApprovedBudget,
                Currency = item.BaseCurrencyCode
            }).ToListAsync(cancellationToken);

        var templateEntities = await Templates.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.Status == ProcurementSpecificationTemplateStatus.Published && item.EffectiveFromUtc <= now &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now))
            .AsNoTracking().OrderBy(item => item.TemplateCode).ThenByDescending(item => item.Version).Take(500)
            .ToListAsync(cancellationToken);
        var templates = templateEntities
            .Select(item => new PurchaseRequisitionLinkageOptionDto
            {
                Id = item.Id,
                Code = item.TemplateCode,
                Name = item.Name,
                Status = item.Status.ToString(),
                Category = item.Kind.ToString(),
                ParentReference = $"v{item.Version}"
            }).ToList();

        var exceptionRuleEntities = await ExceptionRules.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.IsEnabled && item.EffectiveFrom <= now && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now) &&
                !item.PolicySet.IsDeleted && item.PolicySet.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published)
            .Include(item => item.PolicySet).AsNoTracking().OrderBy(item => item.RuleCode).Take(500)
            .ToListAsync(cancellationToken);
        var exceptionRules = exceptionRuleEntities
            .Select(item => new PurchaseRequisitionLinkageOptionDto
            {
                Id = item.Id,
                Code = item.RuleCode,
                Name = item.ExceptionName,
                Status = item.Disposition.ToString(),
                ParentId = item.PolicySetId,
                ParentReference = $"{item.PolicySet.Code}/v{item.PolicySet.Version}",
                Category = item.Category.HasValue ? item.Category.Value.ToString() : null
            }).ToList();

        var exceptionWorkflowEntities = await WorkflowInstances.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.Status == WorkflowInstanceStatus.Completed && item.CompletedDate.HasValue && !item.EntityType.IsDeleted &&
                (item.EntityType.Code == "PROCUREMENT_EXCEPTION" || item.EntityType.Code == "ProcurementException"))
            .Include(item => item.EntityType).Include(item => item.WorkflowDefinition).AsNoTracking()
            .OrderByDescending(item => item.CompletedDate).Take(200)
            .ToListAsync(cancellationToken);
        var exceptionWorkflows = exceptionWorkflowEntities
            .Select(item => new PurchaseRequisitionLinkageOptionDto
            {
                Id = item.Id,
                Code = item.EntityId.ToString(),
                Name = item.WorkflowDefinition.Name,
                Status = item.Status.ToString(),
                ParentId = item.WorkflowDefinitionId
            }).ToList();

        var costCenters = await Requisitions.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.CostCenter != null && item.CostCenter != "")
            .AsNoTracking().Select(item => item.CostCenter!).Distinct().OrderBy(item => item).Take(200)
            .ToListAsync(cancellationToken);

        return new PurchaseRequisitionLinkageOptionsDto
        {
            PlanItems = planItems,
            Budgets = budgets,
            Projects = projects,
            SpecificationTemplates = templates,
            ApprovedExceptionRules = exceptionRules,
            ApprovedExceptionWorkflows = exceptionWorkflows,
            Categories = Enum.GetValues<ProcurementCategoryClass>().Select(ToOption).ToList(),
            RequestTypes = Enum.GetValues<PurchaseRequisitionType>().Select(ToOption).ToList(),
            CostCenters = costCenters
        };
    }

    public async Task PrepareAsync(
        PurchaseRequisition requisition,
        SavePurchaseRequisitionLinkageRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (requisition.TenantId != _currentUser.TenantId)
            throw new ProcurementRequisitionLinkageNotFoundException("PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");
        await EnsureCapabilityAsync(CreatePermission, requisition.RequisitionNumber, correlationId, cancellationToken);
        ValidateRequest(request);

        var tenantId = _currentUser.TenantId;
        var now = DateTime.UtcNow;
        var sourcePlanItem = request.SourcePlanItemId.HasValue
            ? await PlanItems.GetQueryable(item => item.Id == request.SourcePlanItemId.Value && item.TenantId == tenantId && !item.IsDeleted)
                .Include(item => item.ProcurementPlan).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new ProcurementRequisitionLinkageNotFoundException("PLAN_ITEM_NOT_FOUND", "The plan item was not found in the current tenant.")
            : null;

        if (sourcePlanItem is not null &&
            (!string.Equals(sourcePlanItem.ProcurementPlan.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(sourcePlanItem.ProcurementPlan.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(sourcePlanItem.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(sourcePlanItem.Status, "Planned", StringComparison.OrdinalIgnoreCase)))
            throw new ProcurementRequisitionLinkageConflictException(
                "PLAN_ITEM_NOT_EXECUTABLE", "Only an Approved item from an Approved or Active procurement plan can start a purchase requisition.");

        var sourceBudgetId = sourcePlanItem is null
            ? null
            : sourcePlanItem.ProcurementBudgetId ?? sourcePlanItem.ProcurementPlan.BudgetId;
        if (sourcePlanItem is not null && !sourceBudgetId.HasValue)
            throw new ProcurementRequisitionLinkageConflictException(
                "PLAN_BUDGET_REQUIRED", "The selected plan item is not linked to an approved procurement budget.");
        if (sourceBudgetId.HasValue && request.BudgetId.HasValue && sourceBudgetId.Value != request.BudgetId.Value)
            throw new ProcurementRequisitionLinkageValidationException(
                "PLAN_BUDGET_MISMATCH", "The selected budget does not match the budget assigned to the selected plan item.");
        var requestedBudgetId = sourceBudgetId ?? request.BudgetId;

        var budget = requestedBudgetId.HasValue
            ? await Budgets.GetQueryable(item => item.Id == requestedBudgetId.Value && item.TenantId == tenantId && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new ProcurementRequisitionLinkageNotFoundException("BUDGET_NOT_FOUND", "The procurement budget was not found in the current tenant.")
            : null;

        if (sourcePlanItem is not null && budget is not null && budget.DepartmentId != sourcePlanItem.ProcurementPlan.DepartmentId)
            throw new ProcurementRequisitionLinkageValidationException(
                "PLAN_BUDGET_DEPARTMENT_MISMATCH", "The plan item budget does not belong to the procurement plan department.");
        if (sourcePlanItem is not null && budget is not null &&
            (!string.Equals(budget.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(budget.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
             budget.EffectiveDate.HasValue && budget.EffectiveDate.Value > now ||
             budget.ExpiryDate.HasValue && budget.ExpiryDate.Value < now))
            throw new ProcurementRequisitionLinkageConflictException(
                "PLAN_BUDGET_NOT_EFFECTIVE", "The selected plan item budget is not approved and effective for new requisitions.");

        var project = request.ProjectId.HasValue
            ? await Projects.GetQueryable(item => item.Id == request.ProjectId.Value && item.TenantId == tenantId && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new ProcurementRequisitionLinkageNotFoundException("PROJECT_NOT_FOUND", "The project was not found in the current tenant.")
            : null;

        ProcurementSpecificationTemplate? template = null;
        if (request.SpecificationTemplateId.HasValue)
        {
            template = await Templates.GetQueryable(item => item.Id == request.SpecificationTemplateId.Value &&
                    item.TenantId == tenantId && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new ProcurementRequisitionLinkageNotFoundException(
                    "SPECIFICATION_TEMPLATE_NOT_FOUND", "The specification template was not found in the current tenant.");
            var isExistingLink = requisition.SpecificationTemplateId == template.Id;
            if (!isExistingLink && (template.Status != ProcurementSpecificationTemplateStatus.Published ||
                    template.EffectiveFromUtc > now || template.EffectiveToUtc.HasValue && template.EffectiveToUtc.Value < now))
                throw new ProcurementRequisitionLinkageConflictException(
                    "SPECIFICATION_TEMPLATE_NOT_EFFECTIVE", "A new requisition link requires an effective Published specification template.");
        }

        ProcurementPolicyExceptionRule? exceptionRule = null;
        WorkflowInstance? exceptionWorkflow = null;
        ApplicationUser? exceptionApprover = null;
        DateTime? exceptionApprovedAtUtc = null;
        if (request.ApprovedExceptionRuleId.HasValue)
        {
            exceptionRule = await ExceptionRules.GetQueryable(item => item.Id == request.ApprovedExceptionRuleId.Value &&
                    item.TenantId == tenantId && !item.IsDeleted)
                .Include(item => item.PolicySet).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new ProcurementRequisitionLinkageNotFoundException(
                    "EXCEPTION_RULE_NOT_FOUND", "The exception rule was not found in the current tenant.");
            var isExistingLink = requisition.ApprovedExceptionRuleId == exceptionRule.Id;
            if (!isExistingLink && (!exceptionRule.IsEnabled || exceptionRule.EffectiveFrom > now ||
                    exceptionRule.EffectiveTo.HasValue && exceptionRule.EffectiveTo.Value < now ||
                    exceptionRule.PolicySet.IsDeleted || exceptionRule.PolicySet.LifecycleStatus != ProcurementPolicyLifecycleStatus.Published))
                throw new ProcurementRequisitionLinkageConflictException(
                    "EXCEPTION_RULE_NOT_EFFECTIVE", "A new requisition link requires an effective exception rule from a Published policy set.");

            if (!request.ExceptionWorkflowInstanceId.HasValue)
                throw new ProcurementRequisitionLinkageValidationException(
                    "EXCEPTION_WORKFLOW_REQUIRED", "An approved exception must reference its completed shared workflow instance.");
            exceptionWorkflow = await WorkflowInstances.GetQueryable(item => item.Id == request.ExceptionWorkflowInstanceId.Value &&
                    item.TenantId == tenantId && !item.IsDeleted)
                .Include(item => item.EntityType).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new ProcurementRequisitionLinkageNotFoundException(
                    "EXCEPTION_WORKFLOW_NOT_FOUND", "The exception workflow instance was not found in the current tenant.");
            if (exceptionWorkflow.Status != WorkflowInstanceStatus.Completed)
                throw new ProcurementRequisitionLinkageConflictException(
                    "EXCEPTION_WORKFLOW_NOT_APPROVED", "The linked shared exception workflow has not completed with an approved outcome.");
            if (!exceptionWorkflow.CompletedDate.HasValue)
                throw new ProcurementRequisitionLinkageConflictException(
                    "EXCEPTION_WORKFLOW_COMPLETION_DATE_MISSING", "The completed shared exception workflow does not have an approval completion timestamp.");
            if (!string.Equals(exceptionWorkflow.EntityType.Code, "PROCUREMENT_EXCEPTION", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(exceptionWorkflow.EntityType.Code, "ProcurementException", StringComparison.OrdinalIgnoreCase))
                throw new ProcurementRequisitionLinkageValidationException(
                    "EXCEPTION_WORKFLOW_TYPE_INVALID", "The linked workflow must use the shared Procurement Exception entity type.");

            var approvalActivity = await WorkflowActivities.GetQueryable(item => item.WorkflowInstanceId == exceptionWorkflow.Id &&
                    item.TenantId == tenantId && !item.IsDeleted && item.PerformedById.HasValue)
                .Include(item => item.PerformedBy).OrderByDescending(item => item.ActivityDate)
                .AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            exceptionApprover = approvalActivity?.PerformedBy;
            exceptionApprovedAtUtc = exceptionWorkflow.CompletedDate;
        }
        else if (request.ExceptionWorkflowInstanceId.HasValue || !string.IsNullOrWhiteSpace(request.ExceptionApprovalReference) ||
                 !string.IsNullOrWhiteSpace(request.ExceptionEvidenceReference))
        {
            throw new ProcurementRequisitionLinkageValidationException(
                "EXCEPTION_RULE_REQUIRED", "Select an approved exception rule before supplying exception approval references.");
        }

        requisition.SourcePlanId = sourcePlanItem?.ProcurementPlanId;
        requisition.SourcePlanItemId = sourcePlanItem?.Id;
        requisition.SourcePlanNumber = sourcePlanItem?.ProcurementPlan.PlanNumber;
        requisition.SourcePlanTitle = sourcePlanItem?.ProcurementPlan.Title;
        requisition.SourcePlanItemDescription = sourcePlanItem?.ItemDescription;
        requisition.BudgetId = budget?.Id;
        requisition.BudgetCode = budget?.BudgetCode;
        requisition.BudgetAllocated = budget?.AllocatedAmount;
        requisition.BudgetRemaining = budget?.RemainingAmount;
        requisition.BudgetValidated = false;
        if (budget is not null)
            requisition.Currency = budget.Currency;
        requisition.ProcurementCategory = request.ProcurementCategory ?? ParseCategory(sourcePlanItem?.ItemCategory);
        // A planned requisition already carries its department through the source plan.
        // Finance owns any department-to-cost-centre mapping, so the requester must not
        // type or override a cost-centre value for this governed path.
        requisition.CostCenter = sourcePlanItem is null ? TrimOrNull(request.CostCenter, 100) : null;
        requisition.ProjectId = project?.Id;
        requisition.ProjectCode = project?.ProjectCode;
        requisition.ProjectName = project?.Title;
        requisition.RequisitionType = request.RequisitionType;
        requisition.SpecificationTemplateId = template?.Id;
        requisition.SpecificationTemplateCode = template?.TemplateCode;
        requisition.SpecificationTemplateName = template?.Name;
        requisition.SpecificationTemplateVersion = template?.Version;
        requisition.ApprovedExceptionRuleId = exceptionRule?.Id;
        requisition.ApprovedExceptionRuleCode = exceptionRule?.RuleCode;
        requisition.ApprovedExceptionName = exceptionRule?.ExceptionName;
        requisition.ExceptionWorkflowInstanceId = exceptionWorkflow?.Id;
        requisition.ExceptionApprovalReference = exceptionRule is null ? null : TrimOrNull(request.ExceptionApprovalReference, 200)
            ?? $"WF-{exceptionWorkflow!.Id:N}";
        requisition.ExceptionEvidenceReference = exceptionRule is null ? null : TrimOrNull(request.ExceptionEvidenceReference, 500);
        requisition.ExceptionApprovedById = exceptionApprover?.Id;
        requisition.ExceptionApprovedByName = exceptionApprover is null ? null : UserName(exceptionApprover);
        requisition.ExceptionApprovedAtUtc = exceptionApprovedAtUtc;
        requisition.LinkageRevision = Math.Max(0, requisition.LinkageRevision) + 1;
        requisition.LinkageLastUpdatedAtUtc = now;
        requisition.LinkageLastUpdatedById = _currentUser.UserId;
        requisition.LinkageLastUpdatedByName = ActorName;
    }

    public async Task RecordMutationAsync(
        PurchaseRequisition requisition,
        string action,
        PurchaseRequisitionLinkageDto? before,
        string correlationId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var after = Map(requisition);
        await RecordEventAsync(
            requisition,
            action,
            before,
            after,
            reason,
            $"{requisition.LinkageRevision:D4}-{action.ToLowerInvariant()}",
            correlationId,
            cancellationToken);
    }

    public async Task RecordExportAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        await EnsureCapabilityAsync(ExportPermission, requisition.RequisitionNumber, correlationId, cancellationToken);
        await RecordEventAsync(
            requisition,
            "Exported",
            null,
            Map(requisition),
            "Structured requisition-linkage export generated.",
            $"export-{Guid.NewGuid():N}",
            correlationId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseRequisitionLinkageHistoryDto>> GetHistoryAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var exists = await Requisitions.GetQueryable(item => item.Id == requisitionId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().AnyAsync(cancellationToken);
        if (!exists)
            throw new ProcurementRequisitionLinkageNotFoundException("PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");
        return await Events.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.EventType == EventType && item.SourceId == requisitionId)
            .AsNoTracking().OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.CreatedAt)
            .Select(item => new PurchaseRequisitionLinkageHistoryDto
            {
                Id = item.Id,
                Action = item.Action,
                Result = item.Result.ToString(),
                ActorName = item.ActorName,
                Reason = item.Reason,
                OccurredAtUtc = item.OccurredAtUtc,
                IntegrityHash = item.IntegrityHash
            }).ToListAsync(cancellationToken);
    }

    public PurchaseRequisitionLinkageDto Map(PurchaseRequisition requisition) => new()
    {
        SourcePlanId = requisition.SourcePlanId,
        SourcePlanItemId = requisition.SourcePlanItemId,
        SourcePlanNumber = requisition.SourcePlanNumber,
        SourcePlanTitle = requisition.SourcePlanTitle,
        SourcePlanItemDescription = requisition.SourcePlanItemDescription,
        BudgetId = requisition.BudgetId,
        BudgetCode = requisition.BudgetCode,
        BudgetAllocated = requisition.BudgetAllocated,
        BudgetRemaining = requisition.BudgetRemaining,
        ProcurementCategory = requisition.ProcurementCategory,
        CostCenter = requisition.CostCenter,
        ProjectId = requisition.ProjectId,
        ProjectCode = requisition.ProjectCode,
        ProjectName = requisition.ProjectName,
        RequisitionType = requisition.RequisitionType,
        SpecificationTemplateId = requisition.SpecificationTemplateId,
        SpecificationTemplateCode = requisition.SpecificationTemplateCode,
        SpecificationTemplateName = requisition.SpecificationTemplateName,
        SpecificationTemplateVersion = requisition.SpecificationTemplateVersion,
        ApprovedExceptionRuleId = requisition.ApprovedExceptionRuleId,
        ApprovedExceptionRuleCode = requisition.ApprovedExceptionRuleCode,
        ApprovedExceptionName = requisition.ApprovedExceptionName,
        ExceptionWorkflowInstanceId = requisition.ExceptionWorkflowInstanceId,
        ExceptionApprovalReference = requisition.ExceptionApprovalReference,
        ExceptionEvidenceReference = requisition.ExceptionEvidenceReference,
        ExceptionApprovedById = requisition.ExceptionApprovedById,
        ExceptionApprovedByName = requisition.ExceptionApprovedByName,
        ExceptionApprovedAtUtc = requisition.ExceptionApprovedAtUtc,
        Revision = requisition.LinkageRevision,
        LastUpdatedAtUtc = requisition.LinkageLastUpdatedAtUtc,
        LastUpdatedById = requisition.LinkageLastUpdatedById,
        LastUpdatedByName = requisition.LinkageLastUpdatedByName
    };

    private async Task RecordEventAsync(
        PurchaseRequisition requisition,
        string action,
        PurchaseRequisitionLinkageDto? before,
        PurchaseRequisitionLinkageDto after,
        string? reason,
        string eventSuffix,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var evidence = new List<ProcurementControlEventEvidenceReference>();
        if (!string.IsNullOrWhiteSpace(requisition.ExceptionEvidenceReference))
            evidence.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                Reference = requisition.ExceptionEvidenceReference,
                Label = "Approved exception evidence",
                RequirementKey = "PROCUREMENT_EXCEPTION_APPROVAL"
            });
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("purchase-requisition-linkage", requisition.TenantId,
                requisition.Id, eventSuffix),
            EventType = EventType,
            Action = action,
            Result = ProcurementControlEventResult.Succeeded,
            RuleCode = "PLN-004",
            SourceType = SourceType,
            SourceId = requisition.Id,
            SourceReference = requisition.RequisitionNumber,
            Reason = reason,
            Before = before,
            After = after,
            ResultValues = new
            {
                requisition.LinkageRevision,
                requisition.SourcePlanItemId,
                requisition.BudgetId,
                requisition.ProcurementCategory,
                requisition.CostCenter,
                requisition.ProjectId,
                requisition.RequisitionType,
                requisition.SpecificationTemplateId,
                requisition.ApprovedExceptionRuleId
            },
            CorrelationId = NormalizeCorrelation(correlationId),
            CausationId = NormalizeCorrelation(correlationId),
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = evidence
        }, cancellationToken);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            SourceType = SourceType,
            SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? "NEW" : sourceReference
        }, NormalizeCorrelation(correlationId), cancellationToken);
        if (!decision.Allowed) throw new ProcurementRequisitionLinkageAuthorizationException(decision.Message);
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator() || _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role => ProcurementAccessControlRegistry.FindRole(role) is not null)) return;
        throw new ProcurementRequisitionLinkageAuthorizationException(
            "A TDC procurement role or tenant-administration role is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementRequisitionLinkageAuthorizationException("An authenticated tenant context is required.");
    }

    private static void ValidateRequest(SavePurchaseRequisitionLinkageRequest request)
    {
        if (!Enum.IsDefined(request.RequisitionType))
            throw new ProcurementRequisitionLinkageValidationException("REQUEST_TYPE_INVALID", "RequisitionType is invalid.");
        if (request.ProcurementCategory.HasValue && !Enum.IsDefined(request.ProcurementCategory.Value))
            throw new ProcurementRequisitionLinkageValidationException("CATEGORY_INVALID", "ProcurementCategory is invalid.");
    }

    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");
    private string ActorName => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private static string UserName(ApplicationUser user) => Truncate($"{user.FirstName} {user.LastName}".Trim(), 300);
    private static string NormalizeCorrelation(string correlationId) =>
        string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : Truncate(correlationId.Trim(), 100);
    private static string? TrimOrNull(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), maxLength);
    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
    private static ProcurementCategoryClass? ParseCategory(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Enum.TryParse<ProcurementCategoryClass>(value.Replace(" ", string.Empty), true, out var parsed)
            ? parsed
            : null;
    private static PurchaseRequisitionNamedOptionDto ToOption<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        return new PurchaseRequisitionNamedOptionDto
        {
            Value = Convert.ToInt32(value),
            Name = name,
            Label = string.Concat(name.Select((character, index) => index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()))
        };
    }
}
