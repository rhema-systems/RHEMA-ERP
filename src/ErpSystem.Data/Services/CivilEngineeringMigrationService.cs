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
/// CIV-0605. This is intentionally a validation, reconciliation and sign-off boundary.
/// It never creates design, permitting, maintenance, complaint or test owner records.
/// Those modules remain responsible for separately authorized historical posting.
/// </summary>
public sealed class CivilEngineeringMigrationService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService) : ICivilEngineeringMigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringMigrationLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        var policy = await ResolvePolicyAsync(token);
        await RequireProjectAsync(projectId, token);
        await RequireConfiguredProjectActorAsync(projectId, policy.Value.OwnerRoleIds, "stage a Civil migration batch", token);
        return new CivilEngineeringMigrationLookupsDto
        {
            SourceTypes = policy.Value.SourceTypes.Distinct().OrderBy(value => value).ToList(),
            RecordTypes = Enum.GetValues<CivilEngineeringMigrationRecordType>()
                .Where(value => value != CivilEngineeringMigrationRecordType.PhysicalFileReference || policy.Value.PreservePhysicalFileReference)
                .ToList(),
            Documents = await DocumentLookupsAsync(null, token),
            ReconciliationDocuments = await DocumentLookupsAsync(policy.Template.TemplateCode, token),
            PreservePhysicalFileReference = policy.Value.PreservePhysicalFileReference,
            RequireReconciliation = policy.Value.RequireReconciliation,
            RequireSignedAcceptance = policy.Value.RequireSignedAcceptance
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringMigrationBatchDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var batches = await Batches(false).Where(value => value.ProjectId == projectId)
            .OrderByDescending(value => value.CreatedAt).Take(100).ToListAsync(token);
        return await MapAsync(batches, token);
    }

    public async Task<CivilEngineeringMigrationBatchDto> StageAsync(Guid projectId, StageCivilEngineeringMigrationBatchRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required for a migration batch.");
        if (request.Records.Count is < 1 or > 1000)
            throw Validation("A Civil migration batch must contain between 1 and 1,000 records.");

        var inputs = request.Records.Select(value => new StagedInput(
            value.RecordType,
            CleanRequired(value.SourceReference, 180, "Source reference"),
            CleanRequired(value.Title, 250, "Record title"),
            value.RecordDate?.ToUniversalTime(),
            value.CentralDocumentRecordId,
            value.CentralDocumentVersionId,
            Clean(value.PhysicalFileReference, 500))).ToList();

        var requestHash = Hash(new
        {
            projectId,
            request.ClientRequestId,
            request.SourceType,
            sourceRegisterReference = CleanRequired(request.SourceRegisterReference, 180, "Source register reference"),
            records = inputs.Select(value => new
            {
                value.RecordType,
                value.SourceReference,
                value.Title,
                value.RecordDate,
                value.CentralDocumentRecordId,
                value.CentralDocumentVersionId,
                value.PhysicalFileReference
            }).ToList()
        });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Batches(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash))
                throw Conflict("This client request identifier was already used with different Civil migration values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }

        var policy = await ResolvePolicyAsync(token);
        EnsureMigrationPolicy(policy.Value);
        if (!policy.Value.SourceTypes.Contains(request.SourceType))
            throw Validation("Select a migration source enabled by the effective CIV-CFG-013 decision.");
        await RequireProjectAsync(projectId, token);
        await RequireConfiguredProjectActorAsync(projectId, policy.Value.OwnerRoleIds, "stage a Civil migration batch", token);

        var now = DateTime.UtcNow;
        var batch = new ProjectCivilMigrationBatch
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ClientRequestId = request.ClientRequestId,
            RequestHash = requestHash, SourceType = request.SourceType,
            SourceRegisterReference = CleanRequired(request.SourceRegisterReference, 180, "Source register reference"),
            ConfigurationProfileId = policy.ProfileId, ConfigurationDecisionId = policy.DecisionId,
            ReconciliationEvidenceTemplateId = policy.Template.Id,
            ReconciliationEvidenceTemplateCodeSnapshot = policy.Template.TemplateCode,
            PolicyHash = policy.PolicyHash, SubmittedByUserId = UserId,
            CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };

        var existingReferences = await db.ProjectCivilMigrationRecords.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && inputs.Select(item => item.SourceReference).Contains(value.SourceReference)
                && value.MigrationBatch.ProjectId == projectId && value.MigrationBatch.Status != CivilEngineeringMigrationBatchStatus.ValidationFailed)
            .Select(value => value.SourceReference).ToListAsync(token);
        var knownReferences = existingReferences.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inputReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (input, offset) in inputs.Select((value, index) => (value, index)))
        {
            var sequence = offset + 1;
            var sourceReference = input.SourceReference;
            var title = input.Title;
            var physicalReference = input.PhysicalFileReference;
            var issues = new List<(string Code, string Message)>();
            if (!inputReferences.Add(sourceReference))
                issues.Add(("MIGRATION_DUPLICATE_SOURCE_REFERENCE", "The source reference is repeated in this migration batch."));
            if (knownReferences.Contains(sourceReference))
                issues.Add(("MIGRATION_SOURCE_REFERENCE_ALREADY_STAGED", "This source reference was already staged for the selected project."));
            if ((input.CentralDocumentRecordId.HasValue) != (input.CentralDocumentVersionId.HasValue))
                issues.Add(("MIGRATION_DOCUMENT_PAIR_REQUIRED", "A central-DMS record and version must be selected together."));
            if (input.RecordType == CivilEngineeringMigrationRecordType.PhysicalFileReference && string.IsNullOrWhiteSpace(physicalReference))
                issues.Add(("MIGRATION_PHYSICAL_REFERENCE_REQUIRED", "A physical file reference is required for a physical-file migration record."));
            if (input.RecordType != CivilEngineeringMigrationRecordType.PhysicalFileReference && !input.CentralDocumentVersionId.HasValue)
                issues.Add(("MIGRATION_CENTRAL_DOCUMENT_REQUIRED", "A current Published central-DMS document is required for this historical record type."));
            if (!policy.Value.PreservePhysicalFileReference && !string.IsNullOrWhiteSpace(physicalReference))
                issues.Add(("MIGRATION_PHYSICAL_REFERENCE_NOT_ALLOWED", "CIV-CFG-013 does not permit physical-file references."));

            CentralDocumentVersion? document = null;
            if (input.CentralDocumentRecordId.HasValue && input.CentralDocumentVersionId.HasValue)
            {
                document = await CurrentDocumentAsync(input.CentralDocumentRecordId.Value, input.CentralDocumentVersionId.Value, null, token);
                if (document is null)
                    issues.Add(("MIGRATION_DOCUMENT_NOT_CURRENT", "Select a current Published central-DMS document from the active tenant."));
            }

            var record = new ProjectCivilMigrationRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId, MigrationBatchId = batch.Id, Sequence = sequence,
                RecordType = input.RecordType, SourceReference = sourceReference, Title = title,
                RecordDate = input.RecordDate, CentralDocumentRecordId = document?.DocumentRecordId,
                CentralDocumentVersionId = document?.Id, PhysicalFileReference = physicalReference,
                ValidationStatus = issues.Count == 0 ? "Valid" : "Error",
                ValidationMessage = issues.Count == 0 ? null : string.Join(" ", issues.Select(value => value.Message)),
                CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            batch.Records.Add(record);
            foreach (var issue in issues)
            {
                batch.ValidationIssues.Add(new ProjectCivilMigrationValidationIssue
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, MigrationBatchId = batch.Id, MigrationRecordId = record.Id,
                    Sequence = sequence, Code = issue.Code, Severity = "Error", Message = issue.Message,
                    CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
                });
            }
        }

        batch.RecordCount = batch.Records.Count;
        batch.ErrorCount = batch.Records.Count(value => value.ValidationStatus == "Error");
        batch.Status = batch.ErrorCount == 0 ? CivilEngineeringMigrationBatchStatus.AwaitingReconciliation : CivilEngineeringMigrationBatchStatus.ValidationFailed;
        AddRevision(batch, CivilEngineeringAuditEventMap.CreateCivilMigrationBatch, null, Snapshot(batch), "Historical records staged only; owner posting remains disabled.", correlationId);
        AddAudit(batch, CivilEngineeringAuditEventMap.CreateCivilMigrationBatch, null, Snapshot(batch), correlationId);
        if (batch.ErrorCount == 0)
        {
            AddRevision(batch, CivilEngineeringAuditEventMap.SubmitCivilMigrationBatch, null, Snapshot(batch), "Pre-posting validation succeeded and independent reconciliation is required.", correlationId);
            AddAudit(batch, CivilEngineeringAuditEventMap.SubmitCivilMigrationBatch, null, Snapshot(batch), correlationId);
        }
        db.ProjectCivilMigrationBatches.Add(batch);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([batch], token)).Single();
    }

    public async Task<CivilEngineeringMigrationBatchDto> ReconcileAsync(Guid projectId, Guid batchId, ReconcileCivilEngineeringMigrationBatchRequest request, string correlationId, CancellationToken token = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var batch = await LoadAsync(projectId, batchId, true, token);
        ApplyRowVersion(batch, request.RowVersion);
        var policy = await ResolveFrozenPolicyAsync(batch, token);
        await RequireConfiguredProjectActorAsync(projectId, policy.Value.ReviewerRoleIds, "reconcile a Civil migration batch", token);
        if (batch.SubmittedByUserId == UserId)
            throw new UnauthorizedAccessException("The batch submitter cannot reconcile the same Civil migration batch.");
        if (batch.Status != CivilEngineeringMigrationBatchStatus.AwaitingReconciliation || batch.ErrorCount != 0)
            throw Conflict("Only a validation-clean Civil migration batch can be reconciled.");
        var evidence = await CurrentDocumentAsync(request.ReconciliationDocumentRecordId, request.ReconciliationDocumentVersionId, policy.Template.TemplateCode, token)
            ?? throw Validation("Select a current Published reconciliation document using the CIV-CFG-013 DMS template.");
        var before = Snapshot(batch);
        var now = DateTime.UtcNow;
        batch.Status = CivilEngineeringMigrationBatchStatus.Reconciled;
        batch.ReconciledByUserId = UserId;
        batch.ReconciledAt = now;
        batch.ReconciliationDocumentRecordId = evidence.DocumentRecordId;
        batch.ReconciliationDocumentVersionId = evidence.Id;
        batch.ReconciliationDeclaration = CleanRequired(request.Declaration, 2000, "Reconciliation declaration");
        batch.CorrelationId = Correlation(correlationId);
        batch.UpdatedAt = now; batch.UpdatedBy = UserName; batch.LastModifiedById = UserId;
        AddRevision(batch, CivilEngineeringAuditEventMap.SubmitCivilMigrationBatch, before, Snapshot(batch), batch.ReconciliationDeclaration, correlationId);
        AddAudit(batch, CivilEngineeringAuditEventMap.SubmitCivilMigrationBatch, before, Snapshot(batch), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([batch], token)).Single();
    }

    public async Task<CivilEngineeringMigrationBatchDto> SignOffAsync(Guid projectId, Guid batchId, SignOffCivilEngineeringMigrationBatchRequest request, string correlationId, CancellationToken token = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var batch = await LoadAsync(projectId, batchId, true, token);
        ApplyRowVersion(batch, request.RowVersion);
        var policy = await ResolveFrozenPolicyAsync(batch, token);
        await RequireConfiguredProjectActorAsync(projectId, policy.Value.SignOffRoleIds, "sign off a Civil migration batch", token);
        if (batch.SubmittedByUserId == UserId || batch.ReconciledByUserId == UserId)
            throw new UnauthorizedAccessException("The submitter or reconciler cannot sign off the same Civil migration batch.");
        if (batch.Status != CivilEngineeringMigrationBatchStatus.Reconciled)
            throw Conflict("Only an independently reconciled Civil migration batch can be signed off.");
        var before = Snapshot(batch);
        var now = DateTime.UtcNow;
        batch.Status = CivilEngineeringMigrationBatchStatus.ReadyForOwnerPosting;
        batch.SignedOffByUserId = UserId;
        batch.SignedOffAt = now;
        batch.SignOffDeclaration = CleanRequired(request.Declaration, 2000, "Sign-off declaration");
        batch.CorrelationId = Correlation(correlationId);
        batch.UpdatedAt = now; batch.UpdatedBy = UserName; batch.LastModifiedById = UserId;
        AddRevision(batch, CivilEngineeringAuditEventMap.ApproveCivilMigrationBatch, before, Snapshot(batch), batch.SignOffDeclaration, correlationId);
        AddAudit(batch, CivilEngineeringAuditEventMap.ApproveCivilMigrationBatch, before, Snapshot(batch), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([batch], token)).Single();
    }

    public async Task<IReadOnlyList<CivilEngineeringMigrationRevisionDto>> GetHistoryAsync(Guid projectId, Guid batchId, CancellationToken token = default)
    {
        await LoadAsync(projectId, batchId, false, token);
        return await db.ProjectCivilMigrationRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.MigrationBatchId == batchId && !value.IsDeleted)
            .OrderBy(value => value.CreatedAt)
            .Select(value => new CivilEngineeringMigrationRevisionDto { Action = value.Action, ActorName = value.ActorName, ActorRoles = value.ActorRoles, Reason = value.Reason, CorrelationId = value.CorrelationId, CreatedAt = value.CreatedAt })
            .ToListAsync(token);
    }

    private IQueryable<ProjectCivilMigrationBatch> Batches(bool tracked) =>
        (tracked ? db.ProjectCivilMigrationBatches : db.ProjectCivilMigrationBatches.AsNoTracking())
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<ProjectCivilMigrationBatch> LoadAsync(Guid projectId, Guid batchId, bool tracked, CancellationToken token)
    {
        await RequireProjectAsync(projectId, token);
        return await Batches(tracked).SingleOrDefaultAsync(value => value.ProjectId == projectId && value.Id == batchId, token)
            ?? throw new CivilEngineeringMigrationNotFoundException("The Civil migration batch was not found in the selected project.");
    }

    private async Task RequireProjectAsync(Guid projectId, CancellationToken token)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null ||
            !await db.Projects.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted, token))
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private async Task RequireConfiguredProjectActorAsync(Guid projectId, IReadOnlyCollection<Guid> allowedRoleIds, string action, CancellationToken token)
    {
        if (allowedRoleIds.Count == 0)
            throw Validation("CIV-CFG-013 has no configured role for this migration action.");
        var matchingRoleNames = await db.UserRoles.AsNoTracking()
            .Where(value => value.UserId == UserId && allowedRoleIds.Contains(value.RoleId))
            .Join(db.Roles.AsNoTracking(), value => value.RoleId, role => role.Id, (value, role) => role.Name)
            .Where(value => value != null).Select(value => value!).ToListAsync(token);
        if (matchingRoleNames.Count == 0 || !await db.ProjectMembers.AsNoTracking().AnyAsync(value =>
                value.TenantId == TenantId && value.ProjectId == projectId && value.UserId == UserId && value.IsActive && !value.IsDeleted && matchingRoleNames.Contains(value.Role), token))
            throw new UnauthorizedAccessException($"You do not hold an active configured project role to {action}.");
    }

    private async Task<CentralDocumentVersion?> CurrentDocumentAsync(Guid recordId, Guid versionId, string? templateCode, CancellationToken token)
    {
        if (recordId == Guid.Empty || versionId == Guid.Empty) return null;
        var query = db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId && value.Id == versionId && value.DocumentRecordId == recordId);
        if (!string.IsNullOrWhiteSpace(templateCode)) query = query.Where(value => value.DocumentRecord.MetadataTemplateCode == templateCode);
        return await query.SingleOrDefaultAsync(token);
    }

    private async Task<IReadOnlyList<CivilEngineeringMigrationDocumentLookupDto>> DocumentLookupsAsync(string? templateCode, CancellationToken token)
    {
        var query = db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId && (value.DocumentRecord.SourceModule == "Projects" || value.DocumentRecord.SourceModule == "CivilEngineering"));
        if (!string.IsNullOrWhiteSpace(templateCode)) query = query.Where(value => value.DocumentRecord.MetadataTemplateCode == templateCode);
        return await query.OrderBy(value => value.DocumentRecord.DocumentReference).ThenByDescending(value => value.CreatedAt).Take(250)
            .Select(value => new CivilEngineeringMigrationDocumentLookupDto { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber })
            .ToListAsync(token);
    }

    private async Task<Policy> ResolvePolicyAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published
                && value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No effective published Civil Engineering configuration profile exists.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one Civil Engineering configuration profile is effective.");
        var profile = profiles[0];
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-013" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-013 migration decision.");
        return await BuildPolicyAsync(profile.Id, decision, token);
    }

    private async Task<Policy> ResolveFrozenPolicyAsync(ProjectCivilMigrationBatch batch, CancellationToken token)
    {
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == batch.ConfigurationDecisionId && value.ProfileId == batch.ConfigurationProfileId
            && value.ConfigurationKey == "CIV-CFG-013" && !value.IsDeleted, token)
            ?? throw Conflict("The frozen CIV-CFG-013 migration decision is unavailable.");
        var policy = await BuildPolicyAsync(batch.ConfigurationProfileId, decision, token);
        if (policy.Template.Id != batch.ReconciliationEvidenceTemplateId || policy.Template.TemplateCode != batch.ReconciliationEvidenceTemplateCodeSnapshot || !FixedEquals(policy.PolicyHash, batch.PolicyHash))
            throw Conflict("The Civil migration batch no longer matches its frozen CIV-CFG-013 policy snapshot.");
        return policy;
    }

    private async Task<Policy> BuildPolicyAsync(Guid profileId, CivilEngineeringConfigurationDecision decision, CancellationToken token)
    {
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Validation("CIV-CFG-013 is not approved and verified.");
        CivilEngineeringMigrationValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringMigrationValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-013 contains invalid migration-control data."); }
        EnsureMigrationPolicy(value);
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == value.ReconciliationEvidenceTemplateId && item.IsActive && item.PublishedAt.HasValue && !item.IsDeleted, token)
            ?? throw Validation("The CIV-CFG-013 reconciliation DMS template is unavailable.");
        return new Policy(profileId, decision.Id, value, template, Hash(decision.ValueJson));
    }

    private static void EnsureMigrationPolicy(CivilEngineeringMigrationValue value)
    {
        if (value.SourceTypes.Count == 0 || value.OwnerRoleIds.Count == 0 || value.ReviewerRoleIds.Count == 0 || value.SignOffRoleIds.Count == 0 || value.ReconciliationEvidenceTemplateId == Guid.Empty)
            throw Validation("CIV-CFG-013 must select source types, owner/reviewer/sign-off roles and a reconciliation DMS template.");
        if (!value.RequireStaging || !value.RequireReconciliation || !value.RequireSignedAcceptance)
            throw Validation("CIV-CFG-013 must require staging, reconciliation and signed acceptance before the Civil migration workbench is enabled.");
    }

    private async Task<IReadOnlyList<CivilEngineeringMigrationBatchDto>> MapAsync(IReadOnlyCollection<ProjectCivilMigrationBatch> batches, CancellationToken token)
    {
        if (batches.Count == 0) return [];
        var ids = batches.Select(value => value.Id).Distinct().ToList();
        var records = await db.ProjectCivilMigrationRecords.AsNoTracking().Where(value => value.TenantId == TenantId && ids.Contains(value.MigrationBatchId) && !value.IsDeleted).OrderBy(value => value.Sequence).ToListAsync(token);
        var issues = await db.ProjectCivilMigrationValidationIssues.AsNoTracking().Where(value => value.TenantId == TenantId && ids.Contains(value.MigrationBatchId) && !value.IsDeleted).OrderBy(value => value.Sequence).ThenBy(value => value.Code).ToListAsync(token);
        var documentIds = records.Where(value => value.CentralDocumentRecordId.HasValue).Select(value => value.CentralDocumentRecordId!.Value).Distinct().ToList();
        var documents = await db.CentralDocumentRecords.AsNoTracking().Where(value => value.TenantId == TenantId && documentIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.DocumentReference, token);
        return batches.Select(batch => new CivilEngineeringMigrationBatchDto
        {
            Id = batch.Id, ProjectId = batch.ProjectId, SourceType = batch.SourceType, SourceRegisterReference = batch.SourceRegisterReference,
            Status = batch.Status, RecordCount = batch.RecordCount, ErrorCount = batch.ErrorCount, CreatedAt = batch.CreatedAt,
            ReconciledAt = batch.ReconciledAt, SignedOffAt = batch.SignedOffAt,
            IsReadyForOwnerPosting = batch.Status == CivilEngineeringMigrationBatchStatus.ReadyForOwnerPosting,
            RowVersion = Convert.ToBase64String(batch.RowVersion),
            Records = records.Where(value => value.MigrationBatchId == batch.Id).Select(value => new CivilEngineeringMigrationRecordDto
            {
                Sequence = value.Sequence, RecordType = value.RecordType, SourceReference = value.SourceReference, Title = value.Title, RecordDate = value.RecordDate,
                DocumentReference = value.CentralDocumentRecordId.HasValue ? documents.GetValueOrDefault(value.CentralDocumentRecordId.Value) : null,
                PhysicalFileReference = value.PhysicalFileReference, ValidationStatus = value.ValidationStatus, ValidationMessage = value.ValidationMessage
            }).ToList(),
            Issues = issues.Where(value => value.MigrationBatchId == batch.Id).Select(value => new CivilEngineeringMigrationIssueDto { Sequence = value.Sequence, Code = value.Code, Severity = value.Severity, Message = value.Message }).ToList()
        }).ToList();
    }

    private static void ApplyRowVersion(ProjectCivilMigrationBatch batch, string rowVersion)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { throw Validation("The migration batch row version is invalid. Refresh and retry."); }
        if (batch.RowVersion.Length == 0 || !CryptographicOperations.FixedTimeEquals(batch.RowVersion, expected))
            throw Conflict("The Civil migration batch changed concurrently. Refresh and retry.");
    }

    private void AddRevision(ProjectCivilMigrationBatch batch, string action, object? before, object after, string? reason, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        batch.Revisions.Add(new ProjectCivilMigrationRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, MigrationBatchId = batch.Id, Action = action, ActorUserId = UserId,
            ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = Clean(reason, 2000),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions),
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
    }

    private void AddAudit(ProjectCivilMigrationBatch batch, string action, object? before, object after, string correlationId)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectCivilMigrationBatch), ResourceId = batch.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
    }

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The Civil migration batch changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52280 and <= 52291) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting Civil migration batch was detected. Refresh and retry."); }
    }

    private static object Snapshot(ProjectCivilMigrationBatch value) => new
    {
        value.Id, value.ProjectId, value.SourceType, value.SourceRegisterReference, value.Status, value.RecordCount, value.ErrorCount,
        value.ConfigurationProfileId, value.ConfigurationDecisionId, value.ReconciliationEvidenceTemplateId, value.ReconciliationDocumentRecordId,
        value.ReconciliationDocumentVersionId, value.SubmittedByUserId, value.ReconciledByUserId, value.ReconciledAt, value.SignedOffByUserId, value.SignedOffAt
    };
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : throw Validation("Text cannot exceed the permitted length.");
    private static string CleanRequired(string? value, int max, string name) => Clean(value, max) is { } clean && clean.Length >= 3 ? clean : throw Validation($"{name} must contain at least three characters.");
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left ?? string.Empty), Encoding.UTF8.GetBytes(right ?? string.Empty));
    private static CivilEngineeringMigrationValidationException Validation(string message) => new(message);
    private static CivilEngineeringMigrationConflictException Conflict(string message) => new(message);
    private sealed record StagedInput(
        CivilEngineeringMigrationRecordType RecordType,
        string SourceReference,
        string Title,
        DateTime? RecordDate,
        Guid? CentralDocumentRecordId,
        Guid? CentralDocumentVersionId,
        string? PhysicalFileReference);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, CivilEngineeringMigrationValue Value, CentralDocumentMetadataTemplate Template, string PolicyHash);
}
