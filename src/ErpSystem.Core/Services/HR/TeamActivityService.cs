using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc />
public class TeamActivityService : ITeamActivityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITeamAccessGuard _access;
    private readonly ILogger<TeamActivityService> _logger;

    public TeamActivityService(
        IUnitOfWork unitOfWork,
        ITeamAccessGuard access,
        ILogger<TeamActivityService> logger)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _logger = logger;
    }

    // Tenant scoping and the whole authorisation story live in ITeamAccessGuard, which slice F2's
    // service shares. Two copies of an authorisation rule is the thing lane D2 measured the cost of.
    private Guid GetTenantId() => _access.GetTenantId();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private IGenericRepository<TeamTermsOfReference> Terms => _unitOfWork.Repository<TeamTermsOfReference>();
    private IGenericRepository<TeamObjective> Objectives => _unitOfWork.Repository<TeamObjective>();
    private IGenericRepository<TeamTask> Tasks => _unitOfWork.Repository<TeamTask>();
    private IGenericRepository<TeamTaskChecklistItem> ChecklistItems => _unitOfWork.Repository<TeamTaskChecklistItem>();
    private IGenericRepository<TeamTaskAttachment> Attachments => _unitOfWork.Repository<TeamTaskAttachment>();
    private IGenericRepository<TeamMember> Members => _unitOfWork.Repository<TeamMember>();
    private IGenericRepository<Team> Teams => _unitOfWork.Repository<Team>();

    // ── Authorisation ─────────────────────────────────────────────────────────
    //
    // ⚠ All of it lives in ITeamAccessGuard, shared with slice F2's service. The controller can only
    // ask "may this caller write HR records at all"; the horizontal question — is this caller
    // anything to do with THIS team, and in what capacity — needs the record, so it is answered here
    // on every method.

    public Task<bool> CanReadTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => CanReadAsync(teamId, cancellationToken);

    private async Task<bool> CanReadAsync(Guid teamId, CancellationToken ct)
    {
        if (_access.IsHrDesk()) return true;
        return await _access.CallerMembershipAsync(teamId, ct) is not null;
    }

    private Task RequireTeamReadAsync(Guid teamId, CancellationToken ct) => _access.RequireReadAsync(teamId, ct);

    private Task RequireTeamWriteAsync(Guid teamId, CancellationToken ct) => _access.RequireWriteAsync(teamId, ct);

    private Guid? CallerEmployeeId() => _access.CallerEmployeeId();

    private Task<Team> RequireTeamAsync(Guid teamId, CancellationToken ct) => _access.RequireTeamAsync(teamId, ct);

    private Task<Guid?> ResolveMemberAsync(Guid teamId, Guid? memberId, string what, CancellationToken ct)
        => _access.ResolveMemberAsync(teamId, memberId, what, ct);

    private Task<Dictionary<Guid, string>> MemberNamesAsync(IEnumerable<Guid> memberIds, CancellationToken ct)
        => _access.MemberNamesAsync(memberIds, ct);

    /// <summary>
    /// The write authority, OR the member the task is assigned to.
    /// </summary>
    /// <remarks>
    /// ⚠ Kept here rather than on the guard because it is about a TASK, not a team: it is the whole
    /// reason <c>ChangeTaskStatusAsync</c> is a separate door from the update. Progressing work you
    /// were assigned is an ordinary part of being on a team; rewriting the task record — its title,
    /// its objective, who it belongs to — is not.
    /// </remarks>
    private async Task RequireTaskActorAsync(TeamTask task, CancellationToken ct)
    {
        if (_access.IsHrDesk()) return;

        var membership = await _access.CallerMembershipAsync(task.TeamId, ct);
        if (membership is null)
            throw new UnauthorizedAccessException("You are not a member of this team.");

        if (membership.Role is TeamMemberRole.TeamLead or TeamMemberRole.DeputyLead) return;
        if (task.AssigneeMemberId == membership.Id) return;

        throw new UnauthorizedAccessException(
            "This task is not assigned to you, so only the team's lead or deputy can move it.");
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Terms of reference
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<TeamTermsOfReferenceListDto>> GetTermsAsync(
        Guid teamId, CancellationToken cancellationToken = default)
    {
        await RequireTeamReadAsync(teamId, cancellationToken);
        var tenantId = GetTenantId();

        var rows = await Terms.GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == teamId)
            .Include(t => t.ApprovedBy)
            .OrderByDescending(t => t.Version)
            .ToListAsync(cancellationToken);

        return rows.Select(ToListDto).ToList();
    }

    public async Task<TeamTermsOfReferenceDetailDto?> GetTermsByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var row = await Terms.GetQueryable().AsNoTracking()
            .Include(t => t.ApprovedBy)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
        if (row is null) return null;

        await RequireTeamReadAsync(row.TeamId, cancellationToken);
        return ToDetailDto(row);
    }

    public async Task<TeamTermsOfReferenceDetailDto> CreateTermsAsync(
        Guid teamId, CreateTeamTermsOfReferenceDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await RequireTeamAsync(teamId, cancellationToken);
        await RequireTeamWriteAsync(teamId, cancellationToken);
        RequireSaneWindow(dto.EffectiveFrom, dto.EffectiveTo);

        var tenantId = GetTenantId();

        // ⚠ One draft at a time. Two open drafts of the same charter is two people editing the same
        // document without knowing it, and the version number cannot say which won.
        var openDraft = await Terms.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == teamId
                  && (t.Status == TeamTorStatus.Draft || t.Status == TeamTorStatus.PendingApproval),
                cancellationToken);
        if (openDraft is not null)
            throw new InvalidOperationException(
                $"This team already has terms of reference at version {openDraft.Version} that are not "
                + "approved yet. Finish or discard those first.");

        var latestVersion = await Terms.GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == teamId)
            .Select(t => (int?)t.Version)
            .MaxAsync(cancellationToken) ?? 0;

        var entity = new TeamTermsOfReference
        {
            // Explicit: the context auto-stamp is inert, and an unstamped row fails the Tenants FK.
            TenantId = tenantId,
            TeamId = teamId,
            Version = latestVersion + 1,
            Status = TeamTorStatus.Draft,
        };
        ApplyTerms(entity, dto);

        await Terms.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Terms of reference v{Version} drafted for team {TeamId}.", entity.Version, teamId);
        return ToDetailDto(entity);
    }

    public async Task<TeamTermsOfReferenceDetailDto> UpdateTermsAsync(
        Guid id, UpdateTeamTermsOfReferenceDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await RequireTermsAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);
        RequireSaneWindow(dto.EffectiveFrom, dto.EffectiveTo);

        // ⚠ APPROVED IS IMMUTABLE. This is the whole point of versioning them: editing in place
        // would silently change what the committee was chartered to do last year, and every
        // decision minuted under the old terms would start reading against the new ones.
        if (entity.Status is TeamTorStatus.Approved or TeamTorStatus.Superseded)
            throw new InvalidOperationException(
                $"Version {entity.Version} is {entity.Status.ToString().ToLowerInvariant()} and cannot be "
                + "edited. Take a new version instead — the approved text stays as the record of what "
                + "was agreed.");

        ApplyTerms(entity, dto);
        await Terms.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDetailDto(entity);
    }

    public async Task<TeamTermsOfReferenceDetailDto> SubmitTermsAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireTermsAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);

        if (entity.Status != TeamTorStatus.Draft)
            throw new InvalidOperationException($"Only a draft can be submitted; this one is {entity.Status}.");

        entity.Status = TeamTorStatus.PendingApproval;
        await Terms.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDetailDto(entity);
    }

    public async Task<TeamTermsOfReferenceDetailDto> ApproveTermsAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireTermsAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);

        if (entity.Status is not (TeamTorStatus.Draft or TeamTorStatus.PendingApproval))
            throw new InvalidOperationException(
                $"Version {entity.Version} is already {entity.Status.ToString().ToLowerInvariant()}.");

        var tenantId = GetTenantId();

        // ⚠ Supersede the previous approved version in the same save. A team with two approved
        // charters cannot answer "what are we chartered to do", which is the only question this
        // record exists for.
        var standing = await Terms.GetQueryable()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == entity.TeamId
                     && t.Id != entity.Id && t.Status == TeamTorStatus.Approved)
            .ToListAsync(cancellationToken);
        foreach (var old in standing)
        {
            old.Status = TeamTorStatus.Superseded;
            if (old.EffectiveTo is null || old.EffectiveTo > entity.EffectiveFrom)
                old.EffectiveTo = entity.EffectiveFrom;
            await Terms.UpdateAsync(old);
        }

        entity.Status = TeamTorStatus.Approved;
        entity.ApprovedById = CallerEmployeeId();
        entity.ApprovedOn = DateTime.UtcNow;
        await Terms.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Terms of reference v{Version} approved for team {TeamId}; {Count} superseded.",
            entity.Version, entity.TeamId, standing.Count);

        return await GetTermsByIdAsync(id, cancellationToken) ?? ToDetailDto(entity);
    }

    public async Task<TeamTermsOfReferenceDetailDto> NewTermsVersionAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var source = await RequireTermsAsync(id, cancellationToken);
        await RequireTeamWriteAsync(source.TeamId, cancellationToken);

        if (source.Status != TeamTorStatus.Approved)
            throw new InvalidOperationException(
                "A new version is taken from the APPROVED terms. Edit the draft you already have instead.");

        var tenantId = GetTenantId();

        var openDraft = await Terms.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == source.TeamId
                  && (t.Status == TeamTorStatus.Draft || t.Status == TeamTorStatus.PendingApproval),
                cancellationToken);
        if (openDraft is not null)
            throw new InvalidOperationException(
                $"Version {openDraft.Version} is already open as a draft. Finish or discard it first.");

        var latestVersion = await Terms.GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == source.TeamId)
            .Select(t => (int?)t.Version)
            .MaxAsync(cancellationToken) ?? source.Version;

        var clone = new TeamTermsOfReference
        {
            TenantId = tenantId,
            TeamId = source.TeamId,
            Version = latestVersion + 1,
            PreviousVersionId = source.Id,
            Status = TeamTorStatus.Draft,
            Purpose = source.Purpose,
            Scope = source.Scope,
            Authority = source.Authority,
            MembershipRules = source.MembershipRules,
            MeetingCadence = source.MeetingCadence,
            ReportingLine = source.ReportingLine,
            Deliverables = source.Deliverables,
            EffectiveFrom = Today,
            EffectiveTo = source.EffectiveTo,
            Notes = source.Notes,
            // ⚠ The document is NOT cloned. The signed charter belongs to the version that was
            // signed; carrying its ids forward would make two versions claim the same PDF and the
            // new draft would look approved-and-signed before anyone had read it.
        };

        await Terms.AddAsync(clone);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDetailDto(clone);
    }

    public async Task<bool> DeleteTermsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireTermsAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);

        // ⚠ Only a draft is discardable. An approved or superseded version is the record of what was
        // agreed; it is superseded, never removed.
        if (entity.Status is TeamTorStatus.Approved or TeamTorStatus.Superseded)
            throw new InvalidOperationException(
                $"Version {entity.Version} is {entity.Status.ToString().ToLowerInvariant()} and is part of "
                + "the team's record. It cannot be deleted.");

        await Terms.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AttachTermsDocumentAsync(
        Guid id, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken cancellationToken = default)
    {
        var entity = await RequireTermsAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);

        entity.DocumentFileUploadRecordId = fileUploadRecordId;
        entity.DocumentRecordId = documentRecordId;
        entity.DocumentVersionId = documentVersionId;
        entity.DocumentFileName = fileName;
        entity.DocumentMimeType = mimeType;
        entity.DocumentFileSizeBytes = fileSizeBytes;

        await Terms.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<TeamTermsOfReference> RequireTermsAsync(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        return await Terms.GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new ArgumentException($"Terms of reference '{id}' were not found.");
    }

    private static void RequireSaneWindow(DateOnly from, DateOnly? to)
    {
        if (to is { } end && end < from)
            throw new InvalidOperationException("The terms cannot lapse before they take effect.");
    }

    private static void ApplyTerms(TeamTermsOfReference e, CreateTeamTermsOfReferenceDto dto)
    {
        e.Purpose = dto.Purpose.Trim();
        e.Scope = Clean(dto.Scope);
        e.Authority = Clean(dto.Authority);
        e.MembershipRules = Clean(dto.MembershipRules);
        e.MeetingCadence = Clean(dto.MeetingCadence);
        e.ReportingLine = Clean(dto.ReportingLine);
        e.Deliverables = Clean(dto.Deliverables);
        e.EffectiveFrom = dto.EffectiveFrom;
        e.EffectiveTo = dto.EffectiveTo;
        e.Notes = Clean(dto.Notes);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TeamTermsOfReferenceListDto ToListDto(TeamTermsOfReference e) => new()
    {
        Id = e.Id,
        TeamId = e.TeamId,
        Version = e.Version,
        Status = e.Status,
        EffectiveFrom = e.EffectiveFrom,
        EffectiveTo = e.EffectiveTo,
        ApprovedByName = e.ApprovedBy is null ? null : $"{e.ApprovedBy.FirstName} {e.ApprovedBy.LastName}".Trim(),
        ApprovedOn = e.ApprovedOn,
        HasDocument = e.DocumentFileUploadRecordId != null || e.DocumentRecordId != null,
        DocumentFileName = e.DocumentFileName,
        DaysUntilExpiry = e.EffectiveTo is { } to ? to.DayNumber - Today.DayNumber : null,
    };

    private static TeamTermsOfReferenceDetailDto ToDetailDto(TeamTermsOfReference e)
    {
        var list = ToListDto(e);
        return new TeamTermsOfReferenceDetailDto
        {
            Id = list.Id,
            TeamId = list.TeamId,
            Version = list.Version,
            Status = list.Status,
            EffectiveFrom = list.EffectiveFrom,
            EffectiveTo = list.EffectiveTo,
            ApprovedByName = list.ApprovedByName,
            ApprovedOn = list.ApprovedOn,
            HasDocument = list.HasDocument,
            DocumentFileName = list.DocumentFileName,
            DaysUntilExpiry = list.DaysUntilExpiry,
            PreviousVersionId = e.PreviousVersionId,
            Purpose = e.Purpose,
            Scope = e.Scope,
            Authority = e.Authority,
            MembershipRules = e.MembershipRules,
            MeetingCadence = e.MeetingCadence,
            ReportingLine = e.ReportingLine,
            Deliverables = e.Deliverables,
            Notes = e.Notes,
            DocumentMimeType = e.DocumentMimeType,
            DocumentFileSizeBytes = e.DocumentFileSizeBytes,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Objectives
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<TeamObjectiveListDto>> GetObjectivesAsync(
        Guid teamId, CancellationToken cancellationToken = default)
    {
        await RequireTeamReadAsync(teamId, cancellationToken);
        var tenantId = GetTenantId();

        var rows = await Objectives.GetQueryable().AsNoTracking()
            .Where(o => o.TenantId == tenantId && !o.IsDeleted && o.TeamId == teamId)
            .OrderBy(o => o.Code ?? o.Title)
            .ToListAsync(cancellationToken);

        // ⚠ ONE grouped count for the whole list, not a count per objective. A team with twenty
        // objectives would otherwise issue twenty-one queries to render one tab — the N+1 every
        // list projection in this module has had to be repaired for.
        var counts = await TaskCountsByObjectiveAsync(tenantId, teamId, cancellationToken);
        var names = await MemberNamesAsync(rows.Where(o => o.OwnerMemberId != null).Select(o => o.OwnerMemberId!.Value), cancellationToken);

        return rows.Select(o => ToObjectiveListDto(o, counts, names)).ToList();
    }

    public async Task<TeamObjectiveDetailDto?> GetObjectiveByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var row = await Objectives.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId && !o.IsDeleted, cancellationToken);
        if (row is null) return null;

        await RequireTeamReadAsync(row.TeamId, cancellationToken);

        var counts = await TaskCountsByObjectiveAsync(tenantId, row.TeamId, cancellationToken);
        var names = row.OwnerMemberId is { } ownerId
            ? await MemberNamesAsync(new[] { ownerId }, cancellationToken)
            : new Dictionary<Guid, string>();

        return ToObjectiveDetailDto(row, counts, names);
    }

    public async Task<TeamObjectiveDetailDto> CreateObjectiveAsync(
        Guid teamId, CreateTeamObjectiveDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await RequireTeamAsync(teamId, cancellationToken);
        await RequireTeamWriteAsync(teamId, cancellationToken);

        var tenantId = GetTenantId();
        var code = Clean(dto.Code);
        await RequireObjectiveCodeFreeAsync(tenantId, teamId, code, null, cancellationToken);
        RequireObjectiveDates(dto.StartDate, dto.DueDate);

        var entity = new TeamObjective
        {
            TenantId = tenantId,
            TeamId = teamId,
            Code = code,
            Status = TeamObjectiveStatus.Draft,
            ProgressMode = dto.ProgressMode,
        };
        entity.OwnerMemberId = await ResolveMemberAsync(teamId, dto.OwnerMemberId, "owner", cancellationToken);
        ApplyObjective(entity, dto);
        ApplySuppliedProgress(entity, dto);

        await Objectives.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetObjectiveByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TeamObjectiveDetailDto> UpdateObjectiveAsync(
        Guid id, UpdateTeamObjectiveDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await RequireObjectiveAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);

        if (entity.Status is TeamObjectiveStatus.Completed or TeamObjectiveStatus.Cancelled)
            throw new InvalidOperationException(
                $"This objective is {entity.Status.ToString().ToLowerInvariant()} and cannot be edited.");

        var code = Clean(dto.Code);
        await RequireObjectiveCodeFreeAsync(entity.TenantId, entity.TeamId, code, id, cancellationToken);
        RequireObjectiveDates(dto.StartDate, dto.DueDate);

        entity.Code = code;
        entity.ProgressMode = dto.ProgressMode;
        entity.OwnerMemberId = await ResolveMemberAsync(entity.TeamId, dto.OwnerMemberId, "owner", cancellationToken);
        ApplyObjective(entity, dto);
        ApplySuppliedProgress(entity, dto);

        // Switching TO FromTasks recomputes immediately, so the figure on screen is never a stale
        // manual number wearing a derived label.
        if (entity.ProgressMode == TeamObjectiveProgressMode.FromTasks)
            await RecomputeObjectiveProgressAsync(entity, cancellationToken);

        await Objectives.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetObjectiveByIdAsync(id, cancellationToken))!;
    }

    public async Task<TeamObjectiveDetailDto> ChangeObjectiveStatusAsync(
        Guid id, TeamObjectiveStatus status, TeamObjectiveStatusChangeDto body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        var entity = await RequireObjectiveAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);

        if (entity.Status == status)
            throw new InvalidOperationException($"This objective is already {status.ToString().ToLowerInvariant()}.");

        if (entity.Status is TeamObjectiveStatus.Completed or TeamObjectiveStatus.Cancelled)
            throw new InvalidOperationException(
                $"This objective is {entity.Status.ToString().ToLowerInvariant()} and cannot be moved again.");

        switch (status)
        {
            case TeamObjectiveStatus.Completed:
                if (entity.ProgressMode == TeamObjectiveProgressMode.FromTasks)
                    await RecomputeObjectiveProgressAsync(entity, cancellationToken);

                // ⚠ "Whether they have done it, AND how they did it" — the feedback's words. An
                // objective closed short of its target owes the second answer; one at 100 % has
                // already given it in the tasks.
                if (entity.ProgressPercent < 100 && string.IsNullOrWhiteSpace(body.OutcomeSummary))
                    throw new InvalidOperationException(
                        $"This objective is at {entity.ProgressPercent}%. To complete it short of the "
                        + "target, say how the team achieved what it did.");

                entity.OutcomeSummary = Clean(body.OutcomeSummary) ?? entity.OutcomeSummary;
                entity.CompletedOn = Today;
                break;

            case TeamObjectiveStatus.Cancelled:
                if (string.IsNullOrWhiteSpace(body.CancelledReason))
                    throw new InvalidOperationException("Say why the objective is being cancelled.");
                entity.CancelledReason = Clean(body.CancelledReason);
                break;

            case TeamObjectiveStatus.Active:
            case TeamObjectiveStatus.OnHold:
            case TeamObjectiveStatus.Draft:
            case TeamObjectiveStatus.PendingApproval:
                break;
        }

        entity.Status = status;
        await Objectives.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetObjectiveByIdAsync(id, cancellationToken))!;
    }

    public async Task<bool> DeleteObjectiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireObjectiveAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);

        var tenantId = GetTenantId();

        // ⚠ Refused while tasks hang off it, with a count. The foreign key is Restrict, so a hard
        // delete would fail at the database with a constraint name no user can act on; and this
        // store SOFT-deletes, so the row would otherwise vanish from every read while live tasks
        // still held its id — the exact failure the geography module was built to end.
        var taskCount = await Tasks.GetQueryable().AsNoTracking()
            .CountAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.ObjectiveId == id, cancellationToken);
        if (taskCount > 0)
            throw new InvalidOperationException(
                $"{taskCount} task{(taskCount == 1 ? "" : "s")} sit{(taskCount == 1 ? "s" : "")} under this "
                + "objective. Move or remove them first, or cancel the objective instead.");

        await Objectives.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(int Total, bool IsBalanced)> GetObjectiveWeightTotalAsync(
        Guid teamId, CancellationToken cancellationToken = default)
    {
        await RequireTeamReadAsync(teamId, cancellationToken);
        var tenantId = GetTenantId();

        // ⚠ Summed as int? and coalesced. SQL's SUM over no rows returns NULL, and EF throws
        // mapping that to a non-nullable int — a team with no weighted objectives yet is the
        // ordinary state of a team that was created five minutes ago, not an edge case.
        var total = await Objectives.GetQueryable().AsNoTracking()
            .Where(o => o.TenantId == tenantId && !o.IsDeleted && o.TeamId == teamId
                     && o.Status != TeamObjectiveStatus.Cancelled)
            .SumAsync(o => (int?)o.Weight, cancellationToken) ?? 0;

        return (total, total == 100);
    }

    private async Task<TeamObjective> RequireObjectiveAsync(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        return await Objectives.GetQueryable()
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId && !o.IsDeleted, ct)
            ?? throw new ArgumentException($"Objective '{id}' was not found.");
    }

    private async Task RequireObjectiveCodeFreeAsync(
        Guid tenantId, Guid teamId, string? code, Guid? excludeId, CancellationToken ct)
    {
        if (code is null) return;

        var query = Objectives.GetQueryable().AsNoTracking()
            .Where(o => o.TenantId == tenantId && !o.IsDeleted && o.TeamId == teamId && o.Code == code);
        if (excludeId is { } id) query = query.Where(o => o.Id != id);

        if (await query.AnyAsync(ct))
            throw new InvalidOperationException($"This team already has an objective coded '{code}'.");
    }

    private static void RequireObjectiveDates(DateOnly start, DateOnly? due)
    {
        if (due is { } d && d < start)
            throw new InvalidOperationException("An objective cannot be due before it starts.");
    }

    private static void ApplyObjective(TeamObjective e, CreateTeamObjectiveDto dto)
    {
        e.Title = dto.Title.Trim();
        e.Description = Clean(dto.Description);
        e.Measure = Clean(dto.Measure);
        e.TargetValue = dto.TargetValue;
        e.Unit = Clean(dto.Unit);
        e.Weight = dto.Weight;
        e.StartDate = dto.StartDate;
        e.DueDate = dto.DueDate;
    }

    /// <remarks>
    /// ⚠ A supplied progress figure under <c>FromTasks</c> is REFUSED, not ignored. Silently
    /// dropping input is how a field comes to look editable while doing nothing — the defect lane
    /// D1 spent its budget removing from the probation form.
    /// </remarks>
    private static void ApplySuppliedProgress(TeamObjective e, CreateTeamObjectiveDto dto)
    {
        if (dto.ProgressPercent is not int supplied) return;

        if (e.ProgressMode == TeamObjectiveProgressMode.FromTasks)
            throw new InvalidOperationException(
                "This objective's progress is counted from its tasks, so it cannot be typed. Switch it "
                + "to manual progress first, or leave the figure out.");

        e.ProgressPercent = supplied;
    }

    /// <summary>Recomputes a <c>FromTasks</c> objective from the tasks under it.</summary>
    /// <remarks>
    /// ⚠ Cancelled tasks are excluded from BOTH sides. Counting them in the denominator would make
    /// an objective's progress fall when a task nobody is doing any more is cancelled, which reads
    /// as the team going backwards for tidying up.
    /// </remarks>
    private async Task RecomputeObjectiveProgressAsync(TeamObjective objective, CancellationToken ct)
    {
        if (objective.ProgressMode != TeamObjectiveProgressMode.FromTasks) return;

        var tenantId = objective.TenantId;
        var rows = await Tasks.GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.ObjectiveId == objective.Id
                     && t.Status != TeamTaskStatus.Cancelled)
            .Select(t => t.Status)
            .ToListAsync(ct);

        objective.ProgressPercent = rows.Count == 0
            ? 0
            : (int)Math.Round(rows.Count(s => s == TeamTaskStatus.Completed) * 100.0 / rows.Count,
                MidpointRounding.AwayFromZero);
    }

    /// <summary>Recomputes the objective a task belongs to, if it belongs to one.</summary>
    /// <remarks>
    /// <para><b>⚠ CALL THIS AFTER SaveChangesAsync, NEVER BEFORE.</b> It counts the objective's
    /// tasks with a QUERY, and a query goes to the database — which does not yet hold a change that
    /// is only sitting in the change tracker. Calling it first makes every figure exactly one
    /// action stale: complete a task and the objective still reads 0%, complete a second and it
    /// reads 50%. Measured, not theorised — three assertions in <c>run-f1.mjs</c> § 5 caught it on
    /// the first run, and on a screen it would have looked like a caching bug rather than an
    /// ordering one.</para>
    ///
    /// <para>The cost is a second <c>SaveChangesAsync</c> per task write. Worth it: the alternative
    /// is folding the pending change into the count by hand at four call sites, which is four
    /// chances to get it subtly wrong.</para>
    /// </remarks>
    private async Task RecomputeForTaskAsync(Guid? objectiveId, CancellationToken ct)
    {
        if (objectiveId is not Guid id) return;

        var tenantId = GetTenantId();
        var objective = await Objectives.GetQueryable()
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId && !o.IsDeleted, ct);
        if (objective is null || objective.ProgressMode != TeamObjectiveProgressMode.FromTasks) return;

        await RecomputeObjectiveProgressAsync(objective, ct);
        await Objectives.UpdateAsync(objective);
    }

    private async Task<Dictionary<Guid, (int Total, int Done)>> TaskCountsByObjectiveAsync(
        Guid tenantId, Guid teamId, CancellationToken ct)
    {
        var rows = await Tasks.GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == teamId && t.ObjectiveId != null
                     && t.Status != TeamTaskStatus.Cancelled)
            .GroupBy(t => t.ObjectiveId!.Value)
            .Select(g => new
            {
                ObjectiveId = g.Key,
                Total = g.Count(),
                Done = g.Count(t => t.Status == TeamTaskStatus.Completed),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.ObjectiveId, r => (r.Total, r.Done));
    }

    private static TeamObjectiveListDto ToObjectiveListDto(
        TeamObjective o,
        IReadOnlyDictionary<Guid, (int Total, int Done)> counts,
        IReadOnlyDictionary<Guid, string> names)
    {
        var (total, done) = counts.TryGetValue(o.Id, out var c) ? c : (0, 0);
        return new TeamObjectiveListDto
        {
            Id = o.Id,
            TeamId = o.TeamId,
            Code = o.Code,
            Title = o.Title,
            Status = o.Status,
            ProgressPercent = o.ProgressPercent,
            ProgressMode = o.ProgressMode,
            Weight = o.Weight,
            StartDate = o.StartDate,
            DueDate = o.DueDate,
            OwnerMemberId = o.OwnerMemberId,
            OwnerName = o.OwnerMemberId is { } id && names.TryGetValue(id, out var n) ? n : null,
            TaskCount = total,
            CompletedTaskCount = done,
            IsOverdue = o.DueDate is { } due
                        && due < Today
                        && o.Status is not (TeamObjectiveStatus.Completed or TeamObjectiveStatus.Cancelled),
        };
    }

    private static TeamObjectiveDetailDto ToObjectiveDetailDto(
        TeamObjective o,
        IReadOnlyDictionary<Guid, (int Total, int Done)> counts,
        IReadOnlyDictionary<Guid, string> names)
    {
        var list = ToObjectiveListDto(o, counts, names);
        return new TeamObjectiveDetailDto
        {
            Id = list.Id,
            TeamId = list.TeamId,
            Code = list.Code,
            Title = list.Title,
            Status = list.Status,
            ProgressPercent = list.ProgressPercent,
            ProgressMode = list.ProgressMode,
            Weight = list.Weight,
            StartDate = list.StartDate,
            DueDate = list.DueDate,
            OwnerMemberId = list.OwnerMemberId,
            OwnerName = list.OwnerName,
            TaskCount = list.TaskCount,
            CompletedTaskCount = list.CompletedTaskCount,
            IsOverdue = list.IsOverdue,
            Description = o.Description,
            Measure = o.Measure,
            TargetValue = o.TargetValue,
            Unit = o.Unit,
            OutcomeSummary = o.OutcomeSummary,
            CompletedOn = o.CompletedOn,
            CancelledReason = o.CancelledReason,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Tasks
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<TeamTaskListDto>> GetTasksAsync(
        Guid teamId, Guid? objectiveId = null, bool mineOnly = false, CancellationToken cancellationToken = default)
    {
        await RequireTeamReadAsync(teamId, cancellationToken);
        var tenantId = GetTenantId();

        var query = Tasks.GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TeamId == teamId);

        if (objectiveId is { } oid) query = query.Where(t => t.ObjectiveId == oid);

        if (mineOnly)
        {
            // ⚠ Resolved from the TOKEN, never from a caller-supplied member id. "Mine" that a
            // caller can define is not a filter, it is a way to read anyone's list.
            var membership = await _access.CallerMembershipAsync(teamId, cancellationToken);
            if (membership is null) return Array.Empty<TeamTaskListDto>();
            query = query.Where(t => t.AssigneeMemberId == membership.Id);
        }

        var rows = await query
            .Include(t => t.Objective)
            .Include(t => t.AssigneeMember).ThenInclude(m => m!.Employee)
            .OrderBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(t => t.Priority)
            .ToListAsync(cancellationToken);

        // One grouped count for the whole board, not two queries per card.
        var extras = await TaskExtrasAsync(tenantId, rows.Select(r => r.Id).ToList(), cancellationToken);

        return rows.Select(t => ToTaskListDto(t, extras)).ToList();
    }

    public async Task<TeamTaskDetailDto?> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var row = await Tasks.GetQueryable().AsNoTracking()
            .Include(t => t.Objective)
            .Include(t => t.AssigneeMember).ThenInclude(m => m!.Employee)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
        if (row is null) return null;

        await RequireTeamReadAsync(row.TeamId, cancellationToken);

        var items = await ChecklistItems.GetQueryable().AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.TaskId == id)
            .Include(c => c.DoneBy)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        var files = await Attachments.GetQueryable().AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.TaskId == id)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var extras = await TaskExtrasAsync(tenantId, new List<Guid> { id }, cancellationToken);
        var list = ToTaskListDto(row, extras);

        return new TeamTaskDetailDto
        {
            Id = list.Id,
            TeamId = list.TeamId,
            ObjectiveId = list.ObjectiveId,
            ObjectiveTitle = list.ObjectiveTitle,
            Title = list.Title,
            Status = list.Status,
            Priority = list.Priority,
            StartDate = list.StartDate,
            DueDate = list.DueDate,
            AssigneeMemberId = list.AssigneeMemberId,
            AssigneeName = list.AssigneeName,
            AssigneeEmployeeId = list.AssigneeEmployeeId,
            BlockedReason = list.BlockedReason,
            IsOverdue = list.IsOverdue,
            ChecklistTotal = list.ChecklistTotal,
            ChecklistDone = list.ChecklistDone,
            AttachmentCount = list.AttachmentCount,
            Description = row.Description,
            CompletedOn = row.CompletedOn,
            CompletionNotes = row.CompletionNotes,
            SourceMeetingDecisionId = row.SourceMeetingDecisionId,
            ChecklistItems = items.Select(ToChecklistDto).ToList(),
            Attachments = files.Select(ToAttachmentDto).ToList(),
        };
    }

    public async Task<TeamTaskDetailDto> CreateTaskAsync(
        Guid teamId, CreateTeamTaskDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await RequireTeamAsync(teamId, cancellationToken);
        await RequireTeamWriteAsync(teamId, cancellationToken);
        RequireTaskDates(dto.StartDate, dto.DueDate);

        var tenantId = GetTenantId();

        var entity = new TeamTask
        {
            TenantId = tenantId,
            TeamId = teamId,
            Status = TeamTaskStatus.NotStarted,
        };
        entity.ObjectiveId = await ResolveObjectiveForTaskAsync(teamId, dto.ObjectiveId, cancellationToken);
        entity.AssigneeMemberId = await ResolveMemberAsync(teamId, dto.AssigneeMemberId, "assignee", cancellationToken);
        ApplyTask(entity, dto);

        await Tasks.AddAsync(entity);
        // ⚠ Saved BEFORE the recompute — the new task is not in the database until it is, and the
        // count would be short by one. See RecomputeForTaskAsync.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeForTaskAsync(entity.ObjectiveId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetTaskByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TeamTaskDetailDto> UpdateTaskAsync(
        Guid id, UpdateTeamTaskDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await RequireTaskAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);
        RequireTaskDates(dto.StartDate, dto.DueDate);

        var previousObjectiveId = entity.ObjectiveId;

        entity.ObjectiveId = await ResolveObjectiveForTaskAsync(entity.TeamId, dto.ObjectiveId, cancellationToken);
        entity.AssigneeMemberId = await ResolveMemberAsync(entity.TeamId, dto.AssigneeMemberId, "assignee", cancellationToken);
        ApplyTask(entity, dto);

        await Tasks.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ⚠ BOTH objectives, when the task moved between them. Recomputing only the new one leaves
        // the old objective's progress quoting a task it no longer owns.
        // ⚠ And AFTER the save above — see RecomputeForTaskAsync.
        await RecomputeForTaskAsync(entity.ObjectiveId, cancellationToken);
        if (previousObjectiveId != entity.ObjectiveId)
            await RecomputeForTaskAsync(previousObjectiveId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetTaskByIdAsync(id, cancellationToken))!;
    }

    public async Task<TeamTaskDetailDto> ChangeTaskStatusAsync(
        Guid id, TeamTaskStatusChangeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await RequireTaskAsync(id, cancellationToken);

        // ⚠ The one door an ordinary member may use, and only on their own task.
        await RequireTaskActorAsync(entity, cancellationToken);

        if (dto.Status == TeamTaskStatus.Blocked && string.IsNullOrWhiteSpace(dto.BlockedReason))
            throw new InvalidOperationException(
                "Say what is blocking this task. A board full of blocked cards that cannot say why "
                + "tells the lead nothing.");

        entity.Status = dto.Status;
        entity.BlockedReason = dto.Status == TeamTaskStatus.Blocked ? Clean(dto.BlockedReason) : null;

        if (dto.Status == TeamTaskStatus.Completed)
        {
            entity.CompletedOn = Today;
            entity.CompletionNotes = Clean(dto.CompletionNotes) ?? entity.CompletionNotes;
        }
        else
        {
            // Re-opening a task clears the completion date; a task that is not done did not finish
            // on a date.
            entity.CompletedOn = null;
        }

        await Tasks.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeForTaskAsync(entity.ObjectiveId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetTaskByIdAsync(id, cancellationToken))!;
    }

    public async Task<bool> DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireTaskAsync(id, cancellationToken);
        await RequireTeamWriteAsync(entity.TeamId, cancellationToken);

        var objectiveId = entity.ObjectiveId;
        await Tasks.DeleteAsync(entity);
        // ⚠ The delete is SOFT, so until it is saved the row still reads as live to a query.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeForTaskAsync(objectiveId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<TeamTask> RequireTaskAsync(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        return await Tasks.GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new ArgumentException($"Task '{id}' was not found.");
    }

    /// <remarks>
    /// ⚠ Scoped to the team. An objective id from another team would otherwise resolve, and the
    /// task would count towards a progress figure on a board nobody involved can see.
    /// </remarks>
    private async Task<Guid?> ResolveObjectiveForTaskAsync(Guid teamId, Guid? objectiveId, CancellationToken ct)
    {
        if (objectiveId is not Guid id) return null;
        var tenantId = GetTenantId();

        var objective = await Objectives.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId && !o.IsDeleted, ct)
            ?? throw new ArgumentException($"Objective '{id}' was not found.");

        if (objective.TeamId != teamId)
            throw new InvalidOperationException("That objective belongs to a different team.");

        return id;
    }

    private static void RequireTaskDates(DateOnly? start, DateOnly? due)
    {
        if (start is { } s && due is { } d && d < s)
            throw new InvalidOperationException("A task cannot be due before it starts.");
    }

    private static void ApplyTask(TeamTask e, CreateTeamTaskDto dto)
    {
        e.Title = dto.Title.Trim();
        e.Description = Clean(dto.Description);
        e.Priority = dto.Priority;
        e.StartDate = dto.StartDate;
        e.DueDate = dto.DueDate;
    }

    private async Task<Dictionary<Guid, (int Total, int Done, int Files)>> TaskExtrasAsync(
        Guid tenantId, List<Guid> taskIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, (int Total, int Done, int Files)>();
        if (taskIds.Count == 0) return result;

        var checks = await ChecklistItems.GetQueryable().AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && taskIds.Contains(c.TaskId))
            .GroupBy(c => c.TaskId)
            .Select(g => new { TaskId = g.Key, Total = g.Count(), Done = g.Count(c => c.IsDone) })
            .ToListAsync(ct);

        var files = await Attachments.GetQueryable().AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && taskIds.Contains(a.TaskId))
            .GroupBy(a => a.TaskId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var id in taskIds)
        {
            var c = checks.FirstOrDefault(x => x.TaskId == id);
            var f = files.FirstOrDefault(x => x.TaskId == id);
            result[id] = (c?.Total ?? 0, c?.Done ?? 0, f?.Count ?? 0);
        }

        return result;
    }

    private static TeamTaskListDto ToTaskListDto(
        TeamTask t, IReadOnlyDictionary<Guid, (int Total, int Done, int Files)> extras)
    {
        var (total, done, files) = extras.TryGetValue(t.Id, out var e) ? e : (0, 0, 0);
        return new TeamTaskListDto
        {
            Id = t.Id,
            TeamId = t.TeamId,
            ObjectiveId = t.ObjectiveId,
            ObjectiveTitle = t.Objective?.Title,
            Title = t.Title,
            Status = t.Status,
            Priority = t.Priority,
            StartDate = t.StartDate,
            DueDate = t.DueDate,
            AssigneeMemberId = t.AssigneeMemberId,
            AssigneeName = t.AssigneeMember?.Employee is { } emp
                ? $"{emp.FirstName} {emp.LastName}".Trim()
                : null,
            AssigneeEmployeeId = t.AssigneeMember?.EmployeeId,
            BlockedReason = t.BlockedReason,
            IsOverdue = t.DueDate is { } due
                        && due < Today
                        && t.Status is not (TeamTaskStatus.Completed or TeamTaskStatus.Cancelled),
            ChecklistTotal = total,
            ChecklistDone = done,
            AttachmentCount = files,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Checklist
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<TeamTaskChecklistItemDto> AddChecklistItemAsync(
        Guid taskId, CreateTeamTaskChecklistItemDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var task = await RequireTaskAsync(taskId, cancellationToken);
        await RequireTaskActorAsync(task, cancellationToken);

        var entity = new TeamTaskChecklistItem
        {
            // ⚠ Added through the item's OWN repository, never by appending to a tracked task's
            // navigation collection — that turns the add into an UPDATE of the task and can
            // resurrect a soft-deleted sibling through fixup (the SHE checklist-builder lesson).
            TenantId = GetTenantId(),
            TaskId = taskId,
            Text = dto.Text.Trim(),
            DisplayOrder = dto.DisplayOrder,
        };

        await ChecklistItems.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToChecklistDto(entity);
    }

    public async Task<TeamTaskChecklistItemDto> UpdateChecklistItemAsync(
        Guid id, UpdateTeamTaskChecklistItemDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var entity = await ChecklistItems.GetQueryable()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Checklist item '{id}' was not found.");

        var task = await RequireTaskAsync(entity.TaskId, cancellationToken);
        await RequireTaskActorAsync(task, cancellationToken);

        entity.Text = dto.Text.Trim();
        entity.DisplayOrder = dto.DisplayOrder;

        // ⚠ Who ticked it and when, from the TOKEN. A tick nobody owns is worse than no tick, and
        // un-ticking wipes both rather than leaving a stale name beside an empty box.
        if (dto.IsDone && !entity.IsDone)
        {
            entity.IsDone = true;
            entity.DoneById = CallerEmployeeId();
            entity.DoneAt = DateTime.UtcNow;
        }
        else if (!dto.IsDone && entity.IsDone)
        {
            entity.IsDone = false;
            entity.DoneById = null;
            entity.DoneAt = null;
        }

        await ChecklistItems.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reread = await ChecklistItems.GetQueryable().AsNoTracking()
            .Include(c => c.DoneBy)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return ToChecklistDto(reread ?? entity);
    }

    public async Task<bool> DeleteChecklistItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await ChecklistItems.GetQueryable()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        if (entity is null) return false;

        var task = await RequireTaskAsync(entity.TaskId, cancellationToken);
        await RequireTaskActorAsync(task, cancellationToken);

        await ChecklistItems.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static TeamTaskChecklistItemDto ToChecklistDto(TeamTaskChecklistItem c) => new()
    {
        Id = c.Id,
        TaskId = c.TaskId,
        DisplayOrder = c.DisplayOrder,
        Text = c.Text,
        IsDone = c.IsDone,
        DoneByName = c.DoneBy is null ? null : $"{c.DoneBy.FirstName} {c.DoneBy.LastName}".Trim(),
        DoneAt = c.DoneAt,
    };

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Attachments
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<TeamTaskAttachmentDto> AddTaskAttachmentAsync(
        Guid taskId, string? title, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(taskId, cancellationToken);
        await RequireTaskActorAsync(task, cancellationToken);

        var entity = new TeamTaskAttachment
        {
            TenantId = GetTenantId(),
            TaskId = taskId,
            Title = Clean(title),
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
            FileName = fileName,
            MimeType = mimeType,
            FileSizeBytes = fileSizeBytes,
            UploadedById = CallerEmployeeId(),
        };

        await Attachments.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToAttachmentDto(entity);
    }

    public async Task<TeamTaskAttachmentDto?> GetTaskAttachmentAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await Attachments.GetQueryable().AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (entity is null) return null;

        var task = await RequireTaskAsync(entity.TaskId, cancellationToken);
        await RequireTeamReadAsync(task.TeamId, cancellationToken);

        return ToAttachmentDto(entity);
    }

    public async Task<bool> DeleteTaskAttachmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await Attachments.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (entity is null) return false;

        var task = await RequireTaskAsync(entity.TaskId, cancellationToken);
        await RequireTaskActorAsync(task, cancellationToken);

        await Attachments.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<HrStoredFileRef?> GetTermsDocumentRefAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var row = await Terms.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
        if (row is null) return null;

        // ⚠ Entitlement before bytes. Neither the gate nor the DMS asks whether this caller may see
        // the parent record, so a download that skipped this would serve any team's signed charter
        // to anyone who could guess a row id.
        await RequireTeamReadAsync(row.TeamId, cancellationToken);

        if (row.DocumentRecordId is null && row.DocumentFileUploadRecordId is null) return null;

        return new HrStoredFileRef(
            row.DocumentRecordId, row.DocumentVersionId, row.DocumentFileUploadRecordId,
            row.DocumentFileName, row.DocumentMimeType);
    }

    public async Task<HrStoredFileRef?> GetTaskAttachmentRefAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var row = await Attachments.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (row is null) return null;

        var task = await RequireTaskAsync(row.TaskId, cancellationToken);
        await RequireTeamReadAsync(task.TeamId, cancellationToken);

        if (row.DocumentRecordId is null && row.FileUploadRecordId is null) return null;

        return new HrStoredFileRef(
            row.DocumentRecordId, row.DocumentVersionId, row.FileUploadRecordId,
            row.FileName, row.MimeType);
    }

    private static TeamTaskAttachmentDto ToAttachmentDto(TeamTaskAttachment a) => new()
    {
        Id = a.Id,
        TaskId = a.TaskId,
        Title = a.Title,
        FileName = a.FileName,
        MimeType = a.MimeType,
        FileSizeBytes = a.FileSizeBytes,
        UploadedByName = a.UploadedBy is null ? null : $"{a.UploadedBy.FirstName} {a.UploadedBy.LastName}".Trim(),
        CreatedAt = a.CreatedAt,
    };
}
