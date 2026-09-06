using ErpSystem.Shared;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.Configuration;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementSupplierRiskService : IProcurementSupplierRiskService
{
    private const string SourceType = "ProcurementSupplierRisk";
    private const string EventType = "ProcurementSupplierRiskControl";
    private const string ReviewPermission = "procurement.supplier.review";
    private const string ApprovePermission = "procurement.supplier.approve";
    private const string ReportingPermission = "procurement.reports.read";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToArray();
    private static readonly HashSet<string> IncludedOrderStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Approved", "Sent", "Acknowledged", "PartiallyReceived", "Received", "Completed", "Closed"
        };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementSupplierRiskService> _logger;

    public ProcurementSupplierRiskService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        ISupplierValidationService supplierValidation,
        IWorkflowInstanceService workflowInstances,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementSupplierRiskService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _supplierValidation = supplierValidation;
        _workflowInstances = workflowInstances;
        _notificationTopics = notificationTopics;
        _logger = logger;
    }

    private IGenericRepository<ProcurementSupplierRiskAssessment> Assessments =>
        _unitOfWork.Repository<ProcurementSupplierRiskAssessment>();
    private IGenericRepository<ProcurementSupplierRiskAlert> Alerts =>
        _unitOfWork.Repository<ProcurementSupplierRiskAlert>();

    public async Task<ProcurementSupplierRiskSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInternalReaderAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var policy = await TryResolvePolicyAsync(now, cancellationToken);
        var suppliers = await SupplierQuery().Select(item => item.Id).ToListAsync(cancellationToken);
        var assessments = await AssessmentQuery().Include(item => item.Alerts)
            .AsNoTracking().ToListAsync(cancellationToken);
        var current = assessments.GroupBy(item => item.BusinessPartnerId)
            .Select(group => group.OrderByDescending(item => item.AssessedAtUtc)
                .ThenByDescending(item => item.AssessmentSequence).First())
            .ToList();
        return new ProcurementSupplierRiskSummaryDto
        {
            SupplierCount = suppliers.Count,
            AssessedSupplierCount = current.Count,
            CurrentAssessmentCount = policy is null ? 0 : current.Count(item =>
                IsCurrent(item, policy, now)),
            OverdueAssessmentCount = current.Count(item => item.NextReviewDueAtUtc <= now),
            OpenAlertCount = current.SelectMany(item => item.Alerts)
                .Count(item => !item.IsDeleted &&
                    item.Status == ProcurementSupplierRiskAlertStatus.Open),
            EscalatedAlertCount = current.SelectMany(item => item.Alerts)
                .Count(item => !item.IsDeleted &&
                    item.Status == ProcurementSupplierRiskAlertStatus.Escalated),
            AwardBlockedSupplierCount = policy is null ? 0 : current.Count(item =>
                MatchesPolicy(item, policy) && AwardBlocked(item, now)),
            PolicyAvailable = policy is not null,
            PolicyProfileCode = policy?.Decision.Profile.ProfileCode,
            PolicyProfileVersion = policy?.Decision.Profile.Version,
            ExposureWindowMonths = policy?.Value.ExposureWindowMonths,
            ConcentrationLimitPercent = policy?.Value.ConcentrationLimitPercent,
            MinimumScore = policy?.Value.MinimumScore,
            EligibilityAction = policy?.Value.EligibilityAction,
            PolicyReleaseGate = policy is null
                ? "No unique valid Published/effective/approved/evidenced DEC-011 risk policy is available."
                : null
        };
    }

    public async Task<ProcurementSupplierRiskPageDto> SearchAsync(
        ProcurementSupplierRiskSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureInternalReaderAsync(cancellationToken);
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 100);
        var now = DateTime.UtcNow;
        var rows = await AssessmentQuery()
            .Include(item => item.BusinessPartner)
            .Include(item => item.Alerts)
            .AsNoTracking().ToListAsync(cancellationToken);
        var latest = rows.GroupBy(item => item.BusinessPartnerId)
            .Select(group => group.OrderByDescending(item => item.AssessedAtUtc)
                .ThenByDescending(item => item.AssessmentSequence).First())
            .AsEnumerable();
        if (request.BusinessPartnerId.HasValue)
            latest = latest.Where(item =>
                item.BusinessPartnerId == request.BusinessPartnerId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            latest = latest.Where(item =>
                item.AssessmentReference.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.BusinessPartner.PartnerCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.BusinessPartner.PartnerName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(request.RiskBand))
            latest = latest.Where(item =>
                string.Equals(item.RiskBand, request.RiskBand.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        if (request.HasOpenAlerts.HasValue)
            latest = latest.Where(item =>
                HasUnresolvedAlerts(item) == request.HasOpenAlerts.Value);
        var materialized = latest.OrderByDescending(item => item.AssessedAtUtc).ToList();
        return new ProcurementSupplierRiskPageDto
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = materialized.Count,
            Items = materialized.Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize).Select(item => MapList(item, now)).ToList()
        };
    }

    public async Task<ProcurementSupplierRiskAssessmentDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await EnsureInternalReaderAsync(cancellationToken);
        var entity = await AssessmentQuery()
            .Include(item => item.BusinessPartner)
            .Include(item => item.Alerts)
            .AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound("SUPPLIER_RISK_ASSESSMENT_NOT_FOUND",
                "The supplier-risk assessment was not found in the current tenant.");
        return Map(entity, DateTime.UtcNow);
    }

    public async Task<ProcurementSupplierRiskCurrentStateDto> GetCurrentStateAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInternalReaderAsync(cancellationToken);
        var partner = await SupplierQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == businessPartnerId, cancellationToken)
            ?? throw NotFound("SUPPLIER_RISK_SUPPLIER_NOT_FOUND",
                "The supplier was not found in the current tenant.");
        var now = DateTime.UtcNow;
        var policy = await TryResolvePolicyAsync(now, cancellationToken);
        var entity = await AssessmentQuery()
            .Where(item => item.BusinessPartnerId == businessPartnerId)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Alerts)
            .AsNoTracking().OrderByDescending(item => item.AssessedAtUtc)
            .ThenByDescending(item => item.AssessmentSequence)
            .FirstOrDefaultAsync(cancellationToken);
        var reasons = CurrentAwardBlockReasons(entity, policy, now);
        return new ProcurementSupplierRiskCurrentStateDto
        {
            BusinessPartnerId = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PolicyAvailable = policy is not null,
            PolicyReleaseGate = policy is null
                ? "A unique valid Published/effective/approved/evidenced DEC-011 risk policy is required."
                : null,
            Assessment = entity is null ? null : Map(entity, now),
            Current = entity is not null && policy is not null && IsCurrent(entity, policy, now),
            AwardBlocked = reasons.Count != 0,
            AwardBlockReasons = reasons
        };
    }

    public async Task<IReadOnlyList<ProcurementSupplierRiskSupplierOptionDto>>
        GetSupplierOptionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInternalReaderAsync(cancellationToken);
        return await SupplierQuery().AsNoTracking()
            .OrderBy(item => item.PartnerName)
            .Select(item => new ProcurementSupplierRiskSupplierOptionDto
            {
                Id = item.Id,
                Code = item.PartnerCode,
                Name = item.PartnerName
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementSupplierRiskWorkflowOptionDto>>
        GetWorkflowOptionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInternalReaderAsync(cancellationToken);
        return await _unitOfWork.Repository<WorkflowDefinition>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new ProcurementSupplierRiskWorkflowOptionDto
            {
                Id = item.Id,
                Name = item.Name,
                Version = item.Version
            }).ToListAsync(cancellationToken);
    }

    public async Task<ProcurementSupplierRiskAssessmentDto> EvaluateAsync(
        EvaluateProcurementSupplierRiskRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        if (request.BusinessPartnerId == Guid.Empty)
            throw Validation("SUPPLIER_RISK_SUPPLIER_REQUIRED", "A supplier is required.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw Validation("SUPPLIER_RISK_IDEMPOTENCY_REQUIRED", "An idempotency key is required.");
        var idempotency = request.IdempotencyKey.Trim().Length <= 100
            ? request.IdempotencyKey.Trim()
            : Hash(request.IdempotencyKey);
        await EnsureCapabilityAsync(ReviewPermission, request.BusinessPartnerId.ToString("N"),
            correlation, cancellationToken);
        var replay = await AssessmentQuery().Include(item => item.BusinessPartner)
            .Include(item => item.Alerts)
            .AsNoTracking().SingleOrDefaultAsync(item =>
                item.IdempotencyKey == idempotency, cancellationToken);
        if (replay is not null) return Map(replay, DateTime.UtcNow);

        var partner = await SupplierQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.BusinessPartnerId, cancellationToken)
            ?? throw NotFound("SUPPLIER_RISK_SUPPLIER_NOT_FOUND",
                "The supplier was not found in the current tenant.");
        var now = DateTime.UtcNow;
        var policy = await ResolvePolicyAsync(now, cancellationToken);
        var dimensions = ParseDimensions(policy.Value.RiskDimensions);
        var bands = ParseBands(policy.Value.RiskBands);
        var periodStart = now.AddMonths(-policy.Value.ExposureWindowMonths);
        var eligibility = await _supplierValidation.EvaluateEligibilityAsync(
            new SupplierEligibilityEvaluationRequest
            {
                BusinessPartnerId = partner.Id,
                Boundary = SupplierEligibilityBoundary.StatusReview,
                SkipRiskAssessment = true
            }, cancellationToken);
        var orders = await _unitOfWork.Repository<PurchaseOrder>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.OrderDate >= periodStart && item.OrderDate <= now)
            .Include(item => item.Items).ThenInclude(item => item.InventoryItem)
                .ThenInclude(item => item!.Category)
            .AsNoTracking().ToListAsync(cancellationToken);
        orders = orders.Where(item => IncludedOrderStatuses.Contains(item.Status)).ToList();
        var spend = BuildSpendExposure(orders, partner.Id);
        var categories = BuildCategoryExposure(orders, partner.Id);
        var findings = new List<ProcurementSupplierRiskFindingDto>();
        if (orders.Any(item => string.IsNullOrWhiteSpace(item.Currency)))
            findings.Add(DataGap("SUPPLIER_RISK_CURRENCY_REQUIRED",
                "One or more included purchase orders has no currency code."));
        if (orders.SelectMany(item => item.Items).Any(item => item.InventoryItemId.HasValue &&
                item.InventoryItem?.Category is null))
            findings.Add(DataGap("SUPPLIER_RISK_CATEGORY_MAPPING_INCOMPLETE",
                "One or more purchase-order items cannot be resolved to a current inventory category."));

        var metric = await _unitOfWork
            .Repository<ProcurementSupplierPerformanceScorecard>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id && !item.IsDeleted &&
                item.CalculatedAtUtc >= periodStart &&
                item.CalculatedAtUtc <= now &&
                item.DataStatus ==
                ProcurementSupplierPerformanceDataStatus.Complete)
            .AsNoTracking().OrderByDescending(item => item.CalculatedAtUtc)
            .ThenByDescending(item => item.ScorecardSequence)
            .FirstOrDefaultAsync(cancellationToken);
        var dimensionRows = BuildDimensionScores(
            dimensions, partner, eligibility, metric, spend, categories,
            orders.All(item => item.BusinessPartnerId != partner.Id), findings);
        var dataComplete = dimensionRows.All(item => item.Score.HasValue) &&
            findings.All(item => !item.DataGap);
        decimal? score = dataComplete
            ? ProcurementSupplierRiskFormula.CalculateWeightedScore(
                dimensionRows.Select(item =>
                    new ProcurementSupplierRiskWeightedScoreInput(
                        item.Score!.Value, item.WeightPercent)))
            : null;
        var band = score.HasValue ? ResolveBand(bands, score.Value) : null;
        if (score.HasValue && band is null)
        {
            findings.Add(DataGap("SUPPLIER_RISK_BAND_UNRESOLVED",
                "The calculated score does not resolve to exactly one configured risk band."));
            dataComplete = false;
            score = null;
        }
        var maximumShare = spend.Count == 0 ? 0 : spend.Max(item => item.SpendSharePercent);
        var concentrationBreach = maximumShare > policy.Value.ConcentrationLimitPercent;
        var singleSource = categories.Any(item => item.IsSingleSource);
        var scoreBreach = score.HasValue && score.Value < policy.Value.MinimumScore;
        if (!dataComplete)
            findings.Add(Breach("SUPPLIER_RISK_DATA_INCOMPLETE",
                "The configured supplier-risk score cannot be completed from authoritative current data.",
                dataGap: true));
        if (scoreBreach)
            findings.Add(Breach("SUPPLIER_RISK_MINIMUM_SCORE_BREACHED",
                $"Risk score {score:0.00} is below the approved minimum {policy.Value.MinimumScore:0.00}."));
        if (concentrationBreach)
            findings.Add(Breach("SUPPLIER_RISK_CONCENTRATION_LIMIT_BREACHED",
                $"Maximum spend share {maximumShare:0.00}% exceeds the approved {policy.Value.ConcentrationLimitPercent:0.00}% limit."));
        if (singleSource)
            findings.Add(Breach("SUPPLIER_RISK_SINGLE_SOURCE_DEPENDENCY",
                $"{categories.Count(item => item.IsSingleSource)} category/currency exposure(s) depend on this supplier alone."));

        var sequence = await Assessments.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id)
            .MaxAsync(item => (int?)item.AssessmentSequence, cancellationToken) ?? 0;
        sequence++;
        var assessmentId = Guid.NewGuid();
        var assessment = new ProcurementSupplierRiskAssessment
        {
            Id = assessmentId,
            TenantId = _currentUser.TenantId,
            AssessmentReference =
                $"SRISK-{now:yyyyMMdd}-{assessmentId.ToString("N")[..8].ToUpperInvariant()}",
            AssessmentSequence = sequence,
            BusinessPartnerId = partner.Id,
            AssessedAtUtc = now,
            PeriodStartUtc = periodStart,
            PeriodEndUtc = now,
            NextReviewDueAtUtc = now.AddMonths(policy.Value.ReviewFrequencyMonths),
            PolicyDecisionId = policy.Decision.Id,
            PolicyProfileId = policy.Decision.ProfileId,
            PolicyProfileCode = policy.Decision.Profile.ProfileCode,
            PolicyProfileVersion = policy.Decision.Profile.Version,
            PolicySnapshotJson = policy.Decision.ValueJson,
            PolicyValueHash = Hash(policy.Decision.ValueJson),
            ExposureWindowMonths = policy.Value.ExposureWindowMonths,
            MinimumScore = policy.Value.MinimumScore,
            ConcentrationLimitPercent = policy.Value.ConcentrationLimitPercent,
            RiskScore = score,
            RiskBand = band,
            EligibilityAction = policy.Value.EligibilityAction,
            DataComplete = dataComplete,
            MinimumScoreBreached = scoreBreach,
            ConcentrationBreached = concentrationBreach,
            SingleSourceDependency = singleSource,
            MaximumSpendSharePercent = maximumShare,
            SingleSourceCategoryCount = categories.Count(item => item.IsSingleSource),
            DimensionScoresJson = Serialize(dimensionRows),
            SpendExposureJson = Serialize(spend),
            CategoryExposureJson = Serialize(categories),
            EligibilitySnapshotJson = Serialize(eligibility),
            EligibilityDecisionHash = eligibility.DecisionHash,
            FindingsJson = Serialize(findings),
            IdempotencyKey = idempotency,
            CorrelationId = correlation,
            SourceType = Trim(request.SourceType, 100),
            SourceId = request.SourceId,
            SourceReference = Trim(request.SourceReference, 100),
            AssessedByUserId = _currentUser.UserId,
            AssessedByName = ActorName,
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId
        };
        Capture(assessment);
        var alerts = BuildAlerts(assessment, findings, correlation, now);
        await ExecuteAsync(async () =>
        {
            await Assessments.AddAsync(assessment);
            foreach (var alert in alerts) await Alerts.AddAsync(alert);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        assessment.Alerts = alerts;
        await RecordAssessmentEventAsync(assessment, findings, correlation, now, cancellationToken);
        if (alerts.Count != 0)
            await PublishNotificationAsync("procurement.supplier-risk.alerts-opened",
                assessment, alerts.Count, cancellationToken);
        _logger.LogInformation(
            "Supplier risk assessment {AssessmentReference} recorded for {PartnerId} with score {RiskScore} and {AlertCount} alert(s)",
            assessment.AssessmentReference, partner.Id, assessment.RiskScore, alerts.Count);
        // Keep the read-only supplier out of this tracked assessment graph. A later
        // audit/notification save can otherwise attach it as Modified and falsely
        // invalidate an already completed tender recommendation.
        return Map(assessment, now, partner);
    }

    public async Task<ProcurementSupplierRiskAlertDto> EscalateAsync(
        Guid alertId,
        EscalateProcurementSupplierRiskAlertRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var alert = await LoadAlertAsync(alertId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ReviewPermission, alert.Assessment.AssessmentReference,
            correlation, cancellationToken);
        if (alert.Status == ProcurementSupplierRiskAlertStatus.Escalated &&
            alert.LastOperationCorrelationId == correlation)
            return MapAlert(alert);
        EnsureRowVersion(alert.RowVersion, request.RowVersion);
        if (alert.Status != ProcurementSupplierRiskAlertStatus.Open)
            throw Conflict("SUPPLIER_RISK_ALERT_NOT_OPEN",
                "Only an Open supplier-risk alert can be escalated.");
        await ValidateEvidenceAsync(request.Evidence, cancellationToken);
        var workflow = await ValidateWorkflowAsync(request.WorkflowDefinitionId, cancellationToken);
        var now = DateTime.UtcNow;
        var before = AlertSnapshot(alert);
        var instance = await _workflowInstances.StartWorkflowAsync(
            workflow.Id, workflow.EntityTypeId, alert.Id.ToString(), _currentUser.UserId,
            new
            {
                alert.Assessment.AssessmentReference,
                alert.BusinessPartnerId,
                alert.AlertType,
                alert.RuleCode,
                alert.Message
            }, cancellationToken);
        alert.Status = ProcurementSupplierRiskAlertStatus.Escalated;
        alert.WorkflowDefinitionId = workflow.Id;
        alert.WorkflowInstanceId = instance.Id;
        alert.EscalatedById = _currentUser.UserId;
        alert.EscalatedAtUtc = now;
        alert.EscalationReason = request.Reason.Trim();
        alert.EscalationEvidenceJson = Serialize(request.Evidence);
        Touch(alert, "Escalated", correlation, now);
        Capture(alert);
        await Alerts.UpdateAsync(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAlertEventAsync(alert, "Escalated",
            ProcurementControlEventResult.ReviewRequired, before, AlertSnapshot(alert),
            request.Reason, request.Evidence, correlation, now, cancellationToken);
        await PublishNotificationAsync("procurement.supplier-risk.alert-escalated",
            alert.Assessment, 1, cancellationToken);
        return MapAlert(alert);
    }

    public async Task<ProcurementSupplierRiskAlertDto> ResolveAsync(
        Guid alertId,
        ResolveProcurementSupplierRiskAlertRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var alert = await LoadAlertAsync(alertId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, alert.Assessment.AssessmentReference,
            correlation, cancellationToken);
        if (alert.Status == ProcurementSupplierRiskAlertStatus.Resolved &&
            alert.LastOperationCorrelationId == correlation)
            return MapAlert(alert);
        EnsureRowVersion(alert.RowVersion, request.RowVersion);
        if (alert.Status != ProcurementSupplierRiskAlertStatus.Escalated)
            throw Conflict("SUPPLIER_RISK_ALERT_NOT_ESCALATED",
                "Only an Escalated supplier-risk alert can be resolved.");
        await ValidateEvidenceAsync(request.Evidence, cancellationToken);
        if (!alert.EscalatedById.HasValue || alert.EscalatedById == Guid.Empty)
            throw Conflict("SUPPLIER_RISK_ESCALATION_ACTOR_MISSING",
                "The escalation actor was not retained.");
        var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = SourceType,
            SourceReference = alert.Assessment.AssessmentReference,
            ProhibitedActorUserIds = [alert.EscalatedById.Value]
        }, correlation, cancellationToken);
        if (!sod.Allowed)
            throw new ProcurementSupplierRiskAuthorizationException(sod.Message);
        var workflow = await _unitOfWork.Repository<WorkflowInstance>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == alert.WorkflowInstanceId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Conflict("SUPPLIER_RISK_WORKFLOW_INSTANCE_NOT_FOUND",
                "The escalation workflow instance is unavailable.");
        if (workflow.WorkflowDefinitionId != alert.WorkflowDefinitionId ||
            workflow.EntityId != alert.Id ||
            workflow.Status != WorkflowInstanceStatus.Completed)
            throw Conflict("SUPPLIER_RISK_WORKFLOW_NOT_APPROVED",
                "Only the Completed escalation workflow bound to this alert permits resolution.");
        var now = DateTime.UtcNow;
        var before = AlertSnapshot(alert);
        alert.Status = ProcurementSupplierRiskAlertStatus.Resolved;
        alert.ResolvedById = _currentUser.UserId;
        alert.ResolvedAtUtc = now;
        alert.ResolutionReason = request.Reason.Trim();
        alert.ResolutionEvidenceJson = Serialize(request.Evidence);
        Touch(alert, "Resolved", correlation, now);
        Capture(alert);
        await Alerts.UpdateAsync(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAlertEventAsync(alert, "Resolved",
            ProcurementControlEventResult.Succeeded, before, AlertSnapshot(alert),
            request.Reason, request.Evidence, correlation, now, cancellationToken);
        await PublishNotificationAsync("procurement.supplier-risk.alert-resolved",
            alert.Assessment, 1, cancellationToken);
        return MapAlert(alert);
    }

    private IQueryable<BusinessPartner> SupplierQuery() =>
        _unitOfWork.Repository<BusinessPartner>().GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
            (item.PartnerType.Contains("Supplier") ||
             item.PartnerType.Contains("Contractor") ||
             item.PartnerType.Contains("Both")));

    private IQueryable<ProcurementSupplierRiskAssessment> AssessmentQuery() =>
        Assessments.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private async Task<PolicyResolution?> TryResolvePolicyAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var matches = await _unitOfWork.Repository<ProcurementConfigurationDecision>()
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
            .Include(item => item.Profile)
            .Include(item => item.EvidenceLinks)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (matches.Count != 1) return null;
        ProcurementSupplierRiskDecisionValueDto? value;
        try
        {
            value = JsonSerializer.Deserialize<ProcurementSupplierRiskDecisionValueDto>(
                matches[0].ValueJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        if (value is null) return null;
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value,
                new ValidationContext(value), validation, validateAllProperties: true))
            return null;
        try
        {
            _ = ParseDimensions(value.RiskDimensions);
            _ = ParseBands(value.RiskBands);
        }
        catch (ProcurementSupplierRiskValidationException)
        {
            return null;
        }
        return new PolicyResolution(matches[0], value);
    }

    private async Task<PolicyResolution> ResolvePolicyAsync(
        DateTime now,
        CancellationToken cancellationToken) =>
        await TryResolvePolicyAsync(now, cancellationToken)
        ?? throw Validation("SUPPLIER_RISK_POLICY_UNAVAILABLE",
            "A unique valid Published/effective/approved/evidenced DEC-011 risk policy is required.");

    private static IReadOnlyList<DimensionDefinition> ParseDimensions(
        IEnumerable<string> values)
    {
        var result = new List<DimensionDefinition>();
        foreach (var value in values)
        {
            var parts = value.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !decimal.TryParse(parts[1],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var weight))
                throw Validation("SUPPLIER_RISK_DIMENSION_INVALID",
                    $"Risk dimension '{value}' must use Metric=WeightPercent.");
            if (!ProcurementSupplierRiskDimensionCatalog.TryResolve(parts[0], out var metric))
                throw Validation("SUPPLIER_RISK_DIMENSION_INVALID",
                    $"Risk dimension '{parts[0]}' is not supported. Supported metrics: {string.Join(", ", ProcurementSupplierRiskDimensionCatalog.SupportedNames)}.");
            result.Add(new DimensionDefinition(parts[0], weight, metric));
        }
        if (result.Count == 0 || result.Sum(item => item.Weight) != 100 ||
            result.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() != 1))
            throw Validation("SUPPLIER_RISK_DIMENSION_INVALID",
                "Risk dimensions must be unique and total exactly 100 percent.");
        return result;
    }

    private static IReadOnlyList<BandDefinition> ParseBands(IEnumerable<string> values)
    {
        var result = new List<BandDefinition>();
        foreach (var value in values)
        {
            var pair = value.Split('=', 2, StringSplitOptions.TrimEntries);
            var bounds = pair.Length == 2
                ? pair[1].Split('-', 2, StringSplitOptions.TrimEntries)
                : Array.Empty<string>();
            if (pair.Length != 2 || bounds.Length != 2 ||
                !decimal.TryParse(bounds[0],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var minimum) ||
                !decimal.TryParse(bounds[1],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var maximum))
                throw Validation("SUPPLIER_RISK_BAND_INVALID",
                    $"Risk band '{value}' must use BandName=Minimum-Maximum.");
            result.Add(new BandDefinition(pair[0], minimum, maximum));
        }
        result = result.OrderBy(item => item.Minimum).ToList();
        if (result.Count == 0 || result[0].Minimum != 0 || result[^1].Maximum != 100 ||
            result.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() != 1) ||
            result.Zip(result.Skip(1), (left, right) => left.Maximum == right.Minimum)
                .Any(contiguous => !contiguous))
            throw Validation("SUPPLIER_RISK_BAND_INVALID",
                "Risk bands must be unique and provide contiguous coverage from 0 through 100.");
        return result;
    }

    private static string? ResolveBand(IReadOnlyList<BandDefinition> bands, decimal score)
    {
        for (var index = 0; index < bands.Count; index++)
        {
            var band = bands[index];
            if (score >= band.Minimum &&
                (score < band.Maximum || index == bands.Count - 1 && score <= band.Maximum))
                return band.Name;
        }
        return null;
    }

    private static List<ProcurementSupplierSpendExposureDto> BuildSpendExposure(
        IReadOnlyList<PurchaseOrder> orders,
        Guid supplierId)
    {
        return orders.GroupBy(item => NormalizeCurrency(item.Currency))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var supplier = group.Where(item => item.BusinessPartnerId == supplierId).ToList();
                var tenantAmount = group.Sum(item => item.TotalAmount);
                var supplierAmount = supplier.Sum(item => item.TotalAmount);
                return new ProcurementSupplierSpendExposureDto
                {
                    CurrencyCode = group.Key,
                    SupplierAmount = supplierAmount,
                    TenantAmount = tenantAmount,
                    SpendSharePercent =
                        ProcurementSupplierRiskFormula.CalculateSpendShare(
                            supplierAmount, tenantAmount),
                    SupplierOrderCount = supplier.Count,
                    TenantOrderCount = group.Count()
                };
            }).ToList();
    }

    private static List<ProcurementSupplierCategoryExposureDto> BuildCategoryExposure(
        IReadOnlyList<PurchaseOrder> orders,
        Guid supplierId)
    {
        var lines = orders.SelectMany(order => order.Items
            .Where(item => !item.IsDeleted)
            .Select(item => new
            {
                SupplierId = order.BusinessPartnerId,
                Currency = NormalizeCurrency(order.Currency),
                CategoryId = item.InventoryItem?.CategoryId,
                CategoryCode = item.InventoryItem?.Category?.Code ?? "UNCLASSIFIED",
                CategoryName = item.InventoryItem?.Category?.Name ?? "Unclassified",
                Amount = item.LineTotal
            })).ToList();
        return lines.GroupBy(item => new
            {
                item.CategoryId,
                item.CategoryCode,
                item.CategoryName,
                item.Currency
            })
            .Where(group => group.Any(item => item.SupplierId == supplierId))
            .Select(group =>
            {
                var categoryAmount = group.Sum(item => item.Amount);
                var supplierAmount = group.Where(item => item.SupplierId == supplierId)
                    .Sum(item => item.Amount);
                var supplierCount = group.Where(item => item.Amount > 0)
                    .Select(item => item.SupplierId).Distinct().Count();
                return new ProcurementSupplierCategoryExposureDto
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryCode = group.Key.CategoryCode,
                    CategoryName = group.Key.CategoryName,
                    CurrencyCode = group.Key.Currency,
                    SupplierAmount = supplierAmount,
                    CategoryAmount = categoryAmount,
                    SpendSharePercent =
                        ProcurementSupplierRiskFormula.CalculateSpendShare(
                            supplierAmount, categoryAmount),
                    DistinctSupplierCount = supplierCount,
                    IsSingleSource = supplierAmount > 0 && supplierCount == 1
                };
            }).OrderByDescending(item => item.SpendSharePercent)
            .ThenBy(item => item.CategoryCode).ToList();
    }

    private static List<ProcurementSupplierRiskDimensionDto> BuildDimensionScores(
        IReadOnlyList<DimensionDefinition> definitions,
        BusinessPartner partner,
        SupplierValidationResult eligibility,
        ProcurementSupplierPerformanceScorecard? metric,
        IReadOnlyList<ProcurementSupplierSpendExposureDto> spend,
        IReadOnlyList<ProcurementSupplierCategoryExposureDto> categories,
        bool firstAwardBaseline,
        ICollection<ProcurementSupplierRiskFindingDto> findings)
    {
        var result = new List<ProcurementSupplierRiskDimensionDto>();
        foreach (var definition in definitions)
        {
            decimal? score = null;
            string source;
            string? missing = null;
            switch (definition.Metric)
            {
                case ProcurementSupplierRiskDimension.SupplierPerformance:
                    score = metric?.OverallScore;
                    source = metric is null
                        ? "ProcurementSupplierPerformanceScorecard"
                        : $"ProcurementSupplierPerformanceScorecard:{metric.Id:N}";
                    missing = score.HasValue
                        ? null
                        : "No current governed performance scorecard exists in the exposure window.";
                    break;
                case ProcurementSupplierRiskDimension.Delivery:
                    score = metric?.DeliveryTimelinessScore;
                    source = metric is null
                        ? "ProcurementSupplierPerformanceScorecard"
                        : $"ProcurementSupplierPerformanceScorecard:{metric.Id:N}";
                    missing = score.HasValue
                        ? null
                        : "No governed delivery measure exists in the exposure window.";
                    break;
                case ProcurementSupplierRiskDimension.Quality:
                    score = metric?.GrnQualityScore;
                    source = metric is null
                        ? "ProcurementSupplierPerformanceScorecard"
                        : $"ProcurementSupplierPerformanceScorecard:{metric.Id:N}";
                    missing = score.HasValue
                        ? null
                        : "No governed GRN-quality measure exists in the exposure window.";
                    break;
                case ProcurementSupplierRiskDimension.CostCompetitiveness:
                    score = metric?.PriceCompetitivenessScore;
                    source = metric is null
                        ? "ProcurementSupplierPerformanceScorecard"
                        : $"ProcurementSupplierPerformanceScorecard:{metric.Id:N}";
                    missing = score.HasValue
                        ? null
                        : "No governed price-competitiveness measure exists in the exposure window.";
                    break;
                case ProcurementSupplierRiskDimension.Compliance:
                    score = metric?.ContractCompletionScore;
                    source = metric is null
                        ? "ProcurementSupplierPerformanceScorecard"
                        : $"ProcurementSupplierPerformanceScorecard:{metric.Id:N}";
                    if (!score.HasValue && firstAwardBaseline)
                    {
                        score = eligibility.DueDiligenceCurrent &&
                                eligibility.DueDiligenceOutcome ==
                                ProcurementSupplierDueDiligenceOutcome.Clear
                            ? 100
                            : eligibility.DueDiligenceOutcome ==
                              ProcurementSupplierDueDiligenceOutcome.Adverse
                                ? 0
                                : null;
                        source = eligibility.DueDiligenceReviewId.HasValue
                            ? $"FirstAwardDueDiligence:{eligibility.DueDiligenceReviewId:N}"
                            : "FirstAwardDueDiligence";
                    }
                    missing = score.HasValue
                        ? null
                        : firstAwardBaseline
                            ? "No current approved due-diligence outcome exists for the first-award baseline."
                            : "No governed contract-completion measure exists in the exposure window.";
                    break;
                case ProcurementSupplierRiskDimension.DueDiligence:
                    score = eligibility.DueDiligenceCurrent &&
                            eligibility.DueDiligenceOutcome ==
                            ProcurementSupplierDueDiligenceOutcome.Clear
                        ? 100 : eligibility.DueDiligenceOutcome ==
                                ProcurementSupplierDueDiligenceOutcome.Adverse ? 0 : null;
                    source = eligibility.DueDiligenceReviewId.HasValue
                        ? $"ProcurementSupplierDueDiligenceReview:{eligibility.DueDiligenceReviewId:N}"
                        : "ProcurementSupplierDueDiligenceReview";
                    missing = score.HasValue ? null : "No current approved due-diligence outcome exists.";
                    break;
                case ProcurementSupplierRiskDimension.FinancialStability:
                    var financial = eligibility.DueDiligenceChecks.SingleOrDefault(item =>
                        item.CheckType == ProcurementSupplierDueDiligenceCheckType.FinancialStability);
                    score = financial?.Status == ProcurementSupplierDueDiligenceCheckStatus.Clear
                        ? 100 : financial?.Status == ProcurementSupplierDueDiligenceCheckStatus.Adverse
                            ? 0 : null;
                    source = financial is null ? "DueDiligence.FinancialStability" :
                        $"ProcurementSupplierDueDiligenceCheck:{financial.CheckId:N}";
                    missing = score.HasValue ? null : "No current financial-stability check outcome exists.";
                    break;
                case ProcurementSupplierRiskDimension.SpendDiversification:
                    score = 100 - (spend.Count == 0 ? 0 :
                        spend.Max(item => item.SpendSharePercent));
                    source = "PurchaseOrders.TotalAmount by currency";
                    break;
                case ProcurementSupplierRiskDimension.SingleSourceDependency:
                    score = categories.Any(item => item.IsSingleSource) ? 0 : 100;
                    source = "PurchaseOrderItems by inventory category and currency";
                    break;
                default:
                    throw Validation("SUPPLIER_RISK_DIMENSION_INVALID",
                        $"Configured dimension '{definition.Name}' is not supported by the risk engine.");
            }
            if (score.HasValue) score = Clamp(score.Value);
            if (!string.IsNullOrWhiteSpace(missing))
                findings.Add(DataGap("SUPPLIER_RISK_DIMENSION_DATA_MISSING",
                    $"{definition.Name}: {missing}"));
            result.Add(new ProcurementSupplierRiskDimensionDto
            {
                Dimension = definition.Name,
                WeightPercent = definition.Weight,
                Score = score,
                WeightedScore = score.HasValue
                    ? decimal.Round(score.Value * definition.Weight / 100, 2,
                        MidpointRounding.AwayFromZero)
                    : null,
                Source = source,
                MissingReason = missing
            });
        }
        return result;
    }

    private static List<ProcurementSupplierRiskAlert> BuildAlerts(
        ProcurementSupplierRiskAssessment assessment,
        IReadOnlyCollection<ProcurementSupplierRiskFindingDto> findings,
        string correlation,
        DateTime now)
    {
        var alerts = new List<ProcurementSupplierRiskAlert>();
        if (!assessment.DataComplete)
            alerts.Add(NewAlert(assessment, ProcurementSupplierRiskAlertType.DataIncomplete,
                "SUPPLIER_RISK_DATA_INCOMPLETE", "Critical",
                "The supplier risk assessment is incomplete and cannot support an award decision.",
                correlation, now));
        if (assessment.MinimumScoreBreached)
            alerts.Add(NewAlert(assessment, ProcurementSupplierRiskAlertType.MinimumScore,
                "SUPPLIER_RISK_MINIMUM_SCORE_BREACHED", assessment.RiskBand ?? "High",
                findings.First(item => item.Code == "SUPPLIER_RISK_MINIMUM_SCORE_BREACHED").Message,
                correlation, now));
        if (assessment.ConcentrationBreached)
            alerts.Add(NewAlert(assessment, ProcurementSupplierRiskAlertType.Concentration,
                "SUPPLIER_RISK_CONCENTRATION_LIMIT_BREACHED", assessment.RiskBand ?? "High",
                findings.First(item => item.Code == "SUPPLIER_RISK_CONCENTRATION_LIMIT_BREACHED").Message,
                correlation, now));
        if (assessment.SingleSourceDependency)
            alerts.Add(NewAlert(assessment,
                ProcurementSupplierRiskAlertType.SingleSourceDependency,
                "SUPPLIER_RISK_SINGLE_SOURCE_DEPENDENCY", assessment.RiskBand ?? "High",
                findings.First(item => item.Code == "SUPPLIER_RISK_SINGLE_SOURCE_DEPENDENCY").Message,
                correlation, now));
        return alerts;
    }

    private static ProcurementSupplierRiskAlert NewAlert(
        ProcurementSupplierRiskAssessment assessment,
        ProcurementSupplierRiskAlertType type,
        string ruleCode,
        string severity,
        string message,
        string correlation,
        DateTime now)
    {
        var alert = new ProcurementSupplierRiskAlert
        {
            Id = Guid.NewGuid(),
            TenantId = assessment.TenantId,
            AssessmentId = assessment.Id,
            BusinessPartnerId = assessment.BusinessPartnerId,
            AlertType = type,
            Status = ProcurementSupplierRiskAlertStatus.Open,
            RuleCode = ruleCode,
            Severity = severity,
            Message = message,
            OpenedAtUtc = now,
            CreationCorrelationId = correlation,
            LastOperationCorrelationId = correlation,
            LastOperation = "Opened",
            CreatedAt = now,
            CreatedBy = assessment.AssessedByName,
            CreatedById = assessment.AssessedByUserId
        };
        Capture(alert);
        return alert;
    }

    private async Task<ProcurementSupplierRiskAlert> LoadAlertAsync(
        Guid alertId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<ProcurementSupplierRiskAlert> query =
            Alerts.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == alertId && !item.IsDeleted)
            .Include(item => item.Assessment).ThenInclude(item => item.BusinessPartner);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("SUPPLIER_RISK_ALERT_NOT_FOUND",
                "The supplier-risk alert was not found in the current tenant.");
    }

    private async Task<WorkflowDefinition> ValidateWorkflowAsync(
        Guid workflowDefinitionId,
        CancellationToken cancellationToken)
    {
        if (workflowDefinitionId == Guid.Empty)
            throw Validation("SUPPLIER_RISK_WORKFLOW_REQUIRED",
                "A Published shared workflow definition is required.");
        return await _unitOfWork.Repository<WorkflowDefinition>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == workflowDefinitionId && !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("SUPPLIER_RISK_WORKFLOW_INVALID",
                "The selected workflow is inactive, unpublished, foreign, or unavailable.");
    }

    private async Task ValidateEvidenceAsync(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        CancellationToken cancellationToken)
    {
        if (evidence.Count == 0)
            throw Validation("SUPPLIER_RISK_EVIDENCE_REQUIRED",
                "At least one shared evidence reference is required.");
        if (evidence.GroupBy(item => new
            {
                item.ReferenceKind,
                item.ReferenceId,
                Reference = item.Reference?.Trim()
            }).Any(group => group.Count() > 1))
            throw Validation("SUPPLIER_RISK_EVIDENCE_DUPLICATE",
                "Evidence references must be unique.");

        foreach (var item in evidence)
        {
            switch (item.ReferenceKind)
            {
                case ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument:
                    if (!item.ReferenceId.HasValue || item.ReferenceId == Guid.Empty)
                        throw Validation("SUPPLIER_RISK_EVIDENCE_ID_REQUIRED",
                            "Workflow evidence requires ReferenceId.");
                    var workflowEvidenceExists = await _unitOfWork
                        .Repository<WorkflowEvidenceDocument>()
                        .GetQueryable(record =>
                            record.Id == item.ReferenceId.Value &&
                            record.TenantId == _currentUser.TenantId &&
                            !record.IsDeleted)
                        .AsNoTracking().AnyAsync(cancellationToken);
                    if (!workflowEvidenceExists)
                        throw NotFound("SUPPLIER_RISK_EVIDENCE_NOT_FOUND",
                            "The workflow evidence was not found in the current tenant.");
                    break;

                case ProcurementControlEvidenceReferenceKind.FileUploadRecord:
                    if (!item.ReferenceId.HasValue || item.ReferenceId == Guid.Empty)
                        throw Validation("SUPPLIER_RISK_EVIDENCE_ID_REQUIRED",
                            "File evidence requires ReferenceId.");
                    var uploadExists = await _unitOfWork.Repository<FileUploadRecord>()
                        .GetQueryable(record =>
                            record.Id == item.ReferenceId.Value &&
                            record.TenantId == _currentUser.TenantId &&
                            !record.IsDeleted)
                        .AsNoTracking().AnyAsync(cancellationToken);
                    if (!uploadExists)
                        throw NotFound("SUPPLIER_RISK_EVIDENCE_NOT_FOUND",
                            "The shared file upload was not found in the current tenant.");
                    break;

                case ProcurementControlEvidenceReferenceKind.ExternalReference:
                    if (string.IsNullOrWhiteSpace(item.Reference))
                        throw Validation("SUPPLIER_RISK_EVIDENCE_REFERENCE_REQUIRED",
                            "External evidence requires a reference.");
                    break;

                default:
                    throw Validation("SUPPLIER_RISK_EVIDENCE_KIND_INVALID",
                        "The evidence reference kind is invalid.");
            }
        }
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierRiskAuthorizationException(
                "Supplier portal users cannot administer supplier risk.");
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = SourceType,
                SourceReference = sourceReference
            }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierRiskAuthorizationException(decision.Message);
    }

    private async Task EnsureInternalReaderAsync(CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierRiskAuthorizationException(
                "Supplier portal users cannot access supplier-risk administration.");
        if (HasPlatformSuperAdministratorBypass())
            return;

        var decision = await _accessControl.CheckCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = ReportingPermission,
                SourceType = SourceType,
                SourceReference = "supplier-risk-read"
            }, Guid.NewGuid().ToString("N"), cancellationToken);
        if (decision.Allowed)
            return;

        throw new ProcurementSupplierRiskAuthorizationException(
            decision.Message ??
            "A supplier-review, Internal Audit, or TDC procurement responsibility assignment is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw new ProcurementSupplierRiskAuthorizationException(
                "An authenticated tenant context is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() =>
        _currentUser.HasRole(Constants.Roles.SuperAdmin);

    private async Task RecordAssessmentEventAsync(
        ProcurementSupplierRiskAssessment assessment,
        IReadOnlyCollection<ProcurementSupplierRiskFindingDto> findings,
        string correlation,
        DateTime now,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "supplier-risk", assessment.TenantId, assessment.Id,
                $"Assessed-{correlation}"),
            EventType = EventType,
            Action = "Assessed",
            Result = findings.Any(item => item.Breach)
                ? ProcurementControlEventResult.ReviewRequired
                : ProcurementControlEventResult.Succeeded,
            RuleCode = assessment.EligibilityAction.ToString(),
            RuleId = assessment.PolicyDecisionId,
            RuleVersion = $"{assessment.PolicyProfileCode}/v{assessment.PolicyProfileVersion}",
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = assessment.Id,
            SourceReference = assessment.AssessmentReference,
            InputValues = new
            {
                assessment.BusinessPartnerId,
                assessment.PeriodStartUtc,
                assessment.PeriodEndUtc,
                assessment.PolicyDecisionId,
                assessment.EligibilityDecisionHash
            },
            ResultValues = new
            {
                assessment.RiskScore,
                assessment.RiskBand,
                assessment.MaximumSpendSharePercent,
                assessment.SingleSourceCategoryCount,
                assessment.DataComplete,
                assessment.MinimumScoreBreached,
                assessment.ConcentrationBreached,
                assessment.SingleSourceDependency,
                assessment.IntegrityHash
            },
            After = AssessmentSnapshot(assessment),
            CorrelationId = correlation,
            CausationId = correlation,
            OccurredAtUtc = now
        }, cancellationToken);

    private async Task RecordAlertEventAsync(
        ProcurementSupplierRiskAlert alert,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlation,
        DateTime now,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "supplier-risk-alert", alert.TenantId, alert.Id,
                $"{action}-{correlation}"),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = alert.RuleCode,
            RuleId = alert.Assessment.PolicyDecisionId,
            RuleVersion =
                $"{alert.Assessment.PolicyProfileCode}/v{alert.Assessment.PolicyProfileVersion}",
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = alert.Id,
            SourceReference = alert.Assessment.AssessmentReference,
            Reason = Trim(reason, 1000),
            InputValues = new
            {
                alert.AssessmentId,
                alert.BusinessPartnerId,
                alert.AlertType,
                alert.WorkflowDefinitionId,
                alert.WorkflowInstanceId
            },
            ResultValues = new
            {
                alert.Status,
                alert.IntegrityHash
            },
            Before = before,
            After = after,
            CorrelationId = correlation,
            CausationId = correlation,
            OccurredAtUtc = now,
            Evidence = evidence.ToList()
        }, cancellationToken);

    private async Task PublishNotificationAsync(
        string topic,
        ProcurementSupplierRiskAssessment assessment,
        int alertCount,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = assessment.TenantId,
                TopicKey = topic,
                NotificationType = "ProcurementSupplierRiskControl",
                EntityType = SourceType,
                EntityId = assessment.Id,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["businessPartnerId"] = assessment.BusinessPartnerId,
                    ["assessmentReference"] = assessment.AssessmentReference,
                    ["riskBand"] = assessment.RiskBand ?? "Unresolved",
                    ["riskScore"] = assessment.RiskScore ?? 0,
                    ["maximumSpendSharePercent"] = assessment.MaximumSpendSharePercent,
                    ["singleSourceCategoryCount"] = assessment.SingleSourceCategoryCount,
                    ["alertCount"] = alertCount
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish supplier-risk notification {Topic} for {AssessmentId}",
                topic, assessment.Id);
        }
    }

    private static ProcurementSupplierRiskListItemDto MapList(
        ProcurementSupplierRiskAssessment item,
        DateTime now,
        BusinessPartner? partner = null) => new()
    {
        Id = item.Id,
        AssessmentReference = item.AssessmentReference,
        AssessmentSequence = item.AssessmentSequence,
        BusinessPartnerId = item.BusinessPartnerId,
        PartnerCode = (partner ?? item.BusinessPartner).PartnerCode,
        PartnerName = (partner ?? item.BusinessPartner).PartnerName,
        AssessedAtUtc = item.AssessedAtUtc,
        NextReviewDueAtUtc = item.NextReviewDueAtUtc,
        RiskScore = item.RiskScore,
        RiskBand = item.RiskBand,
        MaximumSpendSharePercent = item.MaximumSpendSharePercent,
        SingleSourceCategoryCount = item.SingleSourceCategoryCount,
        DataComplete = item.DataComplete,
        IsCurrent = item.NextReviewDueAtUtc > now,
        OpenAlertCount = item.Alerts.Count(alert => !alert.IsDeleted &&
            alert.Status == ProcurementSupplierRiskAlertStatus.Open),
        EscalatedAlertCount = item.Alerts.Count(alert => !alert.IsDeleted &&
            alert.Status == ProcurementSupplierRiskAlertStatus.Escalated),
        AwardBlocked = AwardBlocked(item, now),
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementSupplierRiskAssessmentDto Map(
        ProcurementSupplierRiskAssessment item,
        DateTime now,
        BusinessPartner? partner = null)
    {
        var list = MapList(item, now, partner);
        return new ProcurementSupplierRiskAssessmentDto
        {
            Id = list.Id,
            AssessmentReference = list.AssessmentReference,
            AssessmentSequence = list.AssessmentSequence,
            BusinessPartnerId = list.BusinessPartnerId,
            PartnerCode = list.PartnerCode,
            PartnerName = list.PartnerName,
            AssessedAtUtc = list.AssessedAtUtc,
            NextReviewDueAtUtc = list.NextReviewDueAtUtc,
            RiskScore = list.RiskScore,
            RiskBand = list.RiskBand,
            MaximumSpendSharePercent = list.MaximumSpendSharePercent,
            SingleSourceCategoryCount = list.SingleSourceCategoryCount,
            DataComplete = list.DataComplete,
            IsCurrent = list.IsCurrent,
            OpenAlertCount = list.OpenAlertCount,
            EscalatedAlertCount = list.EscalatedAlertCount,
            AwardBlocked = list.AwardBlocked,
            IntegrityHash = list.IntegrityHash,
            PeriodStartUtc = item.PeriodStartUtc,
            PeriodEndUtc = item.PeriodEndUtc,
            PolicyDecisionId = item.PolicyDecisionId,
            PolicyProfileId = item.PolicyProfileId,
            PolicyProfileCode = item.PolicyProfileCode,
            PolicyProfileVersion = item.PolicyProfileVersion,
            PolicyValueHash = item.PolicyValueHash,
            ExposureWindowMonths = item.ExposureWindowMonths,
            MinimumScore = item.MinimumScore,
            ConcentrationLimitPercent = item.ConcentrationLimitPercent,
            EligibilityAction = item.EligibilityAction,
            MinimumScoreBreached = item.MinimumScoreBreached,
            ConcentrationBreached = item.ConcentrationBreached,
            SingleSourceDependency = item.SingleSourceDependency,
            EligibilityDecisionHash = item.EligibilityDecisionHash,
            SourceType = item.SourceType,
            SourceId = item.SourceId,
            SourceReference = item.SourceReference,
            AssessedByUserId = item.AssessedByUserId,
            AssessedByName = item.AssessedByName,
            Dimensions = DeserializeList<ProcurementSupplierRiskDimensionDto>(
                item.DimensionScoresJson),
            SpendExposure = DeserializeList<ProcurementSupplierSpendExposureDto>(
                item.SpendExposureJson),
            CategoryExposure = DeserializeList<ProcurementSupplierCategoryExposureDto>(
                item.CategoryExposureJson),
            Findings = DeserializeList<ProcurementSupplierRiskFindingDto>(
                item.FindingsJson),
            Alerts = item.Alerts.Where(alert => !alert.IsDeleted)
                .OrderBy(alert => alert.Status).ThenBy(alert => alert.AlertType)
                .Select(MapAlert).ToList(),
            DecisionKeys = DecisionKeys
        };
    }

    private static ProcurementSupplierRiskAlertDto MapAlert(
        ProcurementSupplierRiskAlert item) => new()
    {
        Id = item.Id,
        AssessmentId = item.AssessmentId,
        AlertType = item.AlertType,
        Status = item.Status,
        RuleCode = item.RuleCode,
        Severity = item.Severity,
        Message = item.Message,
        OpenedAtUtc = item.OpenedAtUtc,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        WorkflowInstanceId = item.WorkflowInstanceId,
        EscalatedById = item.EscalatedById,
        EscalatedAtUtc = item.EscalatedAtUtc,
        EscalationReason = item.EscalationReason,
        ResolvedById = item.ResolvedById,
        ResolvedAtUtc = item.ResolvedAtUtc,
        ResolutionReason = item.ResolutionReason,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        IntegrityHash = item.IntegrityHash
    };

    private static List<string> CurrentAwardBlockReasons(
        ProcurementSupplierRiskAssessment? assessment,
        PolicyResolution? policy,
        DateTime now)
    {
        var reasons = new List<string>();
        if (policy is null)
        {
            reasons.Add("A unique valid effective DEC-011 risk policy is unavailable.");
            return reasons;
        }
        if (assessment is null)
        {
            reasons.Add("A supplier-risk assessment has not been recorded.");
            return reasons;
        }
        if (!IsCurrent(assessment, policy, now))
            reasons.Add("The latest supplier-risk assessment is stale, expired, or bound to a prior policy.");
        if (!assessment.DataComplete)
            reasons.Add("The supplier-risk assessment is incomplete.");
        var breach = HasBreach(assessment);
        if (assessment.EligibilityAction == ProcurementSupplierRiskEligibilityAction.AwardHardStop &&
            breach)
            reasons.Add("DEC-011 applies an award hard stop to the current risk breach.");
        if (assessment.EligibilityAction ==
                ProcurementSupplierRiskEligibilityAction.EscalationRequired &&
            HasUnresolvedAlerts(assessment))
            reasons.Add("DEC-011 requires every current risk alert to complete escalation.");
        return reasons;
    }

    private static bool IsCurrent(
        ProcurementSupplierRiskAssessment item,
        PolicyResolution policy,
        DateTime now) =>
        item.NextReviewDueAtUtc > now && MatchesPolicy(item, policy);

    private static bool MatchesPolicy(
        ProcurementSupplierRiskAssessment item,
        PolicyResolution policy) =>
        item.PolicyDecisionId == policy.Decision.Id &&
        string.Equals(item.PolicyValueHash, Hash(policy.Decision.ValueJson),
            StringComparison.OrdinalIgnoreCase);

    private static bool AwardBlocked(ProcurementSupplierRiskAssessment item, DateTime now) =>
        item.NextReviewDueAtUtc <= now || !item.DataComplete ||
        item.EligibilityAction == ProcurementSupplierRiskEligibilityAction.AwardHardStop &&
        HasBreach(item) ||
        item.EligibilityAction == ProcurementSupplierRiskEligibilityAction.EscalationRequired &&
        HasUnresolvedAlerts(item);

    private static bool HasBreach(ProcurementSupplierRiskAssessment item) =>
        item.MinimumScoreBreached || item.ConcentrationBreached ||
        item.SingleSourceDependency || !item.DataComplete;

    private static bool HasUnresolvedAlerts(ProcurementSupplierRiskAssessment item) =>
        item.Alerts.Any(alert => !alert.IsDeleted &&
            alert.Status != ProcurementSupplierRiskAlertStatus.Resolved);

    private static ProcurementSupplierRiskFindingDto DataGap(string code, string message) =>
        new() { Code = code, Message = message, DataGap = true };

    private static ProcurementSupplierRiskFindingDto Breach(
        string code,
        string message,
        bool dataGap = false) =>
        new() { Code = code, Message = message, Breach = true, DataGap = dataGap };

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0, 100);
    private static string NormalizeCurrency(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "UNSPECIFIED" : value.Trim().ToUpperInvariant();

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw Conflict("SUPPLIER_RISK_ROW_VERSION_REQUIRED",
                "The current row version is required.");
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    current, Convert.FromBase64String(supplied)))
                throw Conflict("SUPPLIER_RISK_CONCURRENCY_CONFLICT",
                    "The alert changed; refresh and retry.");
        }
        catch (FormatException)
        {
            throw Conflict("SUPPLIER_RISK_ROW_VERSION_INVALID",
                "The row version is invalid.");
        }
    }

    private void Touch(
        ProcurementSupplierRiskAlert item,
        string operation,
        string correlation,
        DateTime now)
    {
        item.LastOperation = operation;
        item.LastOperationCorrelationId = correlation;
        item.UpdatedAt = now;
        item.UpdatedBy = ActorName;
        item.LastModifiedById = _currentUser.UserId;
    }

    private static void Capture(ProcurementSupplierRiskAssessment item)
    {
        item.SnapshotJson = Serialize(AssessmentSnapshot(item));
        item.IntegrityHash = Hash(item.SnapshotJson);
    }

    private static void Capture(ProcurementSupplierRiskAlert item)
    {
        item.SnapshotJson = Serialize(AlertSnapshot(item));
        item.IntegrityHash = Hash(item.SnapshotJson);
    }

    private static object AssessmentSnapshot(ProcurementSupplierRiskAssessment item) => new
    {
        schemaVersion = "tdc.supplier-risk-assessment.v1",
        item.Id,
        item.TenantId,
        item.AssessmentReference,
        item.AssessmentSequence,
        item.BusinessPartnerId,
        item.AssessedAtUtc,
        item.PeriodStartUtc,
        item.PeriodEndUtc,
        item.NextReviewDueAtUtc,
        item.PolicyDecisionId,
        item.PolicyProfileId,
        item.PolicyProfileCode,
        item.PolicyProfileVersion,
        item.PolicyValueHash,
        item.ExposureWindowMonths,
        item.MinimumScore,
        item.ConcentrationLimitPercent,
        item.RiskScore,
        item.RiskBand,
        item.EligibilityAction,
        item.DataComplete,
        item.MinimumScoreBreached,
        item.ConcentrationBreached,
        item.SingleSourceDependency,
        item.MaximumSpendSharePercent,
        item.SingleSourceCategoryCount,
        item.DimensionScoresJson,
        item.SpendExposureJson,
        item.CategoryExposureJson,
        item.EligibilityDecisionHash,
        item.FindingsJson,
        item.IdempotencyKey,
        item.CorrelationId,
        item.SourceType,
        item.SourceId,
        item.SourceReference,
        item.AssessedByUserId
    };

    private static object AlertSnapshot(ProcurementSupplierRiskAlert item) => new
    {
        schemaVersion = "tdc.supplier-risk-alert.v1",
        item.Id,
        item.TenantId,
        item.AssessmentId,
        item.BusinessPartnerId,
        item.AlertType,
        item.Status,
        item.RuleCode,
        item.Severity,
        item.Message,
        item.OpenedAtUtc,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.EscalatedById,
        item.EscalatedAtUtc,
        item.EscalationReason,
        item.EscalationEvidenceJson,
        item.ResolvedById,
        item.ResolvedAtUtc,
        item.ResolutionReason,
        item.ResolutionEvidenceJson,
        item.CreationCorrelationId,
        item.LastOperationCorrelationId,
        item.LastOperation
    };

    private async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await action();
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private string ActorName => string.IsNullOrWhiteSpace(_currentUser.FullName)
        ? _currentUser.Username
        : _currentUser.FullName;

    private static string NormalizeCorrelation(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw Validation("SUPPLIER_RISK_CORRELATION_REQUIRED",
                "X-Correlation-ID is required.");
        return correlationId.Trim().Length <= 100
            ? correlationId.Trim()
            : Hash(correlationId);
    }

    private static IReadOnlyList<T> DeserializeList<T>(string value)
    {
        try { return JsonSerializer.Deserialize<List<T>>(value, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null :
        value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];

    private static ProcurementSupplierRiskNotFoundException NotFound(
        string code,
        string message) => new(code, message);
    private static ProcurementSupplierRiskValidationException Validation(
        string code,
        string message) => new(code, message);
    private static ProcurementSupplierRiskConflictException Conflict(
        string code,
        string message) => new(code, message);

    private sealed record PolicyResolution(
        ProcurementConfigurationDecision Decision,
        ProcurementSupplierRiskDecisionValueDto Value);
    private sealed record DimensionDefinition(
        string Name, decimal Weight, ProcurementSupplierRiskDimension Metric);
    private sealed record BandDefinition(string Name, decimal Minimum, decimal Maximum);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
