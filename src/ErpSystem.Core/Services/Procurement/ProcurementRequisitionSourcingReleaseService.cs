using ErpSystem.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementRequisitionSourcingReleaseService : IProcurementRequisitionSourcingReleaseService
{
    private const string SourceType = "PurchaseRequisition";
    private const string EventType = "PurchaseRequisitionSourcingRelease";
    private const string SourcingPermission = "procurement.sourcing.manage";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementComplianceDecisionService _complianceDecisions;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProcurementRequisitionSubmissionControlService _submissionControl;
    private readonly IProcurementRequisitionBudgetControlService _budgetControl;
    private readonly IProcurementRequisitionAuthorityRouteService _authorityControl;
    private readonly IProcurementRequisitionSourcingReleaseStore _releaseStore;

    public ProcurementRequisitionSourcingReleaseService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementComplianceDecisionService complianceDecisions,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        IProcurementRequisitionSubmissionControlService submissionControl,
        IProcurementRequisitionBudgetControlService budgetControl,
        IProcurementRequisitionAuthorityRouteService authorityControl,
        IProcurementRequisitionSourcingReleaseStore releaseStore)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _complianceDecisions = complianceDecisions;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _submissionControl = submissionControl;
        _budgetControl = budgetControl;
        _authorityControl = authorityControl;
        _releaseStore = releaseStore;
    }

    private IGenericRepository<PurchaseRequisition> Requisitions => _unitOfWork.Repository<PurchaseRequisition>();
    private IGenericRepository<PurchaseRequisitionItem> RequisitionItems => _unitOfWork.Repository<PurchaseRequisitionItem>();
    private IGenericRepository<ProcurementSpecificationTemplate> Templates => _unitOfWork.Repository<ProcurementSpecificationTemplate>();
    private IGenericRepository<ProcurementConfigurationProfile> ConfigurationProfiles => _unitOfWork.Repository<ProcurementConfigurationProfile>();
    private IGenericRepository<ProcurementRequisitionAuthorityRoute> Routes => _unitOfWork.Repository<ProcurementRequisitionAuthorityRoute>();
    private IGenericRepository<WorkflowInstance> WorkflowInstances => _unitOfWork.Repository<WorkflowInstance>();
    private IGenericRepository<WorkflowApproval> WorkflowApprovals => _unitOfWork.Repository<WorkflowApproval>();
    private IGenericRepository<ProcurementRequisitionSourcingRelease> Releases => _unitOfWork.Repository<ProcurementRequisitionSourcingRelease>();

    public async Task<PurchaseRequisitionSourcingReadinessDto> GetReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return await GetLinkedControlReadinessAsync(requisitionId, cancellationToken);
    }

    public async Task<PurchaseRequisitionSourcingReadinessDto> GetLinkedControlReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var requisition = await LoadRequisitionAsync(requisitionId, false, cancellationToken);
        return await EvaluateAsync(requisition, cancellationToken, linkedControl: true);
    }

    public async Task<IReadOnlyList<PurchaseRequisitionSourcingReleaseDto>> GetHistoryAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var requisition = await LoadRequisitionAsync(requisitionId, false, cancellationToken);
        var releases = await Releases.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.PurchaseRequisitionId == requisitionId && !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.AttemptNumber).ToListAsync(cancellationToken);
        return releases.Select(item => Map(item, requisition.RequisitionNumber)).ToList();
    }

    public async Task<PurchaseRequisitionSourcingReleaseDto> ReleaseAsync(
        Guid requisitionId,
        string reason,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new ProcurementRequisitionSourcingValidationException(
                "PR_SOURCING_RELEASE_REASON_REQUIRED", "A sourcing-release reason of at least five characters is required.");
        EnsureAuthenticatedTenant();
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(SourcingPermission, requisitionId.ToString("N"), normalizedCorrelation, cancellationToken);

        PurchaseRequisitionSourcingReleaseDto? result = null;
        PurchaseRequisitionSourcingReadinessDto? blocked = null;
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var transactionStarted = false;
            try
            {
                if (_releaseStore.UsesRelationalDatabase)
                {
                    await _unitOfWork.BeginTransactionAsync(cancellationToken);
                    transactionStarted = true;
                }
                if (!_releaseStore.HasRequiredTransaction)
                    throw new ProcurementRequisitionSourcingConflictException(
                        "PR_SOURCING_TRANSACTION_REQUIRED", "Sourcing release must run inside a database transaction.");

                var requisition = await _releaseStore.GetRequisitionForUpdateAsync(
                        _currentUser.TenantId, requisitionId, cancellationToken)
                    ?? throw new ProcurementRequisitionSourcingNotFoundException(
                        "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");
                var readiness = await EvaluateAsync(requisition, cancellationToken);
                if (!readiness.IsCompliant)
                {
                    blocked = readiness;
                    await RecordAsync(requisition, readiness, "SourcingReleaseBlocked",
                        ProcurementControlEventResult.Denied, null, normalizedCorrelation, cancellationToken);
                }
                else if (readiness.CurrentRelease is not null && readiness.IsReleased)
                {
                    result = readiness.CurrentRelease;
                    await RecordAsync(requisition, readiness, "SourcingReleaseReused",
                        ProcurementControlEventResult.Allowed, result, normalizedCorrelation, cancellationToken);
                }
                else
                {
                    var attempt = (await Releases.GetQueryableIncludingDeleted(item => item.TenantId == _currentUser.TenantId &&
                            item.PurchaseRequisitionId == requisition.Id)
                        .Select(item => (int?)item.AttemptNumber).MaxAsync(cancellationToken) ?? 0) + 1;
                    var release = BuildRelease(requisition, readiness, attempt, reason.Trim(), normalizedCorrelation);
                    await Releases.AddAsync(release);
                    readiness.IsReleased = true;
                    readiness.CanRelease = false;
                    readiness.CurrentRelease = Map(release, requisition.RequisitionNumber);
                    readiness.DecisionCode = "PR_SOURCING_RELEASED";
                    readiness.Message = $"Immutable sourcing release {release.ReleaseReference} was recorded.";
                    await RecordAsync(requisition, readiness, "SourcingReleased",
                        ProcurementControlEventResult.Succeeded, readiness.CurrentRelease, normalizedCorrelation, cancellationToken);
                    result = readiness.CurrentRelease;
                }

                if (transactionStarted) await _unitOfWork.CommitAsync(cancellationToken);
                else await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                if (transactionStarted)
                {
                    try { await _unitOfWork.RollbackAsync(cancellationToken); }
                    catch (InvalidOperationException) { }
                }
                throw;
            }
        }, cancellationToken);

        if (blocked is not null) throw new ProcurementRequisitionSourcingBlockedException(blocked);
        return result ?? throw new ProcurementRequisitionSourcingConflictException(
            "PR_SOURCING_RELEASE_MISSING", "The sourcing release operation did not return a release record.");
    }

    public async Task<PurchaseRequisitionSourcingReleaseDto> EnforceSourcingAsync(
        Guid requisitionId,
        string sourceType,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(SourcingPermission, sourceReference, normalizedCorrelation, cancellationToken);
        var requisition = await LoadRequisitionAsync(requisitionId, false, cancellationToken);
        var readiness = await EvaluateAsync(requisition, cancellationToken);
        if (readiness.IsCompliant && readiness.CanRelease)
        {
            await ReleaseAsync(
                requisitionId,
                $"System-generated release for {sourceType} {sourceReference}.",
                normalizedCorrelation,
                cancellationToken);
            requisition = await LoadRequisitionAsync(requisitionId, false, cancellationToken);
            readiness = await EvaluateAsync(requisition, cancellationToken);
        }
        var action = readiness.IsReleased ? "SourcingEntryAllowed" : "SourcingEntryBlocked";
        await RecordAsync(requisition, readiness, action,
            readiness.IsReleased ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
            new { sourceType, sourceReference, ReleaseId = readiness.CurrentRelease?.Id },
            normalizedCorrelation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!readiness.IsReleased)
            throw new ProcurementRequisitionSourcingBlockedException(readiness);
        return readiness.CurrentRelease!;
    }

    private async Task<PurchaseRequisitionSourcingReadinessDto> EvaluateAsync(
        PurchaseRequisition requisition,
        CancellationToken cancellationToken,
        bool linkedControl = false)
    {
        var evaluatedAtUtc = DateTime.UtcNow;
        var budget = linkedControl
            ? await _budgetControl.GetLinkedControlReadinessAsync(requisition.Id, cancellationToken)
            : await _budgetControl.GetReadinessAsync(requisition.Id, cancellationToken);
        budget ??= new PurchaseRequisitionBudgetReadinessDto
        {
            RequisitionId = requisition.Id,
            RequisitionNumber = requisition.RequisitionNumber,
            Status = requisition.Status,
            IsCompliant = false,
            CanReserve = false,
            DecisionCode = "PR_BUDGET_CONTROL_UNAVAILABLE",
            Message = "Budget availability could not be evaluated. Refresh the requisition or contact Finance configuration support."
        };
        var items = await RequisitionItems.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.RequisitionId == requisition.Id && !item.IsDeleted && item.Status != "Cancelled")
            .AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var route = await Routes.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.PurchaseRequisitionId == requisition.Id && !item.IsDeleted)
            .Include(item => item.Steps)
            .AsNoTracking()
            .OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var routeIntegrityValid = route is null ||
            (route.Steps.Count > 0 &&
             ComputeHash(route.SnapshotJson) == route.IntegrityHash &&
             route.Category == requisition.ProcurementCategory &&
             route.Amount == requisition.TotalAmount &&
             string.Equals(route.CurrencyCode, NormalizeCurrency(requisition.Currency), StringComparison.Ordinal));
        var routeWorkflowDefinitionId = route?.WorkflowDefinitionId;
        var workflow = await WorkflowInstances.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.EntityId == requisition.Id &&
                (!routeWorkflowDefinitionId.HasValue ||
                 item.WorkflowDefinitionId == routeWorkflowDefinitionId.Value) &&
                !item.IsDeleted && item.Status == WorkflowInstanceStatus.Completed)
            .AsNoTracking().OrderByDescending(item => item.CompletedDate).ThenByDescending(item => item.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        var requirements = new List<PurchaseRequisitionSourcingRequirementDto>();
        Add(requirements, "PR_STATUS", "Approved requisition",
            string.Equals(requisition.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
            requisition.ApprovedAt.HasValue && requisition.ApprovedById.HasValue,
            "PR_NOT_APPROVED",
            "Complete the configured Purchase Requisition approval workflow with a different authorized approver before sourcing.");

        var completeItems = items.Count > 0 && items.All(item =>
            !string.IsNullOrWhiteSpace(item.ItemDescription) && item.Quantity > 0 &&
            !string.IsNullOrWhiteSpace(item.UnitOfMeasure) && item.EstimatedUnitPrice > 0 && item.LineTotal > 0 &&
            (requisition.SpecificationTemplateId.HasValue || !string.IsNullOrWhiteSpace(item.Specifications)));
        var mandatory = requisition.RequiredDate.HasValue && !string.IsNullOrWhiteSpace(requisition.Department) &&
            !string.IsNullOrWhiteSpace(requisition.Justification) && requisition.BudgetId.HasValue &&
            requisition.ProcurementCategory.HasValue && requisition.TotalAmount > 0 &&
            NormalizeCurrency(requisition.Currency).Length == 3 && NormalizeCurrency(requisition.Currency).All(char.IsLetter) &&
            (requisition.RequisitionType != PurchaseRequisitionType.ProjectPurchase || requisition.ProjectId.HasValue) &&
            completeItems;
        Add(requirements, "MANDATORY_FIELDS", "Complete requisition and specifications", mandatory,
            "PR_SOURCING_FIELDS_INCOMPLETE",
            "Complete the department, required date, justification, budget, category, currency, amount, project linkage where applicable, and positive item lines with specifications.");

        Add(requirements, "BUDGET_AVAILABILITY", "Approved budget availability",
            budget.IsCompliant && budget.CanReserve,
            budget.DecisionCode,
            budget.Message,
            budget.BudgetCode);

        if (route is not null)
            Add(requirements, "AUTHORITY_ROUTE_LINEAGE", "Immutable authority-route lineage",
                routeIntegrityValid,
                "PR_SOURCING_AUTHORITY_ROUTE_INVALID",
                "The latest captured authority route no longer matches the approved requisition or failed integrity verification.",
                route.RouteReference);

        var fingerprintObject = new
        {
            schemaVersion = "tdc.pr-sourcing-release.v2",
            requisition.Id,
            requisition.Status,
            requisition.ApprovedById,
            requisition.ApprovedAt,
            requisition.SourcePlanId,
            requisition.SourcePlanItemId,
            requisition.ApprovedExceptionRuleId,
            requisition.ExceptionWorkflowInstanceId,
            requisition.ExceptionApprovalReference,
            requisition.ExceptionEvidenceReference,
            requisition.ExceptionApprovedById,
            requisition.ExceptionApprovedAtUtc,
            requisition.SpecificationTemplateId,
            requisition.BudgetId,
            AuthorityRouteId = routeIntegrityValid ? route?.Id : null,
            AuthorityRouteIntegrityHash = routeIntegrityValid ? route?.IntegrityHash : null,
            WorkflowInstanceId = workflow?.Id,
            workflow?.CompletedDate,
            requisition.TotalAmount,
            Currency = NormalizeCurrency(requisition.Currency),
            requisition.ProcurementCategory,
            ItemState = items.Select(item => new
            {
                item.Id,
                item.SourcePlanItemId,
                item.ItemDescription,
                item.Specifications,
                item.Quantity,
                item.UnitOfMeasure,
                item.EstimatedUnitPrice,
                item.LineTotal
            }).ToArray()
        };
        var fingerprint = ComputeHash(JsonSerializer.Serialize(fingerprintObject, JsonOptions));
        var latest = await Releases.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.PurchaseRequisitionId == requisition.Id && !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.AttemptNumber).FirstOrDefaultAsync(cancellationToken);
        var latestIntegrityValid = latest is null || ComputeHash(latest.SnapshotJson) == latest.IntegrityHash;
        if (!latestIntegrityValid)
            Add(requirements, "RELEASE_INTEGRITY", "Release snapshot integrity", false,
                "PR_SOURCING_RELEASE_INTEGRITY_INVALID",
                "The latest immutable sourcing-release snapshot failed integrity verification.");
        var isCompliant = requirements.All(item => item.Satisfied);
        var current = latest is not null && latest.ControlFingerprint == fingerprint && latestIntegrityValid;
        return new PurchaseRequisitionSourcingReadinessDto
        {
            RequisitionId = requisition.Id,
            RequisitionNumber = requisition.RequisitionNumber,
            Status = requisition.Status,
            IsCompliant = isCompliant,
            CanRelease = isCompliant && !current,
            IsReleased = isCompliant && current,
            HasStaleRelease = latest is not null && !current,
            DecisionCode = !isCompliant ? requirements.First(item => !item.Satisfied).Code : current
                ? "PR_SOURCING_RELEASE_CURRENT" : "PR_SOURCING_READY",
            Message = !isCompliant ? requirements.First(item => !item.Satisfied).Message : current
                ? $"Sourcing release {latest!.ReleaseReference} matches the approved requisition."
                : "The approved requisition is ready for sourcing. The release audit record will be created automatically when sourcing starts.",
            EvaluatedAtUtc = evaluatedAtUtc,
            ControlFingerprint = fingerprint,
            SourcePlanId = requisition.SourcePlanId,
            SourcePlanItemId = requisition.SourcePlanItemId,
            ApprovedExceptionRuleId = requisition.ApprovedExceptionRuleId,
            ExceptionWorkflowInstanceId = requisition.ExceptionWorkflowInstanceId,
            ExceptionApprovalReference = requisition.ExceptionApprovalReference,
            SpecificationTemplateId = requisition.SpecificationTemplateId,
            SpecificationTemplateCode = requisition.SpecificationTemplateCode,
            SpecificationTemplateVersion = requisition.SpecificationTemplateVersion,
            BudgetCommitmentId = budget.CommitmentId,
            BudgetCommitmentReference = budget.CommitmentReference,
            AuthorityRouteId = routeIntegrityValid ? route?.Id : null,
            AuthorityRouteReference = routeIntegrityValid ? route?.RouteReference : null,
            WorkflowInstanceId = workflow?.Id,
            CurrentRelease = current ? Map(latest!, requisition.RequisitionNumber) : null,
            Requirements = requirements,
            RequiredActions = requirements.Where(item => !item.Satisfied).Select(item => item.Message).Distinct().ToList()
        };
    }

    private async Task<PurchaseRequisitionSourcingReadinessDto> EvaluateLegacyAsync(
        PurchaseRequisition requisition,
        CancellationToken cancellationToken,
        bool linkedControl = false)
    {
        var evaluatedAtUtc = DateTime.UtcNow;
        var submission = linkedControl
            ? await _submissionControl.GetLinkedControlReadinessAsync(requisition.Id, cancellationToken)
            : await _submissionControl.GetReadinessAsync(requisition.Id, cancellationToken);
        var budget = linkedControl
            ? await _budgetControl.GetLinkedControlReadinessAsync(requisition.Id, cancellationToken)
            : await _budgetControl.GetReadinessAsync(requisition.Id, cancellationToken);
        var authority = linkedControl
            ? await _authorityControl.GetLinkedControlReadinessAsync(requisition.Id, cancellationToken)
            : await _authorityControl.GetReadinessAsync(requisition.Id, cancellationToken);
        var items = await RequisitionItems.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.RequisitionId == requisition.Id && !item.IsDeleted)
            .AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var route = authority.AuthorityRouteId.HasValue
            ? await Routes.GetQueryable(item => item.Id == authority.AuthorityRouteId.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .Include(item => item.Steps).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            : null;
        var template = requisition.SpecificationTemplateId.HasValue
            ? await Templates.GetQueryable(item => item.Id == requisition.SpecificationTemplateId.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            : null;
        var sourceProfile = route is null
            ? null
            : await ConfigurationProfiles.GetQueryable(item => item.Id == route.SourceConfigurationProfileId &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var workflow = route is null ? null : await WorkflowInstances.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.EntityId == requisition.Id &&
                item.WorkflowDefinitionId == route.WorkflowDefinitionId && !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.CreatedDate).FirstOrDefaultAsync(cancellationToken);
        var initiatorApproved = workflow is not null && await WorkflowApprovals.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.StepInstance.WorkflowInstanceId == workflow.Id &&
                item.Status == WorkflowApprovalStatus.Approved &&
                item.ProcessedById == requisition.RequestedById)
            .AsNoTracking().AnyAsync(cancellationToken);
        var currentPolicy = route is null || !requisition.ProcurementCategory.HasValue
            ? null
            : await _complianceDecisions.EvaluateAuthorityRouteAsync(new ProcurementAuthorityRouteDecisionRequest
            {
                Category = requisition.ProcurementCategory.Value,
                Amount = requisition.TotalAmount,
                CurrencyCode = NormalizeCurrency(requisition.Currency),
                AtUtc = evaluatedAtUtc,
                SourceType = SourceType,
                SourceReference = requisition.RequisitionNumber
            }, $"sourcing-review:{requisition.Id:N}", cancellationToken);

        var requirements = new List<PurchaseRequisitionSourcingRequirementDto>();
        Add(requirements, "PR_STATUS", "Approved requisition", string.Equals(requisition.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
            requisition.ApprovedAt.HasValue && requisition.ApprovedById.HasValue,
            "PR_NOT_APPROVED", "The requisition must have an Approved outcome and approval metadata before sourcing.");

        var mandatory = requisition.RequiredDate.HasValue && !string.IsNullOrWhiteSpace(requisition.CostCenter) &&
            !string.IsNullOrWhiteSpace(requisition.Justification) && requisition.BudgetId.HasValue &&
            requisition.ProcurementCategory.HasValue && requisition.TotalAmount > 0 &&
            NormalizeCurrency(requisition.Currency).Length == 3 && NormalizeCurrency(requisition.Currency).All(char.IsLetter) &&
            (requisition.RequisitionType != PurchaseRequisitionType.ProjectPurchase || requisition.ProjectId.HasValue) &&
            items.Count > 0 && items.All(item => !string.IsNullOrWhiteSpace(item.ItemDescription) && item.Quantity > 0 &&
                !string.IsNullOrWhiteSpace(item.UnitOfMeasure) && item.EstimatedUnitPrice > 0 && item.LineTotal > 0);
        Add(requirements, "MANDATORY_FIELDS", "Mandatory requisition fields", mandatory,
            "PR_SOURCING_FIELDS_INCOMPLETE", "Required date, cost centre, justification, budget, category, currency, amount, project linkage where applicable, and complete positive lines are required.");

        var exactPlanItemLineage = items.All(item => item.Status == "Cancelled" || item.SourcePlanItemId.HasValue) ||
            items.All(item => item.Status == "Cancelled" || !item.SourcePlanItemId.HasValue) && requisition.SourcePlanItemId.HasValue;
        Add(requirements, "PLAN_LINKAGE", "APP plan and item linkage",
            requisition.SourcePlanId.HasValue && requisition.SourcePlanItemId.HasValue &&
            exactPlanItemLineage &&
            submission.SourcePlanId == requisition.SourcePlanId && submission.SourcePlanItemId == requisition.SourcePlanItemId,
            "PR_SOURCING_PLAN_LINK_REQUIRED", "The requisition must retain exact source procurement-plan and plan-item lineage.",
            requisition.SourcePlanNumber);
        Add(requirements, "APP_ACKNOWLEDGEMENT", "Acknowledged APP or approved exception", submission.IsCompliant,
            submission.DecisionCode, submission.Message,
            submission.AppAcknowledgementReference ?? submission.ExceptionApprovalReference);

        var expectedKind = requisition.ProcurementCategory switch
        {
            ProcurementCategoryClass.Goods => ProcurementSpecificationTemplateKind.Goods,
            ProcurementCategoryClass.Works => ProcurementSpecificationTemplateKind.Works,
            _ => ProcurementSpecificationTemplateKind.Services
        };
        var templateComplete = template is not null && template.Kind == expectedKind && template.PublishedAtUtc.HasValue &&
            template.Status is ProcurementSpecificationTemplateStatus.Published or ProcurementSpecificationTemplateStatus.Retired &&
            !string.IsNullOrWhiteSpace(template.Purpose) && !string.IsNullOrWhiteSpace(template.FunctionalAndPerformanceRequirements) &&
            !string.IsNullOrWhiteSpace(template.ProcessAndMaterialsRequirements) && !string.IsNullOrWhiteSpace(template.DimensionsAndMarkingRequirements) &&
            !string.IsNullOrWhiteSpace(template.TestingAndInspectionRequirements) && !string.IsNullOrWhiteSpace(template.ApplicableStandards) &&
            !string.IsNullOrWhiteSpace(template.Deliverables) && !string.IsNullOrWhiteSpace(template.AcceptanceCriteria) &&
            requisition.SpecificationTemplateCode == template.TemplateCode && requisition.SpecificationTemplateName == template.Name &&
            requisition.SpecificationTemplateVersion == template.Version &&
            (route is null || (template.PublishedAtUtc <= route.CapturedAtUtc &&
                template.EffectiveFromUtc <= route.CapturedAtUtc && (!template.EffectiveToUtc.HasValue || template.EffectiveToUtc >= route.CapturedAtUtc) &&
                (!template.RetiredAtUtc.HasValue || template.RetiredAtUtc >= route.CapturedAtUtc)));
        Add(requirements, "SPECIFICATION", "Complete category-matched specification", templateComplete,
            "PR_SOURCING_SPECIFICATION_INVALID", "The exact published specification snapshot must match the category, contain every required section, and have been effective when the authority route was captured.",
            template is null ? null : $"{template.TemplateCode}-v{template.Version}");

        Add(requirements, "BUDGET_COMMITMENT", "Reserved finance commitment", budget.IsCompliant &&
            budget.CommitmentId.HasValue && string.Equals(budget.CommitmentStatus, ProcurementBudgetCommitmentStatus.Reserved.ToString(), StringComparison.OrdinalIgnoreCase),
            budget.DecisionCode, budget.Message, budget.CommitmentReference);
        var policyLineageCurrent = route is not null && ComputeHash(route.SnapshotJson) == route.IntegrityHash &&
            CurrentPolicyMatchesRoute(currentPolicy, route) && sourceProfile is not null &&
            sourceProfile.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
            sourceProfile.PublishedAt.HasValue && sourceProfile.EffectiveFrom <= evaluatedAtUtc &&
            (!sourceProfile.EffectiveTo.HasValue || sourceProfile.EffectiveTo.Value >= evaluatedAtUtc);
        Add(requirements, "POLICY_LINEAGE", "Current policy and configuration lineage", policyLineageCurrent,
            "PR_SOURCING_POLICY_STALE", "The captured authority route no longer matches the current effective policy, configuration, workflow, rule stages, or immutable route hash.",
            route is null ? null : $"{route.PolicyCode}-v{route.PolicyVersion}:{route.IntegrityHash}");
        Add(requirements, "AUTHORITY_ROUTE", "Immutable authority route", authority.IsCompliant && route is not null &&
            route.Amount == requisition.TotalAmount && route.Category == requisition.ProcurementCategory &&
            route.CurrencyCode == NormalizeCurrency(requisition.Currency),
            authority.DecisionCode, authority.Message,
            route is null ? authority.RouteReference : $"{route.RouteReference}:{route.IntegrityHash}");
        Add(requirements, "AUTHORITY_WORKFLOW", "Completed exact authority workflow", workflow is not null &&
            workflow.Status == WorkflowInstanceStatus.Completed && workflow.CompletedDate.HasValue,
            "PR_SOURCING_WORKFLOW_INCOMPLETE", "The workflow instance bound to the captured authority route must finish with the approved Completed outcome.",
            workflow is null ? null : $"workflow:{workflow.Id:N}");
        Add(requirements, "SOD", "Initiator and approver segregation", workflow is not null && !initiatorApproved,
            "PR_SOURCING_SOD_VIOLATION", "The requisition initiator cannot process an approval in the completed authority workflow.");
        var evidenceComplete = submission.IsCompliant && templateComplete && budget.IsCompliant && route is not null && workflow?.CompletedDate is not null &&
            (!budget.IsOverride || (!string.IsNullOrWhiteSpace(budget.OverrideApprovalReference) && !string.IsNullOrWhiteSpace(budget.OverrideEvidenceReference)));
        Add(requirements, "EVIDENCE", "Required evidence lineage", evidenceComplete,
            "PR_SOURCING_EVIDENCE_INCOMPLETE", "APP/exception, specification, finance, authority-route, workflow, and override evidence where applicable must be present.");

        var fingerprintObject = new
        {
            schemaVersion = "tdc.pr-sourcing-release.v2",
            requisition.Id,
            requisition.Status,
            requisition.ApprovedById,
            requisition.ApprovedAt,
            requisition.SourcePlanId,
            requisition.SourcePlanItemId,
            AppSubmissionId = submission.AppSubmissionId,
            submission.AppSubmissionAttemptNumber,
            submission.AppAcknowledgementReference,
            submission.AppAcknowledgedAtUtc,
            submission.ApprovedExceptionRuleId,
            submission.ExceptionWorkflowInstanceId,
            submission.ExceptionApprovalReference,
            submission.ExceptionEvidenceReference,
            TemplateId = template?.Id,
            TemplateCode = template?.TemplateCode,
            TemplateVersion = template?.Version,
            CommitmentId = budget.CommitmentId,
            budget.CommitmentReference,
            budget.ReservationSequence,
            budget.ReservedAtUtc,
            RouteId = route?.Id,
            RouteHash = route?.IntegrityHash,
            WorkflowInstanceId = workflow?.Id,
            workflow?.CompletedDate,
            requisition.TotalAmount,
            Currency = NormalizeCurrency(requisition.Currency),
            requisition.ProcurementCategory,
            ItemState = items.Select(item => new { item.Id, item.ItemDescription, item.Quantity, item.UnitOfMeasure, item.EstimatedUnitPrice, item.LineTotal }).ToArray()
        };
        var fingerprint = ComputeHash(JsonSerializer.Serialize(fingerprintObject, JsonOptions));
        var latest = await Releases.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.PurchaseRequisitionId == requisition.Id && !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.AttemptNumber).FirstOrDefaultAsync(cancellationToken);
        var latestIntegrityValid = latest is null || ComputeHash(latest.SnapshotJson) == latest.IntegrityHash;
        var isCompliant = requirements.All(item => item.Satisfied) && latestIntegrityValid;
        if (!latestIntegrityValid)
            Add(requirements, "RELEASE_INTEGRITY", "Release snapshot integrity", false,
                "PR_SOURCING_RELEASE_INTEGRITY_INVALID", "The latest immutable sourcing-release snapshot failed integrity verification.");
        var current = latest is not null && latest.ControlFingerprint == fingerprint && latestIntegrityValid;
        var releaseDto = current ? Map(latest!, requisition.RequisitionNumber) : null;
        return new PurchaseRequisitionSourcingReadinessDto
        {
            RequisitionId = requisition.Id,
            RequisitionNumber = requisition.RequisitionNumber,
            Status = requisition.Status,
            IsCompliant = isCompliant,
            CanRelease = isCompliant && !current,
            IsReleased = isCompliant && current,
            HasStaleRelease = latest is not null && !current,
            DecisionCode = !isCompliant ? requirements.First(item => !item.Satisfied).Code : current ? "PR_SOURCING_RELEASE_CURRENT" : "PR_SOURCING_READY",
            Message = !isCompliant ? requirements.First(item => !item.Satisfied).Message : current
                ? $"Immutable sourcing release {latest!.ReleaseReference} matches the current control lineage."
                : "Every pre-sourcing acceptance control is satisfied; an immutable release may be recorded.",
            EvaluatedAtUtc = evaluatedAtUtc,
            ControlFingerprint = fingerprint,
            SourcePlanId = requisition.SourcePlanId,
            SourcePlanItemId = requisition.SourcePlanItemId,
            AppSubmissionId = submission.AppSubmissionId,
            AppSubmissionAttemptNumber = submission.AppSubmissionAttemptNumber,
            AppAcknowledgementReference = submission.AppAcknowledgementReference,
            ApprovedExceptionRuleId = submission.ApprovedExceptionRuleId,
            ExceptionWorkflowInstanceId = submission.ExceptionWorkflowInstanceId,
            ExceptionApprovalReference = submission.ExceptionApprovalReference,
            SpecificationTemplateId = template?.Id,
            SpecificationTemplateCode = template?.TemplateCode,
            SpecificationTemplateVersion = template?.Version,
            BudgetCommitmentId = budget.CommitmentId,
            BudgetCommitmentReference = budget.CommitmentReference,
            AuthorityRouteId = route?.Id,
            AuthorityRouteReference = route?.RouteReference,
            WorkflowInstanceId = workflow?.Id,
            CurrentRelease = releaseDto,
            Requirements = requirements,
            RequiredActions = requirements.Where(item => !item.Satisfied).Select(item => item.Message).Distinct().ToList()
        };
    }

    private ProcurementRequisitionSourcingRelease BuildRelease(
        PurchaseRequisition requisition,
        PurchaseRequisitionSourcingReadinessDto readiness,
        int attempt,
        string reason,
        string correlationId)
    {
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        var releaseReference = Truncate($"SRL-{requisition.RequisitionNumber}-A{attempt}", 100);
        var snapshot = new
        {
            schemaVersion = "tdc.pr-sourcing-release.v2",
            id,
            purchaseRequisitionId = requisition.Id,
            requisition.RequisitionNumber,
            attemptNumber = attempt,
            releaseReference,
            readiness.ControlFingerprint,
            requirements = readiness.Requirements,
            releasedAtUtc = now,
            releasedById = _currentUser.UserId,
            releasedByName = ActorName(),
            releaseReason = reason,
            correlationId
        };
        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        var release = new ProcurementRequisitionSourcingRelease
        {
            Id = id,
            TenantId = _currentUser.TenantId,
            PurchaseRequisitionId = requisition.Id,
            AttemptNumber = attempt,
            ReleaseReference = releaseReference,
            SourcePlanId = requisition.SourcePlanId,
            SourcePlanItemId = requisition.SourcePlanItemId,
            AppSubmissionId = readiness.AppSubmissionId,
            AppSubmissionAttemptNumber = readiness.AppSubmissionAttemptNumber,
            AppAcknowledgementReference = readiness.AppAcknowledgementReference,
            ApprovedExceptionRuleId = readiness.ApprovedExceptionRuleId,
            ExceptionWorkflowInstanceId = readiness.ExceptionWorkflowInstanceId,
            ExceptionApprovalReference = readiness.ExceptionApprovalReference,
            SpecificationTemplateId = requisition.SpecificationTemplateId,
            SpecificationTemplateCode = requisition.SpecificationTemplateCode,
            SpecificationTemplateVersion = requisition.SpecificationTemplateVersion,
            BudgetCommitmentId = readiness.BudgetCommitmentId,
            BudgetCommitmentReference = readiness.BudgetCommitmentReference,
            AuthorityRouteId = readiness.AuthorityRouteId,
            AuthorityRouteReference = readiness.AuthorityRouteReference,
            WorkflowInstanceId = readiness.WorkflowInstanceId,
            ReleasedAtUtc = now,
            ReleasedById = _currentUser.UserId,
            ReleasedByName = ActorName(),
            ReleaseReason = Truncate(reason, 500),
            CorrelationId = correlationId,
            ControlFingerprint = readiness.ControlFingerprint,
            SnapshotJson = snapshotJson,
            IntegrityHash = ComputeHash(snapshotJson),
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        return release;
    }

    private async Task RecordAsync(
        PurchaseRequisition requisition,
        PurchaseRequisitionSourcingReadinessDto readiness,
        string action,
        ProcurementControlEventResult result,
        object? resultValues,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var evidence = readiness.Requirements.Where(item => !string.IsNullOrWhiteSpace(item.EvidenceReference))
            .Select(item => External(item.EvidenceReference!, item.Label, item.Key)).ToList();
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create($"pr-sourcing-{action.ToLowerInvariant()}", requisition.TenantId,
                requisition.Id, _currentUser.UserId, correlationId),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = "PR-SOURCING-READINESS",
            RuleVersion = "1",
            DecisionKeys = ["PR-002", "PR-003", "PR-014"],
            SourceType = SourceType,
            SourceId = requisition.Id,
            SourceReference = requisition.RequisitionNumber,
            Reason = readiness.Message,
            InputValues = new { readiness.ControlFingerprint, requisition.Status, requisition.TotalAmount, requisition.Currency, requisition.ProcurementCategory },
            ResultValues = resultValues ?? new { readiness.IsCompliant, readiness.IsReleased, readiness.DecisionCode, readiness.RequiredActions },
            After = readiness.CurrentRelease,
            Evidence = evidence,
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task<PurchaseRequisition> LoadRequisitionAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = Requisitions.GetQueryable(item => item.Id == id && item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementRequisitionSourcingNotFoundException(
                "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");
    }

    private async Task EnsureCapabilityAsync(string permission, string reference, string correlationId, CancellationToken cancellationToken)
    {
        if (HasPlatformSuperAdministratorBypass()) return;
        try
        {
            var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = SourceType,
                SourceReference = reference
            }, correlationId, cancellationToken);
            if (!decision.Allowed) throw new ProcurementRequisitionSourcingAuthorizationException(decision.Message);
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            throw new ProcurementRequisitionSourcingAuthorizationException(exception.Message);
        }
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read")) return;
        throw new ProcurementRequisitionSourcingAuthorizationException(
            "The procurement records read permission is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementRequisitionSourcingAuthorizationException("An authenticated tenant context is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() => _currentUser.HasRole(Constants.Roles.SuperAdmin);
    private string ActorName() => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private static void Add(List<PurchaseRequisitionSourcingRequirementDto> requirements, string key, string label,
        bool satisfied, string code, string message, string? evidence = null) => requirements.Add(new()
        { Key = key, Label = label, Satisfied = satisfied, Code = satisfied ? $"{key}_SATISFIED" : code, Message = satisfied ? $"{label} is satisfied." : message, EvidenceReference = evidence });
    private static string NormalizeCurrency(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    private static bool CurrentPolicyMatchesRoute(
        ProcurementAuthorityRouteDecisionDto? decision,
        ProcurementRequisitionAuthorityRoute route)
    {
        if (decision is null || !decision.IsReady || decision.Policy is null || decision.Workflow is null ||
            decision.Policy.PolicySetId != route.PolicySetId || decision.Policy.Version != route.PolicyVersion ||
            decision.Policy.SourceConfigurationProfileId != route.SourceConfigurationProfileId ||
            decision.Workflow.WorkflowDefinitionId != route.WorkflowDefinitionId ||
            decision.Workflow.Version != route.WorkflowVersion || decision.Steps.Count == 0 ||
            decision.Steps.Count != route.Steps.Count)
            return false;

        var captured = route.Steps.ToDictionary(item => item.Sequence);
        return decision.Steps.All(item => captured.TryGetValue(item.Sequence, out var step) &&
            step.AuthorityRuleId == item.RuleId && step.RulePolicySetId == item.RulePolicySetId &&
            step.RulePolicyVersion == item.RulePolicyVersion && step.RuleCode == item.RuleCode &&
            step.SourceDecisionKey == item.SourceDecisionKey && step.AuthorityRole == item.AuthorityRole &&
            step.Quorum == item.Quorum && step.IsObserver == item.IsObserver &&
            step.WorkflowDefinitionId == item.WorkflowDefinitionId && step.WorkflowStepId == item.WorkflowStepId &&
            step.WorkflowStepOrder == item.WorkflowStepOrder);
    }
    private static string NormalizeCorrelation(string value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string ComputeHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static ProcurementControlEventEvidenceReference External(string reference, string label, string requirement) => new()
    { ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference, Reference = reference, Label = label, RequirementKey = requirement };

    private static PurchaseRequisitionSourcingReleaseDto Map(ProcurementRequisitionSourcingRelease item, string requisitionNumber) => new()
    {
        Id = item.Id,
        RequisitionId = item.PurchaseRequisitionId,
        RequisitionNumber = requisitionNumber,
        AttemptNumber = item.AttemptNumber,
        ReleaseReference = item.ReleaseReference,
        SourcePlanId = item.SourcePlanId,
        SourcePlanItemId = item.SourcePlanItemId,
        AppSubmissionId = item.AppSubmissionId,
        AppSubmissionAttemptNumber = item.AppSubmissionAttemptNumber,
        AppAcknowledgementReference = item.AppAcknowledgementReference,
        ApprovedExceptionRuleId = item.ApprovedExceptionRuleId,
        ExceptionApprovalReference = item.ExceptionApprovalReference,
        SpecificationTemplateId = item.SpecificationTemplateId,
        SpecificationTemplateCode = item.SpecificationTemplateCode,
        SpecificationTemplateVersion = item.SpecificationTemplateVersion,
        BudgetCommitmentId = item.BudgetCommitmentId,
        BudgetCommitmentReference = item.BudgetCommitmentReference,
        AuthorityRouteId = item.AuthorityRouteId,
        AuthorityRouteReference = item.AuthorityRouteReference,
        WorkflowInstanceId = item.WorkflowInstanceId,
        ReleasedAtUtc = item.ReleasedAtUtc,
        ReleasedById = item.ReleasedById,
        ReleasedByName = item.ReleasedByName,
        ReleaseReason = item.ReleaseReason,
        CorrelationId = item.CorrelationId,
        ControlFingerprint = item.ControlFingerprint,
        IntegrityHash = item.IntegrityHash
    };
}
