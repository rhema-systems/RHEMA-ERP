using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// A narrow Civil workflow envelope over Projects' EOT record. Quantity Survey remains
/// authoritative for variation valuation/budget application, Procurement owns the contract,
/// and central DMS owns the physical evidence.
/// </summary>
public sealed class CivilEngineeringExtensionOfTimeService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : ICivilEngineeringExtensionOfTimeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringExtensionOfTimeLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var policy = await ResolveEffectivePolicyAsync(token);
        return new CivilEngineeringExtensionOfTimeLookupsDto
        {
            Contracts = await ContractLookupsAsync(projectId, token),
            QuantitySurveyVariations = await VariationLookupsAsync(projectId, token),
            Documents = await DocumentLookupsAsync(policy.Template.TemplateCode, token),
            RequireQuantitySurveyVariationForCostImpact = policy.Value.RequireQuantitySurveyVariationForCostImpact,
            RequireFinanceBudgetRevalidation = policy.Value.RequireFinanceBudgetRevalidation,
            RequireProcurementContractRevalidation = policy.Value.RequireProcurementContractRevalidation
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringExtensionOfTimeControlDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var controls = await Controls(false).Where(value => value.ProjectId == projectId)
            .Include(value => value.ExtensionOfTime).Include(value => value.Contract)
            .Include(value => value.QuantitySurveyVariationOrder)
            .Include(value => value.EvidenceDocumentVersion).ThenInclude(value => value.DocumentRecord)
            .OrderByDescending(value => value.SubmittedAt).ToListAsync(token);
        return controls.Select(Map).ToList();
    }

    public async Task<CivilEngineeringExtensionOfTimeControlDto> CreateAsync(Guid projectId, CreateCivilEngineeringExtensionOfTimeRequest request, string correlationId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var policy = await ResolveEffectivePolicyAsync(token);
        var errors = CivilEngineeringExtensionOfTimePolicy.ValidateCreate(request, policy.Value);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new
        {
            projectId, request.ContractId, request.QuantitySurveyVariationOrderId, title = request.Title.Trim(), reason = request.Reason.Trim(),
            scope = request.ScopeSummary.Trim(), request.DaysRequested, proposedCompletion = request.ProposedRevisedCompletionDate?.ToUniversalTime(), request.HasCostImpact,
            request.EvidenceDocumentRecordId, request.EvidenceDocumentVersionId
        });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Controls(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This extension-of-time request identifier was already used with different values.");
            await transaction.CommitAsync(token);
            return await GetAsync(retry.Id, token);
        }

        var contract = await RequireProjectWorksContractAsync(projectId, request.ContractId, token);
        var variation = await RequireVariationAsync(projectId, contract.Id, request.QuantitySurveyVariationOrderId, request.HasCostImpact, policy.Value, token);
        var evidence = await RequireEvidenceAsync(request.EvidenceDocumentRecordId, request.EvidenceDocumentVersionId, policy.Template.TemplateCode, token);
        var now = DateTime.UtcNow;
        var extension = new ProjectExtensionOfTime
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ContractId = contract.Id,
            ReferenceNumber = await NextReferenceAsync(token), Title = RequiredText(request.Title, 200, "Extension-of-time title"),
            Reason = RequiredText(request.Reason, 4000, "Extension-of-time reason"), Status = ProjectExtensionOfTimeStatuses.Draft,
            RequestedDate = now, DaysRequested = request.DaysRequested, RevisedCompletionDate = Utc(request.ProposedRevisedCompletionDate!.Value),
            RequestedByName = UserName, Notes = $"Governed by Civil extension-of-time control; cost impact: {(request.HasCostImpact ? "Yes" : "No")}.",
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        var control = new ProjectCivilExtensionOfTimeControl
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ProjectExtensionOfTimeId = extension.Id, ContractId = contract.Id,
            QuantitySurveyVariationOrderId = variation?.Id, ScopeSummary = RequiredText(request.ScopeSummary, 500, "Affected scope summary"), HasCostImpact = request.HasCostImpact,
            SubmittedById = UserId, SubmittedAt = now, EvidenceDocumentRecordId = evidence.DocumentRecordId, EvidenceDocumentVersionId = evidence.Id,
            ConfigurationProfileId = policy.ProfileId, ConfigurationDecisionId = policy.DecisionId, WorkflowDefinitionId = policy.WorkflowDefinitionId,
            EvidenceMetadataTemplateId = policy.Template.Id, EvidenceMetadataTemplateCodeSnapshot = policy.Template.TemplateCode, PolicyHash = policy.PolicyHash,
            ClientRequestId = request.ClientRequestId, RequestHash = requestHash, CorrelationId = Correlation(correlationId),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.ProjectExtensionOfTimeRequests.Add(extension);
        db.ProjectCivilExtensionOfTimeControls.Add(control);
        var submitted = await workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.ExtensionOfTime, control.Id, policy.WorkflowDefinitionId);
        if (!submitted.ExecutionResult.Success || submitted.Outcome != WorkflowOutcome.Pending)
            throw Conflict(submitted.ExecutionResult.Message ?? "The configured extension-of-time workflow must start in a pending review state.");
        workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.ExtensionOfTime).ApplySubmitOutcome(control, submitted.Outcome, UserId);
        control.WorkflowInstanceId = submitted.ExecutionResult.WorkflowInstanceId;
        AddRevision(control, CivilEngineeringAuditEventMap.CreateCivilExtensionOfTime, "Draft", Snapshot(control, extension), request.Reason, requestHash, correlationId);
        AddAudit(control, CivilEngineeringAuditEventMap.CreateCivilExtensionOfTime, null, Snapshot(control, extension), correlationId);

        // The SQL guard permits the initial Projects EOT draft only while its governed envelope is
        // being inserted. Promote it to UnderReview after that envelope exists in the same transaction.
        await SaveAsync(token);
        extension.Status = ProjectExtensionOfTimeStatuses.UnderReview;
        extension.UpdatedAt = DateTime.UtcNow; extension.UpdatedBy = UserName; extension.LastModifiedById = UserId;
        AddRevision(control, CivilEngineeringAuditEventMap.SubmitCivilExtensionOfTime, "Draft", Snapshot(control, extension), "Submitted through the configured shared workflow.", requestHash, correlationId);
        AddAudit(control, CivilEngineeringAuditEventMap.SubmitCivilExtensionOfTime, null, Snapshot(control, extension), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        db.ChangeTracker.Clear();
        return await GetAsync(control.Id, token);
    }

    public async Task<CivilEngineeringExtensionOfTimeControlDto> ReviewAsync(Guid controlId, ReviewCivilEngineeringExtensionOfTimeRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringExtensionOfTimePolicy.ValidateReview(request);
        if (errors.Count > 0) throw Validation(errors);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var control = await Controls(true).Include(value => value.ExtensionOfTime).SingleOrDefaultAsync(value => value.Id == controlId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil extension-of-time control was not found.");
        await RequireProjectAsync(control.ProjectId, token);
        var policy = await ResolveFrozenPolicyAsync(control, token);
        await EnsureCurrentPolicyAsync(policy, token);
        var mutationHash = Hash(new { controlId, request.Approve, comment = request.Comment.Trim() });
        if (control.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(control.LastMutationRequestHash ?? string.Empty, mutationHash)) throw Conflict("This extension-of-time review identifier was already used with different values.");
            await transaction.CommitAsync(token);
            return await GetAsync(control.Id, token);
        }
        ApplyRowVersion(control, request.RowVersion);
        if (control.Status != "PendingApproval" || control.ApprovalStatus != "Pending") throw Conflict("Only a pending Civil extension-of-time control can be reviewed.");
        if (policy.Value.RequireIndependentApproval && control.SubmittedById == UserId) throw Conflict("The preparer cannot approve or reject the same Civil extension-of-time control.");
        if (!control.WorkflowInstanceId.HasValue || !await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.ExtensionOfTime, control.Id, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the active extension-of-time workflow review step.");
        await RequireProjectWorksContractAsync(control.ProjectId, control.ContractId, token);
        await RequireVariationAsync(control.ProjectId, control.ContractId, control.QuantitySurveyVariationOrderId, control.HasCostImpact, policy.Value, token);
        await RequireEvidenceAsync(control.EvidenceDocumentRecordId, control.EvidenceDocumentVersionId, policy.Template.TemplateCode, token);

        var beforeStatus = control.Status;
        var before = Snapshot(control, control.ExtensionOfTime);
        var result = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.ExtensionOfTime, control.Id, UserId, request.Approve ? "Approve" : "Reject", request.Comment.Trim());
        if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The extension-of-time workflow decision could not be processed.");
        if (request.Approve && result.Outcome is not (WorkflowOutcome.Pending or WorkflowOutcome.Approved)) throw Conflict("The shared workflow did not advance the extension-of-time control.");
        if (!request.Approve && result.Outcome != WorkflowOutcome.Rejected) throw Conflict("The shared workflow did not reject the extension-of-time control.");
        workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.ExtensionOfTime).ApplyApprovalOutcome(control, result.Outcome, UserId, request.Comment.Trim());
        control.LastMutationClientRequestId = request.ClientRequestId; control.LastMutationRequestHash = mutationHash; control.CorrelationId = Correlation(correlationId);
        control.UpdatedAt = DateTime.UtcNow; control.UpdatedBy = UserName; control.LastModifiedById = UserId;
        // Persist the workflow envelope first. The Projects EOT SQL guard then observes the
        // resulting control state when its authoritative status is synchronised below.
        await SaveAsync(token);
        ApplyProjectsEotOutcome(control.ExtensionOfTime, result.Outcome, request.Comment, UserName);
        control.ExtensionOfTime.UpdatedAt = DateTime.UtcNow; control.ExtensionOfTime.UpdatedBy = UserName; control.ExtensionOfTime.LastModifiedById = UserId;
        var action = request.Approve ? CivilEngineeringAuditEventMap.ApproveCivilExtensionOfTime : CivilEngineeringAuditEventMap.RejectCivilExtensionOfTime;
        AddRevision(control, action, beforeStatus, Snapshot(control, control.ExtensionOfTime), request.Comment, mutationHash, correlationId, before);
        AddAudit(control, action, before, Snapshot(control, control.ExtensionOfTime), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        db.ChangeTracker.Clear();
        return await GetAsync(control.Id, token);
    }

    public async Task<IReadOnlyList<CivilEngineeringExtensionOfTimeRevisionDto>> GetHistoryAsync(Guid controlId, CancellationToken token = default)
    {
        var control = await Controls(false).SingleOrDefaultAsync(value => value.Id == controlId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil extension-of-time control was not found.");
        await RequireProjectAsync(control.ProjectId, token);
        return await db.ProjectCivilExtensionOfTimeRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ExtensionOfTimeControlId == controlId && !value.IsDeleted)
            .OrderBy(value => value.CreatedAt).Select(value => new CivilEngineeringExtensionOfTimeRevisionDto
            {
                Id = value.Id, Action = value.Action, FromStatus = value.FromStatus, ToStatus = value.ToStatus, ActorName = value.ActorName,
                ActorRoles = value.ActorRoles, Reason = value.Reason, CorrelationId = value.CorrelationId, CreatedAt = value.CreatedAt
            }).ToListAsync(token);
    }

    private IQueryable<ProjectCivilExtensionOfTimeControl> Controls(bool tracked) =>
        (tracked ? db.ProjectCivilExtensionOfTimeControls : db.ProjectCivilExtensionOfTimeControls.AsNoTracking())
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<CivilEngineeringExtensionOfTimeControlDto> GetAsync(Guid id, CancellationToken token)
    {
        var control = await Controls(false).Where(value => value.Id == id)
            .Include(value => value.ExtensionOfTime).Include(value => value.Contract).Include(value => value.QuantitySurveyVariationOrder)
            .Include(value => value.EvidenceDocumentVersion).ThenInclude(value => value.DocumentRecord).SingleOrDefaultAsync(token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil extension-of-time control was not found.");
        return Map(control);
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken token)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
        return await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted, token);
    }

    private async Task<Contract> RequireProjectWorksContractAsync(Guid projectId, Guid contractId, CancellationToken token)
    {
        var contract = await (
            from package in db.ProjectPackages.AsNoTracking()
            join value in db.Contracts.AsNoTracking() on package.ContractId equals (Guid?)value.Id
            where package.TenantId == TenantId && package.ProjectId == projectId && package.ContractId == contractId && !package.IsDeleted
                  && value.TenantId == TenantId && !value.IsDeleted && value.Status == "Active" && value.ContractType == "Works"
            select value).SingleOrDefaultAsync(token);
        return contract ?? throw Validation("Select an active Works contract linked to this project package.");
    }

    private async Task<ProjectVariationOrder?> RequireVariationAsync(Guid projectId, Guid contractId, Guid? variationId, bool hasCostImpact, CivilEngineeringExtensionOfTimeValue policy, CancellationToken token)
    {
        if (!variationId.HasValue || variationId == Guid.Empty)
        {
            if (hasCostImpact && policy.RequireQuantitySurveyVariationForCostImpact)
                throw Validation("A QS-approved and applied variation is required for a cost-impact extension of time.");
            return null;
        }
        var variation = await db.ProjectVariationOrders.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == variationId && value.ProjectId == projectId && value.ContractId == contractId && value.IsQuantitySurveyGoverned && !value.IsDeleted
            && value.Status == ProjectVariationOrderStatuses.Approved && value.ApprovalStatus == ProjectVariationOrderStatuses.Approved
            && value.DownstreamApplicationStatus == ProjectVariationApplicationStatuses.Applied, token);
        if (variation is null)
            throw Validation("The selected QS variation must be approved, fully applied, and linked to the same project and active Works contract.");
        if (hasCostImpact && policy.RequireFinanceBudgetRevalidation && (!variation.BudgetRevisionId.HasValue || !variation.ForecastVersionId.HasValue))
            throw Conflict("The selected QS variation has not completed its authoritative Finance budget and forecast application.");
        return variation;
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, string templateCode, CancellationToken token)
    {
        var evidence = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId && value.DocumentRecordId == recordId
                && value.DocumentRecord.TenantId == TenantId && value.DocumentRecord.MetadataTemplateCode == templateCode, token);
        return evidence ?? throw Validation("Select current published central-DMS evidence for the configured extension-of-time document type.");
    }

    private async Task<Policy> ResolveEffectivePolicyAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted
                && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No effective published Civil Engineering configuration profile exists.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one Civil Engineering configuration profile is effective.");
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profiles[0].Id && value.ConfigurationKey == "CIV-CFG-014" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-014 extension-of-time decision.");
        return await ResolvePolicyAsync(profiles[0].Id, decision, token);
    }

    private async Task<Policy> ResolveFrozenPolicyAsync(ProjectCivilExtensionOfTimeControl control, CancellationToken token)
    {
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == control.ConfigurationDecisionId && value.ProfileId == control.ConfigurationProfileId && value.ConfigurationKey == "CIV-CFG-014" && !value.IsDeleted, token)
            ?? throw Conflict("The extension-of-time configuration lineage is no longer available.");
        var policy = await ResolvePolicyAsync(control.ConfigurationProfileId, decision, token);
        if (policy.WorkflowDefinitionId != control.WorkflowDefinitionId || policy.Template.Id != control.EvidenceMetadataTemplateId
            || !string.Equals(policy.Template.TemplateCode, control.EvidenceMetadataTemplateCodeSnapshot, StringComparison.OrdinalIgnoreCase)
            || !FixedEquals(policy.PolicyHash, control.PolicyHash))
            throw Conflict("The frozen extension-of-time configuration does not match its approved lineage.");
        return policy;
    }

    private async Task EnsureCurrentPolicyAsync(Policy frozen, CancellationToken token)
    {
        var current = await ResolveEffectivePolicyAsync(token);
        if (current.ProfileId != frozen.ProfileId || current.DecisionId != frozen.DecisionId || !FixedEquals(current.PolicyHash, frozen.PolicyHash))
            throw Conflict("The extension-of-time policy changed after submission. Return the request to a new governed revision.");
    }

    private async Task<Policy> ResolvePolicyAsync(Guid profileId, CivilEngineeringConfigurationDecision decision, CancellationToken token)
    {
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Validation("CIV-CFG-014 is not approved and verified.");
        CivilEngineeringExtensionOfTimeValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringExtensionOfTimeValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-014 contains invalid extension-of-time control data."); }
        if (value.WorkflowDefinitionId == Guid.Empty || value.EvidenceMetadataTemplateId == Guid.Empty || value.ReviewerRoleIds.Count == 0)
            throw Validation("CIV-CFG-014 must select the EOT workflow, DMS template and reviewer roles.");
        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.WorkflowDefinitionId
            && !item.IsDeleted && item.IsActive && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive && item.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.ExtensionOfTime, token);
        if (workflowDefinition is null || await db.WorkflowSteps.AsNoTracking().CountAsync(item => item.TenantId == TenantId && item.WorkflowDefinitionId == value.WorkflowDefinitionId && !item.IsDeleted, token) < 2)
            throw Validation($"The CIV-CFG-014 workflow must be active, Published, bound to {CivilEngineeringWorkflowBindingRegistry.ExtensionOfTime}, and contain review steps.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.EvidenceMetadataTemplateId && !item.IsDeleted && item.IsActive && item.PublishedAt.HasValue, token)
            ?? throw Validation("The CIV-CFG-014 central-DMS template is unavailable.");
        return new Policy(profileId, decision.Id, value.WorkflowDefinitionId, value, template, Hash(decision.ValueJson));
    }

    private async Task<IReadOnlyList<CivilEngineeringExtensionOfTimeLookupDto>> ContractLookupsAsync(Guid projectId, CancellationToken token) =>
        await (from package in db.ProjectPackages.AsNoTracking()
               join contract in db.Contracts.AsNoTracking() on package.ContractId equals (Guid?)contract.Id
               where package.TenantId == TenantId && package.ProjectId == projectId && package.ContractId.HasValue && !package.IsDeleted
                     && contract.TenantId == TenantId && !contract.IsDeleted && contract.Status == "Active" && contract.ContractType == "Works"
               orderby contract.ContractNumber
               select new CivilEngineeringExtensionOfTimeLookupDto { Id = contract.Id, Label = contract.ContractNumber + " — " + contract.ContractTitle }).Distinct().Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringExtensionOfTimeLookupDto>> VariationLookupsAsync(Guid projectId, CancellationToken token) =>
        await db.ProjectVariationOrders.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsQuantitySurveyGoverned && !value.IsDeleted
                && value.Status == ProjectVariationOrderStatuses.Approved && value.ApprovalStatus == ProjectVariationOrderStatuses.Approved && value.DownstreamApplicationStatus == ProjectVariationApplicationStatuses.Applied)
            .OrderByDescending(value => value.RequestedDate).Select(value => new CivilEngineeringExtensionOfTimeLookupDto { Id = value.Id, Label = (value.ReferenceNumber ?? "QS variation") + " — " + value.Title }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringExtensionOfTimeDocumentLookupDto>> DocumentLookupsAsync(string templateCode, CancellationToken token) =>
        await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId && value.DocumentRecord.MetadataTemplateCode == templateCode).OrderBy(value => value.DocumentRecord.DocumentReference)
            .Select(value => new CivilEngineeringExtensionOfTimeDocumentLookupDto { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).Take(250).ToListAsync(token);

    private async Task<string> NextReferenceAsync(CancellationToken token)
    {
        var year = DateTime.UtcNow.Year;
        var sequence = await db.ProjectCivilExtensionOfTimeControls.IgnoreQueryFilters().CountAsync(value => value.TenantId == TenantId && value.SubmittedAt.Year == year, token) + 1;
        return $"CIV-EOT-{year}-{sequence:00000}";
    }

    private static void ApplyProjectsEotOutcome(ProjectExtensionOfTime extension, WorkflowOutcome outcome, string comment, string reviewerName)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                extension.Status = ProjectExtensionOfTimeStatuses.Approved; extension.DaysApproved = extension.DaysRequested; extension.DecisionDate = DateTime.UtcNow; extension.DecidedByName = reviewerName; break;
            case WorkflowOutcome.Rejected:
                extension.Status = ProjectExtensionOfTimeStatuses.Rejected; extension.DaysApproved = null; extension.DecisionDate = DateTime.UtcNow; extension.DecidedByName = reviewerName; extension.Notes = comment.Trim(); break;
            default:
                extension.Status = ProjectExtensionOfTimeStatuses.UnderReview; break;
        }
    }

    private void AddRevision(ProjectCivilExtensionOfTimeControl control, string action, string fromStatus, object after, string? reason, string requestHash, string correlationId, object? before = null)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.ProjectCivilExtensionOfTimeRevisions.Add(new ProjectCivilExtensionOfTimeRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ExtensionOfTimeControlId = control.Id, Action = action, FromStatus = fromStatus, ToStatus = control.Status,
            ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = Clean(reason, 2000),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), RequestHash = requestHash,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
    }

    private void AddAudit(ProjectCivilExtensionOfTimeControl control, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectCivilExtensionOfTimeControl), ResourceId = control.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
        IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
    });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The extension-of-time control changed. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true || exception.InnerException?.Message.Contains("518", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("The extension-of-time control conflicts with an existing governed record or lifecycle rule."); }
    }

    private static CivilEngineeringExtensionOfTimeControlDto Map(ProjectCivilExtensionOfTimeControl value) => new()
    {
        Id = value.Id, ProjectId = value.ProjectId, ProjectExtensionOfTimeId = value.ProjectExtensionOfTimeId, ReferenceNumber = value.ExtensionOfTime.ReferenceNumber ?? string.Empty,
        Title = value.ExtensionOfTime.Title, Reason = value.ExtensionOfTime.Reason ?? string.Empty, ScopeSummary = value.ScopeSummary, ContractId = value.ContractId,
        ContractLabel = value.Contract.ContractNumber + " — " + value.Contract.ContractTitle, HasCostImpact = value.HasCostImpact, QuantitySurveyVariationOrderId = value.QuantitySurveyVariationOrderId,
        QuantitySurveyVariationLabel = value.QuantitySurveyVariationOrder is null ? null : (value.QuantitySurveyVariationOrder.ReferenceNumber ?? "QS variation") + " — " + value.QuantitySurveyVariationOrder.Title,
        DaysRequested = value.ExtensionOfTime.DaysRequested ?? 0, DaysApproved = value.ExtensionOfTime.DaysApproved, RequestedDate = value.ExtensionOfTime.RequestedDate,
        DecisionDate = value.ExtensionOfTime.DecisionDate, ProposedRevisedCompletionDate = value.ExtensionOfTime.RevisedCompletionDate, Status = value.Status, ApprovalStatus = value.ApprovalStatus,
        RejectionReason = value.RejectionReason, WorkflowInstanceId = value.WorkflowInstanceId, EvidenceDocumentRecordId = value.EvidenceDocumentRecordId, EvidenceDocumentVersionId = value.EvidenceDocumentVersionId,
        EvidenceDocumentReference = value.EvidenceDocumentVersion.DocumentRecord.DocumentReference, EvidenceDocumentTitle = value.EvidenceDocumentVersion.DocumentRecord.Title,
        EvidenceVersionNumber = value.EvidenceDocumentVersion.VersionNumber, RowVersion = Convert.ToBase64String(value.RowVersion)
    };

    private static object Snapshot(ProjectCivilExtensionOfTimeControl control, ProjectExtensionOfTime extension) => new
    {
        control.Id, control.ProjectId, control.ProjectExtensionOfTimeId, control.ContractId, control.QuantitySurveyVariationOrderId, control.ScopeSummary, control.HasCostImpact,
        control.Status, control.ApprovalStatus, control.SubmittedById, control.SubmittedAt, control.ApprovedById, control.ApprovedAt, control.RejectionReason, control.WorkflowInstanceId,
        control.EvidenceDocumentRecordId, control.EvidenceDocumentVersionId, control.ConfigurationProfileId, control.ConfigurationDecisionId, control.WorkflowDefinitionId, control.EvidenceMetadataTemplateId,
        control.EvidenceMetadataTemplateCodeSnapshot, control.PolicyHash, extension.ReferenceNumber, extension.Title, extension.Reason,
        ExtensionStatus = extension.Status, extension.DaysRequested, extension.DaysApproved, extension.RevisedCompletionDate
    };

    private static void ApplyRowVersion(ProjectCivilExtensionOfTimeControl control, string encoded)
    {
        try
        {
            var expected = Convert.FromBase64String(encoded);
            if (!CryptographicOperations.FixedTimeEquals(expected, control.RowVersion))
                throw Conflict("The extension-of-time control changed. Refresh and retry.");
        }
        catch (FormatException) { throw Validation("The extension-of-time concurrency version is invalid."); }
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string RequiredText(string? value, int maximum, string label) { var result = value?.Trim(); if (string.IsNullOrWhiteSpace(result)) throw Validation($"{label} is required."); return result.Length <= maximum ? result : throw Validation($"{label} cannot exceed {maximum} characters."); }
    private static string? Clean(string? value, int maximum) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= maximum ? value.Trim() : throw Validation($"Text cannot exceed {maximum} characters.");
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) { var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right); return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
    private static CivilEngineeringSupervisionValidationException Validation(string message) => new(message);
    private static CivilEngineeringSupervisionValidationException Validation(IEnumerable<string> messages) => new(string.Join(" ", messages));
    private static CivilEngineeringSupervisionConflictException Conflict(string message) => new(message);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, CivilEngineeringExtensionOfTimeValue Value, CentralDocumentMetadataTemplate Template, string PolicyHash);
}
