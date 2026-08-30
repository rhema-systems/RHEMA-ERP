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
/// A controlled Civil inspection lifecycle. Projects owns the quality checkpoint and
/// non-conformance records; Planning/GIS owns the spatial validation and central DMS owns files.
/// </summary>
public sealed class CivilEngineeringInspectionControlService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow) : ICivilEngineeringInspectionControlService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringInspectionLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var policy = await ResolveEffectivePolicyAsync(DateTime.UtcNow, token);
        return new CivilEngineeringInspectionLookupsDto
        {
            PlanningGisValidations = await PlanningLookupsAsync(projectId, token),
            Inspectors = await InspectorLookupsAsync(projectId, policy, token),
            Documents = await DocumentLookupsAsync(policy.Template.TemplateCode, token),
            RequireIndependentReinspection = true,
            BlockCompletionOnFailure = policy.Quality.BlockAcceptanceOnFailure
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringInspectionControlDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var values = await Controls(false).Where(value => value.ProjectId == projectId).OrderByDescending(value => value.ScheduledAt).ThenByDescending(value => value.CreatedAt).ToListAsync(token);
        return await MapAsync(values, token);
    }

    public async Task<CivilEngineeringInspectionControlDto> CreateAsync(Guid projectId, CreateCivilEngineeringInspectionControlRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringInspectionPolicy.ValidateCreate(request);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new { projectId, request.PlanningGisValidationId, request.InspectorUserId, scheduledAt = Utc(request.ScheduledAt), purpose = request.Purpose.Trim(), request.PlanDocumentRecordId, request.PlanDocumentVersionId });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Controls(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This inspection request identifier was already used with different values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }

        await RequireProjectAsync(projectId, token);
        var policy = await ResolveEffectivePolicyAsync(DateTime.UtcNow, token);
        var planning = await RequirePlanningValidationAsync(projectId, request.PlanningGisValidationId, token);
        await RequireInspectorAsync(projectId, request.InspectorUserId, policy, token);
        var reviewerIds = await ReviewerRecipientIdsAsync(projectId, policy, token);
        if (reviewerIds.Count == 0)
            throw Validation("No active project member has a configured CIV-CFG-011 quality reviewer role for inspection-plan approval.");
        var planEvidence = await RequireEvidenceAsync(request.PlanDocumentRecordId, request.PlanDocumentVersionId, policy.Template.TemplateCode, token);
        var now = DateTime.UtcNow;
        var checkpoint = new ProjectQualityCheckpoint
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, QaOwnerId = request.InspectorUserId,
            Title = $"Civil inspection - {RequiredText(request.Purpose, 500, "Inspection purpose")}",
            Description = $"Controlled Civil inspection proposed for {Utc(request.ScheduledAt):u}. Spatial reference: {planning.SpatialReferenceSnapshot}",
            Status = "InspectionPlanPendingApproval", DueDate = Utc(request.ScheduledAt), RequiresQaSignOff = true,
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.ProjectQualityCheckpoints.Add(checkpoint);
        var control = new ProjectCivilInspectionControl
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, QualityCheckpointId = checkpoint.Id,
            PlanningGisValidationId = planning.Id, InspectorUserId = request.InspectorUserId,
            Purpose = RequiredText(request.Purpose, 500, "Inspection purpose"), ScheduledAt = Utc(request.ScheduledAt),
            SpatialReferenceSnapshot = planning.SpatialReferenceSnapshot, BoundaryCoordinatesSnapshot = planning.BoundaryCoordinatesSnapshot,
            PlanDocumentRecordId = planEvidence.DocumentRecordId, PlanDocumentVersionId = planEvidence.Id,
            ConfigurationProfileId = policy.ProfileId, SupervisionConfigurationDecisionId = policy.SupervisionDecisionId,
            QualityConfigurationDecisionId = policy.QualityDecisionId, WorkflowDefinitionId = policy.WorkflowDefinitionId,
            EvidenceMetadataTemplateId = policy.Template.Id,
            EvidenceMetadataTemplateCodeSnapshot = policy.Template.TemplateCode, PolicyHash = policy.PolicyHash,
            Stage = CivilEngineeringInspectionStages.PendingApproval, Status = CivilEngineeringInspectionStatuses.PendingApproval,
            PlanApprovalStatus = CivilEngineeringInspectionPlanApprovalStatuses.Pending, PlanSubmittedById = UserId, PlanSubmittedAt = now,
            ClientRequestId = request.ClientRequestId, RequestHash = requestHash, CorrelationId = Correlation(correlationId),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.ProjectCivilInspectionControls.Add(control);
        var submitted = await workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.QualityTest, control.Id, policy.WorkflowDefinitionId);
        if (!submitted.ExecutionResult.Success || submitted.Outcome != WorkflowOutcome.Pending || !submitted.ExecutionResult.WorkflowInstanceId.HasValue)
            throw Conflict(submitted.ExecutionResult.Message ?? "The configured Civil inspection-plan workflow must start in a pending reviewer state.");
        control.WorkflowInstanceId = submitted.ExecutionResult.WorkflowInstanceId;
        foreach (var reviewerId in reviewerIds)
            AddNotification(control, reviewerId, "CivilInspectionPlanReview", "Civil inspection plan awaiting review", $"A Civil inspection plan for '{control.Purpose}' is awaiting independent approval.", "Normal", now, correlationId);
        AddRevision(control, CivilEngineeringAuditEventMap.CreateCivilInspection, string.Empty, Snapshot(control), null, requestHash, correlationId);
        AddAudit(control, CivilEngineeringAuditEventMap.CreateCivilInspection, null, Snapshot(control), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([control], token)).Single();
    }

    public async Task<CivilEngineeringInspectionControlDto> ProcessAsync(Guid inspectionControlId, ProcessCivilEngineeringInspectionControlRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringInspectionPolicy.ValidateProcess(request);
        if (errors.Count > 0) throw Validation(errors);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var control = await Controls(true).Include(value => value.QualityCheckpoint).Include(value => value.NonConformance).SingleOrDefaultAsync(value => value.Id == inspectionControlId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil inspection control was not found.");
        await RequireProjectAsync(control.ProjectId, token);
        var policy = await ResolveFrozenPolicyAsync(control, token);
        var mutationHash = Hash(new { inspectionControlId, request.Action, findings = Clean(request.Findings, 4000), correctiveAction = Clean(request.CorrectiveAction, 2000), reviewComment = Clean(request.ReviewComment, 2000), request.ReinspectionInspectorUserId, request.CentralDocumentRecordId, request.CentralDocumentVersionId });
        if (control.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(control.LastMutationRequestHash ?? string.Empty, mutationHash)) throw Conflict("This inspection action identifier was already used with different values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([control], token)).Single();
        }
        ApplyRowVersion(control, request.RowVersion);
        if (CivilEngineeringInspectionPolicy.IsTerminal(control.Stage)) throw Conflict("A closed Civil inspection cannot be changed.");
        var evidence = request.CentralDocumentVersionId.HasValue
            ? await RequireEvidenceAsync(request.CentralDocumentRecordId!.Value, request.CentralDocumentVersionId.Value, policy.Template.TemplateCode, token)
            : null;
        var before = Snapshot(control);
        var fromStage = control.Stage;
        var action = await ApplyActionAsync(control, request, evidence, policy, token);
        control.LastMutationClientRequestId = request.ClientRequestId;
        control.LastMutationRequestHash = mutationHash;
        control.CorrelationId = Correlation(correlationId);
        control.UpdatedAt = DateTime.UtcNow;
        control.UpdatedBy = UserName;
        control.LastModifiedById = UserId;
        AddRevision(control, action, fromStage, Snapshot(control), request.ReviewComment ?? request.CorrectiveAction ?? request.Findings, mutationHash, correlationId, before);
        AddAudit(control, action, before, Snapshot(control), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([control], token)).Single();
    }

    public async Task<IReadOnlyList<CivilEngineeringInspectionRevisionDto>> GetHistoryAsync(Guid inspectionControlId, CancellationToken token = default)
    {
        var control = await Controls(false).SingleOrDefaultAsync(value => value.Id == inspectionControlId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil inspection control was not found.");
        await RequireProjectAsync(control.ProjectId, token);
        return await db.ProjectCivilInspectionRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.InspectionControlId == inspectionControlId && !value.IsDeleted).OrderBy(value => value.CreatedAt)
            .Select(value => new CivilEngineeringInspectionRevisionDto { Id = value.Id, Action = value.Action, FromStage = value.FromStage, ToStage = value.ToStage, ActorName = value.ActorName, ActorRoles = value.ActorRoles, Reason = value.Reason, CorrelationId = value.CorrelationId, CreatedAt = value.CreatedAt }).ToListAsync(token);
    }

    private async Task<string> ApplyActionAsync(ProjectCivilInspectionControl control, ProcessCivilEngineeringInspectionControlRequest request, CentralDocumentVersion? evidence, Policy policy, CancellationToken token)
    {
        switch (request.Action)
        {
            case CivilEngineeringInspectionAction.ApprovePlan when control.Stage == CivilEngineeringInspectionStages.PendingApproval:
                await ReviewPlanAsync(control, policy, true, request.ReviewComment, token);
                control.Stage = CivilEngineeringInspectionStages.Scheduled;
                control.Status = CivilEngineeringInspectionStatuses.Scheduled;
                control.PlanApprovalStatus = CivilEngineeringInspectionPlanApprovalStatuses.Approved;
                control.PlanApprovedById = UserId;
                control.PlanApprovedAt = DateTime.UtcNow;
                control.PlanRejectionReason = null;
                control.QualityCheckpoint.Status = "Open";
                AddNotification(control, control.InspectorUserId, "CivilInspectionPlanApproved", "Civil inspection plan approved", $"The Civil inspection '{control.Purpose}' is approved and scheduled for {control.ScheduledAt:dd MMM yyyy HH:mm} UTC.", "Normal", DateTime.UtcNow, control.CorrelationId);
                return CivilEngineeringAuditEventMap.ApproveCivilInspectionPlan;

            case CivilEngineeringInspectionAction.RejectPlan when control.Stage == CivilEngineeringInspectionStages.PendingApproval:
                await ReviewPlanAsync(control, policy, false, request.ReviewComment, token);
                control.Stage = CivilEngineeringInspectionStages.Rejected;
                control.Status = CivilEngineeringInspectionStatuses.Rejected;
                control.PlanApprovalStatus = CivilEngineeringInspectionPlanApprovalStatuses.Rejected;
                control.PlanApprovedById = null;
                control.PlanApprovedAt = null;
                control.PlanRejectionReason = RequiredText(request.ReviewComment, 2000, "Plan rejection reason");
                control.QualityCheckpoint.Status = "InspectionPlanRejected";
                if (control.CreatedById.HasValue)
                    AddNotification(control, control.CreatedById.Value, "CivilInspectionPlanRejected", "Civil inspection plan rejected", $"The Civil inspection plan '{control.Purpose}' was rejected. Review the governed history for the reason.", "High", DateTime.UtcNow, control.CorrelationId);
                return CivilEngineeringAuditEventMap.RejectCivilInspectionPlan;

            case CivilEngineeringInspectionAction.RecordPassed when control.Stage == CivilEngineeringInspectionStages.Scheduled:
                RequireInspector(control, "Only the selected qualified inspector can record this inspection result.");
                if (evidence is null) throw Validation("Select current published central-DMS inspection evidence.");
                control.Findings = RequiredText(request.Findings, 4000, "Inspection findings");
                control.InspectedAt = DateTime.UtcNow;
                control.InspectionDocumentRecordId = evidence.DocumentRecordId; control.InspectionDocumentVersionId = evidence.Id;
                control.Stage = CivilEngineeringInspectionStages.Passed; control.Status = CivilEngineeringInspectionStatuses.Passed;
                control.QualityCheckpoint.Status = "InspectionPassed";
                return CivilEngineeringAuditEventMap.ApproveCivilInspection;

            case CivilEngineeringInspectionAction.RecordFailed when control.Stage == CivilEngineeringInspectionStages.Scheduled:
                RequireInspector(control, "Only the selected qualified inspector can record this inspection result.");
                if (evidence is null) throw Validation("Select current published central-DMS failed-inspection evidence.");
                control.Findings = RequiredText(request.Findings, 4000, "Inspection findings");
                control.InspectedAt = DateTime.UtcNow;
                control.InspectionDocumentRecordId = evidence.DocumentRecordId; control.InspectionDocumentVersionId = evidence.Id;
                control.Stage = CivilEngineeringInspectionStages.CorrectiveActionRequired; control.Status = CivilEngineeringInspectionStatuses.Blocked;
                control.QualityCheckpoint.Status = "InspectionFailed";
                control.NonConformance = new ProjectNonConformance
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = control.ProjectId, QualityCheckpointId = control.QualityCheckpointId,
                    OwnerId = control.InspectorUserId, Title = $"Inspection failure - {control.Purpose}", Description = control.Findings,
                    Severity = "High", Status = "Open", ReportedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
                };
                // The inspection control is an overlay over the canonical Projects record. Explicitly
                // register the dependent so the control never relies on implicit change-tracker graph
                // discovery to create a required non-conformance on a failed inspection.
                db.ProjectNonConformances.Add(control.NonConformance);
                control.NonConformanceId = control.NonConformance.Id;
                return CivilEngineeringAuditEventMap.RejectCivilInspection;

            case CivilEngineeringInspectionAction.RecordCorrectiveAction when control.Stage == CivilEngineeringInspectionStages.CorrectiveActionRequired:
                RequireInspector(control, "Only the assigned qualified inspector can record the corrective action and request reinspection.");
                if (evidence is null) throw Validation("Select current published central-DMS corrective-action evidence.");
                if (!control.NonConformanceId.HasValue || control.NonConformance is null) throw Conflict("The failed inspection has no authoritative Projects non-conformance record.");
                await RequireInspectorAsync(control.ProjectId, request.ReinspectionInspectorUserId!.Value, policy, token);
                if (request.ReinspectionInspectorUserId == control.InspectorUserId)
                    throw Conflict("The original inspector cannot perform the independently required reinspection.");
                control.CorrectiveAction = RequiredText(request.CorrectiveAction, 2000, "Corrective action");
                control.CorrectiveActionRecordedAt = DateTime.UtcNow;
                control.CorrectiveActionDocumentRecordId = evidence.DocumentRecordId; control.CorrectiveActionDocumentVersionId = evidence.Id;
                control.ReinspectionInspectorUserId = request.ReinspectionInspectorUserId;
                control.Stage = CivilEngineeringInspectionStages.ReinspectionScheduled; control.Status = CivilEngineeringInspectionStatuses.Active;
                control.NonConformance.CorrectiveAction = control.CorrectiveAction; control.NonConformance.Status = "CorrectiveActionRecorded";
                AddNotification(control, control.ReinspectionInspectorUserId.Value, "CivilInspectionReinspectionAssigned", "Civil reinspection assigned", $"An independent reinspection is assigned for '{control.Purpose}'.", "High", DateTime.UtcNow, control.CorrelationId);
                return CivilEngineeringAuditEventMap.SubmitCivilDefectReinspection;

            case CivilEngineeringInspectionAction.RecordReinspectionPassed when control.Stage == CivilEngineeringInspectionStages.ReinspectionScheduled:
                RequireReinspectionInspector(control, "Only the independently selected qualified inspector can record the reinspection result.");
                if (evidence is null) throw Validation("Select current published central-DMS reinspection evidence.");
                control.Findings = RequiredText(request.Findings, 4000, "Reinspection findings");
                control.ReinspectedAt = DateTime.UtcNow;
                control.ReinspectionDocumentRecordId = evidence.DocumentRecordId; control.ReinspectionDocumentVersionId = evidence.Id;
                control.Stage = CivilEngineeringInspectionStages.Passed; control.Status = CivilEngineeringInspectionStatuses.Passed;
                control.QualityCheckpoint.Status = "InspectionPassed";
                ResolveNonConformance(control, "Independently reinspected and passed.");
                return CivilEngineeringAuditEventMap.ApproveCivilDefectClosure;

            case CivilEngineeringInspectionAction.RecordReinspectionFailed when control.Stage == CivilEngineeringInspectionStages.ReinspectionScheduled:
                RequireReinspectionInspector(control, "Only the independently selected qualified inspector can record the reinspection result.");
                if (evidence is null) throw Validation("Select current published central-DMS reinspection evidence.");
                control.Findings = RequiredText(request.Findings, 4000, "Reinspection findings");
                control.ReinspectedAt = DateTime.UtcNow;
                control.ReinspectionDocumentRecordId = evidence.DocumentRecordId; control.ReinspectionDocumentVersionId = evidence.Id;
                control.Stage = CivilEngineeringInspectionStages.CorrectiveActionRequired; control.Status = CivilEngineeringInspectionStatuses.Blocked;
                control.QualityCheckpoint.Status = "InspectionFailed";
                if (control.NonConformance is not null) { control.NonConformance.Status = "ReinspectionFailed"; control.NonConformance.ResolutionNotes = null; control.NonConformance.ResolvedAt = null; }
                return CivilEngineeringAuditEventMap.RejectCivilDefectClosure;

            case CivilEngineeringInspectionAction.Close when control.Stage == CivilEngineeringInspectionStages.Passed:
                await RequireIndependentCloserAsync(control, policy, token);
                if (evidence is null) throw Validation("Select current published central-DMS closure evidence.");
                control.ClosureDocumentRecordId = evidence.DocumentRecordId; control.ClosureDocumentVersionId = evidence.Id;
                control.ClosedById = UserId; control.ClosedAt = DateTime.UtcNow;
                control.Stage = CivilEngineeringInspectionStages.Closed; control.Status = CivilEngineeringInspectionStatuses.Closed;
                control.QualityCheckpoint.Status = "SignedOff"; control.QualityCheckpoint.SignedOffAt = DateTime.UtcNow; control.QualityCheckpoint.SignedOffById = UserId; control.QualityCheckpoint.SignOffNotes = "Closed through governed Civil inspection control.";
                return CivilEngineeringAuditEventMap.CloseCivilInspection;

            default:
                throw Conflict("The requested inspection action is not valid for the current governed inspection stage.");
        }
    }

    private IQueryable<ProjectCivilInspectionControl> Controls(bool tracked) =>
        (tracked ? db.ProjectCivilInspectionControls : db.ProjectCivilInspectionControls.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task RequireProjectAsync(Guid projectId, CancellationToken token)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null || !await db.Projects.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted, token))
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private async Task<ProjectCivilPlanningGisValidation> RequirePlanningValidationAsync(Guid projectId, Guid id, CancellationToken token)
    {
        var value = await db.ProjectCivilPlanningGisValidations.AsNoTracking().Include(item => item.DesignCase).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted, token)
            ?? throw Validation("Select an approved current-tenant Planning/GIS site validation.");
        if (value.DesignCase.ProjectId != projectId || value.Status != CivilEngineeringPlanningGisValidationStatus.Approved)
            throw Validation("The selected Planning/GIS validation is not approved for this project.");
        return value;
    }

    private async Task<IReadOnlyList<CivilEngineeringInspectionPlanningLookupDto>> PlanningLookupsAsync(Guid projectId, CancellationToken token) =>
        await db.ProjectCivilPlanningGisValidations.AsNoTracking().Include(value => value.DesignCase).Where(value => value.TenantId == TenantId && !value.IsDeleted && value.Status == CivilEngineeringPlanningGisValidationStatus.Approved && value.DesignCase.ProjectId == projectId)
            .OrderByDescending(value => value.ReviewedAt).Select(value => new CivilEngineeringInspectionPlanningLookupDto { Id = value.Id, Label = value.DesignCase.ReferenceNumber + " - " + value.DesignCase.Title, SpatialReference = value.SpatialReferenceSnapshot }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringInspectionLookupOptionDto>> InspectorLookupsAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var memberIds = await db.ProjectMembers.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsActive && !value.IsDeleted).Select(value => value.UserId).Distinct().ToListAsync(token);
        var eligible = await db.UserRoles.AsNoTracking().Where(value => memberIds.Contains(value.UserId) && policy.Supervision.ProjectEngineerRoleIds.Contains(value.RoleId)).Select(value => value.UserId).Distinct().ToListAsync(token);
        return await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.IsActive && eligible.Contains(value.Id)).OrderBy(value => value.FirstName).ThenBy(value => value.LastName)
            .Select(value => new CivilEngineeringInspectionLookupOptionDto { Id = value.Id, Label = DisplayName(value.FirstName, value.LastName, value.UserName) }).ToListAsync(token);
    }

    private async Task RequireInspectorAsync(Guid projectId, Guid inspectorId, Policy policy, CancellationToken token)
    {
        if (inspectorId == Guid.Empty || !(await InspectorLookupsAsync(projectId, policy, token)).Any(value => value.Id == inspectorId))
            throw Validation("The selected inspector is not an active project member with a configured CIV-CFG-005 Project Engineer role.");
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, string templateCode, CancellationToken token)
    {
        if (recordId == Guid.Empty || versionId == Guid.Empty) throw Validation("Select both the central-DMS document and its current published version.");
        var version = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId, token)
            ?? throw Validation("Select a current published current-tenant central-DMS document version.");
        if (version.DocumentRecordId != recordId) throw Validation("The selected DMS version does not belong to the selected document.");
        if (!string.Equals(version.DocumentRecord.MetadataTemplateCode, templateCode, StringComparison.OrdinalIgnoreCase)) throw Validation($"The selected inspection evidence must use DMS template {templateCode}.");
        return version;
    }

    private async Task<IReadOnlyList<CivilEngineeringInspectionDocumentLookupDto>> DocumentLookupsAsync(string templateCode, CancellationToken token) =>
        await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).Where(value => value.TenantId == TenantId && value.DocumentRecord.MetadataTemplateCode == templateCode)
            .OrderBy(value => value.DocumentRecord.DocumentReference).ThenByDescending(value => value.CreatedAt).Select(value => new CivilEngineeringInspectionDocumentLookupDto { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).Take(250).ToListAsync(token);

    private async Task<Policy> ResolveEffectivePolicyAsync(DateTime at, CancellationToken token)
    {
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at)).OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No effective published Civil Engineering configuration profile exists.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one Civil Engineering configuration profile is effective.");
        return await ResolvePolicyAsync(profiles[0], null, null, token);
    }

    private async Task<Policy> ResolveFrozenPolicyAsync(ProjectCivilInspectionControl control, CancellationToken token)
    {
        var profile = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == control.ConfigurationProfileId && !value.IsDeleted, token)
            ?? throw Conflict("The frozen Civil inspection configuration profile is unavailable.");
        var policy = await ResolvePolicyAsync(profile, control.SupervisionConfigurationDecisionId, control.QualityConfigurationDecisionId, token);
        if (policy.WorkflowDefinitionId != control.WorkflowDefinitionId || policy.Template.Id != control.EvidenceMetadataTemplateId || !string.Equals(policy.Template.TemplateCode, control.EvidenceMetadataTemplateCodeSnapshot, StringComparison.OrdinalIgnoreCase) || !FixedEquals(policy.PolicyHash, control.PolicyHash))
            throw Conflict("The frozen Civil inspection configuration lineage no longer matches the control record.");
        return policy;
    }

    private async Task<Policy> ResolvePolicyAsync(CivilEngineeringConfigurationProfile profile, Guid? expectedSupervisionDecisionId, Guid? expectedQualityDecisionId, CancellationToken token)
    {
        var decisions = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProfileId == profile.Id && !value.IsDeleted && (value.ConfigurationKey == "CIV-CFG-005" || value.ConfigurationKey == "CIV-CFG-011")).ToListAsync(token);
        var supervisionDecision = decisions.SingleOrDefault(value => value.ConfigurationKey == "CIV-CFG-005") ?? throw Validation("The effective Civil configuration has no CIV-CFG-005 supervision decision.");
        var qualityDecision = decisions.SingleOrDefault(value => value.ConfigurationKey == "CIV-CFG-011") ?? throw Validation("The effective Civil configuration has no CIV-CFG-011 quality decision.");
        if ((expectedSupervisionDecisionId.HasValue && supervisionDecision.Id != expectedSupervisionDecisionId)
            || (expectedQualityDecisionId.HasValue && qualityDecision.Id != expectedQualityDecisionId))
            throw Conflict("The frozen Civil inspection configuration decisions no longer match the control record.");
        if (supervisionDecision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || supervisionDecision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || supervisionDecision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified || qualityDecision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || qualityDecision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || qualityDecision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Validation("CIV-CFG-005 and CIV-CFG-011 must be approved and verified before Civil inspections are scheduled.");
        CivilEngineeringSupervisionWorkflowValue supervision;
        CivilEngineeringQualityTestValue quality;
        try
        {
            supervision = JsonSerializer.Deserialize<CivilEngineeringSupervisionWorkflowValue>(supervisionDecision.ValueJson, JsonOptions) ?? throw new JsonException();
            quality = JsonSerializer.Deserialize<CivilEngineeringQualityTestValue>(qualityDecision.ValueJson, JsonOptions) ?? throw new JsonException();
        }
        catch (JsonException) { throw Conflict("The frozen Civil inspection configuration values are invalid."); }
        if (supervision.ProjectEngineerRoleIds.Count == 0 || quality.ReviewerRoleIds.Count == 0 || quality.EvidenceMetadataTemplateId == Guid.Empty || quality.WorkflowDefinitionId == Guid.Empty)
            throw Validation("CIV-CFG-005 must select Project Engineer roles and CIV-CFG-011 must select its shared workflow, reviewer roles, and DMS evidence template.");
        var definition = await db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == quality.WorkflowDefinitionId && !value.IsDeleted && value.IsActive
            && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !value.EntityType.IsDeleted && value.EntityType.IsActive && value.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.QualityTest, token);
        if (definition is null || !await db.WorkflowSteps.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.WorkflowDefinitionId == quality.WorkflowDefinitionId && !value.IsDeleted, token))
            throw Validation($"The CIV-CFG-011 workflow must be active, Published, contain a reviewer step, and be bound to {CivilEngineeringWorkflowBindingRegistry.QualityTest}.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == quality.EvidenceMetadataTemplateId && value.IsActive && value.PublishedAt.HasValue && !value.IsDeleted, token)
            ?? throw Validation("The selected CIV-CFG-011 DMS evidence template is unavailable or not published.");
        return new Policy(profile.Id, supervisionDecision.Id, qualityDecision.Id, quality.WorkflowDefinitionId, supervision, quality, template, Hash(new { supervision = supervisionDecision.ValueJson, quality = qualityDecision.ValueJson }));
    }

    private void RequireInspector(ProjectCivilInspectionControl control, string message)
    {
        if (control.InspectorUserId != UserId) throw new UnauthorizedAccessException(message);
    }

    private void RequireReinspectionInspector(ProjectCivilInspectionControl control, string message)
    {
        if (!control.ReinspectionInspectorUserId.HasValue || control.ReinspectionInspectorUserId.Value != UserId) throw new UnauthorizedAccessException(message);
    }

    private async Task RequireIndependentCloserAsync(ProjectCivilInspectionControl control, Policy policy, CancellationToken token)
    {
        if (control.CreatedById == UserId || control.InspectorUserId == UserId || control.ReinspectionInspectorUserId == UserId)
            throw new UnauthorizedAccessException("The maker or inspector cannot close the governed inspection.");
        var isProjectMember = await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == control.ProjectId && value.UserId == UserId && value.IsActive && !value.IsDeleted, token);
        var isQualityReviewer = await db.UserRoles.AsNoTracking().AnyAsync(value => value.UserId == UserId && policy.Quality.ReviewerRoleIds.Contains(value.RoleId), token);
        if (!isProjectMember || !isQualityReviewer) throw new UnauthorizedAccessException("Only an independent active project member with a configured CIV-CFG-011 quality reviewer role can close the inspection.");
    }

    private async Task ReviewPlanAsync(ProjectCivilInspectionControl control, Policy policy, bool approve, string? comment, CancellationToken token)
    {
        if (control.PlanApprovalStatus != CivilEngineeringInspectionPlanApprovalStatuses.Pending || !control.WorkflowInstanceId.HasValue)
            throw Conflict("Only a pending Civil inspection plan with an active shared workflow can be reviewed.");
        if (policy.Quality.RequireIndependentReview && (control.CreatedById == UserId || control.InspectorUserId == UserId))
            throw new UnauthorizedAccessException("The inspection-plan maker or selected inspector cannot provide its independent approval.");
        var isProjectMember = await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == control.ProjectId && value.UserId == UserId && value.IsActive && !value.IsDeleted, token);
        var hasReviewerRole = await db.UserRoles.AsNoTracking().AnyAsync(value => value.UserId == UserId && policy.Quality.ReviewerRoleIds.Contains(value.RoleId), token);
        if (!isProjectMember || !hasReviewerRole)
            throw new UnauthorizedAccessException("Only an active project member with a configured CIV-CFG-011 quality reviewer role can review the inspection plan.");
        if (!await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.QualityTest, control.Id, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the active Civil inspection-plan workflow approval step.");
        var result = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.QualityTest, control.Id, UserId, approve ? "Approve" : "Reject", Clean(comment, 2000));
        if (!result.ExecutionResult.Success)
            throw Conflict(result.ExecutionResult.Message ?? "The Civil inspection-plan workflow decision could not be processed.");
        if (approve && result.Outcome != WorkflowOutcome.Approved)
            throw Conflict("The shared workflow did not approve the Civil inspection plan.");
        if (!approve && result.Outcome != WorkflowOutcome.Rejected)
            throw Conflict("The shared workflow did not reject the Civil inspection plan.");
    }

    private async Task<IReadOnlyList<Guid>> ReviewerRecipientIdsAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var projectMemberIds = db.ProjectMembers.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsActive && !value.IsDeleted).Select(value => value.UserId);
        return await db.UserRoles.AsNoTracking().Where(value => projectMemberIds.Contains(value.UserId) && policy.Quality.ReviewerRoleIds.Contains(value.RoleId) && value.UserId != UserId)
            .Select(value => value.UserId).Distinct().ToListAsync(token);
    }

    private void AddNotification(ProjectCivilInspectionControl control, Guid recipientId, string notificationType, string title, string message, string priority, DateTime now, string correlationId)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RecipientId = recipientId, NotificationType = notificationType,
            Title = title, Message = message, Priority = priority, Status = "Pending", ScheduledFor = now,
            EntityType = nameof(ProjectCivilInspectionControl), EntityId = control.Id, ActionUrl = $"/development/projects/{control.ProjectId}", DeliveryMethods = "InApp",
            AdditionalData = JsonSerializer.Serialize(new { source = "CIV-REQ-FU-005", inspectionControlId = control.Id, projectId = control.ProjectId, correlationId = Correlation(correlationId) }, JsonOptions),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        });
    }

    private static void ResolveNonConformance(ProjectCivilInspectionControl control, string resolution)
    {
        if (control.NonConformance is null) return;
        control.NonConformance.Status = "Resolved";
        control.NonConformance.ResolvedAt = DateTime.UtcNow;
        control.NonConformance.ResolutionNotes = resolution;
    }

    private async Task<IReadOnlyList<CivilEngineeringInspectionControlDto>> MapAsync(IReadOnlyCollection<ProjectCivilInspectionControl> values, CancellationToken token)
    {
        if (values.Count == 0) return [];
        var userIds = values.Select(value => value.InspectorUserId).Concat(values.Where(value => value.ReinspectionInspectorUserId.HasValue).Select(value => value.ReinspectionInspectorUserId!.Value)).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => DisplayName(value.FirstName, value.LastName, value.UserName), token);
        var planningIds = values.Select(value => value.PlanningGisValidationId).Distinct().ToList();
        var planning = await db.ProjectCivilPlanningGisValidations.AsNoTracking().Include(value => value.DesignCase).Where(value => value.TenantId == TenantId && planningIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.DesignCase.ReferenceNumber + " - " + value.DesignCase.Title, token);
        return values.Select(value => new CivilEngineeringInspectionControlDto
        {
            Id = value.Id, ProjectId = value.ProjectId, QualityCheckpointId = value.QualityCheckpointId, PlanningGisValidationId = value.PlanningGisValidationId, PlanningGisValidationLabel = planning.GetValueOrDefault(value.PlanningGisValidationId, "Unavailable Planning/GIS validation"),
            InspectorUserId = value.InspectorUserId, InspectorName = users.GetValueOrDefault(value.InspectorUserId, "Unavailable inspector"), ReinspectionInspectorUserId = value.ReinspectionInspectorUserId, ReinspectionInspectorName = value.ReinspectionInspectorUserId.HasValue ? users.GetValueOrDefault(value.ReinspectionInspectorUserId.Value, "Unavailable inspector") : null,
            NonConformanceId = value.NonConformanceId, Purpose = value.Purpose, ScheduledAt = value.ScheduledAt, SpatialReferenceSnapshot = value.SpatialReferenceSnapshot, BoundaryCoordinatesSnapshot = value.BoundaryCoordinatesSnapshot, Stage = value.Stage, Status = value.Status, Findings = value.Findings, CorrectiveAction = value.CorrectiveAction,
            InspectedAt = value.InspectedAt, CorrectiveActionRecordedAt = value.CorrectiveActionRecordedAt, ReinspectedAt = value.ReinspectedAt, ClosedAt = value.ClosedAt,
            PlanDocumentRecordId = value.PlanDocumentRecordId, PlanDocumentVersionId = value.PlanDocumentVersionId, InspectionDocumentRecordId = value.InspectionDocumentRecordId, InspectionDocumentVersionId = value.InspectionDocumentVersionId,
            CorrectiveActionDocumentRecordId = value.CorrectiveActionDocumentRecordId, CorrectiveActionDocumentVersionId = value.CorrectiveActionDocumentVersionId, ReinspectionDocumentRecordId = value.ReinspectionDocumentRecordId, ReinspectionDocumentVersionId = value.ReinspectionDocumentVersionId,
            ClosureDocumentRecordId = value.ClosureDocumentRecordId, ClosureDocumentVersionId = value.ClosureDocumentVersionId,
            WorkflowDefinitionId = value.WorkflowDefinitionId, WorkflowInstanceId = value.WorkflowInstanceId, PlanApprovalStatus = value.PlanApprovalStatus,
            PlanApprovedById = value.PlanApprovedById, PlanApprovedAt = value.PlanApprovedAt, PlanRejectionReason = value.PlanRejectionReason,
            RowVersion = Convert.ToBase64String(value.RowVersion)
        }).ToList();
    }

    private void AddRevision(ProjectCivilInspectionControl control, string action, string fromStage, object after, string? reason, string requestHash, string correlationId, object? before = null)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.ProjectCivilInspectionRevisions.Add(new ProjectCivilInspectionRevision { Id = Guid.NewGuid(), TenantId = TenantId, InspectionControlId = control.Id, Action = action, FromStage = fromStage, ToStage = control.Stage, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = Clean(reason, 2000), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), RequestHash = requestHash, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }

    private void AddAudit(ProjectCivilInspectionControl control, string action, object? before, object after, string correlationId)
    {
        db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectCivilInspectionControl), ResourceId = control.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The Civil inspection changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && (sql.Number is >= 52078 and <= 52084 || sql.Number == 52162)) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting Civil inspection was detected. Refresh and retry."); }
    }

    private static void ApplyRowVersion(ProjectCivilInspectionControl control, string encoded)
    {
        try
        {
            var expected = Convert.FromBase64String(encoded);
            if (!CryptographicOperations.FixedTimeEquals(expected, control.RowVersion)) throw Conflict("The Civil inspection changed concurrently. Refresh and retry.");
        }
        catch (FormatException) { throw Validation("The Civil inspection concurrency token is invalid. Refresh and retry."); }
    }

    private static object Snapshot(ProjectCivilInspectionControl value) => new { value.Id, value.ProjectId, value.QualityCheckpointId, value.PlanningGisValidationId, value.InspectorUserId, value.ReinspectionInspectorUserId, value.NonConformanceId, value.Purpose, value.ScheduledAt, value.Stage, value.Status, value.PlanApprovalStatus, value.WorkflowDefinitionId, value.WorkflowInstanceId, value.PlanSubmittedById, value.PlanSubmittedAt, value.PlanApprovedById, value.PlanApprovedAt, value.PlanRejectionReason, value.Findings, value.CorrectiveAction, value.InspectedAt, value.CorrectiveActionRecordedAt, value.ReinspectedAt, value.ClosedAt, value.ClosedById, value.PlanDocumentRecordId, value.PlanDocumentVersionId, value.InspectionDocumentRecordId, value.InspectionDocumentVersionId, value.CorrectiveActionDocumentRecordId, value.CorrectiveActionDocumentVersionId, value.ReinspectionDocumentRecordId, value.ReinspectionDocumentVersionId, value.ClosureDocumentRecordId, value.ClosureDocumentVersionId, value.ConfigurationProfileId, value.SupervisionConfigurationDecisionId, value.QualityConfigurationDecisionId, value.EvidenceMetadataTemplateId, value.PolicyHash };
    private static string RequiredText(string? value, int max, string label) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) throw Validation($"{label} is required."); return normalized.Length <= max ? normalized : throw Validation($"{label} cannot exceed {max} characters."); }
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : throw Validation($"Text cannot exceed {max} characters.");
    private static string DisplayName(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static DateTime Utc(DateTime value) => value == default ? DateTime.UtcNow : value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static CivilEngineeringSupervisionValidationException Validation(string value) => new(value);
    private static CivilEngineeringSupervisionValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringSupervisionConflictException Conflict(string value) => new(value);
    private sealed record Policy(Guid ProfileId, Guid SupervisionDecisionId, Guid QualityDecisionId, Guid WorkflowDefinitionId, CivilEngineeringSupervisionWorkflowValue Supervision, CivilEngineeringQualityTestValue Quality, CentralDocumentMetadataTemplate Template, string PolicyHash);
}
