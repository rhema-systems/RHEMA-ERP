using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed class CivilEngineeringSupervisionService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService) : ICivilEngineeringSupervisionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringProjectEngineerAssignmentLookupsDto> GetProjectEngineerAssignmentLookupsAsync(
        Guid projectId,
        CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        await RequireAssignmentAuthorityAsync(projectId, policy, token);
        var candidates = await GetEligibleCandidatesAsync(projectId, policy, token);
        return new CivilEngineeringProjectEngineerAssignmentLookupsDto
        {
            Candidates = candidates,
            Authorities = CivilEngineeringProjectEngineerAssignmentPolicy.Authorities,
            DefaultAuthority = CivilEngineeringProjectEngineerAuthority.SiteSupervisionAndInstructions
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringProjectEngineerAssignmentDto>> ListProjectEngineerAssignmentsAsync(
        Guid projectId,
        CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        var users = await UsersByIdAsync(
            await db.ProjectCivilProjectEngineerAssignments.AsNoTracking()
                .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted)
                .OrderByDescending(value => value.IsActive).ThenByDescending(value => value.EffectiveFrom)
                .Select(value => value.AssignedUserId).Distinct().ToListAsync(token), token);

        var assignments = await db.ProjectCivilProjectEngineerAssignments.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted)
            .OrderByDescending(value => value.IsActive).ThenByDescending(value => value.EffectiveFrom)
            .ToListAsync(token);

        return assignments.Select(value => Map(value, users)).ToList();
    }

    public async Task<CivilEngineeringProjectEngineerAssignmentDto> AssignProjectEngineerAsync(
        Guid projectId,
        AssignCivilEngineeringProjectEngineerRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        if (request.AssignedUserId == Guid.Empty)
            throw Validation("Select a Civil Engineer or Supervising Civil Engineer.");

        await RequireProjectAsync(projectId);
        var effectiveFrom = UtcDate(request.EffectiveFrom);
        var requestHash = Hash(new
        {
            projectId,
            request.AssignedUserId,
            request.Authority,
            effectiveFrom,
            reason = Clean(request.Reason, 2000)
        });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await db.ProjectCivilProjectEngineerAssignments
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash))
                throw Conflict("This client request identifier was already used with different appointment details.");
            await transaction.CommitAsync(token);
            return await GetAssignmentAsync(retry.Id, token);
        }

        var policy = await ResolvePolicyAsync(effectiveFrom, token);
        await RequireAssignmentAuthorityAsync(projectId, policy, token);
        var current = await db.ProjectCivilProjectEngineerAssignments
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsActive && !value.IsDeleted, token);
        var assignmentErrors = CivilEngineeringProjectEngineerAssignmentPolicy.ValidateAssignment(
            effectiveFrom,
            request.Authority,
            current?.EffectiveFrom);
        if (assignmentErrors.Count > 0)
            throw Validation(assignmentErrors);
        if (current is not null && string.IsNullOrWhiteSpace(request.Reason))
            throw Validation("Provide a reason when reassigning the Project Engineer.");
        if (current?.AssignedUserId == request.AssignedUserId)
            throw Conflict("The selected user is already the active Project Engineer. End the appointment before creating a new one.");

        var candidate = await GetCandidateAsync(projectId, request.AssignedUserId, policy, token);
        var candidateErrors = CivilEngineeringProjectEngineerAssignmentPolicy.ValidateCandidate(candidate);
        if (candidateErrors.Count > 0)
            throw Validation(candidateErrors);

        var projectMember = await EnsureProjectEngineerMembershipAsync(projectId, request.AssignedUserId, token);
        var now = DateTime.UtcNow;
        if (current is not null)
        {
            var before = Snapshot(current);
            current.IsActive = false;
            current.EffectiveTo = effectiveFrom.AddTicks(-1);
            current.Reason = Clean(request.Reason, 2000);
            current.UpdatedAt = now;
            current.UpdatedBy = UserName;
            current.LastModifiedById = UserId;
            AddRevision(current, CivilEngineeringAuditEventMap.UpdateProjectEngineerAssignment, before, Snapshot(current), request.Reason, correlationId);
            AddAudit(current, CivilEngineeringAuditEventMap.UpdateProjectEngineerAssignment, before, Snapshot(current), correlationId);
            await DeactivateProjectEngineerMembershipAsync(current.ProjectMemberId, token);

            // The active-assignment index is intentionally strict. Persist the
            // governed closure first, still inside this serializable transaction,
            // before adding its replacement so provider command ordering cannot
            // briefly create two active Project Engineers for one project.
            await SaveAsync(token);
        }

        var assignment = new ProjectCivilProjectEngineerAssignment
        {
            TenantId = TenantId,
            ProjectId = projectId,
            ProjectMemberId = projectMember.Id,
            AssignedUserId = request.AssignedUserId,
            SourceCivilRole = candidate.RoleName,
            ProjectRole = CivilEngineeringAccessControlRegistry.ProjectEngineerRole,
            Authority = request.Authority,
            EffectiveFrom = effectiveFrom,
            IsActive = true,
            ConfigurationProfileId = policy.ProfileId,
            ConfigurationDecisionId = policy.DecisionId,
            PolicyHash = policy.PolicyHash,
            ClientRequestId = request.ClientRequestId,
            RequestHash = requestHash,
            Reason = Clean(request.Reason, 2000),
            CorrelationId = NormalizeCorrelation(correlationId),
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        db.ProjectCivilProjectEngineerAssignments.Add(assignment);
        AddRevision(assignment, CivilEngineeringAuditEventMap.CreateProjectEngineerAssignment, null, Snapshot(assignment), request.Reason, correlationId);
        AddAudit(assignment, CivilEngineeringAuditEventMap.CreateProjectEngineerAssignment, null, Snapshot(assignment), correlationId);

        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAssignmentAsync(assignment.Id, token);
    }

    public async Task<CivilEngineeringProjectEngineerAssignmentDto> EndProjectEngineerAssignmentAsync(
        Guid assignmentId,
        EndCivilEngineeringProjectEngineerAssignmentRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var assignment = await Query(true).SingleOrDefaultAsync(value => value.Id == assignmentId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Project Engineer appointment was not found.");
        await RequireProjectAsync(assignment.ProjectId);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        await RequireAssignmentAuthorityAsync(assignment.ProjectId, policy, token);
        if (!assignment.IsActive)
            throw Conflict("Only an active Project Engineer appointment can be ended.");
        CheckVersion(assignment.RowVersion, request.RowVersion);
        var effectiveTo = UtcDate(request.EffectiveTo);
        var endErrors = CivilEngineeringProjectEngineerAssignmentPolicy.ValidateEnd(assignment.EffectiveFrom, effectiveTo, request.Reason);
        if (endErrors.Count > 0)
            throw Validation(endErrors);

        var before = Snapshot(assignment);
        assignment.IsActive = false;
        assignment.EffectiveTo = effectiveTo;
        assignment.Reason = request.Reason.Trim();
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = UserName;
        assignment.LastModifiedById = UserId;
        await DeactivateProjectEngineerMembershipAsync(assignment.ProjectMemberId, token);

        AddRevision(assignment, CivilEngineeringAuditEventMap.UpdateProjectEngineerAssignment, before, Snapshot(assignment), request.Reason, correlationId);
        AddAudit(assignment, CivilEngineeringAuditEventMap.UpdateProjectEngineerAssignment, before, Snapshot(assignment), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAssignmentAsync(assignment.Id, token);
    }

    public async Task<IReadOnlyList<CivilEngineeringProjectEngineerAssignmentRevisionDto>> GetProjectEngineerAssignmentHistoryAsync(
        Guid assignmentId,
        CancellationToken token = default)
    {
        var assignment = await Query(false).SingleOrDefaultAsync(value => value.Id == assignmentId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Project Engineer appointment was not found.");
        await RequireProjectAsync(assignment.ProjectId);
        return await db.ProjectCivilProjectEngineerAssignmentRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.AssignmentId == assignmentId && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new CivilEngineeringProjectEngineerAssignmentRevisionDto
            {
                Id = value.Id,
                Action = value.Action,
                ActorUserId = value.ActorUserId,
                ActorName = value.ActorName,
                ActorRoles = value.ActorRoles,
                CorrelationId = value.CorrelationId,
                Reason = value.Reason,
                Before = Parse(value.BeforeJson),
                After = Parse(value.AfterJson),
                Timestamp = value.CreatedAt
            }).ToListAsync(token);
    }

    private IQueryable<ProjectCivilProjectEngineerAssignment> Query(bool tracked) =>
        (tracked ? db.ProjectCivilProjectEngineerAssignments : db.ProjectCivilProjectEngineerAssignments.AsNoTracking())
        .Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<CivilEngineeringProjectEngineerAssignmentDto> GetAssignmentAsync(Guid assignmentId, CancellationToken token)
    {
        var assignment = await Query(false).SingleOrDefaultAsync(value => value.Id == assignmentId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Project Engineer appointment was not found.");
        var users = await UsersByIdAsync([assignment.AssignedUserId], token);
        return Map(assignment, users);
    }

    private async Task RequireProjectAsync(Guid projectId)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private async Task<Policy> ResolvePolicyAsync(DateTime atUtc, CancellationToken token)
    {
        var at = atUtc.Kind == DateTimeKind.Utc ? atUtc : atUtc.ToUniversalTime();
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted
                && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published
                && value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0)
            throw Validation("No effective published Civil Engineering configuration profile exists.");
        if (profiles.Count > 1)
            throw Conflict("More than one Civil Engineering configuration profile is effective. Resolve the configuration overlap.");
        var profile = profiles[0];
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-005" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-005 supervision decision.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved
            || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved
            || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified
            || (decision.EffectiveFrom.HasValue && decision.EffectiveFrom > at)
            || (decision.EffectiveTo.HasValue && decision.EffectiveTo < at))
            throw Validation("CIV-CFG-005 is not approved, verified, and effective for this date.");
        var configured = JsonSerializer.Deserialize<CivilEngineeringSupervisionWorkflowValue>(decision.ValueJson, JsonOptions)
            ?? throw Validation("CIV-CFG-005 cannot be read.");
        if (configured.ProjectEngineerRoleIds.Count == 0 || configured.CoordinatorRoleIds.Count == 0)
            throw Validation("CIV-CFG-005 must select eligible Project Engineer and Projects Coordinator roles.");
        return new Policy(profile.Id, decision.Id, Hash(decision.ValueJson), configured);
    }

    private async Task RequireAssignmentAuthorityAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var currentRoles = currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isHead = currentRoles.Contains(CivilEngineeringAccessControlRegistry.HeadRole)
            && await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId
                && value.UserId == UserId && value.IsActive && !value.IsDeleted
                && value.Role == CivilEngineeringAccessControlRegistry.HeadRole, token);
        if (isHead) return;

        var coordinatorNames = await db.Roles.AsNoTracking().Where(value => policy.Value.CoordinatorRoleIds.Contains(value.Id))
            .Select(value => value.Name!).ToListAsync(token);
        var isCoordinator = coordinatorNames.Any(currentRoles.Contains)
            && await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId
                && value.UserId == UserId && value.IsActive && !value.IsDeleted, token);
        if (!isCoordinator)
            throw new UnauthorizedAccessException("Only the assigned Head of Civil Engineering or configured Projects Coordinator can appoint a Project Engineer.");
    }

    private async Task<IReadOnlyList<CivilEngineeringProjectEngineerCandidateDto>> GetEligibleCandidatesAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var members = await db.ProjectMembers.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId
                && value.IsActive && !value.IsDeleted && CivilEngineeringProjectEngineerAssignmentPolicy.SourceCivilRoles.Contains(value.Role))
            .Join(db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.IsActive), member => member.UserId, user => user.Id,
                (member, user) => new { member.UserId, member.Role, user.FirstName, user.LastName, user.UserName })
            .ToListAsync(token);
        var ids = members.Select(value => value.UserId).Distinct().ToList();
        var roles = await UserRolesAsync(ids, token);
        var configuredIds = policy.Value.ProjectEngineerRoleIds.ToHashSet();
        return members.Where(member => roles.TryGetValue(member.UserId, out var values)
                                      && values.Any(value => configuredIds.Contains(value.Id)
                                                               && string.Equals(value.Name, member.Role, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(value => value.FirstName).ThenBy(value => value.LastName).Select(value => new CivilEngineeringProjectEngineerCandidateDto
            {
                UserId = value.UserId,
                DisplayName = DisplayName(value.FirstName, value.LastName, value.UserName),
                SourceCivilRole = value.Role
            }).ToList();
    }

    private async Task<CivilEngineeringProjectEngineerCandidate> GetCandidateAsync(Guid projectId, Guid assignedUserId, Policy policy, CancellationToken token)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == assignedUserId, token);
        var member = await db.ProjectMembers.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProjectId == projectId
            && value.UserId == assignedUserId && value.IsActive && !value.IsDeleted
            && CivilEngineeringProjectEngineerAssignmentPolicy.SourceCivilRoles.Contains(value.Role), token);
        var roles = await UserRolesAsync([assignedUserId], token);
        var configured = member is not null && roles.TryGetValue(assignedUserId, out var userRoles)
            && userRoles.Any(value => policy.Value.ProjectEngineerRoleIds.Contains(value.Id)
                                      && string.Equals(value.Name, member.Role, StringComparison.OrdinalIgnoreCase));
        return new CivilEngineeringProjectEngineerCandidate(assignedUserId, member?.Role ?? string.Empty,
            member is not null, user?.IsActive == true, configured);
    }

    private async Task<ProjectMember> EnsureProjectEngineerMembershipAsync(Guid projectId, Guid userId, CancellationToken token)
    {
        var member = await db.ProjectMembers.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProjectId == projectId
            && value.UserId == userId && value.Role == CivilEngineeringAccessControlRegistry.ProjectEngineerRole && !value.IsDeleted, token);
        if (member is not null)
        {
            member.IsActive = true;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = UserName;
            member.LastModifiedById = UserId;
            return member;
        }

        member = new ProjectMember
        {
            TenantId = TenantId,
            ProjectId = projectId,
            UserId = userId,
            Role = CivilEngineeringAccessControlRegistry.ProjectEngineerRole,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        db.ProjectMembers.Add(member);
        return member;
    }

    private async Task DeactivateProjectEngineerMembershipAsync(Guid projectMemberId, CancellationToken token)
    {
        var membership = await db.ProjectMembers.SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == projectMemberId && !value.IsDeleted, token);
        if (membership is null || membership.Role != CivilEngineeringAccessControlRegistry.ProjectEngineerRole)
            return;

        membership.IsActive = false;
        membership.UpdatedAt = DateTime.UtcNow;
        membership.UpdatedBy = UserName;
        membership.LastModifiedById = UserId;
    }

    private async Task<Dictionary<Guid, List<(Guid Id, string Name)>>> UserRolesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken token)
    {
        if (userIds.Count == 0) return [];
        var rows = await db.UserRoles.AsNoTracking().Where(value => userIds.Contains(value.UserId))
            .Join(db.Roles.AsNoTracking(), userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Id, role.Name })
            .Where(value => value.Name != null).ToListAsync(token);
        return rows.GroupBy(value => value.UserId).ToDictionary(value => value.Key,
            value => value.Select(item => (item.Id, item.Name!)).ToList());
    }

    private async Task<Dictionary<Guid, string>> UsersByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        if (ids.Count == 0) return [];
        return await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && ids.Contains(value.Id))
            .Select(value => new { value.Id, value.FirstName, value.LastName, value.UserName }).ToDictionaryAsync(
                value => value.Id, value => DisplayName(value.FirstName, value.LastName, value.UserName), token);
    }

    private static CivilEngineeringProjectEngineerAssignmentDto Map(ProjectCivilProjectEngineerAssignment value, IReadOnlyDictionary<Guid, string> users) => new()
    {
        Id = value.Id,
        ProjectId = value.ProjectId,
        ProjectMemberId = value.ProjectMemberId,
        AssignedUserId = value.AssignedUserId,
        AssignedUserName = users.GetValueOrDefault(value.AssignedUserId, value.AssignedUserId.ToString()),
        SourceCivilRole = value.SourceCivilRole,
        ProjectRole = value.ProjectRole,
        Authority = value.Authority,
        EffectiveFrom = value.EffectiveFrom,
        EffectiveTo = value.EffectiveTo,
        IsActive = value.IsActive,
        Reason = value.Reason,
        CreatedAt = value.CreatedAt,
        CreatedBy = value.CreatedBy ?? string.Empty,
        RowVersion = Convert.ToBase64String(value.RowVersion)
    };

    private void AddRevision(ProjectCivilProjectEngineerAssignment value, string action, object? before, object after, string? reason, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.ProjectCivilProjectEngineerAssignmentRevisions.Add(new ProjectCivilProjectEngineerAssignmentRevision
        {
            TenantId = TenantId, AssignmentId = value.Id, Action = action, ActorUserId = UserId, ActorName = UserName,
            ActorRoles = ActorRoles, CorrelationId = NormalizeCorrelation(correlationId), Reason = Clean(reason, 2000),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions),
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
    }

    private void AddAudit(ProjectCivilProjectEngineerAssignment value, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
        Resource = nameof(ProjectCivilProjectEngineerAssignment), ResourceId = value.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        NewValues = JsonSerializer.Serialize(new { correlationId = NormalizeCorrelation(correlationId), value = after }, JsonOptions),
        IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent,
        Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
    });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The Project Engineer appointment changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52001 and <= 52005)
        { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("A conflicting Project Engineer appointment already exists. Refresh and retry."); }
    }

    private static object Snapshot(ProjectCivilProjectEngineerAssignment value) => new
    {
        value.Id, value.ProjectId, value.ProjectMemberId, value.AssignedUserId, value.SourceCivilRole, value.ProjectRole,
        value.Authority, value.EffectiveFrom, value.EffectiveTo, value.IsActive, value.ConfigurationProfileId,
        value.ConfigurationDecisionId, value.PolicyHash, value.ClientRequestId, value.Reason
    };

    private static object? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { using var document = JsonDocument.Parse(value); return document.RootElement.Clone(); }
        catch (JsonException) { return value; }
    }

    private static string DisplayName(string? firstName, string? lastName, string? userName)
    {
        var value = string.Join(" ", new[] { firstName, lastName }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim();
        return string.IsNullOrWhiteSpace(value) ? userName ?? string.Empty : value;
    }

    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        value is string text ? text : JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static DateTime UtcDate(DateTime value) => value == default ? default : (value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime()).Date;
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : throw Validation($"Text cannot exceed {max} characters.");
    private static void CheckVersion(byte[] current, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Conflict("The row version is invalid. Refresh and retry."); }
        if (!CryptographicOperations.FixedTimeEquals(current, expected))
            throw Conflict("The Project Engineer appointment changed. Refresh and retry.");
    }
    private static CivilEngineeringSupervisionValidationException Validation(string message) => new(message);
    private static CivilEngineeringSupervisionValidationException Validation(IEnumerable<string> messages) => new(string.Join(" ", messages));
    private static CivilEngineeringSupervisionConflictException Conflict(string message) => new(message);

    private sealed record Policy(Guid ProfileId, Guid DecisionId, string PolicyHash, CivilEngineeringSupervisionWorkflowValue Value);
}
