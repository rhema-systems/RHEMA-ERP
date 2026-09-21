using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc />
public class TeamMeetingService : ITeamMeetingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITeamAccessGuard _access;
    private readonly ITeamActivityService _activity;
    private readonly ILogger<TeamMeetingService> _logger;

    public TeamMeetingService(
        IUnitOfWork unitOfWork,
        ITeamAccessGuard access,
        ITeamActivityService activity,
        ILogger<TeamMeetingService> logger)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _activity = activity;
        _logger = logger;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private IGenericRepository<TeamMeeting> Meetings => _unitOfWork.Repository<TeamMeeting>();
    private IGenericRepository<TeamMeetingAttendee> Attendees => _unitOfWork.Repository<TeamMeetingAttendee>();
    private IGenericRepository<TeamMeetingDecision> Decisions => _unitOfWork.Repository<TeamMeetingDecision>();
    private IGenericRepository<TeamReview> Reviews => _unitOfWork.Repository<TeamReview>();
    private IGenericRepository<TeamReviewLine> ReviewLines => _unitOfWork.Repository<TeamReviewLine>();
    private IGenericRepository<TeamObjective> Objectives => _unitOfWork.Repository<TeamObjective>();
    private IGenericRepository<TeamTask> Tasks => _unitOfWork.Repository<TeamTask>();
    private IGenericRepository<TeamTermsOfReference> Terms => _unitOfWork.Repository<TeamTermsOfReference>();

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Meetings
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<TeamMeetingListDto>> GetMeetingsAsync(
        Guid teamId, CancellationToken cancellationToken = default)
    {
        await _access.RequireReadAsync(teamId, cancellationToken);
        var tenantId = _access.GetTenantId();

        var rows = await Meetings.GetQueryable().AsNoTracking()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && m.TeamId == teamId)
            .OrderByDescending(m => m.ScheduledAt)
            .ToListAsync(cancellationToken);

        // ⚠ Two grouped counts for the whole list, not four queries per row. A committee with two
        // years of monthly minutes is 24 rows, and the per-row shape is the N+1 this module has had
        // to repair in sixteen list projections.
        var counts = await MeetingCountsAsync(tenantId, rows.Select(r => r.Id).ToList(), cancellationToken);
        var chairNames = await _access.MemberNamesAsync(
            rows.Where(m => m.ChairMemberId != null).Select(m => m.ChairMemberId!.Value), cancellationToken);

        return rows.Select(m => ToMeetingListDto(m, counts, chairNames)).ToList();
    }

    public async Task<TeamMeetingDetailDto?> GetMeetingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _access.GetTenantId();
        var row = await Meetings.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId && !m.IsDeleted, cancellationToken);
        if (row is null) return null;

        await _access.RequireReadAsync(row.TeamId, cancellationToken);

        var attendees = await Attendees.GetQueryable().AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.MeetingId == id)
            .Include(a => a.Member).ThenInclude(m => m.Employee)
            .ToListAsync(cancellationToken);

        var decisions = await Decisions.GetQueryable().AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.MeetingId == id)
            .Include(d => d.ResponsibleMember).ThenInclude(m => m!.Employee)
            .OrderBy(d => d.DisplayOrder)
            .ToListAsync(cancellationToken);

        // The raised tasks' titles, so the minute book can show what became of each decision.
        var taskIds = decisions.Where(d => d.RaisedTaskId != null).Select(d => d.RaisedTaskId!.Value).ToList();
        var taskTitles = taskIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await Tasks.GetQueryable().AsNoTracking()
                .Where(t => t.TenantId == tenantId && taskIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Title, cancellationToken);

        var counts = await MeetingCountsAsync(tenantId, new List<Guid> { id }, cancellationToken);
        var chairNames = row.ChairMemberId is { } chairId
            ? await _access.MemberNamesAsync(new[] { chairId }, cancellationToken)
            : new Dictionary<Guid, string>();

        var list = ToMeetingListDto(row, counts, chairNames);
        return new TeamMeetingDetailDto
        {
            Id = list.Id, TeamId = list.TeamId, Kind = list.Kind, Title = list.Title,
            ScheduledAt = list.ScheduledAt, HeldAt = list.HeldAt, Venue = list.Venue,
            Status = list.Status, ChairMemberId = list.ChairMemberId, ChairName = list.ChairName,
            HasMinutesDocument = list.HasMinutesDocument, MinutesFileName = list.MinutesFileName,
            AttendeeCount = list.AttendeeCount, AttendedCount = list.AttendedCount,
            DecisionCount = list.DecisionCount, DecisionsWithTaskCount = list.DecisionsWithTaskCount,
            Agenda = row.Agenda,
            Minutes = row.Minutes,
            CancelledReason = row.CancelledReason,
            MinutesMimeType = row.MinutesMimeType,
            MinutesFileSizeBytes = row.MinutesFileSizeBytes,
            Attendees = attendees.Select(ToAttendeeDto).ToList(),
            Decisions = decisions.Select(d => ToDecisionDto(d, taskTitles)).ToList(),
        };
    }

    public async Task<TeamMeetingDetailDto> CreateMeetingAsync(
        Guid teamId, CreateTeamMeetingDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _access.RequireTeamAsync(teamId, cancellationToken);
        await _access.RequireWriteAsync(teamId, cancellationToken);

        var tenantId = _access.GetTenantId();
        var entity = new TeamMeeting
        {
            // Explicit: the context auto-stamp is inert, and an unstamped row fails the Tenants FK.
            TenantId = tenantId,
            TeamId = teamId,
            Kind = dto.Kind,
            Title = dto.Title.Trim(),
            ScheduledAt = dto.ScheduledAt,
            Venue = Clean(dto.Venue),
            Agenda = Clean(dto.Agenda),
            Status = TeamMeetingStatus.Scheduled,
        };
        entity.ChairMemberId = await _access.ResolveMemberAsync(teamId, dto.ChairMemberId, "chair", cancellationToken);

        await Meetings.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await ReplaceAttendeesAsync(entity, dto.AttendeeMemberIds, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetMeetingAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TeamMeetingDetailDto> UpdateMeetingAsync(
        Guid id, UpdateTeamMeetingDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await RequireMeetingAsync(id, cancellationToken);
        await _access.RequireWriteAsync(entity.TeamId, cancellationToken);

        // ⚠ A held meeting's record is what happened. Re-dating or re-agenda-ing it afterwards would
        // rewrite the minute book; cancel or add a follow-up meeting instead.
        if (entity.Status == TeamMeetingStatus.Held)
            throw new InvalidOperationException(
                "This meeting has been held, so its record cannot be changed. Add its outcome to the "
                + "minutes, or hold a follow-up meeting.");

        entity.Kind = dto.Kind;
        entity.Title = dto.Title.Trim();
        entity.ScheduledAt = dto.ScheduledAt;
        entity.Venue = Clean(dto.Venue);
        entity.Agenda = Clean(dto.Agenda);
        entity.ChairMemberId = await _access.ResolveMemberAsync(
            entity.TeamId, dto.ChairMemberId, "chair", cancellationToken);

        await Meetings.UpdateAsync(entity);
        await ReplaceAttendeesAsync(entity, dto.AttendeeMemberIds, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetMeetingAsync(id, cancellationToken))!;
    }

    /// <summary>
    /// Replaces the invitee list, keeping the attendance already recorded for anyone who stays.
    /// </summary>
    /// <remarks>
    /// ⚠ A REPLACE SET, and the plan's replace-set warning applies: omitting somebody removes them.
    /// But an invitee who stays keeps their <c>Attended</c> and <c>Apology</c> — rebuilding the
    /// whole list from scratch would wipe a register the chair had already marked, which is the
    /// difference between replacing a LIST and replacing its CONTENTS.
    /// </remarks>
    private async Task ReplaceAttendeesAsync(
        TeamMeeting meeting, List<Guid> memberIds, CancellationToken ct)
    {
        var tenantId = meeting.TenantId;
        var wanted = memberIds.Distinct().ToList();

        foreach (var memberId in wanted)
            await _access.ResolveMemberAsync(meeting.TeamId, memberId, "attendee", ct);

        var existing = await Attendees.GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.MeetingId == meeting.Id)
            .ToListAsync(ct);

        foreach (var gone in existing.Where(a => !wanted.Contains(a.MemberId)))
            await Attendees.DeleteAsync(gone);

        var have = existing.Select(a => a.MemberId).ToHashSet();
        foreach (var memberId in wanted.Where(m => !have.Contains(m)))
        {
            // ⚠ Through the attendee's OWN repository, not by adding to meeting.Attendees — adding
            // to a tracked parent's collection turns the insert into an UPDATE of the parent and can
            // resurrect a soft-deleted sibling through fixup (the SHE checklist-builder lesson).
            await Attendees.AddAsync(new TeamMeetingAttendee
            {
                TenantId = tenantId,
                MeetingId = meeting.Id,
                MemberId = memberId,
                // ⚠ Attended stays NULL: the meeting has not happened, so nobody has missed it yet.
            });
        }
    }

    public async Task<TeamMeetingDetailDto> HoldMeetingAsync(
        Guid id, HoldTeamMeetingDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await RequireMeetingAsync(id, cancellationToken);
        await _access.RequireWriteAsync(entity.TeamId, cancellationToken);

        if (entity.Status == TeamMeetingStatus.Cancelled)
            throw new InvalidOperationException("This meeting was cancelled and cannot be held.");

        var heldAt = dto.HeldAt ?? DateTime.UtcNow;

        // A meeting cannot have happened before it was called for by more than a nominal slip; but
        // holding one EARLY is ordinary, so only the absurd case is refused.
        if (heldAt > DateTime.UtcNow.AddDays(1))
            throw new InvalidOperationException("A meeting cannot be recorded as held in the future.");

        entity.Status = TeamMeetingStatus.Held;
        entity.HeldAt = heldAt;
        if (Clean(dto.Minutes) is { } minutes) entity.Minutes = minutes;

        await Meetings.UpdateAsync(entity);

        // ⚠ NOT a replace set, unlike the invitee list. The register is marked in passes — the chair
        // ticks the room, then adds an apology that arrived late — and replacing the set on each
        // save would wipe the earlier pass. Anyone omitted keeps what was already recorded.
        if (dto.Attendance.Count > 0)
        {
            var tenantId = entity.TenantId;
            var rows = await Attendees.GetQueryable()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.MeetingId == id)
                .ToListAsync(cancellationToken);

            foreach (var mark in dto.Attendance)
            {
                var row = rows.FirstOrDefault(a => a.MemberId == mark.MemberId)
                    ?? throw new InvalidOperationException(
                        "Somebody marked on the register was not invited to this meeting. Add them to "
                        + "the invitee list first.");

                row.Attended = mark.Attended;
                row.Apology = mark.Apology;
                row.Notes = Clean(mark.Notes);
                await Attendees.UpdateAsync(row);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Team meeting {MeetingId} recorded as held.", id);

        return (await GetMeetingAsync(id, cancellationToken))!;
    }

    public async Task<TeamMeetingDetailDto> CancelMeetingAsync(
        Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var entity = await RequireMeetingAsync(id, cancellationToken);
        await _access.RequireWriteAsync(entity.TeamId, cancellationToken);

        if (entity.Status == TeamMeetingStatus.Held)
            throw new InvalidOperationException("This meeting has already been held and cannot be cancelled.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Say why the meeting is being cancelled.");

        entity.Status = TeamMeetingStatus.Cancelled;
        entity.CancelledReason = Clean(reason);

        await Meetings.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetMeetingAsync(id, cancellationToken))!;
    }

    public async Task<bool> DeleteMeetingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireMeetingAsync(id, cancellationToken);
        await _access.RequireWriteAsync(entity.TeamId, cancellationToken);

        // ⚠ A held meeting is the minute book. It is cancelled or corrected, never removed.
        if (entity.Status == TeamMeetingStatus.Held)
            throw new InvalidOperationException(
                "This meeting has been held and is part of the team's record. It cannot be deleted.");

        await Meetings.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AttachMinutesDocumentAsync(
        Guid id, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken cancellationToken = default)
    {
        var entity = await RequireMeetingAsync(id, cancellationToken);
        await _access.RequireWriteAsync(entity.TeamId, cancellationToken);

        entity.MinutesFileUploadRecordId = fileUploadRecordId;
        entity.MinutesDocumentRecordId = documentRecordId;
        entity.MinutesDocumentVersionId = documentVersionId;
        entity.MinutesFileName = fileName;
        entity.MinutesMimeType = mimeType;
        entity.MinutesFileSizeBytes = fileSizeBytes;

        await Meetings.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<HrStoredFileRef?> GetMinutesDocumentRefAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _access.GetTenantId();
        var row = await Meetings.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId && !m.IsDeleted, cancellationToken);
        if (row is null) return null;

        // ⚠ Entitlement before bytes: neither the gate nor the DMS asks whether this caller may see
        // the parent, so a download that skipped this would serve any committee's minutes to
        // anyone who could guess a row id.
        await _access.RequireReadAsync(row.TeamId, cancellationToken);

        if (row.MinutesDocumentRecordId is null && row.MinutesFileUploadRecordId is null) return null;

        return new HrStoredFileRef(
            row.MinutesDocumentRecordId, row.MinutesDocumentVersionId, row.MinutesFileUploadRecordId,
            row.MinutesFileName, row.MinutesMimeType);
    }

    private async Task<TeamMeeting> RequireMeetingAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _access.GetTenantId();
        return await Meetings.GetQueryable()
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId && !m.IsDeleted, ct)
            ?? throw new ArgumentException($"Meeting '{id}' was not found.");
    }

    private async Task<Dictionary<Guid, (int Invited, int Attended, int Decisions, int WithTask)>>
        MeetingCountsAsync(Guid tenantId, List<Guid> meetingIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, (int, int, int, int)>();
        if (meetingIds.Count == 0) return result;

        var att = await Attendees.GetQueryable().AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && meetingIds.Contains(a.MeetingId))
            .GroupBy(a => a.MeetingId)
            .Select(g => new
            {
                MeetingId = g.Key,
                Invited = g.Count(),
                // ⚠ `== true` on a bool?, deliberately. Null means "not yet known", and counting it
                // as attended or as absent would both be inventing a fact.
                Attended = g.Count(a => a.Attended == true),
            })
            .ToListAsync(ct);

        var dec = await Decisions.GetQueryable().AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && meetingIds.Contains(d.MeetingId))
            .GroupBy(d => d.MeetingId)
            .Select(g => new
            {
                MeetingId = g.Key,
                Total = g.Count(),
                WithTask = g.Count(d => d.RaisedTaskId != null),
            })
            .ToListAsync(ct);

        foreach (var id in meetingIds)
        {
            var a = att.FirstOrDefault(x => x.MeetingId == id);
            var d = dec.FirstOrDefault(x => x.MeetingId == id);
            result[id] = (a?.Invited ?? 0, a?.Attended ?? 0, d?.Total ?? 0, d?.WithTask ?? 0);
        }

        return result;
    }

    private static TeamMeetingListDto ToMeetingListDto(
        TeamMeeting m,
        IReadOnlyDictionary<Guid, (int Invited, int Attended, int Decisions, int WithTask)> counts,
        IReadOnlyDictionary<Guid, string> chairNames)
    {
        var (invited, attended, decisions, withTask) =
            counts.TryGetValue(m.Id, out var c) ? c : (0, 0, 0, 0);

        return new TeamMeetingListDto
        {
            Id = m.Id,
            TeamId = m.TeamId,
            Kind = m.Kind,
            Title = m.Title,
            ScheduledAt = m.ScheduledAt,
            HeldAt = m.HeldAt,
            Venue = m.Venue,
            Status = m.Status,
            ChairMemberId = m.ChairMemberId,
            ChairName = m.ChairMemberId is { } id && chairNames.TryGetValue(id, out var n) ? n : null,
            HasMinutesDocument = m.MinutesFileUploadRecordId != null || m.MinutesDocumentRecordId != null,
            MinutesFileName = m.MinutesFileName,
            AttendeeCount = invited,
            AttendedCount = attended,
            DecisionCount = decisions,
            DecisionsWithTaskCount = withTask,
        };
    }

    private static TeamMeetingAttendeeDto ToAttendeeDto(TeamMeetingAttendee a) => new()
    {
        Id = a.Id,
        MeetingId = a.MeetingId,
        MemberId = a.MemberId,
        MemberName = a.Member?.Employee is { } e ? $"{e.FirstName} {e.LastName}".Trim() : null,
        EmployeeId = a.Member?.EmployeeId,
        Attended = a.Attended,
        Apology = a.Apology,
        Notes = a.Notes,
    };

    private static TeamMeetingDecisionDto ToDecisionDto(
        TeamMeetingDecision d, IReadOnlyDictionary<Guid, string> taskTitles) => new()
    {
        Id = d.Id,
        MeetingId = d.MeetingId,
        DisplayOrder = d.DisplayOrder,
        Text = d.Text,
        ResponsibleMemberId = d.ResponsibleMemberId,
        ResponsibleName = d.ResponsibleMember?.Employee is { } e
            ? $"{e.FirstName} {e.LastName}".Trim()
            : null,
        DueDate = d.DueDate,
        RaisedTaskId = d.RaisedTaskId,
        RaisedTaskTitle = d.RaisedTaskId is { } tid && taskTitles.TryGetValue(tid, out var t) ? t : null,
    };

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Decisions, and the task each one can become
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<TeamMeetingDecisionDto> AddDecisionAsync(
        Guid meetingId, CreateTeamMeetingDecisionDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var meeting = await RequireMeetingAsync(meetingId, cancellationToken);
        await _access.RequireWriteAsync(meeting.TeamId, cancellationToken);

        var entity = new TeamMeetingDecision
        {
            TenantId = meeting.TenantId,
            MeetingId = meetingId,
            Text = dto.Text.Trim(),
            DisplayOrder = dto.DisplayOrder,
            DueDate = dto.DueDate,
        };
        entity.ResponsibleMemberId = await _access.ResolveMemberAsync(
            meeting.TeamId, dto.ResponsibleMemberId, "responsible member", cancellationToken);

        await Decisions.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ⚠ Re-read rather than mapping the tracked instance: a just-added row has no navigation
        // loaded, so the create response would carry a blank responsible name beside a list that
        // has it. The same repair lane A had to make on the guarantor create.
        return await ReadDecisionAsync(entity.Id, cancellationToken) ?? ToDecisionDto(entity, Empty);
    }

    private static readonly Dictionary<Guid, string> Empty = new();

    /// <summary>Re-reads one decision with its navigations, for the write responses.</summary>
    private async Task<TeamMeetingDecisionDto?> ReadDecisionAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _access.GetTenantId();
        var row = await Decisions.GetQueryable().AsNoTracking()
            .Include(d => d.ResponsibleMember).ThenInclude(m => m!.Employee)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted, ct);
        if (row is null) return null;

        var titles = row.RaisedTaskId is { } tid
            ? await Tasks.GetQueryable().AsNoTracking()
                .Where(t => t.Id == tid)
                .ToDictionaryAsync(t => t.Id, t => t.Title, ct)
            : Empty;

        return ToDecisionDto(row, titles);
    }

    public async Task<TeamMeetingDecisionDto> UpdateDecisionAsync(
        Guid id, UpdateTeamMeetingDecisionDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var (entity, meeting) = await RequireDecisionAsync(id, cancellationToken);
        await _access.RequireWriteAsync(meeting.TeamId, cancellationToken);

        entity.Text = dto.Text.Trim();
        entity.DisplayOrder = dto.DisplayOrder;
        entity.DueDate = dto.DueDate;
        entity.ResponsibleMemberId = await _access.ResolveMemberAsync(
            meeting.TeamId, dto.ResponsibleMemberId, "responsible member", cancellationToken);

        await Decisions.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReadDecisionAsync(id, cancellationToken) ?? ToDecisionDto(entity, Empty);
    }

    public async Task<bool> DeleteDecisionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (entity, meeting) = await RequireDecisionAsync(id, cancellationToken);
        await _access.RequireWriteAsync(meeting.TeamId, cancellationToken);

        // ⚠ Refused once a task has been raised. Removing the decision would leave the task pointing
        // at a minute that no longer exists — an action item whose reason for existing is gone.
        if (entity.RaisedTaskId is not null)
            throw new InvalidOperationException(
                "A task has been raised from this decision. Cancel or delete the task first, or leave "
                + "the decision in the minutes.");

        await Decisions.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TeamTaskDetailDto> RaiseTaskFromDecisionAsync(
        Guid decisionId, RaiseTaskFromDecisionDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var (decision, meeting) = await RequireDecisionAsync(decisionId, cancellationToken);
        await _access.RequireWriteAsync(meeting.TeamId, cancellationToken);

        if (decision.RaisedTaskId is not null)
            throw new InvalidOperationException(
                "A task has already been raised from this decision.");

        // ⚠ The assignee and the due date come from the DECISION, not the caller. An action item
        // that quietly acquired a different owner from the one the meeting named would be worse
        // than no link at all — the minute would say one thing and the board another.
        var task = await _activity.CreateTaskAsync(meeting.TeamId, new CreateTeamTaskDto
        {
            ObjectiveId = dto.ObjectiveId,
            Title = Clean(dto.Title) ?? Truncate(decision.Text, 300),
            Description = decision.Text,
            AssigneeMemberId = decision.ResponsibleMemberId,
            Priority = dto.Priority,
            DueDate = decision.DueDate,
        }, cancellationToken);

        // Both ends, in one save: the task carries SourceMeetingDecisionId (added by F1 as a bare
        // provenance id for exactly this), and the decision carries the task.
        var taskEntity = await Tasks.GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken);
        if (taskEntity is not null)
        {
            taskEntity.SourceMeetingDecisionId = decision.Id;
            await Tasks.UpdateAsync(taskEntity);
        }

        decision.RaisedTaskId = task.Id;
        await Decisions.UpdateAsync(decision);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} raised from meeting decision {DecisionId}.", task.Id, decision.Id);

        // ⚠ RE-READ. `task` was mapped by CreateTaskAsync BEFORE SourceMeetingDecisionId was
        // written, so returning it would hand the caller a task whose link back to the decision is
        // null — while the database holds it. The screen would render "raised from: nothing".
        return await _activity.GetTaskByIdAsync(task.Id, cancellationToken) ?? task;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private async Task<(TeamMeetingDecision Decision, TeamMeeting Meeting)> RequireDecisionAsync(
        Guid id, CancellationToken ct)
    {
        var tenantId = _access.GetTenantId();
        var decision = await Decisions.GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted, ct)
            ?? throw new ArgumentException($"Decision '{id}' was not found.");

        var meeting = await RequireMeetingAsync(decision.MeetingId, ct);
        return (decision, meeting);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Reviews
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<TeamReviewListDto>> GetReviewsAsync(
        Guid teamId, CancellationToken cancellationToken = default)
    {
        await _access.RequireReadAsync(teamId, cancellationToken);
        var tenantId = _access.GetTenantId();

        var rows = await Reviews.GetQueryable().AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.TeamId == teamId)
            .Include(r => r.ReviewedBy)
            .Include(r => r.AcknowledgedBy)
            .OrderByDescending(r => r.PeriodEnd)
            .ToListAsync(cancellationToken);

        var lineCounts = await ReviewLines.GetQueryable().AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted
                     && rows.Select(r => r.Id).Contains(l.ReviewId))
            .GroupBy(l => l.ReviewId)
            .Select(g => new { ReviewId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ReviewId, x => x.Count, cancellationToken);

        return rows.Select(r => ToReviewListDto(r, lineCounts)).ToList();
    }

    public async Task<TeamReviewDetailDto?> GetReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _access.GetTenantId();
        var row = await Reviews.GetQueryable().AsNoTracking()
            .Include(r => r.ReviewedBy)
            .Include(r => r.AcknowledgedBy)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted, cancellationToken);
        if (row is null) return null;

        await _access.RequireReadAsync(row.TeamId, cancellationToken);

        var lines = await ReviewLines.GetQueryable().AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.ReviewId == id)
            .Include(l => l.Objective)
            .ToListAsync(cancellationToken);

        var list = ToReviewListDto(row, new Dictionary<Guid, int> { [id] = lines.Count });
        return new TeamReviewDetailDto
        {
            Id = list.Id, TeamId = list.TeamId,
            PeriodStart = list.PeriodStart, PeriodEnd = list.PeriodEnd,
            ReviewedByName = list.ReviewedByName, OverallRating = list.OverallRating,
            Status = list.Status, AcknowledgedByName = list.AcknowledgedByName,
            AcknowledgedOn = list.AcknowledgedOn, LineCount = list.LineCount,
            Summary = row.Summary,
            Recommendations = row.Recommendations,
            AcknowledgementNote = row.AcknowledgementNote,
            Lines = lines.Select(ToReviewLineDto).ToList(),
        };
    }

    public async Task<TeamReviewDetailDto> CreateReviewAsync(
        Guid teamId, CreateTeamReviewDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _access.RequireTeamAsync(teamId, cancellationToken);
        await _access.RequireWriteAsync(teamId, cancellationToken);
        RequirePeriod(dto.PeriodStart, dto.PeriodEnd);

        var entity = new TeamReview
        {
            TenantId = _access.GetTenantId(),
            TeamId = teamId,
            PeriodStart = dto.PeriodStart,
            PeriodEnd = dto.PeriodEnd,
            OverallRating = dto.OverallRating,
            Summary = Clean(dto.Summary),
            Recommendations = Clean(dto.Recommendations),
            Status = TeamReviewStatus.Draft,
            // ⚠ From the TOKEN, never a caller-supplied id — the actor-from-token convention this
            // module has had to repair in three other areas.
            ReviewedById = _access.CallerEmployeeId(),
        };

        await Reviews.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetReviewAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TeamReviewDetailDto> UpdateReviewAsync(
        Guid id, UpdateTeamReviewDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await RequireReviewAsync(id, cancellationToken);
        await _access.RequireWriteAsync(entity.TeamId, cancellationToken);
        RequireDraft(entity);
        RequirePeriod(dto.PeriodStart, dto.PeriodEnd);

        entity.PeriodStart = dto.PeriodStart;
        entity.PeriodEnd = dto.PeriodEnd;
        entity.OverallRating = dto.OverallRating;
        entity.Summary = Clean(dto.Summary);
        entity.Recommendations = Clean(dto.Recommendations);

        await Reviews.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetReviewAsync(id, cancellationToken))!;
    }

    public async Task<TeamReviewLineDto> UpsertReviewLineAsync(
        Guid reviewId, UpsertTeamReviewLineDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var review = await RequireReviewAsync(reviewId, cancellationToken);
        await _access.RequireWriteAsync(review.TeamId, cancellationToken);
        RequireDraft(review);

        var tenantId = review.TenantId;

        var objective = await Objectives.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.Id == dto.ObjectiveId && o.TenantId == tenantId && !o.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Objective '{dto.ObjectiveId}' was not found.");

        if (objective.TeamId != review.TeamId)
            throw new InvalidOperationException("That objective belongs to a different team.");

        var line = await ReviewLines.GetQueryable()
            .FirstOrDefaultAsync(
                l => l.TenantId == tenantId && !l.IsDeleted
                  && l.ReviewId == reviewId && l.ObjectiveId == dto.ObjectiveId, cancellationToken);

        // ⚠ `isNew` decides whether the write below is an Add or an Update, and calling BOTH is not
        // harmless: `UpdateAsync` on an entity still in the Added state flips it to Modified, EF
        // then issues an UPDATE for a row that does not exist yet, and SaveChanges throws
        // DbUpdateConcurrencyException — "expected to affect 1 row(s), but actually affected 0".
        // The first run of run-f2.mjs § 5 hit exactly that, as a 500.
        var isNew = line is null;
        line ??= new TeamReviewLine
        {
            TenantId = tenantId,
            ReviewId = reviewId,
            ObjectiveId = dto.ObjectiveId,
        };

        line.Rating = dto.Rating;
        line.Comment = Clean(dto.Comment);

        // ⚠ SNAPSHOT, taken now, not read live and not supplied by the caller. A review says what
        // was true at the time; reading the objective's current figure would silently rewrite every
        // past review each time somebody ticked a task, and letting the caller supply it would let
        // a review claim a figure the objective never held.
        line.ProgressAtReview = objective.ProgressPercent;

        if (isNew) await ReviewLines.AddAsync(line);
        else await ReviewLines.UpdateAsync(line);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        line.Objective = objective;
        return ToReviewLineDto(line);
    }

    public async Task<bool> DeleteReviewLineAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _access.GetTenantId();
        var line = await ReviewLines.GetQueryable()
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId && !l.IsDeleted, cancellationToken);
        if (line is null) return false;

        var review = await RequireReviewAsync(line.ReviewId, cancellationToken);
        await _access.RequireWriteAsync(review.TeamId, cancellationToken);
        RequireDraft(review);

        await ReviewLines.DeleteAsync(line);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TeamReviewDetailDto> SubmitReviewAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireReviewAsync(id, cancellationToken);
        await _access.RequireWriteAsync(entity.TeamId, cancellationToken);
        RequireDraft(entity);

        // A review with no rating and no words is not a review.
        if (entity.OverallRating is null && string.IsNullOrWhiteSpace(entity.Summary))
            throw new InvalidOperationException(
                "A review needs an overall rating or a summary before it can be submitted.");

        entity.Status = TeamReviewStatus.Submitted;
        await Reviews.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetReviewAsync(id, cancellationToken))!;
    }

    public async Task<TeamReviewDetailDto> AcknowledgeReviewAsync(
        Guid id, AcknowledgeTeamReviewDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await RequireReviewAsync(id, cancellationToken);
        await _access.RequireWriteAsync(entity.TeamId, cancellationToken);

        if (entity.Status != TeamReviewStatus.Submitted)
            throw new InvalidOperationException(
                entity.Status == TeamReviewStatus.Draft
                    ? "This review has not been submitted yet."
                    : "This review has already been acknowledged.");

        entity.Status = TeamReviewStatus.Acknowledged;
        entity.AcknowledgedById = _access.CallerEmployeeId();
        entity.AcknowledgedOn = DateTime.UtcNow;
        entity.AcknowledgementNote = Clean(dto.Note);

        await Reviews.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetReviewAsync(id, cancellationToken))!;
    }

    public async Task<bool> DeleteReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireReviewAsync(id, cancellationToken);
        await _access.RequireWriteAsync(entity.TeamId, cancellationToken);

        // ⚠ Only a draft. A submitted review is what somebody found, and the team's record of it.
        if (entity.Status != TeamReviewStatus.Draft)
            throw new InvalidOperationException(
                "A submitted review is part of the team's record and cannot be deleted.");

        await Reviews.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<TeamReview> RequireReviewAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _access.GetTenantId();
        return await Reviews.GetQueryable()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted, ct)
            ?? throw new ArgumentException($"Review '{id}' was not found.");
    }

    /// <remarks>⚠ A submitted review is immutable — that is what makes it a record rather than a draft.</remarks>
    private static void RequireDraft(TeamReview review)
    {
        if (review.Status != TeamReviewStatus.Draft)
            throw new InvalidOperationException(
                $"This review has been {review.Status.ToString().ToLowerInvariant()} and cannot be changed.");
    }

    private static void RequirePeriod(DateOnly start, DateOnly end)
    {
        if (end < start)
            throw new InvalidOperationException("A review period cannot end before it starts.");
    }

    private static TeamReviewListDto ToReviewListDto(
        TeamReview r, IReadOnlyDictionary<Guid, int> lineCounts) => new()
    {
        Id = r.Id,
        TeamId = r.TeamId,
        PeriodStart = r.PeriodStart,
        PeriodEnd = r.PeriodEnd,
        ReviewedByName = r.ReviewedBy is null ? null : $"{r.ReviewedBy.FirstName} {r.ReviewedBy.LastName}".Trim(),
        OverallRating = r.OverallRating,
        Status = r.Status,
        AcknowledgedByName = r.AcknowledgedBy is null
            ? null
            : $"{r.AcknowledgedBy.FirstName} {r.AcknowledgedBy.LastName}".Trim(),
        AcknowledgedOn = r.AcknowledgedOn,
        LineCount = lineCounts.TryGetValue(r.Id, out var n) ? n : 0,
    };

    private static TeamReviewLineDto ToReviewLineDto(TeamReviewLine l) => new()
    {
        Id = l.Id,
        ReviewId = l.ReviewId,
        ObjectiveId = l.ObjectiveId,
        ObjectiveTitle = l.Objective?.Title,
        ProgressAtReview = l.ProgressAtReview,
        Rating = l.Rating,
        Comment = l.Comment,
    };

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Dashboard
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<TeamDashboardDto> GetDashboardAsync(
        Guid teamId, CancellationToken cancellationToken = default)
    {
        await _access.RequireReadAsync(teamId, cancellationToken);
        var team = await _access.RequireTeamAsync(teamId, cancellationToken);
        var tenantId = _access.GetTenantId();

        var today = Today;
        var weekEnd = today.AddDays(7);

        var dto = new TeamDashboardDto { TeamId = teamId, TeamName = team.Name };

        // ── The charter ──────────────────────────────────────────────────────
        var approvedTerms = await Terms.GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == teamId
                     && t.Status == TeamTorStatus.Approved)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (approvedTerms is null)
        {
            // ⚠ Stated rather than left blank. A committee operating with no approved charter is
            // the single most useful thing this dashboard can surface, and a blank tile would read
            // as "not loaded yet".
            dto.HasNoApprovedTerms = true;
        }
        else
        {
            dto.TermsOfReferenceId = approvedTerms.Id;
            dto.TermsVersion = approvedTerms.Version;
            dto.TermsStatus = approvedTerms.Status;
            dto.TermsDaysUntilExpiry = approvedTerms.EffectiveTo is { } to
                ? to.DayNumber - today.DayNumber
                : null;
        }

        // ── Objectives ───────────────────────────────────────────────────────
        var objectives = await Objectives.GetQueryable().AsNoTracking()
            .Where(o => o.TenantId == tenantId && !o.IsDeleted && o.TeamId == teamId)
            .Select(o => new { o.Status, o.DueDate, o.ProgressPercent, o.Weight })
            .ToListAsync(cancellationToken);

        var active = objectives.Where(o => o.Status == TeamObjectiveStatus.Active).ToList();
        dto.ObjectivesActive = active.Count;
        dto.ObjectivesCompleted = objectives.Count(o => o.Status == TeamObjectiveStatus.Completed);
        dto.ObjectivesOverdue = objectives.Count(
            o => o.DueDate is { } d && d < today
              && o.Status is not (TeamObjectiveStatus.Completed or TeamObjectiveStatus.Cancelled));
        dto.AverageProgressPercent = active.Count == 0
            ? 0
            : (int)Math.Round(active.Average(o => o.ProgressPercent), MidpointRounding.AwayFromZero);
        dto.ObjectiveWeightTotal = objectives
            .Where(o => o.Status != TeamObjectiveStatus.Cancelled)
            .Sum(o => o.Weight ?? 0);
        dto.ObjectiveWeightsBalanced = dto.ObjectiveWeightTotal == 100;

        // ── Tasks ────────────────────────────────────────────────────────────
        var tasks = await Tasks.GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == teamId)
            .Select(t => new { t.Status, t.DueDate, t.AssigneeMemberId })
            .ToListAsync(cancellationToken);

        var open = tasks.Where(
            t => t.Status is not (TeamTaskStatus.Completed or TeamTaskStatus.Cancelled)).ToList();
        dto.TasksOpen = open.Count;
        dto.TasksOverdue = open.Count(t => t.DueDate is { } d && d < today);
        dto.TasksDueThisWeek = open.Count(t => t.DueDate is { } d && d >= today && d <= weekEnd);
        dto.TasksBlocked = tasks.Count(t => t.Status == TeamTaskStatus.Blocked);
        // ⚠ The count a lead most needs and least often has: open work nobody owns.
        dto.TasksUnassigned = open.Count(t => t.AssigneeMemberId is null);

        // ── Meetings ─────────────────────────────────────────────────────────
        var nowUtc = DateTime.UtcNow;
        var next = await Meetings.GetQueryable().AsNoTracking()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && m.TeamId == teamId
                     && m.Status == TeamMeetingStatus.Scheduled && m.ScheduledAt >= nowUtc)
            .OrderBy(m => m.ScheduledAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (next is not null)
        {
            dto.NextMeetingId = next.Id;
            dto.NextMeetingTitle = next.Title;
            dto.NextMeetingAt = next.ScheduledAt;
        }

        dto.LastMeetingHeldAt = await Meetings.GetQueryable().AsNoTracking()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && m.TeamId == teamId
                     && m.Status == TeamMeetingStatus.Held)
            .MaxAsync(m => (DateTime?)m.HeldAt, cancellationToken);

        // A decision with somebody responsible and no task is an action item nobody is chasing.
        dto.UnactionedDecisions = await Decisions.GetQueryable().AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted
                     && d.RaisedTaskId == null && d.ResponsibleMemberId != null)
            .Join(Meetings.GetQueryable().Where(m => m.TeamId == teamId && !m.IsDeleted),
                  d => d.MeetingId, m => m.Id, (d, m) => d.Id)
            .CountAsync(cancellationToken);

        // ── Reviews ──────────────────────────────────────────────────────────
        var lastReview = await Reviews.GetQueryable().AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.TeamId == teamId)
            .OrderByDescending(r => r.PeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastReview is not null)
        {
            dto.LastReviewPeriodEnd = lastReview.PeriodEnd;
            dto.LastReviewRating = lastReview.OverallRating;
            dto.LastReviewStatus = lastReview.Status;
        }

        return dto;
    }
}
