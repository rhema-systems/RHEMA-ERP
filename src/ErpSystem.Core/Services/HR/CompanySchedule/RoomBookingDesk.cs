using System.Globalization;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.CompanySchedule;

/// <summary>
/// What a room booking's outcomes share across the three services that reach one — the booking service, the room
/// service (retiring a room) and the event service (an event cancelled, rejected or moved with its bookings) — in
/// company-schedule final closure lane 3b-1: the booking's approval on the workflow engine (D-10), and telling its booker
/// (F-34; the user's ruling, every outcome, in the app and by email, never of their own act).
/// </summary>
/// <remarks>
/// <para><b>The engine, as for events (lane 2b).</b> A booking of a room that needs approval is Tentative from creation
/// and its approval starts then; a confirmed booking that moves waits again, with a fresh approval. ⚠ With no published
/// definition the engine answers Approved: <see cref="HrWorkflowFallbackAuthority.SubmitAsync"/> turns that into Pending,
/// and the decision falls to <c>HR.Company.Approve</c>.</para>
///
/// <para><b>Never fails the act.</b> Every caller has saved first; an approval that cannot start, a withdrawal or a
/// notice that fails is logged, not thrown.</para>
///
/// <para><b>A series' dates (lane 3d-1, the user's rulings).</b> The dates of a series booked together — the same booker's
/// bookings of the same room for occurrences of the same series — are <b>approved once</b>: the first asks, and its
/// decision covers every date still waiting with no approval of its own under way (<see cref="SharingSetAsync"/>);
/// deciding another is refused, naming the one that carries it; when the carrier is cancelled, deleted or lapses the
/// approval passes on (<see cref="PassApprovalOnAsync"/>). There is no set table: the set is read from the bookings and
/// their events, as an event series is read from its occurrences. And the booker is <b>told once per act</b>, listing the
/// bookings, when one act approves, does not approve or cancels several of them.</para>
/// </remarks>
public sealed class RoomBookingDesk
{
    /// <summary>The workflow engine's entity type — catalogued, displayed and given a context like CompanyEvent's.</summary>
    public const string WorkflowEntityType = "RoomBooking";

    private readonly IWorkflowIntegrationService _workflow;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly CompanyScheduleNotices _notices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<RoomBookingDesk> _logger;
    private readonly IWorkflowEngine _engine;
    // Lane 3d-1: passing a set's approval on with nobody signed in (the hourly lapse), in a named login's name.
    private readonly IWorkflowService _workflowService;

    public RoomBookingDesk(
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        ITemplatedEmailService templatedEmail,
        CompanyScheduleNotices notices,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<RoomBookingDesk> logger,
        IWorkflowEngine engine,
        IWorkflowService workflowService)
    {
        _workflow = workflow;
        _workflowAdapters = workflowAdapters;
        _templatedEmail = templatedEmail;
        _notices = notices;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
        _engine = engine;
        _workflowService = workflowService;
    }

    // ---- the engine ----

    /// <summary>
    /// Sends a Tentative booking to the engine — at creation, and when a confirmed one moves back to waiting. Saves.
    /// A start that fails leaves it waiting with no approval under way; the approve tier then decides it.
    /// </summary>
    public async Task StartApprovalAsync(RoomBooking b, CancellationToken cancellationToken)
    {
        try
        {
            var (result, outcome) = await HrWorkflowFallbackAuthority.SubmitAsync(_workflow, WorkflowEntityType, b.Id);
            if (!result.ExecutionResult.Success)
            {
                _logger.LogWarning("Approval did not start for booking {BookingNumber}: {Message}", b.BookingNumber, result.ExecutionResult.Message);
                return;
            }
            _workflowAdapters.GetAdapter(WorkflowEntityType).ApplySubmitOutcome(b, outcome, null);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Approval did not start for booking {BookingNumber}; it waits for the approve tier.", b.BookingNumber);
        }
    }

    /// <summary>
    /// The decision: through the engine when an approval is under way, and from the approve tier
    /// (<c>HR.Company.Approve</c>) when none is — an unconfigured tenant, a booking from before 3b-1, a start that failed.
    /// </summary>
    public async Task<WorkflowOutcome> DecideAsync(RoomBooking b, string action, string? comments)
    {
        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("Sign in to decide on a room booking.");

        var description = (action == "Reject" ? "reject" : "approve") + " a room booking";
        if (await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, b.Id))
            return await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
                _workflow, _currentUser, WorkflowEntityType, b.Id, userId, action, comments, description, HrPermissions.ApproveCompany);

        HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(_currentUser, description, HrPermissions.ApproveCompany);
        return action == "Reject" ? WorkflowOutcome.Rejected : WorkflowOutcome.Approved;
    }

    /// <summary>Applies the engine's outcome to the booking (the adapter's rules).</summary>
    public void ApplyOutcome(RoomBooking b, WorkflowOutcome outcome, Guid deciderEmployeeId, string? reason = null) =>
        _workflowAdapters.GetAdapter(WorkflowEntityType).ApplyApprovalOutcome(b, outcome, deciderEmployeeId, reason);

    /// <summary>
    /// Withdraws an approval still under way — the booking was cancelled, deleted or moved. Answers the login that started
    /// the approval withdrawn, or null when none was under way (lane 3d-1: whose name a passed-on approval goes in).
    /// </summary>
    public async Task<Guid?> WithdrawApprovalAsync(RoomBooking b, string reason)
    {
        try
        {
            if (!(await ActiveInstancesAsync(b.TenantId, [b.Id], CancellationToken.None)).TryGetValue(b.Id, out var startedBy))
                return null;
            await _workflow.CancelWorkflowAsync(WorkflowEntityType, b.Id, reason);
            return startedBy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not withdraw the approval of booking {BookingNumber}.", b.BookingNumber);
            return null;
        }
    }

    /// <summary>
    /// Withdraws the approval of a booking cancelled or deleted, and — if it carried its set's approval — passes it to the
    /// next date still waiting (lane 3d-1, the user's ruling).
    /// </summary>
    public async Task WithdrawAndPassOnAsync(RoomBooking b, string reason, CancellationToken cancellationToken)
    {
        if (await WithdrawApprovalAsync(b, reason) is { } startedBy)
            await PassApprovalOnAsync(b, startedBy, cancellationToken);
    }

    // ---- a series' dates booked together (lane 3d-1) ----

    /// <summary>The series of the event a booking is for, or null for a booking of no event or a single event.</summary>
    private async Task<Guid?> SeriesOfAsync(RoomBooking b, CancellationToken cancellationToken)
    {
        if (b.EventId is not { } eventId) return null;
        return await _unitOfWork.Repository<CompanyEvent>().GetQueryable().AsNoTracking()
            .Where(e => e.Id == eventId && e.TenantId == b.TenantId)
            .Select(e => e.RecurrenceSeriesId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The other dates booked with <paramref name="b"/> that still wait: the same booker's bookings of the same room for the
    /// same series, Tentative and not cancelled.
    /// </summary>
    private IQueryable<RoomBooking> WaitingWith(RoomBooking b, Guid seriesId) =>
        _unitOfWork.Repository<RoomBooking>().GetQueryable()
            .Where(x => x.TenantId == b.TenantId && x.Id != b.Id && x.RoomId == b.RoomId && x.BookedById == b.BookedById
                        && !x.IsCancelled && x.Status == BookingStatus.Tentative
                        && x.EventId != null && x.Event!.RecurrenceSeriesId == seriesId);

    /// <summary>
    /// The approvals under way on the given bookings, as booking id → the login that started it. Read straight from the
    /// engine's table, tenant-explicit: the hourly lapse asks it with nobody signed in.
    /// </summary>
    private async Task<Dictionary<Guid, Guid>> ActiveInstancesAsync(Guid tenantId, IEnumerable<Guid> bookingIds, CancellationToken cancellationToken)
    {
        var ids = bookingIds.Distinct().ToList();
        if (ids.Count == 0) return new();
        var rows = await _unitOfWork.Repository<WorkflowInstance>().GetQueryable().AsNoTracking()
            .Where(i => i.TenantId == tenantId && ids.Contains(i.EntityId)
                        && (i.Status == WorkflowInstanceStatus.Created || i.Status == WorkflowInstanceStatus.InProgress
                            || i.Status == WorkflowInstanceStatus.Waiting || i.Status == WorkflowInstanceStatus.Suspended))
            .Select(i => new { i.EntityId, i.InitiatedById, i.CreatedAt })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.EntityId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First().InitiatedById);
    }

    /// <summary>
    /// The dates a decision on <paramref name="b"/> also decides (the user's ruling: approved once for the set) — the other
    /// dates booked with it still waiting with no approval of their own under way — tracked, in date order. Empty for a
    /// booking of no series.
    /// </summary>
    /// <remarks>
    /// A date moved on its own after approval was sent back for an approval of its own (D-10), and keeps it. As for an
    /// event series, a decision on that date also covers the dates still waiting with nothing under way.
    /// </remarks>
    public async Task<List<RoomBooking>> SharingSetAsync(RoomBooking b, CancellationToken cancellationToken)
    {
        if (await SeriesOfAsync(b, cancellationToken) is not { } seriesId) return [];
        var waiting = await WaitingWith(b, seriesId).OrderBy(x => x.StartDateTime).ToListAsync(cancellationToken);
        if (waiting.Count == 0) return waiting;
        var underWay = await ActiveInstancesAsync(b.TenantId, waiting.Select(x => x.Id), cancellationToken);
        return waiting.Where(x => !underWay.ContainsKey(x.Id)).ToList();
    }

    /// <summary>
    /// Refuses a decision on a date whose approval is under way on another date booked with it (the user's ruling) —
    /// naming the one to decide. A date with an approval of its own is decided on its own.
    /// </summary>
    public async Task RefuseSharedElsewhereAsync(RoomBooking b, string verb, CancellationToken cancellationToken)
    {
        if (await SeriesOfAsync(b, cancellationToken) is not { } seriesId) return;
        if ((await ActiveInstancesAsync(b.TenantId, [b.Id], cancellationToken)).Count > 0) return;

        var others = await WaitingWith(b, seriesId).AsNoTracking()
            .OrderBy(x => x.StartDateTime)
            .Select(x => new { x.Id, x.BookingNumber, x.StartDateTime, x.EndDateTime })
            .ToListAsync(cancellationToken);
        var underWay = await ActiveInstancesAsync(b.TenantId, others.Select(x => x.Id), cancellationToken);
        if (others.FirstOrDefault(x => underWay.ContainsKey(x.Id)) is { } carrier)
            throw new InvalidOperationException(
                $"{b.BookingNumber} is approved with the other dates booked with it: {verb} {carrier.BookingNumber} "
              + $"({RoomBookingRules.Describe(RoomBookingRules.AsUtc(carrier.StartDateTime), RoomBookingRules.AsUtc(carrier.EndDateTime))}), "
              + "and the decision covers this one too.");
    }

    /// <summary>
    /// When the date that carried its set's approval is cancelled, deleted or lapses (the user's ruling), the approval
    /// passes to the next date still waiting and still to come — in the name of the login that started the one withdrawn,
    /// so the booker is still the one asking and the approver still not barred. Nothing happens when another date already
    /// has an approval under way, or nothing waits.
    /// </summary>
    /// <remarks>
    /// Tenant-explicit (<see cref="IWorkflowService.StartApprovalWorkflowAsAsync"/>): the hourly lapse runs it with nobody
    /// signed in, where the integration's submit throws. A date whose start has passed is skipped — it lapses itself.
    /// </remarks>
    public async Task PassApprovalOnAsync(RoomBooking from, Guid startedBy, CancellationToken cancellationToken)
    {
        try
        {
            if (await SeriesOfAsync(from, cancellationToken) is not { } seriesId) return;
            var now = DateTime.UtcNow;
            var waiting = await WaitingWith(from, seriesId).AsNoTracking()
                .Where(x => x.StartDateTime > now)
                .OrderBy(x => x.StartDateTime)
                .Select(x => new { x.Id, x.BookingNumber })
                .ToListAsync(cancellationToken);
            if (waiting.Count == 0) return;
            if ((await ActiveInstancesAsync(from.TenantId, waiting.Select(x => x.Id), cancellationToken)).Count > 0) return;

            var next = waiting[0];
            var result = await _workflowService.StartApprovalWorkflowAsAsync(WorkflowEntityType, next.Id, startedBy, from.TenantId);
            if (result.Success)
                _logger.LogInformation("The approval of the dates booked with {From} passed to {Next}", from.BookingNumber, next.BookingNumber);
            else
                _logger.LogWarning("The approval of the dates booked with {From} could not pass to {Next}: {Message}; they wait for the approve tier.",
                    from.BookingNumber, next.BookingNumber, result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The approval of the dates booked with {From} could not be passed on; they wait for the approve tier.", from.BookingNumber);
        }
    }

    /// <summary>
    /// Withdraws the approval of a booking the hourly sweep lapsed (lane 3b-2, F-48) — which may run with nobody signed in.
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="WithdrawApprovalAsync"/> cannot serve here: the integration finds the instance by the signed-in user's
    /// tenant and records the signed-in user, and with nobody signed in it throws — the lapsed booking's approval went on
    /// asking the desk. So the engine is asked directly, by the instance, in the name of the login that started the
    /// approval (the booker's): its activity log needs a real user, and the reason says it lapsed.
    /// </remarks>
    /// <returns>The login that started the approval withdrawn, or null when none was under way (lane 3d-1).</returns>
    public async Task<Guid?> WithdrawLapsedApprovalAsync(RoomBooking b, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var instance = await _unitOfWork.Repository<WorkflowInstance>().GetQueryable().AsNoTracking()
                .Where(i => i.TenantId == b.TenantId && i.EntityId == b.Id
                            && (i.Status == WorkflowInstanceStatus.Created || i.Status == WorkflowInstanceStatus.InProgress
                                || i.Status == WorkflowInstanceStatus.Waiting || i.Status == WorkflowInstanceStatus.Suspended))
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new { i.Id, i.InitiatedById })
                .FirstOrDefaultAsync(cancellationToken);
            if (instance is null) return null;
            await _engine.CancelWorkflowAsync(instance.Id, instance.InitiatedById, reason);
            return instance.InitiatedById;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not withdraw the approval of lapsed booking {BookingNumber}.", b.BookingNumber);
            return null;
        }
    }

    // ---- telling the booker ----

    /// <summary>Marked a no-show (lane 3b-2).</summary>
    public Task TellNoShowAsync(RoomBooking b, CancellationToken cancellationToken) =>
        TellBookerAsync(b, CompanyScheduleNotices.BookingNoShow, CompanyScheduleEmailCatalog.Events.BookingNoShow,
            null, "booking marked a no-show", cancellationToken);

    /// <summary>Approved, at its last stage.</summary>
    public Task TellApprovedAsync(RoomBooking b, string? approvedBy, CancellationToken cancellationToken) =>
        TellBookerAsync(b, CompanyScheduleNotices.BookingApproved, CompanyScheduleEmailCatalog.Events.BookingApproved,
            tokens => tokens["ApprovedBy"] = approvedBy, "booking approved", cancellationToken);

    /// <summary>Cancelled — by the desk, with its event, by retiring its room — or, when <paramref name="notApproved"/>, not approved.</summary>
    public Task TellCancelledAsync(RoomBooking b, bool notApproved, CancellationToken cancellationToken) =>
        TellBookerAsync(b,
            notApproved ? CompanyScheduleNotices.BookingNotApproved : CompanyScheduleNotices.BookingCancelled,
            CompanyScheduleEmailCatalog.Events.BookingCancelled,
            tokens =>
            {
                tokens["CancelTitle"] = notApproved ? "Booking not approved" : "Booking cancelled";
                tokens["CancelSentence"] = notApproved ? "was not approved" : "has been cancelled";
                tokens["CancellationReason"] = b.CancellationReason;
            },
            notApproved ? "booking not approved" : "booking cancelled", cancellationToken);

    /// <summary>
    /// Several bookings approved by one act — the dates of a series (lane 3d-1). Each booker is told once, listing theirs;
    /// one booking uses the single-booking notice.
    /// </summary>
    public async Task TellApprovedAsync(IReadOnlyList<RoomBooking> bookings, string? approvedBy, CancellationToken cancellationToken)
    {
        foreach (var mine in bookings.GroupBy(b => b.BookedById).Select(g => g.OrderBy(b => b.StartDateTime).ToList()))
        {
            if (mine.Count == 1) await TellApprovedAsync(mine[0], approvedBy, cancellationToken);
            else await TellBookerOfManyAsync(mine, "Approved", "Bookings approved",
                $"These bookings of yours have been approved{(approvedBy is null ? string.Empty : $" by {approvedBy}")}. The rooms are yours.",
                withReasons: false, "bookings approved", cancellationToken);
        }
    }

    /// <summary>
    /// Several bookings cancelled — or not approved — by one act: the dates of a series, a series cancelled, a room taken
    /// out of use (lane 3d-1). Each booker is told once, listing theirs, each with its reason; one booking uses the
    /// single-booking notice.
    /// </summary>
    public async Task TellCancelledAsync(IReadOnlyList<RoomBooking> bookings, bool notApproved, CancellationToken cancellationToken)
    {
        foreach (var mine in bookings.GroupBy(b => b.BookedById).Select(g => g.OrderBy(b => b.StartDateTime).ToList()))
        {
            if (mine.Count == 1) await TellCancelledAsync(mine[0], notApproved, cancellationToken);
            else if (notApproved)
                await TellBookerOfManyAsync(mine, "Not approved", "Bookings not approved",
                    "These bookings of yours were not approved, and the rooms are released.", withReasons: true, "bookings not approved", cancellationToken);
            else
                await TellBookerOfManyAsync(mine, "Cancelled", "Bookings cancelled",
                    "These bookings of yours have been cancelled, and the rooms are released.", withReasons: true, "bookings cancelled", cancellationToken);
        }
    }

    /// <summary>
    /// Tells one booker of several of their bookings at once (lane 3d-1, the user's ruling): one email listing them —
    /// each room, time and number, and its reason when <paramref name="withReasons"/> — and one notice in the app. Never of
    /// their own act; never fails the act.
    /// </summary>
    private async Task TellBookerOfManyAsync(
        IReadOnlyList<RoomBooking> mine, string what, string title, string summary, bool withReasons, string description,
        CancellationToken cancellationToken)
    {
        var first = mine[0];
        try
        {
            if (await _notices.ActorEmployeeIdAsync(cancellationToken) == first.BookedById) return;

            var booker = await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
                .Where(x => x.Id == first.BookedById && x.TenantId == first.TenantId && x.IsActive)
                .Select(x => new { x.FirstName, x.LastName, x.EmailAddress })
                .FirstOrDefaultAsync(cancellationToken);
            if (booker is null) return;

            var roomIds = mine.Select(b => b.RoomId).Distinct().ToList();
            var rooms = await _unitOfWork.Repository<MeetingRoom>().GetQueryableIncludingDeleted(r => roomIds.Contains(r.Id))
                .AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.RoomName, cancellationToken);

            if (!string.IsNullOrWhiteSpace(booker.EmailAddress))
                await SendAsync(first.TenantId, CompanyScheduleEmailCatalog.Events.BookingsChanged, booker.EmailAddress,
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["BookerName"] = $"{booker.FirstName} {booker.LastName}".Trim(),
                        ["ChangeTitle"] = title,
                        ["ChangeSummary"] = summary,
                        ["BookingCount"] = mine.Count.ToString(CultureInfo.InvariantCulture),
                        ["BookingList"] = BookingListHtml(mine, rooms, withReasons),
                    }, description);
            await _notices.TellBookerOfManyAsync(first, mine.Count, what, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The {Description} notice for {Count} bookings from {BookingNumber} could not be sent; they are saved.",
                description, mine.Count, first.BookingNumber);
        }
    }

    /// <summary>
    /// The bookings a several-at-once email lists — built here, every value encoded, and emitted raw
    /// (<c>{{{BookingList}}}</c>, declared HTML on the catalogue), as a series email's dates are.
    /// </summary>
    private static string BookingListHtml(IEnumerable<RoomBooking> mine, IReadOnlyDictionary<Guid, string> rooms, bool withReasons) =>
        "<ul style='margin:0.5rem 0 1rem;padding-left:1.25rem'>"
        + string.Concat(mine.Select(b =>
        {
            var line = $"{rooms.GetValueOrDefault(b.RoomId, "the room")} — "
                       + $"{RoomBookingRules.Describe(RoomBookingRules.AsUtc(b.StartDateTime), RoomBookingRules.AsUtc(b.EndDateTime))} ({b.BookingNumber})";
            var why = withReasons ? CompanyEventRules.Clean(b.CancellationReason) : null;
            return $"<li>{System.Net.WebUtility.HtmlEncode(line)}{(why is null ? string.Empty : $"<br><span style='color:#6b7280'>{System.Net.WebUtility.HtmlEncode(why)}</span>")}</li>";
        }))
        + "</ul>";

    /// <summary>
    /// Tells the booker of an outcome, in the app and by email — never of their own act. Reads the booker, the room and
    /// the event fresh: callers may not have loaded them.
    /// </summary>
    public async Task TellBookerAsync(
        RoomBooking b, string notice, string emailKey, Action<Dictionary<string, string?>>? enrich, string description,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _notices.ActorEmployeeIdAsync(cancellationToken) == b.BookedById) return;

            var booker = await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
                .Where(x => x.Id == b.BookedById && x.TenantId == b.TenantId && x.IsActive)
                .Select(x => new { x.FirstName, x.LastName, x.EmailAddress })
                .FirstOrDefaultAsync(cancellationToken);
            if (booker is null) return;

            var roomName = await _unitOfWork.Repository<MeetingRoom>().GetQueryableIncludingDeleted(r => r.Id == b.RoomId)
                .AsNoTracking().Select(r => r.RoomName).FirstOrDefaultAsync(cancellationToken) ?? "the room";
            string? eventName = null;
            if (b.EventId is { } eventId)
                eventName = await _unitOfWork.Repository<CompanyEvent>().GetQueryableIncludingDeleted(e => e.Id == eventId)
                    .AsNoTracking().Select(e => e.EventName).FirstOrDefaultAsync(cancellationToken);

            var start = RoomBookingRules.AsUtc(b.StartDateTime);
            var end = RoomBookingRules.AsUtc(b.EndDateTime);
            var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["BookerName"] = $"{booker.FirstName} {booker.LastName}".Trim(),
                ["BookingNumber"] = b.BookingNumber,
                ["RoomName"] = roomName,
                ["BookingDate"] = start.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture),
                ["BookingTime"] = $"{start.ToString("HH:mm", CultureInfo.InvariantCulture)} – {end.ToString("HH:mm", CultureInfo.InvariantCulture)}",
                ["Purpose"] = CompanyEventRules.Clean(b.Purpose),
                ["EventName"] = eventName,
            };
            enrich?.Invoke(tokens);

            if (!string.IsNullOrWhiteSpace(booker.EmailAddress))
                await SendAsync(b.TenantId, emailKey, booker.EmailAddress, tokens, description);
            await _notices.TellBookerAsync(b, roomName, notice, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The {Description} notice for booking {BookingNumber} could not be sent; the booking is saved.",
                description, b.BookingNumber);
        }
    }

    /// <summary>
    /// One booking email, by the booking's tenant, raced against ten seconds — as the event emails are: an unreachable
    /// mail server must never fail the act.
    /// </summary>
    private async Task<bool> SendAsync(Guid tenantId, string key, string toEmail, Dictionary<string, string?> tokens, string description)
    {
        try
        {
            var send = _templatedEmail.SendForTenantAsync(tenantId, CompanyScheduleEmailCatalog.Module, key, toEmail, tokens);
            if (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(10))) == send)
                return await send;
            _logger.LogWarning("{Description} email timed out after 10 s for {Email}; the booking is saved.", description, toEmail);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send the {Description} email to {Email}; the booking is saved.", description, toEmail);
            return false;
        }
    }
}
