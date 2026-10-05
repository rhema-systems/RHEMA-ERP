using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class AppraisalConversationService : IAppraisalConversationService
{
    private readonly IGenericRepository<AppraisalConversation> _conversationRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<AppraisalReviewEvent> _reviewEventRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAppraisalNotificationService _notifications;
    private readonly IAppraisalLifecycleService _lifecycle;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalConversationService> _logger;

    public AppraisalConversationService(
        IGenericRepository<AppraisalConversation> conversationRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalReviewEvent> reviewEventRepository,
        ICurrentUserProvider currentUserProvider,
        IAppraisalNotificationService notifications,
        IAppraisalLifecycleService lifecycle,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalConversationService> logger)
    {
        _conversationRepository = conversationRepository;
        _appraisalRepository = appraisalRepository;
        _reviewEventRepository = reviewEventRepository;
        _currentUserProvider = currentUserProvider;
        _notifications = notifications;
        _lifecycle = lifecycle;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>Best-effort notification: the conversation is already saved by the time we raise one.</summary>
    private async Task NotifyQuietlyAsync(IEnumerable<AppraisalNotificationRequest> requests, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.RaiseAsync(requests, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to raise conversation notification(s); the originating action stands.");
        }
    }

    /// <summary>"MidYear" becomes "Mid Year" — the enum names are PascalCase and go into notification text as they are.</summary>
    private static string Humanize(ConversationType type)
        => System.Text.RegularExpressions.Regex.Replace(type.ToString(), "(?<=[a-z])([A-Z])", " $1");

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A conversation owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalConversation> GetOwnedAsync(Guid id)
    {
        var entity = await _conversationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Conversation with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// A withdrawn appraisal holds no more conversations (performance closure E-d1): the ones held
    /// stay as its history, and the rest leave the manager's diary.
    /// </summary>
    private async Task EnsureAppraisalNotWithdrawnAsync(Guid appraisalId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        if (await _appraisalRepository.GetQueryable(a => a.Id == appraisalId && a.TenantId == tenantId)
                .AnyAsync(a => a.Status == AppraisalStatus.Withdrawn, cancellationToken))
            throw new InvalidOperationException(
                "This appraisal was withdrawn from its cycle, so it holds no more conversations.");
    }

    /// <summary>The appraisal's work is done while its cycle is Open (performance closure E-d2b, D-59).</summary>
    private Task EnsureCycleOpenAsync(Guid appraisalId, string action, CancellationToken cancellationToken)
        => AppraisalLiveCycle.EnsureAppraisalCycleOpenAsync(
            _appraisalRepository.GetQueryable(), GetTenantId(), appraisalId, action, cancellationToken);

    /// <summary>
    /// Decision D-76: the refusal names every type the enum defines — it listed six, while the
    /// PIP discussion and the ad-hoc meeting were accepted too.
    /// </summary>
    private static string TypeRequiredMessage() =>
        "Say which conversation this is: " + string.Join(", ", Enum.GetNames<ConversationType>()) + ".";

    /// <summary>
    /// Decision D-76: a conversation logged at a review event is logged at one of its own appraisal's.
    /// The id was saved as sent — another appraisal's event, or another tenant's.
    /// </summary>
    private async Task EnsureReviewEventBelongsAsync(Guid? reviewEventId, Guid appraisalId, CancellationToken cancellationToken)
    {
        if (reviewEventId is not Guid eventId) return;
        var tenantId = GetTenantId();
        if (!await _reviewEventRepository.ExistsAsync(r => r.Id == eventId && r.TenantId == tenantId && r.PerformanceAppraisalId == appraisalId))
            throw new InvalidOperationException("The review event is not one of this appraisal's review events.");
    }

    private IQueryable<AppraisalConversation> BaseQuery
    {
        get
        {
            var tenantId = GetTenantId();
            return _conversationRepository.GetQueryable()
                .Where(c => c.TenantId == tenantId)
                .Include(c => c.Appraisal)
                    .ThenInclude(a => a.Employee)
                .Include(c => c.ScheduledBy)
                .Include(c => c.ConductedBy);
        }
    }

    public async Task<AppraisalConversationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Conversation with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalConversationDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(c => c.AppraisalId == appraisalId)
            .OrderByDescending(c => c.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalConversationDto>> GetByTypeAsync(Guid appraisalId, ConversationType type, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(c => c.AppraisalId == appraisalId && c.Type == type)
            .OrderByDescending(c => c.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    /// <summary>
    /// A manager's outstanding conversations — every one they scheduled that has not been held.
    ///
    /// <para>This used to require <c>ScheduledDate &gt;= now</c>, so a conversation dropped off the
    /// list the moment its date passed. The one thing a manager needs from this screen is the
    /// meeting they were supposed to hold last week; a diary that only shows the future hides
    /// exactly the rows that need action.</para>
    ///
    /// <para>A withdrawn appraisal's unheld conversations are not outstanding (performance closure
    /// E-d1): no one will hold them.</para>
    ///
    /// <para>The line manager's reports' conversations are theirs too (D-74): the conductor is now
    /// whoever marks it held, so one HR booked for the manager would otherwise be in nobody's diary
    /// — HR named the manager as conductor to put it there.</para>
    /// </summary>
    public async Task<IEnumerable<AppraisalConversationDto>> GetScheduledByManagerAsync(Guid managerId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(c => (c.ScheduledById == managerId || c.ConductedById == managerId
                         || c.Appraisal.Employee.ManagerId == managerId)
                        && !c.IsCompleted
                        && c.Appraisal.Status != AppraisalStatus.Withdrawn)
            .OrderBy(c => c.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    /// <summary>
    /// Conversations about a given employee — the appraisee's own view, which is keyed on the
    /// appraisal rather than on who scheduled the meeting.
    /// </summary>
    public async Task<IEnumerable<AppraisalConversationDto>> GetByAppraiseeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(c => c.Appraisal.EmployeeId == employeeId)
            .OrderByDescending(c => c.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<AppraisalConversationDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.OrderByDescending(c => c.ScheduledDate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AppraisalConversationDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalConversationDto> CreateAsync(CreateAppraisalConversationDto createDto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // B2: the type is what the gates count — a kick-off holds the self-evaluation, a mid-year the
        // manager's submission, a final review the acknowledgment — so it is never assumed. It
        // defaulted to KickOff, and a number the enum does not define was stored as it came.
        if (createDto.Type is not ConversationType type || !Enum.IsDefined(type))
            throw new ArgumentException(TypeRequiredMessage());

        // The appraisal is the conversation's whole context — who it is about, and which cycle it
        // belongs to. An id from another tenant, or none at all, would save happily and then read
        // back as a conversation about nobody.
        var appraisal = await _appraisalRepository
            .GetQueryable(a => a.Id == createDto.AppraisalId && a.TenantId == tenantId)
            .Select(a => new { a.Id, a.EmployeeId, a.AppraisalNumber, a.Status, CycleStatus = a.AppraisalCycle.Status, a.AppraisalCycle.CycleName })
            .FirstOrDefaultAsync(cancellationToken);
        if (appraisal == null)
            throw new ArgumentException("Appraisal not found.");
        if (appraisal.Status == AppraisalStatus.Withdrawn)
            throw new InvalidOperationException(
                "This appraisal was withdrawn from its cycle, so it holds no more conversations.");
        // The appraisal's work is done while its cycle is Open (performance closure E-d2b).
        AppraisalLiveCycle.EnsureOpen(appraisal.CycleStatus, appraisal.CycleName, "A conversation cannot be booked");
        await EnsureReviewEventBelongsAsync(createDto.ReviewEventId, appraisal.Id, cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.IsCompleted = false;
        // Decision D-74: the scheduler is who booked it, from the token; the conductor is who marks it
        // held. Both came from the body, and naming someone made them a writer of the conversation.
        entity.ScheduledById = actorEmployeeId is Guid me && me != Guid.Empty ? me : null;
        entity.ConductedById = null;

        await _conversationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal conversation created: {Id}", entity.Id);

        // A conversation the employee is not told about is a diary entry, not an invitation.
        if (entity.ScheduledDate is DateTime scheduledFor && appraisal.EmployeeId != entity.ScheduledById)
        {
            await NotifyQuietlyAsync(new[]
            {
                new AppraisalNotificationRequest(
                    appraisal.EmployeeId,
                    AppraisalNotificationType.ConversationScheduled,
                    $"{Humanize(entity.Type)} conversation scheduled",
                    $"Your {Humanize(entity.Type).ToLowerInvariant()} conversation is set for {scheduledFor:d MMM yyyy HH:mm}.",
                    NavigationUrl: $"/hr/performance/conversations/{entity.Id}",
                    AppraisalId: appraisal.Id)
            }, cancellationToken);
        }

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<AppraisalConversationDto> UpdateAsync(UpdateAppraisalConversationDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        if (entity.IsCompleted)
            throw new InvalidOperationException("Cannot update a completed conversation.");
        await EnsureAppraisalNotWithdrawnAsync(entity.AppraisalId, cancellationToken);
        await EnsureCycleOpenAsync(entity.AppraisalId, "The conversation cannot be changed", cancellationToken);

        if (updateDto.ReviewEventId != entity.ReviewEventId)
            await EnsureReviewEventBelongsAsync(updateDto.ReviewEventId, entity.AppraisalId, cancellationToken);

        // Decision D-74: an edit changes the meeting's details — when, the agenda, the notes, the
        // review event — and nothing that says what it is or who it belongs to. The DTO no longer
        // carries the type (the employee was told which conversation was booked), the appraisal (B2),
        // the scheduler and conductor (the token's), or held and its date (CompleteAsync's).
        updateDto.UpdateEntity(entity);

        await _conversationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal conversation updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        // Decision D-74: a held conversation is what the gates count — deleting a held kick-off moved
        // the appraisal back to goal setting, and deleting HR's held placeholder undid an audited
        // advance — so it is never removed. A withdrawn appraisal's are its record (its unheld ones went
        // with the withdrawal).
        if (entity.IsCompleted)
            throw new InvalidOperationException(
                "This conversation was held, so it is part of the appraisal's record and cannot be removed.");
        await EnsureAppraisalNotWithdrawnAsync(entity.AppraisalId, cancellationToken);
        await EnsureCycleOpenAsync(entity.AppraisalId, "The conversation cannot be removed", cancellationToken);

        await _conversationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Appraisal conversation deleted: {Id}", id);
        return true;
    }

    public async Task<AppraisalConversationDto> CompleteAsync(
        Guid conversationId, string? postMeetingNotes, string? keyTakeaways,
        DateTime? heldDate, Guid? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(conversationId);

        if (entity.IsCompleted)
            throw new InvalidOperationException("Conversation is already completed.");
        await EnsureAppraisalNotWithdrawnAsync(entity.AppraisalId, cancellationToken);
        await EnsureCycleOpenAsync(entity.AppraisalId, "The conversation cannot be marked held", cancellationToken);

        // Decision D-74: the date it was held can be stated — a meeting held last week is recorded with
        // its real date — never in the future, and today when none is given. It was always today.
        var now = DateTime.UtcNow;
        if (heldDate is DateTime stated && stated.Date > now.Date)
            throw new InvalidOperationException(
                $"A conversation cannot be recorded as held on {stated:d MMM yyyy}, which has not come yet.");

        entity.IsCompleted = true;
        entity.HeldDate = heldDate ?? now;
        // The conductor is who marks it held (D-74), from the token; an account with no employee
        // record leaves whatever was there.
        if (actorEmployeeId is Guid conductor && conductor != Guid.Empty)
            entity.ConductedById = conductor;
        entity.PostMeetingNotes = postMeetingNotes;
        entity.KeyTakeaways = keyTakeaways;

        await _conversationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal conversation {Id} completed", conversationId);

        // A held conversation can be the step that completes the appraisal — the final review, with
        // no acknowledgment after it — which left it in Governance for good. The gates decide now,
        // and completion settles (B1). A kick-off held on a Draft appraisal does not open it.
        await _lifecycle.SyncAsync(entity.AppraisalId, cancellationToken: cancellationToken);

        var result = await GetByIdAsync(conversationId, cancellationToken);

        // ConversationCompleted has existed on the notification enum since the appraisal run was
        // built and nothing has ever raised it. The employee needs to know the notes are up.
        var appraisee = await _appraisalRepository
            .GetQueryable(a => a.Id == entity.AppraisalId && a.TenantId == entity.TenantId)
            .Select(a => (Guid?)a.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (appraisee is Guid employeeId)
        {
            await NotifyQuietlyAsync(new[]
            {
                new AppraisalNotificationRequest(
                    employeeId,
                    AppraisalNotificationType.ConversationCompleted,
                    $"{Humanize(entity.Type)} conversation held",
                    string.IsNullOrWhiteSpace(keyTakeaways)
                        ? "The notes from your conversation have been recorded."
                        : $"Key takeaways: {(keyTakeaways!.Length > 180 ? keyTakeaways[..180] + "…" : keyTakeaways)}",
                    NavigationUrl: $"/hr/performance/conversations/{entity.Id}",
                    AppraisalId: entity.AppraisalId)
            }, cancellationToken);
        }

        return result;
    }
}
