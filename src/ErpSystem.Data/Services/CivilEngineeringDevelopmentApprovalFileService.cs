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

/// <summary>
/// CIV-0401 Building Inspectorate register. It deliberately stops before workflow handoff/recommendation/decision stages.
/// Projects, Estate, Procurement business-partners, central DMS, Security and AuditLogs remain authoritative owners.
/// </summary>
public sealed class CivilEngineeringDevelopmentApprovalFileService(
    ApplicationDbContext db,
    ICurrentUserService currentUser) : ICivilEngineeringDevelopmentApprovalFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringDevelopmentApprovalLookupsDto> GetLookupsAsync(CancellationToken token = default)
    {
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        await RequirePermittingActorAsync(policy, token);
        return new CivilEngineeringDevelopmentApprovalLookupsDto
        {
            Applicants = await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.RegistrationStatus == "Approved")
                .OrderBy(value => value.PartnerCode).ThenBy(value => value.PartnerName).Select(value => new CivilEngineeringDevelopmentApprovalLookupOptionDto { Id = value.Id, Label = value.PartnerCode + " - " + value.PartnerName }).Take(250).ToListAsync(token),
            Projects = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted)
                .OrderBy(value => value.ProjectCode).Select(value => new CivilEngineeringDevelopmentApprovalLookupOptionDto { Id = value.Id, Label = value.ProjectCode + " - " + value.Title }).Take(250).ToListAsync(token),
            Properties = await db.EstateManagedAssets.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted)
                .OrderBy(value => value.AssetCode).ThenBy(value => value.Name).Select(value => new CivilEngineeringDevelopmentApprovalLookupOptionDto { Id = value.Id, Label = value.AssetCode + " - " + value.Name }).Take(250).ToListAsync(token),
            Documents = await DocumentsAsync(policy.DocumentTemplate.TemplateCode, token)
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalFileDto>> ListAsync(CancellationToken token = default)
    {
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        await RequirePermittingActorAsync(policy, token);
        return await MapAsync(await Files(false).OrderByDescending(value => value.CreatedAt).Take(500).ToListAsync(token), token);
    }

    public async Task<CivilEngineeringDevelopmentApprovalFileDto> CreateAsync(CreateCivilEngineeringDevelopmentApprovalFileRequest request, string correlationId, CancellationToken token = default)
    {
        var now = DateTime.UtcNow;
        var errors = CivilEngineeringDevelopmentApprovalFilePolicy.ValidateCreate(request, now);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new
        {
            request.ApplicantBusinessPartnerId, applicantName = request.ApplicantName?.Trim(), request.ProjectId, request.EstateManagedAssetId,
            applicationReference = request.ApplicationReference.Trim(), dueDate = request.DueDate.Date, siteInspectionDueDate = request.SiteInspectionDueDate?.Date,
            evidence = request.ApplicationEvidence.OrderBy(item => item.CentralDocumentVersionId).Select(item => new { item.CentralDocumentRecordId, item.CentralDocumentVersionId })
        });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Files(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different development-file values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }

        var policy = await ResolvePolicyAsync(now, token);
        await RequirePermittingActorAsync(policy, token);
        errors = CivilEngineeringDevelopmentApprovalFilePolicy.ValidateCreate(request, now);
        if (errors.Count > 0) throw Validation(errors);
        await RequireTargetsAsync(request, token);
        var documents = await RequireDocumentsAsync(request.ApplicationEvidence, policy, token);
        var sequence = await Files(true).CountAsync(value => value.CreatedAt >= new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), token) + 1;
        var file = new ProjectCivilDevelopmentApprovalFile
        {
            Id = Guid.NewGuid(), TenantId = TenantId, FileNumber = $"CIV-PER-{now:yyyy}-{sequence:D5}", ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
            ApplicantBusinessPartnerId = request.ApplicantBusinessPartnerId, ApplicantName = await ApplicantNameAsync(request, token), ProjectId = request.ProjectId,
            EstateManagedAssetId = request.EstateManagedAssetId, ApplicationReference = Text(request.ApplicationReference, 120, "Application reference"),
            CurrentSection = "BuildingInspectorate", DueDate = request.DueDate.Date, SiteInspectionDueDate = request.SiteInspectionDueDate?.Date,
            Status = request.SiteInspectionDueDate.HasValue ? CivilEngineeringDevelopmentApprovalFileStatus.SiteInspectionScheduled : CivilEngineeringDevelopmentApprovalFileStatus.Registered,
            ConfigurationProfileId = policy.ProfileId, PermittingConfigurationDecisionId = policy.PermittingDecisionId, DocumentConfigurationDecisionId = policy.DocumentDecisionId,
            WorkflowDefinitionId = policy.Permitting.WorkflowDefinitionId, MetadataTemplateId = policy.DocumentTemplate.Id, MetadataTemplateCodeSnapshot = policy.DocumentTemplate.TemplateCode,
            PolicyHash = policy.PolicyHash, CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        foreach (var document in documents)
            file.Evidence.Add(new ProjectCivilDevelopmentApprovalEvidence { Id = Guid.NewGuid(), TenantId = TenantId, DevelopmentApprovalFileId = file.Id, Kind = CivilEngineeringDevelopmentApprovalEvidenceKind.ApplicationPackage, CentralDocumentRecordId = document.DocumentRecordId, CentralDocumentVersionId = document.Id, CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId });
        db.ProjectCivilDevelopmentApprovalFiles.Add(file);
        AddRevision(file, CivilEngineeringAuditEventMap.CreateDevelopmentApprovalFile, null, Snapshot(file), null, correlationId);
        AddAudit(file, CivilEngineeringAuditEventMap.CreateDevelopmentApprovalFile, null, Snapshot(file), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([file], token)).Single();
    }

    public async Task<CivilEngineeringDevelopmentApprovalFileDto> RecordSiteInspectionAsync(Guid fileId, RecordCivilEngineeringSiteInspectionRequest request, string correlationId, CancellationToken token = default)
    {
        var now = DateTime.UtcNow;
        var errors = CivilEngineeringDevelopmentApprovalFilePolicy.ValidateInspection(request, now);
        if (errors.Count > 0) throw Validation(errors);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var file = await Files(true).Include(value => value.Evidence).SingleOrDefaultAsync(value => value.Id == fileId, token)
            ?? throw new CivilEngineeringDevelopmentApprovalFileNotFoundException("The development approval file was not found.");
        var policy = await ResolvePolicyAsync(now, token);
        await RequirePermittingActorAsync(policy, token);
        EnsureFrozenPolicy(file, policy);
        if (file.Status == CivilEngineeringDevelopmentApprovalFileStatus.SiteInspectionCompleted) throw Conflict("The site inspection is already recorded and immutable.");
        if (file.Status is not (CivilEngineeringDevelopmentApprovalFileStatus.Registered or CivilEngineeringDevelopmentApprovalFileStatus.SiteInspectionScheduled)) throw Conflict("The development file is not ready for site-inspection capture.");
        if (request.SiteInspectedAt.Date < file.CreatedAt.Date) throw Validation("The site inspection cannot predate the development-file registration.");
        var suppliedHash = Hash(new { request.SiteInspectedAt, evidence = request.Evidence.OrderBy(item => item.CentralDocumentVersionId).Select(item => new { item.CentralDocumentRecordId, item.CentralDocumentVersionId }) });
        var existing = await db.ProjectCivilDevelopmentApprovalFileRevisions.AsNoTracking().AnyAsync(value =>
            value.TenantId == TenantId && value.DevelopmentApprovalFileId == file.Id
            && value.Action == CivilEngineeringAuditEventMap.UpdateDevelopmentFileHandoff
            && value.AfterJson.Contains(suppliedHash), token);
        if (existing) { await transaction.CommitAsync(token); return (await MapAsync([file], token)).Single(); }
        var original = DecodeRowVersion(request.RowVersion);
        db.Entry(file).Property(value => value.RowVersion).OriginalValue = original;
        var documents = await RequireDocumentsAsync(request.Evidence, policy, token);
        var before = Snapshot(file);
        file.SiteInspectedAt = request.SiteInspectedAt.Kind == DateTimeKind.Utc ? request.SiteInspectedAt : request.SiteInspectedAt.ToUniversalTime();
        file.Status = CivilEngineeringDevelopmentApprovalFileStatus.SiteInspectionCompleted;
        file.UpdatedAt = now; file.UpdatedBy = UserName; file.LastModifiedById = UserId;
        foreach (var document in documents)
            file.Evidence.Add(new ProjectCivilDevelopmentApprovalEvidence { Id = Guid.NewGuid(), TenantId = TenantId, DevelopmentApprovalFileId = file.Id, Kind = CivilEngineeringDevelopmentApprovalEvidenceKind.SiteInspection, CentralDocumentRecordId = document.DocumentRecordId, CentralDocumentVersionId = document.Id, CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId });
        var after = new { value = Snapshot(file), inspectionRequestHash = suppliedHash };
        AddRevision(file, CivilEngineeringAuditEventMap.UpdateDevelopmentFileHandoff, before, after, "Site inspection recorded; inter-section handoff remains pending CIV-0402.", correlationId);
        AddAudit(file, CivilEngineeringAuditEventMap.UpdateDevelopmentFileHandoff, before, after, correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([file], token)).Single();
    }

    public async Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalFileRevisionDto>> GetHistoryAsync(Guid fileId, CancellationToken token = default)
    {
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        await RequirePermittingActorAsync(policy, token);
        if (!await Files(false).AnyAsync(value => value.Id == fileId, token)) throw new CivilEngineeringDevelopmentApprovalFileNotFoundException("The development approval file was not found.");
        return await db.ProjectCivilDevelopmentApprovalFileRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.DevelopmentApprovalFileId == fileId && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt).Select(value => new CivilEngineeringDevelopmentApprovalFileRevisionDto { Id = value.Id, Action = value.Action, ActorName = value.ActorName, ActorRoles = value.ActorRoles, Reason = value.Reason, CorrelationId = value.CorrelationId, CreatedAt = value.CreatedAt }).ToListAsync(token);
    }

    private IQueryable<ProjectCivilDevelopmentApprovalFile> Files(bool tracked) =>
        (tracked ? db.ProjectCivilDevelopmentApprovalFiles : db.ProjectCivilDevelopmentApprovalFiles.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task RequireTargetsAsync(CreateCivilEngineeringDevelopmentApprovalFileRequest request, CancellationToken token)
    {
        if (!await db.Projects.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == request.ProjectId && !value.IsDeleted, token)) throw Validation("The selected project is unavailable in this tenant.");
        var property = await db.EstateManagedAssets.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.EstateManagedAssetId && !value.IsDeleted, token)
            ?? throw Validation("The selected property or building is unavailable in this tenant.");
        if (property.ProjectId.HasValue && property.ProjectId != request.ProjectId) throw Validation("The selected property or building is linked to a different project.");
        if (request.ApplicantBusinessPartnerId.HasValue && !await db.BusinessPartners.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == request.ApplicantBusinessPartnerId && !value.IsDeleted && value.RegistrationStatus == "Approved", token)) throw Validation("The selected registered applicant is unavailable in this tenant.");
    }

    private async Task<string> ApplicantNameAsync(CreateCivilEngineeringDevelopmentApprovalFileRequest request, CancellationToken token)
    {
        if (!request.ApplicantBusinessPartnerId.HasValue) return Text(request.ApplicantName, 250, "External applicant name");
        return await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId && value.Id == request.ApplicantBusinessPartnerId.Value)
            .Select(value => value.PartnerName).SingleAsync(token);
    }

    private async Task<Policy> ResolvePolicyAsync(DateTime atUtc, CancellationToken token)
    {
        var at = atUtc.Kind == DateTimeKind.Utc ? atUtc : atUtc.ToUniversalTime();
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No Published Civil Engineering configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one Civil Engineering configuration is effective for this date.");
        var profile = profiles[0];
        var decisions = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProfileId == profile.Id && !value.IsDeleted && (value.ConfigurationKey == "CIV-CFG-004" || value.ConfigurationKey == "CIV-CFG-009")).ToListAsync(token);
        var permitting = decisions.SingleOrDefault(value => value.ConfigurationKey == "CIV-CFG-009") ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-009 permitting decision.");
        var document = decisions.SingleOrDefault(value => value.ConfigurationKey == "CIV-CFG-004") ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-004 document policy.");
        if (!Effective(permitting, at) || !Effective(document, at)) throw Validation("CIV-CFG-009 and CIV-CFG-004 must be approved, verified, and effective before registering development files.");
        CivilEngineeringPermittingReviewValue permittingValue;
        CivilEngineeringDocumentPolicyValue documentValue;
        try { permittingValue = JsonSerializer.Deserialize<CivilEngineeringPermittingReviewValue>(permitting.ValueJson, JsonOptions) ?? throw new JsonException(); documentValue = JsonSerializer.Deserialize<CivilEngineeringDocumentPolicyValue>(document.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("The effective Civil Engineering permitting or document policy cannot be read."); }
        if (permittingValue.WorkflowDefinitionId == Guid.Empty || permittingValue.HandoffRoleIds.Count == 0 || documentValue.MetadataTemplateId == Guid.Empty || !documentValue.RequireVersioning) throw Validation("CIV-CFG-009 must configure a workflow and handoff roles, and CIV-CFG-004 must require a DMS template with versioning.");
        var workflow = await db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == permittingValue.WorkflowDefinitionId && !value.IsDeleted && value.IsActive && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !value.EntityType.IsDeleted && value.EntityType.IsActive && value.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.PermittingReview, token);
        if (workflow is null || !await db.WorkflowSteps.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.WorkflowDefinitionId == workflow.Id && !value.IsDeleted, token)) throw Validation("CIV-CFG-009 must select an active Published permitting workflow with at least one step.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == documentValue.MetadataTemplateId && value.IsActive && value.PublishedAt.HasValue && !value.IsDeleted, token)
            ?? throw Validation("The configured Civil Engineering DMS template is unavailable.");
        var extensions = documentValue.AllowedFileExtensions.Select(NormalizeExtension).Where(value => value.Length > 1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (extensions.Count == 0) throw Validation("CIV-CFG-004 has no allowed document extensions.");
        return new Policy(profile.Id, permitting.Id, document.Id, permittingValue, documentValue, template, extensions, checked((long)documentValue.MaximumFileSizeMb * 1024L * 1024L), Hash(new { ProfileId = profile.Id, profile.Version, permitting = permitting.ValueJson, document = document.ValueJson, TemplateId = template.Id, template.TemplateCode }));
    }

    private async Task RequirePermittingActorAsync(Policy policy, CancellationToken token)
    {
        if (!await db.UserRoles.AsNoTracking().AnyAsync(value => value.UserId == UserId && policy.Permitting.HandoffRoleIds.Contains(value.RoleId), token))
            throw new UnauthorizedAccessException("The current user is not assigned to a CIV-CFG-009 permitting handoff role for this tenant.");
    }

    private async Task<List<CentralDocumentVersion>> RequireDocumentsAsync(IEnumerable<CivilEngineeringDevelopmentApprovalEvidenceRequest> values, Policy policy, CancellationToken token)
    {
        var result = new List<CentralDocumentVersion>();
        foreach (var value in values)
        {
            var version = await db.CentralDocumentVersions.AsNoTracking().Include(item => item.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
                .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.CentralDocumentVersionId, token) ?? throw Validation("Select a current Published central-DMS document.");
            if (version.DocumentRecordId != value.CentralDocumentRecordId) throw Validation("The selected DMS document version does not belong to its selected document record.");
            if (!string.Equals(version.DocumentRecord.MetadataTemplateCode, policy.DocumentTemplate.TemplateCode, StringComparison.OrdinalIgnoreCase)) throw Validation($"Development-file evidence must use DMS template {policy.DocumentTemplate.TemplateCode}.");
            var extension = NormalizeExtension(Path.GetExtension(version.FileName ?? string.Empty));
            if (!policy.AllowedExtensions.Contains(extension)) throw Validation($"Files with extension {extension} are not allowed by CIV-CFG-004.");
            if (version.FileSize is null or <= 0 || version.FileSize > policy.MaximumFileBytes) throw Validation($"Evidence must be non-empty and no larger than {policy.Document.MaximumFileSizeMb} MB.");
            result.Add(version);
        }
        return result;
    }

    private async Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalDocumentLookupDto>> DocumentsAsync(string templateCode, CancellationToken token) =>
        await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).Where(value => value.TenantId == TenantId && value.DocumentRecord.MetadataTemplateCode == templateCode)
            .OrderBy(value => value.DocumentRecord.DocumentReference).ThenByDescending(value => value.CreatedAt).Select(value => new CivilEngineeringDevelopmentApprovalDocumentLookupDto { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalFileDto>> MapAsync(IReadOnlyCollection<ProjectCivilDevelopmentApprovalFile> files, CancellationToken token)
    {
        var ids = files.Select(value => value.Id).ToList();
        var projectIds = files.Select(value => value.ProjectId).Distinct().ToList();
        var propertyIds = files.Select(value => value.EstateManagedAssetId).Distinct().ToList();
        var evidence = await db.ProjectCivilDevelopmentApprovalEvidence.AsNoTracking().Where(value => value.TenantId == TenantId && ids.Contains(value.DevelopmentApprovalFileId) && !value.IsDeleted).ToListAsync(token);
        var currentSections = await db.ProjectCivilDevelopmentApprovalFileHandoffs.AsNoTracking()
            .Where(value => value.TenantId == TenantId && ids.Contains(value.DevelopmentApprovalFileId) && !value.IsDeleted)
            .GroupBy(value => value.DevelopmentApprovalFileId)
            .Select(group => new { FileId = group.Key, Section = group.OrderByDescending(value => value.SequenceNumber).Select(value => value.ToSection).First() })
            .ToDictionaryAsync(value => value.FileId, value => value.Section.ToString(), token);
        var recordIds = evidence.Select(value => value.CentralDocumentRecordId).Distinct().ToList();
        var projects = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && projectIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.ProjectCode + " - " + value.Title, token);
        var properties = await db.EstateManagedAssets.AsNoTracking().Where(value => value.TenantId == TenantId && propertyIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.AssetCode + " - " + value.Name, token);
        var records = await db.CentralDocumentRecords.AsNoTracking().Where(value => value.TenantId == TenantId && recordIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.DocumentReference, token);
        return files.Select(value => new CivilEngineeringDevelopmentApprovalFileDto
        {
            Id = value.Id, FileNumber = value.FileNumber, ApplicantBusinessPartnerId = value.ApplicantBusinessPartnerId, ApplicantName = value.ApplicantName, ProjectId = value.ProjectId,
            ProjectLabel = projects.GetValueOrDefault(value.ProjectId, "Unavailable project"), EstateManagedAssetId = value.EstateManagedAssetId, PropertyLabel = properties.GetValueOrDefault(value.EstateManagedAssetId, "Unavailable property"),
            ApplicationReference = value.ApplicationReference, CurrentSection = currentSections.GetValueOrDefault(value.Id, value.CurrentSection), DueDate = value.DueDate, Status = value.Status, SiteInspectionDueDate = value.SiteInspectionDueDate, SiteInspectedAt = value.SiteInspectedAt,
            Evidence = evidence.Where(item => item.DevelopmentApprovalFileId == value.Id).OrderBy(item => item.Kind).ThenBy(item => item.CreatedAt).Select(item => new CivilEngineeringDevelopmentApprovalEvidenceDto { Kind = item.Kind, CentralDocumentRecordId = item.CentralDocumentRecordId, CentralDocumentVersionId = item.CentralDocumentVersionId, DocumentReference = records.GetValueOrDefault(item.CentralDocumentRecordId) }).ToList(),
            RowVersion = Convert.ToBase64String(value.RowVersion)
        }).ToList();
    }

    private static bool Effective(CivilEngineeringConfigurationDecision decision, DateTime at) => decision.Status == CivilEngineeringConfigurationDecisionStatus.Approved && decision.ApprovalStatus == CivilEngineeringConfigurationApprovalStatus.Approved && decision.EvidenceStatus == CivilEngineeringConfigurationEvidenceStatus.Verified && (!decision.EffectiveFrom.HasValue || decision.EffectiveFrom <= at) && (!decision.EffectiveTo.HasValue || decision.EffectiveTo >= at);
    private static void EnsureFrozenPolicy(ProjectCivilDevelopmentApprovalFile file, Policy policy)
    {
        if (file.ConfigurationProfileId != policy.ProfileId || file.PermittingConfigurationDecisionId != policy.PermittingDecisionId || file.DocumentConfigurationDecisionId != policy.DocumentDecisionId || file.WorkflowDefinitionId != policy.Permitting.WorkflowDefinitionId || file.MetadataTemplateId != policy.DocumentTemplate.Id || !FixedEquals(file.PolicyHash, policy.PolicyHash)) throw new CivilEngineeringDevelopmentApprovalFileConflictException("The effective permitting or document configuration changed. Refresh and use the applicable controlled route.");
    }

    private void AddRevision(ProjectCivilDevelopmentApprovalFile file, string action, object? before, object after, string? reason, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.ProjectCivilDevelopmentApprovalFileRevisions.Add(new ProjectCivilDevelopmentApprovalFileRevision { Id = Guid.NewGuid(), TenantId = TenantId, DevelopmentApprovalFileId = file.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = reason, BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }

    private void AddAudit(ProjectCivilDevelopmentApprovalFile file, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectCivilDevelopmentApprovalFile), ResourceId = file.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The development approval file changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52200 and <= 52299) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting development approval file was detected. Refresh and retry."); }
    }

    private static object Snapshot(ProjectCivilDevelopmentApprovalFile value) => new { value.Id, value.FileNumber, value.ApplicantBusinessPartnerId, value.ApplicantName, value.ProjectId, value.EstateManagedAssetId, value.ApplicationReference, value.CurrentSection, value.DueDate, value.Status, value.SiteInspectionDueDate, value.SiteInspectedAt, value.ConfigurationProfileId, value.PermittingConfigurationDecisionId, value.DocumentConfigurationDecisionId, value.WorkflowDefinitionId, value.MetadataTemplateId, value.PolicyHash };
    private static string Text(string? value, int maximum, string label) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) throw Validation($"{label} is required."); return normalized.Length <= maximum ? normalized : throw Validation($"{label} cannot exceed {maximum} characters."); }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string NormalizeExtension(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : (value.StartsWith('.') ? value : "." + value).Trim().ToLowerInvariant();
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left ?? string.Empty), Encoding.UTF8.GetBytes(right ?? string.Empty));
    private static byte[] DecodeRowVersion(string value) { try { return Convert.FromBase64String(value); } catch (FormatException) { throw Validation("The row version is invalid. Refresh and retry."); } }
    private static CivilEngineeringDevelopmentApprovalFileValidationException Validation(string message) => new(message);
    private static CivilEngineeringDevelopmentApprovalFileValidationException Validation(IEnumerable<string> messages) => new(string.Join(" ", messages));
    private static CivilEngineeringDevelopmentApprovalFileConflictException Conflict(string message) => new(message);

    private sealed record Policy(Guid ProfileId, Guid PermittingDecisionId, Guid DocumentDecisionId, CivilEngineeringPermittingReviewValue Permitting, CivilEngineeringDocumentPolicyValue Document, CentralDocumentMetadataTemplate DocumentTemplate, HashSet<string> AllowedExtensions, long MaximumFileBytes, string PolicyHash);
}
