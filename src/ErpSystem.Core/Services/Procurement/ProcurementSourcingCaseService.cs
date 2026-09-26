using ErpSystem.Shared;
using System.Globalization;
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
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementSourcingCaseService : IProcurementSourcingCaseService
{
    private const string SourceType = "ProcurementSourcingCase";
    private const string EventType = "ProcurementSourcingCaseLifecycle";
    private const string ReadPermission = "procurement.records.read";
    private const string ManagePermission = "procurement.sourcing.manage";
    private const string EvaluateTenderPermission = "procurement.tender.evaluate";
    private const string AdministerTenderPermission = "procurement.tender.administer";
    private const string ApproveTenderPermission = "procurement.tender.approve";
    private const string ManageContractPermission = "procurement.contract.manage";
    private const string CreatePurchaseOrderPermission = "procurement.purchase-order.create";
    private const string ClosePermission = "procurement.sourcing.approve";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementComplianceDecisionService _compliance;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementRequisitionSourcingReleaseService _sourcingReleases;
    private readonly ILogger<ProcurementSourcingCaseService> _logger;

    public ProcurementSourcingCaseService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementComplianceDecisionService compliance,
        IProcurementControlEventService controlEvents,
        IProcurementSodGuardService sodGuard,
        IProcurementRequisitionSourcingReleaseService sourcingReleases,
        ILogger<ProcurementSourcingCaseService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _compliance = compliance;
        _controlEvents = controlEvents;
        _sodGuard = sodGuard;
        _sourcingReleases = sourcingReleases;
        _logger = logger;
    }

    private IGenericRepository<ProcurementSourcingCase> Cases => _unitOfWork.Repository<ProcurementSourcingCase>();
    private IGenericRepository<ProcurementSourcingCaseLot> Lots => _unitOfWork.Repository<ProcurementSourcingCaseLot>();
    private IGenericRepository<ProcurementSourcingCaseLotItem> LotItems => _unitOfWork.Repository<ProcurementSourcingCaseLotItem>();
    private IGenericRepository<ProcurementSourcingCaseSourceRequest> SourceRequests => _unitOfWork.Repository<ProcurementSourcingCaseSourceRequest>();
    private IGenericRepository<PurchaseRequisition> Requisitions => _unitOfWork.Repository<PurchaseRequisition>();
    private IGenericRepository<PurchaseRequisitionItem> RequisitionItems => _unitOfWork.Repository<PurchaseRequisitionItem>();
    private IGenericRepository<ProcurementRequisitionSourcingRelease> Releases => _unitOfWork.Repository<ProcurementRequisitionSourcingRelease>();
    private IGenericRepository<ProcurementPolicyMethodRule> MethodRules => _unitOfWork.Repository<ProcurementPolicyMethodRule>();
    private IGenericRepository<ProcurementPolicyThresholdRule> ThresholdRules => _unitOfWork.Repository<ProcurementPolicyThresholdRule>();
    private IGenericRepository<ProcurementPolicyExceptionRule> ExceptionRules => _unitOfWork.Repository<ProcurementPolicyExceptionRule>();
    private IGenericRepository<WorkflowInstance> WorkflowInstances => _unitOfWork.Repository<WorkflowInstance>();

    public async Task<ProcurementSourcingCaseSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ReadPermission, "summary", "sourcing-case-summary", cancellationToken);
        var cases = await CaseQuery(false).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        var stale = 0;
        foreach (var item in cases.Where(item => item.Status is ProcurementSourcingCaseStatus.Ready or ProcurementSourcingCaseStatus.InProgress))
            if (!(await EvaluateSourceStateAsync(item, cancellationToken)).Current) stale++;
        return new ProcurementSourcingCaseSummaryDto
        {
            TotalCount = cases.Count,
            ReadyCount = cases.Count(item => item.Status == ProcurementSourcingCaseStatus.Ready),
            InProgressCount = cases.Count(item => item.Status == ProcurementSourcingCaseStatus.InProgress),
            ClosedCount = cases.Count(item => item.Status == ProcurementSourcingCaseStatus.Closed),
            CancelledCount = cases.Count(item => item.Status == ProcurementSourcingCaseStatus.Cancelled),
            StaleCount = stale
        };
    }

    public async Task<ProcurementSourcingCasePageDto> SearchAsync(
        ProcurementSourcingCaseSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ReadPermission, "search", "sourcing-case-search", cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = CaseQuery(false);
        if (request.Status.HasValue) query = query.Where(item => item.Status == request.Status.Value);
        if (request.Method.HasValue) query = query.Where(item => item.SelectedMethod == request.Method.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.CaseNumber.Contains(search) ||
                item.PurchaseRequisition.RequisitionNumber.Contains(search) ||
                item.Justification.Contains(search) || item.PolicyCode.Contains(search));
        }
        var total = await query.CountAsync(cancellationToken);
        var entities = await query.OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = new List<ProcurementSourcingCaseDto>(entities.Count);
        foreach (var entity in entities) items.Add(await MapAsync(entity, cancellationToken));
        return new ProcurementSourcingCasePageDto { Page = page, PageSize = pageSize, TotalCount = total, Items = items };
    }

    public async Task<IReadOnlyList<ProcurementSourcingCaseSourceOptionDto>> GetSourceOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ReadPermission, "source-options", "sourcing-case-source-options", cancellationToken);
        var requisitions = await Requisitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.Status == "Approved")
            .AsNoTracking()
            .OrderByDescending(item => item.ApprovedAt)
            .Take(250)
            .ToListAsync(cancellationToken);
        var requisitionIds = requisitions.Select(item => item.Id).ToList();
        var cases = await Cases.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                requisitionIds.Contains(item.PurchaseRequisitionId) && !item.IsDeleted &&
                (item.Status == ProcurementSourcingCaseStatus.Ready ||
                 item.Status == ProcurementSourcingCaseStatus.InProgress))
            .AsNoTracking()
            .OrderByDescending(item => item.CaseSequence)
            .ToListAsync(cancellationToken);
        var options = new List<ProcurementSourcingCaseSourceOptionDto>();
        foreach (var requisition in requisitions)
        {
            PurchaseRequisitionSourcingReadinessDto readiness;
            try
            {
                readiness = await _sourcingReleases.GetReadinessAsync(requisition.Id, cancellationToken);
            }
            catch (ProcurementRequisitionSourcingNotFoundException)
            {
                continue;
            }
            if (!readiness.IsCompliant) continue;
            var existing = cases.FirstOrDefault(entry => entry.PurchaseRequisitionId == requisition.Id);
            options.Add(new ProcurementSourcingCaseSourceOptionDto
            {
                RequisitionId = requisition.Id,
                RequisitionNumber = requisition.RequisitionNumber,
                RequisitionStatus = requisition.Status,
                SourcingReleaseId = readiness.CurrentRelease?.Id,
                ReleaseReference = readiness.CurrentRelease?.ReleaseReference,
                SourcePlanId = requisition.SourcePlanId,
                SourcePlanItemId = requisition.SourcePlanItemId,
                SourcePlanNumber = requisition.SourcePlanNumber,
                SourcePlanItemDescription = requisition.SourcePlanItemDescription,
                Category = requisition.ProcurementCategory ?? ProcurementCategoryClass.Goods,
                EstimatedValue = requisition.TotalAmount,
                CurrencyCode = NormalizeCurrency(requisition.Currency),
                CurrentCaseId = existing?.Id,
                CurrentCaseNumber = existing?.CaseNumber
            });
        }
        return options;
    }

    public async Task<ProcurementSourcingCaseReadinessDto> GetReadinessAsync(
        Guid requisitionId,
        ProcurementMethodType? method = null,
        string? overrideReason = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ReadPermission, requisitionId.ToString("N"), "sourcing-case-readiness", cancellationToken);
        var requisition = await LoadRequisitionAsync(requisitionId, false, cancellationToken);
        var lines = await LoadLinesAsync(requisitionId, false, cancellationToken);
        PurchaseRequisitionSourcingReadinessDto releaseReadiness;
        try { releaseReadiness = await _sourcingReleases.GetReadinessAsync(requisitionId, cancellationToken); }
        catch (ProcurementRequisitionSourcingNotFoundException exception)
        {
            throw new ProcurementSourcingCaseNotFoundException(exception.Code, exception.Message);
        }

        MethodSelectionDecision? selection = null;
        if (releaseReadiness.IsCompliant)
            selection = await ResolveMethodSelectionAsync(requisition, method, overrideReason,
                $"sourcing-case-readiness:{requisitionId:N}", OverrideControlMode.Check, cancellationToken);
        var currentReleaseCase = releaseReadiness.CurrentRelease is null
            ? null
            : await CaseQuery(false).SingleOrDefaultAsync(
                item => item.SourcingReleaseId == releaseReadiness.CurrentRelease.Id &&
                    (item.Status == ProcurementSourcingCaseStatus.Ready ||
                     item.Status == ProcurementSourcingCaseStatus.InProgress),
                cancellationToken);
        var activeCase = await CaseQuery(false)
            .Where(item => item.PurchaseRequisitionId == requisitionId &&
                (item.Status == ProcurementSourcingCaseStatus.Ready ||
                 item.Status == ProcurementSourcingCaseStatus.InProgress))
            .OrderByDescending(item => item.CaseSequence)
            .FirstOrDefaultAsync(cancellationToken);
        var existing = currentReleaseCase ?? activeCase;
        if (existing is null && releaseReadiness.CurrentRelease is null)
        {
            existing = await CaseQuery(false)
                .Where(item => item.PurchaseRequisitionId == requisitionId)
                .OrderByDescending(item => item.CaseSequence)
                .FirstOrDefaultAsync(cancellationToken);
        }
        var existingDto = existing is null ? null : await MapAsync(existing, cancellationToken);
        var methodCompliant = selection?.IsEligible == true;
        var activeCaseBlocksRelease = activeCase is not null &&
            releaseReadiness.CurrentRelease is not null &&
            activeCase.SourcingReleaseId != releaseReadiness.CurrentRelease.Id;
        var canCreate = releaseReadiness.IsCompliant && existing is null && methodCompliant;
        var code = !releaseReadiness.IsCompliant ? releaseReadiness.DecisionCode
            : activeCaseBlocksRelease ? "SOURCING_CASE_ACTIVE_RELEASE_CONFLICT"
            : existing is not null ? "SOURCING_CASE_ALREADY_EXISTS"
            : !methodCompliant ? selection?.DecisionCode ?? "SOURCING_CASE_METHOD_BLOCKED"
            : selection?.MethodSelectionBasis == ProcurementSourcingMethodSelectionBasis.ApprovedOverride
                ? "SOURCING_CASE_OVERRIDE_READY"
                : "SOURCING_CASE_RECOMMENDATION_READY";
        var message = !releaseReadiness.IsCompliant ? releaseReadiness.Message
            : activeCaseBlocksRelease
                ? ActiveCaseConflictMessage(activeCase!)
            : existing is not null ? $"Sourcing case {existing.CaseNumber} already owns this immutable release."
            : !methodCompliant ? selection?.Message ?? "The procurement method could not be resolved from the current effective policy."
            : selection?.Message ?? "The server-derived recommendation can be locked into a sourcing case; its release audit record is created automatically.";
        return new ProcurementSourcingCaseReadinessDto
        {
            RequisitionId = requisition.Id,
            RequisitionNumber = requisition.RequisitionNumber,
            IsReleaseCurrent = releaseReadiness.IsReleased,
            IsMethodCompliant = methodCompliant,
            CanCreate = canCreate,
            DecisionCode = code,
            Message = message,
            RequestedMethod = method,
            RecommendedMethod = selection?.RecommendedMethod,
            SelectedMethod = selection?.SelectedMethod,
            SuggestedMethod = selection?.RecommendedMethod,
            MethodSelectionBasis = selection?.MethodSelectionBasis ?? ProcurementSourcingMethodSelectionBasis.AutomaticRecommendation,
            Override = selection?.Override,
            CurrentRelease = releaseReadiness.CurrentRelease,
            CurrentCase = existingDto,
            MethodCandidates = selection?.Recommendation.MethodCandidates.ToList() ?? [],
            HardStops = selection?.Selection.HardStops.ToList() ?? [],
            ReviewRequirements = selection?.Selection.ReviewRequirements.ToList() ?? [],
            Lines = lines.Select((item, index) => MapLine(item, index + 1)).ToList()
        };
    }

    public async Task<ProcurementSourcingCaseDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ReadPermission, id.ToString("N"), "sourcing-case-get", cancellationToken);
        var entity = await CaseQuery(false).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ProcurementSourcingCaseNotFoundException("SOURCING_CASE_NOT_FOUND", "The sourcing case was not found in the current tenant.");
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<ProcurementSourcingCaseDto> CreateAsync(
        CreateProcurementSourcingCaseRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidateCreate(request);
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(ManagePermission, request.RequisitionId.ToString("N"), normalizedCorrelation, cancellationToken);
        return await CreateCoreAsync(request, normalizedCorrelation, cancellationToken);
    }

    private async Task<ProcurementSourcingCaseDto> CreateCoreAsync(
        CreateProcurementSourcingCaseRequest request,
        string normalizedCorrelation,
        CancellationToken cancellationToken)
    {
        ValidateCreate(request);
        var requisition = await LoadRequisitionAsync(request.RequisitionId, true, cancellationToken);
        var lines = await LoadLinesAsync(requisition.Id, true, cancellationToken);
        var readiness = await GetReadinessAsync(requisition.Id, request.SelectedMethod, request.MethodOverrideReason, cancellationToken);
        if (!readiness.IsReleaseCurrent && readiness.CanCreate)
        {
            await _sourcingReleases.ReleaseAsync(
                requisition.Id,
                $"System-generated release for sourcing case {requisition.RequisitionNumber}.",
                normalizedCorrelation,
                cancellationToken);
            readiness = await GetReadinessAsync(
                requisition.Id,
                request.SelectedMethod,
                request.MethodOverrideReason,
                cancellationToken);
        }
        if (!readiness.IsReleaseCurrent || readiness.CurrentRelease is null)
            throw new ProcurementSourcingCaseValidationException(readiness.DecisionCode, readiness.Message);
        if (readiness.CurrentCase is not null &&
            readiness.CurrentCase.Status is ProcurementSourcingCaseStatus.Ready or ProcurementSourcingCaseStatus.InProgress &&
            readiness.CurrentCase.SourcingReleaseId != readiness.CurrentRelease.Id)
            throw new ProcurementSourcingCaseConflictException(
                "SOURCING_CASE_ACTIVE_RELEASE_CONFLICT",
                ActiveCaseConflictMessage(readiness.CurrentCase));
        var selection = await ResolveMethodSelectionAsync(requisition, request.SelectedMethod, request.MethodOverrideReason,
            normalizedCorrelation, OverrideControlMode.Enforce, cancellationToken);
        if (!selection.IsEligible || !selection.SelectedMethod.HasValue)
        {
            await RecordMethodDecisionAsync(requisition, selection, request.MethodOverrideReason,
                ProcurementControlEventResult.Denied, normalizedCorrelation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ProcurementSourcingCaseValidationException(selection.DecisionCode, selection.Message);
        }
        if (selection.MethodSelectionBasis == ProcurementSourcingMethodSelectionBasis.ApprovedOverride &&
            (readiness.CurrentRelease.ApprovedExceptionRuleId != selection.Override?.RuleId ||
             !string.Equals(readiness.CurrentRelease.ExceptionApprovalReference,
                 selection.Override?.ApprovalReference, StringComparison.Ordinal)))
            throw new ProcurementSourcingCaseConflictException("SOURCING_METHOD_OVERRIDE_RELEASE_LINEAGE_INVALID",
                "The immutable sourcing release does not contain the exact approved method-override rule and approval reference.");
        var selectedMethod = selection.SelectedMethod.Value;
        var compliance = selection.Selection;
        var methodRule = compliance.MatchedRules.SingleOrDefault(item => item.RuleKind == ProcurementPolicyRuleKind.Method)
            ?? throw new ProcurementSourcingCaseConflictException("SOURCING_CASE_METHOD_RULE_MISSING", "The selected method did not resolve one exact method rule.");
        var thresholdRule = compliance.MatchedRules.SingleOrDefault(item => item.RuleKind == ProcurementPolicyRuleKind.Threshold)
            ?? throw new ProcurementSourcingCaseConflictException("SOURCING_CASE_THRESHOLD_RULE_MISSING", "The selected method did not resolve one exact threshold rule.");
        if (methodRule.PolicySetId != thresholdRule.PolicySetId || methodRule.PolicySetId != compliance.Policy.PolicySetId)
            throw new ProcurementSourcingCaseConflictException("SOURCING_CASE_RULE_LINEAGE_INVALID", "Method and threshold rules must belong to the selected effective policy version.");
        var threshold = await ThresholdRules.GetQueryable(item => item.Id == thresholdRule.RuleId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsEnabled)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementSourcingCaseConflictException("SOURCING_CASE_THRESHOLD_RULE_STALE",
                "The matched threshold rule is no longer available in the current tenant.");

        var caseJustification = BuildAutomaticJustification(
            selectedMethod,
            compliance.Policy.PolicyCode,
            compliance.Policy.Version,
            requisition.ProcurementCategory!.Value,
            requisition.TotalAmount,
            NormalizeCurrency(requisition.Currency),
            methodRule.RuleCode,
            threshold,
            request.Justification);

        var lotSpecs = ValidateLots(request.Lots, lines, readiness.CurrentRelease, requisition);
        if (lotSpecs.Count > 1 && (request.Justification?.Trim().Length ?? 0) < 10)
            throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_LOT_JUSTIFICATION_REQUIRED",
                "Explain in at least 10 characters why the requisition is divided into multiple sourcing lots.");
        var fingerprintObject = new
        {
            schemaVersion = "tdc.sourcing-case.v1",
            releaseId = readiness.CurrentRelease.Id,
            releaseFingerprint = readiness.CurrentRelease.ControlFingerprint,
            recommendedMethod = selection.RecommendedMethod,
            selectedMethod,
            selection.MethodSelectionBasis,
            policySetId = compliance.Policy.PolicySetId,
            compliance.Policy.Version,
            methodRuleId = methodRule.RuleId,
            thresholdRuleId = thresholdRule.RuleId,
            overrideRuleId = selection.Override?.RuleId,
            overrideWorkflowInstanceId = selection.Override?.WorkflowInstanceId,
            overrideReason = NullIfWhiteSpace(request.MethodOverrideReason),
            overrideApprovalActorUserIds = selection.Override?.ApprovalActorUserIds.OrderBy(item => item).ToArray(),
            requisition.TotalAmount,
            currency = NormalizeCurrency(requisition.Currency),
            justification = caseJustification,
            lots = lotSpecs.Select(item => new { item.Code, item.Title, item.Description, item.EstimatedValue, item.ItemIds }).ToArray()
        };
        var caseFingerprint = ComputeHash(JsonSerializer.Serialize(fingerprintObject, JsonOptions));
        var existing = await CaseQuery(false).SingleOrDefaultAsync(item =>
            item.SourcingReleaseId == readiness.CurrentRelease.Id &&
            (item.Status == ProcurementSourcingCaseStatus.Ready ||
             item.Status == ProcurementSourcingCaseStatus.InProgress), cancellationToken);
        if (existing is not null)
        {
            if (existing.CaseFingerprint != caseFingerprint)
                throw new ProcurementSourcingCaseConflictException("SOURCING_CASE_RELEASE_ALREADY_USED", $"Release {readiness.CurrentRelease.ReleaseReference} is already locked by {existing.CaseNumber} with different case data.");
            await RecordAsync(existing, "SourcingCaseReused", ProcurementControlEventResult.Allowed,
                new { existing.CaseNumber, existing.SourcingReleaseId }, normalizedCorrelation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await MapAsync(existing, cancellationToken);
        }

        var sequence = (await Cases.GetQueryableIncludingDeleted(item => item.TenantId == _currentUser.TenantId &&
                item.PurchaseRequisitionId == requisition.Id)
            .Select(item => (int?)item.CaseSequence).MaxAsync(cancellationToken) ?? 0) + 1;
        var now = DateTime.UtcNow;
        var caseId = Guid.NewGuid();
        var caseNumber = Truncate($"SC-{requisition.RequisitionNumber}-A{sequence}", 100);
        var lotEntities = lotSpecs.Select((item, index) => new ProcurementSourcingCaseLot
        {
            Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, SourcingCaseId = caseId,
            LotNumber = index + 1, LotCode = item.Code, Title = item.Title, Description = item.Description,
            EstimatedValue = item.EstimatedValue, CurrencyCode = NormalizeCurrency(requisition.Currency),
            CreatedAt = now, CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId
        }).ToList();
        var sourceRequest = new ProcurementSourcingCaseSourceRequest
        {
            Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, SourcingCaseId = caseId, RequestSequence = 1,
            RequestReference = Truncate($"{caseNumber}-R1", 100), SourceType = ExpectedSourceType(selectedMethod),
            Method = selectedMethod, Status = ProcurementSourcingCaseSourceRequestStatus.Planned,
            LotIdsJson = JsonSerializer.Serialize(lotEntities.Select(item => item.Id).ToArray(), JsonOptions),
            PlannedAtUtc = now, CreatedAt = now, CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        var snapshot = new
        {
            schemaVersion = "tdc.sourcing-case.v1",
            id = caseId, caseNumber, sequence, purchaseRequisitionId = requisition.Id, requisition.RequisitionNumber,
            sourcingRelease = readiness.CurrentRelease, sourcePlanId = readiness.CurrentRelease.SourcePlanId,
            sourcePlanItemId = readiness.CurrentRelease.SourcePlanItemId, requisition.ProcurementCategory,
            recommendedMethod = selection.RecommendedMethod, selectedMethod, selection.MethodSelectionBasis,
            estimatedValue = requisition.TotalAmount,
            currencyCode = NormalizeCurrency(requisition.Currency), policy = compliance.Policy,
            methodRule, thresholdRule, authorities = compliance.RequiredAuthorities,
            authorityRouteId = readiness.CurrentRelease.AuthorityRouteId,
            authorityRouteReference = readiness.CurrentRelease.AuthorityRouteReference,
            approvedExceptionRuleId = readiness.CurrentRelease.ApprovedExceptionRuleId,
            readiness.CurrentRelease.ExceptionApprovalReference, requisition.ExceptionEvidenceReference,
            methodOverride = selection.Override is null ? null : new
            {
                selection.Override.RuleId, selection.Override.RuleCode, selection.Override.RuleName,
                selection.Override.ExceptionType, selection.Override.ApproverRole,
                selection.Override.WorkflowDefinitionId, selection.Override.WorkflowInstanceId,
                selection.Override.ApprovalReference, selection.Override.EvidenceReference,
                selection.Override.ApprovedAtUtc,
                approvalActorUserIds = selection.Override.ApprovalActorUserIds.OrderBy(item => item).ToArray(),
                reason = request.MethodOverrideReason!.Trim()
            },
            justification = caseJustification, lots = lotEntities.Select((item, index) => new
            {
                item.Id, item.LotNumber, item.LotCode, item.Title, item.Description, item.EstimatedValue,
                itemIds = lotSpecs[index].ItemIds
            }).ToArray(), sourceRequest = new { sourceRequest.Id, sourceRequest.RequestReference, sourceRequest.SourceType },
            sourceControlFingerprint = readiness.CurrentRelease.ControlFingerprint, caseFingerprint,
            createdAtUtc = now, createdById = _currentUser.UserId, createdByName = ActorName(), correlationId = normalizedCorrelation
        };
        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        var entity = new ProcurementSourcingCase
        {
            Id = caseId, TenantId = _currentUser.TenantId, PurchaseRequisitionId = requisition.Id,
            SourcingReleaseId = readiness.CurrentRelease.Id, CaseSequence = sequence, CaseNumber = caseNumber,
            SourcePlanId = readiness.CurrentRelease.SourcePlanId, SourcePlanItemId = readiness.CurrentRelease.SourcePlanItemId,
            Category = requisition.ProcurementCategory!.Value, RecommendedMethod = selection.RecommendedMethod!.Value,
            SelectedMethod = selectedMethod, MethodSelectionBasis = selection.MethodSelectionBasis,
            EstimatedValue = requisition.TotalAmount, CurrencyCode = NormalizeCurrency(requisition.Currency),
            PolicySetId = compliance.Policy.PolicySetId, PolicyCode = compliance.Policy.PolicyCode,
            PolicyVersion = compliance.Policy.Version, MethodRuleId = methodRule.RuleId, MethodRuleCode = methodRule.RuleCode,
            ThresholdRuleId = thresholdRule.RuleId, ThresholdRuleCode = thresholdRule.RuleCode,
            AuthorityRouteId = readiness.CurrentRelease.AuthorityRouteId,
            AuthorityRouteReference = readiness.CurrentRelease.AuthorityRouteReference,
            ApprovedExceptionRuleId = selection.Override?.RuleId ?? readiness.CurrentRelease.ApprovedExceptionRuleId,
            MethodOverrideWorkflowInstanceId = selection.Override?.WorkflowInstanceId,
            MethodOverrideReason = selection.Override is null ? null : request.MethodOverrideReason!.Trim(),
            MethodOverrideApprovalActorsJson = selection.Override is null ? null :
                JsonSerializer.Serialize(selection.Override.ApprovalActorUserIds.OrderBy(item => item).ToArray(), JsonOptions),
            MethodOverrideApprovedAtUtc = selection.Override?.ApprovedAtUtc,
            ExceptionApprovalReference = readiness.CurrentRelease.ExceptionApprovalReference,
            ExceptionEvidenceReference = requisition.ExceptionEvidenceReference,
            Justification = caseJustification, Status = ProcurementSourcingCaseStatus.Ready,
            CreatedByName = ActorName(), SourceControlFingerprint = readiness.CurrentRelease.ControlFingerprint,
            CaseFingerprint = caseFingerprint, SnapshotJson = snapshotJson, IntegrityHash = ComputeHash(snapshotJson),
            CreatedAt = now, CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray(), PurchaseRequisition = requisition,
            SourcingRelease = await Releases.GetQueryable(item => item.Id == readiness.CurrentRelease.Id).SingleAsync(cancellationToken),
            Lots = lotEntities, SourceRequests = [sourceRequest]
        };
        foreach (var lot in lotEntities) lot.SourcingCase = entity;
        sourceRequest.SourcingCase = entity;
        var lineById = lines.ToDictionary(item => item.Id);
        for (var index = 0; index < lotEntities.Count; index++)
        {
            foreach (var itemId in lotSpecs[index].ItemIds)
            {
                var lotItem = new ProcurementSourcingCaseLotItem
                {
                    Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, SourcingCaseId = caseId,
                    LotId = lotEntities[index].Id, PurchaseRequisitionItemId = itemId,
                    CreatedAt = now, CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId,
                    SourcingCase = entity, Lot = lotEntities[index], PurchaseRequisitionItem = lineById[itemId]
                };
                lotEntities[index].Items.Add(lotItem);
            }
        }
        await Cases.AddAsync(entity);
        await RecordAsync(entity,
            selection.MethodSelectionBasis == ProcurementSourcingMethodSelectionBasis.ApprovedOverride
                ? "SourcingMethodOverrideApproved"
                : "SourcingMethodRecommended",
            ProcurementControlEventResult.Allowed,
            new
            {
                selection.DecisionCode, selection.Message, selection.RecommendedMethod, selection.SelectedMethod,
                selection.MethodSelectionBasis, overrideRuleId = selection.Override?.RuleId,
                overrideWorkflowInstanceId = selection.Override?.WorkflowInstanceId,
                overrideApprovalActorUserIds = selection.Override?.ApprovalActorUserIds
            }, normalizedCorrelation, cancellationToken);
        await RecordAsync(entity, "SourcingCaseCreated", ProcurementControlEventResult.Succeeded,
            new { entity.CaseNumber, entity.RecommendedMethod, entity.SelectedMethod, entity.MethodSelectionBasis,
                entity.PolicyCode, entity.PolicyVersion, entity.MethodRuleCode, entity.ThresholdRuleCode,
                entity.MethodOverrideWorkflowInstanceId },
            normalizedCorrelation, cancellationToken);
        try { await _unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Sourcing case creation conflicted for release {ReleaseId}", entity.SourcingReleaseId);
            throw new ProcurementSourcingCaseConflictException("SOURCING_CASE_CONCURRENT_CREATE", "Another request created a sourcing case for this release. Refresh and retry.");
        }
        return await MapAsync(entity, cancellationToken);
    }

    public Task<ProcurementSourcingCaseDto> CloseAsync(Guid id, ProcurementSourcingCaseActionRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, request, ProcurementSourcingCaseStatus.Closed, correlationId, cancellationToken);

    public Task<ProcurementSourcingCaseDto> CancelAsync(Guid id, ProcurementSourcingCaseActionRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, request, ProcurementSourcingCaseStatus.Cancelled, correlationId, cancellationToken);

    public async Task<ProcurementSourcingCaseEntryGateDto> EnforceSourceEntryAsync(
        Guid requisitionId,
        ProcurementMethodType? expectedMethod,
        string sourceType,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(ManagePermission, sourceReference, normalizedCorrelation, cancellationToken);
        var activeCase = await CaseQuery(false)
            .Where(item => item.PurchaseRequisitionId == requisitionId &&
                (item.Status == ProcurementSourcingCaseStatus.Ready ||
                 item.Status == ProcurementSourcingCaseStatus.InProgress))
            .OrderByDescending(item => item.CaseSequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeCase is not null)
        {
            var state = await EvaluateSourceStateAsync(activeCase, cancellationToken);
            if (!state.Current)
                throw new ProcurementSourcingCaseConflictException(
                    "SOURCING_CASE_ACTIVE_RELEASE_CONFLICT",
                    ActiveCaseConflictMessage(activeCase));

            return await BuildEntryGateAsync(activeCase, expectedMethod, sourceType, sourceReference,
                normalizedCorrelation, recordAllowedDecision: true, cancellationToken);
        }

        var release = await _sourcingReleases.EnforceSourcingAsync(requisitionId, sourceType, sourceReference, normalizedCorrelation, cancellationToken);
        var entity = await CaseQuery(false).SingleOrDefaultAsync(item =>
            item.SourcingReleaseId == release.Id &&
            (item.Status == ProcurementSourcingCaseStatus.Ready ||
             item.Status == ProcurementSourcingCaseStatus.InProgress), cancellationToken);
        if (entity is null && string.Equals(sourceType, "Tender", StringComparison.OrdinalIgnoreCase))
        {
            var readiness = await GetReadinessAsync(requisitionId, expectedMethod, cancellationToken: cancellationToken);
            if (!readiness.IsReleaseCurrent || readiness.CurrentRelease?.Id != release.Id)
                throw new ProcurementRequisitionSourcingValidationException(
                    readiness.DecisionCode,
                    readiness.Message);

            var defaultLot = new CreateProcurementSourcingCaseLotRequest
            {
                LotCode = "LOT-01",
                Title = $"{readiness.RequisitionNumber} approved requirement",
                PurchaseRequisitionItemIds = readiness.Lines.Select(item => item.Id).ToList()
            };
            try
            {
                await CreateCoreAsync(new CreateProcurementSourcingCaseRequest
                {
                    RequisitionId = requisitionId,
                    SelectedMethod = expectedMethod,
                    Lots = [defaultLot]
                }, normalizedCorrelation, cancellationToken);
            }
            catch (ProcurementSourcingCaseConflictException)
            {
                // A concurrent request may have locked this exact immutable release.
                // The authoritative case is reloaded and validated below; unrelated
                // conflicts still fail because no usable case will be found.
            }

            entity = await CaseQuery(false)
                .SingleOrDefaultAsync(item => item.SourcingReleaseId == release.Id &&
                    (item.Status == ProcurementSourcingCaseStatus.Ready ||
                     item.Status == ProcurementSourcingCaseStatus.InProgress), cancellationToken);
            if (entity is null)
                throw new ProcurementRequisitionSourcingValidationException(
                    "SOURCING_CASE_REQUIRED",
                    "The tender could not lock the current immutable sourcing release into a policy-controlled sourcing case.");
        }
        if (entity is null)
        {
            var requisition = await LoadRequisitionAsync(requisitionId, false, cancellationToken);
            var selectedMethod = expectedMethod ?? (string.Equals(sourceType, "RequestForQuotation", StringComparison.OrdinalIgnoreCase)
                ? ProcurementMethodType.RequestForQuotation
                : ProcurementMethodType.NationalCompetitiveTendering);
            return new ProcurementSourcingCaseEntryGateDto
            {
                SourcingCaseId = null,
                SourcingCaseNumber = string.Empty,
                SourcingReleaseId = release.Id,
                SourcePlanItemId = release.SourcePlanItemId,
                SelectedMethod = selectedMethod,
                MethodRuleId = null,
                MethodRuleCode = string.Empty,
                MinimumQuotationCount = 0,
                WorkflowDefinitionId = null,
                EstimatedValue = requisition.TotalAmount,
                CurrencyCode = NormalizeCurrency(requisition.Currency)
            };
        }
        return await BuildEntryGateAsync(entity, expectedMethod, sourceType, sourceReference,
            normalizedCorrelation, recordAllowedDecision: true, cancellationToken);
    }

    public async Task<ProcurementSourcingCaseEntryGateDto> RecoverTenderSourceEntryAsync(
        Guid requisitionId,
        Guid? retainedSourcingReleaseId,
        Guid tenderId,
        string tenderReference,
        string correlationId,
        CancellationToken cancellationToken = default)
        => await RecoverTenderSourceEntryAsync(
            requisitionId,
            retainedSourcingReleaseId,
            tenderId,
            tenderReference,
            correlationId,
            ProcurementTenderSourceRecoveryBoundary.Evaluation,
            cancellationToken);

    public async Task<ProcurementSourcingCaseEntryGateDto> RecoverTenderSourceEntryAsync(
        Guid requisitionId,
        Guid? retainedSourcingReleaseId,
        Guid tenderId,
        string tenderReference,
        string correlationId,
        ProcurementTenderSourceRecoveryBoundary boundary,
        CancellationToken cancellationToken = default)
    {
        if (tenderId == Guid.Empty || string.IsNullOrWhiteSpace(tenderReference))
            throw new ProcurementRequisitionSourcingValidationException(
                "TENDER_SOURCE_REQUIRED", "A persisted tender identity is required to recover sourcing lineage.");

        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            RecoveryPermission(boundary),
            tenderReference,
            normalizedCorrelation,
            cancellationToken);
        var releaseReadiness = await _sourcingReleases.GetLinkedControlReadinessAsync(requisitionId, cancellationToken);
        if (!releaseReadiness.IsReleased || releaseReadiness.CurrentRelease is null)
            throw new ProcurementRequisitionSourcingValidationException(
                releaseReadiness.DecisionCode, releaseReadiness.Message);
        if (retainedSourcingReleaseId.HasValue &&
            retainedSourcingReleaseId.Value != releaseReadiness.CurrentRelease.Id)
            throw new ProcurementRequisitionSourcingValidationException(
                "SOURCING_CASE_LINEAGE_MISMATCH",
                "The tender does not match the current immutable sourcing release.");

        var entity = await CaseQuery(false)
            .SingleOrDefaultAsync(item => item.SourcingReleaseId == releaseReadiness.CurrentRelease.Id &&
                (item.Status == ProcurementSourcingCaseStatus.Ready ||
                 item.Status == ProcurementSourcingCaseStatus.InProgress),
                cancellationToken);
        if (entity is null)
        {
            var readiness = await GetReadinessAsync(requisitionId, cancellationToken: cancellationToken);
            var defaultLot = new CreateProcurementSourcingCaseLotRequest
            {
                LotCode = "LOT-01",
                Title = $"{readiness.RequisitionNumber} approved requirement",
                PurchaseRequisitionItemIds = readiness.Lines.Select(item => item.Id).ToList()
            };
            try
            {
                await CreateCoreAsync(new CreateProcurementSourcingCaseRequest
                {
                    RequisitionId = requisitionId,
                    Lots = [defaultLot]
                }, normalizedCorrelation, cancellationToken);
            }
            catch (ProcurementSourcingCaseConflictException)
            {
                // A concurrent recovery may have locked this exact current release.
            }

            entity = await CaseQuery(false)
                .SingleOrDefaultAsync(item => item.SourcingReleaseId == releaseReadiness.CurrentRelease.Id &&
                    (item.Status == ProcurementSourcingCaseStatus.Ready ||
                     item.Status == ProcurementSourcingCaseStatus.InProgress),
                    cancellationToken);
        }

        if (entity is null)
            throw new ProcurementRequisitionSourcingValidationException(
                "SOURCING_CASE_REQUIRED",
                "The tender's current immutable release could not be locked into a policy-controlled sourcing case.");

        var sourceAlreadyRegistered = entity.SourceRequests.Any(item =>
            !item.IsDeleted &&
            item.Status == ProcurementSourcingCaseSourceRequestStatus.Created &&
            item.SourceEntityId == tenderId &&
            string.Equals(item.SourceType, "Tender", StringComparison.OrdinalIgnoreCase));
        var gate = await BuildEntryGateAsync(entity, null, "Tender", tenderReference,
            normalizedCorrelation, recordAllowedDecision: !sourceAlreadyRegistered, cancellationToken);
        await RegisterSourceRequestCoreAsync(entity.Id, "Tender", tenderId, tenderReference,
            normalizedCorrelation, cancellationToken);
        return gate;
    }

    private static string RecoveryPermission(
        ProcurementTenderSourceRecoveryBoundary boundary) => boundary switch
        {
            ProcurementTenderSourceRecoveryBoundary.Evaluation => EvaluateTenderPermission,
            ProcurementTenderSourceRecoveryBoundary.AwardAdministration => AdministerTenderPermission,
            ProcurementTenderSourceRecoveryBoundary.AwardApproval => ApproveTenderPermission,
            ProcurementTenderSourceRecoveryBoundary.ContractCreation => ManageContractPermission,
            ProcurementTenderSourceRecoveryBoundary.PurchaseOrderCreation => CreatePurchaseOrderPermission,
            _ => throw new ProcurementRequisitionSourcingValidationException(
                "TENDER_SOURCE_RECOVERY_BOUNDARY_INVALID",
                "The tender sourcing-lineage recovery boundary is not supported.")
        };

    private async Task<ProcurementSourcingCaseEntryGateDto> BuildEntryGateAsync(
        ProcurementSourcingCase entity,
        ProcurementMethodType? expectedMethod,
        string sourceType,
        string sourceReference,
        string normalizedCorrelation,
        bool recordAllowedDecision,
        CancellationToken cancellationToken)
    {
        if (entity.Status is ProcurementSourcingCaseStatus.Closed or ProcurementSourcingCaseStatus.Cancelled)
            throw new ProcurementRequisitionSourcingValidationException("SOURCING_CASE_TERMINAL",
                $"Sourcing case {entity.CaseNumber} is {entity.Status} and cannot start another source request.");
        if (expectedMethod.HasValue && entity.SelectedMethod != expectedMethod.Value)
            throw new ProcurementRequisitionSourcingValidationException("SOURCING_CASE_METHOD_MISMATCH",
                $"Sourcing case {entity.CaseNumber} locks {entity.SelectedMethod}; {expectedMethod.Value} cannot bypass that method decision.");
        var expectedType = ExpectedSourceType(entity.SelectedMethod);
        if (!string.Equals(expectedType, sourceType, StringComparison.OrdinalIgnoreCase))
            throw new ProcurementRequisitionSourcingValidationException("SOURCING_CASE_SOURCE_TYPE_MISMATCH",
                $"Method {entity.SelectedMethod} must create a {expectedType} source, not {sourceType}.");
        var state = await EvaluateSourceStateAsync(entity, cancellationToken);
        if (!state.Current)
        {
            await RecordAsync(entity, "SourcingCaseEntryBlocked", ProcurementControlEventResult.Denied,
                new { sourceType, sourceReference, state.Code, state.Message }, normalizedCorrelation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ProcurementRequisitionSourcingValidationException(state.Code, state.Message);
        }
        var methodRule = await LoadOperationalMethodRuleAsync(entity.MethodRuleId, cancellationToken);
        if (recordAllowedDecision)
        {
            await RecordAsync(entity, "SourcingCaseEntryAllowed", ProcurementControlEventResult.Allowed,
                new { sourceType, sourceReference, entity.SourcingReleaseId }, normalizedCorrelation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return new ProcurementSourcingCaseEntryGateDto
        {
            SourcingCaseId = entity.Id,
            HasAdvancedAuthorityRoute = ProcurementTenderRouting.HasAdvancedAuthority(entity.AuthorityRouteId, entity.AuthorityRouteReference),
            SourcingCaseNumber = entity.CaseNumber,
            SourcingReleaseId = entity.SourcingReleaseId,
            SourcePlanItemId = entity.SourcePlanItemId,
            SelectedMethod = entity.SelectedMethod,
            MethodRuleId = entity.MethodRuleId,
            MethodRuleCode = entity.MethodRuleCode,
            MinimumQuotationCount = methodRule.MinimumQuotationCount,
            WorkflowDefinitionId = methodRule.WorkflowDefinitionId,
            EstimatedValue = entity.EstimatedValue,
            CurrencyCode = entity.CurrencyCode
        };
    }

    public async Task RegisterSourceRequestAsync(
        Guid sourcingCaseId,
        string sourceType,
        Guid sourceEntityId,
        string sourceEntityReference,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(ManagePermission, sourceEntityReference, normalizedCorrelation, cancellationToken);
        await RegisterSourceRequestCoreAsync(sourcingCaseId, sourceType, sourceEntityId,
            sourceEntityReference, normalizedCorrelation, cancellationToken);
    }

    private async Task RegisterSourceRequestCoreAsync(
        Guid sourcingCaseId,
        string sourceType,
        Guid sourceEntityId,
        string sourceEntityReference,
        string normalizedCorrelation,
        CancellationToken cancellationToken)
    {
        var entity = await CaseQuery(true).SingleOrDefaultAsync(item => item.Id == sourcingCaseId, cancellationToken)
            ?? throw new ProcurementRequisitionSourcingValidationException("SOURCING_CASE_NOT_FOUND", "The sourcing case no longer exists in this tenant.");
        var existing = entity.SourceRequests.SingleOrDefault(item => item.SourceEntityId == sourceEntityId &&
            string.Equals(item.SourceType, sourceType, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return;
        if (entity.Status is ProcurementSourcingCaseStatus.Closed or ProcurementSourcingCaseStatus.Cancelled)
            throw new ProcurementRequisitionSourcingValidationException("SOURCING_CASE_TERMINAL", "A terminal sourcing case cannot register a source request.");
        var request = entity.SourceRequests.Where(item => item.Status == ProcurementSourcingCaseSourceRequestStatus.Planned &&
                string.Equals(item.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.RequestSequence).FirstOrDefault();
        if (request is null)
        {
            var sequence = entity.SourceRequests.Count == 0 ? 1 : entity.SourceRequests.Max(item => item.RequestSequence) + 1;
            request = new ProcurementSourcingCaseSourceRequest
            {
                Id = Guid.NewGuid(), TenantId = entity.TenantId, SourcingCaseId = entity.Id, RequestSequence = sequence,
                RequestReference = Truncate($"{entity.CaseNumber}-R{sequence}", 100), SourceType = sourceType,
                Method = entity.SelectedMethod, Status = ProcurementSourcingCaseSourceRequestStatus.Planned,
                LotIdsJson = JsonSerializer.Serialize(entity.Lots.Select(item => item.Id).ToArray(), JsonOptions),
                PlannedAtUtc = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId,
                RowVersion = Guid.NewGuid().ToByteArray(), SourcingCase = entity
            };
            entity.SourceRequests.Add(request);
            await SourceRequests.AddAsync(request);
        }
        var now = DateTime.UtcNow;
        request.Status = ProcurementSourcingCaseSourceRequestStatus.Created;
        request.SourceEntityId = sourceEntityId;
        request.SourceEntityReference = Truncate(sourceEntityReference, 100);
        request.RegisteredAtUtc = now;
        request.RegisteredById = _currentUser.UserId;
        request.RegisteredByName = ActorName();
        request.CorrelationId = normalizedCorrelation;
        request.UpdatedAt = now;
        request.UpdatedBy = _currentUser.Username;
        request.LastModifiedById = _currentUser.UserId;
        request.RowVersion = Guid.NewGuid().ToByteArray();
        if (entity.Status == ProcurementSourcingCaseStatus.Ready)
        {
            entity.Status = ProcurementSourcingCaseStatus.InProgress;
            entity.StartedAtUtc = now;
            entity.StartedById = _currentUser.UserId;
            entity.StartedByName = ActorName();
            Touch(entity, now);
        }
        await RecordAsync(entity, "SourcingSourceRegistered", ProcurementControlEventResult.Succeeded,
            new { request.RequestReference, sourceType, sourceEntityId, sourceEntityReference }, normalizedCorrelation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProcurementSourcingCaseEntryGateDto> RevalidateSourceEntryAsync(
        Guid requisitionId,
        Guid sourcingReleaseId,
        Guid sourcingCaseId,
        ProcurementMethodType expectedMethod,
        string sourceType,
        Guid sourceEntityId,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var entity = await CaseQuery(false).SingleOrDefaultAsync(item => item.Id == sourcingCaseId, cancellationToken)
            ?? throw new ProcurementRequisitionSourcingValidationException(
                "SOURCING_CASE_NOT_FOUND", "The retained sourcing case is unavailable in the current tenant.");
        if (entity.PurchaseRequisitionId != requisitionId || entity.SourcingReleaseId != sourcingReleaseId)
            throw new ProcurementRequisitionSourcingValidationException(
                "SOURCING_CASE_LINEAGE_MISMATCH", "The source document no longer matches its immutable requisition, release, and sourcing-case lineage.");
        if (entity.SelectedMethod != expectedMethod ||
            !string.Equals(ExpectedSourceType(entity.SelectedMethod), sourceType, StringComparison.OrdinalIgnoreCase))
            throw new ProcurementRequisitionSourcingValidationException(
                "SOURCING_CASE_METHOD_MISMATCH", $"Sourcing case {entity.CaseNumber} does not authorize {sourceType} execution.");
        if (entity.Status is ProcurementSourcingCaseStatus.Closed or ProcurementSourcingCaseStatus.Cancelled)
            throw new ProcurementRequisitionSourcingValidationException(
                "SOURCING_CASE_TERMINAL", $"Sourcing case {entity.CaseNumber} is {entity.Status} and cannot continue execution.");
        if (!entity.SourceRequests.Any(item => !item.IsDeleted &&
                item.Status == ProcurementSourcingCaseSourceRequestStatus.Created &&
                item.SourceEntityId == sourceEntityId &&
                string.Equals(item.SourceType, sourceType, StringComparison.OrdinalIgnoreCase)))
            throw new ProcurementRequisitionSourcingValidationException(
                "SOURCING_CASE_SOURCE_REQUEST_MISMATCH", "The source document is not the registered request for this sourcing case.");

        var state = await EvaluateSourceStateAsync(entity, cancellationToken);
        await RecordAsync(entity, state.Current ? "SourcingSourceRevalidated" : "SourcingSourceRevalidationBlocked",
            state.Current ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
            new { sourceType, sourceEntityId, sourceReference, state.Code, state.Message }, normalizedCorrelation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!state.Current)
            throw new ProcurementRequisitionSourcingValidationException(state.Code, state.Message);
        var methodRule = await LoadOperationalMethodRuleAsync(entity.MethodRuleId, cancellationToken);

        return new ProcurementSourcingCaseEntryGateDto
        {
            SourcingCaseId = entity.Id,
            HasAdvancedAuthorityRoute = ProcurementTenderRouting.HasAdvancedAuthority(entity.AuthorityRouteId, entity.AuthorityRouteReference),
            SourcingCaseNumber = entity.CaseNumber,
            SourcingReleaseId = entity.SourcingReleaseId,
            SourcePlanItemId = entity.SourcePlanItemId,
            SelectedMethod = entity.SelectedMethod,
            MethodRuleId = entity.MethodRuleId,
            MethodRuleCode = entity.MethodRuleCode,
            MinimumQuotationCount = methodRule.MinimumQuotationCount,
            WorkflowDefinitionId = methodRule.WorkflowDefinitionId,
            EstimatedValue = entity.EstimatedValue,
            CurrencyCode = entity.CurrencyCode
        };
    }

    private async Task<ProcurementSourcingCaseDto> TransitionAsync(
        Guid id,
        ProcurementSourcingCaseActionRequest request,
        ProcurementSourcingCaseStatus target,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureReason(request.Reason);
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var requiredPermission = target == ProcurementSourcingCaseStatus.Cancelled
            ? ManagePermission
            : ClosePermission;
        await EnsureCapabilityAsync(requiredPermission, id.ToString("N"), normalizedCorrelation, cancellationToken);
        var entity = await CaseQuery(true).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ProcurementSourcingCaseNotFoundException("SOURCING_CASE_NOT_FOUND", "The sourcing case was not found in the current tenant.");
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status is ProcurementSourcingCaseStatus.Closed or ProcurementSourcingCaseStatus.Cancelled)
            throw new ProcurementSourcingCaseConflictException("SOURCING_CASE_TERMINAL", $"Sourcing case {entity.CaseNumber} is already {entity.Status}.");
        if (target == ProcurementSourcingCaseStatus.Closed && (entity.Status != ProcurementSourcingCaseStatus.InProgress ||
            !entity.SourceRequests.Any(item => item.Status == ProcurementSourcingCaseSourceRequestStatus.Created)))
            throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_NOT_STARTED", "A sourcing case can close only after at least one controlled source request was created.");
        var before = entity.Status;
        var now = DateTime.UtcNow;
        entity.Status = target;
        entity.ClosedAtUtc = now;
        entity.ClosedById = _currentUser.UserId;
        entity.ClosedByName = ActorName();
        entity.ClosureReason = request.Reason.Trim();
        if (target == ProcurementSourcingCaseStatus.Cancelled)
            foreach (var item in entity.SourceRequests.Where(item => item.Status == ProcurementSourcingCaseSourceRequestStatus.Planned))
                item.Status = ProcurementSourcingCaseSourceRequestStatus.Cancelled;
        Touch(entity, now);
        await RecordAsync(entity, target == ProcurementSourcingCaseStatus.Closed ? "SourcingCaseClosed" : "SourcingCaseCancelled",
            ProcurementControlEventResult.Succeeded, new { Before = before, After = target, Reason = request.Reason.Trim() },
            normalizedCorrelation, cancellationToken);
        try { await _unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ProcurementSourcingCaseConflictException("SOURCING_CASE_CONCURRENCY", "The sourcing case changed after it was loaded. Refresh and retry.");
        }
        return await MapAsync(entity, cancellationToken);
    }

    private async Task<SourceState> EvaluateSourceStateAsync(ProcurementSourcingCase entity, CancellationToken cancellationToken)
    {
        if (ComputeHash(entity.SnapshotJson) != entity.IntegrityHash)
            return new(false, "SOURCING_CASE_INTEGRITY_INVALID", $"Sourcing case {entity.CaseNumber} failed immutable snapshot verification.");
        PurchaseRequisitionSourcingReadinessDto release;
        try
        {
            release = await _sourcingReleases.GetLinkedControlReadinessAsync(
                entity.PurchaseRequisitionId, cancellationToken);
        }
        catch (ProcurementRequisitionSourcingNotFoundException)
        {
            return new(false, "SOURCING_CASE_SOURCE_NOT_AVAILABLE",
                $"Sourcing case {entity.CaseNumber} is retained for history, but its operational requisition is no longer available.");
        }
        if (!release.IsReleased || release.CurrentRelease?.Id != entity.SourcingReleaseId ||
            release.CurrentRelease.ControlFingerprint != entity.SourceControlFingerprint)
            return new(false, "SOURCING_CASE_RELEASE_STALE", $"Sourcing case {entity.CaseNumber} no longer matches the current immutable requisition release.");
        var selection = await ResolveMethodSelectionAsync(entity.PurchaseRequisition, entity.SelectedMethod,
            entity.MethodOverrideReason, $"sourcing-case-revalidate:{entity.Id:N}", OverrideControlMode.None, cancellationToken);
        var decision = selection.Selection;
        var methodRule = decision.MatchedRules.SingleOrDefault(item => item.RuleKind == ProcurementPolicyRuleKind.Method);
        var thresholdRule = decision.MatchedRules.SingleOrDefault(item => item.RuleKind == ProcurementPolicyRuleKind.Threshold);
        if (!selection.IsEligible || selection.RecommendedMethod != entity.RecommendedMethod ||
            selection.SelectedMethod != entity.SelectedMethod || selection.MethodSelectionBasis != entity.MethodSelectionBasis ||
            decision.HardStops.Any() ||
            decision.Policy.PolicySetId != entity.PolicySetId || decision.Policy.Version != entity.PolicyVersion ||
            methodRule?.RuleId != entity.MethodRuleId || thresholdRule?.RuleId != entity.ThresholdRuleId ||
            selection.Override?.RuleId != (entity.MethodSelectionBasis == ProcurementSourcingMethodSelectionBasis.ApprovedOverride
                ? entity.ApprovedExceptionRuleId : null) ||
            selection.Override?.WorkflowInstanceId != entity.MethodOverrideWorkflowInstanceId ||
            !DeserializeIds(entity.MethodOverrideApprovalActorsJson).OrderBy(item => item)
                .SequenceEqual((selection.Override?.ApprovalActorUserIds ?? []).OrderBy(item => item)))
            return new(false, "SOURCING_CASE_POLICY_STALE", $"Sourcing case {entity.CaseNumber} no longer matches the current effective method and threshold policy.");
        return new(true, "SOURCING_CASE_CURRENT", entity.MethodSelectionBasis == ProcurementSourcingMethodSelectionBasis.ApprovedOverride
            ? "The immutable release, recommendation, and approved method-override lineage are current."
            : "The immutable release and automatic policy recommendation are current.");
    }

    private async Task<MethodSelectionDecision> ResolveMethodSelectionAsync(
        PurchaseRequisition requisition,
        ProcurementMethodType? requestedMethod,
        string? overrideReason,
        string correlationId,
        OverrideControlMode controlMode,
        CancellationToken cancellationToken)
    {
        var recommendation = await EvaluateMethodAsync(requisition, null, null, null, correlationId, cancellationToken);
        var recommendedMethod = recommendation.SelectedMethod;
        if (!recommendation.CanProceed || recommendation.HardStops.Any() || !recommendedMethod.HasValue)
        {
            var finding = recommendation.HardStops.FirstOrDefault();
            return new MethodSelectionDecision(false,
                finding?.Code ?? "SOURCING_METHOD_RECOMMENDATION_NOT_RESOLVED",
                finding?.Message ?? "The current effective policy did not resolve one automatic procurement method.",
                recommendedMethod, requestedMethod, ProcurementSourcingMethodSelectionBasis.AutomaticRecommendation,
                recommendation, recommendation, null);
        }

        var selectedMethod = requestedMethod ?? recommendedMethod.Value;
        if (selectedMethod == recommendedMethod.Value)
        {
            return new MethodSelectionDecision(true, "SOURCING_METHOD_RECOMMENDED",
                $"The server recommends {recommendedMethod.Value} from the current effective policy and threshold.",
                recommendedMethod, selectedMethod, ProcurementSourcingMethodSelectionBasis.AutomaticRecommendation,
                recommendation, recommendation, null);
        }

        var overrideDto = new ProcurementSourcingMethodOverrideReadinessDto
        {
            IsRequested = true,
            DecisionCode = "SOURCING_METHOD_OVERRIDE_RULE_REQUIRED",
            Message = $"Changing the recommendation from {recommendedMethod.Value} to {selectedMethod} requires an effective approved exception rule."
        };
        MethodSelectionDecision Fail(string code, string message, ProcurementComplianceDecisionDto? selection = null)
        {
            overrideDto.IsEligible = false;
            overrideDto.DecisionCode = code;
            overrideDto.Message = message;
            return new MethodSelectionDecision(false, code, message, recommendedMethod, selectedMethod,
                ProcurementSourcingMethodSelectionBasis.ApprovedOverride, recommendation, selection ?? recommendation, overrideDto);
        }

        if (!requisition.ApprovedExceptionRuleId.HasValue)
            return Fail(overrideDto.DecisionCode, overrideDto.Message);

        var now = DateTime.UtcNow;
        var rule = await ExceptionRules.GetQueryable(item => item.Id == requisition.ApprovedExceptionRuleId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.PolicySet).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (rule is null)
            return Fail("SOURCING_METHOD_OVERRIDE_RULE_NOT_FOUND", "The linked exception rule is unavailable in the current tenant.");

        overrideDto.RuleId = rule.Id;
        overrideDto.RuleCode = rule.RuleCode;
        overrideDto.RuleName = rule.ExceptionName;
        overrideDto.ExceptionType = rule.ExceptionType;
        overrideDto.ApproverRole = rule.ApproverRole;
        overrideDto.WorkflowDefinitionId = rule.WorkflowDefinitionId;
        overrideDto.WorkflowInstanceId = requisition.ExceptionWorkflowInstanceId;
        overrideDto.ApprovalReference = requisition.ExceptionApprovalReference;
        overrideDto.EvidenceReference = requisition.ExceptionEvidenceReference;
        overrideDto.ApprovedAtUtc = EnsureNullableUtc(requisition.ExceptionApprovedAtUtc);

        var currentPolicyOwnsRule = rule.PolicySetId == recommendation.Policy.PolicySetId ||
            recommendation.Policy.BasePolicySetId == rule.PolicySetId;
        if (!currentPolicyOwnsRule || !rule.IsEnabled || rule.EffectiveFrom > now ||
            rule.EffectiveTo.HasValue && rule.EffectiveTo.Value < now)
            return Fail("SOURCING_METHOD_OVERRIDE_RULE_NOT_EFFECTIVE",
                "The linked exception rule is not effective in the current policy lineage.");
        if (rule.Category.HasValue && rule.Category != requisition.ProcurementCategory)
            return Fail("SOURCING_METHOD_OVERRIDE_CATEGORY_MISMATCH", "The linked exception rule does not apply to this requisition category.");
        if (rule.Method.HasValue && rule.Method != selectedMethod)
            return Fail("SOURCING_METHOD_OVERRIDE_METHOD_MISMATCH",
                $"Exception rule {rule.RuleCode} does not authorize {selectedMethod}.");
        if (rule.Disposition == ProcurementExceptionDisposition.Prohibited)
            return Fail("SOURCING_METHOD_OVERRIDE_PROHIBITED", $"Exception rule {rule.RuleCode} prohibits this override.");
        if (!rule.WorkflowDefinitionId.HasValue)
            return Fail("SOURCING_METHOD_OVERRIDE_WORKFLOW_DEFINITION_REQUIRED",
                $"Exception rule {rule.RuleCode} does not configure the required shared workflow definition.");
        if (string.IsNullOrWhiteSpace(overrideReason) || overrideReason.Trim().Length < 5)
            return Fail("SOURCING_METHOD_OVERRIDE_REASON_REQUIRED", "A method-override reason of at least five characters is required.");
        if (string.IsNullOrWhiteSpace(requisition.ExceptionEvidenceReference))
            return Fail("SOURCING_METHOD_OVERRIDE_EVIDENCE_REQUIRED", "The approved method override requires an evidence reference on the requisition.");
        if (string.IsNullOrWhiteSpace(requisition.ExceptionApprovalReference) || !requisition.ExceptionApprovedAtUtc.HasValue)
            return Fail("SOURCING_METHOD_OVERRIDE_APPROVAL_INCOMPLETE", "The method-override approval reference or approval timestamp is missing.");
        if (!requisition.ExceptionWorkflowInstanceId.HasValue)
            return Fail("SOURCING_METHOD_OVERRIDE_WORKFLOW_REQUIRED", "The method override requires a completed shared Procurement Exception workflow.");

        var workflow = await WorkflowInstances.GetQueryable(item => item.Id == requisition.ExceptionWorkflowInstanceId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.EntityType)
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.StepInstances).ThenInclude(item => item.Approvals)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (workflow is null)
            return Fail("SOURCING_METHOD_OVERRIDE_WORKFLOW_NOT_FOUND", "The linked method-override workflow is unavailable in the current tenant.");
        if (!string.Equals(workflow.EntityType.Code, "PROCUREMENT_EXCEPTION", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(workflow.EntityType.Code, "ProcurementException", StringComparison.OrdinalIgnoreCase))
            return Fail("SOURCING_METHOD_OVERRIDE_WORKFLOW_TYPE_INVALID", "The linked workflow is not a Procurement Exception workflow.");
        if (workflow.EntityId != requisition.Id)
            return Fail("SOURCING_METHOD_OVERRIDE_WORKFLOW_SUBJECT_MISMATCH", "The completed override workflow belongs to another procurement subject.");
        if (workflow.WorkflowDefinition.TenantId != _currentUser.TenantId || workflow.WorkflowDefinition.IsDeleted ||
            !workflow.WorkflowDefinition.IsActive ||
            workflow.WorkflowDefinition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published)
            return Fail("SOURCING_METHOD_OVERRIDE_WORKFLOW_DEFINITION_NOT_EFFECTIVE",
                "The linked shared workflow definition is not a published active definition in the current tenant.");
        if (workflow.Status != WorkflowInstanceStatus.Completed || !workflow.CompletedDate.HasValue)
            return Fail("SOURCING_METHOD_OVERRIDE_WORKFLOW_NOT_APPROVED", "The method-override workflow has not completed with an approved outcome.");
        if (workflow.WorkflowDefinitionId != rule.WorkflowDefinitionId.Value)
            return Fail("SOURCING_METHOD_OVERRIDE_WORKFLOW_DEFINITION_MISMATCH", "The completed workflow does not use the definition configured by the exception rule.");
        if (EnsureUtc(requisition.ExceptionApprovedAtUtc.Value) != EnsureUtc(workflow.CompletedDate.Value))
            return Fail("SOURCING_METHOD_OVERRIDE_APPROVAL_TIMESTAMP_MISMATCH",
                "The requisition approval timestamp does not match the completed shared workflow.");
        if (rule.MaximumDurationDays.HasValue && workflow.CompletedDate.Value.AddDays(rule.MaximumDurationDays.Value) < now)
            return Fail("SOURCING_METHOD_OVERRIDE_APPROVAL_EXPIRED", "The approved method override has expired under its configured maximum duration.");

        var approvedWorkflowActions = workflow.StepInstances.SelectMany(item => item.Approvals)
            .Where(item => !item.IsDeleted && item.Status == WorkflowApprovalStatus.Approved && item.ProcessedById.HasValue)
            .ToList();
        if (!approvedWorkflowActions.Any(item =>
                string.Equals(item.ApproverRole, rule.ApproverRole, StringComparison.OrdinalIgnoreCase)))
            return Fail("SOURCING_METHOD_OVERRIDE_APPROVER_ROLE_MISMATCH",
                $"The completed workflow has no approval from the exception rule role {rule.ApproverRole}.");
        var approvalActors = approvedWorkflowActions
            .Select(item => item.ProcessedById!.Value).Distinct().OrderBy(item => item).ToList();
        overrideDto.ApprovalActorUserIds = approvalActors;
        overrideDto.ApprovedAtUtc = EnsureUtc(workflow.CompletedDate.Value);
        if (approvalActors.Count == 0)
            return Fail("SOURCING_METHOD_OVERRIDE_APPROVER_REQUIRED", "The completed workflow has no traceable approved actor.");
        if (approvalActors.Contains(requisition.RequestedById) &&
            await _unitOfWork.IsProcurementSodEnabledAsync(_currentUser.TenantId, cancellationToken))
            return Fail("SOURCING_METHOD_OVERRIDE_SOD_CONFLICT", "The requisition initiator cannot approve the method override.");

        if (controlMode != OverrideControlMode.None && !HasPlatformSuperAdministratorBypass())
        {
            var capabilityRequest = new ProcurementAccessCapabilityRequest
            {
                PermissionCode = ClosePermission,
                SourceType = SourceType,
                SourceReference = requisition.RequisitionNumber
            };
            if (controlMode == OverrideControlMode.Enforce)
            {
                await EnsureCapabilityAsync(ClosePermission, requisition.RequisitionNumber, correlationId, cancellationToken);
            }
            else
            {
                try
                {
                    var capability = await _accessControl.CheckCapabilityAsync(capabilityRequest, correlationId, cancellationToken);
                    if (!capability.Allowed)
                        return Fail("SOURCING_METHOD_OVERRIDE_FORBIDDEN", capability.Message);
                }
                catch (ProcurementAccessAuthorizationException exception)
                {
                    return Fail("SOURCING_METHOD_OVERRIDE_FORBIDDEN", exception.Message);
                }
            }
        }

        if (controlMode != OverrideControlMode.None)
        {
            var sodRequest = new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-INITIATOR-APPROVER",
                SourceType = SourceType,
                SourceReference = requisition.RequisitionNumber,
                ProhibitedActorUserIds = approvalActors
            };
            var sod = controlMode == OverrideControlMode.Enforce
                ? await _sodGuard.EnforceAsync(sodRequest, correlationId, cancellationToken)
                : await _sodGuard.CheckAsync(sodRequest, correlationId, cancellationToken);
            if (!sod.Allowed) return Fail(sod.Code, sod.Message);
        }

        var selectedDecision = await EvaluateMethodAsync(requisition, selectedMethod, rule, overrideReason,
            correlationId, cancellationToken);
        if (!selectedDecision.CanProceed || selectedDecision.HardStops.Any() || selectedDecision.SelectedMethod != selectedMethod)
        {
            var finding = selectedDecision.HardStops.FirstOrDefault();
            return Fail(finding?.Code ?? "SOURCING_METHOD_OVERRIDE_NOT_ALLOWED",
                finding?.Message ?? $"The current effective policy does not allow {selectedMethod} for this demand.", selectedDecision);
        }
        if (selectedDecision.SelectedException?.RuleId != rule.Id)
            return Fail("SOURCING_METHOD_OVERRIDE_RULE_LINEAGE_INVALID",
                "The requested method did not resolve the exact approved exception rule in the current policy.", selectedDecision);

        overrideDto.IsEligible = true;
        overrideDto.DecisionCode = "SOURCING_METHOD_OVERRIDE_APPROVED";
        overrideDto.Message = $"{selectedMethod} is authorized as an approved override of the server recommendation {recommendedMethod.Value}.";
        return new MethodSelectionDecision(true, overrideDto.DecisionCode, overrideDto.Message,
            recommendedMethod, selectedMethod, ProcurementSourcingMethodSelectionBasis.ApprovedOverride,
            recommendation, selectedDecision, overrideDto);
    }

    private Task<ProcurementComplianceDecisionDto> EvaluateMethodAsync(
        PurchaseRequisition requisition,
        ProcurementMethodType? method,
        ProcurementPolicyExceptionRule? exceptionRule,
        string? overrideReason,
        string correlationId,
        CancellationToken cancellationToken) => _compliance.EvaluateAsync(new ProcurementComplianceDecisionRequest
    {
        Category = requisition.ProcurementCategory ?? ProcurementCategoryClass.Goods,
        Amount = requisition.TotalAmount,
        CurrencyCode = NormalizeCurrency(requisition.Currency),
        RequestedMethod = method,
        EvidenceStage = ProcurementEvidenceStage.Sourcing,
        SourceType = SourceType,
        SourceReference = requisition.RequisitionNumber,
        ActorUserId = _currentUser.UserId,
        ActorRoles = _currentUser.Roles.ToList(),
        SourceOwnerUserId = requisition.RequestedById,
        EntityType = SourceType,
        Action = exceptionRule is null ? "CreateSourcingCase" : "OverrideSourcingMethod",
        ExceptionType = exceptionRule?.ExceptionType,
        JustificationProvided = exceptionRule is null || !string.IsNullOrWhiteSpace(overrideReason),
        ExceptionApprovalReference = exceptionRule is null ? null : requisition.ExceptionApprovalReference,
        EvidenceReferenceKeys = exceptionRule is null || string.IsNullOrWhiteSpace(requisition.ExceptionEvidenceReference)
            ? [] : [requisition.ExceptionEvidenceReference]
    }, correlationId, cancellationToken);

    private IQueryable<ProcurementSourcingCase> CaseQuery(bool tracked)
    {
        var query = Cases.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            // A sourcing case is the retained audit record. Its requisition and line
            // principals may be soft-deleted after the operational lifecycle ends,
            // but that must not make the case itself disappear from history.
            .IgnoreQueryFilters()
            .Include(item => item.PurchaseRequisition)
            .Include(item => item.SourcingRelease)
            .Include(item => item.Lots).ThenInclude(item => item.Items).ThenInclude(item => item.PurchaseRequisitionItem)
            .Include(item => item.SourceRequests);
        return tracked ? query : query.AsNoTracking();
    }

    private static string ActiveCaseConflictMessage(ProcurementSourcingCase item)
    {
        var sourceReference = item.SourceRequests
            .Where(request => !request.IsDeleted &&
                request.Status == ProcurementSourcingCaseSourceRequestStatus.Created &&
                !string.IsNullOrWhiteSpace(request.SourceEntityReference))
            .OrderByDescending(request => request.RequestSequence)
            .Select(request => request.SourceEntityReference)
            .FirstOrDefault();
        var source = string.IsNullOrWhiteSpace(sourceReference)
            ? item.CaseNumber
            : $"{item.CaseNumber} ({sourceReference})";
        return $"This requisition is already being sourced through {source}, but that active case no longer matches the current release. Continue the existing sourcing process, or close/cancel its sourcing case before starting a replacement.";
    }

    private static string ActiveCaseConflictMessage(ProcurementSourcingCaseDto item)
    {
        var sourceReference = item.SourceRequests
            .Where(request => request.Status == ProcurementSourcingCaseSourceRequestStatus.Created &&
                !string.IsNullOrWhiteSpace(request.SourceEntityReference))
            .OrderByDescending(request => request.RequestSequence)
            .Select(request => request.SourceEntityReference)
            .FirstOrDefault();
        var source = string.IsNullOrWhiteSpace(sourceReference)
            ? item.CaseNumber
            : $"{item.CaseNumber} ({sourceReference})";
        return $"This requisition is already being sourced through {source}, but that active case no longer matches the current release. Continue the existing sourcing process, or close/cancel its sourcing case before starting a replacement.";
    }

    private async Task<ProcurementPolicyMethodRule> LoadOperationalMethodRuleAsync(
        Guid methodRuleId,
        CancellationToken cancellationToken)
    {
        return await MethodRules.GetQueryable(item => item.Id == methodRuleId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsEnabled)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementRequisitionSourcingValidationException(
                "SOURCING_CASE_METHOD_RULE_STALE",
                "The sourcing case's method rule is no longer operational in the current tenant.");
    }

    private async Task<PurchaseRequisition> LoadRequisitionAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = Requisitions.GetQueryable(item => item.Id == id && item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementSourcingCaseNotFoundException("PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");
    }

    private async Task<List<PurchaseRequisitionItem>> LoadLinesAsync(Guid requisitionId, bool tracked, CancellationToken cancellationToken)
    {
        var query = RequisitionItems.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
            item.RequisitionId == requisitionId && !item.IsDeleted).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id);
        if (!tracked) return await query.AsNoTracking().ToListAsync(cancellationToken);
        return await query.ToListAsync(cancellationToken);
    }

    private static List<LotSpec> ValidateLots(
        IReadOnlyList<CreateProcurementSourcingCaseLotRequest> requests,
        IReadOnlyList<PurchaseRequisitionItem> lines,
        PurchaseRequisitionSourcingReleaseDto release,
        PurchaseRequisition requisition)
    {
        if (requests.Count == 0) throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_LOTS_REQUIRED", "At least one sourcing lot is required.");
        var lineById = lines.ToDictionary(item => item.Id);
        var allIds = requests.SelectMany(item => item.PurchaseRequisitionItemIds).ToList();
        if (allIds.Any(item => item == Guid.Empty) || allIds.Distinct().Count() != allIds.Count)
            throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_LINE_DUPLICATE", "Each requisition line must belong to exactly one lot.");
        if (allIds.Count != lines.Count || allIds.Any(item => !lineById.ContainsKey(item)))
            throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_LINE_COVERAGE", "Every current requisition line must be assigned to exactly one lot.");
        if (release.SourcePlanItemId != requisition.SourcePlanItemId)
            throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_PLAN_LINEAGE", "The release does not match the requisition's current plan-item lineage.");
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var specs = new List<LotSpec>();
        for (var index = 0; index < requests.Count; index++)
        {
            var item = requests[index];
            if (string.IsNullOrWhiteSpace(item.Title)) throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_LOT_TITLE_REQUIRED", $"Lot {index + 1} requires a title.");
            var code = string.IsNullOrWhiteSpace(item.LotCode) ? $"LOT-{index + 1:00}" : item.LotCode.Trim().ToUpperInvariant();
            if (!codes.Add(code)) throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_LOT_CODE_DUPLICATE", $"Lot code {code} is duplicated.");
            var value = item.PurchaseRequisitionItemIds.Sum(id => lineById[id].LineTotal);
            if (value <= 0) throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_LOT_VALUE_INVALID", $"Lot {code} must contain positive-value requisition lines.");
            specs.Add(new LotSpec(code, item.Title.Trim(), NullIfWhiteSpace(item.Description), value, item.PurchaseRequisitionItemIds.OrderBy(id => id).ToArray()));
        }
        if (specs.Sum(item => item.EstimatedValue) != requisition.TotalAmount)
            throw new ProcurementSourcingCaseValidationException("SOURCING_CASE_VALUE_MISMATCH", "The lot values must reconcile exactly to the released requisition total.");
        return specs;
    }

    private async Task<ProcurementSourcingCaseDto> MapAsync(ProcurementSourcingCase item, CancellationToken cancellationToken)
    {
        var state = await EvaluateSourceStateAsync(item, cancellationToken);
        return new ProcurementSourcingCaseDto
        {
            Id = item.Id, CaseNumber = item.CaseNumber, CaseSequence = item.CaseSequence,
            RequisitionId = item.PurchaseRequisitionId, RequisitionNumber = item.PurchaseRequisition.RequisitionNumber,
            SourcingReleaseId = item.SourcingReleaseId, SourcingReleaseReference = item.SourcingRelease.ReleaseReference,
            SourcePlanId = item.SourcePlanId, SourcePlanItemId = item.SourcePlanItemId,
            SourcePlanNumber = item.PurchaseRequisition.SourcePlanNumber,
            SourcePlanItemDescription = item.PurchaseRequisition.SourcePlanItemDescription,
            Category = item.Category, RecommendedMethod = item.RecommendedMethod, SelectedMethod = item.SelectedMethod,
            MethodSelectionBasis = item.MethodSelectionBasis, EstimatedValue = item.EstimatedValue,
            CurrencyCode = item.CurrencyCode, PolicySetId = item.PolicySetId, PolicyCode = item.PolicyCode,
            PolicyVersion = item.PolicyVersion, MethodRuleId = item.MethodRuleId, MethodRuleCode = item.MethodRuleCode,
            ThresholdRuleId = item.ThresholdRuleId, ThresholdRuleCode = item.ThresholdRuleCode,
            AuthorityRouteId = item.AuthorityRouteId, AuthorityRouteReference = item.AuthorityRouteReference,
            ApprovedExceptionRuleId = item.ApprovedExceptionRuleId,
            MethodOverrideWorkflowInstanceId = item.MethodOverrideWorkflowInstanceId,
            MethodOverrideReason = item.MethodOverrideReason,
            MethodOverrideApprovalActorUserIds = DeserializeIds(item.MethodOverrideApprovalActorsJson),
            MethodOverrideApprovedAtUtc = EnsureNullableUtc(item.MethodOverrideApprovedAtUtc),
            ExceptionApprovalReference = item.ExceptionApprovalReference,
            ExceptionEvidenceReference = item.ExceptionEvidenceReference, Justification = item.Justification,
            Status = item.Status, IsSourceCurrent = state.Current, SourceStateCode = state.Code,
            SourceStateMessage = state.Message, CreatedAtUtc = EnsureUtc(item.CreatedAt), CreatedById = item.CreatedById,
            CreatedByName = item.CreatedByName, StartedAtUtc = EnsureNullableUtc(item.StartedAtUtc), StartedByName = item.StartedByName,
            ClosedAtUtc = EnsureNullableUtc(item.ClosedAtUtc), ClosedByName = item.ClosedByName, ClosureReason = item.ClosureReason,
            SourceControlFingerprint = item.SourceControlFingerprint, CaseFingerprint = item.CaseFingerprint,
            IntegrityHash = item.IntegrityHash, RowVersion = Convert.ToBase64String(item.RowVersion),
            Lots = item.Lots.OrderBy(lot => lot.LotNumber).Select(lot => new ProcurementSourcingCaseLotDto
            {
                Id = lot.Id, LotNumber = lot.LotNumber, LotCode = lot.LotCode, Title = lot.Title,
                Description = lot.Description, EstimatedValue = lot.EstimatedValue, CurrencyCode = lot.CurrencyCode,
                Items = lot.Items.OrderBy(line => line.PurchaseRequisitionItem.CreatedAt).Select(line => new ProcurementSourcingCaseLotItemDto
                {
                    RequisitionItemId = line.PurchaseRequisitionItemId,
                    Description = line.PurchaseRequisitionItem.ItemDescription,
                    Quantity = line.PurchaseRequisitionItem.Quantity,
                    UnitOfMeasure = line.PurchaseRequisitionItem.UnitOfMeasure,
                    EstimatedUnitPrice = line.PurchaseRequisitionItem.EstimatedUnitPrice,
                    LineTotal = line.PurchaseRequisitionItem.LineTotal
                }).ToList()
            }).ToList(),
            SourceRequests = item.SourceRequests.OrderBy(request => request.RequestSequence).Select(request => new ProcurementSourcingCaseSourceRequestDto
            {
                Id = request.Id, RequestSequence = request.RequestSequence, RequestReference = request.RequestReference,
                SourceType = request.SourceType, Method = request.Method, Status = request.Status,
                LotIds = DeserializeIds(request.LotIdsJson), PlannedAtUtc = EnsureUtc(request.PlannedAtUtc),
                SourceEntityId = request.SourceEntityId, SourceEntityReference = request.SourceEntityReference,
                RegisteredAtUtc = EnsureNullableUtc(request.RegisteredAtUtc), RegisteredById = request.RegisteredById,
                RegisteredByName = request.RegisteredByName
            }).ToList()
        };
    }

    private async Task RecordAsync(ProcurementSourcingCase item, string action, ProcurementControlEventResult result,
        object resultValues, string correlationId, CancellationToken cancellationToken)
    {
        var evidence = new List<ProcurementControlEventEvidenceReference>
        {
            External(item.SourcingRelease?.ReleaseReference ?? item.SourcingReleaseId.ToString("N"), "Immutable sourcing release", "SOURCING_RELEASE")
        };
        if (!string.IsNullOrWhiteSpace(item.AuthorityRouteReference))
            evidence.Add(External(item.AuthorityRouteReference, "PR authority route", "AUTHORITY_ROUTE"));
        if (!string.IsNullOrWhiteSpace(item.ExceptionApprovalReference))
            evidence.Add(External(item.ExceptionApprovalReference, "Approved exception", "EXCEPTION"));
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create($"sourcing-case-{action.ToLowerInvariant()}", item.TenantId,
                item.Id, _currentUser.UserId, correlationId),
            EventType = EventType, Action = action, Result = result,
            RuleCode = action.StartsWith("SourcingMethod", StringComparison.Ordinal) ? "TDC-0202" : "TDC-0201",
            RuleId = action == "SourcingMethodOverrideApproved" ? item.ApprovedExceptionRuleId : item.MethodRuleId,
            RuleVersion = item.PolicyVersion.ToString(),
            DecisionKeys = action.StartsWith("SourcingMethod", StringComparison.Ordinal)
                ? ["DEC-001", "DEC-004", "DEC-006"]
                : ["DEC-001", "DEC-002", "DEC-003", "DEC-004", "DEC-006", "DEC-009", "DEC-010"],
            SourceType = SourceType, SourceId = item.Id, SourceReference = item.CaseNumber,
            Reason = item.Justification, InputValues = new
            {
                item.PurchaseRequisitionId, item.SourcingReleaseId, item.RecommendedMethod, item.SelectedMethod,
                item.MethodSelectionBasis, item.EstimatedValue, item.CurrencyCode, item.PolicySetId, item.PolicyVersion,
                item.MethodRuleId, item.ThresholdRuleId, item.MethodOverrideWorkflowInstanceId
            },
            ResultValues = resultValues, After = new { item.Status, item.StartedAtUtc, item.ClosedAtUtc, item.ClosureReason },
            Evidence = evidence, CorrelationId = correlationId, OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task RecordMethodDecisionAsync(
        PurchaseRequisition requisition,
        MethodSelectionDecision decision,
        string? reason,
        ProcurementControlEventResult result,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var evidence = new List<ProcurementControlEventEvidenceReference>();
        if (!string.IsNullOrWhiteSpace(requisition.ExceptionApprovalReference))
            evidence.Add(External(requisition.ExceptionApprovalReference, "Method override approval", "EXCEPTION"));
        if (!string.IsNullOrWhiteSpace(requisition.ExceptionEvidenceReference))
            evidence.Add(External(requisition.ExceptionEvidenceReference, "Method override evidence", "METHOD_OVERRIDE"));
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("sourcing-method-decision", _currentUser.TenantId,
                requisition.Id, _currentUser.UserId, correlationId),
            EventType = EventType,
            Action = decision.MethodSelectionBasis == ProcurementSourcingMethodSelectionBasis.ApprovedOverride
                ? "SourcingMethodOverrideDenied"
                : "SourcingMethodRecommendationDenied",
            Result = result,
            RuleCode = "TDC-0202",
            RuleId = decision.Override?.RuleId,
            RuleVersion = decision.Selection.Policy.Version.ToString(),
            DecisionKeys = ["DEC-001", "DEC-004", "DEC-006"],
            SourceType = "PurchaseRequisition",
            SourceId = requisition.Id,
            SourceReference = requisition.RequisitionNumber,
            Reason = NullIfWhiteSpace(reason),
            InputValues = new
            {
                decision.RecommendedMethod, decision.SelectedMethod, decision.MethodSelectionBasis,
                requisition.ApprovedExceptionRuleId, requisition.ExceptionWorkflowInstanceId
            },
            ResultValues = new { decision.DecisionCode, decision.Message },
            Evidence = evidence,
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task EnsureCapabilityAsync(string permission, string reference, string correlationId, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (HasPlatformSuperAdministratorBypass()) return;
        try
        {
            var result = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission, SourceType = SourceType, SourceReference = reference
            }, correlationId, cancellationToken);
            if (!result.Allowed) throw new ProcurementSourcingCaseAuthorizationException(result.Message);
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            throw new ProcurementSourcingCaseAuthorizationException(exception.Message);
        }
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementSourcingCaseAuthorizationException("An authenticated tenant context is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() => _currentUser.HasRole(Constants.Roles.SuperAdmin);
    private string ActorName() => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private void Touch(ProcurementSourcingCase item, DateTime now)
    {
        item.UpdatedAt = now; item.UpdatedBy = _currentUser.Username; item.LastModifiedById = _currentUser.UserId;
        item.RowVersion = Guid.NewGuid().ToByteArray();
    }
    private static void ValidateCreate(CreateProcurementSourcingCaseRequest request)
    {
        if (request.RequisitionId == Guid.Empty) throw new ProcurementSourcingCaseValidationException("REQUISITION_REQUIRED", "A released requisition is required.");
    }

    private static string BuildAutomaticJustification(
        ProcurementMethodType method,
        string policyCode,
        int policyVersion,
        ProcurementCategoryClass category,
        decimal amount,
        string currencyCode,
        string methodRuleCode,
        ProcurementPolicyThresholdRule threshold,
        string? operationalNotes)
    {
        var amountText = amount.ToString("N2", CultureInfo.InvariantCulture);
        var lowerOperator = threshold.LowerInclusive ? ">=" : ">";
        var upperText = threshold.UpperBound.HasValue
            ? $" and {(threshold.UpperInclusive ? "<=" : "<")} {currencyCode} {threshold.UpperBound.Value.ToString("N2", CultureInfo.InvariantCulture)}"
            : " with no upper limit";
        var rationale = $"{MethodLabel(method)} was selected automatically by policy {policyCode} v{policyVersion}. " +
                        $"Category: {category}; evaluated amount: {currencyCode} {amountText}; " +
                        $"matched threshold {threshold.RuleCode}: {lowerOperator} {currencyCode} {threshold.LowerBound.ToString("N2", CultureInfo.InvariantCulture)}{upperText}; " +
                        $"method rule: {methodRuleCode}.";
        var notes = NullIfWhiteSpace(operationalNotes);
        return Truncate(notes is null ? rationale : $"{rationale} Operational notes: {notes}", 1000);
    }

    private static string MethodLabel(ProcurementMethodType method) => method switch
    {
        ProcurementMethodType.RequestForQuotation => "Request for quotation",
        ProcurementMethodType.NationalCompetitiveTendering => "National competitive tendering",
        ProcurementMethodType.InternationalCompetitiveTendering => "International competitive tendering",
        ProcurementMethodType.RestrictedTendering => "Restricted tendering",
        ProcurementMethodType.SingleSource => "Single source",
        ProcurementMethodType.PettyPurchase => "Petty purchase",
        ProcurementMethodType.FrameworkCallOff => "Framework call-off",
        ProcurementMethodType.QualityBasedSelection => "Quality-based selection",
        ProcurementMethodType.QualityAndCostBasedSelection => "Quality and cost-based selection",
        _ => method.ToString()
    };
    private static void EnsureReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 5)
            throw new ProcurementSourcingCaseValidationException("REASON_REQUIRED", "A reason or justification of at least five characters is required.");
    }
    private static void EnsureRowVersion(byte[] current, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied)) throw new ProcurementSourcingCaseValidationException("ROW_VERSION_REQUIRED", "RowVersion is required.");
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw new ProcurementSourcingCaseValidationException("ROW_VERSION_INVALID", "RowVersion must be valid Base64."); }
        if (!current.SequenceEqual(parsed)) throw new ProcurementSourcingCaseConflictException("SOURCING_CASE_CONCURRENCY", "The sourcing case changed after it was loaded. Refresh and retry.");
    }
    private static string ExpectedSourceType(ProcurementMethodType method) =>
        method == ProcurementMethodType.RequestForQuotation ? "RequestForQuotation" : "Tender";
    private static ProcurementSourcingCaseLineOptionDto MapLine(PurchaseRequisitionItem item, int lineNumber) => new()
    {
        Id = item.Id, LineNumber = lineNumber, Description = item.ItemDescription, Quantity = item.Quantity,
        UnitOfMeasure = item.UnitOfMeasure, EstimatedUnitPrice = item.EstimatedUnitPrice, LineTotal = item.LineTotal
    };
    private static List<Guid> DeserializeIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? EnsureNullableUtc(DateTime? value) => value.HasValue ? EnsureUtc(value.Value) : null;
    private static string NormalizeCurrency(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string ComputeHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static ProcurementControlEventEvidenceReference External(string reference, string label, string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference, Label = label, RequirementKey = requirement
    };

    private sealed record MethodSelectionDecision(
        bool IsEligible,
        string DecisionCode,
        string Message,
        ProcurementMethodType? RecommendedMethod,
        ProcurementMethodType? SelectedMethod,
        ProcurementSourcingMethodSelectionBasis MethodSelectionBasis,
        ProcurementComplianceDecisionDto Recommendation,
        ProcurementComplianceDecisionDto Selection,
        ProcurementSourcingMethodOverrideReadinessDto? Override);
    private enum OverrideControlMode { None, Check, Enforce }
    private sealed record LotSpec(string Code, string Title, string? Description, decimal EstimatedValue, Guid[] ItemIds);
    private sealed record SourceState(bool Current, string Code, string Message);
}
