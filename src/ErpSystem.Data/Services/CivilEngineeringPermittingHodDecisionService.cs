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
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>CIV-0404 final HOD decision over an SCE recommendation and its shared workflow instance.</summary>
public sealed class CivilEngineeringPermittingHodDecisionService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : ICivilEngineeringPermittingHodDecisionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<IReadOnlyList<CivilEngineeringPermittingHodDecisionQueueItemDto>> ListPendingAsync(CancellationToken token = default)
    {
        await RequireHodRoleAsync(token);
        var pending = await Reviews(false)
            .Where(value => value.Stage == CivilEngineeringPermittingEngineeringReviewStage.PendingHodDecision && value.Status == "PendingApproval" && value.WorkflowInstanceId != null)
            .Include(value => value.DevelopmentApprovalFile)
            .OrderBy(value => value.ReviewedAt)
            .Take(250)
            .ToListAsync(token);
        var allowed = new List<ProjectCivilDevelopmentApprovalEngineeringReview>();
        foreach (var review in pending)
        {
            if (!await IsCurrentHodRecipientAsync(review, token)) continue;
            if (await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.PermittingReview, review.Id, UserId)) allowed.Add(review);
        }
        return await MapAsync(allowed, token);
    }

    public async Task<CivilEngineeringPermittingHodDecisionQueueItemDto> DecideAsync(Guid engineeringReviewId, DecideCivilEngineeringPermittingReviewRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringPermittingHodDecisionPolicy.Validate(request);
        if (errors.Count > 0) throw Validation(errors);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await RequireHodRoleAsync(token);
        var review = await Reviews(true).Include(value => value.DevelopmentApprovalFile).SingleOrDefaultAsync(value => value.Id == engineeringReviewId, token)
            ?? throw new CivilEngineeringPermittingHodDecisionNotFoundException("The pending SCE engineering review was not found.");
        var requestHash = Hash(new { engineeringReviewId, request.Outcome, reason = request.Reason?.Trim() });
        var retry = await Decisions(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (retry.EngineeringReviewId != engineeringReviewId || retry.DecidedById != UserId) throw Conflict("This client request identifier was already used for a different HOD decision.");
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different HOD decision values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([review], token)).Single();
        }
        if (review.Stage != CivilEngineeringPermittingEngineeringReviewStage.PendingHodDecision || review.Status != "PendingApproval" || review.ApprovalStatus != "Pending" || !review.WorkflowInstanceId.HasValue)
            throw Conflict("Only a pending SCE recommendation with an active HOD workflow can be decided.");
        if (await Decisions(true).AnyAsync(value => value.EngineeringReviewId == review.Id, token)) throw Conflict("The SCE engineering recommendation already has an immutable HOD decision.");
        await RequireHodForReviewAsync(review, token);
        if (!await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.PermittingReview, review.Id, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the current shared permitting workflow step.");

        var action = CivilEngineeringPermittingHodDecisionPolicy.WorkflowAction(request.Outcome);
        var result = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.PermittingReview, review.Id, UserId, action, request.Reason?.Trim());
        if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The shared permitting workflow could not process the HOD decision.");
        var expected = request.Outcome == CivilEngineeringPermittingHodDecisionOutcome.Approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected;
        if (result.Outcome != expected) throw Conflict("The shared permitting workflow returned an outcome that does not match the selected HOD decision.");

        var now = DateTime.UtcNow;
        db.ProjectCivilDevelopmentApprovalEngineeringReviewDecisions.Add(new ProjectCivilDevelopmentApprovalEngineeringReviewDecision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, EngineeringReviewId = review.Id, ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
            Outcome = request.Outcome, Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(), DecidedById = UserId,
            WorkflowInstanceId = review.WorkflowInstanceId.Value, WorkflowAction = action, WorkflowOutcome = result.Outcome.ToString(), CorrelationId = Correlation(correlationId),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        });
        await db.SaveChangesAsync(token);

        var before = Snapshot(review);
        workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.PermittingReview).ApplyApprovalOutcome(review, result.Outcome, UserId, request.Reason?.Trim());
        review.Stage = CivilEngineeringPermittingHodDecisionPolicy.Stage(request.Outcome);
        review.UpdatedAt = DateTime.UtcNow; review.UpdatedBy = UserName; review.LastModifiedById = UserId;
        var auditAction = request.Outcome switch
        {
            CivilEngineeringPermittingHodDecisionOutcome.Approve => CivilEngineeringAuditEventMap.ApproveEngineeringRecommendation,
            CivilEngineeringPermittingHodDecisionOutcome.Reject => CivilEngineeringAuditEventMap.RejectEngineeringRecommendation,
            _ => CivilEngineeringAuditEventMap.UpdateDevelopmentDecisionReturn
        };
        AddRevision(review, auditAction, before, Snapshot(review), request.Reason?.Trim(), correlationId);
        AddAudit(review, auditAction, before, Snapshot(review), correlationId);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([review], token)).Single();
    }

    private IQueryable<ProjectCivilDevelopmentApprovalEngineeringReview> Reviews(bool tracked) =>
        (tracked ? db.ProjectCivilDevelopmentApprovalEngineeringReviews : db.ProjectCivilDevelopmentApprovalEngineeringReviews.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);
    private IQueryable<ProjectCivilDevelopmentApprovalEngineeringReviewDecision> Decisions(bool tracked) =>
        (tracked ? db.ProjectCivilDevelopmentApprovalEngineeringReviewDecisions : db.ProjectCivilDevelopmentApprovalEngineeringReviewDecisions.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task RequireHodRoleAsync(CancellationToken token)
    {
        if (!currentUser.Roles.Any(value => string.Equals(value, CivilEngineeringAccessControlRegistry.HeadRole, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException($"The {CivilEngineeringAccessControlRegistry.HeadRole} role is required for a final permitting decision.");
        if (!await db.Users.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == UserId && value.IsActive, token))
            throw new UnauthorizedAccessException("The authenticated HOD account is not active in this tenant.");
    }

    private async Task RequireHodForReviewAsync(ProjectCivilDevelopmentApprovalEngineeringReview review, CancellationToken token)
    {
        if (!await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == review.DevelopmentApprovalFile.ProjectId && value.UserId == UserId && value.IsActive && !value.IsDeleted, token))
            throw new UnauthorizedAccessException("The HOD is not an active member of the linked project.");
        if (!await IsCurrentHodRecipientAsync(review, token))
            throw new UnauthorizedAccessException("Only the assigned HOD recipient of the current development-file handoff can make the final decision.");
    }

    private Task<bool> IsCurrentHodRecipientAsync(ProjectCivilDevelopmentApprovalEngineeringReview review, CancellationToken token) =>
        db.ProjectCivilDevelopmentApprovalFileHandoffs.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.DevelopmentApprovalFileId == review.DevelopmentApprovalFileId && !value.IsDeleted)
            .OrderByDescending(value => value.SequenceNumber)
            .Select(value => value.ToSection == CivilEngineeringPermittingSection.HeadOfDepartment && value.RecipientUserId == UserId)
            .FirstOrDefaultAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringPermittingHodDecisionQueueItemDto>> MapAsync(IReadOnlyCollection<ProjectCivilDevelopmentApprovalEngineeringReview> values, CancellationToken token)
    {
        var userIds = values.Select(value => value.ReviewerUserId).Distinct().ToList();
        var categoryIds = values.Select(value => value.CommentCategoryId).Distinct().ToList();
        var recordIds = values.Select(value => value.CentralDocumentRecordId).Distinct().ToList();
        var projectIds = values.Select(value => value.DevelopmentApprovalFile.ProjectId).Distinct().ToList();
        var handoffFileIds = values.Select(value => value.DevelopmentApprovalFileId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => Name(value.FirstName, value.LastName, value.UserName), token);
        var categories = await db.ProjectCatalogEntries.AsNoTracking().Where(value => value.TenantId == TenantId && categoryIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Code + " - " + value.Name, token);
        var records = await db.CentralDocumentRecords.AsNoTracking().Where(value => value.TenantId == TenantId && recordIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.DocumentReference, token);
        var projects = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && projectIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.ProjectCode + " - " + value.Title, token);
        var dueDates = await db.ProjectCivilDevelopmentApprovalFileHandoffs.AsNoTracking().Where(value => value.TenantId == TenantId && handoffFileIds.Contains(value.DevelopmentApprovalFileId) && !value.IsDeleted).GroupBy(value => value.DevelopmentApprovalFileId).Select(group => new { FileId = group.Key, DueDate = group.OrderByDescending(value => value.SequenceNumber).Select(value => value.DueDate).First() }).ToDictionaryAsync(value => value.FileId, value => value.DueDate, token);
        return values.Select(value => new CivilEngineeringPermittingHodDecisionQueueItemDto
        {
            EngineeringReviewId = value.Id, DevelopmentApprovalFileId = value.DevelopmentApprovalFileId, FileNumber = value.DevelopmentApprovalFile.FileNumber,
            ApplicationReference = value.DevelopmentApprovalFile.ApplicationReference, ApplicantName = value.DevelopmentApprovalFile.ApplicantName,
            ProjectLabel = projects.GetValueOrDefault(value.DevelopmentApprovalFile.ProjectId, "Unavailable project"), ReviewerName = users.GetValueOrDefault(value.ReviewerUserId, "Unavailable reviewer"),
            CommentCategoryLabel = categories.GetValueOrDefault(value.CommentCategoryId, "Unavailable category"), ReviewComment = value.ReviewComment,
            RecommendedOutcome = value.RecommendedOutcome, DocumentReference = records.GetValueOrDefault(value.CentralDocumentRecordId), ReviewedAt = value.ReviewedAt,
            HandoffDueDate = dueDates.GetValueOrDefault(value.DevelopmentApprovalFileId), RowVersion = Convert.ToBase64String(value.RowVersion)
        }).ToList();
    }

    private void AddRevision(ProjectCivilDevelopmentApprovalEngineeringReview value, string action, object before, object after, string? reason, string correlationId) =>
        value.Revisions.Add(new ProjectCivilDevelopmentApprovalEngineeringReviewRevision { Id = Guid.NewGuid(), TenantId = TenantId, EngineeringReviewId = value.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = reason, BeforeJson = JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private void AddAudit(ProjectCivilDevelopmentApprovalEngineeringReview value, string action, object before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectCivilDevelopmentApprovalEngineeringReview), ResourceId = value.Id.ToString(), OldValues = JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(after, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private static object Snapshot(ProjectCivilDevelopmentApprovalEngineeringReview value) => new { value.Id, value.DevelopmentApprovalFileId, value.SourceHandoffId, value.ReviewerUserId, value.RecommendedOutcome, value.WorkflowInstanceId, value.Stage, value.Status, value.ApprovalStatus, value.ApprovedById, value.ApprovedAt, value.RejectionReason, value.ReviewedAt };
    private static string Name(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left ?? string.Empty), Encoding.UTF8.GetBytes(right ?? string.Empty));
    private static CivilEngineeringPermittingHodDecisionValidationException Validation(IEnumerable<string> messages) => new(string.Join(" ", messages));
    private static CivilEngineeringPermittingHodDecisionConflictException Conflict(string message) => new(message);
}
