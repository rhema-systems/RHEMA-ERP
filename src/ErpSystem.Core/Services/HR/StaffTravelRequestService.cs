using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
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
    // Held alongside _currentUserProvider only for EmployeeId, which ICurrentUserProvider does not
    // carry. The travel entity's traveller column is an Employee FK, so the self-approval rule
    // below cannot be asked of the user id. Same pairing JobInterviewService uses.
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly HrCurrencyBridge _currency;
    private readonly StaffTravelPolicyGuard _policyGuard;
    private readonly ILogger<StaffTravelRequestService> _logger;

    public StaffTravelRequestService(
        IStaffTravelRequestRepository requestRepository,
        IStaffTravelRequestCommentRepository commentRepository,
        IStaffTravelRequestAttachmentRepository attachmentRepository,
        IStaffGroupTravelRepository groupTravelRepository,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        HrCurrencyBridge currency,
        StaffTravelPolicyGuard policyGuard,
        ILogger<StaffTravelRequestService> logger)
    {
        _requestRepository = requestRepository;
        _commentRepository = commentRepository;
        _attachmentRepository = attachmentRepository;
        _groupTravelRepository = groupTravelRepository;
        _currentUserProvider = currentUserProvider;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currency = currency;
        _policyGuard = policyGuard;
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

    // ---- The trip's facts: the server's, not the payload's -----------------
    //
    // Travel final closure, lane 1 (findings A4, A5, O-5, O-13, O-14, T-17). The request took its
    // unit, its international flag and its policy from the payload, accepted a traveller who had
    // left, and went for approval costed at nothing, back to front, in the past, over another trip,
    // or above the policy's single-trip limit. Each rule below is one of those, refused before the
    // workflow engine is asked — so a refused submission leaves no approval task behind.

    private sealed record TravellerFacts(Guid Id, string Name, Guid? OrganizationUnitId, string? InactiveState);

    /// <summary>The traveller as their employee record has them, or null when there is no such employee.</summary>
    /// <remarks>
    /// <para><b>Their unit is the request's unit</b> (finding O-5). The policy guard resolves on the
    /// request's unit and the payload used to choose it: a self-raised trip carried none, so no
    /// unit-scoped policy ever applied to it, and the desk could name a unit with a laxer policy. It is
    /// the employee's own unit, or their position's when the employee row has none (29 of 4,398 staff
    /// on UAT, 2026-10-02; none where the two disagree).</para>
    ///
    /// <para><b>Who counts as having left</b> (finding O-14): a record switched off, or a status of
    /// Inactive, Terminated or Retired — what separation, termination and deactivation write.
    /// Probation, leave and suspension are statuses of someone still employed.</para>
    /// </remarks>
    private async Task<TravellerFacts?> FindTravellerAsync(
        Guid tenantId, Guid employeeId, CancellationToken cancellationToken)
    {
        var row = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == tenantId && e.Id == employeeId)
            .Select(e => new
            {
                e.FirstName,
                e.LastName,
                e.IsActive,
                e.StaffStatus,
                e.OrganizationUnitId,
                PositionUnitId = (Guid?)e.Position.OrganizationUnitId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        string? inactive = row.StaffStatus is StaffStatus.Inactive or StaffStatus.Terminated or StaffStatus.Retired
            ? row.StaffStatus.ToString()
            : row.IsActive ? null : "deactivated";

        return new TravellerFacts(employeeId, $"{row.FirstName} {row.LastName}".Trim(),
            row.OrganizationUnitId ?? row.PositionUnitId, inactive);
    }

    /// <param name="requireActive">
    /// True where the trip moves forward — raising it, submitting it. An edit of a draft re-reads the
    /// traveller's unit without refusing a leaver; the desk cancels that trip instead.
    /// </param>
    private async Task<TravellerFacts> RequireTravellerAsync(
        Guid tenantId, Guid employeeId, bool requireActive, CancellationToken cancellationToken)
    {
        var traveller = await FindTravellerAsync(tenantId, employeeId, cancellationToken)
            ?? throw new InvalidOperationException("The traveller named is not an employee of this organisation.");

        if (requireActive && traveller.InactiveState is { } state)
            throw new InvalidOperationException(
                $"{traveller.Name} is no longer an active employee ({state}), so travel cannot be raised " +
                "or submitted for them.");

        return traveller;
    }

    /// <summary>What the server decides about a trip: the traveller's unit, and whether it crosses a border.</summary>
    private static void ApplyServerFacts(StaffTravelRequest entity, TravellerFacts traveller)
    {
        entity.OrganizationUnitId = traveller.OrganizationUnitId;
        entity.IsInternational = entity.OriginCountryId != entity.DestinationCountryId;
    }

    /// <summary>Both countries are foreign keys from the payload; they must be this organisation's.</summary>
    private async Task RequireKnownCountriesAsync(
        Guid tenantId, Guid originCountryId, Guid destinationCountryId, CancellationToken cancellationToken)
    {
        var wanted = new[] { originCountryId, destinationCountryId }.Distinct().ToList();
        var found = await _unitOfWork.Repository<Country>().GetQueryable()
            .CountAsync(c => c.TenantId == tenantId && wanted.Contains(c.Id), cancellationToken);
        if (found != wanted.Count)
            throw new InvalidOperationException(
                "Choose the origin and destination countries from the list — one of them is not a country " +
                "this organisation holds.");
    }

    private static void RequireDatesInOrder(DateOnly start, DateOnly end)
    {
        if (end < start)
            throw new InvalidOperationException(
                $"The return date ({end:dd MMM yyyy}) is before the departure date ({start:dd MMM yyyy}).");
    }

    /// <summary>The group and the earlier request a trip names must exist here — and the earlier one must be the same traveller's.</summary>
    /// <remarks>Tenant checks only. Slice 1c gives group membership its own rules (capacity, status, dates).</remarks>
    private async Task RequireLinksAsync(
        Guid tenantId, Guid travellerId, Guid? groupTravelId, Guid? parentRequestId, Guid? selfId,
        CancellationToken cancellationToken)
    {
        if (groupTravelId is Guid groupId)
        {
            var group = await _groupTravelRepository.GetByIdAsync(groupId);
            if (group is null || group.TenantId != tenantId || group.IsDeleted)
                throw new InvalidOperationException("The group trip named does not exist.");
        }

        if (parentRequestId is Guid parentId)
        {
            var parentTraveller = parentId == selfId
                ? (Guid?)null
                : await _requestRepository.GetQueryable()
                    .Where(r => r.Id == parentId && r.TenantId == tenantId)
                    .Select(r => (Guid?)r.EmployeeId)
                    .FirstOrDefaultAsync(cancellationToken);
            if (parentTraveller != travellerId)
                throw new InvalidOperationException("The earlier request named is not one of this traveller's.");
        }
    }

    /// <summary>One traveller cannot be on two trips at once (finding O-13).</summary>
    /// <remarks>
    /// Trips may MEET on a travel day — back from one and off on the next the same day — but not run
    /// over each other, and two cannot leave on the same day. Only trips still going ahead count:
    /// submitted, approved or under way.
    /// </remarks>
    private async Task RequireNoOverlappingTripAsync(
        StaffTravelRequest entity, TravellerFacts traveller, CancellationToken cancellationToken)
    {
        var clash = await _requestRepository.GetQueryable()
            .Where(r => r.TenantId == entity.TenantId
                     && r.EmployeeId == entity.EmployeeId
                     && r.Id != entity.Id
                     && (r.Status == StaffTravelRequestStatus.Submitted
                         || r.Status == StaffTravelRequestStatus.Approved
                         || r.Status == StaffTravelRequestStatus.InProgress)
                     && ((r.TravelStartDate < entity.TravelEndDate && r.TravelEndDate > entity.TravelStartDate)
                         || r.TravelStartDate == entity.TravelStartDate))
            .OrderBy(r => r.TravelStartDate)
            .Select(r => new { r.RequestNumber, r.Status, r.TravelStartDate, r.TravelEndDate })
            .FirstOrDefaultAsync(cancellationToken);

        if (clash is not null)
            throw new InvalidOperationException(
                $"{traveller.Name} is already on trip {clash.RequestNumber} ({clash.Status}) from " +
                $"{clash.TravelStartDate:dd MMM yyyy} to {clash.TravelEndDate:dd MMM yyyy}, which overlaps " +
                "these dates. Change the dates, or cancel the other trip first.");
    }

    /// <summary>
    /// The policy's single-trip limit, checked at submission (decision D-1, finding T-17) — the 422
    /// names the limit. A trip costed in another currency is compared at Finance's rate.
    /// </summary>
    private async Task RequireWithinSingleTripLimitAsync(
        StaffTravelRequest entity, TravelPolicyCaps caps, DateOnly asOf, CancellationToken cancellationToken)
    {
        if (caps.MaxSingleTripBudget is not decimal limit) return;

        // A policy written before migration batch 1 has no currency; the batch backfilled the base,
        // and the base is what its limits were always read in.
        var limitCurrency = caps.CurrencyCode
            ?? await _currency.GetBaseCurrencyCodeAsync(cancellationToken)
            ?? entity.CurrencyCode;
        var estimate = await _currency.ConvertBetweenAsync(
            entity.EstimatedTotalCost, entity.CurrencyCode, limitCurrency, asOf, cancellationToken);
        if (estimate <= limit) return;

        var converted = string.Equals(entity.CurrencyCode, limitCurrency, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $" (about {limitCurrency} {estimate:N2} at Finance's rate)";
        throw new InvalidOperationException(
            $"The estimated cost of {entity.CurrencyCode} {entity.EstimatedTotalCost:N2}{converted} is above the " +
            $"{caps.PolicyName} limit of {limitCurrency} {limit:N2} for a single trip. Reduce the estimate, or " +
            "ask a travel administrator about the policy.");
    }

    /// <summary>
    /// Approved leave over the trip's days — told, not refused (finding O-13): the approver weighs it,
    /// and the leave may be the thing that moves.
    /// </summary>
    private async Task<List<string>> ApprovedLeaveWarningsAsync(
        StaffTravelRequest entity, TravellerFacts traveller, CancellationToken cancellationToken)
    {
        var leave = await _unitOfWork.Repository<LeaveRequest>().GetQueryable()
            .Where(l => l.TenantId == entity.TenantId
                     && l.EmployeeId == entity.EmployeeId
                     && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.InProgress)
                     && l.StartDate <= entity.TravelEndDate
                     && l.EndDate >= entity.TravelStartDate)
            .OrderBy(l => l.StartDate)
            .Select(l => new { l.StartDate, l.EndDate, TypeName = l.LeaveType.Name })
            .ToListAsync(cancellationToken);

        return leave
            .Select(l => $"{traveller.Name} has approved leave ({l.TypeName}) from {l.StartDate:dd MMM yyyy} " +
                         $"to {l.EndDate:dd MMM yyyy}, over this trip's dates.")
            .ToList();
    }

    /// <summary>Why an edit is refused, and what to do instead.</summary>
    private static string EditRefusal(StaffTravelRequestStatus status) => status switch
    {
        StaffTravelRequestStatus.Submitted =>
            "A submitted request cannot be edited while it is out for approval. Recall it, or ask the " +
            "approver to return it for revision.",
        StaffTravelRequestStatus.Approved =>
            "An approved trip cannot be edited. Use Request change to send it back for re-approval.",
        StaffTravelRequestStatus.InProgress => "A trip that is under way cannot be edited.",
        _ => $"A request that is {status} cannot be edited.",
    };

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

    /// <summary>
    /// The policy a trip would be checked against — for the request form, before anything is saved
    /// (finding T-16). The same resolution submission uses: the traveller's own unit, the two
    /// countries, the departure date, approved policies only.
    /// </summary>
    public async Task<StaffTravelPolicyPreviewDto> GetPolicyPreviewAsync(
        Guid employeeId, DateOnly departure, Guid? originCountryId, Guid? destinationCountryId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var traveller = await FindTravellerAsync(tenantId, employeeId, cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var international = originCountryId is Guid origin && destinationCountryId is Guid destination
                            && origin != destination;
        var caps = await _policyGuard.ResolveForAsync(
            tenantId, employeeId, traveller.OrganizationUnitId, departure, international, cancellationToken);

        var unitName = traveller.OrganizationUnitId is Guid unitId
            ? await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                .Where(u => u.Id == unitId && u.TenantId == tenantId)
                .Select(u => u.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new StaffTravelPolicyPreviewDto
        {
            EmployeeId = employeeId,
            OrganizationUnitId = traveller.OrganizationUnitId,
            OrganizationUnitName = unitName,
            IsInternational = international,
            HasPolicy = caps.HasPolicy,
            PolicyId = caps.PolicyId,
            PolicyName = caps.PolicyName,
            VersionNumber = caps.VersionNumber,
            CurrencyCode = caps.HasPolicy
                ? caps.CurrencyCode ?? await _currency.GetBaseCurrencyCodeAsync(cancellationToken)
                : null,
            MaxSingleTripBudget = caps.MaxSingleTripBudget,
            // A class stored as 0 is the omitted-class defect lane 4 fixes (C2); show no cap, not "0".
            MaxFlightClass = caps.MaxFlightClass is { } cabin && Enum.IsDefined(cabin) ? cabin : null,
            MaxHotelRatePerNight = caps.MaxHotelRatePerNight is > 0m ? caps.MaxHotelRatePerNight : null,
        };
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
        // here: travel does not invent a rate, and a headline converted at one day's rate would hide
        // that the trips were costed in different currencies. (This also said Finance's conversion
        // was inverted; Finance fixed that on 2026-09-10.)
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
        var traveller = await RequireTravellerAsync(tenantId, createDto.EmployeeId, requireActive: true, cancellationToken);
        await RequireKnownCountriesAsync(tenantId, createDto.OriginCountryId, createDto.DestinationCountryId, cancellationToken);
        RequireDatesInOrder(createDto.TravelStartDate, createDto.TravelEndDate);
        await RequireLinksAsync(tenantId, traveller.Id, createDto.GroupTravelId, createDto.ParentRequestId,
            selfId: null, cancellationToken);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        ApplyServerFacts(entity, traveller);
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

        // Lane 1, finding A1: only a Draft or a returned request is the requester's to change. This
        // refused only Approved, Completed, Cancelled and Closed, so a Submitted request could be
        // rewritten while the approver decided it — on the desk and through /me alike.
        if (entity.Status is not (StaffTravelRequestStatus.Draft or StaffTravelRequestStatus.ReturnedForRevision))
            throw new InvalidOperationException(EditRefusal(entity.Status));

        await _currency.RequireKnownCurrencyAsync(updateDto.CurrencyCode, cancellationToken);
        await RequireKnownCountriesAsync(tenantId, updateDto.OriginCountryId, updateDto.DestinationCountryId, cancellationToken);
        RequireDatesInOrder(updateDto.TravelStartDate, updateDto.TravelEndDate);
        if (updateDto.GroupTravelId != entity.GroupTravelId)
            await RequireLinksAsync(tenantId, entity.EmployeeId, updateDto.GroupTravelId, parentRequestId: null,
                entity.Id, cancellationToken);
        var traveller = await RequireTravellerAsync(tenantId, entity.EmployeeId, requireActive: false, cancellationToken);

        entity.UpdateEntity(updateDto, updatedByUserId);
        ApplyServerFacts(entity, traveller);

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
    /// <summary>
    /// You cannot decide your own trip.
    /// </summary>
    /// <remarks>
    /// <para>Travel had NO segregation-of-duties rule of its own: <c>CanUserApproveAsync</c> was the
    /// whole gate, and it is a fact about the published definition rather than about the record. On
    /// a tenant with a definition that is defensible — the definition names the approver and can set
    /// <c>preventInitiatorApproval</c>. On a tenant with none it was not a gate at all, and once
    /// submit stopped auto-approving (see <see cref="HrWorkflowFallbackAuthority"/>) a holder of
    /// <see cref="HrPermissions.ApproveTravel"/> could have approved their own trip.</para>
    ///
    /// <para>It runs on BOTH paths deliberately, like the equivalent rule in
    /// <c>StaffMovementService.ApproveAsync</c>: the engine's own check is by ApplicationUser, and
    /// this one is by Employee, so it still catches a traveller deciding through a second login.
    /// The comparison is Employee id to Employee id — <c>StaffTravelRequest.EmployeeId</c> is the
    /// traveller, and the engine's actor is a user id, so the two must not be mixed.</para>
    /// </remarks>
    private void RequireNotTheTraveller(StaffTravelRequest entity, string verb)
    {
        var callerEmployeeId = _currentUserService.EmployeeId;
        if (callerEmployeeId is null || callerEmployeeId.Value != entity.EmployeeId) return;

        throw new UnauthorizedAccessException($"You cannot {verb} your own travel request.");
    }

    /// <remarks>
    /// <para><b>Everything that must hold is checked before the engine is asked</b> (lane 1, finding
    /// A4), so a refused submission leaves no approval task behind: a traveller still employed, a
    /// cost, dates in order and not in the past, a currency Finance holds, no other trip over the same
    /// days, and an estimate within the policy's single-trip limit. Submission used to check the status
    /// and nothing else.</para>
    ///
    /// <para><b>A past departure</b> is refused unless the travel desk gives the reason — a trip taken
    /// at short notice whose paperwork followed. The reason is kept on the request as an internal note
    /// in the submitter's name; the self-service route never passes one.</para>
    ///
    /// <para><b>The policy is recorded</b> — the one the trip was checked against, or none. The column
    /// had no writer; the payload set it and nothing read it.</para>
    /// </remarks>
    public async Task<StaffTravelSubmitResultDto> SubmitAsync(SubmitStaffTravelRequestDto submitDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedRequestAsync(submitDto.RequestId);

        if (entity.Status is not (StaffTravelRequestStatus.Draft or StaffTravelRequestStatus.ReturnedForRevision))
            throw new InvalidOperationException("Only draft or returned requests can be submitted.");

        var traveller = await RequireTravellerAsync(tenantId, entity.EmployeeId, requireActive: true, cancellationToken);
        // A draft written before lane 1 may still carry a unit and an international flag the payload
        // chose; the policy below must be resolved on the facts.
        ApplyServerFacts(entity, traveller);

        if (entity.EstimatedTotalCost <= 0m)
            throw new InvalidOperationException(
                "Enter the trip's estimated cost before submitting it — the approver decides the budget against it.");
        RequireDatesInOrder(entity.TravelStartDate, entity.TravelEndDate);
        await _currency.RequireKnownCurrencyAsync(entity.CurrencyCode, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lateReason = submitDto.LateSubmissionReason?.Trim();
        var departed = entity.TravelStartDate < today;
        if (departed && string.IsNullOrEmpty(lateReason))
            throw new InvalidOperationException(
                $"The departure date ({entity.TravelStartDate:dd MMM yyyy}) has passed. Change the dates, or ask " +
                "the travel desk to submit it with the reason it is late.");
        if (departed && submitDto.SubmittedByEmployeeId is null)
            throw new InvalidOperationException(
                "Submitting a trip after its departure date records the reason in the submitter's name, and " +
                "this account is not linked to an employee.");

        await RequireNoOverlappingTripAsync(entity, traveller, cancellationToken);

        var caps = await _policyGuard.ResolveAsync(entity, cancellationToken);
        await RequireWithinSingleTripLimitAsync(entity, caps, today, cancellationToken);
        entity.PolicyId = caps.PolicyId;

        var warnings = await ApprovedLeaveWarningsAsync(entity, traveller, cancellationToken);

        var (workflowResult, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegrationService, EntityType, entity.Id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "Failed to start the travel approval workflow.");

        // The adapter owns the status. A definition with one Approval step approves on submission
        // (the documented single-step trap), so this can legitimately come back Approved — but only
        // when a definition is actually published. With none, the engine's "not configured" signal
        // is also Approved, which approved the trip outright; the helper substitutes Pending there.
        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, submitOutcome, RequireUserId());

        entity.SubmittedAt = DateTime.UtcNow;
        entity.UpdatedBy = RequireUserId().ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        if (entity.Status == StaffTravelRequestStatus.Approved)
            entity.ApprovedAt ??= DateTime.UtcNow;

        if (departed)
        {
            await _commentRepository.AddAsync(new StaffTravelRequestComment
            {
                TenantId = entity.TenantId,
                StaffTravelRequestId = entity.Id,
                AuthorId = submitDto.SubmittedByEmployeeId!.Value,
                CommentType = TravelRequestCommentType.InternalNote,
                Body = $"Submitted on {today:dd MMM yyyy}, after the departure date of " +
                       $"{entity.TravelStartDate:dd MMM yyyy}. Reason: {lateReason}",
                IsVisibleToTraveller = false,
                CreatedBy = RequireUserId().ToString(),
            });
        }

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request submitted: {RequestNumber}", entity.RequestNumber);

        // Announce what actually happened, not what was asked for: a single-step definition
        // approves on submission, and a notification saying "awaiting approval" about a request
        // that is already approved is worse than none.
        await PublishLifecycleAsync(entity,
            entity.Status == StaffTravelRequestStatus.Approved ? "Approved" : "Submitted",
            cancellationToken);

        return new StaffTravelSubmitResultDto
        {
            Message = entity.Status == StaffTravelRequestStatus.Approved
                ? "Travel request submitted and approved."
                : "Travel request submitted.",
            Status = entity.Status,
            PolicyId = caps.PolicyId,
            PolicyName = caps.PolicyName,
            Warnings = warnings,
        };
    }

    public async Task<bool> ApproveAsync(ApproveStaffTravelRequestDto approveDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(approveDto.RequestId);

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted requests can be approved.");

        var userId = RequireUserId();

        RequireNotTheTraveller(entity, "approve");

        // The engine decides who may approve, against the published definition. The bespoke chain
        // this replaces took the approver from the request body, so a caller could record a
        // decision in someone else's name. With no definition published there is no instance, so
        // CanUserApproveAsync answers false for everybody and the trip could not be decided at
        // all — the Travel approve tier rules in that case.
        var decisionOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserProvider, EntityType, entity.Id, userId,
            "Approve", approveDto.Notes, "approve a travel request", HrPermissions.ApproveTravel);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, decisionOutcome, userId);

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

        RequireNotTheTraveller(entity, "reject");

        var decisionOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserProvider, EntityType, entity.Id, userId,
            "Reject", reason, "reject a travel request", HrPermissions.ApproveTravel);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, decisionOutcome, userId, reason);

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

        // The single request's rules, for every participant (lane 1). Each is checked BEFORE any is
        // raised, so a leaver in the list refuses the call instead of leaving half a group behind;
        // the currency was not checked here at all.
        await _currency.RequireKnownCurrencyAsync(dto.CurrencyCode, cancellationToken);
        await RequireKnownCountriesAsync(tenantId, dto.OriginCountryId, group.DestinationCountryId, cancellationToken);
        RequireDatesInOrder(group.TravelStartDate, group.TravelEndDate);
        var travellers = new List<TravellerFacts>();
        foreach (var employeeId in toAdd)
            travellers.Add(await RequireTravellerAsync(tenantId, employeeId, requireActive: true, cancellationToken));

        foreach (var traveller in travellers)
        {
            var createDto = new CreateStaffTravelRequestDto
            {
                EmployeeId              = traveller.Id,
                InitiatedById           = initiatorEmployeeId,   // Employee FK, not the user id
                InitiatedByRole         = dto.InitiatedByRole,
                TravelType              = dto.TravelType,
                TravelPurpose           = dto.TravelPurpose,
                PurposeDescription      = dto.PurposeDescription,
                Priority                = dto.Priority,
                DestinationCountryId    = group.DestinationCountryId,
                DestinationCity         = group.DestinationCity,
                OriginCountryId         = dto.OriginCountryId,
                OriginCity              = dto.OriginCity,
                TravelStartDate         = group.TravelStartDate,
                TravelEndDate           = group.TravelEndDate,
                EstimatedTotalCost      = dto.EstimatedTotalCost,
                CurrencyCode            = dto.CurrencyCode,
                RequiresVisa            = dto.RequiresVisa,
                RequiresHealthClearance = dto.RequiresHealthClearance,
                RiskLevel               = dto.RiskLevel,
                GroupTravelId           = group.Id,
            };

            var entity = createDto.ToEntity(tenantId, createdByUserId);
            // Each participant's own unit — the template's single unit stamped the whole group with one.
            ApplyServerFacts(entity, traveller);
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
