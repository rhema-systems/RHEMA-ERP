using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using ErpSystem.Application.HR.Extensions;
using Microsoft.AspNetCore.Identity;
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
    // Lane 2 (D-7): the step a request is on — the line rule applies at the line-manager stage alone —
    // and the logins of the traveller's line authorities, whom that stage is addressed to.
    private readonly IWorkflowService _workflowService;
    private readonly UserManager<ApplicationUser> _userManager;
    // Lane 6 (FX-3, D2): the trip's cancel and its Request change cancel the fleet trips of its company-vehicle legs.
    private readonly IStaffTravelFleetService _fleet;
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
        IWorkflowService workflowService,
        UserManager<ApplicationUser> userManager,
        IStaffTravelFleetService fleet,
        ILogger<StaffTravelRequestService> logger)
    {
        _fleet = fleet;
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
        _workflowService = workflowService;
        _userManager = userManager;
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

    /// <summary>
    /// Lane 4, C5 (T-46): <c>TravelRiskLevel.Prohibited</c> prohibits — at submission and at every approval stage.
    /// It is the trip's own level (the requester's choice) or, higher, the latest risk assessment the desk recorded
    /// for the trip that still holds on the departure date (P2: the request's level is the requester's to pick).
    /// A Critical trip's acknowledged assessment is lane 7's (D-18).
    /// </summary>
    private async Task RequireNotProhibitedAsync(StaffTravelRequest entity, string verb, CancellationToken cancellationToken)
    {
        if (entity.RiskLevel == TravelRiskLevel.Prohibited)
            throw new InvalidOperationException(
                $"Travel request {entity.RequestNumber} is rated Prohibited, so it cannot be {verb}. Travel to a prohibited " +
                "destination does not go ahead; if the rating is wrong, correct it first.");

        var assessed = await _unitOfWork.Repository<StaffTravelRiskAssessment>()
            .GetQueryable(a => a.TenantId == entity.TenantId && a.StaffTravelRequestId == entity.Id
                            && (a.ValidUntil == null || a.ValidUntil >= entity.TravelStartDate))
            .OrderByDescending(a => a.AssessedAt ?? a.CreatedAt)
            .Select(a => new { a.RiskLevel, a.AssessmentSource })
            .FirstOrDefaultAsync(cancellationToken);
        if (assessed?.RiskLevel == TravelRiskLevel.Prohibited)
            throw new InvalidOperationException(
                $"The latest risk assessment of travel request {entity.RequestNumber}" +
                (string.IsNullOrWhiteSpace(assessed.AssessmentSource) ? string.Empty : $" ({assessed.AssessmentSource})") +
                $" rates the destination Prohibited, so the trip cannot be {verb}.");
    }

    /// <summary>
    /// Lane 7 (E4, D-39): the visa flag from the register, when the traveller's passport is on file and the register has
    /// the pair — unless the requester answered differently and said why (<paramref name="overrideReason"/>), which then
    /// stands and is kept on the trip, and as an internal note when the reason is new. Without a passport or an entry,
    /// the requester's answer stands and there is nothing to override.
    /// </summary>
    private async Task ApplyVisaRegisterAsync(
        StaffTravelRequest entity, bool askedFlag, string? overrideReason, CancellationToken cancellationToken)
    {
        var verdict = await StaffTravelComplianceRules.VisaVerdictAsync(
            _unitOfWork, entity.TenantId, entity.EmployeeId, entity.DestinationCountryId, cancellationToken);
        var reason = string.IsNullOrWhiteSpace(overrideReason) ? null : overrideReason.Trim();
        if (!verdict.Known)
        {
            entity.RequiresVisa = askedFlag;
            entity.VisaOverrideReason = null;
            return;
        }
        if (askedFlag == verdict.Needs || reason is null)
        {
            entity.RequiresVisa = verdict.Needs;
            entity.VisaOverrideReason = null;
            return;
        }
        if (reason.Length < 5)
            throw new InvalidOperationException(
                "Say why the trip's visa answer differs from the visa register, in at least five characters.");
        var isNew = !string.Equals(entity.VisaOverrideReason, reason, StringComparison.Ordinal);
        entity.RequiresVisa = askedFlag;
        entity.VisaOverrideReason = reason;
        if (isNew && _currentUserService.EmployeeId is Guid author)
            await _commentRepository.AddAsync(new StaffTravelRequestComment
            {
                TenantId = entity.TenantId,
                StaffTravelRequestId = entity.Id,
                AuthorId = author,
                CommentType = TravelRequestCommentType.InternalNote,
                Body = Clip($"The visa register says a {verdict.PassportCountry} passport " +
                            (verdict.Needs ? "needs a visa" : "needs no visa applied for") + $" for {verdict.DestinationCountry}; " +
                            $"the trip is marked as {(askedFlag ? "needing one" : "not needing one")}. Reason: {reason}", 2000),
                IsVisibleToTraveller = false,
                CreatedBy = RequireUserId().ToString(),
            });
    }

    private static void RequireDatesInOrder(DateOnly start, DateOnly end)
    {
        if (end < start)
            throw new InvalidOperationException(
                $"The return date ({end:dd MMM yyyy}) is before the departure date ({start:dd MMM yyyy}).");
    }

    /// <summary>The earlier request a trip names must exist here and be the same traveller's.</summary>
    /// <remarks>
    /// The group a trip belongs to is no longer the payload's at all (slice 1c): the group's own
    /// endpoints add, link and remove travellers.
    /// </remarks>
    private async Task RequireParentRequestAsync(
        Guid tenantId, Guid travellerId, Guid? parentRequestId, Guid? selfId,
        CancellationToken cancellationToken)
    {
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
    /// The policy's single-trip limit, checked at submission against the estimate (decision D-1, finding
    /// T-17) and at HR's approval against the approved budget (lane 2, O-9) — the 422 names the limit. An
    /// amount in another currency is compared at Finance's rate.
    /// </summary>
    /// <param name="amount">The figure checked, in the request's currency.</param>
    /// <param name="what">What the figure is, for the refusal: "estimated cost", "approved budget".</param>
    /// <param name="remedy">What to do about it, for the refusal.</param>
    private async Task RequireWithinSingleTripLimitAsync(
        StaffTravelRequest entity, TravelPolicyCaps caps, DateOnly asOf, decimal amount, string what,
        string remedy, CancellationToken cancellationToken)
    {
        if (caps.MaxSingleTripBudget is not decimal limit) return;

        // A policy written before migration batch 1 has no currency; the batch backfilled the base,
        // and the base is what its limits were always read in.
        var limitCurrency = caps.CurrencyCode
            ?? await _currency.GetBaseCurrencyCodeAsync(cancellationToken)
            ?? entity.CurrencyCode;
        var compared = await _currency.ConvertBetweenAsync(
            amount, entity.CurrencyCode, limitCurrency, asOf, cancellationToken);
        if (compared <= limit) return;

        var converted = string.Equals(entity.CurrencyCode, limitCurrency, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $" (about {limitCurrency} {compared:N2} at Finance's rate)";
        throw new InvalidOperationException(
            $"The {what} of {entity.CurrencyCode} {amount:N2}{converted} is above the " +
            $"{caps.PolicyName} limit of {limitCurrency} {limit:N2} for a single trip. {remedy}");
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
        var dto = entity.ToDto();
        // Lane 6 (D-33): a driver's own request says whose company vehicle it drives — read from the leg that keeps it.
        var driverFor = await _unitOfWork.Repository<StaffTravelGroundTransport>()
            .GetQueryable(g => g.TenantId == tenantId && !g.IsDeleted && g.DriverTravelRequestId == id)
            .Select(g => new { g.StaffTravelRequestId, g.StaffTravelRequest.RequestNumber })
            .FirstOrDefaultAsync(cancellationToken);
        if (driverFor is not null)
        {
            dto.DriverForRequestId = driverFor.StaffTravelRequestId;
            dto.DriverForRequestNumber = driverFor.RequestNumber;
        }
        return dto;
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
        await RequireParentRequestAsync(tenantId, traveller.Id, createDto.ParentRequestId, selfId: null, cancellationToken);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        ApplyServerFacts(entity, traveller);
        await ApplyVisaRegisterAsync(entity, createDto.RequiresVisa, createDto.VisaOverrideReason, cancellationToken);
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
        var traveller = await RequireTravellerAsync(tenantId, entity.EmployeeId, requireActive: false, cancellationToken);

        entity.UpdateEntity(updateDto, updatedByUserId);
        ApplyServerFacts(entity, traveller);
        await ApplyVisaRegisterAsync(entity, updateDto.RequiresVisa, updateDto.VisaOverrideReason, cancellationToken);

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

    // ---- The approval ladder (lane 2, decision D-7) ---------------------------
    //
    // Approval runs in two stages: the traveller's line authority, then HR. The engine addresses the
    // first stage BY NAME to the traveller's two nearest line authorities who can sign in (see
    // StaffTravelApprovalLadder), falling back to the HR role when none can — and this service checks
    // the decider again, on the rule itself: at the line-manager stage only a line authority decides,
    // and the travel desk only when no line authority can, with the reason kept on the request. A Manager
    // who is not the traveller's never decides it. Finding O-1: before this, a line manager could neither
    // open nor approve a travel request at all.

    private const string DecidesAsLineAuthority = "LineAuthority";
    private const string DecidesAsTravelDesk = "TravelDesk";
    private const string DecidesAsApprover = "Approver";

    /// <summary>How the caller stands to a submitted request at the step it is on.</summary>
    /// <param name="StageName">The engine's current step; null when no approval is in progress.</param>
    /// <param name="Refusal">Why the caller may not decide; null when they may (the engine has agreed).</param>
    /// <param name="DecidesAs">How the caller decides when they may.</param>
    /// <param name="Line">At the line-manager stage: the traveller's line authorities.</param>
    /// <param name="WaitingFor">At the line-manager stage: the line authorities the engine will accept.</param>
    /// <param name="NextStageName">The approval stage the route goes to after this one; null when this is the
    /// last — the one whose approval approves the trip, and so the one that sets the budget.</param>
    private sealed record DecisionStanding(
        string? StageName,
        bool IsLineStage,
        string? Refusal,
        string? DecidesAs,
        string? Relation,
        IReadOnlyList<HrLinePerson> Line,
        IReadOnlyList<HrLineApprover> WaitingFor,
        string? NextStageName = null)
    {
        public bool MayDecide => Refusal is null && DecidesAs is not null;
        public bool IsFinalStage => NextStageName is null;
    }

    /// <summary>
    /// The approval stage the request's route goes to after the one it is on, or null when that is the last.
    /// </summary>
    /// <remarks>
    /// Read from the route itself — the engine's current step, its definition, the next approval step by order
    /// — so a stage an administrator adds in the workflow designer (Finance between the line manager and HR,
    /// say) is a middle stage here too. Lane 2a assumed every stage after the line manager's was the last:
    /// with a stage added, its approver would have been offered the budget and the figure discarded.
    /// </remarks>
    private async Task<string?> NextApprovalStageAsync(WorkflowStepInfo? step, CancellationToken cancellationToken)
    {
        if (step is null || step.Id == Guid.Empty) return null;

        var current = await _unitOfWork.Repository<WorkflowStepInstance>().GetQueryable()
            .Where(si => si.Id == step.Id)
            .Select(si => new { si.WorkflowStep.WorkflowDefinitionId, si.WorkflowStep.Order })
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null) return null;

        return await _unitOfWork.Repository<WorkflowStep>().GetQueryable()
            .Where(s => s.WorkflowDefinitionId == current.WorkflowDefinitionId
                     && !s.IsDeleted
                     && s.StepType == WorkflowStepType.Approval
                     && s.Order > current.Order)
            .OrderBy(s => s.Order)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool IsLineStageName(string? stepName)
        => string.Equals(stepName?.Trim(), StaffTravelApprovalLadder.LineStageName, StringComparison.OrdinalIgnoreCase);

    private static string NameList(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            _ => string.Join(", ", list.Take(list.Count - 1)) + " or " + list[^1],
        };
    }

    /// <summary>
    /// Who may decide this request now, and as whom — the rule the approve, reject and return verbs run,
    /// and the one the approver's door, the queue and the screen read, so the button and the verb agree.
    /// </summary>
    /// <remarks>
    /// <para>Never throws for a refusal: <see cref="RequireStandingAsync"/> is the throwing form.</para>
    ///
    /// <para><b>With a published definition</b> the engine must accept the caller first. At the
    /// line-manager stage the caller must then be one of the traveller's line authorities — or, when none
    /// of those the stage was sent to can decide it, hold the travel desk's permission. Any other stage is
    /// the engine's alone (and never the traveller's, which the verbs check separately). A request decided
    /// on the one-step route that lane 2 retired has no line-manager stage, so it is decided as that route
    /// said.</para>
    ///
    /// <para><b>With none published</b> there are no stages: the travel approve tier decides, as before.</para>
    /// </remarks>
    /// <param name="explain">False for the queue, which needs the answer and not the reason: a caller the
    /// engine does not accept is refused without reading the traveller's line.</param>
    private async Task<DecisionStanding> GetStandingAsync(
        StaffTravelRequest entity, bool callerIsTravelDesk, CancellationToken cancellationToken, bool explain = true)
    {
        var none = (IReadOnlyList<HrLinePerson>)Array.Empty<HrLinePerson>();
        var nobody = (IReadOnlyList<HrLineApprover>)Array.Empty<HrLineApprover>();
        var userId = _currentUserProvider.UserId;

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            return new DecisionStanding(null, false, "Only a request out for approval can be decided.", null, null, none, nobody);

        if (!await _workflowIntegrationService.HasActiveApprovalInstanceAsync(EntityType, entity.Id))
        {
            // No instance: either no definition was published when it was submitted, or none is now.
            if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
                return new DecisionStanding(null, false,
                    "This request is not waiting on an approval route. Ask the traveller to recall it and submit it again.",
                    null, null, none, nobody);
            return HrWorkflowFallbackAuthority.CanRuleWithoutWorkflow(_currentUserProvider.Roles, HrPermissions.ApproveTravel)
                ? new DecisionStanding(null, false, null, DecidesAsApprover, null, none, nobody)
                : new DecisionStanding(null, false,
                    "No approval workflow is published for travel, so whoever holds the travel approve permission decides.",
                    null, null, none, nobody);
        }

        var callerMayApprove = userId != Guid.Empty
            && await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, userId);
        if (!callerMayApprove && !explain)
            return new DecisionStanding(null, false, "Not waiting for you.", null, null, none, nobody);

        var step = await _workflowService.GetCurrentWorkflowStepAsync(EntityType, entity.Id);
        var stageName = step?.StepName;
        var nextStage = await NextApprovalStageAsync(step, cancellationToken);

        if (!IsLineStageName(stageName))
            return callerMayApprove
                ? new DecisionStanding(stageName, false, null, DecidesAsApprover, null, none, nobody, nextStage)
                : new DecisionStanding(stageName, false,
                    $"This request is at the {stageName ?? "approval"} stage, and you are not one of its approvers.",
                    null, null, none, nobody, nextStage);

        // ── The line-manager stage ────────────────────────────────────────────────────────────────
        var line = await HrLineAuthority.GetLineAsync(_unitOfWork, entity.TenantId, entity.EmployeeId, cancellationToken);
        var lineApprovers = await HrLineAuthority.GetLineApproversAsync(
            line, _userManager.Users, entity.TenantId, StaffTravelApprovalLadder.LineApproverCount, cancellationToken);

        // Who the engine will accept among them: the stage was addressed to them when it began.
        var waitingFor = new List<HrLineApprover>();
        foreach (var approver in lineApprovers)
        {
            if (approver.UserId == userId
                    ? callerMayApprove
                    : await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, approver.UserId))
                waitingFor.Add(approver);
        }

        var traveller = await TravellerNameAsync(entity, cancellationToken);
        var me = _currentUserService.EmployeeId;
        var mine = me is Guid self ? line.FirstOrDefault(p => p.EmployeeId == self) : null;

        if (mine is not null)
        {
            if (callerMayApprove)
                return new DecisionStanding(stageName, true, null, DecidesAsLineAuthority, mine.Relation, line, waitingFor, nextStage);
            var sentTo = waitingFor.Count > 0
                ? $"It was sent to {NameList(waitingFor.Select(a => a.Name))} when it was submitted."
                : "It was sent elsewhere when it was submitted — your login may not have been active then.";
            return new DecisionStanding(stageName, true,
                $"You are {traveller}'s {mine.Relation}, but this stage is not waiting for you. {sentTo}",
                null, mine.Relation, line, waitingFor, nextStage);
        }

        var then = nextStage is null ? string.Empty : $" Then it goes to {nextStage}.";
        if (waitingFor.Count > 0)
            return new DecisionStanding(stageName, true,
                $"This stage is for {traveller}'s line manager — {NameList(waitingFor.Select(a => a.Name))}.{then}",
                null, null, line, waitingFor, nextStage);

        // Nobody in the line can decide: the travel desk does (D-7).
        if (!callerIsTravelDesk || !callerMayApprove)
            return new DecisionStanding(stageName, true,
                $"{traveller} has no line manager who can approve in the system, so the travel desk decides this stage.",
                null, null, line, waitingFor, nextStage);

        return new DecisionStanding(stageName, true, null, DecidesAsTravelDesk, null, line, waitingFor, nextStage);
    }

    /// <summary>The throwing form of <see cref="GetStandingAsync"/>, for the decision verbs.</summary>
    private async Task<DecisionStanding> RequireStandingAsync(
        StaffTravelRequest entity, bool callerIsTravelDesk, CancellationToken cancellationToken)
    {
        var standing = await GetStandingAsync(entity, callerIsTravelDesk, cancellationToken);
        if (!standing.MayDecide)
            throw new UnauthorizedAccessException(standing.Refusal ?? "You cannot decide this request.");
        return standing;
    }

    private async Task<string> TravellerNameAsync(StaffTravelRequest entity, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<Employee>().GetQueryable()
               .Where(e => e.TenantId == entity.TenantId && e.Id == entity.EmployeeId)
               .Select(e => (e.FirstName + " " + e.LastName).Trim())
               .FirstOrDefaultAsync(cancellationToken)
           ?? "The traveller";

    /// <summary>
    /// When the travel desk decides the line-manager stage, the request says why (D-7): an internal note
    /// in the decider's name — the traveller had no line authority who could approve.
    /// </summary>
    /// <remarks>Checked before the engine is asked, so a desk officer with no employee link is refused
    /// rather than leaving a decision with no note.</remarks>
    private Guid RequireDeskNoteAuthor(DecisionStanding standing)
    {
        if (standing.DecidesAs != DecidesAsTravelDesk) return Guid.Empty;
        return _currentUserService.EmployeeId is Guid author && author != Guid.Empty
            ? author
            : throw new InvalidOperationException(
                "Deciding the line manager's stage for the travel desk records the reason in your name, and this " +
                "account is not linked to an employee.");
    }

    private async Task AddDeskDecisionNoteAsync(
        StaffTravelRequest entity, DecisionStanding standing, Guid authorEmployeeId, string decision,
        Guid userId, CancellationToken cancellationToken)
    {
        if (standing.DecidesAs != DecidesAsTravelDesk) return;

        var traveller = await TravellerNameAsync(entity, cancellationToken);
        string why;
        if (standing.Line.Count == 0)
        {
            why = $"{traveller} has no supervisor recorded and no head of a unit above them.";
        }
        else
        {
            var people = standing.Line.Take(3)
                .Select(p => $"{p.Name} ({p.Relation})")
                .ToList();
            why = $"None of {traveller}'s line authorities could approve it in the system — {string.Join(", ", people)}" +
                  (standing.Line.Count > 3 ? " and others" : string.Empty) +
                  ": no active login, or the approval was not addressed to them.";
        }

        await _commentRepository.AddAsync(new StaffTravelRequestComment
        {
            TenantId = entity.TenantId,
            StaffTravelRequestId = entity.Id,
            AuthorId = authorEmployeeId,
            CommentType = TravelRequestCommentType.InternalNote,
            Body = $"{decision} at the line manager's stage by the travel desk. {why}",
            IsVisibleToTraveller = false,
            CreatedBy = userId.ToString(),
        });
    }

    // ---- The approver's door (lane 2, D-7, finding O-1) ------------------------

    /// <inheritdoc />
    /// <remarks>
    /// Leave's read door (<c>LeavesController.CanReadRequestAsync</c>) without its first arm, which the API
    /// applies: the traveller's line authority reads their people's trips, and whoever the request waits
    /// for reads it while it does. It is narrower than granting the Manager role the travel read
    /// permission, which would open every trip in the organisation to every manager, permanently.
    /// </remarks>
    public async Task<bool> CanOpenAsApproverAsync(Guid requestId, bool callerIsTravelDesk, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);

        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty
            && await HrLineAuthority.IsLineAuthorityAsync(_unitOfWork, entity.TenantId, entity.EmployeeId, me, cancellationToken))
            return true;

        return entity.Status == StaffTravelRequestStatus.Submitted
               && _currentUserService.EmployeeId != entity.EmployeeId
               && (await GetStandingAsync(entity, callerIsTravelDesk, cancellationToken, explain: false)).MayDecide;
    }

    /// <summary>
    /// T-45: an alert never reached the trip it warns about — nothing read its severity. The alerts in force at any point
    /// of the trip, for its destination country and its city (or the whole country), most severe first. Read narrowly.
    /// </summary>
    public async Task<IEnumerable<StaffTravelAlertSummaryDto>> GetDestinationAlertsAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await GetOwnedRequestAsync(requestId);
        var from = request.TravelStartDate.ToDateTime(TimeOnly.MinValue);
        var to = request.TravelEndDate.ToDateTime(TimeOnly.MaxValue);
        var city = request.DestinationCity?.Trim();
        var rows = await _unitOfWork.Repository<StaffTravelAlert>()
            .GetQueryable(a => a.TenantId == request.TenantId && !a.IsDeleted && a.IsActive
                            && a.CountryId == request.DestinationCountryId
                            && a.EffectiveFrom <= to && (a.EffectiveTo == null || a.EffectiveTo >= from))
            .Select(a => new { a.Id, a.AlertType, a.Severity, CountryName = a.Country.Name, a.City, a.Title, a.EffectiveFrom, a.IsActive, a.Body, a.EffectiveTo })
            .ToListAsync(cancellationToken);
        return rows
            .Where(a => string.IsNullOrWhiteSpace(a.City)
                        || string.Equals(a.City.Trim(), city, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Severity).ThenBy(a => a.EffectiveFrom)
            .Select(a => new StaffTravelAlertSummaryDto
            {
                Id = a.Id, AlertType = a.AlertType, Severity = a.Severity, CountryName = a.CountryName, City = a.City,
                Title = a.Title, EffectiveFrom = a.EffectiveFrom, IsActive = a.IsActive,
                Body = a.Body, EffectiveTo = a.EffectiveTo,
            })
            .ToList();
    }

    public async Task<StaffTravelViewerActionsDto> GetViewerActionsAsync(Guid requestId, bool callerIsTravelDesk, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);
        var actions = new StaffTravelViewerActionsDto { RequestId = entity.Id };
        if (entity.Status != StaffTravelRequestStatus.Submitted) return actions;

        var standing = await GetStandingAsync(entity, callerIsTravelDesk, cancellationToken);
        var isTraveller = _currentUserService.EmployeeId is Guid me && me == entity.EmployeeId;

        actions.StageName = standing.StageName;
        actions.IsLineStage = standing.IsLineStage;
        actions.IsFinalStage = standing.IsFinalStage;
        actions.NextStageName = standing.NextStageName;
        actions.CanDecide = standing.MayDecide && !isTraveller;
        actions.DecidesAs = actions.CanDecide ? standing.DecidesAs : null;
        actions.Relation = standing.Relation;
        actions.WaitingFor = standing.WaitingFor.Select(a => $"{a.Name} ({a.Relation})").ToList();
        actions.Reason = actions.CanDecide
            ? null
            : isTraveller ? "You cannot decide your own travel request." : standing.Refusal;
        return actions;
    }

    /// <summary>
    /// How many submitted requests the queue asks about. The engine answers one request at a time, so the
    /// candidates are bounded — leave's figure, several times any real travel backlog.
    /// </summary>
    private const int ApprovalQueueScanCap = 300;

    /// <remarks>
    /// Leave's <c>GetMyPendingApprovalsAsync</c> (closure plan L-10): the candidates are every submitted
    /// request in the tenant but the caller's own, and each is asked of the same rule the verbs run — the
    /// engine first, then the line rule — so the queue lists exactly what the caller can decide, and never
    /// a row that refuses on click.
    /// </remarks>
    public async Task<PagedResult<StaffTravelApprovalQueueItemDto>> GetMyPendingApprovalsAsync(
        int pageNumber, int pageSize, bool callerIsTravelDesk, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        RequireUserId();
        var me = _currentUserService.EmployeeId;

        var candidates = await _requestRepository.GetQueryable()
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => r.TenantId == tenantId
                     && !r.IsDeleted
                     && r.Status == StaffTravelRequestStatus.Submitted
                     && (me == null || r.EmployeeId != me))
            .OrderBy(r => r.SubmittedAt ?? r.CreatedAt)
            .Take(ApprovalQueueScanCap)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var mine = new List<StaffTravelApprovalQueueItemDto>();
        foreach (var request in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var standing = await GetStandingAsync(request, callerIsTravelDesk, cancellationToken, explain: false);
            if (!standing.MayDecide) continue;

            mine.Add(new StaffTravelApprovalQueueItemDto
            {
                Request = request.ToSummaryDto(),
                OriginCity = request.OriginCity,
                StageName = standing.StageName,
                IsLineStage = standing.IsLineStage,
                IsFinalStage = standing.IsFinalStage,
                DecidesAs = standing.DecidesAs!,
                Relation = standing.Relation,
                DaysWaiting = request.SubmittedAt is DateTime submitted ? Math.Max(0, (int)(now - submitted).TotalDays) : 0,
            });
        }

        var size = Math.Clamp(pageSize, 1, 100);
        var page = Math.Max(pageNumber, 1);
        return new PagedResult<StaffTravelApprovalQueueItemDto>
        {
            Items = mine.Skip((page - 1) * size).Take(size).ToList(),
            TotalCount = mine.Count,
            Page = page,
            PageSize = size,
        };
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
        await RequireNotProhibitedAsync(entity, "submitted", cancellationToken);
        // Lane 7 (D-39): the register read again — a passport may have been recorded since the trip was raised — and a
        // passport refused entry at the destination is not submitted.
        var visa = await StaffTravelComplianceRules.VisaVerdictAsync(
            _unitOfWork, entity.TenantId, entity.EmployeeId, entity.DestinationCountryId, cancellationToken);
        if (visa.Prohibited)
            throw new InvalidOperationException(
                $"The visa register records a {visa.PassportCountry} passport as refused entry to {visa.DestinationCountry}, so " +
                $"travel request {entity.RequestNumber} is not submitted. If the entry is wrong, correct the register first.");
        if (visa.Known && entity.VisaOverrideReason is null)
            entity.RequiresVisa = visa.Needs;
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
        await RequireWithinSingleTripLimitAsync(entity, caps, today, entity.EstimatedTotalCost, "estimated cost",
            "Reduce the estimate, or ask a travel administrator about the policy.", cancellationToken);
        entity.PolicyId = caps.PolicyId;

        var warnings = await ApprovedLeaveWarningsAsync(entity, traveller, cancellationToken);
        // Lane 7 (O-16): uninsured international days and a passport near expiry warn here; the ticket is what waits.
        warnings.AddRange(await StaffTravelComplianceRules.SubmissionWarningsAsync(_unitOfWork, entity, cancellationToken));

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

    public async Task<bool> ApproveAsync(ApproveStaffTravelRequestDto approveDto, bool callerIsTravelDesk, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(approveDto.RequestId);

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted requests can be approved.");

        var userId = RequireUserId();

        RequireNotTheTraveller(entity, "approve");
        // Lane 4 (C5): a destination assessed Prohibited after submission stops the approval too.
        await RequireNotProhibitedAsync(entity, "approved", cancellationToken);

        // Lane 2 (D-7): the stage decides who may — the traveller's line authority at the line manager's
        // stage, the travel desk there only when no line authority can, the engine's approvers after.
        var standing = await RequireStandingAsync(entity, callerIsTravelDesk, cancellationToken);
        var deskNoteAuthor = RequireDeskNoteAuthor(standing);

        // Lane 2 (O-9, T-10): the approved budget is the last approver's decision — HR's on the seeded route. It
        // used to be the estimate copied — no screen ever sent one — so the field recorded no decision. Sent at an
        // earlier stage it is refused, not kept: only the approval that approves the trip records it.
        if (approveDto.ApprovedBudget is decimal budget)
        {
            if (!standing.IsFinalStage)
                throw new InvalidOperationException(
                    $"The approved budget is set by the last approver. After this stage the request goes to " +
                    $"{standing.NextStageName} — approve without a budget.");
            if (budget <= 0m)
                throw new InvalidOperationException("The approved budget must be more than zero.");
            var caps = await _policyGuard.ResolveAsync(entity, cancellationToken);
            await RequireWithinSingleTripLimitAsync(entity, caps, DateOnly.FromDateTime(DateTime.UtcNow), budget,
                "approved budget", "Approve a lower budget, or ask a travel administrator about the policy.",
                cancellationToken);
        }

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
        {
            entity.ApprovedBudget = approveDto.ApprovedBudget ?? entity.EstimatedTotalCost;
            // Lane 1: the approver as a person — under the two-stage ladder, the one who gave the final
            // approval. The engine records a platform user; nothing on the trip said who approved it
            // (finding A10). Null for an approver with no employee link.
            entity.ApprovedById = _currentUserService.EmployeeId;
        }

        entity.UpdatedBy = userId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await AddDeskDecisionNoteAsync(entity, standing, deskNoteAuthor, "Approved", userId, cancellationToken);
        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel approval step processed: {RequestNumber}", entity.RequestNumber);

        // A multi-step definition leaves the request Submitted after an intermediate approval, so
        // only announce approval when the engine says it is approved.
        if (entity.Status == StaffTravelRequestStatus.Approved)
            await PublishLifecycleAsync(entity, "Approved", cancellationToken);

        return true;
    }

    public async Task<bool> RejectAsync(Guid requestId, Guid rejectedByUserId, string? reason, bool callerIsTravelDesk, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted requests can be rejected.");

        var userId = RequireUserId();

        RequireNotTheTraveller(entity, "reject");
        var standing = await RequireStandingAsync(entity, callerIsTravelDesk, cancellationToken);
        var deskNoteAuthor = RequireDeskNoteAuthor(standing);

        var decisionOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserProvider, EntityType, entity.Id, userId,
            "Reject", reason, "reject a travel request", HrPermissions.ApproveTravel);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, decisionOutcome, userId, reason);

        entity.UpdatedBy = userId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await AddDeskDecisionNoteAsync(entity, standing, deskNoteAuthor, "Rejected", userId, cancellationToken);
        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request rejected: {RequestNumber}", entity.RequestNumber);

        if (entity.Status == StaffTravelRequestStatus.Rejected)
            await PublishLifecycleAsync(entity, "Rejected", cancellationToken);

        return true;
    }

    /// <remarks>
    /// <para><b>Lane 1 (findings A2, O-11).</b> Cancelling a Submitted trip left its approval task live —
    /// the approver could still decide a cancelled request — so the engine's instance is cancelled first,
    /// and a refusal from the engine stops the cancel. A trip under way cannot be cancelled: it ended
    /// early or it did not, and Complete records that. An Approved trip with an advance whose cash is
    /// still out cannot be cancelled until the advance is settled — cancelling would leave money with
    /// the traveller against a trip that no longer exists. A rejected request is refused too: its rejection reason
    /// lives in <c>CancellationReason</c>, which a cancel would overwrite.</para>
    ///
    /// <para><b>Lane 5 (D-24).</b> A trip with a booking its supplier has confirmed or ticketed is not cancelled — each
    /// such booking is cancelled first, with the fee the supplier charges; the pending and on-hold ones are cancelled
    /// with the trip. (A company vehicle's Fleet trip is lane 6's.)</para>
    /// </remarks>
    public async Task<bool> CancelAsync(CancelStaffTravelRequestDto cancelDto, Guid cancelledByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(cancelDto.RequestId);
        await RequireCancellableAsync(entity, cancellationToken);

        var reason = cancelDto.CancellationReason.Trim();
        // Lane 6 (D-35): the drivers' own requests go with the trip — each checked BEFORE anything moves, since Fleet's
        // cancel below saves at once (G3).
        var drivers = await LiveDriverRequestsAsync(entity, cancellationToken);
        foreach (var driver in drivers)
            await RequireDriverCancellableAsync(driver, entity.RequestNumber, cancellationToken);
        // Lane 6 (D2): the company vehicles' undispatched fleet trips go with the trip — first, so a refusal from Fleet
        // stops the cancel before anything of travel's has moved. (Approved ones were refused above, as committed.)
        await _fleet.CancelForRequestAsync(entity.TenantId, entity.Id, $"Travel request {entity.RequestNumber} cancelled: {reason}", cancellationToken);
        foreach (var driver in drivers)
            await CancelAsync(new CancelStaffTravelRequestDto
            {
                RequestId = driver.Id,
                CancellationReason = Clip($"The trip {entity.RequestNumber} this driver's company vehicle served was cancelled: {reason}", 1000),
                CancelledById = cancelDto.CancelledById,
            }, cancelledByUserId, cancellationToken);
        await CancelLiveApprovalAsync(entity, $"Travel request cancelled: {reason}");

        entity.Status = StaffTravelRequestStatus.Cancelled;
        entity.CancellationReason = reason;
        // CancelledById is an Employee FK; UpdatedBy is a platform-user audit field. The one DTO
        // field was feeding BOTH, so whichever id the caller supplied was wrong for one of them.
        // They are separate now: the employee who cancelled, and the user account that acted.
        entity.CancelledById = cancelDto.CancelledById;
        entity.CancelledAt = DateTime.UtcNow;
        entity.UpdatedBy = cancelledByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        // Lane 3, N2: the trip's advances no money has left for are withdrawn with it. They stayed live on the
        // cancelled trip and could still be approved and paid out. (Cash out was refused above.)
        var undisbursed = await _unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == entity.TenantId && a.StaffTravelRequestId == entity.Id
                            && (a.Status == TravelAdvanceStatus.Requested || a.Status == TravelAdvanceStatus.Approved))
            .ToListAsync(cancellationToken);
        var advanceReason = $"The trip was cancelled: {reason}";
        if (advanceReason.Length > 1000) advanceReason = advanceReason[..1000];
        foreach (var advance in undisbursed)
            StaffTravelAdvanceRules.ApplyCancellation(advance, advanceReason, cancelDto.CancelledById, cancelledByUserId, DateTime.UtcNow);
        // Lane 5, D-24: the holds — pending or on hold, which no supplier committed to — go with the trip. They stayed
        // live, and the budget's Committed kept counting them (Q2).
        var heldCancelled = await StaffTravelBookingRules.CancelHoldsAsync(
            _unitOfWork, entity.TenantId, entity.Id, cancelledByUserId.ToString(), cancellationToken);
        // ...and the plan of a trip that will not happen is marked so (D-24's itinerary half, D-25).
        await StaffTravelItineraryRules.CancelWithTripAsync(
            _unitOfWork, entity.TenantId, entity.Id, cancelledByUserId.ToString(), cancellationToken);

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request cancelled: {RequestNumber} ({Held} held bookings cancelled with it)",
            entity.RequestNumber, heldCancelled);

        await PublishLifecycleAsync(entity, "Cancelled", cancellationToken);

        return true;
    }

    /// <summary>What refuses a cancel (lanes 1 and 5) — checked before anything moves.</summary>
    private async Task RequireCancellableAsync(StaffTravelRequest entity, CancellationToken cancellationToken)
    {
        if (entity.Status is StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Completed
            or StaffTravelRequestStatus.Closed or StaffTravelRequestStatus.Rejected)
            throw new InvalidOperationException($"A request that is {entity.Status} cannot be cancelled.");
        if (entity.Status == StaffTravelRequestStatus.InProgress)
            throw new InvalidOperationException(
                "A trip that is under way cannot be cancelled. If it ended early, mark it completed.");
        if (entity.Status == StaffTravelRequestStatus.Approved)
            await RequireNoAdvanceCashOutAsync(entity, "cancelling the trip", cancellationToken);
        // Lane 5, D-24: a booking a supplier has confirmed or ticketed is the organisation's commitment, cancelled on its
        // own with the fee the supplier charges — refused here BEFORE the approval is withdrawn.
        var committed = await StaffTravelBookingRules.CommittedBookingsAsync(
            _unitOfWork, entity.TenantId, entity.Id, cancellationToken);
        if (committed.Count > 0)
            throw new InvalidOperationException(
                $"Travel request {entity.RequestNumber} has bookings its suppliers have committed to — " +
                $"{string.Join("; ", committed)}. Cancel each on the Bookings tab first, recording what the supplier " +
                "charges, then cancel the trip.");
    }

    // ---- Lane 6, slice 6c: a company vehicle's driver travels on their own request (D-33…D-35) ----

    /// <summary>The live driver's requests a trip's company-vehicle legs keep — tracked, for the cascade (D-35).</summary>
    private async Task<List<StaffTravelRequest>> LiveDriverRequestsAsync(StaffTravelRequest trip, CancellationToken cancellationToken)
    {
        var ids = await _unitOfWork.Repository<StaffTravelGroundTransport>()
            .GetQueryable(g => g.TenantId == trip.TenantId && g.StaffTravelRequestId == trip.Id && !g.IsDeleted
                            && g.DriverTravelRequestId != null)
            .Select(g => g.DriverTravelRequestId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (ids.Count == 0) return new List<StaffTravelRequest>();
        return await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == trip.TenantId && !r.IsDeleted && ids.Contains(r.Id)
                            && r.Status != StaffTravelRequestStatus.Cancelled && r.Status != StaffTravelRequestStatus.Rejected
                            && r.Status != StaffTravelRequestStatus.Completed && r.Status != StaffTravelRequestStatus.Closed)
            .ToListAsync(cancellationToken);
    }

    /// <summary>A driver's request goes with its leg (D-35) — so whatever would refuse its cancel refuses the leg's.</summary>
    private async Task RequireDriverCancellableAsync(StaffTravelRequest driver, string tripNumber, CancellationToken cancellationToken)
    {
        try
        {
            await RequireCancellableAsync(driver, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"The driver's own request {driver.RequestNumber}, raised for {tripNumber}'s company vehicle, goes with it but " +
                $"cannot be cancelled: {ex.Message}");
        }
    }

    private async Task<StaffTravelRequest?> LiveDriverRequestAsync(Guid? driverRequestId, CancellationToken cancellationToken)
    {
        if (driverRequestId is not Guid id) return null;
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted
                            && r.Status != StaffTravelRequestStatus.Cancelled && r.Status != StaffTravelRequestStatus.Rejected
                            && r.Status != StaffTravelRequestStatus.Completed && r.Status != StaffTravelRequestStatus.Closed)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RequireDriverRequestCancellableAsync(Guid? driverRequestId, string tripNumber, CancellationToken cancellationToken = default)
    {
        if (await LiveDriverRequestAsync(driverRequestId, cancellationToken) is { } driver)
            await RequireDriverCancellableAsync(driver, tripNumber, cancellationToken);
    }

    public async Task CancelDriverRequestAsync(
        Guid? driverRequestId, string reason, Guid cancelledById, Guid cancelledByUserId, CancellationToken cancellationToken = default)
    {
        if (await LiveDriverRequestAsync(driverRequestId, cancellationToken) is not { } driver) return;
        await CancelAsync(new CancelStaffTravelRequestDto
        {
            RequestId = driver.Id,
            CancellationReason = Clip(reason, 1000),
            CancelledById = cancelledById,
        }, cancelledByUserId, cancellationToken);
    }

    private static string Clip(string text, int max) => text.Length > max ? text[..max] : text;

    public async Task<bool> MarkCompletedAsync(Guid requestId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);

        if (entity.Status is not (StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress))
            throw new InvalidOperationException("Only approved or in-progress requests can be marked completed.");
        // Lane 1, finding O-11: a trip cannot have happened before it began.
        if (DateOnly.FromDateTime(DateTime.UtcNow) < entity.TravelStartDate)
            throw new InvalidOperationException(
                $"A trip cannot be marked completed before it starts ({entity.TravelStartDate:dd MMM yyyy}).");

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

    // ---- The lifecycle's other writers -------------------------------------
    //
    // Travel final closure, lane 1 (decisions D-6 and D-9, finding A12). ReturnedForRevision and
    // Closed were statuses nothing wrote, a trip could not be changed after approval, and the request
    // had no recall of its own — "use all four or none" (HrWorkflowFallbackAuthority) had three. Who
    // hears about each is lane 8's (D-4); none of these publishes yet.

    /// <summary>Cancels the request's live approval instance, if it has one; a refusal stops the caller.</summary>
    /// <remarks>
    /// Asked of the record, not of the definition: a request submitted while no definition was published
    /// has no instance even after one is, and the engine answers "No active workflow found" for it.
    /// </remarks>
    private async Task CancelLiveApprovalAsync(StaffTravelRequest entity, string reason)
    {
        if (!await _workflowIntegrationService.HasActiveApprovalInstanceAsync(EntityType, entity.Id)) return;

        var result = await _workflowIntegrationService.CancelWorkflowAsync(EntityType, entity.Id, reason);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? "The approval workflow could not be cancelled.");
    }

    /// <summary>An advance whose cash is still with the traveller — paid out and not settled — blocks the caller.</summary>
    /// <remarks>"Cash out" is <see cref="StaffTravelAdvanceRules.CashOut"/> (lane 3), shared with claim recovery, the
    /// separation clearance and the sweep.</remarks>
    private async Task RequireNoAdvanceCashOutAsync(
        StaffTravelRequest entity, string action, CancellationToken cancellationToken)
    {
        var outstanding = await _unitOfWork.Repository<StaffTravelAdvance>().GetQueryable()
            .Where(a => a.TenantId == entity.TenantId && a.StaffTravelRequestId == entity.Id)
            .Where(StaffTravelAdvanceRules.CashOut)
            .OrderBy(a => a.AdvanceNumber)
            .Select(a => new { a.AdvanceNumber, a.CurrencyCode, a.UnsettledAmount })
            .FirstOrDefaultAsync(cancellationToken);

        if (outstanding is not null)
            throw new InvalidOperationException(
                $"Advance {outstanding.AdvanceNumber} still has {outstanding.CurrencyCode} {outstanding.UnsettledAmount:N2} " +
                $"with the traveller. Settle it first — through an expense claim, by recording the cash handed back, or by " +
                $"writing it off — before {action}.");
    }

    /// <summary>An approver sends a submitted request back to its requester to change (D-6).</summary>
    /// <remarks>
    /// Leave's suggest-changes shape (<c>LeaveService.SuggestChangesAsync</c>): the decider is checked
    /// first, then the live approval is cancelled — a fresh one starts when the request is submitted
    /// again — and the status is set here, because the engine has no "returned" outcome for the adapter
    /// to apply. Since lane 2 the decider is whoever may approve at the stage it is on (D-7).
    /// </remarks>
    public async Task<bool> ReturnForRevisionAsync(Guid requestId, string reason, bool callerIsTravelDesk, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            throw new InvalidOperationException("Only a request out for approval can be returned for revision.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Say what needs to change — the reason goes back with the request.");

        var userId = RequireUserId();
        RequireNotTheTraveller(entity, "return");
        var standing = await RequireStandingAsync(entity, callerIsTravelDesk, cancellationToken);
        var deskNoteAuthor = RequireDeskNoteAuthor(standing);

        var trimmed = reason.Trim();
        await CancelLiveApprovalAsync(entity, $"Returned for revision: {trimmed}");
        await AddDeskDecisionNoteAsync(entity, standing, deskNoteAuthor, "Returned for revision", userId, cancellationToken);

        entity.Status = StaffTravelRequestStatus.ReturnedForRevision;
        entity.ReturnedAt = DateTime.UtcNow;
        entity.ReturnedById = _currentUserService.EmployeeId;
        entity.ReturnReason = trimmed;
        // Back with the requester: no longer submitted, and the policy is checked again when it is.
        entity.SubmittedAt = null;
        entity.PolicyId = null;
        entity.UpdatedBy = userId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request returned for revision: {RequestNumber}", entity.RequestNumber);
        return true;
    }

    /// <summary>
    /// Asks for a change to an approved trip: it goes back to the requester and is approved again (D-9).
    /// </summary>
    /// <remarks>
    /// Bookings, advances and claims stay linked. The approval's stamps stay as the record of what is
    /// being changed until the next approval replaces them. Not once the trip is under way. The engine's
    /// instance is already complete, so there is nothing to cancel; resubmission starts a new one.
    /// Lane 2 lets the approver ask too.
    /// </remarks>
    public async Task<bool> RequestChangeAsync(Guid requestId, string reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);

        if (entity.Status == StaffTravelRequestStatus.InProgress)
            throw new InvalidOperationException(
                "A trip that is under way cannot be sent back for a change. If it ended early, mark it completed.");
        if (entity.Status != StaffTravelRequestStatus.Approved)
            throw new InvalidOperationException(
                "Only an approved trip can be sent back for a change. A draft or a returned request can be edited as it is.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Say what has changed — the trip is approved again on it.");

        var userId = RequireUserId();
        // Lane 6 (D-35): the drivers' own requests go with the vehicles — checked before Fleet's cancel saves (G3).
        var drivers = await LiveDriverRequestsAsync(entity, cancellationToken);
        foreach (var driver in drivers)
            await RequireDriverCancellableAsync(driver, entity.RequestNumber, cancellationToken);
        if (drivers.Count > 0 && _currentUserService.EmployeeId is null)
            throw new UnauthorizedAccessException(
                "Sending back a trip whose drivers' requests go with it needs a login linked to an employee record.");
        // Lane 6 (FX-3): the trip goes back for re-approval, so its vehicles are not held meanwhile — the undispatched
        // fleet trips are cancelled (the legs stay, cancelled, as the record), and are booked again once approved.
        await _fleet.CancelForRequestAsync(entity.TenantId, entity.Id,
            $"Travel request {entity.RequestNumber} sent back for a change: {reason.Trim()}", cancellationToken);
        foreach (var driver in drivers)
            await CancelAsync(new CancelStaffTravelRequestDto
            {
                RequestId = driver.Id,
                CancellationReason = Clip($"The trip {entity.RequestNumber} this driver's company vehicle served was sent back for a change: {reason.Trim()}", 1000),
                CancelledById = _currentUserService.EmployeeId!.Value,
            }, userId, cancellationToken);
        entity.Status = StaffTravelRequestStatus.ReturnedForRevision;
        entity.ChangeRequestedAt = DateTime.UtcNow;
        entity.ChangeRequestedById = _currentUserService.EmployeeId;
        entity.ChangeReason = reason.Trim();
        entity.SubmittedAt = null;
        entity.PolicyId = null;
        entity.UpdatedBy = userId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Change requested on approved staff travel request: {RequestNumber}", entity.RequestNumber);
        return true;
    }

    /// <summary>Withdraws a request from approval, back to Draft — the requester's own correction (A12).</summary>
    /// <remarks>
    /// <para><b>Who:</b> the traveller, or whoever raised the request. The rule is here because the
    /// helper does not enforce one without a workflow instance; with an instance the engine also insists
    /// on the login that submitted it, so a trip the desk submitted is the desk's to recall and the
    /// traveller is told to ask them.</para>
    ///
    /// <para>The adapter returns the request to Draft whatever the helper reports: with no definition
    /// published there is nothing for the engine to do, and the record still has to come back.</para>
    /// </remarks>
    public async Task<bool> RecallAsync(Guid requestId, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            throw new InvalidOperationException("Only a request out for approval can be recalled.");

        var me = _currentUserService.EmployeeId;
        if (me is null || (me != entity.EmployeeId && me != entity.InitiatedById))
            throw new UnauthorizedAccessException("Only the traveller, or whoever raised the request, can recall it.");

        var userId = RequireUserId();
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        try
        {
            await HrWorkflowFallbackAuthority.RecallAsync(_workflowIntegrationService, EntityType, entity.Id, userId, trimmed);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Only the requester", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only whoever submitted the request can recall it from approval. Ask them, or ask the approver " +
                "to return it for revision.");
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType).ApplyRecallOutcome(entity, userId, trimmed);
        entity.PolicyId = null;
        entity.UpdatedBy = userId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request recalled: {RequestNumber}", entity.RequestNumber);
        return true;
    }

    /// <summary>Closes a completed trip once nothing is left to settle (D-6) — HR's verb; lane 8's sweep closes too.</summary>
    /// <remarks>
    /// Every claim paid or rejected and every advance settled, written off, rejected or cancelled — the
    /// rule the sweep will apply. A trip closed with a claim in flight would leave that claim to be paid
    /// against a trip everything else treats as finished; once closed, nothing more can be booked,
    /// budgeted, advanced or claimed on it (<see cref="StaffTravelRequestGuards"/>).
    /// </remarks>
    public async Task<bool> CloseAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequestAsync(requestId);

        if (entity.Status != StaffTravelRequestStatus.Completed)
            throw new InvalidOperationException(entity.Status == StaffTravelRequestStatus.Closed
                ? "This trip is already closed."
                : "Only a completed trip can be closed. Mark it completed first.");

        var openClaim = await _unitOfWork.Repository<StaffTravelExpenseClaim>().GetQueryable()
            .Where(c => c.TenantId == entity.TenantId
                     && c.StaffTravelRequestId == entity.Id
                     && c.Status != TravelClaimStatus.Paid
                     && c.Status != TravelClaimStatus.Rejected)
            .OrderBy(c => c.ClaimNumber)
            .Select(c => new { c.ClaimNumber, c.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (openClaim is not null)
            throw new InvalidOperationException(
                $"Expense claim {openClaim.ClaimNumber} is {openClaim.Status}. A trip closes once every claim on it is paid or rejected.");

        var openAdvance = await _unitOfWork.Repository<StaffTravelAdvance>().GetQueryable()
            .Where(a => a.TenantId == entity.TenantId
                     && a.StaffTravelRequestId == entity.Id
                     && a.Status != TravelAdvanceStatus.FullySettled
                     && a.Status != TravelAdvanceStatus.WrittenOff
                     && a.Status != TravelAdvanceStatus.Rejected
                     && a.Status != TravelAdvanceStatus.Cancelled)
            .OrderBy(a => a.AdvanceNumber)
            .Select(a => new { a.AdvanceNumber, a.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (openAdvance is not null)
            throw new InvalidOperationException(
                $"Advance {openAdvance.AdvanceNumber} is {openAdvance.Status}. A trip closes once every advance on it is " +
                "settled, written off, rejected or cancelled.");

        var userId = RequireUserId();
        entity.Status = StaffTravelRequestStatus.Closed;
        entity.ClosedAt = DateTime.UtcNow;
        entity.ClosedById = _currentUserService.EmployeeId;
        entity.UpdatedBy = userId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request closed: {RequestNumber}", entity.RequestNumber);
        return true;
    }

    // ---- Comments ----------------------------------------------------------

    public async Task<StaffTravelRequestCommentDto> AddCommentAsync(CreateStaffTravelRequestCommentDto createDto, Guid tenantId, Guid createdByUserId, Guid authorEmployeeId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // The parent request was never checked, so a comment could be hung off any request id at
        // all — including another tenant's. GetOwnedRequestAsync raises "not found" for both.
        await GetOwnedRequestAsync(createDto.StaffTravelRequestId);

        // Lane 7 (7c2, P4): nor was a reply's parent — a reply could hang off another trip's comment. It answers a comment
        // on the same trip, or it is "not found".
        if (createDto.ParentCommentId is Guid parentId)
        {
            var parent = await _commentRepository.GetByIdAsync(parentId);
            if (parent is null || parent.TenantId != tenantId || parent.StaffTravelRequestId != createDto.StaffTravelRequestId)
                throw new ArgumentException($"Comment with ID '{parentId}' not found on this travel request.");
        }

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

    /// <summary>
    /// Only a comment's author, or a travel administrator, may remove it (lane 1, finding A10) — any desk officer
    /// could rewrite or delete what a colleague had said on a trip. Editing is the author's alone since lane 4
    /// (D-21; see <see cref="UpdateCommentAsync"/>).
    /// </summary>
    private void RequireCommentAuthorOrAdmin(StaffTravelRequestComment comment, bool callerIsTravelAdmin, string verb)
    {
        if (callerIsTravelAdmin) return;
        if (_currentUserService.EmployeeId is Guid me && me == comment.AuthorId) return;
        throw new UnauthorizedAccessException($"Only the comment's author, or a travel administrator, can {verb} it.");
    }

    /// <summary>
    /// Changes a comment — <b>its author's alone</b> since lane 4 (D-21). The administrator's override stays on delete
    /// (moderation) but left edit: once the HR desk holds <c>HR.Travel.Admin</c> (D-3) it would have let any desk
    /// officer reword what a colleague had said, which is what A10 closed. <paramref name="callerIsTravelAdmin"/> is
    /// kept on the signature and no longer widens anything here.
    /// </summary>
    public async Task<StaffTravelRequestCommentDto> UpdateCommentAsync(UpdateStaffTravelRequestCommentDto updateDto, Guid updatedByUserId, bool callerIsTravelAdmin, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommentAsync(updateDto.Id);
        if (_currentUserService.EmployeeId is not Guid me || me != entity.AuthorId)
            throw new UnauthorizedAccessException(
                "Only the comment's author can edit it. A travel administrator can remove it, and say what is meant in a new comment.");
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _commentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _commentRepository.GetWithAuthorAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, bool callerIsTravelAdmin, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommentAsync(commentId);
        RequireCommentAuthorOrAdmin(entity, callerIsTravelAdmin, "delete");
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

    // ---- The traveller's own acts on the portal (lane 7, slice 7c2) --------
    //
    // The /me controller has already resolved the request as the caller's; each of these checks it again, so a rule here
    // never depends on a controller remembering to. Someone else's request is "not found", as everywhere on the portal.

    private async Task<StaffTravelRequest> GetTravellersRequestAsync(Guid requestId, Guid travellerEmployeeId)
    {
        var request = await GetOwnedRequestAsync(requestId);
        if (request.EmployeeId != travellerEmployeeId)
            throw new ArgumentException($"Staff travel request with ID '{requestId}' not found.");
        return request;
    }

    public async Task RequireTravellerMayAttachAsync(Guid requestId, Guid travellerEmployeeId, CancellationToken cancellationToken = default)
    {
        var request = await GetTravellersRequestAsync(requestId, travellerEmployeeId);
        if (request.Status is StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Rejected or StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException(
                $"This trip is {request.Status.ToString().ToLowerInvariant()}, so no file is added to it here — ask the travel desk if one is needed.");
    }

    /// <summary>
    /// D-40: the traveller removes a file they uploaded, while the trip is still theirs to change (a draft, or returned to
    /// them). Once it is out for approval or approved, the desk may be relying on it.
    /// </summary>
    public async Task<bool> DeleteTravellerAttachmentAsync(Guid attachmentId, Guid travellerEmployeeId, CancellationToken cancellationToken = default)
    {
        var attachment = await GetOwnedAttachmentAsync(attachmentId);
        var request = await GetOwnedRequestAsync(attachment.StaffTravelRequestId);
        if (request.EmployeeId != travellerEmployeeId)
            throw new ArgumentException($"Attachment with ID '{attachmentId}' not found.");
        if (attachment.UploadedById != travellerEmployeeId)
            throw new InvalidOperationException("The travel desk added this file, so it is theirs to remove — ask them.");
        if (request.Status is not (StaffTravelRequestStatus.Draft or StaffTravelRequestStatus.ReturnedForRevision))
            throw new InvalidOperationException(
                "This trip has been sent for approval, so the travel desk may be relying on this file — ask them to remove it.");
        await _attachmentRepository.DeleteAsync(attachment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// D-41: the traveller writes to the travel desk — a reply to a note the desk shared with them, or a question of their
    /// own. Always visible to the traveller; the portal has no edit or delete, so what was said stands.
    /// </summary>
    public async Task<StaffTravelRequestCommentDto> AddTravellerCommentAsync(Guid requestId, string body, Guid? parentCommentId, Guid tenantId, Guid createdByUserId, Guid travellerEmployeeId, CancellationToken cancellationToken = default)
    {
        var request = await GetTravellersRequestAsync(requestId, travellerEmployeeId);
        var text = body?.Trim();
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException("Write the message.");
        if (parentCommentId is Guid parentId)
        {
            // A note the traveller cannot see is not one they can answer: the same "not found" as one that does not exist.
            var parent = await _commentRepository.GetByIdAsync(parentId);
            if (parent is null || parent.TenantId != request.TenantId || parent.StaffTravelRequestId != requestId
                || !parent.IsVisibleToTraveller)
                throw new ArgumentException($"Comment with ID '{parentId}' not found.");
        }

        return await AddCommentAsync(new CreateStaffTravelRequestCommentDto
        {
            StaffTravelRequestId = requestId,
            CommentType = parentCommentId is null ? TravelRequestCommentType.Query : TravelRequestCommentType.Response,
            Body = text,
            IsVisibleToTraveller = true,
            ParentCommentId = parentCommentId,
        }, tenantId, createdByUserId, travellerEmployeeId, cancellationToken);
    }

    // ---- Group travel ------------------------------------------------------
    //
    // Lane 1, slice 1c (findings A11, T-30, T-31, T-32, O-18). The group's status was whatever its PUT
    // said, MaxParticipants was a label, a new destination or new dates reached nobody, an existing
    // request could not join, a cancelled traveller held a place for ever, and deleting a group left
    // its travellers pointing at a deleted row.
    //
    // ⚠ The group is read WITH its travellers (GetWithRequestsAsync) wherever they are checked or
    // moved, and saved through change tracking — never GenericRepository.UpdateAsync, whose
    // DbSet.Update would repaint every loaded traveller and employee as modified.

    /// <summary>A group takes travellers while it is being planned or is open.</summary>
    private static void RequireAcceptsTravellers(StaffGroupTravel group)
    {
        if (group.Status is GroupTravelStatus.Planning or GroupTravelStatus.Open) return;

        throw new InvalidOperationException(group.Status == GroupTravelStatus.Closed
            ? $"The group '{group.GroupName}' is closed to new travellers. Reopen it to add someone."
            : $"The group '{group.GroupName}' is {group.Status} and takes no new travellers.");
    }

    /// <summary><c>MaxParticipants</c> was a label (T-31); a cancelled or rejected trip holds no place.</summary>
    private static void RequireRoomFor(StaffGroupTravel group, int joining)
    {
        var taken = group.SeatsTaken();
        if (group.MaxParticipants is int max && taken + joining > max)
            throw new InvalidOperationException(
                $"The group '{group.GroupName}' takes at most {max} traveller(s) and has {taken}, so there is " +
                $"no room for {joining} more.");
    }

    /// <summary>The group's own foreign keys and dates, checked like a trip's.</summary>
    private async Task RequireGroupFactsAsync(
        Guid tenantId, Guid leadEmployeeId, Guid destinationCountryId, DateOnly start, DateOnly end,
        CancellationToken cancellationToken)
    {
        await RequireTravellerAsync(tenantId, leadEmployeeId, requireActive: true, cancellationToken);
        await RequireKnownCountriesAsync(tenantId, destinationCountryId, destinationCountryId, cancellationToken);
        RequireDatesInOrder(start, end);
    }

    /// <summary>Gives a traveller's trip the group's destination and dates (T-32).</summary>
    private static void AlignToGroup(StaffTravelRequest request, StaffGroupTravel group, Guid userId)
    {
        request.DestinationCountryId = group.DestinationCountryId;
        request.DestinationCity = group.DestinationCity;
        request.TravelStartDate = group.TravelStartDate;
        request.TravelEndDate = group.TravelEndDate;
        request.EstimatedDurationDays = Math.Max(0, group.TravelEndDate.DayNumber - group.TravelStartDate.DayNumber + 1);
        request.IsInternational = request.OriginCountryId != request.DestinationCountryId;
        request.UpdatedBy = userId.ToString();
        request.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<StaffGroupTravel> GetOwnedGroupWithRequestsAsync(Guid id)
    {
        var group = await _groupTravelRepository.GetWithRequestsAsync(id);
        if (group == null || group.TenantId != GetTenantId())
            throw new ArgumentException($"Group travel with ID '{id}' not found.");
        return group;
    }

    private async Task<StaffGroupTravelDto> ReadGroupAsync(Guid id)
    {
        var refreshed = await _groupTravelRepository.GetWithRequestsAsync(id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Group travel with ID '{id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<StaffGroupTravelDto> CreateGroupTravelAsync(CreateStaffGroupTravelDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireGroupFactsAsync(tenantId, createDto.LeadEmployeeId, createDto.DestinationCountryId,
            createDto.TravelStartDate, createDto.TravelEndDate, cancellationToken);
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
    /// <para>⚠ The re-read is not decoration: a group read without its navigations maps with a blank
    /// lead, no destination country and an empty traveller list.</para>
    ///
    /// <para><b>Slice 1c.</b> No status from the payload (the verbs below move it); not on a cancelled
    /// or completed group; the limit not below the places already taken; and a new destination or new
    /// dates are given to every traveller whose trip can still change — a draft, or a request returned
    /// for revision. A submitted or approved trip keeps its own (the group's page marks it).</para>
    /// </remarks>
    public async Task<StaffGroupTravelDto> UpdateGroupTravelAsync(UpdateStaffGroupTravelDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedGroupWithRequestsAsync(updateDto.Id);

        if (entity.Status is GroupTravelStatus.Cancelled or GroupTravelStatus.Completed)
            throw new InvalidOperationException($"A group trip that is {entity.Status} cannot be changed.");
        await RequireGroupFactsAsync(tenantId, updateDto.LeadEmployeeId, updateDto.DestinationCountryId,
            updateDto.TravelStartDate, updateDto.TravelEndDate, cancellationToken);
        if (updateDto.MaxParticipants is int max && max < entity.SeatsTaken())
            throw new InvalidOperationException(
                $"The group already has {entity.SeatsTaken()} traveller(s), so its limit cannot be set below that.");

        var moved = entity.DestinationCountryId != updateDto.DestinationCountryId
                    || !string.Equals(entity.DestinationCity, updateDto.DestinationCity, StringComparison.Ordinal)
                    || entity.TravelStartDate != updateDto.TravelStartDate
                    || entity.TravelEndDate != updateDto.TravelEndDate;

        entity.UpdateEntity(updateDto, updatedByUserId);
        if (moved)
        {
            foreach (var request in entity.Requests.Where(r => r.Status is StaffTravelRequestStatus.Draft
                                                                        or StaffTravelRequestStatus.ReturnedForRevision))
                AlignToGroup(request, entity, updatedByUserId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadGroupAsync(entity.Id);
    }

    /// <remarks>
    /// Slice 1c (O-18): the travellers come off the group first. They kept the deleted group's id, so
    /// their trips pointed at a row nothing could read. Their trips carry on as ordinary ones.
    /// </remarks>
    public async Task<bool> DeleteGroupTravelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroupWithRequestsAsync(id);
        var userId = RequireUserId();

        foreach (var request in entity.Requests.ToList())
        {
            request.GroupTravelId = null;
            request.UpdatedBy = userId.ToString();
            request.UpdatedAt = DateTime.UtcNow;
        }
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Opens a group to travellers — from Planning, or reopens a closed one (slice 1c).</summary>
    public async Task<StaffGroupTravelDto> OpenGroupTravelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await GetOwnedGroupWithRequestsAsync(id);
        if (group.Status is not (GroupTravelStatus.Planning or GroupTravelStatus.Closed))
            throw new InvalidOperationException($"A group trip that is {group.Status} cannot be opened.");

        return await SetGroupStatusAsync(group, GroupTravelStatus.Open, cancellationToken);
    }

    /// <summary>Closes a group to new travellers (slice 1c). Its travellers' trips carry on.</summary>
    public async Task<StaffGroupTravelDto> CloseGroupTravelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await GetOwnedGroupWithRequestsAsync(id);
        if (group.Status is not (GroupTravelStatus.Planning or GroupTravelStatus.Open))
            throw new InvalidOperationException($"A group trip that is {group.Status} cannot be closed.");

        return await SetGroupStatusAsync(group, GroupTravelStatus.Closed, cancellationToken);
    }

    /// <summary>
    /// Calls a group trip off (slice 1c) — once none of its travellers has a trip still going ahead.
    /// </summary>
    /// <remarks>
    /// Each traveller's trip is its own request, with its own approval and possibly its own money, and
    /// cancelling one has rules (no cash out, not under way). So the group does not cancel them for the
    /// desk: it names the trips still live, and the desk cancels them or takes the travellers off first.
    /// </remarks>
    public async Task<StaffGroupTravelDto> CancelGroupTravelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await GetOwnedGroupWithRequestsAsync(id);
        if (group.Status is GroupTravelStatus.Cancelled or GroupTravelStatus.Completed)
            throw new InvalidOperationException($"A group trip that is {group.Status} cannot be cancelled.");

        var live = group.Requests
            .Where(r => r.Status is StaffTravelRequestStatus.Draft or StaffTravelRequestStatus.Submitted
                or StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.ReturnedForRevision
                or StaffTravelRequestStatus.InProgress)
            .OrderBy(r => r.RequestNumber)
            .Select(r => r.RequestNumber)
            .ToList();
        if (live.Count > 0)
            throw new InvalidOperationException(
                $"{live.Count} traveller(s) on '{group.GroupName}' still have trips going ahead " +
                $"({string.Join(", ", live.Take(5))}{(live.Count > 5 ? ", …" : string.Empty)}). Cancel those trips, " +
                "or take the travellers off the group, before cancelling it.");

        return await SetGroupStatusAsync(group, GroupTravelStatus.Cancelled, cancellationToken);
    }

    private async Task<StaffGroupTravelDto> SetGroupStatusAsync(
        StaffGroupTravel group, GroupTravelStatus status, CancellationToken cancellationToken)
    {
        group.Status = status;
        group.UpdatedBy = RequireUserId().ToString();
        group.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Group travel {GroupName} is now {Status}", group.GroupName, status);
        return await ReadGroupAsync(group.Id);
    }

    /// <summary>
    /// Puts an existing request on a group (T-30 — the only door used to create a new one): a draft or
    /// a request returned for revision, of a traveller not already on the group, while the group takes
    /// travellers and has room. The trip takes the group's destination and dates.
    /// </summary>
    public async Task<StaffGroupTravelDto> LinkGroupParticipantAsync(Guid groupTravelId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var group = await GetOwnedGroupWithRequestsAsync(groupTravelId);
        RequireAcceptsTravellers(group);

        var request = await GetOwnedRequestAsync(requestId);
        if (request.GroupTravelId == group.Id)
            throw new InvalidOperationException($"{request.RequestNumber} is already on this group.");
        if (request.GroupTravelId is not null)
            throw new InvalidOperationException(
                $"{request.RequestNumber} is on another group trip. Take it off that group first.");
        if (request.Status is not (StaffTravelRequestStatus.Draft or StaffTravelRequestStatus.ReturnedForRevision))
            throw new InvalidOperationException(
                $"Only a draft, or a request returned for revision, can join a group — {request.RequestNumber} is " +
                $"{request.Status}. Joining gives it the group's destination and dates, which a submitted or " +
                "approved trip cannot take.");
        if (group.Requests.Any(r => r.EmployeeId == request.EmployeeId
                                    && r.Status is not (StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Rejected)))
            throw new InvalidOperationException("The traveller is already on this group with another trip.");
        RequireRoomFor(group, 1);

        var userId = RequireUserId();
        request.GroupTravelId = group.Id;
        AlignToGroup(request, group, userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Request {RequestNumber} linked to group travel {GroupName}", request.RequestNumber, group.GroupName);
        return await ReadGroupAsync(group.Id);
    }

    public async Task<StaffGroupTravelDto> AddGroupParticipantsAsync(AddGroupTravelParticipantsDto dto, Guid tenantId, Guid createdByUserId, Guid initiatorEmployeeId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var group = await _groupTravelRepository.GetWithRequestsAsync(dto.GroupTravelId);
        if (group == null || group.TenantId != tenantId)
            throw new ArgumentException($"Group travel with ID '{dto.GroupTravelId}' not found.");
        RequireAcceptsTravellers(group);

        // Skip employees already holding a place on the group — not one whose trip was cancelled or
        // rejected, who can be added again.
        var existing = group.Requests
            .Where(r => r.Status is not (StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Rejected))
            .Select(r => r.EmployeeId)
            .ToHashSet();
        var toAdd = dto.EmployeeIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Where(id => !existing.Contains(id))
            .ToList();
        RequireRoomFor(group, toAdd.Count);

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
            };

            var entity = createDto.ToEntity(tenantId, createdByUserId);
            entity.GroupTravelId = group.Id;
            // Each participant's own unit — the template's single unit stamped the whole group with one.
            ApplyServerFacts(entity, traveller);
            // ...and their own passport's answer from the visa register (lane 7, D-39).
            await ApplyVisaRegisterAsync(entity, dto.RequiresVisa, null, cancellationToken);
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
