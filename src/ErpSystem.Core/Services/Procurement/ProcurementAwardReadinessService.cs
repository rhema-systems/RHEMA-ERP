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

public sealed class ProcurementAwardReadinessService : IProcurementAwardReadinessService
{
    private const string EventType = "ProcurementAwardReadiness";
    private const string ReadPermission = "procurement.records.read";
    private const string ApprovePermission = "procurement.tender.approve";
    private const string EvaluatorAwardApproverSodControl =
        "SOD-EVALUATOR-AWARD-APPROVER";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly ISupplierValidationService _supplierValidation;

    public ProcurementAwardReadinessService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        ISupplierValidationService supplierValidation)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _supplierValidation = supplierValidation;
    }

    private IGenericRepository<ProcurementAwardReadinessDecision> Decisions =>
        _unitOfWork.Repository<ProcurementAwardReadinessDecision>();

    public async Task<ProcurementAwardReadinessDto> EvaluateAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        EvaluateProcurementAwardReadinessRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidateMutationInput(sourceId, request);
        var source = await ResolveSourceHeaderAsync(sourceType, sourceId, cancellationToken);
        var legacyApproval = source.IsLegacyTender;
        await EnsureCapabilityAsync(
            ApprovePermission,
            source.Reference,
            NormalizeCorrelation(correlationId),
            cancellationToken);
        await EnforceAwardSodAsync(source, NormalizeCorrelation(correlationId), cancellationToken);
        await EnforceEvaluatorAwardApproverSodAsync(
            source, NormalizeCorrelation(correlationId), cancellationToken);
        return await EvaluateCoreAsync(
            source,
            request,
            NormalizeCorrelation(correlationId),
            legacyApproval,
            cancellationToken);
    }

    public async Task<ProcurementAwardReadinessDto> EnsureAwardReadyAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        EvaluateProcurementAwardReadinessRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidateMutationInput(sourceId, request);
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var source = await ResolveSourceHeaderAsync(sourceType, sourceId, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, source.Reference, normalizedCorrelation, cancellationToken);
        await EnforceAwardSodAsync(source, normalizedCorrelation, cancellationToken);
        await EnforceEvaluatorAwardApproverSodAsync(
            source, normalizedCorrelation, cancellationToken);
        var result = await EvaluateCoreAsync(
            source,
            request,
            normalizedCorrelation,
            source.IsLegacyTender,
            cancellationToken);
        if (!result.IsReady)
            throw new ProcurementAwardReadinessBlockedException(
                "AWARD_READINESS_BLOCKED",
                $"Award is blocked by {result.BlockedReasons.Count} incomplete or invalid prerequisite(s).",
                result);
        return result;
    }

    public async Task<ProcurementEvaluatorAwardApproverSodStatusDto>
        GetEvaluatorAwardApproverSodStatusAsync(
            ProcurementAwardReadinessSourceType sourceType,
            Guid sourceId,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        if (sourceId == Guid.Empty)
            throw Validation("AWARD_READINESS_SOURCE_REQUIRED",
                "SourceId is required.");
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var source = await ResolveSourceHeaderAsync(
            sourceType, sourceId, cancellationToken);
        await EnsureCapabilityAsync(
            ReadPermission,
            source.Reference,
            normalizedCorrelation,
            cancellationToken);
        return await EvaluateEvaluatorAwardApproverSodAsync(
            source, normalizedCorrelation, cancellationToken);
    }

    public async Task<ProcurementAwardReadinessDto?> GetLatestAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var latest = await DecisionQuery()
            .AsNoTracking()
            .OrderByDescending(item => item.DecisionSequence)
            .FirstOrDefaultAsync(
                item => item.SourceType == sourceType && item.SourceId == sourceId,
                cancellationToken);
        if (latest is null)
        {
            _ = await ResolveSourceHeaderAsync(sourceType, sourceId, cancellationToken);
            return null;
        }

        var currentHash = await TryGetCurrentSourceHashAsync(sourceType, sourceId, cancellationToken);
        return Map(latest, string.Equals(
            latest.SourceIntegrityHash,
            currentHash,
            StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<ProcurementAwardReadinessDto>> GetHistoryAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        _ = await ResolveSourceHeaderAsync(sourceType, sourceId, cancellationToken);
        var boundedTake = Math.Clamp(take, 1, 200);
        var rows = await DecisionQuery()
            .AsNoTracking()
            .Where(item => item.SourceType == sourceType && item.SourceId == sourceId)
            .OrderByDescending(item => item.DecisionSequence)
            .Take(boundedTake)
            .ToListAsync(cancellationToken);
        var currentHash = await TryGetCurrentSourceHashAsync(sourceType, sourceId, cancellationToken);
        return rows.Select(item => Map(
                item,
                string.Equals(item.SourceIntegrityHash, currentHash, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private async Task<ProcurementAwardReadinessDto> EvaluateCoreAsync(
        SourceHeader source,
        EvaluateProcurementAwardReadinessRequest request,
        string correlationId,
        bool legacyApprovalAuthorized,
        CancellationToken cancellationToken)
    {
        var state = await BuildStateAsync(source, legacyApprovalAuthorized, cancellationToken);
        AssertExpectations(request, state);

        var existing = await DecisionQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                    item.SourceType == source.Type &&
                    item.SourceId == source.Id &&
                    item.IdempotencyKey == request.IdempotencyKey.Trim(),
                cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.SourceIntegrityHash, state.SourceIntegrityHash,
                    StringComparison.OrdinalIgnoreCase))
                throw Conflict(
                    "AWARD_READINESS_IDEMPOTENCY_CONFLICT",
                    "The idempotency key was already used against a different source state.");
            return Map(existing, isCurrent: true);
        }

        var nextSequence = (await DecisionQuery()
            .Where(item => item.SourceType == source.Type && item.SourceId == source.Id)
            .Select(item => (int?)item.DecisionSequence)
            .MaxAsync(cancellationToken) ?? 0) + 1;
        var now = DateTime.UtcNow;
        var entity = new ProcurementAwardReadinessDecision
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            SourceType = source.Type,
            SourceId = source.Id,
            SourceReference = source.Reference,
            Method = state.Method,
            DecisionSequence = nextSequence,
            Status = state.BlockedReasons.Count == 0
                ? ProcurementAwardReadinessDecisionStatus.Ready
                : ProcurementAwardReadinessDecisionStatus.Blocked,
            MethodRuleId = state.Authority.MethodRuleId,
            MethodRuleCode = state.Authority.MethodRuleCode,
            AuthorityRouteId = state.Authority.AuthorityRouteId,
            AuthorityRouteReference = state.Authority.AuthorityRouteReference,
            WorkflowDefinitionId = state.Authority.WorkflowDefinitionId,
            WorkflowInstanceId = state.Authority.WorkflowInstanceId,
            RecommendationSubjectType = state.Recommendation.SubjectType,
            RecommendedSubjectIdsJson = Serialize(state.Recommendation.SubjectIds.OrderBy(item => item)),
            RecommendedBusinessPartnerIdsJson =
                Serialize(state.Recommendation.BusinessPartnerIds.OrderBy(item => item)),
            RecommendationSnapshotJson = Serialize(state.Recommendation),
            EvaluationLineageJson = Serialize(state.Evaluations),
            SupplierLineageJson = Serialize(state.Suppliers),
            PrequalificationLineageJson = Serialize(state.Suppliers
                .SelectMany(item => item.Prequalification)
                .OrderBy(item => item.EntryId)),
            VerificationLineageJson = Serialize(state.Verifications),
            AuthorityLineageJson = Serialize(state.Authority),
            EvidenceLineageJson = Serialize(state.Evidence),
            PrerequisiteSnapshotJson = Serialize(state.Groups),
            TimelineSnapshotJson = Serialize(state.Timeline),
            BlockedReasonsJson = Serialize(state.BlockedReasons),
            SourceIntegrityHash = state.SourceIntegrityHash,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlationId,
            EvaluatedAtUtc = now,
            EvaluatedByUserId = _currentUser.UserId,
            EvaluatedByName = ActorName(),
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        entity.IntegrityHash = ComputeHash(Serialize(new
        {
            schemaVersion = "tdc.award-readiness-decision.v1",
            entity.Id,
            entity.TenantId,
            entity.SourceType,
            entity.SourceId,
            entity.SourceReference,
            entity.Method,
            entity.DecisionSequence,
            entity.Status,
            entity.MethodRuleId,
            entity.AuthorityRouteId,
            entity.WorkflowDefinitionId,
            entity.WorkflowInstanceId,
            entity.RecommendationSubjectType,
            entity.RecommendedSubjectIdsJson,
            entity.RecommendedBusinessPartnerIdsJson,
            entity.RecommendationSnapshotJson,
            entity.EvaluationLineageJson,
            entity.SupplierLineageJson,
            entity.PrequalificationLineageJson,
            entity.VerificationLineageJson,
            entity.AuthorityLineageJson,
            entity.EvidenceLineageJson,
            entity.PrerequisiteSnapshotJson,
            entity.TimelineSnapshotJson,
            entity.BlockedReasonsJson,
            entity.SourceIntegrityHash,
            entity.IdempotencyKey,
            entity.CorrelationId,
            entity.EvaluatedAtUtc,
            entity.EvaluatedByUserId
        }));

        await Decisions.AddAsync(entity);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            var winner = await DecisionQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                        item.SourceType == source.Type &&
                        item.SourceId == source.Id &&
                        item.IdempotencyKey == request.IdempotencyKey.Trim(),
                    cancellationToken);
            if (winner is not null &&
                string.Equals(winner.SourceIntegrityHash, state.SourceIntegrityHash,
                    StringComparison.OrdinalIgnoreCase))
                return Map(winner, isCurrent: true);
            throw Conflict(
                "AWARD_READINESS_CONCURRENCY_CONFLICT",
                "Another readiness evaluation was recorded concurrently; refresh and retry with a new idempotency key.");
        }
        var dto = Map(entity, isCurrent: true);
        await RecordAsync(dto, cancellationToken);
        return dto;
    }

    private async Task<ReadinessState> BuildStateAsync(
        SourceHeader source,
        bool legacyApprovalAuthorized,
        CancellationToken cancellationToken)
    {
        var state = new ReadinessState();
        switch (source.Type)
        {
            case ProcurementAwardReadinessSourceType.RequestForQuotation:
                await BuildRfqStateAsync(source, state, cancellationToken);
                break;
            case ProcurementAwardReadinessSourceType.Tender:
                await BuildTenderStateAsync(source, state, legacyApprovalAuthorized, cancellationToken);
                break;
            case ProcurementAwardReadinessSourceType.ExceptionalSourcing:
                await BuildExceptionalStateAsync(source, state, cancellationToken);
                break;
            default:
                throw Validation("AWARD_READINESS_SOURCE_TYPE_INVALID", "Unsupported award-readiness source type.");
        }
        FinalizeState(state, source);
        return state;
    }

    private async Task BuildRfqStateAsync(
        SourceHeader source,
        ReadinessState state,
        CancellationToken cancellationToken)
    {
        var rfq = await _unitOfWork.Repository<RequestForQuotation>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == source.Id && !item.IsDeleted)
            .Include(item => item.Items)
            .Include(item => item.Quotes)
            .Include(item => item.SourcingCase)
            .Include(item => item.OpeningRegister)
            .Include(item => item.Evaluation).ThenInclude(item => item!.Lines)
            .AsNoTracking()
            .SingleAsync(cancellationToken);
        state.Method = ProcurementMethodType.RequestForQuotation;
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Source, "RFQ_SOURCE_CURRENT",
            rfq.Status is not "Awarded" and not "Cancelled" and not "Rejected",
            "RFQ exists in an awardable tenant-owned state.",
            "Restore the RFQ to its controlled approved state.", "RequestForQuotation", rfq.Id);

        var evaluation = rfq.Evaluation;
        if (evaluation is null)
        {
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.Recommendation,
                "RFQ_RECOMMENDATION_MISSING", false,
                "An approved RFQ evaluation recommendation is required.",
                "Complete and approve the RFQ evaluation.");
            state.Recommendation.SubjectType = "RequestForQuotationQuote";
            return;
        }

        state.Authority = new ProcurementAwardReadinessAuthorityDto
        {
            MethodRuleId = evaluation.MethodRuleId,
            MethodRuleCode = evaluation.MethodRuleCode,
            AuthorityRouteId = rfq.SourcingCase?.AuthorityRouteId,
            AuthorityRouteReference = rfq.SourcingCase?.AuthorityRouteReference,
            WorkflowDefinitionId = evaluation.WorkflowDefinitionId,
            WorkflowInstanceId = evaluation.WorkflowInstanceId,
            ApprovalReference = evaluation.ApprovalReference,
            ApprovedAtUtc = evaluation.ApprovedAtUtc,
            ApprovedByUserId = evaluation.ApprovedByUserId,
            ApprovalActorUserIds = ParseGuids(evaluation.ApprovalActorsJson)
        };
        state.Recommendation = new ProcurementAwardReadinessRecommendationDto
        {
            SubjectType = "RequestForQuotationQuote",
            SubjectIds = evaluation.Lines.Where(item => !item.IsDeleted)
                .Select(item => item.QuoteId).Distinct().OrderBy(item => item).ToList(),
            BusinessPartnerIds = evaluation.Lines.Where(item => !item.IsDeleted)
                .Select(item => item.BusinessPartnerId).Distinct().OrderBy(item => item).ToList(),
            Reason = evaluation.RecommendationReason,
            EvidenceReference = evaluation.EvidenceReference,
            RecommendedAtUtc = evaluation.SubmittedAtUtc,
            RecommendedByUserId = evaluation.SubmittedByUserId
        };
        var completeCoverage = evaluation.Lines.Count(item => !item.IsDeleted) ==
                               rfq.Items.Count(item => !item.IsDeleted) &&
                               evaluation.Lines.Where(item => !item.IsDeleted)
                                   .Select(item => item.RfqItemId).Distinct().Count() ==
                               rfq.Items.Count(item => !item.IsDeleted);
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Recommendation,
            "RFQ_RECOMMENDATION_APPROVED",
            evaluation.Status == ProcurementRfqEvaluationStatus.Approved &&
            completeCoverage &&
            state.Recommendation.SubjectIds.Count != 0 &&
            state.Recommendation.BusinessPartnerIds.Count != 0,
            "The approved recommendation covers every RFQ line exactly once.",
            "Approve a complete current RFQ recommendation.", "ProcurementRfqEvaluation", evaluation.Id,
            evaluation.IntegrityHash);
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Evaluation,
            "RFQ_EVALUATION_INTEGRITY",
            !string.IsNullOrWhiteSpace(evaluation.SnapshotJson) &&
            HashMatches(NormalizeJson(evaluation.SnapshotJson), evaluation.IntegrityHash),
            "The retained RFQ evaluation projection is intact.",
            "Recall and replace the evaluation through the controlled committee workflow.",
            "ProcurementRfqEvaluation", evaluation.Id, evaluation.IntegrityHash);
        state.Evaluations.Add(new ProcurementAwardReadinessEvaluationDto
        {
            EvaluationType = "ProcurementRfqEvaluation",
            EvaluationId = evaluation.Id,
            Phase = ProcurementEvaluationPhase.Combined,
            Status = evaluation.Status.ToString(),
            CompletedAtUtc = evaluation.ApprovedAtUtc,
            EvidenceReference = evaluation.EvidenceReference,
            IntegrityHash = evaluation.IntegrityHash
        });
        await AddScoreLineageAsync(
            state,
            ProcurementEvaluationSourceType.RequestForQuotation,
            rfq.Id,
            evaluation.Id,
            "ProcurementRfqEvaluation",
            [(ProcurementEvaluationPhase.Combined, evaluation.SnapshotJson)],
            cancellationToken);
        await AddWorkflowAsync(state, cancellationToken);
        await AddSuppliersAsync(state, requiresPrequalification: false, recommendationAtUtc:
            evaluation.ApprovedAtUtc ?? evaluation.SubmittedAtUtc, cancellationToken);
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.VerificationAndDueDiligence,
            "RFQ_VERIFICATION_NOT_APPLICABLE", ProcurementAwardReadinessPrerequisiteStatus.NotApplicable,
            "The existing tender award-verification checklist is not an RFQ source.",
            null);
        AddEvidence(state, "RFQ_EVALUATION", "Signed RFQ evaluation recommendation",
            null, evaluation.EvidenceReference);
        AddEvidence(state, "RFQ_APPROVAL", "RFQ authority approval", evaluation.WorkflowInstanceId,
            evaluation.ApprovalReference);
        if (rfq.OpeningRegister is not null)
            state.Timeline.Add(Timeline("OpeningRegisterClosed", rfq.OpeningRegister.ClosedAtUtc,
                rfq.OpeningRegister.OpenedByUserId, rfq.OpeningRegister.EvidenceReference,
                rfq.OpeningRegister.IntegrityHash));
        if (evaluation.SubmittedAtUtc.HasValue)
            state.Timeline.Add(Timeline("RecommendationSubmitted", evaluation.SubmittedAtUtc.Value,
                evaluation.SubmittedByUserId, evaluation.EvidenceReference, evaluation.IntegrityHash));
        if (evaluation.ApprovedAtUtc.HasValue)
            state.Timeline.Add(Timeline("RecommendationApproved", evaluation.ApprovedAtUtc.Value,
                evaluation.ApprovedByUserId, evaluation.ApprovalReference, evaluation.IntegrityHash));
    }

    private async Task BuildTenderStateAsync(
        SourceHeader source,
        ReadinessState state,
        bool legacyApprovalAuthorized,
        CancellationToken cancellationToken)
    {
        var tender = await LoadTenderAsync(source.Id, cancellationToken);
        var controls = await _unitOfWork.Repository<ProcurementTenderControl>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.TenderId == source.Id && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (controls.Count > 1)
        {
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.Source,
                "TENDER_CONTROL_AMBIGUOUS", false,
                "More than one active formal tender control exists for this source.",
                "Retire the duplicate control before award.");
            return;
        }
        if (controls.Count == 1)
            await BuildFormalTenderStateAsync(tender, controls[0], state, cancellationToken);
        else
            await BuildLegacyTenderStateAsync(
                tender, state, legacyApprovalAuthorized, cancellationToken);
    }

    private async Task BuildFormalTenderStateAsync(
        Tender tender,
        ProcurementTenderControl control,
        ReadinessState state,
        CancellationToken cancellationToken)
    {
        state.Method = control.Method;
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Source, "TENDER_SOURCE_CURRENT",
            control.Status == ProcurementTenderControlStatus.Approved &&
            tender.Status is not "Awarded" and not "Cancelled",
            "The NCT/ICT control is approved and has not already been awarded.",
            "Complete the source-owned approval workflow.", "ProcurementTenderControl", control.Id,
            control.IntegrityHash);
        state.Authority = new ProcurementAwardReadinessAuthorityDto
        {
            MethodRuleId = control.MethodRuleId,
            MethodRuleCode = control.MethodRuleCode,
            AuthorityRouteId = control.AuthorityRouteId,
            AuthorityRouteReference = control.AuthorityRouteReference,
            WorkflowDefinitionId = control.WorkflowDefinitionId,
            WorkflowInstanceId = control.WorkflowInstanceId,
            ApprovalReference = control.AuthorityApprovalReference,
            ApprovedAtUtc = control.ApprovedAtUtc,
            ApprovedByUserId = control.ApprovedById,
            ApprovalActorUserIds = ParseGuids(control.ApprovalActorsJson)
        };
        var bid = control.RecommendedBidId.HasValue
            ? await LoadRecommendedBidAsync(tender.Id, control.RecommendedBidId.Value, cancellationToken)
            : null;
        state.Recommendation = new ProcurementAwardReadinessRecommendationDto
        {
            SubjectType = "TenderBid",
            SubjectIds = bid is null ? [] : [bid.Id],
            BusinessPartnerIds = bid is null ? [] : [bid.BusinessPartnerId],
            Reason = TryGetJsonString(control.FinancialEvaluationSnapshotJson, "recommendationReason"),
            EvidenceReference = control.FinancialEvaluationEvidenceReference,
            RecommendedAtUtc = control.FinancialEvaluatedAtUtc,
            RecommendedByUserId = TryGetJsonGuid(
                control.FinancialEvaluationSnapshotJson, "evaluatorUserId")
        };
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Recommendation,
            "TENDER_RECOMMENDATION_APPROVED",
            bid is not null && control.ApprovedAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(control.FinancialEvaluationEvidenceReference),
            "The approved NCT/ICT recommendation identifies one current tender bid and supplier.",
            "Complete and approve the financial recommendation.",
            "ProcurementTenderControl", control.Id, control.FinancialEvaluationIntegrityHash);
        var technicalValid = ProjectionValid(
            control.TechnicalEvaluationSnapshotJson, control.TechnicalEvaluationIntegrityHash);
        var financialValid = ProjectionValid(
            control.FinancialEvaluationSnapshotJson, control.FinancialEvaluationIntegrityHash);
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Evaluation,
            "TENDER_EVALUATION_INTEGRITY",
            technicalValid && financialValid,
            "Technical and financial evaluation projections are complete and intact.",
            "Recall and replace invalid evaluation projections through the committee workflow.",
            "ProcurementTenderControl", control.Id, control.IntegrityHash);
        state.Evaluations.AddRange(
        [
            new ProcurementAwardReadinessEvaluationDto
            {
                EvaluationType = "ProcurementTenderControl",
                EvaluationId = control.Id,
                Phase = ProcurementEvaluationPhase.Technical,
                Status = technicalValid ? "Completed" : "Invalid",
                CompletedAtUtc = control.TechnicalEvaluatedAtUtc,
                EvidenceReference = control.TechnicalEvaluationEvidenceReference,
                IntegrityHash = control.TechnicalEvaluationIntegrityHash
            },
            new ProcurementAwardReadinessEvaluationDto
            {
                EvaluationType = "ProcurementTenderControl",
                EvaluationId = control.Id,
                Phase = ProcurementEvaluationPhase.Financial,
                Status = financialValid ? "Completed" : "Invalid",
                CompletedAtUtc = control.FinancialEvaluatedAtUtc,
                EvidenceReference = control.FinancialEvaluationEvidenceReference,
                IntegrityHash = control.FinancialEvaluationIntegrityHash
            }
        ]);
        await AddScoreLineageAsync(
            state,
            ProcurementEvaluationSourceType.Tender,
            tender.Id,
            tender.Id,
            "ProcurementTenderControl",
            [
                (ProcurementEvaluationPhase.Technical,
                    control.TechnicalEvaluationSnapshotJson ?? string.Empty),
                (ProcurementEvaluationPhase.Financial,
                    control.FinancialEvaluationSnapshotJson ?? string.Empty)
            ],
            cancellationToken);
        await AddWorkflowAsync(state, cancellationToken);
        await AddSuppliersAsync(state, tender.RequiresPrequalification,
            control.ApprovedAtUtc ?? control.FinancialEvaluatedAtUtc, cancellationToken);
        await AddAwardVerificationAsync(
            state, tender, control.ApprovedAtUtc ?? control.FinancialEvaluatedAtUtc,
            cancellationToken);
        AddEvidence(state, "TENDER_TECHNICAL_EVALUATION", "Signed technical evaluation",
            null, control.TechnicalEvaluationEvidenceReference);
        AddEvidence(state, "TENDER_FINANCIAL_RECOMMENDATION", "Signed financial recommendation",
            null, control.FinancialEvaluationEvidenceReference);
        AddEvidence(state, "TENDER_AUTHORITY_APPROVAL", "Tender authority approval",
            control.WorkflowInstanceId, control.AuthorityApprovalReference);
        if (control.TechnicalEvaluatedAtUtc.HasValue)
            state.Timeline.Add(Timeline("TechnicalEvaluationCompleted",
                control.TechnicalEvaluatedAtUtc.Value,
                TryGetJsonGuid(control.TechnicalEvaluationSnapshotJson, "evaluatorUserId"),
                control.TechnicalEvaluationEvidenceReference, control.TechnicalEvaluationIntegrityHash));
        if (control.FinancialEvaluatedAtUtc.HasValue)
            state.Timeline.Add(Timeline("RecommendationCompleted",
                control.FinancialEvaluatedAtUtc.Value,
                TryGetJsonGuid(control.FinancialEvaluationSnapshotJson, "evaluatorUserId"),
                control.FinancialEvaluationEvidenceReference, control.FinancialEvaluationIntegrityHash));
        if (control.ApprovedAtUtc.HasValue)
            state.Timeline.Add(Timeline("RecommendationApproved", control.ApprovedAtUtc.Value,
                control.ApprovedById, control.AuthorityApprovalReference, control.IntegrityHash));
    }

    private async Task BuildLegacyTenderStateAsync(
        Tender tender,
        ReadinessState state,
        bool legacyApprovalAuthorized,
        CancellationToken cancellationToken)
    {
        var evaluations = await _unitOfWork.Repository<TenderEvaluation>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.TenderBid.TenderId == tender.Id && !item.IsDeleted)
            .Include(item => item.TenderBid)
            .Include(item => item.TenderEvaluator)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        // A recalled sheet can be replaced by a new evaluation projection.
        // Retain the latest projection per bidder/voter, including a new draft
        // so an unfinished replacement cannot reuse the previous recommendation.
        // The separate SOD resolver deliberately retains all evaluator history.
        evaluations = ProcurementTenderEvaluationProjectionPolicy.SelectCurrent(evaluations);
        var evaluationsComplete = evaluations.Count != 0 &&
                                  evaluations.All(IsCompletedLegacyEvaluation);
        var recommended = evaluationsComplete
            ? evaluations.Where(item => item.IsRecommended).ToList()
            : new List<TenderEvaluation>();
        var recommendedBidIds = recommended.Select(item => item.TenderBidId).Distinct().ToList();
        var recommendedPartnerIds = recommended.Select(item => item.TenderBid.BusinessPartnerId)
            .Distinct().ToList();
        state.Method = tender.SourcingCase?.SelectedMethod ??
                       ProcurementMethodType.NationalCompetitiveTendering;
        state.Authority = new ProcurementAwardReadinessAuthorityDto
        {
            MethodRuleId = tender.SourcingCase?.MethodRuleId,
            MethodRuleCode = tender.SourcingCase?.MethodRuleCode,
            AuthorityRouteId = tender.SourcingCase?.AuthorityRouteId,
            AuthorityRouteReference = tender.SourcingCase?.AuthorityRouteReference,
            ApprovalReference = legacyApprovalAuthorized
                ? "award-readiness-decision"
                : null,
            ApprovedAtUtc = legacyApprovalAuthorized ? DateTime.UtcNow : null,
            ApprovedByUserId = legacyApprovalAuthorized ? _currentUser.UserId : null,
            ApprovalActorUserIds = legacyApprovalAuthorized ? [_currentUser.UserId] : []
        };
        state.Recommendation = new ProcurementAwardReadinessRecommendationDto
        {
            SubjectType = "TenderBid",
            SubjectIds = recommendedBidIds,
            BusinessPartnerIds = recommendedPartnerIds,
            Reason = recommended.Count == 1 ? recommended[0].Recommendation : null,
            EvidenceReference = recommended.Count == 1
                ? $"tender-evaluation:{recommended[0].Id:N}"
                : null,
            RecommendedAtUtc = recommended.Count == 0
                ? null
                : recommended.Max(item => (DateTime?)item.SubmittedDate ?? item.EvaluationDate),
            RecommendedByUserId = recommended.Count == 1
                ? recommended[0].TenderEvaluator.UserId
                : null
        };
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Source,
            "LEGACY_TENDER_SOURCE_CURRENT",
            tender.Status is not "Awarded" and not "Cancelled",
            "The legacy tender exists in an awardable state.",
            "Restore the tender to a controlled pre-award state.", "Tender", tender.Id);
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Recommendation,
            "LEGACY_RECOMMENDATION_UNAMBIGUOUS",
            recommendedBidIds.Count == 1 && recommendedPartnerIds.Count == 1,
            "Exactly one completed, immutable recommended tender bid and supplier are retained.",
            "Resolve conflicting or incomplete evaluator recommendations.",
            "TenderEvaluation", recommended.Count == 1 ? recommended[0].Id : null);
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.AuthorityAndWorkflow,
            "LEGACY_RECOMMENDATION_APPROVAL",
            legacyApprovalAuthorized,
            "This immutable readiness decision is the independently authorized legacy recommendation approval.",
            "Use the award-readiness approval action with delegated authority.");
        foreach (var evaluation in evaluations.OrderBy(item => item.Id))
        {
            state.Evaluations.Add(new ProcurementAwardReadinessEvaluationDto
            {
                EvaluationType = "TenderEvaluation",
                EvaluationId = evaluation.Id,
                Phase = ProcurementEvaluationPhase.Combined,
                Status = evaluation.Status,
                CompletedAtUtc = evaluation.SubmittedDate ?? evaluation.EvaluationDate,
                EvidenceReference = $"tender-evaluation:{evaluation.Id:N}",
                IntegrityHash = ComputeHash(Serialize(new
                {
                    evaluation.Id,
                    evaluation.TenderBidId,
                    evaluation.TenderEvaluatorId,
                    evaluation.Status,
                    evaluation.TotalScore,
                    evaluation.IsRecommended,
                    evaluation.Recommendation,
                    evaluation.SubmittedDate
                }))
            });
        }
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Evaluation,
            "LEGACY_EVALUATIONS_COMPLETE",
            evaluationsComplete,
            "Every retained legacy evaluator result is submitted and immutable.",
            "Submit or remove draft/rejected evaluator results through the controlled committee workflow.");
        await AddLegacyScoreLineageAsync(state, tender.Id, evaluations, cancellationToken);
        await AddSuppliersAsync(state, tender.RequiresPrequalification,
            state.Recommendation.RecommendedAtUtc, cancellationToken);
        await AddAwardVerificationAsync(
            state, tender, state.Recommendation.RecommendedAtUtc, cancellationToken);
        foreach (var evaluation in evaluations)
            AddEvidence(state, "LEGACY_EVALUATION", "Legacy evaluator result",
                evaluation.Id, $"tender-evaluation:{evaluation.Id:N}");
    }

    private async Task BuildExceptionalStateAsync(
        SourceHeader source,
        ReadinessState state,
        CancellationToken cancellationToken)
    {
        var tender = await LoadTenderAsync(source.Id, cancellationToken);
        var controls = await _unitOfWork.Repository<ProcurementExceptionalSourcingControl>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.TenderId == source.Id && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (controls.Count != 1)
        {
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.Source,
                controls.Count == 0 ? "EXCEPTIONAL_CONTROL_MISSING" : "EXCEPTIONAL_CONTROL_AMBIGUOUS",
                false,
                "Exactly one exceptional-sourcing control is required.",
                "Resolve the exceptional-sourcing control lineage before award.");
            return;
        }
        var control = controls[0];
        var petty = control.Method == ProcurementMethodType.PettyPurchase;
        state.Method = control.Method;
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Source,
            "EXCEPTIONAL_SOURCE_CURRENT",
            control.Status == ProcurementExceptionalSourcingControlStatus.Recommended &&
            tender.Status is not "Awarded" and not "Cancelled",
            petty ? "The Petty Purchase has an approved quotation recommendation and is not awarded." : "The exceptional source has reached a negotiated recommendation and is not awarded.",
            petty ? "Complete independent approval and quotation recommendation before award." : "Complete approval, negotiation, and recommendation before award.",
            "ProcurementExceptionalSourcingControl", control.Id, control.IntegrityHash);
        state.Authority = new ProcurementAwardReadinessAuthorityDto
        {
            MethodRuleId = control.MethodRuleId,
            MethodRuleCode = control.MethodRuleCode,
            AuthorityRouteId = control.AuthorityRouteId,
            AuthorityRouteReference = control.AuthorityRouteReference,
            WorkflowDefinitionId = control.WorkflowDefinitionId,
            WorkflowInstanceId = control.WorkflowInstanceId,
            ApprovalReference = control.PpaApprovalReference ??
                                control.ManagingDirectorApprovalReference ??
                                control.BoardApprovalReference ??
                                (petty && control.WorkflowInstanceId.HasValue ? $"workflow:{control.WorkflowInstanceId.Value:N}" : null),
            ApprovedAtUtc = control.ApprovedAtUtc,
            ApprovedByUserId = control.ApprovedById,
            ApprovalActorUserIds = ParseGuids(control.ApprovalActorsJson)
        };
        var bid = control.RecommendedBidId.HasValue
            ? await LoadRecommendedBidAsync(tender.Id, control.RecommendedBidId.Value, cancellationToken)
            : null;
        state.Recommendation = new ProcurementAwardReadinessRecommendationDto
        {
            SubjectType = "TenderBid",
            SubjectIds = bid is null ? [] : [bid.Id],
            BusinessPartnerIds = bid is null ? [] : [bid.BusinessPartnerId],
            Reason = control.RecommendationReason,
            EvidenceReference = control.RecommendationEvidenceReference,
            RecommendedAtUtc = control.RecommendedAtUtc,
            RecommendedByUserId = control.RecommendedById
        };
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Recommendation,
            "EXCEPTIONAL_RECOMMENDATION_COMPLETE",
            bid is not null && control.RecommendedAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(control.RecommendationReason) &&
            !string.IsNullOrWhiteSpace(control.RecommendationEvidenceReference),
            "The negotiated recommendation identifies one current bid and supplier with evidence.",
            "Complete the negotiated recommendation and attach its evidence.",
            "ProcurementExceptionalSourcingControl", control.Id, control.IntegrityHash);
        state.Evaluations.Add(new ProcurementAwardReadinessEvaluationDto
        {
            EvaluationType = petty ? "PettyQuotationRecommendation" : "ExceptionalNegotiatedRecommendation",
            EvaluationId = control.Id,
            Phase = ProcurementEvaluationPhase.Combined,
            Status = control.Status.ToString(),
            CompletedAtUtc = control.RecommendedAtUtc,
            EvidenceReference = control.RecommendationEvidenceReference,
            IntegrityHash = control.IntegrityHash
        });
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.Evaluation,
            petty ? "PETTY_QUOTATION_REVIEWED" : "EXCEPTIONAL_NEGOTIATION_EVALUATED",
            petty ? bid is not null && bid.TotalBidAmount > 0 && bid.TotalBidAmount <= tender.EstimatedValue &&
                control.ApprovedAtUtc.HasValue && control.RecommendedAtUtc.HasValue &&
                !string.IsNullOrWhiteSpace(control.SupplierSelectionEvidenceReference) :
            control.NegotiatedAtUtc.HasValue && control.RecommendedAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(control.NegotiationMinutesEvidenceReference) &&
            !string.IsNullOrWhiteSpace(control.NegotiationOutcomeReference),
            petty ? "The approved supplier quotation and recommendation are retained." : "Negotiation minutes, outcome, and resulting recommendation are retained.",
            petty ? "Review and recommend the recorded quotation within the approved value." : "Complete the negotiation record and recommendation.");
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.ScoreIntegrity,
            "EXCEPTIONAL_SCORE_NOT_APPLICABLE",
            ProcurementAwardReadinessPrerequisiteStatus.NotApplicable,
            "The source-applicable evaluation is a negotiated recommendation, not committee score sheets.",
            null);
        await AddWorkflowAsync(state, cancellationToken);
        await AddSuppliersAsync(state, tender.RequiresPrequalification,
            control.RecommendedAtUtc, cancellationToken);
        await AddAwardVerificationAsync(
            state, tender, control.RecommendedAtUtc, cancellationToken);
        AddEvidence(state, "EXCEPTIONAL_JUSTIFICATION", "Exceptional sourcing justification",
            null, control.JustificationEvidenceReference);
        AddEvidence(state, "EXCEPTIONAL_SUPPLIER_SELECTION", "Supplier selection evidence",
            null, control.SupplierSelectionEvidenceReference);
        AddEvidence(state, "EXCEPTIONAL_NEGOTIATION", "Negotiation minutes",
            control.NegotiationId, control.NegotiationMinutesEvidenceReference);
        AddEvidence(state, "EXCEPTIONAL_RECOMMENDATION", "Negotiated recommendation",
            control.RecommendedBidId, control.RecommendationEvidenceReference);
        AddEvidence(state, "EXCEPTIONAL_AUTHORITY", "Exceptional authority approval",
            control.WorkflowInstanceId, state.Authority.ApprovalReference);
        if (control.ApprovedAtUtc.HasValue)
            state.Timeline.Add(Timeline("ExceptionalAuthorityApproved",
                control.ApprovedAtUtc.Value, control.ApprovedById,
                state.Authority.ApprovalReference, control.IntegrityHash));
        if (control.NegotiatedAtUtc.HasValue)
            state.Timeline.Add(Timeline("NegotiationCompleted",
                control.NegotiatedAtUtc.Value, null,
                control.NegotiationOutcomeReference, control.IntegrityHash));
        if (control.RecommendedAtUtc.HasValue)
            state.Timeline.Add(Timeline("RecommendationCompleted",
                control.RecommendedAtUtc.Value, control.RecommendedById,
                control.RecommendationEvidenceReference, control.IntegrityHash));
    }

    private async Task AddScoreLineageAsync(
        ReadinessState state,
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        Guid scoreSubjectId,
        string scoreSubjectType,
        IReadOnlyList<(ProcurementEvaluationPhase Phase, string Snapshot)> expected,
        CancellationToken cancellationToken)
    {
        var controls = await _unitOfWork.Repository<ProcurementEvaluationCommitteeControl>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceType == sourceType &&
                item.SourceId == sourceId &&
                !item.IsDeleted)
            .Include(item => item.Appointments)
            .Include(item => item.Meetings)
            .AsNoTracking()
            .OrderByDescending(item => item.Version)
            .ToListAsync(cancellationToken);
        var current = controls.FirstOrDefault();
        if (current is null)
        {
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.ScoreIntegrity,
                "EVALUATION_COMMITTEE_MISSING", false,
                "No source-specific evaluation committee control exists.",
                "Constitute and activate the evaluation committee.");
            return;
        }

        var currentMatches = controls.Count(item =>
            item.Status is ProcurementEvaluationCommitteeControlStatus.Draft or
                ProcurementEvaluationCommitteeControlStatus.Active) == 1;
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.ScoreIntegrity,
            "EVALUATION_COMMITTEE_CURRENT",
            currentMatches &&
            current.Status is ProcurementEvaluationCommitteeControlStatus.Active or
                ProcurementEvaluationCommitteeControlStatus.Closed,
            "Exactly one current evaluation committee control is active or closed.",
            "Resolve duplicate/draft committee controls and activate one current committee.",
            "ProcurementEvaluationCommitteeControl", current.Id,
            current.CompositionIntegrityHash);
        var relevantMeetings = current.Meetings.Where(item =>
            !item.IsDeleted && expected.Select(value => value.Phase).Contains(item.Phase)).ToList();
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.ScoreIntegrity,
            "EVALUATION_QUORUM_CURRENT",
            relevantMeetings.Count != 0 &&
            expected.All(value => relevantMeetings.Any(meeting =>
                meeting.Phase == value.Phase &&
                meeting.QuorumMet &&
                meeting.Status is ProcurementEvaluationMeetingStatus.QuorumConfirmed or
                    ProcurementEvaluationMeetingStatus.Closed)),
            "Every required evaluation phase retains signed quorum.",
            "Confirm quorum for every required evaluation phase.",
            "ProcurementEvaluationCommitteeControl", current.Id,
            current.CompositionIntegrityHash);

        var sheets = await _unitOfWork.Repository<ProcurementEvaluationScoreSheet>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.CommitteeControlId == current.Id &&
                item.ScoreSubjectType == scoreSubjectType &&
                item.ScoreSubjectId == scoreSubjectId &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var latest = sheets.GroupBy(item => new
            {
                item.AppointmentId,
                item.Phase,
                item.ScoreSubjectType,
                item.ScoreSubjectId
            })
            .Select(group => group
                .OrderByDescending(item => item.Attempt)
                .ThenByDescending(item => item.SubmittedAtUtc)
                .First())
            .OrderBy(item => item.Phase)
            .ThenBy(item => item.AppointmentId)
            .ToList();
        var currentIds = latest.Select(item => item.Id).ToHashSet();
        var recalls = await _unitOfWork.Repository<ProcurementEvaluationScoreRecall>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                currentIds.Contains(item.ScoreSheetId) &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var eligibleVotingAppointments = current.Appointments
            .Where(item =>
                !item.IsDeleted &&
                item.Status == ProcurementEvaluationAppointmentStatus.Accepted &&
                item.IsVoting)
            .ToDictionary(item => item.Id);

        foreach (var requirement in expected)
        {
            var phaseSheets = latest.Where(item => item.Phase == requirement.Phase).ToList();
            var hasCoverage = phaseSheets.Count == 1 &&
                              eligibleVotingAppointments.TryGetValue(
                                  phaseSheets[0].AppointmentId,
                                  out var appointment) &&
                              appointment.UserId == phaseSheets[0].SubmittedByUserId;
            var allLocked = phaseSheets.Count != 0 &&
                            phaseSheets.All(item =>
                                item.Status == ProcurementEvaluationScoreSheetStatus.Locked);
            var projectionsMatch = phaseSheets.Count != 0 &&
                                   phaseSheets.All(item =>
                                       NormalizeJson(item.ScoreSnapshotJson) ==
                                       NormalizeJson(requirement.Snapshot));
            var hasUnresolvedRecall = recalls.Any(item =>
                phaseSheets.Select(sheet => sheet.Id).Contains(item.ScoreSheetId) &&
                item.Status is ProcurementEvaluationScoreRecallStatus.PendingApproval or
                    ProcurementEvaluationScoreRecallStatus.Approved);
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.ScoreIntegrity,
                $"CURRENT_{requirement.Phase.ToString().ToUpperInvariant()}_SCORES",
                hasCoverage && allLocked && projectionsMatch && !hasUnresolvedRecall,
                $"Exactly one current aggregate locked {requirement.Phase} projection is retained by an eligible voting evaluator.",
                $"Retain one eligible aggregate {requirement.Phase} score projection or replace recalled/stale attempts.",
                "ProcurementEvaluationCommitteeControl", current.Id,
                current.CompositionIntegrityHash);

            var evaluation = state.Evaluations.FirstOrDefault(item =>
                item.Phase == requirement.Phase &&
                (item.EvaluationId == scoreSubjectId ||
                 string.Equals(item.EvaluationType, scoreSubjectType,
                     StringComparison.Ordinal)));
            if (evaluation is not null)
                evaluation.ScoreAttempts = phaseSheets.Select(sheet => MapScore(
                    sheet,
                    recalls.Where(recall => recall.ScoreSheetId == sheet.Id)
                        .Select(recall => recall.Id).ToList())).ToList();
            foreach (var sheet in phaseSheets)
                AddEvidence(state, $"SCORE_{requirement.Phase.ToString().ToUpperInvariant()}",
                    $"{requirement.Phase} locked score attempt {sheet.Attempt}",
                    sheet.Id, sheet.EvidenceReference);
        }

        var requiresTechnicalFinancialSeparation = expected.Any(item =>
                                                     item.Phase == ProcurementEvaluationPhase.Technical) &&
                                                 expected.Any(item =>
                                                     item.Phase == ProcurementEvaluationPhase.Financial);
        if (requiresTechnicalFinancialSeparation)
        {
            var technicalSheets = latest.Where(item =>
                item.Phase == ProcurementEvaluationPhase.Technical).ToList();
            var financialSheets = latest.Where(item =>
                item.Phase == ProcurementEvaluationPhase.Financial).ToList();
            var technicalSheet = technicalSheets.FirstOrDefault();
            var financialSheet = financialSheets.FirstOrDefault();
            var distinctEligibleScorers = technicalSheets.Count == 1 &&
                                          financialSheets.Count == 1 &&
                                          technicalSheet is not null &&
                                          financialSheet is not null &&
                                          technicalSheet.SubmittedByUserId !=
                                          financialSheet.SubmittedByUserId &&
                                          eligibleVotingAppointments.TryGetValue(
                                              technicalSheet.AppointmentId,
                                              out var technicalAppointment) &&
                                          technicalAppointment.UserId ==
                                          technicalSheet.SubmittedByUserId &&
                                          eligibleVotingAppointments.TryGetValue(
                                              financialSheet.AppointmentId,
                                              out var financialAppointment) &&
                                          financialAppointment.UserId ==
                                          financialSheet.SubmittedByUserId;
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.ScoreIntegrity,
                "TENDER_TECHNICAL_FINANCIAL_SCORER_SOD",
                distinctEligibleScorers,
                "The aggregate technical and financial projections were locked by distinct eligible evaluators.",
                "Assign distinct eligible voting evaluators to the technical and financial phases.",
                "ProcurementEvaluationCommitteeControl", current.Id,
                current.CompositionIntegrityHash);
        }
    }

    private async Task AddLegacyScoreLineageAsync(
        ReadinessState state,
        Guid tenderId,
        IReadOnlyList<TenderEvaluation> evaluations,
        CancellationToken cancellationToken)
    {
        var controls = await _unitOfWork.Repository<ProcurementEvaluationCommitteeControl>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceType == ProcurementEvaluationSourceType.Tender &&
                item.SourceId == tenderId &&
                !item.IsDeleted)
            .AsNoTracking()
            .OrderByDescending(item => item.Version)
            .ToListAsync(cancellationToken);
        var current = controls.FirstOrDefault();
        if (current is null)
        {
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.ScoreIntegrity,
                "LEGACY_COMMITTEE_MISSING", false,
                "No evaluation committee control exists for the legacy tender.",
                "Constitute the tender evaluation committee and lock evaluator scores.");
            return;
        }

        // The committee owns one attempt stream per voter and bid. The immutable
        // snapshot identifies the evaluation projection; the subject is the bid.
        var bidIds = evaluations.Select(item => item.TenderBidId).ToHashSet();
        var sheets = await _unitOfWork.Repository<ProcurementEvaluationScoreSheet>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.CommitteeControlId == current.Id &&
                item.Phase == ProcurementEvaluationPhase.Combined &&
                item.ScoreSubjectType == "TenderEvaluation" &&
                bidIds.Contains(item.ScoreSubjectId) &&
                !item.IsDeleted)
            .Include(item => item.Appointment)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var latest = sheets.GroupBy(item => new { item.AppointmentId, item.ScoreSubjectId })
            .Select(group => group.OrderByDescending(item => item.Attempt)
                .ThenByDescending(item => item.SubmittedAtUtc).First())
            .ToList();
        var currentIds = latest.Select(item => item.Id).ToHashSet();
        var recalls = await _unitOfWork.Repository<ProcurementEvaluationScoreRecall>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                currentIds.Contains(item.ScoreSheetId) &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var allCurrent = evaluations.Count != 0 &&
                         latest.Count == evaluations.Count &&
                         evaluations.All(evaluation =>
                             latest.Count(sheet => LegacyScoreMatchesEvaluation(evaluation, sheet) &&
                                                   sheet.Status ==
                                                   ProcurementEvaluationScoreSheetStatus.Locked &&
                                                   LegacyScoreSnapshotMatches(evaluation, sheet)) == 1) &&
                         !recalls.Any(item =>
                             item.Status is ProcurementEvaluationScoreRecallStatus.PendingApproval or
                                 ProcurementEvaluationScoreRecallStatus.Approved);
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.ScoreIntegrity,
            "LEGACY_SCORE_ATTEMPTS_CURRENT",
            allCurrent,
            "Every retained legacy evaluation has one current locked combined-score attempt.",
            "Lock missing evaluator results or replace recalled attempts.",
            "ProcurementEvaluationCommitteeControl", current.Id,
            current.CompositionIntegrityHash);
        foreach (var sheet in latest)
        {
            var evaluationId = TryGetJsonGuid(sheet.ScoreSnapshotJson, "evaluationId");
            var dto = state.Evaluations.SingleOrDefault(item => item.EvaluationId == evaluationId);
            if (dto is not null)
                dto.ScoreAttempts.Add(MapScore(sheet,
                    recalls.Where(item => item.ScoreSheetId == sheet.Id)
                        .Select(item => item.Id).ToList()));
            AddEvidence(state, "LEGACY_LOCKED_SCORE",
                $"Legacy evaluation locked attempt {sheet.Attempt}",
                sheet.Id, sheet.EvidenceReference);
        }
    }

    private async Task AddWorkflowAsync(
        ReadinessState state,
        CancellationToken cancellationToken)
    {
        if (!state.Authority.WorkflowDefinitionId.HasValue ||
            !state.Authority.WorkflowInstanceId.HasValue)
        {
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.AuthorityAndWorkflow,
                "AUTHORITY_WORKFLOW_MISSING", false,
                "The exact source-owned approval workflow lineage is missing.",
                "Submit and complete the configured authority workflow.");
            return;
        }
        var workflow = await _unitOfWork.Repository<WorkflowInstance>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == state.Authority.WorkflowInstanceId.Value &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        state.Authority.WorkflowStatus = workflow?.Status.ToString();
        var valid = workflow is not null &&
                    workflow.WorkflowDefinitionId == state.Authority.WorkflowDefinitionId.Value &&
                    workflow.Status == WorkflowInstanceStatus.Completed &&
                    state.Authority.ApprovedAtUtc.HasValue &&
                    state.Authority.ApprovedByUserId.HasValue &&
                    !string.IsNullOrWhiteSpace(state.Authority.ApprovalReference);
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.AuthorityAndWorkflow,
            "AUTHORITY_WORKFLOW_APPROVED",
            valid,
            "The exact shared workflow completed with retained authority approval evidence.",
            "Complete the configured source-owned authority workflow.",
            "WorkflowInstance", workflow?.Id);
        if (workflow is not null)
        {
            AddEvidence(state, "AUTHORITY_WORKFLOW", "Completed authority workflow",
                workflow.Id, $"workflow:{workflow.Id:N}");
            if (workflow.CompletedDate.HasValue)
                state.Timeline.Add(Timeline("AuthorityWorkflowCompleted",
                    workflow.CompletedDate.Value, state.Authority.ApprovedByUserId,
                    state.Authority.ApprovalReference, null));
        }
    }

    private async Task AddSuppliersAsync(
        ReadinessState state,
        bool requiresPrequalification,
        DateTime? recommendationAtUtc,
        CancellationToken cancellationToken)
    {
        var ids = state.Recommendation.BusinessPartnerIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.SupplierEligibility,
                "RECOMMENDED_SUPPLIER_MISSING", false,
                "No recommended supplier can be derived from the current source.",
                "Record one unambiguous source-owned recommendation.");
            if (requiresPrequalification)
                Add(state, ProcurementAwardReadinessPrerequisiteGroup.Prequalification,
                    "PREQUALIFICATION_SUPPLIER_MISSING", false,
                    "Prequalification cannot be evaluated without a recommended supplier.",
                    "Complete the recommendation.");
            return;
        }
        var partners = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                ids.Contains(item.Id) &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var entries = requiresPrequalification
            ? await _unitOfWork.Repository<ProcurementQualifiedListEntry>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    ids.Contains(item.BusinessPartnerId) &&
                    !item.IsDeleted)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
            : [];
        var now = DateTime.UtcNow;
        foreach (var id in ids.OrderBy(item => item))
        {
            var partner = partners.SingleOrDefault(item => item.Id == id);
            if (partner is null)
            {
                Add(state, ProcurementAwardReadinessPrerequisiteGroup.SupplierEligibility,
                    "SUPPLIER_FOREIGN_OR_MISSING", false,
                    $"Recommended supplier {id} is missing from the current tenant.",
                    "Replace the foreign/stale recommendation.");
                continue;
            }
            var baseline = await _supplierValidation.EvaluateBaselineEligibilityAsync(
                new SupplierEligibilityEvaluationRequest
                {
                    BusinessPartnerId = partner.Id,
                    Boundary = SupplierEligibilityBoundary.Award,
                    IncludeFinancialWarnings = true,
                    // The immutable readiness decision owns this operation's audit.
                    RecordAudit = false,
                    SourceType = "ProcurementAwardReadiness",
                    SourceReference = partner.PartnerCode
                }, cancellationToken);
            var errors = baseline.Errors.ToList();
            var warnings = baseline.Warnings.ToList();
            if (!baseline.IsValid && errors.Count == 0)
                errors.Add("The shared supplier eligibility checks did not pass.");
            if (!partner.ApprovedById.HasValue && !partner.CreatedById.HasValue)
                errors.Add("Supplier controller lineage is missing.");
            if (!partner.PartnerType.Contains("Supplier", StringComparison.OrdinalIgnoreCase) &&
                !partner.PartnerType.Contains("Contractor", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(partner.PartnerType, "Both", StringComparison.OrdinalIgnoreCase))
                errors.Add("Business partner is not eligible for procurement award.");
            await AddSupplierRiskAsync(partner, errors, warnings, cancellationToken);
            var supplier = new ProcurementAwardReadinessSupplierDto
            {
                BusinessPartnerId = partner.Id,
                PartnerCode = partner.PartnerCode,
                PartnerName = partner.PartnerName,
                IsEligible = errors.Count == 0,
                ValidationCode = errors.Count == 0 ? "OK" : "SUPPLIER_NOT_ELIGIBLE",
                Errors = errors,
                Warnings = warnings
            };
            if (requiresPrequalification)
            {
                supplier.Prequalification = entries.Where(item =>
                        item.BusinessPartnerId == partner.Id &&
                        item.Status == ProcurementQualifiedListEntryStatus.Active &&
                        item.ValidFromUtc <= now &&
                        item.ExpiresAtUtc >= now &&
                        item.RevokedAtUtc is null)
                    .OrderBy(item => item.ExpiresAtUtc)
                    .Select(MapPrequalification)
                    .ToList();
            }
            state.Suppliers.Add(supplier);
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.SupplierEligibility,
                $"SUPPLIER_{partner.Id:N}_ELIGIBLE",
                supplier.IsEligible,
                supplier.IsEligible
                    ? $"{partner.PartnerCode} is active, approved, registered, and not blacklisted."
                    : string.Join(" ", errors),
                supplier.IsEligible ? null : "Resolve supplier eligibility before award.",
                "BusinessPartner", partner.Id,
                ComputeHash(Serialize(new
                {
                    partner.Id,
                    partner.PartnerCode,
                    partner.PartnerType,
                    partner.IsActive,
                    partner.IsBlacklisted,
                    partner.ApprovalStatus,
                    partner.RegistrationStatus,
                    baseline.DecisionHash,
                    partner.UpdatedAt
                })));
            if (requiresPrequalification)
                Add(state, ProcurementAwardReadinessPrerequisiteGroup.Prequalification,
                    $"SUPPLIER_{partner.Id:N}_PREQUALIFIED",
                    supplier.Prequalification.Count != 0,
                    supplier.Prequalification.Count != 0
                        ? $"{partner.PartnerCode} has a current approved qualified-list entry."
                        : $"{partner.PartnerCode} has no current approved qualified-list entry.",
                    "Approve or renew the supplier prequalification.",
                    "ProcurementQualifiedListEntry",
                    supplier.Prequalification.FirstOrDefault()?.EntryId,
                    supplier.Prequalification.FirstOrDefault()?.IntegrityHash);
        }
        if (!requiresPrequalification)
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.Prequalification,
                "PREQUALIFICATION_NOT_REQUIRED",
                ProcurementAwardReadinessPrerequisiteStatus.NotApplicable,
                "The source does not require supplier prequalification.",
                null);
        if (recommendationAtUtc.HasValue &&
            partners.Any(item => item.UpdatedAt.HasValue &&
                                 item.UpdatedAt.Value > recommendationAtUtc.Value))
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.SupplierEligibility,
                "SUPPLIER_CHANGED_AFTER_RECOMMENDATION", false,
                "A recommended supplier changed after the recommendation was completed.",
                "Revalidate and re-approve the recommendation.");
    }

    private async Task AddSupplierRiskAsync(
        BusinessPartner partner,
        ICollection<string> errors,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var policies = await _unitOfWork.Repository<ProcurementConfigurationDecision>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.DecisionKey == "DEC-011" && !item.IsDeleted &&
                item.Profile.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
                item.Profile.EffectiveFrom <= now &&
                (!item.Profile.EffectiveTo.HasValue || item.Profile.EffectiveTo.Value >= now) &&
                item.Status == ProcurementConfigurationDecisionStatus.Approved &&
                item.ApprovalStatus == ProcurementConfigurationApprovalStatus.Approved &&
                item.EvidenceStatus == ProcurementConfigurationEvidenceStatus.Verified &&
                (!item.EffectiveFrom.HasValue || item.EffectiveFrom.Value <= now) &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now))
            .AsNoTracking().ToListAsync(cancellationToken);
        if (policies.Count == 0)
        {
            warnings.Add("No effective DEC-011 supplier-risk policy is configured; the standard approved, active, registered, and non-blacklisted supplier checks remain mandatory.");
            return;
        }
        if (policies.Count > 1)
        {
            errors.Add("Multiple effective DEC-011 supplier-risk policies are configured; resolve the ambiguous governance configuration before award.");
            return;
        }
        var policy = policies[0];
        var assessment = await _unitOfWork.Repository<ProcurementSupplierRiskAssessment>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id && !item.IsDeleted)
            .Include(item => item.Alerts.Where(alert => !alert.IsDeleted))
            .AsNoTracking().OrderByDescending(item => item.AssessedAtUtc)
            .ThenByDescending(item => item.AssessmentSequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (assessment is null)
        {
            errors.Add("A current supplier-risk and concentration assessment is required.");
            return;
        }
        var policyHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(policy.ValueJson)));
        if (assessment.PolicyDecisionId != policy.Id ||
            !string.Equals(assessment.PolicyValueHash, policyHash,
                StringComparison.OrdinalIgnoreCase))
            errors.Add("The supplier-risk assessment is bound to a stale DEC-011 policy.");
        if (assessment.NextReviewDueAtUtc <= now)
            errors.Add("The supplier-risk assessment has reached its policy-derived review date.");
        if (!assessment.DataComplete)
            errors.Add("The supplier-risk assessment is incomplete.");
        var breach = assessment.MinimumScoreBreached ||
            assessment.ConcentrationBreached ||
            assessment.SingleSourceDependency ||
            !assessment.DataComplete;
        var unresolved = assessment.Alerts.Any(item =>
            item.Status != ProcurementSupplierRiskAlertStatus.Resolved);
        if (assessment.EligibilityAction ==
                ProcurementSupplierRiskEligibilityAction.AwardHardStop && breach)
            errors.Add("DEC-011 applies an award hard stop to the current supplier-risk breach.");
        else if (assessment.EligibilityAction ==
                     ProcurementSupplierRiskEligibilityAction.EscalationRequired &&
                 unresolved)
            errors.Add("DEC-011 requires all current supplier-risk alerts to complete escalation before award.");
        else if (assessment.EligibilityAction ==
                     ProcurementSupplierRiskEligibilityAction.AlertOnly && breach)
            warnings.Add("DEC-011 records the current supplier-risk breach as alert-only.");
    }

    private async Task AddAwardVerificationAsync(
        ReadinessState state,
        Tender tender,
        DateTime? recommendationAtUtc,
        CancellationToken cancellationToken)
    {
        if (state.Recommendation.SubjectIds.Count != 1 ||
            state.Recommendation.BusinessPartnerIds.Count != 1)
        {
            Add(state,
                ProcurementAwardReadinessPrerequisiteGroup.VerificationAndDueDiligence,
                "VERIFICATION_RECOMMENDATION_AMBIGUOUS", false,
                "Award verification requires exactly one recommended tender bid and supplier.",
                "Resolve the recommendation before due-diligence completion.");
            return;
        }
        var verificationRows = await _unitOfWork.Repository<TenderAwardVerification>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderId == tender.Id &&
                !item.IsDeleted)
            .Include(item => item.Template).ThenInclude(item => item!.Items)
            .Include(item => item.Bidders).ThenInclude(item => item.ItemResults)
                .ThenInclude(item => item.ChecklistItem)
            .Include(item => item.Bidders).ThenInclude(item => item.ItemResults)
                .ThenInclude(item => item.Documents)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var completed = verificationRows.Where(item =>
            string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase)).ToList();
        if (verificationRows.Count == 0)
        {
            Add(state,
                ProcurementAwardReadinessPrerequisiteGroup.VerificationAndDueDiligence,
                "AWARD_VERIFICATION_NOT_CONFIGURED",
                ProcurementAwardReadinessPrerequisiteStatus.NotApplicable,
                "No award-verification checklist is configured for this tender; standard supplier eligibility and evaluation controls remain mandatory.",
                null);
            return;
        }
        if (completed.Count != 1)
        {
            Add(state,
                ProcurementAwardReadinessPrerequisiteGroup.VerificationAndDueDiligence,
                completed.Count == 0
                    ? "AWARD_VERIFICATION_INCOMPLETE"
                    : "AWARD_VERIFICATION_AMBIGUOUS",
                false,
                "Exactly one completed existing award-verification record is required.",
                "Complete or resolve the existing award-verification checklist.");
            return;
        }
        var verification = completed[0];
        var bidId = state.Recommendation.SubjectIds[0];
        var supplierId = state.Recommendation.BusinessPartnerIds[0];
        var bidders = verification.Bidders.Where(item =>
                !item.IsDeleted &&
                item.TenderBidId == bidId &&
                item.BusinessPartnerId == supplierId)
            .ToList();
        if (bidders.Count != 1)
        {
            Add(state,
                ProcurementAwardReadinessPrerequisiteGroup.VerificationAndDueDiligence,
                "AWARD_VERIFICATION_BIDDER_MISMATCH", false,
                "The completed verification does not identify the exact recommended bid and supplier.",
                "Verify the currently recommended bidder.");
            return;
        }
        var bidder = bidders[0];
        var requiredItems = bidder.ItemResults.Where(AwardVerificationEvidencePolicy.IsRequired).ToList();
        var requiredPassed = requiredItems.Count != 0 &&
                             requiredItems.All(item =>
                                 item.IsVerified &&
                                 item.Status == "Passed");
        var requiredEvidence = requiredItems.All(AwardVerificationEvidencePolicy.HasEvidence);
        var templateCurrent = verification.Template is { IsActive: true } &&
                              (!verification.CompletedDate.HasValue ||
                               (!verification.Template.UpdatedAt.HasValue ||
                                verification.Template.UpdatedAt <= verification.CompletedDate) &&
                               verification.Template.Items.Where(item => !item.IsDeleted)
                                   .All(item => !item.UpdatedAt.HasValue ||
                                                item.UpdatedAt <= verification.CompletedDate));
        var timely = verification.CompletedDate.HasValue &&
                     (!recommendationAtUtc.HasValue ||
                      verification.CompletedDate.Value >= recommendationAtUtc.Value);
        var passed = string.Equals(bidder.Status, "Passed", StringComparison.OrdinalIgnoreCase) &&
                     requiredPassed && requiredEvidence && templateCurrent && timely;
        var snapshotHash = ComputeVerificationHash(verification, bidder);
        state.Verifications.Add(new ProcurementAwardReadinessVerificationDto
        {
            VerificationId = verification.Id,
            VerificationBidderId = bidder.Id,
            TenderBidId = bidder.TenderBidId,
            BusinessPartnerId = bidder.BusinessPartnerId,
            TemplateId = verification.TemplateId,
            VerificationStatus = verification.Status,
            BidderStatus = bidder.Status,
            CompletedAtUtc = verification.CompletedDate,
            ItemResultIds = bidder.ItemResults.Where(item => !item.IsDeleted)
                .Select(item => item.Id).OrderBy(item => item).ToList(),
            DocumentIds = bidder.ItemResults.Where(item => !item.IsDeleted)
                .SelectMany(item => item.Documents.Where(document => !document.IsDeleted))
                .Select(item => item.Id).OrderBy(item => item).ToList(),
            SnapshotIntegrityHash = snapshotHash
        });
        Add(state, ProcurementAwardReadinessPrerequisiteGroup.VerificationAndDueDiligence,
            "AWARD_VERIFICATION_PASSED",
            passed,
            passed
                ? "The exact recommended bidder passed every required current checklist item with evidence."
                : "The recommended bidder verification is failed, incomplete, stale, or lacks required evidence.",
            "Complete a current passing verification for the recommended bidder.",
            "TenderAwardVerification", verification.Id, snapshotHash);
        AddEvidence(state, "AWARD_VERIFICATION", "Completed award verification",
            verification.Id, $"award-verification:{verification.Id:N}");
        foreach (var document in bidder.ItemResults.Where(item => !item.IsDeleted)
                     .SelectMany(item => item.Documents.Where(document => !document.IsDeleted)))
            AddEvidence(state, "AWARD_VERIFICATION_DOCUMENT", document.FileName,
                document.Id, document.FilePath);
        if (verification.CompletedDate.HasValue)
            state.Timeline.Add(Timeline("AwardVerificationCompleted",
                verification.CompletedDate.Value, verification.CompletedById,
                $"award-verification:{verification.Id:N}", snapshotHash));
    }

    private void FinalizeState(ReadinessState state, SourceHeader source)
    {
        foreach (var evidence in state.Evidence)
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.Evidence,
                $"EVIDENCE_{evidence.RequirementKey}_{evidence.ReferenceId?.ToString("N") ?? "EXTERNAL"}",
                evidence.IsAvailable,
                evidence.IsAvailable
                    ? $"{evidence.Label} is retained."
                    : $"{evidence.Label} is missing.",
                evidence.IsAvailable ? null : "Attach the required source-owned evidence.",
                "Evidence", evidence.ReferenceId);
        if (state.Evidence.Count == 0)
            Add(state, ProcurementAwardReadinessPrerequisiteGroup.Evidence,
                "EVIDENCE_MISSING", false,
                "No source-owned award evidence could be derived.",
                "Complete evaluation, workflow, and diligence evidence.");

        foreach (var group in Enum.GetValues<ProcurementAwardReadinessPrerequisiteGroup>())
        {
            if (state.Items.ContainsKey(group)) continue;
            Add(state, group, $"{group.ToString().ToUpperInvariant()}_NOT_APPLICABLE",
                ProcurementAwardReadinessPrerequisiteStatus.NotApplicable,
                $"{group} is not applicable to this source.", null);
        }
        state.Groups = state.Items.OrderBy(item => item.Key)
            .Select(pair => new ProcurementAwardReadinessPrerequisiteGroupDto
            {
                Group = pair.Key,
                Status = pair.Value.Any(item =>
                    item.Status == ProcurementAwardReadinessPrerequisiteStatus.Failed)
                    ? ProcurementAwardReadinessPrerequisiteStatus.Failed
                    : pair.Value.All(item =>
                        item.Status == ProcurementAwardReadinessPrerequisiteStatus.NotApplicable)
                        ? ProcurementAwardReadinessPrerequisiteStatus.NotApplicable
                        : ProcurementAwardReadinessPrerequisiteStatus.Passed,
                Items = pair.Value
            }).ToList();
        state.BlockedReasons = state.Groups.SelectMany(item => item.Items)
            .Where(item => item.Status == ProcurementAwardReadinessPrerequisiteStatus.Failed)
            .Select(item => $"{item.Code}: {item.Message}")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        state.Timeline = state.Timeline.OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.EventType).ToList();
        state.SourceIntegrityHash = ComputeHash(Serialize(new
        {
            schemaVersion = "tdc.award-readiness-source.v1",
            source.Type,
            source.Id,
            source.Reference,
            state.Method,
            state.Recommendation,
            state.Evaluations,
            state.Suppliers,
            state.Verifications,
            authority = new
            {
                state.Authority.MethodRuleId,
                state.Authority.MethodRuleCode,
                state.Authority.AuthorityRouteId,
                state.Authority.AuthorityRouteReference,
                state.Authority.WorkflowDefinitionId,
                state.Authority.WorkflowInstanceId,
                state.Authority.WorkflowStatus,
                state.Authority.ApprovalReference,
                sourceOwnedApprovedAtUtc = source.IsLegacyTender
                    ? null
                    : state.Authority.ApprovedAtUtc,
                sourceOwnedApprovedByUserId = source.IsLegacyTender
                    ? null
                    : state.Authority.ApprovedByUserId
            },
            state.Evidence,
            state.Groups,
            state.Timeline
        }));
    }

    private async Task<SourceHeader> ResolveSourceHeaderAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        switch (sourceType)
        {
            case ProcurementAwardReadinessSourceType.RequestForQuotation:
            {
                var source = await _unitOfWork.Repository<RequestForQuotation>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == sourceId &&
                        !item.IsDeleted)
                    .AsNoTracking()
                    .Select(item => new { item.Id, item.RfqNumber })
                    .SingleOrDefaultAsync(cancellationToken);
                return source is null
                    ? throw NotFound("RFQ_NOT_FOUND", "The RFQ was not found in the current tenant.")
                    : new SourceHeader(sourceType, source.Id, source.RfqNumber, false);
            }
            case ProcurementAwardReadinessSourceType.Tender:
            case ProcurementAwardReadinessSourceType.ExceptionalSourcing:
            {
                var source = await _unitOfWork.Repository<Tender>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == sourceId &&
                        !item.IsDeleted)
                    .AsNoTracking()
                    .Select(item => new { item.Id, item.TenderNumber })
                    .SingleOrDefaultAsync(cancellationToken);
                if (source is null)
                    throw NotFound("TENDER_NOT_FOUND",
                        "The tender was not found in the current tenant.");
                var formalCount = await _unitOfWork.Repository<ProcurementTenderControl>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.TenderId == sourceId &&
                        !item.IsDeleted)
                    .CountAsync(cancellationToken);
                var exceptionalCount = await _unitOfWork
                    .Repository<ProcurementExceptionalSourcingControl>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.TenderId == sourceId &&
                        !item.IsDeleted)
                    .CountAsync(cancellationToken);
                if (sourceType == ProcurementAwardReadinessSourceType.Tender &&
                    formalCount == 0 && exceptionalCount != 0)
                    throw Validation("AWARD_READINESS_SOURCE_TYPE_MISMATCH",
                        "This tender is controlled by exceptional sourcing; use ExceptionalSourcing.");
                if (sourceType == ProcurementAwardReadinessSourceType.ExceptionalSourcing &&
                    exceptionalCount == 0)
                    throw NotFound("EXCEPTIONAL_CONTROL_NOT_FOUND",
                        "No exceptional-sourcing control exists for this tender.");
                return new SourceHeader(
                    sourceType,
                    source.Id,
                    source.TenderNumber,
                    sourceType == ProcurementAwardReadinessSourceType.Tender &&
                    formalCount == 0);
            }
            default:
                throw Validation("AWARD_READINESS_SOURCE_TYPE_INVALID",
                    "Unsupported award-readiness source type.");
        }
    }

    private async Task<Tender> LoadTenderAsync(
        Guid tenderId,
        CancellationToken cancellationToken) =>
        await _unitOfWork.Repository<Tender>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == tenderId &&
                !item.IsDeleted)
            .Include(item => item.SourcingCase)
            .AsNoTracking()
            .SingleAsync(cancellationToken);

    private async Task<TenderBid?> LoadRecommendedBidAsync(
        Guid tenderId,
        Guid bidId,
        CancellationToken cancellationToken) =>
        await _unitOfWork.Repository<TenderBid>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderId == tenderId &&
                item.Id == bidId &&
                !item.IsDeleted &&
                item.Status != "Withdrawn" &&
                item.Status != "Rejected")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<string?> TryGetCurrentSourceHashAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var source = await ResolveSourceHeaderAsync(sourceType, sourceId, cancellationToken);
            var state = await BuildStateAsync(source, legacyApprovalAuthorized: true, cancellationToken);
            return state.SourceIntegrityHash;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task EnforceEvaluatorAwardApproverSodAsync(
        SourceHeader source,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var status = await EvaluateEvaluatorAwardApproverSodAsync(
            source, correlationId, cancellationToken);
        if (!status.Allowed)
            throw new ProcurementAwardReadinessAuthorizationException(
                status.Message);
    }

    private async Task<ProcurementEvaluatorAwardApproverSodStatusDto>
        EvaluateEvaluatorAwardApproverSodAsync(
            SourceHeader source,
            string correlationId,
            CancellationToken cancellationToken)
    {
        var lineage = await ResolveEvaluatorAwardLineageAsync(
            source, cancellationToken);
        var evaluatorIds = lineage.Items
            .Select(item => item.EvaluatorUserId)
            .Where(item => item != Guid.Empty)
            .Distinct()
            .OrderBy(item => item)
            .ToList();
        if (evaluatorIds.Count == 0)
            throw Validation(
                "AWARD_READINESS_EVALUATOR_LINEAGE_MISSING",
                "No exact evaluator lineage can be resolved for this source, so evaluator-versus-award-approver separation cannot be evaluated.");

        var independentActors = lineage.ApprovalActorUserIds
            .Where(item => item != Guid.Empty && !evaluatorIds.Contains(item))
            .Distinct()
            .OrderBy(item => item)
            .ToList();
        var latest = await DecisionQuery()
            .AsNoTracking()
            .Where(item => item.SourceType == source.Type &&
                           item.SourceId == source.Id)
            .OrderByDescending(item => item.DecisionSequence)
            .FirstOrDefaultAsync(cancellationToken);
        bool? latestIsCurrent = null;
        if (latest is not null)
        {
            var currentHash = await TryGetCurrentSourceHashAsync(
                source.Type, source.Id, cancellationToken);
            latestIsCurrent = string.Equals(
                latest.SourceIntegrityHash,
                currentHash,
                StringComparison.OrdinalIgnoreCase);
        }

        var sod = await _sodGuard.EnforceAsync(
            new ProcurementSodGuardRequest
            {
                ControlCode = EvaluatorAwardApproverSodControl,
                SourceType = EventType,
                SourceReference = source.Reference,
                ProhibitedActorUserIds = evaluatorIds,
                IndependentActorUserIds = independentActors,
                RequireSoleActorConflict = true
            },
            correlationId,
            cancellationToken);
        var status = new ProcurementEvaluatorAwardApproverSodStatusDto
        {
            SourceType = source.Type,
            SourceId = source.Id,
            SourceReference = source.Reference,
            Allowed = sod.Allowed,
            Code = sod.Code,
            Message = sod.Message,
            CurrentActorUserId = _currentUser.UserId,
            CurrentActorName = ActorName(),
            CurrentActorRoles = _currentUser.Roles
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            EvaluatorUserIds = evaluatorIds,
            IndependentApprovalActorUserIds = independentActors,
            EvaluatorLineage = lineage.Items
                .OrderBy(item => item.Family, StringComparer.Ordinal)
                .ThenBy(item => item.Phase)
                .ThenBy(item => item.AppointmentId)
                .ThenBy(item => item.Attempt)
                .ToList(),
            ReadinessDecisionId = latest?.Id,
            ReadinessDecisionSequence = latest?.DecisionSequence,
            ReadinessSourceIntegrityHash = latest?.SourceIntegrityHash,
            ReadinessIntegrityHash = latest?.IntegrityHash,
            ReadinessDecisionIsCurrent = latestIsCurrent,
            SodDecisionId = sod.DecisionId,
            SodControlCode = string.IsNullOrWhiteSpace(sod.ControlCode)
                ? EvaluatorAwardApproverSodControl
                : sod.ControlCode,
            SodPolicySetId = sod.PolicySetId,
            SodPolicyCode = sod.PolicyCode,
            SodPolicyVersion = sod.PolicyVersion,
            SodRuleId = sod.RuleId,
            SodRuleCode = sod.RuleCode,
            SodSourceDecisionKey = sod.SourceDecisionKey,
            SourceMethodRuleId = lineage.MethodRuleId,
            SourceMethodRuleCode = lineage.MethodRuleCode,
            CorrelationId = correlationId,
            EvaluatedAtUtc = sod.EvaluatedAtUtc == default
                ? DateTime.UtcNow
                : sod.EvaluatedAtUtc
        };
        await RecordEvaluatorAwardApproverSodAsync(
            status, cancellationToken);
        return status;
    }

    private async Task<EvaluatorAwardLineageState>
        ResolveEvaluatorAwardLineageAsync(
            SourceHeader source,
            CancellationToken cancellationToken)
    {
        var state = new EvaluatorAwardLineageState();
        switch (source.Type)
        {
            case ProcurementAwardReadinessSourceType.RequestForQuotation:
                await ResolveRfqEvaluatorLineageAsync(
                    source, state, cancellationToken);
                break;
            case ProcurementAwardReadinessSourceType.ExceptionalSourcing:
                await ResolveExceptionalEvaluatorLineageAsync(
                    source, state, cancellationToken);
                break;
            case ProcurementAwardReadinessSourceType.Tender:
                await ResolveTenderEvaluatorLineageAsync(
                    source, state, cancellationToken);
                break;
            default:
                throw Validation(
                    "AWARD_READINESS_SOURCE_TYPE_INVALID",
                    "Unsupported award-readiness source type.");
        }

        await AddCommitteeScoreEvaluatorLineageAsync(
            source, state, cancellationToken);
        return state;
    }

    private async Task ResolveRfqEvaluatorLineageAsync(
        SourceHeader source,
        EvaluatorAwardLineageState state,
        CancellationToken cancellationToken)
    {
        var evaluations = await _unitOfWork
            .Repository<ProcurementRfqEvaluation>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.RfqId == source.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (evaluations.Count > 1)
            throw AmbiguousEvaluatorLineage(
                "More than one current RFQ evaluation exists.");
        var evaluation = evaluations.SingleOrDefault();
        if (evaluation is null) return;

        state.MethodRuleId = evaluation.MethodRuleId;
        state.MethodRuleCode = evaluation.MethodRuleCode;
        state.ValidEvaluationIds.Add(evaluation.Id);
        if (evaluation.SubmittedByUserId is { } evaluatorId)
            state.Items.Add(DirectEvaluatorLineage(
                "RequestForQuotationEvaluation",
                evaluation.Id,
                evaluatorId,
                ProcurementEvaluationPhase.Combined,
                evaluation.SubmittedAtUtc,
                evaluation.IntegrityHash));
        await AddAuthorityActorsAsync(
            state,
            evaluation.WorkflowInstanceId,
            evaluation.ApprovedByUserId,
            ParseGuids(evaluation.ApprovalActorsJson),
            cancellationToken);
    }

    private async Task ResolveTenderEvaluatorLineageAsync(
        SourceHeader source,
        EvaluatorAwardLineageState state,
        CancellationToken cancellationToken)
    {
        var controls = await _unitOfWork
            .Repository<ProcurementTenderControl>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderId == source.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (controls.Count > 1)
            throw AmbiguousEvaluatorLineage(
                "More than one current formal tender control exists.");
        if (controls.Count == 1)
        {
            var control = controls[0];
            state.MethodRuleId = control.MethodRuleId;
            state.MethodRuleCode = control.MethodRuleCode;
            AddProjectionEvaluator(
                state,
                "FormalTenderTechnicalEvaluation",
                control.Id,
                ProcurementEvaluationPhase.Technical,
                control.TechnicalEvaluationSnapshotJson,
                control.TechnicalEvaluatedAtUtc,
                control.TechnicalEvaluationIntegrityHash);
            AddProjectionEvaluator(
                state,
                "FormalTenderFinancialEvaluation",
                control.Id,
                ProcurementEvaluationPhase.Financial,
                control.FinancialEvaluationSnapshotJson,
                control.FinancialEvaluatedAtUtc,
                control.FinancialEvaluationIntegrityHash);
            await AddAuthorityActorsAsync(
                state,
                control.WorkflowInstanceId,
                control.ApprovedById,
                ParseGuids(control.ApprovalActorsJson),
                cancellationToken);
            return;
        }

        var evaluations = await _unitOfWork.Repository<TenderEvaluation>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderBid.TenderId == source.Id &&
                !item.IsDeleted)
            .Include(item => item.TenderBid)
            .Include(item => item.TenderEvaluator)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        foreach (var evaluation in evaluations.Where(item =>
                     !string.Equals(item.Status, "Draft",
                         StringComparison.OrdinalIgnoreCase)))
        {
            if (evaluation.TenderEvaluator is null ||
                evaluation.TenderEvaluator.IsDeleted ||
                evaluation.TenderEvaluator.TenantId != _currentUser.TenantId ||
                evaluation.TenderEvaluator.TenderId != source.Id ||
                evaluation.TenderEvaluatorId != evaluation.TenderEvaluator.Id ||
                evaluation.TenderEvaluator.UserId == Guid.Empty ||
                evaluation.TenderBid.IsDeleted ||
                evaluation.TenderBid.TenantId != _currentUser.TenantId)
                throw AmbiguousEvaluatorLineage(
                    "A legacy tender evaluation has missing or foreign evaluator lineage.");
            state.ValidEvaluationIds.Add(evaluation.Id);
            state.LegacyEvaluations[evaluation.Id] = evaluation;
            state.Items.Add(DirectEvaluatorLineage(
                "LegacyTenderEvaluation",
                evaluation.Id,
                evaluation.TenderEvaluator.UserId,
                ProcurementEvaluationPhase.Combined,
                evaluation.SubmittedDate ?? evaluation.EvaluationDate,
                null));
        }
    }

    private async Task ResolveExceptionalEvaluatorLineageAsync(
        SourceHeader source,
        EvaluatorAwardLineageState state,
        CancellationToken cancellationToken)
    {
        var controls = await _unitOfWork
            .Repository<ProcurementExceptionalSourcingControl>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderId == source.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (controls.Count != 1)
            throw AmbiguousEvaluatorLineage(
                "Exactly one exceptional-sourcing control is required.");
        var control = controls[0];
        state.MethodRuleId = control.MethodRuleId;
        state.MethodRuleCode = control.MethodRuleCode;
        if (control.RecommendedById is { } evaluatorId)
            state.Items.Add(DirectEvaluatorLineage(
                "ExceptionalNegotiatedRecommendation",
                control.Id,
                evaluatorId,
                ProcurementEvaluationPhase.Combined,
                control.RecommendedAtUtc,
                control.IntegrityHash));
        await AddAuthorityActorsAsync(
            state,
            control.WorkflowInstanceId,
            control.ApprovedById,
            ParseGuids(control.ApprovalActorsJson),
            cancellationToken);
    }

    private async Task AddCommitteeScoreEvaluatorLineageAsync(
        SourceHeader source,
        EvaluatorAwardLineageState state,
        CancellationToken cancellationToken)
    {
        var evaluationSourceType =
            source.Type == ProcurementAwardReadinessSourceType.RequestForQuotation
                ? ProcurementEvaluationSourceType.RequestForQuotation
                : ProcurementEvaluationSourceType.Tender;
        var sheets = await _unitOfWork
            .Repository<ProcurementEvaluationScoreSheet>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.CommitteeControl.SourceType == evaluationSourceType &&
                item.CommitteeControl.SourceId == source.Id &&
                !item.IsDeleted)
            .Include(item => item.CommitteeControl)
            .Include(item => item.Appointment)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (sheets.Count == 0) return;

        if (sheets.Any(item =>
                item.CommitteeControl is null ||
                item.Appointment is null ||
                item.CommitteeControl.TenantId != _currentUser.TenantId ||
                item.Appointment.TenantId != _currentUser.TenantId ||
                item.Appointment.CommitteeControlId !=
                item.CommitteeControlId ||
                item.Appointment.UserId != item.SubmittedByUserId ||
                !ScoreSubjectMatches(source, state, item)))
            throw AmbiguousEvaluatorLineage(
                "Committee score-sheet subject, appointment, submitter, or tenant lineage does not match the award source.");

        var groups = sheets.GroupBy(item => new
        {
            item.CommitteeControlId,
            item.AppointmentId,
            item.Phase,
            item.ScoreSubjectType,
            item.ScoreSubjectId
        });
        foreach (var group in groups)
        {
            var ordered = group
                .OrderBy(item => item.Attempt)
                .ThenBy(item => item.SubmittedAtUtc)
                .ToList();
            var attempts = ordered.Select(item => item.Attempt).ToList();
            if (attempts.Count != attempts.Distinct().Count() ||
                !attempts.SequenceEqual(
                    Enumerable.Range(1, attempts.Count)))
                throw AmbiguousEvaluatorLineage(
                    "Committee score attempts are duplicated or non-contiguous.");
            var retainedId = ordered
                .Where(item =>
                    item.Status ==
                    ProcurementEvaluationScoreSheetStatus.Locked)
                .OrderByDescending(item => item.Attempt)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefault();
            foreach (var sheet in ordered)
            {
                state.Items.Add(new ProcurementAwardEvaluatorLineageDto
                {
                    Family = source.Type ==
                             ProcurementAwardReadinessSourceType.RequestForQuotation
                        ? "RequestForQuotationCommitteeScore"
                        : source.Type ==
                          ProcurementAwardReadinessSourceType.ExceptionalSourcing
                            ? "ExceptionalCommitteeScore"
                            : source.IsLegacyTender
                                ? "LegacyTenderCommitteeScore"
                                : "FormalTenderCommitteeScore",
                    EvaluationId = sheet.ScoreSubjectType == "TenderEvaluation"
                        ? TryGetJsonGuid(sheet.ScoreSnapshotJson, "evaluationId")
                        : sheet.ScoreSubjectType == "ProcurementRfqEvaluation"
                            ? sheet.ScoreSubjectId
                            : null,
                    CommitteeControlId = sheet.CommitteeControlId,
                    AppointmentId = sheet.AppointmentId,
                    ScoreSheetId = sheet.Id,
                    ScoreSubjectType = sheet.ScoreSubjectType,
                    ScoreSubjectId = sheet.ScoreSubjectId,
                    Phase = sheet.Phase,
                    Attempt = sheet.Attempt,
                    ScoreStatus = sheet.Status,
                    IsRetainedAttempt = retainedId == sheet.Id,
                    IsRecalledAttempt =
                        sheet.Status ==
                        ProcurementEvaluationScoreSheetStatus.Recalled,
                    EvaluatorUserId = sheet.SubmittedByUserId,
                    EvaluatedAtUtc = sheet.SubmittedAtUtc,
                    IntegrityHash = sheet.IntegrityHash
                });
            }
        }
    }

    private static bool ScoreSubjectMatches(
        SourceHeader source,
        EvaluatorAwardLineageState state,
        ProcurementEvaluationScoreSheet sheet)
    {
        if (source.Type ==
            ProcurementAwardReadinessSourceType.RequestForQuotation)
            return sheet.ScoreSubjectType ==
                   "ProcurementRfqEvaluation" &&
                   state.ValidEvaluationIds.Contains(
                       sheet.ScoreSubjectId);
        if (source.IsLegacyTender)
            return TryGetJsonGuid(sheet.ScoreSnapshotJson, "evaluationId") is { } evaluationId &&
                   state.LegacyEvaluations.TryGetValue(evaluationId, out var evaluation) &&
                   LegacyScoreMatchesEvaluation(evaluation, sheet);
        return sheet.ScoreSubjectType ==
               "ProcurementTenderControl" &&
               sheet.ScoreSubjectId == source.Id;
    }

    private static bool LegacyScoreMatchesEvaluation(
        TenderEvaluation evaluation,
        ProcurementEvaluationScoreSheet sheet) =>
        sheet.ScoreSubjectType == "TenderEvaluation" &&
        sheet.Phase == ProcurementEvaluationPhase.Combined &&
        sheet.ScoreSubjectId == evaluation.TenderBidId &&
        sheet.TenantId == evaluation.TenantId &&
        sheet.Appointment is not null &&
        sheet.Appointment.TenantId == evaluation.TenantId &&
        sheet.Appointment.CommitteeControlId == sheet.CommitteeControlId &&
        sheet.Appointment.UserId == sheet.SubmittedByUserId &&
        evaluation.TenderEvaluator is not null &&
        evaluation.TenderEvaluator.TenantId == evaluation.TenantId &&
        evaluation.TenderEvaluator.UserId == sheet.SubmittedByUserId &&
        TryGetJsonGuid(sheet.ScoreSnapshotJson, "evaluationId") == evaluation.Id &&
        TryGetJsonGuid(sheet.ScoreSnapshotJson, "TenderBidId") == evaluation.TenderBidId &&
        TryGetJsonGuid(sheet.ScoreSnapshotJson, "TenderEvaluatorId") == evaluation.TenderEvaluatorId &&
        TryGetJsonGuid(sheet.ScoreSnapshotJson, "evaluatorUserId") == sheet.SubmittedByUserId;

    private static bool LegacyScoreSnapshotMatches(
        TenderEvaluation evaluation,
        ProcurementEvaluationScoreSheet sheet)
    {
        var submittedAtUtc = evaluation.SubmittedDate ?? evaluation.UpdatedAt ?? evaluation.EvaluationDate;
        // SQL datetime2 retains the UTC instant, but not DateTime.Kind. Restore
        // the writer's UTC designation without converting the stored value.
        if (submittedAtUtc.Kind == DateTimeKind.Unspecified)
            submittedAtUtc = DateTime.SpecifyKind(submittedAtUtc, DateTimeKind.Utc);
        var expected = TenderEvaluationService.BuildLegacyScoreSnapshot(evaluation, submittedAtUtc);
        try
        {
            using var actualDocument = JsonDocument.Parse(sheet.ScoreSnapshotJson);
            using var expectedDocument = JsonDocument.Parse(expected);
            return ScoreSnapshotValuesEqual(actualDocument.RootElement, expectedDocument.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ScoreSnapshotValuesEqual(JsonElement actual, JsonElement expected)
    {
        if (actual.ValueKind != expected.ValueKind) return false;
        switch (actual.ValueKind)
        {
            case JsonValueKind.Object:
                var actualProperties = actual.EnumerateObject().ToList();
                var expectedProperties = expected.EnumerateObject().ToList();
                return actualProperties.Count == expectedProperties.Count &&
                       actualProperties.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() == actualProperties.Count &&
                       expectedProperties.All(property =>
                           actual.TryGetProperty(property.Name, out var value) &&
                           ScoreSnapshotValuesEqual(value, property.Value));
            case JsonValueKind.Array:
                var actualItems = actual.EnumerateArray().ToList();
                var expectedItems = expected.EnumerateArray().ToList();
                return actualItems.Count == expectedItems.Count &&
                       actualItems.Zip(expectedItems).All(pair => ScoreSnapshotValuesEqual(pair.First, pair.Second));
            case JsonValueKind.Number:
                // Decimal scale may change on a database round trip. Values
                // must remain exactly equal; no floating-point tolerance.
                return actual.TryGetDecimal(out var actualValue) &&
                       expected.TryGetDecimal(out var expectedValue) &&
                       actualValue == expectedValue;
            case JsonValueKind.String:
                return actual.GetString() == expected.GetString();
            default:
                return actual.GetRawText() == expected.GetRawText();
        }
    }

    private async Task AddAuthorityActorsAsync(
        EvaluatorAwardLineageState state,
        Guid? workflowInstanceId,
        Guid? approvedById,
        IReadOnlyCollection<Guid> declaredActorIds,
        CancellationToken cancellationToken)
    {
        var declared = declaredActorIds
            .Where(item => item != Guid.Empty)
            .ToHashSet();
        if (approvedById.HasValue &&
            approvedById.Value != Guid.Empty)
            declared.Add(approvedById.Value);
        if (!workflowInstanceId.HasValue)
        {
            state.ApprovalActorUserIds.UnionWith(declared);
            return;
        }

        var workflow = await _unitOfWork.Repository<WorkflowInstance>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == workflowInstanceId.Value &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        var actual = await _unitOfWork.Repository<WorkflowApproval>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.StepInstance.WorkflowInstanceId ==
                workflowInstanceId.Value &&
                !item.IsDeleted &&
                item.Status == WorkflowApprovalStatus.Approved &&
                item.ProcessedDate.HasValue)
            .AsNoTracking()
            .Select(item => item.ProcessedById ?? item.ApproverId)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (workflow is null || actual.Count == 0 ||
            declared.Any(item => !actual.Contains(item)))
            throw AmbiguousEvaluatorLineage(
                "Authority approval actors do not match the exact completed workflow lineage.");
        state.ApprovalActorUserIds.UnionWith(actual);
    }

    private static void AddProjectionEvaluator(
        EvaluatorAwardLineageState state,
        string family,
        Guid evaluationId,
        ProcurementEvaluationPhase phase,
        string? snapshotJson,
        DateTime? evaluatedAtUtc,
        string? integrityHash)
    {
        var evaluatorId = TryGetJsonGuid(
            snapshotJson, "evaluatorUserId");
        if (!evaluatorId.HasValue) return;
        state.Items.Add(DirectEvaluatorLineage(
            family,
            evaluationId,
            evaluatorId.Value,
            phase,
            evaluatedAtUtc,
            integrityHash));
    }

    private static ProcurementAwardEvaluatorLineageDto
        DirectEvaluatorLineage(
            string family,
            Guid evaluationId,
            Guid evaluatorId,
            ProcurementEvaluationPhase phase,
            DateTime? evaluatedAtUtc,
            string? integrityHash) =>
        new()
        {
            Family = family,
            EvaluationId = evaluationId,
            Phase = phase,
            EvaluatorUserId = evaluatorId,
            EvaluatedAtUtc = evaluatedAtUtc,
            IntegrityHash = integrityHash
        };

    private async Task RecordEvaluatorAwardApproverSodAsync(
        ProcurementEvaluatorAwardApproverSodStatusDto status,
        CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(
            new ProcurementControlEventWriteRequest
            {
                EventKey = ProcurementControlEventKey.Create(
                    "award-readiness-evaluator-sod",
                    _currentUser.TenantId,
                    status.SourceType,
                    status.SourceId,
                    status.CurrentActorUserId,
                    status.CorrelationId),
                EventType = "ProcurementAwardEvaluatorSodDecision",
                Action = "EvaluatorAwardApproverCheck",
                Result = status.Allowed
                    ? ProcurementControlEventResult.Allowed
                    : ProcurementControlEventResult.Denied,
                RuleCode = status.SodRuleCode ??
                           status.SodControlCode,
                RuleId = status.SodRuleId,
                RuleVersion = status.SodPolicyVersion?.ToString(),
                DecisionKeys = ["DEC-004"],
                SourceType = status.SourceType.ToString(),
                SourceId = status.SourceId,
                SourceReference = status.SourceReference,
                Reason = status.Message,
                InputValues = new
                {
                    status.CurrentActorUserId,
                    status.CurrentActorRoles,
                    status.EvaluatorUserIds,
                    status.IndependentApprovalActorUserIds,
                    status.EvaluatorLineage,
                    status.SourceMethodRuleId,
                    status.SourceMethodRuleCode
                },
                ResultValues = new
                {
                    status.Allowed,
                    status.Code,
                    status.SodDecisionId,
                    status.SodControlCode,
                    status.SodPolicySetId,
                    status.SodPolicyCode,
                    status.SodPolicyVersion,
                    status.SodRuleId,
                    status.SodRuleCode,
                    status.SodSourceDecisionKey,
                    status.ReadinessDecisionId,
                    status.ReadinessDecisionSequence,
                    status.ReadinessSourceIntegrityHash,
                    status.ReadinessIntegrityHash,
                    status.ReadinessDecisionIsCurrent
                },
                CorrelationId = status.CorrelationId,
                OccurredAtUtc = status.EvaluatedAtUtc
            },
            cancellationToken);
    }

    private static ProcurementAwardReadinessValidationException
        AmbiguousEvaluatorLineage(string message) =>
        Validation(
            "AWARD_READINESS_EVALUATOR_LINEAGE_AMBIGUOUS",
            message);

    private async Task EnforceAwardSodAsync(
        SourceHeader source,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var prohibited = new HashSet<Guid>();
        if (source.Type == ProcurementAwardReadinessSourceType.RequestForQuotation)
        {
            var sourceCreator = await _unitOfWork.Repository<RequestForQuotation>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == source.Id &&
                    !item.IsDeleted)
                .Select(item => item.CreatedById)
                .SingleAsync(cancellationToken);
            if (sourceCreator.HasValue) prohibited.Add(sourceCreator.Value);
        }
        else
        {
            var suppliers = await ResolveRecommendedSupplierIdsAsync(
                source, cancellationToken);
            if (suppliers.Count == 0 && source.IsLegacyTender)
            {
                // A draft or otherwise incomplete legacy evaluation has no award candidate yet.
                // The readiness evaluation below remains fail closed on its recommendation and
                // evaluation gates; supplier-controller SOD becomes applicable only after a
                // completed immutable recommendation exists.
                return;
            }
            var supplierControllers = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    suppliers.Contains(item.Id) &&
                    !item.IsDeleted)
                .AsNoTracking()
                .Select(item => item.ApprovedById ?? item.CreatedById)
                .ToListAsync(cancellationToken);
            foreach (var actor in supplierControllers.Where(item => item.HasValue))
                prohibited.Add(actor!.Value);
        }
        if (prohibited.Count == 0)
            throw Validation(
                "AWARD_READINESS_SOD_LINEAGE_MISSING",
                "The required prior participant lineage is missing, so separation of duties cannot be evaluated.");
        var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = source.Type == ProcurementAwardReadinessSourceType.RequestForQuotation
                ? "SOD-INITIATOR-APPROVER"
                : "SOD-SUPPLIER-CONTROLLER-AWARD",
            SourceType = "ProcurementAwardReadiness",
            SourceReference = source.Reference,
            ProhibitedActorUserIds = prohibited.ToList()
        }, correlationId, cancellationToken);
        if (!sod.Allowed)
            throw new ProcurementAwardReadinessAuthorizationException(sod.Message);
    }

    private async Task<List<Guid>> ResolveRecommendedSupplierIdsAsync(
        SourceHeader source,
        CancellationToken cancellationToken)
    {
        if (source.Type == ProcurementAwardReadinessSourceType.ExceptionalSourcing)
        {
            var bidId = await _unitOfWork.Repository<ProcurementExceptionalSourcingControl>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.TenderId == source.Id &&
                    !item.IsDeleted)
                .Select(item => item.RecommendedBidId)
                .SingleOrDefaultAsync(cancellationToken);
            return bidId.HasValue
                ? await _unitOfWork.Repository<TenderBid>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == bidId.Value &&
                        item.TenderId == source.Id &&
                        !item.IsDeleted)
                    .Select(item => item.BusinessPartnerId)
                    .ToListAsync(cancellationToken)
                : [];
        }
        var formalBidId = await _unitOfWork.Repository<ProcurementTenderControl>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderId == source.Id &&
                !item.IsDeleted)
            .Select(item => item.RecommendedBidId)
            .SingleOrDefaultAsync(cancellationToken);
        if (formalBidId.HasValue)
            return await _unitOfWork.Repository<TenderBid>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == formalBidId.Value &&
                    item.TenderId == source.Id &&
                    !item.IsDeleted)
                .Select(item => item.BusinessPartnerId)
                .ToListAsync(cancellationToken);
        return await _unitOfWork.Repository<TenderEvaluation>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderBid.TenderId == source.Id &&
                item.IsRecommended &&
                (item.Status == "Submitted" || item.Status == "Approved") &&
                !item.IsDeleted)
            .Select(item => item.TenderBid.BusinessPartnerId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static bool IsCompletedLegacyEvaluation(TenderEvaluation evaluation) =>
        string.Equals(evaluation.Status, "Submitted", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(evaluation.Status, "Approved", StringComparison.OrdinalIgnoreCase);

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = EventType,
                SourceReference = sourceReference
            },
            correlationId,
            cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementAwardReadinessAuthorizationException(decision.Message);
    }

    private async Task RecordAsync(
        ProcurementAwardReadinessDto decision,
        CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "award-readiness",
                _currentUser.TenantId,
                decision.SourceType,
                decision.SourceId,
                decision.IdempotencyKey),
            EventType = EventType,
            Action = decision.IsReady ? "ReadinessAllowed" : "ReadinessBlocked",
            Result = decision.IsReady
                ? ProcurementControlEventResult.Allowed
                : ProcurementControlEventResult.Denied,
            RuleCode = decision.Authority.MethodRuleCode,
            RuleId = decision.Authority.MethodRuleId,
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = decision.SourceType.ToString(),
            SourceId = decision.SourceId,
            SourceReference = decision.SourceReference,
            Reason = decision.IsReady
                ? "All server-derived recommendation and award prerequisites are current."
                : string.Join(" ", decision.BlockedReasons),
            InputValues = new
            {
                decision.IdempotencyKey,
                decision.Recommendation.SubjectIds,
                decision.Recommendation.BusinessPartnerIds
            },
            ResultValues = new
            {
                decision.Id,
                decision.DecisionSequence,
                decision.Status,
                decision.SourceIntegrityHash,
                decision.IntegrityHash
            },
            CorrelationId = decision.CorrelationId,
            OccurredAtUtc = decision.EvaluatedAtUtc,
            Evidence = decision.Evidence.Where(item => item.IsAvailable)
                .OrderByDescending(item => item.ReferenceId.HasValue)
                .GroupBy(
                    item => item.Reference?.Trim() ?? $"id:{item.ReferenceId:N}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(item => new ProcurementControlEventEvidenceReference
                {
                    ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                    ReferenceId = item.ReferenceId,
                    Reference = item.Reference,
                    Label = item.Label,
                    RequirementKey = item.RequirementKey
                }).ToList()
        }, cancellationToken);
    }

    private IQueryable<ProcurementAwardReadinessDecision> DecisionQuery() =>
        Decisions.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private static ProcurementAwardReadinessDto Map(
        ProcurementAwardReadinessDecision entity,
        bool isCurrent)
    {
        var recommendation = Deserialize<ProcurementAwardReadinessRecommendationDto>(
            entity.RecommendationSnapshotJson) ?? new();
        var evaluations = Deserialize<List<ProcurementAwardReadinessEvaluationDto>>(
            entity.EvaluationLineageJson) ?? [];
        var suppliers = Deserialize<List<ProcurementAwardReadinessSupplierDto>>(
            entity.SupplierLineageJson) ?? [];
        var verifications = Deserialize<List<ProcurementAwardReadinessVerificationDto>>(
            entity.VerificationLineageJson) ?? [];
        var authority = Deserialize<ProcurementAwardReadinessAuthorityDto>(
            entity.AuthorityLineageJson) ?? new();
        var evidence = Deserialize<List<ProcurementAwardReadinessEvidenceDto>>(
            entity.EvidenceLineageJson) ?? [];
        var groups = Deserialize<List<ProcurementAwardReadinessPrerequisiteGroupDto>>(
            entity.PrerequisiteSnapshotJson) ?? [];
        var timeline = Deserialize<List<ProcurementAwardReadinessTimelineEntryDto>>(
            entity.TimelineSnapshotJson) ?? [];
        var blocked = Deserialize<List<string>>(entity.BlockedReasonsJson) ?? [];
        return new ProcurementAwardReadinessDto
        {
            Id = entity.Id,
            DecisionSequence = entity.DecisionSequence,
            SourceType = entity.SourceType,
            SourceId = entity.SourceId,
            SourceReference = entity.SourceReference,
            Method = entity.Method,
            Status = entity.Status,
            IsCurrent = isCurrent,
            SourceIntegrityHash = entity.SourceIntegrityHash,
            IntegrityHash = entity.IntegrityHash,
            IdempotencyKey = entity.IdempotencyKey,
            CorrelationId = entity.CorrelationId,
            EvaluatedAtUtc = entity.EvaluatedAtUtc,
            EvaluatedByUserId = entity.EvaluatedByUserId,
            EvaluatedByName = entity.EvaluatedByName,
            Recommendation = recommendation,
            Evaluations = evaluations,
            Suppliers = suppliers,
            Verifications = verifications,
            Authority = authority,
            Evidence = evidence,
            PrerequisiteGroups = groups,
            Timeline = timeline,
            BlockedReasons = blocked,
            AllowedActions = isCurrent && entity.Status ==
                ProcurementAwardReadinessDecisionStatus.Ready
                ? ["EvaluateReadiness", "RecordAward"]
                : ["EvaluateReadiness"]
        };
    }

    private static ProcurementAwardReadinessScoreAttemptDto MapScore(
        ProcurementEvaluationScoreSheet entity,
        List<Guid> recallIds) =>
        new()
        {
            ScoreSheetId = entity.Id,
            CommitteeControlId = entity.CommitteeControlId,
            MeetingId = entity.MeetingId,
            AppointmentId = entity.AppointmentId,
            Phase = entity.Phase,
            ScoreSubjectType = entity.ScoreSubjectType,
            ScoreSubjectId = entity.ScoreSubjectId,
            Attempt = entity.Attempt,
            Status = entity.Status,
            SubmittedAtUtc = entity.SubmittedAtUtc,
            SubmittedByUserId = entity.SubmittedByUserId,
            SubmittedByName = entity.SubmittedByName,
            EvidenceReference = entity.EvidenceReference,
            IntegrityHash = entity.IntegrityHash,
            RecallIds = recallIds
        };

    private static ProcurementAwardReadinessPrequalificationDto MapPrequalification(
        ProcurementQualifiedListEntry item) =>
        new()
        {
            EntryId = item.Id,
            ExerciseId = item.ExerciseId,
            ApplicationId = item.ApplicationId,
            CategoryId = item.CategoryId,
            Status = item.Status,
            ValidFromUtc = item.ValidFromUtc,
            ExpiresAtUtc = item.ExpiresAtUtc,
            ApprovalReference = item.ApprovalReference,
            ApprovalEvidenceReference = item.ApprovalEvidenceReference,
            IntegrityHash = item.IntegrityHash
        };

    private static void AssertExpectations(
        EvaluateProcurementAwardReadinessRequest request,
        ReadinessState state)
    {
        if (!string.IsNullOrWhiteSpace(request.ExpectedSourceIntegrityHash) &&
            !string.Equals(
                request.ExpectedSourceIntegrityHash.Trim(),
                state.SourceIntegrityHash,
                StringComparison.OrdinalIgnoreCase))
            throw Conflict("AWARD_READINESS_SOURCE_STALE",
                "The source changed after it was displayed; refresh readiness before award.");
        AssertSet(
            request.ExpectedRecommendedSubjectIds,
            state.Recommendation.SubjectIds,
            "AWARD_READINESS_RECOMMENDATION_SUBJECT_MISMATCH",
            "The expected recommendation subjects do not match the server-derived recommendation.");
        AssertSet(
            request.ExpectedBusinessPartnerIds,
            state.Recommendation.BusinessPartnerIds,
            "AWARD_READINESS_RECOMMENDED_SUPPLIER_MISMATCH",
            "The expected suppliers do not match the server-derived recommendation.");
    }

    private static void AssertSet(
        IReadOnlyCollection<Guid> expected,
        IReadOnlyCollection<Guid> actual,
        string code,
        string message)
    {
        if (expected.Count == 0) return;
        if (!expected.Distinct().OrderBy(item => item)
                .SequenceEqual(actual.Distinct().OrderBy(item => item)))
            throw Conflict(code, message);
    }

    private static void Add(
        ReadinessState state,
        ProcurementAwardReadinessPrerequisiteGroup group,
        string code,
        bool passed,
        string message,
        string? remediation = null,
        string? lineageType = null,
        Guid? lineageId = null,
        string? lineageHash = null) =>
        Add(state, group, code,
            passed
                ? ProcurementAwardReadinessPrerequisiteStatus.Passed
                : ProcurementAwardReadinessPrerequisiteStatus.Failed,
            message, remediation, lineageType, lineageId, lineageHash);

    private static void Add(
        ReadinessState state,
        ProcurementAwardReadinessPrerequisiteGroup group,
        string code,
        ProcurementAwardReadinessPrerequisiteStatus status,
        string message,
        string? remediation = null,
        string? lineageType = null,
        Guid? lineageId = null,
        string? lineageHash = null)
    {
        if (!state.Items.TryGetValue(group, out var items))
        {
            items = [];
            state.Items[group] = items;
        }
        items.Add(new ProcurementAwardReadinessPrerequisiteItemDto
        {
            Code = code,
            Label = code.Replace('_', ' '),
            Status = status,
            Message = message,
            Remediation = remediation,
            LineageType = lineageType,
            LineageId = lineageId,
            LineageHash = lineageHash
        });
    }

    private static void AddEvidence(
        ReadinessState state,
        string requirementKey,
        string label,
        Guid? referenceId,
        string? reference) =>
        state.Evidence.Add(new ProcurementAwardReadinessEvidenceDto
        {
            RequirementKey = requirementKey,
            Label = label,
            ReferenceId = referenceId,
            Reference = reference,
            IsAvailable = referenceId.HasValue || !string.IsNullOrWhiteSpace(reference)
        });

    private static ProcurementAwardReadinessTimelineEntryDto Timeline(
        string eventType,
        DateTime occurredAtUtc,
        Guid? actorId,
        string? reference,
        string? hash) =>
        new()
        {
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            ActorUserId = actorId,
            Reference = reference,
            IntegrityHash = hash
        };

    private static bool ProjectionValid(string? snapshot, string? hash) =>
        !string.IsNullOrWhiteSpace(snapshot) &&
        !string.IsNullOrWhiteSpace(hash) &&
        HashMatches(NormalizeJson(snapshot), hash);

    private static bool HashMatches(string value, string hash) =>
        string.Equals(ComputeHash(value), hash, StringComparison.OrdinalIgnoreCase);

    private static string ComputeVerificationHash(
        TenderAwardVerification verification,
        TenderAwardVerificationBidder bidder) =>
        ComputeHash(Serialize(new
        {
            verificationId = verification.Id,
            verification.TenderId,
            verification.TemplateId,
            verificationStatus = verification.Status,
            verification.StartedDate,
            verification.CompletedDate,
            bidderId = bidder.Id,
            bidder.TenderBidId,
            bidder.BusinessPartnerId,
            bidderStatus = bidder.Status,
            bidder.VerifiedDate,
            results = bidder.ItemResults.Where(item => !item.IsDeleted)
                .OrderBy(item => item.ChecklistItemId)
                .Select(item => new
                {
                    item.Id,
                    item.ChecklistItemId,
                    item.IsVerified,
                    item.Status,
                    item.VerifiedDate,
                    item.VerifiedById,
                    item.Comments,
                    requiresDocument = item.ChecklistItem?.RequiresDocument,
                    documents = item.Documents.Where(document => !document.IsDeleted)
                        .OrderBy(document => document.Id)
                        .Select(document => new
                        {
                            document.Id,
                            document.FileName,
                            document.FilePath,
                            document.FileSize,
                            document.UploadedDate
                        })
                })
        }));

    private static string? TryGetJsonString(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(property, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid? TryGetJsonGuid(string? json, string property)
    {
        var value = TryGetJsonString(json, property);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static List<Guid> ParseGuids(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, JsonOptions);
        }
        catch (JsonException)
        {
            return json.Trim();
        }
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true ||
        exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;

    private static string NormalizeCorrelation(string correlationId) =>
        string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim().Length <= 100
                ? correlationId.Trim()
                : correlationId.Trim()[..100];

    private void ValidateMutationInput(
        Guid sourceId,
        EvaluateProcurementAwardReadinessRequest request)
    {
        EnsureAuthenticatedTenant();
        if (sourceId == Guid.Empty)
            throw Validation("AWARD_READINESS_SOURCE_REQUIRED", "SourceId is required.");
        if (request is null || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw Validation("AWARD_READINESS_IDEMPOTENCY_REQUIRED",
                "A non-empty idempotency key is required.");
        if (request.IdempotencyKey.Trim().Length > 100)
            throw Validation("AWARD_READINESS_IDEMPOTENCY_INVALID",
                "The idempotency key cannot exceed 100 characters.");
        if (request.ExpectedRecommendedSubjectIds.Contains(Guid.Empty) ||
            request.ExpectedBusinessPartnerIds.Contains(Guid.Empty))
            throw Validation("AWARD_READINESS_EXPECTATION_INVALID",
                "Expected identifiers cannot contain an empty GUID.");
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        // Read endpoints require procurement.records.read or procurement.audit.read.
        // Keep only tenant and external-user protection in the domain service.
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty ||
            _currentUser.IsExternalUser)
            throw new ProcurementAwardReadinessAuthorizationException(
                "An authenticated internal tenant context is required.");
    }

    private string ActorName() =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;

    private static ProcurementAwardReadinessNotFoundException NotFound(
        string code,
        string message) => new(code, message);

    private static ProcurementAwardReadinessConflictException Conflict(
        string code,
        string message) => new(code, message);

    private static ProcurementAwardReadinessValidationException Validation(
        string code,
        string message) => new(code, message);

    private sealed class EvaluatorAwardLineageState
    {
        public List<ProcurementAwardEvaluatorLineageDto> Items { get; } = [];
        public HashSet<Guid> ApprovalActorUserIds { get; } = [];
        public HashSet<Guid> ValidEvaluationIds { get; } = [];
        public Dictionary<Guid, TenderEvaluation> LegacyEvaluations { get; } = [];
        public Guid? MethodRuleId { get; set; }
        public string? MethodRuleCode { get; set; }
    }

    private sealed record SourceHeader(
        ProcurementAwardReadinessSourceType Type,
        Guid Id,
        string Reference,
        bool IsLegacyTender);

    private sealed class ReadinessState
    {
        public ProcurementMethodType Method { get; set; }
        public ProcurementAwardReadinessRecommendationDto Recommendation { get; set; } = new();
        public List<ProcurementAwardReadinessEvaluationDto> Evaluations { get; } = [];
        public List<ProcurementAwardReadinessSupplierDto> Suppliers { get; } = [];
        public List<ProcurementAwardReadinessVerificationDto> Verifications { get; } = [];
        public ProcurementAwardReadinessAuthorityDto Authority { get; set; } = new();
        public List<ProcurementAwardReadinessEvidenceDto> Evidence { get; } = [];
        public Dictionary<ProcurementAwardReadinessPrerequisiteGroup,
            List<ProcurementAwardReadinessPrerequisiteItemDto>> Items { get; } = [];
        public List<ProcurementAwardReadinessPrerequisiteGroupDto> Groups { get; set; } = [];
        public List<ProcurementAwardReadinessTimelineEntryDto> Timeline { get; set; } = [];
        public List<string> BlockedReasons { get; set; } = [];
        public string SourceIntegrityHash { get; set; } = string.Empty;
    }
}
