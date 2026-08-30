using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 1: CORE TRAVEL REQUEST SERVICE
// ============================================================================

#region Staff Travel Request Service

public class StaffTravelRequestService : IStaffTravelRequestService
{
    private readonly IStaffTravelRequestRepository _requestRepository;
    private readonly IStaffTravelRequestCommentRepository _commentRepository;
    private readonly IStaffTravelRequestAttachmentRepository _attachmentRepository;
    private readonly IStaffGroupTravelRepository _groupTravelRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly StaffTravelCurrencyBridge _currency;
    private readonly ILogger<StaffTravelRequestService> _logger;

    public StaffTravelRequestService(
        IStaffTravelRequestRepository requestRepository,
        IStaffTravelRequestCommentRepository commentRepository,
        IStaffTravelRequestAttachmentRepository attachmentRepository,
        IStaffGroupTravelRepository groupTravelRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        StaffTravelCurrencyBridge currency,
        ILogger<StaffTravelRequestService> logger)
    {
        _requestRepository = requestRepository;
        _commentRepository = commentRepository;
        _attachmentRepository = attachmentRepository;
        _groupTravelRepository = groupTravelRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currency = currency;
        _logger = logger;
    }

    /// <summary>
    /// The workflow entity type. Approval authority comes from the published definition, not from a
    /// role attribute — which is the point of being on the engine: a travel approver is usually the
    /// traveller's line manager or head of department, not HR.
    /// </summary>
    private const string EntityType = "StaffTravelRequest";

    /// <summary>
    /// The engine identifies approvers by ApplicationUser id, not Employee id. The entity's own
    /// actor columns (CancelledById) are Employee FKs and are set separately — see the actor split
    /// that slice 1 had to untangle.
    /// </summary>
    private Guid RequireUserId()
    {
        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("No user is associated with the current request.");
        return userId;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffTravelRequest> GetOwnedRequestAsync(Guid id)
    {
        var entity = await _requestRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff travel request with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelRequestComment> GetOwnedCommentAsync(Guid id)
    {
        var entity = await _commentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Comment with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelRequestAttachment> GetOwnedAttachmentAsync(Guid id)
    {
        var entity = await _attachmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Attachment with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffGroupTravel> GetOwnedGroupTravelAsync(Guid id)
    {
        var entity = await _groupTravelRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Group travel with ID '{id}' not found.");
        return entity;
    }

    // ---- Lifecycle notifications -------------------------------------------

    private const string TopicEntityType = "StaffTravelRequest";
    private const string TopicAudience = "Internal";

    private sealed record TopicSeed(string Activity, string Name, string Description,
        string TitleTemplate, string BodyTemplate);

    /// <remarks>
    /// The templates carry the request number, route and dates — never the purpose. A travel
    /// notification reaches more people than the request does, and the purpose is often the
    /// commercially sensitive part ("client meeting, Acme, renegotiation"). Whoever is entitled to
    /// the detail can open the record.
    /// </remarks>
    private static readonly TopicSeed[] TopicSeeds =
    {
        new("Submitted", "Travel: Submitted for approval",
            "System-seeded — a travel request has been submitted and is awaiting approval.",
            "Travel request {{Reference}} submitted",
            "{{Traveller}} — {{Route}}, {{Dates}}. Awaiting approval."),
        new("Approved", "Travel: Approved",
            "System-seeded — a travel request has been approved.",
            "Travel request {{Reference}} approved",
            "{{Traveller}} — {{Route}}, {{Dates}}. Approved."),
        new("Rejected", "Travel: Rejected",
            "System-seeded — a travel request has been rejected.",
            "Travel request {{Reference}} rejected",
            "{{Traveller}} — {{Route}}, {{Dates}}. Rejected."),
        new("Cancelled", "Travel: Cancelled",
            "System-seeded — a travel request has been withdrawn or cancelled.",
            "Travel request {{Reference}} cancelled",
            "{{Traveller}} — {{Route}}, {{Dates}}. Cancelled."),
        new("Completed", "Travel: Completed",
            "System-seeded — travel has been marked completed; expense claims may now be settled.",
            "Travel request {{Reference}} completed",
            "{{Traveller}} — {{Route}}, {{Dates}}. Completed."),
    };

    /// <summary>
    /// Creates this area's notification topics for a tenant if they do not exist yet.
    /// </summary>
    /// <remarks>
    /// Publishing to a topic that was never seeded delivers to nobody while every table says the
    /// event fired — which is exactly the defect recorded as F-09 for travel alerts. Seed first,
    /// then publish.
    /// </remarks>
    private async Task EnsureTopicsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var keys = TopicSeeds.Select(s => $"{TopicEntityType}.{s.Activity}.{TopicAudience}").ToArray();

        var existing = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && keys.Contains(t.Key))
            .Select(t => t.Key)
            .ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        if (existingSet.Count == TopicSeeds.Length) return;

        var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
        foreach (var seed in TopicSeeds)
        {
            var key = $"{TopicEntityType}.{seed.Activity}.{TopicAudience}";
            if (existingSet.Contains(key)) continue;

            var topic = new NotificationTopic
            {
                TenantId = tenantId,
                Key = key,
                Name = seed.Name,
                Description = seed.Description,
                EntityType = TopicEntityType,
                IsSystem = true,
                IsActive = true,
                EnableInApp = true,
                EnableEmail = false,
                EnableSms = false,
                InAppTitleTemplate = seed.TitleTemplate,
                InAppBodyTemplate = seed.BodyTemplate,
                ActionUrlTemplate = "{{ActionPath}}",
                CreatedBy = "System",
            };
            await topicRepo.AddAsync(topic);

            await recipientRepo.AddAsync(new NotificationTopicRecipient
            {
                TenantId = tenantId,
                TopicId = topic.Id,
                RecipientKind = "Role",
                RecipientValue = Constants.Roles.Hr,
                IsSystem = true,
                SendInApp = true,
                CreatedBy = "System",
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Announces a lifecycle transition. Call only AFTER the transition has committed — an event
    /// published for a save that then fails is a notification about something that did not happen.
    /// </summary>
    private async Task PublishLifecycleAsync(
        StaffTravelRequest entity, string activity, CancellationToken cancellationToken)
    {
        await EnsureTopicsAsync(entity.TenantId, cancellationToken);

        // The transitions reach here holding an entity loaded by GetOwnedRequestAsync, which
        // applies no includes — so Employee is null and the traveller's name would be blank. That
        // is F-13 by another route, and a notification reading "Traveller: " is worse than most
        // blank fields because nobody sees the record it came from. Resolve the name here.
        var traveller = entity.Employee is not null
            ? $"{entity.Employee.FirstName} {entity.Employee.LastName}".Trim()
            : await _requestRepository.GetQueryable()
                .Where(r => r.Id == entity.Id)
                .Select(r => (r.Employee.FirstName + " " + r.Employee.LastName).Trim())
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        await _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = entity.TenantId,
            EntityType = TopicEntityType,
            Activity = activity,
            Audience = TopicAudience,
            EntityId = entity.Id,
            TriggeredByUserId = _currentUserProvider.UserId,
            Data = new Dictionary<string, object>
            {
                ["Reference"] = entity.RequestNumber ?? string.Empty,
                ["Traveller"] = traveller,
                ["Route"] = $"{entity.OriginCity} to {entity.DestinationCity}",
                ["Dates"] = $"{entity.TravelStartDate:yyyy-MM-dd} to {entity.TravelEndDate:yyyy-MM-dd}",
                // Area 25 slice 7: /hr/travel/requests/{id} never existed as a route — the desk
                // detail (these topics' recipients are the HR role) lives at /hr/travel/{id}.
                ["ActionPath"] = $"/hr/travel/{entity.Id}",
            },
        }, cancellationToken);
    }

    // ---- Queries -----------------------------------------------------------

    public async Task<StaffTravelRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _requestRepository.GetWithFullDetailsAsync(tenantId, id);
        if (entity == null)
            throw new ArgumentException($"Staff travel request with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffTravelRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _requestRepository.GetByRequestNumberAsync(requestNumber);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _requestRepository.GetQueryable()
                .Where(r => r.TenantId == tenantId && !r.IsDeleted)
                .Include(r => r.Employee)
                .Include(r => r.DestinationCountry)
                .ToListAsync(cancellationToken))
            .ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffTravelRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _requestRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        // Includes go on the page, not on `query` — `query` is also used for the count, and
        // counting through joins is wasted work. The summary DTO resolves both of these names.
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffTravelRequestSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _requestRepository.GetByEmployeeIdAsync(employeeId))
            .Where(r => r.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByStatusAsync(StaffTravelRequestStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _requestRepository.GetByStatusAsync(status))
            .Where(r => r.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByDateRangeAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _requestRepository.GetByDateRangeAsync(start, end))
            .Where(r => r.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _requestRepository.GetByOrganizationUnitAsync(organizationUnitId))
            .Where(r => r.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _requestRepository.GetPendingApprovalAsync())
            .Where(r => r.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetUpcomingTripsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _requestRepository.GetUpcomingTripsAsync(daysAhead))
            .Where(r => r.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetChildRequestsAsync(Guid parentRequestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _requestRepository.GetChildRequestsAsync(parentRequestId))
            .Where(r => r.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    // ---- Dashboard ---------------------------------------------------------

    public async Task<StaffTravelDashboardDto> GetDashboardAsync(int upcomingDays = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Lightweight scalar projection of every request for in-memory aggregation.
        var rows = await _requestRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId)
            .Select(r => new DashboardRow
            {
                Status = r.Status,
                TravelType = r.TravelType,
                RiskLevel = r.RiskLevel,
                IsInternational = r.IsInternational,
                EstimatedTotalCost = r.EstimatedTotalCost,
                CurrencyCode = r.CurrencyCode,
                ApprovedBudget = r.ApprovedBudget,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        bool IsActive(StaffTravelRequestStatus s) =>
            s != StaffTravelRequestStatus.Cancelled && s != StaffTravelRequestStatus.Rejected;

        var dto = new StaffTravelDashboardDto
        {
            TotalRequests        = rows.Count,
            DraftCount           = rows.Count(r => r.Status == StaffTravelRequestStatus.Draft),
            PendingApprovalCount = rows.Count(r => r.Status == StaffTravelRequestStatus.Submitted),
            ApprovedCount        = rows.Count(r => r.Status == StaffTravelRequestStatus.Approved),
            InProgressCount      = rows.Count(r => r.Status == StaffTravelRequestStatus.InProgress),
            CompletedCount       = rows.Count(r => r.Status == StaffTravelRequestStatus.Completed),
            RejectedCount        = rows.Count(r => r.Status == StaffTravelRequestStatus.Rejected),
            CancelledCount       = rows.Count(r => r.Status == StaffTravelRequestStatus.Cancelled),
            InternationalCount   = rows.Count(r => r.IsInternational),
            DomesticCount        = rows.Count(r => !r.IsInternational),
            HighRiskCount        = rows.Count(r => r.RiskLevel == TravelRiskLevel.High
                                               || r.RiskLevel == TravelRiskLevel.Critical
                                               || r.RiskLevel == TravelRiskLevel.Prohibited),
            TotalEstimatedCost   = rows.Where(r => IsActive(r.Status)).Sum(r => r.EstimatedTotalCost),
            TotalApprovedBudget  = rows.Where(r => IsActive(r.Status)).Sum(r => r.ApprovedBudget ?? 0m),
        };

        // ⚠ Split by currency because the two scalar totals above add them together. A tenant that
        // costs one trip in GHS and another in USD gets a headline "total" of neither. Not converted
        // here: travel does not invent a rate, and Finance's conversion is currently inverted, so a
        // converted headline would be confidently wrong rather than visibly incomplete.
        dto.CostByCurrency = rows
            .Where(r => IsActive(r.Status))
            .GroupBy(r => string.IsNullOrWhiteSpace(r.CurrencyCode) ? "—" : r.CurrencyCode)
            .Select(g => new StaffTravelCurrencyTotalDto
            {
                CurrencyCode = g.Key,
                EstimatedTotal = g.Sum(r => r.EstimatedTotalCost),
                ApprovedBudget = g.Sum(r => r.ApprovedBudget ?? 0m),
                RequestCount = g.Count(),
            })
            .OrderByDescending(c => c.EstimatedTotal)
            .ToList();

        dto.ByStatus = rows
            .GroupBy(r => r.Status)
            .Select(g => new StaffTravelStatusCountDto { Status = g.Key, Count = g.Count() })
            .OrderByDescending(s => s.Count)
            .ToList();

        dto.ByTravelType = rows
            .GroupBy(r => r.TravelType)
            .Select(g => new StaffTravelTypeCountDto { TravelType = g.Key, Count = g.Count() })
            .OrderByDescending(t => t.Count)
            .ToList();

        // Monthly trend over the trailing six calendar months (by creation date).
        var anchor = DateTime.UtcNow;
        for (var i = 5; i >= 0; i--)
        {
            var month = anchor.AddMonths(-i);
            dto.MonthlyTrend.Add(new StaffTravelMonthlyCountDto
            {
                Year = month.Year,
                Month = month.Month,
                Label = new DateTime(month.Year, month.Month, 1).ToString("MMM yyyy"),
                Count = rows.Count(r => r.CreatedAt.Year == month.Year && r.CreatedAt.Month == month.Month),
            });
        }

        // Spotlight lists (lightweight navigation already loaded by these queries).
        var upcoming = (await _requestRepository.GetUpcomingTripsAsync(upcomingDays))
            .Where(r => r.TenantId == tenantId)
            .ToList();
        dto.UpcomingTripCount = upcoming.Count;
        dto.UpcomingTrips = upcoming.Take(5).ToSummaryDtoList().ToList();

        dto.PendingApprovals = (await _requestRepository.GetPendingApprovalAsync())
            .Where(r => r.TenantId == tenantId)
            .Take(5).ToSummaryDtoList().ToList();

        var recent = await _requestRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId)
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);
        dto.RecentRequests = recent.ToSummaryDtoList().ToList();

        return dto;
    }

    private sealed class DashboardRow
    {
        public StaffTravelRequestStatus Status { get; set; }
        public StaffTravelType TravelType { get; set; }
        public TravelRiskLevel RiskLevel { get; set; }
        public bool IsInternational { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal? ApprovedBudget { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ---- CRUD --------------------------------------------------------------

    public async Task<StaffTravelRequestDto> CreateAsync(CreateStaffTravelRequestDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.RequestNumber = await GenerateRequestNumberAsync(tenantId, cancellationToken);
        entity.Status = StaffTravelRequestStatus.Draft;

        await _requestRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request created: {RequestNumber}", entity.RequestNumber);

        var refreshed = await _requestRepository.GetWithFullDetailsAsync(tenantId, entity.Id);
        if (refreshed == null)
            throw new ArgumentException($"Staff travel request with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<StaffTravelRequestDto> UpdateAsync(UpdateStaffTravelRequestDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedRequestAsync(updateDto.Id);

        if (entity.Status is StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException($"A request in status '{entity.Status}' cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request updated: {RequestNumber}", entity.RequestNumber);

        var refreshed = await _requestRepository.GetWithFullDetailsAsync(tenantId, entity.Id);
        if (refreshed == null)
            throw new ArgumentException($"Staff travel request with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(id);

        if (entity.Status != StaffTravelRequestStatus.Draft)
            throw new InvalidOperationException("Only draft requests can be deleted. Cancel the request instead.");

        await _requestRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request deleted: {RequestNumber}", entity.RequestNumber);

        return true;
    }

    // ---- Workflow ----------------------------------------------------------

    public async Task<bool> SubmitAsync(SubmitStaffTravelRequestDto submitDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(submitDto.RequestId);

        if (entity.Status is not (StaffTravelRequestStatus.Draft or StaffTravelRequestStatus.ReturnedForRevision))
            throw new InvalidOperationException("Only draft or returned requests can be submitted.");

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, entity.Id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "Failed to start the travel approval workflow.");

        // The adapter owns the status. A definition with one Approval step approves on submission
        // (the documented single-step trap), so this can legitimately come back Approved.
        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, workflowResult.Outcome, RequireUserId());

        entity.SubmittedAt = submitDto.SubmittedAt;
        entity.UpdatedBy = RequireUserId().ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        if (entity.Status == StaffTravelRequestStatus.Approved)
            entity.ApprovedAt ??= DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request submitted: {RequestNumber}", entity.RequestNumber);

        // Announce what actually happened, not what was asked for: a single-step definition
        // approves on submission, and a notification saying "awaiting approval" about a request
        // that is already approved is worse than none.
        await PublishLifecycleAsync(entity,
            entity.Status == StaffTravelRequestStatus.Approved ? "Approved" : "Submitted",
            cancellationToken);

        return true;
    }

    public async Task<bool> ApproveAsync(ApproveStaffTravelRequestDto approveDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(approveDto.RequestId);

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted requests can be approved.");

        var userId = RequireUserId();

        // The engine decides who may approve, against the published definition. The bespoke chain
        // this replaces took the approver from the request body, so a caller could record a
        // decision in someone else's name.
        if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, userId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            EntityType, entity.Id, userId, "Approve", approveDto.Notes);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the approval.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, workflowResult.Outcome, userId);

        // The approved budget is the domain's own decision, not the engine's, so it is applied only
        // once the engine says the request is actually approved.
        if (entity.Status == StaffTravelRequestStatus.Approved)
            entity.ApprovedBudget = approveDto.ApprovedBudget ?? entity.EstimatedTotalCost;

        entity.UpdatedBy = userId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel approval step processed: {RequestNumber}", entity.RequestNumber);

        // A multi-step definition leaves the request Submitted after an intermediate approval, so
        // only announce approval when the engine says it is approved.
        if (entity.Status == StaffTravelRequestStatus.Approved)
            await PublishLifecycleAsync(entity, "Approved", cancellationToken);

        return true;
    }

    public async Task<bool> RejectAsync(Guid requestId, Guid rejectedByUserId, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted requests can be rejected.");

        var userId = RequireUserId();

        if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, userId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            EntityType, entity.Id, userId, "Reject", reason);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the rejection.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, workflowResult.Outcome, userId, reason);

        entity.UpdatedBy = userId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request rejected: {RequestNumber}", entity.RequestNumber);

        if (entity.Status == StaffTravelRequestStatus.Rejected)
            await PublishLifecycleAsync(entity, "Rejected", cancellationToken);

        return true;
    }

    public async Task<bool> CancelAsync(CancelStaffTravelRequestDto cancelDto, Guid cancelledByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(cancelDto.RequestId);

        if (entity.Status is StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException($"A request in status '{entity.Status}' cannot be cancelled.");

        entity.Status = StaffTravelRequestStatus.Cancelled;
        entity.CancellationReason = cancelDto.CancellationReason;
        // CancelledById is an Employee FK; UpdatedBy is a platform-user audit field. The one DTO
        // field was feeding BOTH, so whichever id the caller supplied was wrong for one of them.
        // They are separate now: the employee who cancelled, and the user account that acted.
        entity.CancelledById = cancelDto.CancelledById;
        entity.CancelledAt = cancelDto.CancelledAt;
        entity.UpdatedBy = cancelledByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request cancelled: {RequestNumber}", entity.RequestNumber);

        await PublishLifecycleAsync(entity, "Cancelled", cancellationToken);

        return true;
    }

    public async Task<bool> MarkCompletedAsync(Guid requestId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);

        if (entity.Status is not (StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress))
            throw new InvalidOperationException("Only approved or in-progress requests can be marked completed.");

        entity.Status = StaffTravelRequestStatus.Completed;
        entity.CompletedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request completed: {RequestNumber}", entity.RequestNumber);

        await PublishLifecycleAsync(entity, "Completed", cancellationToken);

        return true;
    }

    // ---- Comments ----------------------------------------------------------

    public async Task<StaffTravelRequestCommentDto> AddCommentAsync(CreateStaffTravelRequestCommentDto createDto, Guid tenantId, Guid createdByUserId, Guid authorEmployeeId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // The parent request was never checked, so a comment could be hung off any request id at
        // all — including another tenant's. GetOwnedRequestAsync raises "not found" for both.
        await GetOwnedRequestAsync(createDto.StaffTravelRequestId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        // AuthorId arrived on the payload, so a caller could post a comment under a colleague's
        // name. Authorship is the caller's identity, never an input.
        entity.AuthorId = authorEmployeeId;

        await _commentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _commentRepository.GetWithAuthorAsync(tenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<StaffTravelRequestCommentDto>> GetCommentsAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _commentRepository.GetByRequestIdAsync(requestId))
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToDto())
            .ToList();
    }

    public async Task<StaffTravelRequestCommentDto> UpdateCommentAsync(UpdateStaffTravelRequestCommentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommentAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _commentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _commentRepository.GetWithAuthorAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommentAsync(commentId);
        await _commentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Attachments -------------------------------------------------------

    public async Task<StaffTravelRequestAttachmentDto> AddAttachmentAsync(CreateStaffTravelRequestAttachmentDto createDto, Guid tenantId, Guid createdByUserId, Guid uploaderEmployeeId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        await GetOwnedRequestAsync(createDto.StaffTravelRequestId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        // Same actor hole as comments: who uploaded a document is a fact about the caller.
        entity.UploadedById = uploaderEmployeeId;

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _attachmentRepository.GetWithUploaderAsync(tenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<StaffTravelRequestAttachmentDto>> GetAttachmentsAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _attachmentRepository.GetByRequestIdAsync(requestId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto())
            .ToList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAttachmentAsync(attachmentId);
        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Group travel ------------------------------------------------------

    public async Task<StaffGroupTravelDto> CreateGroupTravelAsync(CreateStaffGroupTravelDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _groupTravelRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Group travel created: {GroupName}", entity.GroupName);

        var reloaded = await _groupTravelRepository.GetWithRequestsAsync(entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffGroupTravelDto> GetGroupTravelByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _groupTravelRepository.GetWithRequestsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Group travel with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffGroupTravelSummaryDto>> GetAllGroupTravelsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _groupTravelRepository.GetQueryable()
                .Where(g => g.TenantId == tenantId && !g.IsDeleted)
                .Include(g => g.LeadEmployee)
                .Include(g => g.Requests)
                .ToListAsync(cancellationToken))
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffGroupTravelSummaryDto>> GetGroupTravelsByStatusAsync(GroupTravelStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _groupTravelRepository.GetByStatusAsync(status))
            .Where(g => g.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    /// <remarks>
    /// ⚠ The re-read is not decoration. <c>GetOwnedGroupTravelAsync</c> uses the plain
    /// <c>GetByIdAsync</c>, which loads no navigations — so mapping it returned a group with a
    /// blank lead-employee name, no destination country, and an <b>empty Requests list</b>. A
    /// screen re-rendering from the edit response would have shown a group trip with nobody on it.
    /// <c>CreateGroupTravelAsync</c> two methods above already re-reads for the same reason.
    /// </remarks>
    public async Task<StaffGroupTravelDto> UpdateGroupTravelAsync(UpdateStaffGroupTravelDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroupTravelAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _groupTravelRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _groupTravelRepository.GetWithRequestsAsync(entity.Id);
        return (refreshed ?? entity).ToDto();
    }

    public async Task<bool> DeleteGroupTravelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroupTravelAsync(id);
        await _groupTravelRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<StaffGroupTravelDto> AddGroupParticipantsAsync(AddGroupTravelParticipantsDto dto, Guid tenantId, Guid createdByUserId, Guid initiatorEmployeeId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var group = await _groupTravelRepository.GetWithRequestsAsync(dto.GroupTravelId);
        if (group == null || group.TenantId != tenantId)
            throw new ArgumentException($"Group travel with ID '{dto.GroupTravelId}' not found.");

        // Skip employees already participating in the group.
        var existing = group.Requests.Select(r => r.EmployeeId).ToHashSet();
        var toAdd = dto.EmployeeIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Where(id => !existing.Contains(id))
            .ToList();

        foreach (var employeeId in toAdd)
        {
            var createDto = new CreateStaffTravelRequestDto
            {
                EmployeeId              = employeeId,
                InitiatedById           = initiatorEmployeeId,   // Employee FK, not the user id
                InitiatedByRole         = dto.InitiatedByRole,
                TravelType              = dto.TravelType,
                TravelPurpose           = dto.TravelPurpose,
                PurposeDescription      = dto.PurposeDescription,
                OrganizationUnitId      = dto.OrganizationUnitId,
                Priority                = dto.Priority,
                DestinationCountryId    = group.DestinationCountryId,
                DestinationCity         = group.DestinationCity,
                OriginCountryId         = dto.OriginCountryId,
                OriginCity              = dto.OriginCity,
                TravelStartDate         = group.TravelStartDate,
                TravelEndDate           = group.TravelEndDate,
                EstimatedTotalCost      = dto.EstimatedTotalCost,
                CurrencyCode            = dto.CurrencyCode,
                IsInternational         = dto.IsInternational,
                RequiresVisa            = dto.RequiresVisa,
                RequiresHealthClearance = dto.RequiresHealthClearance,
                RiskLevel               = dto.RiskLevel,
                GroupTravelId           = group.Id,
            };

            var entity = createDto.ToEntity(tenantId, createdByUserId);
            // Request numbers are derived from the persisted count, so save each in turn.
            entity.RequestNumber = await GenerateRequestNumberAsync(tenantId, cancellationToken);
            entity.Status = StaffTravelRequestStatus.Draft;
            await _requestRepository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Added {Count} participant(s) to group travel {GroupId}", toAdd.Count, group.Id);
        var refreshed = await _groupTravelRepository.GetWithRequestsAsync(group.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Group travel with ID '{group.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<bool> RemoveGroupParticipantAsync(Guid groupTravelId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);
        if (entity.GroupTravelId != groupTravelId)
            return false;

        entity.GroupTravelId = null;
        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Helpers -----------------------------------------------------------

    /// <summary>Mints the next travel-request number for the tenant and year.</summary>
    /// <remarks>
    /// ⚠ <b>This counted LIVE rows against an index that counts deleted ones, so a single delete
    /// broke every later create — permanently.</b> <c>IX_StaffTravelRequests_TenantId_RequestNumber</c>
    /// is unique with no <c>IsDeleted</c> filter, while the delete is a soft delete: remove
    /// <c>TR-2026-00001</c> and the live count drops back to zero, the next create mints
    /// <c>TR-2026-00001</c> again, and it collides with the row still sitting there. Every travel
    /// request in that tenant then fails with the generic handler's 500, naming nothing.
    ///
    /// <para>Eighth face of the soft-delete/unique-index defect across HR, and the same shape area
    /// 13 recorded as its worst instance — a document-number generator counting rows the schema
    /// does not agree are gone. Found on 2026-08-30 when a harness cleanup deleted its own fixture
    /// and the next run could not create one. It is live: this is the shipped create path, and any
    /// tenant that has ever deleted a travel request is already in this state.</para>
    ///
    /// <para><b>The fix reads the highest number ever issued, deleted rows included</b>, rather
    /// than counting — a count is also wrong the moment the sequence has a gap, and the maximum is
    /// the only value the unique index actually cares about.</para>
    ///
    /// <para>⚠ Still not atomic under concurrent creates. The platform has
    /// <c>INumberSequenceService</c> for exactly this — the training area uses it — and moving
    /// travel onto it is the right long-term answer; it is not done here because the existing rows
    /// carry numbers this format has to keep issuing after, so the sequence needs seeding from the
    /// current maximum as part of that change.</para>
    /// </remarks>
    private async Task<string> GenerateRequestNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"TR-{year}-";

        var issued = await _requestRepository
            .GetQueryableIncludingDeleted(r => r.TenantId == tenantId && r.RequestNumber.StartsWith(prefix))
            .Select(r => r.RequestNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(number => int.TryParse(number[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D5}";
    }
}

#endregion
