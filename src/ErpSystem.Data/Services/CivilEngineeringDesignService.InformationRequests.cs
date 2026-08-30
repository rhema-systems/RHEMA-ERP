using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed partial class CivilEngineeringDesignService
{
    public async Task<IReadOnlyList<CivilEngineeringDesignInputRequestDto>> ListInformationRequestsAsync(
        Guid designCaseId,
        CancellationToken token = default)
    {
        var designCase = await RequiredAsync(designCaseId, false, token);
        await RequireProjectAsync(designCase.ProjectId);
        var values = await InformationRequestQuery(false)
            .Where(value => value.CivilDesignCaseId == designCaseId)
            .OrderByDescending(value => value.RaisedDate)
            .ToListAsync(token);
        return await MapInformationRequestsAsync(values, token);
    }

    public async Task<IReadOnlyList<CivilEngineeringDesignInputRequestDto>> ListAssignedInformationRequestsAsync(
        CancellationToken token = default)
    {
        var sectionId = await RequireCurrentEmployeeSectionAsync(token);
        var values = await InformationRequestQuery(false)
            .Where(value => value.RequestedSectionId == sectionId
                            && value.Status != ProjectRfiStatuses.Closed
                            && value.Status != ProjectRfiStatuses.Void)
            .OrderBy(value => value.ResponseDueDate)
            .ThenByDescending(value => value.RaisedDate)
            .ToListAsync(token);
        return await MapInformationRequestsAsync(values, token);
    }

    public async Task<CivilEngineeringDesignInputRequestDto> CreateInformationRequestAsync(
        Guid designCaseId,
        CreateCivilEngineeringDesignInputRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.RequestedSectionId == Guid.Empty)
            throw Validation("Select a target section and provide a client request identifier.");
        var subject = RequiredText(request.Subject, 3, 200, "Subject");
        var question = RequiredText(request.Question, 10, 4000, "Question");
        var priority = request.Priority?.Trim() ?? string.Empty;
        if (!CivilEngineeringDesignInputRules.Priorities.Contains(priority))
            throw Validation("Select a supported request priority.");
        var dueAt = Utc(request.ResponseDueDate);
        if (!dueAt.HasValue) throw Validation("Select a response due date.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var designCase = await RequiredAsync(designCaseId, false, token);
        await RequireProjectAsync(designCase.ProjectId);
        await RequireTransitionActorAsync(designCase, CivilEngineeringDesignActor.SupervisingCivilEngineer, token);
        try { CivilEngineeringDesignInputRules.EnsureCanCreate(designCase.Stage, dueAt.Value, DateTime.UtcNow); }
        catch (InvalidOperationException exception) { throw Validation(exception.Message); }

        var section = await db.Sections.AsNoTracking()
            .Include(value => value.Department)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId
                                           && value.Id == request.RequestedSectionId
                                           && value.IsActive && !value.IsDeleted
                                           && value.Department.TenantId == TenantId
                                           && value.Department.IsActive && !value.Department.IsDeleted,
                token)
            ?? throw Validation("Select an active current-tenant HR section.");
        if (currentUser.EmployeeId.HasValue)
        {
            var ownSectionId = await db.Employees.AsNoTracking()
                .Where(value => value.TenantId == TenantId && value.Id == currentUser.EmployeeId.Value
                                && value.IsActive && !value.IsDeleted)
                .Select(value => value.SectionId)
                .SingleOrDefaultAsync(token);
            if (ownSectionId == section.Id)
                throw Validation("Select another section for a cross-section information request.");
        }

        var requestHash = Hash(new
        {
            designCaseId,
            request.RequestedSectionId,
            Subject = subject,
            Question = question,
            Priority = priority,
            ResponseDueDate = dueAt,
            request.BlocksDesignReadiness
        });
        var retry = await InformationRequestQuery(false)
            .SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (retry.CivilDesignCaseId != designCaseId || !FixedEquals(retry.RequestHash, requestHash))
                throw RetryConflict();
            await transaction.CommitAsync(token);
            return await MapInformationRequestAsync(retry, token);
        }

        var id = Guid.NewGuid();
        var value = new ProjectRfi
        {
            Id = id,
            TenantId = TenantId,
            ProjectId = designCase.ProjectId,
            CivilDesignCaseId = designCase.Id,
            RequestedSectionId = section.Id,
            RequestedByUserId = UserId,
            ClientRequestId = request.ClientRequestId,
            RequestHash = requestHash,
            ReferenceNumber = $"CIV-INP-{id:N}"[..18].ToUpperInvariant(),
            Subject = subject,
            Question = question,
            Priority = priority,
            Status = ProjectRfiStatuses.Submitted,
            RaisedDate = DateTime.UtcNow,
            ResponseDueDate = dueAt,
            RaisedByName = UserName,
            BlocksCivilDesignReadiness = request.BlocksDesignReadiness,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        db.ProjectRfis.Add(value);
        AddInformationRequestAudit(
            value,
            CivilEngineeringAuditEventMap.CreateCrossSectionInputRequest,
            null,
            InformationRequestSnapshot(value, null),
            correlationId);
        try
        {
            await SaveInformationRequestAsync(token);
            await transaction.CommitAsync(token);
        }
        catch (DbUpdateException exception) when (IsUniqueInformationRequestConflict(exception))
        {
            await transaction.RollbackAsync(token);
            db.ChangeTracker.Clear();
            var concurrent = await InformationRequestQuery(false)
                .SingleOrDefaultAsync(candidate => candidate.ClientRequestId == request.ClientRequestId, token);
            if (concurrent is null || concurrent.CivilDesignCaseId != designCaseId
                                   || !FixedEquals(concurrent.RequestHash, requestHash))
                throw RetryConflict();
            return await MapInformationRequestAsync(concurrent, token);
        }
        return await RequiredInformationRequestDtoAsync(value.Id, token);
    }

    public async Task<CivilEngineeringDesignInputRequestDto> SubmitInformationResponseAsync(
        Guid requestId,
        SubmitCivilEngineeringDesignInputResponseRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.CentralDocumentRecordId == Guid.Empty
            || request.CentralDocumentVersionId == Guid.Empty)
            throw Validation("Select current Published DMS response evidence and provide a client request identifier.");
        var responseText = RequiredText(request.ResponseText, 10, 4000, "Response");
        var mutationHash = Hash(new
        {
            requestId,
            Action = "Respond",
            ResponseText = responseText,
            request.CentralDocumentRecordId,
            request.CentralDocumentVersionId
        });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var value = await RequiredInformationRequestAsync(requestId, true, token);
        if (IsInformationRequestRetry(value, request.ClientRequestId, mutationHash))
        {
            await transaction.RollbackAsync(token);
            return await MapInformationRequestAsync(value, token);
        }
        CheckInformationRequestVersion(value.RowVersion, request.RowVersion);
        try { CivilEngineeringDesignInputRules.EnsureCanRespond(value.Status); }
        catch (InvalidOperationException exception) { throw Conflict(exception.Message); }
        var sectionId = await RequireCurrentEmployeeSectionAsync(token);
        if (value.RequestedSectionId != sectionId)
            throw new UnauthorizedAccessException("Only an active employee in the requested HR section can respond.");

        var policy = await ResolveReconnaissancePolicyAsync(value.CivilDesignCase!.ConfigurationDecisionId, token);
        var version = await db.CentralDocumentVersions.AsNoTracking()
            .Include(item => item.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(item => item.TenantId == TenantId
                                          && item.Id == request.CentralDocumentVersionId,
                token)
            ?? throw Validation("Select a current Published current-tenant DMS response document.");
        if (version.DocumentRecordId != request.CentralDocumentRecordId)
            throw Validation("The selected DMS version does not belong to the selected response document.");
        if (!string.Equals(
                version.DocumentRecord.MetadataTemplateCode,
                policy.CrossSectionTemplate.TemplateCode,
                StringComparison.OrdinalIgnoreCase))
            throw Validation($"The response document must use DMS template {policy.CrossSectionTemplate.TemplateCode}.");

        var before = InformationRequestSnapshot(value, value.CivilDesignInputResponses.LastOrDefault());
        var sequence = value.CivilDesignInputResponses.Count == 0
            ? 1
            : value.CivilDesignInputResponses.Max(item => item.ResponseSequence) + 1;
        var now = DateTime.UtcNow;
        var response = new ProjectCivilDesignInputResponse
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ProjectRfiId = value.Id,
            ResponseSequence = sequence,
            ResponseText = responseText,
            CentralDocumentRecordId = request.CentralDocumentRecordId,
            CentralDocumentVersionId = request.CentralDocumentVersionId,
            RespondedByUserId = UserId,
            RespondedAt = now,
            CorrelationId = NormalizeCorrelation(correlationId),
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        value.CivilDesignInputResponses.Add(response);
        value.Response = responseText;
        value.RespondedByName = UserName;
        value.RespondedDate = now;
        value.Status = ProjectRfiStatuses.Answered;
        value.LastMutationClientRequestId = request.ClientRequestId;
        value.LastMutationRequestHash = mutationHash;
        value.UpdatedAt = now;
        value.UpdatedBy = UserName;
        value.LastModifiedById = UserId;
        AddInformationRequestAudit(
            value,
            CivilEngineeringAuditEventMap.SubmitCrossSectionInput,
            before,
            InformationRequestSnapshot(value, response),
            correlationId);
        await SaveInformationRequestAsync(token);
        await transaction.CommitAsync(token);
        return await RequiredInformationRequestDtoAsync(value.Id, token);
    }

    public async Task<CivilEngineeringDesignInputRequestDto> ReviewInformationResponseAsync(
        Guid requestId,
        ReviewCivilEngineeringDesignInputRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Review reason");
        var mutationHash = Hash(new { requestId, request.Action, Reason = reason });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var value = await RequiredInformationRequestAsync(requestId, true, token);
        await RequireProjectAsync(value.ProjectId);
        await RequireTransitionActorAsync(
            value.CivilDesignCase!,
            CivilEngineeringDesignActor.SupervisingCivilEngineer,
            token);
        if (IsInformationRequestRetry(value, request.ClientRequestId, mutationHash))
        {
            await transaction.RollbackAsync(token);
            return await MapInformationRequestAsync(value, token);
        }
        CheckInformationRequestVersion(value.RowVersion, request.RowVersion);
        var latest = value.CivilDesignInputResponses.OrderByDescending(item => item.ResponseSequence).FirstOrDefault()
                     ?? throw Conflict("The cross-section request has no response to review.");
        if (latest.RespondedByUserId == UserId)
            throw Conflict("The response author cannot review the same cross-section input.");
        string nextStatus;
        try { nextStatus = CivilEngineeringDesignInputRules.ReviewStatus(value.Status, request.Action); }
        catch (InvalidOperationException exception) { throw Conflict(exception.Message); }
        var before = InformationRequestSnapshot(value, latest);
        value.Status = nextStatus;
        value.Notes = reason;
        value.LastMutationClientRequestId = request.ClientRequestId;
        value.LastMutationRequestHash = mutationHash;
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = UserName;
        value.LastModifiedById = UserId;
        var action = request.Action == CivilEngineeringDesignInputReviewAction.Accept
            ? CivilEngineeringAuditEventMap.ApproveCrossSectionInput
            : CivilEngineeringAuditEventMap.RejectCrossSectionInput;
        AddInformationRequestAudit(value, action, before, InformationRequestSnapshot(value, latest), correlationId);
        await SaveInformationRequestAsync(token);
        await transaction.CommitAsync(token);
        return await RequiredInformationRequestDtoAsync(value.Id, token);
    }

    private IQueryable<ProjectRfi> InformationRequestQuery(bool tracked)
    {
        IQueryable<ProjectRfi> query = db.ProjectRfis
            .Include(value => value.CivilDesignCase)
            .Include(value => value.RequestedSection).ThenInclude(value => value!.Department)
            .Include(value => value.CivilDesignInputResponses).ThenInclude(value => value.CentralDocumentRecord)
            .Include(value => value.CivilDesignInputResponses).ThenInclude(value => value.CentralDocumentVersion)
            .Where(value => value.TenantId == TenantId && value.CivilDesignCaseId != null && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<ProjectRfi> RequiredInformationRequestAsync(Guid id, bool tracked, CancellationToken token)
        => await InformationRequestQuery(tracked).SingleOrDefaultAsync(value => value.Id == id, token)
           ?? throw new CivilEngineeringDesignNotFoundException("The cross-section information request was not found.");

    private async Task<CivilEngineeringDesignInputRequestDto> RequiredInformationRequestDtoAsync(
        Guid id,
        CancellationToken token) =>
        await MapInformationRequestAsync(await RequiredInformationRequestAsync(id, false, token), token);

    private async Task<Guid> RequireCurrentEmployeeSectionAsync(CancellationToken token)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == UserId && value.IsActive && value.EmployeeId.HasValue, token);
        if (user?.EmployeeId is not { } employeeId)
            throw new UnauthorizedAccessException("A current active HR employee link is required to respond.");
        var sectionId = await db.Employees.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.Id == employeeId
                            && value.IsActive && !value.IsDeleted && value.SectionId.HasValue)
            .Select(value => value.SectionId)
            .SingleOrDefaultAsync(token);
        return sectionId ?? throw new UnauthorizedAccessException(
            "The current employee must have an active HR section before responding.");
    }

    private async Task<IReadOnlyList<CivilEngineeringDesignInputRequestDto>> MapInformationRequestsAsync(
        IReadOnlyList<ProjectRfi> values,
        CancellationToken token)
    {
        var userIds = values.Select(value => value.RequestedByUserId)
            .Concat(values.SelectMany(value => value.CivilDesignInputResponses.Select(response => (Guid?)response.RespondedByUserId)))
            .Where(value => value.HasValue).Select(value => value!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => (value.FirstName + " " + value.LastName).Trim(), token);
        return values.Select(value => MapInformationRequest(value, users)).ToList();
    }

    private async Task<CivilEngineeringDesignInputRequestDto> MapInformationRequestAsync(
        ProjectRfi value,
        CancellationToken token) =>
        (await MapInformationRequestsAsync([value], token)).Single();

    private static CivilEngineeringDesignInputRequestDto MapInformationRequest(
        ProjectRfi value,
        IReadOnlyDictionary<Guid, string> users) => new()
    {
        Id = value.Id,
        DesignCaseId = value.CivilDesignCaseId!.Value,
        ProjectId = value.ProjectId,
        ReferenceNumber = value.ReferenceNumber,
        Subject = value.Subject,
        Question = value.Question,
        Priority = value.Priority,
        Status = value.Status,
        RaisedDate = value.RaisedDate,
        ResponseDueDate = value.ResponseDueDate,
        RequestedSectionId = value.RequestedSectionId!.Value,
        RequestedSectionLabel = value.RequestedSection is null
            ? string.Empty
            : value.RequestedSection.Department.Name + " / " + value.RequestedSection.Name,
        RequestedByUserId = value.RequestedByUserId!.Value,
        BlocksDesignReadiness = value.BlocksCivilDesignReadiness,
        IsDesignReady = CivilEngineeringDesignInputRules.IsReady(value.BlocksCivilDesignReadiness, value.Status),
        RowVersion = Convert.ToBase64String(value.RowVersion),
        Responses = value.CivilDesignInputResponses.OrderBy(item => item.ResponseSequence).Select(item =>
            new CivilEngineeringDesignInputResponseDto
            {
                Id = item.Id,
                ResponseSequence = item.ResponseSequence,
                ResponseText = item.ResponseText,
                CentralDocumentRecordId = item.CentralDocumentRecordId,
                CentralDocumentVersionId = item.CentralDocumentVersionId,
                DocumentReference = item.CentralDocumentRecord.DocumentReference,
                DocumentTitle = item.CentralDocumentRecord.Title,
                VersionNumber = item.CentralDocumentVersion.VersionNumber,
                RespondedByUserId = item.RespondedByUserId,
                RespondedByName = users.GetValueOrDefault(item.RespondedByUserId, item.RespondedByUserId.ToString()),
                RespondedAt = item.RespondedAt
            }).ToList()
    };

    private void AddInformationRequestAudit(
        ProjectRfi value,
        string action,
        object? before,
        object after,
        string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId,
        UserId = UserId,
        Username = UserName,
        Action = action,
        Resource = nameof(ProjectRfi),
        ResourceId = value.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        NewValues = JsonSerializer.Serialize(new
        {
            correlationId = NormalizeCorrelation(correlationId),
            value = after
        }, JsonOptions),
        IpAddress = currentUser.IpAddress,
        UserAgent = currentUser.UserAgent,
        Timestamp = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName,
        CreatedById = UserId
    });

    private static object InformationRequestSnapshot(ProjectRfi value, ProjectCivilDesignInputResponse? response) => new
    {
        value.Id,
        value.ProjectId,
        value.CivilDesignCaseId,
        value.ReferenceNumber,
        value.Subject,
        value.Question,
        value.Priority,
        value.Status,
        value.RaisedDate,
        value.ResponseDueDate,
        value.RequestedSectionId,
        value.RequestedByUserId,
        value.BlocksCivilDesignReadiness,
        Response = response is null ? null : new
        {
            response.Id,
            response.ResponseSequence,
            response.ResponseText,
            response.CentralDocumentRecordId,
            response.CentralDocumentVersionId,
            response.RespondedByUserId,
            response.RespondedAt
        }
    };

    private static bool IsInformationRequestRetry(ProjectRfi value, Guid clientRequestId, string requestHash)
    {
        if (value.LastMutationClientRequestId != clientRequestId) return false;
        if (!FixedEquals(value.LastMutationRequestHash, requestHash)) throw RetryConflict();
        return true;
    }

    private static void CheckInformationRequestVersion(byte[] current, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Conflict("The information-request row version is invalid. Refresh and retry."); }
        if (!CryptographicOperations.FixedTimeEquals(current, expected))
            throw Conflict("The information request changed. Refresh and retry.");
    }

    private async Task SaveInformationRequestAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException)
        { throw Conflict("The information request changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sqlException
                                                  && sqlException.Number is >= 51930 and <= 51949)
        { throw Conflict(sqlException.Message); }
    }

    private static bool IsUniqueInformationRequestConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 }
        || exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true;
}
