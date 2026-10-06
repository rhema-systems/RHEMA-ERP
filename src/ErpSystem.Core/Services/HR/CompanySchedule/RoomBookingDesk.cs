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

    public RoomBookingDesk(
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        ITemplatedEmailService templatedEmail,
        CompanyScheduleNotices notices,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<RoomBookingDesk> logger,
        IWorkflowEngine engine)
    {
        _workflow = workflow;
        _workflowAdapters = workflowAdapters;
        _templatedEmail = templatedEmail;
        _notices = notices;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
        _engine = engine;
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

    /// <summary>Withdraws an approval still under way — the booking was cancelled, deleted or moved.</summary>
    public async Task WithdrawApprovalAsync(RoomBooking b, string reason)
    {
        try
        {
            if (await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, b.Id))
                await _workflow.CancelWorkflowAsync(WorkflowEntityType, b.Id, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not withdraw the approval of booking {BookingNumber}.", b.BookingNumber);
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
    public async Task WithdrawLapsedApprovalAsync(RoomBooking b, string reason, CancellationToken cancellationToken)
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
            if (instance is null) return;
            await _engine.CancelWorkflowAsync(instance.Id, instance.InitiatedById, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not withdraw the approval of lapsed booking {BookingNumber}.", b.BookingNumber);
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
