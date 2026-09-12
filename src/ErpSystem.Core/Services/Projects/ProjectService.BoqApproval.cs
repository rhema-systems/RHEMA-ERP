using System.Data;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<ProjectBoqVersionDetailDto> SubmitProjectBoqVersionAsync(
        Guid projectId,
        Guid versionId,
        Guid userId,
        string correlationId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var version = await GetProjectBoqVersionEntityAsync(projectId, versionId);
        if (version.VersionType == QuantitySurveyBoqVersionType.Approved || version.PublishedAt.HasValue)
        {
            throw new InvalidOperationException("An approved BoQ publication cannot be resubmitted or changed.");
        }

        if (string.Equals(version.Status, ProjectBoqVersionStatuses.Approved, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureApprovedBoqPublicationAsync(version, userId, correlationId);
            return await GetProjectBoqVersionAsync(projectId, versionId);
        }

        if (!string.Equals(version.Status, ProjectBoqVersionStatuses.Draft, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(version.Status, ProjectBoqVersionStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"BoQ v{version.VersionNumber} must be Draft or Rejected before submission (current status: '{version.Status}').");
        }

        var policy = await RequireEffectiveBoqApprovalPolicyAsync();
        await ValidateStoredBoqSnapshotIntegrityAsync(version);
        var workflowResult = await _workflowIntegrationService.SubmitAsync(
            QuantitySurveyWorkflowBindingRegistry.Boq,
            version.Id,
            policy.BoqWorkflowDefinitionId);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "The configured BoQ approval workflow could not be started.");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Boq);
        adapter.ApplySubmitOutcome(version, workflowResult, userId);
        version.WorkflowInstanceId = workflowResult.ExecutionResult.WorkflowInstanceId;
        version.WorkflowDefinitionId = policy.BoqWorkflowDefinitionId;
        version.SubmittedById = userId;
        version.SubmittedAt = DateTime.UtcNow;
        version.AuditAction = QuantitySurveyAuditEventMap.SubmitBoqVersion;
        SetBoqVersionAuditContext(version, correlationId);

        if (workflowResult.Outcome == WorkflowOutcome.Approved)
        {
            await EnsureApprovedBoqPublicationAsync(version, userId, correlationId);
        }
        else
        {
            await _unitOfWork.Repository<ProjectBoqVersion>().UpdateAsync(version);
            await _unitOfWork.SaveChangesAsync();
        }

        return await GetProjectBoqVersionAsync(projectId, versionId);
    }

    public async Task<ProjectBoqVersionDetailDto> ApproveProjectBoqVersionAsync(
        Guid projectId,
        Guid versionId,
        Guid userId,
        string? comments,
        string correlationId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ApproveWorkflow);
        var version = await GetProjectBoqVersionEntityAsync(projectId, versionId);
        if (version.VersionType == QuantitySurveyBoqVersionType.Approved && version.PublishedAt.HasValue)
        {
            return await GetProjectBoqVersionAsync(projectId, versionId);
        }

        if (string.Equals(version.Status, ProjectBoqVersionStatuses.Approved, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureApprovedBoqPublicationAsync(version, userId, correlationId);
            return await GetProjectBoqVersionAsync(projectId, versionId);
        }

        if (!string.Equals(version.Status, ProjectBoqVersionStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"BoQ v{version.VersionNumber} must be PendingApproval before approval (current status: '{version.Status}').");
        }

        await ValidateStoredBoqSnapshotIntegrityAsync(version);
        var workflowStatus = await GetBoqWorkflowStatusAsync(version);
        WorkflowOutcome outcome;
        if (workflowStatus == WorkflowInstanceStatus.Completed)
        {
            outcome = WorkflowOutcome.Approved;
        }
        else
        {
            if (workflowStatus is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
            {
                throw new InvalidOperationException("The BoQ workflow has ended without approval and cannot be published.");
            }

            var canApprove = await _workflowIntegrationService.CanUserApproveAsync(
                QuantitySurveyWorkflowBindingRegistry.Boq,
                version.Id,
                userId);
            if (!canApprove)
            {
                throw new UnauthorizedAccessException("You are not assigned to the current BoQ workflow approval step.");
            }

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                QuantitySurveyWorkflowBindingRegistry.Boq,
                version.Id,
                userId,
                "Approve",
                NormalizeOptionalBoqComment(comments));
            if (!workflowResult.ExecutionResult.Success)
            {
                throw new InvalidOperationException(
                    workflowResult.ExecutionResult.Message ?? "The BoQ workflow approval could not be processed.");
            }

            version.WorkflowInstanceId ??= workflowResult.ExecutionResult.WorkflowInstanceId;
            outcome = workflowResult.Outcome;
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Boq);
        adapter.ApplyApprovalOutcome(version, outcome, userId, NormalizeOptionalBoqComment(comments));
        version.AuditAction = QuantitySurveyAuditEventMap.ApproveBoqVersion;
        SetBoqVersionAuditContext(version, correlationId);

        if (outcome == WorkflowOutcome.Approved)
        {
            await EnsureApprovedBoqPublicationAsync(version, userId, correlationId);
        }
        else
        {
            await _unitOfWork.Repository<ProjectBoqVersion>().UpdateAsync(version);
            await _unitOfWork.SaveChangesAsync();
        }

        return await GetProjectBoqVersionAsync(projectId, versionId);
    }

    public async Task<ProjectBoqVersionDetailDto> RejectProjectBoqVersionAsync(
        Guid projectId,
        Guid versionId,
        Guid userId,
        string reason,
        string correlationId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ApproveWorkflow);
        var rejectionReason = RequireBoqWorkflowReason(reason);
        var version = await GetProjectBoqVersionEntityAsync(projectId, versionId);
        if (!string.Equals(version.Status, ProjectBoqVersionStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"BoQ v{version.VersionNumber} must be PendingApproval before rejection (current status: '{version.Status}').");
        }

        var workflowStatus = await GetBoqWorkflowStatusAsync(version);
        if (workflowStatus == WorkflowInstanceStatus.Completed)
        {
            throw new InvalidOperationException("The completed BoQ workflow is approved and cannot be changed to a rejected outcome.");
        }

        WorkflowOutcome outcome;
        if (workflowStatus is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
        {
            outcome = WorkflowOutcome.Rejected;
        }
        else
        {
            var canApprove = await _workflowIntegrationService.CanUserApproveAsync(
                QuantitySurveyWorkflowBindingRegistry.Boq,
                version.Id,
                userId);
            if (!canApprove)
            {
                throw new UnauthorizedAccessException("You are not assigned to the current BoQ workflow approval step.");
            }

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                QuantitySurveyWorkflowBindingRegistry.Boq,
                version.Id,
                userId,
                "Reject",
                rejectionReason);
            if (!workflowResult.ExecutionResult.Success)
            {
                throw new InvalidOperationException(
                    workflowResult.ExecutionResult.Message ?? "The BoQ workflow rejection could not be processed.");
            }

            version.WorkflowInstanceId ??= workflowResult.ExecutionResult.WorkflowInstanceId;
            outcome = workflowResult.Outcome;
        }

        if (outcome != WorkflowOutcome.Rejected)
        {
            throw new InvalidOperationException("The shared workflow did not return a rejected outcome, so the BoQ was not marked rejected.");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Boq);
        adapter.ApplyApprovalOutcome(version, outcome, userId, rejectionReason);
        version.AuditAction = QuantitySurveyAuditEventMap.RejectBoqVersion;
        SetBoqVersionAuditContext(version, correlationId);
        await _unitOfWork.Repository<ProjectBoqVersion>().UpdateAsync(version);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectBoqVersionAsync(projectId, versionId);
    }

    public async Task<ProjectBoqVersionDetailDto> RecallProjectBoqVersionAsync(
        Guid projectId,
        Guid versionId,
        Guid userId,
        string reason,
        string correlationId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var recallReason = RequireBoqWorkflowReason(reason);
        var version = await GetProjectBoqVersionEntityAsync(projectId, versionId);
        if (!string.Equals(version.Status, ProjectBoqVersionStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"BoQ v{version.VersionNumber} must be PendingApproval before recall (current status: '{version.Status}').");
        }

        if (version.SubmittedById != userId)
        {
            throw new UnauthorizedAccessException("Only the user who submitted this BoQ version can recall it.");
        }

        var workflowResult = await _workflowIntegrationService.RecallAsync(
            QuantitySurveyWorkflowBindingRegistry.Boq,
            version.Id,
            userId,
            recallReason);
        if (!workflowResult.ExecutionResult.Success || workflowResult.Outcome != WorkflowOutcome.Recalled)
        {
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "The BoQ workflow could not be recalled.");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Boq);
        adapter.ApplyRecallOutcome(version, userId, recallReason);
        version.AuditAction = QuantitySurveyAuditEventMap.RecallBoqVersion;
        SetBoqVersionAuditContext(version, correlationId);
        await _unitOfWork.Repository<ProjectBoqVersion>().UpdateAsync(version);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectBoqVersionAsync(projectId, versionId);
    }

    private async Task<QsBoqVersionPolicyValue> RequireEffectiveBoqApprovalPolicyAsync()
    {
        var policy = await GetEffectiveBoqVersionPolicyAsync()
            ?? throw new InvalidOperationException(
                "No approved QS-DEC-003 BoQ version policy is effective for this tenant and date.");
        if (policy.BoqWorkflowDefinitionId == Guid.Empty)
        {
            throw new InvalidOperationException("The effective QS-DEC-003 policy has no BoQ workflow definition.");
        }

        if (!policy.RequireWorkflowBeforeUse || !policy.ApprovedVersionsImmutable)
        {
            throw new InvalidOperationException(
                "The effective QS-DEC-003 policy must require workflow approval before use and immutable approved publications.");
        }

        return policy;
    }

    private async Task<WorkflowInstanceStatus?> GetBoqWorkflowStatusAsync(ProjectBoqVersion version)
    {
        if (!version.WorkflowInstanceId.HasValue) return null;
        return (await _unitOfWork.Repository<WorkflowInstance>().FirstOrDefaultAsync(item =>
            item.TenantId == _currentUserProvider.TenantId
            && item.Id == version.WorkflowInstanceId.Value
            && item.EntityId == version.Id))?.Status;
    }

    private async Task ValidateStoredBoqSnapshotIntegrityAsync(ProjectBoqVersion version)
    {
        var lines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && item.ProjectId == version.ProjectId
                && item.ProjectBoqVersionId == version.Id))
            .ToList();
        if (lines.Count == 0 || lines.Count != version.LineCount)
        {
            throw new InvalidOperationException("The BoQ snapshot line count does not match its immutable control record.");
        }

        var monetaryCurrencies = lines
            .Where(item => item.LineAmount.HasValue && !string.IsNullOrWhiteSpace(item.Currency))
            .Select(item => item.Currency.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (monetaryCurrencies.Count > 1)
        {
            throw new InvalidOperationException(
                "A BoQ approval submission must use one controlled currency so workflow authority limits are evaluated consistently.");
        }

        var actualHash = ComputeBoqSnapshotHash(lines);
        if (!string.Equals(actualHash, version.SnapshotHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The BoQ snapshot integrity hash is invalid. Approval and publication are blocked.");
        }

        await ValidateRemeasurementVersionAsync(version);
    }

    private async Task EnsureApprovedBoqPublicationAsync(
        ProjectBoqVersion candidate,
        Guid approverUserId,
        string correlationId)
    {
        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.ApproveBoqVersion);
        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.PublishBoqVersion);
        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.RetireBoqPublication);
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var transactionStarted = false;
            try
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
                transactionStarted = true;
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"qs-boq-publication:{_currentUserProvider.TenantId:N}:{candidate.ProjectId:N}");

                var versions = (await _unitOfWork.Repository<ProjectBoqVersion>().FindAsync(item =>
                        item.TenantId == _currentUserProvider.TenantId
                        && item.ProjectId == candidate.ProjectId))
                    .OrderBy(item => item.VersionNumber)
                    .ToList();
                candidate = versions.Single(item => item.Id == candidate.Id);
                await ValidateStoredBoqSnapshotIntegrityAsync(candidate);

                var existingPublication = versions.FirstOrDefault(item =>
                    item.VersionType == QuantitySurveyBoqVersionType.Approved
                    && item.SourceVersionId == candidate.Id
                    && item.PublishedAt.HasValue);
                if (existingPublication != null)
                {
                    candidate.Status = ProjectBoqVersionStatuses.Approved;
                    candidate.ApprovalStatus = ProjectBoqVersionStatuses.Approved;
                    candidate.ApprovedById ??= approverUserId;
                    candidate.ApprovedAt ??= existingPublication.PublishedAt;
                    candidate.AuditAction = QuantitySurveyAuditEventMap.ApproveBoqVersion;
                    SetBoqVersionAuditContext(candidate, correlationId);
                    await _unitOfWork.Repository<ProjectBoqVersion>().UpdateAsync(candidate);
                    await _unitOfWork.SaveChangesAsync();
                    await ApplyApprovedRemeasurementToWorkingBoqAsync(candidate, correlationId);
                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();
                    transactionStarted = false;
                    return true;
                }

                var sourceLines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(item =>
                        item.TenantId == _currentUserProvider.TenantId
                        && item.ProjectId == candidate.ProjectId
                        && item.ProjectBoqVersionId == candidate.Id))
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.LineNumber)
                    .ToList();
                var now = DateTime.UtcNow;
                var publication = new ProjectBoqVersion
                {
                    TenantId = _currentUserProvider.TenantId,
                    ProjectId = candidate.ProjectId,
                    SourceVersionId = candidate.Id,
                    VersionNumber = versions.Select(item => item.VersionNumber).DefaultIfEmpty(0).Max() + 1,
                    VersionType = QuantitySurveyBoqVersionType.Approved,
                    Status = ProjectBoqVersionStatuses.Draft,
                    ApprovalStatus = ProjectBoqVersionStatuses.Draft,
                    WorkflowInstanceId = candidate.WorkflowInstanceId,
                    WorkflowDefinitionId = candidate.WorkflowDefinitionId,
                    SubmittedById = candidate.SubmittedById,
                    SubmittedAt = candidate.SubmittedAt,
                    ApprovedById = approverUserId,
                    ApprovedAt = now,
                    ChangeSummary = $"Approved publication of BoQ v{candidate.VersionNumber}: {candidate.ChangeSummary}",
                    AuditAction = QuantitySurveyAuditEventMap.PublishBoqVersion,
                    SnapshotHash = candidate.SnapshotHash,
                    LineCount = sourceLines.Count,
                    SnapshotAt = candidate.SnapshotAt,
                    ActorRoles = CurrentBoqActorRoles(),
                    CorrelationId = NormalizeCorrelationId(correlationId),
                    CreatedAt = now,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = approverUserId
                };
                await _unitOfWork.Repository<ProjectBoqVersion>().AddAsync(publication);
                foreach (var sourceLine in sourceLines)
                {
                    var line = CloneBoqPublicationLine(sourceLine, publication.Id, now);
                    await _unitOfWork.Repository<ProjectBoqVersionLine>().AddAsync(line);
                }
                await _unitOfWork.SaveChangesAsync();

                foreach (var prior in versions.Where(IsCurrentBoqPublication))
                {
                    prior.Status = ProjectBoqVersionStatuses.Retired;
                    prior.AuditAction = QuantitySurveyAuditEventMap.RetireBoqPublication;
                    SetBoqVersionAuditContext(prior, correlationId);
                    await _unitOfWork.Repository<ProjectBoqVersion>().UpdateAsync(prior);
                }
                await _unitOfWork.SaveChangesAsync();

                candidate.Status = ProjectBoqVersionStatuses.Approved;
                candidate.ApprovalStatus = ProjectBoqVersionStatuses.Approved;
                candidate.ApprovedById ??= approverUserId;
                candidate.ApprovedAt ??= now;
                candidate.RejectionReason = null;
                candidate.AuditAction = QuantitySurveyAuditEventMap.ApproveBoqVersion;
                SetBoqVersionAuditContext(candidate, correlationId);
                publication.Status = ProjectBoqVersionStatuses.Approved;
                publication.ApprovalStatus = ProjectBoqVersionStatuses.Approved;
                publication.PublishedById = approverUserId;
                publication.PublishedAt = now;
                publication.UpdatedBy = _currentUserProvider.Username;
                publication.LastModifiedById = approverUserId;
                await _unitOfWork.Repository<ProjectBoqVersion>().UpdateAsync(candidate);
                await _unitOfWork.Repository<ProjectBoqVersion>().UpdateAsync(publication);
                await _unitOfWork.SaveChangesAsync();
                await ApplyApprovedRemeasurementToWorkingBoqAsync(candidate, correlationId);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                transactionStarted = false;
                return true;
            }
            catch
            {
                try
                {
                    if (transactionStarted && _unitOfWork.HasActiveTransaction)
                    {
                        await _unitOfWork.RollbackAsync();
                    }
                }
                finally
                {
                    _unitOfWork.ClearTrackedChanges();
                }
                throw;
            }
        });
    }

    private ProjectBoqVersionLine CloneBoqPublicationLine(
        ProjectBoqVersionLine source,
        Guid publicationId,
        DateTime now)
        => new()
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = source.ProjectId,
            ProjectBoqVersionId = publicationId,
            LineKey = source.LineKey,
            SourceBoqItemId = source.SourceBoqItemId,
            ProjectPackageId = source.ProjectPackageId,
            PackageCode = source.PackageCode,
            PackageName = source.PackageName,
            SectionCode = source.SectionCode,
            SectionName = source.SectionName,
            TradeCode = source.TradeCode,
            TradeName = source.TradeName,
            CostCode = source.CostCode,
            CostCodeName = source.CostCodeName,
            MeasurementStandard = source.MeasurementStandard,
            MeasurementCode = source.MeasurementCode,
            MeasurementRule = source.MeasurementRule,
            LineNumber = source.LineNumber,
            ItemCode = source.ItemCode,
            ItemType = source.ItemType,
            Description = source.Description,
            Quantity = source.Quantity,
            UnitOfMeasure = source.UnitOfMeasure,
            UnitRate = source.UnitRate,
            LineAmount = source.LineAmount,
            Currency = source.Currency,
            SortOrder = source.SortOrder,
            CreatedAt = now,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

    private void SetBoqVersionAuditContext(ProjectBoqVersion version, string correlationId)
    {
        version.ActorRoles = CurrentBoqActorRoles();
        version.CorrelationId = NormalizeCorrelationId(correlationId);
        version.UpdatedBy = _currentUserProvider.Username;
        version.LastModifiedById = _currentUserProvider.UserId;
    }

    private string CurrentBoqActorRoles()
        => string.Join(',', _currentUserProvider.Roles.OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

    private static string? NormalizeOptionalBoqComment(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(normalized.Length, 2000)];
    }

    private static string RequireBoqWorkflowReason(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 5)
        {
            throw new InvalidOperationException("Enter a reason of at least 5 characters.");
        }
        return normalized[..Math.Min(normalized.Length, 2000)];
    }
}
