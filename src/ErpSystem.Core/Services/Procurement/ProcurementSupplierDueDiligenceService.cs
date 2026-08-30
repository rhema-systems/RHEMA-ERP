using ErpSystem.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public sealed class ProcurementSupplierDueDiligenceService :
    IProcurementSupplierDueDiligenceService
{
    private const string SourceType = "ProcurementSupplierDueDiligence";
    private const string EventType = "ProcurementSupplierDueDiligenceControl";
    private const string ManagePermission = "procurement.supplier.manage";
    private const string ReviewPermission = "procurement.supplier.review";
    private const string ApprovePermission = "procurement.supplier.approve";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToArray();
    private static readonly ProcurementSupplierDueDiligenceCheckType[] RequiredChecks =
        Enum.GetValues<ProcurementSupplierDueDiligenceCheckType>();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementSupplierDueDiligenceService> _logger;

    public ProcurementSupplierDueDiligenceService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IWorkflowInstanceService workflowInstances,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementSupplierDueDiligenceService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _workflowInstances = workflowInstances;
        _notificationTopics = notificationTopics;
        _logger = logger;
    }

    private IGenericRepository<ProcurementSupplierDueDiligenceReview> Reviews =>
        _unitOfWork.Repository<ProcurementSupplierDueDiligenceReview>();
    private IGenericRepository<ProcurementSupplierDueDiligenceCheck> Checks =>
        _unitOfWork.Repository<ProcurementSupplierDueDiligenceCheck>();
    private IGenericRepository<ProcurementSupplierDueDiligenceEvidenceLink> EvidenceLinks =>
        _unitOfWork.Repository<ProcurementSupplierDueDiligenceEvidenceLink>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions =>
        _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<WorkflowInstance> WorkflowInstances =>
        _unitOfWork.Repository<WorkflowInstance>();

    public async Task<ProcurementSupplierDueDiligenceSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var policy = await TryResolvePolicyAsync(now, cancellationToken);
        var query = Reviews.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        var supplierCount = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.IsActive &&
                (item.PartnerType.Contains("Supplier") ||
                 item.PartnerType.Contains("Contractor") ||
                 item.PartnerType.Contains("Both")))
            .CountAsync(cancellationToken);
        var currentSupplierIds = await query.Where(item =>
                item.Status == ProcurementSupplierDueDiligenceStatus.Approved &&
                item.Outcome == ProcurementSupplierDueDiligenceOutcome.Clear &&
                item.ReviewPeriodStartUtc <= now && item.ReviewPeriodEndUtc > now)
            .Select(item => item.BusinessPartnerId).Distinct().ToListAsync(cancellationToken);
        return new ProcurementSupplierDueDiligenceSummaryDto
        {
            TotalReviews = await query.CountAsync(cancellationToken),
            DraftCount = await query.CountAsync(item =>
                item.Status == ProcurementSupplierDueDiligenceStatus.Draft, cancellationToken),
            PendingApprovalCount = await query.CountAsync(item =>
                item.Status == ProcurementSupplierDueDiligenceStatus.PendingApproval, cancellationToken),
            CurrentApprovedCount = currentSupplierIds.Count,
            DueOrExpiredCount = await query.CountAsync(item =>
                item.Status == ProcurementSupplierDueDiligenceStatus.Expired ||
                (item.Status == ProcurementSupplierDueDiligenceStatus.Approved &&
                 item.ReviewPeriodEndUtc <= now), cancellationToken),
            AdverseCount = await query.CountAsync(item =>
                item.Outcome == ProcurementSupplierDueDiligenceOutcome.Adverse &&
                (item.Status == ProcurementSupplierDueDiligenceStatus.Approved ||
                 item.Status == ProcurementSupplierDueDiligenceStatus.PendingApproval), cancellationToken),
            SuppliersWithoutCurrentReview = Math.Max(0, supplierCount - currentSupplierIds.Count),
            PolicyAvailable = policy is not null,
            PolicyProfileCode = policy?.Decision.Profile.ProfileCode,
            PolicyProfileVersion = policy?.Decision.Profile.Version,
            ReviewFrequencyMonths = policy?.Value.ReviewFrequencyMonths,
            PolicyReleaseGate = policy is null
                ? "No unique Published/effective/approved/evidenced DEC-011 policy is available."
                : null
        };
    }

    public async Task<ProcurementSupplierDueDiligencePageDto> SearchAsync(
        ProcurementSupplierDueDiligenceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 200);
        var now = DateTime.UtcNow;
        var query = ReviewQuery().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.ReviewReference.Contains(search) ||
                item.BusinessPartner.PartnerCode.Contains(search) ||
                item.BusinessPartner.PartnerName.Contains(search));
        }
        if (request.BusinessPartnerId.HasValue)
            query = query.Where(item => item.BusinessPartnerId == request.BusinessPartnerId.Value);
        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);
        if (request.ReviewType.HasValue)
            query = query.Where(item => item.ReviewType == request.ReviewType.Value);
        if (request.DueOnly == true)
            query = query.Where(item =>
                item.Status == ProcurementSupplierDueDiligenceStatus.Expired ||
                (item.Status == ProcurementSupplierDueDiligenceStatus.Approved &&
                 item.ReviewPeriodEndUtc <= now));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.ReviewPeriodStartUtc)
            .ThenBy(item => item.BusinessPartner.PartnerCode)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new ProcurementSupplierDueDiligencePageDto
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = rows.Select(item => MapList(item, now)).ToList()
        };
    }

    public async Task<ProcurementSupplierDueDiligenceDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return Map(await LoadAsync(id, tracked: false, cancellationToken), DateTime.UtcNow);
    }

    public async Task<ProcurementSupplierDueDiligenceCurrentStateDto> GetCurrentStateAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        if (!await _unitOfWork.Repository<BusinessPartner>().GetQueryable(item =>
                item.Id == businessPartnerId && item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted).AnyAsync(cancellationToken))
            throw NotFound("SUPPLIER_DUE_DILIGENCE_SUPPLIER_NOT_FOUND",
                "The supplier was not found in the current tenant.");
        return await BuildCurrentStateAsync(_currentUser.TenantId, businessPartnerId,
            DateTime.UtcNow, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementSupplierDueDiligenceWorkflowOptionDto>>
        GetWorkflowOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .AsNoTracking().OrderBy(item => item.Name).ThenByDescending(item => item.Version)
            .Select(item => new ProcurementSupplierDueDiligenceWorkflowOptionDto
            {
                Id = item.Id,
                Name = item.Name,
                Version = item.Version
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementSupplierDueDiligenceSupplierOptionDto>>
        GetSupplierOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var suppliers = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                (item.PartnerType.Contains("Supplier") ||
                 item.PartnerType.Contains("Contractor") ||
                 item.PartnerType.Contains("Both")))
            .AsNoTracking().OrderBy(item => item.PartnerCode).ToListAsync(cancellationToken);
        var states = await Reviews.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.Status == ProcurementSupplierDueDiligenceStatus.Approved)
            .GroupBy(item => item.BusinessPartnerId)
            .Select(group => new
            {
                BusinessPartnerId = group.Key,
                Due = group.Max(item => item.ReviewPeriodEndUtc),
                Current = group.Any(item =>
                    item.Outcome == ProcurementSupplierDueDiligenceOutcome.Clear &&
                    item.ReviewPeriodStartUtc <= now && item.ReviewPeriodEndUtc > now)
            }).ToDictionaryAsync(item => item.BusinessPartnerId, cancellationToken);
        return suppliers.Select(item =>
        {
            states.TryGetValue(item.Id, out var state);
            return new ProcurementSupplierDueDiligenceSupplierOptionDto
            {
                Id = item.Id,
                Code = item.PartnerCode,
                Name = item.PartnerName,
                HasCurrentReview = state?.Current ?? false,
                NextReviewDueAtUtc = state?.Due
            };
        }).ToList();
    }

    public async Task<ProcurementSupplierDueDiligenceDto> CreateAsync(
        CreateProcurementSupplierDueDiligenceRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(ManagePermission, request.BusinessPartnerId.ToString("N"),
            correlation, cancellationToken);
        if (request.BusinessPartnerId == Guid.Empty)
            throw Validation("SUPPLIER_DUE_DILIGENCE_SUPPLIER_REQUIRED", "A supplier is required.");
        var replay = await ReviewQuery().AsNoTracking().SingleOrDefaultAsync(item =>
            item.CreationCorrelationId == correlation, cancellationToken);
        if (replay is not null) return Map(replay, DateTime.UtcNow);
        var partner = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item => item.Id == request.BusinessPartnerId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("SUPPLIER_DUE_DILIGENCE_SUPPLIER_NOT_FOUND",
                "The supplier was not found in the current tenant.");
        if (!(partner.PartnerType.Contains("Supplier", StringComparison.OrdinalIgnoreCase) ||
              partner.PartnerType.Contains("Contractor", StringComparison.OrdinalIgnoreCase) ||
              partner.PartnerType.Contains("Both", StringComparison.OrdinalIgnoreCase)))
            throw Validation("SUPPLIER_DUE_DILIGENCE_PARTNER_TYPE_INVALID",
                "Due diligence is available only for suppliers and contractors.");
        if (await Reviews.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id && !item.IsDeleted &&
                (item.Status == ProcurementSupplierDueDiligenceStatus.Draft ||
                 item.Status == ProcurementSupplierDueDiligenceStatus.PendingApproval))
            .AnyAsync(cancellationToken))
            throw Conflict("SUPPLIER_DUE_DILIGENCE_OPEN_REVIEW_EXISTS",
                "The supplier already has a Draft or PendingApproval review.");
        var now = DateTime.UtcNow;
        var policy = await ResolvePolicyAsync(now, cancellationToken);
        var workflow = await ValidateWorkflowAsync(request.WorkflowDefinitionId, cancellationToken);
        var previous = await Reviews.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id && !item.IsDeleted &&
                (item.Status == ProcurementSupplierDueDiligenceStatus.Approved ||
                 item.Status == ProcurementSupplierDueDiligenceStatus.Expired ||
                 item.Status == ProcurementSupplierDueDiligenceStatus.Superseded))
            .AsNoTracking().OrderByDescending(item => item.CycleNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (request.ReviewType == ProcurementSupplierDueDiligenceReviewType.Initial &&
            previous is not null)
            throw Validation("SUPPLIER_DUE_DILIGENCE_INITIAL_REVIEW_EXISTS",
                "A supplier with review history requires an Annual reassessment.");
        if (request.ReviewType == ProcurementSupplierDueDiligenceReviewType.Annual &&
            previous is null)
            throw Validation("SUPPLIER_DUE_DILIGENCE_INITIAL_REVIEW_REQUIRED",
                "An Annual reassessment requires a prior review.");
        var maxRetainedCycle = await Reviews.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partner.Id)
            .Select(item => (int?)item.CycleNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var cycle = maxRetainedCycle + 1;
        var periodEnd = now.AddMonths(policy.Value.ReviewFrequencyMonths);
        var entity = new ProcurementSupplierDueDiligenceReview
        {
            TenantId = _currentUser.TenantId,
            BusinessPartnerId = partner.Id,
            ReviewReference = $"DD-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            CycleNumber = cycle,
            ReviewType = request.ReviewType,
            Status = ProcurementSupplierDueDiligenceStatus.Draft,
            Outcome = ProcurementSupplierDueDiligenceOutcome.Pending,
            ReviewPeriodStartUtc = now,
            ReviewPeriodEndUtc = periodEnd,
            NextReviewDueAtUtc = periodEnd,
            PolicyDecisionId = policy.Decision.Id,
            PolicyProfileId = policy.Decision.ProfileId,
            PolicyProfileCode = policy.Decision.Profile.ProfileCode,
            PolicyProfileVersion = policy.Decision.Profile.Version,
            ReviewFrequencyMonths = policy.Value.ReviewFrequencyMonths,
            PolicySnapshotJson = policy.Decision.ValueJson,
            PolicyValueHash = Hash(policy.Decision.ValueJson),
            WorkflowDefinitionId = workflow.Id,
            SupersedesReviewId = previous?.Id,
            Notes = Trim(request.Notes, 1000),
            CreationCorrelationId = correlation,
            LastOperation = "Created",
            LastOperationCorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        foreach (var checkType in RequiredChecks)
        {
            var check = new ProcurementSupplierDueDiligenceCheck
            {
                TenantId = entity.TenantId,
                ReviewId = entity.Id,
                CheckType = checkType,
                Status = ProcurementSupplierDueDiligenceCheckStatus.Pending,
                SourceName = CheckName(checkType),
                SourceReference = "Pending",
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            Capture(check);
            entity.Checks.Add(check);
        }
        Capture(entity);
        await Reviews.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "Created", ProcurementControlEventResult.Succeeded,
            null, Snapshot(entity), request.Notes, [], correlation, now, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken), now);
    }

    public async Task<ProcurementSupplierDueDiligenceDto> UpdateAsync(
        Guid id,
        UpdateProcurementSupplierDueDiligenceRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.ReviewReference, correlation, cancellationToken);
        if (IsReplay(entity, "Updated", correlation)) return Map(entity, DateTime.UtcNow);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierDueDiligenceStatus.Draft,
            "Only a Draft review can be edited.");
        ValidateCheckSet(request.Checks);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Notes = Trim(request.Notes, 1000);
        foreach (var input in request.Checks)
        {
            var check = entity.Checks.Single(item => item.CheckType == input.CheckType);
            check.Status = input.Status;
            check.SourceName = input.SourceName.Trim();
            check.SourceReference = input.SourceReference.Trim();
            check.CheckedAtUtc = input.CheckedAtUtc.HasValue ? EnsureUtc(input.CheckedAtUtc.Value) : null;
            check.ValidUntilUtc = input.ValidUntilUtc.HasValue ? EnsureUtc(input.ValidUntilUtc.Value) : null;
            check.ReviewedById = input.Status == ProcurementSupplierDueDiligenceCheckStatus.Pending
                ? null
                : _currentUser.UserId;
            check.ReviewerName = input.Status == ProcurementSupplierDueDiligenceCheckStatus.Pending
                ? null
                : ActorName;
            check.Notes = Trim(input.Notes, 1000);
            check.UpdatedAt = now;
            check.UpdatedBy = ActorName;
            check.LastModifiedById = _currentUser.UserId;
            foreach (var old in check.EvidenceLinks.Where(item => !item.IsDeleted))
                await EvidenceLinks.DeleteAsync(old);
            check.EvidenceLinks.Clear();
            foreach (var evidence in await ResolveEvidenceAsync(
                         entity.Id, check.Id, check.CheckType, input.Evidence, now, cancellationToken))
            {
                await EvidenceLinks.AddAsync(evidence);
                check.EvidenceLinks.Add(evidence);
            }
            Capture(check);
        }
        Touch(entity, "Updated", correlation, now);
        Capture(entity);
        await Reviews.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "Updated", ProcurementControlEventResult.Succeeded,
            before, Snapshot(entity), request.Notes, [], correlation, now, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken), now);
    }

    public async Task<ProcurementSupplierDueDiligenceDto> SubmitAsync(
        Guid id,
        ProcurementSupplierDueDiligenceLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.ReviewReference, correlation, cancellationToken);
        if (IsReplay(entity, "Submitted", correlation)) return Map(entity, DateTime.UtcNow);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierDueDiligenceStatus.Draft,
            "Only a Draft review can be submitted.");
        EnsureLifecycleEvidence(request.Evidence);
        var now = DateTime.UtcNow;
        await ValidateForDecisionAsync(entity, now, cancellationToken);
        var before = Snapshot(entity);
        entity.Status = ProcurementSupplierDueDiligenceStatus.PendingApproval;
        entity.Outcome = entity.Checks.Any(item =>
            item.Status == ProcurementSupplierDueDiligenceCheckStatus.Adverse)
            ? ProcurementSupplierDueDiligenceOutcome.Adverse
            : ProcurementSupplierDueDiligenceOutcome.Clear;
        entity.SubmittedById = _currentUser.UserId;
        entity.SubmittedAtUtc = now;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "Submitted", correlation, now);
        Capture(entity);
        await Reviews.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var workflow = await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == entity.TenantId && item.Id == entity.WorkflowDefinitionId &&
                !item.IsDeleted)
            .SingleAsync(cancellationToken);
        var instance = await _workflowInstances.StartWorkflowAsync(
            workflow.Id, workflow.EntityTypeId, entity.Id.ToString(), _currentUser.UserId,
            new
            {
                entity.BusinessPartnerId,
                entity.ReviewReference,
                entity.CycleNumber,
                entity.ReviewType,
                entity.Outcome,
                entity.PolicyDecisionId,
                entity.ReviewPeriodEndUtc
            }, cancellationToken);
        entity.WorkflowInstanceId = instance.Id;
        Touch(entity, "Submitted", correlation, now);
        Capture(entity);
        await Reviews.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "Submitted", ProcurementControlEventResult.Succeeded,
            before, Snapshot(entity), request.Comment, request.Evidence, correlation, now, cancellationToken);
        await PublishNotificationAsync("procurement.supplier-due-diligence.submitted",
            entity, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken), now);
    }

    public async Task<ProcurementSupplierDueDiligenceDto> ApproveAsync(
        Guid id,
        ProcurementSupplierDueDiligenceLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.ReviewReference, correlation, cancellationToken);
        if (IsReplay(entity, "Approved", correlation)) return Map(entity, DateTime.UtcNow);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierDueDiligenceStatus.PendingApproval,
            "Only a PendingApproval review can be approved.");
        EnsureLifecycleEvidence(request.Evidence);
        await EnsureIndependentActorAsync(entity.SubmittedById, entity.ReviewReference,
            correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity, approving: true, cancellationToken);
        var now = DateTime.UtcNow;
        await ValidateForDecisionAsync(entity, now, cancellationToken);
        var before = Snapshot(entity);
        var prior = await Reviews.GetQueryable(item =>
                item.TenantId == entity.TenantId &&
                item.BusinessPartnerId == entity.BusinessPartnerId &&
                item.Id != entity.Id && !item.IsDeleted &&
                item.Status == ProcurementSupplierDueDiligenceStatus.Approved)
            .OrderByDescending(item => item.CycleNumber)
            .FirstOrDefaultAsync(cancellationToken);
        await ExecuteAsync(async () =>
        {
            if (prior is not null)
            {
                prior.Status = ProcurementSupplierDueDiligenceStatus.Superseded;
                prior.SupersededByReviewId = entity.Id;
                prior.UpdatedAt = now;
                prior.UpdatedBy = ActorName;
                prior.LastModifiedById = _currentUser.UserId;
                prior.LastOperation = "Superseded";
                prior.LastOperationCorrelationId = correlation;
                Capture(prior);
                await Reviews.UpdateAsync(prior);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            entity.Status = ProcurementSupplierDueDiligenceStatus.Approved;
            entity.ApprovedById = _currentUser.UserId;
            entity.ApprovedAtUtc = now;
            entity.ReviewComment = Trim(request.Comment, 1000);
            Touch(entity, "Approved", correlation, now);
            Capture(entity);
            await Reviews.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        await RecordEventAsync(entity, "Approved",
            entity.Outcome == ProcurementSupplierDueDiligenceOutcome.Clear
                ? ProcurementControlEventResult.Succeeded
                : ProcurementControlEventResult.ReviewRequired,
            before, Snapshot(entity), request.Comment, request.Evidence, correlation, now, cancellationToken);
        await PublishNotificationAsync("procurement.supplier-due-diligence.approved",
            entity, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken), now);
    }

    public async Task<ProcurementSupplierDueDiligenceDto> RejectAsync(
        Guid id,
        ProcurementSupplierDueDiligenceLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.ReviewReference, correlation, cancellationToken);
        if (IsReplay(entity, "Rejected", correlation)) return Map(entity, DateTime.UtcNow);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierDueDiligenceStatus.PendingApproval,
            "Only a PendingApproval review can be rejected.");
        EnsureLifecycleEvidence(request.Evidence);
        await EnsureIndependentActorAsync(entity.SubmittedById, entity.ReviewReference,
            correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity, approving: false, cancellationToken);
        var now = DateTime.UtcNow;
        var before = Snapshot(entity);
        entity.Status = ProcurementSupplierDueDiligenceStatus.Rejected;
        entity.RejectedById = _currentUser.UserId;
        entity.RejectedAtUtc = now;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "Rejected", correlation, now);
        Capture(entity);
        await Reviews.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "Rejected", ProcurementControlEventResult.Rejected,
            before, Snapshot(entity), request.Comment, request.Evidence, correlation, now, cancellationToken);
        await PublishNotificationAsync("procurement.supplier-due-diligence.rejected",
            entity, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken), now);
    }

    public async Task<ProcurementSupplierDueDiligenceDto> SupersedeStaleAsync(
        Guid id,
        ProcurementSupplierDueDiligenceLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.ReviewReference, correlation, cancellationToken);
        if (IsReplay(entity, "PolicySuperseded", correlation))
            return Map(entity, DateTime.UtcNow);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierDueDiligenceStatus.PendingApproval,
            "Only a PendingApproval review can be superseded after a policy replacement.");
        EnsureLifecycleEvidence(request.Evidence);
        await EnsureIndependentActorAsync(entity.SubmittedById, entity.ReviewReference,
            correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity, approving: true, cancellationToken);

        var now = DateTime.UtcNow;
        var currentPolicy = await ResolvePolicyAsync(now, cancellationToken);
        if (entity.PolicyDecisionId == currentPolicy.Decision.Id &&
            entity.PolicyValueHash == Hash(currentPolicy.Decision.ValueJson))
            throw Validation("SUPPLIER_DUE_DILIGENCE_POLICY_STILL_CURRENT",
                "The review is already bound to the current DEC-011 policy and must follow the normal approval path.");

        var before = Snapshot(entity);
        entity.Status = ProcurementSupplierDueDiligenceStatus.Superseded;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "PolicySuperseded", correlation, now);
        Capture(entity);
        await Reviews.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "PolicySuperseded",
            ProcurementControlEventResult.ReviewRequired,
            before, Snapshot(entity), request.Comment, request.Evidence,
            correlation, now, cancellationToken);
        await PublishNotificationAsync(
            "procurement.supplier-due-diligence.policy-superseded",
            entity, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken), now);
    }

    public async Task<int> ProcessExpiryAsync(
        Guid? tenantId = null,
        DateTime? atUtc = null,
        CancellationToken cancellationToken = default)
    {
        Guid targetTenant;
        var system = tenantId.HasValue;
        if (system)
        {
            if (tenantId == Guid.Empty)
                throw Validation("SUPPLIER_DUE_DILIGENCE_TENANT_REQUIRED", "A tenant is required.");
            targetTenant = tenantId!.Value;
        }
        else
        {
            var correlation = $"due-diligence-expiry-{DateTime.UtcNow:yyyyMMddHH}";
            await EnsureCapabilityAsync(ManagePermission, "EXPIRY", correlation, cancellationToken);
            targetTenant = _currentUser.TenantId;
        }
        var now = atUtc.HasValue ? EnsureUtc(atUtc.Value) : DateTime.UtcNow;
        var rows = await Reviews.GetQueryable(item =>
                item.TenantId == targetTenant && !item.IsDeleted &&
                item.Status == ProcurementSupplierDueDiligenceStatus.Approved &&
                (item.ReviewPeriodEndUtc <= now ||
                 item.Checks.Any(check => !check.IsDeleted &&
                     check.Status != ProcurementSupplierDueDiligenceCheckStatus.NotApplicable &&
                     check.ValidUntilUtc.HasValue && check.ValidUntilUtc.Value <= now)))
            .Include(item => item.Checks)
            .ToListAsync(cancellationToken);
        foreach (var entity in rows)
        {
            entity.Status = ProcurementSupplierDueDiligenceStatus.Expired;
            entity.ExpiredAtUtc = now;
            entity.LastOperation = "Expired";
            entity.LastOperationCorrelationId = $"expiry-{entity.Id:N}-{now:yyyyMMddHH}";
            entity.UpdatedAt = now;
            entity.UpdatedBy = system ? "Supplier due-diligence scheduler" : ActorName;
            entity.LastModifiedById = system ? null : _currentUser.UserId;
            Capture(entity);
            await Reviews.UpdateAsync(entity);
        }
        if (rows.Count == 0) return 0;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var entity in rows)
        {
            var correlation = entity.LastOperationCorrelationId;
            if (system)
            {
                await _controlEvents.RecordSystemAsync(targetTenant,
                    "Supplier due-diligence scheduler", BuildEvent(entity, "Expired",
                        ProcurementControlEventResult.Warning, null, Snapshot(entity),
                        "The approved review or one of its checks expired.", [], correlation, now),
                    cancellationToken);
            }
            else
            {
                await _controlEvents.RecordAsync(BuildEvent(entity, "Expired",
                    ProcurementControlEventResult.Warning, null, Snapshot(entity),
                    "The approved review or one of its checks expired.", [], correlation, now),
                    cancellationToken);
            }
            await PublishNotificationAsync("procurement.supplier-due-diligence.expired",
                entity, cancellationToken, system ? null : _currentUser.UserId);
        }
        return rows.Count;
    }

    private IQueryable<ProcurementSupplierDueDiligenceReview> ReviewQuery() =>
        Reviews.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Checks.Where(check => !check.IsDeleted))
                .ThenInclude(check => check.EvidenceLinks.Where(link => !link.IsDeleted));

    private async Task<ProcurementSupplierDueDiligenceReview> LoadAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = ReviewQuery();
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound("SUPPLIER_DUE_DILIGENCE_NOT_FOUND",
                "The due-diligence review was not found in the current tenant.");
    }

    private async Task<ProcurementSupplierDueDiligenceCurrentStateDto> BuildCurrentStateAsync(
        Guid tenantId,
        Guid businessPartnerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var policy = await TryResolvePolicyAsync(now, cancellationToken, tenantId);
        if (policy is null)
            return new ProcurementSupplierDueDiligenceCurrentStateDto
            {
                BusinessPartnerId = businessPartnerId,
                PolicyAvailable = false,
                Code = "SUPPLIER_DUE_DILIGENCE_POLICY_UNAVAILABLE",
                Message = "No unique Published/effective/approved/evidenced DEC-011 policy is available."
            };
        var approved = await Reviews.GetQueryable(item => item.TenantId == tenantId &&
                item.BusinessPartnerId == businessPartnerId && !item.IsDeleted &&
                item.Status == ProcurementSupplierDueDiligenceStatus.Approved)
            .Include(item => item.Checks.Where(check => !check.IsDeleted))
                .ThenInclude(check => check.EvidenceLinks.Where(link => !link.IsDeleted))
            .AsNoTracking().OrderByDescending(item => item.CycleNumber)
            .ToListAsync(cancellationToken);
        if (approved.Count == 0)
            return new ProcurementSupplierDueDiligenceCurrentStateDto
            {
                BusinessPartnerId = businessPartnerId,
                PolicyAvailable = true,
                PolicyDecisionId = policy.Decision.Id,
                PolicyValueHash = Hash(policy.Decision.ValueJson),
                Code = "SUPPLIER_DUE_DILIGENCE_REQUIRED",
                Message = "No approved supplier due-diligence review exists."
            };
        if (approved.Count > 1)
            return new ProcurementSupplierDueDiligenceCurrentStateDto
            {
                BusinessPartnerId = businessPartnerId,
                PolicyAvailable = true,
                PolicyDecisionId = policy.Decision.Id,
                PolicyValueHash = Hash(policy.Decision.ValueJson),
                HasApprovedReview = true,
                Code = "SUPPLIER_DUE_DILIGENCE_AMBIGUOUS",
                Message = "More than one Approved due-diligence review exists."
            };
        var review = approved[0];
        var earliest = review.Checks.Where(item =>
                item.Status != ProcurementSupplierDueDiligenceCheckStatus.NotApplicable &&
                item.ValidUntilUtc.HasValue)
            .Select(item => item.ValidUntilUtc).Min();
        var checksClear = review.Checks.Count == RequiredChecks.Length &&
            review.Checks.All(item =>
                item.Status is ProcurementSupplierDueDiligenceCheckStatus.Clear or
                    ProcurementSupplierDueDiligenceCheckStatus.NotApplicable);
        var datesCurrent = review.ReviewPeriodStartUtc <= now &&
            review.ReviewPeriodEndUtc > now &&
            review.Checks.All(item =>
                item.Status == ProcurementSupplierDueDiligenceCheckStatus.NotApplicable ||
                (item.CheckedAtUtc <= now && item.ValidUntilUtc > now));
        var policyCurrent = review.PolicyDecisionId == policy.Decision.Id &&
            review.PolicyValueHash == Hash(policy.Decision.ValueJson);
        var current = review.Outcome == ProcurementSupplierDueDiligenceOutcome.Clear &&
            checksClear && datesCurrent && policyCurrent;
        return new ProcurementSupplierDueDiligenceCurrentStateDto
        {
            BusinessPartnerId = businessPartnerId,
            PolicyAvailable = true,
            PolicyDecisionId = policy.Decision.Id,
            PolicyValueHash = Hash(policy.Decision.ValueJson),
            HasApprovedReview = true,
            IsCurrent = current,
            IsClear = review.Outcome == ProcurementSupplierDueDiligenceOutcome.Clear && checksClear,
            Code = current ? "SUPPLIER_DUE_DILIGENCE_CURRENT" :
                !policyCurrent ? "SUPPLIER_DUE_DILIGENCE_POLICY_STALE" :
                review.Outcome == ProcurementSupplierDueDiligenceOutcome.Adverse
                    ? "SUPPLIER_DUE_DILIGENCE_ADVERSE"
                    : "SUPPLIER_DUE_DILIGENCE_EXPIRED",
            Message = current
                ? "The supplier has one current approved clear due-diligence review."
                : "The approved due-diligence review is adverse, expired, incomplete, or bound to a replaced policy.",
            ReviewId = review.Id,
            ReviewReference = review.ReviewReference,
            ReviewPeriodEndUtc = review.ReviewPeriodEndUtc,
            EarliestCheckExpiryUtc = earliest,
            IntegrityHash = review.IntegrityHash,
            Checks = review.Checks.OrderBy(item => item.CheckType).Select(MapCheck).ToList()
        };
    }

    private async Task<PolicyResolution?> TryResolvePolicyAsync(
        DateTime now,
        CancellationToken cancellationToken,
        Guid? tenantId = null)
    {
        var targetTenant = tenantId ?? _currentUser.TenantId;
        var matches = await _unitOfWork.Repository<ProcurementConfigurationDecision>()
            .GetQueryable(item => item.TenantId == targetTenant &&
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
        return value is null || value.ReviewFrequencyMonths < 1
            ? null
            : new PolicyResolution(matches[0], value);
    }

    private async Task<PolicyResolution> ResolvePolicyAsync(
        DateTime now,
        CancellationToken cancellationToken) =>
        await TryResolvePolicyAsync(now, cancellationToken)
        ?? throw Validation("SUPPLIER_DUE_DILIGENCE_POLICY_UNAVAILABLE",
            "A unique Published/effective/approved/evidenced DEC-011 supplier-risk policy is required.");

    private async Task<WorkflowDefinition> ValidateWorkflowAsync(
        Guid workflowDefinitionId,
        CancellationToken cancellationToken)
    {
        if (workflowDefinitionId == Guid.Empty)
            throw Validation("SUPPLIER_DUE_DILIGENCE_WORKFLOW_REQUIRED",
                "A Published shared workflow definition is required.");
        return await WorkflowDefinitions.GetQueryable(item =>
                item.Id == workflowDefinitionId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("SUPPLIER_DUE_DILIGENCE_WORKFLOW_INVALID",
                "The selected shared workflow is unavailable, inactive, foreign, or unpublished.");
    }

    private async Task ValidateForDecisionAsync(
        ProcurementSupplierDueDiligenceReview entity,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var policy = await ResolvePolicyAsync(now, cancellationToken);
        if (entity.PolicyDecisionId != policy.Decision.Id ||
            entity.PolicyValueHash != Hash(policy.Decision.ValueJson))
            throw Validation("SUPPLIER_DUE_DILIGENCE_POLICY_STALE",
                "The review is not bound to the exact current DEC-011 policy.");
        if (entity.Checks.Count(item => !item.IsDeleted) != RequiredChecks.Length ||
            RequiredChecks.Any(type => entity.Checks.Count(item =>
                !item.IsDeleted && item.CheckType == type) != 1))
            throw Validation("SUPPLIER_DUE_DILIGENCE_CHECK_SET_INVALID",
                "Exactly one record is required for every due-diligence check type.");
        foreach (var check in entity.Checks.Where(item => !item.IsDeleted))
        {
            if (check.Status == ProcurementSupplierDueDiligenceCheckStatus.Pending)
                throw Validation("SUPPLIER_DUE_DILIGENCE_CHECK_PENDING",
                    $"{CheckName(check.CheckType)} is still pending.");
            if (check.EvidenceLinks.Count(item => !item.IsDeleted) == 0)
                throw Validation("SUPPLIER_DUE_DILIGENCE_CHECK_EVIDENCE_REQUIRED",
                    $"{CheckName(check.CheckType)} requires retained evidence.");
            if (check.Status == ProcurementSupplierDueDiligenceCheckStatus.NotApplicable)
            {
                if (string.IsNullOrWhiteSpace(check.Notes))
                    throw Validation("SUPPLIER_DUE_DILIGENCE_NOT_APPLICABLE_REASON_REQUIRED",
                        $"{CheckName(check.CheckType)} requires a NotApplicable reason.");
                continue;
            }
            if (!check.CheckedAtUtc.HasValue || check.CheckedAtUtc.Value > now)
                throw Validation("SUPPLIER_DUE_DILIGENCE_CHECK_DATE_INVALID",
                    $"{CheckName(check.CheckType)} requires a non-future checked date.");
            if (!check.ValidUntilUtc.HasValue || check.ValidUntilUtc.Value <= now)
                throw Validation("SUPPLIER_DUE_DILIGENCE_CHECK_EXPIRED",
                    $"{CheckName(check.CheckType)} requires a future validity date.");
        }
    }

    private static void ValidateCheckSet(
        IReadOnlyCollection<SaveProcurementSupplierDueDiligenceCheckRequest> checks)
    {
        if (checks.Count != RequiredChecks.Length ||
            RequiredChecks.Any(type => checks.Count(item => item.CheckType == type) != 1))
            throw Validation("SUPPLIER_DUE_DILIGENCE_CHECK_SET_INVALID",
                "Exactly one record is required for every due-diligence check type.");
        if (checks.Any(item => string.IsNullOrWhiteSpace(item.SourceName) ||
                               string.IsNullOrWhiteSpace(item.SourceReference)))
            throw Validation("SUPPLIER_DUE_DILIGENCE_SOURCE_REQUIRED",
                "Every check requires its authoritative source and source reference.");
    }

    private async Task<List<ProcurementSupplierDueDiligenceEvidenceLink>> ResolveEvidenceAsync(
        Guid reviewId,
        Guid checkId,
        ProcurementSupplierDueDiligenceCheckType checkType,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> references,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rows = new List<ProcurementSupplierDueDiligenceEvidenceLink>();
        foreach (var reference in references)
        {
            var row = new ProcurementSupplierDueDiligenceEvidenceLink
            {
                TenantId = _currentUser.TenantId,
                ReviewId = reviewId,
                CheckId = checkId,
                ReferenceKind = reference.ReferenceKind,
                Label = Trim(reference.Label, 300),
                RequirementKey = checkType.ToString(),
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            if (reference.ReferenceKind == ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument)
            {
                if (!reference.ReferenceId.HasValue)
                    throw Validation("SUPPLIER_DUE_DILIGENCE_EVIDENCE_ID_REQUIRED",
                        "Workflow evidence requires ReferenceId.");
                var evidence = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                    .GetQueryable(item => item.Id == reference.ReferenceId.Value &&
                        item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFound("SUPPLIER_DUE_DILIGENCE_EVIDENCE_NOT_FOUND",
                        "The workflow evidence was not found in the current tenant.");
                row.WorkflowEvidenceDocumentId = evidence.Id;
                row.Reference = evidence.AttachmentId;
            }
            else if (reference.ReferenceKind == ProcurementControlEvidenceReferenceKind.FileUploadRecord)
            {
                if (!reference.ReferenceId.HasValue)
                    throw Validation("SUPPLIER_DUE_DILIGENCE_EVIDENCE_ID_REQUIRED",
                        "File evidence requires ReferenceId.");
                var upload = await _unitOfWork.Repository<FileUploadRecord>()
                    .GetQueryable(item => item.Id == reference.ReferenceId.Value &&
                        item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFound("SUPPLIER_DUE_DILIGENCE_EVIDENCE_NOT_FOUND",
                        "The shared file upload was not found in the current tenant.");
                row.FileUploadRecordId = upload.Id;
                row.Reference = upload.Id.ToString("N");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(reference.Reference))
                    throw Validation("SUPPLIER_DUE_DILIGENCE_EVIDENCE_REFERENCE_REQUIRED",
                        "External evidence requires a reference.");
                row.Reference = reference.Reference.Trim();
            }
            row.IntegrityHash = Hash(Serialize(new
            {
                row.TenantId,
                row.ReviewId,
                row.CheckId,
                row.ReferenceKind,
                row.WorkflowEvidenceDocumentId,
                row.FileUploadRecordId,
                row.Reference,
                row.Label,
                row.RequirementKey
            }));
            rows.Add(row);
        }
        if (rows.GroupBy(item => new { item.ReferenceKind, item.Reference })
            .Any(group => group.Count() > 1))
            throw Validation("SUPPLIER_DUE_DILIGENCE_EVIDENCE_DUPLICATE",
                "Evidence references must be unique within each check.");
        return rows;
    }

    private async Task EnsureWorkflowOutcomeAsync(
        ProcurementSupplierDueDiligenceReview entity,
        bool approving,
        CancellationToken cancellationToken)
    {
        if (!entity.WorkflowInstanceId.HasValue)
            throw Conflict("SUPPLIER_DUE_DILIGENCE_WORKFLOW_INSTANCE_REQUIRED",
                "The shared workflow instance was not recorded.");
        var instance = await WorkflowInstances.GetQueryable(item =>
                item.TenantId == entity.TenantId &&
                item.Id == entity.WorkflowInstanceId.Value && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Conflict("SUPPLIER_DUE_DILIGENCE_WORKFLOW_INSTANCE_NOT_FOUND",
                "The shared workflow instance is unavailable.");
        if (instance.WorkflowDefinitionId != entity.WorkflowDefinitionId ||
            instance.EntityId != entity.Id)
            throw Conflict("SUPPLIER_DUE_DILIGENCE_WORKFLOW_BINDING_INVALID",
                "The shared workflow instance does not belong to this review.");
        if (approving && instance.Status != WorkflowInstanceStatus.Completed)
            throw Conflict("SUPPLIER_DUE_DILIGENCE_WORKFLOW_NOT_APPROVED",
                "Only a Completed shared workflow permits approval.");
        if (!approving &&
            instance.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            throw Conflict("SUPPLIER_DUE_DILIGENCE_WORKFLOW_NOT_REJECTED",
                "Only a Cancelled or Failed shared workflow permits rejection.");
    }

    private async Task EnsureIndependentActorAsync(
        Guid? prohibitedActor,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!prohibitedActor.HasValue || prohibitedActor == Guid.Empty)
            throw Conflict("SUPPLIER_DUE_DILIGENCE_INITIATOR_NOT_RECORDED",
                "The submitting actor was not recorded.");
        var decision = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = SourceType,
            SourceReference = sourceReference,
            ProhibitedActorUserIds = [prohibitedActor.Value]
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierDueDiligenceAuthorizationException(decision.Message);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierDueDiligenceAuthorizationException(
                "Supplier portal users cannot administer due-diligence reviews.");
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            SourceType = SourceType,
            SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? "NEW" : sourceReference
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierDueDiligenceAuthorizationException(decision.Message);
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierDueDiligenceAuthorizationException(
                "Supplier portal users cannot access due-diligence administration.");
        if (HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read"))
            return;
        throw new ProcurementSupplierDueDiligenceAuthorizationException(
            "The procurement records read permission is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw new ProcurementSupplierDueDiligenceAuthorizationException(
                "An authenticated tenant context is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() =>
        _currentUser.HasRole(Constants.Roles.SuperAdmin);

    private async Task RecordEventAsync(
        ProcurementSupplierDueDiligenceReview entity,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(BuildEvent(entity, action, result, before, after,
            reason, evidence, correlationId, occurredAtUtc), cancellationToken);

    private static ProcurementControlEventWriteRequest BuildEvent(
        ProcurementSupplierDueDiligenceReview entity,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc) => new()
    {
        EventKey = ProcurementControlEventKey.Create(
            "supplier-due-diligence", entity.TenantId, entity.Id,
            $"{action}-{correlationId}"),
        EventType = EventType,
        Action = action,
        Result = result,
        RuleCode = entity.Outcome.ToString(),
        RuleId = entity.PolicyDecisionId,
        RuleVersion = $"{entity.PolicyProfileCode}/v{entity.PolicyProfileVersion}",
        DecisionKeys = DecisionKeys.ToList(),
        SourceType = SourceType,
        SourceId = entity.Id,
        SourceReference = entity.ReviewReference,
        Reason = Trim(reason, 1000),
        InputValues = new
        {
            entity.BusinessPartnerId,
            entity.ReviewType,
            entity.CycleNumber,
            entity.PolicyDecisionId,
            entity.WorkflowDefinitionId
        },
        ResultValues = new
        {
            entity.Status,
            entity.Outcome,
            entity.ReviewPeriodStartUtc,
            entity.ReviewPeriodEndUtc,
            entity.PolicyValueHash,
            entity.IntegrityHash,
            Checks = entity.Checks.Where(item => !item.IsDeleted)
                .OrderBy(item => item.CheckType)
                .Select(item => new
                {
                    item.CheckType,
                    item.Status,
                    item.CheckedAtUtc,
                    item.ValidUntilUtc,
                    item.IntegrityHash
                })
        },
        Before = before,
        After = after,
        CorrelationId = correlationId,
        CausationId = correlationId,
        OccurredAtUtc = occurredAtUtc,
        Evidence = evidence.Select(item => new ProcurementControlEventEvidenceReference
        {
            ReferenceKind = item.ReferenceKind,
            ReferenceId = item.ReferenceId,
            Reference = item.Reference,
            Label = item.Label,
            RequirementKey = item.RequirementKey
        }).ToList()
    };

    private async Task PublishNotificationAsync(
        string topic,
        ProcurementSupplierDueDiligenceReview entity,
        CancellationToken cancellationToken,
        Guid? actorId = null)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = entity.TenantId,
                TopicKey = topic,
                NotificationType = "ProcurementSupplierDueDiligenceControl",
                EntityType = SourceType,
                EntityId = entity.Id,
                TriggeredByUserId = actorId ?? (_currentUser.UserId == Guid.Empty
                    ? entity.CreatedById ?? Guid.Empty
                    : _currentUser.UserId),
                Data = new Dictionary<string, object>
                {
                    ["businessPartnerId"] = entity.BusinessPartnerId,
                    ["reviewReference"] = entity.ReviewReference,
                    ["cycleNumber"] = entity.CycleNumber,
                    ["status"] = entity.Status.ToString(),
                    ["outcome"] = entity.Outcome.ToString(),
                    ["nextReviewDueAtUtc"] = entity.NextReviewDueAtUtc ?? entity.ReviewPeriodEndUtc
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish supplier due-diligence notification {Topic} for {ReviewId}",
                topic, entity.Id);
        }
    }

    private static ProcurementSupplierDueDiligenceListItemDto MapList(
        ProcurementSupplierDueDiligenceReview item,
        DateTime now)
    {
        var checks = item.Checks.Where(check => !check.IsDeleted).ToList();
        var current = item.Status == ProcurementSupplierDueDiligenceStatus.Approved &&
            item.Outcome == ProcurementSupplierDueDiligenceOutcome.Clear &&
            item.ReviewPeriodStartUtc <= now && item.ReviewPeriodEndUtc > now &&
            checks.Count == RequiredChecks.Length &&
            checks.All(check =>
                check.Status == ProcurementSupplierDueDiligenceCheckStatus.NotApplicable ||
                (check.Status == ProcurementSupplierDueDiligenceCheckStatus.Clear &&
                 check.CheckedAtUtc <= now && check.ValidUntilUtc > now));
        return new ProcurementSupplierDueDiligenceListItemDto
        {
            Id = item.Id,
            BusinessPartnerId = item.BusinessPartnerId,
            PartnerCode = item.BusinessPartner?.PartnerCode ?? string.Empty,
            PartnerName = item.BusinessPartner?.PartnerName ?? string.Empty,
            ReviewReference = item.ReviewReference,
            CycleNumber = item.CycleNumber,
            ReviewType = item.ReviewType,
            Status = item.Status,
            Outcome = item.Outcome,
            ReviewPeriodStartUtc = item.ReviewPeriodStartUtc,
            ReviewPeriodEndUtc = item.ReviewPeriodEndUtc,
            NextReviewDueAtUtc = item.NextReviewDueAtUtc,
            IsCurrent = current,
            IsDueOrExpired = item.Status == ProcurementSupplierDueDiligenceStatus.Expired ||
                (item.Status == ProcurementSupplierDueDiligenceStatus.Approved &&
                 item.ReviewPeriodEndUtc <= now),
            PolicyProfileCode = item.PolicyProfileCode,
            PolicyProfileVersion = item.PolicyProfileVersion,
            ReviewFrequencyMonths = item.ReviewFrequencyMonths,
            ClearCheckCount = checks.Count(check =>
                check.Status == ProcurementSupplierDueDiligenceCheckStatus.Clear),
            AdverseCheckCount = checks.Count(check =>
                check.Status == ProcurementSupplierDueDiligenceCheckStatus.Adverse),
            EvidenceCount = checks.Sum(check =>
                check.EvidenceLinks.Count(link => !link.IsDeleted)),
            AllowedActions = AllowedActions(item.Status),
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    private static ProcurementSupplierDueDiligenceDto Map(
        ProcurementSupplierDueDiligenceReview item,
        DateTime now)
    {
        var list = MapList(item, now);
        return new ProcurementSupplierDueDiligenceDto
        {
            Id = list.Id,
            BusinessPartnerId = list.BusinessPartnerId,
            PartnerCode = list.PartnerCode,
            PartnerName = list.PartnerName,
            ReviewReference = list.ReviewReference,
            CycleNumber = list.CycleNumber,
            ReviewType = list.ReviewType,
            Status = list.Status,
            Outcome = list.Outcome,
            ReviewPeriodStartUtc = list.ReviewPeriodStartUtc,
            ReviewPeriodEndUtc = list.ReviewPeriodEndUtc,
            NextReviewDueAtUtc = list.NextReviewDueAtUtc,
            IsCurrent = list.IsCurrent,
            IsDueOrExpired = list.IsDueOrExpired,
            PolicyProfileCode = list.PolicyProfileCode,
            PolicyProfileVersion = list.PolicyProfileVersion,
            ReviewFrequencyMonths = list.ReviewFrequencyMonths,
            ClearCheckCount = list.ClearCheckCount,
            AdverseCheckCount = list.AdverseCheckCount,
            EvidenceCount = list.EvidenceCount,
            AllowedActions = list.AllowedActions,
            RowVersion = list.RowVersion,
            PolicyDecisionId = item.PolicyDecisionId,
            PolicyProfileId = item.PolicyProfileId,
            PolicyValueHash = item.PolicyValueHash,
            PolicySnapshotJson = item.PolicySnapshotJson,
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowInstanceId = item.WorkflowInstanceId,
            SupersedesReviewId = item.SupersedesReviewId,
            SupersededByReviewId = item.SupersededByReviewId,
            Notes = item.Notes,
            ReviewComment = item.ReviewComment,
            SubmittedById = item.SubmittedById,
            SubmittedAtUtc = item.SubmittedAtUtc,
            ApprovedById = item.ApprovedById,
            ApprovedAtUtc = item.ApprovedAtUtc,
            RejectedById = item.RejectedById,
            RejectedAtUtc = item.RejectedAtUtc,
            ExpiredAtUtc = item.ExpiredAtUtc,
            IntegrityHash = item.IntegrityHash,
            Checks = item.Checks.Where(check => !check.IsDeleted)
                .OrderBy(check => check.CheckType).Select(MapCheck).ToList()
        };
    }

    private static ProcurementSupplierDueDiligenceCheckDto MapCheck(
        ProcurementSupplierDueDiligenceCheck item) => new()
    {
        Id = item.Id,
        CheckType = item.CheckType,
        Status = item.Status,
        SourceName = item.SourceName,
        SourceReference = item.SourceReference,
        CheckedAtUtc = item.CheckedAtUtc,
        ValidUntilUtc = item.ValidUntilUtc,
        ReviewedById = item.ReviewedById,
        ReviewerName = item.ReviewerName,
        Notes = item.Notes,
        IntegrityHash = item.IntegrityHash,
        Evidence = item.EvidenceLinks.Where(link => !link.IsDeleted)
            .OrderBy(link => link.ReferenceKind).ThenBy(link => link.Reference)
            .Select(link => new ProcurementSupplierDueDiligenceEvidenceDto
            {
                Id = link.Id,
                ReferenceKind = link.ReferenceKind,
                WorkflowEvidenceDocumentId = link.WorkflowEvidenceDocumentId,
                FileUploadRecordId = link.FileUploadRecordId,
                Reference = link.Reference,
                Label = link.Label,
                RequirementKey = link.RequirementKey,
                IntegrityHash = link.IntegrityHash
            }).ToList()
    };

    private static IReadOnlyList<string> AllowedActions(
        ProcurementSupplierDueDiligenceStatus status) => status switch
    {
        ProcurementSupplierDueDiligenceStatus.Draft => ["Edit", "Submit"],
        ProcurementSupplierDueDiligenceStatus.PendingApproval => ["Approve", "Reject"],
        ProcurementSupplierDueDiligenceStatus.Approved => ["CreateAnnualReassessment"],
        ProcurementSupplierDueDiligenceStatus.Expired => ["CreateAnnualReassessment"],
        ProcurementSupplierDueDiligenceStatus.Superseded => ["View"],
        ProcurementSupplierDueDiligenceStatus.Rejected => ["View"],
        _ => ["View"]
    };

    private static object Snapshot(ProcurementSupplierDueDiligenceReview item) => new
    {
        item.Id,
        item.TenantId,
        item.BusinessPartnerId,
        item.ReviewReference,
        item.CycleNumber,
        item.ReviewType,
        item.Status,
        item.Outcome,
        item.ReviewPeriodStartUtc,
        item.ReviewPeriodEndUtc,
        item.NextReviewDueAtUtc,
        item.PolicyDecisionId,
        item.PolicyProfileId,
        item.PolicyProfileCode,
        item.PolicyProfileVersion,
        item.ReviewFrequencyMonths,
        item.PolicyValueHash,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.SupersedesReviewId,
        item.SupersededByReviewId,
        item.SubmittedById,
        item.SubmittedAtUtc,
        item.ApprovedById,
        item.ApprovedAtUtc,
        item.RejectedById,
        item.RejectedAtUtc,
        item.ExpiredAtUtc,
        Checks = item.Checks.Where(check => !check.IsDeleted)
            .OrderBy(check => check.CheckType)
            .Select(check => new
            {
                check.Id,
                check.CheckType,
                check.Status,
                check.SourceName,
                check.SourceReference,
                check.CheckedAtUtc,
                check.ValidUntilUtc,
                check.ReviewedById,
                check.ReviewerName,
                check.Notes,
                check.IntegrityHash,
                Evidence = check.EvidenceLinks.Where(link => !link.IsDeleted)
                    .OrderBy(link => link.ReferenceKind).ThenBy(link => link.Reference)
                    .Select(link => new
                    {
                        link.ReferenceKind,
                        link.WorkflowEvidenceDocumentId,
                        link.FileUploadRecordId,
                        link.Reference,
                        link.Label,
                        link.RequirementKey,
                        link.IntegrityHash
                    })
            })
    };

    private static void Capture(ProcurementSupplierDueDiligenceReview item)
    {
        item.SnapshotJson = Serialize(Snapshot(item));
        item.IntegrityHash = Hash(item.SnapshotJson);
    }

    private static void Capture(ProcurementSupplierDueDiligenceCheck item)
    {
        item.SnapshotJson = Serialize(new
        {
            item.Id,
            item.TenantId,
            item.ReviewId,
            item.CheckType,
            item.Status,
            item.SourceName,
            item.SourceReference,
            item.CheckedAtUtc,
            item.ValidUntilUtc,
            item.ReviewedById,
            item.ReviewerName,
            item.Notes,
            Evidence = item.EvidenceLinks.Where(link => !link.IsDeleted)
                .OrderBy(link => link.ReferenceKind).ThenBy(link => link.Reference)
                .Select(link => new
                {
                    link.ReferenceKind,
                    link.WorkflowEvidenceDocumentId,
                    link.FileUploadRecordId,
                    link.Reference,
                    link.Label,
                    link.RequirementKey,
                    link.IntegrityHash
                })
        });
        item.IntegrityHash = Hash(item.SnapshotJson);
    }

    private static void Touch(
        ProcurementSupplierDueDiligenceReview item,
        string operation,
        string correlation,
        DateTime now)
    {
        item.LastOperation = operation;
        item.LastOperationCorrelationId = correlation;
        item.UpdatedAt = now;
    }

    private static bool IsReplay(
        ProcurementSupplierDueDiligenceReview item,
        string operation,
        string correlation) =>
        string.Equals(item.LastOperation, operation, StringComparison.Ordinal) &&
        string.Equals(item.LastOperationCorrelationId, correlation, StringComparison.Ordinal);

    private static void EnsureStatus(
        ProcurementSupplierDueDiligenceReview item,
        ProcurementSupplierDueDiligenceStatus status,
        string message)
    {
        if (item.Status != status)
            throw Conflict("SUPPLIER_DUE_DILIGENCE_STATUS_INVALID", message);
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException)
        {
            throw Conflict("SUPPLIER_DUE_DILIGENCE_VERSION_INVALID",
                "RowVersion must be valid Base64.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict("SUPPLIER_DUE_DILIGENCE_VERSION_CONFLICT",
                "The review changed. Reload before continuing.");
    }

    private static void EnsureLifecycleEvidence(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence)
    {
        if (evidence.Count == 0 || evidence.Any(item =>
                !item.ReferenceId.HasValue && string.IsNullOrWhiteSpace(item.Reference)))
            throw Validation("SUPPLIER_DUE_DILIGENCE_LIFECYCLE_EVIDENCE_REQUIRED",
                "Submission and decisions require a shared evidence reference.");
    }

    private string ActorName => string.IsNullOrWhiteSpace(_currentUser.FullName)
        ? _currentUser.Username
        : _currentUser.FullName;

    private static string CheckName(ProcurementSupplierDueDiligenceCheckType type) =>
        type switch
        {
            ProcurementSupplierDueDiligenceCheckType.PpaDebarment => "PPA debarment",
            ProcurementSupplierDueDiligenceCheckType.GraTaxClearance => "GRA tax clearance",
            ProcurementSupplierDueDiligenceCheckType.Sanctions => "Sanctions screening",
            ProcurementSupplierDueDiligenceCheckType.BankVerification => "Bank verification",
            ProcurementSupplierDueDiligenceCheckType.FinancialStability => "Financial stability",
            ProcurementSupplierDueDiligenceCheckType.Reputation => "Reputation review",
            _ => type.ToString()
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

    private static string NormalizeCorrelation(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw Validation("SUPPLIER_DUE_DILIGENCE_CORRELATION_REQUIRED",
                "X-Correlation-ID is required.");
        return correlationId.Trim().Length <= 100
            ? correlationId.Trim()
            : Hash(correlationId);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null :
        value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];

    private static ProcurementSupplierDueDiligenceNotFoundException NotFound(
        string code,
        string message) => new(code, message);
    private static ProcurementSupplierDueDiligenceValidationException Validation(
        string code,
        string message) => new(code, message);
    private static ProcurementSupplierDueDiligenceConflictException Conflict(
        string code,
        string message) => new(code, message);

    private sealed record PolicyResolution(
        ProcurementConfigurationDecision Decision,
        ProcurementSupplierRiskDecisionValueDto Value);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
