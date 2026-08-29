using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementRequisitionSubmissionControlService : IProcurementRequisitionSubmissionControlService
{
    private const string SourceType = "PurchaseRequisition";
    private const string EventType = "PurchaseRequisitionSubmissionControl";
    private const string SubmitPermission = "procurement.requisition.create";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;

    public ProcurementRequisitionSubmissionControlService(
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
    private IGenericRepository<ProcurementAppSubmission> AppSubmissions => _unitOfWork.Repository<ProcurementAppSubmission>();
    private IGenericRepository<ProcurementPolicyExceptionRule> ExceptionRules => _unitOfWork.Repository<ProcurementPolicyExceptionRule>();
    private IGenericRepository<WorkflowInstance> WorkflowInstances => _unitOfWork.Repository<WorkflowInstance>();
    private IGenericRepository<ProcurementControlEvent> Events => _unitOfWork.Repository<ProcurementControlEvent>();

    public async Task<PurchaseRequisitionSubmissionReadinessDto> GetReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return await GetLinkedControlReadinessAsync(requisitionId, cancellationToken);
    }

    public async Task<PurchaseRequisitionSubmissionReadinessDto> GetLinkedControlReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var requisition = await Requisitions.GetQueryable(item => item.Id == requisitionId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Items)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementRequisitionSubmissionNotFoundException(
                "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");
        return (await EvaluateAsync(requisition, cancellationToken)).Readiness;
    }

    public async Task<PurchaseRequisitionSubmissionReadinessDto> EnforceAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (requisition.TenantId != _currentUser.TenantId || requisition.IsDeleted)
            throw new ProcurementRequisitionSubmissionNotFoundException(
                "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");
        await EnsureSubmitCapabilityAsync(requisition.RequisitionNumber, correlationId, cancellationToken);

        var evaluation = await EvaluateAsync(requisition, cancellationToken);
        if (!string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            evaluation.Readiness.IsCompliant = false;
            evaluation.Readiness.CanSubmit = false;
            evaluation.Readiness.DecisionCode = "PR_NOT_DRAFT";
            evaluation.Readiness.Message = $"Purchase requisition cannot be submitted in current status: {requisition.Status}.";
            evaluation.Readiness.RequiredActions = ["Only a Draft purchase requisition can be submitted for approval."];
        }

        await RecordDecisionAsync(requisition, evaluation, correlationId, cancellationToken);
        if (!evaluation.Readiness.CanSubmit)
            throw new ProcurementRequisitionSubmissionBlockedException(evaluation.Readiness);
        return evaluation.Readiness;
    }

    public async Task<IReadOnlyList<PurchaseRequisitionSubmissionControlHistoryDto>> GetHistoryAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var exists = await Requisitions.GetQueryable(item => item.Id == requisitionId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().AnyAsync(cancellationToken);
        if (!exists)
            throw new ProcurementRequisitionSubmissionNotFoundException(
                "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");

        return await Events.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.EventType == EventType && item.SourceId == requisitionId)
            .AsNoTracking().OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.CreatedAt)
            .Select(item => new PurchaseRequisitionSubmissionControlHistoryDto
            {
                Id = item.Id,
                Action = item.Action,
                Result = item.Result.ToString(),
                ActorName = item.ActorName,
                RuleCode = item.RuleCode,
                Reason = item.Reason,
                OccurredAtUtc = item.OccurredAtUtc,
                IntegrityHash = item.IntegrityHash
            }).ToListAsync(cancellationToken);
    }

    private async Task<Evaluation> EvaluateAsync(
        PurchaseRequisition requisition,
        CancellationToken cancellationToken)
    {
        // APP exchange and governed exceptions are retained as traceable planning
        // metadata. The approved business requirements do not make either one a
        // prerequisite for sending a purchase requisition into its configured
        // approval workflow.
        var app = await EvaluateAppAsync(requisition, cancellationToken);
        var exception = await EvaluateExceptionAsync(requisition, cancellationToken);
        var statusAllowsSubmission = string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase);
        var requiredActions = new List<string>();
        if (!statusAllowsSubmission)
            requiredActions.Add("Only a Draft purchase requisition can be submitted for approval.");
        if (string.IsNullOrWhiteSpace(requisition.Department))
            requiredActions.Add("Select or derive the requesting department.");
        if (!requisition.RequiredDate.HasValue)
            requiredActions.Add("Enter the required delivery date.");
        if (string.IsNullOrWhiteSpace(requisition.Justification))
            requiredActions.Add("Enter the business justification.");
        if (!requisition.BudgetId.HasValue || requisition.BudgetId == Guid.Empty)
            requiredActions.Add("Link an approved budget or budget line.");
        if (!requisition.ProcurementCategory.HasValue)
            requiredActions.Add("Select the procurement category.");
        if (string.IsNullOrWhiteSpace(requisition.Currency))
            requiredActions.Add("Select the requisition currency.");
        if (requisition.TotalAmount <= 0)
            requiredActions.Add("The requisition total must be greater than zero.");

        var activeItems = requisition.Items
            .Where(item => !item.IsDeleted &&
                !string.Equals(item.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (activeItems.Count == 0)
        {
            requiredActions.Add("Add at least one requisition item.");
        }
        else
        {
            if (activeItems.Any(item => string.IsNullOrWhiteSpace(item.ItemDescription)))
                requiredActions.Add("Every item must have a description.");
            if (activeItems.Any(item => item.Quantity <= 0 || string.IsNullOrWhiteSpace(item.UnitOfMeasure)))
                requiredActions.Add("Every item must have a positive quantity and unit of measure.");
            if (activeItems.Any(item => item.EstimatedUnitPrice <= 0 || item.LineTotal <= 0))
                requiredActions.Add("Every item must have a positive estimated unit cost and line total.");
            if (!requisition.SpecificationTemplateId.HasValue &&
                activeItems.Any(item => string.IsNullOrWhiteSpace(item.Specifications)))
                requiredActions.Add("Add specifications to every item or select a requisition specification template.");
        }

        var isCompliant = requiredActions.Count == 0;
        if (isCompliant && exception.Attempted && !exception.Allowed)
        {
            requiredActions.Add(exception.Action);
            return new Evaluation(BuildReadiness(
                requisition,
                false,
                false,
                exception.Code,
                exception.Message,
                null,
                app,
                exception,
                requiredActions.Distinct(StringComparer.Ordinal).ToList()), app, exception);
        }

        if (isCompliant && app.Allowed)
        {
            return new Evaluation(BuildReadiness(
                requisition,
                true,
                statusAllowsSubmission,
                "PR_APP_ACKNOWLEDGED",
                $"The purchase requisition has the required business details. APP attempt {app.Submission!.SubmissionNumber}/A{app.Submission.AttemptNumber} is acknowledged and retained for GHANEPS traceability.",
                "AcknowledgedAPP",
                app,
                exception,
                []), app, exception);
        }

        if (isCompliant && exception.Allowed)
        {
            return new Evaluation(BuildReadiness(
                requisition,
                true,
                statusAllowsSubmission,
                "PR_APPROVED_EXCEPTION",
                $"The purchase requisition has the required business details and approved exception {exception.Rule!.RuleCode} is backed by its completed workflow.",
                "ApprovedException",
                app,
                exception,
                []), app, exception);
        }

        var appNote = app.Allowed
            ? $" APP attempt {app.Submission!.SubmissionNumber}/A{app.Submission.AttemptNumber} is acknowledged and retained for GHANEPS traceability."
            : requisition.SourcePlanId.HasValue
                ? " APP exchange status is retained for planning traceability and does not block this approval workflow."
                : string.Empty;
        var message = isCompliant
            ? $"The purchase requisition has the required business details and is ready for its configured approval workflow.{appNote}"
            : "Complete the listed purchase requisition details before submission.";
        return new Evaluation(BuildReadiness(
            requisition,
            isCompliant,
            isCompliant && statusAllowsSubmission,
            isCompliant ? "PR_SUBMISSION_READY" : "PR_REQUIRED_DETAILS_INCOMPLETE",
            message,
            isCompliant ? "ConfiguredApprovalWorkflow" : "BusinessRequirements",
            app,
            exception,
            requiredActions.Distinct(StringComparer.Ordinal).ToList()), app, exception);
    }

    private async Task<AppPath> EvaluateAppAsync(
        PurchaseRequisition requisition,
        CancellationToken cancellationToken)
    {
        const string defaultAction = "Link every requisition line to a non-cancelled item in one approved, published Active plan.";
        var sourcePlanItemIds = requisition.Items.Where(line => !line.IsDeleted &&
                !string.Equals(line.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                line.SourcePlanItemId.HasValue)
            .Select(line => line.SourcePlanItemId!.Value).Distinct().ToList();
        if (sourcePlanItemIds.Count == 0 && requisition.SourcePlanItemId.HasValue)
            sourcePlanItemIds.Add(requisition.SourcePlanItemId.Value);
        if (sourcePlanItemIds.Count == 0)
            return AppPath.Missing("PR_PLAN_ITEM_REQUIRED", "No procurement-plan item is linked.", defaultAction);

        var items = await PlanItems.GetQueryable(row => sourcePlanItemIds.Contains(row.Id) &&
                row.TenantId == _currentUser.TenantId && !row.IsDeleted)
            .Include(row => row.ProcurementPlan).AsNoTracking().ToListAsync(cancellationToken);
        var item = items.FirstOrDefault();
        if (items.Count != sourcePlanItemIds.Count || item is null || items.Any(row => row.ProcurementPlan.IsDeleted))
            return AppPath.Invalid("PR_PLAN_ITEM_NOT_ELIGIBLE", "One or more linked plan items are not available in the current tenant.", defaultAction, item);
        if (items.Any(row => requisition.SourcePlanId != row.ProcurementPlanId))
            return AppPath.Invalid("PR_PLAN_ITEM_MISMATCH", "The requisition plan snapshot does not match every linked plan item.", defaultAction, item);
        if (items.Any(row => string.Equals(row.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)))
            return AppPath.Invalid("PR_PLAN_ITEM_CANCELLED", "A linked plan item is cancelled.", defaultAction, item);
        if (items.Any(row => !string.Equals(row.ProcurementPlan.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
            !row.ProcurementPlan.PublishedDate.HasValue))
            return AppPath.Invalid("PR_PLAN_NOT_PUBLISHED", "Every linked plan item must belong to the same approved, published Active plan.", defaultAction, item);

        var latest = await AppSubmissions.GetQueryable(row => row.ProcurementPlanId == item.ProcurementPlanId &&
                row.TenantId == _currentUser.TenantId && !row.IsDeleted)
            .AsNoTracking().OrderByDescending(row => row.AttemptNumber).ThenByDescending(row => row.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is null)
            return AppPath.Invalid("PR_APP_SUBMISSION_REQUIRED", "The linked plan version has no APP submission attempt.", defaultAction, item);
        if (latest.Status != ProcurementAppSubmissionStatus.Acknowledged)
            return AppPath.Invalid(
                "PR_APP_ACKNOWLEDGEMENT_REQUIRED",
                $"The latest APP attempt {latest.SubmissionNumber}/A{latest.AttemptNumber} is {latest.Status}, not Acknowledged.",
                "Complete and record acknowledgement of the latest APP submission attempt, or link an approved exception.", item, latest);
        if (!latest.AcknowledgedAtUtc.HasValue || string.IsNullOrWhiteSpace(latest.AcknowledgementReference))
            return AppPath.Invalid(
                "PR_APP_ACKNOWLEDGEMENT_INCOMPLETE",
                "The latest APP attempt is marked Acknowledged but its acknowledgement reference or timestamp is missing.",
                "Complete the APP acknowledgement record, or link an approved exception.", item, latest);
        return AppPath.Allow(item, latest);
    }

    private async Task<ExceptionPath> EvaluateExceptionAsync(
        PurchaseRequisition requisition,
        CancellationToken cancellationToken)
    {
        const string defaultAction = "Alternatively, link an effective approved exception rule and its completed Procurement Exception workflow for this requisition.";
        if (!requisition.ApprovedExceptionRuleId.HasValue)
            return ExceptionPath.Missing("PR_EXCEPTION_REQUIRED", "No approved exception is linked.", defaultAction);

        var now = DateTime.UtcNow;
        var rule = await ExceptionRules.GetQueryable(row => row.Id == requisition.ApprovedExceptionRuleId.Value &&
                row.TenantId == _currentUser.TenantId && !row.IsDeleted)
            .Include(row => row.PolicySet).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (rule is null || rule.PolicySet.IsDeleted)
            return ExceptionPath.Invalid("PR_EXCEPTION_RULE_NOT_FOUND", "The linked exception rule is not available in the current tenant.", defaultAction, rule);
        if (!rule.IsEnabled || rule.EffectiveFrom > now || rule.EffectiveTo.HasValue && rule.EffectiveTo.Value < now ||
            rule.PolicySet.LifecycleStatus != ProcurementPolicyLifecycleStatus.Published ||
            rule.PolicySet.EffectiveFrom > now ||
            rule.PolicySet.EffectiveTo.HasValue && rule.PolicySet.EffectiveTo.Value < now)
            return ExceptionPath.Invalid("PR_EXCEPTION_RULE_NOT_EFFECTIVE", "The linked exception rule is not currently effective in a Published policy set.", defaultAction, rule);
        if (rule.Category.HasValue && rule.Category != requisition.ProcurementCategory)
            return ExceptionPath.Invalid("PR_EXCEPTION_CATEGORY_MISMATCH", "The approved exception does not apply to this requisition category.", defaultAction, rule);
        if (rule.JustificationRequired && string.IsNullOrWhiteSpace(requisition.Justification))
            return ExceptionPath.Invalid("PR_EXCEPTION_JUSTIFICATION_REQUIRED", "The approved exception rule requires requisition justification.", "Add the required justification to the Draft requisition.", rule);
        if (rule.EvidenceRequired && string.IsNullOrWhiteSpace(requisition.ExceptionEvidenceReference))
            return ExceptionPath.Invalid("PR_EXCEPTION_EVIDENCE_REQUIRED", "The approved exception rule requires an evidence reference.", "Link the approved exception evidence through the existing shared evidence control.", rule);
        if (!requisition.ExceptionWorkflowInstanceId.HasValue)
            return ExceptionPath.Invalid("PR_EXCEPTION_WORKFLOW_REQUIRED", "The approved exception has no shared workflow instance.", defaultAction, rule);

        var workflow = await WorkflowInstances.GetQueryable(row => row.Id == requisition.ExceptionWorkflowInstanceId.Value &&
                row.TenantId == _currentUser.TenantId && !row.IsDeleted)
            .Include(row => row.EntityType).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (workflow is null)
            return ExceptionPath.Invalid("PR_EXCEPTION_WORKFLOW_NOT_FOUND", "The linked Procurement Exception workflow was not found in the current tenant.", defaultAction, rule);
        if (!string.Equals(workflow.EntityType.Code, "PROCUREMENT_EXCEPTION", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(workflow.EntityType.Code, "ProcurementException", StringComparison.OrdinalIgnoreCase))
            return ExceptionPath.Invalid("PR_EXCEPTION_WORKFLOW_TYPE_INVALID", "The linked workflow is not a Procurement Exception workflow.", defaultAction, rule, workflow);
        if (workflow.EntityId != requisition.Id)
            return ExceptionPath.Invalid("PR_EXCEPTION_WORKFLOW_SUBJECT_MISMATCH", "The completed exception workflow was not approved for this purchase requisition.", "Complete a Procurement Exception workflow whose subject is this Draft requisition.", rule, workflow);
        if (workflow.Status != WorkflowInstanceStatus.Completed || !workflow.CompletedDate.HasValue)
            return ExceptionPath.Invalid("PR_EXCEPTION_WORKFLOW_NOT_APPROVED", "The Procurement Exception workflow has not completed with an approved outcome and timestamp.", defaultAction, rule, workflow);
        if (rule.WorkflowDefinitionId.HasValue && workflow.WorkflowDefinitionId != rule.WorkflowDefinitionId.Value)
            return ExceptionPath.Invalid("PR_EXCEPTION_WORKFLOW_DEFINITION_MISMATCH", "The completed exception workflow does not use the workflow definition configured by the exception rule.", defaultAction, rule, workflow);
        if (rule.MaximumDurationDays.HasValue && workflow.CompletedDate.Value.AddDays(rule.MaximumDurationDays.Value) < now)
            return ExceptionPath.Invalid("PR_EXCEPTION_APPROVAL_EXPIRED", "The approved exception has expired under its configured maximum duration.", "Obtain a new approved Procurement Exception workflow for this requisition.", rule, workflow);
        if (string.IsNullOrWhiteSpace(requisition.ExceptionApprovalReference) || !requisition.ExceptionApprovedAtUtc.HasValue)
            return ExceptionPath.Invalid("PR_EXCEPTION_APPROVAL_INCOMPLETE", "The exception approval reference or approval timestamp is missing from the requisition linkage.", defaultAction, rule, workflow);
        return ExceptionPath.Allow(rule, workflow);
    }

    private static PurchaseRequisitionSubmissionReadinessDto BuildReadiness(
        PurchaseRequisition requisition,
        bool isCompliant,
        bool canSubmit,
        string code,
        string message,
        string? basis,
        AppPath app,
        ExceptionPath exception,
        List<string> actions) => new()
    {
        RequisitionId = requisition.Id,
        RequisitionNumber = requisition.RequisitionNumber,
        Status = requisition.Status,
        IsCompliant = isCompliant,
        CanSubmit = canSubmit,
        DecisionCode = code,
        Message = message,
        Basis = basis,
        SourcePlanId = requisition.SourcePlanId,
        SourcePlanItemId = requisition.SourcePlanItemId,
        SourcePlanNumber = requisition.SourcePlanNumber,
        SourcePlanItemDescription = requisition.SourcePlanItemDescription,
        AppSubmissionId = app.Submission?.Id,
        AppSubmissionNumber = app.Submission?.SubmissionNumber,
        AppSubmissionAttemptNumber = app.Submission?.AttemptNumber,
        AppSubmissionStatus = app.Submission?.Status.ToString(),
        AppAcknowledgementReference = app.Submission?.AcknowledgementReference,
        AppAcknowledgedAtUtc = app.Submission?.AcknowledgedAtUtc,
        ApprovedExceptionRuleId = requisition.ApprovedExceptionRuleId,
        ApprovedExceptionRuleCode = requisition.ApprovedExceptionRuleCode,
        ExceptionWorkflowInstanceId = requisition.ExceptionWorkflowInstanceId,
        ExceptionApprovalReference = requisition.ExceptionApprovalReference,
        ExceptionEvidenceReference = requisition.ExceptionEvidenceReference,
        ExceptionApprovedAtUtc = requisition.ExceptionApprovedAtUtc,
        RequiredActions = actions
    };

    private async Task RecordDecisionAsync(
        PurchaseRequisition requisition,
        Evaluation evaluation,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var allowed = evaluation.Readiness.CanSubmit;
        var evidence = BuildEvidence(evaluation);
        var decisionKeys = new List<string> { "PR-001", "PR-002", "PR-003" };
        if (evaluation.App.Attempted)
            decisionKeys.Add("DEC-009");
        if (evaluation.Exception.Attempted)
            decisionKeys.Add(evaluation.Exception.Rule?.SourceDecisionKey ?? "DEC-006");
        var exceptionRule = evaluation.Exception.Rule;
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("pr-submission-control", requisition.TenantId,
                requisition.Id, allowed ? "allowed" : "denied", Guid.NewGuid()),
            EventType = EventType,
            Action = allowed ? "SubmissionGateAllowed" : "SubmissionGateBlocked",
            Result = allowed ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
            RuleCode = exceptionRule?.RuleCode ?? "PR-SUBMISSION-REQUIREMENTS",
            RuleId = exceptionRule?.Id,
            RuleVersion = exceptionRule?.PolicySet.Version.ToString(),
            DecisionKeys = decisionKeys.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourceType = SourceType,
            SourceId = requisition.Id,
            SourceReference = requisition.RequisitionNumber,
            Reason = evaluation.Readiness.Message,
            InputValues = new
            {
                requisition.SourcePlanId,
                requisition.SourcePlanItemId,
                requisition.ApprovedExceptionRuleId,
                requisition.ExceptionWorkflowInstanceId
            },
            ResultValues = evaluation.Readiness,
            CorrelationId = NormalizeCorrelation(correlationId),
            CausationId = NormalizeCorrelation(correlationId),
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = evidence
        }, cancellationToken);
    }

    private static List<ProcurementControlEventEvidenceReference> BuildEvidence(Evaluation evaluation)
    {
        var values = new List<ProcurementControlEventEvidenceReference>();
        if (!string.IsNullOrWhiteSpace(evaluation.App.Submission?.AcknowledgementReference))
            values.Add(External(evaluation.App.Submission.AcknowledgementReference, "APP acknowledgement", "APP_ACKNOWLEDGEMENT"));
        if (evaluation.Exception.Workflow is not null)
            values.Add(External($"workflow:{evaluation.Exception.Workflow.Id:N}", "Procurement Exception workflow", "PROCUREMENT_EXCEPTION_APPROVAL"));
        if (!string.IsNullOrWhiteSpace(evaluation.Readiness.ExceptionApprovalReference))
            values.Add(External(evaluation.Readiness.ExceptionApprovalReference, "Exception approval reference", "PROCUREMENT_EXCEPTION_APPROVAL"));
        if (!string.IsNullOrWhiteSpace(evaluation.Readiness.ExceptionEvidenceReference))
            values.Add(External(evaluation.Readiness.ExceptionEvidenceReference, "Approved exception evidence", "PROCUREMENT_EXCEPTION_EVIDENCE"));
        return values.GroupBy(item => $"{(int)item.ReferenceKind}:{item.Reference}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).ToList();
    }

    private static ProcurementControlEventEvidenceReference External(string reference, string label, string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference,
        Label = label,
        RequirementKey = requirement
    };

    private async Task EnsureSubmitCapabilityAsync(
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = SubmitPermission,
            SourceType = SourceType,
            SourceReference = sourceReference
        }, NormalizeCorrelation(correlationId), cancellationToken);
        if (!decision.Allowed) throw new ProcurementRequisitionSubmissionAuthorizationException(decision.Message);
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator() || _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role => ProcurementAccessControlRegistry.FindRole(role) is not null)) return;
        throw new ProcurementRequisitionSubmissionAuthorizationException(
            "A TDC procurement role or tenant-administration role is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementRequisitionSubmissionAuthorizationException("An authenticated tenant context is required.");
    }

    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");
    private static string NormalizeCorrelation(string correlationId) =>
        string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : Truncate(correlationId.Trim(), 100);
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    private sealed record Evaluation(
        PurchaseRequisitionSubmissionReadinessDto Readiness,
        AppPath App,
        ExceptionPath Exception);

    private sealed record AppPath(
        bool Attempted,
        bool Allowed,
        string Code,
        string Message,
        string Action,
        ProcurementPlanItem? Item,
        ProcurementAppSubmission? Submission)
    {
        public static AppPath Missing(string code, string message, string action) =>
            new(false, false, code, message, action, null, null);
        public static AppPath Invalid(string code, string message, string action, ProcurementPlanItem? item, ProcurementAppSubmission? submission = null) =>
            new(true, false, code, message, action, item, submission);
        public static AppPath Allow(ProcurementPlanItem item, ProcurementAppSubmission submission) =>
            new(true, true, "PR_APP_ACKNOWLEDGED", "Acknowledged APP linkage is valid.", string.Empty, item, submission);
    }

    private sealed record ExceptionPath(
        bool Attempted,
        bool Allowed,
        string Code,
        string Message,
        string Action,
        ProcurementPolicyExceptionRule? Rule,
        WorkflowInstance? Workflow)
    {
        public static ExceptionPath Missing(string code, string message, string action) =>
            new(false, false, code, message, action, null, null);
        public static ExceptionPath Invalid(string code, string message, string action, ProcurementPolicyExceptionRule? rule, WorkflowInstance? workflow = null) =>
            new(true, false, code, message, action, rule, workflow);
        public static ExceptionPath Allow(ProcurementPolicyExceptionRule rule, WorkflowInstance workflow) =>
            new(true, true, "PR_APPROVED_EXCEPTION", "Approved exception linkage is valid.", string.Empty, rule, workflow);
    }

}
