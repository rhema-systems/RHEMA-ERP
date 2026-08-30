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
/// Governed Projects extension for Civil weekly supervision reporting. It stores only the
/// weekly-report control record; central DMS, workflow, notification and audit owners remain authoritative.
/// </summary>
public sealed class CivilEngineeringWeeklySupervisionService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : ICivilEngineeringWeeklySupervisionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringWeeklySupervisionLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        return new CivilEngineeringWeeklySupervisionLookupsDto
        {
            ActivityCategories = await db.ProjectCatalogEntries.AsNoTracking().Where(value => value.TenantId == TenantId && policy.Value.ActivityCategoryIds.Contains(value.Id)
                    && value.CatalogType == "civil-weekly-activity-categories" && value.IsActive && !value.IsDeleted
                    && (!value.EffectiveFrom.HasValue || value.EffectiveFrom <= DateTime.UtcNow) && (!value.EffectiveTo.HasValue || value.EffectiveTo >= DateTime.UtcNow))
                .OrderBy(value => value.SortOrder).ThenBy(value => value.Name).Select(value => new CivilEngineeringWeeklySupervisionLookupDto { Id = value.Id, Label = value.Code + " - " + value.Name }).ToListAsync(token),
            Contractors = await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive && !value.IsBlacklisted)
                .OrderBy(value => value.PartnerName).Select(value => new CivilEngineeringWeeklySupervisionLookupDto { Id = value.Id, Label = value.PartnerName }).Take(250).ToListAsync(token),
            Milestones = await MilestoneLookupsAsync(projectId, token),
            Risks = await RiskLookupsAsync(projectId, token),
            Issues = await IssueLookupsAsync(projectId, token),
            Dependencies = await DependencyLookupsAsync(projectId, token),
            RecoveryOwners = await RecoveryOwnerLookupsAsync(projectId, token),
            Documents = await DocumentLookupsAsync(policy.Template.TemplateCode, token),
            ActorTypes = CivilEngineeringWeeklySupervisionActorTypes.All,
            EvidenceRoles = CivilEngineeringWeeklySupervisionEvidenceRoles.All,
            SiteStatuses = CivilEngineeringWeeklySupervisionSiteStatuses.All,
            MinimumPhotoCount = policy.Value.MinimumPhotoCount,
            DueDay = policy.Value.DueDay.ToString()
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionReportDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var reports = await Reports(false).Where(value => value.ProjectId == projectId)
            .Include(value => value.Activities).Include(value => value.Evidence).Include(value => value.Reviews)
            .OrderByDescending(value => value.WeekStart).ThenByDescending(value => value.CreatedAt).ToListAsync(token);
        return await MapAsync(reports, token);
    }

    public async Task<CivilEngineeringWeeklySupervisionReportDto> CreateAsync(Guid projectId, CreateCivilEngineeringWeeklySupervisionReportRequest request, string correlationId, CancellationToken token = default)
    {
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var errors = CivilEngineeringWeeklySupervisionPolicy.ValidateCreate(request, policy.Value, DateTime.UtcNow);
        if (errors.Count > 0) throw Validation(errors);
        var weekStart = request.WeekStart.Date;
        var requestHash = Hash(new
        {
            projectId, weekStart, request.ProjectMilestoneId, request.ProjectRiskId, request.ProjectIssueId, request.ProjectTaskDependencyId,
            request.OverallProgressPercent, siteStatus = request.SiteStatus?.Trim() ?? string.Empty, delayReason = Clean(request.DelayReason, 2000), recoveryAction = Clean(request.RecoveryAction, 2000), request.RecoveryOwnerUserId, recoveryDueDate = request.RecoveryDueDate?.ToUniversalTime(),
            request.MaterialUsageSummary, request.SafetyNotes, request.TestSummary,
            activities = request.Activities.Select(value => new { value.ActivityCategoryId, value.ActorType, value.ContractorBusinessPartnerId, value.Description, value.ProgressPercent }),
            evidence = request.Evidence.Select(value => new { value.CentralDocumentRecordId, value.CentralDocumentVersionId, value.EvidenceRole })
        });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Reports(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different weekly-report values.");
            await transaction.CommitAsync(token);
            return await GetAsync(retry.Id, token);
        }

        policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        errors = CivilEngineeringWeeklySupervisionPolicy.ValidateCreate(request, policy.Value, DateTime.UtcNow);
        if (errors.Count > 0) throw Validation(errors);
        await RequireProjectAsync(projectId, token);
        var appointment = await RequireCurrentProjectEngineerAsync(projectId, token);
        var milestone = await RequireProgressReferencesAsync(projectId, request, token);
        var activities = await RequireActivitiesAsync(request.Activities, policy.Value, token);
        var evidence = await RequireEvidenceAsync(request.Evidence, policy.Template.TemplateCode, token);
        var now = DateTime.UtcNow;
        var isProgressCorrection = await IsProgressCorrectionAsync(projectId, weekStart, request.OverallProgressPercent!.Value, token);
        if (isProgressCorrection && !CivilEngineeringWeeklySupervisionSiteStatuses.RequiresRecoveryAction(request.SiteStatus))
            throw Validation("A reduced progress percentage must be recorded as At risk, Delayed, or Stopped with a controlled recovery action for management workflow approval.");
        ProjectActionItem? recoveryAction = null;
        if (CivilEngineeringWeeklySupervisionSiteStatuses.RequiresRecoveryAction(request.SiteStatus))
        {
            recoveryAction = new ProjectActionItem
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId,
                Title = $"Civil recovery action - week of {weekStart:yyyy-MM-dd}",
                Description = RequiredText(request.RecoveryAction, 2000, "Recovery action"), OwnerId = request.RecoveryOwnerUserId,
                DueDate = request.RecoveryDueDate!.Value.ToUniversalTime(), Status = "Open",
                Priority = request.SiteStatus is CivilEngineeringWeeklySupervisionSiteStatuses.Delayed or CivilEngineeringWeeklySupervisionSiteStatuses.Stopped ? "High" : "Normal",
                CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            db.ProjectActionItems.Add(recoveryAction);
        }
        var report = new ProjectCivilWeeklySupervisionReport
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ProjectEngineerAssignmentId = appointment.Id,
            ProjectMilestoneId = request.ProjectMilestoneId, MilestoneTargetDateSnapshot = milestone.TargetDate, MilestoneActualDateSnapshot = milestone.ActualDate, ProjectRiskId = request.ProjectRiskId, ProjectIssueId = request.ProjectIssueId, ProjectTaskDependencyId = request.ProjectTaskDependencyId, HasGovernedProgressControl = true,
            WeekStart = weekStart, WeekEnd = weekStart.AddDays(6), OverallProgressPercent = request.OverallProgressPercent, SiteStatus = request.SiteStatus.Trim(),
            DelayReason = Clean(request.DelayReason, 2000), RecoveryActionItemId = recoveryAction?.Id, IsProgressCorrection = isProgressCorrection,
            MaterialUsageSummary = Clean(request.MaterialUsageSummary, 4000), SafetyNotes = Clean(request.SafetyNotes, 4000), TestSummary = Clean(request.TestSummary, 4000),
            DueAt = CalculateDueAt(weekStart, policy.Value.DueDay), ConfigurationProfileId = policy.ProfileId, ConfigurationDecisionId = policy.DecisionId,
            WorkflowDefinitionId = policy.WorkflowDefinitionId, EvidenceMetadataTemplateId = policy.Template.Id, EvidenceMetadataTemplateCodeSnapshot = policy.Template.TemplateCode,
            PolicyHash = policy.PolicyHash, ClientRequestId = request.ClientRequestId, RequestHash = requestHash, CorrelationId = Correlation(correlationId),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        if (recoveryAction is not null && request.RecoveryOwnerUserId.HasValue)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(), TenantId = TenantId, RecipientId = request.RecoveryOwnerUserId.Value,
                NotificationType = "CivilWeeklyRecoveryActionAssigned", Title = "Civil recovery action assigned",
                Message = $"A recovery action is assigned for the Civil weekly report beginning {weekStart:dd MMM yyyy}.",
                Priority = request.SiteStatus is CivilEngineeringWeeklySupervisionSiteStatuses.Delayed or CivilEngineeringWeeklySupervisionSiteStatuses.Stopped ? "High" : "Normal",
                Status = "Pending", ScheduledFor = now, EntityType = nameof(ProjectCivilWeeklySupervisionReport), EntityId = report.Id,
                ActionUrl = $"/development/projects/{projectId}", DeliveryMethods = "InApp",
                AdditionalData = JsonSerializer.Serialize(new { source = "CIV-REQ-FU-006", reportId = report.Id, recoveryActionId = recoveryAction.Id, projectId }, JsonOptions),
                CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            });
        }
        report.Activities = activities.Select((value, index) => new ProjectCivilWeeklySupervisionActivity
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ReportId = report.Id, Sequence = index + 1, ActivityCategoryId = value.ActivityCategoryId,
            ActorType = value.ActorType.Trim(), ContractorBusinessPartnerId = value.ContractorBusinessPartnerId, Description = value.Description.Trim(), ProgressPercent = value.ProgressPercent,
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        }).ToList();
        report.Evidence = evidence.Select(value => new ProjectCivilWeeklySupervisionEvidence
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ReportId = report.Id, CentralDocumentRecordId = value.Document.DocumentRecordId, CentralDocumentVersionId = value.Document.Id,
            EvidenceRole = value.Role, LinkedByUserId = UserId, LinkedAt = now, CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        }).ToList();
        db.ProjectCivilWeeklySupervisionReports.Add(report);
        var submitted = await workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.WeeklyReport, report.Id, policy.WorkflowDefinitionId);
        if (!submitted.ExecutionResult.Success || submitted.Outcome != WorkflowOutcome.Pending)
            throw Conflict(submitted.ExecutionResult.Message ?? "The configured weekly-report workflow must start in a pending SCE review state.");
        workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.WeeklyReport).ApplySubmitOutcome(report, submitted.Outcome, UserId);
        report.WorkflowInstanceId = submitted.ExecutionResult.WorkflowInstanceId;
        AddRevision(report, CivilEngineeringAuditEventMap.CreateWeeklySupervisionReport, null, Snapshot(report), null, correlationId);
        AddAudit(report, CivilEngineeringAuditEventMap.CreateWeeklySupervisionReport, null, Snapshot(report), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAsync(report.Id, token);
    }

    public async Task<CivilEngineeringWeeklySupervisionReportDto> ProcessAsync(Guid reportId, ProcessCivilEngineeringWeeklySupervisionReportRequest request, string correlationId, CancellationToken token = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var report = await Reports(true).Include(value => value.Reviews).SingleOrDefaultAsync(value => value.Id == reportId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil weekly supervision report was not found.");
        var policy = await ResolveFrozenPolicyAsync(report, token);
        var errors = CivilEngineeringWeeklySupervisionPolicy.ValidateReview(request);
        if (errors.Count > 0) throw Validation(errors);
        var mutationHash = Hash(new { reportId, request.Approve, comment = request.Comment.Trim(), request.CentralDocumentRecordId, request.CentralDocumentVersionId });
        if (report.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(report.LastMutationRequestHash ?? string.Empty, mutationHash)) throw Conflict("This review request identifier was already used with different values.");
            await transaction.CommitAsync(token);
            return await GetAsync(report.Id, token);
        }
        ApplyRowVersion(report, request.RowVersion);
        if (report.Status != "PendingApproval" || report.ApprovalStatus != "Pending") throw Conflict("Only a pending weekly supervision report can be reviewed.");
        if (report.CreatedById == UserId) throw Conflict("The Project Engineer who prepared the weekly report cannot approve or return it.");
        if (!report.WorkflowInstanceId.HasValue || !await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.WeeklyReport, report.Id, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the active weekly-report workflow review step.");
        var evidence = request.CentralDocumentVersionId.HasValue
            ? await RequireEvidenceAsync([new CivilEngineeringWeeklySupervisionEvidenceRequest { CentralDocumentRecordId = request.CentralDocumentRecordId!.Value, CentralDocumentVersionId = request.CentralDocumentVersionId.Value, EvidenceRole = CivilEngineeringWeeklySupervisionEvidenceRoles.Review }], policy.Template.TemplateCode, token)
            : [];
        var reviewEvidence = evidence.Count == 0 ? null : evidence[0].Document;
        var before = Snapshot(report);
        var result = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.WeeklyReport, report.Id, UserId, request.Approve ? "Approve" : "Reject", request.Comment.Trim());
        if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The weekly-report workflow decision could not be processed.");
        if (request.Approve && result.Outcome is not (WorkflowOutcome.Pending or WorkflowOutcome.Approved)) throw Conflict("The shared workflow did not advance the weekly report.");
        if (!request.Approve && result.Outcome != WorkflowOutcome.Rejected) throw Conflict("The shared workflow did not reject the weekly report.");
        workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.WeeklyReport).ApplyApprovalOutcome(report, result.Outcome, UserId, request.Comment.Trim());
        if (result.Outcome == WorkflowOutcome.Approved && report.IsProgressCorrection && !report.ProgressCorrectionDecisionId.HasValue)
        {
            // The final shared-workflow approver is the management approval for a downward
            // progress correction. Persist the decision in the canonical Projects decision register,
            // rather than inventing a separate Civil decision store.
            var decision = new ProjectDecision
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = report.ProjectId,
                Title = $"Civil progress correction - week of {report.WeekStart:yyyy-MM-dd}",
                DecisionDate = DateTime.UtcNow, ApproverId = UserId, Rationale = request.Comment.Trim(),
                ImpactSummary = report.DelayReason, Status = "Approved", ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            };
            db.ProjectDecisions.Add(decision);
            report.ProgressCorrectionDecisionId = decision.Id;
        }
        report.LastMutationClientRequestId = request.ClientRequestId; report.LastMutationRequestHash = mutationHash; report.CorrelationId = Correlation(correlationId);
        report.UpdatedAt = DateTime.UtcNow; report.UpdatedBy = UserName; report.LastModifiedById = UserId;
        db.ProjectCivilWeeklySupervisionReviews.Add(new ProjectCivilWeeklySupervisionReview
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ReportId = report.Id, ClientRequestId = request.ClientRequestId, Sequence = report.Reviews.Count + 1,
            Action = request.Approve ? "Approve" : "Return", Outcome = result.Outcome.ToString(), Comment = request.Comment.Trim(), ActorUserId = UserId,
            CentralDocumentRecordId = reviewEvidence?.DocumentRecordId, CentralDocumentVersionId = reviewEvidence?.Id,
            CorrelationId = Correlation(correlationId), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
        var action = request.Approve ? CivilEngineeringAuditEventMap.ApproveWeeklySupervisionReport : CivilEngineeringAuditEventMap.RejectWeeklySupervisionReport;
        AddRevision(report, action, before, Snapshot(report), request.Comment, correlationId);
        AddAudit(report, action, before, Snapshot(report), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAsync(report.Id, token);
    }

    public async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionReportDto>> EscalateOverdueAsync(Guid projectId, Guid clientRequestId, string correlationId, CancellationToken token = default)
    {
        if (clientRequestId == Guid.Empty) throw Validation("A client request identifier is required to escalate overdue weekly reports.");
        await RequireProjectAsync(projectId, token);
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var reports = await Reports(true).Where(value => value.ProjectId == projectId && value.Status == "PendingApproval" && value.ApprovalStatus == "Pending" && value.DueAt < now && !value.EscalatedAt.HasValue)
            .Include(value => value.Reviews).OrderBy(value => value.DueAt).ToListAsync(token);
        foreach (var report in reports)
        {
            var policy = await ResolveFrozenPolicyAsync(report, token);
            var recipients = await EscalationRecipientsAsync(projectId, policy, token);
            if (recipients.Count == 0) throw Validation("CIV-CFG-006 has no active project-member escalation recipient for this overdue report.");
            var before = Snapshot(report);
            report.EscalatedAt = now; report.UpdatedAt = now; report.UpdatedBy = UserName; report.LastModifiedById = UserId; report.CorrelationId = Correlation(correlationId);
            foreach (var recipientId in recipients)
            {
                db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, RecipientId = recipientId, NotificationType = "CivilWeeklySupervisionReportOverdue",
                    Title = "Overdue Civil weekly supervision report", Message = $"The weekly supervision report for {report.WeekStart:dd MMM yyyy} is overdue and awaiting workflow review.",
                    Priority = "High", Status = "Pending", ScheduledFor = now, EntityType = nameof(ProjectCivilWeeklySupervisionReport), EntityId = report.Id,
                    ActionUrl = $"/development/projects/{projectId}", DeliveryMethods = "InApp",
                    AdditionalData = JsonSerializer.Serialize(new { source = "CIV-0205", reportId = report.Id, projectId, clientRequestId, correlationId = Correlation(correlationId) }, JsonOptions),
                    CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
                });
            }
            AddRevision(report, CivilEngineeringAuditEventMap.EscalateWeeklySupervisionReport, before, Snapshot(report), "Overdue escalation queued for configured Civil recipients.", correlationId);
            AddAudit(report, CivilEngineeringAuditEventMap.EscalateWeeklySupervisionReport, before, Snapshot(report), correlationId);
        }
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await ListAsync(projectId, token);
    }

    private IQueryable<ProjectCivilWeeklySupervisionReport> Reports(bool tracked) =>
        (tracked ? db.ProjectCivilWeeklySupervisionReports : db.ProjectCivilWeeklySupervisionReports.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<CivilEngineeringWeeklySupervisionReportDto> GetAsync(Guid id, CancellationToken token)
    {
        var report = await Reports(false).Where(value => value.Id == id).Include(value => value.Activities).Include(value => value.Evidence).Include(value => value.Reviews).SingleOrDefaultAsync(token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The Civil weekly supervision report was not found.");
        return (await MapAsync([report], token)).Single();
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken token)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
        return await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted, token);
    }

    private async Task<ProjectCivilProjectEngineerAssignment> RequireCurrentProjectEngineerAsync(Guid projectId, CancellationToken token) =>
        await db.ProjectCivilProjectEngineerAssignments.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.AssignedUserId == UserId && value.IsActive && !value.IsDeleted, token)
        ?? throw new UnauthorizedAccessException("Only the current Project Engineer can prepare a governed weekly supervision report.");

    private async Task<Policy> ResolvePolicyAsync(DateTime at, CancellationToken token)
    {
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No effective published Civil Engineering configuration profile exists.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one Civil Engineering configuration profile is effective.");
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profiles[0].Id && value.ConfigurationKey == "CIV-CFG-006" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-006 weekly-report decision.");
        return await ResolvePolicyAsync(profiles[0].Id, decision, token);
    }

    private async Task<Policy> ResolveFrozenPolicyAsync(ProjectCivilWeeklySupervisionReport report, CancellationToken token)
    {
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == report.ConfigurationDecisionId && value.ProfileId == report.ConfigurationProfileId && value.ConfigurationKey == "CIV-CFG-006" && !value.IsDeleted, token)
            ?? throw Conflict("The weekly-report configuration lineage is no longer available.");
        var policy = await ResolvePolicyAsync(report.ConfigurationProfileId, decision, token);
        if (policy.WorkflowDefinitionId != report.WorkflowDefinitionId || policy.Template.Id != report.EvidenceMetadataTemplateId || !string.Equals(policy.Template.TemplateCode, report.EvidenceMetadataTemplateCodeSnapshot, StringComparison.OrdinalIgnoreCase) || !FixedEquals(policy.PolicyHash, report.PolicyHash))
            throw Conflict("The weekly-report frozen configuration does not match its approved lineage.");
        return policy;
    }

    private async Task<Policy> ResolvePolicyAsync(Guid profileId, CivilEngineeringConfigurationDecision decision, CancellationToken token)
    {
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Validation("CIV-CFG-006 is not approved and verified.");
        CivilEngineeringWeeklyReportValue controls;
        try { controls = JsonSerializer.Deserialize<CivilEngineeringWeeklyReportValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-006 contains invalid weekly-report control data."); }
        if (controls.WorkflowDefinitionId == Guid.Empty || controls.MetadataTemplateId == Guid.Empty || controls.ActivityCategoryIds.Count == 0 || controls.EscalationRoleIds.Count == 0)
            throw Validation("CIV-CFG-006 must select the weekly workflow, DMS template, activity categories and escalation roles.");
        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == controls.WorkflowDefinitionId && !item.IsDeleted && item.IsActive && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive && item.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.WeeklyReport, token);
        if (workflowDefinition is null || await db.WorkflowSteps.AsNoTracking().CountAsync(item => item.TenantId == TenantId && item.WorkflowDefinitionId == controls.WorkflowDefinitionId && !item.IsDeleted, token) < 2)
            throw Validation($"The CIV-CFG-006 weekly-report workflow must be active, Published, bound to {CivilEngineeringWorkflowBindingRegistry.WeeklyReport}, and contain SCE and HOD review steps.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == controls.MetadataTemplateId && item.IsActive && item.PublishedAt.HasValue && !item.IsDeleted, token)
            ?? throw Validation("The CIV-CFG-006 weekly-report DMS template is unavailable.");
        return new Policy(profileId, decision.Id, controls.WorkflowDefinitionId, controls, template, Hash(decision.ValueJson));
    }

    private async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionActivityRequest>> RequireActivitiesAsync(IReadOnlyCollection<CivilEngineeringWeeklySupervisionActivityRequest> activities, CivilEngineeringWeeklyReportValue policy, CancellationToken token)
    {
        var categoryIds = activities.Select(value => value.ActivityCategoryId).Distinct().ToList();
        var validCategoryIds = await db.ProjectCatalogEntries.AsNoTracking().Where(value => value.TenantId == TenantId && categoryIds.Contains(value.Id) && policy.ActivityCategoryIds.Contains(value.Id) && value.CatalogType == "civil-weekly-activity-categories" && value.IsActive && !value.IsDeleted && (!value.EffectiveFrom.HasValue || value.EffectiveFrom <= DateTime.UtcNow) && (!value.EffectiveTo.HasValue || value.EffectiveTo >= DateTime.UtcNow)).Select(value => value.Id).ToListAsync(token);
        if (validCategoryIds.Count != categoryIds.Count) throw Validation("One or more selected activity categories are not active, configured Civil weekly-report categories.");
        var contractorIds = activities.Where(value => value.ContractorBusinessPartnerId.HasValue).Select(value => value.ContractorBusinessPartnerId!.Value).Distinct().ToList();
        if (contractorIds.Count > 0)
        {
            var valid = await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId && contractorIds.Contains(value.Id) && !value.IsDeleted && value.IsActive && !value.IsBlacklisted).Select(value => value.Id).ToListAsync(token);
            if (valid.Count != contractorIds.Count) throw Validation("One or more selected contractor Business Partners are inactive, blacklisted, or outside this tenant.");
        }
        return activities.ToList();
    }

    private async Task<IReadOnlyList<(CentralDocumentVersion Document, string Role)>> RequireEvidenceAsync(IEnumerable<CivilEngineeringWeeklySupervisionEvidenceRequest> input, string templateCode, CancellationToken token)
    {
        var result = new List<(CentralDocumentVersion Document, string Role)>();
        foreach (var item in input)
        {
            var document = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == item.CentralDocumentVersionId, token)
                ?? throw Validation("Select a current Published current-tenant central-DMS evidence version.");
            if (document.DocumentRecordId != item.CentralDocumentRecordId || !string.Equals(document.DocumentRecord.MetadataTemplateCode, templateCode, StringComparison.OrdinalIgnoreCase))
                throw Validation("The selected DMS evidence does not belong to the CIV-CFG-006 weekly-report metadata template.");
            result.Add((document, item.EvidenceRole.Trim()));
        }
        return result;
    }

    private async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionDocumentLookupDto>> DocumentLookupsAsync(string templateCode, CancellationToken token) => await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
        .Where(value => value.TenantId == TenantId && value.DocumentRecord.MetadataTemplateCode == templateCode).OrderBy(value => value.DocumentRecord.DocumentReference)
        .Select(value => new CivilEngineeringWeeklySupervisionDocumentLookupDto { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<Guid>> EscalationRecipientsAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var memberIds = await db.ProjectMembers.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsActive && !value.IsDeleted).Select(value => value.UserId).ToListAsync(token);
        return await (
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            where user.TenantId == TenantId
                  && user.IsActive
                  && memberIds.Contains(user.Id)
                  && policy.Value.EscalationRoleIds.Contains(userRole.RoleId)
            select user.Id).Distinct().ToListAsync(token);
    }

    private async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto>> MilestoneLookupsAsync(Guid projectId, CancellationToken token) =>
        await db.ProjectMilestones.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted)
            .OrderBy(value => value.TargetDate).ThenBy(value => value.Title)
            .Select(value => new CivilEngineeringWeeklySupervisionLookupDto { Id = value.Id, Label = value.Title }).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto>> RiskLookupsAsync(Guid projectId, CancellationToken token) =>
        await db.ProjectRisks.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted && value.Status != "Closed" && value.Status != "Resolved")
            .OrderByDescending(value => value.Exposure).ThenBy(value => value.Title)
            .Select(value => new CivilEngineeringWeeklySupervisionLookupDto { Id = value.Id, Label = value.Title + " · " + value.Status }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto>> IssueLookupsAsync(Guid projectId, CancellationToken token) =>
        await db.ProjectIssues.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted && value.Status != "Closed" && value.Status != "Resolved")
            .OrderBy(value => value.TargetResolutionDate).ThenBy(value => value.Title)
            .Select(value => new CivilEngineeringWeeklySupervisionLookupDto { Id = value.Id, Label = value.Title + " · " + value.Status }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto>> DependencyLookupsAsync(Guid projectId, CancellationToken token) =>
        await (
            from dependency in db.ProjectTaskDependencies.AsNoTracking()
            join predecessor in db.ProjectWorkItems.AsNoTracking() on dependency.PredecessorWorkItemId equals predecessor.Id
            join successor in db.ProjectWorkItems.AsNoTracking() on dependency.SuccessorWorkItemId equals successor.Id
            where dependency.TenantId == TenantId && dependency.ProjectId == projectId && !dependency.IsDeleted
                  && predecessor.TenantId == TenantId && predecessor.ProjectId == projectId && !predecessor.IsDeleted
                  && successor.TenantId == TenantId && successor.ProjectId == projectId && !successor.IsDeleted
            orderby predecessor.Title, successor.Title
            select new CivilEngineeringWeeklySupervisionLookupDto { Id = dependency.Id, Label = predecessor.Title + " → " + successor.Title }
        ).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto>> RecoveryOwnerLookupsAsync(Guid projectId, CancellationToken token) =>
        await (
            from member in db.ProjectMembers.AsNoTracking()
            join user in db.Users.AsNoTracking() on member.UserId equals user.Id
            where member.TenantId == TenantId && member.ProjectId == projectId && member.IsActive && !member.IsDeleted
                  && user.TenantId == TenantId && user.IsActive
            orderby user.FirstName, user.LastName, user.UserName
            select new CivilEngineeringWeeklySupervisionLookupDto { Id = user.Id, Label = ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim() == "" ? user.UserName : ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim() }
        ).Distinct().Take(250).ToListAsync(token);

    private async Task<ProjectMilestone> RequireProgressReferencesAsync(Guid projectId, CreateCivilEngineeringWeeklySupervisionReportRequest request, CancellationToken token)
    {
        var milestone = await db.ProjectMilestones.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.Id == request.ProjectMilestoneId && !value.IsDeleted, token);
        if (milestone is null) throw Validation("The selected project milestone is unavailable for this project.");
        if (request.ProjectRiskId.HasValue && !await db.ProjectRisks.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.Id == request.ProjectRiskId && !value.IsDeleted && value.Status != "Closed" && value.Status != "Resolved", token))
            throw Validation("The selected open project risk is unavailable for this project.");
        if (request.ProjectIssueId.HasValue && !await db.ProjectIssues.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.Id == request.ProjectIssueId && !value.IsDeleted && value.Status != "Closed" && value.Status != "Resolved", token))
            throw Validation("The selected open project issue is unavailable for this project.");
        if (request.ProjectTaskDependencyId.HasValue && !await db.ProjectTaskDependencies.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.Id == request.ProjectTaskDependencyId && !value.IsDeleted, token))
            throw Validation("The selected project dependency is unavailable for this project.");
        if (CivilEngineeringWeeklySupervisionSiteStatuses.RequiresRecoveryAction(request.SiteStatus))
        {
            var recoveryOwner = await (
                from member in db.ProjectMembers.AsNoTracking()
                join user in db.Users.AsNoTracking() on member.UserId equals user.Id
                where member.TenantId == TenantId && member.ProjectId == projectId && member.UserId == request.RecoveryOwnerUserId
                      && member.IsActive && !member.IsDeleted && user.TenantId == TenantId && user.IsActive
                select user.Id).AnyAsync(token);
            if (!recoveryOwner) throw Validation("The selected recovery-action owner is not an active member of this project.");
        }
        return milestone;
    }

    private async Task<bool> IsProgressCorrectionAsync(Guid projectId, DateTime weekStart, decimal proposedProgress, CancellationToken token)
    {
        var priorProgress = await Reports(false).Where(value => value.ProjectId == projectId && value.WeekStart < weekStart && value.Status == "Approved" && value.ApprovalStatus == "Approved" && value.OverallProgressPercent.HasValue)
            .OrderByDescending(value => value.WeekStart).Select(value => value.OverallProgressPercent).FirstOrDefaultAsync(token);
        return priorProgress.HasValue && proposedProgress < priorProgress.Value;
    }

    private async Task<IReadOnlyList<CivilEngineeringWeeklySupervisionReportDto>> MapAsync(IReadOnlyCollection<ProjectCivilWeeklySupervisionReport> reports, CancellationToken token)
    {
        if (reports.Count == 0) return [];
        var assignmentIds = reports.Select(value => value.ProjectEngineerAssignmentId).Distinct().ToList();
        var assignments = await db.ProjectCivilProjectEngineerAssignments.AsNoTracking().Where(value => value.TenantId == TenantId && assignmentIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var userIds = assignments.Values.Select(value => value.AssignedUserId).Concat(reports.SelectMany(value => value.Reviews.Select(review => review.ActorUserId))).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => DisplayName(value.FirstName, value.LastName, value.UserName), token);
        var categoryIds = reports.SelectMany(value => value.Activities.Select(activity => activity.ActivityCategoryId)).Distinct().ToList();
        var categories = await db.ProjectCatalogEntries.AsNoTracking().Where(value => value.TenantId == TenantId && categoryIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Code + " - " + value.Name, token);
        var contractorIds = reports.SelectMany(value => value.Activities.Where(activity => activity.ContractorBusinessPartnerId.HasValue).Select(activity => activity.ContractorBusinessPartnerId!.Value)).Distinct().ToList();
        var contractors = await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId && contractorIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.PartnerName, token);
        var milestoneIds = reports.Where(value => value.ProjectMilestoneId.HasValue).Select(value => value.ProjectMilestoneId!.Value).Distinct().ToList();
        var milestones = await db.ProjectMilestones.AsNoTracking().Where(value => value.TenantId == TenantId && milestoneIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Title, token);
        var riskIds = reports.Where(value => value.ProjectRiskId.HasValue).Select(value => value.ProjectRiskId!.Value).Distinct().ToList();
        var risks = await db.ProjectRisks.AsNoTracking().Where(value => value.TenantId == TenantId && riskIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Title, token);
        var issueIds = reports.Where(value => value.ProjectIssueId.HasValue).Select(value => value.ProjectIssueId!.Value).Distinct().ToList();
        var issues = await db.ProjectIssues.AsNoTracking().Where(value => value.TenantId == TenantId && issueIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Title, token);
        var dependencyIds = reports.Where(value => value.ProjectTaskDependencyId.HasValue).Select(value => value.ProjectTaskDependencyId!.Value).Distinct().ToList();
        var dependencies = await DependencyLabelsAsync(dependencyIds, token);
        var actionIds = reports.Where(value => value.RecoveryActionItemId.HasValue).Select(value => value.RecoveryActionItemId!.Value).Distinct().ToList();
        var recoveryActions = await db.ProjectActionItems.AsNoTracking().Where(value => value.TenantId == TenantId && actionIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Title, token);
        var decisionIds = reports.Where(value => value.ProgressCorrectionDecisionId.HasValue).Select(value => value.ProgressCorrectionDecisionId!.Value).Distinct().ToList();
        var correctionDecisions = await db.ProjectDecisions.AsNoTracking().Where(value => value.TenantId == TenantId && decisionIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Title, token);
        var versionIds = reports.SelectMany(value => value.Evidence.Select(evidence => evidence.CentralDocumentVersionId))
            .Concat(reports.SelectMany(value => value.Reviews.Where(review => review.CentralDocumentVersionId.HasValue).Select(review => review.CentralDocumentVersionId!.Value)))
            .Distinct().ToList();
        var documents = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(value => value.TenantId == TenantId && versionIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var now = DateTime.UtcNow;
        return reports.Select(value => new CivilEngineeringWeeklySupervisionReportDto
        {
            Id = value.Id, ProjectId = value.ProjectId, ProjectEngineerAssignmentId = value.ProjectEngineerAssignmentId,
            ProjectEngineerName = assignments.TryGetValue(value.ProjectEngineerAssignmentId, out var assignment) && users.TryGetValue(assignment.AssignedUserId, out var engineer) ? engineer : "Unavailable Project Engineer",
            ProjectMilestoneId = value.ProjectMilestoneId, ProjectMilestoneTitle = value.ProjectMilestoneId.HasValue ? milestones.GetValueOrDefault(value.ProjectMilestoneId.Value, "Unavailable milestone") : "Legacy report without a governed milestone", MilestoneTargetDateSnapshot = value.MilestoneTargetDateSnapshot, MilestoneActualDateSnapshot = value.MilestoneActualDateSnapshot, ProjectRiskId = value.ProjectRiskId, ProjectRiskTitle = value.ProjectRiskId.HasValue ? risks.GetValueOrDefault(value.ProjectRiskId.Value) : null, ProjectIssueId = value.ProjectIssueId, ProjectIssueTitle = value.ProjectIssueId.HasValue ? issues.GetValueOrDefault(value.ProjectIssueId.Value) : null, ProjectTaskDependencyId = value.ProjectTaskDependencyId, ProjectTaskDependencyLabel = value.ProjectTaskDependencyId.HasValue ? dependencies.GetValueOrDefault(value.ProjectTaskDependencyId.Value) : null,
            WeekStart = value.WeekStart, WeekEnd = value.WeekEnd, OverallProgressPercent = value.OverallProgressPercent, SiteStatus = value.SiteStatus, DelayReason = value.DelayReason, RecoveryActionItemId = value.RecoveryActionItemId, RecoveryActionTitle = value.RecoveryActionItemId.HasValue ? recoveryActions.GetValueOrDefault(value.RecoveryActionItemId.Value) : null, IsProgressCorrection = value.IsProgressCorrection, ProgressCorrectionDecisionId = value.ProgressCorrectionDecisionId, ProgressCorrectionDecisionTitle = value.ProgressCorrectionDecisionId.HasValue ? correctionDecisions.GetValueOrDefault(value.ProgressCorrectionDecisionId.Value) : null, MaterialUsageSummary = value.MaterialUsageSummary, SafetyNotes = value.SafetyNotes, TestSummary = value.TestSummary,
            DueAt = value.DueAt, IsOverdue = value.Status == "PendingApproval" && value.DueAt < now, EscalatedAt = value.EscalatedAt, Status = value.Status, ApprovalStatus = value.ApprovalStatus,
            RejectionReason = value.RejectionReason, WorkflowInstanceId = value.WorkflowInstanceId, RowVersion = Convert.ToBase64String(value.RowVersion),
            Activities = value.Activities.OrderBy(item => item.Sequence).Select(item => new CivilEngineeringWeeklySupervisionActivityDto { Sequence = item.Sequence, ActivityCategoryId = item.ActivityCategoryId, ActivityCategoryLabel = categories.GetValueOrDefault(item.ActivityCategoryId, "Unavailable category"), ActorType = item.ActorType, ContractorBusinessPartnerId = item.ContractorBusinessPartnerId, ContractorName = item.ContractorBusinessPartnerId.HasValue ? contractors.GetValueOrDefault(item.ContractorBusinessPartnerId.Value) : null, Description = item.Description, ProgressPercent = item.ProgressPercent }).ToList(),
            Evidence = value.Evidence.Select(item => new CivilEngineeringWeeklySupervisionEvidenceDto { EvidenceRole = item.EvidenceRole, CentralDocumentRecordId = item.CentralDocumentRecordId, CentralDocumentVersionId = item.CentralDocumentVersionId, DocumentReference = documents.TryGetValue(item.CentralDocumentVersionId, out var document) ? document.DocumentRecord.DocumentReference : "Unavailable", DocumentTitle = document?.DocumentRecord.Title ?? "Unavailable", VersionNumber = document?.VersionNumber ?? string.Empty }).ToList(),
            Reviews = value.Reviews.OrderBy(item => item.Sequence).Select(item => new CivilEngineeringWeeklySupervisionReviewDto
            {
                Sequence = item.Sequence, Action = item.Action, Outcome = item.Outcome, Comment = item.Comment,
                CentralDocumentRecordId = item.CentralDocumentRecordId, CentralDocumentVersionId = item.CentralDocumentVersionId,
                DocumentReference = item.CentralDocumentVersionId.HasValue && documents.TryGetValue(item.CentralDocumentVersionId.Value, out var reviewDocument) ? reviewDocument.DocumentRecord.DocumentReference : null,
                DocumentTitle = item.CentralDocumentVersionId.HasValue && documents.TryGetValue(item.CentralDocumentVersionId.Value, out reviewDocument) ? reviewDocument.DocumentRecord.Title : null,
                VersionNumber = item.CentralDocumentVersionId.HasValue && documents.TryGetValue(item.CentralDocumentVersionId.Value, out reviewDocument) ? reviewDocument.VersionNumber : null,
                ActorUserId = item.ActorUserId, ActorName = users.GetValueOrDefault(item.ActorUserId, "Unavailable user"), CreatedAt = item.CreatedAt
            }).ToList()
        }).ToList();
    }

    private void AddRevision(ProjectCivilWeeklySupervisionReport report, string action, object? before, object after, string? reason, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.ProjectCivilWeeklySupervisionRevisions.Add(new ProjectCivilWeeklySupervisionRevision { Id = Guid.NewGuid(), TenantId = TenantId, ReportId = report.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = Clean(reason, 2000), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }

    private void AddAudit(ProjectCivilWeeklySupervisionReport report, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectCivilWeeklySupervisionReport), ResourceId = report.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
        IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
    });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The weekly supervision report changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && (sql.Number is >= 52130 and <= 52144)) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting weekly-report request was detected. Refresh and retry."); }
    }

    private static DateTime CalculateDueAt(DateTime weekStart, CivilEngineeringReportDueDay dueDay) => weekStart.Date.AddDays(dueDay == CivilEngineeringReportDueDay.Friday ? 12 : 8).AddTicks(-1);
    private static void ApplyRowVersion(ProjectCivilWeeklySupervisionReport report, string encoded) { try { report.RowVersion = Convert.FromBase64String(encoded); } catch (FormatException) { throw Validation("The weekly report concurrency version is invalid."); } }
    private async Task<IReadOnlyDictionary<Guid, string>> DependencyLabelsAsync(IReadOnlyCollection<Guid> dependencyIds, CancellationToken token) =>
        await (
            from dependency in db.ProjectTaskDependencies.AsNoTracking()
            join predecessor in db.ProjectWorkItems.AsNoTracking() on dependency.PredecessorWorkItemId equals predecessor.Id
            join successor in db.ProjectWorkItems.AsNoTracking() on dependency.SuccessorWorkItemId equals successor.Id
            where dependency.TenantId == TenantId && dependencyIds.Contains(dependency.Id)
                  && predecessor.TenantId == TenantId && successor.TenantId == TenantId
            select new { dependency.Id, Label = predecessor.Title + " → " + successor.Title }
        ).ToDictionaryAsync(value => value.Id, value => value.Label, token);

    private static object Snapshot(ProjectCivilWeeklySupervisionReport value) => new { value.Id, value.ProjectId, value.ProjectEngineerAssignmentId, value.ProjectMilestoneId, value.MilestoneTargetDateSnapshot, value.MilestoneActualDateSnapshot, value.ProjectRiskId, value.ProjectIssueId, value.ProjectTaskDependencyId, value.WeekStart, value.WeekEnd, value.OverallProgressPercent, value.SiteStatus, value.DelayReason, value.RecoveryActionItemId, value.IsProgressCorrection, value.ProgressCorrectionDecisionId, value.HasGovernedProgressControl, value.DueAt, value.EscalatedAt, value.Status, value.ApprovalStatus, value.WorkflowInstanceId, value.ConfigurationProfileId, value.ConfigurationDecisionId, value.WorkflowDefinitionId, value.EvidenceMetadataTemplateId, value.EvidenceMetadataTemplateCodeSnapshot, value.PolicyHash };
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : throw Validation($"Text cannot exceed {max} characters.");
    private static string RequiredText(string? value, int max, string label) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) throw Validation($"{label} is required."); return normalized.Length <= max ? normalized : throw Validation($"{label} cannot exceed {max} characters."); }
    private static string DisplayName(string? first, string? last, string? fallback) { var result = string.Join(' ', new[] { first, last }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim(); return string.IsNullOrWhiteSpace(result) ? fallback ?? string.Empty : result; }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static CivilEngineeringSupervisionValidationException Validation(string value) => new(value);
    private static CivilEngineeringSupervisionValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringSupervisionConflictException Conflict(string value) => new(value);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, CivilEngineeringWeeklyReportValue Value, CentralDocumentMetadataTemplate Template, string PolicyHash);
}
