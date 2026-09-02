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
/// Projects extension for Civil engineering quality/laboratory test reports.
/// It stores governed linkage only and delegates document storage, workflow execution,
/// identity, project access, business partners and audit storage to their central owners.
/// </summary>
public sealed class CivilEngineeringQualityTestService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : ICivilEngineeringQualityTestService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringQualityTestLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        return new CivilEngineeringQualityTestLookupsDto
        {
            TestCategories = policy.Value.TestCategories.Distinct().OrderBy(value => value).Select(value => new CivilEngineeringQualityTestCategoryLookupDto { Value = value, Label = value.ToString() }).ToList(),
            SourceTypes = CivilEngineeringQualityTestSourceTypes.All,
            ResultStatuses = CivilEngineeringQualityTestResultStatuses.All,
            Reviewers = await ReviewerLookupsAsync(projectId, policy, token),
            SourcePartners = await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive && !value.IsBlacklisted)
                .OrderBy(value => value.PartnerName).Select(value => new CivilEngineeringQualityTestLookupOptionDto { Id = value.Id, Label = value.PartnerName }).Take(250).ToListAsync(token),
            ProjectPackages = await db.ProjectPackages.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted)
                .OrderBy(value => value.Code).ThenBy(value => value.Name).Select(value => new CivilEngineeringQualityTestLookupOptionDto { Id = value.Id, Label = (value.Code ?? "Package") + " - " + value.Name }).ToListAsync(token),
            PaymentCertificates = await db.ProjectPaymentCertificates.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted && value.Status != ProjectPaymentCertificateStatuses.Cancelled)
                .OrderByDescending(value => value.IssueDate).Select(value => new CivilEngineeringQualityTestLookupOptionDto { Id = value.Id, Label = (value.CertificateNumber ?? "Unnumbered certificate") + " - " + value.Title }).Take(250).ToListAsync(token),
            Documents = await DocumentLookupsAsync(policy.Template.TemplateCode, token),
            RequiresEndorsementEvidence = policy.Value.RequireEndorsementEvidence
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringQualityTestReportDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var values = await Reports(false).Where(value => value.ProjectId == projectId).OrderByDescending(value => value.TestedAt).ThenByDescending(value => value.CreatedAt).ToListAsync(token);
        return await MapAsync(values, token);
    }

    public async Task<CivilEngineeringQualityTestReportDto> CreateAsync(Guid projectId, CreateCivilEngineeringQualityTestReportRequest request, string correlationId, CancellationToken token = default)
    {
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var errors = CivilEngineeringQualityTestPolicy.ValidateCreate(request, policy.Value, DateTime.UtcNow);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new
        {
            projectId, request.ReportReference, request.TestCategory, request.SourceType, request.SourceBusinessPartnerId,
            testedAt = request.TestedAt.ToUniversalTime(), request.ResultStatus, request.ResultSummary, request.ReviewerUserId,
            request.ProjectPhaseId, request.ProjectPackageId, request.ProjectPaymentCertificateId, request.CentralDocumentRecordId, request.CentralDocumentVersionId
        });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Reports(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different test-report values.");
            await transaction.CommitAsync(token);
            return await GetAsync(retry.Id, token);
        }

        policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        errors = CivilEngineeringQualityTestPolicy.ValidateCreate(request, policy.Value, DateTime.UtcNow);
        if (errors.Count > 0) throw Validation(errors);
        await RequireProjectAsync(projectId, token);
        var reviewer = await RequireReviewerAsync(projectId, request.ReviewerUserId, policy, token);
        if (policy.Value.RequireIndependentReview && reviewer.Id == UserId) throw Conflict("The test-report maker cannot be the independent reviewer.");
        await RequireProjectLinksAsync(projectId, request.ProjectPhaseId, request.ProjectPackageId, request.ProjectPaymentCertificateId, token);
        await RequireSourcePartnerAsync(request.SourceType, request.SourceBusinessPartnerId, token);
        var evidence = await RequireEvidenceAsync(request.CentralDocumentRecordId, request.CentralDocumentVersionId, policy.Template.TemplateCode, token);
        var now = DateTime.UtcNow;
        var report = new ProjectCivilQualityTestReport
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ProjectPhaseId = request.ProjectPhaseId, ProjectPackageId = request.ProjectPackageId,
            ProjectPaymentCertificateId = request.ProjectPaymentCertificateId, SourceBusinessPartnerId = request.SourceBusinessPartnerId,
            TestCategory = request.TestCategory, SourceType = request.SourceType.Trim(), ReportReference = RequiredText(request.ReportReference, 100, "Test report reference"),
            TestedAt = request.TestedAt.ToUniversalTime(), ResultStatus = request.ResultStatus.Trim(), ResultSummary = RequiredText(request.ResultSummary, 2000, "Test result summary"),
            ReviewerUserId = reviewer.Id, CentralDocumentRecordId = evidence.DocumentRecordId, CentralDocumentVersionId = evidence.Id,
            ConfigurationProfileId = policy.ProfileId, ConfigurationDecisionId = policy.DecisionId, WorkflowDefinitionId = policy.WorkflowDefinitionId,
            EvidenceMetadataTemplateId = policy.Template.Id, EvidenceMetadataTemplateCodeSnapshot = policy.Template.TemplateCode, PolicyHash = policy.PolicyHash,
            AcceptanceBlocked = policy.Value.BlockAcceptanceOnFailure && string.Equals(request.ResultStatus, CivilEngineeringQualityTestResultStatuses.Fail, StringComparison.Ordinal),
            ClientRequestId = request.ClientRequestId, RequestHash = requestHash, CorrelationId = Correlation(correlationId),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.ProjectCivilQualityTestReports.Add(report);
        var submitted = await workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.QualityTest, report.Id, policy.WorkflowDefinitionId);
        if (!submitted.ExecutionResult.Success || submitted.Outcome != WorkflowOutcome.Pending)
            throw Conflict(submitted.ExecutionResult.Message ?? "The configured quality-test workflow must start in a pending reviewer state.");
        workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.QualityTest).ApplySubmitOutcome(report, submitted.Outcome, UserId);
        report.WorkflowInstanceId = submitted.ExecutionResult.WorkflowInstanceId;
        AddRevision(report, CivilEngineeringAuditEventMap.CreateEngineeringTestReport, null, Snapshot(report), null, correlationId);
        AddAudit(report, CivilEngineeringAuditEventMap.CreateEngineeringTestReport, null, Snapshot(report), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAsync(report.Id, token);
    }

    public async Task<CivilEngineeringQualityTestReportDto> ProcessAsync(Guid reportId, ProcessCivilEngineeringQualityTestReportRequest request, string correlationId, CancellationToken token = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var report = await Reports(true).SingleOrDefaultAsync(value => value.Id == reportId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil Engineering test report was not found.");
        var policy = await ResolveFrozenPolicyAsync(report, token);
        var errors = CivilEngineeringQualityTestPolicy.ValidateDecision(request, policy.Value.RequireEndorsementEvidence);
        if (errors.Count > 0) throw Validation(errors);
        var mutationHash = Hash(new { reportId, request.Approve, reason = request.Reason.Trim(), request.EndorsementDocumentRecordId, request.EndorsementDocumentVersionId });
        if (report.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(report.LastMutationRequestHash ?? string.Empty, mutationHash)) throw Conflict("This decision request identifier was already used with different values.");
            await transaction.CommitAsync(token);
            return await GetAsync(report.Id, token);
        }
        ApplyRowVersion(report, request.RowVersion);
        if (report.Status != "PendingApproval" || report.ApprovalStatus != "Pending") throw Conflict("Only a pending test report can be reviewed.");
        if (report.ReviewerUserId != UserId) throw new UnauthorizedAccessException("Only the controlled test-report reviewer can endorse or reject this report.");
        if (policy.Value.RequireIndependentReview && report.CreatedById == UserId) throw Conflict("The test-report maker cannot provide the independent endorsement.");
        if (!report.WorkflowInstanceId.HasValue || !await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.QualityTest, report.Id, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the active quality-test workflow approval step.");
        var endorsement = request.EndorsementDocumentVersionId.HasValue
            ? await RequireEvidenceAsync(request.EndorsementDocumentRecordId!.Value, request.EndorsementDocumentVersionId.Value, policy.Template.TemplateCode, token)
            : null;
        var before = Snapshot(report);
        var result = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.QualityTest, report.Id, UserId, request.Approve ? "Approve" : "Reject", request.Reason.Trim());
        if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The quality-test workflow decision could not be processed.");
        if (request.Approve && result.Outcome != WorkflowOutcome.Approved) throw Conflict("The shared workflow did not approve the test report.");
        if (!request.Approve && result.Outcome != WorkflowOutcome.Rejected) throw Conflict("The shared workflow did not reject the test report.");
        workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.QualityTest).ApplyApprovalOutcome(report, result.Outcome, UserId, request.Reason.Trim());
        report.EndorsementDocumentRecordId = endorsement?.DocumentRecordId;
        report.EndorsementDocumentVersionId = endorsement?.Id;
        report.LastMutationClientRequestId = request.ClientRequestId; report.LastMutationRequestHash = mutationHash; report.CorrelationId = Correlation(correlationId);
        report.UpdatedAt = DateTime.UtcNow; report.UpdatedBy = UserName; report.LastModifiedById = UserId;
        var action = request.Approve ? CivilEngineeringAuditEventMap.ApproveEngineeringTestReport : CivilEngineeringAuditEventMap.RejectEngineeringTestReport;
        AddRevision(report, action, before, Snapshot(report), request.Reason, correlationId);
        AddAudit(report, action, before, Snapshot(report), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAsync(report.Id, token);
    }

    private IQueryable<ProjectCivilQualityTestReport> Reports(bool tracked) =>
        (tracked ? db.ProjectCivilQualityTestReports : db.ProjectCivilQualityTestReports.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<CivilEngineeringQualityTestReportDto> GetAsync(Guid id, CancellationToken token)
    {
        var value = await Reports(false).SingleOrDefaultAsync(item => item.Id == id, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil Engineering test report was not found.");
        return (await MapAsync([value], token)).Single();
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken token)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
        return await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted, token);
    }

    private async Task<Policy> ResolvePolicyAsync(DateTime at, CancellationToken token)
    {
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted
                && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && value.EffectiveFrom <= at
                && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at)).OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No effective published Civil Engineering configuration profile exists.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one Civil Engineering configuration profile is effective.");
        var profile = profiles[0];
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-011" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-011 quality-test decision.");
        return await ResolvePolicyAsync(profile.Id, decision, token);
    }

    private async Task<Policy> ResolveFrozenPolicyAsync(ProjectCivilQualityTestReport report, CancellationToken token)
    {
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == report.ConfigurationDecisionId && value.ProfileId == report.ConfigurationProfileId && value.ConfigurationKey == "CIV-CFG-011" && !value.IsDeleted, token)
            ?? throw Conflict("The test-report configuration lineage is no longer available.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Conflict("The frozen quality-test configuration is no longer approved and verified.");
        CivilEngineeringQualityTestValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringQualityTestValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("The frozen CIV-CFG-011 value is invalid."); }
        if (value.WorkflowDefinitionId != report.WorkflowDefinitionId || value.EvidenceMetadataTemplateId != report.EvidenceMetadataTemplateId || !FixedEquals(Hash(decision.ValueJson), report.PolicyHash))
            throw Conflict("The test-report configuration lineage does not match its frozen workflow policy.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == report.EvidenceMetadataTemplateId && !item.IsDeleted, token)
            ?? throw Conflict("The frozen DMS evidence template is unavailable.");
        if (!string.Equals(template.TemplateCode, report.EvidenceMetadataTemplateCodeSnapshot, StringComparison.OrdinalIgnoreCase)) throw Conflict("The frozen DMS evidence template no longer matches the test report.");
        return new Policy(report.ConfigurationProfileId, report.ConfigurationDecisionId, report.WorkflowDefinitionId, value, template, report.PolicyHash);
    }

    private async Task<Policy> ResolvePolicyAsync(Guid profileId, CivilEngineeringConfigurationDecision decision, CancellationToken token)
    {
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Validation("CIV-CFG-011 is not approved and verified.");
        CivilEngineeringQualityTestValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringQualityTestValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-011 contains invalid quality-test control data."); }
        if (value.WorkflowDefinitionId == Guid.Empty || value.EvidenceMetadataTemplateId == Guid.Empty || value.TestCategories.Count == 0 || value.ReviewerRoleIds.Count == 0)
            throw Validation("CIV-CFG-011 must select its workflow, DMS evidence template, test categories, and reviewer roles.");
        var definition = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.WorkflowDefinitionId && !item.IsDeleted && item.IsActive
            && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive && item.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.QualityTest, token);
        if (definition is null || !await db.WorkflowSteps.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.WorkflowDefinitionId == value.WorkflowDefinitionId && !item.IsDeleted, token))
            throw Validation($"The CIV-CFG-011 quality-test workflow must be active, Published, contain a reviewer step, and be bound to {CivilEngineeringWorkflowBindingRegistry.QualityTest}.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.EvidenceMetadataTemplateId && item.IsActive && item.PublishedAt.HasValue && !item.IsDeleted, token)
            ?? throw Validation("The CIV-CFG-011 DMS test-evidence template is unavailable.");
        return new Policy(profileId, decision.Id, value.WorkflowDefinitionId, value, template, Hash(decision.ValueJson));
    }

    private async Task<IReadOnlyList<CivilEngineeringQualityTestLookupOptionDto>> ReviewerLookupsAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var memberIds = await db.ProjectMembers.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsActive && !value.IsDeleted).Select(value => value.UserId).ToListAsync(token);
        var reviewerIds = await db.UserRoles.AsNoTracking().Where(value => memberIds.Contains(value.UserId) && policy.Value.ReviewerRoleIds.Contains(value.RoleId)).Select(value => value.UserId).Distinct().ToListAsync(token);
        return await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && reviewerIds.Contains(value.Id) && value.IsActive)
            .OrderBy(value => value.FirstName).ThenBy(value => value.LastName).Select(value => new CivilEngineeringQualityTestLookupOptionDto { Id = value.Id, Label = DisplayName(value.FirstName, value.LastName, value.UserName) }).ToListAsync(token);
    }

    private async Task<ApplicationUser> RequireReviewerAsync(Guid projectId, Guid reviewerId, Policy policy, CancellationToken token)
    {
        if (reviewerId == Guid.Empty) throw Validation("Select a controlled test-report reviewer.");
        var options = await ReviewerLookupsAsync(projectId, policy, token);
        if (!options.Any(value => value.Id == reviewerId)) throw Validation("The selected reviewer is not an active project member with an effective CIV-CFG-011 reviewer role.");
        return await db.Users.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == reviewerId && value.IsActive, token);
    }

    private async Task RequireSourcePartnerAsync(string sourceType, Guid? partnerId, CancellationToken token)
    {
        if (sourceType == CivilEngineeringQualityTestSourceTypes.Internal)
        {
            if (partnerId.HasValue) throw Validation("An internal test source must not select an external business partner.");
            return;
        }
        if (!partnerId.HasValue || !await db.BusinessPartners.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == partnerId && !value.IsDeleted && value.IsActive && !value.IsBlacklisted, token))
            throw Validation("Select an active, non-blacklisted controlled source business partner.");
    }

    private async Task RequireProjectLinksAsync(Guid projectId, Guid? phaseId, Guid? packageId, Guid? certificateId, CancellationToken token)
    {
        if (phaseId.HasValue && !await db.ProjectPhases.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == phaseId && value.ProjectId == projectId && !value.IsDeleted, token)) throw Validation("The selected project phase is not part of this project.");
        if (packageId.HasValue && !await db.ProjectPackages.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == packageId && value.ProjectId == projectId && !value.IsDeleted, token)) throw Validation("The selected work package is not part of this project.");
        if (certificateId.HasValue && !await db.ProjectPaymentCertificates.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == certificateId && value.ProjectId == projectId && !value.IsDeleted && value.Status != ProjectPaymentCertificateStatuses.Cancelled, token)) throw Validation("The selected active payment certificate is not part of this project.");
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, string templateCode, CancellationToken token)
    {
        if (recordId == Guid.Empty || versionId == Guid.Empty) throw Validation("Select both the central-DMS document and its current Published version.");
        var version = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId, token) ?? throw Validation("Select a current Published current-tenant central-DMS document version.");
        if (version.DocumentRecordId != recordId) throw Validation("The selected DMS version does not belong to the selected document.");
        if (!string.Equals(version.DocumentRecord.MetadataTemplateCode, templateCode, StringComparison.OrdinalIgnoreCase)) throw Validation($"The selected test evidence must use DMS template {templateCode}.");
        return version;
    }

    private async Task<IReadOnlyList<CivilEngineeringQualityTestDocumentLookupDto>> DocumentLookupsAsync(string templateCode, CancellationToken token) =>
        await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId && value.DocumentRecord.MetadataTemplateCode == templateCode).OrderBy(value => value.DocumentRecord.DocumentReference).ThenByDescending(value => value.CreatedAt)
            .Select(value => new CivilEngineeringQualityTestDocumentLookupDto { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringQualityTestReportDto>> MapAsync(IReadOnlyCollection<ProjectCivilQualityTestReport> values, CancellationToken token)
    {
        var userIds = values.Select(value => value.ReviewerUserId).Concat(values.Where(value => value.ApprovedById.HasValue).Select(value => value.ApprovedById!.Value)).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => DisplayName(value.FirstName, value.LastName, value.UserName), token);
        var partnerIds = values.Where(value => value.SourceBusinessPartnerId.HasValue).Select(value => value.SourceBusinessPartnerId!.Value).Distinct().ToList();
        var partners = await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId && partnerIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.PartnerName, token);
        var packageIds = values.Where(value => value.ProjectPackageId.HasValue).Select(value => value.ProjectPackageId!.Value).Distinct().ToList();
        var packages = await db.ProjectPackages.AsNoTracking().Where(value => value.TenantId == TenantId && packageIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => (value.Code ?? "Package") + " - " + value.Name, token);
        var certificateIds = values.Where(value => value.ProjectPaymentCertificateId.HasValue).Select(value => value.ProjectPaymentCertificateId!.Value).Distinct().ToList();
        var certificates = await db.ProjectPaymentCertificates.AsNoTracking().Where(value => value.TenantId == TenantId && certificateIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.CertificateNumber ?? value.Title, token);
        return values.Select(value => new CivilEngineeringQualityTestReportDto
        {
            Id = value.Id, ProjectId = value.ProjectId, ProjectPackageId = value.ProjectPackageId, ProjectPackageName = value.ProjectPackageId.HasValue ? packages.GetValueOrDefault(value.ProjectPackageId.Value) : null,
            ProjectPaymentCertificateId = value.ProjectPaymentCertificateId, PaymentCertificateNumber = value.ProjectPaymentCertificateId.HasValue ? certificates.GetValueOrDefault(value.ProjectPaymentCertificateId.Value) : null,
            TestCategory = value.TestCategory, TestCategoryLabel = value.TestCategory.ToString(), SourceType = value.SourceType, SourceBusinessPartnerId = value.SourceBusinessPartnerId, SourceBusinessPartnerName = value.SourceBusinessPartnerId.HasValue ? partners.GetValueOrDefault(value.SourceBusinessPartnerId.Value) : null,
            ReportReference = value.ReportReference, TestedAt = value.TestedAt, ResultStatus = value.ResultStatus, ResultSummary = value.ResultSummary,
            ReviewerUserId = value.ReviewerUserId, ReviewerName = users.GetValueOrDefault(value.ReviewerUserId, "Unavailable reviewer"), Status = value.Status, ApprovalStatus = value.ApprovalStatus,
            AcceptanceBlocked = value.AcceptanceBlocked, RejectionReason = value.RejectionReason, WorkflowInstanceId = value.WorkflowInstanceId,
            CentralDocumentRecordId = value.CentralDocumentRecordId, CentralDocumentVersionId = value.CentralDocumentVersionId, EndorsementDocumentRecordId = value.EndorsementDocumentRecordId,
            EndorsementDocumentVersionId = value.EndorsementDocumentVersionId, RowVersion = Convert.ToBase64String(value.RowVersion)
        }).ToList();
    }

    private void AddRevision(ProjectCivilQualityTestReport report, string action, object? before, object after, string? reason, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.ProjectCivilQualityTestRevisions.Add(new ProjectCivilQualityTestRevision { Id = Guid.NewGuid(), TenantId = TenantId, TestReportId = report.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = Clean(reason, 2000), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }

    private void AddAudit(ProjectCivilQualityTestReport report, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectCivilQualityTestReport), ResourceId = report.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The test report changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52121 and <= 52124) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting test report was detected. Refresh and retry."); }
    }

    private static void ApplyRowVersion(ProjectCivilQualityTestReport report, string encoded)
    {
        try
        {
            var expected = Convert.FromBase64String(encoded);
            if (!CryptographicOperations.FixedTimeEquals(expected, report.RowVersion)) throw Conflict("The test report changed concurrently. Refresh and retry.");
        }
        catch (FormatException) { throw Validation("The test-report concurrency token is invalid. Refresh and retry."); }
    }

    private static object Snapshot(ProjectCivilQualityTestReport value) => new { value.Id, value.ProjectId, value.ProjectPackageId, value.ProjectPaymentCertificateId, value.TestCategory, value.SourceType, value.SourceBusinessPartnerId, value.ReportReference, value.TestedAt, value.ResultStatus, value.ReviewerUserId, value.Status, value.ApprovalStatus, value.AcceptanceBlocked, value.WorkflowInstanceId, value.ConfigurationProfileId, value.ConfigurationDecisionId, value.WorkflowDefinitionId, value.EvidenceMetadataTemplateId, value.EvidenceMetadataTemplateCodeSnapshot, value.CentralDocumentRecordId, value.CentralDocumentVersionId, value.EndorsementDocumentRecordId, value.EndorsementDocumentVersionId, value.PolicyHash };
    private static string RequiredText(string? value, int max, string label) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) throw Validation($"{label} is required."); return normalized.Length <= max ? normalized : throw Validation($"{label} cannot exceed {max} characters."); }
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : throw Validation("Text cannot exceed the permitted length.");
    private static string DisplayName(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static CivilEngineeringSupervisionValidationException Validation(string value) => new(value);
    private static CivilEngineeringSupervisionValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringSupervisionConflictException Conflict(string value) => new(value);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, CivilEngineeringQualityTestValue Value, CentralDocumentMetadataTemplate Template, string PolicyHash);
}
