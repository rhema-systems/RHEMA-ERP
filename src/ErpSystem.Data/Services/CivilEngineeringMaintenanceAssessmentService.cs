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
/// Civil's governed assessment overlay. It delegates assignment qualification to the
/// existing Security/Projects owners and review sequencing to the shared Workflow engine.
/// </summary>
public sealed class CivilEngineeringMaintenanceAssessmentService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : ICivilEngineeringMaintenanceAssessmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item));

    public async Task<CivilEngineeringMaintenanceAssessmentLookupsDto> GetLookupsAsync(CancellationToken token = default)
    {
        var policy = await ResolveCurrentPolicyAsync(token);
        return new CivilEngineeringMaintenanceAssessmentLookupsDto
        {
            SupervisingCivilEngineers = await UsersWithRoleAsync(CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, token),
            CivilEngineers = await UsersWithRoleAsync(CivilEngineeringAccessControlRegistry.CivilEngineerRole, token),
            DefectCategories = await db.ProjectCatalogEntries.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted && item.IsActive
                    && item.CatalogType == "civil-defect-categories" && policy.Value.DefectCategoryIds.Contains(item.Id)
                    && (!item.EffectiveFrom.HasValue || item.EffectiveFrom <= DateTime.UtcNow) && (!item.EffectiveTo.HasValue || item.EffectiveTo >= DateTime.UtcNow))
                .OrderBy(item => item.SortOrder).ThenBy(item => item.Name).Select(item => new CivilEngineeringMaintenanceIntakeLookupOptionDto { Id = item.Id, Label = item.Code + " - " + item.Name }).ToListAsync(token),
            Documents = await DocumentLookupsAsync(policy.Template.TemplateCode, token)
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceAssessmentDto>> ListAsync(CancellationToken token = default) =>
        await MapAsync(await Assessments(false).OrderByDescending(item => item.CreatedAt).Take(500).ToListAsync(token), token);

    public async Task<CivilEngineeringMaintenanceAssessmentDto> StartAsync(Guid intakeId, StartCivilEngineeringMaintenanceAssessmentRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringMaintenanceAssessmentPolicy.ValidateStart(request);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new { intakeId, request.SupervisingCivilEngineerUserId, direction = request.Direction.Trim(), dueAt = Utc(request.DueAt) });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Assessments(true).SingleOrDefaultAsync(item => item.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different assessment direction values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }
        var intake = await Intakes(true).SingleOrDefaultAsync(item => item.Id == intakeId, token)
            ?? throw new CivilEngineeringMaintenanceIntakeNotFoundException("The Civil maintenance intake was not found.");
        if (intake.Status != CivilEngineeringMaintenanceIntakeStatuses.Logged)
            throw Conflict("Only a logged Civil maintenance intake can be directed for assessment. Returned assessments must resume from their existing Civil Engineer task.");
        if (await Assessments(true).AnyAsync(item => item.IntakeId == intakeId, token))
            throw Conflict("This Civil maintenance intake already has its governed assessment record.");
        var policy = await ResolveFrozenPolicyAsync(intake, token);
        await RequireActorAsync(intake.ProjectId, UserId, CivilEngineeringAccessControlRegistry.HeadRole, token);
        await RequireActorAsync(intake.ProjectId, request.SupervisingCivilEngineerUserId, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, token);
        CivilEngineeringMaintenanceAssessmentPolicy.EnsureDistinctAssignments(UserId, request.SupervisingCivilEngineerUserId, null);
        var now = DateTime.UtcNow;
        var assessment = new CivilEngineeringMaintenanceAssessment
        {
            Id = Guid.NewGuid(), TenantId = TenantId, IntakeId = intake.Id, HodUserId = UserId, SupervisingCivilEngineerUserId = request.SupervisingCivilEngineerUserId,
            CurrentAssigneeUserId = request.SupervisingCivilEngineerUserId, CurrentDueAt = Utc(request.DueAt), PolicyHash = policy.PolicyHash,
            Stage = CivilEngineeringMaintenanceAssessmentStages.SceAssignment, Status = CivilEngineeringMaintenanceAssessmentStatuses.InProgress,
            ApprovalStatus = CivilEngineeringMaintenanceAssessmentApprovalStatuses.Draft, ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
            CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.CivilEngineeringMaintenanceAssessments.Add(assessment);
        var intakeBefore = IntakeSnapshot(intake);
        intake.Status = CivilEngineeringMaintenanceIntakeStatuses.AssessmentInProgress;
        intake.UpdatedAt = now; intake.UpdatedBy = UserName; intake.LastModifiedById = UserId;
        AddRevision(assessment, CivilEngineeringAuditEventMap.UpdateCivilWorkAssessment, null, assessment.Stage, request.Direction.Trim(), null, Snapshot(assessment), correlationId);
        AddAudit(assessment, CivilEngineeringAuditEventMap.UpdateCivilWorkAssessment, null, Snapshot(assessment), correlationId);
        AddIntakeRevision(intake, CivilEngineeringAuditEventMap.UpdateCivilWorkAssessment, intakeBefore, IntakeSnapshot(intake), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([assessment], token)).Single();
    }

    public async Task<CivilEngineeringMaintenanceAssessmentDto> TransitionAsync(Guid assessmentId, CivilEngineeringMaintenanceAssessmentTransitionRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required for safe retry.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var assessment = await Assessments(true).Include(item => item.Intake).SingleOrDefaultAsync(item => item.Id == assessmentId, token)
            ?? throw new CivilEngineeringMaintenanceAssessmentNotFoundException("The Civil maintenance assessment was not found.");
        var policy = await ResolveFrozenPolicyAsync(assessment.Intake, token);
        var reason = Clean(request.Reason, 2000);
        var mutationHash = Hash(new
        {
            assessmentId, request.Action, request.AssigneeUserId, request.DefectCategoryId, siteAssessment = Clean(request.SiteAssessment, 4000),
            scope = Clean(request.ScopeRecommendation, 4000), remedy = Clean(request.RemedyRecommendation, 4000), request.EstimatedCost,
            request.CentralDocumentRecordId, request.CentralDocumentVersionId, dueAt = Utc(request.DueAt), reason
        });
        if (assessment.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(assessment.LastMutationRequestHash ?? string.Empty, mutationHash)) throw Conflict("This assessment transition request identifier was already used with different values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([assessment], token)).Single();
        }
        ApplyRowVersion(assessment, request.RowVersion);
        CivilEngineeringMaintenanceAssessmentTransition transition;
        try { transition = CivilEngineeringMaintenanceAssessmentPolicy.GetRequired(assessment.Stage, request.Action); }
        catch (InvalidOperationException exception) { throw Validation(exception.Message); }
        if (transition.RequiresReason && (reason is null || reason.Length < 5)) throw Validation("A reason of at least 5 characters is required for this action.");
        await RequireTransitionActorAsync(assessment, transition.Actor, token);
        if (transition.AssigneeRole is not null)
        {
            var assignee = request.AssigneeUserId ?? throw Validation($"Select the controlled {transition.AssigneeRole} assignee.");
            await RequireActorAsync(assessment.Intake.ProjectId, assignee, transition.AssigneeRole, token);
            assessment.CivilEngineerUserId = assignee;
            CivilEngineeringMaintenanceAssessmentPolicy.EnsureDistinctAssignments(assessment.HodUserId, assessment.SupervisingCivilEngineerUserId, assignee);
        }
        else if (request.AssigneeUserId.HasValue) throw Validation("This assessment action does not accept an assignee override.");

        var terminal = transition.ToStage is CivilEngineeringMaintenanceAssessmentStages.Approved or CivilEngineeringMaintenanceAssessmentStages.Rejected;
        if (!terminal && (!request.DueAt.HasValue || Utc(request.DueAt) <= DateTime.UtcNow)) throw Validation("Select a future due date for the next assigned assessment task.");
        if (request.Action == CivilEngineeringMaintenanceAssessmentAction.SubmitAssessment)
        {
            var errors = CivilEngineeringMaintenanceAssessmentPolicy.ValidateAssessmentSubmission(request, policy.Value);
            if (errors.Count > 0) throw Validation(errors);
            await RequireDefectCategoryAsync(request.DefectCategoryId!.Value, policy.Value, token);
            var evidence = await RequireEvidenceAsync(request.CentralDocumentRecordId!.Value, request.CentralDocumentVersionId!.Value, policy.Template.TemplateCode, token);
            assessment.DefectCategoryId = request.DefectCategoryId;
            assessment.SiteAssessment = Clean(request.SiteAssessment, 4000);
            assessment.ScopeRecommendation = Clean(request.ScopeRecommendation, 4000);
            assessment.RemedyRecommendation = Clean(request.RemedyRecommendation, 4000);
            assessment.EstimatedCost = request.EstimatedCost;
            assessment.CentralDocumentRecordId = evidence.DocumentRecordId;
            assessment.CentralDocumentVersionId = evidence.Id;
        }
        else if (request.DefectCategoryId.HasValue || request.SiteAssessment is not null || request.ScopeRecommendation is not null || request.RemedyRecommendation is not null || request.EstimatedCost.HasValue || request.CentralDocumentRecordId.HasValue || request.CentralDocumentVersionId.HasValue)
            throw Validation("Assessment findings and DMS evidence may be recorded only when the Civil Engineer submits the assessment.");

        var before = Snapshot(assessment);
        var intakeBefore = IntakeSnapshot(assessment.Intake);
        var fromStage = assessment.Stage;
        if (transition.StartsSharedWorkflow)
        {
            var result = await workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment, assessment.Id, assessment.Intake.WorkflowDefinitionId);
            if (!result.ExecutionResult.Success || result.Outcome != WorkflowOutcome.Pending) throw Conflict(result.ExecutionResult.Message ?? "The configured assessment workflow must start in a pending SCE review state.");
            workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment).ApplySubmitOutcome(assessment, result.Outcome, UserId);
            assessment.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        }
        else if (transition.AdvancesSharedWorkflow)
        {
            await RequireWorkflowApproverAsync(assessment, token);
            var result = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment, assessment.Id, UserId, "Approve", reason ?? "SCE assessment review completed.");
            if (!result.ExecutionResult.Success || result.Outcome != WorkflowOutcome.Pending) throw Conflict(result.ExecutionResult.Message ?? "The assessment workflow must continue to HOD review after SCE review.");
            workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment).ApplyApprovalOutcome(assessment, result.Outcome, UserId, reason ?? string.Empty);
        }
        else if (transition.CompletesSharedWorkflow || request.Action == CivilEngineeringMaintenanceAssessmentAction.ReturnAssessment)
        {
            await RequireWorkflowApproverAsync(assessment, token);
            var action = request.Action is CivilEngineeringMaintenanceAssessmentAction.Approve or CivilEngineeringMaintenanceAssessmentAction.SubmitToHod ? "Approve" : "Reject";
            var result = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment, assessment.Id, UserId, action, reason ?? "Assessment returned for remediation.");
            if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The assessment workflow decision could not be processed.");
            if (request.Action == CivilEngineeringMaintenanceAssessmentAction.ReturnAssessment && result.Outcome != WorkflowOutcome.Rejected) throw Conflict("The shared assessment workflow did not return a rejected outcome for rework.");
            if (request.Action == CivilEngineeringMaintenanceAssessmentAction.Approve && result.Outcome != WorkflowOutcome.Approved) throw Conflict("The shared assessment workflow did not approve the scope recommendation.");
            if (request.Action == CivilEngineeringMaintenanceAssessmentAction.Reject && result.Outcome != WorkflowOutcome.Rejected) throw Conflict("The shared assessment workflow did not reject the scope recommendation.");
            workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment).ApplyApprovalOutcome(assessment, result.Outcome, UserId, reason ?? string.Empty);
        }

        assessment.Stage = transition.ToStage;
        assessment.CurrentAssigneeUserId = transition.ToStage switch
        {
            CivilEngineeringMaintenanceAssessmentStages.CivilEngineerAssessment => assessment.CivilEngineerUserId,
            CivilEngineeringMaintenanceAssessmentStages.SceAssessmentReview => assessment.SupervisingCivilEngineerUserId,
            CivilEngineeringMaintenanceAssessmentStages.HodFinalReview => assessment.HodUserId,
            _ => null
        };
        assessment.CurrentDueAt = terminal ? null : Utc(request.DueAt);
        if (request.Action == CivilEngineeringMaintenanceAssessmentAction.ReturnAssessment)
        {
            assessment.Status = CivilEngineeringMaintenanceAssessmentStatuses.InProgress;
            assessment.ApprovalStatus = CivilEngineeringMaintenanceAssessmentApprovalStatuses.Draft;
            assessment.RejectionReason = reason;
            assessment.Intake.Status = CivilEngineeringMaintenanceIntakeStatuses.AssessmentReturned;
        }
        else if (request.Action == CivilEngineeringMaintenanceAssessmentAction.Reject)
        {
            assessment.Status = CivilEngineeringMaintenanceAssessmentStatuses.Rejected;
            assessment.ApprovalStatus = CivilEngineeringMaintenanceAssessmentApprovalStatuses.Rejected;
            assessment.RejectionReason = reason;
            assessment.Intake.Status = CivilEngineeringMaintenanceIntakeStatuses.Assessed;
        }
        else if (request.Action == CivilEngineeringMaintenanceAssessmentAction.Approve)
        {
            assessment.Status = CivilEngineeringMaintenanceAssessmentStatuses.Approved;
            assessment.ApprovalStatus = CivilEngineeringMaintenanceAssessmentApprovalStatuses.Approved;
            assessment.ApprovedById = UserId; assessment.ApprovedAt = DateTime.UtcNow;
            assessment.Intake.Status = CivilEngineeringMaintenanceIntakeStatuses.Assessed;
        }
        else if (request.Action != CivilEngineeringMaintenanceAssessmentAction.AssignCivilEngineer)
        {
            assessment.Status = CivilEngineeringMaintenanceAssessmentStatuses.PendingApproval;
            assessment.ApprovalStatus = CivilEngineeringMaintenanceAssessmentApprovalStatuses.Pending;
            assessment.Intake.Status = CivilEngineeringMaintenanceIntakeStatuses.AssessmentInProgress;
        }
        else
        {
            assessment.Status = CivilEngineeringMaintenanceAssessmentStatuses.InProgress;
            assessment.ApprovalStatus = CivilEngineeringMaintenanceAssessmentApprovalStatuses.Draft;
            assessment.Intake.Status = CivilEngineeringMaintenanceIntakeStatuses.AssessmentInProgress;
        }
        assessment.LastMutationClientRequestId = request.ClientRequestId; assessment.LastMutationRequestHash = mutationHash; assessment.CorrelationId = Correlation(correlationId);
        assessment.UpdatedAt = DateTime.UtcNow; assessment.UpdatedBy = UserName; assessment.LastModifiedById = UserId;
        assessment.Intake.UpdatedAt = DateTime.UtcNow; assessment.Intake.UpdatedBy = UserName; assessment.Intake.LastModifiedById = UserId;
        var audit = request.Action switch
        {
            CivilEngineeringMaintenanceAssessmentAction.SubmitAssessment => CivilEngineeringAuditEventMap.SubmitRemediationScope,
            CivilEngineeringMaintenanceAssessmentAction.Approve => CivilEngineeringAuditEventMap.ApproveRemediationScope,
            CivilEngineeringMaintenanceAssessmentAction.Reject or CivilEngineeringMaintenanceAssessmentAction.ReturnAssessment => CivilEngineeringAuditEventMap.RejectRemediationScope,
            _ => CivilEngineeringAuditEventMap.UpdateCivilWorkAssessment
        };
        AddRevision(assessment, audit, fromStage, assessment.Stage, reason, before, Snapshot(assessment), correlationId);
        AddAudit(assessment, audit, before, Snapshot(assessment), correlationId);
        AddIntakeRevision(assessment.Intake, audit, intakeBefore, IntakeSnapshot(assessment.Intake), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([assessment], token)).Single();
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceAssessmentRevisionDto>> GetHistoryAsync(Guid assessmentId, CancellationToken token = default)
    {
        if (!await Assessments(false).AnyAsync(item => item.Id == assessmentId, token)) throw new CivilEngineeringMaintenanceAssessmentNotFoundException("The Civil maintenance assessment was not found.");
        return await db.CivilEngineeringMaintenanceAssessmentRevisions.AsNoTracking().Where(item => item.TenantId == TenantId && item.AssessmentId == assessmentId && !item.IsDeleted).OrderBy(item => item.CreatedAt)
            .Select(item => new CivilEngineeringMaintenanceAssessmentRevisionDto { Id = item.Id, Action = item.Action, FromStage = item.FromStage, ToStage = item.ToStage, ActorName = item.ActorName, ActorRoles = item.ActorRoles, Reason = item.Reason, CorrelationId = item.CorrelationId, CreatedAt = item.CreatedAt }).ToListAsync(token);
    }

    private IQueryable<CivilEngineeringMaintenanceIntake> Intakes(bool tracked) => (tracked ? db.CivilEngineeringMaintenanceIntakes : db.CivilEngineeringMaintenanceIntakes.AsNoTracking()).Where(item => item.TenantId == TenantId && !item.IsDeleted);
    private IQueryable<CivilEngineeringMaintenanceAssessment> Assessments(bool tracked) => (tracked ? db.CivilEngineeringMaintenanceAssessments : db.CivilEngineeringMaintenanceAssessments.AsNoTracking()).Where(item => item.TenantId == TenantId && !item.IsDeleted);

    private async Task RequireTransitionActorAsync(CivilEngineeringMaintenanceAssessment assessment, CivilEngineeringMaintenanceAssessmentActor actor, CancellationToken token)
    {
        var (expectedUserId, role) = actor switch
        {
            CivilEngineeringMaintenanceAssessmentActor.HeadOfCivilEngineering => (assessment.HodUserId, CivilEngineeringAccessControlRegistry.HeadRole),
            CivilEngineeringMaintenanceAssessmentActor.SupervisingCivilEngineer => (assessment.SupervisingCivilEngineerUserId, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole),
            CivilEngineeringMaintenanceAssessmentActor.CivilEngineer => (assessment.CivilEngineerUserId ?? Guid.Empty, CivilEngineeringAccessControlRegistry.CivilEngineerRole),
            _ => throw Validation("The Civil assessment actor is not supported.")
        };
        if (expectedUserId != UserId) throw new UnauthorizedAccessException("Only the assigned Civil Engineering user can perform this assessment action.");
        await RequireActorAsync(assessment.Intake.ProjectId, UserId, role, token);
    }

    private async Task RequireActorAsync(Guid? projectId, Guid userId, string role, CancellationToken token)
    {
        if (userId == UserId && !currentUser.Roles.Any(item => string.Equals(item, role, StringComparison.OrdinalIgnoreCase))) throw new UnauthorizedAccessException($"The {role} role is required for this action.");
        var active = await db.Users.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.Id == userId && item.IsActive, token);
        if (!active) throw Validation("Select an active tenant user for the Civil Engineering role assignment.");
        var roleAssigned = await db.UserRoles.AsNoTracking().Include(item => item.Role).AnyAsync(item => item.UserId == userId && (item.Role.Name == role || item.Role.NormalizedName == role), token);
        if (!roleAssigned) throw Validation($"The selected user does not hold the required {role} Security role.");
        if (projectId.HasValue)
        {
            if (await projectService.GetProjectByIdAsync(projectId.Value) is null) throw new UnauthorizedAccessException("You are not permitted to access the linked project.");
            var member = await db.ProjectMembers.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.ProjectId == projectId && item.UserId == userId && item.IsActive && !item.IsDeleted, token);
            if (!member) throw Validation($"The selected user is not an active member of the linked project.");
        }
    }

    private async Task RequireWorkflowApproverAsync(CivilEngineeringMaintenanceAssessment assessment, CancellationToken token)
    {
        if (!assessment.WorkflowInstanceId.HasValue || !await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment, assessment.Id, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the active maintenance-assessment workflow review step.");
    }

    private async Task RequireDefectCategoryAsync(Guid categoryId, CivilEngineeringMaintenanceAssessmentValue controls, CancellationToken token)
    {
        if (!controls.DefectCategoryIds.Contains(categoryId) || !await db.ProjectCatalogEntries.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.Id == categoryId && item.CatalogType == "civil-defect-categories" && item.IsActive && !item.IsDeleted, token))
            throw Validation("Select an active defect category configured by CIV-CFG-007.");
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, string templateCode, CancellationToken token)
    {
        var version = await db.CentralDocumentVersions.AsNoTracking().Include(item => item.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == versionId, token)
            ?? throw Validation("Select a current Published central-DMS assessment evidence document.");
        if (version.DocumentRecordId != recordId || !string.Equals(version.DocumentRecord.MetadataTemplateCode, templateCode, StringComparison.OrdinalIgnoreCase)) throw Validation($"The assessment evidence must be a current Published DMS version using template {templateCode}.");
        return version;
    }

    private async Task<IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto>> UsersWithRoleAsync(string role, CancellationToken token) =>
        await db.UserRoles.AsNoTracking().Include(item => item.User).Include(item => item.Role).Where(item => item.User.TenantId == TenantId && item.User.IsActive && (item.Role.Name == role || item.Role.NormalizedName == role))
            .OrderBy(item => item.User.FirstName).ThenBy(item => item.User.LastName).Select(item => new CivilEngineeringMaintenanceIntakeLookupOptionDto { Id = item.UserId, Label = Name(item.User.FirstName, item.User.LastName, item.User.UserName) }).Distinct().Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringMaintenanceIntakeDocumentLookupDto>> DocumentLookupsAsync(string templateCode, CancellationToken token) =>
        await db.CentralDocumentVersions.AsNoTracking().Include(item => item.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).Where(item => item.TenantId == TenantId && item.DocumentRecord.MetadataTemplateCode == templateCode)
            .OrderBy(item => item.DocumentRecord.DocumentReference).ThenByDescending(item => item.CreatedAt).Select(item => new CivilEngineeringMaintenanceIntakeDocumentLookupDto { CentralDocumentRecordId = item.DocumentRecordId, CentralDocumentVersionId = item.Id, DocumentReference = item.DocumentRecord.DocumentReference, Title = item.DocumentRecord.Title, VersionNumber = item.VersionNumber }).Take(250).ToListAsync(token);

    private async Task<Policy> ResolveCurrentPolicyAsync(CancellationToken token)
    {
        var profile = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted && item.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && item.EffectiveFrom <= DateTime.UtcNow && (!item.EffectiveTo.HasValue || item.EffectiveTo >= DateTime.UtcNow))
            .OrderByDescending(item => item.IsDefault).ThenByDescending(item => item.Version).FirstOrDefaultAsync(token) ?? throw Validation("No effective published Civil Engineering configuration profile exists.");
        var intake = new CivilEngineeringMaintenanceIntake { TenantId = TenantId, ConfigurationProfileId = profile.Id };
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.ProfileId == profile.Id && item.ConfigurationKey == "CIV-CFG-007" && !item.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-007 maintenance-assessment decision.");
        intake.ConfigurationDecisionId = decision.Id;
        return await ResolvePolicyAsync(intake, true, token);
    }

    private Task<Policy> ResolveFrozenPolicyAsync(CivilEngineeringMaintenanceIntake intake, CancellationToken token) => ResolvePolicyAsync(intake, false, token);

    private async Task<Policy> ResolvePolicyAsync(CivilEngineeringMaintenanceIntake intake, bool requireCurrent, CancellationToken token)
    {
        var profile = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == intake.ConfigurationProfileId && !item.IsDeleted, token)
            ?? throw Validation("The Civil maintenance intake configuration profile is unavailable.");
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == intake.ConfigurationDecisionId && item.ProfileId == profile.Id && item.ConfigurationKey == "CIV-CFG-007" && !item.IsDeleted, token)
            ?? throw Validation("The Civil maintenance intake configuration decision is unavailable.");
        if (requireCurrent && (profile.LifecycleStatus != CivilEngineeringConfigurationProfileStatus.Published || profile.EffectiveFrom > DateTime.UtcNow || (profile.EffectiveTo.HasValue && profile.EffectiveTo < DateTime.UtcNow))) throw Validation("CIV-CFG-007 is not effective for this date.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified) throw Validation("CIV-CFG-007 is not approved and verified.");
        CivilEngineeringMaintenanceAssessmentValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringMaintenanceAssessmentValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-007 contains invalid maintenance-assessment control data."); }
        if (value.WorkflowDefinitionId == Guid.Empty || value.MetadataTemplateId == Guid.Empty || value.DefectCategoryIds.Count == 0) throw Validation("CIV-CFG-007 must select its assessment workflow, DMS template and defect categories.");
        if (intake.WorkflowDefinitionId != Guid.Empty && intake.WorkflowDefinitionId != value.WorkflowDefinitionId) throw Conflict("The maintenance intake workflow lineage no longer matches CIV-CFG-007.");
        var definition = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).Include(item => item.Steps).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.WorkflowDefinitionId && !item.IsDeleted && item.IsActive && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive && item.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment, token)
            ?? throw Validation($"CIV-CFG-007 must reference an active Published workflow bound to {CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment}.");
        var workflowErrors = CivilEngineeringMaintenanceAssessmentPolicy.ValidateWorkflowDefinition(definition);
        if (workflowErrors.Count > 0) throw Validation(workflowErrors);
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.MetadataTemplateId && item.IsActive && item.PublishedAt.HasValue && !item.IsDeleted, token)
            ?? throw Validation("The CIV-CFG-007 DMS assessment-evidence template is unavailable.");
        if (intake.EvidenceMetadataTemplateId != Guid.Empty && (intake.EvidenceMetadataTemplateId != template.Id || !string.Equals(intake.EvidenceMetadataTemplateCodeSnapshot, template.TemplateCode, StringComparison.OrdinalIgnoreCase))) throw Conflict("The maintenance intake evidence-template lineage no longer matches CIV-CFG-007.");
        return new Policy(value, template, Hash(decision.ValueJson));
    }

    private async Task<IReadOnlyList<CivilEngineeringMaintenanceAssessmentDto>> MapAsync(IReadOnlyCollection<CivilEngineeringMaintenanceAssessment> values, CancellationToken token)
    {
        var intakeIds = values.Select(item => item.IntakeId).Distinct().ToList(); var userIds = values.SelectMany(item => new[] { item.HodUserId, item.SupervisingCivilEngineerUserId, item.CivilEngineerUserId ?? Guid.Empty }).Where(item => item != Guid.Empty).Distinct().ToList();
        var categoryIds = values.Where(item => item.DefectCategoryId.HasValue).Select(item => item.DefectCategoryId!.Value).Distinct().ToList(); var documentIds = values.Where(item => item.CentralDocumentRecordId.HasValue).Select(item => item.CentralDocumentRecordId!.Value).Distinct().ToList();
        var intakes = await db.CivilEngineeringMaintenanceIntakes.AsNoTracking().Where(item => item.TenantId == TenantId && intakeIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.IntakeNumber, token);
        var users = await db.Users.AsNoTracking().Where(item => item.TenantId == TenantId && userIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => Name(item.FirstName, item.LastName, item.UserName), token);
        var categories = await db.ProjectCatalogEntries.AsNoTracking().Where(item => item.TenantId == TenantId && categoryIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Code + " - " + item.Name, token);
        var docs = await db.CentralDocumentRecords.AsNoTracking().Where(item => item.TenantId == TenantId && documentIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.DocumentReference, token);
        return values.Select(item => new CivilEngineeringMaintenanceAssessmentDto { Id = item.Id, IntakeId = item.IntakeId, IntakeNumber = intakes.GetValueOrDefault(item.IntakeId, "Unavailable intake"), Stage = item.Stage, Status = item.Status, ApprovalStatus = item.ApprovalStatus, HodUserId = item.HodUserId, HodName = users.GetValueOrDefault(item.HodUserId, "Unavailable HOD"), SupervisingCivilEngineerUserId = item.SupervisingCivilEngineerUserId, SupervisingCivilEngineerName = users.GetValueOrDefault(item.SupervisingCivilEngineerUserId, "Unavailable SCE"), CivilEngineerUserId = item.CivilEngineerUserId, CivilEngineerName = item.CivilEngineerUserId.HasValue ? users.GetValueOrDefault(item.CivilEngineerUserId.Value) : null, CurrentAssigneeUserId = item.CurrentAssigneeUserId, CurrentDueAt = item.CurrentDueAt, DefectCategoryId = item.DefectCategoryId, DefectCategoryLabel = item.DefectCategoryId.HasValue ? categories.GetValueOrDefault(item.DefectCategoryId.Value) : null, SiteAssessment = item.SiteAssessment, ScopeRecommendation = item.ScopeRecommendation, RemedyRecommendation = item.RemedyRecommendation, EstimatedCost = item.EstimatedCost, CentralDocumentRecordId = item.CentralDocumentRecordId, CentralDocumentVersionId = item.CentralDocumentVersionId, EvidenceReference = item.CentralDocumentRecordId.HasValue ? docs.GetValueOrDefault(item.CentralDocumentRecordId.Value) : null, WorkflowInstanceId = item.WorkflowInstanceId, ApprovedById = item.ApprovedById, ApprovedAt = item.ApprovedAt, RejectionReason = item.RejectionReason, RowVersion = Convert.ToBase64String(item.RowVersion) }).ToList();
    }

    private void AddRevision(CivilEngineeringMaintenanceAssessment assessment, string action, string? fromStage, string toStage, string? reason, object? before, object after, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.CivilEngineeringMaintenanceAssessmentRevisions.Add(new CivilEngineeringMaintenanceAssessmentRevision { Id = Guid.NewGuid(), TenantId = TenantId, AssessmentId = assessment.Id, Action = action, FromStage = fromStage, ToStage = toStage, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, Reason = reason, CorrelationId = Correlation(correlationId), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }
    private void AddIntakeRevision(CivilEngineeringMaintenanceIntake intake, string action, object before, object after, string correlationId) => db.CivilEngineeringMaintenanceIntakeRevisions.Add(new CivilEngineeringMaintenanceIntakeRevision { Id = Guid.NewGuid(), TenantId = TenantId, IntakeId = intake.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), BeforeJson = JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private void AddAudit(CivilEngineeringMaintenanceAssessment assessment, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(CivilEngineeringMaintenanceAssessment), ResourceId = assessment.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });

    private async Task SaveAsync(CancellationToken token) { try { await db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw Conflict("The Civil maintenance assessment changed concurrently. Refresh and retry."); } catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52200 and <= 52219) { throw Conflict(sql.Message); } catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting Civil maintenance assessment was detected. Refresh and retry."); } }
    private static void ApplyRowVersion(CivilEngineeringMaintenanceAssessment item, string value) { try { item.RowVersion = Convert.FromBase64String(value); } catch (FormatException) { throw Validation("The assessment concurrency version is invalid."); } }
    private static object Snapshot(CivilEngineeringMaintenanceAssessment item) => new { item.Id, item.IntakeId, item.HodUserId, item.SupervisingCivilEngineerUserId, item.CivilEngineerUserId, item.CurrentAssigneeUserId, item.CurrentDueAt, item.DefectCategoryId, item.SiteAssessment, item.ScopeRecommendation, item.RemedyRecommendation, item.EstimatedCost, item.CentralDocumentRecordId, item.CentralDocumentVersionId, item.WorkflowInstanceId, item.Stage, item.Status, item.ApprovalStatus, item.PolicyHash };
    private static object IntakeSnapshot(CivilEngineeringMaintenanceIntake item) => new { item.Id, item.IntakeNumber, item.Status };
    private static string? Clean(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var normalized = value.Trim(); return normalized.Length <= max ? normalized : throw Validation($"Text cannot exceed {max} characters."); }
    private static DateTime? Utc(DateTime? value) => value?.ToUniversalTime();
    private static string Name(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static CivilEngineeringMaintenanceAssessmentValidationException Validation(string value) => new(value);
    private static CivilEngineeringMaintenanceAssessmentValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringMaintenanceAssessmentConflictException Conflict(string value) => new(value);
    private sealed record Policy(CivilEngineeringMaintenanceAssessmentValue Value, CentralDocumentMetadataTemplate Template, string PolicyHash);
}
