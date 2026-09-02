using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed partial class CivilEngineeringDesignService
{
    public async Task<CivilEngineeringDocumentLookupsDto> GetDocumentLookupsAsync(
        Guid designCaseId,
        CancellationToken token = default)
    {
        var designCase = await RequiredAsync(designCaseId, false, token);
        await RequireProjectAsync(designCase.ProjectId);
        var policy = await ResolveDocumentPolicyAsync(DateTime.UtcNow, token);
        var ownerRoles = await RoleNamesAsync(policy.Value.OwnerRoleIds, token);
        var reviewerRoles = await RoleNamesAsync(policy.Value.ReviewerRoleIds, token);
        var owners = await DocumentMembersAsync(designCase.ProjectId, ownerRoles, token);
        var reviewers = await DocumentMembersAsync(designCase.ProjectId, reviewerRoles, token);
        var packages = await db.ProjectPackages.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == designCase.ProjectId
                            && !value.IsDeleted && value.Code != null && value.Code != string.Empty)
            .OrderBy(value => value.SortOrder).ThenBy(value => value.Code)
            .Select(value => new CivilEngineeringDocumentPackageLookupDto
            {
                Id = value.Id,
                Code = value.Code!,
                Name = value.Name
            }).ToListAsync(token);
        var candidates = await db.CentralDocumentVersions.AsNoTracking()
            .Include(value => value.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId
                            && value.DocumentRecord.MetadataTemplateCode == policy.Template.TemplateCode)
            .OrderBy(value => value.DocumentRecord.DocumentReference)
            .ThenByDescending(value => value.CreatedAt)
            .ToListAsync(token);
        var documents = candidates
            .Select(value => new
            {
                Version = value,
                Extension = CivilEngineeringDocumentRules.NormalizeExtension(Path.GetExtension(value.FileName ?? string.Empty))
            })
            .Where(value => policy.AllowedExtensions.Contains(value.Extension)
                            && value.Version.FileSize is > 0
                            && value.Version.FileSize <= policy.MaximumBytes)
            .Select(value => new CivilEngineeringDocumentVersionLookupDto
            {
                CentralDocumentRecordId = value.Version.DocumentRecordId,
                CentralDocumentVersionId = value.Version.Id,
                DocumentReference = value.Version.DocumentRecord.DocumentReference,
                Title = value.Version.DocumentRecord.Title,
                VersionNumber = value.Version.VersionNumber,
                FileName = value.Version.FileName ?? string.Empty,
                FileExtension = value.Extension,
                FileCategory = CivilEngineeringDocumentRules.FileCategory(value.Extension),
                FileSize = value.Version.FileSize ?? 0
            }).ToList();
        return new CivilEngineeringDocumentLookupsDto
        {
            NamingPolicy = policy.Value.NamingPolicy,
            AllowedFileExtensions = policy.AllowedExtensions.OrderBy(value => value).ToList(),
            MaximumFileSizeMb = policy.Value.MaximumFileSizeMb,
            MetadataTemplateCode = policy.Template.TemplateCode,
            AllowAuthorizedPreview = policy.Value.AllowAuthorizedPreview,
            AllowAuthorizedDownload = policy.Value.AllowAuthorizedDownload,
            Owners = owners,
            Reviewers = reviewers,
            WorkPackages = packages,
            Documents = documents
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringDocumentDto>> ListDocumentsAsync(
        Guid designCaseId,
        CancellationToken token = default)
    {
        var designCase = await RequiredAsync(designCaseId, false, token);
        await RequireProjectAsync(designCase.ProjectId);
        var values = await DocumentQuery(false)
            .Where(value => value.DesignCaseId == designCaseId)
            .OrderBy(value => value.ExpectedDocumentReference)
            .ThenByDescending(value => value.RevisionNumber)
            .ToListAsync(token);
        return await MapDocumentsAsync(values, token);
    }

    public async Task<CivilEngineeringDocumentDto> CreateDocumentAsync(
        Guid designCaseId,
        CreateCivilEngineeringDocumentRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.CentralDocumentRecordId == Guid.Empty
            || request.CentralDocumentVersionId == Guid.Empty || request.OwnerUserId == Guid.Empty
            || request.ReviewerUserId == Guid.Empty)
            throw Validation("Select a current Published DMS file, owner, reviewer, and provide a client request identifier.");
        if (request.OwnerUserId != UserId)
            throw new UnauthorizedAccessException("The document owner must register their own engineering file version.");
        if (request.OwnerUserId == request.ReviewerUserId)
            throw Validation("The document owner and reviewer must be different users.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var designCase = await RequiredAsync(designCaseId, true, token);
        await RequireProjectAsync(designCase.ProjectId);
        var policy = await ResolveDocumentPolicyAsync(DateTime.UtcNow, token);
        await RequireDocumentMemberAsync(designCase.ProjectId, request.OwnerUserId, policy.Value.OwnerRoleIds, "owner", token);
        await RequireDocumentMemberAsync(designCase.ProjectId, request.ReviewerUserId, policy.Value.ReviewerRoleIds, "reviewer", token);
        var package = await ResolveDocumentPackageAsync(designCase.ProjectId, request.ProjectPackageId, policy.Value.NamingPolicy, token);
        var version = await RequireEligibleDocumentVersionAsync(
            request.CentralDocumentRecordId,
            request.CentralDocumentVersionId,
            policy,
            token);
        var expectedReference = CivilEngineeringDocumentRules.ExpectedReference(
            (await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == designCase.ProjectId, token)).ProjectCode,
            request.Discipline,
            package?.Code,
            request.SequenceNumber,
            request.RevisionNumber,
            policy.Value.NamingPolicy);
        if (!string.Equals(version.DocumentRecord.DocumentReference, expectedReference, StringComparison.OrdinalIgnoreCase))
            throw Validation($"The selected DMS document reference must be {expectedReference} under the effective naming policy.");

        var requestHash = Hash(new
        {
            designCaseId,
            request.CentralDocumentRecordId,
            request.CentralDocumentVersionId,
            request.ProjectPackageId,
            request.SupersedesDocumentId,
            request.Discipline,
            request.SequenceNumber,
            request.RevisionNumber,
            request.OwnerUserId,
            request.ReviewerUserId,
            PolicyDecision = policy.DecisionId
        });
        var retry = await DocumentQuery(false).SingleOrDefaultAsync(
            value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            await transaction.RollbackAsync(token);
            return (await MapDocumentsAsync([retry], token)).Single();
        }

        ProjectCivilEngineeringDocument? prior = null;
        var documentKey = Guid.NewGuid();
        if (request.SupersedesDocumentId.HasValue)
        {
            prior = await DocumentQuery(true).SingleOrDefaultAsync(value =>
                value.Id == request.SupersedesDocumentId.Value && value.DesignCaseId == designCaseId, token)
                ?? throw Validation("The selected prior engineering document was not found in this design case.");
            if (prior.Status != CivilEngineeringDocumentStatus.Approved)
                throw Conflict("Only an Approved engineering document can be superseded.");
            if (prior.CentralDocumentRecordId != request.CentralDocumentRecordId
                || prior.CentralDocumentVersionId == request.CentralDocumentVersionId
                || prior.Discipline != request.Discipline
                || prior.SequenceNumber != request.SequenceNumber
                || request.RevisionNumber <= prior.RevisionNumber)
                throw Validation("A successor must use the same DMS record, discipline and sequence, a newer DMS version, and a higher revision number.");
            documentKey = prior.DocumentKey;
        }

        var now = DateTime.UtcNow;
        var entity = new ProjectCivilEngineeringDocument
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DesignCaseId = designCaseId,
            ProjectPackageId = package?.Id,
            DocumentKey = documentKey,
            ClientRequestId = request.ClientRequestId,
            RequestHash = requestHash,
            SupersedesDocumentId = prior?.Id,
            Discipline = request.Discipline,
            FileCategory = CivilEngineeringDocumentRules.FileCategory(Path.GetExtension(version.FileName ?? string.Empty)),
            FileExtension = CivilEngineeringDocumentRules.NormalizeExtension(Path.GetExtension(version.FileName ?? string.Empty)),
            SequenceNumber = request.SequenceNumber,
            RevisionNumber = request.RevisionNumber,
            ExpectedDocumentReference = expectedReference,
            DocumentReferenceSnapshot = version.DocumentRecord.DocumentReference,
            DocumentTitleSnapshot = version.DocumentRecord.Title,
            DmsVersionSnapshot = version.VersionNumber,
            OwnerUserId = request.OwnerUserId,
            ReviewerUserId = request.ReviewerUserId,
            ConfigurationProfileId = policy.ProfileId,
            ConfigurationDecisionId = policy.DecisionId,
            MetadataTemplateId = policy.Template.Id,
            MetadataTemplateCodeSnapshot = policy.Template.TemplateCode,
            PolicyHash = policy.PolicyHash,
            CentralDocumentRecordId = request.CentralDocumentRecordId,
            CentralDocumentVersionId = request.CentralDocumentVersionId,
            CorrelationId = NormalizeCorrelation(correlationId),
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        if (prior is not null)
        {
            var before = DocumentSnapshot(prior);
            prior.Status = CivilEngineeringDocumentStatus.Superseded;
            prior.ReviewReason = $"Superseded by {expectedReference}.";
            prior.UpdatedAt = now;
            prior.UpdatedBy = UserName;
            prior.LastModifiedById = UserId;
            AddDocumentRevision(prior, CivilEngineeringAuditEventMap.RetireEngineeringFileVersion, before, DocumentSnapshot(prior), prior.ReviewReason, correlationId);
            AddDocumentAudit(prior, CivilEngineeringAuditEventMap.RetireEngineeringFileVersion, before, DocumentSnapshot(prior), correlationId);
            await SaveDocumentAsync(token);
        }
        db.ProjectCivilEngineeringDocuments.Add(entity);
        AddDocumentRevision(entity, CivilEngineeringAuditEventMap.CreateEngineeringFileVersion, null, DocumentSnapshot(entity), null, correlationId);
        AddDocumentAudit(entity, CivilEngineeringAuditEventMap.CreateEngineeringFileVersion, null, DocumentSnapshot(entity), correlationId);
        await SaveDocumentAsync(token);
        await transaction.CommitAsync(token);
        var created = await RequiredDocumentAsync(entity.Id, false, token);
        return (await MapDocumentsAsync([created], token)).Single();
    }

    public async Task<CivilEngineeringDocumentDto> SubmitDocumentAsync(
        Guid documentId,
        SubmitCivilEngineeringDocumentRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var entity = await RequiredDocumentAsync(documentId, true, token);
        await RequireProjectAsync(entity.DesignCase.ProjectId);
        var mutationHash = Hash(new { documentId, Action = "Submit" });
        if (IsDocumentMutationRetry(entity, request.ClientRequestId, mutationHash))
        {
            await transaction.RollbackAsync(token);
            return (await MapDocumentsAsync([entity], token)).Single();
        }
        CheckDocumentVersion(entity.RowVersion, request.RowVersion);
        CivilEngineeringDocumentRules.EnsureCanSubmit(entity.Status);
        if (entity.OwnerUserId != UserId)
            throw new UnauthorizedAccessException("Only the assigned document owner can submit this engineering file for review.");
        var policy = await ResolveDocumentPolicyAsync(DateTime.UtcNow, token);
        EnsureFrozenDocumentPolicy(entity, policy);
        await RequireDocumentMemberAsync(entity.DesignCase.ProjectId, UserId, policy.Value.OwnerRoleIds, "owner", token);
        await RequireEligibleDocumentVersionAsync(entity.CentralDocumentRecordId, entity.CentralDocumentVersionId, policy, token);
        var before = DocumentSnapshot(entity);
        entity.Status = CivilEngineeringDocumentStatus.ForReview;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.SubmittedById = UserId;
        entity.ReviewedAt = null;
        entity.ReviewedById = null;
        entity.ReviewReason = null;
        TouchDocument(entity, request.ClientRequestId, mutationHash);
        AddDocumentRevision(entity, CivilEngineeringAuditEventMap.SubmitDrawingForReview, before, DocumentSnapshot(entity), null, correlationId);
        AddDocumentAudit(entity, CivilEngineeringAuditEventMap.SubmitDrawingForReview, before, DocumentSnapshot(entity), correlationId);
        await SaveDocumentAsync(token);
        await transaction.CommitAsync(token);
        return (await MapDocumentsAsync([entity], token)).Single();
    }

    public async Task<CivilEngineeringDocumentDto> ReviewDocumentAsync(
        Guid documentId,
        ReviewCivilEngineeringDocumentRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Review reason");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var entity = await RequiredDocumentAsync(documentId, true, token);
        await RequireProjectAsync(entity.DesignCase.ProjectId);
        var mutationHash = Hash(new { documentId, Action = request.Approve ? "Approve" : "Return", Reason = reason });
        if (IsDocumentMutationRetry(entity, request.ClientRequestId, mutationHash))
        {
            await transaction.RollbackAsync(token);
            return (await MapDocumentsAsync([entity], token)).Single();
        }
        CheckDocumentVersion(entity.RowVersion, request.RowVersion);
        CivilEngineeringDocumentRules.EnsureCanReview(entity.Status);
        if (entity.ReviewerUserId != UserId)
            throw new UnauthorizedAccessException("Only the assigned independent reviewer can decide this engineering document.");
        if (entity.OwnerUserId == UserId)
            throw Conflict("The document owner cannot approve or return their own engineering document.");
        var policy = await ResolveDocumentPolicyAsync(DateTime.UtcNow, token);
        EnsureFrozenDocumentPolicy(entity, policy);
        await RequireDocumentMemberAsync(entity.DesignCase.ProjectId, UserId, policy.Value.ReviewerRoleIds, "reviewer", token);
        await RequireEligibleDocumentVersionAsync(entity.CentralDocumentRecordId, entity.CentralDocumentVersionId, policy, token);
        var before = DocumentSnapshot(entity);
        entity.Status = request.Approve ? CivilEngineeringDocumentStatus.Approved : CivilEngineeringDocumentStatus.Returned;
        entity.ReviewedAt = DateTime.UtcNow;
        entity.ReviewedById = UserId;
        entity.ReviewReason = reason;
        TouchDocument(entity, request.ClientRequestId, mutationHash);
        var action = request.Approve ? CivilEngineeringAuditEventMap.PublishEngineeringFileVersion : CivilEngineeringAuditEventMap.RejectDrawing;
        AddDocumentRevision(entity, action, before, DocumentSnapshot(entity), reason, correlationId);
        AddDocumentAudit(entity, action, before, DocumentSnapshot(entity), correlationId);
        await SaveDocumentAsync(token);
        await transaction.CommitAsync(token);
        return (await MapDocumentsAsync([entity], token)).Single();
    }

    private IQueryable<ProjectCivilEngineeringDocument> DocumentQuery(bool tracked)
    {
        var query = db.ProjectCivilEngineeringDocuments
            .Include(value => value.DesignCase)
            .Include(value => value.ProjectPackage)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<ProjectCivilEngineeringDocument> RequiredDocumentAsync(Guid id, bool tracked, CancellationToken token) =>
        await DocumentQuery(tracked).SingleOrDefaultAsync(value => value.Id == id, token)
        ?? throw new CivilEngineeringDesignNotFoundException("The engineering document registration was not found.");

    private async Task<DocumentPolicy> ResolveDocumentPolicyAsync(DateTime atUtc, CancellationToken token)
    {
        var at = atUtc.Kind == DateTimeKind.Utc ? atUtc : atUtc.ToUniversalTime();
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted
                            && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published
                            && value.EffectiveFrom <= at
                            && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version)
            .Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No Published Civil Engineering configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault)
            throw Conflict("More than one Civil Engineering configuration is effective for this date.");
        var profile = profiles[0];
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-004"
            && !value.IsDeleted, token) ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-004 decision.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved
            || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved
            || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified
            || (decision.EffectiveFrom.HasValue && decision.EffectiveFrom > at)
            || (decision.EffectiveTo.HasValue && decision.EffectiveTo < at))
            throw Validation("CIV-CFG-004 is not approved, verified, and effective for this date.");
        CivilEngineeringDocumentPolicyValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringDocumentPolicyValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-004 contains invalid engineering document policy data."); }
        if (!value.RequireVersioning) throw Validation("CIV-CFG-004 must require central-DMS versioning for governed engineering files.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == value.MetadataTemplateId && item.IsActive
            && item.PublishedAt.HasValue && !item.IsDeleted, token)
            ?? throw Validation("The configured engineering DMS template is not active and Published.");
        var allowed = value.AllowedFileExtensions.Select(CivilEngineeringDocumentRules.NormalizeExtension)
            .Where(item => item.Length > 1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowed.Count == 0) throw Validation("CIV-CFG-004 has no allowed engineering file extensions.");
        return new DocumentPolicy(profile.Id, decision.Id, value, template, allowed,
            checked((long)value.MaximumFileSizeMb * 1024L * 1024L), Hash(new
            {
                Profile = profile.Id,
                profile.Version,
                Decision = decision.Id,
                decision.ValueJson,
                Template = template.Id,
                template.TemplateCode
            }));
    }

    private async Task<CentralDocumentVersion> RequireEligibleDocumentVersionAsync(
        Guid recordId,
        Guid versionId,
        DocumentPolicy policy,
        CancellationToken token)
    {
        var version = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId, token)
            ?? throw Validation("Select a current Published current-tenant DMS engineering file version.");
        if (version.DocumentRecordId != recordId) throw Validation("The DMS version does not belong to the selected document record.");
        if (!string.Equals(version.DocumentRecord.MetadataTemplateCode, policy.Template.TemplateCode, StringComparison.OrdinalIgnoreCase))
            throw Validation($"The engineering document must use DMS template {policy.Template.TemplateCode}.");
        var extension = CivilEngineeringDocumentRules.NormalizeExtension(Path.GetExtension(version.FileName ?? string.Empty));
        if (!policy.AllowedExtensions.Contains(extension)) throw Validation($"Files with extension {extension} are not allowed by CIV-CFG-004.");
        if (version.FileSize is null or <= 0 || version.FileSize > policy.MaximumBytes)
            throw Validation($"The engineering file must be non-empty and no larger than {policy.Value.MaximumFileSizeMb} MB.");
        await RequireCompleteDocumentMetadataAsync(version.DocumentRecordId, policy.Template, token);
        return version;
    }

    private async Task RequireCompleteDocumentMetadataAsync(Guid recordId, CentralDocumentMetadataTemplate template, CancellationToken token)
    {
        IReadOnlyList<string> required;
        try { required = JsonSerializer.Deserialize<List<string>>(template.RequiredFieldsJson, JsonOptions) ?? []; }
        catch (JsonException) { throw Conflict("The configured engineering DMS template has invalid required-field metadata."); }
        var captured = await db.CentralDocumentMetadataValues.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.DocumentRecordId == recordId
                            && !value.IsDeleted && value.FieldValue != null && value.FieldValue != string.Empty)
            .Select(value => new { value.FieldKey, value.FieldLabel }).ToListAsync(token);
        var keys = captured.SelectMany(value => new[] { MetadataKey(value.FieldKey), MetadataKey(value.FieldLabel) }).ToHashSet();
        var missing = required.Where(value => !keys.Contains(MetadataKey(value))).ToList();
        if (missing.Count > 0) throw Validation($"Complete the required DMS metadata before registering this file: {string.Join(", ", missing)}.");
    }

    private async Task<ProjectPackage?> ResolveDocumentPackageAsync(Guid projectId, Guid? packageId, CivilEngineeringDocumentNamingPolicy policy, CancellationToken token)
    {
        if (!packageId.HasValue)
        {
            if (policy == CivilEngineeringDocumentNamingPolicy.ProjectWorkPackageSequenceRevision)
                throw Validation("Select a work package required by the effective engineering document naming policy.");
            return null;
        }
        var package = await db.ProjectPackages.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ProjectId == projectId && value.Id == packageId.Value
            && !value.IsDeleted && value.Code != null && value.Code != string.Empty, token);
        return package ?? throw Validation("The selected work package is not active in this tenant and project.");
    }

    private async Task<HashSet<string>> RoleNamesAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken token)
    {
        if (roleIds.Count == 0) throw Validation("CIV-CFG-004 must configure at least one role for this document responsibility.");
        var names = (await db.Roles.AsNoTracking().Where(value => roleIds.Contains(value.Id) && value.Name != null)
            .Select(value => value.Name!).ToListAsync(token)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (names.Count != roleIds.Distinct().Count()) throw Validation("One or more CIV-CFG-004 role selections no longer exist.");
        return names;
    }

    private async Task<IReadOnlyList<CivilEngineeringDocumentMemberLookupDto>> DocumentMembersAsync(Guid projectId, HashSet<string> roleNames, CancellationToken token)
    {
        var members = await db.ProjectMembers.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsActive
                            && !value.IsDeleted && roleNames.Contains(value.Role))
            .Join(db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.IsActive),
                member => member.UserId, user => user.Id, (member, user) => new { member, user })
            .ToListAsync(token);
        var result = new List<CivilEngineeringDocumentMemberLookupDto>();
        foreach (var value in members)
        {
            var role = await db.UserRoles.AsNoTracking().Where(item => item.UserId == value.user.Id)
                .Join(db.Roles.AsNoTracking(), item => item.RoleId, item => item.Id, (_, item) => item.Name)
                .AnyAsync(name => name != null && roleNames.Contains(name), token);
            if (!role) continue;
            result.Add(new CivilEngineeringDocumentMemberLookupDto
            {
                UserId = value.user.Id,
                DisplayName = DisplayName(value.user),
                RoleName = value.member.Role
            });
        }
        return result.OrderBy(value => value.DisplayName).ToList();
    }

    private async Task RequireDocumentMemberAsync(Guid projectId, Guid userId, IReadOnlyCollection<Guid> roleIds, string label, CancellationToken token)
    {
        var roleNames = await RoleNamesAsync(roleIds, token);
        var valid = await DocumentMembersAsync(projectId, roleNames, token);
        if (valid.All(value => value.UserId != userId))
            throw Validation($"Select an active current-tenant project member configured as a document {label} in CIV-CFG-004.");
    }

    private async Task<IReadOnlyList<CivilEngineeringDocumentDto>> MapDocumentsAsync(IReadOnlyCollection<ProjectCivilEngineeringDocument> values, CancellationToken token)
    {
        var userIds = values.SelectMany(value => new[] { value.OwnerUserId, value.ReviewerUserId }).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, token);
        return values.Select(value => new CivilEngineeringDocumentDto
        {
            Id = value.Id,
            DesignCaseId = value.DesignCaseId,
            ProjectPackageId = value.ProjectPackageId,
            ProjectPackageLabel = value.ProjectPackage is null ? null : $"{value.ProjectPackage.Code} · {value.ProjectPackage.Name}",
            DocumentKey = value.DocumentKey,
            SupersedesDocumentId = value.SupersedesDocumentId,
            Discipline = value.Discipline,
            FileCategory = value.FileCategory,
            FileExtension = value.FileExtension,
            SequenceNumber = value.SequenceNumber,
            RevisionNumber = value.RevisionNumber,
            ExpectedDocumentReference = value.ExpectedDocumentReference,
            DocumentReference = value.DocumentReferenceSnapshot,
            DocumentTitle = value.DocumentTitleSnapshot,
            DmsVersion = value.DmsVersionSnapshot,
            OwnerUserId = value.OwnerUserId,
            OwnerName = users.TryGetValue(value.OwnerUserId, out var owner) ? DisplayName(owner) : "Unavailable user",
            ReviewerUserId = value.ReviewerUserId,
            ReviewerName = users.TryGetValue(value.ReviewerUserId, out var reviewer) ? DisplayName(reviewer) : "Unavailable user",
            Status = value.Status,
            CentralDocumentRecordId = value.CentralDocumentRecordId,
            CentralDocumentVersionId = value.CentralDocumentVersionId,
            SubmittedAt = value.SubmittedAt,
            ReviewedAt = value.ReviewedAt,
            ReviewReason = value.ReviewReason,
            RowVersion = Convert.ToBase64String(value.RowVersion)
        }).ToList();
    }

    private static string DisplayName(ApplicationUser user)
    {
        var full = $"{user.FirstName} {user.LastName}".Trim();
        return full.Length > 0 ? full : user.UserName ?? user.Id.ToString();
    }

    private static string MetadataKey(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static void EnsureFrozenDocumentPolicy(ProjectCivilEngineeringDocument entity, DocumentPolicy policy)
    {
        if (entity.ConfigurationProfileId != policy.ProfileId || entity.ConfigurationDecisionId != policy.DecisionId
            || entity.MetadataTemplateId != policy.Template.Id || !FixedEquals(entity.PolicyHash, policy.PolicyHash))
            throw Conflict("The effective engineering document policy changed. Register a new governed version under the current policy.");
    }

    private static bool IsDocumentMutationRetry(ProjectCivilEngineeringDocument value, Guid requestId, string hash)
    {
        if (value.LastMutationClientRequestId != requestId) return false;
        if (!FixedEquals(value.LastMutationRequestHash, hash)) throw RetryConflict();
        return true;
    }

    private void TouchDocument(ProjectCivilEngineeringDocument value, Guid requestId, string hash)
    {
        value.LastMutationClientRequestId = requestId;
        value.LastMutationRequestHash = hash;
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = UserName;
        value.LastModifiedById = UserId;
    }

    private static void CheckDocumentVersion(byte[] current, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Conflict("The engineering document row version is invalid. Refresh and retry."); }
        if (!CryptographicOperations.FixedTimeEquals(current, expected))
            throw Conflict("The engineering document changed. Refresh and retry.");
    }

    private void AddDocumentRevision(ProjectCivilEngineeringDocument value, string action, object? before, object after, string? reason, string correlationId) =>
        value.Revisions.Add(new ProjectCivilEngineeringDocumentRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, EngineeringDocumentId = value.Id, Action = action,
            ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles,
            CorrelationId = NormalizeCorrelation(correlationId), Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        });

    private void AddDocumentAudit(ProjectCivilEngineeringDocument value, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = nameof(ProjectCivilEngineeringDocument), ResourceId = value.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = NormalizeCorrelation(correlationId), value = after }, JsonOptions),
            IpAddress = currentUser.IpAddress, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private static object DocumentSnapshot(ProjectCivilEngineeringDocument value) => new
    {
        value.Id, value.DesignCaseId, value.ProjectPackageId, value.DocumentKey, value.SupersedesDocumentId,
        value.Discipline, value.FileCategory, value.FileExtension, value.SequenceNumber, value.RevisionNumber,
        value.ExpectedDocumentReference, value.DocumentReferenceSnapshot, value.DocumentTitleSnapshot,
        value.DmsVersionSnapshot, value.OwnerUserId, value.ReviewerUserId, value.Status,
        value.ConfigurationProfileId, value.ConfigurationDecisionId, value.MetadataTemplateId,
        value.MetadataTemplateCodeSnapshot, value.PolicyHash, value.CentralDocumentRecordId,
        value.CentralDocumentVersionId, value.SubmittedAt, value.SubmittedById, value.ReviewedAt,
        value.ReviewedById, value.ReviewReason
    };

    private async Task SaveDocumentAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The engineering document changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 51930 and <= 51949)
        { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 }
                                                  || exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("The engineering document request, DMS version, revision, or successor already exists."); }
    }

    private sealed record DocumentPolicy(
        Guid ProfileId,
        Guid DecisionId,
        CivilEngineeringDocumentPolicyValue Value,
        CentralDocumentMetadataTemplate Template,
        HashSet<string> AllowedExtensions,
        long MaximumBytes,
        string PolicyHash);
}
