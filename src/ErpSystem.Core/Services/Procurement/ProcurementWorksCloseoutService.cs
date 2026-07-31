using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementWorksCloseoutService : IProcurementWorksCloseoutService
{
    private const string EventType = "ProcurementWorksCloseout";
    private const string SourceType = "Contract";
    private const string WorkflowEntityType = "PROCUREMENT_CONTRACT";
    private const string ReadPermission = "procurement.records.read";
    private const string ManagePermission = "procurement.contract.manage";
    private const string ApprovePermission = "procurement.contract.approve";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcurementWorksCloseoutStore _store;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementConfigurationService _configuration;
    private readonly IProcurementComplianceDecisionService _compliance;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly ILogger<ProcurementWorksCloseoutService> _logger;

    public ProcurementWorksCloseoutService(
        IUnitOfWork unitOfWork,
        IProcurementWorksCloseoutStore store,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IProcurementConfigurationService configuration,
        IProcurementComplianceDecisionService compliance,
        IWorkflowIntegrationService workflow,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        ILogger<ProcurementWorksCloseoutService> logger)
    {
        _unitOfWork = unitOfWork;
        _store = store;
        _currentUser = currentUser;
        _access = access;
        _configuration = configuration;
        _compliance = compliance;
        _workflow = workflow;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _logger = logger;
    }

    private IGenericRepository<Contract> Contracts => _unitOfWork.Repository<Contract>();
    private IGenericRepository<Project> Projects => _unitOfWork.Repository<Project>();
    private IGenericRepository<ProjectHandoverItem> HandoverItems =>
        _unitOfWork.Repository<ProjectHandoverItem>();
    private IGenericRepository<ProjectDefectLiabilityCase> Defects =>
        _unitOfWork.Repository<ProjectDefectLiabilityCase>();
    private IGenericRepository<ProjectFinalAccount> FinalAccounts =>
        _unitOfWork.Repository<ProjectFinalAccount>();
    private IGenericRepository<ProjectPaymentCertificate> PaymentCertificates =>
        _unitOfWork.Repository<ProjectPaymentCertificate>();
    private IGenericRepository<ProjectClosure> ProjectClosures =>
        _unitOfWork.Repository<ProjectClosure>();
    private IGenericRepository<PerformanceBondRequest> PerformanceBonds =>
        _unitOfWork.Repository<PerformanceBondRequest>();
    private IGenericRepository<ProcurementWorksCloseoutAction> Actions =>
        _unitOfWork.Repository<ProcurementWorksCloseoutAction>();
    private IGenericRepository<ProcurementWorksCloseoutEvidence> Evidence =>
        _unitOfWork.Repository<ProcurementWorksCloseoutEvidence>();

    public async Task<ProcurementWorksCloseoutOverviewDto> GetOverviewAsync(
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        var correlation = Guid.NewGuid().ToString("N");
        await EnsureCapabilityAsync(ReadPermission, contractId.ToString("N"),
            correlation, cancellationToken);
        var contract = await LoadContractAsync(contractId, cancellationToken);
        var project = await LoadProjectAsync(contractId, false, cancellationToken);
        var history = await ActionQuery()
            .Where(item => item.ContractId == contractId)
            .AsNoTracking()
            .OrderByDescending(item => item.Sequence)
            .ToListAsync(cancellationToken);
        var source = project is null
            ? null
            : await LoadSourceStateAsync(contract, project, history, cancellationToken);

        var checks = new List<ProcurementWorksCloseoutCheckDto>
        {
            ProcurementWorksCloseoutRules.IsWorks(contract.ContractType)
                ? Passed("contract-type", "Works contract", "WORKS_CONTRACT_CONFIRMED",
                    "The contract is classified as Works.", contract.Id, contract.ContractNumber)
                : Failed("contract-type", "Works contract", "WORKS_CONTRACT_REQUIRED",
                    "Works closeout controls apply only to a Works contract."),
            project is not null
                ? Passed("project", "Linked project", "WORKS_PROJECT_LINKED",
                    $"Project {project.ProjectCode} is linked to this contract.",
                    project.Id, project.ProjectCode)
                : Failed("project", "Linked project", "WORKS_PROJECT_REQUIRED",
                    "Exactly one current-tenant project must be linked to the Works contract.")
        };
        ProcurementConfigurationProfileDto? profile = null;
        try
        {
            profile = await LoadProfileAsync(cancellationToken);
            checks.Add(Passed("configuration", "Configuration profile",
                "WORKS_CONFIGURATION_READY",
                $"{profile.ProfileCode} v{profile.Version}", profile.Id,
                $"{profile.ProfileCode}:v{profile.Version}"));
        }
        catch (ProcurementWorksCloseoutException exception)
        {
            checks.Add(Failed("configuration", "Configuration profile",
                exception.Code, exception.Message));
        }

        return new ProcurementWorksCloseoutOverviewDto
        {
            ContractId = contract.Id,
            ContractNumber = contract.ContractNumber,
            ContractStatus = contract.Status,
            ContractRowVersion = Convert.ToBase64String(contract.RowVersion),
            IsWorksContract = ProcurementWorksCloseoutRules.IsWorks(contract.ContractType),
            HasActiveDispute = HasActiveDispute(history),
            IsClosed = history.Any(IsApprovedCloseout) ||
                       string.Equals(contract.Status, "Completed",
                           StringComparison.OrdinalIgnoreCase),
            DecisionKeys = DecisionKeys,
            RequiredEvidence = ProcurementWorksCloseoutRules.RequiredEvidence,
            Project = source is null ? null : MapProjectSummary(source),
            HandoverItems = source?.HandoverItems.Select(item =>
                new ProcurementWorksHandoverSourceDto
                {
                    Id = item.Id,
                    HandoverType = item.HandoverType,
                    Title = item.Title,
                    Status = item.Status,
                    ReferenceNumber = item.ReferenceNumber,
                    CompletedDate = item.CompletedDate
                }).ToList() ?? [],
            Defects = source?.Defects.Select(item =>
                new ProcurementWorksDefectSourceDto
                {
                    Id = item.Id,
                    Title = item.Title,
                    Status = item.Status,
                    IsWarrantyRelated = item.IsWarrantyRelated,
                    WarrantyExpiryDate = item.WarrantyExpiryDate,
                    RectificationCost = item.RectificationCost,
                    Currency = item.Currency
                }).ToList() ?? [],
            PaymentCertificates = source?.Certificates.Select(item =>
                new ProcurementWorksCertificateSourceDto
                {
                    Id = item.Id,
                    Title = item.Title,
                    CertificateNumber = item.CertificateNumber,
                    Status = item.Status,
                    RetentionHeldAmount = item.RetentionHeldAmount,
                    RetentionReleasedAmount = item.RetentionReleasedAmount,
                    Currency = item.Currency
                }).ToList() ?? [],
            PerformanceSecurity = source?.PerformanceBond is null
                ? null
                : new ProcurementWorksSecuritySourceDto
                {
                    Id = source.PerformanceBond.Id,
                    Status = source.PerformanceBond.Status
                },
            Checks = checks,
            History = history.Select(Map).ToList()
        };
    }

    public async Task<ProcurementWorksCloseoutActionDto> SubmitAsync(
        Guid contractId,
        SubmitProcurementWorksCloseoutActionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.Reason, "WORKS_CLOSEOUT_REASON_REQUIRED",
            "A documented reason is required.");
        Require(request.IdempotencyKey, "WORKS_CLOSEOUT_IDEMPOTENCY_REQUIRED",
            "An idempotency key is required.");
        if (!Enum.IsDefined(request.ActionType))
            throw Validation("WORKS_CLOSEOUT_ACTION_TYPE_INVALID",
                "The Works closeout action type is invalid.");
        var contract = await LoadContractAsync(contractId, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, contract.ContractNumber,
            correlation, cancellationToken);
        EnsureRowVersion(contract.RowVersion, request.ContractRowVersion,
            "WORKS_CLOSEOUT_CONTRACT_STALE");
        EnsureWorksAndStatus(contract, request.ActionType);

        var existing = await ActionQuery().AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.IdempotencyKey == request.IdempotencyKey.Trim(),
                cancellationToken);
        if (existing is not null)
        {
            if (existing.ContractId != contractId)
                throw Conflict("WORKS_CLOSEOUT_IDEMPOTENCY_CONFLICT",
                    "The idempotency key belongs to another contract.");
            return Map(existing);
        }

        ProcurementWorksCloseoutAction? created = null;
        _unitOfWork.ClearTrackedChanges();
        await ExecuteAsync(async () =>
        {
            var concurrent = await ActionQuery()
                .SingleOrDefaultAsync(item =>
                    item.IdempotencyKey == request.IdempotencyKey.Trim(),
                    cancellationToken);
            if (concurrent is not null)
            {
                if (concurrent.ContractId != contractId)
                    throw Conflict("WORKS_CLOSEOUT_IDEMPOTENCY_CONFLICT",
                        "The idempotency key belongs to another contract.");
                created = concurrent;
                return;
            }

            contract = await LoadContractAsync(contractId, cancellationToken);
            EnsureRowVersion(contract.RowVersion, request.ContractRowVersion,
                "WORKS_CLOSEOUT_CONTRACT_STALE");
            EnsureWorksAndStatus(contract, request.ActionType);
            var project = await LoadProjectAsync(contract.Id, true, cancellationToken);
            var history = await ActionQuery()
                .Where(item => item.ContractId == contract.Id)
                .OrderBy(item => item.Sequence)
                .ToListAsync(cancellationToken);
            if (history.Any(item =>
                    item.ActionType == request.ActionType &&
                    item.Status is ProcurementWorksCloseoutActionStatus.PendingApproval
                        or ProcurementWorksCloseoutActionStatus.RevalidationFailed))
                throw Conflict("WORKS_CLOSEOUT_OPEN_ACTION",
                    $"An open {request.ActionType} action already exists.");

            var evidence = await ValidateEvidenceAsync(
                contract, request.ActionType, request.Evidence, cancellationToken);
            var evaluation = await EvaluateAsync(
                contract, project!, history, request, evidence,
                correlation, cancellationToken);
            EnsureReady(evaluation.Checks);

            var sequence = (await Actions.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.ContractId == contract.Id && !item.IsDeleted)
                .Select(item => (int?)item.Sequence)
                .MaxAsync(cancellationToken) ?? 0) + 1;
            var now = DateTime.UtcNow;
            created = new ProcurementWorksCloseoutAction
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                ContractId = contract.Id,
                ProjectId = project!.Id,
                Sequence = sequence,
                ActionType = request.ActionType,
                Status = ProcurementWorksCloseoutActionStatus.PendingApproval,
                ConfigurationProfileId = evaluation.Profile.Id,
                ConfigurationProfileVersion = evaluation.Profile.Version,
                PolicySetId = evaluation.Authority.Policy!.PolicySetId,
                PolicyVersion = evaluation.Authority.Policy.Version,
                AuthorityRuleId = evaluation.Authority.Steps.First().RuleId,
                AuthorityName = evaluation.Authority.Steps.First().AuthorityName,
                WorkflowDefinitionId = evaluation.Authority.Workflow!.WorkflowDefinitionId,
                ProjectHandoverItemId = request.ProjectHandoverItemId,
                ProjectDefectLiabilityCaseId = request.ProjectDefectLiabilityCaseId,
                ProjectFinalAccountId = request.ProjectFinalAccountId,
                ProjectPaymentCertificateId = request.ProjectPaymentCertificateId,
                PerformanceBondRequestId = request.PerformanceBondRequestId,
                EffectiveAtUtc = request.EffectiveAtUtc.HasValue
                    ? EnsureUtc(request.EffectiveAtUtc.Value)
                    : now,
                DefectsLiabilityEndsAtUtc =
                    request.ActionType == ProcurementWorksCloseoutActionType.InitialTakeover
                        ? ProcurementWorksCloseoutRules.DefectsLiabilityEnd(
                            request.EffectiveAtUtc.HasValue
                                ? EnsureUtc(request.EffectiveAtUtc.Value)
                                : now,
                            contract.WarrantyPeriodDays)
                        : null,
                Amount = request.Amount,
                Currency = request.Currency?.Trim().ToUpperInvariant(),
                RequiresIndependentFinanceApproval =
                    ProcurementWorksCloseoutRules.IsMonetary(request.ActionType),
                AmountAutoPosted = false,
                SubmittedById = _currentUser.UserId,
                SubmittedByName = ActorName(),
                SubmittedAtUtc = now,
                Reason = request.Reason.Trim(),
                IdempotencyKey = request.IdempotencyKey.Trim(),
                CorrelationId = correlation,
                SourceSnapshotJson = evaluation.SourceSnapshot,
                SourceSnapshotHash = Hash(evaluation.SourceSnapshot),
                ReadinessSnapshotJson = Serialize(evaluation.Checks),
                CreatedAt = now,
                CreatedBy = ActorName(),
                CreatedById = _currentUser.UserId
            };
            created.IntegrityHash = ActionHash(created);
            await Actions.AddAsync(created);
            foreach (var item in evidence)
            {
                item.ActionId = created.Id;
                item.TenantId = created.TenantId;
                item.CreatedAt = now;
                item.CreatedBy = ActorName();
                item.CreatedById = _currentUser.UserId;
                await Evidence.AddAsync(item);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var workflow = await _workflow.SubmitAsync(
                WorkflowEntityType, created.Id, created.WorkflowDefinitionId);
            if (!workflow.ExecutionResult.Success)
                throw Conflict("WORKS_CLOSEOUT_WORKFLOW_START_FAILED",
                    workflow.ExecutionResult.Message ??
                    "The configured Works closeout workflow could not be started.");
            created.WorkflowInstanceId = workflow.ExecutionResult.WorkflowInstanceId;
            created.IntegrityHash = ActionHash(created);
            await Actions.UpdateAsync(created);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(created, "WorksCloseoutSubmitted",
                ProcurementControlEventResult.ReviewRequired, null,
                new { created.ActionType, created.Status, created.WorkflowInstanceId },
                created.Reason, correlation, evaluation.Checks, cancellationToken);
        }, cancellationToken);

        await PublishAsync("procurement.works-closeout.submitted", created!,
            cancellationToken);
        return Map(await LoadActionAsync(created!.Id, cancellationToken));
    }

    public async Task<ProcurementWorksCloseoutActionDto> DecideAsync(
        Guid actionId,
        DecideProcurementWorksCloseoutActionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.Comment, "WORKS_CLOSEOUT_DECISION_COMMENT_REQUIRED",
            "A decision comment is required.");
        var action = await LoadActionAsync(actionId, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, action.Contract.ContractNumber,
            correlation, cancellationToken);
        EnsureRowVersion(action.RowVersion, request.RowVersion,
            "WORKS_CLOSEOUT_ACTION_STALE");
        if (!ProcurementWorksCloseoutRules.CanDecide(action.Status))
            throw Conflict("WORKS_CLOSEOUT_DECISION_NOT_ALLOWED",
                "Only a pending or revalidation-failed Works closeout action may be decided.");
        if (request.Approved &&
            !ProcurementWorksCloseoutRules.IsIndependent(
                _currentUser.UserId, action.SubmittedById,
                action.Contract.CreatedById))
            throw Authorization(
                "The contract creator or closeout submitter cannot positively decide this action.");
        if (!action.WorkflowInstanceId.HasValue)
            throw Conflict("WORKS_CLOSEOUT_WORKFLOW_MISSING",
                "The Works closeout action is not bound to a shared workflow.");

        var workflowStatus = await _unitOfWork.Repository<WorkflowInstance>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == action.WorkflowInstanceId.Value &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item => (WorkflowInstanceStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Conflict("WORKS_CLOSEOUT_WORKFLOW_MISSING",
                "The bound shared workflow instance is not available in this tenant.");

        WorkflowOutcome outcome;
        if (action.Status == ProcurementWorksCloseoutActionStatus.RevalidationFailed)
        {
            if (!request.Approved)
                throw Conflict("WORKS_CLOSEOUT_REVALIDATION_RETRY_ONLY",
                    "A revalidation-failed action has an approved workflow and may only retry application.");
            if (workflowStatus != WorkflowInstanceStatus.Completed)
                throw Conflict("WORKS_CLOSEOUT_WORKFLOW_NOT_APPROVED",
                    "Application can be retried only while the bound workflow remains completed.");
            outcome = WorkflowOutcome.Approved;
        }
        else if (workflowStatus == WorkflowInstanceStatus.Completed)
        {
            outcome = WorkflowOutcome.Approved;
        }
        else if (workflowStatus is WorkflowInstanceStatus.Cancelled
                 or WorkflowInstanceStatus.Failed)
        {
            outcome = WorkflowOutcome.Rejected;
        }
        else
        {
            var workflow = await _workflow.ProcessApprovalAsync(
                WorkflowEntityType, action.Id, _currentUser.UserId,
                request.Approved ? "Approve" : "Reject", request.Comment.Trim());
            if (!workflow.ExecutionResult.Success)
                throw Conflict("WORKS_CLOSEOUT_WORKFLOW_DECISION_FAILED",
                    workflow.ExecutionResult.Message ??
                    "The shared Works closeout workflow decision failed.");
            outcome = workflow.Outcome;
        }

        if (request.Approved && outcome == WorkflowOutcome.Rejected)
            throw Conflict("WORKS_CLOSEOUT_WORKFLOW_REJECTED",
                "The shared workflow rejected the Works closeout action.");
        if (!request.Approved && outcome == WorkflowOutcome.Approved)
            throw Conflict("WORKS_CLOSEOUT_WORKFLOW_APPROVED",
                "An approved shared workflow cannot be recorded as rejected.");
        if (outcome == WorkflowOutcome.Recalled)
            throw Conflict("WORKS_CLOSEOUT_WORKFLOW_RECALLED",
                "The shared workflow was recalled.");

        var now = DateTime.UtcNow;
        if (outcome == WorkflowOutcome.Pending)
        {
            action.DecisionComment = request.Comment.Trim();
            StampUpdate(action, now);
            action.IntegrityHash = ActionHash(action);
            await Actions.UpdateAsync(action);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(action, "WorksCloseoutApprovalProgressed",
                ProcurementControlEventResult.ReviewRequired, null,
                new { action.ActionType, action.Status, Outcome = outcome },
                request.Comment, correlation, null, cancellationToken);
            return Map(await LoadActionAsync(action.Id, cancellationToken));
        }

        if (outcome == WorkflowOutcome.Rejected)
        {
            action.Status = ProcurementWorksCloseoutActionStatus.Rejected;
            action.DecidedById = _currentUser.UserId;
            action.DecidedByName = ActorName();
            action.DecidedAtUtc = now;
            action.DecisionComment = request.Comment.Trim();
            StampUpdate(action, now);
            action.IntegrityHash = ActionHash(action);
            await Actions.UpdateAsync(action);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(action, "WorksCloseoutRejected",
                ProcurementControlEventResult.Rejected, null,
                new { action.ActionType, action.Status },
                request.Comment, correlation, null, cancellationToken);
            await PublishAsync("procurement.works-closeout.rejected", action,
                cancellationToken);
            return Map(await LoadActionAsync(action.Id, cancellationToken));
        }

        var revalidationFailed = false;
        _unitOfWork.ClearTrackedChanges();
        await ExecuteAsync(async () =>
        {
            action = await LoadActionAsync(actionId, cancellationToken);
            EnsureRowVersion(action.RowVersion, request.RowVersion,
                "WORKS_CLOSEOUT_ACTION_STALE");
            if (!ProcurementWorksCloseoutRules.CanDecide(action.Status))
                throw Conflict("WORKS_CLOSEOUT_DECISION_NOT_ALLOWED",
                    "The Works closeout action is no longer available for decision.");

            var contract = await LoadContractAsync(action.ContractId, cancellationToken);
            EnsureWorksAndStatus(contract, action.ActionType);
            var project = await LoadProjectAsync(contract.Id, true, cancellationToken);
            var history = await ActionQuery()
                .Where(item => item.ContractId == contract.Id)
                .OrderBy(item => item.Sequence)
                .ToListAsync(cancellationToken);
            var replay = ToSubmitRequest(action, contract);
            var evaluation = await EvaluateAsync(
                contract, project!, history.Where(item => item.Id != action.Id).ToList(),
                replay, action.Evidence.ToList(), correlation, cancellationToken);
            if (!IsReady(evaluation.Checks))
            {
                action.Status = ProcurementWorksCloseoutActionStatus.RevalidationFailed;
                action.DecisionComment = request.Comment.Trim();
                action.ReadinessSnapshotJson = Serialize(evaluation.Checks);
                StampUpdate(action, now);
                action.IntegrityHash = ActionHash(action);
                await Actions.UpdateAsync(action);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync(action, "WorksCloseoutRevalidationFailed",
                    ProcurementControlEventResult.Denied, null,
                    new { action.ActionType, action.Status, evaluation.Checks },
                    request.Comment, correlation, evaluation.Checks,
                    cancellationToken);
                revalidationFailed = true;
                return;
            }

            await _store.SetMutationContextAsync(action.Id, cancellationToken);
            try
            {
            action.Status = ProcurementWorksCloseoutActionStatus.Approved;
            action.DecidedById = _currentUser.UserId;
            action.DecidedByName = ActorName();
            action.DecidedAtUtc = now;
            action.DecisionComment = request.Comment.Trim();
            action.ReadinessSnapshotJson = Serialize(evaluation.Checks);
            action.SourceSnapshotJson = evaluation.SourceSnapshot;
            action.SourceSnapshotHash = Hash(evaluation.SourceSnapshot);
            StampUpdate(action, now);
            action.IntegrityHash = ActionHash(action);
            await Actions.UpdateAsync(action);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (action.ActionType == ProcurementWorksCloseoutActionType.DefectRectification &&
                action.ProjectDefectLiabilityCaseId.HasValue)
            {
                var defect = await Defects.GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == action.ProjectDefectLiabilityCaseId.Value &&
                        !item.IsDeleted)
                    .SingleAsync(cancellationToken);
                if (string.Equals(defect.Status, ProjectDefectLiabilityStatuses.Resolved,
                        StringComparison.OrdinalIgnoreCase))
                {
                    defect.Status = ProjectDefectLiabilityStatuses.Closed;
                    defect.ResolvedDate ??= now;
                    defect.UpdatedAt = now;
                    defect.UpdatedBy = ActorName();
                    defect.LastModifiedById = _currentUser.UserId;
                    await Defects.UpdateAsync(defect);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }

            if (action.ActionType == ProcurementWorksCloseoutActionType.Termination)
            {
                contract.Status = "Terminated";
                contract.TerminatedAt = action.EffectiveAtUtc ?? now;
                contract.TerminationReason = action.Reason[..Math.Min(action.Reason.Length, 500)];
                StampContract(contract, now);
                await Contracts.UpdateAsync(contract);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else if (action.ActionType == ProcurementWorksCloseoutActionType.Closeout)
            {
                contract.Status = "Completed";
                contract.CompletedAt = action.EffectiveAtUtc ?? now;
                StampContract(contract, now);
                await Contracts.UpdateAsync(contract);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await RecordEventAsync(action, "WorksCloseoutApproved",
                ProcurementControlEventResult.Allowed, null,
                new
                {
                    action.ActionType,
                    action.Status,
                    ContractStatus = contract.Status,
                    action.RequiresIndependentFinanceApproval,
                    action.AmountAutoPosted
                },
                request.Comment, correlation, evaluation.Checks, cancellationToken);
            }
            finally
            {
                await _store.ClearMutationContextAsync(cancellationToken);
            }
        }, cancellationToken);

        if (revalidationFailed)
            return Map(await LoadActionAsync(actionId, cancellationToken));
        await PublishAsync("procurement.works-closeout.approved", action,
            cancellationToken);
        return Map(await LoadActionAsync(actionId, cancellationToken));
    }

    private async Task<Evaluation> EvaluateAsync(
        Contract contract,
        Project project,
        IReadOnlyCollection<ProcurementWorksCloseoutAction> history,
        SubmitProcurementWorksCloseoutActionRequest request,
        IReadOnlyCollection<ProcurementWorksCloseoutEvidence> evidence,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var checks = new List<ProcurementWorksCloseoutCheckDto>();
        var profile = await LoadProfileAsync(cancellationToken);
        checks.Add(Passed("configuration", "Configuration profile",
            "WORKS_CONFIGURATION_READY",
            $"{profile.ProfileCode} v{profile.Version}", profile.Id,
            $"{profile.ProfileCode}:v{profile.Version}"));

        var amount = request.Amount ??
                     (request.ActionType is ProcurementWorksCloseoutActionType.InitialTakeover
                         or ProcurementWorksCloseoutActionType.FinalTakeover
                         or ProcurementWorksCloseoutActionType.Closeout
                         ? contract.ContractValue
                         : 0m);
        var authority = await _compliance.EvaluateAuthorityRouteAsync(
            new ProcurementAuthorityRouteDecisionRequest
            {
                Category = ProcurementCategoryClass.Works,
                Amount = amount,
                CurrencyCode = (request.Currency ?? contract.Currency).ToUpperInvariant(),
                AtUtc = DateTime.UtcNow,
                SourceType = "WorksCloseout",
                SourceReference = $"{contract.ContractNumber}:{request.ActionType}"
            }, correlationId, cancellationToken);
        if (!authority.IsReady || authority.Policy is null ||
            authority.Workflow is null || authority.Steps.Count == 0)
            throw Validation("WORKS_CLOSEOUT_AUTHORITY_BLOCKED", authority.Message);
        checks.Add(Passed("authority", "Statutory authority and workflow",
            authority.DecisionCode, authority.Message,
            authority.Steps.First().RuleId,
            $"{authority.Steps.First().AuthorityName} / {authority.Workflow.Name} v{authority.Workflow.Version}"));

        var state = await LoadSourceStateAsync(contract, project, history, cancellationToken);
        checks.Add(Passed("contract-type", "Works contract",
            "WORKS_CONTRACT_CONFIRMED", "The contract is classified as Works.",
            contract.Id, contract.ContractNumber));
        checks.Add(Passed("project", "Linked project", "WORKS_PROJECT_LINKED",
            $"Project {project.ProjectCode} is linked to the contract.",
            project.Id, project.ProjectCode));
        AddActionChecks(checks, contract, state, history, request);

        foreach (var key in ProcurementWorksCloseoutRules.RequiredEvidence[request.ActionType])
        {
            var match = evidence.FirstOrDefault(item =>
                string.Equals(item.RequirementKey, key,
                    StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                checks.Add(Pending(key, Humanize(key),
                    "WORKS_CLOSEOUT_EVIDENCE_REQUIRED",
                    $"Verified evidence is required: {Humanize(key)}."));
                continue;
            }
            checks.Add(await IsEvidenceCurrentAsync(contract, match, cancellationToken)
                ? Passed(key, match.RequirementLabel,
                    "WORKS_CLOSEOUT_EVIDENCE_VERIFIED",
                    "Current, verified, malware-clean central evidence is linked.",
                    match.WorkflowEvidenceDocumentId ?? match.FileUploadRecordId,
                    match.EvidenceReference)
                : Failed(key, match.RequirementLabel,
                    "WORKS_CLOSEOUT_EVIDENCE_STALE",
                    "The linked evidence is no longer current, tenant-safe, verified, or malware-clean."));
        }

        var snapshot = Serialize(new
        {
            schemaVersion = "tdc.works-closeout.source.v1",
            contract.Id,
            contract.TenantId,
            contract.ContractNumber,
            contract.ContractType,
            contract.Status,
            contract.ContractValue,
            contract.Currency,
            contract.RetentionPercentage,
            contract.WarrantyPeriodDays,
            contract.StartDate,
            contract.EndDate,
            contract.RowVersion,
            ProjectId = project.Id,
            project.ProjectCode,
            ProjectStatus = project.Status,
            state.CompletedPracticalTakeovers,
            state.CompletedFinalTakeovers,
            OpenDefectIds = state.Defects.Where(item =>
                    ProcurementWorksCloseoutRules.IsOpenDefectStatus(item.Status))
                .Select(item => item.Id),
            state.RetentionHeld,
            state.RetentionReleased,
            FinalAccount = state.FinalAccount is null ? null : new
            {
                state.FinalAccount.Id,
                state.FinalAccount.Status,
                state.FinalAccount.FinalAccountValue,
                state.FinalAccount.Currency,
                state.FinalAccount.UpdatedAt
            },
            request.ActionType,
            request.ProjectHandoverItemId,
            request.ProjectDefectLiabilityCaseId,
            request.ProjectFinalAccountId,
            request.ProjectPaymentCertificateId,
            request.PerformanceBondRequestId,
            request.EffectiveAtUtc,
            request.Amount,
            RequestCurrency = request.Currency
        });
        return new Evaluation(profile, authority, checks, snapshot);
    }

    private static void AddActionChecks(
        ICollection<ProcurementWorksCloseoutCheckDto> checks,
        Contract contract,
        SourceState state,
        IReadOnlyCollection<ProcurementWorksCloseoutAction> history,
        SubmitProcurementWorksCloseoutActionRequest request)
    {
        var hasInitial = history.Any(item =>
            item.ActionType == ProcurementWorksCloseoutActionType.InitialTakeover &&
            item.Status == ProcurementWorksCloseoutActionStatus.Approved);
        var initial = history.LastOrDefault(item =>
            item.ActionType == ProcurementWorksCloseoutActionType.InitialTakeover &&
            item.Status == ProcurementWorksCloseoutActionStatus.Approved);
        var hasFinal = history.Any(item =>
            item.ActionType == ProcurementWorksCloseoutActionType.FinalTakeover &&
            item.Status == ProcurementWorksCloseoutActionStatus.Approved);
        var activeDispute = HasActiveDispute(history);
        var openDefects = state.Defects.Count(item =>
            ProcurementWorksCloseoutRules.IsOpenDefectStatus(item.Status));
        var dlpEnd = ProcurementWorksCloseoutRules.DefectsLiabilityEnd(
            initial?.EffectiveAtUtc ?? initial?.DecidedAtUtc,
            contract.WarrantyPeriodDays);
        var dlpEnded = !dlpEnd.HasValue || dlpEnd.Value <= DateTime.UtcNow;
        var alreadyApproved = history.Any(item =>
            item.ActionType == request.ActionType &&
            item.Status == ProcurementWorksCloseoutActionStatus.Approved &&
            (request.ActionType != ProcurementWorksCloseoutActionType.DefectRectification ||
             item.ProjectDefectLiabilityCaseId == request.ProjectDefectLiabilityCaseId));

        if (request.ActionType is not ProcurementWorksCloseoutActionType.DisputeOpen
            and not ProcurementWorksCloseoutActionType.DisputeResolve
            and not ProcurementWorksCloseoutActionType.DefectRectification)
            AddCondition(checks, !alreadyApproved, "duplicate", "Prior approved action",
                "WORKS_ACTION_AVAILABLE", "No equivalent approved action exists.",
                "WORKS_ACTION_ALREADY_APPROVED",
                $"An approved {request.ActionType} action already exists.");

        switch (request.ActionType)
        {
            case ProcurementWorksCloseoutActionType.InitialTakeover:
                var practical = state.HandoverItems.SingleOrDefault(item =>
                    item.Id == request.ProjectHandoverItemId);
                AddCondition(checks,
                    practical is not null &&
                    string.Equals(practical.HandoverType,
                        ProjectHandoverItemTypes.PracticalCompletion,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(practical.Status,
                        ProjectHandoverItemStatuses.Completed,
                        StringComparison.OrdinalIgnoreCase),
                    "practical-completion", "Practical completion",
                    "WORKS_PRACTICAL_COMPLETION_READY",
                    "The linked practical-completion item is complete.",
                    "WORKS_PRACTICAL_COMPLETION_REQUIRED",
                    "Link a completed practical-completion handover item.");
                break;

            case ProcurementWorksCloseoutActionType.DefectRectification:
                var defect = state.Defects.SingleOrDefault(item =>
                    item.Id == request.ProjectDefectLiabilityCaseId);
                AddCondition(checks,
                    defect is not null &&
                    (string.Equals(defect.Status,
                         ProjectDefectLiabilityStatuses.Resolved,
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(defect.Status,
                         ProjectDefectLiabilityStatuses.Closed,
                         StringComparison.OrdinalIgnoreCase)),
                    "defect", "Resolved defect",
                    "WORKS_DEFECT_RECTIFIED",
                    "The linked defect is resolved and ready for independent closure.",
                    "WORKS_DEFECT_NOT_RECTIFIED",
                    "Link a current-project defect in Resolved or Closed status.");
                AddCondition(checks, !alreadyApproved, "defect-approval",
                    "Defect closure approval", "WORKS_DEFECT_APPROVAL_AVAILABLE",
                    "The linked defect has no prior approved rectification action.",
                    "WORKS_DEFECT_ALREADY_APPROVED",
                    "The linked defect already has approved rectification evidence.");
                break;

            case ProcurementWorksCloseoutActionType.FinalTakeover:
                var final = state.HandoverItems.SingleOrDefault(item =>
                    item.Id == request.ProjectHandoverItemId);
                AddCondition(checks, hasInitial, "initial-takeover",
                    "Initial takeover", "WORKS_INITIAL_TAKEOVER_APPROVED",
                    "Initial takeover is approved.",
                    "WORKS_INITIAL_TAKEOVER_REQUIRED",
                    "Initial takeover must be approved first.");
                AddCondition(checks,
                    final is not null &&
                    string.Equals(final.HandoverType,
                        ProjectHandoverItemTypes.FinalCompletion,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(final.Status,
                        ProjectHandoverItemStatuses.Completed,
                        StringComparison.OrdinalIgnoreCase),
                    "final-completion", "Final completion",
                    "WORKS_FINAL_COMPLETION_READY",
                    "The linked final-completion item is complete.",
                    "WORKS_FINAL_COMPLETION_REQUIRED",
                    "Link a completed final-completion handover item.");
                AddCondition(checks, dlpEnded, "defects-liability-period",
                    "Defects-liability period", "WORKS_DLP_ENDED",
                    "The defects-liability period has ended.",
                    "WORKS_DLP_ACTIVE",
                    "Final takeover cannot occur before the defects-liability period ends.");
                AddNoOpenItems(checks, openDefects, activeDispute);
                break;

            case ProcurementWorksCloseoutActionType.WarrantyRelease:
                AddPostCompletionChecks(checks, hasFinal, dlpEnded, openDefects, activeDispute);
                break;

            case ProcurementWorksCloseoutActionType.PerformanceSecurityRelease:
                AddCondition(checks, hasFinal, "final-takeover", "Final takeover",
                    "WORKS_FINAL_TAKEOVER_APPROVED", "Final takeover is approved.",
                    "WORKS_FINAL_TAKEOVER_REQUIRED",
                    "Final takeover must be approved first.");
                AddCondition(checks,
                    request.PerformanceBondRequestId.HasValue &&
                    state.PerformanceBond?.Id == request.PerformanceBondRequestId &&
                    string.Equals(state.PerformanceBond.Status, "Approved",
                        StringComparison.OrdinalIgnoreCase),
                    "performance-security", "Approved performance security",
                    "WORKS_SECURITY_APPROVED",
                    "The linked performance security is approved.",
                    "WORKS_SECURITY_INVALID",
                    "The performance security must belong to the contract award and be approved.");
                AddNoOpenItems(checks, openDefects, activeDispute);
                break;

            case ProcurementWorksCloseoutActionType.RetentionRelease:
                AddPostCompletionChecks(checks, hasFinal, dlpEnded, openDefects, activeDispute);
                var available = Math.Max(state.RetentionHeld - state.RetentionReleased, 0m);
                AddCondition(checks,
                    available > 0m &&
                    ProcurementWorksCloseoutRules.AmountMatches(
                        request.Amount, available),
                    "retention-amount", "Retention release amount",
                    "WORKS_RETENTION_AMOUNT_READY",
                    $"The requested amount matches available retention {available:N2}.",
                    "WORKS_RETENTION_AMOUNT_INVALID",
                    $"The controlled release must equal available retention {available:N2}.");
                AddCurrencyCheck(checks, request.Currency, state.Currency);
                break;

            case ProcurementWorksCloseoutActionType.DisputeOpen:
                AddCondition(checks, !activeDispute, "dispute", "Dispute status",
                    "WORKS_DISPUTE_CAN_OPEN", "No unresolved dispute exists.",
                    "WORKS_DISPUTE_ALREADY_OPEN",
                    "Resolve the active dispute before opening another.");
                break;

            case ProcurementWorksCloseoutActionType.DisputeResolve:
                AddCondition(checks, activeDispute, "dispute", "Dispute status",
                    "WORKS_DISPUTE_ACTIVE", "An active dispute is available for resolution.",
                    "WORKS_DISPUTE_NOT_OPEN", "No active dispute exists.");
                break;

            case ProcurementWorksCloseoutActionType.Termination:
                AddCondition(checks,
                    request.Amount.HasValue && request.Amount.Value >= 0m,
                    "termination-account-amount", "Termination account amount",
                    "WORKS_TERMINATION_AMOUNT_READY",
                    "The termination account amount is recorded for independent Finance processing.",
                    "WORKS_TERMINATION_AMOUNT_REQUIRED",
                    "Record the termination account amount; this does not auto-post Finance.");
                AddCurrencyCheck(checks, request.Currency, state.Currency);
                AddCondition(checks,
                    !history.Any(item =>
                        item.ActionType == ProcurementWorksCloseoutActionType.Termination &&
                        item.Status == ProcurementWorksCloseoutActionStatus.Approved),
                    "termination", "Termination status",
                    "WORKS_TERMINATION_AVAILABLE",
                    "No prior approved termination exists.",
                    "WORKS_TERMINATION_ALREADY_APPROVED",
                    "The contract already has an approved termination.");
                break;

            case ProcurementWorksCloseoutActionType.FinalAccount:
                AddCondition(checks, hasFinal, "final-takeover", "Final takeover",
                    "WORKS_FINAL_TAKEOVER_APPROVED", "Final takeover is approved.",
                    "WORKS_FINAL_TAKEOVER_REQUIRED",
                    "Final takeover must be approved first.");
                AddCondition(checks,
                    state.FinalAccount is not null &&
                    state.FinalAccount.Id == request.ProjectFinalAccountId &&
                    (string.Equals(state.FinalAccount.Status,
                         ProjectFinalAccountStatuses.Approved,
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(state.FinalAccount.Status,
                         ProjectFinalAccountStatuses.Closed,
                         StringComparison.OrdinalIgnoreCase)),
                    "final-account", "Approved project final account",
                    "WORKS_FINAL_ACCOUNT_APPROVED",
                    "The linked project final account is approved.",
                    "WORKS_FINAL_ACCOUNT_NOT_APPROVED",
                    "Link the current project final account in Approved or Closed status.");
                AddCondition(checks,
                    state.FinalAccount is not null &&
                    ProcurementWorksCloseoutRules.AmountMatches(
                        request.Amount, state.FinalAccount.FinalAccountValue),
                    "final-account-amount", "Final account amount",
                    "WORKS_FINAL_ACCOUNT_AMOUNT_MATCH",
                    "The requested amount matches the approved project final account.",
                    "WORKS_FINAL_ACCOUNT_AMOUNT_MISMATCH",
                    "The requested amount must match the approved project final account.");
                AddCurrencyCheck(checks, request.Currency,
                    state.FinalAccount?.Currency ?? state.Currency);
                AddNoOpenItems(checks, openDefects, activeDispute);
                break;

            case ProcurementWorksCloseoutActionType.Closeout:
                AddCondition(checks,
                    state.ProjectClosure is not null &&
                    string.Equals(state.ProjectClosure.Status, ProjectStatuses.Closed,
                        StringComparison.OrdinalIgnoreCase),
                    "project-closure", "Approved project closure",
                    "WORKS_PROJECT_CLOSURE_APPROVED",
                    "The shared Project closure workflow is approved and closed.",
                    "WORKS_PROJECT_CLOSURE_REQUIRED",
                    "Complete the existing Project closure workflow before contract closeout.");
                AddCondition(checks, hasFinal, "final-takeover", "Final takeover",
                    "WORKS_FINAL_TAKEOVER_APPROVED", "Final takeover is approved.",
                    "WORKS_FINAL_TAKEOVER_REQUIRED",
                    "Final takeover must be approved first.");
                AddCondition(checks,
                    history.Any(item =>
                        item.ActionType == ProcurementWorksCloseoutActionType.FinalAccount &&
                        item.Status == ProcurementWorksCloseoutActionStatus.Approved),
                    "final-account", "Final account",
                    "WORKS_FINAL_ACCOUNT_CONTROLLED",
                    "The final account action is approved.",
                    "WORKS_FINAL_ACCOUNT_ACTION_REQUIRED",
                    "Approve the controlled final account action first.");
                AddCondition(checks,
                    contract.RetentionPercentage <= 0m ||
                    state.RetentionHeld <= state.RetentionReleased + 0.01m ||
                    history.Any(item =>
                        item.ActionType == ProcurementWorksCloseoutActionType.RetentionRelease &&
                        item.Status == ProcurementWorksCloseoutActionStatus.Approved),
                    "retention", "Retention release",
                    "WORKS_RETENTION_CONTROLLED",
                    "Required retention release is approved or not applicable.",
                    "WORKS_RETENTION_RELEASE_REQUIRED",
                    "Approve the retention release before closeout.");
                AddCondition(checks,
                    state.PerformanceBond is null ||
                    history.Any(item =>
                        item.ActionType == ProcurementWorksCloseoutActionType.PerformanceSecurityRelease &&
                        item.Status == ProcurementWorksCloseoutActionStatus.Approved),
                    "performance-security", "Performance-security release",
                    "WORKS_SECURITY_RELEASE_CONTROLLED",
                    "Required performance-security release is approved or not applicable.",
                    "WORKS_SECURITY_RELEASE_REQUIRED",
                    "Approve performance-security release before closeout.");
                AddCondition(checks,
                    contract.WarrantyPeriodDays.GetValueOrDefault() <= 0 ||
                    history.Any(item =>
                        item.ActionType == ProcurementWorksCloseoutActionType.WarrantyRelease &&
                        item.Status == ProcurementWorksCloseoutActionStatus.Approved),
                    "warranty", "Warranty release",
                    "WORKS_WARRANTY_RELEASE_CONTROLLED",
                    "Required warranty release is approved or not applicable.",
                    "WORKS_WARRANTY_RELEASE_REQUIRED",
                    "Approve warranty release before closeout.");
                AddNoOpenItems(checks, openDefects, activeDispute);
                break;
        }
    }

    private static void AddPostCompletionChecks(
        ICollection<ProcurementWorksCloseoutCheckDto> checks,
        bool hasFinal,
        bool dlpEnded,
        int openDefects,
        bool activeDispute)
    {
        AddCondition(checks, hasFinal, "final-takeover", "Final takeover",
            "WORKS_FINAL_TAKEOVER_APPROVED", "Final takeover is approved.",
            "WORKS_FINAL_TAKEOVER_REQUIRED",
            "Final takeover must be approved first.");
        AddCondition(checks, dlpEnded, "defects-liability-period",
            "Defects-liability period", "WORKS_DLP_ENDED",
            "The defects-liability/warranty period has ended.",
            "WORKS_DLP_ACTIVE",
            "The defects-liability/warranty period has not ended.");
        AddNoOpenItems(checks, openDefects, activeDispute);
    }

    private static void AddNoOpenItems(
        ICollection<ProcurementWorksCloseoutCheckDto> checks,
        int openDefects,
        bool activeDispute)
    {
        AddCondition(checks, openDefects == 0, "open-defects", "Open defects",
            "WORKS_DEFECTS_CLEARED", "No unresolved defects remain.",
            "WORKS_DEFECTS_OPEN", $"{openDefects} unresolved defect(s) remain.");
        AddCondition(checks, !activeDispute, "active-dispute", "Active dispute",
            "WORKS_NO_ACTIVE_DISPUTE", "No unresolved dispute remains.",
            "WORKS_DISPUTE_OPEN", "An unresolved dispute blocks this action.");
    }

    private static void AddCurrencyCheck(
        ICollection<ProcurementWorksCloseoutCheckDto> checks,
        string? requested,
        string expected)
    {
        AddCondition(checks,
            !string.IsNullOrWhiteSpace(requested) &&
            string.Equals(requested, expected, StringComparison.OrdinalIgnoreCase),
            "currency", "Currency", "WORKS_CURRENCY_MATCH",
            $"The request currency matches {expected.ToUpperInvariant()}.",
            "WORKS_CURRENCY_MISMATCH",
            $"The request currency must match {expected.ToUpperInvariant()}.");
    }

    private async Task<SourceState> LoadSourceStateAsync(
        Contract contract,
        Project project,
        IReadOnlyCollection<ProcurementWorksCloseoutAction> history,
        CancellationToken cancellationToken)
    {
        var handovers = await HandoverItems.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProjectId == project.Id && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        var defects = await Defects.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProjectId == project.Id && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        var certificates = await PaymentCertificates.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProjectId == project.Id && !item.IsDeleted &&
                (!item.ContractId.HasValue || item.ContractId == contract.Id))
            .AsNoTracking().ToListAsync(cancellationToken);
        var finalAccount = await FinalAccounts.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProjectId == project.Id && !item.IsDeleted &&
                (!item.ContractId.HasValue || item.ContractId == contract.Id))
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var performanceBond = await PerformanceBonds.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderAwardId == contract.TenderAwardId && !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var projectClosure = await ProjectClosures.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProjectId == project.Id && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var certified = certificates.Where(item =>
                string.Equals(item.Status, ProjectPaymentCertificateStatuses.Approved,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Status, ProjectPaymentCertificateStatuses.Paid,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new SourceState(
            project,
            handovers,
            defects,
            certificates,
            finalAccount,
            performanceBond,
            projectClosure,
            handovers.Count(item =>
                string.Equals(item.HandoverType,
                    ProjectHandoverItemTypes.PracticalCompletion,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Status, ProjectHandoverItemStatuses.Completed,
                    StringComparison.OrdinalIgnoreCase)),
            handovers.Count(item =>
                string.Equals(item.HandoverType,
                    ProjectHandoverItemTypes.FinalCompletion,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Status, ProjectHandoverItemStatuses.Completed,
                    StringComparison.OrdinalIgnoreCase)),
            certified.Sum(item => item.RetentionHeldAmount),
            certified.Sum(item => item.RetentionReleasedAmount),
            finalAccount?.Currency ?? contract.Currency,
            history);
    }

    private async Task<List<ProcurementWorksCloseoutEvidence>> ValidateEvidenceAsync(
        Contract contract,
        ProcurementWorksCloseoutActionType actionType,
        IEnumerable<ProcurementWorksCloseoutEvidenceRequest> requests,
        CancellationToken cancellationToken)
    {
        var rows = requests.ToList();
        var required = ProcurementWorksCloseoutRules.RequiredEvidence[actionType];
        if (rows.Count == 0)
            throw Validation("WORKS_CLOSEOUT_EVIDENCE_REQUIRED",
                "Works closeout evidence is required.");
        if (rows.GroupBy(item => item.RequirementKey.Trim(),
                StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw Validation("WORKS_CLOSEOUT_EVIDENCE_DUPLICATE",
                "Each evidence requirement can be linked only once.");
        var unknown = rows.FirstOrDefault(item =>
            !required.Contains(item.RequirementKey.Trim(),
                StringComparer.OrdinalIgnoreCase));
        if (unknown is not null)
            throw Validation("WORKS_CLOSEOUT_EVIDENCE_KEY_UNKNOWN",
                $"Evidence key '{unknown.RequirementKey}' does not apply to {actionType}.");

        var result = new List<ProcurementWorksCloseoutEvidence>();
        foreach (var request in rows)
        {
            Require(request.RequirementKey, "WORKS_CLOSEOUT_EVIDENCE_KEY_REQUIRED",
                "Every evidence item requires a requirement key.");
            Require(request.EvidenceReference,
                "WORKS_CLOSEOUT_EVIDENCE_REFERENCE_REQUIRED",
                "Every evidence item requires a reference.");
            Guid? workflowId = null;
            Guid? uploadId = null;
            string sourceHash;
            string serverReference;
            if (!Enum.IsDefined(request.ReferenceKind))
                throw Validation("WORKS_CLOSEOUT_EVIDENCE_KIND_INVALID",
                    "The evidence reference kind is invalid.");
            if (request.ReferenceKind ==
                ProcurementContractActivationEvidenceKind.WorkflowEvidenceDocument)
            {
                if (!request.WorkflowEvidenceDocumentId.HasValue ||
                    request.FileUploadRecordId.HasValue)
                    throw Validation("WORKS_CLOSEOUT_EVIDENCE_REFERENCE_INVALID",
                        "Workflow evidence requires exactly one workflow evidence document ID.");
                var evidence = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == request.WorkflowEvidenceDocumentId.Value &&
                        !item.IsDeleted)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFound("WORKS_CLOSEOUT_EVIDENCE_NOT_FOUND",
                        "Workflow evidence was not found in the current tenant.");
                if (!evidence.IsCurrent ||
                    evidence.VerificationStatus != WorkflowEvidenceVerificationStatus.Verified ||
                    evidence.MalwareScanStatus != WorkflowMalwareScanStatus.Clean ||
                    evidence.ExpiryDate.HasValue &&
                    evidence.ExpiryDate.Value < DateTime.UtcNow)
                    throw Conflict("WORKS_CLOSEOUT_EVIDENCE_NOT_VERIFIED",
                        "Workflow evidence must be current, unexpired, verified, and malware-clean.");
                workflowId = evidence.Id;
                sourceHash = evidence.Sha256;
                serverReference = $"workflow-evidence:{evidence.Id:N}";
            }
            else
            {
                if (!request.FileUploadRecordId.HasValue ||
                    request.WorkflowEvidenceDocumentId.HasValue)
                    throw Validation("WORKS_CLOSEOUT_UPLOAD_REFERENCE_INVALID",
                        "Central-DMS evidence requires exactly one controlled upload ID.");
                var document = await _unitOfWork.Repository<ContractDocument>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.ContractId == contract.Id &&
                        item.FileUploadRecordId == request.FileUploadRecordId.Value &&
                        item.CentralDocumentRecordId.HasValue &&
                        item.CentralDocumentVersionId.HasValue &&
                        !item.IsDeleted)
                    .Include(item => item.FileUploadRecord)
                    .Include(item => item.CentralDocumentRecord)
                    .Include(item => item.CentralDocumentVersion)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFound("WORKS_CLOSEOUT_DMS_DOCUMENT_NOT_FOUND",
                        "The selected evidence is not a central-DMS document for this contract.");
                if (document.FileUploadRecord is null ||
                    document.FileUploadRecord.VirusScanStatus != FileVirusScanStatus.Clean ||
                    document.CentralDocumentRecord is null ||
                    document.CentralDocumentRecord.SourceEntityType != "Contract" ||
                    document.CentralDocumentRecord.SourceRecordId != contract.Id ||
                    document.CentralDocumentVersion is null ||
                    document.CentralDocumentVersion.FileUploadRecordId !=
                    document.FileUploadRecordId)
                    throw Conflict("WORKS_CLOSEOUT_DMS_DOCUMENT_UNSAFE",
                        "Contract evidence must be current central-DMS content with a Clean malware scan.");
                uploadId = document.FileUploadRecordId;
                serverReference =
                    $"dms:{document.CentralDocumentRecordId!.Value:N}:version:{document.CentralDocumentVersionId!.Value:N}";
                sourceHash = Hash(Serialize(new
                {
                    document.FileUploadRecordId,
                    document.CentralDocumentRecordId,
                    document.CentralDocumentVersionId,
                    document.FileUploadRecord.FilePath,
                    document.FileUploadRecord.FileSize
                }));
            }

            var key = request.RequirementKey.Trim();
            result.Add(new ProcurementWorksCloseoutEvidence
            {
                Id = Guid.NewGuid(),
                RequirementKey = key,
                RequirementLabel = Humanize(key),
                ReferenceKind = request.ReferenceKind,
                WorkflowEvidenceDocumentId = workflowId,
                FileUploadRecordId = uploadId,
                EvidenceReference = serverReference,
                EvidenceHash = sourceHash
            });
        }
        return result;
    }

    private async Task<bool> IsEvidenceCurrentAsync(
        Contract contract,
        ProcurementWorksCloseoutEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (evidence.ReferenceKind ==
            ProcurementContractActivationEvidenceKind.WorkflowEvidenceDocument)
        {
            if (!evidence.WorkflowEvidenceDocumentId.HasValue) return false;
            return await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == evidence.WorkflowEvidenceDocumentId.Value &&
                    item.IsCurrent &&
                    item.VerificationStatus == WorkflowEvidenceVerificationStatus.Verified &&
                    item.MalwareScanStatus == WorkflowMalwareScanStatus.Clean &&
                    (!item.ExpiryDate.HasValue || item.ExpiryDate.Value >= DateTime.UtcNow) &&
                    !item.IsDeleted)
                .AsNoTracking().AnyAsync(cancellationToken);
        }
        if (!evidence.FileUploadRecordId.HasValue) return false;
        return await _unitOfWork.Repository<ContractDocument>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ContractId == contract.Id &&
                item.FileUploadRecordId == evidence.FileUploadRecordId.Value &&
                item.CentralDocumentRecordId.HasValue &&
                item.CentralDocumentVersionId.HasValue &&
                !item.IsDeleted)
            .Include(item => item.FileUploadRecord)
            .Include(item => item.CentralDocumentRecord)
            .Include(item => item.CentralDocumentVersion)
            .AsNoTracking()
            .AnyAsync(item =>
                item.FileUploadRecord != null &&
                item.FileUploadRecord.VirusScanStatus == FileVirusScanStatus.Clean &&
                !item.FileUploadRecord.IsDeleted &&
                item.CentralDocumentRecord != null &&
                item.CentralDocumentRecord.TenantId == _currentUser.TenantId &&
                item.CentralDocumentRecord.SourceEntityType == "Contract" &&
                item.CentralDocumentRecord.SourceRecordId == contract.Id &&
                !item.CentralDocumentRecord.IsDeleted &&
                item.CentralDocumentVersion != null &&
                item.CentralDocumentVersion.TenantId == _currentUser.TenantId &&
                item.CentralDocumentVersion.DocumentRecordId ==
                    item.CentralDocumentRecordId &&
                item.CentralDocumentVersion.FileUploadRecordId ==
                    item.FileUploadRecordId &&
                !item.CentralDocumentVersion.IsDeleted,
                cancellationToken);
    }

    private async Task<ProcurementConfigurationProfileDto> LoadProfileAsync(
        CancellationToken cancellationToken)
    {
        var profile = await _configuration.GetEffectiveProfileAsync(
            "TDC-PROCUREMENT", DateTime.UtcNow, cancellationToken)
            ?? throw Validation("WORKS_CLOSEOUT_CONFIGURATION_MISSING",
                "No effective Published TDC procurement configuration profile exists.");
        if (profile.Decisions.Count != 14 ||
            profile.Decisions.Any(item => !item.IsComplete))
            throw Validation("WORKS_CLOSEOUT_CONFIGURATION_INCOMPLETE",
                "The effective profile must contain fourteen complete approved decisions.");
        return profile;
    }

    private async Task<Contract> LoadContractAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await Contracts.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == id && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw NotFound("CONTRACT_NOT_FOUND",
            "The contract was not found in the current tenant.");

    private async Task<Project?> LoadProjectAsync(
        Guid contractId,
        bool required,
        CancellationToken cancellationToken)
    {
        var projects = await Projects.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ContractId == contractId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (projects.Count > 1)
            throw Conflict("WORKS_CLOSEOUT_PROJECT_AMBIGUOUS",
                "More than one current-tenant project is linked to this contract.");
        if (projects.Count == 0 && required)
            throw Validation("WORKS_CLOSEOUT_PROJECT_REQUIRED",
                "Exactly one current-tenant project must be linked to the Works contract.");
        return projects.SingleOrDefault();
    }

    private IQueryable<ProcurementWorksCloseoutAction> ActionQuery() =>
        Actions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Contract)
            .Include(item => item.Project)
            .Include(item => item.Evidence.Where(evidence => !evidence.IsDeleted));

    private async Task<ProcurementWorksCloseoutAction> LoadActionAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await ActionQuery().SingleOrDefaultAsync(item => item.Id == id,
            cancellationToken)
        ?? throw NotFound("WORKS_CLOSEOUT_ACTION_NOT_FOUND",
            "The Works closeout action was not found in the current tenant.");

    private async Task EnsureCapabilityAsync(
        string permission,
        string reference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureInternal();
        if (IsAdministrator()) return;
        try
        {
            var result = await _access.EnforceCapabilityAsync(
                new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = permission,
                    SourceType = EventType,
                    SourceReference = reference
                }, correlationId, cancellationToken);
            if (!result.Allowed) throw Authorization(result.Message);
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            throw Authorization(exception.Message);
        }
    }

    private void EnsureInternal()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty || _currentUser.IsExternalUser)
            throw Authorization("An authenticated internal tenant context is required.");
    }

    private bool IsAdministrator() =>
        _currentUser.Roles.Any(role =>
            role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase));

    private static void EnsureWorksAndStatus(
        Contract contract,
        ProcurementWorksCloseoutActionType actionType)
    {
        if (!ProcurementWorksCloseoutRules.IsWorks(contract.ContractType))
            throw Validation("WORKS_CONTRACT_REQUIRED",
                "Works closeout controls apply only to a Works contract.");
        if (!ProcurementWorksCloseoutRules.CanSubmit(contract.Status, actionType))
            throw Conflict("WORKS_CLOSEOUT_CONTRACT_STATUS_INVALID",
                $"Contract {contract.ContractNumber} cannot perform {actionType} from {contract.Status}.");
    }

    private async Task ExecuteAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        if (_unitOfWork.HasActiveTransaction)
        {
            await action();
            return;
        }
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
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private async Task RecordEventAsync(
        ProcurementWorksCloseoutAction action,
        string eventAction,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        string correlationId,
        IReadOnlyList<ProcurementWorksCloseoutCheckDto>? checks,
        CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "works-closeout", action.TenantId, action.Id,
                eventAction.ToLowerInvariant(), correlationId),
            EventType = EventType,
            Action = eventAction,
            Result = result,
            RuleCode = "CON-005",
            RuleVersion = "TDC-0409",
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = action.ContractId,
            SourceReference = action.Contract?.ContractNumber ??
                              action.ContractId.ToString(),
            Reason = reason,
            InputValues = new
            {
                action.ActionType,
                action.ProjectId,
                action.ConfigurationProfileId,
                action.ConfigurationProfileVersion,
                action.PolicySetId,
                action.PolicyVersion,
                action.AuthorityRuleId,
                action.WorkflowDefinitionId,
                action.WorkflowInstanceId,
                action.ProjectHandoverItemId,
                action.ProjectDefectLiabilityCaseId,
                action.ProjectFinalAccountId,
                action.ProjectPaymentCertificateId,
                action.PerformanceBondRequestId,
                action.Amount,
                action.Currency,
                action.RequiresIndependentFinanceApproval,
                action.AmountAutoPosted,
                action.SourceSnapshotHash
            },
            ResultValues = new
            {
                action.Status,
                action.DecidedById,
                checks
            },
            Before = before,
            After = after,
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = action.Evidence.Select(item =>
                new ProcurementControlEventEvidenceReference
                {
                    ReferenceKind = item.WorkflowEvidenceDocumentId.HasValue
                        ? ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument
                        : ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                    ReferenceId = item.WorkflowEvidenceDocumentId ??
                                  item.FileUploadRecordId,
                    Reference = item.EvidenceReference,
                    Label = item.RequirementLabel,
                    RequirementKey = item.RequirementKey
                }).ToList()
        }, cancellationToken);
    }

    private async Task PublishAsync(
        string topic,
        ProcurementWorksCloseoutAction action,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = action.TenantId,
                TopicKey = topic,
                NotificationType = EventType,
                EntityType = SourceType,
                EntityId = action.ContractId,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["actionId"] = action.Id,
                    ["contractId"] = action.ContractId,
                    ["contractNumber"] = action.Contract?.ContractNumber ?? string.Empty,
                    ["actionType"] = action.ActionType.ToString(),
                    ["status"] = action.Status.ToString(),
                    ["amountAutoPosted"] = action.AmountAutoPosted
                },
                Metadata = new Dictionary<string, object>
                {
                    ["correlationId"] = action.CorrelationId,
                    ["ruleCode"] = "CON-005",
                    ["ruleVersion"] = "TDC-0409"
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish Works closeout notification {Topic} for {ActionId}",
                topic, action.Id);
        }
    }

    private static ProcurementWorksCloseoutProjectSummaryDto MapProjectSummary(
        SourceState state) => new()
    {
        ProjectId = state.Project.Id,
        ProjectCode = state.Project.ProjectCode,
        ProjectTitle = state.Project.Title,
        ProjectStatus = state.Project.Status,
        CompletedPracticalTakeovers = state.CompletedPracticalTakeovers,
        CompletedFinalTakeovers = state.CompletedFinalTakeovers,
        OpenDefects = state.Defects.Count(item =>
            ProcurementWorksCloseoutRules.IsOpenDefectStatus(item.Status)),
        ClosedDefects = state.Defects.Count(item =>
            !ProcurementWorksCloseoutRules.IsOpenDefectStatus(item.Status)),
        RetentionHeld = state.RetentionHeld,
        RetentionReleased = state.RetentionReleased,
        FinalAccountId = state.FinalAccount?.Id,
        FinalAccountStatus = state.FinalAccount?.Status,
        FinalAccountValue = state.FinalAccount?.FinalAccountValue,
        Currency = state.Currency,
        ProjectClosureStatus = state.ProjectClosure?.Status
    };

    private static ProcurementWorksCloseoutActionDto Map(
        ProcurementWorksCloseoutAction item) => new()
    {
        Id = item.Id,
        ContractId = item.ContractId,
        ProjectId = item.ProjectId,
        Sequence = item.Sequence,
        ActionType = item.ActionType,
        Status = item.Status,
        ConfigurationProfileId = item.ConfigurationProfileId,
        ConfigurationProfileVersion = item.ConfigurationProfileVersion,
        PolicySetId = item.PolicySetId,
        PolicyVersion = item.PolicyVersion,
        AuthorityRuleId = item.AuthorityRuleId,
        AuthorityName = item.AuthorityName,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        WorkflowInstanceId = item.WorkflowInstanceId,
        ProjectHandoverItemId = item.ProjectHandoverItemId,
        ProjectDefectLiabilityCaseId = item.ProjectDefectLiabilityCaseId,
        ProjectFinalAccountId = item.ProjectFinalAccountId,
        ProjectPaymentCertificateId = item.ProjectPaymentCertificateId,
        PerformanceBondRequestId = item.PerformanceBondRequestId,
        EffectiveAtUtc = item.EffectiveAtUtc,
        DefectsLiabilityEndsAtUtc = item.DefectsLiabilityEndsAtUtc,
        Amount = item.Amount,
        Currency = item.Currency,
        RequiresIndependentFinanceApproval = item.RequiresIndependentFinanceApproval,
        AmountAutoPosted = item.AmountAutoPosted,
        SubmittedById = item.SubmittedById,
        SubmittedByName = item.SubmittedByName,
        SubmittedAtUtc = item.SubmittedAtUtc,
        DecidedById = item.DecidedById,
        DecidedByName = item.DecidedByName,
        DecidedAtUtc = item.DecidedAtUtc,
        Reason = item.Reason,
        DecisionComment = item.DecisionComment,
        IntegrityHash = item.IntegrityHash,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        Evidence = item.Evidence.Where(evidence => !evidence.IsDeleted)
            .Select(evidence => new ProcurementWorksCloseoutEvidenceDto
            {
                Id = evidence.Id,
                RequirementKey = evidence.RequirementKey,
                RequirementLabel = evidence.RequirementLabel,
                ReferenceKind = evidence.ReferenceKind,
                WorkflowEvidenceDocumentId = evidence.WorkflowEvidenceDocumentId,
                FileUploadRecordId = evidence.FileUploadRecordId,
                EvidenceReference = evidence.EvidenceReference,
                EvidenceHash = evidence.EvidenceHash
            }).ToList()
    };

    private static SubmitProcurementWorksCloseoutActionRequest ToSubmitRequest(
        ProcurementWorksCloseoutAction action,
        Contract contract) => new()
    {
        ActionType = action.ActionType,
        ProjectHandoverItemId = action.ProjectHandoverItemId,
        ProjectDefectLiabilityCaseId = action.ProjectDefectLiabilityCaseId,
        ProjectFinalAccountId = action.ProjectFinalAccountId,
        ProjectPaymentCertificateId = action.ProjectPaymentCertificateId,
        PerformanceBondRequestId = action.PerformanceBondRequestId,
        EffectiveAtUtc = action.EffectiveAtUtc,
        Amount = action.Amount,
        Currency = action.Currency,
        Reason = action.Reason,
        IdempotencyKey = action.IdempotencyKey,
        ContractRowVersion = Convert.ToBase64String(contract.RowVersion)
    };

    private static bool HasActiveDispute(
        IEnumerable<ProcurementWorksCloseoutAction> history)
    {
        var approved = history
            .Where(item => item.Status == ProcurementWorksCloseoutActionStatus.Approved &&
                           item.ActionType is ProcurementWorksCloseoutActionType.DisputeOpen
                               or ProcurementWorksCloseoutActionType.DisputeResolve)
            .OrderBy(item => item.Sequence)
            .ToList();
        return approved.LastOrDefault()?.ActionType ==
               ProcurementWorksCloseoutActionType.DisputeOpen;
    }

    private static bool IsApprovedCloseout(ProcurementWorksCloseoutAction item) =>
        item.ActionType == ProcurementWorksCloseoutActionType.Closeout &&
        item.Status == ProcurementWorksCloseoutActionStatus.Approved;

    private static string ActionHash(ProcurementWorksCloseoutAction item) =>
        Hash(Serialize(new
        {
            schemaVersion = "tdc.works-closeout.action.v1",
            item.Id,
            item.TenantId,
            item.ContractId,
            item.ProjectId,
            item.Sequence,
            item.ActionType,
            item.Status,
            item.ConfigurationProfileId,
            item.ConfigurationProfileVersion,
            item.PolicySetId,
            item.PolicyVersion,
            item.AuthorityRuleId,
            item.WorkflowDefinitionId,
            item.WorkflowInstanceId,
            item.ProjectHandoverItemId,
            item.ProjectDefectLiabilityCaseId,
            item.ProjectFinalAccountId,
            item.ProjectPaymentCertificateId,
            item.PerformanceBondRequestId,
            item.EffectiveAtUtc,
            item.DefectsLiabilityEndsAtUtc,
            item.Amount,
            item.Currency,
            item.RequiresIndependentFinanceApproval,
            item.AmountAutoPosted,
            item.SubmittedById,
            item.SubmittedAtUtc,
            item.DecidedById,
            item.DecidedAtUtc,
            item.Reason,
            item.DecisionComment,
            item.IdempotencyKey,
            item.CorrelationId,
            item.SourceSnapshotHash
        }));

    private static ProcurementWorksCloseoutCheckDto Passed(
        string key, string label, string code, string message,
        Guid? referenceId = null, string? reference = null) => new()
    {
        Key = key,
        Label = label,
        Status = ProcurementWorksCloseoutCheckStatus.Passed,
        Code = code,
        Message = message,
        ReferenceId = referenceId,
        Reference = reference
    };

    private static ProcurementWorksCloseoutCheckDto Failed(
        string key, string label, string code, string message) => new()
    {
        Key = key,
        Label = label,
        Status = ProcurementWorksCloseoutCheckStatus.Failed,
        Code = code,
        Message = message
    };

    private static ProcurementWorksCloseoutCheckDto Pending(
        string key, string label, string code, string message) => new()
    {
        Key = key,
        Label = label,
        Status = ProcurementWorksCloseoutCheckStatus.Pending,
        Code = code,
        Message = message
    };

    private static void AddCondition(
        ICollection<ProcurementWorksCloseoutCheckDto> checks,
        bool ready,
        string key,
        string label,
        string readyCode,
        string readyMessage,
        string blockedCode,
        string blockedMessage)
    {
        checks.Add(ready
            ? Passed(key, label, readyCode, readyMessage)
            : Failed(key, label, blockedCode, blockedMessage));
    }

    private static void EnsureReady(
        IEnumerable<ProcurementWorksCloseoutCheckDto> checks)
    {
        if (!IsReady(checks))
            throw Conflict("WORKS_CLOSEOUT_BLOCKED",
                "The Works closeout action is blocked. Resolve every required source and evidence check.");
    }

    private static bool IsReady(
        IEnumerable<ProcurementWorksCloseoutCheckDto> checks) =>
        checks.All(item => item.Status is ProcurementWorksCloseoutCheckStatus.Passed
            or ProcurementWorksCloseoutCheckStatus.NotRequired);

    private static void Require(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Validation(code, message);
    }

    private static void EnsureRowVersion(
        byte[] current, string supplied, string code)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException)
        {
            throw Validation(code, "The row version is invalid.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict(code, "The record changed. Reload and retry.");
    }

    private static string Humanize(string key) =>
        string.Join(' ', key.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private static string NormalizeCorrelation(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value.Trim()[..Math.Min(value.Trim().Length, 100)];

    private string ActorName() =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;

    private void StampUpdate(ProcurementWorksCloseoutAction item, DateTime now)
    {
        item.UpdatedAt = now;
        item.UpdatedBy = ActorName();
        item.LastModifiedById = _currentUser.UserId;
    }

    private void StampContract(Contract item, DateTime now)
    {
        item.UpdatedAt = now;
        item.UpdatedBy = ActorName();
        item.LastModifiedById = _currentUser.UserId;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();

    private static string Serialize(object value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static ProcurementWorksCloseoutNotFoundException NotFound(
        string code, string message) => new(code, message);
    private static ProcurementWorksCloseoutConflictException Conflict(
        string code, string message) => new(code, message);
    private static ProcurementWorksCloseoutValidationException Validation(
        string code, string message) => new(code, message);
    private static ProcurementWorksCloseoutAuthorizationException Authorization(
        string message) => new(message);

    private sealed record Evaluation(
        ProcurementConfigurationProfileDto Profile,
        ProcurementAuthorityRouteDecisionDto Authority,
        IReadOnlyList<ProcurementWorksCloseoutCheckDto> Checks,
        string SourceSnapshot);

    private sealed record SourceState(
        Project Project,
        IReadOnlyList<ProjectHandoverItem> HandoverItems,
        IReadOnlyList<ProjectDefectLiabilityCase> Defects,
        IReadOnlyList<ProjectPaymentCertificate> Certificates,
        ProjectFinalAccount? FinalAccount,
        PerformanceBondRequest? PerformanceBond,
        ProjectClosure? ProjectClosure,
        int CompletedPracticalTakeovers,
        int CompletedFinalTakeovers,
        decimal RetentionHeld,
        decimal RetentionReleased,
        string Currency,
        IReadOnlyCollection<ProcurementWorksCloseoutAction> History);
}
