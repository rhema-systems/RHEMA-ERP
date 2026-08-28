using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementRequisitionAuthorityRouteService : IProcurementRequisitionAuthorityRouteService
{
    private const string SourceType = "PurchaseRequisition";
    private const string EventType = "PurchaseRequisitionAuthorityRoute";
    private const string SubmitPermission = "procurement.requisition.create";
    private const string ApprovePermission = "procurement.requisition.approve";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementComplianceDecisionService _compliance;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IWorkflowService _workflowService;

    public ProcurementRequisitionAuthorityRouteService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementComplianceDecisionService compliance,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IWorkflowService workflowService)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _compliance = compliance;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _workflowService = workflowService;
    }

    private IGenericRepository<PurchaseRequisition> Requisitions => _unitOfWork.Repository<PurchaseRequisition>();
    private IGenericRepository<ProcurementRequisitionAuthorityRoute> Routes => _unitOfWork.Repository<ProcurementRequisitionAuthorityRoute>();
    private IGenericRepository<ProcurementPolicySet> Policies => _unitOfWork.Repository<ProcurementPolicySet>();
    private IGenericRepository<ProcurementPolicyAuthorityRule> AuthorityRules => _unitOfWork.Repository<ProcurementPolicyAuthorityRule>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions => _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<WorkflowInstance> WorkflowInstances => _unitOfWork.Repository<WorkflowInstance>();

    public async Task<PurchaseRequisitionAuthorityReadinessDto> GetReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return await GetLinkedControlReadinessAsync(requisitionId, cancellationToken);
    }

    public async Task<PurchaseRequisitionAuthorityReadinessDto> GetLinkedControlReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var requisition = await LoadRequisitionAsync(requisitionId, cancellationToken);
        var route = await LoadLatestRouteAsync(requisition.Id, cancellationToken);
        if (!string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase) && route is not null)
            return await MapCapturedReadinessAsync(requisition, route, cancellationToken);

        return MapEvaluation(requisition, await EvaluateAsync(requisition, string.Empty, cancellationToken));
    }

    public async Task<ProcurementAuthorityRouteDecisionDto> EnforceSubmissionAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureRequisition(requisition);
        if (!string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new ProcurementRequisitionAuthorityConflictException(
                "PR_NOT_DRAFT", "Only a Draft purchase requisition can resolve an approval authority route.");
        await EnsureCapabilityAsync(SubmitPermission, requisition.RequisitionNumber, correlationId, cancellationToken);

        var decision = await EvaluateAsync(requisition, correlationId, cancellationToken);
        await RecordEvaluationAsync(requisition, decision, cancellationToken);
        if (!decision.IsReady)
            throw new ProcurementRequisitionAuthorityBlockedException(MapEvaluation(requisition, decision));
        return decision;
    }

    public async Task<ProcurementRequisitionAuthorityRoute> CaptureAsync(
        PurchaseRequisition requisition,
        ProcurementAuthorityRouteDecisionDto decision,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureRequisition(requisition);
        if (!decision.IsReady || decision.Policy is null || decision.Workflow is null || decision.Steps.Count == 0)
            throw new ProcurementRequisitionAuthorityConflictException(
                "PR_AUTHORITY_ROUTE_NOT_READY", "Only a complete, ready authority decision can be captured.");
        if (decision.Category != requisition.ProcurementCategory || decision.Amount != requisition.TotalAmount ||
            !string.Equals(decision.CurrencyCode, NormalizeCurrency(requisition.Currency), StringComparison.OrdinalIgnoreCase))
            throw new ProcurementRequisitionAuthorityConflictException(
                "PR_AUTHORITY_ROUTE_CONTEXT_CHANGED", "The requisition category, amount, or currency changed after authority evaluation.");

        var policy = await Policies.GetQueryable(item => item.Id == decision.Policy.PolicySetId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (policy is null || policy.LifecycleStatus != ProcurementPolicyLifecycleStatus.Published ||
            policy.Version != decision.Policy.Version || policy.EffectiveFrom > decision.PolicyDateUtc ||
            (policy.EffectiveTo.HasValue && policy.EffectiveTo.Value < decision.PolicyDateUtc))
            throw new ProcurementRequisitionAuthorityConflictException(
                "PR_AUTHORITY_POLICY_CHANGED", "The selected procurement policy is no longer the exact Published/effective version that was evaluated.");

        var workflow = await WorkflowDefinitions.GetQueryable(item => item.Id == decision.Workflow.WorkflowDefinitionId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (workflow is null || !WorkflowDefinitionLifecyclePolicy.IsRuntimeEligible(workflow) ||
            workflow.Version != decision.Workflow.Version)
            throw new ProcurementRequisitionAuthorityConflictException(
                "PR_AUTHORITY_WORKFLOW_CHANGED", "The selected shared workflow is no longer the exact Published/active version that was evaluated.");

        var ruleIds = decision.Steps.Select(item => item.RuleId).ToArray();
        var validRuleCount = await AuthorityRules.GetQueryable(item => ruleIds.Contains(item.Id) &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsEnabled &&
                item.EffectiveFrom <= decision.PolicyDateUtc &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= decision.PolicyDateUtc))
            .AsNoTracking().CountAsync(cancellationToken);
        if (validRuleCount != ruleIds.Length)
            throw new ProcurementRequisitionAuthorityConflictException(
                "PR_AUTHORITY_RULE_CHANGED", "One or more selected authority rules are no longer effective for the evaluated policy date.");

        var attemptNumber = await Routes.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId && item.PurchaseRequisitionId == requisition.Id)
            .Select(item => (int?)item.AttemptNumber).MaxAsync(cancellationToken) ?? 0;
        attemptNumber += 1;
        var capturedAt = DateTime.UtcNow;
        var routeId = Guid.NewGuid();
        var snapshot = new
        {
            schemaVersion = "tdc.pr-authority-route.v1",
            routeId,
            purchaseRequisitionId = requisition.Id,
            requisition.RequisitionNumber,
            attemptNumber,
            decision.EvaluationId,
            decision.PolicyDateUtc,
            decision.EvaluatedAtUtc,
            decision.Category,
            decision.Amount,
            decision.CurrencyCode,
            policy = decision.Policy,
            workflow = decision.Workflow,
            steps = decision.Steps.OrderBy(item => item.Sequence).ToArray(),
            capturedAtUtc = capturedAt,
            capturedById = _currentUser.UserId,
            capturedByName = ActorName(),
            correlationId = NormalizeCorrelation(correlationId)
        };
        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        var route = new ProcurementRequisitionAuthorityRoute
        {
            Id = routeId,
            TenantId = _currentUser.TenantId,
            PurchaseRequisitionId = requisition.Id,
            AttemptNumber = attemptNumber,
            RouteReference = Truncate($"ARR-{requisition.RequisitionNumber}-A{attemptNumber}", 100),
            EvaluationId = decision.EvaluationId,
            CorrelationId = NormalizeCorrelation(correlationId),
            PolicySetId = policy.Id,
            PolicyKey = policy.PolicyKey,
            PolicyCode = policy.Code,
            PolicyName = policy.Name,
            PolicyVersion = policy.Version,
            PolicyScopeType = policy.ScopeType,
            SourceConfigurationProfileId = policy.SourceConfigurationProfileId,
            BasePolicySetId = policy.BasePolicySetId,
            Category = decision.Category,
            Amount = decision.Amount,
            CurrencyCode = decision.CurrencyCode,
            PolicyDateUtc = decision.PolicyDateUtc,
            EvaluatedAtUtc = decision.EvaluatedAtUtc,
            WorkflowDefinitionId = workflow.Id,
            WorkflowDefinitionKey = workflow.DefinitionKey,
            WorkflowName = workflow.Name,
            WorkflowVersion = workflow.Version,
            WorkflowEntityTypeCode = decision.Workflow.EntityTypeCode,
            CapturedAtUtc = capturedAt,
            CapturedById = _currentUser.UserId,
            CapturedByName = ActorName(),
            SnapshotJson = snapshotJson,
            IntegrityHash = ComputeHash(snapshotJson),
            CreatedAt = capturedAt,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            Steps = decision.Steps.Select(step => new ProcurementRequisitionAuthorityRouteStep
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                Sequence = step.Sequence,
                AuthorityRuleId = step.RuleId,
                RulePolicySetId = step.RulePolicySetId,
                RulePolicyCode = step.RulePolicyCode,
                RulePolicyVersion = step.RulePolicyVersion,
                RuleCode = step.RuleCode,
                SourceDecisionKey = step.SourceDecisionKey,
                SourceRuleId = step.SourceRuleId,
                AuthorityName = step.AuthorityName,
                AuthorityRole = step.AuthorityRole,
                CurrencyCode = step.CurrencyCode,
                LowerBound = step.LowerBound,
                UpperBound = step.UpperBound,
                LowerInclusive = step.LowerInclusive,
                UpperInclusive = step.UpperInclusive,
                Quorum = step.Quorum,
                IsObserver = step.IsObserver,
                EscalationAuthority = step.EscalationAuthority,
                WorkflowDefinitionId = step.WorkflowDefinitionId,
                WorkflowStepId = step.WorkflowStepId,
                WorkflowStepName = step.WorkflowStepName,
                WorkflowStepOrder = step.WorkflowStepOrder,
                CreatedAt = capturedAt,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId
            }).ToList()
        };
        await Routes.AddAsync(route);

        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("pr-authority-route-capture", requisition.TenantId, route.Id),
            EventType = EventType,
            Action = "AuthorityRouteCaptured",
            Result = ProcurementControlEventResult.Allowed,
            RuleCode = decision.Steps.First().RuleCode,
            RuleId = decision.Steps.First().RuleId,
            RuleVersion = policy.Version.ToString(),
            DecisionKeys = decision.Steps.Select(item => item.SourceDecisionKey)
                .Append("DEC-003").Append("DEC-004").Distinct().ToList(),
            SourceType = SourceType,
            SourceId = requisition.Id,
            SourceReference = requisition.RequisitionNumber,
            Reason = $"Captured immutable authority route {route.RouteReference} using {workflow.Name} v{workflow.Version}.",
            InputValues = new { decision.Category, decision.Amount, decision.CurrencyCode, decision.PolicyDateUtc },
            ResultValues = new { route.Id, route.RouteReference, route.AttemptNumber, route.PolicySetId, route.PolicyVersion, route.WorkflowDefinitionId, route.WorkflowVersion, StepCount = route.Steps.Count, route.IntegrityHash },
            After = snapshot,
            Evidence = [External($"workflow:{workflow.Id:N}", $"{workflow.Name} v{workflow.Version}", "PR_AUTHORITY_WORKFLOW")],
            CorrelationId = route.CorrelationId,
            OccurredAtUtc = capturedAt
        }, cancellationToken);
        return route;
    }

    public async Task<PurchaseRequisitionAuthorityReadinessDto> EnforceApprovalAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureRequisition(requisition, requireDraft: false);
        await EnsureCapabilityAsync(ApprovePermission, requisition.RequisitionNumber, correlationId, cancellationToken);
        var route = await LoadLatestRouteAsync(requisition.Id, cancellationToken)
            ?? throw new ProcurementRequisitionAuthorityBlockedException(BlockedForCapturedRoute(requisition,
                "PR_AUTHORITY_ROUTE_NOT_CAPTURED", "No immutable authority route was captured for this requisition.",
                "Recall the requisition and submit it through the effective authority control."));

        var activeDefinitions = await WorkflowInstances.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.EntityId == requisition.Id && !item.IsDeleted &&
                (item.Status == WorkflowInstanceStatus.Created || item.Status == WorkflowInstanceStatus.InProgress ||
                 item.Status == WorkflowInstanceStatus.Waiting || item.Status == WorkflowInstanceStatus.Suspended))
            .AsNoTracking().Select(item => item.WorkflowDefinitionId).Distinct().ToListAsync(cancellationToken);
        if (activeDefinitions.Count != 1 || activeDefinitions[0] != route.WorkflowDefinitionId)
        {
            var blocked = BlockedForCapturedRoute(requisition, "PR_AUTHORITY_WORKFLOW_INSTANCE_MISMATCH",
                "The active workflow does not use the exact workflow version captured by the authority route.",
                "Restore the captured workflow instance or recall and resubmit the requisition.", route);
            await RecordApprovalGuardAsync(requisition, route, blocked, correlationId, cancellationToken);
            throw new ProcurementRequisitionAuthorityBlockedException(blocked);
        }

        var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = SourceType,
            SourceReference = requisition.RequisitionNumber,
            ProhibitedActorUserIds = [requisition.RequestedById]
        }, NormalizeCorrelation(correlationId), cancellationToken);
        if (!sod.Allowed)
        {
            var blocked = BlockedForCapturedRoute(requisition, sod.Code, sod.Message,
                "Use an independently assigned approver for the current workflow stage.", route);
            throw new ProcurementRequisitionAuthorityBlockedException(blocked);
        }

        var readiness = await MapCapturedReadinessAsync(requisition, route, cancellationToken);
        readiness.DecisionCode = "PR_AUTHORITY_APPROVAL_ALLOWED";
        readiness.Message = "The current actor passed capability, captured-workflow, and initiator/approver SOD checks.";
        await RecordApprovalGuardAsync(requisition, route, readiness, correlationId, cancellationToken);
        return readiness;
    }

    public async Task<ProcurementRequisitionAuthorityRoute?> GetLatestRouteAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return await LoadLatestRouteAsync(requisitionId, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseRequisitionAuthorityRouteHistoryDto>> GetHistoryAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        _ = await LoadRequisitionAsync(requisitionId, cancellationToken);
        var routes = await Routes.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.PurchaseRequisitionId == requisitionId && !item.IsDeleted)
            .Include(item => item.Steps)
            .AsNoTracking()
            .OrderByDescending(item => item.AttemptNumber)
            .ToListAsync(cancellationToken);
        return routes.Select(MapHistory).ToList();
    }

    private async Task<ProcurementAuthorityRouteDecisionDto> EvaluateAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!requisition.ProcurementCategory.HasValue)
            return LocalBlocked(requisition, correlationId, "PR_AUTHORITY_CATEGORY_REQUIRED",
                "A procurement category is required before the approval authority route can be resolved.",
                "Select Goods, Works, Technical Services, or Consultancy Services on the Draft requisition.");
        var currency = NormalizeCurrency(requisition.Currency);
        if (currency.Length != 3 || !currency.All(char.IsLetter))
            return LocalBlocked(requisition, correlationId, "PR_AUTHORITY_CURRENCY_INVALID",
                "The requisition currency must be a three-letter ISO code before authority routing.",
                "Correct the Draft requisition currency and reevaluate the authority route.");

        return await _compliance.EvaluateAuthorityRouteAsync(new ProcurementAuthorityRouteDecisionRequest
        {
            Category = requisition.ProcurementCategory.Value,
            Amount = requisition.TotalAmount,
            CurrencyCode = currency,
            AtUtc = DateTime.UtcNow,
            SourceType = SourceType,
            SourceReference = requisition.RequisitionNumber
        }, NormalizeCorrelation(correlationId), cancellationToken);
    }

    private async Task RecordEvaluationAsync(
        PurchaseRequisition requisition,
        ProcurementAuthorityRouteDecisionDto decision,
        CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("pr-authority-route-evaluation", requisition.TenantId,
                requisition.Id, decision.EvaluationId),
            EventType = EventType,
            Action = decision.IsReady ? "AuthorityRouteResolved" : "AuthorityRouteBlocked",
            Result = decision.IsReady ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
            RuleCode = decision.Steps.FirstOrDefault()?.RuleCode ?? decision.DecisionCode,
            RuleId = decision.Steps.FirstOrDefault()?.RuleId,
            RuleVersion = decision.Policy?.Version.ToString(),
            DecisionKeys = decision.Steps.Select(item => item.SourceDecisionKey)
                .Append("DEC-002").Append("DEC-003").Append("DEC-004").Distinct().ToList(),
            SourceType = SourceType,
            SourceId = requisition.Id,
            SourceReference = requisition.RequisitionNumber,
            Reason = decision.Message,
            InputValues = new { decision.Category, decision.Amount, decision.CurrencyCode, decision.PolicyDateUtc },
            ResultValues = new { decision.IsReady, decision.DecisionCode, PolicySetId = decision.Policy?.PolicySetId, PolicyVersion = decision.Policy?.Version, WorkflowDefinitionId = decision.Workflow?.WorkflowDefinitionId, WorkflowVersion = decision.Workflow?.Version, StepCount = decision.Steps.Count, decision.RequiredActions },
            Evidence = decision.Workflow is null
                ? new List<ProcurementControlEventEvidenceReference>()
                : [External($"workflow:{decision.Workflow.WorkflowDefinitionId:N}", $"{decision.Workflow.Name} v{decision.Workflow.Version}", "PR_AUTHORITY_WORKFLOW")],
            CorrelationId = decision.CorrelationId,
            OccurredAtUtc = decision.EvaluatedAtUtc
        }, cancellationToken);
    }

    private async Task RecordApprovalGuardAsync(
        PurchaseRequisition requisition,
        ProcurementRequisitionAuthorityRoute route,
        PurchaseRequisitionAuthorityReadinessDto readiness,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("pr-authority-approval-guard", requisition.TenantId,
                requisition.Id, _currentUser.UserId, NormalizeCorrelation(correlationId)),
            EventType = EventType,
            Action = "AuthorityApprovalGuard",
            Result = readiness.IsCompliant ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
            RuleCode = route.Steps.OrderBy(item => item.Sequence).First().RuleCode,
            RuleId = route.Steps.OrderBy(item => item.Sequence).First().AuthorityRuleId,
            RuleVersion = route.PolicyVersion.ToString(),
            DecisionKeys = route.Steps.Select(item => item.SourceDecisionKey).Append("DEC-004").Distinct().ToList(),
            SourceType = SourceType,
            SourceId = requisition.Id,
            SourceReference = requisition.RequisitionNumber,
            Reason = readiness.Message,
            InputValues = new { ActorUserId = _currentUser.UserId, requisition.RequestedById, route.Id, route.WorkflowDefinitionId },
            ResultValues = new { readiness.IsCompliant, readiness.DecisionCode, readiness.CurrentWorkflowStage, readiness.CurrentWorkflowStageStatus },
            Evidence = [External($"workflow:{route.WorkflowDefinitionId:N}", $"{route.WorkflowName} v{route.WorkflowVersion}", "PR_AUTHORITY_WORKFLOW")],
            CorrelationId = NormalizeCorrelation(correlationId),
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private PurchaseRequisitionAuthorityReadinessDto MapEvaluation(
        PurchaseRequisition requisition,
        ProcurementAuthorityRouteDecisionDto decision) => new()
    {
        RequisitionId = requisition.Id,
        RequisitionNumber = requisition.RequisitionNumber,
        Status = requisition.Status,
        IsCompliant = decision.IsReady,
        CanSubmit = decision.IsReady && string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase),
        DecisionCode = decision.DecisionCode,
        Message = decision.Message,
        Category = requisition.ProcurementCategory,
        Amount = requisition.TotalAmount,
        CurrencyCode = NormalizeCurrency(requisition.Currency),
        PolicySetId = decision.Policy?.PolicySetId,
        PolicyCode = decision.Policy?.PolicyCode,
        PolicyName = decision.Policy?.PolicyName,
        PolicyVersion = decision.Policy?.Version,
        WorkflowDefinitionId = decision.Workflow?.WorkflowDefinitionId,
        WorkflowName = decision.Workflow?.Name,
        WorkflowVersion = decision.Workflow?.Version,
        Steps = decision.Steps.Select(MapStep).ToList(),
        Findings = decision.Findings.ToList(),
        RequiredActions = decision.RequiredActions.ToList()
    };

    private async Task<PurchaseRequisitionAuthorityReadinessDto> MapCapturedReadinessAsync(
        PurchaseRequisition requisition,
        ProcurementRequisitionAuthorityRoute route,
        CancellationToken cancellationToken)
    {
        WorkflowStepInfo? current = null;
        try
        {
            current = await _workflowService.GetCurrentWorkflowStepAsync(SourceType, requisition.Id);
        }
        catch (InvalidOperationException)
        {
            // The immutable route remains readable even if the operational workflow is unavailable.
        }
        return new PurchaseRequisitionAuthorityReadinessDto
        {
            RequisitionId = requisition.Id,
            RequisitionNumber = requisition.RequisitionNumber,
            Status = requisition.Status,
            IsCompliant = true,
            CanSubmit = false,
            DecisionCode = "PR_AUTHORITY_ROUTE_CAPTURED",
            Message = $"Immutable authority route {route.RouteReference} is bound to {route.WorkflowName} v{route.WorkflowVersion}.",
            Category = route.Category,
            Amount = route.Amount,
            CurrencyCode = route.CurrencyCode,
            PolicySetId = route.PolicySetId,
            PolicyCode = route.PolicyCode,
            PolicyName = route.PolicyName,
            PolicyVersion = route.PolicyVersion,
            WorkflowDefinitionId = route.WorkflowDefinitionId,
            WorkflowName = route.WorkflowName,
            WorkflowVersion = route.WorkflowVersion,
            AuthorityRouteId = route.Id,
            RouteReference = route.RouteReference,
            AttemptNumber = route.AttemptNumber,
            CapturedAtUtc = route.CapturedAtUtc,
            IntegrityHash = route.IntegrityHash,
            CurrentWorkflowStage = current?.StepName,
            CurrentWorkflowStageStatus = current?.Status,
            Steps = route.Steps.OrderBy(item => item.Sequence).Select(MapStep).ToList()
        };
    }

    private static PurchaseRequisitionAuthorityReadinessDto BlockedForCapturedRoute(
        PurchaseRequisition requisition,
        string code,
        string message,
        string requiredAction,
        ProcurementRequisitionAuthorityRoute? route = null) => new()
    {
        RequisitionId = requisition.Id,
        RequisitionNumber = requisition.RequisitionNumber,
        Status = requisition.Status,
        IsCompliant = false,
        CanSubmit = false,
        DecisionCode = code,
        Message = message,
        Category = requisition.ProcurementCategory,
        Amount = requisition.TotalAmount,
        CurrencyCode = NormalizeCurrency(requisition.Currency),
        AuthorityRouteId = route?.Id,
        RouteReference = route?.RouteReference,
        AttemptNumber = route?.AttemptNumber,
        PolicySetId = route?.PolicySetId,
        PolicyCode = route?.PolicyCode,
        PolicyName = route?.PolicyName,
        PolicyVersion = route?.PolicyVersion,
        WorkflowDefinitionId = route?.WorkflowDefinitionId,
        WorkflowName = route?.WorkflowName,
        WorkflowVersion = route?.WorkflowVersion,
        Findings = [new ProcurementComplianceFindingDto { Code = code, Message = message, Severity = ProcurementComplianceFindingSeverity.HardStop }],
        RequiredActions = [requiredAction]
    };

    private static PurchaseRequisitionAuthorityStepDto MapStep(ProcurementAuthorityRouteStepDecisionDto step) => new()
    {
        Sequence = step.Sequence,
        RuleId = step.RuleId,
        RuleCode = step.RuleCode,
        RulePolicyCode = step.RulePolicyCode,
        RulePolicyVersion = step.RulePolicyVersion,
        SourceDecisionKey = step.SourceDecisionKey,
        AuthorityName = step.AuthorityName,
        AuthorityRole = step.AuthorityRole,
        Quorum = step.Quorum,
        IsObserver = step.IsObserver,
        EscalationAuthority = step.EscalationAuthority,
        CurrencyCode = step.CurrencyCode,
        LowerBound = step.LowerBound,
        UpperBound = step.UpperBound,
        LowerInclusive = step.LowerInclusive,
        UpperInclusive = step.UpperInclusive,
        WorkflowStepId = step.WorkflowStepId,
        WorkflowStepName = step.WorkflowStepName,
        WorkflowStepOrder = step.WorkflowStepOrder
    };

    private static PurchaseRequisitionAuthorityStepDto MapStep(ProcurementRequisitionAuthorityRouteStep step) => new()
    {
        Sequence = step.Sequence,
        RuleId = step.AuthorityRuleId,
        RuleCode = step.RuleCode,
        RulePolicyCode = step.RulePolicyCode,
        RulePolicyVersion = step.RulePolicyVersion,
        SourceDecisionKey = step.SourceDecisionKey,
        AuthorityName = step.AuthorityName,
        AuthorityRole = step.AuthorityRole,
        Quorum = step.Quorum,
        IsObserver = step.IsObserver,
        EscalationAuthority = step.EscalationAuthority,
        CurrencyCode = step.CurrencyCode,
        LowerBound = step.LowerBound,
        UpperBound = step.UpperBound,
        LowerInclusive = step.LowerInclusive,
        UpperInclusive = step.UpperInclusive,
        WorkflowStepId = step.WorkflowStepId,
        WorkflowStepName = step.WorkflowStepName,
        WorkflowStepOrder = step.WorkflowStepOrder
    };

    private static PurchaseRequisitionAuthorityRouteHistoryDto MapHistory(ProcurementRequisitionAuthorityRoute route) => new()
    {
        Id = route.Id,
        RouteReference = route.RouteReference,
        AttemptNumber = route.AttemptNumber,
        PolicyCode = route.PolicyCode,
        PolicyName = route.PolicyName,
        PolicyVersion = route.PolicyVersion,
        WorkflowName = route.WorkflowName,
        WorkflowVersion = route.WorkflowVersion,
        Category = route.Category,
        Amount = route.Amount,
        CurrencyCode = route.CurrencyCode,
        CapturedAtUtc = route.CapturedAtUtc,
        CapturedByName = route.CapturedByName,
        CorrelationId = route.CorrelationId,
        IntegrityHash = route.IntegrityHash,
        Steps = route.Steps.OrderBy(item => item.Sequence).Select(MapStep).ToList()
    };

    private ProcurementAuthorityRouteDecisionDto LocalBlocked(
        PurchaseRequisition requisition,
        string correlationId,
        string code,
        string message,
        string requiredAction) => new()
    {
        EvaluationId = Guid.NewGuid(),
        EvaluatedAtUtc = DateTime.UtcNow,
        PolicyDateUtc = DateTime.UtcNow,
        CorrelationId = NormalizeCorrelation(correlationId),
        IsReady = false,
        DecisionCode = code,
        Message = message,
        Category = requisition.ProcurementCategory ?? default,
        Amount = requisition.TotalAmount,
        CurrencyCode = NormalizeCurrency(requisition.Currency),
        Findings = [new ProcurementComplianceFindingDto { Code = code, Message = message, Severity = ProcurementComplianceFindingSeverity.HardStop }],
        RequiredActions = [requiredAction]
    };

    private async Task<PurchaseRequisition> LoadRequisitionAsync(Guid id, CancellationToken cancellationToken) =>
        await Requisitions.GetQueryable(item => item.Id == id && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
        ?? throw new ProcurementRequisitionAuthorityNotFoundException(
            "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");

    private Task<ProcurementRequisitionAuthorityRoute?> LoadLatestRouteAsync(Guid requisitionId, CancellationToken cancellationToken) =>
        Routes.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.PurchaseRequisitionId == requisitionId && !item.IsDeleted)
            .Include(item => item.Steps)
            .AsNoTracking()
            .OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task EnsureCapabilityAsync(
        string permissionCode,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permissionCode,
            SourceType = SourceType,
            SourceReference = sourceReference
        }, NormalizeCorrelation(correlationId), cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementRequisitionAuthorityAuthorizationException(decision.Message);
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator() || _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role => ProcurementAccessControlRegistry.FindRole(role) is not null)) return;
        throw new ProcurementRequisitionAuthorityAuthorizationException(
            "A TDC procurement role or tenant-administration role is required.");
    }

    private void EnsureRequisition(PurchaseRequisition requisition, bool requireDraft = true)
    {
        EnsureAuthenticatedTenant();
        if (requisition.TenantId != _currentUser.TenantId || requisition.IsDeleted)
            throw new ProcurementRequisitionAuthorityNotFoundException(
                "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");
        if (requireDraft && !string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new ProcurementRequisitionAuthorityConflictException(
                "PR_NOT_DRAFT", "Only a Draft purchase requisition can capture an authority route.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementRequisitionAuthorityAuthorizationException("An authenticated tenant context is required.");
    }

    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");
    private string ActorName() => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private static string NormalizeCurrency(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    private static string NormalizeCorrelation(string correlationId) => string.IsNullOrWhiteSpace(correlationId)
        ? Guid.NewGuid().ToString("N") : Truncate(correlationId.Trim(), 100);
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string ComputeHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static ProcurementControlEventEvidenceReference External(string reference, string label, string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference,
        Label = label,
        RequirementKey = requirement
    };
}
