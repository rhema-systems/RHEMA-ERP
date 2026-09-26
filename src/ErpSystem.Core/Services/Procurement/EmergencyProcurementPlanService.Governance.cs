using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.DocumentManagement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public partial class EmergencyProcurementPlanService
{
    internal const string ManagePermission = "procurement.sourcing.manage";
    internal const string ApprovePermission = "procurement.sourcing.approve";
    internal const string ReadPermission = "procurement.records.read";
    internal const string AuditPermission = "procurement.audit.read";
    internal const string SourceType = "EmergencyPurchaseException";
    internal const string WorkflowEntityType = "PROCUREMENT_EXCEPTION";

    private const string Prepared = "Prepared";
    private const string PendingAudit = "PendingAudit";
    private const string AuditVouched = "AuditVouched";
    private const string PendingApproval = "PendingApproval";
    private const string Approved = "Approved";
    private const string Rejected = "Rejected";
    private const string Triggered = "Triggered";
    private const string Filed = "Filed";

    private IGenericRepository<PurchaseRequisition> Requisitions => _unitOfWork.Repository<PurchaseRequisition>();
    private IGenericRepository<ProcurementPolicyExceptionRule> ExceptionRules => _unitOfWork.Repository<ProcurementPolicyExceptionRule>();
    private IGenericRepository<CentralDocumentVersion> DocumentVersions => _unitOfWork.Repository<CentralDocumentVersion>();
    private IGenericRepository<FileUploadRecord> Uploads => _unitOfWork.Repository<FileUploadRecord>();
    private IGenericRepository<ProcurementExceptionalSourcingControl> ExceptionalControls => _unitOfWork.Repository<ProcurementExceptionalSourcingControl>();

    public async Task<EmergencyPurchaseGovernanceOptionsDto> GetGovernanceOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureEmergencyCapabilityAsync(ManagePermission, "options", NewCorrelation(), cancellationToken);
        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;

        var requisitions = await Requisitions.GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted && item.Status == "Draft")
            .AsNoTracking().OrderByDescending(item => item.CreatedAt).Take(250)
            .Select(item => new EmergencyPurchaseRequisitionOptionDto
            {
                Id = item.Id,
                RequisitionNumber = item.RequisitionNumber,
                Status = item.Status,
                Category = item.ProcurementCategory.HasValue ? item.ProcurementCategory.Value.ToString() : null,
                TotalAmount = item.TotalAmount,
                Currency = item.Currency
            }).ToListAsync(cancellationToken);

        var rules = await ExceptionRules.GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted && item.IsEnabled &&
                item.EffectiveFrom <= now && (!item.EffectiveTo.HasValue || item.EffectiveTo >= now) &&
                item.PolicySet.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published &&
                item.PolicySet.EffectiveFrom <= now && (!item.PolicySet.EffectiveTo.HasValue || item.PolicySet.EffectiveTo >= now) &&
                item.ExceptionType.Contains("Emergency") && item.ExceptionType.Contains("Budget") &&
                item.Disposition == ProcurementExceptionDisposition.ApprovalRequired &&
                item.JustificationRequired && item.EvidenceRequired && item.PostAwardFilingRequired &&
                item.WorkflowDefinitionId.HasValue &&
                (item.ApproverRole == "TDC_MANAGING_DIRECTOR" || item.ApproverRole == "TDC_BOARD_APPROVER"))
            .AsNoTracking().OrderBy(item => item.Priority).ThenBy(item => item.RuleCode)
            .Select(item => new EmergencyPurchaseExceptionRuleOptionDto
            {
                Id = item.Id,
                RuleCode = item.RuleCode,
                Name = item.ExceptionName,
                ApproverRole = item.ApproverRole,
                WorkflowDefinitionId = item.WorkflowDefinitionId!.Value
            }).ToListAsync(cancellationToken);

        var versions = await DocumentVersions.GetQueryable(item => item.TenantId == tenantId)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(item => item.DocumentRecord.SourceModule == "Procurement" && item.FileUploadRecordId.HasValue)
            .Include(item => item.DocumentRecord).AsNoTracking()
            .OrderByDescending(item => item.PublishedAt).Take(250).ToListAsync(cancellationToken);
        var uploadIds = versions.Select(item => item.FileUploadRecordId!.Value).Distinct().ToArray();
        var cleanUploadIds = uploadIds.Length == 0
            ? new HashSet<Guid>()
            : (await Uploads.GetQueryable(item => item.TenantId == tenantId && uploadIds.Contains(item.Id) &&
                    !item.IsDeleted && item.VirusScanStatus == FileVirusScanStatus.Clean)
                .AsNoTracking().Select(item => item.Id).ToListAsync(cancellationToken)).ToHashSet();

        var exceptional = await ExceptionalControls.GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                item.Status == ProcurementExceptionalSourcingControlStatus.Filed)
            .Include(item => item.Tender).AsNoTracking().OrderByDescending(item => item.FiledAtUtc).Take(250)
            .Select(item => new EmergencyPurchaseExceptionalSourcingOptionDto
            {
                TenderId = item.TenderId,
                TenderNumber = item.Tender.TenderNumber,
                FilingReference = item.PostAwardFilingReference ?? item.ExceptionReportReference ?? string.Empty
            }).ToListAsync(cancellationToken);

        return new EmergencyPurchaseGovernanceOptionsDto
        {
            Requisitions = requisitions,
            ExceptionRules = rules,
            EvidenceDocuments = versions.Where(item => cleanUploadIds.Contains(item.FileUploadRecordId!.Value))
                .Select(item => new EmergencyPurchaseDocumentOptionDto
                {
                    VersionId = item.Id,
                    Reference = EvidenceReference(item),
                    Title = item.DocumentRecord.Title,
                    VersionNumber = item.VersionNumber
                }).ToList(),
            FiledExceptionalSourcing = exceptional
        };
    }

    public async Task<EmergencyProcurementPlanDetailDto> PrepareExceptionAsync(
        Guid id, PrepareEmergencyPurchaseRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(id, cancellationToken);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, correlationId, cancellationToken);
        EnsureDraft(plan);
        EnsureRowVersion(plan.RowVersion, request.RowVersion);
        var requisition = await Requisitions.GetQueryable(item => item.Id == request.PurchaseRequisitionId &&
                item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken) ?? throw NotFound("EMERGENCY_REQUISITION_NOT_FOUND",
                "The selected purchase requisition is not available in this tenant.");
        if (!string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw Conflict("EMERGENCY_REQUISITION_NOT_DRAFT", "Only a Draft purchase requisition can enter the emergency exception lifecycle.");
        if (requisition.ApprovedExceptionRuleId.HasValue || requisition.ExceptionWorkflowInstanceId.HasValue ||
            !string.IsNullOrWhiteSpace(requisition.ExceptionApprovalReference) ||
            !string.IsNullOrWhiteSpace(requisition.ExceptionEvidenceReference))
            throw Conflict("EMERGENCY_REQUISITION_EXCEPTION_ALREADY_SET",
                "The requisition already has exception lineage. Remove it through its owning governed recovery path before starting an emergency exception.");
        if (await _planRepository.GetQueryable().AnyAsync(item => item.TenantId == _currentUserProvider.TenantId &&
                !item.IsDeleted && item.Id != plan.Id && item.PurchaseRequisitionId == requisition.Id, cancellationToken))
            throw Conflict("EMERGENCY_REQUISITION_ALREADY_BOUND", "This requisition is already bound to another emergency-purchase control.");
        if (!string.Equals(plan.Currency, requisition.Currency, StringComparison.OrdinalIgnoreCase))
            throw Validation("EMERGENCY_CURRENCY_MISMATCH", "The plan and purchase requisition must use the same currency.");
        if (requisition.TotalAmount <= 0 || plan.MaxApprovalLimit <= 0 || requisition.TotalAmount > plan.MaxApprovalLimit)
            throw Validation("EMERGENCY_AUTHORITY_LIMIT_EXCEEDED", "The requisition amount must be positive and within the plan's configured approval limit.");
        if (plan.BudgetReserve > 0 && requisition.TotalAmount > plan.BudgetReserve - plan.UtilizedReserve)
            throw Validation("EMERGENCY_RESERVE_INSUFFICIENT", "The requisition exceeds the remaining emergency budget reserve.");

        var rule = await LoadEffectiveEmergencyRuleAsync(request.ExceptionRuleId, requisition, cancellationToken);
        var evidence = await LoadCleanEvidenceAsync(request.CentralDocumentVersionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Trim().Length < 20)
            throw Validation("EMERGENCY_JUSTIFICATION_REQUIRED", "A reason of at least 20 characters is required.");

        var now = DateTime.UtcNow;
        plan.PurchaseRequisitionId = requisition.Id;
        plan.ExceptionRuleId = rule.Id;
        plan.WorkflowDefinitionId = rule.WorkflowDefinitionId;
        plan.CentralDocumentVersionId = evidence.Version.Id;
        plan.FileUploadRecordId = evidence.Upload.Id;
        plan.EvidenceReference = EvidenceReference(evidence.Version);
        plan.ExceptionJustification = request.Justification.Trim();
        plan.PreparedById = _currentUserProvider.UserId;
        plan.PreparedAtUtc = now;
        plan.ApprovalAuthority = Authority(rule);
        plan.Status = Prepared;
        plan.ExceptionRule = rule;
        Touch(plan, now);

        await SaveWithEventAsync(plan, "EmergencyExceptionPrepared", ProcurementControlEventResult.Succeeded,
            correlationId, request.Justification, new
            {
                RequisitionId = requisition.Id,
                RuleId = rule.Id,
                DocumentVersionId = evidence.Version.Id
            },
            new { plan.Status, plan.IntegrityHash }, cancellationToken,
            UploadEvidence(evidence.Upload.Id, plan.EvidenceReference!, "EMERGENCY_JUSTIFICATION"));
        return await ReloadAsync(id, cancellationToken);
    }

    public async Task<EmergencyProcurementPlanDetailDto> SubmitForAuditAsync(
        Guid id, EmergencyPurchaseLifecycleRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(id, cancellationToken);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, correlationId, cancellationToken);
        EnsureStatus(plan, Prepared, "EMERGENCY_AUDIT_NOT_READY");
        EnsureRowVersion(plan.RowVersion, request.RowVersion);
        await RevalidatePreparedAsync(plan, cancellationToken);
        plan.Status = PendingAudit;
        plan.SubmittedForAuditAtUtc = DateTime.UtcNow;
        Touch(plan, plan.SubmittedForAuditAtUtc.Value);
        await SaveWithEventAsync(plan, "EmergencyAuditRequested", ProcurementControlEventResult.ReviewRequired,
            correlationId, request.Comments, null, new { plan.Status }, cancellationToken);
        return await ReloadAsync(id, cancellationToken);
    }

    public async Task<EmergencyProcurementPlanDetailDto> VouchAsync(
        Guid id, VouchEmergencyPurchaseRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(id, cancellationToken);
        if (!_currentUserProvider.HasRole("TDC_INTERNAL_AUDIT"))
            await DenyAuthorizationAsync(plan, "EmergencyAuditVouch", "Only an Internal Audit user can vouch an emergency-purchase exception.",
                correlationId, cancellationToken);
        await EnsureEmergencyCapabilityAsync(AuditPermission, plan.PlanCode, correlationId, cancellationToken);
        EnsureStatus(plan, PendingAudit, "EMERGENCY_AUDIT_NOT_PENDING");
        EnsureRowVersion(plan.RowVersion, request.RowVersion);
        await RevalidatePreparedAsync(plan, cancellationToken);
        var requisition = plan.PurchaseRequisition!;
        if (await _unitOfWork.IsProcurementSodEnabledAsync(_currentUserProvider.TenantId, cancellationToken) &&
            (_currentUserProvider.UserId == plan.PreparedById || _currentUserProvider.UserId == requisition.RequestedById))
            await DenyAuthorizationAsync(plan, "EmergencyAuditVouch",
                "The preparer or requisitioner cannot provide the independent Internal Audit vouch.", correlationId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.VouchNote) || request.VouchNote.Trim().Length < 10)
            throw Validation("EMERGENCY_AUDIT_NOTE_REQUIRED", "Internal Audit must record a vouch note of at least 10 characters.");

        var now = DateTime.UtcNow;
        plan.InternalAuditVouchedById = _currentUserProvider.UserId;
        plan.InternalAuditVouchedAtUtc = now;
        plan.InternalAuditVouchNote = request.VouchNote.Trim();
        plan.Status = AuditVouched;
        Touch(plan, now);
        await SaveWithEventAsync(plan, "EmergencyAuditVouched", ProcurementControlEventResult.Allowed,
            correlationId, request.VouchNote, new { plan.PreparedById, requisition.RequestedById },
            new { plan.Status, plan.InternalAuditVouchedAtUtc }, cancellationToken);
        return await ReloadAsync(id, cancellationToken);
    }

    public async Task<EmergencyProcurementPlanDetailDto> SubmitForApprovalAsync(
        Guid id, EmergencyPurchaseLifecycleRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(id, cancellationToken);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, correlationId, cancellationToken);
        EnsureStatus(plan, AuditVouched, "EMERGENCY_APPROVAL_NOT_READY");
        EnsureRowVersion(plan.RowVersion, request.RowVersion);
        await RevalidatePreparedAsync(plan, cancellationToken);
        if (!plan.WorkflowDefinitionId.HasValue)
            throw Conflict("EMERGENCY_WORKFLOW_MISSING", "The selected rule does not have an exact workflow definition.");

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var workflow = await _workflowService.StartApprovalWorkflowAsync(
                    WorkflowEntityType, plan.PurchaseRequisitionId!.Value, plan.WorkflowDefinitionId.Value);
                if (!workflow.Success || !workflow.WorkflowInstanceId.HasValue)
                    throw Conflict("EMERGENCY_WORKFLOW_START_FAILED", workflow.Message ?? "The emergency exception workflow could not be started.");

                var now = DateTime.UtcNow;
                plan.WorkflowInstanceId = workflow.WorkflowInstanceId;
                plan.SubmittedForApprovalAtUtc = now;
                plan.Status = PendingApproval;
                Touch(plan, now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await AppendEventAsync(plan, "EmergencyApprovalSubmitted", ProcurementControlEventResult.ReviewRequired,
                    correlationId, request.Comments, new { plan.WorkflowDefinitionId },
                    new { plan.WorkflowInstanceId, plan.Status }, cancellationToken,
                    ExternalEvidence($"workflow:{plan.WorkflowInstanceId:N}", "Emergency exception workflow", "DEC-006"));
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
        return await ReloadAsync(id, cancellationToken);
    }

    public async Task<EmergencyProcurementPlanDetailDto> DecideAsync(
        Guid id, DecideEmergencyPurchaseRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(id, cancellationToken);
        await EnsureEmergencyCapabilityAsync(ApprovePermission, plan.PlanCode, correlationId, cancellationToken);
        EnsureStatus(plan, PendingApproval, "EMERGENCY_APPROVAL_NOT_PENDING");
        EnsureRowVersion(plan.RowVersion, request.RowVersion);
        await RevalidatePreparedAsync(plan, cancellationToken);
        var rule = plan.ExceptionRule!;
        if (!_currentUserProvider.HasRole(rule.ApproverRole))
            await DenyAuthorizationAsync(plan, "EmergencyApprovalDecision",
                $"The configured {Authority(rule)} authority role is required for this decision.", correlationId, cancellationToken);
        if (!plan.WorkflowInstanceId.HasValue ||
            !await _workflowService.CanUserApproveAsync(WorkflowEntityType, plan.PurchaseRequisitionId!.Value, _currentUserProvider.UserId))
            await DenyAuthorizationAsync(plan, "EmergencyApprovalDecision",
                "The current user is not assigned to the active emergency exception workflow.", correlationId, cancellationToken);

        var requisition = plan.PurchaseRequisition!;
        var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = SourceType,
            SourceReference = plan.PlanCode,
            ProhibitedActorUserIds = new[] { plan.PreparedById, requisition.RequestedById, plan.InternalAuditVouchedById }
                .Where(value => value.HasValue && value.Value != Guid.Empty).Select(value => value!.Value).Distinct().ToList(),
            IndependentActorUserIds = [_currentUserProvider.UserId]
        }, correlationId, cancellationToken);
        if (!sod.Allowed)
            throw new EmergencyPurchaseAuthorizationException(sod.Message);

        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not "approve" and not "reject")
            throw Validation("EMERGENCY_APPROVAL_ACTION_INVALID", "Action must be Approve or Reject.");

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await _workflowService.ProcessApprovalStepAsync(
                    WorkflowEntityType, requisition.Id, _currentUserProvider.UserId, action, request.Comments);
                if (!result.Success)
                    throw Conflict("EMERGENCY_WORKFLOW_DECISION_FAILED", result.Message ?? "The workflow decision failed.");

                var now = DateTime.UtcNow;
                var eventResult = ProcurementControlEventResult.ReviewRequired;
                if (action == "approve" && result.Status == WorkflowInstanceStatus.Completed)
                {
                    if (string.IsNullOrWhiteSpace(request.ApprovalReference))
                        throw Validation("EMERGENCY_APPROVAL_REFERENCE_REQUIRED", "The configured authority approval reference is required.");
                    plan.Status = Approved;
                    plan.ApprovedById = _currentUserProvider.UserId;
                    plan.ApprovedDate = now;
                    plan.ApprovalReference = request.ApprovalReference.Trim();
                    requisition.ApprovedExceptionRuleId = rule.Id;
                    requisition.ApprovedExceptionRuleCode = rule.RuleCode;
                    requisition.ApprovedExceptionName = rule.ExceptionName;
                    requisition.ExceptionWorkflowInstanceId = plan.WorkflowInstanceId;
                    requisition.ExceptionApprovalReference = plan.ApprovalReference;
                    requisition.ExceptionEvidenceReference = plan.EvidenceReference;
                    requisition.ExceptionApprovedById = _currentUserProvider.UserId;
                    requisition.ExceptionApprovedByName = _currentUserProvider.FullName;
                    requisition.ExceptionApprovedAtUtc = now;
                    eventResult = ProcurementControlEventResult.Allowed;
                }
                else if (action == "reject")
                {
                    if (!EmergencyPurchaseGovernanceRules.IsRejectedWorkflowOutcome(result.Status))
                        throw Conflict("EMERGENCY_REJECTION_OUTCOME_INVALID", "The shared workflow did not return a rejected outcome.");
                    plan.Status = Rejected;
                    eventResult = ProcurementControlEventResult.Rejected;
                }

                Touch(plan, now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await AppendEventAsync(plan, "EmergencyApprovalDecision", eventResult, correlationId,
                    request.Comments, new { request.Action, result.Status },
                    new { plan.Status, plan.ApprovalReference }, cancellationToken,
                    ExternalEvidence($"workflow:{plan.WorkflowInstanceId:N}", "Emergency exception workflow", "DEC-006"));
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
        return await ReloadAsync(id, cancellationToken);
    }

    public async Task<EmergencyProcurementPlanDetailDto> TriggerGovernedAsync(
        Guid id, EmergencyPurchaseLifecycleRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(id, cancellationToken);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, correlationId, cancellationToken);
        EnsureStatus(plan, Approved, "EMERGENCY_TRIGGER_NOT_APPROVED");
        EnsureRowVersion(plan.RowVersion, request.RowVersion);
        await RevalidateApprovedAsync(plan, cancellationToken);
        plan.Status = Triggered;
        Touch(plan, DateTime.UtcNow);
        await SaveWithEventAsync(plan, "EmergencyPurchaseTriggered", ProcurementControlEventResult.Allowed,
            correlationId, request.Comments, new { plan.PurchaseRequisitionId, plan.WorkflowInstanceId },
            new { plan.Status }, cancellationToken);
        return await ReloadAsync(id, cancellationToken);
    }

    public async Task<EmergencyProcurementPlanDetailDto> FilePostAwardAsync(
        Guid id, FileEmergencyPurchasePostAwardRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(id, cancellationToken);
        await EnsureEmergencyCapabilityAsync(ManagePermission, plan.PlanCode, correlationId, cancellationToken);
        EnsureStatus(plan, Triggered, "EMERGENCY_POST_AWARD_NOT_READY");
        EnsureRowVersion(plan.RowVersion, request.RowVersion);
        await RevalidateApprovedAsync(plan, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Trim().Length < 20)
            throw Validation("EMERGENCY_POST_AWARD_JUSTIFICATION_REQUIRED", "Post-award justification must contain at least 20 characters.");
        var control = await ExceptionalControls.GetQueryable(item => item.TenantId == _currentUserProvider.TenantId &&
                item.TenderId == request.ExceptionalSourcingTenderId && !item.IsDeleted)
            .Include(item => item.Tender).Include(item => item.SourcingCase)
            .SingleOrDefaultAsync(cancellationToken) ?? throw NotFound("EMERGENCY_EXCEPTIONAL_SOURCING_NOT_FOUND",
                "The selected exceptional-sourcing case is not available in this tenant.");
        if (control.Status != ProcurementExceptionalSourcingControlStatus.Filed || !control.FiledAtUtc.HasValue)
            throw Conflict("EMERGENCY_EXCEPTIONAL_SOURCING_NOT_FILED", "Complete the existing exceptional-sourcing post-award filing first.");
        if (control.SourcingCase.PurchaseRequisitionId != plan.PurchaseRequisitionId ||
            control.Tender.SourcePurchaseRequisitionId != plan.PurchaseRequisitionId)
            throw Validation("EMERGENCY_EXCEPTIONAL_SOURCING_SUBJECT_MISMATCH",
                "The filed exceptional-sourcing case does not belong to this emergency purchase requisition.");
        var evidence = await LoadCleanEvidenceAsync(request.CentralDocumentVersionId, cancellationToken);

        var now = DateTime.UtcNow;
        plan.ExceptionalSourcingTenderId = control.TenderId;
        plan.PostAwardJustification = request.Justification.Trim();
        plan.PostAwardCentralDocumentVersionId = evidence.Version.Id;
        plan.PostAwardFileUploadRecordId = evidence.Upload.Id;
        plan.PostAwardEvidenceReference = EvidenceReference(evidence.Version);
        plan.FiledById = _currentUserProvider.UserId;
        plan.FiledAtUtc = now;
        plan.Status = Filed;
        Touch(plan, now);
        await SaveWithEventAsync(plan, "EmergencyPostAwardFiled", ProcurementControlEventResult.Succeeded,
            correlationId, request.Justification, new { control.TenderId, control.PostAwardFilingReference },
            new { plan.Status, plan.FiledAtUtc }, cancellationToken,
            UploadEvidence(evidence.Upload.Id, plan.PostAwardEvidenceReference!, "EMERGENCY_POST_AWARD"),
            ExternalEvidence(control.PostAwardFilingReference ?? control.ExceptionReportReference ?? control.Tender.TenderNumber,
                "Exceptional sourcing filing", "DEC-006"));
        return await ReloadAsync(id, cancellationToken);
    }

    internal async Task<EmergencyProcurementPlan> RequirePlanAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetQueryable().SingleOrDefaultAsync(item =>
            item.Id == id && item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted, cancellationToken);
        return plan ?? throw NotFound("EMERGENCY_PLAN_NOT_FOUND", "The emergency procurement plan was not found in this tenant.");
    }

    private async Task<EmergencyProcurementPlanDetailDto> ReloadAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await GetByIdAsync(id) ?? throw NotFound("EMERGENCY_PLAN_NOT_FOUND", "The emergency procurement plan was not found in this tenant.");
    }

    private async Task<ProcurementPolicyExceptionRule> LoadEffectiveEmergencyRuleAsync(
        Guid id, PurchaseRequisition requisition, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rule = await ExceptionRules.GetQueryable(item => item.Id == id && item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted)
            .Include(item => item.PolicySet).SingleOrDefaultAsync(cancellationToken) ??
            throw NotFound("EMERGENCY_EXCEPTION_RULE_NOT_FOUND", "The selected exception rule was not found in this tenant.");
        if (!rule.IsEnabled || rule.EffectiveFrom > now || rule.EffectiveTo.HasValue && rule.EffectiveTo < now ||
            rule.PolicySet.LifecycleStatus != ProcurementPolicyLifecycleStatus.Published ||
            rule.PolicySet.EffectiveFrom > now || rule.PolicySet.EffectiveTo.HasValue && rule.PolicySet.EffectiveTo < now)
            throw Validation("EMERGENCY_EXCEPTION_RULE_NOT_EFFECTIVE", "The selected exception rule is not currently effective in a Published policy set.");
        if (!rule.ExceptionType.Contains("Emergency", StringComparison.OrdinalIgnoreCase) ||
            !rule.ExceptionType.Contains("Budget", StringComparison.OrdinalIgnoreCase) ||
            rule.Disposition != ProcurementExceptionDisposition.ApprovalRequired || !rule.JustificationRequired ||
            !rule.EvidenceRequired || !rule.PostAwardFilingRequired || !rule.WorkflowDefinitionId.HasValue)
            throw Validation("EMERGENCY_EXCEPTION_RULE_INCOMPLETE",
                "The rule must govern an emergency budget exception with reason, evidence, approval workflow, and post-award filing.");
        if (!EmergencyPurchaseGovernanceRules.IsApprovalAuthorityRole(rule.ApproverRole))
            throw Validation("EMERGENCY_AUTHORITY_INVALID", "The rule approver must be the Managing Director or Board approver role.");
        if (rule.Category.HasValue && rule.Category != requisition.ProcurementCategory)
            throw Validation("EMERGENCY_EXCEPTION_CATEGORY_MISMATCH", "The selected exception rule does not cover the requisition category.");
        return rule;
    }

    private async Task<(CentralDocumentVersion Version, FileUploadRecord Upload)> LoadCleanEvidenceAsync(
        Guid versionId, CancellationToken cancellationToken)
    {
        var version = await DocumentVersions.GetQueryable(item => item.Id == versionId && item.TenantId == _currentUserProvider.TenantId)
            .Where(CentralDocumentEvidenceRules.CurrentPublished()).Include(item => item.DocumentRecord)
            .SingleOrDefaultAsync(cancellationToken) ?? throw Validation("EMERGENCY_EVIDENCE_NOT_PUBLISHED",
                "Select the current Published version of an active central DMS document.");
        if (!string.Equals(version.DocumentRecord.SourceModule, "Procurement", StringComparison.OrdinalIgnoreCase) ||
            !version.FileUploadRecordId.HasValue)
            throw Validation("EMERGENCY_EVIDENCE_INVALID", "The selected evidence must be a stored Procurement document.");
        var upload = await Uploads.GetQueryable(item => item.Id == version.FileUploadRecordId.Value &&
                item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (upload is null || upload.VirusScanStatus != FileVirusScanStatus.Clean)
            throw Validation("EMERGENCY_EVIDENCE_NOT_CLEAN", "The selected DMS file must pass central malware scanning.");
        return (version, upload);
    }

    private async Task RevalidatePreparedAsync(EmergencyProcurementPlan plan, CancellationToken cancellationToken)
    {
        var requisition = await Requisitions.GetQueryable(item => item.Id == plan.PurchaseRequisitionId &&
                item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken) ?? throw Conflict("EMERGENCY_REQUISITION_UNAVAILABLE", "The linked requisition is no longer available.");
        plan.PurchaseRequisition = requisition;
        plan.ExceptionRule = await LoadEffectiveEmergencyRuleAsync(plan.ExceptionRuleId ?? Guid.Empty, requisition, cancellationToken);
        if (!plan.CentralDocumentVersionId.HasValue)
            throw Conflict("EMERGENCY_EVIDENCE_MISSING", "The linked central DMS evidence is missing.");
        var evidence = await LoadCleanEvidenceAsync(plan.CentralDocumentVersionId.Value, cancellationToken);
        if (evidence.Upload.Id != plan.FileUploadRecordId || EvidenceReference(evidence.Version) != plan.EvidenceReference)
            throw Conflict("EMERGENCY_EVIDENCE_LINEAGE_CHANGED", "The prepared DMS evidence lineage no longer matches the immutable snapshot.");
        VerifyIntegrity(plan);
    }

    private async Task RevalidateApprovedAsync(EmergencyProcurementPlan plan, CancellationToken cancellationToken)
    {
        await RevalidatePreparedAsync(plan, cancellationToken);
        var requisition = plan.PurchaseRequisition!;
        if (requisition.ApprovedExceptionRuleId != plan.ExceptionRuleId ||
            requisition.ExceptionWorkflowInstanceId != plan.WorkflowInstanceId ||
            !string.Equals(requisition.ExceptionEvidenceReference, plan.EvidenceReference, StringComparison.Ordinal) ||
            !requisition.ExceptionApprovedAtUtc.HasValue)
            throw Conflict("EMERGENCY_APPROVED_LINEAGE_INVALID", "The approved requisition exception lineage no longer matches this emergency control.");
    }

    internal async Task EnsureEmergencyCapabilityAsync(string permission, string reference, string correlationId,
        CancellationToken cancellationToken)
    {
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            SourceType = SourceType,
            SourceReference = reference
        }, NormalizeCorrelation(correlationId), cancellationToken);
        if (!decision.Allowed) throw new EmergencyPurchaseAuthorizationException(decision.Message);
    }

    internal static void EnsureDraft(EmergencyProcurementPlan plan)
    {
        if (!string.Equals(plan.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw Conflict("EMERGENCY_PLAN_LOCKED", "Only a Draft emergency plan can be changed.");
    }

    private static void EnsureStatus(EmergencyProcurementPlan plan, string required, string code)
    {
        if (!string.Equals(plan.Status, required, StringComparison.Ordinal))
            throw Conflict(code, $"This action requires status {required}; current status is {plan.Status}.");
    }

    private static void EnsureRowVersion(byte[] actual, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException) { throw Validation("EMERGENCY_ROW_VERSION_INVALID", "RowVersion must be valid base64."); }
        if (actual.Length == 0 || !actual.SequenceEqual(expected))
            throw Conflict("EMERGENCY_CONCURRENCY_CONFLICT", "The emergency plan changed. Refresh and try again.");
    }

    private async Task SaveWithEventAsync(
        EmergencyProcurementPlan plan, string action, ProcurementControlEventResult result,
        string correlationId, string? reason, object? before, object? after,
        CancellationToken cancellationToken, params ProcurementControlEventEvidenceReference[] evidence)
    {
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await AppendEventAsync(plan, action, result, correlationId, reason, before, after,
                    cancellationToken, evidence);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    private Task AppendEventAsync(
        EmergencyProcurementPlan plan, string action, ProcurementControlEventResult result,
        string correlationId, string? reason, object? before, object? after,
        CancellationToken cancellationToken, params ProcurementControlEventEvidenceReference[] evidence)
    {
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        return _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("emergency-purchase", plan.TenantId, plan.Id,
                action.ToLowerInvariant(), Guid.NewGuid()),
            EventType = SourceType,
            Action = action,
            Result = result,
            RuleCode = plan.ExceptionRule?.RuleCode ?? "PR-REQ-FU-003",
            RuleId = plan.ExceptionRuleId,
            DecisionKeys = ["DEC-006"],
            SourceType = SourceType,
            SourceId = plan.Id,
            SourceReference = plan.PlanCode,
            Reason = reason,
            Before = before,
            After = after,
            CorrelationId = normalizedCorrelation,
            CausationId = normalizedCorrelation,
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = evidence.ToList()
        }, cancellationToken);
    }

    private async Task DenyAuthorizationAsync(
        EmergencyProcurementPlan plan, string action, string reason, string correlationId,
        CancellationToken cancellationToken)
    {
        await AppendEventAsync(plan, action, ProcurementControlEventResult.Denied,
            correlationId, reason, null, new { Allowed = false }, cancellationToken);
        throw new EmergencyPurchaseAuthorizationException(reason);
    }

    private async Task<EmergencyProcurementPlanDetailDto> RejectDirectLifecycleAsync(
        EmergencyProcurementPlan plan, string action, string code, string reason)
    {
        var correlationId = NewCorrelation();
        await AppendEventAsync(plan, action, ProcurementControlEventResult.Denied,
            correlationId, reason, null, new { Allowed = false, Code = code }, CancellationToken.None);
        throw Validation(code, reason);
    }

    private static void Touch(EmergencyProcurementPlan plan, DateTime now)
    {
        plan.UpdatedAt = now;
        plan.LifecycleSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.emergency-purchase.v1",
            plan.Id,
            plan.TenantId,
            plan.PlanCode,
            plan.Status,
            plan.PurchaseRequisitionId,
            plan.ExceptionRuleId,
            plan.WorkflowDefinitionId,
            plan.WorkflowInstanceId,
            plan.CentralDocumentVersionId,
            plan.FileUploadRecordId,
            plan.EvidenceReference,
            plan.ExceptionJustification,
            plan.PreparedById,
            plan.PreparedAtUtc,
            plan.SubmittedForAuditAtUtc,
            plan.InternalAuditVouchedById,
            plan.InternalAuditVouchedAtUtc,
            plan.InternalAuditVouchNote,
            plan.SubmittedForApprovalAtUtc,
            plan.ApprovalAuthority,
            plan.ApprovalReference,
            plan.ApprovedById,
            plan.ApprovedDate,
            plan.ExceptionalSourcingTenderId,
            plan.PostAwardJustification,
            plan.PostAwardCentralDocumentVersionId,
            plan.PostAwardFileUploadRecordId,
            plan.PostAwardEvidenceReference,
            plan.FiledById,
            plan.FiledAtUtc
        });
        plan.IntegrityHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plan.LifecycleSnapshotJson)));
    }

    private static void VerifyIntegrity(EmergencyProcurementPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.LifecycleSnapshotJson) || string.IsNullOrWhiteSpace(plan.IntegrityHash) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(plan.IntegrityHash),
                Encoding.ASCII.GetBytes(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plan.LifecycleSnapshotJson))))))
            throw Conflict("EMERGENCY_INTEGRITY_INVALID", "The emergency-purchase lifecycle integrity check failed.");
    }

    private static string EvidenceReference(CentralDocumentVersion version) =>
        $"{version.DocumentRecord.DocumentReference}/{version.VersionNumber}";

    private static string Authority(ProcurementPolicyExceptionRule rule) =>
        rule.ApproverRole == "TDC_BOARD_APPROVER" ? "Board" : "ManagingDirector";

    private static ProcurementControlEventEvidenceReference UploadEvidence(Guid id, string reference, string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
        ReferenceId = id,
        Reference = reference,
        Label = "Central DMS evidence",
        RequirementKey = requirement
    };

    private static ProcurementControlEventEvidenceReference ExternalEvidence(string reference, string label, string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference,
        Label = label,
        RequirementKey = requirement
    };

    internal static string NormalizeCorrelation(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();

    internal static string NewCorrelation() => Guid.NewGuid().ToString("N");
    internal static EmergencyPurchaseNotFoundException NotFound(string code, string message) => new(code, message);
    internal static EmergencyPurchaseConflictException Conflict(string code, string message) => new(code, message);
    internal static EmergencyPurchaseValidationException Validation(string code, string message) => new(code, message);
}
