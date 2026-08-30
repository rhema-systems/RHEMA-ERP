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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// CIV-0501 assignment boundary. The existing ProjectWorkItem remains the task owner;
/// this service owns Civil policy, controlled assignee selection and audit evidence.
/// CIV-0502 adds append-only assignee feedback and independent shared-workflow closure acceptance.
/// </summary>
public sealed class CivilEngineeringDirectTaskService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow) : ICivilEngineeringDirectTaskService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringDirectTaskLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        await RequireAssignmentAuthorityAsync(projectId, token);
        return new CivilEngineeringDirectTaskLookupsDto
        {
            Assignees = await EligibleAssigneesAsync(projectId, policy, token),
            Urgencies = policy.Value.AllowedUrgencies.Distinct().OrderBy(value => value).ToList(),
            Documents = await DocumentLookupsAsync(token),
            RequireDueDate = policy.Value.RequireDueDate,
            UrgentResponseHours = policy.Value.UrgentResponseHours
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringDirectTaskDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var values = await Tasks(false).Where(value => value.ProjectId == projectId).OrderByDescending(value => value.DueDate).ThenByDescending(value => value.CreatedAt).ToListAsync(token);
        return await MapAsync(values, token);
    }

    public async Task<CivilEngineeringDirectTaskDto> CreateAsync(Guid projectId, CreateCivilEngineeringDirectTaskRequest request, string correlationId, CancellationToken token = default)
    {
        var at = DateTime.UtcNow;
        var policy = await ResolvePolicyAsync(at, token);
        var errors = CivilEngineeringDirectTaskPolicy.ValidateCreate(request, policy.Value, at);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new
        {
            projectId,
            title = request.Title.Trim(),
            instructions = request.Instructions.Trim(),
            request.AssignedToUserId,
            request.AssignedRoleId,
            request.Urgency,
            urgencyReason = Clean(request.UrgencyReason, 1000),
            dueDate = request.DueDate!.Value.ToUniversalTime(),
            request.CentralDocumentRecordId,
            request.CentralDocumentVersionId
        });

        CivilEngineeringDirectTaskDto? result = null;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var retry = await Tasks(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
            if (retry is not null)
            {
                if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different Civil task values.");
                await transaction.CommitAsync(token);
                result = (await MapAsync([retry], token)).Single();
                return;
            }

            policy = await ResolvePolicyAsync(at, token);
            errors = CivilEngineeringDirectTaskPolicy.ValidateCreate(request, policy.Value, at);
            if (errors.Count > 0) throw Validation(errors);
            await RequireProjectAsync(projectId, token);
            await RequireAssignmentAuthorityAsync(projectId, token);
            var assignee = await RequireAssigneeAsync(projectId, request.AssignedToUserId, request.AssignedRoleId, policy, token);
            var evidence = request.CentralDocumentVersionId.HasValue
                ? await RequireEvidenceAsync(request.CentralDocumentRecordId!.Value, request.CentralDocumentVersionId.Value, token)
                : null;
            var now = DateTime.UtcNow;
            var sortOrder = await db.ProjectWorkItems.Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.ParentId == null && !value.IsDeleted).CountAsync(token);
            var workItem = new ProjectWorkItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProjectId = projectId,
                NodeType = ProjectWorkItemNodeTypes.Task,
                Title = request.Title.Trim(),
                Description = request.Instructions.Trim(),
                Status = "Assigned",
                Priority = CivilEngineeringDirectTaskPolicy.WorkItemPriority(request.Urgency),
                AssignedToUserId = assignee.UserId,
                PlannedStartDate = now,
                PlannedEndDate = request.DueDate!.Value.ToUniversalTime(),
                SortOrder = sortOrder,
                IsRollupEnabled = true,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = UserId
            };
            var task = new ProjectCivilDirectTaskControl
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProjectId = projectId,
                WorkItemId = workItem.Id,
                ClientRequestId = request.ClientRequestId,
                RequestHash = requestHash,
                AssignedToUserId = assignee.UserId,
                AssignedRoleId = assignee.RoleId,
                AssignedRoleName = assignee.RoleName,
                Urgency = request.Urgency,
                IsUrgentPath = CivilEngineeringDirectTaskPolicy.IsUrgent(request.Urgency),
                UrgencyReason = Clean(request.UrgencyReason, 1000),
                UrgentResponseDueAt = CivilEngineeringDirectTaskPolicy.IsUrgent(request.Urgency) ? request.DueDate!.Value.ToUniversalTime() : null,
                DueDate = request.DueDate!.Value.ToUniversalTime(),
                Instructions = request.Instructions.Trim(),
                CentralDocumentRecordId = evidence?.DocumentRecordId,
                CentralDocumentVersionId = evidence?.Id,
                ConfigurationProfileId = policy.ProfileId,
                ConfigurationDecisionId = policy.DecisionId,
                WorkflowDefinitionId = policy.WorkflowDefinitionId,
                FeedbackMetadataTemplateId = policy.Template.Id,
                FeedbackMetadataTemplateCodeSnapshot = policy.Template.TemplateCode,
                PolicyHash = policy.PolicyHash,
                Status = "Assigned",
                ApprovalStatus = "Draft",
                CorrelationId = Correlation(correlationId),
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = UserId
            };
            db.ProjectWorkItems.Add(workItem);
            db.ProjectCivilDirectTaskControls.Add(task);
            if (task.IsUrgentPath)
            {
                db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    RecipientId = assignee.UserId,
                    NotificationType = "CivilDirectTaskUrgentAssignment",
                    Title = "Urgent Civil task assigned",
                    Message = $"An urgent Civil task '{workItem.Title}' requires a response by {task.UrgentResponseDueAt:dd MMM yyyy HH:mm} UTC.",
                    Priority = request.Urgency == CivilEngineeringUrgency.Emergency ? "Critical" : "High",
                    Status = "Pending",
                    ScheduledFor = now,
                    EntityType = nameof(ProjectCivilDirectTaskControl),
                    EntityId = task.Id,
                    ActionUrl = $"/development/civil-engineering/direct-tasks?projectId={projectId}",
                    DeliveryMethods = "InApp",
                    AdditionalData = JsonSerializer.Serialize(new { source = "CIV-0503", taskId = task.Id, projectId, task.UrgencyReason, task.UrgentResponseDueAt, correlationId = Correlation(correlationId) }, JsonOptions),
                    CreatedAt = now,
                    CreatedBy = UserName,
                    CreatedById = UserId
                });
            }
            AddRevision(task, CivilEngineeringAuditEventMap.CreateCivilTask, null, Snapshot(task, workItem), null, correlationId);
            AddAudit(task, CivilEngineeringAuditEventMap.CreateCivilTask, null, Snapshot(task, workItem), correlationId);
            await SaveAsync(token);
            await transaction.CommitAsync(token);
            result = (await MapAsync([task], token)).Single();
        });
        return result ?? throw new InvalidOperationException("The Civil direct-task transaction completed without a result.");
    }

    public async Task<IReadOnlyList<CivilEngineeringDirectTaskDto>> EscalateUrgentAsync(Guid projectId, EscalateCivilEngineeringUrgentTasksRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required to escalate overdue urgent tasks.");
        await RequireProjectAsync(projectId, token);
        await RequireAssignmentAuthorityAsync(projectId, token);
        var requestHash = Hash(new { projectId, request.ClientRequestId });
        var now = DateTime.UtcNow;

        IReadOnlyList<CivilEngineeringDirectTaskDto>? result = null;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var retries = await Tasks(true).Where(value => value.ProjectId == projectId && value.UrgentEscalationClientRequestId == request.ClientRequestId)
                .OrderBy(value => value.DueDate).ToListAsync(token);
            if (retries.Count > 0)
            {
                if (retries.Any(value => !FixedEquals(value.UrgentEscalationRequestHash ?? string.Empty, requestHash)))
                    throw Conflict("This client request identifier was already used with different urgent-task escalation values.");
                await transaction.CommitAsync(token);
                result = await MapAsync(retries, token);
                return;
            }

            var tasks = await Tasks(true).Where(value => value.ProjectId == projectId && value.IsUrgentPath && value.UrgentResponseDueAt.HasValue
                    && value.UrgentResponseDueAt.Value < now && !value.UrgentEscalatedAt.HasValue && value.Status != "Accepted" && value.Status != "Cancelled")
                .Include(value => value.WorkItem).OrderBy(value => value.UrgentResponseDueAt).ToListAsync(token);
            if (tasks.Count == 0)
            {
                await transaction.CommitAsync(token);
                result = [];
                return;
            }

            foreach (var task in tasks)
            {
                var policy = await ResolveFrozenPolicyAsync(task, token);
                var recipients = await UrgentEscalationRecipientsAsync(projectId, task.AssignedToUserId, policy, token);
                if (recipients.Count == 0)
                    throw Validation("CIV-CFG-010 has no active independent project-member escalation recipient for this overdue urgent task.");

                var before = Snapshot(task, task.WorkItem);
                task.UrgentEscalatedAt = now;
                task.UrgentEscalationClientRequestId = request.ClientRequestId;
                task.UrgentEscalationRequestHash = requestHash;
                task.CorrelationId = Correlation(correlationId);
                task.UpdatedAt = now;
                task.UpdatedBy = UserName;
                task.LastModifiedById = UserId;
                foreach (var recipientId in recipients)
                {
                    db.Notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        RecipientId = recipientId,
                        NotificationType = "CivilDirectTaskUrgentEscalation",
                        Title = "Overdue urgent Civil task",
                        Message = $"The urgent Civil task '{task.WorkItem.Title}' passed its response deadline and requires intervention.",
                        Priority = task.Urgency == CivilEngineeringUrgency.Emergency ? "Critical" : "High",
                        Status = "Pending",
                        ScheduledFor = now,
                        EntityType = nameof(ProjectCivilDirectTaskControl),
                        EntityId = task.Id,
                        ActionUrl = $"/development/civil-engineering/direct-tasks?projectId={projectId}",
                        DeliveryMethods = "InApp",
                        AdditionalData = JsonSerializer.Serialize(new { source = "CIV-0503", taskId = task.Id, projectId, task.UrgencyReason, task.UrgentResponseDueAt, request.ClientRequestId, correlationId = Correlation(correlationId) }, JsonOptions),
                        CreatedAt = now,
                        CreatedBy = UserName,
                        CreatedById = UserId
                    });
                }
                AddRevision(task, CivilEngineeringAuditEventMap.OverrideCivilTaskUrgency, before, Snapshot(task, task.WorkItem), "Overdue urgent-task escalation dispatched to configured independent project roles.", correlationId);
                AddAudit(task, CivilEngineeringAuditEventMap.OverrideCivilTaskUrgency, before, Snapshot(task, task.WorkItem), correlationId);
            }
            await SaveAsync(token);
            await transaction.CommitAsync(token);
            result = await MapAsync(tasks, token);
        });
        return result ?? throw new InvalidOperationException("The Civil urgent-task escalation transaction completed without a result.");
    }

    public async Task<CivilEngineeringDirectTaskFeedbackLookupsDto> GetFeedbackLookupsAsync(Guid projectId, Guid taskId, CancellationToken token = default)
    {
        var task = await LoadTaskAsync(projectId, taskId, false, token);
        await RequireProjectMemberAsync(task.ProjectId, token);
        var policy = await ResolveFrozenPolicyAsync(task, token);
        var isAssignee = await IsAssignedActorAsync(task, token);
        var isReviewer = await IsReviewerAsync(task, token);
        if (!isAssignee && !isReviewer) throw new UnauthorizedAccessException("You are not the assigned worker or an authorized independent reviewer for this Civil task.");
        return new CivilEngineeringDirectTaskFeedbackLookupsDto
        {
            Documents = await DocumentLookupsAsync(policy.Template.TemplateCode, token),
            AvailableActions = AvailableActions(task, isAssignee, isReviewer),
            MeasurementUnits = await MeasurementUnitLookupsAsync(token),
            RequireFeedbackEvidence = policy.Value.RequireFeedbackEvidence,
            RequireClosureAcceptance = policy.Value.RequireClosureAcceptance
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringDirectTaskFeedbackDto>> GetFeedbackAsync(Guid projectId, Guid taskId, CancellationToken token = default)
    {
        var task = await LoadTaskAsync(projectId, taskId, false, token);
        await RequireProjectMemberAsync(task.ProjectId, token);
        var rows = await db.ProjectCivilDirectTaskFeedbackEntries.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.DirectTaskControlId == task.Id && !value.IsDeleted)
            .OrderBy(value => value.Sequence).ToListAsync(token);
        return await MapFeedbackAsync(rows, token);
    }

    public async Task<CivilEngineeringDirectTaskDto> ProcessFeedbackAsync(Guid projectId, Guid taskId, ProcessCivilEngineeringDirectTaskFeedbackRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringDirectTaskPolicy.ValidateFeedback(request);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new { taskId, request.Action, message = Clean(request.Message, 2000), request.ProgressPercent, request.MeasurementValue, request.MeasurementUnitId, capturedOfflineAtUtc = request.CapturedOfflineAtUtc?.ToUniversalTime(), request.CentralDocumentRecordId, request.CentralDocumentVersionId, request.RowVersion });
        CivilEngineeringDirectTaskDto? result = null;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var task = await LoadTaskAsync(projectId, taskId, true, token);
            await RequireProjectMemberAsync(task.ProjectId, token);
            var retry = await db.ProjectCivilDirectTaskFeedbackEntries.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.DirectTaskControlId == task.Id && value.ClientRequestId == request.ClientRequestId, token);
            if (retry is not null)
            {
                if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different Civil task feedback values.");
                await transaction.CommitAsync(token);
                result = (await MapAsync([task], token)).Single();
                return;
            }

            ApplyRowVersion(task, request.RowVersion);
            var policy = await ResolveFrozenPolicyAsync(task, token);
            var isAssignee = await IsAssignedActorAsync(task, token);
            var isReviewer = await IsReviewerAsync(task, token);
            var evidence = request.CentralDocumentVersionId.HasValue
                ? await RequireEvidenceAsync(request.CentralDocumentRecordId!.Value, request.CentralDocumentVersionId.Value, policy.Template.TemplateCode, token)
                : null;
            var measurementUnit = request.MeasurementUnitId.HasValue ? await RequireMeasurementUnitAsync(request.MeasurementUnitId.Value, token) : null;
            var now = DateTime.UtcNow;
            var workItem = await db.ProjectWorkItems.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == task.WorkItemId && value.ProjectId == task.ProjectId && !value.IsDeleted, token)
                ?? throw Conflict("The authoritative Projects work item for this Civil task is unavailable.");
            var before = Snapshot(task, workItem);
            var feedback = new ProjectCivilDirectTaskFeedbackEntry
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DirectTaskControlId = task.Id,
                ClientRequestId = request.ClientRequestId,
                RequestHash = requestHash,
                Sequence = await NextFeedbackSequenceAsync(task.Id, token),
                Action = request.Action,
                ProgressPercent = request.ProgressPercent,
                Message = Clean(request.Message, 2000),
                ActorUserId = UserId,
                MeasurementValue = request.MeasurementValue,
                MeasurementUnitId = measurementUnit?.Id,
                CapturedOfflineAtUtc = request.CapturedOfflineAtUtc?.ToUniversalTime(),
                CentralDocumentRecordId = evidence?.DocumentRecordId,
                CentralDocumentVersionId = evidence?.Id,
                // The append-only feedback record is deliberately written before a shared workflow can persist its own records.
                // The subsequent revision/audit preserves the normalized workflow outcome without ever mutating this event.
                WorkflowOutcome = null,
                CorrelationId = Correlation(correlationId),
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = UserId
            };

            db.ProjectCivilDirectTaskFeedbackEntries.Add(feedback);
            await SaveAsync(token);

            task.LastFeedbackClientRequestId = request.ClientRequestId;
            task.LastFeedbackRequestHash = requestHash;
            WorkflowOutcome? outcome;
            try { outcome = await PrepareFeedbackMutationAsync(task, workItem, request, policy, isAssignee, isReviewer, now, token); }
            catch (DbUpdateConcurrencyException) { throw Conflict("The shared direct-task workflow changed concurrently. Refresh and retry."); }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52280 and <= 52291) { throw Conflict(sql.Message); }
            catch (DbUpdateException) { throw Conflict("The shared direct-task workflow could not persist this feedback action. Refresh and retry."); }
            task.CorrelationId = Correlation(correlationId);
            task.UpdatedAt = now;
            task.UpdatedBy = UserName;
            task.LastModifiedById = UserId;
            workItem.UpdatedAt = now;
            workItem.UpdatedBy = UserName;
            workItem.LastModifiedById = UserId;
            var auditAction = AuditAction(request.Action, outcome);
            var after = new { task = Snapshot(task, workItem), feedback = FeedbackSnapshot(feedback) };
            AddRevision(task, auditAction, before, after, Clean(request.Message, 2000), correlationId);
            AddAudit(task, auditAction, before, after, correlationId);
            await SaveAsync(token);
            await transaction.CommitAsync(token);
            result = (await MapAsync([task], token)).Single();
        });
        return result ?? throw new InvalidOperationException("The Civil task-feedback transaction completed without a result.");
    }

    private IQueryable<ProjectCivilDirectTaskControl> Tasks(bool tracked) =>
        (tracked ? db.ProjectCivilDirectTaskControls : db.ProjectCivilDirectTaskControls.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<ProjectCivilDirectTaskControl> LoadTaskAsync(Guid projectId, Guid taskId, bool tracked, CancellationToken token) =>
        await Tasks(tracked).SingleOrDefaultAsync(value => value.ProjectId == projectId && value.Id == taskId, token)
        ?? throw new CivilEngineeringDirectTaskNotFoundException("The Civil direct task was not found.");

    private async Task RequireProjectMemberAsync(Guid projectId, CancellationToken token)
    {
        if (!await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.UserId == UserId && value.IsActive && !value.IsDeleted, token))
            throw new UnauthorizedAccessException("You are not an active member of this Civil task's project.");
    }

    private async Task<bool> IsAssignedActorAsync(ProjectCivilDirectTaskControl task, CancellationToken token)
    {
        if (task.AssignedToUserId != UserId || !currentUser.Roles.Any(value => string.Equals(value, task.AssignedRoleName, StringComparison.OrdinalIgnoreCase))) return false;
        return await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == task.ProjectId && value.UserId == UserId
            && value.IsActive && !value.IsDeleted && value.Role == task.AssignedRoleName, token);
    }

    private async Task<bool> IsReviewerAsync(ProjectCivilDirectTaskControl task, CancellationToken token)
    {
        if (task.AssignedToUserId == UserId) return false;
        var eligible = new[] { CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, CivilEngineeringAccessControlRegistry.CivilEngineerRole };
        var role = eligible.FirstOrDefault(candidate => currentUser.Roles.Any(value => string.Equals(value?.Trim(), candidate, StringComparison.OrdinalIgnoreCase)));
        if (string.IsNullOrWhiteSpace(role)) return false;
        return await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == task.ProjectId && value.UserId == UserId
            && value.IsActive && !value.IsDeleted && value.Role == role, token);
    }

    private static IReadOnlyList<CivilEngineeringDirectTaskFeedbackAction> AvailableActions(ProjectCivilDirectTaskControl task, bool isAssignee, bool isReviewer)
    {
        var actions = new List<CivilEngineeringDirectTaskFeedbackAction>();
        if (isAssignee && task.Status == "Assigned") actions.Add(CivilEngineeringDirectTaskFeedbackAction.Acknowledge);
        if (isAssignee && task.Status is "InProgress" or "Returned")
        {
            actions.Add(CivilEngineeringDirectTaskFeedbackAction.UpdateProgress);
            actions.Add(CivilEngineeringDirectTaskFeedbackAction.Complete);
        }
        if (isReviewer && task.Status == "PendingAcceptance")
        {
            actions.Add(CivilEngineeringDirectTaskFeedbackAction.Accept);
            actions.Add(CivilEngineeringDirectTaskFeedbackAction.Return);
        }
        return actions;
    }

    private async Task<WorkflowOutcome?> PrepareFeedbackMutationAsync(
        ProjectCivilDirectTaskControl task,
        ProjectWorkItem workItem,
        ProcessCivilEngineeringDirectTaskFeedbackRequest request,
        Policy policy,
        bool isAssignee,
        bool isReviewer,
        DateTime now,
        CancellationToken token)
    {
        var message = Clean(request.Message, 2000);
        switch (request.Action)
        {
            case CivilEngineeringDirectTaskFeedbackAction.Acknowledge:
                Require(isAssignee && task.Status == "Assigned", "Only the assigned Civil worker can acknowledge a newly assigned task.");
                task.Status = "InProgress";
                task.AcknowledgedById = UserId;
                task.AcknowledgedAt = now;
                workItem.Status = "InProgress";
                workItem.ActualStartDate ??= now;
                return null;

            case CivilEngineeringDirectTaskFeedbackAction.UpdateProgress:
                Require(isAssignee && (task.Status is "InProgress" or "Returned"), "Only the assigned Civil worker can update task progress.");
                Require(request.ProgressPercent.HasValue && request.ProgressPercent.Value < 100m, "Enter progress from 0 to 99.99; use Complete when the assigned work is finished.");
                Require(!string.IsNullOrWhiteSpace(message), "Enter a progress update.");
                task.Status = "InProgress";
                task.ProgressPercent = request.ProgressPercent.Value;
                workItem.Status = "InProgress";
                workItem.PercentComplete = request.ProgressPercent.Value;
                workItem.ActualStartDate ??= now;
                return null;

            case CivilEngineeringDirectTaskFeedbackAction.Complete:
                Require(isAssignee && (task.Status is "InProgress" or "Returned"), "Only the assigned Civil worker can submit task completion.");
                Require(!string.IsNullOrWhiteSpace(message), "Enter a completion summary.");
                if (policy.Value.RequireFeedbackEvidence)
                    Require(request.CentralDocumentVersionId.HasValue, "CIV-CFG-010 requires current Published central-DMS evidence when task completion is submitted.");
                task.ProgressPercent = 100m;
                task.CompletedById = UserId;
                task.CompletedAt = now;
                task.Status = "PendingAcceptance";
                workItem.Status = "Completed";
                workItem.PercentComplete = 100m;
                workItem.ActualStartDate ??= now;
                workItem.ActualEndDate = now;
                if (!policy.Value.RequireClosureAcceptance)
                {
                    task.ApprovalStatus = "Draft";
                    return null;
                }
                var submission = await workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.DirectTask, task.Id, task.WorkflowDefinitionId);
                if (!submission.ExecutionResult.Success || submission.Outcome != WorkflowOutcome.Pending)
                    throw Conflict(submission.ExecutionResult.Message ?? "The configured direct-task workflow must start in a pending acceptance state.");
                task.WorkflowInstanceId = submission.ExecutionResult.WorkflowInstanceId;
                task.ApprovalStatus = "Pending";
                return submission.Outcome;

            case CivilEngineeringDirectTaskFeedbackAction.Accept:
                Require(isReviewer && task.Status == "PendingAcceptance", "Only an independent authorized Civil reviewer can accept this completed task.");
                if (!policy.Value.RequireClosureAcceptance)
                {
                    task.Status = "Accepted";
                    task.ApprovalStatus = "Approved";
                    task.AcceptedById = UserId;
                    task.AcceptedAt = now;
                    task.ApprovedById = UserId;
                    task.ApprovedAt = now;
                    task.RejectionReason = null;
                    return WorkflowOutcome.Approved;
                }
                if (!task.WorkflowInstanceId.HasValue || !await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.DirectTask, task.Id, UserId))
                    throw new UnauthorizedAccessException("You are not assigned to the current shared direct-task acceptance workflow step.");
                var approval = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.DirectTask, task.Id, UserId, "Approve", message);
                if (!approval.ExecutionResult.Success || approval.Outcome is not (WorkflowOutcome.Pending or WorkflowOutcome.Approved))
                    throw Conflict(approval.ExecutionResult.Message ?? "The shared direct-task workflow could not accept the completion submission.");
                task.ApprovalStatus = approval.Outcome == WorkflowOutcome.Approved ? "Approved" : "Pending";
                if (approval.Outcome == WorkflowOutcome.Approved)
                {
                    task.Status = "Accepted";
                    task.AcceptedById = UserId;
                    task.AcceptedAt = now;
                    task.ApprovedById = UserId;
                    task.ApprovedAt = now;
                    task.RejectionReason = null;
                }
                return approval.Outcome;

            case CivilEngineeringDirectTaskFeedbackAction.Return:
                Require(isReviewer && task.Status == "PendingAcceptance", "Only an independent authorized Civil reviewer can return this completion submission.");
                Require(!string.IsNullOrWhiteSpace(message), "Enter a reason when returning a completed Civil task.");
                if (policy.Value.RequireClosureAcceptance)
                {
                    if (!task.WorkflowInstanceId.HasValue || !await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.DirectTask, task.Id, UserId))
                        throw new UnauthorizedAccessException("You are not assigned to the current shared direct-task acceptance workflow step.");
                    var rejection = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.DirectTask, task.Id, UserId, "Reject", message);
                    if (!rejection.ExecutionResult.Success || rejection.Outcome != WorkflowOutcome.Rejected)
                        throw Conflict(rejection.ExecutionResult.Message ?? "The shared direct-task workflow could not return the completion submission.");
                    task.WorkflowInstanceId = rejection.ExecutionResult.WorkflowInstanceId;
                }
                task.Status = "Returned";
                task.ApprovalStatus = "Draft";
                task.RejectionReason = message;
                task.ApprovedById = null;
                task.ApprovedAt = null;
                task.AcceptedById = null;
                task.AcceptedAt = null;
                task.ProgressPercent = Math.Min(task.ProgressPercent, 99.99m);
                workItem.Status = "InProgress";
                workItem.PercentComplete = task.ProgressPercent;
                workItem.ActualEndDate = null;
                return policy.Value.RequireClosureAcceptance ? WorkflowOutcome.Rejected : null;

            default:
                throw Validation("The requested Civil task feedback action is not supported.");
        }
    }

    private async Task<int> NextFeedbackSequenceAsync(Guid taskId, CancellationToken token)
        => (await db.ProjectCivilDirectTaskFeedbackEntries.Where(value => value.TenantId == TenantId && value.DirectTaskControlId == taskId).MaxAsync(value => (int?)value.Sequence, token) ?? 0) + 1;

    private static string AuditAction(CivilEngineeringDirectTaskFeedbackAction action, WorkflowOutcome? outcome) => action switch
    {
        CivilEngineeringDirectTaskFeedbackAction.Acknowledge => CivilEngineeringAuditEventMap.AcknowledgeCivilTask,
        CivilEngineeringDirectTaskFeedbackAction.UpdateProgress => CivilEngineeringAuditEventMap.UpdateCivilTaskProgress,
        CivilEngineeringDirectTaskFeedbackAction.Complete => CivilEngineeringAuditEventMap.SubmitCivilTaskCompletion,
        CivilEngineeringDirectTaskFeedbackAction.Accept when outcome == WorkflowOutcome.Approved => CivilEngineeringAuditEventMap.ApproveCivilTaskCompletion,
        CivilEngineeringDirectTaskFeedbackAction.Accept => CivilEngineeringAuditEventMap.UpdateCivilTaskProgress,
        CivilEngineeringDirectTaskFeedbackAction.Return => CivilEngineeringAuditEventMap.RejectCivilTaskCompletion,
        _ => CivilEngineeringAuditEventMap.AttachCivilTaskEvidence
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new CivilEngineeringDirectTaskConflictException(message);
    }

    private async Task RequireProjectAsync(Guid projectId, CancellationToken token)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
        if (!await db.Projects.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted, token))
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private async Task RequireAssignmentAuthorityAsync(Guid projectId, CancellationToken token)
    {
        var eligible = new[] { CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, CivilEngineeringAccessControlRegistry.CivilEngineerRole };
        var role = currentUser.Roles.FirstOrDefault(value => eligible.Contains(value?.Trim(), StringComparer.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(role)) throw new UnauthorizedAccessException("Only an assigned Supervising Civil Engineer or Civil Engineer can create a governed direct task.");
        if (!await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.UserId == UserId
            && value.IsActive && !value.IsDeleted && value.Role == role, token))
            throw new UnauthorizedAccessException("The current Civil assignment role is not active for this project.");
    }

    private async Task<IReadOnlyList<CivilEngineeringDirectTaskAssigneeDto>> EligibleAssigneesAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var rows = await db.ProjectMembers.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsActive && !value.IsDeleted)
            .Join(db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.IsActive), member => member.UserId, user => user.Id,
                (member, user) => new { member.UserId, member.Role, user.FirstName, user.LastName, user.UserName })
            .Join(db.UserRoles.AsNoTracking().Where(value => policy.Value.AssigneeRoleIds.Contains(value.RoleId)), value => value.UserId, role => role.UserId,
                (value, role) => new { value.UserId, value.Role, value.FirstName, value.LastName, value.UserName, role.RoleId })
            .Join(db.Roles.AsNoTracking(), value => value.RoleId, role => role.Id,
                (value, role) => new { value.UserId, ProjectRole = value.Role, value.FirstName, value.LastName, value.UserName, RoleId = value.RoleId, RoleName = role.Name })
            .Where(value => value.RoleName != null && value.ProjectRole == value.RoleName)
            .OrderBy(value => value.FirstName).ThenBy(value => value.LastName).ToListAsync(token);
        return rows.GroupBy(value => new { value.UserId, value.RoleId }).Select(value => value.First()).Select(value => new CivilEngineeringDirectTaskAssigneeDto
        {
            UserId = value.UserId,
            RoleId = value.RoleId,
            RoleName = value.RoleName!,
            DisplayName = DisplayName(value.FirstName, value.LastName, value.UserName)
        }).ToList();
    }

    private async Task<CivilEngineeringDirectTaskAssigneeDto> RequireAssigneeAsync(Guid projectId, Guid userId, Guid roleId, Policy policy, CancellationToken token)
    {
        var value = (await EligibleAssigneesAsync(projectId, policy, token)).SingleOrDefault(item => item.UserId == userId && item.RoleId == roleId);
        return value ?? throw Validation("Select an active project member with a CIV-CFG-010 allowed assignee role.");
    }

    private async Task<IReadOnlyList<Guid>> UrgentEscalationRecipientsAsync(Guid projectId, Guid assignedToUserId, Policy policy, CancellationToken token)
    {
        var roleIds = policy.Value.UrgentEscalationRoleIds.Distinct().ToList();
        if (roleIds.Count == 0) throw Validation("CIV-CFG-010 must select urgent escalation roles before an overdue urgent task can be escalated.");
        var rows = await db.ProjectMembers.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsActive && !value.IsDeleted && value.UserId != assignedToUserId)
            .Join(db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.IsActive), member => member.UserId, user => user.Id,
                (member, user) => new { member.UserId, member.Role })
            .Join(db.UserRoles.AsNoTracking().Where(value => roleIds.Contains(value.RoleId)), value => value.UserId, role => role.UserId,
                (value, role) => new { value.UserId, value.Role, role.RoleId })
            .Join(db.Roles.AsNoTracking(), value => value.RoleId, role => role.Id,
                (value, role) => new { value.UserId, ProjectRole = value.Role, RoleName = role.Name })
            .Where(value => value.RoleName != null && value.ProjectRole == value.RoleName)
            .Select(value => value.UserId).Distinct().ToListAsync(token);
        return rows;
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, CancellationToken token)
    {
        var value = await db.CentralDocumentVersions.AsNoTracking().Include(item => item.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == versionId && item.DocumentRecordId == recordId, token);
        return value ?? throw Validation("Select a current Published central-DMS supporting file from this tenant.");
    }

    private async Task<IReadOnlyList<CivilEngineeringDirectTaskDocumentDto>> DocumentLookupsAsync(CancellationToken token) =>
        await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId).OrderBy(value => value.DocumentRecord.DocumentReference).ThenByDescending(value => value.CreatedAt).Take(250)
            .Select(value => new CivilEngineeringDirectTaskDocumentDto { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).ToListAsync(token);

    private async Task<Policy> ResolvePolicyAsync(DateTime at, CancellationToken token)
    {
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted
            && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No effective published Civil Engineering configuration profile exists.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one Civil Engineering configuration profile is effective.");
        var profile = profiles[0];
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-010" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-010 direct-task decision.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Validation("CIV-CFG-010 is not approved and verified.");
        CivilEngineeringTaskAssignmentValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringTaskAssignmentValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-010 contains invalid direct-task control data."); }
        if (value.WorkflowDefinitionId == Guid.Empty || value.FeedbackMetadataTemplateId == Guid.Empty || value.AssigneeRoleIds.Count == 0 || value.AllowedUrgencies.Count == 0)
            throw Validation("CIV-CFG-010 must select a direct-task workflow, feedback DMS template, assignee roles and allowed urgencies.");
        var workflow = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.WorkflowDefinitionId
            && !item.IsDeleted && item.IsActive && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive
            && item.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.DirectTask, token);
        if (workflow is null || !await db.WorkflowSteps.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.WorkflowDefinitionId == value.WorkflowDefinitionId && !item.IsDeleted, token))
            throw Validation($"The CIV-CFG-010 direct-task workflow must be active, Published, contain a step, and be bound to {CivilEngineeringWorkflowBindingRegistry.DirectTask}.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.FeedbackMetadataTemplateId && item.IsActive && item.PublishedAt.HasValue && !item.IsDeleted, token)
            ?? throw Validation("The CIV-CFG-010 feedback DMS template is unavailable.");
        return new Policy(profile.Id, decision.Id, value.WorkflowDefinitionId, value, template, Hash(decision.ValueJson));
    }

    private async Task<Policy> ResolveFrozenPolicyAsync(ProjectCivilDirectTaskControl task, CancellationToken token)
    {
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == task.ConfigurationDecisionId && value.ProfileId == task.ConfigurationProfileId
            && value.ConfigurationKey == "CIV-CFG-010" && !value.IsDeleted, token)
            ?? throw Conflict("The frozen CIV-CFG-010 decision for this direct task is unavailable.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved
            || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved
            || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Conflict("The frozen CIV-CFG-010 decision is no longer an approved and verified record.");

        CivilEngineeringTaskAssignmentValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringTaskAssignmentValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("The frozen CIV-CFG-010 decision contains invalid direct-task control data."); }

        if (task.WorkflowDefinitionId != value.WorkflowDefinitionId
            || task.FeedbackMetadataTemplateId != value.FeedbackMetadataTemplateId
            || !FixedEquals(task.PolicyHash, Hash(decision.ValueJson)))
            throw Conflict("The Civil task configuration snapshot no longer matches its immutable CIV-CFG-010 decision.");

        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == task.WorkflowDefinitionId && !item.IsDeleted && item.IsActive
            && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive
            && item.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.DirectTask, token);
        if (workflowDefinition is null || !await db.WorkflowSteps.AsNoTracking().AnyAsync(item =>
                item.TenantId == TenantId && item.WorkflowDefinitionId == task.WorkflowDefinitionId && !item.IsDeleted, token))
            throw Conflict("The direct-task workflow frozen on this task is not an active Published PROJECT_TASK workflow with an approval step.");

        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == task.FeedbackMetadataTemplateId && item.IsActive && item.PublishedAt.HasValue
            && !item.IsDeleted && item.TemplateCode == task.FeedbackMetadataTemplateCodeSnapshot, token)
            ?? throw Conflict("The frozen CIV-CFG-010 feedback DMS template is no longer active and Published.");

        return new Policy(task.ConfigurationProfileId, task.ConfigurationDecisionId, task.WorkflowDefinitionId, value, template, task.PolicyHash);
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, string templateCode, CancellationToken token)
    {
        var value = await db.CentralDocumentVersions.AsNoTracking().Include(item => item.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == versionId && item.DocumentRecordId == recordId
                && item.DocumentRecord.MetadataTemplateCode == templateCode, token);
        return value ?? throw Validation("Select a current Published central-DMS file using the CIV-CFG-010 feedback template.");
    }

    private async Task<IReadOnlyList<CivilEngineeringDirectTaskDocumentDto>> DocumentLookupsAsync(string templateCode, CancellationToken token) =>
        await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId && value.DocumentRecord.MetadataTemplateCode == templateCode)
            .OrderBy(value => value.DocumentRecord.DocumentReference).ThenByDescending(value => value.CreatedAt).Take(250)
            .Select(value => new CivilEngineeringDirectTaskDocumentDto
            {
                CentralDocumentRecordId = value.DocumentRecordId,
                CentralDocumentVersionId = value.Id,
                DocumentReference = value.DocumentRecord.DocumentReference,
                Title = value.DocumentRecord.Title,
                VersionNumber = value.VersionNumber
            }).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringDirectTaskMeasurementUnitDto>> MeasurementUnitLookupsAsync(CancellationToken token) =>
        await db.UnitsOfMeasure.AsNoTracking().Where(value => value.TenantId == TenantId && value.IsActive && !value.IsDeleted)
            .OrderBy(value => value.Category).ThenBy(value => value.Code).Take(250)
            .Select(value => new CivilEngineeringDirectTaskMeasurementUnitDto { Id = value.Id, Code = value.Code, Name = value.Name, Symbol = value.Symbol, Category = value.Category })
            .ToListAsync(token);

    private async Task<ErpSystem.Core.Entities.Inventory.UnitOfMeasure> RequireMeasurementUnitAsync(Guid unitId, CancellationToken token) =>
        await db.UnitsOfMeasure.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == unitId && value.IsActive && !value.IsDeleted, token)
        ?? throw Validation("Select an active unit of measure from this tenant.");

    private async Task<IReadOnlyList<CivilEngineeringDirectTaskDto>> MapAsync(IReadOnlyCollection<ProjectCivilDirectTaskControl> values, CancellationToken token)
    {
        var workItemIds = values.Select(value => value.WorkItemId).Distinct().ToList();
        var userIds = values.Select(value => value.AssignedToUserId).Distinct().ToList();
        var documentIds = values.Where(value => value.CentralDocumentRecordId.HasValue).Select(value => value.CentralDocumentRecordId!.Value).Distinct().ToList();
        var workItems = await db.ProjectWorkItems.AsNoTracking().Where(value => value.TenantId == TenantId && workItemIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => DisplayName(value.FirstName, value.LastName, value.UserName), token);
        var documents = await db.CentralDocumentRecords.AsNoTracking().Where(value => value.TenantId == TenantId && documentIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.DocumentReference, token);
        return values.Where(value => workItems.ContainsKey(value.WorkItemId)).Select(value => new CivilEngineeringDirectTaskDto
        {
            Id = value.Id,
            ProjectId = value.ProjectId,
            WorkItemId = value.WorkItemId,
            Title = workItems[value.WorkItemId].Title,
            Instructions = value.Instructions,
            AssignedToUserId = value.AssignedToUserId,
            AssignedToName = users.GetValueOrDefault(value.AssignedToUserId, "Unavailable assignee"),
            AssignedRoleId = value.AssignedRoleId,
            AssignedRoleName = value.AssignedRoleName,
            Urgency = value.Urgency,
            DueDate = value.DueDate,
            Status = value.Status,
            ApprovalStatus = value.ApprovalStatus,
            IsUrgentPath = value.IsUrgentPath,
            UrgencyReason = value.UrgencyReason,
            UrgentResponseDueAt = value.UrgentResponseDueAt,
            UrgentEscalatedAt = value.UrgentEscalatedAt,
            ProgressPercent = value.ProgressPercent,
            AcknowledgedAt = value.AcknowledgedAt,
            CompletedAt = value.CompletedAt,
            AcceptedAt = value.AcceptedAt,
            DocumentReference = value.CentralDocumentRecordId.HasValue ? documents.GetValueOrDefault(value.CentralDocumentRecordId.Value) : null,
            WorkflowInstanceId = value.WorkflowInstanceId,
            CreatedAt = value.CreatedAt,
            RowVersion = Convert.ToBase64String(value.RowVersion)
        }).ToList();
    }

    private async Task<IReadOnlyList<CivilEngineeringDirectTaskFeedbackDto>> MapFeedbackAsync(IReadOnlyCollection<ProjectCivilDirectTaskFeedbackEntry> values, CancellationToken token)
    {
        if (values.Count == 0) return [];
        var actorIds = values.Select(value => value.ActorUserId).Distinct().ToList();
        var documentIds = values.Where(value => value.CentralDocumentRecordId.HasValue).Select(value => value.CentralDocumentRecordId!.Value).Distinct().ToList();
        var measurementUnitIds = values.Where(value => value.MeasurementUnitId.HasValue).Select(value => value.MeasurementUnitId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && actorIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => DisplayName(value.FirstName, value.LastName, value.UserName), token);
        var documents = await db.CentralDocumentRecords.AsNoTracking().Where(value => value.TenantId == TenantId && documentIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => value.DocumentReference, token);
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(value => value.TenantId == TenantId && measurementUnitIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => string.IsNullOrWhiteSpace(value.Symbol) ? $"{value.Code} · {value.Name}" : $"{value.Symbol} ({value.Code})", token);
        return values.Select(value => new CivilEngineeringDirectTaskFeedbackDto
        {
            Id = value.Id,
            Sequence = value.Sequence,
            Action = value.Action,
            ProgressPercent = value.ProgressPercent,
            Message = value.Message,
            MeasurementValue = value.MeasurementValue,
            MeasurementUnitLabel = value.MeasurementUnitId.HasValue ? units.GetValueOrDefault(value.MeasurementUnitId.Value) : null,
            CapturedOfflineAtUtc = value.CapturedOfflineAtUtc,
            ActorName = users.GetValueOrDefault(value.ActorUserId, "Unavailable user"),
            DocumentReference = value.CentralDocumentRecordId.HasValue ? documents.GetValueOrDefault(value.CentralDocumentRecordId.Value) : null,
            WorkflowOutcome = value.WorkflowOutcome,
            CorrelationId = value.CorrelationId,
            CreatedAt = value.CreatedAt
        }).ToList();
    }

    private static void ApplyRowVersion(ProjectCivilDirectTaskControl task, string rowVersion)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { throw Validation("The task row version is invalid. Refresh and retry."); }
        if (task.RowVersion.Length == 0 || !CryptographicOperations.FixedTimeEquals(task.RowVersion, expected))
            throw Conflict("The Civil task changed concurrently. Refresh and retry.");
    }

    private void AddRevision(ProjectCivilDirectTaskControl task, string action, object? before, object after, string? reason, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        task.Revisions.Add(new ProjectCivilDirectTaskRevision { Id = Guid.NewGuid(), TenantId = TenantId, DirectTaskControlId = task.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = Clean(reason, 2000), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }

    private void AddAudit(ProjectCivilDirectTaskControl task, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId,
        UserId = UserId,
        Username = UserName,
        Action = action,
        Resource = nameof(ProjectCivilDirectTaskControl),
        ResourceId = task.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
        IpAddress = currentUser.IpAddress ?? string.Empty,
        UserAgent = currentUser.UserAgent,
        Timestamp = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName,
        CreatedById = UserId
    });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The Civil direct task changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52280 and <= 52291) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting Civil direct task was detected. Refresh and retry."); }
    }

    private static object Snapshot(ProjectCivilDirectTaskControl value, ProjectWorkItem workItem) => new { value.Id, value.ProjectId, value.WorkItemId, workItem.Title, value.AssignedToUserId, value.AssignedRoleId, value.AssignedRoleName, value.Urgency, value.IsUrgentPath, value.UrgencyReason, value.UrgentResponseDueAt, value.UrgentEscalatedAt, value.UrgentEscalationClientRequestId, value.DueDate, value.Instructions, value.CentralDocumentRecordId, value.CentralDocumentVersionId, value.ConfigurationProfileId, value.ConfigurationDecisionId, value.WorkflowDefinitionId, value.FeedbackMetadataTemplateId, value.FeedbackMetadataTemplateCodeSnapshot, value.PolicyHash, value.WorkflowInstanceId, value.Status, value.ApprovalStatus, value.ProgressPercent, value.AcknowledgedById, value.AcknowledgedAt, value.CompletedById, value.CompletedAt, value.AcceptedById, value.AcceptedAt, value.LastFeedbackClientRequestId };
    private static object FeedbackSnapshot(ProjectCivilDirectTaskFeedbackEntry value) => new { value.Id, value.ClientRequestId, value.Sequence, value.Action, value.ProgressPercent, value.MeasurementValue, value.MeasurementUnitId, value.CapturedOfflineAtUtc, value.Message, value.CentralDocumentRecordId, value.CentralDocumentVersionId, value.WorkflowOutcome, value.CorrelationId, value.CreatedAt };
    private static string DisplayName(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : throw Validation("Text cannot exceed the permitted length.");
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left ?? string.Empty), Encoding.UTF8.GetBytes(right ?? string.Empty));
    private static CivilEngineeringDirectTaskValidationException Validation(string message) => new(message);
    private static CivilEngineeringDirectTaskValidationException Validation(IEnumerable<string> messages) => new(string.Join(" ", messages));
    private static CivilEngineeringDirectTaskConflictException Conflict(string message) => new(message);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, CivilEngineeringTaskAssignmentValue Value, CentralDocumentMetadataTemplate Template, string PolicyHash);
}
