using ErpSystem.Shared;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementSupplierPerformanceScorecardService :
    IProcurementSupplierPerformanceScorecardService
{
    private const string SourceType = "ProcurementSupplierPerformanceScorecard";
    private const string EventType = "ProcurementSupplierPerformanceControl";
    private const string ReviewPermission = "procurement.supplier.review";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToArray();
    private static readonly HashSet<string> IncludedOrderStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Approved", "Sent", "Acknowledged", "PartiallyReceived",
            "Received", "Completed", "Closed"
        };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementSupplierPerformanceScorecardService> _logger;

    public ProcurementSupplierPerformanceScorecardService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        ISupplierValidationService supplierValidation,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementSupplierPerformanceScorecardService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _supplierValidation = supplierValidation;
        _notificationTopics = notificationTopics;
        _logger = logger;
    }

    private IGenericRepository<ProcurementSupplierPerformanceScorecard> Scorecards =>
        _unitOfWork.Repository<ProcurementSupplierPerformanceScorecard>();

    public async Task<ProcurementSupplierPerformanceSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var policy = await TryResolvePolicyAsync(now, cancellationToken);
        var suppliers = await SupplierQuery().Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var rows = await ScorecardQuery().AsNoTracking()
            .ToListAsync(cancellationToken);
        var current = Latest(rows);
        return new ProcurementSupplierPerformanceSummaryDto
        {
            SupplierCount = suppliers.Count,
            ScoredSupplierCount = current.Count,
            CurrentScorecardCount = policy is null
                ? 0
                : current.Count(item => IsCurrent(item, policy, now)),
            BelowMinimumCount = current.Count(item => item.MinimumScoreBreached),
            InsufficientCoverageCount = current.Count(item =>
                item.DataStatus != ProcurementSupplierPerformanceDataStatus.Complete),
            PolicyAvailable = policy is not null,
            PolicyProfileCode = policy?.Decision.Profile.ProfileCode,
            PolicyProfileVersion = policy?.Decision.Profile.Version,
            PerformanceWindowMonths = policy?.Value.PerformanceWindowMonths,
            MinimumScore = policy?.Value.MinimumScore,
            MinimumDataCoveragePercent =
                policy?.Value.MinimumPerformanceDataCoveragePercent,
            ResponseTargetHours = policy?.Value.ResponseTargetHours,
            EligibilityAction = policy?.Value.PerformanceEligibilityAction,
            PolicyReleaseGate = policy is null
                ? "No unique valid Published/effective/approved/evidenced DEC-011 performance-scorecard policy is available."
                : null
        };
    }

    public async Task<ProcurementSupplierPerformancePageDto> SearchAsync(
        ProcurementSupplierPerformanceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 100);
        var rows = Latest(await ScorecardQuery()
                .Include(item => item.BusinessPartner)
                .AsNoTracking().ToListAsync(cancellationToken))
            .AsEnumerable();
        if (request.BusinessPartnerId.HasValue)
            rows = rows.Where(item =>
                item.BusinessPartnerId == request.BusinessPartnerId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            rows = rows.Where(item =>
                item.ScorecardReference.Contains(search,
                    StringComparison.OrdinalIgnoreCase) ||
                item.BusinessPartner.PartnerCode.Contains(search,
                    StringComparison.OrdinalIgnoreCase) ||
                item.BusinessPartner.PartnerName.Contains(search,
                    StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(request.PerformanceBand))
            rows = rows.Where(item => string.Equals(item.PerformanceBand,
                request.PerformanceBand.Trim(), StringComparison.OrdinalIgnoreCase));
        if (request.DataStatus.HasValue)
            rows = rows.Where(item => item.DataStatus == request.DataStatus.Value);
        if (request.BelowMinimum.HasValue)
            rows = rows.Where(item =>
                item.MinimumScoreBreached == request.BelowMinimum.Value);
        var list = rows.OrderByDescending(item => item.CalculatedAtUtc)
            .ThenBy(item => item.BusinessPartner.PartnerCode).ToList();
        return new ProcurementSupplierPerformancePageDto
        {
            Items = list.Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize).Select(item => MapList(item, DateTime.UtcNow))
                .ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = list.Count
        };
    }

    public async Task<ProcurementSupplierPerformanceScorecardDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var row = await ScorecardQuery().Include(item => item.BusinessPartner)
            .AsNoTracking().SingleOrDefaultAsync(item => item.Id == id,
                cancellationToken)
            ?? throw NotFound("SUPPLIER_PERFORMANCE_SCORECARD_NOT_FOUND",
                "The scorecard was not found in the current tenant.");
        return Map(row, DateTime.UtcNow);
    }

    public async Task<ProcurementSupplierPerformanceCurrentStateDto>
        GetCurrentStateAsync(
            Guid businessPartnerId,
            CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var partner = await SupplierQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == businessPartnerId,
                cancellationToken)
            ?? throw NotFound("SUPPLIER_PERFORMANCE_SUPPLIER_NOT_FOUND",
                "The supplier was not found in the current tenant.");
        var now = DateTime.UtcNow;
        var policy = await TryResolvePolicyAsync(now, cancellationToken);
        var row = await ScorecardQuery().Include(item => item.BusinessPartner)
            .AsNoTracking().Where(item => item.BusinessPartnerId == businessPartnerId)
            .OrderByDescending(item => item.CalculatedAtUtc)
            .ThenByDescending(item => item.ScorecardSequence)
            .FirstOrDefaultAsync(cancellationToken);
        var current = row is not null && policy is not null &&
            IsCurrent(row, policy, now);
        var firstAwardBaseline = row is not null && policy is not null &&
            IsFirstAwardBaseline(row, policy, now);
        var reasons = new List<string>();
        if (policy is null)
            reasons.Add("A unique valid effective DEC-011 performance policy is unavailable.");
        else if (row is null)
            reasons.Add("No governed supplier-performance scorecard exists.");
        else
        {
            if (!current && !firstAwardBaseline)
                reasons.Add("The latest scorecard is stale, expired, incomplete, or bound to a prior policy.");
            if (row.MinimumScoreBreached &&
                row.EligibilityAction ==
                ProcurementSupplierRiskEligibilityAction.AwardHardStop)
                reasons.Add("DEC-011 applies an award hard stop to the below-minimum score.");
        }
        return new ProcurementSupplierPerformanceCurrentStateDto
        {
            BusinessPartnerId = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PolicyAvailable = policy is not null,
            PolicyReleaseGate = policy is null
                ? "Publish and evidence one valid DEC-011 performance configuration."
                : null,
            Scorecard = row is null ? null : Map(row, now),
            Current = current,
            AwardBlocked = reasons.Count != 0,
            AwardBlockReasons = reasons
        };
    }

    public async Task<IReadOnlyList<ProcurementSupplierPerformanceSupplierOptionDto>>
        GetSupplierOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await SupplierQuery().AsNoTracking()
            .OrderBy(item => item.PartnerCode)
            .Select(item => new ProcurementSupplierPerformanceSupplierOptionDto
            {
                Id = item.Id,
                Code = item.PartnerCode,
                Name = item.PartnerName
            }).ToListAsync(cancellationToken);
    }

    public async Task<ProcurementSupplierPerformanceScorecardDto> CalculateAsync(
        CalculateProcurementSupplierPerformanceRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        if (request.BusinessPartnerId == Guid.Empty)
            throw Validation("SUPPLIER_PERFORMANCE_SUPPLIER_REQUIRED",
                "A supplier is required.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw Validation("SUPPLIER_PERFORMANCE_IDEMPOTENCY_REQUIRED",
                "An idempotency key is required.");
        var idempotency = NormalizeKey(request.IdempotencyKey);
        await EnsureCapabilityAsync(ReviewPermission,
            request.BusinessPartnerId.ToString("N"), correlation, cancellationToken);
        var replay = await ScorecardQuery().Include(item => item.BusinessPartner)
            .AsNoTracking().SingleOrDefaultAsync(item =>
                item.IdempotencyKey == idempotency, cancellationToken);
        if (replay is not null)
        {
            if (replay.BusinessPartnerId != request.BusinessPartnerId)
                throw Conflict("SUPPLIER_PERFORMANCE_IDEMPOTENCY_CONFLICT",
                    "The idempotency key belongs to another supplier.");
            return Map(replay, DateTime.UtcNow);
        }

        var partner = await SupplierQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.BusinessPartnerId,
                cancellationToken)
            ?? throw NotFound("SUPPLIER_PERFORMANCE_SUPPLIER_NOT_FOUND",
                "The supplier was not found in the current tenant.");
        var now = DateTime.UtcNow;
        var policy = await ResolvePolicyAsync(now, cancellationToken);
        var definitions = ParseDimensions(policy.Value.PerformanceDimensions);
        var bands = ParseBands(policy.Value.PerformanceBands);
        var periodStart = now.AddMonths(-policy.Value.PerformanceWindowMonths);

        var eligibility = await _supplierValidation.EvaluateEligibilityAsync(
            new SupplierEligibilityEvaluationRequest
            {
                BusinessPartnerId = partner.Id,
                Boundary = SupplierEligibilityBoundary.StatusReview,
                SkipPerformanceScorecard = true,
                SkipRiskAssessment = true
            }, cancellationToken);
        var risk = await _unitOfWork.Repository<ProcurementSupplierRiskAssessment>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id && !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.AssessedAtUtc)
            .ThenByDescending(item => item.AssessmentSequence)
            .FirstOrDefaultAsync(cancellationToken);

        var orders = await _unitOfWork.Repository<PurchaseOrder>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id && !item.IsDeleted &&
                item.OrderDate <= now &&
                (item.OrderDate >= periodStart ||
                 item.Receipts.Any(receipt => !receipt.IsDeleted &&
                     receipt.ReceiptDate >= periodStart &&
                     receipt.ReceiptDate <= now)))
            .Include(item => item.Items)
            .Include(item => item.Receipts.Where(receipt =>
                !receipt.IsDeleted && receipt.ReceiptDate >= periodStart &&
                receipt.ReceiptDate <= now))
            .ThenInclude(receipt => receipt.Items.Where(item => !item.IsDeleted))
            .AsNoTracking().ToListAsync(cancellationToken);
        orders = orders.Where(item => IncludedOrderStatuses.Contains(item.Status))
            .OrderBy(item => item.OrderNumber).ToList();

        var rfqItemIds = orders.SelectMany(item => item.Items)
            .Where(item => item.SourceRfqItemId.HasValue)
            .Select(item => item.SourceRfqItemId!.Value).Distinct().ToList();
        var rfqItems = rfqItemIds.Count == 0
            ? new List<RequestForQuotationItem>()
            : await _unitOfWork.Repository<RequestForQuotationItem>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    rfqItemIds.Contains(item.Id) && !item.IsDeleted)
                .AsNoTracking().ToListAsync(cancellationToken);
        var requisitionIds = orders.Where(item => item.SourceRequisitionId.HasValue)
            .Select(item => item.SourceRequisitionId!.Value)
            .Concat(rfqItems.Where(item =>
                    item.SourcePurchaseRequisitionItemId.HasValue)
                .Select(item => item.SourcePurchaseRequisitionItemId!.Value))
            .Distinct().ToList();
        var sourceRequisitionItemIds = rfqItems.Where(item =>
                item.SourcePurchaseRequisitionItemId.HasValue)
            .Select(item => item.SourcePurchaseRequisitionItemId!.Value)
            .Distinct().ToList();
        var requisitionItems = await _unitOfWork
            .Repository<PurchaseRequisitionItem>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted &&
                (sourceRequisitionItemIds.Contains(item.Id) ||
                 requisitionIds.Contains(item.RequisitionId)))
            .AsNoTracking().ToListAsync(cancellationToken);

        var invitations = await _unitOfWork.Repository<RequestForQuotationInvitation>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id && !item.IsDeleted &&
                item.InvitedAt <= now &&
                (item.InvitedAt >= periodStart ||
                 item.RespondedAt >= periodStart))
            .AsNoTracking().OrderBy(item => item.InvitedAt)
            .ToListAsync(cancellationToken);
        var incidents = await _unitOfWork.Repository<QualityIncident>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id && !item.IsDeleted &&
                item.IncidentDate >= periodStart && item.IncidentDate <= now)
            .AsNoTracking().OrderBy(item => item.IncidentDate)
            .ToListAsync(cancellationToken);
        var contracts = await _unitOfWork.Repository<Contract>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id && !item.IsDeleted &&
                ((item.StartDate ?? item.CreatedAt) <= now) &&
                ((!item.EndDate.HasValue || item.EndDate.Value >= periodStart) ||
                 (item.CompletedAt.HasValue &&
                  item.CompletedAt.Value >= periodStart)))
            .AsNoTracking().OrderBy(item => item.ContractNumber)
            .ToListAsync(cancellationToken);

        var observations = BuildObservations(orders, rfqItems, requisitionItems,
            invitations, incidents, contracts, policy.Value.ResponseTargetHours, now);
        var measures = definitions.Select(definition =>
        {
            var observation = observations[definition.Metric];
            return new ProcurementSupplierPerformanceMeasureDto
            {
                Metric = definition.Metric,
                WeightPercent = definition.Weight,
                Score = observation.Score,
                ObservationCount = observation.Count,
                Numerator = observation.Numerator,
                Denominator = observation.Denominator,
                Unit = observation.Unit,
                Source = observation.Source,
                MissingReason = observation.MissingReason
            };
        }).ToList();
        var formula = ProcurementSupplierPerformanceFormula.Calculate(
            measures, policy.Value.MinimumPerformanceDataCoveragePercent);
        var dataStatus = measures.All(item => !item.Score.HasValue)
            ? ProcurementSupplierPerformanceDataStatus.NoQualifyingActivity
            : formula.DataComplete
                ? ProcurementSupplierPerformanceDataStatus.Complete
                : ProcurementSupplierPerformanceDataStatus.InsufficientCoverage;
        var band = formula.OverallScore.HasValue
            ? ResolveBand(bands, formula.OverallScore.Value)
            : null;
        var findings = BuildFindings(measures, formula, band,
            policy.Value.MinimumScore);
        var minimumBreached = formula.DataComplete &&
            formula.OverallScore.HasValue &&
            formula.OverallScore.Value < policy.Value.MinimumScore;
        var sourceSnapshot = BuildSourceSnapshot(orders, rfqItems, requisitionItems,
            invitations, incidents, contracts);
        var sourceSnapshotJson = Serialize(sourceSnapshot);
        var sequence = (await Scorecards.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id)
            .MaxAsync(item => (int?)item.ScorecardSequence, cancellationToken) ?? 0) + 1;
        var id = Guid.NewGuid();
        var scorecard = new ProcurementSupplierPerformanceScorecard
        {
            Id = id,
            TenantId = _currentUser.TenantId,
            ScorecardReference =
                $"SPERF-{now:yyyyMMdd}-{id.ToString("N")[..8].ToUpperInvariant()}",
            ScorecardSequence = sequence,
            BusinessPartnerId = partner.Id,
            CalculatedAtUtc = now,
            PeriodStartUtc = periodStart,
            PeriodEndUtc = now,
            NextReviewDueAtUtc = now.AddMonths(policy.Value.ReviewFrequencyMonths),
            PolicyDecisionId = policy.Decision.Id,
            PolicyProfileId = policy.Decision.ProfileId,
            PolicyProfileCode = policy.Decision.Profile.ProfileCode,
            PolicyProfileVersion = policy.Decision.Profile.Version,
            PolicySnapshotJson = policy.Decision.ValueJson,
            PolicyValueHash = Hash(policy.Decision.ValueJson),
            PerformanceWindowMonths = policy.Value.PerformanceWindowMonths,
            MinimumScore = policy.Value.MinimumScore,
            MinimumDataCoveragePercent =
                policy.Value.MinimumPerformanceDataCoveragePercent,
            ResponseTargetHours = policy.Value.ResponseTargetHours,
            EligibilityAction = policy.Value.PerformanceEligibilityAction,
            DataStatus = dataStatus,
            DataCoveragePercent = formula.CoveragePercent,
            OverallScore = formula.DataComplete ? formula.OverallScore : null,
            PerformanceBand = formula.DataComplete ? band : null,
            MinimumScoreBreached = minimumBreached,
            DeliveryTimelinessScore = observations[
                ProcurementSupplierPerformanceMetricKey.DeliveryTimeliness].Score,
            GrnQualityScore = observations[
                ProcurementSupplierPerformanceMetricKey.GrnQuality].Score,
            RejectionRateScore = observations[
                ProcurementSupplierPerformanceMetricKey.RejectionRate].Score,
            PriceCompetitivenessScore = observations[
                ProcurementSupplierPerformanceMetricKey.PriceCompetitiveness].Score,
            ResponsivenessScore = observations[
                ProcurementSupplierPerformanceMetricKey.Responsiveness].Score,
            ComplaintResolutionScore = observations[
                ProcurementSupplierPerformanceMetricKey.ComplaintResolution].Score,
            ContractCompletionScore = observations[
                ProcurementSupplierPerformanceMetricKey.ContractCompletion].Score,
            PurchaseOrderCount = orders.Count,
            ReceiptCount = orders.SelectMany(item => item.Receipts).Count(),
            ReceiptLineCount = orders.SelectMany(item => item.Receipts)
                .SelectMany(item => item.Items).Count(),
            PriceComparisonCount = observations[
                ProcurementSupplierPerformanceMetricKey.PriceCompetitiveness].Count,
            ResponseObservationCount = observations[
                ProcurementSupplierPerformanceMetricKey.Responsiveness].Count,
            ComplaintCount = incidents.Count,
            ContractCount = contracts.Count,
            MeasureResultsJson = Serialize(measures),
            SourceSnapshotJson = sourceSnapshotJson,
            SourceSnapshotHash = Hash(sourceSnapshotJson),
            SupplierControlSnapshotJson = Serialize(eligibility),
            SupplierEligibilityDecisionHash = eligibility.DecisionHash,
            RiskAssessmentId = risk?.Id,
            RiskAssessmentIntegrityHash = risk?.IntegrityHash,
            FindingsJson = Serialize(findings),
            IdempotencyKey = idempotency,
            CorrelationId = correlation,
            SourceType = Trim(request.SourceType, 100),
            SourceId = request.SourceId,
            SourceReference = Trim(request.SourceReference, 100),
            CalculatedByUserId = _currentUser.UserId,
            CalculatedByName = ActorName,
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId
        };
        Capture(scorecard);
        await ExecuteAsync(async () =>
        {
            await Scorecards.AddAsync(scorecard);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        scorecard.BusinessPartner = partner;
        await RecordEventAsync(scorecard, findings, correlation, now,
            cancellationToken);
        await PublishNotificationAsync(scorecard, cancellationToken);
        _logger.LogInformation(
            "Supplier performance scorecard {Reference} recorded for {PartnerId}; score {Score}, coverage {Coverage}",
            scorecard.ScorecardReference, partner.Id, scorecard.OverallScore,
            scorecard.DataCoveragePercent);
        return Map(scorecard, now);
    }

    private IQueryable<BusinessPartner> SupplierQuery() =>
        _unitOfWork.Repository<BusinessPartner>().GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
            (item.PartnerType.Contains("Supplier") ||
             item.PartnerType.Contains("Contractor") ||
             item.PartnerType.Contains("Both")));

    private IQueryable<ProcurementSupplierPerformanceScorecard> ScorecardQuery() =>
        Scorecards.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private static List<ProcurementSupplierPerformanceScorecard> Latest(
        IEnumerable<ProcurementSupplierPerformanceScorecard> rows) =>
        rows.GroupBy(item => item.BusinessPartnerId)
            .Select(group => group.OrderByDescending(item => item.CalculatedAtUtc)
                .ThenByDescending(item => item.ScorecardSequence).First())
            .ToList();

    private async Task<PolicyResolution?> TryResolvePolicyAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var matches = await _unitOfWork.Repository<ProcurementConfigurationDecision>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.DecisionKey == "DEC-011" && !item.IsDeleted &&
                item.Profile.LifecycleStatus ==
                ProcurementConfigurationProfileStatus.Published &&
                item.Profile.EffectiveFrom <= now &&
                (!item.Profile.EffectiveTo.HasValue ||
                 item.Profile.EffectiveTo.Value >= now) &&
                item.Status == ProcurementConfigurationDecisionStatus.Approved &&
                item.ApprovalStatus ==
                ProcurementConfigurationApprovalStatus.Approved &&
                item.EvidenceStatus ==
                ProcurementConfigurationEvidenceStatus.Verified &&
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
        if (!Validator.TryValidateObject(value, new ValidationContext(value),
                validation, validateAllProperties: true))
            return null;
        try
        {
            _ = ParseDimensions(value.PerformanceDimensions);
            _ = ParseBands(value.PerformanceBands);
        }
        catch (ProcurementSupplierPerformanceValidationException)
        {
            return null;
        }
        return new PolicyResolution(matches[0], value);
    }

    private async Task<PolicyResolution> ResolvePolicyAsync(
        DateTime now,
        CancellationToken cancellationToken) =>
        await TryResolvePolicyAsync(now, cancellationToken)
        ?? throw Validation("SUPPLIER_PERFORMANCE_POLICY_UNAVAILABLE",
            "A unique valid Published/effective/approved/evidenced DEC-011 performance-scorecard policy is required.");

    private static IReadOnlyList<DimensionDefinition> ParseDimensions(
        IEnumerable<string> values)
    {
        var result = new List<DimensionDefinition>();
        foreach (var value in values)
        {
            var parts = value.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !Enum.TryParse<ProcurementSupplierPerformanceMetricKey>(
                    parts[0], true, out var metric) ||
                !decimal.TryParse(parts[1],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var weight))
                throw Validation("SUPPLIER_PERFORMANCE_DIMENSION_INVALID",
                    $"Performance dimension '{value}' must use supported Metric=WeightPercent.");
            result.Add(new DimensionDefinition(metric, weight));
        }
        var required = Enum.GetValues<ProcurementSupplierPerformanceMetricKey>();
        if (result.Count != required.Length ||
            result.Sum(item => item.Weight) != 100 ||
            result.GroupBy(item => item.Metric).Any(group => group.Count() != 1) ||
            required.Except(result.Select(item => item.Metric)).Any())
            throw Validation("SUPPLIER_PERFORMANCE_DIMENSION_INVALID",
                "All seven performance dimensions must appear exactly once and total 100 percent.");
        return result;
    }

    private static IReadOnlyList<BandDefinition> ParseBands(
        IEnumerable<string> values)
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
                throw Validation("SUPPLIER_PERFORMANCE_BAND_INVALID",
                    $"Performance band '{value}' must use BandName=Minimum-Maximum.");
            result.Add(new BandDefinition(pair[0], minimum, maximum));
        }
        result = result.OrderBy(item => item.Minimum).ToList();
        if (result.Count == 0 || result[0].Minimum != 0 ||
            result[^1].Maximum != 100 ||
            result.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() != 1) ||
            result.Zip(result.Skip(1), (left, right) =>
                left.Maximum == right.Minimum).Any(valid => !valid))
            throw Validation("SUPPLIER_PERFORMANCE_BAND_INVALID",
                "Performance bands must be unique and contiguous from 0 through 100.");
        return result;
    }

    private static string? ResolveBand(
        IReadOnlyList<BandDefinition> bands,
        decimal score)
    {
        var matches = bands.Where((item, index) =>
            score >= item.Minimum &&
            (score < item.Maximum ||
             index == bands.Count - 1 && score == item.Maximum)).ToList();
        return matches.Count == 1 ? matches[0].Name : null;
    }

    internal static Dictionary<ProcurementSupplierPerformanceMetricKey, Observation>
        BuildObservations(
            IReadOnlyCollection<PurchaseOrder> orders,
            IReadOnlyCollection<RequestForQuotationItem> rfqItems,
            IReadOnlyCollection<PurchaseRequisitionItem> requisitionItems,
            IReadOnlyCollection<RequestForQuotationInvitation> invitations,
            IReadOnlyCollection<QualityIncident> incidents,
            IReadOnlyCollection<Contract> contracts,
            decimal responseTargetHours,
            DateTime now)
    {
        var result = new Dictionary<ProcurementSupplierPerformanceMetricKey, Observation>();
        var deliveries = orders.Select(order => new
            {
                Promise = order.PromisedDate ?? order.RequiredDate ??
                    order.Items.Where(line => line.ExpectedDeliveryDate.HasValue)
                        .Select(line => line.ExpectedDeliveryDate).Min(),
                Actual = order.Receipts.Count == 0
                    ? (DateTime?)null
                    : order.Receipts.Max(receipt => receipt.ReceiptDate)
            })
            .Where(item => item.Promise.HasValue && item.Actual.HasValue).ToList();
        var deliveryScores = deliveries.Select(item =>
            item.Actual!.Value <= item.Promise!.Value
                ? 100m
                : ProcurementSupplierPerformanceFormula.Clamp(
                    100m - (decimal)(item.Actual.Value - item.Promise.Value).TotalDays * 5m))
            .ToList();
        result[ProcurementSupplierPerformanceMetricKey.DeliveryTimeliness] =
            Average(deliveryScores, deliveries.Count(item =>
                    item.Actual!.Value <= item.Promise!.Value),
                deliveries.Count, "percent on-time orders",
                "PurchaseOrder promised/required date and PurchaseOrderReceipt receipt date",
                "No received order has both an authoritative promised and actual receipt date.");

        var receiptLines = orders.SelectMany(order => order.Receipts.SelectMany(receipt =>
            receipt.Items.Select(line => new { Receipt = receipt, Line = line }))).ToList();
        var qualityScores = receiptLines.Select(item =>
        {
            var status = item.Line.QualityStatus ?? item.Receipt.InspectionResult;
            if (string.Equals(status, "Passed", StringComparison.OrdinalIgnoreCase))
                return 100m;
            if (string.Equals(status, "Conditional", StringComparison.OrdinalIgnoreCase))
                return 50m;
            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                return 0m;
            return item.Line.ReceivedQuantity > 0
                ? ProcurementSupplierPerformanceFormula.Clamp(
                    item.Line.AcceptedQuantity / item.Line.ReceivedQuantity * 100)
                : (decimal?)null;
        }).Where(item => item.HasValue).Select(item => item!.Value).ToList();
        result[ProcurementSupplierPerformanceMetricKey.GrnQuality] =
            Average(qualityScores, qualityScores.Sum(), qualityScores.Count,
                "average GRN inspection/acceptance score",
                "PurchaseOrderReceipt inspection and receipt-line quality/acceptance",
                "No GRN inspection or acceptance observation exists in the period.");

        var received = receiptLines.Sum(item => item.Line.ReceivedQuantity);
        var rejected = receiptLines.Sum(item => item.Line.RejectedQuantity);
        result[ProcurementSupplierPerformanceMetricKey.RejectionRate] =
            received > 0
                ? new Observation(
                    ProcurementSupplierPerformanceFormula.Clamp(
                        100 - rejected / received * 100),
                    receiptLines.Count, received - rejected, received,
                    "accepted percentage after rejection",
                    "PurchaseOrderReceiptItem accepted/rejected quantities", null)
                : Missing("PurchaseOrderReceiptItem accepted/rejected quantities",
                    "No received quantity exists in the period.");

        var rfqMap = rfqItems.ToDictionary(item => item.Id);
        var requisitionMap = requisitionItems.ToDictionary(item => item.Id);
        var priceScores = new List<decimal>();
        decimal actualValue = 0;
        decimal baselineValue = 0;
        foreach (var order in orders)
        foreach (var line in order.Items.Where(item => item.UnitPrice >= 0))
        {
            PurchaseRequisitionItem? baseline = null;
            if (line.SourceRfqItemId.HasValue &&
                rfqMap.TryGetValue(line.SourceRfqItemId.Value, out var rfqItem) &&
                rfqItem.SourcePurchaseRequisitionItemId.HasValue)
                requisitionMap.TryGetValue(
                    rfqItem.SourcePurchaseRequisitionItemId.Value, out baseline);
            if (baseline is null && order.SourceRequisitionId.HasValue)
            {
                var candidates = requisitionItems.Where(item =>
                    item.RequisitionId == order.SourceRequisitionId.Value &&
                    item.InventoryItemId == line.InventoryItemId &&
                    string.Equals(item.UnitOfMeasure, line.UnitOfMeasure,
                        StringComparison.OrdinalIgnoreCase)).ToList();
                if (candidates.Count == 1) baseline = candidates[0];
            }
            if (baseline is null || baseline.EstimatedUnitPrice <= 0) continue;
            var variance = (line.UnitPrice - baseline.EstimatedUnitPrice) /
                baseline.EstimatedUnitPrice * 100;
            priceScores.Add(ProcurementSupplierPerformanceFormula.Clamp(
                variance <= 0 ? 100 : 100 - variance));
            actualValue += line.UnitPrice * line.OrderedQuantity;
            baselineValue += baseline.EstimatedUnitPrice * line.OrderedQuantity;
        }
        result[ProcurementSupplierPerformanceMetricKey.PriceCompetitiveness] =
            Average(priceScores, actualValue, baselineValue,
                "average source-estimate price score",
                "PurchaseOrderItem price and source PurchaseRequisitionItem estimate",
                "No PO line resolves unambiguously to a positive source estimate.");

        var responseScores = new List<decimal>();
        decimal totalResponseHours = 0;
        foreach (var invitation in invitations)
        {
            var hours = invitation.RespondedAt.HasValue
                ? (decimal)Math.Max(0,
                    (invitation.RespondedAt.Value - invitation.InvitedAt).TotalHours)
                : (decimal)Math.Max(0, (now - invitation.InvitedAt).TotalHours);
            if (!invitation.RespondedAt.HasValue && hours <= responseTargetHours)
                continue;
            totalResponseHours += hours;
            responseScores.Add(ResponseScore(hours, responseTargetHours,
                invitation.RespondedAt.HasValue));
        }
        foreach (var incident in incidents.Where(item => item.RequiresSupplierResponse))
        {
            var started = incident.ReportedDate ?? incident.IncidentDate;
            var hours = incident.SupplierResponseDate.HasValue
                ? (decimal)Math.Max(0,
                    (incident.SupplierResponseDate.Value - started).TotalHours)
                : (decimal)Math.Max(0, (now - started).TotalHours);
            if (!incident.SupplierResponseDate.HasValue && hours <= responseTargetHours)
                continue;
            totalResponseHours += hours;
            responseScores.Add(ResponseScore(hours, responseTargetHours,
                incident.SupplierResponseDate.HasValue));
        }
        result[ProcurementSupplierPerformanceMetricKey.Responsiveness] =
            Average(responseScores, totalResponseHours, responseScores.Count,
                "average response-time score",
                "RFQ invitation and supplier-required quality-incident response timestamps",
                "No matured supplier-response observation exists in the period.");

        var activityExists = orders.Count + contracts.Count > 0;
        var resolvedComplaints = incidents.Count(item =>
            string.Equals(item.Status, "Resolved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Status, "Closed", StringComparison.OrdinalIgnoreCase));
        result[ProcurementSupplierPerformanceMetricKey.ComplaintResolution] =
            incidents.Count > 0
                ? new Observation(decimal.Round(
                        (decimal)resolvedComplaints / incidents.Count * 100, 2,
                        MidpointRounding.AwayFromZero),
                    incidents.Count, resolvedComplaints, incidents.Count,
                    "resolved complaint percentage",
                    "QualityIncident status", null)
                : activityExists
                    ? new Observation(100, 1, 0, 0,
                        "no complaints during qualifying activity",
                        "QualityIncident absence with PO/contract activity", null)
                    : Missing("QualityIncident status",
                        "No qualifying activity exists from which to measure complaints.");

        var contractScores = contracts.Select(contract =>
        {
            if (string.Equals(contract.Status, "Terminated",
                    StringComparison.OrdinalIgnoreCase))
                return 0m;
            if (string.Equals(contract.Status, "Suspended",
                    StringComparison.OrdinalIgnoreCase))
                return 25m;
            if (string.Equals(contract.Status, "Completed",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!contract.CompletedAt.HasValue || !contract.EndDate.HasValue ||
                    contract.CompletedAt.Value <= contract.EndDate.Value)
                    return 100m;
                return ProcurementSupplierPerformanceFormula.Clamp(
                    100m - (decimal)(contract.CompletedAt.Value -
                                     contract.EndDate.Value).TotalDays * 2m);
            }
            if (string.Equals(contract.Status, "Active",
                    StringComparison.OrdinalIgnoreCase))
                return contract.EndDate.HasValue && contract.EndDate.Value < now
                    ? 0m
                    : 100m;
            return (decimal?)null;
        }).Where(item => item.HasValue).Select(item => item!.Value).ToList();
        result[ProcurementSupplierPerformanceMetricKey.ContractCompletion] =
            Average(contractScores, contractScores.Sum(), contractScores.Count,
                "average contract completion/compliance score",
                "Contract status, end date, completed date, and termination state",
                "No Active, Completed, Suspended, or Terminated contract exists in the period.");
        return result;
    }

    private static Observation Average(
        IReadOnlyCollection<decimal> values,
        decimal numerator,
        decimal denominator,
        string unit,
        string source,
        string missing) =>
        values.Count == 0
            ? Missing(source, missing)
            : new Observation(decimal.Round(values.Average(), 2,
                    MidpointRounding.AwayFromZero),
                values.Count, numerator, denominator, unit, source, null);

    private static Observation Missing(string source, string reason) =>
        new(null, 0, null, null, string.Empty, source, reason);

    private static decimal ResponseScore(
        decimal hours,
        decimal target,
        bool responded) =>
        !responded ? 0 :
        hours <= target ? 100 :
        ProcurementSupplierPerformanceFormula.Clamp(target / hours * 100);

    private static List<ProcurementSupplierPerformanceFindingDto> BuildFindings(
        IReadOnlyCollection<ProcurementSupplierPerformanceMeasureDto> measures,
        ProcurementSupplierPerformanceFormulaResult formula,
        string? band,
        decimal minimumScore)
    {
        var findings = measures.Where(item => !item.Score.HasValue)
            .Select(item => new ProcurementSupplierPerformanceFindingDto
            {
                Code = $"SUPPLIER_PERFORMANCE_{item.Metric.ToString().ToUpperInvariant()}_DATA_MISSING",
                Message = $"{item.Metric}: {item.MissingReason}",
                DataGap = true
            }).ToList();
        if (!formula.DataComplete)
            findings.Add(new ProcurementSupplierPerformanceFindingDto
            {
                Code = "SUPPLIER_PERFORMANCE_COVERAGE_INSUFFICIENT",
                Message =
                    $"Measured policy weight {formula.CoveragePercent:0.00}% is below the approved minimum coverage.",
                Breach = true,
                DataGap = true
            });
        if (formula.DataComplete && band is null)
            findings.Add(new ProcurementSupplierPerformanceFindingDto
            {
                Code = "SUPPLIER_PERFORMANCE_BAND_UNRESOLVED",
                Message = "The calculated score does not resolve to exactly one configured performance band.",
                Breach = true,
                DataGap = true
            });
        if (formula.DataComplete && formula.OverallScore < minimumScore)
            findings.Add(new ProcurementSupplierPerformanceFindingDto
            {
                Code = "SUPPLIER_PERFORMANCE_MINIMUM_SCORE_BREACHED",
                Message =
                    $"Performance score {formula.OverallScore:0.00} is below the approved minimum {minimumScore:0.00}.",
                Breach = true
            });
        return findings;
    }

    private static object BuildSourceSnapshot(
        IReadOnlyCollection<PurchaseOrder> orders,
        IReadOnlyCollection<RequestForQuotationItem> rfqItems,
        IReadOnlyCollection<PurchaseRequisitionItem> requisitionItems,
        IReadOnlyCollection<RequestForQuotationInvitation> invitations,
        IReadOnlyCollection<QualityIncident> incidents,
        IReadOnlyCollection<Contract> contracts) => new
    {
        schemaVersion = "tdc.supplier-performance-sources.v1",
        purchaseOrders = orders.OrderBy(item => item.Id).Select(item => new
        {
            item.Id, item.OrderNumber, item.OrderDate, item.RequiredDate,
            item.PromisedDate, item.ReceivedDate, item.Status, item.Currency,
            item.TotalAmount, item.SourceRequisitionId, item.SourceRfqId,
            lines = item.Items.OrderBy(line => line.Id).Select(line => new
            {
                line.Id, line.InventoryItemId, line.OrderedQuantity,
                line.UnitOfMeasure, line.UnitPrice, line.ExpectedDeliveryDate,
                line.SourceRfqItemId
            }),
            receipts = item.Receipts.OrderBy(receipt => receipt.Id).Select(receipt => new
            {
                receipt.Id, receipt.ReceiptNumber, receipt.ReceiptDate,
                receipt.Status, receipt.InspectionDate, receipt.InspectionResult,
                lines = receipt.Items.OrderBy(line => line.Id).Select(line => new
                {
                    line.Id, line.PurchaseOrderItemId, line.ReceivedQuantity,
                    line.AcceptedQuantity, line.RejectedQuantity,
                    line.QualityStatus, line.RejectionReason
                })
            })
        }),
        rfqItems = rfqItems.OrderBy(item => item.Id).Select(item => new
        {
            item.Id, item.RfqId, item.SourcePurchaseRequisitionItemId,
            item.InventoryItemId, item.Quantity, item.UnitOfMeasure
        }),
        requisitionItems = requisitionItems.OrderBy(item => item.Id).Select(item => new
        {
            item.Id, item.RequisitionId, item.InventoryItemId, item.Quantity,
            item.UnitOfMeasure, item.EstimatedUnitPrice
        }),
        responses = invitations.OrderBy(item => item.Id).Select(item => new
        {
            item.Id, item.RfqId, item.InvitedAt, item.RespondedAt, item.Status
        }),
        incidents = incidents.OrderBy(item => item.Id).Select(item => new
        {
            item.Id, item.PurchaseOrderId, item.IncidentNumber, item.IncidentDate,
            item.IncidentType, item.Severity, item.Status, item.ReportedDate,
            item.ResolvedDate, item.RequiresSupplierResponse,
            item.SupplierResponseDate
        }),
        contracts = contracts.OrderBy(item => item.Id).Select(item => new
        {
            item.Id, item.ContractNumber, item.ContractType, item.Status,
            item.StartDate, item.EndDate, item.CompletedAt, item.TerminatedAt
        })
    };

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierPerformanceAuthorizationException(
                "Supplier portal users cannot calculate supplier scorecards.");
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = SourceType,
                SourceReference = sourceReference
            }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierPerformanceAuthorizationException(
                decision.Message);
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierPerformanceAuthorizationException(
                "Supplier portal users cannot access performance-scorecard administration.");
        if (HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read"))
            return;
        throw new ProcurementSupplierPerformanceAuthorizationException(
            "The procurement records read permission is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw new ProcurementSupplierPerformanceAuthorizationException(
                "An authenticated tenant context is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() =>
        _currentUser.HasRole(Constants.Roles.SuperAdmin);

    private async Task RecordEventAsync(
        ProcurementSupplierPerformanceScorecard scorecard,
        IReadOnlyCollection<ProcurementSupplierPerformanceFindingDto> findings,
        string correlation,
        DateTime now,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "supplier-performance", scorecard.TenantId, scorecard.Id,
                $"Calculated-{correlation}"),
            EventType = EventType,
            Action = "Calculated",
            Result = findings.Any(item => item.Breach)
                ? ProcurementControlEventResult.ReviewRequired
                : ProcurementControlEventResult.Succeeded,
            RuleCode = scorecard.EligibilityAction.ToString(),
            RuleId = scorecard.PolicyDecisionId,
            RuleVersion =
                $"{scorecard.PolicyProfileCode}/v{scorecard.PolicyProfileVersion}",
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = scorecard.Id,
            SourceReference = scorecard.ScorecardReference,
            InputValues = new
            {
                scorecard.BusinessPartnerId,
                scorecard.PeriodStartUtc,
                scorecard.PeriodEndUtc,
                scorecard.PolicyDecisionId,
                scorecard.SourceSnapshotHash
            },
            ResultValues = new
            {
                scorecard.OverallScore,
                scorecard.PerformanceBand,
                scorecard.DataStatus,
                scorecard.DataCoveragePercent,
                scorecard.MinimumScoreBreached,
                scorecard.IntegrityHash
            },
            After = ScorecardSnapshot(scorecard),
            CorrelationId = correlation,
            CausationId = correlation,
            OccurredAtUtc = now
        }, cancellationToken);

    private async Task PublishNotificationAsync(
        ProcurementSupplierPerformanceScorecard scorecard,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = scorecard.TenantId,
                TopicKey = scorecard.MinimumScoreBreached
                    ? "procurement.supplier-performance.minimum-breached"
                    : "procurement.supplier-performance.calculated",
                NotificationType = "ProcurementSupplierPerformanceControl",
                EntityType = SourceType,
                EntityId = scorecard.Id,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["businessPartnerId"] = scorecard.BusinessPartnerId,
                    ["scorecardReference"] = scorecard.ScorecardReference,
                    ["overallScore"] = scorecard.OverallScore ?? 0,
                    ["performanceBand"] =
                        scorecard.PerformanceBand ?? "Unresolved",
                    ["dataCoveragePercent"] = scorecard.DataCoveragePercent,
                    ["minimumScoreBreached"] = scorecard.MinimumScoreBreached
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish supplier-performance notification for {ScorecardId}",
                scorecard.Id);
        }
    }

    private static ProcurementSupplierPerformanceListItemDto MapList(
        ProcurementSupplierPerformanceScorecard item,
        DateTime now) => new()
    {
        Id = item.Id,
        ScorecardReference = item.ScorecardReference,
        ScorecardSequence = item.ScorecardSequence,
        BusinessPartnerId = item.BusinessPartnerId,
        PartnerCode = item.BusinessPartner?.PartnerCode ?? string.Empty,
        PartnerName = item.BusinessPartner?.PartnerName ?? string.Empty,
        CalculatedAtUtc = item.CalculatedAtUtc,
        PeriodStartUtc = item.PeriodStartUtc,
        PeriodEndUtc = item.PeriodEndUtc,
        NextReviewDueAtUtc = item.NextReviewDueAtUtc,
        OverallScore = item.OverallScore,
        PerformanceBand = item.PerformanceBand,
        DataStatus = item.DataStatus,
        DataCoveragePercent = item.DataCoveragePercent,
        MinimumScoreBreached = item.MinimumScoreBreached,
        IsCurrent = item.NextReviewDueAtUtc > now &&
                    item.DataStatus ==
                    ProcurementSupplierPerformanceDataStatus.Complete,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementSupplierPerformanceScorecardDto Map(
        ProcurementSupplierPerformanceScorecard item,
        DateTime now)
    {
        var result = new ProcurementSupplierPerformanceScorecardDto
        {
            PolicyDecisionId = item.PolicyDecisionId,
            PolicyProfileId = item.PolicyProfileId,
            PolicyProfileCode = item.PolicyProfileCode,
            PolicyProfileVersion = item.PolicyProfileVersion,
            PolicyValueHash = item.PolicyValueHash,
            PerformanceWindowMonths = item.PerformanceWindowMonths,
            MinimumScore = item.MinimumScore,
            MinimumDataCoveragePercent = item.MinimumDataCoveragePercent,
            ResponseTargetHours = item.ResponseTargetHours,
            EligibilityAction = item.EligibilityAction,
            SourceSnapshotHash = item.SourceSnapshotHash,
            SupplierEligibilityDecisionHash =
                item.SupplierEligibilityDecisionHash,
            RiskAssessmentId = item.RiskAssessmentId,
            RiskAssessmentIntegrityHash = item.RiskAssessmentIntegrityHash,
            SourceType = item.SourceType,
            SourceId = item.SourceId,
            SourceReference = item.SourceReference,
            CalculatedByUserId = item.CalculatedByUserId,
            CalculatedByName = item.CalculatedByName,
            Measures = DeserializeList<ProcurementSupplierPerformanceMeasureDto>(
                item.MeasureResultsJson),
            Findings = DeserializeList<ProcurementSupplierPerformanceFindingDto>(
                item.FindingsJson)
        };
        var list = MapList(item, now);
        result.Id = list.Id;
        result.ScorecardReference = list.ScorecardReference;
        result.ScorecardSequence = list.ScorecardSequence;
        result.BusinessPartnerId = list.BusinessPartnerId;
        result.PartnerCode = list.PartnerCode;
        result.PartnerName = list.PartnerName;
        result.CalculatedAtUtc = list.CalculatedAtUtc;
        result.PeriodStartUtc = list.PeriodStartUtc;
        result.PeriodEndUtc = list.PeriodEndUtc;
        result.NextReviewDueAtUtc = list.NextReviewDueAtUtc;
        result.OverallScore = list.OverallScore;
        result.PerformanceBand = list.PerformanceBand;
        result.DataStatus = list.DataStatus;
        result.DataCoveragePercent = list.DataCoveragePercent;
        result.MinimumScoreBreached = list.MinimumScoreBreached;
        result.IsCurrent = list.IsCurrent;
        result.IntegrityHash = list.IntegrityHash;
        return result;
    }

    private static bool IsCurrent(
        ProcurementSupplierPerformanceScorecard item,
        PolicyResolution policy,
        DateTime now) =>
        item.NextReviewDueAtUtc > now &&
        item.DataStatus == ProcurementSupplierPerformanceDataStatus.Complete &&
        item.PolicyDecisionId == policy.Decision.Id &&
        string.Equals(item.PolicyValueHash, Hash(policy.Decision.ValueJson),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsFirstAwardBaseline(
        ProcurementSupplierPerformanceScorecard item,
        PolicyResolution policy,
        DateTime now)
    {
        var observedScores = new decimal?[]
        {
            item.DeliveryTimelinessScore, item.GrnQualityScore,
            item.RejectionRateScore, item.PriceCompetitivenessScore,
            item.ResponsivenessScore, item.ComplaintResolutionScore,
            item.ContractCompletionScore
        };
        return item.PurchaseOrderCount == 0 && item.ReceiptCount == 0 &&
            observedScores.Any(score => score.HasValue) &&
            observedScores.Where(score => score.HasValue)
                .All(score => score!.Value >= item.MinimumScore) &&
            item.NextReviewDueAtUtc > now && item.IntegrityHash.Length == 64 &&
            item.PolicyDecisionId == policy.Decision.Id &&
            string.Equals(item.PolicyValueHash, Hash(policy.Decision.ValueJson),
                StringComparison.OrdinalIgnoreCase);
    }

    private static void Capture(ProcurementSupplierPerformanceScorecard item)
    {
        item.SnapshotJson = Serialize(ScorecardSnapshot(item));
        item.IntegrityHash = Hash(item.SnapshotJson);
    }

    private static object ScorecardSnapshot(
        ProcurementSupplierPerformanceScorecard item) => new
    {
        schemaVersion = "tdc.supplier-performance-scorecard.v1",
        item.Id,
        item.TenantId,
        item.ScorecardReference,
        item.ScorecardSequence,
        item.BusinessPartnerId,
        item.CalculatedAtUtc,
        item.PeriodStartUtc,
        item.PeriodEndUtc,
        item.NextReviewDueAtUtc,
        item.PolicyDecisionId,
        item.PolicyProfileId,
        item.PolicyProfileCode,
        item.PolicyProfileVersion,
        item.PolicyValueHash,
        item.PerformanceWindowMonths,
        item.MinimumScore,
        item.MinimumDataCoveragePercent,
        item.ResponseTargetHours,
        item.EligibilityAction,
        item.DataStatus,
        item.DataCoveragePercent,
        item.OverallScore,
        item.PerformanceBand,
        item.MinimumScoreBreached,
        item.DeliveryTimelinessScore,
        item.GrnQualityScore,
        item.RejectionRateScore,
        item.PriceCompetitivenessScore,
        item.ResponsivenessScore,
        item.ComplaintResolutionScore,
        item.ContractCompletionScore,
        item.PurchaseOrderCount,
        item.ReceiptCount,
        item.ReceiptLineCount,
        item.PriceComparisonCount,
        item.ResponseObservationCount,
        item.ComplaintCount,
        item.ContractCount,
        item.MeasureResultsJson,
        item.SourceSnapshotHash,
        item.SupplierEligibilityDecisionHash,
        item.RiskAssessmentId,
        item.RiskAssessmentIntegrityHash,
        item.FindingsJson,
        item.IdempotencyKey,
        item.CorrelationId,
        item.SourceType,
        item.SourceId,
        item.SourceReference,
        item.CalculatedByUserId
    };

    private async Task ExecuteAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
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

    private static string NormalizeCorrelation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Validation("SUPPLIER_PERFORMANCE_CORRELATION_REQUIRED",
                "X-Correlation-ID is required.");
        return value.Trim().Length <= 100 ? value.Trim() : Hash(value);
    }

    private static string NormalizeKey(string value) =>
        value.Trim().Length <= 100 ? value.Trim() : Hash(value);

    private static IReadOnlyList<T> DeserializeList<T>(string value)
    {
        try { return JsonSerializer.Deserialize<List<T>>(value, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string Serialize(object value) =>
        JsonSerializer.Serialize(value, JsonOptions);
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null :
        value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];

    private static ProcurementSupplierPerformanceNotFoundException NotFound(
        string code,
        string message) => new(code, message);
    private static ProcurementSupplierPerformanceValidationException Validation(
        string code,
        string message) => new(code, message);
    private static ProcurementSupplierPerformanceConflictException Conflict(
        string code,
        string message) => new(code, message);

    private sealed record PolicyResolution(
        ProcurementConfigurationDecision Decision,
        ProcurementSupplierRiskDecisionValueDto Value);
    private sealed record DimensionDefinition(
        ProcurementSupplierPerformanceMetricKey Metric,
        decimal Weight);
    private sealed record BandDefinition(
        string Name,
        decimal Minimum,
        decimal Maximum);
    internal sealed record Observation(
        decimal? Score,
        int Count,
        decimal? Numerator,
        decimal? Denominator,
        string Unit,
        string Source,
        string? MissingReason);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
