using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<QuantitySurveyEstimateWorkspaceDto> GetQuantitySurveyEstimateWorkspaceAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var versions = (await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.ProjectId == projectId && !value.IsDeleted))
            .OrderByDescending(value => value.EstimateType)
            .ThenByDescending(value => value.VersionNumber)
            .ToList();
        var result = new List<QuantitySurveyEstimateVersionDto>(versions.Count);
        foreach (var version in versions)
        {
            result.Add(await MapEstimateAsync(version));
        }

        var approvedBoqIds = (await _unitOfWork.Repository<ProjectBoqVersion>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId
                && value.ProjectId == projectId
                && value.VersionType == QuantitySurveyBoqVersionType.Approved
                && value.Status == ProjectBoqVersionStatuses.Approved
                && value.PublishedAt.HasValue
                && !value.IsDeleted))
            .OrderByDescending(value => value.VersionNumber)
            .Select(value => value.Id)
            .ToList();
        return new QuantitySurveyEstimateWorkspaceDto { Versions = result, ApprovedBoqVersionIds = approvedBoqIds };
    }

    public async Task<QuantitySurveyEstimateVersionDto> GetQuantitySurveyEstimateVersionAsync(Guid projectId, Guid estimateVersionId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapEstimateAsync(await GetEstimateEntityAsync(projectId, estimateVersionId));
    }

    public async Task<QuantitySurveyEstimateVersionDto> CreateQuantitySurveyEstimateVersionAsync(
        Guid projectId,
        CreateQuantitySurveyEstimateRequest dto,
        string correlationId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        ValidateEstimateRequest(dto);
        var existingRetry = await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().FirstOrDefaultAsync(value =>
            value.TenantId == _currentUserProvider.TenantId && value.ClientRequestId == dto.ClientRequestId);
        if (existingRetry != null)
        {
            if (existingRetry.ProjectId != projectId) throw new InvalidOperationException("The estimate request identifier is already used by another project.");
            await EnsureEstimateRetryMatchesAsync(existingRetry, dto);
            return await MapEstimateAsync(existingRetry);
        }

        var estimateId = await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var transactionStarted = false;
            try
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
                transactionStarted = true;
                await _unitOfWork.AcquireTransactionLockAsync($"qs-estimate:{_currentUserProvider.TenantId:N}:{projectId:N}:{(int)dto.EstimateType}");

                var retry = await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().FirstOrDefaultAsync(value =>
                    value.TenantId == _currentUserProvider.TenantId && value.ClientRequestId == dto.ClientRequestId);
                if (retry != null)
                {
                    if (retry.ProjectId != projectId) throw new InvalidOperationException("The estimate request identifier is already used by another project.");
                    await EnsureEstimateRetryMatchesAsync(retry, dto);
                    await _unitOfWork.CommitAsync();
                    transactionStarted = false;
                    return retry.Id;
                }

                var versions = (await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().FindAsync(value =>
                        value.TenantId == _currentUserProvider.TenantId
                        && value.ProjectId == projectId
                        && value.EstimateType == dto.EstimateType
                        && !value.IsDeleted))
                    .OrderBy(value => value.VersionNumber)
                    .ToList();
                if (versions.Any(value => value.Status is QuantitySurveyEstimateStatuses.Draft or QuantitySurveyEstimateStatuses.PendingApproval or QuantitySurveyEstimateStatuses.Rejected))
                {
                    throw new InvalidOperationException($"Complete the open {dto.EstimateType} version before creating another one.");
                }

                QuantitySurveyEstimateVersion? sourceEstimate = null;
                if (versions.Count > 0)
                {
                    if (!dto.SourceEstimateVersionId.HasValue) throw new InvalidOperationException("Select the current approved estimate as the source for this revision.");
                    sourceEstimate = versions.SingleOrDefault(value => value.Id == dto.SourceEstimateVersionId.Value)
                        ?? throw new InvalidOperationException("The selected source estimate does not belong to this project and estimate family.");
                    if (sourceEstimate.Status != QuantitySurveyEstimateStatuses.Approved)
                    {
                        throw new InvalidOperationException("A new estimate revision must be based on the current approved estimate.");
                    }
                    var currentApproved = versions.Where(value => value.Status == QuantitySurveyEstimateStatuses.Approved).OrderByDescending(value => value.VersionNumber).FirstOrDefault();
                    if (currentApproved?.Id != sourceEstimate.Id) throw new InvalidOperationException("Select the latest approved estimate as the source version.");
                }
                else if (dto.SourceEstimateVersionId.HasValue)
                {
                    throw new InvalidOperationException("The first estimate version cannot reference an earlier estimate.");
                }

                var boq = await _unitOfWork.Repository<ProjectBoqVersion>().FirstOrDefaultAsync(value =>
                    value.TenantId == _currentUserProvider.TenantId
                    && value.ProjectId == projectId
                    && value.Id == dto.ProjectBoqVersionId
                    && value.VersionType == QuantitySurveyBoqVersionType.Approved
                    && value.Status == ProjectBoqVersionStatuses.Approved
                    && value.PublishedAt.HasValue
                    && !value.IsDeleted)
                    ?? throw new InvalidOperationException("Select an approved, published BoQ version from this project.");
                var boqLines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(value =>
                        value.TenantId == _currentUserProvider.TenantId
                        && value.ProjectId == projectId
                        && value.ProjectBoqVersionId == boq.Id
                        && !value.IsDeleted))
                    .OrderBy(value => value.SortOrder)
                    .ThenBy(value => value.LineNumber)
                    .ToList();
                if (boqLines.Count == 0) throw new InvalidOperationException("The selected approved BoQ has no lines to estimate.");
                var currencies = boqLines.Where(value => value.LineAmount.HasValue).Select(value => value.Currency.Trim().ToUpperInvariant()).Where(value => value.Length > 0).Distinct().ToList();
                if (currencies.Count != 1) throw new InvalidOperationException("The selected BoQ must contain one controlled currency before an estimate can be prepared.");
                var currency = await _unitOfWork.Repository<Currency>().FirstOrDefaultAsync(value =>
                    value.TenantId == _currentUserProvider.TenantId && value.CurrencyCode == currencies[0] && value.IsActive && !value.IsDeleted)
                    ?? throw new InvalidOperationException($"The BoQ currency '{currencies[0]}' is not an active Finance currency for this tenant.");

                var project = await _unitOfWork.Repository<Project>().FirstOrDefaultAsync(value =>
                    value.TenantId == _currentUserProvider.TenantId && value.Id == projectId && !value.IsDeleted)
                    ?? throw new InvalidOperationException("The project is not available in the current tenant.");
                var projectAssets = await _unitOfWork.Repository<EstateManagedAsset>().FindAsync(value =>
                    value.TenantId == _currentUserProvider.TenantId
                    && value.ProjectId == projectId
                    && !value.IsDeleted);
                var sourceSnapshot = QuantitySurveyEstimateSourceSnapshotBuilder.Capture(project, projectAssets);

                var (profile, policyDecision, policy, markupDecision, markupPolicy) = await RequireEffectiveEstimatePolicyAsync();
                var normalizedMarkups = ValidateEstimateMarkups(dto.Markups, markupPolicy);
                CentralDocumentVersion? evidence = null;
                if (dto.CentralDocumentVersionId.HasValue)
                {
                    evidence = await _unitOfWork.Repository<CentralDocumentVersion>().FirstOrDefaultAsync(value =>
                        value.TenantId == _currentUserProvider.TenantId && value.Id == dto.CentralDocumentVersionId.Value && !value.IsDeleted)
                        ?? throw new InvalidOperationException("The selected supporting document version is not available in the tenant DMS repository.");
                }

                var now = DateTime.UtcNow;
                var publishedRates = (await _unitOfWork.Repository<QuantitySurveyRateLibraryRate>().FindAsync(value =>
                        value.TenantId == _currentUserProvider.TenantId
                        && value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published
                        && value.EffectiveFrom <= (dto.EstimateDate ?? now)
                        && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= (dto.EstimateDate ?? now))
                        && value.CurrencyId == currency.Id
                        && !value.IsDeleted))
                    .ToList();
                var rateItems = (await _unitOfWork.Repository<QuantitySurveyRateLibraryItem>().FindAsync(value =>
                        value.TenantId == _currentUserProvider.TenantId && value.IsActive && !value.IsDeleted))
                    .ToDictionary(value => value.Id);

                var entity = new QuantitySurveyEstimateVersion
                {
                    TenantId = _currentUserProvider.TenantId,
                    ProjectId = projectId,
                    ProjectBoqVersionId = boq.Id,
                    SourceEstimateVersionId = sourceEstimate?.Id,
                    ClientRequestId = dto.ClientRequestId,
                    VersionNumber = versions.Select(value => value.VersionNumber).DefaultIfEmpty(0).Max() + 1,
                    EstimateType = dto.EstimateType,
                    Name = dto.Name.Trim(),
                    EstimateDate = (dto.EstimateDate ?? now).Date,
                    CurrencyId = currency.Id,
                    CurrencyCodeSnapshot = currency.CurrencyCode,
                    FundingSourceSnapshot = sourceSnapshot.FundingSource,
                    PropertyReferenceSnapshot = sourceSnapshot.PropertyReference,
                    SourceSnapshotSchemaVersion = QuantitySurveyEstimateSourceSnapshotBuilder.CurrentSchemaVersion,
                    Status = QuantitySurveyEstimateStatuses.Draft,
                    ApprovalStatus = QuantitySurveyEstimateStatuses.Draft,
                    ChangeReason = dto.ChangeReason.Trim(),
                    ConfigurationProfileId = profile.Id,
                    ConfigurationDecisionId = policyDecision.Id,
                    ConfigurationProfileVersion = profile.Version,
                    CentralDocumentRecordId = evidence?.DocumentRecordId,
                    CentralDocumentVersionId = evidence?.Id,
                    AuditAction = QuantitySurveyAuditEventMap.CreateEstimateVersion,
                    CorrelationId = NormalizeEstimateCorrelationId(correlationId),
                    ActorRoles = CurrentEstimateActorRoles(),
                    CreatedAt = now,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                };
                await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().AddAsync(entity);

                var lines = new List<QuantitySurveyEstimateLine>(boqLines.Count);
                var sequence = 0;
                foreach (var boqLine in boqLines)
                {
                    rateItems.TryGetValue(publishedRates.Where(value => rateItems.TryGetValue(value.RateLibraryItemId, out var item)
                            && !string.IsNullOrWhiteSpace(boqLine.ItemCode)
                            && string.Equals(item.Code, boqLine.ItemCode, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(value => value.EffectiveFrom)
                        .ThenByDescending(value => value.Version)
                        .Select(value => value.RateLibraryItemId)
                        .FirstOrDefault(), out var matchedItem);
                    var sourceRate = matchedItem == null ? null : publishedRates
                        .Where(value => value.RateLibraryItemId == matchedItem.Id)
                        .OrderByDescending(value => value.EffectiveFrom)
                        .ThenByDescending(value => value.Version)
                        .FirstOrDefault();
                    var unitRate = sourceRate?.UnitRate ?? boqLine.UnitRate
                        ?? throw new InvalidOperationException($"BoQ line '{boqLine.LineNumber ?? boqLine.Description}' has neither a published matching rate nor a BoQ unit rate.");
                    var line = new QuantitySurveyEstimateLine
                    {
                        TenantId = _currentUserProvider.TenantId,
                        EstimateVersionId = entity.Id,
                        Sequence = ++sequence,
                        ProjectBoqVersionLineId = boqLine.Id,
                        SourceRateId = sourceRate?.Id,
                        LineNumberSnapshot = boqLine.LineNumber ?? sequence.ToString(),
                        ItemCodeSnapshot = boqLine.ItemCode,
                        DescriptionSnapshot = boqLine.Description,
                        UnitOfMeasureSnapshot = boqLine.UnitOfMeasure,
                        Quantity = boqLine.Quantity,
                        UnitRate = unitRate,
                        LineAmount = decimal.Round(boqLine.Quantity * unitRate, 2, MidpointRounding.AwayFromZero),
                        SourceRateItemCodeSnapshot = matchedItem?.Code,
                        SourceRateVersionSnapshot = sourceRate?.Version,
                        RateSourceSnapshot = sourceRate == null ? "ApprovedBoQ" : sourceRate.SourceType.ToString(),
                        CreatedAt = now,
                        CreatedBy = _currentUserProvider.Username,
                        CreatedById = _currentUserProvider.UserId
                    };
                    lines.Add(line);
                    await _unitOfWork.Repository<QuantitySurveyEstimateLine>().AddAsync(line);
                }

                var assumptions = dto.Assumptions.Select((value, index) => new QuantitySurveyEstimateAssumption
                {
                    TenantId = _currentUserProvider.TenantId,
                    EstimateVersionId = entity.Id,
                    Sequence = index + 1,
                    Code = value.Code.Trim().ToUpperInvariant(),
                    Description = value.Description.Trim(),
                    Value = value.Value.Trim(),
                    Unit = string.IsNullOrWhiteSpace(value.Unit) ? null : value.Unit.Trim(),
                    CreatedAt = now,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                }).ToList();
                foreach (var assumption in assumptions) await _unitOfWork.Repository<QuantitySurveyEstimateAssumption>().AddAsync(assumption);

                entity.DirectCost = lines.Sum(value => value.LineAmount);
                var markups = normalizedMarkups.Select((value, index) => new QuantitySurveyEstimateMarkup
                {
                    TenantId = _currentUserProvider.TenantId,
                    EstimateVersionId = entity.Id,
                    Sequence = index + 1,
                    Component = value.Component,
                    Percentage = value.Percentage,
                    BasisAmount = entity.DirectCost,
                    Amount = decimal.Round(entity.DirectCost * value.Percentage / 100m, 2, MidpointRounding.AwayFromZero),
                    CreatedAt = now,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                }).ToList();
                foreach (var markup in markups) await _unitOfWork.Repository<QuantitySurveyEstimateMarkup>().AddAsync(markup);
                entity.MarkupTotal = markups.Sum(value => value.Amount);
                entity.TotalAmount = entity.DirectCost + entity.MarkupTotal;
                entity.LineCount = lines.Count;
                entity.AssumptionCount = assumptions.Count;
                entity.MarkupCount = markups.Count;
                entity.SnapshotHash = ComputeEstimateHash(entity, lines, assumptions, markups);
                await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().UpdateAsync(entity);
                await AddEstimateRevisionAsync(entity, QuantitySurveyAuditEventMap.CreateEstimateVersion, null, null, SerializeEstimate(entity), correlationId);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                transactionStarted = false;
                return entity.Id;
            }
            catch
            {
                try { if (transactionStarted && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(); }
                finally { _unitOfWork.ClearTrackedChanges(); }
                throw;
            }
        });
        return await GetQuantitySurveyEstimateVersionAsync(projectId, estimateId);
    }

    public async Task<QuantitySurveyEstimateVersionDto> SubmitQuantitySurveyEstimateVersionAsync(Guid projectId, Guid estimateVersionId, Guid userId, string correlationId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var entity = await GetEstimateEntityAsync(projectId, estimateVersionId);
        if (entity.Status is not (QuantitySurveyEstimateStatuses.Draft or QuantitySurveyEstimateStatuses.Rejected))
            throw new InvalidOperationException("Only a Draft or Rejected estimate can be submitted.");
        await ValidateEstimateIntegrityAsync(entity);
        var (_, _, policy, _, _) = await RequireEffectiveEstimatePolicyAsync();
        var before = SerializeEstimate(entity);
        var result = await _workflowIntegrationService.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Estimate, entity.Id, policy.EstimateWorkflowDefinitionId);
        if (!result.ExecutionResult.Success) throw new InvalidOperationException(result.ExecutionResult.Message ?? "The configured estimate workflow could not be started.");
        _workflowStatusAdapterRegistry.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Estimate).ApplySubmitOutcome(entity, result, userId);
        // Persist an auto-approved workflow as PendingApproval first. FinalizeEstimateApprovalAsync
        // retires the previous approved version under the same serializable family lock before it
        // promotes this version, avoiding the filtered unique-index race. A completed workflow can
        // then be retried safely if finalization is interrupted.
        if (result.Outcome == WorkflowOutcome.Approved)
        {
            entity.Status = QuantitySurveyEstimateStatuses.PendingApproval;
            entity.ApprovalStatus = "Pending";
            entity.ApprovedById = null;
            entity.ApprovedAt = null;
        }
        entity.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        entity.WorkflowDefinitionId = policy.EstimateWorkflowDefinitionId;
        entity.SubmittedById = userId;
        entity.SubmittedAt = DateTime.UtcNow;
        SetEstimateAuditContext(entity, QuantitySurveyAuditEventMap.SubmitEstimateVersion, correlationId);
        await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().UpdateAsync(entity);
        await AddEstimateRevisionAsync(entity, QuantitySurveyAuditEventMap.SubmitEstimateVersion, null, before, SerializeEstimate(entity), correlationId);
        await _unitOfWork.SaveChangesAsync();
        if (result.Outcome == WorkflowOutcome.Approved) await FinalizeEstimateApprovalAsync(entity, userId, null, correlationId);
        return await GetQuantitySurveyEstimateVersionAsync(projectId, estimateVersionId);
    }

    public async Task<QuantitySurveyEstimateVersionDto> ApproveQuantitySurveyEstimateVersionAsync(Guid projectId, Guid estimateVersionId, Guid userId, string? comments, string correlationId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ApproveWorkflow);
        var entity = await GetEstimateEntityAsync(projectId, estimateVersionId);
        if (entity.Status == QuantitySurveyEstimateStatuses.Approved) return await MapEstimateAsync(entity);
        if (entity.Status != QuantitySurveyEstimateStatuses.PendingApproval) throw new InvalidOperationException("The estimate must be PendingApproval before approval.");
        await ValidateEstimateIntegrityAsync(entity);
        var workflowStatus = await GetEstimateWorkflowStatusAsync(entity);
        WorkflowOutcome outcome;
        if (workflowStatus == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
        else
        {
            if (workflowStatus is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) throw new InvalidOperationException("The estimate workflow ended without approval.");
            if (!await _workflowIntegrationService.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Estimate, entity.Id, userId)) throw new UnauthorizedAccessException("You are not assigned to the current estimate approval step.");
            var result = await _workflowIntegrationService.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Estimate, entity.Id, userId, "Approve", NormalizeEstimateComment(comments));
            if (!result.ExecutionResult.Success) throw new InvalidOperationException(result.ExecutionResult.Message ?? "The estimate approval could not be processed.");
            outcome = result.Outcome;
        }
        if (outcome == WorkflowOutcome.Approved) await FinalizeEstimateApprovalAsync(entity, userId, comments, correlationId);
        return await GetQuantitySurveyEstimateVersionAsync(projectId, estimateVersionId);
    }

    public async Task<QuantitySurveyEstimateVersionDto> RejectQuantitySurveyEstimateVersionAsync(Guid projectId, Guid estimateVersionId, Guid userId, string reason, string correlationId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ApproveWorkflow);
        var normalizedReason = RequireEstimateReason(reason);
        var entity = await GetEstimateEntityAsync(projectId, estimateVersionId);
        if (entity.Status != QuantitySurveyEstimateStatuses.PendingApproval) throw new InvalidOperationException("The estimate must be PendingApproval before rejection.");
        var workflowStatus = await GetEstimateWorkflowStatusAsync(entity);
        if (workflowStatus == WorkflowInstanceStatus.Completed) throw new InvalidOperationException("The completed estimate workflow is approved and cannot be rejected.");
        WorkflowOutcome outcome;
        if (workflowStatus is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
        else
        {
            if (!await _workflowIntegrationService.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Estimate, entity.Id, userId)) throw new UnauthorizedAccessException("You are not assigned to the current estimate approval step.");
            var result = await _workflowIntegrationService.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Estimate, entity.Id, userId, "Reject", normalizedReason);
            if (!result.ExecutionResult.Success) throw new InvalidOperationException(result.ExecutionResult.Message ?? "The estimate rejection could not be processed.");
            outcome = result.Outcome;
        }
        if (outcome != WorkflowOutcome.Rejected) throw new InvalidOperationException("The shared workflow did not return a rejected outcome.");
        var before = SerializeEstimate(entity);
        _workflowStatusAdapterRegistry.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Estimate).ApplyApprovalOutcome(entity, outcome, userId, normalizedReason);
        SetEstimateAuditContext(entity, QuantitySurveyAuditEventMap.RejectEstimateVersion, correlationId);
        await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().UpdateAsync(entity);
        await AddEstimateRevisionAsync(entity, QuantitySurveyAuditEventMap.RejectEstimateVersion, normalizedReason, before, SerializeEstimate(entity), correlationId);
        await _unitOfWork.SaveChangesAsync();
        return await GetQuantitySurveyEstimateVersionAsync(projectId, estimateVersionId);
    }

    private async Task FinalizeEstimateApprovalAsync(QuantitySurveyEstimateVersion entity, Guid userId, string? comments, string correlationId)
    {
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var transactionStarted = false;
            try
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
                transactionStarted = true;
                await _unitOfWork.AcquireTransactionLockAsync($"qs-estimate:{_currentUserProvider.TenantId:N}:{entity.ProjectId:N}:{(int)entity.EstimateType}");

                var current = await GetEstimateEntityAsync(entity.ProjectId, entity.Id);
                if (current.Status == QuantitySurveyEstimateStatuses.Approved)
                {
                    await _unitOfWork.CommitAsync();
                    transactionStarted = false;
                    return;
                }

                var before = SerializeEstimate(current);
                var prior = await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().FindAsync(value =>
                    value.TenantId == _currentUserProvider.TenantId && value.ProjectId == current.ProjectId && value.EstimateType == current.EstimateType
                    && value.Id != current.Id && value.Status == QuantitySurveyEstimateStatuses.Approved && !value.IsDeleted);
                foreach (var item in prior)
                {
                    item.Status = QuantitySurveyEstimateStatuses.Retired;
                    item.AuditAction = QuantitySurveyAuditEventMap.RetireEstimateVersion;
                    item.CorrelationId = NormalizeEstimateCorrelationId(correlationId);
                    item.UpdatedBy = _currentUserProvider.Username;
                    item.LastModifiedById = _currentUserProvider.UserId;
                    await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().UpdateAsync(item);
                    await AddEstimateRevisionAsync(item, QuantitySurveyAuditEventMap.RetireEstimateVersion, "Superseded by an approved estimate version.", null, SerializeEstimate(item), correlationId);
                }

                // Retire first so SQL Server never observes two rows satisfying the current-approved
                // filtered unique index. Both saves remain atomic within this transaction.
                if (prior.Any()) await _unitOfWork.SaveChangesAsync();

                _workflowStatusAdapterRegistry.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Estimate)
                    .ApplyApprovalOutcome(current, WorkflowOutcome.Approved, userId, NormalizeEstimateComment(comments));
                SetEstimateAuditContext(current, QuantitySurveyAuditEventMap.ApproveEstimateVersion, correlationId);
                await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().UpdateAsync(current);
                await AddEstimateRevisionAsync(current, QuantitySurveyAuditEventMap.ApproveEstimateVersion, comments, before, SerializeEstimate(current), correlationId);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                transactionStarted = false;
            }
            catch
            {
                try { if (transactionStarted && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(); }
                finally { _unitOfWork.ClearTrackedChanges(); }
                throw;
            }
        });
    }

    private async Task<(QuantitySurveyConfigurationProfile Profile, QuantitySurveyConfigurationDecision PolicyDecision, QsBoqVersionPolicyValue Policy, QuantitySurveyConfigurationDecision MarkupDecision, QsRateBuildUpValue MarkupPolicy)> RequireEffectiveEstimatePolicyAsync()
    {
        var now = DateTime.UtcNow;
        var profiles = await _unitOfWork.Repository<QuantitySurveyConfigurationProfile>().FindAsync(value =>
            value.TenantId == _currentUserProvider.TenantId && value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published
            && value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= now) && !value.IsDeleted);
        var profile = profiles.OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).FirstOrDefault()
            ?? throw new InvalidOperationException("No published QS configuration profile is effective for this tenant and date.");
        var decisions = await _unitOfWork.Repository<QuantitySurveyConfigurationDecision>().FindAsync(value =>
            value.TenantId == _currentUserProvider.TenantId && value.ProfileId == profile.Id
            && (value.DecisionKey == "QS-DEC-003" || value.DecisionKey == "QS-DEC-004")
            && value.Status == QuantitySurveyConfigurationDecisionStatus.Approved
            && value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved
            && (!value.EffectiveFrom.HasValue || value.EffectiveFrom.Value <= now)
            && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= now) && !value.IsDeleted);
        var policyDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-003")
            ?? throw new InvalidOperationException("No approved QS-DEC-003 estimate workflow policy is effective.");
        var markupDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-004")
            ?? throw new InvalidOperationException("No approved QS-DEC-004 markup policy is effective.");
        var policy = JsonSerializer.Deserialize<QsBoqVersionPolicyValue>(policyDecision.ValueJson, QuantitySurveyDecisionJsonOptions)
            ?? throw new InvalidOperationException("The effective QS-DEC-003 policy is invalid.");
        var markup = JsonSerializer.Deserialize<QsRateBuildUpValue>(markupDecision.ValueJson, QuantitySurveyDecisionJsonOptions)
            ?? throw new InvalidOperationException("The effective QS-DEC-004 policy is invalid.");
        if (policy.EstimateWorkflowDefinitionId == Guid.Empty || !policy.RequireWorkflowBeforeUse || !policy.ApprovedVersionsImmutable)
            throw new InvalidOperationException("QS-DEC-003 must configure an estimate workflow, approval before use, and immutable approved versions.");
        return (profile, policyDecision, policy, markupDecision, markup);
    }

    private static IReadOnlyList<QuantitySurveyEstimateMarkupRequest> ValidateEstimateMarkups(IReadOnlyList<QuantitySurveyEstimateMarkupRequest> markups, QsRateBuildUpValue policy)
    {
        var allowed = new HashSet<QuantitySurveyRateComponent> { QuantitySurveyRateComponent.Overhead, QuantitySurveyRateComponent.Profit, QuantitySurveyRateComponent.Contingency, QuantitySurveyRateComponent.Wastage };
        if (markups.Any(value => !allowed.Contains(value.Component))) throw new InvalidOperationException("Estimate markups are limited to the controlled overhead, profit, contingency, and wastage components.");
        if (markups.GroupBy(value => value.Component).Any(group => group.Count() > 1)) throw new InvalidOperationException("Each estimate markup component can be entered only once.");
        foreach (var markup in markups)
        {
            var maximum = markup.Component switch
            {
                QuantitySurveyRateComponent.Overhead => policy.MaximumOverheadPercent,
                QuantitySurveyRateComponent.Profit => policy.MaximumProfitPercent,
                QuantitySurveyRateComponent.Contingency => policy.MaximumContingencyPercent,
                QuantitySurveyRateComponent.Wastage => policy.MaximumWastagePercent,
                _ => 0m
            };
            if (markup.Percentage < 0m || markup.Percentage > maximum) throw new InvalidOperationException($"{markup.Component} must be between 0 and the configured maximum of {maximum}%.");
        }
        return markups.Where(value => value.Percentage > 0m).OrderBy(value => (int)value.Component).ToList();
    }

    private async Task ValidateEstimateIntegrityAsync(QuantitySurveyEstimateVersion entity)
    {
        var lines = (await _unitOfWork.Repository<QuantitySurveyEstimateLine>().FindAsync(value => value.TenantId == _currentUserProvider.TenantId && value.EstimateVersionId == entity.Id && !value.IsDeleted)).OrderBy(value => value.Sequence).ToList();
        var assumptions = (await _unitOfWork.Repository<QuantitySurveyEstimateAssumption>().FindAsync(value => value.TenantId == _currentUserProvider.TenantId && value.EstimateVersionId == entity.Id && !value.IsDeleted)).OrderBy(value => value.Sequence).ToList();
        var markups = (await _unitOfWork.Repository<QuantitySurveyEstimateMarkup>().FindAsync(value => value.TenantId == _currentUserProvider.TenantId && value.EstimateVersionId == entity.Id && !value.IsDeleted)).OrderBy(value => value.Sequence).ToList();
        if (lines.Count != entity.LineCount || assumptions.Count != entity.AssumptionCount || markups.Count != entity.MarkupCount) throw new InvalidOperationException("The estimate snapshot counts do not match its immutable control record.");
        if (entity.DirectCost != lines.Sum(value => value.LineAmount) || entity.MarkupTotal != markups.Sum(value => value.Amount) || entity.TotalAmount != entity.DirectCost + entity.MarkupTotal) throw new InvalidOperationException("The estimate snapshot totals failed integrity validation.");
        if (!string.Equals(entity.SnapshotHash, ComputeEstimateHash(entity, lines, assumptions, markups), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The estimate snapshot integrity hash is invalid.");
    }

    private async Task EnsureEstimateRetryMatchesAsync(
        QuantitySurveyEstimateVersion existing,
        CreateQuantitySurveyEstimateRequest request)
    {
        var headerMatches = existing.ProjectBoqVersionId == request.ProjectBoqVersionId
            && existing.SourceEstimateVersionId == request.SourceEstimateVersionId
            && existing.EstimateType == request.EstimateType
            && string.Equals(existing.Name, request.Name.Trim(), StringComparison.Ordinal)
            && string.Equals(existing.ChangeReason, request.ChangeReason.Trim(), StringComparison.Ordinal)
            && existing.CentralDocumentVersionId == request.CentralDocumentVersionId
            && (!request.EstimateDate.HasValue || existing.EstimateDate.Date == request.EstimateDate.Value.Date);
        if (!headerMatches)
            throw new InvalidOperationException("The client request identifier was already used with a different estimate payload.");

        var storedAssumptions = (await _unitOfWork.Repository<QuantitySurveyEstimateAssumption>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.EstimateVersionId == existing.Id && !value.IsDeleted))
            .OrderBy(value => value.Sequence)
            .Select(value => (value.Code, value.Description, value.Value, value.Unit))
            .ToList();
        var requestedAssumptions = request.Assumptions
            .Select(value => (
                value.Code.Trim().ToUpperInvariant(),
                value.Description.Trim(),
                value.Value.Trim(),
                string.IsNullOrWhiteSpace(value.Unit) ? null : value.Unit.Trim()))
            .ToList();
        if (!storedAssumptions.SequenceEqual(requestedAssumptions))
            throw new InvalidOperationException("The client request identifier was already used with different estimate assumptions.");

        var storedMarkups = (await _unitOfWork.Repository<QuantitySurveyEstimateMarkup>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.EstimateVersionId == existing.Id && !value.IsDeleted))
            .OrderBy(value => (int)value.Component)
            .Select(value => (value.Component, value.Percentage))
            .ToList();
        var requestedMarkups = request.Markups
            .Where(value => value.Percentage > 0m)
            .OrderBy(value => (int)value.Component)
            .Select(value => (value.Component, value.Percentage))
            .ToList();
        if (!storedMarkups.SequenceEqual(requestedMarkups))
            throw new InvalidOperationException("The client request identifier was already used with different estimate markups.");
    }

    private async Task<QuantitySurveyEstimateVersion> GetEstimateEntityAsync(Guid projectId, Guid id)
        => await _unitOfWork.Repository<QuantitySurveyEstimateVersion>().FirstOrDefaultAsync(value => value.TenantId == _currentUserProvider.TenantId && value.ProjectId == projectId && value.Id == id && !value.IsDeleted)
           ?? throw new InvalidOperationException("The requested estimate version was not found in this project.");

    private async Task<WorkflowInstanceStatus?> GetEstimateWorkflowStatusAsync(QuantitySurveyEstimateVersion entity)
        => !entity.WorkflowInstanceId.HasValue ? null : (await _unitOfWork.Repository<WorkflowInstance>().FirstOrDefaultAsync(value => value.TenantId == _currentUserProvider.TenantId && value.Id == entity.WorkflowInstanceId.Value && value.EntityId == entity.Id))?.Status;

    private async Task<QuantitySurveyEstimateVersionDto> MapEstimateAsync(QuantitySurveyEstimateVersion entity)
    {
        var lines = (await _unitOfWork.Repository<QuantitySurveyEstimateLine>().FindAsync(value => value.TenantId == _currentUserProvider.TenantId && value.EstimateVersionId == entity.Id && !value.IsDeleted)).OrderBy(value => value.Sequence).ToList();
        var assumptions = (await _unitOfWork.Repository<QuantitySurveyEstimateAssumption>().FindAsync(value => value.TenantId == _currentUserProvider.TenantId && value.EstimateVersionId == entity.Id && !value.IsDeleted)).OrderBy(value => value.Sequence).ToList();
        var markups = (await _unitOfWork.Repository<QuantitySurveyEstimateMarkup>().FindAsync(value => value.TenantId == _currentUserProvider.TenantId && value.EstimateVersionId == entity.Id && !value.IsDeleted)).OrderBy(value => value.Sequence).ToList();
        var history = (await _unitOfWork.Repository<QuantitySurveyEstimateRevision>().FindAsync(value => value.TenantId == _currentUserProvider.TenantId && value.EstimateVersionId == entity.Id && !value.IsDeleted)).OrderBy(value => value.CreatedAt).ToList();
        return new QuantitySurveyEstimateVersionDto
        {
            Id = entity.Id, ProjectId = entity.ProjectId, ProjectBoqVersionId = entity.ProjectBoqVersionId, SourceEstimateVersionId = entity.SourceEstimateVersionId,
            VersionNumber = entity.VersionNumber, EstimateType = entity.EstimateType, Name = entity.Name, EstimateDate = entity.EstimateDate,
            CurrencyId = entity.CurrencyId, CurrencyCode = entity.CurrencyCodeSnapshot,
            FundingSource = entity.FundingSourceSnapshot, PropertyReference = entity.PropertyReferenceSnapshot, SourceSnapshotSchemaVersion = entity.SourceSnapshotSchemaVersion,
            DirectCost = entity.DirectCost, MarkupTotal = entity.MarkupTotal, TotalAmount = entity.TotalAmount,
            Status = entity.Status, ApprovalStatus = entity.ApprovalStatus, WorkflowInstanceId = entity.WorkflowInstanceId, WorkflowDefinitionId = entity.WorkflowDefinitionId,
            SubmittedById = entity.SubmittedById, SubmittedAt = entity.SubmittedAt, ApprovedById = entity.ApprovedById, ApprovedAt = entity.ApprovedAt, RejectionReason = entity.RejectionReason,
            ChangeReason = entity.ChangeReason, SnapshotHash = entity.SnapshotHash, ConfigurationProfileId = entity.ConfigurationProfileId, ConfigurationProfileVersion = entity.ConfigurationProfileVersion,
            CentralDocumentVersionId = entity.CentralDocumentVersionId, CorrelationId = entity.CorrelationId, RowVersion = entity.RowVersion,
            Lines = lines.Select(value => new QuantitySurveyEstimateLineDto { Id = value.Id, Sequence = value.Sequence, ProjectBoqVersionLineId = value.ProjectBoqVersionLineId, SourceRateId = value.SourceRateId, LineNumber = value.LineNumberSnapshot, ItemCode = value.ItemCodeSnapshot, Description = value.DescriptionSnapshot, UnitOfMeasure = value.UnitOfMeasureSnapshot, Quantity = value.Quantity, UnitRate = value.UnitRate, LineAmount = value.LineAmount, SourceRateItemCode = value.SourceRateItemCodeSnapshot, SourceRateVersion = value.SourceRateVersionSnapshot, RateSource = value.RateSourceSnapshot }).ToList(),
            Assumptions = assumptions.Select(value => new QuantitySurveyEstimateAssumptionDto(value.Id, value.Sequence, value.Code, value.Description, value.Value, value.Unit)).ToList(),
            Markups = markups.Select(value => new QuantitySurveyEstimateMarkupDto(value.Id, value.Sequence, value.Component, value.Percentage, value.BasisAmount, value.Amount)).ToList(),
            ApprovalHistory = history.Select(value => new QuantitySurveyEstimateHistoryDto(value.Id, value.Action, value.ActorUserId, value.ActorName, value.ActorRoles, value.CorrelationId, value.Reason, value.CreatedAt)).ToList()
        };
    }

    private async Task AddEstimateRevisionAsync(QuantitySurveyEstimateVersion entity, string action, string? reason, string? before, string? after, string correlationId)
    {
        QuantitySurveyAuditEventMap.GetRequired(action);
        await _unitOfWork.Repository<QuantitySurveyEstimateRevision>().AddAsync(new QuantitySurveyEstimateRevision
        {
            TenantId = _currentUserProvider.TenantId, EstimateVersionId = entity.Id, Action = action, ActorUserId = _currentUserProvider.UserId,
            ActorName = _currentUserProvider.Username, ActorRoles = CurrentEstimateActorRoles(), CorrelationId = NormalizeEstimateCorrelationId(correlationId),
            Reason = NormalizeEstimateComment(reason), BeforeJson = before, AfterJson = after, CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId
        });
    }

    private static string ComputeEstimateHash(QuantitySurveyEstimateVersion entity, IEnumerable<QuantitySurveyEstimateLine> lines, IEnumerable<QuantitySurveyEstimateAssumption> assumptions, IEnumerable<QuantitySurveyEstimateMarkup> markups)
    {
        var canonical = entity.SourceSnapshotSchemaVersion == 0
            ? JsonSerializer.Serialize(new
            {
                entity.ProjectId, entity.ProjectBoqVersionId, entity.SourceEstimateVersionId, entity.VersionNumber, entity.EstimateType, entity.Name, entity.EstimateDate,
                entity.CurrencyId, entity.CurrencyCodeSnapshot, entity.DirectCost, entity.MarkupTotal, entity.TotalAmount, entity.ConfigurationProfileId, entity.ConfigurationDecisionId, entity.ConfigurationProfileVersion,
                Lines = lines.OrderBy(value => value.Sequence).Select(value => new { value.Sequence, value.ProjectBoqVersionLineId, value.SourceRateId, value.LineNumberSnapshot, value.ItemCodeSnapshot, value.DescriptionSnapshot, value.UnitOfMeasureSnapshot, value.Quantity, value.UnitRate, value.LineAmount, value.SourceRateItemCodeSnapshot, value.SourceRateVersionSnapshot, value.RateSourceSnapshot }),
                Assumptions = assumptions.OrderBy(value => value.Sequence).Select(value => new { value.Sequence, value.Code, value.Description, value.Value, value.Unit }),
                Markups = markups.OrderBy(value => value.Sequence).Select(value => new { value.Sequence, value.Component, value.Percentage, value.BasisAmount, value.Amount })
            })
            : JsonSerializer.Serialize(new
        {
            entity.ProjectId, entity.ProjectBoqVersionId, entity.SourceEstimateVersionId, entity.VersionNumber, entity.EstimateType, entity.Name, entity.EstimateDate,
            entity.CurrencyId, entity.CurrencyCodeSnapshot, entity.FundingSourceSnapshot, entity.PropertyReferenceSnapshot, entity.SourceSnapshotSchemaVersion,
            entity.DirectCost, entity.MarkupTotal, entity.TotalAmount, entity.ConfigurationProfileId, entity.ConfigurationDecisionId, entity.ConfigurationProfileVersion,
            Lines = lines.OrderBy(value => value.Sequence).Select(value => new { value.Sequence, value.ProjectBoqVersionLineId, value.SourceRateId, value.LineNumberSnapshot, value.ItemCodeSnapshot, value.DescriptionSnapshot, value.UnitOfMeasureSnapshot, value.Quantity, value.UnitRate, value.LineAmount, value.SourceRateItemCodeSnapshot, value.SourceRateVersionSnapshot, value.RateSourceSnapshot }),
            Assumptions = assumptions.OrderBy(value => value.Sequence).Select(value => new { value.Sequence, value.Code, value.Description, value.Value, value.Unit }),
            Markups = markups.OrderBy(value => value.Sequence).Select(value => new { value.Sequence, value.Component, value.Percentage, value.BasisAmount, value.Amount })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string SerializeEstimate(QuantitySurveyEstimateVersion value) => JsonSerializer.Serialize(new
    {
        value.Id, value.VersionNumber, value.EstimateType, value.Name, value.FundingSourceSnapshot,
        value.PropertyReferenceSnapshot, value.SourceSnapshotSchemaVersion, value.DirectCost, value.MarkupTotal,
        value.TotalAmount, value.Status, value.ApprovalStatus, value.WorkflowInstanceId, value.SnapshotHash
    });
    private void SetEstimateAuditContext(QuantitySurveyEstimateVersion entity, string action, string correlationId) { QuantitySurveyAuditEventMap.GetRequired(action); entity.AuditAction = action; entity.CorrelationId = NormalizeEstimateCorrelationId(correlationId); entity.ActorRoles = CurrentEstimateActorRoles(); entity.UpdatedBy = _currentUserProvider.Username; entity.LastModifiedById = _currentUserProvider.UserId; }
    private string CurrentEstimateActorRoles() => string.Join(',', _currentUserProvider.Roles.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
    private static string NormalizeEstimateCorrelationId(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static string? NormalizeEstimateComment(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 2000)];
    private static string RequireEstimateReason(string value) => string.IsNullOrWhiteSpace(value) || value.Trim().Length < 5 ? throw new InvalidOperationException("Enter a reason of at least 5 characters.") : value.Trim()[..Math.Min(value.Trim().Length, 2000)];
    private static void ValidateEstimateRequest(CreateQuantitySurveyEstimateRequest dto)
    {
        if (dto.ClientRequestId == Guid.Empty) throw new InvalidOperationException("A client request identifier is required for safe retry handling.");
        if (dto.ProjectBoqVersionId == Guid.Empty) throw new InvalidOperationException("Select an approved project BoQ version.");
        if (!Enum.IsDefined(dto.EstimateType)) throw new InvalidOperationException("Select a recognized estimate type.");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidOperationException("Estimate name is required.");
        if (string.IsNullOrWhiteSpace(dto.ChangeReason) || dto.ChangeReason.Trim().Length < 5) throw new InvalidOperationException("Enter a change reason of at least 5 characters.");
        if (dto.Assumptions is null) throw new InvalidOperationException("Estimate assumptions must be supplied as a list.");
        if (dto.Markups is null) throw new InvalidOperationException("Estimate markups must be supplied as a list.");
        if (dto.Assumptions.GroupBy(value => value.Code.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) throw new InvalidOperationException("Assumption codes must be unique within an estimate version.");
    }
}
