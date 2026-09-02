using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// The Civil review overlay for a QS payment certificate. It deliberately has no Finance
/// side effects and no second workflow: the frozen QS certificate workflow continues only
/// after the assigned Project Engineer has endorsed the engineering review.
/// </summary>
public sealed class CivilEngineeringIpcEndorsementService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService) : ICivilEngineeringIpcEndorsementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringIpcEndorsementLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var assignment = await CurrentAssignmentAsync(projectId, token);
        if (assignment is null)
            return new CivilEngineeringIpcEndorsementLookupsDto();

        var policy = await ResolveEffectivePolicyAsync(DateTime.UtcNow, token);
        var certificates = await db.ProjectPaymentCertificates.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted
                && value.QuantitySurveyValuationWorksheetId != null
                && value.Status == ProjectPaymentCertificateStatuses.Draft && value.ApprovalStatus == "Draft")
            .OrderByDescending(value => value.IssueDate).ThenBy(value => value.CertificateNumber)
            .Select(value => new CivilEngineeringIpcEndorsementCertificateLookupDto
            {
                Id = value.Id,
                Label = (value.CertificateNumber ?? "Unnumbered certificate") + " - " + value.Title,
                Status = value.Status,
                ApprovalStatus = value.ApprovalStatus,
                RowVersion = Convert.ToBase64String(value.RowVersion)
            }).ToListAsync(token);
        var certificateIds = certificates.Select(value => value.Id).ToList();
        return new CivilEngineeringIpcEndorsementLookupsDto
        {
            PaymentCertificates = certificates,
            Documents = await DocumentLookupsAsync(projectId, certificateIds, policy.DocumentTemplate.TemplateCode, token),
            RequiresEndorsementEvidence = policy.Value.RequireDmsEvidence
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringIpcEndorsementDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var items = await Reviews(false).Where(value => value.ProjectId == projectId)
            .OrderByDescending(value => value.SubmittedAt).ThenByDescending(value => value.Sequence).ToListAsync(token);
        return await MapAsync(items, token);
    }

    public async Task<CivilEngineeringIpcEndorsementDto> SubmitAsync(Guid certificateId,
        SubmitCivilEngineeringIpcEndorsementRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringIpcEndorsementPolicy.ValidateSubmission(request);
        if (errors.Count > 0) throw Validation(errors);
        var strategy = db.Database.CreateExecutionStrategy();
        Guid reviewId = Guid.Empty;
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var existing = await Reviews(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
            if (existing is not null)
            {
                var existingHash = SubmissionHash(certificateId, existing.ProjectEngineerAssignmentId, request.Notes);
                if (!FixedEquals(existing.RequestHash, existingHash)) throw Conflict("This IPC handoff request identifier was already used with different values.");
                reviewId = existing.Id;
                await transaction.CommitAsync(token);
                return;
            }

            var certificate = await CertificateAsync(certificateId, true, token);
            await RequireProjectAsync(certificate.ProjectId, token);
            var policy = await ResolveEffectivePolicyAsync(DateTime.UtcNow, token);
            await RequireCoordinatorAsync(certificate.ProjectId, policy, token);
            if (certificate.Status != ProjectPaymentCertificateStatuses.Draft || certificate.ApprovalStatus != "Draft")
                throw Conflict("Only a completed Draft QS payment certificate can be submitted for Project Engineer review.");
            if (!certificate.QuantitySurveyValuationWorksheetId.HasValue)
                throw Conflict("Only a QS-governed payment certificate can enter the Civil Engineering IPC review control.");
            if (certificate.ApprovalWorkflowDefinitionId != policy.WorkflowDefinitionId)
                throw Conflict("The certificate's frozen QS workflow does not match effective CIV-CFG-005 interim-certificate routing.");
            var assignment = await CurrentAssignmentAsync(certificate.ProjectId, token)
                ?? throw Conflict("Assign an active Project Engineer before submitting this IPC for engineering review.");
            await RequireActiveConfiguredEngineerAsync(assignment, policy, token);
            if (policy.Value.RequireIndependentEndorsement && assignment.AssignedUserId == UserId)
                throw Conflict("The Projects Coordinator submitting an IPC cannot also be its independent Project Engineer reviewer.");

            var latest = await Reviews(true).Where(value => value.ProjectPaymentCertificateId == certificate.Id)
                .OrderByDescending(value => value.Sequence).FirstOrDefaultAsync(token);
            if (latest?.Status == CivilEngineeringIpcEndorsementStatuses.AwaitingProjectEngineerReview)
                throw Conflict("This IPC is already awaiting the assigned Project Engineer's review.");
            if (latest?.Status == CivilEngineeringIpcEndorsementStatuses.Endorsed)
                throw Conflict("This IPC has already been endorsed and is ready for its governed QS workflow submission.");

            var nextSequence = (latest?.Sequence ?? 0) + 1;
            var notes = RequiredText(request.Notes, "IPC handoff notes");
            var now = DateTime.UtcNow;
            var review = new ProjectCivilIpcEndorsement
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectPaymentCertificateId = certificate.Id, ProjectId = certificate.ProjectId,
                Sequence = nextSequence, ProjectEngineerAssignmentId = assignment.Id, SubmittedById = UserId, SubmittedAt = now,
                SubmissionNotes = notes, ConfigurationProfileId = policy.ProfileId, ConfigurationDecisionId = policy.DecisionId,
                WorkflowDefinitionId = policy.WorkflowDefinitionId, EvidenceMetadataTemplateId = policy.DocumentTemplate.Id,
                EvidenceMetadataTemplateCodeSnapshot = policy.DocumentTemplate.TemplateCode, RequiresDmsEvidence = policy.Value.RequireDmsEvidence, PolicyHash = policy.PolicyHash,
                ClientRequestId = request.ClientRequestId, RequestHash = SubmissionHash(certificate.Id, assignment.Id, notes),
                CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            db.ProjectCivilIpcEndorsements.Add(review);
            AddRevision(review, CivilEngineeringAuditEventMap.SubmitIpcEngineeringCheck, null, Snapshot(review), notes, correlationId);
            AddAudit(review, CivilEngineeringAuditEventMap.SubmitIpcEngineeringCheck, null, Snapshot(review), correlationId);
            await SaveAsync(token);
            reviewId = review.Id;
            await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
        return await RequiredDtoAsync(reviewId, token);
    }

    public async Task<CivilEngineeringIpcEndorsementDto> ReviewAsync(Guid endorsementId,
        ReviewCivilEngineeringIpcEndorsementRequest request, string correlationId, CancellationToken token = default)
    {
        Guid reviewId = endorsementId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var review = await Reviews(true).SingleOrDefaultAsync(value => value.Id == endorsementId, token)
                ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil Engineering IPC review was not found.");
            await RequireProjectAsync(review.ProjectId, token);
            var policy = await ResolveFrozenPolicyAsync(review, token);
            var errors = CivilEngineeringIpcEndorsementPolicy.ValidateReview(request, policy.Value.RequireDmsEvidence);
            if (errors.Count > 0) throw Validation(errors);
            var notes = RequiredText(request.Notes, "Project Engineer decision note");
            var mutationHash = ReviewHash(review.Id, request.Endorse, notes, request.EvidenceDocumentRecordId, request.EvidenceDocumentVersionId);
            if (review.LastMutationClientRequestId == request.ClientRequestId)
            {
                if (!FixedEquals(review.LastMutationRequestHash, mutationHash)) throw Conflict("This IPC review request identifier was already used with different values.");
                await transaction.CommitAsync(token);
                return;
            }
            ApplyRowVersion(review, request.RowVersion);
            if (review.Status != CivilEngineeringIpcEndorsementStatuses.AwaitingProjectEngineerReview)
                throw Conflict("Only an IPC awaiting Project Engineer review can be endorsed or returned.");
            var assignment = await CurrentAssignmentAsync(review.ProjectId, token);
            if (assignment is null || assignment.Id != review.ProjectEngineerAssignmentId || assignment.AssignedUserId != UserId)
                throw new UnauthorizedAccessException("Only the currently assigned Project Engineer can decide this IPC review.");
            await RequireActiveConfiguredEngineerAsync(assignment, policy, token);
            if (policy.Value.RequireIndependentEndorsement && review.SubmittedById == UserId)
                throw Conflict("The Projects Coordinator who submitted the IPC cannot endorse it.");
            var evidence = request.Endorse
                ? await RequireEvidenceAsync(request.EvidenceDocumentRecordId, request.EvidenceDocumentVersionId, review, token)
                : null;
            var before = Snapshot(review);
            review.Status = request.Endorse ? CivilEngineeringIpcEndorsementStatuses.Endorsed : CivilEngineeringIpcEndorsementStatuses.ReturnedToProjectsCoordinator;
            review.ReviewedById = UserId;
            review.ReviewedAt = DateTime.UtcNow;
            review.ReviewNotes = notes;
            review.EvidenceDocumentRecordId = evidence?.DocumentRecordId;
            review.EvidenceDocumentVersionId = evidence?.Id;
            review.LastMutationClientRequestId = request.ClientRequestId;
            review.LastMutationRequestHash = mutationHash;
            review.CorrelationId = Correlation(correlationId);
            review.UpdatedAt = DateTime.UtcNow;
            review.UpdatedBy = UserName;
            review.LastModifiedById = UserId;
            var action = request.Endorse ? CivilEngineeringAuditEventMap.ApproveIpcEndorsement : CivilEngineeringAuditEventMap.RejectIpcEndorsement;
            AddRevision(review, action, before, Snapshot(review), notes, correlationId);
            AddAudit(review, action, before, Snapshot(review), correlationId);
            await SaveAsync(token);
            await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
        return await RequiredDtoAsync(reviewId, token);
    }

    public async Task EnsureCertificateCanBeAmendedAsync(Guid certificateId, CancellationToken token = default)
    {
        var certificate = await CertificateAsync(certificateId, false, token);
        var latest = await LatestAsync(certificate.Id, token);
        if (CivilEngineeringIpcEndorsementPolicy.IsAmendmentBlocked(latest))
            throw Conflict(latest!.Status == CivilEngineeringIpcEndorsementStatuses.Endorsed
                ? "The IPC was endorsed by the Project Engineer and cannot be amended. Return it for correction before changing certificate values."
                : "The IPC is with the Project Engineer for review and cannot be amended.");
    }

    public async Task EnsureCertificateCanProceedAsync(Guid certificateId, CancellationToken token = default)
    {
        var certificate = await CertificateAsync(certificateId, false, token);
        var latest = await LatestAsync(certificate.Id, token);
        var hasCurrentEngineer = await CurrentAssignmentAsync(certificate.ProjectId, token) is not null;
        try { CivilEngineeringIpcEndorsementPolicy.RequireCanAdvance(latest, hasCurrentEngineer); }
        catch (InvalidOperationException exception) { throw Conflict(exception.Message); }
    }

    private IQueryable<ProjectCivilIpcEndorsement> Reviews(bool tracked) =>
        (tracked ? db.ProjectCivilIpcEndorsements : db.ProjectCivilIpcEndorsements.AsNoTracking())
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<ProjectPaymentCertificate> CertificateAsync(Guid id, bool tracked, CancellationToken token) =>
        await (tracked ? db.ProjectPaymentCertificates : db.ProjectPaymentCertificates.AsNoTracking())
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The QS payment certificate was not found in the active tenant.");

    private async Task<ProjectCivilIpcEndorsement?> LatestAsync(Guid certificateId, CancellationToken token) =>
        await Reviews(false).Where(value => value.ProjectPaymentCertificateId == certificateId)
            .OrderByDescending(value => value.Sequence).FirstOrDefaultAsync(token);

    private async Task<CivilEngineeringIpcEndorsementDto> RequiredDtoAsync(Guid id, CancellationToken token)
    {
        var review = await Reviews(false).SingleOrDefaultAsync(value => value.Id == id, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil Engineering IPC review was not found.");
        return (await MapAsync([review], token)).Single();
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken token)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
        return await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted, token);
    }

    private async Task<ProjectCivilProjectEngineerAssignment?> CurrentAssignmentAsync(Guid projectId, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var candidates = await db.ProjectCivilProjectEngineerAssignments.AsNoTracking().Where(value => value.TenantId == TenantId
                && value.ProjectId == projectId && value.IsActive && !value.IsDeleted && value.EffectiveFrom <= now
                && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .OrderByDescending(value => value.EffectiveFrom).Take(2).ToListAsync(token);
        if (candidates.Count > 1)
            throw Conflict("More than one Project Engineer appointment is active for this project. Resolve the assignment overlap before processing the IPC.");
        return candidates.SingleOrDefault();
    }

    private async Task RequireCoordinatorAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var member = await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId
            && value.UserId == UserId && value.IsActive && !value.IsDeleted, token);
        var configured = await db.UserRoles.AsNoTracking().AnyAsync(value => value.UserId == UserId && policy.Value.CoordinatorRoleIds.Contains(value.RoleId), token);
        if (!member || !configured)
            throw new UnauthorizedAccessException("Only an active project member with a configured Projects Coordinator role can submit an IPC for Project Engineer review.");
    }

    private async Task RequireActiveConfiguredEngineerAsync(ProjectCivilProjectEngineerAssignment assignment, Policy policy, CancellationToken token)
    {
        var membership = await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == assignment.ProjectId
            && value.Id == assignment.ProjectMemberId && value.UserId == assignment.AssignedUserId && value.IsActive && !value.IsDeleted
            && value.Role == CivilEngineeringAccessControlRegistry.ProjectEngineerRole, token);
        var userAndRole = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.Id == assignment.AssignedUserId && value.IsActive)
            .Join(db.UserRoles.AsNoTracking().Where(value => policy.Value.ProjectEngineerRoleIds.Contains(value.RoleId)), user => user.Id, role => role.UserId, (user, _) => user.Id)
            .AnyAsync(token);
        if (!membership || !userAndRole)
            throw Conflict("The frozen Project Engineer appointment is no longer an active project member with an effective CIV-CFG-005 Project Engineer role.");
    }

    private async Task<CentralDocumentVersion?> RequireEvidenceAsync(Guid? recordId, Guid? versionId,
        ProjectCivilIpcEndorsement review, CancellationToken token)
    {
        if (!recordId.HasValue && !versionId.HasValue) return null;
        if (!recordId.HasValue || !versionId.HasValue) throw Validation("Select both the DMS document and its current Published version.");
        var version = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished()).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId, token)
            ?? throw Validation("Select a current Published central-DMS evidence document.");
        if (version.DocumentRecordId != recordId || !string.Equals(version.DocumentRecord.MetadataTemplateCode, review.EvidenceMetadataTemplateCodeSnapshot, StringComparison.OrdinalIgnoreCase))
            throw Validation("The selected evidence does not match the frozen Civil Engineering DMS document policy.");
        var source = version.DocumentRecord.SourceRecordId;
        if (!source.HasValue || (source.Value != review.ProjectId && source.Value != review.ProjectPaymentCertificateId))
            throw Validation("Select DMS evidence linked to this project or payment certificate.");
        return version;
    }

    private async Task<Policy> ResolveEffectivePolicyAsync(DateTime at, CancellationToken token)
    {
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted
                && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && value.EffectiveFrom <= at
                && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No effective published Civil Engineering configuration profile exists.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one Civil Engineering configuration profile is effective. Resolve the overlap before reviewing IPCs.");
        var profile = profiles[0];
        var decisions = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProfileId == profile.Id
            && (value.ConfigurationKey == "CIV-CFG-005" || value.ConfigurationKey == "CIV-CFG-004") && !value.IsDeleted).ToListAsync(token);
        var supervision = decisions.SingleOrDefault(value => value.ConfigurationKey == "CIV-CFG-005")
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-005 supervision decision.");
        var document = decisions.SingleOrDefault(value => value.ConfigurationKey == "CIV-CFG-004")
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-004 document policy.");
        return await ResolvePolicyAsync(profile.Id, supervision, document, at, token);
    }

    private async Task<Policy> ResolveFrozenPolicyAsync(ProjectCivilIpcEndorsement review, CancellationToken token)
    {
        var supervision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId
            && value.Id == review.ConfigurationDecisionId && value.ProfileId == review.ConfigurationProfileId && value.ConfigurationKey == "CIV-CFG-005" && !value.IsDeleted, token)
            ?? throw Conflict("The IPC's frozen CIV-CFG-005 supervision policy is unavailable.");
        var document = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId
            && value.ProfileId == review.ConfigurationProfileId && value.ConfigurationKey == "CIV-CFG-004" && !value.IsDeleted, token)
            ?? throw Conflict("The IPC's frozen CIV-CFG-004 document policy is unavailable.");
        var policy = await ResolvePolicyAsync(review.ConfigurationProfileId, supervision, document, null, token);
        if (policy.WorkflowDefinitionId != review.WorkflowDefinitionId || policy.DocumentTemplate.Id != review.EvidenceMetadataTemplateId
            || !string.Equals(policy.DocumentTemplate.TemplateCode, review.EvidenceMetadataTemplateCodeSnapshot, StringComparison.OrdinalIgnoreCase)
            || !FixedEquals(policy.PolicyHash, review.PolicyHash))
            throw Conflict("The IPC review's frozen Civil Engineering policy lineage no longer matches the configured controls.");
        return policy;
    }

    private async Task<Policy> ResolvePolicyAsync(Guid profileId, CivilEngineeringConfigurationDecision supervision,
        CivilEngineeringConfigurationDecision document, DateTime? effectiveAt, CancellationToken token)
    {
        if (!IsApprovedEffective(supervision, effectiveAt) || !IsApprovedEffective(document, effectiveAt))
            throw Validation("CIV-CFG-005 and CIV-CFG-004 must be approved, verified, and effective before processing IPC reviews.");
        CivilEngineeringSupervisionWorkflowValue value;
        CivilEngineeringDocumentPolicyValue documentValue;
        try
        {
            value = JsonSerializer.Deserialize<CivilEngineeringSupervisionWorkflowValue>(supervision.ValueJson, JsonOptions) ?? throw new JsonException();
            documentValue = JsonSerializer.Deserialize<CivilEngineeringDocumentPolicyValue>(document.ValueJson, JsonOptions) ?? throw new JsonException();
        }
        catch (JsonException) { throw Validation("The effective Civil Engineering IPC or document policy cannot be read."); }
        if (value.InterimCertificateWorkflowDefinitionId == Guid.Empty || value.ProjectEngineerRoleIds.Count == 0 || value.CoordinatorRoleIds.Count == 0 || documentValue.MetadataTemplateId == Guid.Empty)
            throw Validation("CIV-CFG-005 must select the shared QS certificate workflow, Project Engineer and Projects Coordinator roles; CIV-CFG-004 must select a DMS template.");
        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(definition => definition.EntityType).SingleOrDefaultAsync(definition => definition.TenantId == TenantId
            && definition.Id == value.InterimCertificateWorkflowDefinitionId && definition.IsActive && !definition.IsDeleted
            && definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !definition.EntityType.IsDeleted && definition.EntityType.IsActive
            && definition.EntityType.Code == QuantitySurveyWorkflowBindingRegistry.PaymentCertificate, token);
        if (workflowDefinition is null || !await db.WorkflowSteps.AsNoTracking().AnyAsync(step => step.TenantId == TenantId && step.WorkflowDefinitionId == value.InterimCertificateWorkflowDefinitionId && !step.IsDeleted, token))
            throw Validation("CIV-CFG-005 must use an active Published shared QS payment-certificate workflow with approval steps.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId
            && value.Id == documentValue.MetadataTemplateId && value.IsActive && value.PublishedAt.HasValue && !value.IsDeleted, token)
            ?? throw Validation("The CIV-CFG-004 DMS document template is unavailable.");
        return new Policy(profileId, supervision.Id, value.InterimCertificateWorkflowDefinitionId, value, template,
            Hash(supervision.ValueJson + "|" + document.ValueJson));
    }

    private async Task<IReadOnlyList<CivilEngineeringIpcEndorsementDocumentLookupDto>> DocumentLookupsAsync(Guid projectId,
        IReadOnlyCollection<Guid> certificateIds, string templateCode, CancellationToken token) =>
        await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId && value.DocumentRecord.MetadataTemplateCode == templateCode
                && (value.DocumentRecord.SourceRecordId == projectId || certificateIds.Contains(value.DocumentRecord.SourceRecordId ?? Guid.Empty)))
            .OrderBy(value => value.DocumentRecord.DocumentReference).ThenByDescending(value => value.CreatedAt)
            .Select(value => new CivilEngineeringIpcEndorsementDocumentLookupDto
            {
                CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id,
                DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber
            }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringIpcEndorsementDto>> MapAsync(IReadOnlyCollection<ProjectCivilIpcEndorsement> reviews, CancellationToken token)
    {
        if (reviews.Count == 0) return [];
        var certificateIds = reviews.Select(value => value.ProjectPaymentCertificateId).Distinct().ToList();
        var certificates = await db.ProjectPaymentCertificates.AsNoTracking().Where(value => value.TenantId == TenantId && certificateIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => new { value.CertificateNumber, value.Title }, token);
        var assignments = await db.ProjectCivilProjectEngineerAssignments.AsNoTracking().Where(value => value.TenantId == TenantId && reviews.Select(item => item.ProjectEngineerAssignmentId).Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, token);
        var userIds = reviews.Select(value => value.SubmittedById).Concat(reviews.Where(value => value.ReviewedById.HasValue).Select(value => value.ReviewedById!.Value))
            .Concat(assignments.Values.Select(value => value.AssignedUserId)).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => DisplayName(value.FirstName, value.LastName, value.UserName), token);
        return reviews.Select(value =>
        {
            assignments.TryGetValue(value.ProjectEngineerAssignmentId, out var assignment);
            certificates.TryGetValue(value.ProjectPaymentCertificateId, out var certificate);
            return new CivilEngineeringIpcEndorsementDto
            {
                Id = value.Id, ProjectPaymentCertificateId = value.ProjectPaymentCertificateId,
                PaymentCertificateNumber = certificate?.CertificateNumber ?? "Unnumbered certificate", PaymentCertificateTitle = certificate?.Title ?? "Unavailable certificate",
                Sequence = value.Sequence, ProjectEngineerAssignmentId = value.ProjectEngineerAssignmentId,
                ProjectEngineerUserId = assignment?.AssignedUserId ?? Guid.Empty,
                ProjectEngineerName = assignment is null ? "Unavailable Project Engineer" : users.GetValueOrDefault(assignment.AssignedUserId, assignment.AssignedUserId.ToString()),
                SubmittedById = value.SubmittedById, SubmittedByName = users.GetValueOrDefault(value.SubmittedById, value.SubmittedById.ToString()),
                SubmittedAt = value.SubmittedAt, SubmissionNotes = value.SubmissionNotes, Status = value.Status,
                ReviewedById = value.ReviewedById, ReviewedByName = value.ReviewedById.HasValue ? users.GetValueOrDefault(value.ReviewedById.Value, value.ReviewedById.Value.ToString()) : null,
                ReviewedAt = value.ReviewedAt, ReviewNotes = value.ReviewNotes, EvidenceDocumentRecordId = value.EvidenceDocumentRecordId,
                EvidenceDocumentVersionId = value.EvidenceDocumentVersionId, RequiresEndorsementEvidence = value.RequiresDmsEvidence,
                RowVersion = Convert.ToBase64String(value.RowVersion)
            };
        }).ToList();
    }

    private void AddRevision(ProjectCivilIpcEndorsement review, string action, object? before, object after, string? reason, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.ProjectCivilIpcEndorsementRevisions.Add(new ProjectCivilIpcEndorsementRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, IpcEndorsementId = review.Id, Action = action, ActorUserId = UserId,
            ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = Clean(reason),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions),
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
    }

    private void AddAudit(ProjectCivilIpcEndorsement review, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectPaymentCertificate),
        ResourceId = review.ProjectPaymentCertificateId.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), review = after }, JsonOptions),
        IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
    });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The IPC review changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52150 and <= 52157) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting IPC review was detected. Refresh and retry."); }
    }

    private static bool IsApprovedEffective(CivilEngineeringConfigurationDecision value, DateTime? effectiveAt) => value.Status == CivilEngineeringConfigurationDecisionStatus.Approved
        && value.ApprovalStatus == CivilEngineeringConfigurationApprovalStatus.Approved && value.EvidenceStatus == CivilEngineeringConfigurationEvidenceStatus.Verified
        && (!effectiveAt.HasValue || ((!value.EffectiveFrom.HasValue || value.EffectiveFrom <= effectiveAt.Value)
            && (!value.EffectiveTo.HasValue || value.EffectiveTo >= effectiveAt.Value)));
    private static void ApplyRowVersion(ProjectCivilIpcEndorsement review, string encoded)
    {
        try
        {
            var expected = Convert.FromBase64String(encoded);
            if (!CryptographicOperations.FixedTimeEquals(expected, review.RowVersion)) throw Conflict("The IPC review changed concurrently. Refresh and retry.");
        }
        catch (FormatException) { throw Validation("The IPC review concurrency token is invalid. Refresh and retry."); }
    }
    private static object Snapshot(ProjectCivilIpcEndorsement value) => new
    {
        value.Id, value.ProjectPaymentCertificateId, value.ProjectId, value.Sequence, value.ProjectEngineerAssignmentId,
        value.SubmittedById, value.SubmittedAt, value.SubmissionNotes, value.Status, value.ReviewedById, value.ReviewedAt,
        value.ReviewNotes, value.EvidenceDocumentRecordId, value.EvidenceDocumentVersionId, value.ConfigurationProfileId,
        value.ConfigurationDecisionId, value.WorkflowDefinitionId, value.EvidenceMetadataTemplateId,
        value.EvidenceMetadataTemplateCodeSnapshot, value.RequiresDmsEvidence, value.PolicyHash
    };
    private static string SubmissionHash(Guid certificateId, Guid assignmentId, string notes) => Hash(new { Action = "SubmitIpcReview", certificateId, assignmentId, notes = notes.Trim() });
    private static string ReviewHash(Guid reviewId, bool endorse, string notes, Guid? recordId, Guid? versionId) => Hash(new { Action = endorse ? "Endorse" : "Return", reviewId, endorse, notes = notes.Trim(), recordId, versionId });
    private static string RequiredText(string? value, string label) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length is >= 5 and <= 2000 ? value.Trim() : throw Validation($"{label} must contain 5 to 2000 characters.");
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= 2000 ? value.Trim() : throw Validation("Text cannot exceed 2000 characters.");
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value is string text ? text : JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string? left, string? right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static string DisplayName(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static CivilEngineeringSupervisionValidationException Validation(string value) => new(value);
    private static CivilEngineeringSupervisionValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringSupervisionConflictException Conflict(string value) => new(value);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, CivilEngineeringSupervisionWorkflowValue Value, CentralDocumentMetadataTemplate DocumentTemplate, string PolicyHash);
}
