using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementContractActivationService :
    IProcurementContractActivationService
{
    private const string EventType = "ProcurementContractActivation";
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
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementComplianceDecisionService _compliance;
    private readonly IProcurementConfigurationService _configuration;
    private readonly IProcurementGhanepsExchangeService _ghaneps;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IProcurementContractActivationStore _store;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly IContractService _contracts;
    private readonly ILogger<ProcurementContractActivationService> _logger;

    public ProcurementContractActivationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IProcurementComplianceDecisionService compliance,
        IProcurementConfigurationService configuration,
        IProcurementGhanepsExchangeService ghaneps,
        IWorkflowIntegrationService workflow,
        IProcurementContractActivationStore store,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        IContractService contracts,
        ILogger<ProcurementContractActivationService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _access = access;
        _compliance = compliance;
        _configuration = configuration;
        _ghaneps = ghaneps;
        _workflow = workflow;
        _store = store;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _contracts = contracts;
        _logger = logger;
    }

    private IGenericRepository<Contract> ContractRows => _unitOfWork.Repository<Contract>();
    private IGenericRepository<TenderAward> Awards => _unitOfWork.Repository<TenderAward>();
    private IGenericRepository<TenderFee> TenderFees => _unitOfWork.Repository<TenderFee>();
    private IGenericRepository<PerformanceBondRequest> PerformanceBonds =>
        _unitOfWork.Repository<PerformanceBondRequest>();
    private IGenericRepository<ProcurementAwardReadinessDecision> ReadinessDecisions =>
        _unitOfWork.Repository<ProcurementAwardReadinessDecision>();
    private IGenericRepository<ProcurementContractActivation> Activations =>
        _unitOfWork.Repository<ProcurementContractActivation>();
    private IGenericRepository<ProcurementContractActivationEvidence> ActivationEvidence =>
        _unitOfWork.Repository<ProcurementContractActivationEvidence>();

    public async Task<ProcurementContractActivationOverviewDto> GetOverviewAsync(
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ReadPermission, contractId.ToString("N"),
            Guid.NewGuid().ToString("N"), cancellationToken);
        var contract = await LoadContractAsync(contractId, cancellationToken);
        var history = await ActivationQuery()
            .Where(item => item.ContractId == contractId)
            .AsNoTracking()
            .OrderByDescending(item => item.Sequence)
            .ToListAsync(cancellationToken);

        Evaluation? evaluation = null;
        ProcurementContractActivationCheckDto? evaluationFailure = null;
        try
        {
            evaluation = await EvaluateAsync(contract, history.FirstOrDefault()?.Evidence,
                Guid.NewGuid().ToString("N"), cancellationToken);
        }
        catch (Exception exception) when (
            exception is not ProcurementContractActivationAuthorizationException)
        {
            evaluationFailure = Failed("configuration", "Configured activation controls",
                "CONTRACT_ACTIVATION_EVALUATION_FAILED", exception.Message);
        }

        var checks = evaluation?.Checks ??
            new List<ProcurementContractActivationCheckDto> { evaluationFailure! };
        var active = history.FirstOrDefault(item =>
            !ProcurementContractActivationRules.IsTerminal(item.Status));
        var isReady = checks.All(item =>
            item.Status is ProcurementContractActivationCheckStatus.Passed
                or ProcurementContractActivationCheckStatus.NotRequired);
        return new ProcurementContractActivationOverviewDto
        {
            ContractId = contract.Id,
            ContractNumber = contract.ContractNumber,
            ContractStatus = contract.Status,
            IsReady = isReady,
            CanSubmit = ProcurementContractActivationRules.CanSubmitContract(contract.Status) &&
                        active is null,
            CanDecide = active is not null &&
                        ProcurementContractActivationRules.CanDecide(active.Status),
            CanActivate = active is not null &&
                          ProcurementContractActivationRules.CanActivate(active.Status),
            RequiredEvidenceKeys = evaluation is null
                ? Array.Empty<string>()
                : evaluation.RequiredEvidence.Select(item => item.Key).ToList(),
            DecisionKeys = DecisionKeys,
            Checks = checks,
            History = history.Select(Map).ToList()
        };
    }

    public async Task<ProcurementContractActivationDto> SubmitAsync(
        Guid contractId,
        SubmitProcurementContractActivationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.Reason, "CONTRACT_ACTIVATION_REASON_REQUIRED",
            "A documented activation reason is required.");
        Require(request.IdempotencyKey, "CONTRACT_ACTIVATION_IDEMPOTENCY_REQUIRED",
            "An idempotency key is required.");
        var contract = await LoadContractAsync(contractId, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, contract.ContractNumber,
            correlation, cancellationToken);
        EnsureRowVersion(contract.RowVersion, request.ContractRowVersion,
            "CONTRACT_ACTIVATION_CONTRACT_STALE");

        var existing = await ActivationQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.IdempotencyKey == request.IdempotencyKey.Trim(),
                cancellationToken);
        if (existing is not null)
        {
            if (existing.ContractId != contractId)
                throw Conflict("CONTRACT_ACTIVATION_IDEMPOTENCY_CONFLICT",
                    "The idempotency key belongs to another contract.");
            return Map(existing);
        }
        if (!ProcurementContractActivationRules.CanSubmitContract(contract.Status))
            throw Conflict("CONTRACT_ACTIVATION_STATUS_INVALID",
                $"Contract {contract.ContractNumber} cannot enter activation from {contract.Status}.");
        if (await Activations.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ContractId == contractId && !item.IsDeleted &&
                item.Status != ProcurementContractActivationStatus.Rejected &&
                item.Status != ProcurementContractActivationStatus.Cancelled &&
                item.Status != ProcurementContractActivationStatus.Activated)
            .AnyAsync(cancellationToken))
            throw Conflict("CONTRACT_ACTIVATION_OPEN_REQUEST",
                "The contract already has an open activation request.");

        ProcurementContractActivation? created = null;
        _unitOfWork.ClearTrackedChanges();
        await ExecuteAsync(async () =>
        {
            var concurrent = await ActivationQuery()
                .SingleOrDefaultAsync(item =>
                    item.IdempotencyKey == request.IdempotencyKey.Trim(),
                    cancellationToken);
            if (concurrent is not null)
            {
                if (concurrent.ContractId != contractId)
                    throw Conflict("CONTRACT_ACTIVATION_IDEMPOTENCY_CONFLICT",
                        "The idempotency key belongs to another contract.");
                created = concurrent;
                return;
            }
            contract = await LoadContractAsync(contractId, cancellationToken);
            EnsureRowVersion(contract.RowVersion, request.ContractRowVersion,
                "CONTRACT_ACTIVATION_CONTRACT_STALE");
            if (!ProcurementContractActivationRules.CanSubmitContract(contract.Status))
                throw Conflict("CONTRACT_ACTIVATION_STATUS_INVALID",
                    $"Contract {contract.ContractNumber} cannot enter activation from {contract.Status}.");
            if (await Activations.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.ContractId == contractId && !item.IsDeleted &&
                    item.Status != ProcurementContractActivationStatus.Rejected &&
                    item.Status != ProcurementContractActivationStatus.Cancelled &&
                    item.Status != ProcurementContractActivationStatus.Activated)
                .AnyAsync(cancellationToken))
                throw Conflict("CONTRACT_ACTIVATION_OPEN_REQUEST",
                    "The contract already has an open activation request.");

            var evidence = await ValidateEvidenceAsync(
                contract, request.Evidence, cancellationToken);
            var evaluation = await EvaluateAsync(
                contract, evidence, correlation, cancellationToken);
            EnsureReady(evaluation);
            foreach (var item in evidence)
            {
                var requirement = evaluation.RequiredEvidence
                    .FirstOrDefault(candidate => string.Equals(
                        candidate.Key, item.RequirementKey,
                        StringComparison.OrdinalIgnoreCase));
                if (requirement is null)
                    throw Validation(
                        "CONTRACT_ACTIVATION_EVIDENCE_KEY_UNKNOWN",
                        $"Evidence requirement '{item.RequirementKey}' is not configured for this contract.");
                item.RequirementLabel = requirement.Label;
            }

            var sequence = (await Activations.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.ContractId == contractId && !item.IsDeleted)
                .Select(item => (int?)item.Sequence)
                .MaxAsync(cancellationToken) ?? 0) + 1;
            var now = DateTime.UtcNow;
            created = new ProcurementContractActivation
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                ContractId = contract.Id,
                Sequence = sequence,
                Status = ProcurementContractActivationStatus.PendingApproval,
                ConfigurationProfileId = evaluation.Profile.Id,
                ConfigurationProfileVersion = evaluation.Profile.Version,
                PolicySetId = evaluation.Authority.Policy!.PolicySetId,
                PolicyVersion = evaluation.Authority.Policy.Version,
                AuthorityRuleId = evaluation.Authority.Steps.First().RuleId,
                AuthorityName = evaluation.Authority.Steps.First().AuthorityName,
                WorkflowDefinitionId = evaluation.Authority.Workflow!.WorkflowDefinitionId,
                AwardReadinessDecisionId = evaluation.Readiness.Id,
                AwardReadinessSequence = evaluation.Readiness.DecisionSequence,
                AwardReadinessIntegrityHash = evaluation.Readiness.IntegrityHash,
                GhanepsConfigurationDecisionId = evaluation.Ghaneps.ConfigurationDecisionId,
                GhanepsConfigurationValueHash = evaluation.Ghaneps.ConfigurationValueHash,
                GhanepsRequired = evaluation.Ghaneps.HasApplicableMapping,
                GhanepsCompliant = evaluation.Ghaneps.IsCompliant,
                PerformanceSecurityRequired = evaluation.PerformanceRequired,
                PerformanceBondRequestId = evaluation.PerformanceBond?.Id,
                SubmittedById = _currentUser.UserId,
                SubmittedByName = ActorName(),
                SubmittedAtUtc = now,
                Reason = request.Reason.Trim(),
                IdempotencyKey = request.IdempotencyKey.Trim(),
                CorrelationId = correlation,
                ContractSnapshotJson = evaluation.ContractSnapshot,
                ContractSnapshotHash = Hash(evaluation.ContractSnapshot),
                ReadinessSnapshotJson = Serialize(evaluation.Checks),
                CreatedAt = now,
                CreatedBy = ActorName(),
                CreatedById = _currentUser.UserId
            };
            created.IntegrityHash = ActivationHash(created);
            await Activations.AddAsync(created);
            foreach (var item in evidence)
            {
                item.ActivationId = created.Id;
                item.TenantId = created.TenantId;
                item.CreatedAt = now;
                item.CreatedBy = ActorName();
                item.CreatedById = _currentUser.UserId;
                await ActivationEvidence.AddAsync(item);
            }
            contract.Status = "PendingSignature";
            contract.UpdatedAt = now;
            contract.UpdatedBy = ActorName();
            contract.LastModifiedById = _currentUser.UserId;
            await ContractRows.UpdateAsync(contract);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var workflow = await _workflow.SubmitAsync(
                WorkflowEntityType, created.Id, created.WorkflowDefinitionId);
            if (!workflow.ExecutionResult.Success)
                throw Conflict("CONTRACT_ACTIVATION_WORKFLOW_START_FAILED",
                    workflow.ExecutionResult.Message ??
                    "The configured contract workflow could not be started.");
            created.WorkflowInstanceId = workflow.ExecutionResult.WorkflowInstanceId;
            created.IntegrityHash = ActivationHash(created);
            await Activations.UpdateAsync(created);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(created, "ActivationSubmitted",
                ProcurementControlEventResult.ReviewRequired, null,
                new { created.Status, created.WorkflowInstanceId },
                created.Reason, correlation, evaluation.Checks,
                cancellationToken);
        }, cancellationToken);
        await PublishAsync("procurement.contract.activation-submitted", created!,
            cancellationToken);
        return Map(await LoadActivationAsync(created!.Id, cancellationToken));
    }

    public async Task<ProcurementContractActivationDto> DecideAsync(
        Guid activationId,
        DecideProcurementContractActivationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.Comment, "CONTRACT_ACTIVATION_DECISION_COMMENT_REQUIRED",
            "A decision comment is required.");
        var activation = await LoadActivationAsync(activationId, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, activation.Contract.ContractNumber,
            correlation, cancellationToken);
        EnsureRowVersion(activation.RowVersion, request.RowVersion,
            "CONTRACT_ACTIVATION_STALE");
        if (!ProcurementContractActivationRules.CanDecide(activation.Status))
            throw Conflict("CONTRACT_ACTIVATION_DECISION_NOT_ALLOWED",
                "Only a pending contract activation may be decided.");
        if (request.Approved &&
            !ProcurementContractActivationRules.IsIndependent(
                _currentUser.UserId, activation.SubmittedById,
                activation.Contract.CreatedById))
            throw Authorization(
                "The contract creator or activation submitter cannot positively decide this activation.");
        if (!activation.WorkflowInstanceId.HasValue)
            throw Conflict("CONTRACT_ACTIVATION_WORKFLOW_MISSING",
                "The activation is not bound to a workflow instance.");

        var result = await _workflow.ProcessApprovalAsync(
            WorkflowEntityType, activation.Id, _currentUser.UserId,
            request.Approved ? "Approve" : "Reject", request.Comment.Trim());
        if (!result.ExecutionResult.Success)
            throw Conflict("CONTRACT_ACTIVATION_WORKFLOW_DECISION_FAILED",
                result.ExecutionResult.Message ??
                "The contract workflow decision failed.");
        if (request.Approved && result.Outcome == WorkflowOutcome.Rejected)
            throw Conflict("CONTRACT_ACTIVATION_WORKFLOW_REJECTED",
                "The shared workflow rejected the activation.");
        if (!request.Approved && result.Outcome == WorkflowOutcome.Approved)
            throw Conflict("CONTRACT_ACTIVATION_WORKFLOW_APPROVED",
                "An approved shared workflow cannot be recorded as rejected.");
        if (result.Outcome == WorkflowOutcome.Recalled)
            throw Conflict("CONTRACT_ACTIVATION_WORKFLOW_RECALLED",
                "The shared workflow was recalled.");

        var now = DateTime.UtcNow;
        if (result.Outcome == WorkflowOutcome.Pending)
        {
            activation.DecisionComment = request.Comment.Trim();
            activation.UpdatedAt = now;
            activation.UpdatedBy = ActorName();
            activation.LastModifiedById = _currentUser.UserId;
            activation.IntegrityHash = ActivationHash(activation);
            await Activations.UpdateAsync(activation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(activation, "ActivationApprovalProgressed",
                ProcurementControlEventResult.ReviewRequired, null,
                new { activation.Status, result.Outcome }, request.Comment,
                correlation, null, cancellationToken);
            return Map(await LoadActivationAsync(activation.Id, cancellationToken));
        }

        activation.DecidedAtUtc = now;
        activation.DecidedById = _currentUser.UserId;
        activation.DecidedByName = ActorName();
        activation.DecisionComment = request.Comment.Trim();
        if (result.Outcome == WorkflowOutcome.Rejected)
        {
            activation.Status = ProcurementContractActivationStatus.Rejected;
            activation.Contract.Status = "Draft";
            activation.Contract.UpdatedAt = now;
            activation.Contract.UpdatedBy = ActorName();
            activation.Contract.LastModifiedById = _currentUser.UserId;
            activation.IntegrityHash = ActivationHash(activation);
            await Activations.UpdateAsync(activation);
            await ContractRows.UpdateAsync(activation.Contract);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(activation, "ActivationRejected",
                ProcurementControlEventResult.Rejected, null,
                new { activation.Status, ContractStatus = activation.Contract.Status },
                request.Comment, correlation, null, cancellationToken);
            await PublishAsync("procurement.contract.activation-rejected", activation,
                cancellationToken);
            return Map(await LoadActivationAsync(activation.Id, cancellationToken));
        }

        var evaluation = await EvaluateAsync(
            activation.Contract, activation.Evidence, correlation, cancellationToken);
        var signatureCheck = await EvaluateSignatureAsync(
            activation, evaluation.Signature, cancellationToken);
        evaluation.Checks.Add(signatureCheck);
        if (!IsReady(evaluation.Checks))
        {
            activation.Status = ProcurementContractActivationStatus.RevalidationFailed;
            activation.ReadinessSnapshotJson = Serialize(evaluation.Checks);
            activation.IntegrityHash = ActivationHash(activation);
            await Activations.UpdateAsync(activation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(activation, "ActivationRevalidationFailed",
                ProcurementControlEventResult.Denied, null,
                new { activation.Status, evaluation.Checks }, request.Comment,
                correlation, evaluation.Checks, cancellationToken);
            return Map(await LoadActivationAsync(activation.Id, cancellationToken));
        }

        activation.Status = ProcurementContractActivationStatus.Approved;
        activation.ReadinessSnapshotJson = Serialize(evaluation.Checks);
        activation.IntegrityHash = ActivationHash(activation);
        await Activations.UpdateAsync(activation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(activation, "ActivationApproved",
            ProcurementControlEventResult.Allowed, null,
            new { activation.Status, activation.DecidedById },
            request.Comment, correlation, evaluation.Checks, cancellationToken);
        await PublishAsync("procurement.contract.activation-approved", activation,
            cancellationToken);
        return Map(await LoadActivationAsync(activation.Id, cancellationToken));
    }

    public async Task<ContractDto> ActivateAsync(
        Guid activationId,
        ActivateProcurementContractRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.ContractorSignatoryName,
            "CONTRACT_ACTIVATION_CONTRACTOR_SIGNATORY_REQUIRED",
            "The contractor signatory name is required.");
        Require(request.Comment, "CONTRACT_ACTIVATION_COMMENT_REQUIRED",
            "An activation comment is required.");
        var activation = await LoadActivationAsync(activationId, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, activation.Contract.ContractNumber,
            correlation, cancellationToken);
        EnsureRowVersion(activation.RowVersion, request.RowVersion,
            "CONTRACT_ACTIVATION_STALE");
        if (!ProcurementContractActivationRules.CanActivate(activation.Status))
            throw Conflict("CONTRACT_ACTIVATION_NOT_APPROVED",
                "Only an approved or recoverable revalidation-failed activation can activate the contract.");

        var revalidationFailed = false;
        _unitOfWork.ClearTrackedChanges();
        await ExecuteAsync(async () =>
        {
            activation = await LoadActivationAsync(activationId, cancellationToken);
            EnsureRowVersion(activation.RowVersion, request.RowVersion,
                "CONTRACT_ACTIVATION_STALE");
            if (!ProcurementContractActivationRules.CanActivate(activation.Status))
                throw Conflict("CONTRACT_ACTIVATION_NOT_APPROVED",
                    "The activation is no longer eligible to apply.");
            var evaluation = await EvaluateAsync(
                activation.Contract, activation.Evidence, correlation,
                cancellationToken);
            evaluation.Checks.Add(await EvaluateSignatureAsync(
                activation, evaluation.Signature, cancellationToken));
            if (!IsReady(evaluation.Checks))
            {
                activation.Status =
                    ProcurementContractActivationStatus.RevalidationFailed;
                activation.ReadinessSnapshotJson = Serialize(evaluation.Checks);
                activation.IntegrityHash = ActivationHash(activation);
                await Activations.UpdateAsync(activation);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync(activation,
                    "ActivationRevalidationFailed",
                    ProcurementControlEventResult.Denied, null,
                    new { activation.Status, evaluation.Checks },
                    request.Comment, correlation, evaluation.Checks,
                    cancellationToken);
                revalidationFailed = true;
                return;
            }

            var now = DateTime.UtcNow;
            await _store.SetMutationContextAsync(activation.Id, cancellationToken);
            try
            {
                activation.Contract.Status = "Active";
                activation.Contract.ActivatedAt = now;
                activation.Contract.SignedDate = now;
                activation.Contract.SignedById = _currentUser.UserId;
                activation.Contract.SignedByName = ActorName();
                activation.Contract.ContractorSignatoryName =
                    request.ContractorSignatoryName.Trim();
                activation.Contract.ContractorSignedDate =
                    request.ContractorSignedAtUtc.HasValue
                        ? EnsureUtc(request.ContractorSignedAtUtc.Value)
                        : now;
                activation.Contract.UpdatedAt = now;
                activation.Contract.UpdatedBy = ActorName();
                activation.Contract.LastModifiedById = _currentUser.UserId;
                activation.Status = ProcurementContractActivationStatus.Activated;
                activation.ActivatedAtUtc = now;
                activation.ActivatedById = _currentUser.UserId;
                activation.ActivatedByName = ActorName();
                activation.DecisionComment = request.Comment.Trim();
                activation.ReadinessSnapshotJson = Serialize(evaluation.Checks);
                activation.IntegrityHash = ActivationHash(activation);
                var award = await Awards.GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == activation.Contract.TenderAwardId &&
                        !item.IsDeleted)
                    .SingleAsync(cancellationToken);
                award.Status = "ContractSigned";
                award.UpdatedAt = now;
                award.UpdatedBy = ActorName();
                award.LastModifiedById = _currentUser.UserId;
                await ContractRows.UpdateAsync(activation.Contract);
                await Awards.UpdateAsync(award);
                await Activations.UpdateAsync(activation);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync(activation, "ContractActivated",
                    ProcurementControlEventResult.Allowed,
                    new { Status = "PendingSignature" },
                    new { Status = "Active", activation.ActivatedAtUtc },
                    request.Comment, correlation, evaluation.Checks,
                    cancellationToken);
            }
            finally
            {
                await _store.ClearMutationContextAsync(cancellationToken);
            }
        }, cancellationToken);
        if (revalidationFailed)
            throw Conflict("CONTRACT_ACTIVATION_REVALIDATION_FAILED",
                "The contract activation controls changed or remain incomplete. Review the readiness checks.");
        await PublishAsync("procurement.contract.activated", activation,
            cancellationToken);
        return await _contracts.GetByIdAsync(activation.ContractId)
               ?? throw NotFound("CONTRACT_NOT_FOUND",
                   "The activated contract was not found in the current tenant.");
    }

    private async Task<Evaluation> EvaluateAsync(
        Contract contract,
        IEnumerable<ProcurementContractActivationEvidence>? candidateEvidence,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var checks = new List<ProcurementContractActivationCheckDto>();
        var profile = await _configuration.GetEffectiveProfileAsync(
            "TDC-PROCUREMENT", DateTime.UtcNow, cancellationToken)
            ?? throw Validation("CONTRACT_ACTIVATION_CONFIGURATION_MISSING",
                "No effective Published TDC procurement configuration profile exists.");
        if (profile.Decisions.Count != 14 || profile.Decisions.Any(item => !item.IsComplete))
            throw Validation("CONTRACT_ACTIVATION_CONFIGURATION_INCOMPLETE",
                "The effective configuration must contain fourteen complete approved decisions.");
        checks.Add(Passed("configuration", "Configuration profile",
            "CONTRACT_ACTIVATION_CONFIGURATION_READY",
            $"{profile.ProfileCode} v{profile.Version}", profile.Id,
            $"{profile.ProfileCode}:v{profile.Version}"));

        var award = await Awards.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == contract.TenderAwardId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("CONTRACT_ACTIVATION_AWARD_MISSING",
                "The contract award was not found in the current tenant.");
        var awardValid = award.TenderId == contract.TenderId &&
                         award.BusinessPartnerId == contract.BusinessPartnerId &&
                         (!contract.TenderBidId.HasValue ||
                          award.TenderBidId == contract.TenderBidId.Value) &&
                         !string.Equals(award.Status, "Cancelled",
                             StringComparison.OrdinalIgnoreCase) &&
                         ProcurementContractActivationRules.AmountMatchesAward(
                             contract.ContractValue, award.AwardedAmount) &&
                         string.Equals(contract.Currency, award.Currency,
                             StringComparison.OrdinalIgnoreCase);
        checks.Add(awardValid
            ? Passed("award", "Exact approved award",
                "CONTRACT_ACTIVATION_AWARD_READY",
                "Contract supplier, bid, amount, currency, and tender match the active award.",
                award.Id, $"award:{award.Id:N}")
            : Failed("award", "Exact approved award",
                "CONTRACT_ACTIVATION_AWARD_MISMATCH",
                "Contract supplier, bid, amount, currency, tender, or award status differs from the approved award."));

        checks.Add(await EvaluateBudgetCommitmentAsync(
            contract, cancellationToken));

        checks.Add(await EvaluateQuantitySurveyCommercialTermsAsync(contract, cancellationToken));

        var readiness = await ReadinessDecisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceType == ProcurementAwardReadinessSourceType.Tender &&
                item.SourceId == contract.TenderId && !item.IsDeleted)
            .AsNoTracking()
            .OrderByDescending(item => item.DecisionSequence)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw Validation("CONTRACT_ACTIVATION_READINESS_MISSING",
                "No award-readiness decision exists for the contract tender.");
        var recommendedSuppliers = DeserializeIds(
            readiness.RecommendedBusinessPartnerIdsJson);
        var readinessValid =
            readiness.Status == ProcurementAwardReadinessDecisionStatus.Ready &&
            recommendedSuppliers.Contains(contract.BusinessPartnerId);
        checks.Add(readinessValid
            ? Passed("readiness", "Award readiness",
                "CONTRACT_ACTIVATION_READINESS_READY",
                $"Ready decision #{readiness.DecisionSequence} names the contract supplier.",
                readiness.Id, readiness.IntegrityHash)
            : Failed("readiness", "Award readiness",
                "CONTRACT_ACTIVATION_READINESS_BLOCKED",
                "The latest award-readiness decision is blocked or does not name the contract supplier."));

        var category = ContractCategory(contract.ContractType);
        var authority = await _compliance.EvaluateAuthorityRouteAsync(
            new ProcurementAuthorityRouteDecisionRequest
            {
                Category = category,
                Amount = contract.ContractValue,
                CurrencyCode = contract.Currency.ToUpperInvariant(),
                AtUtc = DateTime.UtcNow,
                SourceType = SourceType,
                SourceReference = contract.ContractNumber
            }, correlationId, cancellationToken);
        var authorityReady = authority.IsReady && authority.Policy is not null &&
                             authority.Workflow is not null &&
                             authority.Steps.Count > 0;
        checks.Add(authorityReady
            ? Passed("authority", "Statutory authority and workflow",
                authority.DecisionCode, authority.Message,
                authority.Steps.First().RuleId,
                $"{authority.Steps.First().AuthorityName} / {authority.Workflow!.Name} v{authority.Workflow.Version}")
            : Failed("authority", "Statutory authority and workflow",
                authority.DecisionCode, authority.Message));
        if (!authorityReady)
            throw Validation("CONTRACT_ACTIVATION_AUTHORITY_BLOCKED",
                authority.Message);

        var ghaneps = await _ghaneps.GetAwardComplianceAsync(
            ProcurementGhanepsSourceType.Tender, contract.TenderId,
            cancellationToken);
        checks.Add(ghaneps.IsCompliant
            ? Passed("ghaneps", "GHANEPS award exchange", ghaneps.Code,
                ghaneps.Message, ghaneps.ConfigurationDecisionId,
                ghaneps.ConfigurationValueHash)
            : Failed("ghaneps", "GHANEPS award exchange", ghaneps.Code,
                ghaneps.Message));

        var mandatoryPerformanceFee = await TenderFees.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderId == contract.TenderId && !item.IsDeleted &&
                item.IsMandatory && item.FeeType == "PerformanceBond")
            .AsNoTracking().AnyAsync(cancellationToken);
        var performanceBond = await PerformanceBonds.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderAwardId == contract.TenderAwardId && !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var performanceRequired = mandatoryPerformanceFee || performanceBond is not null;
        var performanceReady = !performanceRequired ||
                               performanceBond is not null &&
                               string.Equals(performanceBond.Status, "Approved",
                                   StringComparison.OrdinalIgnoreCase);
        checks.Add(!performanceRequired
            ? NotRequired("performance-security", "Performance security",
                "CONTRACT_ACTIVATION_SECURITY_NOT_REQUIRED",
                "The tender configuration does not require performance security.")
            : performanceReady
                ? Passed("performance-security", "Performance security",
                    "CONTRACT_ACTIVATION_SECURITY_APPROVED",
                    "The award performance-security request is approved.",
                    performanceBond!.Id, $"performance-bond:{performanceBond.Id:N}")
                : Failed("performance-security", "Performance security",
                    "CONTRACT_ACTIVATION_SECURITY_BLOCKED",
                    "Configured performance security must be submitted and approved."));

        var stage = DeserializeDecision<ProcurementAuthorityStageDecisionValueDto>(
            profile, "DEC-004");
        var signature = DeserializeDecision<ProcurementSignatureDecisionValueDto>(
            profile, "DEC-008");
        var requirements = RequiredEvidence(stage, signature, performanceRequired);
        var evidence = candidateEvidence?.ToList() ?? [];
        foreach (var requirement in requirements)
        {
            var match = evidence.FirstOrDefault(item =>
                string.Equals(item.RequirementKey, requirement.Key,
                    StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                checks.Add(Pending(requirement.Key, requirement.Label,
                    "CONTRACT_ACTIVATION_EVIDENCE_REQUIRED",
                    $"Verified evidence is required: {requirement.Label}."));
                continue;
            }

            var evidenceIsCurrent = await IsEvidenceCurrentAsync(
                contract, match, cancellationToken);
            checks.Add(evidenceIsCurrent
                ? Passed(requirement.Key, requirement.Label,
                    "CONTRACT_ACTIVATION_EVIDENCE_VERIFIED",
                    "Current, malware-clean evidence is linked.",
                    match.FileUploadRecordId ?? match.WorkflowEvidenceDocumentId,
                    match.EvidenceReference)
                : Failed(requirement.Key, requirement.Label,
                    "CONTRACT_ACTIVATION_EVIDENCE_STALE",
                    "The linked evidence is no longer current, tenant-safe, or malware-clean."));
        }

        var snapshot = Serialize(new
        {
            contract.Id,
            contract.TenantId,
            contract.ContractNumber,
            contract.ContractType,
            contract.TenderAwardId,
            contract.TenderId,
            contract.BusinessPartnerId,
            contract.TenderBidId,
            contract.ContractValue,
            Currency = contract.Currency.ToUpperInvariant(),
            contract.PaymentTermId,
            contract.ProvisionalSumAmount,
            contract.ContingencyAmount,
            contract.RetentionPercentage,
            contract.DefectsLiabilityDays,
            contract.RetentionClause,
            contract.AllowSectionalTakeover,
            contract.SectionalTakeoverClause,
            contract.AllowSubcontracting,
            contract.SubcontractPaymentTermId,
            contract.SubcontractTerms,
            contract.ClaimNoticePeriodDays,
            contract.ClaimClause,
            contract.CommercialTermsContractDocumentId,
            contract.CommercialTermsPolicyHash,
            contract.StartDate,
            contract.EndDate,
            contract.ScopeOfWork,
            contract.Deliverables,
            contract.SpecialConditions,
            contract.PenaltyClause,
            contract.UpdatedAt
        });
        return new Evaluation(profile, award, readiness, authority, ghaneps,
            performanceRequired, performanceBond, stage, signature,
            requirements, checks, snapshot);
    }

    private async Task<ProcurementContractActivationCheckDto> EvaluateQuantitySurveyCommercialTermsAsync(
        Contract contract, CancellationToken cancellationToken)
    {
        if (!string.Equals(contract.ContractType, "Works", StringComparison.OrdinalIgnoreCase))
            return NotRequired("qs-commercial-terms", "QS commercial terms",
                "CONTRACT_ACTIVATION_QS_TERMS_NOT_REQUIRED",
                "QS commercial-term controls apply only to Works contracts.");
        try
        {
            var now = DateTime.UtcNow;
            var profile = await _unitOfWork.Repository<QuantitySurveyConfigurationProfile>()
                .GetQueryable(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                                       value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published &&
                                       value.PublishedAt != null && value.EffectiveFrom <= now &&
                                       (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
                .AsNoTracking().OrderByDescending(value => value.IsDefault)
                .ThenByDescending(value => value.Version).FirstOrDefaultAsync(cancellationToken);
            if (profile is null)
                return Failed("qs-commercial-terms", "QS commercial terms",
                    "CONTRACT_ACTIVATION_QS_CONFIGURATION_MISSING",
                    "No Published QS configuration is effective for this Works contract.");
            var decisions = await _unitOfWork.Repository<QuantitySurveyConfigurationDecision>()
                .GetQueryable(value => value.TenantId == _currentUser.TenantId && value.ProfileId == profile.Id &&
                                       !value.IsDeleted && (value.DecisionKey == "QS-DEC-009" || value.DecisionKey == "QS-DEC-012") &&
                                       value.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
                                       value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
                                       value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified &&
                                       (!value.EffectiveFrom.HasValue || value.EffectiveFrom <= now) &&
                                       (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
                .AsNoTracking().ToListAsync(cancellationToken);
            var retentionDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-009");
            var controlsDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-012");
            if (retentionDecision is null || controlsDecision is null)
                return Failed("qs-commercial-terms", "QS commercial terms",
                    "CONTRACT_ACTIVATION_QS_DECISIONS_MISSING",
                    "Approved and evidence-verified QS-DEC-009 and QS-DEC-012 decisions are required.");
            var retention = JsonSerializer.Deserialize<QsRetentionValue>(retentionDecision.ValueJson, JsonOptions);
            var controls = JsonSerializer.Deserialize<QsContractControlsValue>(controlsDecision.ValueJson, JsonOptions);
            if (retention is null || controls is null)
                return Failed("qs-commercial-terms", "QS commercial terms",
                    "CONTRACT_ACTIVATION_QS_POLICY_INVALID",
                    "The effective QS commercial-terms decisions are invalid.");
            var paymentTermReady = contract.PaymentTermId.HasValue &&
                await _unitOfWork.Repository<PaymentTerm>().GetQueryable(value =>
                        value.TenantId == _currentUser.TenantId && value.Id == contract.PaymentTermId.Value &&
                        !value.IsDeleted && value.IsActive &&
                        (value.ApplicableTo == "All" || value.ApplicableTo == "Supplier" || value.ApplicableTo == "Contractor"))
                    .AsNoTracking().AnyAsync(cancellationToken);
            var subcontractPaymentTermReady = contract.SubcontractPaymentTermId.HasValue &&
                await _unitOfWork.Repository<PaymentTerm>().GetQueryable(value =>
                        value.TenantId == _currentUser.TenantId && value.Id == contract.SubcontractPaymentTermId.Value &&
                        !value.IsDeleted && value.IsActive &&
                        (value.ApplicableTo == "All" || value.ApplicableTo == "Supplier" || value.ApplicableTo == "Contractor"))
                    .AsNoTracking().AnyAsync(cancellationToken);
            var documentReady = contract.CommercialTermsContractDocumentId.HasValue &&
                await _unitOfWork.Repository<ContractDocument>().GetQueryable(value =>
                        value.TenantId == _currentUser.TenantId && value.ContractId == contract.Id &&
                        value.Id == contract.CommercialTermsContractDocumentId.Value && !value.IsDeleted &&
                        value.FileUploadRecordId.HasValue && value.CentralDocumentRecordId.HasValue &&
                        value.CentralDocumentVersionId.HasValue && value.FileUploadRecord != null &&
                        value.FileUploadRecord.TenantId == _currentUser.TenantId && !value.FileUploadRecord.IsDeleted &&
                        value.FileUploadRecord.VirusScanStatus == FileVirusScanStatus.Clean &&
                        value.CentralDocumentRecord != null && value.CentralDocumentRecord.TenantId == _currentUser.TenantId &&
                        !value.CentralDocumentRecord.IsDeleted && value.CentralDocumentRecord.LifecycleStatus == "Active" &&
                        value.CentralDocumentVersion != null && value.CentralDocumentVersion.TenantId == _currentUser.TenantId &&
                        !value.CentralDocumentVersion.IsDeleted && value.CentralDocumentVersion.DocumentRecordId == value.CentralDocumentRecordId &&
                        value.CentralDocumentVersion.FileUploadRecordId == value.FileUploadRecordId &&
                        value.CentralDocumentVersion.Status == "Published" &&
                        value.CentralDocumentRecord.CurrentVersion == value.CentralDocumentVersion.VersionNumber)
                    .AsNoTracking().AnyAsync(cancellationToken);
            var issues = QuantitySurveyContractCommercialTermsRules.Validate(new(
                contract.ContractValue, contract.ProvisionalSumAmount, contract.ContingencyAmount,
                contract.RetentionPercentage, contract.DefectsLiabilityDays, contract.AllowSectionalTakeover,
                contract.AllowSubcontracting, subcontractPaymentTermReady, contract.ClaimNoticePeriodDays,
                paymentTermReady, documentReady, contract.RetentionClause, contract.SectionalTakeoverClause,
                contract.SubcontractTerms, contract.ClaimClause, retention.MaximumRetentionPercent,
                retention.DefectsLiabilityDays, retention.SectionalTakeoverReleasePercent,
                controls.ControlProvisionalSums, controls.ControlContingencies, controls.ControlDefectsLiability,
                controls.ControlSectionalTakeover, controls.ControlSubcontracts, controls.ControlClaimClauses,
                controls.RequireCommercialTermsDocument)).ToList();
            var policyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(new
            {
                profile.Id,
                RetentionDecisionId = retentionDecision.Id,
                RetentionValueJson = retentionDecision.ValueJson,
                ControlsDecisionId = controlsDecision.Id,
                ControlsValueJson = controlsDecision.ValueJson
            }))));
            if (contract.CommercialTermsConfigurationProfileId != profile.Id ||
                contract.ContractControlsDecisionId != controlsDecision.Id ||
                contract.RetentionDecisionId != retentionDecision.Id ||
                !string.Equals(contract.CommercialTermsPolicyHash, policyHash, StringComparison.Ordinal))
                issues.Add("The commercial terms were not configured under the current QS policy.");
            return issues.Count == 0
                ? Passed("qs-commercial-terms", "QS commercial terms",
                    "CONTRACT_ACTIVATION_QS_TERMS_READY",
                    "Canonical Works-contract commercial terms satisfy the effective QS retention, DMS and controlled-master policy.",
                    profile.Id, $"{profile.ProfileCode}:v{profile.Version}")
                : Failed("qs-commercial-terms", "QS commercial terms",
                    "CONTRACT_ACTIVATION_QS_TERMS_BLOCKED", string.Join(" ", issues));
        }
        catch (JsonException exception)
        {
            return Failed("qs-commercial-terms", "QS commercial terms",
                "CONTRACT_ACTIVATION_QS_POLICY_INVALID",
                $"The effective QS commercial-terms policy is invalid: {exception.Message}");
        }
    }

    private async Task<ProcurementContractActivationCheckDto> EvaluateBudgetCommitmentAsync(
        Contract contract,
        CancellationToken cancellationToken)
    {
        var tender = await _unitOfWork.Repository<Tender>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == contract.TenderId &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (tender is null || !tender.SourcePurchaseRequisitionId.HasValue ||
            !tender.SourcingReleaseId.HasValue)
        {
            return Failed("commitment", "Budget commitment",
                "CONTRACT_ACTIVATION_BUDGET_LINEAGE_MISSING",
                "The contract tender does not identify an approved requisition and sourcing release.");
        }

        var release = await _unitOfWork.Repository<ProcurementRequisitionSourcingRelease>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == tender.SourcingReleaseId.Value &&
                item.PurchaseRequisitionId == tender.SourcePurchaseRequisitionId.Value &&
                !item.IsDeleted)
            .Include(item => item.PurchaseRequisition)
            .Include(item => item.BudgetCommitment)
                .ThenInclude(item => item.ProcurementBudget)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (release?.BudgetCommitment?.ProcurementBudget is null)
        {
            return Failed("commitment", "Budget commitment",
                "CONTRACT_ACTIVATION_BUDGET_COMMITMENT_MISSING",
                "The contract sourcing release has no authoritative budget commitment.");
        }

        var commitment = release.BudgetCommitment;
        var budget = commitment.ProcurementBudget;
        var result = ProcurementPurchaseOrderComplianceRules.ValidateCommitment(
            new ProcurementCommitmentLifecycleSnapshot(
                _currentUser.TenantId,
                release.PurchaseRequisition.Id,
                release.PurchaseRequisition.TenantId,
                release.PurchaseRequisition.Currency,
                release.PurchaseRequisition.BudgetId,
                release.TenantId,
                release.PurchaseRequisitionId,
                release.BudgetCommitmentId,
                release.BudgetCommitmentReference,
                commitment.Id,
                commitment.TenantId,
                commitment.PurchaseRequisitionId,
                commitment.ProcurementBudgetId,
                commitment.ReservationReference,
                commitment.Status,
                commitment.ReservedAmount,
                commitment.Currency,
                budget.Id,
                budget.TenantId,
                budget.Status,
                budget.Currency,
                budget.CommittedAmount,
                budget.ApprovedById,
                budget.ApprovedDate,
                budget.EffectiveDate,
                budget.ExpiryDate,
                contract.ContractValue,
                contract.Currency,
                DateTime.UtcNow));
        return result.IsValid
            ? Passed("commitment", "Budget commitment", result.Code,
                result.Message, commitment.Id, commitment.ReservationReference)
            : Failed("commitment", "Budget commitment", result.Code,
                result.Message);
    }

    private async Task<bool> IsEvidenceCurrentAsync(
        Contract contract,
        ProcurementContractActivationEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (evidence.ReferenceKind ==
            ProcurementContractActivationEvidenceKind.WorkflowEvidenceDocument)
        {
            if (!evidence.WorkflowEvidenceDocumentId.HasValue)
                return false;
            return await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == evidence.WorkflowEvidenceDocumentId.Value &&
                    item.IsCurrent &&
                    item.VerificationStatus ==
                        WorkflowEvidenceVerificationStatus.Verified &&
                    item.MalwareScanStatus == WorkflowMalwareScanStatus.Clean &&
                    (!item.ExpiryDate.HasValue ||
                     item.ExpiryDate.Value >= DateTime.UtcNow) &&
                    !item.IsDeleted)
                .AsNoTracking()
                .AnyAsync(cancellationToken);
        }

        if (!evidence.FileUploadRecordId.HasValue)
            return false;
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
                item.FileUploadRecord.VirusScanStatus ==
                    FileVirusScanStatus.Clean &&
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

    private async Task<List<ProcurementContractActivationEvidence>> ValidateEvidenceAsync(
        Contract contract,
        IEnumerable<ProcurementContractActivationEvidenceRequest> requests,
        CancellationToken cancellationToken)
    {
        var rows = requests.ToList();
        if (rows.Count == 0)
            throw Validation("CONTRACT_ACTIVATION_EVIDENCE_REQUIRED",
                "Activation evidence is required.");
        if (rows.GroupBy(item => item.RequirementKey.Trim(),
                StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw Validation("CONTRACT_ACTIVATION_EVIDENCE_DUPLICATE",
                "Each activation evidence requirement can be linked only once.");

        var result = new List<ProcurementContractActivationEvidence>();
        foreach (var request in rows)
        {
            Require(request.RequirementKey,
                "CONTRACT_ACTIVATION_EVIDENCE_KEY_REQUIRED",
                "Every activation evidence item requires a requirement key.");
            Require(request.EvidenceReference,
                "CONTRACT_ACTIVATION_EVIDENCE_REFERENCE_REQUIRED",
                "Every activation evidence item requires an evidence reference.");
            Guid? workflowId = null;
            Guid? uploadId = null;
            string sourceHash;
            if (request.ReferenceKind ==
                ProcurementContractActivationEvidenceKind.WorkflowEvidenceDocument)
            {
                if (!request.WorkflowEvidenceDocumentId.HasValue ||
                    request.FileUploadRecordId.HasValue)
                    throw Validation("CONTRACT_ACTIVATION_EVIDENCE_REFERENCE_INVALID",
                        "Workflow evidence requires exactly one workflow evidence document ID.");
                var evidence = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == request.WorkflowEvidenceDocumentId.Value &&
                        !item.IsDeleted)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFound("CONTRACT_ACTIVATION_EVIDENCE_NOT_FOUND",
                        "Workflow evidence was not found in the current tenant.");
                if (!evidence.IsCurrent ||
                    evidence.VerificationStatus != WorkflowEvidenceVerificationStatus.Verified ||
                    evidence.MalwareScanStatus != WorkflowMalwareScanStatus.Clean)
                    throw Conflict("CONTRACT_ACTIVATION_EVIDENCE_NOT_VERIFIED",
                        "Workflow evidence must be current, verified, and malware-clean.");
                workflowId = evidence.Id;
                sourceHash = evidence.Sha256;
            }
            else
            {
                if (!request.FileUploadRecordId.HasValue ||
                    request.WorkflowEvidenceDocumentId.HasValue)
                    throw Validation("CONTRACT_ACTIVATION_UPLOAD_REFERENCE_INVALID",
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
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFound("CONTRACT_ACTIVATION_DMS_DOCUMENT_NOT_FOUND",
                        "The selected evidence is not a central-DMS document for this contract.");
                if (document.FileUploadRecord is null ||
                    document.FileUploadRecord.VirusScanStatus != FileVirusScanStatus.Clean)
                    throw Conflict("CONTRACT_ACTIVATION_DMS_DOCUMENT_UNSAFE",
                        "Contract evidence must have a completed Clean malware scan.");
                uploadId = document.FileUploadRecordId;
                sourceHash = Hash(Serialize(new
                {
                    document.FileUploadRecordId,
                    document.CentralDocumentRecordId,
                    document.CentralDocumentVersionId,
                    document.FileUploadRecord.FilePath,
                    document.FileUploadRecord.FileSize
                }));
            }
            result.Add(new ProcurementContractActivationEvidence
            {
                Id = Guid.NewGuid(),
                RequirementKey = request.RequirementKey.Trim(),
                RequirementLabel = request.RequirementKey.Trim(),
                ReferenceKind = request.ReferenceKind,
                WorkflowEvidenceDocumentId = workflowId,
                FileUploadRecordId = uploadId,
                EvidenceReference = request.EvidenceReference.Trim(),
                EvidenceHash = sourceHash
            });
        }
        return result;
    }

    private async Task<ProcurementContractActivationCheckDto> EvaluateSignatureAsync(
        ProcurementContractActivation activation,
        ProcurementSignatureDecisionValueDto signature,
        CancellationToken cancellationToken)
    {
        if (signature.SignatureMode is
            ProcurementSignatureMode.UploadedManualEvidence or
            ProcurementSignatureMode.ElectronicOrManualEvidence)
            return Passed("signature", "Configured contract signatures",
                "CONTRACT_ACTIVATION_MANUAL_SIGNATURE_VERIFIED",
                "The verified signed-contract DMS evidence satisfies the configured manual-signature mode.");
        if (!activation.WorkflowInstanceId.HasValue)
            return Failed("signature", "Configured contract signatures",
                "CONTRACT_ACTIVATION_SIGNATURE_WORKFLOW_MISSING",
                "The activation has no workflow instance for electronic signature evidence.");
        var approvalIds = _unitOfWork.Repository<WorkflowApproval>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.StepInstance.WorkflowInstanceId ==
                activation.WorkflowInstanceId.Value)
            .Select(item => item.Id);
        var committed = await _unitOfWork.Repository<WorkflowSignatureEvidence>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.IsCommitted && approvalIds.Contains(item.ApprovalId))
            .AsNoTracking().AnyAsync(cancellationToken);
        if (committed)
            return Passed("signature", "Configured contract signatures",
                "CONTRACT_ACTIVATION_SIGNATURE_VERIFIED",
                "A committed shared-workflow signature satisfies DEC-008.",
                activation.WorkflowInstanceId, $"workflow:{activation.WorkflowInstanceId:N}");
        return Failed("signature", "Configured contract signatures",
            "CONTRACT_ACTIVATION_SIGNATURE_MISSING",
            "DEC-008 requires a committed electronic workflow signature.");
    }

    private static IReadOnlyList<EvidenceRequirement> RequiredEvidence(
        ProcurementAuthorityStageDecisionValueDto stage,
        ProcurementSignatureDecisionValueDto signature,
        bool performanceRequired)
    {
        var result = new List<EvidenceRequirement>
        {
            new("legal-review", "Legal review evidence"),
            new("internal-audit-review", "Internal Audit review evidence"),
            new("authority-approval", "Statutory authority evidence"),
            new("signed-contract", "Signed contract document in the central DMS")
        };
        result.AddRange(stage.EvidenceRequirements.Select((label, index) =>
            new EvidenceRequirement($"dec-004-{index + 1:00}-{Slug(label)}",
                $"DEC-004: {label}")));
        result.AddRange(signature.EvidenceRequirements.Select((label, index) =>
            new EvidenceRequirement($"dec-008-{index + 1:00}-{Slug(label)}",
                $"DEC-008: {label}")));
        if (performanceRequired)
            result.Add(new EvidenceRequirement("performance-security",
                "Approved performance-security document"));
        return result.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).ToList();
    }

    private static string Slug(string value)
    {
        var normalized = new string(value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized)
            ? "evidence"
            : normalized[..Math.Min(normalized.Length, 80)];
    }

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

    private IQueryable<ProcurementContractActivation> ActivationQuery() =>
        Activations.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Contract)
            .Include(item => item.Evidence.Where(evidence => !evidence.IsDeleted));

    private async Task<Contract> LoadContractAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await ContractRows.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == id && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw NotFound("CONTRACT_NOT_FOUND",
            "The contract was not found in the current tenant.");

    private async Task<ProcurementContractActivation> LoadActivationAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await ActivationQuery().SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken)
        ?? throw NotFound("CONTRACT_ACTIVATION_NOT_FOUND",
            "The contract activation was not found in the current tenant.");

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
        ProcurementContractActivation activation,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        string correlationId,
        IReadOnlyList<ProcurementContractActivationCheckDto>? checks,
        CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "contract-activation", activation.TenantId, activation.Id,
                action.ToLowerInvariant(), correlationId),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = "CON-006",
            RuleVersion = "TDC-0407",
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = activation.ContractId,
            SourceReference = activation.Contract?.ContractNumber ??
                              activation.ContractId.ToString(),
            Reason = reason,
            InputValues = new
            {
                activation.ConfigurationProfileId,
                activation.ConfigurationProfileVersion,
                activation.PolicySetId,
                activation.PolicyVersion,
                activation.AuthorityRuleId,
                activation.WorkflowDefinitionId,
                activation.WorkflowInstanceId,
                activation.AwardReadinessDecisionId,
                activation.AwardReadinessSequence,
                activation.GhanepsRequired,
                activation.PerformanceSecurityRequired,
                activation.ContractSnapshotHash
            },
            ResultValues = new
            {
                activation.Status,
                activation.DecidedById,
                activation.ActivatedById,
                checks
            },
            Before = before,
            After = after,
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = activation.Evidence.Select(item =>
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
        ProcurementContractActivation activation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = activation.TenantId,
                TopicKey = topic,
                NotificationType = EventType,
                EntityType = SourceType,
                EntityId = activation.ContractId,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["activationId"] = activation.Id,
                    ["contractId"] = activation.ContractId,
                    ["contractNumber"] = activation.Contract?.ContractNumber ?? string.Empty,
                    ["status"] = activation.Status.ToString()
                },
                Metadata = new Dictionary<string, object>
                {
                    ["correlationId"] = activation.CorrelationId,
                    ["ruleCode"] = "CON-006",
                    ["ruleVersion"] = "TDC-0407"
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish contract activation notification {Topic} for {ActivationId}",
                topic, activation.Id);
        }
    }

    private static void EnsureReady(Evaluation evaluation)
    {
        if (!IsReady(evaluation.Checks))
            throw Conflict("CONTRACT_ACTIVATION_BLOCKED",
                "Contract activation is blocked. Resolve every required readiness and evidence check.");
    }

    private static bool IsReady(IEnumerable<ProcurementContractActivationCheckDto> checks) =>
        checks.All(item => item.Status is
            ProcurementContractActivationCheckStatus.Passed or
            ProcurementContractActivationCheckStatus.NotRequired);

    private static T DeserializeDecision<T>(
        ProcurementConfigurationProfileDto profile,
        string decisionKey)
    {
        try
        {
            var decision = profile.Decisions.Single(item =>
                item.DecisionKey.Equals(decisionKey, StringComparison.OrdinalIgnoreCase));
            return decision.Value.Deserialize<T>(JsonOptions)
                   ?? throw new JsonException($"{decisionKey} is empty.");
        }
        catch (Exception exception)
        {
            throw Validation("CONTRACT_ACTIVATION_DECISION_INVALID",
                $"{decisionKey} could not be applied: {exception.Message}");
        }
    }

    private static IReadOnlySet<Guid> DeserializeIds(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? [])
                .ToHashSet();
        }
        catch (JsonException)
        {
            return new HashSet<Guid>();
        }
    }

    private static ProcurementCategoryClass ContractCategory(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "works" => ProcurementCategoryClass.Works,
            "consultancy" or "consultancyservices" =>
                ProcurementCategoryClass.ConsultancyServices,
            "service" or "services" or "generalservices" =>
                ProcurementCategoryClass.GeneralServices,
            "technicalservice" or "technicalservices" =>
                ProcurementCategoryClass.TechnicalServices,
            _ => ProcurementCategoryClass.Goods
        };

    private static string ActivationHash(ProcurementContractActivation item) =>
        Hash(Serialize(new
        {
            schemaVersion = "tdc.contract-activation.v1",
            item.Id,
            item.TenantId,
            item.ContractId,
            item.Sequence,
            item.Status,
            item.ConfigurationProfileId,
            item.ConfigurationProfileVersion,
            item.PolicySetId,
            item.PolicyVersion,
            item.AuthorityRuleId,
            item.AuthorityName,
            item.WorkflowDefinitionId,
            item.WorkflowInstanceId,
            item.AwardReadinessDecisionId,
            item.AwardReadinessSequence,
            item.AwardReadinessIntegrityHash,
            item.GhanepsConfigurationDecisionId,
            item.GhanepsConfigurationValueHash,
            item.GhanepsRequired,
            item.GhanepsCompliant,
            item.PerformanceSecurityRequired,
            item.PerformanceBondRequestId,
            item.SubmittedById,
            item.SubmittedAtUtc,
            item.DecidedById,
            item.DecidedAtUtc,
            item.ActivatedById,
            item.ActivatedAtUtc,
            item.Reason,
            item.DecisionComment,
            item.IdempotencyKey,
            item.CorrelationId,
            item.ContractSnapshotHash
        }));

    private static ProcurementContractActivationDto Map(
        ProcurementContractActivation item) => new()
    {
        Id = item.Id,
        ContractId = item.ContractId,
        Sequence = item.Sequence,
        Status = item.Status,
        ConfigurationProfileId = item.ConfigurationProfileId,
        ConfigurationProfileVersion = item.ConfigurationProfileVersion,
        PolicySetId = item.PolicySetId,
        PolicyVersion = item.PolicyVersion,
        AuthorityRuleId = item.AuthorityRuleId,
        AuthorityName = item.AuthorityName,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        WorkflowInstanceId = item.WorkflowInstanceId,
        AwardReadinessDecisionId = item.AwardReadinessDecisionId,
        AwardReadinessSequence = item.AwardReadinessSequence,
        GhanepsRequired = item.GhanepsRequired,
        GhanepsCompliant = item.GhanepsCompliant,
        PerformanceSecurityRequired = item.PerformanceSecurityRequired,
        PerformanceBondRequestId = item.PerformanceBondRequestId,
        SubmittedById = item.SubmittedById,
        SubmittedByName = item.SubmittedByName,
        SubmittedAtUtc = item.SubmittedAtUtc,
        DecidedById = item.DecidedById,
        DecidedByName = item.DecidedByName,
        DecidedAtUtc = item.DecidedAtUtc,
        ActivatedById = item.ActivatedById,
        ActivatedByName = item.ActivatedByName,
        ActivatedAtUtc = item.ActivatedAtUtc,
        Reason = item.Reason,
        DecisionComment = item.DecisionComment,
        IntegrityHash = item.IntegrityHash,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        Evidence = item.Evidence.Where(evidence => !evidence.IsDeleted)
            .Select(evidence => new ProcurementContractActivationEvidenceDto
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

    private static ProcurementContractActivationCheckDto Passed(
        string key, string label, string code, string message,
        Guid? referenceId = null, string? reference = null) => new()
    {
        Key = key,
        Label = label,
        Status = ProcurementContractActivationCheckStatus.Passed,
        Code = code,
        Message = message,
        ReferenceId = referenceId,
        Reference = reference
    };

    private static ProcurementContractActivationCheckDto Failed(
        string key, string label, string code, string message) => new()
    {
        Key = key,
        Label = label,
        Status = ProcurementContractActivationCheckStatus.Failed,
        Code = code,
        Message = message
    };

    private static ProcurementContractActivationCheckDto Pending(
        string key, string label, string code, string message) => new()
    {
        Key = key,
        Label = label,
        Status = ProcurementContractActivationCheckStatus.Pending,
        Code = code,
        Message = message
    };

    private static ProcurementContractActivationCheckDto NotRequired(
        string key, string label, string code, string message) => new()
    {
        Key = key,
        Label = label,
        Status = ProcurementContractActivationCheckStatus.NotRequired,
        Code = code,
        Message = message,
        IsRequired = false
    };

    private static void Require(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Validation(code, message);
    }

    private static void EnsureRowVersion(byte[] current, string supplied, string code)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Validation(code, "The row version is invalid."); }
        if (!current.SequenceEqual(parsed))
            throw Conflict(code, "The record changed. Reload and retry.");
    }

    private static string NormalizeCorrelation(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value.Trim()[..Math.Min(value.Trim().Length, 100)];

    private string ActorName() =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;

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

    private static ProcurementContractActivationNotFoundException NotFound(
        string code, string message) => new(code, message);
    private static ProcurementContractActivationConflictException Conflict(
        string code, string message) => new(code, message);
    private static ProcurementContractActivationValidationException Validation(
        string code, string message) => new(code, message);
    private static ProcurementContractActivationAuthorizationException Authorization(
        string message) => new(message);

    private sealed record EvidenceRequirement(string Key, string Label);

    private sealed record Evaluation(
        ProcurementConfigurationProfileDto Profile,
        TenderAward Award,
        ProcurementAwardReadinessDecision Readiness,
        ProcurementAuthorityRouteDecisionDto Authority,
        ProcurementGhanepsComplianceDto Ghaneps,
        bool PerformanceRequired,
        PerformanceBondRequest? PerformanceBond,
        ProcurementAuthorityStageDecisionValueDto Stage,
        ProcurementSignatureDecisionValueDto Signature,
        IReadOnlyList<EvidenceRequirement> RequiredEvidence,
        List<ProcurementContractActivationCheckDto> Checks,
        string ContractSnapshot);
}
