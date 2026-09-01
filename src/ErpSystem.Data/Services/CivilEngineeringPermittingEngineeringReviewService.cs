using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>CIV-0403 SCE review overlay. It starts the selected shared workflow but leaves HOD decision and section forwarding to later controlled stages.</summary>
public sealed class CivilEngineeringPermittingEngineeringReviewService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    ICivilEngineeringDevelopmentApprovalFileService approvalFiles,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : ICivilEngineeringPermittingEngineeringReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringPermittingEngineeringReviewLookupsDto> GetLookupsAsync(Guid fileId, CancellationToken token = default)
    {
        var baseLookups = await approvalFiles.GetLookupsAsync(token);
        var file = await RequiredFileAsync(fileId, false, token);
        var policy = await FrozenPolicyAsync(file, token);
        await RequireAssignedSceAsync(file, token);
        return new CivilEngineeringPermittingEngineeringReviewLookupsDto
        {
            CommentCategories = await CommentCategoriesAsync(policy.CommentCategoryIds, token),
            Documents = baseLookups.Documents,
            AllowedOutcomes = policy.AllowedOutcomes.OrderBy(value => value).ToList()
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringPermittingEngineeringReviewDto>> ListAsync(Guid fileId, CancellationToken token = default)
    {
        await approvalFiles.GetLookupsAsync(token);
        await RequiredFileAsync(fileId, false, token);
        return await MapAsync(await Reviews(false).Where(value => value.DevelopmentApprovalFileId == fileId).OrderBy(value => value.ReviewedAt).ToListAsync(token), token);
    }

    public async Task<CivilEngineeringPermittingEngineeringReviewDto> SubmitAsync(Guid fileId, SubmitCivilEngineeringPermittingEngineeringReviewRequest request, string correlationId, CancellationToken token = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var file = await RequiredFileAsync(fileId, true, token);
        await approvalFiles.GetLookupsAsync(token);
        if (file.Status != CivilEngineeringDevelopmentApprovalFileStatus.SiteInspectionCompleted)
            throw Validation("The governed site inspection must be completed before SCE engineering review.");
        var sourceHandoff = await Handoffs(false).Where(value => value.DevelopmentApprovalFileId == fileId).OrderByDescending(value => value.SequenceNumber).FirstOrDefaultAsync(token)
            ?? throw Validation("The development file must be handed off to Civil Engineering before SCE review.");
        if (sourceHandoff.ToSection != CivilEngineeringPermittingSection.CivilEngineering || sourceHandoff.RecipientUserId != UserId)
            throw new UnauthorizedAccessException("Only the assigned SCE recipient of the current Civil Engineering handoff can submit this review.");
        await RequireAssignedSceAsync(file, token);
        var policy = await FrozenPolicyAsync(file, token);
        var errors = CivilEngineeringPermittingEngineeringReviewPolicy.ValidateSubmit(request);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new { fileId, sourceHandoff.Id, request.CommentCategoryId, comment = request.ReviewComment.Trim(), request.RecommendedOutcome, request.CentralDocumentRecordId, request.CentralDocumentVersionId });
        var retry = await Reviews(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (retry.DevelopmentApprovalFileId != fileId || retry.ReviewerUserId != UserId) throw Conflict("This client request identifier was already used by a different permitting review.");
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different permitting-review values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }
        if (await Reviews(true).AnyAsync(value => value.SourceHandoffId == sourceHandoff.Id, token))
            throw Conflict("The current Civil Engineering handoff already has an immutable SCE review.");
        if (!policy.CommentCategoryIds.Contains(request.CommentCategoryId)) throw Validation("Select an engineering comment category configured by the frozen CIV-CFG-009 policy.");
        if (!policy.AllowedOutcomes.Contains(request.RecommendedOutcome)) throw Validation("Select an outcome configured by the frozen CIV-CFG-009 policy.");
        if (CivilEngineeringPermittingEngineeringReviewPolicy.NeedsHodDecision(request.RecommendedOutcome) && !policy.RequireHodDecision)
            throw Conflict("The frozen CIV-CFG-009 policy does not permit a recommendation to bypass its required HOD decision stage.");
        if ((request.RecommendedOutcome is CivilEngineeringPermittingOutcome.ReturnForCorrection or CivilEngineeringPermittingOutcome.RecommendRejection)
            && policy.RequireReasonForReturnOrRejection && request.ReviewComment.Trim().Length < 10)
            throw Validation("The configured permitting policy requires a detailed return or rejection reason of at least 10 characters.");
        await RequireCommentCategoryAsync(request.CommentCategoryId, policy.CommentCategoryIds, token);
        var evidence = await RequireEvidenceAsync(file, request.CentralDocumentRecordId, request.CentralDocumentVersionId, token);
        var now = DateTime.UtcNow;
        var review = new ProjectCivilDevelopmentApprovalEngineeringReview
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DevelopmentApprovalFileId = file.Id, SourceHandoffId = sourceHandoff.Id,
            ClientRequestId = request.ClientRequestId, RequestHash = requestHash, ReviewerUserId = UserId, CommentCategoryId = request.CommentCategoryId,
            ReviewComment = request.ReviewComment.Trim(), RecommendedOutcome = request.RecommendedOutcome, CentralDocumentRecordId = evidence.DocumentRecordId,
            CentralDocumentVersionId = evidence.Id, WorkflowDefinitionId = file.WorkflowDefinitionId,
            Stage = CivilEngineeringPermittingEngineeringReviewPolicy.NeedsHodDecision(request.RecommendedOutcome)
                ? CivilEngineeringPermittingEngineeringReviewStage.PendingHodDecision
                : CivilEngineeringPermittingEngineeringReviewStage.CorrectionRequested,
            Status = CivilEngineeringPermittingEngineeringReviewPolicy.NeedsHodDecision(request.RecommendedOutcome) ? "PendingApproval" : "CorrectionRequested",
            ApprovalStatus = CivilEngineeringPermittingEngineeringReviewPolicy.NeedsHodDecision(request.RecommendedOutcome) ? "Pending" : "NotApplicable",
            RejectionReason = request.RecommendedOutcome == CivilEngineeringPermittingOutcome.ReturnForCorrection ? request.ReviewComment.Trim() : null,
            ReviewedAt = now, CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.ProjectCivilDevelopmentApprovalEngineeringReviews.Add(review);
        if (CivilEngineeringPermittingEngineeringReviewPolicy.NeedsHodDecision(request.RecommendedOutcome))
        {
            var result = await workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.PermittingReview, review.Id, review.WorkflowDefinitionId);
            if (!result.ExecutionResult.Success || result.Outcome != WorkflowOutcome.Pending)
                throw Conflict(result.ExecutionResult.Message ?? "The configured permitting workflow must start in a pending HOD-decision state.");
            workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.PermittingReview).ApplySubmitOutcome(review, result.Outcome, UserId);
            review.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
            review.UpdatedAt = DateTime.UtcNow; review.UpdatedBy = UserName; review.LastModifiedById = UserId;
        }
        AddRevision(review, CivilEngineeringAuditEventMap.SubmitEngineeringRecommendation, null, Snapshot(review), request.RecommendedOutcome == CivilEngineeringPermittingOutcome.ReturnForCorrection ? review.ReviewComment : null, correlationId);
        AddAudit(review, null, Snapshot(review), correlationId);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([review], token)).Single();
    }

    private IQueryable<ProjectCivilDevelopmentApprovalEngineeringReview> Reviews(bool tracked) =>
        (tracked ? db.ProjectCivilDevelopmentApprovalEngineeringReviews : db.ProjectCivilDevelopmentApprovalEngineeringReviews.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);
    private IQueryable<ProjectCivilDevelopmentApprovalFileHandoff> Handoffs(bool tracked) =>
        (tracked ? db.ProjectCivilDevelopmentApprovalFileHandoffs : db.ProjectCivilDevelopmentApprovalFileHandoffs.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);
    private async Task<ProjectCivilDevelopmentApprovalFile> RequiredFileAsync(Guid id, bool tracked, CancellationToken token) =>
        await (tracked ? db.ProjectCivilDevelopmentApprovalFiles : db.ProjectCivilDevelopmentApprovalFiles.AsNoTracking()).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, token)
        ?? throw new CivilEngineeringPermittingEngineeringReviewNotFoundException("The development approval file was not found.");

    private async Task RequireAssignedSceAsync(ProjectCivilDevelopmentApprovalFile file, CancellationToken token)
    {
        if (!currentUser.Roles.Any(value => string.Equals(value, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException($"The {CivilEngineeringAccessControlRegistry.SupervisingEngineerRole} role is required for an SCE permitting review.");
        if (!await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == file.ProjectId && value.UserId == UserId && value.IsActive && !value.IsDeleted, token))
            throw new UnauthorizedAccessException("The SCE reviewer is not an active member of the linked project.");
    }

    private async Task<Policy> FrozenPolicyAsync(ProjectCivilDevelopmentApprovalFile file, CancellationToken token)
    {
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == file.PermittingConfigurationDecisionId && value.ProfileId == file.ConfigurationProfileId && value.ConfigurationKey == "CIV-CFG-009" && !value.IsDeleted, token)
            ?? throw Validation("The development approval file has no valid frozen CIV-CFG-009 permitting policy.");
        CivilEngineeringPermittingReviewValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringPermittingReviewValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("The frozen CIV-CFG-009 permitting policy cannot be read."); }
        if (value.WorkflowDefinitionId != file.WorkflowDefinitionId || value.CommentCategoryIds.Count == 0 || value.AllowedOutcomes.Count == 0)
            throw Conflict("The development approval file’s frozen CIV-CFG-009 policy is incomplete or inconsistent.");
        return new Policy(value.CommentCategoryIds.Where(id => id != Guid.Empty).ToHashSet(), value.AllowedOutcomes.Distinct().ToHashSet(), value.RequireReasonForReturnOrRejection, value.RequireHodDecision);
    }

    private async Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalLookupOptionDto>> CommentCategoriesAsync(HashSet<Guid> ids, CancellationToken token) =>
        await db.ProjectCatalogEntries.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive && value.CatalogType == "civil-permitting-comment-categories" && ids.Contains(value.Id))
            .OrderBy(value => value.SortOrder).ThenBy(value => value.Name).Select(value => new CivilEngineeringDevelopmentApprovalLookupOptionDto { Id = value.Id, Label = value.Code + " - " + value.Name }).ToListAsync(token);

    private async Task RequireCommentCategoryAsync(Guid id, HashSet<Guid> configured, CancellationToken token)
    {
        if (!await db.ProjectCatalogEntries.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == id && configured.Contains(id) && value.CatalogType == "civil-permitting-comment-categories" && value.IsActive && !value.IsDeleted, token))
            throw Validation("The selected engineering comment category is not an active frozen CIV-CFG-009 category.");
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(ProjectCivilDevelopmentApprovalFile file, Guid recordId, Guid versionId, CancellationToken token)
    {
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == file.DocumentConfigurationDecisionId && value.ProfileId == file.ConfigurationProfileId && value.ConfigurationKey == "CIV-CFG-004" && !value.IsDeleted, token)
            ?? throw Validation("The development approval file has no valid frozen CIV-CFG-004 DMS policy.");
        CivilEngineeringDocumentPolicyValue policy;
        try { policy = JsonSerializer.Deserialize<CivilEngineeringDocumentPolicyValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("The frozen CIV-CFG-004 DMS policy cannot be read."); }
        var version = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId, token)
            ?? throw Validation("Select a current Published central-DMS engineering drawing or review evidence.");
        if (version.DocumentRecordId != recordId || !string.Equals(version.DocumentRecord.MetadataTemplateCode, file.MetadataTemplateCodeSnapshot, StringComparison.OrdinalIgnoreCase)) throw Validation("The selected review evidence does not belong to the frozen Civil DMS template.");
        var extension = NormalizeExtension(Path.GetExtension(version.FileName ?? string.Empty));
        var allowed = policy.AllowedFileExtensions.Select(NormalizeExtension).Where(value => value.Length > 1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!policy.RequireVersioning || policy.MetadataTemplateId != file.MetadataTemplateId || !allowed.Contains(extension) || version.FileSize is null or <= 0 || version.FileSize > checked((long)policy.MaximumFileSizeMb * 1024L * 1024L))
            throw Validation("The selected engineering review evidence is not allowed by the frozen CIV-CFG-004 policy.");
        return version;
    }

    private async Task<IReadOnlyList<CivilEngineeringPermittingEngineeringReviewDto>> MapAsync(IReadOnlyCollection<ProjectCivilDevelopmentApprovalEngineeringReview> values, CancellationToken token)
    {
        var ids = values.Select(value => value.Id).ToList();
        var userIds = values.Select(value => value.ReviewerUserId).Distinct().ToList();
        var categoryIds = values.Select(value => value.CommentCategoryId).Distinct().ToList();
        var recordIds = values.Select(value => value.CentralDocumentRecordId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => Name(value.FirstName, value.LastName, value.UserName), token);
        var categories = await db.ProjectCatalogEntries.AsNoTracking().Where(value => value.TenantId == TenantId && categoryIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Code + " - " + value.Name, token);
        var records = await db.CentralDocumentRecords.AsNoTracking().Where(value => value.TenantId == TenantId && recordIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.DocumentReference, token);
        return values.Select(value => new CivilEngineeringPermittingEngineeringReviewDto { Id = value.Id, DevelopmentApprovalFileId = value.DevelopmentApprovalFileId, SourceHandoffId = value.SourceHandoffId, ReviewerName = users.GetValueOrDefault(value.ReviewerUserId, "Unavailable reviewer"), CommentCategoryLabel = categories.GetValueOrDefault(value.CommentCategoryId, "Unavailable category"), ReviewComment = value.ReviewComment, RecommendedOutcome = value.RecommendedOutcome, Stage = value.Stage, Status = value.Status, ApprovalStatus = value.ApprovalStatus, DocumentReference = records.GetValueOrDefault(value.CentralDocumentRecordId), WorkflowInstanceId = value.WorkflowInstanceId, ReviewedAt = value.ReviewedAt, RowVersion = Convert.ToBase64String(value.RowVersion) }).ToList();
    }

    private void AddRevision(ProjectCivilDevelopmentApprovalEngineeringReview value, string action, object? before, object after, string? reason, string correlationId) =>
        value.Revisions.Add(new ProjectCivilDevelopmentApprovalEngineeringReviewRevision { Id = Guid.NewGuid(), TenantId = TenantId, EngineeringReviewId = value.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = reason, BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private void AddAudit(ProjectCivilDevelopmentApprovalEngineeringReview value, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = CivilEngineeringAuditEventMap.SubmitEngineeringRecommendation, Resource = nameof(ProjectCivilDevelopmentApprovalEngineeringReview), ResourceId = value.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(after, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private static object Snapshot(ProjectCivilDevelopmentApprovalEngineeringReview value) => new { value.Id, value.DevelopmentApprovalFileId, value.SourceHandoffId, value.ReviewerUserId, value.CommentCategoryId, value.ReviewComment, value.RecommendedOutcome, value.CentralDocumentRecordId, value.CentralDocumentVersionId, value.WorkflowDefinitionId, value.WorkflowInstanceId, value.Stage, value.Status, value.ApprovalStatus, value.RejectionReason, value.ReviewedAt };
    private static string Name(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string NormalizeExtension(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : (value.StartsWith('.') ? value : "." + value).Trim().ToLowerInvariant();
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left ?? string.Empty), Encoding.UTF8.GetBytes(right ?? string.Empty));
    private static CivilEngineeringPermittingEngineeringReviewValidationException Validation(string message) => new(message);
    private static CivilEngineeringPermittingEngineeringReviewValidationException Validation(IEnumerable<string> messages) => new(string.Join(" ", messages));
    private static CivilEngineeringPermittingEngineeringReviewConflictException Conflict(string message) => new(message);
    private sealed record Policy(HashSet<Guid> CommentCategoryIds, HashSet<CivilEngineeringPermittingOutcome> AllowedOutcomes, bool RequireReasonForReturnOrRejection, bool RequireHodDecision);
}
